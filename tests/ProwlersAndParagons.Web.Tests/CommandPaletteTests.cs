using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The command palette: Ctrl-K, type, Enter.
///
/// <para><b>What these can and cannot see.</b> They render the component and drive its own key
/// handler, which covers what each key does and what the rows announce. They cannot see the
/// document-level listener that hears Ctrl-K in the first place, because that is a browser
/// event on an element no render tree contains — that half is a driven harness in
/// <c>ProofPages</c>, and the two together are what make the shortcut checked at all.</para>
/// </summary>
public sealed class CommandPaletteTests
{
    /// <summary>A context whose palette is already open.</summary>
    private static RenderContext Opened()
    {
        var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<Commands>().Open();
        return ctx;
    }

    private static Commands CommandsOf(RenderContext ctx) =>
        ctx.Services.GetRequiredService<Commands>();

    /// <summary>
    /// Closed, it renders nothing at all.
    ///
    /// <para>The positive control for every test below: they assert on rows, and a component
    /// that always drew its rows would satisfy most of them. This is what says the visibility
    /// is real.</para>
    /// </summary>
    [Fact]
    public void ClosedItIsNotOnThePage()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var page = ctx.Render<CommandPalette>();

        Assert.Empty(page.FindAll(".palette"));

        // ...and opening it puts it there, so "renders nothing" is not "never renders".
        ctx.Services.GetRequiredService<Commands>().Open();
        page.Render();
        Assert.Single(page.FindAll(".palette"));
    }

    /// <summary>
    /// An empty box offers the six steps and no Powers.
    ///
    /// <para>141 Powers under six steps would bury the thing the palette is mostly for. The
    /// count is asserted against the shared step list rather than against the number six, so
    /// a seventh step does not fail this for the wrong reason.</para>
    /// </summary>
    [Fact]
    public void AnEmptyBoxOffersTheStepsAlone()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        var rows = page.FindAll(".palette-row");
        Assert.Equal(Commands.Steps.Count, rows.Count);
        Assert.Equal(Commands.Steps.Select(s => s.Label), rows.Select(r => r.QuerySelector(".palette-label")!.TextContent));
    }

    /// <summary>
    /// Typing reaches the Powers, and the rulebook's own catalogue is what answers.
    ///
    /// <para>Driven through the component rather than through the service, so this covers the
    /// wiring as well as the matching.</para>
    /// </summary>
    [Fact]
    public void TypingFindsAPower()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("plast");

        var labels = page.FindAll(".palette-row .palette-label").Select(e => e.TextContent).ToList();

        Assert.Contains("Plasticity", labels);

        // And the steps are gone, because none of them matches — so this is the filter
        // working rather than the Powers being appended to a list that never narrows.
        Assert.DoesNotContain("Gear", labels);
    }

    /// <summary>
    /// The arrow keys move the current row, and movement wraps in both directions.
    ///
    /// <para>Which row is current is read off <c>aria-selected</c> rather than off the class,
    /// because the class is decoration and the attribute is what a screen reader is told. A
    /// version that moved the ring and not the attribute would look right and announce the
    /// first row for ever.</para>
    /// </summary>
    [Fact]
    public void TheArrowKeysMoveTheCurrentRowAndWrap()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        var box = page.Find(".palette-box");

        Assert.Equal(0, CurrentIndex(page));

        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal(1, CurrentIndex(page));

        // Up from the first row is the last one: a list this short is a ring.
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal(Commands.Steps.Count - 1, CurrentIndex(page));

        // ...and down from the last is the first again.
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal(0, CurrentIndex(page));
    }

    /// <summary>
    /// The box tells assistive technology which row is current, by id, and the id is a row
    /// that exists.
    ///
    /// <para><b>A dangling reference is the failure this catches</b> — the same fault as an
    /// unconditional <c>aria-controls</c> on the budget breakdown. Naming a row that is not
    /// rendered announces nothing while looking exactly like naming one that is.</para>
    /// </summary>
    [Fact]
    public void TheCurrentRowIsNamedByAnIdThatExists()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        var box = page.Find(".palette-box");
        box.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        var named = page.Find(".palette-box").GetAttribute("aria-activedescendant");
        Assert.False(string.IsNullOrEmpty(named));

        var row = page.Find($"#{named}");
        Assert.Equal("true", row.GetAttribute("aria-selected"));
    }

    /// <summary>
    /// Nothing matches: the palette says so, and names no current row.
    ///
    /// <para>The second half is the point. An empty list has nothing to be on, and a box that
    /// kept pointing at row zero would be naming an element that is not in the document.</para>
    /// </summary>
    [Fact]
    public void WithNoMatchesItSaysSoAndNamesNoRow()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("qzqzqz");

        Assert.Empty(page.FindAll(".palette-row"));
        Assert.Single(page.FindAll(".palette-empty"));
        Assert.True(string.IsNullOrEmpty(page.Find(".palette-box").GetAttribute("aria-activedescendant")));
    }

    /// <summary>Escape closes it, and the service is what holds that rather than the component.</summary>
    [Fact]
    public void EscapeCloses()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var page = ctx.Render<CommandPalette>();
        Assert.True(commands.IsOpen);

        page.Find(".palette-box").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(commands.IsOpen);
        Assert.Empty(page.FindAll(".palette"));
    }

    /// <summary>
    /// Enter on a Power asks for it and closes, rather than adding it.
    ///
    /// <para><b>This is the rule the palette must not break.</b> A Power needs ranks, variants
    /// and its Pros and Cons chosen, and the editor is the one component that knows how to
    /// price them — so the palette hands the Power over and changes nothing about the
    /// character itself. The sheet is asserted unchanged for exactly that reason.</para>
    /// </summary>
    [Fact]
    public void EnterOnAPowerRequestsItAndAddsNothing()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var before = ctx.Session.Sheet.SelectedPowers.Count;

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("plasticity");
        page.Find(".palette-box").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("plasticity", commands.RequestedPowerId);
        Assert.False(commands.IsOpen);
        Assert.Equal(before, ctx.Session.Sheet.SelectedPowers.Count);
    }

    /// <summary>
    /// The request is acted on once.
    ///
    /// <para><b>Read-once is what stops the editor reopening over whatever the reader moved
    /// on to.</b> The step that holds it re-renders on every keystroke elsewhere, so a request
    /// that stayed set would reopen on each of them, repeatedly, with nothing on screen saying
    /// why.</para>
    /// </summary>
    [Fact]
    public void ARequestedPowerIsTakenOnlyOnce()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var commands = ctx.Services.GetRequiredService<Commands>();

        commands.RequestPower("plasticity");

        Assert.Equal("plasticity", commands.TakeRequestedPower());
        Assert.Null(commands.TakeRequestedPower());
        Assert.Null(commands.RequestedPowerId);
    }

    /// <summary>
    /// The Powers section opens the editor for a requested Power, and clears the request.
    ///
    /// <para>Driven through the section itself, because "the palette lands you on the right
    /// tab with nothing open" is the failure that looks most like the feature working.</para>
    /// </summary>
    [Fact]
    public void ThePowersSectionOpensWhatWasAskedFor()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var commands = ctx.Services.GetRequiredService<Commands>();
        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        var power = rules.Powers.First(p => p.Id == "plasticity");

        // The positive control: without a request, the section shows its list and no editor.
        var listing = ctx.Render<PowersTab>();
        Assert.Empty(listing.FindAll(".power-editor, form"));
        Assert.Contains("Add a Power", listing.Markup);

        commands.RequestPower(power.Id);

        var editing = ctx.Render<PowersTab>();

        Assert.Contains(power.Name, editing.Markup);
        Assert.DoesNotContain("Add a Power", editing.Markup);
        Assert.Null(commands.RequestedPowerId);
    }

    /// <summary>
    /// The palette and the step band offer the same six steps.
    ///
    /// <para><b>Two lists would drift, and nothing about them being in different files would
    /// have caught it.</b> A palette offering a step the band does not have — or missing one
    /// it does — is worse than no palette. They read one list; this is what says so, by
    /// comparing what each actually draws rather than by reading the source.</para>
    /// </summary>
    [Fact]
    public void ThePaletteAndTheStepBandOfferTheSameSteps()
    {
        using var ctx = Opened();

        var palette = ctx.Render<CommandPalette>();
        var band = ctx.Render<StepNav>();

        var offered = palette.FindAll(".palette-row .palette-label").Select(e => e.TextContent.Trim()).ToList();
        var linked = band.FindAll(".steps-list a").Select(e => e.TextContent.Trim()).ToList();

        Assert.Equal(Commands.Steps.Count, linked.Count);

        // The band prefixes each label with its position, so compare by what each label ends
        // with rather than by equality — and assert the band really carried the label, so a
        // pair of empty strings cannot satisfy this.
        Assert.Equal(offered.Count, linked.Count);
        for (var i = 0; i < offered.Count; i++)
        {
            Assert.NotEmpty(offered[i]);
            Assert.EndsWith(offered[i], linked[i], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The matching rule is the lists' rule, not a second one.
    ///
    /// <para>What a reader has learnt about finding things in the Powers list holds here.
    /// Asserted by driving both through the same query rather than by reading the source.</para>
    /// </summary>
    [Theory]
    [InlineData("plast", "Plasticity")]
    [InlineData("PLAST", "Plasticity")]
    public void ItMatchesTheWayTheListsMatch(string query, string expected)
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var found = commands.Matching(query, 8);

        Assert.Contains(found, c => c.Label == expected);
        Assert.True(OptionFilter.Matches(query, expected));
    }

    /// <summary>The Powers offered are capped, and the steps are not.</summary>
    [Fact]
    public void ThePowersAreCappedAndTheStepsAreNot()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        // "a" reaches most of the catalogue and every step label.
        var found = commands.Matching("a", 3);

        Assert.Equal(3, found.Count(c => c.Kind == CommandKind.Power));
        Assert.Equal(
            Commands.Steps.Count(s => OptionFilter.Matches("a", [s.Label, s.Detail, .. s.Keywords])),
            found.Count(c => c.Kind == CommandKind.Step));
    }

    private static int CurrentIndex(IRenderedComponent<CommandPalette> page)
    {
        var rows = page.FindAll(".palette-row");
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].GetAttribute("aria-selected") == "true") return i;
        }

        return -1;
    }
}
