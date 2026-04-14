namespace ProwlersAndParagonsAutomation.Engine.Models;

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
    public IReadOnlyDictionary<string, string> RankGuide { get; init; } = new Dictionary<string, string>();
}
