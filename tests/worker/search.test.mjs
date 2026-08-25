// Searching the book, and the one property the matching rule exists for.
//
// **A search that always answers is worse than one that admits it found nothing.** The MCP
// server's Power search learned this the expensive way: substring matching answered "she bakes
// bread in the city" with Plasticity, and a wrong match that looks plausible costs more than a
// miss. That rule is ported here, and this file is where it is held.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { CHAPTERS } from '../../worker/corpus.js';
import { search, terms } from '../../worker/search.js';
import { server, signIn } from './harness.mjs';

const headings = result => result.results.map(r => r.heading.toUpperCase());

/**
 * Every fragment that occurs *inside* a heading and is not a word of its own, with the word that
 * proves the search still works. A substring search matches the left column; this one must not.
 *
 * **The positive control is not optional.** Every assertion in the left column is an absence, and
 * an absence is satisfied completely by a search that has stopped returning anything at all —
 * which is the failure shape this repository has shipped four times.
 */
const NOT_A_WORD_INSIDE_A_HEADING = [
    ['city', 'PLASTICITY', 'plasticity'],
    ['ration', 'REGENERATION', 'regeneration'],
    ['kinesis', 'TELEKINESIS', 'telekinesis'],
];

test('the baker’s sentence does not reach Plasticity', () => {
    const found = search(CHAPTERS, 'she bakes bread in the city', 30);

    // **Not `found: 0`, and an earlier version of this file asserted that.** Over in the MCP
    // server the haystack is 141 short Power entries and the sentence genuinely matches nothing.
    // Here it is the whole book, where "city" is a word the text really uses — *City of Heroes*
    // in the introduction, "a city, forest, jungle" in Attuned. Those are real occurrences of a
    // word somebody typed and returning them is correct. What must not happen is the match that
    // is not a word at all.
    assert.ok(found.found > 0, 'the search returned nothing at all, so this proves nothing');
    assert.ok(!headings(found).includes('PLASTICITY'),
        'substring matching is back: "city" reached Plasticity. Found: ' + headings(found).join(', '));

    // And every passage it did return matched inside its body rather than its heading, which is
    // the honest reading of a sentence the book has no section about.
    assert.equal(found.nothingMatchedByHeading, true);
});

for (const [fragment, heading, control] of NOT_A_WORD_INSIDE_A_HEADING) {
    test(`"${fragment}" is not a word of ${heading}`, () => {
        assert.ok(!headings(search(CHAPTERS, fragment, 30)).includes(heading),
            `"${fragment}" reached ${heading}, which is substring matching`);

        // The control: the real word still finds it, so the absence above means something.
        assert.ok(headings(search(CHAPTERS, control, 30)).includes(heading),
            `"${control}" no longer finds ${heading} — the search itself is broken`);
    });
}

test('a word with an English ending still finds its entry', () => {
    // The whole reason the rule is not plain equality: what a reader types is rarely the word the
    // book set as a heading.
    assert.ok(headings(search(CHAPTERS, 'invisible', 30)).includes('INVISIBILITY'));
    assert.ok(headings(search(CHAPTERS, 'regenerates', 30)).includes('REGENERATION'));
});

test('a heading match comes before a passage that merely mentions it', () => {
    const found = search(CHAPTERS, 'knockback', 10);

    assert.ok(found.results.length > 1, 'needs more than one result to say anything about order');
    assert.equal(found.results[0].matchedHeading, true,
        'the first result matched only in its body: ' + found.results[0].heading);
});

test('a passage using both words beats one using the commoner word repeatedly', () => {
    const found = search(CHAPTERS, 'trait cap', 10);

    assert.ok(found.results.length > 0);
    assert.equal(found.results[0].matchedTerms.length, 2,
        'the best result matched one term only: ' + found.results[0].heading);
});

test('nothing but filler is nothing searched for', () => {
    for (const query of ['', '   ', 'the and but', 'a', '???']) {
        const found = search(CHAPTERS, query, 10);

        assert.equal(found.found, 0, query);
        assert.deepEqual(found.results, [], query);
    }
});

test('a word the book does not use finds nothing, and says so plainly', () => {
    const found = search(CHAPTERS, 'photosynthesis', 10);

    assert.equal(found.found, 0);
    assert.equal(found.nothingMatchedByHeading, true);
});

test('whether anything matched by heading is decided before the list is cut', () => {
    // **Computed after the cut, a caller asking for one row turns a heading match at position two
    // into "nothing matched by heading at all".** That is the exact bug the Powers search shipped,
    // and the flag is the part of the answer a caller acts on.
    const wide = search(CHAPTERS, 'knockback', 30);
    const narrow = search(CHAPTERS, 'knockback', 1);

    assert.equal(wide.nothingMatchedByHeading, false, 'no heading matched, so this proves nothing');
    assert.equal(narrow.nothingMatchedByHeading, wide.nothingMatchedByHeading);
    assert.equal(narrow.found, wide.found, 'the count is of what was found, not of what was shown');
    assert.equal(narrow.results.length, 1);
});

test('the snippet is cut around the match, not off the front of the passage', () => {
    const found = search(CHAPTERS, 'knockback', 5);
    const inBody = found.results.find(r => !r.matchedHeading && r.snippet.startsWith('…'));

    assert.ok(inBody, 'no result matched deep inside a passage, so this proves nothing');

    // **The window centres on the word that matched, which is not always the word typed.** The
    // passage this lands on says "knocked around like tenpins" — "knocked" and "knockback" share
    // five letters, which is the shared-prefix rule doing exactly its job. Asserting the typed
    // word appears would be asserting that the rule does not work.
    assert.match(inBody.snippet.toLowerCase(), /knock/);

    // And it really is a window: an ellipsis at the front means text was cut from before it, so
    // this is not the opening of the passage dressed up as an answer.
    assert.ok(inBody.snippet.startsWith('…'));
});

test('a made-up corpus is searched, not the real one cached from a previous call', () => {
    // The index is cached per corpus. Cached on "have I built one at all", every test below this
    // line would have been answered out of the book's own index — plausibly, and wrongly.
    const invented = [{
        chapter: 99,
        title: 'Invented',
        source_ref: 'nowhere',
        sections: [{ heading: 'FLIBBERTIGIBBET', printed_page: 1, text: 'A word found nowhere else.' }],
    }];

    const found = search(invented, 'flibbertigibbet', 5);

    assert.equal(found.found, 1);
    assert.equal(found.results[0].heading, 'FLIBBERTIGIBBET');

    // And the real corpus is still itself afterwards.
    assert.ok(search(CHAPTERS, 'knockback', 5).found > 0);
});

test('terms drops the filler and keeps each word once', () => {
    assert.deepEqual(terms('How does the Trait Cap work, and how does it not?'),
        ['trait', 'cap', 'work']);
});

// ── Over the wire ─────────────────────────────────────────────────────────

test('nobody signed in searches a word of it', async () => {
    const app = server();

    // The positive control first: signed in, this exact address answers 200 with real results.
    const { cookie } = await signIn(app, 'a@b.test');
    const allowed = await app.call('/api/rulebook/search?q=knockback', { cookie });

    assert.equal(allowed.status, 200);
    assert.ok((await allowed.json()).found > 0);

    const refused = await app.call('/api/rulebook/search?q=knockback');

    assert.equal(refused.status, 401);
    assert.ok(!(await refused.text()).toLowerCase().includes('knock someone across'));
});

test('nobody signed in reads the contents or a passage either', async () => {
    const app = server();

    for (const address of ['/api/rulebook/contents', '/api/rulebook/passage?chapter=2&index=0']) {
        assert.equal((await app.call(address)).status, 401, address);
    }

    // The control: each of them answers a signed-in caller.
    const { cookie } = await signIn(app, 'a@b.test');

    for (const address of ['/api/rulebook/contents', '/api/rulebook/passage?chapter=2&index=0']) {
        assert.equal((await app.call(address, { cookie })).status, 200, address);
    }
});

test('an address under the rulebook prefix that is not routed is a 404, not a way in', async () => {
    const app = server();

    // Signed out it is refused before the router ever looks at which address it was — the gate is
    // on the prefix, so a route added below the check cannot be reachable above it.
    assert.equal((await app.call('/api/rulebook/everything')).status, 401);

    const { cookie } = await signIn(app, 'a@b.test');
    assert.equal((await app.call('/api/rulebook/everything', { cookie })).status, 404);
});

test('the contents name every chapter that was baked', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const body = await (await app.call('/api/rulebook/contents', { cookie })).json();

    assert.equal(body.chapters.length, CHAPTERS.length);
    assert.equal(body.sections, CHAPTERS.reduce((n, c) => n + c.sections.length, 0));

    for (const chapter of body.chapters) {
        assert.ok(chapter.sections > 0, 'chapter ' + chapter.chapter + ' has no sections');
        assert.match(chapter.sourceRef, /Ultimate Edition/);
    }
});

test('a passage is fetched by where it is, and a bad address says which kind of bad', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const found = search(CHAPTERS, 'knockback', 1).results[0];

    const body = await (await app.call(
        `/api/rulebook/passage?chapter=${found.chapter}&index=${found.index}`, { cookie })).json();

    assert.equal(body.heading, found.heading);
    assert.equal(body.printedPage, found.printedPage);
    assert.ok(body.text.length > 0);

    // Not a number at all is the caller's mistake; a number naming nothing is a miss.
    assert.equal((await app.call('/api/rulebook/passage?chapter=two&index=0', { cookie })).status, 400);
    assert.equal((await app.call('/api/rulebook/passage?chapter=99&index=0', { cookie })).status, 404);
    assert.equal((await app.call('/api/rulebook/passage?chapter=2&index=99999', { cookie })).status, 404);
});

test('a search with no query at all is refused rather than answered with everything', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    assert.equal((await app.call('/api/rulebook/search', { cookie })).status, 400);

    // An empty query is a different thing from no query: somebody has cleared the box.
    const empty = await app.call('/api/rulebook/search?q=', { cookie });
    assert.equal(empty.status, 200);
    assert.equal((await empty.json()).found, 0);
});

test('the caller cannot ask for the whole book in one response', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const body = await (await app.call('/api/rulebook/search?q=resolve&limit=5000', { cookie })).json();

    assert.ok(body.found > 30, 'this query no longer matches enough to test the cap');
    assert.ok(body.results.length <= 30, 'the limit is caller-controlled: ' + body.results.length);
});
