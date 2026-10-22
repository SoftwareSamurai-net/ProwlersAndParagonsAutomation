using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A GM sends a link instead of reading ten characters down a phone, and the join box says which
/// character it is about.
///
/// <para><b>Both halves replace prose with a mechanism</b>, which is the point rather than a
/// tidy-up: the handout at <c>/join.html</c> had a paragraph about hyphens and capitals not
/// mattering, and a row explaining a disabled button. A link that carries the code makes the first
/// paragraph unnecessary, and naming the character makes the second sentence do the work the
/// explanation was doing.</para>
/// </summary>
public sealed class JoinLinkTests
{
    private const string CampaignId = "g_0000000000000000000000";

    private static async Task<RenderContext> AGmWithACampaign()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Nightfall",
            StoredCampaign.Write(new Campaign(CampaignId, "Nightfall", "standard", 8, false)));

        await Task.CompletedTask;
        return ctx;
    }

    /// <summary>The code the fake server minted, as the GM's own list answers it.</summary>
    private static async Task<string> CodeOn(RenderContext ctx)
    {
        var row = (await ctx.Services.GetRequiredService<AccountCampaignStore>().ListAsync()).Single();

        Assert.NotNull(row.JoinCode);
        return row.JoinCode!;
    }

    private static async Task<IElement> SettingsPanelOf(RenderContext ctx)
    {
        var page = ctx.Render<Campaigns>();

        // The panel is behind its own button, so a test that read the closed row would assert
        // against markup the GM cannot see.
        var settings = page.FindAll("button").Single(b => b.TextContent.Trim() == "Settings");

        await settings.ClickAsync(new MouseEventArgs());

        return page.Find(".campaign-settings");
    }

    /// <summary>
    /// The GM is given a whole URL, and it carries the code.
    ///
    /// <para><b>Absolute, and not a path.</b> This string is pasted into a chat window, where a
    /// leading slash is not a link to anything.</para>
    /// </summary>
    [Fact]
    public async Task TheGmIsGivenALinkAndNotOnlyACode()
    {
        await using var ctx = await AGmWithACampaign();

        var code = await CodeOn(ctx);
        var link = (await SettingsPanelOf(ctx)).QuerySelector(".joinlink .url");

        Assert.NotNull(link);

        var text = link!.TextContent.Trim();

        Assert.StartsWith("http", text, StringComparison.Ordinal);
        Assert.EndsWith($"/campaign?join={code}", text, StringComparison.Ordinal);

        // **No whitespace anywhere in it.** The URL is built as one interpolated string rather
        // than written across the markup precisely so a line break in the razor file cannot land
        // inside it — a link that pastes with a newline in the middle is not a link.
        Assert.DoesNotContain(text, char.IsWhiteSpace);
    }

    /// <summary>
    /// The link's code carries no hyphen, unlike the one that is read aloud.
    ///
    /// <para>The hyphen exists so ten symbols survive being said down a phone. Nobody reads a URL
    /// aloud, the server strips punctuation on the way in, and a code that appears in two spellings
    /// in the same panel invites somebody to type the wrong one.</para>
    /// </summary>
    [Fact]
    public async Task TheLinkCarriesTheCodeTheServerCompares()
    {
        await using var ctx = await AGmWithACampaign();

        var panel = await SettingsPanelOf(ctx);
        var spoken = panel.QuerySelector(".joincode")!.TextContent.Trim();
        var link = panel.QuerySelector(".joinlink .url")!.TextContent.Trim();

        // The control: the spoken form really is the hyphenated one, so what follows is a
        // difference rather than two readings of the same string.
        Assert.Contains('-', spoken);
        Assert.DoesNotContain("-", link.Split("?join=")[1], StringComparison.Ordinal);
        Assert.Equal(spoken.Replace("-", "", StringComparison.Ordinal), link.Split("?join=")[1]);
    }

    /// <summary>
    /// A code in the address bar fills the box, and then leaves the address bar.
    ///
    /// <para><b>The second half is the security half.</b> A join code is a shared secret; one left
    /// in the bar is in the browser's history, in a screenshot of the window, and in whatever the
    /// reader pastes next.</para>
    /// </summary>
    [Fact]
    public async Task ACodeInTheAddressBarFillsTheBoxAndIsThenTakenOutOfIt()
    {
        await using var ctx = await AGmWithACampaign();

        var code = await CodeOn(ctx);

        ctx.Api.SignedIn = ("u_player", "The Player");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        nav.NavigateTo($"campaign?join={code}");

        var page = ctx.Render<Campaigns>();

        Assert.Equal(code, page.Find("#join-code").GetAttribute("value"));
        Assert.DoesNotContain("join=", nav.Uri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Arriving with no code leaves the box alone rather than emptying it or navigating.
    ///
    /// <para>The negative case, because a reader who typed six characters and then re-rendered
    /// would lose them to a parser that treats "no parameter" as "an empty one".</para>
    /// </summary>
    [Fact]
    public async Task ArrivingWithNoCodeChangesNothing()
    {
        await using var ctx = await AGmWithACampaign();

        ctx.Api.SignedIn = ("u_player", "The Player");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;

        var page = ctx.Render<Campaigns>();

        Assert.Equal("", page.Find("#join-code").GetAttribute("value"));
        Assert.Equal(before, nav.Uri);
    }

    /// <summary>
    /// The join box names the character a join would use — now a ticked row in the checkbox list
    /// rather than a sentence, since item 37 turned "who joins" into a list of several.
    ///
    /// <para><b>It used to be a sentence</b> — "Ninefold will join" — which itself replaced "the
    /// character on screen joins" for the same reason this replaces it again: naming the character
    /// is the same work a reader needs, whichever control does it. There is no "none is open" case
    /// to draw: <c>CurrentIdAsync</c> falls back to a default id rather than answering null.</para>
    /// </summary>
    [Fact]
    public async Task TheJoinBoxNamesTheCharacterThatWouldJoin()
    {
        await using var ctx = await AGmWithACampaign();

        ctx.Api.SignedIn = ("u_player", "The Player");
        ctx.Session.Sheet.Name = "Ninefold";

        var page = ctx.Render<Campaigns>();

        var row = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Ninefold", StringComparison.Ordinal));

        Assert.True(row.QuerySelector("input[type=checkbox]")!.HasAttribute("checked"));

        // And the sentences it replaced are gone, not merely joined by a better one.
        Assert.DoesNotContain("The character on screen joins", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Ninefold will join", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>An unnamed character is the one real edge, and it is said rather than left blank.</summary>
    [Fact]
    public async Task AnUnnamedCharacterIsStillNamedAsTheOneJoining()
    {
        await using var ctx = await AGmWithACampaign();

        ctx.Api.SignedIn = ("u_player", "The Player");
        ctx.Session.Sheet.Name = "   ";

        var page = ctx.Render<Campaigns>();

        var row = page.FindAll("label.house-rule")
            .Single(l => l.TextContent.Contains("Unnamed character", StringComparison.Ordinal));

        Assert.True(row.QuerySelector("input[type=checkbox]")!.HasAttribute("checked"));
    }

    /// <summary>
    /// Every link to the handout opens outside the router.
    ///
    /// <para><b>Without a target, Blazor swallows it.</b> The framework intercepts clicks on
    /// same-origin anchors and hands the path to its own router; <c>/join.html</c> is a file on
    /// disk and not a route, so the app would answer its own "no such page" and the file would
    /// never be fetched. That failure is invisible to every other test here — the anchor is
    /// present, the href is right, and the page is broken.</para>
    /// </summary>
    [Theory]
    [InlineData("Campaigns.razor")]
    [InlineData("SignIn.razor")]
    public void EveryLinkToTheHandoutLeavesTheRouter(string page)
    {
        var razor = File.ReadAllText(Path.Combine(RepoRoot(), "web", "Pages", page));

        var anchors = Regex
            .Matches(razor, @"<a\b[^>]*href\s*=\s*""join\.html""[^>]*>")
            .Select(m => m.Value)
            .ToList();

        Assert.NotEmpty(anchors);

        Assert.All(anchors, anchor =>
        {
            Assert.Contains("target=\"_blank\"", anchor, StringComparison.Ordinal);
            Assert.Contains("rel=\"noopener\"", anchor, StringComparison.Ordinal);
        });
    }

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && !File.Exists(Path.Combine(here.FullName, "CLAUDE.md")))
        {
            here = here.Parent;
        }

        Assert.NotNull(here);
        return here!.FullName;
    }
}
