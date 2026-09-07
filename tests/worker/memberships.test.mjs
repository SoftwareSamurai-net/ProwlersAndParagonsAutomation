// A campaign's clone of a character, and the snapshot waiting for a decision.
//
// **Four properties this file exists for, and each is a defect if it is wrong rather than a
// feature that does not work:**
//
//   1. **No cross-account read.** One account cannot reach another's character, campaign or clone
//      by any route, by id or by code. A membership has two owners and every statement is scoped
//      to one of them.
//   2. **Approving a stale snapshot is refused, and the refusal names the newer one.** The GM
//      reads A, the player resubmits B, the GM clicks Approve — without the compare-and-swap, B
//      is approved unseen.
//   3. **The server still never parses a character.** A clone goes in and comes out byte for byte,
//      including payloads that are not characters at all.
//   4. **A clone does not count against the GM's character cap.** The whole reason the clones are
//      not in `characters`: that cap is `COUNT(*) FROM characters WHERE user_id = ?`, so a GM with
//      six players would hit their own five-character limit.
//
// **What this suite cannot see**, stated because the previous outage was exactly this: the harness
// builds its schema by running the migrations, so it cannot tell you that the deployed database's
// schema differs. Everything below is true of a database built from `d1/migrations`; whether
// production's *was* is a question for the deploy, not for here.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { server, signIn } from './harness.mjs';

/** Shaped like what `StoredCampaign` writes: a version and the campaign's own fields. */
const campaignPayload = {
    Version: 1,
    Campaign: {
        Id: 'g_0000000000000000000000',
        Name: 'Nightfall',
        TierId: 'standard',
        TraitCapRank: 8,
        UnlimitedBudget: false,
    },
};

/** Shaped like what `StoredCharacter` writes. Opaque to the server either way. */
const characterPayload = (name = 'Ninefold', might = 6) => JSON.stringify({
    Version: 1,
    Mode: 0,
    Sheet: { SelectedTierId: 'standard', Name: name, AbilityRanks: { might } },
});

const gid = (n = 0) => 'g_000000000000000000000' + n;
const cid = (n = 0) => 'c_000000000000000000000' + n;
const mid = (n = 0) => 'm_000000000000000000000' + n;

// **A leaked account id, matched as a whole value rather than as two characters.**
//
// This was `!body.includes('u_')` in three places, and it failed CI on a body that leaked
// nothing: a membership id is `m_` plus 22 URL-safe characters, that alphabet contains both
// `u` and `_`, and `m_z7u7CoX4nDqzpT6w7Vu_Ss` therefore contains the banned substring. It is
// a ~0.5% coin flipped on every run — 21 adjacent pairs over a 64-symbol alphabet — which is
// why it stayed green for as long as it did, and why re-running until it passes would have
// been the wrong fix twice over.
//
// A user id is `'u_' + 22` URL-safe characters (`worker/crypto.js`), so that is what this
// matches, as a complete JSON string value. `CLAUDE.md`'s rule applies exactly: a ban that
// fires on something innocent is worse than no ban, because the natural fix is to weaken it.
const LEAKED_ACCOUNT_ID = /"u_[A-Za-z0-9_-]{22}"/;

const namesNoAccount = (body) => assert.ok(!LEAKED_ACCOUNT_ID.test(body), body);

const putCampaign = (app, cookie, { theId = gid(), label = 'Nightfall' } = {}) =>
    app.call(`/api/campaigns/${theId}`, {
        method: 'PUT',
        body: { label, payload: JSON.stringify(campaignPayload) },
        cookie,
    });

/** The join code the server minted for a campaign, read the way the GM's screen reads it. */
async function joinCodeFor(app, cookie, theId = gid()) {
    const listed = await (await app.call('/api/campaigns', { cookie })).json();
    const row = listed.campaigns.find(c => c.id === theId);

    assert.ok(row, 'the campaign was not in its own owner’s list');
    assert.ok(row.joinCode, 'the campaign has no join code, so nobody could ever join it');

    return row.joinCode;
}

const join = (app, cookie, { code, characterId = cid(), label = 'Ninefold' }) =>
    app.call('/api/memberships/join', { method: 'POST', body: { code, characterId, label }, cookie });

const submit = (app, cookie, id, { payload = characterPayload(), label = 'Ninefold' } = {}) =>
    app.call(`/api/memberships/${id}/submission`, { method: 'PUT', body: { label, payload }, cookie });

const decide = (app, cookie, id, what, version) =>
    app.call(`/api/memberships/${id}/${what}`, { method: 'POST', body: { version }, cookie });

/**
 * A GM with a campaign and a player who has joined it, which is the state most of the tests
 * below start from. Goes through the real endpoints rather than inserting rows, so a test about
 * approval cannot pass against a join that has stopped working.
 */
async function aTable() {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, gm.cookie)).status, 204);
    const code = await joinCodeFor(app, gm.cookie);

    const player = await signIn(app, 'player@example.test');
    const joined = await join(app, player.cookie, { code });

    assert.equal(joined.status, 200);

    return { app, gm, player, code, membership: (await joined.json()).id };
}

// ── Joining ─────────────────────────────────────────────────────────────────────────────

test('joining by code answers the campaign’s own settings and nothing about the GM', async () => {
    const { app, player, membership } = await aTable();

    // The payload comes back verbatim, which is what lets the browser read a tier out of it. The
    // server does not know there is a tier in there.
    const read = await app.call(`/api/memberships/${membership}`, { cookie: player.cookie });
    const seen = await read.json();

    assert.equal(read.status, 200);
    assert.equal(seen.role, 'player');
    assert.equal(seen.characterId, cid());
    assert.equal(seen.campaignId, gid());
    assert.equal(seen.pendingVersion, 0);
    assert.equal(seen.approved, null, 'joining must not submit anything');
    assert.equal(seen.pending, null);

    // Nothing anywhere in the answer names the GM's account or address.
    const body = JSON.stringify(seen);
    assert.ok(!body.includes('gm@example.test'), body);
    namesNoAccount(body);
});

test('the join answer carries the campaign payload byte for byte', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');

    // Key order and spacing no serialiser would reproduce, sent raw — so "byte for byte" is a
    // claim about the bytes rather than about two objects being deep-equal.
    const raw = '{"Version":1,  "Campaign":{"Name":"Nightfall","TierId":"iconic"}}';

    assert.equal((await app.call(`/api/campaigns/${gid()}`, {
        method: 'PUT', raw: JSON.stringify({ label: 'Nightfall', payload: raw }), cookie: gm.cookie,
    })).status, 204);

    const code = await joinCodeFor(app, gm.cookie);
    const player = await signIn(app, 'player@example.test');
    const joined = await join(app, player.cookie, { code });

    assert.equal((await joined.clone().json()).payload, raw);
});

test('a code is read case-insensitively and without its hyphen', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    const code = await joinCodeFor(app, gm.cookie);
    const player = await signIn(app, 'player@example.test');

    // The positive control: the code as minted really does work, so the two spellings below are
    // being compared against something rather than all failing together.
    assert.equal((await join(app, player.cookie, { code, characterId: cid(1) })).status, 200);

    assert.equal((await join(app, player.cookie,
        { code: code.toLowerCase(), characterId: cid(2) })).status, 200);
    assert.equal((await join(app, player.cookie,
        { code: code.replace('-', ''), characterId: cid(3) })).status, 200);
    assert.equal((await join(app, player.cookie,
        { code: ` ${code.toLowerCase()} `, characterId: cid(4) })).status, 200);
});

test('an unknown code, a rotated one and a malformed one are told apart only where they differ', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    const wasValid = await joinCodeFor(app, gm.cookie);
    const player = await signIn(app, 'player@example.test');

    // Malformed is a 400: the request cannot be about a campaign at all.
    for (const code of ['', 'nope', '12345-6789', 'IIIII-IIIII', 12345, null]) {
        assert.equal((await join(app, player.cookie, { code })).status, 400,
            `'${code}' should not be readable as a code`);
    }

    // A well-formed code naming no campaign is a 404 — and so is one that has been replaced. The
    // two are the same fact from the caller's side and must not be distinguishable, or asking
    // twice tells somebody a code was once valid.
    assert.equal((await join(app, player.cookie, { code: 'ABCDE-FGHJK' })).status, 404);

    const rotated = await app.call(`/api/campaigns/${gid()}/code`,
        { method: 'POST', cookie: gm.cookie });
    assert.equal(rotated.status, 200);
    assert.notEqual((await rotated.json()).joinCode, wasValid);

    const refused = await join(app, player.cookie, { code: wasValid });
    assert.equal(refused.status, 404);
    assert.deepEqual(await refused.json(), { error: 'No campaign is using that code.' },
        'the refusal for a replaced code must be byte-identical to the one for a code that never existed');
});

test('rotating a code does not evict anybody already in the campaign', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await app.call(`/api/campaigns/${gid()}/code`,
        { method: 'POST', cookie: gm.cookie })).status, 200);

    // The membership survives, and the player can still submit through it.
    assert.equal((await app.call(`/api/memberships/${membership}`,
        { cookie: player.cookie })).status, 200);
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
});

test('an ordinary save does not rotate the code', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, gm.cookie)).status, 204);
    const before = await joinCodeFor(app, gm.cookie);

    // A rename, and then a settings change: both are `putCampaign`, which mints a candidate every
    // time. `COALESCE` in the statement is what keeps the one already there.
    assert.equal((await putCampaign(app, gm.cookie, { label: 'Nightfall, year two' })).status, 204);
    assert.equal((await putCampaign(app, gm.cookie, { label: 'Nightfall' })).status, 204);

    assert.equal(await joinCodeFor(app, gm.cookie), before);
});

test('joining twice with the same character is the same membership, not a second one', async () => {
    const { app, player, code, membership } = await aTable();

    const again = await join(app, player.cookie, { code, label: 'Ninefold renamed' });

    assert.equal(again.status, 200);
    assert.equal((await again.json()).id, membership);

    const listed = await (await app.call('/api/memberships', { cookie: player.cookie })).json();

    assert.equal(listed.memberships.length, 1);
    assert.equal(listed.memberships[0].label, 'Ninefold renamed', 'the label should refresh');
});

test('nobody signed in reaches a membership at all', async () => {
    const app = server();

    for (const [method, path] of [
        ['GET', '/api/memberships'],
        ['GET', '/api/memberships/inbox'],
        ['POST', '/api/memberships/join'],
        ['GET', `/api/memberships/${mid()}`],
        ['PUT', `/api/memberships/${mid()}/submission`],
        ['POST', `/api/memberships/${mid()}/approve`],
        ['POST', `/api/memberships/${mid()}/reject`],
        ['GET', `/api/memberships/${mid()}/table`],
        ['POST', `/api/campaigns/${gid()}/code`],
    ]) {
        const response = await app.call(path, { method, body: method === 'GET' ? undefined : {} });

        assert.equal(response.status, 401, `${method} ${path} answered ${response.status}`);
    }
});

// ── No cross-account read ───────────────────────────────────────────────────────────────

test('a third account reaches nothing about a membership it is not part of', async () => {
    const { app, membership } = await aTable();
    const stranger = await signIn(app, 'stranger@example.test');

    assert.equal((await app.call(`/api/memberships/${membership}`,
        { cookie: stranger.cookie })).status, 404);
    assert.equal((await submit(app, stranger.cookie, membership)).status, 404);
    assert.equal((await decide(app, stranger.cookie, membership, 'approve', 1)).status, 404);
    assert.equal((await decide(app, stranger.cookie, membership, 'reject', 1)).status, 404);

    // Nor the campaign, nor the character, by id.
    assert.equal((await app.call(`/api/campaigns/${gid()}`,
        { cookie: stranger.cookie })).status, 404);
    assert.equal((await app.call(`/api/characters/${cid()}`,
        { cookie: stranger.cookie })).status, 404);
    assert.equal((await app.call(`/api/campaigns/${gid()}/code`,
        { method: 'POST', cookie: stranger.cookie })).status, 404);

    // And its own lists are empty rather than showing somebody else's row.
    assert.deepEqual((await (await app.call('/api/memberships',
        { cookie: stranger.cookie })).json()).memberships, []);
    assert.deepEqual((await (await app.call('/api/memberships/inbox',
        { cookie: stranger.cookie })).json()).memberships, []);
});

test('the player cannot approve their own submission and the GM cannot submit', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    // A player holding the membership id is on the wrong side of it for a decision. 404, not 403:
    // "that exists but you may not decide about it" is a fact about somebody else's campaign.
    assert.equal((await decide(app, player.cookie, membership, 'approve', 1)).status, 404);
    assert.equal((await decide(app, player.cookie, membership, 'reject', 1)).status, 404);

    // And the GM cannot write a snapshot into it, which would be the GM editing the clone by hand
    // — deliberately out of this design.
    assert.equal((await submit(app, gm.cookie, membership)).status, 404);
});

test('the GM is never told which account a submission came from', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    const inbox = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    const one = await (await app.call(`/api/memberships/${membership}`, { cookie: gm.cookie })).json();

    assert.equal(inbox.memberships.length, 1, 'the positive control: there is a row to inspect');
    assert.equal(one.role, 'gm');
    assert.equal(one.characterId, null, 'the player’s own character id is not the GM’s business');

    for (const body of [JSON.stringify(inbox), JSON.stringify(one)]) {
        assert.ok(!body.includes('player@example.test'), body);
        namesNoAccount(body);
        assert.ok(!body.includes(cid()), body);
    }
});

test('two players in one campaign cannot see each other', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    const code = await joinCodeFor(app, gm.cookie);

    const one = await signIn(app, 'one@example.test');
    const two = await signIn(app, 'two@example.test');

    const a = (await (await join(app, one.cookie, { code, label: 'Ninefold' })).json()).id;
    const b = (await (await join(app, two.cookie, { code, label: 'Vector' })).json()).id;

    assert.notEqual(a, b, 'two players must not share a membership row');

    assert.equal((await app.call(`/api/memberships/${b}`, { cookie: one.cookie })).status, 404);
    assert.equal((await app.call(`/api/memberships/${a}`, { cookie: two.cookie })).status, 404);

    // Each player's list holds exactly their own; the GM's inbox holds both.
    assert.deepEqual((await (await app.call('/api/memberships',
        { cookie: one.cookie })).json()).memberships.map(m => m.id), [a]);
    assert.deepEqual((await (await app.call('/api/memberships',
        { cookie: two.cookie })).json()).memberships.map(m => m.id).sort(), [b]);
    assert.deepEqual((await (await app.call('/api/memberships/inbox',
        { cookie: gm.cookie })).json()).memberships.map(m => m.id).sort(), [a, b].sort());
});

test('one player cannot squat on another’s character id in the same campaign', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    const code = await joinCodeFor(app, gm.cookie);

    const squatter = await signIn(app, 'squatter@example.test');
    const victim = await signIn(app, 'victim@example.test');

    // The squatter joins naming an id that will later be the victim's. The key is
    // (campaign, player, character), so this is the squatter's own row and blocks nothing.
    assert.equal((await join(app, squatter.cookie, { code, characterId: cid(7) })).status, 200);

    const later = await join(app, victim.cookie, { code, characterId: cid(7) });

    assert.equal(later.status, 200, 'the victim must still be able to join with their own id');
    assert.notEqual((await later.json()).id, undefined);
});

// ── The server still never parses a character ───────────────────────────────────────────

test('a submitted snapshot comes back byte for byte, whatever it is', async () => {
    const cases = [
        '{}',
        '[]',
        '123',
        '"a string"',
        'null',
        // A payload naming a bogus tier: the server has no opinion, and must not grow one.
        '{"Version":1,"Sheet":{"SelectedTierId":"no_such_tier"}}',
        // …and one with no tier at all.
        '{"Version":1,"Sheet":{"Name":"Untiered"}}',
        // Key order and spacing no serialiser would produce.
        '{"b":1,   "a":2}',
    ];

    for (const payload of cases) {
        const { app, gm, player, membership } = await aTable();

        assert.equal((await submit(app, player.cookie, membership, { payload })).status, 200,
            `the server refused ${payload}`);

        const asPlayer = await (await app.call(`/api/memberships/${membership}`,
            { cookie: player.cookie })).json();
        const asGm = await (await app.call(`/api/memberships/${membership}`,
            { cookie: gm.cookie })).json();

        assert.equal(asPlayer.pending, payload, `player read ${payload} back changed`);
        assert.equal(asGm.pending, payload, `GM read ${payload} back changed`);

        assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

        const approved = await (await app.call(`/api/memberships/${membership}`,
            { cookie: gm.cookie })).json();

        assert.equal(approved.approved, payload, `the clone changed ${payload}`);
        assert.equal(approved.pending, null, 'approving clears the pending slot');
    }
});

test('a payload that is not JSON at all is refused', async () => {
    const { app, player, membership } = await aTable();

    // **Sent without going through the helper above**, whose own default would supply a real
    // payload for the missing case — a test defaulted past the thing it is about.
    for (const body of [
        { label: 'x', payload: 'not json' },
        { label: 'x', payload: '{' },
        { label: 'x', payload: '' },
        { label: 'x', payload: 5 },
        { label: 'x', payload: null },
        { label: 'x' },
        {},
    ]) {
        const response = await app.call(`/api/memberships/${membership}/submission`,
            { method: 'PUT', body, cookie: player.cookie });

        assert.equal(response.status, 400, `${JSON.stringify(body)} was accepted`);
    }

    // Nothing got through any of them: the absence is the point, and a route that refused every
    // request would satisfy the loop above for the wrong reason.
    assert.equal((await (await app.call(`/api/memberships/${membership}`,
        { cookie: player.cookie })).json()).pending, null);
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
});

// ── The version check ───────────────────────────────────────────────────────────────────

test('approving a stale snapshot is refused, and the refusal carries the newer one', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 6) })).status, 200);

    // What the GM is looking at.
    const looking = await (await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).json();

    assert.equal(looking.pendingVersion, 1);
    assert.equal(looking.pending, characterPayload('Ninefold', 6));

    // The player resubmits while the diff is on screen.
    const resubmitted = await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 12) });

    assert.equal(resubmitted.status, 200);
    assert.equal((await resubmitted.json()).version, 2, 'a resubmission must move the version');

    // The GM clicks Approve on the version they read.
    const refused = await decide(app, gm.cookie, membership, 'approve', looking.pendingVersion);
    const said = await refused.json();

    assert.equal(refused.status, 409);
    assert.equal(said.pendingVersion, 2);
    assert.equal(said.pending, characterPayload('Ninefold', 12),
        'the refusal must hand back the snapshot the GM has not seen');

    // Nothing was approved. This is the assertion that makes the whole test about a defect rather
    // than about a status code.
    const after = await (await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).json();

    assert.equal(after.approved, null, 'the unseen snapshot must not have become the clone');
    assert.equal(after.pending, characterPayload('Ninefold', 12));

    // And the same decision, made about the version it actually names, goes through — the
    // positive control, without which "409" could be what this endpoint always says.
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 2)).status, 204);
});

test('rejecting a stale snapshot is refused the same way', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 12) })).status, 200);

    const refused = await decide(app, gm.cookie, membership, 'reject', 1);

    assert.equal(refused.status, 409);
    assert.equal((await refused.json()).pendingVersion, 2);

    // Still waiting: a refused rejection must not have quietly dropped the submission.
    const after = await (await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).json();

    assert.equal(after.pending, characterPayload('Ninefold', 12));
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 2)).status, 204);
});

test('a decision naming no version is refused rather than applied to the latest', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    for (const version of [undefined, null, 'one', 1.5, -1, {}]) {
        const response = await app.call(`/api/memberships/${membership}/approve`,
            { method: 'POST', body: { version }, cookie: gm.cookie });

        assert.equal(response.status, 400, `version ${JSON.stringify(version)} was accepted`);
    }

    // Nothing got through any of them.
    assert.equal((await (await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).json()).approved, null);
});

test('the version keeps counting after a decision, so a number cannot be reused', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    // A fresh submission after an approval is version 2, not 1 again — otherwise a GM still
    // holding "1" from the first diff could approve the second snapshot unseen.
    const resubmitted = await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 12) });

    assert.equal((await resubmitted.json()).version, 2);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 409);
});

test('deciding when nothing is waiting says so rather than clearing the clone', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    const again = await decide(app, gm.cookie, membership, 'approve', 1);

    assert.equal(again.status, 409);
    assert.equal((await again.json()).pending, null);

    // The clone survived the second click.
    assert.equal((await (await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).json()).approved, characterPayload());
});

test('rejecting leaves the clone exactly as it was, and the player’s own character alone', async () => {
    const { app, gm, player, membership } = await aTable();

    // A clone the GM has already accepted.
    assert.equal((await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 6) })).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    // The player writes their own character down, then sends a change the GM turns down.
    assert.equal((await app.call(`/api/characters/${cid()}`, {
        method: 'PUT',
        body: { label: 'Ninefold', payload: characterPayload('Ninefold', 12) },
        cookie: player.cookie,
    })).status, 204);

    assert.equal((await submit(app, player.cookie, membership,
        { payload: characterPayload('Ninefold', 12) })).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 2)).status, 204);

    const after = await (await app.call(`/api/memberships/${membership}`,
        { cookie: player.cookie })).json();

    assert.equal(after.approved, characterPayload('Ninefold', 6), 'the clone must not have moved');
    assert.equal(after.pending, null);

    // And the player's own row is untouched — a rejection is a decision about the campaign's copy.
    const own = await app.call(`/api/characters/${cid()}`, { cookie: player.cookie });

    assert.equal(await own.text(), characterPayload('Ninefold', 12));
});

// ── The cap ─────────────────────────────────────────────────────────────────────────────

test('clones do not count against the GM’s character limit', async () => {
    const app = server();
    const gm = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, gm.cookie)).status, 204);
    const code = await joinCodeFor(app, gm.cookie);

    // Six players, each with an approved clone — one more than the default cap of five.
    for (let i = 0; i < 6; i++) {
        const player = await signIn(app, `player${i}@example.test`);
        const joined = await join(app, player.cookie,
            { code, characterId: cid(i), label: `Player ${i}` });

        assert.equal(joined.status, 200);

        const membership = (await joined.json()).id;

        assert.equal((await submit(app, player.cookie, membership)).status, 200);
        assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);
    }

    // The GM's inbox holds six clones…
    const inbox = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(inbox.memberships.length, 6);
    assert.ok(inbox.memberships.every(m => m.hasApproved));

    // …and the GM's own account still reports five slots, every one of them free.
    const listed = await (await app.call('/api/characters', { cookie: gm.cookie })).json();

    assert.equal(listed.limit, 5);
    assert.equal(listed.characters.length, 0);

    // The proof that the cap is real and this test is not passing against an absent one: five of
    // the GM's own characters go in, and the sixth is refused.
    for (let i = 0; i < 5; i++) {
        assert.equal((await app.call(`/api/characters/${cid(i)}`, {
            method: 'PUT', body: { label: `NPC ${i}`, payload: '{}' }, cookie: gm.cookie,
        })).status, 204);
    }

    assert.equal((await app.call(`/api/characters/${cid(5)}`, {
        method: 'PUT', body: { label: 'NPC 5', payload: '{}' }, cookie: gm.cookie,
    })).status, 409);
});

// ── Standings, and what a list carries ──────────────────────────────────────────────────

test('a player’s list says where each character stands', async () => {
    const { app, gm, player, membership } = await aTable();

    const standing = async () => {
        const listed = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
        return listed.memberships.find(m => m.id === membership);
    };

    let now = await standing();
    assert.equal(now.hasApproved, false);
    assert.equal(now.hasPending, false);
    assert.equal(now.campaignId, gid());
    assert.equal(now.characterId, cid());

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    now = await standing();
    assert.equal(now.hasPending, true);
    assert.equal(now.hasApproved, false);
    assert.equal(now.pendingVersion, 1);

    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    now = await standing();
    assert.equal(now.hasPending, false);
    assert.equal(now.hasApproved, true);
    assert.ok(now.approvedAt > 0, 'an approved clone carries when it was accepted');
});

test('no list or read carries a payload where it should not', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    for (const [who, path] of [
        [player.cookie, '/api/memberships'],
        [gm.cookie, '/api/memberships/inbox'],
    ]) {
        const body = await (await app.call(path, { cookie: who })).text();

        // The positive control first: there really is a row in this answer.
        assert.ok(body.includes(membership), body);
        assert.ok(!body.includes('Ninefold') || !body.includes('AbilityRanks'),
            `${path} carries a whole character payload: ${body}`);
        assert.ok(!body.includes('AbilityRanks'), `${path} carries a payload: ${body}`);
    }
});

// ── The live table a member may read ────────────────────────────────────────────────────
//
// **The one campaign read that is scoped to somebody who does not own the campaign**, and the
// reason it exists: everything else about a campaign is the GM's, so a player's browser draws the
// copy of the table's rules that was written onto their character when it joined. That copy stays
// in force; what nothing could say until this route existed is that the game has moved on.
//
// What every test below is really about is the answer's *edges*. The payload is the same bytes
// `join` already hands a player, so the disclosure is not new — the row that authorises the read
// is, and a predicate that let a stranger, a GM, or a campaign belonging to a different account
// through would be a campaign readable by somebody who never joined it.

/** Read the live table for a membership, as the member's own browser reads it. */
const liveTable = (app, cookie, id) => app.call(`/api/memberships/${id}/table`, { cookie });

test('a member reads the game’s live table, including changes made after they joined', async () => {
    const { app, gm, player, membership } = await aTable();

    // The control: the route answers the campaign as it stood at the join, so the assertion
    // below is about a *change* arriving rather than about a route that answers anything at all.
    const atTheJoin = await liveTable(app, player.cookie, membership);

    assert.equal(atTheJoin.status, 200);
    assert.equal((await atTheJoin.json()).payload, JSON.stringify(campaignPayload));

    // The GM raises the price of Immortality and turns a rule on, long after the character joined.
    // Nothing writes that onto the character — that is the whole of item 30 — and this is the read
    // that lets a screen say so.
    const moved = JSON.stringify({
        Version: 1,
        Campaign: {
            ...campaignPayload.Campaign,
            ImmortalityCost: 12,
            Table: { FatalDamage: true },
        },
    });

    assert.equal((await app.call(`/api/campaigns/${gid()}`, {
        method: 'PUT', body: { label: 'Nightfall', payload: moved }, cookie: gm.cookie,
    })).status, 204);

    const now = await liveTable(app, player.cookie, membership);
    const seen = await now.json();

    assert.equal(now.status, 200);
    assert.equal(seen.campaignId, gid());

    // Byte for byte, because the server does not parse it — the same claim the join answer makes.
    assert.equal(seen.payload, moved);
});

test('the live table answers the campaign and nothing else, by key set', async () => {
    const { app, gm, player, membership } = await aTable();

    // A second player in the same campaign, with a submission waiting, so that "nothing about
    // another member" is a claim about something that exists rather than about an empty table.
    const other = await signIn(app, 'other@example.test');
    const code = await joinCodeFor(app, gm.cookie);
    const joined = await join(app, other.cookie,
        { code, characterId: cid(7), label: 'Someone Else' });

    assert.equal(joined.status, 200);
    assert.equal((await submit(app, other.cookie, (await joined.json()).id,
        { payload: characterPayload('Someone Else'), label: 'Someone Else' })).status, 200);

    const response = await liveTable(app, player.cookie, membership);
    const seen = await response.json();

    // **The key set, exactly.** Asserted as a set rather than as a handful of absences, because an
    // absence is satisfied by a field nobody thought to name — and the fields worth withholding
    // here are the ones a later hand adds for convenience: an account id to save a lookup, the
    // label to save a second read, the join code to save reading it out.
    assert.deepEqual(Object.keys(seen).sort(), ['campaignId', 'payload']);

    const body = await (await liveTable(app, player.cookie, membership)).text();

    namesNoAccount(body);
    assert.ok(!body.includes('gm@example.test'), body);
    assert.ok(!body.includes('other@example.test'), body);
    assert.ok(!body.includes('Someone Else'), `the live table names another member: ${body}`);
    assert.ok(!body.includes('AbilityRanks'), `the live table carries a character: ${body}`);
    assert.ok(!body.includes(cid(7)), body);

    // And the join code, which is the campaign's one readable column and is nowhere in the payload.
    assert.ok(!body.includes(code), `the live table leaks the join code: ${body}`);
});

test('only the member reads it: a stranger, the GM of that very row, and nobody at all are refused',
    async () => {
        const { app, gm, player, membership } = await aTable();
        const stranger = await signIn(app, 'stranger@example.test');

        // The positive control, first, so the three refusals below are being compared against a
        // read that works rather than against a route that answers nobody.
        assert.equal((await liveTable(app, player.cookie, membership)).status, 200);

        assert.equal((await liveTable(app, stranger.cookie, membership)).status, 404,
            'the `m.player_user_id = ?` half of `campaignForMember` let a stranger in');

        // **The GM is refused their own campaign here**, which is deliberate: they read it at
        // `/api/campaigns/{id}`, and a second address answering the owner is a second place that
        // could disagree about what a campaign is. The 404 is the same one the stranger gets, so
        // the answer says nothing about which of the two the caller is.
        assert.equal((await liveTable(app, gm.cookie, membership)).status, 404,
            'the `m.player_user_id = ?` half of `campaignForMember` answered the GM of that row');

        assert.equal((await app.call(`/api/memberships/${membership}/table`)).status, 401);
        assert.equal((await liveTable(app, player.cookie, 'm_short')).status, 400);
        assert.equal((await liveTable(app, player.cookie, mid(9))).status, 404,
            'the `m.id = ?` half of `campaignForMember` answered a membership id nobody holds');
    });

test('a campaign the GM deleted answers the same refusal as one that was never the caller’s',
    async () => {
        const { app, gm, player, membership } = await aTable();

        assert.equal((await liveTable(app, player.cookie, membership)).status, 200);
        assert.equal((await app.call(`/api/campaigns/${gid()}`,
            { method: 'DELETE', cookie: gm.cookie })).status, 204);

        // The membership survives on purpose — the player's row is theirs and restoring the
        // campaign is a complete undo — so the standing is still listed and only the table is gone.
        assert.equal((await liveTable(app, player.cookie, membership)).status, 404);
        assert.ok(await playerRow(app, player.cookie, membership),
            'deleting the campaign took the player’s own membership with it');
    });

test('a campaign id shared by two GMs answers each member the one they joined', async () => {
    // **The clause under test is `c.user_id = m.gm_user_id` in the join.** A `g_…` is unique per
    // account and not globally — the schema says so, and a campaign's id reaches every member of
    // it — so matching on the id alone would hand a player the table of a campaign belonging to
    // an account they never joined, live, on every render of their own campaigns page.
    //
    // **Both directions are asserted, and that is what makes this fixture a check rather than a
    // coin flip.** Drop the clause and the join matches *two* `campaigns` rows for either
    // membership, because the only surviving predicate is on `c.id`; `.first()` then hands back
    // whichever row the planner reaches first — the same one for both memberships, since nothing
    // left in the statement distinguishes them. So one of the two players below is answered the
    // other GM's table whichever way that falls, and the mutation cannot survive by being lucky
    // about insertion order. The single-player version of this test passed the mutation.
    //
    // Mallory's campaign is written *first* on top of that, so the wrong row also leads under
    // every plausible ordering — rowid, and `updated_at`.
    const app = server();

    const theTables = async (cookie, name) => {
        assert.equal((await app.call(`/api/campaigns/${gid()}`, {
            method: 'PUT',
            body: {
                label: `${name}’s game`,
                payload: JSON.stringify({
                    Version: 1,
                    Campaign: { ...campaignPayload.Campaign, Name: `${name}’s game` },
                }),
            },
            cookie,
        })).status, 204);

        return await joinCodeFor(app, cookie);
    };

    const mallory = await signIn(app, 'mallory@example.test');
    const malloryCode = await theTables(mallory.cookie, 'Mallory');

    const alice = await signIn(app, 'alice@example.test');
    const aliceCode = await theTables(alice.cookie, 'Alice');

    /** One player, joined to one of the two campaigns sharing an id, reading their own table. */
    const memberOf = async (address, code) => {
        const player = await signIn(app, address);
        const joined = await join(app, player.cookie, { code });

        assert.equal(joined.status, 200);

        const response = await liveTable(app, player.cookie, (await joined.json()).id);

        assert.equal(response.status, 200);

        return await response.json();
    };

    const hers = await memberOf('alices-player@example.test', aliceCode);
    const his = await memberOf('mallorys-player@example.test', malloryCode);

    // The distinguishing byte is the campaign's own name, which is the one field of these two
    // payloads that differs — the ids are equal on purpose, which is the whole fixture.
    assert.equal(hers.campaignId, gid());
    assert.equal(his.campaignId, gid());

    assert.ok(hers.payload.includes('Alice\u2019s game'),
        `a member of Alice’s game was answered: ${hers.payload}`);
    assert.ok(!hers.payload.includes('Mallory'),
        `the live table answered a campaign belonging to a GM this player never joined: ${hers.payload}`);

    assert.ok(his.payload.includes('Mallory\u2019s game'),
        `a member of Mallory’s game was answered: ${his.payload}`);
    assert.ok(!his.payload.includes('Alice'),
        `the live table answered a campaign belonging to a GM this player never joined: ${his.payload}`);
});

// ── Addresses ───────────────────────────────────────────────────────────────────────────

test('an id this server does not use is refused before any query', async () => {
    const { app, player } = await aTable();

    for (const bad of ['nope', 'c_0000000000000000000000', 'g_0000000000000000000000', 'm_short']) {
        assert.equal((await app.call(`/api/memberships/${bad}`, { cookie: player.cookie })).status,
            400, `'${bad}' should not be readable as a membership id`);
    }
});

test('an unrouted address under the prefix is a 404, not a malformed id', async () => {
    const { app, player, membership } = await aTable();

    assert.equal((await app.call(`/api/memberships/${membership}/nonsense`,
        { cookie: player.cookie })).status, 404);
    assert.equal((await app.call(`/api/campaigns/${gid()}/nonsense`,
        { cookie: player.cookie })).status, 404);
});

test('the wrong method is refused on every membership address', async () => {
    const { app, player, membership } = await aTable();

    for (const [method, path] of [
        ['POST', '/api/memberships'],
        ['POST', '/api/memberships/inbox'],
        ['GET', '/api/memberships/join'],
        ['PATCH', `/api/memberships/${membership}`],
        ['POST', `/api/memberships/${membership}/submission`],
        ['PUT', `/api/memberships/${membership}/approve`],
        ['POST', `/api/memberships/${membership}/table`],
        ['GET', `/api/campaigns/${gid()}/code`],
    ]) {
        const response = await app.call(path, { method, body: method === 'GET' ? undefined : {} });
        const authorised = await app.call(path,
            { method, body: method === 'GET' ? undefined : {}, cookie: player.cookie });

        assert.equal(response.status, 401, `${method} ${path} answered ${response.status} signed out`);
        assert.equal(authorised.status, 405, `${method} ${path} answered ${authorised.status}`);
    }
});

// ── What a decision leaves behind ────────────────────────────────────────────────────────
//
// **Approving and rejecting were indistinguishable from the player's side, and that is the defect
// `0007` exists for.** Approve moves the pending slot into the approved one; Reject clears the
// pending slot and leaves the clone. Both then leave two booleans in a state the player has
// already seen — so a rejection reverted their standing to the identical sentence it showed
// before they sent anything, and nothing said a decision had been made at all.

/** The player's own row for a membership, which is where a standing is read from. */
async function playerRow(app, cookie, id) {
    const listed = await (await app.call('/api/memberships', { cookie })).json();

    return listed.memberships.find(m => m.id === id);
}

/** The GM's row for the same membership. */
async function gmRow(app, cookie, id) {
    const listed = await (await app.call('/api/memberships/inbox', { cookie })).json();

    return listed.memberships.find(m => m.id === id);
}

test('a rejection is a fact on the row, not only the absence of an approval', async () => {
    const { app, gm, player, membership } = await aTable();

    // The control: nothing decided yet, and it says so rather than defaulting to either word.
    assert.equal((await playerRow(app, player.cookie, membership)).decision, null);

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 1)).status, 204);

    const after = await playerRow(app, player.cookie, membership);

    assert.equal(after.decision, 'rejected');
    assert.equal(after.hasApproved, false, 'rejecting must not write a clone');
    assert.equal(after.hasPending, false, 'the snapshot is gone either way');
});

test('a rejection after an approval is told apart from the approval', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    const approved = await playerRow(app, player.cookie, membership);

    assert.equal(approved.decision, 'approved');
    assert.equal(approved.hasApproved, true);

    // **The case the whole migration is for.** The clone stays exactly where it was, so every
    // other field on this row comes back reading precisely as it did a moment ago.
    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 2)).status, 204);

    const turnedDown = await playerRow(app, player.cookie, membership);

    assert.equal(turnedDown.hasApproved, approved.hasApproved);
    assert.equal(turnedDown.hasPending, approved.hasPending);
    assert.equal(turnedDown.approvedAt, approved.approvedAt,
        'rejecting must not move the time the clone was accepted');

    // One field differs, and it is the only thing standing between these two states.
    assert.equal(turnedDown.decision, 'rejected');
});

test('the GM sees the last decision too, because both rows land in one record', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 1)).status, 204);

    const row = await gmRow(app, gm.cookie, membership);

    assert.equal(row.decision, 'rejected');

    // And still no account id or address anywhere in the GM's half.
    const body = JSON.stringify(row);

    assert.ok(!body.includes('player@example.test'), body);
    namesNoAccount(body);
});

test('resubmitting shadows the last decision rather than clearing it', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 1)).status, 204);
    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    const resent = await playerRow(app, player.cookie, membership);

    // **Nothing clears it and nothing needs to.** A waiting snapshot is the live fact and the
    // browser reads it first; the decision is still the last one made, which is what it says.
    assert.equal(resent.hasPending, true);
    assert.equal(resent.decision, 'rejected');

    // And the next decision overwrites it.
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 2)).status, 204);
    assert.equal((await playerRow(app, player.cookie, membership)).decision, 'approved');
});

test('a refused decision writes no decision at all', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    // The compare-and-swap refuses this, and a refusal that recorded a decision would tell the
    // player their change was turned down by a GM who never got to decide.
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 99)).status, 409);

    const after = await playerRow(app, player.cookie, membership);

    assert.equal(after.decision, null);
    assert.equal(after.hasPending, true, 'the snapshot is still waiting');
});

test('a rejoined membership starts with nothing decided', async () => {
    const { app, gm, player, code, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'reject', 1)).status, 204);
    assert.equal((await leave(app, player.cookie, membership)).status, 204);

    const again = (await (await join(app, player.cookie, { code })).json()).id;

    assert.equal((await playerRow(app, player.cookie, again)).decision, null,
        'a fresh row must not inherit the decision made about the one that was left');
});

// ── Ending a membership ─────────────────────────────────────────────────────────────────
//
// **Both sides can end one, and each reaches the row by its own column** — which is what makes a
// third account able to end nothing. The row goes and the campaign's clone goes with it: deleting
// a *campaign* keeps its memberships so that writing it back is a complete undo, and ending a
// *membership* is the opposite act with no undo behind it.

const leave = (app, cookie, id) =>
    app.call(`/api/memberships/${id}`, { method: 'DELETE', cookie });

test('a player can leave, and the campaign stops holding their character', async () => {
    const { app, gm, player, membership } = await aTable();

    const sent = await submit(app, player.cookie, membership);
    assert.equal(sent.status, 200);
    assert.equal((await decide(app, gm.cookie, membership, 'approve', 1)).status, 204);

    // The positive control, at both ends: the GM holds a clone and the player has a standing.
    const before = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(before.memberships.length, 1);
    assert.equal(before.memberships[0].hasApproved, true);

    assert.equal((await leave(app, player.cookie, membership)).status, 204);

    // Gone from both halves, and the clone with it — there is no row left to hold one.
    const after = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(after.memberships.length, 0);

    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
    assert.equal(mine.memberships.length, 0);

    assert.equal((await app.call(`/api/memberships/${membership}`,
        { cookie: gm.cookie })).status, 404);
    assert.equal((await app.call(`/api/memberships/${membership}`,
        { cookie: player.cookie })).status, 404);
});

test('a GM can remove a player, reaching the same row by the other column', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    // The control: it is in the GM's inbox to lose.
    const before = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(before.memberships.length, 1);

    assert.equal((await leave(app, gm.cookie, membership)).status, 204);

    const after = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(after.memberships.length, 0);

    // And the player's half goes too. Unlike a deleted campaign, this is not an undoable act
    // held open on one side: the membership is over for both of them.
    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
    assert.equal(mine.memberships.length, 0);
});

test('a third account ends nothing, and learns nothing by asking', async () => {
    const { app, membership } = await aTable();

    const stranger = await signIn(app, 'stranger@example.test');

    assert.equal((await leave(app, stranger.cookie, membership)).status, 204);

    // The membership is still there, which is the whole point: the answer above says the caller
    // has no such row, not that the row is gone.
    const still = await (await app.call('/api/memberships/inbox',
        { cookie: (await signIn(app, 'gm@example.test')).cookie })).json();

    assert.equal(still.memberships.length, 1);
});

test('leaving twice is not an error, and neither is leaving what was never there', async () => {
    const { app, player, membership } = await aTable();

    assert.equal((await leave(app, player.cookie, membership)).status, 204);

    // The end state the caller asked for is "that membership is not there", and it is not — the
    // same reasoning `join` records for a second join with the same code. It is also why a row
    // belonging to somebody else answers identically: a 404-or-204 split would say whether an id
    // exists.
    assert.equal((await leave(app, player.cookie, membership)).status, 204);
    assert.equal((await leave(app, player.cookie, mid(7))).status, 204);

    // A malformed id is still a 400 — that is a fact about the request, not about a row.
    assert.equal((await leave(app, player.cookie, 'not-an-id')).status, 400);
});

test('a GM who deleted the game cannot remove its members, and is told why', async () => {
    const { app, gm, player, membership } = await aTable();

    assert.equal((await app.call(`/api/campaigns/${gid()}`,
        { method: 'DELETE', cookie: gm.cookie })).status, 204);

    // **Not 204.** The player's row deliberately outlives the campaign so that writing the
    // campaign back is a complete undo, and `removeMember`'s `EXISTS` is what keeps that whole —
    // so a removal that did not happen must not be reported as one.
    const refused = await leave(app, gm.cookie, membership);

    assert.equal(refused.status, 409);
    assert.match((await refused.json()).error, /no longer here/);

    // The player still has their row, and can still walk out of it themselves.
    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
    assert.equal(mine.memberships.length, 1);

    assert.equal((await leave(app, player.cookie, membership)).status, 204);
});

test('a player who left can rejoin with the same code, on a fresh membership', async () => {
    const { app, player, code, membership } = await aTable();

    assert.equal((await leave(app, player.cookie, membership)).status, 204);

    // The unique index over (gm, campaign, player, character) is free again, so this is an insert
    // rather than the `ON CONFLICT … DO UPDATE` that answers an existing row.
    const again = await join(app, player.cookie, { code });

    assert.equal(again.status, 200);

    const rejoined = (await again.json()).id;

    assert.notEqual(rejoined, membership, 'rejoining answered the row that was deleted');

    // And it starts clean: no clone, nothing waiting, version back at zero.
    const read = await (await app.call(`/api/memberships/${rejoined}`,
        { cookie: player.cookie })).json();

    assert.equal(read.approved, null);
    assert.equal(read.pending, null);
    assert.equal(read.pendingVersion, 0);
});

test('ending one membership leaves every other one alone', async () => {
    const { app, gm, player, membership } = await aTable();

    // A second character of the same player, in the same game.
    const second = await join(app, player.cookie,
        { code: await joinCodeFor(app, gm.cookie), characterId: cid(1), label: 'Second' });

    assert.equal(second.status, 200);

    const other = (await second.json()).id;

    assert.equal((await leave(app, player.cookie, membership)).status, 204);

    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();

    assert.equal(mine.memberships.length, 1);
    assert.equal(mine.memberships[0].id, other);
    assert.equal(mine.memberships[0].characterId, cid(1));
});

test('a state-changing membership request from another origin is refused', async () => {
    const { app, gm, player, membership } = await aTable();

    for (const [cookie, method, path] of [
        [player.cookie, 'POST', '/api/memberships/join'],
        [player.cookie, 'PUT', `/api/memberships/${membership}/submission`],
        [gm.cookie, 'POST', `/api/memberships/${membership}/approve`],
        [gm.cookie, 'POST', `/api/memberships/${membership}/reject`],
        [player.cookie, 'DELETE', `/api/memberships/${membership}`],
        [gm.cookie, 'DELETE', `/api/memberships/${membership}`],
        [gm.cookie, 'POST', `/api/campaigns/${gid()}/code`],
    ]) {
        const response = await app.call(path,
            { method, body: {}, cookie, origin: 'https://elsewhere.example' });

        assert.equal(response.status, 403, `${method} ${path} answered ${response.status}`);
    }
});

test('sweeping the join codes is bounded', async () => {
    const app = server();
    const player = await signIn(app, 'player@example.test');

    // Twenty are allowed and the twenty-first is not. Asserted on both sides, because a limit
    // that refused the first attempt would satisfy an "eventually refused" assertion for free.
    for (let i = 0; i < 20; i++) {
        assert.equal((await join(app, player.cookie, { code: 'ABCDE-FGHJK' })).status, 404,
            `attempt ${i + 1} should have reached the lookup`);
    }

    assert.equal((await join(app, player.cookie, { code: 'ABCDE-FGHJK' })).status, 429);

    // And the window rolls: an hour later the account may ask again.
    app.now += 61 * 60 * 1000;
    assert.equal((await join(app, player.cookie, { code: 'ABCDE-FGHJK' })).status, 404);
});

test('two GMs may share a campaign id, and a join lands in the campaign whose code was redeemed', async () => {
    // **A `g_…` is unique per account, not globally.** `campaigns` is `PRIMARY KEY (user_id, id)`,
    // and the id is the client's — so two GMs holding the same one is a state the schema allows
    // and a state an attacker can arrange, because a campaign's id is handed to every member of
    // it in the join response and travels in an exported character's own `CampaignId`.
    //
    // The defect this guards: with `gm_user_id` out of `campaign_members_one_per_character`, the
    // player's second join conflicted with their row in the *first* campaign, `ON CONFLICT … DO
    // UPDATE` handed that row back, and every snapshot they sent afterwards was delivered to a GM
    // they never joined — while the GM whose code they redeemed saw an empty inbox.
    const app = server();

    const alice = await signIn(app, 'alice@example.test');
    assert.equal((await putCampaign(app, alice.cookie, { label: 'Alice’s game' })).status, 204);
    const aliceCode = await joinCodeFor(app, alice.cookie);

    const mallory = await signIn(app, 'mallory@example.test');
    assert.equal((await putCampaign(app, mallory.cookie, { label: 'Mallory’s game' })).status, 204,
        'campaign ids are per-account, so the same id in a second account is an ordinary write');
    const malloryCode = await joinCodeFor(app, mallory.cookie);

    assert.notEqual(aliceCode, malloryCode, 'two campaigns must never share a code');

    const player = await signIn(app, 'player@example.test');

    const first = await join(app, player.cookie, { code: aliceCode });
    assert.equal(first.status, 200);
    const inAlice = (await first.json()).id;

    const second = await join(app, player.cookie, { code: malloryCode });
    assert.equal(second.status, 200);
    const inMallory = (await second.json()).id;

    // The whole of it: two campaigns, two memberships.
    assert.notEqual(inMallory, inAlice,
        'the second join answered a membership of the first GM’s campaign');

    // And the snapshot goes where the player thinks it goes. Asserted through the inboxes rather
    // than through the id alone, because the id being different is the mechanism and this is the
    // consequence — a mechanism that stopped producing it would still satisfy the line above.
    assert.equal((await submit(app, player.cookie, inMallory)).status, 200);

    const mallorysInbox = await (await app.call('/api/memberships/inbox', { cookie: mallory.cookie })).json();
    const alicesInbox = await (await app.call('/api/memberships/inbox', { cookie: alice.cookie })).json();

    assert.deepEqual(mallorysInbox.memberships.map(m => [m.id, m.hasPending]), [[inMallory, true]]);
    assert.deepEqual(alicesInbox.memberships.map(m => [m.id, m.hasPending]), [[inAlice, false]]);
});

test('a deleted campaign takes the GM’s half of every membership with it, and leaves the player’s', async () => {
    // **The asymmetry is the design, not an oversight.** There is no cascade and there is not
    // going to be one: the player's row is theirs, and keeping it is what makes restoring a
    // campaign a complete undo. But a GM who deleted a game was still being shown a request
    // waiting on it, with an Approve button under it — a decision surface for a queue that no
    // longer means anything — and the player could go on sending snapshots into it and be told
    // they had been sent.
    const { app, gm, player, membership } = await aTable();

    assert.equal((await submit(app, player.cookie, membership)).status, 200);

    const before = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.equal(before.memberships.length, 1, 'the positive control: it was there to lose');

    assert.equal((await app.call(`/api/campaigns/${gid()}`,
        { method: 'DELETE', cookie: gm.cookie })).status, 204);

    // The GM's half is gone: no inbox row, no detail read, and no decision to make.
    const after = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.deepEqual(after.memberships, []);

    assert.equal((await app.call(`/api/memberships/${membership}`, { cookie: gm.cookie })).status, 404);

    for (const what of ['approve', 'reject']) {
        assert.equal((await decide(app, gm.cookie, membership, what, 1)).status, 404,
            `${what} still reached a campaign that is not there`);
    }

    // The player's half survives, and says what actually happened rather than 404-ing at them.
    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
    assert.equal(mine.memberships.length, 1, 'the player’s own row is theirs and is untouched');

    assert.equal((await app.call(`/api/memberships/${membership}`, { cookie: player.cookie })).status, 200);

    const refused = await submit(app, player.cookie, membership);
    assert.equal(refused.status, 409);
    assert.match((await refused.json()).error, /no longer here/);

    // And it is an undo: writing the campaign back brings the whole thing back, snapshot and all.
    assert.equal((await putCampaign(app, gm.cookie)).status, 204);

    const restored = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.deepEqual(restored.memberships.map(m => [m.id, m.hasPending, m.pendingVersion]),
        [[membership, true, 1]]);
});

test('deleting one of a GM’s two campaigns takes only that one’s half', async () => {
    // **The test above cannot see the half of this rule that says *only* that one.** Its GM owns
    // exactly one campaign, so `EXISTS (SELECT 1 FROM campaigns WHERE user_id = gm_user_id)` and
    // `EXISTS (... AND id = campaign_id)` are the same predicate on that fixture: with one
    // campaign, "this GM still owns a campaign" and "this GM still owns *this* campaign" agree on
    // every row. Dropping the `AND c.id = campaign_members.campaign_id` correlation from
    // `getMembership` therefore left all 230 tests green while a GM with two games could read the
    // clone and the waiting snapshot out of the one they had thrown away.
    //
    // That is this repository's recurring failure exactly — a fixture that cannot reach the
    // behaviour under test — and the fix is a second campaign, not a longer assertion.
    const app = server();
    const gm = await signIn(app, 'gm@example.test');

    assert.equal((await putCampaign(app, gm.cookie, { theId: gid(1), label: 'Nightfall' })).status, 204);
    assert.equal((await putCampaign(app, gm.cookie, { theId: gid(2), label: 'Daybreak' })).status, 204);

    const player = await signIn(app, 'player@example.test');

    const joinedDoomed = await join(app, player.cookie,
        { code: await joinCodeFor(app, gm.cookie, gid(1)), characterId: cid(1) });
    const joinedKept = await join(app, player.cookie,
        { code: await joinCodeFor(app, gm.cookie, gid(2)), characterId: cid(2) });

    assert.equal(joinedDoomed.status, 200);
    assert.equal(joinedKept.status, 200);

    const doomed = (await joinedDoomed.json()).id;
    const kept = (await joinedKept.json()).id;

    assert.notEqual(doomed, kept, 'two campaigns produced one membership, so this proves nothing');

    assert.equal((await submit(app, player.cookie, doomed)).status, 200);
    assert.equal((await submit(app, player.cookie, kept)).status, 200);

    // The positive control: both are in the inbox before either campaign goes.
    const before = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.deepEqual(before.memberships.map(m => m.id).sort(), [doomed, kept].sort());

    assert.equal((await app.call(`/api/campaigns/${gid(1)}`,
        { method: 'DELETE', cookie: gm.cookie })).status, 204);

    // Only the deleted campaign's half goes.
    const after = await (await app.call('/api/memberships/inbox', { cookie: gm.cookie })).json();
    assert.deepEqual(after.memberships.map(m => m.id), [kept],
        'the surviving campaign’s membership went with the deleted one, or the deleted one stayed');

    assert.equal((await app.call(`/api/memberships/${doomed}`, { cookie: gm.cookie })).status, 404,
        'the GM read a clone out of a campaign they deleted, because another campaign of theirs exists');
    assert.equal((await app.call(`/api/memberships/${kept}`, { cookie: gm.cookie })).status, 200);

    for (const what of ['approve', 'reject']) {
        assert.equal((await decide(app, gm.cookie, doomed, what, 1)).status, 404,
            `${what} reached a membership whose campaign is gone`);
    }

    // And the surviving one is still decidable, so the refusals above are about the deletion
    // rather than about decisions having stopped working.
    assert.equal((await decide(app, gm.cookie, kept, 'approve', 1)).status, 204);

    // The player's half is untouched on both, deleted campaign included.
    const mine = await (await app.call('/api/memberships', { cookie: player.cookie })).json();
    assert.deepEqual(mine.memberships.map(m => m.id).sort(), [doomed, kept].sort());

    const refused = await submit(app, player.cookie, doomed);
    assert.equal(refused.status, 409);
    assert.match((await refused.json()).error, /no longer here/);

    assert.equal((await submit(app, player.cookie, kept)).status, 200);
});
