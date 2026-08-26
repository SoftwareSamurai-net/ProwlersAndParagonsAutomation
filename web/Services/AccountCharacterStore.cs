using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Puts the character wherever it belongs: on the server for somebody signed in, in this
/// browser for everybody else.
///
/// <para><b>The anonymous slot tracks what this browser is holding, not what belongs to
/// nobody.</b> Signing in never rewrites it by itself — an account's characters live in their
/// own per-identity slot instead, and the sign-in page still offers to copy the browser's
/// character <em>up</em> rather than doing it automatically, exactly as before. But
/// <see cref="OpenAsync"/> is a signed-in reader looking at one of their account's characters,
/// and once that happens this browser is holding it: the anonymous slot is overwritten to
/// match, replacing whatever was there. That is why signing out has to empty it —
/// see <see cref="ClearAnonymousAsync"/> — or a shared machine would leave an ex-user's account
/// character sitting there for whoever opens this browser next, under no account at all. This
/// reverses what this class used to say: the two stores are no longer untouched by each
/// other, only kept from colliding by choosing one at a time.</para>
///
/// <para><b>It is a store rather than a branch in <c>Program.cs</c></b> because the choice has
/// to be made per call, not once at startup: identity changes when somebody signs in, and the
/// next save has to land in the new place without anything being re-registered.</para>
///
/// <para><b>Nothing here may throw</b>, which it inherits from both halves — and from asking
/// who is here, which is itself a network call on a path that runs before the first render.</para>
/// </summary>
public sealed class AccountCharacterStore : ICharacterStore
{
    private readonly IIdentitySource _who;
    private readonly CharacterStore _inThisBrowser;
    private readonly ApiCharacterStore _inTheAccount;
    private readonly SavedCharacters _local;

    public AccountCharacterStore(
        IIdentitySource who, CharacterStore inThisBrowser, ApiCharacterStore inTheAccount,
        SavedCharacters local)
    {
        _who = who;
        _inThisBrowser = inThisBrowser;
        _inTheAccount = inTheAccount;

        // The browser's plural store, for the list and for the current-character pointer. The
        // pointer is local for *both* sides: which of your characters is on screen is a fact about
        // this tab, and syncing it would mean opening a laptop and having a phone decide what you
        // are looking at.
        _local = local;
    }

    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode) =>
        await (await ChosenAsync()).SaveAsync(sheet, mode);

    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
        await (await ChosenAsync()).LoadAsync();

    public async Task ClearAsync() => await (await ChosenAsync()).ClearAsync();

    /// <summary>
    /// The character this browser was holding before anybody signed in.
    ///
    /// <para>Read from the anonymous slot specifically rather than from "the local store",
    /// because by the time this is asked the local store would answer for the account's own
    /// browser-side slot instead. It is what the sign-in page offers to keep.</para>
    /// </summary>
    public Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAnonymousAsync() =>
        _inThisBrowser.LoadAsync(Identity.Anonymous);

    /// <summary>Whether the account already has a character, so nothing is offered over it.</summary>
    public Task<bool> AccountHasCharacterAsync() => _inTheAccount.HasCharacterAsync();

    /// <summary>
    /// Copy the browser's anonymous character up into the account.
    ///
    /// <para>Explicit, and only ever offered when the account has none. A merge that happened by
    /// itself is the shape that eats somebody's work — and there is no way to tell, from here,
    /// whether the anonymous character is theirs at all.</para>
    /// </summary>
    public async Task<bool> KeepAnonymousCharacterAsync()
    {
        if (await LoadAnonymousAsync() is not { } theirs) return false;

        await _inTheAccount.SaveAsync(theirs.Sheet, theirs.Mode);

        return true;
    }

    // ── The list, for the manager ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Every character on this side of the wire, and the cap if there is one.
    ///
    /// <para><b>An account's list comes from the server; everybody else's comes from this
    /// browser.</b> Same choice the single-character methods make, for the same reason — and it is
    /// why the manager asks this rather than either store: a page that picked a source itself would
    /// be a second place deciding where a character lives.</para>
    ///
    /// <para><b>A visitor with no account has no cap</b>, so the limit is null for them. That is
    /// not "unlimited" being asserted anywhere; local storage simply has no limit worth enforcing,
    /// and the browser is not the thing the cap exists to bound.</para>
    /// </summary>
    public async Task<AccountCharacters> ListAsync() =>
        (await _who.CurrentAsync()).IsSignedIn
            ? await _inTheAccount.ListAsync()
            : new AccountCharacters(null, await _local.ListAsync());

    /// <summary>Open one of them. Null when it is not there, or not one this build can read.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> OpenAsync(string id)
    {
        var who = await _who.CurrentAsync();

        var opened = who.IsSignedIn
            ? await _inTheAccount.LoadAsync(id)
            : await _local.LoadAsync(id);

        // The pointer moves only if there was something to move to. Switching to a character that
        // could not be read would leave the app pointed at nothing, and the next autosave would
        // write the character on screen over an id the visitor did not choose.
        if (opened is not null) await _local.SetCurrentAsync(id);

        // A signed-in reader opening one of their account's characters is now what this browser
        // is holding — see the class remarks — so it replaces whatever the anonymous slot had.
        // Nothing to do when nobody is signed in: the anonymous slot already is where this went.
        if (who.IsSignedIn && opened is not null)
            await _inThisBrowser.SaveAsync(Identity.Anonymous, opened.Value.Sheet, opened.Value.Mode);

        return opened;
    }

    /// <summary>
    /// Empties the browser's anonymous slot.
    ///
    /// <para><b>Signing out calls this</b> — see <c>SignIn.razor</c>'s <c>SignOut</c>. It cannot
    /// live inside <see cref="Accounts.SignOutAsync"/> itself: <see cref="Accounts"/> is the
    /// <see cref="IIdentitySource"/> this store and the browser store beneath it are built on, so
    /// having it depend on either store back would be a cycle. A page-level call is the seam that
    /// is left.</para>
    ///
    /// <para><b>Unconditional, on purpose.</b> This empties whatever the slot holds, not only a
    /// copy <see cref="OpenAsync"/> left there — the owner was shown the alternative of clearing
    /// only what this store itself wrote, and chose the simpler, safer rule: a shared machine must
    /// not hand the next visitor anything that was on screen under somebody else's account, and
    /// nothing here can tell that copy apart from the visitor's own anonymous work once it has
    /// been sitting in the slot for a while.</para>
    /// </summary>
    public Task ClearAnonymousAsync() => _inThisBrowser.ClearAsync(Identity.Anonymous);

    /// <summary>
    /// Read one of them without opening it — same two sources as <see cref="OpenAsync"/> and
    /// none of its side effect.
    ///
    /// <para><b>It exists because the manager has to weigh a character it is not looking at.</b>
    /// A row's confirmation asks whether there is anything to lose, and for the row that is open
    /// the session can answer from the sheet in memory; for every other row the only copy is in
    /// the store, and the question cannot be answered without reading it. <see cref="OpenAsync"/>
    /// would answer it and move the current-character pointer on the way past — so asking "is
    /// this worth asking about" would switch the app to the character somebody is about to throw
    /// away, and the next autosave would write the sheet on screen over it.</para>
    ///
    /// <para>Null means the same as everywhere else here: there is no character at that id this
    /// build can read. A caller deciding whether to protect one should treat that as unknown
    /// rather than as nothing, the way <see cref="AccountCharacters.IsFull"/> treats a cap it
    /// could not ask about.</para>
    /// </summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> ReadAsync(string id) =>
        (await _who.CurrentAsync()).IsSignedIn
            ? await _inTheAccount.LoadAsync(id)
            : await _local.LoadAsync(id);

    /// <summary>
    /// Throw one away, wherever it lives.
    ///
    /// <para><b>Not the one that is open unless it is asked for by id.</b> The single-character
    /// <see cref="ClearAsync()"/> is what "start a new character" calls, and this is a row in a
    /// list; conflating them is how a manager deletes the wrong thing.</para>
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        if ((await _who.CurrentAsync()).IsSignedIn) await _inTheAccount.DeleteAsync(id);

        // Always locally too: a signed-in visitor's browser may still hold a copy under the same
        // id from before they signed in, and leaving it would resurrect the character on the next
        // visit while they were signed out.
        await _local.DeleteAsync(id);
    }

    /// <summary>
    /// Put a character back under the id it had, wherever that id lives.
    ///
    /// <para><b>The other half of a deleted row's undo</b> — see <see cref="DiscardedCharacter"/>.
    /// Deliberately not <see cref="SaveAsync(CharacterSheet, SheetMode)"/>, which writes to
    /// whichever character is <em>open</em>: this names an id, and the point is to restore a row
    /// that was never the open one.</para>
    ///
    /// <para><b>It answers whether the write landed, which every other method here does not.</b>
    /// An autosave that fails is not worth interrupting somebody over; an undo that silently did
    /// nothing is the worst possible outcome, because the reader believes their character is
    /// back. The account cap is the refusal that actually happens: discard a row from a full
    /// account, build something in its place, and there is no room to put the old one back.</para>
    /// </summary>
    public async Task<bool> RestoreAsync(string id, string label, CharacterSheet sheet, SheetMode mode)
    {
        if ((await _who.CurrentAsync()).IsSignedIn)
            return await _inTheAccount.SaveAsync(id, label, sheet, mode) == SaveOutcome.Saved;

        // The browser's own store has no cap and no failure worth reporting — see
        // SavedCharacters, where a storage refusal is the same case as no character at all. It
        // hands back the id it wrote, so a mismatch is the only thing that could mean "not done".
        return await _local.SaveAsync(id, label, sheet, mode) == id;
    }

    /// <summary>Which character the app currently has open, whoever is here.</summary>
    public Task<string> CurrentIdAsync() => _local.CurrentIdAsync();

    private async Task<ICharacterStore> ChosenAsync() =>
        (await _who.CurrentAsync()).IsSignedIn ? _inTheAccount : _inThisBrowser;
}
