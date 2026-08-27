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

        // **The positive control, and it is what ties this to Area.Sheet rather than to any area
        // that happens to draw no bands.** A review pointed out that deleting the `sheet` arm from
        // `Areas.Of` altogether would leave the three absences below green, because the front door
        // draws none of these either — so the test named a mechanism it did not assert. The
        // subtitle is the one thing on the band that only this area produces.
        Assert.Contains("Character sheet", shell.Find(".banner-title").TextContent, StringComparison.Ordinal);

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
    /// <para><b>Both halves, because the negative one alone was satisfied by the wrong answer.</b>
    /// A review deleted the subtitle's whole <c>Area.Sheet</c> arm and both suites stayed green:
    /// the front door's fallback contains no "Villain" either, so the absence held while the
    /// banner said something else entirely. `AreaTests.OnlyTheBuilderNamesThePalette` pairs a
    /// positive with its negative for exactly this reason and this copy of it had kept only the
    /// negative.</para>
    [Fact]
    public void TheBannerDoesNotNameThisVisitorsIdentity()
    {
        using var ctx = new RenderContext().With(SheetMode.Villain);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("sheet");

        var title = ctx.Render<MainLayout>().Find(".banner-title").TextContent;

        Assert.Contains("Character sheet", title, StringComparison.Ordinal);
        Assert.DoesNotContain("Villain", title, StringComparison.Ordinal);
    }

    /// <summary>
    /// The identity switch is the builder's, and is not drawn over a document.
    ///
    /// <para><b>The sharpest of the four findings an adversarial review returned.</b> Those buttons
    /// always act on the character that is <em>open</em>, but <c>/sheet/{id}</c> draws one that is
    /// not it — so pressing "Villain" there recoloured the foreign sheet on screen while silently
    /// flipping and saving <c>IsVillain</c> on somebody else's character. The reviewer's own
    /// phrasing is the fair one: this page's subtitle is deliberately not "Hero" or "Villain"
    /// because the sheet may be somebody else's, and then the two controls acting on exactly that
    /// conflation were left in the band above it.</para>
    /// </summary>
    [Theory]
    [InlineData("sheet", false)]
    [InlineData("sheet/c_AAAAAAAAAAAAAAAAAAAAAA", false)]
    [InlineData("build", true)]
    public void TheIdentitySwitchBelongsToTheBuilderAlone(string path, bool expected)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        Assert.Equal(expected, ctx.Render<MainLayout>().FindAll(".mode-switch").Count > 0);
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
    /// <c>SheetPage</c> turns this red. <b>On the pointer assertion alone</b> — this docstring
    /// claimed "both" and an adversarial review checked it: <c>OpenAsync</c> never touches
    /// <c>Session.Sheet</c>, so the session assertion survives that mutation. It is kept because
    /// it is a real invariant, not because it is what catches this; the pointer is what catches
    /// this. <b>A claim about which assertion fires is worth as little as any other untested
    /// claim.</b></para>
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
    /// <b>A read that loses a race does not write its answer.</b>
    ///
    /// <para><b>Found by an adversarial review, which built a throwaway probe to demonstrate
    /// it.</b> The address was claimed before the read finished, so a second navigation arriving
    /// while the first was in flight let the losing read overwrite the winner's answer — and
    /// because the key already held the new id, nothing would ever re-read to correct it. It stuck
    /// until the tab was reloaded.</para>
    ///
    /// <para><b>Signed in on purpose.</b> Anonymously the read is one synchronous storage call and
    /// the window barely exists; on an account it is an HTTP round trip, which is where this
    /// actually bites — and no other test in this file exercises that branch at all.</para>
    ///
    /// <para>The reviewer demonstrated three shapes and this is the worst of them: <c>/sheet</c>,
    /// which must show the character being built, showing a stored one instead.</para>
    /// </summary>
    [Fact]
    public async Task AReadThatLosesARaceDoesNotWriteItsAnswer()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("acct-7", "player");
        Open(ctx, "Lynchpin");

        const string Stored = "c_FFFFFFFFFFFFFFFFFFFFFF";
        Assert.True(await StoreIn(ctx).RestoreAsync(Stored, "Vandergraff", Character("Vandergraff"), SheetMode.Hero));

        // Hold the stored character's read open, so it is still in flight when the route changes
        // out from under it.
        var held = new TaskCompletionSource();
        ctx.Api.BeforeAnsweringCharacter = _ => held.Task;

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, Stored));

        // Away to the open character while that read is still waiting.
        ctx.Api.BeforeAnsweringCharacter = null;
        page.Render(p => p.Add(c => c.Id, (string?)null));

        // Now let the overtaken read finish. Its answer belongs to an address nobody is on.
        held.SetResult();
        await page.InvokeAsync(() => Task.CompletedTask);

        var shown = SheetText.Visible(page.Find(".sheet"));
        Assert.Contains("Lynchpin", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("Vandergraff", shown, StringComparison.Ordinal);
    }

    /// <summary>
    /// A saved character reads the same way on an account as it does in a browser.
    ///
    /// <para><b>Every other test in this file runs signed out</b>, which a review pointed out
    /// leaves <c>AccountCharacterStore.ReadAsync</c>'s account arm — a real HTTP <c>GET</c> through
    /// <c>ApiCharacterStore</c> — never taken from this page at all. That is the branch the app is
    /// mostly used through.</para>
    /// </summary>
    [Fact]
    public async Task ASavedCharacterReadsTheSameWayOnAnAccount()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("acct-7", "player");
        Open(ctx, "Lynchpin");
        var id = await Seed(ctx, "c_GGGGGGGGGGGGGGGGGGGGGG", "Vandergraff");

        var before = await SavedIn(ctx).CurrentIdAsync();
        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, id));

        Assert.Contains("Vandergraff", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);

        // The account arm really was taken, rather than a local copy answering.
        Assert.Contains(ctx.Api.Asked, a => a.StartsWith("GET /api/characters/" + id, StringComparison.Ordinal));

        // And showing still is not opening on this branch either.
        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Equal(before, await SavedIn(ctx).CurrentIdAsync());
    }

    /// <summary>
    /// A character with no Hero Point limit is drawn without one, whoever is reading it.
    ///
    /// <para>The entry takes a decision about exactly this expression — it is
    /// <c>!UnlimitedBudget</c> read off the sheet being shown, not the visitor's mode — and a
    /// review pointed out that nothing exercised the false case.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterBuiltWithoutALimitIsShownWithoutOne()
    {
        await using var ctx = new RenderContext(storesForReal: true);

        var sandbox = Character("Vandergraff");
        sandbox.UnlimitedBudget = true;
        Assert.True(await StoreIn(ctx).RestoreAsync("c_HHHHHHHHHHHHHHHHHHHHHH", "Vandergraff", sandbox, SheetMode.Hero));

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, "c_HHHHHHHHHHHHHHHHHHHHHH"));

        // Positive control: the character really is on screen, so the absence below is about a
        // sheet that rendered rather than one that did not.
        Assert.Contains("Vandergraff", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);

        Assert.DoesNotContain("of 125", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal);
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
