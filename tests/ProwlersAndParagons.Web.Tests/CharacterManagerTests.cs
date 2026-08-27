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
    /// <b>The import trigger is a real, styled control, and it is now the same size as the button
    /// beside it.</b> It used to carry <c>.small</c> — a fix for the operating system's raw
    /// "Choose File" chip, which read as an afterthought bolted beside "Start a new character".
    /// That fix worked by making this visibly the lesser of the two, and the owner reported the
    /// result: "skinnier and adjacent but also floating".
    ///
    /// <para><b>They are peers.</b> Both make a character that does not exist yet. Separating them
    /// from the list is a container's job, not a font size's — so <c>.make-another</c> draws a rule
    /// above the pair and gives them an equal share of the bar, and this goes back to full
    /// size inside it.</para>
    ///
    /// <para>What has not changed, and is asserted here because it is the fragile half: the input
    /// is still <c>.sr-only</c> rather than <c>display: none</c>, so it keeps its place in the
    /// accessibility tree and its keyboard reachability, and exactly one label points at it.</para>
    /// </summary>
    [Fact]
    public void TheImportTriggerIsAStyledControlOfEqualWeight()
    {
        using var ctx = new RenderContext();
        var cut = ctx.Render<CharacterManager>();

        var input = cut.Find("input[type=file]");
        Assert.Contains("sr-only", input.ClassList);

        var label = cut.Find("label.btn");
        Assert.Equal("import-character-file", label.GetAttribute("for"));
        Assert.Equal(input.Id, label.GetAttribute("for"));

        // One label, because `for`/`id` is the whole of the association — a second, wrapping one
        // would give the same input two, which is the trap a hidden-input pattern usually falls into.
        Assert.Single(cut.FindAll("label[for=import-character-file]"));

        // **Equal, which is the reversal.** No `.small`, and the same class list the primary
        // carries apart from the one that makes it primary.
        Assert.DoesNotContain("small", label.ClassList);

        var start = cut.FindAll("button")
            .Single(b => b.TextContent.Contains("Start a new character", StringComparison.Ordinal));

        Assert.Contains("primary", start.ClassList);
        Assert.DoesNotContain("small", start.ClassList);

        // And both sit in the one bar, which is what separates them from the list above. Asserted
        // by query rather than by comparing element instances: `Find` hands back a bUnit wrapper and
        // `QuerySelectorAll` hands back the raw AngleSharp element, so the two never compare equal.
        var bar = cut.Find(".make-another");
        Assert.NotNull(bar.QuerySelector("label[for=import-character-file]"));
        Assert.Contains(bar.QuerySelectorAll("button"),
            b => b.TextContent.Contains("Start a new character", StringComparison.Ordinal));
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
    /// <summary>
    /// A saved character with the pointer aimed at it — and the session holding it, which is the
    /// half this used to leave out.
    ///
    /// <para><b>It plants a pointer, so it has to plant the whole state that pointer implies.</b>
    /// Opening a character moves the pointer <em>and</em> restores the character into the session;
    /// the app's own boot does both. Leaving the session empty made a fixture no running app can be
    /// in, and the redesign found it: the open character's name is now read from the session — live,
    /// because somebody may be typing it on the finishing step — so a planted pointer with an empty
    /// session drew a block called "Unnamed character" over a store holding "Ninth Precinct".</para>
    ///
    /// <para>The sheet is named after the label for the same reason: a character's label
    /// <em>is</em> its sheet's name, everywhere else in the app — <c>SavedCharacters.LabelFor</c>
    /// derives one from the other. A fixture where they disagree is testing a state the app cannot
    /// produce.</para>
    /// </summary>
    private static async Task<string> OpenRow(RenderContext ctx, string label, CharacterSheet sheet)
    {
        var who = await ctx.Services.GetRequiredService<IIdentitySource>().CurrentAsync();
        var id = SavedCharacters.NewId();

        sheet.Name = label;

        await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(id, label, sheet, SheetMode.Hero);

        ctx.JSInterop.Setup<string?>("ppStore.load", $"pp.character.v1.{who.Key}.current")
            .SetResult(id);

        ctx.Session.Restore(sheet, SheetMode.Hero);

        return id;
    }

    /// <summary>
    /// <b>The positive control for every test that plants a pointer.</b> If the plant did not take,
    /// each of them would be quietly asserting about an ordinary row instead of the open one — and
    /// they would pass, because a row of that name is on screen either way.
    ///
    /// <para>Under the redesign the character on screen is not a row at all: it has its own block
    /// above the list, marked <em>Open now</em> and carrying its spend, because the panel used to be
    /// unable to say which character you were in.</para>
    /// </summary>
    [Fact]
    public async Task ThePlantedPointerReallyMarksTheCharacterOpen()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        await OpenRow(ctx, "Ninth Precinct", SampleCharacters.Hero());

        var cut = ctx.Render<CharacterManager>();

        var open = cut.Find(".character-open");
        Assert.Contains("Ninth Precinct", open.QuerySelector(".nm")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Open now", open.TextContent, StringComparison.Ordinal);

        // And it is not also in the list below, which would be the same character drawn twice.
        Assert.DoesNotContain("Ninth Precinct",
            cut.FindAll("ul.character-list > li").Select(li => li.TextContent));

        // The character that is open offers no "Open", since there is nowhere to go.
        Assert.DoesNotContain(open.QuerySelectorAll("button"),
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
    /// <b>An empty open slot offers nothing to discard, and that is the state the redesign was
    /// built for.</b> Straight after "Start a new character" the pointer is on a fresh slot holding
    /// nothing, the list holds the character you kept, and the panel used to be unable to say so at
    /// all. It now names the empty slot outright — and draws no Discard beside it, because a button
    /// that would throw away nothing is worse than no button.
    ///
    /// <para>This replaces a test that clicked Discard on an open slot whose session was empty. That
    /// fixture cannot happen in the app — the boot restores whatever the pointer names — and the
    /// guarantee it was reaching for is stronger stated this way: not "discarding an empty slot arms
    /// no undo", but "there is nothing there to discard".</para>
    /// </summary>
    [Fact]
    public async Task AnEmptyOpenSlotIsNamedAndOffersNothingToDiscard()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        // One character kept, and the sheet on screen left empty — exactly what the keep-and-start
        // path leaves behind.
        await ctx.Services.GetRequiredService<ApiCharacterStore>()
            .SaveAsync(SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();

        var open = cut.Find(".character-open");
        Assert.Contains("Nothing chosen yet", open.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(open.QuerySelectorAll("button"),
            b => b.TextContent.Contains("Discard", StringComparison.Ordinal));

        // The positive control: the kept character really is on screen, in the list, with its own
        // Discard — so "no Discard" above is about the empty slot and not about a panel that failed
        // to draw anything.
        Assert.Contains("Discard", Row(cut, "Ninth Precinct").TextContent, StringComparison.Ordinal);

        Assert.False(ctx.Session.CanUndo);
        Assert.False(ctx.Services.GetRequiredService<DiscardedCharacter>().CanUndo);
    }

    /// <summary>
    /// Whatever the panel is drawing for the character with this label — the block for the one on
    /// screen, or a row in the list for any other.
    ///
    /// <para><b>Both shapes, because the redesign gave the open character its own.</b> The panel
    /// could not say which character you were in: one reaches the list only once it is worth
    /// keeping, so straight after "Start a new character" the list showed the one you kept and
    /// nothing marked open at all. The open character now sits above the list in a block of its
    /// own — so a helper that only knew about rows would quietly stop finding half of them.</para>
    ///
    /// <para>These two helpers are the only thing in this file that knows the panel's markup. That
    /// is deliberate and it paid: the layout was rebuilt and every test below it was untouched.</para>
    /// </summary>
    private static IElement Row(IRenderedComponent<CharacterManager> cut, string label) =>
        cut.FindAll(".character-open, ul.character-list > li")
            .Single(row => row.QuerySelector(".nm")!.TextContent.Contains(label, StringComparison.Ordinal));

    /// <summary>The "Discard" button belonging to the row with this label, and no other row's.</summary>
    private static IElement Discard(IRenderedComponent<CharacterManager> cut, string label) =>
        Row(cut, label).QuerySelectorAll("button")
            .First(b => b.TextContent.Contains("Discard", StringComparison.Ordinal));

    // A `title` attribute is covered by `NoComponentExplainsAnythingWithATitleAttribute` in
    // WebPresentationTests, which scans every .razor file — a per-component copy here would be a
    // second, narrower version of the same guard, and it would have to distinguish `title=` the
    // HTML attribute from `Title=` the Panel parameter this file actually carries.
}
