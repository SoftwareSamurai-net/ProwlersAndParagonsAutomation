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
import { newCharacterId, newMembershipId, normaliseJoinCode, playerKey } from './crypto.js';
import { fail, json, noContent, readJson, sameOrigin } from './http.js';

/** `m_` plus 22 URL-safe characters — the `c_`/`g_` shape with a third letter. */
const ID_PATTERN = /^m_[A-Za-z0-9_-]{22}$/;

/** A character id, the same pattern `characters.js` validates its own keys with. */
const CHARACTER_ID_PATTERN = /^c_[A-Za-z0-9_-]{22}$/;

const MAX_LABEL_LENGTH = 80;
const DEFAULT_LABEL = 'Unnamed character';

/**
 * The palette word a snapshot travels with, and the only one that changes what approving does.
 *
 * **Told, never read.** This server does not parse a character, so whether a snapshot is a
 * Villain is the same opaque `kind` word every save already sends beside a character (see
 * `characters.js` and 0008) — sent again with the snapshot, because the snapshot is what the GM
 * decides about and the player's own row may say something else by the time they do. Anything
 * other than this exact word, including nothing at all from an older build, is approved as a
 * Hero is: a clone, and the player keeps their character.
 */
const VILLAIN = 'villain';

/** The cap on that word, the bound `characters.js` puts on every index field. */
const MAX_KIND_LENGTH = 40;

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
 *
 * **Except one bit, and it says only that two rows share an account.** Each row also carries
 * `playerKey` — a keyed hash of the campaign and the player's account id, computed by
 * `crypto.playerKey` and never the account id itself. It reveals that two characters in this game
 * came from one player and reveals nothing else: it does not name which account, does not compare
 * across campaigns (the campaign id is folded into the hash), and cannot be inverted without the
 * server's own secret. The invariant above survives because the invariant was always about the
 * *account*, not about whether two rows can be told apart as siblings.
 */
export async function inbox(request, env, deps, user) {
    const rows = await db.listMembershipsForGm(env.DB, user.id);

    return json({ memberships: await Promise.all(rows.map(row => asGmRow(env, row))) });
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
 * The campaign this membership names, as the campaign's own payload — the player's live view of
 * the table they are at.
 *
 * **Why it exists.** Everything else about a campaign is scoped to the account that owns it, so a
 * player's browser cannot read the game they are in at all: what their screen draws is the copy of
 * the table's rules that was written onto their character when it joined. That copy is what is in
 * force for the character and stays so — nothing here changes it — but a GM who raises the price
 * of Immortality afterwards moves nothing, and until now no screen could even say the two had come
 * apart. This is the one read that lets a member's page put the live table beside their copy.
 *
 * **Scoped by `player_user_id` and nothing else, which is the whole of the authorisation.** The
 * caller's own `campaign_members` row is what entitles them, the same predicate `getMembership`'s
 * player half carries — and unlike that one there is no GM arm, because the account that owns a
 * campaign already reads it at its own address. A membership belonging to somebody else, a
 * membership this account is the *GM* of, an id that never existed, and a campaign the GM has
 * deleted all answer the same 404 in the same words. Those are not four facts this server tells
 * apart for a caller: a split would say whether an id exists, which is the reason `read` above
 * gives one sentence to two of them.
 *
 * **Nothing is parsed.** The payload goes out exactly as it came in, the way `join` answers the
 * same bytes and for the same reason — this server holds no rule and must never gain one. A route
 * that lifted `Table` and `ImmortalityCost` out of the payload would be this server knowing what a
 * campaign is shaped like, which is a second place to keep in step with the engine and one that
 * would fail silently, answering nulls, the first time the shape moved.
 *
 * **So what bounds the answer is the read, not a projection**: `db.campaignForMember` selects two
 * columns, the campaign's id and its payload. No account id, no label, no join code, no character,
 * no clone and nothing about another member — the same bound `campaignByJoinCode` states, which is
 * what a player already receives when they redeem a code.
 */
export async function table(request, env, deps, user, id) {
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    const row = await db.campaignForMember(env.DB, { id, playerUserId: user.id });
    if (!row) return fail(404, 'This account has no membership with that id.');

    return json({
        campaignId: row.campaign_id,
        // The campaign's own payload, verbatim, exactly as `join` hands it back. The browser reads
        // the table's rules out of it; this server does not know there are any.
        payload: row.payload,
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

    const kind = normaliseKind(body.value.kind);
    if (kind === undefined) return fail(400, 'That is not a value this server can store.');

    // **A Villain is sent as the nemesis of one of the sender's own Heroes in this game** — the
    // owner's ruling of 2026-10-02. The Hero is named by its membership id, a row this server can
    // check without reading a character. A Hero's snapshot carries no key, whatever was sent.
    const heroId = body.value.nemesisOf ?? null;
    if (heroId !== null && (typeof heroId !== 'string' || !ID_PATTERN.test(heroId))) {
        return fail(400, 'That is not a membership id this server uses.');
    }
    if (kind === VILLAIN && heroId === null) {
        return fail(400, 'A Villain is sent as the nemesis of one of your Heroes in this game. Name the Hero.');
    }
    const nemesisOf = kind === VILLAIN ? heroId : null;

    const written = await db.submitToCampaign(env.DB,
        { id, playerUserId: user.id, label, payload, kind, nemesisOf, now: deps.now() });

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

        // **The Villain is the GM's now.** Nothing more can be sent into a membership whose
        // character was handed over — there is no character of the player's behind it any more,
        // and no hand-back, so a further snapshot would be a request nobody can grant.
        if (row.handed_over_at !== null && row.handed_over_at !== undefined) {
            return fail(409, 'That Villain belongs to the campaign now, so nothing more can be sent.');
        }

        // **The Hero named is not one of the sender's in this game** — somebody else's, this
        // membership itself, a nemesis, or a row that has gone.
        if (nemesisOf !== null
            && !(await db.isOwnHeroInGame(env.DB, { membershipId: id, heroId: nemesisOf, playerUserId: user.id }))) {
            return fail(400, 'That Hero is not one of yours in this game.');
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
    return await decide(request, env, deps, user, id,
        (database, args) => db.approveSubmission(database, { ...args, newCharacterId: newCharacterId() }));
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
 * The GM changes which Hero a nemesis of theirs hunts.
 *
 * <p><b>Only the GM, only a handed-over Villain</b>, for the reason `db.rekeyNemesis` gives. A
 * player asking, an id that is not a nemesis, and a game that is gone are all one 404 — none is a
 * row this caller may re-key — and a Hero who is not in this game is the one 409, because the
 * nemesis is there and the remedy is choosing another Hero.</p>
 */
export async function rekey(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    const body = await readJson(request);
    if (!body) return fail(400, 'That request is too large or is not JSON.');

    const heroId = body.value.nemesisOf;
    if (typeof heroId !== 'string' || !ID_PATTERN.test(heroId)) {
        return fail(400, 'That is not a membership id this server uses.');
    }

    if (await db.rekeyNemesis(env.DB, { id, gmUserId: user.id, nemesisOf: heroId })) return noContent();

    const row = await db.getMembership(env.DB, user.id, id);
    if (!row || row.gm_user_id !== user.id || row.handed_over_at === null || row.handed_over_at === undefined
        || !(await db.campaignStillThere(env.DB, { gmUserId: user.id, campaignId: row.campaign_id }))) {
        return fail(404, 'This account has no nemesis with that id.');
    }

    return fail(409, 'That Hero is not in this game.');
}

/**
 * End a membership: the player leaving, or the GM removing them. One address, two meanings.
 *
 * <p><b>Which it means is decided by which owner column matches, not by anything the caller
 * says.</b> A membership names two accounts and each side's statement carries its own column, so
 * a third account matches neither and ends nothing — the same shape as every other statement
 * here, and the reason this needs no role in the request.</p>
 *
 * <p><b>The row goes, and the campaign's clone with it.</b> Deleting a *campaign* keeps its
 * memberships so that writing it back is a complete undo; ending a *membership* is the opposite
 * act and has no undo. A campaign holding the sheet of somebody who has left would be a roster
 * with no way to correct it.</p>
 *
 * <p><b>A second identical request is not an error</b>, for the reason `join` records about a
 * second join: the end state the caller asked for is "that membership is not there", and it is
 * not. It also means the answer says nothing about whether an id belongs to somebody else, which
 * a 404-or-204 split would.</p>
 *
 * <p><b>The one refusal is a GM whose campaign is gone</b>, which is 409 and the same sentence
 * both decisions give. Answering 204 there would report a removal that did not happen: the
 * player's row deliberately outlives the campaign, and `removeMember`'s `EXISTS` is what keeps a
 * restore whole.</p>
 */
export async function leave(request, env, deps, user, id) {
    if (!sameOrigin(request)) return fail(403, 'This request did not come from this site.');
    if (!ID_PATTERN.test(id)) return fail(400, 'That is not a membership id this server uses.');

    if (await db.leaveCampaign(env.DB, { id, playerUserId: user.id })) return noContent();
    if (await db.removeMember(env.DB, { id, gmUserId: user.id })) return noContent();

    // Neither statement matched. That is a row belonging to somebody else, a row that was already
    // gone, or a game this account has deleted — and only the last is a refusal.
    if (await db.isGmOfMembership(env.DB, { id, gmUserId: user.id })) {
        return fail(409, 'That campaign is no longer here.');
    }

    return noContent();
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

    // Both statements take the clock now: an approval records when the clone was accepted, and a
    // rejection records that a decision happened at all, which is the only thing distinguishing it
    // from never having been read.
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

    // **The snapshot the GM saw is still the one waiting, and it is a Villain — so the only thing
    // that can have refused it is the GM's own cap.** Approving would have moved it onto their
    // account, and a full account refuses the approval whole: nothing is approved, the player
    // keeps their character, and the snapshot is still waiting. Said in the words the roster uses
    // for a full account, with the figure, because the remedy is the GM's. A rejection cannot
    // reach this: with the campaign there and the version matching, a rejection always lands.
    // **A GM who is also the player reaches this row through the player's half of
    // `getMembership`, which does not require the campaign to exist** — so a game they deleted
    // is asked about first, or a refusal about a game that is gone would be reported as a full
    // account, on Reject as much as on Approve.
    if (!(await db.campaignStillThere(env.DB, { gmUserId: user.id, campaignId: row.campaign_id }))) {
        return fail(409, 'That campaign is no longer here.');
    }

    if (row.pending_version === version && row.pending_kind === VILLAIN) {
        const limit = await db.characterLimit(env.DB, user.id);

        return fail(409,
            `Not approved: your account already holds ${limit} characters, and approving this `
            + 'Villain would move it onto your account. Make room and approve again — it is still waiting.',
            { pendingVersion: row.pending_version, pending: row.pending_payload, limit });
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

        // **The last decision, and the whole reason a player can tell one from the other.**
        // Approving moves the pending slot into the approved one; rejecting clears the pending
        // slot and leaves the clone. Both then leave two booleans in the same state the player
        // saw before they sent anything, so without this a rejection is silent and shapeless.
        //
        // `decided_at` is stored beside it and is deliberately not here: nothing draws a time
        // yet, and this server does not send fields the browser binds nothing to.
        decision: row.decision ?? null,

        // **Whether this character was handed to the campaign as a nemesis.** Its row is gone
        // from the player's account, so this is the only place their roster can learn it was
        // given away rather than lost.
        handedOver: row.handed_over_at !== null && row.handed_over_at !== undefined,

        // **Which of the player's memberships this Villain hunts**, so their nemesis block can
        // name the Hero. A membership id, never a character's — see 0012.
        nemesisOf: row.nemesis_of ?? null,

        // **And to which game, by the name the player was shown when they joined it** — the one
        // thing the roster's sentence needs that a membership row does not otherwise carry.
        // Answered only for a handed-over row, and null where the campaign has since been
        // deleted: a name nobody holds any more is not one to print.
        givenTo: row.given_to ?? null,
    };
}

/**
 * What a GM's list row carries — the same, without the player's character id, plus `playerKey`.
 *
 * **Async because the hash is**, and that is the only reason `inbox` maps this through
 * `Promise.all` rather than a bare `.map`. `row.player_user_id` is read here and nowhere else in
 * this function's return value — see the invariant in `inbox`'s own doc comment.
 */
async function asGmRow(env, row) {
    return {
        id: row.id,
        campaignId: row.campaign_id,
        label: row.label,
        hasApproved: row.has_approved === 1,
        approvedAt: row.approved_at ?? null,
        hasPending: row.has_pending === 1,
        pendingAt: row.pending_at ?? null,
        pendingVersion: row.pending_version,

        // **Both shapes or neither**, because they deserialize into one record on the other side:
        // a field on the player's row and not the GM's would silently default on the GM's screens
        // rather than fail, which is exactly the drift `AccountsContractTests` exists for.
        decision: row.decision ?? null,

        // **What the waiting snapshot is, as the player's browser said**, so the approval screen
        // can tell the GM that approving takes the character — from the very word the approval
        // will act on. Null where nothing is waiting or an older build sent no word.
        pendingKind: row.has_pending === 1 ? (row.pending_kind ?? null) : null,

        // **Whether this membership's Villain is the GM's now**, so the campaign's roster marks
        // it as the nemesis rather than as an ordinary approval.
        handedOver: row.handed_over_at !== null && row.handed_over_at !== undefined,

        // **The Hero this Villain hunts**, by membership id, so the GM's table can lay each
        // nemesis out under its Hero.
        nemesisOf: row.nemesis_of ?? null,

        // **The one field the player's row must never gain**, and the reason it stays out of
        // `asPlayerRow`: a player has no use for "which of my own rows share an account with
        // me" — they already know — and sending it there for free would be one more field this
        // server hands out for no consumer, which is exactly the kind of drift
        // `AccountsContractTests` exists to catch.
        playerKey: await playerKey(env, row.campaign_id, row.player_user_id),
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
 * `kind`, as `characters.js` takes an index field: absent is null, a string within the cap is
 * stored verbatim, and `undefined` out of here is the one refusal.
 */
function normaliseKind(value) {
    if (value === undefined || value === null) return null;
    if (typeof value !== 'string' || value.length > MAX_KIND_LENGTH) return undefined;

    return value;
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
