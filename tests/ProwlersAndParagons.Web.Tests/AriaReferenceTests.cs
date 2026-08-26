using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Every ARIA reference on every screen points at something that is actually on the page.
///
/// <para><b>A dangling IDREF is the quietest accessibility bug there is.</b> It promises a screen
/// reader a name, a description or a controlled region and hands it nothing — and every assertion
/// about the attribute's *presence* passes either way, which is how one shipped here before:
/// <c>aria-controls</c> on the budget disclosure named a breakdown that renders inside an
/// <c>@if</c>, so the reference dangled whenever the panel was shut.</para>
///
/// <para><b>This is a sweep, not another per-row check.</b> <c>RowDescriptionTests</c> resolves the
/// target for a Powers row and an Ability row, one surface at a time, which catches an instance;
/// this walks every rendered surface and resolves *every* token of
/// <c>aria-describedby</c>, <c>aria-labelledby</c> and <c>aria-controls</c>, which catches the
/// class. All three are space-separated ID lists, so each token is resolved separately — an
/// attribute naming two ids of which one is real would satisfy any check that looked the whole
/// value up as a single id.</para>
///
/// <para><b>What this does not do is close the screen-reader item.</b> It is a structural check and
/// nothing more: it cannot hear an announcement, cannot tell a useful description from a useless
/// one, and cannot say whether a live region fires at the right moment. `docs/HANDOVER.md` records
/// that testing with a real screen reader is owed on eight surfaces, and this test is not a
/// substitute for any of it — it removes one whole class of silent breakage so that a person doing
/// that work is not spending it on broken plumbing.</para>
/// </summary>
public sealed class AriaReferenceTests
{
    private static readonly string[] ReferenceAttributes =
        ["aria-describedby", "aria-labelledby", "aria-controls"];

    /// <summary>
    /// Renders one surface and returns how many references it carried, having resolved each.
    ///
    /// <para>Returning the count is what lets the caller assert the sweep actually reached
    /// something. A surface that failed to render, or that stopped carrying references at all,
    /// satisfies "no dangling reference" completely — the failure shape this repository has
    /// shipped four times.</para>
    /// </summary>
    private static int ResolveEveryReference(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> page, string surface)
    {
        var found = 0;

        foreach (var attribute in ReferenceAttributes)
        {
            foreach (var element in page.FindAll($"[{attribute}]"))
            {
                var value = element.GetAttribute(attribute);
                if (string.IsNullOrWhiteSpace(value)) continue;

                foreach (var id in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    found++;

                    // CssSelector rather than GetElementById: the fragment is not a document, and
                    // an id is escaped here the same way the browser would match it.
                    var target = page.FindAll($"#{id}");

                    Assert.True(
                        target.Count > 0,
                        $"{surface}: <{element.TagName.ToLowerInvariant()}> carries "
                        + $"{attribute}=\"{value}\" and no element with id \"{id}\" is on the page. "
                        + "A screen reader is promised something and handed nothing, and every "
                        + "check that only counts the attribute passes either way.");
                }
            }
        }

        return found;
    }

    [Fact]
    public void EveryAriaReferenceOnEverySurfaceResolves()
    {
        var total = 0;

        using (var ctx = new RenderContext())
        {
            ctx.Session.Sheet.SelectedTierId = "standard";
            ctx.Session.Sheet.Perks.Add(new SelectedPerk("contacts"));
            ctx.Session.Sheet.Flaws.Add(new SelectedFlaw("amnesia"));

            total += ResolveEveryReference(ctx.Render<PowersTab>(), "PowersTab");
            total += ResolveEveryReference(ctx.Render<AbilitiesTab>(), "AbilitiesTab");
            total += ResolveEveryReference(ctx.Render<TalentsTab>(), "TalentsTab");
            total += ResolveEveryReference(ctx.Render<PerksTab>(), "PerksTab");
            total += ResolveEveryReference(ctx.Render<FlawsTab>(), "FlawsTab");
            total += ResolveEveryReference(ctx.Render<HpBudgetBar>(), "HpBudgetBar");
            total += ResolveEveryReference(ctx.Render<CommandPalette>(), "CommandPalette");
            total += ResolveEveryReference(ctx.Render<Characteristics>(), "Characteristics");
            total += ResolveEveryReference(ctx.Render<Home>(), "Home");
            total += ResolveEveryReference(ctx.Render<ChooseTier>(), "ChooseTier");
            total += ResolveEveryReference(ctx.Render<Review>(), "Review");
            total += ResolveEveryReference(ctx.Render<SheetView>(), "SheetView");
        }

        // The positive control, and it is the point of the count. Every assertion above is an
        // absence; a run in which nothing rendered, or in which the app stopped using ARIA
        // references altogether, would satisfy all of them while proving nothing at all.
        Assert.True(
            total >= 20,
            $"Only {total} ARIA references were found across every surface swept. The app carried "
            + "more than that when this was written, so either the surfaces stopped rendering or "
            + "the attributes went away — fix the sweep rather than lowering this number.");
    }
}
