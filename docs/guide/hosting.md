# Hosting and deployment

Read before touching `.github/workflows/deploy.yml`, `web/wwwroot/_headers`, `_redirects`, or the content security policy.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## Hosting

Cloudflare Pages at `superheroes.softwaresamurai.net`, by `.github/workflows/deploy.yml` on push to `master`. Direct upload, not Cloudflare's Git integration — two deploy paths can disagree.

- **The deploy workflow must never trigger on `pull_request`.** That trigger runs a contributor's workflow file with the base repository's secrets in scope, which puts the Cloudflare token one PR away from anyone. Adding it would be the single most damaging change available in this repository.
- **`_headers` is generated, never hand-edited.** `scripts/write-cloudflare-headers.sh` hashes the inline import map Blazor writes into `index.html`, whose contents change whenever the framework assets are re-fingerprinted — a hard-coded hash would rot silently and stop the app booting on some later deploy. The script exits non-zero if it finds no inline script rather than shipping a policy that would break the site, and CI runs it too, so a broken policy fails on the PR.
- `style-src` needs `'unsafe-inline'` because the budget bar's width is a live inline style attribute. `script-src` does **not**, and should not gain it.
- **The edge injects scripts into the HTML after the policy has been written, so no hash here can ever cover one.** Two do it. The Web Analytics beacon is allowed by host — `https://static.cloudflareinsights.com` in `script-src`, and `connect-src` stays `'self'` because automatic setup on a proxied domain reports to this site's own `/cdn-cgi/rum`. Cloudflare's **Precursor** bot script is not allowed and is blocked in the console: its inline body carries a per-request token, so its hash differs on every response — two loads of the same page gave two hashes — and Cloudflare's own answer is a CSP nonce, which a static `_headers` file cannot mint. **Do not reach for `'unsafe-inline'` for it**; with a hash present browsers ignore `'unsafe-inline'` entirely, so it would do nothing unless the import-map hash were deleted too, which is the whole policy. Turn Precursor off in the dashboard (Security → Settings), and check Bot Fight Mode beside it — that force-enables JavaScript Detections, which injects the same way and cannot be turned off separately.
- `web/wwwroot/_redirects` sends every path to `index.html` with a **200**, not a redirect: a 302 would drop the path and land every shared link on step one.
- `<base href="/">` assumes a root path. A subdomain is fine; a subpath is not, and getting it wrong breaks every asset fetch at once.

---

## The deploy refuses to ship past an unapplied migration

**`Deploy` fails before it uploads anything if `d1/migrations` holds one the remote database has
not run.** It never applies it. Applying from CI would mean a bad migration ships itself, and a
migration is the one operation here that can destroy data — so the manual step stays and this only
refuses to race ahead of it. The error names the command:

```bash
npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
```

**Why it exists.** `0005_campaigns.sql` added `characters.campaign_id`. It was written, reviewed and
merged; the deploy shipped the Worker; **nothing applies migrations to D1.** Production then got
`D1_ERROR: no such column: campaign_id` on `/api/characters` — which broke *ordinary character
saving for every signed-in reader*, over a feature nobody was using yet. 29 failures.

**Every check passed while it happened, and the reason is worth carrying.** `./scripts/test-worker.sh`
runs 192 tests against real SQLite, and **it builds its schema by running the migrations** — so it
can never notice that production's schema was not built. A suite that constructs the world it tests
cannot tell you the real world differs. The post-deploy smoke check could not catch it either: it
asks `/api/me`, which touches no table the migration changed, so it proves the server is wired up
and nothing about its schema.

- **An unrecognised answer fails, and that is the load-bearing part.** The step reads wrangler's
  prose, so a reworded release must not read as "all clear" — the exact guard failure `CLAUDE.md`
  records four times, a check satisfied by there being nothing to check. Only the explicit
  all-clear passes; empty output refuses too, and both were driven and watched.
- **Pinned to a version whose output was read rather than guessed.** `wrangler@3.90.0` — what the
  deploy uses to *bundle* — answers this command with a usage dump, which would have been an
  unrecognised answer on every run. This step simulates nothing, so it does not need the deploy's
  version; it needs one whose two answers have been seen. `4.127.0` was verified against the real
  database before it landed.

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
- **The cause is still open.** The comparator is plain Node over two decoded PNGs, and
  `scripts/visual/png.mjs` is a hand-written decoder that had no tests at all until recently. That
  it passed on identical inputs rules out a deterministic loop and rules in very little else.

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
- **Two documentation files are skipped, and only two.** `PROGRESS.md` and `docs/HANDOVER.md`.
  **"It is only docs" is false here far more often than it looks** — `CLAUDE.md` and
  `docs/guide/*.md` are read by `RepositoryGuideTests`, `docs/ACCOUNTS-SETUP.md` by
  `AccountsContractTests`, `docs/MCP-SETUP.md` and `README.md` by `McpSetupDocumentationTests`,
  `mcp/QUESTION-POLICY.md` by `McpQuestionPolicyTests`. Editing the index past its line budget, or
  adding a guide the routing table does not name, is a red build.

  `WorkflowFilterTests` holds the list to that claim, and it is honest about which half it can
  prove: **that no file a test opens by name is skipped** is checked by scanning the test sources,
  and that direction is the one whose failure costs a missed regression. That a file is *unread*
  cannot be proved at all — a test could compose a path no scan sees — so the list is also an
  allowlist, and adding to it has to be a deliberate act.
- **Qodana runs on `master` and weekly, not on every pull request.** It was 39 of 100 runs, 214 of
  403 minutes, and **all 10 GB** of the repository's Actions cache: one ~420 MB entry per branch
  against a 10 GB ceiling, so the caches evicted each other and every run started cold.

  **What makes that safe is that the pull request was never where this check first ran.**
  `CLAUDE.md`'s process requires `./scripts/qodana-scan.sh` locally, reading zero, before a pull
  request is opened — the CI copy was re-proving a thing already proved, at five and a half minutes
  a push. What is kept is the part a local run cannot give: a scan of `master` **as merged**, which
  is a different claim from a scan of the branches that went into it. If the weekly run starts
  finding things on `master`, the answer is that the local step is being skipped — not that this
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
