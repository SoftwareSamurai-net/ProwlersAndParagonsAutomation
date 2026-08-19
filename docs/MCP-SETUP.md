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

If you have no Claude of your own, the site has four recorded conversations at `/portfolio/replay` that
show the same thing, with the engine costing and validating in your browser as you read.

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

**Point your client at that binary rather than at `dotnet run`.** MSBuild writes its own progress to standard output, which is where the protocol lives — a client reading it sees a corrupt stream and drops the session.

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

`--scope user` registers it for **every project on your machine**, which is what you want for a character builder: you are most likely to use it in a session that has nothing to do with this repository. The default scope is `local`, which is this-project-only — fine if you only ever build characters while working on the tool itself, and confusing if you expect it elsewhere. There is deliberately no `.mcp.json` checked in here, because a project-scoped entry needs an absolute path and there is no path that is right on two machines.

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

- **Nothing appears in the client's tool list.** Check the path is absolute and the file exists. The server writes one line to standard error on startup naming the rules directory it found; clients keep that in their MCP log.
- **`claude mcp list` shows it and the session does not.** The session was already running when you added it, or it was added at `local` scope from a different project. Start a new session, and check `claude mcp list` from the directory you are actually working in.
- **It answers with rules you have edited since.** The published binary carries its own copy. Re-publish over the same path, or point `PROWLERS_RULES_DIR` at your checkout's `data/rules` while you are changing them.
- **"The rules files could not be found."** You are running the binary somewhere without its `data/rules/` folder beside it. Either publish again with `-o`, or set `PROWLERS_RULES_DIR` to a directory holding `tiers.json` and the rest.
- **The session drops immediately.** Something is writing to standard output. Point the client at the built binary, not at `dotnet run`.

