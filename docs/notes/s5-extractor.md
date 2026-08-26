# PageReader unit tests (slice s5-extractor)

## The problem

`tools/RulebookExtractor/PageReader.cs` had no tests of its own. `ColumnLayout` — the pure
geometry underneath it — was already unit-tested against made-up pages, but `PageReader` itself,
which decides reading order, watermark/furniture removal, word splitting and heading detection,
was only ever exercised by regenerating `data/rulebook/` from the publisher's PDF and re-running
`RulebookCorpusTests` against the committed output. Nothing in CI does that regeneration, so the
committed corpus was being compared against itself. An adversarial audit disabled `CrossesGutter`
(made it always return `true`) and reproduced the historical two-column interleaving corruption
byte for byte, with the whole suite green.

## The seam

`PageReader.Read(Page)` took a `UglyToad.PdfPig.Content.Page` directly. `Page` has no public
constructor — its only constructor is `internal`/private and takes a `DictionaryToken`, a
`MediaBox`, a `CropBox`, a `PageRotationDegrees`, a `PageContent`, an `AnnotationProvider` and an
`IPdfTokenScanner`, none of which are things a test can build. (Verified by reflecting over
`UglyToad.PdfPig.dll` 0.1.15 rather than assumed — `Letter` does have a public constructor, but
`Page` does not, which is the type actually on `PageReader.Read`'s signature.)

So the reading-order logic is now split from the PdfPig adapter:

- **`RawLetter`** (new, `internal readonly record struct`, top-level in `PageReader.cs`) is one
  glyph reduced to exactly what the logic looks at: `Value`, `Left`, `Right`, `Baseline`,
  `FontName`, `PointSize`, `IsHorizontal`. It plays the same role for `PageReader` that `Span`
  already plays for `ColumnLayout`.
- **`PageReader.Read(Page page)`** is now a one-line adapter: it maps `page.Letters` onto
  `RawLetter` and calls the internal overload with `page.Width`. Its behaviour is unchanged.
- **`PageReader.Read(IReadOnlyList<RawLetter> letters, double pageWidth)`** (new, `internal`) is
  the actual logic — orientation filter, furniture-band filter, watermark filter, line grouping,
  word building, gutter-aware ordering — moved verbatim from the old `Read(Page)`, with PdfPig
  member accesses (`l.BoundingBox.Left`, `l.StartBaseLine.Y`, `l.TextOrientation`, …) rewritten to
  the flat `RawLetter` fields. No logic changed; only the type it operates on.

`RawLetter` is `internal`, and `RulebookExtractor.csproj` already declares
`InternalsVisibleTo="ProwlersAndParagonsAutomation.Tests"` (for `ColumnLayout`/`Span`), so no new
project wiring was needed — the test project could already see it.

The one incidental fix: `l.FontName` on the real `Letter` type is nullable, and the adapter now
coalesces it to `""` (`CS8604` under the CI build's warnings-as-errors).

## Behaviours covered

Fifteen facts in `tests/ProwlersAndParagonsAutomation.Tests/PageReaderTests.cs`, each a page shape
that broke (or could break) a real extraction, described in `RawLetter` terms:

1. `TwoColumnBodyIsReadColumnByColumnNotRowByRow` — an ordinary two-column body page is read left
   column then right column, not row by row.
2. `TwoFacingHeadingsSharingABaselineStayTwoEntries` — the printed-p.52 case: "OVERKILL" (left)
   and "PHASE SHIFT" (right) on one baseline stay two entries, never fuse into one heading.
3. `AGenuinelyFullWidthLineIsKeptWhole` — a chapter-opening-style sentence spanning the page
   survives as one line, with a two-column body below it still splitting normally.
4. `WordsAreSplitOnRealSpaceGlyphsEvenWhenTheGapIsNarrow` — "FORCE FIELD" stays two words even
   when every gap on the line (0.8pt) is narrower than the gap-based break threshold (4.05pt), so
   only the space glyph's own value can be doing the splitting.
5. `AWideGapSplitsWordsEvenWithNoSpaceGlyphBetweenThem` — "OVERKILL"+"PHASE" with no space glyph
   between them still split, purely from gap size — the word-level half of the p.52 bug.
6. `EachOfTheFourWatermarkTokensIsDroppedOnItsOwn` — four separately-stamped watermark tokens
   (not one contiguous phrase) are each dropped.
7. `TheWatermarkIsDroppedByFontNotByContent` — the same word "ORDER" once in the body face and
   once in the watermark's 6pt Helvetica: only the second is dropped, proving it's a font
   decision, not a text-content one.
8. `RotatedTextIsDroppedAsFurniture` — vertical glyphs never reach the output (would read as
   "RETPAHC" if they did).
9. `TheRunningFootIsDroppedIncludingDoubledGlyphs` — a faked-bold doubled "29" in the foot band
   (below `FurnitureTop`) is dropped entirely, not de-duplicated separately.
10. `FurnitureTopIsAHardBoundary` — the furniture line is `>=`, a hard boundary, not a fuzzy one.
11. `ALineSetInADisplayFaceIsMarkedAsAHeading` / `ALineSetInTheBodyFaceIsNotMarkedAsAHeading` —
    the basic typeface classification.
12. `AnExactTieOfHeadingGlyphsIsNotAMajority` — an exact 50/50 split between the display face and
    the body face must not read as a heading; the rule is a strict majority.
13. `AFontNameThatOnlyContainsTheFamilyIsNotTreatedAsAHeadingFace` — `HeadingFamilies` matching is
    by prefix (after stripping the PDF subset tag), not by substring.
14. `ASingleColumnPageStaysInBaselineOrder` — a full-page single-column layout (a table, the
    credits) reads top to bottom, unsplit.

**Not covered, and why:** the task's list also named "a heading with no body qualifying the
headings beneath it by point size" (the Ch.8-names bug). That chaining/qualifying logic —
`PopTo`, the `outline` stack — lives entirely in `tools/RulebookExtractor/Program.cs`, not in
`PageReader`. `PageReader.Line.IsHeading` only answers "is this line set in a display face"; it
carries no notion of an outline or of one heading qualifying another. Covering that behaviour
would mean giving `Program.cs` the same kind of seam this slice gave `PageReader`, which is a
larger, separate piece of surgery the task scoped out ("do not restructure more than the seam
needs"). Flagging as a follow-up rather than folding it in here.

## Mutation table

Every mutation below: committed test file first, mutate `PageReader.cs`, run
`dotnet test tests/ProwlersAndParagonsAutomation.Tests --filter FullyQualifiedName~PageReaderTests`,
record the failure, `git checkout -- tools/RulebookExtractor/PageReader.cs` (safe — the file was
always committed clean before each mutation), re-run green.

| # | Mutation | Result |
|---|---|---|
| 1 | `CrossesGutter` hardcoded to `return true;` (the audit's own mutation) | **Red.** 3 tests failed. The two-column page collapsed to 8 lines instead of 16, each one row's *both* columns glued together (`"L0 XXX … R0 XXX …"`) — the historical interleaving-by-baseline corruption, reproduced exactly. |
| 2 | Full-width threshold `measure * 0.75` → `measure * 50` (no line can ever qualify as full width) | **Red.** The full-width sentence got cut mid-word: `"…ACROSS THE WHOLE WIDTH OF"` instead of the whole sentence — the old fixed-midpoint-split defect. |
| 3a | Space glyph no longer calls `Close()` (just `continue`) — first attempt, against the *original* fixture | **Survived (green).** Found to be semantically null: the fixture's word-to-word gap (1+3+1=5pt) happened to exceed the gap-based threshold (4.05pt) anyway, so the *other* mechanism silently covered for the disabled one. Fixture tightened (0.3+0.2+0.3=0.8pt, well under 4.05pt) so only the space-glyph-value path can pass. Committed the fixture fix separately (`6aa19aa`) before re-testing. |
| 3b | Same mutation, against the tightened fixture | **Red.** `"FORCE FIELD"` → `"FORCEFIELD"` — the exact historical corpus bug. |
| 4 | Gap-based `Close()` condition wrapped in `false &&` (disabled) | **Red.** 4 tests failed, including two column-layout tests: without gap-based splitting, `BuildWords` fuses words across the gutter into one oversized "word", which cascades into the column split. `"OVERKILL PHASE"` → `"OVERKILLPHASE"` directly, plus knock-on breakage of the two-column and full-width tests. |
| 5 | `IsWatermark` hardcoded to `return false;` | **Red.** Both watermark tests failed; the four injected tokens (`"Purchaser Smith Order 12345"`) leaked straight into the line text. |
| 6 | Orientation filter `.Where(l => l.IsHorizontal)` replaced with `.Where(l => true)` | **Red.** `RotatedTextIsDroppedAsFurniture` failed — the rotated glyphs read as a second line, `"RETPAHC"`, exactly the corruption named in `CLAUDE.md`. |
| 7 | `FurnitureTop` constant `30` → `0` | **Red.** 2 tests failed; the doubled "29"/"29" glyphs in the foot band surfaced as `"2299"` — the faked-bold doubling bug, byte for byte. |
| 8 | Heading-majority check `headingGlyphs * 2 > totalGlyphs` → `>=` (tie counts as heading) | **Red.** `AnExactTieOfHeadingGlyphsIsNotAMajority` failed on an exact 4/4 split. |
| 9 | `HeadingFamilies` match `StartsWith` → `Contains` | **Red.** `"NotLeagueGothic"` was wrongly classified as a heading face. |
| 10 | Single-column branch (`gutterRight <= gutterLeft`) changed to drop the line (`continue;`) instead of emitting it | **Red.** 12 of 15 tests failed — every fixture with fewer than 6 total lines routes through this branch (`ColumnLayout.FindGutter` refuses to guess under 6 lines), so it is exercised far more than just the dedicated single-column test. A second candidate mutation (loosening the branch condition from `<=` to `<`) was reasoned through algebraically rather than run: for a genuine `(0,0)` gutter it is provably a no-op, because `CrossesGutter`'s own first check (`!words.Any(w => w.Right <= centre)`) already returns `false` for every line when `centre == 0`, so the general two-column path degenerates to the same single "right-bucket" ordering the dedicated branch produces. Recorded here as a real, checked finding rather than silently skipped. |

Every mutation that was actually run went red; the one survivor (3a) was diagnosed, the fixture
was fixed, and the corrected version (3b) was re-run and confirmed red before restoring.

## Test count

`tests/ProwlersAndParagonsAutomation.Tests`: 3734 → 3749 (+15).
`tests/ProwlersAndParagons.Web.Tests`: unchanged at 482.
Both suites green under `dotnet test --configuration Release -p:ContinuousIntegrationBuild=true`;
`Catastrophic` grep clean.
