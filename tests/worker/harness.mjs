// What the accounts API is driven against.
//
// **The database is real SQLite running the real migration**, not a stub that agrees with
// whatever the queries happen to say. D1 *is* SQLite, so the statements in `worker/db.js` —
// including the two that depend on `ON CONFLICT … RETURNING` behaving exactly as written — are
// executed rather than described. A hand-written fake would have passed for either of the
// races those statements exist to close.
//
// Only three things are stubbed, and each is stubbed because it is not the system under test:
// the clock, so expiry can be reached without waiting; the secrets, which stay real but are
// read out of the mail; and the mail provider itself, which is somebody else's HTTP API.

import { DatabaseSync } from 'node:sqlite';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { handle, production } from '../../worker/index.js';

const here = dirname(fileURLToPath(import.meta.url));

// **Every migration, in order** — a test running against only `0001` would pass against a
// schema nobody deploys. Adding a migration later means adding it to this list, not discovering
// that the suite quietly stopped exercising it. `migration.test.mjs` indexes into this by
// position, so append rather than insert.
export const MIGRATIONS = [
    join(here, '..', '..', 'd1', 'migrations', '0001_accounts.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0002_characters_list.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0003_invitations.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0004_error_log.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0005_campaigns.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0006_campaign_membership.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0007_decision_recorded.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0008_character_index_fields.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0009_campaign_format.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0010_character_variant_index.sql'),
    join(here, '..', '..', 'd1', 'migrations', '0011_villain_handover.sql'),
];

export const ORIGIN = 'https://pp.example.test';

/**
 * D1's shape over node:sqlite.
 *
 * The methods are the three `worker/db.js` uses and no more, plus `batch` below, so a query reaching for something
 * D1 has and this does not fails loudly here rather than in production. `first()` goes through
 * `all()` because a statement with `RETURNING` produces rows, and `run()` on one discards them.
 */
export function database() {
    const sqlite = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS) sqlite.exec(readFileSync(migration, 'utf8'));

    const wrap = sql => {
        const statement = sqlite.prepare(sql);
        const bound = [];

        const api = {
            bind(...values) {
                bound.push(...values);
                return api;
            },
            async first() {
                return statement.all(...bound)[0] ?? null;
            },
            async all() {
                return { results: statement.all(...bound), success: true };
            },
            async run() {
                // `all` rather than `run`, because SQLite refuses to step a statement that
                // returns rows through the non-row path in some drivers, and both are used here.
                statement.all(...bound);
                return { success: true };
            },
        };

        return api;
    };

    /**
     * D1's `batch`: every statement in one transaction, in order, all or nothing.
     *
     * <p>Added for the nemesis handover, the one write in `worker/db.js` that touches three rows
     * in two tables. **It rolls back on a throw, as D1 documents**, so a test that makes a later
     * statement fail can see that the earlier ones left nothing behind — a batch that merely ran
     * its statements in a row would pass every test that only ever succeeds.</p>
     */
    const batch = async statements => {
        sqlite.exec('BEGIN');
        try {
            const results = [];
            for (const statement of statements) results.push(await statement.all());
            sqlite.exec('COMMIT');
            return results;
        } catch (error) {
            sqlite.exec('ROLLBACK');
            throw error;
        }
    };

    return { prepare: wrap, batch, raw: sqlite };
}

/**
 * A server, with the clock and the mail provider in the test's hands.
 *
 * `now` is a mutable field rather than a fixed value so a test can move time forward between
 * calls — which is the only way to reach a token's expiry or a rate limit's window without the
 * suite taking an hour.
 */
export function server({ now = Date.parse('2026-08-19T10:00:00Z'), gated = false, admin } = {}) {
    const db = database();
    const sent = [];
    const invitationsSent = [];

    const state = {
        now,
        sent,
        invitationsSent,
        db,
        // The failure reference, held still for the same reason the clock is. Production mints a
        // random one per failure, which would make two otherwise identical 500 bodies differ for
        // a reason that has nothing to do with what a test is asking — and one test here requires
        // two bodies to be identical byte for byte.
        reference: 'aa11bb',
        env: {
            DB: db,
            SITE_URL: ORIGIN,
            MAIL_FROM: 'no-reply@example.test',
            RESEND_API_KEY: 'not-a-real-key',
            ADMIN_EMAIL: admin ?? '',
            PLAYER_KEY_SECRET: 'test-player-key-secret-not-real',
        },
    };

    /**
     * Let one address have an account, by writing the row rather than through the page.
     *
     * The page needs an administrator, an administrator needs an account, and an account needs
     * one of these — so a test that wants a signed-in anybody has to start below the endpoint,
     * the same way the deployment starts below it with an environment variable.
     */
    state.invite = (email, { grantsAdmin = false } = {}) => {
        db.raw.prepare(
            'INSERT OR IGNORE INTO invitations (id, email, grants_admin, invited_by, created_at) '
            + 'VALUES (?, ?, ?, NULL, ?)')
            .run('i_' + email.replace(/[^a-z0-9]/gi, '').padEnd(22, 'x').slice(0, 22),
                email.trim().toLowerCase(), grantsAdmin ? 1 : 0, state.now);
    };

    state.deps = {
        ...production,
        now: () => state.now,
        newReference: () => state.reference,
        sendSignInLink: async (env, message) => {
            sent.push(message);
        },
        // A separate array from `sent`: an invitation mail and a sign-in link are different
        // messages to different assertions, and a test asserting nothing was mailed to a
        // stranger should not have to know an invitation is a second kind of mail.
        sendInvitationMail: async (env, message) => {
            invitationsSent.push(message);
        },
    };

    /**
     * A request, with one deliberate piece of scaffolding: **the allow-list is off by default.**
     *
     * <p>Nearly every test in this suite was written before an invitation was needed and is
     * about something else — the rate limit, the token's lifetime, what is stored in the clear —
     * so each would otherwise have to invite an address it does not care about, and the ones
     * that mail a different address per iteration could not do it at all. So a request for a
     * link quietly invites the address it names first.</p>
     *
     * <p><b>Which means those tests say nothing about the gate</b>, and it is
     * `invitations.test.mjs` that does — it passes `gated: true`, which turns this off, and
     * asserts on both sides of the list. There is a test in that file that this scaffolding
     * really is doing something, because a bypass that had stopped working would leave every
     * test here passing for the wrong reason.</p>
     */
    state.call = (path, init = {}) => {
        if (!gated && path === '/api/auth/request' && typeof init.body?.email === 'string') {
            state.invite(init.body.email);
        }

        return handle(request(path, init), state.env, state.deps);
    };

    return state;
}

/**
 * A request at this site's own origin, with the header a browser's own fetch would set.
 *
 * `raw` sends a body exactly as given, which is how the tests about what is stored can send
 * key order and spacing that no serialiser would reproduce.
 */
export function request(path, { method = 'GET', body, raw, cookie, origin = ORIGIN, headers = {} } = {}) {
    const all = { ...headers };
    if (origin !== null) all.origin = origin;
    if (cookie) all.cookie = cookie;

    const payload = raw !== undefined ? raw : body === undefined ? undefined : JSON.stringify(body);
    if (payload !== undefined) all['content-type'] = 'application/json';

    return new Request(ORIGIN + path, { method, headers: all, body: payload });
}

/** The `pp_session=…` pair from a response, ready to be sent back as a cookie header. */
export function cookieFrom(response) {
    const header = response.headers.get('set-cookie');

    return header ? header.split(';')[0] : null;
}

/** The token out of the most recent sign-in mail. */
export function tokenFrom(sent) {
    return new URL(sent.at(-1).link).searchParams.get('t');
}

/**
 * Sign somebody in, all the way through, and hand back their cookie.
 *
 * Used by the tests that are about something else. It goes through the real endpoints rather
 * than inserting a session row, so a test about characters cannot pass against an auth flow
 * that has stopped working.
 */
export async function signIn(state, email) {
    const asked = await state.call('/api/auth/request', { method: 'POST', body: { email } });
    if (asked.status !== 204) throw new Error('Could not ask for a link: ' + asked.status);

    const verified = await state.call('/api/auth/verify',
        { method: 'POST', body: { token: tokenFrom(state.sent) } });
    if (verified.status !== 200) throw new Error('Could not verify: ' + verified.status);

    return { cookie: cookieFrom(verified), identity: await verified.json() };
}

/** Every recorded failure, newest first. The owner's half of the wire, as they would read it. */
export function errorRows(db) {
    return db.raw.prepare('SELECT * FROM error_log ORDER BY last_at DESC').all();
}

/** Every value in the database, as one string, for asking what is stored in the clear. */
export function everythingStored(db) {
    const tables = db.raw.prepare(
        "SELECT name FROM sqlite_master WHERE type = 'table'").all().map(r => r.name);

    return tables
        .flatMap(t => db.raw.prepare('SELECT * FROM "' + t + '"').all())
        .map(row => Object.values(row).join(' '))
        .join(' ');
}
