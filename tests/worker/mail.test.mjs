// The one call this server makes to somebody else, and what it says when that call is refused.
//
// **Nothing else in this suite reaches it.** The harness stubs `sendSignInLink`, because the
// mail provider is not the system under test anywhere else — which meant this file's subject had
// no coverage at all while being the single point every sign-in passes through. `fetch` is
// stubbed here instead of the function, so the request that would go to Resend is inspected and
// the answers Resend gives are played back.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { readFile } from 'node:fs/promises';

import { invitationMessage, sendInvitationMail, sendSignInLink, signInMessage } from '../../worker/mail.js';

const ENV = {
    RESEND_API_KEY: 'not-a-real-key',
    MAIL_FROM: 'no-reply@example.test',
    SITE_URL: 'https://pp.example.test',
};

const MESSAGE = { to: 'player@example.test', link: 'https://pp.example.test/signin?t=abc' };

/**
 * Runs one of the two senders against one canned provider answer, and hands back what it was
 * sent. `send` defaults to `sendSignInLink`, which is what every test predating the invitation
 * mail assumes.
 */
async function against(answer, { env = ENV, message = MESSAGE, send = sendSignInLink } = {}) {
    const original = globalThis.fetch;
    const calls = [];

    globalThis.fetch = async (url, init) => {
        calls.push({ url, init });

        return answer();
    };

    try {
        const failure = await send(env, message).then(() => null, error => error);

        return { calls, failure };
    } finally {
        globalThis.fetch = original;
    }
}

const refusal = (status, body) => () => new Response(
    body === undefined ? '' : JSON.stringify(body),
    { status, headers: { 'content-type': 'application/json' } });

test('a link is handed to the provider as one request, with the link in both bodies', async () => {
    const { calls, failure } = await against(() => new Response(
        JSON.stringify({ id: 'ffff' }), { status: 200 }));

    assert.equal(failure, null, 'an accepted send must not throw');
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, 'https://api.resend.com/emails');

    const body = JSON.parse(calls[0].init.body);

    assert.equal(body.from, ENV.MAIL_FROM);
    assert.deepEqual(body.to, [MESSAGE.to]);
    assert.ok(body.text.includes(MESSAGE.link), 'the plain text part carries no link');
    assert.ok(body.html.includes(MESSAGE.link), 'the HTML part carries no link');
});

test('a refusal names the provider’s own code for it, not just the status', async () => {
    // **The status alone sent somebody to an empty dashboard for a day.** A 400 is a validation
    // error — a statement about which field was refused — and a refusal at validation never
    // reaches the provider's sent list at all, so "check their dashboard" points at the one
    // place that is guaranteed to be empty when this fires.
    const { failure } = await against(refusal(400, {
        name: 'validation_error',
        message: 'The `from` field is not a valid address: nobody@nowhere.test',
    }));

    assert.ok(failure instanceof Error, 'a refused send must throw');
    assert.match(failure.message, /HTTP 400/);
    assert.match(failure.message, /validation_error/);
});

test('nothing from the provider’s prose reaches the message', async () => {
    // The guard that makes the line above safe to log: only `name` is read, and only when it
    // looks like a code. An address cannot pass — it carries an `@` and a dot, and the pattern
    // admits neither — so a provider that started putting the recipient in that field would be
    // dropped rather than quoted.
    const { failure } = await against(refusal(400, {
        name: 'player@example.test',
        message: 'player@example.test is not deliverable',
    }));

    assert.ok(failure instanceof Error);
    assert.match(failure.message, /HTTP 400/);
    assert.doesNotMatch(failure.message, /player@example\.test/,
        'the address reached the message, which is the one thing this may never carry');
    assert.doesNotMatch(failure.message, /not deliverable/,
        'the provider’s prose reached the message');
});

test('a refusal with no readable body is still a refusal', async () => {
    // It runs while reporting a failure, so a body that is empty, or HTML from something in
    // front of the provider, has to produce a message without a code — never a second exception
    // on top of the first, which would replace a diagnosable refusal with a parsing stack trace.
    for (const answer of [
        refusal(502, undefined),
        () => new Response('<html>Bad gateway</html>', { status: 502 }),
        () => new Response(JSON.stringify({ message: 'no name field here' }), { status: 429 }),
    ]) {
        const { failure } = await against(answer);

        assert.ok(failure instanceof Error, 'a refused send must throw whatever the body was');
        assert.match(failure.message, /The mail provider refused the send \(HTTP \d{3}\)\./);
    }
});

// The probe that reads what this file's subject deliberately throws away.
//
// `worker/mail.js` drops the provider's `message` field on purpose — it can quote the address —
// so `scripts/probe-mail.mjs` exists to read it on the owner's own machine. The probe is only
// worth anything if it sends *this* body; one that assembles a lookalike can reproduce the same
// provider error for a different reason and read as a confirmation. That happened, by hand,
// before the probe existed: a different key and a literal `YOUR_ADDRESS` in `to` returned the
// same code the site was returning and sent the diagnosis half an hour the wrong way.

test('the probe sends the body this server sends, rather than one of its own', async () => {
    const source = await readFile(new URL('../../scripts/probe-mail.mjs', import.meta.url), 'utf8');

    assert.match(source, /import \{[^}]*signInMessage[^}]*\} from '\.\.\/worker\/mail\.js'/,
        'the probe must import the shared builder, or it is testing a payload nobody sends');
    assert.match(source, /signInMessage\(env, \{ to, link[^}]*\}\)/,
        'the probe must call the builder, not merely import it');

    // The positive control, and it is not optional: both assertions above are satisfied by a
    // probe that imports the builder and then posts something else entirely. This one fails if
    // the probe grows a second literal body — a `from:` or a `subject:` of its own.
    const ownPayload = source.match(/^\s*(from|subject|html):/gm);
    assert.equal(ownPayload, null,
        `the probe builds part of the message itself: ${ownPayload?.join(', ')}`);
});

test('the builder is what the sender sends, so the probe cannot drift from it', async () => {
    const { calls } = await against(() => new Response(JSON.stringify({ id: 'ffff' }), { status: 200 }));
    const sent = JSON.parse(calls[0].init.body);

    assert.deepEqual(sent, signInMessage(ENV, MESSAGE),
        'sendSignInLink and signInMessage disagree, so the probe would test the wrong body');
});

// ---------------------------------------------------------------------------------------------
// The invitation mail: told when an administrator adds an address, and carrying a one-click link.
// ---------------------------------------------------------------------------------------------

// **This message carries a real sign-in token, on purpose.** See `INVITATION_TOKEN_LIFETIME_MS`
// on `worker/auth.js` for why: an administrator chose this address deliberately, so a
// longer-lived credential in that inbox is an acceptable trade for the first sign-in being one
// click. The token itself is somebody else's concern — `worker/tokens.js` mints it and
// `worker/invitations.js` builds the link with `signInLink` — this file only checks that
// whatever link it is handed reaches the message unchanged, both parts, and that the address
// does too.
const INVITATION = { to: 'guest@example.test', link: 'https://pp.example.test/signin?t=xyz' };

test('an invitation names the address and carries the link it was given, once in each part', async () => {
    const { calls, failure } = await against(
        () => new Response(JSON.stringify({ id: 'ffff' }), { status: 200 }),
        { message: INVITATION, send: sendInvitationMail });

    assert.equal(failure, null, 'an accepted send must not throw');
    assert.equal(calls.length, 1);

    const body = JSON.parse(calls[0].init.body);

    assert.equal(body.from, ENV.MAIL_FROM);
    assert.deepEqual(body.to, [INVITATION.to]);
    assert.ok(body.text.includes(INVITATION.link), 'the plain text part does not carry the link');
    assert.ok(body.html.includes(INVITATION.link), 'the HTML part does not carry the link');
});

test('an invitation mail is refused the same way a sign-in link is', async () => {
    // The refusal-handling is shared between the two senders; this is the control that the
    // sharing really happened rather than a second copy that could drift from the first.
    const { failure } = await against(refusal(400, { name: 'validation_error', message: 'nope' }),
        { message: INVITATION, send: sendInvitationMail });

    assert.ok(failure instanceof Error);
    assert.match(failure.message, /HTTP 400/);
    assert.match(failure.message, /validation_error/);
});

test('the invitation builder is what the invitation sender sends', async () => {
    const { calls } = await against(() => new Response(JSON.stringify({ id: 'ffff' }), { status: 200 }),
        { message: INVITATION, send: sendInvitationMail });
    const sent = JSON.parse(calls[0].init.body);

    assert.deepEqual(sent, invitationMessage(ENV, INVITATION),
        'sendInvitationMail and invitationMessage disagree');
});
