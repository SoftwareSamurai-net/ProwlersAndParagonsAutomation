namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Descriptions are what a player reads when choosing a Power, so they have to agree
/// with the mechanics the same entry declares.
///
/// <para>The history this guards: the original descriptions were invented rather than
/// taken from the rulebook, and 44 of the 46 rankless Powers described per-rank scaling
/// that does not exist.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerDescriptionTests
{
    private readonly RulesFixture _f;

    public PowerDescriptionTests(RulesFixture fixture) => _f = fixture;

    /// <summary>Phrases that only make sense for a Power that actually has ranks.</summary>
    private static readonly string[] PerRankClaims =
    [
        "per rank", "each rank", "every rank", "rank determines", "higher ranks",
        "at lower ranks", "ranks allow", "per additional rank"
    ];

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void DescriptionIsVerifiedAgainstTheRulebook(string id) =>
        Assert.True(_f.Rules.GetPower(id)!.DescriptionVerified,
            $"Power '{id}' description is not marked verified.");

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void DescriptionSaysSomethingUseful(string id)
    {
        var description = _f.Rules.GetPower(id)!.Description;

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.True(description.Length >= 25,
            $"Power '{id}' description is too short to help a player choose: '{description}'");
        Assert.EndsWith(".", description.TrimEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void RanklessPowersDoNotDescribePerRankScaling(string id)
    {
        var power = _f.Rules.GetPower(id)!;
        if (power.RankType is not ("default" or "special")) return;

        // "Its baseline is half your Might" and similar are fine on ranked Powers, but a
        // Power with no rank cannot have anything scale with one.
        var offending = PerRankClaims
            .Where(claim => power.Description.Contains(claim, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offending.Count == 0,
            $"Power '{id}' has no rank ({power.RankType}) but its description says " +
            $"\"{string.Join("\", \"", offending)}\": {power.Description}");
    }

    [Fact]
    public void DescriptionsAreDistinct()
    {
        // Two Powers sharing wording is a copy-paste slip, not a real duplicate.
        var duplicates = _f.Rules.Powers
            .GroupBy(p => p.Description, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(p => p.Id)));

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryPowerCitesThePageItWasCheckedAgainst()
    {
        var uncited = _f.Rules.Powers
            .Where(p => string.IsNullOrWhiteSpace(p.SourceRef))
            .Select(p => p.Id);

        Assert.Empty(uncited);
    }

    [Fact]
    public void PowersWithNotesExplainSomethingTheFieldsCannot()
    {
        // Notes carry the rules a structured field cannot hold — Boost's mirrored rate,
        // Summoning's Threat formula. Every special or variable-cost Power needs one.
        var needExplaining = _f.Rules.Powers
            .Where(p => p.CostType is "special" or "per_rank_variable" or "flat_variable")
            .Where(p => string.IsNullOrWhiteSpace(p.Notes))
            .Select(p => p.Id);

        Assert.Empty(needExplaining);
    }
}
