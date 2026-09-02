# The launch that builds corrupts the stream, and the guard found it on its second day

**A correction to [the two slices that added `.mcp.json`](2026-09-02-the-mcp-server-that-was-never-there.md),
both merged.** They replaced a years-old claim — that pointing a client at `dotnet run` corrupts
the JSON-RPC stream because MSBuild writes to standard output — with a measurement: a launch driven
through a forced full NuGet restore and a recompile put 4,448 bytes on standard output, all of it
protocol. That measurement was real and the conclusion drawn from it was too wide.

## What happened

`TheBuildToolLaunchSpeaksNothingButTheProtocol` failed on a machine where **an MCP server from an
earlier session was still running** — which is the ordinary state of a machine that uses this. The
running process holds `mcp/bin/Release/net10.0/ProwlersAndParagons.Engine.dll`, the build's copy
fails, and MSBuild writes its retries to standard output:

```
Assert.All() Failure: 12 out of 12 items in the collection did not pass.
[0]: Item: "C:\Program Files\dotnet\sdk\10.0.303\Microsof"···
     Error: 'C' is an invalid start of a value.
```

Twelve `MSB3026` lines, in the middle of the handshake. **A client reading that sees a corrupt
stream** — exactly the failure the original warning described, reached by a route nobody had
measured.

## The true statement is narrower than either version

| | Standard output |
|---|---|
| Launch that compiles cleanly | protocol only — 4,448 bytes, 0 non-JSON lines |
| Launch whose copy fails | 12 `MSB3026` lines in the stream |
| Launch that does not build | protocol only, with the lock held and sources touched |
| Not built at all | empty; exit 1; one line on standard error |

So `.mcp.json` passes **`--no-build`**, and `dotnet build --configuration Release` becomes a
**required** step after a clone and after a pull rather than a convenience. The not-built case is
the reason that is safe to require: a server that is not there, with a log line saying so, is a
failure somebody can act on — never a corrupt stream.

## What this says about the guard, which is the part worth keeping

**Neither version of the claim was found by reading.** The first survived years in three documents
and a source comment. The second survived an adversarial review and a merge — and lasted half an
hour against a machine that happened to have a server running.

The guard was written on the argument that *a measurement is not a guard*: reversing old guidance on
one hand-run leaves nothing to catch a future SDK that reintroduces a banner. It did not need a
future SDK. It needed one running process, and it caught the defect **in the change it was written
to hold** — which is the whole of why `CLAUDE.md` says a check is not done until you have broken it
and watched it fail, and why a claim in a document is not a substitute for one.

Mutation, run with the server holding the lock and the sources touched: remove `--no-build` from
`.mcp.json` and the guard goes red again. Restored, it is green under the identical conditions.

## The cost this design has, said before somebody meets it

**A running MCP server blocks a Release build of this repository.** The same lock that corrupted the
stream also stops `dotnet build --configuration Release` copying into `mcp/bin/Release` — measured
here as 10 warnings and 2 errors, which cleared the moment the server process was stopped and then
gave 0 and 0 with all 4,058 and 817 tests passing.

That is a consequence of the registration pointing at the checkout's own build output, and it is
worth knowing rather than solving: **stop the client, or kill the server process, before a Release
build.** CI is unaffected, because no client is connected there. The alternative — a published copy
somewhere else — is the install path whose disappearance was item 18, and trading a build-time
inconvenience for that is not a trade worth making.

## Where the corrected claim now lives

`docs/MCP-SETUP.md` (sections 0 and 1), `docs/guide/mcp-and-headless.md`, `mcp/Program.cs`'s header
and the guard's own doc comment. Each says which launch is safe rather than whether the build tool
is, because "is `dotnet run` safe here" has no answer and "does this launch build" does.
