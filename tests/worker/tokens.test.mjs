// The one place a sign-in token is minted and the one place its link is built — shared by the
// public request path and by an invitation, so the two lifetimes are the only thing that differs
// between them.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { mintSignInToken, signInLink } from '../../worker/tokens.js';
import { database } from './harness.mjs';

const ENV_WITH_SITE = { SITE_URL: 'https://pp.example.test' };

test('minting stores only the hash, never the raw token', async () => {
    const db = database();
    const env = { DB: db, ...ENV_WITH_SITE };
    const deps = { now: () => 1000, newSecret: () => 'the-raw-token' };

    const token = await mintSignInToken(env, deps, { email: 'a@b.test', now: 1000, lifetimeMs: 500 });

    assert.equal(token, 'the-raw-token', 'the caller was not handed back the token it minted');

    const rows = db.raw.prepare('SELECT * FROM login_tokens').all();
    assert.equal(rows.length, 1, 'nothing was stored, so the assertion below checks nothing');
    assert.notEqual(rows[0].token_hash, 'the-raw-token', 'the raw token reached the database');
    assert.equal(rows[0].email, 'a@b.test');
    assert.equal(rows[0].expires_at, 1500, 'now + lifetimeMs was not what was stored');
});

test('signInLink puts the token in the query string of the configured site', () => {
    assert.equal(signInLink(ENV_WITH_SITE, 'abc123'), 'https://pp.example.test/signin?t=abc123');

    // A trailing slash on the setting must not become a doubled one in the link.
    assert.equal(signInLink({ SITE_URL: 'https://pp.example.test/' }, 'abc123'),
        'https://pp.example.test/signin?t=abc123');
});

test('signInLink falls back to a relative link rather than throwing with no SITE_URL', () => {
    // The public request path never reaches this with no `SITE_URL` — it refuses earlier, as a
    // configuration failure. This is the fallback for a caller that does not guard the same way.
    assert.equal(signInLink({}, 'abc123'), '/signin?t=abc123');
});
