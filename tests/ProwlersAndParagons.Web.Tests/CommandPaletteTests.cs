using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using static ProwlersAndParagons.Web.Tests.BusyRenderer;

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
    /// An empty box offers the steps alone and no Powers.
    ///
    /// <para>141 Powers under seven steps would bury the thing the palette is mostly for. The
    /// count is asserted against the shared step list rather than against a number, so an
    /// eighth step does not fail this for the wrong reason.</para>
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
    public async Task TypingFindsAPower()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "plast" }),
            "the word typed into the box");

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
    public async Task TheArrowKeysMoveTheCurrentRowAndWrap()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        var box = page.Find(".palette-box");

        Assert.Equal(0, CurrentIndex(page));

        await Occupying(page, () => box.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }),
            "the first press down");
        Assert.Equal(1, CurrentIndex(page));

        // Up from the first row is the last one: a list this short is a ring.
        await Occupying(page, async () =>
        {
            await box.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
            await box.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        }, "the two presses up");
        Assert.Equal(Commands.Steps.Count - 1, CurrentIndex(page));

        // ...and down from the last is the first again.
        await Occupying(page, () => box.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }),
            "the press down that wraps");
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
    public async Task TheCurrentRowIsNamedByAnIdThatExists()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        var box = page.Find(".palette-box");

        await Occupying(page, () => box.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }),
            "the press that moves the current row");

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
    public async Task WithNoMatchesItSaysSoAndNamesNoRow()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "qzqzqz" }),
            "the word nothing matches");

        Assert.Empty(page.FindAll(".palette-row"));
        Assert.Single(page.FindAll(".palette-empty"));
        Assert.True(string.IsNullOrEmpty(page.Find(".palette-box").GetAttribute("aria-activedescendant")));
    }

    /// <summary>Escape closes it, and the service is what holds that rather than the component.</summary>
    [Fact]
    public async Task EscapeCloses()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var page = ctx.Render<CommandPalette>();
        Assert.True(commands.IsOpen);

        await Occupying(
            page,
            () => page.Find(".palette-box")
                      .KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }),
            "the Escape that closes the palette");

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
    public async Task EnterOnAPowerRequestsItAndAddsNothing()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var before = ctx.Session.Sheet.SelectedPowers.Count;

        var page = ctx.Render<CommandPalette>();
        // Both awaited, and in one occupation: Enter runs whatever row the keystroke before it
        // left current, so a posted `Input` means Enter lands on the steps rather than on a Power.
        await Occupying(page, async () =>
        {
            await page.Find(".palette-box")
                      .InputAsync(new ChangeEventArgs { Value = "plasticity" });
            await page.Find(".palette-box")
                      .KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        }, "the word and the Enter on it");

        Assert.Equal("plasticity", commands.RequestedPowerId);
        Assert.False(commands.IsOpen);
        Assert.Equal(before, ctx.Session.Sheet.SelectedPowers.Count);
    }

    /// <summary>
    /// The palette offers the roster, and choosing it is a plain navigation — the same shape as
    /// choosing a step, and unlike a Power or a gear row it changes nothing about the character.
    ///
    /// <para><b>It does not appear on an empty box</b>, for the reason <see cref="Commands.Matching"/>
    /// gives for the Powers: an empty box is "where do I want to go" and the seven steps already
    /// answer that. Typing something is what asks for it, same as the Powers and Chapter 6's
    /// rows.</para>
    /// </summary>
    [Fact]
    public async Task ThePaletteOffersTheRoster()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "roster" }),
            "the word that finds the roster");

        var row = page.FindAll(".palette-row.kind-roster");
        Assert.Single(row);
        Assert.Equal("Your characters", row[0].QuerySelector(".palette-label")!.TextContent);

        await Occupying(
            page,
            () => page.Find(".palette-box").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }),
            "the Enter that chooses it");

        Assert.False(commands.IsOpen);
        Assert.Equal("characters", ctx.Services.GetRequiredService<NavigationManager>().ToBaseRelativePath(
            ctx.Services.GetRequiredService<NavigationManager>().Uri));
    }

    /// <summary>
    /// The Resolve and Adversity reference is reached from the palette, not from the step band the
    /// shell goldens draw: typing "adversity" offers it, and Enter goes there.
    /// </summary>
    [Fact]
    public async Task ThePaletteOffersTheResolveReference()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "adversity" }),
            "the word that finds the reference");

        var row = page.FindAll(".palette-row.kind-roster");
        Assert.Single(row);
        Assert.Equal("Resolve and Adversity reference", row[0].QuerySelector(".palette-label")!.TextContent);

        await Occupying(
            page,
            () => page.Find(".palette-box").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }),
            "the Enter that chooses it");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal("reference/resolve", nav.ToBaseRelativePath(nav.Uri));
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
    /// The palette and the step band offer the same steps.
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

    // ── Chapter 6's gear catalogue ───────────────────────────────────────────

    /// <summary>
    /// <b>Typing finds a weapon row, under a heading of its own.</b>
    ///
    /// <para>The rows are Chapter 6's own — armour, weapons, p.91's items — and they are matched
    /// in the browser out of the rules the app fetched at boot. Nothing goes over the network for
    /// them, which is the difference between this group and the book's passages.</para>
    /// </summary>
    [Fact]
    public async Task TypingFindsAGearRowUnderItsOwnHeading()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "battle axe" }),
            "the words typed into the box");

        var labels = page.FindAll(".palette-row .palette-label").Select(e => e.TextContent).ToList();

        Assert.Contains("Battle Axe", labels);

        // Under its own heading, and the heading is above the row rather than merely present.
        var headings = page.FindAll(".palette-group").Select(e => e.TextContent).ToList();
        Assert.Contains("Gear from the book", headings);

        Assert.NotEmpty(page.FindAll(".palette-group ~ .palette-row"));

        // And the detail line carries what the page prints beside the name, so a reader can tell
        // two weapons apart without choosing one.
        var detail = page.FindAll(".palette-row")
            .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Battle Axe")
            .QuerySelector(".palette-detail")!.TextContent;

        Assert.Contains("+3", detail, StringComparison.Ordinal);
        Assert.Contains("Two-Handed", detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A printed feature finds the row, and it is not printed on the row that it found.</b>
    /// The same promise a Power's tags make: somebody hunting for a two-handed weapon should not
    /// have to already know which ones are.
    /// </summary>
    [Fact]
    public async Task APrintedFeatureFindsTheRowsThatCarryIt()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "bulky" }),
            "the word typed into the box");

        var rows = page.FindAll(".palette-row .palette-label").Select(e => e.TextContent).ToList();

        // Mail, Tactical Gear and Medium are the three armour rows the book marks Bulky.
        Assert.Contains("Mail", rows);
        Assert.Contains("Tactical Gear", rows);

        // Control: the word is not in any of their names, so the match came from the feature.
        Assert.All(rows, r => Assert.DoesNotContain("Bulky", r, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <b>An empty box still offers the steps alone.</b> 108 gear rows under seven steps would bury
    /// the thing the palette is mostly for, exactly as 141 Powers would — so the catalogue waits
    /// for something to be typed, on the same rule.
    /// </summary>
    [Fact]
    public void AnEmptyBoxOffersNoGearRowsEither()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        Assert.Empty(page.FindAll(".palette-group"));
        Assert.Equal(Commands.Steps.Count, page.FindAll(".palette-row").Count);
    }

    /// <summary>
    /// <b>Choosing a gear row goes to the Gear step and changes nothing about the character.</b>
    ///
    /// <para>This is the rule that is easiest to lose here rather than on a Power: mundane gear is
    /// free, so a palette that simply added the axe would look harmless and would still be a second
    /// place a character is edited. The request narrows the step's catalogue and the choosing stays
    /// where the choosing is.</para>
    /// </summary>
    [Fact]
    public async Task ChoosingAGearRowGoesToTheGearStepAndAddsNothing()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        var carried = ctx.Session.Sheet.Gear.Count;

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "battle axe" }),
            "the words typed into the box");

        await Occupying(
            page,
            () => page.FindAll(".palette-row")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Battle Axe")
                .ClickAsync(new MouseEventArgs()),
            "the row chosen");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        Assert.EndsWith("build/gear", nav.Uri, StringComparison.Ordinal);
        Assert.False(CommandsOf(ctx).IsOpen);

        // Nothing added, and nothing taken away either — the character is exactly as it was.
        Assert.Equal(carried, ctx.Session.Sheet.Gear.Count);
        Assert.DoesNotContain(ctx.Session.Sheet.Gear, g => g.Name == "Battle Axe");

        // What did happen is a request, which the Gear step reads. The test below is the other
        // half; this says the click produced one rather than nothing at all.
        Assert.Equal(GearCatalogue.WeaponPrefix + "battle_axe", CommandsOf(ctx).RequestedGearRowId);
    }

    /// <summary>
    /// <b>The gear rows are capped, and the cap is counted per kind rather than over the list.</b>
    ///
    /// <para>That is the claim <c>PowerLimit</c>'s own doc comment makes — "counted per kind
    /// rather than over the whole list, so a word that matches eight Powers does not push every
    /// weapon off the bottom" — and nothing held it: a shared counter left every assertion in this
    /// file green while a query matching the cap in Powers offered <em>no</em> gear at all. So the
    /// two halves are asserted together, at a limit small enough that both kinds reach it.</para>
    /// </summary>
    [Fact]
    public void TheGearRowsAreCappedAndTheCapIsNotSharedWithThePowers()
    {
        using var ctx = Opened();
        var commands = CommandsOf(ctx);

        // "a" reaches most of the Powers and most of the catalogue, so both kinds hit the cap.
        var found = commands.Matching("a", 3);

        Assert.Equal(3, found.Count(c => c.Kind == CommandKind.Power));
        Assert.Equal(3, found.Count(c => c.Kind == CommandKind.GearRow));

        // The positive control: the cap is a cap and not the number of rows there are, so a query
        // matching fewer than the limit gets all of them and the assertion above is a truncation.
        var few = commands.Matching("battle axe", 3);

        Assert.Contains(few, c => c.Kind == CommandKind.GearRow);
        Assert.True(few.Count(c => c.Kind == CommandKind.GearRow) < 3);
    }

    /// <summary>
    /// <b>And the Gear step it lands on has the row in front of the reader, with the word in the
    /// box.</b> A list narrowed by something the box does not show is a list that looks broken and
    /// cannot be widened again.
    /// </summary>
    [Fact]
    public void TheGearStepArrivesFilteredToTheRequestedRow()
    {
        using var ctx = Opened();

        CommandsOf(ctx).RequestGearRow(GearCatalogue.WeaponPrefix + "battle_axe");

        var step = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.Gear>();

        Assert.Equal("Battle Axe", step.Find(".catalogue .options-filter input").GetAttribute("value"));

        var row = Assert.Single(step.FindAll(".catalogue .options .option"));
        Assert.Contains("Battle Axe", row.TextContent, StringComparison.Ordinal);

        // Read once: rendering the step again is the ordinary consequence of a keystroke anywhere
        // on it, and a request left set would drag the list back here every time.
        Assert.Null(CommandsOf(ctx).RequestedGearRowId);
    }

    // ── Chapter 6's vehicles, headquarters and bases ─────────────────────────

    /// <summary>
    /// <b>Typing finds a vehicle feature and a base feature, under a heading of their own.</b>
    ///
    /// <para>Their prices are in Vehicle Points and Base Points, and each row says which — a bare
    /// number in a list that also holds Hero Point prices is the one mistake this whole area is
    /// careful about.</para>
    /// </summary>
    [Fact]
    public async Task TypingFindsAChapterSixAssetRowWithItsOwnCurrency()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "training" }),
            "the word typed into the box");

        var row = page.FindAll(".palette-row.kind-asset")
            .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Training Facilities");

        Assert.Contains("2 Base Points", row.QuerySelector(".palette-detail")!.TextContent,
                        StringComparison.Ordinal);

        // Singular where the figure is one, which the row beside it is: "1 Base Points" reads as a
        // form field rather than a sentence, and the rule is the one every message here follows.
        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "tesseract" }),
            "a feature priced at more than one");

        Assert.Contains("2 Base Points",
            page.FindAll(".palette-row.kind-asset")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Tesseract")
                .QuerySelector(".palette-detail")!.TextContent, StringComparison.Ordinal);

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "disguised" }),
            "a feature priced at one");

        Assert.Contains("1 Base Point ",
            page.FindAll(".palette-row.kind-asset")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Disguised")
                .QuerySelector(".palette-detail")!.TextContent + " ", StringComparison.Ordinal);

        Assert.Contains("Vehicles and bases from the book",
                        page.FindAll(".palette-group").Select(e => e.TextContent));

        // The control on the currency: a vehicle feature says Vehicle Points, so the label above
        // is about which table the row came off rather than a word printed on every one of them.
        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "spaceflight" }),
            "the second word typed into the box");

        Assert.Contains("Vehicle Points",
            page.FindAll(".palette-row.kind-asset")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Spaceflight")
                .QuerySelector(".palette-detail")!.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Choosing one goes to the Vehicles &amp; bases step and changes nothing.</b> The rule
    /// that is easiest to doubt here rather than on a gear row: a stock vehicle is a whole machine
    /// with four ranks on it, and the palette still only shows it.
    /// </summary>
    [Fact]
    public async Task ChoosingAnAssetRowGoesToTheStepAndAddsNothing()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "helicopter" }),
            "the word typed into the box");

        await Occupying(
            page,
            () => page.FindAll(".palette-row")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Helicopter")
                .ClickAsync(new MouseEventArgs()),
            "the row chosen");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        Assert.EndsWith("build/assets", nav.Uri, StringComparison.Ordinal);
        Assert.False(CommandsOf(ctx).IsOpen);

        // Nothing owned, nothing spent: the step is where a machine is built.
        Assert.Empty(ctx.Session.Sheet.Vehicles);
        Assert.Equal(AssetCatalogue.StockPrefix + "helicopter", CommandsOf(ctx).RequestedAssetRowId);
    }

    /// <summary>
    /// <b>An empty box offers no asset rows either</b>, on the same rule the Powers and the gear
    /// rows follow: fifty-one more rows under the steps would bury what the palette is mostly for.
    /// </summary>
    [Fact]
    public void AnEmptyBoxOffersNoAssetRows()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        Assert.Empty(page.FindAll(".palette-row.kind-asset"));
        Assert.Equal(Commands.Steps.Count, page.FindAll(".palette-row").Count);
    }

    /// <summary>
    /// <b>The cap is counted per kind, and a mutation proved nothing was checking that.</b>
    ///
    /// <para>A single running total over the whole list is the obvious way to write this and the
    /// wrong one: eight Powers would fill it and every gear row and every Chapter 6 row would fall
    /// off the bottom, which is precisely what the palette looked like before the gear slice split
    /// the count. Changing the asset rows' cap to a whole-list count left every other test in this
    /// file green.</para>
    ///
    /// <para>A one-letter query is deliberate — it is the widest net there is, so all three kinds
    /// are over the limit and each one's count is the cap rather than a coincidence.</para>
    /// </summary>
    [Fact]
    public void EachKindOfRowIsCappedOnItsOwnCount()
    {
        using var ctx = Opened();

        const int limit = 8;
        var found = CommandsOf(ctx).Matching("s", limit);

        Assert.Equal(limit, found.Count(c => c.Kind == CommandKind.Power));
        Assert.Equal(limit, found.Count(c => c.Kind == CommandKind.GearRow));
        Assert.Equal(limit, found.Count(c => c.Kind == CommandKind.AssetRow));

        // The control on the query: there really are more of each than the cap lets through, so
        // the three equalities above are a cap and not a count of what happened to match.
        Assert.True(ctx.Session.Rules.Powers.Count > limit);
        Assert.True(ctx.Session.Catalogue.Rows.Count > limit);
        Assert.True(ctx.Session.Assets.Rows.Count > limit);
    }

    /// <summary>
    /// <b>And a reader who was already on the Gear step gets the same answer</b> — which is the
    /// likeliest reader of all, since the palette is how you look a weapon up while choosing gear.
    ///
    /// <para><b>The page is rendered before the row is asked for, and it is the same instance every
    /// assertion is about.</b> That is the whole drive: <c>NavigateTo("build/gear")</c> from
    /// <c>build/gear</c> is a no-op, Blazor reuses the instance rather than initialising a second
    /// one, and a request read in <c>OnInitialized</c> alone is therefore never read at all. This
    /// repository has shipped that exact defect once, on <c>/rules</c>, and the test that hid it
    /// rendered a fresh page afterwards — which is a thing the app never does and a test always
    /// did. See <c>PaletteBookTests.ChoosingAPassageAsksTheRulesReferenceTheSameQuestion</c>.</para>
    /// </summary>
    [Fact]
    public async Task TheGearStepAlreadyOpenIsFilteredToTheRequestedRowToo()
    {
        using var ctx = Opened();

        // On the Gear step already, with the palette over it. Nothing renders a second one.
        var step = ctx.Render<ProwlersAndParagonsAutomation.Web.Pages.Gear>();

        // The positive control: with no request made, the box is empty and the whole catalogue is
        // on offer — so "the list is short" below is a filter and not the page's resting state.
        Assert.Equal("", step.Find(".catalogue .options-filter input").GetAttribute("value"));
        Assert.True(step.FindAll(".catalogue .options .option").Count > 1);

        // On the renderer's own thread, because choosing a row raises `Changed` and the page
        // answers it with `StateHasChanged`.
        await step.InvokeAsync(
            () => CommandsOf(ctx).RequestGearRow(GearCatalogue.WeaponPrefix + "battle_axe"));

        Assert.Equal("Battle Axe", step.Find(".catalogue .options-filter input").GetAttribute("value"));

        var row = Assert.Single(step.FindAll(".catalogue .options .option"));
        Assert.Contains("Battle Axe", row.TextContent, StringComparison.Ordinal);

        Assert.Null(CommandsOf(ctx).RequestedGearRowId);
    }
}
