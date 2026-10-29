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

    /// <summary>
    /// The entry's own limit on earning or spending Resolve through this Perk — Fourth Wall's once
    /// per scene, Great Wealth's once per issue. Null on every Perk whose text sets none. Words for
    /// the sheet beside the Resolve counter, never enforced.
    /// </summary>
    public PerkResolveLimit? ResolveLimit { get; init; }

    /// <summary>
    /// Pet/Sidekick's companion rule: what each unit buys, the ceiling, and what the companion
    /// does to its owner's Resolve. Null on every other Perk. Read by <c>Companions</c>, which
    /// checks an owner and their companion together; a test holds every field to the entry's text.
    /// </summary>
    public PerkCompanionRule? Companion { get; init; }

    public bool NeedsReview { get; init; }
}

/// <summary>What Ch.2 p.55 says a Pet/Sidekick companion is, field by field.</summary>
public sealed record PerkCompanionRule
{
    /// <summary>"Every Hero Point you spend on this Perk gives you 10 Hero Points to create this character."</summary>
    public int HeroPointsPerUnit { get; init; }

    /// <summary>"Can't have more than half your Hero Points" — the owner's budget divided by this.</summary>
    public int CapDivisorOfOwner { get; init; }

    /// <summary>"They must abide by the game's Trait Cap" — the owner's.</summary>
    public bool AbidesByTraitCap { get; init; }

    /// <summary>"Earns 1 Hero Point for every 2 that you earn." Printed, not computed: nothing tracks earning.</summary>
    public int OwnerHeroPointsPerCompanionPoint { get; init; }

    /// <summary>"Pets and sidekicks ... lack Resolve."</summary>
    public bool HasResolve { get; init; }

    /// <summary>"If your companion has higher Trait ranks than you, use their Traits when determining your Resolve."</summary>
    public bool OwnerResolveUsesHigherTraits { get; init; }

    /// <summary>"If your companion has Flaws, you earn Resolve as if that character were a Hero."</summary>
    public bool FlawsEarnOwnerResolve { get; init; }

    /// <summary>The page the Perk is printed on, in Ch.2.</summary>
    public int Page { get; init; }

    /// <summary>The Hero Points a purchase of <paramref name="units"/> gives its companion. Wide, so a nonsense unit count cannot wrap.</summary>
    public long HeroPointsFor(int units) => (long)units * HeroPointsPerUnit;

    /// <summary>The most Hero Points a companion may have: <paramref name="ownerHeroPoints"/> divided as the entry says, rounded down.</summary>
    public int CeilingFor(int ownerHeroPoints) =>
        CapDivisorOfOwner <= 0 ? ownerHeroPoints : ownerHeroPoints / CapDivisorOfOwner;

    /// <summary>The most units an owner with <paramref name="ownerHeroPoints"/> may buy for one companion.</summary>
    public int MostUnitsFor(int ownerHeroPoints) =>
        HeroPointsPerUnit <= 0 ? 0 : CeilingFor(ownerHeroPoints) / HeroPointsPerUnit;
}

/// <summary>How often a Perk lets its holder earn or spend Resolve, in the entry's own terms.</summary>
public sealed record PerkResolveLimit
{
    /// <summary><c>earn</c> or <c>spend</c>.</summary>
    public string Direction { get; init; } = "";

    /// <summary>How many points each time — 1 on both entries that set a limit.</summary>
    public int Points { get; init; }

    /// <summary>When it applies, quoted from the entry: "to make a truly absurd purchase".</summary>
    public string Occasion { get; init; } = "";

    /// <summary><c>scene</c> or <c>issue</c>.</summary>
    public string OncePer { get; init; } = "";

    /// <summary>The page the Perk is printed on, in Ch.2.</summary>
    public int Page { get; init; }
}

/// <summary>One of a Perk's separate questions: the short label it prints under, and the question.</summary>
public sealed record PerkPrompt
{
    /// <summary>What the answer is printed after on the sheet — "Provides".</summary>
    public string Label { get; init; } = "";

    /// <summary>What the editor asks — "What do they provide you with?"</summary>
    public string Ask { get; init; } = "";
}
