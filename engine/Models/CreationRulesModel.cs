namespace ProwlersAndParagonsAutomation.Engine.Models;

public record FlawRules
{
    public int MinAtCreation { get; init; }
    public int MaxAtCreation { get; init; }
    public int MaxEver { get; init; }
    public int ExtraFlawCostHp { get; init; }
}

public record OptionalPackage
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Cost { get; init; }
    public int AbilitiesRank { get; init; }
    public int TalentsRank { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>
/// The floor and ceiling on any Ability or Talent rank (Ch.2, p.17 and p.18, which state the
/// same sentence once for each).
///
/// <para><c>maximum</c> is deliberately not bound: the JSON records it as the prose
/// "trait_cap (set by tier)", because the ceiling is the tier's and lives on
/// <see cref="TierModel.TraitCapRank"/>. Only the floor is a number here.</para>
/// </summary>
public record TraitRankLimits
{
    /// <summary>
    /// The lowest rank an Ability or Talent can have: 1d. Not zero — a character has all six
    /// Abilities and all twelve Talents, and "ordinary people have 2d in every" one of them.
    /// </summary>
    public int Minimum { get; init; } = 1;

    public string Notes { get; init; } = "";
}

public record CreationRulesModel
{
    public IReadOnlyList<string> Sequence { get; init; } = [];
    public FlawRules FlawRules { get; init; } = new();
    public IReadOnlyList<OptionalPackage> OptionalPackages { get; init; } = [];
    public TraitRankLimits TraitRankLimits { get; init; } = new();
}
