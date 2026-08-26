using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The controls that throw a character away outright: discard on the tier page, and load a
/// Hero or a Villain from the portfolio. The character is kept in the browser between visits,
/// so all three can overwrite twenty minutes of work written down nowhere else.
///
/// <para><b>They used to ask first, in the page, and now they do not.</b> A confirming dialogue
/// and a single-level undo make the same promise — the mistake is reversible — but a dialogue
/// pays that cost on every use and undo only pays it on the uses that turn out to be mistakes.
/// So every one of these acts the moment it is clicked, and <see cref="CharacterSession.Undo"/>
/// is what makes that safe: see <c>UndoTests</c> for the mechanism itself, including the
/// single-level guarantee. What is asserted here is that the three controls actually reach
/// it — each one really does buffer what it replaces, and really does replace at once rather
/// than waiting to be asked.</para>
///
/// <para><b>They live on two pages, and that is the point of the split.</b> Starting over
/// belongs to the tool; the two samples are a demonstration and live on the portfolio. So
/// these tests take the page as a parameter rather than assuming one — the guarantee is about
/// the control, not about where it happens to be drawn.</para>
/// </summary>
public sealed class StartAgainTests
{
    private const string Discard = "Start a new character";
    private const string LoadHero = "Load a Hero";

    /// <summary>Renders whichever page owns the named control.</summary>
    private static IRenderedComponent<IComponent> PageFor(RenderContext ctx, string control) =>
        control == Discard ? ctx.Render<ChooseTier>() : ctx.Render<Portfolio>();

    /// <summary>
    /// No question first, whether or not there is something to lose — the whole point of
    /// replacing the confirm with undo. A single click always does the thing.
    /// </summary>
    [Theory]
    [InlineData(Discard)]
    [InlineData(LoadHero)]
    public void OneClickReplacesTheCharacterAtOnce(string control)
    {
        using var ctx = new RenderContext().AsAdministrator().With(SheetMode.Hero);

        // Marks the sheet as this reader's own, distinctly from whatever loaded it — so
        // "replaced" means something even when the control reloads the same sample by name.
        ctx.Session.Sheet.Name = "A name nobody would load by clicking this button";
        var page = PageFor(ctx, control);

        Button(page, control).Click();

        Assert.NotEqual(
            "A name nobody would load by clicking this button", ctx.Session.Sheet.Name);
        Assert.DoesNotContain("Keep this character", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Keep what I have", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The positive control for the guard above: there really was something on the sheet
    /// before the click, so "replaced" is not vacuously true of an already-empty one.
    /// </summary>
    [Theory]
    [InlineData(Discard)]
    [InlineData(LoadHero)]
    public void TheCharacterReplacedReallyHadSomethingInIt(string control)
    {
        using var ctx = new RenderContext().AsAdministrator().With(SheetMode.Hero);
        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);

        Button(PageFor(ctx, control), control).Click();

        Assert.True(ctx.Session.CanUndo);
    }

    /// <summary>
    /// On an untouched sheet there is nothing worth buffering, so the control still acts —
    /// nothing here ever asked about an empty sheet — but there is nothing for the banner to
    /// offer afterwards.
    /// </summary>
    [Fact]
    public void AnEmptySheetIsReplacedAndArmsNoUndo()
    {
        using var ctx = new RenderContext().AsAdministrator();
        var page = ctx.Render<Portfolio>();

        Button(page, LoadHero).Click();

        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);
        Assert.False(ctx.Session.CanUndo);
    }

    /// <summary>
    /// <see cref="CharacterSession.Undo"/> brings back exactly what either control replaced —
    /// asked of the engine's own answers, not a field list, the same way the persistence
    /// round-trip test in this project is.
    /// </summary>
    [Theory]
    [InlineData(Discard)]
    [InlineData(LoadHero)]
    public void UndoingBringsBackTheCharacterThatWasReplaced(string control)
    {
        using var ctx = new RenderContext().AsAdministrator().With(SheetMode.Hero);
        var page = PageFor(ctx, control);

        var name = ctx.Session.Sheet.Name;
        var tier = ctx.Session.Sheet.SelectedTierId;
        var powers = ctx.Session.Sheet.SelectedPowers.Count;
        var mode = ctx.Session.Mode;

        Button(page, control).Click();
        Assert.True(ctx.Session.CanUndo);

        ctx.Session.Undo();

        Assert.Equal(name, ctx.Session.Sheet.Name);
        Assert.Equal(tier, ctx.Session.Sheet.SelectedTierId);
        Assert.Equal(powers, ctx.Session.Sheet.SelectedPowers.Count);
        Assert.Equal(mode, ctx.Session.Mode);
        Assert.False(ctx.Session.CanUndo);
    }

    /// <summary>
    /// Discarding empties the sheet <b>and</b> forgets the stored one, so a reader who does not
    /// undo before closing the tab really has started over — the single-level buffer is memory
    /// only and does not survive that, which is the trade the whole feature makes.
    /// </summary>
    [Fact]
    public void DiscardingEmptiesTheSheetAndForgetsTheStoredOne()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Button(page, Discard).Click();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.Contains(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");
    }

    /// <summary>
    /// The clear has to land <b>after</b> the write-through save that emptying the sheet
    /// fires, or the save puts the old character straight back. <c>RenderContext</c> subscribes
    /// that handler exactly as <c>Program.cs</c> does, so the race is present here rather than
    /// argued about in a comment.
    /// </summary>
    [Fact]
    public void TheClearLandsAfterTheSaveThatEmptyingTheSheetFires()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Button(page, Discard).Click();

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
