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
    /// <b>Discarding a row you are not looking at is undoable, and nothing could undo it before.</b>
    ///
    /// <para>The confirmations came off every row when undo arrived, on the reasoning that the one
    /// on screen has the session's buffer behind it and a different row "was never asked about
    /// either, because switching away from it already left it saved under its own id and this
    /// cannot touch that copy". The first half is true of <c>Open</c>; the second is not true of
    /// <c>Delete</c>, which is precisely what destroys that copy. <c>CharacterSession</c> holds the
    /// sheet being edited and never the others, so a background row went on one click with nothing
    /// behind it at all — the row a reader is least likely to be weighing carefully.</para>
    /// </summary>
    [Fact]
    public async Task DiscardingARowYouAreNotLookingAtIsUndoable()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var discarded = ctx.Services.GetRequiredService<DiscardedCharacter>();
        Assert.False(discarded.CanUndo);   // the positive control: nothing armed to begin with

        var cut = ctx.Render<CharacterManager>();
        await Discard(cut, "Ninth Precinct").ClickAsync();

        // It really is gone — this is an undo, not a confirmation in disguise.
        Assert.DoesNotContain((await account.ListAsync()).Characters, c => c.Id == id);

        Assert.True(discarded.CanUndo);
        Assert.Equal("Ninth Precinct", discarded.UndoLabel);
    }

    /// <summary>
    /// The positive control on the rule, and the same one the session's own buffer follows: a row
    /// with nothing on it arms no undo, because there is nothing to bring back and offering to
    /// would be ceremony over nothing.
    /// </summary>
    [Fact]
    public async Task AnEmptyRowYouAreNotLookingAtArmsNoUndo()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var id = SavedCharacters.NewId();
        await account.SaveAsync(id, "Untouched", new CharacterSheet(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        await Discard(cut, "Untouched").ClickAsync();

        Assert.DoesNotContain((await account.ListAsync()).Characters, c => c.Id == id);
        Assert.False(ctx.Services.GetRequiredService<DiscardedCharacter>().CanUndo);
    }

    /// <summary>
    /// A row the store cannot answer for arms nothing — <b>and clears whatever was armed before
    /// it</b>. Leaving the previous offer standing would put an "Undo" in the banner naming a
    /// character the reader has since discarded something else over.
    /// </summary>
    [Fact]
    public async Task ARowTheStoreCannotAnswerForClearsTheOfferRatherThanKeepingAStaleOne()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        var kept = SavedCharacters.NewId();
        var unreadable = SavedCharacters.NewId();
        await account.SaveAsync(kept, "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(unreadable, "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        var discarded = ctx.Services.GetRequiredService<DiscardedCharacter>();
        var cut = ctx.Render<CharacterManager>();

        await Discard(cut, "Ninth Precinct").ClickAsync();
        Assert.True(discarded.CanUndo);

        // Now the server goes away, so reading the next row back answers null.
        ctx.Api.Unreachable = true;
        await Discard(cut, "The Quiet Hour").ClickAsync();

        Assert.False(discarded.CanUndo);
    }

    // ── The row that is open ─────────────────────────────────────────────────────────────────

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
    /// The positive control for the two below: the pointer really did land, so the row under test
    /// really is the open one. Without it they would both be passing against a panel where nothing
    /// matched <c>_currentId</c> — which is exactly the state they were written to leave.
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
    /// <b>The two buffers do not cross.</b> Discarding the open row empties the sheet, which arms
    /// the session's undo; the store-side buffer must stay empty, or the banner would offer to put
    /// back a row that is not the one that just went.
    /// </summary>
    [Fact]
    public async Task DiscardingTheOpenRowArmsTheSessionsUndoAndNotTheStores()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        var id = await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());
        ctx.Session.LoadSample(SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        await Discard(cut, "Ninth Precinct").ClickAsync();

        Assert.Empty(ctx.Session.Sheet.SelectedPowers);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.DoesNotContain(
            (await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == id);

        Assert.True(ctx.Session.CanUndo);
        Assert.False(ctx.Services.GetRequiredService<DiscardedCharacter>().CanUndo);
    }

    /// <summary>
    /// The open row's positive control: an empty sheet arms neither buffer. That is the visit where
    /// nobody has anything at stake, and an offer to bring back nothing is noise.
    /// </summary>
    [Fact]
    public async Task AnEmptyOpenRowArmsNoUndoAtAll()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        var id = await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());

        var cut = ctx.Render<CharacterManager>();
        await Discard(cut, "Ninth Precinct").ClickAsync();

        Assert.DoesNotContain(
            (await ctx.Services.GetRequiredService<ApiCharacterStore>().ListAsync()).Characters,
            c => c.Id == id);

        Assert.False(ctx.Session.CanUndo);
        Assert.False(ctx.Services.GetRequiredService<DiscardedCharacter>().CanUndo);
    }

    private static IElement Row(IRenderedComponent<CharacterManager> cut, string label) =>
        cut.FindAll("ul.chosen > li")
            .Single(row => row.QuerySelector(".body")!.TextContent.Contains(label, StringComparison.Ordinal));

    /// <summary>The "Discard" button belonging to the row with this label, and no other row's.</summary>
    private static IElement Discard(IRenderedComponent<CharacterManager> cut, string label) =>
        Row(cut, label).QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Discard", StringComparison.Ordinal));

    // A `title` attribute is covered by `NoComponentExplainsAnythingWithATitleAttribute` in
    // WebPresentationTests, which scans every .razor file — a per-component copy here would be a
    // second, narrower version of the same guard, and it would have to distinguish `title=` the
    // HTML attribute from `Title=` the Panel parameter this file actually carries.
}
