# The front end, as an application rather than a form

Slice B closed the six items the previous handover named. This is the plan for what comes
after it: turning a correct sheet-filling tool into something that feels like a modern
application. It is a plan, not a commitment — each phase is separable, and the order is chosen
so that the cheap invisible work comes before the expensive visible work that depends on it.

**Read `CLAUDE.md` first.** Everything below is bounded by the disciplines already recorded
there, and a plan that quietly breaks one of them is not a plan.

---

## The decision that shapes everything: no animation library

**Recommendation: none. Use the platform.** This is not caution, and it is not "no JavaScript" —
the app already ships `wwwroot/js/download.js` and calls into it for the palette, the download
and local storage. More JS is easy and the Content-Security-Policy already allows it: a
same-origin `<script src>` needs no hash and no policy change. Only an *inline* script would,
and the header script hashes exactly one of those today.

The argument against a library is specific rather than ideological:

1. **Everything on the list below is a ten-line Web Animations API call.** `element.animate()`
   is in every browser this app supports, is hardware-accelerated, returns a promise, and
   composites off the main thread. GSAP, anime.js and Motion One exist to paper over an era
   that ended.
2. **The payload is already this project's largest open item** (`PROGRESS.md` item 5, 27 MiB
   uncompressed). Adding 20–70 KB of animation runtime to a page whose own framework is the
   problem is the wrong trade, and slice B just added 381 KB of fonts against that same budget.
3. **There is no network on this machine**, so a library cannot be fetched, vendored or
   verified here — and vendoring an unverified third-party script into a repository that
   deploys to a public site is not something to do casually.
4. **Two newer platform features do the hard parts a library cannot**: the View Transitions API
   animates *between two DOM states* across a route change, which no library can do, and
   scroll-driven animations run off the compositor with no script at all.

**If a library is wanted anyway**, the honest shortlist is Motion One (~5 KB, WAAPI wrapper) or
anime.js (~17 KB). Both would need to be committed under `wwwroot/js/` with their licence. I
would still start without one and add it only where the platform actually falls short.

---

## Phase 0 — the scales nothing can be consistent without — **done**

Invisible on its own; every later phase is cheaper and better for it. **Do this first.**

`PROGRESS.md` has the account. Four things a later phase needs to know:

- **The counts here were an undercount.** Measured off the file: twenty-seven spacing values,
  not twenty, and twenty font sizes, not eleven. Nine spacing rungs and seven type rungs
  replace them, at `--space-0`…`-8` and `--text-xs`…`-3xl`.
- **The scale is not a strict 4px base and the type scale is not a clean 1.2 ratio**, both
  deliberately. `--space-0` and `-2` are 2px and 6px because four of the old values sat between
  4.8px and 7.2px and a 4px-only scale doubles the tightest spacing in the app. `--text-xs` is
  pinned at 0.72rem because that is the size `--muted`'s 4.5:1 floor was *measured* at, and
  `--text-3xl` at 2.15rem because it is the masthead. Do not "tidy" either to a ratio.
- **`--shadow-3` belongs to the sticky strip and nothing else**, asserted by count. If a later
  phase wants a hover lift, that is `--shadow-2`.
- **There is no `--ease-emphasised` yet, on purpose.** Nothing wanted an overshoot; the first
  thing that does is a row arriving in a list, which is Phase 2. Add it there, and use it.

Held by the same rule as colour: `NoScreenRuleNamesARawSpacingOrTypeLength`, which refuses px
as well as rem. **Cost: as estimated. Risk: was low, and the one real hazard was self-inflicted
— a scripted rewrite emitting `-var(…)`, which is invalid CSS and drops the whole declaration
without looking broken.**

---

## Phase 1 — density and hierarchy

The largest perceived improvement per hour, and mostly deletion.

- **De-nest the panels.** The editors are bordered boxes inside bordered boxes; now that
  headings carry structure typographically, most inner borders are redundant. This is the half
  of slice B item 2 that was reached for and only half-delivered.
- **One chrome band.** Banner, step list and budget strip are three stacked horizontal bands.
  The budget is now ~40px of sticky chrome; the step list can join it and scroll away, or
  become a slim progress indicator that stays.
- **Empty states that say what to do.** "None yet." is a full stop. Each list's empty state
  should name the next action, and the tabs should show which sections are untouched.
- **A real grid on wide screens.** The shell is capped at 1100px and everything is one column;
  above ~1400px the editors and a live sheet preview fit side by side (see Phase 4).

**Cost: one slice. Risk: medium — this touches every page, and the print stylesheet corrects
screen rules by specificity, which has broken three times. Re-proof the PDF.**

---

## Phase 2 — motion that carries meaning

**The principle first, because this is where "slick" becomes "noisy".** Animation earns its
place when it expresses *causality* (this happened because you did that), *continuity* (this
is the same object, moved), or *state* (this is loading, this changed). Anything else is
decoration, and decoration on a tool people use for twenty minutes becomes an irritation by
minute three.

Three uses, in order of value:

1. **Continuity across steps — the View Transitions API.** Walking from Abilities to Powers
   currently swaps the page. `document.startViewTransition()` cross-fades and lets shared
   elements morph, so the budget strip and the step list visibly persist while the content
   changes. Blazor's router does not integrate with it; the shim is a JS helper hooked to
   `NavigationManager.LocationChanged`. Unsupported browsers simply do not transition.
2. **Feedback on change.** A Power added should visibly *land* in the list above; the budget
   figure should count rather than jump; the rail should ease. The counting number is
   `element.animate()` over a CSS custom property, or a short `requestAnimationFrame` loop.
3. **Exit animations, which Blazor makes non-trivial.** Blazor reuses DOM nodes across
   renders, so a removed row vanishes instantly. Doing this properly means `@key` on the row
   and a small interop helper that animates the node out *before* the state change removes it.
   **This is the one item here I would defer** — it is the fiddliest and the least noticed.

**Every one of these is gated on `prefers-reduced-motion`.** The tokens already collapse to
`0.01ms`; the JS must check `matchMedia('(prefers-reduced-motion: reduce)')` itself, because a
token cannot reach a script. A test should assert that it does.

**Cost: one slice for 1 and 2. Risk: medium — easy to overdo. Judge every animation by
screenshotting the app at rest; if a still frame looks worse, the animation is hiding it.**

---

## Phase 3 — the interactions that are still forms

Where the app stops feeling like a document and starts feeling like a tool.

- **A command palette.** `Ctrl-K` to jump to a step, find a Power, or add one. This is the
  single highest-leverage affordance on the list and it is mostly built: `OptionFilter.Admits`
  already does the matching, and 141 Powers is exactly the catalogue a palette is for.
- **The pips become the control.** They are `aria-hidden` decoration beside a `+`/`−` stepper;
  clicking the fifth pip should set 5d, with arrow keys and Home/End. Fewer clicks to get from
  2d to 9d, and the control finally matches what it displays.
- **Keyboard navigation in the option lists.** Arrow keys and Enter, with the filter box
  keeping focus — currently every list is mouse-only in practice.
- **Validation where the mistake is made.** The engine already answers continuously; the
  findings only surface at GM review. A Trait over the cap should say so on its own row.
- **Undo.** Three buttons on the tier page can destroy twenty minutes and are guarded by a
  confirm; a single-level undo is friendlier and less interrupting than a dialogue.

**Cost: two slices. Risk: medium-high — the palette and the pip control are new interaction
surfaces and need real keyboard and screen-reader testing, not just bUnit.**

---

## Phase 4 — the sheet as the reward, not the exit

The printed sheet is the deliverable and it appears only at the end, behind six steps. On a
wide screen it should be visible *while* building — a live preview column that updates as the
character does. This is nearly free: `SheetView` already takes a character and already renders
from the session, and there is exactly one sheet component by design.

**Cost: half a slice. Risk: low, but watch the render cost — the sheet re-renders on every
keystroke unless it is throttled.**

---

## Phase 5 — the things that are simply missing

- A skip link and landmark roles; the focus ring is already a separate measured token.
- "Saved" feedback — the character write-through is silent, so nobody knows it happened.
- A print preview that is honest about the browser's own header, which no page can suppress.

---

## What must not break

Every one of these has already cost this project a bug, and all are recorded in `CLAUDE.md`:

- **No component names a colour, a font, a radius, a duration or a raw length.** theme.css is
  the only file that names any of them, and as of Phase 0 the length half is asserted too —
  in px as well as rem.
- **One component owns each repeated class.**
- **Hero and Villain stay purely visual.** No mode field on `CharacterSheet`, and the validator
  is never told. If a phase seems to need one, the design is wrong.
- **The printed sheet is one page and is judged on paper**, through the bUnit-plus-headless-
  Chrome route — never a dev server, and never a screen screenshot.
- **The engine decides.** Nothing in `web/` computes a Hero Point, and no animation may imply a
  figure the engine has not returned — a counting animation must count *to* the engine's
  answer, never predict it.
- **The payload.** Every phase should be able to say what it added.
