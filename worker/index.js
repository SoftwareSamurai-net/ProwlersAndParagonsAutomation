// Every address this server answers, in one table.
//
// It is a hand-written router rather than one file per route under `functions/` for one
// reason: a file that is a route is a file that is reachable, and shared helpers living beside
// routed files is a shape where "is this reachable?" is answered by a naming convention. Here
// there is exactly one routed file — `functions/api/[[path]].js` — and everything else is a
// module that has to be wired in on purpose to be reachable at all.

import * as auth from './auth.js';
import * as characters from './characters.js';
import { CHAPTERS } from './corpus.js';
import { newInvitationId, newSecret, newUserId } from './crypto.js';
import * as db from './db.js';
import {
    categoryOf, kindOf, redact, routePattern, taggedMail, taggedStorage,
} from './errors.js';
import { fail } from './http.js';
import * as invitations from './invitations.js';
import { sendSignInLink } from './mail.js';
import { index, power } from './rulebook.js';

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

        console.error(
            `Unhandled failure in the accounts API [${reference}] (${category}):`, error);

        await record(env, { category, request, error, reference, now: deps.now() });

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
async function record(env, { category, request, error, reference, now }) {
    try {
        if (!env || !env.DB || typeof env.DB.prepare !== 'function') return;

        await db.recordFailure(env.DB, {
            category,
            route: routePattern(request),
            kind: kindOf(error),
            detail: redact(error && error.message),
            reference,
            now,
        });
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

    // Everything below needs somebody to be signed in, and asks once. A route that fetched its
    // own user would be a route that could forget to.
    if (path === '/api/characters' || path.startsWith('/api/characters/') || path === '/api/rulebook/power') {
        const user = await auth.currentUser(request, env, deps);
        if (!user) return fail(401, 'Sign in first.');

        if (path === '/api/rulebook/power') {
            return only('GET', method, () => power(request, entries));
        }

        if (path === '/api/characters') {
            return only('GET', method, () => characters.list(request, env, deps, user));
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
    if (path === '/api/admin/invitations' || path.startsWith('/api/admin/invitations/')) {
        const user = await auth.currentUser(request, env, deps);
        if (!user) return fail(401, 'Sign in first.');
        if (!await invitations.isAdministrator(env, user)) return fail(404, 'No such address.');

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
