# Handover

**The palettes are done. The half of the redesign that matters is not.**

The last slice settled light/dark × Hero/Villain into four measured token sets and gave the app
a theme control. That was the tractable half. What the brief was actually about — *make the
substance visible, not add decoration* — is still open and is the next slice.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

---

## Where things stand

**4189 tests** — 3722 engine, 409 bUnit, 58 accounts — zero warnings at CI strictness, and a
whole-tree Qodana scan reporting **0 findings** (measured on a clean `git archive` export, not
assumed). Seven browser harnesses driven by headless Chrome in the build workflow. Live at
**superheroes.softwaresamurai.net**.

**Accounts are on.** The D1 migrations are applied to the remote database, the `DB` binding
exists, and `/api/me` answers `401` carrying JSON. What is *still* not verified is a sign-in from
end to end: that needs somebody to receive a link, and no test can do it. Resend is configured
against `superheroes.softwaresamurai.net` as a subdomain, deliberately — see
[`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) for why a second SPF record at the apex would break the
owner's personal mail.

**Four palettes, on two independent axes.** hero-light and villain-dark are the two that always
existed, values unchanged; hero-dark and villain-light are new. The theme is `data-theme` on the
document element with **three states** — an explicit `light`, an explicit `dark`, and no attribute
at all, which follows `prefers-color-scheme`. The preference is `pp.theme.v1` in local storage:
per-browser, not on `CharacterSheet` and not on the account.

---

## The next slice: make the substance visible

### The gap, restated

[pnpready.com](https://www.pnpready.com/) is the comparison and it is still worth studying. The
difference is not colour and never was:

1. **They demonstrate the mechanic; we describe features.** Their landing page rolls six dice,
   colours each by what it contributes, lays the arithmetic out as a formula in large numerals,
   and tags the outcome band with *"YOU'RE HERE"*. Then offers **Roll Again**. A reader learns how
   the game works by touching it. Our equivalent surface is a paragraph about tiers.
2. **Numbers are design material.** This app computes Edge, Health, Resolve and a Hero Point total
   that are the whole point of it, and sets them at the same size as a sentence. Their one
   headline is **128px**; our largest type is `--text-3xl` at 2.15rem. The `.hp` treatment
   deliberately makes costs *quieter* — right for a printed form, wrong for a screen where the
   total is the thing the player is watching.
3. **Our real advantages are invisible in the first screen** — an engine verified against the book
   field by field, a validator that names the rule you broke, a replay of real conversations, a
   printed sheet modelled on the published one, an MCP server.

**One correction to the previous handover's reading of that site.** Its *app* palette is light —
`--color-background: oklch(98.47% .002 247.84)` — and only the marketing page sits on the
near-black navy. Do not take "they went dark" as the lesson; the lesson is the numerals and the
demonstration.

### Where to start

**The Hero Point budget was the strongest candidate and a first pass on it is done** — see
"The Hero Point budget becomes the hero moment" in `PROGRESS.md`. The sticky strip's spend figure
is now the app's largest numeral (`--text-3xl`, matching the four figures on the derived-stats
step and the sheet) rather than a step behind them, set in `--heading` rather than plain ink; the
breakdown disclosure draws each of the six categories as a proportional meter on the same
`--accent`/`--panel-sunk` pair the sticky rail already uses, so a reader sees where the points
went rather than only reading six numbers. **This did not touch the animation** — `ppCount` and
the count-up behaviour are unchanged, and the `ppCount` rule in `CLAUDE.md` still applies to
anything that does.

**What is still open, and is the larger half of "make the substance visible":** a first screen
that demonstrates the mechanic rather than describing it — the tier page (`/`) is still six cards
of description, and the budget strip only exists once a tier is chosen, so it cannot be that
first demonstration on its own. Then `docs/FRONT-END-PLAN.md` — Phase 3's last two items
(validation on the row where the mistake is made, and undo) and Phase 4, the sheet as a live
preview column. **Phase 4 overlaps this heavily; do not do them separately.**

### What the slice must not break

Every one is asserted, and all are load-bearing:

- **No component names a colour, a typeface, or a raw length.** `app.css` may not declare a custom
  property at all.
- **Four palettes now, not two.** `EveryScreenPairInUseHoldsItsContrastFloor` is a `[Theory]` over
  all four and measures real WCAG ratios, resolving `var()` and `color-mix()`. **Add the row and
  let the test tell you** — do not adjust by eye, do not weaken a floor. If a new surface puts two
  tokens together that no rule currently does, that pair is unasserted until you add it;
  `--muted` on `--accent-soft` is the obvious one.
- **`--primary` is a fill and `--heading` is text.** Villain `--primary` is 2.0:1 on its surface.
- **The print stylesheet stays white paper and dark ink in all four**, and the guard for it
  resolves the whole cascade rather than reading the print block. See below.
- **`prefers-reduced-motion` turns every animation off** via three duration tokens at `0.01ms`.
- **The sheet still prints on one page.** Judge it by the PDF, never the screen.

### What this slice learned, that the next one needs

- **`@media` contributes nothing to specificity, and that nearly shipped a bug.** A dark palette
  block guarded by `:not([data-theme="light"])` is (0,3,0); the print block is (0,2,0). Left
  unscoped the dark blocks would have outranked print, and a reader in dark mode would have
  printed a full-bleed near-black page — with every existing guard green. `@media screen` on the
  dark half is the fix. **If you add a screen rule that print must override, check the
  specificity, not the source order.**
- **A guard that has never failed is a claim.** The cascade-resolving replacement for that guard
  applied rules in *source order* and passed with the bug re-introduced. It weighs specificity
  now. An earlier, wider mutation had appeared to catch it and had not — it tripped the
  resolver's refusal to model an unknown at-rule, which reads exactly like a catch.
- **A C# guard cannot see a JavaScript property.** Deleting the `localStorage.setItem` from
  `theme.js` — so the theme is forgotten on reload — left all 4,115 tests green. Anything whose
  substance is in a script needs a browser harness; there are seven now.
- **Copy answers what the reader came to do.** Four places explained the app to a developer and
  the owner found all four by reading it, not by any test. The rule and its two guards are in
  `CLAUDE.md`; the short version is that design rationale goes in a `@* *@` comment.
- **Headless Chrome here reports `prefers-color-scheme: dark`**, so an un-stamped proof page
  renders the dark palette. Force `data-theme="light"` on the harness to judge a light one.

### Known gaps that belong to this slice

- **`.shell` spaces its children by `.panel`'s `margin-bottom`**, so any non-panel child gets no
  spacing. The real fix is a `gap` on `.shell` with the margin removed, but `.shell` also holds
  the sticky budget strip, so it needs proofing on every route.
- **No visual regression testing**, and this slice makes that gap acute — four palettes now, and
  every screenshot is judged by eye. Golden PNGs of the proof pages with a per-pixel tolerance
  would close it; the fonts are self-hosted and CI already drives Chrome at a fixed viewport.
  **Generate the goldens in CI on Linux, never from a Windows run** — antialiasing differs.
- **No `aria-live` anywhere**, so crossing into over-budget is announced to nobody. If you add one
  it must go on a sibling summary, **never** on `.budget-figure strong`, which `ppCount` rewrites
  up to 60×/s.
- **Screen-reader testing is owed** on the command palette, the pips, the sign-in page and now the
  light/dark control. `aria-pressed` asserted as the string `"true"` is not the same as having
  been listened to.
- **Home and End on a rank slider also scroll the document.** The fix is a small interop shim.

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

**A table in D1, read by hand in SQL. No admin endpoint.**

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

## The strongest feature idea on the table: a searchable rules index

Raised by the owner, and it is worth its own slice because **the data is already extracted and
nobody is reading it.** `data/rulebook/` holds the printed text of all ten chapters with the page
each section came from, generated by `tools/RulebookExtractor` and guarded by tests. Today it is
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
  owner's call and should be settled before any UI exists.
- **Search that admits when it found nothing.** `search_powers` in the MCP server already solved
  the harder version of this problem and the reasoning transfers directly: matching is word by
  word with a shared-prefix rule rather than by substring, because substring matching answered
  *"she bakes bread in the city"* with **Plasticity** — and a wrong match that looks plausible is
  worse than no match. Read `Mentions` and its tests before writing a second search.
- **Index server-side, not in the browser.** The corpus is ~250KB for one chapter; ten chapters
  in the WebAssembly payload is not viable and would also put the book on the open web.
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
- **The rulebook reader serves Chapter 2 only.** Adding a chapter is one line in
  `scripts/inline-rulebook.mjs` and a decision about what an account is entitled to read.

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
