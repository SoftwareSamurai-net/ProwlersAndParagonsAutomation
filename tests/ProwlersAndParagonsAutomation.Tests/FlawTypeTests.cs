using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Checks flaws.json's flaw_type against <see cref="CanonicalFlawTypes"/> — transcribed from
/// each Flaw's own entry in Chapter 2 (pp.56-60) — and that the type is wired to the thing it
/// actually controls: <see cref="DerivedStatsCalculator.CalculateResolve"/>'s +1-per-Condition-
/// or-Plot-Hook rule.
///
/// <para>Before this file, only four of the 53 flaw_type values were pinned to the book at all
/// (<c>RulesDataTests.TheNamedPlotHooksAndConditionsAreClassifiedThatWay</c>, the four examples
/// the rulebook itself names when defining the two exceptions). The other 49 were checked only
/// for being one of the four known type strings — a retype from "condition" to "regular" left
/// every test green while silently changing that Flaw's contribution to Resolve.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class FlawTypeTests
{
    private readonly RulesFixture _f;

    public FlawTypeTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string> AllFlawIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in CanonicalFlawTypes.All) data.Add(e.Id);
        return data;
    }

    // ── Coverage: the transcription cannot quietly cover a subset ──────────────

    [Fact]
    public void EveryRulebookFlawIsTranscribedAndNothingExtraIs()
    {
        var expected = CanonicalFlawTypes.All.Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal);
        var actual   = _f.Rules.Flaws.Select(fl => fl.Id).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheTranscriptionCovers53Flaws() => Assert.Equal(53, CanonicalFlawTypes.All.Count);

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllFlawIds))]
    public void FlawTypeMatchesItsRulebookEntry(string id)
    {
        var expected = CanonicalFlawTypes.All.Single(e => e.Id == id);
        var flaw     = _f.Rules.GetFlaw(id);

        Assert.NotNull(flaw);
        Assert.Equal(expected.FlawType, flaw.FlawType);
    }

    // ── The consequence: flaw_type actually moves Resolve ──────────────────────

    /// <summary>
    /// <b>Positive control paired with the assertion below</b>: this proves a flaw of each of
    /// the four known types really does move <c>CalculateResolve</c> by the amount its type
    /// implies, for every one of the 53 flaws — not just the two the pre-existing
    /// <c>DerivedStatsCalculatorTests.ConditionAndPlotHookFlawsEachGrantOneResolve</c> sampled.
    /// A flaw retyped away from "regular" without this test would still fail
    /// <see cref="FlawTypeMatchesItsRulebookEntry"/> above, but that only proves the data
    /// disagrees with the book — this proves the disagreement would actually cost or gain the
    /// character a point of Resolve, which is the reason the field matters at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFlawIds))]
    public void EachFlawsResolveContributionMatchesItsType(string id)
    {
        var baseline = RulesFixture.StandardSheet();
        var withFlaw = RulesFixture.StandardSheet();
        withFlaw.Flaws.Add(new SelectedFlaw(id));

        var before = _f.Derived.CalculateResolve(baseline);
        var after  = _f.Derived.CalculateResolve(withFlaw);

        var expected = CanonicalFlawTypes.All.Single(e => e.Id == id);
        var grantsResolve = expected.FlawType is "condition" or "plot_hook" or "plot_hook_and_condition";

        Assert.Equal(grantsResolve ? before + 1 : before, after);
    }
}
