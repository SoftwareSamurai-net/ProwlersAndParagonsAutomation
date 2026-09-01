// Every address this server answers, in one table.
//
// It is a hand-written router rather than one file per route under `functions/` for one
// reason: a file that is a route is a file that is reachable, and shared helpers living beside
// routed files is a shape where "is this reachable?" is answered by a naming convention. Here
// there is exactly one routed file — `functions/api/[[path]].js` — and everything else is a
// module that has to be wired in on purpose to be reachable at all.

import * as adminErrorLog from './adminErrorLog.js';
import * as auth from './auth.js';
import * as campaigns from './campaigns.js';
import * as characters from './characters.js';
import { CHAPTERS } from './corpus.js';
import { newInvitationId, newSecret, newUserId } from './crypto.js';
import * as db from './db.js';
import {
    categoryOf, kindOf, redact, routePattern, taggedMail, taggedStorage,
} from './errors.js';
import { fail } from './http.js';
import * as invitations from './invitations.js';
import * as memberships from './memberships.js';
import { sendInvitationMail, sendSignInLink } from './mail.js';
import { contents, index, passage, power, search } from './rulebook.js';
import { transcripts } from './transcripts.js';
import { TRANSCRIPTS } from './transcripts-corpus.js';

/**
 * Everything the handlers reach for that is not the database.
 *
 * Injected rather than imported at the point of use so the tests can hold the clock still,
 * make the secrets predictable, and read the email instead of sending it. The production set is
 * the default, so a route cannot accidentally run against a stub.
 */
export const production = {
    now: () => Date.now(),
    newSecret,
    newUserId,
    newInvitationId,
    sendSignInLink,
    sendInvitationMail,
    newReference,
};

/**
 * The six characters a report and a log line are joined up by.
 *
 * <p>Random rather than derived from the request: anything derived would encode the address or
 * the path, which is the leak this whole design exists to prevent. Six base-36 characters is
 * ~2 billion — plenty to tell apart the failures in one tail — and it is deliberately not a token
 * and grants nothing, which is why `Math.random` is acceptable here and nowhere else in this
 * directory. See the note in `crypto.js`.</p>
 *
 * <p>It is injected like the clock and the secrets so a test can hold it still. Two failures with
 * different references are two different bodies, and one of this slice's tests is that the same
 * subsystem failure produces byte-identical bodies for a registered and an unregistered
 * address.</p>
 */
function newReference() {
    return Math.random().toString(36).slice(2, 8);
}

/** Built once per isolate, not per request. */
const entries = index(CHAPTERS);

export async function handle(request, env, deps = production) {
    // **The two subsystems are wrapped here, which is what makes a category a thing decided in
    // one place.** `db.js` and `mail.js` know nothing about categories and must not learn: a
    // category assigned per throw site becomes a description of the internals by enumeration,
    // which is exactly the disclosure this design avoids. Everything that comes out of the
    // database is `storage`, everything out of the mail provider is `mail`, a missing binding or
    // setting is `configuration`, and anything else is honestly `unknown`.
    const guarded = { ...env, DB: taggedStorage(env.DB) };
    const guardedDeps = { ...deps, sendSignInLink: taggedMail(deps.sendSignInLink) };

    try {
        const response = await route(request, guarded, guardedDeps);

        // Nothing this server says is cacheable: every answer either depends on who is asking
        // or is a one-shot. A shared cache holding one of these would hand somebody else's
        // identity — or somebody's character — to the next visitor through the same edge.
        response.headers.set('cache-control', 'no-store');

        return response;
    } catch (error) {
        // **A category and a reference, because the two audiences want opposite things.**
        //
        // The message itself is still not passed on: an exception from D1 or from the mail
        // provider can quote a query or an address, and this is the one place where such a
        // string would be handed to whoever asked for it. What the visitor gets instead is the
        // category — enough to know whether to retry, wait, or report, and nothing else — and a
        // reference, so a report and a recorded row can find each other.
        //
        // **The category names the subsystem, never the request.** Asking for a link always
        // answers 204 precisely so the endpoint cannot be used to ask whether an address is
        // registered; a category that appeared only for known addresses would put that oracle
        // straight back through the error path. Nothing below reads the body, the address, or
        // whether a user row exists — and there is a test that requires byte-identical bodies
        // for a registered and an unregistered address failing the same way.
        const category = categoryOf(error);
        const reference = deps.newReference();

        // **The same four fields the table gets, computed once and shared with it** — a second
        // computation here could redact differently from the row a caller's reference points at.
        // One JSON object per line rather than a formatted sentence, so `wrangler pages
        // deployment tail` can filter and read it: the exception's own message never appears,
        // only what `redact` leaves of it, for the same reason the visitor is not shown it either
        // — a tail is exactly the artefact most likely to be pasted into an issue.
        const entry = {
            category,
            route: routePattern(request),
            kind: kindOf(error),
            detail: redact(error && error.message),
            reference,
        };

        console.error(JSON.stringify(entry));

        await record(env, { ...entry, now: deps.now() });

        return fail(500, 'Something went wrong at this end.', { reference, category });
    }
}

/**
 * Write the failure down for the owner, and never fail doing it.
 *
 * <p><b>The thing that just broke may well be the database this writes to.</b> A logger that
 * could throw out of the catch above would turn a storage outage into no response at all — the
 * visitor would lose the reference and the category that are the entire visitor-facing half of
 * this design. So every part of it is inside one try, and the fallback is the live tail that was
 * the only log before this table existed.</p>
 *
 * <p>The raw `env.DB` rather than the wrapped one: there is nothing left to classify here, and a
 * tag applied on the way out of a logger that already swallows everything would be decoration.</p>
 */
async function record(env, { category, route, kind, detail, reference, now }) {
    try {
        if (!env || !env.DB || typeof env.DB.prepare !== 'function') return;

        await db.recordFailure(env.DB, { category, route, kind, detail, reference, now });
    } catch (secondary) {
        console.error(`Could not record the failure [${reference}]:`, secondary);
    }
}

async function route(request, env, deps) {
    const path = new URL(request.url).pathname.replace(/\/+$/, '');
    const method = request.method === 'HEAD' ? 'GET' : request.method;

    if (path === '/api/auth/request') return only('POST', method, () => auth.requestLink(request, env, deps));
    if (path === '/api/auth/verify') return only('POST', method, () => auth.verify(request, env, deps));
    if (path === '/api/auth/signout') return only('POST', method, () => auth.signOut(request, env, deps));
    if (path === '/api/me') return only('GET', method, () => auth.me(request, env, deps));

    // Its own address rather than folded into `/api/me`, because the two need different
    // authentication: `/api/me` answers 401 for an anonymous visitor and that is not a failure,
    // while a name change with nobody signed in has nothing to change and is refused here before
    // `auth.setDisplayName` is asked to guess whose row that would be.
    if (path === '/api/me/display-name') {
        const user = await auth.currentUser(request, env, deps);
        if (!user) return fail(401, 'Sign in first.');

        return only('PUT', method, () => auth.setDisplayName(request, env, deps, user));
    }

    // Everything below needs somebody to be signed in, and asks once. A route that fetched its
    // own user would be a route that could forget to.
    if (path === '/api/characters' || path.startsWith('/api/characters/')
        || path === '/api/campaigns' || path.startsWith('/api/campaigns/')
        || path === '/api/memberships' || path.startsWith('/api/memberships/')
        || path.startsWith('/api/rulebook/') || path === '/api/transcripts') {
        const user = await auth.currentUser(request, env, deps);
        if (!user) return fail(401, 'Sign in first.');

        // The book, in four shapes: one Power's entry beside the editor, a search, what there is
        // to read, and one passage in full. **The prefix is what is gated, not the four
        // addresses** — a fifth added below the check but matched above it would be reachable by
        // anybody, and a list of addresses in a condition is exactly where that goes wrong.
        if (path === '/api/rulebook/power') return only('GET', method, () => power(request, entries));
        if (path === '/api/rulebook/search') return only('GET', method, () => search(request, CHAPTERS));
        if (path === '/api/rulebook/contents') return only('GET', method, () => contents(CHAPTERS));
        if (path === '/api/rulebook/passage') return only('GET', method, () => passage(request, CHAPTERS));

        if (path.startsWith('/api/rulebook/')) return fail(404, 'No such address.');

        // The four recorded conversations the portfolio replays, gated the same way and for the
        // same reason as the book: bundled into the server rather than staged into `wwwroot`, so
        // this is the only way in, and it asks who is calling before it answers.
        if (path === '/api/transcripts') return only('GET', method, () => transcripts(TRANSCRIPTS));

        if (path === '/api/characters') {
            return only('GET', method, () => characters.list(request, env, deps, user));
        }

        if (path === '/api/campaigns') {
            return only('GET', method, () => campaigns.list(request, env, deps, user));
        }

        // A campaign is another opaque blob beside a character: same four verbs, same
        // client-minted id, same refusal to look inside. **It is in this block rather than in one
        // of its own** because the gate is the thing being shared — "signed in, nothing more" —
        // and a second block asking the same question is a second block that could forget to.
        if (path.startsWith('/api/campaigns/')) {
            // **One sub-path, and it is a sub-path rather than a top-level address because it is a
            // property of one campaign.** `code` is the only thing under a campaign's id that is
            // not the campaign itself; everything else past the id is unrouted, which is a 404
            // rather than the 400 the id check below would give a caller who wrote `g_…/typo`.
            const [campaignId, ...rest] = path.slice('/api/campaigns/'.length).split('/');

            if (rest.length > 0) {
                if (rest.join('/') !== 'code') return fail(404, 'No such address.');

                return only('POST', method,
                    () => campaigns.rotateCode(request, env, deps, user, campaignId));
            }

            if (method === 'GET') return campaigns.read(request, env, deps, user, campaignId);
            if (method === 'PUT') return campaigns.write(request, env, deps, user, campaignId);
            if (method === 'DELETE') return campaigns.remove(request, env, deps, user, campaignId);

            return methodNotAllowed('GET, PUT, DELETE');
        }

        // **A campaign's clone of a character, and the snapshot waiting for a decision.** Same
        // gate as characters and campaigns, in the same block and for the same reason — a second
        // block asking the same question is a second block that could forget to.
        //
        // **The two list addresses answer different questions and are deliberately two.**
        // `/api/memberships` is the player's ("which of my characters are in a game, and where do
        // they stand"); `/api/memberships/inbox` is the GM's ("what is waiting for me"). One
        // address with a `role=` parameter would put the answer's meaning in a query string, where
        // a missing value has to default to one of the two — and defaulting to the wrong one is a
        // screen showing somebody else's half of the feature.
        if (path === '/api/memberships') {
            return only('GET', method, () => memberships.listMine(request, env, deps, user));
        }

        if (path.startsWith('/api/memberships/')) {
            const [membershipId, ...rest] = path.slice('/api/memberships/'.length).split('/');

            if (membershipId === 'inbox' && rest.length === 0) {
                return only('GET', method, () => memberships.inbox(request, env, deps, user));
            }

            if (membershipId === 'join' && rest.length === 0) {
                return only('POST', method, () => memberships.join(request, env, deps, user));
            }

            const tail = rest.join('/');

            // **The one address here that answers two verbs**, because ending a membership is
            // not a property of one — it is the membership itself, and `DELETE` on the thing is
            // the shape every other resource in this server uses.
            if (tail === '') {
                if (method === 'GET') {
                    return memberships.read(request, env, deps, user, membershipId);
                }

                if (method === 'DELETE') {
                    return memberships.leave(request, env, deps, user, membershipId);
                }

                return methodNotAllowed('GET, DELETE');
            }

            if (tail === 'submission') {
                return only('PUT', method,
                    () => memberships.submit(request, env, deps, user, membershipId));
            }

            if (tail === 'approve') {
                return only('POST', method,
                    () => memberships.approve(request, env, deps, user, membershipId));
            }

            if (tail === 'reject') {
                return only('POST', method,
                    () => memberships.reject(request, env, deps, user, membershipId));
            }

            return fail(404, 'No such address.');
        }

        // Everything past the prefix is the id, unvalidated here — each handler below checks
        // its shape, because a 400 for a malformed id and a 404 for a missing one both come
        // from knowing what a real id looks like, and only the handler needs to know that.
        const id = path.slice('/api/characters/'.length);

        if (method === 'GET') return characters.read(request, env, deps, user, id);
        if (method === 'PUT') return characters.write(request, env, deps, user, id);
        if (method === 'DELETE') return characters.remove(request, env, deps, user, id);

        return methodNotAllowed('GET, PUT, DELETE');
    }

    // **Managing the allow-list, and a wrong answer here is the whole site.** Everything
    // under this prefix is refused with the same 404 an unrouted address gets, rather than a
    // 403, so that an ordinary account cannot learn there is an administrator's page at all.
    // The check is a database read on every request rather than a claim on the session, because
    // withdrawing somebody's flag has to take effect on their next request and not when their
    // month-old cookie expires.
    if (path === '/api/admin/invitations' || path.startsWith('/api/admin/invitations/')
        || path === '/api/admin/error-log') {
        const user = await auth.currentUser(request, env, deps);
        if (!user) return fail(401, 'Sign in first.');
        if (!await invitations.isAdministrator(env, user)) return fail(404, 'No such address.');

        // **Gated by the same question above, not a second one.** Read-only — there is no verb
        // here beyond GET, because there is nothing to write. See `adminErrorLog.js`.
        if (path === '/api/admin/error-log') {
            return only('GET', method, () => adminErrorLog.list(request, env, deps, user));
        }

        if (path === '/api/admin/invitations') {
            if (method === 'GET') return invitations.list(request, env, deps, user);
            if (method === 'POST') return invitations.add(request, env, deps, user);

            return methodNotAllowed('GET, POST');
        }

        const id = path.slice('/api/admin/invitations/'.length);

        if (method === 'DELETE') return invitations.remove(request, env, deps, user, id);

        return methodNotAllowed('DELETE');
    }

    return fail(404, 'No such address.');
}

function only(allowed, method, handler) {
    return method === allowed ? handler() : methodNotAllowed(allowed);
}

function methodNotAllowed(allow) {
    const response = fail(405, 'That method is not allowed here.');
    response.headers.set('allow', allow);

    return response;
}
