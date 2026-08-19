# The rules engine

What the engine computes and how the rules data is shaped. Where this and [`data/rules/`](../data/rules) disagree, the data file and its `source_ref` win — see [`docs/RULEBOOK-COVERAGE.md`](RULEBOOK-COVERAGE.md).


### Power costs

Each power's `cost_type` decides how it is paid for. Only the per-rank types consume purchased ranks.

| `cost_type` | Powers | Cost |
|---|---|---|
| `per_rank` | 71 | `⌈purchasedRanks × cost_per_rank⌉` — `cost_per_rank` is 0.5, 1, 2 or 3 |
| `flat` | 62 | `cost_flat` HP, ranks not purchasable |
| `per_unit` | 3 | `cost_per_unit × units` — Immunity per immunity, Determination per Resolve, Alternate Form per power level |
| `per_rank_variable` | 2 | Energy Absorption (1 or 3), Omni-Power (3 or 5) — `CostVariantKey` picks the rate |
| `flat_variable` | 1 | Stretching (1 / 3 / 6 by reach) |
| `special` | 2 | Boost mirrors the Trait it raises; Summoning is 1 HP per rank per 2d of Minion Threat |

Pro costs and con discounts are then added (cons are stored as **negative** integers, pros positive).

- **Overkill** and **Weak** each reduce the rate by **1 HP per rank**, not by half — the rulebook wording is "reduces a Power's base cost by 1 Hero Point per rank (or changes its base cost from 1 Hero Point per rank to 1 Hero Point per 2 ranks)". Halving is only equivalent for powers already at 1 HP/rank. (The *Brute Option* is the separate rule for applying Overkill to **Might**.)
- The minimum is per rank, not per power, and it is **1 HP per 2 ranks** — the ranked form of the rulebook's "no Power can ever cost less than 1 Hero Point (or 1 Hero Point per 2 ranks) regardless of its Cons". Reading it as 1 HP per *rank* puts the floor exactly at the undiscounted cost of a 1 HP/rank power, which silently voids every Con on it; that was a real bug, and it is why the wording matters. `CharacterValidator` warns when a power has bottomed out.
- Specialty is the one power the rulebook prices at 0 HP.

### Rank types

`rank_type` records what the rulebook prints in a power's Rank field.

| `rank_type` | Powers | Meaning |
|---|---|---|
| `power` | 63 | Has its own rank; ranks are purchased |
| `default` | 46 | **No rank at all** — use Toughness or Willpower (by Source) when another power targets it |
| `baseline` | 27 | Rank derived from another Trait, with purchased ranks stacked on top |
| `special` | 5 | Works in a way its own entry describes |

### Baseline-rank powers

| Relationship | Example | Formula |
|---|---|---|
| `baseline_equal` | Evasion (Agility), Martial Arts (Might) | trait rank + purchased |
| `baseline_half` | Armor (½ Toughness), Leaping (½ Might) | ⌈trait ÷ 2⌉ + purchased |
| `baseline_fixed` | Running | 3 + purchased |
| `baseline_greater_of` | Strike | max(Might, Martial Arts) + purchased |
| `baseline_selected_trait` | Boost, Expertise | rank of a Trait the player nominates + purchased |

Halves round **up** throughout, per the rulebook's global "Whenever we refer to half of an odd number (or half of an odd number of dice), always round up, regardless of the context" rule.

### Perk costs

Flat perks cost their `cost`. Per-unit perks cost `cost_per_unit × units`.

### Derived statistics

| Stat | Formula |
|---|---|
| **Edge** | (Danger Sense **or** Perception) + max(Agility, Intellect) + Lightning Reflexes, floored at Super Speed × 3 |
| **Health** | max( ⌈(Toughness + Might) ÷ 2⌉, ⌈(Toughness + Willpower) ÷ 2⌉ ) |
| **Resolve** | max(0, (TraitCap − highestRelevantRank) × 2) + Determination Resolve + count of Condition/Plot Hook flaws |

Three powers touch Edge, each differently:

- **Danger Sense** *replaces* Perception in the sum — "Use this Power instead of Perception when making rolls to detect danger and when determining your Edge". It is not added on top.
- **Lightning Reflexes** adds a flat **+6**. It has no rank, so nothing scales.
- **Super Speed** sets Edge to its rank × 3; treated as a floor so it never lowers an already-higher Edge.

Resolve's base term is the rulebook's Resolve table (Trait Cap → 0, Cap−1d → 2, Cap−2d → 4) expressed arithmetically. **Determination** has no rank: every 5 HP spent on it buys 1 extra starting Resolve.

*Highest relevant rank* is the maximum across all ability ranks and the effective ranks of powers whose `affects_resolve` is true. Talents are excluded, as are Movement and Sensory powers by default — `PowerModel.AffectsResolve` overrides that per power. This correctly excludes all eleven powers the rulebook names as Resolve-exempt.

---

## Tiers

| Tier | Hero Points | Trait cap |
|---|---|---|
| Street Level | 75 | 8d |
| Low Level | 100 | 10d |
| Standard | 125 | 12d |
| High Level | 150 | 16d |
| Legendary | 175 | 20d |
| Iconic | 200 | 24d |

Iconic is stated as "200+" in the rulebook with no upper bound, so the data records a flat 200 and the entry carries a note saying why. It is **not** flagged for review — the verification pass settled that the open end is GM discretion rather than a value nobody has checked. `CharacterValidator` says so with an `ICONIC_TIER_OPEN_BUDGET` notice instead.

---

## Wizard Steps

| # | Step | What happens |
|---|---|---|
| 1 | **Choose Tier** | Pick power level (Street Level → Iconic), optionally apply a starting package |
| 2 | **Buy Characteristics** | Ability and talent ranks; browse/search powers with pros & cons; flaws and perks |
| 3 | **Choose Gear** | Free-text mundane gear, correctly free per Ch.6, plus optional custom features at 1–2 HP |
| 4 | **Derived Stats** | Edge, Health and Resolve calculated and displayed |
| 5 | **Finishing Touches** | Name, appearance, motivation, quote, connections |
| 6 | **GM Review** | Full sheet display, validation results, export |

Both front ends run these same six steps against the same engine. The terminal wizard offers **← Back** on steps 1–5 and treats GM Review as the terminus, writing its exports to `output/`; the browser keeps every step reachable from the step bar and hands the same two documents to a download.

---

## Data Conventions

- All JSON keys are `snake_case`, matched by `JsonNamingPolicy.SnakeCaseLower`
- Powers with `cost_type: "special"` carry no numeric cost — `CostCalculator.PerRankRate` must handle each by id or throw
- `powers.json` records verification **per field** rather than with a single boolean; the other rules files use `"needs_review"`, and nothing is currently flagged

### Verification coverage

Every entry in every rules file has been checked against the rulebook — chapters 1–2 throughout, plus Ch.6 for the gear features and Ch.7 for the three toxin Pros and Cons — and **the test suite is what keeps it that way** — `CanonicalPowers.cs` holds the Range, Rank and Cost printed for all 141 Powers, and `RulesDataTests` holds the tier, ability, talent, pro, con, perk and flaw values. A data edit that contradicts the book fails a test.

On top of that, the **20 pre-built Heroes from Chapter 8** are transcribed and rebuilt through the engine. They are finished, playable Standard-tier characters the authors published, so they check the rules as *applied* rather than as transcribed. The engine reproduces all sixty of their printed Edge, Health and Resolve values, and rebuilds **16 of the 20 to exactly their 125 Hero Point budget**; the other four are 1 HP out, each for a reason recorded in [PROGRESS.md](../PROGRESS.md).

They have earned their keep twice over, catching two cost bugs that unit tests had missed — the minimum-cost floor, and a starting package being charged on top of the ranks it grants. Three of them also pin down rules that are easy to read wrongly:

| Hero | What it proves |
|---|---|
| Black Dragon (Edge 20) | Danger Sense 10d + Agility 10d. Adding Danger Sense to Perception would give 26 — it **replaces** Perception |
| Herald / Scáthach (Edge 22) | Danger Sense 8d + Agility 8d + Lightning Reflexes 6 — confirms both the replacement and the flat +6 |
| Alabama Slammer (Edge 36) | Super Speed 12d × 3, the only printed sheet that exercises that rule |

| File | Entries | Verified against |
|---|---|---|
| `powers.json` | 141 | Ch.2 Powers, pp.21–48 — range, rank type, cost, baseline and description |
| `pros.json` | 23 | Ch.2 Pros and Cons, pp.48–53 |
| `cons.json` | 28 | Ch.2 Pros and Cons, pp.48–54 |
| `perks.json` | 13 | Ch.2 Perks, pp.54–55 |
| `flaws.json` | 53 | Ch.2 Flaws, pp.55–60 |
| `abilities.json` | 6 | Ch.2, p.17 |
| `talents.json` | 12 | Ch.2, p.18 |
| `tiers.json` | 6 | Ch.2 Power Levels, p.15 |
| `gear_features.json` | 12 | Ch.6 Equipment, p.93 — custom gear features |
| `sources.json` | 6 | Ch.2 Sources, p.16 — default rank per Source |

A single `needs_review` boolean could not tell a verified cost from a verified description, and it drifted badly: 27 power entries were unflagged while their costs were wrong. `powers.json` therefore carries `verified_fields` plus a `source_ref` page reference on every entry:

```json
"verified_fields": ["range", "rank_type", "cost", "prerequisite", "description"],
"source_ref": "Ultimate Edition, Ch.2 Powers, p.21"
```

`PowerModel.MechanicsVerified` is true when all four mechanical fields are listed; `NeedsReview` is its inverse. `DescriptionVerified` is tracked separately because descriptions affect no calculation.

Questions the verification pass settled:

- **Lightning Reflexes** — flat 3 HP, no rank, and the Edge bonus **is** a flat +6
- **Determination** — no rank; **5 HP buys 1 Resolve**
- **Iconic tier** — "The 200 Hero Points listed for Iconic Heroes is a bare minimum"; there is no upper bound in the book, so this is GM discretion, not missing data
- **Overkill / Weak** — a −1 HP per rank rate reduction, not a halving
- **Compound entries** — the rulebook prints Form, Transformation and Super Senses as single Powers but prices each of their options separately, so each option is its own entry. Five Flaws and several Pros and Cons pair two options under one heading and are likewise stored separately

### A note on descriptions

Power descriptions are **original text written from the rulebook entry**, not rulebook prose. They exist so a player can tell what they are choosing and what resists it. No rulebook wording is reproduced here — only structured metadata and this project's own explanations. For exact rules wording, read the page named in `source_ref`.

They are held to the mechanics they sit beside: `PowerDescriptionTests` fails a rankless Power whose description claims anything scales per rank, which is how the original set went wrong on 44 of the 46 rankless Powers.

Data coverage is everything character creation needs: chapters 1–2 (Basics and Characters) in full, plus Ch.6's twelve custom gear features and Ch.7's three toxin Pros and Cons. Chapters 3, 4, 5 and the rest of 7 are play rules; Ch.9 builds Villains by the Hero rules, which is why the mode is presentation only. Ch.8 is broader than it looks — NPCs and animals from p.111, Extras from p.120, and the twenty pre-built Heroes and Villains from p.126, of which only the last are transcribed, in the test suite where they verify the engine. The one genuine gap is Ch.6's vehicles and headquarters.

Which pages have actually been read, and which are deliberately skipped, is tracked page by page in [docs/RULEBOOK-COVERAGE.md](RULEBOOK-COVERAGE.md); open work is in [PROGRESS.md](../PROGRESS.md).

---

