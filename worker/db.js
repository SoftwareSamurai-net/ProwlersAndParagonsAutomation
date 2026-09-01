// Every statement this system runs, in one file.
//
// Not for tidiness: the handlers above are then readable as policy — who may do what — with no
// SQL in among it, and the queries are reviewable as a set. Two of them do work that would be a
// race if it were written as a read followed by a write, and both are called out below.

/**
 * Rows whose time is up, removed opportunistically. Cheap, indexed, and keeps the tables small.
 *
 * **All three tables, and `login_attempts` was missed first time round.** Its rows are not
 * expiries but rate-limiting windows, so nothing looked wrong: the counting stayed correct
 * because the window rolls inside the statement. What grew was the table — one permanent row per
 * address and per source ever seen, for ever, which is a slow leak rather than a fault and so
 * would never have announced itself.
 */
export async function sweepExpired(db, now) {
    await db.prepare('DELETE FROM login_tokens WHERE expires_at < ?').bind(now).run();
    await db.prepare('DELETE FROM sessions WHERE expires_at < ?').bind(now).run();

    // A window whose start is older than the longest window can only ever be reset on its next
    // use, so the row carries no information. Seconds here, not milliseconds: that is the scale
    // `countAttempt` writes in.
    await db.prepare('DELETE FROM login_attempts WHERE window_start < ?')
        .bind(Math.floor(now / 1000) - 24 * 60 * 60).run();

    // A fault nobody has seen for the whole retention window is not a fault anybody is still
    // investigating. The row count is already bounded by the primary key — see the migration —
    // so this is about the table being *readable*, not about it being large: a year of
    // long-mended faults at the top of a `SELECT *` is how nobody reads the log at all.
    //
    // **Guarded, unlike the three above, and the asymmetry is deliberate.** This runs on the
    // sign-in path, and `error_log` is the newest table — so a deploy that outran its migration
    // would answer *every* sign-in with a 500 because the diagnostics could not be tidied. The
    // whole error-logging subsystem is built so it cannot take a request down with it; a prune is
    // part of that subsystem and gets the same treatment as the write in `index.js`. It is not
    // silent about it, which is the other half of the rule: a prune that does not happen must say
    // so rather than look like a prune that found nothing.
    try {
        await db.prepare('DELETE FROM error_log WHERE last_at < ?')
            .bind(now - ERROR_RETENTION_MS).run();
    } catch (error) {
        console.error('Could not prune the error log. Has migration 0004 been applied?', error);
    }
}

/**
 * How long a recorded failure is worth keeping.
 *
 * <p>Long enough that a fault which happens once a fortnight is still visible next to itself,
 * short enough that the table is about what is wrong now. It is used twice and must be: once to
 * sweep a stale row away, and once *inside* the write below to reset a row rather than continue
 * an old count into a new outage.</p>
 */
export const ERROR_RETENTION_MS = 30 * 24 * 60 * 60 * 1000;

/**
 * Record that something failed, folding it into the row for its (category, route).
 *
 * <p><b>One statement, and the retention window rolls inside it — the same shape as
 * `countAttempt` above and for the same two reasons.</b> A read that found the row and a write
 * that trusted it was still there would let two failures arriving together both insert; and a
 * prune written as a separate pass is a prune that does not happen, because the only thing that
 * reliably runs on a failing system is the failure path. So a row whose last failure is older
 * than the window is reset here — count back to 1, `first_at` moved forward — rather than
 * continuing last month's total into this morning's outage.</p>
 *
 * <p><b>Only the most recent occurrence's detail survives, and `occurrences` is what says so.</b>
 * A failing dependency throws on every request; keeping each one would turn one outage into a
 * full database. Keeping the latest `kind`, `detail` and `reference` together means the three
 * describe the same failure rather than being assembled from different ones.</p>
 */
export async function recordFailure(db, { category, route, kind, detail, reference, now }) {
    const cutoff = now - ERROR_RETENTION_MS;

    await db.prepare(
        'INSERT INTO error_log '
        + '  (category, route, kind, detail, reference, occurrences, first_at, last_at) '
        + 'VALUES (?, ?, ?, ?, ?, 1, ?, ?) '
        + 'ON CONFLICT (category, route) DO UPDATE SET '
        + '  occurrences = CASE WHEN error_log.last_at < ? THEN 1 '
        + '                     ELSE error_log.occurrences + 1 END, '
        + '  first_at    = CASE WHEN error_log.last_at < ? THEN excluded.first_at '
        + '                     ELSE error_log.first_at END, '
        + '  kind        = excluded.kind, '
        + '  detail      = excluded.detail, '
        + '  reference   = excluded.reference, '
        + '  last_at     = excluded.last_at')
        .bind(category, route, kind, detail, reference, now, now, cutoff, cutoff)
        .run();
}

// Not exported: upsertUser below is the only caller. A route that wants a user by address
// gets one from the session join instead — the same restriction the id lookup note above
// records, so this stays a private helper rather than a second way in.
async function userByEmail(db, email) {
    return await db.prepare('SELECT id, email, display_name FROM users WHERE email = ?')
        .bind(email).first();
}

// A lookup by id used to live here and nothing called it. Every route that needs a user gets one
// from the session join below, which is the only way a caller can name one — an id arriving from
// anywhere else would be a caller nominating whose data to read.

/**
 * The account for an address, made if there is not one.
 *
 * <p><b>Signing in and signing up are the same act here</b>, which is what a magic link buys:
 * proving you can read the address is the whole of the check either way, so a separate
 * registration step would add a screen and prove nothing extra.</p>
 */
export async function upsertUser(db, { id, email, displayName, now }) {
    await db.prepare(
        'INSERT INTO users (id, email, display_name, created_at) VALUES (?, ?, ?, ?) '
        + 'ON CONFLICT (email) DO NOTHING')
        .bind(id, email, displayName, now).run();

    return await userByEmail(db, email);
}

/**
 * Change the name on one account.
 *
 * <p>Scoped to `id` alone — there is no `email` or anything else in the `WHERE` — so this
 * statement has no way to reach a row other than the one the caller already authenticated as.
 * The value is written as given: `auth.js` is where a name is trimmed, capped and defaulted,
 * because those are decisions about what a name is, not about how one is stored.</p>
 */
export async function setDisplayName(db, { userId, displayName }) {
    await db.prepare('UPDATE users SET display_name = ? WHERE id = ?')
        .bind(displayName, userId).run();
}

export async function putLoginToken(db, { tokenHash, email, expiresAt }) {
    await db.prepare('INSERT INTO login_tokens (token_hash, email, expires_at) VALUES (?, ?, ?)')
        .bind(tokenHash, email, expiresAt).run();
}

/**
 * Spend a login token, returning the address it was issued for — or null.
 *
 * <p><b>One statement, because it has to be.</b> Read-then-write would let the same link be
 * redeemed twice by two requests that both read it unused, which is exactly the property a
 * single-use token exists to have. The `used_at IS NULL` and expiry tests are in the `WHERE`,
 * so the update itself is the check, and `RETURNING` is how the caller learns it won.</p>
 */
export async function spendLoginToken(db, { tokenHash, now }) {
    const row = await db.prepare(
        'UPDATE login_tokens SET used_at = ? '
        + 'WHERE token_hash = ? AND used_at IS NULL AND expires_at > ? '
        + 'RETURNING email')
        .bind(now, tokenHash, now).first();

    return row?.email ?? null;
}

export async function createSession(db, { idHash, userId, expiresAt, now }) {
    await db.prepare(
        'INSERT INTO sessions (id_hash, user_id, expires_at, created_at) VALUES (?, ?, ?, ?)')
        .bind(idHash, userId, expiresAt, now).run();
}

/** Whoever a live session belongs to. Expiry is part of the query, never a check afterwards. */
export async function sessionUser(db, { idHash, now }) {
    return await db.prepare(
        'SELECT u.id, u.email, u.display_name FROM sessions s '
        + 'JOIN users u ON u.id = s.user_id '
        + 'WHERE s.id_hash = ? AND s.expires_at > ?')
        .bind(idHash, now).first();
}

export async function deleteSession(db, idHash) {
    await db.prepare('DELETE FROM sessions WHERE id_hash = ?').bind(idHash).run();
}

/** One character's payload, scoped to its owner. Somebody else's id and no such id look the
 * same here — both come back null — which is what lets the route above answer both with 404. */
export async function getCharacter(db, userId, id) {
    return await db.prepare('SELECT payload FROM characters WHERE user_id = ? AND id = ?')
        .bind(userId, id).first();
}

/**
 * An account's characters, most recently touched first — what a manager list wants.
 *
 * <p><b>`campaign_id` is in the list and is not derived from anything.</b> It is a string the
 * client sent, stored and handed back, exactly as `label` is — the server cannot read it out of
 * the payload because it never parses one. It is never joined to `campaigns` and never checked
 * against it: a character naming a campaign that has been deleted is a state the browser reports,
 * not a state this query repairs.</p>
 */
export async function listCharacters(db, userId) {
    const result = await db.prepare(
        'SELECT id, label, updated_at, campaign_id FROM characters '
        + 'WHERE user_id = ? ORDER BY updated_at DESC')
        .bind(userId).all();

    return result.results;
}

/** This account's cap. Null only if the user row itself does not exist, which a live session
 * never points at. */
export async function characterLimit(db, userId) {
    const row = await db.prepare('SELECT character_limit FROM users WHERE id = ?')
        .bind(userId).first();

    return row?.character_limit ?? null;
}

/**
 * Create or replace a character, refusing only when the account is at its cap *and* this id is
 * not already one of its own.
 *
 * <p><b>One statement, because it has to be — the same reasoning as `spendLoginToken` and
 * `countAttempt` above.</b> A read that counted the account's characters, followed by a write
 * that trusted the count was still true, would let two PUTs arriving together both see room
 * under the cap and both insert — landing the account one over the limit it was just checked
 * against. The `WHERE` on this `INSERT … SELECT` is the check, not a guard in front of it: it
 * lets the literal row through when the id already belongs to this account (so a replace is
 * never refused, however full the account is) or when the account is still under its
 * `character_limit`, and lets nothing through otherwise. `ON CONFLICT` then does the replace
 * when the id was already there, and `RETURNING` is how the caller learns which happened —
 * a row back means stored, nothing back means refused.</p>
 */
export async function putCharacter(db, { userId, id, label, payload, campaignId, now }) {
    const row = await db.prepare(
        'INSERT INTO characters (user_id, id, label, payload, campaign_id, updated_at) '
        + 'SELECT ?, ?, ?, ?, ?, ? '
        + 'WHERE EXISTS (SELECT 1 FROM characters WHERE user_id = ? AND id = ?) '
        + '   OR (SELECT COUNT(*) FROM characters WHERE user_id = ?) '
        + '       < (SELECT character_limit FROM users WHERE id = ?) '
        + 'ON CONFLICT (user_id, id) DO UPDATE SET '
        + '  label = excluded.label, payload = excluded.payload, '
        + '  campaign_id = excluded.campaign_id, updated_at = excluded.updated_at '
        + 'RETURNING id')
        .bind(userId, id, label, payload, campaignId, now, userId, id, userId, userId)
        .first();

    return row !== null;
}

/** Throw one character away. True if a row was actually removed — absent is not this
 * function's business, it is the caller's to turn into 404 or 204. */
export async function deleteCharacter(db, userId, id) {
    const row = await db.prepare('DELETE FROM characters WHERE user_id = ? AND id = ? RETURNING id')
        .bind(userId, id).first();

    return row !== null;
}

// ── Campaigns ────────────────────────────────────────────────────────────────────────────
//
// **Five statements that are the character ones with a different table name, and that is the
// point.** A campaign is another opaque blob belonging to one account; nothing here knows what a
// tier is, and there is no cap, because the account's cap is a cap on characters and inventing a
// second limit would be inventing a rule the contract does not have.

/** One campaign's payload, scoped to its owner. Somebody else's id and no such id look the same. */
export async function getCampaign(db, userId, id) {
    return await db.prepare('SELECT payload FROM campaigns WHERE user_id = ? AND id = ?')
        .bind(userId, id).first();
}

/**
 * An account's campaigns, most recently touched first.
 *
 * <p><b>`join_code` is the one field of a campaign this server can read</b>, and it is here
 * because the GM has to be able to read it out to somebody. It is not derived from the payload
 * and it is not in it — see the migration: redeeming a code means finding the campaign it belongs
 * to, which is a query, and the payload is the one thing no query looks inside.</p>
 */
export async function listCampaigns(db, userId) {
    const result = await db.prepare(
        'SELECT id, label, updated_at, join_code FROM campaigns '
        + 'WHERE user_id = ? ORDER BY updated_at DESC')
        .bind(userId).all();

    return result.results;
}

/**
 * Create or replace a campaign.
 *
 * <p><b>One statement, and no cap to race against</b> — which is the whole difference from
 * `putCharacter`. There the `WHERE` on an `INSERT … SELECT` is the cap check, written that way
 * because a read followed by a write would let two PUTs both see room. Here there is nothing to
 * check, so this is an ordinary upsert and stays one; a cap added later would have to be written
 * in the statement rather than in front of it.</p>
 *
 * <p><b>The join code is minted on insert and kept on update, in the same statement.</b> A
 * campaign nobody can join is useless, so the first write gives it one — but `putCampaign` is
 * what an ordinary save calls, and a code that changed every time the GM renamed the game would
 * lock out every player who had been told the old one. `COALESCE(campaigns.join_code, excluded.…)`
 * is what says "only if there is not one already": rotating is a separate, deliberate act, in
 * `rotateJoinCode` below.</p>
 */
export async function putCampaign(db, { userId, id, label, payload, joinCode, now }) {
    await db.prepare(
        'INSERT INTO campaigns (user_id, id, label, payload, join_code, updated_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?) '
        + 'ON CONFLICT (user_id, id) DO UPDATE SET '
        + '  label = excluded.label, payload = excluded.payload, '
        + '  join_code = COALESCE(campaigns.join_code, excluded.join_code), '
        + '  updated_at = excluded.updated_at')
        .bind(userId, id, label, payload, joinCode, now).run();
}

/**
 * Replace one campaign's join code, and say whether it landed.
 *
 * <p><b>Scoped to the caller's own campaign</b> — `user_id` is in the `WHERE`, so this statement
 * has no way to rotate a code belonging to somebody else. False means no row matched: an id this
 * account does not own, or one that is not there.</p>
 *
 * <p><b>The uniqueness is the index's, not a read in front of this.</b> A `SELECT` that found no
 * campaign holding a candidate code, followed by an `UPDATE` that trusted it, is the race every
 * other statement in this file is written to avoid. The caller retries on a conflict; see
 * `campaigns.rotateCode`.</p>
 *
 * <p><b>Existing memberships are untouched, deliberately.</b> A code is redeemed once, into a
 * membership row that does not refer back to it — so rotating shuts the door without evicting
 * anybody who is already through it. Evicting is a different act (and is out of this slice).</p>
 */
export async function rotateJoinCode(db, { userId, id, joinCode }) {
    const row = await db.prepare(
        'UPDATE campaigns SET join_code = ? WHERE user_id = ? AND id = ? RETURNING id')
        .bind(joinCode, userId, id).first();

    return row !== null;
}

/**
 * Throw one campaign away.
 *
 * <p><b>Characters that name it are deliberately untouched.</b> Not an oversight and not
 * something a `REFERENCES … ON DELETE` clause should be added for: a character whose campaign has
 * gone is reported as naming a campaign that is not here, by the browser, which is the same shape
 * an unknown tier is reported in. Nulling the column here would silently edit characters somebody
 * did not have open, and would make restoring the campaign impossible to undo.</p>
 */
export async function deleteCampaign(db, userId, id) {
    const row = await db.prepare('DELETE FROM campaigns WHERE user_id = ? AND id = ? RETURNING id')
        .bind(userId, id).first();

    return row !== null;
}

// ── Campaign membership: the clone, and the snapshot waiting for a decision ──────────────
//
// **Every statement below is scoped to whoever is asking, and there is no exception.** A GM's
// reads and writes carry `gm_user_id = ?`; a player's carry `player_user_id = ?`. Nothing here
// takes an account id from a caller, and nothing here lets one account name a row belonging to
// another — so there is no query in this file a request could aim at somebody else's character.
//
// **The one read not scoped to the caller is `campaignByJoinCode`, and that is what a join code
// is.** It is a secret the GM minted and chose to hand out; holding it is the whole of the
// authorisation, exactly as holding a sign-in link is. It answers the campaign's own settings and
// nothing else — no account id, no character, no other member's anything. See the note on it, and
// `docs/CHARACTERS-API.md`, which records it as the one place the "only your own rows" rule bends.
//
// **Nothing here parses a payload and nothing here compares two.** The diff the GM reads is
// computed in the browser by the engine, which is the authority on what a character costs.

/**
 * The campaign a join code belongs to, or null.
 *
 * <p><b>This is the one statement in this file that reads a row the caller does not own</b>, and
 * it is deliberate rather than an oversight: joining by a shared code cannot be done any other
 * way. What bounds it is what it answers — the campaign's id, its label and its own opaque
 * payload, which is what the player needs in order to see the tier they are being asked to build
 * to. It never answers an account id, a character, a clone, or anything about another member.</p>
 *
 * <p><b>The code is matched exactly, on the normalised form.</b> Both callers put a code through
 * one normaliser before it reaches here, so the column and the comparison are always in the same
 * spelling and this is an index probe rather than a scan.</p>
 */
export async function campaignByJoinCode(db, joinCode) {
    return await db.prepare(
        'SELECT user_id, id, label, payload FROM campaigns WHERE join_code = ?')
        .bind(joinCode).first();
}

/**
 * Join a campaign, or hand back the membership that already exists.
 *
 * <p><b>One statement, for the reason every upsert in this file is one.</b> A read that found no
 * membership followed by an insert that trusted it would let two clicks on Join both insert, and
 * the unique index would refuse the second with an error the caller has nowhere to report. The
 * `ON CONFLICT` makes a second join an ordinary no-op that returns the row already there.</p>
 *
 * <p><b>`DO UPDATE SET label = excluded.label` rather than `DO NOTHING`</b>, because `RETURNING`
 * on a `DO NOTHING` conflict yields nothing at all — the caller would read a successful re-join
 * as a failure. Refreshing the label is also correct: it is the name the character goes by, and
 * the character may have been renamed since it joined.</p>
 *
 * <p><b>Neither payload is touched here.</b> Joining is not submitting: a player joins, sees the
 * tier, builds, and sends for approval when they choose to. A join that wrote a snapshot would
 * put a character in front of the GM before the player meant it to be seen.</p>
 *
 * <p><b>`gm_user_id` is in the conflict target and comes back in the `RETURNING`</b>, and both
 * halves matter. A `g_…` is unique per account rather than globally — see the migration — so
 * without the column in the key a player joining a second GM's campaign that happens to share an
 * id conflicts with their row in the first, and this hands back a membership belonging to a GM
 * they never joined. The column in the key is the fix; the column in the `RETURNING` is what lets
 * `memberships.join` refuse rather than trust the index, which is the half a test can break.</p>
 */
export async function joinCampaign(
    db, { id, campaignId, gmUserId, playerUserId, characterId, label, now }) {
    return await db.prepare(
        'INSERT INTO campaign_members '
        + '  (id, campaign_id, gm_user_id, player_user_id, character_id, label, joined_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?, ?) '
        + 'ON CONFLICT (gm_user_id, campaign_id, player_user_id, character_id) DO UPDATE SET '
        + '  label = excluded.label '
        + 'RETURNING id, campaign_id, gm_user_id, label, pending_version')
        .bind(id, campaignId, gmUserId, playerUserId, characterId, label, now)
        .first();
}

/**
 * One membership, readable by either side of it and by nobody else.
 *
 * <p><b>The `OR` is the scoping, not a widening of it.</b> A membership has two owners — the GM
 * who owns the campaign and the player who owns the character — and each is entitled to the row
 * for a different reason: the GM because it is a membership of their game, the player because it
 * is their own character in it. A third account matches neither and gets null, which the route
 * turns into the same 404 an id that never existed gets.</p>
 *
 * <p>Both payloads come back, which is what the diff needs: the clone the campaign is holding and
 * the snapshot waiting for a decision. Neither is parsed here or anywhere on this side of the
 * wire.</p>
 *
 * <p><b>The GM's half needs the campaign to still be there and the player's does not</b>, and the
 * asymmetry is the whole design rather than an oversight. A player whose GM deleted the game keeps
 * their membership and is told, on their own screen, that it names a campaign which is not here —
 * their rows are theirs and nothing on this server reaches in to tidy them. A GM who deleted a
 * game has said they are done with it, and a stale link back into an approval screen for it is not
 * a state to report: it is a decision surface for a queue that no longer means anything. No row is
 * touched either way, so restoring the campaign restores all of it.</p>
 */
export async function getMembership(db, userId, id) {
    return await db.prepare(
        'SELECT id, campaign_id, gm_user_id, player_user_id, character_id, label, '
        + '       approved_payload, approved_at, pending_payload, pending_at, pending_version, '
        + '       joined_at '
        + 'FROM campaign_members WHERE id = ? AND ('
        + '     player_user_id = ? '
        + '  OR (gm_user_id = ? AND EXISTS (SELECT 1 FROM campaigns c '
        + '                                 WHERE c.user_id = campaign_members.gm_user_id '
        + '                                   AND c.id = campaign_members.campaign_id)))')
        .bind(id, userId, userId).first();
}

/**
 * Every membership of a character this account owns — the player's half.
 *
 * <p>No payload: this is what a standing beside a character's name is drawn from ("approved",
 * "changes pending", "not submitted"), and a list carrying two whole characters per row would be
 * the thing `SavedCharacterSummary` exists to avoid, one level up.</p>
 */
export async function listMembershipsForPlayer(db, playerUserId) {
    const result = await db.prepare(
        'SELECT id, campaign_id, character_id, label, approved_at, pending_at, pending_version, '
        + '       approved_payload IS NOT NULL AS has_approved, '
        + '       pending_payload IS NOT NULL AS has_pending '
        + 'FROM campaign_members WHERE player_user_id = ? '
        + 'ORDER BY joined_at DESC')
        .bind(playerUserId).all();

    return result.results;
}

/**
 * Every membership of every campaign this account owns — the GM's half.
 *
 * <p>Across all their campaigns rather than one at a time, because both screens want it: the
 * campaign list needs a waiting count per game, and the approval screen needs the rows of one.
 * Two addresses answering from one statement cannot disagree about the count.</p>
 *
 * <p><b>No payload here either</b>, and no account id: a GM learns that a character called
 * something is waiting, never whose account sent it.</p>
 *
 * <p><b>Only memberships of a campaign that is still there.</b> There is no cascade when a
 * campaign is deleted and there is deliberately not going to be one — the player's half of a
 * membership is theirs, and `campaigns.remove` keeps it so that restoring the campaign is a
 * complete undo. But that left the GM being shown a request waiting on a game they had thrown
 * away, with an Approve button under it, which is not honesty about a state: it is a queue that
 * has stopped meaning anything. The `EXISTS` scopes the GM's half to campaigns they still have
 * without touching the row, so a restore brings the whole thing back.</p>
 */
export async function listMembershipsForGm(db, gmUserId) {
    const result = await db.prepare(
        'SELECT id, campaign_id, label, approved_at, pending_at, pending_version, '
        + '       approved_payload IS NOT NULL AS has_approved, '
        + '       pending_payload IS NOT NULL AS has_pending '
        + 'FROM campaign_members WHERE gm_user_id = ? '
        + '  AND EXISTS (SELECT 1 FROM campaigns c '
        + '              WHERE c.user_id = campaign_members.gm_user_id '
        + '                AND c.id = campaign_members.campaign_id) '
        + 'ORDER BY joined_at DESC')
        .bind(gmUserId).all();

    return result.results;
}

/**
 * Send a snapshot for approval, replacing whatever was waiting, and hand back its version.
 *
 * <p><b>One slot and no history.</b> Resubmitting overwrites: approval history and rollback are
 * deliberately out of this design, because one decision queue is what the owner asked for and a
 * row per submission is a version-control system for characters.</p>
 *
 * <p><b>`pending_version + 1`, computed in the statement.</b> Two submissions arriving together
 * would otherwise both read the same number and both write it, which is exactly the state the
 * compare-and-swap on Approve exists to make impossible — a GM holding version 4 would then be
 * able to approve a different snapshot also calling itself 4.</p>
 *
 * <p><b>Scoped to the player</b>: `player_user_id = ?` is in the `WHERE`, so this cannot write a
 * snapshot into a membership belonging to somebody else, however the id was obtained. No row back
 * means exactly that, and the route answers 404.</p>
 *
 * <p><b>And to a campaign that is still there</b>, the same `EXISTS` as the GM's list above and
 * for the other half of the same reason: a submission into a deleted campaign is a snapshot sent
 * to a queue nobody reads, and the player is told it was sent. Deleting is not a way to reject —
 * the clone and the standing survive, so a restore brings back exactly what was there — but
 * accepting new work into a game that is gone is not a state to report, it is one to refuse.</p>
 */
export async function submitToCampaign(db, { id, playerUserId, label, payload, now }) {
    return await db.prepare(
        'UPDATE campaign_members SET '
        + '  label = ?, pending_payload = ?, pending_at = ?, '
        + '  pending_version = pending_version + 1 '
        + 'WHERE id = ? AND player_user_id = ? '
        + '  AND EXISTS (SELECT 1 FROM campaigns c '
        + '              WHERE c.user_id = campaign_members.gm_user_id '
        + '                AND c.id = campaign_members.campaign_id) '
        + 'RETURNING pending_version')
        .bind(label, payload, now, id, playerUserId).first();
}

/**
 * Accept the snapshot the GM was looking at, and only that one.
 *
 * <p><b>This is the compare-and-swap, and it is a real defect's fix rather than a nicety.</b>
 * Without `pending_version = ?` in the `WHERE`: the GM reads snapshot A, the player resubmits B
 * while the diff is on screen, the GM clicks Approve, and B — which nobody has looked at —
 * becomes the campaign's clone. With it the mismatch matches no row, nothing is written, and the
 * caller answers "this changed while you were looking; here it is".</p>
 *
 * <p><b>`pending_payload IS NOT NULL` is in the `WHERE` too</b>, so approving a membership with
 * nothing waiting is refused rather than clearing the clone by copying a null over it.</p>
 *
 * <p><b>`pending_version` is not reset.</b> It keeps counting, so a resubmission after an
 * approval cannot reuse a number the GM might still be holding on screen — the same defect with
 * an extra step.</p>
 *
 * <p>Null back means refused, for either reason; the caller reads the row afterwards to say
 * which.</p>
 */
export async function approveSubmission(db, { id, gmUserId, version, now }) {
    return await db.prepare(
        'UPDATE campaign_members SET '
        + '  approved_payload = pending_payload, approved_at = ?, '
        + '  pending_payload = NULL, pending_at = NULL '
        + 'WHERE id = ? AND gm_user_id = ? AND pending_version = ? '
        + '  AND pending_payload IS NOT NULL '
        + '  AND EXISTS (SELECT 1 FROM campaigns c '
        + '              WHERE c.user_id = campaign_members.gm_user_id '
        + '                AND c.id = campaign_members.campaign_id) '
        + 'RETURNING id, pending_version')
        .bind(now, id, gmUserId, version).first();
}

/**
 * Turn the snapshot down, and only the one the GM was looking at.
 *
 * <p>The same compare-and-swap as `approveSubmission`, for the same reason and with the same
 * refusal: rejecting a snapshot nobody has read is the same fault as approving one. The clone is
 * untouched — a rejection leaves the campaign exactly as it was, which is the whole of what
 * rejecting means.</p>
 *
 * <p><b>The player's own character is untouched too</b>, and it is not this server's to touch:
 * their rows are theirs, the rejection is a decision about the campaign's copy, and nothing here
 * reaches into somebody's own work to undo it.</p>
 */
export async function rejectSubmission(db, { id, gmUserId, version }) {
    return await db.prepare(
        'UPDATE campaign_members SET pending_payload = NULL, pending_at = NULL '
        + 'WHERE id = ? AND gm_user_id = ? AND pending_version = ? '
        + '  AND pending_payload IS NOT NULL '
        + '  AND EXISTS (SELECT 1 FROM campaigns c '
        + '              WHERE c.user_id = campaign_members.gm_user_id '
        + '                AND c.id = campaign_members.campaign_id) '
        + 'RETURNING id, pending_version')
        .bind(id, gmUserId, version).first();
}

/**
 * Count one attempt against a key and say how many are in the current window.
 *
 * <p><b>The window rolls in the statement rather than in a read-modify-write.</b> Two requests
 * arriving together would otherwise both read the old count and both write the same new one,
 * which is a rate limit that stops counting exactly when it is under load. The `CASE`s reset
 * the count and the window together when the old window has passed.</p>
 */
export async function countAttempt(db, { key, now, windowSeconds }) {
    const cutoff = now - windowSeconds;

    const row = await db.prepare(
        'INSERT INTO login_attempts (key, count, window_start) VALUES (?, 1, ?) '
        + 'ON CONFLICT (key) DO UPDATE SET '
        + '  count = CASE WHEN login_attempts.window_start < ? THEN 1 ELSE login_attempts.count + 1 END, '
        + '  window_start = CASE WHEN login_attempts.window_start < ? THEN ? ELSE login_attempts.window_start END '
        + 'RETURNING count')
        .bind(key, now, cutoff, cutoff, now).first();

    return row?.count ?? 0;
}

/**
 * Give an attempt back, because it caused nothing.
 *
 * <p><b>The limit is on mail, not on requests.</b> `countAttempt` runs before the send, which is
 * what makes it a bound on how many messages one address or one machine can cause — but a send
 * the provider refused caused no message, so leaving the count spent charges somebody for
 * something that never happened. That is not a tidiness point: five refused attempts used to
 * leave the sixth silently rate limited, and a rate-limited answer is deliberately identical to
 * a successful one, so a broken mail provider stopped reporting itself after five tries and
 * started reporting success instead.</p>
 *
 * <p>`count > 0` rather than a floor afterwards: the update is the guard, so two refunds racing
 * cannot take a count below zero and hand somebody an extra attempt.</p>
 */
export async function refundAttempt(db, key) {
    await db.prepare('UPDATE login_attempts SET count = count - 1 WHERE key = ? AND count > 0')
        .bind(key).run();
}

/**
 * The invitation for an address, or null.
 *
 * <p>Asked on the way into a sign-in and again on every administrator's request, so it is a
 * primary-key probe by design: `invitations.email` is unique, and the address is normalised to
 * lower case before it ever reaches here.</p>
 */
export async function invitationFor(db, email) {
    return await db.prepare(
        'SELECT id, email, grants_admin, invited_by, created_at FROM invitations WHERE email = ?')
        .bind(email).first();
}

/** One invitation by its own id — what a withdrawal names, so that no address is in a URL. */
export async function invitationById(db, id) {
    return await db.prepare(
        'SELECT id, email, grants_admin, invited_by, created_at FROM invitations WHERE id = ?')
        .bind(id).first();
}

/**
 * Every invitation, oldest first, and whether each address has become an account.
 *
 * <p>Oldest first rather than newest: this list is short and mostly unchanging, and a stable
 * order means a row does not move under the cursor of somebody about to withdraw it.</p>
 *
 * <p>The join is what lets the page tell "invited" from "signed in" — an address that has never
 * been used is one whose link may simply not have arrived, and that is the state worth showing
 * on a site whose mail has already gone wrong once.</p>
 */
export async function listInvitations(db) {
    const result = await db.prepare(
        'SELECT i.id, i.email, i.grants_admin, i.invited_by, i.created_at, u.id AS user_id '
        + 'FROM invitations i LEFT JOIN users u ON u.email = i.email '
        + 'ORDER BY i.created_at ASC, i.email ASC')
        .all();

    return result.results;
}

/**
 * Let one address have an account, and hand back the row.
 *
 * <p>`RETURNING` rather than an insert followed by a read: the caller wants the row it just
 * made, and two statements would let a withdrawal in between turn a successful add into a
 * null nobody expected.</p>
 */
export async function addInvitation(db, { id, email, grantsAdmin, invitedBy, now }) {
    return await db.prepare(
        'INSERT INTO invitations (id, email, grants_admin, invited_by, created_at) '
        + 'VALUES (?, ?, ?, ?, ?) '
        + 'RETURNING id, email, grants_admin, invited_by, created_at')
        .bind(id, email, grantsAdmin, invitedBy, now).first();
}

export async function removeInvitation(db, id) {
    await db.prepare('DELETE FROM invitations WHERE id = ?').bind(id).run();
}

/**
 * End every session an address is holding.
 *
 * <p>By address rather than by user id, because the caller is holding an invitation and an
 * invitation names an address — and because an address with no account yet has no sessions,
 * which this answers correctly by deleting none. Withdrawing permission has to close the door
 * that is already open, or it is a rule about future requests only.</p>
 */
export async function deleteSessionsFor(db, email) {
    await db.prepare(
        'DELETE FROM sessions WHERE user_id IN (SELECT id FROM users WHERE email = ?)')
        .bind(email).run();
}

/**
 * Every recorded failure, newest first — the owner's read of `error_log`.
 *
 * <p><b>Read-only, and there is nothing else here.</b> No delete, no clear — the table is
 * already bounded by its primary key, so there is nothing this needs to reclaim, and a control
 * that could erase a row is a control that could erase the evidence of the thing it is for.</p>
 */
export async function listErrorLog(db) {
    const result = await db.prepare(
        'SELECT category, route, kind, detail, occurrences, first_at, last_at, reference '
        + 'FROM error_log ORDER BY last_at DESC')
        .all();

    return result.results;
}
