# Handover

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) first. `PROGRESS.md` is the
single source of truth for what is done; this file is where the last session stopped and what
the next one is for. **Delete this file when you have finished the work it describes.**

---

## Two things to know before anything else

**1. #56 (Characters, plural) is on `claude/characters-list-slice`, not on `master`.** It was
stacked on `claude/accounts-slice-a4` and merged after #55 had landed, so it landed into an
orphan. GitHub reports it merged; the code is not deployed. **First job: restack it onto master
and open a fresh PR.**

The conflict files are `worker/corpus.js`, `worker/characters.js`, `worker/db.js`,
`worker/index.js` — all real dual edits:

- `worker/corpus.js` — the branch predates #57 and still does `import ... with { type: 'json' }`.
  Take master's baked version (`scripts/inline-rulebook.mjs` writes it). Re-run the bake after
  the merge in case `data/rulebook/ch02-characters.json` changes.
- `worker/characters.js`, `worker/db.js`, `worker/index.js` — master has the single-character
  routes (#55); the branch replaces them with the plural version. Take the branch's version
  wholesale in each file; the migration on the branch removes the old rows on purpose.

**2. Accounts are ready for setup, not for use.** Master has all the code through #57. The five
steps in [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) — every dashboard click named — turn it on:

- **Step 1 (D1 database) — done.** `d1/wrangler.toml` on master has the id.
- **Step 2 (migrations) — not applied to remote.** From a repo with Node 22:
  ```
  npx wrangler --cwd d1 d1 migrations apply prowlers-and-paragons --remote
  ```
  Only `0001_accounts.sql` applies from master. `0002_characters_list.sql` is on the characters
  branch and applies after that branch is restacked and merged.
- **Steps 3–5** — Cloudflare dashboard + Resend. All click paths in `ACCOUNTS-SETUP.md`.

Until steps 2–5 are done, nothing looks broken: visitors are anonymous, characters live in the
browser under `pp.character.v1`, and the deploy fails unless `/api/me` answers 401 carrying JSON
— the client parses the body rather than the status, so a missing server is a missing feature
rather than a blank page.

---

## Recent PRs

`PROGRESS.md` has the account of each. Everything through #57 is on master; #55, #56, #57 are
what's changed since the last handover.

- [#55](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/55) — Accounts: magic-link sign-in, one character per account, gated rulebook reader
- [#56](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/56) — Characters, plural. **Closed as merged, but merged into an orphan** — see the top of this file.
- [#57](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/57) — Hotfix: corpus baked, CI runs `wrangler pages functions build` at the pinned version

---

## What the next session is for, in order

### 1. Restack #56 onto master

Base=master, head=`claude/characters-list-slice`. Resolve the four worker conflicts above,
re-run the bake, then verify both suites and push. Once merged, apply
`0002_characters_list.sql` to remote D1 (setup step 2 above).

### 2. Finish setup and sign in for real

Steps 3–5 of `ACCOUNTS-SETUP.md`. Then use the app end to end — sign in, save a character,
sign in from a different browser and see it there. The manager's *"open now"* row marking is
untested in a real browser: bUnit's loose JS interop answers null on `ppStore.load`, so the
static proof cannot show it. Everything else has an automated test; this needs eyes.

### 3. Read-only share links

**The one place a bearer key is straightforwardly better than a session** — a viewer with no
account should see a shared character without signing in as anyone. The
`add-read-only-share-links` task carries the brief: `shares(sha256(key), character_id,
expires_at)`; `GET /api/shares/{key}` answers the payload without a session; a `/shared/{key}`
page renders through `SheetView` with no editing controls. Engine still costs and validates in
the browser; `data/rulebook/` is still not on the open web.

### 4. Pre-1.0 codebase audit — the last pass before tagging

Two questions to answer in one pass, both cheapest to do together, both cheapest to delegate
to no-context agents:

- **Is the codebase as optimised as it should be?** Genuine dead code (marked as such in
  `.claude/memory` for later removal), hot paths that touch the engine on every render,
  bundler payload waste, and the token-cost side — files a subagent has to load before it can
  do anything useful.
- **Is it snapshotable for a fresh AI agent?** What can a new Claude session read to know what
  this repo is and where the load-bearing pieces are, without re-tracing every past decision?
  `CLAUDE.md` is the current answer; the audit's job is to say what would improve it — a
  section per source of confusion, or a redraft, or a smaller pointer file for common tasks.

**Do it as the last pass before 1.0**, once the API is stable enough that the audit's targets
are not moving under it.

### 5. Adopt SemVer + Conventional Commits at 1.0

The README says truthfully: `data/rules/` and the character JSON are stable, but the HTTP API
just moved (`/api/character` → `/api/characters/{id}`). The trigger for 1.0 is *"we do not
break `/api/*` without a major"* being a promise this repo can keep — one or two slices from
now. Tag `v1.0.0`, add `<VersionPrefix>` to `Directory.Build.props`, start `feat:` / `fix:`.

### 6. Front-end plan, what remains

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
- The rulebook reader serves Chapter 2 only. Adding a chapter is one line in
  `scripts/inline-rulebook.mjs`, and a decision about what an account is entitled to read.
