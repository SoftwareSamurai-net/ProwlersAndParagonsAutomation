using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Which Pros and Cons may be applied to a piece of gear.</b>
///
/// <para>This was the fall-through case: <c>ProConPicker</c> filtered Powers by Range and rank
/// type, filtered Abilities by <c>applicable_to</c>, and offered gear <em>everything</em> —
/// including the Item Con, which p.93 pointedly leaves off its list of the Cons commonly applied
/// to gear, and which the engine now refuses to credit. Marking the twenty-four the page does name
/// is what turns gear into a target like the other two.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GearProConApplicabilityTests
{
    private readonly RulesFixture _f;
    private readonly ProConApplicability _applicability;

    public GearProConApplicabilityTests(RulesFixture fixture)
    {
        _f = fixture;
        _applicability = new ProConApplicability(_f.Rules);
    }

    /// <summary>
    /// <b>The marked options are p.93's twenty-four and nothing else</b>, read off the page's own
    /// list in <c>gear.json</c> rather than out of a list retyped here.
    ///
    /// <para><b>One printed name needs a mapping and it lives in the test, not the data.</b> The
    /// page writes "Area of Effect" where <c>pros.json</c> carries <c>area_burst</c>, whose printed
    /// name is "Area / Burst (Area of Effect)". <c>zone_nova</c> is the other half of the same
    /// printed Pro and is deliberately <em>not</em> marked: the Weapon Features glossary's own
    /// Area/Burst entry defers to <c>area_burst</c> by id, and that is the only place in the book
    /// where this Pro and a piece of gear meet. The rival reading — that "Area of Effect" names the
    /// whole Pro and reaches both grades — is not refuted by anything printed, and p.93 calls its
    /// list "not exhaustive" and puts everything under GM approval, so it is a widening somebody
    /// can make here in one line if a table wants it.</para>
    /// </summary>
    [Fact]
    public void TheOptionsMarkedForGearArePageNinetyThreesTwentyFour()
    {
        var printed = _f.Rules.Equipment.GearProsAndCons.GearProsAndCons!.CommonlyApplied;

        var marked = _f.Rules.Pros.Where(p => p.ApplicableTo.Contains(ProConApplicability.GearTarget, StringComparer.Ordinal))
            .Select(p => p.Name)
            .Concat(_f.Rules.Cons.Where(c => c.ApplicableTo.Contains(ProConApplicability.GearTarget, StringComparer.Ordinal))
                .Select(c => c.Name))
            .ToList();

        Assert.Equal(printed.Count, marked.Count);

        foreach (var name in printed)
        {
            // "Area of Effect" is the parenthetical inside "Area / Burst (Area of Effect)"; every
            // other printed name is the entry's whole name.
            Assert.True(marked.Any(m => m.Contains(name, StringComparison.Ordinal)),
                $"p.93 names '{name}' for gear and no marked Pro or Con carries that name. "
                + $"Marked: {string.Join(", ", marked)}");
        }

        // The one the page leaves off, which is the whole argument for not crediting it.
        Assert.DoesNotContain(ProConApplicability.GearTarget,
            _f.Rules.GetCon("item")!.ApplicableTo, StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>The picker's list for gear: p.93's options, less the two that cannot be priced on
    /// something rankless.</b>
    ///
    /// <para>Overkill and Weak are on p.93's list and are marked, because the data transcribes the
    /// page. They are not offered, because Ch.2 defines both as a change to a Power's cost per rank
    /// and gear has no rank — <c>CostCalculator</c> has always answered 0 for either. The rule is
    /// asked of <c>cost_type</c> rather than of two ids, so a third option priced that way would be
    /// caught by the same sentence.</para>
    /// </summary>
    [Fact]
    public void TheGearOfferDropsTheItemConAndTheTwoRateReductions()
    {
        var offered = _applicability.ProsForGear().Select(p => p.Id)
            .Concat(_applicability.ConsForGear().Select(c => c.Id))
            .ToList();

        Assert.DoesNotContain("item", offered, StringComparer.Ordinal);
        Assert.DoesNotContain("overkill", offered, StringComparer.Ordinal);
        Assert.DoesNotContain("weak", offered, StringComparer.Ordinal);

        // Two named for gear that *are* offered, one Pro and one Con — so this is a filter rather
        // than an empty list, and the two halves both answer.
        Assert.Contains("armor_piercing", offered, StringComparer.Ordinal);
        Assert.Contains("burnout", offered, StringComparer.Ordinal);

        // p.93 names twenty-four; two of them are the rate reductions.
        var printed = _f.Rules.Equipment.GearProsAndCons.GearProsAndCons!.CommonlyApplied;
        Assert.Equal(printed.Count - 2, offered.Count);
    }

    /// <summary>
    /// <b>It is a filter, not the whole list.</b> Before this, gear fell through to every Pro and
    /// Con there is — which is how the Item Con came to be offered on a sword.
    /// </summary>
    [Fact]
    public void GearIsOfferedFewerOptionsThanEverythingThereIs()
    {
        Assert.True(_applicability.ProsForGear().Count < _f.Rules.Pros.Count);
        Assert.True(_applicability.ConsForGear().Count < _f.Rules.Cons.Count);

        // And not so few that the picker is useless — the page names two dozen for a reason.
        Assert.NotEmpty(_applicability.ProsForGear());
        Assert.NotEmpty(_applicability.ConsForGear());
    }

    /// <summary>
    /// <b>Marking gear did not widen what a Power or an Ability is offered.</b> The three targets
    /// read three different things off the same entry, and a mark added for one of them must not
    /// leak into another — the failure the removed <c>available_pros</c> lists were.
    /// </summary>
    [Fact]
    public void TheAbilityAndPowerOffersAreUnchangedByTheGearMark()
    {
        // Exactly two Cons name Abilities and no Pro does — the figures the picker's own comment
        // states, asserted here because the gear mark went into the same field.
        Assert.Equal(2, _f.Rules.Cons.Count(c => c.ApplicableTo.Contains("abilities", StringComparer.Ordinal)));
        Assert.DoesNotContain(_f.Rules.Pros, p => p.ApplicableTo.Contains("abilities", StringComparer.Ordinal));

        // And a Self-range Power is still refused the Ranged Pro, which is the Range rule the gear
        // mark travels beside and must not have disturbed.
        var forceField = _f.Rules.GetPower("force_field")!;
        var blast = _f.Rules.GetPower("blast")!;

        Assert.Contains(_applicability.ProsFor(blast), p => p.Id == "ranged" || p.Id == "penetrating");
        Assert.DoesNotContain(_applicability.ProsFor(forceField), p => p.Id == "close");
    }
}
