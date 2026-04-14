# Prowlers & Paragons — Rules Extraction & Data Structure Guide

This document is the source of truth for how the P&P rulebook should be parsed, structured, and used by the character creation tool. Read this before writing any extraction or wizard logic.

---

## Project Goal

Build a CLI character creation wizard for the tabletop RPG *Prowlers & Paragons*. The wizard guides a user (player or GM) through the full character creation process, tracks point budgets, validates choices against system rules, and outputs a completed character sheet.

The same creation rules apply to heroes, villains, and henchmen — there is no mechanical distinction. Villain and henchman support is therefore achieved by completing the core wizard.

---

## Step 1: PDF Extraction Strategy

The rulebook PDF is the single source of truth. Extract rules data **once** into structured JSON files that live on disk. The wizard and all downstream logic reads from these files — do not embed raw PDF content into prompts.

### Extraction approach

- Focus on the **first two chapters** first — this covers core mechanics, traits, and creation rules
- Use a PDF extraction library (PyMuPDF / pdfplumber recommended)
- Extract text chapter by chapter, not as one giant dump
- After extraction, parse into the JSON schema defined below
- Store extracted JSON in `/data/rules/`

### Files to produce

```
/data/rules/
  meta.json
  tiers.json
  creation_rules.json
  abilities.json
  talents.json
  powers.json
  pros.json
  cons.json
```

---

## Step 2: The JSON Schema

### `meta.json`
```json
{
  "system": "Prowlers & Paragons",
  "edition": "<extract from PDF>",
  "extracted_version": "1.0",
  "notes": ""
}
```

---

### `tiers.json`
Power tiers define the total point budget and rank caps for a character. Extract all tiers from the rulebook.

```json
[
  {
    "id": "street_level",
    "name": "Street Level",
    "point_budget": 40,
    "max_rank": 4,
    "description": "..."
  }
]
```

---

### `creation_rules.json`
The sequence and constraints of character creation.

```json
{
  "point_budget_by_tier": {
    "street_level": 40,
    "heroic": 60
  },
  "sequence": [
    "choose_tier",
    "buy_abilities",
    "buy_talents",
    "buy_powers",
    "apply_pros_cons"
  ],
  "global_caps": {
    "notes": "Any global rank caps or spend limits go here"
  }
}
```

---

### `abilities.json`
The six base stats (equivalent to D&D ability scores). These are the foundation — some powers and talents reference ability ranks directly.

```json
[
  {
    "id": "might",
    "name": "Might",
    "cost_per_rank": 1,
    "max_rank": 10,
    "description": "..."
  }
]
```

---

### `talents.json`
Skills. Most talents link to a base ability — rolling a talent uses that ability's rank as the dice pool foundation.

```json
[
  {
    "id": "athletics",
    "name": "Athletics",
    "linked_ability": "might",
    "cost_per_rank": 1,
    "max_rank": 10,
    "description": "..."
  }
]
```

---

### `powers.json`
The most complex section. Each power has a base cost and scales by rank. Each rank adds 1d6 to the dice pool.

Some powers have **ability prerequisites** — their maximum rank is derived from a base ability rank (e.g. "max rank = half your Might rank"). Capture this relationship explicitly.

```json
[
  {
    "id": "strike",
    "name": "Strike",
    "category": "Attack",
    "cost_per_rank": 2,
    "max_rank": 10,
    "dice_per_rank": 1,
    "description": "...",
    "prerequisite": null,
    "available_pros": ["extended_range", "area_effect"],
    "available_cons": ["weaponry", "activation"],
    "tags": ["melee", "damage"]
  },
  {
    "id": "half_might_example",
    "name": "Example Ability-Gated Power",
    "category": "...",
    "cost_per_rank": 2,
    "max_rank": 10,
    "dice_per_rank": 1,
    "description": "...",
    "prerequisite": {
      "ability": "might",
      "relationship": "half_rank",
      "description": "Max rank equals half your Might rank, rounded down"
    },
    "available_pros": [],
    "available_cons": [],
    "tags": []
  }
]
```

**Known prerequisite relationship types to watch for during extraction:**
- `half_rank` — max rank = floor(ability rank / 2)
- `equal_rank` — max rank cannot exceed ability rank
- `minimum_rank` — ability must be at least a certain rank to unlock the power

Add others as discovered in the PDF.

---

### `pros.json`
Enhancements that increase a power's (or trait's) cost but expand its narrative or mechanical scope.

```json
[
  {
    "id": "extended_range",
    "name": "Extended Range",
    "cost_modifier": 1,
    "applicable_to": ["powers"],
    "description": "...",
    "narrative_constraint": null
  }
]
```

---

### `cons.json`
Limitations that reduce a power's (or trait's) cost but constrain when or how it can be used. The `narrative_constraint` field is important — it captures the fictional premise of the limitation (e.g. "only works with a specific weapon").

```json
[
  {
    "id": "weaponry",
    "name": "Weaponry",
    "cost_modifier": -1,
    "applicable_to": ["powers"],
    "description": "...",
    "narrative_constraint": "Power only functions with a specific weapon or item. Player must define the item at creation."
  },
  {
    "id": "activation",
    "name": "Activation Required",
    "cost_modifier": -1,
    "applicable_to": ["powers"],
    "description": "...",
    "narrative_constraint": null
  }
]
```

**Note:** Not all pros/cons apply to all trait types. Some apply only to powers, some may apply to talents or abilities too. The `applicable_to` array should reflect what the rulebook actually allows — do not assume broad applicability.

---


## Step 3: Project Architecture

### Three-layer separation — enforce this from day one

The codebase must be structured in three distinct layers. This is not optional — it is the decision that makes a future GUI possible without rewriting everything.

```
/data/rules/        <- extracted JSON (rules as data, no logic)
/engine/            <- all rules logic: budget tracking, validation, prerequisite checks
/cli/               <- presentation only: prompts, input handling, output formatting
```

The engine must never know or care whether it is talking to a terminal or a GUI. It exposes functions that the CLI (or any future frontend) calls. If engine code contains print statements or input() calls, that is a bug.

Example boundary:

```
engine/
  character.py      <- character state, point tracking
  validator.py      <- prerequisite checks, budget enforcement
  loader.py         <- reads JSON from /data/rules/
  calculator.py     <- cost calculations, net power cost with pros/cons

cli/
  wizard.py         <- step-by-step prompts, calls engine, displays results
  formatter.py      <- formats character sheet for terminal output
```

---

## Step 4: Wizard Flow (CLI v1)

Build the wizard after the JSON data is extracted and validated. The wizard calls the engine — it does not touch rules data directly.

### Creation flow

```
1. Choose tier           -> loads point budget + rank caps from tiers.json
2. Buy abilities         -> present 6 abilities, cost per rank, track spend
3. Buy talents           -> present talent list, link to chosen ability ranks
4. Buy powers            -> present power list, enforce prerequisites, track spend
5. Apply pros/cons       -> for each chosen power, present available modifiers
6. Review + confirm      -> show full character summary, remaining points
7. Output sheet          -> write to /output/<character_name>.json + .md
```

### Point budget tracking

- Maintain a running `points_spent` counter throughout
- Show remaining points at each step
- Validate on every choice — do not allow overspend
- Warn if a prerequisite ability rank is too low for a chosen power

### Output format

Produce two files per character:

**`<n>.json`** — machine-readable, full stat block
**`<n>.md`** — human-readable, table-formatted character sheet for use at the table

---

## Future: GUI (Post All-Chapter Extraction)

**Do not build a GUI until all rulebook chapters are fully extracted and the CLI wizard is complete and validated against real characters.**

The GUI is explicitly out of scope until:

1. All chapters from the PDF are extracted into `/data/rules/`
2. The CLI wizard handles the full creation flow without errors
3. At least one hero and one villain have been created end-to-end and verified against the rulebook manually

When that milestone is reached, the clean engine/cli separation means adding a GUI is straightforward — a web frontend (Flask/FastAPI + browser) or desktop wrapper (Tauri) can call the engine directly without touching any existing logic. That work belongs in a new `/gui/` layer alongside `/cli/`, not replacing it.

---

## Key Rules Reminders

- **All dice are d6.** Ranks = number of d6s in the pool. Count successes.
- **Traits = Abilities + Talents + Powers.** These are the three categories of everything a character can have.
- **Pros increase cost, cons reduce cost.** Net cost of a power = `base_cost_per_rank + sum(pro modifiers) - sum(con modifiers)`, multiplied by rank purchased.
- **Cons are narrative commitments**, not just mechanical discounts. The wizard should prompt the user to define the fictional constraint when a con is selected (e.g. "Weaponry — what is the specific weapon?").
- **Heroes and villains use identical rules.** The wizard serves both.

---

## What to Do If the PDF Is Ambiguous

- Prefer the most literal reading of costs and caps
- Where a rule seems incomplete, add a `"needs_review": true` flag to that entry in the JSON
- Do not invent rules — flag the gap and surface it in the wizard as a prompt to the GM
