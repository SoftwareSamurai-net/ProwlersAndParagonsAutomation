using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

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
    ///
    /// <para><b>Through the renderer's dispatcher, because the tier page subscribes to the
    /// session.</b> Raising <c>Changed</c> off-dispatcher throws rather than redrawing — the same
    /// constraint <c>DiscardedCharacterTests</c> already records for the layout. Awaited rather
    /// than blocked on: blocking on a renderer task can deadlock against that same dispatcher,
    /// which arrives as a hung CI run rather than a red test.</para>
    /// </summary>
    private static async Task Build(IRenderedComponent<IComponent> page, RenderContext ctx, string name) =>
        await page.InvokeAsync(() =>
        {
            ctx.Session.Sheet.SelectedTierId = "standard";
            ctx.Session.Sheet.Name = name;
            ctx.Session.NotifyChanged();
        });

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

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
    public async Task TheCharacterOnScreenReallyHadSomethingInItBeforeTheClick()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        await Build(page, ctx, "Second Wind");
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        var listed = await StoreIn(ctx).ListAsync();
        Assert.Equal(
            ["Lynchpin", "Second Wind"],
            listed.Characters.Select(c => c.Label).OrderBy(l => l, StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>A character built in a freshly minted slot is listed by its own autosave</b>, without
    /// anybody pressing anything again. That is what makes the second character reachable: the
    /// list is discovered through the index, so a payload nothing indexed is a character nobody
    /// can get back to.
    ///
    /// <para>This test exists because a mutation found the hole. Dropping the index-add from the
    /// autosave left every other test here green — the kept character is indexed by the explicit
    /// save that keeps it, so nothing noticed that the character actually being <em>built</em> was
    /// never listed at all. That is the same shape as the defect this whole slice is about, one
    /// slot further along.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterBuiltInTheNewSlotIsListedWithoutBeingKeptAgain()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        // Built in the slot the press opened, and nothing else is done: no second press, no
        // import, no switch. Ordinary play, and then the tab is closed.
        await Build(page, ctx, "Second Wind");

        var listed = await StoreIn(ctx).ListAsync();
        Assert.Contains(listed.Characters, c => c.Label == "Second Wind");
        Assert.Contains(listed.Characters, c => c.Label == "Lynchpin");
    }

    /// <summary>
    /// And it survives being switched away from. Opening the other character moves the pointer,
    /// and the one left behind has to still be there under its own id afterwards — which is the
    /// whole promise the switcher makes.
    /// </summary>
    [Fact]
    public async Task SwitchingAwayFromTheNewCharacterLeavesItWhereItWas()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        var lynchpin = await StoreIn(ctx).CurrentIdAsync();
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        await Build(page, ctx, "Second Wind");
        var secondWind = await StoreIn(ctx).CurrentIdAsync();

        Assert.NotNull(await StoreIn(ctx).OpenAsync(lynchpin));
        Assert.Equal("Second Wind", (await StoreIn(ctx).ReadAsync(secondWind))?.Sheet.Name);
    }

    /// <summary>
    /// The character on screen after the press is the empty one, and it is not the same slot the
    /// kept character is in. Without the pointer moving, the very next autosave writes the empty
    /// sheet over what was just kept — which is the same loss by a slower route.
    /// </summary>
    [Fact]
    public async Task ThePointerMovesOffTheCharacterThatWasKept()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        var before = await StoreIn(ctx).CurrentIdAsync();
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());
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
    public async Task TheKeepAndThePointerMoveBothLandBeforeTheSheetIsEmptied()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        var storage = ctx.Storage!;
        var from = storage.Calls.Count;

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.Empty((await StoreIn(ctx).ListAsync()).Characters);
    }

    /// <summary>
    /// <b>The tier page redraws when a control inside it empties the sheet.</b> Found on the
    /// deployed site by pressing the button: the character was correctly kept and the sheet
    /// correctly emptied, and the Standard card still read "Selected" until the page was reloaded.
    ///
    /// <para>The cause is one component changing the session and only itself re-rendering:
    /// <c>CharacterManager</c> is a child of <c>ChooseTier</c>, so its own <c>StateHasChanged</c>
    /// redrew the list of characters and left every tier card above it stale. Nothing on that page
    /// subscribed to the session at all.</para>
    ///
    /// <para><b>It predates this slice</b> — the control this replaced also emptied the sheet, and
    /// the card would have gone on saying "Selected" then too. It is fixed here because this is the
    /// button it is visible on, and because a page that says a tier is chosen while the engine says
    /// none is the one thing on that screen a reader would act on.</para>
    /// </summary>
    [Fact]
    public async Task TheTierPageStopsSayingATierIsChosenOnceTheSheetIsEmptied()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");
        page.Render();

        // The positive control: the card really does say so before the click, so "no longer says
        // Selected" is not vacuously true of a page that never said it.
        Assert.Contains("Selected", page.Markup, StringComparison.OrdinalIgnoreCase);

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.Null(ctx.Session.Sheet.SelectedTierId);
        Assert.DoesNotContain("Selected", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // ── The switcher, which is the half the owner could see ──────────────────────────

    /// <summary>
    /// <b>"I don't see the web UI character switcher working yet."</b> It was never broken — it had
    /// nothing to list, because nothing ever added a character to the index it reads. One kept
    /// character is enough to prove the whole path, so this renders the banner's control rather
    /// than asking the store.
    /// </summary>
    [Fact]
    public async Task TheSwitcherOffersTheCharacterThatWasKept()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        var switcher = ctx.Render<CharacterSwitcher>();
        await switcher.FindAll("button").First(b => b.GetAttribute("aria-expanded") is not null)
            .ClickAsync(new MouseEventArgs());

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
    public async Task WithNothingKeptTheSwitcherSaysThisIsYourOnlyCharacter()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        var switcher = ctx.Render<CharacterSwitcher>();
        await switcher.FindAll("button").First(b => b.GetAttribute("aria-expanded") is not null)
            .ClickAsync(new MouseEventArgs());

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

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
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

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
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 1;
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

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
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 5;
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

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
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        ctx.Api.Unreachable = true;
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("could not be saved", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // ── The three an adversarial review demonstrated ────────────────────────────────

    /// <summary>
    /// <b>A browser that refuses storage keeps the character on screen.</b> Nothing was written
    /// down, so there is nowhere to move on to — and the old code could not tell, because the store
    /// handed back the id it was passed whether or not the write landed and the check compared the
    /// two. A dead check that read like a guard, and the cost of it was the whole character.
    /// </summary>
    [Fact]
    public async Task AStorageRefusalKeepsTheCharacterOnScreenAndSaysSo()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        ctx.Storage!.Refuses = true;
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Equal("standard", ctx.Session.Sheet.SelectedTierId);
        Assert.Contains("could not be saved", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The positive control for the one above: with storage working, the identical sequence goes
    /// through. Otherwise "the sheet still says Lynchpin" would be satisfied by a button that never
    /// does anything at all.
    /// </summary>
    [Fact]
    public async Task WithStorageWorkingTheSameSequenceGoesThrough()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.True(string.IsNullOrEmpty(ctx.Session.Sheet.Name));
        Assert.Contains((await StoreIn(ctx).ListAsync()).Characters, c => c.Label == "Lynchpin");
    }

    /// <summary>
    /// <b>The account's cap is asked about only once the character is actually on the server.</b>
    /// The check used to come first and raced the very thing it was there to prevent: the ordinary
    /// autosave is fire-and-forget over HTTP, so a list read straight after an edit can answer from
    /// before that edit's row existed — reading as room on an account that has none, opening a slot,
    /// and letting everything typed into it be refused by a 409 nobody reports.
    ///
    /// <para><b>Asserted on the order of the requests, not by racing a timer.</b> A test that
    /// slept would be testing this machine's scheduler. What has to be true is that the write is
    /// awaited before the list is read, and <c>FakeApi.Asked</c> records both with their
    /// methods.</para>
    /// </summary>
    [Fact]
    public async Task TheAccountsCharacterIsWrittenBeforeItsCapIsRead()
    {
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 5;
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        var from = ctx.Api.Asked.Count;
        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        var asked = ctx.Api.Asked.Skip(from).ToList();

        var wrote = asked.FindIndex(a => a.StartsWith("PUT /api/characters/", StringComparison.Ordinal));
        var read = asked.FindIndex(a => a == "GET /api/characters");

        Assert.True(wrote >= 0, "the character on screen was never written to the account.");
        Assert.True(read >= 0, "the account's cap was never read at all.");
        Assert.True(wrote < read, "the cap was read before the character was written, which is the race.");
    }

    /// <summary>
    /// <b>An account one short of its cap refuses, counting the character just kept.</b> This is the
    /// case the ordering above exists for: the keep fills the last slot, so there is no room for
    /// another — and asking after the write is what makes that answer right.
    /// </summary>
    [Fact]
    public async Task AnAccountWhoseLastSlotTheKeepFillsRefusesToStartAnother()
    {
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 1;
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        // Kept — that half must still have happened — and nothing started.
        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("account is full", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains((await StoreIn(ctx).ListAsync()).Characters, c => c.Label == "Lynchpin");
    }

    /// <summary>
    /// <b>Undoing after starting another does not put a second copy of the kept character into the
    /// new slot.</b> <c>Undo</c> restores into the sheet and never moves the current-character
    /// pointer, so an undo armed here would be a rescue from a danger that did not exist — and the
    /// reader would find their character listed twice. The buffer is simply not armed.
    /// </summary>
    [Fact]
    public async Task StartingAnotherLeavesNoUndoThatWouldDuplicateTheKeptCharacter()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        await Button(page, StartNew).ClickAsync(new MouseEventArgs());

        Assert.False(ctx.Session.CanUndo);

        // And calling it anyway changes nothing — the guard is the buffer being empty, not a
        // caller remembering not to ask.
        ctx.Session.Undo();

        var listed = (await StoreIn(ctx).ListAsync()).Characters;
        Assert.Single(listed, c => c.Label == "Lynchpin");
    }

    // ── The refusals reach importing too, since both go through the same keep ───────

    /// <summary>
    /// Importing shares the keep, so it shares the refusals — and it has to, or the branch that
    /// protects the character on screen would be tested on one of the two controls that uses it.
    /// A refused import leaves both the character and the message where a reader can see them.
    /// </summary>
    [Fact]
    public async Task ImportingIsRefusedRatherThanOverwritingWhenTheCharacterCannotBeKept()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        var page = ctx.Render<ChooseTier>();
        await Build(page, ctx, "Lynchpin");

        ctx.Storage!.Refuses = true;
        await Import(page, Sheet("Someone else's Hero"));

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("could not be saved", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And on a full account the same: the import is refused rather than written over the character
    /// the reader already has.
    /// </summary>
    [Fact]
    public async Task ImportingIsRefusedOnAFullAccount()
    {
        await using var ctx = new RenderContext(storesForReal: true).AsAdministrator();
        ctx.Api.Limit = 1;
        var page = ctx.Render<ChooseTier>();

        await Build(page, ctx, "Lynchpin");
        await Settle(ctx);

        await Import(page, Sheet("Someone else's Hero"));

        Assert.Equal("Lynchpin", ctx.Session.Sheet.Name);
        Assert.Contains("account is full", page.Markup, StringComparison.OrdinalIgnoreCase);
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
