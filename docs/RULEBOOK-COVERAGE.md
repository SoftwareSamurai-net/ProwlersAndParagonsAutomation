# Rulebook coverage ledger

**What this is.** A page-by-page record of which of the Ultimate Edition this project has
extracted, which it has deliberately not, and which is an open gap. `PROGRESS.md` says *what is
left to do*; this says *what has been read*, so a sweep can be resumed rather than restarted.

**How to use it.** Work a chapter at a time, in printed-page order. When a page is settled, mark
its row. **Update the Resume marker below before you stop** — that is the whole point of the file.

**Page numbers are printed numbers.** PDF page = printed + 3. Each page prints its number twice,
interleaved, so a footer extracts as `151 5` for printed 15 — decode carefully or cross-check
against the table of contents on PDF 4. The book runs printed 5–193 (PDF 8–196).

**The PDFs are gitignored and live in the main working directory**, not in a worktree's `docs/`.

## Resume marker

| | |
|---|---|
| **Last chapter swept** | 5 (Resolve and Adversity), printed 83–86 |
| **Last page settled** | printed 86 |
| **Next to read** | **Chapter 6, printed p.94** — Gadgets, then Vehicles (94) and Headquarters (100). Ch.6 pp.87–93 are already settled: Gear Limits, armour and weapons are free and untracked, and Custom Gear (p.92) is the twelve extracted features |
| **Then** | Ch.7 pp.105–107 and 109–110 (Toxins on 108 is extracted); Ch.8 pp.111–125 (NPCs, animals, Extras); Ch.9 pp.167–189 |
| **Also outstanding** | The unread-keys finding below is written up but **not actioned** — it needs a decision |
| **Updated** | this sweep |

## Status vocabulary

| Mark | Means |
|---|---|
| **EXTRACTED** | The rules on this page are in `data/rules/` and locked by a test |
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
| 3 | Action | 67–72 | **SWEPT** — NOT APPLICABLE throughout |
| 4 | Combat | 73–82 | **SWEPT** — NOT APPLICABLE throughout |
| 5 | Resolve and Adversity | 83–86 | **SWEPT** — NOT APPLICABLE throughout |
| 6 | Equipment | 87–104 | PARTIAL — UNREAD from p.94 |
| 7 | Environment | 105–110 | PARTIAL — UNREAD apart from Toxins (p.108) |
| 8 | Friends and Foes | 111–166 | PARTIAL — pp.111–125 UNREAD |
| 9 | Superhero Gaming | 167–189 | UNREAD |

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

| Ch. | Pages | What is there | Status |
|---|---|---|---|
| 3 | 67–72 | Challenge rolls, assisting, contests, Defining Moments, judging thresholds | NOT APPLICABLE — play |
| 4 | 73–82 | Edge in combat, actions, range, movement, attacks and defenses, damage, special effects, grappling, combat stunts, minions, gritty rules, worked example | NOT APPLICABLE — play. **Edge here is how the number is used**; how it is *derived* is Ch.2 p.60, which is implemented |
| 5 | 83–86 | Earning and spending Resolve; earning and spending Adversity | NOT APPLICABLE — play. The Resolve a character *starts with* is Ch.2 p.60, implemented |

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

An earlier draft of `docs/HANDOVER.md` said the **ABILITY RANKS and TALENT RANKS tables** (Ch.2
pp.17–18 — Impaired/Undeveloped/…, Clueless/Unskilled/…) were "not in `data/rules/` at all" and
told the next session to extract them. **They are there and they are read**: `rank_guide` on
every entry in `abilities.json` and `talents.json`, bound to `AbilityModel.RankGuide` and
`TalentModel.RankGuide`. The strict guard is what proved it, by *not* failing on them. Nothing
needs extracting; what is missing is that no front end prints the word beside the rank.

---

## T-Kay, parsed line by line (printed p.143)

Done because she is one of the four Heroes that do not reconcile, and the residual survived a
per-element cost check. **Every printed element is transcribed faithfully** — checked against
the page character by character:

| Printed | In `PrebuiltHeroes` | |
|---|---|---|
| Agility 4d, Intellect 3d, Might 3d, Perception 4d, Toughness 3d, Willpower 9d | same | ✓ |
| All twelve Talents (Charm 4d and Streetwise 4d, the rest 3d) | same | ✓ |
| `SUPER POWERS` → `Abilities (Willpower)` | `TraitSourcesByHero["T-Kay\|super"] = ["willpower"]` | ✓ |
| `Determination (+2 Resolve)` | `DeterminationResolve: 2` | ✓ |
| `Flight 8d` | 8 purchased, 1 HP/rank | ✓ |
| `Force Field 12d (Zone)` | `pro:zone_nova:zone_ranged` | ✓ |
| `Lightning Reflexes (Limited: only for Telekinesis)` | `con:limited:significantly_limited` | ✓ |
| `Telekinesis 12d (Area, Overload, Zone)` | `area_burst:area`, `overload`, `zone_nova:zone_ranged` | ✓ |
| `Contacts (club music scene)` | `contacts` ×1 | ✓ |
| `Aversion (crowds)`, `Relationship`, `Secret Identity` | `aversion_fear`, `relationship`, `secret_identity` | ✓ |
| `Gear: None` | none | ✓ |
| Health 6, Resolve 3, Hero Points 125 | reproduced by the engine | ✓ |

Cost, element by element: package 50 + abilities 8 + talents 2 + powers 63 + perks 1 = **124**.
Powers: Determination 10, Flight 8, Force Field 12+2, Lightning Reflexes 3−2 floored to 1,
Telekinesis 24+2+2+2. Every figure is what the rulebook prints for that element.

### Three gaps this parse found

None of them changes a cost, and none is a transcription fault.

1. **A conditionally-active Con is not modelled.** Her sheet prints `Edge 8/14` — the +6 from
   Lightning Reflexes applies only when she acts with Telekinesis. The engine has no notion of a
   Con that switches an effect on and off by condition, so it reports the unrestricted 14, and
   the transcription records 14 to match. The two figures a player actually uses at the table
   are 8 and 14, and this tool can print only one of them.
2. **`SelectedProCon` has no narrative label, and here that hides the rule.** The sheet says
   `Limited: only for Telekinesis`; the engine can store `limited` and a grade, and nothing else.
   So the words that *determine which grade is right* — the whole of the argument recorded in
   `PROGRESS.md` item 1 — cannot be stored beside the Con they justify. This gap is already known
   from Stronghold's `(Item: armor)`, but that case loses flavour where this one loses reasoning.
   `SelectedPerk` and `SelectedFlaw` both carry a `NarrativeDetail`; `SelectedProCon` does not.
3. **The transcription does not record the parentheticals either** — "crowds", "club music
   scene". Correct for its job, which is verifying cost and derived stats, but it means
   `PrebuiltHeroes` is not a complete record of the printed page and should not be read as one.
