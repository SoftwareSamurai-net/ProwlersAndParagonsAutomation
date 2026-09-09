using ProwlersAndParagonsAutomation.Cli;
using ProwlersAndParagonsAutomation.Cli.Steps;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The terminal wizard's Vehicles &amp; bases step, as far as a test can reach it.</b>
///
/// <para><b>What this can and cannot see, said plainly.</b> The wizard has no harness — every step
/// drives <c>AnsiConsole</c> prompts directly — so the menu and the six prompts are unreachable
/// from here, exactly as they are for the other six steps. What <em>is</em> reachable is the line
/// each row is offered under, which is the one place this step decides anything rather than
/// plumbing it: <b>the price has to carry its currency</b>, because this screen shows Hero Point
/// figures too and a bare number beside them is the category error the whole chapter is careful
/// about.</para>
///
/// <para>So this checks the thing worth checking and is not a claim that the step is covered.
/// <c>AssetStepTests</c> in the browser suite drives the equivalent surface end to end.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ChooseAssetsStepTests
{
    private readonly RulesFixture _f;

    public ChooseAssetsStepTests(RulesFixture fixture) => _f = fixture;

    private AssetCatalogueRow Row(string id) => _f.Rules.Assets.Find(id)!;

    /// <summary>
    /// <b>Every feature line names its currency, in all three price shapes.</b> A vehicle feature
    /// is Vehicle Points and a base feature is Base Points, and the two tables share ten names
    /// between them — so a line without the unit would be ambiguous even to somebody holding the
    /// book. <b>Singular where the figure is one</b>, on the rule every message in this app follows:
    /// "1 Vehicle Points" reads as a form field rather than as a sentence.
    /// </summary>
    [Fact]
    public void EveryFeatureLineNamesItsCurrency()
    {
        Assert.Equal("Flight — 2 Vehicle Points",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.VehicleFeaturePrefix + "flight")));

        Assert.Equal("Passengers — 1 Vehicle Point per 4 extra passengers",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.VehicleFeaturePrefix + "passengers")));

        Assert.Equal("Hidden Compartments — 1-2 Vehicle Points",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.VehicleFeaturePrefix + "hidden_compartments")));

        Assert.Equal("Size — 1-3 Base Points",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.BaseFeaturePrefix + "size")));

        Assert.Equal("Hidden — 1 Base Point",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.BaseFeaturePrefix + "hidden")));
    }

    /// <summary>
    /// <b>A feature the page limits to one kind of vehicle says so on its own line.</b> Mecha and
    /// Giant are ground vehicles only; a reader choosing from a fifteen-row page cannot be expected
    /// to remember which two.
    /// </summary>
    [Fact]
    public void ARestrictedFeatureSaysWhatItIsRestrictedTo()
    {
        Assert.Equal("Giant — -6 Vehicle Points · ground vehicles only",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.VehicleFeaturePrefix + "giant")));

        // The control: an unrestricted feature carries no such clause, so the phrase above is
        // about this feature rather than printed on every one of them.
        Assert.DoesNotContain("only",
            ChooseAssetsStep.FeatureLabel(Row(AssetCatalogue.VehicleFeaturePrefix + "flight")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A stock vehicle is offered with its four ranks and its printed total.</b> Copying one is
    /// how somebody starts, and the total is what tells them whether their Perk can afford it.
    /// </summary>
    [Fact]
    public void AStockVehicleIsOfferedWithItsRanksAndItsPrintedTotal()
    {
        var line = ChooseAssetsStep.StockLabel(Row(AssetCatalogue.StockPrefix + "helicopter"));

        Assert.Equal("Helicopter — 25 Vehicle Points · Body 7d · Speed 7d · Control 3 · unarmed", line);

        // An armed machine says what it is armed with, so "unarmed" above is a fact about the
        // Helicopter rather than a word on every row.
        Assert.Contains("Weapons 12d",
            ChooseAssetsStep.StockLabel(Row(AssetCatalogue.StockPrefix + "jet_fighter")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The step is in the wizard, between Gear and the derived stats.</b> A step written and not
    /// wired in is a screen nobody can reach, which no test of its labels would notice.
    /// </summary>
    [Fact]
    public void TheStepIsInTheWizardAfterGear()
    {
        var wizard = new WizardOrchestrator(
            _f.Rules, _f.Costs, _f.Derived, _f.Validator, RulesFixture.RepoRoot);

        Assert.Equal(
            ["choose_tier", "buy_characteristics", "choose_gear", "choose_assets",
             "calculate_derived", "finishing_touches", "gm_review"],
            wizard.StepIds);
    }
}
