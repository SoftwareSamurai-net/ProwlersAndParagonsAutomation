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

## Two shapes of answer

Every tool here answers with JSON and there are only two shapes. A call that did what was asked
answers `{"ok": true, …}`. A call that could not answers:

```jsonc
{ "ok": false, "problem": { "code": "NO_SUCH_TIER", "message": "…" } }
```

**A refusal is an answer, not a transport error, and this is the half that is easy to get wrong.**
It comes back as an ordinary *successful* tool result: the `isError` flag a tool result carries is
never set by this server, and the refusal is in the payload. So decide on `ok` and never on the
protocol level — a client waiting for an error will read `{"ok": false}` as a fight that started,
and then take turns in an encounter that does not exist. The character server (`prowlers-and-
paragons`) answers the same way, so one reader works for both.

A `problem` is about the **request**: a tier these rules have not got, an intent this engine does
not take, a side nobody set. It is never a finding about the character — whether a character is
legal is the other server's question — and it is never a result of the fight. **A refused call
changed nothing**, so there is no page, no roll and no ledger line behind it, and there is nothing
in it to narrate: read the message and fix the call.

## Who holds what

**Only Heroes hold Resolve.** Chapter 2 says so twice and Chapter 5 gives the GM **Adversity**
instead. A Villain, a Foe, an Extra and a group of Minions hold none, and a spend charged against
one is an error rather than a quiet zero — build the combatant as a hero, or spend Adversity on
their behalf.

**The GM's pool is Adversity**, opened at one point per Hero per issue plus the scene's Challenge
Level multiplied by the number of Heroes. `spend_adversity` with kind `anything_resolve_can` buys,
for an NPC, whatever a point of Resolve could have bought — and it has to name *which* purchase in
`as_resolve`, because a point spent on nothing in particular is a point spent on nothing. **It runs the
purchases the last table of this document marks bought**; every other purchase it may name is
recognised and answered with a `not yet implemented` line. The table at the end of this document says which is
which, and it is the only place to read that from.

**For an NPC, and never for a Hero.** p.85 spends the GM's pool "on behalf of any NPC whether
they're Villains, Foes, Minions, or Extras", and a Hero named as the `actor` of an
`anything_resolve_can` is refused with nothing spent. The two pools are the whole of that side of
the economy: a Hero buys their own dice with their own Resolve, and a point of Adversity that
bought one for them would be the GM paying to help the party.

## Modifiers: cover, size and the light (p.75)

**Three things move a pool besides the sheet, and each is yours to say — nothing derives any of
them.** By default none of them applies: every figure this server produces is measured **in clear
air, in the open, against somebody the same size** unless you said otherwise.

- **Cover** goes on the attack, because it is a fact about one line of sight. Send `"cover"` as
  `light`, `heavy` or `almost_full` for a band on the attack roll, or `complete` for a target hidden
  altogether — **which cannot be hit**, and is refused with nothing rolled.
- **Attacking through the obstacle** is a separate declaration and it is `"cover_structure"`: send
  the obstacle's Structure rank. Two printed things then follow and both are applied — the attack
  rank has to be **greater** than the Structure or nothing is rolled, and the target may answer with
  the Structure as a **passive** defence, which can be the roll that stops the attack. Leave it out
  to shoot at whatever of the target is exposed and pay the band alone.
- **Size** goes on the combatant, as `"size"` — a bare number whose only meaning is the ratio
  between two of them, in whatever unit your fight is using; omit it and everybody is the same size.
  It moves the **defender's active defence rolls and nothing else**: a Toughness, an Armor and a
  cover's Structure never move for it, however big the attacker is. This server derives the band
  from the two sizes, so do not try to send one.
- **Visibility** goes on the fight, as `"visibility"` on `start_encounter` and `run_encounters`:
  `clear`, `poor` or `none`. It costs **attack rolls and active defence rolls alike**, on both sides
  of every exchange. It comes back inside `table`, so a rate quoted with its table carries it.
- **An invisible opponent** is a fact about a pair rather than about the scene, so it is
  `"invisible"` on a combatant. p.75 counts one as no visibility at all. **Carrying the Invisibility
  Power is not the same as being invisible** — Ch.2 prints "you *can* turn invisible" — so this
  server does not read it off the sheet and you say when somebody has actually gone.
- **Blind Fighting and Radar are read off the sheet** and cancel the penalty for whoever carries
  one; the ledger line says which Power compensated. p.75 gives those two as examples ("a Power
  that compensates for this, like…"), so if you have ruled that some other Power covers it, do not
  put that character in the dark.

A pool taken below one die is not a pool of nothing: p.67 throws one die that scores only on a six.
So a −3d against a 2d Trait is a real roll with a real, small chance, and the ledger shows it.

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
  **off**, in clear air. A run with `wound_penalties` on is measuring a different game from one
  without it, and so is a run in the dark — which is why the scene's `visibility` comes back inside
  that same object rather than beside it.

**The other two of p.75's modifiers are facts about a character, so they come back per combatant.**
`table` cannot carry them: a `size` moves the defender's active defence by up to two dice and an
`invisible` costs whoever faces one three, and both are somebody's rather than the scene's. Each
row of `by_combatant` echoes its own `size` and `invisible` beside its defeat rate, so a report
says which fight it measured without anybody having to remember what they sent.

Say what was measured, in those terms, or do not say it.

## The calls

**The names below are the arguments as the schema spells them**, and they are worth reading rather
than guessing: several are camelCase on the way in and snake_case on the way back out, because the
answer is JSON of this server's own making and the arguments are the tool's signature.

- **`combat_guide`** — this document. It takes no arguments.
- **`start_encounter`** — `combatants`, the table's switches in `table`, a `challengeLevel`, a
  `seed`, an `openingRange` and the scene's `visibility`. Answers
  with an encounter id, the turn order with each combatant's Edge, the opening Adversity pool and
  the table echoed back. Encounters are held in memory by id, for this session only.
- **`take_turn`** — an `encounterId` and **one** `intent`. Acting and rolling are one call: there is
  no separate "roll" step, because an intent is a request and the engine decides what it produces.
  Answers with the ledger lines that step added and the public state.
- **`run_encounters`** — the same setup, `visibility` included, plus `runs`, `policy` and
  `maxPages`, run headless. That
  last one is the sharpest of the camelCase pair above: the answer prints the page limit back in
  snake_case, and sending it that way sends an argument the schema has not got.

A combatant is either a character sheet — the shape the character server's `creation_guide`
describes — with a `kind` of `hero`, `villain`, `foe` or `extra` and a `side`, or a group of
Minions with a `threat_rank`, a `count` and a `side`. Either may also carry a `size` and an
`invisible` flag; see **Modifiers** above. **`kind` and `side` are yours to say and
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

**`Encounter.EntriesNotYetApplied` is empty**, and that is a claim held to the engine rather than a
sentence: no rule of Chapters 3–5 that this server names is left unapplied. p.75's cover, size and
visibility were the last three on that list and are applied now — see **Modifiers** below. What is
still not modelled is the table settings under it, and what `anything_resolve_can` may name.

**`Encounter.SwitchesNotYetApplied`** — table settings you may turn on, which are announced on
page one of the run and do not move the numbers:

| Setting | Entry |
|---|---|
| `TheDrop` | `gritty_the_drop` |
| `FriendlyFire` | `gritty_friendly_fire` |
| `SlowHealing` | `gritty_slow_healing` |
| `RaisedGearLimit` | `gritty_raised_gear_limit` |
| `GearLimitRank` | `gritty_raised_gear_limit` |

The other seven — `FatalDamage`, `ToughMinions`, `WoundPenalties`, `ActiveDefensesCost`,
`HardTargets`, `close_range_penalty` and the initiative variant beside them — are applied.

**`close_range_penalty` costs a dodger two dice against a ranged attack made from inside Close
Range, and it works out on its own which attacks those are.** A Ranged Weapon is one by p.75's own
row; a Power is one when its Ch.2 Range is `ranged`, which is read off the sheet. A fist or a sword
never is. **The one thing it cannot see is a thrown weapon**, because a fight here has no equipment
in it: send `"close_range_only": true` on the attack for the page's own exception — an ordinary
thrown weapon or anything else that only works up close — and the dodger keeps their dice. Leave it
off for a gun.

**`hard_targets` needs you to say which combatants are hard.** Send `"hard_target": true` on a
combatant — a machine, a vehicle, a thick inanimate object — and every **passive** defence of
theirs answers at twice its rank while that setting is on. Nothing derives it: no character sheet
says a character is a machine, and the same battlesuit is a vehicle in one game and a person in
armour in another. An attacker may aim at the weak points instead by sending
`"vulnerable_part": true` on the attack, which costs four dice and cancels the doubling for that
one shot. Whether the thing is complex enough to *have* a weak point is your call, and the ledger
line says so. p.80's advice that vehicle-scale weapons and the strongest characters should carry
the Penetrating Pro is named on the ledger and **not applied**: it is about how a character is
built, which is the other server's question.

**No spend refuses by name any more, and that is a claim held to the engine.** Every `kind` either
`spend_resolve` or `spend_adversity` accepts is resolved: `extra_dice`, `reroll`,
`seize_initiative`, `instant_recovery`, `avoid_fatal_damage`, `stabilise`, `keeping_hold`,
`knockback`, `luring` and `team_attack` for a Hero, and `suppress_flaw`, `misfortune`, `villainy`
and `anything_resolve_can` for the GM. **What is still answered with a `not yet implemented` line
is what `anything_resolve_can` may *name*** — four of a Hero's ten purchases, in the last table of
this document, which is the one place to read that from.

That does not make every spend a spend that always happens. A resolved spend still refuses on the
ledger when the fight is not in a state for it — no roll on the table, nobody down under an effect,
the wrong kind of character, a limit already used, a pool that cannot cover it — and those lines
say what was wrong and leave the pool alone. **A refusal is not a `not yet implemented`**: the
first means the rules said no here, and the second means this engine has not got the rule. Narrate
neither as though it happened.

**`suppress_flaw` buys a Villain, a Foe or an Extra out of one of their Flaws for the rest of the
scene, and takes a `narration`.** Say which Flaw, in `narration`, or the spend is refused with
nothing spent — this engine holds no Flaws of its own, so an unnamed one would put a suppression of
nothing in particular on the ledger. **What the point buys and what you narrate are different
halves and the ledger line says so**: an NPC's Flaws bite when the opportunity presents itself and
the NPC cannot choose when, which is your judgement and no roll of this engine's, so what the
character is saved from is yours to tell. What the engine has recorded is the point leaving the
pool and the suppression itself, which comes back on the public state as `flaw_suppressed` and
refuses a second purchase against the same character — p.85 allows one per character per issue,
**counted over this fight**: an issue is several scenes and a fight is the largest thing this server
can see, so a second scene starts the count again and keeping track across an issue is yours, the
same as `villainy`. The lines say so, so you never have to work it out from a refusal.
A Hero or a group of Minions is refused: the page names three kinds and those are not among them.

**`misfortune` throws a piece of bad luck at the Heroes, and takes a `narration` too.** Say what the
misfortune is — a weapon jams, a stray shot endangers civilians, a Hero's mask comes off — or it is
refused with nothing spent. **The point leaving the pool is not most of this rule, it is the whole
of it**: p.85 gives a misfortune no roll, no threshold, no duration and no way of resisting one, so
the engine records the purchase and your words and nothing else, and the ledger line says so. Two
things the page asks of one are yours to honour: it should be a challenge and a complication rather
than a punishment, and never a heavy-handed plot device — that is what `villainy` is for. It is the
one spend aimed at a side rather than at a character, so it needs no `actor`. A fight with no Hero
in it cannot buy one either way: the pool opens at a point per Hero plus the Challenge Level times
the same number, so such a fight opens on nothing and the spend is refused for want of a point.

**`villainy` has a Villain automatically do whatever the story needs, once per story, and takes a
`narration` as well.** Say what the act is — the switch thrown, the hostage taken, the escape — or
it is refused with nothing spent. **Only a Villain**: p.85 says Foes and Minions lack what it takes,
and naming one is refused. **The story is the encounter**, because a story is a unit Chapter 5
defines nowhere and a fight is the largest thing this engine can see — so the second purchase in
one fight is refused, the count comes back on the public state as `villainy`, and it does *not*
follow you into the next scene of the same story: keeping track across scenes is yours. The act
succeeds without a roll and there is no ledger line for one, because the page attaches none. And
the page's warning is worth passing on: used often, it tells the players their choices did not
matter.

**`narration` belongs to those three and nowhere else.** An empty one says exactly as much as no
one and is refused the same way, with nothing spent. A long one is carried whole onto the ledger
line — there is no cap, because the sentence is the record. And one sent on a `spend_resolve`, or on
`anything_resolve_can`, is **ignored**: those purchases are decided by the rules and the dice, the
engine has nothing to do with your words, and nothing you write there reaches the ledger or the
state. Narrate them in your own message instead.

**`luring` is the one spend that takes a `target`**, because p.79 lets a dodged attack be sent into
a person rather than into the scenery — and a person is the only thing this engine has to send it
into. A lure naming nobody is refused, with nothing spent: there is no scenery here to strike.

**`team_attack` is bought off an attack that said it was one.** Send `"team": true` on the attack
and p.79's +2d is in the pool; the point afterwards makes that roll's sixes explode, and keep
exploding while they keep coming up. A target may be team-attacked once a battle, and the two ways
p.79 lifts that — the Heroes being clever about it, or the GM ruling otherwise — are a person's
call, not this engine's: where the GM has ruled otherwise, attack without the flag. What is *not*
applied is the coordination, because a turn here is one character's action: the participants
waiting until the end of the page, and all of them having to name the same enemy, are yours to
keep track of.

**What `anything_resolve_can` may name** — the ten above are a *Hero's* purchases, and the GM's
pool does not yet buy all ten. p.85's first purchase is the Resolve purchases with different money
behind them, and the ones this engine runs from the GM's pool are the ones marked bought below; the
rest still charge the buyer's own pool, which an NPC has none of, so they are recognised and
answered with a `not yet implemented` line, exactly like the table above. **The two columns
disagreeing with the engine is the failure this table exists to prevent** — an earlier version of
this document advertised every purchase as the GM's, four of them refused, and a model reading it had no way to tell
a purchase that had happened from one that had not:

| `as_resolve` | What the GM's pool does with it |
|---|---|
| `extra_dice` | bought |
| `reroll` | bought |
| `seize_initiative` | not yet implemented |
| `instant_recovery` | not yet implemented |
| `avoid_fatal_damage` | not yet implemented |
| `stabilise` | not yet implemented |
| `keeping_hold` | bought |
| `knockback` | bought |
| `luring` | bought |
| `team_attack` | bought |
