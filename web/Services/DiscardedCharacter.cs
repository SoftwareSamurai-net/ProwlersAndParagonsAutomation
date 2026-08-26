using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The one saved character most recently thrown away from the list, held long enough to put back.
///
/// <para><b>This is the half of undo <see cref="CharacterSession"/> cannot do.</b> That buffer holds
/// the sheet that was <em>on screen</em> — discarding it, loading a sample, importing a file,
/// opening a recording — and it is enough for all four because every one of them replaces the
/// character the session is holding. Discarding a <em>different</em> row is not that: the session
/// has never seen that character, its only copy was the one in the store, and
/// <c>Store.DeleteAsync(id)</c> destroys it. The commit that removed the manager's confirmations
/// said discarding another row "never asked either, because switching away from it already left it
/// saved under its own id and this cannot touch that copy" — the first half is true of
/// <c>Open</c> and the second half is not true of <c>Delete</c>, which is exactly what it
/// deletes. So the row a reader is least likely to be weighing carefully had the least behind
/// it.</para>
///
/// <para><b>Single-level and self-closing on the same rule the session uses.</b> It is armed at
/// <see cref="CharacterSession.Version"/> and requires an exact match, so the first edit to
/// anything closes the window rather than leaving an offer that would sit there indefinitely. One
/// rule in the app, two buffers — and because deleting a background row does not itself raise
/// <see cref="CharacterSession.Changed"/>, the two can never be armed by the same click.</para>
///
/// <para><b>The payload is JSON, never the live sheet.</b> Same reason
/// <see cref="CharacterSession"/> writes its buffer out: holding the instance lets a later edit
/// rewrite what is meant to be a snapshot, which is the replay's own recorded bug reached another
/// way.</para>
///
/// <para><b>Nothing here may throw.</b> It is read on every render of the shell — the banner asks
/// whether there is an offer to draw — so an exception is a blank page rather than a lost undo.</para>
/// </summary>
public sealed class DiscardedCharacter
{
    private readonly AccountCharacterStore _store;
    private readonly CharacterSession _session;
    private readonly IIdentitySource _who;

    public DiscardedCharacter(
        AccountCharacterStore store, CharacterSession session, IIdentitySource who)
    {
        _store = store;
        _session = session;
        _who = who;
    }

    private string? _id;
    private string? _payload;
    private string _label = "";
    private SheetMode _mode;
    private int _armedAtVersion;

    /// <summary>
    /// Whose list the discarded row belonged to.
    ///
    /// <para><b>Captured, and compared before restoring.</b> The store chooses the account or this
    /// browser per call, from whoever is here <em>now</em> — so an offer left standing across a
    /// sign-out would write somebody's account character into the anonymous slot, or the reverse.
    /// Comparing the key rather than listening for a change is the same decision
    /// <see cref="RulebookReader"/> made, for the same reason: a guarantee that depends on an event
    /// being raised is one somebody can remove by editing another file.</para>
    /// </summary>
    private string? _identity;

    /// <summary>
    /// Remember a character that is about to be deleted, so <see cref="UndoAsync"/> can put it
    /// back under the same id.
    ///
    /// <para>Nothing is kept for a character with nothing on it —
    /// <see cref="CharacterSession.IsWorthKeeping"/>, the same predicate the session's own buffer
    /// uses. An empty row has nothing to bring back and offering to would be ceremony.</para>
    /// </summary>
    public async Task RememberAsync(string id, string label, CharacterSheet sheet, SheetMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (!CharacterSession.IsWorthKeeping(sheet)) { Forget(); return; }

        _id = id;
        _payload = CharacterSheetJson.Write(sheet);
        _label = string.IsNullOrWhiteSpace(label) ? "That character" : label.Trim();
        _mode = mode;
        _armedAtVersion = _session.Version;
        _identity = (await _who.CurrentAsync()).Key;
    }

    /// <summary>Drops the offer without acting on it.</summary>
    public void Forget()
    {
        _id = null;
        _payload = null;
        _identity = null;
    }

    /// <summary>Whether <see cref="UndoAsync"/> would do anything right now.</summary>
    public bool CanUndo => _payload is not null && _session.Version == _armedAtVersion;

    /// <summary>What it would bring back, for the message beside the offer.</summary>
    public string UndoLabel => _label;

    /// <summary>
    /// Writes it back under the id it had. Answers false when there was nothing to put back, when
    /// whoever is here now is not who discarded it, or when the store refused.
    ///
    /// <para><b>A refusal is reported rather than swallowed, and the account cap is why.</b> Ch.6
    /// aside, a reader whose account was full may have discarded one row to make room and built
    /// something in its place; putting the old one back would be the character over the cap, and
    /// the server answers <see cref="SaveOutcome.AccountIsFull"/>. Saying so is the difference
    /// between an undo that did not work and an undo that looks like it did.</para>
    ///
    /// <para>One level: the buffer is cleared either way, so a second call does nothing. There is
    /// no redo.</para>
    /// </summary>
    public async Task<bool> UndoAsync()
    {
        if (!CanUndo || _id is not { } id || _payload is not { } payload) return false;

        var who = (await _who.CurrentAsync()).Key;
        var sheet = CharacterSheetJson.Read(payload, strict: false);
        var label = _label;
        var mode = _mode;
        var owner = _identity;

        Forget();

        if (sheet is null || owner is null || owner != who) return false;

        return await _store.RestoreAsync(id, label, sheet, mode);
    }
}
