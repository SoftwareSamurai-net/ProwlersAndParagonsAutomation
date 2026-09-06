# Running a Prowlers & Paragons encounter

This server resolves fights out of Chapters 3–5 of *Prowlers & Paragons Ultimate Edition*. Read
this before you call anything else. It is embedded in the server and served verbatim by
`combat_guide`, so the document you are reading and the document in the repository are the same
bytes.

---

## The one rule: the engine resolves and you narrate

**The engine rolls, counts and decides; you say what it looked like.** Never the other way round.

Every call to `take_turn` and `run_encounters` comes back with **ledger lines**. A ledger line
carries the page of the fight, the id of the rule that was applied, that rule's `source_ref` — the
printed page it came from — and one sentence with the numbers in it. That is the record.

**You may not state a success count, a damage figure, a Health total, a Resolve or Adversity
balance, or an outcome that the ledger did not print.** Not as an estimate, not as a guess, not
"about three successes", not "that should take it down". A plausible number is worse than no
number, because nobody can tell it from a real one: this engine exists so that a fight is
*measured* rather than *imagined*, and a figure you supplied yourself makes the whole record worth
nothing. If you want a number, take a turn and read it off the ledger.

What you *should* do with a ledger line is turn it into prose. "Citizen Soldier rolls 12d Might
and scores 8; the robots roll 6d Threat and score 3; with 5 net successes he could have defeated
up to 5 Minions and there are 4" is a line you may render as *the Soldier lands in the middle of
them and there is nothing left standing*. The narration is yours. The 8, the 3, the 5 and the 4
are not.

**When the engine refuses, say so.** A ledger line beginning `not yet implemented` means the rule
was named, recognised and not applied — see the tables at the end. Do not narrate around it and do
not pretend the effect happened. A silent no-op is indistinguishable from a rule that ran and
changed nothing, which is exactly how a simulator comes to be confidently wrong.

**A refused character sheet is an error result with a reason, not a crash and not a repair.**
Fix the sheet or ask the person; do not invent a legal-looking one to get past the refusal, and do
not quote a Hero Point cost — costing and validating a character is the *other* server's job
(`prowlers-and-paragons`), and no tool here decides whether a character is legal.

## Who holds what

**Only Heroes hold Resolve.** Chapter 2 says so twice and Chapter 5 gives the GM **Adversity**
instead. A Villain, a Foe, an Extra and a group of Minions hold none, and a spend charged against
one is an error rather than a quiet zero — build the combatant as a hero, or spend Adversity on
their behalf.

**The GM's pool is Adversity**, opened at one point per Hero per issue plus the scene's Challenge
Level multiplied by the number of Heroes. `spend_adversity` with kind `anything_resolve_can` buys,
for an NPC, whatever a point of Resolve could have bought — and it has to name *which* purchase in
`as_resolve`, because a point spent on nothing in particular is a point spent on nothing. **This
slice runs two of them**, `extra_dice` and `reroll`; every other purchase it may name is recognised
and answered with a `not yet implemented` line. The table at the end of this document says which is
which, and it is the only place to read that from.

## Quoting a measurement

`run_encounters` answers with a rate. **A rate is only ever quoted with the four things printed
beside it in the same object: `runs` (the N), `seeds`, `policy`, and `table`.** All four are in
every answer for exactly this reason.

- **N.** The tool refuses fewer than 30 runs and says why. A win rate off five fights is noise
  wearing a percentage sign.
- **Seeds.** The runs are seeded `seed`, `seed + 1`, … so the same call gives the same answer.
  Quote the range; a figure nobody can reproduce is an anecdote.
- **Policy.** A policy is *not a rule*. It is a guess about how people play — `attack_the_weakest`
  focuses fire on whoever is nearly down and buys a reroll when a roll came close, which is one
  real table habit out of several. A balance figure is a figure about a party that plays that way.
- **Table settings.** The default is the book's baseline, which is every optional Gritty rule
  **off**. A run with `wound_penalties` on is measuring a different game from one without it.

Say what was measured, in those terms, or do not say it.

## The calls

- **`combat_guide`** — this document.
- **`start_encounter`** — combatants, the table's switches, a Challenge Level and a seed. Answers
  with an encounter id, the turn order with each combatant's Edge, the opening Adversity pool and
  the table echoed back. Encounters are held in memory by id, for this session only.
- **`take_turn`** — an encounter id and **one** intent. Acting and rolling are one call: there is
  no separate "roll" step, because an intent is a request and the engine decides what it produces.
  Answers with the ledger lines that step added and the public state.
- **`run_encounters`** — the same setup plus `runs`, `policy` and `max_pages`, run headless.

A combatant is either a character sheet — the shape the character server's `creation_guide`
describes — with a `kind` of `hero`, `villain`, `foe` or `extra` and a `side`, or a group of
Minions with a `threat_rank`, a `count` and a `side`. **`kind` and `side` are yours to say and
nothing derives them**: the Hero/Villain flag on a sheet is presentation, the same sheet is a
Villain in one GM's game and a Foe in another's, and Chapter 4's tie-break ladder is about
precedence rather than teams — a fight between Heroes is a fight the book prints.

**A sheet has to name a tier these rules have.** The tier fixes the Trait Cap, and Ch.5 p.83
measures a Hero's opening Resolve down from it — so a tier that cannot be looked up is refused
rather than treated as none, which would put a Hero into the fight with 0 Resolve and no way to
tell that from a Hero who really has none. The tier each character was built to is on the opening
ledger, beside the Resolve it bought.

## What this engine does not yet model

Everything below is *recognised and not applied*. Each leaves a ledger line saying so by name, so
a run that touched one is a run you can tell apart from one that did not. **Do not narrate an
effect from this list as though it happened**, and do not quote a balance figure from a run that
turned one of these table settings on without saying that the numbers do not carry it.

**`Encounter.EntriesNotYetApplied`** — rules of the book this server names and does not apply:

| Entry | What it is |
|---|---|
| `keeping_hold` | Ch.4 p.76. Carrying a defeating special effect into the next scene. |
| `knockback` | Ch.4 p.78. Turning a heavy subdual blow into a spectacular flight. |
| `luring` | Ch.4 p.79. Redirecting a dodged attack into whatever was behind you. |
| `team_attacks` | Ch.4 p.79. Making a team attack's sixes explode. |
| `adversity_spend_suppress_flaw` | Ch.5 p.85. Suppressing an NPC's Flaw for a scene. |
| `adversity_spend_misfortune` | Ch.5 p.85. A piece of misfortune that is a challenge. |
| `adversity_spend_villainy` | Ch.5 p.85. An act of villainy the Heroes cannot simply prevent. |
| `modifier_cover` | p.75. Nothing on an attack can say a target is behind something. |
| `modifier_size` | p.75. Nothing says how big anybody is. |
| `modifier_visibility` | p.75. Nothing says what the light is like. |

The last three matter to anybody reading a number off this server: every figure it produces was
measured **in clear air, in the open, against somebody the same size**.

**`Encounter.SwitchesNotYetApplied`** — table settings you may turn on, which are announced on
page one of the run and do not move the numbers:

| Setting | Entry |
|---|---|
| `CloseRangePenalty` | `gritty_close_range` |
| `TheDrop` | `gritty_the_drop` |
| `FriendlyFire` | `gritty_friendly_fire` |
| `HardTargets` | `gritty_hard_targets` |
| `SlowHealing` | `gritty_slow_healing` |
| `RaisedGearLimit` | `gritty_raised_gear_limit` |
| `GearLimitRank` | `gritty_raised_gear_limit` |

The other five — `FatalDamage`, `ToughMinions`, `WoundPenalties`, `ActiveDefensesCost` and the
initiative variant beside them — are applied.

**Spends that refuse by name** — the `kind` values `spend_resolve` and `spend_adversity` accept,
recognise, and answer with a `not yet implemented` line:

| Spend | Tool |
|---|---|
| `keeping_hold` | `spend_resolve` |
| `knockback` | `spend_resolve` |
| `luring` | `spend_resolve` |
| `team_attack` | `spend_resolve` |
| `suppress_flaw` | `spend_adversity` |
| `misfortune` | `spend_adversity` |
| `villainy` | `spend_adversity` |

Everything else a spend can name is resolved: `extra_dice`, `reroll`, `seize_initiative`,
`instant_recovery`, `avoid_fatal_damage` and `stabilise` for a Hero, and `anything_resolve_can`
for the GM naming `extra_dice` or `reroll`.

**What `anything_resolve_can` may name** — the six above are a *Hero's* purchases, and the GM's
pool does not yet buy all six. p.85's first purchase is the Resolve purchases with different money
behind them, and this slice runs the two that are decided after the roll; the rest are recognised
and answered with a `not yet implemented` line, exactly like the table above. **The two columns
disagreeing with the engine is the failure this table exists to prevent** — an earlier version of
this document advertised all six, four of them refused, and a model reading it had no way to tell
a purchase that had happened from one that had not:

| `as_resolve` | What the GM's pool does with it |
|---|---|
| `extra_dice` | bought |
| `reroll` | bought |
| `seize_initiative` | not yet implemented |
| `instant_recovery` | not yet implemented |
| `avoid_fatal_damage` | not yet implemented |
| `stabilise` | not yet implemented |
| `keeping_hold` | not yet implemented |
| `knockback` | not yet implemented |
| `luring` | not yet implemented |
| `team_attack` | not yet implemented |
