using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Holds <c>data/rules/play/*.json</c> to <see cref="CanonicalChallengeRules"/>, which is
/// Chapter 3 transcribed from the page.
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
/// </summary>
public sealed class PlayRulesDataTests
{
    private static string PlayDataPath => Path.Combine(RulesFixture.RepoRoot, "data", "rules", "play");
    private static string RulebookPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

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

    private sealed record PlayFile<TEntry>(Header Header, IReadOnlyList<TEntry> Entries);

    // ── Loading ──────────────────────────────────────────────────────────────

    private static PlayFile<MetaEntry> Meta() => Load<MetaEntry>("play_meta.json");
    private static PlayFile<ChallengeEntry> Challenge() => Load<ChallengeEntry>("challenge.json");

    private static PlayFile<TEntry> Load<TEntry>(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));
        return JsonSerializer.Deserialize<PlayFile<TEntry>>(json, Strict())
               ?? throw new InvalidOperationException($"{fileName} deserialized to null.");
    }

    private static MetaEntry MetaEntryById(string id) => Meta().Entries.Single(e => e.Id == id);
    private static ChallengeEntry ChallengeEntryById(string id) => Challenge().Entries.Single(e => e.Id == id);

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
    /// </summary>
    [Fact]
    public void EverySourceRefNamesAPageInChapterThreeOrTheGlossary()
    {
        var faults = new List<string>();
        var checkedCount = 0;
        var corroborations = 0;

        foreach (var (id, sourceRef, corroboratedBy) in AllEntries())
        {
            checkedCount++;

            var match = Regex.Match(sourceRef, @"\bp\.(\d+)\b");

            if (!match.Success)
            {
                faults.Add($"{id}: source_ref names no page ('{sourceRef}')");
                continue;
            }

            var page = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            if (page is not (>= 67 and <= 72) && page != CanonicalChallengeRules.HalfRulePage)
            {
                faults.Add($"{id}: source_ref names p.{page}, which is outside Ch.3 (67-72) and is not the Glossary (p.7)");
            }

            foreach (var reference in corroboratedBy ?? [])
            {
                corroborations++;

                var second = Regex.Match(reference, @"\bp\.(\d+)\b");

                if (!second.Success)
                {
                    faults.Add($"{id}: corroborated_by names no page ('{reference}')");
                    continue;
                }

                var elsewhere = int.Parse(second.Groups[1].Value, CultureInfo.InvariantCulture);

                if (elsewhere is >= 67 and <= 72)
                {
                    faults.Add(
                        $"{id}: corroborated_by names p.{elsewhere}, which is inside Ch.3 — a second "
                        + "citation of the same chapter is not a second printing");
                }
            }
        }

        // Positive control: an extraction that stopped matching would fault nothing and prove
        // nothing, which is the shape of guard failure this repository has shipped four times.
        Assert.True(checkedCount >= 15, $"Only {checkedCount} entries were read across the two play rules files.");
        Assert.True(corroborations >= 3, $"Only {corroborations} corroborating references were read; Ch.1 reprints three of these rules.");
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
                "when_two_or_more_pursue_the_same_goal", "structure", "offered_at_gm_option"),
            ["roll"] = Keys(
                "die_sides", "pool_formula", "success_map", "dice_rolled", "counting_faces",
                "dice_per_success", "gm_may_veto", "net_success_formula", "min_dice", "max_dice",
                "sixes_explode", "decided_after_the_roll", "explosion_recurses_while_sixes_keep_coming",
                "six_still_worth_successes", "dice_per_resolve_spent", "ordinary_dice_per_resolve_spent",
                "challenge_roll_penalty_dice", "exchange_win_bonus_dice_next_exchange",
                "helper_rolls_against_threshold", "net_successes_per_bonus_die", "bonus_formula",
                "everyone_rolls_individually", "threshold_source"),
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
                "wing_it_is_endorsed", "naming_convention"),
            ["duration"] = Keys(
                "penalty_duration", "regain_consciousness", "limit_per_story",
                "limit_per_scene_per_group", "concurrent_scenes_each_allow_one",
                "may_last_longer_than_an_instant", "typical_exchanges", "arduous_exchanges_min",
                "arduous_exchanges_max", "arduous_condition"),
            ["cost"] = Keys(
                "explode_cost_resolve", "dice_per_resolve_spent", "ordinary_dice_per_resolve_spent",
                "aftermath_permanent_ability_loss_dice", "ability_may_be_bought_back_later",
                "health_after", "unconscious", "challenge_roll_penalty_dice")
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

        Assert.Equal(meta.Header.VerifiedFieldsClosedList, challenge.Header.VerifiedFieldsClosedList);

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
            .ToList();

        Assert.True(kinds.Count >= 15, $"Only {kinds.Count} entries were read across the two files.");

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
    public void EachFileSaysWhatItIsWhereItSitsAndThatNothingReadsIt(string fileName)
    {
        var header = fileName == "play_meta.json" ? Meta().Header : Challenge().Header;

        Assert.False(string.IsNullOrWhiteSpace(header.WhatThisIs));
        Assert.False(string.IsNullOrWhiteSpace(header.NotLogic));
        Assert.Contains(@"data\rules\*.json", header.PlacementNote, StringComparison.Ordinal);
        Assert.Contains("Ch.3 Action", header.SourceRef, StringComparison.Ordinal);
    }

    /// <summary>Both files, as (id, entry, verified_fields) triples.</summary>
    private static IEnumerable<(string Id, object Entry, IReadOnlyList<string> Fields)>
        AllEntriesWithVerifiedFields() =>
        Meta().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e, e.VerifiedFields)));

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
    [Fact]
    public void NoDescriptionRepeatsARunOfTheBooksOwnWords()
    {
        const int run = 10;
        var corpus = CorpusWords();

        // Positive control, and it is the whole instrument: a run taken out of the corpus must be
        // found in the corpus. Without it, a normaliser that quietly produced an empty haystack
        // would pass every assertion below while checking nothing at all.
        const string knownCorpusSentence =
            "You earn one success for every 2 and 4 rolled, and two successes for every 6 rolled.";

        var control = Runs(Normalise(knownCorpusSentence), run).ToList();
        Assert.NotEmpty(control);
        Assert.All(control, phrase =>
            Assert.True(
                corpus.Contains(phrase, StringComparison.Ordinal),
                $"The control phrase '{phrase}' was not found in the corpus, so this test is not "
                + "measuring anything. Fix the normaliser, not the assertion."));

        var faults = new List<string>();

        foreach (var (id, description) in AllDescriptions())
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
    /// Chapter 3's prose plus the Introduction's, reduced to a space-delimited word stream with a
    /// leading and trailing space, so a run can be matched on whole-word boundaries.
    /// </summary>
    private static string CorpusWords()
    {
        var builder = new StringBuilder(" ");

        foreach (var file in new[] { "ch03-action.json", "ch00-introduction.json" })
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
    public void TheKnownAmbiguitiesAreRecordedOnTheEntryTheyAffect(string file, string id)
    {
        var ambiguity = file == "play_meta.json"
            ? MetaEntryById(id).Ambiguity
            : ChallengeEntryById(id).Ambiguity;

        Assert.False(
            string.IsNullOrWhiteSpace(ambiguity),
            $"{file}/{id} records no ambiguity, and the book is unclear here.");
    }

    /// <summary>
    /// The header says what was left out and why. The Sample Thresholds table is the deliberate
    /// omission — worked examples of thresholds already stated numerically, and the one part of
    /// this chapter the extractor is known to scramble.
    /// </summary>
    [Fact]
    public void TheHeaderSaysWhatWasDeliberatelyLeftOut()
    {
        var omitted = Challenge().Header.DeliberatelyOmitted;

        Assert.False(string.IsNullOrWhiteSpace(omitted));
        Assert.Contains("Sample Thresholds", omitted, StringComparison.Ordinal);
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
    public void EveryFieldInAPlayRulesFileDeserializesIntoATestModel(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => fileName == "play_meta.json"
            ? JsonSerializer.Deserialize<PlayFile<MetaEntry>>(json, Strict())
            : (object?)JsonSerializer.Deserialize<PlayFile<ChallengeEntry>>(json, Strict()));

        Assert.True(ex is null,
            $"{fileName} carries a field no model reads, so nothing can hold it to the "
            + $"rulebook: {ex?.Message}");
    }

    // ── Every fact field, compared ───────────────────────────────────────────

    /// <summary>
    /// The seven keys every entry carries whatever it is about. Each has its own guard above —
    /// <see cref="EverySourceRefNamesAPageInChapterThreeOrTheGlossary"/>,
    /// <see cref="EveryEntryDeclaresVerifiedFieldsDrawnFromTheClosedList"/>,
    /// <see cref="NoDescriptionRepeatsARunOfTheBooksOwnWords"/>,
    /// <see cref="EveryEntryDeclaresAKindFromTheClosedList"/> — so the walk starts below them.
    /// </summary>
    private static readonly HashSet<string> EnvelopeFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "kind", "description", "verified_fields", "source_ref", "corroborated_by",
            "ambiguity"
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
        new HashSet<string>(StringComparer.Ordinal) { "thresholds.interpretation.gm_discretion_difficulties" };

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
            ["judging_thresholds.wing_it_is_endorsed"] = Is(CanonicalChallengeRules.JudgingThresholdsWingItIsEndorsed)
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
            leaves >= 90,
            $"The walk found only {leaves} fact fields across both files, which is fewer than the "
            + "entries carry — there are 98 today. It has stopped reading the models; fix the "
            + "walk, not this number.");

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
                faults.Add(
                    $"{path} is a fact field and nothing compares it to CanonicalChallengeRules. "
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

    /// <summary>Both files, as (entry id, entry) pairs, for the reflection walk.</summary>
    private static IEnumerable<(string Id, object Entry)> AllEntryObjects() =>
        Meta().Entries.Select(e => (e.Id, (object)e))
            .Concat(Challenge().Entries.Select(e => (e.Id, (object)e)));

    /// <summary>Both files, as (id, source_ref, corroborated_by) triples.</summary>
    private static IEnumerable<(string Id, string SourceRef, IReadOnlyList<string>? CorroboratedBy)> AllEntries() =>
        Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.SourceRef, e.CorroboratedBy))
            .Concat(Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.SourceRef, e.CorroboratedBy)));

    /// <summary>Both files, as (id, description) pairs.</summary>
    private static IEnumerable<(string Id, string Description)> AllDescriptions() =>
        Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.Description))
            .Concat(Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.Description)));
}
