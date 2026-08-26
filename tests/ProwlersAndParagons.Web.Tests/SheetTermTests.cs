using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The sheet with every name on it saying what it means, which is simply what the sheet does.
///
/// <para>The descriptions were in <c>data/rules</c> the whole time and only the editors ever showed
/// one: a printed sheet says "Presence 6d" and "Plot Hook" and "TECH POWERS" and left a reader to
/// know what those were.</para>
///
/// <para><b>There is no switch, and that is the point.</b> The explanations were briefly a separate
/// page reached by a button — one more thing to build and one more thing for a reader to find. The
/// sheet explains itself on screen everywhere it is drawn: the review step, beside the editors, and
/// every replayed recording.</para>
///
/// <para><b>What keeps it off the deliverable is the print block, not a parameter.</b> The marking
/// and the help cursor are stripped for paper, and a tip only ever appears on <c>:hover</c> or
/// <c>:focus-visible</c>, which a printer has neither of — asserted in <c>WebPresentationTests</c>
/// against the resolved cascade rather than the print block's own declarations.</para>
/// </summary>
public sealed class SheetTermTests
{
    /// <summary>
    /// The sheet, which explains itself. A named helper rather than an inline render because every
    /// test here wants one and the name is what this file is about.
    /// </summary>
    private static IRenderedComponent<SheetView> Explained(RenderContext ctx) =>
        ctx.Render<SheetView>();

    /// <summary>
    /// Every Ability and every Talent — all eighteen, bought or not, because the sheet prints them
    /// all — carries the rules data's own sentence.
    /// </summary>
    [Fact]
    public void EveryTraitOnTheSheetCarriesTheRulesDatasOwnDescription()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        var described = cut.FindAll(".trait-table .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.Equal(rules.Abilities.Count + rules.Talents.Count, described.Count);

        foreach (var trait in rules.Abilities.Select(a => (a.Name, a.Description))
                     .Concat(rules.Talents.Select(t => (t.Name, t.Description))))
        {
            // The sentence itself, not merely that there is one: a component that put the name in
            // both places would satisfy a presence check.
            Assert.Equal(trait.Description, described[trait.Name]);
        }
    }

    /// <summary>A Power on the sheet carries its own entry's description, not its neighbour's.</summary>
    [Fact]
    public void APowerCarriesItsOwnDescription()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        foreach (var chosen in ctx.Session.Sheet.SelectedPowers)
        {
            var power = rules.GetPower(chosen.PowerId)!;

            var term = cut.FindAll(".power-entry .pname .term")
                .Single(t => t.QuerySelector(".term-name")!.TextContent == power.Name);

            Assert.Equal(power.Description, term.QuerySelector(".sr-only")!.TextContent);
        }
    }

    /// <summary>
    /// <b>"Armor 8d", never "Armor8d".</b> Putting a component between a Power's name and its rank
    /// is exactly the change that has produced that bug twice in this repository — Razor strips the
    /// leading whitespace inside an element that follows an expression — and it is invisible to any
    /// test that reads source or strips tags out of markup.
    /// </summary>
    [Fact]
    public void APowersRankIsStillSeparatedFromItsName()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var ranked = ctx.Session.Sheet.SelectedPowers
            .Select(p => rules.GetPower(p.PowerId)!)
            .First(p => p.RankType is not ("default" or "special"));

        // `Visible` rather than TextContent, which would carry both hidden copies of the Power's
        // description — and rather than markup with the tags replaced, which reads "Armor 8d" out
        // of a component printing "Armor8d". See the helper.
        var name = Explained(ctx)
            .FindAll(".power-entry .pname")
            .Single(e => e.Visible().StartsWith(ranked.Name, StringComparison.Ordinal))
            .Visible();

        Assert.Matches($@"^{System.Text.RegularExpressions.Regex.Escape(ranked.Name)} \d+d$", name);
    }

    /// <summary>A Perk and a Flaw carry theirs too — including a Flaw's narrative detail beside it.</summary>
    [Fact]
    public void PerksAndFlawsCarryTheirDescriptions()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);
        var terms = cut.FindAll(".sheet-section .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.NotEmpty(ctx.Session.Sheet.Perks);
        Assert.NotEmpty(ctx.Session.Sheet.Flaws);

        foreach (var perk in ctx.Session.Sheet.Perks)
            Assert.Equal(rules.GetPerk(perk.PerkId)!.Description, terms[rules.GetPerk(perk.PerkId)!.Name]);

        foreach (var flaw in ctx.Session.Sheet.Flaws)
            Assert.Equal(rules.GetFlaw(flaw.FlawId)!.Description, terms[rules.GetFlaw(flaw.FlawId)!.Name]);
    }

    /// <summary>
    /// A Powers group's heading is a Source, and a Source is the one heading on the sheet that
    /// carries a rule. "TECH POWERS" says nothing about what a Source is.
    /// </summary>
    [Fact]
    public void ASourceHeadingExplainsWhatASourceIs()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var cut = Explained(ctx);

        var headings = cut.FindAll(".powers > h3 .term")
            .Select(t => t.QuerySelector(".sr-only")!.TextContent)
            .ToList();

        Assert.NotEmpty(headings);
        Assert.All(headings, h => Assert.Contains(h, rules.Sources.Select(s => s.Description)));

        // "Abilities", "Gear", "Perks" explain themselves and must not gain a control.
        Assert.Empty(cut.FindAll(".sheet-section:not(.powers) > h3 .term"));
    }

    /// <summary>
    /// The four big figures explain themselves through their labels, using the same sentences the
    /// derived-stats step prints under them — one set of rules, two ways of asking.
    /// </summary>
    [Fact]
    public void TheFourFiguresCarryTheRuleBehindThem()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);

        var labels = cut.FindAll(".stat-block .label .term")
            .ToDictionary(
                t => t.QuerySelector(".term-name")!.TextContent,
                t => t.QuerySelector(".sr-only")!.TextContent,
                StringComparer.Ordinal);

        Assert.Equal(4, labels.Count);
        Assert.Contains("Danger Sense", labels["Edge"], StringComparison.Ordinal);
        Assert.Contains("Toughness", labels["Health"], StringComparison.Ordinal);
        Assert.Contains("Trait Cap", labels["Resolve"], StringComparison.Ordinal);
        Assert.Contains("Hero Points", labels["Hero Points"], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Every word the sheet printed before the explanations existed, it still prints.</b>
    ///
    /// <para>This replaced a pair of tests asserting that an un-explained sheet carried no control
    /// — a state that no longer exists, because there is no switch. What has to hold instead is
    /// that wrapping forty names in a component dropped none of them: a name missing from a sheet
    /// is the kind of wrong that looks entirely right.</para>
    /// </summary>
    [Fact]
    public void TheSheetStillPrintsEveryNameAndRank()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var visible = Explained(ctx).Find(".sheet").Visible();

        foreach (var name in rules.Abilities.Select(a => a.Name)
                     .Concat(rules.Talents.Select(t => t.Name))
                     .Concat(ctx.Session.Sheet.SelectedPowers
                         .Select(p => rules.GetPower(p.PowerId)!.Name)))
        {
            Assert.Contains(name, visible, StringComparison.Ordinal);
        }

        // And the figure a player reads off mid-scene.
        Assert.Contains(ctx.Session.Derived.CalculateEdge(ctx.Session.Sheet).ToString(),
            visible, StringComparison.Ordinal);
    }

    /// <summary>
    /// Escape closes a description and moving off brings it back, which WCAG 1.4.13 asks for and
    /// the obvious implementation leaves out: a tip that can only be closed with a pointer is not
    /// dismissable by somebody who is not using one.
    /// </summary>
    [Fact]
    public void EscapeDismissesADescriptionAndLeavingTheTermRestoresIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);
        var term = cut.FindAll(".trait-table .term")[0];

        Assert.DoesNotContain("dismissed", term.QuerySelector(".row-tip")!.ClassList);

        term.QuerySelector(".term-name")!.KeyDown("Escape");
        Assert.Contains("dismissed",
            cut.FindAll(".trait-table .term")[0].QuerySelector(".row-tip")!.ClassList);

        cut.FindAll(".trait-table .term")[0].QuerySelector(".term-name")!.MouseLeave();
        Assert.DoesNotContain("dismissed",
            cut.FindAll(".trait-table .term")[0].QuerySelector(".row-tip")!.ClassList);
    }

    /// <summary>
    /// <b>The description is always in the document, and is what <c>aria-describedby</c> names.</b>
    /// Hiding the one element with <c>display: none</c> would take the sentence away from a screen
    /// reader while every rendering test stayed green — which is why there are two copies, and why
    /// the id has to resolve.
    /// </summary>
    [Fact]
    public void EveryDescribedByPointsAtSomethingThatIsThere()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var cut = Explained(ctx);
        var ids = cut.FindAll("[id]").Select(e => e.GetAttribute("id")).ToHashSet(StringComparer.Ordinal);

        var described = cut.FindAll("[aria-describedby]").ToList();

        Assert.NotEmpty(described);
        Assert.All(described, e => Assert.Contains(e.GetAttribute("aria-describedby")!, ids));
    }
}
