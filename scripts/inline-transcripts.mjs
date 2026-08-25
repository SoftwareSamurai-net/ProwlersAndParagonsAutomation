// Bake every recording in `data/transcripts/` into `worker/transcripts-corpus.js`.
//
// The same reason as `scripts/inline-rulebook.mjs`, which this mirrors: two spec-correct ways of
// importing the JSON both break somewhere. `import ... with { type: 'json' }` is not understood by
// wrangler 3.90's bundled esbuild, so the deploy fails after the branch has already been reviewed;
// `assert { type: 'json' }` is the older spelling, loudly deprecated on the Node 22 the tests run
// on. Baking sidesteps both — the input is committed, the output is checked in a guard test, and
// the Workers entry point stays synchronous, which it has to be.
//
// **Read the directory rather than naming files**, for the same reason as the rulebook's bake:
// a filename written into this script is a list that goes stale the day a recording is added, and
// the failure would be a recording silently missing from `/api/transcripts` rather than anything
// that looks broken. `TranscriptTests` in the engine suite pins `engine/TranscriptLibrary.FileNames`
// to this same directory listing, so the two cannot quietly drift apart either.
//
// **Do not hand-edit `worker/transcripts-corpus.js` afterwards.** A guard in
// `tests/worker/transcripts.test.mjs` re-reads every file in `data/transcripts/` and refuses a
// stale bake, so the file the deploy uploads and the file the tests exercise cannot disagree.

import { readdir, readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');
const from = join(repo, 'data', 'transcripts');

// Sorted, so the bake is byte-identical run to run and a diff of `worker/transcripts-corpus.js`
// shows a change to a recording rather than a change to whatever order the filesystem answered in.
const names = (await readdir(from)).filter(n => n.endsWith('.json')).sort();

if (names.length === 0) {
    console.error(`No recordings found in ${from}.`);
    process.exit(1);
}

const files = {};

for (const name of names) {
    const source = await readFile(join(from, name), 'utf8');
    files[name] = JSON.parse(source);   // fail loud rather than bake a malformed recording
}

const target = join(repo, 'worker', 'transcripts-corpus.js');
const existing = await readFile(target, 'utf8');

// One line replaced, everything else preserved. The literal `BAKED_TRANSCRIPTS` in the source
// file is the marker — do not remove it.
const replaced = existing.replace(
    /const BAKED_TRANSCRIPTS = .*?;$/m,
    `const BAKED_TRANSCRIPTS = ${JSON.stringify(files)};`,
);

if (replaced === existing) {
    console.error(
        'worker/transcripts-corpus.js has no BAKED_TRANSCRIPTS line to replace. Was it hand-edited?');
    process.exit(1);
}

await writeFile(target, replaced);

console.log(`Baked ${names.length} recordings into worker/transcripts-corpus.js.`);
