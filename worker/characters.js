// Characters, plural, belonging to one account.
//
// **Nothing here reads a field of a character.** The payload arrives as a JSON-encoded string
// inside the request body, is checked for being parseable JSON and for the whole request being
// small enough, and is written down verbatim; a read hands the same bytes back. That is not
// laziness — the engine is the authority on what a character is, it runs in the browser, and a
// second place that understood the shape would be a second place to keep in step with it. The
// only things this server is allowed to know about a character are who it belongs to, what it
// is called, and when it was last touched.
//
// This is the contract in docs/CHARACTERS-API.md, which two other pieces of work — the
// browser's store and the manager UI — are being built against at the same time. Change the
// document first if it needs to change; do not let this file drift from it quietly.

import * as db from './db.js';
import { fail, json, noContent, readJson, sameOrigin } from './http.js';

/**
 * `c_` plus 22 URL-safe characters — the shape `crypto.js` mints for a user id, minted
 * client-side here instead. An id is a key in a table; the caller does not get to choose its
 * format, so anything else is refused before it reaches a query.
 */
const ID_PATTERN = /^c_[A-Za-z0-9_-]{22}$/;

/**
 * The campaign id shape, the same pattern `campaigns.js` validates its own keys with, spelled
 * here because this is the one other place a caller can put one into the database.
 */
const CAMPAIGN_ID_PATTERN = /^g_[A-Za-z0-9_-]{22}$/;

const MAX_LABEL_LENGTH = 80;
const DEFAULT_LABEL = 'Unnamed character';

/** The account's characters, most recently touched first, and the cap they are held to. */
export async function list(request, env, deps, user) {
    const [rows, limit] = await Promise.all([
        db.listCharacters(env.DB, user.id),
        db.characterLimit(env.DB, user.id),
    ]);

    return json({
        limit,
        characters: rows.map(row => ({
            id: row.id,
            label: row.label,
            updatedAt: row.updated_at,
            // Handed back exactly as it was handed in. The server never derived this, never
            // checked it against the `campaigns` table, and does not know what it means — see
            // `normaliseCampaignId` below.
            campaignId: row.campaign_id ?? null,
        })),
    });
}

/**
 * One character's payload, byte for byte, or 404.
 *
 * Somebody else's id and an id that never existed answer identically — both come back null from
 * `db.getCharacter`, and neither is this caller's business to tell apart.
 */
export async function read(request, env, deps, user, id) {
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a character id this server uses.');

    const row = await db.getCharacter(env.DB, user.id, id);
    if (!row) return fail(404, 'This account has no character with that id.');

    // The stored text is returned as the body rather than nested inside another object, so the
    // shape on the wire is the shape in local storage. The two stores then agree by
    // construction instead of by a mapping somebody has to maintain.
    return new Response(row.payload, {
        headers: { 'content-type': 'application/json; charset=utf-8' },
    });
}

/**
 * Create or replace one character.
 *
 * The id's shape, the label's length, the JSON check and the cap are the whole of the
 * validation, and all four are about this server rather than about the character: they keep
 * the table's keys well-formed and the database from being filled with junk. Whether the
 * character *itself* is legal is a question for the engine, on the other side of the wire.
 */
export async function write(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a character id this server uses.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const payload = body.value.payload;
    if (typeof payload !== 'string' || !isJson(payload)) {
        return fail(400, 'That is not a character this server can store.');
    }

    const label = normaliseLabel(body.value.label);
    if (label === undefined) return fail(400, 'That label is too long.');

    const campaignId = normaliseCampaignId(body.value.campaignId);
    if (campaignId === undefined) return fail(400, 'That is not a campaign id this server uses.');

    const stored = await db.putCharacter(env.DB,
        { userId: user.id, id, label, payload, campaignId, now: deps.now() });

    if (!stored) {
        const limit = await db.characterLimit(env.DB, user.id);

        return json({ error: `This account already holds ${limit} characters.`, limit }, { status: 409 });
    }

    return noContent();
}

/**
 * Throw one character away.
 *
 * **204 whether or not there was anything to delete**, unlike `read`. The end state the caller
 * asked for is "that character is not there", and it is not there — so reporting 404 would report
 * failure for something that succeeded. The cost of getting this wrong is concrete: a manager with
 * two tabs open deletes in one, deletes in the other, and the second sees an error for a character
 * that is already gone, retries, and sees it again while the app looks broken.
 *
 * Nothing is leaked by answering the same either way — that is the point of answering the same.
 * Somebody else's id and an id that never existed are indistinguishable here, as they are in
 * `read`; they are simply both successes rather than both failures.
 *
 * An ill-formed id is still 400. That is a malformed request, not an absent character.
 */
export async function remove(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a character id this server uses.');

    await db.deleteCharacter(env.DB, user.id, id);

    return noContent();
}

/**
 * `campaignId`, checked for being a well-formed key and nothing else.
 *
 * **The client supplies it, exactly as it supplies `label`, and this server never derives it.**
 * It cannot: the campaign a character belongs to is a field inside the payload, and the payload
 * is never parsed here. So it travels alongside, and what is checked is the same thing that is
 * checked about an id anywhere in this file — that it is a string this table can hold as a key.
 *
 * **It is deliberately not checked against the `campaigns` table.** A character may name a
 * campaign that has been deleted, or one that lives in another browser and has never been
 * uploaded; both are ordinary, and both are reported by the browser rather than refused here.
 * Validating the reference would make this server the authority on whether a character is in a
 * legal state, which is exactly the job it does not have.
 *
 * Missing or null is the ordinary state — a character in no campaign. `undefined` out of this
 * function means a refusal: something that is not a usable id at all.
 */
function normaliseCampaignId(value) {
    if (value === undefined || value === null) return null;
    if (typeof value !== 'string') return undefined;
    if (value === '') return null;

    return CAMPAIGN_ID_PATTERN.test(value) ? value : undefined;
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
 * `label`, trimmed and defaulted.
 *
 * Missing or empty becomes an ordinary state rather than a refusal — a character with no name
 * yet is one this tool can still hold. `undefined` out of this function means the opposite: the
 * one case that *is* a refusal, because the value was not a usable string at all.
 */
function normaliseLabel(value) {
    if (value === undefined || value === null) return DEFAULT_LABEL;
    if (typeof value !== 'string') return undefined;

    const trimmed = value.trim();
    if (trimmed.length === 0) return DEFAULT_LABEL;
    if (trimmed.length > MAX_LABEL_LENGTH) return undefined;

    return trimmed;
}
