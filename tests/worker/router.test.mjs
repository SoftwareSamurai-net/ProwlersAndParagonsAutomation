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

    const posted = await app.call('/api/character', { method: 'POST', body: {}, cookie });
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
