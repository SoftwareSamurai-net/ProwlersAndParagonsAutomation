namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The write-through that follows every edit, with <b>one write at the wire at a time</b> and
/// everything typed behind it coalesced into the next one.
///
/// <para><b>This is a class rather than three lines in <c>Program.cs</c> because those three lines
/// lost somebody's keystrokes.</b> The subscription used to be
/// <c>session.Changed += () =&gt; _ = SaveThenAnnounce(session.Version)</c> — one fire-and-forget
/// write per change, with nothing ordering it against the one before. Against local storage that
/// is harmless: <c>localStorage.setItem</c> is synchronous, so the second write cannot start until
/// the first has finished. Against an account it is a lost update. Two <c>PUT</c>s really are in
/// flight together, <c>worker/characters.js</c> stores whichever body reaches it last, and it
/// writes down nothing that would let it tell an older body from a newer one — so the older one
/// wins whenever it happens to commit second, and the shell says "Saved" over the result.</para>
///
/// <para><b>It is not a hypothetical, and a name is where it bites.</b> A name is typed one
/// character at a time and each character is a change, so a burst of writes in flight together is
/// the ordinary case rather than a corner. The e2e <c>ACCOUNT_SAVE</c> check caught it: a second
/// browser, signed in as the account that had just built the character, was handed
/// "Account Bound H" for a sheet the first browser had finished typing as "Account Bound Hero" and
/// had been told was saved.</para>
///
/// <para><b>Why serialising here rather than a version the server compares.</b> The server-side
/// compare-and-swap is the right shape where two <em>parties</em> are writing — it is what
/// <c>pending_version</c> does for a campaign submission, where a GM and a player each hold a
/// snapshot. Here both writes come from one tab, one after the other, and the newer one is
/// unambiguously the one to keep; there is no decision for a server to make. Sending a counter
/// would also need one that survives a reload and a second tab, because
/// <see cref="CharacterSession.Version"/> counts changes since <em>this</em> page loaded and starts
/// again at zero in the next tab — a server refusing everything the second tab writes would be a
/// worse defect than the one being fixed. And 409 already means something else on this route: it is
/// how the account's cap is reported, which <see cref="SaveOutcome.AccountIsFull"/> reads.</para>
///
/// <para><b>What it costs and what it buys.</b> Seventeen keystrokes stop being seventeen requests
/// and become two: the one already open, and one carrying everything typed since. The last edit is
/// never dropped — the pump re-reads the live sheet after each write returns, so whatever the
/// reader typed while a write was open goes out in the next one.</para>
///
/// <para><b>The lock is not decoration.</b> Blazor WebAssembly runs this on one thread, but a
/// continuation after an <c>await</c> in a test host does not, and the window between deciding a
/// write is the last one and lowering the flag is exactly where a change would be swallowed.</para>
/// </summary>
public sealed class Autosave
{
    private readonly CharacterSession _session;
    private readonly ICharacterStore _store;

    private readonly Lock _gate = new();

    /// <summary>Whether a write is open at the wire right now. Guarded by <see cref="_gate"/>.</summary>
    private bool _writing;

    /// <summary>
    /// Whether the character changed while that write was open. Guarded by <see cref="_gate"/>.
    ///
    /// <para><b>A flag and not a queue</b>, deliberately: the sheet is one mutable object, so a
    /// queue of edits would be a queue of pointers to the same character and every entry would
    /// serialise identically. What is pending is "there is something newer than what just went",
    /// and the answer to it is one more write of whatever is on screen when it starts.</para>
    /// </summary>
    private bool _again;

    public Autosave(CharacterSession session, ICharacterStore store)
    {
        _session = session;
        _store = store;
    }

    /// <summary>
    /// Subscribe to the character's change bell. Called once, by whoever boots the app.
    ///
    /// <para>Kept off the constructor so that constructing this is not a side effect on the
    /// session, and so a test can build one and drive <see cref="Changed"/> by hand.</para>
    /// </summary>
    public void Start() => _session.Changed += Changed;

    /// <summary>
    /// One edit happened. Starts a write, or notes that the write already open has been overtaken.
    /// </summary>
    public void Changed()
    {
        lock (_gate)
        {
            if (_writing)
            {
                _again = true;
                return;
            }

            _writing = true;
        }

        _ = Pump();
    }

    /// <summary>
    /// Write, announce, and go round again if anything changed while that was happening.
    ///
    /// <para><b>The version is read immediately before the write and not after it</b>, so what is
    /// announced can only ever be older than the bytes that landed, never newer. Understating is
    /// safe — <see cref="CharacterSession.Saved"/> is weighed against the character's current
    /// version, so an understated report simply does not say "Saved" yet, and the write that
    /// follows says it truthfully. Overstating is the lie this class exists to end.</para>
    ///
    /// <para><b>The catch is what stops one failure becoming permanent silence.</b>
    /// <see cref="ICharacterStore.SaveAsync"/> does not throw — see its own doc comment — but this
    /// is a fire-and-forget continuation, and if one ever did, a raised flag with nobody left to
    /// lower it would coalesce every future edit into a write that is never going to start. The
    /// throw is not swallowed: it goes on to the same nowhere it went before this class existed.</para>
    ///
    /// <para><b>And the edit behind a failed write is picked up rather than dropped with it.</b>
    /// Clearing both flags together was a lost update by the other door: the keystroke that raised
    /// <see cref="_again"/> had nobody left to send it, which is precisely what this class exists
    /// to prevent, reached through the failure path instead of the success path. So a pending edit
    /// keeps <see cref="_writing"/> raised and starts a fresh pump — the throw still goes where it
    /// went, and the edit still goes to the store.</para>
    ///
    /// <para><b>Conditioned on the flag, and that is the bound.</b> An unconditional re-pump
    /// against a store that throws every time is a hot loop; conditioned, the writes are bounded by
    /// the edits made rather than by the failures suffered, and a throw with nothing pending lowers
    /// the flag and stops. <b>This is not a retry policy and must not become one</b> — the write
    /// that failed is not tried again, because the reader's next keystroke is the only thing that
    /// says there is anything left to send.</para>
    /// </summary>
    private async Task Pump()
    {
        while (true)
        {
            try
            {
                var version = _session.Version;

                await _store.SaveAsync(_session.Sheet, _session.Mode);

                _session.NotifySaved(version);
            }
            catch
            {
                bool pending;

                lock (_gate)
                {
                    pending = _again;
                    _again = false;

                    // Stays raised when something is pending, because the pump started below *is*
                    // that write: lowering it would let the next edit start a second one beside
                    // it, which is the race this class exists to end.
                    _writing = pending;
                }

                if (pending) _ = Pump();

                throw;
            }

            lock (_gate)
            {
                if (!_again)
                {
                    _writing = false;
                    return;
                }

                _again = false;
            }
        }
    }
}
