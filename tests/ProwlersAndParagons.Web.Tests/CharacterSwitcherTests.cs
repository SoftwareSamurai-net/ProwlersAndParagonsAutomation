using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Which character is open, and the way to another one, from every step of the builder.
///
/// <para><b>Swapping used to mean walking back to step one.</b> <c>CharacterManager</c> is a panel
/// on the tier page, so a reader several steps in who wanted their other character had to navigate to
/// the start of a wizard to reach it. The manager keeps everything else it does; this is the one
/// act worth having from everywhere.</para>
/// </summary>
public sealed class CharacterSwitcherTests
{
    /// <param name="path">The address to render at, which decides whether the switcher draws.</param>
    /// <param name="signedIn">
    /// Set <b>before anything else touches the store</b>. `.With(mode)` loads a sample, which asks
    /// who is here and caches the answer — so signing in after it leaves the identity resolved as
    /// anonymous and every account list comes back empty. That cost a debugging cycle: the store
    /// answered `listed=0 current=legacy` while the test read as though it had signed somebody in.
    /// </param>
    private static RenderContext At(string path, bool signedIn = false)
    {
        var ctx = new RenderContext();

        // Signed in, the character comes from the account and `.With(mode)` is skipped: loading a
        // sample writes it through as another saved row, so the list read four characters with one
        // name twice and the test failed for a reason that had nothing to do with the component.
        if (signedIn) ctx.Api.SignedIn = ("acct-7", "player");
        else ctx.With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        return ctx;
    }

    /// <summary>
    /// <b>The builder alone</b>, exactly as the step list and the budget strip are. Naming the
    /// visitor's own character over a rules search or a recording of somebody else's is the fault
    /// those two were pulled off three areas to fix.
    ///
    /// <para>The two halves are asserted together on purpose: "it is absent from the rules
    /// reference" is satisfied completely by a control that renders nowhere at all.</para>
    /// </summary>
    [Theory]
    [InlineData("build", true)]
    [InlineData("build/characteristics", true)]
    [InlineData("build/review", true)]
    [InlineData("rules", false)]
    [InlineData("admin", false)]
    [InlineData("admin/portfolio/replay/the-conductor", false)]
    public void ItBelongsToTheBuilderAlone(string path, bool expected)
    {
        using var ctx = At(path);

        Assert.Equal(expected, ctx.Render<MainLayout>().FindAll(".character-switch").Count > 0);
    }

    [Fact]
    public void ItNamesTheCharacterThatIsOpen()
    {
        using var ctx = At("build/characteristics");
        ctx.Session.Sheet.Name = "Ninth Precinct";

        Assert.Equal("Ninth Precinct",
            ctx.Render<MainLayout>().Find(".character-switch-name").TextContent.Trim());
    }

    /// <summary>
    /// An unnamed character says so rather than leaving the control blank — a control with
    /// nothing in it reads as broken rather than as empty. Same choice the review step's page
    /// title makes.
    /// </summary>
    [Fact]
    public void AnUnnamedCharacterStillSaysSomething()
    {
        using var ctx = At("build/characteristics");
        ctx.Session.Sheet.Name = "   ";

        Assert.Equal("Unnamed character",
            ctx.Render<MainLayout>().Find(".character-switch-name").TextContent.Trim());
    }

    /// <summary>
    /// <b><c>aria-expanded</c> is written as a string, and <c>aria-controls</c> exists only while
    /// its target does.</b> Blazor renders a <c>true</c> bool as <c>aria-expanded=""</c>, which is
    /// invalid ARIA that assistive technology reads as *not* expanded — so the natural spelling
    /// announces the opposite of the state. And naming an element that renders inside an
    /// <c>@if</c> leaves a dangling IDREF whenever it is shut, which is the bug the budget
    /// disclosure shipped once.
    /// </summary>
    [Fact]
    public void TheDisclosureAnnouncesItselfProperlyBothWaysRound()
    {
        using var ctx = At("build/characteristics");
        var shell = ctx.Render<MainLayout>();
        var shut = shell.Find(".character-switch-name");

        Assert.Equal("false", shut.GetAttribute("aria-expanded"));
        Assert.Null(shut.GetAttribute("aria-controls"));
        Assert.Empty(shell.FindAll("#character-switch-list"));

        shut.Click();

        var open = shell.Find(".character-switch-name");

        Assert.Equal("true", open.GetAttribute("aria-expanded"));
        Assert.Equal("character-switch-list", open.GetAttribute("aria-controls"));
        Assert.NotNull(shell.Find("#character-switch-list"));
    }

    /// <summary>
    /// The list offers the others and never the one already open — an entry that reloaded the
    /// character you are looking at is a control that appears to do nothing.
    /// </summary>
    [Fact]
    public async Task ItOffersTheOthersAndNotTheOneAlreadyOpen()
    {
        await using var ctx = At("build/characteristics", signedIn: true);
        var (_, openName, otherName) = await TwoSaved(ctx);

        var shell = ctx.Render<MainLayout>();
        await shell.Find(".character-switch-name").ClickAsync(new());

        // The click handler refreshes the list asynchronously, so the render that shows it lands
        // after the click returns. Waiting for the element is what makes this a test of the
        // component rather than of bUnit dispatch timing.
        await shell.WaitForElementAsync("#character-switch-list li button");

        var offered = shell.FindAll("#character-switch-list li button")
            .Select(b => b.TextContent.Trim()).ToList();

        // The positive control: the list has to have reached storage at all, or every assertion
        // about what it does not contain is satisfied by a list that read nothing.
        Assert.NotEmpty(offered);
        Assert.Contains(otherName, offered);
        Assert.DoesNotContain(openName, offered);


    }

    /// <summary>
    /// Two characters on the account, with the first of them actually <em>opened</em> through the
    /// store rather than only pushed into the session.
    ///
    /// <para><b>Opening it is the part that matters and the part I got wrong first.</b> The list
    /// hides the current row by comparing against <c>CurrentIdAsync</c>, which is a pointer the
    /// store owns — so a character merely restored into the session is not "the open one" as far
    /// as the store is concerned, and nothing gets hidden. The first version of this test also
    /// left <c>.With(mode)</c>'s sample sitting in the list, so it read three rows with one name
    /// twice and failed for a reason that had nothing to do with the component.</para>
    /// </summary>
    private static async Task<(AccountCharacterStore Store, string OpenName, string OtherName)>
        TwoSaved(RenderContext ctx)
    {
        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var store = ctx.Services.GetRequiredService<AccountCharacterStore>();
        var who = await ctx.Services.GetRequiredService<IIdentitySource>().CurrentAsync();

        var hero = SampleCharacters.Hero();
        var villain = SampleCharacters.Villain();
        var heroId = SavedCharacters.NewId();

        await account.SaveAsync(heroId, hero.Name, hero, SheetMode.Hero);
        await account.SaveAsync(SavedCharacters.NewId(), villain.Name, villain, SheetMode.Villain);

        // **Which row is open resolves through `ppStore`, and bUnit answers null to every interop
        // read** — so `CurrentIdAsync` comes back `legacy`, nothing ever matches, and the list
        // offers the character you are already looking at. Planting the pointer is what
        // `CharacterManagerTests` does for the same reason; without it these two tests pass
        // against a component that filters nothing.
        ctx.JSInterop.Setup<string?>("ppStore.load", $"pp.character.v1.{who.Key}.current")
            .SetResult(heroId);

        if (await store.OpenAsync(heroId) is { } opened) ctx.Session.RestoreBeforeFirstRender(opened.Sheet, opened.Mode);

        return (store, hero.Name, villain.Name);
    }

    /// <summary>
    /// <b>Clicking one actually swaps the session.</b> Everything above reads markup; none of it
    /// would notice a list that renders perfectly and is wired to nothing.
    /// </summary>
    [Fact]
    public async Task ChoosingOneSwapsTheCharacterOnScreen()
    {
        await using var ctx = At("build/characteristics", signedIn: true);
        var (_, _, otherName) = await TwoSaved(ctx);

        var shell = ctx.Render<MainLayout>();
        await shell.Find(".character-switch-name").ClickAsync(new());
        await shell.WaitForElementAsync("#character-switch-list li button");
        await shell.FindAll("#character-switch-list li button")[0].ClickAsync(new());

        Assert.Equal(otherName, ctx.Session.Sheet.Name);

        // And it shuts behind itself: a menu left standing over the step you just landed on is
        // the reader having to dismiss something they already finished with.
        Assert.Empty(shell.FindAll("#character-switch-list"));
    }

    /// <summary>
    /// <b>Swapping tells everything drawing the session, not only the control that was
    /// clicked.</b>
    ///
    /// <para><c>ChoosingOneSwapsTheCharacterOnScreen</c> above asserts <c>Session.Sheet</c> moved,
    /// and that is exactly the hole this fills: assigning the field is not telling anybody. The
    /// pill redraws because it is the component that handled the click and Blazor re-renders it
    /// either way — so a swap that notified nothing looked completely correct from the one control
    /// a reader was looking at, while the sheet under it went on drawing the character they had
    /// just navigated away from.</para>
    ///
    /// <para>The sheet is rendered beside the shell rather than inside it: both resolve the same
    /// scoped <see cref="CharacterSession"/> out of one container, which is the relationship under
    /// test. <c>Character</c> is left null, which is what makes <c>SheetView</c> subscribe — the
    /// builder's own review and characteristics steps draw it exactly that way.</para>
    /// </summary>
    [Fact]
    public async Task SwappingRedrawsTheSheetAndNotOnlyThePill()
    {
        await using var ctx = At("build/characteristics", signedIn: true);
        var (_, openName, otherName) = await TwoSaved(ctx);

        var shell = ctx.Render<MainLayout>();
        var sheet = ctx.Render<SheetView>();

        // The control: the sheet is drawing the character that is open, so what changes below is
        // the swap rather than the sheet having been blank all along.
        Assert.Contains(openName, sheet.Markup, StringComparison.Ordinal);

        await shell.Find(".character-switch-name").ClickAsync(new());
        await shell.WaitForElementAsync("#character-switch-list li button");
        await shell.FindAll("#character-switch-list li button")[0].ClickAsync(new());

        // The pill follows, because it is the component that handled the click.
        Assert.Contains(otherName, shell.Find(".character-switch-name").TextContent,
            StringComparison.Ordinal);

        // And so must everything else drawing that character.
        Assert.Contains(otherName, sheet.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(openName, sheet.Markup, StringComparison.Ordinal);
    }
}
