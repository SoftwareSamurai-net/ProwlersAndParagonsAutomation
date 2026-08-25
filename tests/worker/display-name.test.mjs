// Changing your own name, and nobody else's.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { MAX_DISPLAY_NAME_LENGTH } from '../../worker/auth.js';
import { server, signIn } from './harness.mjs';

test('a signed-in account can change its own name', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const changed = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: 'Dorian' } });

    assert.equal(changed.status, 200);
    assert.equal((await changed.json()).displayName, 'Dorian');

    // And it round-trips through the ordinary identity check, not just the response that made
    // the change — a route that answered correctly but wrote nothing would pass the assertion
    // above and fail this one.
    const me = await app.call('/api/me', { cookie });
    assert.equal((await me.json()).displayName, 'Dorian');
});

test('a signed-out caller is refused, and changes nothing', async () => {
    const app = server();
    await signIn(app, 'player@example.test');

    const refused = await app.call('/api/me/display-name',
        { method: 'PUT', body: { displayName: 'Somebody Else' } });

    assert.equal(refused.status, 401);

    const stored = app.db.raw
        .prepare('SELECT display_name FROM users WHERE email = ?').all('player@example.test');
    assert.equal(stored[0].display_name, 'player', 'the refused request changed the row anyway');
});

test('a request from another origin is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const response = await app.call('/api/me/display-name', {
        method: 'PUT', cookie, body: { displayName: 'Dorian' }, origin: 'https://evil.attacker.test',
    });

    assert.equal(response.status, 403);
});

test('a name changes only the caller\'s own row', async () => {
    const app = server();
    const a = await signIn(app, 'a@example.test');
    const b = await signIn(app, 'b@example.test');

    await app.call('/api/me/display-name',
        { method: 'PUT', cookie: a.cookie, body: { displayName: 'Account A' } });

    const bNow = await app.call('/api/me', { cookie: b.cookie });
    assert.equal((await bNow.json()).displayName, 'b', 'changing one account\'s name moved another\'s');
});

test('leading and trailing whitespace is trimmed', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const changed = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: '  Dorian  ' } });

    assert.equal((await changed.json()).displayName, 'Dorian');
});

test('a blank name resets to the email\'s local part rather than being refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    await app.call('/api/me/display-name', { method: 'PUT', cookie, body: { displayName: 'Dorian' } });
    const reset = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: '   ' } });

    assert.equal(reset.status, 200);
    assert.equal((await reset.json()).displayName, 'player');
});

test('a name that is not a string is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const response = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: 12345 } });

    assert.equal(response.status, 400);
});

test('a name carrying a control character is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const response = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: 'Dorian\nSheiles' } });

    assert.equal(response.status, 400);

    const stored = app.db.raw
        .prepare('SELECT display_name FROM users WHERE email = ?').all('player@example.test');
    assert.equal(stored[0].display_name, 'player', 'the refused name was stored anyway');
});

test('a name past the length cap is refused, and the cap is not decoration', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    // The positive control: exactly at the cap succeeds, so the failure below is the length and
    // not some other property of a long string.
    const atCap = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: 'x'.repeat(MAX_DISPLAY_NAME_LENGTH) } });
    assert.equal(atCap.status, 200);

    const overCap = await app.call('/api/me/display-name',
        { method: 'PUT', cookie, body: { displayName: 'x'.repeat(MAX_DISPLAY_NAME_LENGTH + 1) } });
    assert.equal(overCap.status, 400);
});

test('the method is checked', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const response = await app.call('/api/me/display-name', { method: 'GET', cookie });

    assert.equal(response.status, 405);
});
