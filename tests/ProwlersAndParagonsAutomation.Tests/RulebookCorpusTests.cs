using System.Text.Json;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><c>data/rulebook/</c> is the whole book, and it is not <c>data/rules/</c>.</b>
///
/// <para>The two stores answer different questions and must not be confused. <c>data/rules/</c>
/// is the <em>mechanics</em> a character is built from — structured, verified entry by entry
/// against the page, and the only thing the engine reads. <c>data/rulebook/</c> is the
/// <em>text</em>, extracted from the publisher's PDF chapter by chapter, so a player can be
/// shown what a Power actually says. Nothing in the engine reads it, no cost comes from it, and
/// a disagreement between the two is always resolved in favour of <c>data/rules/</c>.</para>
///
/// <para><b>It is deliberately not part of the browser payload.</b> The web csproj copies
/// <c>data/rules</c> and <c>data/transcripts</c> into <c>wwwroot</c> and nothing else, so this
/// corpus is not served by the deployed site. That is a decision waiting on the account-gated
/// reader, not an oversight — see <c>docs/RULEBOOK-COVERAGE.md</c>.</para>
/// </summary>
public sealed class RulebookCorpusTests
{
    private static string CorpusPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    // Named Number rather than Chapter because a record member may not share its type's name;
    // the JSON key is "chapter", so it is bound explicitly.
    private sealed record Chapter(
        [property: System.Text.Json.Serialization.JsonPropertyName("chapter")] int Number,
        string Title, int[] PrintedPages, string SourceRef, Section[] Sections);

    private sealed record Section(string Heading, int PrintedPage, string Text);

    // Cached rather than constructed per call: CA1869, which is an error under
    // ContinuousIntegrationBuild and so does not show up in a local `dotnet test`.
    private static readonly JsonSerializerOptions SnakeCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static Chapter[] All() =>
        Directory.GetFiles(CorpusPath, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<Chapter>(File.ReadAllText(f), SnakeCase)
                ?? throw new InvalidOperationException($"{f} is not readable as a chapter."))
            .ToArray();

    [Fact]
    public void EveryChapterOfTheBookIsPresentAndParses()
    {
        var chapters = All();

        Assert.Equal(10, chapters.Length);
        Assert.Equal(Enumerable.Range(0, 10), chapters.Select(c => c.Number));

        Assert.All(chapters, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.NotEmpty(c.Sections);
            Assert.Contains("Ultimate Edition", c.SourceRef, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The chapters tile the book from the Introduction to the end of Chapter 9 with no gap
    /// and no overlap, apart from the one page the book itself skips between chapters. A
    /// missing range here is a chapter somebody forgot to extract, which is invisible
    /// otherwise: every individual file would still parse.
    /// </summary>
    [Fact]
    public void TheChaptersCoverThePrintedBookWithoutAGap()
    {
        var ranges = All().Select(c => (From: c.PrintedPages[0], To: c.PrintedPages[1]))
                          .OrderBy(r => r.From)
                          .ToList();

        Assert.Equal(5, ranges[0].From);
        Assert.Equal(189, ranges[^1].To);

        for (var i = 1; i < ranges.Count; i++)
            Assert.True(ranges[i].From - ranges[i - 1].To <= 2,
                $"printed pp.{ranges[i - 1].To}-{ranges[i].From} belong to no chapter file.");
    }

    /// <summary>
    /// Every section says which printed page it came from, inside its chapter's range — that
    /// is what makes a quotation checkable against the book by whoever reads it next.
    /// </summary>
    [Fact]
    public void EverySectionCarriesAPageInsideItsChapter()
    {
        Assert.All(All(), c => Assert.All(c.Sections, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Heading));
            Assert.InRange(s.PrintedPage, c.PrintedPages[0], c.PrintedPages[1]);
        }));
    }

    /// <summary>
    /// <b>The publisher's watermark is not content and must never be redistributed.</b> Every
    /// page of the PDF carries the purchaser's name and order number, and it extracts as four
    /// separate words — so a filter written against the whole phrase let all four through into
    /// the prose, which is how the first extraction shipped it into fifty chapters' worth of
    /// text before this test existed.
    /// </summary>
    [Fact]
    public void ThePurchaserWatermarkIsNotInTheCorpus()
    {
        foreach (var file in Directory.GetFiles(CorpusPath, "*.json"))
        {
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("Sheiles", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("50732985", text, StringComparison.Ordinal);
            Assert.DoesNotContain("(Order #", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A spot check that the extraction is the <em>book</em> and not a scramble: two entries
    /// whose printed text is known, on pages far apart, each read back with their stat line
    /// intact. The two-column layout is the hazard — read by baseline alone, the headings of
    /// facing entries interleave and every entry ends up carrying its neighbour's text.
    /// </summary>
    [Fact]
    public void KnownEntriesReadBackAsTheyArePrinted()
    {
        var ch2 = All().Single(c => c.Number == 2);

        var phasing = ch2.Sections.Single(s => s.Heading == "PHASING");
        Assert.Equal(36, phasing.PrintedPage);
        Assert.Contains("Self • Default Rank • 9 Hero Points", phasing.Text, StringComparison.Ordinal);
        Assert.Contains("out of phase with the physical world", phasing.Text, StringComparison.Ordinal);

        // Its facing neighbour's text must not have bled in.
        Assert.DoesNotContain("PSYCHOMETRY", phasing.Text, StringComparison.Ordinal);

        var overkill = ch2.Sections.Single(s => s.Heading == "OVERKILL");
        Assert.Equal(52, overkill.PrintedPage);
        Assert.Contains("1 Hero Point per 2 ranks", overkill.Text, StringComparison.Ordinal);

        // Both optional Trait Cap rules are printed here, which is why data/rules records them
        // against p.52 rather than against the chapter that discusses caps.
        Assert.Contains("3 ranks", overkill.Text, StringComparison.Ordinal);
        Assert.Contains("never be lower than 9d", overkill.Text, StringComparison.Ordinal);
    }
}
