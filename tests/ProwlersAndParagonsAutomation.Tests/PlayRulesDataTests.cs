using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

// Chapter 7's canonical file is aliased, and the reason is a property of another guard rather than
// a matter of taste. AccountsContractTests.NoKeyOrTokenIsInTheRepository scans tests/ for a JWT
// shape — three token runs of 24, 16 and 16 characters joined by dots — and an ordinary C# member
// path is exactly that shape once the type name reaches twenty-four characters.
// `CanonicalEquipmentRules` is twenty-three and cleared it by one; `CanonicalEnvironmentRules` is
// twenty-five and did not, so a hundred and fifty registrations below reported themselves as
// committed credentials. Shortening the reference is the narrow fix: widening the scanner's
// identifier-path exclusion is the one edit that could switch that scan off while leaving every
// assertion in it green, which its own comment says in as many words.
using EnvRules = ProwlersAndParagonsAutomation.Tests.CanonicalEnvironmentRules;

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
            ["ch04-combat.json", "ch00-introduction.json"]),
        new(
            "equipment.json",
            "Ch.6 Equipment",
            CanonicalEquipmentRules.FirstPage,
            CanonicalEquipmentRules.LastPage,
            ["ch06-equipment.json", "ch00-introduction.json"]),
        new(
            "environment.json",
            "Ch.7 Environment",
            EnvRules.FirstPage,
            EnvRules.LastPage,
            ["ch07-environment.json", "ch00-introduction.json"])
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
        string SourceRef,
        int? SpecialCasesCount);

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

    // ── Chapter 6's models ───────────────────────────────────────────────────

    private sealed record WeaponRowModel(
        string Name, string Class, int? BonusDice, bool Subdual, IReadOnlyList<string> Features);

    private sealed record EquipmentGearLimitModel(
        string WhatItIs,
        int DefaultRank,
        string Usually,
        string MaximumEffectiveRankIs,
        string WorkedExampleWeapon,
        int WorkedExampleWeaponBonusDice,
        string WorkedExampleTrait,
        int WorkedExampleTraitRank,
        int WorkedExampleMaximumEffectiveRank,
        bool TraitCapIsADifferentThing,
        int StandardPowerLevelTraitCap,
        string MakesMundaneGearLessUsefulFor);

    private sealed record CloseCombatExceptionModel(
        string AppliesTo,
        string Condition,
        string YouMayUseInstead,
        bool AtTheWieldersOption,
        string WorkedExampleWeapon,
        int WorkedExampleWeaponBonusDice,
        int WorkedExampleGearLimit,
        int WorkedExampleArmedMaximumEffectiveRank,
        int WorkedExampleUnarmedRank,
        string WhatTheWeaponStillBuys);

    private sealed record RaisedLimitModel(
        IReadOnlyList<int> RaisedOptions,
        bool RaisedOptionsAreOpenEnded,
        bool MayBeDisregardedEntirely,
        string Suits,
        int PowersAreOvershadowedUnlessTheTraitCapExceedsTheGearLimitBy,
        IReadOnlyList<string> BalanceOptionsGiven,
        int BalanceOptionBonusDice,
        string BalanceOptionsExclude);

    private sealed record WeaponBonusModel(
        bool EveryWeaponHasOne,
        IReadOnlyList<string> MeleeAttackTraits,
        IReadOnlyList<string> MeleeDefenseTraits,
        IReadOnlyList<string> RangedAttackTraits,
        string AddedTo,
        string SubdualMarker,
        string DefaultDamage,
        string AncientAndModernDamage,
        string AdvancedDamage,
        IReadOnlyList<string> AdvancedPhysicalExceptions,
        string RangedWeaponsReach,
        IReadOnlyList<string> RangedReachExceptions);

    /// <summary>
    /// <b>How the printed table's columns were paired, which is a reading and not a printed
    /// column.</b> The extractor splits each weapons table into two blocks and pairing them row by
    /// row is this project's; see
    /// <see cref="TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns"/>, which derives it.
    /// </summary>
    private sealed record EquipmentInterpretationModel(string WhatThisIs, string RowAlignment);

    private sealed record EquipmentEntry(
        string Id,
        string Name,
        string Kind,
        string PrintedUnder,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        EquipmentGearLimitModel? GearLimit,
        CloseCombatExceptionModel? CloseCombatException,
        RaisedLimitModel? RaisedLimit,
        WeaponBonusModel? WeaponBonus,
        IReadOnlyList<WeaponRowModel>? Weapons,
        EquipmentInterpretationModel? Interpretation);

    // ── Chapter 7: environment.json ──────────────────────────────────────────

    private sealed record EnvDisasterModel(
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

    private sealed record EnvDisasterResultModel(
        int MinorGoalsAchievedMin,
        int MinorGoalsAchievedMax,
        int MajorGoalsAchievedMin,
        int MajorGoalsAchievedMax,
        string Narrator,
        bool Embellishment);

    private sealed record EnvEnergyModel(
        string KindsAreLumpedInto,
        string Why,
        bool GravityAndMagnetismAreEnergy,
        string GravityAndMagnetismAre,
        IReadOnlyList<string> GravityAndMagnetismRepresentedWith,
        string GravityAndMagnetismIfClassifiedAsEnergy);

    private sealed record EnvEnergyTypesModel(
        IReadOnlyList<string> Types,
        string ForceKineticAttacksAre,
        bool SonicFootnoteIsOfferedAsOptional,
        string SonicInAVacuum,
        int SonicUnderwaterBonusDice);

    private sealed record EnvFallingModel(
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

    private sealed record EnvFallingRowModel(int? UpToFeet, int Rank);

    private sealed record EnvHostileEnvironmentModel(
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

    private sealed record EnvSuffocationModel(
        string HoldBreathMinutesEqualTo,
        int DamagePerPageAfterThat,
        string AllSuffocationDamageRemovedWhen,
        bool RemovalIsImmediate,
        string DefeatedRatherThanKilledUnless,
        string SurvivingIsExplainedBy);

    private sealed record EnvSwimmingModel(
        string TravelSpeed,
        string AgilityUsedForMovementChallengeRolls,
        int PerceptionPenaltyDiceUnderwater,
        int ScubaMaskReducesVisualPerceptionPenaltyTo,
        string UnderwaterCombatEdge,
        int UnderwaterPhysicalAttackPenaltyDice,
        int UnderwaterActiveDefensePenaltyDice,
        string IgnoredBy,
        string DeepWaterComplexitiesLeftTo);

    private sealed record EnvLeapingModel(
        string EveryCharacterEffectivelyHas,
        string AtRank,
        string ForThePurposeOf,
        string ThePowerMustBeBoughtToUseItFor,
        bool DistancesAreDeliberatelyAbstract);

    private sealed record EnvLiftingModel(
        string MaximumWeightNormally,
        string StaticValueAssumes,
        string RollAskedForWhen,
        string RollIsAskedForBy,
        string RollTrait,
        string RollAgainst,
        string ThresholdDependsOn,
        string ReadOff);

    private sealed record EnvLiftingRowModel(
        string Weight, IReadOnlyList<string> Examples, int Threshold);

    private sealed record EnvScorchingModel(
        IReadOnlyList<string> Sources,
        bool IsAnAttack,
        string ResistedOnlyWith,
        bool GmMayAllowACreativeActiveDefense,
        string WorksLike,
        bool TableIsAGuide);

    private sealed record EnvScorchingRowModel(string Heat, string Electricity, int Rank);

    private sealed record EnvSmashingModel(
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

    private sealed record EnvSmashingRowModel(IReadOnlyList<string> Materials, int Structure);

    private sealed record EnvSmashingTableModel(
        IReadOnlyList<EnvSmashingRowModel> Rows,
        IReadOnlyList<string> FootnotedRowMaterials,
        string OzymandiumNote);

    private sealed record EnvDamagingCoverModel(
        string AppliesWhen,
        string AttackPenetratesWhen,
        string TargetMayUseTheObjectsStructureAs,
        IReadOnlyList<string> OptionsGiven);

    private sealed record EnvSceneryAsWeaponsModel(
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

    private sealed record EnvSceneryRowModel(
        IReadOnlyList<string> Scenery, int Structure, int MaximumAttackRank);

    private sealed record EnvMassiveObjectsModel(
        string WorksLike,
        string UsesInsteadOfBodyOrStructure,
        string Requires,
        string AlwaysBreaksApartAfter);

    private sealed record EnvMassiveObjectRowModel(
        IReadOnlyList<string> Objects, int WeightRank, int MaximumAttackRank);

    private sealed record EnvToxinsModel(
        IReadOnlyList<string> ToxinsAre,
        string ToxinsAreDescribedAs,
        string WorkLike,
        string ResistedOnlyWith,
        IReadOnlyList<string> PassiveDefensesNamed,
        bool ResistanceIsAPower,
        string TheProsAndConsApplyOnlyTo,
        string Unless);

    /// <summary>
    /// One of the three Pros and Cons pp.108-109 print, recorded as a pointer and nothing else —
    /// the same deferral shape <c>spend_combat</c> uses towards Chapter 4, checked by
    /// <see cref="AnEnvironmentEntryThatDefersToTheCharacterRulesCarriesReferencesAndNothingElse"/>.
    /// </summary>
    private sealed record EnvToxinOptionModel(
        bool TranscribedHere,
        string PrintedName,
        string OptionKind,
        string DetailStore,
        string PowerId,
        string OptionId);

    private sealed record EnvDiseaseRowModel(
        string Name,
        string Power,
        int Rank,
        IReadOnlyList<string> Options,
        bool Footnoted);

    private sealed record EnvDiseasesTableModel(
        IReadOnlyList<EnvDiseaseRowModel> Rows,
        IReadOnlyList<string> FootnotedRowNames,
        bool OptionsAreReferencedNotTranscribed,
        string StaphInfectionNote);

    private sealed record EnvToxinEffectModel(
        string Power, IReadOnlyList<string> Traits, int RankMin, int RankMax);

    private sealed record EnvDrugRowModel(
        string Name,
        IReadOnlyList<EnvToxinEffectModel> Effects,
        IReadOnlyList<string> Options,
        bool Footnoted);

    private sealed record EnvDrugsTableModel(
        IReadOnlyList<EnvDrugRowModel> Rows,
        IReadOnlyList<string> FootnotedRowNames,
        bool OptionsAreReferencedNotTranscribed,
        string AncientPoisonNote);

    private sealed record EnvInterpretationModel(string WhatThisIs, string RowAlignment);

    private sealed record EnvironmentEntry(
        string Id,
        string Name,
        string Kind,
        string PrintedUnder,
        string Description,
        IReadOnlyList<string> VerifiedFields,
        string SourceRef,
        IReadOnlyList<string>? CorroboratedBy,
        string? Ambiguity,
        EnvDisasterModel? Disaster,
        IReadOnlyList<EnvDisasterResultModel>? DisasterResults,
        EnvEnergyModel? Energy,
        EnvEnergyTypesModel? EnergyTypes,
        EnvFallingModel? Falling,
        IReadOnlyList<EnvFallingRowModel>? FallingTable,
        EnvHostileEnvironmentModel? HostileEnvironment,
        EnvSuffocationModel? Suffocation,
        EnvSwimmingModel? Swimming,
        EnvLeapingModel? Leaping,
        EnvLiftingModel? Lifting,
        IReadOnlyList<EnvLiftingRowModel>? LiftingTable,
        EnvScorchingModel? Scorching,
        IReadOnlyList<EnvScorchingRowModel>? ScorchingTable,
        EnvSmashingModel? Smashing,
        EnvSmashingTableModel? SmashingTable,
        EnvDamagingCoverModel? DamagingCover,
        EnvSceneryAsWeaponsModel? SceneryAsWeapons,
        IReadOnlyList<EnvSceneryRowModel>? SceneryTable,
        EnvMassiveObjectsModel? MassiveObjects,
        IReadOnlyList<EnvMassiveObjectRowModel>? MassiveObjectsTable,
        EnvToxinsModel? Toxins,
        EnvToxinOptionModel? ToxinOption,
        EnvDiseasesTableModel? DiseasesTable,
        EnvDrugsTableModel? DrugsAndPoisonsTable,
        EnvInterpretationModel? Interpretation);

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
        string Summary,
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
        string? ReductionRounds,
        string? ResolveReducesDamageTo,
        int? OnePointEveryHoursForTheLowestBand,
        int? FatalDamageIsRequiredBelowHealth);

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
        int OpenTerrainGmMayAllowRangeClassesPerPageMin,
        int OpenTerrainGmMayAllowRangeClassesPerPageMax,
        string OpenTerrainAllowanceAppliesTo,
        bool OpenTerrainAllowanceIsGmDiscretion);

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
        string AreaAttackCappedBy,
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
        string PartialSurpriseLimitPrintedAs,
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
        int? OnePointEveryHours);

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
        string ZeroOrLessParenthetical,
        string AppliesTo,
        int CostResolveToIgnore,
        int PagesIgnoredPerResolvePoint);

    private sealed record CombatEntry(
        string Id,
        string Name,
        string Kind,
        string PrintedUnder,
        string? PrintedUnderNote,
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
        string PrintedUnder,
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
        WoundPenaltiesModel? WoundPenalties,
        CombatInterpretationModel? Interpretation);

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
    private static PlayFile<EquipmentEntry> Equipment() => Load<EquipmentEntry>("equipment.json");
    private static PlayFile<EnvironmentEntry> Environment() => Load<EnvironmentEntry>("environment.json");

    private static MetaEntry MetaEntryById(string id) => Meta().Entries.Single(e => e.Id == id);
    private static CombatEntry CombatEntryById(string id) => Combat().Entries.Single(e => e.Id == id);
    private static GrittyEntry GrittyEntryById(string id) => Gritty().Entries.Single(e => e.Id == id);
    private static ChallengeEntry ChallengeEntryById(string id) => Challenge().Entries.Single(e => e.Id == id);
    private static ResolveEntry ResolveEntryById(string id) => Resolve().Entries.Single(e => e.Id == id);
    private static EquipmentEntry EquipmentEntryById(string id) => Equipment().Entries.Single(e => e.Id == id);
    private static EnvironmentEntry EnvironmentEntryById(string id) => Environment().Entries.Single(e => e.Id == id);

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
    /// <b>The eleven Powers Chapter 5 names as Resolve-exempt, checked against what
    /// <c>powers.json</c> actually makes the engine answer.</b> The character rules answer this
    /// question through <see cref="DerivedStatsCalculator.ResolveAffectedByPower"/> — an explicit
    /// <c>affects_resolve</c> if there is one, and the Movement/Sensory category default otherwise
    /// — and until now nothing compared that answer to the page it came from.
    ///
    /// <para><b>One of the eleven needs a mapping and it is ours, not the book's.</b> "Swinging" is
    /// <c>swing_line</c> in the rules data. "Super Senses" needs none — its sixteen entries are all
    /// prefixed with the printed name, because the book prints sixteen options under one Power —
    /// and the mapping lives here rather than in the JSON precisely because it is a reading:
    /// <c>resolve.json</c> transcribes the printed names and nothing else.</para>
    ///
    /// <para><b>Ten of the eleven are exemptions and are checked as exemptions. The eleventh is
    /// not one, and asking the entry about it answers a different question.</b> p.83 exempts
    /// "Expertise (except for combat skills)", which is a carve-out: what the page governs is the
    /// <em>purchase</em>, and the entry's <c>affects_resolve: false</c> is only the default the
    /// carve-out is an exception to. A loop that required <c>false</c> of that flag would be
    /// satisfied by an engine that had lost the carve-out entirely — the flag is what stays put
    /// when the rule is deleted. So Expertise is asked per selection instead, both ways round:
    /// nominated to a Talent it is exempt, and nominated to one of the Abilities in
    /// <c>affects_resolve_when_nominated</c> it counts. That is what p.83 actually says, and it is
    /// the one printed name on this list whose answer is not a property of an entry.</para>
    /// </summary>
    [Fact]
    public void EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData()
    {
        var named = ResolveEntryById("resolve_exceptions").Exceptions;

        Assert.NotNull(named);
        Assert.Equal(CanonicalResolveRules.NamedResolveExemptPowers, named.NamedPowers);

        var faults = new List<string>();
        var matched = 0;
        var askedPerSelection = 0;

        foreach (var printedName in named.NamedPowers)
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

                // The carve-out entry: the page's claim is about a purchase, so ask about one.
                if (power.AffectsResolveWhenNominated.Count > 0)
                {
                    askedPerSelection++;

                    var exemptNomination = _f.Rules.Talents[0].Id;
                    var countingNomination = power.AffectsResolveWhenNominated[0];

                    if (_f.Derived.ResolveAffectedBySelection(
                            new SelectedPower(power.Id, 1) { BaselineTraitId = exemptNomination }))
                    {
                        faults.Add(
                            $"'{printedName}' is named on p.{CanonicalResolveRules.ExceptionsPage} as a "
                            + $"Power that does not affect Resolve, and entry '{power.Id}' nominated to "
                            + $"the Talent '{exemptNomination}' counts towards it");
                    }

                    if (!_f.Derived.ResolveAffectedBySelection(
                            new SelectedPower(power.Id, 1) { BaselineTraitId = countingNomination }))
                    {
                        faults.Add(
                            $"p.{CanonicalResolveRules.ExceptionsPage} exempts '{printedName}' "
                            + $"'{CanonicalResolveRules.ExpertiseQualifier}', and entry '{power.Id}' "
                            + $"nominated to '{countingNomination}' is exempt anyway — the carve-out "
                            + "is gone and only the entry's default is left");
                    }

                    continue;
                }

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

        // Positive control: all eleven printed names are asked about, and Super Senses alone is
        // sixteen entries, so a lookup that had stopped matching would fault nothing and prove
        // nothing.
        Assert.True(matched >= 26, $"Only {matched} powers.json entries were reached for {named.NamedPowers.Count} printed names.");

        // And the carve-out branch was actually taken, or the paragraph above describes a check
        // that did not run and every Expertise assertion held by not being made.
        Assert.Equal(1, askedPerSelection);

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
    /// <b>The carve-out on p.83, asserted from both sides on one character.</b> The page exempts
    /// "Expertise (except for combat skills)", so an Expertise nominated to a combat skill
    /// <em>does</em> count towards the opening pool while one nominated to anything else does not.
    /// A Standard-tier 6d character with Expertise at the 12d cap therefore opens on 0 Resolve when
    /// the nomination is Might, Agility or Willpower and on 12 when it is Science — the same sheet,
    /// the same rank, two answers, and the nomination is the only thing that moved.
    ///
    /// <para><b>The nominations here are Abilities, which is all an Expertise may be nominated
    /// to.</b> Ch.2 p.28: "Your specialization must fall under one of your Abilities or Talents".
    /// This test used to nominate the Martial Arts <em>Power</em>, which is not a legal Expertise
    /// at all — the engine answered p.83's attack-or-defence question of that Power, so the case
    /// passed while asserting the carve-out against a sheet
    /// <c>EXPERTISE_NOMINATION_NOT_A_TRAIT</c> now refuses. Willpower is here because it is the
    /// half of Ch.4 p.75's Attack and Defense table an earlier reading of "combat skills" left
    /// out: defending against a Mental Power is its own row.</para>
    ///
    /// <para><b>This replaces a test that pinned the wrong answer on purpose.</b>
    /// <c>ADivergenceTheEngineCannotYetExpress</c> asserted the engine's 12 for the combat case and
    /// was written to go red the day the carve-out landed, because a divergence recorded as a test
    /// that would still pass after the fix is one nobody notices was closed. It went red, and this
    /// is what it was replaced with.</para>
    ///
    /// <para><b>Both halves are here deliberately.</b> Asserting only the combat case would be
    /// satisfied by an entry whose <c>affects_resolve</c> had simply been flipped to true, which is
    /// the opposite error and exempts nothing; asserting only the Science case would be satisfied by
    /// the engine as it stood before the fix.</para>
    /// </summary>
    [Fact]
    public void AnExpertiseNominatedToACombatSkillCountsTowardsResolve()
    {
        var tier = _f.Rules.GetTier("standard");
        Assert.NotNull(tier);

        const int abilityRank = 6;
        var cap = tier.TraitCapRank;

        // A 6d character with one specialisation bought up to the cap. Every Ability and every
        // Talent is at 6d, so the Expertise reaches the cap whatever it is nominated to and the
        // only thing that differs between the sheets below is the nomination. No Determination and
        // no Condition or Plot Hook Flaw, so the figure is the chapter's base table and nothing
        // else.
        CharacterSheet Character(string nomination)
        {
            var built = RulesFixture.StandardSheet();
            foreach (var ability in _f.Rules.Abilities) built.AbilityRanks[ability.Id] = abilityRank;
            foreach (var talent in _f.Rules.Talents) built.TalentRanks[talent.Id] = abilityRank;

            built.SelectedPowers.Add(
                new SelectedPower("expertise", cap - abilityRank) { BaselineTraitId = nomination });

            return built;
        }

        var fromTheTable = ResolveEntryById("starting_resolve").StartingResolve;
        Assert.NotNull(fromTheTable);

        // The nomination is a combat skill, so the Expertise counts, the highest relevant rank is
        // the cap, and the opening pool is nothing.
        var atTheCap = fromTheTable.AtTraitCap;

        // Nominated to a Talent instead, the Expertise is exempt however high it was bought, so the
        // highest relevant rank is the 6d Ability and the pool is six dice of room, twice over.
        var sixDiceUnderTheCap =
            fromTheTable.AtTraitCap + (cap - abilityRank) * fromTheTable.ResolvePerRankBelowCap;

        Assert.NotEqual(atTheCap, sixDiceUnderTheCap);

        // The entry's own flag is still the exemption, because the carve-out rides on the
        // nomination. A flipped flag would pass the combat cases below and break the Science one.
        var entry = _f.Rules.GetPower("expertise");
        Assert.NotNull(entry);
        Assert.False(
            DerivedStatsCalculator.ResolveAffectedByPower(entry),
            "powers.json now says Expertise affects Resolve unconditionally, which is the opposite "
            + $"error: p.{CanonicalResolveRules.ExceptionsPage} exempts it '"
            + $"{CanonicalResolveRules.ExpertiseQualifier}', so an Expertise nominated to anything "
            + "else must still be exempt. The carve-out needs the nomination, not a flipped flag.");

        foreach (var (nomination, expected) in new[]
                 {
                     ("might",     atTheCap),
                     ("agility",   atTheCap),
                     ("willpower", atTheCap),
                     ("science",   sixDiceUnderTheCap)
                 })
        {
            var sheet = Character(nomination);

            // Positive controls per sheet, because every assertion is about a rank the character
            // has to actually reach. An Expertise that came out at 6 would produce the Science
            // figure for a reason that has nothing to do with the carve-out.
            var expertise = sheet.GetPower("expertise");
            Assert.NotNull(expertise);
            Assert.Equal(cap, _f.Derived.GetEffectiveRank(expertise, sheet));
            Assert.Equal(abilityRank, sheet.AbilityRanks.Values.Max());
            Assert.Null(sheet.GetPower("determination"));
            Assert.Empty(sheet.Flaws);

            Assert.Equal(expected, _f.Derived.CalculateResolve(sheet));
        }
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

    /// <summary>
    /// <b>Chapter 4's two files, held to the same structural link Chapter 5 is.</b> Same argument
    /// as <see cref="EveryEntryNamesAHeadingPrintedOnThePageItCites"/>: a <c>source_ref</c> alone
    /// names a page, and a page carrying nine or ten printed blocks is a wide target, so every
    /// entry names the heading it was transcribed from and that heading has to exist on the page
    /// the entry cites. A wrong page and a wrong heading both fail.
    ///
    /// <para>The two files are checked together and reported together, because they are one
    /// chapter split by what a rule <em>is</em> rather than by where it is printed — three of
    /// <c>gritty.json</c>'s headings sit on p.79 beside two of <c>combat.json</c>'s, so a check
    /// scoped to one file would accept a Gritty rule citing a heading that belongs to the ordinary
    /// combat rules and the other way round.</para>
    ///
    /// <para><b>What it cannot catch, measured rather than assumed:</b> swapping one heading for
    /// <em>another heading on the same page</em>. Retagging <c>gritty_the_drop</c> from
    /// <c>THE DROP</c> to <c>FATAL DAMAGE</c> — both p.79 — walks straight through, where the same
    /// mutation across a page boundary is caught. That is the shape of the check by construction:
    /// it narrows the target from a page to the headings on that page, which is a real narrowing
    /// and not an exact one, and telling two headings on one page apart would mean matching an
    /// entry's content against the section's prose. <see cref="EveryEntryNamesAHeadingPrintedOnThePageItCites"/>
    /// has had the same property since Chapter 5 and it is written down here rather than left for
    /// the next reader to discover by mutating it.</para>
    /// </summary>
    [Fact]
    public void EveryChapterFourEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterHeadings("ch04-combat.json");

        // Positive control: the corpus lookup has to have found the chapter's headings. An empty
        // set would fault every entry, which is loud — but a set missing one page would fault only
        // the entries on it and read as a data error, so the count is asserted.
        Assert.True(headings.Count >= 55, $"Only {headings.Count} headings were read out of Chapter 4.");

        // Negative control, and it has to be a heading that really exists somewhere else: a made-up
        // one is rejected by a lookup that had lost every page number too. ACTIVE DEFENSES is the
        // Gritty rule printed on p.79 and on no other page — p.75's near-namesake is the different
        // heading ACTIVE AND PASSIVE DEFENSES, which is exactly the confusion this pairing catches.
        Assert.Contains((79, "ACTIVE DEFENSES"), headings);
        Assert.DoesNotContain((75, "ACTIVE DEFENSES"), headings);
        Assert.Contains((75, "ACTIVE AND PASSIVE DEFENSES"), headings);

        var faults = new List<string>();

        void Check(string file, string id, string sourceRef, string printedUnder)
        {
            var page = int.Parse(
                Regex.Match(sourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                CultureInfo.InvariantCulture);

            if (!headings.Contains((page, printedUnder)))
            {
                faults.Add(
                    $"{file}/{id}: printed_under '{printedUnder}' is not a heading on p.{page} "
                    + "of Chapter 4 — either the heading or the source_ref page is wrong");
            }
        }

        foreach (var entry in Combat().Entries)
            Check("combat.json", entry.Id, entry.SourceRef, entry.PrintedUnder);

        foreach (var entry in Gritty().Entries)
            Check("gritty.json", entry.Id, entry.SourceRef, entry.PrintedUnder);

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <b>The one entry whose heading does not cover the whole of it says so.</b>
    /// <c>pages_and_turns</c> takes what a page is and the one-turn-per-character rule from p.73's
    /// <c>EDGE</c>, and the sentence that ends a page from <c>ACTIONS</c> beside it. Naming one
    /// heading is therefore a slightly narrow claim, and the entry carries a note saying where the
    /// rest is — asserted here so the note cannot quietly disappear and leave a reader hunting the
    /// section named for a sentence that is not in it.
    /// </summary>
    [Fact]
    public void TheOneEntrySplitAcrossTwoHeadingsSaysWhereItsOtherHalfIsPrinted()
    {
        var entry = Combat().Entries.Single(e => e.Id == "pages_and_turns");

        Assert.Equal("EDGE", entry.PrintedUnder);
        Assert.False(string.IsNullOrWhiteSpace(entry.PrintedUnderNote));
        Assert.Contains("ACTIONS", entry.PrintedUnderNote!, StringComparison.Ordinal);

        // Both headings really are on p.73, which is what makes the note a statement about the
        // page rather than about this file.
        var headings = ChapterHeadings("ch04-combat.json");
        Assert.Contains((73, "EDGE"), headings);
        Assert.Contains((73, "ACTIONS"), headings);

        // And it is the only one: any other entry carrying the note would be an unrecorded second
        // case of the same narrowness.
        Assert.Equal(
            ["pages_and_turns"],
            Combat().Entries.Where(e => e.PrintedUnderNote is not null).Select(e => e.Id));
    }

    // ── Chapter 6: the Gear Limit, and the tables it bites on ────────────────

    /// <summary>
    /// Every feature name the three weapons tables print, longest first so the row regex below
    /// prefers <c>Armor Piercing</c> to <c>Armor</c>. <b>It is a closed vocabulary on purpose</b>:
    /// the parse below is what proves the pairing, and a parse that accepted any word at all would
    /// tile any text and prove nothing.
    /// </summary>
    private static readonly string[] WeaponFeatureNames =
    [
        "Armor Piercing", "Line of Sight", "Two-Handed", "Penetrating", "Versatile", "Flexible",
        "Irritant", "Launcher", "Readied", "Binding", "Braced", "Dazzle", "Ensnare", "Shield",
        "Shock", "Burst", "Stun", "Thrown", "Area"
    ];

    /// <summary>
    /// One printed row of a weapons table's right-hand block: the Weapon Bonus, its optional
    /// subdual marker, and the features cell.
    /// </summary>
    private static Regex WeaponBonusRow()
    {
        var feature = "(?:" + string.Join('|', WeaponFeatureNames.Select(Regex.Escape)) + ")";

        return new Regex(
            @"(?<bonus>\+\d+d|—)(?<sub> \(s\))? (?<features>—|"
            + feature + "(?:, " + feature + ")*)",
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// <b>The three weapons tables are paired out of the corpus, not typed into a canonical
    /// file.</b>
    ///
    /// <para>The book prints each table as four columns — type, class, Weapon Bonus, features —
    /// and the extractor reads a three-or-more-column table across rather than down, so each one
    /// arrives as two blocks: the names beside their class, and the bonuses beside their features.
    /// <b>Pairing the two blocks row by row is this project's reading</b>, which is why each entry
    /// carries an <c>interpretation</c> saying so and why the rows are on
    /// <see cref="DerivedPaths"/> rather than in <see cref="CanonicalEquipmentRules"/>: sixty-three
    /// rows transcribed a second time would be a second thing to disagree with the first, where the
    /// corpus is the book.</para>
    ///
    /// <para><b>What anchors the pairing is that three of its rows are printed a second time, in
    /// prose.</b> p.87's Gear Limit paragraph gives the pistol two dice, p.87's close-combat
    /// exception gives the battle axe three, and Ch.4 p.80's own worked example gives a basic sword
    /// two — three sentences in two chapters, none of them in a table. A misalignment of one row
    /// moves all three.</para>
    ///
    /// <para><b>Two controls on the parse itself.</b> Each block is required to <em>tile</em>: the
    /// rows are matched end to end from the first character, and what is left over must not begin
    /// another row — the advanced table is followed on the same page by its feature glossary, so
    /// "there is nothing else here" is a claim worth making. And the parser is driven past the end
    /// of a block and required to fail, because a parser that quietly returned a short list would
    /// make a truncated table agree with a truncated expectation.</para>
    /// </summary>
    [Fact]
    public void TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns()
    {
        var sections = ChapterSixSections();

        var typeBlocks = sections.Where(s => TypeRows(s.Text).Count >= 15).ToList();

        var bonusBlocks = sections
            .Where(s => s.Heading.Contains("WEAPONS —", StringComparison.Ordinal)
                        && s.Heading.Contains("BONUS FEATURES", StringComparison.Ordinal))
            .ToList();

        // The controls on finding the blocks at all. The Armor table's heading also ends in BONUS
        // FEATURES and is deliberately not one of these three.
        Assert.Equal(3, typeBlocks.Count);
        Assert.Equal(3, bonusBlocks.Count);

        int[] sizes =
        [
            CanonicalEquipmentRules.WeaponTableSizes.Ancient,
            CanonicalEquipmentRules.WeaponTableSizes.Modern,
            CanonicalEquipmentRules.WeaponTableSizes.Advanced
        ];

        string[] entryIds = ["ancient_weapons", "modern_weapons", "advanced_weapons"];

        var everyRow = new List<WeaponRowModel>();

        for (var table = 0; table < 3; table++)
        {
            var names = TypeRows(typeBlocks[table].Text);

            Assert.Equal(sizes[table], names.Count);

            var (rows, rest) = BonusRows(bonusBlocks[table].Text, names.Count);

            // The tiling control: nothing after the last row starts another one. The advanced
            // table is followed by the feature glossary on the same page, so this is the assertion
            // that says the table was read to its end and no further.
            var next = WeaponBonusRow().Match(rest);
            Assert.False(
                next.Success && next.Index == 0,
                $"{entryIds[table]}: the corpus block carries another row after the "
                + $"{names.Count} the table is supposed to have — '{next.Value}'");

            var expected = names
                .Zip(rows, (n, r) => new WeaponRowModel(n.Name, n.Class, r.BonusDice, r.Subdual, r.Features))
                .ToList();

            var shipped = EquipmentEntryById(entryIds[table]).Weapons;

            Assert.NotNull(shipped);
            Assert.Equal(expected.Count, shipped!.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Name, shipped[i].Name);
                Assert.Equal(expected[i].Class, shipped[i].Class);
                Assert.Equal(expected[i].BonusDice, shipped[i].BonusDice);
                Assert.Equal(expected[i].Subdual, shipped[i].Subdual);
                Assert.Equal(expected[i].Features, shipped[i].Features);
            }

            everyRow.AddRange(expected);
        }

        // The three prose anchors, each read out of the derived rows rather than out of the file.
        int? BonusOf(string name) =>
            everyRow.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal)).BonusDice;

        Assert.Equal(
            CanonicalEquipmentRules.AnchorRows.PistolBonusDice,
            BonusOf(CanonicalEquipmentRules.AnchorRows.PistolName));

        Assert.Equal(
            CanonicalEquipmentRules.AnchorRows.BattleAxeBonusDice,
            BonusOf(CanonicalEquipmentRules.AnchorRows.BattleAxeName));

        Assert.Equal(
            CanonicalGrittyRules.GearLimit.WorkedExampleWeaponBonusDice,
            BonusOf(CanonicalEquipmentRules.AnchorRows.SwordName));

        // And the parser really can run out, so a block that had lost a row could not agree with an
        // expectation that had lost the same one.
        Assert.Throws<InvalidOperationException>(
            () => BonusRows(bonusBlocks[0].Text, sizes[0] + 1));
    }

    /// <summary>
    /// <b>Chapter 4 and Chapter 6 both print the Gear Limit, and the two transcriptions agree.</b>
    ///
    /// <para>p.80 states the rule and points at Chapter 6 for the detail; p.87 is that detail. The
    /// default rank and the raised options are printed in both places, so this is the same kind of
    /// second printing <c>corroborated_by</c> exists for — and the entry carries the citation.</para>
    ///
    /// <para><b>Each chapter's worked example is replayed through the shipped JSON</b>, and neither
    /// figure is typed here: p.80's basic sword is looked up in Chapter 6's own ancient table, and
    /// p.87's pistol in its modern one, and each is added to the default rank the file carries.
    /// Two transcriptions can agree and both be wrong; the authors' arithmetic cannot.</para>
    /// </summary>
    [Fact]
    public void TheTwoChaptersThatPrintTheGearLimitAgreeAboutIt()
    {
        var chapterFour = GrittyEntryById("gritty_raised_gear_limit").GearLimit!;
        var chapterSix = EquipmentEntryById("gear_limit").GearLimit!;
        var raised = EquipmentEntryById("raising_the_gear_limit").RaisedLimit!;

        Assert.Equal(chapterFour.DefaultRank, chapterSix.DefaultRank);
        Assert.Equal(chapterFour.RaisedOptions, raised.RaisedOptions);
        Assert.Equal(chapterFour.RaisedOptionsAreOpenEnded, raised.RaisedOptionsAreOpenEnded);

        // The citation is on the entry, so a reader lands on the other printing.
        Assert.Contains(
            EquipmentEntryById("raising_the_gear_limit").CorroboratedBy ?? [],
            reference => reference.Contains("p.80", StringComparison.Ordinal));

        int BonusOf(string entryId, string weapon) =>
            EquipmentEntryById(entryId).Weapons!
                .Single(w => string.Equals(w.Name, weapon, StringComparison.Ordinal))
                .BonusDice!.Value;

        // p.80's sword: 6 + 2 = 8, with the 2 read out of Chapter 6's ancient table.
        var sword = BonusOf("ancient_weapons", CanonicalEquipmentRules.AnchorRows.SwordName);

        Assert.Equal(chapterFour.WorkedExampleWeaponBonusDice, sword);
        Assert.Equal(
            chapterFour.WorkedExampleMaximumEffectiveRankAtTheDefaultLimit,
            chapterSix.DefaultRank + sword);

        // p.87's pistol: the same arithmetic on the other chapter's own example.
        var pistol = BonusOf("modern_weapons", CanonicalEquipmentRules.AnchorRows.PistolName);

        Assert.Equal(chapterSix.WorkedExampleWeaponBonusDice, pistol);
        Assert.Equal(chapterSix.WorkedExampleMaximumEffectiveRank, chapterSix.DefaultRank + pistol);

        // The control: the example is a weapon with a bonus on it, so the sums above are sums of
        // two figures rather than of one and a zero — and the two chapters' examples are different
        // weapons, or "they agree" would be one arithmetic done twice.
        Assert.True(sword > 0 && pistol > 0);
        Assert.NotEqual(
            chapterFour.WorkedExampleWeapon, chapterSix.WorkedExampleWeapon, StringComparer.Ordinal);

        // p.87's close-combat exception does its own arithmetic on a third weapon, and it comes out
        // of the same table: 6 + 3 = 9, which is what the unarmed 12d is allowed to beat.
        var exception = EquipmentEntryById("gear_limit_close_combat_exception").CloseCombatException!;
        var axe = BonusOf("ancient_weapons", CanonicalEquipmentRules.AnchorRows.BattleAxeName);

        Assert.Equal(exception.WorkedExampleWeaponBonusDice, axe);
        Assert.Equal(exception.WorkedExampleGearLimit, chapterSix.DefaultRank);
        Assert.Equal(exception.WorkedExampleArmedMaximumEffectiveRank, chapterSix.DefaultRank + axe);
        Assert.True(exception.WorkedExampleUnarmedRank > exception.WorkedExampleArmedMaximumEffectiveRank);
    }

    /// <summary>
    /// <b>Every Chapter 6 entry names a heading printed on the page it cites</b>, the same
    /// narrowing <see cref="EveryChapterFourEntryNamesAHeadingPrintedOnThePageItCites"/> makes and
    /// with the same known limit: it cannot tell two headings on one page apart.
    /// </summary>
    [Fact]
    public void EveryChapterSixEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterHeadings("ch06-equipment.json");

        // Positive control: the lookup found the chapter's headings at all.
        Assert.True(headings.Count >= 100, $"Only {headings.Count} headings were read out of Chapter 6.");

        // Negative control, on a heading that really exists on another page: GEAR LIMITS is p.87's
        // and no other page's.
        Assert.Contains((87, "GEAR LIMITS"), headings);
        Assert.DoesNotContain((88, "GEAR LIMITS"), headings);

        var faults = new List<string>();

        foreach (var entry in Equipment().Entries)
        {
            var page = int.Parse(
                Regex.Match(entry.SourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                CultureInfo.InvariantCulture);

            if (!headings.Contains((page, entry.PrintedUnder)))
            {
                faults.Add(
                    $"equipment.json/{entry.Id}: printed_under '{entry.PrintedUnder}' is not a "
                    + $"heading on p.{page} of Chapter 6");
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }


    // ── Chapter 7: the environment ───────────────────────────────────────────

    /// <summary>
    /// <b>Every Chapter 7 entry names a heading printed on the page it cites</b>, the same
    /// narrowing <see cref="EveryChapterFourEntryNamesAHeadingPrintedOnThePageItCites"/> makes and
    /// with the same known limit: it cannot tell two headings on one page apart.
    /// </summary>
    [Fact]
    public void EveryChapterSevenEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterHeadings("ch07-environment.json");

        // Positive control: the lookup found the chapter's headings at all.
        Assert.True(headings.Count >= 30, $"Only {headings.Count} headings were read out of Chapter 7.");

        // Negative control, on a heading that really exists on another page. FALLING is the prose
        // section on p.105; the table under it is a different heading on p.106, which is exactly
        // the confusion this pairing catches.
        Assert.Contains((105, "FALLING"), headings);
        Assert.DoesNotContain((106, "FALLING"), headings);
        Assert.Contains((106, "FALLING — DISTANCE RANK"), headings);

        var faults = new List<string>();

        foreach (var entry in Environment().Entries)
        {
            var page = int.Parse(
                Regex.Match(entry.SourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                CultureInfo.InvariantCulture);

            if (!headings.Contains((page, entry.PrintedUnder)))
            {
                faults.Add(
                    $"environment.json/{entry.Id}: printed_under '{entry.PrintedUnder}' is not a "
                    + $"heading on p.{page} of Chapter 7");
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>One Chapter 7 corpus section, looked up by its heading and printed page.</summary>
    private static string ChapterSevenBlock(string heading, int page)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch07-environment.json")));

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (!string.Equals(section.GetProperty("heading").GetString(), heading, StringComparison.Ordinal))
                continue;

            if (section.TryGetProperty("printed_page", out var printed)
                && printed.ValueKind == JsonValueKind.Number
                && printed.GetInt32() == page)
            {
                return section.GetProperty("text").GetString() ?? "";
            }
        }

        throw new InvalidOperationException($"Chapter 7 has no section '{heading}' on p.{page}.");
    }

    /// <summary>
    /// The corpus sections each interpreted Chapter 7 table is built out of, and the page they are
    /// on. The list is what <see cref="TheChapterSevenTablesAreReadOutOfTheCorpusColumns"/> reads,
    /// written down once so its shape can be asserted rather than described.
    /// </summary>
    public static TheoryData<string, string[], int> ChapterSevenInterpretedTables() => new()
    {
        { "disaster_results", ["DISASTER — MINOR", "RESULTS — MAJOR RESULTS"], 105 },
        { "lifting_table", ["LIFTING — WEIGHT EXAMPLES THRESHOLD"], 106 },
        { "scorching_table", ["SCORCHING — HEAT ELECTRICITY", "SCORCHING — RANK"], 107 },
        { "smashing_table", ["SMASHING — MATERIAL", "SMASHING — STRUCTURE"], 107 },
        { "scenery_table", ["SCENERY — SCENERY STRUCTURE RANK"], 108 },
        { "massive_objects_table", ["MASSIVE OBJECTS — OBJECT RANK RANK"], 108 },
    };

    /// <summary>
    /// <b>An interpretation that describes the wrong extraction is worse than none</b>, because it
    /// is the one place a reader is told what shape to expect before they open the corpus.
    ///
    /// <para>The six interpreted tables do not arrive the same way. Three are split into two corpus
    /// sections and have to be <em>paired</em> — Disaster Results, Scorching, Smashing. Three arrive
    /// as one section: Lifting's rows each run onto a second line, and Scenery and Massive Objects
    /// are one undivided run of cells to be <em>cut</em>. The entries said so correctly; the
    /// paragraph above <see cref="TheChapterSevenTablesAreReadOutOfTheCorpusColumns"/> and the
    /// matching one in <c>docs/guide/play-rules.md</c> both said all six were two blocks, which
    /// sends a reader looking for sections that do not exist.</para>
    ///
    /// <para><b>So the shape is derived rather than described.</b> Whether a table has one corpus
    /// section or two is a fact about <c>ch07-environment.json</c>, and the claim "two blocks" is
    /// allowed in a <c>row_alignment</c> exactly when the corpus really carries two.</para>
    ///
    /// <para><b>What this cannot do, stated rather than claimed.</b> It reads one phrase out of a
    /// paragraph of prose, so it catches a <c>row_alignment</c> that names the wrong shape and not
    /// one that names the right shape and then describes it wrongly. Measured: the Smashing entry
    /// says "two blocks" twice, so editing one of the two is a null mutation and the honest report
    /// is that it is null, not that the check has a hole. Both directions were broken and watched —
    /// Scenery's run rewritten as "two blocks", and Smashing's every occurrence removed.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ChapterSevenInterpretedTables))]
    public void EveryChapterSevenInterpretationDescribesTheShapeItsCorpusBlockHas(
        string id, string[] headings, int page)
    {
        // Positive control on the fixture: every heading named really is a section of Chapter 7 on
        // the page given, or "the corpus carries one section" would be a claim about a typo.
        foreach (var heading in headings)
            Assert.NotEmpty(ChapterSevenBlock(heading, page));

        var interpretation = EnvironmentEntryById(id).Interpretation;

        Assert.NotNull(interpretation);
        Assert.Contains("not a rule the page states", interpretation.WhatThisIs, StringComparison.Ordinal);

        var saysTwoBlocks = interpretation.RowAlignment.Contains("two blocks", StringComparison.Ordinal);

        Assert.Equal(headings.Length == 2, saysTwoBlocks);
    }

    /// <summary>
    /// Matches <paramref name="pattern"/> end to end from the first character of
    /// <paramref name="text"/>, exactly <paramref name="expected"/> times, and returns what is left.
    ///
    /// <para><b>Two controls in one helper.</b> It throws when a row does not match, so a parser
    /// cannot quietly return a short list and let a truncated block agree with a truncated
    /// expectation; and the caller checks that the remainder does not begin another row, which is
    /// how "the table was read to its end and no further" gets asserted.</para>
    /// </summary>
    private static (List<Match> Rows, string Remainder) Tile(string text, Regex pattern, int expected)
    {
        var rows = new List<Match>();
        var at = 0;

        for (var i = 0; i < expected; i++)
        {
            var match = pattern.Match(text, at);

            if (!match.Success || match.Index != at)
            {
                throw new InvalidOperationException(
                    $"Row {i} does not tile the corpus block at character {at}: "
                    + $"'{text[at..Math.Min(text.Length, at + 60)]}'");
            }

            rows.Add(match);
            at = match.Index + match.Length;
            while (at < text.Length && (text[at] == ' ' || text[at] == '\n')) at++;
        }

        return (rows, text[at..]);
    }

    private static void AssertNothingElseTiles(string entryId, string rest, Regex pattern)
    {
        var next = pattern.Match(rest);

        Assert.False(
            next.Success && next.Index == 0,
            $"{entryId}: the corpus block carries another row after the ones the table is supposed "
            + $"to have — '{next.Value}'");
    }

    /// <summary>
    /// <b>Chapter 7's ten tables are read out of the corpus, not typed into a canonical file.</b>
    ///
    /// <para>Six of them need a reading first, because the extractor reads a table of three or more
    /// columns across rather than down: the Disaster Results table, the Lifting table, the Scorching
    /// table, the Smashing table, the Scenery table and the Massive Objects table. <b>The six do not
    /// all arrive the same way, and this sentence said they did.</b> Only three arrive as two corpus
    /// sections to be paired — Disaster Results, Scorching and Smashing. Lifting arrives as one
    /// section whose every row spans two lines. Scenery and Massive Objects arrive as one section
    /// apiece, an undivided run of name, rank, rank that has to be cut. Each entry's own
    /// <c>row_alignment</c> already said which of the three it was;
    /// <see cref="EveryChapterSevenInterpretationDescribesTheShapeItsCorpusBlockHas"/> now holds it
    /// to the corpus. <b>Pairing the blocks and cutting the runs into rows is this
    /// project's reading</b>, which is why each of those six entries carries an
    /// <c>interpretation</c> saying so and why the rows are on <see cref="DerivedPaths"/> — a
    /// hundred and twenty rows transcribed a second time would be a second thing to disagree with
    /// the first, where the corpus is the book. The other four the extractor prints straight and
    /// they are derived for the same reason.</para>
    ///
    /// <para><b>What anchors each alignment is something printed a second time.</b> The disaster
    /// counts (3 goals and 9) are in the prose above their table; the lifting bands interlock, each
    /// floor being the band below's ceiling; the scorching examples alternate strictly between a
    /// heat source and an electrical one; the smashing footnote is printed on the last material and
    /// explained at the foot of the ranks, so the two blocks are known to end together; and both
    /// rank tables print a ceiling that is their own row's rank plus the six dice p.108 allows.</para>
    ///
    /// <para><b>Two controls on every parse.</b> Each block has to <em>tile</em> — rows matched end
    /// to end from the first character, with nothing left that begins another row — and
    /// <see cref="Tile"/> throws rather than returning short, so a block that had lost a row could
    /// not agree with an expectation that had lost the same one. The row counts come from
    /// <see cref="EnvRules.TableSizes"/>, because a derivation cannot notice a
    /// table that has lost half of itself when the expectation lost the same half.</para>
    /// </summary>
    [Fact]
    public void TheChapterSevenTablesAreReadOutOfTheCorpusColumns()
    {
        // ── Disaster Results, p.105: two blocks, three columns ───────────────
        var disasterBlock = ChapterSevenBlock("DISASTER — MINOR", 105)
                            + " " + ChapterSevenBlock("RESULTS — MAJOR RESULTS", 105);

        var disasterRow = new Regex(
            @"(?<minor>\d+) Goals? Accomplished (?<majorMin>\d+)(?: (?:or|to) (?<majorMax>\d+))? "
            + @"Goals? Accomplished (?<who>GM with Embellishment|Players with Embellishment|GM|Players)",
            RegexOptions.CultureInvariant);

        var (disasterRows, disasterRest) =
            Tile(disasterBlock, disasterRow, EnvRules.TableSizes.DisasterResults);

        AssertNothingElseTiles("disaster_results", disasterRest, disasterRow);

        var disasters = EnvironmentEntryById("disaster_results").DisasterResults!;
        Assert.Equal(disasterRows.Count, disasters.Count);

        for (var i = 0; i < disasterRows.Count; i++)
        {
            var minor = int.Parse(disasterRows[i].Groups["minor"].Value, CultureInfo.InvariantCulture);
            var majorMin = int.Parse(disasterRows[i].Groups["majorMin"].Value, CultureInfo.InvariantCulture);
            var majorMax = disasterRows[i].Groups["majorMax"].Success
                ? int.Parse(disasterRows[i].Groups["majorMax"].Value, CultureInfo.InvariantCulture)
                : majorMin;
            var who = disasterRows[i].Groups["who"].Value;

            Assert.Equal(minor, disasters[i].MinorGoalsAchievedMin);
            Assert.Equal(minor, disasters[i].MinorGoalsAchievedMax);
            Assert.Equal(majorMin, disasters[i].MajorGoalsAchievedMin);
            Assert.Equal(majorMax, disasters[i].MajorGoalsAchievedMax);
            Assert.Equal(who.StartsWith("GM", StringComparison.Ordinal) ? "gm" : "players", disasters[i].Narrator);
            Assert.Equal(who.Contains("Embellishment", StringComparison.Ordinal), disasters[i].Embellishment);
        }

        // The anchor: the prose above the table gives a minor disaster 3 goals and a major one 9,
        // and only one alignment of the two blocks ends the two columns on those figures.
        Assert.Equal(EnvRules.Disasters.MinorGoals, disasters[^1].MinorGoalsAchievedMax);
        Assert.Equal(EnvRules.Disasters.MajorGoals, disasters[^1].MajorGoalsAchievedMax);

        // ── Energy Types, p.105: the names, without the description column ───
        var energyBlock = ChapterSevenBlock("TYPES — DESCRIPTION", 105);

        var energyNames = Regex
            .Matches(energyBlock, @"(?:^|\. )(?<name>[A-Z][A-Za-z]+(?:/[A-Za-z]+)+|Cosmic Energy/Radiation)")
            .Select(m => m.Groups["name"].Value)
            .ToList();

        Assert.Equal(EnvRules.TableSizes.EnergyTypes, energyNames.Count);
        Assert.Equal(energyNames, EnvironmentEntryById("energy_types").EnergyTypes!.Types);

        // ── Falling, p.106 ──────────────────────────────────────────────────
        var fallingRow = new Regex(
            @"(?:Up to (?<feet>\d+) Feet|(?<any>Any Farther)) (?<rank>\d+)d",
            RegexOptions.CultureInvariant);

        var (fallingRows, fallingRest) = Tile(
            ChapterSevenBlock("FALLING — DISTANCE RANK", 106),
            fallingRow,
            EnvRules.TableSizes.Falling);

        AssertNothingElseTiles("falling_table", fallingRest, fallingRow);

        var falling = EnvironmentEntryById("falling_table").FallingTable!;
        Assert.Equal(fallingRows.Count, falling.Count);

        for (var i = 0; i < fallingRows.Count; i++)
        {
            Assert.Equal(
                fallingRows[i].Groups["any"].Success
                    ? null
                    : int.Parse(fallingRows[i].Groups["feet"].Value, CultureInfo.InvariantCulture),
                falling[i].UpToFeet);

            Assert.Equal(
                int.Parse(fallingRows[i].Groups["rank"].Value, CultureInfo.InvariantCulture),
                falling[i].Rank);
        }

        // ── Lifting, p.106: three columns, and a weight cell over two lines ──
        var liftingLines = ChapterSevenBlock("LIFTING — WEIGHT EXAMPLES THRESHOLD", 106)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(EnvRules.TableSizes.Lifting * 2, liftingLines.Length);

        var lifting = EnvironmentEntryById("lifting_table").LiftingTable!;
        Assert.Equal(EnvRules.TableSizes.Lifting, lifting.Count);

        // The first row's cells sit on the first of its two lines, and every later row's on the
        // second — which is the reading, and it is what the interlocking bands below anchor.
        var first = Regex.Match(liftingLines[0], @"^(?<head>\S+) (?<examples>.+?) (?<threshold>\d+)$");
        Assert.True(first.Success, $"The Lifting table's first line does not parse: '{liftingLines[0]}'");

        Assert.Equal($"{first.Groups["head"].Value} {liftingLines[1]}", lifting[0].Weight);
        Assert.Equal(first.Groups["examples"].Value.Split(", "), lifting[0].Examples);
        Assert.Equal(int.Parse(first.Groups["threshold"].Value, CultureInfo.InvariantCulture), lifting[0].Threshold);

        for (var row = 1; row < lifting.Count; row++)
        {
            var head = liftingLines[row * 2];
            var rest = Regex.Match(liftingLines[(row * 2) + 1], @"^(?<examples>.+?) (?<threshold>\d+) (?<tail>.+)$");

            Assert.True(rest.Success, $"The Lifting table's row {row} does not parse: '{liftingLines[(row * 2) + 1]}'");

            Assert.Equal($"{head} {rest.Groups["tail"].Value}", lifting[row].Weight);
            Assert.Equal(rest.Groups["examples"].Value.Split(", "), lifting[row].Examples);
            Assert.Equal(int.Parse(rest.Groups["threshold"].Value, CultureInfo.InvariantCulture), lifting[row].Threshold);
        }

        // The anchor: the thresholds run 1 to 12 with no gap, and each band's floor is the band
        // below it's ceiling — "500 Pounds to 1 Ton" under "100 Pounds to 500 Pounds". A row paired
        // with the wrong line breaks both.
        for (var row = 0; row < lifting.Count; row++)
        {
            Assert.Equal(row + 1, lifting[row].Threshold);

            if (row == 0) continue;

            var floor = lifting[row].Weight.Split(" to ")[0];
            Assert.EndsWith(floor, lifting[row - 1].Weight, StringComparison.Ordinal);
        }

        // ── Scorching, p.107: two blocks, three columns ──────────────────────
        var scorching = EnvironmentEntryById("scorching_table").ScorchingTable!;
        Assert.Equal(EnvRules.TableSizes.Scorching, scorching.Count);

        var scorchingExamples = ChapterSevenBlock("SCORCHING — HEAT ELECTRICITY", 107);
        var scorchingRanks = ChapterSevenBlock("SCORCHING — RANK", 107)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(EnvRules.TableSizes.Scorching, scorchingRanks.Length);

        // The example block is tiled with the shipped names in the shipped order, which is what
        // makes it a check: a row whose two cells had been swapped, or a pair out of order, would
        // not reproduce the block. The anchor is the alternation itself — a heat source, then an
        // electrical one — so an alignment one cell out puts two fires in a row.
        var rebuilt = string.Join(' ', scorching.Select(r => $"{r.Heat} {r.Electricity}"));
        Assert.Equal(scorchingExamples, rebuilt);

        for (var i = 0; i < scorching.Count; i++)
        {
            Assert.Equal($"{scorching[i].Rank}d", scorchingRanks[i]);
        }

        // ── Smashing, p.107: the ranks are derived, the row grouping is not ──
        var smashing = EnvironmentEntryById("smashing_table").SmashingTable!;
        Assert.Equal(EnvRules.TableSizes.Smashing, smashing.Rows.Count);

        var materialBlock = ChapterSevenBlock("SMASHING — MATERIAL", 107);
        var structureBlock = ChapterSevenBlock("SMASHING — STRUCTURE", 107)
            .Split('\n')[0]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(EnvRules.TableSizes.Smashing, structureBlock.Length);

        // The one grouping the corpus cannot yield: "…Ice, Rope Plastic, Rubber, Wood Brick…" has
        // no recoverable row boundary, because the separator inside a row and the separator between
        // two rows differ only by a comma. So the grouping is transcribed in the canonical file and
        // proved to TILE the block — the asterisk on the last material is what says the two blocks
        // end together, and it is added back here because the transcription drops it.
        var tiled = string.Join(
            ' ',
            EnvRules.SmashingMaterials.Select(row => string.Join(", ", row)));

        Assert.Equal(materialBlock, tiled + "*");

        for (var i = 0; i < smashing.Rows.Count; i++)
        {
            Assert.Equal(EnvRules.SmashingMaterials[i], smashing.Rows[i].Materials);
            Assert.Equal($"{smashing.Rows[i].Structure}d", structureBlock[i]);
        }

        // ── Scenery and Massive Objects, p.108: three columns read across ────
        var rankRow = new Regex(@"(?<names>.+?) (?<a>\d+)d (?<b>\d+)d", RegexOptions.CultureInvariant);

        var (sceneryRows, sceneryRest) = Tile(
            ChapterSevenBlock("SCENERY — SCENERY STRUCTURE RANK", 108),
            rankRow,
            EnvRules.TableSizes.Scenery);

        AssertNothingElseTiles("scenery_table", sceneryRest, rankRow);

        var scenery = EnvironmentEntryById("scenery_table").SceneryTable!;
        Assert.Equal(sceneryRows.Count, scenery.Count);

        for (var i = 0; i < sceneryRows.Count; i++)
        {
            Assert.Equal(sceneryRows[i].Groups["names"].Value.Split(", "), scenery[i].Scenery);
            Assert.Equal(int.Parse(sceneryRows[i].Groups["a"].Value, CultureInfo.InvariantCulture), scenery[i].Structure);
            Assert.Equal(int.Parse(sceneryRows[i].Groups["b"].Value, CultureInfo.InvariantCulture), scenery[i].MaximumAttackRank);
        }

        var (massiveRows, massiveRest) = Tile(
            ChapterSevenBlock("MASSIVE OBJECTS — OBJECT RANK RANK", 108),
            rankRow,
            EnvRules.TableSizes.MassiveObjects);

        AssertNothingElseTiles("massive_objects_table", massiveRest, rankRow);

        var massive = EnvironmentEntryById("massive_objects_table").MassiveObjectsTable!;
        Assert.Equal(massiveRows.Count, massive.Count);

        for (var i = 0; i < massiveRows.Count; i++)
        {
            Assert.Equal(massiveRows[i].Groups["names"].Value.Split(", "), massive[i].Objects);
            Assert.Equal(int.Parse(massiveRows[i].Groups["a"].Value, CultureInfo.InvariantCulture), massive[i].WeightRank);
            Assert.Equal(int.Parse(massiveRows[i].Groups["b"].Value, CultureInfo.InvariantCulture), massive[i].MaximumAttackRank);
        }

        // ── Diseases, p.109 ─────────────────────────────────────────────────
        var diseaseLines = ChapterSevenBlock("DISEASES — TOXIN POWER", 109).Split('\n');
        var diseases = EnvironmentEntryById("diseases_table").DiseasesTable!;

        Assert.Equal(EnvRules.TableSizes.Diseases, diseases.Rows.Count);

        for (var i = 0; i < diseases.Rows.Count; i++)
        {
            var match = Regex.Match(
                diseaseLines[i],
                @"^(?<name>.+?)(?<star>\*)? (?<power>Slay|Stun) (?<rank>\d+)d \((?<options>[^)]*)\)(?: \*)?$");

            Assert.True(match.Success, $"The Diseases table's row {i} does not parse: '{diseaseLines[i]}'");

            Assert.Equal(match.Groups["name"].Value, diseases.Rows[i].Name);
            Assert.Equal(match.Groups["power"].Value, diseases.Rows[i].Power);
            Assert.Equal(int.Parse(match.Groups["rank"].Value, CultureInfo.InvariantCulture), diseases.Rows[i].Rank);
            Assert.Equal(match.Groups["star"].Success, diseases.Rows[i].Footnoted);
            Assert.Equal(OptionIds(match.Groups["options"].Value), diseases.Rows[i].Options);
        }

        // The row after the last is the footnote, not another row — the tiling control, in the one
        // shape this block takes.
        Assert.StartsWith("*", diseaseLines[diseases.Rows.Count], StringComparison.Ordinal);

        // ── Drugs and Poisons, p.109 ────────────────────────────────────────
        var drugRow = new Regex(
            @"(?<name>.+?)(?<star>\*)? (?<power1>Slay|Stun|Drain)(?: \((?<traits1>[^)]*)\))? "
            + @"(?<rank1>\d+)d(?: to (?<rank1b>\d+)d)?"
            + @"(?: & (?<power2>Slay|Stun|Drain)(?: \((?<traits2>[^)]*)\))? (?<rank2>\d+)d(?: to (?<rank2b>\d+)d)?)?"
            + @" \((?<options>[^)]*)\)",
            RegexOptions.CultureInvariant);

        var (drugRows, drugRest) = Tile(
            ChapterSevenBlock("DRUGS AND POISONS — TOXIN POWER", 109).Split('\n')[0],
            drugRow,
            EnvRules.TableSizes.DrugsAndPoisons);

        AssertNothingElseTiles("drugs_and_poisons_table", drugRest, drugRow);

        var drugs = EnvironmentEntryById("drugs_and_poisons_table").DrugsAndPoisonsTable!;
        Assert.Equal(drugRows.Count, drugs.Rows.Count);

        for (var i = 0; i < drugRows.Count; i++)
        {
            var row = drugRows[i];

            Assert.Equal(row.Groups["name"].Value, drugs.Rows[i].Name);
            Assert.Equal(row.Groups["star"].Success, drugs.Rows[i].Footnoted);
            Assert.Equal(OptionIds(row.Groups["options"].Value), drugs.Rows[i].Options);

            var expected = new List<EnvToxinEffectModel>();

            for (var slot = 1; slot <= 2; slot++)
            {
                if (!row.Groups[$"power{slot}"].Success) continue;

                var min = int.Parse(row.Groups[$"rank{slot}"].Value, CultureInfo.InvariantCulture);

                expected.Add(new EnvToxinEffectModel(
                    row.Groups[$"power{slot}"].Value,
                    row.Groups[$"traits{slot}"].Success
                        ? row.Groups[$"traits{slot}"].Value.Split(", ")
                        : [],
                    min,
                    row.Groups[$"rank{slot}b"].Success
                        ? int.Parse(row.Groups[$"rank{slot}b"].Value, CultureInfo.InvariantCulture)
                        : min));
            }

            Assert.Equal(expected.Count, drugs.Rows[i].Effects.Count);

            for (var e = 0; e < expected.Count; e++)
            {
                Assert.Equal(expected[e].Power, drugs.Rows[i].Effects[e].Power);
                Assert.Equal(expected[e].Traits, drugs.Rows[i].Effects[e].Traits);
                Assert.Equal(expected[e].RankMin, drugs.Rows[i].Effects[e].RankMin);
                Assert.Equal(expected[e].RankMax, drugs.Rows[i].Effects[e].RankMax);
            }
        }

        // The controls on the two toxin tables' shape, because a regex that had stopped seeing the
        // exceptional rows would tile the block just as happily: one row inflicts two Powers, two
        // print a span of ranks rather than a figure, and two name Traits in a parenthesis.
        Assert.Single(drugs.Rows, r => r.Effects.Count == 2);
        Assert.Equal(2, drugs.Rows.Count(r => r.Effects.Any(e => e.RankMax > e.RankMin)));
        Assert.Equal(2, drugs.Rows.Count(r => r.Effects.Any(e => e.Traits.Count > 0)));
    }

    /// <summary>The printed parenthesis of a toxin row, as the ids the character rules carry.</summary>
    private static List<string> OptionIds(string printed) =>
        [.. printed.Split(", ").Select(name => name.ToLowerInvariant().Replace('-', '_').Replace(' ', '_'))];

    /// <summary>
    /// <b>The ceiling column of both rank tables is the row's own rank plus the six dice p.108
    /// allows</b>, and the prose prints two of those rows a third time.
    ///
    /// <para>This is the strongest thing about the two three-column tables: the alignment of a run
    /// of "name, rank, rank" into rows is a reading, and the reading is right only if every row's
    /// second figure is its first plus the cap bonus the entry beside it transcribes. A run cut one
    /// cell out fails on every row at once.</para>
    ///
    /// <para>The two prose anchors come from outside the table: "motorcycles have 5d Body, so you
    /// can't roll more than 11d", and "most wooden telephone poles have 7d Structure (6d plus 1d for
    /// thickness), so you can't roll more than 13d". The second is the better of the two, because
    /// its printed figure is <em>not</em> the table's — the table gives the pole 6d and the prose
    /// adds one for thickness, which is the GM adjustment p.107 allows.</para>
    /// </summary>
    [Fact]
    public void TheImprovisedWeaponCeilingIsEveryRowsRankPlusTheCapBonus()
    {
        var rule = EnvironmentEntryById("scenery_as_weapons").SceneryAsWeapons!;
        var scenery = EnvironmentEntryById("scenery_table").SceneryTable!;
        var massive = EnvironmentEntryById("massive_objects_table").MassiveObjectsTable!;

        // Positive control: the tables were found and are not empty, or "every row agrees" is
        // satisfied by there being no rows.
        Assert.Equal(EnvRules.TableSizes.Scenery, scenery.Count);
        Assert.Equal(EnvRules.TableSizes.MassiveObjects, massive.Count);
        Assert.True(rule.CapBonusDice > 0);

        foreach (var row in scenery)
            Assert.Equal(row.Structure + rule.CapBonusDice, row.MaximumAttackRank);

        foreach (var row in massive)
            Assert.Equal(row.WeightRank + rule.CapBonusDice, row.MaximumAttackRank);

        // The first anchor: the motorcycle's Body and its ceiling, both printed in the prose, are
        // the row the table gives it.
        var motorcycle = scenery.Single(
            r => r.Scenery.Contains(EnvRules.SceneryAsWeapons.AnchorRowMotorcycle));

        Assert.Equal(rule.WorkedExampleObjectBody, motorcycle.Structure);
        Assert.Equal(rule.WorkedExampleMaximumAttackRank, motorcycle.MaximumAttackRank);

        // The second, and the one the table alone could not produce: the pole's printed 7d is the
        // table's 6d plus one die of thickness, which is inside the range p.107 lets a GM move a
        // Structure by, and its ceiling is that 7d plus the cap bonus.
        var pole = scenery.Single(
            r => r.Scenery.Contains(EnvRules.SceneryAsWeapons.AnchorRowTelephonePole));

        Assert.Equal(rule.SecondWorkedExampleStructureFromTheTable, pole.Structure);

        Assert.Equal(
            rule.SecondWorkedExampleObjectStructure,
            pole.Structure + rule.SecondWorkedExampleThicknessAdjustment);

        Assert.Equal(
            rule.SecondWorkedExampleMaximumAttackRank,
            rule.SecondWorkedExampleObjectStructure + rule.CapBonusDice);

        var smashing = EnvironmentEntryById("smashing").Smashing!;

        Assert.InRange(
            rule.SecondWorkedExampleThicknessAdjustment,
            smashing.GmMayAdjustStructureByMin,
            smashing.GmMayAdjustStructureByMax);

        // And the pole's printed ceiling really is not the table's row, or the anchor would be the
        // same claim as the motorcycle's made twice.
        Assert.NotEqual(rule.SecondWorkedExampleMaximumAttackRank, pole.MaximumAttackRank);
    }

    /// <summary>
    /// <b>Chapter 4 and Chapter 7 both print the cover rule, and the two transcriptions agree.</b>
    ///
    /// <para>p.75's <c>modifier_cover</c> says an attack goes through cover whose Structure it
    /// exceeds and that the target may answer with that Structure; p.108's <c>damaging_cover</c> is
    /// the same two clauses with the second option spelled out. So this is the same kind of second
    /// printing <c>corroborated_by</c> exists for elsewhere in this store, and the entry carries the
    /// citation.</para>
    ///
    /// <para><b>Chapter 4's sentence is what the expectation is built from</b>, rather than a string
    /// typed here: the two files state the rule in different words, so what is compared is the two
    /// clauses' content — that both name the cover's Structure as the threshold an attack rank has
    /// to beat, and that both grant the target that Structure as a passive defence.</para>
    ///
    /// <para><b>What this pair cannot do, stated rather than claimed.</b> The two chapters share no
    /// <em>figure</em>: Chapter 4's cover entry prints dice bands Chapter 7 does not, and Chapter 7's
    /// penetration clause prints no number at all. So the comparison is over two words in a prose
    /// clause, and a clause reworded to "the attack rank exceeds half the object's Structure" — or to
    /// require the Structure to exceed the attack rank — still carries both words and still passes.
    /// Broken and watched in the direction it does cover, from each side in turn: Chapter 4's
    /// threshold moved onto Body, then Chapter 7's, and each fails naming its own clause. There is
    /// no shared figure to compare instead, and inventing a parser over English is the heuristic
    /// this repository has twice been warned off tuning until the examples in front of it look
    /// right.</para>
    /// </summary>
    [Fact]
    public void TheTwoChaptersThatPrintDamagingCoverAgreeAboutIt()
    {
        var chapterFour = CombatEntryById("modifier_cover").Cover!;
        var chapterSeven = EnvironmentEntryById("damaging_cover").DamagingCover!;

        // Chapter 4 grants the passive defence and Chapter 7 says what it is, so the flag and the
        // phrase have to agree — a file that had dropped either would leave the other unopposed.
        Assert.True(chapterFour.TargetMayUseTheCoversStructureAsAPassiveDefense);
        Assert.Contains("passive defense", chapterSeven.TargetMayUseTheObjectsStructureAs, StringComparison.Ordinal);

        // Both name the same threshold, in each chapter's own words.
        foreach (var clause in new[] { chapterFour.AttackingThroughCoverRequires, chapterSeven.AttackPenetratesWhen })
        {
            Assert.Contains("attack rank", clause, StringComparison.Ordinal);
            Assert.Contains("Structure", clause, StringComparison.Ordinal);
        }

        // The citation is on the entry, so a reader lands on the other printing.
        Assert.Contains(
            EnvironmentEntryById("damaging_cover").CorroboratedBy ?? [],
            reference => reference.Contains("p.75", StringComparison.Ordinal));

        // The control: the two entries really are two files' worth of words rather than one copied
        // into the other, which is what this store's deferral policy exists to prevent.
        Assert.NotEqual(
            chapterFour.AttackingThroughCoverRequires,
            chapterSeven.AttackPenetratesWhen,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>Chapter 4 works out how far a throw goes and Chapter 7 says what is being thrown, so the
    /// two chapters have to mean the same thing by both halves of one formula.</b>
    ///
    /// <para>p.74's <c>throwing_range</c> prints <c>throwing rank = Might − the object's weight
    /// rank</c>. p.108 supplies both operands from the other end: <c>scenery_as_weapons</c> makes a
    /// thrown object an attack on the same Trait, and <c>massive_objects</c> is the rule that puts
    /// the very figure the formula subtracts in place of an object's Structure. Neither chapter
    /// restates the other, which is why this is a check and not a duplication — but a file that
    /// moved a throw onto Agility, or renamed the weight figure, would leave Chapter 4's formula
    /// subtracting something no page supplies.</para>
    ///
    /// <para><b>The expectation is parsed out of the formula string rather than typed here</b>, the
    /// way <see cref="TheEngineComputesTheEdgeThisChapterPrints"/> parses its operands: a formula
    /// rewritten to name a different Trait produces a different operand and fails, where two
    /// hard-coded strings would go on agreeing with each other for ever.</para>
    ///
    /// <para><b>The one thing the two chapters do not agree about is on record on both sides.</b>
    /// "Weight rank" is defined nowhere in the book, and this pair of pages is where that costs
    /// something — so <c>massive_objects</c>' <c>ambiguity</c> has to name Chapter 4's formula, or
    /// the silence would be recorded where it does no harm and not where it does.</para>
    /// </summary>
    [Fact]
    public void TheTwoChaptersThatPrintAThrowAgreeAboutItsTwoOperands()
    {
        var formula = CombatEntryById("throwing_range").Throwing!.RankFormula;

        var operands = formula[(formula.IndexOf('=', StringComparison.Ordinal) + 1)..]
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Positive control on the parse: a formula that had stopped being a subtraction of two
        // named things would leave one operand, and "both operands agree" would hold vacuously.
        Assert.Equal(2, operands.Length);
        Assert.NotEqual(operands[0], operands[1], StringComparer.Ordinal);

        var scenery = EnvironmentEntryById("scenery_as_weapons").SceneryAsWeapons!;
        var massive = EnvironmentEntryById("massive_objects").MassiveObjects!;

        // The Trait the distance is worked out from is the Trait Chapter 7 rolls the throw on.
        Assert.Equal(operands[0], scenery.ThrownAttackTrait, StringComparer.Ordinal);

        // And what the formula subtracts is exactly what Chapter 7 puts in an object's place.
        Assert.Equal(operands[1], massive.UsesInsteadOfBodyOrStructure, StringComparer.Ordinal);

        // Chapter 7 makes a thrown object a ranged attack, which is the whole reason a distance has
        // to be worked out for it at all — so Chapter 4's table is measuring this throw.
        Assert.Contains("ranged", scenery.ThrownAttackIs, StringComparison.Ordinal);

        // The gap is recorded where it bites. Chapter 4 subtracts a rank whose scale is printed two
        // hundred pages away and whose top four rows are off the end of it, and the entry that
        // supplies that rank is the one that has to say so.
        Assert.Contains(
            "Chapter 4",
            EnvironmentEntryById("massive_objects").Ambiguity ?? "",
            StringComparison.Ordinal);
    }

    /// <summary>Chapter 2 p.17's Weight table, as printed weight (lower case) to Might rank.</summary>
    private static Dictionary<string, int> ChapterTwoWeightTable()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch02-characters.json")));

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (!string.Equals(
                    section.GetProperty("heading").GetString(),
                    "WEIGHT — RANK WEIGHT RANK WEIGHT",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!section.TryGetProperty("printed_page", out var page)
                || page.ValueKind != JsonValueKind.Number
                || page.GetInt32() != 17)
            {
                continue;
            }

            return Regex
                .Matches(
                    section.GetProperty("text").GetString() ?? "",
                    @"(?<rank>\d+)d (?<weight>[\d,]+ (?:pounds|tons?|kilotons?|megatons?))")
                .ToDictionary(
                    m => m.Groups["weight"].Value.ToLowerInvariant(),
                    m => int.Parse(m.Groups["rank"].Value, CultureInfo.InvariantCulture),
                    StringComparer.Ordinal);
        }

        throw new InvalidOperationException("Chapter 2 has no Weight table on p.17.");
    }

    /// <summary>
    /// <b>"Weight rank" is defined, and the definition is in Chapter 2.</b> p.17's Might entry
    /// prints the Weight table and then says, in as many words, that "when not referring to Might,
    /// the rank that corresponds to an object's weight on this table is sometimes called its weight
    /// rank". Chapter 7 uses the phrase on p.108 and Chapter 4 subtracts it on p.74, and neither
    /// page repeats the definition — which is why both entries read as though there were not one.
    ///
    /// <para><b>Both Chapter 7 entries said there was not, and both were wrong in a way that
    /// changes results.</b> <c>massive_objects</c> offered two candidate columns and said they were
    /// not the same scale, so "the choice decides whether anything can be thrown at all";
    /// <c>lifting</c> said the static lifting maximum was "named and never printed". p.17 prints it,
    /// and this repository's own <c>data/rules/abilities.json</c> already records it under Might.
    /// Nothing here could see that, because every Chapter 7 check reads Chapter 7's corpus.</para>
    ///
    /// <para><b>What the corpus proves, rather than what this comment claims.</b> Read Chapter 2's
    /// table out of the corpus and two things fall out at once. Every Lifting band closes on a
    /// weight that table ranks, and the printed threshold is exactly <em>half</em> that rank in all
    /// twelve rows — so the threshold column cannot itself be a weight rank, which disposes of one
    /// of <c>massive_objects</c>' two candidates. And the two objects the Lifting table and the
    /// Massive Objects table have in common — an aircraft carrier and a skyscraper — carry a weight
    /// rank that lands inside the Lifting band they are printed in, which is the other candidate
    /// confirmed. Neither holds under any other reading of the scale.</para>
    ///
    /// <para>What is left open, and stays on the entry: Chapter 2 prints 1d to 24d and stops at a
    /// megaton, while the Massive Objects table runs to 69d, so its top four rows name a weight the
    /// book never gives.</para>
    /// </summary>
    [Fact]
    public void TheWeightRankChapterSevenUsesIsTheOneChapterTwoDefines()
    {
        var weights = ChapterTwoWeightTable();

        // Positive control: the table was found whole, both ends included. A partial parse would
        // silently drop the bands it could not rank and every loop below would run on fewer rows.
        Assert.Equal(24, weights.Count);
        Assert.Equal(1, weights["50 pounds"]);
        Assert.Equal(24, weights["1 megaton"]);

        var lifting = EnvironmentEntryById("lifting_table").LiftingTable!;
        var massive = EnvironmentEntryById("massive_objects_table").MassiveObjectsTable!;

        Assert.Equal(EnvRules.TableSizes.Lifting, lifting.Count);
        Assert.Equal(EnvRules.TableSizes.MassiveObjects, massive.Count);

        // Every band closes on a weight Chapter 2 ranks, and its threshold is half that rank.
        var ceilings = new List<int>();

        foreach (var band in lifting)
        {
            var ceiling = band.Weight.Split(" to ")[^1].ToLowerInvariant();

            if (ceiling.StartsWith("under ", StringComparison.Ordinal))
                ceiling = ceiling["under ".Length..];

            Assert.True(
                weights.TryGetValue(ceiling, out var rank),
                $"the Lifting band '{band.Weight}' closes on '{ceiling}', which Chapter 2's Weight "
                + "table does not print");

            Assert.Equal(0, rank % 2);
            Assert.Equal(rank / 2, band.Threshold);

            ceilings.Add(rank);
        }

        // And the bands interlock on Chapter 2's scale as well as on their printed strings.
        for (var row = 1; row < ceilings.Count; row++)
            Assert.Equal(ceilings[row - 1] + 2, ceilings[row]);

        // The other candidate, confirmed from the other end: the objects both Chapter 7 tables name
        // carry a weight rank inside the Lifting band they are printed in.
        var shared = lifting
            .SelectMany((band, index) => band.Examples.Select(name => (Name: name, Band: index)))
            .Join(
                massive.SelectMany(row => row.Objects.Select(name => (Name: name, row.WeightRank))),
                left => left.Name,
                right => right.Name,
                (left, right) => (left.Name, left.Band, right.WeightRank),
                StringComparer.Ordinal)
            .ToList();

        // Positive control: the two tables really do share rows, or the loop below proves nothing.
        Assert.Equal(2, shared.Count);

        foreach (var (name, band, weightRank) in shared)
        {
            Assert.InRange(weightRank, band == 0 ? 1 : ceilings[band - 1], ceilings[band]);
            Assert.True(weightRank > 0, $"{name} carries no weight rank");
        }

        // Both entries cite the chapter that defines the figure they use, so a reader lands on it.
        foreach (var id in new[] { "lifting", "massive_objects" })
        {
            Assert.Contains(
                EnvironmentEntryById(id).CorroboratedBy ?? [],
                reference => reference.Contains("p.17", StringComparison.Ordinal));
        }

        // And the one thing that is still open is on the entry it bites: the printed scale stops at
        // 24d and this table does not.
        Assert.Contains(massive, row => row.WeightRank > weights.Values.Max());
    }

    /// <summary>
    /// <b>The hazard ceiling cannot be what limits a minor hazard, and the entry that records the
    /// unit clash has to say which way round it runs.</b>
    ///
    /// <para>p.106 charges a minor hazard 1 point per <em>minute</em> and caps every hazard at
    /// 1 point per <em>page</em>, and no page in the book gives a number converting one into the
    /// other. <b>It does state the direction, though, and this store already carries it</b>:
    /// <c>combat.json</c>'s <c>pages_and_turns</c> transcribes p.73's "a page represents a few
    /// seconds of time in the game world", which the Glossary prints again on p.7. A page no longer
    /// than a minute makes the per-page allowance the larger of the two over any stretch of time, so
    /// the ceiling is slack against the minor rate rather than clamping it.</para>
    ///
    /// <para><b>The entry said the opposite.</b> Its <c>ambiguity</c> read that ten minutes in smoke
    /// "could have taken at most a handful by the ceiling", which is true only of a page longer than
    /// a minute and would send a simulator clamping damage the book does not clamp — a
    /// result-changing reading, on one of the two ambiguities this chapter's guide singles out as
    /// result-changing. Nothing caught it, because
    /// <see cref="TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect"/> asserts an ambiguity is
    /// <em>present</em> and says nothing at all about what it claims.</para>
    ///
    /// <para>So the comparison is made here against the unit the other file defines rather than left
    /// to prose, and the entry is required to name the fact that settles the direction. What stays
    /// genuinely open — how many pages a minute is, and what the ceiling means when a minor and a
    /// major hazard overlap — is what the rewritten ambiguity is now about.</para>
    /// </summary>
    [Fact]
    public void TheHazardCeilingIsSlackAgainstTheMinorRateItIsPrintedBeside()
    {
        var entry = EnvironmentEntryById("hostile_environments");
        var hazard = entry.HostileEnvironment!;
        var page = CombatEntryById("pages_and_turns").Page!;

        // Positive control: both rates were found and are rates. A ceiling of nothing against a rate
        // of nothing satisfies the comparison below without either figure having been read.
        Assert.True(hazard.MinorDamagePerMinuteAfterThat > 0, "the minor hazard rate is not a rate");
        Assert.True(hazard.MaximumHazardDamagePerPage > 0, "the hazard ceiling is not a rate");

        // The unit fact, taken from the file that transcribes the page it is printed on rather than
        // typed here: a page is seconds, so a minute is more than one page.
        Assert.Contains("seconds", page.APageIs, StringComparison.Ordinal);
        Assert.DoesNotContain("minute", page.APageIs, StringComparison.Ordinal);

        // Which is all the arithmetic needs. A page is no longer than a minute, so a minute buys at
        // least one page's worth of the ceiling, and the ceiling is the larger allowance.
        Assert.True(
            hazard.MaximumHazardDamagePerPage >= hazard.MinorDamagePerMinuteAfterThat,
            $"a minor hazard spends {hazard.MinorDamagePerMinuteAfterThat} a minute against a "
            + $"ceiling of {hazard.MaximumHazardDamagePerPage} a page, and a page is "
            + $"'{page.APageIs}' — so the ceiling would be what limits a minor hazard after all");

        // And the entry records that, rather than the other way round: it has to name the unit and
        // the page that settle the direction, because that is the half of this the book does answer.
        var ambiguity = entry.Ambiguity ?? "";

        Assert.Contains("seconds", ambiguity, StringComparison.Ordinal);
        Assert.Contains("p.73", ambiguity, StringComparison.Ordinal);
    }

    /// <summary>Which fields an environment entry that defers to another store may carry.</summary>
    private static readonly HashSet<string> EnvironmentReferenceOnlyFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "transcribed_here", "printed_name", "option_kind", "detail_store", "power_id", "option_id"
        };

    /// <summary>
    /// <b>An entry that says it defers must actually defer.</b> The three Pros and Cons pp.108-109
    /// print are priced on the Powers they modify in <c>data/rules/powers.json</c>, and a Hero Point
    /// figure copied into this store would be the second transcription
    /// <see cref="AnEntryThatDefersToAnotherChapterCarriesReferencesAndNothingElse"/> was written
    /// over <c>resolve.json</c> to prevent — the difference here being that the other store is the
    /// character rules rather than another chapter, which changes nothing about the argument.
    /// </summary>
    [Fact]
    public void AnEnvironmentEntryThatDefersToTheCharacterRulesCarriesReferencesAndNothingElse()
    {
        var deferring = Environment().Entries.Where(e => e.ToxinOption?.TranscribedHere == false).ToList();

        // Positive control: a rule over an empty set is satisfied by there being nothing to check.
        Assert.Equal(EnvRules.ToxinOptions.Length, deferring.Count);

        var faults = new List<string>();

        foreach (var entry in deferring)
        {
            var strangers = EntryLeaves(entry.Id, entry, stopAt: null)
                .Select(leaf => leaf.Path[(leaf.Path.LastIndexOf('.') + 1)..])
                .Where(name => !EnvironmentReferenceOnlyFields.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (strangers.Count > 0)
                faults.Add($"{entry.Id} sets transcribed_here false and still carries {string.Join(", ", strangers)}");
        }

        Assert.True(
            faults.Count == 0,
            "An entry that hands a mechanic to the character rules may carry the reference and "
            + "nothing more — the Hero Point cost belongs on the Power, where PowerProConTests "
            + "holds it to the page: " + string.Join("; ", faults));
    }

    /// <summary>
    /// <b>Every Pro or Con this chapter names resolves in the character rules.</b>
    ///
    /// <para>The three deferring entries name a Power and an option on it; the two toxin tables name
    /// an option in every row's printed parenthesis. None of them is transcribed here, so a
    /// reference that resolved to nothing would leave this store pointing at a rule that has been
    /// renamed away — which is the shape of failure <c>PlayRulesRepository</c>'s throwing lookup
    /// exists to prevent one layer down.</para>
    ///
    /// <para><b>Two of the three live on a Power rather than in <c>pros.json</c> or
    /// <c>cons.json</c>, and that is a finding rather than an inconvenience.</b> Caustic, Lethal
    /// Disease and Non-Lethal Disease are printed in Chapter 7 and are Power-specific options in
    /// <c>powers.json</c>; Toxin is Chapter 2's generic Con and is in <c>cons.json</c>. So the
    /// lookup tries both stores and the test asserts which one answered, or a reference could drift
    /// from one to the other unnoticed.</para>
    ///
    /// <para><b>A Power-specific option is resolved against the Power the row inflicts, not against
    /// <c>powers.json</c> at large.</b> All three of this section's options are printed with their
    /// scope in the sentence — "This applies only to the Slay Power", "only to the Stun Power" — so
    /// a row that hangs one off the other Power is wrong in exactly the way p.108 forbids, and a
    /// lookup asking only whether the id exists somewhere cannot see it. Measured rather than
    /// reasoned about: the Common Cold row moved onto <c>lethal_disease</c>, which is Slay's option
    /// on a Stun row, passed. A row naming two Powers — Mustard Gas is Slay and Stun — clears if the
    /// option sits on either, because the page scopes the option and not the row.</para>
    /// </summary>
    [Fact]
    public void EveryProOrConTheToxinTablesNameResolvesInTheCharacterRules()
    {
        var generic = _f.Rules.Pros.Select(p => p.Id)
            .Concat(_f.Rules.Cons.Select(c => c.Id))
            .ToHashSet(StringComparer.Ordinal);

        var onPowers = _f.Rules.Powers
            .SelectMany(power => power.PowerPros.Concat(power.PowerCons).Select(o => (power.Id, o.Id)))
            .ToHashSet();

        // Positive control on both stores: a lookup against an empty set would resolve nothing and
        // fault everything, but a lookup against a set that had lost one store would fault only the
        // references into it, and read as a data error.
        Assert.True(generic.Count >= 40, $"Only {generic.Count} generic Pro and Con ids were loaded.");
        Assert.True(onPowers.Count >= 100, $"Only {onPowers.Count} Power-specific options were loaded.");

        var faults = new List<string>();
        var resolvedOnAPower = 0;
        var resolvedGenerically = 0;

        // The three deferring entries: each names the Power its option is priced on.
        var deferring = Environment().Entries.Where(e => e.ToxinOption is not null).ToList();

        // Positive control, and it counts the entries rather than the ones that resolved. Counting
        // resolutions here would be a second way of saying what `faults` says, asserted first and
        // in figures — a misspelled option id then fails as "expected 3, actual 2" and the message
        // naming the reference that resolved to nothing is never reached. Measured: it did.
        Assert.Equal(EnvRules.ToxinOptions.Length, deferring.Count);

        foreach (var entry in deferring)
        {
            var option = entry.ToxinOption!;

            Assert.Equal(EnvRules.ToxinOptionDetailStore, option.DetailStore);

            if (onPowers.Contains((option.PowerId, option.OptionId))) resolvedOnAPower++;
            else faults.Add($"{entry.Id}: powers.json has no '{option.OptionId}' on the Power '{option.PowerId}'");
        }

        // And every option either table's rows name, wherever it lives — <b>against the Power that
        // row inflicts</b>, not merely somewhere in powers.json. All three of this section's options
        // are printed "this applies only to the Slay Power" or "only to the Stun Power", so a row is
        // wrong in exactly the way the page forbids when it hangs one off the other Power, and a
        // lookup that only asked whether the id exists somewhere cannot see that. Measured: a
        // Common Cold row moved onto `lethal_disease` — Slay's option, on a Stun row — passed.
        var referenced = EnvironmentEntryById("diseases_table").DiseasesTable!.Rows
            .Select(r => (Row: r.Name, Powers: (IReadOnlyList<string>)[r.Power], r.Options))
            .Concat(EnvironmentEntryById("drugs_and_poisons_table").DrugsAndPoisonsTable!.Rows
                .Select(r => (
                    Row: r.Name,
                    Powers: (IReadOnlyList<string>)[.. r.Effects.Select(e => e.Power)],
                    r.Options)))
            .ToList();

        Assert.Equal(
            EnvRules.TableSizes.Diseases + EnvRules.TableSizes.DrugsAndPoisons,
            referenced.Count);

        foreach (var (row, powers, options) in referenced)
        {
            Assert.NotEmpty(options);

            // The Power a row names is the printed name; powers.json keys on the id. A row whose
            // Power resolved to nothing would make every Power-specific lookup below fail open.
            var powerIds = powers
                .Select(name => name.ToLowerInvariant())
                .ToList();

            Assert.NotEmpty(powerIds);

            foreach (var powerId in powerIds)
            {
                Assert.Contains(
                    powerId,
                    _f.Rules.Powers.Select(p => p.Id),
                    StringComparer.Ordinal);
            }

            foreach (var id in options)
            {
                if (generic.Contains(id)) resolvedGenerically++;
                else if (powerIds.Any(powerId => onPowers.Contains((powerId, id)))) resolvedOnAPower++;
                else if (onPowers.Any(pair => string.Equals(pair.Item2, id, StringComparison.Ordinal)))
                {
                    faults.Add(
                        $"'{row}' inflicts {string.Join(" and ", powers)} and names the Pro or Con "
                        + $"'{id}', which powers.json carries on "
                        + string.Join(", ", onPowers.Where(p => p.Item2 == id).Select(p => $"'{p.Item1}'"))
                        + " and not on that Power");
                }
                else faults.Add($"'{row}' names the Pro or Con '{id}', which is in neither store");
            }
        }

        // Both halves have to have fired, or "everything resolved" would be a claim about one store
        // with the other never consulted.
        Assert.True(resolvedGenerically > 0 && resolvedOnAPower > EnvRules.ToxinOptions.Length,
            $"{resolvedGenerically} references resolved generically and {resolvedOnAPower} on a Power.");

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>One Chapter 6 section: its heading and its prose.</summary>
    private sealed record CorpusSection(string Heading, int Page, string Text);

    private static IReadOnlyList<CorpusSection> ChapterSixSections()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch06-equipment.json")));

        return
        [
            .. document.RootElement.GetProperty("sections").EnumerateArray()
                .Select(section => new CorpusSection(
                    section.GetProperty("heading").GetString() ?? "",
                    section.TryGetProperty("printed_page", out var page)
                        && page.ValueKind == JsonValueKind.Number
                            ? page.GetInt32()
                            : 0,
                    section.GetProperty("text").GetString() ?? ""))
        ];
    }

    /// <summary>
    /// The left-hand block of a weapons table, as (type, class) rows: words accumulate until
    /// <c>Melee</c> or <c>Ranged</c> closes a row. A block whose last row does not close is not a
    /// type block and yields nothing.
    /// </summary>
    private static List<(string Name, string Class)> TypeRows(string text)
    {
        var rows = new List<(string, string)>();
        var current = new List<string>();

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(token, "Melee", StringComparison.Ordinal)
                || string.Equals(token, "Ranged", StringComparison.Ordinal))
            {
                rows.Add((string.Join(' ', current), token));
                current.Clear();
            }
            else
            {
                current.Add(token);
            }
        }

        return current.Count == 0 ? rows : [];
    }

    /// <summary>
    /// The right-hand block of a weapons table, as <paramref name="count"/> rows, and whatever text
    /// follows them. <b>It throws rather than returning a short list</b>, because a parser that
    /// stopped early would let a truncated block agree with a truncated expectation.
    /// </summary>
    private static (List<(int? BonusDice, bool Subdual, IReadOnlyList<string> Features)>, string)
        BonusRows(string text, int count)
    {
        var row = WeaponBonusRow();
        var rows = new List<(int?, bool, IReadOnlyList<string>)>();
        var at = 0;

        for (var i = 0; i < count; i++)
        {
            var match = row.Match(text, at);

            if (!match.Success || match.Index != at)
            {
                throw new InvalidOperationException(
                    $"row {i + 1} of {count} does not start at character {at} of the corpus block: "
                    + $"'{text[at..Math.Min(text.Length, at + 40)]}'");
            }

            var bonus = match.Groups["bonus"].Value;
            var features = match.Groups["features"].Value;

            rows.Add((
                string.Equals(bonus, "—", StringComparison.Ordinal)
                    ? null
                    : int.Parse(bonus[1..^1], CultureInfo.InvariantCulture),
                match.Groups["sub"].Success,
                string.Equals(features, "—", StringComparison.Ordinal)
                    ? []
                    : features.Split(", ", StringSplitOptions.None)));

            at = match.Index + match.Length;
            if (at < text.Length && text[at] == ' ') at++;
        }

        return (rows, text[at..]);
    }

    /// <summary>Every (printed page, heading) pair in one chapter of the corpus.</summary>
    private static HashSet<(int Page, string Heading)> ChapterHeadings(string file)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, file)));

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

    /// <summary>Every (printed page, heading) pair in the Chapter 5 corpus.</summary>
    private static HashSet<(int Page, string Heading)> ChapterFiveHeadings() =>
        ChapterHeadings("ch05-resolve-and-adversity.json");

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
    /// <para><b>Both the threshold and the rescue read off the entry rather than being re-derived in
    /// the test.</b> The threshold check is not just <c>-full</c>: it also requires <c>fatal.KilledAt</c>
    /// to equal the transcribed sentence, so a mutation to that field — as opposed to a mutation to the
    /// arithmetic this test does on its own — has somewhere to be caught. The rescue direction comes
    /// from <c>Interpretation.ResolveReducesDamageTo</c>, the reading <c>TheFatalDamagePrintedWordAndItsWorkedExampleDisagree</c>
    /// proves the worked example actually supports, rather than from the printed <c>fatal_damage.resolve_reduces_damage_to</c>
    /// word, which that same test proves is contradicted. Mutating either <c>killed_at</c> or
    /// <c>resolve_reduces_damage_to</c> — the fact field or the interpretation — now moves this
    /// fixture.</para>
    /// </summary>
    [Fact]
    public void TheFatalDamageExampleOnPageSeventyNineComesOutAsPrinted()
    {
        var entry = GrittyEntryById("gritty_fatal_damage");
        var fatal = entry.FatalDamage;
        var interpretation = entry.Interpretation;

        Assert.NotNull(fatal);
        Assert.NotNull(interpretation);
        Assert.True(fatal.HealthCanGoNegative);

        // The threshold is read off the entry's own killed_at field, checked against the
        // transcription rather than assumed — a mutation to killed_at has to land here.
        Assert.Equal(CanonicalGrittyRules.FatalDamage.KilledAt, fatal.KilledAt);
        Assert.Equal(CanonicalGrittyRules.FatalDamage.ResolveReducesDamageTo, fatal.ResolveReducesDamageTo);

        var full = CanonicalGrittyRules.FatalDamage.ExampleFullHealth;
        var after = CanonicalGrittyRules.FatalDamage.ExampleCurrentHealth
                    - CanonicalGrittyRules.FatalDamage.ExampleDamage;

        Assert.Equal(CanonicalGrittyRules.FatalDamage.ExampleHealthAfter, after);

        var fatalThreshold = -full;
        Assert.True(after <= fatalThreshold, "−5 reaches the negative of Clint's full 5 Health exactly");

        // The rescue's direction comes from the interpretation, not from the contradicted printed
        // word — see CanonicalGrittyRules.FatalDamage and the entry's own ambiguity.
        var direction = interpretation.ResolveReducesDamageTo switch
        {
            "1 point above the fatal threshold" => 1,
            "1 point below the fatal threshold" => -1,
            var other => throw new InvalidOperationException(
                $"interpretation.resolve_reduces_damage_to '{other}' names neither direction")
        };

        var rescued = fatalThreshold + direction * fatal.CostResolveToAvoid;
        Assert.Equal(CanonicalGrittyRules.FatalDamage.ExampleHealthAfterSpendingResolve, rescued);

        // And the rescue leaves him dying rather than well: −4 is at or below the dying line.
        Assert.True(rescued <= fatal.DyingBeginsWhenLethalDamageReducesYouTo);
    }

    /// <summary>
    /// <b>Finding 1's proof.</b> <c>gritty_fatal_damage</c>'s fact field transcribes p.79's word —
    /// "1 point below this fatal threshold" — as printed, and its <c>interpretation</c> carries the
    /// reading the same paragraph's own worked example supports instead. This reads both the word and
    /// the example's three figures out of <c>ch04-combat.json</c> rather than off
    /// <c>CanonicalGrittyRules</c>, so it fires if the corpus is ever re-extracted with a different
    /// reading of the page.
    /// </summary>
    [Fact]
    public void TheFatalDamagePrintedWordAndItsWorkedExampleDisagree()
    {
        var section = ChapterFourSectionText("FATAL DAMAGE");
        Assert.False(string.IsNullOrEmpty(section));

        Assert.Contains("1 point below this fatal threshold", section, StringComparison.Ordinal);

        var fullHealth = int.Parse(
            Regex.Match(section, @"tough guy with (\d+) Health").Groups[1].Value,
            CultureInfo.InvariantCulture);
        var beforeRescue = int.Parse(
            Regex.Match(section, "taking him down to −(\\d+) Health").Groups[1].Value,
            CultureInfo.InvariantCulture);
        var afterRescue = int.Parse(
            Regex.Match(section, "leaving him at −(\\d+) Health").Groups[1].Value,
            CultureInfo.InvariantCulture);

        var fatalThreshold = -fullHealth;
        Assert.Equal(fatalThreshold, -beforeRescue);

        var belowReading = fatalThreshold - 1; // what the printed word computes
        var aboveReading = fatalThreshold + 1; // what the worked example computes

        Assert.Equal(aboveReading, -afterRescue);
        Assert.NotEqual(belowReading, -afterRescue);

        // And the entry's own fields say the same two things: the fact carries the word, the
        // interpretation carries the reading the arithmetic above just proved.
        var entry = GrittyEntryById("gritty_fatal_damage");
        Assert.Equal("1 point below the fatal threshold", entry.FatalDamage!.ResolveReducesDamageTo);
        Assert.Equal("1 point above the fatal threshold", entry.Interpretation!.ResolveReducesDamageTo);
        Assert.False(string.IsNullOrWhiteSpace(entry.Ambiguity));
    }

    /// <summary>Every section printed under <paramref name="heading"/> in Chapter 4's corpus, joined.</summary>
    private static string ChapterFourSectionText(string heading)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch04-combat.json")));

        var builder = new StringBuilder();

        foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
        {
            if (section.TryGetProperty("heading", out var sectionHeading)
                && sectionHeading.ValueKind == JsonValueKind.String
                && string.Equals(sectionHeading.GetString(), heading, StringComparison.Ordinal))
            {
                builder.Append(section.GetProperty("text").GetString()).Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// <b>Finding 2's proof.</b> p.80 states an hourly parenthetical for the three higher Slow
    /// Healing bands — "(1 every 12 hours)", "(1 every 8 hours)", "(1 every 6 hours)" — but the
    /// lowest band's clause is only "1 point of damage per day if your Toughness is 6d or less",
    /// with none. So the lowest band's row carries a null <c>one_point_every_hours</c>, and its
    /// 24-hour reading is <em>derived</em> — a day's hours divided by that band's own transcribed
    /// rate — rather than typed as a fourth transcribed parenthetical the page does not print.
    /// </summary>
    [Fact]
    public void TheSlowHealingLowestBandHasNoPrintedHourlyFigureAndItsReadingIsDerived()
    {
        var section = ChapterFourSectionText("SLOW HEALING");
        Assert.False(string.IsNullOrEmpty(section));

        Assert.Contains("1 point of damage per day if your Toughness is 6d or less", section, StringComparison.Ordinal);
        Assert.Contains("2 points per day (1 every 12 hours)", section, StringComparison.Ordinal);

        var entry = GrittyEntryById("gritty_slow_healing");
        var lowestBand = entry.SlowHealing!.Bands[0];

        Assert.Null(lowestBand.MinToughness);
        Assert.Equal(6, lowestBand.MaxToughness);
        Assert.Equal(1, lowestBand.HealthPerDay);
        Assert.Null(lowestBand.OnePointEveryHours);

        Assert.NotNull(entry.Interpretation);
        Assert.False(string.IsNullOrWhiteSpace(entry.Interpretation.WhatThisIs));

        var derivedHours = CanonicalGrittyRules.SlowHealing.HoursPerDay / lowestBand.HealthPerDay;
        Assert.Equal(24, derivedHours);
        Assert.Equal(derivedHours, entry.Interpretation.OnePointEveryHoursForTheLowestBand);
    }

    /// <summary>
    /// <b>p.81's parenthetical covers the "or less", not the "0".</b> "Whenever you are down to 0
    /// Health or less (which is possible when using the Fatal Damage rules) you suffer a −4d penalty"
    /// hangs the clause off the whole phrase — but exactly 0 needs no optional rule whatever: p.75
    /// says "Once a target's Health falls to 0, they are defeated", in any fight at any table. Only
    /// the negative half of the band needs Fatal Damage, which is the rule that lets Health go below
    /// zero at all.
    ///
    /// <para><b>The entry used to assert the other reading as a fact.</b> The field said
    /// <c>zero_or_less_is_reachable_only_with: "the Fatal Damage rule"</c> — flat, and a claim the
    /// page does not make: read that way the −4d band is unreachable in an ordinary game and a
    /// defeated character's rolls carry no penalty. The fact field now carries p.81's clause as
    /// printed and the reading is an <c>interpretation</c>.</para>
    ///
    /// <para>The reading is <b>derived rather than typed</b>: the boundary is Chapter 4's own
    /// <c>damage.defeated_at_health</c>, read out of <c>combat.json</c>, and the corpus is asked for
    /// both printed sentences so the derivation is against the book rather than against two of this
    /// project's files agreeing with each other.</para>
    /// </summary>
    [Fact]
    public void TheWoundPenaltyParentheticalCoversTheNegativeHalfOfItsBand()
    {
        var wounds = ChapterFourSectionText("WOUND PENALTIES");
        var damageSection = ChapterFourSectionText("DAMAGE");

        Assert.False(string.IsNullOrEmpty(wounds));
        Assert.False(string.IsNullOrEmpty(damageSection));

        var entry = GrittyEntryById("gritty_wound_penalties");
        var penalties = entry.WoundPenalties;
        var interpretation = entry.Interpretation;

        Assert.NotNull(penalties);
        Assert.NotNull(interpretation);
        Assert.False(string.IsNullOrWhiteSpace(interpretation.WhatThisIs));

        // The fact field is the page's clause and nothing else — verbatim, out of the corpus.
        Assert.Contains(penalties.ZeroOrLessParenthetical, wounds, StringComparison.Ordinal);
        Assert.Equal(CanonicalGrittyRules.WoundPenalties.ZeroOrLessParenthetical, penalties.ZeroOrLessParenthetical);

        // p.75 is what makes the flat reading wrong: a character reaches exactly the defeat figure
        // with no Gritty rule switched on, so the boundary the parenthetical guards is below it.
        var damage = CombatEntryById("damage").Damage;

        Assert.NotNull(damage);
        Assert.Contains(
            $"Health falls to {damage.DefeatedAtHealth}, they are defeated",
            damageSection,
            StringComparison.Ordinal);

        Assert.Equal(
            damage.DefeatedAtHealth,
            interpretation.FatalDamageIsRequiredBelowHealth);

        // And the rule the parenthetical names is the one that opens the negatives up. Without it
        // there is nothing below the defeat figure to reach, which is the whole of the reading.
        var fatal = GrittyEntryById("gritty_fatal_damage").FatalDamage;

        Assert.NotNull(fatal);
        Assert.True(
            fatal.HealthCanGoNegative,
            "Fatal Damage is what puts Health below the defeat figure. If it no longer does, "
            + "p.81's parenthetical is about something else and this reading has to be redone.");

        // The band's top value is therefore reachable in an ordinary fight, and its penalty applies
        // there. Stated as a bound rather than as prose so a widened reading has to move it.
        Assert.True(
            interpretation.FatalDamageIsRequiredBelowHealth >= damage.DefeatedAtHealth,
            "The reading may not push the Fatal Damage requirement above the defeat figure: that "
            + "would make the −4d band unreachable without an optional rule, which is the flat "
            + "reading this interpretation exists to replace.");
    }

    /// <summary>
    /// <b>The Example of Combat, p.81, stepped through the data.</b> Six of its rolls resolve
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
    public void TheExampleOfCombatOnPageEightyOneResolvesThroughTheData()
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

        // Turn order: three Edge scores sorted downward, and then the Minions — who go where the
        // file puts them. Appending them was the fixture answering its own question: the whole
        // content of "Minions act last" is tie_break.minions_act, so that field decides, and a file
        // that moved them to the front builds an order p.81 does not print.
        Assert.False(order.MinionsHaveAnEdge);

        var named = new[]
            {
                ("Citizen Soldier", CanonicalCombatRules.ExampleOfCombat.CitizenSoldierEdge),
                ("the mecha", CanonicalCombatRules.ExampleOfCombat.MechaEdge),
                ("Gatecrasher", CanonicalCombatRules.ExampleOfCombat.GatecrasherEdge)
            }
            .OrderByDescending(c => c.Item2)
            .Select(c => c.Item1)
            .ToArray();

        var byEdge = order.MinionsAct switch
        {
            "after everyone else" => named.Append(CanonicalCombatRules.ExampleOfCombat.MinionsLabel),
            "before everyone else" => named.Prepend(CanonicalCombatRules.ExampleOfCombat.MinionsLabel),
            var other => throw new InvalidOperationException(
                $"tie_break.minions_act '{other}' does not say where in the order the Minions go, "
                + "so the Example of Combat's turn order cannot be built from the data.")
        };

        Assert.Equal(CanonicalCombatRules.ExampleOfCombat.TurnOrder, byEdge.ToArray());

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

        // The cap has to be doing work on this fixture, or Math.Min below is the identity and
        // asserting its result against MinionsPresent is a tautology — which is what this was.
        // "Could have defeated up to five … turns these four into scrap" is precisely an allowance
        // that overshoots the mob, so the overshoot is the positive control.
        var present = CanonicalCombatRules.ExampleOfCombat.MinionsPresent;

        Assert.True(
            couldDefeat > present,
            $"The rate allows {couldDefeat} and {present} are present. p.81's sentence is an "
            + "allowance that overshoots the mob; if it no longer does, this step proves nothing "
            + "about the cap and the fixture needs a different example.");

        var actuallyDefeated = Math.Min(couldDefeat, present);

        Assert.Equal(present, actuallyDefeated);
        Assert.NotEqual(couldDefeat, actuallyDefeated);

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
    /// <para><b>"One place" means the one the book NAMES, and that distinction is load-bearing.</b>
    /// It is not a claim that no other rate in Chapter 4 halves something: <c>gritty_slow_healing</c>'s
    /// Medicine rate is the identical "1 point per 2 net successes" construction on p.80, and p.80
    /// prints no direction for it at all. Tough Minions is the exception because p.81 says "note that
    /// you are rounding down in this unique case"; Slow Healing is silent, which is a third state —
    /// neither the book-wide rule confirmed nor a second exception — and a simulator that reads "the
    /// one place" as "the only rate of this shape" would settle it by accident. So the silence is
    /// asserted here beside the exception, on the entry's own <c>ambiguity</c>.</para>
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

        Assert.True(
            rounding.Exceptions.Count == 1,
            $"p.7 names one exception to the half-rounds-up rule; play_meta.json records "
            + $"{rounding.Exceptions.Count}. \"The one place a half goes downward\" is a claim about "
            + "what the book NAMES, not about which rates halve something — p.80's Medicine rate is "
            + "the same 'per 2 net successes' shape with no direction printed for it, and is silent "
            + "rather than a second exception. A new entry here has to be a rule that prints its own "
            + "direction, the way p.81 does.");

        var exception = rounding.Exceptions[0];
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

        // And the other rate of this shape is on record as unresolved rather than quietly assumed.
        // Slow Healing's Medicine roll heals 1 point per 2 net successes on p.80 with no direction
        // printed beside it — the same construction, without the sentence that makes this one an
        // exception. Asserting the shapes match is what stops the ambiguity from being about some
        // other field: if the rate ever stops being "per 2", this pairing stops being the point.
        var healing = GrittyEntryById("gritty_slow_healing");

        Assert.NotNull(healing.SlowHealing);
        Assert.Equal(
            tough.NetSuccessesPerMinionDefeated,
            healing.SlowHealing.MedicineNetSuccessesPerPoint);
        Assert.False(
            string.IsNullOrWhiteSpace(healing.Ambiguity),
            "gritty_slow_healing carries the same 'per 2 net successes' rate as Tough Minions with "
            + "no printed rounding direction, and records no ambiguity — which leaves the direction "
            + "for an odd roll to be decided silently by whoever implements it first.");
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
    ///
    /// <para><b>The eleventh entry is not one of them</b>, and neither is <c>kind</c> a marker for the
    /// file. <c>gritty_overview</c> is the paragraph that offers the ten and is <c>narrative</c>, so
    /// "every entry in <c>gritty.json</c> is a <c>table_setting</c>" is false; and <c>combat.json</c>
    /// carries a <c>table_setting</c> of its own — <c>seize_initiative_gm_alternative</c>, p.73's
    /// choice offered to the GM — so selecting on <c>kind</c> across the store would not reproduce
    /// this list. Hence ten ids, named.</para>
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
    /// <b>Finding 3's proof.</b> p.78's Special Cases preamble introduces nine entries — AMBUSHES,
    /// AREA ATTACKS, CHARGE ATTACKS, CLOBBERING ATTACKS, DEFENDING OTHERS, GOING ALL-OUT, KNOCKBACK,
    /// LURING, TEAM ATTACKS — not eleven; the header, <c>play-rules.md</c> and
    /// <c>RULEBOOK-COVERAGE.md</c> all said eleven before this fix. The nine now carry their own
    /// <c>kind</c>, <c>special_case</c>, the same way the Gritty rules carry <c>table_setting</c> —
    /// so a rule quietly dropped from the file, or one wrongly reclassified, moves the count this
    /// test checks against the header's own <c>special_cases_count</c>, rather than the two figures
    /// being able to drift with nothing to compare them.
    /// </summary>
    [Fact]
    public void TheHeadersSpecialCasesCountEqualsTheNumberOfSpecialCaseEntries()
    {
        var header = Combat().Header;
        var specialCases = Combat().Entries.Where(e => e.Kind == "special_case").Select(e => e.Id).ToList();

        Assert.NotNull(header.SpecialCasesCount);
        Assert.Equal(header.SpecialCasesCount, specialCases.Count);

        Assert.Equal(
            [
                "ambushes", "area_attacks", "charge_attacks", "clobbering_attacks",
                "defending_others", "going_all_out", "knockback", "luring", "team_attacks"
            ],
            specialCases.Order(StringComparer.Ordinal));

        // And the corpus really does print nine headings under SPECIAL CASES, before GRITTY COMBAT
        // RULES starts the next section — a positive control on the number itself.
        var headings = ChapterHeadings("ch04-combat.json");
        var specialCaseHeadings = new[]
        {
            "AMBUSHES", "AREA ATTACKS", "CHARGE ATTACKS", "CLOBBERING ATTACKS", "DEFENDING OTHERS",
            "GOING ALL-OUT", "KNOCKBACK", "LURING", "TEAM ATTACKS"
        };
        Assert.Equal(9, specialCaseHeadings.Length);
        foreach (var heading in specialCaseHeadings)
        {
            Assert.Contains(headings, h => h.Heading == heading && h.Page is 78 or 79);
        }
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
        Assert.True(checkedCount >= 139, $"Only {checkedCount} entries were read across the seven play rules files.");
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
                "some_powers_require_resolve", "npc_flaws_bite_when_the_opportunity_arises",
                // Chapter 6
                "condition", "applies_to", "every_weapon_has_one",
                // Chapter 7
                "is_an_attack", "attack_rank_depends_on", "roll_asked_for_when",
                "roll_is_asked_for_by", "sources", "applies_when", "work_like", "toxins_are",
                "read_the_table_when", "most_goals_need", "hazard_grades",
                "hold_breath_minutes_equal_to", "minor_withstood_for_minutes_equal_to",
                "major_withstood_for_pages_equal_to"),
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
                "net_successes_to_move_one_range_class", "attack_traits",
                // Chapter 6
                "raised_options", "balance_option_bonus_dice",
                "powers_are_overshadowed_unless_the_trait_cap_exceeds_the_gear_limit_by",
                "melee_attack_traits", "melee_defense_traits", "ranged_attack_traits", "added_to",
                "worked_example_trait_rank", "worked_example_maximum_effective_rank",
                "worked_example_armed_maximum_effective_rank", "worked_example_unarmed_rank",
                "worked_example_gear_limit", "standard_power_level_trait_cap",
                "maximum_effective_rank_is", "you_may_use_instead",
                // Chapter 7
                "rank", "hard_landing_bonus_dice", "soft_landing_penalty_dice",
                "soft_landing_allows_active_defense", "resisted_only_with",
                "gm_may_allow_a_creative_active_defense", "roll_trait", "roll_traits",
                "roll_against", "perception_penalty_dice_underwater",
                "scuba_mask_reduces_visual_perception_penalty_to",
                "underwater_physical_attack_penalty_dice",
                "underwater_active_defense_penalty_dice", "underwater_combat_edge",
                "agility_used_for_movement_challenge_rolls", "close_combat_bonus_dice",
                "thrown_attack_bonus_dice", "thrown_attack_trait", "cap_bonus_dice",
                "sonic_underwater_bonus_dice", "table_is_a_guide", "read_off", "most_goals_need",
                "attack_penetrates_when", "target_may_use_the_objects_structure_as"),
            ["threshold"] = Keys(
                "threshold_min", "threshold_max", "difficulty", "threshold", "threshold_source",
                "static_threshold_used_when", "helper_rolls_against_threshold",
                // Chapter 7
                "threshold_depends_on", "roll_against", "bend_or_small_hole_min_net_successes",
                "big_hole_min_net_successes",
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
                "area_attack_minions_per_net_success",
                // Chapter 6, and the same catch-all argument: an entry claiming "effect" is
                // claiming that something about what the mechanic DOES was checked.
                "usually", "makes_mundane_gear_less_useful_for", "trait_cap_is_a_different_thing",
                "what_the_weapon_still_buys", "at_the_wielders_option", "may_be_disregarded_entirely",
                "raised_options_are_open_ended", "suits", "balance_options_given",
                "balance_options_exclude", "subdual_marker", "default_damage",
                "ancient_and_modern_damage", "advanced_damage", "advanced_physical_exceptions",
                "ranged_weapons_reach", "ranged_reach_exceptions", "subdual", "features",
                "bonus_dice",
                // Chapter 7, and the same catch-all argument once more.
                "is_an_attack", "works_like", "resisted_only_with", "table_is_a_guide",
                "minor_goals", "major_goals", "gm_supplies_in_a_minor_disaster",
                "gm_supplies_in_a_major_disaster", "remaining_goals_come_from", "a_goal_is",
                "a_goal_may_be_worth_its_own_scene", "who_decides_a_goal_needs_its_own_scene",
                "resolution_read_off", "narrator", "embellishment", "kinds_are_lumped_into", "why",
                "gravity_and_magnetism_are_energy", "gravity_and_magnetism_are",
                "gravity_and_magnetism_represented_with",
                "gravity_and_magnetism_if_classified_as_energy", "types",
                "force_kinetic_attacks_are", "sonic_footnote_is_offered_as_optional",
                "sonic_in_a_vacuum", "up_to_feet", "hard_landing_examples", "soft_landing_examples",
                "minor_examples", "major_examples", "minor_damage_per_minute_after_that",
                "major_damage_per_page_after_that", "maximum_hazard_damage_per_page",
                "maximum_applies_across_simultaneous_hazards", "powers_that_protect",
                "damage_per_page_after_that", "all_suffocation_damage_removed_when",
                "removal_is_immediate", "defeated_rather_than_killed_unless",
                "surviving_is_explained_by", "travel_speed", "ignored_by",
                "deep_water_complexities_left_to", "every_character_effectively_has", "at_rank",
                "for_the_purpose_of", "the_power_must_be_bought_to_use_it_for",
                "distances_are_deliberately_abstract", "maximum_weight_normally",
                "static_value_assumes", "weight", "examples", "heat", "electricity",
                "vehicles_and_complex_machines_have", "simple_objects_have", "structure_determines",
                "gm_may_adjust_structure_by_min", "gm_may_adjust_structure_by_max",
                "adjustment_factors", "adjustment_factors_are_open_ended",
                "bend_or_small_hole_max_net_successes",
                "net_successes_may_be_combined_over_attempts",
                "an_especially_thick_object_may_need_several_attempts", "materials",
                "footnoted_row_materials", "attack_penetrates_when",
                "target_may_use_the_objects_structure_as", "options_given", "thrown_attack_is",
                "attack_rank_caps_at", "worked_example_object", "worked_example_object_body",
                "worked_example_maximum_attack_rank", "second_worked_example_object",
                "second_worked_example_structure_from_the_table",
                "second_worked_example_thickness_adjustment",
                "second_worked_example_object_structure",
                "second_worked_example_maximum_attack_rank", "degradation_dice_per_page",
                "degradation_applies_to", "degradation_is_only_for_these_purposes",
                "ordinary_human_strength_degrades_nothing", "edge_cases_left_to", "scenery",
                "maximum_attack_rank", "uses_instead_of_body_or_structure", "requires",
                "always_breaks_apart_after", "objects", "weight_rank", "toxins_are_described_as",
                "passive_defenses_named", "resistance_is_a_power",
                "the_pros_and_cons_apply_only_to", "unless", "printed_name", "option_kind",
                "detail_store", "power_id", "option_id", "power", "options", "footnoted",
                "footnoted_row_names", "options_are_referenced_not_transcribed", "effects",
                "traits", "rank_min", "rank_max"),
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
                // Chapter 7
                "track_time_in_pages_for_a_major_hazard_even_out_of_combat",
                "always_breaks_apart_after", "degradation_dice_per_page",
                "hold_breath_minutes_equal_to", "minor_withstood_for_minutes_equal_to",
                "major_withstood_for_pages_equal_to", "removal_is_immediate",
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
        Assert.Equal(meta.Header.VerifiedFieldsClosedList, Equipment().Header.VerifiedFieldsClosedList);
        Assert.Equal(meta.Header.VerifiedFieldsClosedList, Environment().Header.VerifiedFieldsClosedList);

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
            .Concat(Equipment().Entries.Select(e => (e.Id, e.Kind)))
            .Concat(Environment().Entries.Select(e => (e.Id, e.Kind)))
            .ToList();

        Assert.True(kinds.Count >= 139, $"Only {kinds.Count} entries were read across the seven files.");

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
    [InlineData("equipment.json")]
    [InlineData("environment.json")]
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
        "equipment.json" => Equipment().Header,
        "environment.json" => Environment().Header,
        _ => Resolve().Header
    };

    /// <summary>All three files, as (id, entry, verified_fields) triples.</summary>
    private static IEnumerable<(string Id, object Entry, IReadOnlyList<string> Fields)>
        AllEntriesWithVerifiedFields() =>
        Meta().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Resolve().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Combat().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Gritty().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Equipment().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Environment().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)));

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
    [InlineData(
        "equipment.json",
        "The default Gear Limit in most games is 6d. That means the maximum effective rank you "
        + "can have when using a piece of mundane gear is 6d plus whatever bonus it provides.")]
    [InlineData(
        "environment.json",
        "Falls are treated like attacks that can only be resisted with passive defenses")]
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
    // and Wound Penalties records the extraction fault that filed it under another heading. Slow
    // Healing is the fifth: its Medicine rate is Tough Minions' construction without Tough Minions'
    // sentence about which way a half goes.
    [InlineData("combat.json", "throwing_table")]
    [InlineData("combat.json", "minions_attacking")]
    [InlineData("combat.json", "seize_initiative_gm_alternative")]
    [InlineData("gritty.json", "gritty_wound_penalties")]
    [InlineData("gritty.json", "gritty_slow_healing")]
    // Three more the chapter leaves loose in its own sentences: an allowance offered to "high
    // ranks" with no rank named, a cap printed as a parenthesis that reads wider than where it
    // sits, and "but that's about it" standing in for a list the page never gives.
    [InlineData("combat.json", "movement")]
    [InlineData("combat.json", "attacking_minions")]
    [InlineData("combat.json", "ambushes")]
    // Chapter 7. The first two change results outright - a rate in minutes under a ceiling in
    // pages, and a rank Chapter 4's throwing formula subtracts without either page defining it.
    // The rest are the chapter's own hedges: an undefined "super strong", an undefined "mundane",
    // an optional footnote, a scope granted for one purpose, a maximum named and never printed,
    // a table given no duration, a distinction drawn and then dropped, a state the page never
    // says is undone, a mask stated for one sense, a goal that can be failed with no rule for
    // failing it, and a Power used against two named Traits.
    [InlineData("environment.json", "hostile_environments")]
    [InlineData("environment.json", "massive_objects")]
    [InlineData("environment.json", "scenery_as_weapons")]
    [InlineData("environment.json", "toxins")]
    [InlineData("environment.json", "energy_types")]
    [InlineData("environment.json", "leaping")]
    [InlineData("environment.json", "lifting")]
    [InlineData("environment.json", "scorching")]
    [InlineData("environment.json", "smashing")]
    [InlineData("environment.json", "suffocation")]
    [InlineData("environment.json", "swimming")]
    [InlineData("environment.json", "disasters")]
    [InlineData("environment.json", "drugs_and_poisons_table")]
    public void TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect(string file, string id)
    {
        var ambiguity = file switch
        {
            "play_meta.json" => MetaEntryById(id).Ambiguity,
            "challenge.json" => ChallengeEntryById(id).Ambiguity,
            "combat.json" => CombatEntryById(id).Ambiguity,
            "gritty.json" => GrittyEntryById(id).Ambiguity,
            "environment.json" => EnvironmentEntryById(id).Ambiguity,
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
    ///
    /// <para><b>A header also has to say when its chapter's printed range is wider than its text.</b>
    /// Chapters 4 and 5 both end on a page the corpus extracts nothing from — p.82 and p.86 — and a
    /// header's <c>source_ref</c> names the whole chapter, so without the sentence the range reads as
    /// a claim that the page was read and found empty. Chapter 5's header said so and Chapter 4's two
    /// did not.</para>
    /// </summary>
    [Theory]
    [InlineData("challenge.json", "Sample Thresholds")]
    [InlineData("resolve.json", "poker chips")]
    [InlineData("combat.json", "Example of Combat")]
    [InlineData("gritty.json", "worked example")]
    [InlineData("resolve.json", "p.86")]
    [InlineData("combat.json", "p.82")]
    [InlineData("gritty.json", "p.82")]
    [InlineData("equipment.json", "Weapon Features")]
    [InlineData("equipment.json", "Armor")]
    [InlineData("equipment.json", "pp.91-104")]
    [InlineData("environment.json", "p.110")]
    [InlineData("environment.json", "chapter opening")]
    [InlineData("environment.json", "description column")]
    [InlineData("environment.json", "powers.json")]
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
    [InlineData("equipment.json")]
    [InlineData("environment.json")]
    public void EveryFieldInAPlayRulesFileDeserializesIntoATestModel(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => fileName switch
        {
            "play_meta.json" => JsonSerializer.Deserialize<PlayFile<MetaEntry>>(json, Strict()),
            "challenge.json" => (object?)JsonSerializer.Deserialize<PlayFile<ChallengeEntry>>(json, Strict()),
            "combat.json" => JsonSerializer.Deserialize<PlayFile<CombatEntry>>(json, Strict()),
            "gritty.json" => JsonSerializer.Deserialize<PlayFile<GrittyEntry>>(json, Strict()),
            "equipment.json" => JsonSerializer.Deserialize<PlayFile<EquipmentEntry>>(json, Strict()),
            "environment.json" => JsonSerializer.Deserialize<PlayFile<EnvironmentEntry>>(json, Strict()),
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
    ///   <item><c>summary</c> — the player-facing text <c>ResolveEntryList</c> renders instead of
    ///   <c>description</c>; the same run-of-ten-words check covers it (see
    ///   <see cref="NoDescriptionRepeatsARunOfTheBooksOwnWords"/>), and
    ///   <see cref="NoSummaryNamesProgramVocabulary"/> is the narrower cheap check beside it</item>
    ///   <item><c>kind</c> — <see cref="EveryEntryDeclaresAKindFromTheClosedList"/></item>
    ///   <item><c>ambiguity</c> —
    ///   <see cref="TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect"/></item>
    ///   <item><c>printed_under</c> —
    ///   <see cref="EveryEntryNamesAHeadingPrintedOnThePageItCites"/> for Chapter 5 and
    ///   <see cref="EveryChapterFourEntryNamesAHeadingPrintedOnThePageItCites"/> for Chapter 4's
    ///   two files, both comparisons against the corpus rather than against a canonical constant,
    ///   which is why registering ninety near-identical checks here would have been the weaker
    ///   option</item>
    ///   <item><c>who</c> —
    ///   <see cref="EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms"/>, which compares
    ///   both values against <see cref="CanonicalResolveRules"/> and requires every spend to carry
    ///   one</item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> EnvelopeFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "kind", "description", "summary", "verified_fields", "source_ref",
            "corroborated_by", "ambiguity", "printed_under", "who"
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
            "breaking_free.interpretation.reduction_rounds",
            // gritty.json's Fatal Damage: the printed word and its own worked example disagree
            // (see CanonicalGrittyRules.FatalDamage), and this is the reading the arithmetic
            // supports rather than a second transcription of the contradicted word.
            "gritty_fatal_damage.interpretation.resolve_reduces_damage_to",
            // gritty.json's Slow Healing: only the top three bands print an hourly figure. The
            // lowest band's 24-hour reading is 24 (a day's hours, not a page reference) divided by
            // itself, i.e. a day converted to hours — arithmetic, not a transcription.
            "gritty_slow_healing.interpretation.one_point_every_hours_for_the_lowest_band",
            // gritty.json's Wound Penalties: p.81 hangs its parenthetical on the whole of "0 Health
            // or less", and which half it governs is a reading — derived from p.75's own defeat
            // figure by TheWoundPenaltyParentheticalCoversTheNegativeHalfOfItsBand.
            "gritty_wound_penalties.interpretation.fatal_damage_is_required_below_health",
            // equipment.json's three weapons tables. The rows are NOT typed into
            // CanonicalEquipmentRules: the corpus already carries the printed columns, and a
            // second transcription of sixty-three rows is a second thing to disagree with the
            // first. TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns derives every row from
            // data/rulebook/ch06-equipment.json instead, and the pairing of the two blocks the
            // extractor splits each table into is the reading each interpretation records.
            "ancient_weapons.weapons",
            "modern_weapons.weapons",
            "advanced_weapons.weapons",
            "ancient_weapons.interpretation.row_alignment",
            "modern_weapons.interpretation.row_alignment",
            "advanced_weapons.interpretation.row_alignment",
            // environment.json's ten tables, and the same argument. The corpus already carries the
            // printed columns, so TheChapterSevenTablesAreReadOutOfTheCorpusColumns derives every
            // row from data/rulebook/ch07-environment.json rather than typing a hundred and twenty
            // rows into CanonicalEnvironmentRules a second time. Six of the ten need a reading of
            // how the printed columns line up, which each entry's interpretation records; the other
            // four the extractor prints straight, and they are here because the corpus is the book
            // and a canonical copy of it would only be something else to disagree with.
            "disaster_results.disaster_results",
            "energy_types.energy_types.types",
            "falling_table.falling_table",
            "lifting_table.lifting_table",
            "scorching_table.scorching_table",
            "smashing_table.smashing_table.rows",
            "scenery_table.scenery_table",
            "massive_objects_table.massive_objects_table",
            "diseases_table.diseases_table.rows",
            "drugs_and_poisons_table.drugs_and_poisons_table.rows",
            "disaster_results.interpretation.row_alignment",
            "lifting_table.interpretation.row_alignment",
            "scorching_table.interpretation.row_alignment",
            "smashing_table.interpretation.row_alignment",
            "scenery_table.interpretation.row_alignment",
            "massive_objects_table.interpretation.row_alignment"
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
            ["movement.movement.open_terrain_gm_may_allow_range_classes_per_page_min"] = Is(CanonicalCombatRules.Movement.OpenTerrainGmMayAllowRangeClassesPerPageMin),
            ["movement.movement.open_terrain_gm_may_allow_range_classes_per_page_max"] = Is(CanonicalCombatRules.Movement.OpenTerrainGmMayAllowRangeClassesPerPageMax),
            ["movement.movement.open_terrain_allowance_applies_to"] = Is(CanonicalCombatRules.Movement.OpenTerrainAllowanceAppliesTo),
            ["movement.movement.open_terrain_allowance_is_gm_discretion"] = Is(CanonicalCombatRules.Movement.OpenTerrainAllowanceIsGmDiscretion),

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
            ["attacking_minions.attacking_minions.area_attack_capped_by"] = Is(CanonicalCombatRules.AttackingMinions.AreaAttackCappedBy),
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
            ["ambushes.ambush.partial_surprise_limit_printed_as"] = Is(CanonicalCombatRules.Ambush.PartialSurpriseLimitPrintedAs),
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
            ["gritty_wound_penalties.wound_penalties.zero_or_less_parenthetical"] = Is(CanonicalGrittyRules.WoundPenalties.ZeroOrLessParenthetical),
            ["gritty_wound_penalties.wound_penalties.applies_to"] = Is(CanonicalGrittyRules.WoundPenalties.AppliesTo),
            ["gritty_wound_penalties.wound_penalties.cost_resolve_to_ignore"] = Is(CanonicalGrittyRules.WoundPenalties.CostResolveToIgnore),
            ["gritty_wound_penalties.wound_penalties.pages_ignored_per_resolve_point"] = Is(CanonicalGrittyRules.WoundPenalties.PagesIgnoredPerResolvePoint),

            // ── equipment.json, Chapter 6 pp.87-90 ───────────────────────────
            ["gear_limit.gear_limit.what_it_is"] = Is(CanonicalEquipmentRules.GearLimit.WhatItIs),
            ["gear_limit.gear_limit.default_rank"] = Is(CanonicalEquipmentRules.GearLimit.DefaultRank),
            ["gear_limit.gear_limit.usually"] = Is(CanonicalEquipmentRules.GearLimit.Usually),
            ["gear_limit.gear_limit.maximum_effective_rank_is"] = Is(CanonicalEquipmentRules.GearLimit.MaximumEffectiveRankIs),
            ["gear_limit.gear_limit.worked_example_weapon"] = Is(CanonicalEquipmentRules.GearLimit.WorkedExampleWeapon),
            ["gear_limit.gear_limit.worked_example_weapon_bonus_dice"] = Is(CanonicalEquipmentRules.GearLimit.WorkedExampleWeaponBonusDice),
            ["gear_limit.gear_limit.worked_example_trait"] = Is(CanonicalEquipmentRules.GearLimit.WorkedExampleTrait),
            ["gear_limit.gear_limit.worked_example_trait_rank"] = Is(CanonicalEquipmentRules.GearLimit.WorkedExampleTraitRank),
            ["gear_limit.gear_limit.worked_example_maximum_effective_rank"] = Is(CanonicalEquipmentRules.GearLimit.WorkedExampleMaximumEffectiveRank),
            ["gear_limit.gear_limit.trait_cap_is_a_different_thing"] = Is(CanonicalEquipmentRules.GearLimit.TraitCapIsADifferentThing),
            ["gear_limit.gear_limit.standard_power_level_trait_cap"] = Is(CanonicalEquipmentRules.GearLimit.StandardPowerLevelTraitCap),
            ["gear_limit.gear_limit.makes_mundane_gear_less_useful_for"] = Is(CanonicalEquipmentRules.GearLimit.MakesMundaneGearLessUsefulFor),

            ["gear_limit_close_combat_exception.close_combat_exception.applies_to"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.AppliesTo),
            ["gear_limit_close_combat_exception.close_combat_exception.condition"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.Condition),
            ["gear_limit_close_combat_exception.close_combat_exception.you_may_use_instead"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.YouMayUseInstead),
            ["gear_limit_close_combat_exception.close_combat_exception.at_the_wielders_option"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.AtTheWieldersOption),
            ["gear_limit_close_combat_exception.close_combat_exception.worked_example_weapon"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WorkedExampleWeapon),
            ["gear_limit_close_combat_exception.close_combat_exception.worked_example_weapon_bonus_dice"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WorkedExampleWeaponBonusDice),
            ["gear_limit_close_combat_exception.close_combat_exception.worked_example_gear_limit"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WorkedExampleGearLimit),
            ["gear_limit_close_combat_exception.close_combat_exception.worked_example_armed_maximum_effective_rank"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WorkedExampleArmedMaximumEffectiveRank),
            ["gear_limit_close_combat_exception.close_combat_exception.worked_example_unarmed_rank"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WorkedExampleUnarmedRank),
            ["gear_limit_close_combat_exception.close_combat_exception.what_the_weapon_still_buys"] = Is(CanonicalEquipmentRules.CloseCombatCarveOut.WhatTheWeaponStillBuys),

            ["raising_the_gear_limit.raised_limit.raised_options"] = Is(CanonicalEquipmentRules.RaisedLimit.RaisedOptions),
            ["raising_the_gear_limit.raised_limit.raised_options_are_open_ended"] = Is(CanonicalEquipmentRules.RaisedLimit.RaisedOptionsAreOpenEnded),
            ["raising_the_gear_limit.raised_limit.may_be_disregarded_entirely"] = Is(CanonicalEquipmentRules.RaisedLimit.MayBeDisregardedEntirely),
            ["raising_the_gear_limit.raised_limit.suits"] = Is(CanonicalEquipmentRules.RaisedLimit.Suits),
            ["raising_the_gear_limit.raised_limit.powers_are_overshadowed_unless_the_trait_cap_exceeds_the_gear_limit_by"] = Is(CanonicalEquipmentRules.RaisedLimit.PowersAreOvershadowedUnlessTheTraitCapExceedsTheGearLimitBy),
            ["raising_the_gear_limit.raised_limit.balance_options_given"] = Is(CanonicalEquipmentRules.RaisedLimit.BalanceOptionsGiven),
            ["raising_the_gear_limit.raised_limit.balance_option_bonus_dice"] = Is(CanonicalEquipmentRules.RaisedLimit.BalanceOptionBonusDice),
            ["raising_the_gear_limit.raised_limit.balance_options_exclude"] = Is(CanonicalEquipmentRules.RaisedLimit.BalanceOptionsExclude),

            ["weapon_bonus.weapon_bonus.every_weapon_has_one"] = Is(CanonicalEquipmentRules.WeaponBonus.EveryWeaponHasOne),
            ["weapon_bonus.weapon_bonus.melee_attack_traits"] = Is(CanonicalEquipmentRules.WeaponBonus.MeleeAttackTraits),
            ["weapon_bonus.weapon_bonus.melee_defense_traits"] = Is(CanonicalEquipmentRules.WeaponBonus.MeleeDefenseTraits),
            ["weapon_bonus.weapon_bonus.ranged_attack_traits"] = Is(CanonicalEquipmentRules.WeaponBonus.RangedAttackTraits),
            ["weapon_bonus.weapon_bonus.added_to"] = Is(CanonicalEquipmentRules.WeaponBonus.AddedTo),
            ["weapon_bonus.weapon_bonus.subdual_marker"] = Is(CanonicalEquipmentRules.WeaponBonus.SubdualMarker),
            ["weapon_bonus.weapon_bonus.default_damage"] = Is(CanonicalEquipmentRules.WeaponBonus.DefaultDamage),
            ["weapon_bonus.weapon_bonus.ancient_and_modern_damage"] = Is(CanonicalEquipmentRules.WeaponBonus.AncientAndModernDamage),
            ["weapon_bonus.weapon_bonus.advanced_damage"] = Is(CanonicalEquipmentRules.WeaponBonus.AdvancedDamage),
            ["weapon_bonus.weapon_bonus.advanced_physical_exceptions"] = Is(CanonicalEquipmentRules.WeaponBonus.AdvancedPhysicalExceptions),
            ["weapon_bonus.weapon_bonus.ranged_weapons_reach"] = Is(CanonicalEquipmentRules.WeaponBonus.RangedWeaponsReach),
            ["weapon_bonus.weapon_bonus.ranged_reach_exceptions"] = Is(CanonicalEquipmentRules.WeaponBonus.RangedReachExceptions),

            // ── environment.json, Chapter 7 ──────────────────────────────────
            // disasters
            ["disasters.disaster.minor_goals"] = Is(EnvRules.Disasters.MinorGoals),
            ["disasters.disaster.major_goals"] = Is(EnvRules.Disasters.MajorGoals),
            ["disasters.disaster.gm_supplies_in_a_minor_disaster"] = Is(EnvRules.Disasters.GmSuppliesInAMinorDisaster),
            ["disasters.disaster.gm_supplies_in_a_major_disaster"] = Is(EnvRules.Disasters.GmSuppliesInAMajorDisaster),
            ["disasters.disaster.remaining_goals_come_from"] = Is(EnvRules.Disasters.RemainingGoalsComeFrom),
            ["disasters.disaster.a_goal_is"] = Is(EnvRules.Disasters.AGoalIs),
            ["disasters.disaster.most_goals_need"] = Is(EnvRules.Disasters.MostGoalsNeed),
            ["disasters.disaster.a_goal_may_be_worth_its_own_scene"] = Is(EnvRules.Disasters.AGoalMayBeWorthItsOwnScene),
            ["disasters.disaster.who_decides_a_goal_needs_its_own_scene"] = Is(EnvRules.Disasters.WhoDecidesAGoalNeedsItsOwnScene),
            ["disasters.disaster.resolution_read_off"] = Is(EnvRules.Disasters.ResolutionReadOff),
            ["disasters.disaster.read_the_table_when"] = Is(EnvRules.Disasters.ReadTheTableWhen),
            // energy
            ["energy.energy.kinds_are_lumped_into"] = Is(EnvRules.Energy.KindsAreLumpedInto),
            ["energy.energy.why"] = Is(EnvRules.Energy.Why),
            ["energy.energy.gravity_and_magnetism_are_energy"] = Is(EnvRules.Energy.GravityAndMagnetismAreEnergy),
            ["energy.energy.gravity_and_magnetism_are"] = Is(EnvRules.Energy.GravityAndMagnetismAre),
            ["energy.energy.gravity_and_magnetism_represented_with"] = Is(EnvRules.Energy.GravityAndMagnetismRepresentedWith),
            ["energy.energy.gravity_and_magnetism_if_classified_as_energy"] = Is(EnvRules.Energy.GravityAndMagnetismIfClassifiedAsEnergy),
            // energy_types
            ["energy_types.energy_types.force_kinetic_attacks_are"] = Is(EnvRules.EnergyTypes.ForceKineticAttacksAre),
            ["energy_types.energy_types.sonic_footnote_is_offered_as_optional"] = Is(EnvRules.EnergyTypes.SonicFootnoteIsOfferedAsOptional),
            ["energy_types.energy_types.sonic_in_a_vacuum"] = Is(EnvRules.EnergyTypes.SonicInAVacuum),
            ["energy_types.energy_types.sonic_underwater_bonus_dice"] = Is(EnvRules.EnergyTypes.SonicUnderwaterBonusDice),
            // falling
            ["falling.falling.is_an_attack"] = Is(EnvRules.Falling.IsAnAttack),
            ["falling.falling.resisted_only_with"] = Is(EnvRules.Falling.ResistedOnlyWith),
            ["falling.falling.gm_may_allow_a_creative_active_defense"] = Is(EnvRules.Falling.GmMayAllowACreativeActiveDefense),
            ["falling.falling.attack_rank_depends_on"] = Is(EnvRules.Falling.AttackRankDependsOn),
            ["falling.falling.read_off"] = Is(EnvRules.Falling.ReadOff),
            ["falling.falling.hard_landing_examples"] = Is(EnvRules.Falling.HardLandingExamples),
            ["falling.falling.hard_landing_bonus_dice"] = Is(EnvRules.Falling.HardLandingBonusDice),
            ["falling.falling.soft_landing_examples"] = Is(EnvRules.Falling.SoftLandingExamples),
            ["falling.falling.soft_landing_penalty_dice"] = Is(EnvRules.Falling.SoftLandingPenaltyDice),
            ["falling.falling.soft_landing_allows_active_defense"] = Is(EnvRules.Falling.SoftLandingAllowsActiveDefense),
            // hostile_environments
            ["hostile_environments.hostile_environment.hazard_grades"] = Is(EnvRules.HostileEnvironments.HazardGrades),
            ["hostile_environments.hostile_environment.minor_examples"] = Is(EnvRules.HostileEnvironments.MinorExamples),
            ["hostile_environments.hostile_environment.major_examples"] = Is(EnvRules.HostileEnvironments.MajorExamples),
            ["hostile_environments.hostile_environment.minor_withstood_for_minutes_equal_to"] = Is(EnvRules.HostileEnvironments.MinorWithstoodForMinutesEqualTo),
            ["hostile_environments.hostile_environment.minor_damage_per_minute_after_that"] = Is(EnvRules.HostileEnvironments.MinorDamagePerMinuteAfterThat),
            ["hostile_environments.hostile_environment.major_withstood_for_pages_equal_to"] = Is(EnvRules.HostileEnvironments.MajorWithstoodForPagesEqualTo),
            ["hostile_environments.hostile_environment.major_damage_per_page_after_that"] = Is(EnvRules.HostileEnvironments.MajorDamagePerPageAfterThat),
            ["hostile_environments.hostile_environment.track_time_in_pages_for_a_major_hazard_even_out_of_combat"] = Is(EnvRules.HostileEnvironments.TrackTimeInPagesForAMajorHazardEvenOutOfCombat),
            ["hostile_environments.hostile_environment.maximum_hazard_damage_per_page"] = Is(EnvRules.HostileEnvironments.MaximumHazardDamagePerPage),
            ["hostile_environments.hostile_environment.maximum_applies_across_simultaneous_hazards"] = Is(EnvRules.HostileEnvironments.MaximumAppliesAcrossSimultaneousHazards),
            ["hostile_environments.hostile_environment.powers_that_protect"] = Is(EnvRules.HostileEnvironments.PowersThatProtect),
            // suffocation
            ["suffocation.suffocation.hold_breath_minutes_equal_to"] = Is(EnvRules.Suffocation.HoldBreathMinutesEqualTo),
            ["suffocation.suffocation.damage_per_page_after_that"] = Is(EnvRules.Suffocation.DamagePerPageAfterThat),
            ["suffocation.suffocation.all_suffocation_damage_removed_when"] = Is(EnvRules.Suffocation.AllSuffocationDamageRemovedWhen),
            ["suffocation.suffocation.removal_is_immediate"] = Is(EnvRules.Suffocation.RemovalIsImmediate),
            ["suffocation.suffocation.defeated_rather_than_killed_unless"] = Is(EnvRules.Suffocation.DefeatedRatherThanKilledUnless),
            ["suffocation.suffocation.surviving_is_explained_by"] = Is(EnvRules.Suffocation.SurvivingIsExplainedBy),
            // swimming
            ["swimming.swimming.travel_speed"] = Is(EnvRules.Swimming.TravelSpeed),
            ["swimming.swimming.agility_used_for_movement_challenge_rolls"] = Is(EnvRules.Swimming.AgilityUsedForMovementChallengeRolls),
            ["swimming.swimming.perception_penalty_dice_underwater"] = Is(EnvRules.Swimming.PerceptionPenaltyDiceUnderwater),
            ["swimming.swimming.scuba_mask_reduces_visual_perception_penalty_to"] = Is(EnvRules.Swimming.ScubaMaskReducesVisualPerceptionPenaltyTo),
            ["swimming.swimming.underwater_combat_edge"] = Is(EnvRules.Swimming.UnderwaterCombatEdge),
            ["swimming.swimming.underwater_physical_attack_penalty_dice"] = Is(EnvRules.Swimming.UnderwaterPhysicalAttackPenaltyDice),
            ["swimming.swimming.underwater_active_defense_penalty_dice"] = Is(EnvRules.Swimming.UnderwaterActiveDefensePenaltyDice),
            ["swimming.swimming.ignored_by"] = Is(EnvRules.Swimming.IgnoredBy),
            ["swimming.swimming.deep_water_complexities_left_to"] = Is(EnvRules.Swimming.DeepWaterComplexitiesLeftTo),
            // leaping
            ["leaping.leaping.every_character_effectively_has"] = Is(EnvRules.Leaping.EveryCharacterEffectivelyHas),
            ["leaping.leaping.at_rank"] = Is(EnvRules.Leaping.AtRank),
            ["leaping.leaping.for_the_purpose_of"] = Is(EnvRules.Leaping.ForThePurposeOf),
            ["leaping.leaping.the_power_must_be_bought_to_use_it_for"] = Is(EnvRules.Leaping.ThePowerMustBeBoughtToUseItFor),
            ["leaping.leaping.distances_are_deliberately_abstract"] = Is(EnvRules.Leaping.DistancesAreDeliberatelyAbstract),
            // lifting
            ["lifting.lifting.maximum_weight_normally"] = Is(EnvRules.Lifting.MaximumWeightNormally),
            ["lifting.lifting.static_value_assumes"] = Is(EnvRules.Lifting.StaticValueAssumes),
            ["lifting.lifting.roll_asked_for_when"] = Is(EnvRules.Lifting.RollAskedForWhen),
            ["lifting.lifting.roll_is_asked_for_by"] = Is(EnvRules.Lifting.RollIsAskedForBy),
            ["lifting.lifting.roll_trait"] = Is(EnvRules.Lifting.RollTrait),
            ["lifting.lifting.roll_against"] = Is(EnvRules.Lifting.RollAgainst),
            ["lifting.lifting.threshold_depends_on"] = Is(EnvRules.Lifting.ThresholdDependsOn),
            ["lifting.lifting.read_off"] = Is(EnvRules.Lifting.ReadOff),
            // scorching
            ["scorching.scorching.sources"] = Is(EnvRules.Scorching.Sources),
            ["scorching.scorching.is_an_attack"] = Is(EnvRules.Scorching.IsAnAttack),
            ["scorching.scorching.resisted_only_with"] = Is(EnvRules.Scorching.ResistedOnlyWith),
            ["scorching.scorching.gm_may_allow_a_creative_active_defense"] = Is(EnvRules.Scorching.GmMayAllowACreativeActiveDefense),
            ["scorching.scorching.works_like"] = Is(EnvRules.Scorching.WorksLike),
            ["scorching.scorching.table_is_a_guide"] = Is(EnvRules.Scorching.TableIsAGuide),
            // smashing
            ["smashing.smashing.vehicles_and_complex_machines_have"] = Is(EnvRules.Smashing.VehiclesAndComplexMachinesHave),
            ["smashing.smashing.simple_objects_have"] = Is(EnvRules.Smashing.SimpleObjectsHave),
            ["smashing.smashing.structure_determines"] = Is(EnvRules.Smashing.StructureDetermines),
            ["smashing.smashing.gm_may_adjust_structure_by_min"] = Is(EnvRules.Smashing.GmMayAdjustStructureByMin),
            ["smashing.smashing.gm_may_adjust_structure_by_max"] = Is(EnvRules.Smashing.GmMayAdjustStructureByMax),
            ["smashing.smashing.adjustment_factors"] = Is(EnvRules.Smashing.AdjustmentFactors),
            ["smashing.smashing.adjustment_factors_are_open_ended"] = Is(EnvRules.Smashing.AdjustmentFactorsAreOpenEnded),
            ["smashing.smashing.roll_traits"] = Is(EnvRules.Smashing.RollTraits),
            ["smashing.smashing.roll_against"] = Is(EnvRules.Smashing.RollAgainst),
            ["smashing.smashing.bend_or_small_hole_min_net_successes"] = Is(EnvRules.Smashing.BendOrSmallHoleMinNetSuccesses),
            ["smashing.smashing.bend_or_small_hole_max_net_successes"] = Is(EnvRules.Smashing.BendOrSmallHoleMaxNetSuccesses),
            ["smashing.smashing.big_hole_min_net_successes"] = Is(EnvRules.Smashing.BigHoleMinNetSuccesses),
            ["smashing.smashing.net_successes_may_be_combined_over_attempts"] = Is(EnvRules.Smashing.NetSuccessesMayBeCombinedOverAttempts),
            ["smashing.smashing.an_especially_thick_object_may_need_several_attempts"] = Is(EnvRules.Smashing.AnEspeciallyThickObjectMayNeedSeveralAttempts),
            // smashing_table
            ["smashing_table.smashing_table.footnoted_row_materials"] = Is(EnvRules.SmashingFootnotedRowMaterials),
            // damaging_cover
            ["damaging_cover.damaging_cover.applies_when"] = Is(EnvRules.DamagingCover.AppliesWhen),
            ["damaging_cover.damaging_cover.attack_penetrates_when"] = Is(EnvRules.DamagingCover.AttackPenetratesWhen),
            ["damaging_cover.damaging_cover.target_may_use_the_objects_structure_as"] = Is(EnvRules.DamagingCover.TargetMayUseTheObjectsStructureAs),
            ["damaging_cover.damaging_cover.options_given"] = Is(EnvRules.DamagingCover.OptionsGiven),
            // scenery_as_weapons
            ["scenery_as_weapons.scenery_as_weapons.applies_to"] = Is(EnvRules.SceneryAsWeapons.AppliesTo),
            ["scenery_as_weapons.scenery_as_weapons.close_combat_bonus_dice"] = Is(EnvRules.SceneryAsWeapons.CloseCombatBonusDice),
            ["scenery_as_weapons.scenery_as_weapons.thrown_attack_trait"] = Is(EnvRules.SceneryAsWeapons.ThrownAttackTrait),
            ["scenery_as_weapons.scenery_as_weapons.thrown_attack_bonus_dice"] = Is(EnvRules.SceneryAsWeapons.ThrownAttackBonusDice),
            ["scenery_as_weapons.scenery_as_weapons.thrown_attack_is"] = Is(EnvRules.SceneryAsWeapons.ThrownAttackIs),
            ["scenery_as_weapons.scenery_as_weapons.attack_rank_caps_at"] = Is(EnvRules.SceneryAsWeapons.AttackRankCapsAt),
            ["scenery_as_weapons.scenery_as_weapons.cap_bonus_dice"] = Is(EnvRules.SceneryAsWeapons.CapBonusDice),
            ["scenery_as_weapons.scenery_as_weapons.worked_example_object"] = Is(EnvRules.SceneryAsWeapons.WorkedExampleObject),
            ["scenery_as_weapons.scenery_as_weapons.worked_example_object_body"] = Is(EnvRules.SceneryAsWeapons.WorkedExampleObjectBody),
            ["scenery_as_weapons.scenery_as_weapons.worked_example_maximum_attack_rank"] = Is(EnvRules.SceneryAsWeapons.WorkedExampleMaximumAttackRank),
            ["scenery_as_weapons.scenery_as_weapons.second_worked_example_object"] = Is(EnvRules.SceneryAsWeapons.SecondWorkedExampleObject),
            ["scenery_as_weapons.scenery_as_weapons.second_worked_example_structure_from_the_table"] = Is(EnvRules.SceneryAsWeapons.SecondWorkedExampleStructureFromTheTable),
            ["scenery_as_weapons.scenery_as_weapons.second_worked_example_thickness_adjustment"] = Is(EnvRules.SceneryAsWeapons.SecondWorkedExampleThicknessAdjustment),
            ["scenery_as_weapons.scenery_as_weapons.second_worked_example_object_structure"] = Is(EnvRules.SceneryAsWeapons.SecondWorkedExampleObjectStructure),
            ["scenery_as_weapons.scenery_as_weapons.second_worked_example_maximum_attack_rank"] = Is(EnvRules.SceneryAsWeapons.SecondWorkedExampleMaximumAttackRank),
            ["scenery_as_weapons.scenery_as_weapons.degradation_dice_per_page"] = Is(EnvRules.SceneryAsWeapons.DegradationDicePerPage),
            ["scenery_as_weapons.scenery_as_weapons.degradation_applies_to"] = Is(EnvRules.SceneryAsWeapons.DegradationAppliesTo),
            ["scenery_as_weapons.scenery_as_weapons.degradation_is_only_for_these_purposes"] = Is(EnvRules.SceneryAsWeapons.DegradationIsOnlyForThesePurposes),
            ["scenery_as_weapons.scenery_as_weapons.ordinary_human_strength_degrades_nothing"] = Is(EnvRules.SceneryAsWeapons.OrdinaryHumanStrengthDegradesNothing),
            ["scenery_as_weapons.scenery_as_weapons.edge_cases_left_to"] = Is(EnvRules.SceneryAsWeapons.EdgeCasesLeftTo),
            // massive_objects
            ["massive_objects.massive_objects.works_like"] = Is(EnvRules.MassiveObjects.WorksLike),
            ["massive_objects.massive_objects.uses_instead_of_body_or_structure"] = Is(EnvRules.MassiveObjects.UsesInsteadOfBodyOrStructure),
            ["massive_objects.massive_objects.requires"] = Is(EnvRules.MassiveObjects.Requires),
            ["massive_objects.massive_objects.always_breaks_apart_after"] = Is(EnvRules.MassiveObjects.AlwaysBreaksApartAfter),
            // toxins
            ["toxins.toxins.toxins_are"] = Is(EnvRules.Toxins.ToxinsAre),
            ["toxins.toxins.toxins_are_described_as"] = Is(EnvRules.Toxins.ToxinsAreDescribedAs),
            ["toxins.toxins.work_like"] = Is(EnvRules.Toxins.WorkLike),
            ["toxins.toxins.resisted_only_with"] = Is(EnvRules.Toxins.ResistedOnlyWith),
            ["toxins.toxins.passive_defenses_named"] = Is(EnvRules.Toxins.PassiveDefensesNamed),
            ["toxins.toxins.resistance_is_a_power"] = Is(EnvRules.Toxins.ResistanceIsAPower),
            ["toxins.toxins.the_pros_and_cons_apply_only_to"] = Is(EnvRules.Toxins.TheProsAndConsApplyOnlyTo),
            ["toxins.toxins.unless"] = Is(EnvRules.Toxins.Unless),
            // toxin_con_caustic
            ["toxin_con_caustic.toxin_option.transcribed_here"] = Is(false),
            ["toxin_con_caustic.toxin_option.printed_name"] = Is(EnvRules.ToxinOptions[0].PrintedName),
            ["toxin_con_caustic.toxin_option.option_kind"] = Is(EnvRules.ToxinOptions[0].OptionKind),
            ["toxin_con_caustic.toxin_option.detail_store"] = Is(EnvRules.ToxinOptionDetailStore),
            ["toxin_con_caustic.toxin_option.power_id"] = Is(EnvRules.ToxinOptions[0].PowerId),
            ["toxin_con_caustic.toxin_option.option_id"] = Is(EnvRules.ToxinOptions[0].OptionId),
            // toxin_pro_lethal_disease
            ["toxin_pro_lethal_disease.toxin_option.transcribed_here"] = Is(false),
            ["toxin_pro_lethal_disease.toxin_option.printed_name"] = Is(EnvRules.ToxinOptions[1].PrintedName),
            ["toxin_pro_lethal_disease.toxin_option.option_kind"] = Is(EnvRules.ToxinOptions[1].OptionKind),
            ["toxin_pro_lethal_disease.toxin_option.detail_store"] = Is(EnvRules.ToxinOptionDetailStore),
            ["toxin_pro_lethal_disease.toxin_option.power_id"] = Is(EnvRules.ToxinOptions[1].PowerId),
            ["toxin_pro_lethal_disease.toxin_option.option_id"] = Is(EnvRules.ToxinOptions[1].OptionId),
            // toxin_pro_non_lethal_disease
            ["toxin_pro_non_lethal_disease.toxin_option.transcribed_here"] = Is(false),
            ["toxin_pro_non_lethal_disease.toxin_option.printed_name"] = Is(EnvRules.ToxinOptions[2].PrintedName),
            ["toxin_pro_non_lethal_disease.toxin_option.option_kind"] = Is(EnvRules.ToxinOptions[2].OptionKind),
            ["toxin_pro_non_lethal_disease.toxin_option.detail_store"] = Is(EnvRules.ToxinOptionDetailStore),
            ["toxin_pro_non_lethal_disease.toxin_option.power_id"] = Is(EnvRules.ToxinOptions[2].PowerId),
            ["toxin_pro_non_lethal_disease.toxin_option.option_id"] = Is(EnvRules.ToxinOptions[2].OptionId),
            // diseases_table
            ["diseases_table.diseases_table.footnoted_row_names"] = Is(EnvRules.DiseasesFootnotedRowNames),
            ["diseases_table.diseases_table.options_are_referenced_not_transcribed"] = Is(EnvRules.ToxinTableOptionsAreReferencedNotTranscribed),
            // drugs_and_poisons_table
            ["drugs_and_poisons_table.drugs_and_poisons_table.footnoted_row_names"] = Is(EnvRules.DrugsFootnotedRowNames),
            ["drugs_and_poisons_table.drugs_and_poisons_table.options_are_referenced_not_transcribed"] = Is(EnvRules.ToxinTableOptionsAreReferencedNotTranscribed)
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
            leaves >= 820,
            $"The walk found only {leaves} fact fields across the seven files, which is fewer than "
            + "the entries carry — there are 878 today, 98 of them Chapter 3's, 388 Chapter 4's "
            + "and 174 Chapter 7's. It has stopped reading the models; fix the walk, not this "
            + "number.");

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

        if (Equipment().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal)))
            return nameof(CanonicalEquipmentRules);

        if (Environment().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal)))
            return nameof(CanonicalEnvironmentRules);

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
            .Concat(Gritty().Entries.Select(e => (e.Id, (object)e)))
            .Concat(Equipment().Entries.Select(e => (e.Id, (object)e)))
            .Concat(Environment().Entries.Select(e => (e.Id, (object)e)));

    /// <summary>All three files, as (file, id, source_ref, corroborated_by) rows.</summary>
    private static IEnumerable<(string File, string Id, string SourceRef, IReadOnlyList<string>? CorroboratedBy)>
        AllEntries() =>
        Meta().Entries.Select(e => ("play_meta.json", e.Id, e.SourceRef, e.CorroboratedBy))
            .Concat(Challenge().Entries.Select(e => ("challenge.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Resolve().Entries.Select(e => ("resolve.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Combat().Entries.Select(e => ("combat.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Gritty().Entries.Select(e => ("gritty.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Equipment().Entries.Select(e => ("equipment.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Environment().Entries.Select(e => ("environment.json", e.Id, e.SourceRef, e.CorroboratedBy)));

    /// <summary>
    /// The descriptions of one file, as (id, description) pairs. <c>resolve.json</c> is the one
    /// file that also carries a <c>summary</c> — the player-facing text <c>ResolveEntryList</c>
    /// renders instead of <c>description</c> — so its rows are doubled up rather than swapped:
    /// <see cref="NoDescriptionRepeatsARunOfTheBooksOwnWords"/> has to catch a lifted run in
    /// either field, not just the one it used to check.
    /// </summary>
    private static IEnumerable<(string Id, string Description)> DescriptionsIn(string fileName) => fileName switch
    {
        "play_meta.json" => Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.Description)),
        "challenge.json" => Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.Description)),
        "combat.json" => Combat().Entries.Select(e => ($"combat.json/{e.Id}", e.Description)),
        "gritty.json" => Gritty().Entries.Select(e => ($"gritty.json/{e.Id}", e.Description)),
        "equipment.json" => Equipment().Entries.Select(e => ($"equipment.json/{e.Id}", e.Description)),
        "environment.json" => Environment().Entries.Select(e => ($"environment.json/{e.Id}", e.Description)),
        _ => Resolve().Entries.SelectMany(e => new[]
        {
            ($"resolve.json/{e.Id}/description", e.Description),
            ($"resolve.json/{e.Id}/summary", e.Summary)
        })
    };

    // ── Summary: the player-facing text ─────────────────────────────────────

    /// <summary>
    /// Every one of the 28 entries carries a non-empty <c>summary</c> — the page has no other
    /// source of player-facing text, so a blank one is a blank row rather than a fallback to
    /// <c>description</c>.
    /// </summary>
    [Fact]
    public void EveryEntryCarriesANonEmptySummary()
    {
        var faults = Resolve().Entries
            .Where(e => string.IsNullOrWhiteSpace(e.Summary))
            .Select(e => e.Id)
            .ToList();

        Assert.True(faults.Count == 0, $"missing summary: {string.Join(", ", faults)}");
    }

    /// <summary>
    /// <b>A cheap denylist, and its doc comment says what it cannot do</b> — per <c>CLAUDE.md</c>'s
    /// rule that a denylist of spellings cannot make a verdict honest. This catches the obvious
    /// slip — a maintainer's note leaking into the player-facing <c>summary</c> field by naming a
    /// type, a test, a file format or the act of transcription — and nothing more. It cannot prove
    /// a summary is <em>good</em> prose, only that it does not carry one of these tells.
    /// A PascalCase identifier (an internal capital with a lowercase run before it, e.g.
    /// <c>DerivedStatsCalculator</c>) is the shape a C# type or member name takes and a summary
    /// should never need one; requiring that lowercase run is what keeps an all-caps abbreviation
    /// the book itself uses — GM, NPC — from tripping the check. The six words are the vocabulary
    /// a maintainer reaches for when explaining the data rather than the rule.
    /// </summary>
    [Fact]
    public void NoSummaryNamesProgramVocabulary()
    {
        var pascalCase = new Regex(@"\b[A-Z][a-z0-9]+[A-Z][A-Za-z0-9]*\b");
        string[] bannedWords = ["engine", "test", "file", "json", "transcri", "recorded"];

        var faults = new List<string>();

        foreach (var entry in Resolve().Entries)
        {
            var summary = entry.Summary;

            if (pascalCase.IsMatch(summary))
            {
                faults.Add($"{entry.Id}: summary contains a PascalCase identifier — '{pascalCase.Match(summary).Value}'");
            }

            foreach (var word in bannedWords)
            {
                if (summary.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    faults.Add($"{entry.Id}: summary names program vocabulary — '{word}'");
                }
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }
}
