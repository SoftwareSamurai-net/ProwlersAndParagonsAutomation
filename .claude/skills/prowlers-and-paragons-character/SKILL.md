---
name: prowlers-and-paragons-character
description: Build a legal, costed Prowlers & Paragons Ultimate Edition character from a description ("a washed-up boxer who punches through time"), by proposing one and letting this repository's rules engine price and validate it. Use when asked to create, cost, check or repair a P&P Hero or Villain, or when handed a character JSON to validate.
---

# Building a Prowlers & Paragons character

You propose. **The engine decides.**

Nothing you write here may state a Hero Point cost, a derived stat, or whether a character
is legal. You choose a concept, pick Traits and Powers that express it, and hand the file to
`build`; `CostCalculator` and `CharacterValidator` answer, and you adjust until the answer is
zero. Invert that ordering and this is a random number generator with good prose — the whole
reason it is worth doing is that the engine is trustworthy: 141 Powers priced against the
book, and sixteen of the twenty published Heroes rebuilt to their exact 125-point budget.

You will get the arithmetic wrong if you try it. Do not try it.

## The command

```bash
dotnet run -- build --from character.json
```

**This skill is for working inside this repository.** There is a second way in for somebody who
has not checked it out: `mcp/` is an MCP server over the same engine, and its own document —
`mcp/QUESTION-POLICY.md`, served as the `creation_guide` tool — covers which questions to ask
somebody describing a character out loud. The rules below are the same either way, because both
call the same `CostCalculator` and `CharacterValidator`.

| | |
|---|---|
| `--from <file>` | The character. `-` reads it from standard input. |
| `--out <dir>` | Where the `.txt` and `.json` sheets go. A relative path is relative to where you are; with no `--out` they go to `output/` beside the program instead. The report gives absolute paths either way. |
| `--no-export` | Cost and validate only. Use this while iterating. |
| `--help` | The same table, from the program. |

**Exit 0** the character is legal — there may still be warnings.
**Exit 1** it breaks a rule; every issue is in the report.
**Exit 2** the file could not be read, or the arguments made no sense.

Standard output is one JSON report for each of those three. Anything about the run itself goes
to standard error, so you can parse standard output whole. `--help` is the one exception: it
prints the usage text, on standard output, and exits 0.

The report:

```jsonc
{
  "ok": false,                  // no errors — the same thing exit 0 means
  "exit_code": 1,
  "character": { "name": "…", "tier": "standard", "package": "hero_package" },
  "hero_points": { "spent": 131, "budget": 125, "remaining": -6 },
  "trait_cap": 12,
  "derived": { "edge": 14, "health": 8, "resolve": 6 },
  "issues": [ … ],
  "exports": { "text": "…absolute path", "json": "…absolute path" }
}
```

`exports` is null when you passed `--no-export` — and also when the sheets could not be
written, which comes with an `EXPORTS_NOT_WRITTEN` warning. Check it before telling anyone
their sheet is ready.

## The loop

1. **Propose.** Write the character file from the concept. Guess ranks; do not compute costs.
2. **Submit.** `dotnet run -- build --from character.json --no-export`
3. **Read the report.** Exit 0 and you are done — export the sheets by re-running without
   `--no-export`.
4. **Repair.** Each issue carries the facts to act on. Change the file. Go to 2.

Three or four passes is normal. The first one is usually over budget, because a concept
always wants more than 125 points.

### Repairing from an issue

Every issue has `severity`, `code` and `message`. Errors and warnings alike carry as much of
this as applies, and this is what you act on — **do not parse the message**:

| field | what it is |
|---|---|
| `subject_kind` | `character`, `tier`, `ability`, `talent`, `power`, `gear`, `gear_feature`, `flaw` |
| `subject_id` | the id to change — or the item's name, for gear |
| `owner_id` | the piece of gear a feature sits on |
| `value` | what the character has |
| `limit` | what the rules allow, in the same unit |
| `options` | the values the fix must be chosen from, where the rules fix the set |

So `TRAIT_ABOVE_CAP` with `subject_kind: "ability"`, `subject_id: "intellect"`, `value: 14`,
`limit: 12` means: set `AbilityRanks.intellect` to 12 or less. `POWER_VARIANT_NOT_CHOSEN`
with `options: ["narrow", "broad"]` means: put one of those two in that Power's
`CostVariantKey`. An issue with `options` is always repaired by choosing from them — never by
inventing a value that seems reasonable.

`hero_points.remaining` is negative exactly when you are over budget, and its size tells you
how much to give up. Prefer giving up ranks on Traits the concept does not lean on, rather
than deleting a Power that is the concept.

**A `null` under `hero_points` or `derived` means the engine could not answer**, not zero.
Something in the character has no cost yet — a variable-cost Power with no variant, an
invented id. Fix the errors and the figures appear.

## The file

The shape is the character's **inputs**. It is not the JSON export, which is a report: that
document holds costs, derived stats and findings, and feeding it back would mean rebuilding a
character out of its own conclusions.

Field names are the ones below. Case is forgiven (`selectedTierId` works); underscores are
not, and **a field name that is not on this list is refused rather than ignored** — so a typo
is reported instead of silently emptying the section it was meant to fill.

Every field is optional to the *reader*, and a character with only some of them is not legal:
the smallest legal one is a tier, one flaw, **and all six Abilities and all twelve Talents**,
because no Trait can be lower than 1d. A tier and a flaw alone comes back with eighteen
`TRAIT_BELOW_MINIMUM` errors.

```jsonc
{
  "Name": "Chrono Jab",
  "SelectedTierId": "standard",          // required in practice: it sets the budget and cap
  "SelectedPackageId": "hero_package",   // optional; omit for a character who took none

  // ALL SIX Abilities and ALL TWELVE Talents, always. See "What trips up a first draft".
  "AbilityRanks": {
    "might": 8, "agility": 6, "intellect": 3,
    "perception": 3, "toughness": 3, "willpower": 3
  },
  "TalentRanks": {
    "streetwise": 4, "academics": 2, "charm": 2, "command": 2, "covert": 2,
    "investigation": 2, "medicine": 2, "professional": 2, "science": 2,
    "survival": 2, "technology": 2, "vehicles": 2
  },

  // Pros and Cons applied to an Ability rather than a Power. The Brute Option is Overkill
  // on Might; the published Stronghold buys four Abilities through his armour with Item.
  "AbilityModifiers": { "might": [ { "Id": "overkill" } ] },

  // Only Traits whose Source is NOT the default. Abilities default to Innate and Talents to
  // Trained, and recording a Trait on its own default prints nothing, so leave it out.
  "AbilitySources": { "might": "tech" },
  "TalentSources":  { "streetwise": "tech" },

  "SelectedPowers": [
    {
      "PowerId": "armor",
      "PurchasedRanks": 4,               // ranks bought ON TOP of any free baseline
      "Pros": [ { "Id": "subtle" } ],
      "Cons": [ { "Id": "burnout" } ],
      "SourceId": "tech",                // one of the six; a Power has no default
      "CostVariantKey": null,            // required where cost_type is *_variable
      "Units": 1,                        // for a per_unit Power
      "BaselineTraitId": null            // required for Boost and Expertise
    }
  ],

  "Perks": [ { "PerkId": "contacts", "Units": 2, "NarrativeDetail": "dockworkers" } ],
  "Flaws": [ { "FlawId": "code", "NarrativeDetail": "never hits first" } ],

  "Gear": [
    {
      "Name": "Jo Sticks",
      "Features": [ { "FeatureId": "accurate", "GradeKey": "accurate" } ],
      "Pros": [], "Cons": [],
      "PairedUnderTwoFisted": false      // true only if the character has the Two-Fisted Power
    }
  ],

  "Appearance": "", "Motivation": "", "Quote": "",
  "Connections": [ "His old trainer" ]
}
```

A Pro or Con is `{ "Id": "...", "VariantKey": null, "Units": null }`.

**`VariantKey` is required for a Pro or Con priced by grade, and omitting it is an error, not
a default.** Those are the ones with a `cost_modifier_range` in `pros.json` / `cons.json` —
Charges, Area/Burst, Limited and the rest. The keys of that object are the values it accepts,
and the report hands them to you in `options`. `Units` is for a Power-specific Pro or Con that
scales.

## Finding ids

Ids are in `data/rules/`, and they are the only names the file accepts. Read the file rather
than guessing a snake_case id from a printed name — several do not match.

| file | holds |
|---|---|
| `tiers.json` | the six tiers, with `hero_points` and `trait_cap_rank` |
| `creation_rules.json` | the three packages, the flaw limits |
| `abilities.json`, `talents.json` | the six Abilities and twelve Talents |
| `powers.json` | 141 Powers: `range`, `rank_type`, `max_rank`, `cost_type`, `prerequisite`, and their own `power_pros` / `power_cons` |
| `pros.json`, `cons.json` | the generic ones, each stating what it may be applied to |
| `perks.json`, `flaws.json`, `gear_features.json`, `sources.json` | the rest |

## What trips up a first draft

Most of these are things the engine will tell you. They are here so the first pass is closer.

- **A tier is required.** Without one there is no budget and no Trait Cap, nothing else can be
  checked, and the character can never exit 0. A tier id the rules do not have is the same
  refusal, not a shrug.
- **1 to 3 flaws at creation.** None is an error, and so is a fourth — the rulebook prices a
  fourth at 3 HP during play, but this tool refuses it at creation rather than charging for it.
- **Write out all six Abilities and all twelve Talents, every time.** Ch.2 says it once for each:
  *"No Ability can have a rank lower than 1d"*, *"No Talent can have a rank lower than 1d"*. A
  character has all eighteen — 0d is not a low rank, it is a Trait nobody can be without — so a
  missing one is `TRAIT_BELOW_MINIMUM` and the character is refused. Ordinary people have 2d in
  each, which is the sensible filler for the ones your concept does not care about.
- **The minimum costs Hero Points.** Without a package you pay for all eighteen at 1d, which is
  18 HP before anything interesting. That is why the Civilian Package is 35 HP for 2d in all
  eighteen — 36 points of ranks — and why the rulebook calls a package a small discount.
- **A package grants its ranks and they are a floor, not an offer.** With
  `"SelectedPackageId": "superhero_package"` every Ability and Talent is 3d, and writing one
  *below* 3d is `TRAIT_BELOW_PACKAGE` — "cannot lower any of these below the package rank". Still
  write all eighteen out; ranks the package covers simply cost nothing.
- **The Trait Cap applies to Powers too**, at their *effective* rank — baseline plus
  purchased. Standard tier is 12d.
- **A rankless Power takes no ranks.** `max_rank: 0` in `powers.json` means it is priced as a
  whole; buying ranks for it is an error. Invisibility and Lightning Reflexes are both like
  this, and both look rankable.
- **27 Powers start from another Trait**, and `prerequisite.relationship` says how. All five:
  `baseline_equal` takes that Trait's whole rank; `baseline_half` half of it, rounded up;
  `baseline_fixed` a rank printed in the entry (Running, 3d); `baseline_greater_of` the higher
  of an Ability and some Powers (Strike, from Might or Martial Arts); `baseline_selected_trait`
  the rank of a Trait *you* nominate in `BaselineTraitId` (Boost, Expertise — and for Boost
  that nomination also sets the cost per rank).
  `PurchasedRanks` stacks on top, so 4 purchased ranks of a `baseline_equal` Power on a 9d
  Ability is 13d — over the cap before you have noticed.
- **`GradeKey` is required for the two gear features priced by grade** (`cost_type` is
  `flat_variable`) and must be left out for the ten flat ones.
- **`Units` is only for a `per_unit` Power** — Immunity, Determination, Alternate Form. Check
  `cost_type` in `powers.json`; leave it at 1 otherwise.
- **A package pays for the ranks it grants**, so it is a discount rather than a surcharge. The
  Superhero Package is 50 HP for what costs 54 bought separately.
- **Mundane gear is free and untracked.** Give a character whatever kit suits them. Only
  custom features and Pros and Cons on an item cost anything.
- **A Source on a Power is free, and its absence is only a warning** — but a sheet groups
  Powers under Source headings, so a character with none prints an unsorted list. Set them.
- Halves always round **up**.

## Two things not to do

- **Do not repair a character by clamping it silently.** If the concept does not fit the
  budget, say which part you gave up and why. A player whose idea was too expensive should
  find that out.
- **Do not add rules the engine does not enforce.** Some Pros and Cons state a constraint the
  rulebook does not print per Power — "applies to attack Powers", say. Those travel as a
  caveat and are the GM's call, deliberately. Mention one if it is clearly being stretched;
  do not refuse the character over it.

  **The constraints the rulebook does print are enforced, though.** An option's
  `applies_to_ranges` and `applies_to_rank_types` are checked on submit, so the Ranged Pro on a
  Self-range Power comes back as `PRO_NOT_APPLICABLE` rather than passing. Read those two
  fields when choosing an option and you will not meet it.

- **Do not read the Iconic tier's warning as permission.** `ICONIC_TIER_OPEN_BUDGET` says its
  200 points are a minimum, and `HP_BUDGET_EXCEEDED` still errors above them. Going over is the
  GM's call to make, not the tool's to certify.
