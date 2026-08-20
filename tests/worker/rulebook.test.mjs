// The book's own text, and who is allowed to read it.
//
// Two separate claims: the reader answers only a signed-in caller, and the corpus is bundled
// into the server rather than copied into `wwwroot`, which is what makes that gate the only
// way in.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { index } from '../../worker/rulebook.js';
import { server, signIn } from './harness.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

test('a signed-in reader gets a Power’s printed entry', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const response = await app.call('/api/rulebook/power?name=Armor', { cookie });

    assert.equal(response.status, 200);

    const entry = await response.json();
    assert.equal(entry.heading, 'ARMOR');
    assert.ok(entry.printedPage > 0);
    assert.match(entry.sourceRef, /Ultimate Edition/);

    // The stat line is what ties the body to its own heading — the same join
    // `RulebookCorpusTests` holds the corpus to across a hundred and sixteen entries.
    assert.match(entry.text, /^Self •/);
});

test('the name is matched however it is cased or spaced', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (const name of ['Armor', 'armor', 'ARMOR', '  Armor  ']) {
        assert.equal((await app.call('/api/rulebook/power?name=' + encodeURIComponent(name),
            { cookie })).status, 200, name);
    }
});

test('a name the book has no entry for is a plain 404', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Not an error state: Super Senses' options are separate entries in the rules data and
    // share one printed entry, so a good fraction of the Powers have no heading of their own.
    assert.equal((await app.call('/api/rulebook/power?name=Acute%20Hearing', { cookie })).status, 404);
    assert.equal((await app.call('/api/rulebook/power?name=Elasticity', { cookie })).status, 404);
    assert.equal((await app.call('/api/rulebook/power', { cookie })).status, 400);
});

test('nobody signed in reads a word of it', async () => {
    const app = server();

    // The positive control first: signed in, this exact address answers 200.
    const { cookie } = await signIn(app, 'a@b.test');
    assert.equal((await app.call('/api/rulebook/power?name=Armor', { cookie })).status, 200);

    const refused = await app.call('/api/rulebook/power?name=Armor');

    assert.equal(refused.status, 401);
    assert.ok(!(await refused.text()).includes('Self •'));
});

test('an expired session reads nothing either', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.now += 31 * 24 * 60 * 60 * 1000;

    assert.equal((await app.call('/api/rulebook/power?name=Armor', { cookie })).status, 401);
});

test('the corpus is not in the browser payload', () => {
    // The whole access control is where the file is. A copy under wwwroot is a public URL, and
    // no amount of checking sessions in this directory would make it not be one.
    //
    // **`wwwroot/data` is staged by the build and is gitignored**, so on a checkout nobody has
    // built it does not exist at all — which used to throw ENOENT and read as this guard failing.
    // A missing directory genuinely satisfies the claim: nothing is staged, so the corpus is not.
    // It is reported rather than silently passed, because "there was nothing to check" and "I
    // checked and it was fine" are different answers and only one of them is evidence.
    const staged = join(root, 'web', 'wwwroot', 'data');

    if (!existsSync(staged)) {
        console.log('    (nothing staged into wwwroot yet — build web/ to make this a real check)');
        return;
    }

    const directories = readdirSync(staged, { withFileTypes: true })
        .filter(e => e.isDirectory()).map(e => e.name);

    assert.deepEqual(directories.sort(), ['rules', 'transcripts'],
        'something has staged another data directory into the site; if it is the rulebook, '
        + 'the book is now on the open web');
});

test('the index takes the first of two sections sharing a heading', () => {
    const entries = index([{
        source_ref: 'test',
        sections: [
            { heading: 'ARMOR', printed_page: 21, text: 'the entry' },
            { heading: 'Armor', printed_page: 60, text: 'a table of the same name' },
        ],
    }]);

    assert.equal(entries.get('ARMOR').text, 'the entry');
});

test('every chapter the bake reads is one the repository generated', () => {
    // `scripts/inline-rulebook.mjs` reads one chapter file — adding a chapter is a decision about
    // what an account is entitled to read, not a glob. This is what fails if a chapter is added
    // by hand rather than taken from the extractor's output.
    //
    // (The point moved from `worker/corpus.js` to the bake script when the JSON stopped being
    // *imported* and started being *inlined*, for the reasons in the sync test above. The claim
    // and its guard are unchanged; only the file the claim lives in is different.)
    const source = readFileSync(join(root, 'scripts', 'inline-rulebook.mjs'), 'utf8');
    const named = [...source.matchAll(/['"]data\/rulebook\/([^'"]+)['"]|'data', 'rulebook', ['"]([^'"]+)['"]/g)]
        .flatMap(m => [m[1], m[2]]).filter(Boolean);

    assert.ok(named.length > 0, 'the bake script names no chapter at all');

    const onDisk = readdirSync(join(root, 'data', 'rulebook'));
    for (const file of named) assert.ok(onDisk.includes(file), file + ' is not in data/rulebook/');
});
