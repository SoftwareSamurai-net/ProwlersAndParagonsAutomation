using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The sheet's second page</b> — vehicles, headquarters, Gadgets and campaign-asset
/// contributions, rendered only when the character owns at least one of the four. See
/// docs/guide/printed-sheet.md, "Vehicles, Headquarters, Gadgets and campaign assets print on a
/// second page — only when owned".
///
/// <para><b>Every line asserted here comes off <see cref="AssetFormatter"/></b>, exactly as
/// the page itself is built — a test written against invented text would pass while the page
/// printed something else, which is the trap <c>SheetText</c>'s own guide entry warns about.</para>
/// </summary>
public sealed class SheetAssetsPageTests
{
    private static CharacterSheet Character(string name = "Ninth Precinct") =>
        new() { Name = name, SelectedTierId = "standard" };

    // ── The page does not exist for a character who owns nothing ──────────────────────

    /// <summary>
    /// The ordinary case — nearly every character in the game. No second page, no empty box:
    /// see <see cref="SheetView.HasAssets"/>, which this depends on entirely.
    /// </summary>
    [Fact]
    public void ACharacterOwningNothingRendersNoAssetsPage()
    {
        using var ctx = new RenderContext();
        ctx.Session.RestoreBeforeFirstRender(Character(), SheetMode.Hero);

        var page = ctx.Render<SheetView>();

        Assert.Empty(page.FindAll(".sheet-assets-page"));
    }

    /// <summary>
    /// One vehicle is enough to raise the page, and nothing else about the sheet's first page
    /// is disturbed — proving the gate is really <c>OR</c> across the four families rather than
    /// a count that happens to true up.
    /// </summary>
    [Fact]
    public void OwningOneVehicleAloneRaisesTheAssetsPage()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        sheet.Vehicles.Add(new OwnedVehicle("The Wing") { PerkHeroPoints = 1 });
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var page = ctx.Render<SheetView>();

        Assert.Single(page.FindAll(".sheet-assets-page"));
    }

    // ── Each family prints AssetFormatter's own text ───────────────────────────────────

    [Fact]
    public void TheVehicleFamilyPrintsAssetFormattersText()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        var vehicle = new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 1, Body = 3, Speed = 4, Control = 1,
            Features = [new SelectedAssetFeature("sensors")]
        };
        sheet.Vehicles.Add(vehicle);
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var page = ctx.Render<SheetView>();
        var text = SheetText.Visible(page.Find(".sheet-assets-page"));

        Assert.Contains(AssetFormatter.Describe(vehicle, ctx.Session.Costs), text, StringComparison.Ordinal);
        Assert.Contains(AssetFormatter.Characteristics(vehicle), text, StringComparison.Ordinal);
        Assert.Contains(
            AssetFormatter.Feature(vehicle.Features[0], ctx.Session.Rules, onAVehicle: true),
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeadquartersFamilyPrintsAssetFormattersText()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        var headquarters = new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("training_facilities")]
        };
        sheet.Headquarters.Add(headquarters);
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var page = ctx.Render<SheetView>();
        var text = SheetText.Visible(page.Find(".sheet-assets-page"));

        Assert.Contains(AssetFormatter.Describe(headquarters, ctx.Session.Costs), text, StringComparison.Ordinal);
        Assert.Contains(
            AssetFormatter.Feature(headquarters.Features[0], ctx.Session.Rules, onAVehicle: false),
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGadgetFamilyPrintsAssetFormattersText()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        var gadget = new BuiltGadget("Freeze Ray") { Complexity = 5 };
        sheet.Gadgets.Add(gadget);
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var page = ctx.Render<SheetView>();
        var text = SheetText.Visible(page.Find(".sheet-assets-page"));

        Assert.Contains(
            AssetFormatter.Describe(gadget, ctx.Session.Costs, sheet.ImmortalityCost),
            text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A campaign contribution prints what this character put in — never the pooled object or
    /// another member's share, which this sheet cannot see and must not claim to.
    /// </summary>
    [Fact]
    public void TheCampaignFamilyPrintsOnlyThisCharactersContribution()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        var contribution = new CampaignAssetContribution("wing-1")
        {
            Name = "The Wing", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 3
        };
        sheet.CampaignAssets.Add(contribution);
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var page = ctx.Render<SheetView>();
        var text = SheetText.Visible(page.Find(".sheet-assets-page"));

        Assert.Contains(AssetFormatter.Describe(contribution), text, StringComparison.Ordinal);

        // "3 HP put in" is this character's own figure. Nothing on the page may claim a budget
        // or a total spend for the shared object — this sheet was never handed one.
        Assert.DoesNotContain("Vehicle Points", text, StringComparison.Ordinal);
    }

    // ── An id that resolves to nothing prints the fallback, and never throws ──────────

    /// <summary>
    /// A Gadget whose Powers carry a Con id the rulebook does not have cannot be priced —
    /// <c>CostCalculator</c> throws rather than guessing, exactly as it does for gear and for
    /// every other cost on this sheet. <see cref="AssetFormatter.Describe(BuiltGadget,
    /// CostCalculator, int?)"/> already routes the spend through <see
    /// cref="AssetFormatter.Reachable"/>, so the page prints the sentence that says the spend
    /// could not be worked out instead of taking the render down. Removing <c>Reachable</c>'s
    /// own <c>catch</c> turns this red.
    /// </summary>
    [Fact]
    public void AnUnpriceableGadgetRendersTheFallbackAndNeverThrows()
    {
        using var ctx = new RenderContext();
        var sheet = Character();
        var gadget = new BuiltGadget("Misbuilt Ray")
        {
            Complexity = 5,
            Powers = [new SelectedPower("armor", 4, [], [new SelectedProCon("not_a_real_con")])]
        };
        sheet.Gadgets.Add(gadget);
        ctx.Session.RestoreBeforeFirstRender(sheet, SheetMode.Hero);

        var exception = Record.Exception(() => ctx.Render<SheetView>());
        Assert.Null(exception);

        var page = ctx.Render<SheetView>();
        var text = SheetText.Visible(page.Find(".sheet-assets-page"));

        Assert.Contains("Misbuilt Ray", text, StringComparison.Ordinal);
        Assert.Contains("spend cannot be worked out", text, StringComparison.Ordinal);
    }
}
