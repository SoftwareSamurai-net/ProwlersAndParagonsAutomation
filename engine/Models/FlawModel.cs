namespace ProwlersAndParagonsAutomation.Engine.Models;

public record FlawModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>regular | plot_hook | condition | plot_hook_and_condition</summary>
    public string FlawType { get; init; } = "regular";

    public string Description { get; init; } = "";
    public string? NarrativeConstraint { get; init; }
    public bool NeedsReview { get; init; }
}
