using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The one filter box, in the component every list of options shares.
///
/// <para><b>This is the only item of the redesign that came from somebody using the thing</b>
/// — scrolling 141 Powers, and four other lists that had no search at all. It lives in
/// <c>OptionList</c> so that a sixth list gets it by being a list, which is also why these
/// tests drive it through the tabs rather than through the component on its own: what matters
/// is that the tabs got it without asking.</para>
/// </summary>
public sealed class OptionFilterTests
{
    /// <summary>
    /// The count beside the box is the number of rows, and it is asserted against the rows
    /// actually drawn rather than against a figure from the rules.
    ///
    /// <para><b>This is here because the first version of the count was wrong and every test
    /// passed.</b> The tally is filled by the rows as they render and read by the list that
    /// drew them, which is a pass behind — and getting that off by one render reports double.
    /// A count is exactly the kind of thing nobody checks against the list underneath it.</para>
    /// </summary>
    [Fact]
    public void TheCountIsTheNumberOfRowsUnderIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<PowersTab>();

        var rows = page.FindAll(".options .option").Count;
        var count = page.Find(".options-count").TextContent.Trim();

        Assert.Equal($"{rows} of {rows}", count);

        // And it is the whole catalogue, not a page of it. If this ever legitimately shows a
        // subset, the assertion above still holds and this one is what says so.
        Assert.Equal(ctx.Services.GetRequiredService<RulesRepository>().Powers.Count, rows);
    }

    /// <summary>Typing narrows the list, and the count follows it down.</summary>
    [Fact]
    public void TypingNarrowsTheListAndTheCount()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<PowersTab>();
        var before = page.FindAll(".options .option").Count;

        page.Find(".options-filter input").Input("telekin");

        var after = page.FindAll(".options .option").Count;

        Assert.True(after > 0, "Filtering to a Power that exists left nothing on the page.");
        Assert.True(after < before, "Filtering changed nothing.");
        Assert.Equal($"{after} of {before}", page.Find(".options-count").TextContent.Trim());

        Assert.All(page.FindAll(".options .option"),
            row => Assert.Contains("telekin", row.TextContent, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A word that is only in a Power's tags still finds it.
    ///
    /// <para><b>The Powers tab had the only search box in the app and it read tags</b>, which
    /// are never printed on the row. Moving the box into the shared component would have
    /// quietly narrowed the one list that already worked, so a row carries the words it can be
    /// found by as well as the words it shows.</para>
    /// </summary>
    [Fact]
    public void AWordOnlyInTheTagsStillFindsThePower()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        // A tag that appears in no Power's name or stat line, so a match can only have come
        // from the keywords. Picked from the data rather than hard-coded, or this test starts
        // asserting about a tag the rules no longer carry.
        var tag = rules.Powers
            .SelectMany(p => p.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(t => t.Length > 4 && !rules.Powers.Any(p =>
                p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(tag);

        var page = ctx.Render<PowersTab>();
        page.Find(".options-filter input").Input(tag);

        Assert.NotEmpty(page.FindAll(".options .option"));
    }

    /// <summary>
    /// A filter that matches nothing says so. An empty box under a search field reads as a
    /// broken page, and the four lists this component serves are all long enough that a reader
    /// cannot tell "nothing matches" from "nothing loaded".
    /// </summary>
    [Fact]
    public void AFilterThatMatchesNothingSaysSo()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<PowersTab>();
        page.Find(".options-filter input").Input("zzzznotapower");

        Assert.Empty(page.FindAll(".options .option"));
        Assert.NotEmpty(page.FindAll(".options-empty"));
    }

    /// <summary>
    /// And it says nothing when the list is empty for its own reasons. A caller whose list is
    /// genuinely empty explains that in its own words — "None yet." — and answering a question
    /// the reader did not ask would contradict it.
    /// </summary>
    [Fact]
    public void AnEmptyListIsNotBlamedOnTheFilter()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        // Every Flaw taken, so the list of ones left to pick is empty with nothing typed.
        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        ctx.Session.Sheet.Flaws.Clear();
        foreach (var flaw in rules.Flaws) ctx.Session.Sheet.Flaws.Add(new SelectedFlaw(flaw.Id));

        var page = ctx.Render<FlawsTab>();

        Assert.Empty(page.FindAll(".options .option"));
        Assert.Empty(page.FindAll(".options-empty"));
    }

    /// <summary>
    /// Every list of options actually has a box.
    ///
    /// <para><b>This is the claim the whole change rests on and nothing asserted it.</b> The
    /// Powers tab had the only search box in the app; the point of moving it into
    /// <c>OptionList</c> was that Perks, Flaws, gear features and the Pro/Con picker got one by
    /// being lists of options. Adding <c>Filterable="false"</c> to any of those four takes the
    /// box away again with the suite green — and the one test that touched the Flaws tab
    /// asserted the <i>absence</i> of an empty-state line, which passes with no filter at
    /// all.</para>
    /// </summary>
    [Fact]
    public void EveryListOfOptionsCarriesAFilterBox()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        // The gear features are two clicks in — the list only exists once a piece of gear is
        // being customised. **That has to be driven rather than assumed**: rendering the gear
        // page and looking is what made an earlier version of this test pass on a page with no
        // options on it at all, which would have proved nothing about the very list it names.
        var gear = ctx.Render<Gear>();
        gear.FindAll(".chosen button")
            .First(b => b.TextContent.Contains("Customise", StringComparison.Ordinal))
            .Click();

        // Same again: the Pro/Con list is behind an "Add a Pro" toggle.
        // The Power is resolved before the render, so the parameter builder does not close over
        // the test context.
        var armor = ctx.Services.GetRequiredService<RulesRepository>().GetPower("armor");

        var prosAndCons = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Power, armor)
            .Add(c => c.Selected, [])
            .Add(c => c.IsPro, true));
        prosAndCons.FindAll("button")
            .First(b => b.TextContent.Contains("Add a Pro", StringComparison.Ordinal))
            .Click();

        var lists = new (string Where, IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> Page)[]
        {
            ("Powers", ctx.Render<PowersTab>()),
            ("Perks", ctx.Render<PerksTab>()),
            ("Flaws", ctx.Render<FlawsTab>()),
            ("Gear features", gear),
            ("Pros and Cons", prosAndCons)
        };

        foreach (var (where, page) in lists)
        {
            // A list that renders no options at all cannot be evidence either way, so it has to
            // have something in it before the box means anything.
            Assert.True(page.FindAll(".options .option").Count > 0,
                $"The {where} list rendered no options, so this test proves nothing about it.");

            Assert.True(page.FindAll(".options-filter input").Count > 0,
                $"The {where} list has no filter box. Moving the box into OptionList was the "
                + "whole point of the change; this list has opted out of it.");
        }
    }

    /// <summary>
    /// A word that appears only in a row's description finds it.
    ///
    /// <para>Four of the five placeholders promise this in as many words — "Filter Perks by
    /// name or description" — and the two tests above match on a <b>name</b> and on a
    /// <b>tag</b>. Dropping <c>Caveat</c> from what the filter reads left both green and made
    /// those four placeholders lie.</para>
    /// </summary>
    /// <param name="which">
    /// <b>Every list that promises it, not one of them.</b> Covering Flaws alone left the Perks
    /// tab free to stop passing its description to the row while its own placeholder still read
    /// "Filter Perks by name or description" — green, and the promise false. Gear features and
    /// the Pro/Con picker were equally unguarded.
    /// </param>
    [Theory]
    [InlineData("flaws")]
    [InlineData("perks")]
    [InlineData("gear")]
    public void AWordOnlyInTheDescriptionFindsTheRow(string which)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        // Chosen from the data: a word in some row's description that is in no row's name, so a
        // match can only have come from the description.
        var (names, descriptions) = which switch
        {
            "flaws" => (rules.Flaws.Select(f => f.Name).ToList(),
                        rules.Flaws.Select(f => f.Description ?? "").ToList()),
            "perks" => (rules.Perks.Select(p => p.Name).ToList(),
                        rules.Perks.Select(p => p.Description ?? "").ToList()),
            _ => (rules.GearFeatures.Select(g => g.Name).ToList(),
                  rules.GearFeatures.Select(g => g.Description ?? "").ToList())
        };

        var word = descriptions
            .SelectMany(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.Trim('.', ',', '(', ')', ';', ':', '"'))
            .Where(w => w.Length > 6)
            .FirstOrDefault(w => !names.Any(n => n.Contains(w, StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(word);

        var page = Page(ctx, which);

        // **Scoped, because the Gear step has two lists now** — Chapter 6's catalogue and the
        // custom features — and an unscoped selector types into the first and reads the rows of
        // both. That is not a detail: it made this test assert that thirteen unfiltered rows all
        // contained a word nobody had filtered on.
        var scope = Scope(which);

        page.Find($"{scope}.options-filter input").Input(word);

        var rows = page.FindAll($"{scope}.options .option");

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
            Assert.Contains(word, row.TextContent, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The tab holding one of the filtered lists, driven to where the list exists.</summary>
    /// <summary>
    /// Which list on the page this case is about. Empty for the two tabs that have one; the Gear
    /// step has two, and the custom features are the list with descriptions on it.
    /// </summary>
    private static string Scope(string which) => which == "gear" ? ".customising " : "";

    private static IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> Page(
        RenderContext ctx, string which)
    {
        if (which == "flaws") return ctx.Render<FlawsTab>();
        if (which == "perks") return ctx.Render<PerksTab>();

        var gear = ctx.Render<Gear>();
        gear.FindAll(".chosen button")
            .First(b => b.TextContent.Contains("Customise", StringComparison.Ordinal))
            .Click();

        return gear;
    }

    /// <summary>
    /// The tier cards turn the box off. Six choices meant to be compared are not a list to be
    /// searched, and a filter over them would be furniture.
    /// </summary>
    [Fact]
    public void TheTierCardsHaveNoFilterBox()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<ChooseTier>();

        Assert.Empty(page.FindAll(".options-filter"));
        Assert.NotEmpty(page.FindAll(".options.cards .option"));
    }
}
