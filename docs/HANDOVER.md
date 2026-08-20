# Handover

**The next slice is a visual redesign, and it is the first slice in this project's history
where the brief is not "make the rules right".** The rules are right. What is wrong is that
nothing on screen shows it.

Read [`CLAUDE.md`](../CLAUDE.md) and [`PROGRESS.md`](../PROGRESS.md) after this file.

---

## Where things stand

**4130 tests** — 3685 engine, 387 bUnit, 58 accounts — zero warnings at CI strictness, and a
whole-tree Qodana scan reporting **0 findings** (measured on a clean `git archive` export, not
assumed). Live at **superheroes.softwaresamurai.net**.

**Accounts are on.** The D1 migrations are applied to the remote database, the `DB` binding
exists, and `/api/me` answers `401` carrying JSON — which is the deploy's own pass condition and
the only thing that would notice a broken binding. What is *not* verified is a sign-in from end
to end: that needs somebody to receive a link, and no test can do it. Resend is configured
against `superheroes.softwaresamurai.net` as a subdomain, deliberately, because the apex carries
Proton Mail's records — see [`ACCOUNTS-SETUP.md`](ACCOUNTS-SETUP.md) for why a second SPF record
at the apex would break the owner's personal mail.

Landed since the last handover: #61 (the plural-characters restack), #62 and #63 (hosting docs
and the Resend traps), #64 (Qodana to zero, plus a proof guard that mutation found).

---

## The next slice: a visual redesign

### Why, stated honestly

The app looks like a printout. That is not an accident — the printed sheet **is** the
deliverable, the screen design was derived from it, and the palette is white paper with navy
ink because that is what a character sheet is. Judged as a document it is fine.

Judged against what people now expect of a companion app for this game, it is not close.
**[pnpready.com](https://www.pnpready.com/) is the comparison, and it should be studied before
anything is designed here.** What follows was measured off that page, not eyeballed:

| | P&P Ready | This app |
|---|---|---|
| Ground | `oklch(0.1549 0.017 252.63)` — near-black navy, with three lighter elevation steps above it | white paper |
| Display face | Bebas Neue, **112px** for the page's one headline, uppercase, tracked | Oswald, 2.15rem |
| Accents | gold `oklch(0.84 0.16 88)` and crimson, on the dark | one navy, one crimson |
| Numerals | huge, with tiny uppercase labels beneath — the number *is* the graphic | same size as body text |
| First screen | an interactive dice roller that teaches the core mechanic | a paragraph explaining tiers |

**The colour is the least important row in that table.** Three things matter more:

1. **They demonstrate the mechanic; we describe features.** Their landing page rolls six dice,
   colours each by what it contributes (gold six = +2, cream even = +1, grey odd = nothing),
   lays the arithmetic out as a formula in large numerals, and tags the outcome band with
   *"YOU'RE HERE"*. Then offers **Roll Again**. A reader learns how the game works by touching
   it. Our equivalent surface is prose.
2. **Numbers are design material.** This app computes Edge, Health, Resolve and a Hero Point
   total that are the whole point of it, and sets them at the same size as a sentence. The `.hp`
   treatment deliberately makes costs *quieter* — right for a printed form, wrong for a screen
   where the total is the thing the player is watching.
3. **The copy is editorial.** "You're up." / "Your players go left. You're already there." Ours
   says "Choose a tier". Both are honest; only one is written.

**Our real advantages are invisible.** The engine is verified against the book field by field,
the validator names the rule you broke, there is a replay of real conversations, a printed sheet
modelled on the published one, and an MCP server. A visitor sees none of that in the first
screen. **The redesign's job is to make the substance visible, not to add decoration.**

### The one decision to make before designing anything

The user's own framing was *"hero/villain as light/dark modes"*. That is currently **not** how it
works, and the difference is a real fork:

- **Today:** `:root[data-mode="hero"]` and `:root[data-mode="villain"]` are two palettes of the
  same lightness. There is no `prefers-color-scheme` anywhere in `web/wwwroot/css/` — grep it —
  and no light/dark concept at all. A third palette exists inside `@media print`.
- **Option A — Hero is light, Villain is dark.** One axis. Cheapest, and it makes the mode
  switch dramatic. But it re-couples identity to theme, which this codebase deliberately
  separated (`IsVillain` is presentation and nothing else), and it means a player who wants a
  dark UI has to build a Villain.
- **Option B — two independent axes:** light/dark × Hero/Villain, four token sets. Correct, and
  what "dark mode" normally means. Four times the palette work and every contrast pair needs
  re-measuring.
- **Option C — commit to dark only**, as the reference does, keeping Hero/Villain as accent
  identity within it. One palette to get right, the most confident look, and print is already a
  separate palette so paper is unaffected.

**Recommendation: C.** It is the least work of the three, it is the direction the comparison
points at, and it does not put a mechanic back on `IsVillain`. Confirm with the owner before
starting — this decides the whole slice.

### What the slice must not break

Every one of these is asserted by a test, and all of them are load-bearing:

- **No component names a colour, a typeface, or a raw length.** Checked by hex, keyword,
  `rgb()`/`hsl()`/`oklch()` function syntax, `font-family` *and* `font:` shorthand. All values
  come from tokens in `theme.css`; `app.css` may not declare a custom property at all.
- **`--primary` is a fill and `--heading` is text, and they must stay apart.** Villain
  `--primary` measures 2.0:1 on its surface and is unreadable as type.
- **Contrast is measured, not eyeballed.** `--muted` is held to 4.5:1 because it carries prose at
  0.72rem, and `--text-xs` is pinned at 0.72rem *because that is the size the ratio was measured
  at*. `--focus` is a separate token from `--accent` because a focus ring needs 3:1 and Hero
  `--accent` is 1.8:1. A dark ground invalidates **every one of these measurements** — re-measure
  the lot, and note that the print palette has a luminance test while the screen palette does
  not. Consider adding one.
- **The print stylesheet stays white paper and dark ink** whatever the screen does. It restates
  every token the screen palettes declare; one left out keeps its screen value through the
  cascade, which is exactly how a full-bleed ink dump shipped once.
- **`prefers-reduced-motion` turns every animation off** by setting three duration tokens to
  `0.01ms`, not `0`.
- **The sheet still prints on one page.** Judge it by the PDF, never the screen — the harness for
  rendering `SheetView` to a real printed page is in `CLAUDE.md`.

### Where to start

1. **Look at the running app.** Three defects were found in one screenshot of the tier page
   during this session — an unstyled OS file picker, a card grid flush against the panel below
   it, and a tooltip explaining a design decision instead of answering a question. **The whole
   suite was green through all three**, because every guard in this repository reads source or
   markup and none of them looks at a page. Fixed in `fix/ui-papercuts`; the lesson is the
   reason they existed.
2. **Then read `docs/FRONT-END-PLAN.md`.** Phase 3's last two items (validation on the row where
   the mistake is made, and undo) and Phases 4–5 are still open, and Phase 4 — the sheet as a
   live preview column — overlaps the redesign heavily. Do not do them separately.
3. **Pick the one screen that should demonstrate rather than describe.** The strongest candidate
   is the Hero Point budget: it already recomputes live, it already has a breakdown, and it is
   the number a player actually watches. Making *that* the hero moment is the closest thing this
   app has to the dice roller.

### Known gaps that belong to this slice

- **`.shell` spaces its children by `.panel`'s `margin-bottom`**, so any non-panel child gets no
  spacing. Patched for the one case that exists; the real fix is a `gap` on `.shell` with the
  margin removed, but `.shell` also holds the sticky budget strip and its negative-margin bleed,
  so it needs proofing on every route.
- **No visual regression testing**, and this slice makes that gap acute. Golden PNGs of the proof
  pages with a per-pixel tolerance would close it. Two things make it viable: the fonts are
  self-hosted, and CI already drives Chrome at a fixed viewport. **Generate the goldens in CI on
  Linux, never from a Windows run** — antialiasing differs and every one will mismatch.
- **No `aria-live` anywhere**, so crossing into over-budget is announced to nobody. If you add
  one it must go on a sibling summary, **never** on `.budget-figure strong`, which `ppCount`
  rewrites up to 60×/s.
- **Screen-reader testing is owed** on the command palette, the pips and the sign-in page.
  `aria-expanded` being asserted as the string `"true"` is not the same as having been listened
  to.
- **Home and End on a rank slider also scroll the document.** The fix is a small interop shim
  like `palette.js`, not a Razor attribute.

---

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

- **A green suite is not a working app.** Three visible defects survived 4130 tests. Anything
  whose substance is *appearance* has no guard in this repository at all.
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
