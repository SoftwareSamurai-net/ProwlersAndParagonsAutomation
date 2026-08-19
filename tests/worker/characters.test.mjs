// One character, belonging to one account, following somebody to another browser.
//
// That last sentence is the whole slice, and the test named for it is the one that would fail
// if any part of the chain stopped working.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { server, signIn } from './harness.mjs';

/** Shaped like what `CharacterStore` writes: a version, a mode, and the inputs. */
const character = {
    Version: 1,
    Mode: 0,
    Sheet: { Name: 'Ninefold', AbilityRanks: { might: 8, agility: 6 }, SelectedPowers: [] },
};

test('a character follows its account to another browser', async () => {
    const app = server();
    const laptop = await signIn(app, 'player@example.test');

    assert.equal((await app.call('/api/character',
        { method: 'PUT', body: character, cookie: laptop.cookie })).status, 204);

    // A second sign-in from somewhere else: a new link, a new session, the same account.
    const phone = await signIn(app, 'player@example.test');
    assert.notEqual(phone.cookie, laptop.cookie, 'the same session was reused, so this proves nothing');

    const read = await app.call('/api/character', { cookie: phone.cookie });

    assert.equal(read.status, 200);
    assert.deepEqual(await read.json(), character);
});

test('what comes back is what went in, byte for byte', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Key order, spacing, and a number no round trip through a typed model would preserve. The
    // server stores the text; anything that reserialised it would rearrange all three.
    const raw = '{ "Version":1, "Mode":1, "Sheet":{"z":1,"a":2,"n":1.0000000000000002} }';

    assert.equal((await app.call('/api/character', { method: 'PUT', raw, cookie })).status, 204);
    assert.equal(await (await app.call('/api/character', { cookie })).text(), raw);
});

test('one account cannot see another account’s character', async () => {
    const app = server();
    const mine = await signIn(app, 'me@example.test');
    const yours = await signIn(app, 'you@example.test');

    await app.call('/api/character', { method: 'PUT', body: character, cookie: mine.cookie });

    // Nothing of mine is reachable with your cookie…
    assert.equal((await app.call('/api/character', { cookie: yours.cookie })).status, 404);

    // …and saving yours does not land on top of mine.
    await app.call('/api/character',
        { method: 'PUT', body: { ...character, Sheet: { Name: 'Someone else' } }, cookie: yours.cookie });

    const still = await (await app.call('/api/character', { cookie: mine.cookie })).json();
    assert.equal(still.Sheet.Name, 'Ninefold');
});

test('nobody signed in reaches a character at all', async () => {
    const app = server();
    await signIn(app, 'a@b.test');

    for (const method of ['GET', 'PUT', 'DELETE']) {
        const response = await app.call('/api/character',
            { method, body: method === 'PUT' ? character : undefined });

        assert.equal(response.status, 401, method);
    }
});

test('a saved character can be thrown away', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await app.call('/api/character', { method: 'PUT', body: character, cookie });
    assert.equal((await app.call('/api/character', { cookie })).status, 200);

    assert.equal((await app.call('/api/character', { method: 'DELETE', cookie })).status, 204);
    assert.equal((await app.call('/api/character', { cookie })).status, 404);

    // Twice is not an error: the end state is what was asked for.
    assert.equal((await app.call('/api/character', { method: 'DELETE', cookie })).status, 204);
});

test('saving again replaces rather than accumulating', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await app.call('/api/character', { method: 'PUT', body: character, cookie });
    await app.call('/api/character',
        { method: 'PUT', body: { ...character, Sheet: { Name: 'Renamed' } }, cookie });

    assert.equal((await (await app.call('/api/character', { cookie })).json()).Sheet.Name, 'Renamed');
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM characters').all()[0].n, 1);
});

test('a body that is not JSON, or is enormous, is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const send = raw => app.call('/api/character', { method: 'PUT', raw, cookie });

    assert.equal((await send('not json at all')).status, 400);
    assert.equal((await send('"a string is not a character"')).status, 400);
    assert.equal((await send('{"a":"' + 'x'.repeat(300 * 1024) + '"}')).status, 400);

    // The positive control: the same path accepts a real one.
    assert.equal((await send(JSON.stringify(character))).status, 204);
});

test('the size cap is bytes, not characters', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // **The cap counted UTF-16 code units and its comment said bytes**, found by a reviewer. An
    // astral-plane character is two units and four bytes, so a body padded with them reached
    // about twice the stated limit. The existing test padded with ASCII, where the two measures
    // agree — which is exactly why it passed.
    const emoji = '\u{1F600}';                         // four bytes, two code units
    const padding = emoji.repeat(40 * 1024);           // 160 KB of bytes, 80K units

    const under = '{"a":"' + padding + '"}';
    const over = '{"a":"' + emoji.repeat(80 * 1024) + '"}';   // 320 KB of bytes

    assert.ok(new TextEncoder().encode(over).byteLength > 256 * 1024);
    assert.ok(over.length < 256 * 1024, 'the padding is not astral, so this proves nothing');

    assert.equal((await app.call('/api/character', { method: 'PUT', raw: under, cookie })).status, 204);
    assert.equal((await app.call('/api/character', { method: 'PUT', raw: over, cookie })).status, 400);
});
