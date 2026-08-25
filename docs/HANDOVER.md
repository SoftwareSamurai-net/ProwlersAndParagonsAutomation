# Handover

**The redesign is done, both halves. What is left is the list at the bottom, and none of it is a
defect.**

Light/dark × Hero/Villain was the tractable half and was already settled. The half the brief was
actually about — *make the substance visible, not add decoration* — landed as an
information-architecture change rather than as one widget: `/` is a front door offering two
avenues, the builder is under `/build`, the whole rulebook is searchable at `/rules` behind an
account, every option and Trait says what it is on hover, and the printed sheet is drawn beside the
editors while you build. `PROGRESS.md` has the full account under *"A front door with two
avenues"*.

**What the previous handover asked for, and how it was answered.** It named three candidates for
what a first screen should demonstrate — a dice roller, a live cost, a verdict — and said to take
the choice to the owner before building. That was done and **the answer was none of the three**:
present the avenues, with the working assumption that somebody arriving is here to build a
character rather than to look a rule up. The reason none of them fitted is worth carrying: **`/`
was both the first screen and step one of the wizard**, so any demonstration parked there would
have re-opened the decision `ChooseTier.razor` already recorded when the samples were moved off it.

**It is merged and deployed.** [#73](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/73)
went into `master` as `9ff148e`; Build, Deploy and Qodana are all green on it, and the new routes
are live. **There is no open PR and no branch carrying work**, so the next session starts with
nothing to reconcile.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

---

## Where things stand

**4303 tests** — 3730 engine, 449 bUnit, 124 accounts — **re-measured on `master` at `9ff148e`
after the merge**, not carried across from the branch, which is the mistake this row records below.
Nine browser harnesses driven by headless Chrome in the build workflow (two are new: the front door
and the rules reference at 375px). Live at **superheroes.softwaresamurai.net**.

**The new server routes were checked in production, not inferred from a green deploy.** All three
answer `401` with `application/json`:

```bash
curl -i https://superheroes.softwaresamurai.net/api/rulebook/contents
```

That is the check worth making rather than fetching a page, because `_redirects` serves every
unmatched path as `index.html` with a **200** — so a route that never shipped comes back looking
like a working page, and only the body tells you. A JSON refusal proves the address is routed *and*
that the gate is on it.

**Re-measure this rather than adding to it.** It has been wrong twice in a fortnight: three
branches each claimed a different total for the same tree, and then this file copied one of them
and carried it through a merge. Two commands, and they disagree with nothing:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
```

**A whole-tree Qodana scan reports 0**, measured on `master` at `9ff148e` — after the merge, not on
the branch — from a report that exists rather than from an exit code. It got there by being run four
times: 23 on the first pass, all in code the redesign added; 2 after fixing them; 0 after the last
two; 0 again on the merge commit.

**Do not repeat that zero without re-running `./scripts/qodana-scan.sh`**, which is a rule this
repository has broken twice. It needs Docker Desktop running; without it the script exits non-zero
saying so, rather than reporting a clean scan of nothing.

**Sign-in works end to end, and that sentence has never been true before.** A link was requested
on the live site, arrived, and signed somebody in. Every handover before this one said the same
thing the other way round — that it needed somebody to receive a link and no test could do it —
so this is the one claim here that no suite backs and that somebody watched happen.

All four migrations are applied to the remote database (`invitations` and `error_log` were the
outstanding pair) and all four variables are set: `RESEND_API_KEY`, `MAIL_FROM`, `SITE_URL`,
`ADMIN_EMAIL`. **The deploy workflow does not run migrations** — deliberately — so a future
migration is a manual step again, and `invitations` is queried unguarded on the sign-in path,
which is what makes an unapplied one visible immediately.

**Getting there took four deploy cycles and diagnosed nothing, which is why
`scripts/probe-mail.mjs` now exists.** The fault was the API key; every reading of the evidence
said otherwise. See *Testing the mail path without deploying* in
[`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) — that file also carries the Resend subdomain reasoning,
since a second SPF record at the apex would break the owner's personal mail.

**This site is not a sign-up.** Only invited addresses may ask for a link, and the list is managed
at `/admin` by somebody already signed in — reached by its address, with no link in the navigation
and no button that appears only for administrators, because the browser holds no claim about who
anybody is. Asking for a link still always answers `204`, so the page cannot be used to ask who is
on the list.

**Four palettes, on two independent axes.** hero-light and villain-dark are the two that always
existed, values unchanged; hero-dark and villain-light are new. The theme is `data-theme` on the
document element with **three states** — an explicit `light`, an explicit `dark`, and no attribute
at all, which follows `prefers-color-scheme`. The preference is `pp.theme.v1` in local storage:
per-browser, not on `CharacterSheet` and not on the account.

---

## The redesign, and what it settled

[pnpready.com](https://www.pnpready.com/) was the comparison and the reading of it in the previous
handover was right about the diagnosis and wrong about the remedy, in an instructive way.

The diagnosis: **they demonstrate the mechanic and we described features**, and **numbers are
design material** — this app computes Edge, Health, Resolve and a Hero Point total that are the
whole point of it, and set them at the same size as a sentence.

The remedy it proposed was a demonstration on the first screen, and it offered three candidates —
a dice roller, a live cost, a verdict. **The owner chose none of them.** What was actually wrong
with the first screen was not that it failed to demonstrate: it was that `/` was *step one of the
wizard*, so a visitor who had not decided what they came for was already inside a job. The site
does two things and only one of them was reachable without knowing the address of the other.

So the answer was to present the avenues, and to let the figures do the work the demonstration was
meant to do: the front door's numerals are the engine's — 141 Powers off the loaded rules, the
spend off the same `TryCost` the budget strip calls — at the same size and in the same ink as the
four derived stats. Nothing on that page is typed in.

**One correction to the previous handover's reading of that site, still worth keeping.** Its *app*
palette is light — `--color-background: oklch(98.47% .002 247.84)` — and only the marketing page
sits on the near-black navy. Do not take "they went dark" as the lesson.

### What is now true that was not

- **`/` is a front door**, `/build` is the six creation steps, `/rules` is the reference, `/admin`
  holds the account pages and the portfolio. `Areas.Of` decides every band of chrome from the first
  segment, and an unrouted address falls to the front door rather than to the builder.
- **The whole rulebook is searchable behind an account**, cited by printed page. All ten chapters
  are baked into the worker; the entitlement question that blocked this is settled.
- **Hovering an option or a Trait says what it is.** The descriptions were in `data/rules` the
  whole time with nothing showing them.
- **The sheet is drawn beside the editors** on the characteristics step above 1500px.

### What the next session should know before touching any of it

- **`SheetView` only redraws because it subscribes**, and it subscribes only when it is showing the
  session's own character. It takes no parameter that changes, so Blazor skips it otherwise. If a
  sheet ever looks stale, that is the first place to look — and if you make it subscribe
  unconditionally you will tie a recorded character to the visitor's edits, which the replay guard
  will catch and which is worth understanding before you fight it.
- **The portfolio gate is a front door rather than a lock**, and the honest sentence for that is in
  `CLAUDE.md`. The transcripts are still ordinary files under `wwwroot`. Making it real means
  serving them from the worker as the rulebook is — which would also take four fetches out of every
  visitor's startup — and is a refactor of `ReplayLibrary.LoadAsync` and `Program.cs`. It is the
  cleanest small piece of work left.
- **The old `/portfolio` and `/replay` addresses 404 now**, deliberately. The content is
  account-gated, so a public link that still worked would be the wrong answer.
- **The search's honesty flags say how results matched, never what to conclude.** `found: 0` is the
  only answer meaning the book is silent. The first version of the MCP Power search got this exactly
  wrong and told a reader the rulebook had nothing while a dozen real passages sat under the
  sentence. Read `worker/search.js`'s header before changing the matching.
- **The baker's sentence proves something narrower here than it does in the MCP server**, and the
  first version of that comment claimed the wider thing. Over there it finds nothing; here it finds
  twenty-one real passages, because "city" is a word the book uses. What must not happen is that it
  reaches **Plasticity**.

### What is left, in the order I would take it

1. **Phase 3's validation-on-the-row**, from `docs/FRONT-END-PLAN.md`. The largest remaining item
   and the one a player would feel: the engine answers continuously and the findings still only
   surface at GM review. Note that the rows now have somewhere to put it — every option row and
   every Trait row grew an `aria-describedby` target — but a rule you have broken must be *visible*
   and not hover-only.
2. **Phase 3's undo.** Three buttons can still destroy twenty minutes behind a confirm.
3. **Serving the transcripts from the worker**, which turns the portfolio's front door into a lock
   and shrinks the startup fetch. Small and self-contained.
4. **Visual regression testing.** The gap was already acute at four palettes and this slice added
   three whole screens. Golden PNGs of the proof pages with a per-pixel tolerance would close it;
   the fonts are self-hosted and CI already drives Chrome at a fixed viewport. **Generate the
   goldens in CI on Linux, never from a Windows run** — antialiasing differs.

### Still open from before, unchanged

- **`.shell` spaces its children by `.panel`'s `margin-bottom`**, so any non-panel child gets no
  spacing. The real fix is a `gap` on `.shell` with the margin removed, but `.shell` also holds the
  sticky budget strip, so it needs proofing on every route.
- **No `aria-live` anywhere**, so crossing into over-budget is announced to nobody. If you add one
  it must go on a sibling summary, **never** on `.budget-figure strong`, which `ppCount` rewrites up
  to 60×/s.
- **Screen-reader testing is owed** on the command palette, the pips, the sign-in page, the
  light/dark control — and now on the row descriptions and the rules search. `aria-pressed` asserted
  as the string `"true"` is not the same as having been listened to.
- **Home and End on a rank slider also scroll the document.** The fix is a small interop shim.

### What this slice learned, that the next one needs

- **A guard shaped by the code rather than by the claim is a guard that gets worked around.** Three
  were widened here: the contract scanner required `searchParams.get('field')` *on the expression*,
  so hoisting the parameters into a local reported a field the server plainly reads as unread;
  `NoScreenCalcNamesARawLength` refused `100vh`, which names the container exactly as `100%` does;
  and the corpus sync test asserted exactly one chapter. **Widen to the claim, and pin the
  exemption as narrowly as the claim allows** — the `calc` exemption is on the figure 100, not on
  the unit, and `37svh` is still refused. Watched, both ways.
- **Three faults were found by looking at a screenshot and by nothing else.** A dotted underline
  drawn in `--rule` is invisible under a word — and it was the only marking on a control. A CSS
  comment claimed the preview keeps the sheet's three columns and it does not. Search results sat
  unframed between two panels. Every rendering test passed through all three.
- **A component can look live and not be.** See `SheetView` above. The tab strip beside it was
  updating, which is what made it read as working.
- **`display: none` and `visibility: hidden` are not interchangeable for an absolutely-positioned
  tip.** The precedent was already recorded on `AClosedTipTakesNoLayoutBox` and it applied here
  unchanged: a hidden element keeps its box.
- **A `sed` pattern containing `||` will be split by the shell** and the tail redirected into a
  file named after the fragment. It happened here, the file was committed, and it took a second
  commit to remove. Use an exact-match editing tool for anything with shell metacharacters in it.


## Error reporting: two audiences, one failure

> **Built.** [#66](https://github.com/DorianSheiles/ProwlersAndParagonsAutomation/pull/66) did the
> cheap half — a 500 stopped being reported as an unreachable site and gained a reference — and
> the rest landed as designed below: the four categories in `worker/errors.js`, the `error_log`
> table read by hand, and one sentence per category on the sign-in page. See the completed entry
> in [`PROGRESS.md`](../PROGRESS.md) for what the mutation pass found, and
> [`docs/ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) for how to read the log.
>
> **The reasoning below is kept because it is the reasoning, not a plan.** Every constraint in it
> is now load-bearing on shipped code — especially the one about a category never depending on
> whether an account exists, which is the security property the whole taxonomy is shaped around.
>
> **And it has a second axis now.** The invitation list landed in the same reconciliation, so
> "an account exists" is no longer the only thing a category could betray — "this address was
> invited" is the other. Reconciling the two found one place it leaked: the gate had been written
> above the `SITE_URL` check, so a misconfigured deployment answered an invited address with a
> 500 and a stranger with `204`. The deployment check goes first now, and
> `a broken deployment answers an invited and an uninvited address identically` pins it. Two
> channels stay open and are recorded in the gate's own comment rather than papered over: an
> uninvited address does not wait on the mail provider, and while that provider refuses
> everything an invited address gets a 500 where a stranger still gets `204`.

**Independent of the redesign and much smaller — a day, not a slice.** It touches `worker/` and
one client message and nothing the redesign will move.

**Scoped here rather than built**, because the shape is a decision and the privacy half is not
reversible once a table exists.

**The problem is that the two audiences want opposite things.** A visitor needs to know whether to
retry, wait, or report — and nothing else, because an internal message is both meaningless to them
and a disclosure. The owner needs to know what actually threw. Today the visitor gets one flat
sentence and the owner gets a live tail: close it and the error is gone, so any failure nobody
happened to be watching for is unrecoverable.

### The middle ground: a closed set of categories

Not a free-text message on either side. **The catch classifies the failure into a small fixed
set**, and each side renders that category its own way:

| Category | What the visitor is told | Why it is safe to say |
|---|---|---|
| `mail` | "We could not send the email just now." | Names a subsystem, not a cause. Tells them the address was fine and the useful move is to try later or report it. |
| `storage` | "We could not save that just now." | Same shape. Distinguishes "your work did not persist" from "your work was rejected", which is the difference they actually need. |
| `configuration` | "This site is not set up correctly. Reporting this would help." | The one that must never say retry, because retrying cannot fix it. This is the category the sign-in failure would have landed in. |
| `unknown` | "Something went wrong at our end." | The honest default. Anything unclassified lands here rather than being guessed at. |

**Three constraints on the taxonomy, and the first is a security property rather than a style
rule:**

- **A category may never depend on whether an account exists.** Every refusal to sign in says the
  same thing today, and asking for a link always answers 204, precisely so the endpoint cannot be
  used to ask whether an address is registered. A category that appeared only for known addresses
  would reintroduce that oracle through the error path. Categories describe the *subsystem that
  failed*, never the request that reached it.
- **The set is closed and small.** A category per throw site becomes a description of the internals
  by enumeration, which is the disclosure this is meant to avoid.
- **`unknown` must stay reachable.** A taxonomy with no default grows a category for every new
  failure, and the pressure is then to classify by guessing.

### The private half

**A table in D1, read by hand in SQL. No admin endpoint** — superseded once the invitation list
landed: it made "am I an admin" a question the server already answers on every request, so a
read-only `/api/admin/error-log`, gated by that identical check, added no role to `Identity` and
no new concept. See the completed entry in [`PROGRESS.md`](../PROGRESS.md) and the reversal
recorded in `d1/migrations/0004_error_log.sql`. The reasoning below is kept for the same reason
the section above is: it is still why the table is shaped the way it is, only the "read by hand
alone" half of the conclusion changed.

- **No admin route, deliberately.** `Identity` carries a key and a name and no role — there is a
  test asserting the wire identity holds nothing else — so "am I an admin" is not a question the
  client can ask, and inventing a role to answer it is a much larger change than this needs. The
  precedent is `users.character_limit`, which is raised by hand in SQL on the reasoning that a cap
  you can raise on yourself is not one. Read errors the same way: `wrangler d1 execute`.
- **Never the address, and there is precedent in the schema's own comments.**
  `0001_accounts.sql` says an id that is an address "puts the address into every log". The same
  applies here, more directly. Store the route, the category, the exception's *type*, a timestamp
  and the reference — and if the message is stored at all, redact it.
- **On redaction, be honest about what it buys.** `users.email` is in that database in the clear
  already, by necessity, so an error row is not a new exposure *boundary*. What redaction protects
  against is different and still worth having: the error log is the thing most likely to be read
  aloud, pasted into an issue, or screenshotted. An exception from D1 or a mail provider can quote
  a query or an address — `worker/index.js` says so where it refuses to pass the message on — so a
  stored message needs the addresses and long random strings stripped, and the table should never
  be treated as safe to publish.
- **Cap the writes.** A failing dependency will throw on every request, and an unbounded log turns
  one outage into a full database. Dedupe on `(category, route)` inside a window, or keep a count
  against one row rather than inserting per occurrence. Whatever the mechanism, **log what was
  dropped** — a silently truncated error log reads as a quiet period.
- **Prune.** Decide a retention window and enforce it in the same statement that writes, or it
  will not happen.

### What the tests have to pin

Two of these are the point of the slice, and both are the plant-and-assert-absence shape this
repository already uses:

- **Throw an exception whose message contains an address and a token-shaped string, then assert
  neither reaches the stored row** — with a positive control that a row was written at all, or the
  assertion passes against a logger that silently does nothing. That control is not optional; this
  repository has shipped four guards that passed by measuring nothing.
- **Assert the public body carries a category and a reference and no exception text**, on the same
  provoked failure. `AccountsContractTests` is the place that can see both halves of the wire.
- **Assert the category never varies with account existence** — provoke the same subsystem failure
  for a registered and an unregistered address and require identical bodies.

### What this is not

- **Not a third-party error service.** Nothing about who somebody is currently leaves the
  Cloudflare account this site already deploys to, and that property is worth more than a nicer
  dashboard.
- **Not stack traces to the client**, in any environment. There is no debug build of a deployed
  site here, so a flag that turns them on is a flag that is one mistake from being on.

---

## The searchable rules index — **built**

> **Done, and the reasoning below is kept because it is the reasoning.** It shipped inside the
> redesign rather than as a slice of its own, because the front door had to offer it somewhere. All
> ten chapters, `worker/search.js`, `/rules`, cited by printed page. Every constraint below is now
> load-bearing on shipped code — especially "search that admits when it found nothing", which is
> the whole design of the honesty flags.
>
> **Two of its predictions were wrong and are corrected in place below.** The entitlement decision
> was taken (an account may read the book, and the restrictions on shipping the rulebook text and
> the published characters are lifted). And the baker's sentence does *not* transfer as stated —
> see the correction under "Search that admits when it found nothing".

Raised by the owner, and it was worth its own slice because **the data was already extracted and
nobody was reading it.** `data/rulebook/` holds the printed text of all ten chapters with the page
each section came from, generated by `tools/RulebookExtractor` and guarded by tests. It was
served one entry at a time, Chapter 2 only, beside a Power in the editor.

Why it is the strongest candidate:

- **It is useful at the table**, which nothing else here is. Character creation happens once;
  looking a rule up happens every session. It is also what the comparison app leads its
  navigation with — *Library*, and *"Search the rules"*.
- **The expensive half is done.** Extraction was the hard, subtle part — two-column layout,
  per-page gutter detection, watermark removal by font, headings by typeface. That is finished
  and tested.
- **It composes with the sign-in that now works.** The reader is already account-gated, and
  `data/rulebook/` is deliberately not staged into `wwwroot` — that placement *is* the access
  control, and there is a test on both sides of the repository.

What a slice would actually involve:

- **A decision about entitlement first, not last.** Serving all ten chapters to any account is a
  different thing from serving Chapter 2 beside a Power. The corpus is the book's text, held here
  by the author's permission to the repository owner — so who may read how much of it is the
  owner's call and should be settled before any UI exists. **Taken: an account may read the book,
  and the restrictions on shipping the rulebook text and the published characters are lifted.** It
  was asked as a blocking question and answered in one line, which is what that item was for.
- **Search that admits when it found nothing.** `search_powers` in the MCP server already solved
  the harder version of this problem and the reasoning transfers directly: matching is word by
  word with a shared-prefix rule rather than by substring, because substring matching answered
  *"she bakes bread in the city"* with **Plasticity** — and a wrong match that looks plausible is
  worse than no match. Read `Mentions` and its tests before writing a second search.

  **The rule transfers; the test for it does not, and assuming otherwise put a false claim in the
  new file's header for one commit.** Over there the baker's sentence finds *nothing*, because the
  haystack is 141 short Power entries. Here it is the whole book, where "city" is a word the text
  genuinely uses — *City of Heroes* in the introduction, "a city, forest, jungle" in Attuned:
  **twenty-one real matches, measured**. The property to pin is that the sentence must not reach
  Plasticity, with the positive control beside it.
- **Index server-side, not in the browser.** The corpus is ~250KB for one chapter; ten chapters
  in the WebAssembly payload is not viable and would also put the book on the open web. **Measured
  as built: 725KB baked into the worker, 224KB gzipped**, well inside the limit — and the index is
  constructed on the first search rather than at module load, because a Worker's startup CPU budget
  is not a thing to spend walking three quarters of a megabyte of prose for a request that may
  never ask a question.
- **Cite the page.** Every section carries its printed page number. A rules answer that names
  "Ch.2 p.29" is checkable against the book on the table; one that does not is a claim.

## Not this slice, but still open

- **Read-only share links.** The `add-read-only-share-links` task carries the brief:
  `shares(sha256(key), character_id, expires_at)`, `GET /api/shares/{key}` with no session, and a
  `/shared/{key}` page rendering through `SheetView` with no editing controls. The one place a
  bearer key beats a session.
- **Four published Heroes rebuild 1 HP out**, each for a recorded reason. Do not tune an
  ambiguous variant to force one to zero — that is fitting the model to the answer.
- **The payload is ~27 MiB uncompressed** because trimming is off: `RulesRepository`
  deserializes by reflection and the trimmer can quietly remove model properties, leaving the
  site running on empty rules. See `PROGRESS.md` item 5.
- ~~**The rulebook reader serves Chapter 2 only.**~~ **Closed.** All ten, and
  `scripts/inline-rulebook.mjs` reads the directory rather than naming files — with a test that it
  names none, because a filename there is a list that goes stale the first time a chapter is added
  and the failure is a chapter silently missing from the search.

---

## What previous sessions got wrong, so you do not repeat it

- **A green suite is not a working app.** Three visible defects survived 4133 tests, and four
  pieces of developer jargon on screen survived 4186 — both found by a person looking at the app.
  Anything whose substance is *appearance* or *wording* has almost no guard here.
- **Qodana's PR-mode count is not comparable to a whole-tree scan.** It reported "9 new problems"
  on a PR that changed one Markdown file. Run the scan yourself on a clean export.
- **Stacked PRs need the base branch to survive.** Do not merge the top of a stack before the
  bottom; GitHub will let you, and the result is an orphan merge that reports success.
- **Any CLI flag written into a workflow gets `--help` at the pinned version first.** A flag was
  invented once and only CI caught it.
- **Runtime version differences ship silently.** Wrangler 3.90.0's bundled esbuild is older than
  Node 22's; both suites and a whole-tree scan passed while the deploy failed to parse.
- **`gh pr merge --auto` merges immediately** on a repo with no required status checks. To
  actually gate on CI, poll `gh pr checks` until green, then merge.
- **Reality beats the docs.** Six files claimed the site was at `pp.softwaresamurai.net` and that
  the domain was "not attached yet". It has been `superheroes.softwaresamurai.net` for some time.
  When a screenshot and a document disagree, update the document.
- **A confident paragraph in this repository can cost more than no paragraph at all.**
  `PROGRESS.md` item 8 argued that a bad API key answers `401`/`403` and therefore could not be
  the cause of a `400`. Every sentence was defensible, the conclusion was wrong, and the variable
  it pointed at was re-entered twice with a deploy each time. The provider answers
  `name: validation_error` for a bad key *and* a bad field, at different statuses. **When a
  document rules a cause out, check what that ruling rests on before you spend a cycle acting on
  it** — and prefer an instrument that observes over an argument that eliminates.
- **A diagnostic that builds its own version of the payload can agree with the bug.** A
  hand-written probe reproduced the exact provider error for an entirely different reason and read
  as a confirmation. `scripts/probe-mail.mjs` imports the real builder for this reason, and two
  tests keep it that way.
- **"The secret exists" is not "the secret is right".** `wrangler pages secret list` shows names
  and never values, which rules out one cause and reads like it rules out four.
- **Do not write a test fixture at the path the real file lives at.** A throwaway `.dev.vars` in
  the repository root overwrote the owner's, and the tidy-up `rm` finished it. It is gitignored:
  no reflog, no stash, nothing to recover, and the provider will not show a key twice. `ls` the
  target before any `>`, `rm` or `mv`, and put scratch files somewhere that is not the repository.
  In the same session a `sed -i` delete whose paired insert failed removed 45 lines of
  `PROGRESS.md` in silence — **a shell redirect is not an editing tool**, and a two-step edit
  where step one destroys is a two-step edit that needs step two to be checked.
