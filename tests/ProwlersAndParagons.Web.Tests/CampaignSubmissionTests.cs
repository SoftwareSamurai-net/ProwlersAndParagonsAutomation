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
}
