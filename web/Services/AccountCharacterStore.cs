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

    /// <summary>
    /// Nothing was started, because the character the pointer names could not be read into this
    /// browser — so what is on screen is not it, and writing it down under that id would put a
    /// stranger over somebody's character. See <see cref="ApiCharacterStore.UnreadId"/>.
    ///
    /// <para><b>It is a fourth sentence rather than <see cref="NotKept"/>, because the reason and
    /// the way out are both different.</b> "Your character could not be saved" is about a write
    /// that was attempted and failed, and it invites trying again in a moment; this write was
    /// never attempted and trying again changes nothing. What ends it is reading the character —
    /// the banner's retry, or opening one from the list — which is what the sentence has to
    /// say.</para>
    ///
    /// <para><b>Only when there is something on screen to keep.</b> An untouched sheet has nothing
    /// to write down, so the split costs it only the slot it was going to reuse: a fresh id is
    /// minted instead and the act goes through, leaving the unread character where it is.</para>
    /// </summary>
    NothingRead,
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

        // Passed on rather than reported here: the refusal is the account half's, and this is the
        // one store the shell holds. See ApiCharacterStore.WriteRefused.
        _inTheAccount.WriteRefused += why => WriteRefused?.Invoke(why);
    }

    /// <summary>
    /// Raised when a write-through was refused rather than attempted, carrying the sentence to
    /// put in front of a reader — see <see cref="ApiCharacterStore.WriteRefused"/>, which is
    /// where it is decided.
    ///
    /// <para><b>The browser's own half raises nothing</b>, and that is not an omission: local
    /// storage has no index row saying a character is priced, so there is no second opinion
    /// there to weigh a sheet against. The loss this refuses needs an account's pointer and an
    /// account's list, which is where it happens.</para>
    /// </summary>
    public event Action<string>? WriteRefused;

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

    /// <summary>
    /// Which of the account's characters this browser has open and could not read, or null —
    /// see <see cref="ApiCharacterStore.UnreadId"/>, which is where it is decided.
    ///
    /// <para><b>The account half's answer, passed on rather than copied</b>, the same way the
    /// refusal event above is. <b>The browser's own store has none</b>, for the reason
    /// <see cref="LastReadRefusal"/> gives one paragraph up: local storage either holds the
    /// character or does not, so there is no state there in which a character may be behind the
    /// pointer and unreadable.</para>
    ///
    /// <para>A reader who is not signed in is looking at the browser's store, so nothing on
    /// screen may weigh this without asking who is here first.</para>
    ///
    /// <para><b>It is about the pointer as it stands, not about a character that once failed to
    /// load</b>: every move of the pointer from this class goes through
    /// <see cref="PointAtAsync"/>, which ends it. A reader who opened another character in the
    /// manager is not in this state, whatever happened before they did.</para>
    /// </summary>
    public string? UnreadId => _inTheAccount.UnreadId;

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
        if (opened is not null) await PointAtAsync(id);

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
    /// Moves the current-character pointer, and tells the account's store that it moved.
    ///
    /// <para><b>The one place the pointer is written here, so the second half cannot be
    /// forgotten at one of them.</b> <see cref="ApiCharacterStore.UnreadId"/> is a fact about the
    /// pointer — "the character it names is one this browser could not read" — so it stops being
    /// true the moment the pointer names something else. It was read by the banner without that
    /// check first, which left a reader who had opened another character in the manager looking
    /// at a sentence saying their character could not be loaded, over a character that had loaded
    /// perfectly, beside a "Try again" that would have switched them away from it.</para>
    /// </summary>
    private async Task PointAtAsync(string id)
    {
        await _local.SetCurrentAsync(id);

        _inTheAccount.PointerMovedTo(id);
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

        // Deleting the row that is open moves the pointer back to the legacy slot from inside
        // `SavedCharacters`, so this is the one pointer move `PointAtAsync` cannot cover. Asked
        // rather than assumed, because it only moves when the deleted row was the open one.
        _inTheAccount.PointerMovedTo(await _local.CurrentIdAsync());
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

        // **The pointer may name a character this browser never read, and this path writes at the
        // pointer.** It goes through the four-argument `SaveAsync` rather than the autosave, so
        // the write-through's own refusal does not cover it — from the split state, "start a new
        // character" and "import" put the sheet on screen straight over the unread character. The
        // question is the autosave's, asked once and here: see
        // `ApiCharacterStore.WouldWriteOverACharacterNothingRead`.
        //
        // `UnreadId` is read first only to keep the ordinary press free of work: it is null every
        // time nothing has failed to read, and resolving the pointer can cost a list read.
        var unread = who.IsSignedIn && _inTheAccount.UnreadId is { Length: > 0 }
            ? await _inTheAccount.WouldWriteOverACharacterNothingRead(
                await _inTheAccount.CurrentIdAsync())
            : null;

        if (!CharacterSession.IsWorthKeeping(sheet))
        {
            // Nothing to keep, so nothing to move away from. Reusing the slot rather than minting
            // one keeps a reader who pressed this twice from collecting empty ids.
            //
            // **Unless the slot is not this browser's to reuse.** From the split state the slot is
            // not empty — it holds a character nothing here has read — so reusing it would leave
            // the reader building into an id every write is refused at. A fresh one costs nothing
            // and leaves that character exactly where it is.
            if (unread is not null)
            {
                var fresh = SavedCharacters.NewId();

                _inTheAccount.MintedHere(fresh);
                await PointAtAsync(fresh);
            }

            return StartAnotherOutcome.Started;
        }

        // Something on screen, and nowhere safe to put it: the id the keep would write at holds a
        // character this browser has not got. Refused whole rather than half — see `NothingRead`.
        if (unread is not null) return StartAnotherOutcome.NothingRead;

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

        var opened = SavedCharacters.NewId();

        // Nothing is behind a slot this browser just made up, so a read of it that cannot reach
        // the server is not a character it failed to get — see `ApiCharacterStore.UnreadId`.
        if (who.IsSignedIn) _inTheAccount.MintedHere(opened);

        await PointAtAsync(opened);

        return StartAnotherOutcome.Started;
    }

    /// <summary>Which character the app currently has open, whoever is here.</summary>
    public Task<string> CurrentIdAsync() => _local.CurrentIdAsync();

    private async Task<ICharacterStore> ChosenAsync() =>
        (await _who.CurrentAsync()).IsSignedIn ? _inTheAccount : _inThisBrowser;
}
