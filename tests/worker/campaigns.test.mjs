// Campaigns, plural, belonging to one account — and the invariant they share with characters.
//
// **The whole point of this file is that the server does not know what a campaign is.** The four
// addresses, the id shape and the label default are all it is allowed to decide; a tier, a Trait
// Cap and a budget are inside the payload, which no query in this system ever parses.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { server, signIn } from './harness.mjs';

/** Shaped like what `StoredCampaign` writes: a version and the campaign's own fields. */
const campaign = {
    Version: 1,
    Campaign: {
        Id: 'g_0000000000000000000000',
        Name: 'The Long Winter',
        TierId: 'standard',
        TraitCapRank: 8,
        UnlimitedBudget: false,
    },
};

/** A well-formed campaign id, distinguished only by its last digit. */
const gid = (n = 0) => 'g_000000000000000000000' + n;

/** A well-formed character id, the same trick with the other letter. */
const cid = (n = 0) => 'c_000000000000000000000' + n;

/** `campaign`, with a different `Name` — distinguishable in the raw bytes a read hands back. */
const named = name => ({ ...campaign, Campaign: { ...campaign.Campaign, Name: name } });

const putCampaign = (app, cookie, { theId = gid(), label, payload = campaign, format } = {}) =>
    app.call(`/api/campaigns/${theId}`,
        { method: 'PUT', body: { label, payload: JSON.stringify(payload), format }, cookie });

test('a campaign follows its account to another browser', async () => {
    const app = server();
    const laptop = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, laptop.cookie, { label: 'The Long Winter' })).status, 204);

    const phone = await signIn(app, 'gm@example.test');
    assert.notEqual(phone.cookie, laptop.cookie, 'the same session was reused, so this proves nothing');

    const read = await app.call(`/api/campaigns/${gid()}`, { cookie: phone.cookie });

    assert.equal(read.status, 200);
    assert.deepEqual(await read.json(), campaign);
});

test('nobody signed in reaches a campaign at all', async () => {
    const app = server();

    for (const [method, path] of [
        ['GET', '/api/campaigns'],
        ['GET', `/api/campaigns/${gid()}`],
        ['PUT', `/api/campaigns/${gid()}`],
        ['DELETE', `/api/campaigns/${gid()}`],
    ]) {
        const response = await app.call(path,
            { method, body: method === 'PUT' ? { payload: '{}' } : undefined });

        assert.equal(response.status, 401, `${method} ${path} answered ${response.status}`);
    }
});

test('one account cannot read, overwrite or delete another account’s campaign by id', async () => {
    const app = server();
    const mine = await signIn(app, 'me@example.test');
    const yours = await signIn(app, 'you@example.test');

    await putCampaign(app, mine.cookie, { label: 'Mine' });

    assert.equal((await app.call(`/api/campaigns/${gid()}`, { cookie: yours.cookie })).status, 404);

    assert.equal((await app.call(`/api/campaigns/${gid()}`,
        { method: 'DELETE', cookie: yours.cookie })).status, 204);
    assert.equal((await app.call(`/api/campaigns/${gid()}`, { cookie: mine.cookie })).status, 200,
        'mine must have survived a delete attempt from another account');

    assert.equal((await putCampaign(app, yours.cookie, {
        label: 'Yours',
        payload: { ...campaign, Campaign: { ...campaign.Campaign, Name: 'Someone else’s game' } },
    })).status, 204);

    const stillMine = await (await app.call(`/api/campaigns/${gid()}`, { cookie: mine.cookie })).json();
    assert.equal(stillMine.Campaign.Name, 'The Long Winter');
});

test('an id that is not the campaign shape is refused before it reaches a query', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    // A character id is refused here on purpose: the two shapes differ by one letter precisely so
    // that neither can be passed where the other is meant.
    for (const bad of [cid(), 'g_short', 'g_' + 'x'.repeat(23), '../../etc', 'g_'])
    {
        const response = await app.call(`/api/campaigns/${encodeURIComponent(bad)}`, { cookie });
        assert.equal(response.status, 400, `${bad} answered ${response.status}`);
    }
});

test('the list is ordered most recently touched first, and carries no cap', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    await putCampaign(app, cookie, { theId: gid(1), label: 'First' });
    app.now += 1000;
    await putCampaign(app, cookie, { theId: gid(2), label: 'Second' });

    const listed = await (await app.call('/api/campaigns', { cookie })).json();

    assert.deepEqual(listed.campaigns.map(c => c.label), ['Second', 'First']);

    // **No `limit`.** `users.character_limit` caps characters; there is no campaign equivalent,
    // and answering with one would invent a rule the contract does not have.
    assert.equal(listed.limit, undefined);
});

test('an empty or missing label becomes the ordinary default rather than a refusal', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    await putCampaign(app, cookie, { theId: gid(1), label: '   ' });
    await putCampaign(app, cookie, { theId: gid(2) });

    const listed = await (await app.call('/api/campaigns', { cookie })).json();

    assert.deepEqual(listed.campaigns.map(c => c.label).sort(),
        ['Unnamed campaign', 'Unnamed campaign']);
});

// ── The invariant ────────────────────────────────────────────────────────────────────────

/**
 * Payloads that are not characters, are not campaigns, and are not anything this server should
 * have an opinion about.
 *
 * The last two are the interesting ones: a payload naming a tier that does not exist, on a
 * character with an Ability far above any Trait Cap, is an *illegal character* — and storing it
 * is correct, because the engine reports it and the person fixes it. A server that refused it
 * would be deciding a rule, and would do it with a copy of the rules that could drift.
 */
const opaque = [
    ['an empty object', '{}'],
    ['an array', '[]'],
    ['a bare number', '123'],
    ['a bogus tier and a 99d Ability',
        '{"Version":1,"Mode":0,"Sheet":{"SelectedTierId":"no_such_tier","AbilityRanks":{"might":99}}}'],
    ['no tier at all', '{"Version":1,"Mode":0,"Sheet":{"Name":"Half a character"}}'],
];

test('the server stores a character it cannot make sense of, byte for byte', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    let n = 0;
    for (const [what, payload] of opaque) {
        const theId = cid(n++);

        const written = await app.call(`/api/characters/${theId}`,
            { method: 'PUT', body: { label: what, payload }, cookie });

        assert.equal(written.status, 204, `${what} was refused with ${written.status}`);

        const read = await app.call(`/api/characters/${theId}`, { cookie });

        assert.equal(read.status, 200, what);
        assert.equal(await read.text(), payload, `${what} did not come back byte for byte`);
    }
});

test('the same is true of a campaign', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    let n = 0;
    for (const [what, payload] of opaque) {
        const theId = gid(n++);

        const written = await app.call(`/api/campaigns/${theId}`,
            { method: 'PUT', body: { label: what, payload }, cookie });

        assert.equal(written.status, 204, `${what} was refused with ${written.status}`);
        assert.equal(await (await app.call(`/api/campaigns/${theId}`, { cookie })).text(), payload);
    }
});

// ── The character's campaign_id ──────────────────────────────────────────────────────────

test('campaignId is carried into the list exactly as it was sent', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    await app.call(`/api/characters/${cid(1)}`,
        { method: 'PUT', body: { label: 'In a game', payload: '{}', campaignId: gid() }, cookie });
    await app.call(`/api/characters/${cid(2)}`,
        { method: 'PUT', body: { label: 'In none', payload: '{}' }, cookie });

    const listed = await (await app.call('/api/characters', { cookie })).json();
    const byLabel = Object.fromEntries(listed.characters.map(c => [c.label, c.campaignId]));

    assert.equal(byLabel['In a game'], gid());
    assert.equal(byLabel['In none'], null, 'a character in no campaign must say so, not be absent');
});

test('a campaignId naming no campaign is stored anyway', async () => {
    // **The server never checks the reference and must not start.** A character may name a
    // campaign that lives in another browser and has never been uploaded, or one that has been
    // deleted. Both are ordinary; the browser reports them, the same way it reports an unknown
    // tier. Validating here would make this server the authority on whether a character is in a
    // legal state, which is the job it exists not to have.
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const written = await app.call(`/api/characters/${cid(1)}`,
        { method: 'PUT', body: { label: 'Orphan', payload: '{}', campaignId: gid(9) }, cookie });

    assert.equal(written.status, 204);

    const listed = await (await app.call('/api/characters', { cookie })).json();
    assert.equal(listed.characters[0].campaignId, gid(9));
});

test('a campaignId that is not an id at all is refused, and one that is missing is not', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'player@example.test');

    const send = campaignId => app.call(`/api/characters/${cid(1)}`,
        { method: 'PUT', body: { label: 'x', payload: '{}', campaignId }, cookie });

    assert.equal((await send('not-an-id')).status, 400);
    assert.equal((await send(cid())).status, 400, 'a character id is not a campaign id');
    assert.equal((await send(42)).status, 400);

    assert.equal((await send(null)).status, 204);
    assert.equal((await send('')).status, 204, 'empty is "no campaign", not a malformed one');
});

test('deleting a campaign leaves its members naming it', async () => {
    // **Owner-approved, and the opposite of what a foreign key would do.** A character whose
    // campaign has gone is reported as naming a campaign that is not here — the same shape an
    // unknown tier takes — rather than being quietly edited by a delete somebody pressed
    // elsewhere. Restoring the campaign puts everything back, which a cascade could not.
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    await putCampaign(app, cookie, { label: 'The Long Winter' });
    await app.call(`/api/characters/${cid(1)}`,
        { method: 'PUT', body: { label: 'Ninefold', payload: '{}', campaignId: gid() }, cookie });

    assert.equal((await app.call(`/api/campaigns/${gid()}`,
        { method: 'DELETE', cookie })).status, 204);

    const listed = await (await app.call('/api/characters', { cookie })).json();

    assert.equal(listed.characters.length, 1, 'the character must not have been deleted with it');
    assert.equal(listed.characters[0].campaignId, gid(),
        'the character stopped naming its campaign; the delete cascaded or nulled the column');
});

test('a campaign is not a character and does not count against the cap', async () => {
    // Five is the default cap. Five characters fill it; a sixth campaign is still fine, because
    // the cap is a cap on characters and `putCampaign` has no cap check at all.
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    for (let i = 0; i < 5; i++) {
        assert.equal((await app.call(`/api/characters/${cid(i)}`,
            { method: 'PUT', body: { label: `C${i}`, payload: '{}' }, cookie })).status, 204);
    }

    assert.equal((await app.call(`/api/characters/${cid(6)}`,
        { method: 'PUT', body: { label: 'One too many', payload: '{}' }, cookie })).status, 409);

    for (let i = 0; i < 6; i++) {
        assert.equal((await putCampaign(app, cookie, { theId: gid(i), label: `G${i}` })).status, 204,
            'a campaign was refused for a cap that is not about campaigns');
    }
});

// ── `payload_format`: an older build's save must not erase what it cannot see ────────────

test('a save with no format at all is treated as format 0, exactly like an older build', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    // Nothing has ever written this campaign, so the stored format defaults to 0 (the column's
    // own DEFAULT) and a write naming no format at all — the shape an older build sends — has to
    // land rather than being refused against a row that does not yet exist.
    assert.equal((await putCampaign(app, cookie, { label: 'First' })).status, 204);

    const read = await app.call(`/api/campaigns/${gid()}`, { cookie });
    assert.equal(read.status, 200);
});

test('a lower format after a higher one is refused with the sentence written for the reader', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, cookie,
        { label: 'Newer', payload: named('Newer'), format: 1 })).status, 204);

    const refused = await putCampaign(app, cookie,
        { label: 'Older tab', payload: named('Older tab'), format: 0 });
    assert.equal(refused.status, 409);

    const body = await refused.json();
    assert.equal(body.error,
        'This tab is running an older version of the site. Saving now would erase settings it '
        + 'cannot see — reload the page and try again.');

    // **Nothing was written.** The refusal is only honest if the stored campaign is untouched —
    // a merge, even a partial one, would be the same loss this feature exists to prevent, one
    // field at a time.
    const read = await app.call(`/api/campaigns/${gid()}`, { cookie });
    assert.equal((await read.json()).Campaign.Name, 'Newer');
});

test('the same format saves cleanly, and a higher one after it also lands', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, cookie,
        { label: 'One', payload: named('One'), format: 1 })).status, 204);
    assert.equal((await putCampaign(app, cookie,
        { label: 'Still one', payload: named('Still one'), format: 1 })).status, 204);
    assert.equal((await putCampaign(app, cookie,
        { label: 'Two', payload: named('Two'), format: 2 })).status, 204);

    const read = await app.call(`/api/campaigns/${gid()}`, { cookie });
    assert.equal((await read.json()).Campaign.Name, 'Two');
});

test('a format that is not an integer is refused before it reaches storage', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    for (const bad of ['1', 1.5, true, {}, [1], null]) {
        assert.equal((await putCampaign(app, cookie, { label: 'Bad', format: bad })).status, 400,
            `format ${JSON.stringify(bad)} was not refused`);
    }
});

test('a read is unaffected by the format check — the bytes come back exactly as stored', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'gm@example.test');

    await putCampaign(app, cookie, { label: 'Newer', format: 1 });
    await putCampaign(app, cookie, { label: 'Older tab', format: 0 }); // refused, 409

    const read = await app.call(`/api/campaigns/${gid()}`, { cookie });
    assert.equal(read.status, 200);
    assert.deepEqual(await read.json(), campaign);
});
