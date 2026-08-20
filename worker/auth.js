// Who somebody is, and how they come to be it.
//
// The whole model is: prove you can read an address, then hold a cookie for a month. There is
// no password to lose, no third-party identity provider, and nothing in the browser that an
// injected script could read.

import { hash } from './crypto.js';
import * as db from './db.js';
import { normaliseEmail } from './email.js';
import { mayHaveAnAccount } from './invitations.js';
import {
    clearSessionCookie, fail, json, noContent, readJson, sameOrigin, sessionCookie,
    setSessionCookie,
} from './http.js';

/** A link is good for fifteen minutes. Long enough to walk to another machine; short as a leak. */
export const TOKEN_LIFETIME_MS = 15 * 60 * 1000;

/** A session lasts a month. Long enough not to be a nuisance, short enough to end by itself. */
export const SESSION_LIFETIME_MS = 30 * 24 * 60 * 60 * 1000;

/** Per address, per hour. Enough for somebody who mistypes and retries; not enough to be a weapon. */
export const LINKS_PER_ADDRESS_PER_HOUR = 5;

/** Per source address, per hour. The limit that stops one machine mailing a thousand people. */
export const LINKS_PER_CLIENT_PER_HOUR = 20;

const HOUR_SECONDS = 60 * 60;

/**
 * Ask for a sign-in link.
 *
 * It answers 204 whether or not anything was sent, and that is deliberate. A reply that
 * distinguished "sent" from "there is no such account" would turn this endpoint into a way of
 * asking whether somebody has an account here, one address at a time. So a refused rate limit,
 * an address with no account, and a link actually in flight are one answer.
 *
 * The one thing that does *not* answer 204 is the mail provider refusing the send: that is this
 * site being broken, and reporting it as success leaves somebody watching an inbox for a
 * message nobody tried to deliver.
 */
export async function requestLink(request, env, deps) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    const email = normaliseEmail(body?.value?.email);
    if (!email) return fail(400, 'That does not look like an email address.');

    const now = deps.now();
    await db.sweepExpired(env.DB, now);

    const seconds = Math.floor(now / 1000);
    const addressKey = 'email:' + email;
    const clientKey = 'ip:' + clientAddress(request);
    const byAddress = await db.countAttempt(env.DB,
        { key: addressKey, now: seconds, windowSeconds: HOUR_SECONDS });
    const byClient = await db.countAttempt(env.DB,
        { key: clientKey, now: seconds, windowSeconds: HOUR_SECONDS });

    // Silently, for the reason above: a caller must not be able to tell a limit from a send.
    if (byAddress > LINKS_PER_ADDRESS_PER_HOUR || byClient > LINKS_PER_CLIENT_PER_HOUR) {
        return noContent();
    }

    // **An address nobody invited gets the same answer as one that was, and no mail.**
    // This site is not a sign-up: an account is what puts the rulebook's own text on screen, so
    // who may have one is a decision rather than a form. The answer is `204` either way for the
    // same reason a rate-limited request is — anything else makes this endpoint a way of asking
    // who is on the list, one address at a time.
    //
    // **It is deliberately above the deployment check below.** A stranger probing a site whose
    // `SITE_URL` is missing would otherwise get a 500 where an invited address gets one too,
    // which says nothing about the list — but the reverse ordering also means the owner of a
    // broken deployment is told about it whichever address he tries, and that is worth more.
    //
    // **What this does not hide is time.** An invited address waits on a call to the mail
    // provider and an uninvited one returns immediately, so somebody willing to measure can
    // still tell them apart. Closing that would mean padding every refusal to the length of a
    // send, which trades a real defence — the list itself — for the appearance of one. Recorded
    // rather than fixed.
    if (!await mayHaveAnAccount(env, email)) return noContent();

    // **The link's domain comes from configuration, and this refuses rather than guessing.**
    // It used to fall back to the origin of the request — which is derived from the host the
    // request arrived on, and `sameOrigin` above checks the *Origin header against that host*
    // rather than validating the host itself. So on a deployment where more than one hostname
    // routes here (a Pages preview alias, a custom domain mid-change), a caller who could
    // influence the effective host got a link minted for it — and the link carries the raw
    // token, because that is the credential. Refusing costs a 500 and a clear message on a
    // misconfigured deployment; guessing costs somebody their account.
    if (!env.SITE_URL) {
        console.error('SITE_URL is not set, so no sign-in link can be addressed. See docs/ACCOUNTS-SETUP.md.');

        return fail(500, 'This site is not configured to send sign-in links.');
    }

    const token = deps.newSecret();
    await db.putLoginToken(env.DB, {
        tokenHash: await hash(token),
        email,
        expiresAt: now + TOKEN_LIFETIME_MS,
    });

    try {
        await deps.sendSignInLink(env, {
            to: email,
            link: env.SITE_URL.replace(/\/+$/, '') + '/signin?t=' + token,
        });
    } catch (error) {
        // **A send that failed has to give the attempt back, or the failure stops being
        // reported.** The two answers this endpoint gives are deliberately indistinguishable:
        // rate limited and sent are both 204, so that nobody can ask it whether an address has
        // an account here. That is right, and it is also what turns a broken mail provider into
        // a lie — five refusals spend the allowance, and every try after that answers "a link
        // is on its way" for the rest of the hour while nothing has been sent all day. It is
        // the shape that hides itself: somebody retries *because* nothing arrived, and
        // retrying is what silences the 500 that would have named the fault. It cost this
        // deployment its first sign-in, and neither the mail provider nor Cloudflare had
        // anything to show for it, because by then nothing was being attempted.
        //
        // So an attempt is spent on a message, not on a request. The limit still bounds the
        // mail one address or one machine can cause, because a message that was caused is a
        // message the provider accepted. What it no longer bounds is requests against a
        // provider refusing all of them — which cost a write and a refused API call each, and
        // buy back the only signal there is that something at this end is broken.
        await db.refundAttempt(env.DB, addressKey);
        await db.refundAttempt(env.DB, clientKey);

        throw error;
    }

    return noContent();
}

/**
 * Spend a link and become somebody.
 *
 * Every refusal says the same thing, whether the token was never issued, has expired, or has
 * already been used. There is nothing a legitimate visitor does with the difference, and quite
 * a lot somebody guessing tokens would.
 */
export async function verify(request, env, deps) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    const token = typeof body?.value?.token === 'string' ? body.value.token : '';
    if (!token) return fail(400, 'No sign-in token was sent.');

    const now = deps.now();
    const email = await db.spendLoginToken(env.DB, { tokenHash: await hash(token), now });
    if (!email) return fail(401, 'That sign-in link is not usable. Ask for another.');

    // **Asked again here, and not only when the link was sent.** A link lasts fifteen minutes,
    // which is long enough for an invitation to be withdrawn in — and the token is spent by the
    // statement above whether or not this passes, so a withdrawn address cannot hold a live link
    // in reserve. The refusal is the same sentence every other one on this route gives.
    if (!await mayHaveAnAccount(env, email)) {
        return fail(401, 'That sign-in link is not usable. Ask for another.');
    }

    const user = await db.upsertUser(env.DB, {
        id: deps.newUserId(),
        email,
        displayName: email.slice(0, email.indexOf('@')),
        now,
    });

    const secret = deps.newSecret();
    await db.createSession(env.DB, {
        idHash: await hash(secret),
        userId: user.id,
        expiresAt: now + SESSION_LIFETIME_MS,
        now,
    });

    return json(identityOf(user), {
        headers: { 'set-cookie': setSessionCookie(secret, SESSION_LIFETIME_MS / 1000) },
    });
}

/**
 * Who the caller is, or 401.
 *
 * 401 is the ordinary answer for a visitor with no account, not an error condition — the front
 * end reads it as "anonymous" and carries on. Nothing about it is worth logging.
 */
export async function me(request, env, deps) {
    const user = await currentUser(request, env, deps);

    return user ? json(identityOf(user)) : fail(401, 'Nobody is signed in.');
}

/**
 * Stop being signed in.
 *
 * The row goes as well as the cookie. Clearing only the cookie leaves a live session behind for
 * anybody who copied it, which makes signing out a gesture rather than an act.
 */
export async function signOut(request, env, deps) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const secret = sessionCookie(request);
    if (secret) await db.deleteSession(env.DB, await hash(secret));

    return noContent({ 'set-cookie': clearSessionCookie() });
}

/**
 * The signed-in user behind a request, or null.
 *
 * Shared by every endpoint that needs one, so there is one answer to "who is this" rather than
 * one per route. A route that forgets to call it is a route with no authentication at all,
 * which is why the router hands the user to the endpoints that need one instead of letting
 * each fetch its own.
 */
export async function currentUser(request, env, deps) {
    const secret = sessionCookie(request);
    if (!secret) return null;

    return await db.sessionUser(env.DB, { idHash: await hash(secret), now: deps.now() });
}

/**
 * What the browser is told about somebody.
 *
 * A key and a name, and nothing else. It matches `Identity` on the other side of the wire
 * exactly, and the omissions are the design: no claims, no token, no expiry, no email. An
 * address on this response would end up in every log the front end's host keeps, for no feature.
 */
function identityOf(user) {
    return { key: user.id, displayName: user.display_name };
}

/**
 * Who is asking, for rate-limiting purposes only.
 *
 * `CF-Connecting-IP` is set by Cloudflare's own edge and cannot be written by the client behind
 * it — unlike `X-Forwarded-For`, which is a header anybody may send and which is therefore not
 * consulted. An unknown source counts as one bucket rather than none: no header must not mean
 * no limit.
 */
function clientAddress(request) {
    return request.headers.get('cf-connecting-ip') || 'unknown';
}
