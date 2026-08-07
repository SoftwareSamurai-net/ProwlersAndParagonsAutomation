using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Which generic Pros and Cons may be applied to a Power.
///
/// <para>The rulebook states this inside each option's own entry, never as a list on the
/// Power ("This Pro applies to Zone Powers", "applies to Powers that only affect you"). The
/// constraints below are transcribed from Ch.2 pp.48–53 — if one of these fails, check the
/// page, do not edit the expectation to match the code.</para>
///
/// <para>Only the constraints the rulebook prints for every Power are enforced: its Range
/// (Self, Touch, Ranged, Zone, Special — p.19) and its Rank type. The rest travel as a
/// caveat on the option; see <see cref="AnUncheckableConstraintIsCarriedAsACaveat"/>.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ProConApplicabilityTests
{
    private readonly RulesFixture _f;
    private readonly ProConApplicability _sut;

    public ProConApplicabilityTests(RulesFixture fixture)
    {
        _f   = fixture;
        _sut = new ProConApplicability(fixture.Rules);
    }

    private PowerModel Power(string id) => _f.Rules.GetPower(id)!;

    // ── The constraints, as the rulebook states them ─────────────────────────

    /// <summary>
    /// Every range constraint the generic entries state, with the Ranges each permits.
    /// Ch.2 p.19 defines the five: Self affects you, Touch reaches Close Range, Ranged
    /// reaches Distant Range, Zone is the area option, Special varies.
    /// </summary>
    public static TheoryData<string, string, string[]> RangeConstraints() => new()
    {
        // id,             kind,  permitted base Ranges
        { "close",         "pro", ["touch"] },                    // "+2 Pro when applied to Touch Powers"
        { "close",         "con", ["ranged"] },                   // "-2 Con when applied to Ranged Powers"
        { "ranged",        "pro", ["touch", "zone"] },            // priced from Touch; Zone addressed explicitly
        { "touch",         "con", ["ranged"] },                   // "-4 Con ... Powers used at Distant Range"
        { "line_of_sight", "pro", ["touch", "ranged", "zone"] },  // priced from Touch/Distant; Zone addressed
        { "expansive",     "pro", ["zone"] },                     // "This Pro applies to Zone Powers"
        { "imbue",         "pro", ["self"] },                     // "Powers that only affect you"
        { "area_burst",    "pro", ["ranged"] },                   // "These Pros apply to Ranged Powers"
        { "zone_nova",     "pro", ["ranged", "touch"] },          // "Ranged Powers and Touch Powers"
        { "ricochet",      "pro", ["ranged"] },                   // "applies to ranged attack Powers"
    };

    [Theory]
    [MemberData(nameof(RangeConstraints))]
    public void RangeConstraintsMatchTheRulebook(string id, string kind, string[] ranges)
    {
        IGenericProCon option = kind == "pro" ? _f.Rules.GetPro(id)! : _f.Rules.GetCon(id)!;

        Assert.Equal(ranges.Order(), option.AppliesToRanges.Order());
    }

    /// <summary>Degrades is the only entry that constrains on Rank rather than Range.</summary>
    [Fact]
    public void DegradesAppliesToRankedPowersOnly()
    {
        var degrades = _f.Rules.GetCon("degrades")!;

        Assert.Equal<IEnumerable<string>>(["baseline", "power"], degrades.AppliesToRankTypes.Order());

        Assert.True(ProConApplicability.IsApplicable(degrades, Power("blast")));       // Power Rank
        Assert.True(ProConApplicability.IsApplicable(degrades, Power("strike")));      // Baseline Rank
        Assert.False(ProConApplicability.IsApplicable(degrades, Power("adaptation"))); // Default Rank
    }

    /// <summary>
    /// Exactly these entries constrain on something checkable. Everything else states no
    /// Range or Rank condition and so applies to any Power — pinned so an entry cannot
    /// quietly acquire or lose a constraint.
    /// </summary>
    [Fact]
    public void OnlyTheTranscribedEntriesAreConstrained()
    {
        var constrained = _f.Rules.Pros
            .Where(p => p.AppliesToRanges.Count > 0 || p.AppliesToRankTypes.Count > 0)
            .Select(p => $"pro:{p.Id}")
            .Concat(_f.Rules.Cons.Where(c => c.AppliesToRanges.Count > 0 || c.AppliesToRankTypes.Count > 0)
                .Select(c => $"con:{c.Id}"))
            .Order()
            .ToList();

        Assert.Equal<IEnumerable<string>>(
            ["con:close", "con:degrades", "con:touch",
             "pro:area_burst", "pro:close", "pro:expansive", "pro:imbue",
             "pro:line_of_sight", "pro:ranged", "pro:ricochet", "pro:zone_nova"],
            constrained);
    }

    // ── Applying them ────────────────────────────────────────────────────────

    [Fact]
    public void AnOptionWithNoStatedConstraintAppliesToEveryPower()
    {
        // Burnout's entry names no Range or Rank condition at all.
        var burnout = _f.Rules.GetCon("burnout")!;

        Assert.All(_f.Rules.Powers, p =>
            Assert.True(ProConApplicability.IsApplicable(burnout, p), $"burnout rejected on {p.Id}"));
    }

    /// <summary>
    /// The regression this whole change exists for. The Ranged Pro raises a Touch Power to
    /// Distant Range; a Self Power affects only you, so there is nothing to raise. Six
    /// Powers used to offer it anyway because the old hand-written lists said so.
    /// </summary>
    [Theory]
    [InlineData("animal_empathy")]
    [InlineData("boost")]
    [InlineData("communications")]
    [InlineData("leadership")]
    [InlineData("ventriloquism")]
    [InlineData("weakness_detection")]
    public void TheRangedProIsNotOfferedOnASelfPower(string powerId)
    {
        var power = Power(powerId);

        Assert.Equal("self", power.Range);
        Assert.DoesNotContain(_sut.ProsFor(power), p => p.Id == "ranged");
    }

    [Fact]
    public void TheTouchConIsNotOfferedOnASelfPower() =>
        Assert.DoesNotContain(_sut.ConsFor(Power("teleportation")), c => c.Id == "touch");

    [Fact]
    public void ExpansiveIsOfferedOnZonePowersAndNoOthers()
    {
        foreach (var power in _f.Rules.Powers.Where(p => p.Range is not "special"))
        {
            var offered = _sut.ProsFor(power).Any(p => p.Id == "expansive");
            Assert.Equal(power.Range == "zone", offered);
        }
    }

    [Fact]
    public void ImbueIsOfferedOnSelfPowersAndNoOthers()
    {
        foreach (var power in _f.Rules.Powers.Where(p => p.Range is not "special"))
        {
            var offered = _sut.ProsFor(power).Any(p => p.Id == "imbue");
            Assert.Equal(power.Range == "self", offered);
        }
    }

    /// <summary>
    /// A Range of Special "works in some unique way discussed in the description" (p.19),
    /// so nothing can be ruled out for it and every option stays available.
    /// </summary>
    [Fact]
    public void ASpecialRangePowerIsOfferedEveryRangeConstrainedOption()
    {
        var special = _f.Rules.Powers.Where(p => p.Range == "special").ToList();
        Assert.NotEmpty(special);

        foreach (var power in special)
        {
            Assert.Equal(_f.Rules.Pros.Count, _sut.ProsFor(power).Count);

            // Degrades still applies on Rank type, which Special range does not excuse.
            var expectedCons = _f.Rules.Cons.Count(c =>
                c.AppliesToRankTypes.Count == 0 || c.AppliesToRankTypes.Contains(power.RankType));
            Assert.Equal(expectedCons, _sut.ConsFor(power).Count);
        }
    }

    // ── Caveats ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The constraints that are not enforced. "Powers that inflict physical or energy
    /// damage" cannot be decided from anything the rulebook prints per Power, and deciding
    /// it here would mean inventing the data the old lists were made of. Ch.2 calls the
    /// list "not intended to cover every possible option" and leaves it under GM approval,
    /// so the condition is carried to the player instead of being guessed at.
    /// </summary>
    [Theory]
    [InlineData("armor_piercing", "pro")]
    [InlineData("penetrating", "pro")]
    [InlineData("ongoing", "pro")]
    [InlineData("carrier_attack", "pro")]
    [InlineData("resisted", "pro")]
    [InlineData("affect_inanimate", "pro")]
    [InlineData("only_inanimate", "con")]
    [InlineData("constant", "con")]
    [InlineData("uncontrolled", "con")]
    [InlineData("sustained", "con")]
    public void AnUncheckableConstraintIsCarriedAsACaveat(string id, string kind)
    {
        IGenericProCon option = kind == "pro" ? _f.Rules.GetPro(id)! : _f.Rules.GetCon(id)!;

        Assert.False(string.IsNullOrWhiteSpace(option.ApplicabilityCaveat));
        Assert.EndsWith(".", option.ApplicabilityCaveat!.TrimEnd(), StringComparison.Ordinal);

        // A caveat is a note to the player, never a silent filter.
        Assert.All(_f.Rules.Powers, p =>
            Assert.True(ProConApplicability.IsApplicable(option, p) ||
                        option.AppliesToRanges.Count > 0 || option.AppliesToRankTypes.Count > 0));
    }

    [Fact]
    public void NoPowerCarriesAnInventedProOrConList()
    {
        // The old available_pros / available_cons fields are gone from the data. Nothing
        // deserialises them any more, so this guards the JSON rather than the model.
        var json = File.ReadAllText(Path.Combine(RulesFixture.DataPath, "powers.json"));

        Assert.DoesNotContain("available_pros", json, StringComparison.Ordinal);
        Assert.DoesNotContain("available_cons", json, StringComparison.Ordinal);
    }
}
