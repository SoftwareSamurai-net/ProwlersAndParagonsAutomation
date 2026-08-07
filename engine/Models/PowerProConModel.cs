namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// A Pro or Con printed inside a single Power's own entry in Chapter 2, as opposed to
/// the generic ones in pros.json / cons.json that can be applied to many Powers.
///
/// <para>These are not just extra generic entries: the generic ones are all flat Hero
/// Point modifiers, while several of these change the Power's <b>rate</b> instead —
/// Constructs' Devices is +2 Hero Points <i>per rank</i>. A few also scale with a
/// quantity, such as how many extra Sources a Power can affect.</para>
/// </summary>
public record PowerProConModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// flat | per_rank | flat_variable | per_rank_variable | per_unit | per_rank_per_unit
    /// </summary>
    public string CostType { get; init; } = "";

    /// <summary>Flat Hero Point change. Positive for a Pro, negative for a Con.</summary>
    public int? CostModifier { get; init; }

    /// <summary>Change to the Power's Hero Points per rank, for the per_rank types.</summary>
    public double? CostPerRank { get; init; }

    /// <summary>Hero Points per unit, for per_unit (see <see cref="CostUnitLabel"/>).</summary>
    public int? CostPerUnit { get; init; }

    /// <summary>What a unit is: "power level", "additional Source".</summary>
    public string? CostUnitLabel { get; init; }

    /// <summary>Flat values to pick between, for flat_variable — Only X is -2 or -4.</summary>
    public IReadOnlyDictionary<string, int>? CostModifierRange { get; init; }

    /// <summary>Per-rank rates to pick between, for per_rank_variable.</summary>
    public IReadOnlyDictionary<string, double>? CostPerRankRange { get; init; }

    public string Description { get; init; } = "";

    /// <summary>True when picking this requires a variant key on the selection.</summary>
    public bool NeedsVariant =>
        CostType is "flat_variable" or "per_rank_variable";

    /// <summary>True when this changes the per-rank rate rather than the total.</summary>
    public bool ModifiesRate =>
        CostType is "per_rank" or "per_rank_variable" or "per_rank_per_unit";
}
