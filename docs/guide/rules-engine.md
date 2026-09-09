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

Highest relevant rank = max(all ability ranks, effective ranks of the powers `ResolveAffectedBySelection` answers true for). Talents excluded. Movement and Sensory category powers excluded by default; `PowerModel.AffectsResolve` overrides this per-power (`super_speed` is explicitly true; 11 non-combat Utility/Special powers are explicitly false). This reproduces the rulebook's list of Resolve-exempt powers — do not "fix" it by naming powers individually. **The question is asked of the purchase and not of the entry**, because one Power's answer depends on what the player nominated — the next paragraph.

**One Power's answer depends on the purchase and not on the entry, so ask `ResolveAffectedBySelection` rather than `ResolveAffectedByPower`.** Ch.5 p.83 exempts "Expertise (except for combat skills)", which is a carve-out and not an exemption: whether a given Expertise counts depends on the Trait the player nominated in `SelectedPower.BaselineTraitId`. `affects_resolve: false` stays the entry's default and `affects_resolve_when_nominated` on the same entry names the nominations that overturn it, so a Standard-tier 6d character with Expertise at the 12d cap opens on **0** Resolve with a combat nomination and **12** with any other. Every other Power answers identically either way — the new list is empty on all 140 of them — and a host that has a `SelectedPower` in hand should still use the selection-aware call, because a row about somebody's sheet that printed the entry's flag would contradict the Resolve figure beside it.

**"Combat skills" is a reading, and this is the repository's: an Expertise nominated to one of the four Abilities the book's own Attack and Defense table uses to attack or defend.** The phrase occurs once in the whole extracted corpus, on p.83, and is never defined. Ch.4 p.75's table is the book's own statement of which Traits fight, and its five rows name four Abilities between them — Unarmed (Might; Agility or Toughness or Power), Melee Weapon (Might; Agility or ½ Toughness or Power), Ranged Weapon (Agility; …), Physical Power (Power; Agility or ½ Toughness or Power), Mental Power (Power; Willpower or Power). So the set is **Might, Agility, Toughness, Willpower**. Intellect and Perception are in no row and are out. Agility is also the Trait behind the book's own Expertise (Agility: Firearms).

**Toughness and Willpower are in because defending is combat.** They are p.75's *passive* defences, and an earlier reading dropped them on the ground that "resisting is not a skill exercised" — but that page draws the active/passive distinction for one purpose only, to say you cannot use an active defence while immobilised or surprised. Both kinds are defences you roll. p.17 says so of each: Toughness "is used to resist Powers that affect you physically, as well as physical agents or toxins", Willpower "is used to defend against Powers that affect the mind or soul, and to resist negative emotions and impulses".

**No Talent is ever a combat skill, and the one printed datum is what settles it.** p.18 names all twelve and Martial Arts is a *Power*, so the natural first guess at the set — the combat Talents — has no members. Better than that argument: Scáthach (Ch.8 p.135) prints Expertise (Academics: Strategy and Tactics) at 12d, which is the Standard Trait Cap exactly, beside a Resolve of **5**. That figure only comes out if the Expertise is exempt; counting it gives **3**. `PrebuiltHeroTests.ScathachsPrintedResolveIsWhatSaysATalentNominationDoesNotCount` asserts both numbers through the engine. Vector and Herald (Airmid) are the two Heroes with Expertise (Science: …), and widening the set to Talents moves their printed Resolve too.

**A nominated Power is not modelled, because it is not a legal Expertise.** Ch.2 p.28: "Your specialization must fall under one of your Abilities or Talents", and the printed baseline sentence agrees — "the rank of the Ability or Talent it falls under". So `CharacterValidator` reports `EXPERTISE_NOMINATION_NOT_A_TRAIT` and the engine answers nothing: an illegal character is reported, never repaired. `ResolveAffectedBySelection` used to ask p.83's own attack-or-defence question of the nominated Power instead, which gave an illegal sheet a defensible Resolve and hid the finding. Boost is unaffected — its own entry (Ch.2 p.24) raises "one specific Ability, Talent, or Power", so a Power nomination there is correct.

**The reading errs towards counting, and therefore towards *less* Resolve for a Hero.** The nomination is a Trait id and the specialisation itself is free text, so Expertise (Agility: Firearms) and Expertise (Agility: Acrobatics) cannot be told apart and both count, where a GM would probably count only the first. **The rival reading is that the specialisation decides rather than the nomination**, and it has textual support: p.83 says "combat *skills*" where the book's own word for the twelve on p.18 is Talents, which is some evidence the phrase was meant to reach a Talent. It is not modelled because there is nothing to model it from — the specialisation is a free-text string nobody can classify. **p.83 gives the GM the final say, and that is the release valve.** `PowerDataTests` pins the set and carries the whole argument, `RulebookCorpusTests` holds the p.75 table and each quoted p.17 sentence to its page, and the twenty published Heroes are the backstop. The book's *silence* — not the reading — is recorded as an `ambiguity` on `resolve_exceptions`; see [`play-rules.md`](play-rules.md).


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

The rest of Chapter 6 is beside that as `data/rules/gear.json`, and it is loaded now: `RulesRepository.Equipment` answers the file and `GearCatalogue` flattens its three pickable tables into the 108 rows the Gear step and the command palette offer. See the section on it below for the armour table, the shields rule, the weapon-features glossary and the copy rule the three weapons tables live under.

**A row on a character is an id and never a copy.** `SelectedGear.CatalogueId` is nullable, and null is the ordinary case — p.91's list is "examples, not a catalogue of prices", so a character may carry a letter from their mother. What a Battle Axe is worth stays in `gear.json`, so a corrected figure corrects every sheet that names the row; a sheet that stored the figure would be a second copy to disagree with the first. An id that resolves to nothing is `UNKNOWN_GEAR_CATALOGUE_ROW` — **reported, never repaired**, because dropping it would hand back a plain item where a Battle Axe was sent, which is the misspelled-field-name failure the strict reader exists for. A payload written before the field existed reads back byte-identical.

Custom *features* on mundane gear do cost HP: twelve of them at 1–2 HP each in `gear_features.json`, ten flat and two graded, plus ordinary Pros and Cons on the item. `CostCalculator.GearCost` prices one item and `TotalGearCost` feeds `TotalCost`. Three things about gear differ from Powers:

- **Gear floors at 0 HP, not 1.** "Regardless of Cons, no piece of gear can cost less than 0 Hero Points." Cons discount an item to free and stop.
- **The Item Con is not credited, even when a host writes it down.** Ch.6 says every piece of gear has it, which is a statement of what gear *is*, not a discount to claim — and Item is absent from the same page's list of Cons commonly applied to gear. Crediting it would make every 1 HP feature free. **That sentence was true of what the engine *added* and false of the answer it gave** until this was fixed: nothing puts Item on an item automatically, so a submitted character recording the Con the book says every object carries got `cons.json`'s −1 like any other. `GearCost` now skips it, by the id `gear.json`'s own `item_con_id` names. The Con still *prints*, because it is true of the item; the picker no longer offers it, because an option nobody may apply is not one to offer.
- **Two-Fisted customises a matched pair for one price.** A pair is one `SelectedGear` with `PairedUnderTwoFisted` set, so it is charged once by construction; the validator checks the Power is actually there.


## `data/rules/gear.json` — Chapter 6's equipment, read by the Gear step and the palette

**It is on `RulesRepository.DataFileNames`**, so every self-loading host fetches it before its first
render, `RulesRepository.Equipment` answers it and `GearCatalogue` flattens it. It was extracted a
slice before it was consumed, which is the order the 141 Powers were done in and the order that made
them trustworthy — and the pair the extraction slice left behind (an entry on
`RulesSourceTests.NotLoadedByTheRepository` and a test asserting the file was *off* the load list)
moved together, which is what that pair was for. `EquipmentDataTests` still holds the file to the
page; the repository is lenient at runtime, as it is for every other file.

**The payload cost, since item 5 records payload as a characteristic.** `gear.json` is about 73 KiB
uncompressed and 13 KiB gzipped, on a rules payload that was about 232 KiB — so the rules the browser
fetches at boot grew by roughly a third. Against the 27 MiB first load that is about a quarter of one
percent, and every framework asset is fingerprinted, so a returning visitor pays nothing. The figures
are approximate deliberately: to the byte, they are a number in a sentence that nothing re-counts and
that every data edit moves. The `data/rules/play/` files stay off the list and `PlayPayloadTests`
proves it.

**`GearCatalogue` is the one flattening**, because five surfaces need the same answer — the browser's
Gear step, the terminal wizard's, the command palette, `GearFormatter` and `CharacterValidator` — and
a second flattening is a second thing to disagree with the first. It hangs off the repository rather
than being registered in DI, unlike `ProConApplicability` and `SourceGrouping`: those answer
questions across several files and are a host's to wire up; this is one file's contents in the shape
every reader of them wants, so four hosts do not each have to remember to build one. Row ids are
prefixed by table (`armor:`, `weapon:`, `item:`) — the weapons rows have no id column, because they
are a byte-for-byte copy of the play store's and that store keys them by name, so their segment is
derived from the printed name. **The prefix prevents a collision rather than fixing one**: strip it
and today's 108 segments are still distinct, and `GearCatalogueTests` says so rather than claiming a
control that is not there.

**What is in it** is Chapter 6 pp.88–93: the rule that a worn suit grants the Armor Power at
Toughness plus its bonus, the nine-row armour table, Bulky and Rigid, shields, the eighteen-entry
Weapon Features glossary, p.91's thirty-six mundane items with the rule that none of them is bought,
p.92's Custom Gear rule, and p.93's Pros and Cons on gear. **p.93's twelve priced custom features are
not in it** — they have been `gear_features.json` since the custom-gear slice, and a test now reads
p.93's own headings out of the corpus and requires each to resolve to one of the twelve, which is the
check that says the twelve are all of them.

**The Gear Limit caps a worn suit, and p.88 is not where that is written.** p.88 gives the Armor
rank as Toughness plus the suit's bonus and never restates the limit, so the effective rank is the
lesser of Toughness and the Gear Limit, *plus* the bonus — 8d in a standard game, whatever the
wearer's Toughness. **p.87 settles it and the file records it as an `interpretation`, not as an
`ambiguity`**: the limit is about equipment that boosts a Trait "(usually armor and weapons)", its
worked example fixes the order of the arithmetic at limit-plus-bonus, it says outright that this
makes armour less useful to a superhuman, and its one printed exception is for melee weapons alone.
A Power's rank substituted in for Toughness under the same entry's second sentence is a Trait rank
and is capped the same way. This was recorded here as the book's silence, which it is not — the
silence is p.88's, and **a doubt the book has settled is one the consumer slice would resolve by
guessing.** `ThePageEightySevenGearLimitAnswersWhatPageEightyEightLeavesOut` reads all four
sentences out of the corpus, so it fails if the page it rests on is not the page that is there.

**It is called `gear.json` and not `equipment.json` because that name was taken.** The play store's
file is `data/rules/play/equipment.json`, and `PlayPayloadTests` refuses any file in `data/rules/`
whose *basename* matches one under `play/` — by name as well as by path, because a play file copied
up one level is outside the directory and inside every host's glob. That guard is right and the
collision was the new file's fault. `gear` is the book's own word for mundane kit, and it puts the
file beside `gear_features.json`.

### The three weapons tables are stored twice, and a test holds the copies equal

This is the one thing here that looks like a mistake and is not. `data/rules/play/equipment.json`
carries the sixty-three ancient, modern and advanced rows, derived from the corpus and checked there.
**Neither store can read the other**: `engine/` may not reach into `data/rules/play/` (no glob
descends into it, it is off `DataFileNames`, and `PlayPayloadTests` refuses both routes), and
`play/` may not reach out to `data/rules/`. A fight needs a weapon's dice and so does a character
sheet, so the rows are **copied byte for byte** into `gear.json` and
`EquipmentDataTests.TheWeaponTablesAreACopyOfThePlayStoresAndAreHeldEqualToIt` compares them as
serialized JSON — as serialized JSON rather than field by field, so a field added to one copy and not
the other fails too.

**Edit both in the same commit or the guard goes red**, and do not reconcile a difference by editing
whichever copy is easier to reach. This repository already holds two copies of one truth this way —
`TranscriptLibrary`'s bake into `worker/transcripts-corpus.js`, and `worker/corpus.js` — and the test
between them is what makes it honest rather than a duplication to tidy away.

### p.93 and `CostCalculator`: one disagreement settled, one still recorded

- **The Item Con is settled: not credited.** `GearCost` skips it, by the id `gear.json`'s own
  `item_con_id` names, and `EquipmentDataTests.TheItemConIsNotCreditedEvenWhenAHostRecordsIt` is the
  test that used to assert the opposite. The page settles neither reading outright — Item is a
  description of gear and uncreditable, or a Con like any other with the 0 HP floor stopping it
  paying out — and the argument for this one is the page's own: **Item is absent from the twenty-four
  Cons p.93 names as commonly applied to gear**, which is the tell that it says what gear is rather
  than what it costs. CLAUDE.md's settled list has said so throughout; the engine was the half that
  disagreed.
- **Overkill and Weak are named as commonly applied to gear and are still worth nothing on it.** Ch.2
  defines both as a change to a Power's cost *per rank*, gear has no rank, and
  `CostCalculator.ResolveConCost` returns 0 for either before it looks anything up. The page does not
  say what either should be worth, so this stays recorded rather than repaired and the test asserting
  it stands. **What did change is that neither is offered**: an option that provably costs and
  discounts nothing is a decision with no consequence to put in front of a player.

### Which Pros and Cons a piece of gear may take

`ProConPicker.Target.Gear` used to fall through to *every* option there is, because nothing in the
data said what gear could take — which is how the Item Con came to be on offer for a sword. p.93
names twenty-four, and each one now records `"gear"` in its own `applicable_to`, so gear filters like
the other two targets. That is the same shape the whole `available_pros` argument settled on:
**applicability is stated inside the option, never curated per subject.**

- **One printed name needs a mapping and it lives in the test, not the data.** The page writes "Area
  of Effect" where `pros.json` carries `area_burst`, whose printed name is "Area / Burst (Area of
  Effect)". `zone_nova` is the other half of the same printed Pro and is deliberately **not** marked:
  the Weapon Features glossary's own Area/Burst entry defers to `area_burst` by id, and that is the
  only place in the book where this Pro and a piece of gear meet. The rival reading — that the page
  names the whole Pro and reaches both grades — is refuted by nothing printed, and p.93 calls its
  list "not exhaustive" under GM approval, so it is a one-line widening if a table wants it.
- **Overkill and Weak are marked and not offered, and the rule is asked of the price.**
  `ProConApplicability.AppliesToGear` drops any option whose `cost_type` is `special`, which is
  exactly those two — rather than naming two ids, so a third option priced that way is caught by the
  same sentence. The data says what the page names; the engine says what it can price.

### A worn suit's Armor rank is a derived figure, not a Power and not a note

`DerivedStatsCalculator.ArmorFromGear(sheet)` answers `min(base, gear limit) + bonus`, where the base
is the wearer's Toughness or their own Armor Power's effective rank, whichever is higher (p.88's
second sentence). `ArmorRankInSuit(sheet, bonus)` answers the same question for a row not yet chosen,
which is what a picker shows beside an armour row. Both print through hosts: the Gear step beside the
item, the `.txt` sheet's derived block, and `derived.armor_from_gear` in the JSON export.

**Nothing adds an Armor Power to the sheet, and that is the point.** Mundane gear is free; an Armor
Power costs Hero Points; the engine does not make design decisions about somebody's character. The
figure is reported and the character is unchanged — `ArmourRankTests` asserts an armoured character
costs exactly what the same character costs with the suit taken off. **Only the best suit answers**,
because a character wearing two is wearing one and carrying the other.

**The 6d default Gear Limit is a C# constant, and that is a trade rather than an oversight.**
`DerivedStatsCalculator.DefaultGearLimitRank` is 6, p.87. The figure is extracted — in
`data/rules/play/equipment.json`, the play store, which `engine/` may not read and `play/` cannot do
without. The alternative was a second transcription of one number into `gear.json`, which is the
duplication the weapon-table copy rule already carries for sixty-three rows and a poor trade for one.
So the constant is here and `ArmourRankTests.TheDefaultGearLimitIsThePlayStoresAndTheBooksSameFigure`
is the seam — it reads both stores, as a test may and neither store may, and checks the entry's own
`source_ref` so the pin is to p.87 rather than to whatever an entry called `gear_limit` happens to
say. A table that raised the limit is read through `EffectiveGearLimit(sheet)` and nowhere else, for
the reason `EffectiveTraitCap` is read through one method: the raised limit is a **pair**, a switch
and a rank, and a rank left on the table while the switch is off is a figure the table has not
adopted.

**Two of p.91's granted Powers are bought by naming an option, and the id alone is half an answer.**
Immunity is priced per unit because each one is "named and paid for separately", and Super Senses —
Acute is printed "Acute (X)": so the Gas Mask's Toxins, its "limited to" clause, and the Parabolic
Microphone's Hearing were all missing from a file that said only `immunity` and `super_senses_acute`.
`granted_power_selection` and `granted_power_limited_to` carry them. **Resolving an id is not
checking it** — both of those resolve, and an under-specified grant looks exactly like a right one —
so `EveryGrantedPowerBoughtByNamingAnOptionRecordsTheOptionThePagePrints` reads *which* Powers need an
option off `powers.json` (a name carrying `(X)`, or a per-unit price) rather than listing them, and
requires the option to be a word p.91 prints in that item's own sentence.

**Three items on p.91 print a `Label (n)` figure and only two of them are about breaking the item.**
Handcuffs' Inhuman (5) and Zip Tie's Brutal (4) are thresholds to break; Rappelling Gear's Easy (0)
is the **Agility roll that uses it**, and it was recorded as a third break threshold — which says the
gear falls apart on a roll nobody fails, and dropped the Trait the page names. `break_threshold` and
`use_threshold` are separate fields for that reason. **No check that reads a value and compares it to
a figure typed beside it can see this**: 0 and "Easy" are both correct figures and the defect is which
field they sit in, so `EveryThresholdOnAnItemIsTheKindOfRollThePagePrints` slices p.91 into per-item
sentences and asks what kind of roll each one describes.

### The discipline is the play store's, applied to a creation-side file

`verified_fields` from a closed list declared in the file's own header and always including
`description`; a `source_ref` naming a page in Chapter 6; a `printed_under` checked against the
heading the corpus found on that page; `ambiguity` for the book's silence and `interpretation` for
this project's reading, never a fact field for either. Descriptions are original text — a test fails
any run of ten consecutive words shared with `data/rulebook/ch06-equipment.json`. And
`EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook` walks the models by reflection so a field
added to the data cannot quietly go unchecked, with a positive control on the walk (161 leaves today)
and a negative control that feeds an unregistered field to the same classifier and requires it to be
reported.

**Three tables are derived rather than transcribed**, because a second transcription is a second
thing to disagree with the first: the nine armour rows and the thirty-six equipment items are parsed
out of the corpus, and the weapons rows come from the play copy. Each parse has a closed vocabulary,
has to tile with nothing following the last row, and is driven one row past the end and required to
throw. What *is* transcribed beside them is each table's row count and one anchor row — a derivation
cannot notice a table that has lost half of itself when the expectation lost the same half.

## Gadgets, vehicles and headquarters: what a character owns, in three currencies

`gadgets.json`, `vehicles.json` and `headquarters.json` are Ch.6 pp.94–103 as data — seven, twenty-two and five entries — and all three are **loaded**: `RulesRepository.Gadgets`, `.Vehicles` and `.Headquarters` answer the files, and `AssetCatalogue` is what `GearCatalogue` is for `gear.json`, flattening the six stock vehicles and the forty-five features into rows a host can offer. They are still held to the book by `Chapter6RulesDataTests`, which is a different claim from being loaded: a loader proves a file parses, not that it says what the book says.

- **They came off `RulesSourceTests`' exemption list in the same commit that put them on `RulesRepository.DataFileNames`**, which is the pairing that guard exists to enforce — removing an exemption without adding a collection, or the reverse, fails one of its two checks. Every browser now fetches them before its first render, which is the decision the consumer slice was the one to make.
- **Two currencies, and neither of them is Hero Points.** p.96: "Every Hero Point you put into this Perk grants you 25 Vehicle Points." p.100: "every Hero Point you put into the Headquarters Perk also grants you 3 Base Points". Every price below those lines is in the second currency, and each file's `header.two_currencies` says so, because a consumer that added one of these figures into a Hero Point total would have made a category error rather than an arithmetic one. **The `unique_vehicle` and `headquarters` entries in `perks.json` are already the Hero Point half** — both `per_unit` at 1 HP a unit — so a character can buy the allowance today and has nowhere to spend it.
- **The two meet again in exactly one place per file, and both are one-way.** Vehicles: Unique Systems is "1 Vehicle Point per Hero Point", so a Power's own HP price is what it costs on a vehicle — the Submersible's sonar is Radar carrying its own Sonar Con, at 2. Headquarters: Mobile costs **0 Base Points** and forces a Unique Vehicle purchase in Hero Points, so the free feature is the expensive one.
- **A Gadget is the exception in the other direction: it pays out.** p.94: "If you make the roll, you gain a number of Hero Points equal to double the item's Complexity to buy Abilities, Talents, and Powers that represent your new Gadget." The character's own budget is never charged, which is why `gadgets.json` has no second-currency header. **The Item Con is on every Gadget by default and credited nothing** — the same rule as gear, stated again on its own page — so `default_con_is_credited: false` is a fact from the book and not a modelling choice.
- **A vehicle and a headquarters are *objects a character owns*, not purchases on a sheet**, so they carry their own characteristics, their own feature list and their own budget. `CharacterSheet.Vehicles` and `.Headquarters` hold them; `CostCalculator.VehiclePointsSpent`, `.BasePointsSpent` and the two `…PointBudget` methods total each in its own currency; the validator reports one over its budget. **None of the second currency is in `TotalCost`** — what `TotalCost` owes is the Perk, and `TotalAssetPerkCost` is that figure.
- **A vehicle's four ranks are bought from zero, and `SelectedGear`'s free baseline is the wrong precedent.** p.96 opens Body, Speed and Control at nothing and gives Weapons no starting value at all. Mundane gear is free because the chapter says it is untracked; a unique vehicle is the opposite of untracked, and a rank nobody paid for would be a Perk that quietly bought more than it says. `Weapons` is `int?`, because the printed tables give an unarmed machine an em dash — no rank at all, which is not the same claim as a rank of nothing.
- **A machine the whole team paid for is not an owned one.** Both p.96 and p.100 let Heroes pool their allowances, and a `CharacterSheet` is one character — so a pooled object is either unrepresentable on one sheet or double-counted across five. `CampaignAssetContribution` is the sheet's whole side of it: an asset id, a name, a kind, and the Hero Points this character put in. Nothing about what the object turned out to be, because five members carrying five copies of one base's feature list is five copies to disagree. **The campaign-side object is a later slice**; this shape exists so it can be summed.
- **The constraints the validator reports, and the two it deliberately does not.** Control may not exceed half the Speed, **half rounded up** like every other half in the book — p.7's glossary settles it "regardless of the context", and p.96 works the rule itself when it calls 4 "half of 7". This was first written as *doubled Control against Speed*, which is a rank tighter at every odd Speed and reported three of p.97's own machines illegal; `CharacterValidator.HalfRoundedUp` is the one halving both this and the Mecha floor go through. Negative Control floors at −3; a Mecha's Might may not be lower than half its Body; and a Gadget needs 6d of Technology with a Complexity no further than it. **A Gadget's Powers, Abilities and Talents are each asked whether the rulebook has them** — the first was, the other two were not, and `GadgetSpend` skips a rank whose Trait id resolves to nothing, so a misspelling bought free ranks and silence at once. **An alternate headquarters that "can't cost more Base Points than your primary" is not enforced**: nothing on the sheet says what an alternate base contains, so it is a caveat rather than a check. **Nor is the Gadget ceiling of half the builder's Intellect per issue**, which is a fact about an issue and not about a sheet — nothing in this engine knows which issue it is looking at.
- **The same Perk recorded twice is a warning, not an error.** A vehicle carries the Hero Points its own Unique Vehicle Perk cost, and a `SelectedPerk` naming that Perk is a second spend somebody wrote down; `TotalCost` charges both, correctly, because both are on the sheet. Nothing about that is illegal — a character may buy the Perk twice over, and points held against a machine nobody has detailed yet is an ordinary thing to do — so `ASSET_PERK_RECORDED_TWICE` says what is likely rather than refusing it. Reported, never repaired.
- **Teamwork is Resolve's twin.** Training Facilities grants a point an issue to everybody sharing the base, and the entry's own `behaves_like` is `resolve` — so `DerivedStatsCalculator.CalculateTeamwork` computes it for anybody and a host that knows it is showing a Villain keeps the same silence it keeps about Resolve. `engine/` and `sheets/` cannot tell the difference and must not learn: `PresentationFlagsTests` is the guard. **Only bases this character owns are counted**, which is a real gap rather than a simplification: the grant is to everybody who shares the base, a shared base belongs to a campaign, and a contribution records Hero Points rather than a feature list. The campaign slice is where that is answered. **And a second base with the feature pays no second point**: p.103 is a condition and a flat grant, and the one axis it multiplies on is characters — the entry's own `granted_to` is "every character who shares the headquarters". Counting bases multiplies on an axis the sentence never mentions. It is a silence the GM may fill either way, not a reading the engine gets to make.
- **The three mundane vehicle tables and the four feature tables are derived out of `data/rulebook/`, not typed a second time.** Seventy-eight rows transcribed again would be a second copy to disagree with the first, so `Chapter6RulesDataTests` parses them out of the corpus and checks the shipped data against that. The pairing of names to figures is this project's reading, recorded as an `interpretation` — see [`rulebook-corpus.md`](rulebook-corpus.md) for why a four-column table arrives as two blocks.
- **p.96's own arithmetic is the strongest check here, and all six reconcile — twice.** `Chapter6RulesDataTests` reconciles them out of the shipped JSON, and `AssetCostTests` reconciles them again through `CostCalculator`'s own arithmetic, which is a different claim: a file can be a perfect transcription and the calculator built on it can still add the wrong columns. Each stock vehicle is priced through the rates on its own page and comes out at the printed total exactly, which checks every rate on the way rather than checking one transcription against another. **The Submersible is the one that had to be read properly to get there**, and it was recorded as a book error first: its feature line prints `Rader (Sonar)`, and taking that as a plain 3 Hero Point Radar gives 15 against a printed 14. The parenthesis names a Con — Ch.2 p.38 prints `CON Sonar (−1)` inside Radar's own entry — so the Power is 2 Hero Points and 2 Vehicle Points, and the total is exact. **A stock total that stops reconciling is a transcription error, not the book's**: the six totals are the only independent check this data has on its own rates.
- **Every `ambiguity` in the three files is a silence in the book, not an open question for a consumer to settle.** They are things the page does not say — whether an advanced feature's +1d stacks, whether an unspent Teamwork point carries over, which of Armor and half Toughness is a rammed character's Body — and a consumer that needs an answer picks one **in its own code, saying so**, rather than writing it back into the data as though the book had printed it. **A half is not one of them**: p.7's glossary settles every halving in the book upward, "regardless of the context", which is why neither the Gadget per-issue ceiling nor the Foe damage capacity records one, and why `CostCalculator` reaches for `Math.Ceiling` everywhere.


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
- **`affects_resolve` is a `bool?` and stays one; the nomination-dependent case is a second field beside it.** `affects_resolve_when_nominated` is a list of Trait ids on the Expertise entry and empty on the other 140 — see the Resolve section above for why the list holds Abilities only. Spelling the carve-out as a string in `affects_resolve` would make every reader of the flag handle a value that is not a flag.
- **`notes` is shown to a player verbatim, so write it for one.** `PowerEditor.razor` prints it under the Power in the browser and `PowerBrowser` prints it in the terminal, neither with any framing — so a sentence explaining a JSON field arrives at somebody choosing a Power for their character. Say what the rule is, not how the file spells it; the derivation goes in this guide. `PowerDataTests.NoPowersNoteNamesAJsonField` scans all 141 for a snake_case identifier, which is the tell the two offenders shared.
- **`powers.json` tracks verification per field, not with a boolean.** Every entry has `verified_fields` (any of `range`, `rank_type`, `cost`, `prerequisite`, `description`, `pros_cons`) and a `source_ref` page reference. `PowerModel.MechanicsVerified` requires the first four; `NeedsReview` is its inverse. The old single flag drifted badly — 27 entries were unflagged while their costs were wrong — so when you change a mechanical field, update `verified_fields` to match what you actually checked.
- **Power `description` values in `data/rules/` stay original text written from the rulebook entry, never rulebook prose.** The rulebook text now lives in `data/rulebook/` instead — see [`rulebook-corpus.md`](rulebook-corpus.md) — and the two must not be merged: `data/rules/` is what the deployed site serves, and the descriptions there are what a player reads while choosing. Descriptions exist so a player can tell what they are choosing and what resists it, and they must agree with the mechanics beside them — `PowerDescriptionTests` fails a rankless power whose description claims per-rank scaling, which is how the original set went wrong on 44 of the 46 rankless powers.
- `powers.json` has **141** entries. Form, Transformation and Super Senses are single Powers in the rulebook but each of their options is bought separately at its own cost, so each option is its own entry. Super Senses is nonetheless *costed* as one Power — see "Super Senses is one Power" above; splitting it is a storage decision, not a rules one.
- **`campaign_cost_min` / `campaign_cost_max` are on the one entry whose printed text hands its price to the table, and they are a transcription of that text.** Ch.2 p.31 prices Immortality at 3 HP flat and then says "In a game where Heroes can die, GMs should charge more for this — somewhere between 6 and 12 Hero Points" — a range where every other entry prints a number. The structured pair exists so `CharacterValidator` can bound a campaign's house price without a figure being written into C#, which would be a price this project had invented and the rules audit could never hold to a page. **The prose stays the source**: `PowerDataTests.ImmortalitysCampaignCostRangeIsWhatItsOwnDescriptionSays` reads the sentence back out of the shipped data and takes the two numbers *it* names, because asserting literals beside it would pass over prose rewritten to say 8. `OnlyImmortalityHandsItsPriceToTheTable` keeps the set at one, with the non-empty control — a second entry gaining the pair would be a house-rule surface the book does not print for it, which is what `available_pros` was one field over.
- `gear_features.json` holds the twelve Ch.6 custom features. `cost_type` is `flat` (with `cost`) or `flat_variable` (with `cost_range`, for the two the rulebook prices at 1 to 2 HP).


## A table's house rules: priced from the sheet, bounded by the data, never read from a campaign

**The house Trait Cap's precedent, exactly, for two more settings** — and PROGRESS item 15 is where
that argument is made. The setting lives on the campaign, is copied onto the character when it
joins, and the engine reads the character. Nothing here resolves a campaign id; nothing here may.

- **`CharacterSheet.ImmortalityCost` is a price, so this engine reads it.** `CostCalculator.PowerCost`
  takes it as a second argument and charges it **only** for an entry that carries a
  `campaign_cost_min`/`max` pair — the Power is asked before the table is — so a campaign that
  re-priced Immortality has not thereby re-priced Armor. `TotalPowersCost` forwards the sheet's,
  which is how the figure reaches a budget: the running total on a screen and the verdict under it
  have to be one number.
- **Every surface that prices a Power for a character passes it, and `HousePriceReadTests` is the
  guard.** `PowerCost(sp)` is the right answer to "what does the rulebook charge" and the wrong
  answer to "what does this character pay", and the two are one optional argument apart, so the
  wrong one compiles silently. That is `TraitCapReadTests`' shape one field over, and for the same
  reason: counting the readers in a doc comment was tried for the cap and the count was wrong.
- **A price outside the range is charged as written and reported** —
  `IMMORTALITY_COST_OUTSIDE_RANGE`, against the bounds in the data. Clamping would be worse than
  usual here: the cost would look right on every screen while the campaign's setting said something
  else. **The rulebook's own 1 HP floor for an unranked Power still applies underneath**, so a table
  setting 0 does not produce a free Power — that is a rule about the Power, not about the table, and
  it is the one place a price is not charged as the number written.
- **`CharacterSheet.CampaignTable` is carried and read by nothing here.** None of its thirteen
  switches is about what a character costs or whether it is legal, which is the whole of what this
  engine decides — they resolve fights, and that is `play/`'s business. It is on the sheet because
  the encounter server is handed characters and never a campaign, so a fight fought with somebody's
  Hero is fought under the book unless the Hero brought its table's rules along. `CampaignTableNamesTests`
  holds its switch list to `play/Encounter/TableRules.cs` by reading that file as source — reflecting
  over it would mean the test project referencing `play/` to enforce that `engine/` does not. **The
  block reaches a fight through `TableRules.From`**, the one seam between the two lists, and the
  same guard reads that conversion too: a switch on both sides and missing from the copy compiles,
  passes the list check, and is a house rule no fight carries. Who decides *which* table a fight is
  under, out of the sheets it was handed, is the encounter server's — see
  [`mcp-and-headless.md`](mcp-and-headless.md).
- **A house price on a character in no campaign is reported in `web/`, not here.** Saying it means
  reading `CharacterSheet.CampaignId`, which `PresentationFlagsTests` bars from all rules code — so
  it is `CampaignJoin.Inspect`'s `IMMORTALITY_COST_WITHOUT_CAMPAIGN`, beside the tier and cap
  mismatches. That is not a workaround: this engine judges a price, and a host judges a membership.
  The guard caught the first version of the check, which had it in the validator.
- **Null and "every switch off" are the same game**, which is what lets a campaign or a character
  stored before any of this existed read back unchanged rather than as a table that has opted out of
  something. `CampaignTable.IsTheBook` is that question, and the browser stores the book as null.


## The engine never touches the filesystem

`RulesRepository` reads through `IRulesSource`, not `File.ReadAllText`. Two implementations ship: `FileSystemRulesSource` (the CLI) and `InMemoryRulesSource` (any host that loads the data itself — a browser has no filesystem). `RulesRepository(string)` and `FromBasePath` still work exactly as before.

**Keep `IRulesSource` synchronous.** Making it async would push `await` through every lazy collection and from there into `CostCalculator` and `CharacterValidator`, turning a pure instantly-callable engine into an async one for no gain. A host that can only load asynchronously does that once at startup and hands over strings.

`RulesRepository.DataFileNames` lists every file a self-loading host must fetch — it cannot glob a directory that isn't there. **Add a new rules file to that list**, or a browser build silently runs on an incomplete rules set; a test enforces it. The one exception is named in that test rather than filtered by a pattern: `meta.json` is provenance. **All four Chapter 6 files came off that list and are the worked example of the pairing**: each exemption and its collection moved in one commit, and the guard fails if only one happens.

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


