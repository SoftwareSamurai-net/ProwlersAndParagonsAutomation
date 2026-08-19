// One character, belonging to one account.
//
// **Nothing here reads a field of a character.** The payload arrives as JSON, is checked for
// being JSON and for being small enough, and is written down verbatim; a read hands the same
// bytes back. That is not laziness — the engine is the authority on what a character is, it
// runs in the browser, and a second place that understood the shape would be a second place to
// keep in step with it. The one thing the server is allowed to know is who it belongs to.

import * as db from './db.js';
import { fail, noContent, readJson, sameOrigin } from './http.js';

/** The stored character, byte for byte, or 404. */
export async function read(request, env, deps, user) {
    const row = await db.getCharacter(env.DB, user.id);
    if (!row) return fail(404, 'This account has no character stored.');

    // The stored text is returned as the body rather than nested inside another object, so the
    // shape on the wire is the shape in local storage. The two stores then agree by
    // construction instead of by a mapping somebody has to maintain.
    return new Response(row.payload, {
        headers: { 'content-type': 'application/json; charset=utf-8' },
    });
}

/**
 * Write the character down.
 *
 * The cap and the JSON check are the whole of the validation, and both are about this server
 * rather than about the character: one keeps somebody from filling the database with a file,
 * the other keeps it from storing something no build could ever read back. Whether the
 * character is *legal* is a question for the engine, on the other side of the wire, and asking
 * it here would need the rules — which is exactly the dependency this project does not have.
 */
export async function write(request, env, deps, user) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That is not a character this server can store.');

    await db.putCharacter(env.DB, { userId: user.id, payload: body.text, now: deps.now() });

    return noContent();
}

/** Throw the stored character away. Absent is not an error: the end state is the same. */
export async function remove(request, env, deps, user) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    await db.deleteCharacter(env.DB, user.id);

    return noContent();
}
