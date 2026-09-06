using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>What "Send for approval" actually sends, and which character it describes.</b>
///
/// <para><b>This file exists because of one row in production.</b> A GM opened an approved sheet
/// on their campaign page and got an unnamed, completely empty character — Low Level, Trait Cap
/// 10d, every Trait 0d — while the player who sent it holds a fully statted one under exactly the
/// id that row names. The membership's <c>approved_payload</c> was 379 characters: an envelope
/// carrying nothing but the three fields <c>CampaignJoin.Apply</c> copies in from the campaign
/// (its tier, its id, its house Trait Cap). The label on the same row said "Jetstream".</para>
///
/// <para><b>The label and the payload came from different places, and that is the defect.</b>
/// The page sent <c>Session.Sheet</c> — the character on screen — into a membership keyed on
/// <c>AccountCharacterStore.CurrentIdAsync()</c>, the browser's current-character pointer. Those
/// two are supposed to name the same character and there is nothing that makes them: the sign-in
/// page empties the session when the account's character cannot be read
/// (<c>SignIn.razor</c>: <c>if (await Store.LoadAsync() is { } theirs) Session.Open(…); else
/// Session.StartAgain();</c>) and leaves the pointer exactly where it was, and the app's own boot
/// restore in <c>Program.cs</c> does the same for a read that throws. From there the row for a
/// real character offers "Send for approval", and pressing it sends the empty sheet under that
/// character's name.</para>
///
/// <para><b>Every test here presses the button a person presses</b>, against a storage that really
/// stores — <c>RenderContext(storesForReal: true)</c>, for the reason that parameter records: the
/// pointer has to be a real id, and bUnit's recorder answers null to every read.</para>
/// </summary>
public sealed class CampaignSubmissionTests
{
    private const string CampaignId = "g_EQVHwU_Bl0VZdFrEssjk0A";

    /// <summary>The owner's own ids, so the row under test is the row that was reported.</summary>
    private const string JetstreamId = "c_L1Omk2RUC1om6IEvsnPcRg";

    private const string SubjectId = "c_2222222222222222222222";

    /// <summary>Jetstream, as the player's account actually holds it: statted, named, costed.</summary>
    private static CharacterSheet Jetstream() => new()
    {
        SelectedTierId = "low_level",
        TraitCapRank = 10,
        Name = "Jetstream",
        AbilityRanks = { ["might"] = 4, ["agility"] = 6, ["awareness"] = 3 },
        TalentRanks = { ["covert"] = 3 },
        SelectedPowers = { new SelectedPower("flight", 5) },
    };

    private static CharacterSheet SubjectX02() => new()
    {
        SelectedTierId = "low_level",
        TraitCapRank = 10,
        Name = "Subject X-02",
        AbilityRanks = { ["might"] = 6, ["resilience"] = 5 },
    };

    /// <summary>
    /// A character somebody has started from the Powers step: one Power bought, no Ability rank
    /// recorded, no name yet. <b>This is a character</b>, and the predicate that called it empty
    /// is what finding 2 is about.
    /// </summary>
    private static CharacterSheet PowersOnly() => new()
    {
        SelectedTierId = "low_level",
        TraitCapRank = 10,
        SelectedPowers = { new SelectedPower("flight", 4) },
    };

    /// <summary>
    /// The other end of the same objection: every Ability bought, all of them at the rulebook's
    /// own floor of 1d. Nothing is below the minimum, so nothing about it reads as untouched —
    /// and it is the control that keeps the Abilities half of the question honest.
    /// </summary>
    private static CharacterSheet EveryAbilityAtItsFloor(RulesRepository rules)
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId = "street_level",
            TraitCapRank = 8,
        };

        foreach (var ability in rules.Abilities) sheet.AbilityRanks[ability.Id] = 1;

        return sheet;
    }

    /// <summary>
    /// A game somebody else runs, a player signed in to it, and the player's own two characters
    /// on the server — with this browser's current-character pointer on Jetstream.
    ///
    /// <para>The characters are written through <see cref="ApiCharacterStore"/> rather than into
    /// the stub's dictionary, so a test here cannot pass against a store that has stopped
    /// writing.</para>
    /// </summary>
    private static async Task<(RenderContext Ctx, string Code)> ATableAndTwoCharacters()
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Blood & Justice",
            StoredCampaign.Write(
                new Campaign(CampaignId, "Blood & Justice", "low_level", 10, false)));

        ctx.Api.SignedIn = ("u_player", "Billy");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        Assert.Equal(SaveOutcome.Saved,
            await account.SaveAsync(SubjectId, "Subject X-02", SubjectX02(), SheetMode.Hero));
        Assert.Equal(SaveOutcome.Saved,
            await account.SaveAsync(JetstreamId, "Jetstream", Jetstream(), SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(JetstreamId);

        return (ctx, code);
    }

    /// <summary>
    /// A game somebody else runs, and one character of the caller's choosing on the player's
    /// account with this browser's pointer on it and the sheet on screen.
    ///
    /// <para>The sheet is written down and then <em>a second copy of it</em> is put on screen, so
    /// that what a join does to the character on screen cannot quietly be a change to the bytes
    /// this fixture stored.</para>
    /// </summary>
    private static async Task<(RenderContext Ctx, string Code)> ATableAndThisCharacter(
        Func<RulesRepository, CharacterSheet> build)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Blood & Justice",
            StoredCampaign.Write(
                new Campaign(CampaignId, "Blood & Justice", "low_level", 10, false)));

        ctx.Api.SignedIn = ("u_player", "Billy");

        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(SubjectId, SavedCharacters.LabelFor(build(rules)), build(rules), SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(SubjectId);

        ctx.Session.Open(build(rules), SheetMode.Hero);

        return (ctx, code);
    }

    /// <summary>Types the code and presses Join, the way the player does.</summary>
    private static void Join(IRenderedComponent<Campaigns> page, string code)
    {
        page.Find("#join-code").Input(code);
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Join").Click();
    }

    /// <summary>Presses the one "Send for approval" the page is offering.</summary>
    private static async Task Send(IRenderedComponent<Campaigns> page) =>
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim() == "Send for approval")
            .ClickAsync(new MouseEventArgs());

    /// <summary>
    /// The state the reported row was sent from: the pointer names Jetstream, and the session is
    /// empty because the character behind it never made it onto the screen.
    ///
    /// <para><b>Reached by the app, not invented here.</b> <c>SignIn.razor</c> calls
    /// <c>Session.StartAgain()</c> on both its paths whenever <c>Store.LoadAsync()</c> answers
    /// null — a read that 404s, times out, or comes back as the site's own <c>index.html</c> —
    /// and <c>Program.cs</c>'s boot restore does the same for one that throws. Neither moves the
    /// current-character pointer, because neither has any reason to: the account's character is
    /// still there and still the one this browser has open.</para>
    /// </summary>
    private static void TheCharacterNeverArrivedOnScreen(RenderContext ctx) =>
        ctx.Session.StartAgain();

    /// <summary>
    /// The same state, reached through the page that actually produces it: the player follows a
    /// sign-in link while the read of their character fails, and <c>SignIn.razor</c> empties the
    /// session and leaves the pointer where it was.
    ///
    /// <para><b>Driven rather than arranged, because the arrangement is the thing under
    /// suspicion.</b> <see cref="TheCharacterNeverArrivedOnScreen"/> above calls
    /// <c>StartAgain</c> by hand and could go on passing over a sign-in page that had stopped
    /// doing so; this one presses the app's own path and would go red if it did. The network is
    /// put back before the page under test is rendered — the failure is a moment, not a
    /// state.</para>
    /// </summary>
    private static async Task TheSignInReadFailed(RenderContext ctx)
    {
        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("signin?t=a-link");

        var signIn = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.SignIn>();

        // The positive control on the fixture: the page really did spend the token and really did
        // empty the session, which is the whole of the state the rest of the test is about. A
        // sign-in that silently did nothing would leave Jetstream on screen and make every
        // assertion below hold for the wrong reason.
        Assert.Contains("Signed in", signIn.Markup, StringComparison.Ordinal);
        Assert.True(ctx.Session.HasNothingOnIt(ctx.Session.Sheet),
            "the sign-in page did not empty the session, so the reported state is not reached");

        ctx.Api.BeforeAnsweringCharacter = null;
    }

    /// <summary>The bytes the account is holding under one id, as the server holds them.</summary>
    private static async Task<string> StoredPayload(RenderContext ctx, string id) =>
        await ctx.Services.GetRequiredService<HttpClient>()
            .GetStringAsync($"/api/characters/{id}", Xunit.TestContext.Current.CancellationToken);

    /// <summary>
    /// <b>Joining from the emptied-session state must not write the empty sheet over the
    /// character the pointer names.</b>
    ///
    /// <para>This is the loss rather than the mislabelling: <c>CampaignJoin.Apply</c> writes the
    /// campaign's tier onto whatever sheet is on screen and rings the session's bell, and the
    /// write-through that follows lands under the browser's current-character pointer — which is
    /// still Jetstream. A tier is enough for <c>IsWorthKeeping</c>, so the guard that stops empty
    /// sheets being written passes it by construction, and a fully statted character was replaced
    /// by a 379-byte envelope in the course of typing a join code.</para>
    ///
    /// <para><b>The payload is compared byte for byte, and the one difference allowed is the one
    /// the join is entitled to make</b> — the campaign id it now names. Anything else moving is
    /// the character having been rewritten.</para>
    /// </summary>
    [Fact]
    public async Task JoiningWithAnEmptiedSessionDoesNotWriteOverTheStoredCharacter()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        // The character is exactly as it was, but for the campaign it has now joined. Taking that
        // one field back out is what makes this a byte comparison rather than a field-by-field
        // one — and the assertion that it really was taken out is what stops the comparison
        // passing over a payload that never carried it.
        var after = await StoredPayload(ctx, JetstreamId);
        var withoutTheCampaign =
            after.Replace($"\"CampaignId\":\"{CampaignId}\",", "", StringComparison.Ordinal);

        Assert.NotEqual(after, withoutTheCampaign);
        Assert.Equal(before, withoutTheCampaign);

        // The row names the character it is actually about, on both halves.
        var row = Assert.Single((await ctx.Services
            .GetRequiredService<ApiMembershipStore>().MineAsync())!);

        Assert.Equal("Jetstream", row.Label);
        Assert.Equal(JetstreamId, row.CharacterId);

        // And the page said what it did rather than doing it silently.
        Assert.Contains("Jetstream was put back on screen first", page.Markup,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The other side of the same guard: when the pointer's character cannot be read, nothing
    /// is written at all.</b>
    ///
    /// <para>Re-adopting is the preferred answer and it needs a read to succeed. Where the read
    /// fails there is no way to tell a real character behind the pointer from an empty slot — so
    /// the join is refused, the character is named so the reader knows which one this is about,
    /// and the campaign never hears from this browser.</para>
    /// </summary>
    [Fact]
    public async Task AJoinIsRefusedWhenThePointersCharacterCannotBeRead()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        var page = ctx.Render<Campaigns>();

        // The network goes away again, this time for the read the join itself takes.
        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        Join(page, code);

        ctx.Api.BeforeAnsweringCharacter = null;

        Assert.Contains("Jetstream is the character this browser has open", page.Markup,
            StringComparison.Ordinal);

        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));

        // Nothing joined, so there is no row to be labelled wrongly later.
        Assert.Empty((await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync())!);
    }

    /// <summary>
    /// <b>The defect, driven through the page.</b> The player joins with Jetstream on screen; a
    /// later visit leaves the session empty with the pointer still on Jetstream; they press Send
    /// for approval on the Jetstream row — and what arrives at the server must still be Jetstream.
    ///
    /// <para><b>The assertion is that the label and the payload describe the same character.</b>
    /// Before the fix they did not: the row kept the name it was joined under and the snapshot was
    /// an empty sheet carrying nothing but the campaign's own three fields, which is the 379-byte
    /// payload the owner's GM was shown as a character.</para>
    /// </summary>
    [Fact]
    public async Task SendingForApprovalCarriesTheCharacterTheRowNames()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        TheCharacterNeverArrivedOnScreen(ctx);

        // The control on the state under test: the app really is offering to send, and it is
        // offering it on the row for the character the pointer names.
        page.Render();
        Assert.Contains("Jetstream", page.Markup, StringComparison.Ordinal);

        await Send(page);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var mine = await memberships.MineAsync();
        var row = Assert.Single(mine!);

        var detail = await memberships.ReadAsync(row.Id);

        Assert.NotNull(detail);
        Assert.NotNull(detail!.Pending);

        // One source: the name on the row and the name on the sheet are the same character's.
        Assert.Equal("Jetstream", detail.Label);
        Assert.Equal("Jetstream", detail.Pending!.Name);

        // And it is the character, not an envelope shaped like one. Ranks the player bought,
        // rather than the three fields a join copies in.
        Assert.Equal(6, detail.Pending.AbilityRanks["agility"]);
        Assert.Equal(5, detail.Pending.SelectedPowers.Single().PurchasedRanks);
    }

    /// <summary>
    /// <b>Sending a character with nothing on it is refused on the page, with a sentence.</b>
    ///
    /// <para>Reached the way a player reaches it and not by arrangement: joining a game with an
    /// empty character is the ordinary first move — the join box says so out loud, "its tier is
    /// filled in if you have not chosen one" — and the autosave that follows writes the sheet
    /// <c>CampaignJoin.Apply</c> has just put a tier on. So the character behind the row really is
    /// the 379-byte envelope, stored under a real id, and pressing Send is one click away.</para>
    ///
    /// <para><b>Nothing is repaired.</b> The refusal writes nothing, and the row is left exactly
    /// as it was for the player to send properly.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterWithNothingOnItIsRefusedRatherThanSent()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        // A brand-new slot, empty, and the pointer on it — "start a new character", then join.
        await ctx.Services.GetRequiredService<SavedCharacters>()
            .SetCurrentAsync("c_3333333333333333333333");

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        // The control on the fixture: the join really did land, and it really did put the
        // campaign's tier and cap onto the empty sheet. Without this the refusal below could be
        // a page that never reached the guard.
        Assert.Equal("low_level", ctx.Session.Sheet.SelectedTierId);
        Assert.Equal(10, ctx.Session.Sheet.TraitCapRank);

        await Send(page);

        Assert.Contains("This character has nothing on it yet", page.Markup, StringComparison.Ordinal);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var row = Assert.Single((await memberships.MineAsync())!);

        // Refused means refused: nothing is waiting, and the campaign holds nothing.
        Assert.False(row.HasPending);
        Assert.False(row.HasApproved);
    }

    /// <summary>
    /// <b>A character built out of Powers alone is a character, and it is sent.</b>
    ///
    /// <para>The refusal above asked one question — is every Ability below the rulebook's floor —
    /// and answered it of a sheet with four Powers on it and no Ability rank bought yet. That is
    /// somebody halfway through the Powers step, and it is exactly the work this application
    /// exists to keep: their send was refused with "this character has nothing on it yet", and
    /// the GM's screen captioned their clone as empty.</para>
    ///
    /// <para>The assertion is on what the server was handed — the Power and its ranks — rather
    /// than on the absence of the refusal, because a page that had stopped sending anything at
    /// all would satisfy the absence.</para>
    /// </summary>
    [Fact]
    public async Task APowersOnlyCharacterIsSentRatherThanCalledEmpty()
    {
        var (ctx, code) = await ATableAndThisCharacter(_ => PowersOnly());
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();
        Join(page, code);
        await Send(page);

        Assert.DoesNotContain("nothing on it yet", page.Markup, StringComparison.Ordinal);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var row = Assert.Single((await memberships.MineAsync())!);

        Assert.True(row.HasPending, "the powers-only character was not sent");

        var detail = await memberships.ReadAsync(row.Id);

        Assert.NotNull(detail!.Pending);
        Assert.Equal("flight", detail.Pending!.SelectedPowers.Single().PowerId);
        Assert.Equal(4, detail.Pending.SelectedPowers.Single().PurchasedRanks);
    }

    /// <summary>
    /// <b>Every Ability bought at the rulebook's own floor is a character too</b>, and the
    /// control on the Abilities half of the question: 1d across the board reports no
    /// <c>TRAIT_BELOW_MINIMUM</c> at all, so nothing here reads as untouched.
    /// </summary>
    [Fact]
    public async Task ACharacterAtEveryAbilitysFloorIsSent()
    {
        var (ctx, code) = await ATableAndThisCharacter(EveryAbilityAtItsFloor);
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();
        Join(page, code);
        await Send(page);

        Assert.DoesNotContain("nothing on it yet", page.Markup, StringComparison.Ordinal);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var row = Assert.Single((await memberships.MineAsync())!);
        var detail = await memberships.ReadAsync(row.Id);

        Assert.NotNull(detail!.Pending);
        Assert.All(ctx.Session.Rules.Abilities,
            a => Assert.Equal(1, detail.Pending!.AbilityRanks[a.Id]));
    }

    /// <summary>
    /// <b>The reported envelope is still empty under the wider definition</b>, which is what
    /// stops the two tests above from having simply turned the refusal off. Read from the owner's
    /// own bytes rather than from a sheet built here.
    /// </summary>
    [Fact]
    public void TheReportedEnvelopeIsStillNothingOnASheet()
    {
        using var ctx = new RenderContext();

        var envelope = new StoredCharacter(
            ctx.Session.Costs,
            ctx.Services.GetRequiredService<CharacterValidator>()).Read(TheReportedPayload);

        Assert.NotNull(envelope);
        Assert.True(ctx.Session.HasNothingOnIt(envelope!.Value.Sheet),
            "the payload the owner's GM was shown is no longer recognised as empty");

        // The two the definition was widened for, side by side with it.
        Assert.False(ctx.Session.HasNothingOnIt(PowersOnly()));
        Assert.False(ctx.Session.HasNothingOnIt(EveryAbilityAtItsFloor(ctx.Session.Rules)));
    }

    /// <summary>
    /// <b>A rulebook with no Abilities in it does not make every character in the app empty.</b>
    ///
    /// <para>The Abilities half compares a count of below-minimum findings against
    /// <c>Rules.Abilities.Count</c>, so with no Abilities loaded the comparison is <c>0 == 0</c>
    /// and every sheet answers "nothing on it" at once — a rules file that failed to load turning
    /// into "nobody has a character", refusals across the campaigns page and "the submission was
    /// empty" over every sheet on the GM's screen.</para>
    /// </summary>
    [Fact]
    public void ARulebookWithNoAbilitiesDoesNotCallEveryCharacterEmpty()
    {
        using var ctx = new RenderContext();

        var withoutAbilities = new RulesRepository(new InMemoryRulesSource(
            RulesRepository.DataFileNames.ToDictionary(
                name => name,
                name => name == "abilities.json" ? "[]" : RulesFile(name),
                StringComparer.Ordinal)));

        // The control on the fixture: the repository really is one with no Abilities in it, and
        // really does still answer for everything else.
        Assert.Empty(withoutAbilities.Abilities);
        Assert.NotEmpty(withoutAbilities.Powers);

        var validator = new CharacterValidator(
            withoutAbilities,
            new CostCalculator(withoutAbilities),
            new DerivedStatsCalculator(withoutAbilities));

        Assert.False(
            CharacterSession.HasNothingOnIt(Jetstream(), validator, withoutAbilities),
            "a rulebook with no Abilities made a fully statted character read as empty");

        // And the shipped rules still answer the question they are there to answer.
        Assert.True(ctx.Session.HasNothingOnIt(new CharacterSheet()));
    }

    /// <summary>One shipped rules file, by name.</summary>
    private static string RulesFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;

        return File.ReadAllText(Path.Combine(dir!.FullName, "data", "rules", name));
    }

    /// <summary>
    /// <b>The autosave will not put an empty sheet over the character the pointer names, and it
    /// says so on screen.</b>
    ///
    /// <para><b>The belt beside the campaigns page's own guard, and it is needed because the loss
    /// does not need the campaigns page.</b> Any edit at all from the emptied-session state fires
    /// the write-through — here the reader simply picks a tier, which is the first thing anybody
    /// does. Before this the empty sheet went straight over a fully statted character, and
    /// <c>IsWorthKeeping</c> could not stop it because a tier is what it counts.</para>
    ///
    /// <para><b>Refusing silently is not acceptable and is what the live region is for.</b> A
    /// reader who is not told goes on typing into a sheet that is not being kept, which is the
    /// exact state the "Saved" word exists to stop somebody being in.</para>
    /// </summary>
    [Fact]
    public async Task TheAutosaveRefusesToWriteAnEmptySheetOverAStoredCharacter()
    {
        var (ctx, _) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        var before = await StoredPayload(ctx, JetstreamId);

        await TheSignInReadFailed(ctx);

        var layout = ctx.Render<ProwlersAndParagonsAutomation.Web.Layout.MainLayout>();
        var tiers = ctx.Render<ChooseTier>();

        var first = ctx.Session.Rules.Tiers[0];

        await tiers.FindAll("button")
            .First(b => b.TextContent.Contains(first.Name, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // The control on the act: the tier really did land on the sheet, so a write really was
        // attempted. Without this the assertions below hold for a page that does nothing.
        Assert.Equal(first.Id, ctx.Session.Sheet.SelectedTierId);

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "Jetstream was not overwritten", layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        Assert.Equal(before, await StoredPayload(ctx, JetstreamId));
    }

    /// <summary>
    /// The other half of the belt, and the one that keeps it from refusing ordinary work: an
    /// empty sheet at an id nothing is stored under is written exactly as it always was. That is
    /// the first save of every new character there has ever been.
    /// </summary>
    [Fact]
    public async Task TheAutosaveStillWritesAnEmptySheetToASlotOfItsOwn()
    {
        var (ctx, _) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        const string fresh = "c_4444444444444444444444";
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(fresh);

        var tiers = ctx.Render<ChooseTier>();
        var first = ctx.Session.Rules.Tiers[0];

        await tiers.FindAll("button")
            .First(b => b.TextContent.Contains(first.Name, StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        await tiers.WaitForAssertionAsync(async () =>
            Assert.NotNull(await account.LoadAsync(fresh)));
    }

    /// <summary>
    /// <b>The submission carries the edit the player has just made, not the copy the server
    /// happens to be holding.</b>
    ///
    /// <para>Reading the character back by id made the label and the payload agree and bought
    /// that with a lag: the autosave is fire-and-forget over HTTP and nobody awaits it, so the
    /// stored copy can be a rank behind the sheet on screen at the moment Send is pressed. The
    /// player raises Agility, presses Send in the same breath, and is told it was sent — while
    /// the GM decides about the character as it was a moment ago.</para>
    ///
    /// <para><b>The write is genuinely still in flight rather than mocked away</b>: the character
    /// is opened through the manager the way a reader opens one, the rank is raised on the
    /// Abilities step by pressing its own button, and <c>FakeApi</c> holds the <c>PUT</c> open
    /// across the click on Send.</para>
    /// </summary>
    [Fact]
    public async Task SendingCarriesTheEditTheAutosaveHasNotLandedYet()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        // Opened through the manager's own control, so which character is on screen is the app's
        // answer rather than this test's. The pointer starts elsewhere, or the row that is open
        // offers no "Open" to press.
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(SubjectId);

        var manager = ctx.Render<ProwlersAndParagonsAutomation.Web.Components.CharacterManager>();

        await manager.FindAll(".open-target")
            .First(b => b.TextContent.Contains("Jetstream", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        Assert.Equal("Jetstream", ctx.Session.Sheet.Name);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        // From here the server stops finishing writes, which is the state every edit is in for
        // as long as its request is in the air.
        var held = new TaskCompletionSource();
        ctx.Api.BeforeStoringCharacter = _ => held.Task;

        var abilities = ctx.Render<Characteristics>();

        await abilities.FindAll("button")
            .First(b => b.GetAttribute("aria-label") == "Raise Agility")
            .ClickAsync(new MouseEventArgs());

        // Two controls before the act: the edit really is on the sheet the player sees, and the
        // server really has not caught up with it. Without the second this test would pass
        // against a store that had already written.
        Assert.Equal(7, ctx.Session.Sheet.AbilityRanks["agility"]);

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        Assert.Equal(6, (await account.LoadAsync(JetstreamId))!.Value.Sheet.AbilityRanks["agility"]);

        await Send(page);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var row = Assert.Single((await memberships.MineAsync())!);
        var detail = await memberships.ReadAsync(row.Id);

        Assert.NotNull(detail!.Pending);
        Assert.Equal(7, detail.Pending!.AbilityRanks["agility"]);
        Assert.Equal("Jetstream", detail.Label);

        held.SetResult();
    }

    /// <summary>
    /// <b>The other half of the same choice, and the arm that must not be traded away for the
    /// freshness above: where the sheet on screen is somebody else's character entirely, the
    /// stored copy is what goes.</b>
    ///
    /// <para>Loading a sample replaces the character on screen and deliberately does <em>not</em>
    /// move the current-character pointer — that is what its one-level undo exists for. So a
    /// reader who loads one and then opens their campaigns has a Villain on screen and their own
    /// character under the row's id, which is exactly the shape the reported row was sent from.
    /// The <c>PUT</c> is held so the sample has not yet reached the server, which is what leaves
    /// the two genuinely different and makes the assertion able to tell which was sent.</para>
    /// </summary>
    [Fact]
    public async Task SendingFallsBackToTheStoredCopyWhenTheSheetOnScreenIsAnotherCharacter()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero, JetstreamId);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var held = new TaskCompletionSource();
        ctx.Api.BeforeStoringCharacter = _ => held.Task;

        // The portfolio's own act: a sample replaces what is on screen, and the pointer stays put.
        ctx.Session.LoadSample(SheetMode.Villain);

        // Controls: the sheet on screen really is a different character, and the session really
        // has stopped claiming to hold the row's one.
        Assert.NotEqual("Jetstream", ctx.Session.Sheet.Name);
        Assert.False(ctx.Session.HoldsTheCharacterAt(JetstreamId));

        await Send(page);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var row = Assert.Single((await memberships.MineAsync())!);
        var detail = await memberships.ReadAsync(row.Id);

        Assert.NotNull(detail!.Pending);
        Assert.Equal("Jetstream", detail.Pending!.Name);
        Assert.Equal(6, detail.Pending.AbilityRanks["agility"]);

        held.SetResult();
    }

    /// <summary>
    /// <b>A session that has been emptied stops claiming the character it was holding.</b>
    ///
    /// <para>This is the one clear that matters, because <c>StartAgain</c> is the only method that
    /// empties the screen without moving the current-character pointer — so it is the only place
    /// the sheet and the id can come apart. A <c>HeldId</c> left standing over an empty sheet
    /// would put the submission back exactly where it started: the page would believe the sheet
    /// on screen was the account's character and send it.</para>
    ///
    /// <para>Both halves are driven: the character is opened through the manager's own control,
    /// and it is the sign-in page that empties the session.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptiedSessionStopsClaimingTheCharacterItHeld()
    {
        var (ctx, _) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(SubjectId);

        var manager = ctx.Render<ProwlersAndParagonsAutomation.Web.Components.CharacterManager>();

        await manager.FindAll(".open-target")
            .First(b => b.TextContent.Contains("Jetstream", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // The control on the fixture: opening a row really does tell the session which character
        // it is holding. Without this the assertion below would pass over a session that had
        // never claimed anything.
        Assert.True(ctx.Session.HoldsTheCharacterAt(JetstreamId));

        await TheSignInReadFailed(ctx);

        Assert.Null(ctx.Session.HeldId);
        Assert.False(ctx.Session.HoldsTheCharacterAt(JetstreamId));
    }

    /// <summary>
    /// <b>The remedy for the row that is already wrong: the player resubmits.</b>
    ///
    /// <para>Starts from the broken state — an empty clone approved into the campaign, labelled
    /// with the player's real character — and goes through both screens: the player presses Send
    /// for approval with the pointer on Jetstream, and the GM approves what arrives. Nothing
    /// anywhere rewrites the bad row; it is replaced by a decision, which is the only way a clone
    /// has ever changed.</para>
    /// </summary>
    [Fact]
    public async Task ResubmittingReplacesAnEmptyCloneWithTheRealSheet()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        // The broken row, put there the way it got there: an empty sheet in the pending slot,
        // approved by the GM. Written through the wire rather than through the page, because the
        // page can no longer produce one — which is the fix, and is why this has to be arranged.
        await AnEmptyCloneIsApproved(ctx, membership);

        ctx.Api.SignedIn = ("u_player", "Billy");

        var broken = await memberships.ReadAsync(membership);
        Assert.NotNull(broken!.Approved);
        Assert.Empty(broken.Approved!.AbilityRanks);

        // The player opens the campaigns page and sends again. Nothing else changes.
        var again = ctx.Render<Campaigns>();
        await Send(again);

        Assert.Contains("Sent to", again.Markup, StringComparison.Ordinal);

        // The GM approves what is now waiting, through their own screen.
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        await approval.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        await approval.FindAll(".campaign-diff .btn")
            .First(b => b.TextContent.Contains("Approve", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        var mended = await memberships.ReadAsync(membership);

        Assert.NotNull(mended!.Approved);
        Assert.Equal("Jetstream", mended.Approved!.Name);
        Assert.Equal(6, mended.Approved.AbilityRanks["agility"]);
        Assert.Equal("Jetstream", mended.Label);
    }

    /// <summary>
    /// Puts the reported row into the campaign: the empty payload in the pending slot, approved.
    ///
    /// <para>Through the wire, because after the fix no click on either page can produce one —
    /// and the state has to stay reachable in a test for as long as rows like it exist in the
    /// deployed database.</para>
    /// </summary>
    private static async Task AnEmptyCloneIsApproved(RenderContext ctx, string membership)
    {
        await AnEmptySnapshotIsSent(ctx, membership);

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var version = (await store.ReadAsync(membership))!.PendingVersion;

        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, version)).Outcome);
    }

    /// <summary>The same payload, left waiting for a decision. Ends signed in as the GM.</summary>
    private static async Task AnEmptySnapshotIsSent(RenderContext ctx, string membership)
    {
        var http = ctx.Services.GetRequiredService<HttpClient>();

        ctx.Api.SignedIn = ("u_player", "Billy");

        using var sending = new StringContent(
            $$"""{"label":"Jetstream","payload":{{System.Text.Json.JsonSerializer.Serialize(TheReportedPayload)}}}""",
            System.Text.Encoding.UTF8, "application/json");

        Assert.True(
            (await http.PutAsync($"/api/memberships/{membership}/submission", sending,
                Xunit.TestContext.Current.CancellationToken)).IsSuccessStatusCode,
            "the empty snapshot never landed, so nothing under test is reached");

        ctx.Api.SignedIn = ("u_gm", "The GM");
    }

    /// <summary>
    /// <b>The payload out of the owner's database, byte for byte.</b> 379 characters: the envelope,
    /// and a sheet carrying only the tier, the campaign id and the house Trait Cap that
    /// <c>CampaignJoin.Apply</c> copies in — every Trait 0d, no Powers, no name.
    ///
    /// <para>Written out here rather than built from a <see cref="CharacterSheet"/> so that a
    /// change to how this app writes a sheet cannot quietly stop this test reproducing the row it
    /// is about.</para>
    /// </summary>
    internal const string TheReportedPayload =
        """
        {"Version":1,"Mode":0,"Sheet":{"IsVillain":false,"UnlimitedBudget":false,"SelectedTierId":"low_level","CampaignId":"g_EQVHwU_Bl0VZdFrEssjk0A","TraitCapRank":10,"AbilityRanks":{},"AbilityModifiers":{},"TalentRanks":{},"AbilitySources":{},"TalentSources":{},"SelectedPowers":[],"Perks":[],"Flaws":[],"Name":"","Appearance":"","Motivation":"","Quote":"","Connections":[],"Gear":[]}}
        """;

    /// <summary>
    /// <b>The GM's screen says an approved clone is empty rather than drawing it as a
    /// character.</b>
    ///
    /// <para>This is what the owner was shown: an unnamed sheet, Low Level, Trait Cap 10d, every
    /// Trait 0d, Resolve 20, 0 of 100 Hero Points — rendered as though it were the character the
    /// row is named after. A blank form is not a character, and a screen that draws one as a
    /// character is lying to the person deciding about it.</para>
    ///
    /// <para>The sheet is still drawn beneath the sentence, deliberately: the GM has to be able to
    /// see what they are being told about, and nothing here repairs the row.</para>
    /// </summary>
    [Fact]
    public async Task TheApprovalScreenSaysWhenTheCloneIsEmpty()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        await AnEmptyCloneIsApproved(ctx, membership);

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        // The control: the row is there and it is the one the owner's GM opened.
        Assert.Contains("Jetstream", approval.Markup, StringComparison.Ordinal);

        await approval.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var shown = approval.Find(".campaign-diff").TextContent;

        Assert.Contains("The submission was empty — ask the player to resubmit", shown,
            StringComparison.Ordinal);

        // The positive control on the arm under test: this is the settled-sheet arm and the sheet
        // really was drawn, so the sentence is beside a sheet rather than instead of one.
        Assert.NotEmpty(approval.FindAll(".campaign-diff .sheet"));
    }

    /// <summary>
    /// <b>The roster says it too, and that is the level a reader arrives at.</b>
    ///
    /// <para>Opening the row has said "The submission was empty" since the slice that added it.
    /// The list did not: it said <em>Approved</em> beside the character's name, over a campaign
    /// holding an unnamed sheet with every Trait at 0d — which is the state the owner was shown,
    /// and the state nobody scanning a roster would have opened.</para>
    ///
    /// <para>Both lists, because both lie the same way: the GM's roster here, and the player's
    /// own "Games you are in" below.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsRosterMarksAMembershipHoldingAnEmptySubmission()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero, JetstreamId);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        await AnEmptyCloneIsApproved(ctx, membership);

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        var row = approval.Find(".campaign-list .campaign-row");

        // The control: this really is the row for the character, drawn with its standing — so the
        // marker is beside the label rather than instead of a row that failed to draw.
        Assert.Contains("Jetstream", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("Approved", row.TextContent, StringComparison.Ordinal);

        Assert.Contains("empty submission", row.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same marker on the player's own list, where "Approved" is the reassurance that their
    /// character is the one at the table.
    /// </summary>
    [Fact]
    public async Task ThePlayersOwnListMarksAMembershipHoldingAnEmptySubmission()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero, JetstreamId);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        await AnEmptyCloneIsApproved(ctx, membership);

        ctx.Api.SignedIn = ("u_player", "Billy");

        var mine = ctx.Render<Campaigns>();
        var row = mine.Find(".campaign-list .campaign-row");

        Assert.Contains("Jetstream", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("empty submission", row.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control that keeps the marker off every row on both screens: a real character's row
    /// says nothing of the kind, on either list.
    /// </summary>
    [Fact]
    public async Task ARealSubmissionIsNotMarkedEmptyOnEitherList()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero, JetstreamId);

        var page = ctx.Render<Campaigns>();
        Join(page, code);
        await Send(page);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        // The player's own list, with a real snapshot waiting.
        var mine = ctx.Render<Campaigns>();
        Assert.Contains("Jetstream", mine.Find(".campaign-list .campaign-row").TextContent,
            StringComparison.Ordinal);
        Assert.DoesNotContain("empty submission", mine.Markup, StringComparison.Ordinal);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var version = (await memberships.ReadAsync(membership))!.PendingVersion;
        Assert.Equal(DecisionOutcome.Done,
            (await memberships.ApproveAsync(membership, version)).Outcome);

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        Assert.Contains("Jetstream", approval.Find(".campaign-list .campaign-row").TextContent,
            StringComparison.Ordinal);
        Assert.DoesNotContain("empty submission", approval.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of the sentence above, and the one that keeps it from being printed over
    /// every sheet on the screen: a real character says nothing of the kind.
    /// </summary>
    [Fact]
    public async Task ARealCloneIsNotCalledEmpty()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);
        await Send(page);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var version = (await memberships.ReadAsync(membership))!.PendingVersion;
        Assert.Equal(DecisionOutcome.Done,
            (await memberships.ApproveAsync(membership, version)).Outcome);

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        await approval.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var shown = approval.Find(".campaign-diff").TextContent;

        Assert.DoesNotContain("The submission was empty", shown, StringComparison.Ordinal);
        Assert.NotEmpty(approval.FindAll(".campaign-diff .sheet"));
    }

    /// <summary>
    /// <b>The same sentence one slot earlier, where it is worth more.</b> A GM told before pressing
    /// Approve does not make an empty sheet the campaign's clone in the first place — which is the
    /// state every row of this kind in the deployed database went through.
    ///
    /// <para>Approve is still on the screen, because a decision about somebody's character is the
    /// GM's to take and not this page's to refuse.</para>
    /// </summary>
    [Fact]
    public async Task TheApprovalScreenSaysWhenAWaitingSnapshotIsEmpty()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var membership = Assert.Single((await memberships.MineAsync())!).Id;

        await AnEmptySnapshotIsSent(ctx, membership);

        var approval = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.CampaignApproval>(
            p => p.Add(c => c.Id, CampaignId));

        await approval.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var shown = approval.Find(".campaign-diff").TextContent;

        Assert.Contains("The submission was empty — ask the player to resubmit", shown,
            StringComparison.Ordinal);

        // The positive control: this is the diff arm, and the diff really ran — so the sentence
        // is beside a decision rather than instead of one.
        Assert.Contains("fields compared", shown, StringComparison.Ordinal);
        Assert.Contains(approval.FindAll(".campaign-diff .btn"),
            b => b.TextContent.Contains("Approve", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A character that could not be read is a different sentence from one with nothing on
    /// it</b>, and getting the two the wrong way round is the false alarm this project keeps
    /// writing down: "your character is empty" over a character that is not is exactly what
    /// teaches somebody to distrust every message the app gives them.
    /// </summary>
    [Fact]
    public async Task ACharacterThatCouldNotBeReadSaysThatInstead()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var _ = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        // The read that the submission is now built on, refused the way a network refuses it.
        ctx.Api.BeforeAnsweringCharacter = _ => throw new HttpRequestException("no network");

        await Send(page);

        Assert.Contains("could not be read just now", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Try again, or sign in again", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing on it yet", page.Markup, StringComparison.Ordinal);

        // Not the other refusal: nothing here says the character has gone.
        Assert.DoesNotContain("not on this account", page.Markup, StringComparison.Ordinal);

        ctx.Api.BeforeAnsweringCharacter = null;

        Assert.False(Assert.Single((await ctx.Services
            .GetRequiredService<ApiMembershipStore>().MineAsync())!).HasPending);
    }

    /// <summary>
    /// <b>A character that is not there is a third sentence, and it is a different instruction.</b>
    ///
    /// <para>One sentence covered four failures — a dropped connection, a session that ended while
    /// the tab was open, a character no longer on the account, and a payload this build cannot
    /// open — and told all four to try again in a moment. Two of those never come right by
    /// waiting, so a reader would wait, and go on waiting. <c>CampaignApproval</c> has told its
    /// own unreachable and unreadable arms apart since it shipped; this is the same split one
    /// screen over.</para>
    ///
    /// <para>Reached the way it happens: a sample is on screen — which is what makes the page take
    /// the stored read at all — while the row's character has gone from the account.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterThatIsNotThereIsToldApartFromOneThatCannotBeReached()
    {
        var (ctx, code) = await ATableAndTwoCharacters();
        await using var disposing = ctx;

        ctx.Session.Open(Jetstream(), SheetMode.Hero, JetstreamId);

        var page = ctx.Render<Campaigns>();
        Join(page, code);

        var held = new TaskCompletionSource();
        ctx.Api.BeforeStoringCharacter = _ => held.Task;

        ctx.Session.LoadSample(SheetMode.Villain);

        await ctx.Services.GetRequiredService<ApiCharacterStore>().DeleteAsync(JetstreamId);

        await Send(page);

        Assert.Contains("not on this account any more", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Open it in the character manager", page.Markup, StringComparison.Ordinal);

        // And not the sentence for a server that could not be reached, which would have this
        // reader waiting for a character that is gone.
        Assert.DoesNotContain("Try again, or sign in again", page.Markup, StringComparison.Ordinal);

        Assert.False(Assert.Single((await ctx.Services
            .GetRequiredService<ApiMembershipStore>().MineAsync())!).HasPending);

        held.SetResult();
    }
}
