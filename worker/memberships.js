// A campaign's clone of a character, and the snapshot a player has sent for approval.
//
// **Fork and pull request, for characters.** A player builds in their own rows and needs nobody's
// permission to do it. When they want a change to count at the table they send a snapshot; the GM
// reads a diff and accepts or rejects the whole thing; accepting replaces the campaign's clone.
// Both sides keep a copy, so neither is ever editing the other's row.
//
// **This file is `campaigns.js` with two payload slots and a version, and it must stay that
// boring.** Nothing here reads a field of a character or of a campaign — not a tier, not a Trait
// Cap, not a rank. The diff the GM reads is computed in the browser by the engine, which is the
// authority on what a character costs and whether it is legal. A second place that understood the
// shape of a character would be a second place to keep in step with it.
//
// **Two things here are not like anything else in this directory, and both are stated rather than
// buried:**
//
//   1. **`join` reads a campaign row the caller does not own.** That is what a join code is: a
//      secret the GM minted and chose to hand out, and holding it is the whole of the
//      authorisation — the same shape as holding a sign-in link. It answers the campaign's own
//      settings and nothing else: no account id, no character, no other member's anything. Every
//      other statement in `db.js` is scoped to the caller, and `docs/CHARACTERS-API.md` records
//      this as the one place that rule bends and why.
//
//   2. **A membership has two owners.** The GM owns the campaign; the player owns the character.
//      Each side's reads and writes carry its own column in the `WHERE`, so neither can name a
//      row belonging to a third account — and the GM only ever sees a payload the player
//      deliberately sent, never one this server went and fetched out of their account.
//
// See docs/CHARACTERS-API.md, which states the contract for all of it.

import { campaignByJoinCode } from './db.js';
import * as db from './db.js';
import { newMembershipId, normaliseJoinCode } from './crypto.js';
import { fail, json, noContent, readJson, sameOrigin } from './http.js';

/** `m_` plus 22 URL-safe characters — the `c_`/`g_` shape with a third letter. */
const ID_PATTERN = /^m_[A-Za-z0-9_-]{22}$/;

/** A character id, the same pattern `characters.js` validates its own keys with. */
const CHARACTER_ID_PATTERN = /^c_[A-Za-z0-9_-]{22}$/;

const MAX_LABEL_LENGTH = 80;
const DEFAULT_LABEL = 'Unnamed character';

/**
 * How many join attempts one account gets an hour.
 *
 * **A join code is a secret and this is what stops it being swept.** Ten symbols from thirty is
 * about 5.9 × 10^14 codes, which no rate limit is needed to protect on its own — but a limit
 * costs one statement and removes the whole question, and the endpoint is the only place in this
 * server where a caller can probe for something belonging to somebody else. Keyed on the account
 * rather than the code, because an account is what asking requires.
 */
const JOIN_ATTEMPTS_PER_HOUR = 20;
const JOIN_WINDOW_SECONDS = 60 * 60;

/**
 * Redeem a join code: put one of the caller's characters into somebody's campaign.
 *
 * **What comes back is the campaign, and that is the point of the request.** The player has to be
 * able to see the tier they are being asked to build to — the browser decides what to do with
 * that (inherit into an empty field, report a disagreement, and never repair one), and it can
 * decide nothing without the campaign's own settings.
 *
 * **A second join with the same code and the same character is not an error.** It answers the
 * membership already there. Joining twice is what a reload of a half-finished form does, and the
 * end state the caller asked for is "this character is in that campaign", which it is.
 *
 * **Nothing is submitted here.** A join that wrote a snapshot would put a character in front of
 * the GM before the player meant it to be seen; sending for approval is a separate, deliberate
 * act with its own address.
 */
export async function join(request, env, deps, user) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const code = normaliseJoinCode(body.value.code);
    if (code === undefined) return fail(400, 'That is not a join code.');

    const characterId = body.value.characterId;
    if (typeof characterId !== 'string' || !CHARACTER_ID_PATTERN.test(characterId)) {
        return fail(400, 'That is not a character id this server uses.');
    }

    const label = normaliseLabel(body.value.label);
    if (label === undefined) return fail(400, 'That label is too long.');

    // **Counted before the lookup, not after it.** A limit applied only to successful joins is a
    // limit on nothing: what is being bounded is how many codes one account may try.
    const tried = await db.countAttempt(env.DB, {
        key: `join:${user.id}`, now: deps.now(), windowSeconds: JOIN_WINDOW_SECONDS,
    });

    if (tried > JOIN_ATTEMPTS_PER_HOUR) {
        return fail(429, 'That is a lot of join codes. Try again a little later.');
    }

    const campaign = await campaignByJoinCode(env.DB, code);

    // **A code that names no campaign and a code that has been replaced answer identically**,
    // because they are the same fact from here: there is no campaign with that code. Neither
    // answer says whether a code was ever valid, which is the only thing a caller could learn by
    // asking twice.
    if (!campaign) return fail(404, 'No campaign is using that code.');

    const membership = await db.joinCampaign(env.DB, {
        id: newMembershipId(),
        campaignId: campaign.id,
        gmUserId: campaign.user_id,
        playerUserId: user.id,
        characterId,
        label,
        now: deps.now(),
    });

    // The insert either wrote a row or returned the one already there; both come back non-null.
    // Nothing back means the statement matched nothing, which it cannot here — so it is a failure
    // rather than a state, and it is reported as one rather than answered as a join.
    if (!membership) return fail(500, 'That campaign could not be joined just now.');

    // **The row that came back belongs to the campaign whose code was redeemed, asserted rather
    // than assumed.** The unique index is what makes this true; this is what makes it *checked*.
    // A `g_…` is unique per account rather than globally, so a key missing `gm_user_id` would let
    // a re-join hand back a membership of a different GM's campaign — and the player would go on
    // sending snapshots to somebody they never joined. That is a defect to refuse, not to answer
    // 200 to, and refusing it here is a line a mutation can break and watch fail.
    if (membership.gm_user_id !== campaign.user_id) {
        return fail(500, 'That campaign could not be joined just now.');
    }

    return json({
        id: membership.id,
        campaignId: campaign.id,
        label: campaign.label,
        // The campaign's own payload, verbatim, exactly as `campaigns.read` hands it back to its
        // owner. The browser reads a tier out of it; this server does not know there is one.
        payload: campaign.payload,
        pendingVersion: membership.pending_version,
    });
}

/**
 * Every membership of a character this account owns — the player's half.
 *
 * This is what a standing beside a character's name is drawn from: approved, changes pending, or
 * not submitted. No payload, for the reason `characters.list` sends none: a list that carried two
 * whole characters per row would cost a read per row to draw a label.
 */
export async function listMine(request, env, deps, user) {
    const rows = await db.listMembershipsForPlayer(env.DB, user.id);

    return json({ memberships: rows.map(asPlayerRow) });
}

/**
 * Every membership of every campaign this account owns — the GM's half.
 *
 * **Across all their campaigns rather than one at a time**, because both screens want it: the
 * campaign list needs a waiting count per game, and the approval screen needs the rows of one.
 * Two addresses answering from one statement cannot disagree about the count.
 *
 * **No account id and no address ever appears here.** A GM learns that a character called
 * something is waiting; whose account sent it is not a thing this server tells anybody.
 */
export async function inbox(request, env, deps, user) {
    const rows = await db.listMembershipsForGm(env.DB, user.id);

    return json({ memberships: rows.map(asGmRow) });
}

/**
 * One membership in full: the clone, and the snapshot waiting for a decision.
 *
 * **Readable by either side and by nobody else** — the GM because it is a membership of their
 * game, the player because it is their own character in it. A third account matches neither and
 * gets the same 404 an id that never existed gets.
 *
 * **Both payloads, because this is what the diff is computed from**, in the browser. Neither is
 * parsed on this side of the wire.
 *
 * **`characterId` is answered to the player and withheld from the GM.** The player needs it —
 * it names which of their own characters this is. The GM does not: they approve `m_…`, and an id
 * belonging to another account's row is a disclosure with no use behind it.
 *
 * **No timestamps here, and they were here for one commit.** `approvedAt` and `pendingAt` are in
 * the two lists, which is where a "sent three hours ago" belongs; this read exists so the browser
 * can compute a diff. `AccountsContractTests` caught them: the server was sending two fields the
 * client bound nothing to, which is exactly the drift that test exists for — and binding a field
 * nothing draws would have been a field that rots.
 */
export async function read(request, env, deps, user, id) {
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    const row = await db.getMembership(env.DB, user.id, id);
    if (!row) return fail(404, 'This account has no membership with that id.');

    const mine = row.player_user_id === user.id;

    return json({
        id: row.id,
        campaignId: row.campaign_id,
        label: row.label,
        // Which side of the membership the caller is on. The browser draws a different screen for
        // each, and it must not have to guess from what came back.
        role: mine ? 'player' : 'gm',
        characterId: mine ? row.character_id : null,
        approved: row.approved_payload ?? null,
        pending: row.pending_payload ?? null,
        pendingVersion: row.pending_version,
    });
}

/**
 * Send a snapshot for approval, replacing whatever was waiting.
 *
 * **One slot, and resubmitting overwrites it.** There is deliberately no history: what the owner
 * asked for is a decision queue, and a row per submission is a version-control system for
 * characters.
 *
 * **The version comes back and the caller has to keep it.** It is what Approve and Reject send
 * back to prove they are deciding about the snapshot that was on screen — see `decide` below.
 */
export async function submit(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const payload = body.value.payload;
    if (typeof payload !== 'string' || !isJson(payload)) {
        return fail(400, 'That is not a character this server can store.');
    }

    const label = normaliseLabel(body.value.label);
    if (label === undefined) return fail(400, 'That label is too long.');

    const written = await db.submitToCampaign(env.DB,
        { id, playerUserId: user.id, label, payload, now: deps.now() });

    if (!written) {
        // Nothing matched, and there are now two reasons rather than one. The row is re-read to
        // tell them apart — only on this path, so the write itself stays a single statement with
        // no read racing in front of it.
        const row = await db.getMembership(env.DB, user.id, id);

        // No such membership, or one belonging to somebody else. The two are the same fact from
        // here and answer the same way, exactly as a character id does.
        if (!row || row.player_user_id !== user.id) {
            return fail(404, 'This account has no membership with that id.');
        }

        // **The membership is theirs and the campaign is gone.** A 404 here would be a lie the
        // player could act on — their character is still in the list, still shows a standing, and
        // the honest answer is about the game rather than about them. The membership is untouched
        // and so is their own character; if the GM restores the campaign, this starts working
        // again with the clone and the standing exactly as they were.
        return fail(409, 'That campaign is no longer here, so nothing can be sent to it.');
    }

    return json({ version: written.pending_version });
}

/**
 * Accept the snapshot the GM was looking at.
 *
 * **The version is the whole of the correctness.** Without it: the GM reads snapshot A, the player
 * resubmits B while the diff is on screen, the GM clicks Approve, and B — which nobody has looked
 * at — becomes the campaign's clone. So the decision carries the version it was made about, and a
 * mismatch is refused with the newer snapshot attached: *this changed while you were looking, here
 * it is.*
 */
export async function approve(request, env, deps, user, id) {
    return await decide(request, env, deps, user, id, db.approveSubmission);
}

/**
 * Turn the snapshot down. The clone is untouched, which is the whole of what rejecting means.
 *
 * **The same version check as Approve, for the same reason.** Rejecting a snapshot nobody has read
 * is the same fault as approving one — the player is told their change was refused, about a change
 * the GM never saw.
 *
 * **The player's own character is untouched too**, and it is not this server's to touch: a
 * rejection is a decision about the campaign's copy, and nothing here reaches into somebody's own
 * work to undo it.
 */
export async function reject(request, env, deps, user, id) {
    return await decide(request, env, deps, user, id, db.rejectSubmission);
}

/**
 * The compare-and-swap both decisions share, and the refusal both give.
 *
 * <p>One function because a refusal has to read the same either way and two copies would be two
 * chances for one of them to forget to attach the newer snapshot — which is the whole of what
 * makes the refusal useful rather than merely correct.</p>
 *
 * <p><b>The row is re-read only on the refusal path.</b> On the way through, the statement's own
 * `RETURNING` is the answer; a read in front of it would be the very race this exists to close.</p>
 */
async function decide(request, env, deps, user, id, statement) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const version = body.value.version;

    // **A missing version is refused rather than defaulted.** Defaulting it to whatever is
    // current would turn the compare-and-swap into a decision about the latest snapshot, which
    // is exactly the defect it exists to prevent, reachable by omitting one field.
    if (!Number.isInteger(version) || version < 0) {
        return fail(400, 'That decision names no version of the submission.');
    }

    const decided = await statement(env.DB, { id, gmUserId: user.id, version, now: deps.now() });

    if (decided) return noContent();

    // Nothing was written. Three reasons, and the caller is owed different words for two of them.
    const row = await db.getMembership(env.DB, user.id, id);

    // Not a membership of a campaign this account owns — including one where the caller is the
    // player rather than the GM. A player deciding about their own submission is not a thing.
    if (!row || row.gm_user_id !== user.id) {
        return fail(404, 'This account has no membership with that id.');
    }

    if (row.pending_payload === null || row.pending_payload === undefined) {
        return fail(409, 'There is nothing waiting for a decision here.', {
            pendingVersion: row.pending_version,
            pending: null,
        });
    }

    // **The newer snapshot travels with the refusal**, so the screen can redraw the diff rather
    // than telling somebody to go and look again. That is the difference between a refusal that
    // is safe and one that is also usable.
    // Exactly the two fields the browser binds, and no more. A refusal carrying a `pendingAt` and
    // a `label` nothing reads is the same drift the detail read above was caught for.
    return fail(409, 'This changed while you were looking at it.', {
        pendingVersion: row.pending_version,
        pending: row.pending_payload,
    });
}

/** What a player's list row carries. `has_*` arrives from SQLite as 1 or 0. */
function asPlayerRow(row) {
    return {
        id: row.id,
        campaignId: row.campaign_id,
        characterId: row.character_id,
        label: row.label,
        hasApproved: row.has_approved === 1,
        approvedAt: row.approved_at ?? null,
        hasPending: row.has_pending === 1,
        pendingAt: row.pending_at ?? null,
        pendingVersion: row.pending_version,
    };
}

/** What a GM's list row carries — the same, without the player's character id. */
function asGmRow(row) {
    return {
        id: row.id,
        campaignId: row.campaign_id,
        label: row.label,
        hasApproved: row.has_approved === 1,
        approvedAt: row.approved_at ?? null,
        hasPending: row.has_pending === 1,
        pendingAt: row.pending_at ?? null,
        pendingVersion: row.pending_version,
    };
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
 * Missing or empty is an ordinary state. `undefined` out of this function is the one case that is
 * a refusal: a value that was not a usable string at all.
 */
function normaliseLabel(value) {
    if (value === undefined || value === null) return DEFAULT_LABEL;
    if (typeof value !== 'string') return undefined;

    const trimmed = value.trim();
    if (trimmed.length === 0) return DEFAULT_LABEL;
    if (trimmed.length > MAX_LABEL_LENGTH) return undefined;

    return trimmed;
}
