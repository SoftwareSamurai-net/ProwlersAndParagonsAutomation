using System.Text.RegularExpressions;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// What the sheet actually renders.
///
/// <para><b>This file exists because of "Armor8d".</b> Razor strips the leading whitespace
/// inside a <c>&lt;text&gt;</c> block, and inside an element that follows an expression — so
/// <c>@name</c> followed by <c>&lt;text&gt; @(rank)d&lt;/text&gt;</c> printed the Power's
/// name and its rank with nothing between them. It shipped, it was fixed on the sheet, and
/// the identical bug in a second spelling was left live on the Powers tab for another whole
/// slice, because every test this repository had reads source files and no source file
/// looks wrong.</para>
///
/// <para>So these assert on rendered markup, and nothing here would pass if the separator
/// went away again.</para>
/// </summary>
public sealed class SheetRenderTests
{
    /// <summary>
    /// A Power's name and its rank, in every place either is printed. The rendered text has
    /// to read "Armor 8d" — a name, a space, a rank.
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void ARankNeverRunsIntoThePowerItBelongsTo(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        foreach (var text in new[]
                 {
                     ctx.Render<SheetView>().Markup,
                     ctx.Render<PowersTab>().Markup
                 })
        {
            var joined = Rendered(text);
            var run = new Regex(@"[A-Za-z)\]](\d+)d\b", RegexOptions.None, TimeSpan.FromSeconds(5)).Match(joined);

            Assert.False(run.Success,
                $"A rank is printed with no space before it: \"{Excerpt(joined, run.Index)}\"");
        }
    }

    /// <summary>
    /// The same rule at the other end: a rank must actually be there. A test that only
    /// banned the run-together spelling would be satisfied by printing no rank at all.
    /// </summary>
    [Fact]
    public void EveryRankedPowerPrintsItsRank()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var sheet = Rendered(ctx.Render<SheetView>().Markup);

        Assert.Contains("Armor 8d", sheet, StringComparison.Ordinal);
        Assert.Contains("Danger Sense 11d", sheet, StringComparison.Ordinal);
        Assert.Contains("Stun 6d", sheet, StringComparison.Ordinal);

        // A rankless Power prints its name and no rank at all, not "Communications 0d".
        Assert.Contains("Communications", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("Communications 0d", sheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sheet is a form: every Ability and all twelve Talents appear whether a rank was
    /// bought or not, because the published Hero Sheet lists them all.
    ///
    /// <para>An unbought Trait reads <c>0d</c>, not a blank. 0d is a fact about the
    /// character — the rulebook's own floor for a Talent — and printing a rule to write on
    /// instead invites someone to fill in a number the tool has already decided.</para>
    /// </summary>
    [Fact]
    public void EveryAbilityAndTalentIsOnTheSheetWithANumber()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var view = ctx.Render<SheetView>();
        var sheet = Rendered(view.Markup);

        foreach (var ability in ctx.Session.Rules.Abilities)
            Assert.Contains(ability.Name, sheet, StringComparison.Ordinal);

        foreach (var talent in ctx.Session.Rules.Talents)
            Assert.Contains(talent.Name, sheet, StringComparison.Ordinal);

        Assert.Equal(12, ctx.Session.Rules.Talents.Count);

        // Ninth Precinct buys four Talents; the other eight print 0d rather than a rule.
        Assert.Contains("0d", sheet, StringComparison.Ordinal);
        Assert.Empty(view.FindAll(".trait-table .rule-line"));

        // Every Trait row carries a rank, and every rank is a number followed by d.
        var ranks = view.FindAll(".trait-table tr > td:last-child").Select(td => td.TextContent.Trim());
        Assert.All(ranks, r => Assert.Matches(@"^\d+d$", r));
    }

    /// <summary>
    /// A Power's rank is printed whenever it has one, including 0d. Only a genuinely
    /// rankless Power gets no figure — and it gets, instead, the rank that stands in for it
    /// when another Power acts on it, which is the number a player needs the moment their
    /// Communications is Drained.
    /// </summary>
    [Fact]
    public void ARanklessPowerPrintsTheRankThatStandsInForIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var sheet = Rendered(ctx.Render<SheetView>().Markup);

        // Communications is rankless, Tech-Sourced, and Tech reads Toughness — 8d here.
        Assert.Contains("Against other Powers: Toughness 8d", sheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// A baseline-rank Power says which Trait it derives from, set the way the rulebook sets
    /// it. This used to read "(½ toughness)" at a player holding a book that capitalises
    /// every Trait — while the formatter's own doc comment claimed otherwise.
    /// </summary>
    [Fact]
    public void ABaselinePowerNamesTheTraitItDerivesFrom()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var sheet = Rendered(ctx.Render<SheetView>().Markup);

        Assert.Contains("Baseline Rank (½ Toughness)", sheet, StringComparison.Ordinal);
        Assert.Contains("Baseline Rank (Perception)", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("toughness)", sheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hero Point costs are set apart from the numbers a player rolls. A cost is bookkeeping
    /// — consulted when rebuilding a character, never at the table — and setting it in the
    /// same face as a rank made the sheet read as a receipt.
    /// </summary>
    [Fact]
    public void HeroPointCostsAreSetApartFromRanks()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var view = ctx.Render<SheetView>();

        var costs = view.FindAll(".hp").Select(e => e.TextContent.Trim()).ToList();

        Assert.NotEmpty(costs);
        Assert.All(costs, c => Assert.EndsWith("HP", c, StringComparison.Ordinal));

        // And nothing that is *not* a cost has borrowed the treatment.
        Assert.DoesNotContain(view.FindAll(".trait-table .hp"), _ => true);
    }

    /// <summary>
    /// An untouched sheet renders a blank form rather than a page of "None." — the state a
    /// player is in when they print before building anything, and the one the old sheet was
    /// useless in.
    /// </summary>
    [Fact]
    public void AnEmptyCharacterStillRendersAUsableForm()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Render<SheetView>();

        Assert.Contains("Unnamed", sheet.Markup, StringComparison.Ordinal);

        foreach (var heading in new[] { "Abilities", "Talents", "Powers", "Perks", "Gear", "Flaws", "Origin", "Notes" })
            Assert.Contains(heading, Rendered(sheet.Markup), StringComparison.OrdinalIgnoreCase);

        // Room to write, in the boxes the engine has no data for.
        Assert.True(sheet.FindAll(".rule-line").Count > 30);
    }

    /// <summary>
    /// Every section of a printed sheet is a ruled box, and every box gets a heading — that
    /// is the layout the published sheet uses and the thing that makes it read as a form
    /// rather than as a report.
    /// </summary>
    [Fact]
    public void EverySheetSectionIsARuledBox()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var sheet = ctx.Render<SheetView>();

        Assert.Equal(3, sheet.FindAll(".sheet-masthead > .sheet-section").Count);
        Assert.Equal(3, sheet.FindAll(".sheet-columns > .sheet-col").Count);
        Assert.Equal(3, sheet.FindAll(".sheet-foot > .sheet-section").Count);

        // Four figures, not three: Hero Points joins Edge, Health and Resolve.
        Assert.Equal(4, sheet.FindAll(".stat-blocks.quad .stat-block").Count);
    }

    /// <summary>
    /// A Villain has no Hero Point budget (Ch.9), so the fourth box shows what they cost
    /// rather than a spend against nothing — and the budget sub-line is absent, not zero.
    /// </summary>
    [Fact]
    public void TheFourthFigureAdaptsToTheMode()
    {
        using var hero = new RenderContext().With(SheetMode.Hero);
        var heroBox = hero.Render<SheetView>().FindAll(".stat-blocks.quad .stat-block")[3];

        Assert.Contains("Hero Points", heroBox.TextContent, StringComparison.Ordinal);
        Assert.Contains("of 125", heroBox.TextContent, StringComparison.Ordinal);

        using var villain = new RenderContext().With(SheetMode.Villain);
        var villainBox = villain.Render<SheetView>().FindAll(".stat-blocks.quad .stat-block")[3];

        Assert.Contains("Points Spent", villainBox.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(" of ", villainBox.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Powers print under their Source heading, the way the published sheets set them, and a
    /// Power with no Source still prints — under a plain heading at the end. Leaving one off
    /// its own character sheet would be worse than showing it unsourced.
    /// </summary>
    [Fact]
    public void PowersPrintUnderTheirSourceHeadings()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);
        var headings = ctx.Render<SheetView>()
            .FindAll(".sheet-section.powers > h3")
            .Select(h => h.TextContent)
            .ToList();

        Assert.Contains("MAGIC POWERS", headings);
        Assert.Contains("POWERS", headings);          // the unsourced fallback
    }

    /// <summary>
    /// Pros and Cons are labelled on the sheet. Run together into one comma list, a reader
    /// cannot tell which of them cost Hero Points and which paid for the rest.
    /// </summary>
    [Fact]
    public void ProsAndConsAreLabelledSeparately()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);
        var sheet = Rendered(ctx.Render<SheetView>().Markup);

        Assert.Contains("Cons: Unreliable", sheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing on a rendered page may carry a raw id. This catches the class of bug the
    /// source-reading tests cannot: a lookup that silently falls back to its id argument
    /// renders <c>super_senses_thermal_vision</c> and no source file says so.
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void NoRenderedTextShowsASnakeCaseId(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);
        var snake = new Regex(@"\b[a-z]+(_[a-z]+)+\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var (name, markup) in new[]
                 {
                     ("SheetView", ctx.Render<SheetView>().Markup),
                     ("PowersTab", ctx.Render<PowersTab>().Markup)
                 })
        {
            var leak = snake.Match(Rendered(markup));
            Assert.False(leak.Success, $"{name} renders the id '{leak.Value}' at the player.");
        }
    }

    /// <summary>
    /// Text as a reader sees it: tags removed, entities resolved, runs of whitespace
    /// collapsed — but <b>not</b> collapsed away, because a missing space is the whole point.
    /// </summary>
    private static string Rendered(string markup)
    {
        var text = new Regex("<[^>]*>", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(markup, "\n");
        text = System.Net.WebUtility.HtmlDecode(text);
        return new Regex(@"[ \t]+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(text, " ");
    }

    private static string Excerpt(string text, int at) =>
        text.Substring(Math.Max(0, at - 30), Math.Min(60, text.Length - Math.Max(0, at - 30))).Replace('\n', ' ');
}
