using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Puts the character wherever it belongs: on the server for somebody signed in, in this
/// browser for everybody else.
///
/// <para><b>The anonymous slot is never touched by an account.</b> Signing in on a shared
/// browser must not overwrite what somebody was building, and signing out must not have eaten
/// it — so the two stores are two stores, and this only chooses. Copying between them happens
/// once, by hand, on the sign-in page, and never as a side effect.</para>
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

        return opened;
    }

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

    /// <summary>Which character the app currently has open, whoever is here.</summary>
    public Task<string> CurrentIdAsync() => _local.CurrentIdAsync();

    private async Task<ICharacterStore> ChosenAsync() =>
        (await _who.CurrentAsync()).IsSignedIn ? _inTheAccount : _inThisBrowser;
}
