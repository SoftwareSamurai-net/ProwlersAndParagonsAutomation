using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;
using Assets = ProwlersAndParagonsAutomation.Web.Pages.Assets;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>PROGRESS item 33, rulings 5+6: the player holds the shared object and the GM approves it.</b>
///
/// <para>A proposal is a full <see cref="CampaignAsset"/> riding a
/// <see cref="CampaignAssetContribution"/> — no new server route, no migration, the server parses
/// nothing. It reaches the GM exactly the way an ordinary character submission does. This file
/// drives both halves through the real pages: a player builds and presents one on the Vehicles and
/// bases step, and a GM adopts, adopts with changes, or refuses it on the approval screen.</para>
/// </summary>
public sealed class CampaignAssetProposalTests
{
    private const string CampaignId = "g_0000000000000000000000";
    private const string PlayerCharacter = "c_0000000000000000000000";

    private static Campaign ATable() => new(CampaignId, "Nightfall", "standard", 8, false);

    // ── The player's side: Assets.razor ──────────────────────────────────────────

    /// <summary>
    /// <b>Building a proposal and reading its own findings live, before it is ever sent.</b> The
    /// same engine call the GM's screen will make once it arrives — reused here rather than a
    /// second calculation, so the two can never disagree.
    /// </summary>
    [Fact]
    public void ProposingASurplusIsWarnedAboutOnTheStepItselfAndOnceWrittenDown()
    {
        using var ctx = new RenderContext(storesForReal: true);
        ctx.Session.Sheet.CampaignId = CampaignId;   // proposing needs a game to propose into

        var page = ctx.Render<Assets>();

        page.Find("input[aria-label='Name a proposal']").Input("Skyhook");
        page.Find("button[aria-label='Propose a shared vehicle or base']").Click();

        // Hero Points on the proposal, with nothing built yet — the whole of it is a surplus.
        page.Find("#proposal-hp").Change("1");

        Assert.Contains("unspent", page.Markup, StringComparison.Ordinal);
        Assert.Contains("25 Vehicle Points", page.Markup, StringComparison.Ordinal);

        // Present it — the whole of "presenting" today is writing the contribution onto the
        // sheet, carrying the object it names.
        page.FindAll("button")
            .First(b => b.TextContent.Contains("Propose to the GM", StringComparison.Ordinal))
            .Click();

        var contribution = Assert.Single(ctx.Session.Sheet.CampaignAssets);
        Assert.NotNull(contribution.Proposal);
        Assert.Equal("Skyhook", contribution.Proposal!.Name);
        Assert.Equal(1, contribution.HeroPoints);

        // The engine agrees, on the real character, once it is written down — the finding a GM
        // will see is the same finding the player saw while building it.
        var findings = ctx.Session.Validate().Issues;
        Assert.Contains(findings, i => i.Code == "CAMPAIGN_ASSET_SURPLUS");
    }

    // ── The GM's side: CampaignApproval.razor ────────────────────────────────────

    private static async Task<(RenderContext Ctx, string Membership)> AProposalWaiting(
        Func<CampaignAsset, CampaignAsset>? shape = null)
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var code = ctx.Api.Campaign(CampaignId, "Nightfall", StoredCampaign.Write(ATable()));

        ctx.Api.SignedIn = ("u_player", "The Player");
        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var joined = await memberships.JoinAsync(code, PlayerCharacter, "Ninefold");
        Assert.NotNull(joined);

        var proposal = new CampaignAsset("a_1111111111111111111111",
            CampaignAssetContribution.Vehicle, "Skyhook")
        { Body = 6, Speed = 8 };

        if (shape is not null) proposal = shape(proposal);

        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            Name = "Ninefold",
            AbilityRanks = { ["might"] = 6, ["agility"] = 4 },
        };
        sheet.CampaignAssets.Add(new CampaignAssetContribution(proposal.Id)
        {
            Name = proposal.Name, Kind = proposal.Kind, HeroPoints = 30, Proposal = proposal
        });

        Assert.NotNull(await memberships.SubmitAsync(joined!.Value.Id, sheet, SheetMode.Hero));

        ctx.Api.SignedIn = ("u_gm", "The GM");

        return (ctx, joined.Value.Id);
    }

    /// <summary>Open the one waiting row's diff, the way a GM presses "Read the changes".</summary>
    private static async Task Open(IRenderedComponent<CampaignApproval> page) =>
        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

    /// <summary><b>A proposal riding a submission renders as a row on the GM's screen.</b></summary>
    [Fact]
    public async Task AProposalRendersAsARowWithThreeAnswers()
    {
        var (ctx, _) = await AProposalWaiting();
        await using var _disposeCtx = ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await Open(page);

        var diff = page.Find(".campaign-diff").TextContent;
        Assert.Contains("proposes", diff, StringComparison.Ordinal);
        Assert.Contains("Skyhook", diff, StringComparison.Ordinal);
        Assert.Contains("vehicle", diff, StringComparison.Ordinal);

        var row = page.Find(".proposal-row");
        Assert.Contains("Adopt", row.QuerySelector("button")!.TextContent, StringComparison.Ordinal);
        Assert.Equal(3, row.QuerySelectorAll("button").Length);
    }

    /// <summary>
    /// <b>Adopt is one click: the campaign keeps the proposer's own id, and the character is
    /// approved in the same action.</b>
    /// </summary>
    [Fact]
    public async Task AdoptWritesTheCampaignsAssetsUnderTheSameIdAndApproves()
    {
        var (ctx, membership) = await AProposalWaiting();
        await using var _disposeCtx = ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await Open(page);

        await page.Find(".proposal-row button").ClickAsync(new MouseEventArgs());

        var campaigns = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var campaign = await campaigns.LoadAsync(CampaignId);

        var kept = Assert.Single(CampaignAsset.On(campaign));
        Assert.Equal("a_1111111111111111111111", kept.Id);
        Assert.Equal("Skyhook", kept.Name);
        Assert.Equal(6, kept.Body);

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var detail = await memberships.ReadAsync(membership);
        Assert.NotNull(detail!.Approved);
        Assert.Null(detail.Pending);

        Assert.Contains("Approved", page.Markup, StringComparison.Ordinal);

        // **The proposal row does not double-print.** Once adopted there is no pending sheet left
        // to read a proposal off, and reopening the row draws the settled character instead — not
        // a second copy of the row that just closed.
        Assert.DoesNotContain("proposal-row", page.Markup, StringComparison.Ordinal);

        await Open(page);
        Assert.DoesNotContain("proposal-row", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Adopt with changes opens the same draft editor a GM's own object uses, pre-filled — and
    /// saving writes the edited object rather than the one proposed.</b>
    /// </summary>
    [Fact]
    public async Task AdoptWithChangesWritesTheEditedObjectAndApproves()
    {
        var (ctx, membership) = await AProposalWaiting();
        await using var _disposeCtx = ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await Open(page);

        await page.FindAll(".proposal-row button")
            .First(b => b.TextContent.Contains("Adopt with changes", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // The editor is pre-filled with the proposed object.
        var speed = page.Find("#shared-speed");
        Assert.Equal("8", speed.GetAttribute("value"));

        await speed.ChangeAsync(new ChangeEventArgs { Value = "20" });

        await page.FindAll(".btn.primary")
            .First(b => b.TextContent.Contains("Save", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        var campaigns = ctx.Services.GetRequiredService<AccountCampaignStore>();
        var campaign = await campaigns.LoadAsync(CampaignId);

        var kept = Assert.Single(CampaignAsset.On(campaign));
        Assert.Equal(20, kept.Speed);   // the GM's edit, not the proposer's own 8

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var detail = await memberships.ReadAsync(membership);
        Assert.NotNull(detail!.Approved);
    }

    /// <summary>
    /// <b>A proposal naming an id the campaign already holds is an amendment</b>, shown as a diff
    /// against the adopted object rather than as a fresh addition.
    /// </summary>
    [Fact]
    public async Task AProposalNamingAnAdoptedIdIsShownAsAnAmendment()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        var existing = new CampaignAsset("a_1111111111111111111111",
            CampaignAssetContribution.Vehicle, "Skyhook") { Body = 6, Speed = 8 };
        var code = ctx.Api.Campaign(CampaignId, "Nightfall",
            StoredCampaign.Write(ATable() with { Assets = [existing] }));

        ctx.Api.SignedIn = ("u_player", "The Player");
        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var joined = await memberships.JoinAsync(code, PlayerCharacter, "Ninefold");
        Assert.NotNull(joined);

        // An amendment: the same id, a changed Speed.
        var amendment = existing with { Speed = 12 };
        var sheet = new CharacterSheet { SelectedTierId = "standard", Name = "Ninefold" };
        sheet.CampaignAssets.Add(new CampaignAssetContribution(amendment.Id)
        {
            Name = amendment.Name, Kind = amendment.Kind, HeroPoints = 30, Proposal = amendment
        });
        Assert.NotNull(await memberships.SubmitAsync(joined!.Value.Id, sheet, SheetMode.Hero));

        ctx.Api.SignedIn = ("u_gm", "The GM");
        await using var _disposeCtx = ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, CampaignId));
        await Open(page);

        var row = page.Find(".proposal-row");
        Assert.Contains("already holds an object", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("Speed: 8d → 12d", row.TextContent, StringComparison.Ordinal);
    }
}
