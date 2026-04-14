namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// Describes how a power's baseline rank is derived from a character ability.
/// <list type="bullet">
///   <item><term>baseline_equal</term><description>Baseline = ability rank (e.g. Strike = Might)</description></item>
///   <item><term>baseline_half</term><description>Baseline = ⌈ability rank / 2⌉ (e.g. Armor = ⌈Toughness / 2⌉)</description></item>
///   <item><term>baseline_fixed</term><description>Baseline is a fixed value regardless of abilities (e.g. Running = 3d). See <see cref="FixedValue"/>.</description></item>
/// </list>
/// </summary>
public record PowerPrerequisiteModel
{
    /// <summary>Ability id that provides the baseline, or null for baseline_fixed.</summary>
    public string? Ability { get; init; }

    /// <summary>baseline_equal | baseline_half | baseline_fixed</summary>
    public string Relationship { get; init; } = "";

    public string Description { get; init; } = "";

    /// <summary>
    /// Only meaningful when Relationship == "baseline_fixed".
    /// Not present in JSON — populated by RulesRepository post-load fixup.
    /// </summary>
    public int? FixedValue { get; init; }
}
