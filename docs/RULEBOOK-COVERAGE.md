# Rulebook coverage ledger

**What this is.** A page-by-page record of which of the Ultimate Edition this project has
extracted, which it has deliberately not, and which is an open gap. `PROGRESS.md` says *what is
left to do*; this says *what has been read*, so a sweep can be resumed rather than restarted.

**How to use it.** Work a chapter at a time, in printed-page order. When a page is settled, mark
its row. **Update the Resume marker below before you stop** — that is the whole point of the file.

**Page numbers are printed numbers.** PDF page = printed + 3. Each page prints its number twice,
interleaved, so a footer extracts as `151 5` for printed 15 — decode carefully or cross-check
against the table of contents on PDF 4. The PDF is 195 pages, so the last printed number is 192;
chapter text runs printed 5–188, printed 189 is the blank Hero Sheet form, and 190–192 are the
backers and the index. (An earlier version of this line said "printed 5–193 (PDF 8–196)", which is
wrong on both figures and contradicted the resume marker four lines below it.)

**The PDFs are gitignored and live in the main working directory**, not in a worktree's `docs/`.

## Resume marker

| | |
|---|---|
| **Text extraction** | **DONE for the whole book, and regenerable.** All ten chapters are in `data/rulebook/`, printed pp.5–188, 1525 sections, each carrying its printed page. Rebuild with `dotnet run --project tools/RulebookExtractor -- <pdf> data/rulebook` |
| **Do not trust the first extraction's reputation** | The corpus shipped once with every chapter opening scrambled, 135 empty sections and 83 doubled page numbers in mid-sentence, and the tests passed. See the completed entry in `PROGRESS.md`. **The damaged prose still read as English**, so judge a change here by re-running the extractor and the corpus tests, not by reading a paragraph and finding it plausible |
| **Rules extraction** | Ch.1–2 complete, plus Ch.6 custom gear and Ch.7 toxins. **Ch.3, Ch.4 and Ch.5 are extracted** into `data/rules/play/` rather than `data/rules/` — see their rows below and [`docs/guide/play-rules.md`](guide/play-rules.md). That is the whole of the play block, pp.67–86. **Ch.6 pp.94–104 are now extracted too**, into `data/rules/` and read by nothing yet — see the Chapter 6 section below |
| **Next to read for *rules*** | **Chapter 6 pp.94–104 are now extracted** — Gadgets (94), Vehicles (94–100) and Headquarters (100–103), into `data/rules/gadgets.json`, `vehicles.json` and `headquarters.json`, held to the pages by `Chapter6RulesDataTests`. Printed p.104 carries no chapter text. What is left in this chapter is pp.87–93: Gear Limits, armour and weapons, of which pp.87–90 are already in `data/rules/play/equipment.json` for the fight engine and Custom Gear (p.92) is the twelve features in `gear_features.json` |
| **Then** | Ch.7 pp.105–107 and 109–110 (Toxins on 108 is extracted); Ch.8 pp.111–125 (NPC, animal and Extra stat blocks — GM material, so decide whether it is in scope at all); Ch.9 pp.167–188 (printed 189 is the blank Hero Sheet form, not chapter text) |
| **Reading it is now cheap** | The prose is in `data/rulebook/`, so a sweep no longer needs the PDF — grep the corpus, and open the page only to check a table |
| **Updated** | the Chapter 6 gadgets, vehicles and headquarters slice |

## The two stores

| | `data/rules/` | `data/rulebook/` |
|---|---|---|
| Holds | mechanics: ids, costs, ranges, rank types, and this project's own descriptions | the book's printed text, chapter by chapter |
| Read by | the engine — every cost and every verdict | the accounts server, behind a sign-in: bundled into `worker/`, never staged into `wwwroot`, and served only to a session — see `docs/ACCOUNTS-SETUP.md` |
| Served publicly | **yes**, copied into `wwwroot` by the web csproj | **no**, deliberately excluded |
| On a disagreement | wins | loses |

**The corpus exists by the author's permission to this repository's owner**, which is what changed the rule that no rulebook wording may appear here. That permission does not travel with a fork.

## Status vocabulary

| Mark | Means |
|---|---|
| **EXTRACTED** | The rules on this page are in `data/rules/` — or, for play rules, `data/rules/play/` — and locked by a test |
| **IMPLEMENTED** | Not data, but logic in `engine/` that a test covers |
| **NOT APPLICABLE** | Play rules a character generator does not need. Names *why* |
| **GAP** | This tool should have it and does not. Must appear in `PROGRESS.md` |
| **PARTIAL** | Some of the page is in, some is not. Says which |
| **UNREAD** | Nobody has checked |

## Chapters

| Ch. | Title | Printed pages | Status |
|---|---|---|---|
| — | Introduction | 5–8 | **SWEPT** — one rule, extracted |
| 1 | Basics | 9–12 | **SWEPT** — NOT APPLICABLE throughout |
| 2 | Characters | 13–65 | EXTRACTED, **with a caveat** — see the unread-keys finding |
| 3 | Action | 67–72 | **EXTRACTED as play rules** — every mechanic on pp.67–71 is in `data/rules/play/`, locked by `PlayRulesDataTests` against `CanonicalChallengeRules`. Still NOT APPLICABLE to *character creation*, which is the question the rest of this column answers |
| 4 | Combat | 73–82 | **EXTRACTED as play rules** — every mechanic on pp.73–79 is in `data/rules/play/combat.json` and the ten optional Gritty Combat Rules on pp.79–81 are in `gritty.json`, both locked by `PlayRulesDataTests` against `CanonicalCombatRules` and `CanonicalGrittyRules`. The Example of Combat on p.81 is not an entry; it is the fixture the entries are made to resolve. Still NOT APPLICABLE to *character creation*, except that the Edge and Health formulas it prints are the ones `DerivedStatsCalculator` already implements — tests now hold each pair to the same answer |
| 5 | Resolve and Adversity | 83–86 | **EXTRACTED as play rules** — every mechanic on pp.83–85 is in `data/rules/play/resolve.json`, locked by `PlayRulesDataTests` against `CanonicalResolveRules`; p.86 carries no chapter text. Still NOT APPLICABLE to *character creation*, except that the starting-Resolve table it prints is what `DerivedStatsCalculator.CalculateResolve` already implements — a test now holds the two to the same answer |
| 6 | Equipment | 87–104 | PARTIAL — **pp.94–104 EXTRACTED**, see the detail below. pp.87–93 are the remainder: pp.87–90 are transcribed into `data/rules/play/equipment.json` for the fight engine rather than for creation, and p.92's Custom Gear is `gear_features.json` |
| 7 | Environment | 105–110 | PARTIAL — UNREAD apart from Toxins (p.108) |
| 8 | Friends and Foes | 111–166 | PARTIAL — pp.111–125 UNREAD |
| 9 | Superhero Gaming | 167–188 | UNREAD |

### Introduction, printed 5–8 — swept

| Pages | What is there | Status |
|---|---|---|
| 5–6 | What roleplaying is, welcome | NOT APPLICABLE — no mechanics |
| 7 | **Glossary**, including **"Half: always round up, regardless of context"** | IMPLEMENTED — the global halving rule the engine applies everywhere |
| 8 | Glossary continued | NOT APPLICABLE |

### Chapter 1, Basics, printed 9–12 — swept

A summary of the whole game, restating what later chapters give in full. Nothing here is
character-creation data, and nothing in it is unique to it.

| Pages | What is there | Status |
|---|---|---|
| 9 | Characters, Challenge Rolls, **Trait Ranks** (ordinary people are 1d–6d) | NOT APPLICABLE — the 1d floor and the ordinary-person range are taken from Ch.2 pp.17–18, which state them as rules rather than as summary |
| 10 | Thresholds, Embellishments, narrative-control table | NOT APPLICABLE — play |
| 11 | Combat, Resolve and Adversity, in summary | NOT APPLICABLE — play |
| 12 | Chapter close | NOT APPLICABLE |

### Chapters 3, 4 and 5, printed 67–86 — swept

Swept together because the test is the same for all three: does any page price something in
Hero Points? **Across twenty pages the phrase appears once**, on p.70, and it points backwards
at Ch.2's Advancement rather than pricing anything — a Defining Moment permanently costs 1d of
an Ability, "this doesn't prevent you from spending Hero Points to raise that Ability in the
future".

**All three chapters have since been extracted on their own merits, and the sweep above is not what
changed.** The sweep asked whether a *character generator* needs them, and the answer is still no.
The simulator of `PROGRESS.md` item 14 does need them, so pp.67–71, pp.73–81 and pp.83–85 are now
verified data in `data/rules/play/`, held to the page by `PlayRulesDataTests`.

**Two things the slices found that the sweep did not, and both are the same shape**: a formula the
chapter prints turns out to be one `engine/` already implements, so the chapter is not wholly
inapplicable to character creation after all. The starting-Resolve table is Ch.5's own (p.83); Edge
and Health are Ch.4's (Edge on p.73 under `EDGE`, Health on p.75 under `HEALTH`, both reprinted from
Ch.2 p.60 — this line said p.75 for the pair). Nothing had ever compared the engine's
arithmetic to the pages it came from; three tests do now, each building its expected figure out of
the shipped JSON rather than out of a number typed into the test.

| Ch. | Pages | What is there | Status |
|---|---|---|---|
| 3 | 67–72 | Challenge rolls, assisting, contests, Defining Moments, judging thresholds | **EXTRACTED** — as *play* rules, into `data/rules/play/`, which is a subdirectory so that no csproj's non-recursive `data\rules\*.json` glob can reach it. Still NOT APPLICABLE to character creation |
| 4 | 73–82 | Edge in combat, actions, range, movement, attacks and defenses, damage, Health, healing, special effects, grappling, combat stunts, minions, nine special cases, ten gritty rules, worked example | **EXTRACTED** — as *play* rules, into `data/rules/play/combat.json` (pp.73–79) and `gritty.json` (pp.79–81), beside Ch.3 and Ch.5 and under the same non-recursive glob. Still NOT APPLICABLE to character creation. **Edge here is how the number is used, and p.73 also prints how it is derived** — as does p.75 for Health, both reprinted from Ch.2 p.60 and both implemented in `DerivedStatsCalculator`; the two statements of each are now compared |
| 5 | 83–86 | Earning and spending Resolve; earning and spending Adversity | **EXTRACTED** — as *play* rules, into `data/rules/play/resolve.json`, beside Ch.3 and under the same non-recursive glob. Still NOT APPLICABLE to character creation. The starting-Resolve **table** is Ch.5's own (p.83) and is implemented; Determination and the Condition/Plot Hook Flaws that add to it are Ch.2 p.60 |

### Chapter 6, printed 94–104 — extracted

Three files under `data/rules/`, **none of them on `RulesRepository.DataFileNames`**. That list is
the contract for a host which fetches the character rules over HTTP, so putting a file on it makes
the browser download it before its first render — a decision about the payload that belongs to the
slice which teaches `CostCalculator` what a vehicle or a headquarters costs, not to the slice that
read the pages. `RulesSourceTests.DataFileNamesListsEveryShippedRulesFile` names the three
exclusions and requires each one to still exist, and `Chapter6RulesDataTests` holds all three to the
rulebook meanwhile — so they are extracted data rather than unread data.

**Two currencies, and neither is Hero Points.** A vehicle is bought in **Vehicle Points** at 25 per
Hero Point of the Unique Vehicle Perk; a headquarters in **Base Points** at 3 per Hero Point of the
Headquarters Perk. Every price in those two files is in the second currency. A **Gadget** is the
exception in the other direction: it is not bought at all — a successful build *pays out* Hero
Points equal to twice its Complexity.

| Pages | What is there | Status |
|---|---|---|
| 94 | **Gadgets** — the 6d Technology prerequisite, Complexity 3 to the builder's Technology rank, the Technology roll against Complexity as its own threshold, the pool of twice Complexity in Hero Points, the Item Con that is credited nothing, the instability die, Science and Medicine as alternative Talents, and the ceiling of half the builder's Intellect per issue | **EXTRACTED** — `data/rules/gadgets.json`, seven entries |
| 94–95 | **Vehicle characteristics and vehicle combat** — Body, Speed, Control and Weapons; the vehicular Gear Limit; piloting, Edge, chases, attacks and defences, damage and repair, targeting a system; capital ships and capital-ship ramming | **EXTRACTED** — `data/rules/vehicles.json` |
| 96 | **Foe and Minion pilots**, **mundane vs unique vehicles**, what Vehicle Points buy, the **six stock vehicles**, and the optional cap on upgrading one | **EXTRACTED** — same file |
| 96–100 | The **23 vehicle features**, each with a Vehicle Point price: four are drawbacks that pay points back (Giant, Open Cockpit, Swimming, Transforming), one is free (Running), five are priced per unit and one is graded | **EXTRACTED** — same file |
| 97–98 | The three **mundane vehicle tables** — 24 air/space rows, 15 ground, 15 water | **EXTRACTED** — derived out of `data/rulebook/ch06-equipment.json` rather than typed a second time; see the pairing note below |
| 100–103 | **Headquarters** — the Perk, the +1d an advanced feature may be worth on a roll, and all **22 base features** with the price of each and what each grade buys. Mobile and Training Facilities carry mechanics of their own and have entries beside the table | **EXTRACTED** — `data/rules/headquarters.json`, five entries |
| 104 | Nothing — the page carries no chapter text at all | **NOT APPLICABLE** |

**The three mundane vehicle tables are paired, and the pairing is this project's reading.** The book
sets each as four columns and the extractor reads a table of three or more columns **across** rather
than down (see [`guide/rulebook-corpus.md`](guide/rulebook-corpus.md)), so each arrives as a block of
names beside their Body and a separate block of Speed, Control and Weapons. Pairing them row by row
in printed order is recorded as an `interpretation` on each entry rather than as a fact, and it was
confirmed against the PDF with `RulebookExtractor --page 97` and `--page 98`: every Body row and its
figures share a baseline exactly. **Three witnesses hold it where that diagnostic cannot run** — every
capital-ship row's Control is p.94's −3d per 30 Health (nine rows at once), all six of p.96's stock
vehicles reprint a row characteristic for characteristic, and p.96's Foe example reprints the sedan's
7d Body. A misalignment of one row breaks all three, which was measured by rotating one table's
figures against its names.

**All six printed totals reconcile, and the sixth took a Con to get there.** Priced through the rules
on its own page — 1 Vehicle Point per rank of Body, Speed and Weapons, 2 per rank of Control, plus
the features — every stock vehicle comes out at its printed total exactly. The **Submersible** was
recorded here as coming to 15 against a printed **14**, which was wrong: its feature line prints
`Rader (Sonar)`, and the parenthesis names the Con that Ch.2 p.38 prints inside the Radar entry —
`CON Sonar (−1): This Power only works underwater`. Radar is 3 Hero Points, the Con takes it to 2,
and Unique Systems converts one for one, so the sonar is 2 Vehicle Points and the total is 14. The
test derives that price out of `powers.json` rather than restating it, so a change to Radar or to
its Con fails there instead of leaving a stale constant behind.

**What is still owed on these three files is a consumer, not a reading.** Nothing in the application
loads them, which is why they are off `DataFileNames` — see
[`guide/rules-engine.md`](guide/rules-engine.md) for what a slice that priced a vehicle or a
headquarters would have to add.

---

## Finding: five keys in `creation_rules.json` that nothing reads, and two have rotted

Turned up by the Ch.3 sweep, following its one Hero Point mention back into Ch.2.

`data/rules/creation_rules.json` has **nine** top-level keys. `CreationRulesModel` declares
**four** — `sequence`, `flaw_rules`, `optional_packages`, `trait_rank_limits`. The other five are
deserialized into nothing and silently dropped:

| Key | What it holds | |
|---|---|---|
| `sequence_notes` | per-step guidance | unread |
| `trait_costs` | "1 Hero Point per rank" for Abilities and Talents | unread; duplicates `abilities.json` / `talents.json` |
| `derived_characteristics` | prose formulas for Edge, Health and Resolve | unread — **and wrong**, see below |
| `advancement` | HP earned per issue, the floating Trait Cap option, retcons | unread; post-creation, so nothing consumes it |
| `global_caps` | the optional rule letting Overkill/Weak push a Trait past the cap | unread |

**Two of them contradict rules this project has since settled and locked by tests elsewhere.**
Because nothing reads them, nothing could notice:

- `derived_characteristics.resolve` says the formula adds **"Determination ranks"**, with a note
  reading "Determination adds +1/rank". The settled rule — asserted by tests and stated in
  `CLAUDE.md` — is that **Determination has no rank**: it is 5 Hero Points per 1 Resolve.
- `derived_characteristics.edge` gives `Perception + max(Agility, Intellect)` and notes that
  Danger Sense "can modify" Edge. The settled rule is that Danger Sense **replaces** Perception,
  which is a different formula and was a real bug when the code read it the other way.

Both blocks carry `"needs_review": false`, so they assert they have been checked.

**This is the two-places-for-one-fact failure the repository already warns about**, in a place
nobody was looking: `PROGRESS.md` and the README both drifted from the code that way, and both
now point at a single source. A prose copy of the derived-stat formulas inside a rules file is
the same thing — a second statement of a rule, in a file whose entire premise is that a data
edit contradicting the book fails a test. This one cannot fail a test, because it is not loaded.

### Actioned, and the guard found four more

1. **`derived_characteristics` and `trait_costs` are deleted.** Both restated what the engine and
   the other rules files already say authoritatively, and one had rotted.
   `RulesFileCoverageTests.TheDerivedStatFormulasAreNotRestatedInTheRulesData` asserts they do
   not come back.
2. **`advancement` and `global_caps` are modelled and tested** against Ch.2 pp.52 and 62. Nothing
   consumes either — this tool builds a starting character — which is exactly why they needed a
   test rather than a consumer. `global_caps` also gained the **converse** optional rule the
   sweep found in the same passage: a GM may require every damaging Trait at or above a chosen
   rank to carry Overkill or Weak, "never lower than 9d". Only the first half had been recorded.
3. **`RulesFileCoverageTests` now deserializes every rules file with
   `JsonUnmappedMemberHandling.Disallow`**, so a key no model reads fails a test by name. The
   engine's own reader stays lenient on purpose: a rules file gaining a field should be a failing
   test, never a broken site.

**On its first run that guard failed on four more files**, none of which anybody had looked at:

| File | Unread | Now |
|---|---|---|
| `talents.json` | `special_use` — Medicine treats wounds on a Hard (2) roll, Technology repairs objects, both 1 point per net success | modelled as `TalentSpecialUse` |
| `pros.json` | `source_ref` | modelled — so a Pro's page citation was unreadable, and no test could check one |
| `cons.json` | `source_ref` | modelled, same |
| `creation_rules.json` | `trait_rank_limits.maximum` and `flaw_rules.notes` | modelled |

### One thing this corrected, which was written down wrong

An earlier session note said the **ABILITY RANKS and TALENT RANKS tables** (Ch.2
pp.17–18 — Impaired/Undeveloped/…, Clueless/Unskilled/…) were "not in `data/rules/` at all" and
told the next session to extract them. **They are there and they are read**: `rank_guide` on
every entry in `abilities.json` and `talents.json`, bound to `AbilityModel.RankGuide` and
`TalentModel.RankGuide`. The strict guard is what proved it, by *not* failing on them. Nothing
needs extracting; what is missing is that no front end prints the word beside the rank.

---

## T-Kay, parsed line by line (printed p.143)

Done while she was one of the four Heroes that did not reconcile, and the residual survived a
per-element cost check. **Every printed element is transcribed faithfully** — checked against
the page character by character:

**She reconciles now, and nothing in this parse changed to make her.** Her sheet prints
`Limited: only for Telekinesis` with no grade, and the rulebook prints no rule mapping a
restriction onto one of its three; the owner ruled it *somewhat limited* (−1) on 2026-09-06,
which takes her from 124 to exactly 125. **The cost breakdown below is not the pre-ruling one
with a note attached — it is the post-ruling one, recomputed through `CostCalculator` element by
element**, so the Lightning Reflexes line, the Powers subtotal and the grand total all moved. The
transcription either side of it did not: no Power, rank, Pro, Perk or Flaw was touched.

| Printed | In `PrebuiltHeroes` | |
|---|---|---|
| Agility 4d, Intellect 3d, Might 3d, Perception 4d, Toughness 3d, Willpower 9d | same | ✓ |
| All twelve Talents (Charm 4d and Streetwise 4d, the rest 3d) | same | ✓ |
| `SUPER POWERS` → `Abilities (Willpower)` | `TraitSourcesByHero["T-Kay\|super"] = ["willpower"]` | ✓ |
| `Determination (+2 Resolve)` | `DeterminationResolve: 2` | ✓ |
| `Flight 8d` | 8 purchased, 1 HP/rank | ✓ |
| `Force Field 12d (Zone)` | `pro:zone_nova:zone_ranged` | ✓ |
| `Lightning Reflexes (Limited: only for Telekinesis)` | `con:limited:somewhat_limited` — the grade is the owner's ruling of 2026-09-06, not a printed value | ✓ |
| `Telekinesis 12d (Area, Overload, Zone)` | `area_burst:area`, `overload`, `zone_nova:zone_ranged` | ✓ |
| `Contacts (club music scene)` | `contacts` ×1 | ✓ |
| `Aversion (crowds)`, `Relationship`, `Secret Identity` | `aversion_fear`, `relationship`, `secret_identity` | ✓ |
| `Gear: None` | none | ✓ |
| Health 6, Resolve 3, Hero Points 125 | reproduced by the engine | ✓ |

Cost, element by element: package 50 + abilities 8 + talents 2 + powers 64 + perks 1 = **125**.
Powers: Determination 10, Flight 8, Force Field 12+2, **Lightning Reflexes 3−1 = 2**,
Telekinesis 24+2+2+2. Every figure is what the rulebook prints for that element.

That Lightning Reflexes line is the only one the ruling moves, and it is also why the two harsher
readings were indistinguishable. Lightning Reflexes is a flat 3 HP unranked Power, so −2 takes it
to 1 outright and −4 would take it to −1, which the 1 HP floor on an unranked Power holds at 1 —
measured through `CostCalculator.PowerCost`, not reasoned about: 2, 1, 1 for the three grades.
Only the mildest grade is visible in her total at all, which is a fact about the floor and was
never an argument for the grade.

### Three gaps this parse found

None of them changes a cost, and none is a transcription fault.

1. **A conditionally-active Con is not modelled.** Her sheet prints `Edge 8/14` — the +6 from
   Lightning Reflexes applies only when she acts with Telekinesis. The engine has no notion of a
   Con that switches an effect on and off by condition, so it reports the unrestricted 14, and
   the transcription records 14 to match. The two figures a player actually uses at the table
   are 8 and 14, and this tool can print only one of them.
2. **`SelectedProCon` has no narrative label, and here that hides the rule.** The sheet says
   `Limited: only for Telekinesis`; the engine can store `limited` and a grade, and nothing else.
   So the words the owner's ruling of 2026-09-06 was *about* — "only for Telekinesis", the whole
   of what makes one grade right — cannot be stored beside the Con they justify, and the ruling
   has to live in a comment in `PrebuiltHeroes` instead. This gap is already known
   from Stronghold's `(Item: armor)`, but that case loses flavour where this one loses reasoning.
   `SelectedPerk` and `SelectedFlaw` both carry a `NarrativeDetail`; `SelectedProCon` does not.
3. **The transcription does not record the parentheticals either** — "crowds", "club music
   scene". Correct for its job, which is verifying cost and derived stats, but it means
   `PrebuiltHeroes` is not a complete record of the printed page and should not be read as one.
