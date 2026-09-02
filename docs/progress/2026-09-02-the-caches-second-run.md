# One of the two caches pays and the other does not

**A note, not a slice.** [`2026-09-01-the-build-redoing-its-work.md`](2026-09-01-the-build-redoing-its-work.md)
added two caches and then refused to claim anything for them: *"A cache is worth what its second run
says it is worth and nothing else. Do not quote a figure for those two from this run."* The run that
merged it is that second run. This file is the answer, written separately because that directory's
README says an entry is not edited once its slice is merged.

## The measurement

Run [`33581518450`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33581518450),
green, against `main`'s own [`33476911802`](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/33476911802):

| Step | `main` | first run (caches written) | second run (caches read) |
|---|---|---|---|
| Restore | 11s | 15s | **11s** |
| Build | 58s | 55s | 49s |
| Test | 46s | 45s | 41s |
| Drive the proof harnesses | 50s | 30s | 28s |
| Publish the browser front end | 63s | 4s | 4s |
| The deploy would parse this | 12s | 13s | **3s** |
| saving the caches | — | 10s | 1s |
| **Total** | **270s** | **206s** | **175s** |

## What it says

**The wrangler cache pays: 12s → 3s.** `npx --yes wrangler@<version>` was fetching ~30 MB of
registry every run, and `~/.npm` keyed on the pinned version removes it. The post step cost 0s
rather than 5s, because a hit has nothing to write back.

**The NuGet cache does not: 11s → 11s.** It is not a saving. `main` restores in 11 seconds without
a cache and this branch restores in 11 seconds with one — so what it bought on this run is one more
cache entry and a second of post-step, for nothing.

## It is being kept for one more measurement, and that is a decision rather than an oversight

The case it was added for is not the one that ran. A warm hit on an unchanged dependency graph is
the *easy* case, and 11 seconds is apparently already the floor for it. What `restore-keys` is for
is the **near miss** — a Dependabot bump where the exact key misses, a prefix entry restores, and
`dotnet restore` fetches only the package that actually moved. No run since it landed has been that
case.

So: leave it, and read Restore on the next Dependabot pull request. **If that is also 11 seconds,
delete the step** — a cache that never pays is complexity plus a slot against a ceiling this
repository has already filled once, when Qodana's entries were ~420 MB a branch against 10 GB and
were evicting each other.

**Do not re-measure this by reasoning about it.** The figure that matters is a single number on a
single run:

```bash
gh api repos/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/<id>/jobs --jq '
.jobs[] | .steps[] | select(.name=="Restore") | "\(((.completed_at|fromdate)-(.started_at|fromdate)))s"'
```

## What is not claimed here

**`Build` at 58 → 49 and `Test` at 46 → 41 are not this change.** Nothing in it touches either step
except `PP_PROOF`, which moved work *onto* `Test` rather than off it. Two runs on different runners
differ by more than that on their own, and the honest reading of those two rows is noise. The rows
worth reading are Publish, the proof harnesses and the wrangler step, which moved by amounts no
runner varies by.

Pull request: https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/129
