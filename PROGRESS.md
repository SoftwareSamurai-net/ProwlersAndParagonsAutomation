# Progress

The single source of truth for what is done and what is left in this project.

**This file must be updated as part of any task that changes what is done or what remains.** Not afterwards, not in a follow-up — in the same change, so the record and the code land together. Previously this information lived in two places (the README roadmap and a gaps list in `CLAUDE.md`) and drifted out of step with reality; both now point here instead.

Keep it honest. A half-finished item stays open with a note on what is missing. "Done" means done and verified, not written.

---

## Current state

| | |
|---|---|
| Rulebook coverage | Chapters 1–2 (Basics, Characters) fully extracted and verified |
| Powers | 141 entries, all mechanically verified against Ch.2 pp.21–48 |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws — all verified, nothing flagged |
| Tests | 2053, run in CI at the same strictness as the build |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Known-wrong data | None outstanding |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8. Hero Point *totals* do not yet reconcile exactly — see [Power-specific Pros and Cons](#1-power-specific-pros-and-cons) for why.

---

## Remaining work

Roughly in the order that unblocks the most.

### 1. Power-specific Pros and Cons

**The largest data gap, and the one that blocks the most.**

`available_pros` / `available_cons` in `powers.json` are project guesses. The rulebook lists each Power's own Pros and Cons *inside that Power's entry* in Chapter 2, and those have never been extracted. The generic list in `pros.json` / `cons.json` is complete and verified; it is the per-Power ones that are missing.

Consequences while this is open:

- The wizard offers plausible-but-unverified Pro/Con options per Power.
- **Hero Point totals cannot be reconciled.** Rebuilding the 20 published Heroes lands a few HP under their 125-point budgets, and the residual is these Pros: Regeneration's *Fast*, Strike's *Throw* and *Deflect*, Telepathy's *Mind Link* and *Cloak Others*, Ensnare's *Line*, Invisibility's *Jamming*, Alternate Form's *Independent Forms*. Pros increase cost, so omitting them under-counts, which matches the direction observed.
- `PrebuiltHeroTests` therefore asserts only the fully-determined parts of a Hero's cost. Closing this gap should let it assert the full 125.

### 2. Gear costs (Chapter 6)

Gear is currently free text with no HP cost (`ChooseGearStep`). Chapter 6 prices custom gear against a Gear Limit derived from Resources. Several published Heroes carry gear that costs Hero Points — Vector's padded costume, Vigilant's armoured suit — so this is a second contributor to the Hero Point gap above, and it needs Chapter 6 extracted first.

### 3. Sources

Not modelled at all. A Source sets the stand-in rank for the 46 rankless (`rank_type: "default"`) Powers — Toughness or Willpower depending on Source — which matters whenever one Power targets another (Drain, Nullify, Dispel and Power Absorption all name a Source). The published Hero sheets group Powers under Source headings (`TECH POWERS`, `MAGIC POWERS`, `INNATE POWERS`, `TRAINED POWERS`), so the data is there to transcribe.

### 4. Qodana baseline

Establish a committed baseline (`--baseline,qodana.sarif.json`) so only *new* problems fail CI. The last recorded scan found 144 problems, 0 errors, all style or dead-code notes — but that figure predates the test project, so re-scan before baselining.

### 5. Remaining rulebook chapters

Chapters 3–9 are not extracted. Rough order of usefulness to the wizard: 6 (Equipment, needed for gear costs), 5 (Resolve, already partly used), 4 (Combat), 8 (Friends and Foes), then the rest.

### 6. Choose and apply a licence

The project is intended for open-source release but is currently unlicensed, which legally means nobody may use it. Apache 2.0 is the working preference: its NOTICE requirement makes the "no rulebook content here, you must own the rulebook" statement travel with any fork. Whatever is chosen must be explicit that it covers this project's code and original text only — not the game system, which is © LakeSide Games. Worth contacting LakeSide before any public release.

---

## Completed work

Newest first. Link the PR so the reasoning stays findable.

### Chapter 1–2 rules verification and test suite — [#5](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/5)

Started as a README correctness check and turned into a full verification pass.

**Data.** Every power entry had carried `cost_per_rank: 1` with `cost_type: "per_rank"`, which was wrong for 91 of 125 — the rulebook prices Powers six different ways. There was no `range` field at all, and no rank-type distinction, so 46 rankless Powers were modelled as ranked and priced from ranks they cannot have. Buff was missing entirely. `powers.json` was regenerated from Ch.2 as 141 entries with correct range, rank type, costs and baselines, and verification moved from a single `needs_review` boolean to per-field `verified_fields` plus a `source_ref` page reference. All 141 descriptions were rewritten: the originals were invented, and 44 of the 46 rankless Powers described per-rank scaling that does not exist.

**Rules fixes.** Danger Sense *replaces* Perception when computing Edge rather than adding to it. Super Speed sets Edge to rank × 3 and had been missing entirely. Determination is 5 HP per Resolve with no rank, not 1 Resolve per rank — a 5× error. Overkill and Weak reduce the per-rank rate by 1 HP, not to a flat 0.5, which had mispriced every 2 and 3 HP/rank Power. The minimum cost is per rank, not 1 HP per Power.

**Tests.** 2053 tests wired into CI. `CanonicalPowers.cs` holds the Range/Rank/Cost printed for all 141 Powers; `RulesDataTests` holds the tier, ability, talent, pro, con, perk and flaw values; `PrebuiltHeroes.cs` transcribes the 20 published Heroes and asserts their printed Edge, Health and Resolve. Three of those Heroes independently confirmed the Danger Sense, Super Speed and Lightning Reflexes fixes.

**Other.** Pros, cons, perks, flaws, abilities, talents and tiers were all checked and found already correct; their flags are cleared. Four wrong claims in the README were corrected. `.gitignore` now excludes `*.pdf` repository-wide and CI fails if a PDF is ever tracked.

### Earlier

Predates this file, reconstructed from git history:

- **Toolchain, Qodana and README** — [#4](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/4). Qodana Community linter wired into CI, analyzer warnings as errors in CI only, README restored.
- **Back-navigation** between wizard steps, and **JSON export** alongside the `.txt` sheet. Both done — do not re-implement.
- **Initial extraction** of chapters 1–2 into `data/rules/`, and the three-layer `data → engine → cli` architecture.

---

## How to maintain this

When you finish a piece of work:

1. Move it out of **Remaining** and into **Completed** with a short account of what changed and *why* — the reasoning is the part that is expensive to recover.
2. Update **Current state** if the headline numbers moved (test count, entry counts, coverage).
3. If the work revealed new gaps, add them to **Remaining** rather than leaving them in a commit message.
4. Link the PR.

If a task turns out to be partly blocked, say so explicitly in the item and name the blocker. An item that quietly narrows its own scope is worse than one that stays open.
