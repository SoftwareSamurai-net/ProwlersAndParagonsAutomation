using ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><see cref="ParagraphJoiner"/>, driven against made-up passages.</b>
///
/// <para>The corpus used to run every entry together as one flat run — LUCK's description arrived
/// as a single 120-word line — because nothing kept the page's own vertical spacing once the
/// words were joined into a paragraph. <see cref="PageReader.Line.LeadingGap"/> is where that
/// spacing survives; this is where it gets turned into a decision. Real corpus coverage of the
/// decision is in <c>RulebookCorpusTests</c> (<c>LucksDescriptionKeepsItsThreePrintedParagraphs</c>
/// and the guard beside it); these cases pin the rule itself against passages built by hand,
/// which the committed corpus cannot show for a shape it happens not to contain — the same reason
/// <see cref="PageReaderTests"/> and <see cref="ColumnLayoutTests"/> exist.</para>
/// </summary>
public sealed class ParagraphJoinerTests
{
    private static PageReader.Line Line(string text, double? gap) =>
        new(text, Baseline: 0, Left: 0, Right: 0, IsHeading: false, Size: 9, LeadingGap: gap);

    /// <summary>
    /// The shape LUCK actually has on the page: three ordinary-leaded lines, then a line set off
    /// with twice that leading — the way a printed paragraph break looks in the measured
    /// distribution (PROGRESS.md item 1c). Positive control first: splitting has to have happened
    /// at all before asking whether it happened in the right place.
    /// </summary>
    [Fact]
    public void ALineWithMoreThanOneAndAHalfTimesTheNormalGapStartsAParagraph()
    {
        var lines = new List<PageReader.Line>
        {
            Line("You are incredibly lucky.", null),
            Line("You gain a number of Luck dice.", 12),
            Line("You can spend them freely.", 12),
            Line("PRO Control (+4): alters probability.", 24),
        };

        var text = ParagraphJoiner.Join(lines);

        Assert.Contains('\n', text); // positive control: a break was actually inserted
        Assert.Equal(
            "You are incredibly lucky. You gain a number of Luck dice. You can spend them freely.\n"
            + "PRO Control (+4): alters probability.",
            text);
    }

    /// <summary>
    /// <b>Over-splitting guard.</b> A passage whose line-to-line leading merely wobbles between
    /// two close values — measured on the real book at 8.5pt body type, where it sits at 10pt on
    /// most lines and 12pt on some with nothing meant by the difference — must not read the larger
    /// of the two as a paragraph start. 12 / 10 = 1.2, short of <see cref="ParagraphJoiner.Multiplier"/>.
    /// </summary>
    [Fact]
    public void OrdinaryWobbleInTheLeadingIsNotMistakenForAParagraphStart()
    {
        var lines = new List<PageReader.Line>
        {
            Line("Tamara is a bright, funny,", null),
            Line("and optimistic young woman,", 10),
            Line("maybe because she has been", 12),
            Line("through a lot in her life.", 10),
        };

        var text = ParagraphJoiner.Join(lines);

        Assert.DoesNotContain('\n', text);
        Assert.Equal(
            "Tamara is a bright, funny, and optimistic young woman, maybe because she has been "
            + "through a lot in her life.",
            text);
    }

    /// <summary>
    /// <b>The threshold is local to the passage, not a book-wide constant.</b> The very same 12pt
    /// gap is a real paragraph start in a 9pt-normal-12 passage and ordinary noise in an
    /// 8.5pt-normal-10 one — this is the same input value read two different ways depending on
    /// what else is in its own passage, which a single fixed point threshold could not do.
    /// </summary>
    [Fact]
    public void TheSameAbsoluteGapReadsDifferentlyInDifferentPassages()
    {
        var tighterPassage = new List<PageReader.Line>
        {
            Line("A short passage.", null),
            Line("Set at 8.5pt, normal-10.", 10),
            Line("This 12pt gap is just noise.", 12),
        };
        Assert.DoesNotContain('\n', ParagraphJoiner.Join(tighterPassage));

        var widerPassage = new List<PageReader.Line>
        {
            Line("A short passage.", null),
            Line("Set at 11pt, normal-8.", 8),
            Line("This 12pt gap is a real break.", 12),
        };
        Assert.Contains('\n', ParagraphJoiner.Join(widerPassage));
    }

    /// <summary>
    /// A word broken across a line ending in a real hyphen is always rejoined, whatever the
    /// vertical gap above the continuation happens to be — a hyphenated word can never itself be
    /// the start of a new paragraph.
    /// </summary>
    [Fact]
    public void AHyphenatedWordIsRejoinedEvenAcrossALargeGap()
    {
        var lines = new List<PageReader.Line>
        {
            Line("This Power represents your", null),
            Line("ability to alter probability fields, and it covers this top-", 12),
            Line("ic in detail, however far below the hyphen the page sets it.", 24),
            Line("PRO Control (+4): a genuine second paragraph, not hyphenated above.", 24),
        };

        var text = ParagraphJoiner.Join(lines);

        Assert.Equal(
            "This Power represents your ability to alter probability fields, and it covers this "
            + "topic in detail, however far below the hyphen the page sets it.\n"
            + "PRO Control (+4): a genuine second paragraph, not hyphenated above.",
            text);
    }

    /// <summary>
    /// An em-dash-shaped double hyphen is not a broken word and must not be swallowed by the
    /// hyphen rejoin — the same rule <c>Program.Join</c> always had, still true beside the new
    /// paragraph logic.
    /// </summary>
    [Fact]
    public void ADoubleHyphenIsNotTreatedAsAWordBreak()
    {
        var lines = new List<PageReader.Line>
        {
            Line("free action, and you can do so at any time--", null),
            Line("you don't have to wait for your turn to act.", 12),
        };

        var text = ParagraphJoiner.Join(lines);

        Assert.Equal(
            "free action, and you can do so at any time-- you don't have to wait for your turn to act.",
            text);
    }

    /// <summary>
    /// <b>Too few samples to call anything "normal": nothing splits.</b> A one- or two-line
    /// passage has at most one measured gap, which can never exceed itself times the multiplier —
    /// the conservative direction, since a missed break is a plain paragraph and a false one is a
    /// sentence broken in half.
    /// </summary>
    [Fact]
    public void APassageWithFewerThanTwoGapsNeverSplits()
    {
        var oneLine = new List<PageReader.Line> { Line("Just one line.", null) };
        Assert.Equal("Just one line.", ParagraphJoiner.Join(oneLine));

        var twoLines = new List<PageReader.Line>
        {
            Line("The first line.", null),
            Line("The second line, however far below.", 400), // a huge gap, but the only sample
        };
        Assert.DoesNotContain('\n', ParagraphJoiner.Join(twoLines));
    }

    /// <summary>
    /// A line whose predecessor is unknown — the first line after a heading, after a full-width
    /// break, or at the top of a fresh page or column, all of which <see cref="PageReader"/>
    /// reports as a <c>null</c> <see cref="PageReader.Line.LeadingGap"/> — is joined as an
    /// ordinary continuation, never as a paragraph start manufactured from nothing.
    /// </summary>
    [Fact]
    public void ANullGapMidPassageIsJoinedAsAnOrdinaryContinuation()
    {
        var lines = new List<PageReader.Line>
        {
            Line("The paragraph starts here", null),
            Line("and continues normally,", 12),
            Line("crosses a page break here", null), // no comparable predecessor
            Line("and keeps going after it.", 12),
        };

        var text = ParagraphJoiner.Join(lines);

        Assert.DoesNotContain('\n', text);
        Assert.Equal(
            "The paragraph starts here and continues normally, crosses a page break here and "
            + "keeps going after it.",
            text);
    }

    /// <summary>Empty and whitespace-only lines contribute nothing, same as before this existed.</summary>
    [Fact]
    public void BlankLinesAreSkipped()
    {
        var lines = new List<PageReader.Line>
        {
            Line("First.", null),
            Line("   ", 12),
            Line("Second.", 12),
        };

        Assert.Equal("First. Second.", ParagraphJoiner.Join(lines));
    }

    [Fact]
    public void AnEmptyPassageJoinsToAnEmptyString()
    {
        Assert.Equal("", ParagraphJoiner.Join([]));
    }
}
