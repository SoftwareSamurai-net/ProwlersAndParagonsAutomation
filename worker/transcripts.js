// The recorded conversations, for somebody who is signed in.
//
// **A transcript holds a character, never an answer about one — see `engine/TranscriptLibrary`.**
// This file does not know that, and must not learn it: it hands back exactly the bytes
// `data/transcripts/` holds, keyed by file name, and the strict reader that refuses a field a
// character no longer has still runs where it always has, in the browser. Nothing here feeds a
// cost, a rank or a validity.
//
// **The whole bundle, in one answer, rather than one address per file.** The browser used to fetch
// each recording by name from `wwwroot/data/transcripts/`; behind the gate there is one thing to
// ask for and one thing to refuse, so `/api/transcripts` answers every recording at once and the
// browser splits them apart exactly as it always split apart four files.

import { json } from './http.js';

/**
 * Every recording, keyed by file name.
 *
 * <p>An object rather than an array, so the key is the same file name
 * `engine/TranscriptLibrary.FileNames` names — the browser's strict reader is keyed on that name,
 * not on a position in a list this server could reorder without anybody noticing.</p>
 */
export function transcripts(files) {
    return json(files);
}
