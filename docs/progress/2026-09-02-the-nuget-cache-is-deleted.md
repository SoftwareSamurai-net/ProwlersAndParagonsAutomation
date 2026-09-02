# The NuGet cache is deleted, and yesterday's entry was too kind to it

**This corrects [`2026-09-02-the-caches-second-run.md`](2026-09-02-the-caches-second-run.md), which
merged an hour earlier.** That entry called the NuGet cache *neutral* — "11s → 11s, which is no
saving" — and kept it pending one more measurement on a Dependabot bump. Both halves are wrong. It
is not neutral, it is a **net loss**, and the measurement that says so was already in hand when the
entry was written. It is deleted.

## What the earlier entry left out: the cache step costs time too

It compared `Restore` against `Restore` and stopped there. A cache is not free to read — pulling
190 MB of `~/.nuget/packages` is itself a step on the clock, and counting it changes the sign of
the answer.

Both runs below **hit** the cache on the same key, which is the best case a cache has:

| | cache step | post step | the step it helps | total | `main`, no cache |
|---|---|---|---|---|---|
| NuGet | 5s | 0–1s | `Restore` 11s / 15s | **16–21s** | **11s** |
| wrangler | 4s | 0s | bundle 3s | **7s** | **12s** |

Runs [`33581518450`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33581518450)
and [`33582926328`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33582926328);
`main`'s own [`33476911802`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33476911802)
is the no-cache column.

**So the NuGet cache spends five seconds to save nothing.** `dotnet restore` takes 11–15 seconds
whether or not the packages are already on disk, and `main` was doing it in 11 with an empty
`~/.nuget`. Whatever that step is spending its time on, it is not the download.

**And the control was run rather than argued.** This change's own build —
[`33583956160`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33583956160),
the first with the step actually gone — restored in **13 seconds**, which is the middle of the band
two cache *hits* produced. That is the reading that matters, because it is the only one taken with
the cache removed rather than inferred from `main` before it existed.

**The wrangler cache is kept, on the identical arithmetic reaching the opposite answer**: 4 seconds
to restore 133 MB, against a 12-second `npx` fetch from the registry. Two caches, added in one
change, measured the same way, and only one of them earns its place — which is the argument for
measuring a cache rather than reasoning about one.

## The two ways the earlier entry got there

**It compared the wrong pair.** `Restore` against `Restore` is the natural comparison and it is
incomplete, because adding a cache adds a step. The comparison that decides is *everything the
cache touches* against *the same thing without it*.

**And it treated a noisy figure as a stable one.** `Restore` came back 11s on one hit and 15s on
the next — a 36% spread between two runs that differ in nothing that matters. The earlier entry
read 11s and 11s and wrote "11s → 11s" as though the number were solid. It reversed its own
reasoning as soon as a third reading existed.

The delete-condition it wrote — *wait for a Dependabot bump, and if `Restore` is still 11s, remove
the step* — was deferring to a future measurement when the present one already answered. A
near-miss on `restore-keys` could only ever make the cache **slower** than a hit, and a hit is
already a loss.

## What is not claimed

**Deleting this does not make the build five seconds faster in a way anybody will see.** The step
was 5s of a 175s build. The reason to remove it is that it is machinery earning nothing — plus a
190 MB entry against a repository cache ceiling this project has already filled once, when
Qodana's per-branch entries were ~420 MB against 10 GB and evicting each other.

`build.yml`'s `Restore` step now carries the arithmetic in a comment, and says not to re-add the
cache without a run showing `Restore` beating 11 seconds.

Pull request: https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/130
