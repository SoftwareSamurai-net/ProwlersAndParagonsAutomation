namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// A Power as printed in the rulebook. Every entry carries a Range, a rank type
/// and a cost, and those three fields together determine how it is paid for.
/// </summary>
public record PowerModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";

    /// <summary>self | touch | ranged | zone | special</summary>
    public string Range { get; init; } = "";

    /// <summary>
    /// power | baseline | default | special.
    /// <c>default</c> means the Power has no rank at all — the rulebook has you use
    /// Toughness or Willpower (by Source) in its place when another Power targets it.
    /// <c>baseline</c> means it has a rank derived from another Trait via <see cref="Prerequisite"/>.
    /// </summary>
    public string RankType { get; init; } = "";

    /// <summary>per_rank | flat | per_unit | per_rank_variable | flat_variable | special</summary>
    public string CostType { get; init; } = "";

    /// <summary>
    /// HP per purchased rank when CostType is per_rank. 0.5 for Powers priced at
    /// "1 Hero Point per 2 ranks". Null for every other cost type.
    /// </summary>
    public double? CostPerRank { get; init; }

    /// <summary>Total HP when CostType is flat. Null otherwise.</summary>
    public int? CostFlat { get; init; }

    /// <summary>HP per unit when CostType is per_unit (see <see cref="CostUnitLabel"/>).</summary>
    public int? CostPerUnit { get; init; }

    /// <summary>What a unit is for per_unit Powers: "immunity", "power level", "resolve".</summary>
    public string? CostUnitLabel { get; init; }

    /// <summary>
    /// True when each unit bought is a different thing the player names, rather than more of
    /// the same thing. Immunity alone: "Each immunity is named and paid for separately"
    /// (Ch.2 p.31). Determination's Resolve and Alternate Form's power levels are counted.
    /// A character records the names in <see cref="SelectedPower.UnitNames"/>.
    /// </summary>
    public bool UnitsAreNamed { get; init; }

    /// <summary>
    /// <see cref="CostUnitLabel"/> for a count: "1 immunity", "3 immunities". "unit" where the
    /// data carries no label.
    /// </summary>
    public string UnitNoun(int count)
    {
        var label = string.IsNullOrEmpty(CostUnitLabel) ? "unit" : CostUnitLabel;
        if (count == 1) return label;
        return label.EndsWith('y') && !label.EndsWith("ey", StringComparison.Ordinal)
            ? label[..^1] + "ies"
            : label + "s";
    }

    /// <summary>
    /// Selectable cost values for the two variable cost types, keyed by variant name —
    /// e.g. Omni-Power { narrow: 3, broad: 5 }, Stretching { close_range: 1, ... }.
    /// The value is HP per rank for per_rank_variable and total HP for flat_variable.
    /// </summary>
    public IReadOnlyDictionary<string, double>? CostVariants { get; init; }

    /// <summary>
    /// 0 when no ranks can be purchased — the Power has no rank, or it is bought for a
    /// flat/per-unit price. Null means ranks are purchasable up to the game's Trait Cap.
    /// </summary>
    public int? MaxRank { get; init; }

    public int DicePerRank { get; init; } = 1;

    /// <summary>
    /// Project-authored paraphrase, not rulebook text. Never listed in
    /// <see cref="VerifiedFields"/> — see <see cref="DescriptionVerified"/>.
    /// </summary>
    public string Description { get; init; } = "";

    public PowerPrerequisiteModel? Prerequisite { get; init; }

    // There is deliberately no list of generic Pros and Cons here. The rulebook states
    // applicability inside each generic option instead, so it is derived — see
    // ProConApplicability. The lists that used to live here were this project's guesses.

    /// <summary>
    /// Generic options this Power's own printed text names, overriding the Range constraint
    /// the option itself states. Empty for all but a handful of entries.
    ///
    /// <para><b>This is not the <c>available_pros</c> list that was removed, and must never
    /// grow into one.</b> That list was a guess about which options suited a Power, and it
    /// filtered absolutely. This one records the opposite and much narrower thing: the
    /// rulebook printing, inside a Power's entry, an instruction to apply a named generic
    /// option that the option's own Range rule would otherwise forbid. Force Field is Self
    /// range and its entry reads "Apply the Zone Pro to shield large areas, the Ranged Pro
    /// to shield things at a distance, or the Area Pro to shield large areas at a distance"
    /// (Ch.2 p.29) — and T-Kay, printed on p.143, carries Force Field 12d (Zone).</para>
    ///
    /// <para>An entry here needs a sentence of the Power's own text behind it, which it
    /// carries in <see cref="ProAllowanceModel.Reason"/> and a test asserts is present. It is
    /// the same shape as Deflection covering both attack types, which the book also states as
    /// prose rather than as a marked PRO.</para>
    ///
    /// <para><b>Pros only, and Range only.</b> The name says Pros and the applicability check
    /// enforces it, so an id here cannot quietly exempt a Con that shares it; and it overrides
    /// the option's Range rule alone, never its rank-type rule. Both were true of the field's
    /// first version by accident rather than by construction.</para>
    /// </summary>
    public IReadOnlyList<ProAllowanceModel> ProsAllowedByOwnText { get; init; } = [];

    /// <summary>Pros printed inside this Power's own rulebook entry.</summary>
    public IReadOnlyList<PowerProConModel> PowerPros { get; init; } = [];

    /// <summary>Cons printed inside this Power's own rulebook entry.</summary>
    public IReadOnlyList<PowerProConModel> PowerCons { get; init; } = [];

    /// <summary>
    /// Words this Power can be <em>found</em> by, that its <see cref="Description"/> does not
    /// necessarily print — a searchable vocabulary, not a category label. Two hosts read it:
    /// the browser passes it to <c>OptionRow</c> as <c>Keywords</c> so the Powers tab's filter
    /// box can match on a word the printed description never uses, and the MCP server's
    /// <c>search_powers</c> weights a tag match at 6 points, between a name/id match and a
    /// category match — see <c>CharacterTools.Score</c>.
    ///
    /// <para><b>Not a mechanical field, and not claimed as one.</b> It is absent from
    /// <see cref="VerifiedFields"/>'s closed set (range, rank_type, cost, prerequisite,
    /// description, pros_cons) because no cost, rank or validity ever reads it — the same
    /// reasoning CLAUDE.md records for <c>linked_ability</c> on a Talent. Every word here is
    /// chosen by reading this Power's own printed entry and asking what a player would call the
    /// effect, not transcribed from the rulebook, so it carries no <see cref="SourceRef"/>
    /// citation and is not something a rules audit re-verifies against a page.</para>
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Whether this power's effective rank counts toward the Resolve calculation.
    /// Null means "use category default" (Movement and Sensory default to false; all others true).
    /// Set explicitly in powers.json only for exceptions to the category rule.
    /// </summary>
    public bool? AffectsResolve { get; init; }

    /// <summary>
    /// The nominated Trait ids that make this Power count towards Resolve <em>despite</em>
    /// <see cref="AffectsResolve"/> being false — the Ch.5 p.83 carve-out, and today the
    /// Expertise entry alone carries one.
    ///
    /// <para><b>Why a list and not a second bool.</b> p.83 exempts "Expertise (except for
    /// combat skills)", which is not an exemption but a carve-out: whether a given Expertise
    /// counts depends on the Trait the player nominated on <c>SelectedPower.BaselineTraitId</c>,
    /// and one flag on one entry has no room to say that. So <see cref="AffectsResolve"/> stays
    /// the default answer and this names the nominations that overturn it.</para>
    ///
    /// <para><b>It holds Ability and Talent ids only</b>, which is the whole scope Ch.2 p.28
    /// gives a nomination — "Your specialization must fall under one of your Abilities or
    /// Talents". A nominated <em>Power</em> is not listed here and never should be, because it is
    /// not a legal Expertise in the first place: <c>CharacterValidator</c> reports it as
    /// <c>EXPERTISE_NOMINATION_NOT_A_TRAIT</c> rather than the engine finding it an answer.</para>
    ///
    /// <para>Empty on every other entry, which is what makes the carve-out inert everywhere the
    /// book does not print one.</para>
    /// </summary>
    public IReadOnlyList<string> AffectsResolveWhenNominated { get; init; } = [];

    /// <summary>
    /// The cheapest a table may re-price this Power at, when its own printed entry hands the
    /// price to the GM. Null on every entry whose price the book states flatly, which is all of
    /// them but one.
    ///
    /// <para><b>Immortality is the one, and this is the range its own <see cref="Description"/>
    /// prints</b> — "In a game where Heroes can die, GMs should charge more for this — somewhere
    /// between 6 and 12 Hero Points" (Ch.2 p.31). <b>The prose is the source and these two
    /// numbers are a transcription of it</b>, which is why
    /// <c>PowerDataTests.ImmortalitysCampaignCostRangeIsWhatItsOwnDescriptionSays</c> reads that
    /// sentence back out of the shipped data and requires it to still say 6 and 12. A
    /// range restated in a second place is a range that can drift from the one the book printed,
    /// and the point of the structured pair is that something mechanical can read it — not that
    /// there are now two answers.</para>
    ///
    /// <para><b>Why the numbers are here rather than as literals in the engine.</b>
    /// <c>CharacterValidator</c> bounds a campaign's house price by these and
    /// <c>CostCalculator</c> charges it, so a figure spelled in C# would be a price this
    /// repository had invented — unreachable by the rules audit that holds every other price to a
    /// page, and exactly the thing <c>data/rules/</c> exists to prevent.</para>
    ///
    /// <para><b>It is not a price and nothing is charged from it.</b> <see cref="CostFlat"/> is
    /// still what this Power costs. This is the interval a <em>table</em> may move that price
    /// into, and only a campaign can do the moving: a character with no campaign carries no house
    /// price at all, and prices from <see cref="CostFlat"/> exactly as it always has.</para>
    /// </summary>
    public int? CampaignCostMin { get; init; }

    /// <summary>
    /// The dearest a table may re-price this Power at — the other end of
    /// <see cref="CampaignCostMin"/>, and null wherever that is.
    /// </summary>
    public int? CampaignCostMax { get; init; }

    /// <summary>
    /// Whether this Power's own entry hands its price to the table, which is what carrying both
    /// ends of the range means. False everywhere else, so a caller asking "may a campaign
    /// re-price this?" has one answer to read rather than two nullables to combine.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasCampaignCostRange => CampaignCostMin is not null && CampaignCostMax is not null;

    /// <summary>
    /// Which fields have been checked against the rulebook: any of
    /// range, rank_type, cost, prerequisite, description, pros_cons.
    /// Replaces the old single needs_review boolean, which could not distinguish
    /// a verified cost from a verified description.
    /// </summary>
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];

    /// <summary>Where the entry was verified from, e.g. "Ultimate Edition, Ch.2 Powers, p.21".</summary>
    public string? SourceRef { get; init; }

    /// <summary>
    /// True where the Power's own entry says to buy it again — "Buy this Power multiple times
    /// if you want multiple forms" (Alternate Form, p.21) and the same sentence on Duplication
    /// (p.27). The same mark an option that may be bought again carries, and it means the same
    /// thing: a second purchase is what the book asked for, not a Power listed twice, so
    /// <see cref="CharacterValidator"/> does not warn <c>DUPLICATE_POWER</c> on it. A test holds
    /// the marked entries to exactly those whose printed text says so.
    /// </summary>
    public bool Repeatable { get; init; }

    public string? Notes { get; init; }

    public bool IsVerified(string field) =>
        VerifiedFields.Contains(field, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when every field that affects HP cost or rank has been verified.</summary>
    public bool MechanicsVerified =>
        IsVerified("range") && IsVerified("rank_type") && IsVerified("cost") && IsVerified("prerequisite");

    public bool DescriptionVerified => IsVerified("description");

    /// <summary>
    /// True when a mechanically significant field is still unverified. Descriptions are
    /// excluded — they do not affect any calculation, and are reported separately.
    /// </summary>
    public bool NeedsReview => !MechanicsVerified;
}
