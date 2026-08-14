namespace ProwlersAndParagonsAutomation.Engine.Models;

public record ConModel : IGenericProCon
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// Flat HP discount (stored as a negative integer in JSON, e.g. -2).
    /// Null when cost varies by variant — see CostModifierRange.
    /// </summary>
    public int? CostModifier { get; init; }

    /// <summary>
    /// Keyed variant discounts, e.g. { "6_per_scene": -1, "3_per_scene": -2, "1_per_scene": -4 }.
    /// The selected variant key is stored on SelectedProCon.VariantKey.
    /// </summary>
    public IReadOnlyDictionary<string, int>? CostModifierRange { get; init; }

    /// <summary>flat | special</summary>
    public string CostType { get; init; } = "flat";

    public IReadOnlyList<string> ApplicableTo { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> AppliesToRanges { get; init; } = [];

    /// <inheritdoc />
    public IReadOnlyList<string> AppliesToRankTypes { get; init; } = [];

    /// <inheritdoc />
    public bool Repeatable { get; init; }

    /// <inheritdoc />
    public string? ApplicabilityCaveat { get; init; }

    public string Description { get; init; } = "";
    public string? NarrativeConstraint { get; init; }
    public bool NeedsReview { get; init; }
    public string? Notes { get; init; }
}
