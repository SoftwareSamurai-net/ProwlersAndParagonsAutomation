// The book's own text, bundled into the server rather than served as a static asset.
//
// **That placement is the entire access control.** `data/rulebook/` is deliberately not copied
// into `wwwroot` — a file under `wwwroot` is a public URL, and the corpus is the publisher's
// prose, held here by permission granted to this repository's owner. Bundled here it is
// reachable only through `/api/rulebook/...`, which asks who is calling first.
//
// Chapter 2 only, because Chapter 2 is where the Powers are and a Power's entry is what the
// editors want to show. Adding a chapter is adding a line here and a case in `rulebook.js`;
// adding *all* of them is a decision about what a signed-in account is entitled to read, which
// is worth taking deliberately rather than by a glob.

import chapterTwo from '../data/rulebook/ch02-characters.json' with { type: 'json' };

export const CHAPTERS = [chapterTwo];
