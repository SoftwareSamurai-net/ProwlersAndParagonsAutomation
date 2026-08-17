namespace ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

/// <summary>The horizontal extent of one word on a line. Enough to decide the page's columns.</summary>
internal readonly record struct Span(double Left, double Right);

/// <summary>
/// Where the columns are. Pure geometry, separated from <see cref="PageReader"/> so it can be
/// driven against a made-up page rather than only against the book — the shipped data cannot show
/// you a layout it happens not to contain, and the bug below was invisible for exactly that reason.
/// </summary>
internal static class ColumnLayout
{
    /// <summary>
    /// A real gutter in this book measures about 18pt. Anything narrower is a coincidence between
    /// two lines, and treating it as a gutter would split a single-column page down the middle.
    /// </summary>
    internal const double MinGutterWidth = 8;

    /// <summary>
    /// Finds the vertical band separating the columns, searched only in the middle fifth of the
    /// page so a ragged right edge cannot be mistaken for it.
    ///
    /// <para><b>The tolerance is raised until a band of real width appears, rather than fixed at
    /// the emptiest point.</b> A page may carry a few full-width lines across its gutter, so the
    /// crossing count there is not zero — and taking the strict minimum then measuring the run at
    /// exactly that count is a trap: on printed p.83 the minimum is reached on a <b>1pt spur</b>
    /// where two lines happen to end, while the actual 20pt gutter beside it is crossed by seven.
    /// The narrow spur failed the width test, the page was declared single-column, and both
    /// columns were then emitted interleaved line by line — which is the very defect this whole
    /// class exists to prevent, arrived at from the other direction. Sixteen pages were damaged
    /// that way.</para>
    ///
    /// <para>Returns an empty band when no tolerance inside the cap yields a wide enough gap,
    /// which is how a genuinely single-column page — a full-page table, the credits — reads whole
    /// instead of being cut in half.</para>
    /// </summary>
    /// <summary>
    /// <b>Two tests, because neither alone covers both shapes of page in this book.</b>
    ///
    /// <para>On an ordinary two-column page almost every line sits in one column only, so there
    /// is no gap <em>within</em> a line to find — the gutter shows up as a column of the page
    /// that few words cross. That is <see cref="ByEmptiness"/>.</para>
    ///
    /// <para>But printed p.81 sets two sidebars side by side <em>above</em> full-width body text.
    /// No column of that page is empty top to bottom, so by emptiness its gutter dips only a
    /// quarter below a typical line — indistinguishable from noise — and the sidebars were read
    /// straight across. There the gutter is visible only as a gap repeated at the same x on the
    /// lines that do have one, which is <see cref="ByRepeatedGap"/>.</para>
    ///
    /// <para>Emptiness is tried first because it is the stronger signal, and it is the one that
    /// applies to nearly every page.</para>
    /// </summary>
    internal static (double Left, double Right) FindGutter(
        IReadOnlyList<IReadOnlyList<Span>> lines, double pageWidth)
    {
        if (lines.Count < 6) return (0, 0);

        var byEmptiness = ByEmptiness(lines, pageWidth);
        return byEmptiness.Right - byEmptiness.Left >= MinGutterWidth
            ? byEmptiness
            : ByRepeatedGap(lines, pageWidth);
    }

    /// <summary>The widest column of the page's middle fifth that few words cross.</summary>
    private static (double Left, double Right) ByEmptiness(
        IReadOnlyList<IReadOnlyList<Span>> lines, double pageWidth)
    {
        var words = lines.SelectMany(l => l).ToList();
        if (words.Count == 0) return (0, 0);

        const double stepSize = 0.5;
        int Coverage(double x) => words.Count(w => w.Left < x && w.Right > x);

        // How busy a column is on this page. The gutter is judged relative to that rather than
        // against zero, because a page may carry full-width lines over its gutter.
        var block = new List<int>();
        for (var x = pageWidth * 0.15; x <= pageWidth * 0.85; x += stepSize) block.Add(Coverage(x));
        block.Sort();
        var typical = block[block.Count / 2];
        if (typical == 0) return (0, 0);

        var threshold = Math.Max(1.0, typical * 0.25);

        double bestLeft = 0, bestRight = 0, runStart = -1;
        var to = pageWidth * 0.60;
        for (var x = pageWidth * 0.40; x <= to; x += stepSize)
        {
            if (Coverage(x) <= threshold)
            {
                if (runStart < 0) runStart = x;
            }
            else if (runStart >= 0)
            {
                if (x - runStart > bestRight - bestLeft) { bestLeft = runStart; bestRight = x; }
                runStart = -1;
            }
        }
        if (runStart >= 0 && to - runStart > bestRight - bestLeft) { bestLeft = runStart; bestRight = to; }

        return (bestLeft, bestRight);
    }

    /// <summary>A gutter-sized hole recurring at the same place on enough of the page's lines.</summary>
    private static (double Left, double Right) ByRepeatedGap(
        IReadOnlyList<IReadOnlyList<Span>> lines, double pageWidth)
    {
        var candidates = new List<Span>();

        foreach (var line in lines)
            for (var i = 1; i < line.Count; i++)
            {
                var gap = new Span(line[i - 1].Right, line[i].Left);
                var centre = (gap.Left + gap.Right) / 2;

                if (gap.Right - gap.Left >= MinGutterWidth
                    && centre >= pageWidth * 0.35 && centre <= pageWidth * 0.65)
                    candidates.Add(gap);
            }

        // A handful of coincidental holes is not a column break. Requiring a real share of the
        // page's lines to agree keeps a single-column page from being split down the middle by
        // two lines that happen to line up.
        if (candidates.Count < Math.Max(3, lines.Count * 0.15)) return (0, 0);

        // Median edges: robust to a line whose gap is wider or offset.
        var left = Median(candidates.Select(c => c.Left).ToList());
        var right = Median(candidates.Select(c => c.Right).ToList());

        return right - left >= MinGutterWidth ? (left, right) : (0, 0);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}
