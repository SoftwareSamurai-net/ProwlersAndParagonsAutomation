using Bunit;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The page that says who may have an account here.
///
/// <para><b>It decides nothing, and that is the property worth rendering for.</b> Whether the
/// reader may see or change this list is the server's answer on every request; the browser holds
/// no claim it could get wrong. So the page is reachable by anybody who types its address, and
/// what it does for an account it is not for — say so, and show nothing — is as much the feature
/// as the list itself.</para>
///
/// <para>What the server actually enforces is tested against the real server in
/// <c>tests/worker/invitations.test.mjs</c>, running the real migration in real SQLite. These are
/// about the page.</para>
/// </summary>
public sealed class AdminPageTests
{
    /// <summary>A context whose account may manage the list, with two addresses on it.</summary>
    private static RenderContext Managing()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("acct-1", "boss");
        ctx.Api.ManagesInvitations = true;
        ctx.Api.You = "boss@example.test";
        ctx.Api.Invited.Add((null, "boss@example.test", true, true, false));
        ctx.Api.Invited.Add(("i_guest", "guest@example.test", false, false, true));

        return ctx;
    }

    [Fact]
    public void AnAccountThisPageIsNotForIsToldSo_AndShownNothing()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-2", "player");

        var page = ctx.Render<Admin>();

        Assert.Contains("not this account", page.Markup, StringComparison.OrdinalIgnoreCase);

        // The point of the refusal: no address, and nothing to press. A page that leaked one row
        // while refusing would be worse than one that did not refuse at all.
        Assert.DoesNotContain("@example.test", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("input"));
        Assert.Empty(page.FindAll("button"));
    }

    [Fact]
    public void NobodySignedInIsSentToSignIn()
    {
        using var ctx = new RenderContext();

        var page = ctx.Render<Admin>();

        Assert.Contains(page.FindAll("a"), a => a.GetAttribute("href") == "signin");
        Assert.Empty(page.FindAll("input"));
    }

    [Fact]
    public void ASiteWithNoServerIsNotReportedAsARefusal()
    {
        // A deploy without its functions answers every address with the app's own page and a 200,
        // which the reader must not be told is a decision about their account. It is also not
        // worth advising them to try again — but "could not reach" is at least true of it.
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-3", "boss");
        ctx.Api.Unreachable = true;

        var page = ctx.Render<Admin>();

        Assert.Contains("could not reach", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryInvitedAddressIsShown()
    {
        using var ctx = Managing();

        var page = ctx.Render<Admin>();

        Assert.Contains("boss@example.test", page.Markup, StringComparison.Ordinal);
        Assert.Contains("guest@example.test", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two rows that cannot be withdrawn here are not offered as though they could be.
    ///
    /// <para>Your own, because withdrawing it would take this page with it; and the address set
    /// on the deployment, which has no row to remove and is changed where the site's other
    /// settings are. The server refuses both — this is about not offering a button that is going
    /// to be refused.</para>
    /// </summary>
    [Fact]
    public void YourOwnAndTheDeploymentsAddressCarryNoWithdrawButton()
    {
        // **Your own row has to carry an id, or this test cannot tell the two cases apart.**
        // It did not: the fixture's administrator was also the deployment's address, whose row has
        // no id at all — so a page offering a button on every row with an id passed, because the
        // one row it would have wrongly offered was excluded by the other half of the condition.
        // Found by mutation, which is the only thing that could have found it.
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-1", "deputy");
        ctx.Api.ManagesInvitations = true;
        ctx.Api.You = "deputy@example.test";
        ctx.Api.Invited.Add((null, "boss@example.test", true, true, false));
        ctx.Api.Invited.Add(("i_deputy", "deputy@example.test", true, true, true));
        ctx.Api.Invited.Add(("i_guest", "guest@example.test", false, false, true));
        ctx.Api.Invited.Add(("i_other", "other@example.test", false, true, true));

        var page = ctx.Render<Admin>();

        var offered = page.FindAll("li")
            .Where(row => row.QuerySelector("button") is not null)
            .Select(row => row.QuerySelector("strong")!.TextContent.Trim())
            .ToList();

        Assert.Equal(["guest@example.test", "other@example.test"], offered);
    }

    [Fact]
    public async Task AddingAnAddressPutsItOnTheList()
    {
        using var ctx = Managing();

        var page = ctx.Render<Admin>();
        page.Find("#invite-email").Input("newcomer@example.test");
        page.Find("form").Submit();

        await Task.Yield();

        Assert.Contains("POST /api/admin/invitations", ctx.Api.Asked);
        page.WaitForAssertion(() =>
            Assert.Contains("newcomer@example.test", page.Markup, StringComparison.Ordinal));

        // And the box is emptied, so a second click cannot re-send the first address.
        Assert.Equal("", page.Find("#invite-email").GetAttribute("value") ?? "");
    }

    [Fact]
    public void WithdrawingTakesTheAddressOffTheList()
    {
        using var ctx = Managing();

        var page = ctx.Render<Admin>();
        page.FindAll("li").Single(r => r.TextContent.Contains("guest@example.test",
            StringComparison.Ordinal)).QuerySelector("button")!.Click();

        page.WaitForAssertion(() =>
            Assert.DoesNotContain("guest@example.test", page.Markup, StringComparison.Ordinal));

        Assert.Contains("DELETE /api/admin/invitations/i_guest", ctx.Api.Asked);
    }

    [Fact]
    public void ARefusedChangeIsReportedRatherThanLookingLikeItWorked()
    {
        using var ctx = Managing();
        ctx.Api.RefuseInvitationChanges = true;

        var page = ctx.Render<Admin>();
        page.FindAll("li").Single(r => r.TextContent.Contains("guest@example.test",
            StringComparison.Ordinal)).QuerySelector("button")!.Click();

        page.WaitForAssertion(() =>
            Assert.NotNull(page.Find("[role=alert]")));

        // Still there, because it was not withdrawn. A list that dropped the row optimistically
        // would show a state the server does not agree with.
        Assert.Contains("guest@example.test", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// An address that has never been used says so.
    ///
    /// <para>Worth more on this site than on most: the mail carrying a sign-in link has already
    /// failed here once, silently, so "invited and never arrived" is a state whoever manages the
    /// list needs to be able to see rather than infer.</para>
    /// </summary>
    [Fact]
    public void WhetherAnAddressHasEverSignedInIsOnTheRow()
    {
        using var ctx = Managing();

        var page = ctx.Render<Admin>();

        var guest = page.FindAll("li")
            .Single(r => r.TextContent.Contains("guest@example.test", StringComparison.Ordinal));

        Assert.Contains("has not signed in yet", guest.TextContent, StringComparison.Ordinal);

        var boss = page.FindAll("li")
            .Single(r => r.TextContent.Contains("boss@example.test", StringComparison.Ordinal));

        Assert.Contains("has signed in", boss.TextContent, StringComparison.Ordinal);
        Assert.Contains("manages this list", boss.TextContent, StringComparison.Ordinal);
    }
}
