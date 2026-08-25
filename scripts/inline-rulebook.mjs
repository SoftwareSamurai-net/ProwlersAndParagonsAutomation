// Bake every chapter in `data/rulebook/` into `worker/corpus.js`.
//
// **This exists because two ways of *importing* the JSON both broke somewhere.** Neither was
// wrong; the deploy pipeline and the test runner disagreed about which one they can parse:
//
// * `import chapter from '../data/rulebook/ch02-characters.json' with { type: 'json' }` —
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
// **All ten chapters, found by reading the directory rather than named here.** It was Chapter 2
// alone while the only reader was a Power's entry beside the Powers editor. It is the whole book
// now because the reference searches the whole book, and a list of filenames in this script is a
// list that goes stale the first time a chapter is added — the extractor decides what chapters
// there are, and this reads whatever it wrote.
//
// **Do not hand-edit `worker/corpus.js` afterwards.** The guard in `tests/worker/router.test.mjs`
// re-reads every source file and refuses a stale bake, so the file the deploy uploads and the
// files the tests exercise cannot disagree.

import { readdir, readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');
const from = join(repo, 'data', 'rulebook');

// Sorted, so the bake is byte-identical run to run and a diff of `worker/corpus.js` shows a
// change to the book rather than a change to whatever order the filesystem answered in. The
// names begin `ch00`…`ch09`, so this is reading order.
const names = (await readdir(from)).filter(n => n.endsWith('.json')).sort();

if (names.length === 0) {
    console.error(`No chapters found in ${from}. Has the extractor been run?`);
    process.exit(1);
}

const chapters = [];

for (const name of names) {
    const source = await readFile(join(from, name), 'utf8');
    chapters.push(JSON.parse(source));   // fail loud rather than bake malformed JSON
}

const target = join(repo, 'worker', 'corpus.js');
const existing = await readFile(target, 'utf8');

// One line replaced, everything else preserved. The literal `BAKED_CHAPTERS` in the source file
// is the marker — do not remove it.
const replaced = existing.replace(
    /const BAKED_CHAPTERS = .*?;$/m,
    `const BAKED_CHAPTERS = ${JSON.stringify(chapters)};`,
);

if (replaced === existing) {
    console.error('worker/corpus.js has no BAKED_CHAPTERS line to replace. Was it hand-edited?');
    process.exit(1);
}

await writeFile(target, replaced);

const sections = chapters.reduce((total, c) => total + c.sections.length, 0);

console.log(
    `Baked ${names.length} chapters and ${sections.toLocaleString()} sections `
    + `into worker/corpus.js.`);
