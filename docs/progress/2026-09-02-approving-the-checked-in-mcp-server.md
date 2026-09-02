# The approval gate on the checked-in MCP server, and why `claude mcp list` cannot tell you about it

**A correction to [the slice that added `.mcp.json`](2026-09-02-the-mcp-server-that-was-never-there.md),
written as its own file because that one is merged.** That entry said the one approval Claude Code
asks for could be answered in advance with `enableAllProjectMcpServers` in a project's
`.claude/settings.json`. That was reasoned about rather than tried, and it is not what works.

## What works, measured

**`enabledMcpjsonServers` in the checkout's `.claude/settings.local.json`**, which is git-ignored,
so it approves the server for the person who wrote it and not for everybody who clones — which is
the right shape for a gate whose whole purpose is that running a program a repository handed you is
a decision:

```json
{ "enabledMcpjsonServers": ["prowlers-and-paragons"] }
```

**The per-project key of the same name in `~/.claude.json` does not work.** It is there, it takes
the value, and it changes nothing.

## And `claude mcp list` is wrong about it in both directions

It printed `⏸ Pending approval` before the approval and after it. There is no state of this
repository in which that line has been informative.

**So the check had to be behavioural**, which is also the only kind that survives the harness
changing under us: a headless session either has the tools or does not.

```bash
claude -p "Do you have a tool named mcp__prowlers-and-paragons__creation_guide? Answer YES or NO only."
```

| Approval in | `claude mcp list` | Headless session |
|---|---|---|
| nothing | Pending approval | `NO` |
| `~/.claude.json`, per project | Pending approval | `NO` |
| `.claude/settings.local.json` | Pending approval | `YES`, all six tools |

The middle row is the finding. The first slice would have shipped the third row's instruction as
the second row's key, and the symptom would have been a server that stays silently absent while
every visible signal says the setup is correct — which is, once again, the shape of the failure
item 18 was.

## Why the documents say the command and not just the answer

Four places now carry this — `docs/MCP-SETUP.md`, `README.md`,
`docs/guide/mcp-and-headless.md` and `PROGRESS.md` — and each names the `claude -p` check rather
than only the setting. **Which key a client honours is somebody else's software and will move.** A
document that states today's answer goes quietly stale; one that hands over the command that
answers it stays true, and this repository has been bitten by the difference before.
