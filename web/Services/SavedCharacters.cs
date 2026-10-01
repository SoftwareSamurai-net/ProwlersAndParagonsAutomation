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
/// <param name="CampaignId">
/// The campaign this character belongs to, or null for one that belongs to none.
///
/// <para><b>Duplicated out of the payload on purpose, and it was the first field here that
/// was.</b> The whole reason this record exists is that a list of many characters must not have to
/// deserialize and cost every one of them to draw a row — so "which of my characters are in this
/// game" would otherwise be exactly what that remark refuses, once per row. It is supplied by the
/// client on both sides, exactly as <c>Label</c> is; the server never derives it, never validates
/// it and never joins it to anything, because the server does not know what a character is. See
/// <c>docs/CHARACTERS-API.md</c>.</para>
///
/// <para><b>An index written before this field existed still lists, and the reason is measured
/// rather than assumed.</b> This paragraph first claimed the <c>= null</c> was what made that
/// work; it is not. Removing the default was tried, and every test stayed green: for a positional
/// record, <c>System.Text.Json</c> supplies the parameter's own default for a key that is absent
/// from the JSON, and <c>default(string?)</c> is null either way. The default is here for C#
/// callers, and the compatibility is the serializer's behaviour — which is exactly why it is
/// pinned by a test that reads a literal three-field index rather than by a note. <b>What would
/// really break it is a <c>JsonRequired</c> or a <c>required</c> member on this parameter</b>,
/// which was tried too and does break it: every entry in every returning visitor's index fails to
/// deserialize and their list of characters silently empties.</para>
/// </param>
/// <param name="Kind">
/// Which palette this character is built in — <see cref="SheetMode"/>, lower-cased — or null for
/// a row written before this field existed.
///
/// <para><b>Three more duplicates out of the payload, and the paragraphs above apply to all of
/// them unchanged.</b> They were added because a roster of thirty could say a name and a time and
/// nothing else: two characters called Emir Hughes were indistinguishable, and "show me the
/// Standard-tier Villains" was a question the list could not be asked. Every one is supplied by
/// the client on both sides, stored verbatim and never derived — see <c>docs/CHARACTERS-API.md</c>
/// and migration <c>0008</c>.</para>
///
/// <para><b>Defaulted, and the defaults are load-bearing for the same measured reason
/// <c>CampaignId</c>'s is.</b> An index or an account row written before <c>0008</c> has none of
/// the three, and must still list — the serializer supplies each parameter's own default for an
/// absent key, and a <c>required</c> member here would silently empty every returning visitor's
/// list. <c>RosterTests</c> pins it by reading a checked-in four-field index rather than by
/// trusting this note.</para>
/// </param>
/// <param name="TierId">
/// The tier this character is built to, or null for one with no tier chosen and for a row written
/// before this field existed.
///
/// <para><b>An id out of <c>data/rules/tiers.json</c>, resolved to a name only where there is a
/// <see cref="RulesRepository"/> to resolve it.</b> The name is not stored: it is the rules data's
/// to change, and a copy of it in an index would be the stale one.</para>
/// </param>
/// <param name="Spent">
/// Hero Points as the engine priced them, or null when it declined to price this character.
///
/// <para><b>Null is an answer, not a gap.</b> The engine throws rather than guessing on an
/// incomplete selection — a variable-cost Power with no variant — so a half-built character has no
/// figure, and the honest row is its name and nothing else. That is the same rule the front door
/// follows, and it is why this is <c>int?</c> rather than an <c>int</c> defaulting to zero: zero
/// is a real spend and "unknown" is not it.</para>
/// </param>
/// <param name="VariantOf">
/// The id of the character this one is a version of, or null for a root — item 21's slice one,
/// the roster's own copy of <see cref="Engine.CharacterVariant.OfCharacterId"/>.
///
/// <para><b>A fourth duplicate out of the payload, on the same bargain <see cref="CampaignId"/>
/// struck.</b> A roster drawing the tree <see cref="Web.Services.CharacterVariants.Group"/> builds
/// must not cost a payload read per row any more than the game or the tier does, so this rides
/// beside them — supplied by the client, stored verbatim, never derived. Null on a row written
/// before this field existed, which correctly means "a root", exactly what
/// <see cref="Engine.CharacterSheet.Variant"/> already means by null.</para>
/// </param>
/// <param name="VariantKind">
/// What kind of version it is — <see cref="Engine.CharacterVariant.Later"/>,
/// <see cref="Engine.CharacterVariant.AsSeenBy"/> or <see cref="Engine.CharacterVariant.AlternateForm"/>
/// — or null alongside <paramref name="VariantOf"/>. The two travel together; see
/// <see cref="IndexFieldsFor"/>.
/// </param>
public sealed record SavedCharacterSummary(
    string Id,
    string Label,
    long UpdatedAt,
    string? CampaignId = null,
    string? Kind = null,
    string? TierId = null,
    int? Spent = null,
    string? VariantOf = null,
    string? VariantKind = null);

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
/// <em>exists</em>. A character that exists in storage but is missing from the index is
/// <em>unreachable</em>, because every other character is discovered <em>through</em> the
/// index (there is no enumeration to find a stray key some other way) — the legacy slot is
/// the one exception, checked directly every time.</para>
///
/// <para><b>The paragraph above used to say the orphan direction "can only ever be the legacy
/// slot", and that stopped being true when the autosave started adding entries</b> — it was true
/// while nothing but an explicit labelled save ever added one. It is corrected rather than
/// reordered around: an adversarial review proposed writing the index first so that a half-failed
/// pair leaves an entry naming nothing rather than a character nothing names, and that was traced
/// through and rejected. <see cref="WriteIndexAsync"/> swallows its own failures, so an index write
/// that fails does not abort the pair and both orders end identically; the one case where they
/// differ favours payload-first, because a payload write that throws has then touched nothing at
/// all. The next autosave writes both again regardless, which is what actually repairs it.</para>
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

    /// <summary>
    /// The one anonymous slot an account character may be copied into, and the only one signing
    /// out is allowed to empty.
    ///
    /// <para><b>A reserved id rather than "whatever is open", which is the bug this replaced.</b>
    /// The first version wrote the copy through the anonymous <em>current</em> pointer and cleared
    /// the same way — so opening an account character wrote over whichever local character the
    /// reader happened to have open, and signing out deleted it. That is a real, named, deliberately
    /// saved character, not a scratch slot, and it was destroyed with no confirmation and no undo.
    /// Two independent adversarial reviews demonstrated it, and a third defect fell out of the same
    /// cause: because the clear was unconditional, a reader who signed in and out without ever
    /// opening an account character lost their anonymous draft too.</para>
    ///
    /// <para>Keeping the copy in a slot of its own makes all three go away by construction: nothing
    /// of the reader's is ever written over, the clear knows exactly what it is allowed to remove,
    /// and "is this slot a copy" is answerable at boot — which is what closes the leak for somebody
    /// who closes the tab instead of pressing the button.</para>
    /// </summary>
    public const string AccountCopyId = "account-copy";

    private readonly IJSRuntime _js;
    private readonly IIdentitySource _who;
    private readonly StoredCharacter _payload;

    /// <summary>Kept as well as handed to <see cref="_payload"/>: an index entry carries the
    /// character's spend now, and that is this class's own question rather than the reader's.
    /// See <see cref="IndexFieldsFor"/>.</summary>
    private readonly CostCalculator _costs;

    public SavedCharacters(IJSRuntime js, CostCalculator costs, CharacterValidator validator, IIdentitySource who)
    {
        _js = js;
        _who = who;
        _costs = costs;
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

    /// <summary>
    /// The name to list a character under. Trimmed, and never empty — an unnamed character is an
    /// ordinary state rather than a fault, and a blank row reads as broken rather than as unnamed.
    ///
    /// <para>Internal and shared with <see cref="ApiCharacterStore"/> because both autosave paths
    /// need it and a character must not be listed under one name in this browser and another on
    /// the account. There were two copies of this before, which is how they would have drifted.</para>
    /// </summary>
    internal static string LabelFor(CharacterSheet sheet) =>
        string.IsNullOrWhiteSpace(sheet.Name) ? "Unnamed character" : sheet.Name.Trim();

    /// <summary>
    /// The five things besides its name and its campaign that an index records about a character,
    /// so a row can say what it is without the payload being read.
    ///
    /// <para><b>One spelling, shared with <see cref="ApiCharacterStore"/> for the reason
    /// <see cref="LabelFor"/> is.</b> Both autosave paths write these, and a character described
    /// one way in this browser and another on the account is a list that disagrees with itself
    /// depending on who is signed in. There were two copies of <c>LabelFor</c> once; this does not
    /// repeat that.</para>
    ///
    /// <para><b>The spend is asked for through <see cref="CharacterSession.TryCost"/> and may come
    /// back null.</b> The engine throws rather than guessing on an incomplete selection, and an
    /// autosave fires on every change — including the change that makes a sheet unpriceable. A
    /// throw here would take down a write that has nothing to do with the figure; null is the
    /// answer, and a row with no figure is the honest drawing of it.</para>
    ///
    /// <para><b>The last two are item 21's pair, read straight off <see cref="CharacterSheet.Variant"/>
    /// with no engine call behind them</b> — unlike the spend, there is nothing here that can
    /// throw: a null <c>Variant</c> answers two nulls, and a set one answers its own two fields
    /// verbatim. Both travel together, exactly as they sit on the one record that names them.</para>
    /// </summary>
    /// <summary>The stored word for a Villain — the one the server acts on when one is approved.</summary>
    public const string VillainKind = "villain";

    /// <summary>
    /// The stored word for a palette: <see cref="VillainKind"/> or <c>hero</c>. One spelling for
    /// the index and for a submission, because the server hands a Villain over on the strength of
    /// it and a second spelling is a Villain it would not recognise.
    /// </summary>
    public static string KindOf(SheetMode mode) => mode == SheetMode.Villain ? VillainKind : "hero";

    internal static (string Kind, string? TierId, int? Spent, string? VariantOf, string? VariantKind)
        IndexFieldsFor(CharacterSheet sheet, SheetMode mode, CostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(costs);

        // Lower-cased, because it is a stored key rather than a word on a screen — the roster
        // capitalises it for a reader, and a stored "Hero" would be a presentation decision
        // written into a column that outlives it.
        var kind = KindOf(mode);

        return (
            kind, sheet.SelectedTierId, CharacterSession.TryCost(() => costs.TotalCost(sheet)),
            sheet.Variant?.OfCharacterId, sheet.Variant?.Kind);
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

            // The legacy slot predates the index entirely, so it is checked directly rather than
            // discovered through the index the way every other character is. It reaches the index
            // on its first worth-keeping autosave now, which is where the real label comes from;
            // this is the fallback for a payload written before that, by a browser that has not
            // been back since.
            if (result.TrueForAll(e => e.Id != LegacyId))
            {
                var raw = await _js.InvokeAsync<string?>("ppStore.load", prefix);

                // **Read rather than merely counted, and that is a change.** It used to list any
                // payload at all, so a visitor who had done nothing but switch the palette — which
                // autosaves an otherwise empty sheet — was shown a row called "Unnamed character"
                // for a character that did not exist. Nothing else in this class lists an empty
                // sheet; this was the one place that did, and it is the one place where the cost of
                // reading a payload to draw a row is worth paying, because it is exactly one.
                if (!string.IsNullOrWhiteSpace(raw)
                    && _payload.Read(raw) is { } slot
                    && CharacterSession.IsWorthKeeping(slot.Sheet))
                {
                    // The payload is already open here, so this row carries the same fields an
                    // index entry does rather than being the one row in the list that cannot say
                    // what it is. It is the one place reading a payload to draw a row is worth
                    // paying for, and having paid, there is nothing to be saved by using less of it.
                    var (kind, tierId, spent, variantOf, variantKind) =
                        IndexFieldsFor(slot.Sheet, slot.Mode, _costs);

                    result.Add(new SavedCharacterSummary(
                        LegacyId, LabelFor(slot.Sheet), 0, slot.Sheet.CampaignId, kind, tierId, spent,
                        variantOf, variantKind));
                }
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
    /// learns its id without a second round trip — and whether anything was written at all.
    ///
    /// <para><b>The second half of that answer was missing and the miss was silent.</b> This
    /// used to return the id alone, and it returned it whether the write landed or was swallowed
    /// by a browser that refuses storage. Both callers weighing the result compared it against the
    /// id they passed in — which, for a non-null id, is the same string either way, so the check
    /// was dead code that read like a guard. One of them was the undo behind a discarded row and
    /// the other was "keep this character and start another": both would report success over a
    /// character that had gone nowhere. An adversarial review demonstrated the second.</para>
    ///
    /// <para>Unbounded here on purpose — the cap is the account's, not this browser's; see
    /// <c>docs/CHARACTERS-API.md</c>. Nothing in this class refuses a save for having "too
    /// many" characters.</para>
    /// </summary>
    public async Task<(string Id, bool Stored)> SaveAsync(
        string? id, string label, CharacterSheet sheet, SheetMode mode)
    {
        var resolvedId = id ?? NewId();

        try
        {
            var prefix = PrefixFor(await _who.CurrentAsync());
            var resolvedLabel = string.IsNullOrWhiteSpace(label) ? "Unnamed character" : label.Trim();
            var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await _js.InvokeVoidAsync(
                "ppStore.save", PayloadKeyFor(prefix, resolvedId), StoredCharacter.Write(sheet, mode));

            var (kind, tierId, spent, variantOf, variantKind) = IndexFieldsFor(sheet, mode, _costs);

            var index = await ReadIndexAsync(prefix);
            index.RemoveAll(e => e.Id == resolvedId);
            index.Add(new SavedCharacterSummary(
                resolvedId, resolvedLabel, updatedAt, sheet.CampaignId, kind, tierId, spent,
                variantOf, variantKind));
            await WriteIndexAsync(prefix, index);
        }
        // The id is still handed back — a caller that minted one wants it either way — but the
        // second half of the answer is now false, which is the whole point of there being one.
        catch (Exception e) when (IsStorageFailure(e)) { return (resolvedId, false); }

        return (resolvedId, true);
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
    /// Writes one payload at an id named outright, for the given identity, touching neither the
    /// current-character pointer nor the index.
    ///
    /// <para><b>By id rather than through "whatever is open", which is the whole point.</b> The
    /// account copy has a reserved slot of its own
    /// (<see cref="AccountCopyId"/>); routing its write through the pointer meant moving the
    /// pointer first and trusting the very next read to see it, which is action at a distance — and
    /// it is not hypothetical, because the first version of the copy-down did exactly that and put
    /// the payload under the previously-open character's id. Naming the id removes the ordering
    /// question entirely.</para>
    ///
    /// <para><b>The index is deliberately untouched</b>, so the copy never appears in the reader's
    /// own list of characters. It is not one of theirs.</para>
    /// </summary>
    internal async Task SavePayloadAsync(Identity who, string id, CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            await _js.InvokeVoidAsync(
                "ppStore.save", PayloadKeyFor(PrefixFor(who), id), StoredCharacter.Write(sheet, mode));
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* same rule as every other write here */ }
    }

    /// <summary>Removes one payload at an id named outright. The counterpart to <see cref="SavePayloadAsync"/>.</summary>
    internal async Task ClearPayloadAsync(Identity who, string id)
    {
        try { await _js.InvokeVoidAsync("ppStore.clear", PayloadKeyFor(PrefixFor(who), id)); }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing left to try */ }
    }

    /// <summary>
    /// Writes to whichever character is currently open, for the given identity — what
    /// <see cref="CharacterStore.SaveAsync"/> calls on every change.
    ///
    /// <para><b>It puts the open character into the index, and that was the missing half of the
    /// whole feature.</b> This used to bump an existing entry's <c>UpdatedAt</c> and do nothing at
    /// all when there was no entry — so a character only ever reached the list by way of
    /// <see cref="SaveAsync(string?, string, CharacterSheet, SheetMode)"/>, which nothing outside
    /// this assembly called. The list, the switcher and both undo buffers all read the index, so
    /// every one of them worked perfectly against a list that could never have more than the
    /// legacy slot in it. Adding the entry here is what makes a second character exist: the
    /// account's store has always worked this way (its <c>PUT</c> creates the row on the first
    /// autosave), and the two sides disagreeing is what hid this.</para>
    ///
    /// <para><b>The label follows the sheet's own name</b>, for the same reason and on the same
    /// terms as <c>ApiCharacterStore.SaveAsync</c>: this is the autosave path, there is nobody to
    /// ask, and a list that still shows the name a character had when it was first written down is
    /// a list that lies about a character somebody has since named. This reverses the older note
    /// that "an ordinary edit is not a rename" — that was written when the only way into the index
    /// was an explicit labelled save, and it made the label a thing you could set and never change.
    /// </para>
    ///
    /// <para><b>Nothing empty is ever listed</b> — <see cref="CharacterSession.IsWorthKeeping"/>,
    /// the same predicate the account's store applies before its own write. The payload is still
    /// written either way, because emptying the current slot is how starting over leaves it; what
    /// is guarded is only the row a person sees. Without this, minting a fresh id and opening it
    /// would create a listed, empty character the instant the palette was switched — exactly the
    /// defect that guard was added to the account side to fix.</para>
    ///
    /// <para><b>The legacy slot is indexed too, once it is worth listing.</b> It was left out
    /// while nothing could name it, and <see cref="ListAsync"/> synthesised a row reading "Unnamed
    /// character" for it — which is what somebody's imported, named character was being called.
    /// The synthesised row stays as the fallback for a slot holding something this predicate does
    /// not count; an index entry simply wins over it.</para>
    /// </summary>
    internal async Task SaveCurrentAsync(Identity who, CharacterSheet sheet, SheetMode mode)
    {
        try
        {
            var prefix = PrefixFor(who);
            var id = await CurrentIdAsync(who);

            // **The payload first, then the index — and this order was questioned and kept.** An
            // adversarial review argued the reverse: that an index write failing after a successful
            // payload write leaves a character nothing names, which is unreachable because every
            // non-legacy character is discovered *through* the index. The mechanism is real and the
            // conclusion does not follow, because `WriteIndexAsync` swallows its own failures — so
            // an index write that fails does not abort this method, and both orders end in exactly
            // the same state: a payload with no entry. The orders differ in only one case, and it
            // favours this one. If the *payload* write throws, this order has not yet touched the
            // index and nothing is written at all; the reverse order would already have added an
            // entry naming a character that is not there, which `ListAsync` then has to drop on
            // every future visit.
            await _js.InvokeVoidAsync("ppStore.save", PayloadKeyFor(prefix, id), StoredCharacter.Write(sheet, mode));

            if (!CharacterSession.IsWorthKeeping(sheet)) return;

            var (kind, tierId, spent, variantOf, variantKind) = IndexFieldsFor(sheet, mode, _costs);

            var index = await ReadIndexAsync(prefix);
            var entry = new SavedCharacterSummary(
                id, LabelFor(sheet), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sheet.CampaignId, kind, tierId, spent, variantOf, variantKind);

            var i = index.FindIndex(e => e.Id == id);
            if (i >= 0) index[i] = entry; else index.Add(entry);

            await WriteIndexAsync(prefix, index);
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
