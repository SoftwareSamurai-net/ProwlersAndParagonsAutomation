namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// Describes how a baseline-rank Power derives its free baseline rank. The rulebook
/// prints this in the Power's Rank field as "Baseline Rank (X)"; purchased ranks are
/// then added on top of the baseline.
/// <list type="bullet">
///   <item><term>baseline_equal</term><description>Baseline = ability rank (Evasion = Agility)</description></item>
///   <item><term>baseline_half</term><description>Baseline = ⌈ability rank / 2⌉ (Armor = ⌈Toughness / 2⌉)</description></item>
///   <item><term>baseline_fixed</term><description>Baseline is a flat value (Running = 3d). See <see cref="FixedValue"/>.</description></item>
///   <item><term>baseline_greater_of</term><description>Baseline = the greater of <see cref="Ability"/> and the effective ranks of <see cref="Powers"/> (Strike = Might or Martial Arts)</description></item>
///   <item><term>baseline_selected_trait</term><description>Baseline = the rank of a Trait the player nominates at purchase (Boost, Expertise). See <see cref="SelectedPower.BaselineTraitId"/>.</description></item>
/// </list>
/// </summary>
public record PowerPrerequisiteModel
{
    /// <summary>
    /// Ability id providing the baseline. Null for baseline_fixed and
    /// baseline_selected_trait, where no ability is fixed in advance.
    /// </summary>
    public string? Ability { get; init; }

    /// <summary>
    /// baseline_equal | baseline_half | baseline_fixed | baseline_greater_of | baseline_selected_trait
    /// </summary>
    public string Relationship { get; init; } = "";

    /// <summary>
    /// Power ids that also contribute to the baseline, used by baseline_greater_of.
    /// Strike lists martial_arts here.
    /// </summary>
    public IReadOnlyList<string> Powers { get; init; } = [];

    public string Description { get; init; } = "";

    /// <summary>Only meaningful when Relationship == "baseline_fixed" (Running = 3).</summary>
    public int? FixedValue { get; init; }
}
