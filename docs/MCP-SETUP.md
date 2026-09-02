# Connecting the rules engine to your own Claude

This repository ships an **MCP server**: a small local program that answers questions about the
Prowlers & Paragons rules. Connect it to a Claude client and you can describe a character in
ordinary words — *"a washed-up boxer who punches through time"* — and get a legal, costed one
back, with Claude asking you the two or three questions your description genuinely leaves open.

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

## 0. Checked this repository out? It is already configured

[`.mcp.json`](../.mcp.json) at the root of this repository registers the server for anybody
working in this checkout, on any operating system, with **no path to install and none to keep
up to date**. It starts the server through the build tool from a path relative to the checkout,
so a clone, a fresh machine and a git worktree are all already correct.

```bash
dotnet build --configuration Release
```

**Run that after cloning, and after every `git pull`** — the registration passes `--no-build`, so
nothing else will. A checkout that has not been built gets exit 1, an empty standard output and one
line on standard error: a server that is not there, with a log saying so.

**It is `--no-build` because a launch that compiles can corrupt the stream, and this was measured
rather than guessed.** With a server already running from an earlier session — the ordinary state of
a machine that uses this — the copy into `mcp/bin/Release` fails, and MSBuild writes its `MSB3026`
retries **to standard output**, where the protocol lives. A harness read twelve of those lines in
the middle of a JSON-RPC stream. A launch that does not build has nothing to say.

**Claude Code will not start a server a repository proposed until somebody says so, once per
checkout.** That is a deliberate gate on running a program a clone handed you. Answer it either way:

- **In an interactive session**, Claude Code asks the first time.
- **Without one**, list the server in `enabledMcpjsonServers` in that checkout's
  `.claude/settings.local.json` — a personal, git-ignored file, so this approves it for you and
  not for everybody who clones:

```json
{
  "enabledMcpjsonServers": ["prowlers-and-paragons"]
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

`-o mcp-server` inside the checkout works too, but **the path you give your client has to keep existing** — a checkout you move, or a git worktree you delete when a branch is done, takes the server with it. Somewhere outside the repository is the boring choice, which is what all three blocks above do.

That produces `ProwlersAndParagons.Mcp.exe` (no extension on macOS and Linux) with the rules files beside it, so it needs no repository checked out and no working directory of its own. It is framework-dependent, so the machine running it still needs the **.NET 10 runtime** — add `--self-contained -r win-x64` (or your own runtime identifier) to publish one that does not.

**Point your client at that binary rather than at the build tool.** Two reasons, and the first is
the plain one: the build tool needs the checkout, and the whole point of publishing is a copy that
does not. The second is that **the build tool is only safe on that stream while it is not
building** — see section 0 — and a published binary never builds at all.

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

## The six tools, and why six

| | |
|---|---|
| `creation_guide` | The question policy: which two or three questions change the build, what to decide silently, and the JSON shape a character takes |
| `list_options` | Tiers, packages, abilities, talents, sources, perks, flaws, pros, cons, gear features |
| `search_powers` | Which Powers could realise a described effect, with how each row matched — by name, or only on a word inside its description, which cuts both ways and says so |
| `power_detail` | One Power in full, with only the Pros and Cons it may legally take |
| `check_character` | **The judge.** Costs and validates, and reports what was spent on what |
| `character_sheet` | The printed sheet, as text |

**Costing and validating are one tool on purpose.** `cost_character` beside `validate_character` is the engine's API rather than the conversation's: no turn of a conversation wants a price without knowing whether the thing priced is allowed, and a separate costing tool is an invitation to quote a number for a character that breaks a rule.

The hard part of this front end is not the transport — it is deciding which questions are worth asking. That reasoning lives in [`mcp/QUESTION-POLICY.md`](../mcp/QUESTION-POLICY.md), which *is* what `creation_guide` returns, so there is one copy of it and it cannot drift from what the tool teaches.

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
- **The session drops immediately.** Something is writing to standard output. Point the client at the built binary, not at `dotnet run`.

