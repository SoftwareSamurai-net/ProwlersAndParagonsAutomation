# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slice it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

Six pull requests merged: [#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30)–[#35](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/35).
3190 tests, zero warnings at CI strictness, a whole-tree Qodana scan at zero, and MIT in
`LICENSE`. The tool creates, prices, validates, prints and exports characters through three
front ends: the terminal wizard, the browser app, and `build --from character.json`.

The current branch is `claude/last-four-heroes`, which holds one `PROGRESS.md` edit recording
that all four remaining Hero residuals are pricing questions rather than transcription faults.
Commit it or fold it into the next slice; nothing depends on it.

---

## The slice to build: conversational character creation

**The goal, in the owner's words:** he describes a character out loud, and the tool builds one
that can do what he described. Ask him questions back when the description does not determine
something that matters.

**And it has to be something other people can use with their own Claude**, which is what decides
the surface below. The audience is two groups the owner named himself: people who code, and
"normie mates who just want to see it work in the browser". They need different answers, and only
one of them can bring their own inference.

That is a different job from what `build --from` and the existing skill already do, and the
difference is the whole slice. Today the loop is: a model writes a JSON file, submits it, reads
the findings, resubmits. That works and is tested. What it is not is a **conversation** — there
is nothing that turns "a washed-up boxer who punches through time" into the two or three
questions whose answers actually change the build, and nothing that reports back in the
language the description was given in.

### What already exists, so you do not rebuild it

| | |
|---|---|
| `dotnet run -- build --from x.json` | Costs and validates. Exits 0 legal, 1 illegal, 2 unreadable. One JSON report on stdout for all three. `cli/Headless/BuildCommand.cs` |
| Structured findings | Every issue carries `subject_kind`, `subject_id`, `owner_id`, `value`, `limit`, `options` beside the sentence, so a repair loop never parses English |
| `.claude/skills/prowlers-and-paragons-character/SKILL.md` | The schema, the report shape, the repair loop, and the rules that trip up a first draft. `SkillDocumentationTests` feeds its own example through the strict reader, so it cannot rot silently |
| `engine/CharacterSheetJson` | Reads and writes the character-sheet shape. Strict on submit (an unknown field is refused), lenient for the browser's local storage |
| The engine | 141 Powers, both Trait floors, the Trait Cap, budget, applicability, duplicates, quantities. **It is the judge and it is trustworthy**: 16 of 20 published Heroes rebuild to exactly 125 |

### The surface, and why it is now two things

**For the owner and anyone else who codes: an MCP server.** This reverses an earlier note in this
file, and the reversal is correct rather than a change of mind — the old note said "a skill plus
the command, not an MCP server", and that reasoning was scoped to *the owner, in Claude Code,
with the repo checked out*. The question that reopened it is different: **other people connecting
their own Claude account.** MCP is the mechanism built for exactly that, and it puts the
conversation in a client designed for it while we handle no credentials at all — the user talks
through the subscription they already pay for.

A stdio server wrapping the engine, exposing something like `cost_character`,
`validate_character` and `list_powers`. The engine is already pure, synchronous and
filesystem-free, so this is small. It does not replace `build --from`; both call the same engine.

**For everyone else: a replay demo with the engine live.** The owner asked whether a visitor could
use a free Claude account from the browser. **They cannot, and this is worth writing down so it is
not re-investigated:** a claude.ai subscription cannot be lent to a third-party site (there is no
sign-in-with-Claude that hands a website your inference quota), the API is separate billing with
its own keys and no dependable free tier, and claude.ai's custom connectors are gated to paid
plans. A browser visitor has no way to bring their own inference.

So the two real options are a proxy the owner funds — a Worker holding his key, rate-limited,
which costs money, invites abuse and **breaks the static-site property the README advertises** —
or a **replay**: three or four real transcripts of a description, the questions back, the answers
and the resulting character, with the model's turns replayed and **the engine run for real in
WASM**. Costs, validation and the printed sheet genuinely computed client-side; then hand the
character to the existing editor so the visitor can poke at it.

The replay is the recommendation for a portfolio piece: no account, no key, no server, no running
cost, nothing to abuse. **The one thing that would ruin it is faking the numbers.** If the sheet
is a screenshot then the demo misrepresents the thing that was built; the half worth showing off
is the engine deciding, and that half must stay live. Label the replay as a replay.

Either way, the hard part is the same and it is below: the question policy, the register of the
reply, and never inventing a Hero Point. The transport is the easy half.

### What to actually build

1. **A question policy.** The interesting design question is *which* questions are worth asking.
   A description under-determines dozens of fields; almost all of them can be defaulted without
   the owner caring. The ones that change the character materially are few:

   - **Tier**, because it sets the budget and the Trait Cap and everything else is measured
     against it. Never guess this.
   - **Where the power comes from** — one of the six Sources. It costs nothing and changes no
     rank, but it decides how a sheet reads and what a rankless Power's default rank is.
   - **Whether a described effect is one Power or several.** "Punches through time" could be
     Strike plus Blink, or Omni-Power, or Alternate Form. This is the question that most changes
     the build, and it is the one a model is most tempted to answer silently.
   - **What the character is bad at**, because a package sets a floor at 2d or 3d and the
     remaining points have to come from somewhere. Ordinary people have 2d in everything;
     deciding what stays ordinary is a characterisation question, not an arithmetic one.

   Everything else — talent spread, exact ranks, which Flaw, gear — can be proposed and shown,
   not asked. **Ask two or three questions, not ten.** A questionnaire is a worse interface than
   a wizard, and the wizard already exists.

2. **Report back in his language, not the engine's.** He said "a character who can do what I
   described". The reply should say what the character can *do* — "9d Strike, so you hit at
   Extreme difficulty most of the time" — and mention Hero Points as bookkeeping. The validator's
   messages are already written for a player rather than a developer (there is a test for it);
   match that register. Do not print `TRAIT_BELOW_MINIMUM` at him.

3. **Show the sheet, not the JSON.** `build` writes a `.txt` sheet and a `.json` export. The
   `.txt` is the one a person reads. Offer it.

4. **Keep the ordering.** The model proposes, the engine decides. Nothing in this slice may
   compute a Hero Point, quote a cost from memory, or declare a character legal without the
   command having said so. Every previous slice that drifted here produced a confidently wrong
   number.

### Two traps this specific slice will hit

- **A description that cannot be afforded.** "Superman, but also a detective" is 300 Hero Points
  at Standard tier. The right move is to say so and offer the trade — a lower rank, a narrower
  Power, or a higher tier if the GM allows — not to silently build something weaker and present
  it as what was asked for. `hero_points.remaining` goes negative by exactly the overspend, which
  is the number to quote.
- **The rulebook is in `docs/` and a worktree cannot see it.** `*.pdf` is gitignored, so both
  PDFs live in the main working directory only. `ls docs/*.pdf` from a worktree reports nothing,
  which reads as "there is no rulebook" and is wrong — a whole slice was worked through on that
  assumption last session. The extraction recipe is at the end of the printed-sheet section of
  `CLAUDE.md`; PdfPig in a scratch console project, page offset a constant +3.

### How to know it works

The existing tests cover the command. What this slice needs is different: **run it against real
descriptions and read the output as the owner would.** Try at least one description that is
cheap, one that is unaffordable, one that is ambiguous about whether it is one Power or three,
and one that names something the system has no Power for. The last is the interesting one — the
honest answer is to say which Power comes closest and why, not to invent one.

If a description reveals a rules gap, that is a finding for `PROGRESS.md`, not something to paper
over in the prompt.

---

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the
   reasoning survives, and it has twice been allowed to describe a state the code had left.
2. **Have the work adversarially reviewed by agents that know nothing about it**, act on the
   findings, re-review, and only then merge. Ask each reviewer, for every guard test, to name a
   plausible bug the test claims to cover but would not catch. Over three rounds last session
   that question found nine ways an illegal character was certified legal, a command that hung
   for ever on `--from CON`, and thirteen false claims in the prose — two of them in the entry
   written to describe the work.
3. **Ask a reviewer to audit the fixes, not just the code.** The single most valuable reviewer of
   the session was the one pointed at the previous round's fixes: four of the six it checked did
   not hold.
4. **Commit before letting a mutation pass run, or give it its own worktree.** A reviewer doing
   mutation testing restores files with `git checkout -- <file>` and reverted an uncommitted fix
   in a file we were both touching. The single-file form does this as surely as `git checkout --
   .` does.
5. **Do not start a dev server.** It raises an approval dialogue that blocks unattended work.
   `dotnet build`, `dotnet test` and the Docker Qodana scan do not.
6. **Check a rulebook citation before repeating it.** Every one of the twenty Hero page citations
   was ten pages out, and had been through five review rounds, because nothing read them.
