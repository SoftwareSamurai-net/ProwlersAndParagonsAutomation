using Bunit;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The sign-in page, which is two pages depending on who is reading it.
///
/// <para><b>Written because a proof of this rendered the wrong half and looked fine.</b> The
/// signed-out form appeared under a heading saying "Signed in", because the harness had asked
/// who was here before it said — <see cref="Accounts"/> remembers the first answer, deliberately,
/// since every save and every load asks. Nothing on the page looked wrong.</para>
/// </summary>
public sealed class SignInPageTests
{
    [Fact]
    public void AVisitorWithNoAccountIsAskedForOneThingAndNoPassword()
    {
        using var ctx = new RenderContext();

        var page = ctx.Render<SignIn>();

        Assert.NotNull(page.Find("#signin-email"));

        // One field, and it is the address. A password box here would be the whole design gone.
        Assert.Empty(page.FindAll("input[type=password]"));
        Assert.Single(page.FindAll("input"));
    }

    [Fact]
    public void SomebodySignedInSeesTheirAccountRatherThanTheForm()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var page = ctx.Render<SignIn>();

        Assert.Contains("player", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("#signin-email"));

        // And a way back out, which is the half of an account somebody on a shared machine needs.
        Assert.Contains("Sign out",
            page.FindAll("button").Select(b => b.TextContent.Trim()));
    }

    /// <summary>
    /// Asking for a link says the same thing whether or not the address has an account.
    ///
    /// <para>Anything else makes this page a way of asking whether somebody has an account here,
    /// one address at a time. The server answers 204 either way; what is asserted here is that
    /// the page does not undo that by saying something different.</para>
    /// </summary>
    [Fact]
    public void AskingForALinkSaysNothingAboutWhetherTheAddressHasAnAccount()
    {
        using var ctx = new RenderContext();

        var page = ctx.Render<SignIn>();
        page.Find("#signin-email").Input("stranger@example.test");
        page.Find("form").Submit();

        var said = page.Markup;

        Assert.Contains("on its way", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no account", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not exist", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A site whose server is not there says so, rather than claiming a link is in flight.
    /// </summary>
    [Fact]
    public void AnUnreachableSiteIsNotReportedAsALinkSent()
    {
        using var ctx = new RenderContext();
        ctx.Api.Unreachable = true;

        var page = ctx.Render<SignIn>();
        page.Find("#signin-email").Input("player@example.test");
        page.Find("form").Submit();

        Assert.Contains("Could not reach", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("on its way", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Something that is not an address is refused before anything is sent.
    ///
    /// <para>The one refusal worth showing. Every other outcome is deliberately the same answer.</para>
    /// </summary>
    [Fact]
    public void SomethingThatIsNotAnAddressIsRefused()
    {
        using var ctx = new RenderContext();

        // The server decides what an address is, and that decision is tested against the server.
        // What is tested here is that the page passes its refusal on rather than swallowing it.
        ctx.Api.LinkRequestAnswer = System.Net.HttpStatusCode.BadRequest;

        var page = ctx.Render<SignIn>();
        page.Find("#signin-email").Input("not-an-address");
        page.Find("form").Submit();

        Assert.Contains("does not look like an email address", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>The box somebody signed in gets to change their name starts at what it is now.</summary>
    [Fact]
    public void TheNameBoxStartsAtTheAccountsCurrentName()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var page = ctx.Render<SignIn>();

        Assert.Equal("player", page.Find("#display-name").GetAttribute("value"));
    }

    /// <summary>Saving a new name changes what the account is called, on this page and not just
    /// in the request that asked for it.</summary>
    [Fact]
    public void SavingANewNameChangesWhatTheAccountIsCalled()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var page = ctx.Render<SignIn>();
        page.Find("#display-name").Input("Dorian");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Save name").Click();

        Assert.Equal("Dorian", ctx.Api.SignedIn?.DisplayName);
        Assert.Contains("Dorian", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A name the server refuses says so, rather than the page quietly acting as though nothing
    /// was asked. Read from <see cref="FakeApi.SignedIn"/> rather than only the markup, because a
    /// broken save that still shows the old name and no problem would satisfy a markup-only check.
    /// </summary>
    [Fact]
    public void ANameTheServerRefusesLeavesTheOldOneAndSaysSo()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.RefuseDisplayNameChanges = true;

        var page = ctx.Render<SignIn>();
        page.Find("#display-name").Input("Dorian");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Save name").Click();

        Assert.Equal("player", ctx.Api.SignedIn?.DisplayName);
        Assert.Contains("could not be saved", page.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
