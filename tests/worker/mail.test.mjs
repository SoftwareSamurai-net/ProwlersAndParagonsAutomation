// The one call this server makes to somebody else, and what it says when that call is refused.
//
// **Nothing else in this suite reaches it.** The harness stubs `sendSignInLink`, because the
// mail provider is not the system under test anywhere else — which meant this file's subject had
// no coverage at all while being the single point every sign-in passes through. `fetch` is
// stubbed here instead of the function, so the request that would go to Resend is inspected and
// the answers Resend gives are played back.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { sendSignInLink } from '../../worker/mail.js';

const ENV = {
    RESEND_API_KEY: 'not-a-real-key',
    MAIL_FROM: 'no-reply@example.test',
};

const MESSAGE = { to: 'player@example.test', link: 'https://pp.example.test/signin?t=abc' };

/** Runs `sendSignInLink` against one canned provider answer, and hands back what it was sent. */
async function against(answer, { env = ENV, message = MESSAGE } = {}) {
    const original = globalThis.fetch;
    const calls = [];

    globalThis.fetch = async (url, init) => {
        calls.push({ url, init });

        return answer();
    };

    try {
        const failure = await sendSignInLink(env, message).then(() => null, error => error);

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
