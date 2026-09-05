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

public sealed record CombatPageModel(
    string APageIs,
    int TurnsPerCharacterPerPage,
    string PageEndsWhen
);

public sealed record CombatEdgeModel(
    string Formula,
    string ActsInOrder,
    string OptionalRandomInitiative,
    string RandomInitiativeEffectiveEdge,
    string RandomInitiativeLasts
);

public sealed record CombatTieBreakModel(
    IReadOnlyList<string> Order,
    string StillTiedAct,
    bool SimultaneousCharactersCanKnockEachOtherOut,
    bool MinionsHaveAnEdge,
    string MinionsAct,
    string MinionAlliesAndEnemiesAct
);

public sealed record CombatHoldingModel(
    bool MayHoldInReserve,
    string WaitingFor,
    string IfItNeverHappens,
    string OrderAmongHolders
);

public sealed record CombatSeizeInitiativeModel(
    int CostResolve,
    string Effect,
    string Duration,
    bool SeizersGoBeforeEveryoneElse,
    string OrderAmongSeizers
);

public sealed record CombatGmAlternativeModel(
    string InsteadOf,
    string Effect,
    string ChosenBy,
    string Rationale
);

public sealed record CombatInterpretationModel(
    string WhatThisIs,
    string? DurationIsInheritedFrom,
    string? AverageRounds,
    string? DurationRounds,
    string? ReductionRounds
);

public sealed record CombatActionsModel(
    string OnYourTurn,
    string AnActionIs,
    bool AttacksAreTheCommonestAction,
    bool DefendingYourselfIsAvailable,
    string FreeActionsAllowed,
    IReadOnlyList<string> FreeActionExamples
);

public sealed record CombatMultipleActionsModel(
    int PenaltyDicePerExtraAction,
    string AppliesTo,
    bool MustBeDeclaredBeforeAnyChallengeRoll,
    bool AppliesToDefenseRolls,
    bool AppliesToOtherChallengeRolls,
    bool SameTargetMoreThanOncePerPage,
    bool ExtraActionsBuyExtraMovement
);

public sealed record CombatRangeModel(
    string Class,
    string Covers
);

public sealed record CombatRangeRulesModel(
    bool MeasuredPrecisely,
    string InitialRangeClassSetBy,
    string CloseCombatAttacksRequire,
    string CloseCombatAttacksReach,
    string RangedAttacksReach,
    IReadOnlyList<string> ExceptionsGiven
);

public sealed record CombatEstimatesModel(
    int CloseFeet,
    int DistantFeet,
    int ExtremeFeet,
    string StatedAs
);

public sealed record CombatThrowingModel(
    string OrdinaryPeopleReach,
    int TableUsedWhenMightExceeds,
    string RankFormula,
    int MinimumRank,
    bool AccuracyIsWhatIsMeasured
);

public sealed record CombatThrowingTableModel(
    int MinRank,
    int? MaxRank,
    string Range
);

public sealed record CombatMovementModel(
    int PagesToCloseOrOpenWithinCloseRange,
    int PagesPerRangeClass,
    int PagesPerRangeClassWithATravelPower,
    int TravelPowerRankRequired,
    bool MovingPreventsActions,
    string AssumedTerrain,
    int OpenTerrainGmMayAllowRangeClassesPerPageMin,
    int OpenTerrainGmMayAllowRangeClassesPerPageMax,
    string OpenTerrainAllowanceAppliesTo,
    bool OpenTerrainAllowanceIsGmDiscretion
);

public sealed record CombatMovementContestModel(
    string Trigger,
    string Roll,
    string OnFootAgainstATravelPowerUses,
    string WinnerGets
);

public sealed record CombatChaseModel(
    string Structure,
    int PagesPerExchange,
    string Roll,
    string OnFootAgainstATravelPowerUses,
    bool TheRollIsAnAction,
    bool EachPursuerPicksOneQuarry,
    int ExchangeWinBonusDiceNextExchange,
    int NetSuccessesToMoveOneRangeClass,
    string EndsCloserThan,
    string EndsFartherThan,
    string AtCloserThanCloseRange,
    string AtFartherThanExtremeRange
);

public sealed record CombatAttackModel(
    string Roll,
    string ThresholdSource,
    string OnMoreSuccessesThanTheTarget,
    string OnFailingTheThreshold,
    bool AccuracyAndDamageAreOneTrait,
    bool DefenseAndDamageResistanceAreOneTrait,
    string DefenderUses
);

public sealed record CombatAttackDefenseTableModel(
    string Type,
    string AttackTrait,
    IReadOnlyList<string> DefenseTraits
);

public sealed record CombatDefensesModel(
    string ActiveRepresent,
    string PassiveRepresent,
    IReadOnlyList<string> CommonActiveTraits,
    IReadOnlyList<string> CommonPassiveTraits,
    IReadOnlyList<string> ActiveUnusableWhen,
    bool ACrampedOrAwkwardPositionPreventsActiveDefenses,
    bool LosingYourNextTurnPreventsActiveDefenses,
    int DefensesUsedPerAttack,
    string DefenseChosen
);

public sealed record CombatDamageTypesModel(
    bool LethalIsTheMoreDangerous,
    string ToughnessAgainstLethal,
    string ToughnessAgainstSubdual,
    IReadOnlyList<string> SubdualSourcesGiven,
    string PhysicalDamageDefault,
    string PsychicDamageIs,
    string PsychicDamageResistedWith
);

public sealed record CombatBandModel(
    string? Cover,
    int Dice,
    string? AttackerRelativeSize,
    string? Visibility
);

public sealed record CombatCoverModel(
    string Affects,
    IReadOnlyList<CombatBandModel> Bands,
    bool ACompletelyHiddenTargetCannotBeHit,
    string AttackingThroughCoverRequires,
    bool TargetMayUseTheCoversStructureAsAPassiveDefense
);

public sealed record CombatSizeModel(
    string Affects,
    IReadOnlyList<CombatBandModel> Bands
);

public sealed record CombatVisibilityModel(
    string Affects,
    IReadOnlyList<CombatBandModel> Bands,
    IReadOnlyList<string> PoorExamples,
    IReadOnlyList<string> NoneExamples,
    bool AnInvisibleOpponentCountsAsNoVisibility,
    IReadOnlyList<string> PowersThatCompensateGiven
);

public sealed record CombatDamageModel(
    int DamagePerNetSuccess,
    string Reduces,
    int DefeatedAtHealth,
    string DefeatedMeans,
    bool DeathOnlyUnderTheGrittyCombatRules
);

public sealed record CombatHealthModel(
    string Formula,
    bool VillainsUseTheSameFormula,
    bool FoesHalveTheResult,
    bool NpcTotalsAreSuggestions,
    bool MinionsUseHealth
);

public sealed record CombatReferenceModel(
    bool TranscribedHere,
    string DetailChapter,
    IReadOnlyList<string> DeferredTopics
);

public sealed record CombatHealingModel(
    string Roll,
    string AfterAFightDifficulty,
    int AfterAFightThreshold,
    int HealthPerNetSuccess,
    string AlsoAvailableAfter,
    int FullRestHours,
    string FullRestDifficulty,
    int FullRestThreshold,
    string RemovedNpcsRecover
);

public sealed record CombatSpecialEffectModel(
    IReadOnlyList<string> SourcesGiven,
    string DurationFormula,
    string ExpiresAt,
    bool DurationStacksByAttackingTheSameTargetAgain,
    string DefeatedWhenTheDurationReaches,
    string DefeatByEffectLasts
);

public sealed record CombatBreakingFreeModel(
    string AvailableWhen,
    string TakenOn,
    string Roll,
    IReadOnlyList<string> RollExamplesGiven,
    string ThresholdSource,
    string DurationReducedBy,
    int FreeWhenTheDurationReaches,
    bool MayActOnTheSamePageWhenFreed
);

public sealed record CombatKeepingHoldModel(
    string Trigger,
    int CostResolve,
    string ExtendsTo,
    bool MayBeRepeatedSceneAfterScene
);

public sealed record CombatInstantRecoveryModel(
    int CostResolve,
    string TakenOn,
    bool AfterADamagingDefeatRegainsConsciousness,
    int AfterADamagingDefeatRestoresHealth,
    bool AlsoFreesYouFromASpecialEffect,
    bool RequiresBeingDefeatedToFreeYourselfFromAnEffect,
    int LimitPerScene
);

public sealed record CombatGrapplingModel(
    string AGrabIs,
    string AHoldIs,
    string AnEscapeIs,
    string Roll,
    string ThresholdSource,
    bool OpponentMayUseAnActiveDefenseInsteadWhenNotAlreadyGrappling,
    bool InflictingOrdinaryDamageInCloseCombatNeedsNoSpecialRules
);

public sealed record CombatGrapplingTableModel(
    int? MaxNetSuccesses,
    string Grab,
    string Hold,
    string Escape,
    int? MinNetSuccesses
);

public sealed record CombatGrabModel(
    string PartialMeans,
    bool PartialBlocksActiveDefensesAgainstAnyoneElse,
    string PartialResolvedBy,
    bool MayExitByLettingGoOfTheObject,
    string FullMeans,
    bool FullAllowsUsingOrTossingItTheSamePage,
    bool FullSuffersTheMultipleActionPenalty,
    bool FullIsInEffectAFreeAction
);

public sealed record CombatHoldModel(
    string PartialMeans,
    bool PartialBlocksActiveDefensesAgainstAnyoneElse,
    string PartialOnlyPhysicalAction,
    string FullMeans,
    string FullLeavesTheHeldCharacterOnly,
    bool FullAllowsAttacksOnSubsequentPages,
    string FullDamageRoll,
    string FullDamageThresholdSource,
    string FullSubmissionRoll,
    string FullSubmissionThresholdSource,
    string AHeldCharacterMayUse,
    IReadOnlyList<string> PowersProbablyUnavailableWhenHeld,
    string AdjudicatedCaseByCaseBy
);

public sealed record CombatEscapeModel(
    string PartialOutOfAPartialHold,
    string PartialOutOfAFullHold,
    string Full,
    bool MayAlsoExitGrapplingCompletely
);

public sealed record CombatCombatStuntModel(
    string WhatItIs,
    string WorksLike,
    string TraitsUsedAreChosenBy,
    string EffectDescribedBy,
    string Duration,
    bool DelayingYourNextActionExtendsIt,
    bool PenaltiesFromMultipleStuntsAreCumulative
);

public sealed record CombatSampleStuntModel(
    string Name,
    IReadOnlyList<string> AttackTraits,
    IReadOnlyList<string> DefenseTraits
);

public sealed record CombatCombatStuntBandModel(
    int? MaxNetSuccesses,
    IReadOnlyList<string> SampleEffects,
    int? MinNetSuccesses
);

public sealed record CombatMinionsModel(
    string OnlyCharacteristic,
    bool ActInGroupsRatherThanAsIndividuals
);

public sealed record CombatThreatRankModel(
    string Category,
    int MinThreat,
    int? MaxThreat
);

public sealed record CombatAttackingMinionsModel(
    bool MinionsHaveHealth,
    int MinionsDefeatedPerNetSuccess,
    int MinionsDefeatedPerNetSuccessWithAnAreaAttack,
    string AreaAttackCappedBy,
    int MaximumMinionsPerNetSuccess,
    bool EffectsThatDoubleTheRateDoNotStack,
    string OnADamagingAttack,
    string OnASpecialEffect
);

public sealed record CombatMinionsAttackingModel(
    string AGroupActsLike,
    bool AGroupMaySplitToAttackMultipleEnemies,
    int TargetsPerGroupPerPage,
    int AttackRollsPerGroupPerPage,
    int DefenseRollsOpposingIt,
    string TheGroupBonusAppliesTo,
    string TheGroupBonusDoesNotApplyTo,
    int MaximumAttackingOneTargetInCloseCombat,
    int MaximumAttackingOneTargetAtRange
);

public sealed record CombatMinionGroupAttackModel(
    int MinMinions,
    int MaxMinions,
    int BonusDice
);

public sealed record CombatAmbushModel(
    string Roll,
    string DeceptionOrSeductionRoll,
    string ThresholdSource,
    string OnSuccess,
    bool ASurprisedTargetCanAct,
    bool ASurprisedTargetCanUseActiveDefenses,
    string SurpriseLasts,
    bool EmbellishmentRightsAllowPartialSurprise,
    string PartialSurpriseKeeps,
    string PartialSurpriseLimitPrintedAs,
    string OnFailure,
    bool MultipleAmbushersMayRollAsAGroup,
    bool EveryTargetRollsTheirOwnPerception,
    bool MinionsRollPerceptionInGroups
);

public sealed record CombatAreaAttackModel(
    string Targets,
    int AttackRolls,
    string DefenseRolls,
    IReadOnlyList<string> AnActiveDefenseMustEither,
    IReadOnlyList<string> ExamplesGiven
);

public sealed record CombatChargeModel(
    string WhatItIs,
    IReadOnlyList<string> AttackTraits,
    bool SwimmingMayBeUsedOnlyUnderwater,
    int AttackBonusDice,
    string OwnActiveDefenseRanks,
    string PenaltyLasts,
    string IfTheTargetUsesAPassiveDefense,
    string SelfDamageReducedBy
);

public sealed record CombatClobberingModel(
    string WhatItIs,
    int AttackRolls,
    int AttackPenaltyDice,
    string DefenseRolls,
    IReadOnlyList<string> Targets,
    string ThePrimaryTargetIs,
    bool StopsIfThePrimaryDefendsActivelyAndTakesNoDamage
);

public sealed record CombatDefendingOthersModel(
    string Cost,
    string Range,
    string Effect,
    bool MayUseAnActiveOrAPassiveDefense,
    string AnActiveDefenseLeavesTheDamageOn,
    bool TheProtectedCharacterMayStillUseAPassiveDefense,
    string APassiveDefenseLeavesTheDamageOn
);

public sealed record CombatAllOutAttackModel(
    int AttackBonusDice,
    string DefenseRanks,
    bool AffectsActiveDefenses,
    bool AffectsPassiveDefenses,
    string Lasts,
    string OpponentsWhoCouldNotPenetrateYourPassiveDefense
);

public sealed record CombatAllOutDefenseModel(
    int DefenseBonusDice,
    string Lasts,
    bool PreventsAttacking,
    bool PreventsOtherActions,
    bool AllowsMovement,
    bool AllowsFreeActions,
    bool ATravelPowerOrSpeedMayBeUsedAsAnActiveDefense
);

public sealed record CombatKnockbackModel(
    string RequiresDamageType,
    int MinimumDamage,
    int CostResolve,
    string TargetIsThrownAsIfByAMightRankEqualTo,
    bool TargetFallsProne,
    bool TargetLosesTheirNextTurnToAct,
    string DamageOnStrikingASolidObject,
    bool TheObjectMustBeTougherThanTheTarget,
    string APassiveDefenseAboveTheObjectsStructure
);

public sealed record CombatLuringModel(
    string WhatItIs,
    IReadOnlyList<string> AppliesToAttackTypes,
    string DeclaredBefore,
    bool RequiresAnActiveDefense,
    int DefenseMustExceedTheAttackRollBy,
    int CostResolve,
    string RedirectsTo,
    bool MayRedirectOntoAPerson,
    string RedirectingOntoAPersonCosts,
    bool TheNewTargetMakesTheirOwnDefenseRoll
);

public sealed record CombatTeamAttackModel(
    string WhatItIs,
    string ParticipantsActAt,
    bool AllParticipantsMustTargetTheSameEnemy,
    int AttackBonusDice,
    int CostResolveToMakeSixesExplode,
    bool ExplosionRecursesWhileSixesKeepComing,
    int LimitPerTargetPerBattle,
    string TheLimitMayBeLiftedBy,
    bool UseSparingly
);

public sealed record CombatEntry(
    string Id,
    string Name,
    string Kind,
    string PrintedUnder,
    CombatPageModel? Page,
    string? PrintedUnderNote,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    CombatEdgeModel? Edge,
    IReadOnlyList<string>? CorroboratedBy,
    CombatTieBreakModel? TieBreak,
    CombatHoldingModel? Holding,
    CombatSeizeInitiativeModel? SeizeInitiative,
    CombatGmAlternativeModel? GmAlternative,
    CombatInterpretationModel? Interpretation,
    string? Ambiguity,
    CombatActionsModel? Actions,
    CombatMultipleActionsModel? MultipleActions,
    IReadOnlyList<CombatRangeModel>? Ranges,
    CombatRangeRulesModel? RangeRules,
    CombatEstimatesModel? Estimates,
    CombatThrowingModel? Throwing,
    IReadOnlyList<CombatThrowingTableModel>? ThrowingTable,
    CombatMovementModel? Movement,
    CombatMovementContestModel? MovementContest,
    CombatChaseModel? Chase,
    CombatAttackModel? Attack,
    IReadOnlyList<CombatAttackDefenseTableModel>? AttackDefenseTable,
    CombatDefensesModel? Defenses,
    CombatDamageTypesModel? DamageTypes,
    CombatCoverModel? Cover,
    CombatSizeModel? Size,
    CombatVisibilityModel? Visibility,
    CombatDamageModel? Damage,
    CombatHealthModel? Health,
    CombatReferenceModel? Reference,
    CombatHealingModel? Healing,
    CombatSpecialEffectModel? SpecialEffect,
    CombatBreakingFreeModel? BreakingFree,
    CombatKeepingHoldModel? KeepingHold,
    CombatInstantRecoveryModel? InstantRecovery,
    CombatGrapplingModel? Grappling,
    IReadOnlyList<CombatGrapplingTableModel>? GrapplingTable,
    CombatGrabModel? Grab,
    CombatHoldModel? Hold,
    CombatEscapeModel? Escape,
    CombatCombatStuntModel? CombatStunt,
    IReadOnlyList<CombatSampleStuntModel>? SampleStunts,
    IReadOnlyList<CombatCombatStuntBandModel>? CombatStuntBands,
    CombatMinionsModel? Minions,
    IReadOnlyList<CombatThreatRankModel>? ThreatRanks,
    CombatAttackingMinionsModel? AttackingMinions,
    CombatMinionsAttackingModel? MinionsAttacking,
    IReadOnlyList<CombatMinionGroupAttackModel>? MinionGroupAttack,
    CombatAmbushModel? Ambush,
    CombatAreaAttackModel? AreaAttack,
    CombatChargeModel? Charge,
    CombatClobberingModel? Clobbering,
    CombatDefendingOthersModel? DefendingOthers,
    CombatAllOutAttackModel? AllOutAttack,
    CombatAllOutDefenseModel? AllOutDefense,
    CombatKnockbackModel? Knockback,
    CombatLuringModel? Luring,
    CombatTeamAttackModel? TeamAttack
);
