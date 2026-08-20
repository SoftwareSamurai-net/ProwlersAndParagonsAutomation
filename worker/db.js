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
        console.error('Could not prune the error log. Has migration 0003 been applied?', error);
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

export async function userByEmail(db, email) {
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

/** An account's characters, most recently touched first — what a manager list wants. */
export async function listCharacters(db, userId) {
    const result = await db.prepare(
        'SELECT id, label, updated_at FROM characters WHERE user_id = ? ORDER BY updated_at DESC')
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
export async function putCharacter(db, { userId, id, label, payload, now }) {
    const row = await db.prepare(
        'INSERT INTO characters (user_id, id, label, payload, updated_at) '
        + 'SELECT ?, ?, ?, ?, ? '
        + 'WHERE EXISTS (SELECT 1 FROM characters WHERE user_id = ? AND id = ?) '
        + '   OR (SELECT COUNT(*) FROM characters WHERE user_id = ?) '
        + '       < (SELECT character_limit FROM users WHERE id = ?) '
        + 'ON CONFLICT (user_id, id) DO UPDATE SET '
        + '  label = excluded.label, payload = excluded.payload, updated_at = excluded.updated_at '
        + 'RETURNING id')
        .bind(userId, id, label, payload, now, userId, id, userId, userId)
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
