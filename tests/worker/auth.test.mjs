// Signing in, and every way it is meant to refuse.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
    LINKS_PER_ADDRESS_PER_HOUR, LINKS_PER_CLIENT_PER_HOUR, SESSION_LIFETIME_MS, TOKEN_LIFETIME_MS,
} from '../../worker/auth.js';
import { cookieFrom, everythingStored, ORIGIN, server, signIn, tokenFrom } from './harness.mjs';

test('a link is mailed, spent, and leaves the caller signed in', async () => {
    const app = server();

    const asked = await app.call('/api/auth/request',
        { method: 'POST', body: { email: 'Player@Example.test' } });

    assert.equal(asked.status, 204);
    assert.equal(app.sent.length, 1, 'no mail was sent, so nothing below proves anything');

    // The address is normalised on the way in, and it is the normalised one that is written to.
    assert.equal(app.sent[0].to, 'player@example.test');
    assert.ok(app.sent[0].link.startsWith(ORIGIN + '/signin?t='), app.sent[0].link);

    const verified = await app.call('/api/auth/verify',
        { method: 'POST', body: { token: tokenFrom(app.sent) } });

    assert.equal(verified.status, 200);
    assert.deepEqual(Object.keys(await verified.clone().json()).sort(), ['displayName', 'key']);
    assert.equal((await verified.json()).displayName, 'player');

    const cookie = cookieFrom(verified);
    assert.ok(cookie?.startsWith('pp_session='), cookie);

    const who = await app.call('/api/me', { cookie });
    assert.equal(who.status, 200);
});

test('the identity carries a usable key, not just the right field names', async () => {
    // **A contract on field names is not a contract.** `AccountsContractTests` compares the keys
    // the server returns against the names the client binds, and a fix audit returned
    // `{ key: null, displayName: … }` — same names, both suites green. The consequence is the
    // worst-shaped one available: the server establishes the session and sets the cookie, and the
    // client reads a null key as "nobody is signed in". A session that exists and is disowned.
    const app = server();
    const { identity, cookie } = await signIn(app, 'player@example.test');

    assert.equal(typeof identity.key, 'string');
    assert.ok(identity.key.length > 0, 'the key is empty');
    assert.equal(typeof identity.displayName, 'string');

    // And the same key comes back from the other route, so the two cannot answer differently
    // about who somebody is.
    const me = await (await app.call('/api/me', { cookie })).json();

    assert.equal(me.key, identity.key);
    assert.equal(me.displayName, identity.displayName);

    // It is the account's own id — the row the session points at, not something reflected back
    // from the request.
    const stored = app.db.raw.prepare('SELECT id FROM users WHERE email = ?').all('player@example.test');
    assert.equal(stored.length, 1);
    assert.equal(identity.key, stored[0].id);
});

test('the cookie is HttpOnly, Secure and SameSite=Lax', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');
    assert.ok(cookie);

    const header = (await app.call('/api/auth/verify',
        { method: 'POST', body: { token: 'no' } })).headers.get('set-cookie');

    // A refusal sets no cookie at all; the attributes are asserted on the one that succeeded.
    assert.equal(header, null);

    const app2 = server();
    const asked = await app2.call('/api/auth/request',
        { method: 'POST', body: { email: 'a@b.test' } });
    assert.equal(asked.status, 204);

    const verified = await app2.call('/api/auth/verify',
        { method: 'POST', body: { token: tokenFrom(app2.sent) } });
    const set = verified.headers.get('set-cookie');

    assert.match(set, /HttpOnly/);
    assert.match(set, /Secure/);
    assert.match(set, /SameSite=Lax/);
    assert.match(set, new RegExp('Max-Age=' + SESSION_LIFETIME_MS / 1000 + '\\b'));
});

test('a link works once', async () => {
    const app = server();
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    const token = tokenFrom(app.sent);

    assert.equal((await app.call('/api/auth/verify', { method: 'POST', body: { token } })).status, 200);
    assert.equal((await app.call('/api/auth/verify', { method: 'POST', body: { token } })).status, 401);
});

test('a link expires', async () => {
    const app = server();
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    const token = tokenFrom(app.sent);

    app.now += TOKEN_LIFETIME_MS + 1;

    assert.equal((await app.call('/api/auth/verify', { method: 'POST', body: { token } })).status, 401);
});

test('a session expires', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    assert.equal((await app.call('/api/me', { cookie })).status, 200);

    app.now += SESSION_LIFETIME_MS + 1;

    assert.equal((await app.call('/api/me', { cookie })).status, 401);
});

test('every refusal to verify says the same thing', async () => {
    const app = server();
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    const token = tokenFrom(app.sent);
    await app.call('/api/auth/verify', { method: 'POST', body: { token } });

    const used = await app.call('/api/auth/verify', { method: 'POST', body: { token } });
    const never = await app.call('/api/auth/verify', { method: 'POST', body: { token: 'made-up' } });

    assert.equal(used.status, never.status);
    assert.deepEqual(await used.json(), await never.json());
});

test('asking for a link says nothing about whether the address has an account', async () => {
    const app = server();
    await signIn(app, 'known@example.test');

    const known = await app.call('/api/auth/request',
        { method: 'POST', body: { email: 'known@example.test' } });
    const unknown = await app.call('/api/auth/request',
        { method: 'POST', body: { email: 'stranger@example.test' } });

    assert.equal(known.status, unknown.status);
    assert.equal(await known.text(), await unknown.text());
});

test('the rate limit stops the mail without changing the answer', async () => {
    const app = server();

    for (let i = 0; i <= LINKS_PER_ADDRESS_PER_HOUR; i++) {
        const response = await app.call('/api/auth/request',
            { method: 'POST', body: { email: 'a@b.test' } });
        assert.equal(response.status, 204);
    }

    assert.equal(app.sent.length, LINKS_PER_ADDRESS_PER_HOUR,
        'the limit did not bite, so this test is asserting nothing');

    // One more, over the limit: still 204, still no mail.
    const over = await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    assert.equal(over.status, 204);
    assert.equal(app.sent.length, LINKS_PER_ADDRESS_PER_HOUR);

    // And the window rolls rather than latching for ever.
    app.now += 61 * 60 * 1000;
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    assert.equal(app.sent.length, LINKS_PER_ADDRESS_PER_HOUR + 1);
});

test('one machine cannot mail a link to every address it knows', async () => {
    // **This limit had no test at all**, found by a reviewer: deleting the `byClient` half of the
    // guard left all 36 tests passing, because nothing in the suite ever set the header the limit
    // is keyed on, so every call counted as the same `unknown` source. The address limit does not
    // cover this — the whole point of it is a different address every time.
    const app = server();
    const from = { 'cf-connecting-ip': '203.0.113.9' };

    for (let i = 0; i <= LINKS_PER_CLIENT_PER_HOUR; i++) {
        const response = await app.call('/api/auth/request',
            { method: 'POST', body: { email: `victim${i}@example.test` }, headers: from });

        assert.equal(response.status, 204);
    }

    assert.equal(app.sent.length, LINKS_PER_CLIENT_PER_HOUR,
        'the per-client limit did not bite, so this test is asserting nothing');

    // The positive control, and the thing that makes this a *client* limit rather than a global
    // one: another source is unaffected, on an address that has asked for nothing.
    await app.call('/api/auth/request', {
        method: 'POST',
        body: { email: 'somebody@example.test' },
        headers: { 'cf-connecting-ip': '198.51.100.4' },
    });

    assert.equal(app.sent.length, LINKS_PER_CLIENT_PER_HOUR + 1);
});

test('no header a caller can write moves the rate-limit bucket', async () => {
    // Only `CF-Connecting-IP` is set by Cloudflare's own edge; every header below can be written
    // by whoever is calling. **Naming one of them was not enough**: a fix audit made the code
    // trust `X-Real-IP` as well and mailed 26 links against a cap of 20 with all 42 tests green,
    // because the guard only knew about `X-Forwarded-For`. So the whole family is varied at once
    // and the limit still has to bite.
    const spoofable =
        ['x-forwarded-for', 'x-real-ip', 'x-client-ip', 'x-cluster-client-ip', 'forwarded',
         'true-client-ip', 'x-original-forwarded-for', 'client-ip', 'remote-addr'];

    const app = server();

    for (let i = 0; i <= LINKS_PER_CLIENT_PER_HOUR + 1; i++) {
        // Every one of them different on every call, so any single header being read as the
        // source gives this caller a fresh bucket each time.
        const headers = Object.fromEntries(spoofable.map(h => [h, `203.0.113.${i}`]));

        await app.call('/api/auth/request',
            { method: 'POST', body: { email: `victim${i}@example.test` }, headers });
    }

    assert.equal(app.sent.length, LINKS_PER_CLIENT_PER_HOUR,
        'varying a caller-supplied header got past the per-client limit, so one of '
        + spoofable.join('/') + ' is being read as the source');
});

test('the link is addressed to the configured site and nothing else can move it', async () => {
    // **The refusal below is not enough on its own.** It only asks what happens when SITE_URL is
    // missing; a fix audit added a second header the code trusted *in addition* — leaving the
    // refusal intact — and mailed a link to `https://evil.attacker.test` with all 42 tests green.
    // What has to be asserted is where the link points, not that a fallback was removed.
    const app = server();

    const hostile = {
        'x-forwarded-host': 'evil.attacker.test',
        'x-forwarded-proto': 'http',
        'x-original-host': 'evil.attacker.test',
        'x-host': 'evil.attacker.test',
        forwarded: 'host=evil.attacker.test',
        host: 'evil.attacker.test',
    };

    await app.call('/api/auth/request',
        { method: 'POST', body: { email: 'a@b.test' }, headers: hostile });

    assert.equal(app.sent.length, 1, 'nothing was sent, so this asserts nothing');
    assert.equal(new URL(app.sent[0].link).origin, ORIGIN,
        'the link was addressed somewhere other than SITE_URL: ' + app.sent[0].link);
});

test('a site with no configured address sends nothing and says so', async () => {
    // The link used to be addressed from the origin of the request, which is derived from the
    // host it arrived on. Refusing is the safe direction: a link minted for a host somebody else
    // controls carries a live token, because the token *is* the credential.
    const app = server();
    delete app.env.SITE_URL;

    const response = await app.call('/api/auth/request',
        { method: 'POST', body: { email: 'a@b.test' } });

    assert.equal(response.status, 500);
    assert.equal(app.sent.length, 0);

    // And no token row was written for a link that could never be delivered.
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens').all()[0].n, 0);
});

test('the rate-limit table does not grow for ever, in either kind of key', async () => {
    const app = server();

    // **Both prefixes, because checking one was not enough**: a fix audit restricted the sweep to
    // `key LIKE 'email:%'` and left fifty `ip:` rows behind with all 42 tests green. Every row in
    // this table is one of the two, and a sweep that misses a kind is a sweep that does nothing
    // for the kind there are most of.
    await app.call('/api/auth/request', {
        method: 'POST',
        body: { email: 'a@b.test' },
        headers: { 'cf-connecting-ip': '203.0.113.1' },
    });

    const before = app.db.raw.prepare('SELECT key FROM login_attempts').all().map(r => r.key);
    assert.ok(before.some(k => k.startsWith('email:')), 'no address window was written');
    assert.ok(before.some(k => k.startsWith('ip:')), 'no source window was written');

    // A day later, somebody else asks. The old windows carry no information — the window rolls
    // inside the statement — so they are swept. Nothing about the counting looked wrong while
    // they accumulated, which is why this is a slow leak rather than a fault.
    app.now += 25 * 60 * 60 * 1000;
    await app.call('/api/auth/request', {
        method: 'POST',
        body: { email: 'c@d.test' },
        headers: { 'cf-connecting-ip': '198.51.100.2' },
    });

    const keys = app.db.raw.prepare('SELECT key FROM login_attempts').all().map(r => r.key);

    assert.deepEqual(keys.filter(k => k.startsWith('email:')), ['email:c@d.test']);
    assert.deepEqual(keys.filter(k => k.startsWith('ip:')), ['ip:198.51.100.2']);
});

test('one address being rate limited does not lock out another', async () => {
    const app = server();

    for (let i = 0; i <= LINKS_PER_ADDRESS_PER_HOUR + 2; i++) {
        await app.call('/api/auth/request', { method: 'POST', body: { email: 'noisy@b.test' } });
    }

    const before = app.sent.length;
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'quiet@b.test' } });

    assert.equal(app.sent.length, before + 1);
});

test('neither the token nor the session secret is stored in the clear', async () => {
    const app = server();
    await app.call('/api/auth/request', { method: 'POST', body: { email: 'a@b.test' } });
    const token = tokenFrom(app.sent);

    const verified = await app.call('/api/auth/verify', { method: 'POST', body: { token } });
    const secret = cookieFrom(verified).slice('pp_session='.length);

    const stored = everythingStored(app.db);

    // The positive control: the row is there at all, found by its hash.
    assert.ok(stored.includes('a@b.test'), 'nothing was stored, so the absences below are free');
    assert.ok(token.length > 20 && secret.length > 20);

    assert.ok(!stored.includes(token), 'the login token is in the database in the clear');
    assert.ok(!stored.includes(secret), 'the session secret is in the database in the clear');
});

test('signing out ends the session rather than only the cookie', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const out = await app.call('/api/auth/signout', { method: 'POST', cookie });

    assert.equal(out.status, 204);
    assert.match(out.headers.get('set-cookie'), /Max-Age=0/);

    // The cookie is presented again, as somebody who had copied it would. It is dead.
    assert.equal((await app.call('/api/me', { cookie })).status, 401);
});

test('signing in twice on one address is one account', async () => {
    const app = server();

    const first = await signIn(app, 'a@b.test');
    const second = await signIn(app, 'A@B.test');

    assert.equal(first.identity.key, second.identity.key);
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM users').all()[0].n, 1);
});

test('a state-changing request must come from this site', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (const origin of ['https://elsewhere.test', null]) {
        const asked = await app.call('/api/auth/request',
            { method: 'POST', body: { email: 'a@b.test' }, origin });
        const out = await app.call('/api/auth/signout', { method: 'POST', cookie, origin });
        // `/api/character` (singular) is gone; the characters slice's `/api/characters/{id}`
        // is the state-changing address that needs the same origin check now.
        const saved = await app.call('/api/characters/c_0000000000000000000000',
            { method: 'PUT', body: { any: 'thing' }, cookie, origin });

        assert.equal(asked.status, 403, 'origin ' + origin);
        assert.equal(out.status, 403, 'origin ' + origin);
        assert.equal(saved.status, 403, 'origin ' + origin);
    }

    // The positive control: the same three calls from this site are not refused.
    assert.equal((await app.call('/api/auth/signout', { method: 'POST', cookie })).status, 204);
});

test('an address that is not one is refused before anything is stored', async () => {
    const app = server();

    for (const email of ['', 'nope', 'a b@c.test', '@b.test', 'a@', 'a@b@c', 42, null, undefined]) {
        const response = await app.call('/api/auth/request', { method: 'POST', body: { email } });
        assert.equal(response.status, 400, JSON.stringify(email));
    }

    assert.equal(app.sent.length, 0);
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens').all()[0].n, 0);
});
