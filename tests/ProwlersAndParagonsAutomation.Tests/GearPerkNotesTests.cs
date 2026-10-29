using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The Gear step says where Chapter 6 hands gear to the GM with the Perks in mind</b> — p.91
/// for mundane gear and p.92 for custom features. Words only; the Perk names and the page are
/// read from <c>gear.json</c>, so the expectations here are built from the same data.
/// </summary>
public sealed class GearPerkNotesTests(RulesFixture f) : IClassFixture<RulesFixture>
{
    private IReadOnlyList<string> Weighed => f.Rules.Equipment.EquipmentCatalogue.MundaneGear.PerksTheGmWeighs;
    private IReadOnlyList<string> Gating => f.Rules.Equipment.CustomGear.Customization!.GmMayGateFeaturesBehindPerks;

    /// <summary>Every Perk either list names is a Perk, so each line prints a name and never an id.</summary>
    [Fact]
    public void EveryPerkTheGearRulesNameIsAPerk()
    {
        Assert.NotEmpty(Weighed);
        Assert.NotEmpty(Gating);
        Assert.All(Weighed.Concat(Gating), id => Assert.NotNull(f.Rules.GetPerk(id)));
    }

    [Fact]
    public void TheMundaneLineNamesEachPerkTheGmWeighsAndCitesItsPage()
    {
        var line = GearPerkNotes.MundaneGear(f.Rules, RulesFixture.StandardSheet())!;

        Assert.All(Weighed, id => Assert.Contains(f.Rules.GetPerk(id)!.Name, line, StringComparison.Ordinal));
        Assert.Contains("(p.91)", line, StringComparison.Ordinal);
        Assert.DoesNotContain("This character has", line, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCustomFeatureLineNamesEachPerkTheGmMayAskForAndCitesItsPage()
    {
        var line = GearPerkNotes.CustomFeatures(f.Rules, RulesFixture.StandardSheet())!;

        Assert.All(Gating, id => Assert.Contains(f.Rules.GetPerk(id)!.Name, line, StringComparison.Ordinal));
        Assert.DoesNotContain(f.Rules.GetPerk("contacts")!.Name, line, StringComparison.Ordinal);
        Assert.Contains("(p.92)", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A character holding some of them is told which. The control is the same line for the same
    /// character without them, above; and a Perk neither page names is not listed.
    /// </summary>
    [Fact]
    public void ACharacterIsToldWhichOfThemItHolds()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Perks.Add(new SelectedPerk("patron", 1, "ARGUS"));
        sheet.Perks.Add(new SelectedPerk("wealth", 1, null));
        sheet.Perks.Add(new SelectedPerk("fame", 1, null));

        Assert.EndsWith(" This character has Patron and Wealth.",
            GearPerkNotes.MundaneGear(f.Rules, sheet), StringComparison.Ordinal);
        Assert.EndsWith(" This character has Patron and Wealth.",
            GearPerkNotes.CustomFeatures(f.Rules, sheet), StringComparison.Ordinal);
    }
}
