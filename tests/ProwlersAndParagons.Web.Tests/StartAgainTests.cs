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
    /// Starting another character empties the sheet and <b>keeps</b> the stored one, which is the
    /// reversal this control needed. It used to clear the slot the character was in — the owner
    /// pressed it after importing a character and lost it. Nothing is thrown away here now, so
    /// there is no <c>ppStore.clear</c> to find; <c>StartAnotherTests</c> is where the keeping
    /// itself is proved, against a storage that actually stores.
    ///
    /// <para>The buffer is still armed, and still matters for the same reason it always did: the
    /// sheet on screen was replaced, and a reader who meant to keep editing it wants it back
    /// without walking to the manager. It is memory only and does not survive closing the tab —
    /// but the character does now, which it did not before.</para>
    /// </summary>
    [Fact]
    public void StartingAnotherEmptiesTheSheetAndThrowsNothingAway()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        Button(page, Discard).Click();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.DoesNotContain(ctx.JSInterop.Invocations, i => i.Identifier == "ppStore.clear");
    }

    // **The ordering property is not asserted here, and that is deliberate.** The keep has to land
    // before the write-through save that emptying the sheet fires, or that save writes the empty
    // sheet over the character being kept — but this context's storage answers null to every read,
    // so the pointer move it records is invisible to the very next call that reads the pointer.
    // A test written here would watch the autosave land on the old key and would have to either
    // assert that (blessing the bug) or assert nothing. `StartAnotherTests` owns it instead,
    // against a storage that actually stores.

    private static IElement Button(IRenderedComponent<IComponent> page, string label) =>
        page.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));
}
