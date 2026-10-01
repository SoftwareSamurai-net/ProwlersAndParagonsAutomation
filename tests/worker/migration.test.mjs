// The migration that turns one character per account into many.
//
// Exercised directly against SQLite rather than through the server, because that is the only
// way to prove anything about it: `characters.test.mjs`'s harness always applies both
// migrations together, so it can never hold a database in the *pre*-0002 shape a real deployed
// database is in the moment before this migration runs against it. Everything here builds that
// shape by hand — a users row, a characters row with no `id` or `label` column because those
// columns do not exist until 0002 creates them — then applies 0002 and reads what it left.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { DatabaseSync } from 'node:sqlite';
import { readFileSync } from 'node:fs';

import { MIGRATIONS } from './harness.mjs';

/** A database with only 0001 applied — the schema `characters` had before this migration. */
function preMigrationDb() {
    const db = new DatabaseSync(':memory:');
    db.exec(readFileSync(MIGRATIONS[0], 'utf8'));

    return db;
}

function user(db, id, email) {
    db.prepare('INSERT INTO users (id, email, created_at) VALUES (?, ?, ?)').run(id, email, 1000);
}

/** A row in the one-character-per-account shape: no `id`, no `label` — 0001's `characters`. */
function oldCharacter(db, userId, payload, updatedAt) {
    db.prepare('INSERT INTO characters (user_id, payload, updated_at) VALUES (?, ?, ?)')
        .run(userId, payload, updatedAt);
}

function apply0002(db) {
    db.exec(readFileSync(MIGRATIONS[1], 'utf8'));
}

test('an existing character survives 0002 under a generated id and the default label', () => {
    const db = preMigrationDb();
    user(db, 'u_veteran', 'veteran@example.test');
    oldCharacter(db, 'u_veteran', '{"Sheet":{"Name":"Ninefold"}}', 2000);

    apply0002(db);

    const rows = db.prepare('SELECT * FROM characters WHERE user_id = ?').all('u_veteran');

    assert.equal(rows.length, 1, 'the row must not be dropped by the rebuild');
    assert.match(rows[0].id, /^c_[a-z0-9]{22}$/, 'a generated id in the shape the server validates');
    assert.equal(rows[0].label, 'Unnamed character');
    assert.equal(rows[0].payload, '{"Sheet":{"Name":"Ninefold"}}', 'byte for byte, unparsed');
    assert.equal(rows[0].updated_at, 2000, 'the timestamp is not reset by the rebuild');
});

test('two accounts’ existing characters each get their own generated id', () => {
    const db = preMigrationDb();
    user(db, 'u_a', 'a@example.test');
    user(db, 'u_b', 'b@example.test');
    oldCharacter(db, 'u_a', '{}', 1000);
    oldCharacter(db, 'u_b', '{}', 1000);

    apply0002(db);

    const ids = db.prepare('SELECT id FROM characters').all().map(r => r.id);

    assert.equal(ids.length, 2);
    assert.notEqual(ids[0], ids[1], 'two migrated rows must not collide on a generated id');
});

test('an account with no character before the migration still has none after it', () => {
    const db = preMigrationDb();
    user(db, 'u_empty', 'empty@example.test');

    apply0002(db);

    assert.equal(db.prepare('SELECT COUNT(*) AS n FROM characters WHERE user_id = ?')
        .all('u_empty')[0].n, 0);
});

test('every existing account gets the default cap of 5, not null', () => {
    const db = preMigrationDb();
    user(db, 'u_old', 'old@example.test');

    apply0002(db);

    assert.equal(db.prepare('SELECT character_limit FROM users WHERE id = ?')
        .all('u_old')[0].character_limit, 5);
});

test('the rebuilt table still enforces one row per (user_id, id)', () => {
    // Not a behaviour 0002 adds — SQLite refuses a duplicate primary key on any table — but the
    // whole point of the rebuild is that `(user_id, id)` is now *the* primary key rather than
    // `user_id` alone, and that is worth pinning against a regression that recreates the new
    // table without it (a bare `CREATE TABLE ... (user_id, id, ...)` with no `PRIMARY KEY`
    // clause would compile and lose exactly the guarantee this migration exists to add).
    const db = preMigrationDb();
    user(db, 'u_dup', 'dup@example.test');
    apply0002(db);

    db.prepare('INSERT INTO characters (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_dup', 'c_0000000000000000000000', 'One', '{}', 1000);

    assert.throws(() => db.prepare(
        'INSERT INTO characters (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_dup', 'c_0000000000000000000000', 'Two', '{}', 2000));

    // The positive control: the same id under a *different* user_id is not a conflict, because
    // the key is the pair, not `id` alone.
    user(db, 'u_dup_2', 'dup2@example.test');
    assert.doesNotThrow(() => db.prepare(
        'INSERT INTO characters (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_dup_2', 'c_0000000000000000000000', 'Also one', '{}', 1000));
});

// ── 0005: campaigns, and the column that says which one a character is in ────────────────

/** A database with every migration up to and including 0004 — the shape before campaigns. */
function preCampaignsDb() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 4)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

function apply0005(db) {
    db.exec(readFileSync(MIGRATIONS[4], 'utf8'));
}

test('a character written before campaigns existed survives 0005 belonging to none', () => {
    // **This is the migration half of the rule that nothing bumped `StoredCharacter`'s version.**
    // An absent campaign reads back as null on both sides, and null means "belongs to no
    // campaign" — which is true, and is the only answer that loses nobody's work.
    const db = preCampaignsDb();
    user(db, 'u_veteran', 'veteran@example.test');
    db.prepare('INSERT INTO characters (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_veteran', 'c_0000000000000000000000', 'Ninefold', '{"Sheet":{"Name":"Ninefold"}}', 2000);

    apply0005(db);

    const rows = db.prepare('SELECT * FROM characters WHERE user_id = ?').all('u_veteran');

    assert.equal(rows.length, 1, 'the row must not be dropped — 0005 alters, it does not rebuild');
    assert.equal(rows[0].campaign_id, null);
    assert.equal(rows[0].label, 'Ninefold', 'the label survived the alter');
    assert.equal(rows[0].payload, '{"Sheet":{"Name":"Ninefold"}}', 'byte for byte, unparsed');
    assert.equal(rows[0].updated_at, 2000);
});

test('0005 adds campaigns keyed by the pair, like characters', () => {
    const db = preCampaignsDb();
    user(db, 'u_gm', 'gm@example.test');
    apply0005(db);

    const insert = (userId, id) => db.prepare(
        'INSERT INTO campaigns (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run(userId, id, 'A game', '{}', 1000);

    insert('u_gm', 'g_0000000000000000000000');
    assert.throws(() => insert('u_gm', 'g_0000000000000000000000'));

    // The positive control: the same id under a different account is not a conflict.
    user(db, 'u_gm2', 'gm2@example.test');
    assert.doesNotThrow(() => insert('u_gm2', 'g_0000000000000000000000'));
});

test('deleting a campaign row does not touch a character that names it', () => {
    // There is no foreign key on `characters.campaign_id`, deliberately — see the migration's own
    // comment. A cascade or a `SET NULL` here would decide something on the player's behalf.
    const db = preCampaignsDb();
    user(db, 'u_gm', 'gm@example.test');
    apply0005(db);

    db.prepare('INSERT INTO campaigns (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_gm', 'g_0000000000000000000000', 'A game', '{}', 1000);
    db.prepare('INSERT INTO characters (user_id, id, label, payload, campaign_id, updated_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?)')
        .run('u_gm', 'c_0000000000000000000000', 'Ninefold', '{}', 'g_0000000000000000000000', 1000);

    db.prepare('DELETE FROM campaigns WHERE user_id = ? AND id = ?')
        .run('u_gm', 'g_0000000000000000000000');

    const rows = db.prepare('SELECT * FROM characters WHERE user_id = ?').all('u_gm');

    assert.equal(rows.length, 1);
    assert.equal(rows[0].campaign_id, 'g_0000000000000000000000');
});

// ── 0007: the decision itself, which the row kept only the consequence of ─────────────────

/** Every migration up to and including 0006 — the shape before a decision was recorded. */
function preDecisionDb() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 6)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

test('a membership written before 0007 survives it, undecided', () => {
    // **NULL is the right answer for those rows and not a missing value.** A membership from
    // before this migration has a decision that was made and not written down, or none at all,
    // and an ALTER cannot tell which — so it says nothing rather than guessing, and the browser
    // reads that as no decision. Defaulting to 'approved' would tell a player their last
    // rejection had been accepted.
    const db = preDecisionDb();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_p', 'p@example.test');

    db.prepare(
        'INSERT INTO campaign_members (id, campaign_id, gm_user_id, player_user_id, '
        + 'character_id, label, approved_payload, approved_at, pending_version, joined_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)')
        .run('m_0000000000000000000000', 'g_a', 'u_gm', 'u_p', 'c_x', 'Ninefold',
             '{"Sheet":{}}', 3000, 4, 1000);

    db.exec(readFileSync(MIGRATIONS[6], 'utf8'));

    const rows = db.prepare('SELECT * FROM campaign_members').all();

    assert.equal(rows.length, 1, 'the row must not be dropped — 0007 alters, it does not rebuild');
    assert.equal(rows[0].decision, null);
    assert.equal(rows[0].decided_at, null);

    // Everything the row already held is untouched, including the clone, byte for byte.
    assert.equal(rows[0].approved_payload, '{"Sheet":{}}');
    assert.equal(rows[0].approved_at, 3000);
    assert.equal(rows[0].pending_version, 4);
    assert.equal(rows[0].label, 'Ninefold');
});

test('0007 adds both columns and neither is NOT NULL', () => {
    const db = preDecisionDb();

    // The positive control: they are genuinely absent beforehand, or the assertion below would
    // hold for a migration that did nothing at all.
    const before = db.prepare('PRAGMA table_info(campaign_members)').all().map(c => c.name);

    assert.ok(!before.includes('decision'), before.join(', '));
    assert.ok(!before.includes('decided_at'), before.join(', '));

    db.exec(readFileSync(MIGRATIONS[6], 'utf8'));

    const after = db.prepare('PRAGMA table_info(campaign_members)').all();
    const named = Object.fromEntries(after.map(c => [c.name, c]));

    assert.ok(named.decision, 'no decision column');
    assert.ok(named.decided_at, 'no decided_at column');

    // NOT NULL on either would refuse every row that has never been decided, which is every row
    // at the moment somebody joins.
    assert.equal(named.decision.notnull, 0);
    assert.equal(named.decided_at.notnull, 0);
});

// ── 0006: the clone, the approval slot and the join code ─────────────────────────────────

/** Every migration up to and including 0005 — the shape before campaign membership. */
function preMembershipDb() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 5)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

function apply0006(db) {
    db.exec(readFileSync(MIGRATIONS[5], 'utf8'));
}

test('a campaign written before 0006 survives it, with no code until it is next written', () => {
    // **The ALTER cannot invent randomness for rows that already exist**, and that is the state
    // this pins: a campaign made before join codes has none, which the screen reports rather than
    // something that breaks. A `NOT NULL DEFAULT ''` would be worse — every such campaign would
    // share one code, and the unique index would refuse the second row.
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    db.prepare('INSERT INTO campaigns (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run('u_gm', 'g_0000000000000000000000', 'Nightfall', '{"Campaign":{}}', 2000);

    apply0006(db);

    const rows = db.prepare('SELECT * FROM campaigns WHERE user_id = ?').all('u_gm');

    assert.equal(rows.length, 1, 'the row must not be dropped — 0006 alters, it does not rebuild');
    assert.equal(rows[0].join_code, null);
    assert.equal(rows[0].label, 'Nightfall', 'the label survived the alter');
    assert.equal(rows[0].payload, '{"Campaign":{}}', 'byte for byte, unparsed');
    assert.equal(rows[0].updated_at, 2000);
});

test('any number of campaigns may have no code, and no two may share one', () => {
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    apply0006(db);

    const insert = (id, code) => db.prepare(
        'INSERT INTO campaigns (user_id, id, label, payload, join_code, updated_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?)').run('u_gm', id, 'A game', '{}', code, 1000);

    // NULLs are distinct in a unique index, which is what lets pre-0006 rows coexist.
    assert.doesNotThrow(() => insert('g_000000000000000000000a', null));
    assert.doesNotThrow(() => insert('g_000000000000000000000b', null));

    assert.doesNotThrow(() => insert('g_000000000000000000000c', 'ABCDEFGHJK'));
    assert.throws(() => insert('g_000000000000000000000d', 'ABCDEFGHJK'),
        'two campaigns sharing a code would make redeeming it ambiguous');

    // Across accounts too: a code is global, unlike an id, because it is redeemed by somebody who
    // does not know whose campaign it is.
    user(db, 'u_gm2', 'gm2@example.test');
    assert.throws(() => db.prepare(
        'INSERT INTO campaigns (user_id, id, label, payload, join_code, updated_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?)')
        .run('u_gm2', 'g_000000000000000000000e', 'A game', '{}', 'ABCDEFGHJK', 1000));
});

test('a membership is one per character per campaign, and a character id is not global', () => {
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_one', 'one@example.test');
    user(db, 'u_two', 'two@example.test');
    apply0006(db);

    const insert = (id, campaignId, playerUserId, characterId) => db.prepare(
        'INSERT INTO campaign_members '
        + '(id, campaign_id, gm_user_id, player_user_id, character_id, label, joined_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?, ?)')
        .run(id, campaignId, 'u_gm', playerUserId, characterId, 'Ninefold', 1000);

    insert('m_000000000000000000000a', 'g_a', 'u_one', 'c_x');

    // The same character in the same campaign twice is one membership.
    assert.throws(() => insert('m_000000000000000000000b', 'g_a', 'u_one', 'c_x'));

    // The positive controls: another player's row with the same character id is not a conflict —
    // so one account cannot squat on an id and block a join — and the same character in a second
    // campaign is not a conflict either.
    assert.doesNotThrow(() => insert('m_000000000000000000000c', 'g_a', 'u_two', 'c_x'));
    assert.doesNotThrow(() => insert('m_000000000000000000000d', 'g_b', 'u_one', 'c_x'));
});

test('a fresh membership has no clone, nothing waiting, and version zero', () => {
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_p', 'p@example.test');
    apply0006(db);

    db.prepare(
        'INSERT INTO campaign_members '
        + '(id, campaign_id, gm_user_id, player_user_id, character_id, label, joined_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?, ?)')
        .run('m_0000000000000000000000', 'g_a', 'u_gm', 'u_p', 'c_x', 'Ninefold', 1000);

    const row = db.prepare('SELECT * FROM campaign_members').all()[0];

    assert.equal(row.approved_payload, null, 'joining is not submitting');
    assert.equal(row.pending_payload, null);
    assert.equal(row.pending_version, 0, 'the compare-and-swap has to start somewhere');
});

test('the clones are not in characters, so they cannot count against a cap', () => {
    // **This is the whole reason 0006 adds a table rather than a column.** The cap in
    // `db.putCharacter` is `COUNT(*) FROM characters WHERE user_id = ?`; a clone stored there
    // would spend one of the GM's own five slots per player.
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_p', 'p@example.test');
    apply0006(db);

    for (let i = 0; i < 6; i++) {
        db.prepare(
            'INSERT INTO campaign_members (id, campaign_id, gm_user_id, player_user_id, '
            + 'character_id, label, approved_payload, approved_at, joined_at) '
            + 'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)')
            .run(`m_00000000000000000000${i}0`, 'g_a', 'u_gm', 'u_p', `c_${i}`,
                'Ninefold', '{}', 2000, 1000);
    }

    assert.equal(db.prepare('SELECT COUNT(*) AS n FROM campaign_members').all()[0].n, 6);
    assert.equal(db.prepare('SELECT COUNT(*) AS n FROM characters WHERE user_id = ?')
        .all('u_gm')[0].n, 0, 'six clones must leave the GM’s own five slots untouched');
});

test('deleting a campaign leaves its memberships, exactly as it leaves its characters', () => {
    // **No cascade from `campaigns`, on purpose and for the same reason 0005 has no foreign key on
    // `characters.campaign_id`.** A cascade would delete the clone the GM accepted, which is the
    // campaign's own record of what was agreed, on the strength of one click that may have been a
    // mistake. Restoring the campaign has to put everything back.
    const db = preMembershipDb();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_p', 'p@example.test');
    apply0006(db);

    db.prepare('INSERT INTO campaigns (user_id, id, label, payload, join_code, updated_at) '
        + 'VALUES (?, ?, ?, ?, ?, ?)')
        .run('u_gm', 'g_0000000000000000000000', 'Nightfall', '{}', 'ABCDEFGHJK', 1000);
    db.prepare(
        'INSERT INTO campaign_members (id, campaign_id, gm_user_id, player_user_id, '
        + 'character_id, label, approved_payload, joined_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)')
        .run('m_0000000000000000000000', 'g_0000000000000000000000', 'u_gm', 'u_p', 'c_x',
            'Ninefold', '{"kept":true}', 1000);

    db.prepare('DELETE FROM campaigns WHERE user_id = ? AND id = ?')
        .run('u_gm', 'g_0000000000000000000000');

    const rows = db.prepare('SELECT * FROM campaign_members').all();

    assert.equal(rows.length, 1);
    assert.equal(rows[0].approved_payload, '{"kept":true}');
});

test('deleting either account does cascade, unlike deleting the campaign', () => {
    // **The one place a cascade is right**, and it is different in kind from the campaign above:
    // a membership names two accounts and cannot mean anything with either of them gone. `users`
    // has no delete route at all today, so this is about the schema being coherent rather than
    // about a control anybody can press.
    const db = preMembershipDb();
    db.exec('PRAGMA foreign_keys = ON');
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_p', 'p@example.test');
    apply0006(db);

    const insert = () => db.prepare(
        'INSERT INTO campaign_members (id, campaign_id, gm_user_id, player_user_id, '
        + 'character_id, label, joined_at) VALUES (?, ?, ?, ?, ?, ?, ?)')
        .run('m_0000000000000000000000', 'g_a', 'u_gm', 'u_p', 'c_x', 'Ninefold', 1000);

    insert();
    db.prepare('DELETE FROM users WHERE id = ?').run('u_p');
    assert.equal(db.prepare('SELECT COUNT(*) AS n FROM campaign_members').all()[0].n, 0);

    // The positive control on the other column, or a cascade wired to one of the two would pass.
    user(db, 'u_p', 'p@example.test');
    insert();
    db.prepare('DELETE FROM users WHERE id = ?').run('u_gm');
    assert.equal(db.prepare('SELECT COUNT(*) AS n FROM campaign_members').all()[0].n, 0);
});

// ── 0008, which adds three columns a list needs and a payload read would otherwise cost ──────
//
// Against the *pre*-0008 schema, for the reason this whole file exists: the server harness applies
// every migration together, so it can never hold the database a real deployment is holding in the
// moment before this one runs.

/** A database with 0001–0007 applied — `characters` as it was before 0008. */
function pre0008Db() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 7)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

function apply0008(db) {
    db.exec(readFileSync(MIGRATIONS[7], 'utf8'));
}

/** A row in the pre-0008 shape: no `kind`, no `tier_id`, no `spent`. */
function character(db, userId, id, label, payload, updatedAt) {
    db.prepare(
        'INSERT INTO characters (user_id, id, label, payload, updated_at) VALUES (?, ?, ?, ?, ?)')
        .run(userId, id, label, payload, updatedAt);
}

test('an existing character survives 0008 with the three new columns null', () => {
    const db = pre0008Db();
    user(db, 'u_gm', 'gm@example.test');
    character(db, 'u_gm', 'c_a', 'Cael Hughes', '{"Sheet":{"Name":"Cael Hughes"}}', 2000);

    apply0008(db);

    const rows = db.prepare('SELECT * FROM characters WHERE user_id = ?').all('u_gm');

    assert.equal(rows.length, 1, 'no rebuild, so no row can be lost');
    assert.equal(rows[0].label, 'Cael Hughes');
    assert.equal(rows[0].payload, '{"Sheet":{"Name":"Cael Hughes"}}', 'byte for byte, unparsed');
    assert.equal(rows[0].updated_at, 2000);
    assert.equal(rows[0].kind, null);
    assert.equal(rows[0].tier_id, null);
    assert.equal(rows[0].spent, null, 'nothing is backfilled — that would mean parsing a payload');
});

test('0008 leaves a character in a campaign in its campaign', () => {
    // The positive control on the column 0005 added: a migration that rebuilt the table instead
    // of altering it could drop `campaign_id` and every assertion above would still pass.
    const db = pre0008Db();
    user(db, 'u_gm', 'gm@example.test');
    character(db, 'u_gm', 'c_a', 'Cael Hughes', '{}', 2000);
    db.prepare('UPDATE characters SET campaign_id = ? WHERE id = ?').run('g_ashfall', 'c_a');

    apply0008(db);

    assert.equal(
        db.prepare('SELECT campaign_id FROM characters WHERE id = ?').all('c_a')[0].campaign_id,
        'g_ashfall');
});

test('0008 leaves the account cap alone', () => {
    // `character_limit` is on `users` and this migration does not name that table — asserted
    // because a raised cap is the one piece of per-account state an owner sets by hand, and
    // losing it silently would look exactly like an account that had never been raised.
    const db = pre0008Db();
    user(db, 'u_gm', 'gm@example.test');
    db.prepare('UPDATE users SET character_limit = 40 WHERE id = ?').run('u_gm');

    apply0008(db);

    assert.equal(
        db.prepare('SELECT character_limit FROM users WHERE id = ?').all('u_gm')[0].character_limit,
        40);
});

// ── 0010, item 21's two columns for the variant tree a roster draws ──────────────────────────
//
// Against the *pre*-0010 schema, for the same reason 0008's own block is: the server harness
// applies every migration together, so it can never hold the database a real deployment is
// holding in the moment before this one runs.

/** A database with 0001–0009 applied — `characters` as it was before 0010. */
function pre0010Db() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 9)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

function apply0010(db) {
    db.exec(readFileSync(MIGRATIONS[9], 'utf8'));
}

test('an existing character survives 0010 with both new columns null, byte for byte', () => {
    const db = pre0010Db();
    user(db, 'u_gm', 'gm@example.test');
    character(db, 'u_gm', 'c_a', 'Cael Hughes', '{"Sheet":{"Name":"Cael Hughes"}}', 2000);

    apply0010(db);

    const rows = db.prepare('SELECT * FROM characters WHERE user_id = ?').all('u_gm');

    assert.equal(rows.length, 1, 'no rebuild, so no row can be lost');
    assert.equal(rows[0].label, 'Cael Hughes');
    assert.equal(rows[0].payload, '{"Sheet":{"Name":"Cael Hughes"}}', 'byte for byte, unparsed');
    assert.equal(rows[0].updated_at, 2000);
    assert.equal(rows[0].variant_of, null);
    assert.equal(rows[0].variant_kind, null, 'nothing is backfilled — that would mean parsing a payload');
});

test('0010 leaves the three 0008 index fields, a campaign and the account cap alone', () => {
    // The positive control on every earlier column: a migration that rebuilt the table instead
    // of altering it could drop any of them and every assertion above would still pass.
    const db = pre0010Db();
    user(db, 'u_gm', 'gm@example.test');
    db.prepare('UPDATE users SET character_limit = 40 WHERE id = ?').run('u_gm');
    character(db, 'u_gm', 'c_a', 'Cael Hughes', '{}', 2000);
    db.prepare('UPDATE characters SET campaign_id = ?, kind = ?, tier_id = ?, spent = ? WHERE id = ?')
        .run('g_ashfall', 'villain', 'high_level', 164, 'c_a');

    apply0010(db);

    const row = db.prepare('SELECT * FROM characters WHERE id = ?').all('c_a')[0];
    assert.equal(row.campaign_id, 'g_ashfall');
    assert.equal(row.kind, 'villain');
    assert.equal(row.tier_id, 'high_level');
    assert.equal(row.spent, 164);
    assert.equal(
        db.prepare('SELECT character_limit FROM users WHERE id = ?').all('u_gm')[0].character_limit,
        40);
});

// ── 0011, the nemesis handover's two columns on a membership ─────────────────────────────────
//
// Against the pre-0011 schema, for the reason 0010's block gives.

/** A database with 0001–0010 applied — `campaign_members` as it was before 0011. */
function pre0011Db() {
    const db = new DatabaseSync(':memory:');
    for (const migration of MIGRATIONS.slice(0, 10)) db.exec(readFileSync(migration, 'utf8'));

    return db;
}

test('a membership survives 0011 whole, with no kind and nothing handed over', () => {
    const db = pre0011Db();
    user(db, 'u_gm', 'gm@example.test');
    user(db, 'u_pl', 'player@example.test');
    db.prepare(
        'INSERT INTO campaign_members (id, campaign_id, gm_user_id, player_user_id, character_id, '
        + '  label, approved_payload, approved_at, pending_payload, pending_at, pending_version, '
        + '  joined_at, decision, decided_at) '
        + "VALUES ('m_a', 'g_a', 'u_gm', 'u_pl', 'c_a', 'Vesper', ?, 10, ?, 20, 3, 5, 'approved', 10)")
        .run('{"a":1}', '{"b":2}');

    db.exec(readFileSync(MIGRATIONS[10], 'utf8'));

    const row = db.prepare('SELECT * FROM campaign_members WHERE id = ?').all('m_a')[0];
    assert.equal(row.approved_payload, '{"a":1}', 'byte for byte, unparsed');
    assert.equal(row.pending_payload, '{"b":2}');
    assert.equal(row.pending_version, 3, 'the compare-and-swap token survives');
    assert.equal(row.decision, 'approved');
    assert.equal(row.pending_kind, null,
        'nothing is backfilled — a kind could only come from parsing the payload');
    assert.equal(row.handed_over_at, null, 'nothing written before 0011 was ever handed over');
});
