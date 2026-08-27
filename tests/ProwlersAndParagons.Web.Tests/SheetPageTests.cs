using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// One character read as a document, at <c>/sheet</c> and <c>/sheet/{id}</c>.
///
/// <para><b>The page differs from the review step by what it omits, and that is the property
/// these hold.</b> <c>/build/sheet</c> existed once and was retired because it was the review step
/// with <c>Explain</c> flipped — a second address for the page you were already on. If a panel
/// about <em>building</em> the character ever appears above this sheet, the page has become the
/// review step again, and <see cref="TheDocumentIsAloneOnThePage"/> is what says so.</para>
///
/// <para><b>The second address is the one that earns the page, and it is the one that can lose
/// somebody's work.</b> <c>/sheet/{id}</c> reads a saved character without opening it. The
/// distinction is not cosmetic: <c>OpenAsync</c> moves the current-character pointer and
/// overwrites this browser's anonymous slot, so a page that used it would switch the app to
/// whichever old character somebody glanced at — and the next autosave would write the sheet on
/// screen over it. That is the shape of the defect this project has already shipped once.
/// <see cref="ShowingIsNotOpening"/> is the guard, and it was broken and watched to fail.</para>
/// </summary>
public sealed class SheetPageTests
{
    private static AccountCharacterStore StoreIn(RenderContext ctx) =>
        ctx.Services.GetRequiredService<AccountCharacterStore>();

    private static SavedCharacters SavedIn(RenderContext ctx) =>
        ctx.Services.GetRequiredService<SavedCharacters>();

    /// <summary>
    /// A character with a name and a tier — enough that the engine can price it and that the sheet
    /// has something on it to read.
    /// </summary>
    private static CharacterSheet Character(string name)
    {
        var sheet = new CharacterSheet { Name = name, SelectedTierId = "standard" };
        return sheet;
    }

    /// <summary>
    /// Put a character in the store under its own id, without opening it and without touching the
    /// current-character pointer — which is exactly what <c>RestoreAsync</c> is for.
    /// </summary>
    private static async Task<string> Seed(RenderContext ctx, string id, string name)
    {
        Assert.True(await StoreIn(ctx).RestoreAsync(id, name, Character(name), SheetMode.Hero),
            "the fixture's own write has to have landed, or every assertion below holds vacuously");

        return id;
    }

    /// <summary>Put a character on screen, the way the builder would have.</summary>
    private static void Open(RenderContext ctx, string name)
    {
        ctx.Session.Sheet.Name = name;
        ctx.Session.Sheet.SelectedTierId = "standard";
    }

    // ── The open character ───────────────────────────────────────────────────────────

    /// <summary>
    /// <c>/sheet</c> with no id is the character being built.
    /// </summary>
    [Fact]
    public void TheBareAddressIsTheOpenCharacter()
    {
        using var ctx = new RenderContext();
        Open(ctx, "Lynchpin");

        var page = ctx.Render<SheetPage>();

        Assert.Contains("Lynchpin", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing about building the character is drawn above it: no findings panel, no "Take it
    /// away", no step buttons.
    ///
    /// <para><b>This is the guard against the page becoming the review step again</b>, which is
    /// the whole reason <c>/build/sheet</c> was deleted. It asserts absences, so it carries a
    /// positive control: the sheet itself has to be there, or every absence below is satisfied by
    /// a page that rendered nothing at all.</para>
    /// </summary>
    [Fact]
    public void TheDocumentIsAloneOnThePage()
    {
        using var ctx = new RenderContext();
        Open(ctx, "Lynchpin");

        var page = ctx.Render<SheetPage>();

        // Positive control first, deliberately: three of this repository's four historical guard
        // faults were a feature that never ran being mistaken for one that worked.
        Assert.Single(page.FindAll(".sheet"));

        Assert.Empty(page.FindAll(".panel"));
        Assert.Empty(page.FindAll(".nav-buttons"));
        Assert.DoesNotContain("Checks against the rules", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The builder's two bands are not drawn over it — asserted through the layout, because a page
    /// cannot see the shell above it.
    ///
    /// <para>The mechanism is <see cref="Areas.Of"/> answering <see cref="Area.Sheet"/> rather than
    /// anything on the page, which is the point: a page that had to remember to drop the chrome
    /// would eventually forget.</para>
    /// </summary>
    [Theory]
    [InlineData("sheet")]
    [InlineData("sheet/c_AAAAAAAAAAAAAAAAAAAAAA")]
    public void TheBuildersBandsAreNotDrawnOverADocument(string path)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var shell = ctx.Render<MainLayout>();

        Assert.Empty(shell.FindAll(".steps"));
        Assert.Empty(shell.FindAll(".budget"));

        // The switcher is the builder's for the same reason, and it matters more here: on
        // /sheet/{id} the sheet may be somebody else's, and a banner naming this visitor's own
        // character over it is the fault the budget strip was pulled off three areas to fix.
        Assert.Empty(shell.FindAll(".character-switch"));
    }

    /// <summary>
    /// The banner does not name Hero or Villain here.
    ///
    /// <para>Same rule the rules reference is held to, and for a sharper reason: the sheet on
    /// screen may not be this visitor's character at all.</para>
    /// </summary>
    [Fact]
    public void TheBannerDoesNotNameThisVisitorsIdentity()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("sheet");

        Assert.DoesNotContain("Villain",
            ctx.Render<MainLayout>().Find(".banner-title").TextContent, StringComparison.Ordinal);
    }

    // ── A saved character, shown rather than opened ──────────────────────────────────

    /// <summary>
    /// <c>/sheet/{id}</c> draws the character stored at that id, not the one on screen.
    /// </summary>
    [Fact]
    public async Task ANamedAddressDrawsThatCharacter()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        Open(ctx, "Lynchpin");
        var id = await Seed(ctx, "c_AAAAAAAAAAAAAAAAAAAAAA", "Vandergraff");

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, id));

        var shown = SheetText.Visible(page.Find(".sheet"));
        Assert.Contains("Vandergraff", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("Lynchpin", shown, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Showing is not opening, and this is the guard that says so.</b>
    ///
    /// <para>Looking at a saved character must leave the app exactly where it was: the same
    /// character in the session, and the same current-character pointer. <c>ReadAsync</c> has no
    /// side effect; <c>OpenAsync</c> beside it moves the pointer and overwrites the anonymous
    /// slot, and reaching for it here would mean a glance at an old character quietly switched the
    /// app to it — with the next autosave writing the sheet on screen over whatever was actually
    /// open.</para>
    ///
    /// <para><b>Broken and watched to fail:</b> swapping <c>ReadAsync</c> for <c>OpenAsync</c> in
    /// <c>SheetPage</c> turns this red on both assertions. That is a mutation that changes the
    /// answer rather than one the page is indifferent to.</para>
    /// </summary>
    [Fact]
    public async Task ShowingIsNotOpening()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        Open(ctx, "Lynchpin");
        var id = await Seed(ctx, "c_BBBBBBBBBBBBBBBBBBBBBB", "Vandergraff");

        var before = await SavedIn(ctx).CurrentIdAsync();

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, id));

        // The positive control: the other character really was drawn, so the two assertions below
        // are about a page that did something rather than one that failed to read anything.
        Assert.Contains("Vandergraff", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Equal(before, await SavedIn(ctx).CurrentIdAsync());
    }

    /// <summary>
    /// An id nobody can read says so, rather than falling back to the visitor's own sheet.
    ///
    /// <para><b>The distinction this protects is between two nulls.</b> "No character was read"
    /// and "no id was asked for" both arrive as a null character, and collapsing them renders the
    /// reader's own sheet at somebody else's dead link — which reads as their character having
    /// been renamed.</para>
    /// </summary>
    [Fact]
    public async Task AnAddressWithNothingBehindItSaysSo()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        Open(ctx, "Lynchpin");

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, "c_CCCCCCCCCCCCCCCCCCCCCC"));

        Assert.Empty(page.FindAll(".sheet"));
        Assert.DoesNotContain("Lynchpin", page.Markup, StringComparison.Ordinal);
        Assert.Single(page.FindAll(".empty-state"));
    }

    /// <summary>
    /// Walking from one saved character to another redraws.
    ///
    /// <para><b>Blazor reuses the component when only the route parameter changes</b>, so a
    /// one-shot "have I read yet" flag would leave the first character on screen under the
    /// second's address. Keyed on the id instead — and this is what holds that, because nothing
    /// else would notice.</para>
    /// </summary>
    [Fact]
    public async Task ChangingWhichCharacterIsNamedRedraws()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var first = await Seed(ctx, "c_DDDDDDDDDDDDDDDDDDDDDD", "Vandergraff");
        var second = await Seed(ctx, "c_EEEEEEEEEEEEEEEEEEEEEE", "Halfmask");

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, first));
        Assert.Contains("Vandergraff", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);

        page.Render(p => p.Add(c => c.Id, second));

        var shown = SheetText.Visible(page.Find(".sheet"));
        Assert.Contains("Halfmask", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("Vandergraff", shown, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two controls under the sheet never reach paper.
    ///
    /// <para>The print block already hides <c>.no-print</c>, so this needed no new stylesheet rule
    /// — but nothing would have said if the class were left off, and a printed sheet with a
    /// "Print" button on it is the deliverable spoiled.</para>
    /// </summary>
    [Fact]
    public void TheControlsAreMarkedOffPaper()
    {
        using var ctx = new RenderContext();
        Open(ctx, "Lynchpin");

        var page = ctx.Render<SheetPage>();

        Assert.Contains(page.FindAll(".no-print"),
            e => e.TextContent.Contains("Print", StringComparison.Ordinal));
    }
}
