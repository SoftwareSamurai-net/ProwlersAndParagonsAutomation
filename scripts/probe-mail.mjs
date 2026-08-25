#!/usr/bin/env node
// Sends one real message through the real provider, and prints what it says back.
//
// **This exists because the server is deliberately bad at telling you why a send failed, and
// that is correct of it.** `worker/mail.js` reports the provider's status and its machine code
// and drops the `message` field, because that field can quote the address and whatever else was
// sent — it reaches a visitor's screen and a log line, and neither is a place for it. The cost
// of that discipline is that the owner cannot see the one sentence that names the broken field.
//
// So the sentence is read here instead: on the owner's machine, from the owner's own credentials,
// printed to a terminal and stored nowhere.
//
// **It sends the same body the server sends**, because it imports the builder rather than
// writing one. A hand-written probe was tried first and cost half an hour: it carried a
// different key and a literal `YOUR_ADDRESS`, reproduced the same provider code for an entirely
// different reason, and read exactly like a confirmation of the theory being tested.
//
//   node scripts/probe-mail.mjs your.address@example.com
//
// Credentials come from `.dev.vars` in the repository root — the file `wrangler pages dev`
// already reads, gitignored, and never committed. See `docs/ACCOUNTS-SETUP.md`.

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { RESEND_ENDPOINT, signInMessage } from '../worker/mail.js';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

/**
 * `.dev.vars` as an object.
 *
 * <p>The format is wrangler's: `NAME=value` a line at a time, `#` comments, no quoting rules of
 * its own. Surrounding quotes are stripped **and reported**, because a value pasted with them is
 * one of the things this probe exists to catch — a quoted `MAIL_FROM` is refused by the provider
 * as a malformed address, and the quotes are invisible in a dashboard field.</p>
 */
function readDevVars() {
    const path = join(ROOT, '.dev.vars');
    let text;

    try {
        text = readFileSync(path, 'utf8');
    } catch {
        fail(`No .dev.vars in ${ROOT}.`,
            'Create one with RESEND_API_KEY and MAIL_FROM — docs/ACCOUNTS-SETUP.md has the shape.');
    }

    const vars = {};

    for (const [index, line] of text.split(/\r?\n/).entries()) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;

        const at = trimmed.indexOf('=');
        if (at < 1) fail(`.dev.vars line ${index + 1} is not NAME=value.`);

        const name = trimmed.slice(0, at).trim();
        const raw = trimmed.slice(at + 1).trim();
        const unquoted = raw.replace(/^(["'])(.*)\1$/s, '$2');

        if (unquoted !== raw) {
            console.warn(`note: ${name} in .dev.vars is wrapped in quotes; using the value inside`
                + ' them. Cloudflare does not strip quotes, so if the same value is quoted there,'
                + ' that alone would be refused.');
        }

        vars[name] = unquoted;
    }

    return vars;
}

/** Prints the reason and stops. Never prints a credential. */
function fail(...lines) {
    for (const line of lines) console.error(line);
    process.exit(2);
}

const to = process.argv[2];
if (!to) fail('Usage: node scripts/probe-mail.mjs <address to send to>');

const env = readDevVars();

for (const name of ['RESEND_API_KEY', 'MAIL_FROM']) {
    if (!env[name]) fail(`.dev.vars has no ${name}.`);
}

// The one thing worth saying about the key, said without saying the key. A trailing newline or a
// stray space is invisible everywhere else and is refused as an invalid key.
console.log(`RESEND_API_KEY: ${env.RESEND_API_KEY.length} characters,`
    + ` starts "${env.RESEND_API_KEY.slice(0, 3)}…"`);
console.log(`MAIL_FROM:      ${JSON.stringify(env.MAIL_FROM)}`);
console.log(`to:             ${JSON.stringify(to)}`);
console.log();

// Quoted rather than bare, so a trailing space or a newline in either value is visible in the
// output rather than being something you have to already suspect.
const body = signInMessage(env, { to, link: 'https://example.invalid/signin?t=probe' });

console.log('Sending the same body the server sends…');

const response = await fetch(RESEND_ENDPOINT, {
    method: 'POST',
    headers: {
        authorization: `Bearer ${env.RESEND_API_KEY}`,
        'content-type': 'application/json',
    },
    body: JSON.stringify(body),
});

const answer = await response.text();

console.log(`\nHTTP ${response.status}`);
console.log(answer);

if (response.ok) {
    console.log('\nAccepted. If no mail arrives, it left this end — check spam, then the'
        + ' provider\'s own Emails list, which records a send that was accepted and then bounced.');
    process.exit(0);
}

// The mapping the docs used to get wrong: the provider answers `validation_error` for a bad key
// as well as for a bad field, so the *name* does not tell them apart and the status does.
console.log('\nRefused. The `message` field above names the field at fault — that is the whole'
    + ' reason this probe exists, since the server drops it on purpose.');
process.exit(1);
