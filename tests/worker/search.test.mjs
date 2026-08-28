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
import { search as scopedSearch } from '../../worker/rulebook.js';
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

// ── One chapter at a time ─────────────────────────────────────────────────
//
// **The narrowing is on this side because it cannot honestly be on the other.** `MOST_RESULTS` is
// 30 and no caller can raise it, so a browser keeping only one chapter's rows out of the answer
// would be filtering what survived the cap — and `found` would be a count of the whole book
// presented as a count of the chapter. That is the fault the `/rules` page is designed against,
// and the tests below are the three properties that make the server version not have it.

/**
 * A query and a chapter where the whole-book answer never reaches that chapter at all.
 *
 * **Chosen by measurement, not by taste.** A script ran twenty ordinary queries against the real
 * corpus and asked, for each chapter, whether it had matches and yet appeared nowhere in the
 * unscoped top thirty. This pair had the widest margin of any it found: Ch.8 is the largest
 * chapter in the book at 742 sections and holds forty passages using the word, and the unscoped
 * search fills its thirty rows entirely out of Ch.2 and Ch.6 before reaching one of them. So a
 * reader who wants the vehicles in *Friends and Foes* cannot get there by asking for more rows.
 */
const OUT_OF_REACH = { query: 'vehicle', chapter: 8 };

test('a scoped search answers out of that chapter and no other', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const body = await (await app.call(
        '/api/rulebook/search?q=knockback&chapter=4', { cookie })).json();

    // The positive control, and it is the whole test: every assertion after this is satisfied
    // completely by a search that has stopped returning anything at all.
    assert.ok(body.results.length > 0, 'the scoped search returned nothing, so this proves nothing');

    for (const row of body.results) {
        assert.equal(row.chapter, 4, `a Ch.${row.chapter} passage came back from a Ch.4 search`);
    }

    // And the whole-book answer to the same query really does span more than one chapter, so the
    // absence above is the scoping and not the corpus happening to hold it all in one place.
    const wide = await (await app.call('/api/rulebook/search?q=knockback', { cookie })).json();

    assert.ok(new Set(wide.results.map(r => r.chapter)).size > 1,
        'the unscoped search only reaches one chapter, so scoping to one proves nothing');
});

test('a chapter the top thirty never reaches is still found when scoped', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const { query, chapter } = OUT_OF_REACH;

    const wide = await (await app.call(
        `/api/rulebook/search?q=${query}&limit=30`, { cookie })).json();

    // The premise, asserted rather than assumed: this pair stops being a test of anything the day
    // the corpus shifts enough to put the chapter into the unscoped rows.
    assert.ok(wide.results.length === 30,
        `"${query}" no longer fills the cap, so there is nothing outside it`);
    assert.ok(!wide.results.some(r => r.chapter === chapter),
        `Ch.${chapter} is now inside the unscoped top thirty; pick another pair`);

    const scoped = await (await app.call(
        `/api/rulebook/search?q=${query}&chapter=${chapter}`, { cookie })).json();

    assert.ok(scoped.found > 0,
        `Ch.${chapter} has nothing for "${query}" at all, so the pair proves nothing`);
    assert.ok(scoped.results.length > 0);
    assert.ok(scoped.results.every(r => r.chapter === chapter));

    // This is the whole reason the parameter is on the server: the rows above are unreachable
    // from the unscoped answer, at any limit a caller is allowed to ask for.
    assert.ok(scoped.found < wide.found,
        'the scoped count is not smaller than the whole book’s, so nothing was scoped');
});

test('found counts the chapter, not the book', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const wide = await (await app.call('/api/rulebook/search?q=knockback', { cookie })).json();
    const scoped = await (await app.call('/api/rulebook/search?q=knockback&chapter=4', { cookie })).json();

    assert.ok(wide.found > 0 && scoped.found > 0, 'one of these found nothing, so this proves nothing');
    assert.ok(scoped.found < wide.found,
        `the scoped search reports ${scoped.found} of ${wide.found} — the whole book’s count`);

    // Counted over the scoped set *before* the list was cut, which is the ordering `search.js`
    // insists on: asking for one row must not change what the count or the flag say.
    const oneRow = await (await app.call(
        '/api/rulebook/search?q=knockback&chapter=4&limit=1', { cookie })).json();

    assert.equal(oneRow.found, scoped.found, 'the count is of what was found, not of what was shown');
    assert.equal(oneRow.nothingMatchedByHeading, scoped.nothingMatchedByHeading);
    assert.equal(oneRow.results.length, 1);
});

test('the flag describes the chapter that was searched', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Ch.4 (Combat) sets an entry under this heading; Ch.2 uses the word without one. Both halves
    // are asserted, so a flag hard-coded either way is red — and this is the property that makes
    // scoping honest rather than merely narrow: the flag is about what was searched.
    const entry = await (await app.call('/api/rulebook/search?q=knockback&chapter=4', { cookie })).json();
    const mention = await (await app.call('/api/rulebook/search?q=knockback&chapter=2', { cookie })).json();

    assert.ok(entry.found > 0 && mention.found > 0, 'one of these found nothing, so this proves nothing');
    assert.equal(entry.nothingMatchedByHeading, false);
    assert.equal(mention.nothingMatchedByHeading, true);
});

test('the caller still cannot raise the cap on a scoped search', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const body = await (await app.call(
        '/api/rulebook/search?q=resolve&chapter=2&limit=5000', { cookie })).json();

    assert.ok(body.found > 30, 'this query no longer matches enough in Ch.2 to test the cap');
    assert.ok(body.results.length <= 30, 'the limit is caller-controlled: ' + body.results.length);
});

test('a chapter that is not a number and one the book has not are different answers', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    // Neither may be `found: 0`. That is this API's one way of saying the book is silent, and the
    // page prints it as a sentence about the rulebook — a claim made on the strength of a typo.
    assert.equal((await app.call('/api/rulebook/search?q=knockback&chapter=two', { cookie })).status, 400);
    assert.equal((await app.call('/api/rulebook/search?q=knockback&chapter=', { cookie })).status, 400);
    assert.equal((await app.call('/api/rulebook/search?q=knockback&chapter=99', { cookie })).status, 404);

    // The control: the same query with a real chapter answers.
    const ok = await app.call('/api/rulebook/search?q=knockback&chapter=2', { cookie });
    assert.equal(ok.status, 200);
    assert.ok((await ok.json()).found > 0);
});

test('no chapter at all is still the whole book', async () => {
    const app = server();
    const { cookie } = await signIn(app, 'a@b.test');

    const body = await (await app.call('/api/rulebook/search?q=knockback', { cookie })).json();
    const direct = search(CHAPTERS, 'knockback', 30);

    assert.equal(body.found, direct.found);
    assert.ok(body.found > 0);
});

test('a scoped search cannot be read by somebody signed out', async () => {
    const app = server();

    // The gate is on the prefix, not on which address or which parameters — so a parameter added
    // below the check cannot be a way past it. The positive control is the signed-in call.
    const { cookie } = await signIn(app, 'a@b.test');
    assert.equal((await app.call('/api/rulebook/search?q=knockback&chapter=2', { cookie })).status, 200);

    const refused = await app.call('/api/rulebook/search?q=knockback&chapter=2');

    assert.equal(refused.status, 401);
    assert.ok(!(await refused.text()).toLowerCase().includes('knock someone across'));
});

test('a made-up corpus is scoped out of itself, not out of the real book', async () => {
    // **The scope is cached per chapter number, and keyed on the corpus as well as on the
    // number.** Cached on the number alone, a second corpus — which is every test that hands this
    // a made-up chapter — would be answered out of the first one's array, plausibly and wrongly.
    // That is the exact fault `corpusIndex` in `search.js` carries a comment about, one layer up.
    //
    // Driven through `scopedSearch` rather than `search`, because the cache is in that file and
    // calling the ranking directly would walk straight past it.
    const invented = [{
        chapter: 2,
        title: 'Invented',
        source_ref: 'nowhere',
        sections: [{ heading: 'FLIBBERTIGIBBET', printed_page: 1, text: 'A word found nowhere else.' }],
    }];

    const ask = (chapters, q) => scopedSearch(
        new Request(`https://example.test/api/rulebook/search?q=${q}&chapter=2`), chapters);

    // Warm the real book's Ch.2 first, so an unkeyed cache would have something wrong to answer
    // with. The control: it is a real answer, not an empty one.
    const real = await (await ask(CHAPTERS, 'knockback')).json();
    assert.ok(real.found > 0, 'the real Ch.2 answered nothing, so this proves nothing');

    const made = await (await ask(invented, 'flibbertigibbet')).json();

    assert.equal(made.found, 1);
    assert.equal(made.results[0].heading, 'FLIBBERTIGIBBET');

    // ...and the real book is still itself afterwards.
    assert.equal((await (await ask(CHAPTERS, 'knockback')).json()).found, real.found);
});

// ── The work a query can ask for ──────────────────────────────────────────
//
// **The output cap bounds the answer; these bound the cost of producing it.** An audit measured a
// 70KB query of 17,549 distinct three-letter words answering in 2.7 seconds against the real
// corpus, where an ordinary query takes about 2ms — two super-linear paths, the dedup and the
// per-term scan over the whole vocabulary, neither bounded by anything. Any signed-in account
// could reach it, this route has no rate limit, and Workers is billed on CPU per request.

test('a query cannot ask for unbounded work', () => {
    const letters = 'abcdefghijklmnopqrstuvwxyz';
    const words = [];
    for (const a of letters) for (const b of letters) for (const c of letters) words.push(a + b + c);
    const attack = words.join(' ');

    assert.ok(attack.length > 60_000, 'the attack query is not large enough to be one');

    // Warm the index, so this times the matching rather than the one-off construction.
    search(CHAPTERS, 'knockback', 5);

    const started = Date.now();
    const answered = search(CHAPTERS, attack, 30);
    const took = Date.now() - started;

    // The cap, which is the actual guarantee: however long the query, only so many words are
    // searched on. Asserted rather than the timing, because a wall clock on a busy CI runner is
    // not a fact about this code.
    assert.ok(answered.terms.length <= 60,
        `${answered.terms.length} terms were searched on; the cap is 60`);

    // ...and a generous ceiling on the time, which is what would actually have caught the
    // original defect. 2.7s was the measurement; anything near it means a cap stopped applying.
    assert.ok(took < 1000, `the attack query took ${took}ms, which is the unbounded shape back`);

    // The answer is still honest about what it used, which is what makes truncating acceptable
    // rather than a silent lie about the search.
    assert.ok(Array.isArray(answered.terms));
});

test('the dedup is not quadratic', () => {
    // 40,000 distinct words cost 2.5 seconds through an array `includes`. The cap alone bounds
    // this now, but the data structure is the reason it cannot come back by raising the cap.
    const many = Array.from({ length: 40_000 }, (_, i) => 'w' + i).join(' ');

    const started = Date.now();
    const used = terms(many);
    const took = Date.now() - started;

    assert.ok(used.length <= 60, `${used.length} terms kept; the cap is 60`);
    assert.ok(took < 500, `tokenising 40,000 words took ${took}ms`);
});

test('an ordinary question is not truncated', () => {
    // The positive control on the caps: they must be far past anything a person types, or this
    // whole guard is a bug wearing a test. A real sentence keeps every word that carries meaning.
    const asked = terms('how does the trait cap work when a power is maintained in combat');

    assert.ok(asked.length >= 4, `only ${asked.length} words survived a real sentence`);
    assert.ok(asked.includes('trait'));
    assert.ok(asked.includes('cap'));

    // ...and it still answers.
    assert.ok(search(CHAPTERS, 'how does the trait cap work', 10).found > 0);
});
