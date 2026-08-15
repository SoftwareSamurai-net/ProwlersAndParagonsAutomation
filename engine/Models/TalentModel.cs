namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// The one mechanical thing a Talent's entry prints beyond its rank: Medicine treats wounds on
/// a Hard (2) roll healing 1 damage per net success, and Technology repairs objects the same
/// way (Ch.2 p.18). Two Talents of the twelve carry one.
///
/// <para>Play rules rather than creation rules, which is why nothing here consumes them — but
/// they were in the rules file and read by nothing, so no test could hold them to the page.</para>
/// </summary>
public record TalentSpecialUse
{
    public string Action { get; init; } = "";

    /// <summary>The challenge roll's threshold — 2, which is Hard.</summary>
    public int Threshold { get; init; }

    /// <summary>Medicine only: treating yourself is Daunting (3) instead.</summary>
    public int? ThresholdSelf { get; init; }

    public int? DamageHealedPerNetSuccess { get; init; }
    public int? DamageRepairedPerNetSuccess { get; init; }

    public string Frequency { get; init; } = "";
}

public record TalentModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int CostPerRank { get; init; }
    public int MinRank { get; init; }
    public int? MaxRank { get; init; }
    public int OrdinaryHumanRank { get; init; }
    public string LinkedAbility { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>
    /// The rulebook's TALENT RANKS table (Ch.2 p.18): 1d Clueless, 2d Unskilled, 3d Proficient,
    /// 4d Advanced, 5d Expert, 6d Master. It stops at 6d because above that is superhuman.
    /// </summary>
    public IReadOnlyDictionary<string, string> RankGuide { get; init; } = new Dictionary<string, string>();

    /// <summary>Present on Medicine and Technology; null on the other ten.</summary>
    public TalentSpecialUse? SpecialUse { get; init; }
}
