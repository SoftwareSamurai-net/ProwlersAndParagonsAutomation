# Play rules

Read before touching `data/rules/play/`. This is the rules for *resolving an action*, which is a different store from the mechanics the character engine reads and is deliberately kept out of its way.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## What is in here, and what reads it

`data/rules/play/` holds the play rules as verified data: Chapter 3's challenge rolls and Chapter 5's two currencies today, and Chapter 4 when it arrives. Three files so far.

| File | Holds |
|---|---|
| `play_meta.json` | The dice model everything else is expressed in — pool, success map, the sub‑1d floor, automatic successes, net successes — plus the Introduction's book‑wide "half rounds up" rule (p.7) and the one printed exception to it |
| `challenge.json` | Every mechanic printed in Ch.3 Action, pp.67–72 |
| `resolve.json` | Every mechanic printed in Ch.5 Resolve and Adversity, pp.83–86 |

**Nothing reads any of them, and `PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile` is why that is a fact rather than a sentence.** No engine, no host, no test project beyond the ones that hold them to the book — proved by scanning `engine/`, `sheets/`, `cli/`, `web/` and `mcp/` for any spelling of a play file's path, with comments blanked and a positive control that this project names all four. That is the point of the slice: **the data is verified before anything trusts it**, which is the order the 141 Powers were done in and the order that made them trustworthy. The models and the resolution logic arrive with the simulator itself — that is `PROGRESS.md` item 14's later slice, and when it lands it is a *second* engine beside `engine/`, never a change to it.

**`engine/` must not learn any of this.** `CLAUDE.md`'s settled list says why in one line: `engine/` is the authority on cost and validity and knows nothing about resolving an action. A `PlayRulesRepository` belongs in a new project, not in an existing one.

## Why a subdirectory, and not just more files in `data/rules/`

Three csproj files copy the rules data, and **every one of them globs `data\rules\*.json` — one star, non‑recursive**:

| Project | What it does with the glob |
|---|---|
| `ProwlersAndParagonsAutomation.csproj` | copies to the CLI's output directory |
| `mcp/ProwlersAndParagons.Mcp.csproj` | copies to the published MCP server |
| `web/ProwlersAndParagons.Web.csproj` | stages into `wwwroot/data/rules`, from where the deployed site serves it publicly |

So a file one level down reaches none of them, and the third row is the one that matters: **a file under `wwwroot` is a public URL.** The same reasoning already keeps the rulebook corpus out of the payload — see [`rulebook-corpus.md`](rulebook-corpus.md) — and the placement *is* the whole access control there too. Play rules are not secret, but they are a store no visitor's browser has any use for, and shipping them would put the whole of Chapters 3–5 into every first page load for nothing.

**That claim is proved rather than asserted.** `PlayPayloadTests` reads the three globs as source and refuses a recursive one — in either separator, so a rewritten `data/rules/**/*.json` fails as a recursive glob rather than as "the extraction has stopped matching", which is a message that sends the reader to fix the wrong thing. It expands the glob the way MSBuild does and requires the result to hold no play file, and checks the staged and copied output directories on disk. Do not "tidy" `data/rules/play/` back up a level, and do not change a glob to `**` for convenience.

**`web/wwwroot/data/rules` is not always on disk, and the check does not skip when it is absent.** That directory is written by `web/`'s build, and the engine test project does not reference `web/` — a solution-wide `dotnet test` stages it only because the bUnit project pulls `web/` in. So when it is missing the expectation is built from `web/`'s own `RulesDataFile` glob instead, which is what MSBuild *would* stage, and the failure message says which of the two it read. A check whose answer is "run a build first" is a check nobody runs.

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

Every entry also carries a `source_ref` in the existing spelling (`"Ultimate Edition, Ch.3 Action, p.67"`) naming a page in its **own** chapter — 67–72 for the two Chapter 3 files, 83–86 for `resolve.json` — or p.7 for the Glossary's rounding rule. `EverySourceRefNamesAPageInItsOwnChapterOrTheGlossary` reads the bound per file rather than per directory, because one 67–86 window across the store would accept a Chapter 4 page in either chapter's file and a Chapter 3 page in Chapter 5's.

**Chapter 5's entries carry a `printed_under` as well, and it is checked against the corpus rather than against a constant.** A page in a six‑page chapter is a wide target; the heading the mechanic was transcribed from is a narrow one. `EveryEntryNamesAHeadingPrintedOnThePageItCites` requires the value to be a heading the extractor found *on that page* of `ch05-resolve-and-adversity.json`, so a wrong page and a wrong heading both fail, and the field is exempt from the canonical walk only because that check is the stronger of the two.

## `corroborated_by`, and the three rules the book prints twice

Chapter 1's summary reprints three of Chapter 3's rules — the Challenge Rolls bands and the success rule on p.9, the Thresholds table on p.10 — and two of Chapter 5's, on p.11. Refusing those pages under the range rule would have thrown away **the only place in the book where a value here is printed a second time**, so an entry may carry a `corroborated_by` list, and a reference on it must name a page *outside* its own chapter's range: a second citation of the same chapter is not a second printing.

**It is not a decorative citation.** `TheThreeRulesChapterOneReprintsAgreeWithTheTranscription` reads Ch.1 out of the corpus and finds each row there — and **derives the printed row from the canonical record rather than typing it out again**. A threshold row prints as `Superhuman 6 to 8` or `Godlike 12 or more` exactly as its min, max and null ceiling say it should; a band prints as `−1 to 0 Opponent with Embellishment` exactly as its bounds, outcome and embellishment flag say; the success clause is built from the canonical map's own faces. So a wrong value in `CanonicalChallengeRules` builds a string Chapter 1 does not contain, and typing the expected strings out would only have added a fourth transcription to disagree with.

**Chapter 5's two are checked the same way**, by `ChapterOneReprintsTheAdversityRateAndTheShapeOfTheResolveTable`. The Adversity rate is formatted out of the canonical number — "1 point of Adversity per Hero" is built, not typed — and the *direction* of the Resolve table is computed from its own rows: more room under the cap pays more, so the test looks for "the more powerful you are, the less Resolve you have", and a table that had been inverted would look for a sentence p.11 does not contain.

This is the same argument as the fixture rule below, one step weaker and one step wider: a worked example proves the whole chain on one case, and a second printing proves three tables outright.

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

Chapter 5's is the Adversity example under Challenge Level (p.85): four Heroes, a Challenge Level 2 scene, eight points to the GM. The award is **computed from the data file's own `award_factors` and `award_operation`**, so dropping the party size from the factor list gives 2 and turning the product into a sum gives 6 — only the transcription the book prints gives 8.

The counting function is a test helper and is not the start of an engine. Keep it that way until the simulator exists.

## Chapter 5: two currencies, and the one place this store touches `engine/`

`resolve.json` holds twenty-eight entries across pp.83–85 — the chapter's printed range is 83–86 and p.86 carries no chapter text, which the header says so nobody goes looking for it.

**Every spend is keyed to who holds the pool, and that is `CLAUDE.md`'s settled rule turned into data.** "Only Heroes have Resolve; the GM gets Adversity, spendable on any NPC" was a sentence in a list of things not to re-litigate; it is now a `who` field on each of the twelve spends, `hero` or `gm`, both values transcribed from the chapter's own words (p.84 "Because only Heroes have Resolve", p.85 "As the GM, you have … Adversity"). `EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms` checks it — **and does not trust the id prefix to do it**, because a spend renamed out of `spend_`/`adversity_spend_` would escape a check built on the prefix alone. The cross-check is the currency the entry actually charges: `cost_adversity` must be the GM's and `cost_resolve` must be a Hero's, whatever the entry is called.

**The Resolve table is the one mechanic in `data/rules/play/` that the character engine already implements**, so this is the one file here that reads into `engine/` — once, in a test. `DerivedStatsCalculator.CalculateResolve` has computed `(TraitCap − highest relevant rank) × 2` since long before this store existed, and until now the two statements of that rule were unconnected. `TheEngineComputesTheTableThisFileRecords` builds the expected figure out of the *shipped JSON's* rate and pivot and compares it with the engine's answer at three ranks. It is a read and it stays one: nothing wires the engine to these bytes, and `NothingInTheApplicationNamesAPlayRulesFile` still fails if anybody tries. Chapter 5 states the base only — Determination and the Condition/Plot Hook Flaws are Chapter 2's additions to the same figure, and the entry's `ambiguity` records that the chapter never says whether its table is the whole of a character's opening pool.

**The eleven Powers Chapter 5 exempts are cross-checked against `powers.json`.** `EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData` resolves each printed name to its entries and requires `DerivedStatsCalculator.ResolveAffectedByPower` to answer false — via an explicit `affects_resolve` or via the Movement/Sensory category default. Two of the eleven need a mapping and **it lives in the test, not in the data**, because it is a reading: the book's "Swinging" is `swing_line` in the rules data, and its "Super Senses" is sixteen entries there. The data transcribes the printed names and nothing else.

**Two things the chapter states about itself are checked against the file rather than merely recorded.** "Three things you can do with Adversity that Heroes can't" is compared with the number of exclusive Adversity spends the file actually carries, and the six ways of earning are split into the ones a simulator could apply and the ones that need a person — a distinction the book does not draw, so it is an `interpretation`, derived from the entries' own `kind` and required to have both halves non-empty.

**References out, never transcriptions in.** The six combat spends are recorded as ids pointing at Ch.4 with `transcribed_here: false`, and Interludes point at Ch.9 for what an interlude is. Copying either chapter in would create a second transcription to disagree with the first — which is the same reason `challenge.json` does not restate the rounding rule `play_meta.json` holds.

## What is deliberately not in the data

- **The Sample Thresholds table (p.71).** Nine rows of worked examples illustrating thresholds the Thresholds table already states numerically — no mechanic of its own. It is also the one part of Ch.3 the extractor scrambles, being a three‑column table it reads across rather than down.
- **Describing the Action (p.68).** Advice on how to narrate, with no number and no procedure in it.

From Chapter 5:

- **The chapter opening (p.83)** — an essay on what makes a character a Hero, with no number and no procedure in it.
- **"Track it with poker chips or glass beads" (pp.83, 85)** — printed twice, and advice about the table rather than a rule of the game.

Each file's header names its own omissions, and a test requires the header to keep saying so.
