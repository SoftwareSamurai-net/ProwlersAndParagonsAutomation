using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The character the pointer names, and what happens when this browser has not got it.</b>
///
/// <para><b>This is the state <c>CampaignSubmissionTests</c> left open.</b> That file stopped a
/// campaign act from being taken on a session that had been emptied by a failed read; the state
/// itself remained, because neither <c>SignIn.razor</c> nor the app's own boot restore does
/// anything about it. Both empty the sheet when the account's character cannot be read and
/// neither moves the current-character pointer — correctly, the character is still on the server —
/// so a signed-in reader is looking at an empty builder the app believes is their character, with
/// nothing on screen saying so and the next edit autosaving under that character's id.</para>
///
/// <para><b>Two halves, and the second is the one that costs a character.</b> The app says on
/// screen that the character could not be loaded and offers a way back to it; and the write-through
/// refuses to write anything at all over an id whose character this browser never read.
/// <c>CampaignSubmissionTests.TheAutosaveRefusesToWriteAnEmptySheetOverAStoredCharacter</c> covers
/// the moment straight afterwards, while the sheet is still empty; <b>that guard stops applying
/// the instant somebody types a name</b>, which is what these tests are about.</para>
///
/// <para><b>Every failed read here is produced by the app</b> — through the sign-in page, or
/// through the same three lines <c>Program.cs</c> boots with — rather than arranged by hand. The
/// first reproduction of the reported defect called <c>StartAgain</c> directly and would have gone
/// on passing over a sign-in page that had stopped doing so.</para>
/// </summary>
public sealed class SessionHoldsThePointerTests
{
    /// <summary>The reported row's own id, so this file is about the character that was lost.</summary>
    private const string JetstreamId = "c_L1Omk2RUC1om6IEvsnPcRg";

    /// <summary>Jetstream, as the account holds it: statted, named, costed.</summary>
    private static CharacterSheet Jetstream() => new()
    {
        SelectedTierId = "low_level",
        TraitCapRank = 10,
        Name = "Jetstream",
        AbilityRanks = { ["might"] = 4, ["agility"] = 6, ["awareness"] = 3 },
        TalentRanks = { ["covert"] = 3 },
        SelectedPowers = { new SelectedPower("flight", 5) },
    };

    /// <summary>
    /// A signed-in account holding Jetstream, with this browser's pointer on it.
    ///
    /// <para>Written through <see cref="ApiCharacterStore"/> rather than into the stub's own
    /// dictionary, so nothing here can pass against a store that has stopped writing.</para>
    /// </summary>
    private static async Task<RenderContext> AnAccountHoldingJetstream()
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_player", "Billy");

        Assert.Equal(
            SaveOutcome.Saved,
            await ctx.Services.GetRequiredService<ApiCharacterStore>()
                .SaveAsync(JetstreamId, "Jetstream", Jetstream(), SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(JetstreamId);

        return ctx;
    }

    /// <summary>The bytes the account is holding under one id, as the server holds them.</summary>
    private static async Task<string> StoredPayload(RenderContext ctx, string id) =>
        await ctx.Services.GetRequiredService<HttpClient>()
            .GetStringAsync($"/api/characters/{id}", Xunit.TestContext.Current.CancellationToken);

    /// <summary>
    /// The player follows a sign-in link while the read of their character fails.
    ///
    /// <para><b>Driven through the page that produces the state</b>, for the reason
    /// <c>CampaignSubmissionTests</c> gives: an arrangement that calls <c>StartAgain</c> by hand
    /// goes on passing over a sign-in page that has stopped calling it. The network is put back
    /// afterwards — the failure is a moment, not a state.</para>
    /// </summary>
    private static async Task TheSignInReadFailed(RenderContext ctx)
    {
        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("signin?t=a-link");

        var signIn = ctx.Render<SignIn>();

        // The positive control on the fixture: the page really did spend the token and really did
        // empty the session. A sign-in that quietly did nothing would leave Jetstream on screen
        // and every assertion below would hold for the wrong reason.
        Assert.Contains("Signed in", signIn.Markup, StringComparison.Ordinal);
        Assert.True(ctx.Session.HasNothingOnIt(ctx.Session.Sheet),
            "the sign-in page did not empty the session, so the reported state is not reached");

        ctx.Api.BeforeAnsweringCharacter = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// The same state through the app's own boot, which is the commoner way into it.
    ///
    /// <para><b>These are <c>Program.cs</c>'s own three lines and not a paraphrase of them</b>:
    /// the restore reads the open character through <see cref="ICharacterStore"/>, puts it on
    /// screen if it came, and leaves the session empty if it did not — without moving the pointer,
    /// because the character is still there. bUnit cannot run the host's startup, so the honest
    /// thing available is to take the same path it takes before the first render.</para>
    /// </summary>
    private static async Task TheBootRestoreFailed(RenderContext ctx)
    {
        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        var restored = await ctx.Services.GetRequiredService<ICharacterStore>().LoadAsync();

        if (restored is { } theirs)
        {
            ctx.Session.RestoreBeforeFirstRender(theirs.Sheet, theirs.Mode, JetstreamId);
        }

        ctx.Api.BeforeAnsweringCharacter = null;

        // The control on the fixture, and it is the whole of what the boot path contributes: the
        // read really did fail, so the branch that puts a character on screen really was not taken.
        Assert.Null(restored);
        Assert.True(ctx.Session.HasNothingOnIt(ctx.Session.Sheet));
    }

    /// <summary>Types a name into the finishing step, the way a reader does.</summary>
    private static async Task Name(IRenderedComponent<Finishing> page, string name) =>
        await page.Find("#ft-name").InputAsync(new ChangeEventArgs { Value = name });

    /// <summary>
    /// <b>The split is said on screen, and it says what to do about it.</b>
    ///
    /// <para>Nothing said it before: a reader whose character could not be read was handed an
    /// empty builder that looked exactly like having started a new one, on an app that believed
    /// the empty sheet was their character.</para>
    /// </summary>
    [Fact]
    public async Task AFailedSignInReadIsSaidOnScreenAndOffersTheManager()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        await TheSignInReadFailed(ctx);

        // The state itself, before anything is drawn: the pointer still names Jetstream and the
        // session is not holding it. This is the disagreement the sentence below is about.
        Assert.Equal(
            JetstreamId,
            await ctx.Services.GetRequiredService<AccountCharacterStore>().CurrentIdAsync());
        Assert.False(ctx.Session.HoldsTheCharacterAt(JetstreamId));

        var layout = ctx.Render<MainLayout>();

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "could not be loaded", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        var status = layout.Find(".save-status");

        Assert.Contains("has not been kept", status.TextContent, StringComparison.Ordinal);
        Assert.Equal("build/characters", status.QuerySelector("a")?.GetAttribute("href"));
        Assert.Contains(
            status.QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Try again");
    }

    /// <summary>
    /// <b>The same, reached through the app's own boot rather than through the sign-in page.</b>
    ///
    /// <para>It is the same fault by the other route, and it is the route most readers take: the
    /// tab is opened, the restore fails, and the app draws an empty builder under a pointer that
    /// still names a character.</para>
    /// </summary>
    [Fact]
    public async Task AFailedBootRestoreIsSaidOnScreenToo()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        await TheBootRestoreFailed(ctx);

        var layout = ctx.Render<MainLayout>();

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "could not be loaded", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The loss itself: a half-built sheet must not land on the character the pointer names.</b>
    ///
    /// <para><b>This is the hole the earlier guard leaves.</b> That one refuses a sheet with
    /// literally nothing on it, which covers the moment straight after the failed read and stops
    /// covering it the instant a reader types a name — and typing a name is the first thing
    /// somebody who thinks they are starting a new character does. From there the write-through
    /// put an unnamed stranger's opening moves straight over a fully statted character.</para>
    ///
    /// <para><b>The stored bytes are compared, not the store's opinion of them</b>, and the
    /// refusal is asserted on screen as well: a write that is declined silently leaves the reader
    /// typing into a sheet that is not being kept.</para>
    /// </summary>
    [Fact]
    public async Task TheAutosaveWillNotWriteAHalfBuiltSheetOverTheUnreadCharacter()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<MainLayout>();
        var finishing = ctx.Render<Finishing>();

        await Name(finishing, "A new character");

        // The control on the act: the edit really did land on the sheet, and the sheet really is
        // past the point where the empty-sheet guard would answer for it. Without both, the
        // assertions below hold for a page that did nothing.
        Assert.Equal("A new character", ctx.Session.Sheet.Name);
        Assert.False(ctx.Session.HasNothingOnIt(ctx.Session.Sheet));

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "Jetstream was not overwritten", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));
    }

    /// <summary>
    /// The same, from the boot path — because the two ways in must not be two behaviours.
    /// </summary>
    [Fact]
    public async Task TheAutosaveWillNotWriteOverTheUnreadCharacterAfterAFailedBoot()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        var before = await StoredPayload(ctx, JetstreamId);

        await TheBootRestoreFailed(ctx);

        var finishing = ctx.Render<Finishing>();

        await Name(finishing, "A new character");

        Assert.Equal("A new character", ctx.Session.Sheet.Name);

        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));
    }

    /// <summary>
    /// <b>A retry that lands puts the character back on screen and says which one it is.</b>
    ///
    /// <para>Re-adopting is the answer wherever it is available — the campaigns page already
    /// prefers it to refusing — and it is what ends the split rather than merely reporting it.
    /// The sentence is owed because the reader is handed a different character than the one they
    /// were looking at, which is exactly what this region announces for a sample or an import.</para>
    /// </summary>
    [Fact]
    public async Task ARetryThatLandsPutsTheCharacterBackAndSaysSo()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<MainLayout>();

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            layout.Find(".save-status").QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Try again"));

        await layout.Find(".save-status")
            .QuerySelectorAll("button")
            .Single(b => b.TextContent.Trim() == "Try again")
            .ClickAsync(new MouseEventArgs());

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "Jetstream is back on screen", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        // The sheet really is the stored character, and the session says so — which is the claim
        // the whole item is about.
        Assert.Equal("Jetstream", ctx.Session.Sheet.Name);
        Assert.True(ctx.Session.HoldsTheCharacterAt(JetstreamId));

        // And the app is writing again: the split is over, so an ordinary edit lands.
        var finishing = ctx.Render<Finishing>();
        await Name(finishing, "Jetstream II");

        await finishing.WaitForAssertionAsync(async () => Assert.Contains(
            "Jetstream II", await StoredPayload(ctx, JetstreamId), StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A retry that does not land says so, and keeps the two ways out on screen.</b>
    ///
    /// <para>A control that reports nothing is indistinguishable from one that is not wired up —
    /// and a sentence that replaced the offer would take the retry away from the reader on the one
    /// press where they most obviously want it again.</para>
    /// </summary>
    [Fact]
    public async Task ARetryThatFailsSaysSoAndLeavesTheOfferUp()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<MainLayout>();

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            layout.Find(".save-status").QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Try again"));

        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        await layout.Find(".save-status")
            .QuerySelectorAll("button")
            .Single(b => b.TextContent.Trim() == "Try again")
            .ClickAsync(new MouseEventArgs());

        ctx.Api.BeforeAnsweringCharacter = null;

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "It still could not be read", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        Assert.Contains(
            layout.Find(".save-status").QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Try again");
    }

    /// <summary>
    /// <b>The control that keeps all of this from refusing ordinary work</b>: a pointer on a slot
    /// the account has never written to reads back as nothing there, which is the first save of
    /// every new character there has ever been. It must be written, and nothing must be said.
    ///
    /// <para>Refusing here was the shape available and it is wrong for exactly the reason the
    /// campaigns page's own guard records: an account with no characters yet mints an id and
    /// never loads anything into the session, so a check that could not tell "unreachable" from
    /// "not there" would break the flow it was built to protect.</para>
    /// </summary>
    [Fact]
    public async Task AFreshSlotThatReadsBackAsNothingIsStillWritten()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        const string fresh = "c_5555555555555555555555";
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(fresh);

        // The read the app takes on the way in, and it answers "there is nothing at that id" —
        // the ordinary 404, not a failure.
        Assert.Null(await ctx.Services.GetRequiredService<ICharacterStore>().LoadAsync());

        var layout = ctx.Render<MainLayout>();
        var finishing = ctx.Render<Finishing>();

        await Name(finishing, "Somebody new");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        await finishing.WaitForAssertionAsync(async () => Assert.NotNull(await account.LoadAsync(fresh)));

        Assert.DoesNotContain(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing is said to somebody who is not signed in</b>, and the browser's own store cannot
    /// be in this state at all: local storage either holds the character or does not.
    /// </summary>
    [Fact]
    public async Task NothingIsSaidToAnAnonymousReader()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        var layout = ctx.Render<MainLayout>();

        Assert.Null(await ctx.Services.GetRequiredService<ICharacterStore>().LoadAsync());
        Assert.Null(ctx.Services.GetRequiredService<AccountCharacterStore>().UnreadId);

        Assert.DoesNotContain(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal);
    }
}
