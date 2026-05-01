namespace ProwlersAndParagonsAutomation.Engine.Models;

public record PerkModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>flat | per_unit</summary>
    public string CostType { get; init; } = "flat";

    /// <summary>HP cost for flat-cost perks.</summary>
    public int? Cost { get; init; }

    /// <summary>HP cost per unit for per_unit perks.</summary>
    public int? CostPerUnit { get; init; }

    /// <summary>Human-readable label for one unit (e.g. "contact type", "Hero Point (= 10 Hero Points for companion)").</summary>
    public string? UnitLabel { get; init; }

    public string Description { get; init; } = "";
    public string? NarrativeConstraint { get; init; }
    public bool NeedsReview { get; init; }
}
