# The build spent a third of its four minutes on work it had already done

**[The earlier CI entry](../guide/hosting.md#what-each-workflow-costs-and-the-three-things-that-hold-it-down) cut the *number* of runs; this one cuts what one run costs, and it is
a different exercise.** That one was about triggers — cancelling superseded runs, taking Qodana off
pull requests, skipping two documentation files. None of it touched a step. This started from the
per-step timings of a single green run and asked what each second was buying.

The run measured is `33473271163`, **241 seconds**, and it is worth re-reading rather than trusting
this table:

```bash
gh api repos/SoftwareSamurai-net/ProwlersAndParagonsAutomation/actions/runs/<id>/jobs --jq '
.jobs[] | "JOB: \(.name)", (.steps[] | "  \(((.completed_at|fromdate)-(.started_at|fromdate)))s  \(.name)")'
```

| Step | Seconds | What was actually happening |
|---|---|---|
| Publish the browser front end | 51 | **27.5 of them Brotli** |
| Drive the proof harnesses | 50 | a 782-test suite re-run, then 21 sequential Chrome launches |
| Build | 40 | — |
| Test | 35 | of which 28 is the suite the step below re-ran |
| The deploy would parse this | 18 | `npx` fetching the same wrangler tarball as last time |
| Restore | 16 | **no cache of any kind existed in this repository's workflows** |
| everything else | 31 | — |

#### Five changes, and the measurement that justifies each

- **Brotli is 27.5 seconds of the publish, and this step cannot see a `.br` file.**
  `-clp:PerformanceSummary` on a local publish puts `BrotliCompress` at **27,543 ms** across two
  calls — an order of magnitude above `Csc` at 3,658 ms, which is the next thing down. It produces
  209 `.br` and 209 `.gz` sidecars and takes the output from **29 MB to 49 MB**. What the pull
  request publishes *for* is the two steps after it: that every `data/rules/*.json` plus
  `index.html`, `_headers` and `_redirects` landed, and that `write-cloudflare-headers.sh` can
  still find the inline import map to hash. Both were run against an uncompressed publish before
  the flag was written into the workflow — twelve rules files, all four required files, and the
  script writing `_headers` with its one script hash — so `EnableDefaultCompressionFormats=false`
  is a measurement rather than a reading of the SDK's targets.

  **`deploy.yml` is deliberately left alone.** Whether Cloudflare Pages ever *serves* those
  sidecars or merely stores them is a claim about production that nothing here has tested, and the
  place to answer it is against the live site. It is the same 27 seconds and a third of the upload
  if it holds; it is its own change, with its own proof.

- **The 782-test bUnit suite ran twice per build, and the second run existed to set a variable.**
  The log says it plainly — `Passed! ... 782 ... Duration: 28 s` in the Test step, and
  `Passed! ... 782 ... Duration: 11 s` in the browser step eleven seconds later. `PP_PROOF` gates
  only whether `ProofPages` *writes* the harnesses; the assertions are identical either way, which
  `ProofPages.cs` says in as many words. So the variable moved onto the Test step and the second
  `dotnet test` is gone. **Nothing is skipped**: the same 782 tests run, once. The step ordering is
  now load-bearing and the comment says so — anything reading `web/wwwroot/proof-*.html` must come
  after the Test step.

- **Twenty-one Chrome launches, strictly one after another.** From the log's own timestamps: the
  first is cold and takes **17.5 seconds**, the twenty after it take about **0.95 seconds each**,
  on a four-core runner doing nothing else. They are independent — each already gets its own
  `--user-data-dir` — so the loop body became a function driven by `xargs -d '\n' -P 4`.
  `-d '\n'` is not decoration: the default splitting tears `STICKY: PASS` into two arguments.

- **There was no cache of any kind in any workflow.** `~/.nuget/packages`, keyed on the project
  files, `Directory.Build.props` and `global.json`, with a prefix `restore-keys` so a one-package
  bump costs a delta rather than a cold restore. **Not `setup-dotnet`'s own `cache:` input** — that
  wants a `packages.lock.json`, which this repository does not have.

- **`npx --yes wrangler@<version>` refetched ~30 MB every run.** The version read moved into its
  own step with an output, because a cache cannot be keyed on a value computed inside the step that
  uses it, and `~/.npm` is now cached against that exact version. **This caches the download, not
  the install** — `npx` still unpacks into a fresh directory.

#### The step gained a positive control it never had, and that is not incidental

`xargs` over an empty list exits **0** having run nothing, and so did the `while read` loop before
it — a mangled `checks` string read as twenty-one passes. That is this repository's oldest failure
shape, sitting inside the step whose comment is three paragraphs about not trusting a verdict. The
step now counts the `ok` lines and requires the expected number, and says which of two different
things went wrong: a page reaching the wrong verdict, or no page running at all.

**Four rehearsals against a stub Chrome, before any of this reached CI.** The step's own `run:`
block was extracted from the YAML and driven with a stub that derives a verdict from the page name
alone, so what is under test is the workflow's parsing and aggregation rather than the stub:

| Mutation | Result |
|---|---|
| none — every page agrees | 21 `ok`, exit 0 |
| one page's verdict inverted | that page named with its actual title, the other 20 still `ok`, exit 1 |
| every page left at its resting `measuring` text | all 21 reported, exit 1 |
| the `checks` list emptied | *the proof checks list is empty*, exit 1 |

**The third and fourth are the ones worth having.** A harness whose script never ran leaves neither
verdict, and an empty list runs nothing — both were previously green.

**And the first draft failed one of them honestly.** With a page inverted, the count guard fired
first and reported *"the list has stopped matching"* — a true failure with a misdiagnosis attached,
which is how somebody spends an afternoon on the wrong thing. The two guards are now separate
sentences in a fixed order.

#### What it actually did, measured against `main` rather than against the baseline above

**The 241-second run is where the work came from and is the wrong thing to compare against.**
`main` moved six commits while this was open — a whole join-link slice, eight more bUnit tests — so
some of the difference in a before-and-after against it would be the tree growing rather than
anything here. The honest comparison is `main`'s own Build run of `998322e` against this branch
rebased onto it: **the same content, two workflows.**

| Step | `main` | this branch |
|---|---|---|
| Restore | 11s | 15s |
| Build | 58s | 55s |
| Test | 46s | 45s |
| Drive the proof harnesses | 50s | **30s** |
| Publish the browser front end | 63s | **4s** |
| The deploy would parse this | 12s | 13s |
| saving the two caches | — | 10s |
| **Total** | **270s** | **206s** |

**Publish 63 → 4 and the proof harnesses 50 → 30 are the two that landed**, and they are the two
that needed no cache. Publish beat its own prediction because Brotli was the *only* cold work left
in it: the Build step above has already compiled everything it needs.

**The two caches are a net cost on this run and are honestly unproven.** Restore went *up*, from 11
to 15, and the wrangler step did not move — because this run wrote both caches rather than reading
either, and saving them cost a further 10 seconds in the post steps. A cache is worth what its
second run says it is worth and nothing else. **Do not quote a figure for those two from this run.**

**This slice adds no test, and that is the measurement rather than an omission.** All five suites
were re-run on the rebased tree with `./scripts/count-tests.sh` — **4055 / 817 / 254 / 14 / 19 =
5159**, all green, and none of it this slice's. What changed is a workflow: `WorkflowFilterTests`'
guards over `build.yml` were already there and still pass. What stands in for a new test is the
four rehearsals above, which is the honest instrument for a step that only a runner can execute.

**That figure is quoted here and deliberately not carried anywhere else.** `PROGRESS.md`'s Tests
row stopped holding counts while this branch was open, for the reason `testing.md` records — they
went wrong four separate ways — so this is a reading taken on one tree on one day, and the way to
get today's is to run the script.

#### What was measured and deliberately not changed

- **Splitting the job.** Node's three suites need no .NET and the publish half needs no test result;
  three parallel jobs would put wall clock near 100 seconds. It costs roughly **twice the runner
  minutes**, because the web job re-pays restore and build. Worth it if pull-request latency is what
  you are buying and not if the Actions bill is — and [the earlier CI entry](../guide/hosting.md#what-each-workflow-costs-and-the-three-things-that-hold-it-down) records that the bill was
  never the binding constraint.
- **Qodana's 329 seconds.** 69 of them are `docker pull` of the linter, which nothing on a hosted
  runner can cache. 14 are the `bootstrap:` line reinstalling SDK 10.0.100 the image already
  bundles. And **842 of the 2,356 files it analyses are build artefacts** — `project.nuget.cache`,
  `*.assets.cache`, `UglyToad.PdfPig.*.dll` — because `qodana.yaml` excludes bare `bin` and `obj`,
  which are root-relative and so miss `engine/obj/` and every sibling. That is a real finding and a
  separate change; it is off the pull-request path, so it blocks nobody.
- **Build at 40 seconds**, which is a solution-wide compile that is already parallel.
- **Build running again on push to `main`.** A scan of `main` as merged is a different claim from a
  scan of the branches that went into it — the same reasoning [the earlier CI entry](../guide/hosting.md#what-each-workflow-costs-and-the-three-things-that-hold-it-down) turns on.
### The Qodana scan left its export behind, and on Windows that orphaned a worktree

**`scripts/qodana-scan.sh` deleted its staging directory at the *start* of a run and never at the
end.** That was invisible until it was not: Qodana *builds* the project it is given, so
`.qodana-scan/project/` does not stay the clean tree `git archive` wrote — it grows
`obj/Release/net10.0/…` under every test project, about half a gigabyte, and every worktree that had
ever run a scan was still holding one.

**The failure it caused is worse than the disk.** A worktree root under `.claude/worktrees/` is
already ~125 characters; the deepest path in that export lands near 288, and Windows' 260-character
limit starts refusing operations on it. `git worktree remove` then fails with **"Filename too
long" *after* deregistering the worktree** — so the worktree is gone from `git worktree list`, the
files are still on disk, and git can no longer help delete them. That is what happened cleaning up
after the leave/notify/view slice.

**`results/` and `qodana.log` survive and the export does not**, because the first two are what the
run is for and what a failure is read from, and the third is `git archive <commit>` — one command
reproduces it exactly.

**It is a `trap … EXIT`, not a line at the end of the happy path.** The SARIF-missing bail-out and a
Ctrl-C are the runs most likely to leave a mess, and a trap that only fires when nothing went wrong
never fires on them. Watched by planting an `exit 1` immediately after the trap: the script exited
1, `project/` was gone and `results/` was not.

**Related and not fixed here:** `core.longpaths` is unset in this repository and globally, so git is
capped at 260 characters whatever the OS allows; `git config --global core.longpaths true` is one
command and covers every clone. The Windows `LongPathsEnabled` policy is the general fix and needs
administrator rights. Neither is necessary now that the export goes.

Pull request: https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/125
