using System.Text.Json;
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
/// This class owns one thing: getting those bytes in and out of the browser. The account's
/// store owns getting the same bytes in and out of a server, and the two share the envelope so
/// a character saved on a laptop cannot restore wrongly on a phone.</para>
///
/// <para><b>Nothing here may throw.</b> A browser that refuses local storage is the same case
/// as no character at all, and restoring happens before the first render — so an exception is
/// not a lost character but an app that does not start.</para>
/// </summary>
public sealed class CharacterStore : ICharacterStore
{
    private const string StorageKey = "pp.character.v1";

    private readonly IJSRuntime _js;
    private readonly IIdentitySource _who;
    private readonly StoredCharacter _payload;

    public CharacterStore(
        IJSRuntime js, CostCalculator costs, CharacterValidator validator, IIdentitySource who)
    {
        _js = js;
        _who = who;
        _payload = new StoredCharacter(costs, validator);
    }

    /// <summary>
    /// Which slot in local storage belongs to whoever is here.
    ///
    /// <para><b>The anonymous visitor keeps the historical key exactly.</b> That key is the one
    /// this store has been writing since it was written, so introducing identities does not
    /// orphan a single saved character. Suffixing it "for consistency" would empty every
    /// returning visitor's browser, silently, and look exactly like storage having been
    /// cleared.</para>
    ///
    /// <para>An account's characters land beside it rather than on top of it, so signing in on a
    /// shared browser cannot overwrite what the anonymous visitor was building — and signing out
    /// cannot have eaten it.</para>
    /// </summary>
    private static string KeyFor(Identity who) =>
        who.Key == Identity.Anonymous.Key ? StorageKey : $"{StorageKey}.{who.Key}";

    /// <summary>Writes the character to local storage. Failure is not worth reporting.</summary>
    public async Task SaveAsync(CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            await _js.InvokeVoidAsync(
                "ppStore.save", KeyFor(await _who.CurrentAsync()), StoredCharacter.Write(sheet, mode));
        }
        catch (Exception e) when (IsStorageFailure(e)) { }
    }

    /// <summary>The stored character, or null if there is none this build can trust.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync() =>
        await LoadAsync(await _who.CurrentAsync());

    /// <summary>
    /// The character in one particular slot, whoever is here now.
    ///
    /// <para>Exists for one caller: the sign-in page, which offers to keep what the browser was
    /// holding <em>before</em> anybody signed in. By then the ordinary overload would answer for
    /// the account's own browser-side slot, which is empty and is not what was meant.</para>
    /// </summary>
    internal async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync(Identity who)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("ppStore.load", KeyFor(who));
            if (string.IsNullOrWhiteSpace(json)) return null;

            if (Read(json) is { } restored) return restored;

            // Storage this build cannot use is removed rather than left. Left in place it is
            // re-read and re-rejected on every visit, and if it ever gets past a guard the
            // failure repeats forever with no way out from inside the app.
            //
            // The slot cleared is the slot read, which is why this takes the identity rather
            // than asking again: an unreadable anonymous character would otherwise be answered
            // by emptying whichever slot the *current* visitor happens to own.
            await ClearAsync(who);
            return null;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>Forgets the stored character. What "Start a new character" actually does.</summary>
    public async Task ClearAsync() => await ClearAsync(await _who.CurrentAsync());

    private async Task ClearAsync(Identity who)
    {
        try { await _js.InvokeVoidAsync("ppStore.clear", KeyFor(who)); }
        catch (Exception e) when (IsStorageFailure(e)) { }
    }

    /// <summary>
    /// Reads a stored payload. Internal so the tests can feed it malformed storage — that
    /// handling is the part most worth testing, because a saved character that stops the app
    /// booting is far worse than one that is forgotten.
    /// </summary>
    internal (CharacterSheet Sheet, SheetMode Mode)? Read(string json) => _payload.Read(json);

    /// <summary>
    /// Everything that can go wrong between here and the browser's storage. Every one means the
    /// same thing: there is no saved character, carry on without one.
    ///
    /// <para>Named rather than a bare <c>catch</c> so the list is reviewable, and wider than the
    /// obvious three because the alternative — an unobserved exception out of a
    /// fire-and-forget save — stops persistence silently and tells nobody.</para>
    /// </summary>
    private static bool IsStorageFailure(Exception e) =>
        e is JsonException                  // a character this build cannot even write down
          or JSException                    // the browser refused, or ppStore is missing
          or InvalidOperationException      // interop unavailable
          or ObjectDisposedException        // the host is going away
          or TaskCanceledException          // ditto, mid-call
          or ArgumentException              // an id or key the payload invented
          or OverflowException              // a number no build can hold
          or NotSupportedException;         // a type the serializer cannot handle
}
