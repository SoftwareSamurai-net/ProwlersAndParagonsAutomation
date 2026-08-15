namespace ProwlersAndParagonsAutomation.Engine.Models;

public record FlawRules
{
    public int MinAtCreation { get; init; }
    public int MaxAtCreation { get; init; }
    public int MaxEver { get; init; }
    public int ExtraFlawCostHp { get; init; }

    /// <summary>
    /// What the four numbers above leave unsaid: which Flaws cost, and that a Flaw may be
    /// swapped between stories for nothing if the GM agrees (Ch.2 p.62).
    /// </summary>
    public string Notes { get; init; } = "";
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

    /// <summary>
    /// Prose, not a number: the maximum is the tier's Trait Cap, so it varies by game rather
    /// than being a constant this file could hold.
    /// </summary>
    public string Maximum { get; init; } = "";

    public string Notes { get; init; } = "";
}

/// <summary>
/// How a character earns and spends Hero Points after creation (Ch.2 p.62).
///
/// <para>Nothing in this tool consumes it — a character generator builds a starting
/// character — but it is rulebook content, and it was sitting in the rules file unread and
/// therefore unlockable by any test. Modelled so it can be held to the page.</para>
/// </summary>
public record AdvancementRules
{
    /// <summary>"GMs should award each player 1 Hero Point roughly every 3 issues."</summary>
    [System.Text.Json.Serialization.JsonPropertyName("hp_per_3_issues")]
    public int HpPer3Issues { get; init; }

    /// <summary>1 HP for defeating an archvillain, saving a city, and the like.</summary>
    public int HpForNoteworthyAchievement { get; init; }

    /// <summary>1 HP for a marriage, a birth, a death — one Hero per story.</summary>
    public int HpForMajorLifeEvent { get; init; }

    public string SpendingNotes { get; init; } = "";

    /// <summary>Hard (never changes) and floating (+1d per 10 HP earned) Trait Caps.</summary>
    public IReadOnlyDictionary<string, string> TraitCapOptions { get; init; } =
        new Dictionary<string, string>();

    public string Retcons { get; init; } = "";
}

/// <summary>
/// The optional rules that let a Trait sit outside the tier's Trait Cap, both printed inside
/// the Overkill Con's own entry (Ch.2 p.52) rather than with the caps.
///
/// <para><b>Not enforced, and deliberately so</b> — both are stated as things a GM "may" do,
/// and the engine is not told which house rules are in play. Recorded so the text can be
/// checked against the book and shown to whoever is deciding.</para>
/// </summary>
public record GlobalCapRules
{
    public string Notes { get; init; } = "";

    /// <summary>
    /// "A Trait with either Con can exceed the Trait Cap by up to 3 ranks in games with a
    /// Trait Cap of at least 9d or by up to 6 ranks in games with a Trait Cap of at least 18d."
    /// </summary>
    public string OverkillWeakException { get; init; } = "";

    /// <summary>
    /// The converse the same passage prints: a GM may require every damaging Trait at or above
    /// a chosen rank to carry Overkill or Weak, and "it should never be lower than 9d".
    /// </summary>
    public string OverkillWeakRequirement { get; init; } = "";

    public string? SourceRef { get; init; }
}

public record CreationRulesModel
{
    public IReadOnlyList<string> Sequence { get; init; } = [];

    /// <summary>Guidance per step of <see cref="Sequence"/>, keyed by step name.</summary>
    public IReadOnlyDictionary<string, string> SequenceNotes { get; init; } =
        new Dictionary<string, string>();

    public FlawRules FlawRules { get; init; } = new();
    public IReadOnlyList<OptionalPackage> OptionalPackages { get; init; } = [];
    public TraitRankLimits TraitRankLimits { get; init; } = new();
    public AdvancementRules Advancement { get; init; } = new();
    public GlobalCapRules GlobalCaps { get; init; } = new();
}
