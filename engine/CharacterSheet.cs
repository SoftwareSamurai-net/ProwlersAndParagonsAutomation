namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Records a perk selected for a character. Units is always 1 for flat-cost perks;
/// for per_unit perks it holds the quantity purchased (e.g. number of contact types).
/// </summary>
public record SelectedPerk(string PerkId, int Units = 1, string? NarrativeDetail = null);

/// <summary>
/// Records a flaw selected for a character, pairing the flaw id with any
/// narrative detail required by the flaw's NarrativeConstraint.
/// </summary>
public record SelectedFlaw(string FlawId, string? NarrativeDetail = null);

/// <summary>
/// Records which variant of a variable-cost pro/con the player has chosen.
/// For a fixed-cost pro/con, VariantKey is null.
/// For a variable-cost one (e.g. Charges, Area/Burst), VariantKey is the
/// key from CostModifierRange (e.g. "3_per_scene", "area").
/// </summary>
public record SelectedProCon(string Id, string? VariantKey = null)
{
    /// <summary>
    /// Quantity for a Power-specific Pro or Con that scales — how many extra Sources
    /// Also X covers, for instance. Null means "however many units the Power itself has",
    /// which is what Alternate Form's Independent Forms wants.
    /// </summary>
    public int? Units { get; init; }
}

/// <summary>
/// A power as it appears on the character sheet: the power id,
/// how many ranks were purchased (above the free baseline), and
/// which pros/cons have been applied.
/// </summary>
public record SelectedPower(
    string PowerId,
    int PurchasedRanks,
    IReadOnlyList<SelectedProCon> Pros,
    IReadOnlyList<SelectedProCon> Cons)
{
    public SelectedPower(string powerId, int purchasedRanks)
        : this(powerId, purchasedRanks, [], []) { }

    /// <summary>
    /// Which variant was chosen for a Power whose cost varies — a key from
    /// <see cref="Models.PowerModel.CostVariants"/> such as "broad" for Omni-Power
    /// or "extreme_range" for Stretching. Null for fixed-cost Powers.
    /// </summary>
    public string? CostVariantKey { get; init; }

    /// <summary>
    /// How many units were bought for a per_unit Power: immunities for Immunity,
    /// Resolve for Determination, power levels for Alternate Form. Always 1 otherwise.
    /// </summary>
    public int Units { get; init; } = 1;

    /// <summary>
    /// The Trait the player nominated for a baseline_selected_trait Power (Boost,
    /// Expertise). Holds an ability, talent or power id. For Boost this also sets
    /// the per-rank cost, which matches the affected Trait's own cost per rank.
    /// </summary>
    public string? BaselineTraitId { get; init; }
}

/// <summary>
/// Mutable state object for a character being built in the wizard.
/// All calculators and validators receive this and read from it.
/// </summary>
public class CharacterSheet
{
    /// <summary>Id of the selected tier, or null if not yet chosen.</summary>
    public string? SelectedTierId { get; set; }

    /// <summary>
    /// Id of the optional package applied (Civilian/Hero/Superhero), or null.
    /// The package sets a floor on ability and talent ranks — it does not
    /// prevent buying higher ranks on top.
    /// </summary>
    public string? SelectedPackageId { get; set; }

    /// <summary>Purchased ability ranks, keyed by ability id.</summary>
    public Dictionary<string, int> AbilityRanks { get; } = new();

    /// <summary>Purchased talent ranks, keyed by talent id.</summary>
    public Dictionary<string, int> TalentRanks { get; } = new();

    /// <summary>Powers on this character sheet.</summary>
    public List<SelectedPower> SelectedPowers { get; } = new();

    /// <summary>Perks purchased for this character.</summary>
    public List<SelectedPerk> Perks { get; } = new();

    /// <summary>Flaws selected for this character (min 1, max 3 at creation).</summary>
    public List<SelectedFlaw> Flaws { get; } = new();

    // ── Convenience helpers ───────────────────────────────────────────────

    public int GetAbilityRank(string abilityId) =>
        AbilityRanks.GetValueOrDefault(abilityId, 0);

    public int GetTalentRank(string talentId) =>
        TalentRanks.GetValueOrDefault(talentId, 0);

    public SelectedPower? GetPower(string powerId) =>
        SelectedPowers.FirstOrDefault(p => p.PowerId == powerId);

    public bool HasPower(string powerId) =>
        SelectedPowers.Any(p => p.PowerId == powerId);

    // ── Narrative & flavour (CLI-facing, not used by engine logic) ────────

    public string Name { get; set; } = "";
    public string Appearance { get; set; } = "";
    public string Motivation { get; set; } = "";
    public string Quote { get; set; } = "";
    public List<string> Connections { get; } = [];
    public List<string> Gear { get; } = [];
}
