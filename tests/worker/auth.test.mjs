// Signing in, and every way it is meant to refuse.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { LINKS_PER_ADDRESS_PER_HOUR, SESSION_LIFETIME_MS, TOKEN_LIFETIME_MS } from '../../worker/auth.js';
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
        const saved = await app.call('/api/character',
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
