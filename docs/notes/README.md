# Stream notes

**These are evidence, not status.** Each file is one work stream's audit trail from the pre-1.0
slice: what a guard claimed, the exact mutation that defeated it, what replaced it, and the
mutation table showing the replacement watched red and then green.

[`PROGRESS.md`](../../PROGRESS.md) is still the single source of truth for what is done and what
remains, and `CLAUDE.md` is still where a rule that must not be broken again is written down. **Do
not read these files for either.** They exist because a mutation table is long, specific, and
worth keeping — the thing a later reader needs when a guard fails and they want to know whether it
has ever passed for the right reason — and folding eleven of them into `PROGRESS.md` would have
buried the record it is meant to be.

The stream numbers are the order they were commissioned in and mean nothing else.

| File | What it covers |
|---|---|
| `s1-quantities.md` | Six negative-quantity validator branches, five of which had no isolating test |
| `s2-contract.md` | The engine's missing filesystem guard, and the route check defeated by a duplicate literal |
| `s3-presentation.md` | `light-dark()` passing the colour scan, and the class-owner check that was a prefix match |
| `s4-harness.md` | Hard-coded harness verdicts, and the deliberately-broken twins that now catch them |
| `s5-extractor.md` | `PageReader`'s seam and its first tests |
| `s6-rulesdata.md` | Flaw types, Power baselines, graded Cons and talent links pinned to the book |
| `s7-gear.md` | `GearFormatter`, and the JSON export's shape |
| `s8-search.md` | The `search_powers` scorer, measured against the labelled set |
| `s9-diffguard.md` | The pixel comparator's two tolerance holes |
| `s10-rowvalidation.md` | Findings on the row that broke the rule |
| `s11-undo.md` | Single-level undo, and the import that was never being saved |
