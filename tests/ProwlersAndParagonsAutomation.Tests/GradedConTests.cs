namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Checks cons.json's variant-to-value mapping for the four graded Cons — Conditional,
/// Limited, Shutdown, Side Effect — against <see cref="CanonicalGradedCons"/>, transcribed
/// from each Con's own grading sentence in Chapter 2 (pp.48-54).
///
/// <para>Before this file, <c>RulesDataTests.GradedConsRunFromMinusOneToMinusFour</c> checked
/// only that each of the four carried the values {-1, -2, -4} as a set — it sorted them before
/// comparing, so it cannot tell "somewhat_limited": -1 from "somewhat_limited": -4. Swapping
/// which variant key maps to which value passes that test and prices a character exactly
/// backwards on whichever grade was swapped, with nothing in the suite noticing.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GradedConTests
{
    private readonly RulesFixture _f;

    public GradedConTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string> AllGradedConIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in CanonicalGradedCons.All) data.Add(e.Id);
        return data;
    }

    // ── Coverage ─────────────────────────────────────────────────────────────

    [Fact]
    public void TheTranscriptionCoversAllFourGradedCons() =>
        Assert.Equal(4, CanonicalGradedCons.All.Count);

    [Fact]
    public void TheFourGradedConsAreExactlyTheOnesThisTranscribesNothingMore()
    {
        // The set RulesDataTests.GradedConsRunFromMinusOneToMinusFour already exercises.
        string[] expected = ["conditional", "limited", "shutdown", "side_effect"];
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
                     CanonicalGradedCons.All.Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal));
    }

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllGradedConIds))]
    public void EachGradeMapsToTheValuePrintedForIt(string id)
    {
        var expected = CanonicalGradedCons.All.Single(e => e.Id == id);
        var con      = _f.Rules.GetCon(id);

        Assert.NotNull(con);
        Assert.NotNull(con.CostModifierRange);

        Assert.Equal(expected.Grades.OrderBy(kv => kv.Key, StringComparer.Ordinal),
                     con.CostModifierRange.OrderBy(kv => kv.Key, StringComparer.Ordinal));
    }
}
