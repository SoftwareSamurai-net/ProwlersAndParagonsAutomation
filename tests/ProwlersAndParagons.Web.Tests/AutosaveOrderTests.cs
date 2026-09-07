using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// The name the <em>account</em> holds, read back through the app's own store — which is what
    /// a second browser signing in would be handed.
    /// </summary>
    private static async Task<string?> NameHeld(RenderContext ctx) =>
        (await ctx.Services.GetRequiredService<ApiCharacterStore>().LoadAsync())?.Sheet.Name;
}
