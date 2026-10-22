using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using static ProwlersAndParagons.Web.Tests.BusyRenderer;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A Pro or Con on the sheet explains itself, and the palette names a rules term with its kind.
///
/// <para><b>The owner's report of 2026-09-30, in one sentence:</b> Immortality's own
/// <i>Vulnerable</i> sat on a sheet with no tooltip, so he looked the word up, typed
/// <i>Vulnerability</i>, and was handed the Flaw — a different rule under a near-identical name.
/// Two things close it. On the sheet every Pro and Con is a <see cref="Term"/>, and a Power's own
/// option opens its sentence with whose it is. In the palette every generic Pro, Con, Perk and
/// Flaw and every Power's own option is a row that says its kind before its description, so the
/// two <i>Vulnerab…</i> rows sit beside each other labelled Con and Flaw.</para>
/// </summary>
public sealed class RulesTermTests
{
    private static RenderContext Opened()
    {
        var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<Commands>().Open();
        return ctx;
    }

    private static Commands CommandsOf(RenderContext ctx) =>
        ctx.Services.GetRequiredService<Commands>();

    // ── The sheet ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A Power's own Con on the sheet is a term whose sentence names the Power.</b>
    ///
    /// <para>The positive control is that the printed line is unchanged — <c>Cons: Vulnerable</c>
    /// as the text export writes it — and then that the word is a button with a description that
    /// opens "Immortality's own Con." A generic Con on the same sheet is a term too, with its own
    /// entry's description and no owner, which is the other half: the sentence says whose only
    /// where there is a whose.</para>
    /// </summary>
    [Fact]
    public void APowersOwnConExplainsWhoseItIsAndAGenericOneExplainsItself()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("immortality", 0, [],
            [new SelectedProCon("vulnerable"), new SelectedProCon("unreliable")]));

        var page = ctx.Render<SheetView>();

        var line = page.FindAll(".power-entry .statline")
            .Single(e => SheetText.Visible(e).StartsWith("Cons:", StringComparison.Ordinal));

        // `SheetText.Visible` breaks a line where a browser would, and a term's hidden copies
        // are block-shaped to it, so the join is read with its whitespace collapsed: what is
        // asserted is the words and their order, which is what the text export prints.
        Assert.Equal("Cons: Vulnerable, Unreliable",
            System.Text.RegularExpressions.Regex.Replace(SheetText.Visible(line), @"\s*,\s*", ", ").Trim());

        var terms = line.QuerySelectorAll(".term").ToList();
        Assert.Equal(2, terms.Count);

        var own = terms[0];
        Assert.Equal("Vulnerable", own.QuerySelector(".term-name")!.TextContent);
        var ownText = own.QuerySelector(".sr-only")!.TextContent;
        Assert.StartsWith("Immortality's own Con.", ownText, StringComparison.Ordinal);
        Assert.Contains("killed", ownText, StringComparison.Ordinal);

        // Keyed on the Power as well as the option, so a generic option spelled the same could
        // never be described by this sentence.
        Assert.Equal("term-immortality-vulnerable", own.QuerySelector(".sr-only")!.Id);
        Assert.Equal("term-immortality-vulnerable", own.QuerySelector(".term-name")!.GetAttribute("aria-describedby"));

        var generic = terms[1];
        Assert.Equal("Unreliable", generic.QuerySelector(".term-name")!.TextContent);
        var genericText = generic.QuerySelector(".sr-only")!.TextContent;
        Assert.DoesNotContain("own", genericText, StringComparison.Ordinal);
        Assert.Equal(ctx.Session.Rules.GetCon("unreliable")!.Description, genericText);
        Assert.Equal("term-unreliable", generic.QuerySelector(".sr-only")!.Id);
    }

    /// <summary>A repeated option still collapses to <c>×N</c>, as one term.</summary>
    [Fact]
    public void ARepeatedOptionIsOneTermWithItsCount()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("energy_absorption", 4,
            [new SelectedProCon("also_x"), new SelectedProCon("also_x"), new SelectedProCon("also_x")], []));

        var line = ctx.Render<SheetView>().FindAll(".power-entry .statline")
            .Single(e => SheetText.Visible(e).StartsWith("Pros:", StringComparison.Ordinal));

        var term = Assert.Single(line.QuerySelectorAll(".term"));
        Assert.Equal("Also X ×3", term.QuerySelector(".term-name")!.TextContent);
    }

    /// <summary>With explanations off the line is the bare words, exactly as the text export.</summary>
    [Fact]
    public void WithExplanationsOffTheLineIsBareWords()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("immortality", 0, [],
            [new SelectedProCon("vulnerable")]));

        var page = ctx.Render<SheetView>(p => p.Add(s => s.Explain, false));
        var line = page.FindAll(".power-entry .statline")
            .Single(e => e.TextContent.StartsWith("Cons:", StringComparison.Ordinal));

        Assert.Empty(line.QuerySelectorAll(".term"));
        Assert.Equal("Cons: Vulnerable", line.TextContent.Trim());
    }

    // ── The palette ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Typing the confusing word lists both rules, each saying which kind it is.</b> The Con
    /// says whose it is; the Flaw says it is a Flaw. Under one heading, so a reader who found the
    /// wrong one sees the right one beside it.
    /// </summary>
    [Fact]
    public async Task TypingVulnerableListsTheConAndTheFlawEachNamedByKind()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "vulnerab" }),
            "the words typed into the box");

        var rows = page.FindAll(".palette-row.kind-term").Select(r => (
            Label: r.QuerySelector(".palette-label")!.TextContent,
            Detail: r.QuerySelector(".palette-detail")!.TextContent)).ToList();

        Assert.Contains(rows, r => r.Label == "Vulnerable"
            && r.Detail.StartsWith("Immortality's own Con · ", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Label == "Vulnerability"
            && r.Detail.StartsWith("Flaw · ", StringComparison.Ordinal));

        Assert.Contains("Rules terms", page.FindAll(".palette-group").Select(e => e.TextContent));
    }

    /// <summary>
    /// A Power's own option, chosen, opens that Power in its editor — the same request choosing
    /// the Power makes — and adds nothing.
    /// </summary>
    [Fact]
    public async Task ChoosingAPowersOwnConOpensThatPowerAndAddsNothing()
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();
        var carried = ctx.Session.Sheet.SelectedPowers.Count;

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "vulnerable" }),
            "the words typed into the box");

        // The row is picked by its owner's name in the detail, which is the thing the row exists
        // to say: a reader who types the bare word is shown whose it is before choosing.
        await Occupying(
            page,
            () => page.FindAll(".palette-row.kind-term")
                .Single(r => r.QuerySelector(".palette-label")!.TextContent == "Vulnerable"
                    && r.QuerySelector(".palette-detail")!.TextContent.StartsWith("Immortality's own Con", StringComparison.Ordinal))
                .ClickAsync(new MouseEventArgs()),
            "the row chosen");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        Assert.EndsWith("build/characteristics", nav.Uri, StringComparison.Ordinal);
        Assert.False(CommandsOf(ctx).IsOpen);
        Assert.Equal("immortality", CommandsOf(ctx).RequestedPowerId);
        Assert.Equal(carried, ctx.Session.Sheet.SelectedPowers.Count);
    }

    /// <summary>A Flaw, chosen, opens the Flaws section; a generic Con, the Powers section.</summary>
    [Theory]
    [InlineData("Vulnerability", "flaws")]
    [InlineData("Unreliable", "powers")]
    [InlineData("Contacts", "perks")]
    public async Task ChoosingAGenericTermOpensTheSectionItIsBoughtOn(string label, string section)
    {
        using var ctx = Opened();

        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = label.ToLowerInvariant() }),
            "the words typed into the box");

        await Occupying(
            page,
            () => page.FindAll(".palette-row.kind-term")
                .First(r => r.QuerySelector(".palette-label")!.TextContent == label)
                .ClickAsync(new MouseEventArgs()),
            "the row chosen");

        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        Assert.EndsWith("build/characteristics", nav.Uri, StringComparison.Ordinal);
        Assert.False(CommandsOf(ctx).IsOpen);
        Assert.Null(CommandsOf(ctx).RequestedPowerId);
        Assert.Equal(section, CommandsOf(ctx).RequestedSection);
    }

    /// <summary>
    /// The terms are capped per kind like the Powers, and an empty box offers none — the two
    /// properties every other in-browser group holds.
    /// </summary>
    [Fact]
    public void TheTermsAreCappedAndAnEmptyBoxOffersNone()
    {
        using var ctx = Opened();

        var commands = CommandsOf(ctx);

        Assert.DoesNotContain(commands.Matching("", 8), c => c.Kind == CommandKind.Term);

        // "the" matches a great many descriptions; the cap holds it to the limit.
        var found = commands.Matching("the", 5);
        Assert.Equal(5, found.Count(c => c.Kind == CommandKind.Term));
    }
}
