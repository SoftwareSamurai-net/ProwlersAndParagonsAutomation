using System.Text.RegularExpressions;
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
    /// The manager, as the tier page draws it. That page shows the controls and a count rather
    /// than the rows — the rows belong to the roster, which is what
    /// <see cref="OpeningAnotherCharacterEndsTheSplitAndTheBannerWithIt"/> renders instead.
    /// </summary>
    private static IRenderedComponent<ChooseTier> ManagerIn(RenderContext ctx) => ctx.Render<ChooseTier>();

    private static async Task Press(IRenderedComponent<IComponent> page, string label) =>
        await page.FindAll("button")
            .First(b => b.TextContent.Contains(label, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

    /// <summary>
    /// Builds something worth keeping on the sheet, through the renderer's dispatcher — the same
    /// step <c>StartAnotherTests</c> takes, and for the reason it records: raising the session's
    /// change event off-dispatcher throws rather than redrawing.
    /// </summary>
    private static async Task Build(IRenderedComponent<IComponent> page, RenderContext ctx, string name) =>
        await page.InvokeAsync(() =>
        {
            ctx.Session.Sheet.SelectedTierId = "standard";
            ctx.Session.Sheet.Name = name;
            ctx.Session.NotifyChanged();
        });

    private static AccountCharacterStore StoreIn(RenderContext ctx) =>
        ctx.Services.GetRequiredService<AccountCharacterStore>();

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
        Assert.Equal("characters", status.QuerySelector("a")?.GetAttribute("href"));
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

    // ── The other write at the pointer, and the two ways this fact can be stale ─────────────

    /// <summary>
    /// <b>"Start a new character" is the write at the pointer that is not the autosave, and it
    /// went straight through the guard.</b>
    ///
    /// <para><c>StartAnotherAsync</c> keeps what is on screen by writing it down at
    /// <c>CurrentIdAsync()</c> through the four-argument <c>SaveAsync</c> — deliberately, because
    /// the fire-and-forget autosave is nobody's guarantee — so the write-through's own refusal
    /// never sees it. From the split state that is the same loss through another button: the
    /// stranger on screen lands on the character nothing in this browser has read.</para>
    ///
    /// <para><b>Asserted on what the store received</b>, not on what the page says about it: the
    /// bytes the account holds for Jetstream are the bytes it held before the click.</para>
    /// </summary>
    [Fact]
    public async Task StartingAnotherIsRefusedWhileTheCharacterBehindThePointerIsUnread()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        var page = ManagerIn(ctx);

        // Something on screen worth keeping, which is what makes this the dangerous case: the
        // empty-sheet guard stopped answering for this sheet the moment it was named.
        await Build(page, ctx, "A new character");

        Assert.True(CharacterSession.IsWorthKeeping(ctx.Session.Sheet));

        await Press(page, "Start a new character");

        // Said under the button, because a keep that does nothing reads as a broken control.
        await page.WaitForAssertionAsync(() => Assert.Contains(
            "could not be loaded into this browser", page.Markup, StringComparison.Ordinal));

        // And it does not say the two things that would be lies here: that the character was
        // saved, or that waiting will help.
        Assert.DoesNotContain("Your character is saved", page.Markup, StringComparison.Ordinal);

        // The whole of the claim: the account's bytes for Jetstream are untouched.
        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));

        // Refused whole rather than half — the pointer did not move either, so the sheet on
        // screen has not been quietly re-homed into a slot nothing wrote it to.
        Assert.Equal(JetstreamId, await StoreIn(ctx).CurrentIdAsync());
        Assert.Equal("A new character", ctx.Session.Sheet.Name);
    }

    /// <summary>
    /// <b>An untouched sheet goes through, into a slot of its own rather than the unread one.</b>
    ///
    /// <para>This is the commonest press of that button from this state — the session was emptied
    /// a moment ago by the failed read — and there is nothing to write down, so nothing can be
    /// lost. What may not happen is the ordinary reuse of the slot: it is not empty, it holds a
    /// character this browser has not got, so building into it would leave every save refused.</para>
    /// </summary>
    [Fact]
    public async Task StartingAnotherFromAnEmptySheetOpensAFreshSlotRatherThanReusingTheUnreadOne()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        // The control on the arrangement: the slot about to be reused is Jetstream's own.
        Assert.Equal(JetstreamId, await StoreIn(ctx).CurrentIdAsync());

        var page = ManagerIn(ctx);

        await Press(page, "Start a new character");

        var fresh = await StoreIn(ctx).CurrentIdAsync();

        Assert.NotEqual(JetstreamId, fresh);
        Assert.Null(StoreIn(ctx).UnreadId);
        Assert.DoesNotContain(
            "could not be loaded into this browser", page.Markup, StringComparison.Ordinal);

        // And the reader can actually work in it: what they build lands at the fresh id, and
        // Jetstream is where it was.
        var finishing = ctx.Render<Finishing>();
        await Name(finishing, "Somebody new");

        await finishing.WaitForAssertionAsync(async () => Assert.Contains(
            "Somebody new", await StoredPayload(ctx, fresh), StringComparison.Ordinal));

        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));
    }

    /// <summary>
    /// <b>Opening another character ends the split, and the banner has to stop saying it.</b>
    ///
    /// <para><c>UnreadId</c> is a fact about the <em>pointer</em> — "the character it names could
    /// not be read into this browser" — and the banner read it without asking where the pointer
    /// is. So a reader who took the way out the banner itself offers, and opened one of their
    /// characters from the manager, went on being told that their character could not be loaded
    /// while looking straight at a character that had loaded perfectly — beside a "Try again"
    /// that would have switched them away from it.</para>
    /// </summary>
    [Fact]
    public async Task OpeningAnotherCharacterEndsTheSplitAndTheBannerWithIt()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        const string bulwarkId = "c_3333333333333333333333";

        Assert.Equal(
            SaveOutcome.Saved,
            await ctx.Services.GetRequiredService<ApiCharacterStore>().SaveAsync(
                bulwarkId, "Bulwark",
                new CharacterSheet { SelectedTierId = "low_level", Name = "Bulwark" },
                SheetMode.Hero));

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<MainLayout>();

        // The control: the sentence really is up before the reader does anything about it.
        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal));

        // The way out, taken through the page: the manager's rows are the buttons that open them,
        // and the character that is open is not one of them. The roster, because the tier page
        // draws a count and a link where the rows are.
        var page = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.Roster>();

        await page.WaitForAssertionAsync(() => Assert.Contains(
            page.FindAll("button.open-target"),
            b => b.TextContent.Contains("Bulwark", StringComparison.Ordinal)));

        await page.FindAll("button.open-target")
            .Single(b => b.TextContent.Contains("Bulwark", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        Assert.Equal("Bulwark", ctx.Session.Sheet.Name);
        Assert.Equal(bulwarkId, await StoreIn(ctx).CurrentIdAsync());

        // The fact is gone, and so is the sentence.
        Assert.Null(StoreIn(ctx).UnreadId);

        await layout.WaitForAssertionAsync(() => Assert.DoesNotContain(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An id this browser minted is not a character it could not get.</b>
    ///
    /// <para><c>AdoptAnIdAsync</c> mints when the account's list comes back with nothing in it —
    /// <em>including</em> when it comes back with nothing because the server could not be asked.
    /// So a new account signing in during an outage minted an id, failed to read it, and had that
    /// counted as a split: the first thing that account was told, before it had a character at
    /// all, was that its character could not be loaded and that what is on screen is not being
    /// kept.</para>
    ///
    /// <para><b>What the mutation on <c>_minted</c> actually costs, measured rather than
    /// assumed</b>: the banner and the store's own answer, not the write. Removing the exception
    /// leaves both wrong and the save still lands, because
    /// <c>WouldWriteOverACharacterNothingRead</c> ends the split itself once a list read succeeds
    /// and finds nothing worth protecting behind the pointer — so the write is refused only while
    /// the list is unreachable too, which is a moment in which no write could land anyway. The
    /// last two assertions here are the control that the account can still work, and the two
    /// above them are the guard.</para>
    /// </summary>
    [Fact]
    public async Task AnIdThisBrowserMintedIsNotACharacterItCouldNotRead()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_newcomer", "Newcomer");

        // Asked once and remembered, so the outage below is about the character routes rather
        // than about identity — which is a different failure with a different answer.
        Assert.True((await ctx.Services.GetRequiredService<IIdentitySource>().CurrentAsync()).IsSignedIn);

        ctx.Api.Unreachable = true;

        // The boot read, on an account with nothing stored and a server that cannot be asked.
        Assert.Null(await ctx.Services.GetRequiredService<ICharacterStore>().LoadAsync());

        var minted = await StoreIn(ctx).CurrentIdAsync();

        // The control on the arrangement: an id really was minted, so the read that failed was of
        // a slot this browser made up rather than of the pointer's own default.
        Assert.NotEqual(SavedCharacters.LegacyId, minted);

        Assert.Null(StoreIn(ctx).UnreadId);

        var layout = ctx.Render<MainLayout>();

        Assert.DoesNotContain(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal);

        // And the first character this account ever builds is written rather than refused.
        ctx.Api.Unreachable = false;

        var finishing = ctx.Render<Finishing>();
        await Name(finishing, "Newcomers first");

        await finishing.WaitForAssertionAsync(async () => Assert.Contains(
            "Newcomers first", await StoredPayload(ctx, minted), StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Signing out takes the sentence with it, and the store's answer is deliberately still
    /// there while it does.</b>
    ///
    /// <para><c>NothingIsSaidToAnAnonymousReader</c> is a control on the whole feature and not on
    /// this check: it renders an app in which nothing has ever failed to read, so
    /// <c>UnreadId</c> is null and the banner would stay quiet with the identity check taken
    /// out. The account's own answer outlives a sign-out — nothing clears it, and nothing should,
    /// since signing back in finds the same character behind the same pointer — so the one thing
    /// keeping it off an anonymous reader's screen is <c>MainLayout.Split</c> asking who is here.
    /// This is the test of that.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutTakesTheSplitNoticeWithIt()
    {
        await using var ctx = await AnAccountHoldingJetstream();

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<MainLayout>();

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal));

        // Out through the button that does it. The address is cleared of the spent token first,
        // so this renders the signed-in page rather than spending a link again.
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("signin");

        var signIn = ctx.Render<SignIn>();

        await signIn.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Sign out")
            .ClickAsync(new MouseEventArgs());

        // The fact itself is untouched — which is what makes this a test of the check on screen
        // rather than of the store having quietly forgotten.
        Assert.Equal(JetstreamId, StoreIn(ctx).UnreadId);

        await layout.WaitForAssertionAsync(() => Assert.DoesNotContain(
            "could not be loaded", layout.Find(".save-status").TextContent, StringComparison.Ordinal));
    }

    // ── The fixture that hand-copies the app's boot ────────────────────────────────────────

    /// <summary>
    /// <b><see cref="TheBootRestoreFailed"/> is three lines copied out of <c>web/Program.cs</c>,
    /// and a copy nothing checks is a fixture that goes on testing a boot the app has stopped
    /// having.</b>
    ///
    /// <para>bUnit cannot run the host's startup, so the mirror is the honest thing available —
    /// but the moment the real restore stops reading through <c>ICharacterStore.LoadAsync()</c>,
    /// or starts doing something about a read that answered null, every test above it goes on
    /// passing against a path nobody takes. This is what turns that into a red test in the same
    /// minute.</para>
    ///
    /// <para><b>What it cannot do</b>, said plainly because <c>CLAUDE.md</c> requires it: this is
    /// a scan for three spellings, and it has no opinion about what the lines between them do. A
    /// boot that kept all three and added a fourth line moving the pointer would walk straight
    /// through it. It is the cheap catch on the mirror drifting, not a proof that the mirror is
    /// the boot.</para>
    /// </summary>
    [Fact]
    public void TheBootThisFileMirrorsIsStillTheBootTheAppHas()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "web", "Program.cs"));

        foreach (var (what, pattern) in BootLines)
        {
            Assert.True(pattern.IsMatch(program),
                $"`web/Program.cs` no longer {what}, so `TheBootRestoreFailed` is mirroring a boot "
                + "the app has stopped having and every test driven through it is passing against "
                + $"a path nobody takes. Pattern: {pattern}");
        }
    }

    /// <summary>
    /// The positive control on the scan above, and it is not ceremony: a pattern that has stopped
    /// matching anything at all would fail loudly, but one that has been loosened until it matches
    /// a boot that does something else entirely would not. Each is asserted to reject the edit it
    /// exists to catch.
    /// </summary>
    [Fact]
    public void TheBootScanRejectsABootThatDoesSomethingElse()
    {
        var (readsThrough, branchesOnIt, putsItOnScreen) =
            (BootLines[0].Pattern, BootLines[1].Pattern, BootLines[2].Pattern);

        // Reading one character by id is not reading "the open character", and it is the read
        // that does not record the split.
        Assert.DoesNotMatch(readsThrough, "    saved = await store.LoadAsync(theirId);");

        // A boot that acted on the read without checking it came back is the defect itself.
        Assert.DoesNotMatch(branchesOnIt, "    if (saved is not null || restored)");

        // And restoring something other than what the read answered is the shape that puts a
        // sheet on screen the app never fetched.
        Assert.DoesNotMatch(
            putsItOnScreen,
            "        session.RestoreBeforeFirstRender(\n            new CharacterSheet(), SheetMode.Hero,");

        // ...while each still matches the line it is about, so "rejects everything" cannot pass
        // for "rejects the edit".
        Assert.Matches(readsThrough, "    saved = await store.LoadAsync();");
        Assert.Matches(branchesOnIt, "    if (saved is { } restored)");
        Assert.Matches(
            putsItOnScreen,
            "        session.RestoreBeforeFirstRender(\n            restored.Sheet, restored.Mode,");
    }

    /// <summary>
    /// The three lines of <c>web/Program.cs</c> that <see cref="TheBootRestoreFailed"/> stands in
    /// for: the open character is read through <c>ICharacterStore</c>'s no-argument
    /// <c>LoadAsync</c>, a character is put on screen only if one came back, and it is put there
    /// with <c>RestoreBeforeFirstRender</c> carrying the sheet and mode that read answered.
    /// </summary>
    private static readonly (string What, Regex Pattern)[] BootLines =
    [
        ("reads the open character through `ICharacterStore.LoadAsync()`",
            new Regex(@"saved\s*=\s*await\s+store\.LoadAsync\(\s*\)\s*;", RegexOptions.None,
                TimeSpan.FromSeconds(5))),

        ("puts a character on screen only when the read answered one",
            new Regex(@"if\s*\(\s*saved\s+is\s*\{\s*\}\s*restored\s*\)", RegexOptions.None,
                TimeSpan.FromSeconds(5))),

        ("restores what that read answered, with `RestoreBeforeFirstRender`",
            new Regex(@"session\.RestoreBeforeFirstRender\(\s*restored\.Sheet,\s*restored\.Mode,",
                RegexOptions.None, TimeSpan.FromSeconds(5))),
    ];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
