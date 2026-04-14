namespace ProwlersAndParagonsAutomation.Engine.Models;

public record TierModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int HeroPoints { get; init; }
    public int TraitCapRank { get; init; }
    public string Description { get; init; } = "";
    public string? Notes { get; init; }
    public bool NeedsReview { get; init; }
}
