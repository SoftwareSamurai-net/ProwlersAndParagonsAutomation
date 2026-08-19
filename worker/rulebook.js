// A Power's printed entry, for somebody who is signed in.
//
// The lookup is by heading, because that is the join the corpus actually supports: Chapter 2
// sets each Power's entry under its name in capitals, and `RulebookCorpusTests` holds the
// corpus to that across a hundred and sixteen entries by checking each one opens with the stat
// line `data/rules` records. So this file needs no table of its own, and cannot drift from one.

import { fail, json } from './http.js';

/**
 * Headings to sections, built once when the module loads.
 *
 * A Map rather than a scan per request: the chapter has some hundreds of sections and this is
 * asked once per Power a player looks at. Built from whatever chapters `corpus.js` bundles, so
 * adding one needs no change here.
 */
export function index(chapters) {
    const byHeading = new Map();

    for (const chapter of chapters) {
        for (const section of chapter.sections) {
            const key = section.heading.toUpperCase();

            // First entry wins. A heading occurring twice in one chapter is a table of the same
            // name as an entry, or a continuation; the entry itself comes first in reading
            // order, and the alternative — last wins — showed a player the table.
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
