using Bunit;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The single-level undo behind <see cref="CharacterSession.StartAgain"/>,
/// <see cref="CharacterSession.LoadSample"/> and <see cref="CharacterSession.ReplaceWithUndo"/> —
/// the mechanism itself, and the one place it is offered on screen.
///
/// <para>Which control reaches which of these three methods, and that each really does act at
/// once rather than asking first, is <c>StartAgainTests</c> and <c>ReplayRenderTests</c>'
/// business. This file is about what happens once one of them has fired: is the old character
/// recoverable, exactly once, and does the banner actually offer it.</para>
/// </summary>
public sealed class UndoTests
{
    /// <summary>Nothing has replaced anything yet, so there is nothing to bring back.</summary>
    [Fact]
    public void ThereIsNothingToUndoOnAFreshSession()
    {
        using var ctx = new RenderContext();

        Assert.False(ctx.Session.CanUndo);

        // Calling it anyway does nothing rather than throwing — a stray click, or a keyboard
        // shortcut pressed with nothing armed, must not take the app down.
        ctx.Session.Undo();
        Assert.False(ctx.Session.CanUndo);
    }

    /// <summary>
    /// Replacing a character that had nothing in it arms no undo — there was nothing at stake,
    /// and offering to bring back an empty sheet would be ceremony over nothing.
    /// </summary>
    [Fact]
    public void ReplacingAnEmptyCharacterArmsNoUndo()
    {
        using var ctx = new RenderContext();
        Assert.False(ctx.Session.HasSomethingToLose);

        ctx.Session.StartAgain();

        Assert.False(ctx.Session.CanUndo);
    }

    /// <summary>
    /// Replacing a character that had something in it arms undo and names what it would bring
    /// back — the label is read off the rendered banner in
    /// <see cref="TheBannerOffersUndoAndRestoresOnClick"/>; this is the value it reads from.
    /// </summary>
    [Fact]
    public void ReplacingACharacterWithSomethingInItArmsUndo()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var name = ctx.Session.Sheet.Name;

        ctx.Session.StartAgain();

        Assert.True(ctx.Session.CanUndo);
        Assert.Equal(name, ctx.Session.UndoLabel);
    }

    /// <summary>
    /// <see cref="CharacterSession.Undo"/> restores the sheet <b>as its own object</b>, not the
    /// live instance that was replaced — asserted by mutating the character that is on screen
    /// after the undo and checking the buffer was not the thing that changed. This is the
    /// replay's own recorded bug (handing over a shared instance let the first edit rewrite the
    /// recording) reached from the other side: here it would show up as a second undo somehow
    /// bringing back an edit that happened <em>after</em> the first one.
    /// </summary>
    [Fact]
    public void UndoRestoresACopyNotTheLiveInstance()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var before = ctx.Session.Sheet;

        ctx.Session.StartAgain();
        ctx.Session.Undo();

        Assert.NotSame(before, ctx.Session.Sheet);

        // Edit the restored sheet, then confirm nothing left in the (now empty) undo buffer
        // could echo the change back — there is nothing to echo it from, since Undo cleared
        // the buffer the moment it ran.
        ctx.Session.Sheet.Name = "Changed after undo";
        Assert.False(ctx.Session.CanUndo);
    }

    /// <summary>
    /// Single-level: doing it once clears the buffer, so a second call in a row does nothing —
    /// there is no redo, and no way to reach further back than the one thing just replaced.
    /// </summary>
    [Fact]
    public void ASecondUndoDoesNotGoBackFurther()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var original = ctx.Session.Sheet.Name;

        ctx.Session.StartAgain();
        ctx.Session.Undo();
        Assert.Equal(original, ctx.Session.Sheet.Name);
        Assert.False(ctx.Session.CanUndo);

        // Nothing is armed any more, so undoing again is a no-op rather than a second step back
        // — there is nothing before "original" for it to reach.
        ctx.Session.Sheet.Name = "Edited after the first undo";
        ctx.Session.Undo();

        Assert.Equal("Edited after the first undo", ctx.Session.Sheet.Name);
    }

    /// <summary>
    /// The window closes on the first edit to the character that replaced the buffered one —
    /// not on a timer, on <see cref="CharacterSession.Version"/> moving. Undoing at that point
    /// would silently throw away whatever the reader had just done to the new character, which
    /// is the exact footgun this feature exists to remove, reintroduced from the other side.
    /// </summary>
    [Fact]
    public void TheWindowClosesOnTheFirstEditToTheReplacement()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var original = ctx.Session.Sheet.Name;

        ctx.Session.StartAgain();
        Assert.True(ctx.Session.CanUndo);

        // An ordinary edit to the character that replaced the buffered one.
        ctx.Session.Sheet.Name = "Building someone new";
        ctx.Session.NotifyChanged();

        Assert.False(ctx.Session.CanUndo);

        ctx.Session.Undo();
        Assert.Equal("Building someone new", ctx.Session.Sheet.Name);
        Assert.NotEqual(original, ctx.Session.Sheet.Name);
    }

    /// <summary>
    /// The one place undo is offered — the live region already in the banner, shared with
    /// "Saved" rather than opening a second one. Reads <c>TextContent</c>, never markup with
    /// the tags stripped, for the reason the two test projects' own remarks give.
    /// </summary>
    [Fact]
    public async Task TheBannerOffersUndoAndRestoresOnClick()
    {
        await using var ctx = new RenderContext().With(SheetMode.Hero);
        var name = ctx.Session.Sheet.Name;
        var layout = ctx.Render<MainLayout>();

        // The positive control: before anything is replaced, the region says nothing about
        // undo — so the assertion below is reading a real appearance, not a message that was
        // always there.
        Assert.DoesNotContain("Undo", layout.Find(".save-status").TextContent, StringComparison.Ordinal);

        await layout.InvokeAsync(() => ctx.Session.StartAgain());
        layout.Render();

        var status = layout.Find(".save-status");
        Assert.Contains(name, status.TextContent, StringComparison.Ordinal);
        Assert.Contains("Undo", status.TextContent, StringComparison.Ordinal);

        status.QuerySelector("button")!.Click();

        Assert.Equal(name, ctx.Session.Sheet.Name);
        Assert.DoesNotContain("Undo", layout.Find(".save-status").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Undo takes priority over "Saved" when both are momentarily true — the very change that
    /// arms undo also completes a write-through over what it replaced, so without this the
    /// autosave would win the race and hide the one thing worth announcing.
    ///
    /// <para><see cref="CharacterSession.NotifySaved"/> is called directly, the same way
    /// <c>SaveStatusTests.ASaveIsAnnouncedOnceTheSessionReportsItHappened</c> does, rather than
    /// waited for through the real write-through: what is being pinned is the ordering in
    /// <c>MainLayout</c>'s markup once both are true for the same version, not how quickly a
    /// real save lands.</para>
    /// </summary>
    [Fact]
    public async Task UndoOutranksASimultaneousSavedAnnouncement()
    {
        await using var ctx = new RenderContext().With(SheetMode.Hero);
        var layout = ctx.Render<MainLayout>();

        await layout.InvokeAsync(() =>
        {
            ctx.Session.StartAgain();
            ctx.Session.NotifySaved(ctx.Session.Version);
        });
        layout.Render();

        var status = layout.Find(".save-status").TextContent;
        Assert.Contains("Undo", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved", status, StringComparison.Ordinal);
    }
}
