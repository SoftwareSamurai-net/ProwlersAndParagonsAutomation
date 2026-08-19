using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Whose character is whose.
///
/// <para><b>There are no accounts yet, so what these pin is the seam rather than a feature.</b>
/// Every visitor is anonymous and the app behaves exactly as it did — the point is that adding a
/// sign-in later is a different <see cref="IIdentitySource"/> and nothing else, and that the
/// characters people have already saved survive it.</para>
/// </summary>
public sealed class IdentityStorageTests
{
    /// <summary>The key this store has been writing since before identities existed.</summary>
    private const string HistoricalKey = "pp.character.v1";

    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly CharacterValidator Validator =
        new(Rules, Costs, new DerivedStatsCalculator(Rules));

    private sealed class FixedIdentity(Identity who) : IIdentitySource
    {
        public ValueTask<Identity> CurrentAsync() => ValueTask.FromResult(who);
    }

    private static CharacterStore StoreFor(IIdentitySource who, FakeLocalStorage storage) =>
        new(storage, Costs, Validator, who);

    /// <summary>
    /// The anonymous visitor writes to exactly the key the store has always written to.
    ///
    /// <para><b>This is the whole compatibility promise.</b> Everybody is anonymous today, so
    /// suffixing the key "for consistency" would empty every returning visitor's browser —
    /// silently, and looking exactly like storage having been cleared rather than like a bug.</para>
    /// </summary>
    [Fact]
    public async Task AnAnonymousVisitorKeepsTheHistoricalKey()
    {
        var storage = new FakeLocalStorage();
        var store = StoreFor(new LocalIdentity(), storage);

        await store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);

        Assert.NotNull(storage.Peek(HistoricalKey));
    }

    /// <summary>
    /// A character saved before identities existed is still there afterwards.
    ///
    /// <para>Asserted by reading it back rather than by comparing keys, so this covers the whole
    /// path — the key, the version gate and the shape — rather than a string.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterSavedBeforeIdentitiesStillLoads()
    {
        var storage = new FakeLocalStorage();

        // Written by the old store: same key, no notion of who.
        await StoreFor(new LocalIdentity(), storage).SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        var restored = await StoreFor(new LocalIdentity(), storage).LoadAsync();

        Assert.NotNull(restored);
        Assert.Equal(SheetMode.Villain, restored!.Value.Mode);
        Assert.Equal(SampleCharacters.Villain().Name, restored.Value.Sheet.Name);
    }

    /// <summary>
    /// Somebody signed in gets their own slot, beside the anonymous one rather than on top of it.
    ///
    /// <para><b>Beside is the requirement.</b> On a shared browser, signing in must not overwrite
    /// what the anonymous visitor was building — and signing out must not have eaten it.</para>
    /// </summary>
    [Fact]
    public async Task SigningInDoesNotOverwriteTheAnonymousCharacter()
    {
        var storage = new FakeLocalStorage();
        var anon = StoreFor(new LocalIdentity(), storage);
        var account = StoreFor(new FixedIdentity(new Identity("acct-7", "Dorian")), storage);

        var mine = SampleCharacters.Hero();
        mine.Name = "Anonymous work in progress";
        await anon.SaveAsync(mine, SheetMode.Hero);

        await account.SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        // Two slots, not one.
        var back = await anon.LoadAsync();
        Assert.NotNull(back);
        Assert.Equal("Anonymous work in progress", back!.Value.Sheet.Name);

        var theirs = await account.LoadAsync();
        Assert.NotNull(theirs);
        Assert.Equal(SheetMode.Villain, theirs!.Value.Mode);
    }

    /// <summary>
    /// Clearing is scoped to whoever asked, so "start a new character" cannot empty somebody
    /// else's slot.
    /// </summary>
    [Fact]
    public async Task ClearingOneIdentityLeavesTheOther()
    {
        var storage = new FakeLocalStorage();
        var anon = StoreFor(new LocalIdentity(), storage);
        var account = StoreFor(new FixedIdentity(new Identity("acct-7", "Dorian")), storage);

        await anon.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        await account.ClearAsync();

        Assert.NotNull(await anon.LoadAsync());
        Assert.Null(await account.LoadAsync());
    }

    /// <summary>
    /// The stub says nobody is signed in, and the app is allowed to read that.
    ///
    /// <para>The positive control for the tests above: they build a signed-in identity by hand,
    /// and would all pass in an app where signing in is impossible <em>and</em> one where the
    /// stub had quietly started claiming somebody was.</para>
    /// </summary>
    [Fact]
    public async Task TheShippedIdentityIsAnonymous()
    {
        var who = await new LocalIdentity().CurrentAsync();

        Assert.False(who.IsSignedIn);
        Assert.Null(who.DisplayName);
        Assert.Equal("local", who.Key);

        // ...and a named one reads as signed in, so IsSignedIn is reading something.
        Assert.True(new Identity("acct-7", "Dorian").IsSignedIn);
    }

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
}
