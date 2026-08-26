using System.Net;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// One character, one account, end to end — from the browser's side of the wire.
///
/// <para><b>What the server does is tested against the real server</b>, in
/// <c>tests/worker</c>, driven over the real migration in real SQLite. What is tested here is
/// the half a Node test cannot see: which store a character goes to, what happens when the
/// server is not there, and that signing in never touches what this browser was holding.</para>
///
/// <para><b>The anonymous slot is the thing to break carefully.</b> Every visitor today is
/// anonymous, so a mistake here does not lose an account's character — it empties the browser
/// of everybody who has ever used the site, silently, looking exactly like storage having been
/// cleared.</para>
/// </summary>
public sealed class AccountTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(RepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly CharacterValidator Validator =
        new(Rules, Costs, new DerivedStatsCalculator(Rules));

    private const string AnonymousKey = "pp.character.v1";

    /// <summary>Where an opened account character is copied to, and the only slot sign-out clears.</summary>
    private const string AccountCopyKey = $"{AnonymousKey}.{SavedCharacters.AccountCopyId}";

    /// <summary>The four pieces the app wires together, built the way <c>Program.cs</c> does.</summary>
    private sealed record Wired(
        FakeApi Api, FakeLocalStorage Storage, Accounts Who, AccountCharacterStore Store);

    private static Wired Build()
    {
        var api = new FakeApi();
        var storage = new FakeLocalStorage();
        var http = new HttpClient(api) { BaseAddress = new Uri("https://pp.example.test/") };
        var who = new Accounts(http);
        var local = new CharacterStore(storage, Costs, Validator, who);

        // The account's store reads the current-id pointer out of the browser, so it needs the
        // same SavedCharacters the local store is built on — which of your characters is open is a
        // fact about this tab, not something an account should decide from another device.
        var saved = new SavedCharacters(storage, Costs, Validator, who);
        var remote = new ApiCharacterStore(http, saved, Costs, Validator);

        return new Wired(api, storage, who, new AccountCharacterStore(who, local, remote, saved));
    }

    [Fact]
    public async Task AVisitorWithNoAccountIsAnonymousAndKeepsTheirCharacterInThisBrowser()
    {
        var app = Build();

        await app.Store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);

        Assert.False((await app.Who.CurrentAsync()).IsSignedIn);
        Assert.NotNull(app.Storage.Peek(AnonymousKey));
        Assert.Null(app.Api.StoredCharacter);
    }

    [Fact]
    public async Task SomebodySignedInKeepsTheirCharacterWithTheirAccount()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");

        await app.Store.SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        Assert.NotNull(app.Api.StoredCharacter);

        // …and nothing was written to this browser under any key at all.
        Assert.Null(app.Storage.Peek(AnonymousKey));
        Assert.Null(app.Storage.Peek(AnonymousKey + ".acct-7"));

        var back = await app.Store.LoadAsync();

        Assert.NotNull(back);
        Assert.Equal(SheetMode.Villain, back!.Value.Mode);
        Assert.Equal(SampleCharacters.Villain().Name, back.Value.Sheet.Name);
    }

    /// <summary>
    /// The defect behind the owner's "empty characters to discard" report about the manager
    /// panel — found here, in the autosave path, rather than in the panel that only lists what
    /// this wrote.
    ///
    /// <para><b>The write-through PUT creates the account's row if the id is new — there is no
    /// separate "create" step.</b> Before <see cref="CharacterSession.IsWorthKeeping"/> guarded
    /// it, switching the palette or the sandbox toggle before choosing a tier fired
    /// <see cref="CharacterSession.NotifyChanged"/> exactly the way choosing a tier does, and
    /// this method PUT the untouched sheet through regardless — a real, listed, empty character
    /// on the account from one click nowhere near "Start a new character".</para>
    /// </summary>
    [Fact]
    public async Task SwitchingThePaletteOnAnUntouchedSheetCreatesNoAccountCharacter()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");

        var untouched = new CharacterSheet();
        await app.Store.SaveAsync(untouched, SheetMode.Villain);

        Assert.Null(app.Api.StoredCharacter);
        Assert.DoesNotContain(app.Api.Asked, a => a.StartsWith("PUT ", StringComparison.Ordinal));
    }

    /// <summary>
    /// The positive control for the guard above: a tier on its own is a decision, the same one
    /// <see cref="CharacterSession.HasSomethingToLose"/> already treats as worth asking about
    /// before discarding — so the same sheet, once it carries one, autosaves exactly as before.
    /// </summary>
    [Fact]
    public async Task ButChoosingATierStillAutosavesNormally()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");

        var sheet = new CharacterSheet { SelectedTierId = "standard" };
        await app.Store.SaveAsync(sheet, SheetMode.Hero);

        Assert.NotNull(app.Api.StoredCharacter);
        Assert.Contains(app.Api.Asked, a => a.StartsWith("PUT ", StringComparison.Ordinal));
    }

    /// <summary>
    /// The character follows the account, which is the whole slice.
    ///
    /// <para>Two browsers, one account: the second has its own empty local storage and finds the
    /// character anyway. Asserted through <see cref="AccountCharacterStore"/> rather than by
    /// reading the server, so it covers the routing as well as the transport.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterFollowsItsAccountToAnotherBrowser()
    {
        var laptop = Build();
        laptop.Api.SignedIn = ("acct-7", "player");

        var mine = SampleCharacters.Hero();
        mine.Name = "Ninefold";
        await laptop.Store.SaveAsync(mine, SheetMode.Hero);

        // A different browser: its own storage, its own client, the same account and server.
        var phone = Build();
        phone.Api.SignedIn = laptop.Api.SignedIn;
        phone.Api.StoredCharacter = laptop.Api.StoredCharacter;

        var there = await phone.Store.LoadAsync();

        Assert.NotNull(there);
        Assert.Equal("Ninefold", there!.Value.Sheet.Name);
        Assert.Null(phone.Storage.Peek(AnonymousKey));
    }

    /// <summary>
    /// Signing in on a shared browser leaves what somebody was building exactly where it was,
    /// and signing out gives it back.
    /// </summary>
    [Fact]
    public async Task SigningInAndOutNeverTouchesTheAnonymousCharacter()
    {
        var app = Build();

        var beingBuilt = SampleCharacters.Hero();
        beingBuilt.Name = "Anonymous work in progress";
        await app.Store.SaveAsync(beingBuilt, SheetMode.Hero);

        var untouched = app.Storage.Peek(AnonymousKey);
        Assert.NotNull(untouched);

        // Somebody signs in and saves a different character.
        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");
        await app.Store.SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        Assert.Equal(untouched, app.Storage.Peek(AnonymousKey));

        // …and signing out hands the first one back.
        app.Api.SignedIn = null;
        await app.Who.SignOutAsync();

        var back = await app.Store.LoadAsync();

        Assert.NotNull(back);
        Assert.Equal("Anonymous work in progress", back!.Value.Sheet.Name);
    }

    /// <summary>
    /// Opening one of an account's own characters copies it into a reserved anonymous slot of its
    /// own — and leaves the reader's own anonymous work exactly where it was.
    ///
    /// <para><b>This asserted the opposite until an adversarial review demonstrated the harm.</b>
    /// The copy used to go through the anonymous <em>current</em> pointer, so it landed on top of
    /// whichever local character the reader had open — a real, named, deliberately saved character,
    /// overwritten with somebody else's data under its own label. It now has
    /// <see cref="SavedCharacters.AccountCopyId"/> to itself, and the second assertion below is the
    /// one that would have caught the old behaviour.</para>
    /// </summary>
    [Fact]
    public async Task OpeningAnAccountCharacterIsCopiedIntoItsOwnAnonymousSlot()
    {
        var app = Build();

        var priorAnon = SampleCharacters.Hero();
        priorAnon.Name = "Old anonymous work";
        await app.Store.SaveAsync(priorAnon, SheetMode.Hero);

        var mineBefore = app.Storage.Peek(AnonymousKey);
        Assert.NotNull(mineBefore);

        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");

        var theirs = SampleCharacters.Villain();
        theirs.Name = "Their account character";
        await app.Store.SaveAsync(theirs, SheetMode.Villain);
        var id = (await app.Store.ListAsync()).Characters.Single().Id;

        Assert.NotNull(await app.Store.OpenAsync(id));

        // The copy is in its own slot...
        Assert.NotNull(app.Storage.Peek(AccountCopyKey));

        // ...and the reader's own anonymous character is byte-for-byte what it was.
        Assert.Equal(mineBefore, app.Storage.Peek(AnonymousKey));
    }

    /// <summary>
    /// Opening a character while nobody is signed in writes no copy — there is nothing to copy
    /// from, and the reserved slot stays empty.
    ///
    /// <para><b>This test used to assert nothing at all, and a mutation proved it.</b> It read the
    /// anonymous slot back after opening a character from that same slot, which is a self-write:
    /// removing the <c>who.IsSignedIn</c> guard from the copy-down left the whole suite green.
    /// Asserting the <em>reserved</em> slot is what makes a spurious copy observable, because a
    /// signed-out copy would have to land there and there is no other way for it to appear.</para>
    /// </summary>
    [Fact]
    public async Task OpeningACharacterSignedOutWritesNoCopy()
    {
        var app = Build();

        var mine = SampleCharacters.Hero();
        mine.Name = "Mine, anonymously";
        await app.Store.SaveAsync(mine, SheetMode.Hero);

        var id = (await app.Store.ListAsync()).Characters.Single().Id;

        // The positive control: the open really happened.
        Assert.NotNull(await app.Store.OpenAsync(id));

        Assert.Null(app.Storage.Peek(AccountCopyKey));
        Assert.Equal("Mine, anonymously", (await app.Store.LoadAnonymousAsync())!.Value.Sheet.Name);
    }

    /// <summary>
    /// Signing out removes the account copy and nothing else.
    ///
    /// <para><b>The unconditional version of this was the worst defect in the change.</b> It
    /// emptied whatever the anonymous slot held, so a reader who built a draft, signed in for any
    /// reason and signed out again lost that draft — silently, with no undo, having never gone near
    /// an account character. Both halves are asserted here: the copy goes, and the draft stays.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutRemovesTheAccountCopyAndNothingElse()
    {
        var app = Build();

        var mine = SampleCharacters.Hero();
        mine.Name = "My own draft";
        await app.Store.SaveAsync(mine, SheetMode.Hero);
        var mineBefore = app.Storage.Peek(AnonymousKey);

        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");

        var theirs = SampleCharacters.Villain();
        theirs.Name = "Their account character";
        await app.Store.SaveAsync(theirs, SheetMode.Villain);
        var id = (await app.Store.ListAsync()).Characters.Single().Id;
        await app.Store.OpenAsync(id);

        // The positive control: the copy really is there before signing out.
        Assert.NotNull(app.Storage.Peek(AccountCopyKey));

        app.Api.SignedIn = null;
        await app.Who.SignOutAsync();
        await app.Store.ClearAnonymousAsync();

        Assert.Null(app.Storage.Peek(AccountCopyKey));
        Assert.Equal(mineBefore, app.Storage.Peek(AnonymousKey));
    }

    /// <summary>
    /// A signed-out reader's ordinary saving still works, unaffected by any of the above — the
    /// anonymous slot is simply empty rather than gone.
    /// </summary>
    [Fact]
    public async Task OrdinarySavingAfterSigningOutStillWorks()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");

        app.Api.SignedIn = null;
        await app.Who.SignOutAsync();
        await app.Store.ClearAnonymousAsync();

        var freshlyAnonymous = SampleCharacters.Hero();
        freshlyAnonymous.Name = "Freshly anonymous";
        await app.Store.SaveAsync(freshlyAnonymous, SheetMode.Hero);

        var back = await app.Store.LoadAsync();
        Assert.NotNull(back);
        Assert.Equal("Freshly anonymous", back!.Value.Sheet.Name);
    }

    /// <summary>
    /// The sign-in carry-over offer still reads the anonymous slot correctly: available while
    /// the account has none, gone once it does — none of which the new copy-on-open behaviour
    /// changes, because opening a character is a different call from asking whether to offer
    /// one.
    /// </summary>
    [Fact]
    public async Task TheCarryOverOfferStillOnlyAppliesWhileTheAccountHasNoCharacter()
    {
        var app = Build();

        var beingBuilt = SampleCharacters.Hero();
        beingBuilt.Name = "Half-finished";
        await app.Store.SaveAsync(beingBuilt, SheetMode.Hero);

        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");

        // Nothing has opened anything yet, so the offer's premise still holds.
        Assert.False(await app.Store.AccountHasCharacterAsync());
        Assert.Equal("Half-finished", (await app.Store.LoadAnonymousAsync())!.Value.Sheet.Name);

        Assert.True(await app.Store.KeepAnonymousCharacterAsync());

        // The account now has one, so a fresh sign-in would offer nothing more — and the
        // browser's own copy is untouched by the offer itself, exactly as it always was.
        Assert.True(await app.Store.AccountHasCharacterAsync());
        Assert.Equal("Half-finished", (await app.Store.LoadAnonymousAsync())!.Value.Sheet.Name);
    }

    /// <summary>
    /// The anonymous character is copied up only when asked, and only into an empty account.
    /// </summary>
    [Fact]
    public async Task WhatTheBrowserWasHoldingIsKeptOnlyOnRequest()
    {
        var app = Build();

        var beingBuilt = SampleCharacters.Hero();
        beingBuilt.Name = "Half-finished";
        await app.Store.SaveAsync(beingBuilt, SheetMode.Hero);

        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("a-token");

        // Nothing has happened by itself.
        Assert.False(await app.Store.AccountHasCharacterAsync());
        Assert.Null(app.Api.StoredCharacter);

        Assert.True(await app.Store.KeepAnonymousCharacterAsync());

        var now = await app.Store.LoadAsync();
        Assert.NotNull(now);
        Assert.Equal("Half-finished", now!.Value.Sheet.Name);

        // And the browser's own copy is still there, not moved.
        Assert.NotNull(app.Storage.Peek(AnonymousKey));
    }

    /// <summary>
    /// Two accounts on one machine do not share a character.
    ///
    /// <para><b>The server's own tests cover this and the browser's did not</b>, which a fix audit
    /// found by noticing the stub had a single character slot for every account — so any test of
    /// two accounts against the character store would have been quietly a test of one, and would
    /// have passed against a server with no notion of ownership at all. Nothing in production was
    /// wrong; what was missing was the ability to tell.</para>
    /// </summary>
    [Fact]
    public async Task TwoAccountsOnOneMachineDoNotShareACharacter()
    {
        var app = Build();

        app.Api.SignedIn = ("acct-7", "player");
        var mine = SampleCharacters.Hero();
        mine.Name = "Mine";
        await app.Store.SaveAsync(mine, SheetMode.Hero);

        // Somebody else signs in on the same browser. Their account has nothing in it.
        app.Api.SignedIn = ("acct-9", "somebody-else");
        await app.Who.CompleteSignInAsync("a-token");

        Assert.Null(await app.Store.LoadAsync());
        Assert.False(await app.Store.AccountHasCharacterAsync());

        // And saving theirs does not land on top of the first.
        var theirs = SampleCharacters.Villain();
        theirs.Name = "Theirs";
        await app.Store.SaveAsync(theirs, SheetMode.Villain);

        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CompleteSignInAsync("another-token");

        var back = await app.Store.LoadAsync();
        Assert.NotNull(back);
        Assert.Equal("Mine", back!.Value.Sheet.Name);
    }

    [Fact]
    public async Task AnAccountThatAlreadyHasACharacterIsNotOfferedAReplacement()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        await app.Store.SaveAsync(SampleCharacters.Villain(), SheetMode.Villain);

        Assert.True(await app.Store.AccountHasCharacterAsync());
    }

    /// <summary>
    /// A server that cannot be asked answers "yes, there is one already".
    ///
    /// <para><b>The direction is the whole point.</b> This question exists only to decide whether
    /// to offer to copy the browser's character up, and the two ways of being wrong are not
    /// equal: answering "no" on a failed request offers to write over a character that may well
    /// be there, and the offer is a button somebody will press. Declining to offer costs one
    /// visit to the sign-in page.</para>
    /// </summary>
    [Fact]
    public async Task AnUnreachableServerIsAssumedToHaveACharacterRatherThanNotTo()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CurrentAsync();

        app.Api.Unreachable = true;

        Assert.True(await app.Store.AccountHasCharacterAsync());
    }

    /// <summary>
    /// A site whose server is not there still starts, still costs, still validates.
    ///
    /// <para>Three shapes of absence, and the third is the one that looks like success:
    /// <c>_redirects</c> serves every unmatched path as <c>index.html</c> with a 200, so a
    /// deploy without its API answers <c>/api/me</c> with a page of HTML. A client that believed
    /// the status would hand the app an identity built from nothing.</para>
    /// </summary>
    [Fact]
    public async Task NoServerMeansAnAnonymousVisitorRatherThanABlankPage()
    {
        foreach (var (name, arrange) in new (string, Action<FakeApi>)[]
        {
            ("no network", api => api.Unreachable = true),
            ("not deployed", api => api.ServerNotDeployed = true),
            ("signed out", _ => { }),
        })
        {
            var app = Build();
            arrange(app.Api);

            var who = await app.Who.CurrentAsync();

            Assert.False(who.IsSignedIn, name);
            Assert.Equal(Identity.Anonymous.Key, who.Key);

            // And the character generator carries on: the local slot answers, and nothing threw.
            Assert.Null(await app.Store.LoadAsync());
            await app.Store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);

            if (name != "no network") Assert.NotNull(app.Storage.Peek(AnonymousKey));
        }
    }

    /// <summary>
    /// A server that answers with something unusable loses the character rather than the app.
    ///
    /// <para>The same guarantee local storage already had, now over a wire: the payload is
    /// costed and validated before the app is allowed to render against it, so a null at any
    /// depth is a character that is not restored rather than a page that never draws.</para>
    /// </summary>
    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{"Version":1}""")]
    [InlineData("""{"Version":99,"Mode":0,"Sheet":{}}""")]
    public async Task AnUnusableAnswerIsNoCharacterRatherThanNoApp(string payload)
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        app.Api.StoredCharacter = payload;

        Assert.Null(await app.Store.LoadAsync());
    }

    /// <summary>
    /// A payload with a hole in it is repaired and kept, not thrown away.
    ///
    /// <para><b>The two directions are different rules and both matter.</b> The theory above is
    /// about a payload the engine could not answer for at all, which has to be discarded because
    /// the budget bar renders on every route and would take the app down on the first frame.
    /// This is about a payload that is merely incomplete — a null where an entry belongs, an id
    /// no build recognises — and there the answer is the opposite: a field removed in a later
    /// build should cost a field, not somebody's character. <c>CharacterSheetJson.Repair</c> is
    /// where the difference is decided, shared with the headless command.</para>
    ///
    /// <para>It is asserted over the wire as well as in local storage because the two stores now
    /// share that reader, and sharing it is the only thing keeping a character saved on a laptop
    /// from restoring differently on a phone.</para>
    /// </summary>
    [Theory]
    [InlineData("""{"Version":1,"Mode":0,"Sheet":{"Name":"Patched","SelectedPowers":[null]}}""")]
    [InlineData("""{"Version":1,"Mode":0,"Sheet":{"Name":"Patched","AbilityRanks":{"not_an_ability":4}}}""")]
    public async Task AnIncompleteAnswerIsRepairedRatherThanDiscarded(string payload)
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        app.Api.StoredCharacter = payload;

        var restored = await app.Store.LoadAsync();

        Assert.NotNull(restored);
        Assert.Equal("Patched", restored!.Value.Sheet.Name);
    }

    /// <summary>
    /// A save that fails is silent, and that is a cost stated rather than hidden.
    ///
    /// <para>Nothing in <see cref="ICharacterStore"/> may throw — restoring happens before the
    /// first render, so an exception there is a blank page rather than a lost character. The
    /// honest fix for a failed save is telling somebody, which the front-end plan still owes.</para>
    /// </summary>
    [Fact]
    public async Task NothingInTheStoreThrowsWhenTheServerIsGone()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");
        await app.Who.CurrentAsync();

        app.Api.Unreachable = true;

        await app.Store.SaveAsync(SampleCharacters.Hero(), SheetMode.Hero);
        Assert.Null(await app.Store.LoadAsync());
        await app.Store.ClearAsync();
    }

    /// <summary>
    /// Signing out is local even when the server cannot be told.
    ///
    /// <para>Somebody who has asked to sign out on a shared machine must not be left signed in
    /// because a request failed. The session's own expiry, and the server deleting the row when
    /// it does hear, are what make that safe rather than merely polite.</para>
    /// </summary>
    [Fact]
    public async Task SigningOutTakesEffectEvenIfTheServerCannotBeReached()
    {
        var app = Build();
        app.Api.SignedIn = ("acct-7", "player");

        Assert.True((await app.Who.CurrentAsync()).IsSignedIn);

        app.Api.Unreachable = true;
        await app.Who.SignOutAsync();

        Assert.False((await app.Who.CurrentAsync()).IsSignedIn);
    }

    [Fact]
    public async Task AskingForALinkTellsApartTheThreeThingsThatCanHappen()
    {
        var app = Build();
        Assert.Equal(LinkRequest.Accepted, (await app.Who.AskForLinkAsync("player@example.test")).Result);

        app.Api.Unreachable = true;
        Assert.Equal(LinkRequest.Unavailable, (await app.Who.AskForLinkAsync("player@example.test")).Result);
    }

    /// <summary>
    /// <b>A server that answered is not a server that could not be reached.</b>
    ///
    /// <para>Written because the two were one outcome, and it cost real time: three sign-in
    /// attempts against a deployment whose environment variables predated it were all reported
    /// as "could not reach the site just now, try again in a moment", while the server was
    /// answering promptly with a 500. The advice was wrong in both halves — nothing was
    /// unreachable, and trying again could not help — so the search went to the network instead
    /// of to the server's own configuration.</para>
    ///
    /// <para>The reference is asserted in both shapes, because both are real: the server mints
    /// one only on the path that catches an exception, and something in front of the app can
    /// answer with a failure whose body is not JSON at all. A client that threw while reading
    /// the second would turn a reported failure into an unreported one.</para>
    /// </summary>
    [Fact]
    public async Task AFailureFromTheServerIsNotAFailureToReachIt()
    {
        var app = Build();

        // Reached, broken, carrying a reference to quote.
        app.Api.LinkRequestAnswer = HttpStatusCode.InternalServerError;
        app.Api.LinkRequestReference = "7f3a91";

        var reported = await app.Who.AskForLinkAsync("player@example.test");

        Assert.Equal(LinkRequest.Failed, reported.Result);
        Assert.Equal("7f3a91", reported.Reference);

        // Reached, broken, no reference — still a failure, still not a network problem.
        app.Api.LinkRequestReference = null;

        var bare = await app.Who.AskForLinkAsync("player@example.test");

        Assert.Equal(LinkRequest.Failed, bare.Result);
        Assert.Null(bare.Reference);

        // The positive control: unreachable still reads as unreachable. Without it this test
        // would pass just as well against a client that called every outcome Failed.
        app.Api.Unreachable = true;

        Assert.Equal(LinkRequest.Unavailable,
            (await app.Who.AskForLinkAsync("player@example.test")).Result);
    }

    /// <summary>
    /// The identity that arrives over the wire carries a key and a name and nothing else.
    ///
    /// <para>Asserted by reflection rather than by naming the two, because the point is the
    /// omissions: no claims, no token, no expiry, no email address. A field added to
    /// <see cref="Identity"/> is a decision about what this project stores about people, and it
    /// should not be possible to make it without this failing.</para>
    /// </summary>
    [Fact]
    public void AnIdentityIsAKeyAndANameAndNothingElse()
    {
        var carried = typeof(Identity)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name != "IsSignedIn")   // derived from DisplayName, stores nothing
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DisplayName", "Key"], carried);
    }

    private static string RepoRoot()
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
