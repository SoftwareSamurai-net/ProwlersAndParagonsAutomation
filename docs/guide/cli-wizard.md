# The terminal wizard

Read before touching `cli/`. The wizard is the oldest front end and the only one with no harness.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Wizard flow

`WizardOrchestrator.Run()` iterates `_steps` in order, rendering the HP budget panel before each step:

1. `ChooseTierStep` — selects tier and optional package
2. `BuyCharacteristicsStep` — abilities, talents, powers (via `PowerBrowser` + `ProConSelector`), flaws
3. `ChooseGearStep` — gear: Chapter 6's catalogue or free text, no HP cost either way
4. `CalculateDerivedStep` — displays computed Edge and Health
5. `FinishingTouchesStep` — name, appearance, motivation, quote, connections
6. `GmReviewStep` — full validation, sheet display, `.txt` **and** `.json` export to `output/`

Steps 1–5 render a Back/Continue prompt (`WizardOrchestrator.PromptNavigation`); `gm_review` is the terminus and breaks the loop.

## The wizard shows the Trait Cap in force, and has no step that sets one

**A house Trait Cap is a ceiling a table imposes, tighter than the tier's** — `CharacterSheet.TraitCapRank`, null meaning "the tier's". The wizard cannot set one: there is no step for it, and there is deliberately not going to be, because a cap is a fact about a game rather than about a character and the two places it arrives from are joining a campaign in the browser and `build --from --trait-cap` (see [`mcp-and-headless.md`](mcp-and-headless.md)).

**It still has to show one.** A character built in the browser and exported, or handed to `build --from` and read back, arrives carrying a cap — so the budget panel above every step (`HpBudgetDisplay`), the Powers browser's rank prompt (`PowerBrowser`) and both rank prompts on the Abilities and Talents step (`BuyCharacteristicsStep`) all read `DerivedStatsCalculator.EffectiveTraitCap` rather than `tier.TraitCapRank`. All four were reading the tier's and printing it as the character's, which is a prompt bounded by one number over a verdict reached with another — and the panel's own "Trait Cap: 12d" would have been the one figure on screen that nothing else in the program agreed with. `TraitCapReadTests` is the guard.

The tier list on step 1 (`ChooseTierStep`) still reads each tier's own cap, and should: those are the six tiers a person is choosing between, and a card printing some character's house cap would be describing the wrong thing.


## The Gear step picks from the book, and still takes anything you type

`ChooseGearStep`'s menu opens with **"Pick from the book (Ch.6)"** beside the free-text entry it
always had. Both are right and neither replaces the other: p.91 calls its own list "examples, not a
catalogue of prices", so a character may carry a letter from their mother, and mundane gear is free
whichever way it arrives.

- **The 108 rows are grouped by the book's own headings first**, because one page of a 108-row
  `SelectionPrompt` is unusable. The groups are the armour and weapons tables by era, plus p.91's
  equipment — the book's divisions, not a taxonomy invented here.
- **An armour row is offered with the rank it would grant *this* wearer.** That is the figure
  neither page prints — p.88 gives Toughness plus the suit's bonus, p.87 caps the Toughness half at
  the Gear Limit — so which suit is worth taking depends on the character, and a line showing only
  the printed bonus would leave the choice unmade. A shield says what its die is for, because the
  weapons table prints only the half you get by swinging it.
- **Nothing here buys a Power.** The Armor rank is reported; `sheet.SelectedPowers` is untouched.

**`CatalogueLabel` is the one part of this step a test can reach**, and `ChooseGearStepTests` says so
in as many words rather than implying the step is covered. The wizard has no harness — every step
drives `AnsiConsole` directly — so the menu and the two prompts are unreachable from a test here,
exactly as they are for the other five steps. The equivalent surface is driven end to end in the
browser suite's `GearCataloguePickerTests`.
