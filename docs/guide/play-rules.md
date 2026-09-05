# Play rules

Read before touching `data/rules/play/`. This is the rules for *resolving an action*, which is a different store from the mechanics the character engine reads and is deliberately kept out of its way.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## What is in here, and what reads it

`data/rules/play/` holds the play rules as verified data: the whole of the book's play block, Chapters 3 to 5, printed pp.67–86. Five files.

| File | Holds |
|---|---|
| `play_meta.json` | The dice model everything else is expressed in — pool, success map, the sub‑1d floor, automatic successes, net successes — plus the Introduction's book‑wide "half rounds up" rule (p.7) and the one printed exception to it |
| `challenge.json` | Every mechanic printed in Ch.3 Action, pp.67–72 |
| `combat.json` | Every mechanic printed in Ch.4 Combat, pp.73–79 — fifty‑one entries |
| `gritty.json` | Ch.4's ten optional Gritty Combat Rules, pp.79–81, plus the paragraph introducing them. Every one is a `table_setting` |
| `resolve.json` | Every mechanic printed in Ch.5 Resolve and Adversity, pp.83–86 |

**Nothing reads any of them, and `PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile` is why that is a fact rather than a sentence.** No engine, no host, no test project beyond the ones that hold them to the book — proved by scanning `engine/`, `sheets/`, `cli/`, `web/` and `mcp/` for any spelling of a play file's path, with comments blanked and a positive control that this project names every one of them. That is the point of the slice: **the data is verified before anything trusts it**, which is the order the 141 Powers were done in and the order that made them trustworthy. The models and the resolution logic arrive with the simulator itself — that is `PROGRESS.md` item 14's later slice, and when it lands it is a *second* engine beside `engine/`, never a change to it.

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

Every entry also carries a `source_ref` in the existing spelling (`"Ultimate Edition, Ch.3 Action, p.67"`) naming a page in its **own** chapter — 67–72 for the two Chapter 3 files, 73–82 for `combat.json` and `gritty.json`, 83–86 for `resolve.json` — or p.7 for the Glossary's rounding rule. `EverySourceRefNamesAPageInItsOwnChapterOrTheGlossary` reads the bound per file rather than per directory, because one 67–86 window across the store would accept a Chapter 4 page in either chapter's file and a Chapter 3 page in Chapter 5's.

**Chapter 4's and Chapter 5's entries carry a `printed_under` as well, and it is checked against the corpus rather than against a constant.** A page in a six‑page chapter is a wide target; the heading the mechanic was transcribed from is a narrow one. `EveryEntryNamesAHeadingPrintedOnThePageItCites` requires the value to be a heading the extractor found *on that page* of `ch05-resolve-and-adversity.json`, so a wrong page and a wrong heading both fail, and the field is exempt from the canonical walk only because that check is the stronger of the two.

`EveryChapterFourEntryNamesAHeadingPrintedOnThePageItCites` does the same for `combat.json` and `gritty.json`, and **checks the two files together on purpose**: they are one chapter split by what a rule *is* rather than by where it is printed, so three of the Gritty headings sit on p.79 beside two of the ordinary ones, and a check scoped to one file would accept a Gritty rule citing a combat heading and the other way round. Its negative control is the pair the chapter itself invites confusion between — p.79's Gritty `ACTIVE DEFENSES` against p.75's `ACTIVE AND PASSIVE DEFENSES`. **One entry's heading does not cover the whole of it and says so**: `pages_and_turns` takes what a page is from p.73's `EDGE` and the sentence that ends a page from `ACTIONS` beside it, so it carries a `printed_under_note`, and `TheOneEntrySplitAcrossTwoHeadingsSaysWhereItsOtherHalfIsPrinted` requires both that the note is there and that it is the only one — a second entry quietly acquiring the same narrowness is a second unrecorded reading.

## `corroborated_by`, and the three rules the book prints twice

Chapter 1's summary reprints three of Chapter 3's rules — the Challenge Rolls bands and the success rule on p.9, the Thresholds table on p.10 — and two of Chapter 5's, on p.11. Refusing those pages under the range rule would have thrown away **the only place in the book where a value here is printed a second time**, so an entry may carry a `corroborated_by` list, and a reference on it must name a page *outside* its own chapter's range: a second citation of the same chapter is not a second printing.

**It is not a decorative citation.** `TheThreeRulesChapterOneReprintsAgreeWithTheTranscription` reads Ch.1 out of the corpus and finds each row there — and **derives the printed row from the canonical record rather than typing it out again**. A threshold row prints as `Superhuman 6 to 8` or `Godlike 12 or more` exactly as its min, max and null ceiling say it should; a band prints as `−1 to 0 Opponent with Embellishment` exactly as its bounds, outcome and embellishment flag say; the success clause is built from the canonical map's own faces. So a wrong value in `CanonicalChallengeRules` builds a string Chapter 1 does not contain, and typing the expected strings out would only have added a fourth transcription to disagree with.

**Chapter 4's three come from Chapter 2 rather than Chapter 1**, and are checked the same way by `ChapterTwoReprintsTheThreatRanksTableAndTheTwoFormulas`. Ch.2 p.13 prints the Threat Ranks table Ch.4 p.77 reprints, and Ch.2 p.60 prints the Edge and Health formulas Ch.4 pp.73 and 75 restate. Each printed row is *built* from the canonical record — a Threat row prints as `Civilians 2d` or `Super 7d or More` exactly as its category, floor and null ceiling say it should, and both sentences are rebuilt out of the operand list parsed from the formula string — so a wrong value builds a string Chapter 2 does not contain.

**Chapter 5's two are checked the same way**, by `ChapterOneReprintsTheAdversityRateAndTheShapeOfTheResolveTable`. The Adversity rate is formatted out of the canonical number — "1 point of Adversity per Hero" is built, not typed — and the *direction* of the Resolve table is computed from its own rows: more room under the cap pays more, so the test looks for "the more powerful you are, the less Resolve you have", and a table that had been inverted would look for a sentence p.11 does not contain.

This is the same argument as the fixture rule below, one step weaker and one step wider: a worked example proves the whole chain on one case, and a second printing proves three tables outright.

## A reading of the page is not a transcription of it: label it, and derive it

`CanonicalChallengeRules` has the same standing as `CanonicalPowers` — **it is the rulebook** — so a value that is this project's reading rather than the book's words has no place in it, and no place in an entry's transcribed rows either.

The Thresholds table is the case that made the rule. The book prints two columns, Difficulty and Threshold; **"the GM chooses inside this row" is nowhere on the page.** It was nonetheless a `gm_discretion` boolean on every row of `challenge.json` and a field of the canonical `Threshold` record, which made a reading look like a third printed column and put it behind the "do not edit this to match the code" notice.

It is now one `interpretation` object beside the table, saying in its own first field that it is ours. **And its value is derived rather than typed**: `TheRowsLeftToGmDiscretionAreExactlyTheOnesPrintedAsARange` computes the list from the rows whose printed threshold is a range — Superhuman 6 to 8, Legendary 9 to 11, Godlike 12 or more — and compares. The version it replaced asserted `Assert.Equal(3, …GmDiscretion)`, which is a count agreeing with the table by coincidence and would go on agreeing after somebody widened a row.

**Chapter 5 shipped the same mistake once, and it is the reason to state the rule as a rule.** `cost_per_point_shared: 1` sat in `spend_assisting_allies` as a transcribed cost, with `SharePointCost = 1` in `CanonicalResolveRules` under the quote it supposedly came from. p.84 prices exactly one share — two points of Resolve for every point shared, and only for a giver who could not actually be assisting — and "as many points as you wish" says how many may move, never what one costs. So the par rate is an inference from the penalty, and the canonical comment was claiming a quote contained a value it does not. It is now an `interpretation` on the entry with the inference stated in its own first field, `ambiguity` says the rate is not printed, and `TheParShareRateIsInferredFromThePrintedPenaltyRate` derives it: the stated rate is a doubling, so par is half of it, and a change to the printed penalty has to move the reading with it.

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

## Chapter 4: the fight, and the ten switches beside it

`combat.json` holds fifty-one entries for pp.73–79 and `gritty.json` eleven for pp.79–81 — the ten optional rules and the paragraph that offers them. They are two files rather than one because the Gritty rules are **settings for a whole table**, taken or not taken before play, where everything in `combat.json` is a thing that happens during one. `TheTenGrittyRulesAreEachATableSetting` holds that split to the `kind` field and to a named list of ten, because a rule quietly dropped from the file would leave every other test green.

**The chapter's worked fight is not an entry, and that is the point.** A worked example is not a mechanic; it is the thing that proves the mechanics, and `TheExampleOfCombatOnPageEightyOneResolvesThroughTheData` steps six of its rolls through the JSON's own tables — the damage rate, the Grappling table, the Minion rate, and then, for its last roll, Chapter 3's narrative-control bands. That is the only fixture in this store that crosses two chapters' files, and it is the strongest thing here: two transcriptions can agree and both be wrong, and the authors' own arithmetic cannot.

Five more of the chapter's printed examples are pinned the same way — p.74's movement, p.74's chase, p.76's Mind Control and the escape from it, and p.79's Clint Castle, who is killed at exactly −5 and saved at −4.

**Clint Castle is also where the page contradicts itself, and both halves are on record.** p.79 says a spent Resolve reduces the damage "to 1 point below this fatal threshold" — one point further from zero, −6 at Clint's −5 threshold — but the worked example two sentences later leaves him at −4, one point *above* it, alive. `gritty_fatal_damage.resolve_reduces_damage_to` carries the printed word, because `gritty.json` is the rulebook and not a repair of it; its `interpretation` carries the reading the arithmetic supports, and its `ambiguity` names the contradiction between them. `TheFatalDamagePrintedWordAndItsWorkedExampleDisagree` reads both the word and the example's own three figures out of `ch04-combat.json` rather than off the canonical file, so it fires if the corpus is ever re-extracted differently, and `TheFatalDamageExampleOnPageSeventyNineComesOutAsPrinted` picks its rescue direction from the interpretation rather than assuming it, so mutating either the fact field or the interpretation moves the fixture.

### Three readings, and each one is derived from something the book does print

Chapter 4 prints three figures it never actually states, and all three are `interpretation` blocks rather than fact fields, for the reason `challenge.json`'s Thresholds table established: **a fact field is a claim that the page states the thing.**

| Reading | Where it comes from | Proved by |
|---|---|---|
| A special effect's duration rounds **up** | `play_meta.json`'s book‑wide rule (p.7); p.76's own example turns 5 net successes into 3 pages | `TheSpecialEffectExampleOnPageSeventySixComesOutAsPrinted` |
| Breaking free reduces a duration by half your net successes, rounding **up** | the same rule; p.76's example removes 2 pages for 3 net successes | `TheBreakFreeExampleOnPageSeventySixComesOutAsPrinted` |
| A Health average rounds **up** | the same rule; `DerivedStatsCalculator.CalculateHealth` already does it | `TheEngineComputesTheHealthThisChapterPrintsIncludingTheRounding` |

**Each of the three computes the other direction too and requires it to be wrong**, which is what stops the derivation from being a restatement. Rounding a special effect down gives two pages where p.76 prints three; averaging Toughness 3 with Might 4 downward gives 3 where the engine gives 4.

There is a fourth, and it is about duration rather than arithmetic: p.73 offers the GM an **alternative effect** for the same 1 Resolve that seizes the initiative, and attaches no duration to it. `seize_initiative_gm_alternative` therefore carries no duration of its own; its `interpretation` names the entry it inherits one from, and `TheGmAlternativeToSeizingTheInitiativeInheritsThePurchasesDuration` requires that entry to exist and to have one. **These two values are here because Chapter 5's slice sent them back.** `resolve.json`'s `spend_combat` had carried them as though p.84 stated them; it does not, p.73 does, and the guard written over that file is what found it. The same test checks that Chapter 5 still defers rather than transcribing.

### Tough Minions is the one place in the book where a half goes down

`play_meta.json` has recorded that since the Chapter 3 slice, as the single exception to the Glossary's rounding rule, with the rule itself left to Chapter 4. It is now here: 1 Minion per **2 full** net successes, "note that you are rounding down in this unique case".

`ToughMinionsIsTheOnePlaceAHalfGoesDownward` asserts the pair together and by name, because **either half alone is a statement about a file rather than about the book** — a rounding rule with no exception recorded, and an exception with no rule to except from, would each pass on their own. The printed example separates them: five net successes defeat two Minions, and the book-wide direction would defeat three.

### The two figures the character engine already computes

Chapter 4 prints the Edge formula (p.73) and the Health formula (p.75), and `DerivedStatsCalculator` has implemented both since long before this store existed — so this file reads into `engine/` twice, the same way `resolve.json` does once for the Resolve table. `TheEngineComputesTheEdgeThisChapterPrints` and `TheEngineComputesTheHealthThisChapterPrintsIncludingTheRounding` hold each pair to one answer.

**The expected figure is built from the file's own formula string, not from arithmetic written in the test.** The operand names are parsed out of `edge = perception + max(agility, intellect)` and looked up on the sheet, so a formula naming Willpower instead of Intellect computes a different number and fails, rather than agreeing with a hard-coded expression that happens to say the same thing. Both are reads and they stay reads: nothing wires the engine to these bytes.

### The ambiguities, and the one that is about the corpus

Four are pinned by name.

- **The Throwing table opens at 3d and the formula beside it floors at 0d**, so a throwing rank of 0d, 1d or 2d is reachable in play and printed nowhere. Either the sentence above the table applies and the throw reaches Close Range, or a figure below the table is not a throw the table describes.
- **The Minion group bonus "does not apply when determining whether an attack can penetrate cover or harm characters using Powers like Armor or Force Field"** — but both are decided by the very attack roll the bonus is granted to. Read strictly it asks for two attack totals against one defence roll, and the page offers no such mechanism.
- **The GM's alternative to seizing the initiative** has no stated duration, and no page says whether the GM's choice between the two effects is fixed for a table, for a campaign, or taken per purchase. Chapter 5 raised the same question from the other side and left it open.
- **Wound Penalties' `ambiguity` records an extraction fault as well as a rules one**, because a reader comparing the entry against an older corpus would find the heading and not the rule. See below.

### The two extraction faults this slice fixed

The corpus filed p.81's `WOUND PENALTIES` heading under the Example of Combat's, as one section called `EXAMPLE OF COMBAT — WOUND PENALTIES` — the tenth Gritty Combat rule and the worked fight merged, with the qualifier inverted. **The prose was never damaged; only the heading was**, which is why it survived: both halves read as English.

**Splitting that section off then exposed a second fault underneath it, in the same block.** The Example of Combat prints as ten paragraphs and arrived as one 3,361-character run with no `\n` in it at all, because every line of a full-width block was handed a null leading gap — a full-width line was treated as having no comparable predecessor, the way a column *resuming below* one genuinely does — so `ParagraphJoiner` had nothing to measure. It is the same shape as the heading fault and it survived for the same reason: the words were all there, in the right order, and only the structure was gone. A full-width line is now measured against the lowest line of the band it closes, or against the full-width line before it; the column resuming below one still gets nothing, deliberately, because its top is half a page from where that column left off and inventing a predecessor there would put a break inside a sentence. `TheExampleOfCombatKeepsItsTenPrintedParagraphs` pins the result.

Both are reading-order faults rather than heading-recognition ones, and both are fixed in the extractor rather than worked around here — `data/rulebook/` is never hand-edited. The rules, the third defect the heading fix also repaired on printed p.107, and the one case the positional rule cannot tell apart are in [`rulebook-corpus.md`](rulebook-corpus.md).

## Chapter 5: two currencies, and the one place this store touches `engine/`

`resolve.json` holds twenty-eight entries across pp.83–85 — the chapter's printed range is 83–86 and p.86 carries no chapter text, which the header says so nobody goes looking for it.

**Every spend is keyed to who holds the pool, and that is `CLAUDE.md`'s settled rule turned into data.** "Only Heroes have Resolve; the GM gets Adversity, spendable on any NPC" was a sentence in a list of things not to re-litigate; it is now a `who` field on each of the twelve spends, `hero` or `gm`, both values transcribed from the chapter's own words (p.84 "Because only Heroes have Resolve", p.85 "As the GM, you have … Adversity"). `EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms` checks it — **and does not trust the id prefix to do it**, because a spend renamed out of `spend_`/`adversity_spend_` would escape a check built on the prefix alone.

**The cross-check that replaced the prefix reached only nine of the twelve, which is why every spend now names its `currency`.** Keying on the cost field — `cost_adversity` is the GM's, `cost_resolve` is a Hero's — says nothing about a spend that prints no cost, and three do not: `spend_combat` defers its costs to Ch.4, `spend_using_powers` charges whatever the Power asks, and `adversity_spend_anything_resolve_can` charges whatever it is imitating. All three were still keyed by their id alone. So `currency` is `resolve` or `adversity` on every spend, transcribed and compared against `CanonicalResolveRules` like any other fact, and the test reads that field rather than the id: rename a spend and drop its `who` and it fails on the entry, not on the naming convention. Where an entry *does* print a cost, the cost and the declared currency have to agree, so the new field cannot drift away from the number beside it.

**The Resolve table is the one mechanic in `data/rules/play/` that the character engine already implements**, so this is the one file here that reads into `engine/` — once, in a test. `DerivedStatsCalculator.CalculateResolve` has computed `(TraitCap − highest relevant rank) × 2` since long before this store existed, and until now the two statements of that rule were unconnected. `TheEngineComputesTheTableThisFileRecords` builds the expected figure out of the *shipped JSON's* rate and pivot and compares it with the engine's answer at three ranks. It is a read and it stays one: nothing wires the engine to these bytes, and `NothingInTheApplicationNamesAPlayRulesFile` still fails if anybody tries. Chapter 5 states the base only — Determination and the Condition/Plot Hook Flaws are Chapter 2's additions to the same figure, and the entry's `ambiguity` records that the chapter never says whether its table is the whole of a character's opening pool.

**Ten of the eleven Powers Chapter 5 exempts are cross-checked against `powers.json`.** `EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData` resolves each printed name to its entries and requires `DerivedStatsCalculator.ResolveAffectedByPower` to answer false — via an explicit `affects_resolve` or via the Movement/Sensory category default. One of them needs a name mapping and **it lives in the test, not in the data**, because it is a reading: the book's "Swinging" is `swing_line` in the rules data. "Super Senses" needs no mapping and is sixteen entries there, because the book prints sixteen options under one Power. The data transcribes the printed names and nothing else.

**The eleventh is Expertise, and it is a carve-out rather than an exemption — which is a divergence, recorded and not repaired.** p.83 reads "Expertise (except for combat skills)", so an Expertise nominated to a combat skill *does* count towards the opening pool. `powers.json` has one Expertise entry carrying one unconditional `affects_resolve: false`, and a single flag has no room for the nomination, so the engine exempts every Expertise there is. The cross-check used to require `false` for all eleven — which made a green suite **ratify** an answer the page contradicts. Expertise is now excluded from that loop by name with the reason, and `ADivergenceTheEngineCannotYetExpress` asserts the gap on purpose: a Standard-tier 6d character with Expertise (Martial Arts) at the 12d cap gets 12 Resolve from the engine where the page wants 0. **It is written to fail when the gap closes** — the message says to swap the two figures and delete the exclusion — because a divergence recorded as a test that would still pass after the fix is one nobody will notice was closed. Teaching the engine the carve-out is `PROGRESS.md` item 14's engine work, not this store's; nothing here changes `powers.json` or `DerivedStatsCalculator`.

**Two things the chapter states about itself are checked against the file rather than merely recorded.** "Three things you can do with Adversity that Heroes can't" is compared with the number of exclusive Adversity spends the file actually carries, and the six ways of earning are split into the ones a simulator could apply and the ones that need a person — a distinction the book does not draw, so it is an `interpretation`, derived from the entries' own `kind` and required to have both halves non-empty.

**References out, never transcriptions in.** The six combat spends are recorded as ids pointing at Ch.4 with `transcribed_here: false`, and Interludes point at Ch.9 for what an interlude is. Copying either chapter in would create a second transcription to disagree with the first — which is the same reason `challenge.json` does not restate the rounding rule `play_meta.json` holds.

**And that is now a guard, because the entry broke its own rule the first time.** `spend_combat` declared `transcribed_here: false`, pointed at Ch.4, and carried two of Ch.4's values anyway — the GM's alternative to seizing the initiative and how long it lasts — under a description claiming the alternative was "printed here rather than there". It is not: **Ch.4 p.73 prints the whole rule**, cost, duration and alternative, so the two fields were precisely the second transcription the policy exists to prevent, and the one that would have gone stale first when Chapter 4's slice transcribes its own page. `AnEntryThatDefersToAnotherChapterCarriesReferencesAndNothingElse` refuses any fact field beyond the flag, the chapter and the deferred ids on an entry that says it does not transcribe — written over the file rather than over the one entry, so the next chapter this store points at is covered without anybody remembering to.

## What is deliberately not in the data

- **The Sample Thresholds table (p.71).** Nine rows of worked examples illustrating thresholds the Thresholds table already states numerically — no mechanic of its own. It is also the one part of Ch.3 the extractor scrambles, being a three‑column table it reads across rather than down.
- **Describing the Action (p.68).** Advice on how to narrate, with no number and no procedure in it.

From Chapter 4:

- **The chapter opening (p.73)**, an essay about tone, and the **Special Cases preamble (p.78)**, which introduces the nine entries under it and states nothing of its own.
- **The Example of Combat (p.81)** — a worked fight rather than a mechanic, and exercised as a fixture against the entries instead, which is the stronger use for it.
- **Two more worked examples in `gritty.json`**: p.80's sword, which shows Gear Limit arithmetic the entry already states as numbers, and p.79's Clint Castle, likewise a fixture rather than a second transcription.

From Chapter 5:

- **The chapter opening (p.83)** — an essay on what makes a character a Hero, with no number and no procedure in it.
- **"Track it with poker chips or glass beads" (pp.83, 85)** — printed twice, and advice about the table rather than a rule of the game.

Each file's header names its own omissions, and a test requires the header to keep saying so.
