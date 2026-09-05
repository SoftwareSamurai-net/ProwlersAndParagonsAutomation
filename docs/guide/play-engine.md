# The play engine

Read before touching `play/` — the second engine, which resolves an action where `engine/` costs and validates a character.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

Read [`play-rules.md`](play-rules.md) too: it is the store this engine reads, and every convention it records — `interpretation` against `ambiguity`, `table_setting`, `transcribed_here: false` — decides what this engine is allowed to do with a field.

---

## What `play/` is, and what it is not

`play/` is a project beside `engine/`. It references `engine/` and **nothing references it back** — not `engine/`, not `sheets/`, and, until slice (e) lands its hosts, nothing else in the solution but the test project.

| It is | It is not |
|---|---|
| the authority on **what happens** when somebody attacks, moves, grapples or spends Resolve | the authority on what anything **costs** — `CostCalculator` is, and `play/` may not name it |
| a reader of `data/rules/play/` | a reader of `data/rules/*.json`; it holds no opinion about Powers, Pros or Hero Points |
| a pure function over an immutable state | a simulation loop that mutates a `CharacterSheet` — it cannot reach one after `CombatantFactory` |
| told what kind of combatant it is looking at | allowed to read the character's palette flag to work it out |

`CLAUDE.md`'s settled list is where this comes from, in one line: **play rules do not go into `engine/`, which is the authority on cost and validity and knows nothing about resolving an action.** A combat simulator is a *second* engine beside it.

**Four guards hold that, and three of them were already here.** `play/` is on `AccountsContractTests`'s and `PresentationFlagsTests`'s list of rules projects, so it inherits *no account*, *no filesystem*, *no network* and *no presentation flag*. `PlayContractTests` adds the two that are new: `engine/` and `sheets/` never name `play/` in either spelling, and `play/` names neither `CostCalculator` nor `CharacterValidator`. Every one carries a positive control that the names it scans for are real, because a scan for names that no longer exist passes on everything.

## Reading the rules: `PlayRulesRepository`

Same shape as `RulesRepository` and none of its data. Five files, lazy, snake_case, read through the same **synchronous** `IRulesSource` — a host that can only load asynchronously does it once and hands over strings, exactly as the browser already does for the character rules.

**`PlayRulesRepository.DataFileNames` and `RulesRepository.DataFileNames` never meet, and a test says so.** That second list is what a browser fetches at boot; a play file on it is a public URL nobody asked for, which is the same exposure `PlayPayloadTests` keeps the csproj globs away from, reached by another route.

**A lookup that misses is a throw, not a null.** Every read here is a rule the engine is about to apply, and a rule that resolved to nothing would produce a plausible number with no rule behind it.

**The models cover every key of every entry**, because `PlayRulesFileCoverageTests` re-reads the five files with `JsonUnmappedMemberHandling.Disallow` — the shape `RulesFileCoverageTests` already uses, and for the failure it was written for: `creation_rules.json` carried five keys nothing read, and one of them had rotted away from the engine while reading as a source of truth. The repository itself stays lenient, so a data edit is a failing test rather than a broken host. That check earned its keep on the day it landed: the merge from the Chapter 4 branch renamed four fields and it caught all four.

**Prose, `ambiguity` and `interpretation` are modelled too.** They are part of the file and a reader of an entry needs the ambiguity beside the number. What the engine may *consume* is narrower — see below.

## The dice contract

```csharp
public interface IDiceSource { int[] Roll(int count); }
```

**It returns raw d6 faces, and that is the whole of the design decision.** A source that answered with a count of successes would be smaller and would be wrong, because three printed rules change the *mapping* rather than the pool:

- **Checking Your Swing** (Ch.3 p.69) flattens a six from two successes to one.
- **The sub-1d floor** (p.67) throws one die that scores only on a six, and only for one.
- **Every exploding-six offer** in the book — the Defining Moment's, the team attack's, Checking Your Swing's paid one — rerolls a *face*.

None of those survives the faces being thrown away. `PROGRESS.md` item 14 records the same reasoning as a decision taken before the slice started.

Two implementations ship:

- **`SeededDice(int seed)`** — deterministic, and it keeps its `Seed` as a property so a report can print it. A balance figure without its seed is a figure nobody can reproduce.
- **`ScriptedDice(params int[] faces)`** — the faces a printed example rolled, in order. **It throws when it runs out**, which is a positive control rather than defensive programming: a source that quietly returned zeros would let an engine that skipped a defence roll still reach the printed answer. `Remaining` is the other half — a fixture asserts it is zero at the end, which catches an engine making *more* rolls than the page.

`SuccessCounter` is where faces become successes, and **there is no literal 2, 4 or 6 in it**. The map, the sub-1d floor and the automatic-success rate are all `play_meta.json`'s; Checking Your Swing swaps the map for `challenge.json`'s rather than correcting the answer afterwards.

## The ledger

Every rule the engine applies writes a `LedgerLine` naming the entry's id and carrying its `source_ref`, so **any figure in a run traces to a printed page**. That is not decoration: the owner wants balance measured rather than guessed, and a measurement is worth what its audit trail is worth.

Three things the ledger is deliberately used for beyond narration:

- **A table setting that is on says so on page one**, and one that is on but *not yet applied* says that instead. A setting accepted and quietly ignored is the worst of the three possible behaviours — the report prints the setting, the numbers do not carry it, and nothing says so.
- **An unimplemented intent says `not yet implemented`, by name, and changes nothing else.** A silent no-op is indistinguishable from a rule that ran and had no effect, and a balance measurement turns on exactly that difference.
- **Where the engine follows an `interpretation`, the line says so and quotes the printed word it is departing from.** A reading applied silently is a reading nobody can argue with.

## The fixtures rule

> **A mechanic is proved by a printed example or by a property, never by a test that restates the code.**

Two transcriptions can agree and both be wrong — which is why `data/rules/play/` is held to the book by a reflection walk against `CanonicalCombatRules` and its siblings rather than by a second copy of itself. An engine checked against a test somebody wrote from the same reading of the page is a *third* transcription carrying the same defect. The authors' own arithmetic cannot be talked round.

`PlayWorkedExamples` holds the eight the book prints — p.67's arm wrestling, p.74's movement and chase, p.76's Mind Control and the escape from it, p.79's Clint Castle, p.81's whole fight, p.85's Adversity. **They return verdicts rather than asserting**, because they are driven twice (below), and a twin with a doctored harness proves nothing.

**Every example carries a positive control before its outcome.** The book prints *successes* and `IDiceSource` returns *faces*, so each scripts faces chosen to produce the printed counts and then requires the engine to have counted exactly those — the count first, the consequence second. Three of this repository's four historical guard faults were a feature that did not run being mistaken for a feature that worked.

`PlayEnginePropertyTests` holds what no worked example can: a run always terminates inside its page limit, Health never falls below the entry's own defeat floor while Fatal Damage is off, a step never modifies the state it was given (compared by serialising before and after, not by reading fields), and a whole encounter leaves a `CharacterSheet` byte-identical through `CharacterSheetJson`. Each carries its own control that the run did something.

### The broken twin

`PlayEngineTwinTests` runs the **byte-identical** examples against a copy of the rules carrying one documented substitution: `special_effects.interpretation.duration_rounds`, `up` to `down`. `WithDefect` reads the shipped bytes and **throws if that line does not occur exactly once** — zero means the twin has stopped reproducing anything and would pass for the wrong reason; more than one means it is not the single change it documents.

Three properties of it are load-bearing:

- **It turns two examples red, not one**, and that is a property of the page rather than of the twin: p.76 prints one exchange, and an effect lasting two pages instead of three is not the effect the escape example is about. Both are named with the message each has to produce, so a twin that broke for an unrelated reason cannot be read as a working negative control.
- **The other six stay green**, and a fourth test requires the twin's other four files to be the shipped bytes — otherwise "six stay green" could be a coincidence.
- **It also proves the engine consumes the interpretation rather than assuming a direction.** An engine that hard-coded the rounding would leave the twin green, and the twin test would fail saying so. That was watched: substituting `Half(net, entry.Interpretation!.DurationRounds!)` for `Half(net, "up")` turns the twin red for exactly that reason.

## What is real, and what says `NotYetImplemented`

**Real intents**: `Attack` (with lethal/subdual/psychic, one of p.75's five `AttackType` rows, a special effect, all-out, charge and area), `Move`, `Hold`, `GrappleIntent` (grab, hold, escape), `BreakFree`, `EndTurn`, `EndPage`, and four spends — `ExtraDice`, `Reroll`, `SeizeInitiative`, `AvoidFatalDamage`.

**Intents that leave a `not yet implemented` ledger line and change nothing**:

| Intent | Entry it will read |
|---|---|
| `SpendResolve(KeepingHold)` | `keeping_hold`, Ch.4 p.76 |
| `SpendResolve(InstantRecovery)` | `instant_recovery`, Ch.4 p.76 |
| `SpendResolve(Knockback)` | `knockback`, Ch.4 p.78 |
| `SpendResolve(Luring)` | `luring`, Ch.4 p.79 |
| `SpendResolve(TeamAttack)` | `team_attacks`, Ch.4 p.79 |
| every `SpendAdversity` | the four `adversity_spend_*` entries, Ch.5 p.85 |

**Table settings recorded but not yet applied** — `Encounter.SwitchesNotYetApplied`, announced on page one of every run that turns one on: `CloseRangePenalty`, `TheDrop`, `FriendlyFire`, `HardTargets`, `SlowHealing`, and the two Gear Limit switches. The other five gritty rules are applied: `FatalDamage`, `ToughMinions`, `WoundPenalties`, `ActiveDefensesCost`, and the initiative variant beside them.

**Two ways to be out of the fight, and both are on `Combatant`**: beaten down to `damage.defeated_at_health`, and p.76's `DefeatedByEffect` — an effect whose duration reached what was left of the target, which lasts the rest of the scene and is not a Health total (an Ensnare that ends a fight does it without a point of damage). `Defeated` reads both, so `Over`, `RunToEnd` and the policy all see either. **A defeated combatant is refused rather than resolved**: every intent that is a character *doing* something — `Attack`, `Move`, `Hold`, `GrappleIntent`, `BreakFree` — is refused for a defeated actor and against a defeated target, citing whichever of the two rules put them there. The Resolve purchases are deliberately not guarded, because Chapter 5's spends are exactly what a character who has just gone down does: p.76's instant recovery and p.79's Fatal Damage rescue are both bought from there.

**Mechanics with no intent yet**: multiple actions and their −2d, combat stunts, ambushes, clobbering attacks, defending others, healing between fights, and the throwing table. None of them is stubbed; there is simply nothing to call.

## The readings this engine makes, and why each is here rather than in the data

`play-rules.md` states the discipline: **a fact field is a claim that the page states the thing**, and a reading is labelled, kept out of the transcription, and derived from something printed. These are the engine's, and they exist because a simulator cannot decline to pick.

| Reading | Why the data cannot answer it |
|---|---|
| **An odd pool banks the even half.** 11d taking automatic successes banks 5. | `automatic_successes`'s own `ambiguity`: the rule is priced in pairs and the printed example is 12d, which settles nothing. Integer division is the reading that never gives a character more than the page promises. |
| **A Toughness is halved once, however many printed rules say to halve it.** The table's `1/2 Toughness` rows (p.75) and `lethal_and_subdual`'s lethal clause (p.75) can both fire on one figure. | The two agree wherever the book's own defaults hold — the unarmed row is one of `lethal_and_subdual`'s two named subdual sources, and everything else physical defaults to lethal — so the page never has to say. Where a caller puts them at odds, halving twice would take a Toughness of 12 to 3, which no row prints. p.81's Example of Combat is what settles the shape: the mecha's Might attack is answered by 12d Armor at its full rank, the table's "Power" column doing the work with no halving in sight. |
| **"Power", in the defence column, is any defence Trait the table never names by name.** | p.75's table prints a bare `Power` beside Agility, Toughness and Willpower and says no more. Deriving the set from the table's own other columns is the reading that cannot drift from it. |
| **Dodging an area attack halves the defence rather than forfeiting a turn.** `area_attacks` (p.78) prints the two as a choice and the engine has to make one. | The page gives the choice to the defender and there is nobody to ask; halving is what keeps a fight comparable across runs, since forfeiting a turn moves a character's whole page and would make an area attack's cost depend on where in the order the dodger happened to be. The ledger names the option not taken, and a policy that could choose is what would settle it properly. |
| **The Minion cap applies to both rates.** An attack cannot defeat more Minions than are present. | `attacking_minions`'s `ambiguity`: the parenthesis is printed on the area-attack clause alone, but its second half — "or within reach" — is the phrase for an ordinary attack. |
| **Wound Penalties' deeper band replaces the shallower one.** | The entry's own `ambiguity`: the two are printed as thresholds rather than steps, and at zero Health a character is already below half of any positive Health. |
| **All-out and charge penalties expire at the end of the following page.** | The page says "until after your next turn to act", which is a turn rather than a page. This is that sentence to within a turn. |
| **The GM's alternative to seizing the initiative is a table setting.** | `seize_initiative_gm_alternative`'s `ambiguity`: no page says whether the GM's preference is fixed for a table, a campaign, or taken per purchase. A setting is the reading that makes a measurement reproducible. |
| **The GM's alternative multiplies an Edge by two.** The factor is supplied here, not read. | `seize_initiative_gm_alternative`'s effect is a sentence — "doubles the buyer's effective Edge" — and the entry carries no multiplier. The engine requires the word *doubles* to still be there and throws if it is not, rather than defaulting to 2 against a rule that has changed. |
| **A "Travel Power" is one of eight named ids.** | The entry says "a Travel Power" and Ch.2 has no such category flag; the Movement category is the closest thing and holds Powers nobody would call travel. |
| **The id breaks a tie the ladder cannot.** | p.73 says characters on the same figure and the same rung act *simultaneously*; a stepped engine has to pick an order to step in. |

And one the engine **consumes** rather than makes, with a doc comment naming the entry: `gritty_fatal_damage`'s `interpretation` — a spent Resolve leaves you one point *above* the fatal threshold, which is what the worked example computes, against a printed word that says below. The ledger line quotes both.

## `Combatant`, and the one place a sheet is read

`CombatantFactory.From(sheet, rules, derived, play, kind)` is the only code in `play/` that touches a `CharacterSheet`. Everything after it works on immutable `Combatant` snapshots, which is what makes `Encounter.Step` pure and is checked from the other side by the byte-identical round-trip test.

**The caller supplies the `Kind`.** It is an argument and not something read off the sheet: the flag on a character is presentation, no rules code may see it, and the same sheet is a Villain in one game and a Foe in another. `Kind` is the only thing that decides whether Health is halved and whether Resolve is held.

**The caller supplies the `Side` too, and it is a separate field on purpose.** Nothing in Ch.3–5 says who is on whose side — p.73's ladder is about precedence, not teams — so `Combatant.Side` is a free-form name the caller chooses, and `OneSideIsDown`, `Over` and `AttackTheWeakest.IsEnemyOf` partition on it and on nothing else. Deriving it from `Kind` was a defect rather than a reading: it made p.73's fight between Heroes unendable (every combatant was on the Hero side, so no side could ever be down) and put a Villain's Minions on the same side as the Foe they were fighting. The factories default to `heroes` for a Hero and `villains` for everybody else, which is the arrangement every fight the book works through happens to have; `Kind` still decides tie order, Health and who holds Resolve.

**Only a Hero can hold Resolve, and the type is what says so** — the constructor is private and `Combatant.Hero` is the only factory that takes a pool, so a spend charged to anybody else is a throw rather than a silent draw on nothing. Ch.2 says it twice; the character engine computes the figure for everybody and it is noise on a Villain. Here it is not noise, it is unconstructible.

**A Foe's Health is halved upward.** p.75 says a Foe halves the result and never says which way an odd total goes; the Glossary's book-wide rule (p.7) pushes a half up, so a Health of 3 halves to 2 and not to 1. Both directions are computed in the test and the wrong one is required to be wrong.

## Policies are not rules

`IPolicy` says which intent a character would have chosen. That is a person's decision at a table and a guess anywhere else, which is why `IPolicy.Name` exists and why **every report built on a run has to print it beside the seed, the N and the table settings.** A balance figure is a figure about a particular way of playing; one quoted without its policy is a figure about nothing.

`AttackTheWeakest` is the one that ships: hit the enemy with the least Health left, with the best available attack Trait, and buy a reroll when the roll fell short by two successes or fewer. **The two is a judgement, not a rule** — the page puts no condition on the purchase at all — and a policy that rerolled everything or nothing would answer a different question from the one a balance run is asking.

`RunToEnd` takes a page limit, and **the limit is why it terminates rather than an argument that it would**: two combatants who cannot get through each other's defences would fight for ever, and a simulator that hangs is worse than one that reports a draw.
