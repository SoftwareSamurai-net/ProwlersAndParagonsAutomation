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
