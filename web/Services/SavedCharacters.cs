using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One row a manager page can draw, and nothing more. The payload itself is fetched
/// separately, by <see cref="SavedCharacters.LoadAsync(string)"/> — a list of many
/// characters should not have to deserialize and cost every one of them just to print a
/// label and a timestamp.
/// </summary>
/// <param name="Id"><c>c_</c> followed by 22 URL-safe characters, or
/// <see cref="SavedCharacters.LegacyId"/> for the one character this browser could hold
/// before there was a list.</param>
/// <param name="Label">What the player called it. Opaque here exactly as it is on the
/// server — see <c>docs/CHARACTERS-API.md</c>.</param>
/// <param name="UpdatedAt">Unix milliseconds. Used to sort "most recently touched first"
/// and nothing else.</param>
public sealed record SavedCharacterSummary(string Id, string Label, long UpdatedAt);

/// <summary>
/// Many characters, kept in this browser's local storage.
///
/// <para><b>The key scheme, and the trap in it.</b> Before this class existed,
/// <see cref="CharacterStore"/> wrote exactly one payload per identity, at the bare
/// <c>pp.character.v1</c> for an anonymous visitor or <c>pp.character.v1.{key}</c> for an
/// account. That key has to keep meaning what it always did — suffixing it "for
/// consistency" would empty every returning visitor's browser, silently, and look exactly
/// like storage having been cleared. So the bare key is never moved: it stays exactly where
/// it was and is treated as one entry in the list, under the reserved id
/// <see cref="LegacyId"/>. A character created after this class shipped gets its own key,
/// <c>{prefix}.{id}</c>, where <c>prefix</c> is the same per-identity string
/// <see cref="CharacterStore"/> always used.</para>
///
/// <para><b>An index, not enumeration.</b> Real <c>localStorage</c> can be enumerated with a
/// new JS call, and the fake used in tests cannot be enumerated at all — it is a flat
/// dictionary with no "list the keys" operation, because nothing before this needed one.
/// An index key (<c>{prefix}.index</c>, a JSON list of <see cref="SavedCharacterSummary"/>)
/// avoids needing either: it is one more thing this class already knows how to read and
/// write, rather than a new interop surface to guard against failing and a new thing the
/// fake has to model faithfully. The cost is the one thing enumeration would have given for
/// free — see the next paragraph.</para>
///
/// <para><b>The index and the storage can disagree, and each direction is handled
/// differently on purpose.</b> An index entry naming a character that is not actually
/// there — deleted by hand, or by a version of this class that failed midway — is dropped
/// from the list rather than shown broken; storage is the source of truth for what
/// <em>exists</em>. A character that exists in storage but is missing from the index can
/// only ever be the legacy slot, because every other character is discovered <em>through</em>
/// the index (there is no enumeration to find a stray key some other way) — and that
/// direction is exactly what the legacy-slot handling above already covers: it is checked
/// directly, every time, rather than through the index at all.</para>
///
/// <para><b>Nothing here may throw.</b> Every one of the methods below is asked from a
/// page, or from <see cref="CharacterStore"/> on the path to the very first render, and a
/// browser that refuses storage, a hand-edited index, an index naming a character that is
/// not there, and a character whose payload the engine cannot price are all the same case:
/// there is no character (or no list, or no index) here — carry on.</para>
/// </summary>
public sealed class SavedCharacters
{
    /// <summary>The key this store has been writing since before there was a list.</summary>
    private const string StorageKey = "pp.character.v1";

    /// <summary>
    /// The id of the one character this browser could hold before this class existed. Never
    /// generated, never collides with a minted id (those always start with <c>c_</c>), and
    /// its payload lives at the bare identity prefix rather than at <c>{prefix}.{id}</c> —
    /// see <see cref="PayloadKeyFor"/>.
    /// </summary>
    public const string LegacyId = "legacy";

    private readonly IJSRuntime _js;
    private readonly IIdentitySource _who;
    private readonly StoredCharacter _payload;

    public SavedCharacters(IJSRuntime js, CostCalculator costs, CharacterValidator validator, IIdentitySource who)
    {
        _js = js;
        _who = who;
        _payload = new StoredCharacter(costs, validator);
    }

    /// <summary>
    /// Which identity's namespace a key lives in — exactly the string
    /// <see cref="CharacterStore"/> used to write to directly. Anonymous keeps the bare,
    /// historical key; an account gets its own beside it rather than on top of it.
    /// </summary>
    private static string PrefixFor(Identity who) =>
        who.Key == Identity.Anonymous.Key ? StorageKey : $"{StorageKey}.{who.Key}";

    private static string IndexKeyFor(string prefix) => $"{prefix}.index";

    private static string CurrentKeyFor(string prefix) => $"{prefix}.current";

    /// <summary>
    /// Where one character's payload lives. The legacy slot is the bare prefix itself —
    /// never moved, so a returning visitor finds it exactly where it always was; every other
    /// character gets a key of its own beside it.
    /// </summary>
    private static string PayloadKeyFor(string prefix, string id) =>
        id == LegacyId ? prefix : $"{prefix}.{id}";

    /// <summary><c>c_</c> plus 22 URL-safe characters — 16 random bytes, base64url without
    /// padding. The server validates exactly this shape; see <c>docs/CHARACTERS-API.md</c>.
    ///
    /// <para>Internal rather than private because the account's store mints one too: an account
    /// whose current-id pointer still says <see cref="LegacyId"/> has to adopt a real id before
    /// its first save, since <c>legacy</c> is this browser's private name for a slot and not a
    /// key the server will accept. One implementation, so the two cannot mint different shapes
    /// and only one of them get refused.</para>
    /// </summary>
    internal static string NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"c_{text}";
    }

    // ── Reading and writing the index ───────────────────────────────────────────────

    private async Task<List<SavedCharacterSummary>> ReadIndexAsync(string prefix)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("ppStore.load", IndexKeyFor(prefix));
            if (string.IsNullOrWhiteSpace(raw)) return [];

            return JsonSerializer.Deserialize<List<SavedCharacterSummary>>(raw) ?? [];
        }
        // A hand-edited or corrupted index is not a crash: it is an index that names
        // nothing, same as an index that was never written. The characters it forgot are
        // not lost — their payloads are untouched — only unreachable until relabelled,
        // which is the cost of an index over enumeration stated in the class remarks.
        catch (Exception e) when (IsStorageFailure(e)) { return []; }
    }

    private async Task WriteIndexAsync(string prefix, List<SavedCharacterSummary> index)
    {
        try { await _js.InvokeVoidAsync("ppStore.save", IndexKeyFor(prefix), JsonSerializer.Serialize(index)); }
        catch (Exception e) when (IsStorageFailure(e)) { /* the index is a cache; nothing lost but the list */ }
    }

    // ── The manager surface ──────────────────────────────────────────────────────────

    /// <summary>
    /// Every character this browser holds for whoever is here now, most recently touched
    /// first. Reconciles the index against storage rather than trusting either alone — see
    /// the class remarks for which side wins in which direction.
    /// </summary>
    public async Task<IReadOnlyList<SavedCharacterSummary>> ListAsync()
    {
        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            var index = await ReadIndexAsync(prefix);

            var result = new List<SavedCharacterSummary>();
            foreach (var entry in index)
            {
                var raw = await _js.InvokeAsync<string?>("ppStore.load", PayloadKeyFor(prefix, entry.Id));
                if (!string.IsNullOrWhiteSpace(raw)) result.Add(entry);
                // else: the index names a character that is not there. Storage wins —
                // dropped from the list rather than shown broken.
            }

            // The legacy slot is never in the index until somebody has explicitly saved or
            // relabelled it through this class — it predates the index entirely. So it is
            // checked directly, every visit, rather than discovered through the index the
            // way every other character is.
            if (result.TrueForAll(e => e.Id != LegacyId))
            {
                var raw = await _js.InvokeAsync<string?>("ppStore.load", prefix);
                if (!string.IsNullOrWhiteSpace(raw))
                    result.Add(new SavedCharacterSummary(LegacyId, "Unnamed character", 0));
            }

            result.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
            return result;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return Array.Empty<SavedCharacterSummary>(); }
    }

    /// <summary>One character's inputs, or null if there is none this build can trust at that id.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync(string id)
    {
        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            var raw = await _js.InvokeAsync<string?>("ppStore.load", PayloadKeyFor(prefix, id));
            return _payload.Read(raw);
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>
    /// Create or replace a character. Passing <paramref name="id"/> as null mints a fresh
    /// one; passing an existing id overwrites that character's payload and label in place.
    /// Returns the id that was actually written, so a caller that just created a character
    /// learns its id without a second round trip.
    ///
    /// <para>Unbounded here on purpose — the cap is the account's, not this browser's; see
    /// <c>docs/CHARACTERS-API.md</c>. Nothing in this class refuses a save for having "too
    /// many" characters.</para>
    /// </summary>
    public async Task<string> SaveAsync(string? id, string label, CharacterSheet sheet, SheetMode mode)
    {
        var resolvedId = id ?? NewId();

        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            var resolvedLabel = string.IsNullOrWhiteSpace(label) ? "Unnamed character" : label.Trim();
            var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await _js.InvokeVoidAsync(
                "ppStore.save", PayloadKeyFor(prefix, resolvedId), StoredCharacter.Write(sheet, mode));

            var index = await ReadIndexAsync(prefix);
            index.RemoveAll(e => e.Id == resolvedId);
            index.Add(new SavedCharacterSummary(resolvedId, resolvedLabel, updatedAt));
            await WriteIndexAsync(prefix, index);
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing persisted; the id is still handed back */ }

        return resolvedId;
    }

    /// <summary>Forgets one character. Forgetting the one that is open falls back to the legacy slot.</summary>
    public async Task DeleteAsync(string id)
    {
        try
        {
            var who = await _who.CurrentAsync();
            var prefix = PrefixFor(who);

            await _js.InvokeVoidAsync("ppStore.clear", PayloadKeyFor(prefix, id));

            var index = await ReadIndexAsync(prefix);
            if (index.RemoveAll(e => e.Id == id) > 0) await WriteIndexAsync(prefix, index);

            if (await CurrentIdAsync(who) == id) await SetCurrentAsync(who, LegacyId);
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing left to remove */ }
    }

    // ── Which one is open ────────────────────────────────────────────────────────────

    /// <summary>
    /// The id of the character currently open, for whoever is here now. Defaults to
    /// <see cref="LegacyId"/> — nothing has ever switched, so the one that is open is the
    /// one this browser has always held.
    /// </summary>
    public async Task<string> CurrentIdAsync() => await CurrentIdAsync(await _who.CurrentAsync());

    private async Task<string> CurrentIdAsync(Identity who)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("ppStore.load", CurrentKeyFor(PrefixFor(who)));
            return string.IsNullOrWhiteSpace(raw) ? LegacyId : raw;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return LegacyId; }
    }

    /// <summary>Switches which character is open. Does not load or save one — only points at it.</summary>
    public async Task SetCurrentAsync(string id) => await SetCurrentAsync(await _who.CurrentAsync(), id);

    private async Task SetCurrentAsync(Identity who, string id)
    {
        try { await _js.InvokeVoidAsync("ppStore.save", CurrentKeyFor(PrefixFor(who)), id); }
        catch (Exception e) when (IsStorageFailure(e)) { /* the switch did not take; still no character lost */ }
    }

    // ── The single-slot surface CharacterStore autosaves through ────────────────────

    /// <summary>
    /// Writes to whichever character is currently open, for the given identity — what
    /// <see cref="CharacterStore.SaveAsync"/> calls on every change. Does not touch the
    /// label: an ordinary edit is not a rename. If the open character is already in the
    /// index (it has been saved or relabelled through the manager surface at least once)
    /// its <c>UpdatedAt</c> is bumped so "most recently touched first" reflects ordinary
    /// play and not only explicit saves; the legacy slot, which starts outside the index by
    /// definition, is left alone until somebody names it.
    /// </summary>
    internal async Task SaveCurrentAsync(Identity who, CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            var prefix = PrefixFor(who);
            var id = await CurrentIdAsync(who);

            await _js.InvokeVoidAsync("ppStore.save", PayloadKeyFor(prefix, id), StoredCharacter.Write(sheet, mode));

            if (id != LegacyId)
            {
                var index = await ReadIndexAsync(prefix);
                var i = index.FindIndex(e => e.Id == id);
                if (i >= 0)
                {
                    index[i] = index[i] with { UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                    await WriteIndexAsync(prefix, index);
                }
            }
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* an autosave failing is not worth reporting */ }
    }

    /// <summary>The currently open character, for the given identity. What <see cref="CharacterStore.LoadAsync()"/> reads.</summary>
    internal async Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadCurrentAsync(Identity who)
    {
        try
        {
            var prefix = PrefixFor(who);
            var id = await CurrentIdAsync(who);
            var raw = await _js.InvokeAsync<string?>("ppStore.load", PayloadKeyFor(prefix, id));

            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (_payload.Read(raw) is { } restored) return restored;

            // Unreadable rather than merely absent: removed rather than left, so it is not
            // re-read and re-rejected on every future visit. See CharacterStore's remarks.
            await ClearCurrentAsync(who);
            return null;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>Forgets the currently open character, for the given identity. What <see cref="CharacterStore.ClearAsync()"/> does.</summary>
    internal async Task ClearCurrentAsync(Identity who)
    {
        try
        {
            var prefix = PrefixFor(who);
            var id = await CurrentIdAsync(who);
            await _js.InvokeVoidAsync("ppStore.clear", PayloadKeyFor(prefix, id));
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing left to try */ }
    }

    /// <summary>
    /// Everything that can go wrong between here and the browser's storage. Every one means
    /// the same thing: there is no character (or no list, or no index) here — carry on.
    ///
    /// <para>Shared with nothing else in the assembly on purpose — <see cref="CharacterStore"/>
    /// no longer touches storage directly at all, so there is exactly one copy of this list
    /// rather than two that could drift.</para>
    /// </summary>
    private static bool IsStorageFailure(Exception e) =>
        e is JsonException                  // a character, list or index this build cannot even write down
          or JSException                    // the browser refused, or ppStore is missing
          or InvalidOperationException      // interop unavailable
          or ObjectDisposedException        // the host is going away
          or TaskCanceledException          // ditto, mid-call
          or ArgumentException              // an id or key the payload invented
          or OverflowException              // a number no build can hold
          or NotSupportedException;         // a type the serializer cannot handle
}
