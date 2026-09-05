# Play rules

Read before touching `data/rules/play/`. This is the rules for *resolving an action*, which is a different store from the mechanics the character engine reads and is deliberately kept out of its way.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## What is in here, and what reads it

`data/rules/play/` holds the play rules as verified data: Chapter 3's challenge rolls today, and Chapters 4 and 5 when they arrive. Two files so far.

| File | Holds |
|---|---|
| `play_meta.json` | The dice model everything else is expressed in — pool, success map, the sub‑1d floor, automatic successes, net successes — plus the Introduction's book‑wide "half rounds up" rule (p.7) and the one printed exception to it |
| `challenge.json` | Every mechanic printed in Ch.3 Action, pp.67–72 |

**Nothing reads either of them.** No engine, no host, no test project beyond the ones that hold them to the book. That is the point of the slice: **the data is verified before anything trusts it**, which is the order the 141 Powers were done in and the order that made them trustworthy. The models and the resolution logic arrive with the simulator itself — that is `PROGRESS.md` item 14's later slice, and when it lands it is a *second* engine beside `engine/`, never a change to it.

**`engine/` must not learn any of this.** `CLAUDE.md`'s settled list says why in one line: `engine/` is the authority on cost and validity and knows nothing about resolving an action. A `PlayRulesRepository` belongs in a new project, not in an existing one.

## Why a subdirectory, and not just more files in `data/rules/`

Three csproj files copy the rules data, and **every one of them globs `data\rules\*.json` — one star, non‑recursive**:

| Project | What it does with the glob |
|---|---|
| `ProwlersAndParagonsAutomation.csproj` | copies to the CLI's output directory |
| `mcp/ProwlersAndParagons.Mcp.csproj` | copies to the published MCP server |
| `web/ProwlersAndParagons.Web.csproj` | stages into `wwwroot/data/rules`, from where the deployed site serves it publicly |

So a file one level down reaches none of them, and the third row is the one that matters: **a file under `wwwroot` is a public URL.** The same reasoning already keeps the rulebook corpus out of the payload — see [`rulebook-corpus.md`](rulebook-corpus.md) — and the placement *is* the whole access control there too. Play rules are not secret, but they are a store no visitor's browser has any use for, and shipping them would put the whole of Chapters 3–5 into every first page load for nothing.

**That claim is proved rather than asserted.** `PlayPayloadTests` reads the three globs as source and refuses a recursive one, expands the glob the way MSBuild does and requires the result to hold no play file, and checks the staged and copied output directories on disk. Do not "tidy" `data/rules/play/` back up a level, and do not change a glob to `**` for convenience.

Adding a play rules file therefore needs **no** change to `RulesRepository.DataFileNames` — that list is the contract for a host that loads the *character* rules over HTTP, and a play file must never appear on it.

## The closed `verified_fields` list

Same discipline as `powers.json`: verification is tracked **per field**, not with a boolean, because a single flag drifts and 27 Power entries once sat unflagged with wrong costs. Each file's header declares the vocabulary and both files must declare the same one:

```
trigger · roll · threshold · effect · duration · cost · description
```

An entry's `verified_fields` must be non‑empty, must be a subset of that list, and must include `description` — the one field that is *written* rather than transcribed, and therefore the one most easily left unchecked.

**And a claimed word has to answer to a key the entry actually carries.** `VerifiedFieldKeys` maps each word to the JSON keys it may cover, and a claim no key can answer goes red. That found three: both band tables declared `threshold` while carrying only net‑success bands — which is the figure left *after* a threshold has been subtracted, not a threshold — and `assisting` declared `trigger` with nothing trigger‑shaped on it. The map is deliberately generous everywhere except `threshold`, because widening it to admit the bands would have made the check say nothing.

## Every fact field is compared, and a reflection walk proves it

`EveryFieldInAPlayRulesFileIsReadByTheTestModels` used to be read as the coverage guarantee. It is not, and it is now called `…DeserializesIntoATestModel`: **loading a value is not verifying it.** An adversarial pass found around twenty fields that deserialized perfectly and were compared to nothing at all — `gm_is_opponent_when_unopposed`, `sixes_explode`, `regain_consciousness`, `wing_it_is_endorsed` and the rest — every one of them reading as verified data with nothing behind it.

`EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook` walks the test models by reflection and requires every leaf below an entry's envelope to be one of three things:

| | |
|---|---|
| **transcribed** | registered in `CanonicalChecks` against a value in `CanonicalChallengeRules`, which carries the printed sentence in its own comment |
| **derived** | listed in `DerivedPaths` — this project's reading, proved by a named test that *computes* it from transcribed values |
| **prose** | `what_this_is`, `note`, or any `*_note`, asserted non‑empty and nothing else |

Anything else fails, naming the path. **Prose is identified by a naming rule rather than an exemption list**, because a list of "this one is only descriptive" is exactly how twenty fact fields came to be unchecked.

Two properties are load-bearing. The walk carries a **positive control** on itself — it must find at least 90 leaves, against 98 today — because a reflection walk that stopped finding properties would report no faults and prove nothing. And `TheCoverageWalkReportsAFieldNothingComparesToTheRulebook` is its **negative control**: an unregistered field on a throwaway record is fed to the same classifier and must be reported, with the two prose spellings beside it passing. A classifier that faulted nothing would satisfy the main test perfectly.

**What the walk cannot see is a field whose value is null**, since a null leaf and an optional shape an entry does not use are the same thing to reflection. `arduous_exchanges_max` is the only one, and it is asserted by name.

Every entry also carries a `source_ref` in the existing spelling (`"Ultimate Edition, Ch.3 Action, p.67"`) naming a page in 67–72, or p.7 for the Glossary's rounding rule. A test enforces the range, so a value pasted in from another chapter cannot pass as Chapter 3's.

## A reading of the page is not a transcription of it: label it, and derive it

`CanonicalChallengeRules` has the same standing as `CanonicalPowers` — **it is the rulebook** — so a value that is this project's reading rather than the book's words has no place in it, and no place in an entry's transcribed rows either.

The Thresholds table is the case that made the rule. The book prints two columns, Difficulty and Threshold; **"the GM chooses inside this row" is nowhere on the page.** It was nonetheless a `gm_discretion` boolean on every row of `challenge.json` and a field of the canonical `Threshold` record, which made a reading look like a third printed column and put it behind the "do not edit this to match the code" notice.

It is now one `interpretation` object beside the table, saying in its own first field that it is ours. **And its value is derived rather than typed**: `TheRowsLeftToGmDiscretionAreExactlyTheOnesPrintedAsARange` computes the list from the rows whose printed threshold is a range — Superhuman 6 to 8, Legendary 9 to 11, Godlike 12 or more — and compares. The version it replaced asserted `Assert.Equal(3, …GmDiscretion)`, which is a count agreeing with the table by coincidence and would go on agreeing after somebody widened a row.

So: **a derived field is named as derived, kept out of the transcription, and asserted from the transcribed values it is derived from.**

## Descriptions are ours; the book's words are the corpus's

The rule from [`rules-engine.md`](rules-engine.md) applies here unchanged: **a `description` in `data/rules/` is original text written from the entry, never rulebook prose.** The printed text lives in `data/rulebook/` under a permission that does not travel with a fork.

The guard is stronger than a sentence comparison, because the realistic failure is a description built *around* a lifted clause rather than one pasted whole: `NoDescriptionRepeatsARunOfTheBooksOwnWords` fails on any run of **ten consecutive words** shared with Ch.3 or the Introduction, after reducing both sides to letters and digits so re‑punctuating a lifted clause does not dodge it. It carries a positive control — a phrase taken out of the corpus must be *found* in the corpus — because a normaliser that quietly produced an empty haystack would otherwise pass every assertion while checking nothing.

## `ambiguity` is a field, not a comment

Where the book is genuinely unclear, the entry says so in an `ambiguity` string. **An ambiguity nobody wrote down becomes an implementation decision nobody made** — a simulator simply picks a reading, and from then on the choice is invisible. Four are pinned by name because they change results:

- **The sub‑1d floor is reachable by several routes** (the GM's ±4d, Ch.4's wound penalties, the one‑shot Defining Moment's −2d) and nothing says whether they sum before the floor applies once or are floored as each lands.
- **Automatic successes are priced in pairs and the book never addresses an odd pool.** The printed example is 12d taking 6, which is even and settles nothing.
- **"Extra" net successes in a group action has two readings** — the amount above 3, or the whole total once it passes 3. On 5 net successes that is the difference between giving away 2 and giving away 5.
- **The one‑shot Defining Moment may replace the permanent rank loss or be paid on top of it.** The paragraph opens by calling Defining Moments "even more debilitating" in a one‑shot, which reads additively; the only *instead of* the page prints is its closing sentence, which is about **ordinary** games, where the GM *may* offer the swap. So the replacement is stated exactly where it is optional and unstated where it would be the rule.

**The fourth is here because it had been decided instead of recorded.** The entry carried `replaces: "the permanent 1d Ability reduction"` as a fact field, and a test named for that trade asserted nothing of the sort. What is a fact is now `ordinary_games_option` — offered, replacing, and not compulsory, which are the three halves of the one sentence that says any of it — and the reading for one-shots is in `ambiguity` where it belongs. **A fact field is a claim that the page states the thing**; when it does not, the field is the wrong container no matter how likely the reading.

## The fixture rule: pin an example the authors worked through

Everything else in `PlayRulesDataTests` compares one transcription to another, and **two transcriptions can agree and both be wrong**. So each chapter's data has to be exercised by at least one worked example printed in the book.

Chapter 3's is the arm‑wrestling exhibition under the Challenge Rolls table (p.67). Citizen Soldier declines to roll his 12d and banks 6; Gatecrasher rolls `1,2,2,2,3,3,3,5,5,5,6,6` for 7; 1 net success puts him in the Actor‑with‑Embellishment band. The test counts those twelve dice **with the success map the JSON ships**, divides by **the JSON's own automatic‑success rate**, and looks the result up in **the JSON's own band table**, so the whole chain has to be right to reach the printed answer. It asserts the roll is twelve dice first, because a fixture that quietly lost one would still produce *a* number.

The counting function is a test helper and is not the start of an engine. Keep it that way until the simulator exists.

## What is deliberately not in the data

- **The Sample Thresholds table (p.71).** Nine rows of worked examples illustrating thresholds the Thresholds table already states numerically — no mechanic of its own. It is also the one part of Ch.3 the extractor scrambles, being a three‑column table it reads across rather than down.
- **Describing the Action (p.68).** Advice on how to narrate, with no number and no procedure in it.

Both are named in `challenge.json`'s header, and a test requires the header to keep saying so.
