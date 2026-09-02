# The running server was reading out of the directory the build writes to

**Branch** `claude/mcp-server-release-build-9e6dc0`. The pull request link belongs here.

The claim under investigation was one this repository had already written down, in
[the previous entry](2026-09-02-the-launch-that-builds-corrupts-the-stream.md): *a running MCP
server blocks a Release build of this repository, measured as 10 warnings and 2 errors that cleared
to 0 and 0 once the process was stopped.* It was recorded as a cost to live with, and the mitigation
was a sentence telling a human to stop the client first.

It reproduces exactly, and the mitigation was the wrong shape.

## What was measured, 2026-09-02, SDK 10.0.303

| Server running from | `dotnet build` | `dotnet build --configuration Release` |
|---|---|---|
| `mcp/bin/Release/` | succeeded, 0 warnings, 0 errors | **FAILED, 10 warnings, 2 errors** |
| nothing | succeeded, 0/0 | succeeded, 0/0 |
| `mcp-server/` (published) | — | **succeeded, 0/0** |

**The ten and the two are not a coincidence of counts.** They are MSBuild's `Copy` retry default:
ten `MSB3026` retries at one second apart, then `MSB3027` and `MSB3021`. All twelve messages name
one file and one process:

```
error MSB3027: Could not copy ...\engine\bin\Release\net10.0\ProwlersAndParagons.Engine.dll
to "bin\Release\net10.0\ProwlersAndParagons.Engine.dll". Exceeded retry count of 10.
The file is locked by: "ProwlersAndParagons.Mcp (19664)"
```

**Three things in that message were not in the original account and change what the fix should be.**

- **The locked file is `ProwlersAndParagons.Engine.dll`, not the server's own assembly.** The server
  loads the engine, so it holds the engine's copy in `mcp/bin/`. Nothing else in the solution fails;
  the whole build gets as far as the `mcp` project and stalls there for ten seconds.
- **The blast radius is one configuration.** A Debug build is untouched, because the server runs
  Release binaries. So plain `dotnet test`, `git push` and CI never see it, and
  `dotnet build --configuration Release`, `dotnet test --configuration Release` and the CI repro all
  do.
- **It is a per-directory lock, so a worktree only blocks itself.**

**And PID 19664 was alive while the report said it had been killed.** Disconnecting the tools in a
session does not reliably reap the process. That is what disqualifies "stop the client before a
Release build": it is a step that can appear done and not be, and its failure mode is a confusing
`MSB3027` ten seconds into somebody else's build.

## The fix is that the server's read path was the build's write path

`.mcp.json` now runs a copy published out of the way:

```json
"command": "dotnet",
"args": ["exec", "mcp-server/ProwlersAndParagons.Mcp.dll"]
```

**Nothing is lost in freshness, which is the objection to check first.** The registration already
passed `--no-build`, so the running server was already a frozen snapshot of the last Release build.
It is now a frozen snapshot of the last publish. The lock was buying nothing.

**The trade is which command wants the server stopped.** `dotnet publish -c Release -o mcp-server`
now does — it writes into the directory the server reads from — and everything on the way to a push
does not. Publishing is deliberate and rare; a Release build is what an agent does several times an
hour.

**`exec` is load-bearing and was chosen by measurement.** For the never-published case:

| Command, against a file that is not there | Exit | stdout | stderr |
|---|---|---|---|
| `dotnet mcp-server/Missing.dll` | 1 | **303 bytes** of "Possible reasons for this include" | one line |
| `dotnet exec mcp-server/Missing.dll` | 129 | **empty** | one line naming the path |

Standard output is where the protocol lives. Plain `dotnet <dll>` would have re-introduced, in the
one case a new user is most likely to hit, exactly the class of defect the three previous
corrections in this area were about. `McpSetupDocumentationTests` asserts the word.

## The guards, each broken and watched to fail

`TheProjectRegistrationNamesAProjectThatIsThere` asserted the registered `--project` was on disk.
There is no longer a project in the registration, and the published copy it names is git-ignored
build output, so *existence* is not assertable in a clone. What replaced it is the pairing that
actually rots — **the guide publishes to a directory and the registration runs an assembly out of
that same directory** — matched by destination rather than by document order, so it cannot be
satisfied by whichever fenced block happens to come first.

`TheCheckedInRegistrationRunsWhatSectionZeroPublishes`, four mutations, four reds:

| Mutation | Failed on |
|---|---|
| drop `"exec"` from the args | `Assert.Equal` on the first argument |
| point the args at `mcp-srv/` | the directory is not one `.gitignore` ignores |
| rename the assembly to `ProwlersAndParagons.Server.dll` | assembly name from the `.csproj` |
| section 0 publishes nothing (back to `dotnet build`) | no publish command lands in that directory |

`TheBuildToolLaunchSpeaksNothingButTheProtocol` is renamed
`TheCheckedInRegistrationSpeaksNothingButTheProtocol`, because it no longer starts the build tool.
Its pre-step changed from "build Release if the binary is missing" to "publish if it is missing",
and **the reason the old one did not assert the build's exit code now applies to the new one, one
directory along**: with a server running out of `mcp-server/`, the publish fails while leaving a
perfectly good binary in place. What matters is the file.

Two runs against it:

- **Registration points at `mcp-server/Nope.dll`** — red, on the positive control:
  *"The registration in .mcp.json produced no reply to initialize at all."* The launch being broken
  is caught as a broken launch rather than as an empty stream that passes.
- **`mcp-server/` moved aside entirely**, which is CI and a fresh clone — green in 2 seconds,
  having published it. That is the case the pre-step exists for and it was measured rather than
  assumed.

## What this does not claim

**The stray-line assertion in that test was not re-broken here.** It is the same assertion as
before, on a different launch; the mutation that exercises it is a write to standard output inside
the server, which the source scan and `TheBuiltProgramSpeaksNothingButTheProtocol` already cover.
Recording it as newly-proven coverage would be the mistake `CLAUDE.md` names.

**And the lock is not gone, it is relocated.** Anybody who has a client running from an older
registration still holds `mcp/bin/Release`; `docs/MCP-SETUP.md` now has a troubleshooting entry that
names `MSB3027` and says to read the holding process out of the message.
