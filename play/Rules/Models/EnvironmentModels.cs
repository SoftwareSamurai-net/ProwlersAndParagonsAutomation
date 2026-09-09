// Written from the shipped data/rules/play/environment.json. Every key of every entry in that file
// has a property here, because PlayRulesFileCoverageTests deserializes it with
// JsonUnmappedMemberHandling.Disallow: a field nothing reads is a field nothing can hold to the
// rulebook.
//
// Three of this chapter's tables are applied and the rest of it is loaded and not. Encounter.Scenery
// reads smashing_table, scenery_table and massive_objects_table, and scenery_as_weapons and
// massive_objects beside them, because Chapter 4's modifier_cover, knockback and throwing_range each
// name a figure only this chapter prints — the cover's Structure, an object tougher than the target,
// an object's weight rank. Everything else here is a mechanic this engine has no intent for at all
// (falling, suffocation, swimming, disasters, toxins), which is the same footing as multiple actions
// and combat stunts: not stubbed, simply nothing to call. Encounter.EntriesNotYetApplied stays
// untouched for that reason — that set is the engine's record of what it has been *given* to apply
// and has not.

namespace ProwlersAndParagonsAutomation.Play.Rules.Models;

/// <summary>p.105's disaster procedure: how many goals, who invents them, and when the table is read.</summary>
public sealed record EnvironmentDisasterModel(
    int MinorGoals,
    int MajorGoals,
    int GmSuppliesInAMinorDisaster,
    int GmSuppliesInAMajorDisaster,
    string RemainingGoalsComeFrom,
    string AGoalIs,
    string MostGoalsNeed,
    bool AGoalMayBeWorthItsOwnScene,
    string WhoDecidesAGoalNeedsItsOwnScene,
    string ResolutionReadOff,
    string ReadTheTableWhen);

/// <summary>
/// One row of p.105's Disaster Results table: how many goals each size of disaster needs for this
/// outcome, and who narrates it.
/// </summary>
/// <param name="Narrator">"gm" or "players", as the printed cell says.</param>
/// <param name="Embellishment">
/// Whether the printed cell adds "with Embellishment" — the same licence Chapter 3's narrative
/// control bands grant, and recorded as a flag for the same reason those are.
/// </param>
public sealed record EnvironmentDisasterResultModel(
    int MinorGoalsAchievedMin,
    int MinorGoalsAchievedMax,
    int MajorGoalsAchievedMin,
    int MajorGoalsAchievedMax,
    string Narrator,
    bool Embellishment);

/// <summary>p.105's rule about what counts as energy, and what two forces are instead.</summary>
public sealed record EnvironmentEnergyModel(
    string KindsAreLumpedInto,
    string Why,
    bool GravityAndMagnetismAreEnergy,
    string GravityAndMagnetismAre,
    IReadOnlyList<string> GravityAndMagnetismRepresentedWith,
    string GravityAndMagnetismIfClassifiedAsEnergy);

/// <summary>
/// p.105's Energy Types table: the nine category names, and the two rows that carry a mechanic.
///
/// <para>The description column is deliberately absent — it is the book's own prose, and the names
/// are what a rule turns on. The file header says so.</para>
/// </summary>
public sealed record EnvironmentEnergyTypesModel(
    IReadOnlyList<string> Types,
    string ForceKineticAttacksAre,
    bool SonicFootnoteIsOfferedAsOptional,
    string SonicInAVacuum,
    int SonicUnderwaterBonusDice);

/// <summary>p.105's falling rule: an attack only a passive defence answers.</summary>
public sealed record EnvironmentFallingModel(
    bool IsAnAttack,
    string ResistedOnlyWith,
    bool GmMayAllowACreativeActiveDefense,
    string AttackRankDependsOn,
    string ReadOff,
    IReadOnlyList<string> HardLandingExamples,
    int HardLandingBonusDice,
    IReadOnlyList<string> SoftLandingExamples,
    int SoftLandingPenaltyDice,
    string SoftLandingAllowsActiveDefense);

/// <summary>One row of p.106's Falling table.</summary>
/// <param name="UpToFeet">
/// The band's ceiling in feet, or null for the open-topped last row, which the table prints as
/// "Any Farther". Null is a printed cell and not a missing one.
/// </param>
public sealed record EnvironmentFallingRowModel(int? UpToFeet, int Rank);

/// <summary>p.106's two grades of environmental hazard, and the ceiling they share.</summary>
public sealed record EnvironmentHostileEnvironmentModel(
    IReadOnlyList<string> HazardGrades,
    IReadOnlyList<string> MinorExamples,
    IReadOnlyList<string> MajorExamples,
    string MinorWithstoodForMinutesEqualTo,
    int MinorDamagePerMinuteAfterThat,
    string MajorWithstoodForPagesEqualTo,
    int MajorDamagePerPageAfterThat,
    bool TrackTimeInPagesForAMajorHazardEvenOutOfCombat,
    int MaximumHazardDamagePerPage,
    bool MaximumAppliesAcrossSimultaneousHazards,
    IReadOnlyList<string> PowersThatProtect);

/// <summary>p.106's suffocation clock, and the damage that undoes itself.</summary>
public sealed record EnvironmentSuffocationModel(
    string HoldBreathMinutesEqualTo,
    int DamagePerPageAfterThat,
    string AllSuffocationDamageRemovedWhen,
    bool RemovalIsImmediate,
    string DefeatedRatherThanKilledUnless,
    string SurvivingIsExplainedBy);

/// <summary>p.106's underwater penalties, and the Power that pays none of them.</summary>
public sealed record EnvironmentSwimmingModel(
    string TravelSpeed,
    string AgilityUsedForMovementChallengeRolls,
    int PerceptionPenaltyDiceUnderwater,
    int ScubaMaskReducesVisualPerceptionPenaltyTo,
    string UnderwaterCombatEdge,
    int UnderwaterPhysicalAttackPenaltyDice,
    int UnderwaterActiveDefensePenaltyDice,
    string IgnoredBy,
    string DeepWaterComplexitiesLeftTo);

/// <summary>p.106's free half-Might Leaping, and what it does not buy.</summary>
public sealed record EnvironmentLeapingModel(
    string EveryCharacterEffectivelyHas,
    string AtRank,
    string ForThePurposeOf,
    string ThePowerMustBeBoughtToUseItFor,
    bool DistancesAreDeliberatelyAbstract);

/// <summary>p.106's lifting roll: asked for only when the scene is moving.</summary>
public sealed record EnvironmentLiftingModel(
    string MaximumWeightNormally,
    string StaticValueAssumes,
    string RollAskedForWhen,
    string RollIsAskedForBy,
    string RollTrait,
    string RollAgainst,
    string ThresholdDependsOn,
    string ReadOff);

/// <summary>One row of p.106's Lifting table: a weight band, what weighs that, and the threshold.</summary>
public sealed record EnvironmentLiftingRowModel(
    string Weight, IReadOnlyList<string> Examples, int Threshold);

/// <summary>p.107's scorching rule, which is the falling rule applied to heat and current.</summary>
public sealed record EnvironmentScorchingModel(
    IReadOnlyList<string> Sources,
    bool IsAnAttack,
    string ResistedOnlyWith,
    bool GmMayAllowACreativeActiveDefense,
    string WorksLike,
    bool TableIsAGuide);

/// <summary>One row of p.107's Scorching table: a heat source beside an electrical one, and a rank.</summary>
public sealed record EnvironmentScorchingRowModel(string Heat, string Electricity, int Rank);

/// <summary>p.107's Structure rules: what has one, how it moves, and how a thing is broken.</summary>
public sealed record EnvironmentSmashingModel(
    string VehiclesAndComplexMachinesHave,
    string SimpleObjectsHave,
    string StructureDetermines,
    int GmMayAdjustStructureByMin,
    int GmMayAdjustStructureByMax,
    IReadOnlyList<string> AdjustmentFactors,
    bool AdjustmentFactorsAreOpenEnded,
    IReadOnlyList<string> RollTraits,
    string RollAgainst,
    int BendOrSmallHoleMinNetSuccesses,
    int BendOrSmallHoleMaxNetSuccesses,
    int BigHoleMinNetSuccesses,
    bool NetSuccessesMayBeCombinedOverAttempts,
    bool AnEspeciallyThickObjectMayNeedSeveralAttempts);

/// <summary>One row of p.107's Smashing table: the materials that share a Structure rank.</summary>
public sealed record EnvironmentSmashingRowModel(IReadOnlyList<string> Materials, int Structure);

/// <summary>p.107's Smashing table, with the setting note its asterisk points at.</summary>
public sealed record EnvironmentSmashingTableModel(
    IReadOnlyList<EnvironmentSmashingRowModel> Rows,
    IReadOnlyList<string> FootnotedRowMaterials,
    string OzymandiumNote);

/// <summary>
/// p.108's cover rules — and the one place this chapter restates a Chapter 4 figure. p.75's
/// <c>modifier_cover</c> prints both clauses too, which is why the entry carries a
/// <c>corroborated_by</c>.
/// </summary>
public sealed record EnvironmentDamagingCoverModel(
    string AppliesWhen,
    string AttackPenetratesWhen,
    string TargetMayUseTheObjectsStructureAs,
    IReadOnlyList<string> OptionsGiven);

/// <summary>p.108's improvised weapons: the two bonuses, the ceiling, and the wear.</summary>
public sealed record EnvironmentSceneryAsWeaponsModel(
    string AppliesTo,
    int CloseCombatBonusDice,
    string ThrownAttackTrait,
    int ThrownAttackBonusDice,
    string ThrownAttackIs,
    string AttackRankCapsAt,
    int CapBonusDice,
    string WorkedExampleObject,
    int WorkedExampleObjectBody,
    int WorkedExampleMaximumAttackRank,
    string SecondWorkedExampleObject,
    int SecondWorkedExampleStructureFromTheTable,
    int SecondWorkedExampleThicknessAdjustment,
    int SecondWorkedExampleObjectStructure,
    int SecondWorkedExampleMaximumAttackRank,
    int DegradationDicePerPage,
    string DegradationAppliesTo,
    bool DegradationIsOnlyForThesePurposes,
    bool OrdinaryHumanStrengthDegradesNothing,
    string EdgeCasesLeftTo);

/// <summary>One row of p.108's Scenery table.</summary>
/// <param name="MaximumAttackRank">
/// The printed third column, which is the row's Structure plus the six dice the rule above allows.
/// The book prints it out; it is not this project's arithmetic.
/// </param>
public sealed record EnvironmentSceneryRowModel(
    IReadOnlyList<string> Scenery, int Structure, int MaximumAttackRank);

/// <summary>p.108's rule for an object too big to have a useful Structure.</summary>
public sealed record EnvironmentMassiveObjectsModel(
    string WorksLike,
    string UsesInsteadOfBodyOrStructure,
    string Requires,
    string AlwaysBreaksApartAfter);

/// <summary>One row of p.108's Massive Objects table.</summary>
public sealed record EnvironmentMassiveObjectRowModel(
    IReadOnlyList<string> Objects, int WeightRank, int MaximumAttackRank);

/// <summary>p.108's definition of a toxin, and the scope of the three options beneath it.</summary>
public sealed record EnvironmentToxinsModel(
    IReadOnlyList<string> ToxinsAre,
    string ToxinsAreDescribedAs,
    string WorkLike,
    string ResistedOnlyWith,
    IReadOnlyList<string> PassiveDefensesNamed,
    bool ResistanceIsAPower,
    string TheProsAndConsApplyOnlyTo,
    string Unless);

/// <summary>
/// One of the three Pros and Cons pp.108-109 print, recorded as a pointer and nothing else.
///
/// <para><b><c>TranscribedHere</c> is false and every other field is a reference.</b> All three are
/// already priced on the Power they modify in <c>data/rules/powers.json</c>, and a second copy in
/// this store would be a second thing to disagree with the first — the same policy
/// <c>resolve.json</c>'s <c>spend_combat</c> follows towards Chapter 4.</para>
/// </summary>
public sealed record EnvironmentToxinOptionModel(
    bool TranscribedHere,
    string PrintedName,
    string OptionKind,
    string DetailStore,
    string PowerId,
    string OptionId);

/// <summary>One row of p.109's Diseases table.</summary>
/// <param name="Options">
/// The Pros and Cons the printed parenthesis names, as the ids they carry in the character rules —
/// referenced rather than transcribed, for the reason
/// <see cref="EnvironmentToxinOptionModel"/> gives.
/// </param>
public sealed record EnvironmentDiseaseRowModel(
    string Name,
    string Power,
    int Rank,
    IReadOnlyList<string> Options,
    bool Footnoted);

/// <summary>p.109's Diseases table, with the note its one asterisk points at.</summary>
public sealed record EnvironmentDiseasesTableModel(
    IReadOnlyList<EnvironmentDiseaseRowModel> Rows,
    IReadOnlyList<string> FootnotedRowNames,
    bool OptionsAreReferencedNotTranscribed,
    string StaphInfectionNote);

/// <summary>
/// One Power a row of p.109's Drugs and Poisons table inflicts. Most rows have one; Mustard Gas has
/// two, and the two venoms print a span of ranks rather than a figure.
/// </summary>
/// <param name="Traits">
/// The Traits the printed parenthesis names, for the two Drain rows. Empty for every other row —
/// and the entry's <c>ambiguity</c> records that Chapter 2's Drain is defined over a Source rather
/// than over named Traits.
/// </param>
public sealed record EnvironmentToxinEffectModel(
    string Power, IReadOnlyList<string> Traits, int RankMin, int RankMax);

/// <summary>One row of p.109's Drugs and Poisons table.</summary>
public sealed record EnvironmentDrugRowModel(
    string Name,
    IReadOnlyList<EnvironmentToxinEffectModel> Effects,
    IReadOnlyList<string> Options,
    bool Footnoted);

/// <summary>p.109's Drugs and Poisons table, with the note its one asterisk points at.</summary>
public sealed record EnvironmentDrugsTableModel(
    IReadOnlyList<EnvironmentDrugRowModel> Rows,
    IReadOnlyList<string> FootnotedRowNames,
    bool OptionsAreReferencedNotTranscribed,
    string AncientPoisonNote);

/// <summary>
/// This project's reading of how a printed table's columns line up, kept apart from the
/// transcription of it for the reason <c>play-rules.md</c> states: a fact field is a claim that the
/// page states the thing, and the pairing is a claim about the extraction.
/// </summary>
public sealed record EnvironmentInterpretationModel(string WhatThisIs, string RowAlignment);

/// <summary>One entry of <c>environment.json</c> — Chapter 7, pp.105-109.</summary>
public sealed record EnvironmentEntry(
    string Id,
    string Name,
    string Kind,
    string PrintedUnder,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    IReadOnlyList<string>? CorroboratedBy,
    string? Ambiguity,
    EnvironmentDisasterModel? Disaster,
    IReadOnlyList<EnvironmentDisasterResultModel>? DisasterResults,
    EnvironmentEnergyModel? Energy,
    EnvironmentEnergyTypesModel? EnergyTypes,
    EnvironmentFallingModel? Falling,
    IReadOnlyList<EnvironmentFallingRowModel>? FallingTable,
    EnvironmentHostileEnvironmentModel? HostileEnvironment,
    EnvironmentSuffocationModel? Suffocation,
    EnvironmentSwimmingModel? Swimming,
    EnvironmentLeapingModel? Leaping,
    EnvironmentLiftingModel? Lifting,
    IReadOnlyList<EnvironmentLiftingRowModel>? LiftingTable,
    EnvironmentScorchingModel? Scorching,
    IReadOnlyList<EnvironmentScorchingRowModel>? ScorchingTable,
    EnvironmentSmashingModel? Smashing,
    EnvironmentSmashingTableModel? SmashingTable,
    EnvironmentDamagingCoverModel? DamagingCover,
    EnvironmentSceneryAsWeaponsModel? SceneryAsWeapons,
    IReadOnlyList<EnvironmentSceneryRowModel>? SceneryTable,
    EnvironmentMassiveObjectsModel? MassiveObjects,
    IReadOnlyList<EnvironmentMassiveObjectRowModel>? MassiveObjectsTable,
    EnvironmentToxinsModel? Toxins,
    EnvironmentToxinOptionModel? ToxinOption,
    EnvironmentDiseasesTableModel? DiseasesTable,
    EnvironmentDrugsTableModel? DrugsAndPoisonsTable,
    EnvironmentInterpretationModel? Interpretation);
