namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// A Power as printed in the rulebook. Every entry carries a Range, a rank type
/// and a cost, and those three fields together determine how it is paid for.
/// </summary>
public record PowerModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";

    /// <summary>self | touch | ranged | zone | special</summary>
    public string Range { get; init; } = "";

    /// <summary>
    /// power | baseline | default | special.
    /// <c>default</c> means the Power has no rank at all — the rulebook has you use
    /// Toughness or Willpower (by Source) in its place when another Power targets it.
    /// <c>baseline</c> means it has a rank derived from another Trait via <see cref="Prerequisite"/>.
    /// </summary>
    public string RankType { get; init; } = "";

    /// <summary>per_rank | flat | per_unit | per_rank_variable | flat_variable | special</summary>
    public string CostType { get; init; } = "";

    /// <summary>
    /// HP per purchased rank when CostType is per_rank. 0.5 for Powers priced at
    /// "1 Hero Point per 2 ranks". Null for every other cost type.
    /// </summary>
    public double? CostPerRank { get; init; }

    /// <summary>Total HP when CostType is flat. Null otherwise.</summary>
    public int? CostFlat { get; init; }

    /// <summary>HP per unit when CostType is per_unit (see <see cref="CostUnitLabel"/>).</summary>
    public int? CostPerUnit { get; init; }

    /// <summary>What a unit is for per_unit Powers: "immunity", "power level", "resolve".</summary>
    public string? CostUnitLabel { get; init; }

    /// <summary>
    /// Selectable cost values for the two variable cost types, keyed by variant name —
    /// e.g. Omni-Power { narrow: 3, broad: 5 }, Stretching { close_range: 1, ... }.
    /// The value is HP per rank for per_rank_variable and total HP for flat_variable.
    /// </summary>
    public IReadOnlyDictionary<string, double>? CostVariants { get; init; }

    /// <summary>
    /// 0 when no ranks can be purchased — the Power has no rank, or it is bought for a
    /// flat/per-unit price. Null means ranks are purchasable up to the game's Trait Cap.
    /// </summary>
    public int? MaxRank { get; init; }

    public int DicePerRank { get; init; } = 1;

    /// <summary>
    /// Project-authored paraphrase, not rulebook text. Never listed in
    /// <see cref="VerifiedFields"/> — see <see cref="DescriptionVerified"/>.
    /// </summary>
    public string Description { get; init; } = "";

    public PowerPrerequisiteModel? Prerequisite { get; init; }

    // There is deliberately no list of generic Pros and Cons here. The rulebook states
    // applicability inside each generic option instead, so it is derived — see
    // ProConApplicability. The lists that used to live here were this project's guesses.

    /// <summary>
    /// Generic options this Power's own printed text names, overriding the Range constraint
    /// the option itself states. Empty for all but a handful of entries.
    ///
    /// <para><b>This is not the <c>available_pros</c> list that was removed, and must never
    /// grow into one.</b> That list was a guess about which options suited a Power, and it
    /// filtered absolutely. This one records the opposite and much narrower thing: the
    /// rulebook printing, inside a Power's entry, an instruction to apply a named generic
    /// option that the option's own Range rule would otherwise forbid. Force Field is Self
    /// range and its entry reads "Apply the Zone Pro to shield large areas, the Ranged Pro
    /// to shield things at a distance, or the Area Pro to shield large areas at a distance"
    /// (Ch.2 p.29) — and T-Kay, printed on p.143, carries Force Field 12d (Zone).</para>
    ///
    /// <para>An entry here needs a sentence of the Power's own text behind it. It is the
    /// same shape as Deflection covering both attack types, which the book also states as
    /// prose rather than as a marked PRO.</para>
    /// </summary>
    public IReadOnlyList<string> ProsAllowedByOwnText { get; init; } = [];

    /// <summary>Pros printed inside this Power's own rulebook entry.</summary>
    public IReadOnlyList<PowerProConModel> PowerPros { get; init; } = [];

    /// <summary>Cons printed inside this Power's own rulebook entry.</summary>
    public IReadOnlyList<PowerProConModel> PowerCons { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Whether this power's effective rank counts toward the Resolve calculation.
    /// Null means "use category default" (Movement and Sensory default to false; all others true).
    /// Set explicitly in powers.json only for exceptions to the category rule.
    /// </summary>
    public bool? AffectsResolve { get; init; }

    /// <summary>
    /// Which fields have been checked against the rulebook: any of
    /// range, rank_type, cost, prerequisite, description, pros_cons.
    /// Replaces the old single needs_review boolean, which could not distinguish
    /// a verified cost from a verified description.
    /// </summary>
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];

    /// <summary>Where the entry was verified from, e.g. "Ultimate Edition, Ch.2 Powers, p.21".</summary>
    public string? SourceRef { get; init; }

    public string? Notes { get; init; }

    public bool IsVerified(string field) =>
        VerifiedFields.Contains(field, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when every field that affects HP cost or rank has been verified.</summary>
    public bool MechanicsVerified =>
        IsVerified("range") && IsVerified("rank_type") && IsVerified("cost") && IsVerified("prerequisite");

    public bool DescriptionVerified => IsVerified("description");

    /// <summary>
    /// True when a mechanically significant field is still unverified. Descriptions are
    /// excluded — they do not affect any calculation, and are reported separately.
    /// </summary>
    public bool NeedsReview => !MechanicsVerified;
}
