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
| Power-specific Pros/Cons | 102 entries across 61 Powers, verified |
| Other rules data | Tiers, abilities, talents, pros, cons, perks, flaws — all verified, nothing flagged |
| Tests | 2406, run in CI at the same strictness as the build |
| Wizard | All six creation steps working, with back-navigation and `.txt` + `.json` export |
| Known-wrong data | None outstanding |

The engine reproduces the printed Edge, Health and Resolve of all 20 pre-built Heroes in Chapter 8, and rebuilds **13 of the 20 to exactly their 125 Hero Point budget**. The remaining seven are within 6 HP, each for a recorded reason — see [Close the last seven Heroes](#1-close-the-last-seven-heroes).

---

## Remaining work

Roughly in the order that unblocks the most. Items 1–3 have a task brief with the exact
rulebook line numbers and data shapes in
[`docs/HANDOVER-toxins-and-gear.md`](docs/HANDOVER-toxins-and-gear.md).

### 0. Three toxin Pros/Cons are missing

The original Pros/Cons extraction was scoped to Chapter 2. Sweeping the **whole** book for
`^(PRO|CON) [+-]\d+ Hero Point` turns up exactly three more, all in Ch.7's Toxins section
(lines 4090–4112), and each applies to a specific Power:

| Name | Kind | Cost | Applies to |
|---|---|---|---|
| Caustic | con | −2 | Stun |
| Lethal Disease | pro | +6 | Slay |
| Non-Lethal Disease | pro | +2 | Stun |

Small and certain. They belong in `power_pros` / `power_cons` on `stun` and `slay`, and move
the totals to 105 entries across 62 Powers. This is the last known gap in Pros and Cons —
the sweep was exhaustive.

### 1. Close the last seven Heroes

Thirteen of the twenty published Heroes now rebuild to exactly 125 Hero Points. The other seven are held at a known residual in `PrebuiltHeroes.BuildByHero`, each with a reason:

| Hero | Residual | Why |
|---|---|---|
| Vector | −6 | Four times any other residual, so worth one look. His sheet reads `Deflection (Physical and Energy)`, but the Power says to pick *one* type — covering both is probably not free |
| Vigilant | −1 | Its Jo Sticks are *Upgraded*, a custom gear feature not modelled |
| Shadow | +2 | Unexplained |
| Herald (Airmid) | +2 | Unresolved |
| Herald (Scathach) | +1 | Strike carries four Pros and Cons at once — most likely a variant reading |
| Talon | +1 | Unresolved |
| T-Kay | −1 | `Limited: only for Telekinesis` does not say which grade |

Gear does not explain these — see the item below for why that earlier guess was wrong. The two ambiguous grades (`Side Effect: collateral damage`, `Limited: only for Telekinesis`) are guesses that could be revisited, but only ±1–2 HP hangs on them, so do not tune them just to force a zero — that would be fitting the model to the answer.

One thing genuinely cannot be modelled as things stand: Eidolon's `Omni-Power (Mind Link)` applies Telepathy's Pro to a *mimicked* Power. Pros are stored per Power, so there is nowhere for it to live. Eidolon reconciles anyway, so it costs nothing today.

### 1b. `available_pros` / `available_cons` are still guesses

Separate from the above. These lists say which *generic* Pros and Cons suit each Power, and they were invented by this project. The rulebook does not state applicability per Power — it states it inside each generic entry ("This Pro applies to Powers that inflict physical or energy damage"). So the honest fix is probably to drop the per-Power lists and filter generically from those constraints, rather than to keep curating 141 guesses. Worth deciding before the wizard leans on them further.

### 2. Custom gear features (Chapter 6)

**Correcting an earlier assumption.** Gear was listed here as an unpriced cost. Chapter 6 says the opposite for most of it: *"Players don't have to worry about buying mundane gear… none of this needs to be tracked."* A **Gear Limit** caps the Trait rank you can apply while using mundane gear (6d by default) — it is not a budget and costs nothing. So `ChooseGearStep` charging nothing for free-text gear is **correct**, and the Heroes' residuals are not explained by gear as previously recorded.

Gear comes in three tiers, and only one of them is missing:

| Tier | Cost | Modelled? |
|---|---|---|
| Mundane gear | Free, untracked | Yes — free text, correctly free |
| Signature equipment | A Power with the Item Con | Yes |
| **Custom features on mundane gear** | **1–6 HP each** | **No** |

The gap is the third. Twelve features, 1–2 HP each (Ch.6 lines 3247–3285): Accurate/Very Accurate, Bonded, Collapsible, Concealed, Deflecting, Fitted, Hardened, Masterpiece, Powerful/Very Powerful, Reinforced, Silenced, Upgraded. Gear can also take ordinary Pros and Cons, and has its own floor — no piece costs less than **0** HP, unlike a Power's floor of 1. Two-Fisted customises two identical weapons for the price of one.

This would be the first thing to spend Hero Points outside `TotalCost`'s current four categories, so the budget panel needs it too.

Only one published Hero's residual looks like this: Vigilant's Jo Sticks are *Upgraded*, and he is 1 HP short. Shadow's *Silenced* pistols and Psidearm's *Thrown* batons point the other way, since Psidearm already reconciles exactly — so the authors may not have charged for them consistently. Do not tune to these.

### 3. Sources

Not modelled at all. A Source sets the stand-in rank for the 46 rankless (`rank_type: "default"`) Powers — Toughness or Willpower depending on Source — which matters whenever one Power targets another (Drain, Nullify, Dispel and Power Absorption all name a Source). The published Hero sheets group Powers under Source headings (`TECH POWERS`, `MAGIC POWERS`, `INNATE POWERS`, `TRAINED POWERS`), so the data is there to transcribe.

### 4. Qodana baseline

Establish a committed baseline (`--baseline,qodana.sarif.json`) so only *new* problems fail CI. The last recorded scan found 144 problems, 0 errors, all style or dead-code notes — but that figure predates the test project, so re-scan before baselining.

### 5. Remaining rulebook chapters

Chapters 3–9 are not extracted. Rough order of usefulness to the wizard: 6 (Equipment, needed for gear costs), 5 (Resolve, already partly used), 4 (Combat), 8 (Friends and Foes), then the rest.

### 6. Web SPA front end, hosted on softwaresamurai.net

The wizard is CLI-only. The `data → engine → cli` split exists precisely so another front
end can be added without touching the rules logic, and that promise has not been tested yet.

The shape that keeps the promise: a `web/` layer alongside `cli/`, with the engine exposed
over a small HTTP API (or compiled to WebAssembly, which would let the whole thing be a
static site with no server to run). Both keep `engine/` free of presentation. Do **not**
reimplement cost or validation logic in the browser — that is the one rule the architecture
exists to protect, and duplicating it would guarantee drift from the tests.

The extraction guide's original condition still applies: build this after the CLI handles
the full creation flow, which it now does.

### 7. Assisted character creation from a description

Give the tool a prompt like "a washed-up boxer who punches through time" and have it produce
a legal, costed character. This is worth doing *because* the rules engine is now trustworthy:
the model proposes, and `CostCalculator` and `CharacterValidator` decide what is legal, so it
cannot invent a character that does not add up. That ordering is the whole value — a model
inventing costs directly would be a random number generator with good prose.

Wants a machine-usable surface first: something that takes a structured character definition,
validates it, and returns errors the caller can act on. That is close to what
`CharacterSheetExporter`'s JSON already emits, read in reverse.

### 8. Printable character sheet with hero/villain styling

A proper sheet rather than the current `.txt` dump: blue and white for Heroes, black and red
for Villains. Mechanically the two are identical — Ch.9 is explicit that Villains are built
exactly like Heroes, just without a Hero Point budget — so this is presentation only, and
belongs in the front end, not the engine.

### 9. Choose and apply a licence

The project is intended for open-source release but is currently unlicensed, which legally means nobody may use it. Apache 2.0 is the working preference: its NOTICE requirement makes the "no rulebook content here, you must own the rulebook" statement travel with any fork. Whatever is chosen must be explicit that it covers this project's code and original text only — not the game system, which is © LakeSide Games. Worth contacting LakeSide before any public release.

---

## Completed work

Newest first. Link the PR so the reasoning stays findable.

### Pros and Cons on Abilities, and what gear actually costs — [#9](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/9)

**Abilities can carry Pros and Cons.** The rulebook's Brute Option is Overkill applied to Might, and Stronghold buys four Abilities through his powered armour, so his sheet reads `Abilities (Agility, Might, Perception, Toughness) (Item: armor)`. Nothing modelled that. `CharacterSheet.AbilityModifiers` and `CostCalculator.AbilityCost` now do, including the Brute Option's half price and a floor of zero. Stronghold's Item Con on four Abilities is worth exactly −4, which is exactly what he was over by: **13 of 20 Heroes now rebuild to exactly 125**.

**Gear turned out to be a wrong assumption, not a missing feature.** This file previously listed gear as an unpriced cost contributing to the Hero Point gap. Chapter 6 says mundane gear is free and explicitly not tracked, and a Gear Limit is a cap on the Trait rank you can apply while using it, not a budget. So the wizard's free-text gear step was right all along, and the residuals recorded against "has gear" were misattributed — they are now corrected. What genuinely remains is custom *features* on mundane gear at 1–6 HP each, which is a much smaller and better-defined gap.

### Hero Pros/Cons transcription, and the package double-charge — [#8](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/8)

Transcribed the Pros and Cons each published Hero sheet carries, which turned the Hero Point reconstruction from a rough check into an exact one for most of them.

**It found a second cost bug, and a bigger one than the last.** With the Pros and Cons in, seven Heroes came out over budget by exactly 4 — including Citizen Soldier, who has no Pros or Cons at all, so it could not have been the new data. 4 is exactly what the Superhero Package saves: it costs 50 Hero Points for 3d in six Abilities and twelve Talents, which is 54 bought separately. `TotalCost` had been adding the package price **on top of** every rank at full price, charging twice for the ranks the package grants. That made taking a package strictly worse than not taking one, which cannot be right for something the rulebook sells "at a small discount".

With packages paying for what they grant, **12 of the 20 Heroes rebuild to exactly 125** — seven on the Superhero Package, four on the Hero Package, one on the Civilian. The sheets never print which package was taken, but for those twelve exactly one package lands the total on the point, so the inference is safe.

That is the whole engine end to end against numbers the authors published: package-aware ability and talent costs, baseline ranks, every cost type, and both generic and Power-specific Pros and Cons.

### Power-specific Pros and Cons — [#7](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/7)

Extracted the 102 Pros and Cons the rulebook prints inside individual Power entries, across 61 Powers. Completeness was checked by counting every PRO/CON marker in the chapter against the entries parsed: 102 markers, 102 entries, none unaccounted for.

These are not simply more generic Pros. Every generic one is a flat Hero Point change, but 23 of these are not: ten change the Power's cost **per rank** (Constructs' *Devices* is +2 per rank, so on a 6-rank Constructs it is +12, not +2), five are graded, five scale with how many extra Sources the Power reaches, and Alternate Form's *Independent Forms* scales per power level. `PowerProConModel` and `CostCalculator` now separate flat modifiers from rate modifiers to handle that.

**This found a real bug in the previous change.** The minimum-cost floor had been read as "1 Hero Point per rank", but the rulebook's parenthesis — "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons" — is the ranked form of the same rule, so the floor is 1 per *2* ranks. Read the old way, the floor sat exactly at the undiscounted cost of any 1 HP/rank Power, which silently made every Con on such a Power worth nothing. It went unnoticed until a test applied a Con to Armor and got no discount.

The wizard now offers a Power's own Pros and Cons first, marked as belonging to that Power, and prompts for a variant or quantity where one is needed.

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
