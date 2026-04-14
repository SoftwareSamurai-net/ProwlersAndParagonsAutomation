namespace ProwlersAndParagonsAutomation.Engine.Models;

public record PowerModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";

    /// <summary>
    /// Cost per purchased rank. Currently always 1.0 in data.
    /// Typed as double? to accommodate future 0.5 (Brute Option) values.
    /// Null when cost_type is "special" — handled separately by CostCalculator.
    /// </summary>
    public double? CostPerRank { get; init; }

    /// <summary>per_rank | flat | special</summary>
    public string CostType { get; init; } = "per_rank";

    public int? MaxRank { get; init; }
    public int DicePerRank { get; init; } = 1;
    public string Description { get; init; } = "";
    public PowerPrerequisiteModel? Prerequisite { get; init; }
    public IReadOnlyList<string> AvailablePros { get; init; } = [];
    public IReadOnlyList<string> AvailableCons { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool NeedsReview { get; init; }
    public string? Notes { get; init; }
}
