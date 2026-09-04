// The players in a GM's own campaigns, what they hold, and the cap they are held to.
//
// **Four properties this file exists for, and the first two are the whole of the security of the
// screen:**
//
//   1. **The same gate as the invitation list, not a second one.** 401 signed out; the byte-
//      identical 404 an unrouted address gets for an ordinary account, so the endpoint cannot be
//      found by trying.
//   2. **Scope is campaign membership, and the refusal says nothing.** A GM cannot see or cap a
//      player who is in somebody else's campaign, and the answer they get is byte-identical to the
//      one an address nobody has ever used gives.
//   3. **A cap is not self-service.** The caller never appears in their own list and cannot set
//      their own cap through this screen, even if they have joined their own campaign.
//   4. **Lowering a cap under what somebody holds destroys nothing.** That is `putCharacter`'s
//      existing behaviour and this screen is a number over it — the test below drives the cap
//      through the endpoint and then asserts the old rule still holds through the character
//      routes, which is the join between the two halves nothing else checks.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { cookieFrom, server, tokenFrom } from './harness.mjs';

const ADMIN = 'boss@example.test';
const DEPUTY = 'deputy@example.test';
const PLAYER = 'player@example.test';
const STRANGER = 'stranger@example.test';

const gid = (n = 0) => 'g_000000000000000000000' + n;
const cid = (n = 0) => 'c_000000000000000000000' + n;

/** Shaped like what `StoredCampaign` writes. Opaque to the server. */
const campaignPayload = JSON.stringify({
    Version: 1,
    Campaign: { Id: gid(), Name: 'Nightfall', TierId: 'standard' },
});

/** Shaped like what `StoredCharacter` writes. Opaque to the server. */
const characterPayload = JSON.stringify({
    Version: 1, Mode: 0, Sheet: { Name: 'Ninefold', AbilityRanks: { might: 8 } },
});

/** A server with the gate on and one bootstrap administrator, as a deployment has. */
const gated = () => server({ gated: true, admin: ADMIN });

/** Sign in as somebody the gate really allows, and hand back their cookie. */
async function enter(app, email) {
    const asked = await app.call('/api/auth/request', { method: 'POST', body: { email } });
    assert.equal(asked.status, 204, 'could not ask for a link');

    const verified = await app.call('/api/auth/verify',
        { method: 'POST', body: { token: tokenFrom(app.sent) } });
    assert.equal(verified.status, 200, 'could not spend the link');

    return cookieFrom(verified);
}

const putCampaign = (app, cookie, theId = gid()) =>
    app.call(`/api/campaigns/${theId}`,
        { method: 'PUT', body: { label: 'Nightfall', payload: campaignPayload }, cookie });

async function joinCodeFor(app, cookie, theId = gid()) {
    const listed = await (await app.call('/api/campaigns', { cookie })).json();
    const row = listed.campaigns.find(c => c.id === theId);

    assert.ok(row?.joinCode, 'the campaign has no join code, so nobody could ever join it');

    return row.joinCode;
}

const join = (app, cookie, code, characterId = cid()) =>
    app.call('/api/memberships/join',
        { method: 'POST', body: { code, characterId, label: 'Ninefold' }, cookie });

const putCharacter = (app, cookie, theId, extra = {}) =>
    app.call(`/api/characters/${theId}`, {
        method: 'PUT',
        body: { label: 'Ninefold', payload: characterPayload, ...extra },
        cookie,
    });

const accounts = (app, cookie) => app.call('/api/admin/accounts', { cookie });

const setLimit = (app, cookie, email, characterLimit) =>
    app.call(`/api/admin/accounts/${encodeURIComponent(email)}/character-limit`,
        { method: 'PUT', body: { characterLimit }, cookie });

const heldBy = (app, cookie, email) =>
    app.call(`/api/admin/accounts/${encodeURIComponent(email)}/characters`, { cookie });

/**
 * Two GMs, each with a campaign and a player in it, and the first GM is the administrator.
 *
 * Everything goes through the real endpoints rather than inserting rows, so a test about scoping
 * cannot pass against a join that has stopped working.
 */
async function twoTables() {
    const app = gated();

    app.invite(DEPUTY, { grantsAdmin: true });
    app.invite(PLAYER);
    app.invite(STRANGER);

    const boss = await enter(app, ADMIN);
    assert.equal((await putCampaign(app, boss, gid(0))).status, 204);
    const bossCode = await joinCodeFor(app, boss, gid(0));

    const deputy = await enter(app, DEPUTY);
    assert.equal((await putCampaign(app, deputy, gid(1))).status, 204);
    const deputyCode = await joinCodeFor(app, deputy, gid(1));

    const player = await enter(app, PLAYER);
    assert.equal((await join(app, player, bossCode, cid(0))).status, 200);

    const stranger = await enter(app, STRANGER);
    assert.equal((await join(app, stranger, deputyCode, cid(1))).status, 200);

    return { app, boss, deputy, player, stranger, bossCode };
}

// ── The gate ────────────────────────────────────────────────────────────────────────────

test('the accounts screen is invisible to an ordinary account, exactly as the invitation list is',
    async () => {
        const { app, player } = await twoTables();

        const nowhere = await app.call('/api/no-such-thing', { cookie: player });
        const nowhereBody = await nowhere.json();

        for (const [name, response] of [
            ['the list', await accounts(app, player)],
            ['the cap', await setLimit(app, player, PLAYER, 9)],
            ['the characters', await heldBy(app, player, PLAYER)],
        ]) {
            assert.equal(response.status, 404, name);
            assert.deepEqual(await response.json(), nowhereBody, name);
        }
    });

test('nobody signed in reaches any of the three addresses', async () => {
    const { app } = await twoTables();

    assert.equal((await accounts(app, null)).status, 401);
    assert.equal((await setLimit(app, null, PLAYER, 9)).status, 401);
    assert.equal((await heldBy(app, null, PLAYER)).status, 401);
});

test('the list answers GET and nothing else, and an unrouted sub-path is a 404', async () => {
    const { app, boss } = await twoTables();

    assert.equal((await app.call('/api/admin/accounts',
        { method: 'DELETE', cookie: boss })).status, 405);

    // The cap is a PUT and the characters are a GET; the other way round is not a route.
    assert.equal((await app.call(
        `/api/admin/accounts/${encodeURIComponent(PLAYER)}/character-limit`,
        { cookie: boss })).status, 405);
    assert.equal((await app.call(
        `/api/admin/accounts/${encodeURIComponent(PLAYER)}/characters`,
        { method: 'PUT', body: { characterLimit: 9 }, cookie: boss })).status, 405);

    // Everything else past the key is unrouted, including the bare key.
    assert.equal((await app.call(
        `/api/admin/accounts/${encodeURIComponent(PLAYER)}`, { cookie: boss })).status, 404);
    assert.equal((await app.call(
        `/api/admin/accounts/${encodeURIComponent(PLAYER)}/payload`, { cookie: boss })).status, 404);
});

// ── The list ────────────────────────────────────────────────────────────────────────────

test('the list is the players in the caller’s own campaigns, with what each of them holds',
    async () => {
        const { app, boss, player } = await twoTables();

        for (const n of [0, 1, 2]) {
            assert.equal((await putCharacter(app, player, cid(n))).status, 204, `character ${n}`);
        }

        const response = await accounts(app, boss);
        assert.equal(response.status, 200);

        const body = await response.json();

        assert.deepEqual(body.accounts.map(a => a.email), [PLAYER],
            'the list is not exactly this GM’s own players');

        assert.equal(body.accounts[0].characterCount, 3);
        assert.equal(body.accounts[0].characterLimit, 5);
        assert.equal(body.accounts[0].displayName, 'player');

        // The positive control on the count: it is a count of that account's rows, not of
        // everybody's. The stranger's character must not be in it.
        const stranger = await enter(app, STRANGER);
        assert.equal((await putCharacter(app, stranger, cid(5))).status, 204);

        const again = await (await accounts(app, boss)).json();
        assert.equal(again.accounts[0].characterCount, 3, 'the count is not scoped to the account');
    });

test('a GM sees nothing of a player who is only in somebody else’s campaign', async () => {
    const { app, boss, stranger } = await twoTables();

    assert.equal((await putCharacter(app, stranger, cid(1))).status, 204);

    // **Byte-identical to an address nobody has ever used.** The refusal must not be a way of
    // asking whether somebody has an account here, or who is playing in whose game.
    const outOfScope = await heldBy(app, boss, STRANGER);
    const neverExisted = await heldBy(app, boss, 'nobody@example.test');

    assert.equal(outOfScope.status, 404);
    assert.equal(neverExisted.status, 404);
    assert.deepEqual(await outOfScope.json(), await neverExisted.json());

    const cappedOutOfScope = await setLimit(app, boss, STRANGER, 25);
    const cappedNeverExisted = await setLimit(app, boss, 'nobody@example.test', 25);

    assert.equal(cappedOutOfScope.status, 404);
    assert.deepEqual(await cappedOutOfScope.json(), await cappedNeverExisted.json());

    // And nothing happened to the account it named — the strong half of the assertion above,
    // since a 404 over a write that landed would be the worst of both.
    const deputy = await enter(app, DEPUTY);
    const theirs = await (await accounts(app, deputy)).json();

    assert.deepEqual(theirs.accounts.map(a => a.email), [STRANGER]);
    assert.equal(theirs.accounts[0].characterLimit, 5, 'the out-of-scope write landed anyway');
});

test('the caller is never in their own list and cannot cap themselves', async () => {
    // **A cap somebody can raise on themselves is not a cap** — `docs/CHARACTERS-API.md` says so
    // and this screen must not quietly reverse it. A GM *can* redeem their own join code, so
    // without the exclusion they would be a player in their own campaign and their own row would
    // be sitting on the screen with an editable number in it.
    const { app, boss, bossCode } = await twoTables();

    assert.equal((await join(app, boss, bossCode, cid(7))).status, 200,
        'a GM joining their own campaign is the state this test is about');

    const body = await (await accounts(app, boss)).json();
    assert.deepEqual(body.accounts.map(a => a.email), [PLAYER]);

    const refused = await setLimit(app, boss, ADMIN, 500);
    assert.equal(refused.status, 404);

    // Nothing moved, which is what the 404 has to mean here.
    const own = await (await app.call('/api/characters', { cookie: boss })).json();
    assert.equal(own.limit, 5, 'the caller raised their own cap through the admin screen');
});

test('a GM with no players is told so with an empty list, not a refusal', async () => {
    // The positive control for the gate tests above: an administrator reading nothing must not be
    // mistaken for the gate refusing them, or the two would be measuring the same thing.
    const app = gated();
    const boss = await enter(app, ADMIN);

    const response = await accounts(app, boss);

    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { accounts: [] });
});

// ── The cap ─────────────────────────────────────────────────────────────────────────────

test('setting a cap answers the row as it now stands', async () => {
    const { app, boss, player } = await twoTables();

    assert.equal((await putCharacter(app, player, cid(0))).status, 204);

    const response = await setLimit(app, boss, PLAYER, 25);
    assert.equal(response.status, 200);

    assert.deepEqual(await response.json(), {
        account: {
            email: PLAYER, displayName: 'player', characterCount: 1, characterLimit: 25,
        },
    });

    // And the account itself is told the new number by the endpoint it already asks.
    const theirs = await (await app.call('/api/characters', { cookie: player })).json();
    assert.equal(theirs.limit, 25);
});

test('a cap is a whole number in a range, and everything else is refused', async () => {
    const { app, boss, player } = await twoTables();

    for (const characterLimit of [-1, 501, 1.5, '5', null, true]) {
        const response = await setLimit(app, boss, PLAYER, characterLimit);

        assert.equal(response.status, 400, `${JSON.stringify(characterLimit)} was accepted`);
    }

    // Missing altogether is the same refusal.
    assert.equal((await app.call(
        `/api/admin/accounts/${encodeURIComponent(PLAYER)}/character-limit`,
        { method: 'PUT', body: {}, cookie: boss })).status, 400);

    // The positive control on the bounds: both ends of the range are accepted, so the refusals
    // above are about the values rather than about the endpoint refusing everything.
    for (const characterLimit of [0, 500]) {
        assert.equal((await setLimit(app, boss, PLAYER, characterLimit)).status, 200,
            `${characterLimit} was refused`);
    }

    const theirs = await (await app.call('/api/characters', { cookie: player })).json();
    assert.equal(theirs.limit, 500, 'the last accepted cap is not the one stored');
});

test('a cap set to zero refuses the next character and destroys none of the ones held',
    async () => {
        // **This is the join between the screen and the mechanism, and it is the reason the item
        // this screen came from says "a UI over behaviour that is already correct".** The cap is
        // driven through the admin endpoint here and the outcome is read through the character
        // routes — `characters.test.mjs` already owns the cap's own behaviour, and this asserts
        // that a number arriving from the new screen reaches the same statement.
        const { app, boss, player } = await twoTables();

        for (const n of [0, 1, 2]) {
            assert.equal((await putCharacter(app, player, cid(n))).status, 204);
        }

        assert.equal((await setLimit(app, boss, PLAYER, 1)).status, 200);

        // Every one of the three is still there, still readable, and still saveable under its
        // own id — `putCharacter`'s first `WHERE` clause lets an id the account already owns
        // through however full it is.
        const listed = await (await app.call('/api/characters', { cookie: player })).json();
        assert.equal(listed.characters.length, 3, 'lowering the cap destroyed rows');
        assert.equal(listed.limit, 1);

        assert.equal((await putCharacter(app, player, cid(1), { label: 'Renamed' })).status, 204,
            'an id the account already holds must save at any cap');

        // A *new* id is refused, and the 409 names the number this screen just set.
        const refused = await putCharacter(app, player, cid(8));
        assert.equal(refused.status, 409);
        assert.deepEqual(await refused.json(),
            { error: 'This account already holds 1 characters.', limit: 1 });

        // Raising it again is all it takes to let the next one through, which is the whole point
        // of the screen: the number is the entire mechanism.
        assert.equal((await setLimit(app, boss, PLAYER, 25)).status, 200);
        assert.equal((await putCharacter(app, player, cid(8))).status, 204);
    });

// ── What a player holds ─────────────────────────────────────────────────────────────────

test('a player’s sheets are listed by name, time and index column, and never by payload',
    async () => {
        const { app, boss, player } = await twoTables();

        app.now += 1000;
        assert.equal((await putCharacter(app, player, cid(0), {
            label: 'Ninefold', kind: 'villain', tierId: 'high', spent: 118,
        })).status, 204);

        app.now += 1000;
        assert.equal((await putCharacter(app, player, cid(1), { label: 'Second' })).status, 204);

        const response = await heldBy(app, boss, PLAYER);
        assert.equal(response.status, 200);

        const body = await response.json();
        const text = JSON.stringify(body);

        assert.equal(body.email, PLAYER);
        assert.deepEqual(body.characters.map(c => c.label), ['Second', 'Ninefold'],
            'not ordered most recently touched first');

        assert.deepEqual(body.characters[1], {
            id: cid(0), label: 'Ninefold', updatedAt: app.now - 1000,
            kind: 'villain', tierId: 'high', spent: 118,
        });

        // A character written with none of the three index columns answers null for each, which
        // is the ordinary state for any row saved before `0008`.
        assert.deepEqual(
            { kind: body.characters[0].kind, tierId: body.characters[0].tierId,
                spent: body.characters[0].spent },
            { kind: null, tierId: null, spent: null });

        // **Not one byte of a payload.** The server has never parsed a character and this screen
        // does not start; a GM reading a member's sheet reads the campaign's clone, which is a
        // sheet that member deliberately sent.
        assert.ok(!text.includes('AbilityRanks'), text);
        assert.ok(!text.includes('payload'), text);
    });

test('a player holding nothing is an empty list, not a 404', async () => {
    // The positive control for the scoping test: an account in scope with no characters and an
    // account out of scope must not be the same answer, or the 404 above says nothing.
    const { app, boss } = await twoTables();

    const response = await heldBy(app, boss, PLAYER);

    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { email: PLAYER, characters: [] });
});

test('an address is the key however it was typed, and a key that is no address is a 404',
    async () => {
        const { app, boss } = await twoTables();

        // The same normalisation the gate uses, so a capital letter names the same account here
        // as it does when somebody asks for a link.
        assert.equal((await heldBy(app, boss, 'Player@Example.Test')).status, 200);

        // Not an address at all: the 404 an unrouted address gets, rather than a status that
        // would say "that one is well-formed and simply not here".
        const nowhere = await (await app.call('/api/no-such-thing', { cookie: boss })).json();

        for (const key of ['not-an-address', '%E0%A4%A', 'a b@example.test']) {
            const response = await app.call(`/api/admin/accounts/${key}/characters`,
                { cookie: boss });

            assert.equal(response.status, 404, key);
            assert.deepEqual(await response.json(), nowhere, key);
        }
    });
