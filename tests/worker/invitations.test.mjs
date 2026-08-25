// Who may have an account here, and who may decide.
//
// **This is the only file that turns the allow-list on.** The harness invites the address a
// request names, because nearly every other test predates the list and is about something else;
// so the first test below is the positive control for that scaffolding, and everything after it
// passes `gated: true` and means what it says.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { INVITATION_TOKEN_LIFETIME_MS, TOKEN_LIFETIME_MS } from '../../worker/auth.js';
import { cookieFrom, errorRows, server, signIn, tokenFrom } from './harness.mjs';

const ADMIN = 'boss@example.test';

/** A server with the gate on and one bootstrap administrator, as a deployment has. */
const gated = (options = {}) => server({ gated: true, admin: ADMIN, ...options });

const ask = (app, email) => app.call('/api/auth/request', { method: 'POST', body: { email } });

/** Sign in as somebody the gate really allows, and hand back their cookie. */
async function enter(app, email) {
    const asked = await ask(app, email);
    assert.equal(asked.status, 204, 'could not ask for a link');

    const verified = await app.call('/api/auth/verify',
        { method: 'POST', body: { token: tokenFrom(app.sent) } });
    assert.equal(verified.status, 200, 'could not spend the link');

    return cookieFrom(verified);
}

test('the harness’s open door is really a door', async () => {
    // **Without this every test in this suite could be passing for the wrong reason.** The
    // harness invites the address a link is asked for, so that tests about the rate limit and the
    // token's lifetime need not know the list exists. If that scaffolding ever stopped mattering
    // — because the gate stopped working — nothing else here would notice.
    const open = server();
    await ask(open, 'anyone@example.test');
    assert.equal(open.sent.length, 1, 'the harness no longer lets an uninvited address through');

    const shut = gated();
    await ask(shut, 'anyone@example.test');
    assert.equal(shut.sent.length, 0, 'the gate let an uninvited address through');
});

test('an address nobody invited is answered exactly like one that was', async () => {
    // The whole reason this refusal is silent: an endpoint that said "not on the list" would be a
    // way of asking who is, one address at a time — and the list is short, private, and the only
    // thing standing between a stranger and the rulebook's text.
    const app = gated();
    app.invite('invited@example.test');

    const yes = await ask(app, 'invited@example.test');
    const no = await ask(app, 'stranger@example.test');

    assert.equal(yes.status, no.status);
    assert.equal(await yes.text(), await no.text());
    assert.equal(app.sent.length, 1, 'the invited address is the only one mailed');
    assert.equal(app.sent[0].to, 'invited@example.test');
});

test('the bootstrap administrator needs no row, and a deployment with neither allows nobody', async () => {
    const app = gated();

    await ask(app, ADMIN);
    assert.equal(app.sent.length, 1, 'the address in ADMIN_EMAIL was refused');

    // The safe direction for a fork or a half-finished deployment: nothing configured allows
    // nobody, rather than everybody. A site that signs nobody in is visibly broken; one that lets
    // a stranger in is not.
    const unconfigured = server({ gated: true });
    await ask(unconfigured, 'anyone@example.test');
    await ask(unconfigured, ADMIN);
    assert.equal(unconfigured.sent.length, 0);
});

test('an address is invited however it was typed', async () => {
    // The gate is a lookup of one normalised address against another, so a capital letter on
    // either side is an invitation that exists and never matches — which reads from the outside
    // as somebody who was allowed and still cannot sign in.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const added = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: '  Guest@Example.test ' }, cookie });

    assert.equal(added.status, 200);
    assert.equal((await added.json()).invitation.email, 'guest@example.test');

    await ask(app, 'GUEST@example.TEST');
    assert.equal(app.sent.at(-1).to, 'guest@example.test');
});

test('a link stops working if the invitation goes while it is in flight', async () => {
    // Fifteen minutes is long enough to be withdrawn in, and the link is the credential. The
    // token is spent by the statement that reads it either way, so a withdrawn address cannot
    // hold a live link in reserve against being let back in.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    app.invite('guest@example.test');
    await ask(app, 'guest@example.test');
    const token = tokenFrom(app.sent);

    const list = await (await app.call('/api/admin/invitations', { cookie })).json();
    const guest = list.invitations.find(i => i.email === 'guest@example.test');

    assert.equal((await app.call(`/api/admin/invitations/${guest.id}`,
        { method: 'DELETE', cookie })).status, 204);

    const verified = await app.call('/api/auth/verify', { method: 'POST', body: { token } });

    assert.equal(verified.status, 401);
    assert.equal((await verified.json()).error, 'That sign-in link is not usable. Ask for another.',
        'the refusal must be the same sentence every other one on this route gives');
});

test('the administrator’s page is invisible to an ordinary account', async () => {
    // 404 rather than 403, and the same 404 an unrouted address gets: an account that is not an
    // administrator must not be able to learn that there is an administrator's page at all.
    const app = gated();
    app.invite('player@example.test');
    const ordinary = await enter(app, 'player@example.test');

    const refused = await app.call('/api/admin/invitations', { cookie: ordinary });
    const nowhere = await app.call('/api/no-such-thing', { cookie: ordinary });

    assert.equal(refused.status, 404);
    assert.deepEqual(await refused.json(), await nowhere.json());

    // Signed out is a different answer, because it is a different question.
    assert.equal((await app.call('/api/admin/invitations')).status, 401);

    // And every verb on it, so a write is not left reachable by a check that only guards reads.
    for (const [path, method] of [
        ['/api/admin/invitations', 'POST'],
        ['/api/admin/invitations/i_aaaaaaaaaaaaaaaaaaaaaa', 'DELETE'],
    ]) {
        const response = await app.call(path, { method, body: { email: 'x@y.test' }, cookie: ordinary });
        assert.equal(response.status, 404, `${method} ${path} was reachable`);
    }
});

test('the list carries the bootstrap administrator, marked as the one row nothing can remove', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();

    assert.equal(listed.you, ADMIN);

    const boss = listed.invitations.find(i => i.email === ADMIN);
    assert.ok(boss, 'the address that always works is missing from the list of who may sign in');
    assert.equal(boss.removable, false);
    assert.equal(boss.grantsAdmin, true);
    assert.equal(boss.id, null, 'a row with an id is a row this page would offer to withdraw');
});

test('an invitation is granted, used, and shows as used', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const added = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    assert.equal(added.status, 200);

    const invitation = (await added.json()).invitation;
    assert.match(invitation.id, /^i_[A-Za-z0-9_-]{22}$/);
    assert.equal(invitation.grantsAdmin, false);
    assert.equal(invitation.hasSignedIn, false);

    await enter(app, 'guest@example.test');

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    const guest = listed.invitations.find(i => i.email === 'guest@example.test');

    assert.equal(guest.hasSignedIn, true, 'an address that has become an account still shows as unused');
});

test('inviting the same address twice is not an error', async () => {
    // The end state asked for is "this address may sign in", and it may. A conflict here would
    // make the page's own list something to reconcile before every click.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const first = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const again = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const bootstrap = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: ADMIN }, cookie });

    assert.equal(first.status, 200);
    assert.equal(again.status, 200);
    assert.equal((await again.json()).alreadyAllowed, true);
    assert.equal(bootstrap.status, 200);
    assert.equal((await bootstrap.json()).alreadyAllowed, true);

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();

    assert.equal(listed.invitations.filter(i => i.email === 'guest@example.test').length, 1);
    assert.equal(listed.invitations.filter(i => i.email === ADMIN).length, 1,
        'the address that needs no row was given one');
});

test('adding an address mails it', async () => {
    // **What the message itself says is a `worker/mail.test.mjs` question**, the same split as
    // the sign-in link: this file drives the route and asserts a mail was attempted for the
    // right address and carried a link; the shape of the message body is asserted where the
    // builder actually runs, since the stub here stands in for the network call and never sees
    // the built body.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const added = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    assert.equal(added.status, 200);
    assert.equal((await added.json()).mailed, true);

    assert.equal(app.invitationsSent.length, 1, 'nothing was mailed, so the assertion below checks nothing');
    assert.equal(app.invitationsSent[0].to, 'guest@example.test');
    assert.ok(app.invitationsSent[0].link.includes('?t='), 'the invitation carries no token');
});

test('the invitation link signs the invited person in, in one click', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    const token = new URL(app.invitationsSent.at(-1).link).searchParams.get('t');
    const verified = await app.call('/api/auth/verify', { method: 'POST', body: { token } });

    assert.equal(verified.status, 200);
    assert.equal((await verified.json()).displayName, 'guest');

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    assert.equal(listed.invitations.find(i => i.email === 'guest@example.test')?.hasSignedIn, true);
});

test('an invitation link works once, the same as any other sign-in link', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const token = new URL(app.invitationsSent.at(-1).link).searchParams.get('t');

    const first = await app.call('/api/auth/verify', { method: 'POST', body: { token } });
    const second = await app.call('/api/auth/verify', { method: 'POST', body: { token } });

    assert.equal(first.status, 200);
    assert.equal(second.status, 401, 'the same invitation link signed somebody in twice');
});

test('an invitation link outlives the public request path’s fifteen minutes', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const token = new URL(app.invitationsSent.at(-1).link).searchParams.get('t');

    // Past the ordinary link's lifetime, and this one must still work — that is the entire
    // feature. `TOKEN_LIFETIME_MS` rather than a bare number, so this test breaks the moment the
    // two constants are made equal by accident rather than passing on a coincidence.
    app.now += TOKEN_LIFETIME_MS + 1;

    const verified = await app.call('/api/auth/verify', { method: 'POST', body: { token } });
    assert.equal(verified.status, 200,
        'an invitation link expired at the public request path’s shorter lifetime');
});

test('an invitation link still expires, after three days rather than fifteen minutes', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const token = new URL(app.invitationsSent.at(-1).link).searchParams.get('t');

    app.now += INVITATION_TOKEN_LIFETIME_MS + 1;

    const verified = await app.call('/api/auth/verify', { method: 'POST', body: { token } });
    assert.equal(verified.status, 401, 'an invitation link never expires');
});

test('a spent or expired invitation token is actually pruned, not only refused', async () => {
    // **`sweepExpired` was written for `login_tokens` in general and this confirms it also
    // covers the longer-lived row an invitation writes, rather than assuming a three-day
    // `expires_at` behaves the same as a fifteen-minute one.** The comparison is `expires_at <
    // now` regardless of how the row got that value, so nothing in `worker/db.js` needed to
    // change — this is the test that checks that rather than takes it on faith.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    const stillThere = app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens WHERE email = ?')
        .all('guest@example.test')[0].n;
    assert.equal(stillThere, 1, 'nothing was minted, so the assertions below check nothing');

    // The positive control: well within the three days, a sweep must not take it early.
    app.now += TOKEN_LIFETIME_MS + 1;
    await ask(app, 'nobody@example.test');
    assert.equal(
        app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens WHERE email = ?')
            .all('guest@example.test')[0].n,
        1, 'a sweep pruned an invitation link well inside its three days');

    // Past the three days, the same sweep — reached by any request for a link — removes it.
    app.now += INVITATION_TOKEN_LIFETIME_MS;
    await ask(app, 'nobody@example.test');
    assert.equal(
        app.db.raw.prepare('SELECT COUNT(*) AS n FROM login_tokens WHERE email = ?')
            .all('guest@example.test')[0].n,
        0, 'an expired invitation token was never pruned');
});

test('an invitation mail carries the token; the database keeps only its hash', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const token = new URL(app.invitationsSent.at(-1).link).searchParams.get('t');

    assert.ok(token.length > 0, 'nothing was minted, so the assertion below checks nothing');

    // Scoped to the guest's row rather than the whole table, which also holds the one minted for
    // ADMIN's own sign-in above.
    const stored = app.db.raw.prepare('SELECT token_hash FROM login_tokens WHERE email = ?')
        .all('guest@example.test');

    assert.equal(stored.length, 1, 'nothing was stored for the invited address');
    assert.notEqual(stored[0].token_hash, token, 'the raw token reached the database');
    assert.ok(!JSON.stringify(stored).includes(token), 'the raw token appears somewhere in storage');
});

test('an address already on the list is not mailed again', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    assert.equal(app.invitationsSent.length, 1, 'the first add did not mail, so this checks nothing');

    const again = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    assert.equal((await again.json()).alreadyAllowed, true);
    assert.equal(app.invitationsSent.length, 1, 'an address already on the list was mailed again');

    const bootstrap = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: ADMIN }, cookie });

    assert.equal((await bootstrap.json()).alreadyAllowed, true);
    assert.equal(app.invitationsSent.length, 1, 'the bootstrap administrator was mailed');
});

test('a mail failure does not cost the invitation, and says so', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    app.deps = {
        ...app.deps,
        sendInvitationMail: async () => { throw new Error('The mail provider refused the send (HTTP 500).'); },
    };

    const added = await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    // **Still a 200, and still on the list.** The row is what grants the permission; a broken
    // mail provider must not read to the administrator as "nothing happened", and must not take
    // back a permission that has already been granted.
    assert.equal(added.status, 200);
    assert.equal((await added.json()).mailed, false);

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    assert.ok(listed.invitations.some(i => i.email === 'guest@example.test'),
        'a failed mail took the invitation with it');

    // And the address really can sign in — the row, not the mail, is what grants that. Counted
    // from a baseline rather than asserted at 1, because entering as ADMIN above already sent
    // one sign-in link of its own.
    const before = app.sent.length;
    await ask(app, 'guest@example.test');
    assert.equal(app.sent.length, before + 1, 'the address that could not be mailed also cannot sign in');
});

test('a mail failure while adding is recorded, the same as any other mail outage', async () => {
    // **Deliberately recorded, and this is the decision worth stating.** A broken provider or a
    // bad key breaks every sign-in the same way, silently, until somebody notices an inbox
    // stayed empty — which is the exact failure `docs/ACCOUNTS-SETUP.md` describes costing this
    // deployment its first sign-in. Writing a row here means the owner can find that fault from
    // an invitation that never delivered, not only from a visitor who complained about it.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    app.deps = {
        ...app.deps,
        sendInvitationMail: async () => { throw new Error('Resend refused to send (HTTP 422, validation_error).'); },
    };

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    const rows = errorRows(app.db);

    // The positive control: an absence is satisfied by a logger that writes nothing at all.
    assert.equal(rows.length, 1, 'nothing was recorded, so the assertions below check nothing');
    assert.equal(rows[0].category, 'mail');
    assert.equal(rows[0].route, '/api/admin/invitations');
    assert.match(rows[0].detail, /Resend refused to send/);
    assert.ok(!rows[0].detail.includes('guest@example.test'), rows[0].detail);
});

test('what is not an address is refused, and nothing is written', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    for (const email of ['', 'nobody', 'two@at@signs.test', 'has space@example.test', '@example.test', 42]) {
        const response = await app.call('/api/admin/invitations',
            { method: 'POST', body: { email }, cookie });

        assert.equal(response.status, 400, `${JSON.stringify(email)} was accepted`);
    }

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    assert.equal(listed.invitations.length, 1, 'only the bootstrap administrator should be listed');
});

test('an invitation can carry the right to manage the list', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'deputy@example.test', grantsAdmin: true }, cookie });
    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });

    const deputy = await enter(app, 'deputy@example.test');
    const guest = await enter(app, 'guest@example.test');

    assert.equal((await app.call('/api/admin/invitations', { cookie: deputy })).status, 200);
    assert.equal((await app.call('/api/admin/invitations', { cookie: guest })).status, 404);
});

test('withdrawing an invitation closes the door that is already open', async () => {
    // **Deleting the row alone would be a gesture.** It stops the next link and does nothing
    // about the month-long cookie the person is holding, so somebody removed from the list would
    // go on reading the rulebook until it expired.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const guest = await enter(app, 'guest@example.test');

    // A character of theirs, to prove the next assertion is about something.
    const id = 'c_' + 'a'.repeat(22);
    assert.equal((await app.call(`/api/characters/${id}`, {
        method: 'PUT', cookie: guest, body: { payload: '{"v":1}', label: 'Theirs' },
    })).status, 204);

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    const row = listed.invitations.find(i => i.email === 'guest@example.test');

    assert.equal((await app.call(`/api/admin/invitations/${row.id}`,
        { method: 'DELETE', cookie })).status, 204);

    assert.equal((await app.call('/api/me', { cookie: guest })).status, 401,
        'the session the withdrawn address was holding is still live');

    // Counted rather than read off the last message: nothing new being sent leaves the
    // *previous* link to this address sitting at the end of the list, which reads as a pass to
    // an assertion on the last message sent, and was one until this comment was written.
    const before = app.sent.length;
    await ask(app, 'guest@example.test');
    assert.equal(app.sent.length, before, 'a withdrawn address was mailed a link');

    // **And their work is still there.** Withdrawing permission to sign in is not the same act as
    // destroying somebody's characters, and conflating the two would make this the most dangerous
    // button in the application. Letting them back in gives them back everything.
    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'guest@example.test' }, cookie });
    const returned = await enter(app, 'guest@example.test');

    const theirs = await app.call(`/api/characters/${id}`, { cookie: returned });
    assert.equal(theirs.status, 200);
    assert.equal(await theirs.text(), '{"v":1}');
});

test('you cannot withdraw your own invitation', async () => {
    // The one click on this page that could not be undone from this page: the next request would
    // be refused, including the request to put it back.
    const app = gated();
    const cookie = await enter(app, ADMIN);

    await app.call('/api/admin/invitations',
        { method: 'POST', body: { email: 'deputy@example.test', grantsAdmin: true }, cookie });
    const deputy = await enter(app, 'deputy@example.test');

    const listed = await (await app.call('/api/admin/invitations', { cookie: deputy })).json();
    const own = listed.invitations.find(i => i.email === 'deputy@example.test');

    const refused = await app.call(`/api/admin/invitations/${own.id}`,
        { method: 'DELETE', cookie: deputy });

    assert.equal(refused.status, 409);
    assert.equal((await app.call('/api/admin/invitations', { cookie: deputy })).status, 200,
        'the deputy locked themselves out anyway');
});

test('an id that is not one this server mints never reaches a query', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    for (const id of ['nonsense', 'c_' + 'a'.repeat(22), 'i_short', "i_'; DROP TABLE invitations;--"]) {
        const response = await app.call(`/api/admin/invitations/${id}`, { method: 'DELETE', cookie });

        assert.equal(response.status, 400, `${id} was not refused`);
    }

    // Absent is not a refusal: the end state asked for is that the invitation is not there.
    assert.equal((await app.call(`/api/admin/invitations/i_${'z'.repeat(22)}`,
        { method: 'DELETE', cookie })).status, 204);
});

test('a request from another site cannot change the list', async () => {
    const app = gated();
    const cookie = await enter(app, ADMIN);

    const posted = await app.call('/api/admin/invitations', {
        method: 'POST', body: { email: 'guest@example.test' }, cookie, origin: 'https://evil.test',
    });

    assert.equal(posted.status, 403);

    const listed = await (await app.call('/api/admin/invitations', { cookie })).json();
    assert.equal(listed.invitations.length, 1, 'a cross-site request added somebody');
});

test('one reading of what an address is, shared by the gate and the key', async () => {
    // Two normalisers that disagree by one character is an invitation that exists and never
    // matches. This is a source check because the failure is invisible to a behavioural one until
    // somebody happens to test the character they differ on.
    const { readFileSync, readdirSync } = await import('node:fs');
    const { join, dirname } = await import('node:path');
    const { fileURLToPath } = await import('node:url');

    const worker = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'worker');

    const definers = readdirSync(worker)
        .filter(f => f.endsWith('.js'))
        .filter(f => readFileSync(join(worker, f), 'utf8').includes('function normaliseEmail'));

    assert.deepEqual(definers, ['email.js'],
        'more than one file decides what an email address is: ' + definers.join(', '));

    // The positive control: the two that must agree really do both use it.
    for (const file of ['auth.js', 'invitations.js']) {
        assert.match(readFileSync(join(worker, file), 'utf8'), /from '\.\/email\.js'/,
            `${file} does not import the shared reading of an address`);
    }
});

test('a broken deployment answers an invited and an uninvited address identically', async () => {
    // **The one place the two slices that met here genuinely disagreed.** The gate was written
    // above the `SITE_URL` check, so on a deployment with that setting missing an invited address
    // got a 500 and an uninvited one got `204` — which makes every misconfiguration a way of
    // reading the list, one address at a time. It is the same oracle the error slice refuses for
    // account existence, on the axis this file's feature added.
    //
    // Asserted as byte-identical bodies rather than as equal statuses, for the reason that slice
    // gives: a category, a reference or a sentence that differed would be the leak wearing a
    // different hat. The reference is held still by the harness, so two failures differ only if
    // something about *them* differs.
    const app = gated();
    delete app.env.SITE_URL;

    app.invite('invited@example.test');

    const invited = await ask(app, 'invited@example.test');
    const stranger = await ask(app, 'nobody@example.test');

    assert.equal(invited.status, stranger.status,
        'a missing setting told the caller whether the address was on the list');
    const invitedBody = await invited.text();
    assert.equal(invitedBody, await stranger.text());

    // The positive control: this passes trivially if the deployment is not actually broken, or if
    // the gate has stopped gating. So the failure has to be the configuration one, and the
    // stranger has to still be refused when the deployment is sound.
    assert.equal(invited.status, 500, 'the deployment was not broken, so this asserts nothing');
    assert.equal(JSON.parse(invitedBody).category, 'configuration');
    assert.equal(app.sent.length, 0, 'a link was mailed by a site that cannot address one');

    const sound = gated();
    sound.invite('invited@example.test');
    await ask(sound, 'invited@example.test');
    await ask(sound, 'nobody@example.test');
    assert.equal(sound.sent.length, 1, 'the gate is not gating, so the comparison above is empty');
});
