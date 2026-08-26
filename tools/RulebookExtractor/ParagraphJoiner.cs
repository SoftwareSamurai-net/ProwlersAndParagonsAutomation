namespace ProwlersAndParagonsAutomation.Tools.RulebookExtractor;

/// <summary>
/// Turns one section's lines into the paragraphs the printed page actually has.
///
/// <para><b>The corpus used to run a whole entry together as one flat line</b> — LUCK's
/// description arrived as a single 120-word run — because nothing in the extractor kept the
/// information that would tell a line wrap from a paragraph start. That information was never
/// missing from the PDF: the page sets a paragraph start with more vertical space above it than
/// an ordinary wrapped line gets, and <see cref="PageReader"/> already measures that gap for every
/// line as <see cref="PageReader.Line.LeadingGap"/>. This is where it gets turned into a decision,
/// separated from <c>Program.cs</c> the same way <see cref="ColumnLayout"/> is separated from
/// <see cref="PageReader"/> — so it can be driven against a made-up passage rather than only
/// against the book.</para>
/// </summary>
internal static class ParagraphJoiner
{
    /// <summary>
    /// A line starts a new paragraph when its gap is more than this multiple of the smallest gap
    /// seen elsewhere in the same passage.
    ///
    /// <para><b>Measured, not guessed — see <c>PROGRESS.md</c> item 1c for the full distribution.</b>
    /// Across the whole book, a passage's ordinary line-to-line leading clusters tightly (12pt at
    /// 9pt body type, 10-12pt at 8.5pt, 18pt in the Introduction's own looser single-column
    /// style), and the closest any genuine paragraph break ever sits above its own passage's
    /// normal leading is 1.5x (LUCK's second PRO block: 12pt normal, 18pt before it). 1.4x clears
    /// that gap in both directions: comfortably below every measured real break, and comfortably
    /// above the noise inside one passage's own leading value — an 8.5pt passage's line-to-line
    /// gap wobbles between 10pt and 12pt with nothing meant by it, and 12/10 = 1.2, well short of
    /// 1.4.</para>
    /// </summary>
    internal const double Multiplier = 1.4;

    /// <summary>
    /// Joins one section's lines into its paragraphs, separated by <c>\n</c> —
    /// <c>RulebookProse.Read</c> and <c>BookText</c> already split and render on exactly that
    /// character, so nothing downstream of the corpus needs to change for a passage that gains
    /// real breaks to show them.
    ///
    /// <para>A word broken across a line ends in a hyphen and is rejoined; that check runs first
    /// and unconditionally, because a hyphenated word can never itself be the start of a new
    /// paragraph, whatever the vertical gap measured above it happens to be. Everything else is
    /// joined by a single space unless it clears <see cref="Multiplier"/>.</para>
    ///
    /// <para><b>The threshold is local to this passage, never a book-wide constant.</b> The same
    /// nominal font size is set with different leading in different parts of the book — the
    /// Introduction's single-column prose is normal-18pt at 11pt type, where a Chapter 8
    /// character quote at the very same 11pt is normal-12pt — so a fixed point value picked to
    /// catch the Introduction's real breaks (which start at 30pt) would never fire inside a
    /// tighter-leaded passage, and picked to catch a tighter passage's would mark every ordinary
    /// line-wrap of the Introduction's own looser style as a fresh paragraph. Using <em>this
    /// passage's own</em> smallest gap as its normal leading sidesteps both: whatever style a
    /// given passage is set in, its tightest observed line-to-line gap is by definition an
    /// ordinary wrap, never a paragraph start (a break only ever adds space, never removes it).
    /// </para>
    ///
    /// <para>A passage with fewer than two measured gaps (a one- or two-line entry, or one whose
    /// only line follows a heading or a full-width break) has nothing to compare and never splits
    /// — the conservative direction, since the risk this whole feature has to answer to is a
    /// break invented mid-sentence, not a break missed.</para>
    /// </summary>
    public static string Join(IReadOnlyList<PageReader.Line> lines)
    {
        var gaps = new List<double>(lines.Count);
        foreach (var l in lines)
            if (l.LeadingGap is double g)
                gaps.Add(g);

        // Fewer than two samples: there is nothing to call "normal" against, so nothing splits.
        double? normalLeading = gaps.Count >= 2 ? gaps.Min() : null;

        var text = "";

        foreach (var raw in lines)
        {
            var line = raw.Text.Trim();
            if (line.Length == 0) continue;

            if (text.Length == 0) { text = line; continue; }

            var hyphenated = text.EndsWith('-') && !text.EndsWith("--", StringComparison.Ordinal);

            if (hyphenated)
                text = text[..^1] + line;
            else if (normalLeading is double normal
                     && raw.LeadingGap is double gap
                     && gap > normal * Multiplier)
                text += "\n" + line;
            else
                text += " " + line;
        }

        return text.Trim();
    }
}
