using Bunit;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// How a passage of the book is set on screen.
///
/// <para><b>Rendered rather than read as source, because what was wrong was the output.</b> The
/// owner sent a screenshot of the LUCK entry: a stat line, a description and two Pros run together
/// as one 200-word paragraph, with no structure at all. The splitting is
/// <c>RulebookProseTests</c>'s subject; this is the other half — that what it found reaches the
/// page as separate things a reader can look at one at a time.</para>
///
/// <para>The text here is the corpus's own wording for LUCK, quoted rather than fetched, because
/// this is a test about a component and not about the server that answers for the book.</para>
/// </summary>
public sealed class BookTextTests
{
    /// <summary>Ch.2 p.33, verbatim, exactly as flat as the extraction leaves it.</summary>
    private const string Luck =
        "Self • Power Rank • 2 Hero Points per rank You are incredibly lucky or so skilled that "
        + "you make everything look easy. You gain a number of Luck dice equal to your Luck rank "
        + "at the start of every issue. PRO Control (+4): Rather than just being lucky, this Power "
        + "represents your ability to consciously alter probability fields. PRO Unbelievable "
        + "(+1 per rank): You can also spend Luck dice to buy yourself lucky breaks.";

    private static IRenderedComponent<BookText> Render(RenderContext ctx, string text) =>
        ctx.Render<BookText>(p => p.Add(c => c.Text, text));

    /// <summary>
    /// The stat line is three labelled fields, not a run of bullets inside the prose.
    /// </summary>
    [Fact]
    public void ThePowersStatLineIsSetApartAsThreeLabelledFields()
    {
        using var ctx = new RenderContext();

        var cut = Render(ctx, Luck);

        var stat = cut.Find(".book-stat");
        Assert.Equal(["Range", "Rank", "Cost"], stat.QuerySelectorAll("dt").Select(e => e.TextContent));
        Assert.Equal(["Self", "Power Rank", "2 Hero Points per rank"],
            stat.QuerySelectorAll("dd").Select(e => e.TextContent));
    }

    /// <summary>
    /// Each Pro and Con printed inside the entry is a block of its own, carrying its marker, its
    /// name and its price — and the description no longer carries any of it.
    /// </summary>
    [Fact]
    public void EachPrintedOptionIsABlockOfItsOwn()
    {
        using var ctx = new RenderContext();

        var cut = Render(ctx, Luck);

        var options = cut.FindAll(".book-options > li");
        Assert.Equal(2, options.Count);

        // TextContent, never markup with the tags stripped: stripping a tag leaves a separator
        // where it was, so a test that did it would read "PRO Control +4" out of a component that
        // had run all three together.
        Assert.Equal("PRO", options[0].QuerySelector(".kind")!.TextContent);
        Assert.Equal("Control", options[0].QuerySelector("b")!.TextContent);
        Assert.Equal("+4", options[0].QuerySelector(".price")!.TextContent);

        // In the case the book printed it, which is why this is not `.hp`: that class sets its
        // text in capitals and would render this "+1 PER RANK".
        Assert.Equal("+1 per rank", options[1].QuerySelector(".price")!.TextContent);
        Assert.Empty(options[1].QuerySelectorAll(".hp"));
    }

    /// <summary>
    /// <b>The paragraph a reader starts on is the description and only the description.</b> This is
    /// the fault in the screenshot: everything was in it.
    /// </summary>
    [Fact]
    public void TheDescriptionIsWhatIsLeftAfterTheStatLineAndTheOptions()
    {
        using var ctx = new RenderContext();

        var cut = Render(ctx, Luck);

        // The description paragraphs are the ones outside the option list.
        var description = cut.FindAll(".book-text > p").Select(p => p.TextContent).ToList();
        var whole = string.Join(" ", description);

        Assert.StartsWith("You are incredibly lucky", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("Hero Points per rank", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("PRO", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("probability fields", whole, StringComparison.Ordinal);
    }

    /// <summary>
    /// A passage the book marks in none of those ways is drawn as its own prose and nothing else —
    /// no empty stat block, no empty option list. Most of the book is this case.
    /// </summary>
    [Fact]
    public void AnOrdinaryPassageIsJustItsProse()
    {
        using var ctx = new RenderContext();

        var cut = Render(ctx, "Distances are intentionally abstract in P&P.");

        Assert.Empty(cut.FindAll(".book-stat"));
        Assert.Empty(cut.FindAll(".book-options"));
        Assert.Contains("intentionally abstract", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The citation is drawn when it is given, because a rules answer that can be checked against
    /// the copy on the table is the difference between this and remembering.
    /// </summary>
    [Fact]
    public void ThePrintedPageIsShownWhenItIsGiven()
    {
        using var ctx = new RenderContext();

        var cut = ctx.Render<BookText>(p => p
            .Add(c => c.Text, Luck)
            .Add(c => c.Citation, "Ultimate Edition, Ch.2 Characters, printed p.33"));

        Assert.Contains("printed p.33", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing about the same passage differs between two renders of it.
    ///
    /// <para>Not a general worry, a specific one: the parse is cached in a property set from
    /// <c>OnParametersSet</c>, and a component that cached against the wrong thing would draw the
    /// previous passage's structure under this passage's heading. That is precisely how
    /// <c>RulebookEntry</c> could show one Power's rules under another's name, which is why it
    /// clears its entry before asking for the next.</para>
    /// </summary>
    [Fact]
    public void ChangingThePassageRedrawsIt()
    {
        using var ctx = new RenderContext();

        var cut = Render(ctx, Luck);
        Assert.NotEmpty(cut.FindAll(".book-stat"));

        cut.Render(p => p.Add(c => c.Text, "Distances are intentionally abstract."));

        Assert.Empty(cut.FindAll(".book-stat"));
        Assert.Empty(cut.FindAll(".book-options"));
        Assert.DoesNotContain("incredibly lucky", cut.Markup, StringComparison.Ordinal);
    }
}
