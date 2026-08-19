using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Moving through a list of options from the keyboard, with the caret never leaving the box.
///
/// <para><b>Driven through the Powers tab rather than through the component alone</b>, for the
/// reason the filter's own tests are: what matters is that the five pickable lists got this by
/// being lists of options, not that a component in isolation can do it.</para>
/// </summary>
public sealed class OptionListKeyboardTests
{
    private static IRenderedComponent<PowersTab> Powers(RenderContext ctx) => ctx.Render<PowersTab>();

    private static void Press(IRenderedComponent<PowersTab> page, string key) =>
        page.Find(".options-filter input").KeyDown(new KeyboardEventArgs { Key = key });

    private static int CurrentIndex(IRenderedComponent<PowersTab> page)
    {
        var rows = page.FindAll(".options .option");
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].GetAttribute("aria-selected") == "true") return i;
        }

        return -1;
    }

    /// <summary>
    /// The box owns the cursor and the rows are options of a listbox.
    ///
    /// <para><b>The <c>tabindex</c> is the half worth asserting.</b> The rows are still buttons,
    /// so without it 141 Powers are 141 tab stops between the filter and anything after it —
    /// which is the state this replaces, and which reads as "keyboard support" to anybody
    /// checking that Tab reaches things.</para>
    /// </summary>
    [Fact]
    public void TheBoxIsACombobxAndTheRowsAreItsOptions()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        var box = page.Find(".options-filter input");
        Assert.Equal("combobox", box.GetAttribute("role"));

        var list = page.Find(".options");
        Assert.Equal("listbox", list.GetAttribute("role"));
        Assert.Equal(box.GetAttribute("aria-controls"), list.GetAttribute("id"));

        Assert.All(page.FindAll(".options .option"), row =>
        {
            Assert.Equal("option", row.GetAttribute("role"));
            Assert.Equal("-1", row.GetAttribute("tabindex"));
        });
    }

    /// <summary>The arrows move the cursor, and it wraps at both ends.</summary>
    [Fact]
    public void TheArrowsMoveTheCursorAndWrap()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        // Narrowed first, so the wrap is over a handful of rows rather than 141.
        page.Find(".options-filter input").Input("plast");
        var last = page.FindAll(".options .option").Count - 1;

        Assert.Equal(0, CurrentIndex(page));

        Press(page, "ArrowDown");
        Assert.Equal(Math.Min(1, last), CurrentIndex(page));

        Press(page, "Home");
        Press(page, "ArrowUp");
        Assert.Equal(last, CurrentIndex(page));

        Press(page, "End");
        Assert.Equal(last, CurrentIndex(page));

        Press(page, "ArrowDown");
        Assert.Equal(0, CurrentIndex(page));
    }

    /// <summary>
    /// The box names the row the cursor is on, and it is a row that exists.
    ///
    /// <para>A dangling reference announces nothing while looking exactly like naming a row that
    /// is there — the same fault as an unconditional <c>aria-controls</c>.</para>
    /// </summary>
    [Fact]
    public void TheCursorIsNamedByAnIdThatExists()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        page.Find(".options-filter input").Input("plast");
        Press(page, "ArrowDown");

        var named = page.Find(".options-filter input").GetAttribute("aria-activedescendant");
        Assert.False(string.IsNullOrEmpty(named));

        Assert.Equal("true", page.Find($"#{named}").GetAttribute("aria-selected"));
    }

    /// <summary>
    /// Enter picks the row the cursor is on — the whole point, and the part that cannot work by
    /// itself.
    ///
    /// <para><b>The row is never focused, so the browser cannot activate it.</b> The row's own
    /// callback is carried back through the tally; without that the arrows would move a
    /// highlight that Enter could do nothing with.</para>
    /// </summary>
    [Fact]
    public void EnterPicksTheRowTheCursorIsOn()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        // The Powers list opens an editor rather than adding, so the editor appearing for the
        // named Power is what says the right row was chosen.
        page.Find(".options-filter input").Input("plasticity");
        var name = page.Find(".options .option .name").TextContent.Trim();

        Press(page, "Enter");

        Assert.Empty(page.FindAll(".options-filter"));
        Assert.Contains(name, page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Enter picks the row the cursor moved to, not the first one.
    ///
    /// <para>The positive control for the test above: an implementation that always ran the first
    /// row's callback passes it, and would be a list whose arrow keys do nothing at all.</para>
    /// </summary>
    [Fact]
    public void EnterFollowsTheCursorRatherThanTakingTheFirstRow()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        page.Find(".options-filter input").Input("a");

        var rows = page.FindAll(".options .option");
        var first = rows[0].QuerySelector(".name")!.TextContent.Trim();
        var second = rows[1].QuerySelector(".name")!.TextContent.Trim();
        Assert.NotEqual(first, second);

        Press(page, "ArrowDown");
        Press(page, "Enter");

        Assert.Contains(second, page.Markup, StringComparison.Ordinal);
    }

    /// <summary>Escape empties the box rather than leaving it — a filter's Escape means "show everything".</summary>
    [Fact]
    public void EscapeClearsTheQuery()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        var all = page.FindAll(".options .option").Count;

        page.Find(".options-filter input").Input("plast");
        Assert.NotEqual(all, page.FindAll(".options .option").Count);

        Press(page, "Escape");
        Assert.Equal(all, page.FindAll(".options .option").Count);
    }

    /// <summary>
    /// Typing puts the cursor back on the first row.
    ///
    /// <para>Keeping it would leave the reader on whatever happened to be fourth in a list they
    /// have just replaced — and, worse, on a row the new filter may not even have let through.</para>
    /// </summary>
    [Fact]
    public void TypingReturnsTheCursorToTheTop()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = Powers(ctx);

        page.Find(".options-filter input").Input("a");
        Press(page, "ArrowDown");
        Press(page, "ArrowDown");
        Assert.Equal(2, CurrentIndex(page));

        page.Find(".options-filter input").Input("ar");
        Assert.Equal(0, CurrentIndex(page));
    }

    /// <summary>
    /// A list with no filter box is left exactly as it was.
    ///
    /// <para><b>The tier cards are the case this protects.</b> Six choices to compare and Tab
    /// through, not a catalogue to move a cursor through — and announcing a listbox nothing is
    /// driving would promise a keyboard behaviour that is not there, which is worse than
    /// promising none.</para>
    /// </summary>
    [Fact]
    public void AListWithNoBoxIsStillPlainButtons()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        var cards = page.Find(".options.cards");
        Assert.Null(cards.GetAttribute("role"));

        Assert.All(page.FindAll(".options.cards .option"), row =>
        {
            Assert.Null(row.GetAttribute("role"));
            Assert.Null(row.GetAttribute("tabindex"));
            Assert.Null(row.GetAttribute("aria-selected"));
        });
    }
}
