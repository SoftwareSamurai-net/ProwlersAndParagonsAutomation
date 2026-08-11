using System.Text.RegularExpressions;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
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
    ///
    /// <para><b>This reads <c>TextContent</c>, not the markup.</b> Stripping tags out of the
    /// markup puts a separator where the tag was, so <c>&lt;b&gt;Armor&lt;/b&gt;&lt;span&gt;8d&lt;/span&gt;</c>
    /// reads as "Armor 8d" to any test that does it — which is exactly how the Powers tab kept
    /// this bug through a test written to catch it. The browser concatenates the text nodes;
    /// so does this.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void ARankNeverRunsIntoThePowerItBelongsTo(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        // Both surfaces, counted separately. Summed, a selector that stopped matching on one
        // of them could hide behind the other's count — and it nearly did: the Villain sample
        // has four Powers and the sheet seven, so one combined threshold of eight had exactly
        // zero margin.
        var onSheet = ctx.Render<SheetView>().FindAll(".power-entry .head .pname");
        var onTab = ctx.Render<PowersTab>().FindAll(".chosen > li .body > div:first-child");

        Assert.NotEmpty(onSheet);
        Assert.NotEmpty(onTab);

        var run = new Regex(@"[A-Za-z)\]]\d+d\b", RegexOptions.None, TimeSpan.FromSeconds(5));
        var backwards = new Regex(@"\b\d+d[A-Za-z(\[]", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var entry in onSheet.Concat(onTab).Select(e => Collapse(e.TextContent)))
        {
            Assert.False(run.IsMatch(entry), $"A rank is printed with no space before it: \"{entry}\"");
            Assert.False(backwards.IsMatch(entry), $"A rank runs into what follows it: \"{entry}\"");
        }
    }

    /// <summary>
    /// The same rule at the other end: a rank must actually be there, and it must be the
    /// engine's. A test that only banned the run-together spelling would be satisfied by
    /// printing no rank at all.
    ///
    /// <para>Every entry is checked against what the engine says, on both surfaces and in
    /// both modes — not three names on one page. Naming Powers means the Villain sheet had no
    /// positive rank assertion anywhere, and a rank that was right on the sheet and wrong on
    /// the Powers tab was invisible.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void EveryPowerPrintsTheRankTheEngineGivesIt(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        var expected = ctx.Session.Sheet.SelectedPowers.ToDictionary(
            sp => ctx.Session.Rules.GetPower(sp.PowerId)!.Name,
            sp => ctx.Session.Rules.GetPower(sp.PowerId)! is { RankType: "default" or "special" }
                ? null
                : (int?)ctx.Session.Derived.GetEffectiveRank(sp, ctx.Session.Sheet),
            StringComparer.Ordinal);

        Assert.NotEmpty(expected);

        // A rankless Power is in there, or the null branch below is never exercised.
        Assert.Contains(expected.Values, rank => rank is null);

        foreach (var entry in ctx.Render<SheetView>().FindAll(".power-entry .head .pname"))
        {
            var text = Collapse(entry.TextContent);
            var name = expected.Keys.Single(n => text.StartsWith(n, StringComparison.Ordinal));

            Assert.Equal(expected[name] is { } rank ? $"{name} {rank}d" : name, text);
        }
    }

    /// <summary>
    /// The sheet is a form: every Ability and all twelve Talents appear whether a rank was
    /// bought or not, because the published Hero Sheet lists them all.
    ///
    /// <para>An unbought Trait reads <c>0d</c>, not a blank. 0d is a fact about the
    /// character — the rulebook's own floor for a Talent — and printing a rule to write on
    /// instead invites someone to fill in a number the tool has already decided.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void EveryTraitIsOnTheSheetAgainstItsOwnRank(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);
        var view = ctx.Render<SheetView>();

        // Name → what is printed beside it. Checking the pair, not just that both appear
        // somewhere, is what stops a constant satisfying this: printing "0d" on every row
        // passed the version of this test that only looked for the shapes.
        var printed = view.FindAll(".trait-table tr")
            .ToDictionary(
                tr => Collapse(tr.QuerySelector("td")!.TextContent),
                tr => Collapse(tr.QuerySelector("td:last-child")!.TextContent),
                StringComparer.Ordinal);

        foreach (var ability in ctx.Session.Rules.Abilities)
            Assert.Equal($"{ctx.Session.Sheet.GetAbilityRank(ability.Id)}d", printed[ability.Name]);

        foreach (var talent in ctx.Session.Rules.Talents)
            Assert.Equal($"{ctx.Session.Sheet.GetTalentRank(talent.Id)}d", printed[talent.Name]);

        // Six Abilities and twelve Talents, all of them, bought or not — the sheet is a form.
        Assert.Equal(6 + 12, printed.Count);
        Assert.Equal(12, ctx.Session.Rules.Talents.Count);

        // An unbought Trait reads 0d, never a rule to write on: 0d is a fact about the
        // character, and a blank invites someone to fill in a number the tool has decided.
        Assert.Contains("0d", printed.Values);
        Assert.Empty(view.FindAll(".trait-table .rule-line"));
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

        // Asserted per element, not against the stripped markup. Stripping a tag leaves a
        // separator behind, so a forbidden string split across two elements survives a
        // DoesNotContain over the whole page — which is the same hole in the other direction.
        foreach (var line in ctx.Render<SheetView>().FindAll(".power-entry .statline"))
            Assert.DoesNotContain("toughness)", Collapse(line.TextContent), StringComparison.Ordinal);
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

        var headings = sheet.FindAll(".sheet-section > h3").Select(h => Collapse(h.TextContent)).ToList();

        foreach (var heading in new[] { "Abilities", "Talents", "Powers", "Perks", "Gear", "Flaws", "Origin", "Notes" })
            Assert.Contains(headings, h => h.Contains(heading, StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Unnamed", Collapse(sheet.Find(".identity .ident-name").TextContent));

        // Room to write, in every box the engine has nothing to put in — not merely 30 lines
        // somewhere on the page. This is the regression the sheet was rebuilt to remove: an
        // empty section used to print the word "None.", which is a report of what the tool
        // knows rather than a form you can fill in at the table.
        foreach (var box in new[] { "Powers", "Perks", "Gear", "Flaws", "Origin", "Notes" })
        {
            var section = sheet.FindAll(".sheet-section")
                .Single(s => s.QuerySelector("h3") is { } h
                             && Collapse(h.TextContent).Contains(box, StringComparison.OrdinalIgnoreCase));

            Assert.NotEmpty(section.QuerySelectorAll(".rule-line"));
            Assert.DoesNotContain("None.", Collapse(section.TextContent), StringComparison.Ordinal);
        }
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

        // Every box in the three columns and the foot carries a heading. Two masthead boxes
        // deliberately do not — they hold the character's name and the sheet's own title, and
        // neither would be captioned. Without this, dropping a Title printed a box with no
        // heading at all and every count above still matched.
        var captioned = sheet.FindAll(".sheet-columns .sheet-section")
            .Concat(sheet.FindAll(".sheet-foot > .sheet-section"))
            .ToList();

        Assert.True(captioned.Count >= 9, $"Only {captioned.Count} sections were found to check.");

        foreach (var section in captioned)
        {
            var heading = section.QuerySelector("h3");
            Assert.NotNull(heading);
            Assert.NotEmpty(Collapse(heading.TextContent));
        }
    }

    /// <summary>
    /// Gear reads left to right. Its rows span both columns of the stat table, which also
    /// makes each one the last cell of its row — and <c>td:last-child</c> sets those right,
    /// because that is where a rank belongs. Every piece of equipment on the sheet was flush
    /// against the right margin.
    /// </summary>
    [Fact]
    public void GearIsSetAcrossTheRowRatherThanAgainstTheMargin()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var sheet = ctx.Render<SheetView>();

        var gear = sheet.FindAll(".sheet-section")
            .Single(s => s.QuerySelector("h3") is { } h
                         && Collapse(h.TextContent).Contains("Gear", StringComparison.OrdinalIgnoreCase));

        var spanning = gear.QuerySelectorAll("td[colspan]");

        Assert.NotEmpty(spanning);
        Assert.All(spanning, td => Assert.Equal("2", td.GetAttribute("colspan")));

        foreach (var item in ctx.Session.Sheet.Gear)
            Assert.Contains(spanning, td => Collapse(td.TextContent).Contains(item.Name, StringComparison.Ordinal));
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
    /// Pros and Cons are labelled on the sheet, and <b>both</b> reach it. Run together into
    /// one comma list, a reader cannot tell which of them cost Hero Points and which paid for
    /// the rest; dropping one silently is worse, because the sheet still looks complete.
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void ProsAndConsBothReachTheSheetLabelled(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        // Neither sample buys a Pro on a Power — they are built to fill sections, not to
        // exercise this — so one is added here rather than left to chance. A Con is already
        // there in both, which is the half that used to be the only half tested.
        var powers = ctx.Session.Sheet.SelectedPowers;
        powers[0] = powers[0] with { Pros = [new SelectedProCon("armor_piercing")], Cons = [new SelectedProCon("unreliable")] };

        var lines = ctx.Render<SheetView>()
            .FindAll(".power-entry .statline")
            .Select(e => Collapse(e.TextContent))
            .ToList();

        Assert.Contains(lines, l => l.StartsWith("Pros: Armor Piercing", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Cons: Unreliable", StringComparison.Ordinal));

        // Named, never a raw id — Label() resolves against the Power's own entry first.
        Assert.DoesNotContain(lines, l => l.Contains('_', StringComparison.Ordinal));
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
    /// The sheet prints the <c>Abilities (…)</c> line the engine builds, inside the Source
    /// group it belongs to and above the Powers — which is where the published sheets put
    /// it, and the whole reason the line is not a marking on the Abilities table.
    ///
    /// <para>Asserted against the engine's own answer rather than against a string this test
    /// spells out, so the two cannot drift apart in agreement with each other. Both samples
    /// carry one: the Hero has Tech Abilities behind an Item Con, the Villain a Magic
    /// Talent.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheSheetPrintsTheTraitSourceLineInsideItsGroup(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        var groups = ctx.Session.Grouping.GroupBySource(ctx.Session.Sheet);
        var expected = groups.SelectMany(g => g.TraitLines).ToList();
        Assert.NotEmpty(expected);

        var sheet = ctx.Render<SheetView>();

        Assert.Equal(expected,
            sheet.FindAll(".power-entry.trait-sources").Select(e => Collapse(e.TextContent)));

        // Above the Powers in the same box, not appended after them. Read off the rendered
        // order of the whole column, so a line printed in the wrong group fails too.
        foreach (var group in groups.Where(g => g.TraitLines.Count > 0 && g.Powers.Count > 0))
        {
            var box = sheet.FindAll(".sheet-section.powers")
                .Single(s => Collapse(s.TextContent).StartsWith(group.Heading, StringComparison.Ordinal));

            var entries = box.QuerySelectorAll(".power-entry").Select(e => Collapse(e.TextContent)).ToList();

            Assert.Equal(group.TraitLines, entries.Take(group.TraitLines.Count));
        }
    }

    /// <summary>
    /// A Trait on its default Source prints nothing at all. Without this, a renderer that
    /// listed every Ability under INNATE POWERS would satisfy the test above — it would
    /// still match the engine — and the sheet would carry eighteen lines saying that an
    /// ordinary character is ordinary.
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TraitsOnTheirDefaultSourceArePrintedNowhere(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        var named = ctx.Session.Sheet.AbilitySources.Keys
            .Concat(ctx.Session.Sheet.TalentSources.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var onDefault = ctx.Session.Rules.Abilities.Select(a => a.Name)
            .Concat(ctx.Session.Rules.Talents.Select(t => t.Name))
            .Where(n => !named.Contains(Id(ctx, n)))
            .ToList();

        Assert.NotEmpty(onDefault);

        var lines = ctx.Render<SheetView>()
            .FindAll(".power-entry.trait-sources")
            .Select(e => Collapse(e.TextContent))
            .ToList();

        foreach (var name in onDefault)
            Assert.DoesNotContain(lines, l => l.Contains(name, StringComparison.Ordinal));
    }

    /// <summary>
    /// A Source group holding <b>only</b> a trait line still prints on the sheet — a Trait
    /// bought through powered armour on a character with no Tech Power.
    ///
    /// <para>This exists because an adversarial pass filtered the sheet's groups to those
    /// with Powers in them and <b>every test stayed green</b>. Both samples happen to put
    /// their marked Traits in a Source that also has Powers, so nothing noticed that a
    /// Trait-only group had stopped rendering. The rule is stated in CLAUDE.md — nothing
    /// that renders groups may gate on there being Powers — and three places once did.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void ASourceWithNoPowersStillPrintsOnTheSheet(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        // A Source no Power on either sample uses, so the group can only exist because of
        // the Trait. Chosen from the rules rather than named, so it cannot go stale — and
        // never the Ability default, which is filtered out precisely because it says
        // nothing. Picking the default made this test's own setup a no-op.
        var unused = ctx.Session.Rules.Sources.First(s =>
            s.Id != SourceGrouping.DefaultAbilitySourceId &&
            ctx.Session.Sheet.SelectedPowers.All(p => p.SourceId != s.Id));

        ctx.Session.Sheet.AbilitySources["intellect"] = unused.Id;

        var heading = SourceGrouping.HeadingFor(unused);
        var boxes = ctx.Render<SheetView>().FindAll(".sheet-section.powers")
            .Select(e => Collapse(e.TextContent))
            .ToList();

        var box = Assert.Single(boxes, b => b.StartsWith(heading, StringComparison.Ordinal));
        Assert.Contains("Abilities (Intellect)", box, StringComparison.Ordinal);
    }

    /// <summary>
    /// A character with a Trait Source and <b>no Powers at all</b> gets a real Powers column
    /// rather than the blank form. The test above cannot reach this: its sample still has
    /// Powers, so the "is this column empty?" branch is never taken, and reverting that
    /// branch to ask about <c>SelectedPowers</c> left the whole suite green.
    /// </summary>
    [Fact]
    public void ASheetWithATraitSourceAndNoPowersIsNotABlankForm()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.AbilitySources["might"] = "tech";

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);

        var sheet = ctx.Render<SheetView>();
        var boxes = sheet.FindAll(".sheet-section.powers").Select(e => Collapse(e.TextContent)).ToList();

        var box = Assert.Single(boxes);
        Assert.StartsWith("TECH POWERS", box, StringComparison.Ordinal);
        Assert.Contains("Abilities (Might)", box, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the other way: a character with nothing at all still gets the blank Powers form,
    /// so deleting that branch would not satisfy the test above.
    /// </summary>
    [Fact]
    public void AnEmptySheetStillPrintsABlankPowersForm()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var sheet = ctx.Render<SheetView>();

        Assert.Empty(sheet.FindAll(".sheet-section.powers"));
        Assert.Contains(sheet.FindAll(".sheet-section"), s =>
            Collapse(s.TextContent).StartsWith("Powers", StringComparison.Ordinal));
    }

    /// <summary>
    /// The Origin box lists the Sources the character draws on, and it is read off the
    /// groups rather than off the Powers — so a Trait bought through powered armour puts
    /// Tech there even with no Tech Power. Reverting it to scan Powers stayed green.
    /// </summary>
    [Fact]
    public void TheOriginBoxNamesASourceHeldOnlyByATrait()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.AbilitySources["might"] = "tech";

        var origin = ctx.Render<SheetView>().FindAll(".sheet-section")
            .Select(e => Collapse(e.TextContent))
            .Single(t => t.StartsWith("Origin", StringComparison.Ordinal));

        Assert.Contains("Tech", origin, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Powers tab is the one surface that deliberately skips a group holding only a
    /// trait line: it edits Powers, and a heading with nothing under it says less than no
    /// heading. Asserted in both directions so the exception cannot quietly become general.
    /// </summary>
    [Fact]
    public void ThePowersTabSkipsAGroupThatHoldsNoPowers()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var unusedSource = ctx.Session.Rules.Sources.First(s =>
            s.Id != SourceGrouping.DefaultAbilitySourceId &&
            ctx.Session.Sheet.SelectedPowers.All(p => p.SourceId != s.Id));

        ctx.Session.Sheet.AbilitySources["intellect"] = unusedSource.Id;

        var headings = ctx.Render<PowersTab>().FindAll("h3")
            .Select(h => Collapse(h.TextContent))
            .ToList();

        Assert.DoesNotContain(SourceGrouping.HeadingFor(unusedSource), headings);

        // The groups that do hold Powers are all still there, so this is a skip and not a
        // failure to render.
        var expected = ctx.Session.Grouping.GroupBySource(ctx.Session.Sheet)
            .Where(g => g.Powers.Count > 0)
            .Select(g => g.Heading);

        Assert.Equal(expected, headings);
    }

    private static string Id(RenderContext ctx, string traitName) =>
        ctx.Session.Rules.Abilities.FirstOrDefault(a => a.Name == traitName)?.Id
        ?? ctx.Session.Rules.Talents.First(t => t.Name == traitName).Id;

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

    /// <summary>
    /// One element's text as a reader sees it. Runs of whitespace collapse to a single space
    /// — which is what a browser does — but a missing one stays missing.
    /// </summary>
    private static string Collapse(string text) =>
        new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(text, " ").Trim();
}
