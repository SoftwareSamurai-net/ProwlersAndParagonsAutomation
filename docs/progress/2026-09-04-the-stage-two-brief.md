# The brief for stage two, written before any of it was done

**This file is the one thing in this directory that is not an account of finished work.** Everything
else here says what a slice did; this says what the next one is being asked to do. It is here rather
than in `PROGRESS.md` because it is long, because it is a single author's argument that no second
branch will want to edit, and because a file per slice does not collide — the reasons the directory
exists in the first place. [`PROGRESS.md`](../../PROGRESS.md) item 10 remains the statement of what
is open; this is only the working brief for it, and item 10 links here.

**Written 2026-09-04**, immediately after stage one merged as
[#149](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/149). Every figure
in it was measured on the runner that day. **When stage two lands it writes its own file**, and this
one is not edited to match what actually happened — the archive's rule about not rewriting entries
applies here for the same reason: a brief edited into agreement with its outcome stops being
evidence of what was known beforehand.

**The retirement count it quotes is a snapshot and goes stale by design.** It says one; run the
`gh run list` command item 10 carries rather than believing this file.

---

You are finishing what `PROGRESS.md` item 10 has left open. Stage one is built and merged — two
drivers, six checks, six deliberately-broken twins, both run by `build.yml`. What remains is the
signed-in half, two behavioural gaps the driver can now close, one fix that was never proved, and
the retirement of the harness that is now redundant. Land it in reviewable pieces and leave the
repository with a verification suite other automation can depend on.

## Read before planning anything, in this order

`CLAUDE.md` — all of it, twice if the disciplines look like boilerplate; they are not. Then
`PROGRESS.md` item 10 end to end, including **"What has to be true before `scripts/e2e/` is
deleted"** and **"One thing the accessibility check found that is a decision, not a defect"**.
Then `docs/guide/testing.md`'s *Driving the assembled app* and *The pixel diff, and why the goldens
are the fragile part*. Then `docs/progress/2026-09-04-a-second-driver-and-what-it-measured.md`,
which is the account of stage one and records three faults the harness had in itself. Then
`docs/guide/accounts-server.md`, and `docs/guide/hosting.md` on the job cap and what each workflow
costs.

Then read the code: `scripts/e2e.sh`, `scripts/e2e/defects.mjs`, `tests/e2e/Harness.cs`,
`tests/e2e/Runner.cs`, `tests/e2e/Program.cs` and every file in `tests/e2e/Checks/`.
`Checks/Boot.cs` is the worked example of the shape and comment density expected;
`Checks/Accessibility.cs` is the one with the most history in its comments and the most to learn
from.

## What exists, and its measured cost

`./scripts/e2e.sh [--driver node|dotnet]` publishes the site, serves it with the `wrangler pages
dev` version `deploy.yml` pins, and drives real Chrome. `node` is the hand-rolled DevTools
Protocol client and runs five checks; `dotnet` is `tests/e2e` over `Microsoft.Playwright` and runs
those five plus `A11Y`, which is axe-core inside the page. Each twin is driven with
`--only <CHECK>`, so a twin run costs one check rather than six.

**Measured on the runner, two consecutive green runs: the Build job is 1103s and 1132s — 18m23s
and 18m52s — against a `timeout-minutes: 30` cap.** Per step, node 392s/395s and Playwright
521s/528s, steady to within one percent. **Any plan that adds to that job has to say what it
drops.** `PROGRESS.md` item 10 names the levers in order; the first is dropping a driver from the
workflow, and raising the cap is not one of them — read `docs/guide/hosting.md` for why.

## Facts to verify yourself before building on them

These were established in the session that built stage one. Confirm each; if any is wrong, the plan
changes.

1. **`worker/tokens.js` stores only the SHA-256 of a sign-in token**, `worker/crypto.js`'s `hash`
   is plain WebCrypto which Node provides identically, and `db.spendLoginToken` verifies by hash
   lookup and burns the row. This is what makes stage two test-seeding rather than a bypass.
2. **`wrangler pages dev` bundles a `functions/` directory found in the *working directory*** and
   there is no flag for it. `e2e.sh` runs from `.e2e/` specifically so none is found. Stage two
   needs the opposite, which is a change to how the server starts and not only new checks.
3. **`scripts/apply-migrations.sh` already creates and migrates a local D1.** Read it rather than
   writing a second thing that does the same job.
4. **Playwright can emulate `prefers-reduced-motion`** (`BrowserNewContextOptions.ReducedMotion`)
   and can send a real key chord. Both matter below. Confirm the .NET API surface; do not assume
   the JavaScript one.

## The five pieces of work

### 1. Stage two: a signed-in session, seeded from outside the application

**Do not open a seam in the application.** `PROGRESS.md` item 10 argues this at length and the
argument is settled: generate a token in Node, hash it, `INSERT` the row into the **local** D1, and
drive `/signin?token=<raw>`. The app then runs its real verify path — hash lookup, expiry test,
single-use burn, session cookie — and nothing is faked but a row, which is what an email would
have caused. No code in the shipped bundle, no secret, no localhost test, nothing to compile out.

What it needs: `functions/` bundled, a D1 binding, and a migrated local database. What it must not
do: touch the deployed site, or the real mail send, which is `scripts/probe-mail.mjs`'s job.

**The checks worth having behind sign-in**, and each needs a twin: the rulebook at `/rules` serving
real prose to an account and refusing an anonymous reader; a character saved to the server rather
than to local storage and surviving a different browser context; and the admin page refusing a
signed-in account that is not the owner. **The third is the one that matters most and is the
easiest to get vacuous** — a check that asserts "not allowed" passes against a page that failed to
load at all, so its positive control has to prove the page rendered *and* that the reader was
signed in as somebody.

### 2. `motion.js` and `palette.js`, which are still only a `file://` proof page

Item 10 names both as gaps stage one did not close. Both are behavioural and both are now reachable:

- **Reduced motion.** `motion.js` has a `still()` guard. Two guards in `WebPresentationTests`
  assert only that the file *mentions* `still()` and `setTimeout`, and `CLAUDE.md` records that
  both pass against a script doing the opposite — `|| !still()) return` is one character and
  serves the animation to exactly the people who asked for none. A driver with
  `ReducedMotion = "reduce"` can ask whether anything animated.
- **The Ctrl-K chord.** `palette.js` binds it. Nothing has ever pressed it in the running app.

**Read `Checks/Accessibility.cs`'s comment about waiting on `getAnimations()` before you write the
motion check.** It is the same mechanism from the other side: that check waits for animations to
*finish*, and this one has to prove they never *started*. A check that asserts "nothing animated"
is satisfied by a page that never rendered, so the positive control is that the same page animates
when reduced motion is off.

### 3. Prove `kill_tree`, which fixed a real leak and was never watched to work

`scripts/e2e.sh`'s `stop_server` used `pkill -P` on Linux, which kills direct children only —
wrangler's tree is `npx` → node → `workerd`, so `workerd` outlived its step still holding a port.
It is `kill_tree` now, reading ppid out of `/proc/<pid>/stat`.

**The green CI runs do not prove it**, and this is the point: `port_in_use` alone masks the leak by
stepping over the held port, so the harness would be green either way. Prove it directly — start a
server, stop it, and assert nothing is listening and no `workerd` remains — and note that the
obvious fixture collapses, because `bash -c` with a single command execs it and you get one process
instead of three. Run the Linux path in a container: `MSYS_NO_PATHCONV=1 docker run --rm -v
"$(pwd -W):/work"`, `tr -d '\r'` the script first because a mounted Windows tree is CRLF, and do
not assume the image has `ss`, `pgrep` or `python3` — `mcr.microsoft.com/dotnet/sdk:10.0` has none
of them.

### 4. The contrast decision the harness reported and did not make

`A11Y` exempts one node: the wizard's Next control before a tier is chosen, at **2.23:1
Hero/Light, 3.28:1 Hero/Dark, 2.54:1 Villain/Light, 3.22:1 Villain/Dark** against a 4.5:1 floor.
WCAG 1.4.3 exempts inactive components and this one is inactive, so the exemption is correct in
conformance terms — `PROGRESS.md` item 10 records that legibility is a separate question and the
owner's call.

**Do not decide it yourself.** Put the options to the owner with the numbers: raise
`.btn.disabled`'s opacity, or give the step a sentence saying what is missing — which the Campaigns
page already does for a disabled Join, and which `PROGRESS.md` argues is the better shape. If they
choose a change, the exemption comes out in the same slice and `A11Y` must stay green without it.

### 5. Retire `scripts/e2e/`, when and only when the condition is met

**The condition is written down and is not a feeling**: twenty consecutive green `Build` runs on
`main` in which the `--driver dotnet` step reported six checks green and all six twins red. Item 10
carries the `gh run list` command. As of the merge of #149 the count is **one**.

**So this is almost certainly not the slice that deletes it**, and the honest thing is to check the
count, say what it is, and leave the deletion alone. If the count is met: `cdp.mjs` and `drive.mjs`
go together, `defects.mjs` stays (both drivers share it), `e2e.sh`'s `--driver` flag becomes
unnecessary, and `E2eDriverTests`' cross-driver assertions need rewriting rather than deleting —
their own failure messages say so.

## Hard constraints

**Keep Playwright off the pixel path.** `scripts/visual-regression.sh`, `scripts/visual/diff.mjs`,
`scripts/visual/png.mjs`, `.github/workflows/visual-goldens.yml` and `tests/visual-goldens/` stay
exactly as they are. A golden must be rendered by the *same* Chrome that compares it, and
`Channel = "chrome"` is what keeps this repository at two renderers instead of three.

**These survive or the work has failed:**

- **A twin per check, and a check with no twin fails the run.** `defects.mjs` substitutes one
  documented line and **throws if it does not match exactly one line**. `e2e.sh` requires every
  driven check to have a twin, and `E2eDriverTests` holds the converse across both drivers.
- **A twin must *say* FAIL, never merely fail to say PASS.**
- **Positive control before outcome, as separate sentences.** `[CONTROL] the work did not happen: …`
  and `[OUTCOME] …` are different bug reports.
- **Nothing may reach past the browser.** No `localStorage.setItem` to arrange a state, no calling
  into a component, no `el.click()` from inside an evaluated string. **Seeding a D1 row is not an
  exception to this and you should be able to say why**: the row is what an email would have caused,
  and everything after it is the application's own verify path driven by a real navigation.

## Break every check and watch it go red. This is not ceremony

Stage one had three faults of exactly the kind reading does not reveal, and all three are in
`docs/progress/2026-09-04-…`:

- a name-extraction pattern `[A-Z][A-Z_]*` matched `A11Y` as the single letter `A`, in six places
  including the guard that decides whether every check has a negative control — the guard stayed
  green when a twinless check was added;
- `A11Y` scanned whichever palette *and page state* the previous check left behind, so two honest
  runs of one check disagreed about a real defect;
- a correct finding was deleted as an artefact because "re-running a measurement" was done in a
  different state from the first run.

Ask of each assertion: *what would still be true if the feature were absent?* And **a mutation that
is semantically null is not a finding** — say so rather than recording a hole that is not there.

## Orchestration

**Sequential first.** One agent changes how the server starts — `functions/` bundled, a D1 binding,
the local database migrated — and gets one signed-in check green with a twin. Everything in piece 1
depends on that, and five agents inventing five ways to start a server is the expensive failure.

**Then fan out**: the remaining signed-in checks, the motion check, and the Ctrl-K check are
independent once the fixture is fixed. One agent per check, each responsible for its own twin and
for watching it fail.

**Do not parallelise these**, all for the same reason — one file every branch wants to touch:
`.github/workflows/build.yml`, `scripts/e2e.sh`, `scripts/e2e/defects.mjs`, `PROGRESS.md`,
`docs/guide/testing.md`. One agent, once, at the end. `docs/progress/` takes a new file per slice
and does not collide; read `docs/progress/README.md` for why.

**Two branches that merge cleanly can still contradict each other.** In stage one two slices each
wrote a `SettingsMenu` helper, in different namespaces, and the merge compiled perfectly with both.
Budget one integration pass that runs the whole suite plus every twin, both drivers, on one branch,
and treat *that* run as the evidence rather than the branches' own.

**Name pull requests the way a changelog would** — read `CLAUDE.md`'s section on it. A title that
narrates the session is the failure mode it names.

## Questions the work must answer, not assume

1. **What does the job cost afterwards, as a number?** It is 18–19 minutes now. Signed-in checks
   mean a differently-configured server, which may mean a *second* server start rather than more
   checks on the existing one. Measure it, and if it does not fit, say which lever you pulled.
2. **Does the D1 seed belong in `e2e.sh` or in the driver?** The shell owns the server today and the
   driver owns nothing outside the browser. A driver that writes to a database has stopped being a
   thing that only knows a URL, which is the property that made it easy to reason about.
3. **How does a twin work for a signed-in check?** A defect in the *published site* cannot break a
   server-side authorisation rule. Does the twin mechanism need a second kind of defect — a seeded
   row that is expired, or an account that is not the owner — and if so, how does `defects.mjs`
   stay the one place a negative control is declared?
4. **Can the anonymous checks still run when `functions/` is bundled?** Stage one's whole
   `/api/`-falls-through-to-`index.html` behaviour changes. If the six existing checks need a
   different server from the new ones, that is two server configurations and it doubles a cost that
   is already 18 minutes.
5. **Is `motion.js`'s reduced-motion behaviour observable at all without a golden?** "Nothing
   animated" needs a positive control that the same page animates otherwise, which means two
   contexts in one run.
6. **What happens to `scripts/e2e/drive.mjs` if a signed-in check is added?** It cannot run one, so
   the driven-set/twinned-set arithmetic gets a second asymmetry like `A11Y`'s. Read the comment at
   that comparison in `e2e.sh` before touching it; it says what was already given up and why.

## Report honestly

If partway through the answer turns out to be that stage two is not worth its cost, say so and say
why — that is a good outcome, not a failed task. Report what you measured, not what you expect. If
a check could not be written honestly, leave it out and name it rather than shipping a weaker
version under the same name. And if the twenty-run count for retirement is not met, say the number
rather than reasoning about whether it feels safe.
