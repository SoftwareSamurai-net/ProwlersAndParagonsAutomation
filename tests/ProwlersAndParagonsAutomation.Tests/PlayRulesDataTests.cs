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
            ["ch05-resolve-and-adversity.json", "ch00-introduction.json"])
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
    /// <b>This repository's reading of the six ways to earn, kept apart from the transcription of
    /// them.</b> The book draws no line between an award a program could hand out and one that
    /// needs a person to decide something happened; we do, because a simulator has to. Derived by
    /// <see cref="TheEarningsASimulatorCouldApplyAreExactlyTheOnesWithAStatedTrigger"/> from the
    /// entries' own <c>kind</c>, never typed out.
    /// </summary>
    private sealed record EarningInterpretationModel(
        string WhatThisIs, IReadOnlyList<string> MechanisableEntryIds);

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
        int? CostResolve,
        int? CostAdversity,
        int? CostPerPointShared,
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
        EarningInterpretationModel? Interpretation,
        EarningModel? Earning,
        SpendingOverviewModel? SpendingOverview,
        SpendModel? Spend,
        RerollFloorModel? RerollFloor,
        AdversityPoolModel? Adversity,
        ChallengeLevelModel? ChallengeLevel,
        UnheroicActionModel? UnheroicAction);

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

    private static MetaEntry MetaEntryById(string id) => Meta().Entries.Single(e => e.Id == id);
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

        // Positive control on the fixture: the sheet must actually reach the rank being tested, or
        // every row would be measuring an empty character and agreeing trivially at 2 x cap.
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = rank;
        Assert.Equal(rank, sheet.AbilityRanks["might"]);

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
    /// <para><b>Two of the eleven need a mapping and it is ours, not the book's.</b> "Swinging" is
    /// <c>swing_line</c> in the rules data, and "Super Senses" is sixteen entries there because the
    /// book prints sixteen options under one Power. The mapping lives here rather than in the JSON
    /// precisely because it is a reading — <c>resolve.json</c> transcribes the printed names and
    /// nothing else.</para>
    /// </summary>
    [Fact]
    public void EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData()
    {
        var named = ResolveEntryById("resolve_exceptions").Exceptions;

        Assert.NotNull(named);
        Assert.Equal(CanonicalResolveRules.NamedResolveExemptPowers, named.NamedPowers);

        var faults = new List<string>();
        var matched = 0;

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

        // Positive control: eleven printed names, and Super Senses alone is sixteen entries, so a
        // lookup that had stopped matching would fault nothing and prove nothing.
        Assert.True(matched >= 25, $"Only {matched} powers.json entries were reached for {named.NamedPowers.Count} printed names.");
        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// The two printed names that are spelled differently in <c>powers.json</c>. <b>Ours, and kept
    /// out of the data</b> — see <see cref="EveryPowerChapterFiveNamesAsExemptIsExemptInTheRulesData"/>.
    /// Super Senses needs no entry here because its sixteen options are all prefixed with the
    /// printed name.
    /// </summary>
    private static readonly Dictionary<string, string> ChapterFivePowerNames =
        new(StringComparer.Ordinal) { ["Swinging"] = "Swing Line" };

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
    /// <para><b>The id prefix is not what makes it true.</b> A spend renamed out of its prefix would
    /// escape a check built on the prefix alone, so the currency the entry actually charges is the
    /// cross-check: an entry that costs Adversity must be the GM's, an entry that costs Resolve must
    /// be a Hero's, whatever it is called.</para>
    /// </summary>
    [Fact]
    public void EveryResolveSpendIsTheHerosAndEveryAdversitySpendIsTheGms()
    {
        var entries = Resolve().Entries;

        var resolveSpends = entries.Where(e => e.Id.StartsWith("spend_", StringComparison.Ordinal)).ToList();
        var adversitySpends = entries.Where(e => e.Id.StartsWith("adversity_spend_", StringComparison.Ordinal)).ToList();

        // Positive controls: both sides have to be populated, or "every spend is keyed correctly"
        // is satisfied by there being no spends.
        Assert.True(resolveSpends.Count >= 8, $"Only {resolveSpends.Count} Resolve spends were found; p.84 prints six headings, one of which carries three.");
        Assert.True(adversitySpends.Count >= 4, $"Only {adversitySpends.Count} Adversity spends were found; p.85 prints the general rule and three exclusives.");

        // And the two keys must differ, or one value would satisfy both halves of the rule.
        Assert.NotEqual(CanonicalResolveRules.ResolveIsSpentBy, CanonicalResolveRules.AdversityIsSpentBy);

        var faults = new List<string>();

        foreach (var entry in resolveSpends.Where(e => e.Who != CanonicalResolveRules.ResolveIsSpentBy))
            faults.Add($"{entry.Id}: who is {entry.Who ?? "unset"}, and Resolve is spent by the {CanonicalResolveRules.ResolveIsSpentBy}");

        foreach (var entry in adversitySpends.Where(e => e.Who != CanonicalResolveRules.AdversityIsSpentBy))
            faults.Add($"{entry.Id}: who is {entry.Who ?? "unset"}, and Adversity is spent by the {CanonicalResolveRules.AdversityIsSpentBy}");

        // The cross-check that does not depend on the id: what the entry charges.
        foreach (var entry in entries.Where(e => e.Spend is not null))
        {
            var spend = entry.Spend!;

            if (spend.CostAdversity is not null && entry.Who != CanonicalResolveRules.AdversityIsSpentBy)
                faults.Add($"{entry.Id} charges Adversity and is keyed to {entry.Who ?? "nobody"}");

            if ((spend.CostResolve is not null || spend.CostPerPointShared is not null)
                && entry.Who != CanonicalResolveRules.ResolveIsSpentBy)
            {
                faults.Add($"{entry.Id} charges Resolve and is keyed to {entry.Who ?? "nobody"}");
            }
        }

        // Nothing outside the two groups carries a who, so an entry cannot be keyed without being
        // one of the spends this test enumerates.
        Assert.Equal(
            resolveSpends.Concat(adversitySpends).Select(e => e.Id).Order().ToList(),
            entries.Where(e => e.Who is not null).Select(e => e.Id).Order().ToList());

        Assert.True(faults.Count == 0, string.Join("; ", faults));
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
            "transcribed_here", "detail_chapter", "combat_spend_refs"
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

        // Negative control: the classifier has to be capable of rejecting something.
        Assert.DoesNotContain((83, "SPENDING GLASS BEADS"), headings);

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
        Assert.True(checkedCount >= 45, $"Only {checkedCount} entries were read across the three play rules files.");
        Assert.True(corroborations >= 5, $"Only {corroborations} corroborating references were read; Ch.1 reprints three of Ch.3's rules and two of Ch.5's.");
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
                "applies_to", "keep_the_first_roll_if_the_reroll_is_worse"),
            ["threshold"] = Keys(
                "threshold_min", "threshold_max", "difficulty", "threshold", "threshold_source",
                "static_threshold_used_when", "helper_rolls_against_threshold",
                "helper_threshold_difficulty", "gm_discretion_difficulties", "net_success_formula"),
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
                "use_sparingly", "transcribed_here", "combat_spend_refs", "example_given"),
            ["duration"] = Keys(
                "penalty_duration", "regain_consciousness", "limit_per_story",
                "limit_per_scene_per_group", "concurrent_scenes_each_allow_one",
                "may_last_longer_than_an_instant", "typical_exchanges", "arduous_exchanges_min",
                "arduous_exchanges_max", "arduous_condition",
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
                "cost_resolve", "cost_adversity", "cost_per_point_shared",
                "cost_per_point_shared_when_unable_to_assist", "then_unconscious",
                "some_powers_require_resolve")
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
            .ToList();

        Assert.True(kinds.Count >= 45, $"Only {kinds.Count} entries were read across the three files.");

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
        _ => Resolve().Header
    };

    /// <summary>All three files, as (id, entry, verified_fields) triples.</summary>
    private static IEnumerable<(string Id, object Entry, IReadOnlyList<string> Fields)>
        AllEntriesWithVerifiedFields() =>
        Meta().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)))
            .Concat(Resolve().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)));

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
    public void TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect(string file, string id)
    {
        var ambiguity = file switch
        {
            "play_meta.json" => MetaEntryById(id).Ambiguity,
            "challenge.json" => ChallengeEntryById(id).Ambiguity,
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
    public void EveryFieldInAPlayRulesFileDeserializesIntoATestModel(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => fileName switch
        {
            "play_meta.json" => JsonSerializer.Deserialize<PlayFile<MetaEntry>>(json, Strict()),
            "challenge.json" => (object?)JsonSerializer.Deserialize<PlayFile<ChallengeEntry>>(json, Strict()),
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
            "resolve_earning_overview.interpretation.mechanisable_entry_ids"
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

            ["spend_assisting_allies.spend.cost_per_point_shared"] = Is(CanonicalResolveRules.SharePointCost),
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
            ["adversity_spend_villainy.spend.use_sparingly"] = Is(CanonicalResolveRules.VillainyUseSparingly)
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
            leaves >= 225,
            $"The walk found only {leaves} fact fields across the three files, which is fewer than "
            + "the entries carry — there are 233 today, 98 of them Chapter 3's. It has stopped "
            + "reading the models; fix the walk, not this number.");

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
    private static string CanonicalFileFor(string entryId) =>
        Resolve().Entries.Any(e => string.Equals(e.Id, entryId, StringComparison.Ordinal))
            ? nameof(CanonicalResolveRules)
            : nameof(CanonicalChallengeRules);

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
            .Concat(Resolve().Entries.Select(e => (e.Id, (object)e)));

    /// <summary>All three files, as (file, id, source_ref, corroborated_by) rows.</summary>
    private static IEnumerable<(string File, string Id, string SourceRef, IReadOnlyList<string>? CorroboratedBy)>
        AllEntries() =>
        Meta().Entries.Select(e => ("play_meta.json", e.Id, e.SourceRef, e.CorroboratedBy))
            .Concat(Challenge().Entries.Select(e => ("challenge.json", e.Id, e.SourceRef, e.CorroboratedBy)))
            .Concat(Resolve().Entries.Select(e => ("resolve.json", e.Id, e.SourceRef, e.CorroboratedBy)));

    /// <summary>The descriptions of one file, as (id, description) pairs.</summary>
    private static IEnumerable<(string Id, string Description)> DescriptionsIn(string fileName) => fileName switch
    {
        "play_meta.json" => Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.Description)),
        "challenge.json" => Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.Description)),
        _ => Resolve().Entries.Select(e => ($"resolve.json/{e.Id}", e.Description))
    };
}
