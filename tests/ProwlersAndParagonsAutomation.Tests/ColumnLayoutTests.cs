using ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The corpus generator, driven against made-up pages.</b>
///
/// <para>Everything else about <c>data/rulebook/</c> is asserted as data, which is right — the
/// corpus is committed and the publisher's PDF is not on a build agent. But data can only show
/// you the layouts the book happens to contain, and the extractor's failures have all been
/// layout-shaped: a page whose gutter is not where it was assumed, a line that runs the full
/// width, two facing headings sharing a baseline. Nothing in CI ran a line of this code, so a
/// regression stayed invisible until somebody regenerated the corpus by hand and read it.</para>
///
/// <para>Each case below is a page shape that broke a real extraction, described in the terms the
/// layout code actually sees: the horizontal extent of each word on each line.</para>
/// </summary>
public sealed class ColumnLayoutTests
{
    private const double PageWidth = 612;

    /// <summary>A line of words from <paramref name="left"/>, each <paramref name="wordWidth"/> wide.</summary>
    private static List<Span> Run(double left, double right, double wordWidth = 20, double space = 3)
    {
        var words = new List<Span>();
        for (var x = left; x + wordWidth <= right; x += wordWidth + space)
            words.Add(new Span(x, x + wordWidth));
        return words;
    }

    /// <summary>The book's ordinary body page: two columns, a 20pt gutter, nothing across it.</summary>
    private static List<IReadOnlyList<Span>> TwoColumnPage(int lines = 40)
    {
        var page = new List<IReadOnlyList<Span>>();
        for (var i = 0; i < lines; i++)
        {
            page.Add(Run(45, 297));      // left column
            page.Add(Run(317, 567));     // right column
        }
        return page;
    }

    [Fact]
    public void AnOrdinaryTwoColumnPageFindsItsGutter()
    {
        var (left, right) = ColumnLayout.FindGutter(TwoColumnPage(), PageWidth);

        Assert.True(right - left >= ColumnLayout.MinGutterWidth,
            $"no gutter found on a plainly two-column page (got {left:F1}-{right:F1}).");
        Assert.InRange((left + right) / 2, 295, 320);
    }

    /// <summary>
    /// <b>Most lines of a two-column page sit in one column, so they contain no gap at all.</b>
    /// A detector that looks only for a hole repeated inside lines finds almost none here and
    /// calls the page single-column — which reads both columns straight across, and cost 26 of
    /// Chapter 2's Power entries their opening stat line.
    /// </summary>
    [Fact]
    public void AGutterIsFoundEvenWhenNoSingleLineContainsIt()
    {
        var page = TwoColumnPage();

        Assert.All(page, line =>
        {
            for (var i = 1; i < line.Count; i++)
                Assert.True(line[i].Left - line[i - 1].Right < ColumnLayout.MinGutterWidth,
                    "this fixture is meant to have no gutter-sized gap inside any line.");
        });

        var (left, right) = ColumnLayout.FindGutter(page, PageWidth);
        Assert.True(right - left >= ColumnLayout.MinGutterWidth);
    }

    /// <summary>
    /// <b>Printed p.81 sets two sidebars side by side above full-width body text.</b> No column
    /// of that page is empty top to bottom, so measured purely by emptiness the gutter dips only
    /// a little below a typical line and reads as noise — and the two sidebars were extracted
    /// interleaved, a sentence of one alternating with a sentence of the other.
    /// </summary>
    [Fact]
    public void AGutterIsFoundOnAPageThatIsOnlyPartlyInColumns()
    {
        var page = new List<IReadOnlyList<Span>>();

        for (var i = 0; i < 10; i++)                       // the two sidebars
        {
            var line = new List<Span>();
            line.AddRange(Run(45, 297));
            line.AddRange(Run(317, 567));
            page.Add(line);
        }

        for (var i = 0; i < 30; i++) page.Add(Run(45, 567)); // full-width body below them

        var (left, right) = ColumnLayout.FindGutter(page, PageWidth);

        Assert.True(right - left >= ColumnLayout.MinGutterWidth,
            $"no gutter found on a part-columnar page (got {left:F1}-{right:F1}).");
        Assert.InRange((left + right) / 2, 295, 320);
    }

    /// <summary>
    /// A page set as one column — a full-page table, the credits — must not be cut down the
    /// middle. Splitting one of those emits the halves of every line as two separate blocks.
    /// </summary>
    [Fact]
    public void ASingleColumnPageIsNotGivenAGutter()
    {
        var page = new List<IReadOnlyList<Span>>();
        for (var i = 0; i < 40; i++) page.Add(Run(45, 567));

        var (left, right) = ColumnLayout.FindGutter(page, PageWidth);

        Assert.True(right - left < ColumnLayout.MinGutterWidth,
            $"a single-column page was split at {left:F1}-{right:F1}.");
    }

    /// <summary>
    /// Two lines happening to break at the same place is a coincidence, not a column. This is the
    /// shape that would let a couple of short lines split a page of running text.
    /// </summary>
    [Fact]
    public void TwoCoincidentalGapsDoNotMakeAGutter()
    {
        var page = new List<IReadOnlyList<Span>>();
        for (var i = 0; i < 38; i++) page.Add(Run(45, 567));

        for (var i = 0; i < 2; i++)
        {
            var line = new List<Span>();
            line.AddRange(Run(45, 297));
            line.AddRange(Run(317, 567));
            page.Add(line);
        }

        var (left, right) = ColumnLayout.FindGutter(page, PageWidth);

        Assert.True(right - left < ColumnLayout.MinGutterWidth,
            $"two lines were enough to invent a gutter at {left:F1}-{right:F1}.");
    }

    /// <summary>Too little on the page to tell anything from; guessing would be worse than not.</summary>
    [Fact]
    public void AVeryShortPageIsLeftAlone()
    {
        var page = new List<IReadOnlyList<Span>>();
        for (var i = 0; i < 3; i++)
        {
            var line = new List<Span>();
            line.AddRange(Run(45, 297));
            line.AddRange(Run(317, 567));
            page.Add(line);
        }

        var (left, right) = ColumnLayout.FindGutter(page, PageWidth);
        Assert.True(right - left < ColumnLayout.MinGutterWidth);
    }
}
