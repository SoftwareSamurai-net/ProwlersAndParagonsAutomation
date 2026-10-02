using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Sheets;

using ProwlersAndParagons.Testing;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A diff row's detail is taken apart into the parts that stayed, went, arrived or changed — and
/// the screen draws each part marked, rather than the whole detail twice around an arrow.
///
/// <para><b>The owner's report, verbatim in shape:</b> a Life Drain row read
/// <c>8d · Magic · Side Effect (Detrimental) · Limited (Significantly Limited) · Conditional
/// (Often Works) · Two-Handed · Signature → 6d · Magic · Conditional (Occasionally Works) ·
/// Concentration · Readied · Two-Handed</c>, which is correct and cannot be read. Every case here
/// is a claim about which of those thirteen words a GM is shown as gone, new or changed.</para>
///
/// <para><b>What this file cannot see:</b> the ink and the strike-through are in <c>app.css</c>,
/// which bUnit does not apply. What it holds is that the markup says which is which — a
/// <c>&lt;del&gt;</c>, an <c>&lt;ins&gt;</c>, and a spoken word inside each — so a stylesheet
/// that dropped the stroke would still leave a screen reader told, and the pixel goldens are
/// where the stroke itself is judged.</para>
/// </summary>
public sealed class CampaignDiffPartsTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(RepoRoot());
    private static readonly CostCalculator Costs = new(Rules);

    // ── The parts, off the row ──────────────────────────────────────────────────

    /// <summary>
    /// The report's row, taken apart. Order is the old side's with what arrived at the end; the
    /// rank and the graded Con are each one change rather than a removal and an addition.
    /// </summary>
    [Fact]
    public void ALongPowerLineIsTakenApartIntoWhatStayedWentArrivedAndChanged()
    {
        var before = "8d · Magic · Side Effect (Detrimental) · Limited (Significantly Limited) · Conditional (Often Works) · Two-Handed · Signature";
        var after = "6d · Magic · Conditional (Occasionally Works) · Concentration · Readied · Two-Handed";

        var parts = CampaignDiff.PartsOf(before, after);

        Assert.Equal(
            [
                "6d ← 8d",
                "= Magic",
                "- Side Effect (Detrimental)",
                "- Limited (Significantly Limited)",
                "Conditional (Occasionally Works) ← Conditional (Often Works)",
                "= Two-Handed",
                "- Signature",
                "+ Concentration",
                "+ Readied",
            ],
            parts.Select(Spell));

        // The control on the control: every word of both sides is accounted for exactly once.
        Assert.Equal(before.Split(" · ").Order(),
            parts.Where(p => p.Kind is not PartKind.Added).Select(p => p.Was ?? p.Text).Order());
        Assert.Equal(after.Split(" · ").Order(),
            parts.Where(p => p.Kind is not PartKind.Removed).Select(p => p.Text).Order());
    }

    /// <summary>
    /// A one-part row is one changed part, so <c>Might 6d → 8d</c> reads as it always has and a
    /// row with one side missing is all one kind — the row's own word says which.
    /// </summary>
    [Theory]
    [InlineData("6d", "8d", "8d ← 6d")]
    [InlineData("Standard", "High Level", "High Level ← Standard")]
    [InlineData("on → off", "off → on", "off → on ← on → off")]
    public void AOnePartRowIsOneChange(string before, string after, string spelt)
    {
        var part = Assert.Single(CampaignDiff.PartsOf(before, after));

        Assert.Equal(PartKind.Changed, part.Kind);
        Assert.Equal(spelt, Spell(part));
    }

    [Fact]
    public void ASideThatIsNotThereMakesEveryPartOneKind()
    {
        Assert.All(CampaignDiff.PartsOf(null, "4d · Trained · Reach/Throw"),
            p => Assert.Equal(PartKind.Added, p.Kind));
        Assert.All(CampaignDiff.PartsOf("4d · Trained", null),
            p => Assert.Equal(PartKind.Removed, p.Kind));
        Assert.Equal(3, CampaignDiff.PartsOf(null, "4d · Trained · Reach/Throw").Count);
    }

    /// <summary>
    /// <b>Two of one name are paired only where one is left over on each side.</b> Two Limited Cons
    /// with one regraded is one change beside one kept part, because the kept one is matched
    /// verbatim first and what is left is one each. Two on the old side against one on the new
    /// is not paired, because pairing it means guessing which became which, and a guess prints a
    /// change nobody made — so it is reported as what went and what came.
    /// </summary>
    [Fact]
    public void TwoOfOneNameArePairedOnlyWhereOneIsLeftOverEachSide()
    {
        Assert.Equal(
            ["= 4d", "Limited (Significantly Limited) ← Limited (Somewhat Limited)", "= Limited (Severely Limited)"],
            CampaignDiff.PartsOf(
                "4d · Limited (Somewhat Limited) · Limited (Severely Limited)",
                "4d · Limited (Significantly Limited) · Limited (Severely Limited)").Select(Spell));

        Assert.Equal(
            ["= 4d", "- Limited (Somewhat Limited)", "- Limited (Severely Limited)", "+ Limited (Significantly Limited)"],
            CampaignDiff.PartsOf(
                "4d · Limited (Somewhat Limited) · Limited (Severely Limited)",
                "4d · Limited (Significantly Limited)").Select(Spell));
    }

    /// <summary>
    /// A unit count and a multiple pair on their noun, so <c>6 immunities</c> against
    /// <c>1 immunities</c> is one change, and so is a Pro's <c>×2</c> against <c>×3</c>.
    /// </summary>
    [Theory]
    [InlineData("1d · 3 immunities · Fire, Cold", "1d · 4 immunities · Fire, Cold, Acid",
        "= 1d|4 immunities ← 3 immunities|- Fire, Cold|+ Fire, Cold, Acid")]
    [InlineData("2d · Area ×2", "2d · Area ×3", "= 2d|Area ×3 ← Area ×2")]
    [InlineData("2d · ×2", "2d · ×5", "= 2d|×5 ← ×2")]
    public void CountsAndMultiplesPairOnWhatTheyCount(string before, string after, string spelt)
    {
        Assert.Equal(spelt, string.Join("|", CampaignDiff.PartsOf(before, after).Select(Spell)));
    }

    /// <summary>
    /// The separator the parts are split on is the one every detail line is joined with, held
    /// against a real Power's row so that a change to the join and not the split — or the other
    /// way — is a red test rather than a row that comes back as one part.
    /// </summary>
    [Fact]
    public void TheSeparatorIsTheOneTheDetailLinesAreJoinedWith()
    {
        var before = ASheet();
        var after = ASheet();

        before.SelectedPowers.Add(new SelectedPower("life_drain", 8, [], [new("signature")]) { SourceId = "magic" });
        after.SelectedPowers.Add(new SelectedPower("life_drain", 6, [], [new("readied")]) { SourceId = "magic" });

        var row = Assert.Single(CampaignDiff.Between(before, after, Rules, Costs).Rows,
            r => r.What == "Power: Life Drain");

        Assert.Equal(["6d ← 8d", "= Magic", "- Signature", "+ Readied"], row.Parts.Select(Spell));
    }

    // ── The screen ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The approval screen marks each part, and prints the detail once.</b>
    ///
    /// <para>Rendered rather than read off the record, because the record has carried
    /// <c>Before</c> and <c>After</c> since the day it existed and the screen still printed both
    /// whole — the fault is in what is drawn. The positive control is the row itself, then that the
    /// old whole-line spelling is gone from it.</para>
    /// </summary>
    [Fact]
    public async Task TheApprovalScreenStrikesWhatWentAndMarksWhatCame()
    {
        var (ctx, membership) = await AJoinedMember();
        await using var _ = ctx;

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();

        var first = ASheet();
        first.SelectedPowers.Add(new SelectedPower("life_drain", 8, [],
            [new("side_effect", "detrimental"), new("conditional", "often_works"), new("two_handed"), new("signature")])
            { SourceId = "magic" });

        var second = ASheet();
        second.SelectedPowers.Add(new SelectedPower("life_drain", 6, [],
            [new("conditional", "occasionally_works"), new("concentration"), new("two_handed")])
            { SourceId = "magic" });

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, first, SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, second, SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));
        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var row = Assert.Single(page.FindAll(".diff-rows li"),
            li => li.QuerySelector(".what")?.TextContent == "Power: Life Drain");

        // What went is a <del>, what came is an <ins>, and each says so to a screen reader.
        Assert.Equal(["Side Effect (Detrimental)", "Signature"],
            row.QuerySelectorAll("del").Select(Visible));
        Assert.Equal(["Concentration"], row.QuerySelectorAll("ins").Select(Visible));
        Assert.All(row.QuerySelectorAll("del"), d => Assert.Contains("removed", d.TextContent, StringComparison.Ordinal));
        Assert.All(row.QuerySelectorAll("ins"), i => Assert.Contains("added", i.TextContent, StringComparison.Ordinal));

        // The rank and the graded Con are each one change; what stayed is marked as kept.
        Assert.Equal(["8d → 6d", "Conditional (Often Works) → Conditional (Occasionally Works)"],
            row.QuerySelectorAll(".changed").Select(e => e.TextContent));
        Assert.Equal(["Magic", "Two-Handed"], row.QuerySelectorAll(".kept").Select(e => e.TextContent));

        // And the whole line is not printed twice around an arrow any more: the old spelling had
        // the Source on both sides of one text node, so "Magic" appears exactly once in the row.
        Assert.Equal(1, row.TextContent.Split("Magic").Length - 1);
    }

    /// <summary>
    /// <b>A row says when it was sent, and the diff says what it is measured against.</b>
    ///
    /// <para>The server has sent <c>pendingAt</c> and <c>approvedAt</c> since the memberships
    /// route existed and the GM's screen read neither, so a request three weeks old and one from
    /// this morning were the same row. <see cref="FakeApi"/>'s clock runs a minute behind the
    /// real one, which is why "just now" is the honest phrase here; the phrase itself is
    /// <see cref="ProwlersAndParagonsAutomation.Web.Services.Ages"/>'s, tested on its own.</para>
    /// </summary>
    [Fact]
    public async Task TheRowSaysWhenItWasSentAndTheDiffWhatItIsAgainst()
    {
        var (ctx, membership) = await AJoinedMember();
        await using var _ = ctx;

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();

        // Nothing sent yet: no time is claimed, because none has been told.
        var quiet = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));
        Assert.Empty(quiet.FindAll(".campaign-row .when"));

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);

        var settled = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));
        // The standing says "Approved" and the age follows it, rather than each saying it once.
        Assert.Equal("just now", settled.Find(".campaign-row .when").TextContent);
        Assert.Equal(1, settled.Find(".campaign-row .who").TextContent.Split("Approved").Length - 1);

        await settled.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());
        Assert.Equal("Approved just now.", settled.Find(".campaign-diff .diff-when").TextContent);

        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 8), SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var waiting = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));
        Assert.Equal("Sent just now", waiting.Find(".campaign-row .when").TextContent);

        await waiting.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());
        Assert.Equal("Sent just now. The sheet it would replace was approved just now.",
            waiting.Find(".campaign-diff .diff-when").TextContent);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>One part in four spellings, so an expected list reads as the screen would.</summary>
    private static string Spell(DiffPart part) => part.Kind switch
    {
        PartKind.Kept => $"= {part.Text}",
        PartKind.Added => $"+ {part.Text}",
        PartKind.Removed => $"- {part.Text}",
        _ => $"{part.Text} ← {part.Was}",
    };

    /// <summary>An element's text without its <c>sr-only</c> word.</summary>
    private static string Visible(IElement element) =>
        string.Concat(element.ChildNodes.Where(n => n is not IElement { ClassName: "sr-only" }).Select(n => n.TextContent));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static CharacterSheet ASheet(int might = 6) => new()
    {
        SelectedTierId = "standard",
        Name = "Ninefold",
        AbilityRanks = { ["might"] = might, ["agility"] = 4 },
        TalentRanks = { ["covert"] = 3 },
    };

    private static async Task<(RenderContext Ctx, string Membership)> AJoinedMember()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(
            "g_0000000000000000000000", "Nightfall",
            StoredCampaign.Write(
                new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var joined = await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .JoinAsync(code, "c_0000000000000000000000", "Ninefold");

        Assert.NotNull(joined);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        return (ctx, joined!.Value.Id);
    }
}
