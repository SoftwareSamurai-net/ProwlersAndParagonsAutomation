// The D1 migration gate's decision logic — scripts/d1-migrations/gate.mjs — driven with canned
// wrangler output and canned migration SQL. No network call happens here and no real `wrangler`
// is invoked: everything this file imports is a pure function, so what this suite proves is the
// decision, not that a particular Cloudflare account answers a particular way today. Whether
// `scripts/apply-migrations.sh` actually calls `wrangler` correctly is not this file's business —
// see scripts/test-deploy-gate.sh's own header for what that script is for.
//
// **What this file cannot see:** whether wrangler's prose changes in some future release. Two of
// the fixtures below (the "additive pending" table and the "unrecognised" usage dump) are bytes
// captured from the real `wrangler@4.127.0` and `wrangler@3.90.0` binaries against this
// repository's own `d1/` on 2026-08-28, not invented strings — see the comments beside each. If
// wrangler reword either shape, this suite staying green proves nothing about the real CLI; only
// re-running against the real binary would catch that, which is exactly the trap CLAUDE.md names
// for a suite that builds the world it tests.
//
// **Two things the task briefing describing this gate got wrong, corrected here rather than
// worked around** (see this session's own notes for the fuller argument):
//
//   1. `0006_campaign_membership.sql` does not contain a comment reading "a DROP TABLE here
//      would cascade". No migration file in this repository has a destructive keyword inside a
//      comment or a string literal today, so the "keyword inside a comment/string" branches
//      below use small synthetic SQL instead of a real file, clearly labelled as such.
//   2. `0002_characters_list.sql` is a genuine SQLite 12-step table rebuild — it contains both a
//      literal `DROP TABLE characters` and a literal `ALTER TABLE characters_new RENAME TO
//      characters`, with no `pp:allow-destructive` marker anywhere in the file. Classified
//      honestly, per this task's own destructive-pattern rules, it is DESTRUCTIVE — not additive.
//      "every real file under d1/migrations/ classifies as additive" does not hold for the
//      migrations as they exist on disk, and weakening the classifier to make it hold would be
//      the exact "denylist tuned to pass the fixture" failure CLAUDE.md warns against. The test
//      below asserts the honest answer for every real file, 0002 included, and says why.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { classifyMigrationSql, readPending, decide } from '../../scripts/d1-migrations/gate.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const migrationsDir = join(here, '..', '..', 'd1', 'migrations');

const realSql = (name) => readFileSync(join(migrationsDir, name), 'utf8');

// ---------------------------------------------------------------------------------------------
// classifyMigrationSql
// ---------------------------------------------------------------------------------------------

test('an additive migration classifies as additive', () => {
    // 0005_campaigns.sql: one ALTER TABLE ADD COLUMN, one CREATE TABLE. Nothing destructive.
    const result = classifyMigrationSql(realSql('0005_campaigns.sql'));
    assert.equal(result.destructive, false);
    assert.deepEqual(result.statements, []);
    assert.equal(result.overridden, false);
});

test('a genuine table rebuild (DROP TABLE + RENAME TO) classifies as destructive', () => {
    // 0002_characters_list.sql really does this, on purpose, to reshape `characters`'s primary
    // key — see the fixture's own header comment. It has no override marker.
    const sql = realSql('0002_characters_list.sql');
    const result = classifyMigrationSql(sql);

    assert.equal(result.destructive, true);
    assert.equal(result.overridden, false);
    assert.ok(result.statements.some((s) => /DROP\s+TABLE\s+characters\s*;/i.test(s)),
        `expected a DROP TABLE statement among: ${JSON.stringify(result.statements)}`);
    assert.ok(result.statements.some((s) => /RENAME\s+TO\s+characters\s*;/i.test(s)),
        `expected a RENAME TO statement among: ${JSON.stringify(result.statements)}`);
});

test('a destructive keyword inside a SQL comment does not count — synthetic fixture', () => {
    // No real migration in this repository has this shape today (see this file's header
    // comment), so this is written by hand. It still exercises the exact masking the real
    // 0006 comment about foreign-key deletes would need if it ever grows a DROP-shaped aside.
    const sql = `
        -- Note for whoever reads this later: a DROP TABLE here would cascade badly, so this
        -- migration does not do that.
        CREATE TABLE widgets (
            id TEXT PRIMARY KEY
        );
    `;

    const result = classifyMigrationSql(sql);
    assert.equal(result.destructive, false, `wrongly flagged: ${JSON.stringify(result.statements)}`);
    assert.deepEqual(result.statements, []);
});

test('a destructive keyword inside a string literal does not count — synthetic fixture', () => {
    const sql = `
        INSERT INTO log_messages (id, message)
        VALUES ('m1', 'warning: a DROP TABLE would be destructive here');
    `;

    const result = classifyMigrationSql(sql);
    assert.equal(result.destructive, false, `wrongly flagged: ${JSON.stringify(result.statements)}`);
});

test('a block comment hides a destructive keyword the same way a line comment does', () => {
    const sql = `
        /* Historical note: this table used to say DELETE FROM here, before the redesign. */
        ALTER TABLE widgets ADD COLUMN label TEXT;
    `;

    const result = classifyMigrationSql(sql);
    assert.equal(result.destructive, false, `wrongly flagged: ${JSON.stringify(result.statements)}`);
});

test('the override marker is read, and an empty reason does not count', () => {
    const withReason = 'DROP TABLE widgets;\n-- pp:allow-destructive: replaced by a view\n';
    assert.equal(classifyMigrationSql(withReason).overridden, true);

    const emptyReason = 'DROP TABLE widgets;\n-- pp:allow-destructive:   \n';
    assert.equal(classifyMigrationSql(emptyReason).overridden, false);

    const noMarkerAtAll = 'DROP TABLE widgets;\n';
    assert.equal(classifyMigrationSql(noMarkerAtAll).overridden, false);
});

test('every real file under d1/migrations classifies honestly — 0002 is the documented exception', () => {
    const files = readdirSync(migrationsDir).filter((f) => f.endsWith('.sql'));

    // Positive control: prove the directory scan actually found files, so the loop below is not
    // silently checking nothing.
    assert.ok(files.length > 0, 'no .sql files were found under d1/migrations at all');

    // See this file's header comment: 0002 is a real, deliberate table rebuild with no override
    // marker, so it is destructive under this task's own rules. Every other file in this
    // repository today is additive.
    const expectedDestructive = new Set(['0002_characters_list.sql']);

    for (const file of files) {
        const { destructive, statements } = classifyMigrationSql(realSql(file));
        if (expectedDestructive.has(file)) {
            assert.equal(destructive, true, `${file} was expected to classify destructive`);
        } else {
            assert.equal(destructive, false,
                `${file} classified destructive unexpectedly: ${JSON.stringify(statements)}`);
        }
    }
});

// ---------------------------------------------------------------------------------------------
// readPending
// ---------------------------------------------------------------------------------------------

// Captured verbatim from `npx wrangler@4.127.0 --cwd d1 d1 migrations list prowlers-and-paragons
// --remote` against the real database on 2026-08-28 — see this file's header comment.
const REAL_PENDING_OUTPUT = `
 ⛅️ wrangler 4.127.0
────────────────────
Resource location: remote

Migrations to be applied:
┌──────────────────────────────┐
│ Name                         │
├──────────────────────────────┤
│ 0006_campaign_membership.sql │
└──────────────────────────────┘
`;

// The literal phrase wrangler@4.127.0 answers with when the remote schema is fully caught up,
// per the phrase this repository's earlier gate (PR #104's history, quoted in
// docs/guide/hosting.md) verified against the real database. Wrapped in the same preamble shape
// as the captured output above, since a real answer is never just the one line.
const REAL_CLEAN_OUTPUT = `
 ⛅️ wrangler 4.127.0
────────────────────
Resource location: remote

No migrations to apply!
`;

// Captured verbatim from `npx wrangler@3.90.0 --cwd d1 d1 migrations list ... --remote`. 3.90.0
// does not understand `--cwd` at all and dumps this command's usage instead of running it.
//
// **This used to be "the exact wrangler version deploy.yml bundles with", and it is not any
// more**: the action is `cloudflare/wrangler-action@v4` and everything here now pins 4.127.0,
// held together by `WranglerIsPinnedToOneVersion`. The fixture stays, and is worth more than the
// history it records — a usage dump is what *any* wrangler answers a flag it does not know, and
// the version that will one day not know one is a version nobody has met yet. What must never
// happen is a usage dump being read as "nothing pending"; that is what this case pins.
//
// ANSI colour codes stripped; everything else is verbatim.
const REAL_390_USAGE_DUMP = `
X [ERROR] Unknown argument: cwd


wrangler d1 migrations list <database>

List your D1 migrations

POSITIONALS
  database  The name or binding of the DB  [string] [required]

GLOBAL FLAGS
  -j, --experimental-json-config  Experimental: support wrangler.json  [boolean]
  -c, --config                    Path to .toml configuration file  [string]
  -e, --env                       Environment to use for operations and .env files  [string]
  -h, --help                      Show help  [boolean]
  -v, --version                   Show version number  [boolean]

OPTIONS
      --local       Execute commands/files against a local DB for use with wrangler dev  [boolean]
      --remote      Execute commands/files against a remote DB for use with wrangler dev --remote  [boolean]
      --preview     Execute commands/files against a preview D1 DB  [boolean] [default: false]
      --persist-to  Specify directory to use for local persistence (you must use --local with this flag)  [string]
`;

// The exact sentence Cloudflare answered with when the deploy's own token first tried this (see
// docs/guide/hosting.md and the history of PR #104/#105).
const REAL_7403_OUTPUT = 'X [ERROR] The given account is not valid or is not authorized to access '
    + 'this service [code: 7403]';

test('readPending recognises the clean answer as nothing pending', () => {
    assert.deepEqual(readPending(REAL_CLEAN_OUTPUT), { recognised: true, pending: [] });
});

test('readPending parses the pending table into filenames', () => {
    assert.deepEqual(
        readPending(REAL_PENDING_OUTPUT),
        { recognised: true, pending: ['0006_campaign_membership.sql'] });
});

test('readPending refuses to recognise a usage dump', () => {
    assert.equal(readPending(REAL_390_USAGE_DUMP).recognised, false);
});

test('readPending refuses to recognise empty output', () => {
    assert.deepEqual(readPending(''), { recognised: false, pending: [] });
});

// ---------------------------------------------------------------------------------------------
// decide — every branch named in the task
// ---------------------------------------------------------------------------------------------

test('decide: nothing pending -> proceed', () => {
    const result = decide({ listStdout: REAL_CLEAN_OUTPUT, listStatus: 0, migrations: [] });
    assert.equal(result.action, 'proceed');
    assert.equal(result.exitCode, 0);
});

test('decide: additive pending -> apply', () => {
    const result = decide({
        listStdout: REAL_PENDING_OUTPUT,
        listStatus: 0,
        migrations: [{ name: '0006_campaign_membership.sql', sql: realSql('0006_campaign_membership.sql') }],
    });
    assert.equal(result.action, 'apply');
    assert.equal(result.exitCode, 0);
});

test('decide: destructive pending -> refuse, naming the file and the statement', () => {
    // A synthetic "pending" table naming 0002: this repository's real database is not actually
    // sitting on 0002 today (0006 is its one real pending migration, per the fixture above), but
    // 0002's file content is real, and the point under test is what decide() does once told a
    // destructive file is pending — which does not require the live account to agree.
    const listStdout = REAL_PENDING_OUTPUT.replace(
        '0006_campaign_membership.sql', '0002_characters_list.sql');

    const result = decide({
        listStdout,
        listStatus: 0,
        migrations: [{ name: '0002_characters_list.sql', sql: realSql('0002_characters_list.sql') }],
    });

    assert.equal(result.action, 'refuse');
    assert.equal(result.exitCode, 1);
    assert.match(result.message, /0002_characters_list\.sql/);
    assert.match(result.message, /DROP\s+TABLE\s+characters/i);
    assert.match(result.message, /pp:allow-destructive/);
});

test('decide: destructive pending WITH an override marker -> apply', () => {
    const listStdout = REAL_PENDING_OUTPUT.replace(
        '0006_campaign_membership.sql', '0002_characters_list.sql');

    const sqlWithOverride = realSql('0002_characters_list.sql')
        + '\n-- pp:allow-destructive: reshaping the primary key is the whole point of this file\n';

    const result = decide({
        listStdout,
        listStatus: 0,
        migrations: [{ name: '0002_characters_list.sql', sql: sqlWithOverride }],
    });

    assert.equal(result.action, 'apply');
    assert.equal(result.exitCode, 0);
    assert.match(result.message, /0002_characters_list\.sql/);
});

test('decide: the 7403 unauthorised answer -> refuse, naming D1: Edit', () => {
    const result = decide({ listStdout: REAL_7403_OUTPUT, listStatus: 0, migrations: [] });
    assert.equal(result.action, 'refuse');
    assert.equal(result.exitCode, 1);
    assert.match(result.message, /D1: Edit/);
    // The load-bearing property is behavioural, not lexical: an unauthorised answer must refuse
    // the deploy exactly like an unrecognised one does, never be treated as "nothing pending".
    assert.notEqual(result.action, 'proceed');
});

test('decide: unrecognised wrangler output -> refuse', () => {
    const result = decide({ listStdout: REAL_390_USAGE_DUMP, listStatus: 0, migrations: [] });
    assert.equal(result.action, 'refuse');
    assert.equal(result.exitCode, 1);
});

test('decide: empty wrangler output -> refuse', () => {
    const result = decide({ listStdout: '', listStatus: 1, migrations: [] });
    assert.equal(result.action, 'refuse');
    assert.equal(result.exitCode, 1);
});

test('decide: a pending name with no matching local file content -> refuse, not silently additive', () => {
    // The dangerous failure mode this guards: if a name is pending but its SQL cannot be read,
    // treating it as empty (and therefore additive) would apply an unseen migration.
    const result = decide({
        listStdout: REAL_PENDING_OUTPUT,
        listStatus: 0,
        migrations: [], // no content supplied for the pending name at all
    });
    assert.equal(result.action, 'refuse');
    assert.equal(result.exitCode, 1);
    assert.match(result.message, /0006_campaign_membership\.sql/);
});
