// Who may have an account here, and the page that decides.
//
// **This is an allow-list, not a sign-up.** Every other shape of gate — a shared secret, an
// invite code in a link — is a credential that travels, and this site already has exactly one
// way of proving who somebody is: they can read an address. So the gate is a list of addresses,
// checked before a link is minted, and the answer to an address that is not on it is the same
// `204` every other outcome gives.
//
// **Nothing here holds a rule about characters and nothing here may gain one.** It stores
// addresses and a flag; the engine still runs in the browser and still decides everything about
// a character.

import { INVITATION_TOKEN_LIFETIME_MS } from './auth.js';
import * as db from './db.js';
import { normaliseEmail } from './email.js';
import { kindOf, redact } from './errors.js';
import { fail, json, noContent, readJson, sameOrigin } from './http.js';
import { mintSignInToken, signInLink } from './tokens.js';

/** `i_` plus 22 URL-safe characters — the shape of every other id this server mints. */
const ID_PATTERN = /^i_[A-Za-z0-9_-]{22}$/;

/**
 * The bootstrap administrator's address, normalised, or null.
 *
 * <p><b>An environment variable rather than a row, because the first row is impossible.</b>
 * Managing the list needs an account, an account needs an invitation, and an invitation needs
 * somebody to have added it. Something outside the database has to break that circle, and the
 * dashboard is where this deployment's other three settings already live.</p>
 *
 * <p>It is deliberately not written into the table on first use either. A variable that is
 * still the answer is a variable somebody can change when the owner's address changes; a row
 * copied from it once is a stale duplicate nobody remembers exists.</p>
 */
export function bootstrapAdmin(env) {
    const value = env.ADMIN_EMAIL;

    return typeof value === 'string' && value.includes('@') ? value.trim().toLowerCase() : null;
}

/**
 * Whether this address may ask for a sign-in link.
 *
 * <p>The bootstrap administrator is always allowed and is never in the table, so removing every
 * row locks nobody out of their own site — and a deployment that has set no `ADMIN_EMAIL` allows
 * nobody at all, which is the direction a mistake here should fail in.</p>
 */
export async function mayHaveAnAccount(env, email) {
    if (email === bootstrapAdmin(env)) return true;

    return await db.invitationFor(env.DB, email) !== null;
}

/**
 * Whether this user may manage the list.
 *
 * <p>Asked per request rather than carried on the session, because it is the answer that has to
 * be current: withdrawing somebody's administrator flag through the page below must take effect
 * on their next request, not when their month-old cookie expires.</p>
 */
export async function isAdministrator(env, user) {
    if (user.email === bootstrapAdmin(env)) return true;

    const invitation = await db.invitationFor(env.DB, user.email);

    return invitation?.grants_admin === 1;
}

/**
 * Everyone who may have an account, for the page that manages them.
 *
 * <p>The bootstrap administrator is included and marked, because a list that silently omitted
 * the one address that always works would be a list somebody would try to add — and adding it
 * would make a second, removable answer to a question that already has an unremovable one.</p>
 */
export async function list(request, env, deps, user) {
    const rows = await db.listInvitations(env.DB);
    const bootstrap = bootstrapAdmin(env);

    const invitations = rows.map(row => ({
        id: row.id,
        email: row.email,
        grantsAdmin: row.grants_admin === 1,
        hasSignedIn: row.user_id !== null,
        createdAt: row.created_at,
        removable: true,
    }));

    if (bootstrap) {
        invitations.unshift({
            id: null,
            email: bootstrap,
            grantsAdmin: true,
            hasSignedIn: rows.some(r => r.email === bootstrap && r.user_id !== null),
            createdAt: null,

            // Not a permission this page granted, so not one it can take away. The lever is the
            // environment variable, which is a deploy rather than a click — deliberately, since
            // it is the address that can never be locked out.
            removable: false,
        });
    }

    return json({ you: user.email, invitations });
}

/**
 * Let one more address have an account.
 *
 * <p>Answers the row it made, so the page does not have to guess an id or re-read the list.</p>
 *
 * <p><b>A newly-added address is mailed a one-click link, and a withdrawn or already-listed one
 * is not.</b> The row is what grants the permission — a token is only ever a shortcut to using
 * it — so this writes the row first and mails second — a mail failure must not cost the
 * invitation, only the one click mail was for; the address can still ask for an ordinary link
 * afterwards. See below for what happens when the send itself fails.</p>
 */
export async function add(request, env, deps, user) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const email = normaliseEmail(body.value?.email);
    if (!email) return fail(400, 'That does not look like an email address.');

    // Not a refusal: the address can already have an account, which is the end state asked for.
    // Reporting a conflict would make the page's own list a thing to reconcile before every
    // click, and there is nothing here to lose by saying yes twice. Nothing is mailed either —
    // the address was told once, when it was first added.
    if (email === bootstrapAdmin(env)) {
        return json({ invitation: null, alreadyAllowed: true });
    }

    const existing = await db.invitationFor(env.DB, email);
    if (existing) return json({ invitation: shape(existing), alreadyAllowed: true });

    const invitation = await db.addInvitation(env.DB, {
        id: deps.newInvitationId(),
        email,
        grantsAdmin: body.value?.grantsAdmin === true ? 1 : 0,
        invitedBy: user.id,
        now: deps.now(),
    });

    // **Sent because an administrator chose this address on purpose, which is a different act
    // from the public endpoint that must never confirm one.** `POST /api/admin/invitations` is
    // reached only by somebody `isAdministrator` already lets manage this list — asking for a
    // sign-in link, by contrast, always answers `204` so a stranger cannot use it to learn who is
    // on the list one address at a time. Mailing here does not reopen that question: the caller
    // already knows the address exists, because they just typed it.
    const mailed = await tellThemTheyCanSignIn(env, deps, email);

    return json({ invitation: shape(invitation), alreadyAllowed: false, mailed });
}

/**
 * Tell somebody they were just added, and never let that cost the invitation above.
 *
 * <p><b>The row is already written by the time this runs.</b> A mail provider outage is this
 * site being broken in a smaller way than a failed sign-in — the address can still ask for a
 * link the ordinary way — so this reports the failure to the caller (`mailed: false`) rather
 * than rolling anything back or answering with a 500 that would leave the administrator thinking
 * nothing happened.</p>
 *
 * <p><b>Mints a real sign-in token, held longer than the public request path's.</b> Through the
 * same `mintSignInToken` and `signInLink` that path uses — see `INVITATION_TOKEN_LIFETIME_MS` on
 * `worker/auth.js` for why three days is an acceptable trade here and nowhere else. The token
 * is spent, one time, through the ordinary `/api/auth/verify` route; nothing about verifying it
 * differs from any other sign-in link.</p>
 *
 * <p><b>Minted even when the send is about to fail.</b> The row it writes is cheap, unread until
 * spent, and pruned by `sweepExpired` like any other; the alternative — minting only after
 * confirming the send will succeed — is not an option `fetch` offers, and reporting `mailed:
 * false` already tells the caller the address must ask for an ordinary link instead.</p>
 *
 * <p><b>Recorded to `error_log` too, deliberately.</b> A provider outage or a bad key breaks
 * every sign-in as well, silently, until somebody notices an inbox stayed empty — which is
 * exactly the failure `docs/ACCOUNTS-SETUP.md` describes costing this deployment its first
 * sign-in. Writing a row here means the owner can find the same fault from an invitation that
 * never delivered, not only from a visitor who complained. The category is `mail` without
 * needing `errors.js`'s `taggedMail` wrapper — there is nothing else this call could have
 * thrown from, so the subsystem is known without asking.</p>
 */
async function tellThemTheyCanSignIn(env, deps, email) {
    try {
        const token = await mintSignInToken(env, deps,
            { email, now: deps.now(), lifetimeMs: INVITATION_TOKEN_LIFETIME_MS });

        await deps.sendInvitationMail(env, { to: email, link: signInLink(env, token) });

        return true;
    } catch (error) {
        await recordMailFailure(env, deps, error);

        return false;
    }
}

/**
 * Write the failure down, and never throw doing it — the same promise `index.js`'s own logger
 * makes, for the same reason: the thing that just broke may be the database this writes to, and
 * this runs on a path that must still answer the administrator with a 200 either way.
 */
async function recordMailFailure(env, deps, error) {
    try {
        if (!env || !env.DB || typeof env.DB.prepare !== 'function') return;

        await db.recordFailure(env.DB, {
            category: 'mail',
            route: '/api/admin/invitations',
            kind: kindOf(error),
            detail: redact(error && error.message),
            reference: deps.newReference(),
            now: deps.now(),
        });
    } catch (secondary) {
        console.error('Could not record an invitation mail failure:', secondary);
    }
}

/**
 * Withdraw an invitation, and end the sessions it was holding open.
 *
 * <p><b>Deleting the row alone would be a gesture.</b> It stops the next link being sent and
 * does nothing about the month-long cookie the person is already holding, so somebody removed
 * from the list would keep reading the rulebook until it expired. Same reasoning as signing out
 * deleting the session row rather than only the cookie.</p>
 *
 * <p><b>The account and its characters stay.</b> Withdrawing permission to sign in is not the
 * same act as destroying somebody's work, and conflating them would make this button the most
 * dangerous control in the application. Adding the address again gives them back everything.</p>
 *
 * <p><b>You cannot remove your own.</b> It is the one click on this page that cannot be undone
 * from this page — the next request would be refused, including the request to put it back.</p>
 */
export async function remove(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not an invitation id this server uses.');

    const invitation = await db.invitationById(env.DB, id);
    if (!invitation) return noContent();

    if (invitation.email === user.email) {
        return fail(409, 'You cannot withdraw your own invitation; you would lose the page that '
            + 'puts it back.');
    }

    await db.removeInvitation(env.DB, id);
    await db.deleteSessionsFor(env.DB, invitation.email);

    return noContent();
}

/** One row, in the shape the page reads. */
function shape(row) {
    return {
        id: row.id,
        email: row.email,
        grantsAdmin: row.grants_admin === 1,
        hasSignedIn: false,
        createdAt: row.created_at,
        removable: true,
    };
}
