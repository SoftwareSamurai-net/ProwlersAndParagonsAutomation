# The printed sheet

Read before changing the print stylesheet, `SheetView`, or the two sample characters. The printed sheet is the deliverable.

> Part of the guide set indexed by [`CLAUDE.md`](../../CLAUDE.md). Read that first; it carries the
> disciplines that apply whatever you are working on. **Open work lives in
> [`PROGRESS.md`](../../PROGRESS.md)** — this file records how things are, not what is left.

---

## The printed sheet is the deliverable

**It is modelled on the published Ultimate Edition Hero Sheet**, which is at `docs/Prowlers_&_Paragons_Ultimate_Edition_Hero_Sheet.pdf` — untracked, because `*.pdf` is gitignored repository-wide, so get your own copy from the publisher. Look at it before changing the layout.

What is reproduced is the **structure**: a masthead of three boxes, three columns (Traits / the Powers stack / the four figures), a foot of free-text boxes, every section ruled with a centred heading in a bar. What is *not* reproduced is any of the trade dress — no hex pattern, no wordmark, no colour scheme. Those are LakeSide Games'.

Two consequences of the reference being a **form** rather than a summary, both deliberate:

- **Every Ability and all twelve Talents print, bought or not**, with a rule where the number goes. A sheet that hides a Talent at 0d is a report of what the tool knows; the published one is something you can write on.
- **Alias, Team, Origin, Notes and Details have no equivalent in the engine and print as labelled blank rules.** Do not delete them for being unbacked, and do not add fields to `CharacterSheet` to fill them — a pen is the right tool for those.

`web/wwwroot/css/app.css` ends with the print stylesheet and it is load-bearing. **Judge it by the PDF, never by the screen** — computed styles cannot tell you whether a page break lands mid-entry.

- **The sheet explains every name on it, and the printed page is unchanged by that.** `SheetView.Explain`
  defaults to on, so the review step, the preview beside the editors and every replayed recording all
  draw terms. **This does not reach paper, and know which rule is doing the work.** The tip is
  `.row-tip`, and it is **its own base `display: none`** that keeps it off the page — opened only by
  `:hover` and `:focus-visible`, neither of which paper can be in. The print block never mentions it.
  (`.tip-wrap` *is* in the print block's hide list and is **not** the mechanism: that class belongs to
  `Tooltip`, and a `Term` has no such ancestor. A guard written against it passed while a reviewer
  put every description on the printed page.) The print block's own contribution is `.term-name`
  giving up `text-decoration` and `cursor`, so the word prints as a word; and the description's other
  copy — the one `aria-describedby` names, which cannot be `display: none` without leaving the
  accessibility tree — is `.sr-only`, a clipped 1px box that contributes nothing to a printed page. The default used to be off precisely to protect this page, on
  the argument that a sheet gaining forty controls would be a different document. The premise is
  right and the printed sheet gains nothing, which was true before the flip and untested:
  `PrintingASheetIsUnchangedByTheExplanations` reads the cascade and pins all three.
- **The sheet is one page and should stay one page.** The three columns are equal height and the box marked `fill` in each — Notes and Origin — absorbs the difference, so a short character still prints a full page instead of a third of one. That is a flex `flex: 1` on `.sheet-section.fill` plus `justify-content: space-between` on its rules, not a tuned line count; do not go back to counting lines.
- **The browser prints its own header, and no page can stop it.** The URL, the date and the page number across the top are the print dialogue's "Headers and footers" setting, which belongs to the person printing. The review step tells them where the switch is; that is the only lever there is. Do not add a `@page` margin box or a page counter to try — Chrome supports neither.

**The PDFs are in `docs/`, and a worktree cannot see them.** `*.pdf` is gitignored repository-wide, so both books sit in the main working directory and `docs/` inside a `.claude/worktrees/…` checkout holds only the extraction guide. `ls docs/*.pdf` from a worktree therefore reports nothing, which reads as "there is no rulebook" and is wrong — a whole slice was worked through on that assumption. Look at `<repo root>/docs/`, not the worktree's.

**To read the rulebook itself, extract its text with PdfPig.** There is no `pdftoppm`, and the `Read` tool cannot open a PDF without it — so a scratch console project referencing `PdfPig` is the way in. Group each page's words by rounded baseline and sort descending to recover lines; `page.Text` unbroken is fine for searching.

**This said "and no Python on this machine" in two places, and that stopped being true on 2026-08-26** — the owner installed it that day, so the sentence was accurate when written rather than careless. It is a fact about the machine, not about the repository, which is exactly the kind that goes stale without anything touching the tree. Measured: `python`, `python3` and `py` all answer, at **3.14.7**, with `pip` 26.2.1.

**The conclusion above is unchanged, and only its reason moved.** No PDF library is installed — `pypdf`, `PyPDF2`, `fitz`, `pdfplumber`, `pdfminer` and `PIL` all fail to import — so PdfPig via a scratch C# project is still the route into a PDF. What the retired sentence should no longer be read as is a reason to avoid Python for the many jobs here that are not PDFs. `node` is present too, and is what the accounts suite runs on.

**The offset is a constant +3** — printed 15 = PDF 18, printed 127 = PDF 130 — and Ch.8's twenty Heroes are PDF pp.130–149. Do not try to read it off a footer casually: **each page prints its number twice, interleaved**, so PDF 18 extracts as `151 5` (two 15s) and PDF 148 as `14154 5` (two 145s). An earlier note here recorded a variable offset on the strength of misreading one of those, and was ten pages out in the chapter it was offered for. Decode a footer carefully, or cross-check against the table of contents on PDF 4, which is in printed numbers.

Chapter marginalia are extractable too (`2 2` beside `chapter`), which is worth checking before citing one: the Random Hero Generator on printed p.63–64 is still **Chapter 2**, not Chapter 3.

How to actually look at a printed sheet, since the browser pane cannot screenshot and headless Chrome cannot wait for Blazor to boot: **render `SheetView` through bUnit and write `.Markup` into a static page** against the real `theme.css` and `app.css`, then print that with `chrome --headless --print-to-pdf`. A throwaway `[Theory]` in the bUnit project taking the output directory from an environment variable does it in one `dotnet test` run — **no dev server**, which is the point: starting one raises an approval dialogue that blocks unattended work. (An earlier note here said to capture `document.querySelector('.sheet').outerHTML` from the running app. That works and needs a server; this does not.)

The same harness screenshots the **screen** design — `--screenshot` instead of `--print-to-pdf`, with the real `theme.css` and `app.css` linked and `data-mode` set. **Pass `--virtual-time-budget=3000` or you will proof a lie.** `.panel` carries `animation: rise var(--enter) both`, which starts at `opacity: 0`, and a bare `--screenshot` fires before it finishes: every panel comes out washed and everything inside one reads as muted text on a faded ground. That was diagnosed as a palette fault and half-fixed as one before the second screenshot showed the label was bright the whole time.

Two things Chrome will waste your time on: `--print-to-pdf` needs an **absolute Windows path** or it fails with "Access is denied", and `--no-pdf-header-footer` is what removes the URL-and-date band so you are judging the sheet rather than the print dialogue. Set `data-mode` on `<html>` in the harness or you will proof one palette twice.

Rasterising the result needs a PDF library, and none is installed for either runtime (no `pdftoppm`, and Python is present but carries no PDF module — see above); Docnet.Core plus ImageSharp 3.1.x in a scratch console project works. Pin ImageSharp below 4.0, which refuses to build without a licence key. Repeat the sheet three times in the harness to force breaks through every kind of block.

- **The rule is white paper and readable ink, not "everything black".** A third palette at the bottom of `theme.css` handles print: a shared block fixes the surfaces white and the body text near-black, then each mode restates its own `--heading`, `--rule`, `--accent` and `--muted` as ink. So a Hero sheet prints navy and a Villain crimson, and neither prints the near-black surface that made a Villain sheet a full-bleed ink dump. Restate *every* token the two screen palettes declare — one left out keeps its screen value through the cascade, which is exactly how that happened. Two tests: one resolves the cascade per mode and checks luminance both ways, one checks nothing is missed.
- **Colour on paper is ink, never fill.** The heading bars use `--accent-soft`, a tint, and they are the largest run of colour on the page at about 6mm. `--primary` stays white in print because it is a *fill* token — the sheet banner used it, and filling a banner strip solid costs a cartridge a character. Backgrounds also need `print-color-adjust: exact`, or browsers drop them and the sheet prints half-styled.
- **Hero Point costs are set apart from ranks** (`.hp`): smaller, lighter, letter-spaced, muted. A rank is what you roll; a cost is bookkeeping consulted only when rebuilding the character, and in the same face the sheet read as a receipt.
- **A Trait with no ranks bought prints `0d`, not a blank rule.** 0d is a fact about the character. The blank rules are only for Alias, Team, Origin, Notes and Details — the fields the engine genuinely has no answer for.
- **A print rule that corrects a screen rule must match its specificity.** The print block is one `@media print` at the bottom of the same file, so it does not win by being later — `@media` adds nothing to specificity. `.ruled { gap: 4mm }` (0,1,0) silently lost to `.sheet-section.fill > .ruled { gap: 0 }` (0,3,0), which left Notes and Origin — the two boxes that exist to be written on — with their lines about 2mm apart. The same trap put gear flush right: `td[colspan]` and `td:last-child` weigh the same, so the fix held on source order alone until it was written `tr > td[colspan]`.
- **`break-inside: avoid` on `.sheet-section`, except the Powers groups *and* the `fill` boxes.** `fill` stretches to the height of the tallest column and the Powers column is unbounded, so it is the second thing on the page that can exceed a page — and the failure is the same one: Chrome pushes the whole box to the next page and abandons the rest of the current one. Both opt out, and a test names both.
- **`break-inside: avoid` on `.sheet-section`, except the Powers groups.** Every small box asks not to be broken, which is what stops a four-line Gear box straddling a page. A Powers group carries `.powers` and opts back out: Chrome honours the request by pushing the whole box to the next page first, and on a fifteen-Power character that left **two thirds of page one blank**. The one box that can exceed a page is the one that must be allowed to break.
- **Set `align-items: start` on any grid of ruled boxes.** The default `stretch` makes a one-line Perks box as tall as the Flaws box beside it — on paper, a ruled void that can run a whole page.
- **`--focus` is a separate token from `--accent`.** A focus ring is a non-text indicator and WCAG 1.4.11 wants 3:1; Hero `--accent` is 1.8:1 on `--surface`, which is a ring nobody can see. `--muted` is held to 4.5:1 rather than 3:1 because it carries the explanatory prose at 0.72–0.82rem. Both were measured, not eyeballed; re-measure if you change them.
- **A `position: fixed` running footer does not work.** Chrome's print output renders it once, at the top of page two, over the content. The character's name repeats across pages via the **document title**, which the browser prints in its own header — that is why `Review.razor`'s `<PageTitle>` leads with the name. A test asserts `position: fixed` never returns to the print block.
- `h1 { display: none }` in print: the page heading is the tool's, not the sheet's.


## The two sample characters

`SampleCharacters.Hero()` and `.Villain()` return finished Standard-tier sheets, offered on **`/portfolio`** so a sheet can be previewed without building one. They fill every section a printed sheet has, which an empty sheet does not.

**This entry said "on the tier page" for a while after they stopped being there, and then said "on the portfolio" after that moved too.** They are at **`/admin/portfolio`** now, behind the account pages. The reason for the first move is worth keeping and is the same one: a demonstration is not a step in making your own character, and somebody who came to build one had to walk past them. `AreaTests.TheSamplesAreBehindTheAccountAndNotOnTheTierPage` pins both halves.

- **They are this project's own characters.** The published Ch.8 Heroes stay in the test suite, where they verify the engine against printed numbers. **The reason has changed and the practice has not:** shipping them used to be barred as redistributing the authors' content, and the owner has since lifted that — the book's text and the published characters may be served to an account. These two are still the ones the app offers, because they were written for this tool and fill every section a sheet has, which is what a preview is for.
- **`SampleCharacterTests` holds them to the rules** — legal, inside budget, fully priceable, every section filled, at least one Source heading, and both exports rendering. Writing them caught three real mistakes: ranks bought on rankless Powers (`invisibility`, `lightning_reflexes` are `max_rank: 0`), and Danger Sense and Resistance pushed over the Trait Cap because both take a **baseline equal to** an Ability rather than half it. Check `rank_type` and `prerequisite` before adding ranks to a sample.
- The Villain deliberately leaves one Power without a Source, so the sheet shows the plain `POWERS` fallback heading and the review step shows a warning. Both are things a preview should exercise; it is not an oversight.

