using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Holds <c>data/rules/play/*.json</c> to <see cref="CanonicalChallengeRules"/> and
/// <see cref="CanonicalResolveRules"/>, which are Chapters 3 and 5 transcribed from the page.
///
/// <para><b>Nothing in the application reads these files yet</b>, and that is exactly why the
/// tests have to. Unverified data that no code loads is the worst of both worlds: it reads as a
/// source of truth, it is what a simulator will be built on, and until something asserts against
/// the book a wrong number here costs nothing to introduce. The 141 Powers were verified before
/// anything trusted them; this is the same order of operations.</para>
///
/// <para><b>The models below are test-local on purpose.</b> Play rules do not go into
/// <c>engine/</c>, and this slice deliberately adds no project of its own — the JSON is
/// deserialized straight into records that live here. Real models arrive with the simulator.</para>
///
/// <para><b>The one thing here that reaches into <c>engine/</c> is a read.</b> Chapter 5's Resolve
/// table is the single mechanic in these files the character engine already implements, and
/// <see cref="TheEngineComputesTheTableThisFileRecords"/> holds the two to the same answer. That is
/// a test comparing two independent statements of one rule; it is not the engine learning to read
/// play rules, which <see cref="PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile"/>
/// continues to forbid.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PlayRulesDataTests
{
    private readonly RulesFixture _f;

    public PlayRulesDataTests(RulesFixture fixture) => _f = fixture;

    private static string PlayDataPath => Path.Combine(RulesFixture.RepoRoot, "data", "rules", "play");
    private static string RulebookPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    /// <summary>
    /// The three play rules files, each with the chapter it transcribes and the printed pages that
    /// chapter occupies. <b>The page range is per file and not per store</b>: a Chapter 4 page
    /// number pasted into a Chapter 5 entry has to fail as loudly as a Chapter 9 one would, and a
    /// single 67-86 range across the directory would accept both.
    /// </summary>
    private sealed record PlayFileFacts(
        string FileName, string ChapterLabel, int FirstPage, int LastPage, string[] CorpusFiles);

    private static readonly IReadOnlyList<PlayFileFacts> Files =
    [
        new("play_meta.json", "Ch.3 Action", 67, 72, ["ch03-action.json", "ch00-introduction.json"]),
        new("challenge.json", "Ch.3 Action", 67, 72, ["ch03-action.json", "ch00-introduction.json"]),
        new(
            "resolve.json",
            "Ch.5 Resolve and Adversity",
            CanonicalResolveRules.FirstPage,
            CanonicalResolveRules.LastPage,
            ["ch05-resolve-and-adversity.json", "ch00-introduction.json"]),
        new(
            "combat.json",
            "Ch.4 Combat",
            CanonicalCombatRules.FirstPage,
            CanonicalCombatRules.LastPage,
            ["ch04-combat.json", "ch00-introduction.json"]),
        new(
            "gritty.json",
            "Ch.4 Combat",
            CanonicalCombatRules.FirstPage,
            CanonicalCombatRules.LastPage,
            ["ch04-combat.json", "ch00-introduction.json"])
    ];

    private static PlayFileFacts FactsFor(string fileName) =>
        Files.Single(f => string.Equals(f.FileName, fileName, StringComparison.Ordinal));

    /// <summary>
    /// The same strictness <see cref="RulesFileCoverageTests"/> applies: a key no model reads is
    /// a failing test rather than data that silently deserializes into nothing.
    /// </summary>
    private static JsonSerializerOptions Strict() => new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        UnmappedMemberHandling      = JsonUnmappedMemberHandling.Disallow
    };

    // ── Test-local models ────────────────────────────────────────────────────

    private sealed record Header(
        string WhatThisIs,
        string PlacementNote,
        string NotLogic,
        string? DeliberatelyOmitted,
        IReadOnlyList<string> VerifiedFieldsClosedList,
        string SourceRef);

    private sealed record SubOneDieModel(
        int DiceRolled, IReadOnlyList<int> CountingFaces, int SuccessesWhenHit, string Otherwise);

    private sealed record AutoSuccessModel(int DicePerSuccess, bool GmMayVeto);

    private sealed record RoundingExceptionModel(
        string Name, string Direction, string WhatItGoverns, string Reference, string Note);

    private sealed record RoundingModel(
        string Direction,
        string Scope,
        IReadOnlyList<string> Examples,
        IReadOnlyList<RoundingExceptionModel> Exceptions);

    private sealed record MetaEntry(
        string Id,
        string Name,
        string Kind,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        int? DieSides,
        string? PoolFormula,
        IReadOnlyDictionary<string, int>? SuccessMap,
        SubOneDieModel? SubOneDie,
        AutoSuccessModel? AutomaticSuccesses,
        string? NetSuccessFormula,
        RoundingModel? Rounding);

    private sealed record BandModel(
        int? MinNetSuccesses, int? MaxNetSuccesses, string Outcome, bool? Embellishment);

    private sealed record ActorSelectionModel(
        string ActorIs,
        string OpponentIs,
        string WhenTwoOrMorePursueTheSameGoal,
        bool GmIsOpponentWhenUnopposed,
        string GmAcceptsPlayerInputWhenActorIsNpc);

    private sealed record ThresholdModel(string Difficulty, int ThresholdMin, int? ThresholdMax);

    /// <summary>
    /// <b>This repository's reading of the Thresholds table, kept apart from the transcription of
    /// it.</b> "The GM chooses inside this row" is not a column the book prints, and it sat in the
    /// canonical file and in the table's own rows as though it were one.
    /// </summary>
    private sealed record ThresholdsInterpretationModel(
        string WhatThisIs,
        IReadOnlyList<string> GmDiscretionDifficulties);

    private sealed record OpposedModel(string ThresholdSource, string StaticThresholdUsedWhen);

    private sealed record ConditionModifierModel(
        int MinDice, int MaxDice, string AppliedBy, string MayApplyTo,
        IReadOnlyList<string> ExamplesGiven);

    private sealed record EmbellishmentModel(
        string HeldBy, bool MustNotContradictTheNarration, bool MustNotRenderItMeaningless, string Size);

    private sealed record CompromiseModel(
        string OfferedBy, string Trade, bool RequiresAgreementOfBoth, bool OpponentMayRefuse);

    private sealed record SwingModel(
        IReadOnlyDictionary<string, int> SuccessMap,
        string SixesExplode,
        int ExplodeCostResolve,
        bool DecidedAfterTheRoll,
        bool ExplosionRecursesWhileSixesKeepComing,
        bool OtherExplodeOffersProvideNoExtraBenefit);

    private sealed record AssistModel(
        int HelperRollsAgainstThreshold,
        string HelperThresholdDifficulty,
        int NetSuccessesPerBonusDie,
        string Rounding,
        string BonusFormula,
        bool BestHelperOnly);

    private sealed record GroupActionModel(
        string Trigger,
        bool EveryoneRollsIndividually,
        int DistributeAboveNetSuccesses,
        bool AboveIsStrict,
        int MinimumNetSuccessesToDistribute,
        string DistributeTo,
        IReadOnlyList<string> ExamplesGiven);

    private sealed record ContestModel(
        string Structure,
        int TypicalExchanges,
        int ArduousExchangesMin,
        int? ArduousExchangesMax,
        string ArduousCondition,
        bool ExchangeWinnerNarratesThatExchange,
        int ExchangeWinBonusDiceNextExchange,
        bool FinalExchangeDecidesTheContest);

    private sealed record DefiningMomentModel(
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
        bool AbilityMayBeBoughtBackLater);

    private sealed record OrdinaryGamesOptionModel(
        bool OfferedAtGmOption, bool ReplacesThePermanentAbilityLoss, bool Mandatory);

    private sealed record OneShotModel(
        string Context,
        int HealthAfter,
        bool Unconscious,
        string RegainConsciousness,
        int ChallengeRollPenaltyDice,
        string PenaltyDuration,
        OrdinaryGamesOptionModel OrdinaryGamesOption);

    private sealed record JudgingModel(string Descriptor, string Difficulty, int Threshold);

    private sealed record ChallengeEntry(
        string Id,
        string Name,
        string Kind,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        IReadOnlyList<BandModel>? Bands,
        ActorSelectionModel? ActorSelection,
        IReadOnlyList<ThresholdModel>? Thresholds,
        ThresholdsInterpretationModel? Interpretation,
        string? NamingConvention,
        OpposedModel? Opposed,
        ConditionModifierModel? ConditionModifier,
        EmbellishmentModel? Embellishment,
        CompromiseModel? Compromise,
        string? SilverLiningsAndComplicationsDecidedBy,
        bool? MixablePerPlayer,
        SwingModel? CheckingYourSwing,
        AssistModel? Assist,
        GroupActionModel? GroupAction,
        ContestModel? Contest,
        DefiningMomentModel? DefiningMoment,
        OneShotModel? OneShotVariant,
        IReadOnlyList<JudgingModel>? JudgingGuideline,
        string? CalibratedFor,
        bool? WingItIsEndorsed);

    // ── Chapter 5's models ───────────────────────────────────────────────────

    private sealed record StartingResolveRowModel(int RanksBelowTraitCap, int Resolve);

    private sealed record StartingResolveModel(
        string GrantedAt,
        bool IssueIsAGameSession,
        string DependsOn,
        string MeasuredFrom,
        int AtTraitCap,
        int ResolvePerRankBelowCap,
        string Formula,
        IReadOnlyList<StartingResolveRowModel> TableRows,
        bool TableContinuesBeyondThePrintedRows);

    private sealed record ExceptionsModel(
        bool TalentsCount,
        string Criterion,
        IReadOnlyList<string> NamedPowers,
        string ExpertiseQualifier,
        bool NamedListIsQualifiedAsUsual,
        bool GmHasFinalSay);

    private sealed record MaximumRankModel(
        string AppliesWhen, IReadOnlyList<string> PowersNamed, string RankUsed);

    private sealed record CarryoverModel(
        bool CarriesOverBetweenIssues,
        bool UnspentIsLostAtIssueEnd,
        bool GmMayAllowCarryover,
        bool GmCarryoverShouldBeRare);

    private sealed record EarningOverviewModel(
        bool GmMayAwardWheneverTheySeeFit, bool ListedWaysAreExamplesNotAClosedList);

    /// <summary>
    /// <b>A reading of the page, kept apart from the transcription of it.</b> Two entries carry
    /// one, both for the same reason: the book does not say the thing, and a fact field is a claim
    /// that it does.
    /// <list type="bullet">
    ///   <item><c>resolve_earning_overview</c> — which of the six ways to earn a simulator could
    ///   apply on its own, derived from the entries' own <c>kind</c> by
    ///   <see cref="TheEarningsASimulatorCouldApplyAreExactlyTheOnesWithAStatedTrigger"/></item>
    ///   <item><c>spend_assisting_allies</c> — what an ordinary share costs, which p.84 never
    ///   prices, derived from the penalty rate it does price by
    ///   <see cref="TheParShareRateIsInferredFromThePrintedPenaltyRate"/></item>
    /// </list>
    /// Every field but <c>what_this_is</c> is optional, so an entry answers only for its own
    /// reading, and every one of them is a <see cref="DerivedPaths"/> entry with a test that
    /// <em>computes</em> it rather than a constant somebody typed.
    /// </summary>
    private sealed record InterpretationModel(
        string WhatThisIs,
        IReadOnlyList<string>? MechanisableEntryIds,
        int? InferredCostPerPointShared);

    /// <summary>
    /// One model for all six earning entries, because they are one mechanic printed six times with
    /// different triggers. Every field is optional and the walk skips nulls, so an entry answers
    /// only for what its own paragraph states — which is how <c>award_stated</c> can record that
    /// Interludes name no figure without inventing one.
    /// </summary>
    private sealed record EarningModel(
        int? AwardResolve,
        bool? AwardStated,
        string? Trigger,
        string? AvailableWhen,
        bool? MayBeOutsideCombat,
        int? LimitPerBattle,
        bool? MustFitTheSituation,
        int? SomeFlawsAwardPerIssueInstead,
        string? InterludeIs,
        string? DetailChapter,
        bool? NotAnInvitationToDerailTheGame,
        bool? GmJudged,
        IReadOnlyList<string>? ExamplesGiven,
        bool? MustBeSpentOnTheSamePage,
        bool? ThenUnconscious,
        string? UnconsciousUntil);

    private sealed record SpendingOverviewModel(
        bool GmMayExpandTheUses, bool ListedUsesAreTheBasicOnes);

    /// <summary>
    /// One model for every spend, players' and GM's alike, for the reason the page gives: "you can
    /// use Adversity to do anything players can do with Resolve". The two currencies differ in who
    /// holds them and in three exclusives, not in shape.
    /// </summary>
    private sealed record SpendModel(
        string? Currency,
        int? CostResolve,
        int? CostAdversity,
        int? CostPerPointSharedWhenUnableToAssist,
        string? PointsSharedLimit,
        bool? MustNarrateTheAssistance,
        bool? NarrationHasNoMechanicalEffect,
        IReadOnlyList<string>? UnableToAssistExamples,
        bool? FlashbackRequiredWhenUnable,
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
        string? Prevents,
        string? Duration,
        IReadOnlyList<string>? EligibleCharacters,
        IReadOnlyList<string>? ExcludedCharacters,
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
        bool? UseSparingly);

    private sealed record RerollFloorModel(
        bool SpendingShouldNeverMakeThingsWorse,
        bool KeepTheFirstRollIfTheRerollIsWorse,
        string AppliesTo);

    private sealed record AdversityPoolModel(
        int PointsPerHeroPerIssue,
        string HeldBy,
        bool CarriesOverBetweenIssues,
        bool IsMoreOfAFixedResourceThanResolve,
        bool GmMayAddWaysToEarn,
        bool ShouldNotBeAsEasyToEarnAsResolve);

    private sealed record ChallengeLevelGuidanceModel(int Level, string UsedFor);

    private sealed record ChallengeLevelModel(
        IReadOnlyList<string> AwardFactors,
        string AwardOperation,
        string AwardedAt,
        int TypicalLevelMin,
        int TypicalLevelMax,
        bool LevelThreeMayBeExceeded,
        bool OnlyAHandfulOfScenesPerStory,
        bool MayBeSavedForLaterInTheIssue,
        IReadOnlyList<ChallengeLevelGuidanceModel> LevelGuidance);

    private sealed record UnheroicActionModel(
        int AwardAdversity,
        bool AwardedImmediately,
        IReadOnlyList<string> Triggers,
        bool AlsoWhenContraryToMotivation,
        bool AppliesEvenIfCoerced,
        IReadOnlyList<string> CoercionForms);

    private sealed record ResolveEntry(
        string Id,
        string Name,
        string Kind,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        string PrintedUnder,
        string? Who,
        StartingResolveModel? StartingResolve,
        ExceptionsModel? Exceptions,
        MaximumRankModel? MaximumPossibleRank,
        CarryoverModel? Carryover,
        EarningOverviewModel? EarningOverview,
        InterpretationModel? Interpretation,
        EarningModel? Earning,
        SpendingOverviewModel? SpendingOverview,
        SpendModel? Spend,
        RerollFloorModel? RerollFloor,
        AdversityPoolModel? Adversity,
        ChallengeLevelModel? ChallengeLevel,
        UnheroicActionModel? UnheroicAction);

    // ── Chapter 4's models ───────────────────────────────────────────────────
    //
    // Generated shape, hand-checked: one record per printed block, so a key added to a play rules
    // file has somewhere to land or fails the strict reader. The blocks are named for the mechanic
    // rather than for the entry, which is why `cover`, `size` and `visibility` each carry their own
    // band row type — three tables with the same field name and three different shapes.

    private sealed record PageModel(
        string APageIs,
        int TurnsPerCharacterPerPage,
        string PageEndsWhen);

    private sealed record EdgeModel(
        string Formula,
        string ActsInOrder,
        string OptionalRandomInitiative,
        string RandomInitiativeEffectiveEdge,
        string RandomInitiativeLasts);

    private sealed record TieBreakModel(
        IReadOnlyList<string> Order,
        string StillTiedAct,
        bool SimultaneousCharactersCanKnockEachOtherOut,
        bool MinionsHaveAnEdge,
        string MinionsAct,
        string MinionAlliesAndEnemiesAct);

    private sealed record HoldingModel(
        bool MayHoldInReserve,
        string WaitingFor,
        string IfItNeverHappens,
        string OrderAmongHolders);

    private sealed record SeizeInitiativeModel(
        int CostResolve,
        string Effect,
        string Duration,
        bool SeizersGoBeforeEveryoneElse,
        string OrderAmongSeizers);

    private sealed record GmAlternativeModel(
        string InsteadOf,
        string Effect,
        string ChosenBy,
        string Rationale);

    private sealed record CombatInterpretationModel(
        string? WhatThisIs,
        string? DurationIsInheritedFrom,
        string? AverageRounds,
        string? DurationRounds,
        string? ReductionRounds);

    private sealed record ActionsModel(
        string OnYourTurn,
        string AnActionIs,
        bool AttacksAreTheCommonestAction,
        bool DefendingYourselfIsAvailable,
        string FreeActionsAllowed,
        IReadOnlyList<string> FreeActionExamples);

    private sealed record MultipleActionsModel(
        int PenaltyDicePerExtraAction,
        string AppliesTo,
        bool MustBeDeclaredBeforeAnyChallengeRoll,
        bool AppliesToDefenseRolls,
        bool AppliesToOtherChallengeRolls,
        bool SameTargetMoreThanOncePerPage,
        bool ExtraActionsBuyExtraMovement);

    private sealed record RangesRowModel(
        string Class,
        string Covers);

    private sealed record RangeRulesModel(
        bool MeasuredPrecisely,
        string InitialRangeClassSetBy,
        string CloseCombatAttacksRequire,
        string CloseCombatAttacksReach,
        string RangedAttacksReach,
        IReadOnlyList<string> ExceptionsGiven);

    private sealed record EstimatesModel(
        int CloseFeet,
        int DistantFeet,
        int ExtremeFeet,
        string StatedAs);

    private sealed record ThrowingModel(
        string OrdinaryPeopleReach,
        int TableUsedWhenMightExceeds,
        string RankFormula,
        int MinimumRank,
        bool AccuracyIsWhatIsMeasured);

    private sealed record ThrowingTableRowModel(
        int MinRank,
        int? MaxRank,
        string Range);

    private sealed record MovementModel(
        int PagesToCloseOrOpenWithinCloseRange,
        int PagesPerRangeClass,
        int PagesPerRangeClassWithATravelPower,
        int TravelPowerRankRequired,
        bool MovingPreventsActions,
        string AssumedTerrain,
        int OpenTerrainGmMayAllowRangeClassesPerPage);

    private sealed record MovementContestModel(
        string Trigger,
        string Roll,
        string OnFootAgainstATravelPowerUses,
        string WinnerGets);

    private sealed record ChaseModel(
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
        string AtFartherThanExtremeRange);

    private sealed record AttackModel(
        string Roll,
        string ThresholdSource,
        string OnMoreSuccessesThanTheTarget,
        string OnFailingTheThreshold,
        bool AccuracyAndDamageAreOneTrait,
        bool DefenseAndDamageResistanceAreOneTrait,
        string DefenderUses);

    private sealed record AttackDefenseTableRowModel(
        string Type,
        string AttackTrait,
        IReadOnlyList<string> DefenseTraits);

    private sealed record DefensesModel(
        string ActiveRepresent,
        string PassiveRepresent,
        IReadOnlyList<string> CommonActiveTraits,
        IReadOnlyList<string> CommonPassiveTraits,
        IReadOnlyList<string> ActiveUnusableWhen,
        bool ACrampedOrAwkwardPositionPreventsActiveDefenses,
        bool LosingYourNextTurnPreventsActiveDefenses,
        int DefensesUsedPerAttack,
        string DefenseChosen);

    private sealed record DamageTypesModel(
        bool LethalIsTheMoreDangerous,
        string ToughnessAgainstLethal,
        string ToughnessAgainstSubdual,
        IReadOnlyList<string> SubdualSourcesGiven,
        string PhysicalDamageDefault,
        string PsychicDamageIs,
        string PsychicDamageResistedWith);

    private sealed record CoverBandsRowModel(
        string Cover,
        int Dice);

    private sealed record CoverModel(
        string Affects,
        IReadOnlyList<CoverBandsRowModel> Bands,
        bool ACompletelyHiddenTargetCannotBeHit,
        string AttackingThroughCoverRequires,
        bool TargetMayUseTheCoversStructureAsAPassiveDefense);

    private sealed record SizeBandsRowModel(
        string AttackerRelativeSize,
        int Dice);

    private sealed record SizeModel(
        string Affects,
        IReadOnlyList<SizeBandsRowModel> Bands);

    private sealed record VisibilityBandsRowModel(
        string Visibility,
        int Dice);

    private sealed record VisibilityModel(
        string Affects,
        IReadOnlyList<VisibilityBandsRowModel> Bands,
        IReadOnlyList<string> PoorExamples,
        IReadOnlyList<string> NoneExamples,
        bool AnInvisibleOpponentCountsAsNoVisibility,
        IReadOnlyList<string> PowersThatCompensateGiven);

    private sealed record DamageModel(
        int DamagePerNetSuccess,
        string Reduces,
        int DefeatedAtHealth,
        string DefeatedMeans,
        bool DeathOnlyUnderTheGrittyCombatRules);

    private sealed record HealthModel(
        string Formula,
        bool VillainsUseTheSameFormula,
        bool FoesHalveTheResult,
        bool NpcTotalsAreSuggestions,
        bool MinionsUseHealth);

    private sealed record ReferenceModel(
        bool TranscribedHere,
        string DetailChapter,
        IReadOnlyList<string> DeferredTopics);

    private sealed record HealingModel(
        string Roll,
        string AfterAFightDifficulty,
        int AfterAFightThreshold,
        int HealthPerNetSuccess,
        string AlsoAvailableAfter,
        int FullRestHours,
        string FullRestDifficulty,
        int FullRestThreshold,
        string RemovedNpcsRecover);

    private sealed record SpecialEffectModel(
        IReadOnlyList<string> SourcesGiven,
        string DurationFormula,
        string ExpiresAt,
        bool DurationStacksByAttackingTheSameTargetAgain,
        string DefeatedWhenTheDurationReaches,
        string DefeatByEffectLasts);

    private sealed record BreakingFreeModel(
        string AvailableWhen,
        string TakenOn,
        string Roll,
        IReadOnlyList<string> RollExamplesGiven,
        string ThresholdSource,
        string DurationReducedBy,
        int FreeWhenTheDurationReaches,
        bool MayActOnTheSamePageWhenFreed);

    private sealed record KeepingHoldModel(
        string Trigger,
        int CostResolve,
        string ExtendsTo,
        bool MayBeRepeatedSceneAfterScene);

    private sealed record InstantRecoveryModel(
        int CostResolve,
        string TakenOn,
        bool AfterADamagingDefeatRegainsConsciousness,
        int AfterADamagingDefeatRestoresHealth,
        bool AlsoFreesYouFromASpecialEffect,
        bool RequiresBeingDefeatedToFreeYourselfFromAnEffect,
        int LimitPerScene);

    private sealed record GrapplingModel(
        string AGrabIs,
        string AHoldIs,
        string AnEscapeIs,
        string Roll,
        string ThresholdSource,
        bool OpponentMayUseAnActiveDefenseInsteadWhenNotAlreadyGrappling,
        bool InflictingOrdinaryDamageInCloseCombatNeedsNoSpecialRules);

    private sealed record GrapplingTableRowModel(
        int? MinNetSuccesses,
        int? MaxNetSuccesses,
        string Grab,
        string Hold,
        string Escape);

    private sealed record GrabModel(
        string PartialMeans,
        bool PartialBlocksActiveDefensesAgainstAnyoneElse,
        string PartialResolvedBy,
        bool MayExitByLettingGoOfTheObject,
        string FullMeans,
        bool FullAllowsUsingOrTossingItTheSamePage,
        bool FullSuffersTheMultipleActionPenalty,
        bool FullIsInEffectAFreeAction);

    private sealed record HoldModel(
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
        string AdjudicatedCaseByCaseBy);

    private sealed record EscapeModel(
        string PartialOutOfAPartialHold,
        string PartialOutOfAFullHold,
        string Full,
        bool MayAlsoExitGrapplingCompletely);

    private sealed record CombatStuntModel(
        string WhatItIs,
        string WorksLike,
        string TraitsUsedAreChosenBy,
        string EffectDescribedBy,
        string Duration,
        bool DelayingYourNextActionExtendsIt,
        bool PenaltiesFromMultipleStuntsAreCumulative);

    private sealed record SampleStuntsRowModel(
        string Name,
        IReadOnlyList<string> AttackTraits,
        IReadOnlyList<string> DefenseTraits);

    private sealed record CombatStuntBandsRowModel(
        int? MinNetSuccesses,
        int? MaxNetSuccesses,
        IReadOnlyList<string> SampleEffects);

    private sealed record MinionsModel(
        string OnlyCharacteristic,
        bool ActInGroupsRatherThanAsIndividuals);

    private sealed record ThreatRanksRowModel(
        string Category,
        int MinThreat,
        int? MaxThreat);

    private sealed record AttackingMinionsModel(
        bool MinionsHaveHealth,
        int MinionsDefeatedPerNetSuccess,
        int MinionsDefeatedPerNetSuccessWithAnAreaAttack,
        string CappedBy,
        int MaximumMinionsPerNetSuccess,
        bool EffectsThatDoubleTheRateDoNotStack,
        string OnADamagingAttack,
        string OnASpecialEffect);

    private sealed record MinionsAttackingModel(
        string AGroupActsLike,
        bool AGroupMaySplitToAttackMultipleEnemies,
        int TargetsPerGroupPerPage,
        int AttackRollsPerGroupPerPage,
        int DefenseRollsOpposingIt,
        string TheGroupBonusAppliesTo,
        string TheGroupBonusDoesNotApplyTo,
        int MaximumAttackingOneTargetInCloseCombat,
        int MaximumAttackingOneTargetAtRange);

    private sealed record MinionGroupAttackRowModel(
        int MinMinions,
        int MaxMinions,
        int BonusDice);

    private sealed record AmbushModel(
        string Roll,
        string DeceptionOrSeductionRoll,
        string ThresholdSource,
        string OnSuccess,
        bool ASurprisedTargetCanAct,
        bool ASurprisedTargetCanUseActiveDefenses,
        string SurpriseLasts,
        bool EmbellishmentRightsAllowPartialSurprise,
        string PartialSurpriseKeeps,
        string OnFailure,
        bool MultipleAmbushersMayRollAsAGroup,
        bool EveryTargetRollsTheirOwnPerception,
        bool MinionsRollPerceptionInGroups);

    private sealed record AreaAttackModel(
        string Targets,
        int AttackRolls,
        string DefenseRolls,
        IReadOnlyList<string> AnActiveDefenseMustEither,
        IReadOnlyList<string> ExamplesGiven);

    private sealed record ChargeModel(
        string WhatItIs,
        IReadOnlyList<string> AttackTraits,
        bool SwimmingMayBeUsedOnlyUnderwater,
        int AttackBonusDice,
        string OwnActiveDefenseRanks,
        string PenaltyLasts,
        string IfTheTargetUsesAPassiveDefense,
        string SelfDamageReducedBy);

    private sealed record ClobberingModel(
        string WhatItIs,
        int AttackRolls,
        int AttackPenaltyDice,
        string DefenseRolls,
        IReadOnlyList<string> Targets,
        string ThePrimaryTargetIs,
        bool StopsIfThePrimaryDefendsActivelyAndTakesNoDamage);

    private sealed record DefendingOthersModel(
        string Cost,
        string Range,
        string Effect,
        bool MayUseAnActiveOrAPassiveDefense,
        string AnActiveDefenseLeavesTheDamageOn,
        bool TheProtectedCharacterMayStillUseAPassiveDefense,
        string APassiveDefenseLeavesTheDamageOn);

    private sealed record AllOutAttackModel(
        int AttackBonusDice,
        string DefenseRanks,
        bool AffectsActiveDefenses,
        bool AffectsPassiveDefenses,
        string Lasts,
        string OpponentsWhoCouldNotPenetrateYourPassiveDefense);

    private sealed record AllOutDefenseModel(
        int DefenseBonusDice,
        string Lasts,
        bool PreventsAttacking,
        bool PreventsOtherActions,
        bool AllowsMovement,
        bool AllowsFreeActions,
        bool ATravelPowerOrSpeedMayBeUsedAsAnActiveDefense);

    private sealed record KnockbackModel(
        string RequiresDamageType,
        int MinimumDamage,
        int CostResolve,
        string TargetIsThrownAsIfByAMightRankEqualTo,
        bool TargetFallsProne,
        bool TargetLosesTheirNextTurnToAct,
        string DamageOnStrikingASolidObject,
        bool TheObjectMustBeTougherThanTheTarget,
        string APassiveDefenseAboveTheObjectsStructure);

    private sealed record LuringModel(
        string WhatItIs,
        IReadOnlyList<string> AppliesToAttackTypes,
        string DeclaredBefore,
        bool RequiresAnActiveDefense,
        int DefenseMustExceedTheAttackRollBy,
        int CostResolve,
        string RedirectsTo,
        bool MayRedirectOntoAPerson,
        string RedirectingOntoAPersonCosts,
        bool TheNewTargetMakesTheirOwnDefenseRoll);

    private sealed record TeamAttackModel(
        string WhatItIs,
        string ParticipantsActAt,
        bool AllParticipantsMustTargetTheSameEnemy,
        int AttackBonusDice,
        int CostResolveToMakeSixesExplode,
        bool ExplosionRecursesWhileSixesKeepComing,
        int LimitPerTargetPerBattle,
        string TheLimitMayBeLiftedBy,
        bool UseSparingly);

    private sealed record OverviewModel(
        string DefaultCombatIs,
        bool RulesAreOptional,
        bool AnySubsetMayBeUsed,
        bool ReviewBeforeAdopting,
        bool ARetconOrDoOverIsAllowedIfARuleIsDroppedAfterPlay);

    private sealed record ActiveDefensePenaltyModel(
        bool ActiveDefensesAreMinorActions,
        int CumulativePenaltyDicePerExtraActiveDefense,
        bool FirstActiveDefenseOnAPageIsUnpenalised,
        string CountedPer,
        bool AffectsPassiveDefenses);

    private sealed record CloseRangePenaltyModel(
        int PenaltyDiceToActiveDefense,
        string AppliesAgainst,
        string AppliesOnlyToAttacksUsableAt,
        string IgnoredFor);

    private sealed record TheDropModel(
        string HeldBy,
        string HeldAgainst,
        string Effect,
        string AlsoHeldBy,
        IReadOnlyList<string> ExamplesGiven,
        string FinalSay);

    private sealed record FatalDamageModel(
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
        bool InstantRecoveryRequiresBeingStable);

    private sealed record FriendlyFireModel(
        int PenaltyDice,
        string AppliesWhen,
        int SecondAttackTriggeredAtNetSuccesses,
        string SecondAttackIsAgainst,
        int SecondAttackPenaltyDice,
        string SecondTargetSelectedBy,
        string SecondTargetSelected);

    private sealed record HardTargetsModel(
        string AppliesTo,
        string PassiveDefenseRank,
        int PenaltyDiceToNegateIt,
        string NegationAvailableAgainst,
        string RecommendedProForVehicleScaleWeapons,
        string RecommendedProForThePhysicalAttacksOfPowerfulSuperhumanCharacters);

    private sealed record GearLimitModel(
        string WhatItIs,
        int DefaultRank,
        IReadOnlyList<int> RaisedOptions,
        bool RaisedOptionsAreOpenEnded,
        string WorkedExampleWeapon,
        int WorkedExampleWeaponBonusDice,
        int WorkedExampleMaximumEffectiveRankAtTheDefaultLimit,
        string DetailChapter);

    private sealed record SlowHealingBandsRowModel(
        int? MinToughness,
        int? MaxToughness,
        int HealthPerDay,
        int OnePointEveryHours);

    private sealed record SlowHealingModel(
        IReadOnlyList<SlowHealingBandsRowModel> Bands,
        bool HealingAfterEachBattle,
        bool HealingOnRegainingConsciousnessAfterADefeat,
        bool YouMayBeConsciousAtZeroOrNegativeHealth,
        bool InThatConditionAnyDamageAtAllDefeatsYou,
        bool StabilizationAvailableAsOftenAsNecessary,
        string MedicineHealingLimit,
        int MedicineHealthPerNetSuccesses,
        int MedicineNetSuccessesPerPoint);

    private sealed record ToughMinionsModel(
        int NetSuccessesPerMinionDefeated,
        bool FullNetSuccessesRequired,
        string Rounding,
        bool RoundingIsANamedUniqueException,
        int WorkedExampleNetSuccesses,
        int WorkedExampleMinionsDefeated,
        int AreaAttackMinionsPerNetSuccess,
        int AreaAttackRateItReplaces,
        string AlternativeOffered);

    private sealed record WoundPenaltiesModel(
        int AtOrBelowHalfFullHealthPenaltyDice,
        int AtOrBelowZeroHealthPenaltyDice,
        string ZeroOrLessIsReachableOnlyWith,
        string AppliesTo,
        int CostResolveToIgnore,
        int PagesIgnoredPerResolvePoint);

    private sealed record CombatEntry(
        string Id,
        string Name,
        string Kind,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        PageModel? Page,
        EdgeModel? Edge,
        TieBreakModel? TieBreak,
        HoldingModel? Holding,
        SeizeInitiativeModel? SeizeInitiative,
        GmAlternativeModel? GmAlternative,
        CombatInterpretationModel? Interpretation,
        ActionsModel? Actions,
        MultipleActionsModel? MultipleActions,
        IReadOnlyList<RangesRowModel>? Ranges,
        RangeRulesModel? RangeRules,
        EstimatesModel? Estimates,
        ThrowingModel? Throwing,
        IReadOnlyList<ThrowingTableRowModel>? ThrowingTable,
        MovementModel? Movement,
        MovementContestModel? MovementContest,
        ChaseModel? Chase,
        AttackModel? Attack,
        IReadOnlyList<AttackDefenseTableRowModel>? AttackDefenseTable,
        DefensesModel? Defenses,
        DamageTypesModel? DamageTypes,
        CoverModel? Cover,
        SizeModel? Size,
        VisibilityModel? Visibility,
        DamageModel? Damage,
        HealthModel? Health,
        ReferenceModel? Reference,
        HealingModel? Healing,
        SpecialEffectModel? SpecialEffect,
        BreakingFreeModel? BreakingFree,
        KeepingHoldModel? KeepingHold,
        InstantRecoveryModel? InstantRecovery,
        GrapplingModel? Grappling,
        IReadOnlyList<GrapplingTableRowModel>? GrapplingTable,
        GrabModel? Grab,
        HoldModel? Hold,
        EscapeModel? Escape,
        CombatStuntModel? CombatStunt,
        IReadOnlyList<SampleStuntsRowModel>? SampleStunts,
        IReadOnlyList<CombatStuntBandsRowModel>? CombatStuntBands,
        MinionsModel? Minions,
        IReadOnlyList<ThreatRanksRowModel>? ThreatRanks,
        AttackingMinionsModel? AttackingMinions,
        MinionsAttackingModel? MinionsAttacking,
        IReadOnlyList<MinionGroupAttackRowModel>? MinionGroupAttack,
        AmbushModel? Ambush,
        AreaAttackModel? AreaAttack,
        ChargeModel? Charge,
        ClobberingModel? Clobbering,
        DefendingOthersModel? DefendingOthers,
        AllOutAttackModel? AllOutAttack,
        AllOutDefenseModel? AllOutDefense,
        KnockbackModel? Knockback,
        LuringModel? Luring,
        TeamAttackModel? TeamAttack);

    private sealed record GrittyEntry(
        string Id,
        string Name,
        string Kind,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        OverviewModel? Overview,
        ActiveDefensePenaltyModel? ActiveDefensePenalty,
        CloseRangePenaltyModel? CloseRangePenalty,
        TheDropModel? TheDrop,
        FatalDamageModel? FatalDamage,
        FriendlyFireModel? FriendlyFire,
        HardTargetsModel? HardTargets,
        GearLimitModel? GearLimit,
        SlowHealingModel? SlowHealing,
        ToughMinionsModel? ToughMinions,
        WoundPenaltiesModel? WoundPenalties);

    private sealed record PlayFile<TEntry>(Header Header, IReadOnlyList<TEntry> Entries);

    // ── Loading ──────────────────────────────────────────────────────────────

    private static PlayFile<MetaEntry> Meta() => Load<MetaEntry>("play_meta.json");
    private static PlayFile<ChallengeEntry> Challenge() => Load<ChallengeEntry>("challenge.json");
    private static PlayFile<ResolveEntry> Resolve() => Load<ResolveEntry>("resolve.json");

    private static PlayFile<TEntry> Load<TEntry>(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));
        return JsonSerializer.Deserialize<PlayFile<TEntry>>(json, Strict())
               ?? throw new InvalidOperationException($"{fileName} deserialized to null.");
    }

    private static PlayFile<CombatEntry> Combat() => Load<CombatEntry>("combat.json");
    private static PlayFile<GrittyEntry> Gritty() => Load<GrittyEntry>("gritty.json");

    private static MetaEntry MetaEntryById(string id) => Meta().Entries.Single(e => e.Id == id);
    private static CombatEntry CombatEntryById(string id) => Combat().Entries.Single(e => e.Id == id);
    private static GrittyEntry GrittyEntryById(string id) => Gritty().Entries.Single(e => e.Id == id);
    private static ChallengeEntry ChallengeEntryById(string id) => Challenge().Entries.Single(e => e.Id == id);
    private static ResolveEntry ResolveEntryById(string id) => Resolve().Entries.Single(e => e.Id == id);

    // ── The dice model, play_meta.json ───────────────────────────────────────

    [Fact]
    public void TheSuccessMapIsTheOnePrintedOnPage67()
    {
        var entry = MetaEntryById("success_map");
        var map = entry.SuccessMap;

        Assert.NotNull(map);

        // The map is a dictionary, which absorbs any key at all — so the face set is asserted
        // before the values are. Without this a face could go missing and every lookup below
        // would still find what it looked for.
        Assert.Equal(
            CanonicalChallengeRules.SuccessMap.Keys.Order(),
            map.Keys.Select(k => int.Parse(k, CultureInfo.InvariantCulture)).Order());

        foreach (var (face, successes) in CanonicalChallengeRules.SuccessMap)
        {
            Assert.Equal(successes, map[face.ToString(CultureInfo.InvariantCulture)]);
        }

        Assert.Contains($"p.{CanonicalChallengeRules.DiceModelPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    [Fact]
    public void APoolIsThatManySixSidedDice()
    {
        var entry = MetaEntryById("dice_pool");

        Assert.Equal(6, entry.DieSides);
        Assert.NotNull(entry.PoolFormula);
    }

    [Fact]
    public void BelowOneDieYouStillRollOneAndOnlyASixCounts()
    {
        var floor = MetaEntryById("sub_one_die_floor").SubOneDie;

        Assert.NotNull(floor);
        Assert.Equal(CanonicalChallengeRules.SubOneDieDiceRolled, floor.DiceRolled);
        Assert.Equal([CanonicalChallengeRules.SubOneDieCountingFace], floor.CountingFaces);
        Assert.Equal(CanonicalChallengeRules.SubOneDieSuccessesWhenHit, floor.SuccessesWhenHit);
    }

    [Fact]
    public void AutomaticSuccessesAreOnePerTwoDiceNotRolledAndTheGmMayVeto()
    {
        var auto = MetaEntryById("automatic_successes").AutomaticSuccesses;

        Assert.NotNull(auto);
        Assert.Equal(CanonicalChallengeRules.DiceNotRolledPerAutomaticSuccess, auto.DicePerSuccess);
        Assert.Equal(CanonicalChallengeRules.AutomaticSuccessesCanBeVetoed, auto.GmMayVeto);
    }

    [Fact]
    public void NetSuccessesAreSuccessesLessTheThreshold()
    {
        var formula = MetaEntryById("net_successes").NetSuccessFormula;

        Assert.NotNull(formula);
        Assert.Contains("successes - threshold", formula, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rounding rule is the Introduction's, not Chapter 3's, and its one printed exception
    /// belongs to Chapter 4. Both facts are recorded here so a simulator built on this data does
    /// not have to rediscover that there is exactly one place the convention reverses.
    /// </summary>
    [Fact]
    public void HalfRoundsUpEverywhereAndNamesItsOnePrintedException()
    {
        var entry = MetaEntryById("half_rounds_up");
        var rounding = entry.Rounding;

        Assert.NotNull(rounding);
        Assert.Equal(CanonicalChallengeRules.HalfRoundingDirection, rounding.Direction);
        Assert.Contains($"p.{CanonicalChallengeRules.HalfRulePage}", entry.SourceRef, StringComparison.Ordinal);

        var exception = Assert.Single(rounding.Exceptions);
        Assert.Equal(CanonicalChallengeRules.HalfRuleExceptionName, exception.Name);
        Assert.Equal(CanonicalChallengeRules.HalfRuleExceptionDirection, exception.Direction);
        Assert.Contains(
            $"p.{CanonicalChallengeRules.HalfRuleExceptionPage}",
            exception.Reference,
            StringComparison.Ordinal);
    }

    // ── The bands, challenge.json ────────────────────────────────────────────

    [Fact]
    public void TheNarrativeControlBandsAreTheOnesPrintedOnPage67()
    {
        var entry = ChallengeEntryById("narrative_control");

        AssertBands(CanonicalChallengeRules.NarrativeControl, entry.Bands);
        Assert.Contains($"p.{CanonicalChallengeRules.NarrativeControlPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTraditionalResultsBandsAreTheOnesPrintedOnPage69()
    {
        var entry = ChallengeEntryById("traditional_results");

        AssertBands(CanonicalChallengeRules.TraditionalResults, entry.Bands);
        Assert.Contains($"p.{CanonicalChallengeRules.TraditionalResultsPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    private static void AssertBands(
        IReadOnlyList<CanonicalChallengeRules.Band> expected, IReadOnlyList<BandModel>? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Min, actual[i].MinNetSuccesses);
            Assert.Equal(expected[i].Max, actual[i].MaxNetSuccesses);
            Assert.Equal(expected[i].Outcome, actual[i].Outcome);
            Assert.Equal(expected[i].Embellishment, actual[i].Embellishment);
        }

        // A band table with a hole or an overlap resolves wrongly rather than loudly, so every
        // net-success figure the tables can see is required to land in exactly one row.
        for (var net = -10; net <= 10; net++) BandFor(actual, net);
    }

    /// <summary>
    /// <b>Exactly one band must match, not the first one that does.</b> A gap between two bands
    /// and an overlap between them are both silent faults — the first makes an outcome
    /// unreachable, the second makes it arbitrary — and <c>First</c> would hide both.
    ///
    /// <para>Written out rather than left to <c>Single</c> so the failure says which figure fell
    /// through and how many rows claimed it. A bare LINQ "sequence contains more than one matching
    /// element" names neither, and a boundary bug is exactly the case where you need both.</para>
    /// </summary>
    private static BandModel BandFor(IReadOnlyList<BandModel> bands, int netSuccesses)
    {
        var matching = bands
            .Where(b => (b.MinNetSuccesses is null || netSuccesses >= b.MinNetSuccesses)
                        && (b.MaxNetSuccesses is null || netSuccesses <= b.MaxNetSuccesses))
            .ToList();

        Assert.True(
            matching.Count == 1,
            $"{netSuccesses} net successes matched {matching.Count} bands, not 1. "
            + (matching.Count == 0
                ? "The table has a hole, so that outcome cannot be reached at all."
                : "The table overlaps, so which outcome applies is whichever row is read first."));

        return matching[0];
    }

    // ── The rest of Chapter 3 ────────────────────────────────────────────────

    [Fact]
    public void TheNineThresholdsAreTheOnesPrintedOnPage67()
    {
        var entry = ChallengeEntryById("thresholds");
        var actual = entry.Thresholds;

        Assert.NotNull(actual);
        Assert.Equal(CanonicalChallengeRules.Thresholds.Count, actual.Count);

        for (var i = 0; i < CanonicalChallengeRules.Thresholds.Count; i++)
        {
            var expected = CanonicalChallengeRules.Thresholds[i];

            Assert.Equal(expected.Difficulty, actual[i].Difficulty);
            Assert.Equal(expected.Min, actual[i].ThresholdMin);
            Assert.Equal(expected.Max, actual[i].ThresholdMax);
        }

        Assert.Contains($"p.{CanonicalChallengeRules.ThresholdsPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>"The GM chooses inside this row" is ours, and it is asserted from the printed rows rather
    /// than from a hand-typed list.</b> The book prints Difficulty and Threshold and nothing else;
    /// the discretion flag used to sit in the table's own rows and in the canonical transcription,
    /// where it read as a third printed column. It is now one labelled <c>interpretation</c> object
    /// beside the table, and what makes it true is derived here: a row leaves the GM a choice
    /// exactly when its printed threshold is a range rather than a single number — "Superhuman 6 to
    /// 8", "Legendary 9 to 11", "Godlike 12 or more".
    ///
    /// <para>Deriving it is the point. A count of three, or a second hand-typed list, would agree
    /// with the table by coincidence and keep agreeing after somebody widened a row.</para>
    /// </summary>
    [Fact]
    public void TheRowsLeftToGmDiscretionAreExactlyTheOnesPrintedAsARange()
    {
        var entry = ChallengeEntryById("thresholds");
        var rows = entry.Thresholds;
        var interpretation = entry.Interpretation;

        Assert.NotNull(rows);
        Assert.NotNull(interpretation);

        var ranged = rows
            .Where(r => r.ThresholdMax is null || r.ThresholdMax != r.ThresholdMin)
            .Select(r => r.Difficulty)
            .ToList();

        // Positive control: the derivation has to find something, or an empty list would match an
        // empty claim and this test would assert nothing at all.
        Assert.NotEmpty(ranged);

        Assert.Equal(ranged, interpretation.GmDiscretionDifficulties);

        // And the flat rows really are flat, so "a range" is a distinction the table can make.
        Assert.Equal(
            rows.Count - ranged.Count,
            rows.Count(r => r.ThresholdMax is not null && r.ThresholdMax == r.ThresholdMin));
    }

    [Fact]
    public void AnOpponentsSuccessesAreTheThreshold()
    {
        var opposed = ChallengeEntryById("opposed_threshold").Opposed;

        Assert.NotNull(opposed);
        Assert.Contains("successes", opposed.ThresholdSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TheConditionModifierRunsFromMinusFourToPlusFourDice()
    {
        var modifier = ChallengeEntryById("condition_modifier").ConditionModifier;

        Assert.NotNull(modifier);
        Assert.Equal(CanonicalChallengeRules.ConditionModifierMinDice, modifier.MinDice);
        Assert.Equal(CanonicalChallengeRules.ConditionModifierMaxDice, modifier.MaxDice);
    }

    [Fact]
    public void CheckingYourSwingFlattensTheSixAndChargesOneResolveToExplodeIt()
    {
        var entry = ChallengeEntryById("checking_your_swing");
        var swing = entry.CheckingYourSwing;

        Assert.Equal("table_setting", entry.Kind);
        Assert.NotNull(swing);

        Assert.Equal(
            CanonicalChallengeRules.CheckingYourSwingSuccessMap.Keys.Order(),
            swing.SuccessMap.Keys.Select(k => int.Parse(k, CultureInfo.InvariantCulture)).Order());

        foreach (var (face, successes) in CanonicalChallengeRules.CheckingYourSwingSuccessMap)
        {
            Assert.Equal(successes, swing.SuccessMap[face.ToString(CultureInfo.InvariantCulture)]);
        }

        Assert.Equal(CanonicalChallengeRules.CheckingYourSwingExplodeCostResolve, swing.ExplodeCostResolve);
        Assert.True(swing.DecidedAfterTheRoll);
        Assert.True(swing.OtherExplodeOffersProvideNoExtraBenefit);
        Assert.Contains($"p.{CanonicalChallengeRules.CheckingYourSwingPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistingIsOneBonusDiePerTwoNetSuccessesAgainstHardTwoAndOnlyTheBestHelperCounts()
    {
        var assist = ChallengeEntryById("assisting").Assist;

        Assert.NotNull(assist);
        Assert.Equal(CanonicalChallengeRules.AssistHelperThreshold, assist.HelperRollsAgainstThreshold);
        Assert.Equal(CanonicalChallengeRules.AssistHelperDifficulty, assist.HelperThresholdDifficulty);
        Assert.Equal(CanonicalChallengeRules.AssistNetSuccessesPerBonusDie, assist.NetSuccessesPerBonusDie);
        Assert.Equal(CanonicalChallengeRules.AssistBestHelperOnly, assist.BestHelperOnly);

        // "rounding up as always" — the Glossary rule, restated here because getting it wrong
        // silently halves what a helper is worth.
        Assert.Equal(CanonicalChallengeRules.HalfRoundingDirection, assist.Rounding);

        // The helper's threshold has to be the Hard row of the Thresholds table, not a number
        // that merely happens to be 2.
        var hard = CanonicalChallengeRules.Thresholds.Single(t => t.Difficulty == assist.HelperThresholdDifficulty);
        Assert.Equal(hard.Min, assist.HelperRollsAgainstThreshold);
    }

    /// <summary>
    /// <b>The bound is strict, and a bare 3 cannot say so.</b> The page reads "characters who earn
    /// more than 3 net successes can distribute these extra net successes" — so 3 distributes
    /// nothing and 4 is the first figure that does. Recording the pivot alone left a simulator free
    /// to read it as "3 or more", which hands out a share the book does not.
    ///
    /// <para>The entry carries the inequality in three pieces and this checks they agree, so the
    /// derived minimum cannot drift away from the pivot it is derived from. What "extra" means once
    /// the bound is passed is a separate question the book genuinely does not answer, and it stays
    /// in <c>ambiguity</c>.</para>
    /// </summary>
    [Fact]
    public void AGroupActionDistributesOnlyWhatIsEarnedStrictlyAboveThreeNetSuccesses()
    {
        var group = ChallengeEntryById("group_action").GroupAction;

        Assert.NotNull(group);
        Assert.Equal(
            CanonicalChallengeRules.GroupActionDistributeAboveNetSuccesses,
            group.DistributeAboveNetSuccesses);
        Assert.Equal(CanonicalChallengeRules.GroupActionAboveIsStrict, group.AboveIsStrict);
        Assert.Equal(
            CanonicalChallengeRules.GroupActionMinimumNetSuccessesToDistribute,
            group.MinimumNetSuccessesToDistribute);
        Assert.True(group.EveryoneRollsIndividually);

        // The three fields have to describe one rule: the minimum is the pivot plus one exactly
        // when the bound is strict. Without this the file could say "above 3, strictly, minimum 3".
        Assert.Equal(
            group.DistributeAboveNetSuccesses + (group.AboveIsStrict ? 1 : 0),
            group.MinimumNetSuccessesToDistribute);
    }

    /// <summary>
    /// <b>"6 or more" is a floor, and the data models it as one.</b> The page reads "Most contests
    /// should involve 3 exchanges, but especially arduous ones can have 6 or more" — so an arduous
    /// contest has a minimum and no printed ceiling, exactly the shape the Thresholds table already
    /// uses for Godlike's "12 or more". A bare 6 asserts a cap the book never prints.
    /// </summary>
    [Fact]
    public void AContestIsUsuallyThreeExchangesAndAnArduousOneIsSixOrMore()
    {
        var contest = ChallengeEntryById("contests").Contest;

        Assert.NotNull(contest);
        Assert.Equal(CanonicalChallengeRules.ContestTypicalExchanges, contest.TypicalExchanges);
        Assert.Equal(CanonicalChallengeRules.ContestArduousExchangesMin, contest.ArduousExchangesMin);
        Assert.Equal(CanonicalChallengeRules.ContestArduousExchangesMax, contest.ArduousExchangesMax);
        Assert.Equal(CanonicalChallengeRules.ContestExchangeWinBonusDice, contest.ExchangeWinBonusDiceNextExchange);
        Assert.True(contest.FinalExchangeDecidesTheContest);

        // Same open-ended shape as the Thresholds table's top row, and asserted against it rather
        // than against a second hand-typed null, so the two cannot drift into different models of
        // "or more".
        var godlike = CanonicalChallengeRules.Thresholds[^1];
        Assert.Null(godlike.Max);
        Assert.Null(contest.ArduousExchangesMax);
    }

    [Fact]
    public void ADefiningMomentTriplesWhatAResolvePointBuysAndCostsARankAfterwards()
    {
        var moment = ChallengeEntryById("defining_moment").DefiningMoment;

        Assert.NotNull(moment);
        Assert.True(moment.SixesExplode);
        Assert.Equal(CanonicalChallengeRules.SuccessMap[6], moment.SixStillWorthSuccesses);
        Assert.Equal(CanonicalChallengeRules.DefiningMomentDicePerResolveSpent, moment.DicePerResolveSpent);
        Assert.Equal(CanonicalChallengeRules.OrdinaryDicePerResolveSpent, moment.OrdinaryDicePerResolveSpent);
        Assert.Equal(CanonicalChallengeRules.DefiningMomentLimitPerStory, moment.LimitPerStory);
        Assert.Equal(CanonicalChallengeRules.DefiningMomentLimitPerScene, moment.LimitPerScenePerGroup);
        Assert.Equal(
            CanonicalChallengeRules.DefiningMomentPermanentAbilityLossDice,
            moment.AftermathPermanentAbilityLossDice);
        Assert.Equal(CanonicalChallengeRules.DefiningMomentPhysicalAbilities, moment.PhysicalTaskReducesOneOf);
        Assert.Equal(CanonicalChallengeRules.DefiningMomentMentalAbilities, moment.MentalTaskReducesOneOf);
    }

    /// <summary>
    /// The six Abilities a Defining Moment can cost you are the six the engine already knows,
    /// spelled the same way. A play rule naming an Ability the rules data does not have is a
    /// rule nothing could ever apply.
    /// </summary>
    [Fact]
    public void TheAbilitiesADefiningMomentCanReduceAreTheSixInTheRulesData()
    {
        var moment = ChallengeEntryById("defining_moment").DefiningMoment;

        Assert.NotNull(moment);

        var rules = RulesRepositoryAbilityIds();
        var named = moment.PhysicalTaskReducesOneOf.Concat(moment.MentalTaskReducesOneOf).Order().ToList();

        Assert.Equal(rules, named);
    }

    private static List<string> RulesRepositoryAbilityIds() =>
        ["agility", "intellect", "might", "perception", "toughness", "willpower"];

    /// <summary>
    /// <b>The name says what the body asserts, and no more.</b> It used to say the variant "trades
    /// the rank loss for" the penalty — a replacement the page does not state for one-shots, and
    /// which the body never checked. The one-shot paragraph opens "Defining Moments are even more
    /// debilitating in one-shot games", which reads additively; the entry's <c>ambiguity</c> now
    /// carries both readings and no fact field picks one.
    /// </summary>
    [Fact]
    public void TheOneShotDefiningMomentDropsYouToZeroHealthAndTwoDiceForTheStory()
    {
        var oneShot = ChallengeEntryById("defining_moment_one_shot").OneShotVariant;

        Assert.NotNull(oneShot);
        Assert.Equal(CanonicalChallengeRules.OneShotHealthAfter, oneShot.HealthAfter);
        Assert.Equal(CanonicalChallengeRules.OneShotChallengeRollPenaltyDice, oneShot.ChallengeRollPenaltyDice);
        Assert.True(oneShot.Unconscious);
    }

    /// <summary>
    /// <b>The one "instead of" the page prints is about ordinary games, and it is optional.</b>
    /// "GMs may let Heroes in ordinary games choose this option instead of reducing one of their
    /// Abilities by 1d, but that's entirely optional." All three halves of that sentence are
    /// recorded — it is offered, it replaces, and it is not compulsory — because dropping the last
    /// one turns a GM's option into a rule of the game.
    /// </summary>
    [Fact]
    public void TheOrdinaryGamesOptionIsAReplacementTheGmMayOfferAndNeedNot()
    {
        var oneShot = ChallengeEntryById("defining_moment_one_shot").OneShotVariant;

        Assert.NotNull(oneShot);

        var option = oneShot.OrdinaryGamesOption;

        Assert.Equal(
            CanonicalChallengeRules.OneShotOptionOfferedInOrdinaryGamesAtGmOption,
            option.OfferedAtGmOption);
        Assert.Equal(
            CanonicalChallengeRules.OneShotOptionInOrdinaryGamesReplacesTheAbilityLoss,
            option.ReplacesThePermanentAbilityLoss);
        Assert.Equal(
            CanonicalChallengeRules.OneShotOptionInOrdinaryGamesIsMandatory,
            option.Mandatory);

        // And the one-shot case itself states no such replacement, so nothing in the entry may
        // claim one. This is the field the review found asserting an ambiguity as fact.
        var ambiguity = ChallengeEntryById("defining_moment_one_shot").Ambiguity;
        Assert.NotNull(ambiguity);
        Assert.Contains("even more debilitating", ambiguity, StringComparison.Ordinal);
    }

    [Fact]
    public void JudgingThresholdsIsHardTwoBrutalFourSuperhumanSix()
    {
        var entry = ChallengeEntryById("judging_thresholds");
        var guideline = entry.JudgingGuideline;

        Assert.NotNull(guideline);
        Assert.Equal(CanonicalChallengeRules.JudgingThresholds.Count, guideline.Count);

        for (var i = 0; i < guideline.Count; i++)
        {
            var expected = CanonicalChallengeRules.JudgingThresholds[i];

            Assert.Equal(expected.Descriptor, guideline[i].Descriptor);
            Assert.Equal(expected.Difficulty, guideline[i].Difficulty);
            Assert.Equal(expected.ThresholdValue, guideline[i].Threshold);

            // Each guideline row has to be a row of the Thresholds table, at that row's floor.
            var row = CanonicalChallengeRules.Thresholds.Single(t => t.Difficulty == guideline[i].Difficulty);
            Assert.Equal(row.Min, guideline[i].Threshold);
        }

        Assert.Contains($"p.{CanonicalChallengeRules.JudgingThresholdsPage}", entry.SourceRef, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbellishmentsAndCompromisesAreBothRecorded()
    {
        var embellishment = ChallengeEntryById("embellishment").Embellishment;
        var compromise = ChallengeEntryById("compromise").Compromise;

        Assert.NotNull(embellishment);
        Assert.True(embellishment.MustNotContradictTheNarration);
        Assert.True(embellishment.MustNotRenderItMeaningless);

        Assert.NotNull(compromise);
        Assert.True(compromise.RequiresAgreementOfBoth);
        Assert.True(compromise.OpponentMayRefuse);
    }

    // ── The fixture from the book ────────────────────────────────────────────

    /// <summary>
    /// <b>The arm-wrestling example printed under the Challenge Rolls table (p.67), resolved
    /// against the shipped JSON.</b>
    ///
    /// <para>Everything else in this file compares one transcription to another, and two
    /// transcriptions can agree and both be wrong. This one takes twelve dice the authors printed,
    /// counts them with the success map the data file ships, subtracts the automatic successes the
    /// data file's rate produces, and looks the result up in the data file's own band table — so
    /// the whole chain has to be right to reach the answer the book prints.</para>
    ///
    /// <para><b>The counting function here is a test helper and nothing else.</b> No engine code
    /// exists for this yet and none is being smuggled in: the simulator arrives in a later
    /// slice.</para>
    /// </summary>
    [Fact]
    public void TheArmWrestlingExampleOnPage67ComesOutAsPrinted()
    {
        var map = MetaEntryById("success_map").SuccessMap;
        var auto = MetaEntryById("automatic_successes").AutomaticSuccesses;
        var bands = ChallengeEntryById("narrative_control").Bands;

        Assert.NotNull(map);
        Assert.NotNull(auto);
        Assert.NotNull(bands);

        // Positive control on the fixture itself: this must be the printed roll, twelve dice for
        // a 12d Trait. A fixture that quietly lost a die would still produce *a* number, and the
        // assertions below would then be measuring something the book never printed.
        Assert.Equal(
            CanonicalChallengeRules.ArmWrestling.PoolDice,
            CanonicalChallengeRules.ArmWrestling.GatecrasherRoll.Count);

        var gatecrasher = CountSuccesses(map, CanonicalChallengeRules.ArmWrestling.GatecrasherRoll);
        Assert.Equal(CanonicalChallengeRules.ArmWrestling.GatecrasherSuccesses, gatecrasher);

        var soldier = CanonicalChallengeRules.ArmWrestling.PoolDice / auto.DicePerSuccess;
        Assert.Equal(CanonicalChallengeRules.ArmWrestling.CitizenSoldierAutomaticSuccesses, soldier);

        // Gatecrasher rolled more, so he is the Actor and the Soldier's total is his threshold.
        var net = gatecrasher - soldier;
        Assert.Equal(CanonicalChallengeRules.ArmWrestling.NetSuccesses, net);

        var band = BandFor(bands, net);
        Assert.Equal(CanonicalChallengeRules.ArmWrestling.Outcome, band.Outcome);
        Assert.Equal(CanonicalChallengeRules.ArmWrestling.Embellishment, band.Embellishment);
    }

    /// <summary>Counts a roll with the map the data file ships, not with one written here.</summary>
    private static int CountSuccesses(IReadOnlyDictionary<string, int> map, IEnumerable<int> dice) =>
        dice.Sum(die => map[die.ToString(CultureInfo.InvariantCulture)]);

    // ── Chapter 5: the pools ─────────────────────────────────────────────────

    [Fact]
    public void TheStartingResolveTableIsTheOnePrintedOnPage83()
    {
        var entry = ResolveEntryById("starting_resolve");
        var table = entry.StartingResolve;

        Assert.NotNull(table);
        Assert.Equal(CanonicalResolveRules.StartingResolveAtTraitCap, table.AtTraitCap);
        Assert.Equal(CanonicalResolveRules.StartingResolvePerRankBelowCap, table.ResolvePerRankBelowCap);
        Assert.Contains($"p.{CanonicalResolveRules.StartingResolvePage}", entry.SourceRef, StringComparison.Ordinal);

        // The four printed rows have to BE the formula rather than merely sit beside it: each row
        // is recomputed from the rate, so a row and the rate cannot drift apart.
        Assert.Equal(CanonicalResolveRules.StartingResolveTable.Count, table.TableRows.Count);

        foreach (var row in table.TableRows)
        {
            Assert.Equal(
                table.AtTraitCap + row.RanksBelowTraitCap * table.ResolvePerRankBelowCap,
                row.Resolve);
        }

        // "Etc. Etc." is the last printed row, so the ladder is open-ended — the same shape the
        // Thresholds table gives Godlike, and asserted rather than assumed because a simulator that
        // read four rows as a closed table would cap Resolve at six.
        Assert.True(table.TableContinuesBeyondThePrintedRows);
    }

    /// <summary>
    /// <b>The one read this file makes into <c>engine/</c>, and it is the point of recording the
    /// table as arithmetic.</b> Chapter 5's ladder is already implemented — <c>CalculateResolve</c>
    /// has computed it since long before <c>data/rules/play/</c> existed — so there are two
    /// statements of one rule in this repository and nothing held them together. This does, on three
    /// worked ranks: at the cap, one die under it, and three dice under it.
    ///
    /// <para>The expected figure is built from the <em>shipped JSON's</em> rate and pivot, not from
    /// a number written here, so the test fails if the file and the engine disagree whichever of
    /// them is wrong. The character has no Determination and no Condition or Plot Hook Flaw, which
    /// are Chapter 2's additions to the same figure and are recorded in the entry's
    /// <c>ambiguity</c> — this compares the base the table states, and nothing else.</para>
    ///
    /// <para><b>This is a read, and it stays a read.</b> Nothing here wires the engine to the play
    /// rules; <see cref="PlayPayloadTests.NothingInTheApplicationNamesAPlayRulesFile"/> would fail
    /// if anybody tried.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void TheEngineComputesTheTableThisFileRecords(int ranksBelowTheCap)
    {
        var table = ResolveEntryById("starting_resolve").StartingResolve;

        Assert.NotNull(table);

        // The file says the ladder is measured from the Trait Cap, so the test measures from the
        // Trait Cap. If that claim changed, this stops being the right comparison and says so.
        Assert.Equal("trait_cap", table.MeasuredFrom);

        var tier = _f.Rules.GetTier("standard");
        Assert.NotNull(tier);

        var cap = tier.TraitCapRank;
        var rank = cap - ranksBelowTheCap;

        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = rank;

        // Positive control on the fixture, and it has to be a claim about the sheet rather than
        // about the line above it: the rank under test must be the highest relevant Trait the
        // character has, or the row is measuring something else and agreeing by accident. The
        // version this replaces asserted the value it had just assigned, which is true by
        // construction and would have gone on passing over an empty sheet.
        var top = sheet.AbilityRanks.Values.Max();
        var atTheTop = sheet.AbilityRanks.Where(r => r.Value == top).Select(r => r.Key).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["might"], atTheTop);
        Assert.Equal(rank, top);

        // No Power can out-rank it either, and the two Chapter 2 additions to this same figure are
        // absent — Determination buys Resolve outright and a Condition or Plot Hook Flaw grants a
        // point — so what is compared is the base the chapter's table states and nothing else.
        Assert.Empty(sheet.SelectedPowers);
        Assert.Null(sheet.GetPower("determination"));
        Assert.DoesNotContain(
            sheet.Flaws,
            f => _f.Rules.GetFlaw(f.FlawId)?.FlawType is "condition" or "plot_hook" or "plot_hook_and_condition");

        var fromTheFile = table.AtTraitCap + ranksBelowTheCap * table.ResolvePerRankBelowCap;
        var fromTheEngine = _f.Derived.CalculateResolve(sheet);

        Assert.Equal(fromTheFile, fromTheEngine);
    }

    /// <summary>
    /// <b>The eleven Powers Chapter 5 names as Resolve-exempt, checked against the flags
    /// <c>powers.json</c> actually carries.</b> The character rules answer this question through
    /// <see cref="DerivedStatsCalculator.ResolveAffectedByPower"/> — an explicit
    /// <c>affects_resolve</c> if there is one, and the Movement/Sensory category default otherwise —
    /// and until now nothing compared that answer to the page it came from.
    ///
    /// <para><b>One of the eleven needs a mapping and it is ours, not the book's.</b> "Swinging" is
    /// <c>swing_line</c> in the rules data. "Super Senses" needs none — its sixteen entries are all
    /// prefixed with the printed name, because the book prints sixteen options under one Power —
    /// and the mapping lives here rather than in the JSON precisely because it is a reading:
    /// <c>resolve.json</c> transcribes the printed names and nothing else.</para>
    ///
    /// <para><b>And one of the eleven is not asked at all.</b> p.83 exempts "Expertise (except for
    /// combat skills)", which is a carve-out and not an exemption, so requiring <c>false</c> for it
    /// would have made a green test ratify an answer the page contradicts. It is excluded by name,
    /// with the reason, and the divergence is asserted on purpose by
    /// <see cref="ADivergenceTheEngineCannotYetExpress"/>.</para>
    /// </summary>
    [Fact]
    public void EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData()
    {
        var named = ResolveEntryById("resolve_exceptions").Exceptions;

        Assert.NotNull(named);
        Assert.Equal(CanonicalResolveRules.NamedResolveExemptPowers, named.NamedPowers);

        // The names this check does not ask about have to be names the page actually prints, or an
        // exclusion could quietly cover nothing (a typo) or everything (a widened rule).
        Assert.Subset(named.NamedPowers.ToHashSet(StringComparer.Ordinal), NamesThisCheckCannotAsk.Keys.ToHashSet(StringComparer.Ordinal));

        var faults = new List<string>();
        var matched = 0;

        foreach (var printedName in named.NamedPowers.Where(n => !NamesThisCheckCannotAsk.ContainsKey(n)))
        {
            var dataName = ChapterFivePowerNames.GetValueOrDefault(printedName, printedName);

            var entries = _f.Rules.Powers
                .Where(p => string.Equals(p.Name, dataName, StringComparison.Ordinal)
                            || p.Name.StartsWith($"{dataName} —", StringComparison.Ordinal))
                .ToList();

            if (entries.Count == 0)
            {
                faults.Add($"'{printedName}' matches no entry in powers.json (looked for '{dataName}')");
                continue;
            }

            foreach (var power in entries)
            {
                matched++;

                if (DerivedStatsCalculator.ResolveAffectedByPower(power))
                {
                    faults.Add(
                        $"'{printedName}' is named on p.{CanonicalResolveRules.ExceptionsPage} as a Power "
                        + $"that does not affect Resolve, and powers.json entry '{power.Id}' "
                        + $"(category {power.Category}, affects_resolve {power.AffectsResolve?.ToString() ?? "unset"}) "
                        + "counts towards it");
                }
            }
        }

        // Positive control: ten of the eleven printed names are asked about, and Super Senses alone
        // is sixteen entries, so a lookup that had stopped matching would fault nothing and prove
        // nothing.
        Assert.True(matched >= 25, $"Only {matched} powers.json entries were reached for {named.NamedPowers.Count} printed names.");
        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// The one printed name spelled differently in <c>powers.json</c>. <b>Ours, and kept out of the
    /// data</b> — see <see cref="EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData"/>. Super
    /// Senses needs no entry here because its sixteen options are all prefixed with the printed
    /// name.
    /// </summary>
    private static readonly Dictionary<string, string> ChapterFivePowerNames =
        new(StringComparer.Ordinal) { ["Swinging"] = "Swing Line" };

    /// <summary>
    /// The printed names the exemption cross-check must <b>not</b> ask <c>powers.json</c> about,
    /// each with the reason it cannot answer. <b>Excluded by name and never by a rule</b>, so a
    /// second one cannot be added without writing down why.
    ///
    /// <para>Expertise is the only member, and it is a carve-out rather than an exemption: p.83
    /// reads "Expertise (except for combat skills)", so whether an Expertise counts depends on the
    /// Trait it was nominated to. <c>affects_resolve</c> is one flag on one entry and cannot say
    /// that, so the entry is an unconditional <c>false</c> and the engine's answer is wrong for
    /// exactly the combat-skill case. Requiring <c>false</c> here made a green test <em>ratify</em>
    /// that; <see cref="ADivergenceTheEngineCannotYetExpress"/> asserts it instead, so it is
    /// recorded rather than blessed.</para>
    /// </summary>
    private static readonly Dictionary<string, string> NamesThisCheckCannotAsk =
        new(StringComparer.Ordinal)
        {
            ["Expertise"] =
                "p.83 exempts Expertise 'except for combat skills', and powers.json carries one "
                + "unconditional affects_resolve flag for all of them"
        };

    /// <summary>
    /// <b>A place where the page and the engine give different answers, asserted on purpose.</b>
    /// p.83 exempts "Expertise (except for combat skills)" — so an Expertise nominated to a combat
    /// skill <em>does</em> count towards the opening pool. <c>powers.json</c> has one Expertise
    /// entry with one <c>affects_resolve: false</c>, and the flag has no room for the nomination,
    /// so the engine exempts every Expertise there is.
    ///
    /// <para><b>The engine is not changed here, and neither is the data.</b> Chapter 5 is
    /// transcribed in this slice; teaching <c>DerivedStatsCalculator</c> the carve-out is
    /// <c>PROGRESS.md</c> item 14's engine work, and <c>CLAUDE.md</c>'s rule is that this
    /// repository <em>reports</em> rather than repairs. What was not acceptable was the previous
    /// state: <see cref="EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData"/> required
    /// <c>false</c> for all eleven names including Expertise, so a green suite was ratifying an
    /// answer the page contradicts.</para>
    ///
    /// <para><b>This test is written to go red when the gap closes, and that is the point.</b> It
    /// asserts the engine's present answer exactly. The day the carve-out lands, the figure becomes
    /// the one the page wants and this fails — at which point flip the two numbers and delete the
    /// exclusion in <see cref="NamesThisCheckCannotAsk"/>. A divergence recorded as a passing test
    /// that would still pass after the fix is a divergence nobody will ever notice was closed.</para>
    /// </summary>
    [Fact]
    public void ADivergenceTheEngineCannotYetExpress()
    {
        var tier = _f.Rules.GetTier("standard");
        Assert.NotNull(tier);

        const int abilityRank = 6;
        var cap = tier.TraitCapRank;

        // A 6d martial artist whose Martial Arts specialisation is bought up to the cap: Martial
        // Arts sits at its Might baseline, and Expertise nominated to it is bought the rest of the
        // way. No Determination and no Condition or Plot Hook Flaw, so the figure below is the
        // chapter's base table and nothing else.
        var sheet = RulesFixture.StandardSheet();
        foreach (var ability in _f.Rules.Abilities) sheet.AbilityRanks[ability.Id] = abilityRank;

        sheet.SelectedPowers.Add(new SelectedPower("martial_arts", 0));
        sheet.SelectedPowers.Add(
            new SelectedPower("expertise", cap - abilityRank) { BaselineTraitId = "martial_arts" });

        // Positive controls on the fixture, because every assertion below is about a rank the
        // character has to actually reach. An Expertise that came out at 6 would produce the same
        // Resolve for a reason that has nothing to do with the carve-out.
        var expertise = sheet.GetPower("expertise");
        Assert.NotNull(expertise);
        Assert.Equal(cap, _f.Derived.GetEffectiveRank(expertise, sheet));
        Assert.Equal(abilityRank, sheet.AbilityRanks.Values.Max());
        Assert.Null(sheet.GetPower("determination"));
        Assert.Empty(sheet.Flaws);

        // And the flag really is the unconditional one the page cannot be expressed through.
        var entry = _f.Rules.GetPower("expertise");
        Assert.NotNull(entry);
        Assert.False(
            DerivedStatsCalculator.ResolveAffectedByPower(entry),
            "powers.json now says Expertise affects Resolve unconditionally, which is the opposite "
            + $"error: p.{CanonicalResolveRules.ExceptionsPage} exempts it '"
            + $"{CanonicalResolveRules.ExpertiseQualifier}', so an Expertise nominated to anything "
            + "else must still be exempt. The carve-out needs the nomination, not a flipped flag.");

        var fromTheTable = ResolveEntryById("starting_resolve").StartingResolve;
        Assert.NotNull(fromTheTable);

        // What the page wants: the nomination is a combat skill, so the Expertise counts, the
        // highest relevant rank is the cap, and the opening pool is nothing.
        var thePageWants = fromTheTable.AtTraitCap;

        // What the engine answers: the Expertise is exempt whatever it was nominated to, so the
        // highest relevant rank is the 6d Ability and the pool is six dice of room, twice over.
        var theEngineAnswers =
            fromTheTable.AtTraitCap + (cap - abilityRank) * fromTheTable.ResolvePerRankBelowCap;

        Assert.NotEqual(thePageWants, theEngineAnswers);

        var answer = _f.Derived.CalculateResolve(sheet);

        // The informative assertion first: this is the one that fires the day the gap closes, and
        // "Expected 12, Actual 0" on its own would send the reader to fix the wrong side.
        Assert.True(
            answer != thePageWants,
            $"CalculateResolve now answers {thePageWants} for an Expertise nominated to a combat "
            + $"skill, which is what p.{CanonicalResolveRules.ExceptionsPage} wants — 'Expertise "
            + $"({CanonicalResolveRules.ExpertiseQualifier})' means the nomination counts towards "
            + "the opening pool. The engine has learned the carve-out: swap the two figures in this "
            + "test so it asserts agreement, and delete the Expertise entry from "
            + "NamesThisCheckCannotAsk so the exemption cross-check asks about it again.");

        Assert.Equal(theEngineAnswers, answer);
    }

    /// <summary>
    /// <b>The worked example printed under Challenge Level (p.85), resolved against the shipped
    /// JSON.</b> Four Heroes, a Challenge Level 2 scene, eight points to the GM.
    ///
    /// <para>Chapter 3's fixture is the arm-wrestling exhibition and this is the same instrument for
    /// Chapter 5: the award is computed from the data file's own factors and its own operation, so
    /// a lost factor gives 2, a sum gives 6, and only the transcription the book prints gives 8. The
    /// opening pool beside it is the same trick one step smaller — four Heroes at the rate the file
    /// carries.</para>
    /// </summary>
    [Fact]
    public void TheAdversityExampleOnPage85ComesOutAsPrinted()
    {
        var level = ResolveEntryById("adversity_earn_challenge_level").ChallengeLevel;
        var pool = ResolveEntryById("adversity_pool").Adversity;

        Assert.NotNull(level);
        Assert.NotNull(pool);

        var operands = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["challenge_level"] = CanonicalResolveRules.AdversityExample.ChallengeLevel,
            ["hero_count"] = CanonicalResolveRules.AdversityExample.Heroes
        };

        // Positive control on the fixture: both operands have to be consumed. A factor list that
        // quietly lost the party size would still produce a number, and it would be 2.
        Assert.Equal(operands.Count, level.AwardFactors.Count);
        Assert.All(level.AwardFactors, factor => Assert.Contains(factor, operands.Keys));

        var award = level.AwardOperation switch
        {
            "product" => level.AwardFactors.Aggregate(1, (running, factor) => running * operands[factor]),
            "sum" => level.AwardFactors.Sum(factor => operands[factor]),
            _ => throw new InvalidOperationException(
                $"resolve.json states an award operation this test cannot apply: '{level.AwardOperation}'.")
        };

        Assert.Equal(CanonicalResolveRules.AdversityExample.Adversity, award);

        // And the scene's award is on top of an opening pool the same party size decides.
        Assert.Equal(
            CanonicalResolveRules.AdversityExample.Heroes,
            CanonicalResolveRules.AdversityExample.Heroes * pool.PointsPerHeroPerIssue);

        // The example's level is one the guidance describes, so the fixture is not being resolved
        // against a level the book never discusses.
        Assert.Contains(level.LevelGuidance, row => row.Level == CanonicalResolveRules.AdversityExample.ChallengeLevel);
    }

    /// <summary>
    /// <b><c>CLAUDE.md</c>'s settled rule, as data rather than as prose.</b> "Only Heroes have
    /// Resolve; the GM gets Adversity, spendable on any NPC" — so every spend in this file is keyed
    /// to one side of the screen, and the two keys are the ones the chapter states on pp.84 and 85.
    ///
    /// <para><b>Neither the id prefix nor a cost field is what makes it true.</b> The prefix was
    /// never load-bearing — a spend renamed out of <c>spend_</c>/<c>adversity_spend_</c> would
    /// escape a check built on it — and the cross-check that replaced it, "what does the entry
    /// charge", reached only nine of the twelve: <c>spend_combat</c> defers its costs to Ch.4,
    /// <c>spend_using_powers</c> charges whatever the Power asks, and
    /// <c>adversity_spend_anything_resolve_can</c> charges whatever it is imitating, so all three
    /// were keyed by their id alone after all.</para>
    ///
    /// <para><b>So every spend names its <c>currency</c> outright</b>, transcribed like any other
    /// fact and compared against <see cref="CanonicalResolveRules"/> by the coverage walk. This
    /// test reads that field, never the id: <c>resolve</c> must be the Hero's and
    /// <c>adversity</c> must be the GM's, whatever the entry is called.</para>
    /// </summary>
    [Fact]
    public void EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms()
    {
        var entries = Resolve().Entries;
        var spends = entries.Where(e => e.Spend is not null).ToList();

        // Positive control: a rule about every spend is worth what the set of spends is worth, and
        // p.84 prints six headings (one carrying three) beside p.85's general rule and three
        // exclusives.
        Assert.True(spends.Count >= 12, $"Only {spends.Count} spends were found across the file.");

        // And the two pools have to be told apart at both ends, or one value would satisfy both
        // halves of the rule.
        Assert.NotEqual(CanonicalResolveRules.ResolveIsSpentBy, CanonicalResolveRules.AdversityIsSpentBy);
        Assert.NotEqual(CanonicalResolveRules.ResolveCurrency, CanonicalResolveRules.AdversityCurrency);

        var faults = new List<string>();

        foreach (var entry in spends)
        {
            var spend = entry.Spend!;

            if (spend.Currency is null)
            {
                faults.Add(
                    $"{entry.Id} is a spend and names no currency, so nothing says which pool it "
                    + "draws on");
                continue;
            }

            if (!CanonicalResolveRules.PoolHolders.TryGetValue(spend.Currency, out var holder))
            {
                faults.Add(
                    $"{entry.Id} charges '{spend.Currency}', and Chapter 5 has two pools: "
                    + string.Join(" and ", CanonicalResolveRules.PoolHolders.Keys));
                continue;
            }

            if (entry.Who != holder)
            {
                faults.Add(
                    $"{entry.Id} charges {spend.Currency} and is keyed to {entry.Who ?? "nobody"}; "
                    + $"{spend.Currency} is spent by the {holder}");
            }

            // Where an entry does print a cost, the cost and the declared currency have to be the
            // same currency — otherwise `currency` could quietly disagree with the number beside it.
            if (spend.CostAdversity is not null && spend.Currency != CanonicalResolveRules.AdversityCurrency)
                faults.Add($"{entry.Id} states a cost in Adversity and declares currency {spend.Currency}");

            if (spend.CostResolve is not null && spend.Currency != CanonicalResolveRules.ResolveCurrency)
                faults.Add($"{entry.Id} states a cost in Resolve and declares currency {spend.Currency}");
        }

        // The faults come first deliberately: each names the entry and what is wrong with it, and
        // the two set-shaped assertions below fail with a diff that sends the reader hunting.
        Assert.True(faults.Count == 0, string.Join("; ", faults));

        // Both pools have to be represented, or "every spend is keyed correctly" is satisfied by a
        // file in which every spend is a Hero's.
        foreach (var currency in CanonicalResolveRules.PoolHolders.Keys)
        {
            Assert.Contains(
                spends,
                e => string.Equals(e.Spend!.Currency, currency, StringComparison.Ordinal));
        }

        // A `who` and a `spend` are the same claim from two directions, so an entry cannot carry
        // one without the other.
        Assert.Equal(
            spends.Select(e => e.Id).Order(StringComparer.Ordinal).ToList(),
            entries.Where(e => e.Who is not null).Select(e => e.Id).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// <b>What an ordinary share of Resolve costs is not printed, so it is not transcribed.</b>
    /// p.84 prices exactly one share — "you have to spend 2 points of Resolve for every point you
    /// want to share" — and charges it only to a giver who could not actually be assisting. "As
    /// many points as you wish" says how many may move and never what one costs.
    ///
    /// <para>One-for-one was nonetheless a <c>cost_per_point_shared</c> fact field, with the same
    /// quote beside <c>SharePointCost = 1</c> in <see cref="CanonicalResolveRules"/> — the exact
    /// shape the Thresholds table's <c>gm_discretion</c> column had, a reading dressed as a printed
    /// value and filed under "do not edit this to match the code". It is now an
    /// <c>interpretation</c> beside the entry, and <b>derived rather than typed</b>: the stated
    /// rate is a penalty of double, so the rate it doubles is half of it. Change the printed
    /// penalty and this has to move with it.</para>
    /// </summary>
    [Fact]
    public void TheParShareRateIsInferredFromThePrintedPenaltyRate()
    {
        var entry = ResolveEntryById("spend_assisting_allies");

        Assert.NotNull(entry.Spend);
        Assert.NotNull(entry.Interpretation);
        Assert.NotNull(entry.Interpretation.InferredCostPerPointShared);

        // The one rate the page states, and the entry has to carry it or there is nothing to infer
        // from — a reading derived from a missing figure would be a reading derived from nothing.
        Assert.Equal(
            CanonicalResolveRules.SharePointCostWhenUnableToAssist,
            entry.Spend.CostPerPointSharedWhenUnableToAssist);

        // And the page states no other one. Recorded on the canonical side so the claim sits beside
        // the quote it is a claim about.
        Assert.False(CanonicalResolveRules.OrdinaryShareRateIsPrinted);

        // The derivation: the printed rate is charged as a doubling, so par is half of it.
        const int theDoubling = 2;

        Assert.Equal(
            CanonicalResolveRules.SharePointCostWhenUnableToAssist / theDoubling,
            entry.Interpretation.InferredCostPerPointShared);

        // And the entry says out loud that this is ours rather than the book's, in both places a
        // reader would look: the labelled block and the ambiguity.
        Assert.Contains("not a figure the page prints", entry.Interpretation.WhatThisIs, StringComparison.Ordinal);
        Assert.Contains("not printed", entry.Ambiguity ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Adversity does everything Resolve does and exactly three things more, and the file has to
    /// hold three.</b> "There are also three things you can do with Adversity that Heroes can't do
    /// with Resolve" — a transcribed count with nothing behind it is a number; this makes it a
    /// claim about the data beside it.
    /// </summary>
    [Fact]
    public void AdversityAddsExactlyTheThreeExclusiveSpendsItClaims()
    {
        var mirror = ResolveEntryById("adversity_spend_anything_resolve_can").Spend;

        Assert.NotNull(mirror);
        Assert.True(mirror.CanDoAnythingResolveCan);

        var exclusives = Resolve().Entries
            .Where(e => e.Id.StartsWith("adversity_spend_", StringComparison.Ordinal)
                        && e.Id != "adversity_spend_anything_resolve_can")
            .Select(e => e.Id)
            .ToList();

        Assert.Equal(mirror.ExclusiveSpendsCount, exclusives.Count);
    }

    /// <summary>
    /// The fields an entry may carry when it says it does not transcribe the chapter it points at:
    /// the flag itself, the chapter it defers to, and the ids of what was deferred. <b>Nothing
    /// else</b> — a value is either transcribed from a page this file cites or it is somewhere
    /// else's to record.
    /// </summary>
    private static readonly HashSet<string> ReferenceOnlyFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "transcribed_here", "detail_chapter", "combat_spend_refs", "currency"
        };

    /// <summary>
    /// <b>An entry that says it defers to another chapter must actually defer to it.</b>
    /// <c>spend_combat</c> declares <c>transcribed_here: false</c> and pointed at Ch.4 — and then
    /// carried two of Ch.4's values anyway, with a description claiming the GM's initiative
    /// alternative was "printed here rather than there". <b>Ch.4 p.73 prints it in full</b>, with
    /// the 1 Resolve cost, the duration and the alternative, so the two fields were the second
    /// transcription the entry's own policy exists to prevent — and the one that would have gone
    /// stale first, because Chapter 4's slice will transcribe the page properly.
    ///
    /// <para>Written as a rule over the file rather than as an assertion about the one entry that
    /// breaks it: the next chapter this store points at gets the same guard for free.</para>
    /// </summary>
    [Fact]
    public void AnEntryThatDefersToAnotherChapterCarriesReferencesAndNothingElse()
    {
        var deferring = Resolve().Entries.Where(e => e.Spend?.TranscribedHere == false).ToList();

        // Positive control: a rule over an empty set is satisfied by there being nothing to check,
        // and this one is worth exactly as much as the entries it reaches.
        Assert.NotEmpty(deferring);

        var faults = new List<string>();

        foreach (var entry in deferring)
        {
            var strangers = EntryLeaves(entry.Id, entry, stopAt: null)
                .Select(leaf => leaf.Path[(leaf.Path.LastIndexOf('.') + 1)..])
                .Where(name => !ReferenceOnlyFields.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (strangers.Count > 0)
            {
                faults.Add(
                    $"{entry.Id} sets transcribed_here false and still carries "
                    + string.Join(", ", strangers));
            }
        }

        Assert.True(
            faults.Count == 0,
            "An entry that hands a mechanic to another chapter may carry the reference and nothing "
            + "more — transcribe the value in that chapter's own file, where the page it comes from "
            + "is the one being cited: " + string.Join("; ", faults));
    }

    /// <summary>
    /// <b>Every entry names the heading it was transcribed from, and the heading has to exist on the
    /// page the entry cites.</b> This is the structural link between the data and the corpus: a
    /// <c>source_ref</c> alone says a page, and a page in a six-page chapter is a wide target.
    ///
    /// <para>It is also why <c>printed_under</c> is exempt from the canonical walk — it is checked
    /// against the book directly rather than against a constant somebody typed, which is the
    /// stronger of the two.</para>
    /// </summary>
    [Fact]
    public void EveryEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterFiveHeadings();

        // Positive control: the corpus lookup has to have found the chapter's headings. An empty
        // set would fault every entry, which is loud — but a set missing one page would fault only
        // the entries on it and read as a data error, so the count is asserted.
        Assert.True(headings.Count >= 25, $"Only {headings.Count} headings were read out of Chapter 5.");

        // Negative control, and it has to be a heading that really exists somewhere else: a made-up
        // one is rejected by a lookup that had lost every page number too. VILLAINY is printed on
        // p.85 and on no other page of the chapter, so the pair is right and the page is wrong.
        Assert.Contains((85, "VILLAINY"), headings);
        Assert.DoesNotContain((83, "VILLAINY"), headings);
        Assert.DoesNotContain((84, "VILLAINY"), headings);

        var faults = new List<string>();

        foreach (var entry in Resolve().Entries)
        {
            var page = int.Parse(
                Regex.Match(entry.SourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                CultureInfo.InvariantCulture);

            if (!headings.Contains((page, entry.PrintedUnder)))
            {
                faults.Add(
                    $"{entry.Id}: printed_under '{entry.PrintedUnder}' is not a heading on p.{page} "
                    + "of Chapter 5 — either the heading or the source_ref page is wrong");
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>Every (printed page, heading) pair in the Chapter 5 corpus.</summary>
    private static HashSet<(int Page, string Heading)> ChapterFiveHeadings()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch05-resolve-and-adversity.json")));

        var headings = new HashSet<(int, string)>();

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (!section.TryGetProperty("printed_page", out var page)
                || page.ValueKind != JsonValueKind.Number
                || !section.TryGetProperty("heading", out var heading)
                || heading.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            headings.Add((page.GetInt32(), heading.GetString()!));
        }

        return headings;
    }

    /// <summary>
    /// <b>Which of the six ways to earn a simulator could apply on its own is ours, and it is
    /// derived rather than typed.</b> The book draws no such line — it lists six ways and then says
    /// a GM may award a point for anything at all. But a simulator has to know which awards it can
    /// hand out and which need somebody at the table to say that a moment happened, and an
    /// unrecorded answer to that becomes an implementation decision nobody made.
    ///
    /// <para>The list is computed from the entries' own <c>kind</c>, so a seventh way added as a
    /// formula appears here automatically and one demoted to narrative disappears. Both halves are
    /// required to be non-empty: a derivation that produced everything or nothing would agree with
    /// a matching claim and check nothing.</para>
    /// </summary>
    [Fact]
    public void TheEarningsASimulatorCouldApplyAreExactlyTheOnesWithAStatedTrigger()
    {
        var earning = Resolve().Entries.Where(e => e.Earning is not null).ToList();

        Assert.Equal(6, earning.Count);

        var mechanisable = earning
            .Where(e => string.Equals(e.Kind, "formula", StringComparison.Ordinal))
            .Select(e => e.Id)
            .ToList();

        var fiat = earning
            .Where(e => !string.Equals(e.Kind, "formula", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(mechanisable);
        Assert.NotEmpty(fiat);

        var interpretation = ResolveEntryById("resolve_earning_overview").Interpretation;

        Assert.NotNull(interpretation);
        Assert.NotNull(interpretation.MechanisableEntryIds);
        Assert.Equal(mechanisable, interpretation.MechanisableEntryIds);

        // Each of the two groups has to earn its label from the page. A mechanisable one states a
        // figure and a bound the rules settle themselves; a fiat one carries the printed marker of
        // somebody's judgement — a GM's feeling, a fit with the situation, a caution against
        // gaming it, or no figure at all.
        foreach (var entry in earning.Where(e => mechanisable.Contains(e.Id, StringComparer.Ordinal)))
        {
            Assert.NotNull(entry.Earning!.AwardResolve);
            Assert.True(
                entry.Earning.LimitPerBattle is not null || entry.Earning.MustBeSpentOnTheSamePage == true,
                $"{entry.Id} is listed as mechanisable and states no bound a program could apply.");
        }

        foreach (var entry in fiat)
        {
            Assert.True(
                entry.Earning!.GmJudged == true
                || entry.Earning.MustFitTheSituation == true
                || entry.Earning.NotAnInvitationToDerailTheGame == true
                || entry.Earning.AwardStated == false,
                $"{entry.Id} is left to the GM here and carries nothing on the page that says so.");
        }
    }

    /// <summary>
    /// <b>The one claim <c>resolve.json</c> makes about another play file, checked across both.</b>
    /// <c>spend_challenge_roll_dice</c>'s description says Chapter 3's Defining Moment is the same
    /// purchase at three times the rate, and until now that was a sentence in prose with the two
    /// numbers a file apart — <c>challenge.json</c> carrying both, and nothing tying either to the
    /// ordinary spend Chapter 5 actually prints.
    ///
    /// <para>The multiple is read out of <c>challenge.json</c>'s own pair, so this is not a third
    /// place to type 3: the Defining Moment rate has to be exactly that multiple of the ordinary
    /// one, and the ordinary one has to be the rate Chapter 5's entry states in dice per point of
    /// Resolve. Change either file alone and the two stop agreeing.</para>
    /// </summary>
    [Fact]
    public void TheDefiningMomentBuysTripleWhatChapterFivesOrdinaryDiceSpendBuys()
    {
        var moment = ChallengeEntryById("defining_moment").DefiningMoment;
        var ordinary = ResolveEntryById("spend_challenge_roll_dice").Spend;

        Assert.NotNull(moment);
        Assert.NotNull(ordinary);

        // Chapter 5's ordinary purchase, as dice per point: one point buys one die.
        Assert.NotNull(ordinary.CostResolve);
        Assert.NotNull(ordinary.DiceGained);
        Assert.Equal(0, ordinary.DiceGained % ordinary.CostResolve);

        var chapterFivesRate = ordinary.DiceGained / ordinary.CostResolve;

        // Positive control: a rate of zero would divide into anything and prove nothing.
        Assert.True(chapterFivesRate > 0, $"Chapter 5's ordinary dice spend comes out at {chapterFivesRate} dice per point.");

        // Chapter 3 restates the ordinary rate beside its own, which is what makes the comparison
        // possible at all — and the two files have to agree about it before the multiple means
        // anything.
        Assert.Equal(chapterFivesRate, moment.OrdinaryDicePerResolveSpent);

        var multiple = moment.DicePerResolveSpent / moment.OrdinaryDicePerResolveSpent;

        // And the literal below is tied to the sentence that makes the claim, so the two cannot be
        // changed apart: resolve.json's description calls the Defining Moment "the same purchase at
        // three times the rate", and that sentence is what this test exists to hold to the data.
        const int theMultipleTheDescriptionClaims = 3;

        Assert.Contains(
            "three times the rate",
            ResolveEntryById("spend_challenge_roll_dice").Description,
            StringComparison.Ordinal);

        Assert.Equal(theMultipleTheDescriptionClaims, multiple);
        Assert.Equal(theMultipleTheDescriptionClaims * chapterFivesRate, moment.DicePerResolveSpent);
    }

    /// <summary>
    /// <b>Chapter 1's summary prints two of this chapter's rules a second time, on p.11.</b> Same
    /// instrument as <see cref="TheThreeRulesChapterOneReprintsAgreeWithTheTranscription"/> does for
    /// Chapter 3: everything else here compares one transcription to another, and a second printing
    /// is the only independent check the book itself offers.
    ///
    /// <para><b>Both expectations are built from the canonical values rather than typed out.</b> The
    /// Adversity rate is formatted from the number; the direction of the Resolve table is computed
    /// from its rows, so a table that had been inverted would look for "the more Resolve you have",
    /// which p.11 does not print.</para>
    /// </summary>
    [Fact]
    public void ChapterOneReprintsTheAdversityRateAndTheShapeOfTheResolveTable()
    {
        var page = ChapterOnePage(CanonicalResolveRules.ChapterOneSummaryPage);

        // Positive control: an empty haystack would satisfy nothing below and look like agreement.
        Assert.True(page.Length > 500, $"Ch.1 p.{CanonicalResolveRules.ChapterOneSummaryPage} came back as {page.Length} characters.");

        var rate = $"{CanonicalResolveRules.AdversityPerHeroPerIssue} point of Adversity per Hero";
        Assert.Contains(rate, page, StringComparison.Ordinal);

        var rows = CanonicalResolveRules.StartingResolveTable;
        var moreRoomMeansMoreResolve = rows[^1].Resolve > rows[0].Resolve;

        var clause = moreRoomMeansMoreResolve
            ? "the more powerful you are, the less Resolve you have"
            : "the more powerful you are, the more Resolve you have";

        Assert.Contains(clause, page, StringComparison.Ordinal);

        // And the citation is on the entries, so it is a reference rather than a coincidence.
        foreach (var id in new[] { "starting_resolve", "adversity_pool" })
        {
            Assert.Contains(
                ResolveEntryById(id).CorroboratedBy ?? [],
                reference => reference.Contains(
                    $"p.{CanonicalResolveRules.ChapterOneSummaryPage}", StringComparison.Ordinal));
        }
    }

    // ── Chapter 4: the fixtures from the book ────────────────────────────────

    /// <summary>
    /// <b>Half a number, read out of <c>play_meta.json</c> rather than out of a call to
    /// <c>Math.Ceiling</c> written here.</b> Every Chapter 4 figure that halves something — a
    /// special effect's duration, the reduction that breaks it, a Health average — goes through
    /// this, so a change to the Glossary rule the file records moves every one of them together and
    /// the fixtures below stop matching the printed answers.
    /// </summary>
    private static int HalfBy(int value, string direction) => direction switch
    {
        "up" => (int)Math.Ceiling(value / 2.0),
        "down" => (int)Math.Floor(value / 2.0),
        _ => throw new InvalidOperationException($"unknown rounding direction '{direction}'")
    };

    private static string BookWideRoundingDirection() =>
        MetaEntryById("half_rounds_up").Rounding?.Direction
        ?? throw new InvalidOperationException("play_meta.json records no rounding direction.");

    /// <summary>
    /// <b>p.74's movement example, resolved through the file's own rates.</b> "Powermad needs to
    /// smash a machinegun turret at Distant Range. He has no Travel Power, so it'll take him 2 pages
    /// to run up to the thing… His ally, Flicker, is also at Distant Range, but she has the Running
    /// Power at 9d, so she can move to within Close Range of the turret and smash it in 1 page."
    ///
    /// <para>Neither number is typed here: both come out of <c>movement</c>, and the rank that
    /// decides which applies comes out of the same block. Flicker's 9d is the fixture's own input,
    /// and the assertion that it clears the threshold is made against the file rather than against a
    /// constant, so lowering the printed 6d in the data would put Powermad and Flicker on the same
    /// footing and fail here rather than silently.</para>
    /// </summary>
    [Fact]
    public void TheMovementExampleOnPageSeventyFourComesOutAsPrinted()
    {
        var movement = CombatEntryById("movement").Movement;

        Assert.NotNull(movement);

        const int flickerRunningRank = 9;

        // Positive control on the fixture: the two characters must actually differ in the way the
        // example turns on, or both halves below would be true of any pair of numbers.
        Assert.True(
            flickerRunningRank >= movement.TravelPowerRankRequired,
            "Flicker's 9d Running has to clear the Travel Power rank the file records, or the "
            + "example is not exercising the rule it is printed to illustrate.");

        var powermad = movement.PagesPerRangeClass;
        var flicker = flickerRunningRank >= movement.TravelPowerRankRequired
            ? movement.PagesPerRangeClassWithATravelPower
            : movement.PagesPerRangeClass;

        Assert.Equal(2, powermad);   // "it'll take him 2 pages to run up to the thing"
        Assert.Equal(1, flicker);    // "she can move to within Close Range … in 1 page"
    }

    /// <summary>
    /// <b>p.74's chase, run one exchange at a time through <c>chases</c>.</b> Flicker starts at
    /// Distant Range and wins three exchanges with 3, 1 and 4 net successes; the printed outcome is
    /// that the first closes a range class and lends her dice, the second lends dice and nothing
    /// else, and the third ends the chase.
    ///
    /// <para>The threshold, the bonus and both ending conditions are read from the file, and the
    /// range ladder is read from <c>range_classes</c>'s own ordering — so a table whose classes were
    /// reordered, or whose closing threshold moved, would put Flicker somewhere the page does not.</para>
    /// </summary>
    [Fact]
    public void TheChaseExampleOnPageSeventyFourComesOutAsPrinted()
    {
        var chase = CombatEntryById("chases").Chase;
        var classes = CombatEntryById("range_classes").Ranges;

        Assert.NotNull(chase);
        Assert.NotNull(classes);

        // The ladder the chase moves along, innermost first, taken from the file's own row order.
        var ladder = classes.Select(c => c.Class).ToList();
        Assert.Equal(["Close", "Distant", "Extreme"], ladder);   // positive control on the ordering

        // The chase names its ends in the book's own phrasing — "Close Range" — while the table names
        // the classes. Resolving one to the other is asserted rather than assumed, because an
        // unmatched name would silently make the ending condition unreachable and the chase run for
        // ever while every other assertion below still passed.
        var innermost = ladder.IndexOf(chase.EndsCloserThan.Replace(" Range", "", StringComparison.Ordinal));
        var outermost = ladder.IndexOf(chase.EndsFartherThan.Replace(" Range", "", StringComparison.Ordinal));

        Assert.Equal(0, innermost);
        Assert.Equal(ladder.Count - 1, outermost);

        var position = ladder.IndexOf("Distant");
        var bonusesEarned = new List<int>();
        var ended = false;

        foreach (var net in new[] { 3, 1, 4 })
        {
            bonusesEarned.Add(chase.ExchangeWinBonusDiceNextExchange);

            if (net >= chase.NetSuccessesToMoveOneRangeClass) position--;

            if (position < innermost || position > outermost) { ended = true; break; }
        }

        // "She closes to within Close Range of the getaway car and gets a +2d bonus on her next roll."
        // "…she wins the exchange but scores only 1 net success. That's enough to secure the +2d
        // bonus … but not enough to close the distance any further."
        // "…this time scoring 4 net successes and ending the chase."
        Assert.Equal([2, 2, 2], bonusesEarned);
        Assert.True(ended, "the third exchange takes Flicker closer than Close Range, which ends the chase");
        Assert.Equal(3, bonusesEarned.Count);
    }

    /// <summary>
    /// <b>p.76's Mind Control, and the rounding the rule never states.</b> Heartbreaker rolls 8
    /// against Parthian's 3, and "Heartbreaker gains control of Parthian's mind for 3 pages" — which
    /// five net successes only reach if half rounds up.
    ///
    /// <para>So this is both the fixture and the proof of the entry's <c>interpretation</c>: the
    /// direction is taken from <c>play_meta.json</c>'s book-wide rule rather than typed, the entry is
    /// required to agree with it, and the other direction is computed too and shown to contradict the
    /// printed answer. A reading asserted only by restating itself would prove nothing.</para>
    /// </summary>
    [Fact]
    public void TheSpecialEffectExampleOnPageSeventySixComesOutAsPrinted()
    {
        var entry = CombatEntryById("special_effects");
        var rounds = entry.Interpretation?.DurationRounds;

        Assert.Equal(BookWideRoundingDirection(), rounds);

        var net = CanonicalCombatRules.SpecialEffect.ExampleAttackSuccesses
                  - CanonicalCombatRules.SpecialEffect.ExampleDefenseSuccesses;

        Assert.Equal(5, net);   // positive control: the fixture's own arithmetic
        Assert.Equal(CanonicalCombatRules.SpecialEffect.ExampleDurationPages, HalfBy(net, rounds!));

        // And the reading is doing work: the other direction gives an answer p.76 contradicts.
        Assert.NotEqual(CanonicalCombatRules.SpecialEffect.ExampleDurationPages, HalfBy(net, "down"));
    }

    /// <summary>
    /// <b>p.76's escape from that Mind Control.</b> Parthian rolls 7 against Heartbreaker's 4 — three
    /// net successes — and "This reduces the Mind Control duration by 2 pages", leaving one of the
    /// three the effect had bought. Same derivation as above, and the same demonstration that the
    /// other direction is wrong: rounding down would remove one page, not two.
    /// </summary>
    [Fact]
    public void TheBreakFreeExampleOnPageSeventySixComesOutAsPrinted()
    {
        var entry = CombatEntryById("breaking_free");
        var rounds = entry.Interpretation?.ReductionRounds;

        Assert.Equal(BookWideRoundingDirection(), rounds);

        var net = CanonicalCombatRules.BreakingFree.ExampleAttemptSuccesses
                  - CanonicalCombatRules.BreakingFree.ExampleOpposingSuccesses;

        Assert.Equal(3, net);   // positive control
        var removed = HalfBy(net, rounds!);
        Assert.Equal(CanonicalCombatRules.BreakingFree.ExamplePagesRemoved, removed);
        Assert.NotEqual(CanonicalCombatRules.BreakingFree.ExamplePagesRemoved, HalfBy(net, "down"));

        // The effect had lasted three pages; two removed leaves one, and the entry's own free-at
        // figure says that is not yet free.
        var remaining = CanonicalCombatRules.SpecialEffect.ExampleDurationPages - removed;
        Assert.Equal(1, remaining);
        Assert.True(
            remaining > entry.BreakingFree!.FreeWhenTheDurationReaches,
            "one page left is not yet free, which is why the page says Parthian is loose after "
            + "Heartbreaker's next turn rather than at once");
    }

    /// <summary>
    /// <b>p.79's Clint Castle, resolved through <c>gritty.json</c>.</b> Five Health, down to one, and
    /// a ninja master stabs him for six: "taking him down to −5 Health. Clint's full Health is 5, so
    /// that's just enough to kill him. Our hero spends 1 Resolve to prevent that from happening,
    /// leaving him at −4 Health."
    ///
    /// <para>The fatal line and the rescue are both computed from the entry rather than typed: the
    /// threshold is the negative of full Health because the file says <c>killed_at</c> is that, and
    /// −4 is one point above it because the file says the point buys exactly that.</para>
    /// </summary>
    [Fact]
    public void TheFatalDamageExampleOnPageSeventyNineComesOutAsPrinted()
    {
        var fatal = GrittyEntryById("gritty_fatal_damage").FatalDamage;

        Assert.NotNull(fatal);
        Assert.True(fatal.HealthCanGoNegative);

        var full = CanonicalGrittyRules.FatalDamage.ExampleFullHealth;
        var after = CanonicalGrittyRules.FatalDamage.ExampleCurrentHealth
                    - CanonicalGrittyRules.FatalDamage.ExampleDamage;

        Assert.Equal(CanonicalGrittyRules.FatalDamage.ExampleHealthAfter, after);

        var fatalThreshold = -full;
        Assert.True(after <= fatalThreshold, "−5 reaches the negative of Clint's full 5 Health exactly");

        // "reduce the damage … to 1 point below this fatal threshold" — one point of Health above it.
        var rescued = fatalThreshold + fatal.CostResolveToAvoid;
        Assert.Equal(CanonicalGrittyRules.FatalDamage.ExampleHealthAfterSpendingResolve, rescued);

        // And the rescue leaves him dying rather than well: −4 is at or below the dying line.
        Assert.True(rescued <= fatal.DyingBeginsWhenLethalDamageReducesYouTo);
    }

    /// <summary>
    /// <b>The Example of Combat, pp.81-82, stepped through the data.</b> Six of its rolls resolve
    /// against three different files — Chapter 4's damage rule, its Grappling table, its Minion rule,
    /// and Chapter 3's narrative-control bands for the last one — and every threshold and rate comes
    /// out of the JSON rather than out of this test.
    ///
    /// <para>This is the fixture rule from <c>docs/guide/play-rules.md</c> applied to Chapter 4: two
    /// transcriptions can agree and both be wrong, so the chapter's data has to be exercised by an
    /// example the authors worked through. It is also why the Example is not itself an entry — a
    /// worked fight is not a mechanic, and it is worth more as the thing that proves the mechanics.</para>
    /// </summary>
    [Fact]
    public void TheExampleOfCombatOnPagesEightyOneAndEightyTwoResolvesThroughTheData()
    {
        var damage = CombatEntryById("damage").Damage;
        var grappling = CombatEntryById("grappling_table").GrapplingTable;
        var minions = CombatEntryById("attacking_minions").AttackingMinions;
        var order = CombatEntryById("edge_ties").TieBreak;
        var bands = ChallengeEntryById("narrative_control").Bands;

        Assert.NotNull(damage);
        Assert.NotNull(grappling);
        Assert.NotNull(minions);
        Assert.NotNull(order);
        Assert.NotNull(bands);

        // Turn order: three Edge scores sorted downward, then the Minions, who have none.
        Assert.False(order.MinionsHaveAnEdge);

        var byEdge = new[]
            {
                ("Citizen Soldier", CanonicalCombatRules.ExampleOfCombat.CitizenSoldierEdge),
                ("the mecha", CanonicalCombatRules.ExampleOfCombat.MechaEdge),
                ("Gatecrasher", CanonicalCombatRules.ExampleOfCombat.GatecrasherEdge)
            }
            .OrderByDescending(c => c.Item2)
            .Select(c => c.Item1)
            .Append("the robotic Minions")
            .ToArray();

        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.TurnOrder, byEdge);

        // "With a total of 2 net successes, a giant mechanical foot stomps Gate into the ground,
        // inflicting 2 points of damage."
        var stompNet = CanonicalCombatRules.ExampleOfCombat.StompAttackSuccesses
                       - CanonicalCombatRules.ExampleOfCombat.StompDefenseSuccesses;

        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.StompDamage, stompNet * damage.DamagePerNetSuccess);

        // "With 5 net successes, our Hero could have defeated up to five of these robotic rogues,
        // so the player describes how Citizen Soldier turns these four into scrap metal."
        var minionNet = CanonicalCombatRules.ExampleOfCombat.MinionAttackSuccesses
                        - CanonicalCombatRules.ExampleOfCombat.MinionDefenseSuccesses;

        var couldDefeat = minionNet * minions.MinionsDefeatedPerNetSuccess;
        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.MinionsCouldHaveBeenDefeated, couldDefeat);
        Assert.Equal(
            CanonicalCombatRules.ExampleOfCombat.MinionsPresent,
            Math.Min(couldDefeat, CanonicalCombatRules.ExampleOfCombat.MinionsPresent));

        // "the mecha also gets 9 successes when it rolls its 15d Armor for defense. Gatecrasher's
        // attack has no effect."
        var chargeNet = CanonicalCombatRules.ExampleOfCombat.ChargeAttackSuccesses
                        - CanonicalCombatRules.ExampleOfCombat.ChargeDefenseSuccesses;

        Assert.Equal(0, chargeNet);
        Assert.Equal(0, chargeNet * damage.DamagePerNetSuccess);

        // "With 3 net successes, the mecha places our Hero in a full hold."
        var holdNet = CanonicalCombatRules.ExampleOfCombat.HoldAttackSuccesses
                      - CanonicalCombatRules.ExampleOfCombat.HoldDefenseSuccesses;

        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.HoldResult, GrapplingResult(grappling, holdNet).Hold);

        // "With no net successes, the Soldier remains trapped in those mighty metal mitts."
        var escapeNet = CanonicalCombatRules.ExampleOfCombat.EscapeAttemptSuccesses
                        - CanonicalCombatRules.ExampleOfCombat.EscapeOpposingSuccesses;

        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.EscapeResult, GrapplingResult(grappling, escapeNet).Escape);

        // "One net success may not be much, but it's enough for narrative control… But with only one
        // net success, the GM gets an embellishment." That is Chapter 3's band table, reached from
        // Chapter 4's fight — the one step here that crosses files.
        var eyebeamNet = CanonicalCombatRules.ExampleOfCombat.EyebeamAttackSuccesses
                         - CanonicalCombatRules.ExampleOfCombat.EyebeamDefenseSuccesses;

        var band = bands.Single(b =>
            (b.MinNetSuccesses is null || eyebeamNet >= b.MinNetSuccesses)
            && (b.MaxNetSuccesses is null || eyebeamNet <= b.MaxNetSuccesses));

        Assert.Equal("actor", band.Outcome);
        Assert.True(band.Embellishment);
    }

    /// <summary>The Grappling table row a net-success figure falls in.</summary>
    private static GrapplingTableRowModel GrapplingResult(
        IReadOnlyList<GrapplingTableRowModel> table, int net) =>
        table.Single(r =>
            (r.MinNetSuccesses is null || net >= r.MinNetSuccesses)
            && (r.MaxNetSuccesses is null || net <= r.MaxNetSuccesses));

    // ── Chapter 4: the readings, derived rather than typed ───────────────────

    /// <summary>
    /// <b>Tough Minions rounds down, and it is the only rule in the book that does.</b> The Glossary
    /// (p.7) says a half goes up and names one exception; <c>gritty.json</c> is that exception. Both
    /// halves are asserted together and by name, because either one alone is a statement about a file
    /// rather than about the book: a rounding rule with no exception recorded and an exception with no
    /// rule to except from would each pass on their own.
    ///
    /// <para>The worked example is what makes it bite. Five net successes defeat two Minions, and the
    /// book-wide direction would defeat three — so the two directions are computed and the printed
    /// answer picks one.</para>
    /// </summary>
    [Fact]
    public void ToughMinionsIsTheOnePlaceAHalfGoesDownward()
    {
        var rounding = MetaEntryById("half_rounds_up").Rounding;
        var tough = GrittyEntryById("gritty_tough_minions").ToughMinions;

        Assert.NotNull(rounding);
        Assert.NotNull(tough);

        Assert.Equal("up", rounding.Direction);

        var exception = Assert.Single(rounding.Exceptions);
        Assert.Equal("Tough Minions", exception.Name);
        Assert.Equal("down", exception.Direction);
        Assert.Contains(
            $"p.{CanonicalChallengeRules.HalfRuleExceptionPage}", exception.Reference, StringComparison.Ordinal);

        // The Gritty file agrees with the exception the Glossary records, and disagrees with the
        // book-wide rule — which is the whole content of "unique case".
        Assert.Equal(exception.Direction, tough.Rounding);
        Assert.NotEqual(rounding.Direction, tough.Rounding);
        Assert.True(tough.RoundingIsANamedUniqueException);

        // And the printed example separates them: 5 net successes, two Minions down, not three.
        var net = CanonicalGrittyRules.ToughMinions.WorkedExampleNetSuccesses;

        Assert.Equal(
            CanonicalGrittyRules.ToughMinions.WorkedExampleMinionsDefeated,
            HalfBy(net, tough.Rounding));

        Assert.NotEqual(
            CanonicalGrittyRules.ToughMinions.WorkedExampleMinionsDefeated,
            HalfBy(net, rounding.Direction));
    }

    /// <summary>
    /// <b>The GM's alternative to seizing the initiative inherits the purchase's duration.</b> p.73
    /// prints the duration once, on the spend, and offers the alternative as a different effect for
    /// the same point of Resolve rather than as a different purchase — so it runs as long. The page
    /// never says so, which is why the value is an <c>interpretation</c> naming the entry it is taken
    /// from rather than a duration typed into the block.
    ///
    /// <para>Chapter 5's <c>spend_combat</c> is the reason this is here at all: it carried these two
    /// values as though p.84 stated them, and the guard written over that file sent them back to this
    /// chapter. So the test also checks that Chapter 5 still defers rather than transcribing.</para>
    /// </summary>
    [Fact]
    public void TheGmAlternativeToSeizingTheInitiativeInheritsThePurchasesDuration()
    {
        var alternative = CombatEntryById("seize_initiative_gm_alternative");
        var inheritedFrom = alternative.Interpretation?.DurationIsInheritedFrom;

        Assert.Equal("seizing_initiative", inheritedFrom);

        // The entry it names has to exist and to carry a duration, or the inheritance is a pointer
        // at nothing — which is exactly how this value went stale in the other chapter.
        var purchase = CombatEntryById(inheritedFrom!).SeizeInitiative;

        Assert.NotNull(purchase);
        Assert.False(string.IsNullOrWhiteSpace(purchase.Duration));

        // The alternative replaces the effect and not the cost, so it carries neither cost nor
        // duration of its own.
        Assert.NotNull(alternative.GmAlternative);
        Assert.Equal(CanonicalCombatRules.SeizeInitiativeGmAlternative.InsteadOf, alternative.GmAlternative.InsteadOf);

        // Chapter 5 still points here rather than restating it.
        var deferred = ResolveEntryById("spend_combat").Spend;
        Assert.NotNull(deferred);
        Assert.False(deferred.TranscribedHere);
        Assert.Contains("Ch.4", deferred.DetailChapter!, StringComparison.Ordinal);
    }

    // ── Chapter 4: the two figures the character engine already computes ─────

    /// <summary>
    /// <b>Edge is printed twice — here and in Chapter 2 — and the engine has computed it since long
    /// before this store existed.</b> Same shape as
    /// <see cref="TheEngineComputesTheTableThisFileRecords"/> for Chapter 5's Resolve table: two
    /// statements of one rule, held to the same answer on a worked character.
    ///
    /// <para><b>The expected figure is built from the file's own formula, not from arithmetic written
    /// here.</b> The operand names are read out of <c>edge.formula</c> and looked up on the sheet, so
    /// a formula that named Willpower instead of Intellect would compute a different number and fail,
    /// rather than agreeing with a hard-coded <c>perception + max(agility, intellect)</c>.</para>
    /// </summary>
    [Fact]
    public void TheEngineComputesTheEdgeThisChapterPrints()
    {
        var edge = CombatEntryById("edge_order").Edge;

        Assert.NotNull(edge);

        var operands = FormulaOperands(edge.Formula, "edge");
        Assert.Equal(["perception", "agility", "intellect"], operands);

        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["perception"] = 4;
        sheet.AbilityRanks["agility"] = 3;
        sheet.AbilityRanks["intellect"] = 5;

        // Positive control: the two candidates must actually differ, or "use whichever is greater"
        // is not being exercised and any of the three orderings would agree.
        Assert.NotEqual(sheet.AbilityRanks["agility"], sheet.AbilityRanks["intellect"]);
        Assert.Empty(sheet.SelectedPowers);   // no Danger Sense, Lightning Reflexes or Super Speed

        var fromTheFile = sheet.AbilityRanks[operands[0]]
                          + Math.Max(sheet.AbilityRanks[operands[1]], sheet.AbilityRanks[operands[2]]);

        Assert.Equal(fromTheFile, _f.Derived.CalculateEdge(sheet));
    }

    /// <summary>
    /// <b>Health is the second figure this chapter shares with the character engine, and the one that
    /// proves the rounding reading.</b> The formula's operands are read out of the file, the direction
    /// of the average comes from <c>play_meta.json</c>'s book-wide rule by way of the entry's
    /// <c>interpretation</c>, and the fixture is deliberately odd — Toughness 3 with Might 4 averages
    /// 3.5 — so the two directions give different answers and only one matches the engine.
    /// </summary>
    [Fact]
    public void TheEngineComputesTheHealthThisChapterPrintsIncludingTheRounding()
    {
        var entry = CombatEntryById("health");
        var health = entry.Health;

        Assert.NotNull(health);

        var rounds = entry.Interpretation?.AverageRounds;
        Assert.Equal(BookWideRoundingDirection(), rounds);

        var operands = FormulaOperands(health.Formula, "health");
        Assert.Equal(["toughness", "might", "toughness", "willpower"], operands);

        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["toughness"] = 3;
        sheet.AbilityRanks["might"] = 4;
        sheet.AbilityRanks["willpower"] = 1;

        // Positive control on the fixture: the pair has to be odd, or the rounding under test never
        // fires and both directions agree.
        Assert.Equal(1, (sheet.AbilityRanks["toughness"] + sheet.AbilityRanks["might"]) % 2);

        int Average(string a, string b, string direction) =>
            HalfBy(sheet.AbilityRanks[a] + sheet.AbilityRanks[b], direction);

        var up = Math.Max(Average(operands[0], operands[1], rounds!), Average(operands[2], operands[3], rounds!));
        var down = Math.Max(Average(operands[0], operands[1], "down"), Average(operands[2], operands[3], "down"));

        Assert.NotEqual(up, down);   // the reading is doing work on this fixture
        Assert.Equal(up, _f.Derived.CalculateHealth(sheet));

        // And the Foe rule the same entry records is the other half of Chapter 2's sentence.
        Assert.True(health.FoesHalveTheResult);
        Assert.False(health.MinionsUseHealth);
    }

    /// <summary>
    /// The lower-cased identifiers on the right of a formula's <c>=</c>, in the order they appear,
    /// with the left-hand name required to be <paramref name="expectedSubject"/>. Function names are
    /// dropped, so <c>max(average(toughness, might), average(toughness, willpower))</c> yields the
    /// four Traits and nothing else.
    /// </summary>
    private static List<string> FormulaOperands(string formula, string expectedSubject)
    {
        var parts = formula.Split('=', 2);
        Assert.Equal(2, parts.Length);
        Assert.Equal(expectedSubject, parts[0].Trim());

        return Regex.Matches(parts[1], @"[a-z_]+")
            .Select(m => m.Value)
            .Where(name => name is not ("max" or "min" or "average"))
            .ToList();
    }

    // ── Chapter 4: structural guards of its own ──────────────────────────────

    /// <summary>
    /// <b>The same guard <c>resolve.json</c> carries, over this chapter's one deferral.</b> p.75
    /// hands NPC Health to Chapter 8 and does not restate it, so <c>npc_health</c> records the
    /// pointer and nothing else. An entry that says it does not transcribe and then transcribes
    /// anyway is precisely the second copy the policy exists to prevent — which is how Chapter 5's
    /// combat spend came to be carrying two of this chapter's values.
    ///
    /// <para>Written over the whole file rather than over the one entry, so the next chapter this
    /// store points at is covered without anybody remembering to.</para>
    /// </summary>
    [Fact]
    public void ACombatEntryThatDefersToAnotherChapterCarriesReferencesAndNothingElse()
    {
        var deferring = Combat().Entries.Where(e => e.Reference is { TranscribedHere: false }).ToList();

        // Positive control: there has to be one, or the guard is measuring nothing.
        var entry = Assert.Single(deferring);
        Assert.Equal("npc_health", entry.Id);

        Assert.Equal(CanonicalCombatRules.NpcHealth.DetailChapter, entry.Reference!.DetailChapter);
        Assert.NotEmpty(entry.Reference.DeferredTopics);

        // Nothing else on the entry: every other block is null, so there is no transcribed value to
        // go stale against the chapter it points at.
        var carried = EntryLeaves(entry.Id, entry, stopAt: null)
            .Select(leaf => leaf.Path)
            .Where(path => !path.StartsWith($"{entry.Id}.reference.", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            carried.Count == 0,
            $"{entry.Id} says it does not transcribe Chapter 8 and then carries "
            + string.Join(", ", carried));
    }

    /// <summary>
    /// <b>Ten Gritty Combat Rules, and every one of them a setting for the whole table.</b> The
    /// chapter offers them as things a group turns on rather than as things a character does, which is
    /// what <c>kind</c> records — and the count is asserted because a rule quietly dropped from the
    /// file would leave every other test green.
    /// </summary>
    [Fact]
    public void TheTenGrittyRulesAreEachATableSetting()
    {
        var entries = Gritty().Entries;

        var settings = entries.Where(e => e.Kind == "table_setting").Select(e => e.Id).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(
            [
                "gritty_active_defenses", "gritty_close_range", "gritty_fatal_damage",
                "gritty_friendly_fire", "gritty_hard_targets", "gritty_raised_gear_limit",
                "gritty_slow_healing", "gritty_the_drop", "gritty_tough_minions",
                "gritty_wound_penalties"
            ],
            settings);

        // The eleventh entry is the paragraph that introduces them, and it is not one of the ten.
        var rest = entries.Where(e => e.Kind != "table_setting").Select(e => e.Id).ToList();
        Assert.Equal(["gritty_overview"], rest);
        Assert.True(GrittyEntryById("gritty_overview").Overview!.AnySubsetMayBeUsed);
    }

    /// <summary>
    /// <b>Chapter 2 prints three of this chapter's rules a second time, and this is the only
    /// independent printing any Chapter 4 value has.</b> Everything else in this file compares one
    /// transcription to another; the Example of Combat answers that for the chain as a whole, and this
    /// answers it for the Threat Ranks table and the two formulas.
    ///
    /// <para><b>The printed text is derived from the transcription, never typed out again.</b> A
    /// Threat row prints as "Civilians 2d" or "Super 7d or More" exactly as its category, floor and
    /// null ceiling say it should, and both formulas are rebuilt out of their own operand lists — so a
    /// wrong value here builds a string Chapter 2 does not contain, and typing the expected strings
    /// would only have added a fourth transcription to disagree with.</para>
    /// </summary>
    [Fact]
    public void ChapterTwoReprintsTheThreatRanksTableAndTheTwoFormulas()
    {
        var minions = ChapterTwoPage(CanonicalCombatRules.ThreatRanksCorroboratingPage);
        var derived = ChapterTwoPage(CanonicalCombatRules.Edge.CorroboratingPage);

        // Positive control: an empty haystack satisfies nothing below and looks exactly like agreement.
        Assert.True(minions.Length > 200, $"Ch.2 p.13 came back as {minions.Length} characters.");
        Assert.True(derived.Length > 200, $"Ch.2 p.60 came back as {derived.Length} characters.");

        var faults = new List<string>();

        foreach (var row in CanonicalCombatRules.ThreatRanks)
        {
            var printed = row.MaxThreat is null
                ? $"{row.Category} {row.MinThreat}d or More"
                : $"{row.Category} {row.MinThreat}d";

            if (!minions.Contains(printed, StringComparison.Ordinal))
                faults.Add($"Ch.2 p.13 does not print the Threat Ranks row '{printed}'");
        }

        var edge = FormulaOperands(CanonicalCombatRules.Edge.Formula, "edge");
        var edgeSentence =
            $"your {Capitalise(edge[0])} plus the greater of your {Capitalise(edge[1])} or {Capitalise(edge[2])}";

        if (!derived.Contains(edgeSentence, StringComparison.Ordinal))
            faults.Add($"Ch.2 p.60 does not print '{edgeSentence}'");

        var health = FormulaOperands(CanonicalCombatRules.Health.Formula, "health");
        var healthSentence =
            $"the average of your {Capitalise(health[0])} and {Capitalise(health[1])} or the average of "
            + $"your {Capitalise(health[2])} and {Capitalise(health[3])}";

        if (!derived.Contains(healthSentence, StringComparison.Ordinal))
            faults.Add($"Ch.2 p.60 does not print '{healthSentence}'");

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    private static string Capitalise(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    /// <summary>Every Chapter 2 section printed on <paramref name="printedPage"/>, joined.</summary>
    private static string ChapterTwoPage(int printedPage)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch02-characters.json")));

        var builder = new StringBuilder();

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (section.TryGetProperty("printed_page", out var page)
                && page.ValueKind == JsonValueKind.Number
                && page.GetInt32() == printedPage)
            {
                builder.Append(section.GetProperty("text").GetString()).Append('\n');
            }
        }

        return builder.ToString();
    }

    // ── Structural guards over both files ────────────────────────────────────

    /// <summary>
    /// A <c>source_ref</c> is the only thing that makes a value checkable, so every entry has to
    /// name a page this chapter actually occupies — or p.7, which is where the Glossary states
    /// the rounding rule the chapter's arithmetic depends on.
    ///
    /// <para><b><c>corroborated_by</c> is the deliberate exception, and it points the other way.</b>
    /// Chapter 1 reprints three of these rules in its summary — the Challenge Rolls bands and the
    /// success rule on p.9, the Thresholds table on p.10 — and refusing those pages would have
    /// thrown away the only place in the book where a Chapter 3 value is printed twice. A
    /// corroborating reference must name a page <em>outside</em> 67-72, because a second citation
    /// of the same chapter is not a second printing, and
    /// <see cref="TheThreeRulesChapterOneReprintsAgreeWithTheTranscription"/> holds it to the
    /// corpus rather than letting it be a decorative citation.</para>
    ///
    /// <para><b>The range is the entry's own chapter's, not the directory's.</b> With Chapter 5
    /// here beside Chapter 3, a single 67-86 window would accept a Chapter 4 page in either file
    /// and accept a Chapter 3 page in the Resolve file — so the bound comes from
    /// <see cref="Files"/>, per file.</para>
    /// </summary>
    [Fact]
    public void EverySourceRefNamesAPageInItsOwnChapterOrTheGlossary()
    {
        var faults = new List<string>();
        var checkedCount = 0;
        var corroborations = 0;

        foreach (var (file, id, sourceRef, corroboratedBy) in AllEntries())
        {
            checkedCount++;

            var facts = FactsFor(file);
            var match = Regex.Match(sourceRef, @"\bp\.(\d+)\b");

            if (!match.Success)
            {
                faults.Add($"{file}/{id}: source_ref names no page ('{sourceRef}')");
                continue;
            }

            var page = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            if ((page < facts.FirstPage || page > facts.LastPage) && page != CanonicalChallengeRules.HalfRulePage)
            {
                faults.Add(
                    $"{file}/{id}: source_ref names p.{page}, which is outside {facts.ChapterLabel} "
                    + $"({facts.FirstPage}-{facts.LastPage}) and is not the Glossary (p.7)");
            }

            foreach (var reference in corroboratedBy ?? [])
            {
                corroborations++;

                var second = Regex.Match(reference, @"\bp\.(\d+)\b");

                if (!second.Success)
                {
                    faults.Add($"{file}/{id}: corroborated_by names no page ('{reference}')");
                    continue;
                }

                var elsewhere = int.Parse(second.Groups[1].Value, CultureInfo.InvariantCulture);

                if (elsewhere >= facts.FirstPage && elsewhere <= facts.LastPage)
                {
                    faults.Add(
                        $"{file}/{id}: corroborated_by names p.{elsewhere}, which is inside "
                        + $"{facts.ChapterLabel} — a second citation of the same chapter is not a "
                        + "second printing");
                }
            }
        }

        // Positive control: an extraction that stopped matching would fault nothing and prove
        // nothing, which is the shape of guard failure this repository has shipped four times.
        Assert.True(checkedCount >= 105, $"Only {checkedCount} entries were read across the five play rules files.");
        Assert.True(corroborations >= 8, $"Only {corroborations} corroborating references were read; Ch.1 reprints three of Ch.3's rules and two of Ch.5's, and Ch.2 reprints three of Ch.4's.");
        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <b>Chapter 1 prints three of Chapter 3's rules a second time, and this is the only place in
    /// the book where a transcription here can be checked against an independent printing.</b>
    /// Everything else in this file compares one transcription to another; the arm-wrestling fixture
    /// answers that for the chain as a whole, and this answers it for the three tables.
    ///
    /// <para><b>The printed row is derived from the canonical record, not typed out again.</b> A
    /// threshold row prints as "Superhuman 6 to 8" or "Godlike 12 or more" exactly as its min, max
    /// and null-ceiling say it should, and a band row prints as "−1 to 0 Opponent with
    /// Embellishment" exactly as its bounds, outcome and embellishment flag say — so a wrong value
    /// in <see cref="CanonicalChallengeRules"/> builds a string Chapter 1 does not contain. Typing
    /// the expected strings out would only have added a fourth transcription to disagree with.</para>
    ///
    /// <para>Two normalisations, both load-bearing: the book sets a real minus sign (U+2212) and the
    /// corpus keeps it, and Ch.1's success sentence uses "or" where Ch.3 uses "and".</para>
    /// </summary>
    [Fact]
    public void TheThreeRulesChapterOneReprintsAgreeWithTheTranscription()
    {
        var bandsPage = ChapterOnePage(9);
        var thresholdsPage = ChapterOnePage(10);

        // Positive control: the corpus lookup has to have found the pages at all. An empty haystack
        // satisfies nothing below and would look exactly like agreement.
        Assert.True(bandsPage.Length > 500, $"Ch.1 p.9 came back as {bandsPage.Length} characters.");
        Assert.True(thresholdsPage.Length > 200, $"Ch.1 p.10 came back as {thresholdsPage.Length} characters.");

        var faults = new List<string>();

        foreach (var row in CanonicalChallengeRules.Thresholds)
        {
            var printed = $"{row.Difficulty} {PrintedRange(row.Min, row.Max)}";

            if (!thresholdsPage.Contains(printed, StringComparison.Ordinal))
                faults.Add($"Ch.1 p.10 does not print the Thresholds row '{printed}'");
        }

        foreach (var band in CanonicalChallengeRules.NarrativeControl)
        {
            var outcome = band.Outcome == "actor" ? "Actor" : "Opponent";
            var printed = $"{PrintedRange(band.Min, band.Max)} {outcome}"
                          + (band.Embellishment == true ? " with Embellishment" : string.Empty);

            if (!bandsPage.Contains(printed, StringComparison.Ordinal))
                faults.Add($"Ch.1 p.9 does not print the Challenge Rolls row '{printed}'");
        }

        // The success rule, with the faces read out of the canonical map rather than written here.
        var ones = CanonicalChallengeRules.SuccessMap.Where(f => f.Value == 1).Select(f => f.Key).Order().ToList();
        var twos = CanonicalChallengeRules.SuccessMap.Where(f => f.Value == 2).Select(f => f.Key).Order().ToList();

        var onesClause = $"one success for every {string.Join(" or ", ones)} rolled";
        var twosClause = $"two successes for every {string.Join(" or ", twos)} rolled";

        if (!bandsPage.Contains(onesClause, StringComparison.Ordinal))
            faults.Add($"Ch.1 p.9 does not print '{onesClause}'");

        if (!bandsPage.Contains(twosClause, StringComparison.Ordinal))
            faults.Add($"Ch.1 p.9 does not print '{twosClause}'");

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>How a net-success band or a threshold row is printed in either chapter's table.</summary>
    private static string PrintedRange(int? min, int? max) => (min, max) switch
    {
        (null, not null) => $"{max} or less",
        (not null, null) => $"{min} or more",
        (not null, not null) when min == max => $"{min}",
        _ => $"{min} to {max}"
    };

    /// <summary>
    /// Every Chapter 1 section printed on <paramref name="printedPage"/>, joined, with the book's
    /// minus sign folded to a hyphen so a bound formatted from an <c>int</c> can be found in it.
    /// </summary>
    private static string ChapterOnePage(int printedPage)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch01-basics.json")));

        var builder = new StringBuilder();

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (section.TryGetProperty("printed_page", out var page)
                && page.ValueKind == JsonValueKind.Number
                && page.GetInt32() == printedPage)
            {
                builder.Append(section.GetProperty("text").GetString()).Append('\n');
            }
        }

        return builder.ToString().Replace('−', '-');
    }

    /// <summary>
    /// Which JSON keys each word of the <c>verified_fields</c> vocabulary may be claimed on. <b>It
    /// is a coverage map, not a semantic one</b>: it says a word is answerable from at least one
    /// key the entry carries, which is the difference between a claim and a decoration.
    ///
    /// <para><b>It is generous everywhere except <c>threshold</c></b>, and that is where it bit: the
    /// two band tables claimed <c>threshold</c> while carrying only net-success bands, which is a
    /// different quantity — net successes are what is left <em>after</em> a threshold. Widening the
    /// map to admit them would have made the check say nothing.</para>
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> VerifiedFieldKeys =
        new(StringComparer.Ordinal)
        {
            ["trigger"] = Keys(
                // Chapter 4
                "may_hold_in_reserve", "waiting_for", "on_your_turn", "table_used_when_might_exceeds",
                "a_grab_is", "a_hold_is", "an_escape_is", "requires_damage_type", "minimum_damage",
                "declared_before", "held_by", "held_against", "taken_on", "what_it_is",
                "trigger", "context", "declared_by", "offered_by", "actor_is", "opponent_is",
                "when_two_or_more_pursue_the_same_goal", "structure", "offered_at_gm_option",
                // Chapter 5
                "granted_at", "depends_on", "applies_when", "available_when", "awarded_at",
                "triggers", "applies_even_if_coerced", "requires_remotely_reasonable",
                "some_powers_require_resolve", "npc_flaws_bite_when_the_opportunity_arises"),
            ["roll"] = Keys(
                "die_sides", "pool_formula", "success_map", "dice_rolled", "counting_faces",
                "dice_per_success", "gm_may_veto", "net_success_formula", "min_dice", "max_dice",
                "sixes_explode", "decided_after_the_roll", "explosion_recurses_while_sixes_keep_coming",
                "six_still_worth_successes", "dice_per_resolve_spent", "ordinary_dice_per_resolve_spent",
                "challenge_roll_penalty_dice", "exchange_win_bonus_dice_next_exchange",
                "helper_rolls_against_threshold", "net_successes_per_bonus_die", "bonus_formula",
                "everyone_rolls_individually", "threshold_source",
                // Chapter 5
                "dice_gained", "unlimited", "rerolls", "includes_dice_bought_with_resolve",
                "applies_to", "keep_the_first_roll_if_the_reroll_is_worse",
                // Chapter 4
                "roll", "formula", "optional_random_initiative", "random_initiative_effective_edge",
                "penalty_dice_per_extra_action", "attack_trait", "defense_traits", "affects",
                "bands", "dice", "works_like", "traits_used_are_chosen_by",
                "the_group_bonus_applies_to", "bonus_dice", "attack_rolls", "defense_rolls",
                "attack_bonus_dice", "attack_penalty_dice", "defense_bonus_dice",
                "cumulative_penalty_dice_per_extra_active_defense", "penalty_dice_to_active_defense",
                "stabilise_roll", "penalty_dice", "penalty_dice_to_negate_it", "default_rank",
                "worked_example_weapon_bonus_dice", "medicine_net_successes_per_point",
                "deception_or_seduction_roll", "requires_an_active_defense",
                "at_or_below_half_full_health_penalty_dice", "at_or_below_zero_health_penalty_dice",
                "health_per_net_success", "damage_per_net_success", "net_successes_per_minion_defeated",
                "minions_defeated_per_net_success", "exchange_win_bonus_dice_next_exchange",
                "net_successes_to_move_one_range_class", "attack_traits"),
            ["threshold"] = Keys(
                "threshold_min", "threshold_max", "difficulty", "threshold", "threshold_source",
                "static_threshold_used_when", "helper_rolls_against_threshold",
                "helper_threshold_difficulty", "gm_discretion_difficulties", "net_success_formula",
                // Chapter 4
                "after_a_fight_threshold", "after_a_fight_difficulty", "full_rest_threshold",
                "full_rest_difficulty", "stabilise_threshold", "stabilise_difficulty",
                "defense_must_exceed_the_attack_roll_by"),
            ["effect"] = Keys(
                "outcome", "embellishment", "held_by", "size", "must_not_contradict_the_narration",
                "must_not_render_it_meaningless", "trade", "requires_agreement_of_both",
                "opponent_may_refuse", "silver_linings_and_complications_decided_by",
                "mixable_per_player", "applied_by", "may_apply_to", "examples_given",
                "distribute_above_net_successes", "above_is_strict",
                "minimum_net_successes_to_distribute", "distribute_to",
                "exchange_winner_narrates_that_exchange", "final_exchange_decides_the_contest",
                "aftermath_permanent_ability_loss_dice", "physical_task_reduces_one_of",
                "mental_task_reduces_one_of", "health_after", "unconscious", "direction", "scope",
                "examples", "successes_when_hit", "otherwise", "net_success_formula",
                "gm_is_opponent_when_unopposed",
                "gm_accepts_player_input_when_actor_is_npc", "dice_per_success",
                "other_explode_offers_provide_no_extra_benefit", "rounding", "best_helper_only",
                "success_map", "challenge_roll_penalty_dice", "replaces_the_permanent_ability_loss",
                "wing_it_is_endorsed", "naming_convention",
                // Chapter 5
                "effect", "at_trait_cap", "resolve_per_rank_below_cap", "formula", "talents_count",
                "criterion", "named_powers", "expertise_qualifier", "gm_has_final_say", "rank_used",
                "powers_named", "gm_may_allow_carryover", "gm_carryover_should_be_rare",
                "gm_may_award_whenever_they_see_fit", "listed_ways_are_examples_not_a_closed_list",
                "award_resolve", "award_adversity", "award_stated", "may_be_outside_combat",
                "interlude_is", "detail_chapter", "not_an_invitation_to_derail_the_game",
                "gm_judged", "gm_may_expand_the_uses", "listed_uses_are_the_basic_ones",
                "points_shared_limit", "must_narrate_the_assistance",
                "narration_has_no_mechanical_effect", "flashback_required_when_unable",
                "unable_to_assist_examples", "invents", "subject_to_gm_approval", "uses",
                "imitated_power_rank_source", "grants_a_new_power", "only_heroes_have_resolve",
                "player_must_spend_for_a_friendly_extra",
                "extra_cannot_use_the_power_if_nobody_spends",
                "applies_only_while_the_extra_is_with_the_heroes",
                "spending_should_never_make_things_worse", "points_per_hero_per_issue", "held_by",
                "is_more_of_a_fixed_resource_than_resolve", "gm_may_add_ways_to_earn",
                "should_not_be_as_easy_to_earn_as_resolve", "award_factors", "award_operation",
                "typical_level_min", "typical_level_max", "level_three_may_be_exceeded",
                "only_a_handful_of_scenes_per_story", "level", "used_for", "awarded_immediately",
                "coercion_forms", "also_when_contrary_to_motivation",
                "can_do_anything_resolve_can", "may_be_spent_on_any_npc", "npc_kinds",
                "all_npcs_share_one_pool", "exclusive_spends_count", "prevents",
                "eligible_characters", "excluded_characters",
                "npcs_cannot_choose_when_their_flaws_bite", "what_it_is",
                "must_be_a_challenge_not_a_punishment", "must_not_be_a_plot_device", "automatic",
                "use_sparingly", "transcribed_here", "combat_spend_refs", "example_given",
                // Chapter 4. The catch-all bucket, as it already is for the other two chapters:
                // an entry claiming "effect" is claiming that something about what the mechanic
                // DOES was checked, and almost every field of almost every entry answers that.
                "an_action_is", "class", "covers", "ranges", "range_rules", "estimates",
                "close_feet", "distant_feet", "extreme_feet", "stated_as", "rank_formula",
                "minimum_rank", "ordinary_people_reach", "min_rank", "max_rank", "range",
                "moving_prevents_actions", "open_terrain_gm_may_allow_range_classes_per_page",
                "winner_gets", "structure", "the_roll_is_an_action", "ends_closer_than",
                "ends_farther_than", "at_closer_than_close_range", "at_farther_than_extreme_range",
                "on_more_successes_than_the_target", "on_failing_the_threshold", "defender_uses",
                "type", "active_represent", "passive_represent", "active_unusable_when",
                "defenses_used_per_attack", "defense_chosen", "toughness_against_lethal",
                "toughness_against_subdual", "physical_damage_default", "psychic_damage_is",
                "psychic_damage_resisted_with", "cover", "visibility", "attacker_relative_size",
                "poor_examples", "none_examples", "reduces", "defeated_at_health", "defeated_means",
                "villains_use_the_same_formula", "foes_halve_the_result", "npc_totals_are_suggestions",
                "minions_use_health", "average_rounds", "duration_rounds", "reduction_rounds",
                "deferred_topics", "sources_given", "defeated_when_the_duration_reaches",
                "available_when", "threshold_source", "partial_means", "full_means",
                "min_net_successes", "max_net_successes", "grab", "hold", "escape", "full",
                "effect_described_by", "penalties_from_multiple_stunts_are_cumulative",
                "sample_effects", "only_characteristic", "category", "min_threat", "max_threat",
                "minions_have_health", "capped_by", "maximum_minions_per_net_success",
                "on_a_damaging_attack", "a_group_acts_like", "targets_per_group_per_page",
                "the_group_bonus_does_not_apply_to", "min_minions", "max_minions",
                "maximum_attacking_one_target_in_close_combat", "on_success", "on_failure",
                "targets", "an_active_defense_must_either", "swimming_may_be_used_only_underwater",
                "own_active_defense_ranks", "self_damage_reduced_by", "the_primary_target_is",
                "range", "defense_ranks", "prevents_attacking", "allows_movement",
                "target_falls_prone", "damage_on_striking_a_solid_object", "redirects_to",
                "all_participants_must_target_the_same_enemy", "use_sparingly",
                "default_combat_is", "rules_are_optional", "any_subset_may_be_used",
                "active_defenses_are_minor_actions", "applies_against", "ignored_for",
                "health_can_go_negative", "killed_at", "resolve_reduces_damage_to",
                "second_attack_is_against", "passive_defense_rank", "negation_available_against",
                "raised_options", "worked_example_weapon", "healing_after_each_battle",
                "rounding", "rounding_is_a_named_unique_exception", "alternative_offered",
                "applies_to_attack_types", "instead_of", "chosen_by", "rationale",
                "order", "still_tied_act", "simultaneous_characters_can_knock_each_other_out",
                "minions_have_an_edge", "minions_act", "minion_allies_and_enemies_act",
                "order_among_holders", "must_be_declared_before_any_challenge_roll",
                "applies_to_defense_rolls", "applies_to_other_challenge_rolls",
                "same_target_more_than_once_per_page", "extra_actions_buy_extra_movement",
                "health_per_net_success", "after_a_damaging_defeat_regains_consciousness",
                "after_a_damaging_defeat_restores_health", "also_frees_you_from_a_special_effect",
                "requires_being_defeated_to_free_yourself_from_an_effect",
                "at_or_below_half_full_health_penalty_dice", "at_or_below_zero_health_penalty_dice",
                "detail_chapter", "final_say", "worked_example_net_successes",
                "area_attack_minions_per_net_success"),
            ["duration"] = Keys(
                "penalty_duration", "regain_consciousness", "limit_per_story",
                "limit_per_scene_per_group", "concurrent_scenes_each_allow_one",
                "may_last_longer_than_an_instant", "typical_exchanges", "arduous_exchanges_min",
                "arduous_exchanges_max", "arduous_condition",
                // Chapter 4
                "a_page_is", "page_ends_when", "turns_per_character_per_page", "if_it_never_happens",
                "duration_is_inherited_from", "pages_to_close_or_open_within_close_range",
                "pages_per_range_class", "pages_per_range_class_with_a_travel_power",
                "pages_per_exchange", "on_a_special_effect", "surprise_lasts", "penalty_lasts",
                "lasts", "target_loses_their_next_turn_to_act", "duration_formula", "expires_at",
                "duration_reduced_by", "free_when_the_duration_reaches", "extends_to",
                "may_be_repeated_scene_after_scene", "limit_per_scene",
                "full_allows_attacks_on_subsequent_pages", "limit_per_target_per_battle",
                "participants_act_at", "counted_per", "first_active_defense_on_a_page_is_unpenalised",
                "dying_damage_per_page", "dying_ends_at", "medicine_healing_limit", "health_per_day",
                "one_point_every_hours", "pages_ignored_per_resolve_point", "full_rest_hours",
                "also_available_after", "removed_npcs_recover", "defeat_by_effect_lasts",
                "partial_resolved_by", "partial_only_physical_action",
                // Chapter 5
                "carries_over_between_issues", "unspent_is_lost_at_issue_end",
                "some_flaws_award_per_issue_instead", "unconscious_until",
                "must_be_spent_on_the_same_page", "may_be_saved_for_later_in_the_issue",
                "limit_per_battle", "limit_per_character_per_issue", "duration"),
            ["cost"] = Keys(
                "explode_cost_resolve", "dice_per_resolve_spent", "ordinary_dice_per_resolve_spent",
                "aftermath_permanent_ability_loss_dice", "ability_may_be_bought_back_later",
                "health_after", "unconscious", "challenge_roll_penalty_dice",
                // Chapter 5
                "cost_resolve", "cost_adversity", "currency",
                "cost_per_point_shared_when_unable_to_assist", "then_unconscious",
                "some_powers_require_resolve",
                // Chapter 4
                "cost", "cost_resolve_to_make_sixes_explode", "cost_resolve_to_avoid",
                "cost_resolve_to_stabilise_immediately", "cost_resolve_to_ignore",
                "redirecting_onto_a_person_costs")
        };

    private static HashSet<string> Keys(params string[] names) =>
        new HashSet<string>(names, StringComparer.Ordinal);

    /// <summary>
    /// <c>verified_fields</c> is what distinguishes a transcribed value from a plausible one, and
    /// it is worthless if the vocabulary drifts: the closed list lives in each file's header and
    /// both files have to agree on it.
    ///
    /// <para><b>And a word has to answer to a key the entry actually carries.</b> Both band tables
    /// declared <c>threshold</c> and carry no threshold — they are net-success bands, which is the
    /// figure left after a threshold has been subtracted — and <c>assisting</c> declared
    /// <c>trigger</c> with no trigger-shaped field on it. A claim about a field that is not there
    /// cannot be checked by anything and reads as verification, so it is worse than no claim.</para>
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresVerifiedFieldsDrawnFromTheClosedList()
    {
        var meta = Meta();
        var challenge = Challenge();
        var resolve = Resolve();

        Assert.Equal(meta.Header.VerifiedFieldsClosedList, challenge.Header.VerifiedFieldsClosedList);
        Assert.Equal(meta.Header.VerifiedFieldsClosedList, resolve.Header.VerifiedFieldsClosedList);

        var closed = meta.Header.VerifiedFieldsClosedList;
        Assert.NotEmpty(closed);

        // Every word of the closed list except "description" has to be mapped, or an unmapped word
        // would be silently unenforceable — the same hole one layer up.
        var unmapped = closed
            .Where(word => word != "description" && !VerifiedFieldKeys.ContainsKey(word))
            .ToList();

        Assert.True(
            unmapped.Count == 0,
            $"The closed list names {string.Join(", ", unmapped)}, which VerifiedFieldKeys does not "
            + "map to any JSON key, so a claim of it could never be checked.");

        var faults = new List<string>();

        foreach (var (id, entry, fields) in AllEntriesWithVerifiedFields())
        {
            if (fields.Count == 0)
            {
                faults.Add($"{id}: verified_fields is empty, so nothing about the entry has been checked against the page");
                continue;
            }

            var strangers = fields.Except(closed, StringComparer.Ordinal).ToList();
            if (strangers.Count > 0) faults.Add($"{id}: verified_fields names {string.Join(", ", strangers)}, which is not in the header's closed list");

            // "description" is the one field every entry has and the one most easily left
            // unchecked, since it is the only field written rather than transcribed.
            if (!fields.Contains("description", StringComparer.Ordinal))
                faults.Add($"{id}: verified_fields does not include description");

            var carried = EntryLeaves(id, entry, stopAt: null)
                .Select(leaf => leaf.Path[(leaf.Path.LastIndexOf('.') + 1)..])
                .ToHashSet(StringComparer.Ordinal);

            foreach (var word in fields.Where(f => f != "description" && closed.Contains(f, StringComparer.Ordinal)))
            {
                if (VerifiedFieldKeys.TryGetValue(word, out var allowed) && !carried.Overlaps(allowed))
                {
                    faults.Add(
                        $"{id}: verified_fields claims '{word}', and the entry carries no field that "
                        + $"could answer it — it has {string.Join(", ", carried.Order())}");
                }
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <c>kind</c> sorts the entries by the shape of the mechanic and is this project's word rather
    /// than the book's, so it is a closed list for the same reason <c>verified_fields</c> is: a
    /// typo would otherwise invent a sixth kind nothing handles.
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresAKindFromTheClosedList()
    {
        var kinds = Meta().Entries.Select(e => (e.Id, e.Kind))
            .Concat(Challenge().Entries.Select(e => (e.Id, e.Kind)))
            .Concat(Resolve().Entries.Select(e => (e.Id, e.Kind)))
            .Concat(Combat().Entries.Select(e => (e.Id, e.Kind)))
            .Concat(Gritty().Entries.Select(e => (e.Id, e.Kind)))
            .ToList();

        Assert.True(kinds.Count >= 105, $"Only {kinds.Count} entries were read across the five files.");

        var faults = kinds
            .Where(k => !CanonicalChallengeRules.EntryKinds.Contains(k.Kind, StringComparer.Ordinal))
            .Select(k => $"{k.Id}: kind '{k.Kind}' is not one of {string.Join(", ", CanonicalChallengeRules.EntryKinds)}")
            .ToList();

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// The header is the file's own account of itself, and every line of it is load-bearing: the
    /// placement note is the access-control decision <see cref="PlayPayloadTests"/> proves, and
    /// <c>not_logic</c> is what stops the next reader wiring an engine to it.
    /// </summary>
    [Theory]
    [InlineData("play_meta.json")]
    [InlineData("challenge.json")]
    [InlineData("resolve.json")]
    [InlineData("combat.json")]
    [InlineData("gritty.json")]
    public void EachFileSaysWhatItIsWhereItSitsAndThatNothingReadsIt(string fileName)
    {
        var header = HeaderOf(fileName);

        Assert.False(string.IsNullOrWhiteSpace(header.WhatThisIs));
        Assert.False(string.IsNullOrWhiteSpace(header.NotLogic));
        Assert.Contains(@"data\rules\*.json", header.PlacementNote, StringComparison.Ordinal);
        Assert.Contains(FactsFor(fileName).ChapterLabel, header.SourceRef, StringComparison.Ordinal);
    }

    private static Header HeaderOf(string fileName) => fileName switch
    {
        "play_meta.json" => Meta().Header,
        "challenge.json" => Challenge().Header,
        "combat.json" => Combat().Header,
        "gritty.json" => Gritty().Header,
        _ => Resolve().Header
    };

    /// <summary>All three files, as (id, entry, verified_fields) triples.</summary>
    private static IEnumerable<(string Id, object Entry, IReadOnlyList<string> Fields)>
        AllEntriesWithVerifiedFields() =>
        Meta().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Resolve().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Combat().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Gritty().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)));

    /// <summary>
    /// <b>Descriptions in <c>data/rules/</c> are this project's own words, never the book's.</b>
    /// The rule predates the corpus and survives it: <c>data/rulebook/</c> holds the printed text
    /// under a permission that does not travel with a fork, and <c>data/rules/</c> is what gets
    /// served. See <c>docs/guide/rules-engine.md</c>.
    ///
    /// <para>Checked as a run of ten consecutive words rather than as a whole sentence, because
    /// the failure mode is a description assembled around a lifted clause, which a sentence-level
    /// comparison walks straight past.</para>
    /// </summary>
    [Theory]
    [InlineData(
        "play_meta.json",
        "You earn one success for every 2 and 4 rolled, and two successes for every 6 rolled.")]
    [InlineData(
        "challenge.json",
        "You earn one success for every 2 and 4 rolled, and two successes for every 6 rolled.")]
    [InlineData(
        "resolve.json",
        "Resolve doesn't carry over between issues. When an issue ends, unspent Resolve is lost.")]
    [InlineData(
        "combat.json",
        "Every net success rolled on a damaging attack inflicts 1 point of damage.")]
    [InlineData(
        "gritty.json",
        "You suffer a cumulative \u2212 1d penalty to all active defense rolls after the first on the same page.")]
    public void NoDescriptionRepeatsARunOfTheBooksOwnWords(string fileName, string knownCorpusSentence)
    {
        const int run = 10;
        var corpus = CorpusWords(FactsFor(fileName).CorpusFiles);

        // Positive control, and it is the whole instrument: a run taken out of the corpus must be
        // found in the corpus. Without it, a normaliser that quietly produced an empty haystack
        // would pass every assertion below while checking nothing at all. It is per file because
        // the corpus is: Chapter 5's descriptions are measured against Chapter 5, and a control
        // sentence from Chapter 3 would pass while proving the wrong chapter had been loaded.
        var control = Runs(Normalise(knownCorpusSentence), run).ToList();
        Assert.NotEmpty(control);
        Assert.All(control, phrase =>
            Assert.True(
                corpus.Contains(phrase, StringComparison.Ordinal),
                $"The control phrase '{phrase}' was not found in the corpus for {fileName}, so this "
                + "test is not measuring anything. Fix the normaliser or the corpus list, not the "
                + "assertion."));

        var faults = new List<string>();

        foreach (var (id, description) in DescriptionsIn(fileName))
        {
            foreach (var phrase in Runs(Normalise(description), run))
            {
                if (corpus.Contains(phrase, StringComparison.Ordinal))
                    faults.Add($"{id}: description repeats the book verbatim — '{phrase}'");
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// The named chapters' prose, reduced to a space-delimited word stream with a leading and
    /// trailing space, so a run can be matched on whole-word boundaries.
    /// </summary>
    private static string CorpusWords(IEnumerable<string> corpusFiles)
    {
        var builder = new StringBuilder(" ");

        foreach (var file in corpusFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RulebookPath, file)));

            foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
            {
                builder.Append(Normalise(section.GetProperty("text").GetString() ?? string.Empty)).Append(' ');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Letters and digits only, lower-cased, single-spaced, wrapped in spaces. The book uses
    /// curly apostrophes and a real minus sign; dropping punctuation altogether means a
    /// description cannot dodge this check by re-punctuating a lifted clause.
    /// </summary>
    private static string Normalise(string text)
    {
        var builder = new StringBuilder(" ");
        var lastWasSpace = true;

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        if (!lastWasSpace) builder.Append(' ');

        return builder.ToString();
    }

    /// <summary>Every window of <paramref name="length"/> consecutive words, space-delimited.</summary>
    private static IEnumerable<string> Runs(string normalised, int length)
    {
        var words = normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i + length <= words.Length; i++)
        {
            yield return " " + string.Join(' ', words.Skip(i).Take(length)) + " ";
        }
    }

    /// <summary>
    /// The ambiguities this slice is required to record. <b>An ambiguity nobody wrote down becomes
    /// an implementation decision nobody made</b> — the simulator would simply pick a reading and
    /// the choice would be invisible from then on.
    ///
    /// <para>The fourth is here because it had been decided rather than recorded: the one-shot
    /// entry carried <c>replaces: "the permanent 1d Ability reduction"</c> as a fact field, which
    /// is one of two live readings of a page that never says it.</para>
    /// </summary>
    [Theory]
    [InlineData("play_meta.json", "sub_one_die_floor")]
    [InlineData("play_meta.json", "automatic_successes")]
    [InlineData("challenge.json", "group_action")]
    [InlineData("challenge.json", "defining_moment_one_shot")]
    // Chapter 5. The first four are the ones a simulator would have to decide silently; the last
    // two are places the page names three of four kinds of character, or a unit it never defines.
    [InlineData("resolve.json", "spend_assisting_allies")]
    [InlineData("resolve.json", "adversity_spend_misfortune")]
    [InlineData("resolve.json", "earn_sacrifice")]
    [InlineData("resolve.json", "boost_and_shapeshifting_count_at_maximum")]
    [InlineData("resolve.json", "earn_interlude")]
    [InlineData("resolve.json", "adversity_spend_suppress_flaw")]
    [InlineData("resolve.json", "adversity_spend_villainy")]
    [InlineData("resolve.json", "reroll_floor")]
    [InlineData("resolve.json", "starting_resolve")]
    [InlineData("resolve.json", "resolve_exceptions")]
    // Chapter 4. The throwing gap and the Minion group bonus change results outright; the GM's
    // alternative to seizing the initiative is the one Chapter 5 pointed at and could not answer;
    // and Wound Penalties records the extraction fault that filed it under another heading.
    [InlineData("combat.json", "throwing_table")]
    [InlineData("combat.json", "minions_attacking")]
    [InlineData("combat.json", "seize_initiative_gm_alternative")]
    [InlineData("gritty.json", "gritty_wound_penalties")]
    public void TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect(string file, string id)
    {
        var ambiguity = file switch
        {
            "play_meta.json" => MetaEntryById(id).Ambiguity,
            "challenge.json" => ChallengeEntryById(id).Ambiguity,
            "combat.json" => CombatEntryById(id).Ambiguity,
            "gritty.json" => GrittyEntryById(id).Ambiguity,
            _ => ResolveEntryById(id).Ambiguity
        };

        Assert.False(
            string.IsNullOrWhiteSpace(ambiguity),
            $"{file}/{id} records no ambiguity, and the book is unclear here.");
    }

    /// <summary>
    /// The header says what was left out and why. For Chapter 3 that is the Sample Thresholds
    /// table — worked examples of thresholds already stated numerically, and the one part of that
    /// chapter the extractor is known to scramble. For Chapter 5 it is the chapter-opening essay
    /// and the advice to track both pools with poker chips, neither of which carries a mechanic.
    /// </summary>
    [Theory]
    [InlineData("challenge.json", "Sample Thresholds")]
    [InlineData("resolve.json", "poker chips")]
    [InlineData("combat.json", "Example of Combat")]
    [InlineData("gritty.json", "worked example")]
    public void TheHeaderSaysWhatWasDeliberatelyLeftOut(string fileName, string mustName)
    {
        var omitted = HeaderOf(fileName).DeliberatelyOmitted;

        Assert.False(string.IsNullOrWhiteSpace(omitted));
        Assert.Contains(mustName, omitted, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Every field in a play rules file deserializes into one of the models above — and that is
    /// all this proves.</b> Same failure <see cref="RulesFileCoverageTests"/> guards against: a key
    /// nothing deserializes reads like a source of truth and is not one.
    ///
    /// <para><b>It was called <c>EveryFieldInAPlayRulesFileIsReadByTheTestModels</c>, and the name
    /// was doing work the body does not.</b> "Read" was taken for "checked" — an adversarial pass
    /// found some twenty fields that deserialized here and were compared to nothing at all. Loading
    /// a value is not verifying it, and the test that verifies is
    /// <see cref="EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook"/> below.</para>
    /// </summary>
    [Theory]
    [InlineData("play_meta.json")]
    [InlineData("challenge.json")]
    [InlineData("resolve.json")]
    [InlineData("combat.json")]
    [InlineData("gritty.json")]
    public void EveryFieldInAPlayRulesFileDeserializesIntoATestModel(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => fileName switch
        {
            "play_meta.json" => JsonSerializer.Deserialize<PlayFile<MetaEntry>>(json, Strict()),
            "challenge.json" => (object?)JsonSerializer.Deserialize<PlayFile<ChallengeEntry>>(json, Strict()),
            "combat.json" => JsonSerializer.Deserialize<PlayFile<CombatEntry>>(json, Strict()),
            "gritty.json" => JsonSerializer.Deserialize<PlayFile<GrittyEntry>>(json, Strict()),
            _ => JsonSerializer.Deserialize<PlayFile<ResolveEntry>>(json, Strict())
        });

        Assert.True(ex is null,
            $"{fileName} carries a field no model reads, so nothing can hold it to the "
            + $"rulebook: {ex?.Message}");
    }

    // ── Every fact field, compared ───────────────────────────────────────────

    /// <summary>
    /// The keys an entry carries whatever it is about. <b>Each is exempt from the walk only because
    /// it has a named guard of its own</b>, and that is the whole of the licence — nothing goes on
    /// this list to make a fault go away:
    /// <list type="bullet">
    ///   <item><c>source_ref</c>, <c>corroborated_by</c> —
    ///   <see cref="EverySourceRefNamesAPageInItsOwnChapterOrTheGlossary"/></item>
    ///   <item><c>verified_fields</c> —
    ///   <see cref="EveryEntryDeclaresVerifiedFieldsDrawnFromTheClosedList"/></item>
    ///   <item><c>description</c> — <see cref="NoDescriptionRepeatsARunOfTheBooksOwnWords"/></item>
    ///   <item><c>kind</c> — <see cref="EveryEntryDeclaresAKindFromTheClosedList"/></item>
    ///   <item><c>ambiguity</c> —
    ///   <see cref="TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect"/></item>
    ///   <item><c>printed_under</c> —
    ///   <see cref="EveryEntryNamesAHeadingPrintedOnThePageItCites"/>, which is a comparison
    ///   against the corpus rather than against a canonical constant, and is why registering
    ///   twenty-eight near-identical checks here would have been the weaker option</item>
    ///   <item><c>who</c> —
    ///   <see cref="EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms"/>, which compares
    ///   both values against <see cref="CanonicalResolveRules"/> and requires every spend to carry
    ///   one</item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> EnvelopeFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "kind", "description", "verified_fields", "source_ref", "corroborated_by",
            "ambiguity", "printed_under", "who"
        };

    /// <summary>
    /// <b>Prose is identified by name, not by a hand-kept list of exemptions.</b> A list of "this
    /// one is only descriptive" is how twenty fact fields came to be unchecked in the first place;
    /// a naming rule cannot be extended quietly. <c>what_this_is</c> labels a block as this
    /// project's own words, and a <c>*_note</c> suffix marks an aside. Everything else is a fact
    /// field and has to be compared to the book.
    /// </summary>
    private static bool IsProse(string leafName) =>
        leafName is "what_this_is" or "note" || leafName.EndsWith("_note", StringComparison.Ordinal);

    /// <summary>
    /// Fields that are this repository's reading of the transcribed values beside them, each proved
    /// by a named test that <em>derives</em> it rather than typing it out — see
    /// <see cref="TheRowsLeftToGmDiscretionAreExactlyTheOnesPrintedAsARange"/>. They are exempt from
    /// the canonical comparison because there is nothing in the book to compare them to; that is
    /// the whole reason they are labelled.
    /// </summary>
    private static readonly HashSet<string> DerivedPaths =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "thresholds.interpretation.gm_discretion_difficulties",
            "resolve_earning_overview.interpretation.mechanisable_entry_ids",
            "spend_assisting_allies.interpretation.inferred_cost_per_point_shared",
            // Chapter 4's three, each derived by a named test from a value that IS printed.
            "seize_initiative_gm_alternative.interpretation.duration_is_inherited_from",
            "health.interpretation.average_rounds",
            "special_effects.interpretation.duration_rounds",
            "breaking_free.interpretation.reduction_rounds"
        };

    /// <summary>
    /// <b>Every fact field of every entry, and the rulebook value it must equal.</b> A path is
    /// <c>&lt;entry id&gt;.&lt;field&gt;</c>, descending into nested objects and indexing into lists
    /// of objects. Registering a path also stops the walk descending into it, so a whole table is
    /// one entry here checked by one comparer.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<object?, string?>> CanonicalChecks =
        new Dictionary<string, Func<object?, string?>>(StringComparer.Ordinal)
        {
            // play_meta.json
            ["dice_pool.die_sides"] = Is(CanonicalChallengeRules.DieSides),
            ["dice_pool.pool_formula"] = Is(CanonicalChallengeRules.PoolFormula),
            ["success_map.success_map"] = MapIs(CanonicalChallengeRules.SuccessMap),
            ["sub_one_die_floor.sub_one_die.dice_rolled"] = Is(CanonicalChallengeRules.SubOneDieDiceRolled),
            ["sub_one_die_floor.sub_one_die.counting_faces"] =
                Is(new[] { CanonicalChallengeRules.SubOneDieCountingFace }),
            ["sub_one_die_floor.sub_one_die.successes_when_hit"] = Is(CanonicalChallengeRules.SubOneDieSuccessesWhenHit),
            ["sub_one_die_floor.sub_one_die.otherwise"] = Is(CanonicalChallengeRules.SubOneDieOtherwise),
            ["automatic_successes.automatic_successes.dice_per_success"] =
                Is(CanonicalChallengeRules.DiceNotRolledPerAutomaticSuccess),
            ["automatic_successes.automatic_successes.gm_may_veto"] =
                Is(CanonicalChallengeRules.AutomaticSuccessesCanBeVetoed),
            ["net_successes.net_success_formula"] = Is(CanonicalChallengeRules.NetSuccessFormula),
            ["half_rounds_up.rounding.direction"] = Is(CanonicalChallengeRules.HalfRoundingDirection),
            ["half_rounds_up.rounding.scope"] = Is(CanonicalChallengeRules.HalfRuleScope),
            ["half_rounds_up.rounding.examples"] = Is(CanonicalChallengeRules.HalfRuleExamples),
            ["half_rounds_up.rounding.exceptions[0].name"] = Is(CanonicalChallengeRules.HalfRuleExceptionName),
            ["half_rounds_up.rounding.exceptions[0].direction"] = Is(CanonicalChallengeRules.HalfRuleExceptionDirection),
            ["half_rounds_up.rounding.exceptions[0].what_it_governs"] =
                Is(CanonicalChallengeRules.HalfRuleExceptionGoverns),
            ["half_rounds_up.rounding.exceptions[0].reference"] =
                Names(CanonicalChallengeRules.HalfRuleExceptionPage),

            // challenge.json
            ["narrative_control.bands"] = BandsAre(CanonicalChallengeRules.NarrativeControl),
            ["actor_selection.actor_selection.actor_is"] = Is(CanonicalChallengeRules.ActorIs),
            ["actor_selection.actor_selection.opponent_is"] = Is(CanonicalChallengeRules.OpponentIs),
            ["actor_selection.actor_selection.when_two_or_more_pursue_the_same_goal"] =
                Is(CanonicalChallengeRules.WhenTwoOrMorePursueTheSameGoal),
            ["actor_selection.actor_selection.gm_is_opponent_when_unopposed"] =
                Is(CanonicalChallengeRules.GmIsOpponentWhenUnopposed),
            ["actor_selection.actor_selection.gm_accepts_player_input_when_actor_is_npc"] =
                Is(CanonicalChallengeRules.GmAcceptsPlayerInputWhenActorIsNpc),
            ["thresholds.thresholds"] = ThresholdsAre(CanonicalChallengeRules.Thresholds),
            ["thresholds.naming_convention"] = Is(CanonicalChallengeRules.ThresholdNamingConvention),
            ["opposed_threshold.opposed.threshold_source"] = Is(CanonicalChallengeRules.OpposedThresholdSource),
            ["opposed_threshold.opposed.static_threshold_used_when"] =
                Is(CanonicalChallengeRules.StaticThresholdUsedWhen),
            ["condition_modifier.condition_modifier.min_dice"] = Is(CanonicalChallengeRules.ConditionModifierMinDice),
            ["condition_modifier.condition_modifier.max_dice"] = Is(CanonicalChallengeRules.ConditionModifierMaxDice),
            ["condition_modifier.condition_modifier.applied_by"] =
                Is(CanonicalChallengeRules.ConditionModifierAppliedBy),
            ["condition_modifier.condition_modifier.may_apply_to"] =
                Is(CanonicalChallengeRules.ConditionModifierMayApplyTo),
            ["condition_modifier.condition_modifier.examples_given"] =
                Is(CanonicalChallengeRules.ConditionModifierExamples),
            ["embellishment.embellishment.held_by"] = Is(CanonicalChallengeRules.EmbellishmentHeldBy),
            ["embellishment.embellishment.must_not_contradict_the_narration"] =
                Is(CanonicalChallengeRules.EmbellishmentMustNotContradictTheNarration),
            ["embellishment.embellishment.must_not_render_it_meaningless"] =
                Is(CanonicalChallengeRules.EmbellishmentMustNotRenderItMeaningless),
            ["embellishment.embellishment.size"] = Is(CanonicalChallengeRules.EmbellishmentSize),
            ["compromise.compromise.offered_by"] = Is(CanonicalChallengeRules.CompromiseOfferedBy),
            ["compromise.compromise.trade"] = Is(CanonicalChallengeRules.CompromiseTrade),
            ["compromise.compromise.requires_agreement_of_both"] =
                Is(CanonicalChallengeRules.CompromiseRequiresAgreementOfBoth),
            ["compromise.compromise.opponent_may_refuse"] = Is(CanonicalChallengeRules.CompromiseOpponentMayRefuse),
            ["traditional_results.bands"] = BandsAre(CanonicalChallengeRules.TraditionalResults),
            ["traditional_results.silver_linings_and_complications_decided_by"] =
                Is(CanonicalChallengeRules.TraditionalResultsQualifiersDecidedBy),
            ["traditional_results.mixable_per_player"] = Is(CanonicalChallengeRules.TraditionalResultsMixablePerPlayer),
            ["checking_your_swing.checking_your_swing.success_map"] =
                MapIs(CanonicalChallengeRules.CheckingYourSwingSuccessMap),
            ["checking_your_swing.checking_your_swing.sixes_explode"] =
                Is(CanonicalChallengeRules.CheckingYourSwingSixesExplode),
            ["checking_your_swing.checking_your_swing.explode_cost_resolve"] =
                Is(CanonicalChallengeRules.CheckingYourSwingExplodeCostResolve),
            ["checking_your_swing.checking_your_swing.decided_after_the_roll"] =
                Is(CanonicalChallengeRules.CheckingYourSwingDecidedAfterTheRoll),
            ["checking_your_swing.checking_your_swing.explosion_recurses_while_sixes_keep_coming"] =
                Is(CanonicalChallengeRules.CheckingYourSwingExplosionRecurses),
            ["checking_your_swing.checking_your_swing.other_explode_offers_provide_no_extra_benefit"] =
                Is(CanonicalChallengeRules.CheckingYourSwingOtherExplodeOffersProvideNoExtraBenefit),
            ["assisting.assist.helper_rolls_against_threshold"] = Is(CanonicalChallengeRules.AssistHelperThreshold),
            ["assisting.assist.helper_threshold_difficulty"] = Is(CanonicalChallengeRules.AssistHelperDifficulty),
            ["assisting.assist.net_successes_per_bonus_die"] = Is(CanonicalChallengeRules.AssistNetSuccessesPerBonusDie),
            ["assisting.assist.rounding"] = Is(CanonicalChallengeRules.HalfRoundingDirection),
            ["assisting.assist.bonus_formula"] = Is(CanonicalChallengeRules.AssistBonusFormula),
            ["assisting.assist.best_helper_only"] = Is(CanonicalChallengeRules.AssistBestHelperOnly),
            ["group_action.group_action.trigger"] = Is(CanonicalChallengeRules.GroupActionTrigger),
            ["group_action.group_action.everyone_rolls_individually"] =
                Is(CanonicalChallengeRules.GroupActionEveryoneRollsIndividually),
            ["group_action.group_action.distribute_above_net_successes"] =
                Is(CanonicalChallengeRules.GroupActionDistributeAboveNetSuccesses),
            ["group_action.group_action.above_is_strict"] = Is(CanonicalChallengeRules.GroupActionAboveIsStrict),
            ["group_action.group_action.minimum_net_successes_to_distribute"] =
                Is(CanonicalChallengeRules.GroupActionMinimumNetSuccessesToDistribute),
            ["group_action.group_action.distribute_to"] = Is(CanonicalChallengeRules.GroupActionDistributeTo),
            ["group_action.group_action.examples_given"] = Is(CanonicalChallengeRules.GroupActionExamples),
            ["contests.contest.structure"] = Is(CanonicalChallengeRules.ContestStructure),
            ["contests.contest.typical_exchanges"] = Is(CanonicalChallengeRules.ContestTypicalExchanges),
            ["contests.contest.arduous_exchanges_min"] = Is(CanonicalChallengeRules.ContestArduousExchangesMin),
            ["contests.contest.arduous_condition"] = Is(CanonicalChallengeRules.ContestArduousCondition),
            ["contests.contest.exchange_winner_narrates_that_exchange"] =
                Is(CanonicalChallengeRules.ContestExchangeWinnerNarratesThatExchange),
            ["contests.contest.exchange_win_bonus_dice_next_exchange"] =
                Is(CanonicalChallengeRules.ContestExchangeWinBonusDice),
            ["contests.contest.final_exchange_decides_the_contest"] =
                Is(CanonicalChallengeRules.ContestFinalExchangeDecidesTheContest),
            ["defining_moment.defining_moment.declared_by"] = Is(CanonicalChallengeRules.DefiningMomentDeclaredBy),
            ["defining_moment.defining_moment.sixes_explode"] = Is(CanonicalChallengeRules.DefiningMomentSixesExplode),
            ["defining_moment.defining_moment.six_still_worth_successes"] = Is(CanonicalChallengeRules.SuccessMap[6]),
            ["defining_moment.defining_moment.explosion_recurses_while_sixes_keep_coming"] =
                Is(CanonicalChallengeRules.DefiningMomentExplosionRecurses),
            ["defining_moment.defining_moment.dice_per_resolve_spent"] =
                Is(CanonicalChallengeRules.DefiningMomentDicePerResolveSpent),
            ["defining_moment.defining_moment.ordinary_dice_per_resolve_spent"] =
                Is(CanonicalChallengeRules.OrdinaryDicePerResolveSpent),
            ["defining_moment.defining_moment.limit_per_story"] =
                Is(CanonicalChallengeRules.DefiningMomentLimitPerStory),
            ["defining_moment.defining_moment.limit_per_scene_per_group"] =
                Is(CanonicalChallengeRules.DefiningMomentLimitPerScene),
            ["defining_moment.defining_moment.concurrent_scenes_each_allow_one"] =
                Is(CanonicalChallengeRules.DefiningMomentConcurrentScenesEachAllowOne),
            ["defining_moment.defining_moment.may_last_longer_than_an_instant"] =
                Is(CanonicalChallengeRules.DefiningMomentMayLastLongerThanAnInstant),
            ["defining_moment.defining_moment.aftermath_permanent_ability_loss_dice"] =
                Is(CanonicalChallengeRules.DefiningMomentPermanentAbilityLossDice),
            ["defining_moment.defining_moment.physical_task_reduces_one_of"] =
                Is(CanonicalChallengeRules.DefiningMomentPhysicalAbilities),
            ["defining_moment.defining_moment.mental_task_reduces_one_of"] =
                Is(CanonicalChallengeRules.DefiningMomentMentalAbilities),
            ["defining_moment.defining_moment.ability_may_be_bought_back_later"] =
                Is(CanonicalChallengeRules.DefiningMomentAbilityMayBeBoughtBackLater),
            ["defining_moment_one_shot.one_shot_variant.context"] = Is(CanonicalChallengeRules.OneShotContext),
            ["defining_moment_one_shot.one_shot_variant.health_after"] = Is(CanonicalChallengeRules.OneShotHealthAfter),
            ["defining_moment_one_shot.one_shot_variant.unconscious"] = Is(CanonicalChallengeRules.OneShotUnconscious),
            ["defining_moment_one_shot.one_shot_variant.regain_consciousness"] =
                Is(CanonicalChallengeRules.OneShotRegainConsciousness),
            ["defining_moment_one_shot.one_shot_variant.challenge_roll_penalty_dice"] =
                Is(CanonicalChallengeRules.OneShotChallengeRollPenaltyDice),
            ["defining_moment_one_shot.one_shot_variant.penalty_duration"] =
                Is(CanonicalChallengeRules.OneShotPenaltyDuration),
            ["defining_moment_one_shot.one_shot_variant.ordinary_games_option.offered_at_gm_option"] =
                Is(CanonicalChallengeRules.OneShotOptionOfferedInOrdinaryGamesAtGmOption),
            ["defining_moment_one_shot.one_shot_variant.ordinary_games_option.replaces_the_permanent_ability_loss"] =
                Is(CanonicalChallengeRules.OneShotOptionInOrdinaryGamesReplacesTheAbilityLoss),
            ["defining_moment_one_shot.one_shot_variant.ordinary_games_option.mandatory"] =
                Is(CanonicalChallengeRules.OneShotOptionInOrdinaryGamesIsMandatory),
            ["judging_thresholds.judging_guideline"] = JudgingIs(CanonicalChallengeRules.JudgingThresholds),
            ["judging_thresholds.calibrated_for"] = Is(CanonicalChallengeRules.JudgingThresholdsCalibratedFor),
            ["judging_thresholds.wing_it_is_endorsed"] = Is(CanonicalChallengeRules.JudgingThresholdsWingItIsEndorsed),

            // resolve.json — Chapter 5, held to CanonicalResolveRules
            ["starting_resolve.starting_resolve.granted_at"] = Is(CanonicalResolveRules.StartingResolveGrantedAt),
            ["starting_resolve.starting_resolve.issue_is_a_game_session"] =
                Is(CanonicalResolveRules.IssueIsAGameSession),
            ["starting_resolve.starting_resolve.depends_on"] = Is(CanonicalResolveRules.StartingResolveDependsOn),
            ["starting_resolve.starting_resolve.measured_from"] = Is(CanonicalResolveRules.StartingResolveMeasuredFrom),
            ["starting_resolve.starting_resolve.at_trait_cap"] = Is(CanonicalResolveRules.StartingResolveAtTraitCap),
            ["starting_resolve.starting_resolve.resolve_per_rank_below_cap"] =
                Is(CanonicalResolveRules.StartingResolvePerRankBelowCap),
            ["starting_resolve.starting_resolve.formula"] = Is(CanonicalResolveRules.StartingResolveFormula),
            ["starting_resolve.starting_resolve.table_rows"] = ResolveTableIs(CanonicalResolveRules.StartingResolveTable),
            ["starting_resolve.starting_resolve.table_continues_beyond_the_printed_rows"] =
                Is(CanonicalResolveRules.StartingResolveTableContinues),

            ["resolve_exceptions.exceptions.talents_count"] = Is(CanonicalResolveRules.TalentsCountTowardsResolve),
            ["resolve_exceptions.exceptions.criterion"] = Is(CanonicalResolveRules.ResolveExemptCriterion),
            ["resolve_exceptions.exceptions.named_powers"] = Is(CanonicalResolveRules.NamedResolveExemptPowers),
            ["resolve_exceptions.exceptions.expertise_qualifier"] = Is(CanonicalResolveRules.ExpertiseQualifier),
            ["resolve_exceptions.exceptions.named_list_is_qualified_as_usual"] =
                Is(CanonicalResolveRules.NamedResolveExemptionsAreQualifiedAsUsual),
            ["resolve_exceptions.exceptions.gm_has_final_say"] =
                Is(CanonicalResolveRules.GmHasFinalSayOnResolveExemptions),

            ["boost_and_shapeshifting_count_at_maximum.maximum_possible_rank.applies_when"] =
                Is(CanonicalResolveRules.MaximumRankAppliesWhen),
            ["boost_and_shapeshifting_count_at_maximum.maximum_possible_rank.powers_named"] =
                Is(CanonicalResolveRules.RankRaisingPowersNamed),
            ["boost_and_shapeshifting_count_at_maximum.maximum_possible_rank.rank_used"] =
                Is(CanonicalResolveRules.RankUsedWhenAPowerCanRaiseIt),

            ["carryover.carryover.carries_over_between_issues"] =
                Is(CanonicalResolveRules.ResolveCarriesOverBetweenIssues),
            ["carryover.carryover.unspent_is_lost_at_issue_end"] =
                Is(CanonicalResolveRules.UnspentResolveIsLostAtIssueEnd),
            ["carryover.carryover.gm_may_allow_carryover"] = Is(CanonicalResolveRules.GmMayAllowResolveCarryover),
            ["carryover.carryover.gm_carryover_should_be_rare"] = Is(CanonicalResolveRules.ResolveCarryoverShouldBeRare),

            ["resolve_earning_overview.earning_overview.gm_may_award_whenever_they_see_fit"] =
                Is(CanonicalResolveRules.GmMayAwardResolveWheneverTheySeeFit),
            ["resolve_earning_overview.earning_overview.listed_ways_are_examples_not_a_closed_list"] =
                Is(CanonicalResolveRules.ListedWaysToEarnAreExamples),

            ["earn_defeat.earning.award_resolve"] = Is(CanonicalResolveRules.DefeatAward),
            ["earn_defeat.earning.trigger"] = Is(CanonicalResolveRules.DefeatTrigger),
            ["earn_defeat.earning.may_be_outside_combat"] = Is(CanonicalResolveRules.DefeatMayBeOutsideCombat),
            ["earn_defeat.earning.limit_per_battle"] = Is(CanonicalResolveRules.DefeatLimitPerBattle),

            ["earn_flaw.earning.award_resolve"] = Is(CanonicalResolveRules.FlawAward),
            ["earn_flaw.earning.trigger"] = Is(CanonicalResolveRules.FlawTrigger),
            ["earn_flaw.earning.must_fit_the_situation"] = Is(CanonicalResolveRules.FlawMustFitTheSituation),
            ["earn_flaw.earning.some_flaws_award_per_issue_instead"] =
                Is(CanonicalResolveRules.SomeFlawsAwardPerIssueInstead),

            ["earn_interlude.earning.award_stated"] = Is(CanonicalResolveRules.InterludeAwardIsStated),
            ["earn_interlude.earning.trigger"] = Is(CanonicalResolveRules.InterludeTrigger),
            ["earn_interlude.earning.interlude_is"] = Is(CanonicalResolveRules.InterludeIs),
            ["earn_interlude.earning.detail_chapter"] = Is(CanonicalResolveRules.InterludeDetailChapter),

            ["earn_motivation.earning.award_resolve"] = Is(CanonicalResolveRules.MotivationAward),
            ["earn_motivation.earning.trigger"] = Is(CanonicalResolveRules.MotivationTrigger),
            ["earn_motivation.earning.not_an_invitation_to_derail_the_game"] =
                Is(CanonicalResolveRules.MotivationIsNotAnInvitationToDerailTheGame),

            ["earn_roleplaying.earning.award_resolve"] = Is(CanonicalResolveRules.RoleplayingAward),
            ["earn_roleplaying.earning.trigger"] = Is(CanonicalResolveRules.RoleplayingTrigger),
            ["earn_roleplaying.earning.gm_judged"] = Is(CanonicalResolveRules.RoleplayingIsGmJudged),
            ["earn_roleplaying.earning.examples_given"] = Is(CanonicalResolveRules.RoleplayingExamples),

            ["earn_sacrifice.earning.award_resolve"] = Is(CanonicalResolveRules.SacrificeAward),
            ["earn_sacrifice.earning.available_when"] = Is(CanonicalResolveRules.SacrificeAvailableWhen),
            ["earn_sacrifice.earning.must_be_spent_on_the_same_page"] =
                Is(CanonicalResolveRules.SacrificeMustBeSpentOnTheSamePage),
            ["earn_sacrifice.earning.then_unconscious"] = Is(CanonicalResolveRules.SacrificeThenUnconscious),
            ["earn_sacrifice.earning.unconscious_until"] = Is(CanonicalResolveRules.SacrificeUnconsciousUntil),

            ["resolve_spending_overview.spending_overview.gm_may_expand_the_uses"] =
                Is(CanonicalResolveRules.GmMayExpandResolveUses),
            ["resolve_spending_overview.spending_overview.listed_uses_are_the_basic_ones"] =
                Is(CanonicalResolveRules.ListedResolveUsesAreTheBasicOnes),

            // Which pool each spend draws on. Registered per entry rather than derived, because a
            // rule that read the currency off the id would agree with a wrong id by construction —
            // which is the hole EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms had.
            ["spend_assisting_allies.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_challenge_roll_dice.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_reroll_challenge_roll.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_reroll_other_roll.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_combat.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_lucky_break.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_power_stunt.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["spend_using_powers.spend.currency"] = Is(CanonicalResolveRules.ResolveCurrency),
            ["adversity_spend_anything_resolve_can.spend.currency"] = Is(CanonicalResolveRules.AdversityCurrency),
            ["adversity_spend_suppress_flaw.spend.currency"] = Is(CanonicalResolveRules.AdversityCurrency),
            ["adversity_spend_misfortune.spend.currency"] = Is(CanonicalResolveRules.AdversityCurrency),
            ["adversity_spend_villainy.spend.currency"] = Is(CanonicalResolveRules.AdversityCurrency),

            ["spend_assisting_allies.spend.cost_per_point_shared_when_unable_to_assist"] =
                Is(CanonicalResolveRules.SharePointCostWhenUnableToAssist),
            ["spend_assisting_allies.spend.points_shared_limit"] = Is(CanonicalResolveRules.SharePointsLimit),
            ["spend_assisting_allies.spend.must_narrate_the_assistance"] =
                Is(CanonicalResolveRules.ShareMustNarrateTheAssistance),
            ["spend_assisting_allies.spend.narration_has_no_mechanical_effect"] =
                Is(CanonicalResolveRules.ShareNarrationHasNoMechanicalEffect),
            ["spend_assisting_allies.spend.unable_to_assist_examples"] =
                Is(CanonicalResolveRules.UnableToAssistExamples),
            ["spend_assisting_allies.spend.flashback_required_when_unable"] =
                Is(CanonicalResolveRules.ShareFlashbackRequiredWhenUnable),

            ["spend_challenge_roll_dice.spend.cost_resolve"] = Is(CanonicalResolveRules.ChallengeRollDiceCost),
            ["spend_challenge_roll_dice.spend.dice_gained"] = Is(CanonicalResolveRules.ChallengeRollDiceGained),
            ["spend_challenge_roll_dice.spend.unlimited"] = Is(CanonicalResolveRules.ChallengeRollDiceSpendIsUnlimited),
            ["spend_challenge_roll_dice.spend.decided_after_the_roll"] =
                Is(CanonicalResolveRules.ChallengeRollDiceDecidedAfterTheRoll),

            ["spend_reroll_challenge_roll.spend.cost_resolve"] = Is(CanonicalResolveRules.RerollChallengeRollCost),
            ["spend_reroll_challenge_roll.spend.rerolls"] = Is(CanonicalResolveRules.RerollChallengeRollCovers),
            ["spend_reroll_challenge_roll.spend.includes_dice_bought_with_resolve"] =
                Is(CanonicalResolveRules.RerollIncludesDiceBoughtWithResolve),

            ["spend_reroll_other_roll.spend.cost_resolve"] = Is(CanonicalResolveRules.RerollAnyOtherRollCost),
            ["spend_reroll_other_roll.spend.applies_to"] = Is(CanonicalResolveRules.RerollAnyOtherRollAppliesTo),
            ["spend_reroll_other_roll.spend.example_given"] = Is(CanonicalResolveRules.RerollAnyOtherRollExample),

            ["spend_combat.spend.transcribed_here"] = Is(CanonicalResolveRules.CombatSpendsAreTranscribedHere),
            ["spend_combat.spend.detail_chapter"] = Is(CanonicalResolveRules.CombatSpendsDetailChapter),
            ["spend_combat.spend.combat_spend_refs"] = Is(CanonicalResolveRules.CombatSpendRefs),

            ["spend_lucky_break.spend.cost_resolve"] = Is(CanonicalResolveRules.LuckyBreakCost),
            ["spend_lucky_break.spend.invents"] = Is(CanonicalResolveRules.LuckyBreakInvents),
            ["spend_lucky_break.spend.subject_to_gm_approval"] =
                Is(CanonicalResolveRules.LuckyBreakSubjectToGmApproval),
            ["spend_lucky_break.spend.example_given"] = Is(CanonicalResolveRules.LuckyBreakExample),

            ["spend_power_stunt.spend.cost_resolve"] = Is(CanonicalResolveRules.PowerStuntCost),
            ["spend_power_stunt.spend.uses"] = Is(CanonicalResolveRules.PowerStuntUses),
            ["spend_power_stunt.spend.imitated_power_rank_source"] = Is(CanonicalResolveRules.PowerStuntRankSource),
            ["spend_power_stunt.spend.requires_remotely_reasonable"] =
                Is(CanonicalResolveRules.PowerStuntRequiresRemotelyReasonable),
            ["spend_power_stunt.spend.grants_a_new_power"] = Is(CanonicalResolveRules.PowerStuntGrantsANewPower),

            ["spend_using_powers.spend.some_powers_require_resolve"] =
                Is(CanonicalResolveRules.SomePowersRequireResolve),
            ["spend_using_powers.spend.only_heroes_have_resolve"] = Is(CanonicalResolveRules.OnlyHeroesHaveResolve),
            ["spend_using_powers.spend.player_must_spend_for_a_friendly_extra"] =
                Is(CanonicalResolveRules.PlayerMustSpendForAFriendlyExtra),
            ["spend_using_powers.spend.extra_cannot_use_the_power_if_nobody_spends"] =
                Is(CanonicalResolveRules.ExtraCannotUseThePowerIfNobodySpends),
            ["spend_using_powers.spend.applies_only_while_the_extra_is_with_the_heroes"] =
                Is(CanonicalResolveRules.PowerResolveRuleAppliesOnlyWhileTheExtraIsWithTheHeroes),

            ["reroll_floor.reroll_floor.spending_should_never_make_things_worse"] =
                Is(CanonicalResolveRules.SpendingShouldNeverMakeThingsWorse),
            ["reroll_floor.reroll_floor.keep_the_first_roll_if_the_reroll_is_worse"] =
                Is(CanonicalResolveRules.KeepTheFirstRollIfTheRerollIsWorse),
            ["reroll_floor.reroll_floor.applies_to"] = Is(CanonicalResolveRules.RerollFloorAppliesTo),

            ["adversity_pool.adversity.points_per_hero_per_issue"] =
                Is(CanonicalResolveRules.AdversityPerHeroPerIssue),
            ["adversity_pool.adversity.held_by"] = Is(CanonicalResolveRules.AdversityHeldBy),
            ["adversity_pool.adversity.carries_over_between_issues"] =
                Is(CanonicalResolveRules.AdversityCarriesOverBetweenIssues),
            ["adversity_pool.adversity.is_more_of_a_fixed_resource_than_resolve"] =
                Is(CanonicalResolveRules.AdversityIsMoreOfAFixedResource),
            ["adversity_pool.adversity.gm_may_add_ways_to_earn"] =
                Is(CanonicalResolveRules.GmMayAddWaysToEarnAdversity),
            ["adversity_pool.adversity.should_not_be_as_easy_to_earn_as_resolve"] =
                Is(CanonicalResolveRules.AdversityShouldNotBeAsEasyToEarnAsResolve),

            ["adversity_earn_challenge_level.challenge_level.award_factors"] =
                Is(CanonicalResolveRules.ChallengeLevelAwardFactors),
            ["adversity_earn_challenge_level.challenge_level.award_operation"] =
                Is(CanonicalResolveRules.ChallengeLevelOperation),
            ["adversity_earn_challenge_level.challenge_level.awarded_at"] =
                Is(CanonicalResolveRules.ChallengeLevelAwardedAt),
            ["adversity_earn_challenge_level.challenge_level.typical_level_min"] =
                Is(CanonicalResolveRules.ChallengeLevelTypicalMin),
            ["adversity_earn_challenge_level.challenge_level.typical_level_max"] =
                Is(CanonicalResolveRules.ChallengeLevelTypicalMax),
            ["adversity_earn_challenge_level.challenge_level.level_three_may_be_exceeded"] =
                Is(CanonicalResolveRules.ChallengeLevelThreeMayBeExceeded),
            ["adversity_earn_challenge_level.challenge_level.only_a_handful_of_scenes_per_story"] =
                Is(CanonicalResolveRules.OnlyAHandfulOfScenesHaveAChallengeLevel),
            ["adversity_earn_challenge_level.challenge_level.may_be_saved_for_later_in_the_issue"] =
                Is(CanonicalResolveRules.ChallengeLevelAdversityMayBeSavedForLater),
            ["adversity_earn_challenge_level.challenge_level.level_guidance"] =
                GuidanceIs(CanonicalResolveRules.ChallengeLevelGuidanceRows),

            ["adversity_earn_unheroic_action.unheroic_action.award_adversity"] =
                Is(CanonicalResolveRules.UnheroicActionAward),
            ["adversity_earn_unheroic_action.unheroic_action.awarded_immediately"] =
                Is(CanonicalResolveRules.UnheroicActionAwardIsImmediate),
            ["adversity_earn_unheroic_action.unheroic_action.triggers"] =
                Is(CanonicalResolveRules.UnheroicActionTriggers),
            ["adversity_earn_unheroic_action.unheroic_action.also_when_contrary_to_motivation"] =
                Is(CanonicalResolveRules.UnheroicActionAlsoWhenContraryToMotivation),
            ["adversity_earn_unheroic_action.unheroic_action.applies_even_if_coerced"] =
                Is(CanonicalResolveRules.UnheroicActionAppliesEvenIfCoerced),
            ["adversity_earn_unheroic_action.unheroic_action.coercion_forms"] =
                Is(CanonicalResolveRules.UnheroicActionCoercionForms),

            ["adversity_spend_anything_resolve_can.spend.can_do_anything_resolve_can"] =
                Is(CanonicalResolveRules.AdversityCanDoAnythingResolveCan),
            ["adversity_spend_anything_resolve_can.spend.may_be_spent_on_any_npc"] =
                Is(CanonicalResolveRules.AdversityMayBeSpentOnAnyNpc),
            ["adversity_spend_anything_resolve_can.spend.npc_kinds"] = Is(CanonicalResolveRules.NpcKinds),
            ["adversity_spend_anything_resolve_can.spend.all_npcs_share_one_pool"] =
                Is(CanonicalResolveRules.AllNpcsShareOneAdversityPool),
            ["adversity_spend_anything_resolve_can.spend.exclusive_spends_count"] =
                Is(CanonicalResolveRules.AdversityExclusiveSpendCount),

            ["adversity_spend_suppress_flaw.spend.cost_adversity"] = Is(CanonicalResolveRules.SuppressFlawCost),
            ["adversity_spend_suppress_flaw.spend.prevents"] = Is(CanonicalResolveRules.SuppressFlawPrevents),
            ["adversity_spend_suppress_flaw.spend.duration"] = Is(CanonicalResolveRules.SuppressFlawDuration),
            ["adversity_spend_suppress_flaw.spend.eligible_characters"] =
                Is(CanonicalResolveRules.SuppressFlawEligible),
            ["adversity_spend_suppress_flaw.spend.limit_per_character_per_issue"] =
                Is(CanonicalResolveRules.SuppressFlawLimitPerCharacterPerIssue),
            ["adversity_spend_suppress_flaw.spend.npc_flaws_bite_when_the_opportunity_arises"] =
                Is(CanonicalResolveRules.NpcFlawsBiteWhenTheOpportunityArises),
            ["adversity_spend_suppress_flaw.spend.npcs_cannot_choose_when_their_flaws_bite"] =
                Is(CanonicalResolveRules.NpcsCannotChooseWhenTheirFlawsBite),

            ["adversity_spend_misfortune.spend.cost_adversity"] = Is(CanonicalResolveRules.MisfortuneCost),
            ["adversity_spend_misfortune.spend.what_it_is"] = Is(CanonicalResolveRules.MisfortuneIs),
            ["adversity_spend_misfortune.spend.examples_given"] = Is(CanonicalResolveRules.MisfortuneExamples),
            ["adversity_spend_misfortune.spend.must_be_a_challenge_not_a_punishment"] =
                Is(CanonicalResolveRules.MisfortuneMustBeAChallengeNotAPunishment),
            ["adversity_spend_misfortune.spend.must_not_be_a_plot_device"] =
                Is(CanonicalResolveRules.MisfortuneMustNotBeAPlotDevice),

            ["adversity_spend_villainy.spend.cost_adversity"] = Is(CanonicalResolveRules.VillainyCost),
            ["adversity_spend_villainy.spend.limit_per_story"] = Is(CanonicalResolveRules.VillainyLimitPerStory),
            ["adversity_spend_villainy.spend.automatic"] = Is(CanonicalResolveRules.VillainyIsAutomatic),
            ["adversity_spend_villainy.spend.effect"] = Is(CanonicalResolveRules.VillainyEffect),
            ["adversity_spend_villainy.spend.examples_given"] = Is(CanonicalResolveRules.VillainyExamples),
            ["adversity_spend_villainy.spend.eligible_characters"] = Is(CanonicalResolveRules.VillainyEligible),
            ["adversity_spend_villainy.spend.excluded_characters"] = Is(CanonicalResolveRules.VillainyExcluded),
            ["adversity_spend_villainy.spend.use_sparingly"] = Is(CanonicalResolveRules.VillainyUseSparingly),


            // combat.json
            ["pages_and_turns.page.a_page_is"] = Is(CanonicalCombatRules.Page.APageIs),
            ["pages_and_turns.page.turns_per_character_per_page"] = Is(CanonicalCombatRules.Page.TurnsPerCharacterPerPage),
            ["pages_and_turns.page.page_ends_when"] = Is(CanonicalCombatRules.Page.PageEndsWhen),

            ["edge_order.edge.formula"] = Is(CanonicalCombatRules.Edge.Formula),
            ["edge_order.edge.acts_in_order"] = Is(CanonicalCombatRules.Edge.ActsInOrder),
            ["edge_order.edge.optional_random_initiative"] = Is(CanonicalCombatRules.Edge.OptionalRandomInitiative),
            ["edge_order.edge.random_initiative_effective_edge"] = Is(CanonicalCombatRules.Edge.RandomInitiativeEffectiveEdge),
            ["edge_order.edge.random_initiative_lasts"] = Is(CanonicalCombatRules.Edge.RandomInitiativeLasts),

            ["edge_ties.tie_break.order"] = Is(CanonicalCombatRules.TieBreak.Order),
            ["edge_ties.tie_break.still_tied_act"] = Is(CanonicalCombatRules.TieBreak.StillTiedAct),
            ["edge_ties.tie_break.simultaneous_characters_can_knock_each_other_out"] = Is(CanonicalCombatRules.TieBreak.SimultaneousCharactersCanKnockEachOtherOut),
            ["edge_ties.tie_break.minions_have_an_edge"] = Is(CanonicalCombatRules.TieBreak.MinionsHaveAnEdge),
            ["edge_ties.tie_break.minions_act"] = Is(CanonicalCombatRules.TieBreak.MinionsAct),
            ["edge_ties.tie_break.minion_allies_and_enemies_act"] = Is(CanonicalCombatRules.TieBreak.MinionAlliesAndEnemiesAct),

            ["holding_an_action.holding.may_hold_in_reserve"] = Is(CanonicalCombatRules.Holding.MayHoldInReserve),
            ["holding_an_action.holding.waiting_for"] = Is(CanonicalCombatRules.Holding.WaitingFor),
            ["holding_an_action.holding.if_it_never_happens"] = Is(CanonicalCombatRules.Holding.IfItNeverHappens),
            ["holding_an_action.holding.order_among_holders"] = Is(CanonicalCombatRules.Holding.OrderAmongHolders),

            ["seizing_initiative.seize_initiative.cost_resolve"] = Is(CanonicalCombatRules.SeizeInitiative.CostResolve),
            ["seizing_initiative.seize_initiative.effect"] = Is(CanonicalCombatRules.SeizeInitiative.Effect),
            ["seizing_initiative.seize_initiative.duration"] = Is(CanonicalCombatRules.SeizeInitiative.Duration),
            ["seizing_initiative.seize_initiative.seizers_go_before_everyone_else"] = Is(CanonicalCombatRules.SeizeInitiative.SeizersGoBeforeEveryoneElse),
            ["seizing_initiative.seize_initiative.order_among_seizers"] = Is(CanonicalCombatRules.SeizeInitiative.OrderAmongSeizers),

            ["seize_initiative_gm_alternative.gm_alternative.instead_of"] = Is(CanonicalCombatRules.SeizeInitiativeGmAlternative.InsteadOf),
            ["seize_initiative_gm_alternative.gm_alternative.effect"] = Is(CanonicalCombatRules.SeizeInitiativeGmAlternative.Effect),
            ["seize_initiative_gm_alternative.gm_alternative.chosen_by"] = Is(CanonicalCombatRules.SeizeInitiativeGmAlternative.ChosenBy),
            ["seize_initiative_gm_alternative.gm_alternative.rationale"] = Is(CanonicalCombatRules.SeizeInitiativeGmAlternative.Rationale),

            ["actions.actions.on_your_turn"] = Is(CanonicalCombatRules.Actions.OnYourTurn),
            ["actions.actions.an_action_is"] = Is(CanonicalCombatRules.Actions.AnActionIs),
            ["actions.actions.attacks_are_the_commonest_action"] = Is(CanonicalCombatRules.Actions.AttacksAreTheCommonestAction),
            ["actions.actions.defending_yourself_is_available"] = Is(CanonicalCombatRules.Actions.DefendingYourselfIsAvailable),
            ["actions.actions.free_actions_allowed"] = Is(CanonicalCombatRules.Actions.FreeActionsAllowed),
            ["actions.actions.free_action_examples"] = Is(CanonicalCombatRules.Actions.FreeActionExamples),

            ["multiple_actions.multiple_actions.penalty_dice_per_extra_action"] = Is(CanonicalCombatRules.MultipleActions.PenaltyDicePerExtraAction),
            ["multiple_actions.multiple_actions.applies_to"] = Is(CanonicalCombatRules.MultipleActions.AppliesTo),
            ["multiple_actions.multiple_actions.must_be_declared_before_any_challenge_roll"] = Is(CanonicalCombatRules.MultipleActions.MustBeDeclaredBeforeAnyChallengeRoll),
            ["multiple_actions.multiple_actions.applies_to_defense_rolls"] = Is(CanonicalCombatRules.MultipleActions.AppliesToDefenseRolls),
            ["multiple_actions.multiple_actions.applies_to_other_challenge_rolls"] = Is(CanonicalCombatRules.MultipleActions.AppliesToOtherChallengeRolls),
            ["multiple_actions.multiple_actions.same_target_more_than_once_per_page"] = Is(CanonicalCombatRules.MultipleActions.SameTargetMoreThanOncePerPage),
            ["multiple_actions.multiple_actions.extra_actions_buy_extra_movement"] = Is(CanonicalCombatRules.MultipleActions.ExtraActionsBuyExtraMovement),

            ["range_classes.ranges"] = RangeClassesAre(CanonicalCombatRules.Ranges),
            ["range_classes.range_rules.measured_precisely"] = Is(CanonicalCombatRules.RangeRules.MeasuredPrecisely),
            ["range_classes.range_rules.initial_range_class_set_by"] = Is(CanonicalCombatRules.RangeRules.InitialRangeClassSetBy),
            ["range_classes.range_rules.close_combat_attacks_require"] = Is(CanonicalCombatRules.RangeRules.CloseCombatAttacksRequire),
            ["range_classes.range_rules.close_combat_attacks_reach"] = Is(CanonicalCombatRules.RangeRules.CloseCombatAttacksReach),
            ["range_classes.range_rules.ranged_attacks_reach"] = Is(CanonicalCombatRules.RangeRules.RangedAttacksReach),
            ["range_classes.range_rules.exceptions_given"] = Is(CanonicalCombatRules.RangeRules.ExceptionsGiven),

            ["range_distance_estimates.estimates.close_feet"] = Is(CanonicalCombatRules.RangeEstimates.CloseFeet),
            ["range_distance_estimates.estimates.distant_feet"] = Is(CanonicalCombatRules.RangeEstimates.DistantFeet),
            ["range_distance_estimates.estimates.extreme_feet"] = Is(CanonicalCombatRules.RangeEstimates.ExtremeFeet),
            ["range_distance_estimates.estimates.stated_as"] = Is(CanonicalCombatRules.RangeEstimates.StatedAs),

            ["throwing_range.throwing.ordinary_people_reach"] = Is(CanonicalCombatRules.Throwing.OrdinaryPeopleReach),
            ["throwing_range.throwing.table_used_when_might_exceeds"] = Is(CanonicalCombatRules.Throwing.TableUsedWhenMightExceeds),
            ["throwing_range.throwing.rank_formula"] = Is(CanonicalCombatRules.Throwing.RankFormula),
            ["throwing_range.throwing.minimum_rank"] = Is(CanonicalCombatRules.Throwing.MinimumRank),
            ["throwing_range.throwing.accuracy_is_what_is_measured"] = Is(CanonicalCombatRules.Throwing.AccuracyIsWhatIsMeasured),

            ["throwing_table.throwing_table"] = ThrowingRowsAre(CanonicalCombatRules.ThrowingTable),

            ["movement.movement.pages_to_close_or_open_within_close_range"] = Is(CanonicalCombatRules.Movement.PagesToCloseOrOpenWithinCloseRange),
            ["movement.movement.pages_per_range_class"] = Is(CanonicalCombatRules.Movement.PagesPerRangeClass),
            ["movement.movement.pages_per_range_class_with_a_travel_power"] = Is(CanonicalCombatRules.Movement.PagesPerRangeClassWithATravelPower),
            ["movement.movement.travel_power_rank_required"] = Is(CanonicalCombatRules.Movement.TravelPowerRankRequired),
            ["movement.movement.moving_prevents_actions"] = Is(CanonicalCombatRules.Movement.MovingPreventsActions),
            ["movement.movement.assumed_terrain"] = Is(CanonicalCombatRules.Movement.AssumedTerrain),
            ["movement.movement.open_terrain_gm_may_allow_range_classes_per_page"] = Is(CanonicalCombatRules.Movement.OpenTerrainGmMayAllowRangeClassesPerPage),

            ["movement_contest.movement_contest.trigger"] = Is(CanonicalCombatRules.MovementContest.Trigger),
            ["movement_contest.movement_contest.roll"] = Is(CanonicalCombatRules.MovementContest.Roll),
            ["movement_contest.movement_contest.on_foot_against_a_travel_power_uses"] = Is(CanonicalCombatRules.MovementContest.OnFootAgainstATravelPowerUses),
            ["movement_contest.movement_contest.winner_gets"] = Is(CanonicalCombatRules.MovementContest.WinnerGets),

            ["chases.chase.structure"] = Is(CanonicalCombatRules.Chase.Structure),
            ["chases.chase.pages_per_exchange"] = Is(CanonicalCombatRules.Chase.PagesPerExchange),
            ["chases.chase.roll"] = Is(CanonicalCombatRules.Chase.Roll),
            ["chases.chase.on_foot_against_a_travel_power_uses"] = Is(CanonicalCombatRules.Chase.OnFootAgainstATravelPowerUses),
            ["chases.chase.the_roll_is_an_action"] = Is(CanonicalCombatRules.Chase.TheRollIsAnAction),
            ["chases.chase.each_pursuer_picks_one_quarry"] = Is(CanonicalCombatRules.Chase.EachPursuerPicksOneQuarry),
            ["chases.chase.exchange_win_bonus_dice_next_exchange"] = Is(CanonicalCombatRules.Chase.ExchangeWinBonusDiceNextExchange),
            ["chases.chase.net_successes_to_move_one_range_class"] = Is(CanonicalCombatRules.Chase.NetSuccessesToMoveOneRangeClass),
            ["chases.chase.ends_closer_than"] = Is(CanonicalCombatRules.Chase.EndsCloserThan),
            ["chases.chase.ends_farther_than"] = Is(CanonicalCombatRules.Chase.EndsFartherThan),
            ["chases.chase.at_closer_than_close_range"] = Is(CanonicalCombatRules.Chase.AtCloserThanCloseRange),
            ["chases.chase.at_farther_than_extreme_range"] = Is(CanonicalCombatRules.Chase.AtFartherThanExtremeRange),

            ["attacks_and_defenses.attack.roll"] = Is(CanonicalCombatRules.Attack.Roll),
            ["attacks_and_defenses.attack.threshold_source"] = Is(CanonicalCombatRules.Attack.ThresholdSource),
            ["attacks_and_defenses.attack.on_more_successes_than_the_target"] = Is(CanonicalCombatRules.Attack.OnMoreSuccessesThanTheTarget),
            ["attacks_and_defenses.attack.on_failing_the_threshold"] = Is(CanonicalCombatRules.Attack.OnFailingTheThreshold),
            ["attacks_and_defenses.attack.accuracy_and_damage_are_one_trait"] = Is(CanonicalCombatRules.Attack.AccuracyAndDamageAreOneTrait),
            ["attacks_and_defenses.attack.defense_and_damage_resistance_are_one_trait"] = Is(CanonicalCombatRules.Attack.DefenseAndDamageResistanceAreOneTrait),
            ["attacks_and_defenses.attack.defender_uses"] = Is(CanonicalCombatRules.Attack.DefenderUses),

            ["attack_and_defense_table.attack_defense_table"] = AttackDefenseRowsAre(CanonicalCombatRules.AttackDefenseTable),

            ["active_and_passive_defenses.defenses.active_represent"] = Is(CanonicalCombatRules.Defenses.ActiveRepresent),
            ["active_and_passive_defenses.defenses.passive_represent"] = Is(CanonicalCombatRules.Defenses.PassiveRepresent),
            ["active_and_passive_defenses.defenses.common_active_traits"] = Is(CanonicalCombatRules.Defenses.CommonActiveTraits),
            ["active_and_passive_defenses.defenses.common_passive_traits"] = Is(CanonicalCombatRules.Defenses.CommonPassiveTraits),
            ["active_and_passive_defenses.defenses.active_unusable_when"] = Is(CanonicalCombatRules.Defenses.ActiveUnusableWhen),
            ["active_and_passive_defenses.defenses.a_cramped_or_awkward_position_prevents_active_defenses"] = Is(CanonicalCombatRules.Defenses.ACrampedOrAwkwardPositionPreventsActiveDefenses),
            ["active_and_passive_defenses.defenses.losing_your_next_turn_prevents_active_defenses"] = Is(CanonicalCombatRules.Defenses.LosingYourNextTurnPreventsActiveDefenses),
            ["active_and_passive_defenses.defenses.defenses_used_per_attack"] = Is(CanonicalCombatRules.Defenses.DefensesUsedPerAttack),
            ["active_and_passive_defenses.defenses.defense_chosen"] = Is(CanonicalCombatRules.Defenses.DefenseChosen),

            ["lethal_and_subdual.damage_types.lethal_is_the_more_dangerous"] = Is(CanonicalCombatRules.DamageTypes.LethalIsTheMoreDangerous),
            ["lethal_and_subdual.damage_types.toughness_against_lethal"] = Is(CanonicalCombatRules.DamageTypes.ToughnessAgainstLethal),
            ["lethal_and_subdual.damage_types.toughness_against_subdual"] = Is(CanonicalCombatRules.DamageTypes.ToughnessAgainstSubdual),
            ["lethal_and_subdual.damage_types.subdual_sources_given"] = Is(CanonicalCombatRules.DamageTypes.SubdualSourcesGiven),
            ["lethal_and_subdual.damage_types.physical_damage_default"] = Is(CanonicalCombatRules.DamageTypes.PhysicalDamageDefault),
            ["lethal_and_subdual.damage_types.psychic_damage_is"] = Is(CanonicalCombatRules.DamageTypes.PsychicDamageIs),
            ["lethal_and_subdual.damage_types.psychic_damage_resisted_with"] = Is(CanonicalCombatRules.DamageTypes.PsychicDamageResistedWith),

            ["modifier_cover.cover.affects"] = Is(CanonicalCombatRules.Cover.Affects),
            ["modifier_cover.cover.bands"] = ModifierBandsAre(CanonicalCombatRules.Cover.Bands),
            ["modifier_cover.cover.a_completely_hidden_target_cannot_be_hit"] = Is(CanonicalCombatRules.Cover.ACompletelyHiddenTargetCannotBeHit),
            ["modifier_cover.cover.attacking_through_cover_requires"] = Is(CanonicalCombatRules.Cover.AttackingThroughCoverRequires),
            ["modifier_cover.cover.target_may_use_the_covers_structure_as_a_passive_defense"] = Is(CanonicalCombatRules.Cover.TargetMayUseTheCoversStructureAsAPassiveDefense),

            ["modifier_size.size.affects"] = Is(CanonicalCombatRules.Size.Affects),
            ["modifier_size.size.bands"] = ModifierBandsAre(CanonicalCombatRules.Size.Bands),

            ["modifier_visibility.visibility.affects"] = Is(CanonicalCombatRules.Visibility.Affects),
            ["modifier_visibility.visibility.bands"] = ModifierBandsAre(CanonicalCombatRules.Visibility.Bands),
            ["modifier_visibility.visibility.poor_examples"] = Is(CanonicalCombatRules.Visibility.PoorExamples),
            ["modifier_visibility.visibility.none_examples"] = Is(CanonicalCombatRules.Visibility.NoneExamples),
            ["modifier_visibility.visibility.an_invisible_opponent_counts_as_no_visibility"] = Is(CanonicalCombatRules.Visibility.AnInvisibleOpponentCountsAsNoVisibility),
            ["modifier_visibility.visibility.powers_that_compensate_given"] = Is(CanonicalCombatRules.Visibility.PowersThatCompensateGiven),

            ["damage.damage.damage_per_net_success"] = Is(CanonicalCombatRules.Damage.DamagePerNetSuccess),
            ["damage.damage.reduces"] = Is(CanonicalCombatRules.Damage.Reduces),
            ["damage.damage.defeated_at_health"] = Is(CanonicalCombatRules.Damage.DefeatedAtHealth),
            ["damage.damage.defeated_means"] = Is(CanonicalCombatRules.Damage.DefeatedMeans),
            ["damage.damage.death_only_under_the_gritty_combat_rules"] = Is(CanonicalCombatRules.Damage.DeathOnlyUnderTheGrittyCombatRules),

            ["health.health.formula"] = Is(CanonicalCombatRules.Health.Formula),
            ["health.health.villains_use_the_same_formula"] = Is(CanonicalCombatRules.Health.VillainsUseTheSameFormula),
            ["health.health.foes_halve_the_result"] = Is(CanonicalCombatRules.Health.FoesHalveTheResult),
            ["health.health.npc_totals_are_suggestions"] = Is(CanonicalCombatRules.Health.NpcTotalsAreSuggestions),
            ["health.health.minions_use_health"] = Is(CanonicalCombatRules.Health.MinionsUseHealth),

            ["npc_health.reference.transcribed_here"] = Is(CanonicalCombatRules.NpcHealth.TranscribedHere),
            ["npc_health.reference.detail_chapter"] = Is(CanonicalCombatRules.NpcHealth.DetailChapter),
            ["npc_health.reference.deferred_topics"] = Is(CanonicalCombatRules.NpcHealth.DeferredTopics),

            ["healing.healing.roll"] = Is(CanonicalCombatRules.Healing.Roll),
            ["healing.healing.after_a_fight_difficulty"] = Is(CanonicalCombatRules.Healing.AfterAFightDifficulty),
            ["healing.healing.after_a_fight_threshold"] = Is(CanonicalCombatRules.Healing.AfterAFightThreshold),
            ["healing.healing.health_per_net_success"] = Is(CanonicalCombatRules.Healing.HealthPerNetSuccess),
            ["healing.healing.also_available_after"] = Is(CanonicalCombatRules.Healing.AlsoAvailableAfter),
            ["healing.healing.full_rest_hours"] = Is(CanonicalCombatRules.Healing.FullRestHours),
            ["healing.healing.full_rest_difficulty"] = Is(CanonicalCombatRules.Healing.FullRestDifficulty),
            ["healing.healing.full_rest_threshold"] = Is(CanonicalCombatRules.Healing.FullRestThreshold),
            ["healing.healing.removed_npcs_recover"] = Is(CanonicalCombatRules.Healing.RemovedNpcsRecover),

            ["special_effects.special_effect.sources_given"] = Is(CanonicalCombatRules.SpecialEffect.SourcesGiven),
            ["special_effects.special_effect.duration_formula"] = Is(CanonicalCombatRules.SpecialEffect.DurationFormula),
            ["special_effects.special_effect.expires_at"] = Is(CanonicalCombatRules.SpecialEffect.ExpiresAt),
            ["special_effects.special_effect.duration_stacks_by_attacking_the_same_target_again"] = Is(CanonicalCombatRules.SpecialEffect.DurationStacksByAttackingTheSameTargetAgain),
            ["special_effects.special_effect.defeated_when_the_duration_reaches"] = Is(CanonicalCombatRules.SpecialEffect.DefeatedWhenTheDurationReaches),
            ["special_effects.special_effect.defeat_by_effect_lasts"] = Is(CanonicalCombatRules.SpecialEffect.DefeatByEffectLasts),

            ["breaking_free.breaking_free.available_when"] = Is(CanonicalCombatRules.BreakingFree.AvailableWhen),
            ["breaking_free.breaking_free.taken_on"] = Is(CanonicalCombatRules.BreakingFree.TakenOn),
            ["breaking_free.breaking_free.roll"] = Is(CanonicalCombatRules.BreakingFree.Roll),
            ["breaking_free.breaking_free.roll_examples_given"] = Is(CanonicalCombatRules.BreakingFree.RollExamplesGiven),
            ["breaking_free.breaking_free.threshold_source"] = Is(CanonicalCombatRules.BreakingFree.ThresholdSource),
            ["breaking_free.breaking_free.duration_reduced_by"] = Is(CanonicalCombatRules.BreakingFree.DurationReducedBy),
            ["breaking_free.breaking_free.free_when_the_duration_reaches"] = Is(CanonicalCombatRules.BreakingFree.FreeWhenTheDurationReaches),
            ["breaking_free.breaking_free.may_act_on_the_same_page_when_freed"] = Is(CanonicalCombatRules.BreakingFree.MayActOnTheSamePageWhenFreed),

            ["keeping_hold.keeping_hold.trigger"] = Is(CanonicalCombatRules.KeepingHold.Trigger),
            ["keeping_hold.keeping_hold.cost_resolve"] = Is(CanonicalCombatRules.KeepingHold.CostResolve),
            ["keeping_hold.keeping_hold.extends_to"] = Is(CanonicalCombatRules.KeepingHold.ExtendsTo),
            ["keeping_hold.keeping_hold.may_be_repeated_scene_after_scene"] = Is(CanonicalCombatRules.KeepingHold.MayBeRepeatedSceneAfterScene),

            ["instant_recovery.instant_recovery.cost_resolve"] = Is(CanonicalCombatRules.InstantRecovery.CostResolve),
            ["instant_recovery.instant_recovery.taken_on"] = Is(CanonicalCombatRules.InstantRecovery.TakenOn),
            ["instant_recovery.instant_recovery.after_a_damaging_defeat_regains_consciousness"] = Is(CanonicalCombatRules.InstantRecovery.AfterADamagingDefeatRegainsConsciousness),
            ["instant_recovery.instant_recovery.after_a_damaging_defeat_restores_health"] = Is(CanonicalCombatRules.InstantRecovery.AfterADamagingDefeatRestoresHealth),
            ["instant_recovery.instant_recovery.also_frees_you_from_a_special_effect"] = Is(CanonicalCombatRules.InstantRecovery.AlsoFreesYouFromASpecialEffect),
            ["instant_recovery.instant_recovery.requires_being_defeated_to_free_yourself_from_an_effect"] = Is(CanonicalCombatRules.InstantRecovery.RequiresBeingDefeatedToFreeYourselfFromAnEffect),
            ["instant_recovery.instant_recovery.limit_per_scene"] = Is(CanonicalCombatRules.InstantRecovery.LimitPerScene),

            ["grappling.grappling.a_grab_is"] = Is(CanonicalCombatRules.Grappling.AGrabIs),
            ["grappling.grappling.a_hold_is"] = Is(CanonicalCombatRules.Grappling.AHoldIs),
            ["grappling.grappling.an_escape_is"] = Is(CanonicalCombatRules.Grappling.AnEscapeIs),
            ["grappling.grappling.roll"] = Is(CanonicalCombatRules.Grappling.Roll),
            ["grappling.grappling.threshold_source"] = Is(CanonicalCombatRules.Grappling.ThresholdSource),
            ["grappling.grappling.opponent_may_use_an_active_defense_instead_when_not_already_grappling"] = Is(CanonicalCombatRules.Grappling.OpponentMayUseAnActiveDefenseInsteadWhenNotAlreadyGrappling),
            ["grappling.grappling.inflicting_ordinary_damage_in_close_combat_needs_no_special_rules"] = Is(CanonicalCombatRules.Grappling.InflictingOrdinaryDamageInCloseCombatNeedsNoSpecialRules),

            ["grappling_table.grappling_table"] = GrapplingRowsAre(CanonicalCombatRules.GrapplingTable),

            ["grab.grab.partial_means"] = Is(CanonicalCombatRules.Grab.PartialMeans),
            ["grab.grab.partial_blocks_active_defenses_against_anyone_else"] = Is(CanonicalCombatRules.Grab.PartialBlocksActiveDefensesAgainstAnyoneElse),
            ["grab.grab.partial_resolved_by"] = Is(CanonicalCombatRules.Grab.PartialResolvedBy),
            ["grab.grab.may_exit_by_letting_go_of_the_object"] = Is(CanonicalCombatRules.Grab.MayExitByLettingGoOfTheObject),
            ["grab.grab.full_means"] = Is(CanonicalCombatRules.Grab.FullMeans),
            ["grab.grab.full_allows_using_or_tossing_it_the_same_page"] = Is(CanonicalCombatRules.Grab.FullAllowsUsingOrTossingItTheSamePage),
            ["grab.grab.full_suffers_the_multiple_action_penalty"] = Is(CanonicalCombatRules.Grab.FullSuffersTheMultipleActionPenalty),
            ["grab.grab.full_is_in_effect_a_free_action"] = Is(CanonicalCombatRules.Grab.FullIsInEffectAFreeAction),

            ["hold.hold.partial_means"] = Is(CanonicalCombatRules.Hold.PartialMeans),
            ["hold.hold.partial_blocks_active_defenses_against_anyone_else"] = Is(CanonicalCombatRules.Hold.PartialBlocksActiveDefensesAgainstAnyoneElse),
            ["hold.hold.partial_only_physical_action"] = Is(CanonicalCombatRules.Hold.PartialOnlyPhysicalAction),
            ["hold.hold.full_means"] = Is(CanonicalCombatRules.Hold.FullMeans),
            ["hold.hold.full_leaves_the_held_character_only"] = Is(CanonicalCombatRules.Hold.FullLeavesTheHeldCharacterOnly),
            ["hold.hold.full_allows_attacks_on_subsequent_pages"] = Is(CanonicalCombatRules.Hold.FullAllowsAttacksOnSubsequentPages),
            ["hold.hold.full_damage_roll"] = Is(CanonicalCombatRules.Hold.FullDamageRoll),
            ["hold.hold.full_damage_threshold_source"] = Is(CanonicalCombatRules.Hold.FullDamageThresholdSource),
            ["hold.hold.full_submission_roll"] = Is(CanonicalCombatRules.Hold.FullSubmissionRoll),
            ["hold.hold.full_submission_threshold_source"] = Is(CanonicalCombatRules.Hold.FullSubmissionThresholdSource),
            ["hold.hold.a_held_character_may_use"] = Is(CanonicalCombatRules.Hold.AHeldCharacterMayUse),
            ["hold.hold.powers_probably_unavailable_when_held"] = Is(CanonicalCombatRules.Hold.PowersProbablyUnavailableWhenHeld),
            ["hold.hold.adjudicated_case_by_case_by"] = Is(CanonicalCombatRules.Hold.AdjudicatedCaseByCaseBy),

            ["escape.escape.partial_out_of_a_partial_hold"] = Is(CanonicalCombatRules.Escape.PartialOutOfAPartialHold),
            ["escape.escape.partial_out_of_a_full_hold"] = Is(CanonicalCombatRules.Escape.PartialOutOfAFullHold),
            ["escape.escape.full"] = Is(CanonicalCombatRules.Escape.Full),
            ["escape.escape.may_also_exit_grappling_completely"] = Is(CanonicalCombatRules.Escape.MayAlsoExitGrapplingCompletely),

            ["combat_stunts.combat_stunt.what_it_is"] = Is(CanonicalCombatRules.CombatStunt.WhatItIs),
            ["combat_stunts.combat_stunt.works_like"] = Is(CanonicalCombatRules.CombatStunt.WorksLike),
            ["combat_stunts.combat_stunt.traits_used_are_chosen_by"] = Is(CanonicalCombatRules.CombatStunt.TraitsUsedAreChosenBy),
            ["combat_stunts.combat_stunt.effect_described_by"] = Is(CanonicalCombatRules.CombatStunt.EffectDescribedBy),
            ["combat_stunts.combat_stunt.duration"] = Is(CanonicalCombatRules.CombatStunt.Duration),
            ["combat_stunts.combat_stunt.delaying_your_next_action_extends_it"] = Is(CanonicalCombatRules.CombatStunt.DelayingYourNextActionExtendsIt),
            ["combat_stunts.combat_stunt.penalties_from_multiple_stunts_are_cumulative"] = Is(CanonicalCombatRules.CombatStunt.PenaltiesFromMultipleStuntsAreCumulative),
            ["combat_stunts.sample_stunts"] = SampleStuntsAre(CanonicalCombatRules.SampleStunts),

            ["combat_stunts_table.combat_stunt_bands"] = StuntBandsAre(CanonicalCombatRules.CombatStuntBands),

            ["minions_in_combat.minions.only_characteristic"] = Is(CanonicalCombatRules.Minions.OnlyCharacteristic),
            ["minions_in_combat.minions.act_in_groups_rather_than_as_individuals"] = Is(CanonicalCombatRules.Minions.ActInGroupsRatherThanAsIndividuals),

            ["threat_ranks.threat_ranks"] = ThreatRowsAre(CanonicalCombatRules.ThreatRanks),

            ["attacking_minions.attacking_minions.minions_have_health"] = Is(CanonicalCombatRules.AttackingMinions.MinionsHaveHealth),
            ["attacking_minions.attacking_minions.minions_defeated_per_net_success"] = Is(CanonicalCombatRules.AttackingMinions.MinionsDefeatedPerNetSuccess),
            ["attacking_minions.attacking_minions.minions_defeated_per_net_success_with_an_area_attack"] = Is(CanonicalCombatRules.AttackingMinions.MinionsDefeatedPerNetSuccessWithAnAreaAttack),
            ["attacking_minions.attacking_minions.capped_by"] = Is(CanonicalCombatRules.AttackingMinions.CappedBy),
            ["attacking_minions.attacking_minions.maximum_minions_per_net_success"] = Is(CanonicalCombatRules.AttackingMinions.MaximumMinionsPerNetSuccess),
            ["attacking_minions.attacking_minions.effects_that_double_the_rate_do_not_stack"] = Is(CanonicalCombatRules.AttackingMinions.EffectsThatDoubleTheRateDoNotStack),
            ["attacking_minions.attacking_minions.on_a_damaging_attack"] = Is(CanonicalCombatRules.AttackingMinions.OnADamagingAttack),
            ["attacking_minions.attacking_minions.on_a_special_effect"] = Is(CanonicalCombatRules.AttackingMinions.OnASpecialEffect),

            ["minions_attacking.minions_attacking.a_group_acts_like"] = Is(CanonicalCombatRules.MinionsAttacking.AGroupActsLike),
            ["minions_attacking.minions_attacking.a_group_may_split_to_attack_multiple_enemies"] = Is(CanonicalCombatRules.MinionsAttacking.AGroupMaySplitToAttackMultipleEnemies),
            ["minions_attacking.minions_attacking.targets_per_group_per_page"] = Is(CanonicalCombatRules.MinionsAttacking.TargetsPerGroupPerPage),
            ["minions_attacking.minions_attacking.attack_rolls_per_group_per_page"] = Is(CanonicalCombatRules.MinionsAttacking.AttackRollsPerGroupPerPage),
            ["minions_attacking.minions_attacking.defense_rolls_opposing_it"] = Is(CanonicalCombatRules.MinionsAttacking.DefenseRollsOpposingIt),
            ["minions_attacking.minions_attacking.the_group_bonus_applies_to"] = Is(CanonicalCombatRules.MinionsAttacking.TheGroupBonusAppliesTo),
            ["minions_attacking.minions_attacking.the_group_bonus_does_not_apply_to"] = Is(CanonicalCombatRules.MinionsAttacking.TheGroupBonusDoesNotApplyTo),
            ["minions_attacking.minions_attacking.maximum_attacking_one_target_in_close_combat"] = Is(CanonicalCombatRules.MinionsAttacking.MaximumAttackingOneTargetInCloseCombat),
            ["minions_attacking.minions_attacking.maximum_attacking_one_target_at_range"] = Is(CanonicalCombatRules.MinionsAttacking.MaximumAttackingOneTargetAtRange),

            ["minion_group_attack_table.minion_group_attack"] = MinionGroupRowsAre(CanonicalCombatRules.MinionGroupAttack),

            ["ambushes.ambush.roll"] = Is(CanonicalCombatRules.Ambush.Roll),
            ["ambushes.ambush.deception_or_seduction_roll"] = Is(CanonicalCombatRules.Ambush.DeceptionOrSeductionRoll),
            ["ambushes.ambush.threshold_source"] = Is(CanonicalCombatRules.Ambush.ThresholdSource),
            ["ambushes.ambush.on_success"] = Is(CanonicalCombatRules.Ambush.OnSuccess),
            ["ambushes.ambush.a_surprised_target_can_act"] = Is(CanonicalCombatRules.Ambush.ASurprisedTargetCanAct),
            ["ambushes.ambush.a_surprised_target_can_use_active_defenses"] = Is(CanonicalCombatRules.Ambush.ASurprisedTargetCanUseActiveDefenses),
            ["ambushes.ambush.surprise_lasts"] = Is(CanonicalCombatRules.Ambush.SurpriseLasts),
            ["ambushes.ambush.embellishment_rights_allow_partial_surprise"] = Is(CanonicalCombatRules.Ambush.EmbellishmentRightsAllowPartialSurprise),
            ["ambushes.ambush.partial_surprise_keeps"] = Is(CanonicalCombatRules.Ambush.PartialSurpriseKeeps),
            ["ambushes.ambush.on_failure"] = Is(CanonicalCombatRules.Ambush.OnFailure),
            ["ambushes.ambush.multiple_ambushers_may_roll_as_a_group"] = Is(CanonicalCombatRules.Ambush.MultipleAmbushersMayRollAsAGroup),
            ["ambushes.ambush.every_target_rolls_their_own_perception"] = Is(CanonicalCombatRules.Ambush.EveryTargetRollsTheirOwnPerception),
            ["ambushes.ambush.minions_roll_perception_in_groups"] = Is(CanonicalCombatRules.Ambush.MinionsRollPerceptionInGroups),

            ["area_attacks.area_attack.targets"] = Is(CanonicalCombatRules.AreaAttack.Targets),
            ["area_attacks.area_attack.attack_rolls"] = Is(CanonicalCombatRules.AreaAttack.AttackRolls),
            ["area_attacks.area_attack.defense_rolls"] = Is(CanonicalCombatRules.AreaAttack.DefenseRolls),
            ["area_attacks.area_attack.an_active_defense_must_either"] = Is(CanonicalCombatRules.AreaAttack.AnActiveDefenseMustEither),
            ["area_attacks.area_attack.examples_given"] = Is(CanonicalCombatRules.AreaAttack.ExamplesGiven),

            ["charge_attacks.charge.what_it_is"] = Is(CanonicalCombatRules.Charge.WhatItIs),
            ["charge_attacks.charge.attack_traits"] = Is(CanonicalCombatRules.Charge.AttackTraits),
            ["charge_attacks.charge.swimming_may_be_used_only_underwater"] = Is(CanonicalCombatRules.Charge.SwimmingMayBeUsedOnlyUnderwater),
            ["charge_attacks.charge.attack_bonus_dice"] = Is(CanonicalCombatRules.Charge.AttackBonusDice),
            ["charge_attacks.charge.own_active_defense_ranks"] = Is(CanonicalCombatRules.Charge.OwnActiveDefenseRanks),
            ["charge_attacks.charge.penalty_lasts"] = Is(CanonicalCombatRules.Charge.PenaltyLasts),
            ["charge_attacks.charge.if_the_target_uses_a_passive_defense"] = Is(CanonicalCombatRules.Charge.IfTheTargetUsesAPassiveDefense),
            ["charge_attacks.charge.self_damage_reduced_by"] = Is(CanonicalCombatRules.Charge.SelfDamageReducedBy),

            ["clobbering_attacks.clobbering.what_it_is"] = Is(CanonicalCombatRules.Clobbering.WhatItIs),
            ["clobbering_attacks.clobbering.attack_rolls"] = Is(CanonicalCombatRules.Clobbering.AttackRolls),
            ["clobbering_attacks.clobbering.attack_penalty_dice"] = Is(CanonicalCombatRules.Clobbering.AttackPenaltyDice),
            ["clobbering_attacks.clobbering.defense_rolls"] = Is(CanonicalCombatRules.Clobbering.DefenseRolls),
            ["clobbering_attacks.clobbering.targets"] = Is(CanonicalCombatRules.Clobbering.Targets),
            ["clobbering_attacks.clobbering.the_primary_target_is"] = Is(CanonicalCombatRules.Clobbering.ThePrimaryTargetIs),
            ["clobbering_attacks.clobbering.stops_if_the_primary_defends_actively_and_takes_no_damage"] = Is(CanonicalCombatRules.Clobbering.StopsIfThePrimaryDefendsActivelyAndTakesNoDamage),

            ["defending_others.defending_others.cost"] = Is(CanonicalCombatRules.DefendingOthers.Cost),
            ["defending_others.defending_others.range"] = Is(CanonicalCombatRules.DefendingOthers.Range),
            ["defending_others.defending_others.effect"] = Is(CanonicalCombatRules.DefendingOthers.Effect),
            ["defending_others.defending_others.may_use_an_active_or_a_passive_defense"] = Is(CanonicalCombatRules.DefendingOthers.MayUseAnActiveOrAPassiveDefense),
            ["defending_others.defending_others.an_active_defense_leaves_the_damage_on"] = Is(CanonicalCombatRules.DefendingOthers.AnActiveDefenseLeavesTheDamageOn),
            ["defending_others.defending_others.the_protected_character_may_still_use_a_passive_defense"] = Is(CanonicalCombatRules.DefendingOthers.TheProtectedCharacterMayStillUseAPassiveDefense),
            ["defending_others.defending_others.a_passive_defense_leaves_the_damage_on"] = Is(CanonicalCombatRules.DefendingOthers.APassiveDefenseLeavesTheDamageOn),

            ["going_all_out.all_out_attack.attack_bonus_dice"] = Is(CanonicalCombatRules.AllOutAttack.AttackBonusDice),
            ["going_all_out.all_out_attack.defense_ranks"] = Is(CanonicalCombatRules.AllOutAttack.DefenseRanks),
            ["going_all_out.all_out_attack.affects_active_defenses"] = Is(CanonicalCombatRules.AllOutAttack.AffectsActiveDefenses),
            ["going_all_out.all_out_attack.affects_passive_defenses"] = Is(CanonicalCombatRules.AllOutAttack.AffectsPassiveDefenses),
            ["going_all_out.all_out_attack.lasts"] = Is(CanonicalCombatRules.AllOutAttack.Lasts),
            ["going_all_out.all_out_attack.opponents_who_could_not_penetrate_your_passive_defense"] = Is(CanonicalCombatRules.AllOutAttack.OpponentsWhoCouldNotPenetrateYourPassiveDefense),
            ["going_all_out.all_out_defense.defense_bonus_dice"] = Is(CanonicalCombatRules.AllOutDefense.DefenseBonusDice),
            ["going_all_out.all_out_defense.lasts"] = Is(CanonicalCombatRules.AllOutDefense.Lasts),
            ["going_all_out.all_out_defense.prevents_attacking"] = Is(CanonicalCombatRules.AllOutDefense.PreventsAttacking),
            ["going_all_out.all_out_defense.prevents_other_actions"] = Is(CanonicalCombatRules.AllOutDefense.PreventsOtherActions),
            ["going_all_out.all_out_defense.allows_movement"] = Is(CanonicalCombatRules.AllOutDefense.AllowsMovement),
            ["going_all_out.all_out_defense.allows_free_actions"] = Is(CanonicalCombatRules.AllOutDefense.AllowsFreeActions),
            ["going_all_out.all_out_defense.a_travel_power_or_speed_may_be_used_as_an_active_defense"] = Is(CanonicalCombatRules.AllOutDefense.ATravelPowerOrSpeedMayBeUsedAsAnActiveDefense),

            ["knockback.knockback.requires_damage_type"] = Is(CanonicalCombatRules.Knockback.RequiresDamageType),
            ["knockback.knockback.minimum_damage"] = Is(CanonicalCombatRules.Knockback.MinimumDamage),
            ["knockback.knockback.cost_resolve"] = Is(CanonicalCombatRules.Knockback.CostResolve),
            ["knockback.knockback.target_is_thrown_as_if_by_a_might_rank_equal_to"] = Is(CanonicalCombatRules.Knockback.TargetIsThrownAsIfByAMightRankEqualTo),
            ["knockback.knockback.target_falls_prone"] = Is(CanonicalCombatRules.Knockback.TargetFallsProne),
            ["knockback.knockback.target_loses_their_next_turn_to_act"] = Is(CanonicalCombatRules.Knockback.TargetLosesTheirNextTurnToAct),
            ["knockback.knockback.damage_on_striking_a_solid_object"] = Is(CanonicalCombatRules.Knockback.DamageOnStrikingASolidObject),
            ["knockback.knockback.the_object_must_be_tougher_than_the_target"] = Is(CanonicalCombatRules.Knockback.TheObjectMustBeTougherThanTheTarget),
            ["knockback.knockback.a_passive_defense_above_the_objects_structure"] = Is(CanonicalCombatRules.Knockback.APassiveDefenseAboveTheObjectsStructure),

            ["luring.luring.what_it_is"] = Is(CanonicalCombatRules.Luring.WhatItIs),
            ["luring.luring.applies_to_attack_types"] = Is(CanonicalCombatRules.Luring.AppliesToAttackTypes),
            ["luring.luring.declared_before"] = Is(CanonicalCombatRules.Luring.DeclaredBefore),
            ["luring.luring.requires_an_active_defense"] = Is(CanonicalCombatRules.Luring.RequiresAnActiveDefense),
            ["luring.luring.defense_must_exceed_the_attack_roll_by"] = Is(CanonicalCombatRules.Luring.DefenseMustExceedTheAttackRollBy),
            ["luring.luring.cost_resolve"] = Is(CanonicalCombatRules.Luring.CostResolve),
            ["luring.luring.redirects_to"] = Is(CanonicalCombatRules.Luring.RedirectsTo),
            ["luring.luring.may_redirect_onto_a_person"] = Is(CanonicalCombatRules.Luring.MayRedirectOntoAPerson),
            ["luring.luring.redirecting_onto_a_person_costs"] = Is(CanonicalCombatRules.Luring.RedirectingOntoAPersonCosts),
            ["luring.luring.the_new_target_makes_their_own_defense_roll"] = Is(CanonicalCombatRules.Luring.TheNewTargetMakesTheirOwnDefenseRoll),

            ["team_attacks.team_attack.what_it_is"] = Is(CanonicalCombatRules.TeamAttack.WhatItIs),
            ["team_attacks.team_attack.participants_act_at"] = Is(CanonicalCombatRules.TeamAttack.ParticipantsActAt),
            ["team_attacks.team_attack.all_participants_must_target_the_same_enemy"] = Is(CanonicalCombatRules.TeamAttack.AllParticipantsMustTargetTheSameEnemy),
            ["team_attacks.team_attack.attack_bonus_dice"] = Is(CanonicalCombatRules.TeamAttack.AttackBonusDice),
            ["team_attacks.team_attack.cost_resolve_to_make_sixes_explode"] = Is(CanonicalCombatRules.TeamAttack.CostResolveToMakeSixesExplode),
            ["team_attacks.team_attack.explosion_recurses_while_sixes_keep_coming"] = Is(CanonicalCombatRules.TeamAttack.ExplosionRecursesWhileSixesKeepComing),
            ["team_attacks.team_attack.limit_per_target_per_battle"] = Is(CanonicalCombatRules.TeamAttack.LimitPerTargetPerBattle),
            ["team_attacks.team_attack.the_limit_may_be_lifted_by"] = Is(CanonicalCombatRules.TeamAttack.TheLimitMayBeLiftedBy),
            ["team_attacks.team_attack.use_sparingly"] = Is(CanonicalCombatRules.TeamAttack.UseSparingly),


            // gritty.json
            ["gritty_overview.overview.default_combat_is"] = Is(CanonicalGrittyRules.Overview.DefaultCombatIs),
            ["gritty_overview.overview.rules_are_optional"] = Is(CanonicalGrittyRules.Overview.RulesAreOptional),
            ["gritty_overview.overview.any_subset_may_be_used"] = Is(CanonicalGrittyRules.Overview.AnySubsetMayBeUsed),
            ["gritty_overview.overview.review_before_adopting"] = Is(CanonicalGrittyRules.Overview.ReviewBeforeAdopting),
            ["gritty_overview.overview.a_retcon_or_do_over_is_allowed_if_a_rule_is_dropped_after_play"] = Is(CanonicalGrittyRules.Overview.ARetconOrDoOverIsAllowedIfARuleIsDroppedAfterPlay),

            ["gritty_active_defenses.active_defense_penalty.active_defenses_are_minor_actions"] = Is(CanonicalGrittyRules.ActiveDefensePenalty.ActiveDefensesAreMinorActions),
            ["gritty_active_defenses.active_defense_penalty.cumulative_penalty_dice_per_extra_active_defense"] = Is(CanonicalGrittyRules.ActiveDefensePenalty.CumulativePenaltyDicePerExtraActiveDefense),
            ["gritty_active_defenses.active_defense_penalty.first_active_defense_on_a_page_is_unpenalised"] = Is(CanonicalGrittyRules.ActiveDefensePenalty.FirstActiveDefenseOnAPageIsUnpenalised),
            ["gritty_active_defenses.active_defense_penalty.counted_per"] = Is(CanonicalGrittyRules.ActiveDefensePenalty.CountedPer),
            ["gritty_active_defenses.active_defense_penalty.affects_passive_defenses"] = Is(CanonicalGrittyRules.ActiveDefensePenalty.AffectsPassiveDefenses),

            ["gritty_close_range.close_range_penalty.penalty_dice_to_active_defense"] = Is(CanonicalGrittyRules.CloseRangePenalty.PenaltyDiceToActiveDefense),
            ["gritty_close_range.close_range_penalty.applies_against"] = Is(CanonicalGrittyRules.CloseRangePenalty.AppliesAgainst),
            ["gritty_close_range.close_range_penalty.applies_only_to_attacks_usable_at"] = Is(CanonicalGrittyRules.CloseRangePenalty.AppliesOnlyToAttacksUsableAt),
            ["gritty_close_range.close_range_penalty.ignored_for"] = Is(CanonicalGrittyRules.CloseRangePenalty.IgnoredFor),

            ["gritty_the_drop.the_drop.held_by"] = Is(CanonicalGrittyRules.TheDrop.HeldBy),
            ["gritty_the_drop.the_drop.held_against"] = Is(CanonicalGrittyRules.TheDrop.HeldAgainst),
            ["gritty_the_drop.the_drop.effect"] = Is(CanonicalGrittyRules.TheDrop.Effect),
            ["gritty_the_drop.the_drop.also_held_by"] = Is(CanonicalGrittyRules.TheDrop.AlsoHeldBy),
            ["gritty_the_drop.the_drop.examples_given"] = Is(CanonicalGrittyRules.TheDrop.ExamplesGiven),
            ["gritty_the_drop.the_drop.final_say"] = Is(CanonicalGrittyRules.TheDrop.FinalSay),

            ["gritty_fatal_damage.fatal_damage.health_can_go_negative"] = Is(CanonicalGrittyRules.FatalDamage.HealthCanGoNegative),
            ["gritty_fatal_damage.fatal_damage.killed_at"] = Is(CanonicalGrittyRules.FatalDamage.KilledAt),
            ["gritty_fatal_damage.fatal_damage.cost_resolve_to_avoid"] = Is(CanonicalGrittyRules.FatalDamage.CostResolveToAvoid),
            ["gritty_fatal_damage.fatal_damage.resolve_reduces_damage_to"] = Is(CanonicalGrittyRules.FatalDamage.ResolveReducesDamageTo),
            ["gritty_fatal_damage.fatal_damage.resolve_may_be_spent_on_damage_you_inflict_on_someone_else"] = Is(CanonicalGrittyRules.FatalDamage.ResolveMayBeSpentOnDamageYouInflictOnSomeoneElse),
            ["gritty_fatal_damage.fatal_damage.resolve_also_stabilises_if_necessary"] = Is(CanonicalGrittyRules.FatalDamage.ResolveAlsoStabilisesIfNecessary),
            ["gritty_fatal_damage.fatal_damage.dying_begins_when_lethal_damage_reduces_you_to"] = Is(CanonicalGrittyRules.FatalDamage.DyingBeginsWhenLethalDamageReducesYouTo),
            ["gritty_fatal_damage.fatal_damage.dying_damage_per_page"] = Is(CanonicalGrittyRules.FatalDamage.DyingDamagePerPage),
            ["gritty_fatal_damage.fatal_damage.dying_ends_at"] = Is(CanonicalGrittyRules.FatalDamage.DyingEndsAt),
            ["gritty_fatal_damage.fatal_damage.stabilise_roll"] = Is(CanonicalGrittyRules.FatalDamage.StabiliseRoll),
            ["gritty_fatal_damage.fatal_damage.stabilise_difficulty"] = Is(CanonicalGrittyRules.FatalDamage.StabiliseDifficulty),
            ["gritty_fatal_damage.fatal_damage.stabilise_threshold"] = Is(CanonicalGrittyRules.FatalDamage.StabiliseThreshold),
            ["gritty_fatal_damage.fatal_damage.stabilise_also_by"] = Is(CanonicalGrittyRules.FatalDamage.StabiliseAlsoBy),
            ["gritty_fatal_damage.fatal_damage.cost_resolve_to_stabilise_immediately"] = Is(CanonicalGrittyRules.FatalDamage.CostResolveToStabiliseImmediately),
            ["gritty_fatal_damage.fatal_damage.instant_recovery_requires_being_stable"] = Is(CanonicalGrittyRules.FatalDamage.InstantRecoveryRequiresBeingStable),

            ["gritty_friendly_fire.friendly_fire.penalty_dice"] = Is(CanonicalGrittyRules.FriendlyFire.PenaltyDice),
            ["gritty_friendly_fire.friendly_fire.applies_when"] = Is(CanonicalGrittyRules.FriendlyFire.AppliesWhen),
            ["gritty_friendly_fire.friendly_fire.second_attack_triggered_at_net_successes"] = Is(CanonicalGrittyRules.FriendlyFire.SecondAttackTriggeredAtNetSuccesses),
            ["gritty_friendly_fire.friendly_fire.second_attack_is_against"] = Is(CanonicalGrittyRules.FriendlyFire.SecondAttackIsAgainst),
            ["gritty_friendly_fire.friendly_fire.second_attack_penalty_dice"] = Is(CanonicalGrittyRules.FriendlyFire.SecondAttackPenaltyDice),
            ["gritty_friendly_fire.friendly_fire.second_target_selected_by"] = Is(CanonicalGrittyRules.FriendlyFire.SecondTargetSelectedBy),
            ["gritty_friendly_fire.friendly_fire.second_target_selected"] = Is(CanonicalGrittyRules.FriendlyFire.SecondTargetSelected),

            ["gritty_hard_targets.hard_targets.applies_to"] = Is(CanonicalGrittyRules.HardTargets.AppliesTo),
            ["gritty_hard_targets.hard_targets.passive_defense_rank"] = Is(CanonicalGrittyRules.HardTargets.PassiveDefenseRank),
            ["gritty_hard_targets.hard_targets.penalty_dice_to_negate_it"] = Is(CanonicalGrittyRules.HardTargets.PenaltyDiceToNegateIt),
            ["gritty_hard_targets.hard_targets.negation_available_against"] = Is(CanonicalGrittyRules.HardTargets.NegationAvailableAgainst),
            ["gritty_hard_targets.hard_targets.recommended_pro_for_vehicle_scale_weapons"] = Is(CanonicalGrittyRules.HardTargets.RecommendedProForVehicleScaleWeapons),
            ["gritty_hard_targets.hard_targets.recommended_pro_for_the_physical_attacks_of_powerful_superhuman_characters"] = Is(CanonicalGrittyRules.HardTargets.RecommendedProForThePhysicalAttacksOfPowerfulSuperhumanCharacters),

            ["gritty_raised_gear_limit.gear_limit.what_it_is"] = Is(CanonicalGrittyRules.GearLimit.WhatItIs),
            ["gritty_raised_gear_limit.gear_limit.default_rank"] = Is(CanonicalGrittyRules.GearLimit.DefaultRank),
            ["gritty_raised_gear_limit.gear_limit.raised_options"] = Is(CanonicalGrittyRules.GearLimit.RaisedOptions),
            ["gritty_raised_gear_limit.gear_limit.raised_options_are_open_ended"] = Is(CanonicalGrittyRules.GearLimit.RaisedOptionsAreOpenEnded),
            ["gritty_raised_gear_limit.gear_limit.worked_example_weapon"] = Is(CanonicalGrittyRules.GearLimit.WorkedExampleWeapon),
            ["gritty_raised_gear_limit.gear_limit.worked_example_weapon_bonus_dice"] = Is(CanonicalGrittyRules.GearLimit.WorkedExampleWeaponBonusDice),
            ["gritty_raised_gear_limit.gear_limit.worked_example_maximum_effective_rank_at_the_default_limit"] = Is(CanonicalGrittyRules.GearLimit.WorkedExampleMaximumEffectiveRankAtTheDefaultLimit),
            ["gritty_raised_gear_limit.gear_limit.detail_chapter"] = Is(CanonicalGrittyRules.GearLimit.DetailChapter),

            ["gritty_slow_healing.slow_healing.bands"] = HealingBandsAre(CanonicalGrittyRules.SlowHealing.Bands),
            ["gritty_slow_healing.slow_healing.healing_after_each_battle"] = Is(CanonicalGrittyRules.SlowHealing.HealingAfterEachBattle),
            ["gritty_slow_healing.slow_healing.healing_on_regaining_consciousness_after_a_defeat"] = Is(CanonicalGrittyRules.SlowHealing.HealingOnRegainingConsciousnessAfterADefeat),
            ["gritty_slow_healing.slow_healing.you_may_be_conscious_at_zero_or_negative_health"] = Is(CanonicalGrittyRules.SlowHealing.YouMayBeConsciousAtZeroOrNegativeHealth),
            ["gritty_slow_healing.slow_healing.in_that_condition_any_damage_at_all_defeats_you"] = Is(CanonicalGrittyRules.SlowHealing.InThatConditionAnyDamageAtAllDefeatsYou),
            ["gritty_slow_healing.slow_healing.stabilization_available_as_often_as_necessary"] = Is(CanonicalGrittyRules.SlowHealing.StabilizationAvailableAsOftenAsNecessary),
            ["gritty_slow_healing.slow_healing.medicine_healing_limit"] = Is(CanonicalGrittyRules.SlowHealing.MedicineHealingLimit),
            ["gritty_slow_healing.slow_healing.medicine_health_per_net_successes"] = Is(CanonicalGrittyRules.SlowHealing.MedicineHealthPerNetSuccesses),
            ["gritty_slow_healing.slow_healing.medicine_net_successes_per_point"] = Is(CanonicalGrittyRules.SlowHealing.MedicineNetSuccessesPerPoint),

            ["gritty_tough_minions.tough_minions.net_successes_per_minion_defeated"] = Is(CanonicalGrittyRules.ToughMinions.NetSuccessesPerMinionDefeated),
            ["gritty_tough_minions.tough_minions.full_net_successes_required"] = Is(CanonicalGrittyRules.ToughMinions.FullNetSuccessesRequired),
            ["gritty_tough_minions.tough_minions.rounding"] = Is(CanonicalGrittyRules.ToughMinions.Rounding),
            ["gritty_tough_minions.tough_minions.rounding_is_a_named_unique_exception"] = Is(CanonicalGrittyRules.ToughMinions.RoundingIsANamedUniqueException),
            ["gritty_tough_minions.tough_minions.worked_example_net_successes"] = Is(CanonicalGrittyRules.ToughMinions.WorkedExampleNetSuccesses),
            ["gritty_tough_minions.tough_minions.worked_example_minions_defeated"] = Is(CanonicalGrittyRules.ToughMinions.WorkedExampleMinionsDefeated),
            ["gritty_tough_minions.tough_minions.area_attack_minions_per_net_success"] = Is(CanonicalGrittyRules.ToughMinions.AreaAttackMinionsPerNetSuccess),
            ["gritty_tough_minions.tough_minions.area_attack_rate_it_replaces"] = Is(CanonicalGrittyRules.ToughMinions.AreaAttackRateItReplaces),
            ["gritty_tough_minions.tough_minions.alternative_offered"] = Is(CanonicalGrittyRules.ToughMinions.AlternativeOffered),

            ["gritty_wound_penalties.wound_penalties.at_or_below_half_full_health_penalty_dice"] = Is(CanonicalGrittyRules.WoundPenalties.AtOrBelowHalfFullHealthPenaltyDice),
            ["gritty_wound_penalties.wound_penalties.at_or_below_zero_health_penalty_dice"] = Is(CanonicalGrittyRules.WoundPenalties.AtOrBelowZeroHealthPenaltyDice),
            ["gritty_wound_penalties.wound_penalties.zero_or_less_is_reachable_only_with"] = Is(CanonicalGrittyRules.WoundPenalties.ZeroOrLessIsReachableOnlyWith),
            ["gritty_wound_penalties.wound_penalties.applies_to"] = Is(CanonicalGrittyRules.WoundPenalties.AppliesTo),
            ["gritty_wound_penalties.wound_penalties.cost_resolve_to_ignore"] = Is(CanonicalGrittyRules.WoundPenalties.CostResolveToIgnore),
            ["gritty_wound_penalties.wound_penalties.pages_ignored_per_resolve_point"] = Is(CanonicalGrittyRules.WoundPenalties.PagesIgnoredPerResolvePoint)
        };

    private static HashSet<string> RegisteredPaths =>
        new HashSet<string>(CanonicalChecks.Keys.Concat(DerivedPaths), StringComparer.Ordinal);

    /// <summary>
    /// <b>The test the review asked for, and the one the deserialization check was mistaken for.</b>
    /// It walks the models by reflection, so a field cannot be added to a play rules file and
    /// quietly go unchecked: every leaf below an entry's envelope is prose, a labelled derivation,
    /// or a value compared here against <see cref="CanonicalChallengeRules"/>. Anything else is a
    /// fault naming the path.
    ///
    /// <para><b>What it does not see is a field whose value is null</b>, because a null leaf and an
    /// optional shape this entry does not use are the same thing to reflection —
    /// <c>arduous_exchanges_max</c> is the one such field and
    /// <see cref="AContestIsUsuallyThreeExchangesAndAnArduousOneIsSixOrMore"/> asserts it by
    /// name.</para>
    /// </summary>
    [Fact]
    public void EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook()
    {
        var faults = new List<string>();
        var leaves = 0;

        foreach (var (id, entry) in AllEntryObjects())
        {
            leaves += EntryLeaves(id, entry, RegisteredPaths).Count();
            faults.AddRange(CoverageFaults(id, entry));
        }

        // Positive control on the walk itself. A reflection walk that stopped finding properties —
        // a records-to-classes refactor, a filter that matched everything — would report no faults
        // and prove nothing, which is the exact shape of the four guard failures CLAUDE.md lists.
        Assert.True(
            leaves >= 590,
            $"The walk found only {leaves} fact fields across the five files, which is fewer than "
            + "the entries carry — there are 634 today, 98 of them Chapter 3's and 388 Chapter "
            + "4's. It has stopped reading the models; fix the walk, not this number.");

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>A record with one unregistered fact field, one prose field and one aside.</summary>
    private sealed record CoverageProbe(int MadeUpNumber, string WhatThisIs, string ExtraNote);

    /// <summary>
    /// <b>The negative control for the walk above, and it is the whole instrument.</b> A classifier
    /// that faulted nothing would pass <see cref="EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook"/>
    /// perfectly while checking nothing at all — which is how a guard in this repository has been
    /// wrong four times. So an unregistered field is fed to the same classifier and must be
    /// reported, and the two prose spellings beside it must not be.
    /// </summary>
    [Fact]
    public void TheCoverageWalkReportsAFieldNothingComparesToTheRulebook()
    {
        var faults = CoverageFaults("probe", new CoverageProbe(1, "ours", "an aside"));

        var fault = Assert.Single(faults);
        Assert.Contains("probe.made_up_number", fault, StringComparison.Ordinal);

        // And prose really is passing as prose rather than being missed by the walk, which would
        // look identical from the count above.
        var empty = CoverageFaults("probe", new CoverageProbe(1, "   ", "an aside"));
        Assert.Equal(2, empty.Count);
        Assert.Contains(empty, f => f.Contains("probe.what_this_is", StringComparison.Ordinal));
    }

    /// <summary>
    /// Which canonical file an entry answers to, for the fault message. The probe record in
    /// <see cref="TheCoverageWalkReportsAFieldNothingComparesToTheRulebook"/> belongs to neither,
    /// so it is named as Chapter 3's — the classifier is what that test measures, not the wording.
    /// </summary>
    private static string CanonicalFileFor(string entryId)
    {
        if (Resolve().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal)))
            return nameof(CanonicalResolveRules);

        if (Combat().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal)))
            return nameof(CanonicalCombatRules);

        if (Gritty().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal)))
            return nameof(CanonicalGrittyRules);

        return nameof(CanonicalChallengeRules);
    }

    private static List<string> CoverageFaults(string entryId, object entry)
    {
        var faults = new List<string>();

        foreach (var (path, value) in EntryLeaves(entryId, entry, RegisteredPaths))
        {
            var leafName = path[(path.LastIndexOf('.') + 1)..];

            if (IsProse(leafName))
            {
                if (value is not string text || string.IsNullOrWhiteSpace(text))
                    faults.Add($"{path}: prose field is empty");

                continue;
            }

            if (DerivedPaths.Contains(path)) continue;

            if (!CanonicalChecks.TryGetValue(path, out var check))
            {
                // Named rather than hard-coded, because there are two canonical files now and a
                // message sending the reader to the wrong chapter's is worse than no message.
                // Found by mutation: a Chapter 5 field faulted with Chapter 3's file in the text.
                faults.Add(
                    $"{path} is a fact field and nothing compares it to {CanonicalFileFor(entryId)}. "
                    + "Transcribe it there with the sentence it came from, register it, or move it "
                    + "into description/ambiguity prose — an unchecked fact field reads as verified "
                    + "data and is not");
                continue;
            }

            var fault = check(value);
            if (fault is not null) faults.Add($"{path} {fault}");
        }

        return faults;
    }

    /// <summary>
    /// Every leaf below an entry's envelope, as a dotted path. Nested models are descended into and
    /// lists of models are indexed; a list of scalars, a dictionary or a scalar is a leaf. A path in
    /// <paramref name="stopAt"/> is yielded whole rather than descended into, so a table registered
    /// as one value is compared by one comparer.
    /// </summary>
    private static IEnumerable<(string Path, object Value)> EntryLeaves(
        string entryId, object entry, HashSet<string>? stopAt)
    {
        foreach (var property in entry.GetType().GetProperties())
        {
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
            if (EnvelopeFields.Contains(name)) continue;

            var value = property.GetValue(entry);
            if (value is null) continue;

            foreach (var leaf in Descend($"{entryId}.{name}", value, stopAt)) yield return leaf;
        }
    }

    private static IEnumerable<(string Path, object Value)> Descend(
        string path, object value, HashSet<string>? stopAt)
    {
        if (stopAt is not null && stopAt.Contains(path))
        {
            yield return (path, value);
            yield break;
        }

        if (IsTestModel(value))
        {
            foreach (var property in value.GetType().GetProperties())
            {
                var child = property.GetValue(value);
                if (child is null) continue;

                var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);

                foreach (var leaf in Descend($"{path}.{name}", child, stopAt)) yield return leaf;
            }

            yield break;
        }

        if (value is System.Collections.IEnumerable items and not string)
        {
            var rows = items.Cast<object>().ToList();

            if (rows.Count > 0 && rows.TrueForAll(IsTestModel))
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    foreach (var leaf in Descend($"{path}[{i}]", rows[i], stopAt)) yield return leaf;
                }

                yield break;
            }
        }

        yield return (path, value);
    }

    /// <summary>A model is one of the records declared in this class; anything else is a value.</summary>
    private static bool IsTestModel(object value) =>
        value.GetType().DeclaringType == typeof(PlayRulesDataTests);

    private static Func<object?, string?> Is(object? expected) => actual =>
        ValuesEqual(expected, actual) ? null : $"is {Show(actual)}; the rulebook says {Show(expected)}";

    private static Func<object?, string?> Names(int page) => actual =>
        actual is string text && text.Contains($"p.{page}", StringComparison.Ordinal)
            ? null
            : $"is {Show(actual)}, which does not name p.{page}";

    private static Func<object?, string?> MapIs(IReadOnlyDictionary<int, int> expected) => actual =>
    {
        if (actual is not IReadOnlyDictionary<string, int> map) return $"is {Show(actual)}, not a success map";

        var faces = map.Keys.Select(k => int.Parse(k, CultureInfo.InvariantCulture)).Order().ToList();
        if (!faces.SequenceEqual(expected.Keys.Order())) return $"maps faces {string.Join(",", faces)}";

        var wrong = expected
            .Where(pair => map[pair.Key.ToString(CultureInfo.InvariantCulture)] != pair.Value)
            .Select(pair => $"{pair.Key}→{map[pair.Key.ToString(CultureInfo.InvariantCulture)]} not {pair.Value}")
            .ToList();

        return wrong.Count == 0 ? null : $"scores {string.Join(", ", wrong)}";
    };

    private static Func<object?, string?> BandsAre(IReadOnlyList<CanonicalChallengeRules.Band> expected) => actual =>
    {
        if (actual is not IReadOnlyList<BandModel> bands) return $"is {Show(actual)}, not a band table";
        if (bands.Count != expected.Count) return $"has {bands.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (bands[i].MinNetSuccesses != expected[i].Min
                || bands[i].MaxNetSuccesses != expected[i].Max
                || !string.Equals(bands[i].Outcome, expected[i].Outcome, StringComparison.Ordinal)
                || bands[i].Embellishment != expected[i].Embellishment)
            {
                return $"row {i} is {bands[i]}; the rulebook says {expected[i]}";
            }
        }

        return null;
    };

    private static Func<object?, string?> ThresholdsAre(
        IReadOnlyList<CanonicalChallengeRules.Threshold> expected) => actual =>
    {
        if (actual is not IReadOnlyList<ThresholdModel> rows) return $"is {Show(actual)}, not a thresholds table";
        if (rows.Count != expected.Count) return $"has {rows.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(rows[i].Difficulty, expected[i].Difficulty, StringComparison.Ordinal)
                || rows[i].ThresholdMin != expected[i].Min
                || rows[i].ThresholdMax != expected[i].Max)
            {
                return $"row {i} is {rows[i]}; the rulebook says {expected[i]}";
            }
        }

        return null;
    };

    private static Func<object?, string?> ResolveTableIs(
        IReadOnlyList<CanonicalResolveRules.StartingResolveRow> expected) => actual =>
    {
        if (actual is not IReadOnlyList<StartingResolveRowModel> rows) return $"is {Show(actual)}, not a Resolve table";
        if (rows.Count != expected.Count) return $"has {rows.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (rows[i].RanksBelowTraitCap != expected[i].RanksBelowTraitCap
                || rows[i].Resolve != expected[i].Resolve)
            {
                return $"row {i} is {rows[i]}; the rulebook says {expected[i]}";
            }
        }

        return null;
    };

    private static Func<object?, string?> GuidanceIs(
        IReadOnlyList<CanonicalResolveRules.ChallengeLevelGuidance> expected) => actual =>
    {
        if (actual is not IReadOnlyList<ChallengeLevelGuidanceModel> rows)
            return $"is {Show(actual)}, not a Challenge Level guidance table";

        if (rows.Count != expected.Count) return $"has {rows.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (rows[i].Level != expected[i].Level
                || !string.Equals(rows[i].UsedFor, expected[i].UsedFor, StringComparison.Ordinal))
            {
                return $"row {i} is {rows[i]}; the rulebook says {expected[i]}";
            }
        }

        return null;
    };

    private static Func<object?, string?> JudgingIs(
        IReadOnlyList<CanonicalChallengeRules.Judging> expected) => actual =>
    {
        if (actual is not IReadOnlyList<JudgingModel> rows) return $"is {Show(actual)}, not a judging guideline";
        if (rows.Count != expected.Count) return $"has {rows.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (!string.Equals(rows[i].Descriptor, expected[i].Descriptor, StringComparison.Ordinal)
                || !string.Equals(rows[i].Difficulty, expected[i].Difficulty, StringComparison.Ordinal)
                || rows[i].Threshold != expected[i].ThresholdValue)
            {
                return $"row {i} is {rows[i]}; the rulebook says {expected[i]}";
            }
        }

        return null;
    };


    // ── Chapter 4's table comparers ──────────────────────────────────────────
    //
    // One per printed table, for the same reason the Chapter 3 ones exist: a table registered as a
    // single path is compared row by row against the canonical record, so a reordered, shortened or
    // altered table fails naming the row rather than passing because the walk never descended into it.

    private static Func<object?, string?> RowsAre<TModel, TCanonical>(
        string what,
        IReadOnlyList<TCanonical> expected,
        Func<TModel, TCanonical, bool> same) => actual =>
    {
        if (actual is not IReadOnlyList<TModel> rows) return $"is {Show(actual)}, not {what}";
        if (rows.Count != expected.Count) return $"has {rows.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (!same(rows[i], expected[i])) return $"row {i} is {rows[i]}; the rulebook says {expected[i]}";
        }

        return null;
    };

    private static bool Same(IReadOnlyList<string> actual, IReadOnlyList<string> expected) =>
        actual.SequenceEqual(expected, StringComparer.Ordinal);

    private static Func<object?, string?> RangeClassesAre(
        IReadOnlyList<CanonicalCombatRules.RangeClass> expected) =>
        RowsAre<RangesRowModel, CanonicalCombatRules.RangeClass>("a range class table", expected,
            (a, e) => a.Class == e.Name && a.Covers == e.Covers);

    private static Func<object?, string?> ThrowingRowsAre(
        IReadOnlyList<CanonicalCombatRules.ThrowingRow> expected) =>
        RowsAre<ThrowingTableRowModel, CanonicalCombatRules.ThrowingRow>("a Throwing table", expected,
            (a, e) => a.MinRank == e.MinRank && a.MaxRank == e.MaxRank && a.Range == e.Range);

    private static Func<object?, string?> AttackDefenseRowsAre(
        IReadOnlyList<CanonicalCombatRules.AttackDefenseRow> expected) =>
        RowsAre<AttackDefenseTableRowModel, CanonicalCombatRules.AttackDefenseRow>(
            "an Attack and Defense table", expected,
            (a, e) => a.Type == e.Type && a.AttackTrait == e.AttackTrait && Same(a.DefenseTraits, e.DefenseTraits));

    private static Func<object?, string?> ModifierBandsAre(
        IReadOnlyList<CanonicalCombatRules.ModifierBand> expected) => actual =>
    {
        // The three modifier tables share a shape and not a field name — cover, size and visibility
        // each name their own condition column — so the row is read through whichever model it is.
        var conditions = actual switch
        {
            IReadOnlyList<CoverBandsRowModel> c => c.Select(r => (r.Cover, r.Dice)).ToList(),
            IReadOnlyList<SizeBandsRowModel> z => z.Select(r => (r.AttackerRelativeSize, r.Dice)).ToList(),
            IReadOnlyList<VisibilityBandsRowModel> v => v.Select(r => (r.Visibility, r.Dice)).ToList(),
            _ => null
        };

        if (conditions is null) return $"is {Show(actual)}, not a modifier table";
        if (conditions.Count != expected.Count) return $"has {conditions.Count} rows, not {expected.Count}";

        for (var i = 0; i < expected.Count; i++)
        {
            if (conditions[i].Item1 != expected[i].Condition || conditions[i].Item2 != expected[i].Dice)
                return $"row {i} is {conditions[i]}; the rulebook says {expected[i]}";
        }

        return null;
    };

    private static Func<object?, string?> GrapplingRowsAre(
        IReadOnlyList<CanonicalCombatRules.GrapplingRow> expected) =>
        RowsAre<GrapplingTableRowModel, CanonicalCombatRules.GrapplingRow>("a Grappling table", expected,
            (a, e) => a.MinNetSuccesses == e.Min && a.MaxNetSuccesses == e.Max
                      && a.Grab == e.Grab && a.Hold == e.Hold && a.Escape == e.Escape);

    private static Func<object?, string?> SampleStuntsAre(
        IReadOnlyList<CanonicalCombatRules.SampleStunt> expected) =>
        RowsAre<SampleStuntsRowModel, CanonicalCombatRules.SampleStunt>("a sample stunt list", expected,
            (a, e) => a.Name == e.Name && Same(a.AttackTraits, e.AttackTraits)
                      && Same(a.DefenseTraits, e.DefenseTraits));

    private static Func<object?, string?> StuntBandsAre(
        IReadOnlyList<CanonicalCombatRules.StuntBand> expected) =>
        RowsAre<CombatStuntBandsRowModel, CanonicalCombatRules.StuntBand>("a Combat Stunts table", expected,
            (a, e) => a.MinNetSuccesses == e.Min && a.MaxNetSuccesses == e.Max
                      && Same(a.SampleEffects, e.SampleEffects));

    private static Func<object?, string?> ThreatRowsAre(
        IReadOnlyList<CanonicalCombatRules.ThreatRow> expected) =>
        RowsAre<ThreatRanksRowModel, CanonicalCombatRules.ThreatRow>("a Threat Ranks table", expected,
            (a, e) => a.Category == e.Category && a.MinThreat == e.MinThreat && a.MaxThreat == e.MaxThreat);

    private static Func<object?, string?> MinionGroupRowsAre(
        IReadOnlyList<CanonicalCombatRules.MinionGroupRow> expected) =>
        RowsAre<MinionGroupAttackRowModel, CanonicalCombatRules.MinionGroupRow>(
            "a Minion Group Attack table", expected,
            (a, e) => a.MinMinions == e.MinMinions && a.MaxMinions == e.MaxMinions && a.BonusDice == e.BonusDice);

    private static Func<object?, string?> HealingBandsAre(
        IReadOnlyList<CanonicalGrittyRules.HealingBand> expected) =>
        RowsAre<SlowHealingBandsRowModel, CanonicalGrittyRules.HealingBand>("a Slow Healing table", expected,
            (a, e) => a.MinToughness == e.MinToughness && a.MaxToughness == e.MaxToughness
                      && a.HealthPerDay == e.HealthPerDay && a.OnePointEveryHours == e.OneEvery);

    private static bool ValuesEqual(object? expected, object? actual)
    {
        if (expected is null || actual is null) return expected is null && actual is null;

        if (expected is System.Collections.IEnumerable left and not string
            && actual is System.Collections.IEnumerable right and not string)
        {
            return left.Cast<object>().SequenceEqual(right.Cast<object>());
        }

        return Equals(expected, actual);
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object>()) + "]",
        _ => value.ToString() ?? "null"
    };

    /// <summary>All three files, as (entry id, entry) pairs, for the reflection walk.</summary>
    private static IEnumerable<(string Id, object Entry)> AllEntryObjects() =>
        Meta().Entries.Select(e => (e.Id, (object)e))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e)))
            .Concat(Resolve().Entries.Select(e => (e.Id, (object)e)))
            .Concat(Combat().Entries.Select(e => (e.Id, (object)e)))
            .Concat(Gritty().Entries.Select(e => (e.Id, (object)e)));

    /// <summary>All three files, as (file, id, source_ref, corroborated_by) rows.</summary>
    private static IEnumerable<(string File, string Id, string SourceRef, IReadOnlyList<string>? CorroboratedBy)>
        AllEntries() =>
        Meta().Entries.Select(e => ("play_meta.json", e.Id, e.SourceRef, e.CorroboratedBy))
            .Concat(Challenge().Entries.Select(e => ("challenge.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Resolve().Entries.Select(e => ("resolve.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Combat().Entries.Select(e => ("combat.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Gritty().Entries.Select(e => ("gritty.json", e.Id, e.SourceRef, e.CorroboratedBy)));

    /// <summary>The descriptions of one file, as (id, description) pairs.</summary>
    private static IEnumerable<(string Id, string Description)> DescriptionsIn(string fileName) => fileName switch
    {
        "play_meta.json" => Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.Description)),
        "challenge.json" => Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.Description)),
        "combat.json" => Combat().Entries.Select(e => ($"combat.json/{e.Id}", e.Description)),
        "gritty.json" => Gritty().Entries.Select(e => ($"gritty.json/{e.Id}", e.Description)),
        _ => Resolve().Entries.Select(e => ($"resolve.json/{e.Id}", e.Description))
    };
}
