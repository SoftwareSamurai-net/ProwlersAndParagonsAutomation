using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Making a second character without destroying the first.
///
/// <para><b>What these are about.</b> The owner imported a character, pressed "Start a new
/// character", and lost it. Two reports, one defect: <c>StartNew</c> was <c>StartAgain</c> plus
/// <c>ClearAsync</c> — it emptied the slot the character was in — and an import overwrote whatever
/// the current-character pointer was aimed at. Nothing in the app ever put a character into the
/// index, so the manager's list, the banner's switcher and both undo buffers all worked perfectly
/// against a list that could never hold more than one row.</para>
///
/// <para><b>Every test here presses a control a person presses, against a storage that actually
/// stores</b> — <c>RenderContext(storesForReal: true)</c>. That is deliberate and it is the
/// point. The feature that shipped broken had tests, and all of them drove the machinery directly:
/// they called the store, so they could not have noticed that nothing else did. A test that can
/// only reach the store through a button is a test that fails when the button does not reach
/// it.</para>
/// </summary>
public sealed class StartAnotherTests
{
    private const string StartNew = "Start a new character";

    /// <summary>
    /// A character with a name and a tier — enough that <see cref="CharacterSession.IsWorthKeeping"/>
    /// counts it, which is what decides whether it is written down and listed at all.
    /// </summary>
    private static void Build(RenderContext ctx, string name)
    {
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.Name = name;
        ctx.Session.NotifyChanged();
    }

    private static AccountCharacterStore StoreIn(RenderContext ctx) =>
        ctx.Services.GetRequiredService<AccountCharacterStore>();

    private static IElement Button(IRenderedComponent<IComponent> page, string label) =>
        page.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));

    // ── The report, end to end ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>The owner's report, as nearly as a render test can put it.</b> Build a character, press
    /// the button, and it is still there — under its own name — while the sheet on screen is
    /// empty and ready for the next one.
    /// </summary>
    [Fact]
    public async Task StartingAnotherKeepsTheOneOnScreenAndOpensAnEmptyOne()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        Button(page, StartNew).Click();

        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.True(string.IsNullOrEmpty(ctx.Session.Sheet.Name));

        var listed = await StoreIn(ctx).ListAsync();
        Assert.Contains(listed.Characters, c => c.Label == "Lynchpin");
    }

    /// <summary>
    /// The positive control for the test above, and it is not ceremony: three of this
    /// repository's four historical guard faults were a feature that never ran being mistaken for
    /// one that worked. An empty sheet is "kept" vacuously — nothing is written, nothing is
    /// listed, and every assertion about not losing it holds for free.
    /// </summary>
    [Fact]
    public void TheCharacterOnScreenReallyHadSomethingInItBeforeTheClick()
    {
        using var ctx = new RenderContext(storesForReal: true);
        ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        Assert.True(ctx.Session.HasSomethingToLose);
        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
    }

    /// <summary>
    /// The kept character is listed under the name the reader gave it, not under the placeholder
    /// <c>ListAsync</c> synthesises for a slot nothing has ever named. That placeholder is what
    /// somebody's imported, named character was being called in the one row the list could draw.
    /// </summary>
    [Fact]
    public async Task TheKeptCharacterIsListedUnderItsOwnName()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        Button(page, StartNew).Click();

        var listed = await StoreIn(ctx).ListAsync();
        var kept = Assert.Single(listed.Characters);
        Assert.Equal("Lynchpin", kept.Label);
        Assert.NotEqual("Unnamed character", kept.Label);
    }

    /// <summary>
    /// Two presses give two kept characters and a third empty slot — the list really grows, rather
    /// than the second press overwriting what the first one kept.
    /// </summary>
    [Fact]
    public async Task PressingItTwiceLeavesTwoCharactersBehind()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        Build(ctx, "Lynchpin");
        Button(page, StartNew).Click();

        Build(ctx, "Second Wind");
        Button(page, StartNew).Click();

        var listed = await StoreIn(ctx).ListAsync();
        Assert.Equal(
            ["Lynchpin", "Second Wind"],
            listed.Characters.Select(c => c.Label).OrderBy(l => l, StringComparer.Ordinal));
    }

    /// <summary>
    /// The character on screen after the press is the empty one, and it is not the same slot the
    /// kept character is in. Without the pointer moving, the very next autosave writes the empty
    /// sheet over what was just kept — which is the same loss by a slower route.
    /// </summary>
    [Fact]
    public async Task ThePointerMovesOffTheCharacterThatWasKept()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        var before = await StoreIn(ctx).CurrentIdAsync();
        Button(page, StartNew).Click();
        var after = await StoreIn(ctx).CurrentIdAsync();

        Assert.NotEqual(before, after);

        // And the kept character is still readable at the id it was left under.
        var kept = await StoreIn(ctx).ReadAsync(before);
        Assert.Equal("Lynchpin", kept?.Sheet.Name);
    }

    /// <summary>
    /// The autosave that emptying the sheet fires must not land before the pointer has moved, or
    /// it writes the empty sheet over the character being kept. Asserted on the order of the calls
    /// that actually reached storage, not on the end state — a write and a clear of one key leave
    /// the same dictionary whichever way round they happened.
    /// </summary>
    [Fact]
    public void TheKeepAndThePointerMoveBothLandBeforeTheSheetIsEmptied()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        var storage = ctx.Storage!;
        var from = storage.Calls.Count;

        Button(page, StartNew).Click();

        var writes = storage.Calls.Skip(from)
            .Where(c => c.Identifier == "ppStore.save")
            .Select(c => c.Key)
            .ToList();

        var movedPointer = writes.IndexOf("pp.character.v1.current");
        var indexed = writes.IndexOf("pp.character.v1.index");

        Assert.True(indexed >= 0, "the character on screen was never written into the index");
        Assert.True(movedPointer >= 0, "the current-character pointer was never moved");
        Assert.True(
            indexed < movedPointer,
            "the character was listed only after the pointer had already moved off it");

        // Everything after the pointer moved belongs to the empty sheet, and none of it may be the
        // payload of the character just kept — which for a browser that has never switched before
        // is the bare historical key.
        Assert.DoesNotContain(
            "pp.character.v1", writes.Skip(movedPointer + 1), StringComparer.Ordinal);
    }

    /// <summary>
    /// An untouched sheet costs nothing: nothing is written down, no slot is minted, and the list
    /// stays empty. Otherwise pressing this on a fresh visit would collect empty characters — the
    /// defect the account's own store already had a guard against.
    /// </summary>
    [Fact]
    public async Task AnUntouchedSheetIsNotKeptAndLeavesNoEmptyRow()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        Button(page, StartNew).Click();
        Button(page, StartNew).Click();

        Assert.Empty((await StoreIn(ctx).ListAsync()).Characters);
    }

    // ── The switcher, which is the half the owner could see ──────────────────────────

    /// <summary>
    /// <b>"I don't see the web UI character switcher working yet."</b> It was never broken — it had
    /// nothing to list, because nothing ever added a character to the index it reads. One kept
    /// character is enough to prove the whole path, so this renders the banner's control rather
    /// than asking the store.
    /// </summary>
    [Fact]
    public void TheSwitcherOffersTheCharacterThatWasKept()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");
        Button(page, StartNew).Click();

        var switcher = ctx.Render<CharacterSwitcher>();
        switcher.FindAll("button").First(b => b.GetAttribute("aria-expanded") is not null).Click();

        Assert.Contains("Lynchpin", switcher.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing else saved yet.", switcher.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The positive control for the one above, and the state the owner actually saw: without a
    /// second character the switcher has nothing to offer and says so. A markup assertion for a
    /// name is satisfied by a switcher that has stopped rendering at all, so the one-character
    /// case has to be shown to look different.
    /// </summary>
    [Fact]
    public void WithNothingKeptTheSwitcherSaysThisIsYourOnlyCharacter()
    {
        using var ctx = new RenderContext(storesForReal: true);
        ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        var switcher = ctx.Render<CharacterSwitcher>();
        switcher.FindAll("button").First(b => b.GetAttribute("aria-expanded") is not null).Click();

        Assert.Contains("This is your only character.", switcher.Markup, StringComparison.Ordinal);
    }

    // ── Importing ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// An import goes into a slot of its own. It used to overwrite whatever the pointer was aimed
    /// at, which is the other half of the owner's report — "if I import Lynchpin, my character,
    /// there is no option to start a new character that doesn't blow away my old one".
    ///
    /// <para>Driven through the manager's own handler rather than through a file input: the file
    /// half is <c>CharacterImport</c>'s and is tested there, and what is at issue here is where
    /// the imported character lands.</para>
    /// </summary>
    [Fact]
    public async Task ImportingKeepsTheCharacterAlreadyOpen()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        await Import(page, Sheet("Someone else's Hero"));

        Assert.Equal("Someone else's Hero", ctx.Session.Sheet.Name);

        var listed = await StoreIn(ctx).ListAsync();
        Assert.Contains(listed.Characters, c => c.Label == "Lynchpin");
    }

    /// <summary>
    /// Nothing is buffered for undo, because nothing was destroyed. An undo here would put a
    /// second copy of the kept character into the imported one's slot — a duplicate offered as a
    /// rescue, which is worse than no offer at all.
    /// </summary>
    [Fact]
    public async Task ImportingArmsNoUndoBecauseNothingWasReplaced()
    {
        using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        Build(ctx, "Lynchpin");

        await Import(page, Sheet("Someone else's Hero"));

        Assert.False(ctx.Session.CanUndo);
    }

    private static CharacterSheet Sheet(string name) =>
        new() { SelectedTierId = "standard", Name = name };

    private static async Task Import(IRenderedComponent<IComponent> page, CharacterSheet sheet) =>
        await page.FindComponent<CharacterManager>()
            .InvokeAsync(() => page.FindComponent<ImportCharacter>()
                .Instance.OnImported.InvokeAsync((sheet, SheetMode.Hero)));

    // ── The account side, which had the same gap and a worse failure ─────────────────

    /// <summary>
    /// <b>On an account the old path did not empty a slot — it deleted the row from the
    /// server.</b> So the cap has to be asked about before anything moves: a character created
    /// lazily by its first autosave is refused with a status that path has nowhere to report, and
    /// everything typed into it would go quietly nowhere. Refusing here is the only place a person
    /// can be told.
    /// </summary>
    [Fact]
    public async Task AFullAccountRefusesToStartAnotherAndKeepsWhatIsOpen()
    {
        using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 1;
        var page = ctx.Render<ChooseTier>();

        Build(ctx, "Lynchpin");
        await Settle(ctx);

        Button(page, StartNew).Click();

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("account is full", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The positive control for the refusal: with room on the account the same click goes through,
    /// so "refused" is a state the test can tell from "the button does nothing here either".
    /// </summary>
    [Fact]
    public async Task AnAccountWithRoomStartsAnotherAndSaysNothingAboutBeingFull()
    {
        using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 5;
        var page = ctx.Render<ChooseTier>();

        Build(ctx, "Lynchpin");
        await Settle(ctx);

        Button(page, StartNew).Click();

        Assert.True(string.IsNullOrEmpty(ctx.Session.Sheet.Name));
        Assert.DoesNotContain("account is full", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A server that cannot be reached stops the whole act rather than half of it. Opening a fresh
    /// slot is what stops the next autosave landing on the character being left behind; if that
    /// character was never actually stored, moving on abandons it with no copy anywhere.
    /// </summary>
    [Fact]
    public async Task AServerThatCannotBeReachedStartsNothingAndSaysSo()
    {
        using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        var page = ctx.Render<ChooseTier>();

        Build(ctx, "Lynchpin");
        await Settle(ctx);

        ctx.Api.Unreachable = true;
        Button(page, StartNew).Click();

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("could not be saved", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lets the fire-and-forget write-through that an edit fires actually finish before the next
    /// step reads the server. The account's store is asynchronous over HTTP in a way the browser's
    /// dictionary is not, so a signed-in test that did not wait would be racing its own setup.
    /// </summary>
    private static async Task Settle(RenderContext ctx)
    {
        var store = ctx.Services.GetRequiredService<ICharacterStore>();
        await store.SaveAsync(ctx.Session.Sheet, ctx.Session.Mode);
    }
}
