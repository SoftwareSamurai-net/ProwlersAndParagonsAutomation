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

    public AccountCharacterStore(
        IIdentitySource who, CharacterStore inThisBrowser, ApiCharacterStore inTheAccount)
    {
        _who = who;
        _inThisBrowser = inThisBrowser;
        _inTheAccount = inTheAccount;
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

    private async Task<ICharacterStore> ChosenAsync() =>
        (await _who.CurrentAsync()).IsSignedIn ? _inTheAccount : _inThisBrowser;
}
