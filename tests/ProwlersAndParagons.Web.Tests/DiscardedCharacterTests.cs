using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The undo behind a <em>saved row</em> being discarded — the half
/// <see cref="CharacterSession"/>'s own buffer cannot do.
///
/// <para>That buffer holds the character that was on screen, which is enough for discarding it,
/// loading a sample, importing a file and opening a recording, because every one of those replaces
/// the sheet the session is holding. Discarding a different row is not that: the session has never
/// seen that character and its only copy is the one in the store, which is what the delete removes.
/// <c>CharacterManagerTests</c> covers which click arms which buffer; this file is about what
/// happens once one is armed.</para>
/// </summary>
public sealed class DiscardedCharacterTests
{
    private static DiscardedCharacter Buffer(RenderContext ctx) =>
        ctx.Services.GetRequiredService<DiscardedCharacter>();

    private static AccountCharacterStore Store(RenderContext ctx) =>
        ctx.Services.GetRequiredService<AccountCharacterStore>();

    /// <summary>Nothing discarded, nothing to bring back — and asking anyway does not throw.</summary>
    [Fact]
    public async Task ThereIsNothingToUndoOnAFreshSession()
    {
        await using var ctx = new RenderContext();

        Assert.False(Buffer(ctx).CanUndo);
        Assert.False(await Buffer(ctx).UndoAsync());
    }

    /// <summary>
    /// The whole point: the character goes back into the store <b>under the id it had</b>, so the
    /// row a reader was looking at is the row that comes back.
    /// </summary>
    [Fact]
    public async Task UndoWritesTheCharacterBackUnderTheSameId()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var id = SavedCharacters.NewId();
        var hero = SampleCharacters.Hero();
        await Store(ctx).RestoreAsync(id, "Ninth Precinct", hero, SheetMode.Hero);

        await Buffer(ctx).RememberAsync(id, "Ninth Precinct", hero, SheetMode.Hero);
        await Store(ctx).DeleteAsync(id);

        // The positive control on the assertion below: it really was gone first.
        Assert.Null(await Store(ctx).ReadAsync(id));

        Assert.True(await Buffer(ctx).UndoAsync());

        var back = await Store(ctx).ReadAsync(id);
        Assert.NotNull(back);
        Assert.Equal(hero.Name, back!.Value.Sheet.Name);
        Assert.Equal(hero.SelectedPowers.Count, back.Value.Sheet.SelectedPowers.Count);

        // And it is listed under the label it had, not defaulted to "Unnamed character".
        Assert.Contains((await Store(ctx).ListAsync()).Characters,
            c => c.Id == id && c.Label == "Ninth Precinct");
    }

    /// <summary>One level, and no redo: the buffer is cleared by the undo that used it.</summary>
    [Fact]
    public async Task ItIsSingleLevel()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var id = SavedCharacters.NewId();
        await Buffer(ctx).RememberAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        Assert.True(await Buffer(ctx).UndoAsync());
        Assert.False(Buffer(ctx).CanUndo);
        Assert.False(await Buffer(ctx).UndoAsync());
    }

    /// <summary>
    /// Discarding a second row replaces the offer rather than queueing one — single-level means the
    /// most recent, and an "Undo" naming a character two discards ago is worse than none.
    /// </summary>
    [Fact]
    public async Task ASecondDiscardReplacesTheOffer()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        Assert.Equal("The Quiet Hour", Buffer(ctx).UndoLabel);
    }

    /// <summary>
    /// <b>Self-closing on the same rule the session's buffer uses.</b> The window is armed at
    /// <see cref="CharacterSession.Version"/> and requires an exact match, so the next edit to
    /// anything closes it rather than leaving an offer standing indefinitely.
    /// </summary>
    [Fact]
    public async Task AnEditClosesTheWindow()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        Assert.True(Buffer(ctx).CanUndo);

        ctx.Session.Sheet.Name = "Something else";
        ctx.Session.NotifyChanged();

        Assert.False(Buffer(ctx).CanUndo);
    }

    /// <summary>
    /// A character with nothing on it arms nothing — <see cref="CharacterSession.IsWorthKeeping"/>,
    /// the same predicate the session's own buffer uses, so the two cannot come to disagree about
    /// what is worth an offer.
    /// </summary>
    [Fact]
    public async Task AnEmptyCharacterArmsNothing()
    {
        await using var ctx = new RenderContext();

        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "Untouched", new CharacterSheet(), SheetMode.Hero);

        Assert.False(Buffer(ctx).CanUndo);
    }

    /// <summary>
    /// <b>A refusal is reported rather than swallowed, and the account cap is the refusal that
    /// happens.</b> Discard a row from a full account, build something in its place, and there is
    /// no room to put the old one back. An undo that silently did nothing is the worst outcome
    /// available: the reader believes their character is there.
    /// </summary>
    [Fact]
    public async Task ARestoreIntoAFullAccountIsRefusedRatherThanSilentlyLost()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 2;

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var doomed = SavedCharacters.NewId();
        await account.SaveAsync(doomed, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(SavedCharacters.NewId(), "Kept", SampleCharacters.Villain(), SheetMode.Villain);

        await Buffer(ctx).RememberAsync(doomed, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await Store(ctx).DeleteAsync(doomed);

        // The room the discard made is taken by something else, which is the whole scenario.
        await account.SaveAsync(SavedCharacters.NewId(), "Built instead", SampleCharacters.Hero(), SheetMode.Hero);

        Assert.False(await Buffer(ctx).UndoAsync());
        Assert.Null(await Store(ctx).ReadAsync(doomed));

        // The positive control: with room, the identical call succeeds — so the refusal above is
        // the cap and not a restore that never worked.
        ctx.Api.Limit = 9;
        await Buffer(ctx).RememberAsync(doomed, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        Assert.True(await Buffer(ctx).UndoAsync());
    }

    /// <summary>
    /// <b>An offer does not survive a change of who is here.</b> The store chooses the account or
    /// this browser per call, from whoever is signed in <em>now</em> — so an offer left standing
    /// across a sign-out would write an account's character into the anonymous slot. The key is
    /// captured and compared rather than an event being listened for, which is the decision
    /// <see cref="RulebookReader"/> already made for the same reason.
    /// </summary>
    [Fact]
    public async Task AnOfferDoesNotSurviveSigningOut()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var id = SavedCharacters.NewId();
        await Buffer(ctx).RememberAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        // Signed out through the app's own path, so the identity the store and the buffer ask for
        // really has changed rather than the stub alone having moved.
        await ctx.Services.GetRequiredService<Accounts>().SignOutAsync();

        Assert.False(await Buffer(ctx).UndoAsync());

        // And nothing was written into the browser's own slot on the way past.
        Assert.Null(await Store(ctx).ReadAsync(id));
    }

    /// <summary>
    /// The banner offers it, and clicking it puts the character back — the one place undo is
    /// offered, shared with the session's own rather than opening a second region.
    /// </summary>
    [Fact]
    public async Task TheBannerOffersItAndPutsTheCharacterBack()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var layout = ctx.Render<MainLayout>();

        // The positive control: nothing about undo before anything is discarded, so the appearance
        // below is a real change of state rather than a message that was always there.
        Assert.DoesNotContain("Undo", layout.Find(".save-status").TextContent, StringComparison.Ordinal);

        var id = SavedCharacters.NewId();
        await Buffer(ctx).RememberAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        layout.Render();

        var status = layout.Find(".save-status").TextContent;
        Assert.Contains("Ninth Precinct", status, StringComparison.Ordinal);
        Assert.Contains("discarded", status, StringComparison.Ordinal);

        await layout.FindAll(".save-status button")
            .Single(b => b.TextContent.Contains("Undo", StringComparison.Ordinal))
            .ClickAsync();

        Assert.NotNull(await Store(ctx).ReadAsync(id));
        Assert.Contains("is back", layout.Find(".save-status").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal reaches the reader in words. The region says what happened rather than the offer
    /// simply vanishing, which is indistinguishable from having worked.
    /// </summary>
    [Fact]
    public async Task TheBannerSaysSoWhenItCouldNotPutOneBack()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 1;

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(SavedCharacters.NewId(), "Built instead", SampleCharacters.Hero(), SheetMode.Hero);

        var layout = ctx.Render<MainLayout>();
        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        layout.Render();

        await layout.FindAll(".save-status button")
            .Single(b => b.TextContent.Contains("Undo", StringComparison.Ordinal))
            .ClickAsync();

        var status = layout.Find(".save-status").TextContent;
        Assert.Contains("could not be put back", status, StringComparison.Ordinal);
        Assert.Contains("Ninth Precinct", status, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The session's own undo wins the region.</b> The two can never be armed by one click —
    /// discarding a background row raises no change event, so it cannot touch the session's
    /// Version — but if both were somehow armed, the one whose window closes on the very next edit
    /// is the one more likely to be about what just happened.
    /// </summary>
    [Fact]
    public async Task TheSessionsOwnUndoWinsTheRegion()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Session.LoadSample(SheetMode.Hero);

        var layout = ctx.Render<MainLayout>();

        await Buffer(ctx).RememberAsync(
            SavedCharacters.NewId(), "A discarded row", SampleCharacters.Villain(), SheetMode.Villain);

        // Through the renderer's dispatcher: the layout subscribes to the session's change event,
        // so raising it off-dispatcher throws rather than redrawing.
        await layout.InvokeAsync(() => ctx.Session.StartAgain());

        // The positive control, and it earned itself: `StartAgain` raises `Changed` *before* it
        // fills the buffer, so the redraw that event triggers still sees `CanUndo` false. In the
        // app the completed write-through fires `Saved` a moment later and the offer appears then;
        // here the render has to be asked for, or this test reads a region that is one pass stale.
        Assert.True(ctx.Session.CanUndo);
        layout.Render();

        var status = layout.Find(".save-status").TextContent;
        Assert.Contains("was replaced", status, StringComparison.Ordinal);
        Assert.DoesNotContain("A discarded row", status, StringComparison.Ordinal);
    }
}
