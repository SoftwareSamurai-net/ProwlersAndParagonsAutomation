# Handover: toxin Pros/Cons, custom gear features, and Vector

A task-scoped brief so a fresh session can start without re-deriving anything.
**Delete this file when the three tasks are done** — [`PROGRESS.md`](../PROGRESS.md) stays
the permanent record.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. Everything below
assumes them.

---

## Getting the rulebook text back

The PDF is gitignored and lives at `docs/Prowlers_&_Paragons_Ultimate_Edition.pdf` (not in
the worktree — it is in the main checkout). Every line number in this document comes from:

```bash
pdftotext "docs/Prowlers_&_Paragons_Ultimate_Edition.pdf" rulebook_raw.txt
```

**No flags.** `-layout` produces different line numbering and interleaves the two-column
pages; do not use it for these sections. `pdftotext` ships with Git for Windows at
`C:\Program Files\Git\mingw64\bin\pdftotext.exe`.

Chapter starts, for orientation: Ch.1 176, Ch.2 278, Ch.3 2510, Ch.4 2662, Ch.5 2957,
Ch.6 3053, Ch.7 3812, Ch.8 4208, Ch.9 7809.

---

## Task 1 — three toxin Pros/Cons (small, certain)

**Rulebook: Ch.7 Toxins, lines 4090–4112.** These are the only Pros/Cons anywhere outside
Chapter 2 — verified by sweeping the whole book for `^(PRO|CON) [+-]\d+ Hero Point`, which
returns exactly these three. They were missed because the original extraction was scoped to
Ch.2.

| Name | Kind | Cost | Applies to |
|---|---|---|---|
| Caustic | con | −2 | **Stun** only |
| Lethal Disease | pro | +6 | **Slay** only |
| Non-Lethal Disease | pro | +2 | **Stun** only |

Note Non-Lethal Disease is Stun, not Slay — easy to assume otherwise given it sits next to
Lethal Disease.

They belong in `power_pros` / `power_cons` on `stun` and `slay` in `data/rules/powers.json`,
in the existing shape:

```json
{ "id": "caustic", "name": "Caustic", "cost_type": "flat",
  "cost_modifier": -2, "description": "..." }
```

Descriptions must be **original text written from the entry**, never rulebook prose — see
the descriptions policy in `CLAUDE.md`.

### What else must change

- `tests/.../CanonicalPowerProsCons.cs` — add the three rows.
- `PowerProConTests.TheRulebookPrints102PowerSpecificProsAndCons` → **105**.
- `PowerProConTests.SixtyOnePowersCarryAtLeastOne` → **62** (Slay gains its first; Stun
  already has EMP).
- `source_ref` on `stun`/`slay` currently cites Ch.2. Either leave it (the Power's own stat
  line is still Ch.2) or note the Ch.7 addition in `notes`. Prefer `notes`.

---

## Task 2 — custom gear features (medium)

**Rulebook: Ch.6 Custom Gear, lines 3247–3285.** Twelve headings, fourteen prices because
two are graded:

| Feature | HP |
|---|---|
| Accurate / Very Accurate | 1 or 2 |
| Bonded | 1 |
| Collapsible | 1 |
| Concealed | 1 |
| Deflecting | 2 |
| Fitted | 2 |
| Hardened | 2 |
| Masterpiece | 2 |
| Powerful / Very Powerful | 1 or 2 |
| Reinforced | 2 |
| Silenced | 1 |
| Upgraded | 2 |

Surrounding rules that matter:

- Custom gear **can also take ordinary Pros and Cons** (line 3284), and *"no piece of gear
  can cost less than 0 Hero Points"* — a floor at 0, not at 1 like Powers.
- Two-Fisted lets you customise **two identical weapons for the price of one**.
- Mundane gear itself is free and untracked (Ch.6 Resources, line 3057). The wizard's
  free-text gear step is correct — do not "fix" it.

### Suggested shape

A new `data/rules/gear_features.json` is the natural home, mirroring `pros.json`:
`id`, `name`, `cost_type` (`flat` / `flat_variable`), `cost_modifier` or
`cost_modifier_range`, `description`, `source_ref`.

Then a `SelectedGear` record (name + feature list + pros/cons) on `CharacterSheet`,
a `GearCost()` on `CostCalculator` flooring at 0, and `ChooseGearStep` gaining an optional
"customise this item" path. Keep free-text mundane gear as the default — most gear is free
and forcing a cost flow on it would misrepresent the rules.

Watch: this is the first thing to spend Hero Points from outside `TotalCost`'s current four
categories, so `TotalCost` and the HP budget panel both need it.

---

## Task 3a — the residuals pair up, and that is a clue (do this first)

The seven residuals are **−6, −1, −1, +1, +1, +2, +2**. Three matched pairs and one outlier.

An earlier reading called this noise. That was too quick. Repeated *identical* values are
what a shared cause looks like — both real bugs found so far announced themselves exactly
this way (the starting-package double-charge hit seven Heroes at an identical +4; Stronghold's
four Item Cons were exactly −4). Treat matching residuals as a signal to chase, not as
rounding.

The honest counterweight: seven values confined to an eight-point range will collide by
chance fairly often, so pairing alone is weak evidence. It earns an investigation, not a
conclusion.

### An experiment already run, with a real result

When a sheet writes `Super Senses (A, B, C) (Item: suit)`, the Con was recorded **once**,
on the reasoning that the rulebook treats Super Senses as a single Power. That was flagged
as arbitrary at the time. This project stores each option as its own entry with its own
cost and its own floor, so the other defensible reading is that each option carries the Con.

Applying the Con to **every** option in the group was tried:

| Hero | Before | After |
|---|---|---|
| Talon | +1 | **0 — exact** |
| Shadow | +2 | +1 |

No Hero got worse, and Talon closes to the point. The shift is only −1 rather than −3 per
group because most Super Senses options cost 1 HP flat, and a Power's floor of 1 stops the
Con biting.

**This was reverted, not shipped**, because it changes a modelling judgement rather than
fixing an outright bug, and it deserves a decision made on the rules rather than on the
scoreboard. Picking it up is small:

1. In `PrebuiltHeroes.ProsConsByHero`, add `["<Hero>|super_senses_<option>"] = ["con:item"]`
   for the remaining options in Shadow's and Talon's groups. Vigilant has a one-option
   group, so nothing changes for him.
2. Move Talon to the exact list in `BuildByHero` and in `HeroRebuildsToExactly125`; set
   Shadow's residual to 1.
3. `MostHeroesReconcileExactly` → **14**.

Decide it on the rules first: is a Con written once against a Super Senses group one Con on
one Power, or one Con per option bought through the item? Write the reasoning down either
way. If you keep the current reading, record *why*, so the next person does not re-run this.

### Pairs still unexplained after that

`T-Kay −1 / Vigilant −1` and `Herald (Scathach) +1 / Shadow +1`. Look for something the
members of each pair share. Known leads: Vigilant's Jo Sticks are *Upgraded* (a Task 2 gear
feature, worth +2, which would take him to +1 and pair him with the others rather than
resolve him); T-Kay's `Limited: only for Telekinesis` has no stated grade. Airmid's +2 has
no partner once Shadow moves, which weakens the pairing story for that one.

## Task 3b — Vector's −6 (investigation, timeboxed)

`PrebuiltHeroes.BuildByHero["Vector"]` is `("superhero_package", -6)`: the rebuild costs 119
where the sheet says 125. It is four times any other residual, so unlike the rest it is
worth one look.

**Leading hypothesis.** His sheet reads `Deflection (Physical and Energy) 10d (Exclusive)`.
Deflection's rulebook entry says *"Decide whether you can deflect physical attacks or energy
attacks when you select this Power"* — one type. Covering both is not free, and there is no
printed Pro for it. If the authors charged a second Deflection or an ad-hoc surcharge, that
is roughly the missing 6.

His full sheet, already transcribed in `PrebuiltHeroes.cs`: Attuned, Blink 10d (Exclusive),
Deflection 10d (Exclusive), Immunity (Illusions), Phasing (Exclusive), Regeneration
(Conditional: while awake), Telekinesis 6d (Exclusive), Expertise (Science) 12d; Contacts ×2;
gear is a mundane padded costume, which is free.

**Timebox it.** If no clean reading appears, leave the residual and say so. Do not tune an
ambiguous variant to force zero — `CLAUDE.md` says this and it matters more than the number.

---

## Ground rules for all three

- Every rules value gets a test, and fixtures like `CanonicalPowerProsCons.cs` are
  transcriptions of the book: if one fails, check the page, do not edit the fixture.
- Build and test at CI strictness before committing:
  `dotnet build --configuration Release -p:ContinuousIntegrationBuild=true` then
  `dotnet test --no-build --configuration Release -p:ContinuousIntegrationBuild=true`.
- Update `PROGRESS.md` in the same commit series, and delete this file when done.
- Currently **13 of 20** published Heroes rebuild to exactly 125 HP. Tasks 1 and 2 should not
  change that — no unreconciled Hero has a toxin Pro, and only Vigilant has a gear feature
  (Upgraded, +2, which moves him from −1 to +1). If a count moves unexpectedly, something is
  wrong.
