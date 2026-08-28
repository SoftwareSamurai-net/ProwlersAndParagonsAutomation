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
import { newJoinCode, normaliseJoinCode } from './crypto.js';
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
 * How many times a fresh join code is minted before a collision is reported as a failure.
 *
 * Three, because at 30^10 codes one collision is already not a thing that happens and an
 * unbounded retry against a broken database is a request that never answers.
 */
const CODE_MINT_ATTEMPTS = 3;

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
        campaigns: rows.map(row => ({
            id: row.id,
            label: row.label,
            updatedAt: row.updated_at,
            // **The one field of a campaign this server can read, and it is here because the GM
            // has to be able to read it out to somebody.** It is not in the payload and cannot
            // be: redeeming a code means finding the campaign it belongs to, which is a query,
            // and the payload is the one thing no query looks inside. It is only ever sent to the
            // campaign's own owner — this list is `WHERE user_id = ?`.
            joinCode: row.join_code ?? null,
        })),
    });
}

/**
 * Replace a campaign's join code.
 *
 * **A code is a capability, so it has to be replaceable.** An id leaked is leaked for ever; a code
 * leaked is one POST away from being useless. Nobody already in the campaign is evicted — a code
 * is redeemed once, into a membership that does not refer back to it — so this shuts the door
 * without touching who is already through it.
 *
 * **The uniqueness is the index's and the retry is here.** A read that found no campaign holding a
 * candidate code, followed by a write that trusted it, is the race every statement in `db.js` is
 * written to avoid. So a collision arrives as a thrown constraint and this tries again — bounded,
 * because an unbounded retry against a genuinely broken database is a request that never answers.
 * At 30^10 codes a single collision is already not a thing that happens.
 */
export async function rotateCode(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a campaign id this server uses.');

    for (let attempt = 0; attempt < CODE_MINT_ATTEMPTS; attempt++) {
        const code = newJoinCode();

        try {
            const rotated = await db.rotateJoinCode(env.DB,
                { userId: user.id, id, joinCode: normaliseJoinCode(code) });

            // Scoped to this account, so nothing matched means an id this account does not own —
            // the same 404 a read of somebody else's campaign gets, and for the same reason.
            if (!rotated) return fail(404, 'This account has no campaign with that id.');

            // The hyphenated form goes back, because that is the form somebody reads out. The
            // stored form has no hyphen; `normaliseJoinCode` is what keeps the two agreeing.
            return json({ joinCode: code });
        } catch (error) {
            if (attempt === CODE_MINT_ATTEMPTS - 1) throw error;
        }
    }

    // Unreachable: the loop either returns or rethrows on its last pass. Present because a
    // function whose every path is inside a loop is one an edit can quietly leave falling out.
    return fail(500, 'A new code could not be minted just now.');
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

    // **A candidate code, kept only if there is not one already.** A campaign nobody can join is
    // useless, so the first write gives it one — and `COALESCE` in the statement is what stops an
    // ordinary save (a rename, a tier change) rotating it and locking out everybody who had been
    // told the old one. Rotating is `rotateCode` above, deliberately.
    await db.putCampaign(env.DB, {
        userId: user.id, id, label, payload,
        joinCode: normaliseJoinCode(newJoinCode()),
        now: deps.now(),
    });

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
