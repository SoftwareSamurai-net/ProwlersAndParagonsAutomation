# Handover

**The redesign is finished, the follow-ups are merged and deployed, and the next thing is the
pre-1.0 audit.** There is no open PR and no branch carrying work.

What shipped since the last handover, in two waves. The first was the open half of the visual
redesign: `/` became a front door offering two avenues, the builder moved under `/build`, the whole
rulebook became searchable at `/rules` behind an account, every option and Trait says what it is on
hover, and the printed sheet is drawn beside the editors while you build.

The second was a fifteen-agent fan-out over everything that was left, merged one stream at a time
onto an integration branch so that fifteen streams cost **one** deploy:

- **Inviting somebody now emails them** a 3-day single-use one-click link. It sent nothing at all
  before — `invitations.add` granted permission and told nobody, while being called an invitation.
- **An account can change its own display name.** No migration; the column always existed.
- **The error log is readable at `/admin`**, gated exactly as the invitation list.
- **The recorded conversations moved into the worker**, so the replay gate is a real gate and four
  fetches came off every visitor's startup.
- **A labelled evaluation set for the Power search** — 33 expectations, baseline 24, plus a ratchet.
- **Accessibility**: skip link, landmarks, a live region for crossing into over-budget, saved
  feedback, slider key handling.
- **Visual regression**: seven proof pages pixel-diffed against Linux-rendered goldens, in CI.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

---

## Where things stand

**4374 tests** — 3734 engine, 474 bUnit, 166 accounts — **measured on `master` after the merge**,
not carried across from any branch. Nine browser harnesses driven by headless Chrome, plus the pixel
diff. Live at **superheroes.softwaresamurai.net**.

**Re-measure rather than adding to it.** This row has been wrong twice, and the integration that
produced these figures managed to break it twice more on the way — three competing Tests rows
accumulated from keep-both merge resolutions, each claiming a different total. Two commands:

```bash
dotnet test --configuration Release -p:ContinuousIntegrationBuild=true
./scripts/test-worker.sh
```

**`dotnet test` prints one `Passed!` line per project, and there are two.** If you see one, a project
failed to **build** and its result is simply missing from the output — which is the same trap as the
`Catastrophic` one already recorded, in another spelling. Count the lines.

**A whole-tree Qodana scan reports 0.** `./scripts/qodana-scan.sh`; it needs Docker Desktop running,
and refuses rather than reporting a clean scan of nothing when it is not. Do not repeat the zero
without re-running it.

**Sign-in, and now the invitation email, both work end to end — watched, not tested.** A link was
requested on the live site and used; and an invitation was sent from `/admin` to somebody who
received it and signed in. Those are the two claims here no suite backs. What the suites assert is
that a message *builder* is called and a token is minted, which is a different sentence.

**The live D1 database is `prowlers-and-paragons`.** Two documents named `prowlers-accounts`, which
does not exist, so their worked `wrangler d1 execute` examples failed for anybody who followed them.

**Verify a deployed route by its body, never its status.** `_redirects` serves every unmatched path
as `index.html` with a **200**, so a route that never shipped comes back looking like a working page:

```bash
curl -i https://superheroes.softwaresamurai.net/api/rulebook/contents
```

A JSON refusal proves the address is routed *and* that the gate is on it.

---

## The next thing: the pre-1.0 audit

The owner's words: *"nearly ready for 1.0"*. `PROGRESS.md` item 7 has carried the shape of this for
a while and it is now the top of the list. An adversarial audit of all three suites has been run —
twelve areas, each required to prove a gap **by mutation** rather than by reading. See
`PROGRESS.md` for what it found and what was done about it.

The other half of item 7 — *is this repository snapshotable to a fresh agent?* — is untouched. It
asks what a new session can read to know what this repo is without re-tracing every past decision,
and whether `CLAUDE.md` should be redrafted or split into smaller pointer files. It is now over a
thousand lines.

---

## What is left, in the order I would take it

1. **Phase 3's validation-on-the-row**, from `docs/FRONT-END-PLAN.md`. The largest remaining
   front-end item and the one a player would feel: the engine answers continuously and the findings
   still only surface at GM review. A design for it exists — see `PROGRESS.md`. The rows now have
   somewhere to put it, since every option row and every Trait row grew an `aria-describedby`
   target, **but a rule you have broken must be visible and not hover-only**.
2. **Phase 3's undo.** Three controls can still destroy twenty minutes behind a confirm. A design
   exists for this too.
3. **The `search_powers` scorer.** `PROGRESS.md` item 4 forbade touching it until a labelled set
   existed. It exists now: 33 expectations, **24 met**, with a ratchet that fails if the count
   drops. That is exactly what lets somebody try a scoring change and see whether it helped or only
   moved the failures around.
4. **The four published Heroes 1 HP out.** Every cheap explanation is spent.
5. **Durable telemetry**, deferred by the owner — `PROGRESS.md` item 9 has the research and the trap
   that would break the site silently if anybody migrates.

---

## Still open from before, unchanged

- **`.shell` spaces its children by `.panel`'s `margin-bottom`**, so any non-panel child gets no
  spacing. The real fix is a `gap` on `.shell` with the margin removed, but `.shell` also holds the
  sticky budget strip, so it needs proofing on every route.
- **Screen-reader testing is owed** on the command palette, the pips, the sign-in page, the
  light/dark control, the row descriptions and the rules search. `aria-pressed` asserted as the
  string `"true"` is not the same as having been listened to, and the new live region is exactly the
  kind of thing that needs hearing rather than asserting.
- **The browser payload is ~27 MiB** because trimming is off — `PROGRESS.md` item 5.

---

## What this round learned, that the next one needs

- **Mergeable is about text, not agreement, and it bit here.** A branch cut before the transcripts
  moved into the worker still carried the startup `ReplayLibrary.LoadAsync`; both registrations
  merged cleanly and keeping both would have silently restored four fetches for every visitor. When
  merging parallel branches, read what each one *meant*, not just whether git was quiet.
- **A guard that cannot tell an explanation from a directive taxes the explanation.** A csproj
  contract scan used `Contains`, so writing down *why* transcripts are no longer staged failed the
  test. Strip comments before scanning, and keep a positive control that plants the real thing.
- **Some things a build cannot fix.** `wwwroot/**` is globbed at project *evaluation*, so a
  `RemoveDir` of a stale staged directory always breaks its own build. Recorded in the csproj.
- **A proof page whose content depends on test order cannot be pixel-checked.** Three were written
  by a `[Theory]` into fixed filenames, so the palette was a coin toss. Nothing before the pixel
  diff had ever compared those pages byte for byte.
- **An audit is worth running even when everything is green.** Two real bugs came out of one:
  a search route where any signed-in account could spend **2.7 seconds** of CPU on a 70KB query, and
  a page that existed, rendered, was tested, and could be reached by nobody.

---

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
- **A `sed` pattern containing `||` is split by the shell**, and the tail is redirected into a file
  named after the fragment. That happened here, the junk file was committed, and it took a second
  commit to remove. Anything with shell metacharacters in it wants an exact-match editing tool, not
  `sed -i` through a shell.
- **A mechanical "keep both" conflict resolution can cut through a method.** Resolving two agents'
  additions that way left unbalanced braces in a test helper — and solution-level `dotnet test`
  reported one `Passed!` line for the project that still built, so the suite *looked* green while a
  whole project failed to compile. Count the `Passed!` lines; there should be one per project.
- **Two branches can merge cleanly and still contradict each other.** One cut before a change and
  one after it offered git two registrations, both applied without complaint, and keeping both would
  have undone the earlier change silently. Git's silence is about text.
- **Do not let a doc name an external resource without checking it exists.** Two files told a stuck
  reader to run `wrangler d1 execute prowlers-accounts`; there is no such database. A value that
  lives in somebody else's dashboard goes stale — write the command that *finds* it instead.
