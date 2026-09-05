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

public sealed record ResolveTableRowModel(
    int RanksBelowTraitCap,
    int Resolve
);

public sealed record ResolveStartingResolveModel(
    string GrantedAt,
    bool IssueIsAGameSession,
    string DependsOn,
    string MeasuredFrom,
    int AtTraitCap,
    int ResolvePerRankBelowCap,
    string Formula,
    IReadOnlyList<ResolveTableRowModel> TableRows,
    bool TableContinuesBeyondThePrintedRows
);

public sealed record ResolveExceptionsModel(
    bool TalentsCount,
    string Criterion,
    IReadOnlyList<string> NamedPowers,
    string ExpertiseQualifier,
    bool NamedListIsQualifiedAsUsual,
    bool GmHasFinalSay
);

public sealed record ResolveMaximumPossibleRankModel(
    string AppliesWhen,
    IReadOnlyList<string> PowersNamed,
    string RankUsed
);

public sealed record ResolveCarryoverModel(
    bool CarriesOverBetweenIssues,
    bool UnspentIsLostAtIssueEnd,
    bool GmMayAllowCarryover,
    bool GmCarryoverShouldBeRare
);

public sealed record ResolveEarningOverviewModel(
    bool GmMayAwardWheneverTheySeeFit,
    bool ListedWaysAreExamplesNotAClosedList
);

public sealed record ResolveInterpretationModel(
    string WhatThisIs,
    IReadOnlyList<string>? MechanisableEntryIds,
    int? InferredCostPerPointShared
);

public sealed record ResolveEarningModel(
    int? AwardResolve,
    string? Trigger,
    bool? MayBeOutsideCombat,
    int? LimitPerBattle,
    bool? MustFitTheSituation,
    int? SomeFlawsAwardPerIssueInstead,
    bool? AwardStated,
    string? InterludeIs,
    string? DetailChapter,
    bool? NotAnInvitationToDerailTheGame,
    bool? GmJudged,
    IReadOnlyList<string>? ExamplesGiven,
    string? AvailableWhen,
    bool? MustBeSpentOnTheSamePage,
    bool? ThenUnconscious,
    string? UnconsciousUntil
);

public sealed record ResolveSpendingOverviewModel(
    bool GmMayExpandTheUses,
    bool ListedUsesAreTheBasicOnes
);

public sealed record ResolveSpendModel(
    string Currency,
    int? CostPerPointSharedWhenUnableToAssist,
    string? PointsSharedLimit,
    bool? MustNarrateTheAssistance,
    bool? NarrationHasNoMechanicalEffect,
    IReadOnlyList<string>? UnableToAssistExamples,
    bool? FlashbackRequiredWhenUnable,
    int? CostResolve,
    int? DiceGained,
    bool? Unlimited,
    bool? DecidedAfterTheRoll,
    string? Rerolls,
    bool? IncludesDiceBoughtWithResolve,
    string? AppliesTo,
    string? ExampleGiven,
    bool? TranscribedHere,
    string? DetailChapter,
    IReadOnlyList<string>? CombatSpendRefs,
    string? Invents,
    bool? SubjectToGmApproval,
    string? Uses,
    string? ImitatedPowerRankSource,
    bool? RequiresRemotelyReasonable,
    bool? GrantsANewPower,
    bool? SomePowersRequireResolve,
    bool? OnlyHeroesHaveResolve,
    bool? PlayerMustSpendForAFriendlyExtra,
    bool? ExtraCannotUseThePowerIfNobodySpends,
    bool? AppliesOnlyWhileTheExtraIsWithTheHeroes,
    bool? CanDoAnythingResolveCan,
    bool? MayBeSpentOnAnyNpc,
    IReadOnlyList<string>? NpcKinds,
    bool? AllNpcsShareOnePool,
    int? ExclusiveSpendsCount,
    int? CostAdversity,
    string? Prevents,
    string? Duration,
    IReadOnlyList<string>? EligibleCharacters,
    int? LimitPerCharacterPerIssue,
    bool? NpcFlawsBiteWhenTheOpportunityArises,
    bool? NpcsCannotChooseWhenTheirFlawsBite,
    string? WhatItIs,
    IReadOnlyList<string>? ExamplesGiven,
    bool? MustBeAChallengeNotAPunishment,
    bool? MustNotBeAPlotDevice,
    int? LimitPerStory,
    bool? Automatic,
    string? Effect,
    IReadOnlyList<string>? ExcludedCharacters,
    bool? UseSparingly
);

public sealed record ResolveRerollFloorModel(
    bool SpendingShouldNeverMakeThingsWorse,
    bool KeepTheFirstRollIfTheRerollIsWorse,
    string AppliesTo
);

public sealed record ResolveAdversityModel(
    int PointsPerHeroPerIssue,
    string HeldBy,
    bool CarriesOverBetweenIssues,
    bool IsMoreOfAFixedResourceThanResolve,
    bool GmMayAddWaysToEarn,
    bool ShouldNotBeAsEasyToEarnAsResolve
);

public sealed record ResolveLevelGuidanceModel(
    int Level,
    string UsedFor
);

public sealed record ResolveChallengeLevelModel(
    IReadOnlyList<string> AwardFactors,
    string AwardOperation,
    string AwardedAt,
    int TypicalLevelMin,
    int TypicalLevelMax,
    bool LevelThreeMayBeExceeded,
    bool OnlyAHandfulOfScenesPerStory,
    bool MayBeSavedForLaterInTheIssue,
    IReadOnlyList<ResolveLevelGuidanceModel> LevelGuidance
);

public sealed record ResolveUnheroicActionModel(
    int AwardAdversity,
    bool AwardedImmediately,
    IReadOnlyList<string> Triggers,
    bool AlsoWhenContraryToMotivation,
    bool AppliesEvenIfCoerced,
    IReadOnlyList<string> CoercionForms
);

public sealed record ResolveEntry(
    string Id,
    string Name,
    string Kind,
    string PrintedUnder,
    ResolveStartingResolveModel? StartingResolve,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    IReadOnlyList<string>? CorroboratedBy,
    string? Ambiguity,
    ResolveExceptionsModel? Exceptions,
    ResolveMaximumPossibleRankModel? MaximumPossibleRank,
    ResolveCarryoverModel? Carryover,
    ResolveEarningOverviewModel? EarningOverview,
    ResolveInterpretationModel? Interpretation,
    ResolveEarningModel? Earning,
    ResolveSpendingOverviewModel? SpendingOverview,
    string? Who,
    ResolveSpendModel? Spend,
    ResolveRerollFloorModel? RerollFloor,
    ResolveAdversityModel? Adversity,
    ResolveChallengeLevelModel? ChallengeLevel,
    ResolveUnheroicActionModel? UnheroicAction
);
