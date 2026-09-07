# The play engine

Read before touching `play/` — the second engine, which resolves an action where `engine/` costs and validates a character.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

Read [`play-rules.md`](play-rules.md) too: it is the store this engine reads, and every convention it records — `interpretation` against `ambiguity`, `table_setting`, `transcribed_here: false` — decides what this engine is allowed to do with a field.

---

## What `play/` is, and what it is not

`play/` is a project beside `engine/`. It references `engine/` and **nothing on the character side references it back** — not `engine/`, not `sheets/`, not `cli/`, `web/` or `mcp/`. Exactly two projects do: the test project, and `mcp-play/`, the encounter server that is this engine's one host (see [`mcp-and-headless.md`](mcp-and-headless.md)). `PlayContractTests` holds that list as an allowlist, so a third reference is flagged rather than missed.

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

**Real intents**: `Attack` (with lethal/subdual/psychic, one of p.75's five `AttackType` rows, a special effect, all-out, charge, area and team), `Move`, `Hold`, `GrappleIntent` (grab, hold, escape), `BreakFree`, `Stabilise`, `EndTurn`, `EndPage`, **every Resolve purchase Chapters 4 and 5 print** — `ExtraDice`, `Reroll`, `SeizeInitiative`, `AvoidFatalDamage`, `Stabilise`, `InstantRecovery`, `KeepingHold`, `Knockback`, `Luring` and `TeamAttack` — and `SpendAdversity(AnythingResolveCan)` naming one of the purchases on `Encounter.AdversityBuys`, and `SpendAdversity` naming `SuppressFlaw` or `Misfortune`. `SpendResolve` has no `default` branch any more: a member of the enum with no branch is a compile-time hole rather than a silent refusal.

### Not applied, and named in the code so the two lists cannot drift

**`Encounter.EntriesNotYetApplied`** — every entry this slice knows about and does not apply. `PlayEngineStepTests` requires this table and that set to match in both directions, and drives every purchase through `Step` to be sure the ones listed refuse and the ones not listed do not.

| Entry | What it is, and why not |
|---|---|
| `adversity_spend_villainy` | Ch.5 p.85. |
| `modifier_cover` | p.75. Nothing on an `Attack` can say a target is behind something. |
| `modifier_size` | p.75. Nothing says how big anybody is. |
| `modifier_visibility` | p.75. Nothing says what the light is like. |

The last three are listed rather than left silent because a reader of a balance run needs to know the figure was measured in clear air, in the open, against somebody the same size. **Chapter 4's four Resolve purchases used to head this table and no longer do**: `keeping_hold`, `knockback`, `luring` and `team_attacks` are applied, and what each of them cannot reach is recorded below rather than as a whole entry nothing runs.

**`Encounter.SwitchesNotYetApplied`** — table settings a run may turn on, announced on page one of every run that does, saying that the numbers do not carry them:

| Setting | Entry |
|---|---|
| `CloseRangePenalty` | `gritty_close_range` |
| `TheDrop` | `gritty_the_drop` |
| `FriendlyFire` | `gritty_friendly_fire` |
| `HardTargets` | `gritty_hard_targets` |
| `SlowHealing` | `gritty_slow_healing` |
| `RaisedGearLimit` | `gritty_raised_gear_limit` |
| `GearLimitRank` | `gritty_raised_gear_limit` |

The other five gritty rules are applied: `FatalDamage`, `ToughMinions`, `WoundPenalties`, `ActiveDefensesCost`, and the initiative variant beside them.

**p.85's first Adversity purchase is applied**, and it is the one that is not a rule of its own: "whatever a point of Resolve could have done, on behalf of any NPC" is the Resolve purchases with the GM's money behind them. `SpendAdversity` carries which one — a point spent on nothing in particular would be a point spent on nothing — the pool pays, and the NPC's non-existent Resolve is never touched. `Encounter.AdversityBuys` is the list of the ones it runs, kept as one list so the gate and the dispatch cannot drift; the rest refuse by name, because they still charge the buyer's own pool and an NPC has none. The other three Adversity spends are rules of their own, and each is a scene rather than a roll — see below.

**p.85's suppress-a-Flaw spend: what is state, and what is narration.** "GMs can spend 1 Adversity to prevent a Flaw from getting the better of a Villain, Foe, or Extra for the rest of a scene, but no character can benefit from this more than once per issue." **Nothing in `play/` makes a Flaw bite** — the page says an NPC's Flaws come into play "whenever the opportunity presents itself", which is a GM's judgement and not a roll — so *what the suppression saves the character from* never reaches the state, and the ledger line says in as many words that it is the GM's to narrate. Everything the page states in figures does reach it: `cost_adversity` leaves the pool exactly once, only the three kinds on `eligible_characters` may be bought out (a Hero or a Minion group is refused by name, off that list rather than off a list here), and `Combatant.SuppressedFlaw` carries the Flaw the GM named. **That field is read**, which is what keeps it from being a flag nothing looks at: the second purchase against the same character is refused, and it is on the public state the encounter server publishes. A purchase that names no Flaw is refused with nothing spent, the shape a lure that names nobody is refused in — this engine holds no Flaws, so an unnamed one would put "some weakness or other" on the ledger.

**p.85's misfortune: the pool is the whole of the mechanism, and the entry says so.** "You can spend 1 Adversity to throw a misfortune at the Heroes." The entry's own `ambiguity` records that **nothing here is mechanical** — a misfortune is three examples and two prohibitions, with no roll, no threshold, no duration and no way of resisting one printed anywhere — so a point leaving the pool is not most of this rule, it *is* this rule, and an engine that invented a mechanic for it would be inventing one the book does not have. What the ledger carries beside the point is the GM's own sentence, taken off `SpendAdversity.Narration`: **a spend that does not say what the misfortune is is refused with nothing spent**, because a pool that has moved with no words behind it is a line nobody can narrate from and nobody can audit. The line quotes what the page asks of one — a challenge rather than a punishment, never a heavy-handed plot device — and says the misfortune itself is the GM's to narrate.

It is also **the one spend aimed at a side rather than at a character**, so its `Actor` is not read and its ledger line names none: p.85 throws it at "the Heroes", the line names the side they are on, and a fight with no Hero in it is refused because there is nobody for it to land on.

**What a team attack cannot coordinate**: `team_attacks`'s `participants_act_at` — "the end of the page" — and `all_participants_must_target_the_same_enemy` both describe several characters acting as one, and a `Step` is one character's action. The `attack_bonus_dice`, the per-target-per-battle limit and the exploding sixes are applied; the ledger line beside the bonus names the two clauses that are not. `the_limit_may_be_lifted_by` is "the Heroes being clever about it, or the GM ruling otherwise", which is a person's decision: the refusal quotes it, and a caller who has been told the GM ruled otherwise attacks without the flag.

**What a lure cannot hit**: `luring.redirects_to` is "whatever lies directly behind you", and this engine has no scenery, nothing with a Structure and nothing behind anybody. A lure that names nobody is **refused rather than charged for** — the attack had already missed the buyer by at least the margin p.79 asks for, so a point taken for it would buy a state change nothing could receive, which is the shape of defect this guide's ledger rules exist to prevent. Naming a person is what `SpendResolve.Target` is for.

**What a knockback cannot hit**: `knockback`'s `damage_on_striking_a_solid_object`, `the_object_must_be_tougher_than_the_target` and `a_passive_defense_above_the_objects_structure` all need a piece of scenery with a Structure, and this engine has neither. The throw and the forfeited turn are applied; the ledger line names the clause that is not, rather than leaving a reader to assume the extra damage was rolled. `target_falls_prone` is on the line and nowhere else on purpose: **the book attaches no mechanic to being prone.** All four printings of the word — p.78's here, and Hyper Breath, Shockwave and Slick in Chapter 2 — either pair it with losing the next turn to act or leave it as description, and nothing anywhere rolls against it. A `Prone` flag would be a state nothing reads.

**One clause inside a rule that is otherwise applied**: `minions_attacking.the_group_bonus_does_not_apply_to` — the size bonus is not supposed to count towards penetrating cover or hurting somebody behind Armor or a Force Field. The entry's own `ambiguity` is why it is not applied: both of those are decided by the same attack roll the bonus is granted to, so read strictly it asks for two attack totals against one defence roll and the page offers no mechanism. The ledger line says so rather than claiming the bonus is "on the attack roll and nothing else", which is what it used to say. The per-target caps in the same entry **are** applied.

**Fatal Damage is a clock, not a floor.** With that setting on, lethal damage past `dying_begins_when_lethal_damage_reduces_you_to` starts a character bleeding at `dying_damage_per_page`, ticked at `EndPage`, until `dying_ends_at` — stabilisation or the negative of their full Health. Three things stop it: p.79's `Stabilise` roll (the Trait `stabilise_roll` names, at its own difficulty and threshold), `cost_resolve_to_stabilise_immediately`, and the Fatal Damage rescue itself, which `resolve_also_stabilises_if_necessary` makes do both. `instant_recovery_requires_being_stable` is why p.76's instant recovery is implemented here rather than listed as unimplemented: a rule *about* a purchase cannot be read while the purchase is a stub. `stabilise_also_by` — "a Power like Healing" — is prose and a GM's call, so the ledger names it rather than applying it.

**Two ways to be out of the fight, and both are on `Combatant`**: beaten down to `damage.defeated_at_health`, and p.76's `DefeatedByEffect` — an effect whose duration reached what was left of the target, which lasts the rest of the scene and is not a Health total (an Ensnare that ends a fight does it without a point of damage). `Defeated` reads both, so `Over`, `RunToEnd` and the policy all see either. **A defeated combatant is refused rather than resolved**: every intent that is a character *doing* something — `Attack`, `Move`, `Hold`, `GrappleIntent`, `BreakFree` — is refused for a defeated actor and against a defeated target, citing whichever of the two rules put them there. **Chapter 5's Resolve purchases are deliberately not guarded, and Chapter 4's four are.** The unguarded ones are exactly what a character who has just gone down does: p.76's instant recovery and p.79's Fatal Damage rescue are both bought from there, and refusing them would make the two purchases the book prints for that situation unreachable. Keeping a hold, knocking somebody across the street, luring and leading a team attack are things a character does while they are still in the fight, so `KeepingHold`, `Knockback`, `Luring` and `TeamAttack` refuse a defeated buyer, citing whichever rule put them out.

**Grappling is four states, not two.** `Grapple` records the *move* as well as the band, because p.76 gives them different consequences: a full **hold** is control over a person and leaves them only trying to escape, a full **grab** is control of an *object* and restrains nobody, and a partial grab or hold takes both characters' active defences away *against anyone else* — the character they are tangled with is the one they can still meet. A partial hold additionally leaves both of them one physical action, "an opposed Might roll". Two of those were applied to nobody and one was applied to the wrong person: every full grapple immobilised its loser, so a character who had lost their sword could not dodge.

**Mechanics with no intent yet**: multiple actions and their −2d, combat stunts, ambushes, clobbering attacks, defending others, healing between fights, throwing something at somebody (p.74's table is read, but only by a knockback: nothing picks an object up), `gritty_fatal_damage`'s `resolve_may_be_spent_on_damage_you_inflict_on_someone_else` (`SpendResolve.Target` exists for p.79's luring, and the Fatal Damage rescue does not read it — buying back somebody else's blow is a rescue this engine still only performs on its buyer), and **the item a full grab wins** — this engine has no inventory, so "use or toss it the same page" is a consequence it records on the ledger and cannot apply. None of them is stubbed; there is simply nothing to call.

## The readings this engine makes, and why each is here rather than in the data

`play-rules.md` states the discipline: **a fact field is a claim that the page states the thing**, and a reading is labelled, kept out of the transcription, and derived from something printed. These are the engine's, and they exist because a simulator cannot decline to pick.

| Reading | Why the data cannot answer it |
|---|---|
| **An odd pool banks the even half.** 11d taking automatic successes banks 5. | `automatic_successes`'s own `ambiguity`: the rule is priced in pairs and the printed example is 12d, which settles nothing. Integer division is the reading that never gives a character more than the page promises. |
| **A Toughness is halved once, however many printed rules say to halve it.** The table's `1/2 Toughness` rows (p.75) and `lethal_and_subdual`'s lethal clause (p.75) can both fire on one figure. | The two agree wherever the book's own defaults hold — the unarmed row is one of `lethal_and_subdual`'s two named subdual sources, and everything else physical defaults to lethal — so the page never has to say. Where a caller puts them at odds, halving twice would take a Toughness of 12 to 3, which no row prints. p.81's Example of Combat is what settles the shape: the mecha's Might attack is answered by 12d Armor at its full rank, the table's "Power" column doing the work with no halving in sight. |
| **"Power", in the defence column, is any defence Trait the table never names by name.** | p.75's table prints a bare `Power` beside Agility, Toughness and Willpower and says no more. Deriving the set from the table's own other columns is the reading that cannot drift from it. |
| **A charge's impact is the charger's own net against their own attack roll, at the damage rate, less what the target took.** | p.78 says the charger "makes their own passive defense roll against the attack" and that the self-damage is "reduced by the damage inflicted on the target", and prints no arithmetic. The attack roll's successes are the only threshold on the table; `damage.damage_per_net_success` is the only rate the book has; and the subtraction floors at nothing, because a charge that hurt the target more cannot heal the charger. |
| **A charge may be rolled with Might, Density, Growth or a Travel Power.** | `charge_attacks.attack_traits` lists three ids and one piece of prose — "any Trait usable for a close combat attack" — and Chapter 2 has no close-combat flag. The narrowest reading that keeps every charge the book describes legal is the attack Trait of p.75's two close-combat rows, which is Might. |
| **An `Encounter` is one scene, so "the rest of the scene" is the rest of the fight.** p.85's suppressed Flaw is carried to the end and never expires inside one. | The page measures the suppression in scenes and this engine's largest unit is the encounter: `Step` turns pages, and nothing in it ends a scene. A duration that expired on some page number would be a clock the book does not print. |
| **The suppress-a-Flaw limit is refused per character, not per Flaw.** | The printed sentence names a character — "no character can benefit from this more than once per issue" — and the entry's own `ambiguity` says the two readings differ for anybody carrying two Flaws. The refusal quotes the ambiguity rather than settling it. |
| **A held character's Powers are refused rather than adjudicated.** | `hold` says a held character may use "any Power they could reasonably use while physically restrained", `adjudicated_case_by_case_by: gm`. That is a judgement and this engine has nobody to ask, so it applies the clause it can — "only trying to escape" — and the refusal names the clause it could not. |
| **An attack penetrates a passive defence when the attacking Trait's rank is greater than the defence's.** p.78's guard on going all-out turns on it. | The page says "opponents who could not penetrate your passive defense still cannot" and never defines penetrating. Chapter 4 states the test once, under Cover: `modifier_cover.attacking_through_cover_requires` is "an attack rank greater than the cover's Structure". The engine reads that phrase and throws if it goes away. |
| **Dodging an area attack halves the defence rather than forfeiting a turn.** `area_attacks` (p.78) prints the two as a choice and the engine has to make one. | The page gives the choice to the defender and there is nobody to ask; halving is what keeps a fight comparable across runs, since forfeiting a turn moves a character's whole page and would make an area attack's cost depend on where in the order the dodger happened to be. The ledger names the option not taken, and a policy that could choose is what would settle it properly. |
| **The Minion cap applies to both rates.** An attack cannot defeat more Minions than are present. | `attacking_minions`'s `ambiguity`: the parenthesis is printed on the area-attack clause alone, but its second half — "or within reach" — is the phrase for an ordinary attack. |
| **Wound Penalties' deeper band replaces the shallower one.** | The entry's own `ambiguity`: the two are printed as thresholds rather than steps, and at zero Health a character is already below half of any positive Health. |
| **All-out and charge penalties expire at the end of the following page.** | The page says "until after your next turn to act", which is a turn rather than a page. This is that sentence to within a turn. |
| **The GM's alternative to seizing the initiative is a table setting.** | `seize_initiative_gm_alternative`'s `ambiguity`: no page says whether the GM's preference is fixed for a table, a campaign, or taken per purchase. A setting is the reading that makes a measurement reproducible. |
| **The GM's alternative multiplies an Edge by two.** The factor is supplied here, not read. | `seize_initiative_gm_alternative`'s effect is a sentence — "doubles the buyer's effective Edge" — and the entry carries no multiplier. The engine requires the word *doubles* to still be there and throws if it is not, rather than defaulting to 2 against a rule that has changed. |
| **A policy's attack Traits are the table's named attacking Traits plus the combatant's own Powers.** | Nothing says which Traits a character would attack with; p.75's table is the only printed list of attacking Traits, and its "Power" column is the combatant's, so the set is derived from the two rather than typed into the policy. It is a policy's judgement either way, which is why `IPolicy.Name` goes in every report. |
| **Luring is decided on the roll that has just happened.** `declared_before` is "the attacker makes their attack roll". | A `Step` is the whole exchange — the attack is declared, rolled and applied in one — so there is no point between the declaration and the roll for a declaration to sit in. p.79 puts the *payment* after the roll anyway ("if your defense roll exceeds their attack roll by 3 or more, you can spend 1 Resolve"), and every condition the declaration gates is checked against the roll itself. |
| **"A physical or energy attack" is every row of p.75's table but the mental one.** | `luring.applies_to_attack_types` prints two words the Attack and Defense table does not use, and Chapter 2 has no physical/energy flag. The mental row is the one the table names, so the set is derived by excluding it rather than by listing the other four here. |
| **A knocked-back target has no weight rank, so the throwing rank is the attack rank itself.** p.78 throws them "as if by someone with a Might rank equal to your attack rank", and p.74 turns a Might into a distance. | `throwing_range.rank_formula` subtracts the object's weight rank from the thrower's Might, and nothing in Chapters 3–5 gives a character a weight rank — Chapter 6 prices gear, not people. Reading the throwing rank as the attack rank is the longest throw the sentence can mean, and the ledger line says which figure it used. The table's own `ambiguity` about ranks below 3d never arises: `throwing_range.ordinary_people_reach` covers every rank up to `table_used_when_might_exceeds`. |
| **A knockback moves the pair and no other pair.** | `EncounterState.Ranges` is pairwise because p.73's ranges are: there is no board and no distance from a fixed point, so "flies backwards" can only be expressed as the distance between the two characters involved. Where it lands is the class the throw reaches, and never nearer than they already were. |
| **A "Travel Power" is one of eight named ids.** | The entry says "a Travel Power" and Ch.2 has no such category flag; the Movement category is the closest thing and holds Powers nobody would call travel. |
| **The id breaks a tie the ladder cannot.** | p.73 says characters on the same figure and the same rung act *simultaneously*; a stepped engine has to pick an order to step in. |
| **A Resolve purchase is not turn-gated.** `instant_recovery.taken_on` is "your next turn to act" and this engine does not enforce it. | A defeated character has no turn to be theirs, and p.79's Fatal Damage rescue is bought in the middle of somebody else's. Enforcing the phrase would make the two purchases the book prints for a character who has just gone down unreachable. |

And one the engine **consumes** rather than makes, with a doc comment naming the entry: `gritty_fatal_damage`'s `interpretation` — a spent Resolve leaves you one point *above* the fatal threshold, which is what the worked example computes, against a printed word that says below. The ledger line quotes both.

## `Combatant`, and the one place a sheet is read

`CombatantFactory.From(sheet, rules, derived, play, kind)` is the only code in `play/` that touches a `CharacterSheet`. Everything after it works on immutable `Combatant` snapshots, which is what makes `Encounter.Step` pure and is checked from the other side by the byte-identical round-trip test.

**The caller supplies the `Kind`.** It is an argument and not something read off the sheet: the flag on a character is presentation, no rules code may see it, and the same sheet is a Villain in one game and a Foe in another. `Kind` is the only thing that decides whether Health is halved and whether Resolve is held.

**The caller supplies the `Side` too, and it is a separate field on purpose.** Nothing in Ch.3–5 says who is on whose side — p.73's ladder is about precedence, not teams — so `Combatant.Side` is a free-form name the caller chooses, and `OneSideIsDown`, `Over` and `AttackTheWeakest.IsEnemyOf` partition on it and on nothing else. Deriving it from `Kind` was a defect rather than a reading: it made p.73's fight between Heroes unendable (every combatant was on the Hero side, so no side could ever be down) and put a Villain's Minions on the same side as the Foe they were fighting. The factories default to `heroes` for a Hero and `villains` for everybody else, which is the arrangement every fight the book works through happens to have; `Kind` still decides tie order, Health and who holds Resolve.

**Only a Hero can hold Resolve, and the type is what says so** — the constructor is private and `Combatant.Hero` is the only factory that takes a pool, so a spend charged to anybody else is a throw rather than a silent draw on nothing. Ch.2 says it twice; the character engine computes the figure for everybody and it is noise on a Villain. Here it is not noise, it is unconstructible.

**A Foe's Health is halved upward.** p.75 says a Foe halves the result and never says which way an odd total goes; the Glossary's book-wide rule (p.7) pushes a half up, so a Health of 3 halves to 2 and not to 1. Both directions are computed in the test and the wrong one is required to be wrong.

## Policies are not rules

`IPolicy` says which intent a character would have chosen. That is a person's decision at a table and a guess anywhere else, which is why `IPolicy.Name` exists and why **every report built on a run has to print it beside the seed, the N and the table settings.** A balance figure is a figure about a particular way of playing; one quoted without its policy is a figure about nothing.

`AttackTheWeakest` is the one that ships: hit the enemy with the least Health left, with the best available attack Trait, and buy a reroll when the roll fell short by two successes or fewer. **What "available" means is derived, not listed** — p.75's table names the attacking Trait of every row, and the table's "Power" column means the combatant's own, which is the same derivation `ChooseDefence` makes on the other side of the roll. Four ids used to be typed into the policy, and a character built out of Energy Blast held their action for a whole fight while holding an obvious weapon. **The two is a judgement, not a rule** — the page puts no condition on the purchase at all — and a policy that rerolled everything or nothing would answer a different question from the one a balance run is asking.

`RunToEnd` takes a page limit, and **the limit is why it terminates rather than an argument that it would**: two combatants who cannot get through each other's defences would fight for ever, and a simulator that hangs is worse than one that reports a draw.
