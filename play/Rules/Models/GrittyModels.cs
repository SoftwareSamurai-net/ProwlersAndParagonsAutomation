// Written from the shipped data/rules/play JSON. Every key of every
// entry in the file this covers has a property here, because PlayRulesFileCoverageTests
// deserializes the file with JsonUnmappedMemberHandling.Disallow: a field nothing reads is a
// field nothing can hold to the rulebook, which is the failure creation_rules.json shipped.
//
// Prose, `ambiguity` and `interpretation` are modelled too. They are part of the file, and a
// reader of an entry needs to see the ambiguity beside the number. What the engine may *consume*
// is narrower — see docs/guide/play-engine.md — and where it consumes an interpretation it says
// so in a doc comment naming the entry.

namespace ProwlersAndParagonsAutomation.Play.Rules.Models;

public sealed record GrittyOverviewModel(
    string DefaultCombatIs,
    bool RulesAreOptional,
    bool AnySubsetMayBeUsed,
    bool ReviewBeforeAdopting,
    bool ARetconOrDoOverIsAllowedIfARuleIsDroppedAfterPlay
);

public sealed record GrittyActiveDefensePenaltyModel(
    bool ActiveDefensesAreMinorActions,
    int CumulativePenaltyDicePerExtraActiveDefense,
    bool FirstActiveDefenseOnAPageIsUnpenalised,
    string CountedPer,
    bool AffectsPassiveDefenses
);

public sealed record GrittyCloseRangePenaltyModel(
    int PenaltyDiceToActiveDefense,
    string AppliesAgainst,
    string AppliesOnlyToAttacksUsableAt,
    string IgnoredFor
);

public sealed record GrittyTheDropModel(
    string HeldBy,
    string HeldAgainst,
    string Effect,
    string AlsoHeldBy,
    IReadOnlyList<string> ExamplesGiven,
    string FinalSay
);

public sealed record GrittyFatalDamageModel(
    bool HealthCanGoNegative,
    string KilledAt,
    int CostResolveToAvoid,
    string ResolveReducesDamageTo,
    bool ResolveMayBeSpentOnDamageYouInflictOnSomeoneElse,
    bool ResolveAlsoStabilisesIfNecessary,
    int DyingBeginsWhenLethalDamageReducesYouTo,
    int DyingDamagePerPage,
    string DyingEndsAt,
    string StabiliseRoll,
    string StabiliseDifficulty,
    int StabiliseThreshold,
    string StabiliseAlsoBy,
    int CostResolveToStabiliseImmediately,
    bool InstantRecoveryRequiresBeingStable
);

public sealed record GrittyInterpretationModel(
    string WhatThisIs,
    string? ResolveReducesDamageTo,
    int? OnePointEveryHoursForTheLowestBand,
    int? FatalDamageIsRequiredBelowHealth
);

public sealed record GrittyFriendlyFireModel(
    int PenaltyDice,
    string AppliesWhen,
    int SecondAttackTriggeredAtNetSuccesses,
    string SecondAttackIsAgainst,
    int SecondAttackPenaltyDice,
    string SecondTargetSelectedBy,
    string SecondTargetSelected
);

public sealed record GrittyHardTargetsModel(
    string AppliesTo,
    string PassiveDefenseRank,
    int PenaltyDiceToNegateIt,
    string NegationAvailableAgainst,
    string RecommendedProForVehicleScaleWeapons,
    string RecommendedProForThePhysicalAttacksOfPowerfulSuperhumanCharacters
);

public sealed record GrittyGearLimitModel(
    string WhatItIs,
    int DefaultRank,
    IReadOnlyList<int> RaisedOptions,
    bool RaisedOptionsAreOpenEnded,
    string WorkedExampleWeapon,
    int WorkedExampleWeaponBonusDice,
    int WorkedExampleMaximumEffectiveRankAtTheDefaultLimit,
    string DetailChapter
);

public sealed record GrittyBandModel(
    int? MaxToughness,
    int HealthPerDay,
    int? MinToughness,
    int? OnePointEveryHours
);

public sealed record GrittySlowHealingModel(
    IReadOnlyList<GrittyBandModel> Bands,
    bool HealingAfterEachBattle,
    bool HealingOnRegainingConsciousnessAfterADefeat,
    bool YouMayBeConsciousAtZeroOrNegativeHealth,
    bool InThatConditionAnyDamageAtAllDefeatsYou,
    bool StabilizationAvailableAsOftenAsNecessary,
    string MedicineHealingLimit,
    int MedicineHealthPerNetSuccesses,
    int MedicineNetSuccessesPerPoint
);

public sealed record GrittyToughMinionsModel(
    int NetSuccessesPerMinionDefeated,
    bool FullNetSuccessesRequired,
    string Rounding,
    bool RoundingIsANamedUniqueException,
    int WorkedExampleNetSuccesses,
    int WorkedExampleMinionsDefeated,
    int AreaAttackMinionsPerNetSuccess,
    int AreaAttackRateItReplaces,
    string AlternativeOffered
);

public sealed record GrittyWoundPenaltiesModel(
    int AtOrBelowHalfFullHealthPenaltyDice,
    int AtOrBelowZeroHealthPenaltyDice,
    string ZeroOrLessParenthetical,
    string AppliesTo,
    int CostResolveToIgnore,
    int PagesIgnoredPerResolvePoint
);

public sealed record GrittyEntry(
    string Id,
    string Name,
    string Kind,
    string PrintedUnder,
    GrittyOverviewModel? Overview,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    GrittyActiveDefensePenaltyModel? ActiveDefensePenalty,
    GrittyCloseRangePenaltyModel? CloseRangePenalty,
    GrittyTheDropModel? TheDrop,
    GrittyFatalDamageModel? FatalDamage,
    GrittyInterpretationModel? Interpretation,
    string? Ambiguity,
    GrittyFriendlyFireModel? FriendlyFire,
    GrittyHardTargetsModel? HardTargets,
    GrittyGearLimitModel? GearLimit,
    GrittySlowHealingModel? SlowHealing,
    GrittyToughMinionsModel? ToughMinions,
    GrittyWoundPenaltiesModel? WoundPenalties
);
