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
import { newSecret, newUserId } from './crypto.js';
import { fail } from './http.js';
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
        // The message is not passed on. An exception from D1 or from the mail provider can
        // quote a query or an address, and this is the one place where such a string would be
        // handed to whoever asked for it.
        console.error('Unhandled failure in the accounts API:', error);

        return fail(500, 'Something went wrong at this end.');
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
