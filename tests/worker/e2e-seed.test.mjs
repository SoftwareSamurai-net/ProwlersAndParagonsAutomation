// The end-to-end harness's seed planner, which mints the `login_tokens` row an email would have
// caused so that `scripts/e2e.sh` can sign a reader in from outside the application.
//
// **Why it is tested here, in the accounts server's suite, rather than beside the harness.** Two
// reasons and the first is that there is nowhere else: `scripts/test-worker.sh` is this
// repository's only Node test runner, and `scripts/e2e.sh` reports *verdicts* rather than a test
// count — `docs/guide/testing.md` says why that cannot be totalled with the other suites. The
// second is that half of what is asserted below is a claim about **this server**: the hash a row
// is seeded under has to be the hash `worker/crypto.js` mints, or every seeded link is a lookup
// that finds nothing, and the whole of stage two fails as "the link did not sign anybody in" with
// nothing in the output saying why.
//
// **And the two throwing properties are the honesty of a seed twin.** `scripts/e2e/defects.mjs`
// declares a negative control by naming the slots it overrides; `plan` throws if a name it does
// not mint appears, and throws again if a twin's plan comes out identical to the real run's — the
// same pair `buildTwin`'s line substitution has, against a database row. Neither fires on the
// shipped list, correctly, so neither had ever been watched to fire. These drive them over
// synthetic defects that do.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { hash as workerHash } from '../../worker/crypto.js';
import { DEFECTS, EXPECTATIONS, kindOf } from '../../scripts/e2e/defects.mjs';
import { envFor, hash, plan, sqlFor } from '../../scripts/e2e/seed.mjs';

const NOW = 1_700_000_000_000;

/** A seed defect shaped the way `defects.mjs` declares one, with whatever overrides are wanted. */
const twin = slots => [{ name: 'a-synthetic-twin', check: 'RULES', expects: 'control', seed: { slots } }];

test('importing the planner does not run its command line', async () => {
    // The positive control for every other test in this file. Until the `invokedDirectly` guard,
    // importing this module printed a usage line and exited 2 — so an import of it took the test
    // process with it, and none of the assertions below could exist at all.
    const module = await import('../../scripts/e2e/seed.mjs');

    assert.equal(typeof module.plan, 'function');
    assert.equal(typeof module.sqlFor, 'function');
    assert.equal(typeof module.envFor, 'function');
});

test('the hash a row is seeded under is the one worker/crypto.js mints', async () => {
    // Node's `crypto` here, WebCrypto there. What has to agree is the digest, because
    // `db.spendLoginToken` finds a row by it and by nothing else.
    for (const secret of ['', 'a', 'the-raw-token', 'zAe9_-QbT7xyz', 'a token with spaces']) {
        assert.equal(hash(secret), await workerHash(secret),
            `the seeded hash of ${JSON.stringify(secret)} is not what the server would look up`);
    }
});

test('the real plan mints a live token per slot, all at distinct addresses', () => {
    const profiles = plan(NOW);

    assert.ok(profiles.real, 'there is no profile for the real run');

    const slots = Object.keys(profiles.real.tokens);
    assert.ok(slots.length >= 4, `only ${slots.length} slots are minted; the SLOTS table has moved`);

    const tokens = new Set();

    for (const [slot, t] of Object.entries(profiles.real.tokens)) {
        assert.ok(t.expiresAt > NOW, `the real run's ${slot} token is already expired`);
        assert.match(t.email, /@e2e\.invalid$/, `${slot} was minted at ${t.email}`);
        tokens.add(t.token);
    }

    assert.equal(tokens.size, slots.length, 'two slots share a token, and a token is single-use');
});

test('a twin that names a slot the harness does not mint is refused', () => {
    assert.throws(
        () => plan(NOW, twin({ NOT_A_SLOT: { expired: true } })),
        /overrides a token slot called 'NOT_A_SLOT'/,
        'a negative control aimed at a slot that does not exist was planned anyway');
});

test('a twin that names an account the harness does not mint is refused', () => {
    assert.throws(
        () => plan(NOW, twin({ RULES: { account: 'nobody-in-particular' } })),
        /points slot RULES at an account called 'nobody-in-particular'/,
        'a negative control aimed at an account that does not exist was planned anyway');
});

test('a twin whose plan is identical to the real run is refused', () => {
    // The shape of the failure this catches: a `seed.slots` entry that has stopped changing
    // anything the application can tell apart. The twin is then a second copy of the real run and
    // passes for the wrong reason — `buildTwin`'s zero-occurrences case, against a row.
    assert.throws(
        () => plan(NOW, twin({})),
        /seeds exactly what the real run seeds/,
        'a twin that changes nothing was planned as though it were a negative control');

    // And the same override that *does* change something must still be accepted, or the assertion
    // above would pass against a planner that refused everything.
    assert.ok(plan(NOW, twin({ RULES: { expired: true } }))['a-synthetic-twin'],
        'a twin that really does differ was refused, so the check above proves nothing');
});

test('the SQL seeds one invitation per address and one token row per slot', () => {
    const profiles = plan(NOW, twin({ RULES: { expired: true } }));
    const sql = sqlFor(profiles, NOW);

    const invitations = sql.match(/^INSERT INTO invitations /gm) ?? [];
    const logins = sql.match(/^INSERT INTO login_tokens /gm) ?? [];

    const addresses = new Set();
    let slots = 0;

    for (const profile of Object.values(profiles)) {
        for (const t of Object.values(profile.tokens)) {
            addresses.add(t.email);
            slots++;
        }
    }

    assert.equal(invitations.length, addresses.size, 'the invitation rows do not match the addresses');
    assert.equal(logins.length, slots, 'a slot was minted with no row to sign in against');

    // The raw token is what travels by email and what the driver drives; only its hash is stored,
    // which is the property that makes seeding a row honest rather than a back door.
    for (const profile of Object.values(profiles)) {
        for (const t of Object.values(profile.tokens)) {
            assert.ok(!sql.includes(t.token), 'a raw sign-in token was written into the SQL');
            assert.ok(sql.includes(hash(t.token)), 'a token was minted with no hash seeded for it');
        }
    }
});

test('the environment for a drive is KEY=VALUE, one token and one address per slot', () => {
    const profiles = plan(NOW);
    const lines = envFor(profiles.real);

    assert.equal(lines.length, Object.keys(profiles.real.tokens).length * 2);

    for (const line of lines) {
        assert.match(line, /^PP_E2E_[A-Z0-9_]+=(?!$).+$/, `"${line}" is not a KEY=VALUE a shell can use`);
    }
});

test('every twin declares a kind of red verdict this harness knows', () => {
    assert.ok(DEFECTS.length >= 6, 'the twin list could not be read, and an empty one agrees with everything');

    for (const defect of DEFECTS) {
        assert.ok(EXPECTATIONS.includes(defect.expects),
            `twin '${defect.name}' declares expects: ${JSON.stringify(defect.expects)}, which `
            + `scripts/e2e.sh cannot require of its FAIL line. Known: ${EXPECTATIONS.join(', ')}.`);

        assert.ok(['site', 'seed'].includes(kindOf(defect)));
    }
});
