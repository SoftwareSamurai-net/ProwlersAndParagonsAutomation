// Approving a Villain hands it to the campaign's owner, for good.
//
// **The owner's rulings of 2026-10-01**, each of which is a test below: an approved Villain moves
// to the GM's account and the player keeps no sheet; there is no hand-back; it counts against the
// GM's character cap, and a full account refuses the approval whole; a Hero's approval is the
// clone it always was.
//
// **The decision most likely to go wrong is how the server learns a snapshot is a Villain**, and
// these tests pin the answer: the browser says so beside the snapshot, the server stores the word
// with it, and approval acts on the stored word. Never on the payload, which the server does not
// parse, and never on the player's own row, which describes their sheet now rather than the
// snapshot the GM read — two tests flip that row after sending and require nothing to change.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { server, signIn } from './harness.mjs';

const campaignPayload = JSON.stringify({
    Version: 1,
    Campaign: { Id: 'g_0000000000000000000000', Name: 'Nightfall', TierId: 'standard' },
});

const villainPayload = (name = 'The Hollow Regent') => JSON.stringify({
    Version: 1, Mode: 1, Sheet: { SelectedTierId: 'standard', Name: name, IsVillain: true },
});

const gid = (n = 0) => 'g_000000000000000000000' + n;
const cid = (n = 0) => 'c_000000000000000000000' + n;

const putCampaign = (app, cookie, theId = gid()) =>
    app.call(`/api/campaigns/${theId}`, {
        method: 'PUT', body: { label: 'Nightfall', payload: campaignPayload }, cookie,
    });

const putCharacter = (app, cookie, id, { label = 'NPC', payload = '{}', kind } = {}) =>
    app.call(`/api/characters/${id}`, { method: 'PUT', body: { label, payload, kind }, cookie });

/**
 * Which Hero each Villain membership a fixture made is keyed to, so a submission names it the way
 * the browser does — a Villain is refused without one (0012).
 */
const heroOf = new Map();

const submit = (app, cookie, id, {
    payload = villainPayload(), label = 'The Hollow Regent', kind = 'villain', nemesisOf = heroOf.get(id) ?? null,
} = {}) =>
    app.call(`/api/memberships/${id}/submission`, { method: 'PUT', body: { label, payload, kind, nemesisOf }, cookie });

/** Join a Hero for the same account, and key a Villain membership to it. */
async function aHeroFor(app, cookie, code, villainMembership, characterId = cid(8)) {
    const joined = await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId, label: 'Jetstream' }, cookie,
    });
    assert.equal(joined.status, 200);
    const hero = (await joined.json()).id;
    heroOf.set(villainMembership, hero);

    return hero;
}

const decide = (app, cookie, id, what, version) =>
    app.call(`/api/memberships/${id}/${what}`, { method: 'POST', body: { version }, cookie });

const characters = async (app, cookie) => (await (await app.call('/api/characters', { cookie })).json());

async function playerRow(app, cookie, id) {
    return (await (await app.call('/api/memberships', { cookie })).json()).memberships.find(m => m.id === id);
}

async function gmRow(app, cookie, id) {
    return (await (await app.call('/api/memberships/inbox', { cookie })).json()).memberships.find(m => m.id === id);
}

/**
 * A GM with a campaign, and a player whose Villain is saved on their account and has joined it.
 * Goes through the real endpoints throughout, so nothing below passes against a join or a save
 * that has stopped working.
 */
async function aVillainAtTheTable({ gmHolds = 0 } = {}) {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    for (let i = 0; i < gmHolds; i++) {
        assert.equal((await putCharacter(app, gm.cookie, cid(i + 1), { label: `NPC ${i}` })).status, 204);
    }

    const listed = await (await app.call('/api/campaigns', { cookie: gm.cookie })).json();
    const code = listed.campaigns[0].joinCode;

    const player = await signIn(app, 'player@example.test');
    assert.equal((await putCharacter(app, player.cookie, cid(),
        { label: 'The Hollow Regent', payload: villainPayload(), kind: 'villain' })).status, 204);

    const joined = await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId: cid(), label: 'The Hollow Regent' }, cookie: player.cookie,
    });
    assert.equal(joined.status, 200);

    const membership = (await joined.json()).id;
    const hero = await aHeroFor(app, player.cookie, code, membership);

    return { app, gm, player, membership, hero, code };
}

test('approving a Villain moves the approved snapshot to the GM and takes it from the player', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    const sent = villainPayload('The Hollow Regent, as sent');

    assert.equal((await submit(app, player.cookie, membership, { payload: sent })).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    // The GM's account holds it, under an id this server minted, as the snapshot that was sent —
    // byte for byte, because nothing here reads it.
    const gms = await characters(app, gm.cookie);
    assert.equal(gms.characters.length, 1, 'the GM gained exactly one character');
    const taken = gms.characters[0];
    assert.match(taken.id, /^c_[A-Za-z0-9_-]{22}$/);
    assert.notEqual(taken.id, cid(), 'a fresh id, never the player’s own');
    assert.equal(taken.label, 'The Hollow Regent');
    assert.equal(taken.kind, 'villain');
    assert.equal(taken.campaignId, gid());

    const opened = await app.call(`/api/characters/${taken.id}`, { cookie: gm.cookie });
    assert.equal(opened.status, 200);
    assert.equal(await opened.text(), sent, 'the approved snapshot, not the player’s saved copy');

    // The player keeps no sheet.
    assert.equal((await characters(app, player.cookie)).characters.length, 0);
    assert.equal((await app.call(`/api/characters/${cid()}`, { cookie: player.cookie })).status, 404);

    // Both sides read it as a handover, and the player is told which game took it.
    const mine = await playerRow(app, player.cookie, membership);
    assert.equal(mine.handedOver, true);
    assert.equal(mine.givenTo, 'Nightfall');
    assert.equal(mine.decision, 'approved');

    const theirs = await gmRow(app, gm.cookie, membership);
    assert.equal(theirs.handedOver, true);
    assert.equal(theirs.hasApproved, true, 'the campaign still holds its clone, as for any approval');
});

test('approving a Hero is the clone it always was: the player keeps it and the GM gains nothing', async () => {
    // The control for every test in this file: the same table, the same route, a different word.
    const { app, gm, player, membership } = await aVillainAtTheTable();

    assert.equal((await submit(app, player.cookie, membership, { kind: 'hero' })).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    assert.equal((await characters(app, gm.cookie)).characters.length, 0);
    assert.equal((await characters(app, player.cookie)).characters.length, 1);
    assert.equal((await playerRow(app, player.cookie, membership)).handedOver, false);
    assert.equal((await playerRow(app, player.cookie, membership)).givenTo, null,
        'a game’s name is answered only for a handed-over row');
    assert.equal((await gmRow(app, gm.cookie, membership)).handedOver, false);
});

test('a snapshot sent with no kind at all, as an older build sends it, approves as a Hero', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();

    assert.equal((await submit(app, player.cookie, membership, { kind: null })).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    assert.equal((await characters(app, player.cookie)).characters.length, 1);
    assert.equal((await characters(app, gm.cookie)).characters.length, 0);
});

test('a full GM account refuses the approval whole, says so, and leaves everything where it was', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable({ gmHolds: 5 });
    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    const refused = await decide(app, gm.cookie, membership, 'approve', 1);
    assert.equal(refused.status, 409);
    const said = await refused.json();
    assert.match(said.error, /already holds 5 characters/);
    assert.match(said.error, /still waiting/);
    assert.equal(said.limit, 5);
    assert.equal(said.pendingVersion, 1, 'the refusal is about the snapshot the GM saw');

    // Nothing moved: not the GM's count, not the player's character, not the membership.
    assert.equal((await characters(app, gm.cookie)).characters.length, 5);
    assert.equal((await characters(app, player.cookie)).characters.length, 1);
    const row = await gmRow(app, gm.cookie, membership);
    assert.equal(row.hasPending, true, 'the snapshot is still waiting');
    assert.equal(row.hasApproved, false);
    assert.equal(row.decision, null, 'a refused approval records no decision');
    assert.equal(row.handedOver, false);

    // **The positive control: it was the cap and nothing else.** One NPC goes, and the identical
    // request — same version — now lands.
    assert.equal((await app.call(`/api/characters/${cid(1)}`, { method: 'DELETE', cookie: gm.cookie })).status, 204);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);
    assert.equal((await characters(app, gm.cookie)).characters.length, 5);
    assert.equal((await characters(app, player.cookie)).characters.length, 0);
});

test('a stale Villain snapshot is refused as stale, not as a full account', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable({ gmHolds: 5 });
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    const refused = await decide(app, gm.cookie, membership, 'approve', 1);
    assert.equal(refused.status, 409);
    assert.equal((await refused.json()).error, 'This changed while you were looking at it.');
});

test('what approval does follows the snapshot, not the player’s sheet afterwards', async () => {
    // A Villain sent, then the player's own row flipped to a Hero: approval still hands it over.
    const villain = await aVillainAtTheTable();
    assert.equal((await submit(villain.app, villain.player.cookie, villain.membership)).status, 200);
    assert.equal((await putCharacter(villain.app, villain.player.cookie, cid(),
        { label: 'The Hollow Regent', kind: 'hero' })).status, 204);
    assert.equal((await decide(villain.app, villain.gm.cookie, villain.membership, 'approve', 1)).status, 204);
    assert.equal((await characters(villain.app, villain.gm.cookie)).characters.length, 1);
    assert.equal((await characters(villain.app, villain.player.cookie)).characters.length, 0);

    // A Hero sent, then the row flipped to a Villain: approval is a clone and takes nothing.
    const hero = await aVillainAtTheTable();
    assert.equal((await submit(hero.app, hero.player.cookie, hero.membership, { kind: 'hero' })).status, 200);
    assert.equal((await putCharacter(hero.app, hero.player.cookie, cid(),
        { label: 'The Hollow Regent', kind: 'villain' })).status, 204);
    assert.equal((await decide(hero.app, hero.gm.cookie, hero.membership, 'approve', 1)).status, 204);
    assert.equal((await characters(hero.app, hero.gm.cookie)).characters.length, 0);
    assert.equal((await characters(hero.app, hero.player.cookie)).characters.length, 1);
});

test('the GM is told a Villain is waiting, from the word approval will act on', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();

    assert.equal((await gmRow(app, gm.cookie, membership)).pendingKind, null, 'nothing waiting yet');

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await gmRow(app, gm.cookie, membership)).pendingKind, 'villain');

    assert.equal((await decide(app, gm.cookie, membership, 'reject', 1)).status, 204);
    assert.equal((await gmRow(app, gm.cookie, membership)).pendingKind, null,
        'a word about a snapshot that is no longer waiting describes nothing');
    assert.equal((await characters(app, player.cookie)).characters.length, 1,
        'a rejection leaves the Villain exactly where it was');
});

test('a tab still holding the Villain cannot save it back onto the player’s account', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    const autosave = await putCharacter(app, player.cookie, cid(),
        { label: 'The Hollow Regent', payload: villainPayload(), kind: 'villain' });
    assert.equal(autosave.status, 410);
    assert.match((await autosave.json()).error, /given to a campaign as a nemesis/);
    assert.equal((await characters(app, player.cookie)).characters.length, 0, 'nothing was written');

    // The control: the player's account is not refusing everything — another id saves, and the
    // handed-over Villain no longer counts against their cap.
    assert.equal((await putCharacter(app, player.cookie, cid(7), { label: 'Kestrel' })).status, 204);
});

test('nothing more can be sent into a handed-over membership, and nothing more approved', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    const again = await submit(app, player.cookie, membership);
    assert.equal(again.status, 409);
    assert.match((await again.json()).error, /belongs to the campaign now/);

    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 409);
    assert.equal((await characters(app, gm.cookie)).characters.length, 1, 'a second click took nothing more');
});

test('a Villain the player never saved to their account still moves: what moves is the snapshot', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await app.call(`/api/characters/${cid()}`, { method: 'DELETE', cookie: player.cookie })).status, 204);

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);
    assert.equal((await characters(app, gm.cookie)).characters.length, 1);
});

test('a GM approving their own Villain into their own game is not counted twice against the cap', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    // Five characters, the cap, one of them the Villain about to be handed over.
    for (let i = 1; i <= 4; i++) assert.equal((await putCharacter(app, gm.cookie, cid(i))).status, 204);
    assert.equal((await putCharacter(app, gm.cookie, cid(), { label: 'The Hollow Regent', kind: 'villain' })).status, 204);

    const code = (await (await app.call('/api/campaigns', { cookie: gm.cookie })).json()).campaigns[0].joinCode;
    const joined = await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId: cid(), label: 'The Hollow Regent' }, cookie: gm.cookie,
    });
    const membership = (await joined.json()).id;
    await aHeroFor(app, gm.cookie, code, membership);

    assert.equal((await submit(app, gm.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    const held = (await characters(app, gm.cookie)).characters;
    assert.equal(held.length, 5, 'the copy arrived and the original went');
    assert.ok(!held.some(c => c.id === cid()), 'the original id is gone');
});

test('a GM who is also the player is told the game is gone, not that their account is full', async () => {
    // The player's half of `getMembership` matches without the campaign, so this account reaches
    // the refusal path for a game it deleted. Both decisions must say what is true.
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);
    for (let i = 1; i <= 5; i++) assert.equal((await putCharacter(app, gm.cookie, cid(i))).status, 204);

    const code = (await (await app.call('/api/campaigns', { cookie: gm.cookie })).json()).campaigns[0].joinCode;
    const membership = (await (await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId: cid(), label: 'The Hollow Regent' }, cookie: gm.cookie,
    })).json()).id;
    await aHeroFor(app, gm.cookie, code, membership);
    assert.equal((await submit(app, gm.cookie, membership)).status, 200);
    assert.equal((await app.call(`/api/campaigns/${gid()}`, { method: 'DELETE', cookie: gm.cookie })).status, 204);

    for (const what of ['approve', 'reject']) {
        const refused = await decide(app, gm.cookie, membership, what, 1);
        assert.equal(refused.status, 409, what);
        assert.equal((await refused.json()).error, 'That campaign is no longer here.', what);
    }
});

test('a rejection of a Villain is never answered as a full account', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable({ gmHolds: 5 });
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    // Version 1 is stale, so the rejection is refused — as stale, never as the GM's cap.
    const refused = await decide(app, gm.cookie, membership, 'reject', 1);
    assert.equal(refused.status, 409);
    assert.equal((await refused.json()).error, 'This changed while you were looking at it.');
});

test('leaving a handed-over membership leaves the Villain with the GM', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    assert.equal((await app.call(`/api/memberships/${membership}`, { method: 'DELETE', cookie: player.cookie })).status, 204);
    assert.equal((await characters(app, gm.cookie)).characters.length, 1, 'there is no hand-back');
    assert.equal((await characters(app, player.cookie)).characters.length, 0);
});

test('a deleted campaign leaves the handover on the player’s row and names no game', async () => {
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    assert.equal((await app.call(`/api/campaigns/${gid()}`, { method: 'DELETE', cookie: gm.cookie })).status, 204);

    const mine = await playerRow(app, player.cookie, membership);
    assert.equal(mine.handedOver, true);
    assert.equal(mine.givenTo, null);
});

test('the three writes are one transaction: a failure in the last undoes the first two', async () => {
    // Drives `batch`'s all-or-nothing directly: a trigger makes the player's delete throw, so the
    // GM's copy and the membership's mark must both be rolled back with it.
    const { app, gm, player, membership } = await aVillainAtTheTable();
    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    app.db.raw.exec(
        "CREATE TRIGGER no_delete BEFORE DELETE ON characters BEGIN SELECT RAISE(ABORT, 'refused'); END");

    const failed = await decide(app, gm.cookie, membership, 'approve', 1);
    assert.equal(failed.status, 500);

    app.db.raw.exec('DROP TRIGGER no_delete');
    assert.equal((await characters(app, gm.cookie)).characters.length, 0, 'the GM’s copy was undone');
    const row = await gmRow(app, gm.cookie, membership);
    assert.equal(row.hasPending, true, 'still waiting');
    assert.equal(row.handedOver, false, 'the mark was undone');
    assert.equal((await characters(app, player.cookie)).characters.length, 1);
});

// ── Keyed to a Hero (0012) ───────────────────────────────────────────────────────────────

test('a Villain is refused without a Hero, and a Hero carries no key whatever is sent', async () => {
    const { app, gm, player, membership, hero } = await aVillainAtTheTable();

    const bare = await submit(app, player.cookie, membership, { nemesisOf: null });
    assert.equal(bare.status, 400);
    assert.match((await bare.json()).error, /nemesis of one of your Heroes/);

    // The control: the same membership sent as a Hero with a key goes, and the key is dropped.
    assert.equal((await submit(app, player.cookie, membership, { kind: 'hero', nemesisOf: hero })).status, 200);
    assert.equal((await gmRow(app, gm.cookie, membership)).nemesisOf, null);
});

test('the key is stored with the snapshot and both sides read it', async () => {
    const { app, gm, player, membership, hero } = await aVillainAtTheTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await gmRow(app, gm.cookie, membership)).nemesisOf, hero);

    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);
    assert.equal((await gmRow(app, gm.cookie, membership)).nemesisOf, hero, 'approval keeps the key');
    assert.equal((await playerRow(app, player.cookie, membership)).nemesisOf, hero);
});

test('a Villain can only be keyed to one of the sender’s own Heroes in the same game', async () => {
    const { app, gm, player, membership, code } = await aVillainAtTheTable();

    // Another player's Hero in the same game.
    const other = await signIn(app, 'other@example.test');
    const theirs = (await (await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId: cid(5), label: 'Brian Talison' }, cookie: other.cookie,
    })).json()).id;

    for (const [what, heroId] of [['another player’s Hero', theirs], ['the Villain itself', membership],
        ['an id that never existed', 'm_0000000000000000000000']]) {
        const refused = await submit(app, player.cookie, membership, { nemesisOf: heroId });
        assert.equal(refused.status, 400, what);
        assert.equal((await refused.json()).error, 'That Hero is not one of yours in this game.', what);
    }

    assert.equal((await gmRow(app, gm.cookie, membership)).hasPending, false, 'nothing was written');
});

test('the GM re-keys a nemesis of theirs; the player cannot, and nobody can before it is handed over', async () => {
    const { app, gm, player, membership, hero, code } = await aVillainAtTheTable();
    const rekey = (cookie, heroId) => app.call(`/api/memberships/${membership}/nemesis-of`,
        { method: 'PUT', body: { nemesisOf: heroId }, cookie });

    const other = await signIn(app, 'other@example.test');
    const theirs = (await (await app.call('/api/memberships/join', {
        method: 'POST', body: { code, characterId: cid(5), label: 'Brian Talison' }, cookie: other.cookie,
    })).json()).id;

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await rekey(gm.cookie, theirs)).status, 404, 'not the GM’s until it is approved');

    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    assert.equal((await rekey(player.cookie, hero)).status, 404, 'the player cannot re-key');
    assert.equal((await rekey(gm.cookie, membership)).status, 409, 'not to itself');

    // The GM may key it to any Hero in the game, another player's included.
    assert.equal((await rekey(gm.cookie, theirs)).status, 204);
    assert.equal((await gmRow(app, gm.cookie, membership)).nemesisOf, theirs);
});
