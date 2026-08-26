using ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The corpus generator's reading order, driven against made-up pages.</b>
///
/// <para>Everything else about <c>data/rulebook/</c> is asserted as data, which is right — the
/// corpus is committed and the publisher's PDF is not on a build agent. But <see cref="PageReader"/>
/// had no tests of its own at all: an adversarial audit disabled its gutter-crossing check and
/// reproduced the historical corpus corruption byte for byte with the whole suite green, because
/// nothing regenerates the corpus from the PDF in CI, so the committed corpus was only ever being
/// compared against itself. <see cref="ColumnLayout"/> already solved the same problem for the
/// column geometry underneath this by being driven against made-up spans; <see cref="RawLetter"/>
/// does the same job one level up, standing in for a PdfPig <c>Letter</c> that a test cannot build
/// (the PdfPig <c>Page</c> it lives on has no public constructor at all).</para>
///
/// <para>Each case below is a page shape that broke, or could break, a real extraction, described
/// in the terms <see cref="PageReader"/> actually sees: one glyph per character, with a position,
/// a font, a size, and whether it runs horizontally.</para>
/// </summary>
public sealed class PageReaderTests
{
    private const double PageWidth = 612;
    private const string BodyFont = "AAAAAA+PublicSans-Regular";
    private const double BodySize = 9;

    /// <summary>
    /// One glyph per character of <paramref name="text"/>, including a real glyph for every
    /// space, all spaced identically — letter-to-letter and word-to-word alike. Nothing about the
    /// geometry here can be used to tell a word boundary from a letter gap; only the presence of a
    /// whitespace-valued glyph can, which is the property <c>BuildWords</c> is meant to rely on.
    /// </summary>
    private static List<RawLetter> Glyphs(
        string text, double left, double baseline,
        string font = BodyFont, double size = BodySize,
        double charWidth = 6, double gap = 1, double? spaceWidth = null)
    {
        var glyphs = new List<RawLetter>();
        var x = left;
        foreach (var ch in text)
        {
            var w = char.IsWhiteSpace(ch) ? spaceWidth ?? charWidth * 0.5 : charWidth;
            glyphs.Add(new RawLetter(ch.ToString(), x, x + w, baseline, font, size, true));
            x += w + gap;
        }
        return glyphs;
    }

    /// <summary>
    /// One line of an ordinary body page: <paramref name="firstWord"/>, then enough "XXX" filler
    /// words — each a separate real word, the way any two words on a page are — to occupy the
    /// column from <paramref name="left"/> up to (but not past) <paramref name="right"/>. Filling
    /// the whole column, rather than leaving one short word with nothing beside it, is what a real
    /// two-column page's lines look like and what the gutter-finder needs to see a genuine gap.
    /// </summary>
    private static List<RawLetter> FilledLine(string firstWord, double left, double right, double baseline)
    {
        var glyphs = new List<RawLetter>();
        var x = left;

        void Place(char ch, double width)
        {
            glyphs.Add(new RawLetter(ch.ToString(), x, x + width, baseline, BodyFont, BodySize, true));
            x += width + 1;
        }

        foreach (var ch in firstWord) Place(ch, 6);
        while (x + 25 <= right)
        {
            Place(' ', 3);
            foreach (var ch in "XXX") Place(ch, 6);
        }

        return glyphs;
    }

    /// <summary>Eight rows of an ordinary two-column body page, with a distinct first word per column per row.</summary>
    private static List<RawLetter> TwoColumnBody(string leftPrefix, string rightPrefix, int rows = 8, double startBaseline = 700)
    {
        var glyphs = new List<RawLetter>();
        for (var i = 0; i < rows; i++)
        {
            var baseline = startBaseline - i * 14;
            glyphs.AddRange(FilledLine($"{leftPrefix}{i}", 45, 270, baseline));
            glyphs.AddRange(FilledLine($"{rightPrefix}{i}", 340, 567, baseline));
        }
        return glyphs;
    }

    // ------------------------------------------------------------------------------------------
    // Column assignment: a two-column page must be read column by column, not row by row.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The defect this whole class exists to catch: reading a two-column page by baseline alone
    /// interleaves the columns. Deliberately breaking <c>CrossesGutter</c> — the audit's own
    /// mutation — reproduces exactly this on this fixture; see the mutation table in
    /// <c>docs/notes/s5-extractor.md</c>.
    /// </summary>
    [Fact]
    public void TwoColumnBodyIsReadColumnByColumnNotRowByRow()
    {
        var glyphs = TwoColumnBody("L", "R");

        var lines = new PageReader().Read(glyphs, PageWidth);

        // Positive control: both columns of every row actually reached the output.
        Assert.Equal(16, lines.Count);

        for (var i = 0; i < 8; i++)
            Assert.StartsWith($"L{i} ", lines[i].Text, StringComparison.Ordinal);
        for (var i = 0; i < 8; i++)
            Assert.StartsWith($"R{i} ", lines[8 + i].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Printed p.52: two headings — "OVERKILL" and "PHASE SHIFT" — set side by side on one
    /// baseline, one entry per column. Reading by baseline alone (or by "some word sits either
    /// side of the gutter") glues them into one heading, "OVERKILL PHASE SHIFT", which reads as a
    /// single plausible-looking entry and silently merges two Powers into one.
    /// </summary>
    [Fact]
    public void TwoFacingHeadingsSharingABaselineStayTwoEntries()
    {
        var glyphs = TwoColumnBody("L", "R");
        glyphs.AddRange(Glyphs("OVERKILL", left: 45, baseline: 610));
        glyphs.AddRange(Glyphs("PHASE SHIFT", left: 340, baseline: 610));

        var lines = new PageReader().Read(glyphs, PageWidth);

        // Positive control: the body text around the headings still reads correctly.
        Assert.Contains(lines, l => l.Text.StartsWith("L0 XXX", StringComparison.Ordinal));

        Assert.Contains(lines, l => l.Text == "OVERKILL");
        Assert.Contains(lines, l => l.Text == "PHASE SHIFT");
        Assert.DoesNotContain(lines, l => l.Text.Contains("OVERKILL PHASE", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every chapter opening in the book is set full width. Splitting a full-width line at a
    /// fixed midpoint — the previous extractor's approach — cuts the sentence in two and files the
    /// halves in different blocks; this asserts the whole sentence survives as one line.
    /// </summary>
    [Fact]
    public void AGenuinelyFullWidthLineIsKeptWhole()
    {
        const string Opening =
            "THIS OPENING RUNS ACROSS THE WHOLE WIDTH OF THE PRINTED PAGE FROM ONE MARGIN TO THE OTHER";

        var glyphs = Glyphs(Opening, left: 45, baseline: 700);
        glyphs.AddRange(TwoColumnBody("L", "R", startBaseline: 680));

        var lines = new PageReader().Read(glyphs, PageWidth);

        // Positive control: the two-column body beneath the opening still split normally.
        Assert.Contains(lines, l => l.Text.StartsWith("L0 XXX", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Text.StartsWith("R0 XXX", StringComparison.Ordinal));

        Assert.Equal(Opening, lines[0].Text);
    }

    // ------------------------------------------------------------------------------------------
    // Word splitting: on real space glyphs, and on a wide gap where there is no space glyph.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// In the condensed display face a word space is barely wider than the gap between two
    /// letters. Guessing word boundaries from gap size merged "FORCE FIELD" into "FORCEFIELD";
    /// this fixture makes the gap across the space (0.8pt) <em>narrower</em> than the gap-based
    /// break threshold (4.05pt at this size), so only recognising the space glyph's own value —
    /// not its width or the size of the surrounding gaps — can produce the right answer.
    /// </summary>
    [Fact]
    public void WordsAreSplitOnRealSpaceGlyphsEvenWhenTheGapIsNarrow()
    {
        var glyphs = Glyphs("FORCE FIELD", left: 45, baseline: 700, charWidth: 6, gap: 0.3, spaceWidth: 0.2);

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: one line, both words read
        Assert.Equal("FORCE FIELD", lines[0].Text);
    }

    /// <summary>
    /// Two facing headings are two separate text runs with no space glyph between them at all —
    /// only a wide gap. This is the word-level half of the p.52 bug: without a gap-based break,
    /// "OVERKILL" and "PHASE" fuse into "OVERKILLPHASE" even though nothing whitespace-valued sits
    /// between them.
    /// </summary>
    [Fact]
    public void AWideGapSplitsWordsEvenWithNoSpaceGlyphBetweenThem()
    {
        var glyphs = Glyphs("OVERKILL", left: 45, baseline: 700);
        glyphs.AddRange(Glyphs("PHASE", left: 140, baseline: 700));

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: one line, both words read
        Assert.Equal("OVERKILL PHASE", lines[0].Text);
    }

    // ------------------------------------------------------------------------------------------
    // The watermark: dropped by font, four separate tokens, never by matching the text itself.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// A filter written against the whole watermark phrase matches none of its four separately
    /// stamped tokens. Each is asserted dropped on its own — a version checking only the surname,
    /// the order number and the literal "(Order #" was defeated by injecting a fourth token that
    /// didn't match any of those three.
    /// </summary>
    [Fact]
    public void EachOfTheFourWatermarkTokensIsDroppedOnItsOwn()
    {
        var glyphs = Glyphs("THE HERO STANDS READY", left: 45, baseline: 700);

        var x = 45.0;
        foreach (var token in new[] { "Purchaser", "Smith", "Order", "12345" })
        {
            var run = Glyphs(token, left: x, baseline: 690, font: "ZZZZZZ+Helvetica", size: 6);
            glyphs.AddRange(run);
            x = run[^1].Right + 30;
        }

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: the watermark's own line vanishes entirely
        Assert.Equal("THE HERO STANDS READY", lines[0].Text);

        Assert.DoesNotContain("Purchaser", lines[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Smith", lines[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Order", lines[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", lines[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same word, once in the body face at body size and once in the watermark's 6pt
    /// Helvetica — proving the filter is a font-and-size decision rather than a word-content one.
    /// A content-based check could not tell these apart; a font-based one drops only the second.
    /// </summary>
    [Fact]
    public void TheWatermarkIsDroppedByFontNotByContent()
    {
        var glyphs = Glyphs("ORDER", left: 45, baseline: 700, font: "AAAAAA+PublicSans-Regular", size: 9);
        glyphs.AddRange(Glyphs("ORDER", left: 400, baseline: 700, font: "ZZZZZZ+Helvetica", size: 6));

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: the surviving copy still reads
        Assert.Equal("ORDER", lines[0].Text);
    }

    // ------------------------------------------------------------------------------------------
    // Rotated text and the running foot are furniture, never prose.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The chapter title runs up the outer margin, one letter at a time, and extracts reversed
    /// ("retpahc") if it is ever read as a line. This fixture places it on its own baseline, so if
    /// the orientation filter is ever lost the reversed word appears as a second line.
    /// </summary>
    [Fact]
    public void RotatedTextIsDroppedAsFurniture()
    {
        var glyphs = Glyphs("THE HERO STANDS READY", left: 45, baseline: 700);

        var x = 500.0;
        foreach (var ch in "RETPAHC")
        {
            glyphs.Add(new RawLetter(ch.ToString(), x, x + 6, 680, "AAAAAA+LeagueGothic-Regular", 10, false));
            x += 7;
        }

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: the horizontal content still reads
        Assert.Equal("THE HERO STANDS READY", lines[0].Text);
    }

    /// <summary>
    /// The display faces are faked bold by drawing the text twice a fraction of a point apart, so
    /// a page number reads as "2299" — but every instance of it lives in the running foot, below
    /// the furniture line. Dropping the foot is what removes the doubling; there is no separate
    /// de-duplicator to fail.
    /// </summary>
    [Fact]
    public void TheRunningFootIsDroppedIncludingDoubledGlyphs()
    {
        var glyphs = Glyphs("THE HERO STANDS READY", left: 45, baseline: 700);
        glyphs.AddRange(Glyphs("29", left: 45, baseline: 10, font: "AAAAAA+CCBiffBamBoom", size: 12));
        glyphs.AddRange(Glyphs("29", left: 45.3, baseline: 10.1, font: "AAAAAA+CCBiffBamBoom", size: 12));

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: body content survives the foot being dropped
        Assert.Equal("THE HERO STANDS READY", lines[0].Text);
    }

    /// <summary>The furniture line is a boundary (<c>&gt;=</c>), not a fuzzy judgement call.</summary>
    [Fact]
    public void FurnitureTopIsAHardBoundary()
    {
        var glyphs = Glyphs("KEPT", left: 45, baseline: 30);
        glyphs.AddRange(Glyphs("DROPPED", left: 45, baseline: 29.9));

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control: the surviving line reads
        Assert.Equal("KEPT", lines[0].Text);
    }

    // ------------------------------------------------------------------------------------------
    // Headings are detected by typeface, and require a genuine majority.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ALineSetInADisplayFaceIsMarkedAsAHeading()
    {
        var glyphs = Glyphs("POWERS", left: 45, baseline: 700, font: "AAAAAA+LeagueGothic-Regular", size: 24);

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control
        Assert.Equal("POWERS", lines[0].Text);
        Assert.True(lines[0].IsHeading);
    }

    [Fact]
    public void ALineSetInTheBodyFaceIsNotMarkedAsAHeading()
    {
        var glyphs = Glyphs("Powers act on Powers.", left: 45, baseline: 700);

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control
        Assert.False(lines[0].IsHeading);
    }

    /// <summary>
    /// Exactly half a line's glyphs in a display face must not read as a heading — the rule is a
    /// strict majority (<c>headingGlyphs * 2 &gt; totalGlyphs</c>), not "any at all" or "at least half".
    /// </summary>
    [Fact]
    public void AnExactTieOfHeadingGlyphsIsNotAMajority()
    {
        var glyphs = new List<RawLetter>();
        var x = 45.0;
        foreach (var ch in "POWE")
        {
            glyphs.Add(new RawLetter(ch.ToString(), x, x + 6, 700, "AAAAAA+LeagueGothic-Regular", 12, true));
            x += 7;
        }
        foreach (var ch in "RSAB")
        {
            glyphs.Add(new RawLetter(ch.ToString(), x, x + 6, 700, "AAAAAA+PublicSans-Regular", 9, true));
            x += 7;
        }

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control
        Assert.Equal("POWERSAB", lines[0].Text);
        Assert.False(lines[0].IsHeading, "an exact tie between the two faces must not count as a heading majority");
    }

    /// <summary>The display-face list is matched by prefix after stripping the PDF subset tag, not by substring.</summary>
    [Fact]
    public void AFontNameThatOnlyContainsTheFamilyIsNotTreatedAsAHeadingFace()
    {
        var glyphs = Glyphs("TEXT", left: 45, baseline: 700, font: "AAAAAA+NotLeagueGothic", size: 12);

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Single(lines); // positive control
        Assert.False(lines[0].IsHeading);
    }

    // ------------------------------------------------------------------------------------------
    // A single-column page — a full-page table, the credits — is read whole, not split down the middle.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ASingleColumnPageStaysInBaselineOrder()
    {
        var glyphs = new List<RawLetter>();
        var words = new[] { "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX" };
        for (var i = 0; i < words.Length; i++)
            glyphs.AddRange(FilledLine(words[i], 45, 567, 700 - i * 14));

        var lines = new PageReader().Read(glyphs, PageWidth);

        Assert.Equal(words.Length, lines.Count); // positive control: every row survived as its own line
        for (var i = 0; i < words.Length; i++)
            Assert.StartsWith(words[i] + " ", lines[i].Text, StringComparison.Ordinal);
    }
}
