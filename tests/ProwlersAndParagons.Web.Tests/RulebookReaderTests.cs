using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The book's own text where a player is choosing a Power, and who gets to see it.
///
/// <para><b>The gate itself is on the server and is tested there</b> — bundled into the
/// function rather than copied into <c>wwwroot</c>, because a file under <c>wwwroot</c> is a
/// public URL. What is tested here is what the browser does with the answer: shows it, or shows
/// nothing at all.</para>
///
/// <para><b>Nothing at all is the important half.</b> About a fifth of the 141 Powers have no
/// printed entry of their own — Super Senses' sixteen options share one between them — so a row
/// of apologies would be a worse page than silence, and it would appear under Powers that are
/// perfectly fine.</para>
/// </summary>
public sealed class RulebookReaderTests
{
    private static PowerModel Armor(RenderContext ctx) =>
        ctx.Services.GetRequiredService<RulesRepository>().Powers.Single(p => p.Id == "armor");

    private static RenderContext SignedInWithTheBook()
    {
        var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Book["Armor"] = "Self • Half Toughness • 1 Hero Point per rank\n"
            + "Armor represents protection against damage.";

        return ctx;
    }

    [Fact]
    public void SomebodySignedInIsOfferedTheEntryAndCanOpenIt()
    {
        using var ctx = SignedInWithTheBook();

        var armor = Armor(ctx);
        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));

        var toggle = editor.Find(".book-toggle");
        Assert.Contains("what the book says", toggle.TextContent, StringComparison.OrdinalIgnoreCase);

        // Closed to start with, and saying so — not merely looking closed.
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Empty(editor.FindAll(".book-text"));

        toggle.Click();

        Assert.Equal("true", editor.Find(".book-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("protection against damage", editor.Find(".book-text").TextContent,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The page reference is shown, because a quotation nobody can check against their own copy
    /// is worth less than one they can.
    /// </summary>
    [Fact]
    public void TheEntryCarriesThePageItWasPrintedOn()
    {
        using var ctx = SignedInWithTheBook();

        var armor = Armor(ctx);
        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));
        editor.Find(".book-toggle").Click();

        var text = editor.Find(".book-text").TextContent;

        Assert.Contains("printed p.21", text, StringComparison.Ordinal);
        Assert.Contains("Ultimate Edition", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A visitor with no account sees no trace of it — not a locked box, not an invitation.
    ///
    /// <para>The positive control is the test above: the same Power, the same editor, with
    /// somebody signed in, does render the toggle. Without that pair, a component that had
    /// stopped rendering altogether would satisfy this.</para>
    /// </summary>
    [Fact]
    public void AVisitorWithNoAccountSeesNothingAtAll()
    {
        using var ctx = new RenderContext();
        ctx.Api.Book["Armor"] = "Self • Half Toughness • 1 Hero Point per rank";

        var armor = Armor(ctx);
        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));

        Assert.Empty(editor.FindAll(".book-toggle"));
        Assert.Empty(editor.FindAll(".book-text"));

        // And the mechanics above it are untouched: the entry is shown beside them, never
        // instead of them. Asserted on the stat line the engine formats, which is what a player
        // with no account is left with — and which comes from data/rules, not from the book.
        Assert.Contains("Toughness", editor.Find("p.small.muted").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Power the book files under another heading shows nothing, and is not an error.
    /// </summary>
    [Fact]
    public void APowerWithNoPrintedEntryOfItsOwnShowsNothing()
    {
        using var ctx = SignedInWithTheBook();

        var senses = ctx.Services.GetRequiredService<RulesRepository>().Powers
            .First(p => p.Id.StartsWith("super_senses_", StringComparison.Ordinal));

        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, senses));

        Assert.Empty(editor.FindAll(".book-toggle"));
    }

    /// <summary>
    /// A miss is remembered, so a fifth of the Powers do not each cost a round trip per render.
    /// </summary>
    [Fact]
    public async Task AMissIsAskedForOnceAndThenRemembered()
    {
        await using var ctx = SignedInWithTheBook();
        var reader = ctx.Services.GetRequiredService<RulebookReader>();

        Assert.Null(await reader.ForPowerAsync("Nothing Like This"));
        Assert.Null(await reader.ForPowerAsync("Nothing Like This"));
        Assert.Null(await reader.ForPowerAsync("Nothing Like This"));

        Assert.Equal(1, ctx.Api.Asked.Count(a => a.Contains("Nothing", StringComparison.Ordinal)));

        // The positive control: a different name really does go back to the server.
        Assert.NotNull(await reader.ForPowerAsync("Armor"));
        Assert.Equal(1, ctx.Api.Asked.Count(a => a.Contains("Armor", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A site deployed without its server answers this address with the app's own page and a
    /// 200. Believing the status would put an empty entry under every Power.
    /// </summary>
    [Fact]
    public async Task APageOfHtmlIsNotAnEntry()
    {
        await using var ctx = new RenderContext();
        ctx.Api.ServerNotDeployed = true;

        Assert.Null(await ctx.Services.GetRequiredService<RulebookReader>().ForPowerAsync("Armor"));
    }

    /// <summary>
    /// <b>Signing out stops the book being readable in the same tab.</b>
    ///
    /// <para>This was a real leak, found by a reviewer and reproduced before it was fixed. The
    /// cache's own comment claimed it was "per visit and per scope, so signing out and back in
    /// re-asks" — and <b>Blazor WebAssembly has one DI scope for the life of the app</b>, so a
    /// scoped service is a singleton here and signing out is pure SPA state with no reload. A
    /// signed-in visitor on a shared machine could open a Power's entry, sign out, open the same
    /// Power, and be handed the publisher's prose out of the dictionary without the server —
    /// which would have refused — ever being asked.</para>
    ///
    /// <para>The positive control is the first assertion: the entry has to have been cached for
    /// the second to mean anything. A reader that had stopped working at all would otherwise
    /// satisfy this test perfectly.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutStopsTheBookBeingReadableInTheSameTab()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Book["Armor"] = "the publisher's prose";

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        var reader = ctx.Services.GetRequiredService<RulebookReader>();

        Assert.NotNull(await reader.ForPowerAsync("Armor"));

        ctx.Api.SignedIn = null;
        await accounts.SignOutAsync();

        Assert.Null(await reader.ForPowerAsync("Armor"));
    }

    /// <summary>
    /// And one account's answers are not handed to the next one on the same machine.
    ///
    /// <para>The other half of the same fault: the cache is keyed to whoever it was filled for,
    /// so a different account signing in on that tab starts empty rather than inheriting.</para>
    /// </summary>
    [Fact]
    public async Task OneAccountDoesNotInheritAnothersAnswers()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Book["Armor"] = "the publisher's prose";

        var reader = ctx.Services.GetRequiredService<RulebookReader>();
        Assert.NotNull(await reader.ForPowerAsync("Armor"));

        var asked = ctx.Api.Asked.Count(a => a.Contains("Armor", StringComparison.Ordinal));

        // A different account, and the book taken away from the server, so the only way to
        // answer is out of the cache.
        ctx.Api.SignedIn = ("acct-9", "somebody-else");
        ctx.Api.Book.Clear();
        await ctx.Services.GetRequiredService<Accounts>().CompleteSignInAsync("a-token");

        Assert.Null(await reader.ForPowerAsync("Armor"));
        Assert.True(ctx.Api.Asked.Count(a => a.Contains("Armor", StringComparison.Ordinal)) > asked,
            "the server was never re-asked, so the answer came out of the other account's cache.");
    }

    /// <summary>
    /// While the entry is closed, <c>aria-controls</c> names nothing.
    ///
    /// <para>The body renders inside an <c>@if</c>, so naming it unconditionally leaves a
    /// dangling IDREF — the same trap the budget breakdown's disclosure records.
    /// <c>aria-expanded</c> is what carries the state. Unguarded until a reviewer mutated the
    /// attribute to be unconditional and all seven tests here stayed green.</para>
    /// </summary>
    [Fact]
    public void WhileClosedTheDisclosureNamesNoElement()
    {
        using var ctx = SignedInWithTheBook();

        var armor = Armor(ctx);
        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));

        var toggle = editor.Find(".book-toggle");

        Assert.Null(toggle.GetAttribute("aria-controls"));

        toggle.Click();

        // …and once open it names an element that is actually there.
        var named = editor.Find(".book-toggle").GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(named));
        Assert.NotNull(editor.Find("#" + named));
    }

    /// <summary>
    /// Two renders of one Power produce identical markup, ids included.
    ///
    /// <para>The same trap <c>Tooltip</c> hit: an id generated per render breaks the replay
    /// guard that requires two renders of one character to be the same, and it does it silently
    /// — every other test still passes.</para>
    /// </summary>
    [Fact]
    public void TwoRendersOfOnePowerAreIdentical()
    {
        using var first = SignedInWithTheBook();
        using var second = SignedInWithTheBook();

        var (one, two) = (Armor(first), Armor(second));

        var a = first.Render<PowerEditor>(p => p.Add(e => e.Power, one));
        var b = second.Render<PowerEditor>(p => p.Add(e => e.Power, two));

        a.Find(".book-toggle").Click();
        b.Find(".book-toggle").Click();

        Assert.Equal(a.Markup, b.Markup);
    }
}
