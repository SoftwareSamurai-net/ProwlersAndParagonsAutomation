using UglyToad.PdfPig.Content;

namespace ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

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
    private static bool IsWatermark(Letter l) =>
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

    public sealed record Line(
        string Text, double Baseline, double Left, double Right, bool IsHeading, double Size);

    private sealed record Word(string Text, double Left, double Right, bool Heading, double Size);

    public IReadOnlyList<Line> Read(Page page)
    {
        // Rotated text is furniture in this book and never prose: the chapter title runs up the
        // outer margin one letter at a time, and the word "chapter" runs beside it — which
        // arrives reversed ("retpahc") and, grouped by baseline, lands inside body lines.
        var letters = page.Letters
            .Where(l => l.TextOrientation == TextOrientation.Horizontal)
            .Where(l => l.StartBaseLine.Y >= FurnitureTop)
            .Where(l => !IsWatermark(l))
            .ToList();



        var lines = letters
            .GroupBy(l => Math.Round(l.StartBaseLine.Y / 2.0) * 2.0)
            .Select(g => (Baseline: g.Key, Words: BuildWords(g.OrderBy(l => l.BoundingBox.Left).ToList())))
            .Where(l => l.Words.Count > 0)
            .OrderByDescending(l => l.Baseline)
            .ToList();

        return Order(lines, page.Width);
    }

    /// <summary>
    /// <b>The page carries its spaces as real glyphs</b>, so words are split on those rather than
    /// guessed from gaps. Guessing is what merged "FORCE FIELD" into "FORCEFIELD": in the
    /// condensed display face a word space is barely wider than the gap between two letters, and
    /// a threshold that separates them there runs a body line into pieces.
    /// </summary>
    private static List<Word> BuildWords(List<Letter> line)
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
            if (text.Length > 0 && l.BoundingBox.Left - right > Math.Max(2.5, 0.45 * l.PointSize))
                Close();

            if (text.Length == 0) left = l.BoundingBox.Left;
            right = l.BoundingBox.Right;
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

        void Flush()
        {
            ordered.AddRange(left);
            ordered.AddRange(right);
            left.Clear();
            right.Clear();
        }

        static Line Build(double baseline, List<Word> ws) =>
            new(string.Join(' ', ws.Select(w => w.Text)),
                baseline,
                ws[0].Left,
                ws[^1].Right,
                ws.Count(w => w.Heading) * 2 > ws.Count,
                ws.Max(w => w.Size));

        foreach (var (baseline, words) in lines)
        {
            if (gutterRight <= gutterLeft)                       // single-column page
            {
                ordered.Add(Build(baseline, words));
                continue;
            }

            if (CrossesGutter(words, gutterLeft, gutterRight, measure))
            {
                Flush();
                ordered.Add(Build(baseline, words));
                continue;
            }

            var l = words.Where(w => w.Right <= gutterRight).ToList();
            var r = words.Where(w => w.Right > gutterRight).ToList();

            if (l.Count > 0) left.Add(Build(baseline, l));
            if (r.Count > 0) right.Add(Build(baseline, r));
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
