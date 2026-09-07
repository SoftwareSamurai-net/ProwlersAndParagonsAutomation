using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>Two edits, two writes, and the older one may not land second.</b>
///
/// <para>The autosave fires once per <see cref="CharacterSession.NotifyChanged"/> and, before
/// <see cref="Autosave"/> existed, each of those was a fire-and-forget <c>PUT</c> with nothing
/// ordering it against the one before. Against local storage that is harmless — the write is a
/// synchronous <c>localStorage.setItem</c>. Against an account it is a lost update: two answers
/// to two requests really are in flight together, the server stores whichever body reaches it
/// last, and it writes down nothing that would let it tell an older one from a newer one. The
/// palette's rulebook search already carries a sequence guard for exactly this shape, and
/// <c>docs/guide/browser.md</c> names the cause there as "the autosave's defect in a new place".
/// This is that defect in its original place.</para>
///
/// <para><b>What it cost.</b> A name is typed one character at a time, so a burst of edits is the
/// ordinary case rather than a corner: the e2e <c>ACCOUNT_SAVE</c> check found a second browser
/// holding "Account Bound H" for a character the first browser had finished typing as "Account
/// Bound Hero" and had been told was saved. That is a player losing the tail of a keystroke run to
/// a slow network, and the shell saying "Saved" over it.</para>
///
/// <para><b>The gate is at the wire and nowhere else</b>, which is where <see cref="FakeApi"/>
/// already puts one: <see cref="FakeApi.BeforeStoringCharacter"/> is reached after the request has
/// been recorded and before the row is replaced, so a test can hold one write open, make a second
/// edit, and decide the order the two bodies are stored in. A seam anywhere else would be a seam
/// where the race is not.</para>
/// </summary>
public sealed class AutosaveOrderTests
{
    private const string Typed = "Account Bound H";
    private const string Finished = "Account Bound Hero";

    private const string AlphaId = "c_alpha";
    private const string BravoId = "c_bravo";

    /// <summary>
    /// <b>The account ends up holding what was on screen, not what was on screen two keystrokes
    /// ago.</b>
    ///
    /// <para>The first write is held at the wire, a second edit is made behind it, and only then
    /// is the first released. Whatever the app chooses to do with the second edit — send it beside
    /// the first, or wait for the first to land — the account has to finish holding the newer
    /// name. Before <see cref="Autosave"/> it finished holding the older one.</para>
    /// </summary>
    [Fact]
    public async Task AnEditMadeWhileAWriteIsAtTheWireIsNotLostToItsOwnPredecessor()
    {
        await using var ctx = SignedIn();
        var layout = ctx.Render<MainLayout>();

        var held = HoldTheFirstWrite(ctx);

        await Type(layout, ctx, Typed);
        await held.Reached.Task.WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        // The second keystroke, made while the first write is still open at the wire. Everything
        // between the edit and the request is synchronous in this context — FakeLocalStorage and
        // FakeApi both answer without yielding — so by the time this returns the app has already
        // done whatever it is going to do about the second edit.
        await Type(layout, ctx, Finished);

        held.Release.SetResult();

        await layout.WaitForAssertionAsync(async () => Assert.Equal(Finished, await NameHeld(ctx)));
    }

    /// <summary>
    /// <b>"Saved" is never said about bytes the account does not hold.</b>
    ///
    /// <para>The same interleaving, read from the other side. The shell's word is derived from the
    /// version a completed write reported against the version the character is at now — see
    /// <see cref="CharacterSession.Saved"/> — so with two writes racing it can be true of a write
    /// that has since been overwritten by an older one. A reader who is told their work is kept and
    /// whose work is not kept is worse off than one who is told nothing.</para>
    /// </summary>
    [Fact]
    public async Task SavedIsNotSaidWhileTheAccountHoldsAnOlderName()
    {
        await using var ctx = SignedIn();
        var layout = ctx.Render<MainLayout>();

        var held = HoldTheFirstWrite(ctx);

        await Type(layout, ctx, Typed);
        await held.Reached.Task.WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        await Type(layout, ctx, Finished);

        held.Release.SetResult();

        // The positive control: the word has to actually appear, or the assertion under it is
        // about a state the app never reached and would hold for a shell that says nothing at all.
        await layout.WaitForAssertionAsync(
            () => Assert.Equal("Saved", layout.Find(".save-status").TextContent.Trim()));

        Assert.Equal(ctx.Session.Sheet.Name, await NameHeld(ctx));
    }

    /// <summary>
    /// <b>The mechanism, stated on its own: one write at the wire at a time.</b>
    ///
    /// <para>The two tests above say what the account ends up holding, which is the outcome worth
    /// having; this says how it is arrived at, because the outcome could also be reached by a
    /// version the server compared — and it is not, so a later reading of this file should not
    /// have to guess. A second request cannot overtake a first that was never sent.</para>
    ///
    /// <para><b>Bounded rather than instantaneous, and the bound is what it is for.</b> This is an
    /// absence, and an absence needs a window. The window is the same synchronous stretch the test
    /// above relies on: with the wire held open, a second write that was going to be dispatched has
    /// already been dispatched by the time the edit's <c>InvokeAsync</c> returns.</para>
    /// </summary>
    [Fact]
    public async Task NoSecondWriteIsSentWhileTheFirstIsStillOpen()
    {
        await using var ctx = SignedIn();
        var layout = ctx.Render<MainLayout>();

        var held = HoldTheFirstWrite(ctx);

        await Type(layout, ctx, Typed);
        await held.Reached.Task.WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        await Type(layout, ctx, Finished);
        await Type(layout, ctx, Finished + "!");

        Assert.Equal(1, Volatile.Read(ref held.Writes));

        held.Release.SetResult();

        await layout.WaitForAssertionAsync(
            async () => Assert.Equal(Finished + "!", await NameHeld(ctx)));
    }

    /// <summary>
    /// <b>The version announced is read before the write and not after it, and this is the
    /// interleaving that tells the two apart.</b>
    ///
    /// <para><see cref="Autosave"/>'s doc comment says the announced version can only ever
    /// understate what landed. Nothing held it to that: the tests above hold the <em>first</em>
    /// write open, and both orderings of that one line behave identically until a second write is
    /// in flight. So this holds the <em>coalesced</em> one — the moment the account is holding the
    /// older name, the reader has typed a newer one, and the write carrying it has not landed.</para>
    ///
    /// <para><b>Read before:</b> the first write announces the version it started at, which is no
    /// longer the character's version, so the shell says nothing and the write that follows says it
    /// truthfully. <b>Read after:</b> it announces the version the character is at <em>now</em> —
    /// which is the version of bytes still on the wire — and the shell tells a reader their
    /// finished name is kept while the account holds a prefix of it. That is the exact lie this
    /// class was written to end, and it survives the fix if the line moves.</para>
    ///
    /// <para><b>The positive control is the second gate being reached.</b> It is proof that the
    /// first write returned and the pump went round, which is the only moment the assertion below
    /// is about: without it, "the shell does not say Saved" would hold just as well for a run in
    /// which no write had finished at all.</para>
    /// </summary>
    [Fact]
    public async Task SavedIsNotSaidForBytesStillOnTheWire()
    {
        await using var ctx = SignedIn();
        var layout = ctx.Render<MainLayout>();

        var held = HoldEachWrite(ctx);

        await Type(layout, ctx, Typed);
        await held.Reached(1).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        await Type(layout, ctx, Finished);

        held.Let(1);

        // The coalesced write has reached the wire, so the first has returned and been announced.
        await held.Reached(2).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        // The control on the state: this is the window the assertion is about — the account holds
        // the older name and the newer one has not landed.
        Assert.Equal(Typed, await NameHeld(ctx));
        Assert.Equal(Finished, ctx.Session.Sheet.Name);

        layout.Render();

        Assert.NotEqual("Saved", layout.Find(".save-status").TextContent.Trim());

        held.Let(2);

        await layout.WaitForAssertionAsync(
            () => Assert.Equal("Saved", layout.Find(".save-status").TextContent.Trim()));

        Assert.Equal(Finished, await NameHeld(ctx));
    }

    /// <summary>
    /// <b>No bytes land under an id they were not built for, when the reader switches character
    /// while a write is open.</b>
    ///
    /// <para><b>This is the hazard coalescing introduces and nothing else in the file asks about.</b>
    /// The old subscription wrote once per edit and each write carried the sheet it fired on. The
    /// pump re-reads the live sheet <em>after</em> the previous write returns — which is the whole
    /// point of it — so between those two moments the reader may have opened somebody else
    /// entirely. The coalesced write then has a sheet from one character and a pointer that may
    /// name another, and the two are read at different instants.</para>
    ///
    /// <para><b>The invariant, and it holds in both directions.</b> The write already at the wire
    /// carries the first character's bytes and its address was fixed before the switch, so it lands
    /// on the first character. The coalesced write reads the sheet <em>and</em> resolves the pointer
    /// after it, so both are the second character. Neither writes one character's bytes over the
    /// other's row, which is the only failure here that would cost somebody a character rather than
    /// a keystroke.</para>
    ///
    /// <para><b>Switched through the manager's own control</b>, because "open another character" is
    /// two lines — <c>Store.OpenAsync</c> then <c>Session.Open</c> — and a test that ran them by
    /// hand would go on passing over a manager that had stopped doing one of them.</para>
    /// </summary>
    [Fact]
    public async Task SwitchingCharacterWhileAWriteIsOpenPutsNeitherOverTheOther()
    {
        await using var ctx = SignedIn();

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await Store(account, AlphaId, "Alpha");
        await Store(account, BravoId, "Bravo");
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(AlphaId);

        // Put Alpha on screen silently, so the first write this test sees is the edit below and
        // not the opening of the fixture.
        var alpha = await account.LoadAsync();
        ctx.Session.RestoreBeforeFirstRender(alpha!.Value.Sheet, alpha.Value.Mode, AlphaId);

        var layout = ctx.Render<MainLayout>();
        var held = HoldEachWrite(ctx);

        await Type(layout, ctx, "Alpha edited");
        await held.Reached(1).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        // The switch, made while that write is still at the wire.
        var manager = ctx.Render<ProwlersAndParagonsAutomation.Web.Components.CharacterManager>();

        await manager.FindAll(".open-target")
            .First(b => b.TextContent.Contains("Bravo", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // Two controls on the act: the switch really landed on the sheet, and the write it was
        // supposed to overtake really had not finished. Without the second this asserts nothing
        // about an interleaving.
        Assert.Equal("Bravo", ctx.Session.Sheet.Name);
        Assert.Equal(1, held.SoFar);

        held.Let(1);

        await held.Reached(2).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        held.Let(2);

        // The addresses, in order: the write that was open went to the character it was built
        // from, and the coalesced one went to the character now on screen.
        await layout.WaitForAssertionAsync(() => Assert.Equal(BravoId, held.IdOf(2)));
        Assert.Equal(AlphaId, held.IdOf(1));

        // And the bytes: neither row is holding the other character.
        await layout.WaitForAssertionAsync(async () =>
        {
            Assert.Equal("Alpha edited", (await account.LoadAsync(AlphaId))!.Value.Sheet.Name);
            Assert.Equal("Bravo", (await account.LoadAsync(BravoId))!.Value.Sheet.Name);
        });
    }

    /// <summary>
    /// <b>The store's refusals survive being reached from inside the pump's loop.</b>
    ///
    /// <para><c>PROGRESS.md</c> items 26 and 27 are two writes the account's store declines rather
    /// than makes — an empty sheet over a real character, and anything at all over a character this
    /// browser could not read — and both are reported in <c>.save-status</c>, because a write
    /// declined in silence leaves somebody typing into a sheet that is not being kept. Every test
    /// of either drives the <em>first</em> write after the state arises. The coalesced write is a
    /// second trip round a loop, and nothing had asked whether a refusal there still reaches a
    /// reader or still declines to write.</para>
    ///
    /// <para><b>It is the interleaving that makes it the coalesced one.</b> A rename is put on the
    /// wire and held there; the sheet is emptied down to a bare tier behind it, which is
    /// <see cref="CharacterSession.IsWorthKeeping"/> true and
    /// <see cref="CharacterSession.HasNothingOnIt"/> true — the exact pair item 26's guard exists
    /// for — and that edit can only be sent by the pump going round again. The rename lands, the
    /// second trip is refused, the account keeps the rename, and the shell says so.</para>
    /// </summary>
    [Fact]
    public async Task ARefusalOnTheCoalescedWriteIsStillReportedAndStillWritesNothing()
    {
        await using var ctx = SignedIn();

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await Store(account, AlphaId, "Jetstream");
        await ctx.Services.GetRequiredService<SavedCharacters>().SetCurrentAsync(AlphaId);

        var stored = await account.LoadAsync();
        ctx.Session.RestoreBeforeFirstRender(stored!.Value.Sheet, stored.Value.Mode, AlphaId);

        var layout = ctx.Render<MainLayout>();
        var held = HoldEachWrite(ctx);

        await Type(layout, ctx, "Jetstream renamed");
        await held.Reached(1).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        // Emptied down to a bare tier while that rename is still at the wire, so the only thing
        // that can carry this edit is the pump's next trip round.
        await layout.InvokeAsync(() =>
        {
            ctx.Session.StartAgain(offerUndo: false);
            ctx.Session.Sheet.SelectedTierId = "street_level";
            ctx.Session.NotifyChanged();
        });

        // The controls on the state: the sheet really is the pair item 26 refuses, and the write
        // it has to be refused on really has not started yet.
        Assert.True(CharacterSession.IsWorthKeeping(ctx.Session.Sheet));
        Assert.True(ctx.Session.HasNothingOnIt(ctx.Session.Sheet));
        Assert.Equal(1, held.SoFar);

        held.Let(1);

        await layout.WaitForAssertionAsync(() => Assert.Contains(
            "Jetstream renamed was not overwritten",
            layout.Find(".save-status").TextContent,
            StringComparison.Ordinal));

        // And it declined rather than wrote: no second body reached the wire, and the account is
        // still holding the rename the first one carried.
        Assert.Equal(1, held.SoFar);
        Assert.Equal("Jetstream renamed", (await account.LoadAsync(AlphaId))!.Value.Sheet.Name);
    }

    // ── That the real app is wired to any of this ─────────────────────────────────────────────

    /// <summary>
    /// <b>The app really starts the autosave, and not only <see cref="RenderContext"/> does.</b>
    ///
    /// <para><b>This is the positive control on everything above it, and it was missing.</b> Every
    /// test in this file drives <see cref="Autosave"/> through the test context, which starts one
    /// of its own — so deleting <c>web/Program.cs</c>'s <c>Start()</c> left the whole suite green
    /// while the deployed application subscribed to nothing and wrote no character anywhere.
    /// Measured, not reasoned about: with that one line removed, 5,829 tests passed.</para>
    ///
    /// <para><b>Which is <c>PROGRESS.md</c> item 10 exactly.</b> That was a feature built, tested,
    /// reviewed twice and merged while nothing in the application ever wrote to the store it read
    /// from, because every test reached the store itself. A context that wires the service it is
    /// testing is the same fault with a shorter fuse: the thing under test is real, and the only
    /// thing missing is the app asking for it.</para>
    ///
    /// <para><b>What it cannot do</b>, said plainly because <c>CLAUDE.md</c> requires it: bUnit
    /// cannot run the host's startup, so this is a scan for one spelling and it has no opinion
    /// about the lines around it. A boot that kept the call and unsubscribed on the next line
    /// would walk through it. It is the cheap catch on the app forgetting to ask, which is the
    /// failure that actually happened, not a proof that the subscription survives the boot.</para>
    /// </summary>
    [Fact]
    public void TheAppItselfStartsTheAutosave()
    {
        Assert.True(StartsTheAutosave.IsMatch(Program()),
            "`web/Program.cs` no longer starts the autosave, so nothing in the deployed "
            + "application subscribes to the character's change bell and no edit is written down "
            + "anywhere. Every test in this file would go on passing: RenderContext starts an "
            + $"Autosave of its own. Pattern: {StartsTheAutosave}");
    }

    /// <summary>
    /// The positive control on the scan above. A pattern that had been loosened until registering
    /// the service counted as starting it would report a wired app over an unwired one — and
    /// registering it is exactly what would still be there after the deletion this guards against.
    /// </summary>
    [Fact]
    public void TheScanDoesNotAcceptRegisteringTheAutosaveAsStartingIt()
    {
        Assert.DoesNotMatch(StartsTheAutosave, "builder.Services.AddScoped<Autosave>();");
        Assert.DoesNotMatch(StartsTheAutosave, "host.Services.GetRequiredService<Autosave>();");

        // ...while it still matches the line it is about, so "rejects everything" cannot pass for
        // "rejects the edit".
        Assert.Matches(StartsTheAutosave, "host.Services.GetRequiredService<Autosave>().Start();");
    }

    /// <summary>
    /// The one line of <c>web/Program.cs</c> that turns <see cref="Autosave"/> from a registered
    /// service into a subscription: the host resolves it and calls <c>Start()</c> on it.
    /// </summary>
    private static readonly Regex StartsTheAutosave = new(
        @"GetRequiredService<\s*Autosave\s*>\(\s*\)\s*\.\s*Start\(\s*\)\s*;",
        RegexOptions.None, TimeSpan.FromSeconds(5));

    private static string Program() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "web", "Program.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }

    // ── The fixture ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A context whose stores really store and whose visitor really has an account, so the write
    /// goes to <see cref="ApiCharacterStore"/> rather than to this browser.
    /// </summary>
    private static RenderContext SignedIn()
    {
        var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("acct_saver", "Saver");

        return ctx;
    }

    private sealed class HeldWrite
    {
        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Writes;
    }

    /// <summary>
    /// Hold the first <c>PUT</c> open at the wire and count every one that reaches it.
    ///
    /// <para>The first is held; every later one is let through untouched, so the test decides only
    /// the order of the first two rather than driving the whole conversation.</para>
    /// </summary>
    private static HeldWrite HoldTheFirstWrite(RenderContext ctx)
    {
        var held = new HeldWrite();

        ctx.Api.BeforeStoringCharacter = async _ =>
        {
            if (Interlocked.Increment(ref held.Writes) != 1) return;

            held.Reached.SetResult();
            await held.Release.Task;
        };

        return held;
    }

    /// <summary>
    /// Hold <em>every</em> <c>PUT</c> open at the wire, each behind a gate of its own.
    ///
    /// <para><see cref="HoldTheFirstWrite"/> lets everything after the first through, which is
    /// right for the tests that only decide the order of the first two bodies. A test about what
    /// the shell says <em>between</em> two writes needs the second one held as well, so that the
    /// window it asserts in is one the test opened rather than one it hoped for.</para>
    /// </summary>
    private sealed class GatedWrites
    {
        private readonly Dictionary<int, TaskCompletionSource> _reached = [];
        private readonly Dictionary<int, TaskCompletionSource> _release = [];
        private readonly Lock _gate = new();

        private readonly Dictionary<int, string> _ids = [];

        public Task Reached(int nth) => Slot(_reached, nth).Task;

        /// <summary>Which character's row the nth write was addressed to.</summary>
        public void WrittenAt(int nth, string id)
        {
            lock (_gate) _ids[nth] = id;
        }

        public string? IdOf(int nth)
        {
            lock (_gate) return _ids.GetValueOrDefault(nth);
        }

        /// <summary>How many writes have reached the wire.</summary>
        public int SoFar
        {
            get { lock (_gate) return _ids.Count; }
        }

        public void Let(int nth) => Slot(_release, nth).TrySetResult();

        public async Task Arrive(int nth)
        {
            Slot(_reached, nth).TrySetResult();
            await Slot(_release, nth).Task;
        }

        private TaskCompletionSource Slot(Dictionary<int, TaskCompletionSource> of, int nth)
        {
            lock (_gate)
            {
                if (!of.TryGetValue(nth, out var slot))
                {
                    slot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    of[nth] = slot;
                }

                return slot;
            }
        }
    }

    private static GatedWrites HoldEachWrite(RenderContext ctx)
    {
        var gates = new GatedWrites();
        var seen = 0;

        ctx.Api.BeforeStoringCharacter = id =>
        {
            var nth = Interlocked.Increment(ref seen);
            gates.WrittenAt(nth, id);

            return gates.Arrive(nth);
        };

        return gates;
    }

    /// <summary>
    /// One edit, made the way <c>Finishing.razor</c> makes it: the field is written and the bell is
    /// rung. Anything that arranged the write by calling the store directly would be testing a path
    /// the app does not take, which is the fault <c>RenderContext</c>'s own mirror exists for.
    /// </summary>
    private static Task Type(IRenderedComponent<MainLayout> layout, RenderContext ctx, string name) =>
        layout.InvokeAsync(() =>
        {
            ctx.Session.Sheet.Name = name;
            ctx.Session.NotifyChanged();
        });

    /// <summary>One named character on the account, put there rather than typed.</summary>
    private static async Task Store(ApiCharacterStore account, string id, string name)
    {
        var sheet = new CharacterSheet { Name = name, SelectedTierId = "street_level" };

        Assert.Equal(SaveOutcome.Saved, await account.SaveAsync(id, name, sheet, SheetMode.Hero));
    }

    /// <summary>
    /// The name the <em>account</em> holds, read back through the app's own store — which is what
    /// a second browser signing in would be handed.
    /// </summary>
    private static async Task<string?> NameHeld(RenderContext ctx) =>
        (await ctx.Services.GetRequiredService<ApiCharacterStore>().LoadAsync())?.Sheet.Name;
}
