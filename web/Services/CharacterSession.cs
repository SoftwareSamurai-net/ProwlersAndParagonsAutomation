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

    private SheetMode _mode = SheetMode.Hero;

    /// <summary>Hero or Villain. Changes the palette and nothing else.</summary>
    public SheetMode Mode
    {
        get => _mode;
        set { if (_mode == value) return; _mode = value; NotifyChanged(); }
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
        _mode = mode;
    }

    /// <summary>Throws the character away. The tier page offers this; nothing else does.</summary>
    public void StartAgain()
    {
        Sheet = new CharacterSheet();
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
        _mode = mode;
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
    /// Whether to show the Hero Point budget at all. A Villain is built to whatever the
    /// GM thinks the scene needs, so the bar and the over-budget error are a Hero concern.
    /// </summary>
    public bool ShowBudget => Mode == SheetMode.Hero;

    public ValidationResult Validate() => Validator.Validate(Sheet);
}
