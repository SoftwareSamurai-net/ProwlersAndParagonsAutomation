// One failure, two audiences, and the three properties that decide whether this is safe.
//
// The visitor gets a category and a reference and nothing else. The owner gets a row. What is
// asserted here is that the visitor's half discloses nothing — including, and especially, whether
// an address has an account — and that the owner's half is written, redacted, and bounded.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { handle } from '../../worker/index.js';
import { CATEGORIES, redact, routePattern } from '../../worker/errors.js';
import { ERROR_RETENTION_MS } from '../../worker/db.js';
import { cookieFrom, errorRows, request, server, signIn, ORIGIN } from './harness.mjs';

/**
 * An address and a token, of the shapes that really turn up in a provider's own error text.
 *
 * **Both are obviously fake, and that is a requirement rather than a preference.** These are
 * printed to stderr by the server's own `console.error` on every provoked failure, so they land
 * in the CI log of a public repository verbatim — a real address used here would be published by
 * the very test that exists to stop addresses being published. `example.test` is reserved by
 * RFC 6761 and cannot belong to anybody. The first version of this file used a real address; it
 * is the mistake this comment exists to stop somebody repeating.
 */
const ADDRESS = 'someone.real@example.test';
const TOKEN = 'NotARealToken_kQ9wZ2xR7vB4nM6pL1jH8gF5dS0aY3uI';

/**
 * A database that fails only on the statements a needle matches, and works for everything else.
 *
 * <p><b>Needed because the obvious provocation destroys the thing being measured.</b> Replacing
 * the whole database makes the error log's own write fail too, so the redaction assertions would
 * run against an empty table and pass by measuring nothing — the exact shape the positive control
 * below exists to refuse.</p>
 */
function failingOn(db, needle, message) {
    return {
        raw: db.raw,
        prepare(sql) {
            if (!sql.includes(needle)) return db.prepare(sql);

            const statement = {
                bind: () => statement,
                first: async () => { throw new Error(message); },
                all: async () => { throw new Error(message); },
                run: async () => { throw new Error(message); },
            };

            return statement;
        },
    };
}

/** Make the mail provider throw, quoting whatever a real one might quote. */
function mailFailsWith(app, message) {
    app.deps = {
        ...app.deps,
        sendSignInLink: async () => { throw new Error(message); },
    };
}

const askForLink = (app, email) =>
    app.call('/api/auth/request', { method: 'POST', body: { email } });

// ---------------------------------------------------------------------------------------------
// 1. Nothing anybody should read twice reaches the stored row — with a control that there is one.
// ---------------------------------------------------------------------------------------------

test('a stored failure keeps neither the address nor the token out of the message', async () => {
    const app = server();

    // Exactly the shape that makes this worth doing: a provider's error quoting who it was
    // mailing and the credential it was mailed with.
    mailFailsWith(app,
        `Resend refused to send to ${ADDRESS} with key ${TOKEN} (HTTP 422).`);

    const response = await askForLink(app, ADDRESS);
    assert.equal(response.status, 500);

    const rows = errorRows(app.db);

    // **The positive control, and it is not optional.** Every assertion below is an absence, and
    // an absence is satisfied completely by a logger that writes nothing at all. This repository
    // has shipped four guards that passed by measuring nothing; this is the line that stops this
    // being the fifth.
    assert.equal(rows.length, 1, 'no row was written, so the assertions below measure nothing');
    assert.ok(rows[0].detail.length > 0, 'the row has no message, so redaction is untested');

    // And the message still says what happened — a `redact` that returned the empty string would
    // satisfy every absence below while destroying the reason the column exists.
    assert.match(rows[0].detail, /Resend refused to send/);
    assert.match(rows[0].detail, /HTTP 422/);

    assert.ok(!rows[0].detail.includes(ADDRESS), rows[0].detail);
    assert.ok(!rows[0].detail.includes(TOKEN), rows[0].detail);
    assert.ok(!rows[0].detail.includes('example.test'), rows[0].detail);
    assert.ok(!rows[0].detail.includes('someone.real'), rows[0].detail);
});

test('nothing anywhere in the database holds the address or the token from a failure', async () => {
    // The stronger form: not "the detail column is clean" but "no column is". A future field
    // that helpfully kept the raw message would pass the test above and fail this one.
    const app = server();
    mailFailsWith(app, `Refused for ${ADDRESS} using ${TOKEN}.`);

    await askForLink(app, 'someone.else@example.test');

    const everything = JSON.stringify(errorRows(app.db));

    assert.equal(errorRows(app.db).length, 1, 'no row was written; this asserts nothing');
    assert.ok(!everything.includes(ADDRESS), everything);
    assert.ok(!everything.includes(TOKEN), everything);
});

test('the redaction takes out the shapes it claims to, and leaves prose alone', () => {
    // Read directly, because the rule is easier to break than to notice: every case here was
    // reachable through a real provider's error text.
    assert.equal(redact(`near "SELECT": at users.email = ${ADDRESS}`),
        'near "SELECT": at users.email = [address]');

    assert.ok(!redact(`session ${TOKEN} expired`).includes(TOKEN));
    assert.match(redact(`session ${TOKEN} expired`), /session \[redacted\] expired/);

    // A bare local part with no dotted domain is still an address; D1 quotes them that way.
    assert.equal(redact('no row for bob@localhost'), 'no row for [address]');

    // A 64-character hex hash is a long random string even though it is only ever a hash.
    assert.equal(redact('token_hash = ' + 'a'.repeat(64)), 'token_hash = [redacted]');

    // Ordinary words survive, or the column is worthless.
    assert.equal(redact('The mail provider refused the send (HTTP 500).'),
        'The mail provider refused the send (HTTP 500).');

    // And it is capped, so a whole quoted query cannot land here.
    assert.ok(redact('x'.repeat(5000)).length <= 200);

    // Nothing is not a crash.
    assert.equal(redact(undefined), '');
    assert.equal(redact(''), '');
});

// ---------------------------------------------------------------------------------------------
// 2. The public body carries a category and a reference, and no exception text.
// ---------------------------------------------------------------------------------------------

test('the 500 body carries a category and a reference and nothing about what threw', async () => {
    const app = server();
    mailFailsWith(app, `Resend refused to send to ${ADDRESS} with key ${TOKEN} (HTTP 422).`);

    const response = await askForLink(app, ADDRESS);
    const said = await response.text();
    const body = JSON.parse(said);

    assert.equal(response.status, 500);

    assert.equal(body.category, 'mail');
    assert.equal(body.reference, app.reference);
    assert.ok(typeof body.error === 'string' && body.error.length > 0);

    // The exception, in every part somebody could reconstruct it from.
    assert.ok(!said.includes(ADDRESS), said);
    assert.ok(!said.includes(TOKEN), said);
    assert.ok(!said.includes('Resend'), said);
    assert.ok(!said.includes('422'), said);
    assert.ok(!said.includes('stack'), said);

    // Exactly three keys. A field added here is a field the visitor is handed, and the whole
    // design is about that list staying short enough to read.
    assert.deepEqual(Object.keys(body).sort(), ['category', 'error', 'reference']);
});

test('the category the visitor is told is one of the four and never a fifth', async () => {
    const app = server();
    mailFailsWith(app, 'anything at all');

    const body = await (await askForLink(app, 'a@b.test')).json();

    assert.ok(CATEGORIES.includes(body.category), body.category);
});

// ---------------------------------------------------------------------------------------------
// 3. The category never varies with whether the address has an account. This is the security one.
// ---------------------------------------------------------------------------------------------

test('the same failure answers a registered and an unregistered address identically', async () => {
    const app = server();

    // A real account, made the real way — so the difference between the two addresses below is
    // a row in `users`, which is exactly the thing that must not be observable.
    const registered = 'registered@example.test';
    await signIn(app, registered);

    assert.equal(
        app.db.raw.prepare('SELECT COUNT(*) AS n FROM users WHERE email = ?').all(registered)[0].n,
        1, 'the registered address has no account, so this test compares two strangers');
    assert.equal(
        app.db.raw.prepare('SELECT COUNT(*) AS n FROM users WHERE email = ?')
            .all('stranger@example.test')[0].n,
        0, 'the unregistered address has an account, so this test compares two members');

    // One subsystem, broken the same way for both.
    mailFailsWith(app, 'The mail provider refused the send (HTTP 500).');

    const known = await askForLink(app, registered);
    const unknown = await askForLink(app, 'stranger@example.test');

    const knownBody = await known.text();
    const unknownBody = await unknown.text();

    assert.equal(known.status, unknown.status);
    assert.equal(known.headers.get('content-type'), unknown.headers.get('content-type'));

    // Byte for byte. Asking for a link always answers 204 precisely so this endpoint cannot be
    // used to ask whether an address is registered; a category, a message or a reference that
    // differed here would put that oracle straight back through the error path.
    assert.equal(knownBody, unknownBody,
        'the failure body distinguishes a registered address from an unregistered one');

    // And the owner's half does not distinguish them either — one row, not one per account.
    const rows = errorRows(app.db);
    assert.equal(rows.length, 1, 'the recorded failures tell the two addresses apart');
    assert.equal(rows[0].occurrences, 2);
});

test('a failure before any account exists says the same as one after', async () => {
    // The other direction, and worth its own test: the first version of a taxonomy that leaked
    // would leak on the empty database, where every address is a stranger.
    const empty = server();
    mailFailsWith(empty, 'The mail provider refused the send (HTTP 500).');
    const stranger = await (await askForLink(empty, 'nobody@example.test')).text();

    const populated = server();
    await signIn(populated, 'member@example.test');
    mailFailsWith(populated, 'The mail provider refused the send (HTTP 500).');
    const member = await (await askForLink(populated, 'member@example.test')).text();

    assert.equal(stranger, member);
});

// ---------------------------------------------------------------------------------------------
// The categories themselves: the subsystem that failed, and `unknown` still reachable.
// ---------------------------------------------------------------------------------------------

test('a database failure is storage, and it is recorded even though the database is what broke', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.env = {
        ...app.env,
        DB: failingOn(app.db, 'FROM sessions s',
            `D1_ERROR: near "SELECT": syntax error at users.email = ${ADDRESS}`),
    };

    const response = await handle(request('/api/me', { cookie }), app.env, app.deps);
    assert.equal(response.status, 500);
    assert.equal((await response.json()).category, 'storage');

    const rows = errorRows(app.db);
    assert.equal(rows.length, 1);
    assert.equal(rows[0].category, 'storage');
    assert.equal(rows[0].route, '/api/me');
    assert.ok(!rows[0].detail.includes(ADDRESS), rows[0].detail);
});

test('a missing setting is configuration, which is the one that must not say retry', async () => {
    const app = server();
    delete app.env.SITE_URL;

    const response = await askForLink(app, 'a@b.test');

    assert.equal(response.status, 500);
    assert.equal((await response.json()).category, 'configuration');
    assert.equal(errorRows(app.db)[0].category, 'configuration');

    // The behaviour the old test pinned, still true: nothing sent, and no token row for a link
    // that could never have been delivered.
    assert.equal(app.sent.length, 0);
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens').all()[0].n, 0);
});

test('a missing database binding is configuration, not storage and not a TypeError', async () => {
    // Nothing failed — there is nothing there. Reported as `storage` it would send somebody
    // looking at a healthy database; untagged it arrives as `TypeError` on `undefined.prepare`
    // and lands in `unknown`, which is true but useless.
    const app = server();

    const response = await handle(request('/api/me', { cookie: 'pp_session=x' }),
        { ...app.env, DB: undefined }, app.deps);

    assert.equal(response.status, 500);
    assert.equal((await response.json()).category, 'configuration');
});

test('an unclassified failure is unknown, and unknown stays reachable', async () => {
    // `newSecret` is not one of the two wrapped subsystems, so a failure in it is genuinely
    // unclassified — which is what `unknown` is for. A taxonomy whose default is unreachable
    // grows a category for every new failure, and the pressure is then to classify by guessing.
    const app = server();
    app.deps = { ...app.deps, newSecret: () => { throw new Error('no randomness available'); } };

    const response = await askForLink(app, 'a@b.test');

    assert.equal(response.status, 500);
    assert.equal((await response.json()).category, 'unknown');
    assert.equal(errorRows(app.db)[0].category, 'unknown');
});

test('the mail wrapper does not relabel a failure an inner boundary already classified', async () => {
    // The mail send happens inside a request already running against a wrapped database. A
    // storage failure raised while sending must stay `storage`: the innermost boundary is the
    // one that knows which subsystem it is.
    const app = server();
    let thrown;

    app.deps = {
        ...app.deps,
        // **The `env` handed to a route is the wrapped one**, which is the whole point — reaching
        // for `app.env.DB` here instead goes round the storage boundary, so the error arrives
        // untagged and the mail wrapper labels it `mail`. That is what this test did first time
        // and it passed for the wrong reason in the wrong direction.
        sendSignInLink: async (env) => {
            try {
                await env.DB.prepare('SELECT * FROM no_such_table').bind().first();
            } catch (error) {
                thrown = error;
                throw error;
            }
        },
    };

    const response = await askForLink(app, 'a@b.test');

    assert.ok(thrown, 'the database did not throw, so nothing was classified twice');
    assert.equal((await response.json()).category, 'storage');
});

// ---------------------------------------------------------------------------------------------
// Bounded: one row per (category, route), counted, pruned, and no caller-chosen string in it.
// ---------------------------------------------------------------------------------------------

test('a dependency failing on every request writes one row, not one row per request', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM sessions s', 'D1_ERROR: everything is down') };

    for (let i = 0; i < 25; i++) {
        await handle(request('/api/me', { cookie }), app.env, app.deps);
    }

    const rows = errorRows(app.db);

    assert.equal(rows.length, 1, 'an outage wrote ' + rows.length + ' rows; the log is unbounded');

    // **The count is the record of what was dropped.** Only the latest occurrence's message is
    // kept, so without this the log would read as a single failure — a silently truncated log
    // reads as a quiet period, which is the failure mode this number exists to prevent.
    assert.equal(rows[0].occurrences, 25);
    assert.ok(rows[0].first_at <= rows[0].last_at);
});

test('a caller cannot add rows by varying the part of the path they choose', async () => {
    // `/api/characters/{id}` carries a caller-chosen id. Stored as the path, a thousand made-up
    // ids would be a thousand rows — an error log anybody passing by can fill.
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM characters', 'D1_ERROR: down') };

    for (let i = 0; i < 12; i++) {
        await handle(
            request('/api/characters/c_' + String(i).padStart(22, '0'), { cookie }),
            app.env, app.deps);
    }

    const rows = errorRows(app.db);

    assert.equal(rows.length, 1, 'the caller wrote ' + rows.length + ' rows by varying an id');
    assert.equal(rows[0].route, '/api/characters/{id}');
    assert.equal(rows[0].occurrences, 12);

    // And no id reached the table at all.
    assert.ok(!JSON.stringify(rows).includes('c_0000000000000000000'), JSON.stringify(rows));
});

test('a caller cannot add rows by varying a campaign id either', async () => {
    // **The campaign routes need their own arm in `routePattern`, and what it buys is legibility
    // rather than a bound.** An unrecognised path already falls to `other`, which is one row — so
    // the table was never at risk. What was at risk is the log being *readable*: without the arm,
    // every campaign failure lands in the same `other` bucket as a request to an address nobody
    // routes, and the owner cannot tell a broken campaign route from a stray crawler. So this
    // asserts both halves, and the second is the one the arm is for.
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM campaigns', 'D1_ERROR: down') };

    for (let i = 0; i < 1000; i++) {
        await handle(
            request('/api/campaigns/g_' + String(i).padStart(22, '0'), { cookie }),
            app.env, app.deps);
    }

    const rows = errorRows(app.db);

    assert.equal(rows.length, 1, 'the caller wrote ' + rows.length + ' rows by varying an id');
    assert.equal(rows[0].route, '/api/campaigns/{id}',
        'campaign failures are filed under ' + rows[0].route + ' rather than their own pattern');
    assert.equal(rows[0].occurrences, 1000);

    // And no id reached the table at all.
    assert.ok(!JSON.stringify(rows).includes('g_0000000000000000000'), JSON.stringify(rows));
});

test('the route is a pattern from a closed list, and anything else is other', () => {
    const at = path => routePattern(new Request(ORIGIN + path));

    assert.equal(at('/api/me'), '/api/me');
    assert.equal(at('/api/auth/request'), '/api/auth/request');
    assert.equal(at('/api/characters'), '/api/characters');
    assert.equal(at('/api/characters/c_abcdefghijklmnopqrstuv'), '/api/characters/{id}');
    assert.equal(at('/api/campaigns'), '/api/campaigns');
    assert.equal(at('/api/campaigns/g_abcdefghijklmnopqrstuv'), '/api/campaigns/{id}');

    // **The two membership lists are exact addresses and each is filed under its own name.** A
    // GM's inbox failing and a player's standings failing are different faults with different
    // causes; one `/api/memberships` covering both would make the log say only "memberships".
    assert.equal(at('/api/memberships'), '/api/memberships');
    assert.equal(at('/api/memberships/inbox'), '/api/memberships/inbox');
    assert.equal(at('/api/memberships/join'), '/api/memberships/join');

    // **And everything under one membership's id is one pattern, verb included.** `route` is half
    // of a primary key, so a pattern per verb triples the rows this prefix can occupy for no gain:
    // `kind` and `detail` already say which statement threw.
    assert.equal(at('/api/memberships/m_abcdefghijklmnopqrstuv'), '/api/memberships/{id}');
    assert.equal(at('/api/memberships/m_abcdefghijklmnopqrstuv/submission'),
        '/api/memberships/{id}');
    assert.equal(at('/api/memberships/m_abcdefghijklmnopqrstuv/approve'), '/api/memberships/{id}');
    assert.equal(at('/api/memberships/m_abcdefghijklmnopqrstuv/reject'), '/api/memberships/{id}');

    // A campaign's join-code rotation is under the campaign's own id, and so is its pattern.
    assert.equal(at('/api/campaigns/g_abcdefghijklmnopqrstuv/code'), '/api/campaigns/{id}');

    assert.equal(at('/api/me/'), '/api/me', 'a trailing slash is the same address');
    assert.equal(at('/api/nothing-here'), 'other');
    assert.equal(at('/api/../secret'), 'other');
});

test('a stale row starts a fresh count rather than continuing last month’s', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM sessions s', 'D1_ERROR: down') };

    await handle(request('/api/me', { cookie }), app.env, app.deps);
    await handle(request('/api/me', { cookie }), app.env, app.deps);

    const during = errorRows(app.db)[0];
    assert.equal(during.occurrences, 2);
    const firstAt = during.first_at;

    // Long enough afterwards that this is a new fault, not the same one still going.
    app.now += ERROR_RETENTION_MS + 1;
    await handle(request('/api/me', { cookie }), app.env, app.deps);

    const after = errorRows(app.db)[0];

    assert.equal(errorRows(app.db).length, 1);
    assert.equal(after.occurrences, 1,
        'an outage last month is being reported as one continuous fault with this one');
    assert.ok(after.first_at > firstAt, 'first_at was not moved forward with the reset count');
});

test('a long-mended fault is swept away, and a live one is not', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM sessions s', 'D1_ERROR: down') };
    await handle(request('/api/me', { cookie }), app.env, app.deps);

    assert.equal(errorRows(app.db).length, 1, 'nothing was recorded, so the sweep proves nothing');

    // The sweep is opportunistic, on the same path as the other three tables'. A working
    // database again, and somebody asks for a link.
    app.env = { ...app.env, DB: app.db };
    app.now += ERROR_RETENTION_MS + 1;
    await askForLink(app, 'someone@example.test');

    assert.equal(errorRows(app.db).length, 0, 'a fault nobody has seen for a month is still here');

    // The positive control: a *recent* row survives the same sweep. Without this, a sweep that
    // deleted the whole table would pass the assertion above.
    app.env = { ...app.env, DB: failingOn(app.db, 'FROM sessions s', 'D1_ERROR: down again') };
    await handle(request('/api/me', { cookie }), app.env, app.deps);
    assert.equal(errorRows(app.db).length, 1);

    app.env = { ...app.env, DB: app.db };
    await askForLink(app, 'someone.else@example.test');

    assert.equal(errorRows(app.db).length, 1, 'the sweep took a failure from moments ago');
});

test('a failure the log itself cannot record still answers the visitor', async () => {
    // The thing that broke may well be the database the log writes to. A logger that could throw
    // out of the catch would cost the visitor the reference and the category that are the whole
    // visitor-facing half of this design.
    const app = server();
    const exploding = { prepare() { throw new Error('D1_ERROR: everything is down'); } };

    const response = await handle(request('/api/me', { cookie: 'pp_session=x' }),
        { ...app.env, DB: exploding }, app.deps);

    assert.equal(response.status, 500);

    const body = await response.json();
    assert.equal(body.category, 'storage');
    assert.equal(body.reference, app.reference);
});

test('an error log that cannot be pruned does not stop anybody signing in', async () => {
    // **The failure this guards is a deploy that outran its migration.** The prune runs on the
    // sign-in path, so an `error_log` that is not there yet would answer every sign-in with a 500
    // — diagnostics taking down authentication, which is the wrong way round by a long way.
    const app = server();
    app.env = {
        ...app.env,
        DB: failingOn(app.db, 'DELETE FROM error_log', 'no such table: error_log'),
    };
    // **Invited by hand, because this test cannot go through `state.call`.** It needs an `env`
    // of its own — one whose `DELETE FROM error_log` throws — so it calls `handle` directly and
    // misses the scaffolding that quietly invites the address a link is asked for. Without this
    // the gate answers 204 with no mail sent, which is indistinguishable from the pass this
    // test is looking for and is why the assertion below counts the send rather than the status.
    app.invite('a@b.test');

    const response = await handle(
        request('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } }),
        app.env, app.deps);

    assert.equal(response.status, 204, 'a prune that could not run refused a sign-in');
    assert.equal(app.sent.length, 1, 'no link was sent, so the request did not really succeed');
});

test('a session that works is not recorded as a failure', async () => {
    // The negative control for the whole table: an ordinary visit writes nothing. A logger that
    // recorded every request would pass most of the tests above and be useless.
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await app.call('/api/me', { cookie });
    await app.call('/api/nothing-here');
    await app.call('/api/me');

    assert.equal(errorRows(app.db).length, 0);
});

test('the session cookie never reaches the error log', async () => {
    // It is a bearer credential and it is in a header on every failing request. Nothing reads the
    // headers, and this is what says so.
    const app = server();
    const verified = await signIn(app, 'a@b.test');
    const secret = cookieFrom(await handle(
        request('/api/me', { cookie: verified.cookie }), app.env, app.deps));

    app.env = { ...app.env, DB: failingOn(app.db, 'FROM sessions s', 'D1_ERROR: down') };
    await handle(request('/api/me', { cookie: verified.cookie }), app.env, app.deps);

    const stored = JSON.stringify(errorRows(app.db));

    assert.equal(errorRows(app.db).length, 1, 'nothing recorded; this asserts nothing');
    assert.ok(!stored.includes(verified.cookie.split('=')[1]), stored);
    assert.equal(secret, null, 'a plain GET set a cookie, which this test did not expect');
});

// ---------------------------------------------------------------------------------------------
// The tail: `console.error` in the catch is structured, carries the same fields as the row, and
// applies the same redaction — so a tail is not the one place an address or a token still leaks.
// ---------------------------------------------------------------------------------------------

test('the structured log line carries the row’s own fields and none of the raw message', async () => {
    const app = server();
    mailFailsWith(app, `Resend refused to send to ${ADDRESS} with key ${TOKEN} (HTTP 422).`);

    const original = console.error;
    const logged = [];
    console.error = (...args) => logged.push(args);

    try {
        await askForLink(app, ADDRESS);
    } finally {
        console.error = original;
    }

    // **The positive control, and it is not optional.** Every assertion below is an absence, and
    // a `console.error` that had been deleted, or that logged nothing about the failure, would
    // satisfy every one of them. This is the line that says the assertions below measure
    // something.
    assert.equal(logged.length, 1, 'nothing was logged, so the assertions below measure nothing');

    const entry = JSON.parse(logged[0][0]);

    // The same fields the row gets, so a tail and the table agree about one failure.
    assert.equal(entry.category, 'mail');
    assert.equal(entry.route, '/api/auth/request');
    assert.equal(entry.kind, 'Error');
    assert.equal(entry.reference, app.reference);
    assert.match(entry.detail, /Resend refused to send/);
    assert.match(entry.detail, /HTTP 422/);

    // And the same redaction — a tail is exactly the artefact most likely to be pasted into an
    // issue, which is the whole reason the row itself is redacted.
    const raw = JSON.stringify(logged[0]);
    assert.ok(!raw.includes(ADDRESS), raw);
    assert.ok(!raw.includes(TOKEN), raw);
    assert.ok(!raw.includes('example.test'), raw);
});

test('the tail and the row are computed once, not twice — they agree because they are the same object', async () => {
    // The stronger form of the test above: rather than trusting that two independent redactions
    // happened to agree, this reads both halves of one failure and requires them to be
    // byte-identical, field by field.
    const app = server();
    mailFailsWith(app, `Resend refused to send to ${ADDRESS} with key ${TOKEN} (HTTP 422).`);

    const original = console.error;
    let logged;
    console.error = (...args) => { logged = args; };

    try {
        await askForLink(app, ADDRESS);
    } finally {
        console.error = original;
    }

    assert.ok(logged, 'nothing was logged, so this test measures nothing');

    const entry = JSON.parse(logged[0]);
    const row = errorRows(app.db)[0];

    assert.equal(row.category, entry.category);
    assert.equal(row.route, entry.route);
    assert.equal(row.kind, entry.kind);
    assert.equal(row.detail, entry.detail);
    assert.equal(row.reference, entry.reference);
});
