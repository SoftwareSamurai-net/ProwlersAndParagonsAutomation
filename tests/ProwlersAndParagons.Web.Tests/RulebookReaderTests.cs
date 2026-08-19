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
