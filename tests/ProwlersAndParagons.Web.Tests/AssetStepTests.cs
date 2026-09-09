using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Services;
using Assets = ProwlersAndParagonsAutomation.Web.Pages.Assets;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The Vehicles &amp; bases step, in the browser.</b>
///
/// <para>Rendered against the shipped <c>vehicles.json</c> and <c>headquarters.json</c>. What
/// these are mostly about is that <b>the page decides nothing</b>: every figure it prints comes
/// off <c>CostCalculator</c>, and every list it offers comes off <c>AssetCatalogue</c>. A number
/// computed here would be the browser deciding a rule.</para>
/// </summary>
public sealed class AssetStepTests
{
    private static RenderContext Ctx() => new(storesForReal: true);

    private static void Click(IRenderedComponent<Assets> page, string text) =>
        page.FindAll("button")
            .First(b => b.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();

    /// <summary>
    /// The four panels all carry a button reading "Add", so the label is what tells them apart —
    /// a <c>TextContent</c> match on this page always found the vehicles' one.
    /// </summary>
    private static void ClickLabelled(IRenderedComponent<Assets> page, string label) =>
        page.Find($"button[aria-label='{label}']").Click();

    /// <summary>
    /// <b>Naming a vehicle adds it and opens it, and nothing about it is bought yet.</b> The four
    /// characteristics start at zero because p.96 does: a machine with a rank nobody paid for
    /// would be a Perk that quietly bought more than it says.
    /// </summary>
    [Fact]
    public void NamingAVehicleAddsAnEmptyMachineBoughtFromZero()
    {
        using var ctx = Ctx();
        var page = ctx.Render<Assets>();

        page.Find("input[aria-label='New vehicle']").Input("The Wing");
        ClickLabelled(page, "Add vehicle");

        var machine = Assert.Single(ctx.Session.Sheet.Vehicles);
        Assert.Equal("The Wing", machine.Name);
        Assert.Equal(0, machine.Body);
        Assert.Equal(0, machine.Speed);
        Assert.Equal(0, machine.Control);
        Assert.Null(machine.Weapons);
        Assert.Empty(machine.Features);

        // And it costs the character nothing until a Hero Point goes on the Perk.
        Assert.Equal(0, ctx.Session.Costs.TotalAssetPerkCost(ctx.Session.Sheet));
    }

    /// <summary>
    /// <b>A feature added on this page is priced by the engine, in Vehicle Points.</b> The panel
    /// heading is the spend against the budget, and both figures are asked of
    /// <c>CostCalculator</c> — a page that added the numbers itself would be a second calculator
    /// to disagree with the first.
    /// </summary>
    [Fact]
    public void AFeatureIsAddedAndPricedInTheSecondCurrency()
    {
        using var ctx = Ctx();
        ctx.Session.Sheet.Vehicles.Add(new OwnedVehicle("The Wing") { PerkHeroPoints = 1 });

        var page = ctx.Render<Assets>();
        Click(page, "Edit");

        page.FindAll(".machine .options .option")
            .First(o => o.TextContent.Contains("Sensors", StringComparison.Ordinal))
            .Click();

        var machine = Assert.Single(ctx.Session.Sheet.Vehicles);
        Assert.Equal("sensors", Assert.Single(machine.Features).FeatureId);

        // Ten Vehicle Points of a twenty-five point budget — the engine's figures, printed.
        Assert.Equal(10, ctx.Session.Costs.VehiclePointsSpent(machine));
        Assert.Contains("10/25 VP", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A graded base feature goes on at its cheapest grade rather than at none.</b>
    /// <c>CostCalculator</c> throws rather than guessing a grade, so a feature added without one
    /// would take the whole page down — and the cheapest is the honest default to offer, the same
    /// answer the Gear step gives.
    /// </summary>
    [Fact]
    public void AGradedBaseFeatureArrivesAtItsCheapestGrade()
    {
        using var ctx = Ctx();
        ctx.Session.Sheet.Headquarters.Add(new OwnedHeadquarters("The Loft") { PerkHeroPoints = 2 });

        var page = ctx.Render<Assets>();
        Click(page, "Edit");

        page.FindAll(".base .options .option")
            .First(o => o.TextContent.Contains("Science Labs", StringComparison.Ordinal))
            .Click();

        var feature = Assert.Single(Assert.Single(ctx.Session.Sheet.Headquarters).Features);
        Assert.Equal("science_labs", feature.FeatureId);
        Assert.Equal("standard", feature.GradeKey);

        // The point of the default: the base can be priced at all.
        Assert.Equal(1, ctx.Session.Costs.BasePointsSpent(ctx.Session.Sheet.Headquarters[0]));
    }

    /// <summary>
    /// <b>Copying a stock vehicle fills in the numbers and buys nothing.</b> p.96 prints its six
    /// machines as worked examples with their totals; copying one is how somebody starts.
    ///
    /// <para><b>The Helicopter comes out at its printed twenty-five</b>, which is the check worth
    /// making here rather than a field-by-field comparison: the page copies a feature <em>line</em>
    /// — "Passengers 4" is not a bare feature name — and getting that reading wrong would leave
    /// every field right and the total wrong.</para>
    /// </summary>
    [Fact]
    public void CopyingAStockVehicleFillsInItsPrintedNumbers()
    {
        using var ctx = Ctx();
        ctx.Session.Sheet.Vehicles.Add(new OwnedVehicle("The Wing") { PerkHeroPoints = 1 });

        var page = ctx.Render<Assets>();
        Click(page, "Edit");

        page.FindAll(".machine .options .option")
            .First(o => o.TextContent.Contains("Helicopter", StringComparison.Ordinal))
            .Click();

        var machine = Assert.Single(ctx.Session.Sheet.Vehicles);

        // The name is the reader's, not the row's: this is their machine now.
        Assert.Equal("The Wing", machine.Name);
        Assert.Equal(7, machine.Body);
        Assert.Equal(7, machine.Speed);
        Assert.Equal(3, machine.Control);
        Assert.Null(machine.Weapons);

        var printed = ctx.Session.Assets.StockVehicles.Single(v => v.Name == "Helicopter");
        Assert.Equal(printed.VehiclePoints, ctx.Session.Costs.VehiclePointsSpent(machine));
    }

    /// <summary>
    /// <b>A Gadget opens at the minimum Complexity and shows a pool that was paid out.</b> Opening
    /// at zero would greet somebody with a validation finding for pressing Add, which is the app
    /// telling them off for using it.
    /// </summary>
    [Fact]
    public void AGadgetOpensAtTheMinimumComplexityAndCostsTheCharacterNothing()
    {
        using var ctx = Ctx();
        var before = ctx.Session.Costs.TotalCost(ctx.Session.Sheet);

        var page = ctx.Render<Assets>();
        page.Find("input[aria-label='New Gadget']").Input("Freeze Ray");
        ClickLabelled(page, "Add Gadget");

        var gadget = Assert.Single(ctx.Session.Sheet.Gadgets);
        Assert.Equal(ctx.Session.Assets.MinimumGadgetComplexity, gadget.Complexity);

        Assert.Contains("Hero Points the build paid out", page.Markup, StringComparison.Ordinal);
        Assert.Equal(before, ctx.Session.Costs.TotalCost(ctx.Session.Sheet));
    }

    /// <summary>
    /// <b>A finding about a machine lands on that machine's row.</b> The engine decides; the page
    /// only reads back what it already said.
    /// </summary>
    [Fact]
    public void AFindingAboutAVehicleIsShownOnItsOwnRow()
    {
        using var ctx = Ctx();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.Vehicles.Add(new OwnedVehicle("The Barge")
        {
            PerkHeroPoints = 1, Body = 40
        });
        ctx.Session.Sheet.Vehicles.Add(new OwnedVehicle("The Dinghy") { PerkHeroPoints = 1 });

        var page = ctx.Render<Assets>();

        // The finding sits on the list item beside the row rather than inside it — see
        // ChosenRow, which moved the flex layout onto a wrapper so a finding could stack under it.
        var rows = page.FindAll(".chosen > li");
        var barge = rows.First(r => r.TextContent.Contains("The Barge", StringComparison.Ordinal));
        var dinghy = rows.First(r => r.TextContent.Contains("The Dinghy", StringComparison.Ordinal));

        Assert.Contains("Vehicle Points", barge.TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(barge.QuerySelectorAll(".finding"));

        // The control: the machine beside it carries none, so the finding is about the row rather
        // than drawn on every row.
        Assert.Empty(dinghy.QuerySelectorAll(".finding"));
    }

    /// <summary>
    /// <b>Arriving from the palette seeds the filter box with the row's name and opens the thing
    /// the row belongs to.</b> A list narrowed by something the box does not show is a list that
    /// looks broken; a list inside a panel the reader cannot see reads as the palette having done
    /// nothing at all.
    /// </summary>
    [Fact]
    public void TheStepArrivesFilteredToTheRequestedRow()
    {
        using var ctx = Ctx();
        ctx.Session.Sheet.Headquarters.Add(new OwnedHeadquarters("The Loft") { PerkHeroPoints = 2 });

        var commands = ctx.Services.GetRequiredService<Commands>();
        commands.RequestAssetRow(AssetCatalogue.BaseFeaturePrefix + "training_facilities");

        var page = ctx.Render<Assets>();

        Assert.Equal("Training Facilities",
            page.Find(".base .options-filter input").GetAttribute("value"));

        var offered = page.FindAll(".base .options .option");
        Assert.Single(offered);
        Assert.Contains("Training Facilities", offered[0].TextContent, StringComparison.Ordinal);

        // Read once: rendering the step again is the ordinary consequence of a keystroke anywhere
        // on it, and a request left set would drag the list back here every time.
        Assert.Null(commands.RequestedAssetRowId);
    }
}
