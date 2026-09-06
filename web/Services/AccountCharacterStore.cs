using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What happened when somebody asked to keep the character they have and open a fresh slot.</summary>
public enum StartAnotherOutcome
{
    /// <summary>
    /// A fresh slot is open. Whatever was on screen is kept under its own id and is in the list.
    /// </summary>
    Started,

    /// <summary>
    /// Nothing was started, because the account has no room for another character. The character
    /// on screen is untouched — which is the whole difference from what this replaced.
    /// </summary>
    NoRoom,

    /// <summary>
    /// Nothing was started, because the character on screen could not be written down — the server
    /// refused it, the browser refused storage, or neither could be reached at all.
    ///
    /// <para><b>Refusing is the only safe answer here, and it is not the cautious one by
    /// accident.</b> Opening a fresh slot is what stops the next autosave landing on the character
    /// being left behind; if that character has not actually been stored, moving on abandons it
    /// with no copy anywhere. So a write that did not land stops the whole act rather than half
    /// of it.</para>
    /// </summary>
    NotKept,

    /// <summary>
    /// The character on screen is kept and safe, but no fresh slot was opened: the account's cap
    /// could not be read, so whether there is room for another is unknown.
    ///
    /// <para><b>It is a separate answer from <see cref="NotKept"/> because the sentence a reader
    /// gets is different, and the wrong one of the two is a lie about their character.</b> "Your
    /// character could not be saved" over a character that was saved is exactly the kind of false
    /// alarm that teaches somebody to distrust every message the app gives them.</para>
    /// </summary>
    NotStarted,
}

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

    /// <summary>
    /// Why the last <see cref="OpenAsync"/> or <see cref="ReadAsync"/> answered null, or
    /// <see cref="ReadRefusal.None"/> when it did not. See <see cref="ApiCharacterStore.LastReadRefusal"/>
    /// for why the two failures may not be one null, and why it is beside the read rather than in
    /// its return type.
    ///
    /// <para><b>The browser's own store has no unreachable half</b>: local storage either holds
    /// the character or does not, and a browser refusing storage answers the same "there is
    /// nothing here" as an id nobody has written. So the local side is always
    /// <see cref="ReadRefusal.NotThere"/>, which is the honest reading of it rather than a
    /// simplification.</para>
    /// </summary>
    public ReadRefusal LastReadRefusal { get; private set; } = ReadRefusal.None;

    /// <summary>Open one of them. Null when it is not there, or not one this build can read.</summary>
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> OpenAsync(string id)
    {
        var who = await _who.CurrentAsync();

        var opened = who.IsSignedIn
            ? await _inTheAccount.LoadAsync(id)
            : await _local.LoadAsync(id);

        LastReadRefusal = Why(who.IsSignedIn, opened);

        // The pointer moves only if there was something to move to. Switching to a character that
        // could not be read would leave the app pointed at nothing, and the next autosave would
        // write the character on screen over an id the visitor did not choose.
        if (opened is not null) await _local.SetCurrentAsync(id);

        // A signed-in reader opening one of their account's characters is now what this browser is
        // holding, so it goes into the anonymous side — but into `AccountCopyId` and never over
        // whatever was open there. Writing it through the anonymous *current* pointer, which is
        // what this did first, overwrote a real named local character; see that constant.
        // Nothing to do when nobody is signed in: the anonymous slot already is where this came
        // from, and writing it back would be a self-write no test could observe.
        if (who.IsSignedIn && opened is not null)
            await CopyDownAsync(opened.Value.Sheet, opened.Value.Mode);

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
    /// <para><b>Conditional, and the unconditional version was a defect.</b> It used to empty
    /// whatever the anonymous slot held. That destroyed the reader's own work in two ways nobody
    /// had traced: a draft built before signing in was deleted by a later sign-out even though no
    /// account character was ever opened, and a named local character that happened to be open was
    /// deleted outright. Both were demonstrated by adversarial review, and both are gone because
    /// the copy now lives at <see cref="SavedCharacters.AccountCopyId"/> and this removes only
    /// that. Anything of the reader's own is untouched.</para>
    ///
    /// <para><b>The pointer goes back to the legacy slot</b>, because leaving it at an id that has
    /// just been deleted would land the next read on nothing while the reader's own characters sat
    /// in the list unreachable.</para>
    /// </summary>
    public Task ClearAnonymousAsync() =>
        _local.ClearPayloadAsync(Identity.Anonymous, SavedCharacters.AccountCopyId);

    /// <summary>
    /// Puts an opened account character into the anonymous side's reserved slot.
    ///
    /// <para><b>Written at the id outright, never through the current-character pointer.</b> The
    /// first version moved the pointer and then saved "the open character", trusting the write to
    /// see the move — and it did not: the payload landed under the previously-open character's id
    /// and overwrote it. Naming the id removes the ordering question, leaves the reader's pointer
    /// where they left it, and keeps the copy out of their index so it never appears in their own
    /// list.</para>
    /// </summary>
    private Task CopyDownAsync(CharacterSheet sheet, SheetMode mode) =>
        _local.SavePayloadAsync(Identity.Anonymous, SavedCharacters.AccountCopyId, sheet, mode);

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
    public async Task<(CharacterSheet Sheet, SheetMode Mode)?> ReadAsync(string id)
    {
        var who = await _who.CurrentAsync();

        var read = who.IsSignedIn
            ? await _inTheAccount.LoadAsync(id)
            : await _local.LoadAsync(id);

        LastReadRefusal = Why(who.IsSignedIn, read);

        return read;
    }

    /// <summary>
    /// Which of the two failures the read that just happened was, read off whichever half
    /// answered it. Spelled once, because two copies of this would be two chances for one of them
    /// to report an unreachable server as a character that is not there.
    /// </summary>
    private ReadRefusal Why(bool signedIn, (CharacterSheet Sheet, SheetMode Mode)? read) =>
        read is not null ? ReadRefusal.None
        : signedIn ? _inTheAccount.LastReadRefusal
        : ReadRefusal.NotThere;

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

        // The browser's own store has no cap, but it does have a browser that can refuse storage
        // — and this is the one method here that has to say so, because an undo that silently did
        // nothing leaves the reader believing their character is back. It used to compare the
        // returned id against the one passed in, which is the same string either way.
        return (await _local.SaveAsync(id, label, sheet, mode)).Stored;
    }

    /// <summary>
    /// Keeps the character on screen under its own id and opens a fresh, empty slot beside it.
    ///
    /// <para><b>This is the path that did not exist, and its absence was the defect.</b> "Start a
    /// new character" was <c>StartAgain</c> plus <see cref="ClearAsync()"/> — it emptied the slot
    /// the character was in rather than leaving it there and pointing somewhere else, so making a
    /// second character meant destroying the first. Importing had the same shape by a different
    /// route: it overwrote whatever the pointer was aimed at. Nothing anywhere in the app ever
    /// added a character to the index, so the manager's list, the banner's switcher and both undo
    /// buffers — all of which read that index and all of which were tested — could only ever have
    /// had the one slot to work with.</para>
    ///
    /// <para><b>The order is the whole of the correctness here.</b> The character is written down
    /// <em>first</em>, then the pointer moves, and only then may the caller empty the session. Both
    /// steps are awaited, so the fire-and-forget autosave that emptying fires reads a pointer that
    /// has already moved and lands on the new slot. Doing it the other way round — the shape the
    /// account copy-down shipped once — writes the empty sheet over the character being kept.</para>
    ///
    /// <para><b>The caller passes the sheet rather than this reading one</b>, because the character
    /// on screen is the session's and this class has never known a session exists. It is the same
    /// sheet the autosave would write; naming it here is what lets this run before the session is
    /// emptied.</para>
    ///
    /// <para><b>An empty sheet is kept by doing nothing to it.</b> There is nothing to write down
    /// and nothing to lose, so the pointer stays where it is and the slot is simply reused. That
    /// also means starting over from an untouched sheet costs no id and leaves no empty row.</para>
    /// </summary>
    public async Task<StartAnotherOutcome> StartAnotherAsync(CharacterSheet sheet, SheetMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var who = await _who.CurrentAsync();

        if (!CharacterSession.IsWorthKeeping(sheet))
        {
            // Nothing to keep, so nothing to move away from. Reusing the slot rather than minting
            // one keeps a reader who pressed this twice from collecting empty ids.
            return StartAnotherOutcome.Started;
        }

        if (who.IsSignedIn)
        {
            // **The character is written down first, and only then is the cap asked about.** The
            // order used to be the other way round and it raced the very thing it was there to
            // prevent: the ordinary autosave is fire-and-forget over HTTP, so a list read straight
            // after an edit can answer from before that edit's row existed. On an account one short
            // of its cap that reads as room, a fresh slot opens, and the character built in it is
            // refused with a 409 that the autosave path has nowhere to report — everything typed
            // into it goes quietly nowhere. An adversarial review demonstrated exactly that.
            //
            // Writing first removes the race rather than narrowing it: the write is awaited, so the
            // list that follows it cannot be answering from before the character existed.
            //
            // The account's own resolved id, not the browser's raw pointer: that pointer may still
            // read `legacy`, which is this browser's private name for a slot and which the server
            // refuses. Asking the account's store is what maps it. And it is written explicitly
            // rather than trusted to the autosave that fired on the last edit, because nobody
            // awaits that one.
            var kept = await _inTheAccount.SaveAsync(
                await _inTheAccount.CurrentIdAsync(), SavedCharacters.LabelFor(sheet), sheet, mode);

            if (kept != SaveOutcome.Saved)
                return kept == SaveOutcome.AccountIsFull
                    ? StartAnotherOutcome.NoRoom
                    : StartAnotherOutcome.NotKept;

            // Now the cap, counting the character just kept. A cap that could not be read is not
            // room — the same direction `AccountCharacters.IsFull` takes — but it is `NotStarted`
            // rather than `NotKept`, because by here the character demonstrably is kept.
            var listed = await _inTheAccount.ListAsync();
            if (listed.Limit is null) return StartAnotherOutcome.NotStarted;
            if (listed.Characters.Count >= listed.Limit) return StartAnotherOutcome.NoRoom;
        }
        else
        {
            // **Whether the write landed, not whether the id came back.** This used to compare the
            // returned id against the one passed in, which for a non-null id is the same string
            // whether or not anything was stored — a dead check that read like a guard, and on a
            // browser refusing storage it reported success over a character that had gone nowhere.
            var id = await _local.CurrentIdAsync();
            if (!(await _local.SaveAsync(id, SavedCharacters.LabelFor(sheet), sheet, mode)).Stored)
                return StartAnotherOutcome.NotKept;
        }

        await _local.SetCurrentAsync(SavedCharacters.NewId());

        return StartAnotherOutcome.Started;
    }

    /// <summary>Which character the app currently has open, whoever is here.</summary>
    public Task<string> CurrentIdAsync() => _local.CurrentIdAsync();

    private async Task<ICharacterStore> ChosenAsync() =>
        (await _who.CurrentAsync()).IsSignedIn ? _inTheAccount : _inThisBrowser;
}
