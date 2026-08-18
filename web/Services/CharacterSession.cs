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

    public CharacterSheet Sheet { get; private set; } = new();

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

    /// <summary>Call after mutating <see cref="Sheet"/>.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Puts back a character read out of local storage. Deliberately silent — the shell
    /// wires this up before the first render, so there is nothing to redraw yet, and the
    /// player should see their character where they left it rather than watch it arrive.
    /// </summary>
    public void Restore(CharacterSheet sheet, SheetMode mode)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        Sheet = sheet;

        // **A one-way migration, and the only reason this still takes a mode.** The palette used
        // to live in the storage envelope beside the character rather than on it. A character
        // saved before that changed carries no `IsVillain`, so it deserializes as a Hero and
        // somebody's Villain would come back wearing the wrong colours; the envelope is the only
        // place the truth survives for those. It cannot lose anything either way round, because
        // from here on the two are kept in step — `Mode` is a reading of the sheet.
        if (mode == SheetMode.Villain) Sheet.IsVillain = true;
    }

    /// <summary>
    /// Throws the character away. The tier page offers this; nothing else does.
    ///
    /// <para><b>The palette and the sandbox setting survive it, and now have to be carried
    /// deliberately.</b> Both used to sit beside the character and were untouched by a new one;
    /// both are on the sheet now, so a bare <c>new()</c> would silently return a Villain builder
    /// to Hero colours and switch the sandbox off. Neither is a fact about the character being
    /// discarded — they are how the person is working.</para>
    /// </summary>
    public void StartAgain()
    {
        var villain = Sheet.IsVillain;
        var unlimited = Sheet.UnlimitedBudget;

        Sheet = new CharacterSheet { IsVillain = villain, UnlimitedBudget = unlimited };
        NotifyChanged();
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
        Sheet = mode == SheetMode.Hero ? SampleCharacters.Hero() : SampleCharacters.Villain();
        Sheet.IsVillain = mode == SheetMode.Villain;
        NotifyChanged();
    }

    // ── Questions the shell asks constantly ───────────────────────────────

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

    public int TraitCap => Sheet.SelectedTierId is null
        ? 0
        : Rules.GetTier(Sheet.SelectedTierId)?.TraitCapRank ?? 0;

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
}
