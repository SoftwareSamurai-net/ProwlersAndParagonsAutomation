using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The browser-side plural store: many characters in one browser, an index that can drift
/// from what is actually stored, a legacy slot that predates the whole idea of a list, and a
/// notion of "the one that is open" that <see cref="CharacterStore"/> autosaves through.
///
/// <para>Everything here shares <see cref="FakeLocalStorage"/> with <see cref="CharacterStore"/>
/// and <see cref="IdentityStorageTests"/> on purpose: the historical key
/// (<c>pp.character.v1</c>) and the per-identity suffix are the same string in both files, and
/// a test built against a different fake would not catch the two disagreeing.</para>
/// </summary>
public sealed class SavedCharactersTests
{
    /// <summary>The key this store has been writing since before there was a list.</summary>
    private const string HistoricalKey = "pp.character.v1";

    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly CharacterValidator Validator =
        new(Rules, Costs, new DerivedStatsCalculator(Rules));

    private sealed class FixedIdentity(Identity who) : IIdentitySource
    {
        public ValueTask<Identity> CurrentAsync() => ValueTask.FromResult(who);
    }

    private static SavedCharacters FreshSaved(FakeLocalStorage storage, IIdentitySource? who = null) =>
        new(storage, Costs, Validator, who ?? new LocalIdentity());

    private static CharacterStore FreshStore(FakeLocalStorage storage, IIdentitySource? who = null) =>
        new(storage, Costs, Validator, who ?? new LocalIdentity());

    /// <summary>A payload this build's version gate accepts, wrapping the smallest usable sheet.</summary>
    private static string MinimalPayload(string name) =>
        $"{{\"Version\":1,\"Mode\":0,\"Sheet\":{{\"SelectedTierId\":\"standard\",\"Name\":\"{name}\"}}}}";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    // ── The legacy slot ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The one thing this whole slice would break if it got wrong. A visitor who saved a
    /// character before the manager existed must see it in the list on their very first
    /// visit afterwards — not an empty manager next to a character that is still there.
    ///
    /// <para><b>Under its own name</b>, which it was not. This row used to read "Unnamed
    /// character" whatever the character was actually called, because it was synthesised from the
    /// bare fact that a payload existed rather than from the payload. So somebody's imported,
    /// named character — the owner's report — was listed as unnamed in the one row the list could
    /// draw. It costs one read of one payload, which is the trade this slot alone is worth.</para>
    /// </summary>
    [Fact]
    public async Task AVisitorWithOnlyTheHistoricalKeySeesItListedUnderItsOwnName()
    {
        var storage = new FakeLocalStorage();
        storage.Poke(HistoricalKey, MinimalPayload("Built before the manager"));

        var list = await FreshSaved(storage).ListAsync();

        var entry = Assert.Single(list);
        Assert.Equal(SavedCharacters.LegacyId, entry.Id);
        Assert.Equal("Built before the manager", entry.Label);
    }

    /// <summary>
    /// A character with no name still gets a row, under the placeholder — an unnamed character is
    /// an ordinary state, and a blank row reads as broken rather than as unnamed. The positive
    /// control for the test above: without this, "reads the name off the payload" and "always says
    /// Unnamed character" would be indistinguishable for every character nobody named.
    /// </summary>
    [Fact]
    public async Task AnUnnamedLegacyCharacterIsStillListed()
    {
        var storage = new FakeLocalStorage();
        storage.Poke(HistoricalKey, """{"Version":1,"Mode":0,"Sheet":{"SelectedTierId":"standard"}}""");

        var entry = Assert.Single(await FreshSaved(storage).ListAsync());
        Assert.Equal("Unnamed character", entry.Label);
    }

    /// <summary>
    /// An empty sheet in the legacy slot is not a character and is not listed.
    ///
    /// <para><b>It used to be.</b> Switching the palette autosaves an otherwise untouched sheet,
    /// which put a payload at this key — and the list drew a row called "Unnamed character" for a
    /// character nobody had started. Nothing else in this class lists an empty sheet; this was the
    /// one place that did.</para>
    /// </summary>
    [Fact]
    public async Task AnEmptySheetInTheLegacySlotIsNotACharacter()
    {
        var storage = new FakeLocalStorage();
        storage.Poke(HistoricalKey, """{"Version":1,"Mode":0,"Sheet":{}}""");

        Assert.Empty(await FreshSaved(storage).ListAsync());
    }

    /// <summary>
    /// A visitor who never saved anything gets an empty list, not a phantom legacy entry
    /// synthesised from nothing.
    /// </summary>
    [Fact]
    public async Task AVisitorWithNothingSavedSeesAnEmptyList()
    {
        var storage = new FakeLocalStorage();

        Assert.Empty(await FreshSaved(storage).ListAsync());
    }

    /// <summary>
    /// The legacy payload is never moved. Relabelling it through the manager surface must
    /// still find it at the exact bare key <see cref="CharacterStore"/> has always used —
    /// moving it to a numbered slot "to be consistent" is exactly the silent-data-loss shape
    /// this whole key scheme exists to avoid.
    /// </summary>
    [Fact]
    public async Task RelabellingTheLegacySlotLeavesItsPayloadAtTheHistoricalKey()
    {
        var storage = new FakeLocalStorage();
        storage.Poke(HistoricalKey, MinimalPayload("Before the list existed"));
        var saved = FreshSaved(storage);

        var restored = (await saved.LoadAsync(SavedCharacters.LegacyId))!.Value.Sheet;
        await saved.SaveAsync(SavedCharacters.LegacyId, "My First Hero", restored, SheetMode.Hero);

        Assert.NotNull(storage.Peek(HistoricalKey));
        Assert.Equal("Before the list existed", (await saved.LoadAsync(SavedCharacters.LegacyId))!.Value.Sheet.Name);

        var entry = Assert.Single(await saved.ListAsync());
        Assert.Equal(SavedCharacters.LegacyId, entry.Id);
        Assert.Equal("My First Hero", entry.Label);
    }

    // ── The index can disagree with storage, in both directions ─────────────────────

    /// <summary>
    /// An index entry naming a character that is not actually in storage — deleted by hand,
    /// or by a run that failed halfway — is dropped rather than shown broken. Storage, not
    /// the index, decides what exists.
    /// </summary>
    [Fact]
    public async Task AnIndexEntryNamingAMissingCharacterIsDropped()
    {
        var storage = new FakeLocalStorage();
        storage.Poke($"{HistoricalKey}.index",
            "[{\"Id\":\"c_doesnotexist000000000\",\"Label\":\"Ghost\",\"UpdatedAt\":1}]");
        // No payload written at pp.character.v1.c_doesnotexist000000000.

        Assert.Empty(await FreshSaved(storage).ListAsync());
    }

    /// <summary>
    /// The other direction: a character that exists in storage but is missing from the
    /// index. The only character this can ever be is the legacy one — every other character
    /// is discovered <em>through</em> the index, since the fake (and real localStorage,
    /// without a new interop call) cannot be enumerated — and that is exactly what the
    /// legacy-slot handling checks directly rather than through the index.
    /// </summary>
    [Fact]
    public async Task AStoredCharacterMissingFromTheIndexStillAppearsWhenItIsTheLegacySlot()
    {
        var storage = new FakeLocalStorage();
        storage.Poke($"{HistoricalKey}.index", "[]");
        storage.Poke(HistoricalKey, MinimalPayload("Not in any index"));

        var entry = Assert.Single(await FreshSaved(storage).ListAsync());
        Assert.Equal(SavedCharacters.LegacyId, entry.Id);
    }

    // ── Minting and using ids ─────────────────────────────────────────────────────────

    /// <summary>The shape the server validates: <c>c_</c> plus 22 URL-safe characters.</summary>
    [Fact]
    public async Task SaveAsyncMintsAnIdInTheShapeTheServerValidates()
    {
        var storage = new FakeLocalStorage();

        var id = await FreshSaved(storage).SaveAsync(null, "Ninefold", SampleCharacters.Hero(), SheetMode.Hero);

        Assert.StartsWith("c_", id);
        var suffix = id["c_".Length..];
        Assert.Equal(22, suffix.Length);
        Assert.All(suffix, ch => Assert.True(
            char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_',
            $"'{ch}' is not URL-safe base64"));
    }

    /// <summary>Two saves without an id get two different ids, not one overwriting the other.</summary>
    [Fact]
    public async Task TwoNewSavesGetDistinctIdsAndDistinctStorage()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);

        var first = SampleCharacters.Hero();
        first.Name = "First";
        var second = SampleCharacters.Hero();
        second.Name = "Second";

        var id1 = await saved.SaveAsync(null, "One", first, SheetMode.Hero);
        var id2 = await saved.SaveAsync(null, "Two", second, SheetMode.Hero);

        Assert.NotEqual(id1, id2);

        var list = await saved.ListAsync();
        Assert.Equal(2, list.Count);

        Assert.Equal("First", (await saved.LoadAsync(id1))!.Value.Sheet.Name);
        Assert.Equal("Second", (await saved.LoadAsync(id2))!.Value.Sheet.Name);
    }

    /// <summary>Saving again with an existing id replaces it in place rather than duplicating it.</summary>
    [Fact]
    public async Task SavingWithAnExistingIdUpsertsRatherThanDuplicating()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);

        var id = await saved.SaveAsync(null, "First name", SampleCharacters.Hero(), SheetMode.Hero);

        var renamed = SampleCharacters.Hero();
        renamed.Name = "Renamed";
        var again = await saved.SaveAsync(id, "Second name", renamed, SheetMode.Hero);

        Assert.Equal(id, again);

        var list = await saved.ListAsync();
        var entry = Assert.Single(list);
        Assert.Equal("Second name", entry.Label);
        Assert.Equal("Renamed", (await saved.LoadAsync(id))!.Value.Sheet.Name);
    }

    /// <summary>A whole character — not just a name — survives being saved and loaded by id.</summary>
    [Fact]
    public async Task AWholeCharacterRoundTripsThroughSaveAndLoadById()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);
        var original = SampleCharacters.Villain();

        var id = await saved.SaveAsync(null, "Ninefold", original, SheetMode.Villain);
        var restored = (await saved.LoadAsync(id))!.Value;

        Assert.Equal(SheetMode.Villain, restored.Mode);
        Assert.Equal(Costs.TotalCost(original), Costs.TotalCost(restored.Sheet));
        Assert.Equal(original.Name, restored.Sheet.Name);
    }

    // ── Deleting ─────────────────────────────────────────────────────────────────────

    /// <summary>Deleting removes both the list entry and the loadable payload — not just one of them.</summary>
    [Fact]
    public async Task DeleteAsyncRemovesFromBothTheListAndStorage()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);
        var id = await saved.SaveAsync(null, "Temporary", SampleCharacters.Hero(), SheetMode.Hero);

        await saved.DeleteAsync(id);

        Assert.Empty(await saved.ListAsync());
        Assert.Null(await saved.LoadAsync(id));
    }

    /// <summary>Deleting the character that is currently open falls back to the legacy slot.</summary>
    [Fact]
    public async Task DeletingTheOpenCharacterFallsBackToLegacy()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);
        var id = await saved.SaveAsync(null, "Currently open", SampleCharacters.Hero(), SheetMode.Hero);
        await saved.SetCurrentAsync(id);

        await saved.DeleteAsync(id);

        Assert.Equal(SavedCharacters.LegacyId, await saved.CurrentIdAsync());
    }

    /// <summary>Deleting one character that is not open leaves the open one alone.</summary>
    [Fact]
    public async Task DeletingAnotherCharacterDoesNotDisturbTheOpenOne()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);
        var open = await saved.SaveAsync(null, "Open", SampleCharacters.Hero(), SheetMode.Hero);
        var other = await saved.SaveAsync(null, "Other", SampleCharacters.Villain(), SheetMode.Villain);
        await saved.SetCurrentAsync(open);

        await saved.DeleteAsync(other);

        Assert.Equal(open, await saved.CurrentIdAsync());
        Assert.NotNull(await saved.LoadAsync(open));
    }

    // ── Which one is open ────────────────────────────────────────────────────────────

    /// <summary>Nothing has ever switched, so the one that is open is the legacy slot.</summary>
    [Fact]
    public async Task CurrentDefaultsToTheLegacySlot()
    {
        var storage = new FakeLocalStorage();

        Assert.Equal(SavedCharacters.LegacyId, await FreshSaved(storage).CurrentIdAsync());
    }

    /// <summary>Switching and asking again answers with what was switched to.</summary>
    [Fact]
    public async Task SwitchingTheCurrentCharacterPersists()
    {
        var storage = new FakeLocalStorage();
        var saved = FreshSaved(storage);
        var id = await saved.SaveAsync(null, "Ninefold", SampleCharacters.Hero(), SheetMode.Hero);

        await saved.SetCurrentAsync(id);

        Assert.Equal(id, await FreshSaved(storage).CurrentIdAsync());
    }

    // ── Ordering ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Most recently touched first — asserted by poking the index directly with the two
    /// entries stored in the <em>wrong</em> order, so the assertion can only pass if the
    /// list is actually re-sorted by <c>UpdatedAt</c> rather than just handed back in
    /// whatever order the index happened to store them.
    /// </summary>
    [Fact]
    public async Task TheListIsOrderedByMostRecentlyTouchedFirstRegardlessOfIndexOrder()
    {
        var storage = new FakeLocalStorage();
        storage.Poke($"{HistoricalKey}.c_older0000000000000000", MinimalPayload("Older"));
        storage.Poke($"{HistoricalKey}.c_newer0000000000000000", MinimalPayload("Newer"));
        storage.Poke($"{HistoricalKey}.index",
            "[{\"Id\":\"c_older0000000000000000\",\"Label\":\"Older\",\"UpdatedAt\":100}," +
            "{\"Id\":\"c_newer0000000000000000\",\"Label\":\"Newer\",\"UpdatedAt\":200}]");

        var list = await FreshSaved(storage).ListAsync();

        Assert.Equal(["c_newer0000000000000000", "c_older0000000000000000"], list.Select(e => e.Id));
    }

    // ── Identities do not share a list ───────────────────────────────────────────────

    /// <summary>An account's list and the anonymous visitor's list are two lists, not one.</summary>
    [Fact]
    public async Task DifferentIdentitiesHaveIndependentLists()
    {
        var storage = new FakeLocalStorage();
        var anon = FreshSaved(storage, new LocalIdentity());
        var account = FreshSaved(storage, new FixedIdentity(new Identity("acct-7", "Dorian")));

        await anon.SaveAsync(null, "Anonymous character", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(null, "Account character", SampleCharacters.Villain(), SheetMode.Villain);

        Assert.Equal("Anonymous character", Assert.Single(await anon.ListAsync()).Label);
        Assert.Equal("Account character", Assert.Single(await account.ListAsync()).Label);
    }

    // ── Nothing here may throw ───────────────────────────────────────────────────────

    /// <summary>A browser that refuses storage is "no characters", not an exception, from every method.</summary>
    [Fact]
    public async Task ABrowserThatRefusesStorageIsNotAnErrorFromAnyMethod()
    {
        var storage = new FakeLocalStorage { Refuses = true };
        var saved = FreshSaved(storage);

        Assert.Empty(await saved.ListAsync());
        Assert.Null(await saved.LoadAsync("c_whatever0000000000000"));
        await saved.SaveAsync(null, "Doomed", SampleCharacters.Hero(), SheetMode.Hero);
        await saved.DeleteAsync("c_whatever0000000000000");
        Assert.Equal(SavedCharacters.LegacyId, await saved.CurrentIdAsync());
        await saved.SetCurrentAsync("c_whatever0000000000000");
    }

    /// <summary>A hand-edited index that is not even JSON does not stop the list from answering.</summary>
    [Fact]
    public async Task AHandEditedIndexDoesNotThrow()
    {
        var storage = new FakeLocalStorage();
        storage.Poke($"{HistoricalKey}.index", "not json at all");
        storage.Poke(HistoricalKey, MinimalPayload("Still here"));

        var list = await FreshSaved(storage).ListAsync();

        // The corrupt index cost nothing but itself: the legacy slot is found directly,
        // exactly as it would be with no index at all.
        var entry = Assert.Single(list);
        Assert.Equal(SavedCharacters.LegacyId, entry.Id);
    }

    /// <summary>A payload the engine cannot even parse is "no character" at that id, not a crash.</summary>
    [Fact]
    public async Task AnUnreadablePayloadAtAnIndexedIdLoadsAsNull()
    {
        var storage = new FakeLocalStorage();
        storage.Poke($"{HistoricalKey}.c_broken00000000000000", "not json at all");
        storage.Poke($"{HistoricalKey}.index",
            "[{\"Id\":\"c_broken00000000000000\",\"Label\":\"Broken\",\"UpdatedAt\":1}]");

        Assert.Null(await FreshSaved(storage).LoadAsync("c_broken00000000000000"));
    }

    // ── CharacterStore autosaves through whichever character is open ────────────────

    /// <summary>
    /// The load-bearing wiring for the manager this store exists to back: switching which
    /// character is open changes where <see cref="CharacterStore"/>'s autosave — and so
    /// <c>Program.cs</c>'s boot restore — actually reads and writes, without either of them
    /// knowing a list exists.
    /// </summary>
    [Fact]
    public async Task CharacterStoreReadsAndWritesWhicheverCharacterIsCurrentlyOpen()
    {
        var storage = new FakeLocalStorage();
        var who = new LocalIdentity();
        var saved = FreshSaved(storage, who);
        var store = FreshStore(storage, who);

        // Ordinary play before any manager exists: autosave lands at the historical key.
        await store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);
        Assert.NotNull(storage.Peek(HistoricalKey));

        // The manager creates a second character and switches to it.
        var second = SampleCharacters.Villain();
        var id = await saved.SaveAsync(null, "Second character", second, SheetMode.Villain);
        await saved.SetCurrentAsync(id);

        // The next autosave — CharacterStore has no idea anything switched — lands on the
        // second character, and the boot path (CharacterStore.LoadAsync) reads it back.
        var edited = SampleCharacters.Villain();
        edited.Name = "Edited after switching";
        await store.SaveAsync(edited, SheetMode.Villain);

        var restored = await store.LoadAsync();
        Assert.NotNull(restored);
        Assert.Equal("Edited after switching", restored!.Value.Sheet.Name);
        Assert.Equal("Edited after switching", (await saved.LoadAsync(id))!.Value.Sheet.Name);
    }

    /// <summary>
    /// An ordinary autosave to an already-listed character bumps its place in the list, so
    /// "most recently touched first" reflects play and not only explicit manager saves.
    /// </summary>
    [Fact]
    public async Task AutosavingTheOpenCharacterBumpsItsPlaceInTheList()
    {
        var storage = new FakeLocalStorage();
        var who = new LocalIdentity();
        var saved = FreshSaved(storage, who);
        var store = FreshStore(storage, who);

        var older = await saved.SaveAsync(null, "Older", SampleCharacters.Hero(), SheetMode.Hero);
        var newer = await saved.SaveAsync(null, "Newer", SampleCharacters.Villain(), SheetMode.Villain);

        // Both pinned to timestamps long before "now", so the autosave below — which stamps
        // the real current time — is unambiguously the more recent one however fast the
        // test runs.
        storage.Poke($"{HistoricalKey}.index",
            $"[{{\"Id\":\"{older}\",\"Label\":\"Older\",\"UpdatedAt\":1}}," +
            $"{{\"Id\":\"{newer}\",\"Label\":\"Newer\",\"UpdatedAt\":2}}]");

        await saved.SetCurrentAsync(older);
        await store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);

        var list = await saved.ListAsync();
        Assert.Equal(older, list[0].Id);
        Assert.Equal(newer, list[1].Id);
    }
}
