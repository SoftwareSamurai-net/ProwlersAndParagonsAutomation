namespace ProwlersAndParagonsAutomation.Engine.Models;

public record ProModel : IGenericProCon
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// Flat HP cost added to the power. Null when cost varies by variant — see CostModifierRange.
    /// </summary>
    public int? CostModifier { get; init; }

    /// <summary>
    /// Keyed variant costs, e.g. { "area": 2, "burst": 1 }.
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
