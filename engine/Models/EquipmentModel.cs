namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// Chapter 6's creation-side equipment, pp.88-93: armour, shields, the Weapon Features
/// glossary, the mundane equipment list, Custom Gear and the Pros and Cons rule for gear.
/// Read from <c>data/rules/equipment.json</c>.
///
/// <para><b>Nothing loads this yet, and that is deliberate.</b> The file is not on
/// <see cref="RulesRepository.DataFileNames"/>, so no host fetches it and
/// <c>RulesRepository</c> exposes no collection for it. Wiring it up — and deciding what the
/// Gear step does with an armour row — is the consumer slice's decision. These types exist so
/// the data is <em>read</em> by something strict: <c>EquipmentDataTests</c> deserializes the
/// file into them with <c>JsonUnmappedMemberHandling.Disallow</c>, which is what stops a key
/// nobody models from sitting in a rules file looking like a source of truth.</para>
///
/// <para><b>The three weapons tables here are a copy.</b> The originals are in
/// <c>data/rules/play/equipment.json</c>, which <c>engine/</c> may not read and which
/// <c>play/</c> cannot do without. A test holds the two copies equal; see the file's own
/// header for the argument.</para>
/// </summary>
public record EquipmentDataModel
{
    public EquipmentHeaderModel Header { get; init; } = new();

    /// <summary>p.88's rule for what a worn suit does to a character.</summary>
    public EquipmentEntryModel ArmorRule { get; init; } = new();

    /// <summary>p.88's nine-row Armor table.</summary>
    public ArmorTableModel ArmorTable { get; init; } = new();

    /// <summary>Bulky and Rigid, the two features the Armor table cites.</summary>
    public IReadOnlyList<ArmorFeatureModel> ArmorFeatures { get; init; } = [];

    /// <summary>p.88's shield rule. A shield is a defence and a weapon row both.</summary>
    public EquipmentEntryModel Shields { get; init; } = new();

    /// <summary>What the Weapon Features glossary is, as opposed to what is in it.</summary>
    public EquipmentEntryModel WeaponFeaturesRule { get; init; } = new();

    /// <summary>The glossary itself: eighteen entries covering nineteen cited names.</summary>
    public IReadOnlyList<WeaponFeatureModel> WeaponFeatures { get; init; } = [];

    /// <summary>The ancient, modern and advanced tables, copied from the play store.</summary>
    public IReadOnlyList<WeaponTableModel> WeaponTables { get; init; } = [];

    /// <summary>p.91: mundane gear is free and untracked, with thirty-six examples.</summary>
    public EquipmentCatalogueModel EquipmentCatalogue { get; init; } = new();

    /// <summary>p.92: the one place gear costs Hero Points.</summary>
    public EquipmentEntryModel CustomGear { get; init; } = new();

    /// <summary>p.93: the Item Con, the 0 HP floor, and the twenty-four common options.</summary>
    public EquipmentEntryModel GearProsAndCons { get; init; } = new();
}

/// <summary>
/// The file's own account of itself: what it holds, why it is here rather than in the play
/// store, why the weapons tables are duplicated, and what was deliberately left out.
/// </summary>
public record EquipmentHeaderModel
{
    public string WhatThisIs { get; init; } = "";
    public string WhyHereAndNotInThePlayStore { get; init; } = "";
    public string TheWeaponTablesAreACopyAndThatIsDeliberate { get; init; } = "";
    public string NotYetLoaded { get; init; } = "";
    public string PlacementNote { get; init; } = "";
    public IReadOnlyList<string> VerifiedFieldsClosedList { get; init; } = [];
    public string DescriptionsAreOurs { get; init; } = "";
    public string DeliberatelyOmitted { get; init; } = "";
    public string SourceRef { get; init; } = "";
}

/// <summary>
/// The envelope every entry in this file carries, around whichever block of facts it holds.
///
/// <para><c>Interpretation</c> is this project's reading of the page and is never a fact
/// field; <c>Ambiguity</c> is the book's own silence. The distinction is the one
/// <c>data/rules/play/</c> settled: a fact field is a claim that the page states the thing.</para>
/// </summary>
public record EquipmentEntryModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The heading the entry was transcribed from, checked against the corpus.</summary>
    public string PrintedUnder { get; init; } = "";

    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public IReadOnlyList<string>? CorroboratedBy { get; init; }
    public string? Ambiguity { get; init; }
    public EquipmentInterpretationModel? Interpretation { get; init; }

    // The fact block. Exactly one of these is populated per entry, by name, so a reader
    // that has an entry in hand does not have to know which shape it carries.
    public ArmorRankModel? Armor { get; init; }
    public ShieldModel? Shield { get; init; }
    public WeaponFeatureGlossaryModel? Glossary { get; init; }
    public CustomGearModel? Customization { get; init; }
    public GearProsAndConsModel? GearProsAndCons { get; init; }
}

/// <summary>A reading of the page, labelled as one. Never a transcription.</summary>
public record EquipmentInterpretationModel
{
    public string WhatThisIs { get; init; } = "";
    public string? RowAlignment { get; init; }
    public string? CopyRuleNote { get; init; }
    public string? GrantedPowersResolvedToIdsNote { get; init; }
}

/// <summary>p.88: what wearing a suit of armour does.</summary>
public record ArmorRankModel
{
    /// <summary>The Power a worn suit grants outright.</summary>
    public string GrantsPowerId { get; init; } = "";

    public string RankIs { get; init; } = "";
    public string RankBaseTrait { get; init; } = "";
    public bool RankBaseMayBeTheArmorPowerInstead { get; init; }
    public string RankBaseSubstitutionAppliesWhile { get; init; } = "";
    public bool BuiltInExtrasArePowers { get; init; }
    public IReadOnlyList<string> BuiltInExtrasExamples { get; init; } = [];
}

/// <summary>p.88's Armor table, with the row count beside the rows.</summary>
public record ArmorTableModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PrintedUnder { get; init; } = "";
    public IReadOnlyList<ArmorRowModel> Rows { get; init; } = [];

    /// <summary>How many rows the table has, beside the rows themselves — a derivation
    /// cannot notice a table that has lost half of itself when the expectation lost the
    /// same half.</summary>
    public int RowCount { get; init; }

    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public EquipmentInterpretationModel? Interpretation { get; init; }
    public string? Ambiguity { get; init; }
}

/// <summary>One printed row of the Armor table.</summary>
public record ArmorRowModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Ancient, Modern or Advanced — the era column.</summary>
    public string Category { get; init; } = "";

    public int ArmorBonusDice { get; init; }

    /// <summary>Bulky, Rigid, or neither. Names an <see cref="ArmorFeatureModel"/>.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
}

/// <summary>Bulky or Rigid: what the suit's weight or stiffness costs the wearer.</summary>
public record ArmorFeatureModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PrintedUnder { get; init; } = "";

    /// <summary>Negative: it is a penalty.</summary>
    public int PenaltyDice { get; init; }

    public IReadOnlyList<string> PenalisedRolls { get; init; } = [];

    /// <summary>Bulky lets a strong enough wearer off; Rigid does not, so this is null there.</summary>
    public int? ExemptIfMightAtLeast { get; init; }

    /// <summary>The p.93 custom feature that buys the penalty off — Fitted, for both.</summary>
    public string RemovableByGearFeatureId { get; init; } = "";

    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public string? Ambiguity { get; init; }
}

/// <summary>p.88: a shield defends, and can be swung instead.</summary>
public record ShieldModel
{
    public int BonusDice { get; init; }
    public string BonusName { get; init; } = "";
    public string CarriedIn { get; init; } = "";
    public IReadOnlyList<string> AppliesToDefenses { get; init; } = [];
    public IReadOnlyList<string> AppliesAgainstAttackKinds { get; init; } = [];
    public bool MayBeUsedAsAnOffHandWeapon { get; init; }
    public string StrikingWithItCosts { get; init; } = "";
    public IReadOnlyList<string> NoBenefitWhen { get; init; } = [];

    /// <summary>A shield is also printed in the weapons tables, which is why it is here twice.</summary>
    public bool AlsoAWeaponRow { get; init; }

    public IReadOnlyList<string> WeaponRows { get; init; } = [];
    public string WeaponFeatureId { get; init; } = "";
}

/// <summary>What the Weapon Features glossary is, as distinct from what is in it.</summary>
public record WeaponFeatureGlossaryModel
{
    public string Describes { get; init; } = "";
    public bool FurtherFeaturesComeFromCustomizing { get; init; }
    public int CustomizationIsOnPage { get; init; }
    public int EntryCount { get; init; }

    /// <summary>Larger than <see cref="EntryCount"/>: one entry covers Area and Burst both.</summary>
    public int NameCount { get; init; }

    public IReadOnlyList<int> PrintedAcrossPages { get; init; } = [];
}

/// <summary>
/// One entry of the Weapon Features glossary. Most of the fields are null on most entries:
/// six of the eighteen do nothing but hand the reader back to a Pro or a Con, and the rest
/// each state a different kind of thing.
/// </summary>
public record WeaponFeatureModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The names the weapons tables cite this entry by. One entry covers two.</summary>
    public IReadOnlyList<string> CoversNames { get; init; } = [];

    public string PrintedUnder { get; init; } = "";

    /// <summary>The Pro, Con or Power this feature defers to, where it defers to one.</summary>
    public WeaponFeatureReferenceModel? WorksLike { get; init; }

    // ── Area/Burst ───────────────────────────────────────────────────────
    public int? BurstDiameterFeet { get; init; }
    public IReadOnlyList<BurstDiameterExceptionModel>? BurstDiameterExceptions { get; init; }

    // ── Damage and what else the weapon can do ───────────────────────────
    public bool? InflictsDamage { get; init; }
    public IReadOnlyList<string>? MayStillBeUsedFor { get; init; }
    public IReadOnlyList<string>? MayAlsoBeUsedFor { get; init; }
    public bool? CarrierAttack { get; init; }

    // ── Braced ───────────────────────────────────────────────────────────
    public string? Requires { get; init; }
    public int? TwoHandedInsteadIfMightAtLeast { get; init; }

    // ── The Powers a feature delivers ────────────────────────────────────
    public string? PowerRankIs { get; init; }
    public int? PowerRankBaseDice { get; init; }
    public string? PowerRankAdds { get; init; }
    public int? AttackThreshold { get; init; }
    public string? AttackThresholdLabel { get; init; }
    public string? AttackTrait { get; init; }

    // ── Launcher ─────────────────────────────────────────────────────────
    public bool? GrenadesSelectedSeparately { get; init; }
    public bool? IdenticalStatisticsToThrownGrenades { get; init; }
    public bool? ButNotTheSameItems { get; init; }

    // ── Shield, Thrown, Versatile ────────────────────────────────────────
    public int? ShieldBonusDice { get; init; }
    public string? BaseRange { get; init; }
    public bool? FartherWithStrength { get; init; }
    public string? RangeDetailChapter { get; init; }
    public bool? AmmunitionIsNotTracked { get; init; }
    public int? TwoHandedBonusDice { get; init; }

    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public string? Ambiguity { get; init; }
}

/// <summary>Which store a weapon feature hands the reader to, and the id there.</summary>
public record WeaponFeatureReferenceModel
{
    /// <summary>pro | con | power</summary>
    public string Kind { get; init; } = "";

    public string Id { get; init; } = "";
}

/// <summary>A weapon the page gives a smaller blast radius than the general figure.</summary>
public record BurstDiameterExceptionModel
{
    public string Weapon { get; init; } = "";
    public int DiameterFeet { get; init; }
}

/// <summary>
/// One of the three weapons tables, copied from <c>data/rules/play/equipment.json</c>.
/// <b>Do not edit the rows here alone</b> — a test holds the two copies equal.
/// </summary>
public record WeaponTableModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PrintedUnder { get; init; } = "";

    /// <summary>The file these rows are a copy of.</summary>
    public string CopiedFrom { get; init; } = "";

    public IReadOnlyList<WeaponRowModel> Weapons { get; init; } = [];
    public int RowCount { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public EquipmentInterpretationModel? Interpretation { get; init; }
    public string? Ambiguity { get; init; }
}

/// <summary>One printed weapons-table row.</summary>
public record WeaponRowModel
{
    public string Name { get; init; } = "";

    /// <summary>Melee or Ranged.</summary>
    public string Class { get; init; } = "";

    /// <summary>Null for the one row the book prints with no bonus at all.</summary>
    public int? BonusDice { get; init; }

    /// <summary>The printed "(s)": the weapon knocks down rather than wounds.</summary>
    public bool Subdual { get; init; }

    /// <summary>Names of <see cref="WeaponFeatureModel"/> entries, by their printed spelling.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];
}

/// <summary>p.91: the rule that mundane gear is not bought, and the list that follows it.</summary>
public record EquipmentCatalogueModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string PrintedUnder { get; init; } = "";
    public MundaneGearModel MundaneGear { get; init; } = new();
    public IReadOnlyList<EquipmentItemModel> Items { get; init; } = [];
    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public IReadOnlyList<string>? CorroboratedBy { get; init; }
    public string? Ambiguity { get; init; }
    public EquipmentInterpretationModel? Interpretation { get; init; }
}

/// <summary>The rule the list is an example of: nobody buys any of it.</summary>
public record MundaneGearModel
{
    public bool IsFree { get; init; }
    public bool IsTracked { get; init; }
    public bool CostsHeroPoints { get; init; }

    /// <summary>The Talent rank from which a character is assumed to carry that Talent's tools.</summary>
    public int AssumedCarriedForTalentsAtRank { get; init; }

    public IReadOnlyList<string> AssumedCarriedExamples { get; init; } = [];

    /// <summary>Perk ids the GM weighs when a player asks for something beyond the ordinary.</summary>
    public IReadOnlyList<string> PerksTheGmWeighs { get; init; } = [];

    public bool EvenABrokeHeroHasWhatTheyNeed { get; init; }
    public bool OrdinaryPossessionsAreNotTrackedEither { get; init; }
    public bool GmUsesCommonSense { get; init; }
    public bool NpcsHaveWhateverTheGmWants { get; init; }
    public bool MinionsDoNotUseGear { get; init; }
    public bool MinionGearIsDetailNotMechanics { get; init; }
    public bool ListIsExamplesNotACatalogueOfPrices { get; init; }
    public int ItemCount { get; init; }

    /// <summary>Zero. The list prints no price for anything, which is the rule's corroboration.</summary>
    public int PrintedPriceCount { get; init; }

    /// <summary>Zero, likewise, for availability.</summary>
    public int PrintedAvailabilityCount { get; init; }
}

/// <summary>
/// One item off p.91's list. A dozen or so carry a real mechanical effect; the rest are props,
/// and every mechanical field below is null on them.
/// </summary>
public record EquipmentItemModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>powers.json ids the printed parenthetical resolves to, where it names a Power.</summary>
    public IReadOnlyList<string>? GrantsPowers { get; init; }

    /// <summary>The one item whose granted Power is printed with a rank.</summary>
    public int? GrantedPowerRank { get; init; }

    public int? BonusDice { get; init; }
    public string? BonusAppliesTo { get; init; }
    public int? BreakThreshold { get; init; }
    public string? BreakThresholdLabel { get; init; }
    public string? Duration { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
}

/// <summary>p.92: how a mundane item becomes a customised one, and what that costs.</summary>
public record CustomGearModel
{
    public IReadOnlyList<string> SitsBetween { get; init; } = [];
    public bool CostsHeroPoints { get; init; }
    public IReadOnlyList<string> WhatMayBeAdded { get; init; } = [];
    public int CustomFeaturesAreOnPage { get; init; }

    /// <summary>Twelve — the entries of <c>gear_features.json</c>.</summary>
    public int CustomFeatureCount { get; init; }

    public bool PairedWeaponsCostOnce { get; init; }
    public string PairedWeaponsRequirePowerId { get; init; } = "";
    public bool PairedWeaponsMustBeIdentical { get; init; }
    public bool SubjectToGmApproval { get; init; }
    public bool PlayersMayInventFeatures { get; init; }
    public IReadOnlyList<string> GmMayGateFeaturesBehindPerks { get; init; } = [];
    public bool GmMayProhibitFeaturesEntirely { get; init; }
}

/// <summary>p.93: gear is priced by the Powers table, with two floors of its own.</summary>
public record GearProsAndConsModel
{
    public bool EveryPieceOfGearHasTheItemCon { get; init; }
    public string ItemConId { get; init; } = "";

    /// <summary>Why the Item Con is not credited: it says what gear is, not what it costs.</summary>
    public bool ItemConIsAStatementNotADiscount { get; init; }

    public bool CostIsTheSameAsOnAPower { get; init; }

    /// <summary>Zero, where a Power floors at one.</summary>
    public int MinimumCostHeroPoints { get; init; }

    public bool GearNeverPaysOut { get; init; }

    /// <summary>The twenty-four options the page names, by their printed spelling.</summary>
    public IReadOnlyList<string> CommonlyApplied { get; init; } = [];

    public int CommonlyAppliedCount { get; init; }
    public bool ListIsNotExhaustive { get; init; }

    /// <summary>The Item Con is not on the common list, which is why it is not a discount.</summary>
    public bool ItemIsAbsentFromTheCommonList { get; init; }
}
