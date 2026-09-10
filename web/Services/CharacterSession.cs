using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Which palette the app is wearing.
///
/// <para>Ch.9 builds Villains exactly like Heroes and prints no separate stat-block
/// format, so this is a presentation choice and nothing more. It is deliberately not a
/// field on <see cref="CharacterSheet"/>: the engine has no opinion about it, and giving
/// it one would be the first crack in "the browser never decides a rule".</para>
/// </summary>
public enum SheetMode { Hero, Villain }

/// <summary>
/// The character being built, plus the calculators that answer questions about it.
///
/// <para>Every page reads its numbers from here and every page writes its edits here, so
/// this is the browser's equivalent of the CLI's <c>WizardOrchestrator</c> state. It owns
/// no rules logic of its own — each property below forwards to the engine. Anything that
/// looks like arithmetic in this file is a bug.</para>
/// </summary>
public sealed class CharacterSession
{
    public CharacterSession(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        CharacterValidator validator,
        ProConApplicability applicability,
        SourceGrouping grouping)
    {
        Rules         = rules;
        Costs         = costs;
        Derived       = derived;
        Validator     = validator;
        Applicability = applicability;
        Grouping      = grouping;
    }

    public RulesRepository Rules { get; }
    public CostCalculator Costs { get; }
    public DerivedStatsCalculator Derived { get; }
    private CharacterValidator Validator { get; }
    public ProConApplicability Applicability { get; }
    public SourceGrouping Grouping { get; }

    /// <summary>Chapter 6's armour, weapons and equipment as pickable rows.</summary>
    public GearCatalogue Catalogue => Rules.Catalogue;

    /// <summary>
    /// Chapter 6's stock vehicles and its two feature tables as pickable rows, plus the printed
    /// rates the step shows. Every price here is in a second currency, never Hero Points.
    /// </summary>
    public AssetCatalogue Assets => Rules.Assets;

    public CharacterSheet Sheet { get; private set; } = new();

    /// <summary>
    /// Which stored character <see cref="Sheet"/> was read from, or null when it was not read
    /// from one this session knows the id of.
    ///
    /// <para><b>It exists so that "the character on screen" and "the character at this id" can be
    /// the same claim rather than two hopes.</b> The browser's current-character pointer says
    /// which id the next write lands on; nothing until now said which character the sheet
    /// actually is. <see cref="StartAgain"/> empties the sheet without moving the pointer — which
    /// is what <c>SignIn.razor</c> does when the account's character cannot be read — and from
    /// there the two disagreed with nothing able to notice.</para>
    ///
    /// <para><b>Null is "this session does not know", never "no", and every reader has to take it
    /// that way.</b> A caller that has not been given an id leaves it null, so a screen weighing
    /// it must fall back to whatever it did before rather than treating the answer as a
    /// difference. That is why <see cref="HoldsTheCharacterAt"/> below is the only way to ask.</para>
    /// </summary>
    public string? HeldId { get; private set; }

    /// <summary>
    /// Whether the sheet on screen is known to be the stored character at <paramref name="id"/>.
    /// False for an unknown id and false for a session that was never told one — see
    /// <see cref="HeldId"/> for why those two may not be told apart here.
    /// </summary>
    public bool HoldsTheCharacterAt(string? id) =>
        HeldId is { Length: > 0 } && string.Equals(HeldId, id, StringComparison.Ordinal);

    /// <summary>
    /// Hero or Villain. Changes the palette and nothing else.
    ///
    /// <para><b>It is the character's own answer now, not a field beside it.</b> A sheet for a
    /// Villain opens wearing the Villain palette, and keeps it after being exported and read
    /// back — which a mode held out here could not do, because it was never in the file. This
    /// property is a reading of <c>Sheet.IsVillain</c> and stores nothing itself, so the two
    /// cannot drift.</para>
    ///
    /// <para><b>Still only a palette.</b> Both samples are legal Standard-tier characters built
    /// by identical rules, the validator is never told, and no cost or figure moves. The budget
    /// used to live on this switch and does not any more — see <see cref="ShowBudget"/>.</para>
    /// </summary>
    public SheetMode Mode
    {
        get => Sheet.IsVillain ? SheetMode.Villain : SheetMode.Hero;
        set
        {
            var villain = value == SheetMode.Villain;
            if (Sheet.IsVillain == villain) return;
            Sheet.IsVillain = villain;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Whether the character is being built in the sandbox — no Hero Point limit.
    ///
    /// <para><b>Independent of <see cref="Mode"/>, which is the whole point of the split.</b> A
    /// Hero can be built in the sandbox and a Villain can be held to a budget. One control
    /// deciding the other is the conflation this pair exists to end.</para>
    /// </summary>
    public bool UnlimitedBudget
    {
        get => Sheet.UnlimitedBudget;
        set
        {
            if (Sheet.UnlimitedBudget == value) return;
            Sheet.UnlimitedBudget = value;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Raised whenever anything on the sheet changes, so the shell can redraw the budget
    /// bar and the step list without every page having to tell it to.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Counts every call to <see cref="NotifyChanged"/>. What "the character has not changed
    /// since a given save started" means, for <see cref="Saved"/> below — never read as
    /// anything else, and in particular never as a count of edits a player would recognise.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>Call after mutating <see cref="Sheet"/>.</summary>
    public void NotifyChanged()
    {
        Version++;
        Changed?.Invoke();
    }

    /// <summary>
    /// Raised once a write-through to storage <em>completes</em>, carrying the
    /// <see cref="Version"/> that was current when that write <em>started</em> — never on the
    /// edit that started it, which is what <see cref="Changed"/> is for.
    ///
    /// <para><b>This reports what the store actually did, and invents nothing on top of
    /// it.</b> <see cref="ICharacterStore.SaveAsync"/> never throws — see its own doc comment —
    /// so there is no failure branch to represent here, and there is deliberately no "saving…"
    /// state either: nobody outside the store knows how long a write takes, and a spinner timed
    /// by guesswork is exactly the invented state this event exists to avoid.</para>
    ///
    /// <para><b>The version is what keeps this honest under two events racing.</b> Whoever wires
    /// <see cref="Changed"/> to the store fires one save per edit; a fast store can finish one
    /// while a slower one from an earlier edit is still in flight, and nothing guarantees a
    /// subscriber to both events sees "edit, then its own save" in that order — a save started
    /// before the edit can easily be reported <em>after</em> it. Comparing the carried version
    /// against <see cref="Version"/> at the moment "Saved" is read, rather than latching a bare
    /// flag from whichever event happened to run last, is what makes the answer right regardless
    /// of that ordering.</para>
    ///
    /// <para>Raised by whoever wires <see cref="Changed"/> to the store — <c>Program.cs</c> for
    /// the real app — after its own <c>await</c> on <see cref="ICharacterStore.SaveAsync"/>
    /// returns. Kept off <see cref="CharacterSession"/>'s own dependencies on purpose: this
    /// class does not know a store exists, the same reason <see cref="NotifyChanged"/> is a
    /// bell somebody else has to ring rather than a call this class makes itself.</para>
    /// </summary>
    public event Action<int>? Saved;

    /// <summary>
    /// Call once the write-through started at <paramref name="version"/> has completed —
    /// <see cref="Version"/> as it stood right after the <see cref="Changed"/> that triggered
    /// this particular save, captured by the caller before the write began.
    /// </summary>
    public void NotifySaved(int version) => Saved?.Invoke(version);

    /// <summary>
    /// Puts a character on screen and tells everything drawing one. <b>This is the one to reach
    /// for.</b>
    ///
    /// <para><b>Assigning the field is not telling anybody</b>, and the difference does not show
    /// up where you make the change. Whoever swaps the character is a component handling a click,
    /// so Blazor re-renders <em>it</em> either way — the control the reader is looking at follows
    /// perfectly while the sheet beneath it, the budget strip and the findings panel all go on
    /// drawing the character that was replaced. That shipped: the banner's pill named the new
    /// character over the old one's sheet.</para>
    ///
    /// <para>Named for the word this app already uses for moving the current-character pointer —
    /// <see cref="ICharacterStore"/>'s <c>OpenAsync</c> — so a call site reads
    /// <c>Store.OpenAsync</c> then <c>Session.Open</c>, and "showing is not opening" holds in one
    /// vocabulary on both sides.</para>
    /// </summary>
    /// <param name="id">
    /// Which stored character this is, where the caller knows — see <see cref="HeldId"/>. Left
    /// out by a caller putting up a sheet that is not one of the account's rows, or that is one
    /// whose id it has not been told.
    /// </param>
    public void Open(CharacterSheet sheet, SheetMode mode, string? id = null)
    {
        RestoreBeforeFirstRender(sheet, mode, id);
        NotifyChanged();
    }

    /// <summary>
    /// Puts back a character read out of local storage, <b>silently</b>.
    ///
    /// <para><b>Silent because the shell wires this up before the first render</b>, so there is
    /// nothing to redraw yet and the player should see their character where they left it rather
    /// than watch it arrive. <c>Program.cs</c> is the only caller in the app, and a test setting
    /// up a fixture before it renders is the only other honest one.</para>
    ///
    /// <para><b>The long name is the fix, not decoration.</b> This was called <c>Restore</c>, and
    /// it sat beside no louder alternative — so five call sites that run long after the first
    /// render reached for it, got no redraw, and left the screen disagreeing with itself. One of
    /// the six had been patched with a bare <c>NotifyChanged()</c> afterwards, which is the shape
    /// of a trap being stepped on rather than removed. Nothing that runs after the first render
    /// may call this; <see cref="Open"/> is what those want, and
    /// <c>NothingDrawnCallsTheSilentRestore</c> holds the line.</para>
    /// </summary>
    /// <param name="id">Which stored character this is, where the caller knows — see <see cref="HeldId"/>.</param>
    public void RestoreBeforeFirstRender(CharacterSheet sheet, SheetMode mode, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        Sheet = sheet;
        HeldId = id;

        // **A one-way migration, and the only reason this still takes a mode.** The palette used
        // to live in the storage envelope beside the character rather than on it. A character
        // saved before that changed carries no `IsVillain`, so it deserializes as a Hero and
        // somebody's Villain would come back wearing the wrong colours; the envelope is the only
        // place the truth survives for those. It cannot lose anything either way round, because
        // from here on the two are kept in step — `Mode` is a reading of the sheet.
        if (mode == SheetMode.Villain) Sheet.IsVillain = true;
    }

    /// <summary>
    /// Empties the sheet on screen.
    ///
    /// <para><b>The palette and the sandbox setting survive it, and now have to be carried
    /// deliberately.</b> Both used to sit beside the character and were untouched by a new one;
    /// both are on the sheet now, so a bare <c>new()</c> would silently return a Villain builder
    /// to Hero colours and switch the sandbox off. Neither is a fact about the character being
    /// discarded — they are how the person is working.</para>
    /// </summary>
    /// <param name="offerUndo">
    /// Whether to keep what was on screen for <see cref="Undo"/>.
    ///
    /// <para><b>False when the caller has already written that character down somewhere of its
    /// own, and this is not a nicety.</b> <see cref="Undo"/> restores into the sheet and does not
    /// move the current-character pointer — so undoing after the pointer has moved writes a second
    /// copy of the kept character into the fresh slot, and the reader ends up with it listed twice.
    /// An adversarial review demonstrated exactly that against "Start a new character". It is the
    /// same reason importing does not buffer: an undo is a rescue, and offering one from a
    /// character that was never in danger costs more than it gives.</para>
    ///
    /// <para>True for a caller that really is throwing the character away — discarding the row
    /// that is open, which empties the slot as well.</para>
    /// </param>
    public void StartAgain(bool offerUndo = true)
    {
        var previous = Sheet;
        var villain = Sheet.IsVillain;
        var unlimited = Sheet.UnlimitedBudget;

        Sheet = new CharacterSheet { IsVillain = villain, UnlimitedBudget = unlimited };

        // **The sheet is no longer any stored character**, and saying so is the whole reason
        // `HeldId` exists: this is the one method that empties the screen without moving the
        // pointer, so it is the one place the two can come apart.
        HeldId = null;

        NotifyChanged();

        if (offerUndo) Buffer(previous);
    }

    /// <summary>
    /// Replaces the character with one of the samples, and sets the palette to match.
    ///
    /// <para>The mode follows the sample because that is the whole point of loading one —
    /// seeing the sheet dressed as a Hero or as a Villain. It is still only a palette:
    /// both samples are legal Standard-tier characters built by identical rules, and
    /// nothing on either sheet records which it is.</para>
    /// </summary>
    public void LoadSample(SheetMode mode)
    {
        var previous = Sheet;
        Sheet = mode == SheetMode.Hero ? SampleCharacters.Hero() : SampleCharacters.Villain();
        Sheet.IsVillain = mode == SheetMode.Villain;
        HeldId = null;
        NotifyChanged();
        Buffer(previous);
    }

    /// <summary>
    /// The same as <see cref="Open"/>, for a caller that is overwriting the character on
    /// screen rather than switching to one that already lives under its own id — importing a
    /// file, and opening a recorded character.
    ///
    /// <para><b>Both keep the current-character pointer exactly where it was</b>, unlike signing
    /// in, signing out, or opening a different saved character from the manager — all of which
    /// move which id the next autosave writes to, so the character being left behind is safe in
    /// its own row. These two do not move that pointer, so the very next write-through overwrites
    /// the only stored copy of what was on screen — which is what makes this destructive, and
    /// what <see cref="Buffer"/> exists to undo.</para>
    ///
    /// <para>It goes through <see cref="Open"/> rather than the silent restore, because every
    /// caller that reaches this wants the redraw and the autosave that follows it — the one
    /// exception is the app's own boot. <b>This method said exactly that while five call sites
    /// elsewhere were getting it wrong</b>, which is why the two are now told apart by their
    /// names rather than by a remark on one of them.</para>
    /// </summary>
    /// <param name="id">Which stored character this is, where the caller knows — see <see cref="HeldId"/>.</param>
    public void ReplaceWithUndo(CharacterSheet sheet, SheetMode mode, string? id = null)
    {
        var previous = Sheet;
        Open(sheet, mode, id);
        Buffer(previous);
    }

    // ── Undo ───────────────────────────────────────────────────────────────

    /// <summary>The character <see cref="Buffer"/> is holding, as JSON — or null if nothing is.</summary>
    private string? _undoSnapshot;
    private SheetMode _undoMode;
    private string _undoLabel = "";

    /// <summary>
    /// The <see cref="Version"/> the moment the buffer above was filled. <see cref="CanUndo"/>
    /// requires an exact match, which is what makes the window close on the first edit to the
    /// character that replaced the buffered one — see <see cref="CanUndo"/>.
    /// </summary>
    private int _undoArmedAtVersion;

    /// <summary>
    /// Keeps <paramref name="previous"/> so <see cref="Undo"/> can bring it back, called by every
    /// method that replaces <see cref="Sheet"/> outright and would otherwise autosave over the
    /// only stored copy of what was there.
    ///
    /// <para><b>As JSON, never the live object.</b> Holding the <see cref="CharacterSheet"/>
    /// instance itself would let the very first edit to the character that replaced it rewrite
    /// this one too — the replay's own recorded bug (handing over a shared instance let the first
    /// edit rewrite the recording), reached a second way. Writing it out is the same round trip
    /// <see cref="ICharacterStore"/> already trusts for local storage.</para>
    ///
    /// <para>Nothing is kept when there was nothing worth keeping — an empty sheet has nothing
    /// for <see cref="Undo"/> to bring back, and offering to would be ceremony over nothing.</para>
    /// </summary>
    private void Buffer(CharacterSheet previous)
    {
        ArgumentNullException.ThrowIfNull(previous);

        if (!IsWorthKeeping(previous)) { _undoSnapshot = null; return; }

        _undoSnapshot = CharacterSheetJson.Write(previous);
        _undoMode = previous.IsVillain ? SheetMode.Villain : SheetMode.Hero;
        _undoLabel = string.IsNullOrWhiteSpace(previous.Name) ? "Your character" : previous.Name;
        _undoArmedAtVersion = Version;
    }

    /// <summary>
    /// Whether <see cref="Undo"/> would do anything right now.
    ///
    /// <para><b>A fact about this screen, never about the character</b> — it is not exported,
    /// stored, or read back, and it does not survive a refresh; it lives only as long as this
    /// session does, the same reason the budget breakdown's open/shut state is a field on a
    /// component rather than on <see cref="CharacterSheet"/>.</para>
    ///
    /// <para><b>Single-level, and self-closing.</b> <see cref="Version"/> has to be exactly what
    /// it was the instant the buffer was filled — so the first edit to the character that
    /// replaced the buffered one closes the window, rather than leaving an undo sitting there
    /// that would also throw that edit away. There is no explicit clear for this reason: any
    /// mutation at all moves <see cref="Version"/> on, and the comparison below stops matching by
    /// itself.</para>
    /// </summary>
    public bool CanUndo => _undoSnapshot is not null && Version == _undoArmedAtVersion;

    /// <summary>What <see cref="Undo"/> would bring back, for the message beside the offer.</summary>
    public string UndoLabel => _undoLabel;

    /// <summary>
    /// Brings back the character <see cref="Buffer"/> is holding, if <see cref="CanUndo"/> is
    /// still true. One level: this clears the buffer before returning, so a second call in a row
    /// does nothing — there is no redo, and no way to step back further than the one thing that
    /// was just replaced.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var sheet = CharacterSheetJson.Read(_undoSnapshot!, strict: false);
        _undoSnapshot = null;
        if (sheet is null) return;

        Open(sheet, _undoMode);
    }

    // ── Questions the shell asks constantly ───────────────────────────────

    /// <summary>
    /// Whether the sheet holds anything a player would mind losing.
    ///
    /// <para><b>Here rather than in the pages, because several of them ask it and they must not
    /// disagree.</b> The tier page's discard, the portfolio's samples and the replay's "open this
    /// character" all replace the character outright, all can destroy twenty minutes written down
    /// nowhere else, and all decide whether to ask first by answering this. Two copies had already
    /// drifted apart once by a field.</para>
    ///
    /// <para>A tier on its own counts: it is a decision, and it is the one every other choice is
    /// measured against.</para>
    /// </summary>
    public bool HasSomethingToLose => IsWorthKeeping(Sheet);

    /// <summary>
    /// The same question as <see cref="HasSomethingToLose"/>, asked of a sheet that is not
    /// necessarily the one a session is holding.
    ///
    /// <para><b>Static because <see cref="ApiCharacterStore"/>'s autosave needs the same answer
    /// and only ever has the sheet, never a session.</b> That store's write-through PUTs
    /// unconditionally — it creates the account's row if the id is new — so switching the
    /// palette or the sandbox toggle before choosing anything else used to create a real,
    /// listed, empty character the moment either fired. One predicate answers both questions so
    /// they cannot drift apart the way two copies of this already have once.</para>
    /// </summary>
    public static bool IsWorthKeeping(CharacterSheet sheet) =>
        sheet.SelectedTierId is not null
        || sheet.SelectedPowers.Count > 0
        || sheet.AbilityRanks.Count > 0
        || sheet.TalentRanks.Count > 0
        || !string.IsNullOrWhiteSpace(sheet.Name);

    /// <summary>
    /// Whether there is nothing on this sheet for anybody else to look at — <b>literally
    /// nothing</b>.
    ///
    /// <para><b>Every kind of thing a character can be made of is asked about, and that is the
    /// fix rather than a widening.</b> This used to be the Ability question alone: every Ability
    /// below the rulebook's floor, which is what an untouched sheet looks like to
    /// <c>CharacterValidator</c>. But a character is not only its Abilities. A sheet with four
    /// Powers on it and no Ability rank bought yet answered "nothing on it" — so the send was
    /// refused with "this character has nothing on it yet" over somebody's afternoon, and the
    /// GM's screen captioned their clone as empty. **A powers-only sheet is a character, a
    /// Talents-only sheet is a character, and a sheet with nothing but a name is a character**;
    /// each of them is somebody's half-finished work, and this app exists to keep exactly
    /// that.</para>
    ///
    /// <para><b>The Abilities half is still the engine's answer rather than a count of a
    /// dictionary</b>, and that is not decoration: the editors leave a 0 behind when a Trait is
    /// stepped back down, so a sheet with six zeroed Ability entries is untouched and a
    /// dictionary count would call it built. Ch.2 states twice that no Ability can be lower than
    /// 1d and that every character has all of them, so <c>TRAIT_BELOW_MINIMUM</c> across every
    /// one of them is the absence of a character rather than a weak one.</para>
    ///
    /// <para><b>Counted against the rules rather than a literal six, and a rulebook with no
    /// Abilities in it answers "not empty".</b> Without that guard <c>below == 0 == Count</c> and
    /// every sheet in the app would be called empty at once — a rules file that failed to load
    /// turning into "nobody has a character", which is the loudest possible way to be wrong about
    /// the quietest possible cause.</para>
    ///
    /// <para><b>Why not <see cref="IsWorthKeeping"/>.</b> That predicate answers "would a player
    /// mind losing this", and a tier alone counts — deliberately, because choosing one is a
    /// decision. But <c>CampaignJoin.Apply</c> writes a tier onto an empty sheet in the course of
    /// typing a join code, so the empty submission this exists to refuse passes
    /// <see cref="IsWorthKeeping"/> by construction. Two questions, two predicates; the one thing
    /// they may not do is share a spelling and drift.</para>
    ///
    /// <para><b>A sheet the engine cannot price is not empty and is not refused here.</b>
    /// <see cref="TryCost"/> answers null for a variable-cost Power with no variant chosen, which
    /// is a half-finished character and exactly the work this app exists to keep — and a sheet
    /// with a Power on it has already answered false above. The approval screen has its own arm
    /// for a snapshot it cannot price; refusing to send one would be repairing rather than
    /// reporting.</para>
    /// </summary>
    public bool HasNothingOnIt(CharacterSheet sheet) => HasNothingOnIt(sheet, Validator, Rules);

    /// <summary>
    /// The same question, for a caller that has no session — <c>ApiCharacterStore</c>'s
    /// write-through, which only ever has the sheet.
    ///
    /// <para><b>Static for the reason <see cref="IsWorthKeeping"/> is static</b>, and it is the
    /// same bargain: two spellings of "this sheet is empty" is two chances for the belt that
    /// refuses to write one and the page that refuses to send one to disagree about somebody's
    /// character. These two have already drifted apart once as separate copies.</para>
    /// </summary>
    public static bool HasNothingOnIt(
        CharacterSheet sheet, CharacterValidator validator, RulesRepository rules)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(rules);

        // Anything at all that somebody chose. A Power, a Talent, a Perk, a Flaw, a piece of
        // Gear or a name is a character, whatever the Abilities block says.
        if (sheet.TalentRanks.Count > 0
            || sheet.SelectedPowers.Count > 0
            || sheet.Perks.Count > 0
            || sheet.Flaws.Count > 0
            || sheet.Gear.Count > 0
            || !string.IsNullOrWhiteSpace(sheet.Name))
        {
            return false;
        }

        // See the remarks: without this, a rules repository with no Abilities in it would make
        // every sheet in the app empty at once.
        if (rules.Abilities.Count == 0) return false;

        var below = validator.Validate(sheet).Issues
            .Count(i => i.SubjectKind == ValidationSubject.Ability
                        && string.Equals(i.Code, "TRAIT_BELOW_MINIMUM", StringComparison.Ordinal));

        return below == rules.Abilities.Count;
    }

    public int Spent => Costs.TotalCost(Sheet);

    /// <summary>
    /// A cost, or null if the sheet cannot be priced yet.
    ///
    /// <para>The engine throws rather than guessing when a selection is incomplete — a
    /// variable-cost Power with no variant chosen, a graded gear feature with no grade.
    /// The pages here never create one, because each asks for the variant before it adds
    /// anything, and <see cref="CharacterValidator"/> reports the gap properly on the
    /// review step. This exists so the always-on budget bar shows a dash in the meantime
    /// instead of taking the whole app down.</para>
    /// </summary>
    public static int? TryCost(Func<int> cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        try { return cost(); }
        catch (InvalidOperationException) { return null; }
    }

    public int Budget => Sheet.SelectedTierId is null
        ? 0
        : Rules.GetTier(Sheet.SelectedTierId)?.HeroPoints ?? 0;

    /// <summary>
    /// <b>The cap this character is built to</b> — the house cap on the sheet where there is one
    /// and the tier's otherwise, which is <c>DerivedStatsCalculator.EffectiveTraitCap</c> and is
    /// not spelled out a second time here. Every rank field on every step is bounded by this, and
    /// the number the browser bounds by has to be the number the validator judges by and the
    /// number Resolve was measured from.
    /// </summary>
    public int TraitCap =>
        DerivedStatsCalculator.EffectiveTraitCap(Sheet, Tier) ?? 0;

    /// <summary>
    /// The tier's own ceiling, for the screens that say so when a house cap has moved
    /// <see cref="TraitCap"/> off it. 0 with no tier, like the two figures above.
    /// </summary>
    public int TierTraitCap => Tier?.TraitCapRank ?? 0;

    /// <summary>
    /// Whether a house cap is in force and differs from the tier's — the one question a screen
    /// asks before printing the tier's figure beside the one it is showing. A house cap that
    /// happens to equal the tier's is not worth a note about.
    /// </summary>
    public bool TraitCapIsNotTheTiers => Tier is not null && TraitCap != Tier.TraitCapRank;

    private Engine.Models.TierModel? Tier =>
        Sheet.SelectedTierId is null ? null : Rules.GetTier(Sheet.SelectedTierId);

    /// <summary>
    /// Whether the Hero Point budget is a limit for this character.
    ///
    /// <para><b>This used to read <c>Mode == Hero</c>, and that was the conflation.</b> Ch.9
    /// builds Villains by exactly the Hero rules, so "a Villain has no budget" was never a rule
    /// about Villains — it was a GM building to whatever the scene needs, which is a way of
    /// working rather than a kind of character. A Hero can be built that way too, and a Villain
    /// can be held to a tier's points. So the question is now the sandbox toggle and the palette
    /// has no opinion about it.</para>
    ///
    /// <para>False does not mean the budget is hidden. The strip shows a running total with no
    /// cap, no rail and no remaining figure — a fact about the character, without a limit it is
    /// not being held to.</para>
    /// </summary>
    public bool ShowBudget => !Sheet.UnlimitedBudget;

    public ValidationResult Validate() => Validator.Validate(Sheet);

    /// <summary>
    /// The same, for a character that is not the one being built — a recorded one the replay
    /// is showing.
    ///
    /// <para>It exists so the validator can stay private here. Every other engine service on
    /// this class is exposed because components need to ask it things; making this one public
    /// too would put a second way of reaching the same answer beside
    /// <see cref="Validate()"/>, and two of those is how they end up disagreeing.</para>
    /// </summary>
    public ValidationResult Validate(CharacterSheet sheet) => Validator.Validate(sheet);

    /// <summary>
    /// What is wrong with one of a campaign's shared vehicles or bases, or nothing.
    ///
    /// <para><b>Here for the same reason <see cref="Validate(CharacterSheet)"/> is</b>: the object
    /// is not the character being built and the validator stays private. A shared object is priced
    /// off the same printed table a machine on a sheet is, so it is held to the same printed
    /// constraints — see <c>CharacterValidator.CheckSharedAsset</c>, which is where the argument
    /// for that living in the engine rather than on the screen is written down.</para>
    /// </summary>
    /// <param name="asset">The campaign's shared object.</param>
    /// <param name="contributions">
    /// Every contribution the caller has collected, from any member — optional, and null for a
    /// caller (or a test) that only has the object. Held to the same cap and the same
    /// kind-mismatch check <see cref="CharacterValidator.CheckSharedAsset"/> already makes when it
    /// is handed contributions.
    /// </param>
    public IReadOnlyList<ValidationIssue> Check(
        CampaignAsset asset, IEnumerable<CampaignAssetContribution>? contributions = null) =>
        Validator.CheckSharedAsset(asset, contributions);

    /// <summary>
    /// Every shared object a campaign has, with its books open — <see cref="CampaignAssets.Ledger"/>,
    /// handed both engine services it needs without a `.razor` file ever naming
    /// <c>CharacterValidator</c> in an <c>@inject</c> line. <c>NoPageNamesATypeThisProjectDeclares</c>
    /// scans the visible prose of every page, and an <c>@inject</c> directive is not stripped from
    /// it — the type after the keyword is exactly as visible to that guard as a sentence would be,
    /// which is why this wrapper exists rather than a second injected engine service beside
    /// <see cref="Costs"/>.
    /// </summary>
    public IReadOnlyList<CampaignAssetLine> AssetLedger(
        Campaign? campaign, IEnumerable<(string MembershipId, string Who, string? PlayerKey, CharacterSheet Sheet)> members) =>
        CampaignAssets.Ledger(campaign, Costs, Validator, members);
}
