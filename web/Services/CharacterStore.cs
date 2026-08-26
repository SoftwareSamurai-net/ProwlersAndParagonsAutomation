using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Keeps the character in the browser's local storage, so a refresh, a bookmark or a shared
/// deep link does not throw it away.
///
/// <para>It used to. The sheet lived in a scoped <see cref="CharacterSession"/> and nowhere
/// else, so reloading the page — or opening a link someone sent you, which <c>_redirects</c>
/// deliberately serves with a 200 precisely so links <em>can</em> be shared — silently dropped
/// twenty minutes of work and landed on "Choose a tier first".</para>
///
/// <para><b>What a stored character is lives in <see cref="StoredCharacter"/>, not here.</b>
/// This class owns one thing: which of the browser's <em>several</em> saved characters is the
/// one currently open. <see cref="SavedCharacters"/> owns the storage underneath — the index,
/// the id scheme, the legacy slot — and this class is now a thin single-character view onto
/// it, kept because every existing caller (<see cref="AccountCharacterStore"/>,
/// <c>Program.cs</c>'s boot restore, the autosave on every <see cref="CharacterSession"/>
/// change) only ever needs "the one that is open", never the whole list.</para>
///
/// <para><b>A manager switching which character is open needs no change here.</b> Switching
/// is <see cref="SavedCharacters.SetCurrentAsync(string)"/>, and this class always asks
/// <see cref="SavedCharacters"/> which id is current before reading or writing — so the next
/// autosave, and the next boot, land on whichever character was switched to.</para>
///
/// <para><b>Nothing here may throw.</b> A browser that refuses local storage is the same case
/// as no character at all, and restoring happens before the first render — so an exception is
/// not a lost character but an app that does not start. <see cref="SavedCharacters"/> already
/// guarantees this for every method it exposes, so nothing here needs its own guard on top.</para>
/// </summary>
public sealed class CharacterStore : ICharacterStore
{
    private readonly IIdentitySource _who;
    private readonly SavedCharacters _saved;

    public CharacterStore(
        IJSRuntime js, CostCalculator costs, CharacterValidator validator, IIdentitySource who)
    {
        _who = who;
        _saved = new SavedCharacters(js, costs, validator, who);
    }

    /// <summary>Writes to whichever character is currently open. Failure is not worth reporting.</summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode) =>
        await _saved.SaveCurrentAsync(await _who.CurrentAsync(), sheet, mode);

    /// <summary>The currently open character, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
        await LoadAsync(await _who.CurrentAsync());

    /// <summary>
    /// The currently open character in one particular identity's slot, whoever is here now.
    ///
    /// <para>Exists for one caller: the sign-in page, which offers to keep what the browser was
    /// holding <em>before</em> anybody signed in. By then the ordinary overload would answer for
    /// the account's own browser-side slot, which is empty and is not what was meant.</para>
    /// </summary>
    internal async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync(Identity who) =>
        await _saved.LoadCurrentAsync(who);

    /// <summary>Forgets the currently open character. What "Start a new character" actually does.</summary>
    public async Task ClearAsync() => await ClearAsync(await _who.CurrentAsync());

    /// <summary>
    /// Forgets one particular identity's currently open character, whoever is here now.
    ///
    /// <para>Exists for one caller: <see cref="AccountCharacterStore.ClearAnonymousAsync"/>,
    /// which signing out uses to empty the anonymous slot — see its remarks for why.</para>
    /// </summary>
    private async Task ClearAsync(Identity who) => await _saved.ClearCurrentAsync(who);
}
