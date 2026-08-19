// Every statement this system runs, in one file.
//
// Not for tidiness: the handlers above are then readable as policy — who may do what — with no
// SQL in among it, and the queries are reviewable as a set. Two of them do work that would be a
// race if it were written as a read followed by a write, and both are called out below.

/** Rows whose time is up, removed opportunistically. Cheap, indexed, and keeps the table small. */
export async function sweepExpired(db, now) {
    await db.prepare('DELETE FROM login_tokens WHERE expires_at < ?').bind(now).run();
    await db.prepare('DELETE FROM sessions WHERE expires_at < ?').bind(now).run();
}

export async function userByEmail(db, email) {
    return await db.prepare('SELECT id, email, display_name FROM users WHERE email = ?')
        .bind(email).first();
}

export async function userById(db, id) {
    return await db.prepare('SELECT id, email, display_name FROM users WHERE id = ?')
        .bind(id).first();
}

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

export async function getCharacter(db, userId) {
    return await db.prepare('SELECT payload, updated_at FROM characters WHERE user_id = ?')
        .bind(userId).first();
}

export async function putCharacter(db, { userId, payload, now }) {
    await db.prepare(
        'INSERT INTO characters (user_id, payload, updated_at) VALUES (?, ?, ?) '
        + 'ON CONFLICT (user_id) DO UPDATE SET payload = excluded.payload, updated_at = excluded.updated_at')
        .bind(userId, payload, now).run();
}

export async function deleteCharacter(db, userId) {
    await db.prepare('DELETE FROM characters WHERE user_id = ?').bind(userId).run();
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
