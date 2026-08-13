# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is only the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the slice it describes.** It is a note between
sessions, not documentation.

---

## Where things stand

Nine pull requests merged, [#30](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/30)–[#39](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/39).
3298 tests, zero warnings at CI strictness, a whole-tree Qodana scan at zero, MIT in `LICENSE`,
and the site live on Cloudflare Pages. The tool creates, prices, validates, prints and exports
characters through **four** front ends: the terminal wizard, the browser app,
`build --from character.json`, and an MCP server somebody can connect to their own Claude.

The last slice built that MCP server ([#39](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/39)).
Its entry in `PROGRESS.md` and the "The MCP server" section of `CLAUDE.md` carry the reasoning;
the question policy — which two or three questions are worth asking somebody describing a
character out loud — is `mcp/QUESTION-POLICY.md`, embedded in the assembly and served verbatim
as the `creation_guide` tool.

---

## The slice to build: the replay demo

**Who it is for.** The MCP server serves people who code and can bring their own Claude. This is
the other audience the owner named — "normie mates who just want to see it work in the browser".
They have no account to bring.

**They cannot bring one, and this is settled** — do not re-investigate it. A claude.ai
subscription cannot be lent to a third-party site (there is no sign-in-with-Claude that hands a
website your inference quota), the API is separate billing with its own keys and no dependable
free tier, and claude.ai's custom connectors are gated to paid plans.

So the two options are a proxy the owner funds — a Worker holding his key, rate-limited, which
costs money, invites abuse and **breaks the static-site property the README advertises** — or a
**replay**: three or four real transcripts of a description, the questions back, the answers and
the resulting character, with the model's turns replayed and **the engine run for real in
WebAssembly**. Costs, validation and the printed sheet genuinely computed client-side; then hand
the character to the existing editor so the visitor can poke at it.

**The replay is the recommendation.** No account, no key, no server, no running cost, nothing to
abuse. It is also the honest demonstration, because the half worth showing off is the engine
deciding, and that half stays live.

### The two things that would ruin it

1. **Faking the numbers.** If the sheet is a screenshot, or the costs are baked into the
   transcript, the demo misrepresents the thing that was built. Every figure on screen must come
   back from `CostCalculator` and `CharacterValidator` running in the visitor's browser — which
   they already do, because `web/` is the same compiled engine. **If the replay data holds a
   Hero Point total, that is the bug.**
2. **Not labelling it.** A replayed conversation presented as a live one is a lie about what the
   visitor is looking at. Say it is a recording, in the UI, where they cannot miss it.

### What already exists, so you do not rebuild it

| | |
|---|---|
| `web/` | Blazor WebAssembly, the engine compiled to WASM, `CharacterSession` + `CharacterStore`, the six creation pages and `SheetView`. A character already survives a refresh and a shared link |
| `mcp/QUESTION-POLICY.md` | The question policy. **The transcripts should follow it** — that is what makes the demo a demonstration of the design rather than of a chat |
| `mcp/` over stdio | How to *produce* the transcripts: drive a real conversation through the real server and record it, rather than writing dialogue by hand |
| `engine/CharacterSheetJson` | Reads and writes the character-sheet shape — the inputs, not the export. The shape a transcript's final character should be stored in |
| `SampleCharacters` | Two finished characters and the "load a sample" flow the replay can hand off to |
| `CharacterStore` | Storing a character the app can restore, and the guard that refuses one the engine cannot answer for |

### What to actually build

1. **Three or four recorded conversations**, each with a description, the questions the assistant
   asked back, the answers, and the character that came out. Cover the cases that make the
   design visible: one that is cheap, one that does not fit the budget and has to give something
   up, and one where the description is ambiguous about whether it is one Power or several.
2. **A replay surface in `web/`** that steps through a transcript at the visitor's pace, and at
   the end **runs the character through the engine in front of them** — the budget bar, the
   findings, the printed sheet.
3. **A hand-off into the editor**, so the visitor can change a rank and watch the numbers move.
   That is the moment the demo earns its keep.
4. **A label**, and a line saying where the live version is: the MCP server, with the README's
   setup.

### Traps this slice will hit

- **`web/` names no colour and no internal type**, and `WebPresentationTests` fails the build if
  a new component does either. Read the "three presentation rules" section of `CLAUDE.md` before
  writing markup; one component owns each repeated class.
- **Anything about what a component renders is tested in `tests/ProwlersAndParagons.Web.Tests`
  with bUnit**, not by reading source. That split exists because a source-reading test shipped
  "Armor8d" twice.
- **The transcripts are data and will rot.** If they hold ids the rules files no longer have, the
  replay breaks quietly. Hold them to the engine the way `SkillDocumentationTests` holds the
  skill: read every character in every transcript through the strict reader and validate it.
- **The payload is already 27 MiB** (item 5 in `PROGRESS.md`). Do not make it worse with images;
  the transcripts are text.
- **The rulebook PDFs are in `docs/` and a worktree cannot see them** — `*.pdf` is gitignored, so
  they live in the main working directory only. `ls docs/*.pdf` from a worktree reports nothing,
  which reads as "there is no rulebook" and is wrong.

### How to know it works

Open it as somebody who has never seen the tool. Can they tell it is a recording? Do they reach
a printed sheet? Can they change something and see the number move? And **check the numbers on
screen against `dotnet run -- build --from` for the same character** — if the two disagree, the
demo is lying, which is the one failure that matters here.

---

## How this project expects to be worked on

Not preferences — this is what the last few slices cost when they were skipped.

1. **Update `PROGRESS.md` in the same change**, not afterwards. It is the only place the
   reasoning survives, and it has twice been allowed to describe a state the code had left.
2. **Have the work adversarially reviewed by agents that know nothing about it**, act on the
   findings, re-review, and only then merge. Ask each reviewer, for every guard test, to name a
   plausible bug the test claims to cover but would not catch. Over five rounds last session
   that question found a search flag that told a model the rulebook has no Power for flight,
   five tests that were theatre, and two fixes that did not hold.
3. **Ask a reviewer to audit the fixes, not just the code.** The single most valuable reviewer of
   the last two sessions was the one pointed at the previous round's fixes: four of six did not
   hold the first time, two of eight the second.
4. **Run the suite on Linux before you push.** Every reviewer and every local run is on Windows,
   and CI is not: `Path.Combine("C:", "app")` is rooted on Windows and relative on Linux, which
   is how a green local run pushed a red build. Four minutes closes it:

   ```bash
   docker run --rm -v "$(pwd -W):/src" -w //src mcr.microsoft.com/dotnet/sdk:10.0 \
     bash -c "dotnet test --configuration Release -p:ContinuousIntegrationBuild=true"
   ```

   Copy the tree somewhere first, or the Linux build leaves Linux artifacts in your `bin`/`obj`.
5. **Commit before letting a mutation pass run, or give it its own worktree.** A reviewer doing
   mutation testing restores files with `git checkout -- <file>` and has reverted uncommitted work.
6. **Do not start a dev server.** It raises an approval dialogue that blocks unattended work.
   `dotnet build`, `dotnet test` and the Docker Qodana scan do not.
7. **Check a rulebook citation before repeating it.** Every one of the twenty Hero page citations
   was ten pages out, and had been through five review rounds, because nothing read them.
8. **A whole-tree Qodana scan is part of finishing**, not an extra. The command is in `CLAUDE.md`;
   the repository holds it at zero, and the last slice's scan found three guards that an
   inspection read as dead code and a client can actually reach.
