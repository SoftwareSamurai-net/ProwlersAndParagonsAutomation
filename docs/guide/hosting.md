# Hosting and deployment

Read before touching `.github/workflows/deploy.yml`, `web/wwwroot/_headers`, `_redirects`, or the content security policy.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Hosting

Cloudflare Pages at `superheroes.softwaresamurai.net`, by `.github/workflows/deploy.yml` on push to `main`. Direct upload, not Cloudflare's Git integration — two deploy paths can disagree.

- **The deploy workflow must never trigger on `pull_request`.** That trigger runs a contributor's workflow file with the base repository's secrets in scope, which puts the Cloudflare token one PR away from anyone. Adding it would be the single most damaging change available in this repository.
- **`_headers` is generated, never hand-edited.** `scripts/write-cloudflare-headers.sh` hashes the inline import map Blazor writes into `index.html`, whose contents change whenever the framework assets are re-fingerprinted — a hard-coded hash would rot silently and stop the app booting on some later deploy. The script exits non-zero if it finds no inline script rather than shipping a policy that would break the site, and CI runs it too, so a broken policy fails on the PR.
- `style-src` needs `'unsafe-inline'` because the budget bar's width is a live inline style attribute. `script-src` does **not**, and should not gain it.
- **The edge injects scripts into the HTML after the policy has been written, so no hash here can ever cover one.** Two do it. The Web Analytics beacon is allowed by host — `https://static.cloudflareinsights.com` in `script-src`, and `connect-src` stays `'self'` because automatic setup on a proxied domain reports to this site's own `/cdn-cgi/rum`. Cloudflare's **Precursor** bot script is not allowed and is blocked in the console: its inline body carries a per-request token, so its hash differs on every response — two loads of the same page gave two hashes — and Cloudflare's own answer is a CSP nonce, which a static `_headers` file cannot mint. **Do not reach for `'unsafe-inline'` for it**; with a hash present browsers ignore `'unsafe-inline'` entirely, so it would do nothing unless the import-map hash were deleted too, which is the whole policy. Turn Precursor off in the dashboard (Security → Settings), and check Bot Fight Mode beside it — that force-enables JavaScript Detections, which injects the same way and cannot be turned off separately.
- `web/wwwroot/_redirects` sends every path to `index.html` with a **200**, not a redirect: a 302 would drop the path and land every shared link on step one.
- **A file that exists on disk is served ahead of that rule, which is what makes `/join.html` work** — the player handout is a static page rather than a Blazor route, so a GM can send it to somebody who has no account and is not going to download the application to read it. It needs nothing from the policy above: same-origin stylesheet, the self-hosted faces, `js/theme.js`, all already covered by `default-src 'self'`. Adding another such page means adding it to the scans in `WebPresentationTests` — they read stylesheets by enumeration but HTML by name, and `join.css` sat outside every presentation rule for as long as those scans named `app.css` alone.
- `<base href="/">` assumes a root path. A subdomain is fine; a subpath is not, and getting it wrong breaks every asset fetch at once.

---

## Migrations apply themselves before the Pages upload, and refuse rather than guess

**This replaces both the manual step and the gate that only refused.** The history below is kept
because the reasoning still matters — a future change to this area is a change to a mechanism that
has already broken twice — but the mechanism itself has moved on from both.

**What it cost to learn, the first time.** `0005_campaigns.sql` added `characters.campaign_id`. It
was written, reviewed and merged; the deploy shipped the Worker; nothing applied the migration.
Production answered `D1_ERROR: no such column: campaign_id` on `/api/characters` — **breaking
ordinary character saving for every signed-in reader**, over a feature nobody was using yet. 29
failures. Every check passed while it happened and none could have: `./scripts/test-worker.sh`
**builds its schema by running the migrations**, so it can never notice that production's schema was
not built, and the post-deploy smoke check asks `/api/me`, which touches no table the migration
changed.

**What it cost to learn, the second time.** A step that only *refused* to deploy past a pending
migration — never applying one, on the reasoning that applying from CI would let a bad migration
ship itself — was built, driven through its branches, and merged as PR #104. It was reverted on its
first real run: the deploy's `CLOUDFLARE_API_TOKEN` had no D1 permission at all, wrangler answered
*"The given account is not valid or is not authorized to access this service [code: 7403]"*, the
step could not tell whether anything was pending, and it refused — **correctly, by its own design**.
But that left `main` unable to deploy at all, which is a worse failure than the one it prevents.

**The mechanism that replaces both.** `scripts/apply-migrations.sh` runs
`wrangler d1 migrations list prowlers-and-paragons --remote`, hands the real output to the pure
decision in `scripts/d1-migrations/gate.mjs`, and:

- **Nothing pending** — proceeds; the Pages upload continues.
- **Only additive DDL is pending** — `ADD COLUMN`, `CREATE TABLE`, `CREATE INDEX`, a plain
  `INSERT ... VALUES`, and the like — runs `wrangler d1 migrations apply` itself and continues.
  Applying automatically is safe here in a way it was not for #104's design, because the classifier
  never touches a migration whose SQL contains a destructive pattern.
- **A destructive pattern is pending** — `DROP TABLE`, `DROP COLUMN`, `DELETE FROM`, `TRUNCATE`, or a
  table rebuild (`ALTER TABLE ... RENAME TO`, or the `..._new`/`INSERT ... SELECT`/`DROP TABLE`
  12-step) — **the deploy aborts**, naming the file and the exact statement. The one way past this
  is the migration's own author adding `-- pp:allow-destructive: <reason>` to the file, because the
  person who knows a destructive statement is safe this one time is whoever is writing it, not
  whoever last edited the gate.
- **Wrangler answers with anything this gate does not recognise — including empty output, or the
  7403 unauthorised answer above** — the deploy aborts. An unauthorised answer is never read as
  "nothing pending": the refusal names the exact permission needed, **D1: Edit**, alongside whatever
  the token already has for Cloudflare Pages.

Every branch is driven with canned wrangler output in `tests/deploy/migration-gate.test.mjs` (also
runnable via `scripts/test-deploy-gate.sh`), including the classifier reading real content out of
`d1/migrations` — comments and string literals are masked out before pattern-matching, so a comment
*describing* a destructive statement is never mistaken for one, and a destructive keyword quoted
inside a string literal is not either.

**`d1/migrations/**` is in `deploy.yml`'s `paths:` filter now, and has to be.** Before this, a pull
request that added only a migration file changed nothing under `web/` or `worker/`, so the deploy
workflow never ran at all — the auto-apply above would have been a no-op for exactly the change it
exists to handle.

**Pinned to `wrangler@4.127.0` — and that is now the only wrangler version in this repository,
which it was not.** It used to be pinned here *against* the deploy: `cloudflare/wrangler-action@v3`
bundled `3.90.0` for the Pages upload, and `3.90.0` does not understand `--cwd` at all — verified
against the real binary, `npx wrangler@3.90.0 --cwd d1 d1 migrations list prowlers-and-paragons
--remote` answers `Unknown argument: cwd` and dumps the subcommand's own usage rather than a
result — which this gate would correctly call "unrecognised" and refuse on, every single run, for a
reason that has nothing to do with what is actually pending. `4.127.0`'s two real shapes,
`Migrations to be applied:` with a table and `No migrations to apply!`, were both read against the
real production database before this was written.

**The action is `@v4` now, and the two pins collapsed into one — but not by themselves.** `@v3`
hard-coded `DEFAULT_WRANGLER_VERSION = "3.90.0"`, so leaving `wranglerVersion` unset *was* a pin;
`@v4`'s default is the range `"4"` — read out of the two published `dist` bundles rather than
inferred — which resolves to whatever the newest 4.x is at the moment the runner asks. Two deploys
minutes apart could bundle two wranglers. So `deploy.yml` states `wranglerVersion: "4.127.0"`
explicitly, and `WranglerIsPinnedToOneVersion` fails the suite when that input, the `# wrangler=`
comment the pull-request dry-run parses, and `scripts/apply-migrations.sh`'s own constant stop
agreeing. **That last part is the fix to a warning rather than to a bug**: commit `a4ec6ef` taught
the dry-run's `sed` to match any major *and* ended by noting the version inside the comment was
still hand-maintained, so a major bump had to update it or the two would drift apart again. This is
that note, made to fail.

**The deploy token needs D1: Edit, and that half has now run rather than being assumed.** It is an
account permission granted in the Cloudflare dashboard, not a setting in a file, so nothing in this
repository can do it: without it `wrangler d1 migrations list` answers 7403 on every deploy and this
gate refuses exactly as it did the first time, correctly.

**Until `0007_decision_recorded.sql` there was no evidence either way, and the reason is worth
keeping.** The deploys before it found *nothing pending*, so the apply path never executed — and a
token holding only D1: Read produces that identical log before failing on the first migration that
actually has to be applied. Reading those runs as proof of Edit would have been the same mistake as
reading a 7403 as all-clear, one step later. On the deploy of `a978806` the gate read one pending
file, classified it additive, applied it, **and then asked the database again** — `No migrations to
apply!`. That second question is the whole difference between "the schema moved" and "wrangler
exited 0", and it is why the script asks it.

- **Do not "fix" a 7403 by treating it as all-clear.** That is the exact failure this whole section
  exists to prevent: a check satisfied by there being nothing to check.
- **The classifier is a judge, not a fixer.** A destructive migration is refused, never rewritten
  and never silently split into a safe and an unsafe half — the same rule this repository already
  holds the rules engine to for an illegal character.

## A hung step costs six hours, and nothing was stopping it

**`Build` has `timeout-minutes: 30`, and it is there because a step hung and ran to GitHub's own
six-hour ceiling.** The visual comparator printed `pixel-identical` for six of seven proof pages,
printed **no line at all** for the seventh, and sat there until the platform killed the job — six
hours of the organisation's allowance spent saying nothing. A re-run over byte-identical inputs
passed, so it was not a deterministic fault in the page or the golden.

- **The cap is on the job, not on the step that hung.** Capping that one step would fix the
  instance and not the class; the next hang is somewhere else. Thirty minutes against a job that
  takes four to six is generous enough never to kill a slow runner.
- **`scripts/visual-regression.sh` also caps each comparison** at `compare_deadline`, and that is
  not redundant with the job cap: a job-level timeout says *the build hung*, and this says **which
  page**. `timeout`'s exit code 124 is reported as its own finding rather than folded into an
  ordinary mismatch, because a picture that changed and a comparison that never finished are
  different facts.
- **The deadline is one variable used by both the `timeout` and the message quoting it.** A message
  naming a number that lives somewhere else is a claim with a shelf life.
- **The cause is no longer open, and it was not the comparator — this bullet used to say it
  probably was.** It happened again on the run of `6d1078e`, and that run says which half: the
  step spent **1666 seconds having printed not one line**, not a single `pixel-identical`, so it
  never reached a comparison at all. The orphan processes the runner killed on the way out were
  `chrome` and `chrome_crashpad_handler`. It hung in the **first capture** — and the capture was
  the one thing in that script with no deadline on it, because `compare_deadline` bounds
  `node diff.mjs` and nothing bounded Chrome.

  **So the earlier fix was aimed at the half that was already bounded.** `screenshot_deadline`
  now caps each capture at 120s, a capture timeout **aborts** the run rather than failing one
  page (eight pages queued behind a wedged renderer is sixteen minutes, which is how a
  30-minute cap gets hit by a check that had its answer in the first two), and a hang is
  reported separately from "no screenshot was produced" because they are different faults —
  the second is retried, the first is not.

  Watched to fire, both ways, via the `PP_CHROME_BIN` seam: a stand-in that sleeps gives
  `exit 1` at 122s naming the page, and one that copies the goldens into the requested
  `--screenshot=` path takes all eight pages green in 52s without tripping it.

  **What is still open is why Chrome wedges**, and it is intermittent rather than deterministic:
  the re-run of that same commit did the whole comparison step in **13 seconds**. A re-run over
  byte-identical inputs passing is what this bullet said the first time, and it still holds —
  what has changed is that the failure now costs two minutes and names a page instead of
  cancelling the job.

**Default `timeout-minutes` is unlimited**, which is the whole trap: every job in every repository
here runs to six hours before anybody is told, and the failure is indistinguishable from a job that
is merely slow.

## What each workflow costs, and the three things that hold it down

**This is a private repository on a metered plan, so Actions minutes are a real budget** — 2,000 a
month, and a single busy session measured **403 minutes across 98 runs**. Qodana was 214 of them,
Build 150, and everything else 39. The three changes below took that down without dropping a check
that matters. Measure before changing any of it:

```bash
gh run list --limit 100 --json name,status,createdAt,updatedAt --jq '
[.[] | select(.status=="completed") | {name, secs: ((.updatedAt|fromdateiso8601) - (.createdAt|fromdateiso8601))}]
| group_by(.name) | map({workflow: .[0].name, runs: length, total_min: ((map(.secs)|add)/60|round)})
| sort_by(-.total_min)[]'
```

- **Superseded runs are cancelled** — `concurrency` with `cancel-in-progress: true`, keyed on
  workflow and ref, on Build and Qodana. A force-push to a branch used to leave the previous run
  burning to completion on a commit nobody would merge. **`deploy.yml` sets the opposite on
  purpose and must keep it**: cancelling a half-finished deploy is how a site ends up serving a
  partial upload.
- **One documentation file is skipped, and only one: `docs/HANDOVER.md`.** It used to be two —
  `PROGRESS.md` came off the list once `ProgressArchiveTests` started opening it, to hold its
  completed-work section to being a pointer rather than a place entries pile up. **"It is only
  docs" is false here far more often than it looks** — `CLAUDE.md` and `docs/guide/*.md` are read
  by `RepositoryGuideTests`, `docs/ACCOUNTS-SETUP.md` by `AccountsContractTests`,
  `docs/MCP-SETUP.md` and `README.md` by `McpSetupDocumentationTests`, `mcp/QUESTION-POLICY.md` by
  `McpQuestionPolicyTests`. Editing the index past its line budget, or adding a guide the routing
  table does not name, is a red build.

  `WorkflowFilterTests` holds the list to that claim, and it is honest about which half it can
  prove: **that no file a test opens by name is skipped** is checked by scanning the test sources,
  and that direction is the one whose failure costs a missed regression. That a file is *unread*
  cannot be proved at all — a test could compose a path no scan sees — so the list is also an
  allowlist, and adding to it has to be a deliberate act.
- **Qodana runs on `main` and weekly, not on every pull request.** It was 39 of 100 runs, 214 of
  403 minutes, and **all 10 GB** of the repository's Actions cache: one ~420 MB entry per branch
  against a 10 GB ceiling, so the caches evicted each other and every run started cold.

  **What makes that safe is that the pull request was never where this check first ran.**
  `CLAUDE.md`'s process requires `./scripts/qodana-scan.sh` locally, reading zero, before a pull
  request is opened — the CI copy was re-proving a thing already proved, at five and a half minutes
  a push. What is kept is the part a local run cannot give: a scan of `main` **as merged**, which
  is a different claim from a scan of the branches that went into it. If the weekly run starts
  finding things on `main`, the answer is that the local step is being skipped — not that this
  should go back on every push.

**Branch protection is unavailable on this plan, so no check is *required*** — which is why
path-filtering is safe here and would not be everywhere. A skipped job reports no status, and on a
repository with required checks that leaves a pull request permanently unmergeable. Check this
before copying the pattern anywhere else.

**Caches are not free forever either.** They evict at 10 GB per repository, and a scan of what is
actually there is one call:

```bash
gh api "repos/DorianSheiles/ProwlersAndParagonsAutomation/actions/cache/usage"
```
