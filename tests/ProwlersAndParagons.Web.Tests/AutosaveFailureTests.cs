using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The lost update by the other door: a write that <em>threw</em> must not take the edit that
/// overtook it with it.</b>
///
/// <para><see cref="Autosave"/> exists so that the last edit is never dropped — a keystroke made
/// while a write is open is coalesced into the next one. The coalescing is a single flag, and
/// <see cref="Autosave.Pump"/>'s catch clears it: before this file, one throw lowered
/// <c>_writing</c> and <c>_again</c> together and returned, so the pending edit had nobody left to
/// write it. That is exactly the defect the class was built to end, reached through the failure
/// path instead of the success path, and the class's own doc comment conceded the case without
/// closing it.</para>
///
/// <para><b>Why it is worth closing even though <see cref="ICharacterStore.SaveAsync"/> says it
/// does not throw.</b> "Does not throw" is a contract, not a mechanism: <c>ApiCharacterStore</c>
/// catches the seven exception types <c>IsUnreachable</c> names and <c>SavedCharacters</c> catches
/// the storage ones, and anything outside those lists — an <c>ArgumentNullException</c>, a
/// serialiser that meets a type it was not told about, whatever the next store to implement this
/// interface forgets — walks straight out. The cost of being wrong about that is somebody's last
/// keystroke, silently, with the shell saying nothing; the cost of the fix is four lines.</para>
///
/// <para><b>Driven against <see cref="Autosave"/> itself, with a store that really throws.</b> The
/// app's own store cannot be made to throw through <see cref="FakeApi"/> — a handler that throws
/// surfaces as an <c>HttpRequestException</c>, which <c>ApiCharacterStore</c> catches by design and
/// turns into <see cref="SaveOutcome.NotSaved"/>. Reaching the path through the wire would
/// therefore be testing the catch that already exists rather than the one that does not, so the
/// seam is the interface.</para>
/// </summary>
public sealed class AutosaveFailureTests
{
    /// <summary>
    /// <b>A write that throws does not take the keystroke behind it with it.</b>
    ///
    /// <para>The first write is held open, a second edit is made behind it, and the first is then
    /// released into an exception. Whatever the class does with the exception, the account has to
    /// finish holding the second edit: it is the newest thing the reader typed and nothing else is
    /// ever going to send it.</para>
    /// </summary>
    [Fact]
    public async Task AnEditMadeWhileAFailingWriteWasOpenIsStillWritten()
    {
        var session = ASession();
        var store = new ThrowsOnceThenStores();

        new Autosave(session, store).Start();

        session.Sheet.Name = "First";
        session.NotifyChanged();

        await store.Reached(1).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        // The control on the act: the write really is open at the store, so the edit below really
        // is the one that has to be coalesced rather than one that starts a write of its own.
        Assert.Equal(1, Volatile.Read(ref store.Started));

        session.Sheet.Name = "Second";
        session.NotifyChanged();

        Assert.Equal(1, Volatile.Read(ref store.Started));

        store.Let(1);

        await AssertEventually(
            () => Volatile.Read(ref store.Started) == 2,
            "the write that threw cleared the pending flag and nobody sent \"Second\" — the edit "
            + "made while that write was open is lost, which is the defect Autosave exists to end "
            + "reached through the failure path");

        store.Let(2);

        await AssertEventually(
            () => store.LastWritten == "Second",
            "the write that followed the throw did not carry the edit that raised the flag");
    }

    /// <summary>
    /// <b>And a throw with nothing behind it stops, rather than spinning.</b>
    ///
    /// <para>The counterpart to the test above, and the reason the re-pump is conditioned on the
    /// pending flag rather than unconditional: a store that throws every time would otherwise be
    /// asked for ever. The bound is the number of edits, not the number of failures.</para>
    /// </summary>
    [Fact]
    public async Task AThrowWithNoPendingEditDoesNotStartAnotherWrite()
    {
        var session = ASession();
        var store = new AlwaysThrows();

        new Autosave(session, store).Start();

        session.Sheet.Name = "Only";
        session.NotifyChanged();

        await store.Reached.Task.WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        store.Release.SetResult();

        // Long enough for a runaway loop to make itself obvious: this store throws synchronously
        // after the first, so an unconditional re-pump would be into the thousands by here.
        await Task.Delay(200, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref store.Started));

        // And the class is not wedged either: the next edit still gets a write of its own, which
        // is the half a lowered `_writing` flag buys and the half a raised one would lose.
        session.Sheet.Name = "Another";
        session.NotifyChanged();

        await AssertEventually(
            () => Volatile.Read(ref store.Started) == 2,
            "a throw left the write-open flag raised, so every later edit coalesces into a write "
            + "that is never going to start");
    }

    /// <summary>
    /// <b>The write started after a throw is still a write at the wire, and nothing may start a
    /// second one beside it.</b>
    ///
    /// <para><b>This is the hole the first fix left, found by breaking it.</b> Recovering the
    /// pending edit needs two things and only one of them is visible from the test above: the fresh
    /// pump has to start, <em>and</em> the write-open flag has to stay raised while it runs.
    /// Lowering it and starting the pump anyway passed every test in both projects — 5,834 of them
    /// — while leaving the class with two writes in flight from one tab, which is the whole defect
    /// <see cref="Autosave"/> exists to end, now reachable through any store that throws once.</para>
    ///
    /// <para>So the interleaving is carried one step further than the test above: the throw, the
    /// re-pump, and then an edit made while <em>that</em> write is at the wire. It has to be
    /// coalesced like any other, and go out in the write after it.</para>
    /// </summary>
    [Fact]
    public async Task NoWriteStartsBesideTheOneThatFollowsAThrow()
    {
        var session = ASession();
        var store = new ThrowsOnceThenStores();

        new Autosave(session, store).Start();

        session.Sheet.Name = "First";
        session.NotifyChanged();

        await store.Reached(1).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        session.Sheet.Name = "Second";
        session.NotifyChanged();

        store.Let(1);

        // The write that carries "Second" — started by the catch, and open at the wire now.
        await store.Reached(2).WaitAsync(
            TimeSpan.FromSeconds(20), Xunit.TestContext.Current.CancellationToken);

        session.Sheet.Name = "Third";
        session.NotifyChanged();

        // Bounded rather than instantaneous, for the reason the order tests give: a second write
        // that was going to be dispatched has already been dispatched by now.
        await Task.Delay(200, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(2, Volatile.Read(ref store.Started));

        store.Let(2);

        await AssertEventually(
            () => store.LastWritten == "Third",
            "the edit made while the post-throw write was open never went out, so recovering from "
            + "a throw drops the keystroke behind its own recovery");
    }

    // ── The fixture ───────────────────────────────────────────────────────────────────────────

    private static async Task AssertEventually(Func<bool> holds, string why)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (!holds())
        {
            Assert.True(DateTime.UtcNow < deadline, why);
            await Task.Delay(20, Xunit.TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A session over the real rules, with no store and no container behind it.</summary>
    private static CharacterSession ASession()
    {
        var rules = RulesRepository.FromBasePath(RepoRoot());
        var costs = new CostCalculator(rules);
        var derived = new DerivedStatsCalculator(rules);

        return new CharacterSession(
            rules, costs, derived,
            new CharacterValidator(rules, costs, derived),
            new ProConApplicability(rules),
            new SourceGrouping(rules));
    }

    /// <summary>
    /// Throws the first time and stores every time after that, with each of the first two calls
    /// held open at a gate of its own so a test can decide what happens behind them.
    ///
    /// <para><b>Two gates rather than one</b>, because the interleaving that matters runs past the
    /// throw: the write started <em>by</em> the catch has to be held too, or an edit made while it
    /// is open is an edit the test only hoped was concurrent.</para>
    /// </summary>
    private sealed class ThrowsOnceThenStores : ICharacterStore
    {
        private const int Gated = 2;

        private readonly Dictionary<int, TaskCompletionSource> _reached = [];
        private readonly Dictionary<int, TaskCompletionSource> _release = [];
        private readonly Lock _gate = new();

        public int Started;

        public string? LastWritten { get; private set; }

        public Task Reached(int nth) => Slot(_reached, nth).Task;

        public void Let(int nth) => Slot(_release, nth).TrySetResult();

        public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
        {
            ArgumentNullException.ThrowIfNull(sheet);

            var nth = Interlocked.Increment(ref Started);

            if (nth <= Gated)
            {
                Slot(_reached, nth).TrySetResult();
                await Slot(_release, nth).Task;
            }

            if (nth == 1) throw new InvalidTimeZoneException("the store fell over");

            LastWritten = sheet.Name;
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

        public Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
            Task.FromResult<(CharacterSheet, SheetMode)?>(null);

        public Task ClearAsync() => Task.CompletedTask;
    }

    /// <summary>Throws every time; the first call is held open so the test can time its edits.</summary>
    private sealed class AlwaysThrows : ICharacterStore
    {
        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Started;

        public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
        {
            if (Interlocked.Increment(ref Started) == 1)
            {
                Reached.SetResult();
                await Release.Task;
            }

            throw new InvalidTimeZoneException("the store fell over");
        }

        public Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
            Task.FromResult<(CharacterSheet, SheetMode)?>(null);

        public Task ClearAsync() => Task.CompletedTask;
    }

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
}
