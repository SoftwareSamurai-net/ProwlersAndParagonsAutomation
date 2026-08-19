// Many characters, belonging to one account, each addressed by an id the client picks.
//
// This is the contract in docs/CHARACTERS-API.md. The four addresses, the cap, the id shape and
// the label default are all here because they are all this server is allowed to decide about a
// character — everything else about one is the engine's business, on the other side of the wire.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { server, signIn } from './harness.mjs';

/** Shaped like what `CharacterStore` writes: a version, a mode, and the inputs. */
const character = {
    Version: 1,
    Mode: 0,
    Sheet: { Name: 'Ninefold', AbilityRanks: { might: 8, agility: 6 }, SelectedPowers: [] },
};

/** A well-formed id — `c_` plus 22 URL-safe characters — distinguished only by its last digit,
 * so a test can mint several without colliding. */
const id = (n = 0) => 'c_000000000000000000000' + n;

const put = (app, cookie, { theId = id(), label, payload = character } = {}) =>
    app.call(`/api/characters/${theId}`,
        { method: 'PUT', body: { label, payload: JSON.stringify(payload) }, cookie });

test('a character follows its account to another browser', async () => {
    const app = server();
    const laptop = await signIn(app, 'player@example.test');

    assert.equal((await put(app, laptop.cookie, { label: 'Ninefold' })).status, 204);

    // A second sign-in from somewhere else: a new link, a new session, the same account.
    const phone = await signIn(app, 'player@example.test');
    assert.notEqual(phone.cookie, laptop.cookie, 'the same session was reused, so this proves nothing');

    const read = await app.call(`/api/characters/${id()}`, { cookie: phone.cookie });

    assert.equal(read.status, 200);
    assert.deepEqual(await read.json(), character);
});

test('what comes back is what went in, byte for byte', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Key order, spacing, and a number no round trip through a typed model would preserve. The
    // server stores the text; anything that reserialised it would rearrange all three.
    const raw = '{ "Version":1, "Mode":1, "Sheet":{"z":1,"a":2,"n":1.0000000000000002} }';

    assert.equal((await app.call(`/api/characters/${id()}`,
        { method: 'PUT', body: { label: 'x', payload: raw }, cookie })).status, 204);
    assert.equal(await (await app.call(`/api/characters/${id()}`, { cookie })).text(), raw);
});

test('one account cannot read, overwrite or delete another account’s character by id', async () => {
    const app = server();
    const mine = await signIn(app, 'me@example.test');
    const yours = await signIn(app, 'you@example.test');

    await put(app, mine.cookie, { label: 'Mine' });

    // Nothing of mine is reachable with your cookie…
    assert.equal((await app.call(`/api/characters/${id()}`, { cookie: yours.cookie })).status, 404);

    // …and deleting the same id from your account succeeds, because from your side nothing is
    // there — **but it must not land on my row.** The status is the weak half of this assertion;
    // the strong half is the read after it.
    assert.equal((await app.call(`/api/characters/${id()}`,
        { method: 'DELETE', cookie: yours.cookie })).status, 204);
    assert.equal((await app.call(`/api/characters/${id()}`, { cookie: mine.cookie })).status, 200,
        'mine must have survived a delete attempt from another account');

    // …and writing under the same id from your account creates *your own* row rather than
    // overwriting mine, because the primary key is (user_id, id).
    assert.equal((await put(app, yours.cookie,
        { label: 'Yours', payload: { ...character, Sheet: { Name: 'Someone else' } } })).status, 204);

    const stillMine = await (await app.call(`/api/characters/${id()}`, { cookie: mine.cookie })).json();
    assert.equal(stillMine.Sheet.Name, 'Ninefold');
});

test('nobody signed in reaches a character, or the list, at all', async () => {
    const app = server();
    await signIn(app, 'a@b.test');

    for (const path of ['/api/characters', `/api/characters/${id()}`]) {
        for (const method of ['GET', 'PUT', 'DELETE']) {
            const response = await app.call(path,
                { method, body: method === 'PUT' ? { label: 'x', payload: JSON.stringify(character) } : undefined });

            assert.equal(response.status, 401, `${method} ${path}`);
        }
    }
});

test('a saved character can be thrown away, and twice is not an error', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await put(app, cookie, { label: 'x' });
    assert.equal((await app.call(`/api/characters/${id()}`, { cookie })).status, 200);

    assert.equal((await app.call(`/api/characters/${id()}`, { method: 'DELETE', cookie })).status, 204);
    assert.equal((await app.call(`/api/characters/${id()}`, { cookie })).status, 404);
});

test('deleting is idempotent — twice, and never, are both successes', async () => {
    // **The contract said 404 here and it was wrong.** The end state asked for is "that character
    // is not there", and it is not there. Reporting failure for that costs a manager with two tabs
    // open: delete in one, delete in the other, and the second sees an error for a character that
    // is already gone, retries, and sees it again while the app looks broken.
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Never stored by anybody.
    assert.equal((await app.call(`/api/characters/${id(9)}`, { method: 'DELETE', cookie })).status, 204);

    // Stored, then deleted twice.
    await put(app, cookie, { label: 'Ninefold' });
    assert.equal((await app.call(`/api/characters/${id()}`, { method: 'DELETE', cookie })).status, 204);
    assert.equal((await app.call(`/api/characters/${id()}`, { method: 'DELETE', cookie })).status, 204);

    // The positive control: it really is gone, so the 204s above are not a delete that never ran.
    assert.equal((await app.call(`/api/characters/${id()}`, { cookie })).status, 404);
});

test('an ill-formed id is still refused, even to delete', async () => {
    // 400 rather than 204: a malformed request is not an absent character, and answering "fine"
    // to a request this server could not have acted on would hide a client bug for ever.
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (const bad of ['nope', 'c_short', 'c_' + 'x'.repeat(23), '../../etc', 'c_with spaces here 1234']) {
        const response = await app.call(`/api/characters/${encodeURIComponent(bad)}`,
            { method: 'DELETE', cookie });

        assert.equal(response.status, 400, bad);
    }
});

test('saving again under the same id replaces rather than accumulating', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await put(app, cookie, { label: 'x' });
    await put(app, cookie, { label: 'x', payload: { ...character, Sheet: { Name: 'Renamed' } } });

    assert.equal((await (await app.call(`/api/characters/${id()}`, { cookie })).json()).Sheet.Name, 'Renamed');
    assert.equal(app.db.raw.prepare('SELECT COUNT(*) AS n FROM characters').all()[0].n, 1);
});

test('a payload that is not JSON, or is enormous, is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const send = payload => app.call(`/api/characters/${id()}`,
        { method: 'PUT', body: { label: 'x', payload }, cookie });

    assert.equal((await send('not json at all')).status, 400);
    assert.equal((await send(undefined)).status, 400);
    assert.equal((await send(42)).status, 400);

    // The positive control: the same path accepts a real one, including a bare-value payload —
    // the server only checks that it parses, never that it is an object.
    assert.equal((await send(JSON.stringify(character))).status, 204);
});

test('the size cap is bytes, whatever the characters are', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // **The cap counted UTF-16 code units and its comment said bytes**, found by a reviewer, in
    // the single-character predecessor of this endpoint — carried forward here because the cap
    // is still `readJson`'s and still on the whole request body.
    const paddings = [
        { name: 'astral (4 bytes, 2 units)', char: '\u{1F600}' },
        { name: 'CJK (3 bytes, 1 unit)', char: '中' },
    ];

    for (const { name, char } of paddings) {
        const bytes = new TextEncoder().encode(char).byteLength;
        const over = '{"a":"' + char.repeat(Math.ceil((300 * 1024) / bytes)) + '"}';

        assert.ok(new TextEncoder().encode(over).byteLength > 256 * 1024, name);

        const raw = JSON.stringify({ label: 'x', payload: over });
        assert.equal((await app.call(`/api/characters/${id()}`,
            { method: 'PUT', raw, cookie })).status, 400, name + ' got past the cap');
    }

    // The positive control: a body genuinely under the cap is still accepted.
    const under = '{"a":"' + '中'.repeat(1024) + '"}';
    assert.equal((await put(app, cookie, { label: 'x', payload: under })).status, 204);
});

test('a label is trimmed, and empty or missing becomes "Unnamed character"', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    await put(app, cookie, { theId: id(1), label: '  Ninefold  ' });
    await put(app, cookie, { theId: id(2), label: '' });
    await put(app, cookie, { theId: id(3), label: undefined });
    await put(app, cookie, { theId: id(4), label: '   ' });

    const list = (await (await app.call('/api/characters', { cookie })).json()).characters;
    const labelOf = theId => list.find(c => c.id === theId).label;

    assert.equal(labelOf(id(1)), 'Ninefold', 'surrounding whitespace should have been trimmed');
    assert.equal(labelOf(id(2)), 'Unnamed character');
    assert.equal(labelOf(id(3)), 'Unnamed character');
    assert.equal(labelOf(id(4)), 'Unnamed character', 'whitespace-only is empty once trimmed');
});

test('a label over 80 characters is refused, and 80 exactly is not', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    assert.equal((await put(app, cookie, { theId: id(1), label: 'x'.repeat(81) })).status, 400);
    assert.equal((await put(app, cookie, { theId: id(2), label: 'x'.repeat(80) })).status, 204);

    // A label that is not a string at all is refused the same way, not coerced.
    assert.equal((await put(app, cookie, { theId: id(3), label: 42 })).status, 400);
});

test('an id in the wrong shape is refused on every route that takes one', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (const bad of ['not-an-id', 'c_tooshort', 'u_' + '0'.repeat(22), 'c_' + '!'.repeat(22)]) {
        assert.equal((await app.call(`/api/characters/${bad}`, { cookie })).status, 400, `GET ${bad}`);
        assert.equal((await put(app, cookie, { theId: bad })).status, 400, `PUT ${bad}`);
        assert.equal((await app.call(`/api/characters/${bad}`, { method: 'DELETE', cookie })).status, 400,
            `DELETE ${bad}`);
    }
});

test('the list is capped, ordered most-recently-touched first, and the cap is reported', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (let n = 0; n < 5; n++) {
        app.now += 1000;
        assert.equal((await put(app, cookie, { theId: id(n), label: `Character ${n}` })).status, 204);
    }

    const body = await (await app.call('/api/characters', { cookie })).json();
    assert.equal(body.limit, 5);
    assert.deepEqual(body.characters.map(c => c.label),
        ['Character 4', 'Character 3', 'Character 2', 'Character 1', 'Character 0']);

    // A sixth is refused — the account is at its cap and this id is a new one.
    app.now += 1000;
    const refused = await put(app, cookie, { theId: id(5), label: 'Character 5' });
    assert.equal(refused.status, 409);
    assert.deepEqual(await refused.json(), { error: 'This account already holds 5 characters.', limit: 5 });

    // The positive control for the id half of the cap check: replacing one already stored is
    // allowed at exactly the same fullness that just refused a sixth.
    app.now += 1000;
    assert.equal((await put(app, cookie, { theId: id(2), label: 'Renamed' })).status, 204);

    const after = await (await app.call('/api/characters', { cookie })).json();
    assert.equal(after.characters.length, 5, 'a replace must not grow the count past the cap');
    assert.equal(after.characters[0].label, 'Renamed', 'the replace should also be the most recent');
});

test('two accounts each hold their own five — one account being full does not block another', async () => {
    const app = server();
    const full = await signIn(app, 'full@example.test');
    const other = await signIn(app, 'other@example.test');

    for (let n = 0; n < 5; n++) {
        assert.equal((await put(app, full.cookie, { theId: id(n) })).status, 204);
    }
    assert.equal((await put(app, full.cookie, { theId: id(5) })).status, 409);

    // A fresh account, on the same server, is nowhere near its own cap.
    assert.equal((await put(app, other.cookie, { theId: id(0) })).status, 204);
});

test('a GM account raised past 5 can hold more, by the SQL in the setup doc', async () => {
    const app = server();
    const { cookie, identity } = await signIn(app, 'gm@example.test');

    app.db.raw.prepare('UPDATE users SET character_limit = 25 WHERE id = ?').run(identity.key);

    for (let n = 0; n < 6; n++) {
        assert.equal((await put(app, cookie, { theId: id(n) })).status, 204, `character ${n}`);
    }

    assert.equal((await (await app.call('/api/characters', { cookie })).json()).limit, 25);
});

// What 0002 does to a row that existed before it — a generated id, the default label, the
// payload and timestamp untouched — is tested directly against the migration in
// migration.test.mjs, against the *pre*-migration schema. This harness always applies both
// migrations together, so it cannot reproduce a database 0002 has not yet run against; asserting
// the migration's behaviour here would only re-describe what the fixture was built to already
// have, not what the `INSERT … SELECT` actually does to an old row.
