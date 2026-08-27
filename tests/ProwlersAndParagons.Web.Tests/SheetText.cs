using AngleSharp.Dom;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// What a reader actually sees on a rendered sheet, modelled the way a browser renders it.
///
/// <para><b>It moved here when the sheet started explaining itself by default.</b> It lived in
/// <see cref="ExplainedSheetTests"/> while exactly one address drew a sheet with terms on it;
/// every other sheet test could read <c>TextContent</c> and get what the page says. That stopped
/// being true the moment <c>SheetView.Explain</c> defaulted to on — a term's cell now holds the
/// name <em>and</em> two copies of its description, so <c>TextContent</c> on a Trait cell reads
/// "PresenceHow forceful…How forceful…" and seventeen tests across four files failed at once. They
/// were all asking the same question and all deserve the same answer, which is this one rather
/// than four new ones.</para>
///
/// <para>Two things are dropped: the <c>sr-only</c> copy of each description, which
/// <c>aria-describedby</c> names and nobody sees, and the tip, which is <c>display: none</c> until
/// somebody hovers. Both are required and neither is visible — see <c>Term</c> for why the
/// description has to exist twice.</para>
///
/// <para><b>Nothing is inserted between two inline elements, and that is the whole point.</b> A
/// browser concatenates adjacent inline text with no separator, which is why
/// <c>&lt;b&gt;Armor&lt;/b&gt;&lt;span&gt;8d&lt;/span&gt;</c> reads "Armor8d" on the page.
/// <b>This repository has shipped a test for that bug that was beaten by its own helper</b> — the
/// helper replaced every tag with a newline, so the broken markup read "Armor 8d" to the assertion
/// written to catch it. So a boundary is a separator here only where the browser makes one, which
/// is a block element.</para>
///
/// <para>Whitespace already in the markup is <em>collapsed</em>, which is also what a browser does
/// and is not the same thing as inserting some: a run of newlines and indentation between two
/// inline elements renders as one space, and no whitespace at all renders as none.</para>
/// </summary>
public static class SheetText
{
    /// <summary>
    /// The elements the sheet is built from that a browser lays out on a line of their own.
    ///
    /// <para>A closed list rather than a stylesheet lookup, because these are the block elements
    /// HTML defines as such and the sheet uses no others; what it must <b>not</b> contain is an
    /// inline element, for the reason in <see cref="Visible"/>.</para>
    /// </summary>
    private static readonly string[] OwnLine =
        ["ARTICLE", "SECTION", "DIV", "P", "H1", "H2", "H3", "UL", "OL", "LI",
         "TABLE", "TBODY", "TR", "TD", "TH", "DL", "DT", "DD", "FOOTER", "BR"];

    /// <summary>What a reader sees, with runs of whitespace collapsed to one space.</summary>
    public static string Visible(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var lines = Gather(node)
            .Split('\n')
            .Select(line => string.Join(" ",
                line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);

        return string.Join(" ", lines);
    }

    private static string Gather(INode node)
    {
        if (node is IElement hidden &&
            (hidden.ClassList.Contains("sr-only") || hidden.ClassList.Contains("row-tip")))
            return "";

        if (node.NodeType == NodeType.Text) return node.TextContent;

        var inside = string.Concat(node.ChildNodes.Select(Gather));

        return node is IElement block && OwnLine.Contains(block.TagName, StringComparer.Ordinal)
            ? inside + "\n"
            : inside;
    }
}
