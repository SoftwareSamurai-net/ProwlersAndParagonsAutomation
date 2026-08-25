// The parts of the server that are not about any one feature.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { handle } from '../../worker/index.js';
import { ORIGIN, request, server, signIn } from './harness.mjs';

test('an address nobody defined is a 404, not a stack trace', async () => {
    const app = server();

    assert.equal((await app.call('/api/nothing-here')).status, 404);
    assert.equal((await app.call('/api/character/7', { method: 'GET' })).status, 404);
});

test('the wrong method is refused and says what is allowed', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const got = await app.call('/api/auth/request', { cookie });
    assert.equal(got.status, 405);
    assert.equal(got.headers.get('allow'), 'POST');

    // `/api/character` (singular) was the one-character-per-account address; the characters
    // slice replaced it with `/api/characters/{id}`, so this now exercises the same "wrong
    // method on a route that needs a user" shape at the new address.
    const posted = await app.call('/api/characters/c_0000000000000000000000',
        { method: 'POST', body: {}, cookie });
    assert.equal(posted.status, 405);
    assert.match(posted.headers.get('allow'), /PUT/);
});

test('nothing this server says is cacheable', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    for (const path of ['/api/me', '/api/character', '/api/rulebook/power?name=Armor', '/api/nope']) {
        const response = await app.call(path, { cookie });

        assert.equal(response.headers.get('cache-control'), 'no-store', path);
    }
});

test('a failure inside says nothing about what failed', async () => {
    // The database is replaced with one that throws whatever D1 would throw, quoting a query.
    const app = server();
    const exploding = {
        prepare() {
            throw new Error('D1_ERROR: near "SELECT": syntax error at users.email = dorian@example.test');
        },
    };

    const response = await handle(request('/api/me', { cookie: 'pp_session=x' }),
        { ...app.env, DB: exploding }, app.deps);

    assert.equal(response.status, 500);

    const said = await response.text();
    assert.ok(!said.includes('D1_ERROR'), said);
    assert.ok(!said.includes('dorian@example.test'), said);
});

test('a trailing slash is the same address', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    assert.equal((await app.call('/api/me/', { cookie })).status, 200);
});

test('the inlined rulebook is in step with data/rulebook/', async () => {
    // **Baked, not imported.** `worker/corpus.js` inlines Chapter 2 as an object literal because
    // both spec spellings of a JSON import broke somewhere: `with { type: 'json' }` is not
    // understood by wrangler 3.90's bundled esbuild, so the deploy went red after this branch
    // was already reviewed; the older `assert { type: 'json' }` is loudly deprecated in Node 22.
    // See `scripts/inline-rulebook.mjs`.
    //
    // The trap is that a stale bake looks exactly like a fresh one until somebody reads the
    // book. This asserts the bake is byte-for-byte what the extractor last produced, so a
    // regenerated corpus that nobody re-baked fails on the PR rather than on the deploy after.
    const { readFile } = await import('node:fs/promises');
    const { fileURLToPath } = await import('node:url');
    const { dirname, join } = await import('node:path');

    const here = dirname(fileURLToPath(import.meta.url));
    const repo = join(here, '..', '..');

    const { readdir } = await import('node:fs/promises');

    const from = join(repo, 'data', 'rulebook');
    const names = (await readdir(from)).filter(n => n.endsWith('.json')).sort();

    const onDisk = [];
    for (const name of names) onDisk.push(JSON.parse(await readFile(join(from, name), 'utf8')));

    const { CHAPTERS } = await import('../../worker/corpus.js');

    // **Every chapter, not a sample of them.** A bake that dropped one would leave the search
    // quietly unable to answer a whole chapter's questions, which reads exactly like the book not
    // covering the subject. The count is asserted separately from the contents so that a missing
    // chapter says so rather than arriving as a diff of fifteen hundred sections.
    assert.equal(CHAPTERS.length, onDisk.length,
        `worker/corpus.js holds ${CHAPTERS.length} chapters and data/rulebook/ has ${onDisk.length}. `
        + 'Re-run: node scripts/inline-rulebook.mjs');

    assert.deepEqual(CHAPTERS, onDisk,
        'worker/corpus.js is out of step with data/rulebook/. '
        + 'Re-run: node scripts/inline-rulebook.mjs');
});


test('the routed entry point actually loads', async () => {
    // **Nothing else in this suite loads it.** Every other test imports `worker/index.js`
    // directly, so a broken path in the one file Cloudflare routes — or a corpus import that
    // does not resolve — would fail at deploy time and nowhere before it. Importing it here
    // exercises the whole graph, including `corpus.js` pulling a 250KB JSON module in with an
    // import attribute.
    const mod = await import('../../functions/api/[[path]].js');

    assert.equal(typeof mod.onRequest, 'function');
});

test('the routed file is a shim and holds no logic', async () => {
    // `functions/api/[[path]].js` is the only file Cloudflare routes. Everything reachable is
    // reachable through it, so it staying four lines is what keeps "what is exposed?" a
    // question with one answer.
    const { readFileSync } = await import('node:fs');
    const { fileURLToPath } = await import('node:url');
    const { dirname, join } = await import('node:path');

    const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
    const source = readFileSync(join(root, 'functions', 'api', '[[path]].js'), 'utf8');

    const code = source.split('\n')
        .map(line => line.trim())
        .filter(line => line && !line.startsWith('//'));

    assert.deepEqual(code, [
        "import { handle } from '../../worker/index.js';",
        'export const onRequest = ({ request, env }) => handle(request, env);',
    ]);
});

test('the origin check is on the site’s own origin, not on any origin at all', async () => {
    const app = server();

    // A subdomain and a scheme change are both different origins, and both are refused. Without
    // this the check could be satisfied by "there is an Origin header", which is not a check.
    for (const origin of [ORIGIN.replace('https', 'http'), 'https://evil.' + new URL(ORIGIN).host]) {
        const response = await app.call('/api/auth/request',
            { method: 'POST', body: { email: 'a@b.test' }, origin });

        assert.equal(response.status, 403, origin);
    }
});
