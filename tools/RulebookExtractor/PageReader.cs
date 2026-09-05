using UglyToad.PdfPig.Content;

namespace ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

/// <summary>
/// One PDF glyph, reduced to exactly what the reading-order logic below looks at: its text, its
/// horizontal extent, its baseline, the font it is set in, its point size, and whether it runs
/// horizontally.
///
/// <para><b>This is the seam that makes <see cref="PageReader"/> testable.</b>
/// <see cref="UglyToad.PdfPig.Content.Page"/> has no public constructor — its only constructor
/// takes a <c>DictionaryToken</c>, a token scanner and four other internal PDF-parsing types — so
/// nothing built from a real <see cref="UglyToad.PdfPig.Content.Page"/> can be constructed inside
/// a test. <see cref="PageReader.Read(Page)"/> stays a one-line adapter that maps PdfPig's own
/// <c>Letter</c> onto this record; everything that actually decides reading order runs against
/// <see cref="RawLetter"/> instead, the same way <see cref="ColumnLayout"/> is driven against
/// <see cref="Span"/> rather than against a page.</para>
/// </summary>
internal readonly record struct RawLetter(
    string Value, double Left, double Right, double Baseline,
    string FontName, double PointSize, bool IsHorizontal);

/// <summary>
/// Turns one PDF page into lines of text in reading order.
///
/// <para>The previous extractor split every page at a fixed midpoint and emitted the left half
/// then the right half. That is right for the two-column body and <b>destroys any line that runs
/// the full width</b> — it cuts the line in two and files the halves in different blocks, so a
/// sentence loses its middle and the orphans pile up at the front of the section. Every chapter
/// opening in the book is set full width, which is why every chapter opening was scrambled.</para>
///
/// <para>So the gutter is found per page rather than assumed, and whether a line crosses it is
/// decided <b>by whether a word actually sits astride it</b> — not by whether the line's leftmost
/// and rightmost words fall either side, which is equally true of two facing headings sharing a
/// baseline. Reading p.52 that way is how "OVERKILL" and "PHASE SHIFT", two separate entries,
/// came out as one heading.</para>
/// </summary>
public sealed class PageReader
{
    /// <summary>
    /// Everything below this is the running foot: page number, wordmark, watermark.
    ///
    /// <para><b>This is also what keeps the doubled glyphs out.</b> The display faces are faked
    /// bold by drawing the text twice a fraction of a point apart, so the two passes interleave
    /// and the page number 29 reads as "2299" — which the previous corpus carried inside 83
    /// sentences. Every element that does it sits in the running foot, so dropping the foot
    /// removes them all; a glyph-level de-duplicator was written for the job and turned out to
    /// change not one byte of the output, so it is not here. What guards the outcome is
    /// <c>RulebookCorpusTests.NoDoubledGlyphArtifactSurvivesInTheCorpus</c>, which asserts on the
    /// corpus rather than on the mechanism and so still bites if this line moves.</para>
    /// </summary>
    private const double FurnitureTop = 30;

    /// <summary>
    /// The purchaser's name and order number, stamped on every page. 6pt Helvetica appears
    /// nowhere else in the book: 780 words, which is exactly four per page across 195 pages.
    /// Dropped by font so it cannot depend on spelling the purchaser's name in the source.
    /// </summary>
    private static bool IsWatermark(RawLetter l) =>
        Family(l.FontName).StartsWith("Helvetica", StringComparison.Ordinal) && l.PointSize <= 6.5;

    /// <summary>The display faces. A line set in one of these is a heading, not prose.</summary>
    private static readonly string[] HeadingFamilies =
    [
        "LeagueGothic", "RefrigeratorDeluxe", "CCBiffBamBoom", "Montserrat-SemiBold",
        "Montserrat-Bold", "Montserrat-ExtraBold"
    ];

    /// <summary>Strips the PDF subset prefix, e.g. "WQRANL+LeagueGothic-Regular".</summary>
    private static string Family(string? fontName)
    {
        var name = fontName ?? "";
        return name.Length > 7 && name[6] == '+' ? name[7..] : name;
    }

    /// <param name="LeadingGap">
    /// The vertical distance from this line's baseline to the baseline of the line immediately
    /// before it <b>in the same column-run</b> — the same physical column, unbroken by a
    /// full-width line, a page boundary or the start of the page. <c>null</c> when there is no
    /// such predecessor: the first line of a column, the line right after a full-width break, or
    /// the first line read from a fresh page. Those are exactly the places where two baselines
    /// are not comparable — a column restarts near the top of the page, so a raw subtraction
    /// there would be large and negative, meaning "no signal" rather than "no gap" — so a
    /// predecessor is deliberately not invented for them. See <see cref="Order"/>.
    /// </param>
    public sealed record Line(
        string Text, double Baseline, double Left, double Right, bool IsHeading, double Size,
        double? LeadingGap = null);

    private sealed record Word(string Text, double Left, double Right, bool Heading, double Size);

    /// <summary>Adapts one real PdfPig page onto <see cref="RawLetter"/> and reads it.</summary>
    public IReadOnlyList<Line> Read(Page page) =>
        Read(page.Letters.Select(l => new RawLetter(
            l.Value, l.BoundingBox.Left, l.BoundingBox.Right, l.StartBaseLine.Y,
            l.FontName ?? "", l.PointSize, l.TextOrientation == TextOrientation.Horizontal)).ToList(),
            page.Width);

    /// <summary>
    /// The actual reading-order logic, taking the seam type so it can be driven against a
    /// made-up page in a test rather than only against the book.
    /// </summary>
    internal IReadOnlyList<Line> Read(IReadOnlyList<RawLetter> letters, double pageWidth)
    {
        // Rotated text is furniture in this book and never prose: the chapter title runs up the
        // outer margin one letter at a time, and the word "chapter" runs beside it — which
        // arrives reversed ("retpahc") and, grouped by baseline, lands inside body lines.
        var kept = letters
            .Where(l => l.IsHorizontal)
            .Where(l => l.Baseline >= FurnitureTop)
            .Where(l => !IsWatermark(l))
            .ToList();

        var lines = kept
            .GroupBy(l => Math.Round(l.Baseline / 2.0) * 2.0)
            .Select(g => (Baseline: g.Key, Words: BuildWords(g.OrderBy(l => l.Left).ToList())))
            .Where(l => l.Words.Count > 0)
            .OrderByDescending(l => l.Baseline)
            .ToList();

        return Order(lines, pageWidth);
    }

    /// <summary>
    /// <b>The page carries its spaces as real glyphs</b>, so words are split on those rather than
    /// guessed from gaps. Guessing is what merged "FORCE FIELD" into "FORCEFIELD": in the
    /// condensed display face a word space is barely wider than the gap between two letters, and
    /// a threshold that separates them there runs a body line into pieces.
    /// </summary>
    private static List<Word> BuildWords(List<RawLetter> line)
    {
        var words = new List<Word>();
        var text = "";
        double left = 0, right = 0, size = 0;
        var headingGlyphs = 0;
        var totalGlyphs = 0;

        void Close()
        {
            if (text.Trim().Length > 0)
                words.Add(new Word(text.Trim(), left, right, headingGlyphs * 2 > totalGlyphs, size));
            text = "";
            headingGlyphs = 0;
            totalGlyphs = 0;
            size = 0;
        }

        foreach (var l in line)
        {
            if (string.IsNullOrWhiteSpace(l.Value)) { Close(); continue; }

            // A space glyph is not the only word break. Two facing headings share a baseline with
            // nothing but the gutter between them and no space glyph in it, so reading spaces
            // alone fused "OVERKILL" and "PHASE SHIFT" into one word — which then sat astride the
            // gutter and was read as a full-width line, merging two entries into one.
            // The threshold is well above the ~1pt that separates letters inside a word at 9pt.
            if (text.Length > 0 && l.Left - right > Math.Max(2.5, 0.45 * l.PointSize))
                Close();

            if (text.Length == 0) left = l.Left;
            right = l.Right;
            text += l.Value;
            size = Math.Max(size, l.PointSize);

            totalGlyphs++;
            if (HeadingFamilies.Any(f => Family(l.FontName).StartsWith(f, StringComparison.Ordinal)))
                headingGlyphs++;
        }

        Close();
        return words;
    }

    /// <summary>
    /// Reading order for a page that mixes a two-column body with full-width lines.
    ///
    /// <para>A line with a word astride the gutter runs full width; any other line is cut at the
    /// gutter into the part belonging to each column. Full-width lines then break the page into
    /// bands, and within a band the left column is read before the right — the ordinary
    /// two-column rule, applied to the part of the page it is actually true of.</para>
    /// </summary>
    private static List<Line> Order(List<(double Baseline, List<Word> Words)> lines, double pageWidth)
    {
        var (gutterLeft, gutterRight) = FindGutter(lines, pageWidth);

        // The width of the page's text block, used to tell a genuinely full-width line from a
        // column line that merely reaches past the middle.
        var all = lines.SelectMany(l => l.Words).ToList();
        var measure = all.Count == 0 ? pageWidth : all.Max(w => w.Right) - all.Min(w => w.Left);

        var ordered = new List<Line>(lines.Count);
        var left = new List<Line>();
        var right = new List<Line>();

        // The previous line seen in each running column, so a gap can be measured against the line
        // that is actually physically above this one. Reset at a full-width break (a column that
        // resumes below one has no comparable predecessor) and implicitly reset at the start of
        // every page, because a fresh call to Order() starts these at null again.
        //
        // A predecessor that was itself a heading is treated the same as no predecessor at all:
        // the space above an entry's first line is the space the layout gives a heading, not a
        // paragraph gap, and <c>Program.cs</c> never joins body text across a heading anyway (a
        // heading closes the section and starts a fresh one) — so that gap carries no signal
        // either side would ever use it for.
        (double Baseline, bool IsHeading)? leftPrev = null, rightPrev = null, singlePrev = null;

        // <summary>
        // Empties the band's two column-runs into the output, left column then right.
        //
        // <para><paramref name="aFullWidthRunFollows"/> is true when the band is being closed
        // <b>because a full-width line was reached</b>, rather than because the page ran out. In
        // that case one line of the band may not belong to its column at all: <b>the title of the
        // full-width block underneath it.</b> Printed p.81 is the case — "EXAMPLE OF COMBAT" is
        // set at the left margin, so it sits inside the left column's x-range and reads as the
        // left column's last line, but everything it titles runs the full width below the band.
        // Emitting it with its column put it <em>before</em> the right-hand sidebar's own heading,
        // "WOUND PENALTIES", and since a heading with no body of its own qualifies the headings
        // beneath it, the two sections merged into one called "EXAMPLE OF COMBAT — WOUND
        // PENALTIES" — carrying a Gritty Combat rule and a two-page worked example under a heading
        // that inverts which is which.</para>
        //
        // <para>The signal is positional and does not depend on recognising either title: the line
        // is a <b>heading</b>, it is <b>lower than every other line in the band</b> — so nothing in
        // either column follows it — and a full-width run starts immediately below it. A heading at
        // the foot of one column whose body continues at the top of the other does not match,
        // because that column runs on past it.</para>
        // </summary>
        void Flush(bool aFullWidthRunFollows = false)
        {
            var banded = new List<Line>(left.Count + right.Count);
            banded.AddRange(left);
            banded.AddRange(right);

            Line? title = null;
            if (aFullWidthRunFollows && banded.Count > 1)
            {
                var lowest = banded.MinBy(l => l.Baseline)!;
                if (lowest.IsHeading && banded.All(l => ReferenceEquals(l, lowest) || l.Baseline > lowest.Baseline))
                    title = lowest;
            }

            foreach (var line in banded)
                if (!ReferenceEquals(line, title))
                    ordered.Add(line);

            if (title is not null) ordered.Add(title);

            left.Clear();
            right.Clear();
            leftPrev = null;
            rightPrev = null;
        }

        static Line Build(double baseline, List<Word> ws, (double Baseline, bool IsHeading)? prev)
        {
            var isHeading = ws.Count(w => w.Heading) * 2 > ws.Count;
            var gap = prev is { IsHeading: false } p ? p.Baseline - baseline : (double?)null;
            return new(string.Join(' ', ws.Select(w => w.Text)),
                baseline, ws[0].Left, ws[^1].Right, isHeading, ws.Max(w => w.Size), gap);
        }

        foreach (var (baseline, words) in lines)
        {
            if (gutterRight <= gutterLeft)                       // single-column page
            {
                var single = Build(baseline, words, singlePrev);
                ordered.Add(single);
                singlePrev = (baseline, single.IsHeading);
                continue;
            }

            if (CrossesGutter(words, gutterLeft, gutterRight, measure))
            {
                Flush(aFullWidthRunFollows: true);
                ordered.Add(Build(baseline, words, null));
                continue;
            }

            var l = words.Where(w => w.Right <= gutterRight).ToList();
            var r = words.Where(w => w.Right > gutterRight).ToList();

            if (l.Count > 0) { var bl = Build(baseline, l, leftPrev); left.Add(bl); leftPrev = (baseline, bl.IsHeading); }
            if (r.Count > 0) { var br = Build(baseline, r, rightPrev); right.Add(br); rightPrev = (baseline, br.IsHeading); }
        }

        Flush();
        return ordered;
    }

    /// <summary>
    /// Whether this line runs the full width of the page rather than belonging to one column.
    ///
    /// <para><b>The test is that no inter-word gap swallows the gutter band</b>, not that some
    /// single word sits astride it. A word straddling the band is sufficient but not necessary:
    /// running text has a space every few characters, and if one happens to fall inside the band
    /// then no word crosses it and a genuinely full-width line reads as two columns. That is what
    /// re-broke the opening of printed p.13 — "from the Heroes the GM" — after the gutter search
    /// was widened.</para>
    ///
    /// <para>Two facing headings, by contrast, are separated by a gap the width of the gutter,
    /// which is exactly what this rejects. The gap is measured against
    /// <see cref="ColumnLayout.MinGutterWidth"/> rather than against the detected band, because
    /// the band spans where <em>every</em> line is empty and is therefore a little wider than the
    /// gap on any one of them — requiring a gap to swallow it whole made every line on printed
    /// p.81 read as full width.</para>
    /// </summary>
    private static bool CrossesGutter(
        List<Word> words, double gutterLeft, double gutterRight, double measure)
    {
        var centre = (gutterLeft + gutterRight) / 2;

        if (!words.Any(w => w.Right <= centre) || !words.Any(w => w.Left >= centre)) return false;

        // <b>A full-width line runs nearly the whole measure.</b> Without this, a left-column line
        // whose last word pokes past the gutter centre counts as full width, and because a
        // full-width line ends the current band, one false positive splits the page's column flow
        // in two and the halves are emitted out of order. That cost 26 of Chapter 2's Power
        // entries their opening stat line, each one picking up the tail of its neighbour — and
        // the previous extractor, for all its faults, got every one of those right.
        if (words[^1].Right - words[0].Left < measure * 0.75) return false;

        for (var i = 1; i < words.Count; i++)
        {
            var gapLeft = words[i - 1].Right;
            var gapRight = words[i].Left;

            if (gapLeft <= centre && gapRight >= centre && gapRight - gapLeft >= ColumnLayout.MinGutterWidth)
                return false;                       // a column break sits at the gutter
        }

        return true;
    }

    private static (double Left, double Right) FindGutter(
        List<(double Baseline, List<Word> Words)> lines, double pageWidth) =>
        ColumnLayout.FindGutter(
            lines.Select(l => (IReadOnlyList<Span>)l.Words
                                   .Select(w => new Span(w.Left, w.Right)).ToList()).ToList(),
            pageWidth);
}
