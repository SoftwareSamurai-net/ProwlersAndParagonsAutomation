using AngleSharp.Dom;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The "What is here" panel on <c>/rules</c>, which used to be a list of chapters that looked like
/// links and was not one.
///
/// <para><b>Rendered rather than read as source, because the deliverable is what reaches the
/// page.</b> A source scan cannot tell a control that is drawn from one behind a branch nothing
/// reaches, and cannot see a count that arrives through a helper.</para>
///
/// <para><b>What the scoped search itself does is not tested here.</b> That a chapter's own
/// passages come back, that <c>found</c> counts the chapter and not the book, and that a chapter
/// outside the unscoped top thirty is still reachable, are properties of the real server over the
/// real corpus and are held in <c>tests/worker/search.test.mjs</c>. What is held here is the other
/// half: that this page asks for it, says which chapter it asked about, and can get back.</para>
/// </summary>
public sealed class RulesIndexTests
{
    private static RenderContext SignedIn()
    {
        var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct_reader", "A reader");
        return ctx;
    }

    /// <summary>The panel under a given heading, or a failure naming what was there instead.</summary>
    private static IElement Panel(IRenderedComponent<RulesReference> cut, string heading)
    {
        var panels = cut.FindAll(".panel");

        var found = panels.FirstOrDefault(
            p => p.QuerySelector(".panel-head h2")?.TextContent.Trim() == heading);

        Assert.True(found is not null,
            $"No panel on /rules is headed \"{heading}\". Headings present: "
            + string.Join(", ", panels.Select(p => p.QuerySelector(".panel-head h2")?.TextContent.Trim() ?? "(none)"))
            + ". Either the panel is gone or this test has stopped reaching it, and both are what "
            + "it exists to catch.");

        return found!;
    }

    private static IRenderedComponent<RulesReference> Searched(RenderContext ctx, string query)
    {
        var cut = ctx.Render<RulesReference>();

        cut.Find("#rules-search").Input(query);
        cut.Find("form").Submit();

        return cut;
    }

    /// <summary>
    /// Every chapter offers a search of its own, and the offer names the chapter it belongs to.
    /// </summary>
    [Fact]
    public void EveryChapterRowIsAControl()
    {
        using var ctx = SignedIn();

        var cut = Searched(ctx, "knockback");
        var rows = Panel(cut, "What is here").QuerySelectorAll("li");

        // The positive control: a panel that rendered no rows satisfies every assertion below it.
        Assert.Equal(ctx.Api.Chapters.Count, rows.Length);
        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var button = row.QuerySelector("button");

            Assert.True(button is not null,
                $"The chapter row \"{row.TextContent.Trim()}\" offers nothing to press. The whole "
                + "point of this panel is that it stopped being inert.");

            Assert.False(button!.HasAttribute("disabled"),
                "A chapter row is disabled with a query in the box, so nothing here can be run.");
        }
    }

    /// <summary>
    /// <b>No passage count is printed in this panel</b> — and the identical figure elsewhere on the
    /// page is asserted to still be there.
    ///
    /// <para><b>The ban is scoped to this panel on purpose, and a wider one would have been
    /// wrong.</b> "12 passages, best 5 first" beside the results is a different number doing a real
    /// job: it says how many matched against how many are shown, which is the honesty the whole
    /// page is built around. What is dropped here is the count of how the extractor split a
    /// chapter, which answers nothing a reader asked.</para>
    /// </summary>
    [Fact]
    public void TheChapterListCountsNoPassages()
    {
        using var ctx = SignedIn();

        var cut = Searched(ctx, "knockback");
        var here = Panel(cut, "What is here").TextContent;

        Assert.DoesNotMatch(@"\d+\s+passages?", here);

        // ...and the control, without which the assertion above is satisfied by a page whose
        // panels have all gone: the results still say how many matched, in those words.
        var results = Panel(cut, "What the book says")
            .QuerySelector(".panel-head .muted")!.TextContent;

        Assert.Matches(@"\d+\s+passages?", results);
    }

    /// <summary>
    /// Pressing a chapter runs the search <b>on the server</b>, scoped to that chapter.
    ///
    /// <para>Asserted on the address as well as on the rows, because those are two different
    /// claims: rows only from one chapter is exactly what a client-side filter would also produce,
    /// and a client-side filter is filtering what survived a cap it cannot raise.</para>
    /// </summary>
    [Fact]
    public void AChapterRowAsksTheServerForThatChapter()
    {
        using var ctx = SignedIn();

        var cut = Searched(ctx, "knockback");

        // The whole book first, so the narrowing below is a change rather than the only state.
        var wide = cut.FindAll(".panel").First(
            p => p.QuerySelector(".panel-head h2")?.TextContent.Trim() == "What the book says");

        Assert.True(wide.QuerySelectorAll(".chosen > li").Length > 1,
            "the unscoped search returned one row or none, so narrowing it proves nothing");

        ctx.Api.Asked.Clear();

        // Chapter 4 in the stub is Combat, whose one passage mentions knockback in its body.
        var combat = Panel(cut, "What is here").QuerySelectorAll("li")
            .First(li => li.TextContent.Contains("Combat", StringComparison.Ordinal));

        combat.QuerySelector("button")!.Click();

        Assert.Contains(ctx.Api.Asked, a => a.Contains("chapter=4", StringComparison.Ordinal));

        var scoped = Panel(cut, "What Ch.4 says");
        var rows = scoped.QuerySelectorAll(".chosen > li");

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains("Ch.4", row.TextContent, StringComparison.Ordinal));

        // And it says so above the list. A narrowed answer under the whole book's heading reads as
        // the book having only these passages, which is the complaint this change started from.
        Assert.Equal("What Ch.4 says", scoped.QuerySelector(".panel-head h2")!.TextContent.Trim());
    }

    /// <summary>
    /// The box searches the whole book again, which is the way back out of a chapter.
    ///
    /// <para><b>A scope that survived the next query would answer a new question out of a chapter
    /// chosen for the old one</b>, and nothing on screen would look wrong — the shape of every
    /// fault this page is built against.</para>
    /// </summary>
    [Fact]
    public void SearchingAgainLeavesTheChapterBehind()
    {
        using var ctx = SignedIn();

        var cut = Searched(ctx, "knockback");

        var combat = Panel(cut, "What is here").QuerySelectorAll("li")
            .First(li => li.TextContent.Contains("Combat", StringComparison.Ordinal));

        combat.QuerySelector("button")!.Click();

        // The control: it really is scoped before the box is used again.
        Assert.NotNull(Panel(cut, "What Ch.4 says"));

        ctx.Api.Asked.Clear();
        cut.Find("form").Submit();

        Assert.All(ctx.Api.Asked, a => Assert.DoesNotContain("chapter=", a, StringComparison.Ordinal));
        Assert.NotNull(Panel(cut, "What the book says"));
    }

    /// <summary>
    /// A chapter cannot be searched for nothing, and the panel says what it is waiting for.
    ///
    /// <para><b>An empty query is answered <c>found: 0</c>, which this page prints as a sentence
    /// about the rulebook.</b> A row that ran on an empty box would therefore tell a reader that a
    /// chapter has nothing to say, on the strength of their not having typed yet.</para>
    /// </summary>
    [Fact]
    public void AChapterCannotBeSearchedForNothing()
    {
        using var ctx = SignedIn();

        var cut = ctx.Render<RulesReference>();
        var panel = Panel(cut, "What is here");

        var buttons = panel.QuerySelectorAll("li button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.True(b.HasAttribute("disabled"),
            "A chapter can be searched with an empty box, which answers a reader that the "
            + "chapter is silent when nothing was asked."));

        // A disabled control that does not say why is a dead end, so the panel says what it wants.
        var aside = panel.QuerySelector(".panel-head .muted")!.TextContent;

        Assert.False(string.IsNullOrWhiteSpace(aside),
            "Every chapter row is disabled and the panel says nothing about why.");

        // ...and the same rows come alive once there is something to search for, which is the
        // control on the assertion above: rows disabled for ever would satisfy it too.
        cut.Find("#rules-search").Input("knockback");

        Assert.All(Panel(cut, "What is here").QuerySelectorAll("li button"),
            b => Assert.False(b.HasAttribute("disabled")));
    }

    /// <summary>
    /// A scoped search that finds nothing says <b>which chapter</b> found nothing, and how to widen.
    ///
    /// <para>"Nothing in the book uses knockback" is simply false when one chapter was read, and it
    /// is the over-claim the page's own design note is written against.</para>
    /// </summary>
    [Fact]
    public void AScopedMissNamesTheChapterAndTheWayOut()
    {
        using var ctx = SignedIn();

        // A word only Ch.2 uses, then scoped to Ch.4 — a real miss inside a real chapter.
        var cut = Searched(ctx, "highest");

        var combat = Panel(cut, "What is here").QuerySelectorAll("li")
            .First(li => li.TextContent.Contains("Combat", StringComparison.Ordinal));

        combat.QuerySelector("button")!.Click();

        var state = cut.Find(".empty-state").TextContent;

        Assert.Contains("Ch.4", state, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing in the book", state, StringComparison.Ordinal);
        Assert.Contains("Search", state, StringComparison.Ordinal);

        // The control on the whole case: unscoped, the same word really is found, so the empty
        // state above is the scoping and not a search that has stopped working.
        cut.Find("form").Submit();
        Assert.Empty(cut.FindAll(".empty-state"));
    }

}
