using Bunit;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// "Saved", beside the account link — the only sign the write-through to storage ever happens.
///
/// <para><b>There was none before this.</b> Every edit writes through <see cref="ICharacterStore"/>
/// silently, so nothing on screen told a player their work was kept — or, on a bad connection to
/// an account, that it was not yet.</para>
/// </summary>
public sealed class SaveStatusTests
{
    /// <summary>Nothing has been saved yet, so nothing is said.</summary>
    [Fact]
    public void NothingIsAnnouncedBeforeAnySaveHasHappened()
    {
        using var ctx = new RenderContext();
        var layout = ctx.Render<MainLayout>();

        var status = layout.Find(".save-status");
        Assert.Equal("", status.TextContent);
        Assert.Equal("polite", status.GetAttribute("aria-live"));
    }

    /// <summary>
    /// <b>The word appears once the session reports a write-through completed</b> — the same
    /// event <c>Program.cs</c> raises after its own <c>await</c> on
    /// <see cref="ICharacterStore.SaveAsync"/> returns, called directly here rather than through
    /// a real save so the test is not timed against local storage.
    /// </summary>
    [Fact]
    public async Task ASaveIsAnnouncedOnceTheSessionReportsItHappened()
    {
        await using var ctx = new RenderContext();
        var layout = ctx.Render<MainLayout>();

        await layout.InvokeAsync(() => ctx.Session.NotifySaved(ctx.Session.Version));
        layout.Render();

        Assert.Equal("Saved", layout.Find(".save-status").TextContent);
    }

    /// <summary>
    /// <b>A stale completion cannot un-confirm a newer save that has already landed.</b>
    ///
    /// <para>This is what the version carried on <see cref="CharacterSession.Saved"/> buys over
    /// a bare flag: a slow write from an earlier edit, reported <em>after</em> a faster one for a
    /// later edit has already finished, must not blank the word back out — the character on
    /// screen genuinely is saved, and the late arrival is talking about a version nobody is
    /// looking at any more. <c>WaitForAssertion</c> establishes that landing before the stale
    /// event is applied, rather than assuming it, since the real autosave wiring is what answers
    /// the edit in this test and nothing here controls how many <c>await</c>s that takes.</para>
    /// </summary>
    [Fact]
    public async Task AStaleCompletionCannotUnconfirmANewerSave()
    {
        await using var ctx = new RenderContext();
        var layout = ctx.Render<MainLayout>();

        var beforeTheEdit = ctx.Session.Version;

        await layout.InvokeAsync(() => ctx.Session.NotifyChanged());
        await layout.WaitForAssertionAsync(() =>
            Assert.Equal("Saved", layout.Find(".save-status").TextContent));

        // The slow save for the edit before this one finally lands.
        await layout.InvokeAsync(() => ctx.Session.NotifySaved(beforeTheEdit));
        layout.Render();

        Assert.Equal("Saved", layout.Find(".save-status").TextContent);
    }

    /// <summary>
    /// <b>A real character mutation, through the same autosave wiring the app uses</b>, rather
    /// than the two events called directly — this is the positive control for the wiring itself:
    /// if <c>RenderContext</c>'s subscription stopped calling <c>NotifySaved</c> after the store
    /// completed, every other test in this file would still pass.
    ///
    /// <para><c>WaitForAssertion</c> rather than a bare assertion, because the fire-and-forget
    /// write-through is real here — <c>ICharacterStore.SaveAsync</c>, through local storage —
    /// and nothing in this test controls how many <c>await</c>s stand between the edit and the
    /// word appearing.</para>
    /// </summary>
    [Fact]
    public async Task AnOrdinaryEditEventuallySaysSaved()
    {
        await using var ctx = new RenderContext();
        var layout = ctx.Render<MainLayout>();

        await layout.InvokeAsync(() => ctx.Session.Mode = SheetMode.Villain);

        await layout.WaitForAssertionAsync(() =>
            Assert.Equal("Saved", layout.Find(".save-status").TextContent));
    }
}
