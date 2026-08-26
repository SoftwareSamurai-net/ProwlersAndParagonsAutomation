using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The sheet with every name on it saying what it means.
///
/// <para>The descriptions were in <c>data/rules</c> the whole time and only the editors ever showed
/// one: a printed sheet says "Presence 6d" and "Plot Hook" and "TECH POWERS" and left a reader to
/// know what those were.</para>
///
/// <para><b>The negative is the important half of this file.</b> The printed sheet is what the tool
/// produces, and <c>SheetView</c> is drawn on the review step, beside the editors, and on every
/// replayed recording. If <c>Explain</c> leaks into any of those, forty names on the deliverable
/// become controls — so the off case is asserted as hard as the on case.</para>
/// </summary>
public sealed class ExplainedSheetTests
{
    private static IRenderedComponent<SheetView> Explained(RenderContext ctx) =>
        ctx.Render<SheetView>(p => p.Add(v => v.Explain, true));

    /// <summary>
    /// The elements the sheet is built from that a browser lays out on a line of their own.
    ///
    /// <para>A closed list rather than a stylesheet lookup, because these are the block elements
    /// HTML defines as such and the sheet uses no others; what it must <b>not</b> contain is an
    /// inline element, for the reason in <see cref="Visible"/>.</para>
    /// </summary>
    private static readonly string[] OwnLine =
        ["ARTICLE", "SECTION", "DIV", "P", "H1", "H2", "H3", "UL", "OL", "LI",
         "TABLE", "TBODY", "TR", "TD", "TH", "DL", "DT", "DD", "FOOTER", "BR"];

    /// <summary>
    /// What a reader sees, modelled the way a browser renders it.
    ///
    /// <para>Two things are dropped: the <c>sr-only</c> copy of each description, which
    /// <c>aria-describedby</c> names and nobody sees, and the tip, which is <c>display: none</c>
    /// until somebody hovers.</para>
    ///
    /// <para><b>Nothing is inserted between two inline elements, and that is the whole point.</b>
    /// A browser concatenates adjacent inline text with no separator, which is why
    /// <c>&lt;b&gt;Armor&lt;/b&gt;&lt;span&gt;8d&lt;/span&gt;</c> reads "Armor8d" on the page.
    /// <b>This repository has shipped a test for that bug that was beaten by its own helper</b> —
    /// the helper replaced every tag with a newline, so the broken markup read "Armor 8d" to the
    /// assertion written to catch it. So a boundary is a separator here only where the browser
    /// makes one, which is a block element.</para>
    ///
    /// <para>Whitespace already in the markup is <em>collapsed</em>, which is also what a browser
    /// does and is not the same thing as inserting some: a run of newlines and indentation between
    /// two inline elements renders as one space, and no whitespace at all renders as none.</para>
    /// </summary>
    private static string Visible(INode node)
    {
        var lines = Gather(node)
            .Split('\n')
            .Select(line => string.Join(" ",
                line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);

        return string.Join(" ", lines);
    }

    private static string Gather(INode node)
    {
        if (node is IElement hidden &&
            (hidden.ClassList.Contains("sr-only") || hidden.ClassList.Contains("row-tip")))
            return "";

        if (node.NodeType == NodeType.Text) return node.TextContent;

        var inside = string.Concat(node.ChildNodes.Select(Gather));

        return node is IElement block && OwnLine.Contains(block.TagName, StringComparer.Ordinal)
            ? inside + "\n"
            : inside;
    }

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

        foreach (var explain in (bool[])[true, false])
        {
            // `Visible` rather than TextContent, which would carry both hidden copies of the
            // Power's description — and rather than markup with the tags replaced, which reads
            // "Armor 8d" out of a component printing "Armor8d". See the helper.
            var name = Visible(ctx.Render<SheetView>(p => p.Add(v => v.Explain, explain))
                .FindAll(".power-entry .pname")
                .Single(e => Visible(e).StartsWith(ranked.Name, StringComparison.Ordinal)));

            Assert.Matches($@"^{System.Text.RegularExpressions.Regex.Escape(ranked.Name)} \d+d$", name);
        }
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
    /// <b>The sheet drawn without explanations has no control on it at all.</b> This is the guard
    /// on the deliverable: the review step, the preview beside the editors and every replayed
    /// recording all draw <c>SheetView</c> and none of them passes <c>Explain</c>.
    /// </summary>
    [Fact]
    public void TheOrdinarySheetGainsNoControls()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var plain = ctx.Render<SheetView>();

        Assert.Empty(plain.FindAll(".term"));
        Assert.Empty(plain.FindAll(".term-name"));
        Assert.Empty(plain.FindAll(".row-tip"));

        // And the positive control beside it: the same character, explained, really does have them.
        Assert.NotEmpty(Explained(ctx).FindAll(".term-name"));
    }

    /// <summary>
    /// The review step in particular, because that is the page whose sheet goes on paper and the
    /// one somebody would be tempted to switch on in place of a page of its own.
    /// </summary>
    [Fact]
    public void TheReviewStepsSheetIsNotTheExplainedOne()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        Assert.Empty(ctx.Render<Review>().FindAll(".term-name"));
    }

    /// <summary>
    /// Every word the plain sheet prints, the explained sheet prints too. Wrapping forty names in a
    /// component is exactly the change that could drop one, and a name missing from a sheet is the
    /// kind of wrong that looks entirely right.
    /// </summary>
    [Fact]
    public void ExplainingTheSheetChangesNoneOfItsWords()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        static string Words(IRenderedComponent<SheetView> sheet) => Visible(sheet.Find(".sheet"));

        // Word for word identical, which is the strongest form this can take: the hidden copies of
        // each description are what `Visible` drops, so what is left on the explained sheet is
        // exactly what the plain one prints. Wrapping forty names in a component is precisely the
        // change that could quietly drop one, and a name missing from a sheet looks entirely right.
        Assert.Equal(Words(ctx.Render<SheetView>()), Words(Explained(ctx)));
    }

    /// <summary>The page itself draws the sheet explained, and offers the way back.</summary>
    [Fact]
    public void ThePageDrawsTheSheetExplained()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<ExplainedSheet>();

        Assert.NotEmpty(page.FindAll(".sheet .term-name"));
        Assert.Contains("build/review",
            page.FindAll("a").Select(a => a.GetAttribute("href")));
    }

    /// <summary>
    /// The review step offers a way to it, or nothing does — the page is reachable by address and
    /// that is not a way anybody finds it.
    /// </summary>
    [Fact]
    public void TheReviewStepOffersAWayToIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        Assert.Contains("build/sheet",
            ctx.Render<Review>().FindAll("a").Select(a => a.GetAttribute("href")));
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
