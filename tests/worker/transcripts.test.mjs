// The recorded conversations, and who is allowed to read them.
//
// Two separate claims, the same shape as `rulebook.test.mjs`: the reader answers only a
// signed-in caller, and the recordings are bundled into the server rather than copied into
// `wwwroot`, which is what makes that gate the only way in.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { TRANSCRIPTS } from '../../worker/transcripts-corpus.js';
import { server, signIn } from './harness.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

// A sentence that occurs in one recording and nowhere else in this test file's own text, so a
// match on it really means the body carried a transcript rather than this file's own wording.
const DISTINCTIVE_TEXT = 'chip shop on the corner';

test('a signed-in reader gets every recording', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const response = await app.call('/api/transcripts', { cookie });
    assert.equal(response.status, 200);

    const body = await response.json();

    assert.deepEqual(Object.keys(body).sort(), Object.keys(TRANSCRIPTS).sort());
    assert.ok(JSON.stringify(body).includes(DISTINCTIVE_TEXT));
});

test('nobody signed in reads a word of it', async () => {
    const app = server();

    // The positive control first: signed in, this exact address answers 200 and carries the
    // recordings.
    const { cookie } = await signIn(app, 'a@b.test');
    const allowed = await app.call('/api/transcripts', { cookie });
    assert.equal(allowed.status, 200);
    assert.ok((await allowed.text()).includes(DISTINCTIVE_TEXT));

    const refused = await app.call('/api/transcripts');

    assert.equal(refused.status, 401);
    assert.ok(!(await refused.text()).includes(DISTINCTIVE_TEXT));
});

test('an expired session reads nothing either', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    app.now += 31 * 24 * 60 * 60 * 1000;

    const refused = await app.call('/api/transcripts', { cookie });
    assert.equal(refused.status, 401);
    assert.ok(!(await refused.text()).includes(DISTINCTIVE_TEXT));
});

test('the wrong method is refused', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const posted = await app.call('/api/transcripts', { method: 'POST', cookie });
    assert.equal(posted.status, 405);
    assert.equal(posted.headers.get('allow'), 'GET');
});

test('the bake is byte-for-byte the recordings on disk', async () => {
    // **The trap is that a stale bake looks exactly like a fresh one until somebody reads a
    // recording.** This asserts the bake is what `data/transcripts/` actually holds, so an edited
    // recording that nobody re-baked fails on the PR rather than in front of whoever opens it.
    const from = join(root, 'data', 'transcripts');
    const names = (await readdir(from)).filter(n => n.endsWith('.json')).sort();

    const onDisk = {};
    for (const name of names) onDisk[name] = JSON.parse(await readFile(join(from, name), 'utf8'));

    assert.deepEqual(Object.keys(TRANSCRIPTS).sort(), names,
        `worker/transcripts-corpus.js holds ${Object.keys(TRANSCRIPTS).length} recordings and `
        + `data/transcripts/ has ${names.length}. Re-run: node scripts/inline-transcripts.mjs`);

    assert.deepEqual(TRANSCRIPTS, onDisk,
        'worker/transcripts-corpus.js is out of step with data/transcripts/. '
        + 'Re-run: node scripts/inline-transcripts.mjs');
});

test('the bake names no recording of its own', () => {
    // Same shape as the rulebook's own version of this test: a filename written into the bake
    // script is a list that goes stale the day a recording is added, and the failure would be a
    // recording silently missing from `/api/transcripts` rather than anything visibly broken.
    const source = readFile(join(root, 'scripts', 'inline-transcripts.mjs'), 'utf8');

    return source.then(text => {
        const named = [...text.matchAll(/['"][a-z-]+\.json['"]/g)];

        assert.equal(named.length, 0,
            'scripts/inline-transcripts.mjs names a recording file: '
            + named.map(m => m[0]).join(', ')
            + '. It should read data/transcripts/ instead, so a new recording needs no edit here.');
    });
});

test('the corpus is not in the browser payload', () => {
    // The whole access control is where the file is — see rulebook.test.mjs's version of this
    // claim, which now also covers this store. `wwwroot/data` is staged by the build and is
    // gitignored, so on a checkout nobody has built it does not exist at all, and a missing
    // directory genuinely satisfies the claim.
    const staged = join(root, 'web', 'wwwroot', 'data');

    return import('node:fs').then(({ existsSync, readdirSync }) => {
        if (!existsSync(staged)) {
            console.log('    (nothing staged into wwwroot yet — build web/ to make this a real check)');
            return;
        }

        const directories = readdirSync(staged, { withFileTypes: true })
            .filter(e => e.isDirectory()).map(e => e.name);

        assert.ok(!directories.includes('transcripts'),
            'wwwroot/data/transcripts exists — the recordings are on the open web again.');
    });
});
