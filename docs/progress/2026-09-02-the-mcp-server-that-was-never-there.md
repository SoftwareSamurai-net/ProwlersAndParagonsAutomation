# The MCP server that was never there, and a registration the repository can now check

**Item 18 said the cause was unknown and that the first step was to reproduce it and read the
server's own stderr.** There is no stderr. That is the finding.

## What was actually wrong

`~/.claude.json` registered the server at

```
C:\Users\dorians\AppData\Local\ProwlersAndParagons\mcp-server\ProwlersAndParagons.Mcp.exe
```

and neither `mcp-server\` nor its parent `ProwlersAndParagons\` was on disk. Running that path by
hand gives `No such file or directory` and exit 127. **A client sees a process that dies before it
speaks, and reports `CONNECTION_CLOSED`** — which is why a whole session's worth of looking for a
server log found nothing. There was no process, so there was no log.

The registration itself was correct, and matched [`docs/MCP-SETUP.md`](../MCP-SETUP.md) verbatim.
Step 2 of that guide had plainly been followed. Step 1 — the publish — either was never run, or the
directory it wrote to stopped existing afterwards.

**The server was never at fault.** Published from the checkout and driven over stdio, it answers
`initialize` and `tools/list` with 4,448 bytes of protocol on standard output, one line naming the
rules directory on standard error, and a clean exit. All six tools are present. Re-publishing to
the registered path and running `claude mcp list` returns `✔ Connected`.

**One observation that is not proof and is worth writing down anyway.** The publish was run from
inside a shell hosted by the packaged (MSIX) Claude app, and the resulting binary reported its own
`BaseDirectory` as
`…\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\ProwlersAndParagons\mcp-server` —
the container's redirected view of `%LOCALAPPDATA%`. A publish and a launch on opposite sides of
that boundary would produce exactly the symptom above. Whether that is what happened originally is
not established, and it is recorded as a hazard of installing to `%LOCALAPPDATA%` from inside a
packaged host rather than as the cause.

## The fix is to stop having an install path

A path outside the checkout is a fourth thing to keep in step with three that are already in git,
and **no test can see it** — the registration lived in a file on one machine that nothing in this
repository had ever heard of. That is the whole reason this failure could sit unexamined for a
session: there was no place to write a check.

[`.mcp.json`](../../.mcp.json) at the repository root registers the server project-scoped, through
the build tool, at a path *relative* to the checkout:

```json
"command": "dotnet",
"args": ["run", "--project", "mcp/ProwlersAndParagons.Mcp.csproj",
         "--configuration", "Release", "--verbosity", "quiet"]
```

A clone, a fresh machine, another operating system and a git worktree are then all correct without
anybody publishing anything. Two caveats, both real:

- **Claude Code asks once, per checkout, before starting a server a repository proposed.**
  `claude mcp list` reports *Pending approval* until an interactive session answers it, or until
  `enableAllProjectMcpServers` is set in that project's `.claude/settings.json`. Measured: a probe
  entry added to `.mcp.json` was picked up and held at exactly that gate.
- **The first launch compiles**, so `dotnet build --configuration Release` once after cloning keeps
  a client from waiting on a build. The guide says so.

## The blanket warning about `dotnet run` was too broad, and was in the way

The guide said MSBuild writes its progress to standard output, so a client pointed at the build tool
sees a corrupt stream. On the .NET 10 SDK this repository pins, **it does not**. Driven through a
forced full NuGet restore — `NUGET_PACKAGES` pointed at an empty directory, which the run then
populated with four packages, so the restore demonstrably happened — plus a recompile of `engine`,
`sheets` and `mcp`, standard output carried 4,448 bytes and **zero non-JSON lines**. The same run
without `--verbosity quiet` was also clean, but that one has no positive control proving a rebuild
occurred, so the flag stays.

`NoCommandInTheGuideStartsTheServerThroughTheBuildTool` survives with a different justification,
written into its doc comment: a fenced block is the part people copy, every fenced block in that
guide is for a client working *outside* a checkout, and `dotnet run` needs the checkout.

**The measurement was the whole justification, and a measurement is not a guard.** A review of this
slice made the point that reversing years-old guidance on one hand-run on one warm machine leaves
nothing to catch a future SDK that reintroduces a banner — and `.mcp.json` now *depends* on that
guidance being wrong, which turns a claim in a document into a property of the product.
`TheBuildToolLaunchSpeaksNothingButTheProtocol` holds it: it reads the command and arguments out of
`.mcp.json` (a copy here would go on passing after somebody edited the registration), starts them
from the repository root the way a client starts a project-scoped server, and requires every line
of standard output to parse as JSON.

The same review found the fresh-machine case untested — the dotnet CLI's one-time welcome and
telemetry notice is exactly the stray-stdout failure this design must not have, and it cannot
appear on a machine that has run `dotnet` before. `.mcp.json` sets `DOTNET_NOLOGO`,
`DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` rather than relying on a
measurement that could not have observed it.

## Two other documents were still asserting the corrected claim

Also from the review, and the more serious finding of the two: `docs/guide/mcp-and-headless.md` and
`README.md` both still said a client must point at the built binary because `dotnet run` writes to
standard output. `CLAUDE.md` sends anybody touching `mcp/` to that guide first, so leaving it would
have handed the next reader the reasoning this slice had just measured false. Both now give the
reason that is actually true — `dotnet run` needs the checkout, so publishing is for a client that
has not got one — and the README says that a checkout already has the server.

## The guards

Two new ones in `McpSetupDocumentationTests`, both broken and watched to fail:

| Guard | Mutation | Result |
|---|---|---|
| `TheProjectRegistrationNamesAProjectThatIsThere` | `mcp/Gone.csproj` | red |
| `TheProjectRegistrationIsRelativeToTheCheckout` | `C:/somewhere/…csproj` | red |
| `TheBuildToolLaunchSpeaksNothingButTheProtocol` | a `Console.Out.WriteLine` in `Program.cs` | red |
| — its positive control | `.mcp.json` naming `mcp/Gone.csproj`, so nothing starts | red |

The first is the guard for this exact failure in the only place it can now be caught: a
registration that names something not on disk. It is deliberately about the path and not the flags
beside it.

`EveryRelativeLinkInTheGuideResolves`, which already existed, now covers the guide's link to
`.mcp.json` for free.
