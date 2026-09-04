using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The rulebook behind <c>Ctrl</c>/<c>Cmd</c>+<c>K</c>: the third group of rows, for a reader who
/// is signed in.
///
/// <para><b>What is held here and what is held elsewhere.</b> That the matching rule is right —
/// that "city" must not reach Plasticity, that a heading beats a mention — is a property of the
/// real code over the real corpus and is held in <c>tests/worker/search.test.mjs</c>. What is held
/// here is the browser's half: that the palette asks, that it asks once for a burst of typing,
/// that it never asks on behalf of somebody signed out, that a late answer for an earlier query
/// cannot land on top of a newer one's rows, and that choosing a row asks <c>/rules</c> the same
/// question.</para>
///
/// <para><b>Every absence here carries a positive control.</b> "No book rows" and "no request" are
/// satisfied completely by a palette that has stopped asking at all, which is the failure shape
/// this repository has shipped four times — so each of those assertions is made beside a drive
/// that differs in one thing and does produce the rows.</para>
/// </summary>
public sealed class PaletteBookTests
{
    /// <summary>An open palette, and nobody signed in — the app every earlier palette test drove.</summary>
    private static RenderContext Anonymous()
    {
        var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<Commands>().Open();
        return ctx;
    }

    /// <summary>
    /// An open palette belonging to somebody the server will answer the book for.
    ///
    /// <para><b>Signed in before the sample is loaded, and the order is load-bearing.</b>
    /// <c>Accounts</c> answers who is here once and remembers it, and loading a sample fires the
    /// autosave, which asks. Setting the account afterwards leaves every service in the context
    /// holding "anonymous" for the rest of the test — a palette that would never search, tested
    /// for searching.</para>
    /// </summary>
    private static RenderContext SignedIn()
    {
        var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct_reader", "A reader");
        ctx.With(SheetMode.Hero);
        ctx.Services.GetRequiredService<Commands>().Open();
        return ctx;
    }

    private static Commands CommandsOf(RenderContext ctx) =>
        ctx.Services.GetRequiredService<Commands>();

    /// <summary>
    /// The book's rows, read structurally rather than by sniffing a row for a citation format.
    ///
    /// <para>The heading is drawn immediately above the first passage and the passages are last,
    /// so every <c>.palette-row</c> that follows it is one of the book's. Reading them by the
    /// shape of their detail line would be asking the test to know the citation format, which is
    /// one of the things under test.</para>
    /// </summary>
    private static List<string> BookRows(IRenderedComponent<CommandPalette> page) =>
        [.. page.FindAll(".palette-group ~ .palette-row")
               .Select(r => r.QuerySelector(".palette-label")!.TextContent.Trim())];

    /// <summary>
    /// The results panel on <c>/rules</c>, or null when no search has answered.
    ///
    /// <para>Asked for by its heading rather than by <c>.chosen</c>, because the chapter index
    /// underneath it is drawn in the same list component — a selector would answer for the page
    /// that has searched nothing at all.</para>
    /// </summary>
    private static IElement? Results(IRenderedComponent<RulesReference> page) =>
        page.FindAll(".panel").FirstOrDefault(
            p => p.QuerySelector(".panel-head h2")?.TextContent.Trim() == "What the book says");

    private static List<string> Searches(RenderContext ctx) =>
        [.. ctx.Api.Asked.Where(a => a.Contains("/api/rulebook/search", StringComparison.Ordinal))];

    /// <summary>Wait for something the renderer is not going to redraw when it happens.</summary>
    private static async Task Until(Func<bool> ready, string what)
    {
        for (var i = 0; i < 200; i++)
        {
            if (ready()) return;
            await Task.Delay(10);
        }

        Assert.Fail($"Waited two seconds and {what} never happened.");
    }

    /// <summary>
    /// Signed in, typing reaches the book: a group of its own, the book's heading as the label and
    /// the printed citation as the detail.
    /// </summary>
    [Fact]
    public async Task SignedInTheBookIsOfferedWithItsPrintedPage()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();

        // The positive control, and it is the reason the assertions below are not satisfied by a
        // component that always draws these rows: an empty box offers the steps and nothing else,
        // to a signed-in reader exactly as to anybody.
        Assert.Empty(page.FindAll(".palette-group"));

        page.Find(".palette-box").Input("knockback");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)));

        Assert.Equal("In the book", page.Find(".palette-group").TextContent.Trim());
        Assert.Contains("KNOCKBACK", BookRows(page));

        // The citation is the one /rules prints, and this is asserted against the row's own detail
        // line rather than against a string written out here — see the cross-surface test below.
        var cited = page.FindAll(".palette-group ~ .palette-row")
            .Select(r => r.QuerySelector(".palette-detail")!.TextContent.Trim())
            .ToList();

        Assert.All(cited, c => Assert.StartsWith("Ch.", c, StringComparison.Ordinal));
        Assert.Contains(cited, c => c.Contains("p.21", StringComparison.Ordinal));
    }

    /// <summary>
    /// The box says the book is there only to somebody who will be shown it.
    ///
    /// <para><b>A label promising a rulebook to an anonymous reader is the wrong promise.</b> They
    /// will type into it and be shown nothing, because the server refuses them the book on the
    /// prefix — and the <c>aria-label</c> is the label, so what it claims is what a screen reader
    /// is told the control does.</para>
    /// </summary>
    [Fact]
    public async Task OnlyASignedInReaderIsToldTheBoxSearchesTheBook()
    {
        using (var anonymous = Anonymous())
        {
            var page = anonymous.Render<CommandPalette>();
            var box = page.Find(".palette-box");

            Assert.Equal("Go to a step, or find a Power", box.GetAttribute("aria-label"));

            // A placeholder is not a label, and the label is not a placeholder either: both are
            // present and both say the same thing.
            Assert.Equal(box.GetAttribute("aria-label"), box.GetAttribute("placeholder"));
        }

        using var signedIn = SignedIn();
        var theirs = signedIn.Render<CommandPalette>();

        await theirs.WaitForAssertionAsync(() =>
            Assert.Contains("book", theirs.Find(".palette-box").GetAttribute("aria-label")!,
                StringComparison.Ordinal));

        Assert.Equal(
            theirs.Find(".palette-box").GetAttribute("aria-label"),
            theirs.Find(".palette-box").GetAttribute("placeholder"));
    }

    /// <summary>
    /// Nobody signed in: no rows from the book, and no request made on their behalf.
    ///
    /// <para><b>The request half is the one worth asserting.</b> The server would refuse it and
    /// the palette would show nothing either way, so a client that asked anyway would look
    /// identical on screen — and would be sending every keystroke of somebody who is not signed in
    /// to an address that exists to serve the publisher's text.</para>
    /// </summary>
    [Fact]
    public async Task AnonymousNothingIsShownFromTheBookAndNothingIsAsked()
    {
        using (var anonymous = Anonymous())
        {
            var page = anonymous.Render<CommandPalette>();
            anonymous.Api.Asked.Clear();

            page.Find(".palette-box").Input("knockback");

            // Longer than the pause, so this is "it did not ask" rather than "it has not asked
            // yet". The pause is the shipped figure; this waits several times it.
            await Task.Delay(CommandsOf(anonymous).BookPause * 4, Xunit.TestContext.Current.CancellationToken);

            Assert.Empty(Searches(anonymous));
            Assert.Empty(page.FindAll(".palette-group"));
            Assert.Empty(BookRows(page));
        }

        // The positive control on both absences: the identical drive, differing only in who is
        // asking, does ask and does draw the rows. Without it a palette that had stopped searching
        // altogether would satisfy every assertion above.
        using var signedIn = SignedIn();
        var theirs = signedIn.Render<CommandPalette>();
        signedIn.Api.Asked.Clear();

        theirs.Find(".palette-box").Input("knockback");

        await theirs.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(theirs)));
        Assert.Single(Searches(signedIn));
    }

    /// <summary>
    /// A word shorter than the server can search on is not sent.
    ///
    /// <para><b>The figure is the server's, not a taste.</b> <c>terms()</c> in
    /// <c>worker/search.js</c> drops every word of two characters or fewer, so a two-letter query
    /// is one the server cannot run at all — it answers <c>found: 0</c> for a question it never
    /// asked. Sending it spends a round trip to be told nothing.</para>
    /// </summary>
    [Fact]
    public async Task AQueryTooShortForTheServerToSearchOnIsNotSent()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        ctx.Api.Asked.Clear();

        page.Find(".palette-box").Input("kn");
        await Task.Delay(CommandsOf(ctx).BookPause * 4, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(Searches(ctx));

        // One more letter, and the same box asks. The control that makes the absence above mean
        // something: two characters is a threshold rather than the search being switched off.
        page.Find(".palette-box").Input("kno");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)));
        Assert.Single(Searches(ctx));
    }

    /// <summary>
    /// A burst of typing is one request, and the box can still ask twice.
    ///
    /// <para><b>This is the only thing in the app that goes over the network per keystroke</b> —
    /// the steps and the 141 Powers are filtered in the browser — so a word typed at an ordinary
    /// speed would otherwise be one request per letter for answers nobody reads.</para>
    ///
    /// <para><b>The second drive is the positive control and it is not optional.</b> "One request
    /// for two keystrokes" is satisfied perfectly by a palette that asks once and never again, so
    /// the same two keystrokes are driven with the answer waited for in between, and that must
    /// produce two.</para>
    /// </summary>
    [Fact]
    public async Task TwoKeystrokesInABurstAreOneRequestAndTwoApartAreTwo()
    {
        using (var burst = SignedIn())
        {
            var page = burst.Render<CommandPalette>();
            burst.Api.Asked.Clear();

            page.Find(".palette-box").Input("trai");
            page.Find(".palette-box").Input("trait");

            await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)));

            // Quiet for longer than the pause after the answer landed, so a second request that
            // was merely slow would still have been counted by the time this reads.
            await Task.Delay(CommandsOf(burst).BookPause * 2, Xunit.TestContext.Current.CancellationToken);

            Assert.Single(Searches(burst));
        }

        using var apart = SignedIn();
        var theirs = apart.Render<CommandPalette>();
        apart.Api.Asked.Clear();

        theirs.Find(".palette-box").Input("trai");
        await theirs.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(theirs)));

        theirs.Find(".palette-box").Input("trait");
        await Until(() => Searches(apart).Count == 2, "the second keystroke asked the book");

        Assert.Equal(2, Searches(apart).Count);
    }

    /// <summary>
    /// A late answer for an earlier query never lands on top of the newer one's rows.
    ///
    /// <para><b>This is the defect class this repository has shipped before</b> — the autosave
    /// race, where two writes in flight together finished in either order — and on this surface it
    /// is silent when it happens: the rows look like an answer, they are just the answer to the
    /// question before last. A pause cannot help, because both requests really were made.</para>
    ///
    /// <para>The seam is on the wire and nowhere else. A seam anywhere further in would be racing
    /// something that is not the race; <c>FakeApi.Holding</c> gates the <em>answer</em>, after the
    /// request has been counted, which is what lets this test know the first call is in flight
    /// before it types the second.</para>
    /// </summary>
    [Fact]
    public async Task ALateAnswerForAnEarlierQueryDoesNotOverwriteTheCurrentRows()
    {
        using var ctx = SignedIn();

        // The pause is not what this is about, and leaving it in would mean sleeping through it
        // twice to reach the state the guard is for.
        CommandsOf(ctx).BookPause = TimeSpan.Zero;

        var slow = new TaskCompletionSource();

        ctx.Api.Holding = async request =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/rulebook/search") return;
            if (request.RequestUri.Query.Contains("q=trait", StringComparison.Ordinal)) await slow.Task;
        };

        var page = ctx.Render<CommandPalette>();
        ctx.Api.Asked.Clear();

        page.Find(".palette-box").Input("trait");
        await Until(() => Searches(ctx).Count == 1, "the first query reached the server");

        page.Find(".palette-box").Input("surprise");
        await page.WaitForAssertionAsync(() => Assert.Equal(["SURPRISE"], BookRows(page)));

        // Now let the older question answer, having been overtaken.
        slow.SetResult();
        await Until(() => slow.Task.IsCompleted, "the held answer was released");
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        // The positive control: both queries really were asked and really were answered, so this
        // is a late answer being dropped rather than a request that never happened.
        Assert.Equal(2, Searches(ctx).Count);
        Assert.Contains(Searches(ctx), a => a.Contains("q=trait", StringComparison.Ordinal));
        Assert.Contains(Searches(ctx), a => a.Contains("q=surprise", StringComparison.Ordinal));

        Assert.Equal(["SURPRISE"], BookRows(page));
        Assert.DoesNotContain("TRAIT CAP", BookRows(page));
    }

    /// <summary>
    /// Choosing a passage asks the rules reference the reader's own question.
    ///
    /// <para><b>What is handed over is the query, not the heading.</b> The palette is 44rem of
    /// overlay and a passage is the publisher's prose set out on a page built for it, so the row
    /// is a request to another screen — the same idiom that hands a Power to the editor on another
    /// step. The row that was chosen is in that page's answer, a few rows down the same ranked
    /// list.</para>
    /// </summary>
    [Fact]
    public async Task ChoosingAPassageAsksTheRulesReferenceTheSameQuestion()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)));

        // The positive control: with no request made, the page draws its box and no answer at all.
        // Without it, "the results are there" is satisfied by a page that always shows them. It is
        // asked by panel heading rather than by `.chosen`, because the chapter index below the
        // results is drawn in the same list component and would answer either way.
        var cold = ctx.Render<RulesReference>();
        await cold.WaitForAssertionAsync(() => Assert.NotEmpty(cold.FindAll("#rules-search")));
        Assert.Null(Results(cold));

        page.FindAll(".palette-group ~ .palette-row")[0].Click();

        Assert.False(CommandsOf(ctx).IsOpen);
        Assert.EndsWith("/rules", ctx.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);

        var rules = ctx.Render<RulesReference>();
        await rules.WaitForAssertionAsync(() => Assert.NotNull(Results(rules)));

        Assert.Equal("knockback", rules.Find("#rules-search").GetAttribute("value"));
        Assert.Contains("KNOCKBACK", Results(rules)!.TextContent, StringComparison.Ordinal);

        // Acted on once. Left set, it would re-run the palette's question over whatever the reader
        // had since typed into the box themselves — the same read-once rule a requested Power
        // follows, and the reason that one is taken rather than peeked at.
        Assert.Null(CommandsOf(ctx).RequestedSearch);
    }

    /// <summary>
    /// Both surfaces print one citation for one passage.
    ///
    /// <para><b>Asserted by comparing what each actually draws</b>, rather than by reading the
    /// source for a shared helper — a second helper spelling the same format the same way today
    /// would pass a source check and drift tomorrow. A citation reading one thing in the palette
    /// and another on <c>/rules</c> is the app disagreeing with itself about where a rule is, on
    /// the one figure the page exists to let somebody check against the book on the table.</para>
    /// </summary>
    [Fact]
    public async Task ThePaletteAndTheRulesReferenceCiteAPassageIdentically()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)));

        var inThePalette = page.FindAll(".palette-group ~ .palette-row")
            .ToDictionary(
                r => r.QuerySelector(".palette-label")!.TextContent.Trim(),
                r => r.QuerySelector(".palette-detail")!.TextContent.Trim(),
                StringComparer.Ordinal);

        var rules = ctx.Render<RulesReference>();
        rules.Find("#rules-search").Input("knockback");
        rules.Find("form").Submit();

        await rules.WaitForAssertionAsync(() => Assert.NotEmpty(rules.FindAll(".chosen > li")));

        var onThePage = rules.FindAll(".chosen > li").ToDictionary(
            li => li.QuerySelector("b")!.TextContent.Trim(),
            li => li.QuerySelector(".cost")!.TextContent.Trim(),
            StringComparer.Ordinal);

        // The control: the two lists really do have a passage in common, so the comparison below
        // is over something rather than over nothing.
        var shared = inThePalette.Keys.Where(onThePage.ContainsKey).ToList();
        Assert.NotEmpty(shared);

        foreach (var heading in shared)
            Assert.Equal(onThePage[heading], inThePalette[heading]);
    }
}
