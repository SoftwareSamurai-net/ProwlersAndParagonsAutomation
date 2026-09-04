# Qodana stops reinstalling an SDK the image already has

**`qodana.yaml`'s `bootstrap:` downloaded 239 MB of .NET SDK on every scan, over an SDK that was
already there and already fine.** Fourteen seconds a run, and the comment above the line had said
for months that it was unnecessary in the ordinary case — *"the bundled 10.0.x SDK already
satisfies `global.json`'s `rollForward: latestMinor`, so this is belt-and-braces for when
`global.json` is bumped ahead of the image."* The belt-and-braces was right; paying for it every
time was not.

From the scan log of [`33472160838`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33472160838):

```
05:05:21  dotnet-install: Attempting to download using primary link .../dotnet-sdk-10.0.100-linux-x64.tar.gz
05:05:33  dotnet-install: Downloaded file size is 239125653 bytes.
05:05:35  dotnet-install: Installation finished successfully.
```

## The premise was asserted for months and is now measured

`jetbrains/qodana-cdnet:2026.2` ships **8.0.422, 9.0.315 and 10.0.301**. With this repository's
`global.json` mounted at `/data/project`, `dotnet --version` inside the image answers **10.0.301**
and exits 0. So `latestMinor` was satisfied before the install started, every time.

## `dotnet --version` is the predicate, because it is the same question the scan asks

Run from a directory holding a `global.json`, `dotnet --version` resolves that file and fails when
nothing installed satisfies it. That is the SDK resolver giving its own answer, rather than a
version string this file compares by hand — which would be a second implementation of a rule that
already exists and would drift from it.

## Three rehearsals in the real image, not three readings of it

| | result |
|---|---|
| this repository's `global.json` | *the image's own SDK 10.0.301 satisfies global.json, so nothing is installed* — **1.1s** |
| a `global.json` demanding `11.0.100` | predicate exits **155** with *Install the [11.0.100] .NET SDK*; the install branch runs |
| `10.0.100` with `rollForward: disable` | install branch runs, installs 10.0.100, **and `dotnet --version` then answers 10.0.100** |

**The third is the one that matters and it is why the second was not enough.** A guard that takes
the install branch has proved it can *decide* to install; it has not proved the install still
works. `11.0.100` does not exist, so that run could only ever have failed on the download — an
honest negative control for the branch and no control at all for the thing the branch does.
`10.0.100` with `rollForward: disable` is unsatisfiable by an image holding 10.0.301 and is a real
SDK, so it exercises the whole path and ends on the positive control: the version the resolver
reports afterwards is the version that was asked for.

## Two smaller things

**The `echo` on both branches is deliberate.** A skipped install and a completed one are equally
silent, and this repository's oldest failure is a step that did nothing looking exactly like a step
that worked. The log now names the branch it took and the SDK it found.

**It is a folded block scalar because a one-liner does not parse.** `bootstrap: if … echo "Qodana
bootstrap: …"` ends the YAML key at the colon-space inside the message. That was written, and
`qodana.yaml` stopped loading — caught by parsing the file rather than by a red scan, which is the
cheaper of the two places to find it.

## Not claimed

**This does not speed up a pull request.** Qodana runs on `main` and weekly, deliberately — see
[the earlier CI entry](../guide/hosting.md#what-each-workflow-costs-and-the-three-things-that-hold-it-down)
— so fourteen seconds comes off a workflow that gates nothing. It is worth doing because it is
fourteen seconds of pure waste with a one-line fix, not because anybody was waiting on it.

**The other two Qodana findings from that investigation are untouched.** The 69-second `docker pull`
of the linter is not cacheable on a hosted runner. And the `bin`/`obj` path exclusions not matching
nested directories is real but was measured at **0.2 seconds** — the artefacts are 842 files and
almost no time — so it was abandoned rather than fixed; the reasoning is in
[the build entry](2026-09-01-the-build-redoing-its-work.md).

Pull request: https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/132
