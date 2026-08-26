# S4: negative controls for the proof harnesses

`ProofPages.cs`'s `MustNotShow` dictionary bans hard-coded verdicts by source spelling —
`["proof-sticky.html"] = ["say(true"]`, and similarly for motion and slider. A denylist of
spellings cannot close an unbounded spelling space, and it was proved not to: `|| true`
defeats it, because `|| true` is textually distinct from `say(true`.

The fix is not a longer denylist. It is a negative control: alongside every real proof page
that has one, this file now writes a *twin* that reproduces one documented defect the harness
exists to catch, and CI requires the real page to say `PASS` **and its twin to say `FAIL`**, in
the same run. A harness that reports `PASS` on both proves nothing, and that is now a build
failure.

## Reproducing the audit's defeat

Both weakenings were applied directly to `tests/ProwlersAndParagons.Web.Tests/ProofPages.cs`,
built, regenerated under `PP_PROOF=1`, and driven through headless Chrome with the exact flags
CI uses (`--headless=new --no-sandbox --disable-gpu --allow-file-access-from-files
--virtual-time-budget=8000 --dump-dom`). Both were reverted afterwards; neither shipped.

### Sticky

Changed:

```diff
-            say(rendered && moved && startedBelow && stuck && scrolled,
+            say((rendered && moved && startedBelow && stuck && scrolled) || true,
```

- `TheStickyHarnessMeasuresRatherThanAsserts` (which runs `MustNotShow` on every build, not only
  under `PP_PROOF`) **stayed green** — `|| true` is not `say(true`.
- Driven against a shell carrying the real defect this file's own docstring names
  (`#app { overflow-x: hidden }`, which removes the containing block `position: sticky` needs),
  the weakened harness reported:

  ```
  <title>STICKY: PASS
  <div id="verdict" class="">STICKY: PASS
  scrollY 600
  .budget top: before 117.0, after -483.0 (want ~0)
  .steps bottom after: -483.0 (want negative — scrolled away)</div>
  ```

  The strip moved the full 483px it should have stayed pinned against — a strip that is
  visibly not sticking, reported `PASS`. The honest (unweakened) harness driven against the
  identical broken page reported `STICKY: FAIL` with the same numbers.

### Motion

Changed:

```diff
-          const ok = checks.every((c) => c.ok);
+          const ok = checks.every((c) => c.ok) || true;
```

- `TheMotionHarnessDrivesTheShippedScript` (also unconditional) stayed green for the same reason.
- Driven against a copy of `js/motion.js` with the exact reduced-motion inversion this file's
  own docstring already named — `if (!document.startViewTransition || !still()) return;`
  instead of `|| still())` — under `--force-prefers-reduced-motion`:

  ```
  <title>MOTION: PASS
  <div id="verdict" class="">MOTION: PASS (prefers-reduced-motion: reduce)
    ...
    FAIL begin opens a transition — reduced=true started=1
    FAIL end releases it — releasedAt=released
    FAIL an unreleased transition frees itself — reduced=true started=1 releasedAt=released
    FAIL a transition actually opened (positive control) — reduced=true transitions=2
    ...
    FAIL a second transition releases the first — reduced=true released 2 of 2 opened
    FAIL a transition stays open until end() is called — reduced=true released 0 before end(), 1 after
  ```

  A real View Transition opened for a visitor who asked for no motion — six checks reported
  `FAIL` individually, and the page still said `MOTION: PASS`. The plain (non-reduced) run showed
  the mirror failure: the transition that should have opened did not. The honest harness against
  the same broken script reported `MOTION: FAIL` in both directions.

## The twins

Every parameterised harness template (`StickyHarness`, `MotionHarness`, `ShortcutHarness`,
`ThemeHarness`, `SliderHarness`, `NarrowHarness` — already parameterised — and `MeasureHarness`)
takes the target it drives as its only variable, substituted into an otherwise byte-identical
template. **The verdict-computing script never changes between a real page and its twin** — only
the shell it iframes, or the shipped `.js` file it loads, differs.

| Harness | Real page | Twin | Injected defect |
|---|---|---|---|
| Sticky | `proof-sticky.html` | `proof-sticky-broken.html` | `#app { overflow-x: hidden }` on the shell twin `proof-shell-hero-broken.html` — removes the containing block, the exact category this file's docstring names |
| Motion | `proof-motion.html` | `proof-motion-broken.html` | `js/motion.js` with `!still()` in place of `still()` at the view-transition gate — the exact reduced-motion inversion this file's docstring already named |
| Shortcut | `proof-shortcut.html` | `proof-shortcut-broken.html` | `js/palette.js` with `e.preventDefault();` removed — Ctrl-K reaches the component but never takes the key from the browser |
| Theme | `proof-theme.html` | `proof-theme-broken.html` | `js/theme.js` with the `localStorage.setItem` call removed — the exact regression `CLAUDE.md` already records |
| Slider | `proof-slider.html` | `proof-slider-broken.html` | `js/slider.js` with `e.preventDefault();` removed — Home/End reach the guard and are counted, but the browser default is never taken |
| Narrow | `proof-narrow.html` (+3 targets) | `proof-narrow-broken.html` | `.shell { width: 3000px }` on shell twin `proof-shell-hero-broken-narrow.html` — a real, generously-wide element |
| Measure (insets) | `proof-measure.html` | `proof-measure-broken.html` | `.banner-inner { margin-left: 96px }` on shell twin `proof-shell-hero-broken-inset.html` — moves one band's content out from under the others, the exact defect this file's own comments name |

Every broken `.js` script is *derived* from the shipped file (`WithDefect` reads the real file
and substitutes one documented line, throwing if that line has moved — see `ProofPages.cs`), so
a twin can never silently drift from the file the app actually ships. A hand-written duplicate
would be exactly the trap `MustNotShow` already refuses for the harness scripts' own
`ppMotion`/`ppSlider` object literals.

**No new `.js` file is written to `web/wwwroot`.** `web/wwwroot/proof-*.html` is already
gitignored, and every twin page follows that name — but there is no equivalent pattern for a
generated `.js` file, and this slice is not permitted to touch `.gitignore`. So a broken script
travels as a `data:text/javascript;base64,…` URL substituted into the same `<script src="…">`
placeholder the real page uses (`AsScriptSrc`), rather than as a second file. Verified locally
that a `<script src="data:…">` and a dynamically-created `<script>` with `.src` set to a `data:`
URL (the reload path `ThemeHarness` uses) both execute under the exact CI Chrome flags before
committing to the design.

`proof-narrow-front.html` and `proof-narrow-rules.html` exercise the identical `NarrowHarness`
function already proofed by the shell twin above, so one twin covers all four narrow targets
rather than one per target.

## Mutation table

Driven through headless Chrome on Windows (`chrome.exe`, version 152.0.7977.64) with the same
flags CI uses. Every real page said `PASS`; every twin said `FAIL`.

```
  ok    proof-sticky.html                                    -> STICKY: PASS
  ok    proof-sticky-broken.html                             -> STICKY: FAIL
  ok    proof-motion.html                                    -> MOTION: PASS
  ok    proof-motion.html (--force-prefers-reduced-motion)    -> MOTION: PASS
  ok    proof-motion-broken.html                             -> MOTION: FAIL
  ok    proof-motion-broken.html (--force-prefers-reduced-motion) -> MOTION: FAIL
  ok    proof-shortcut.html                                  -> SHORTCUT: PASS
  ok    proof-shortcut-broken.html                           -> SHORTCUT: FAIL
  ok    proof-theme.html                                     -> THEME: PASS
  ok    proof-theme-broken.html                              -> THEME: FAIL
  ok    proof-slider.html                                    -> SLIDER: PASS
  ok    proof-slider-broken.html                             -> SLIDER: FAIL
  ok    proof-narrow.html                                    -> NARROW: PASS
  ok    proof-narrow-broken.html                             -> NARROW: FAIL
  ok    proof-measure.html                                   -> INSETS: PASS
  ok    proof-measure-broken.html                            -> INSETS: FAIL
```

### The positive control on the negative control

Per instructions, each twin was also un-broken and re-driven, to prove the *twin itself* can be
told apart from a twin that silently stopped reproducing its defect:

- `proof-sticky-broken.html`, with `#app { overflow-x: hidden }` removed from the shell twin,
  reported `STICKY: PASS` — the same page CI expects `FAIL` from would fail the CI step, exactly
  as intended, if the injected defect were ever accidentally undone.
- `proof-motion-broken.html`, with `!still()` reverted to `still()` in the broken script, reported
  `MOTION: PASS` in both the plain and `--force-prefers-reduced-motion` runs, for the same reason.

Both were restored and re-verified (the full sixteen-row table above is the *post-restore* run).

## What was not built, and why

**The narrow-viewport and box-inset harnesses got twins too** (`proof-narrow-broken.html`,
`proof-measure-broken.html`) — the task named these as candidates to judge, not to skip, and the
same "inject one inline `<style>` into a twin copy of the rendered page" mechanism that works for
sticky works for them with no risk to the pixel-diff stream: nothing under `web/wwwroot/css` is
touched, and `scripts/visual-regression.sh` names its seven target pages by a fixed list, not a
glob, so the new twin files are invisible to it.

**A structural check that each verdict identifier is *derived from* the measured variables,
rather than constant, was considered and rejected.** Two reasons:

1. **It would be a second unbounded-spelling denylist, of the exact shape that just failed.**
   Proving an expression "flows from" named variables without a real data-flow analysis means
   either building one (far past what this repository's tooling does — `WebPresentationTests`
   and `ProofPages.MustShow`/`MustNotShow` are string-matching, not parsing) or writing a second
   regex-shaped heuristic. A rewrite as trivial as extracting the boolean into a helper one call
   away, or `const ok = checks.every(c => c.ok) === (1 === 1)`, would dodge a naive structural
   check exactly the way `|| true` dodged `MustNotShow` — the same failure mode the twin exists
   to stop trusting.
2. **The twin is already the stronger property, and it subsumes this one.** A structural check
   asks "does the code look like it computes the verdict honestly"; the twin asks "does the
   harness actually reach the right verdict when the thing it measures is broken" — which is the
   property that matters, and is exactly the discipline `CLAUDE.md` names ("write the guard, then
   break it, then watch it fail"). Anything a derived-ness check could catch that changes real
   behaviour, the twin catches by observation; anything it could catch that does not change real
   behaviour is not worth catching.

`MustNotShow` stays — it is cheap, it still catches the lazy spelling, and its docstring now says
plainly that the twin, not the denylist, is the real guarantee.

## Whether a real browser was driven

Yes, throughout. `chrome.exe` (Google Chrome 152.0.7977.64, Windows) was driven headless with the
exact flags `.github/workflows/build.yml` uses, against every real proof page and every twin, in
both directions where applicable (plain and `--force-prefers-reduced-motion`). This is not the
Linux Chrome CI itself runs, and the pixel-diff stream (excluded from this slice) is explicit that
goldens generated from Windows Chrome are worthless — but the checks here are `<title>` tokens
read out of a `--dump-dom`, not pixels, and the same JavaScript engine's `prefers-reduced-motion`
media query, `Element.animate`, and View Transitions behaviour is what is under test, not layout
rendering fidelity. Nothing in this slice's verification depends on Chrome's rasteriser.
