using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The three controls that throw a character away: load a Hero, load a Villain, and start again.
/// The character is kept in the browser between visits, so all three destroy work written down
/// nowhere else, and there is no undo behind any of them.
///
/// <para>Guarding only the red one would have been the wrong half. Loading a sample overwrites the
/// stored character just as completely — the write-through save fires on the same change — and it
/// reads as the safe option, which is worse.</para>
///
/// <para><b>They now live on two pages, and that is the point of the split.</b> Starting over
/// belongs to the tool; the two samples are a demonstration and moved to the portfolio. So these
/// tests take the page as a parameter rather than assuming one — the guarantee is about the
/// control, not about where it happens to be drawn.</para>
/// </summary>
public sealed class StartAgainTests
{
    private const string Discard = "Start a new character";
    private const string LoadHero = "Load a Hero";

    /// <summary>Renders whichever page owns the named control.</summary>
    private static IRenderedComponent<IComponent> PageFor(RenderContext ctx, string control) =>
        control == Discard ? ctx.Render<ChooseTier>() : ctx.Render<Portfolio>();

    [Theory]
    [InlineData(Discard)]
    [InlineData(LoadHero)]
    public void OneClickDoesNotReplaceACharacterThereIsSomethingToLose(string control)
    {
        using var ctx = new RenderContext().AsAdministrator().With(SheetMode.Hero);
        var page = PageFor(ctx, control);
        var before = ctx.Session.Sheet.Name;

        Button(page, control).Click();

        // It has asked, and nothing has happened yet — not to the sheet, and not to storage.
        Assert.Equal(before, ctx.Session.Sheet.Name);
        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);
        Assert.Contains("Keep this character", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");
    }

    /// <summary>
    /// On an untouched sheet there is nothing to ask about, so a sample loads on one click.
    /// That is the visit where a player is most likely to want one and least likely to have
    /// anything at stake — asking there would be ceremony.
    /// </summary>
    [Fact]
    public void AnEmptySheetIsReplacedWithoutBeingAskedAbout()
    {
        using var ctx = new RenderContext().AsAdministrator();
        var page = ctx.Render<Portfolio>();

        Button(page, LoadHero).Click();

        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);
        Assert.DoesNotContain("Keep this character", page.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Discard)]
    [InlineData(LoadHero)]
    public void ChangingYourMindLeavesTheCharacterExactlyAsItWas(string control)
    {
        using var ctx = new RenderContext().AsAdministrator().With(SheetMode.Hero);
        var page = PageFor(ctx, control);

        var name = ctx.Session.Sheet.Name;
        var tier = ctx.Session.Sheet.SelectedTierId;
        var powers = ctx.Session.Sheet.SelectedPowers.Count;
        var mode = ctx.Session.Mode;

        Button(page, control).Click();
        Button(page, "Keep this character").Click();

        Assert.Equal(name, ctx.Session.Sheet.Name);
        Assert.Equal(tier, ctx.Session.Sheet.SelectedTierId);
        Assert.Equal(powers, ctx.Session.Sheet.SelectedPowers.Count);
        Assert.Equal(mode, ctx.Session.Mode);
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");

        // And the control is back, rather than the page being left mid-question.
        Assert.Contains(control, page.Markup, StringComparison.Ordinal);
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

        Button(page, Discard).Click();
        Button(page, "Yes, discard").Click();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.Contains(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");
    }

    /// <summary>
    /// The clear has to land <b>after</b> the write-through save that emptying the sheet
    /// fires, or the save puts the old character straight back. `RenderContext` subscribes
    /// that handler exactly as `Program.cs` does, so the race is present here rather than
    /// argued about in a comment.
    /// </summary>
    [Fact]
    public void TheClearLandsAfterTheSaveThatEmptyingTheSheetFires()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Button(page, Discard).Click();
        Button(page, "Yes, discard").Click();

        var calls = ctx.JSInterop.Invocations
            .Select(i => i.Identifier)
            .Where(id => id is "ppStore.save" or "ppStore.clear")
            .ToList();

        Assert.Equal("ppStore.clear", calls[^1]);
        Assert.Contains("ppStore.save", calls);
    }

    private static IElement Button(IRenderedComponent<IComponent> page, string label) =>
        page.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));
}
