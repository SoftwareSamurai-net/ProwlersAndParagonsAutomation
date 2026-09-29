# Connecting the rules engine to your own Claude

This repository ships **two MCP servers**: small local programs that answer questions about the
Prowlers & Paragons rules.

- **`prowlers-and-paragons`** builds characters. Connect it to a Claude client and you can describe
  one in ordinary words — *"a washed-up boxer who punches through time"* — and get a legal, costed
  one back, with Claude asking you the two or three questions your description genuinely leaves
  open.
- **`prowlers-and-paragons-play`** runs fights. Hand it combatants and it resolves an encounter a
  turn at a time out of Chapters 3–5, or runs the same matchup a few hundred times and reports the
  rates. See [the encounter server](#the-encounter-server) below.

**They are two programs, published separately and registered separately**, because they are two
engines: one is the authority on what a character costs and whether it is legal, the other on what
these dice against this threshold under these rules produce. Register either without the other.

**It handles no credentials and holds no API key.** The server knows about the rulebook and
nothing else; the conversation happens in the Claude client you already use, on your own
subscription. Nothing is sent anywhere by this program.

**Every Hero Point figure and the word "legal" come from the engine, never from the model.**
That ordering is the whole point — see [`mcp/QUESTION-POLICY.md`](../mcp/QUESTION-POLICY.md),
which is what the `creation_guide` tool returns, so there is one copy and it cannot drift from
what the assistant is taught.

If you have no Claude of your own, the site has four recorded conversations that show the same thing,
with the engine costing and validating in your browser as you read. They are at
`/admin/portfolio/replay` and **need an account** — they used to be public and the site's owner moved
them, because this is not a sign-up and a demonstration is a thing to show somebody rather than a
thing to publish.

---

## 0. Checked this repository out? Publish once and it is configured

[`.mcp.json`](../.mcp.json) at the root of this repository registers the server for anybody working
in this checkout, on any operating system, from a path relative to the checkout — so a clone, a
fresh machine and a git worktree are all already correct, with **no absolute path to install and
none to keep up to date**. It wants one command first:

```bash
dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o mcp-server
dotnet publish mcp-play/ProwlersAndParagons.McpPlay.csproj -c Release -o mcp-play-server
```

**Two commands and two directories, because they are two programs.** `.mcp.json` registers both,
each out of its own published copy, and a publish of one must not overwrite the other's binary.
Skip the second and you have the character builder and no encounter server, with a log line saying
which file is missing.

**Run those after cloning, and again after a `git pull` that moves either engine or the rules** —
the registration runs the published copy and never builds, so nothing else will bring it up to
date. A checkout that has not published gets exit 129, an **empty standard output** and one line on
standard error naming the file that is missing: a server that is not there, with a log saying so.

**It runs a published copy rather than `mcp/bin/Release` because a running server blocks a Release
build of this repository, and that was measured rather than reasoned about.** A server holding
`mcp/bin/Release` is the ordinary state of a machine that uses this, and with one running:

| Command | Server in `mcp/bin/Release` | Server in `mcp-server/` |
|---|---|---|
| `dotnet build` | succeeded, 0 warnings, 0 errors | succeeded, 0/0 |
| `dotnet build --configuration Release` | **FAILED, 10 warnings, 2 errors** | **succeeded, 0/0** |

Measured 2026-09-02 on the pinned SDK. The ten and the two are not arbitrary: they are MSBuild's
`Copy` retry default — ten `MSB3026` retries, then `MSB3027` and `MSB3021` — and all twelve name one
file, `engine/bin/Release/net10.0/ProwlersAndParagons.Engine.dll`, being copied into `mcp/bin/`, with
the holding process named in the message. Stopping the server cleared it to 0 and 0. The right-hand
column is the same build, on the same machine, genuinely recopying that file while the published
server was live and answering `tools/list`.

**The lock moves rather than vanishing, and that is the whole trade.** `dotnet publish -o
mcp-server` is now the command that wants the server stopped first, which is a thing you do
deliberately and rarely; `dotnet build`, `dotnet build --configuration Release`, `dotnet test` and
everything on the way to a push are free. Nothing is lost in freshness either — the registration
never rebuilt on launch, so the running server was always the last thing built, and it is now the
last thing published.

**Claude Code will not start a server a repository proposed until somebody says so, once per
checkout.** That is a deliberate gate on running a program a clone handed you. Answer it either way:

- **In an interactive session**, Claude Code asks the first time.
- **Without one**, list the server in `enabledMcpjsonServers` in that checkout's
  `.claude/settings.local.json` — a personal, git-ignored file, so this approves it for you and
  not for everybody who clones:

```json
{
  "enabledMcpjsonServers": ["prowlers-and-paragons", "prowlers-and-paragons-play"]
}
```

**`claude mcp list` goes on printing *Pending approval* afterwards, and is wrong about it.** The
same per-project key in `~/.claude.json` looks like it should work and does not. Both were measured
rather than reasoned about, and this is the command that settles it on your machine:

```bash
claude -p "Do you have a tool named mcp__prowlers-and-paragons__creation_guide? Answer YES or NO only."
```

`YES` means the server is connected, whatever `claude mcp list` says.

**Everything below is for the other case**: connecting a client that is not working inside this
checkout — Claude Desktop, or a Claude Code you use everywhere *except* here. That wants a copy of
the server installed somewhere of its own.

---

## 1. Publish it somewhere it will stay

Pick the block for your shell. **An unexpanded variable does not error** — it publishes the
server to a directory named after the variable, and then step 2 registers a path that is not
there, which surfaces much later as "nothing appears in the tool list".

**Windows, PowerShell:**

```powershell
dotnet publish mcp\ProwlersAndParagons.Mcp.csproj -c Release -o "$env:LOCALAPPDATA\ProwlersAndParagons\mcp-server"
```

**Windows, Git Bash:**

```bash
dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o "$LOCALAPPDATA/ProwlersAndParagons/mcp-server"
```

**macOS or Linux:**

```bash
dotnet publish mcp/ProwlersAndParagons.Mcp.csproj -c Release -o "$HOME/.local/share/prowlers-and-paragons"
```

`-o mcp-server` inside the checkout is what section 0 does, and it is right for a client working *in* the checkout because the registration is relative to it. For a client that is not, **the path you give it has to keep existing** — a checkout you move, or a git worktree you delete when a branch is done, takes the server with it. Somewhere outside the repository is the boring choice, which is what all three blocks above do.

That produces `ProwlersAndParagons.Mcp.exe` (no extension on macOS and Linux) with the rules files beside it, so it needs no repository checked out and no working directory of its own. It is framework-dependent, so the machine running it still needs the **.NET 10 runtime** — add `--self-contained -r win-x64` (or your own runtime identifier) to publish one that does not.

**Point your client at that binary rather than at the build tool.** The build tool needs the
checkout, and the whole point of publishing is a copy that does not. A published binary also never
builds, which is what keeps it off the build's write path — the reason section 0 uses one too, for a
client that *is* in a checkout.

**Re-publish to the same path after a `git pull`.** The server holds its own copy of the rules, so an old binary keeps answering with old rules, perfectly happily.

## 2. Tell your client about it

**Claude Code.** The scope is the part that matters. Same three shells, same order, and the path
has to be the one you just published to:

**Windows, PowerShell:**

```powershell
claude mcp add --scope user prowlers-and-paragons -- "$env:LOCALAPPDATA\ProwlersAndParagons\mcp-server\ProwlersAndParagons.Mcp.exe"
```

**Windows, Git Bash:**

```bash
claude mcp add --scope user prowlers-and-paragons -- "$LOCALAPPDATA/ProwlersAndParagons/mcp-server/ProwlersAndParagons.Mcp.exe"
```

**macOS or Linux** — no `.exe`, and it needs the executable bit, which `dotnet publish` sets:

```bash
claude mcp add --scope user prowlers-and-paragons -- "$HOME/.local/share/prowlers-and-paragons/ProwlersAndParagons.Mcp"
```

**If `claude` is not a recognised command**, the CLI is installed and not on your `PATH` — the native installer puts it at `%USERPROFILE%\.local\bin\claude.exe` on Windows and `~/.local/bin/claude` elsewhere. Call it by full path (`& "$env:USERPROFILE\.local\bin\claude.exe" mcp add …` in PowerShell), or put that directory on your `PATH` and open a new terminal.

`--scope user` registers it for **every project on your machine**, which is what you want for a character builder: you are most likely to use it in a session that has nothing to do with this repository. The default scope is `local`, which is this-project-only — fine if you only ever build characters while working on the tool itself, and confusing if you expect it elsewhere. Section 0 covers the checkout itself, through the `.mcp.json` at the root — that entry is project-scoped and needs no absolute path, because it starts the server from a path relative to the repository.

Check it, and remove it, with:

```bash
claude mcp list
```

```bash
claude mcp remove prowlers-and-paragons --scope user
```

**A session that is already running will not pick it up** — start a new one, then `/mcp` lists the connected servers. The tools arrive namespaced, as `mcp__prowlers-and-paragons__check_character` and so on; you never type those, you just describe a character.

**Claude Desktop** — Settings → Developer → Edit Config, which opens `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "prowlers-and-paragons": {
      "command": "C:\\Users\\you\\AppData\\Local\\ProwlersAndParagons\\mcp-server\\ProwlersAndParagons.Mcp.exe"
    }
  }
}
```

On macOS or Linux the same file takes an ordinary path and no extension:

```json
{
  "mcpServers": {
    "prowlers-and-paragons": {
      "command": "/Users/you/.local/share/prowlers-and-paragons/ProwlersAndParagons.Mcp"
    }
  }
}
```

Restart Claude Desktop. Use an absolute path in both — a client starts the program from a working directory of its own choosing — and note that JSON needs its backslashes doubled.

## 3. Describe a character

> *"Build me a Prowlers & Paragons character: a washed-up boxer who punches through time."*

Claude reads the question policy, asks you what it genuinely cannot infer, proposes a whole character, and hands it to the engine. What comes back is the engine's answer — every Hero Point figure and the word "legal" come from `CostCalculator` and `CharacterValidator`, never from the model.

You will be asked about two or three things and told about the rest: the tier, whether an effect you described is one Power or several, and what your character is deliberately ordinary at. Everything else — ranks, talents, which package, which flaw — is decided and shown to you, because a questionnaire is a worse interface than the wizard this repository already has. Ask for the sheet at the end and you get the printed one, not JSON.

## The seven tools, and why seven

| | |
|---|---|
| `creation_guide` | The question policy: which two or three questions change the build, what to decide silently, and the JSON shape a character takes |
| `list_options` | Tiers, packages, abilities, talents, sources, perks, flaws, pros, cons, gear features |
| `search_powers` | Which Powers could realise a described effect, with how each row matched — by name, or only on a word inside its description, which cuts both ways and says so |
| `power_detail` | One Power in full, with only the Pros and Cons it may legally take |
| `check_character` | **The judge.** Costs and validates one character, and reports what was spent on what |
| `check_alternate_forms` | The one rule that needs two sheets at once — Ch.2 p.21's Alternate Form family, checked over a roster `check_character` cannot see |
| `character_sheet` | The printed sheet, as text |

**Costing and validating are one tool on purpose.** `cost_character` beside `validate_character` is the engine's API rather than the conversation's: no turn of a conversation wants a price without knowing whether the thing priced is allowed, and a separate costing tool is an invitation to quote a number for a character that breaks a rule.

The hard part of this front end is not the transport — it is deciding which questions are worth asking. That reasoning lives in [`mcp/QUESTION-POLICY.md`](../mcp/QUESTION-POLICY.md), which *is* what `creation_guide` returns, so there is one copy of it and it cannot drift from what the tool teaches.

## The encounter server

`prowlers-and-paragons-play` is the second program. It resolves fights through
[the second engine](guide/play-engine.md) — Chapters 3–5 of the rulebook, read out of
`data/rules/play/` — and it holds no opinion at all about whether a character is legal. That is the
other server's question, and neither one answers the other's.

### The four tools

| | |
|---|---|
| `combat_guide` | The play policy: that the engine resolves and you narrate, what may never be stated without a ledger line behind it, who holds Resolve and who holds Adversity, what a measurement has to be quoted with, and what the engine does not yet model |
| `start_encounter` | Opens a fight and holds it by id: combatants, the table's switches, a Challenge Level and a seed. Answers with the order of action, the GM's Adversity pool and the table echoed back |
| `take_turn` | One intent, resolved. Acting and rolling are the same call. Answers with the ledger lines that step added and the state afterwards |
| `run_encounters` | The same fight on N consecutive seeds, with the rates — and N, the seeds, the policy and the table settings in the same object |

**Acting and rolling are one tool on purpose.** An intent is a *request*, and the whole discipline
of the second engine is that the engine decides what a request produces; a separate rolling call
would be an invitation to declare an attack, look at the dice, and decide afterwards what was being
attempted.

**Every answer carries ledger lines, and each names the rule it applied and the page it is printed
on.** That is what makes a figure off this server worth acting on. The policy behind it — including
the list of what is recognised and *not applied* — is in
[`mcp-play/PLAY-POLICY.md`](../mcp-play/PLAY-POLICY.md), which *is* what `combat_guide` returns, so
there is one copy of it and it cannot drift from what the tool teaches.

**`run_encounters` refuses fewer than 30 runs**, and says why: it answers with rates, and a rate off
five fights is noise wearing a percentage sign.

### Installing it outside a checkout

Section 0 covers a client working *in* this repository. For one that is not, publish it somewhere
that will keep existing and register it by absolute path, exactly as sections 1 and 2 do for the
character builder — the same three shells, with `mcp-play/ProwlersAndParagons.McpPlay.csproj` as
the project and `ProwlersAndParagons.McpPlay` as the binary. On macOS or Linux:

```bash
dotnet publish mcp-play/ProwlersAndParagons.McpPlay.csproj -c Release -o "$HOME/.local/share/prowlers-and-paragons-play"
```

```bash
claude mcp add --scope user prowlers-and-paragons-play -- "$HOME/.local/share/prowlers-and-paragons-play/ProwlersAndParagons.McpPlay"
```

On Windows the path is `$env:LOCALAPPDATA\ProwlersAndParagons\mcp-play-server` and the binary
gains a `.exe`, as in section 1. **Claude Desktop takes a second entry in the same
`claude_desktop_config.json` object** — keyed `prowlers-and-paragons-play`, with `command` set to
the absolute path of `ProwlersAndParagons.McpPlay` (plus `.exe` on Windows), shaped exactly like the
two blocks in section 2.

**Both stores ship beside this binary**, the character rules and the play rules under them, so it
needs no repository checked out. `PROWLERS_RULES_DIR` points at the character rules and the play
rules are the `play` folder under *that* — there is no second variable, and a directory with no
`play` under it is refused at startup rather than guessed past.

## Troubleshooting

- **The tools are not there and nothing is wrong with the server.** It is the approval gate in
  section 0, not a fault. `claude mcp list` says *Pending approval* whether or not it has been
  approved, so use the one-line `claude -p` check there instead.
- **`CONNECTION_CLOSED`, and no server log anywhere.** The registered file is not there, so
  nothing ever started — there is no stderr to read because there was no process. Run the command
  in the registration by hand: `No such file or directory` is the whole diagnosis. This has
  happened here; see item 18 in [`PROGRESS.md`](../PROGRESS.md). Working inside the checkout, use
  section 0 and delete the registration that names a path.
- **Nothing appears in the client's tool list.** Check the path is absolute and the file exists. The server writes one line to standard error on startup naming the rules directory it found; clients keep that in their MCP log.
- **`claude mcp list` shows it and the session does not.** The session was already running when you added it, or it was added at `local` scope from a different project. Start a new session, and check `claude mcp list` from the directory you are actually working in.
- **It answers with rules you have edited since.** The published binary carries its own copy. Re-publish over the same path, or point `PROWLERS_RULES_DIR` at your checkout's `data/rules` while you are changing them.
- **"The rules files could not be found."** You are running the binary somewhere without its `data/rules/` folder beside it. Either publish again with `-o`, or set `PROWLERS_RULES_DIR` to a directory holding `tiers.json` and the rest.
- **The session drops immediately.** Something is writing to standard output. Point the client at the published binary, not at `dotnet run`.
- **The character tools are there and the encounter tools are not.** They are two programs and
  two publishes; section 0 has both commands, and the approval list in `.claude/settings.local.json`
  has to name both servers. The one-line `claude -p` check works for the second one too — ask about
  `mcp__prowlers-and-paragons-play__combat_guide`.
- **"The play rules could not be found."** The encounter server has the character rules and no
  `play` folder under them. There is no second environment variable: point `PROWLERS_RULES_DIR` at a
  `data/rules` that has `data/rules/play` under it, or publish again with `-o` so both stores land
  beside the binary.
- **A Release build fails with `MSB3027` and a file in `mcp/bin/`.** A server is running out of that directory, which is the failure section 0 exists to prevent — an older registration, or a client started before this one landed. The message names the holding process; stop it, and re-read section 0.

