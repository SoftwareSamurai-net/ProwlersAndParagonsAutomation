# The rules engine

Read before touching `engine/`, `sheets/` or anything under `data/rules/`: the cost formulas, the derived stats, the baseline-rank powers, Sources, and the JSON conventions.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Derived stats

```
Edge    = (DangerSense effective rank, else Perception) + max(Agility, Intellect)
          + 6 if Lightning Reflexes
          then floored at SuperSpeed effective rank × 3
Health  = max(⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉)
Resolve = max(0, (TraitCap − highestRelevantRank) × 2)
          + Determination Resolve bought (5 HP each)
          + count of Condition/Plot Hook flaws

TraitCap = CharacterSheet.TraitCapRank ?? tier.TraitCapRank
```

**`TraitCap` there is `DerivedStatsCalculator.EffectiveTraitCap(sheet, tier)`, and a *house* cap
substitutes for the tier’s rather than merely gating validation.** A campaign may impose a ceiling
no tier expresses — Pinnacle City caps a non-superhuman NPC at 6d where the Standard tier allows
12d — and `CharacterSheet.TraitCapRank` is that ceiling, null meaning “the tier’s”.

The owner settled the design question on 2026-09-05, and the arithmetic is the reason there was no
honest alternative: **the cap *is* the datum Resolve is measured from.** Gate on a 6d house cap
while the tier’s 12d keeps doing the arithmetic and a character sitting at 4d is paid
`(12 − 4) × 2 = 16` Resolve for a restraint the campaign imposed on them rather than one they chose.
Substituting pays `(6 − 4) × 2 = 4`, and staying low becomes a decision with a price — spend the
room under the cap on power, or leave it unspent and take the Resolve. The trade is the player’s to
make.

Two consequences come with it, both intended:

- **A tighter cap lowers the Resolve *ceiling* too** — 24 at a 12d cap, 12 at 6d — which is what
  being tied to the cap means in the other direction, and reads as a nerf the first time somebody
  sees it. `ATighterCapLowersTheResolveCeiling` pins both figures.
- **It is noise on a Villain.** Only Heroes have Resolve (below), so on the NPC sheets that
  surfaced this the house cap is doing validation work and the Resolve half is a figure nobody
  should quote. Both halves land; only one is visible per kind of character.

**Read the cap through `EffectiveTraitCap` and nowhere else** — *every* surface that answers “what
is this character built to”, and there is deliberately no number in that sentence. They are
`CalculateResolve`, `CharacterValidator.CheckTraitCap`, `CharacterSession.TraitCap`,
`SheetView`’s meta line, `ReplayVerdict`, `BuildCommand`, `Judgement`, the JSON export, and the
three rank prompts in the terminal wizard (`HpBudgetDisplay`, `PowerBrowser`,
`BuyCharacteristicsStep`). A second spelling of `sheet.TraitCapRank ?? tier.TraitCapRank` is how one
of them ends up disagreeing with the figure printed beside it.

**This paragraph said “six places” while there were eight, and that is why it no longer counts.**
A number in a sentence is not a guard: nothing re-counted it, and the two surfaces it had missed
were the ones a reader would have trusted it about. `TraitCapReadTests` holds the rule instead — it
scans `web/`, `cli/`, `mcp/` and `sheets/` for a **tier-shaped** read of `TraitCapRank` and requires
each one to be listed there by file, with a count and the reason it really is about the tier: the
tier catalogue somebody chooses from, or the tier’s own ceiling printed *beside* the character’s.
It is an allowlist of receivers rather than a denylist of them, so a new read under a name nobody
anticipated is flagged rather than missed, and a listed entry whose reads have gone is flagged too
— an exemption for something that is no longer there permits its whole file for nothing.

**Nonsense is reported and still used, like everything else here.** A house cap above the tier’s is
`TRAIT_CAP_ABOVE_TIER` (`Value` the house cap, `Limit` the tier’s) and one below 1d is
`TRAIT_CAP_BELOW_MINIMUM` (`Limit` 1) — and `EffectiveTraitCap` still answers the number as
written, so the Resolve and the `TRAIT_ABOVE_CAP` findings beside them agree with the character’s
own file. Clamping would be the engine making a design decision about somebody’s game and would
leave the finding describing a number nothing used.

Three Edge details are easy to get wrong and were all bugs at one point: Danger Sense **replaces** Perception rather than adding to it, Lightning Reflexes is a **flat +6** with no rank, and Super Speed is missing from most summaries. All three are verified against Ch.2 — p.60 names the three, and their own entries are pp.25, 33 and 44.

**The engine computes Resolve for every character, and only Heroes have any.** Ch.2 says it twice
— *"Only Heroes have Resolve"* — and Ch.5 gives the GM **Adversity** instead, spendable "on behalf
of any NPC whether they're Villains, Foes, Minions, or Extras". Ch.9's *"the only difference
between Foes and Villains is that Foes have less Health"* is not a contradiction; it compares two
kinds of antagonist, and Resolve is not in scope of that comparison.

This is deliberately **not** modelled. The engine is never told which it is looking at — that is
the same rule `IsVillain` lives under — so it answers the Hero question and the figure is noise on
a Villain. What must not drift is the guidance that reads it: `mcp/QUESTION-POLICY.md` and the
skill both say not to quote the figure, and name the three purchases that turn on it. **Never buy
Determination on a Villain** (Hero Points for Resolve); **Plot Hook and Condition Flaws grant
nothing mechanical** to one; and **the Trait Cap trade does not apply** — Resolve is what a *Hero*
pays for a rank at the cap, so a Villain caps for free. All three were got wrong in the session
that found this, in advice already given to the owner.

**And it is why a Villain's Flaws are the players' handles.** A Hero's Flaw is a drawback bought
with the Resolve it pays out; with the payout gone the drawback is all there is, so the one to
three slots are where the GM decides how the character can be beaten. Prefer a Flaw that bites
without a Resolve payout to notice it — Vulnerability halves active *and* passive defence as a
printed rule, which is the strongest in the book.

Halves always round **up** — the rulebook has a global rule for this (the Glossary in the Introduction, p.7, "Half").

Highest relevant rank = max(all ability ranks, effective ranks of powers where `affects_resolve == true`). Talents excluded. Movement and Sensory category powers excluded by default; `PowerModel.AffectsResolve` overrides this per-power (`super_speed` is explicitly true; 11 non-combat Utility/Special powers are explicitly false). This reproduces the rulebook's list of Resolve-exempt powers exactly — do not "fix" it by naming powers individually.


## Power cost formula

`CostCalculator.PowerCost()` branches on `cost_type`. Only `per_rank` and `per_rank_variable` consume purchased ranks:

| `cost_type` | Cost |
|---|---|
| `per_rank` | `⌈ranks × cost_per_rank⌉` (`cost_per_rank` ∈ 0.5, 1, 2, 3) |
| `flat` | `cost_flat` |
| `per_unit` | `cost_per_unit × Units` |
| `per_rank_variable` | rate from `cost_variants[CostVariantKey]` |
| `flat_variable` | total from `cost_variants[CostVariantKey]` |
| `special` | `boost` mirrors the nominated Trait's rate; `summoning` is ⌈Threat / 2⌉ per rank |

Then pro costs and con discounts are summed in (cons are negative in the data).

- **Overkill/Weak reduce the rate by 1 HP per rank, floored at 0.5** — *not* a ×0.5 multiplier. Ch.2: "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)." A previous version halved the rate, which mispriced every 2 and 3 HP/rank power. The "Brute Option" is the separate Ch.2 p.17 rule for applying Overkill to Might.
- **The minimum is per rank, not per power.** Ch.2: "No Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons." See `MinimumRankedCost`. Specialty is the sole 0 HP power.
- Variable-cost pros/cons (e.g. Charges, Area/Burst) store their variants in `CostModifierRange`; `SelectedProCon.VariantKey` picks the right value.
- **Generic pros/cons are always flat; a power's own pros/cons may change its rate.** `powers.json` carries `power_pros` / `power_cons` for the 106 entries the rulebook attaches to one named Power. Eleven are per-rank — Constructs' Devices is +2 HP *per rank* — so `CostCalculator.ResolveModifiers` returns flat and rate totals separately and `RankedParts` adds the rate part to the Power's own rate before multiplying. Resolution prefers a power's own entry over a generic one with the same id.
- Of those 106: 102 carry a PRO/CON marker inside a Ch.2 Power entry, three carry one in Ch.7's Toxins section (pp.108-109, on Stun and Slay), and one — Deflection's *Physical and Energy* — has no marker because the Power's own text states it as prose. A whole-book sweep for the marker confirms there are no others.
- The rulebook minimum is **1 HP per 2 ranks**, not 1 HP per rank. Reading it the other way puts the floor exactly at the undiscounted cost of a 1 HP/rank power, which silently voids every Con on it. See `MinimumRankedCost`.
- `max_rank == 0` means no ranks are purchasable — either the power has no rank or it is bought flat/per-unit. The validator errors if ranks were bought anyway.


## Baseline-rank powers

27 powers derive a free baseline rank from another Trait; purchased ranks stack on top.

| Relationship | Formula |
|---|---|
| `baseline_equal` | trait rank + purchased |
| `baseline_half` | ⌈trait / 2⌉ + purchased |
| `baseline_fixed` | `fixed_value` + purchased (Running = 3d) |
| `baseline_greater_of` | max(`ability`, effective ranks of `powers`) + purchased — Strike = Might or Martial Arts |
| `baseline_selected_trait` | rank of `SelectedPower.BaselineTraitId` + purchased — Boost, Expertise |

`fixed_value` now lives in the JSON, so the old `RulesRepository.LoadPowers()` post-load patch is gone. `baseline_selected_trait` powers need `BaselineTraitId` on the selection; without it the baseline is 0 and the validator raises an error. Boost's *cost* also comes from that nomination.


## Starting packages

A package **buys the ranks it grants** — `AbilityCost`/`TalentCost` only charge for ranks above the package's own rank. The Superhero Package is 50 HP for 3d in six Abilities and twelve Talents, which is 54 bought separately; the rulebook sells packages "at a small discount", so charging the price on top of full-rate ranks double-pays and makes a package strictly worse than none. That was a real bug, found because seven published Heroes came out exactly 4 HP over — the Superhero discount.


## Pros and Cons on Abilities

Abilities can carry them too, not just Powers — `CharacterSheet.AbilityModifiers`. Overkill on Might is the Brute Option and halves it; everything else is flat, floored at 0. Only ranks the package does not already cover are discountable.

**The Ability is part of the Brute Option, not decoration.** `AbilityCost` did not check *which* Ability carried the Con, so Overkill or Weak on any of the six halved it — 12d Intellect for 6 HP, legal, with one Con on it. Ch.2 p.17 names Might and nothing else. No published Hero moved when it was fixed: Stronghold's four armoured Abilities carry Item, not Overkill.

**A Pro or Con on an Ability the character has bought no ranks in is reported.** `AbilityCost` walks `AbilityRanks`, so a modifier keyed anywhere else never resolves and the Con the player recorded is silently worth nothing.


## Gear costs nothing (mostly)

Ch.6: mundane gear is free and **explicitly not tracked**, so `ChooseGearStep` taking free text with no HP cost is correct — do not "fix" it. A Gear Limit caps the Trait rank usable with mundane gear (6d default); it is not a budget. Signature equipment is a Power with the Item Con.

Custom *features* on mundane gear do cost HP: twelve of them at 1–2 HP each in `gear_features.json`, ten flat and two graded, plus ordinary Pros and Cons on the item. `CostCalculator.GearCost` prices one item and `TotalGearCost` feeds `TotalCost`. Three things about gear differ from Powers:

- **Gear floors at 0 HP, not 1.** "Regardless of Cons, no piece of gear can cost less than 0 Hero Points." Cons discount an item to free and stop.
- **The Item Con is not credited.** Ch.6 says every piece of gear has it, which is a statement of what gear *is*, not a discount to claim — and Item is absent from the same page's list of Cons commonly applied to gear. Crediting it would make every 1 HP feature free.
- **Two-Fisted customises a matched pair for one price.** A pair is one `SelectedGear` with `PairedUnderTwoFisted` set, so it is charged once by construction; the validator checks the Power is actually there.


## Super Senses is one Power

Ch.2: "Regardless of the options you select, Super Senses is always considered a single Power." Its sixteen options are separate `super_senses_*` entries only because each carries its own price. `CostCalculator.TotalPowersCost` therefore sums the group before applying its Cons and its floor **once** — `PowerCost` still answers per option, which is what the wizard and sheet display.

The floor is what this changes: most options cost 1 HP flat, so per option a Con would be swallowed by that option's own floor and be worth nothing. Do not "fix" this by applying the Con to every option instead; that reaches the same numbers but multiplies a Con the sheet wrote once. Super Senses is the **only** such group — Transformation says "Regardless of which Transformation Power you possess", plural, and Form makes no grouping claim, so both stay priced entry by entry.


## Generic Pro/Con applicability is derived, never listed per Power

`powers.json` deliberately has **no** `available_pros` / `available_cons`. The rulebook states applicability inside each generic option — "This Pro applies to Zone Powers", "applies to Powers that only affect you" — not inside the Power, so `ProConApplicability` answers it from the option. Do not reintroduce per-Power lists; the ones that used to exist were invented, left 68 of the 141 Powers with no generic Pro at all, and offered the Ranged Pro on six Self-range Powers.

Only constraints the book prints for every Power are enforced: `applies_to_ranges` (Self/Touch/Ranged/Zone/Special, Ch.2 p.19) and `applies_to_rank_types` (Degrades alone). Ten entries carry one. A Range of **Special** is never filtered out — the book says such Powers work in ways their description defines, so nothing can be ruled out for them.

**One thing does come from the Power, and it only ever widens: `pros_allowed_by_own_text`.** A Power whose own printed text tells you to apply a named generic option overrides that option's Range rule. Force Field is the only entry that carries it: it is Self range, and its entry (Ch.2 p.29) reads "Apply the **Zone** Pro to shield large areas, the **Ranged** Pro to shield things at a distance, or the **Area** Pro to shield large areas at a distance" — while T-Kay, printed on p.143, has `Force Field 12d (Zone)`. Until it existed, both editors refused that Pro and the validator called a Hero in the rulebook illegal, so **a published character could not be built in this tool**. It is the same shape as Deflection covering both attack types, which the book also states as prose rather than as a marked PRO. **This is not the removed `available_pros` list and must not become one**: that list guessed which options suited a Power and filtered absolutely; this records a printed sentence, needs one behind every entry, and there is a test that no other Power claims it and that the three Pros reach no other Self-range Power. A sweep of Ch.2 for prose naming a generic Pro found one other case, Illusions and the Zone Pro, left alone because no printed character exercises it and the book gives no price for Zone on a Zone-range Power.

Everything else an option states — "Powers that inflict physical or energy damage", "that can be activated and deactivated at will" — is an `applicability_caveat`: shown to the player, never enforced. Enforcing it would mean ~7 booleans × 141 Powers of fresh guesswork. Ch.2 calls the list "not intended to cover every possible option" and puts it under GM approval, so a caveat is the honest model. **A caveat must never become a filter** — there is a test.

**Those ten enforced constraints are checked by `CharacterValidator` as well as by the two editors' pickers**, as `PRO_NOT_APPLICABLE` / `CON_NOT_APPLICABLE`. They were the pickers' business alone until then, so a submitted character could carry the Ranged Pro on a Self-range Power and exit 0 — a hole in the claim the headless command exists to make. Only generic options on a Power are checked: a Pro printed inside a Power's own entry is applicable to that Power by definition, and gear and Abilities have no Range for an option to object to. A test builds every one of the 141 Powers with every option the pickers offer it and asserts the validator refuses none of them, so the two can never disagree in either direction.


## Sources, and the default rank

Six of them (Ch.2 p.16), in `sources.json`: Innate, Magic, Psychic, Super, Tech, Trained. Each names the Ability that stands in as a **rankless** Power's rank whenever Powers act on other Powers (Drain, Nullify, Dispel, Power Absorption, Power Mimicry). Innate/Super/Tech → Toughness; Magic/Psychic/**Trained** → Willpower. Trained is the one people guess wrong.

`DerivedStatsCalculator.GetRankAgainstPowers` answers that. It is **deliberately separate from `GetEffectiveRank`**, which still returns 0 for a rankless Power. The default rank substitutes only against other Powers — it is not the Power's rank, and folding it in would change Edge and Resolve away from the figures the published sheets print. There is a test; do not "simplify" the two into one.

A Source costs nothing and changes no rank, so a missing one **on a Power** is a warning, not an error. **On an Ability or Talent it is not reported at all**, and that difference is the rule rather than a gap: Ch.2 p.16 gives those two a default — Innate and Trained — so silence means "on its default". A Power has no default, which is why `POWER_WITHOUT_SOURCE` exists and no Trait equivalent does.


## Sheets group Powers by Source

`SourceGrouping` lives in `engine/`, not in a renderer, because the text sheet, the JSON export, the GM review and the browser's sheet all need the same answer. Published sheets print `TECH POWERS`, `MAGIC POWERS` and so on rather than one flat list, and all four surfaces do too.

- Groups follow `sources.json` order, so a sheet does not reshuffle as Powers are added.
- A Power with **no** Source still prints, under a plain `POWERS` heading at the end. Do not "tidy" this by filtering it out — leaving a Power off its own character sheet is worse than showing it unsourced, and the validator already warns. A **Trait** with no Source is different: it is not unsourced, it is on its default, so it prints nothing.
- **A group can hold no Powers at all** — a Trait bought through powered armour on a character with no Tech Power — so nothing that renders groups may gate on `SelectedPowers.Count`. Three places did. The Powers *tab* is the one deliberate exception: it edits Powers, and a heading with nothing under it says less than no heading.


## Sources on Abilities and Talents

Ch.2 p.16: **every** Ability, Talent and Power has a Source; Abilities are usually Innate and Talents usually Trained "at least when dealing with ordinary people. When dealing with supers and characters who aren't human, however, anything goes." `CharacterSheet.AbilitySources` / `TalentSources` record only the Traits that deviate, which is exactly what a sheet prints.

- **Abilities are not marked on the Abilities block, and that is correct.** A sheet records the Source as an `Abilities (Might, Toughness)` line *inside* the relevant Power group. Stronghold's `TECH POWERS` opens with his four armoured Abilities. The Abilities and Talents tables stay plain lists of ranks.
- **Do not derive the line from rank.** Ch.2 p.64 — the *random generation* chapter — says "Sources for your Powers and Abilities with a rank of 7d or greater", and reading that as a threshold is contradicted by the sheets in both directions: Alabama Slammer marks 6d Perception and Toughness; Citizen Soldier leaves 9d Willpower unmarked. It is an author's exception list, so it is stored. `ThePrintedTraitSourcesAreNotARankThreshold` names both counterexamples.
- **Two printed shapes, and one extrapolated from them.** Ch.8 shows named Traits — `Abilities (Might, Toughness)` — and, where every Ability *and* every Talent share a Source, `Abilities and Talents (All)` (both Heralds and Nano). **A single block alone reading `(All)` is this project's extension of that convention**, not something any sheet prints: the three Heroes that mark Talents at all mark every Trait. Named Traits print in `abilities.json`/`talents.json` order, which is the order the sheets print them in — and is also alphabetical, so no test can tell the two apart.
- **A Trait explicitly set to its own default prints nothing**, same as one never touched. Same Source, same statement — so the editors remove the entry rather than storing it, or an ordinary character prints eighteen lines restating the rulebook.
- **Abilities on one Source are split by the Pros and Cons they carry**, because the marking covers the whole printed line. The engine prints `(Item)` where the book prints `(Item: armor)`: `SelectedProCon` has no free-text label. Recorded, not tuned away.
- A Source costs nothing and changes no rank, which is why the **persistence round trip cannot see one** through cost or the derived stats. It compares the Source headings and trait lines instead — still an engine answer, not a field list.


## Perk cost formula

Flat-cost perks: pay `Cost` HP. Per-unit perks: pay `CostPerUnit × Units` HP. `SelectedPerk(PerkId, Units, NarrativeDetail?)` — Units is always 1 for flat perks.


## The 1d minimum on every Trait

Ch.2 states it twice — once for Abilities (p.17), once for Talents (p.18): **no rank can be lower than 1d**, and "ordinary people have 2d in every" one. So **a character has all six Abilities and all twelve Talents**, and 0d is not a low rank but a Trait nobody can be without. `TRAIT_BELOW_MINIMUM` enforces it.

- **It costs Hero Points.** Without a package you pay for all eighteen at 1d — 18 HP before anything interesting. The Civilian Package's 35 HP for 2d in all eighteen is 36 points of ranks, which is exactly the "small discount" the rulebook calls a package. That is the corroboration, and it is why the reading is not negotiable.
- **All twenty published Heroes take a package**, so every one of their Traits sits at or above its floor. That is why rebuilding them never caught this, and why a rule can be missing for a long time without the strongest test in the suite noticing.
- **`TRAIT_BELOW_PACKAGE` is the other floor**: a package's granted ranks "cannot be lowered below the package rank". It costs nothing to break — `AbilityCost` and `TalentCost` charge only for ranks above what the package covers — so the mistake was free and therefore silent. It is the rule that proved Herald (Airmid)'s recorded package impossible.
- **A printed sheet still shows 0d for a Trait not filled in yet**, and should: the sheet is a form, and 0d is a fact about the page in front of you. The validator is what says the character is not finished.

## JSON data conventions

- All JSON keys use `snake_case` (matched by `JsonNamingPolicy.SnakeCaseLower`)
- `pros.json` cost modifiers are positive integers; `cons.json` cost modifiers are **negative** integers
- Powers with `cost_type: "special"` have no numeric cost — `CostCalculator.PerRankRate` must handle each such power by id or throw
- **`powers.json` tracks verification per field, not with a boolean.** Every entry has `verified_fields` (any of `range`, `rank_type`, `cost`, `prerequisite`, `description`, `pros_cons`) and a `source_ref` page reference. `PowerModel.MechanicsVerified` requires the first four; `NeedsReview` is its inverse. The old single flag drifted badly — 27 entries were unflagged while their costs were wrong — so when you change a mechanical field, update `verified_fields` to match what you actually checked.
- **Power `description` values in `data/rules/` stay original text written from the rulebook entry, never rulebook prose.** The rulebook text now lives in `data/rulebook/` instead — see [`rulebook-corpus.md`](rulebook-corpus.md) — and the two must not be merged: `data/rules/` is what the deployed site serves, and the descriptions there are what a player reads while choosing. Descriptions exist so a player can tell what they are choosing and what resists it, and they must agree with the mechanics beside them — `PowerDescriptionTests` fails a rankless power whose description claims per-rank scaling, which is how the original set went wrong on 44 of the 46 rankless powers.
- `powers.json` has **141** entries. Form, Transformation and Super Senses are single Powers in the rulebook but each of their options is bought separately at its own cost, so each option is its own entry. Super Senses is nonetheless *costed* as one Power — see "Super Senses is one Power" above; splitting it is a storage decision, not a rules one.
- `gear_features.json` holds the twelve Ch.6 custom features. `cost_type` is `flat` (with `cost`) or `flat_variable` (with `cost_range`, for the two the rulebook prices at 1 to 2 HP).


## The engine never touches the filesystem

`RulesRepository` reads through `IRulesSource`, not `File.ReadAllText`. Two implementations ship: `FileSystemRulesSource` (the CLI) and `InMemoryRulesSource` (any host that loads the data itself — a browser has no filesystem). `RulesRepository(string)` and `FromBasePath` still work exactly as before.

**Keep `IRulesSource` synchronous.** Making it async would push `await` through every lazy collection and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for no gain. A host that can only load asynchronously does that once at startup and hands over strings.

`RulesRepository.DataFileNames` lists every file a self-loading host must fetch — it cannot glob a directory that isn't there. **Add a new rules file to that list**, or a browser build silently runs on an incomplete rules set; a test enforces it.

**Both halves of that rule are now enforced rather than asserted, and neither was.** This section
and `TheEngineHasNoNetwork`'s own doc comment both claimed the engine has no filesystem access, and
nothing checked it: a reachable, non-throwing `File.Exists(...)` in `CostCalculator.AbilityCost`
left all 3,734 tests green. `TheEngineHasNoFilesystemAccess` bans `File.`, `Directory.`,
`FileStream`, `StreamReader`/`StreamWriter` and a written-out `using System.IO;` across `engine/`
and `sheets/`. Three things about it are deliberate:

- **`Path.Combine` and `Path.GetFullPath` stay legal.** They are string manipulation with nothing
  on the far end, and `RulesRepository.FromBasePath` legitimately calls one.
- **`FileSystemRulesSource.cs` is excluded by name**, because it is the documented seam a host with
  a disk is supposed to use — an exception rather than a loosened ban for everyone.
- **Comments are blanked before the scan.** Three files here name `File.ReadAllText` or
  `AppContext.BaseDirectory` as history, and a guard that cannot tell an explanation from a
  directive taxes the explanation.

The `using` is in the list because the SDK's implicit usings already bring `System.IO` into every
file — which is exactly why a stray `File.Exists` compiles in silence and needed a guard rather
than a missing import to catch it.


