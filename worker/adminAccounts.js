// What the players in this GM's campaigns hold, and the cap they are held to.
//
// **Gated by the same question as the invitation list, not a second one.** `index.js` asks
// `invitations.isAdministrator` before any of these three addresses is reached, so this module
// does not re-decide who may see it — a second check here is the shape of bug where the two
// eventually disagree. See the note in `index.js` for why an ordinary account is answered with the
// same 404 an unrouted address gets.
//
// **Scoped to campaign membership, and that is the decision rather than a detail.** The list is
// the accounts that are players in a campaign this caller runs — joined through `campaign_members`
// — and not every account on the server. `isAdministrator` is one person today, so an all-accounts
// list would not bite yet; it would the moment a second GM is ever made an administrator, and a
// privilege that only misbehaves later is the kind this project has been bitten by before. The
// cost is accepted knowingly: **a GM cannot see the characters of somebody who is not in one of
// their campaigns, and should not.**
//
// **A row is keyed by `email`, and the account id stays off the wire.** The membership design
// mints `m_…` ids precisely so a GM is never told whose account is on the other side; nothing here
// weakens that, because this caller is already reading every address on the invitation list drawn
// beside this panel on the same page — the address is what they recognise a person by, and it is
// unique in `users` by the schema. **A membership id would be the wrong key** rather than a safer
// one: a player in two of this GM's campaigns, or with two characters in one, has several
// memberships and would be several rows, and the thing being set is a property of the *account*.
// The address travels in the path; it never reaches `error_log`, because `route` there is a
// pattern from a closed list and `redact` takes addresses out of a message.
//
// **Nothing here parses a payload.** The character list answers `label`, `updated_at` and the
// three index columns `0008` added — every one of them written by the client, stored verbatim and
// handed back verbatim. The server has never known what a character is and this module does not
// teach it.

import * as db from './db.js';
import { normaliseEmail } from './email.js';
import { fail, json, readJson, sameOrigin } from './http.js';

/**
 * The largest cap this screen will set.
 *
 * **Not a rule about how many characters anybody should have** — it is the bound that keeps a
 * caller from writing an arbitrary integer into a column, the same kind of check `characters.js`
 * makes about `spent`. Five hundred is two orders of magnitude above the twenty-five a GM account
 * is raised to by hand today, so it refuses a typo and nothing anybody would mean.
 */
export const MAX_CHARACTER_LIMIT = 500;

/** Every player in this caller's campaigns, and what each of them holds. */
export async function list(request, env, deps, user) {
    const rows = await db.listPlayersOfGm(env.DB, user.id);

    return json({ accounts: rows.map(shape) });
}

/**
 * Raise or lower one player's cap.
 *
 * <p><b>The bounds are checked before the write and the scope is checked by it.</b> A body this
 * server will not store is a malformed request whoever sent it, so 400 is answered without asking
 * whether the address names anybody — which is also what keeps the refusals from being an oracle:
 * a caller learns the same thing from either answer about an address in somebody else's campaigns
 * as about an address that has never existed.</p>
 */
export async function setLimit(request, env, deps, user, key) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const characterLimit = body.value.characterLimit;

    if (typeof characterLimit !== 'number' || !Number.isInteger(characterLimit)
        || characterLimit < 0 || characterLimit > MAX_CHARACTER_LIMIT) {
        return fail(400, `A cap is a whole number from 0 to ${MAX_CHARACTER_LIMIT}.`);
    }

    const email = addressIn(key);
    if (!email) return fail(404, 'No such address.');

    const row = await db.setCharacterLimit(env.DB, {
        gmUserId: user.id, email, characterLimit,
    });

    // Nothing matched: the address is not a player in one of this caller's campaigns, or nobody
    // has ever used it. **The same body an unrouted address gets**, byte for byte, so the two are
    // not told apart — this endpoint must not be a way of asking who has an account here.
    if (!row) return fail(404, 'No such address.');

    return json({ account: shape(row) });
}

/**
 * One player's own characters — what they are called and when they were last touched.
 *
 * <p><b>These are their `characters` rows, which is a different set from the clones a campaign
 * holds.</b> A GM can already read the campaign's clone of any member's character: a sheet that
 * member deliberately sent and the GM accepted. This is the account's own list, is not a thing
 * anybody sent, and is reachable here only because the two are in a campaign together.</p>
 *
 * <p><b>Two reads rather than one, on purpose.</b> An account in scope holding nothing and an
 * address out of scope are different answers — an empty list and a 404 — and one statement
 * returning no rows cannot tell them apart.</p>
 */
export async function characters(request, env, deps, user, key) {
    const email = addressIn(key);
    if (!email) return fail(404, 'No such address.');

    const account = await db.playerOfGm(env.DB, { gmUserId: user.id, email });
    if (!account) return fail(404, 'No such address.');

    const rows = await db.listCharacters(env.DB, account.id);

    return json({
        email: account.email,
        characters: rows.map(row => ({
            id: row.id,
            label: row.label,
            updatedAt: row.updated_at,

            // The three index columns `0008` added, and the reason they may be answered here is
            // the reason they exist at all: every one is a string or a number the *client* wrote
            // beside the payload, so returning them is handing back what was handed in. The
            // payload itself is not in this response and must never be — see the header.
            kind: row.kind ?? null,
            tierId: row.tier_id ?? null,
            spent: row.spent ?? null,
        })),
    });
}

/**
 * The address a path segment names, or null.
 *
 * <p>Percent-decoded first, because a path segment is what arrives — an address is allowed a `+`
 * and a `%` is how anything else travels — and normalised through the one reader `auth` and
 * `invitations` already share, so a capital letter in a typed address is the same account here as
 * it is at the gate. A segment that decodes to nothing usable is not "a malformed id" worth its
 * own status: it is an address this server has no row for, which is the 404 above.</p>
 */
function addressIn(key) {
    try {
        return normaliseEmail(decodeURIComponent(key));
    } catch {
        return null;
    }
}

/** One account, in the shape the panel reads. */
function shape(row) {
    return {
        email: row.email,
        displayName: row.display_name ?? null,
        characterCount: row.character_count,
        characterLimit: row.character_limit,
    };
}
