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

# A whole roster in one process, and one report covering all of it
dotnet run -- build --from-dir campaign/ --no-export --traits-above 6

# --no-build when anything else may be building this working tree
dotnet run --no-build -- build --from character.json --no-export
```

**Use `dotnet run --no-build` whenever you are not the only thing in this checkout.** Several
`dotnet run` commands at once collide on the compiler — one of them fails on a file another is
writing, and the failure has nothing to do with the character. `--no-build` skips the build and
runs the last one, which is what makes several agents driving one working tree safe. Build once
yourself first if you have just changed the code.

**This skill is for working inside this repository.** There is a second way in for somebody who
has not checked it out: `mcp/` is an MCP server over the same engine, and its own document —
`mcp/QUESTION-POLICY.md`, served as the `creation_guide` tool — covers which questions to ask
somebody describing a character out loud. The rules below are the same either way, because both
call the same `CostCalculator` and `CharacterValidator`.

| | |
|---|---|
| `--from <file>` | The character. `-` reads it from standard input, and may be given at most once. **Repeatable** — pass it once per character to check a roster in one process. |
| `--from-dir <dir>` | Every `*.json` directly in `<dir>`, in name order. Not recursive, so an `output/` of previous exports underneath it is not swept up. Combines with `--from`. |
| `--out <dir>` | Where the `.txt` and `.json` sheets go. A relative path is relative to where you are; with no `--out` they go to `output/` beside the program instead. The report gives absolute paths either way. |
| `--no-export` | Cost and validate only. Use this while iterating. |
| `--overwrite` | Name each export after its character alone, with no timestamp, replacing any file of that name. Without it every run *adds* a pair — right for one export, wrong for a roster re-checked after an edit. Two characters whose names reduce to the same file name come back with an `EXPORT_NAME_COLLISION` warning rather than one writing over the other. |
| `--traits-above <n>` | Add to the roster section every Ability, Talent and Power whose effective rank is above `n`, and the Trait a Power's baseline is derived from — so you can see whether the Power justifies the rank. |
| `--trait-cap <n>` | Build **every** character in the run to a house Trait Cap of `n` rather than the tier's, overriding the `TraitCapRank` field on the file. A campaign may cap tighter than any tier does — Pinnacle City caps a non-superhuman at 6d. **It moves Resolve**, which is measured from the cap; the report carries the cap in force as `trait_cap` and the tier's as `tier_trait_cap`. A value that is not a whole number is exit 2; one that makes no sense (0d, or above the tier's) is a finding on the character, as it is when the file carries it. |
| `--help` | The same table, from the program. |
| `--no-build` | Not this command's flag — it goes before the `--`, as `dotnet run --no-build -- build …`. Skips the compile, which is what stops several agents in one working tree colliding. |

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
  "trait_cap": 12,                // the cap in force — the house one if there is one
  "tier_trait_cap": 12,           // the tier's own, so you can see when they differ
  "derived": { "edge": 14, "health": 8, "resolve": 6 },
  "issues": [ … ],
  "exports": { "text": "…absolute path", "json": "…absolute path" }
}
```

`exports` is null when you passed `--no-export` — and also when the sheets could not be
written, which comes with an `EXPORTS_NOT_WRITTEN` warning. Check it before telling anyone
their sheet is ready.

### More than one character

**One character reports exactly the document above.** Give the command two or more — repeated
`--from`, or `--from-dir` — and standard output is still exactly one JSON document, wrapping
them:

```jsonc
{
  "ok": false,                  // true only when every character is legal
  "exit_code": 2,               // the highest of theirs: 2 beats 1 beats 0
  "characters": [
    { /* the report above, plus "source": the file it was read from */ }
  ],
  "roster": {
    "character_count": 3,
    "read_count": 2,            // a file that could not be read is in characters, not here
    "traits_above": [           // only with --traits-above
      { "source": "…", "name": "…", "traits": [
        { "kind": "ability", "id": "might", "rank": 9 },
        { "kind": "power", "id": "armor", "rank": 9, "purchased_ranks": 5,
          "baseline_rank": 4, "baseline_relationship": "baseline_half",
          "baseline_traits": [ "toughness" ], "affects_resolve": true }
      ] }
    ],
    "spending": [
      { "source": "…", "name": "…",
        "totals": { "package": 40, "abilities": 12, "talents": 2,
                    "powers": 4, "perks": 2, "gear": 0, "total": 60 },
        "perks": [ { "id": "contacts", "units": 2, "cost": 2 } ] }
    ],
    "perks_by_id": [ { "id": "contacts", "characters": 2, "units": 5 } ]
  }
}
```

**A file that cannot be read is one exit-2 report inside `characters`, not the end of the
run** — a typo in one file name does not cost you the other twenty-seven answers. Read
`exit_code` to know that something needs attention and each character's own to know which.

`perks_by_id` is the "which sheets are padded with Contacts" question: Contacts is the
cheapest dial on the sheet, and a roster leaning on it is visible here and in no category
total.

## Build it at full strength first

**The default is the strongest legal character the concept allows**, and the person trades
*down* from there if they want to. Trading down is a decision they make out loud; trading up is
a correction they have to notice they need, and a sheet quietly six points weaker than it could
be looks exactly like a sheet that is not. The instinct to build tastefully — to spend 118 of
125 because the concept "felt like" a modest character — makes a decision nobody asked for and
hides it behind prose about the character being unassuming.

Concretely, since the rulebook has no single power axis:

- **`remaining` at zero is the target**, not a ceiling to stay politely under.
- **Take a package.** It is a discount, not a flavour choice.
- **Take the headline Trait to the Trait Cap** wherever the concept supports it. Three ranks
  under the cap is playing a lower tier than the one that was chosen.
- **Prefer a Power that arrives with a baseline rank** — 27 derive free ranks from a Trait the
  character is buying anyway, so the same points buy a higher effective rank.
- **Use the Cons the character would genuinely suffer**, and only those. Cons that never cost
  them anything in play make a cheaper sheet, not a stronger character.
- **The derived-stat levers are worth more than a rank each**: Danger Sense replaces Perception
  in Edge, Lightning Reflexes is a flat +6, Super Speed sets Edge to rank × 3.

**One trade has no right answer and must be said out loud.** Resolve comes off the *gap*
between the Trait Cap and the highest relevant rank, so taking a headline Trait to the cap
drives Resolve towards zero. Build the specialist, say what it cost in one sentence, and let
them take the generalist instead if that is what they wanted.

**Expertise is the cheap way past that trade, except in a fight.** At 1 HP per 2 ranks on top of a
Trait's rank it is the cheapest high number on the sheet, and Ch.5 p.83 exempts it from Resolve —
*"except for combat skills"*. So an Expertise nominated to **Might, Agility, Toughness or
Willpower** — the four Abilities Ch.4 p.75's Attack and Defense table uses to attack or defend —
counts at its full rank and drives Resolve down exactly as that Ability would; one nominated to a
Talent, or to Intellect or Perception, costs nothing. A 6d Hero with Expertise (Agility: Firearms)
at a 12d cap opens on **0 base Resolve rather than 12**, before Determination and any
Condition or Plot Hook Flaws are added. Read the figure from the report rather than working it out,
and do not sell a combat Expertise as free Resolve.

**Nominate an Ability or a Talent and nothing else.** Ch.2 p.28: *"Your specialization must fall
under one of your Abilities or Talents"*. An Expertise whose `BaselineTraitId` names a Power is
refused with `EXPERTISE_NOMINATION_NOT_A_TRAIT` — Boost is the one Power that may be nominated to
another Power. **The book never defines a combat skill**, so which nominations count is this
repository's reading of p.83, and it errs towards counting: the specialisation itself is free text,
so Expertise (Agility: Acrobatics) counts here where a GM probably would not count it. Erring that
way means *less* Resolve, so it never flatters a Hero — and p.83 gives the GM the final say. Say so
if somebody's build turns on it.

**And the cap may not be the tier's.** A campaign can impose a tighter one — Pinnacle City caps a
non-superhuman NPC at 6d where the Standard tier allows 12d — and that is `TraitCapRank` on the
character file, or `--trait-cap <n>` for a whole run. **It substitutes for the tier's rather than
merely gating validation**, so it moves Resolve: at Standard a 4d character is paid `(12−4)×2 = 16`,
and under a 6d house cap the same character is paid `(6−4)×2 = 4`. Read `trait_cap` from the report
rather than the tier's `trait_cap_rank`, build to *that* number, and say the ceiling out loud when
`tier_trait_cap` differs from it — the trade above is a different trade under a tighter cap, and it
lowers the Resolve ceiling too (24 at 12d, 12 at 6d). A cap **above** the tier's is
`TRAIT_CAP_ABOVE_TIER` and a cap below 1d is `TRAIT_CAP_BELOW_MINIMUM`; both are errors, and both
figures are still what the report was computed from, because this tool reports and never repairs.

This licenses none of the following: overruling a weakness they stated, dropping a Flaw or Con
they asked for, going over budget, or making the character cheaper rather than stronger. The
goal is the strongest sheet *at* the budget.

### A Villain has no Resolve, and it changes the build

Ch.2 says it twice — *"Only Heroes have Resolve"* — and Ch.5 gives the GM **Adversity** instead,
spendable "on behalf of any NPC whether they're Villains, Foes, Minions, or Extras". The engine
builds Heroes, so the report carries a Resolve figure for a Villain anyway. **It is noise; do
not quote it.** Three consequences:

- **Never buy Determination on a Villain.** It is Hero Points spent on Resolve, and the Resolve
  is worth nothing — the one purchase that goes from good to dead on this distinction alone.
- **Plot Hook and Condition Flaws grant nothing mechanical**, since what they grant is starting
  Resolve. Take one to three anyway; creation requires it and they cost nothing. Choose for the
  story, not the number.
- **The Trait Cap trade above does not apply.** Resolve is what a *Hero* pays for a rank at the
  cap. A Villain pays nothing, so cap every Trait the concept supports. A **house** cap is the
  exception worth naming: on a Villain it is doing validation work and nothing else — build to it
  because the table said so, and ignore the Resolve half of it as you ignore the rest.

`IsVillain` is a real field and is **presentation only** — it picks the sheet's palette, and no
rules code reads it. Flip it and every figure in the report is identical. Ch.9 builds Villains
by exactly the Hero rules otherwise, and `HP_BUDGET_EXCEEDED` above the tier's points is the
GM's call rather than a Villain exemption: a GM may overspend on a Hero too.

A **Foe** is not a Villain: Ch.9 says the only mechanical difference is that Foes have about
half a Villain's Health. This tool builds Heroes and Villains, and does not halve anything.

### A Villain's Flaws are the players' handles

For a Hero a Flaw is a bargain — a drawback bought with the Resolve it pays out. A Villain gets
no Resolve, so only the drawback is left, which makes the Flaw slots the one place on the sheet
where the GM decides **how this character can be beaten**. Choose for that and nothing else.

The test is whether the Flaw bites without a Resolve payout to notice it. Absentminded, Clumsy,
Quirk, Decorum, Notoriety, Creepy, Unusual Looks, Broke and Illiterate do not — pure flavour on a
Villain. Prefer one from each of these three, so the party can win by fighting, outthinking or
exposing them rather than only the way the GM imagined:

- **In the fight.** **Vulnerability** is the strongest in the book: active *and* passive defence
  halved against one attack, effect or weapon, printed as a rule rather than a Resolve trigger.
  **Severe Reaction** and **Severe Requirement** are impossible to resist by their own text.
  **Power Limits** is a Con the GM controls and is the whole answer on a one-Power Villain.
  **Finite Power**, **Light Sensitive**, **Night Blind**, **Impaired Sense**.
- **In their behaviour.** **Code**, **Severe Compulsion**, **Frenzy**, **Hidden Agenda** — the
  party baits rather than beats. **Flashbacks/Guilt** carries printed numbers: helpless for a
  page, or −2d for three.
- **Outside the fight.** **Secret**, **Secret Identity**, **Relationship**, **Wanted**,
  **Obligation** — won by investigation, exposure or leverage.

**Never spend a slot on something the fiction carries free.** A rival organisation is an Enemy on
the sheet and a plot in the campaign, and only the second is load-bearing. Creation allows one to
three Flaws, so a slot spent on colour is a handle the party does not get.

## The loop

1. **Propose.** Write the character file at full strength. Guess ranks; do not compute costs.
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
  "IsVillain": false,                    // presentation only — the sheet's palette, never a cost
  "SelectedTierId": "standard",          // required in practice: it sets the budget and cap
  "TraitCapRank": null,                  // a house cap tighter than the tier's; null = the tier's
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
  the rank of a Trait *you* nominate in `BaselineTraitId` (Boost, Expertise). **The two differ
  in what may be nominated**: Boost raises "one specific Ability, Talent, or Power" (Ch.2 p.24)
  and that nomination also sets its cost per rank, while an Expertise's specialisation "must
  fall under one of your Abilities or Talents" (Ch.2 p.28) — a Power there is refused.
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
  Self-range Power comes back as `PRO_NOT_APPLICABLE` rather than passing.

  **Do not read those two fields as the whole answer, in either direction.** A Power's own
  entry can name a generic option its Range would otherwise forbid, and then that option is
  legal on it — Force Field is Self range and the rulebook says to apply the Zone Pro to it,
  which is how T-Kay is printed. `power_detail` is the authority: it lists what a Power may
  actually take, and a row allowed this way carries `allowed_by_this_power_text` quoting the
  sentence, plus the grades that Power may pick. Reading `applies_to_ranges` off the generic
  catalogue instead will make you drop a Pro the rulebook prints on a published Hero.

- **Do not read the Iconic tier's warning as permission.** `ICONIC_TIER_OPEN_BUDGET` says its
  200 points are a minimum, and `HP_BUDGET_EXCEEDED` still errors above them. Going over is the
  GM's call to make, not the tool's to certify.
