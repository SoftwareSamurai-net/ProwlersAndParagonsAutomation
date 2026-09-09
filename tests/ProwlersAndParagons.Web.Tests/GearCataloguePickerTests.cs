using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The Gear step picking from Chapter 6's catalogue, in the browser.</b>
///
/// <para>Rendered against the shipped <c>gear.json</c> like every other test here, and through a
/// context that really stores, so a chosen row can be asked for back rather than asserted at the
/// moment of the click.</para>
/// </summary>
public sealed class GearCataloguePickerTests
{
    private static RenderContext Ctx() => new(storesForReal: true);

    private static GearCatalogue CatalogueOf(RenderContext ctx) => ctx.Session.Catalogue;

    private static void Click(IRenderedComponent<IComponent> page, string text) =>
        page.FindAll("button")
            .First(b => b.TextContent.Contains(text, StringComparison.Ordinal))
            .Click();

    // ── The Item Con is no longer on offer ───────────────────────────────────

    /// <summary>
    /// <b>The Cons picker on a piece of gear offers p.93's list and not the Item Con.</b>
    ///
    /// <para>This is the browser half of the engine's answer. <c>Target.Gear</c> used to fall
    /// through to every Con there is, which put the one Con the engine refuses to credit in front
    /// of a player as though taking it would discount their sword.</para>
    /// </summary>
    [Fact]
    public void TheGearConsPickerDoesNotOfferTheItemConOrTheTwoRateReductions()
    {
        using var ctx = Ctx();

        var picker = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Scope, ProConPicker.Target.Gear)
            .Add(c => c.Selected, [])
            .Add(c => c.IsPro, false));

        Click(picker, "Add a Con");

        var offered = picker.FindAll(".options .option .name").Select(e => e.TextContent).ToList();

        // Positive control first: the list has options in it, so what follows is a filter rather
        // than an empty box.
        Assert.NotEmpty(offered);
        Assert.Contains(offered, o => o.Contains("Burnout", StringComparison.Ordinal));

        Assert.DoesNotContain(offered, o => o.Contains("Item", StringComparison.Ordinal));
        Assert.DoesNotContain(offered, o => o.Contains("Overkill", StringComparison.Ordinal));
        Assert.DoesNotContain(offered, o => o.Contains("Weak", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>And a Power's picker is unchanged</b>, which is what says the filter is scoped rather
    /// than a Con that has been taken out of the data.
    /// </summary>
    [Fact]
    public void APowersConsPickerStillOffersTheItemCon()
    {
        using var ctx = Ctx();

        var blast = ctx.Services.GetRequiredService<RulesRepository>().GetPower("blast");

        var picker = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Power, blast)
            .Add(c => c.Scope, ProConPicker.Target.Power)
            .Add(c => c.Selected, [])
            .Add(c => c.IsPro, false));

        Click(picker, "Add a Con");

        var offered = picker.FindAll(".options .option .name").Select(e => e.TextContent).ToList();

        Assert.Contains(offered, o => o.Contains("Item", StringComparison.Ordinal));
    }

    // ── Picking a row ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Choosing a catalogue row puts one item on the sheet, under the printed name, carrying
    /// the row's id.</b> The item is free, because p.91 says mundane gear is not bought — a battle
    /// axe as much as a torch.
    /// </summary>
    [Fact]
    public void ChoosingAWeaponRowAddsAFreeItemCarryingTheRowsId()
    {
        using var ctx = Ctx();

        var page = ctx.Render<Gear>();

        page.Find(".catalogue .options-filter input").Input("Battle Axe");
        page.FindAll(".catalogue .options .option")[0].Click();

        var item = Assert.Single(ctx.Session.Sheet.Gear);

        Assert.Equal("Battle Axe", item.Name);
        Assert.Equal(GearCatalogue.WeaponPrefix + "battle_axe", item.CatalogueId);
        Assert.Equal(0, ctx.Session.Costs.GearCost(item));

        // And the row it names really is the row that was clicked.
        var row = CatalogueOf(ctx).Find(item.CatalogueId!)!;
        Assert.Equal(3, row.BonusDice);
    }

    /// <summary>
    /// <b>The line on the sheet says what the row is worth.</b> A weapon that arrived with no
    /// bonus and no features beside its name would be a name somebody typed, which is the thing
    /// this step already did.
    /// </summary>
    [Fact]
    public void TheChosenRowsBonusAndFeaturesShowBesideItsName()
    {
        using var ctx = Ctx();

        var page = ctx.Render<Gear>();

        page.Find(".catalogue .options-filter input").Input("Battle Axe");
        page.FindAll(".catalogue .options .option")[0].Click();

        var line = page.Find(".chosen .body").TextContent;

        Assert.Contains("Battle Axe", line, StringComparison.Ordinal);
        Assert.Contains("+3", line, StringComparison.Ordinal);
        Assert.Contains("Two-Handed", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Free text still works, and still records no row.</b> p.91's list is "examples, not a
    /// catalogue of prices", so a character may carry something that is not on it.
    /// </summary>
    [Fact]
    public void TypingAnItemStillAddsOneWithNoCatalogueRow()
    {
        using var ctx = Ctx();

        var page = ctx.Render<Gear>();

        page.Find("input[aria-label='New gear item']").Input("A letter from his mother");
        Click(page, "Add");

        var item = Assert.Single(ctx.Session.Sheet.Gear);

        Assert.Equal("A letter from his mother", item.Name);
        Assert.Null(item.CatalogueId);
    }

    /// <summary>
    /// <b>An armour row reports a rank rather than buying a Power.</b> The step shows what the
    /// suit is worth to this character — the Gear Limit, plus the suit's bonus — and puts nothing
    /// on the Powers tab, because a piece of free mundane kit is not a purchase.
    /// </summary>
    [Fact]
    public void AnArmourRowReportsItsRankAndBuysNoPower()
    {
        using var ctx = Ctx();

        ctx.Session.Sheet.AbilityRanks["toughness"] = 10;

        var page = ctx.Render<Gear>();

        page.Find(".catalogue .options-filter input").Input("Plate");
        page.FindAll(".catalogue .options .option")[0].Click();

        // 6d Gear Limit, +2 for Plate: 8d, not the wearer's whole 10d Toughness plus two.
        var line = page.Find(".chosen .body").TextContent;

        Assert.Contains("Plate +2", line, StringComparison.Ordinal);
        Assert.Contains("Rigid", line, StringComparison.Ordinal);
        Assert.Contains("grants Armor 8d", line, StringComparison.Ordinal);

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
    }
}
