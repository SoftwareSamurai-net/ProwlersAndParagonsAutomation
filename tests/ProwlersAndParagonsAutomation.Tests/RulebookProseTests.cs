using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><see cref="RulebookProse"/> finds the structure the book prints, and this drives it over the
/// whole corpus rather than over an example.</b>
///
/// <para>The extraction flattens a Power's entry to one line — <c>LUCK</c> arrives as a single
/// 200-word run with its stat line and both its Pros run into the prose — so the two surfaces that
/// show the book's own words showed a wall. What the splitter pulls out is only ever a marker the
/// page itself sets: the bullet-separated stat line, <c>PRO Name (price):</c> inside an entry, and
/// a generic option's own opening price.</para>
///
/// <para><b>The counts are the guard, and they are measured rather than chosen.</b> A parse over
/// prose can fail in two directions and only one of them is loud: matching too little shows as a
/// passage that reads exactly as it did before, which nobody notices. So the numbers below were
/// measured across all 1,525 passages first, and a change that starts matching more or less than
/// the book marks moves one of them.</para>
///
/// <para><b>Every count carries the negative beside it.</b> Six sections share a heading with a
/// Power and are not that Power's entry — Ch.6's ARMOR gear row and COMMUNICATIONS base points,
/// Ch.7's SWIMMING and LEAPING, Ch.4's HEALING, Ch.9's TIME TRAVEL — and a splitter that gave
/// those a stat line would be inventing one. A count on its own is satisfied by a pattern that
/// fires everywhere.</para>
/// </summary>
public sealed class RulebookProseTests
{
    private static string CorpusPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    private sealed record Chapter(
        [property: System.Text.Json.Serialization.JsonPropertyName("chapter")] int Number,
        Section[] Sections);

    // Only the heading and the text: this file asks what the splitter makes of a passage, and
    // the printed page is `RulebookCorpusTests`' subject. A positional property nothing reads is
    // a field somebody has to wonder about.
    private sealed record Section(string Heading, string Text);

    private static readonly JsonSerializerOptions SnakeCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static List<(int Chapter, string Heading, string Text)> Corpus() =>
        [.. Directory.GetFiles(CorpusPath, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<Chapter>(File.ReadAllText(f), SnakeCase)
                ?? throw new InvalidOperationException($"{f} is not readable as a chapter."))
            .SelectMany(c => c.Sections.Select(s => (c.Number, s.Heading, s.Text)))];

    /// <summary>
    /// <b>Exactly as many passages yield a stat line as there are Powers in the rules data</b> —
    /// 141 and 141, over the whole book.
    ///
    /// <para>That equality is the real guard and it was not the one first written here. The count
    /// was set to 116, which is what <c>RulebookCorpusTests</c> checks: the Power entries whose
    /// corpus heading matches a name in <c>data/rules</c>. The other 25 are the Form,
    /// Transformation and Super Senses options, each printed with a stat line of its own and each
    /// its own entry in <c>powers.json</c> — filed in the corpus under headings the rules data
    /// spells differently, which is exactly why a heading match misses them. Reading the number off
    /// the rules data instead of writing it down makes the two stores witnesses to each other.</para>
    /// </summary>
    [Fact]
    public void EveryPowerEntryYieldsItsThreeStatFields()
    {
        var found = Corpus().Where(s => RulebookProse.Read(s.Text).Stat is not null).ToList();

        Assert.Equal(new RulesRepository(RulesFixture.DataPath).Powers.Count, found.Count);

        // Not merely non-null: a Range out of the book's closed set of five, a rank type, and a
        // cost. A parse that returned three empty strings would satisfy the count above.
        Assert.All(found, s =>
        {
            var stat = RulebookProse.Read(s.Text).Stat!;

            Assert.Contains(stat.Range, (string[])["Self", "Touch", "Ranged", "Zone", "Special"]);
            Assert.NotEmpty(stat.RankType);
            Assert.NotEmpty(stat.Cost);
        });
    }

    /// <summary>
    /// The negative that makes the count above mean something: a section sharing a Power's heading
    /// without being that Power's entry is left alone. Ch.6's ARMOR is a row in a gear table.
    /// </summary>
    [Theory]
    [InlineData(4, "HEALING")]
    [InlineData(6, "ARMOR")]
    [InlineData(6, "COMMUNICATIONS")]
    [InlineData(7, "SWIMMING")]
    [InlineData(7, "LEAPING")]
    [InlineData(9, "TIME TRAVEL")]
    public void ASectionSharingAPowersHeadingIsNotGivenAStatLine(int chapter, string heading)
    {
        var section = Corpus().Single(s => s.Chapter == chapter && s.Heading == heading);

        Assert.Null(RulebookProse.Read(section.Text).Stat);

        // And the text is not lost on the way past — the whole point of degrading is that the
        // passage still reads.
        Assert.NotEmpty(RulebookProse.Read(section.Text).Paragraphs);
    }

    /// <summary>
    /// The 102 Pros and Cons printed inside a Power's own entry, which is the figure
    /// <c>CLAUDE.md</c> records from a whole-book sweep for the marker.
    /// </summary>
    [Fact]
    public void EveryOptionPrintedInsideAnEntryBecomesABlockOfItsOwn()
    {
        var options = Corpus().SelectMany(s => RulebookProse.Read(s.Text).Options).ToList();

        Assert.Equal(102, options.Count);
        Assert.All(options, o =>
        {
            Assert.Contains(o.Kind, (string[])["PRO", "CON"]);
            Assert.NotEmpty(o.Name);
            Assert.NotEmpty(o.Price);
            Assert.NotEmpty(o.Text);
        });
    }

    /// <summary>
    /// A generic option's own section opens with its price rather than with prose — 51 of them,
    /// including OVERKILL and WEAK, which price themselves <c>(Special)</c>, and CLOSE, which is
    /// priced both ways at once and is one lead rather than two.
    /// </summary>
    [Fact]
    public void AGenericOptionsSectionOpensWithItsOwnPrice()
    {
        var leads = Corpus().Where(s => RulebookProse.Read(s.Text).Lead is not null).ToList();

        Assert.Equal(51, leads.Count);

        // The three that a narrower pattern would have dropped, named so a regression says which.
        Assert.Contains(leads, s => s.Heading == "OVERKILL");
        Assert.Contains(leads, s => s.Heading == "WEAK");
        Assert.Contains(leads, s =>
            s.Heading == "CLOSE" && RulebookProse.Read(s.Text).Lead!.Contains(
                "or CON", StringComparison.Ordinal));
        Assert.All(leads, s => Assert.StartsWith(
            RulebookProse.Read(s.Text).Lead!, s.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Nothing is dropped and nothing is invented.</b> Every word of every passage survives the
    /// split, in order — which is the one property that cannot be checked by counting, and the
    /// failure that would be hardest to see: a regular expression that swallowed a sentence would
    /// leave a passage reading perfectly well and saying something else.
    /// </summary>
    [Fact]
    public void EveryWordOfEveryPassageSurvivesTheSplit()
    {
        foreach (var (chapter, heading, text) in Corpus())
        {
            var prose = RulebookProse.Read(text);

            var rebuilt = string.Join(" ", new[]
                {
                    prose.Stat is { } s ? $"{s.Range} • {s.RankType} • {s.Cost}" : "",
                    prose.Lead ?? "",
                    string.Join(" ", prose.Paragraphs),
                    string.Join(" ", prose.Options.Select(o =>
                        $"{o.Kind} {o.Name} ({o.Price}): {o.Text}")),
                });

            // Assert.True with a message rather than Assert.Equal, which has no message overload:
            // "1,525 passages, one of them wrong" needs to say which one.
            Assert.True(Words(text) == Words(rebuilt),
                $"Ch.{chapter} {heading} does not survive the split intact."
                + $"{Environment.NewLine}  in:  {Words(text)}"
                + $"{Environment.NewLine}  out: {Words(rebuilt)}");
        }
    }

    /// <summary>
    /// The worked example, spelled out, because a count says a parse ran and not that it was right.
    /// LUCK is the entry the owner sent a screenshot of: 200 words in one paragraph, the stat line
    /// and both Pros buried in it.
    /// </summary>
    [Fact]
    public void LuckComesApartIntoItsPrintedParts()
    {
        var prose = RulebookProse.Read(
            Corpus().Single(s => s is { Chapter: 2, Heading: "LUCK" }).Text);

        Assert.Equal("Self", prose.Stat!.Range);
        Assert.Equal("Power Rank", prose.Stat.RankType);
        Assert.Equal("2 Hero Points per rank", prose.Stat.Cost);

        Assert.Equal(["Control", "Unbelievable"], prose.Options.Select(o => o.Name));
        Assert.Equal(["+4", "+1 per rank"], prose.Options.Select(o => o.Price));
        Assert.All(prose.Options, o => Assert.Equal("PRO", o.Kind));

        // The description keeps the description and gives up the options: it opens where the book
        // opens and stops before the first marker.
        var description = Assert.Single(prose.Paragraphs);
        Assert.StartsWith("You are incredibly lucky", description, StringComparison.Ordinal);
        Assert.DoesNotContain("PRO", description, StringComparison.Ordinal);
    }

    /// <summary>
    /// A passage the book marks in none of the three ways — which is most of the book — comes back
    /// as its own text and nothing else. That is what the two surfaces rendered before this
    /// existed, and it has to stay true or the change is a rewrite of the corpus rather than a
    /// reading of it.
    /// </summary>
    [Fact]
    public void AnOrdinaryPassageIsHandedBackUnchanged()
    {
        var plain = Corpus()
            .Where(s => RulebookProse.Read(s.Text) is
                { Stat: null, Lead: null, Options.Count: 0, Paragraphs.Count: > 0 })
            .ToList();

        // The bulk of the book, so this is also the control on the three counts above not having
        // quietly become "everything".
        Assert.True(plain.Count > 1200,
            $"only {plain.Count} of {Corpus().Count} passages are ordinary prose; the splitter is "
            + "finding structure the book does not print.");

        Assert.All(plain.Take(200), s => Assert.Equal(
            Words(s.Text), Words(string.Join(" ", RulebookProse.Read(s.Text).Paragraphs))));
    }

    /// <summary>Empty in, empty out — and no exception, because this runs during a render.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingInIsNothingOut(string? text)
    {
        var prose = RulebookProse.Read(text);

        Assert.Null(prose.Stat);
        Assert.Null(prose.Lead);
        Assert.Empty(prose.Paragraphs);
        Assert.Empty(prose.Options);
    }

    private static string Words(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
