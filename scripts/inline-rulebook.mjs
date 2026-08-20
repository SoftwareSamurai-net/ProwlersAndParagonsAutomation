// Bake `data/rulebook/ch02-characters.json` into `worker/corpus.js`.
//
// **This exists because two ways of *importing* the JSON both broke somewhere.** Neither was
// wrong; the deploy pipeline and the test runner disagreed about which one they can parse:
//
// * `import chapterTwo from '../data/rulebook/ch02-characters.json' with { type: 'json' }` —
//   the current spec syntax. Node 22 runs it; wrangler 3.90's bundled esbuild refuses it
//   ("Expected ';' but found 'with'"), so `pages deploy` errors before it uploads anything.
// * `assert { type: 'json' }` — the older spelling. wrangler accepts it; Node 22 warns loudly
//   about a deprecation, and it is going to be removed. Trading a broken deploy for a broken
//   test run.
//
// Baking sidesteps both. It runs before every push (see `.github/workflows/deploy.yml`), is
// deterministic — the input is committed and the output is checked in a guard test — and keeps
// the Workers entry point synchronous, which it has to be.
//
// **Do not hand-edit `worker/corpus.js` afterwards.** The guard in `tests/worker/router.test.mjs`
// re-reads the source and refuses a stale bake, so the file the deploy uploads and the file the
// tests exercise cannot disagree.

import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');

const source = await readFile(join(repo, 'data', 'rulebook', 'ch02-characters.json'), 'utf8');

const parsed = JSON.parse(source);   // fail loud rather than bake malformed JSON

const target = join(repo, 'worker', 'corpus.js');
const existing = await readFile(target, 'utf8');

// One line replaced, everything else preserved. The literal `CHAPTER_TWO_PLACEHOLDER` in the
// source file is the marker — do not remove it.
const replaced = existing.replace(
    /const CHAPTER_TWO = .*?;$/m,
    `const CHAPTER_TWO = ${JSON.stringify(parsed)};`,
);

if (replaced === existing) {
    console.error('worker/corpus.js has no CHAPTER_TWO line to replace. Was it hand-edited?');
    process.exit(1);
}

await writeFile(target, replaced);

console.log(`Baked ${source.length.toLocaleString()} bytes of Chapter 2 into worker/corpus.js.`);
