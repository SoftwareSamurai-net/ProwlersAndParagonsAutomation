using Bunit;
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
        Assert.DoesNotContain("nothing on it yet", page.Markup, StringComparison.Ordinal);

        ctx.Api.BeforeAnsweringCharacter = null;

        Assert.False(Assert.Single((await ctx.Services
            .GetRequiredService<ApiMembershipStore>().MineAsync())!).HasPending);
    }
}
