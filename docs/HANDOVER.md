# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the single source of truth for what is done; this file is only the short version of where the last session stopped.

**Delete this file when you have finished the slice you pick up. It is a note between sessions, not documentation.**

---

## Where things stand

PR #28 is merged. The browser front end is live, prints a one-page sheet in Hero navy or Villain crimson, and keeps the character in the browser between visits. 2765 tests, zero warnings at CI strictness, and a whole-tree Qodana scan reports zero.

Nothing is half-finished. The next slice is a free choice from the **Remaining work** list in `PROGRESS.md`.

## What to pick, and why

- **Item 2, Sources on Abilities and Talents**, is the one with two surfaces already waiting for it — the `.txt` sheet and the browser's `SheetView` both group Powers by Source and both stop short of the `Abilities (…)` line the published sheets print. It is well-scoped and the transcription target is named in the item.
- **Item 5, the browser payload**, is 27 MiB because trimming is off. Worth doing only with a plan to *verify* it — a trimmed-away model property is a runtime silence, not a build error, so "it built" proves nothing. Read the item before starting; the local toolchain cannot run the trimmer at all.
- **Item 3** collects the things the sheet still cannot say. The largest is that a middle page of a multi-page sheet is anonymous, and CSS has no portable answer — do not spend a session rediscovering that. The two smaller ones in that item are real and cheap.

## How this project expects to be worked on

These are not preferences, they are what the last few slices cost when they were skipped:

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the reasoning survives.
2. **Have the work adversarially reviewed by agents that know nothing about it**, act on the findings, re-review, and only then merge. Ask each reviewer, for every guard test, to name a plausible bug the test claims to cover but would not catch — that question finds more than "review this diff". Two of those reviews found a shipping bug and five test mutations that passed green.
3. **Commit before probing.** The falsifiability probes work by breaking the code and checking a test goes red. Revert one named file at a time — never `git checkout -- .`, which once destroyed a full set of uncommitted fixes and made the probe results that followed meaningless.
4. **Do not start a dev server.** It raises an approval dialogue the owner has to click, which blocks unattended work. `dotnet build`, `dotnet test` and the Docker Qodana scan do not. To look at rendered output without one, render the component through bUnit, dump the markup into a static page against the real stylesheets, and print that with headless Chrome.
5. **Judge the printed sheet from a PDF, never from the screen.** Computed styles cannot tell you where a page break lands. The pipeline is described at the end of the printed-sheet section in `CLAUDE.md`.

## Local memory

The memory directory for this project holds four notes. Three were written in the last session and describe the four points above; they should stay until they stop being true. The fourth, `admin-installs-need-uac-heads-up`, is about winget installs needing an elevation prompt and is still correct.

Nothing there needs deleting. If a note turns out to be wrong, delete it rather than adding a second one beside it.
