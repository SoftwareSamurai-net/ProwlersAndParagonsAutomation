using AngleSharp.Dom;
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
    public void AnAccountThisPageIsNotForIsToldSoAndShownNothing()
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
        await using var ctx = Managing();

        var page = ctx.Render<Admin>();
        await page.Find("#invite-email").InputAsync("newcomer@example.test");
        await page.Find("form").SubmitAsync();

        Assert.Contains("POST /api/admin/invitations", ctx.Api.Asked);

        // **On the list, not merely on the page**, which is the whole claim in the name. The
        // page also prints "added <address>" as its confirmation, so an assertion against the
        // markup as a whole is satisfied by that sentence alone — and duly was: deleting the
        // `await Reload()` from `Invite()`, so the address is posted and the list never refetched,
        // left this test green. Reading the rows is what makes it the test it says it is.
        await page.WaitForAssertionAsync(() =>
            Assert.Contains("newcomer@example.test",
                page.FindAll("li strong").Select(row => row.TextContent.Trim())));

        // And the box is emptied, so a second click cannot re-send the first address.
        Assert.Equal("", page.Find("#invite-email").GetAttribute("value") ?? "");

        // **The reader is told the link actually went**, not merely that the address is on the
        // list — those are two different facts and the mail path has already failed here once,
        // silently, without anybody being told.
        Assert.Contains("sent a link", page.Find("[role=status]").TextContent);
    }

    /// <summary>
    /// A mail outage must not read as though nothing happened, and must not read as a refusal
    /// either — the address really is on the list; only the mail failed to say so.
    /// </summary>
    [Fact]
    public async Task AMailFailureStillAddsTheAddressAndSaysSoHonestly()
    {
        await using var ctx = Managing();
        ctx.Api.InvitationMailSucceeds = false;

        var page = ctx.Render<Admin>();
        await page.Find("#invite-email").InputAsync("newcomer@example.test");
        await page.Find("form").SubmitAsync();

        // Still added — a broken mail provider does not cost the invitation.
        await page.WaitForAssertionAsync(() =>
            Assert.Contains("newcomer@example.test",
                page.FindAll("li strong").Select(row => row.TextContent.Trim())));

        var status = page.Find("[role=status]").TextContent;
        Assert.Contains("newcomer@example.test", status);
        Assert.Contains("could not be sent", status);

        // **Not the sentence a successful send gets.** "Can sign in now" alone is true of both
        // outcomes, so it cannot be what tells them apart — a mutation collapsing the two
        // messages into one left this test green when it only checked for that.
        Assert.DoesNotContain("has been sent a link", status);

        // Not a refusal: nothing here looks like `_problem`'s alert.
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    /// <summary>An address already on the list is told so, not told it was just sent something.</summary>
    [Fact]
    public async Task AnAddressAlreadyOnTheListIsToldSoRatherThanMailedAgain()
    {
        await using var ctx = Managing();

        var page = ctx.Render<Admin>();
        await page.Find("#invite-email").InputAsync("guest@example.test");
        await page.Find("form").SubmitAsync();

        await page.WaitForAssertionAsync(() =>
            Assert.NotEmpty(page.FindAll("[role=status]")));

        var status = page.Find("[role=status]").TextContent;
        Assert.Contains("guest@example.test", status);
        Assert.Contains("already", status, StringComparison.OrdinalIgnoreCase);
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

    // ── The failure log ─────────────────────────────────────────────────────────

    /// <summary>
    /// The ordinary case, and it has to read as reassurance rather than as a blank section —
    /// see <c>CLAUDE.md</c> on the budget's "None yet." for the same rule applied elsewhere.
    /// </summary>
    [Fact]
    public void NoRecordedFailuresReadsAsReassurance()
    {
        using var ctx = Managing();

        var page = ctx.Render<Admin>();

        Assert.Contains("Nothing has failed", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordedFailureShowsWhatItWasAndHowOften()
    {
        using var ctx = Managing();
        ctx.Api.ErrorLogRows.Add(("mail", "/api/auth/request", "Error",
            "Resend refused to send (HTTP 422).", 5, 1_000, 2_000, "aa11bb"));

        var page = ctx.Render<Admin>();

        Assert.Contains("sending mail", page.Markup, StringComparison.Ordinal);
        Assert.Contains("/api/auth/request", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Resend refused to send (HTTP 422).", page.Markup, StringComparison.Ordinal);
        Assert.Contains("5 times", page.Markup, StringComparison.Ordinal);
        Assert.Contains("aa11bb", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A row nobody has seen in a while must not read as an ongoing outage — the one row this
    /// site has ever produced was a resolved outage, and a count with no sense of time reads as
    /// the site being on fire right now.
    /// </summary>
    [Fact]
    public void AnOldFailureSaysItHasNotHappenedAgain()
    {
        using var ctx = Managing();
        var longAgo = DateTimeOffset.UtcNow.AddDays(-40).ToUnixTimeMilliseconds();
        ctx.Api.ErrorLogRows.Add(("mail", "/api/auth/request", "Error", "Refused.", 5,
            longAgo, longAgo, "aa11bb"));

        var page = ctx.Render<Admin>();

        Assert.Contains("not again since", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>The positive control for the row above: a fault still within the last day does
    /// not carry the same reassurance, or the sentence would be meaningless.</summary>
    [Fact]
    public void ARecentFailureDoesNotClaimToHaveStopped()
    {
        using var ctx = Managing();
        var justNow = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        ctx.Api.ErrorLogRows.Add(("mail", "/api/auth/request", "Error", "Refused.", 1,
            justNow, justNow, "aa11bb"));

        var page = ctx.Render<Admin>();

        Assert.DoesNotContain("not again since", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// An account that may not manage the list is never even asked about the failure log.
    ///
    /// <para>Weaker than the markup, and the reason it exists anyway: the page's own refusal
    /// already hides every panel from an account this page is not for, which a mutation removing
    /// the fetch's own guard does not disturb — the row would simply never reach the markup being
    /// checked. Reading <see cref="FakeApi.Asked"/> catches that mutation, because it is the one
    /// observable difference the guard actually makes: without it, the browser would ask an
    /// endpoint it already knows will refuse it.</para>
    /// </summary>
    [Fact]
    public void AnAccountThatMayNotManageTheListIsNeverAskedAboutTheFailureLog()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-2", "player");

        ctx.Render<Admin>();

        Assert.DoesNotContain("GET /api/admin/error-log", ctx.Api.Asked);
    }

    /// <summary>The positive control: an administrator really is asked, or the assertion above
    /// would pass whether or not the page ever calls the endpoint at all.</summary>
    [Fact]
    public void AnAdministratorIsAskedAboutTheFailureLog()
    {
        using var ctx = Managing();

        ctx.Render<Admin>();

        Assert.Contains("GET /api/admin/error-log", ctx.Api.Asked);
    }

    // ── The players in your own campaigns ───────────────────────────────────────

    /// <summary>
    /// A context whose account manages the list and runs a campaign with one player in it.
    ///
    /// <para>What the <em>server</em> enforces about that scope — that the list is the caller's
    /// own campaigns, that the caller is not in it, that an address outside it answers exactly
    /// like an address nobody has used — is tested against the real server in
    /// <c>tests/worker/admin-accounts.test.mjs</c>, running the real migration in real SQLite.
    /// These are about the panel.</para>
    /// </summary>
    private static RenderContext WithAPlayer()
    {
        var ctx = Managing();

        ctx.Api.Players.Add(("player@example.test", "Kestrel", 2, 5));

        return ctx;
    }

    [Fact]
    public void NobodyInYourCampaignsReadsAsReassuranceRatherThanABlank()
    {
        // Same rule the failure log's empty state follows: a section with nothing in it has to
        // say what the nothing means, or it reads as a panel that failed to draw.
        using var ctx = Managing();

        var page = ctx.Render<Admin>();

        Assert.Contains("Nobody is in one of your campaigns yet", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void EachPlayerIsShownWithTheirAddressAndWhatTheyHold()
    {
        using var ctx = WithAPlayer();
        ctx.Api.Players.Add(("other@example.test", null, 0, 25));

        var page = ctx.Render<Admin>();

        Assert.Contains("Kestrel", page.Markup, StringComparison.Ordinal);
        Assert.Contains("player@example.test", page.Markup, StringComparison.Ordinal);
        Assert.Contains("2 of 5 characters", page.Markup, StringComparison.Ordinal);

        // An account that has never set a name is named by its address rather than by nothing.
        Assert.Contains("other@example.test", page.Markup, StringComparison.Ordinal);
        Assert.Contains("0 of 25 characters", page.Markup, StringComparison.Ordinal);

        // The number is in a box with a visible label — a placeholder is not a label.
        var box = page.Find("#cap-0");
        Assert.Equal("5", box.GetAttribute("value"));
        Assert.Contains(page.FindAll("label"), l => l.GetAttribute("for") == "cap-0");
    }

    /// <summary>
    /// The Save button is dead until there is something to save, and while the write is in flight.
    ///
    /// <para><b>Both halves matter and they fail differently.</b> A button live on an unchanged
    /// number invites a write that changes nothing; a button live during a write invites a second
    /// one, and the two would race for the same row.</para>
    /// </summary>
    [Fact]
    public void TheSaveButtonIsDeadUntilTheNumberChanges()
    {
        using var ctx = WithAPlayer();

        var page = ctx.Render<Admin>();

        Assert.True(Save(page).HasAttribute("disabled"),
            "Save is live before anything has been typed");

        page.Find("#cap-0").Input("25");

        Assert.False(Save(page).HasAttribute("disabled"),
            "Save is still dead after the number changed");

        // Typing the original number back is not a change either — the comparison is against the
        // server's value, not against whether anybody has touched the box.
        page.Find("#cap-0").Input("5");

        Assert.True(Save(page).HasAttribute("disabled"),
            "Save is live for a number that is what the server already has");
    }

    [Fact]
    public void SettingACapSendsItAndRedrawsTheRowFromTheServer()
    {
        using var ctx = WithAPlayer();

        var page = ctx.Render<Admin>();
        page.Find("#cap-0").Input("25");
        Save(page).Click();

        Assert.Contains("PUT /api/admin/accounts/player%40example.test/character-limit",
            ctx.Api.Asked);

        // **Redrawn from the list, not patched in place.** The count beside the cap is the
        // server's answer about that account; a page doing its own arithmetic there would be
        // showing a figure nobody computed.
        page.WaitForAssertion(() =>
            Assert.Contains("2 of 25 characters", page.Markup, StringComparison.Ordinal));

        // And the button goes dead again, because the box now holds what the server holds.
        page.WaitForAssertion(() =>
            Assert.True(Save(page).HasAttribute("disabled")));

        Assert.Empty(page.FindAll("[role=alert]"));
    }

    /// <summary>
    /// Each of the three ways a cap change can fail says something different.
    ///
    /// <para>"Not a number this server will store", "that account is not in one of your campaigns
    /// any more" and "nothing was reached" are three different things to do about it, and one
    /// sentence for all three makes every one of them read as the first.</para>
    /// </summary>
    [Theory]
    [InlineData("refused", "whole number")]
    [InlineData("gone", "no longer in one of your campaigns")]
    [InlineData("broken", "nothing said why")]
    public void ARefusedCapIsReportedRatherThanLookingLikeItWorked(string how, string expected)
    {
        using var ctx = WithAPlayer();

        var page = ctx.Render<Admin>();
        page.Find("#cap-0").Input("900");

        ctx.Api.RefuseCapChanges = how == "refused";
        ctx.Api.CapAccountIsGone = how == "gone";
        ctx.Api.CapChangeBreaks = how == "broken";

        Save(page).Click();

        page.WaitForAssertion(() =>
            Assert.Contains(expected, page.Find("[role=alert]").TextContent,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// A player's sheets are fetched when somebody asks to see them, and not before.
    ///
    /// <para>Twenty players would otherwise be twenty requests for something nobody has asked to
    /// look at, on a page whose other two panels are already two requests.</para>
    /// </summary>
    [Fact]
    public void APlayersSheetsAreReadOnlyWhenTheyAreAskedFor()
    {
        using var ctx = WithAPlayer();
        ctx.Api.Held["player@example.test"] =
        [
            ("c_0", "Ninefold", 1_700_000_000_000, "villain", "high", 118),
            ("c_1", "Second", 1_700_000_000_000, null, null, null),
        ];

        var page = ctx.Render<Admin>();

        Assert.DoesNotContain("GET /api/admin/accounts/player%40example.test/characters",
            ctx.Api.Asked);
        Assert.DoesNotContain("Ninefold", page.Markup, StringComparison.Ordinal);

        // The disclosure names nothing while it is shut — an `aria-controls` on an element that
        // is not in the document is the dangling reference `AriaReferenceTests` sweeps for.
        Assert.Null(Disclosure(page).GetAttribute("aria-controls"));
        Assert.Equal("false", Disclosure(page).GetAttribute("aria-expanded"));

        Disclosure(page).Click();

        page.WaitForAssertion(() =>
            Assert.Contains("Ninefold", page.Markup, StringComparison.Ordinal));

        Assert.Contains("GET /api/admin/accounts/player%40example.test/characters", ctx.Api.Asked);
        Assert.Equal("sheets-0", Disclosure(page).GetAttribute("aria-controls"));
        Assert.Equal("true", Disclosure(page).GetAttribute("aria-expanded"));

        // What a sheet is allowed to say: its name, its tier, its spend, when it moved. **Never a
        // payload** — the server sends none and this panel is not where a character is read.
        var sheets = page.Find("#sheets-0").TextContent;
        Assert.Contains("high", sheets, StringComparison.Ordinal);
        Assert.Contains("118 HP", sheets, StringComparison.Ordinal);

        // A sheet with none of the three index columns prints a name and no figure, which is the
        // front door's rule: zero would be a cost nobody computed.
        Assert.Contains("Second", sheets, StringComparison.Ordinal);
        Assert.DoesNotContain("0 HP", sheets, StringComparison.Ordinal);
    }

    /// <summary>
    /// A player holding nothing and a list that could not be read are two different sentences.
    ///
    /// <para>Saying "nothing saved yet" for the second tells a GM their player has built nothing,
    /// on the strength of a request that failed.</para>
    /// </summary>
    [Fact]
    public void SheetsThatCouldNotBeReadDoNotReadAsAPlayerWhoHasBuiltNothing()
    {
        using var ctx = WithAPlayer();

        var page = ctx.Render<Admin>();
        Disclosure(page).Click();

        page.WaitForAssertion(() =>
            Assert.Contains("Nothing saved to their account yet", page.Markup,
                StringComparison.Ordinal));

        using var broken = WithAPlayer();
        broken.Api.HeldCharactersUnavailable = true;

        var second = broken.Render<Admin>();
        Disclosure(second).Click();

        second.WaitForAssertion(() =>
            Assert.Contains("could not be read", second.Markup, StringComparison.Ordinal));

        Assert.DoesNotContain("Nothing saved to their account yet", second.Markup,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnAccountThatMayNotManageTheListIsNeverAskedAboutThePlayers()
    {
        // Same reasoning as the failure log's pair: the page's own refusal already hides every
        // panel, so only the recorded call can see the guard on the fetch itself.
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-2", "player");

        ctx.Render<Admin>();

        Assert.DoesNotContain("GET /api/admin/accounts", ctx.Api.Asked);
    }

    /// <summary>The positive control for the assertion above.</summary>
    [Fact]
    public void AnAdministratorIsAskedAboutThePlayers()
    {
        using var ctx = Managing();

        ctx.Render<Admin>();

        Assert.Contains("GET /api/admin/accounts", ctx.Api.Asked);
    }

    /// <summary>The first player row's Save button, re-found so a redraw is not read stale.</summary>
    private static IElement Save(IRenderedComponent<Admin> page) =>
        page.FindAll("button").First(b => b.TextContent.Trim() is "Save" or "Saving…");

    /// <summary>The first player row's sheets disclosure, on the same terms.</summary>
    private static IElement Disclosure(IRenderedComponent<Admin> page) =>
        page.FindAll("button").First(b => b.HasAttribute("aria-expanded"));
}
