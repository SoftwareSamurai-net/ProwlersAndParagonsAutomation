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
};

/** Built once per isolate, not per request. */
const entries = index(CHAPTERS);

export async function handle(request, env, deps = production) {
    try {
        const response = await route(request, env, deps);

        // Nothing this server says is cacheable: every answer either depends on who is asking
        // or is a one-shot. A shared cache holding one of these would hand somebody else's
        // identity — or somebody's character — to the next visitor through the same edge.
        response.headers.set('cache-control', 'no-store');

        return response;
    } catch (error) {
        // **A reference, so a report and a log line can be joined up.**
        //
        // The message itself is still not passed on: an exception from D1 or from the mail
        // provider can quote a query or an address, and this is the one place where such a
        // string would be handed to whoever asked for it. But "something went wrong" with
        // nothing else in it means somebody reporting a failure and somebody reading the logs
        // have no way to find each other — and this server's only log is a live tail, so an
        // error nobody was watching for is simply gone. A short id in both places costs
        // nothing and is the difference between one grep and a guess.
        //
        // Random rather than derived from the request: anything derived would encode the
        // address or the path, which is the leak this whole branch exists to prevent. Six
        // base-36 characters is ~2 billion — plenty to tell apart the failures in one tail,
        // and it is deliberately not a token and grants nothing.
        const reference = Math.random().toString(36).slice(2, 8);

        console.error(`Unhandled failure in the accounts API [${reference}]:`, error);

        return fail(500, 'Something went wrong at this end.', { reference });
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
