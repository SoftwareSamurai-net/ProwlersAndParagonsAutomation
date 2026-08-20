# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is the short version of where the last
session stopped and what the next one is for.

**Delete this file when you have finished the work it describes.** It is a note between
sessions, not documentation.

---

## Two things to know before anything else

**1. #56 (Characters, plural) is on `claude/characters-list-slice`, not on `master`.** It was
stacked on `claude/accounts-slice-a4` and merged after #55 had landed, so it landed into an
orphan. GitHub reports it merged; the code is not deployed. **First job: restack it onto
master and open a fresh PR.** The conflicts (`worker/corpus.js`, `worker/characters.js`,
`worker/db.js`, `worker/index.js`) are real dual edits and resolve to "keep both":

- `worker/corpus.js` — take master's baked version (#57), discard the branch's `import`.
- The three routing files — take the branch's plural version; the migration removes the old
  single-character routes on purpose.

**2. Accounts are ready for setup, not for use.** Master has all the code through #57. The five
steps in [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) — every dashboard click named — turn it on:

- **Step 1 (D1 database) — done.** `d1/wrangler.toml` on master has the id.
- **Step 2 (migrations) — not yet applied to remote.** From a repo with Node 22:
  ```
  npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
  ```
  Only `0001_accounts.sql` applies from master. `0002_characters_list.sql` is on the
  characters branch and applies after that branch is restacked and merged.
- **Steps 3–5** — Cloudflare dashboard + Resend. All click paths in `ACCOUNTS-SETUP.md`.

Until steps 2–5 are done, nothing looks broken: every visitor is anonymous, characters live in
the browser under `pp.character.v1`, the deploy fails unless `/api/me` answers 401 carrying
JSON — the client parses the body rather than the status, so a missing server is a missing
feature rather than a blank page.

---

## Where the deployed site actually is

**Master, right now: 3683 engine + 342 bUnit + 45 accounts = 4070 tests**, zero warnings at CI
strictness, MIT in `LICENSE`, site live on Cloudflare Pages. Five front ends on one engine
assembly: terminal wizard, browser app, `build --from`, MCP server, accounts server. CI drives
a browser too — six proof harnesses required to *say* `PASS` in their `<title>`.

Since Phases 0–2 of the front-end plan merged as [#46](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/46):

| | |
|---|---|
| [#49](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/49) | Phase 3's `Ctrl-K` command palette; `CharacterSheet.IsVillain` and `UnlimitedBudget` toggles |
| [#50](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/50) | `Tooltip`, and the guard refusing `title` attributes |
| [#51](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/51) | The rank pips became a real slider |
| [#52](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/52) | Two areas, and the `ICharacterStore` / `IIdentitySource` seam |
| [#54](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/54) | README split by domain |
| [#55](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/55) | **Accounts** — magic-link sign-in, one character per account, gated rulebook reader |
| [#56](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/56) | **Characters, plural** — closed as merged, but merged into an orphan; code is on `claude/characters-list-slice`. **Restack it.** |
| [#57](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/57) | Hotfix: the corpus is baked (`scripts/inline-rulebook.mjs`); CI now runs `wrangler pages functions build` at the pinned version, so bundler differences fail the PR |

Old branches — `…-221eb7`, `…-c88220`, `claude/reconcile-a1-a3` — must not be started from.

---

## What the next session is for, in order

### 1. Restack #56 onto master

Fresh PR base=master, head=`claude/characters-list-slice`. Resolve the conflicts above, verify:

```
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
```

Expect **3685 + 386** and **58** (master's 45 plus the branch's 13 characters tests). Then run
migrations against remote D1 (setup step 2), and continue steps 3–5.

### 2. Sign in for real and use the app

The manager's *"open now"* row marking is untested in a real browser — bUnit's loose JS interop
answers null on `ppStore.load`, so the static proof cannot show it. Everything else has an
automated test; this needs eyes.

### 3. Read-only share links

**The one place a bearer key is straightforwardly better than a session** — a viewer with no
account should see a shared character without signing in as anyone. The
`add-read-only-share-links` task carries the brief: `shares(sha256(key), character_id,
expires_at)`; `GET /api/shares/{key}` answers the payload without a session; a `/shared/{key}`
page renders through `SheetView` with no editing controls. Engine still costs and validates in
the browser; `data/rulebook/` still not on the open web.

### 4. Adopt SemVer + Conventional Commits at 1.0

The README now says truthfully: `data/rules/` and the character JSON are stable, but the HTTP
API just moved (`/api/character` → `/api/characters/{id}`) and is not stable yet. The trigger
for 1.0 is *"we do not break `/api/*` without a major"* being a promise this repo can keep,
one or two slices from now. Tag `v1.0.0`, add `<VersionPrefix>` to `Directory.Build.props`,
start `feat:` / `fix:` prefixes.

### 5. Front-end plan, what remains

Phase 3's last two — validation on the row where the mistake is made, and undo — plus Phase 4
(sheet as live preview, absorbing Phase 1's `--column` widening) and Phase 5 (skip link,
"Saved" feedback, honest print preview). Plan in [`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md).

**"Saved" feedback is now owed rather than merely wanted.** `ApiCharacterStore` swallows a
failed save silently because nothing in `ICharacterStore` may throw — a network drop loses
work the user is not told about.

### Known warts, all deliberate

- Home and End on a rank slider also scroll the document. Fix is an interop shim, not a Razor
  attribute.
- Screen-reader testing owed on the command palette, the pips, and the sign-in page.
- The rulebook reader serves Chapter 2 only (once the characters slice is on master). Adding a
  chapter is one line in `scripts/inline-rulebook.mjs`.

---

## Prerequisites — before writing any code

1. **Toolchain.** `dotnet --version` = 10.0.x, Node 22 or Docker for the accounts tests.
2. **Start from `master`.** The only relevant branch is `claude/characters-list-slice`, which
   needs restacking.
3. **Baseline all three suites** and take the numbers from the run, not from this document.
   On master today: **3683 + 342** and **45**.
4. **A crashed test process still prints `Passed! - Failed: 0`.** Grep for `Catastrophic`,
   check totals moved. `node --test` on an empty glob exits 0 reporting zero — CI asserts the
   count for a reason.
5. **Any wrangler command written into a workflow: `--help` first at the pinned version, in
   Docker if you don't have Node locally.** `deploy.yml` carries a marker comment on its
   `cloudflare/wrangler-action@v3` line; do not change it without changing CI.
6. **Look at the app without a dev server** (which raises an approval dialogue): `PP_PROOF=1
   dotnet test tests/ProwlersAndParagons.Web.Tests` writes `web/wwwroot/proof-*.html`.
   Screenshot with headless Chrome; `--virtual-time-budget=3000` is not optional.
7. **A proof rendering the wrong state looks entirely plausible.** Sign a context in *before*
   `.With(mode)`; loading a sample caches the identity as anonymous. Documented in
   `ProofPages.cs`.

---

## How this project expects to be worked on

1. **Update `PROGRESS.md` in the same change.**
2. **Adversarial review by no-context agents**, with the mutation demand *scoped* — "the
   security and ordering guards", filtered to affected classes. The last slice paid
   25–40 min/agent because the brief said "every guard, both full suites".
3. **Then a reviewer pointed at the fixes, not the code.** Ask for a *variant*, never a re-run.
4. **A later declaration of the same thing beats a `Contains`.** Use `EffectiveValue` and
   `RulesTargeting`.
5. **Placement in the cascade is part of aiming a mutation.** Insert after; keep the arity.
6. **Look at the rendered page, not just the tests. Open every proof.**
7. **Fix the class of defect, not the defect.**
8. **Check a rulebook citation before repeating it.**
9. **Verify CLI flags at the pinned version before pushing.**

---

## Traps whatever you touch

- **The rulebook PDFs are in `docs/` in the main working directory only** — `*.pdf` is
  gitignored, so worktrees cannot see them.
- **`data/rulebook/` is generated** — do not hand-edit. **And `worker/corpus.js` is baked from
  it** by `scripts/inline-rulebook.mjs`; a bake guard in `router.test.mjs` fails the PR on
  drift.
- **Whole-tree Qodana on a built directory means nothing.** Export first
  (`git archive HEAD | tar -x -C <tmp>`).
- **Warnings are errors only under `ContinuousIntegrationBuild`.**
- **Before any destructive revert, `git stash push -u -m pre-experiment`.**
- **`perl -pi` silently edits nothing on this machine.** Use `sed -i` or the editor; check
  `git diff --numstat`.
- **Heredocs full of JS can silently produce NUL bytes**, which git treats as binary. Use the
  editor.
- **Workflow files are CRLF.** An `Edit` matching an LF-terminated line finds nothing.
- **A check that never ran looks exactly like one that passed.** Assert on the positive.
- **`pull_request` runs contributor workflows with the *base* repo's secrets** — deploy has no
  `pull_request` trigger for that reason.
