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

    /// <summary>
    /// The separate questions <see cref="NarrativeConstraint"/> asks, each answered in its own box
    /// and joined into one <c>SelectedPerk.NarrativeDetail</c> — Patron's "who they are, what they
    /// provide, and what they expect in return" is three questions, and one line invited one
    /// answer. Empty where the constraint is one question.
    /// </summary>
    public IReadOnlyList<PerkPrompt> NarrativePrompts { get; init; } = [];

    /// <summary>
    /// Whether the constraint is asked once per unit bought — Contacts buys one contact type per
    /// Hero Point and asks each to be named, so two units with one name is half an answer.
    /// </summary>
    public bool NarrativePerUnit { get; init; }

    /// <summary>
    /// The entry's own rule that a character with a secret identity decides whether the Perk
    /// belongs to their civilian identity or their heroic alter ego — Fame and Reputation.
    /// </summary>
    public bool IdentityChoice { get; init; }

    public bool NeedsReview { get; init; }
}

/// <summary>One of a Perk's separate questions: the short label it prints under, and the question.</summary>
public sealed record PerkPrompt
{
    /// <summary>What the answer is printed after on the sheet — "Provides".</summary>
    public string Label { get; init; } = "";

    /// <summary>What the editor asks — "What do they provide you with?"</summary>
    public string Ask { get; init; } = "";
}
