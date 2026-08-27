// Campaigns, plural, belonging to one account.
//
// **This file is `characters.js` with a different table, and it must stay that boring.** The
// payload arrives as a JSON-encoded string inside the request body, is checked for being
// parseable JSON and for the whole request being small enough, and is written down verbatim; a
// read hands the same bytes back. **Nothing here reads a field of a campaign** — not the tier,
// not the Trait Cap, not the budget flag. The server does not know what a tier is and must never
// learn: a second place that understood a campaign is a second place to keep in step with the
// engine, which runs in the browser and is the authority on every one of those things.
//
// The only things this server is allowed to know about a campaign are who it belongs to, what it
// is called, and when it was last touched. See docs/CHARACTERS-API.md, which states the same
// invariant for a character and now states it for a campaign.

import * as db from './db.js';
import { fail, json, noContent, readJson, sameOrigin } from './http.js';

/**
 * `g_` plus 22 URL-safe characters — the character id shape with a different letter, so a
 * campaign id cannot be passed where a character id is meant and neither reaches a query
 * malformed. An id is a key in a table; the caller does not get to choose its format.
 */
const ID_PATTERN = /^g_[A-Za-z0-9_-]{22}$/;

const MAX_LABEL_LENGTH = 80;
const DEFAULT_LABEL = 'Unnamed campaign';

/**
 * The account's campaigns, most recently touched first.
 *
 * **No cap and no `limit`, unlike the character list.** `users.character_limit` is a cap on
 * characters; there is no campaign equivalent, and answering with one would be inventing a rule
 * rather than serving the contract.
 */
export async function list(request, env, deps, user) {
    const rows = await db.listCampaigns(env.DB, user.id);

    return json({
        campaigns: rows.map(row => ({ id: row.id, label: row.label, updatedAt: row.updated_at })),
    });
}

/**
 * One campaign's payload, byte for byte, or 404.
 *
 * Somebody else's id and an id that never existed answer identically — both come back null from
 * `db.getCampaign`, and neither is this caller's business to tell apart.
 */
export async function read(request, env, deps, user, id) {
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a campaign id this server uses.');

    const row = await db.getCampaign(env.DB, user.id, id);
    if (!row) return fail(404, 'This account has no campaign with that id.');

    // The stored text is the body rather than nested inside another object, so the shape on the
    // wire is the shape in local storage — the two stores then agree by construction.
    return new Response(row.payload, {
        headers: { 'content-type': 'application/json; charset=utf-8' },
    });
}

/**
 * Create or replace one campaign.
 *
 * The id's shape, the label's length and the JSON check are the whole of the validation, and all
 * three are about this server rather than about the campaign: they keep the table's keys
 * well-formed and the database from being filled with junk. Whether the campaign's settings make
 * sense for a character is a question for the browser, on the other side of the wire.
 */
export async function write(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a campaign id this server uses.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const payload = body.value.payload;
    if (typeof payload !== 'string' || !isJson(payload)) {
        return fail(400, 'That is not a campaign this server can store.');
    }

    const label = normaliseLabel(body.value.label);
    if (label === undefined) return fail(400, 'That label is too long.');

    await db.putCampaign(env.DB, { userId: user.id, id, label, payload, now: deps.now() });

    return noContent();
}

/**
 * Throw one campaign away.
 *
 * **204 whether or not there was anything to delete**, the same rule and for the same reason as
 * a character: the end state the caller asked for is "that campaign is not there", and it is not
 * there.
 *
 * **Characters that name it keep naming it.** There is no cascade here and there is no update
 * that clears the column, because the server cannot tell whether the deletion was a mistake and
 * the browser reports the state honestly — a character that names a campaign which is not here.
 * Nulling the field would be this server making a decision about somebody's character, which is
 * the one thing it exists not to do.
 */
export async function remove(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a campaign id this server uses.');

    await db.deleteCampaign(env.DB, user.id, id);

    return noContent();
}

function isJson(text) {
    try {
        JSON.parse(text);
        return true;
    } catch {
        return false;
    }
}

/**
 * `label`, trimmed and defaulted — the same three states `characters.js` gives a character's.
 *
 * Missing or empty becomes an ordinary state rather than a refusal. `undefined` out of this
 * function means the opposite: the one case that *is* a refusal, because the value was not a
 * usable string at all.
 */
function normaliseLabel(value) {
    if (value === undefined || value === null) return DEFAULT_LABEL;
    if (typeof value !== 'string') return undefined;

    const trimmed = value.trim();
    if (trimmed.length === 0) return DEFAULT_LABEL;
    if (trimmed.length > MAX_LABEL_LENGTH) return undefined;

    return trimmed;
}
