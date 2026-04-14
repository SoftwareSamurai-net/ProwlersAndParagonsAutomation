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

public record CreationRulesModel
{
    public IReadOnlyList<string> Sequence { get; init; } = [];
    public FlawRules FlawRules { get; init; } = new();
    public IReadOnlyList<OptionalPackage> OptionalPackages { get; init; } = [];
}
