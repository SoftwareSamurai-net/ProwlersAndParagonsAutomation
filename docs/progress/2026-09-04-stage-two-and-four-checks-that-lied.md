# Stage two, and four checks that passed while measuring less than they claimed

The signed-in half of `PROGRESS.md` item 10, the two behavioural gaps it named, a fix that had never
been watched to work, and one design decision that was the owner's. What follows is the account of
what changed and — at more length, because it is the expensive part to recover — the four separate
times a check in this slice reported PASS over something it was not actually measuring.

Pull request: [#151](https://github.com/SoftwareSamurai-net/ProwlersAndParagonsAutomation/pull/151).
The brief this was built from is
[`2026-09-04-the-stage-two-brief.md`](2026-09-04-the-stage-two-brief.md), and it is not edited to
agree with any of this.

## The server reversed, and it cost nothing

Stage one ran `wrangler pages dev` from `.e2e/` **specifically so that no `functions/` directory was
found**. That was a real answer to a real question — there was no database to bind — and it meant
nothing behind sign-in existed to drive.

It runs from the repository root now, with `--d1 DB=<id>`, `--persist-to`, and `SITE_URL` and
`ADMIN_EMAIL` as bindings. The local D1 is created and migrated by `e2e.sh` from `d1/migrations`,
the same source `scripts/apply-migrations.sh` reads for production.

**The question that decided whether this was affordable was answered by measurement before anything
was built**: do the six anonymous checks still pass against a server that answers `/api/`? They do,
unchanged. `/api/me` returns `401 {"error":"Nobody is signed in."}` where it used to return an
unparseable `index.html`, and the application reads both as "anonymous" — which is its own
documented behaviour. So there is **one** server configuration, not two, and the Build job does not
double.

**Two positive controls guard the reversal**, because both failure modes are silent:

- `start_server` greps wrangler's log for `No Functions` and refuses. A `functions/` moved, or a
  `cd` lost in an edit, would otherwise put the harness back in stage one's shape with every
  signed-in check red and blaming the application.
- The schema is probed with a `SELECT` against `login_tokens` rather than by reading the
  migration's own output. **The first version read the output and was wrong**: `d1 migrations apply`
  names each file on a fresh database and says "No migrations to apply!" on one that is already
  migrated, so the check failed against a perfectly good database on the second run of the day —
  the state a developer's `.e2e/` is always in and the state CI never sees.

## Signing in without opening a seam

`worker/tokens.js` stores **only the SHA-256** of a sign-in token; the raw token travels by email;
`db.spendLoginToken` verifies by hash lookup and burns the row in one statement. So the harness does
what an email does — mint, hash, insert, and navigate to the link — and the application runs its own
verify path: hash lookup, expiry test, single-use burn, invitation gate, session cookie.

`scripts/e2e/seed.mjs` **imports `worker/crypto.js`** rather than reimplementing `hash`. It is plain
WebCrypto and Node provides it identically, so there is no second implementation of the one function
that decides whether a token matches.

Four things had to be found rather than assumed:

- **The parameter is `?t=`, not `?token=`.** The brief said `?token=` in as many words. The wrong
  name is silent — the page shows its ordinary form, the token stays unspent, and every signed-in
  check goes red with nothing anywhere saying why.
- **`ADMIN_EMAIL` must name an address nothing signs in as.** `worker/invitations.js` makes whoever
  it names the bootstrap administrator; pointing it at the seeded account would sign the driver in
  as the owner and `ADMIN` would be measuring nothing at all.
- **A token is single-use, so the world is re-seeded before every drive**, twins included. One
  token cannot serve the real run and then a twin.
- **Node will not spawn `npx.cmd` without `shell: true`** — a deliberate change made for a
  command-injection CVE. The seed died on Windows with an `EINVAL` and a stack trace naming a
  hundred-character SQL string. `shell: true` would have put that SQL through `cmd.exe`'s quoting,
  which is precisely what the restriction exists to prevent, so Node writes the statements to a file
  and the shell runs `wrangler d1 execute --file`.

### A second kind of twin, because a signed-in check cannot have the first

Every negative control in this harness is a copy of the published site with one documented line
changed. **That cannot work for a check the server answers.** The application's own logic is inside
`_framework/*.wasm` — there is no line to substitute — and the twin and the real site are served by
the same `functions/`, so a defect in the site cannot reach the gate.

So `defects.mjs` declares `SEED_DEFECTS` beside `DEFECTS`, and `--list` emits a third field naming
the kind. A `seed` twin serves the **real** published site against a differently-seeded database:

| Twin | Check | What it removes | Which half goes red |
|---|---|---|---|
| `signin-token-already-expired` | `RULES` | a live token | the **control** — no session, so nothing to read |
| `seeded-account-is-the-owner` | `ADMIN` | the refusal | the **outcome** — the page lets an ordinary account in |

The second is the one worth having, and it is why the admin check is worth more than the rulebook
check: nothing about the browser, the session or the page changes, only whether this account is
allowed.

**What a seed twin does not prove is written into `defects.mjs`'s header rather than left implied**:
it does not prove the check would notice a broken *page*, because it does not break one.

## Four checks that passed while measuring less than they claimed

This is the part worth reading. Every one of these was green, and every one was found by looking at
its own output rather than by anything failing.

**1. `MOTION` reported one movement where two mechanisms should have run.** The check drives
`js/motion.js` in two browser contexts differing only in `prefers-reduced-motion` and requires the
counters to move in one and stay still in the other. It passed. Its verdict line said *"motion
allowed: 1 movement(s)"* — and there are two mechanisms, a counting figure and a view transition, so
one is the wrong number. The transition half was never running: the link it clicked
(`a[href='build/characteristics']`) is not on the page it looked at, `CountAsync()` returned 0, and
the click was **skipped silently**. The control is now per-mechanism rather than a sum, and the link
is required rather than optional.

**2. Then the counting half turned out never to have run either.** With the control split, `counts`
was 0: choosing a tier changes what may be *spent*, and the figure `ppCount` animates is what *has
been* spent. The check now raises a Characteristic, which is the cheapest thing in the application
that moves that figure.

**3. And then the transition half was zero for a third reason.** `window.ppMotionStats` lives in the
document, and `harness.Open` loads a new one. The counters were being read after a full navigation
that had reset them — so a transition that really happened reported as `transitions: 0`, which reads
exactly like a browser that does not support them. Everything after the first navigation now happens
inside one document.

**4. `RULES` was racing the server, and it passed in isolation.** `RulesReference` sets `_refused`
only once `/api/rulebook/contents` has answered, so until then it renders its *un-refused* branch —
"Search the book", with nothing in it. Read a moment too early, an anonymous reader looks exactly
like a signed-in one. The check passed every time it was run alone and failed inside a full ten-check
run, which is the tell for a race: the same page, a busier machine. It waits for the page to settle
on one of its two real answers now, the same shape `ADMIN` already used.

**The pattern in all four is one thing.** None was found by reading the code, and none would have
been found by a failing test — they were all *green*. Three were found by reading the figures a
passing check printed about itself, and the fourth by running the suite in a different order. That
is the argument for a verdict line that quotes numbers instead of saying "ok".

## Proving `kill_tree`, which fixed a real leak and had never been watched to work

`stop_server` used `pkill -P` on Linux, which kills direct children only — and wrangler's tree is
`npx` → node → `workerd`, so `workerd` outlived every step holding a port. It became `kill_tree`,
reading ppids out of `/proc/<pid>/stat`.

**The green CI runs said nothing about that fix.** Every server in this harness takes a fresh port,
so a leaked listener changes no verdict anywhere: the runs were green before the fix, green after
it, and would be green again if it were reverted.

So `children_of` and `kill_tree` moved into `scripts/e2e/process-tree.sh`, which `e2e.sh` sources
and `tests/shell/kill-tree.test.sh` drives — the same file, not a copy. `build.yml` runs it in
seconds, with no publish, no server and no browser.

**The fixture that collapses is written into both files.** `bash -c` with a single simple command
*execs* it rather than forking, so the obvious three-deep tree — `bash -c 'bash -c "sleep 300"'` —
is one `sleep` wearing three hats, and a test built on it passes against a `kill_tree` that kills
only the pid it was handed. Which is the bug. Every level ends `; true`, and the **first assertion
in the file is that the tree really is deeper than one process**.

**Watched to fail**: with `kill_tree` reduced to one level of children — which is what `pkill -P`
did — the test reports `18 survived kill_tree` and names the leak. With the real one it passes.

**And it found a second thing on the way.** The test could not run at all from Windows through
Docker: `git` checks `*.sh` out CRLF here, and a Linux `bash` answers that with
`syntax error near unexpected token $'{\r'`. `.gitattributes` now pins `*.sh` to `eol=lf`. The
positive control is what caught it — without the "is this really a tree" assertion, the run would
have been a pile of `command not found` and a verdict nobody read.

## The contrast decision, which was the owner's

The wizard's Next control, before a tier is chosen, measured **2.23:1 (Hero/Light), 3.28:1
(Hero/Dark), 2.54:1 (Villain/Light), 3.22:1 (Villain/Dark)** against a 4.5:1 floor. WCAG 1.4.3
exempts text in an inactive component, so it was not a conformance failure and the A11Y check
carried a node-by-node exemption for it.

The owner's answer, of the two options put: **raise the opacity**. `.btn.disabled` and
`.btn:disabled` are `0.8`, which clears 4.5:1 in all four palettes with Hero/Light tightest at
**4.89:1**. **The exemption is gone entirely** — there is nothing exempt in that check now — and
what replaces its positive control is a count of disabled controls on `/build`, so a scan that saw
none goes red instead of quietly proving nothing.

**Watched to fail**: put back to 0.45 in a published copy, A11Y reports four `color-contrast`
violations naming those four original figures. The arithmetic used to choose 0.8 reproduces axe's
own readings at 0.45 to within 0.01, which is why it can be trusted about 0.8.

**What was not done is the better half.** A readable dead control still does not say *why* it is
dead. The other option — a sentence on the step naming what is missing, the way the Campaigns page
does for a disabled Join — is still unwritten and is now a smaller change than it was.

## Retiring `scripts/e2e/`: the count is one

The condition is twenty consecutive green `Build` runs on `main` whose `--driver dotnet` step
reported every check green and every twin red. Counted on 2026-09-04: `ae03aaef` is the **only**
`Build` run on `main` that has ever had that step in it. So this is not the change that deletes the
hand-rolled driver, and the honest report is the number.

## What this leaves open

- **The deployed site.** Everything here is local. The same driver pointed at the deployed site
  would exercise the real edge, the real Functions and the real bundle anonymously, and needs no
  credential and no seeding — but it is a second job in `build.yml`, not a check in this one.
- **Screen readers**, which no harness closes.
- **A `_redirects` regression**, still invisible locally for the reason stage one recorded.
