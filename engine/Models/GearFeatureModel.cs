namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// A custom feature that can be added to a piece of mundane gear, from Ch.6 (p.92).
///
/// <para>These are the only part of gear that costs Hero Points. Mundane gear itself is
/// free and untracked, and anything more capable than a custom feature is a Power with the
/// Item Con instead.</para>
/// </summary>
public record GearFeatureModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>flat | flat_variable</summary>
    public string CostType { get; init; } = "flat";

    /// <summary>HP cost for a flat feature. Null when the feature is graded.</summary>
    public int? Cost { get; init; }

    /// <summary>
    /// Keyed grades for the two features the rulebook prices at 1 to 2 HP, e.g.
    /// { "accurate": 1, "very_accurate": 2 }. The chosen key lives on
    /// <see cref="SelectedGearFeature.GradeKey"/>.
    /// </summary>
    public IReadOnlyDictionary<string, int>? CostRange { get; init; }

    /// <summary>
    /// What the rulebook says the feature can be added to — "armor", "personal weapon",
    /// "any item" and so on. Advisory: the GM approves gear customisation either way, so
    /// nothing enforces it.
    /// </summary>
    public string AppliesTo { get; init; } = "";

    public string Description { get; init; } = "";
    public string? Notes { get; init; }
    public string SourceRef { get; init; } = "";
}
