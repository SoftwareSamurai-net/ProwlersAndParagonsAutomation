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

    /// <summary>
    /// Generic Pros from pros.json thought to suit this Power. Unlike
    /// <see cref="PowerPros"/> these are a project judgement, not printed in the entry —
    /// the rulebook states applicability in each generic Pro instead.
    /// </summary>
    public IReadOnlyList<string> AvailablePros { get; init; } = [];

    /// <summary>Generic Cons from cons.json thought to suit this Power. See <see cref="AvailablePros"/>.</summary>
    public IReadOnlyList<string> AvailableCons { get; init; } = [];

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
