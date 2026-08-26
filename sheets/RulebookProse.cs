using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>A Power's opening line, as the book sets it: Range, rank type, cost.</summary>
public sealed record RulebookStatLine(string Range, string RankType, string Cost);

/// <summary>
/// One Pro or Con printed inside an entry.
/// </summary>
/// <param name="Kind"><c>PRO</c> or <c>CON</c>, as the book marks it.</param>
/// <param name="Name">The option's name — "Control", "Only X".</param>
/// <param name="Price">What the book charges for it, in its own words: "+4", "−2 or −4".</param>
/// <param name="Text">What it does.</param>
public sealed record RulebookOption(string Kind, string Name, string Price, string Text);

/// <summary>
/// A passage of the book, broken into the parts the book itself sets apart.
/// </summary>
/// <param name="Stat">The Power's stat line, or null for a passage that is not a Power entry.</param>
/// <param name="Lead">
/// A generic Pro or Con's own price, where the passage opens with one — "PRO +1 Hero Point".
/// </param>
/// <param name="Paragraphs">The prose, in the passage's own paragraphs. Never empty.</param>
/// <param name="Options">The Pros and Cons printed inside the entry, in printed order.</param>
public sealed partial record RulebookProse(
    RulebookStatLine? Stat,
    string? Lead,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<RulebookOption> Options);

/// <summary>
/// Finds the structure the book prints and the extraction flattened.
///
/// <para><b>Every one of these seams is a marker the book sets in the page, not a guess about
/// where a sentence feels like ending.</b> A Power's entry opens with a bullet-separated stat
/// line; a Pro or Con printed inside an entry is marked <c>PRO Name (price):</c>; a generic
/// option's own section opens <c>PRO +1 Hero Point</c>. Those three shapes were measured across
/// all 1,523 passages before a line of this was written — 116 stat lines, 52 price leads and 102
/// option blocks, with nothing left over — and <c>RulebookProseTests</c> holds those counts, so a
/// change that starts guessing shows up as a count that moved rather than as prose nobody reads
/// twice.</para>
///
/// <para><b>What this deliberately does not do is invent a paragraph break.</b> The extractor
/// runs a Power's whole entry together on one line — <c>LUCK</c> arrives as a single 200-word run
/// — so pulling the stat line and the options out is as far as honest structure goes. Splitting
/// the remaining description on "For example," or on sentence count would be this file deciding
/// where the author's paragraphs were. The rest of that gap belongs to the extractor, which is
/// where the page's own vertical spacing still exists to be read; see <c>PROGRESS.md</c>.</para>
///
/// <para><b>It degrades to the text it was given.</b> A passage that matches none of the three —
/// which is most of the book — comes back as its own lines and nothing else, exactly what the two
/// call sites rendered before this existed. Six sections share a heading with a Power and are not
/// Power entries (Ch.6's ARMOR gear row, Ch.3's SWIMMING, Ch.5's HEALING); they have no stat line
/// and must not be given one.</para>
///
/// <para>Here rather than in <c>web/</c> because the parse is a fact about the corpus and belongs
/// where the corpus tests can reach it, beside <see cref="PowerFormatter"/>, which renders the
/// same stat line from the verified mechanics. <b>Where the two disagree, <c>data/rules</c>
/// wins</b> — this reads the book's own words and decides nothing.</para>
/// </summary>
public sealed partial record RulebookProse
{
    /// <summary>
    /// A closed set of price shapes, and closed on purpose.
    ///
    /// <para>The book prices in whole and half Hero Points, per rank, per two ranks, per unit and
    /// per power level, with three entries offering a choice ("1 or 3", "1, 3 or 6") and two
    /// pricing themselves <c>(Special)</c>. Anything else is a shape nobody has read, and the
    /// right answer there is to leave the passage alone rather than to widen this until something
    /// matches.</para>
    /// </summary>
    private const string Money =
        @"\d+(?:\.5)?(?:(?:,| or) \d+(?:\.5)?)* Hero Points?"
        + @"(?: per (?:rank|\d+ ranks|unit|power level))?";

    /// <summary>The same, signed, for an option that adds to or takes off a Power's price.</summary>
    private const string Signed =
        @"(?:\(Special\)|[+−-]\d+(?: to [+−-]?\d+)?(?: or [+−-]?\d+)? Hero Points?"
        + @"(?: per (?:rank|\d+ ranks|unit))?)";

    /// <summary>
    /// "Self • Power Rank • 2 Hero Points per rank". The bullet is the book's own; the five Range
    /// words are the closed set Ch.2 p.19 prints.
    /// </summary>
    private static readonly Regex StatLine = new(
        @"^(?<range>Self|Touch|Ranged|Zone|Special) • (?<rank>[^•]+?) • "
        + @"(?<cost>" + Money + @"|Varies|Special|None)(?=\s|$)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// A generic option's own section, which opens with its price rather than with prose. CLOSE is
    /// priced both ways at once — "PRO +2 Hero Points or CON −2 Hero Points" — so the second half
    /// is part of the lead rather than a second option.
    /// </summary>
    private static readonly Regex PriceLead = new(
        @"^(?<lead>(?:PRO|CON) " + Signed + @"(?: or (?:PRO|CON) " + Signed + @")?)(?=\s|$)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// "PRO Control (+4): " — a Pro or Con printed inside a Power's own entry. The colon and the
    /// parenthesis are both the book's, which is what makes this a marker rather than a pattern.
    /// </summary>
    private static readonly Regex Marker = new(
        @"\b(?<kind>PRO|CON) (?<name>\p{Lu}[\p{L}'’/\- ]*?) \((?<price>[^)]{1,40})\): ",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Reads one passage. Never throws, and never returns no paragraphs.</summary>
    public static RulebookProse Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new RulebookProse(null, null, [], []);

        var rest = text;

        RulebookStatLine? stat = null;
        if (StatLine.Match(rest) is { Success: true } opening)
        {
            stat = new RulebookStatLine(
                opening.Groups["range"].Value,
                opening.Groups["rank"].Value.Trim(),
                opening.Groups["cost"].Value);

            rest = rest[opening.Length..];
        }

        string? lead = null;
        if (PriceLead.Match(rest) is { Success: true } priced)
        {
            lead = priced.Groups["lead"].Value;
            rest = rest[priced.Length..];
        }

        var marks = Marker.Matches(rest);

        // Everything before the first marker is the entry's own description; each marker runs to
        // the next one, or to the end. Taken in printed order, which is the order a reader met
        // them on the page.
        var body = marks.Count == 0 ? rest : rest[..marks[0].Index];

        var options = new List<RulebookOption>(marks.Count);
        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i];
            var end = i + 1 < marks.Count ? marks[i + 1].Index : rest.Length;

            options.Add(new RulebookOption(
                mark.Groups["kind"].Value,
                mark.Groups["name"].Value.Trim(),
                mark.Groups["price"].Value.Trim(),
                rest[(mark.Index + mark.Length)..end].Trim()));
        }

        return new RulebookProse(stat, lead, Lines(body), options);
    }

    /// <summary>
    /// The passage's own paragraph breaks, which the extractor keeps where the page had them.
    /// Nothing here adds one.
    /// </summary>
    private static List<string> Lines(string text) =>
        [.. text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];
}
