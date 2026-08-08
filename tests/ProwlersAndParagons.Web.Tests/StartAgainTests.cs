using AngleSharp.Dom;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// "Start a new character" is the only control in the app that destroys work, and there is
/// no undo behind it — the sheet is not written down anywhere else. It sits on the first
/// page, between two buttons a player is meant to press.
/// </summary>
public sealed class StartAgainTests
{
    [Fact]
    public void OneClickDoesNotDiscardTheCharacter()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Discard(page).Click();

        // It has asked, and nothing has gone yet.
        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);
        Assert.Contains("Keep it", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangingYourMindLeavesTheCharacterExactlyAsItWas()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();
        var before = ctx.Session.Sheet.SelectedPowers.Count;

        Discard(page).Click();
        page.FindAll("button").Single(b => b.TextContent.Contains("Keep it", StringComparison.Ordinal)).Click();

        Assert.Equal(before, ctx.Session.Sheet.SelectedPowers.Count);
        Assert.DoesNotContain("Keep it", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirming empties the sheet <b>and</b> forgets the stored one. Clearing only the
    /// session left the old character in local storage, so it came back on the next visit —
    /// which reads as the button not having worked.
    /// </summary>
    [Fact]
    public void ConfirmingEmptiesTheSheetAndForgetsTheStoredOne()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Discard(page).Click();
        page.FindAll("button")
            .Single(b => b.TextContent.Contains("Yes, discard", StringComparison.Ordinal))
            .Click();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.Contains(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");
    }

    private static IElement Discard(IRenderedComponent<ChooseTier> page) =>
        page.FindAll("button").Single(b => b.TextContent.Contains("Start a new character", StringComparison.Ordinal));
}
