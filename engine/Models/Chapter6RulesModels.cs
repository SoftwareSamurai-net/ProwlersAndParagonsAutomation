namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// The three Chapter 6 stores this project extracted for pp.94-104 — <c>gadgets.json</c>,
/// <c>vehicles.json</c> and <c>headquarters.json</c> — as models.
///
/// <para><b>None of them is on <see cref="RulesRepository.DataFileNames"/>, deliberately.</b> That
/// list is the contract for a host which fetches the character rules over HTTP, and adding a file
/// to it is a decision about what the browser downloads on its first page load. The slice that
/// teaches <c>CostCalculator</c> and the sheet what a vehicle or a headquarters costs is the slice
/// that gets to make it. Until then these models exist so the data can be held to the rulebook by a
/// test, which is the whole reason a rules file is worth having: unread data reads like a source of
/// truth and is not one, and a file with no model is a file
/// <c>JsonUnmappedMemberHandling.Disallow</c> can say nothing about.</para>
///
/// <para>Every csproj's <c>data\rules\*.json</c> glob copies the three files to every host's output
/// directory already, so the consumer slice has to add a loader and nothing else.</para>
///
/// <para><b>Two currencies, and neither is Hero Points.</b> A vehicle is bought in Vehicle Points at
/// 25 per Hero Point of the Unique Vehicle Perk; a headquarters in Base Points at 3 per Hero Point
/// of the Headquarters Perk. Every price below those two lines is in the second currency, so a
/// consumer that adds one of these figures to a Hero Point total has made a category error.</para>
/// </summary>
public record Chapter6Header
{
    public string WhatThisIs { get; init; } = "";
    public string NotLogic { get; init; } = "";

    /// <summary>Present on <c>vehicles.json</c> and <c>headquarters.json</c>; null on gadgets.</summary>
    public string? TwoCurrencies { get; init; }

    public string DeliberatelyOmitted { get; init; } = "";
    public IReadOnlyList<string> VerifiedFieldsClosedList { get; init; } = [];
    public string SourceRef { get; init; } = "";
}

/// <summary>
/// What every entry in the three files carries, whatever else it carries beside it.
///
/// <para><c>Interpretation</c> is this project's reading of a page rather than a transcription of
/// one, and it says so in its own first field — the discipline
/// <c>docs/guide/play-rules.md</c> established for the Thresholds table. A fact field is a claim
/// that the page states the thing; a reading goes here.</para>
/// </summary>
public abstract record Chapter6Entry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>formula | table | table_setting | prerequisite | special_case</summary>
    public string Kind { get; init; } = "";

    /// <summary>A heading printed on the page this entry cites, as the corpus records it.</summary>
    public string PrintedUnder { get; init; } = "";

    public IReadOnlyDictionary<string, string>? Interpretation { get; init; }

    /// <summary>Original text written from the entry, never the rulebook's own words.</summary>
    public string Description { get; init; } = "";

    public IReadOnlyList<string> VerifiedFields { get; init; } = [];
    public string SourceRef { get; init; } = "";
    public string? Ambiguity { get; init; }
}

// ── gadgets.json ─────────────────────────────────────────────────────────────

public record GadgetRulesFile
{
    public Chapter6Header Header { get; init; } = new();
    public IReadOnlyList<GadgetEntry> Entries { get; init; } = [];
}

public record GadgetEntry : Chapter6Entry
{
    public GadgetPrerequisites? Prerequisites { get; init; }
    public GadgetComplexity? Complexity { get; init; }
    public GadgetChallengeRoll? ChallengeRoll { get; init; }
    public GadgetBuild? Build { get; init; }
    public GadgetInstability? Instability { get; init; }
    public GadgetTalents? Talents { get; init; }
    public GadgetScope? Scope { get; init; }
}

public record GadgetPrerequisites
{
    public int MinimumTechnologyRank { get; init; }
    public IReadOnlyList<string> AlsoNeeds { get; init; } = [];
    public string MaximumPerIssueFormula { get; init; } = "";
    public string MaximumPerIssueTrait { get; init; } = "";
    public int MaximumPerIssueDivisor { get; init; }
}

public record GadgetComplexity
{
    public int Minimum { get; init; }
    public string MaximumFormula { get; init; } = "";
    public string MaximumTrait { get; init; } = "";
    public IReadOnlyList<string> Governs { get; init; } = [];
}

public record GadgetChallengeRoll
{
    public string TraitRolled { get; init; } = "";
    public string ThresholdIs { get; init; } = "";
    public string OnSuccess { get; init; } = "";
    public int MarginalSuccessMinimum { get; init; }
    public int MarginalSuccessMaximum { get; init; }
    public string MarginalSuccessEffect { get; init; } = "";
    public string MarginalSuccessDecidedBy { get; init; } = "";
    public int SceneMinutesPerComplexity { get; init; }
    public bool TimeIsSpentWhetherOrNotItSucceeds { get; init; }
}

public record GadgetBuild
{
    public int HeroPointsGrantedMultiplier { get; init; }
    public string HeroPointsGrantedFormula { get; init; } = "";
    public IReadOnlyList<string> SpendableOn { get; init; } = [];
    public string DefaultCon { get; init; } = "";
    public bool DefaultConIsCredited { get; init; }
    public bool OtherProsAndConsAllowed { get; init; }
    public bool MayInsteadModifyMundaneGear { get; init; }
}

public record GadgetInstability
{
    public int DiceRolledPerUse { get; init; }
    public string FailsWhen { get; init; } = "";
    public string OnFailure { get; init; } = "";
    public string RollIsMade { get; init; } = "";
}

public record GadgetTalents
{
    public string TechnologyMakes { get; init; } = "";
    public string ScienceMakes { get; init; } = "";
    public string MedicineMakes { get; init; } = "";
    public bool GmMayAllowMagicalOrTechnoMagical { get; init; }
    public bool GmChoosesTheTalentForThose { get; init; }
    public string PerIssueCeilingIsUnchanged { get; init; } = "";
}

public record GadgetScope
{
    public string IntendedFor { get; init; } = "";
    public string StandingSupplyIsAPower { get; init; } = "";
    public bool GmMayAllowBuildingBeforeAnAdventure { get; init; }
    public bool PreBuiltStillCountsAgainstThePerIssueCeiling { get; init; }
}

// ── vehicles.json ────────────────────────────────────────────────────────────

public record VehicleRulesFile
{
    public Chapter6Header Header { get; init; } = new();
    public IReadOnlyList<VehicleEntry> Entries { get; init; } = [];
}

public record VehicleEntry : Chapter6Entry
{
    public VehicleBody? Body { get; init; }
    public VehicleSpeed? Speed { get; init; }
    public VehicleControl? Control { get; init; }
    public VehicleWeapons? Weapons { get; init; }
    public VehicularGearLimit? VehicularGearLimit { get; init; }
    public VehiclePiloting? Piloting { get; init; }
    public VehicleEdge? Edge { get; init; }
    public VehicleChases? Chases { get; init; }
    public VehicleAttacksAndDefenses? AttacksAndDefenses { get; init; }
    public VehicleDamageAndRepair? DamageAndRepair { get; init; }
    public VehicleTargetedSystems? TargetedSystems { get; init; }
    public CapitalShips? CapitalShips { get; init; }
    public CapitalShipRamming? CapitalShipRamming { get; init; }
    public FoeAndMinionPilots? FoeAndMinionPilots { get; init; }
    public UniqueVehiclePerk? UniqueVehiclePerk { get; init; }
    public UniqueVehicleCharacteristics? UniqueVehicleCharacteristics { get; init; }
    public StockUpgradeLimit? StockUpgradeLimit { get; init; }

    /// <summary>The rows of one printed mundane-vehicle table.</summary>
    public IReadOnlyList<MundaneVehicleRow>? Vehicles { get; init; }

    /// <summary>The six stock vehicles printed on p.96.</summary>
    public IReadOnlyList<StockVehicleRow>? Stock { get; init; }

    /// <summary>The twenty-three vehicle features and their Vehicle Point prices.</summary>
    public IReadOnlyList<VehicleFeatureRow>? Features { get; init; }
}

public record VehicleBody
{
    public string Measures { get; init; } = "";
    public IReadOnlyList<string> CountsAs { get; init; } = [];
    public string Exception { get; init; } = "";
    public bool ProtectsPassengers { get; init; }
    public string PassengersUseItAs { get; init; } = "";
    public string ProtectionLostWithFeature { get; init; } = "";
}

public record VehicleSpeed
{
    public string Measures { get; init; } = "";
    public string EquivalentTo { get; init; } = "";
    public bool SeparateRankPerMovementMode { get; init; }
}

public record VehicleControl
{
    public string IsA { get; init; } = "";
    public IReadOnlyList<string> Covers { get; init; } = [];
    public string ControlRollIs { get; init; } = "";
    public string ControlRollTrait { get; init; } = "";

    /// <summary>Negative: a capital ship loses 3d of Control for every 30 points of Health.</summary>
    public int CapitalShipControlPerThirtyHealth { get; init; }
}

public record VehicleWeapons
{
    public string Represents { get; init; } = "";
    public bool IsAnAbstraction { get; init; }
    public string UnarmedValue { get; init; } = "";
}

public record VehicularGearLimit
{
    public bool VehiclesAreMundaneGear { get; init; }
    public string DefaultApplies { get; init; } = "";
    public IReadOnlyList<int> RaisedOptions { get; init; } = [];
    public bool RaisedOptionsAreOpenEnded { get; init; }
    public string RaisingItLetsCharactersUse { get; init; } = "";
}

public record VehiclePiloting
{
    public bool RoutineOperationIsAutomatic { get; init; }
    public string RollCalledForWhen { get; init; } = "";
    public string RollIsUsually { get; init; } = "";
}

public record VehicleEdge
{
    public string Formula { get; init; } = "";
    public string Trait { get; init; } = "";
    public bool UsesFullRankRegardlessOfGearLimit { get; init; }
    public bool CapitalShipsActAfterOrdinaryVehicles { get; init; }
    public bool EdgeOrdersCapitalShipsAmongThemselves { get; init; }
}

public record VehicleChases
{
    public string ResolvedAs { get; init; } = "";
    public string OpenTerrainUses { get; init; } = "";
    public string WindingTerrainUses { get; init; } = "";
    public bool TranscribedHere { get; init; }
    public int DeferredToChapter { get; init; }
}

public record VehicleAttacksAndDefenses
{
    public string AttackerOptionOne { get; init; } = "";
    public string AttackerOptionTwo { get; init; } = "";
    public string WhichOptionIsUsed { get; init; } = "";
    public string ControlRollRepresents { get; init; } = "";
    public string WeaponsRollRepresents { get; init; } = "";
    public string AttackPowerSubstitutesFor { get; init; } = "";
    public string RamSubstitutesForWeapons { get; init; } = "";
    public string VehicleActiveDefenseRoll { get; init; } = "";
    public string VehiclePassiveDefenseRoll { get; init; } = "";
    public string CharactersAttackingAVehicleUse { get; init; } = "";
}

public record VehicleDamageAndRepair
{
    public int DamagePerNetSuccess { get; init; }
    public string DisabledOrDestroyedAt { get; init; } = "";
    public int RamDamagePerNetSuccess { get; init; }
    public string RammerSuffersTheSameDamageWhen { get; init; } = "";
    public string CharacterEffectiveBodyIs { get; init; } = "";
    public bool VehiclesDoNotHeal { get; init; }
    public string RepairTalent { get; init; } = "";
    public string FieldRepairNeeds { get; init; } = "";
}

public record VehicleTargetedSystems
{
    public bool IsAGmOption { get; init; }

    /// <summary>Negative: the attacker accepts a 2d penalty to aim at a system.</summary>
    public int AttackPenaltyDice { get; init; }

    public bool InflictsDamage { get; init; }
    public string Disables { get; init; } = "";
    public int NetSuccessesShortMin { get; init; }
    public int NetSuccessesShortMax { get; init; }
    public string ShortDuration { get; init; } = "";
    public int NetSuccessesLongMin { get; init; }
    public string LongDuration { get; init; } = "";
}

public record CapitalShips
{
    public string WhatTheyAre { get; init; } = "";
    public string EdgeIs { get; init; } = "";
    public string EdgeTrait { get; init; } = "";
    public string EdgeHalvedWhen { get; init; } = "";
    public string GmMayRequirePower { get; init; } = "";
    public bool HealthIsSeparateFromBody { get; init; }
    public int HealthMultipleOf { get; init; }
    public bool HealthHasNoFormula { get; init; }
    public int ControlPerThirtyHealth { get; init; }
    public string StandardWeaponsCarryCon { get; init; } = "";
    public IReadOnlyList<string> OverkillAppliesExceptAgainst { get; init; } = [];
    public string OverkillEffect { get; init; } = "";
    public int PinpointWeaponRankOffset { get; init; }
    public bool PinpointWeaponsCarryOverkill { get; init; }
    public string PinpointWeaponsUsuallyEnoughTo { get; init; } = "";
    public bool NeverPerformActiveDefenses { get; init; }
    public string OneShotDestructionThreshold { get; init; } = "";
}

public record CapitalShipRamming
{
    public IReadOnlyList<string> MayOnlyRam { get; init; } = [];
    public string ResolvedBy { get; init; } = "";
    public string SmallerShipBonus { get; init; } = "";
    public int WorkedExampleSmallHealth { get; init; }
    public int WorkedExampleSmallControl { get; init; }
    public int WorkedExampleLargeHealth { get; init; }
    public int WorkedExampleLargeControl { get; init; }
    public int WorkedExampleBonusDice { get; init; }
    public string DamageOnSuccess { get; init; } = "";
    public bool DamageUsesUndamagedHealth { get; init; }
    public int HalfDamageOptionAtNetSuccesses { get; init; }
    public int DoubleDamageOptionAtNetSuccesses { get; init; }
}

public record FoeAndMinionPilots
{
    public string FoeVehicleDamageCapacity { get; init; } = "";
    public string FoeWorkedExampleVehicle { get; init; } = "";
    public int FoeWorkedExampleBody { get; init; }
    public int FoeWorkedExampleDamageCapacity { get; init; }
    public string MinionsUseInPlaceOfVehicles { get; init; } = "";
    public bool MinionsAttackInGroups { get; init; }
    public IReadOnlyList<string> MinionGroupBonusAppliesTo { get; init; } = [];
    public int MinionVehiclesLostPerNetSuccess { get; init; }
    public bool AppliesToCapitalShips { get; init; }
}

public record UniqueVehiclePerk
{
    /// <summary>The id in <c>perks.json</c> this rule is the detail of.</summary>
    public string PerkId { get; init; } = "";

    public int VehiclePointsPerHeroPoint { get; init; }
    public IReadOnlyList<string> SpendableOn { get; init; } = [];
    public bool HeroesMayPoolPoints { get; init; }
}

public record UniqueVehicleCharacteristics
{
    public int BodyCostPerRank { get; init; }
    public int SpeedCostPerRank { get; init; }
    public int WeaponsCostPerRank { get; init; }
    public int ControlCostPerRank { get; init; }
    public string ControlMayNotExceed { get; init; } = "";
    public int NegativeControlRefundPerRank { get; init; }
    public int NegativeControlMinimum { get; init; }
    public int InitialBody { get; init; }
    public int InitialSpeed { get; init; }
    public int InitialControl { get; init; }
    public string InitialWeapons { get; init; } = "";
}

public record StockUpgradeLimit
{
    public bool IsOptional { get; init; }
    public string Suits { get; init; } = "";
    public int MaxBodyRanksAdded { get; init; }
    public int MaxSpeedRanksAdded { get; init; }
    public int MaxControlRanksAdded { get; init; }
}

/// <summary>One printed row of a mundane vehicle table (pp.97-98).</summary>
public record MundaneVehicleRow
{
    public string Name { get; init; } = "";

    /// <summary>The Body cell exactly as printed — "9d", or "3d or 1d" on the one row that prints two.</summary>
    public string BodyPrinted { get; init; } = "";

    /// <summary>Null on the one row whose printed cell carries two values.</summary>
    public int? Body { get; init; }

    public int Speed { get; init; }
    public int Control { get; init; }

    /// <summary>Null where the table prints an em dash: the vehicle is unarmed.</summary>
    public int? Weapons { get; init; }

    /// <summary>The table's asterisk: the Body does not shield the crew.</summary>
    public bool OpenCockpit { get; init; }

    /// <summary>The table's double asterisk and its parenthesised figure, or null.</summary>
    public int? CapitalShipHealth { get; init; }
}

/// <summary>One of the six stock vehicles printed on p.96.</summary>
public record StockVehicleRow
{
    public string Name { get; init; } = "";
    public int VehiclePoints { get; init; }
    public int Body { get; init; }
    public int Speed { get; init; }
    public int Control { get; init; }
    public int? Weapons { get; init; }

    /// <summary>The feature line as printed, including any rating, e.g. "Passengers 4".</summary>
    public IReadOnlyList<string> Features { get; init; } = [];
}

/// <summary>One of the twenty-three vehicle features, priced in Vehicle Points.</summary>
public record VehicleFeatureRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>flat | flat_variable | per_unit</summary>
    public string CostType { get; init; } = "";

    /// <summary>Vehicle Points for a flat feature; negative for a drawback. Null when graded or per unit.</summary>
    public int? Cost { get; init; }

    /// <summary>Keyed grades, for the one feature the chapter prices at 1 or 2 points.</summary>
    public IReadOnlyDictionary<string, int>? CostRange { get; init; }

    public int? CostPerUnit { get; init; }
    public string? UnitLabel { get; init; }

    /// <summary>What the feature does, in this project's words.</summary>
    public string Mechanic { get; init; } = "";

    /// <summary>Other features this one needs, as the page states them.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    /// <summary>A kind of vehicle the feature is limited to, or null.</summary>
    public string? RestrictedTo { get; init; }

    public int PrintedPage { get; init; }
}

// ── headquarters.json ────────────────────────────────────────────────────────

public record HeadquartersRulesFile
{
    public Chapter6Header Header { get; init; } = new();
    public IReadOnlyList<HeadquartersEntry> Entries { get; init; } = [];
}

public record HeadquartersEntry : Chapter6Entry
{
    public HeadquartersPerk? HeadquartersPerk { get; init; }
    public AdvancedFeatureBonus? AdvancedFeatureBonus { get; init; }
    public MobileHeadquarters? MobileHeadquarters { get; init; }
    public Teamwork? Teamwork { get; init; }

    /// <summary>The twenty-two base features and their Base Point prices.</summary>
    public IReadOnlyList<BaseFeatureRow>? Features { get; init; }
}

public record HeadquartersPerk
{
    /// <summary>The id in <c>perks.json</c> this rule is the detail of.</summary>
    public string PerkId { get; init; } = "";

    public int BasePointsPerHeroPoint { get; init; }
    public string GrantsByDefault { get; init; } = "";
    public bool DefaultHeadquartersCostsNoBasePoints { get; init; }
    public bool HeroesMayPoolPoints { get; init; }
}

public record AdvancedFeatureBonus
{
    public bool FeaturesAreMostlyNarrative { get; init; }
    public int GmMayGrantBonusDice { get; init; }
    public string BonusAppliesTo { get; init; } = "";
    public bool IsGmDiscretion { get; init; }
    public string WorkedExampleFeature { get; init; } = "";
    public string WorkedExampleRoll { get; init; } = "";
}

public record MobileHeadquarters
{
    public int BasePointCost { get; init; }
    public bool IsAlsoAUniqueVehicle { get; init; }
    public string VehicleCharacteristicsBoughtWith { get; init; } = "";
    public bool AlwaysACapitalShip { get; init; }
    public int HealthDefault { get; init; }
    public int HealthWithLargeSize { get; init; }
    public int HealthWithSprawlingSize { get; init; }
    public bool LargestSizeCannotBeAttackedOrDestroyed { get; init; }
}

public record Teamwork
{
    public string GrantedByFeature { get; init; } = "";
    public int PointsPerIssue { get; init; }
    public string GrantedTo { get; init; } = "";
    public string BehavesLike { get; init; } = "";
    public string SpendableOnlyOn { get; init; } = "";
}

/// <summary>One of the twenty-two base features, priced in Base Points.</summary>
public record BaseFeatureRow
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>flat | flat_variable | per_unit</summary>
    public string CostType { get; init; } = "";

    /// <summary>Base Points for a flat feature. Null when graded or per unit.</summary>
    public int? Cost { get; init; }

    /// <summary>Keyed grades for the features the chapter prices as a range.</summary>
    public IReadOnlyDictionary<string, int>? CostRange { get; init; }

    public int? CostPerUnit { get; init; }
    public string? UnitLabel { get; init; }

    /// <summary>What each grade buys, keyed by the same keys as <see cref="CostRange"/>.</summary>
    public IReadOnlyDictionary<string, string>? GradeEffects { get; init; }

    /// <summary>What the feature does, in this project's words.</summary>
    public string Mechanic { get; init; } = "";

    /// <summary>A limit or a rider the entry prints, or null.</summary>
    public string? Constraint { get; init; }

    public int PrintedPage { get; init; }
}
