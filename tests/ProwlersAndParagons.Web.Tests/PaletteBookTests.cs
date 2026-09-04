using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    /// <summary>
    /// How long to let something that is going to happen actually happen.
    ///
    /// <para><b>Far longer than anything here needs, on purpose.</b> bUnit's default is a second,
    /// and a second is a plausible stall on a loaded runner — so a wait tuned close to the pause
    /// would be a test that goes red when the machine is busy, which is a check that ends up
    /// deleted. Nothing waits this long when it is working; a run that does is already
    /// failing.</para>
    /// </summary>
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a thing that must <i>not</i> happen is given to happen anyway.
    ///
    /// <para><b>This one is the dangerous direction.</b> A wait that is too short here does not
    /// fail — it <em>passes</em>, having asked "did it search yet" before it would have.</para>
    ///
    /// <para><b>What it is actually waiting out is a keystroke being dispatched and the component
    /// rendering, and in most of these tests that is all it is.</b> An ask that is refused for who
    /// is asking, or for a query the server could not run, returns <i>before</i> the pause is ever
    /// reached — so half a second is not "the pause plus margin" there, it is a very long time to
    /// give one <c>Input</c> and one render. Where a stale offer means the ask can get as far as
    /// the pause, the test shortens it to <see cref="Impatient"/> as well, and then this is
    /// twenty-five times that; the comment at each such drive says which of the two it is.</para>
    /// </summary>
    private static readonly TimeSpan LongEnoughToBeSure = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// A pause short enough that "it has not asked yet" stops being a possible reading, for the
    /// drives where an ask can reach the pause at all.
    /// </summary>
    private static readonly TimeSpan Impatient = TimeSpan.FromMilliseconds(20);

    /// <summary>Wait for something the renderer is not going to redraw when it happens.</summary>
    private static async Task Until(Func<bool> ready, string what)
    {
        for (var i = 0; i < 1000; i++)
        {
            if (ready()) return;
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Waited ten seconds and {what} never happened.");
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

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

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
                StringComparison.Ordinal), Patient);

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

            // **The pause is not what is being waited out here, and saying otherwise was wrong.**
            // Nobody has ever been offered the book in this context, so the ask returns before the
            // pause is reached at all — what the wait below covers is the keystroke being
            // dispatched and the component rendering, and half a second is a long time for that.
            // The pause is shortened anyway so that this drive cannot start passing for the "it
            // has not asked *yet*" reason if the offer ever became true on some path.
            CommandsOf(anonymous).BookPause = Impatient;
            anonymous.Api.Asked.Clear();

            page.Find(".palette-box").Input("knockback");

            await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

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

        await theirs.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(theirs)), Patient);
        Assert.Single(Searches(signedIn));
    }

    /// <summary>
    /// Signing out takes the book's words off the screen, and does not wait to be asked again.
    ///
    /// <para><b>Blazor WebAssembly has one DI scope for the life of the app and signing out is
    /// pure SPA state with no reload</b>, so anything holding the book's prose holds it across a
    /// sign-out unless something drops it. That is not hypothetical here: it is verbatim the leak
    /// <see cref="RulebookReader"/>'s own cache was fixed for, where a signed-in visitor on a
    /// shared machine could sign out and be shown a Power's entry the server would have
    /// refused.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutTakesTheBooksWordsOffTheScreen()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        // The positive control, and it is the whole test: the rows are there to be lost.
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        ctx.Api.SignedIn = null;
        await accounts.SignOutAsync();

        // Closed and opened again, because that is what a reader does — signing out is done from
        // the account page, and the palette asks who is here on every open.
        CommandsOf(ctx).Close();
        CommandsOf(ctx).Open();

        await page.WaitForAssertionAsync(() => Assert.Empty(BookRows(page)), Patient);

        // **The pause is shortened because this drive can genuinely reach it.** The palette asks
        // who is here as it opens; if that answer has not landed by the time the box is typed into,
        // the ask starts and is dropped after the pause rather than before it — so this wait has
        // to outlast the pause as well as the dispatch and the render. Twenty-five times a 20ms
        // pause is not a close call; waiting the shipped figure and a bit was the version of this
        // that flaked.
        CommandsOf(ctx).BookPause = Impatient;
        ctx.Api.Asked.Clear();

        // And the same query the account could search is not even sent now. Asserting only that
        // the rows are gone would be satisfied by the box having been emptied on the way in,
        // which happens on every open and is not this.
        page.Find(".palette-box").Input("knockback");
        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(Searches(ctx));
        Assert.Empty(BookRows(page));
        Assert.Empty(page.FindAll(".palette-group"));
        Assert.Equal("Go to a step, or find a Power",
            page.Find(".palette-box").GetAttribute("aria-label"));
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

        // **Two characters is short of the server's own threshold, so this ask returns before the
        // pause too** — the wait below is over the keystroke being dispatched and the render that
        // follows it, not over 220ms. Shortened for the same belt-and-braces reason as the
        // anonymous drive: if the threshold ever moved, this would be an absence measured against
        // a pause rather than one measured against nothing having been sent.
        CommandsOf(ctx).BookPause = Impatient;
        ctx.Api.Asked.Clear();

        page.Find(".palette-box").Input("kn");
        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(Searches(ctx));

        // One more letter, and the same box asks. The control that makes the absence above mean
        // something: two characters is a threshold rather than the search being switched off.
        page.Find(".palette-box").Input("kno");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);
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
    ///
    /// <para><b>The burst is driven against a pause this test holds open, not against a duration
    /// it hopes is long enough.</b> Against a real 220ms it was asking whether two calls to
    /// <c>Input</c> could be dispatched inside a fifth of a second, which is a question about how
    /// busy the machine is — it flaked once inside a full five-suite run, and lengthening the pause
    /// only moved the same wager further off. <see cref="Commands.Pausing"/> is the seam: both
    /// keystrokes wait on one gate, so when it is released the earlier one is provably still in its
    /// pause, which is the state the guard is <i>for</i>. There is no wall clock left in this half
    /// at all. The shipped figure is still asserted for the one property this test can honestly
    /// hold it to: that it is a pause and not zero.</para>
    /// </summary>
    [Fact]
    public async Task TwoKeystrokesInABurstAreOneRequestAndTwoApartAreTwo()
    {
        using (var burst = SignedIn())
        {
            var page = burst.Render<CommandPalette>();

            // A pause of zero is no debounce, and it is the mutation this whole test exists to
            // catch — so the shipped default is checked before it is replaced.
            Assert.True(CommandsOf(burst).BookPause > TimeSpan.Zero,
                "the shipped pause is zero, so nothing is debounced and the drive below proves "
                + "nothing about a burst.");

            var pause = new TaskCompletionSource();
            CommandsOf(burst).Pausing = _ => pause.Task;
            burst.Api.Asked.Clear();

            page.Find(".palette-box").Input("trai");
            page.Find(".palette-box").Input("trait");

            // Both keystrokes are now inside the pause, which no amount of machine load can
            // change. Released together, the first must find that a newer one has overtaken it.
            pause.SetResult();

            await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

            Assert.Single(Searches(burst));
        }

        using var apart = SignedIn();
        var theirs = apart.Render<CommandPalette>();
        apart.Api.Asked.Clear();

        theirs.Find(".palette-box").Input("trai");
        await theirs.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(theirs)), Patient);

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
        var wentThrough = false;

        ctx.Api.Holding = async request =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/rulebook/search") return;
            if (!request.RequestUri.Query.Contains("q=trait", StringComparison.Ordinal)) return;

            await slow.Task;

            // Recorded so the wait below is on the gate having actually let go, rather than on a
            // duration somebody picked. What is left after this is deserializing a small body,
            // which is not a thing to time out on.
            wentThrough = true;
        };

        var page = ctx.Render<CommandPalette>();
        ctx.Api.Asked.Clear();

        page.Find(".palette-box").Input("trait");
        await Until(() => Searches(ctx).Count == 1, "the first query reached the server");

        page.Find(".palette-box").Input("surprise");
        await page.WaitForAssertionAsync(() => Assert.Equal(["SURPRISE"], BookRows(page)), Patient);

        // Now let the older question answer, having been overtaken.
        slow.SetResult();
        await Until(() => wentThrough, "the held answer was let through");
        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        // The positive control: both queries really were asked and really were answered, so this
        // is a late answer being dropped rather than a request that never happened. And the window
        // above is known to be long enough because the mutation was watched: with the sequence
        // check removed from `Settle`, this same drive reports "TRAIT CAP" here.
        Assert.Equal(2, Searches(ctx).Count);
        Assert.Contains(Searches(ctx), a => a.Contains("q=trait", StringComparison.Ordinal));
        Assert.Contains(Searches(ctx), a => a.Contains("q=surprise", StringComparison.Ordinal));

        Assert.Equal(["SURPRISE"], BookRows(page));
        Assert.DoesNotContain("TRAIT CAP", BookRows(page));
    }

    /// <summary>
    /// An older answer landing while a newer question is still out does not let the palette say it
    /// found nothing.
    ///
    /// <para><b>The opposite ordering to the test above, and it is a different fault.</b> There the
    /// stale answer arrives last and must not overwrite; here it arrives <i>first</i>, and what it
    /// must not do is report that the palette has finished waiting. "Nothing here matches what you
    /// typed" is drawn from that flag, so an answer to the question before last used to print the
    /// sentence in the middle of a search that was still running — and then replace it with five
    /// rows, which is exactly the "changing its mind" the sentence is held back to avoid.</para>
    ///
    /// <para>Both queries are words the app itself does not match, so the sentence really is what
    /// the box would draw: with a step or a Power on screen there is nothing for it to be wrong
    /// about.</para>
    /// </summary>
    [Fact]
    public async Task AnOlderAnswerArrivingFirstDoesNotSayNothingMatches()
    {
        using var ctx = SignedIn();

        // Not what this is about, and leaving it in means sleeping through it twice.
        CommandsOf(ctx).BookPause = TimeSpan.Zero;

        var older = new TaskCompletionSource();
        var newer = new TaskCompletionSource();
        var olderWentThrough = false;

        ctx.Api.Holding = async request =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/rulebook/search") return;

            if (request.RequestUri.Query.Contains("q=surprise", StringComparison.Ordinal))
            {
                await older.Task;
                olderWentThrough = true;
                return;
            }

            if (request.RequestUri.Query.Contains("q=trait", StringComparison.Ordinal))
                await newer.Task;
        };

        var page = ctx.Render<CommandPalette>();
        ctx.Api.Asked.Clear();

        page.Find(".palette-box").Input("surprise");
        await Until(() => Searches(ctx).Count == 1, "the first query reached the server");

        page.Find(".palette-box").Input("trait cap");
        await Until(() => Searches(ctx).Count == 2, "the second query reached the server");

        // The older question answers first, having been overtaken by a question still in flight.
        older.SetResult();
        await Until(() => olderWentThrough, "the older answer was let through");
        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        // The flag first, because it is what the sentence is drawn from and it does not depend on
        // a render having happened yet.
        Assert.True(CommandsOf(ctx).BookIsBeingAsked,
            "an answer to the question before last reported that nothing is outstanding, while the "
            + "current question is still on the wire.");
        Assert.Empty(page.FindAll(".palette-empty"));

        // And when the newer one lands, the rows are its own.
        newer.SetResult();
        await page.WaitForAssertionAsync(() => Assert.Equal(["TRAIT CAP"], BookRows(page)), Patient);
        Assert.Empty(page.FindAll(".palette-empty"));

        // The positive control on both absences: the same box, a word nothing knows, and the
        // sentence really is printed — so "no .palette-empty" above is the guard working rather
        // than a sentence this palette never draws.
        page.Find(".palette-box").Input("zzzqqq");
        await page.WaitForAssertionAsync(
            () => Assert.Equal(
                "Nothing here matches what you typed.",
                page.Find(".palette-empty").TextContent.Trim()),
            Patient);
    }

    /// <summary>
    /// Typing again takes the previous question's rows off the screen at once, and the row that
    /// replaces them carries the query it is actually under.
    ///
    /// <para><b>Rows for a query nobody is asking are read as an answer, and they were.</b> They
    /// sat under the new text for the pause plus a round trip — long enough to read and press Enter
    /// on. And the query each row hands to <c>/rules</c> is baked in when it is built, so choosing
    /// a stale one searched the book for a word the reader had already typed over: the palette
    /// answering a question about Plasticity with a page about knockback.</para>
    /// </summary>
    [Fact]
    public async Task TypingAgainDropsTheRowsForTheQueryBeforeIt()
    {
        using var ctx = SignedIn();

        CommandsOf(ctx).BookPause = TimeSpan.Zero;

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("surprise");

        // The positive control: there are rows to lose, and they are the older query's.
        await page.WaitForAssertionAsync(() => Assert.Equal(["SURPRISE"], BookRows(page)), Patient);

        var held = new TaskCompletionSource();

        ctx.Api.Holding = async request =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/rulebook/search") return;
            if (!request.RequestUri.Query.Contains("q=trait", StringComparison.Ordinal)) return;

            await held.Task;
        };

        page.Find(".palette-box").Input("trait cap");

        // Read on the render that keystroke caused, with the new answer deliberately still on the
        // wire — which is the whole window the defect lived in.
        Assert.Empty(BookRows(page));
        Assert.Empty(page.FindAll(".palette-group"));

        held.SetResult();
        await page.WaitForAssertionAsync(() => Assert.Equal(["TRAIT CAP"], BookRows(page)), Patient);

        // And the row that is there now hands over the query it is under, not the one before it.
        page.FindAll(".palette-group ~ .palette-row")[0].Click();

        Assert.Equal("trait cap", CommandsOf(ctx).TakeRequestedSearch());
    }

    /// <summary>
    /// A book that could not be asked is not a book with nothing in it.
    ///
    /// <para><b>Both come back as no rows, and only one of them may be said out loud.</b> A search
    /// that answered <c>found: 0</c> means the corpus does not use the word. A session that expired
    /// on the server, a 500, or a laptop off the network mean nothing was learnt at all — and
    /// "nothing here matches what you typed" over those is the app telling a reader the rulebook
    /// has no entry for a word it may well have three of. This is the same mistake
    /// <c>RulebookReader</c>'s own doc comment records the Powers search shipping, one surface
    /// along.</para>
    ///
    /// <para><b>A refusal also stops the offer.</b> A 401 while this browser still believes it is
    /// signed in is an account that is gone, and the box's label promises the book — so it stops
    /// promising it, and does not start again on the next open.</para>
    /// </summary>
    [Fact]
    public async Task ABookThatCouldNotBeAskedIsNotABookWithNothingInIt()
    {
        // Nothing came back at all. The identity is already known by the time the network goes,
        // which is the real shape of it: the reader is signed in and the server is not there.
        using (var lost = SignedIn())
        {
            var page = lost.Render<CommandPalette>();
            lost.Api.Asked.Clear();
            lost.Api.Unreachable = true;

            page.Find(".palette-box").Input("surprise");

            await Until(() => Searches(lost).Count == 1, "the query was sent");
            await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

            Assert.Empty(BookRows(page));
            Assert.Empty(page.FindAll(".palette-group"));
            Assert.Empty(page.FindAll(".palette-empty"));
        }

        // The session expired server-side: this browser still thinks it is signed in, and every
        // address under the book's prefix answers 401.
        using (var expired = SignedIn())
        {
            var page = expired.Render<CommandPalette>();
            expired.Api.BookRefusesTheSession = true;

            page.Find(".palette-box").Input("surprise");

            await Until(() => Searches(expired).Count >= 1, "the query was sent");
            await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

            Assert.Empty(BookRows(page));
            Assert.Empty(page.FindAll(".palette-empty"));

            // And the box stops promising a book the server will not hand over — on this open and
            // on the next one, because the account it was refused for has not changed.
            await page.WaitForAssertionAsync(
                () => Assert.Equal("Go to a step, or find a Power",
                    page.Find(".palette-box").GetAttribute("aria-label")),
                Patient);

            CommandsOf(expired).Close();
            CommandsOf(expired).Open();

            expired.Api.Asked.Clear();
            CommandsOf(expired).BookPause = Impatient;

            page.Find(".palette-box").Input("surprise");
            await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

            Assert.Empty(Searches(expired));
            Assert.Equal("Go to a step, or find a Power",
                page.Find(".palette-box").GetAttribute("aria-label"));
        }

        // The positive control on every absence above, and it is the whole point of the
        // distinction: the identical drive against a server that *does* answer prints the sentence
        // for a word the book really has nothing for.
        using var silent = SignedIn();
        var theirs = silent.Render<CommandPalette>();

        theirs.Find(".palette-box").Input("zzzqqq");

        await theirs.WaitForAssertionAsync(
            () => Assert.Equal(
                "Nothing here matches what you typed.",
                theirs.Find(".palette-empty").TextContent.Trim()),
            Patient);
    }

    /// <summary>
    /// Nothing is asked on behalf of somebody who has just signed out, however close to the same
    /// tick the typing is.
    ///
    /// <para><b>Two windows, and both were open.</b> The palette re-asks who is here when it opens,
    /// and that ask used to run <i>after</i> the interop that moves focus into the box — so between
    /// the caret landing and the answer arriving, the box was typeable and the offer was whatever
    /// it had been before. And an ask that had already started re-reads the offer after its pause,
    /// because signing out is pure SPA state and a fifth of a second is long enough for a reader to
    /// do it from the account page.</para>
    ///
    /// <para>The interop is held open deliberately rather than raced: bUnit answers an unplanned
    /// call immediately, which would close the window this test is about and leave it passing for
    /// the wrong reason.</para>
    /// </summary>
    [Fact]
    public async Task TypingWhileASignOutIsSettlingAsksNothing()
    {
        using var ctx = SignedIn();

        // Focus moving into the box, held. Planned before the render that opens the palette so the
        // call is genuinely outstanding rather than answered on the spot.
        var focus = ctx.JSInterop.SetupVoid("ppPalette.enter", _ => true);

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        // The positive control: this palette does ask, for this account, with this drive.
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        ctx.Api.SignedIn = null;
        await accounts.SignOutAsync();

        CommandsOf(ctx).Close();
        CommandsOf(ctx).BookPause = Impatient;
        ctx.Api.Asked.Clear();

        // Opened and typed into with the focus call still unanswered. Whoever is here has to be
        // settled before the caret arrives, or these keystrokes go out under the old account.
        CommandsOf(ctx).Toggle();
        page.Find(".palette-box").Input("knockback");

        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(Searches(ctx));
        Assert.Empty(BookRows(page));

        focus.SetVoidResult();
    }

    /// <summary>
    /// And an ask already in the air when the account goes is dropped rather than sent.
    ///
    /// <para>The pause is held open rather than waited out, so this is the state itself rather than
    /// a race against a real fifth of a second: a keystroke that started while the reader was
    /// signed in, and a sign-out that lands before the request would.</para>
    /// </summary>
    [Fact]
    public async Task AnAskInFlightWhenTheAccountGoesIsDropped()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();

        // The positive control first: with the pause taken normally, this same drive asks.
        page.Find(".palette-box").Input("knockback");
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        var pause = new TaskCompletionSource();
        CommandsOf(ctx).Pausing = _ => pause.Task;

        ctx.Api.Asked.Clear();
        page.Find(".palette-box").Input("knockback again");

        await Until(() => CommandsOf(ctx).BookIsBeingAsked, "the keystroke reached its pause");

        var accounts = ctx.Services.GetRequiredService<Accounts>();
        ctx.Api.SignedIn = null;
        await accounts.SignOutAsync();

        // What the next open does, done here without one: the palette asks who is here, and that
        // answer has to reach the question already in the air.
        await CommandsOf(ctx).NoteWhoIsAskingAsync();

        pause.SetResult();
        await Task.Delay(LongEnoughToBeSure, Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(Searches(ctx));
        Assert.Empty(BookRows(page));
    }

    /// <summary>
    /// Choosing a passage asks the rules reference the reader's own question — including when the
    /// reader is already looking at it.
    ///
    /// <para><b>What is handed over is the query, not the heading.</b> The palette is 44rem of
    /// overlay and a passage is the publisher's prose set out on a page built for it, so the row
    /// is a request to another screen — the same idiom that hands a Power to the editor on another
    /// step. The row that was chosen is in that page's answer, a few rows down the same ranked
    /// list.</para>
    ///
    /// <para><b>The page is rendered <i>before</i> the row is chosen and it is the same instance
    /// that is asserted on, which is the whole point of this drive.</b> A version of this test that
    /// rendered a fresh <c>RulesReference</c> afterwards passed against a real defect: the request
    /// was read in <c>OnInitializedAsync</c> alone, so a reader already on <c>/rules</c> — the most
    /// likely reader to open the palette and look something up — got nothing at all, because
    /// <c>NavigateTo("rules")</c> from <c>/rules</c> is a no-op and Blazor reuses the instance
    /// rather than initialising a second one. Rendering a second component is what hid it; that is
    /// a thing the app never does and a test always did.</para>
    /// </summary>
    [Fact]
    public async Task ChoosingAPassageAsksTheRulesReferenceTheSameQuestion()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        // On /rules already, with the palette over it. This is the instance every assertion below
        // is about, and nothing renders another one.
        var rules = ctx.Render<RulesReference>();
        await rules.WaitForAssertionAsync(() => Assert.NotEmpty(rules.FindAll("#rules-search")), Patient);

        // The positive control: with no request made yet, the page draws its box and no answer at
        // all. Without it, "the results are there" is satisfied by a page that always shows them.
        // It is asked by panel heading rather than by `.chosen`, because the chapter index below
        // the results is drawn in the same list component and would answer either way.
        Assert.Null(Results(rules));

        page.FindAll(".palette-group ~ .palette-row")[0].Click();

        Assert.False(CommandsOf(ctx).IsOpen);
        Assert.EndsWith("/rules", ctx.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);

        await rules.WaitForAssertionAsync(() => Assert.NotNull(Results(rules)), Patient);

        Assert.Equal("knockback", rules.Find("#rules-search").GetAttribute("value"));
        Assert.Contains("KNOCKBACK", Results(rules)!.TextContent, StringComparison.Ordinal);

        // Acted on once. Left set, it would re-run the palette's question over whatever the reader
        // had since typed into the box themselves — the same read-once rule a requested Power
        // follows, and the reason that one is taken rather than peeked at.
        Assert.Null(CommandsOf(ctx).RequestedSearch);
    }

    /// <summary>
    /// And a reader who arrives at <c>/rules</c> afterwards is asked the same question, which is
    /// the other half of the same request.
    /// </summary>
    [Fact]
    public async Task APassageChosenBeforeTheRulesReferenceExistsIsStillAnswered()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        page.FindAll(".palette-group ~ .palette-row")[0].Click();

        var rules = ctx.Render<RulesReference>();
        await rules.WaitForAssertionAsync(() => Assert.NotNull(Results(rules)), Patient);

        Assert.Equal("knockback", rules.Find("#rules-search").GetAttribute("value"));
        Assert.Contains("KNOCKBACK", Results(rules)!.TextContent, StringComparison.Ordinal);
        Assert.Null(CommandsOf(ctx).RequestedSearch);
    }

    /// <summary>
    /// The book's rows are reachable by the keys the palette prints, and Enter on one does what
    /// the mouse does.
    ///
    /// <para><b>A row that can only be clicked is half a feature here.</b> The palette exists to be
    /// used without leaving the keyboard — its own foot says so in three key boxes — and the book's
    /// rows are appended below the app's own, so reaching them means arrowing past everything that
    /// matched in the browser first. The group heading is <c>role="presentation"</c> with no id and
    /// is deliberately not a stop on the way.</para>
    /// </summary>
    [Fact]
    public async Task TheArrowKeysReachTheBooksRowsAndEnterChoosesOne()
    {
        using var ctx = SignedIn();

        var page = ctx.Render<CommandPalette>();
        page.Find(".palette-box").Input("knockback");

        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        // Where the book starts in the one flat list. Read off the rendered ids rather than
        // counted here, because how many Powers "knockback" matches is not this test's business —
        // and the arrow keys move through exactly these positions.
        var firstBookRow = page.FindAll(".palette-group ~ .palette-row")[0].Id;
        var steps = page.FindAll(".palette-row").Select(r => r.Id).ToList();
        var at = steps.IndexOf(firstBookRow);

        // The control on the drive itself: the book is genuinely below something, so the arrowing
        // below is arrowing rather than a no-op on a one-row list.
        Assert.True(at > 0, $"the book's first row is at {at}, so nothing was arrowed past.");

        for (var i = 0; i < at; i++)
            page.Find(".palette-box").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        // On the row, and announced as being on it: aria-activedescendant is what a screen reader
        // is told, and a ring that moved without it would look right and announce the first row.
        var current = page.FindAll(".palette-row")[at];
        Assert.Equal("true", current.GetAttribute("aria-selected"));
        Assert.Equal(current.Id, page.Find(".palette-box").GetAttribute("aria-activedescendant"));
        Assert.Contains(current.QuerySelector(".palette-label")!.TextContent.Trim(), BookRows(page));

        page.Find(".palette-box").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // The same outcome the click has: the palette is gone, /rules is where the reader is, and
        // the question that goes with them is the one they typed.
        Assert.False(CommandsOf(ctx).IsOpen);
        Assert.EndsWith("/rules", ctx.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);
        Assert.Equal("knockback", CommandsOf(ctx).TakeRequestedSearch());
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
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(BookRows(page)), Patient);

        var inThePalette = page.FindAll(".palette-group ~ .palette-row")
            .ToDictionary(
                r => r.QuerySelector(".palette-label")!.TextContent.Trim(),
                r => r.QuerySelector(".palette-detail")!.TextContent.Trim(),
                StringComparer.Ordinal);

        var rules = ctx.Render<RulesReference>();
        rules.Find("#rules-search").Input("knockback");
        rules.Find("form").Submit();

        await rules.WaitForAssertionAsync(() => Assert.NotEmpty(rules.FindAll(".chosen > li")), Patient);

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
