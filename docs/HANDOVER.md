# Handover

**Read this whole file before touching anything. Treat every claim as hostile until you verify
it against the repo, the deploy, or the running database.** The last session shipped three
distinct mistakes and did not catch a fourth until the user pushed for a re-check. What follows
names them and lists what to check.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

---

## Four things wrong right now — verify these first

**1. `d1/wrangler.toml` on `master` has `REPLACE-ME` as the `database_id`.** The wrangler
`d1 create` command was run in the user's terminal on 2026-08-20 and returned
`57b6cdf2-300c-4cf4-8c19-8d2dcf9fcaed`. The commit landing that id went into the orphan
`claude/accounts-slice-a4` branch; master never got it. Step 1 of `ACCOUNTS-SETUP.md` is not
done on master until this branch (`claude/docs-post-deploy-fix`, PR [#58](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/58))
merges. **Verify** by running `grep database_id d1/wrangler.toml` on master head after any
merge; if it says `REPLACE-ME`, migration step 2 will fail.

**2. #56 (Characters, plural) merged into an orphan and is not on master.** It was stacked on
`claude/accounts-slice-a4`; #55 merged first, orphaning the base; GitHub reports #56 merged
because the button was pressed on it, but it landed into a branch that no longer feeds master.
The code is on `claude/characters-list-slice`. **First restack job:** open a fresh PR base=master,
head=`claude/characters-list-slice`. Conflicts and resolution are in `worker/corpus.js`,
`worker/characters.js`, `worker/db.js`, `worker/index.js` — see section below.
**Verify** the code is not on master with:
```
git ls-tree origin/master --name-only | grep -E 'CharacterManager|SavedCharacters|CharacterImport'
```
Empty output means it is genuinely not there.

**3. Master's deploy failed after #55 merged**, because `worker/corpus.js` used
`import ... with { type: 'json' }`. Wrangler 3.90.0's bundled esbuild refuses that syntax; Node
22 runs it. Both suites and a whole-tree Qodana scan were green. Fixed by [#57](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/57):
the corpus is baked (`scripts/inline-rulebook.mjs`) and CI runs `wrangler pages functions build`
at the pinned version so a bundler difference fails the PR. **Verify by pulling master and
running the same command CI runs**:
```
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo" -w /repo node:22-alpine \
  npx --yes wrangler@3.90.0 pages functions build --outfile=/tmp/w.js
```

**4. #57's first push invented a non-existent flag** (`wrangler pages deploy --dry-run` — that
flag exists for `wrangler deploy`, not for Pages). CI caught it. The fix was a second push
using the real `pages functions build`. Nothing on master should reference `--dry-run`; verify:
```
grep -n "dry-run" .github/workflows/*.yml
```
Empty output means the mistake is fully gone.

---

## Where things stand on master (verify by running, do not trust)

Take the numbers from the run, not from this document. Yesterday's numbers were on branches
that were later merged elsewhere. **Baseline both suites before touching anything**:

```
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
```

Prior to opening this file's PR, master reported **3683 engine + 342 bUnit + 45 accounts**.
Once #58 merges (docs + the id fix), the numbers do not change; once #56 is restacked and
merged, they rise. If your baseline disagrees with the run before your change, something
is wrong with your assumption, not with the run.

---

## The account setup, honestly

`ACCOUNTS-SETUP.md` names every dashboard click. Where things actually stand:

- **Step 1 (D1 database) — created but its id is not on master** until PR #58 merges. The DB
  itself exists on Cloudflare's side (`prowlers-and-paragons`, id
  `57b6cdf2-300c-4cf4-8c19-8d2dcf9fcaed`); the repo just doesn't reference it yet on master.
- **Step 2 (migrations) — not applied to remote.** After #58 merges:
  ```
  npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
  ```
  Only `0001_accounts.sql` applies from master. `0002_characters_list.sql` is on
  `claude/characters-list-slice` and needs the restack (job 1 in this file) first.
- **Steps 3–5** — Cloudflare dashboard + Resend + a redeploy. Click paths in `ACCOUNTS-SETUP.md`.

The deploy itself fails unless `/api/me` answers **401 carrying JSON** — that check on the
deploy pipeline is the only thing that would notice a misconfigured DB binding or a missing
`SITE_URL`. Trust it, not any local test.

---

## The restack conflict, resolved as far as possible on paper

`claude/characters-list-slice` predates #57's bake. Merging it onto master:

- **`worker/corpus.js`** — take master's baked version. The branch's version still has the
  `import ... with { type: 'json' }` that broke the deploy. Re-run `scripts/inline-rulebook.mjs`
  after the merge in case the rulebook JSON drifted.
- **`worker/characters.js`, `worker/db.js`, `worker/index.js`** — take the branch's plural
  version. The migration on the branch removes the single-character routes and rows on purpose;
  master's post-#55 versions are the ones being replaced.
- Anything else conflicting — read both sides. Do not resolve without reading.

**Do not trust a green suite as evidence of a correct merge.** #56's own PR was green when it
merged into an orphan. The two things that catch merge damage independently are:

1. `AccountsContractTests` in the engine suite — reads both language halves and refuses if the
   addresses the browser asks for and the routes the server serves disagree. It was the only
   thing that caught the `/api/character` → `/api/characters/{id}` rename during the original
   slice.
2. The CI `wrangler pages functions build` step, at the version pinned in `deploy.yml`.

Run both. Read the SARIF, do not grep the summary.

---

## What the next session is for, in order

1. **Merge PR #58** (fixes 1 and 4 above; you are looking at this file on that branch).
2. **Restack #56 onto master** — conflicts above. New PR, base=master, head=`claude/characters-list-slice`.
3. **Apply migrations to remote D1** (setup step 2).
4. **Steps 3–5 of `ACCOUNTS-SETUP.md`.** Then sign in for real: the manager's *"open now"* row
   marking has no automated test in a real browser (bUnit's loose JS interop answers null on
   `ppStore.load`).
5. **Read-only share links.** The `add-read-only-share-links` task carries the brief.
6. **Pre-1.0 codebase audit.** Not versioning ceremony — an actual audit of what a fresh AI
   agent needs to be productive here, plus optimisation. See `PROGRESS.md`'s item 7.
7. **Front-end plan, remaining phases** ([`docs/FRONT-END-PLAN.md`](FRONT-END-PLAN.md)). Not
   blocked by any of the above; can start on master today, touches only `web/` and its tests.

### Known warts, all deliberate

- Home and End on a rank slider also scroll the document. Fix is an interop shim, not a Razor
  attribute.
- Screen-reader testing owed on the command palette, the pips, and the sign-in page.
- The rulebook reader serves Chapter 2 only. Adding a chapter is one line in
  `scripts/inline-rulebook.mjs`, and a decision about what an account is entitled to read.

---

## What the previous session's tools got wrong, so you do not repeat them

- **Stacked PRs need the base branch to survive.** Do not merge the top of a stack before the
  bottom, and do not merge a stacked PR after its base has itself merged — GitHub will let you
  do this and the result is an orphan merge.
- **Any CLI flag written into a workflow gets `--help` at the pinned version first**, in Docker
  if you do not have Node locally. I invented a flag; CI caught it, and only CI.
- **Runtime version differences ship silently.** Wrangler 3.90.0's esbuild is older than Node
  22's. If a test suite passes under Node 22 but the deploy runs under wrangler, run the deploy's
  bundler locally before pushing. #57 added the CI step that does this; do not remove it.
- **Local branch state can lie to you.** After a merge to master, my working tree still showed
  the merged files present, so I claimed things were on master that were only on the (now
  orphan) branch. Fetch master, `git ls-tree` it explicitly, and trust that over your worktree.
