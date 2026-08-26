namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Checks powers.json's Prerequisite (relationship, Ability, Powers, FixedValue) for every
/// baseline-rank Power against <see cref="CanonicalPowerBaselines"/>, transcribed from each
/// Power's own printed "Baseline Rank (X)" stat line in Chapter 2.
///
/// <para>Before this file, <c>PowerDataTests.PrerequisiteRelationshipIsOneTheCalculatorHandles</c>
/// checked only that the relationship string was one the calculator recognizes and, for
/// <c>baseline_equal</c>/<c>baseline_half</c>, that the named Ability resolved to a real
/// Ability — never that it was the Ability the book actually names. Swapping Armor's baseline
/// from Toughness to, say, Willpower changes every character with the Power and nothing here
/// would have noticed: the id still resolves, so the old test still passes.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerBaselineTests
{
    private readonly RulesFixture _f;

    public PowerBaselineTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string> AllBaselinePowerIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in CanonicalPowerBaselines.All) data.Add(e.PowerId);
        return data;
    }

    // ── Coverage: the transcription cannot quietly cover a subset ──────────────

    [Fact]
    public void EveryBaselinePowerIsTranscribedAndNothingExtraIs()
    {
        var expected = CanonicalPowerBaselines.All.Select(e => e.PowerId).OrderBy(x => x, StringComparer.Ordinal);
        var actual   = _f.Rules.Powers.Where(p => p.RankType == "baseline")
                                       .Select(p => p.Id)
                                       .OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheTranscriptionCoversAllTwentySevenBaselinePowers() =>
        Assert.Equal(27, CanonicalPowerBaselines.All.Count);

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllBaselinePowerIds))]
    public void BaselinePrerequisiteMatchesItsRulebookStatLine(string id)
    {
        var expected = CanonicalPowerBaselines.All.Single(e => e.PowerId == id);
        var power    = _f.Rules.GetPower(id);

        Assert.NotNull(power);
        var prerequisite = power.Prerequisite;
        Assert.NotNull(prerequisite);

        Assert.Equal(expected.Relationship, prerequisite.Relationship);
        Assert.Equal(expected.Ability, prerequisite.Ability);
        Assert.Equal(expected.FixedValue, prerequisite.FixedValue);
        Assert.Equal(expected.Powers.OrderBy(x => x, StringComparer.Ordinal),
                     prerequisite.Powers.OrderBy(x => x, StringComparer.Ordinal));
    }
}
