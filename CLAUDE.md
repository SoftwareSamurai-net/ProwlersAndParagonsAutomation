# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Read PROGRESS.md first, and update it before you finish

[`PROGRESS.md`](PROGRESS.md) is the single source of truth for what is done and what remains. Read it before starting anything so you do not re-implement finished work or re-verify locked data.

**Updating it is part of the task, not a follow-up.** Any change that finishes a piece of work, moves a headline number, or uncovers a new gap updates `PROGRESS.md` in the same commit series. Do not leave the reasoning only in a commit message — commit messages are hard to find six months later.

This used to live in two places (the README roadmap and a gaps list further down this file) and drifted out of step with the code. Both now point at `PROGRESS.md`. Do not reintroduce a second list.

## Name a pull request the way a changelog would

**Conventional Commits, and the subject says what the change *does*.** `feat: swap characters from
any step`, `fix: stop sign-out deleting an untouched draft`, `docs: record the owner's reports`,
`test:`, `refactor:`, `chore:`. The owner reads these as notifications — a title is often the whole
of what they see, so it has to carry the change on its own.

**What that rules out** is the shape this repository kept producing: a title that narrates the
session rather than the diff. *"Record the owner's three reports, and defer accessibility"* says
what somebody did for an afternoon; `docs: record reported defects and defer a11y work` says what
landed. Keep the body for the reasoning — it can be as long as the change deserves, and the entries
in `PROGRESS.md` are where the argument really lives.

## Where the rest of this lives

**This file was 1,431 lines. It is now the part that applies whatever you are working on**, and
everything else is in [`docs/guide/`](docs/guide/), one file per area. The rule that decided which
is this:

> **A rule stays here if breaking it costs work regardless of what you were doing. It moves to a
> guide if you can only break it while working on that area.**

So the stash rule and the break-it-and-watch-it-fail rule are here, because you can lose a day to
either while editing a JSON file. "No component names a colour" is not, because you cannot break it
without opening `web/`.

**The guides are not loaded for you.** This file is; they are not. So reading the guide for an area
**before** you touch it is a step you have to take, and nothing in them is optional background —
they are the same hard-won rules this file used to carry, and every one is there because something
went wrong once.

| About to touch | Read first |
|---|---|
| `engine/`, `sheets/`, `data/rules/*.json` | [`docs/guide/rules-engine.md`](docs/guide/rules-engine.md) |
| `web/` — any component or page, `app.css`, `theme.css` | [`docs/guide/browser.md`](docs/guide/browser.md) |
| the print stylesheet, `SheetView`, `SampleCharacters` | [`docs/guide/printed-sheet.md`](docs/guide/printed-sheet.md) |
| `data/transcripts/`, `TranscriptLibrary`, `ReplayLoader` | [`docs/guide/replay.md`](docs/guide/replay.md) |
| `mcp/`, `cli/Headless/`, the character-building skill | [`docs/guide/mcp-and-headless.md`](docs/guide/mcp-and-headless.md) |
| `worker/`, `functions/`, sign-in, invitations, the error log | [`docs/guide/accounts-server.md`](docs/guide/accounts-server.md) |
| `data/rulebook/`, `tools/RulebookExtractor/` | [`docs/guide/rulebook-corpus.md`](docs/guide/rulebook-corpus.md) |
| any test, any guard, Qodana, the pixel goldens | [`docs/guide/testing.md`](docs/guide/testing.md) |
| `.github/workflows/deploy.yml`, `_headers`, `_redirects`, the CSP | [`docs/guide/hosting.md`](docs/guide/hosting.md) |
| `cli/` — the terminal wizard | [`docs/guide/cli-wizard.md`](docs/guide/cli-wizard.md) |

**A dead pointer is worse than no pointer**, so `RepositoryGuideTests` holds the two halves
together: every path this table names exists, and every file in `docs/guide/` is named by it. An
orphaned guide is one nobody will read, and a table naming a file that has been renamed away sends a
reader hunting for rules that are still in force somewhere else. The same tests hold this file to a
line budget — **not because length is a vice, but because a file nobody finishes is a file whose
last two hundred lines do not fire.** That is the failure this split exists to fix: three whole
subsystems shipped in one pull request and none of the three was written down here, because the
place to write them down had stopped being a place anybody read.

**Adding something? Put it in the guide for its area, not here.** The way this file got to 1,431
lines was one reasonable paragraph at a time.

## Commands

```bash
# Run the terminal wizard
dotnet run

# Cost and validate a character without a terminal (see docs/guide/mcp-and-headless.md)
dotnet run -- build --from character.json --no-export

# Run the browser front end
dotnet run --project web/ProwlersAndParagons.Web.csproj

# Publish the MCP server where a client can launch it (see docs/guide/mcp-and-headless.md)
dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o mcp-server

# Publish the browser front end as a static site
dotnet publish web/ProwlersAndParagons.Web.csproj --configuration Release

# Build without running
dotnet build

# Run with a specific project file
dotnet run --project ProwlersAndParagonsAutomation.csproj

# Reproduce the CI build — analyzer warnings become errors
dotnet build --configuration Release -p:ContinuousIntegrationBuild=true

# Run the tests (also run in CI, with the same strict flags)
dotnet test

# Run the accounts server's tests — a separate suite, because that server is JavaScript.
# Uses local Node 22+ if there is one, Docker otherwise. Also run in CI.
./scripts/test-worker.sh

# Ask the mail provider why it refused a send, instead of deploying to find out.
# Needs .dev.vars — see docs/guide/accounts-server.md, and never write to that path.
node scripts/probe-mail.mjs you@example.com
```

## Two disciplines that are commands, not cautions

Both of these were written here as warnings first, and both were then ignored by somebody who
had read them in the same session. A caution does not fire hundreds of steps later, when you are
thinking about something else. These are phrased as things to *run*.

### Before any destructive revert, stash

```bash
git stash push -u -m pre-experiment
```

**Run it before `git checkout -- .`, `git checkout -- <file>`, `git reset --hard`, or letting any
mutation pass revert for you.** `git checkout --` takes uncommitted work with it, silently and
with no confirmation. That has now cost this project rework three times, the last of them by an
agent that had read this paragraph's predecessor earlier in the same session and reverted one
file to undo a mutation, taking an unrelated uncommitted change with it.

There is no judgement call to make about whether a particular revert is risky. Stash first. If the
stash turns out to be empty, it cost nothing; `git stash pop` afterwards is one command.

**Committing first is better still** where the work is in a committable state — a mutation
experiment run against committed work has nothing to lose. Stash is for when it is not.

**And the loss does not announce itself at the commit — it announces itself as a commit message
that describes a change the commit does not contain.** That has now happened here too, to somebody
who had read the paragraph above in the same session: a fix was written, the suite was run green,
a mutation was applied *on top of it* to check a guard, and `git checkout -- <file>` reverted both.
The staged razor and test files still looked like the change, `git commit` succeeded, and the
stylesheet half was simply gone. Two habits catch it and neither is a judgement call:

- **Re-run the suite *after* the revert, never only before it.** A green run taken before a
  `checkout` says nothing about the tree being committed.
- **Read `git show --stat HEAD` against what the message claims.** A missing file in that list is
  the whole failure, visible in one line.

The deeper rule is the one at the top of this section: a mutation belongs against *committed* work.
If the fix had been committed before the guard was mutated, there would have been nothing to lose.

**And when it goes wrong anyway, git has probably still got it.**

```bash
git reflog                                  # every HEAD move: bad reset, bad rebase, lost commit
git fsck --unreachable | grep commit        # dropped stashes and orphaned commits
git stash apply <sha>                       # recover one by hand
git show <sha>:path/to/file                 # or just read one file out of it
```

`git stash pop` **prints the SHA it dropped** — `Dropped refs/stash@{0} (c1c89e…)`. That line is the
cheapest recovery handle there is, and piping the pop to `/dev/null` throws it away. Do not.

**The line that decides whether any of this works is whether an object was ever created.** A stash,
a commit, even a bare `git add`, all write objects that survive being dropped and are findable
above. A working-tree edit that was never stashed, added or committed is not an object, and
`git checkout -- <file>` over it is unrecoverable by any means — which is exactly the loss this
section opens with. So the stash rule is not only prevention: **it is what makes recovery possible
at all.**

Related, for the other direction: when something *is* broken and nobody knows since when,
`git bisect run <command>` will find the commit. It takes any command whose exit code says
good-or-bad, so the harness drivers work directly — a script that regenerates the proofs and greps
`<title>` for `PASS` is a usable bisect predicate, and would have located a regression this project
shipped inside a fix.

### A check is not done until you have broken it and watched it fail

**Write the guard, then deliberately break the thing it guards, then run it and see it go red.**
Not "reason about whether it would catch it" — run it. A check that has never failed is a claim,
and the claim is usually wrong: this repository has now shipped, on separate occasions,

- a guard that passed because a one-character inversion left every asserted string in place,
- a guard that passed because its `setTimeout` was present and did nothing,
- a proof whose four checks passed because a missing stylesheet meant **the animation never ran
  at all**, so every assertion about the end state held trivially,
- an inset measurement that reported a spread of `0.00px` while one band was visibly 60px out of
  line, because `getBoundingClientRect()` returns the border box and the break was padding.

Every one of those was found by breaking it. None was found by reading it.

**And every harness carries a positive control**: assert that the work *happened* — an execution
counter, `getAnimations().length`, an element count, a scroll position that actually moved —
**before** asserting that its outcome was right. Three of the four failures above were a feature
that did not run being mistaken for a feature that worked, which is the single most common way a
check in this repository has been wrong.

A mutation that is semantically null does not count as breaking it. Removing the assigned resting
frame from the counting figure changes nothing observable, because the easing already reaches
exactly 1 at `t=1`; the honest report is that the mutation was a no-op, not that the guard has a
hole. Break it with something that changes the answer.

**And a mutation can be null because the *fixture* is wrong, not the guard.** A `PageReader`
mutation disabling the space-glyph split survived, and the cause was a fixture whose gap sizes
happened to satisfy the other splitting mechanism too — so the behaviour under test was still
reached, by the wrong route. The honest response is to tighten the fixture and re-run to red, not
to record a hole that is not there and not to record coverage that is not there either.

### A denylist of spellings cannot make a verdict honest. Build the broken twin

**This is the general form of the rule above, and it is now the shape every behavioural harness in
this repository uses.** `MustNotShow` banned the literal `say(true` in the sticky harness so that
a verdict could not be hard-coded. `|| true` is textually distinct, walks straight through it, and
made the harness report `PASS` against a strip that moved the full 483px it should have stayed
pinned against — and the same trick inverted the motion harness while `MustNotShow` stayed green.
The spelling space is unbounded; a longer denylist buys one more spelling and no more.

So each parameterised harness writes a **deliberately-broken twin beside the real page**, driven by
the *byte-identical* harness script — only the thing under test differs, because a twin with a
doctored script proves nothing. CI requires the real page to say `PASS` **and** the twin to say
`FAIL`. `WithDefect` reads the shipped file and substitutes one documented line, **throwing if that
line has moved**, so a twin cannot silently stop reproducing its defect and start passing for the
wrong reason.

Two properties to keep if you touch this:

- **A twin must say `FAIL`, not merely fail to say `PASS`.** A harness whose script never ran
  leaves its resting `measuring` text, which is neither verdict — and "not PASS" would call that
  green. This is the same failure three of this repository's four historical guard faults were.
- **`MustNotShow` stays.** It is cheap and it catches the lazy spelling. Its doc comment now says
  what it cannot do, so nobody reads it as the guarantee again.

A structural "the verdict expression is derived from the measured variables" check was considered
and rejected: with no data-flow analysis in this repository's tooling it is a second unbounded
denylist of the same shape that just failed, and the twin subsumes it by proving behaviour.

## Architecture

Four layers with a strict no-upward-dependency rule, one project each — and three hosts on the
top layer, none of which may hold a rule of its own:

```
                                          ↗   cli/
data/rules/   →   engine/   →   sheets/   →   web/   ←   data/transcripts/
                                          ↘   mcp/
```

- **`data/rules/`** — JSON files only. No logic. All rules data extracted from the P&P Ultimate Edition PDF lives here.
- **`data/transcripts/`** — the second data input, and **not rules**: four recorded conversations the browser replays, read by `engine/TranscriptLibrary`. They are read *through* the engine rather than by it — every character in one goes through `CharacterSheetJson`'s strict reader — and nothing in the engine's rules logic knows they exist. Only `web/` reads them this way; `worker/` also holds a baked copy of the same bytes, gated behind an account, but relays them without parsing a word of them — see [`docs/guide/accounts-server.md`](docs/guide/accounts-server.md). See [`docs/guide/replay.md`](docs/guide/replay.md).
- **`engine/`** — Pure C#, zero Spectre.Console references, no filesystem access. `CostCalculator` and `CharacterValidator` are the authority on HP costs and validity. No front end tallies points itself. The one file here that is not rules logic is `SampleCharacters.cs`, which builds two `CharacterSheet`s for preview — see [`docs/guide/printed-sheet.md`](docs/guide/printed-sheet.md).
- **`sheets/`** — The `.txt` and `.json` exports, plus the stat-line and gear-line formatters, all returning strings. Shared by every host — the wizard, the browser, the build command and the MCP server; writing a string somewhere is the host's job.
- **`cli/`** — Terminal presentation. Uses Spectre.Console for all rendering. Each wizard step implements `IWizardStep` and receives `CharacterSheet`, `RulesRepository`, `CostCalculator`, and `DerivedStatsCalculator` via `Execute()`.
- **`web/`** — Browser presentation. Blazor WebAssembly; see [`docs/guide/browser.md`](docs/guide/browser.md).
- **`mcp/`** — Protocol presentation. An MCP server over stdio; see [`docs/guide/mcp-and-headless.md`](docs/guide/mcp-and-headless.md).

**These are separate projects on purpose, and splitting them was the point of the Blazor slice.** `engine/` and `sheets/` used to be compiled into the root executable, which a WebAssembly project cannot reference without dragging Spectre.Console in with it. Now the arrows above hold at compile time: `web/` has no calculator of its own and no reference that could reach one. Do not merge them back.

### Key engine types

| Type | Role |
|---|---|
| `CharacterSheet` | Mutable wizard state — all purchases accumulate here |
| `RulesRepository` | Lazy JSON loader with snake_case deserialization and cached lookup dictionaries |
| `CostCalculator` | HP cost logic — `PowerCost()`, `PerkCost()`, `TotalCost()`; all methods are pure |
| `DerivedStatsCalculator` | Edge, Health, Resolve, baseline/effective rank calculations |
| `PowerFormatter` (sheets) | Renders a Power's rulebook stat line (`Self · Baseline Rank (½ Toughness) · 1 HP per rank`) so output can be checked against the book |
| `CharacterSheetRenderer` (sheets) | Builds the `.txt` and `.json` exports as strings, for whichever host asked |
| `CharacterValidator` | Returns `ValidationResult` with `Error`/`Warning` severity issues |

## Settled — do not redo

Open work lives in [`PROGRESS.md`](PROGRESS.md), not here. What follows is the short list of things already decided, kept inline because the cost of re-litigating them is high.

Back-navigation, JSON export and engine unit tests are **done** — do not re-implement them.

**All rules data in chapters 1–2 is verified and locked by tests.** Every one of the 141 power entries (range, rank type, cost, baseline, description) plus all tiers, abilities, talents, pros, cons, perks and flaws has been checked against the book, and no `needs_review` flag remains anywhere in `data/rules/`. Do not re-verify these, and do not reintroduce a uniform `cost_per_rank`.

Settled rules questions:

- Lightning Reflexes is a **flat +6** Edge bonus on a flat 3 HP unranked Power
- Danger Sense **replaces** Perception in the Edge calculation; it is not added to it
- Super Speed sets Edge to **rank × 3**
- Determination is **5 HP per 1 Resolve** with no rank
- Overkill and Weak are a **−1 HP per rank** rate reduction, floored at 1 HP per 2 ranks — not a halving
- The minimum cost of a ranked power is **1 HP per 2 ranks**; for an unranked one it is 1 HP. A piece of **gear** floors at **0** instead
- Super Senses is costed as **one Power**, not one per option — the rulebook says so in as many words
- Deflection covers one attack type; covering **both doubles its rate** to 2 HP per rank, stated in the Power's own text rather than as a marked Pro
- The Item Con is **not** credited against a piece of gear
- Generic Pro/Con applicability is **derived from the option**, never listed on the Power; unenforceable constraints are caveats, not filters. The single exception widens rather than narrows and is a record of printed text: a Power whose own entry names a generic option overrides that option's Range rule, which is `pros_allowed_by_own_text` and today is Force Field alone
- An option is taken **once**, unless its own entry says to buy it again — Also X on Energy Absorption and on Energy Form, and the generic Affect Inanimate, marked `repeatable` in the data. Not the other five Also X entries, which are priced per unit
- A rankless Power's **default rank** comes from its Source and applies **only** against other Powers — it is not its effective rank
- Sheets group Powers under Source headings; an Ability's or Talent's Source prints as a line **inside** a Power group, never as a marking on the Abilities block, and it is **not** derivable from rank
- The Iconic tier's "200+" is explicitly a bare minimum, so it is GM discretion rather than missing data
- Hero and Villain are **one app with four palettes**, since light/dark became an axis of its own — Hero/Villain is an identity and light/dark is a reader's preference, and neither is derivable from the other. The mode *is* a field on `CharacterSheet` — `IsVillain` — and no rules code may read it, which a test enforces. That reverses an earlier entry, and only because the budget moved off the switch: "a Villain has no Hero Point budget" was never a rule about Villains, and is now `UnlimitedBudget`, an independent toggle either kind of character can carry
- `engine/`, `sheets/`, `cli/` and `web/` are **separate projects**, so the dependency arrows hold at compile time rather than by convention
- Assisted creation *in this repository, for somebody with it checked out*, is a **non-interactive command plus a skill** — and the model proposes while the engine decides, never the other way round. **For somebody else, connecting their own Claude, it is an MCP server**, which is the mechanism built for exactly that and lets us handle no credentials at all. The two are not in tension and both call the same engine; the earlier flat "not an MCP server" note was scoped to the first case and is superseded
- **A visitor to the site cannot bring their own Claude, and that is settled — do not re-investigate it.** A claude.ai subscription cannot be lent to a third-party site, the API is separate billing with no dependable free tier, and custom connectors are gated to paid plans. A proxy funded by the owner was rejected — it costs money, invites abuse, and breaks the static-site property the README advertises. **What has changed is who the answer is for:** the recordings are at `/admin/portfolio/replay`, behind an account, because the owner decided the demonstrations are a thing to show somebody rather than a thing to publish. The technical finding above is unaffected; only the audience is
- **This is a tool for running *and* playing P&P**, not a character generator and not a GM screen — the owner's answer to the question `PROGRESS.md` item 11 was built around, and it dissolves that question rather than picking a side. **What it does not license is the part worth carrying here**, because an agent in any area could reasonably assume otherwise: play rules do not go into `engine/`, which is the authority on cost and validity and knows nothing about resolving an action — a combat simulator is a *second* engine beside it. The bullet below is unaffected and gets more load-bearing, not less
- An illegal character is **reported, never repaired**: the engine is a judge and does not make design decisions about somebody's character
- An assisted build **starts at full strength and trades down out loud**, rather than being built tastefully and quietly leaving points unspent. Trading down is a decision the person makes; trading up is a correction they have to notice they need. It does not license overruling a weakness they stated, dropping what they asked for, or exceeding the budget
- **Only Heroes have Resolve**; the GM gets Adversity, spendable on any NPC. The engine computes the figure anyway and it is noise on a Villain — never quote it, never buy Determination on one, and cap a Villain's Traits freely, because the Resolve a Hero pays for a rank at the cap is not a currency a Villain holds
- **A Villain's one to three Flaws are the players' handles** — the payout half of the bargain is gone, so the slots are where the GM says how the character can be beaten. A slot spent on colour, or on something the fiction carries free, is a handle the party does not get

Each of these was wrong at some point and is now covered by a regression test naming the rule. If one appears to be violated, read `PROGRESS.md` and the test before changing the code.

