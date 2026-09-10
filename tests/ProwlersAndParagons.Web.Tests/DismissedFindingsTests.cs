using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A Warning can be waved off, on this browser only — <see cref="DismissedFindings"/>. The
/// routing itself is <see cref="SheetFindingsTests"/>'s job and the plain rendering is
/// <see cref="RowFindingRenderTests"/>'s; this file is the dismiss control's own behaviour: it
/// hides one Warning and no other, never draws on an Error, survives being read back from
/// storage, can be undone, and does nothing at all when storage refuses it.
/// </summary>
public sealed class DismissedFindingsTests
{
    private static IElement PowerRow(IRenderedComponent<IComponent> page, string name) =>
        page.FindAll(".chosen > li")
            .First(li => li.TextContent.Contains(name, StringComparison.Ordinal));

    /// <summary>Two Powers, both missing a Source, so each carries its own <c>POWER_WITHOUT_SOURCE</c>
    /// Warning — the same code, two different <c>SubjectId</c>s.</summary>
    private static void TwoUnsourcedPowers(RenderContext ctx)
    {
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3, [], []));
        sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4, [], []));
    }

    /// <summary>
    /// Dismissing the Warning on one Power's row hides it there and leaves the other Power's own
    /// Warning — same code, different subject — exactly as it was. The key is the four fields
    /// <see cref="SheetFindings"/> routes on, not the message or the code alone.
    /// </summary>
    [Fact]
    public void DismissingOneWarningHidesOnlyThatOneWarning()
    {
        using var ctx = new RenderContext(storesForReal: true);
        TwoUnsourcedPowers(ctx);

        var page = ctx.Render<PowersTab>();
        Assert.NotNull(PowerRow(page, "Armor").QuerySelector(".row-findings .finding"));
        Assert.NotNull(PowerRow(page, "Invisibility").QuerySelector(".row-findings .finding"));

        PowerRow(page, "Armor").QuerySelector(".finding-dismiss")!.Click();

        Assert.Null(PowerRow(page, "Armor").QuerySelector(".row-findings .finding"));
        Assert.NotNull(PowerRow(page, "Invisibility").QuerySelector(".row-findings .finding"));
    }

    /// <summary>
    /// An Error never draws the control, even though it sits in the very same list a Warning's
    /// control renders in — <c>RowFinding</c> asserts on severity rather than trusting that only
    /// a Warning's key would ever reach it.
    /// </summary>
    [Fact]
    public void AnErrorRowHasNoDismissControl()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        foreach (var a in ctx.Session.Rules.Abilities) sheet.AbilityRanks[a.Id] = 1;
        sheet.AbilityRanks["might"] = 13; // above Standard's Trait Cap of 12d: an Error.

        var page = ctx.Render<AbilitiesTab>();
        var might = page.FindAll(".rank-row")
            .First(r => r.QuerySelector(".rank-name")?.TextContent.TrimStart()
                .StartsWith("Might", StringComparison.Ordinal) == true);
        var findings = might.NextElementSibling;

        Assert.NotNull(findings);
        Assert.Contains("error", findings!.QuerySelector(".finding")!.ClassList);
        Assert.Null(findings.QuerySelector(".finding-dismiss"));
    }

    /// <summary>
    /// The dismissal is read back out of storage by a component instance that never dismissed
    /// anything itself — a fresh <see cref="DismissedFindings"/> against the same store,
    /// standing in for the browser being reloaded. It is not merely remembered by the instance
    /// that made the call.
    /// </summary>
    [Fact]
    public async Task ADismissalSurvivesBeingReadBackFromStorageByANewInstance()
    {
        using var ctx = new RenderContext(storesForReal: true);
        TwoUnsourcedPowers(ctx);

        var page = ctx.Render<PowersTab>();
        PowerRow(page, "Armor").QuerySelector(".finding-dismiss")!.Click();

        var characterId = DismissedFindings.CharacterKey(ctx.Session);
        var issue = Assert.Single(
            SheetFindings.ForPower(ctx.Session.Validate(), "armor"), i => i.Code == "POWER_WITHOUT_SOURCE");

        var reloaded = new DismissedFindings(ctx.Storage!);
        await reloaded.LoadAsync(characterId);

        Assert.True(reloaded.IsDismissed(characterId, issue));
    }

    /// <summary>
    /// The review panel's "Show them" clears every dismissal for this character, and the Warning
    /// it had been hiding is back on its own row afterwards.
    /// </summary>
    [Fact]
    public void ShowThemBringsADismissedWarningBack()
    {
        using var ctx = new RenderContext(storesForReal: true);
        TwoUnsourcedPowers(ctx);

        var powers = ctx.Render<PowersTab>();
        PowerRow(powers, "Armor").QuerySelector(".finding-dismiss")!.Click();
        Assert.Null(PowerRow(powers, "Armor").QuerySelector(".row-findings .finding"));

        var review = ctx.Render<Review>();
        var note = review.Find(".btn.small");
        Assert.Contains("Show them", review.Markup, StringComparison.Ordinal);
        note.Click();

        powers.Render();
        Assert.NotNull(PowerRow(powers, "Armor").QuerySelector(".row-findings .finding"));
    }

    /// <summary>
    /// A browser that refuses storage dismisses nothing — the same failure discipline
    /// <see cref="SavedCharacters"/> uses. The click still fires; nothing about the finding
    /// changes because the write never landed.
    /// </summary>
    [Fact]
    public void AStorageRefusalMeansNothingIsDismissed()
    {
        using var ctx = new RenderContext(storesForReal: true);
        TwoUnsourcedPowers(ctx);
        ctx.Storage!.Refuses = true;

        var page = ctx.Render<PowersTab>();
        PowerRow(page, "Armor").QuerySelector(".finding-dismiss")!.Click();

        Assert.NotNull(PowerRow(page, "Armor").QuerySelector(".row-findings .finding"));
    }
}
