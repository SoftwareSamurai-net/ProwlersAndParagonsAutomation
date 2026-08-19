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
