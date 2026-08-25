// The book's own text, for somebody who is signed in.
//
// The Power lookup is by heading, because that is the join the corpus actually supports: Chapter 2
// sets each Power's entry under its name in capitals, and `RulebookCorpusTests` holds the
// corpus to that across a hundred and sixteen entries by checking each one opens with the stat
// line `data/rules` records. So this file needs no table of its own, and cannot drift from one.
//
// **The search and the browse read the same bundled chapters and hold no second copy.** Where the
// mechanics and the book disagree, `data/rules` wins and nothing here feeds a cost, a rank or a
// validity — the engine has never heard of this file.

import { fail, json } from './http.js';
import { search as findPassages } from './search.js';

/**
 * Headings to sections, built once when the module loads.
 *
 * A Map rather than a scan per request: the book has some fifteen hundred sections and this is
 * asked once per Power a player looks at. Built from whatever chapters `corpus.js` bundles, so
 * adding one needs no change here.
 */
export function index(chapters) {
    const byHeading = new Map();

    for (const chapter of chapters) {
        for (const section of chapter.sections) {
            const key = section.heading.toUpperCase();

            // First entry wins. A heading occurring twice is a table of the same name as an
            // entry, or a continuation; the entry itself comes first in reading order, and the
            // alternative — last wins — showed a player the table.
            if (!byHeading.has(key)) {
                byHeading.set(key, {
                    heading: section.heading,
                    printedPage: section.printed_page,
                    text: section.text,
                    sourceRef: chapter.source_ref,
                });
            }
        }
    }

    return byHeading;
}

/**
 * The entry for a Power, by the name `data/rules` gives it.
 *
 * A miss is a 404 and is entirely ordinary: Super Senses' sixteen options, Form's and
 * Transformation's, are separate entries in the rules data and share one printed entry between
 * them, so about a fifth of the Powers have no heading of their own. The front end shows
 * nothing rather than an apology.
 */
export function power(request, entries) {
    const name = new URL(request.url).searchParams.get('name');
    if (!name) return fail(400, 'No Power was named.');

    const entry = entries.get(name.trim().toUpperCase());

    return entry ? json(entry) : fail(404, 'The book prints no entry under that name.');
}

/**
 * The most rows one search will answer with.
 *
 * **A cap the caller cannot raise**, because the alternative is a query that serialises fifteen
 * hundred passages with their snippets into one response. A caller wanting the next page asks for
 * a narrower query; that is what the terms are for.
 */
const MOST_RESULTS = 30;

/**
 * What the book says about a query.
 *
 * **The flags travel with the answer and say how it matched, never what to conclude.** `found: 0`
 * is the only answer that means the book is silent; `nothingMatchedByHeading` says every passage
 * matched in its body, which is ordinary for a question phrased as a question.
 */
export function search(request, chapters) {
    const parameters = new URL(request.url).searchParams;
    const query = parameters.get('q');

    if (query === null) return fail(400, 'Nothing was searched for.');

    const asked = Number.parseInt(parameters.get('limit') ?? '', 10);
    const limit = Number.isFinite(asked) && asked > 0 ? Math.min(asked, MOST_RESULTS) : MOST_RESULTS;

    return json(findPassages(chapters, query, limit));
}

/**
 * What there is to read: every chapter, and how much of it.
 *
 * The front door draws its one figure from this rather than counting anything of its own — what is
 * readable is this server's answer, and a count kept on the other side would be a second one that
 * could disagree with it.
 */
export function contents(chapters) {
    const listed = chapters.map(chapter => ({
        chapter: chapter.chapter,
        title: chapter.title,
        printedPages: chapter.printed_pages,
        sourceRef: chapter.source_ref,
        sections: chapter.sections.length,
    }));

    return json({
        chapters: listed,
        sections: listed.reduce((total, c) => total + c.sections, 0),
    });
}

/**
 * One passage in full, by the chapter and position a search result carries.
 *
 * **Addressed by position rather than by heading**, unlike the Power lookup: headings repeat
 * across the book — a table named after the entry above it, a continuation — and the Power lookup
 * resolves that by taking the first, which is right for a Power and wrong for "show me the one I
 * just clicked".
 */
export function passage(request, chapters) {
    const parameters = new URL(request.url).searchParams;

    const chapterNumber = Number.parseInt(parameters.get('chapter') ?? '', 10);
    const at = Number.parseInt(parameters.get('index') ?? '', 10);

    if (!Number.isFinite(chapterNumber) || !Number.isFinite(at) || at < 0) {
        return fail(400, 'That is not an address in the book.');
    }

    const chapter = chapters.find(c => c.chapter === chapterNumber);
    const section = chapter?.sections[at];

    if (!section) return fail(404, 'The book has no passage there.');

    return json({
        chapter: chapter.chapter,
        chapterTitle: chapter.title,
        index: at,
        heading: section.heading,
        printedPage: section.printed_page,
        text: section.text,
        sourceRef: chapter.source_ref,
    });
}
