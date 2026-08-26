using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The GM review step's findings name the step that caused them.
///
/// <para><b>The gap this closes was recorded in <c>PROGRESS.md</c> item 2</b>, found by an
/// adversarial audit: the review step listed findings with no route back. A reader on the last of
/// six steps was told which rule was broken and left to work out which earlier step holds the
/// thing that broke it.</para>
///
/// <para><b>The routing is asserted against real findings the validator actually produced, never
/// against hand-built <c>ValidationIssue</c>s.</b> A hand-built issue would let this file agree
/// with a <c>SubjectKind</c> the engine never files that code under — which is the failure the
/// engine-side <c>ValidationIssueStructureTests</c> exists to prevent on the other side of the
/// same seam. Every sheet below is broken in a specific way and the engine is asked what it
/// thinks.</para>
/// </summary>
public sealed class FindingRouteTests
{
    /// <summary>The three addresses this app routes a finding to.</summary>
    private static readonly string[] StepAddresses = ["build", "build/characteristics", "build/gear"];

    /// <summary>
    /// A sheet at Standard tier with every Trait floored, so the only findings are the ones a
    /// test deliberately provokes.
    ///
    /// <para>Without this every character is eighteen <c>TRAIT_BELOW_MINIMUM</c> errors deep —
    /// Ch.2 floors all six Abilities and all twelve Talents at 1d — and a test looking for one
    /// finding would be picking it out of a crowd.</para>
    /// </summary>
    private static CharacterSheet Legal(RenderContext ctx)
    {
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPackageId = "superhero";

        foreach (var ability in ctx.Session.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 3;
        foreach (var talent in ctx.Session.Rules.Talents) sheet.TalentRanks[talent.Id] = 3;

        return sheet;
    }

    private static ValidationIssue Only(RenderContext ctx, string code)
    {
        var issues = ctx.Session.Validate().Issues.Where(i => i.Code == code).ToList();

        Assert.True(issues.Count > 0,
            $"the engine produced no {code} finding for this sheet, so this test is asserting "
            + "about a case it never reached. Fix the sheet, not the assertion.");

        return issues[0];
    }

    [Fact]
    public void AnAbilityOverTheTraitCapPointsAtAbilities()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);
        sheet.AbilityRanks["might"] = 99;

        var to = FindingRoute.For(Only(ctx, "TRAIT_ABOVE_CAP"), sheet);

        Assert.NotNull(to);
        Assert.Equal("build/characteristics", to!.Value.Href);
        Assert.Equal("abilities", to.Value.Section);
        Assert.Equal("Abilities", to.Value.Label);
    }

    /// <summary>
    /// A Power's finding carries the Power, so the reader lands on its editor rather than on a
    /// list of everything they have.
    /// </summary>
    [Fact]
    public void APowersFindingCarriesThePowerItself()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);
        // invisibility is max_rank 0 — ranks bought on it are an error the validator names.
        sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));

        var to = FindingRoute.For(Only(ctx, "POWER_HAS_NO_RANK"), sheet);

        Assert.NotNull(to);
        Assert.Equal("powers", to!.Value.Section);
        Assert.Equal("invisibility", to.Value.PowerId);
    }

    /// <summary>
    /// <b>The budget is deliberately unrouted, and this is the assertion that keeps it that
    /// way.</b> Every purchase on the character contributes to it, so naming one step would be
    /// naming one of several answers as though it were the answer.
    /// </summary>
    [Fact]
    public void TheBudgetFindingGetsNoLinkAtAll()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);

        // Expensive enough to actually break 125, rather than expensive-looking. Setting the six
        // Abilities to the Trait Cap was the first attempt and did not do it — the package covers
        // the first 3d of each — which `Only` reported rather than passing on a case it never
        // reached. Ranked Powers are what puts it over.
        foreach (var ability in ctx.Session.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 12;
        foreach (var talent in ctx.Session.Rules.Talents) sheet.TalentRanks[talent.Id] = 12;

        Assert.Null(FindingRoute.For(Only(ctx, "HP_BUDGET_EXCEEDED"), sheet));
    }

    /// <summary>
    /// Renders the real page and reads what a reader would see.
    ///
    /// <para><b>A positive control on the fixture comes first</b>: the page has to be showing
    /// findings at all, or "every finding has a link" is satisfied by a page showing none — the
    /// failure shape this repository has shipped four times.</para>
    /// </summary>
    [Fact]
    public void TheReviewPageDrawsALinkOnEveryFindingThatHasOne()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);
        sheet.AbilityRanks["might"] = 99;
        sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));

        var page = ctx.Render<Review>();
        var rows = page.FindAll("ul.issues li");

        Assert.True(rows.Count >= 2,
            $"the review page is showing {rows.Count} findings; this test needs the two it "
            + "provokes to actually be on the page before it can assert anything about them.");

        var routable = ctx.Session.Validate().Issues
            .Count(i => FindingRoute.For(i, sheet) is not null);
        var links = page.FindAll("ul.issues a.finding-step");

        Assert.Equal(routable, links.Count);

        // Every link goes somewhere the app actually routes, and says something.
        Assert.All(links, link =>
        {
            var href = link.GetAttribute("href");
            Assert.False(string.IsNullOrWhiteSpace(href));
            Assert.Contains(href, StepAddresses);
            Assert.False(string.IsNullOrWhiteSpace(link.TextContent));
        });
    }

    /// <summary>
    /// <b>Clicking it actually does the two things, in the order that matters.</b>
    ///
    /// <para>Everything above asserts the markup and the routing function; none of it would notice
    /// a handler that renders a perfect link and wires nothing to it. The request has to be set
    /// <em>before</em> the step is reached, because <c>Characteristics.OnInitialized</c> reads it
    /// on the way in — so this asserts the request is standing at the moment the address
    /// changes, which is the property, rather than that two things happened in some order.</para>
    /// </summary>
    [Fact]
    public void ClickingItAsksForTheSectionAndThenGoesThere()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);
        sheet.AbilityRanks["might"] = 99;

        var commands = ctx.Services.GetRequiredService<Commands>();
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        // The control: nothing is asked for until the reader asks.
        Assert.Null(commands.RequestedSection);

        string? sectionWhenTheAddressChanged = null;
        nav.LocationChanged += (_, _) => sectionWhenTheAddressChanged = commands.RequestedSection;

        ctx.Render<Review>().Find("ul.issues a.finding-step").Click();

        Assert.EndsWith("build/characteristics", nav.Uri, StringComparison.Ordinal);
        Assert.Equal("abilities", sectionWhenTheAddressChanged);
    }

    /// <summary>
    /// <b>The step consumes the request rather than leaving it standing.</b> A section left set
    /// would drag the reader back to that tab every time anything else on the step re-rendered —
    /// which is why <c>Commands.TakeRequestedSection</c> takes rather than peeks, unlike the Power
    /// beside it, which two components need.
    /// </summary>
    [Fact]
    public void TheStepTakesTheSectionSoItActsOnce()
    {
        using var ctx = new RenderContext();
        Legal(ctx);

        var commands = ctx.Services.GetRequiredService<Commands>();
        commands.RequestSection("flaws");

        var page = ctx.Render<Characteristics>();

        Assert.Null(commands.RequestedSection);
        Assert.NotNull(page.Find("[aria-current='true']"));
    }

    /// <summary>
    /// <b>It is a real link, not a button dressed as one.</b> The destination is another address,
    /// so it has to be announced as a link and openable in a new tab — the middle button fires
    /// <c>auxclick</c> and never reaches the click handler, so the <c>href</c> is what serves it.
    /// A <c>&lt;button&gt;</c> here would look identical and do neither.
    /// </summary>
    [Fact]
    public void TheStepIsALinkWithARealAddress()
    {
        using var ctx = new RenderContext();
        var sheet = Legal(ctx);
        sheet.AbilityRanks["might"] = 99;

        var link = ctx.Render<Review>().Find("ul.issues a.finding-step");

        Assert.Equal("A", link.TagName);
        Assert.Equal("build/characteristics", link.GetAttribute("href"));
    }
}
