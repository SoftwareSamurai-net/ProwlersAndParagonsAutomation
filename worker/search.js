// Finding a passage in the book by what somebody half-remembers.
//
// **The matching rule is the MCP server's `Mentions`, ported**, and it is not a refinement of a
// substring search — it is a different answer to a different problem. Substring matching answered
// *"she bakes bread in the city"* with **Plasticity**, and a match like that is worse than no
// match at all, because it arrives looking exactly like a real one and there is nothing in it a
// reader can see is wrong.
//
// **What that sentence proves here is narrower than it proves there, and the first version of this
// comment claimed the wider thing.** It said the baker's sentence has to stay at nothing found.
// It does not and cannot: over there the haystack is 141 short Power entries, here it is the whole
// book, and "city" is a word the book genuinely uses — *City of Heroes* in the introduction, "a
// city, forest, jungle" in Attuned. Measured: twenty-one passages, every one of them a real
// occurrence of a word somebody typed. The property worth pinning is the one the rule actually
// buys — **the sentence must not reach PLASTICITY**, which is what substring matching did and what
// no amount of honest prose about a city should — with the positive control beside it, since a
// search that has stopped working satisfies every absence.
//
// **What is deliberately different from the Powers search is the stopword list.** That one drops
// "power", "powers", "character" and "super" because they are in half the entries it searches and
// carry no information *about a Power*. Here they are section headings a reader will actually type
// — "powers", "super senses" — so only the ordinary English filler is dropped. The rule that stops
// "he can turn invisible" matching everything is still there; it is the words, not the list, that
// were tuned for a catalogue.
//
// **The index is built on the first search and kept, not built when the module loads.** A Worker
// gets a small budget of startup CPU, and walking three quarters of a megabyte of prose is not a
// thing to spend it on for a request that may never ask a question. The first search in an isolate
// pays for it; every one after is a map lookup.

/** What separates one word from the next, in a query and in the book alike. */
const NOT_A_WORD = /[^\p{L}\p{N}]+/u;

/**
 * The words in every sentence, which carry nothing about which sentence.
 *
 * Without them "how do I make a character stronger" matches on "how", "make" and "you" and
 * returns the whole book in an order nobody can account for.
 */
const STOPWORDS = new Set([
    'the', 'and', 'but', 'can', 'for', 'his', 'her', 'its', 'their', 'them', 'they',
    'she', 'him', 'who', 'that', 'this', 'with', 'from', 'into', 'onto', 'out', 'off',
    'are', 'was', 'were', 'been', 'has', 'have', 'had', 'does', 'did', 'you', 'your',
    'any', 'all', 'one', 'two', 'not', 'when', 'what', 'how', 'why', 'where', 'which',
    'able', 'very', 'just', 'like', 'also', 'get', 'gets',
]);

const ENDINGS = ['ing', 'es', 'ed', 's'];

/**
 * A word with the commonest English ending taken off, when what is left is still a word worth
 * matching on. Deliberately crude — anything cleverer would be a stemmer, and a stemmer that gets
 * one word wrong is harder to explain than a substring search that misses one.
 */
function stem(word) {
    for (const ending of ENDINGS) {
        if (word.length - ending.length >= 3 && word.endsWith(ending)) {
            return word.slice(0, -ending.length);
        }
    }

    return word;
}

/**
 * How much of a query is read, and how many distinct words are searched on.
 *
 * **Both caps exist because neither the output limit nor the gate bounds the *work*.** An audit
 * measured it: a 70KB query of 17,549 distinct three-letter words answered in **2.7 seconds**
 * against the real corpus, where an ordinary one-word query takes about 2ms. Two paths are
 * super-linear in the term count — this function's own dedup, and the per-term scan over the
 * whole vocabulary in `reached` — and `MOST_RESULTS` in `rulebook.js` caps only how many rows come
 * back, never how many words were matched to produce them.
 *
 * Any signed-in account could reach it, there is no rate limit on that route the way
 * `/api/auth/request` has one, and Workers is billed and limited on CPU per request. So the cap is
 * on the input, where the cost actually is.
 *
 * **Sixty words is far past any real question.** The longest sensible query somebody types at a
 * table is a sentence; the stopword filter already removes the filler from it. A query longer than
 * this is not a question, and truncating rather than refusing keeps an honest answer coming back
 * for the first sixty words — the reader gets results, and `terms` in the response says exactly
 * which words were used, which is the same honesty the flags already carry.
 */
const MOST_QUERY_BYTES = 2000;
const MOST_TERMS = 60;

export function terms(query) {
    // Cut before splitting, so a megabyte of text is never tokenised at all. This is the bound
    // that matters most: the split itself is linear in the input, and everything after it is worse.
    const asked = String(query).slice(0, MOST_QUERY_BYTES).toLowerCase();

    // A Set, not an array with `includes`. The array made the dedup O(n²) — measured at 9ms for
    // 2,000 words and 2.5s for 40,000, which is the shape of that curve. The cap above bounds this
    // anyway; both are here because the cap is a policy and this is just the right data structure.
    const seen = new Set();

    for (const word of asked.split(NOT_A_WORD)) {
        if (word.length <= 2) continue;
        if (STOPWORDS.has(word)) continue;

        seen.add(word);
        if (seen.size >= MOST_TERMS) break;
    }

    return [...seen];
}

function sharedPrefixLength(a, b) {
    let length = 0;
    while (length < a.length && length < b.length && a[length] === b[length]) length++;
    return length;
}

/**
 * Whether one word in the book is the word somebody typed, allowing for the endings English puts
 * on it — "regenerates" finds Regeneration, "invisible" finds Invisibility.
 *
 * **A shared prefix long enough to be the same word with a different ending**, rather than two
 * words that happen to start alike: "invisible" and "invisibility" share seven letters, "bread"
 * and "breath" share four. It does let a coincidence through — "animals" reaches Animation, which
 * shares five — and requiring the leftovers to be short instead was tried in the Powers search and
 * is worse: the leftovers of animal/animation are "l" and "tion", and those of
 * invisible/invisibility are "le" and "ility", so any rule refusing the first refuses the second.
 */
function isTheSameWord(word, term, termStem) {
    if (word === term || word === termStem) return true;

    const shared = sharedPrefixLength(word, term);

    return shared >= 5 && shared >= Math.min(word.length, term.length) - 3;
}

/**
 * Every section, with its words, built once per isolate.
 *
 * Two maps rather than one, because where a word occurs is most of what decides the order: a
 * heading is what the section is *about*, and the body is what it happens to mention.
 */
function build(chapters) {
    const sections = [];
    const inHeading = new Map();
    const inText = new Map();

    const put = (map, word, at) => {
        let found = map.get(word);
        if (!found) map.set(word, (found = new Set()));
        found.add(at);
    };

    for (const chapter of chapters) {
        for (let i = 0; i < chapter.sections.length; i++) {
            const section = chapter.sections[i];
            const at = sections.length;

            sections.push({
                chapter: chapter.chapter,
                chapterTitle: chapter.title,
                sourceRef: chapter.source_ref,
                index: i,
                heading: section.heading,
                printedPage: section.printed_page,
                text: section.text,
            });

            for (const word of section.heading.toLowerCase().split(NOT_A_WORD)) {
                if (word) put(inHeading, word, at);
            }

            for (const word of section.text.toLowerCase().split(NOT_A_WORD)) {
                if (word) put(inText, word, at);
            }
        }
    }

    return {
        sections,
        inHeading,
        inText,
        headingWords: [...inHeading.keys()],
        textWords: [...inText.keys()],
    };
}

let built = null;
let builtFor = null;

/**
 * The index, building it the first time it is asked for.
 *
 * **Keyed on the corpus it was built from, not merely on having been built.** The first version
 * cached on a bare null check and ignored its argument thereafter, so a second corpus — which is
 * every test that hands this a made-up chapter — would have been answered from the first one's
 * index, silently and with entirely plausible results. It is one line either way and only one of
 * them is honest.
 */
function corpusIndex(chapters) {
    if (builtFor !== chapters) {
        built = build(chapters);
        builtFor = chapters;
    }

    return built;
}

/** Every section a term reaches in one of the two maps, and which words did it. */
function reached(term, words, map) {
    const termStem = stem(term);
    const hits = new Set();
    const matchedWords = [];

    for (const word of words) {
        if (!isTheSameWord(word, term, termStem)) continue;

        matchedWords.push(word);
        for (const at of map.get(word)) hits.add(at);
    }

    return { hits, matchedWords };
}

/**
 * A window of the passage around the first word that matched, so a reader can see *why* a result
 * is a result before opening it.
 *
 * **It is never the opening of the section regardless of the match.** A snippet that ignores the
 * query reads as an answer to it, which is the same failure as a search that always returns its
 * five best rows: nothing in it looks wrong.
 */
function snippet(text, matchedWords, width = 260) {
    const haystack = text.toLowerCase();

    let at = -1;

    for (const word of matchedWords) {
        const found = haystack.indexOf(word);
        if (found >= 0 && (at < 0 || found < at)) at = found;
    }

    if (at < 0) return text.slice(0, width) + (text.length > width ? '…' : '');

    const start = Math.max(0, at - Math.floor(width / 3));
    const end = Math.min(text.length, start + width);

    return (start > 0 ? '…' : '')
        + text.slice(start, end).trim()
        + (end < text.length ? '…' : '');
}

/**
 * What the book says about a query, best first.
 *
 * **`found: 0` is the only answer that means the book has nothing**, and the flags beside the
 * results say how they matched rather than what to conclude. The first version of the Powers
 * search attached "usually means the rulebook has no Power for this" to a heading-miss, so a
 * query that matched a dozen passages in the body was told the book was silent.
 *
 * **`nothingMatchedByHeading` is computed over the whole result and then the list is cut**, not
 * after: computed after the cut, a caller asking for one row turned a heading match at position
 * two into "nothing matched by heading at all".
 */
export function search(chapters, query, limit = 20) {
    const asked = terms(query);
    const index = corpusIndex(chapters);

    if (asked.length === 0) {
        return {
            query: String(query),
            terms: [],
            found: 0,
            nothingMatchedByHeading: true,
            results: [],
        };
    }

    const scores = new Map();

    const note = (at, points, term, words, byHeading) => {
        let row = scores.get(at);

        if (!row) {
            scores.set(at, (row = {
                points: 0, terms: new Set(), words: new Set(), byHeading: false,
            }));
        }

        row.points += points;
        row.terms.add(term);
        for (const word of words) row.words.add(word);
        if (byHeading) row.byHeading = true;
    };

    for (const term of asked) {
        const heading = reached(term, index.headingWords, index.inHeading);
        for (const at of heading.hits) note(at, 10, term, heading.matchedWords, true);

        const text = reached(term, index.textWords, index.inText);
        for (const at of text.hits) note(at, 2, term, text.matchedWords, false);
    }

    const ranked = [...scores.entries()]
        .map(([at, row]) => ({ at, row }))
        // Every term matched beats any number of points from one term: a passage using both
        // words somebody typed is what they meant, and one using the commoner word ten times
        // is not.
        .sort((a, b) =>
            b.row.terms.size - a.row.terms.size
            || b.row.points - a.row.points
            || index.sections[a.at].chapter - index.sections[b.at].chapter
            || index.sections[a.at].index - index.sections[b.at].index);

    const nothingMatchedByHeading = !ranked.some(r => r.row.byHeading);

    const results = ranked.slice(0, Math.max(0, limit)).map(({ at, row }) => {
        const section = index.sections[at];

        return {
            chapter: section.chapter,
            chapterTitle: section.chapterTitle,
            index: section.index,
            heading: section.heading,
            printedPage: section.printedPage,
            sourceRef: section.sourceRef,
            matchedTerms: [...row.terms],
            matchedHeading: row.byHeading,
            snippet: snippet(section.text, [...row.words]),
        };
    });

    return {
        query: String(query),
        terms: asked,
        found: ranked.length,
        nothingMatchedByHeading,
        results,
    };
}
