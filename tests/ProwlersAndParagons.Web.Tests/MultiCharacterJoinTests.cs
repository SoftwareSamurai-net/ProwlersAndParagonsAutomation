using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>Joining a campaign with several characters at once — item 37.</b>
///
/// <para>The join box used to put only the character on screen into a game: adding a second one
/// meant opening it in the character manager, coming back, re-typing the code and pressing Join
/// again. The fix is a checkbox list of the account's characters under the code box, the
/// on-screen one pre-ticked, one press joining every ticked row.</para>
///
/// <para><b>The on-screen character keeps its exact old path</b> — the pointer guard, the session
/// mutation, <c>NotifyChanged</c> — and every other ticked character is read by id
/// (<c>ReadAsync</c>, never <c>OpenAsync</c>) and written back by id
/// (<c>AccountCharacterStore.RestoreAsync</c>), so the pointer never moves for it. That split is
/// what most of these tests are actually about.</para>
///
/// <para>Every test drives <c>RenderContext(storesForReal: true)</c> and presses the button a
/// player presses, for the reason <c>CampaignSubmissionTests</c> records: the current-character
/// pointer has to be a real id, and bUnit's recorder answers null to every read.</para>
/// </summary>
public sealed class MultiCharacterJoinTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string OnScreenId = "c_1111111111111111111111";
    private const string OtherId = "c_2222222222222222222222";
    private const string FillerId = "c_3333333333333333333333";

    private static CharacterSheet OnScreenSheet(string? tierId = null) =>
        new() { Name = "Ninefold", SelectedTierId = tierId };

    private static CharacterSheet OtherSheet(string? tierId = null) =>
        new() { Name = "Subject X-02", SelectedTierId = tierId };

    /// <summary>
    /// A game run by somebody else, and two of the player's own characters on the account — the
    /// pointer on the first, and nothing put on screen, so the on-screen path's own re-adopt
    /// (<c>TheCharacterOnScreenIsThePointers</c>) pulls it in exactly the way it does for every
    /// other test of that guard.
    /// </summary>
    private static async Task<(RenderContext Ctx, string Code)> AGameAndTwoCharacters(
        string? campaignTierId = "standard", string? onScreenTierId = null, string? otherTierId = null)
    {
        var ctx = new RenderContext(storesForReal: true);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(CampaignId, "Pinnacle City", campaignTierId, null, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        Assert.Equal(SaveOutcome.Saved, await account.SaveAsync(
            OnScreenId, "Ninefold", OnScreenSheet(onScreenTierId), SheetMode.Hero));
        Assert.Equal(SaveOutcome.Saved, await account.SaveAsync(
            OtherId, "Subject X-02", OtherSheet(otherTierId), SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(OnScreenId);

        return (ctx, code);
    }

    private static async Task TickOther(IRenderedComponent<Campaigns> page)
    {
        var row = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Subject X-02", StringComparison.Ordinal));

        await row.QuerySelector("input[type=checkbox]")!.ChangeAsync(new() { Value = true });
    }

    private static async Task PressJoin(IRenderedComponent<Campaigns> page) =>
        await page.FindAll("button")
            .Single(b => b.TextContent.Trim().StartsWith("Join with", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

    // ── Two characters, one press ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ticking the second character changes what the button itself says, and pressing it joins
    /// both — one membership per character, on the one game the code names.
    /// </summary>
    [Fact]
    public async Task TwoTickedCharactersJoinInOnePress()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await TickOther(page);

        // The button counts what a press would actually do, not merely how many boxes exist.
        Assert.Contains("Join with 2 characters",
            page.FindAll("button").Select(b => b.TextContent.Trim()));

        await PressJoin(page);

        var mine = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();

        Assert.NotNull(mine);
        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, m => m.CharacterId == OnScreenId);
        Assert.Contains(mine, m => m.CharacterId == OtherId);

        // One line per character, naming which is which.
        Assert.Contains("Ninefold: Joined Pinnacle City.", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Subject X-02: Joined Pinnacle City.", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The non-open character is written by id, and the pointer never so much as flickers at
    /// it.</b> The whole reason <c>JoinOther</c> reads with <c>ReadAsync</c> and writes back with
    /// <c>RestoreAsync</c> rather than opening it into the session.
    /// </summary>
    [Fact]
    public async Task TheNonOpenCharacterIsWrittenByIdAndThePointerDoesNotMove()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await TickOther(page);
        await PressJoin(page);

        // The pointer is exactly where it started.
        Assert.Equal(OnScreenId, await ctx.Services.GetRequiredService<SavedCharacters>().CurrentIdAsync());

        // And the character it names is the one this test opened on screen, not the other one —
        // a moved pointer would show up here as Subject X-02.
        Assert.Equal("Ninefold", ctx.Session.Sheet.Name);

        // The other character was written down all the same, by id.
        var stored = await ctx.Services.GetRequiredService<ApiCharacterStore>().LoadAsync(OtherId);

        Assert.NotNull(stored);
        Assert.Equal(CampaignId, stored!.Value.Sheet.CampaignId);
        Assert.Equal("Subject X-02", stored.Value.Sheet.Name);
    }

    /// <summary>
    /// A tier disagreement is one character's own outcome and does not touch another's: the
    /// disagreeing character's sheet is untouched and its row says so, while the character on
    /// screen — which agrees, or has no tier yet to disagree with — still joins.
    /// </summary>
    [Fact]
    public async Task ATierDisagreementWritesNothingForThatCharacterAndSaysSo()
    {
        var (ctx, code) = await AGameAndTwoCharacters(
            campaignTierId: "standard", onScreenTierId: null, otherTierId: "low_level");
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await TickOther(page);
        await PressJoin(page);

        Assert.Contains(
            "Subject X-02: Pinnacle City is played at a different tier. Nothing was changed.",
            page.Markup, StringComparison.Ordinal);

        // Nothing about the disagreeing character moved.
        var stored = await ctx.Services.GetRequiredService<ApiCharacterStore>().LoadAsync(OtherId);

        Assert.NotNull(stored);
        Assert.Null(stored!.Value.Sheet.CampaignId);
        Assert.Equal("low_level", stored.Value.Sheet.SelectedTierId);

        // The character on screen was not caught by the other one's disagreement.
        Assert.Contains("Ninefold: Joined Pinnacle City.", page.Markup, StringComparison.Ordinal);
        Assert.Equal(CampaignId, ctx.Session.Sheet.CampaignId);
    }

    /// <summary>
    /// A read that fails for one character is reported on its own row, in the same two sentences
    /// <c>Submit</c> already tells apart — and does not stop the character on screen from joining
    /// in the same press.
    /// </summary>
    [Fact]
    public async Task AReadFailureIsReportedPerRow()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await TickOther(page);

        ctx.Api.BeforeAnsweringCharacter = id =>
            id == OtherId ? throw new HttpRequestException("no network") : Task.CompletedTask;

        await PressJoin(page);

        ctx.Api.BeforeAnsweringCharacter = null;

        Assert.Contains(
            "Subject X-02: That character could not be read just now — the connection may have "
            + "dropped, or your sign-in may have ended.",
            page.Markup, StringComparison.Ordinal);

        // The character on screen never goes near `ReadAsync`, so the same outage does not touch it.
        Assert.Contains("Ninefold: Joined Pinnacle City.", page.Markup, StringComparison.Ordinal);

        var mine = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();
        Assert.Single(mine!);
    }

    /// <summary>A stored character with nothing on it is skipped rather than joined.</summary>
    [Fact]
    public async Task AnEmptyStoredCharacterIsSkippedWithItsOwnSentence()
    {
        var ctx = new RenderContext(storesForReal: true);
        await using var _ = ctx;

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(CampaignId, "Pinnacle City", null, null, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        Assert.Equal(SaveOutcome.Saved,
            await account.SaveAsync(OnScreenId, "Ninefold", OnScreenSheet(), SheetMode.Hero));

        // A slot with literally nothing built into it — no name, no tier, no Ability above the
        // rulebook's floor.
        Assert.Equal(SaveOutcome.Saved,
            await account.SaveAsync(OtherId, "Unnamed character", new CharacterSheet(), SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(OnScreenId);

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Unnamed character", StringComparison.Ordinal))
            .QuerySelector("input[type=checkbox]")!
            .ChangeAsync(new() { Value = true });

        await PressJoin(page);

        Assert.Contains(
            "Unnamed character: This character has nothing on it yet, so it was not put into the game.",
            page.Markup, StringComparison.Ordinal);

        var mine = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();
        Assert.Single(mine!);
        Assert.DoesNotContain(mine!, m => m.CharacterId == OtherId);
    }

    /// <summary>
    /// <b>The one way an already-existing character misses the cap check that spares it</b>: it
    /// stops existing between the read this join takes and the write it is about to make. That
    /// race is what turns <c>RestoreAsync</c>'s answer from true to false, and the row has to say
    /// the join happened and the save did not — the join is real (`JoinAsync` already told the
    /// server), so claiming nothing happened would itself be false.
    /// </summary>
    [Fact]
    public async Task AWriteBackRefusedByTheAccountCapIsReportedNotRepaired()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        Assert.Equal(SaveOutcome.Saved,
            await account.SaveAsync(FillerId, "Filler", new CharacterSheet { Name = "Filler" }, SheetMode.Hero));

        // Tightened after seeding, so the three characters already stored are grandfathered — the
        // real server's own rule, which lets an existing row be rewritten however full the
        // account is. Two will remain once the race below removes Subject X-02's.
        ctx.Api.Limit = 2;

        var deleted = false;

        ctx.Api.BeforeStoringCharacter = async id =>
        {
            if (id != OtherId || deleted) return;
            deleted = true;

            using var response = await ctx.Services.GetRequiredService<HttpClient>()
                .DeleteAsync($"/api/characters/{OtherId}");
            response.EnsureSuccessStatusCode();
        };

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await TickOther(page);
        await PressJoin(page);

        Assert.Contains(
            "Subject X-02: Joined Pinnacle City, but the change could not be saved just now — "
            + "the account may be full. Try again in a moment.",
            page.Markup, StringComparison.Ordinal);

        // The join really did happen server-side — the server does not know about the account's
        // storage cap at all, so the membership stands even though the write-back did not.
        var mine = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();
        Assert.Contains(mine!, m => m.CharacterId == OtherId);

        // And the row genuinely was not written back: it is gone, not merely unmoved.
        Assert.Null(await account.LoadAsync(OtherId));
    }

    // ── The controls around the press ───────────────────────────────────────────────────────

    /// <summary>The code stays in the box after a join, so several characters can share it.</summary>
    [Fact]
    public async Task TheCodeStaysInTheBoxAfterAJoin()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        var page = ctx.Render<Campaigns>();

        await page.Find("#join-code").InputAsync(new() { Value = code });
        await PressJoin(page);

        Assert.Equal(code, page.Find("#join-code").GetAttribute("value"));
    }

    /// <summary>
    /// A character already in a game is shown disabled with a note, and is not offered to a press
    /// of Join even if something ticked it earlier — the page reruns the same test at the moment
    /// Join is pressed rather than trusting a stale tick.
    /// </summary>
    [Fact]
    public async Task ACharacterAlreadyInAGameIsDisabledAndNeverJoinedAgainFromHere()
    {
        var (ctx, code) = await AGameAndTwoCharacters(campaignTierId: null);
        await using var _ = ctx;

        // Subject X-02 is already in some other game, from this account's point of view — the
        // honest, weaker fact this page can know without redeeming the code.
        var alreadyElsewhere = OtherSheet();
        alreadyElsewhere.CampaignId = "g_elsewhere";

        Assert.Equal(SaveOutcome.Saved, await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(OtherId, "Subject X-02", alreadyElsewhere, SheetMode.Hero));

        var page = ctx.Render<Campaigns>();

        var row = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Subject X-02", StringComparison.Ordinal));

        Assert.Contains("Already in a game", row.TextContent, StringComparison.Ordinal);
        Assert.True(row.QuerySelector("input[type=checkbox]")!.HasAttribute("disabled"));

        await page.Find("#join-code").InputAsync(new() { Value = code });

        // Only the character on screen, so the button still reads the plain, singular label.
        Assert.Contains("Join with 1 character",
            page.FindAll("button").Select(b => b.TextContent.Trim()));

        await PressJoin(page);

        var mine = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();
        Assert.DoesNotContain(mine!, m => m.CharacterId == OtherId);
    }

    /// <summary>
    /// <b>Refresh's independent reads are asked at once, not one after another.</b> Every request
    /// this test holds arrives before any of them is allowed to answer, which sequential awaits
    /// cannot do: a request fired only after the one before it completed would never be seen
    /// alongside the others.
    /// </summary>
    [Fact]
    public async Task RefreshAsksItsFourIndependentReadsAtOnce()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("u_player", "The Player");

        string[] expected =
        [
            "/api/campaigns", "/api/memberships", "/api/memberships/inbox", "/api/characters",
        ];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var release = new TaskCompletionSource();

        ctx.Api.Holding = async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (!expected.Contains(path)) return;

            lock (seen) seen.Add(path);

            await release.Task;
        };

        ctx.Render<Campaigns>();

        await Until(() => { lock (seen) return seen.Count == expected.Length; },
            "all four of Refresh's independent reads were in flight at once");

        release.SetResult();
    }

    /// <summary>
    /// <b><c>EmptySubmissions.AmongAsync</c> reads its rows at once too.</b> Two memberships each
    /// hold a submission, so the page's own refresh has two membership-detail reads to make; both
    /// are seen before either is allowed to answer, which one after another cannot manage.
    /// </summary>
    [Fact]
    public async Task EmptySubmissionsChecksEveryRowAtOnce()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        const string GameA = "g_AAAAAAAAAAAAAAAAAAAAAA";
        const string GameB = "g_BBBBBBBBBBBBBBBBBBBBBB";
        const string AlphaId = "c_1111111111111111111111";
        const string BetaId = "c_2222222222222222222222";

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var codeA = ctx.Api.Campaign(GameA, "Game A",
            StoredCampaign.Write(new Campaign(GameA, "Game A", null, null, false)));
        var codeB = ctx.Api.Campaign(GameB, "Game B",
            StoredCampaign.Write(new Campaign(GameB, "Game B", null, null, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        var joinedA = await memberships.JoinAsync(codeA, AlphaId, "Alpha");
        var joinedB = await memberships.JoinAsync(codeB, BetaId, "Beta");

        Assert.NotNull(joinedA);
        Assert.NotNull(joinedB);

        Assert.NotNull(await memberships.SubmitAsync(
            joinedA!.Value.Id, new CharacterSheet { Name = "Alpha" }, SheetMode.Hero));
        Assert.NotNull(await memberships.SubmitAsync(
            joinedB!.Value.Id, new CharacterSheet { Name = "Beta" }, SheetMode.Hero));

        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(AlphaId);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var release = new TaskCompletionSource();

        ctx.Api.Holding = async request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // Only a single membership's own detail address — not the list, the inbox, or join.
            if (!path.StartsWith("/api/memberships/", StringComparison.Ordinal)
                || path.EndsWith("/inbox", StringComparison.Ordinal)
                || path.EndsWith("/join", StringComparison.Ordinal))
            {
                return;
            }

            lock (seen) seen.Add(path);

            await release.Task;
        };

        ctx.Render<Campaigns>();

        await Until(() => { lock (seen) return seen.Count == 2; },
            "both membership detail reads were in flight at once");

        release.SetResult();
    }

    /// <summary>Wait for something the renderer will not redraw when it happens.</summary>
    private static async Task Until(Func<bool> ready, string what)
    {
        for (var i = 0; i < 1000; i++)
        {
            if (ready()) return;
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Waited ten seconds and {what} never happened.");
    }
}
