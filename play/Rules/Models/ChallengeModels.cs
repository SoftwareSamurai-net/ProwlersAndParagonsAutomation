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

public sealed record ChallengeBandModel(
    int? MaxNetSuccesses,
    string Outcome,
    bool? Embellishment,
    int? MinNetSuccesses
);

public sealed record ChallengeActorSelectionModel(
    string ActorIs,
    string OpponentIs,
    string WhenTwoOrMorePursueTheSameGoal,
    bool GmIsOpponentWhenUnopposed,
    string GmAcceptsPlayerInputWhenActorIsNpc
);

public sealed record ChallengeThresholdModel(
    string Difficulty,
    int ThresholdMin,
    int? ThresholdMax
);

public sealed record ChallengeInterpretationModel(
    string WhatThisIs,
    IReadOnlyList<string> GmDiscretionDifficulties
);

public sealed record ChallengeOpposedModel(
    string ThresholdSource,
    string StaticThresholdUsedWhen
);

public sealed record ChallengeConditionModifierModel(
    int MinDice,
    int MaxDice,
    string AppliedBy,
    string MayApplyTo,
    IReadOnlyList<string> ExamplesGiven
);

public sealed record ChallengeEmbellishmentModel(
    string HeldBy,
    bool MustNotContradictTheNarration,
    bool MustNotRenderItMeaningless,
    string Size
);

public sealed record ChallengeCompromiseModel(
    string OfferedBy,
    string Trade,
    bool RequiresAgreementOfBoth,
    bool OpponentMayRefuse
);

public sealed record ChallengeCheckingYourSwingModel(
    IReadOnlyDictionary<string, int> SuccessMap,
    string SixesExplode,
    int ExplodeCostResolve,
    bool DecidedAfterTheRoll,
    bool ExplosionRecursesWhileSixesKeepComing,
    bool OtherExplodeOffersProvideNoExtraBenefit
);

public sealed record ChallengeAssistModel(
    int HelperRollsAgainstThreshold,
    string HelperThresholdDifficulty,
    int NetSuccessesPerBonusDie,
    string Rounding,
    string BonusFormula,
    bool BestHelperOnly
);

public sealed record ChallengeGroupActionModel(
    string Trigger,
    bool EveryoneRollsIndividually,
    int DistributeAboveNetSuccesses,
    bool AboveIsStrict,
    int MinimumNetSuccessesToDistribute,
    string DistributeTo,
    IReadOnlyList<string> ExamplesGiven
);

public sealed record ChallengeContestModel(
    string Structure,
    int TypicalExchanges,
    int ArduousExchangesMin,
    string ArduousCondition,
    bool ExchangeWinnerNarratesThatExchange,
    int ExchangeWinBonusDiceNextExchange,
    bool FinalExchangeDecidesTheContest,
    int? ArduousExchangesMax
);

public sealed record ChallengeDefiningMomentModel(
    string DeclaredBy,
    bool SixesExplode,
    int SixStillWorthSuccesses,
    bool ExplosionRecursesWhileSixesKeepComing,
    int DicePerResolveSpent,
    int OrdinaryDicePerResolveSpent,
    int LimitPerStory,
    int LimitPerScenePerGroup,
    bool ConcurrentScenesEachAllowOne,
    bool MayLastLongerThanAnInstant,
    int AftermathPermanentAbilityLossDice,
    IReadOnlyList<string> PhysicalTaskReducesOneOf,
    IReadOnlyList<string> MentalTaskReducesOneOf,
    bool AbilityMayBeBoughtBackLater
);

public sealed record ChallengeOrdinaryGamesOptionModel(
    bool OfferedAtGmOption,
    bool ReplacesThePermanentAbilityLoss,
    bool Mandatory
);

public sealed record ChallengeOneShotVariantModel(
    string Context,
    int HealthAfter,
    bool Unconscious,
    string RegainConsciousness,
    int ChallengeRollPenaltyDice,
    string PenaltyDuration,
    ChallengeOrdinaryGamesOptionModel OrdinaryGamesOption
);

public sealed record ChallengeJudgingGuidelineModel(
    string Descriptor,
    string Difficulty,
    int Threshold
);

public sealed record ChallengeEntry(
    string Id,
    string Name,
    string Kind,
    IReadOnlyList<ChallengeBandModel>? Bands,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    IReadOnlyList<string>? CorroboratedBy,
    ChallengeActorSelectionModel? ActorSelection,
    string? Ambiguity,
    IReadOnlyList<ChallengeThresholdModel>? Thresholds,
    ChallengeInterpretationModel? Interpretation,
    string? NamingConvention,
    ChallengeOpposedModel? Opposed,
    ChallengeConditionModifierModel? ConditionModifier,
    ChallengeEmbellishmentModel? Embellishment,
    ChallengeCompromiseModel? Compromise,
    string? SilverLiningsAndComplicationsDecidedBy,
    bool? MixablePerPlayer,
    ChallengeCheckingYourSwingModel? CheckingYourSwing,
    ChallengeAssistModel? Assist,
    ChallengeGroupActionModel? GroupAction,
    ChallengeContestModel? Contest,
    ChallengeDefiningMomentModel? DefiningMoment,
    ChallengeOneShotVariantModel? OneShotVariant,
    IReadOnlyList<ChallengeJudgingGuidelineModel>? JudgingGuideline,
    string? CalibratedFor,
    bool? WingItIsEndorsed
);
