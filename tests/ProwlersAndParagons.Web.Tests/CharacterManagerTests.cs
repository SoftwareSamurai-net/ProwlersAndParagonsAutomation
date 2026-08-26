using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The panel at the top of the tier page: what this browser or this account is holding, and
/// the way between them.
///
/// <para><b>Rendered, not read as source</b>, for the reason the two test projects' own remarks
/// give — a repeated figure or a wrong noun in the copy is a bug in what a component *draws*,
/// invisible to anything that only reads the file.</para>
/// </summary>
public sealed class CharacterManagerTests
{
    private static string Text(string markup) =>
        Regex.Replace(Regex.Replace(markup, "<[^>]*>", ""), @"\s+", " ").Trim();

    /// <summary>
    /// The count used to be printed twice — "2 of 5" beside the title, and "2 of 5 on this
    /// account" again in the closing sentence. One panel, one fact, stated once.
    /// </summary>
    [Fact]
    public async Task TheAccountCountAppearsOnceNotTwice()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        var cut = ctx.Render<CharacterManager>();

        var occurrences = Regex.Count(Text(cut.Markup), "2 of 5");
        Assert.Equal(1, occurrences);
    }

    /// <summary>
    /// The positive control for the guard above: the figure really is on the page, just once —
    /// a test that only counted zero-or-one occurrences would pass just as well against a panel
    /// that had quietly stopped stating the count at all.
    /// </summary>
    [Fact]
    public async Task TheAccountCountReallyIsThere()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();

        Assert.Contains("1 of 5", Text(cut.Markup), StringComparison.Ordinal);
    }

    /// <summary>
    /// A signed-in account with nothing built yet is told where a character will actually live —
    /// on the account, following the reader anywhere — rather than the one sentence this used to
    /// share with the anonymous case, which named the wrong place for a signed-in visitor.
    /// </summary>
    [Fact]
    public void TheEmptyStateForAnAccountNamesTheAccount()
    {
        using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var markup = ctx.Render<CharacterManager>().Markup;

        Assert.Contains("account", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("this browser", markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The other half: an anonymous visitor is told the truth for their case too.</summary>
    [Fact]
    public void TheEmptyStateForAnAnonymousVisitorNamesThisBrowser()
    {
        using var ctx = new RenderContext();

        var markup = ctx.Render<CharacterManager>().Markup;

        Assert.Contains("this browser", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("your account", markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The empty state names the action, the same rule every other empty list in this app is
    /// held to. <c>EmptyStateTests</c> covers the five editors and the Pros/Cons picker; this is
    /// the sixth list that can hold nothing, and it is not driven by that file because it is
    /// rendered through a different context (account-aware, not <c>RenderContext.Empty()</c>).
    /// </summary>
    [Fact]
    public void TheEmptyStateNamesTheAction()
    {
        using var ctx = new RenderContext();

        var markup = ctx.Render<CharacterManager>().Markup;
        var state = Text(new Regex(@"<p class=""empty-state[^""]*"">(.*?)</p>", RegexOptions.Singleline)
            .Match(markup).Groups[1].Value);

        Assert.Contains("Start", state, StringComparison.Ordinal);
        Assert.True(state.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 8,
            $"Empty state is too short to say what to do next: \"{state}\"");
    }

    /// <summary>
    /// The import trigger is a real, styled control beside "Start a new character" — not the
    /// operating system's own file-picker chrome, and not a second control competing with the
    /// primary action for the reader's eye.
    /// </summary>
    [Fact]
    public void TheImportTriggerIsAStyledSecondaryControl()
    {
        using var ctx = new RenderContext();

        var cut = ctx.Render<CharacterManager>();

        var input = cut.Find("input[type=file]");
        Assert.Contains("sr-only", input.ClassList);

        var label = cut.Find("label.btn");
        Assert.Contains("small", label.ClassList);
        Assert.Equal(input.GetAttribute("id"), label.GetAttribute("for"));

        // Smaller than the primary action, not the same weight — `.btn.small` is a narrower
        // class list than the plain `.btn` (or `.btn.danger`) "Start a new character" carries.
        var primary = cut.FindAll("button").Single(b => b.TextContent.Contains("Start a new character", StringComparison.Ordinal));
        Assert.DoesNotContain("small", primary.ClassList);
    }

    // ── Throwing one away ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The row a reader is <b>not</b> looking at is the one this panel used to discard on the
    /// first click, and the one it could least afford to.
    ///
    /// <para>The confirmation was keyed to the open character — <c>id == _currentId &amp;&amp;
    /// Session.HasSomethingToLose</c> — so a character built twenty minutes ago and since switched
    /// away from went in one click, with nothing behind it: the session holds the sheet being
    /// edited and never the others, so there was nothing left in memory to put back either.</para>
    /// </summary>
    [Fact]
    public async Task DiscardingACharacterYouAreNotLookingAtAsksFirst()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();

        Assert.Contains("Keep this character", cut.Markup, StringComparison.Ordinal);

        // And nothing has happened yet — the character is still on the account, not merely still
        // drawn. A panel that had deleted it and then asked would satisfy the assertion above.
        Assert.Contains((await account.ListAsync()).Characters, c => c.Id == id);
    }

    /// <summary>Changing your mind leaves it exactly where it was, and offers the row again.</summary>
    [Fact]
    public async Task ChangingYourMindLeavesTheOtherCharacterExactlyAsItWas()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();
        Button(cut, "Keep this character").Click();

        Assert.Contains((await account.ListAsync()).Characters, c => c.Id == id);
        Assert.DoesNotContain("Keep this character", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(Discard(cut, "Ninth Precinct"));
    }

    /// <summary>
    /// The positive control for the question: it is a question, not a wall. Confirming really does
    /// throw the character away — and throws away the one that was asked about, which is the other
    /// half of the same guarantee.
    /// </summary>
    [Fact]
    public async Task ConfirmingDiscardsTheCharacterYouAreNotLookingAt()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var doomed = SavedCharacters.NewId();
        var spared = SavedCharacters.NewId();
        await account.SaveAsync(doomed, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(spared, "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();
        Button(cut, "Yes, discard this character").Click();

        var left = (await account.ListAsync()).Characters;
        Assert.DoesNotContain(left, c => c.Id == doomed);
        Assert.Contains(left, c => c.Id == spared);
    }

    /// <summary>
    /// The other positive control, and the one that says the question is asked for a reason rather
    /// than out of habit: a character with nothing on it still goes on one click.
    ///
    /// <para>Same rule <see cref="CharacterSession.HasSomethingToLose"/> already applied to the
    /// sheet on screen, asked of a row through <see cref="CharacterSession.IsWorthKeeping"/> — so
    /// the two cannot come to mean different things depending on which row was clicked. Without
    /// this, a panel that asked about every row unconditionally would pass every assertion
    /// above.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptyCharacterYouAreNotLookingAtIsDiscardedOnOneClick()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Untouched", new CharacterSheet(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Untouched").Click();

        Assert.DoesNotContain("Keep this character", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain((await account.ListAsync()).Characters, c => c.Id == id);
    }

    /// <summary>
    /// A character the store cannot answer for is asked about rather than assumed empty. Null from
    /// the store means "unreadable payload" and "the server did not answer" alike, and discarding
    /// somebody's character in silence because their network dropped is the wrong half of that.
    /// </summary>
    [Fact]
    public async Task ACharacterTheStoreCannotAnswerForIsAskedAboutRatherThanAssumedEmpty()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();

        // The list has been drawn; now the server goes away, so reading the row back answers null.
        ctx.Api.Unreachable = true;
        Discard(cut, "Ninth Precinct").Click();

        Assert.Contains("Keep this character", cut.Markup, StringComparison.Ordinal);
    }

    // -- The row that is open -----------------------------------------------------------------

    /// <summary>
    /// The open row's own branch, which nothing here could reach before.
    ///
    /// <para><b>Which row is open resolves through <c>ppStore</c>, and bUnit's loose interop
    /// answers null to every read</b> — so <c>_currentId</c> was always <c>legacy</c>, no account
    /// row ever matched it, and every test in this file exercised the not-open half. That is the
    /// caveat <c>PROGRESS.md</c> records about the "open now" marking, and it is why the branch
    /// this panel has had the longest was covered here by nothing at all. Planting the pointer is
    /// the whole of the fix.</para>
    ///
    /// <para>The key is spelled out rather than asked for, the same way <c>SavedCharactersTests</c>
    /// spells out the historical key: it is a storage layout two files have to agree on, and a
    /// test that derived it from the code under test could not catch the layout changing.</para>
    /// </summary>
    private static async Task<string> OpenRow(RenderContext ctx, string label, CharacterSheet sheet)
    {
        var who = await ctx.Services.GetRequiredService<IIdentitySource>().CurrentAsync();
        var id = SavedCharacters.NewId();

        await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(id, label, sheet, SheetMode.Hero);

        ctx.JSInterop.Setup<string?>("ppStore.load", $"pp.character.v1.{who.Key}.current")
            .SetResult(id);

        return id;
    }

    /// <summary>
    /// The positive control for the three below: the pointer really did land, so the row under
    /// test really is the open one. Without it they would all be passing against a panel where
    /// nothing matched <c>_currentId</c> — which is exactly the state they were written to leave.
    /// </summary>
    [Fact]
    public async Task ThePlantedPointerReallyMarksTheRowOpen()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());

        var cut = ctx.Render<CharacterManager>();

        Assert.Contains("open now", Row(cut, "Ninth Precinct").TextContent, StringComparison.Ordinal);

        // And the row that is open offers no "Open" button, since there is nowhere to go.
        Assert.DoesNotContain(
            Row(cut, "Ninth Precinct").QuerySelectorAll("button"),
            b => b.TextContent.Trim() == "Open");
    }

    /// <summary>
    /// The open row asks too, and the question is answered from the sheet in memory rather than
    /// from the stored copy — which is the half of the predicate the not-open tests cannot reach.
    /// </summary>
    [Fact]
    public async Task DiscardingTheOpenCharacterAsksFirst()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        var id = await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());

        // The session is what the open row is weighed by, so it is what has to hold something.
        ctx.Session.LoadSample(SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();

        Assert.Contains("Keep this character", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(ctx.Session.Sheet.SelectedPowers);
        Assert.Contains(
            (await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == id);
    }

    /// <summary>
    /// Confirming empties the sheet <b>and</b> forgets the stored one — both, because clearing
    /// only the session leaves the character in the store and it comes back on the next visit,
    /// which reads as the button not having worked. Same guarantee <c>StartAgainTests</c> holds
    /// for "Start a new character"; this is the row that does it by id.
    /// </summary>
    [Fact]
    public async Task ConfirmingDiscardsTheOpenCharacterAndEmptiesTheSheet()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        var id = await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());
        ctx.Session.LoadSample(SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();
        Button(cut, "Yes, discard this character").Click();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.DoesNotContain(
            (await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == id);
    }

    /// <summary>
    /// The open row's positive control, and the reason the predicate reads the session for this
    /// row rather than the store: the sheet on screen is ahead of storage by however much has not
    /// been written yet. Here it is <em>behind</em> — the stored copy is a whole Hero and the
    /// session is empty — and the empty session is the right answer, because that is what
    /// discarding this row would actually cost the reader.
    /// </summary>
    [Fact]
    public async Task AnEmptyOpenCharacterIsDiscardedOnOneClick()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        var id = await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());

        var cut = ctx.Render<CharacterManager>();
        Discard(cut, "Ninth Precinct").Click();

        Assert.DoesNotContain("Keep this character", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            (await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == id);
    }

    /// <summary>The row with this label.</summary>
    private static IElement Row(IRenderedComponent<CharacterManager> cut, string label) =>
        cut.FindAll("ul.chosen > li")
            .Single(row => row.QuerySelector(".body")!.TextContent.Contains(label, StringComparison.Ordinal));

    /// <summary>The "Discard" button belonging to the row with this label, and no other row's.</summary>
    private static IElement Discard(IRenderedComponent<CharacterManager> cut, string label) =>
        Row(cut, label).QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Discard", StringComparison.Ordinal));

    private static IElement Button(IRenderedComponent<CharacterManager> cut, string label) =>
        cut.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));

    // A `title` attribute is covered by `NoComponentExplainsAnythingWithATitleAttribute` in
    // WebPresentationTests, which scans every .razor file — a per-component copy here would be a
    // second, narrower version of the same guard, and it would have to distinguish `title=` the
    // HTML attribute from `Title=` the Panel parameter this file actually carries.
}
