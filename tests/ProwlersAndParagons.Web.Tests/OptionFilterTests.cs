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
