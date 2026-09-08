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
not take, a side nobody set, two characters whose sheets disagree about the house rules. It is never a finding about the character — whether a character is
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
`as_resolve`, because a point spent on nothing in particular is a point spent on nothing. **It runs
all ten of a Hero's purchases**, which the last table of this document lists and holds to the engine
— read it there rather than from this sentence.

**For an NPC, and never for a Hero.** p.85 spends the GM's pool "on behalf of any NPC whether
they're Villains, Foes, Minions, or Extras", and a Hero named as the `actor` of an
`anything_resolve_can` is refused with nothing spent. The two pools are the whole of that side of
the economy: a Hero buys their own dice with their own Resolve, and a point of Adversity that
bought one for them would be the GM paying to help the party. **A group of Minions is on p.85's list
and four of the ten still refuse one**, because p.73 gives a Minion group no Edge and p.77 gives it
no Health — the last table's second paragraph says which four and why.

## Whose game is this: the table comes with the sheets

**Hand this server the characters as their campaign exported them and the house rules come with
them.** A stored character carries a `CampaignTable` — the ten optional Gritty Combat Rules, the
GM's alternative to seizing the initiative, Checking Your Swing, the optional Edge roll and a
raised Gear Limit — written onto it when it joined a game. That block is the only route a house
rule has into a fight: **this server holds no account and cannot resolve a campaign**, by design,
so a Hero fought without its own sheet is a Hero fought under the book.

**Which spelling.** The block is `CampaignTable`, spelled the way the rest of a character is,
because a combatant's `character` is read by the same strict reader the character server uses. The
`.json` **export** spells the same block `campaign_table` — that is a different document with a
different convention, and a sheet carrying it is refused `CHARACTER_UNREADABLE` rather than read as
a character at no table. That refusal is the point: a house rule quietly dropped is a fight measured
under the wrong game with nothing in the answer to say so.

**Four cases, and every one of them is either on page one or in the refusal:**

- **The sheets agree.** The fight is resolved under what they carry, and page one says so.
- **The sheets disagree.** Refused, `TABLE_DISAGREES`, naming both characters and the first setting
  they differ on. Two blocks that differ are two contrary claims about which game is being played,
  and taking either would measure a fight under rules half the characters in it were not built for.
  Export them from the same campaign, or fight them under a table you pass yourself and sheets that
  carry none.
- **One of them carries none.** Accepted, and page one names the character that brought nothing.
  An absent block is silence rather than a contrary claim, so a campaign's Hero against a Villain
  built in the sandbox is an ordinary fight and not an error. **Do not narrate that character as
  having agreed to the house rules**; they are being fought under somebody else's.
  **And read the sentence page one prints, because there are two of them.** A character naming no
  campaign was built outside any game. A character naming a campaign and still carrying no block is
  a different thing — either that game adopted nothing, or the copy was taken before it did, and
  this server cannot tell which. Page one says so and names the campaign. **Do not report that
  second one as being at no table**: it may be a player's Hero from a game whose rules never
  travelled, and it has just been fought under somebody else's.
- **You also pass a table yourself.** It has to agree with the sheets switch by switch. Agreement
  is fine and comes back as both. A disagreement is refused, `CALL_TABLE_DISAGREES`, naming the
  setting: neither is quietly preferred, because whichever won, the other is a setting somebody
  chose and this server threw away.

With nothing on the sheets and nothing passed, the fight is the book as printed, which is what it
has always been.

**Where the table came from comes back inside `table`, as `source` and `source_note`** — `book`,
`call`, `sheets`, or `sheets_and_call`. Quote it with the rest: two runs whose switches read alike
may have got them off the characters or off an argument somebody typed, and a reader deciding
whether a figure is about *their* game needs to know which. `run_encounters` answers with no ledger,
so for a measurement that echo is the only place it is written down.

**A character's `ImmortalityCost` is not read here and never will be, and this is the one place to
say so.** It is a *price* — Ch.2 p.31 hands Immortality's cost to the table — so it changes what a
character costs and whether it fits a budget, which is the character server's question and not this
one's. Two combatants whose sheets name different prices for it is not a disagreement about
anything a fight can see, and this server does not look.

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
  the table echoed back. Encounters are held in memory by id, for this session only. **The switches
  are optional and are usually the sheets'** — see *Whose game is this* above; passing them as well
  is fine only where they say the same thing.
- **`take_turn`** — an `encounterId` and **one** `intent`. Acting and rolling are one call: there is
  no separate "roll" step, because an intent is a request and the engine decides what it produces.
  Answers with the ledger lines that step added and the public state.
- **`run_encounters`** — the same setup, `visibility` included, plus `runs`, `policy` and
  `maxPages`, run headless. That
  last one is the sharpest of the camelCase pair above: the answer prints the page limit back in
  snake_case, and sending it that way sends an argument the schema has not got. Its echoed table
  carries where the settings came from, so a rate quoted off it is reproducible from its own answer.

A combatant is either a character sheet — the shape the character server's `creation_guide`
describes — with a `kind` of `hero`, `villain`, `foe` or `extra` and a `side`, or a group of
Minions with a `threat_rank`, a `count` and a `side`. Either may also carry a `size` and an
`invisible` flag; see **Modifiers** above. **`kind` and `side` are yours to say and
nothing derives them**: the Hero/Villain flag on a sheet is presentation, the same sheet is a
Villain in one GM's game and a Foe in another's, and Chapter 4's tie-break ladder is about
precedence rather than teams — a fight between Heroes is a fight the book prints.

**A sheet may also carry its campaign's house rules, and that is how a fight learns them** — see
*Whose game is this* above.

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
visibility were the last three on that list and are applied now — see **Modifiers** below.

**`Encounter.SwitchesNotYetApplied` is empty** too, and it is the same kind of claim. `RaisedGearLimit`
and `GearLimitRank` were the last two on it. p.80 caps the Trait rank you can bring to bear *when
using mundane equipment* and points at Chapter 6 for the detail; until Chapter 6 was extracted a
fight had the ceiling and no Weapon Bonus for it to bite on, so neither the raised limit nor the
default one could honestly be applied. Both are applied now — see **The Gear Limit** below — and
`raised_gear_limit` moves the numbers, so a run that turns it on is a run whose figures carry it.

**Every gritty rule is applied**: `fatal_damage`, `tough_minions`, `wound_penalties`,
`active_defenses_cost`, `hard_targets`, `close_range_penalty`, `the_drop`, `friendly_fire`,
`slow_healing` and `raised_gear_limit`, with the initiative variant beside them.

**One clause of one applied rule is still declined, and it is the only thing in this engine that
writes the phrase.** p.77's Minion group bonus is applied; the sentence narrowing it — that the
bonus does not count towards penetrating cover or harming somebody behind Armor or a Force Field —
is not, because the entry's own `ambiguity` says both are decided by the very attack roll the bonus
is granted to and the page offers no mechanism for two totals against one defence roll. A group's
attack says so on its own ledger line. Do not narrate that narrowing as though it happened.

## The Gear Limit

**An attack that names a held item is capped.** Send `item` on an attack and the actor's Trait rank
is capped at the Gear Limit in force — 6d by default, or whatever the campaign raised it to — and
the weapon's bonus dice are added to what is left. A 10d Might swinging a basic sword rolls 8d, and
the ledger line says the limit, the rank before and after, and which printed weapon the item was
matched to.

**It reaches the two weapon rows of p.75's table and no others.** `melee_weapon` and `ranged_weapon`
are mundane equipment being used. `physical_power` and `mental_power` roll a Power's own rank, which
is not equipment, and `unarmed` is a fist by the table's own word — each writes a line saying which
silence it is. **p.87's close-combat exception is yours to take, not the engine's**: where a
bare-handed rank beats what the limit lets a weapon carry, the page lets the wielder roll the
bare-handed one, and the way to say so is to send the attack as `unarmed` with the item still in
hand and the damage kind the weapon buys.

**The limit in force rides back inside `table`.** `start_encounter` and `run_encounters` both echo
`table.gear_limit` — the rank an item-backed attack is actually capped at, whether the campaign
raised it, and where the figure came from. It is echoed as a figure because the two switches beside
it say only that a rank was *set*, and a reader deciding whether a rate is about their game needs
the number the fight was resolved under. A rank stored without `raised_gear_limit` is a figure the
table has not adopted and the echo reports the default.

**An item Chapter 6 does not print gets no bonus and the line says so.** The object a fight opens
with is your own phrase, so it is matched to the longest printed weapon name inside it — "a basic
sword" is the Sword, "a battle axe" is the Battle Axe rather than the Axe. A rolled-up newspaper is
capped and adds nothing, and what a Weapon Bonus for it would be is yours to decide.

**Only the attacker's side of Chapter 6 is applied, and the defender's is reported.** p.88 also adds
a melee weapon's bonus to Agility or Martial Arts *when defending against close combat attacks*, and
p.87's ceiling is written about applying a Trait rather than about attacking with one. This engine
does neither on the defending side: a defender holding a printed melee weapon rolls the Trait as it
stands, and a ledger line citing `weapon_bonus` says so, because p.87's exception is offered for a
defense rank exactly as for an attack rank and whether it is taken is the wielder's. **Do not narrate
a defender's weapon as having helped them.** Mundane armour is not reachable at all: a combatant
carries no gear, so no defence in a fight here is item-backed.

**Say a weapon's name the way you say it, not the way the table files it.** The three tables are
alphabetical, so eight rows are printed inverted — `Rifle, Sniper`, `Pistol, Snub`, `Shield,
Spiked`, `Shotgun, Automatic`, `Blast Rifle, Military` and the three grenades — and both spellings
are matched, so "a sniper rifle" is the sniper's rifle at +4d rather than the plain Rifle at +3d. A
hyphen counts as a space. A plural does not: "pistols" matches nothing and is reported as an item
Chapter 6 does not print, so name one weapon in the singular. **And a phrase naming two printed
weapons of equal standing adds nothing at all** — "his shield and dagger" is a question about which
of them is being swung, so the line names both and leaves the figure to you.

**`slow_healing` is half a rule about the days after a fight, and page one of every run that takes
it says which half the fight is carrying.** Inside a scene: nobody heals on regaining consciousness
after a defeat, so `instant_recovery` brings a character round on the Health they went down with; a
character in that condition **may be walking around at or below zero**, which the state reports and
the turn order includes; and any damage at all puts them straight back down. Outside it, and named
on the ledger rather than left silent: the daily healing rate by Toughness, the loss of the
after-battle healing roll, and the Medicine Talent's once-a-week limit. Those are yours to keep
track of between scenes — do not narrate a character healing here.

**A `grab` needs you to say what is being grabbed, and a full one puts it in the winner's hands for
the page.** p.76 aims a grab at an object and a hold at a person, so `"move": "grab"` takes an
`"item"` — any words you like — and a grab naming nothing is refused with nothing rolled, because a
grab of nothing is the page's own definition of a hold. A partial grab records what the two of them
are fighting over and comes back on the state under `grapples`; a full one moves it, and the winner's
`holding` says what they have, the page they took it on, and whether they have swung it yet.

**And the target has to be holding it, which means saying so when you open the fight.** p.76 takes
an item "away from your opponent", so a grab is aimed at something somebody has — and this server
has no inventory to look one up in, so a combatant at `start_encounter` takes a `"holding"` field:
the one handheld thing that character walks in with, in your own words. **A grab for an item its
target is not recorded as holding is refused with nothing rolled**, and the refusal says what they
*are* holding. So a fight opened with nobody carrying anything is a fight in which no grab can
land — if the Villain has a sword, say `"holding": "the sword"` on the Villain. An item somebody
walked in with reports `carried_in: true` and no `won_on_page`, because p.76's one page is a limit
on what a grab wins and nothing takes a weapon off somebody who simply brought one.

**While a partial grab stands, either of them may go on rolling for the contested object.** That is
the page's own way out — "you each get to make opposed Might rolls on your turn to act to try
gaining control" — and it is the one grab this server allows for an item the target is not holding,
because a half-measure leaves the object where it was and the character who owned it is rolling
against somebody who has not got it either.

**What the page gives the winner is one page, and this server applies it as state.** Send
`"item"` on an attack to use it, or the `toss` intent to throw it away — either is refused by name
unless that is exactly what the actor is holding — an opening `holding` and a full grab are the two
ways anything reaches anybody's hands here, and an item this server does not know about is one you
would otherwise be conjuring into the fight by naming it. **Neither spends the turn**: that is p.76's "in effect, a
free action", and the actor still has their ordinary action afterwards. Anything still held at the
end of the page it was won on is tossed aside and nobody has it — an item that *was* used stays
where it is, and what becomes of it after that is yours. **And a character who has been put out of
the fight lets go of whatever they are holding when the page turns**, whoever won it and whenever:
nobody can grapple a defeated target, so an object left on a body would be out of the fight for
good. Where it landed is yours.

**Three things about a grab are yours and the ledger says which.** What losing the item means for
the loser's own attacks: no figure of theirs is changed, because an attack here names a Trait and
nothing in this repository's rules data says which Trait a weapon backs — so if the sword mattered,
say so yourself. Where a tossed item landed, for the same reason there is no scenery here. Naming an item on an attack **changes no figure at all**: not the
pool, not the row of the table, not the damage. Do not narrate it as a bonus.

**Either of them may end a partial grab by letting go: send `toss` naming the contested object.**
p.76 prints that exit in the same paragraph as the deadlock, and it works for the character who is
not holding the thing as well as for the one who is — the grab record goes and both of them have
their active defences against everybody else back. Nobody comes out of it in control of the object:
only a full grab is that. Left alone, a partial grab ends only when somebody rolls three net
successes, and until then neither character can dodge anybody but each other.

**While a partial grab stands, neither of them may attack with the contested object.** That is
p.76's "they can't use it, but neither can you", and an attack naming it is refused by name for
either party, with nothing rolled — including the one who walked in with it, who is still recorded
as holding it. Attacking with anything else is untouched: what the deadlock takes is the use of that
object and both characters' active defences against anybody but each other.

**`friendly_fire` needs nothing from you.** Whether a target is "engaged in close combat or
otherwise bunched up" is worked out from the range bands you already set: anyone at Close Range
with the target who is not you is somebody a stray round can find. A ranged attack into that costs
four dice, and **a shot that lands nothing sends a second attack, resolved for real** — the GM's
random pick comes off the same seeded dice as everything else and the die face is on the ledger, so
the choice is reproducible and auditable. That second attack is the same weapon at a different
person: what you declared about the first shot — cover, all-out, charge, area, team, a weak point —
was about that target and does not travel. Read its outcome off the ledger like any other attack;
do not narrate the stray round as a miss.

**`the_drop` needs you to say who has a weapon or Power aimed and ready.** Send `"ready": true` on
a combatant and their effective Edge is doubled for the order of action, against everyone who has
not — which is exactly p.79's rule, because an order only ever compares two characters at a time
and two ready ones double alike. Nothing derives it: carrying a gun is a capability and having it
levelled is a state, and p.79 gives you the final say over the whole rule in as many words. **The
other half of the page is not applied and the ledger says so**: a shooter also has the drop on
anyone closing to engage them, which is held against one opponent and not the rest, and one order
of action cannot carry that. Keep track of it yourself, the same as a team attack's coordination.

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
and `anything_resolve_can` for the GM. **And what `anything_resolve_can` may *name* is now every one
of the ten too** — the last table of this document says so, row by row, and is the one place to read
that from. **Nothing a spend can produce carries a `not yet implemented` line any more**, and no
table setting does either. What still does is one clause of one applied rule: p.77's Minion group
bonus, above.

That does not make every spend a spend that always happens. A resolved spend still refuses on the
ledger when the fight is not in a state for it — no roll on the table, nobody down under an effect,
the wrong kind of character, a limit already used, a pool that cannot cover it — and those lines
say what was wrong and leave the pool alone. **A refusal is not a `not yet implemented`**: the
first means the rules said no here, and the second means this engine has not got the rule. Narrate
neither as though it happened.

**`points` is a whole number of points and at least one.** Only `extra_dice` reads it for anything
but the price — a point buys a die, so three points buy three — and every other purchase charges the
price its own page prints. A spend of none or of fewer than none is refused with nothing spent and
nothing bought: it used to take a purchase out of a pool that could not pay for it, and a negative
one ran the arithmetic backwards and *added* to the pool. Send the number of points you mean, or
leave it out and get one.

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
pool now buys **all ten**. p.85's first purchase is the Resolve purchases with different money
behind them, so the point leaves Adversity, the NPC's non-existent Resolve is never touched, and
every purchase keeps the limits its own page prints: a seize lasts the rest of the fight and doubles
an Edge instead where the GM has taken the alternative, an instant recovery is once a scene and is
refused to somebody still bleeding out, the Fatal Damage rescue stabilises into the bargain, and a
stabilise needs somebody actually on the clock. **The two columns disagreeing with the engine is the
failure this table exists to prevent** — an earlier version of this document advertised every
purchase as the GM's while four of them refused, and a model reading it had no way to tell a
purchase that had happened from one that had not. The table stays now that every row reads alike,
because that sameness is the claim, and a row that stops being true has to fail somewhere:

**A group of Minions is the one NPC four of them refuse.** p.85 spends on behalf of any NPC and a
Minion group is on its list, but four purchases have nothing for one when it arrives, so each
refuses by name and spends nothing. `seize_initiative`: p.73 gives a Minion group no Edge to double
and has them act after everyone else, so neither form of the purchase moves them. `instant_recovery`,
`avoid_fatal_damage` and `stabilise`: p.77 gives a Minion group no Health, so there is no defeat to
come round from, no fatal threshold to buy back from and no dying clock to stop — a group is
defeated by the bodies taken out of it, and an effect against one takes bodies too. Spend the point
on the Villain, the Foe or the Extra instead.

| `as_resolve` | What the GM's pool does with it |
|---|---|
| `extra_dice` | bought |
| `reroll` | bought |
| `seize_initiative` | bought |
| `instant_recovery` | bought |
| `avoid_fatal_damage` | bought |
| `stabilise` | bought |
| `keeping_hold` | bought |
| `knockback` | bought |
| `luring` | bought |
| `team_attack` | bought |
