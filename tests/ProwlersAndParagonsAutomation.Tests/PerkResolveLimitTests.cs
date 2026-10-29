using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Fourth Wall earns Resolve once per scene and Great Wealth spends it once per issue</b>, and
/// the sheet's Resolve counter says so. The structured <c>resolve_limit</c> is a transcription of
/// the entry's own sentence, so these tests read the sentence back rather than asserting literals
/// beside it — prose rewritten to say "once per session" would pass a test that typed "scene".
/// </summary>
public sealed class PerkResolveLimitTests(RulesFixture f) : IClassFixture<RulesFixture>
{
    /// <summary>
    /// The limit is on exactly the Perks whose text sets one. The text side is the witness: any
    /// entry saying "once per" carries a limit, and nothing else does.
    /// </summary>
    [Fact]
    public void ALimitIsOnExactlyThePerksWhoseTextSetsOne()
    {
        var saying = f.Rules.Perks
            .Where(p => p.Description.Contains("once per ", StringComparison.Ordinal))
            .Select(p => p.Id).ToList();

        Assert.Equal(["fourth_wall", "great_wealth"], saying);
        Assert.Equal(saying, f.Rules.Perks.Where(p => p.ResolveLimit is not null).Select(p => p.Id));
    }

    /// <summary>Every field of each limit is what its entry's own sentence says.</summary>
    [Fact]
    public void EachLimitIsWhatItsEntrySays()
    {
        var limited = f.Rules.Perks.Where(p => p.ResolveLimit is not null).ToList();

        Assert.NotEmpty(limited);
        Assert.All(limited, p =>
        {
            var limit = p.ResolveLimit!;

            Assert.Contains(limit.Direction, (string[])["earn", "spend"]);
            Assert.Contains(limit.OncePer, (string[])["scene", "issue"]);
            Assert.Contains($"{limit.Direction} {limit.Points} point of Resolve", p.Description, StringComparison.Ordinal);
            Assert.Contains($"once per {limit.OncePer}", p.Description, StringComparison.Ordinal);
            Assert.Contains(limit.Occasion, p.Description, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Each limit cites the page its Perk is printed on, read out of the corpus by the Perk's own
    /// heading — Great Wealth shares one with Wealth, "WEALTH/GREAT WEALTH".
    /// </summary>
    [Fact]
    public void EachLimitCitesThePageItsPerkIsPrintedOn()
    {
        using var chapter = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "data", "rulebook", "ch02-characters.json")));
        var entries = chapter.RootElement.GetProperty("sections").EnumerateArray()
            .Where(s => s.GetProperty("text").GetString()!.StartsWith("Perk •", StringComparison.Ordinal))
            .ToList();

        var limited = f.Rules.Perks.Where(p => p.ResolveLimit is not null).ToList();
        Assert.NotEmpty(limited);
        Assert.All(limited, p =>
        {
            var printed = Assert.Single(entries, s =>
                s.GetProperty("heading").GetString()!.Split('/').Contains(p.Name.ToUpperInvariant()));
            Assert.Equal(printed.GetProperty("printed_page").GetInt32(), p.ResolveLimit!.Page);
        });
    }
}
