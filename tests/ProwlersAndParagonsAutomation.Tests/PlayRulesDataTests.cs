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

    private sealed record ThresholdModel(
        string Difficulty, int ThresholdMin, int? ThresholdMax, bool GmDiscretion);

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
        bool OtherExplodeOffersProvideNoExtraBenefit,
        string Intent);

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
        string? Ambiguity,
        IReadOnlyList<BandModel>? Bands,
        ActorSelectionModel? ActorSelection,
        IReadOnlyList<ThresholdModel>? Thresholds,
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
            Assert.Equal(expected.GmDiscretion, actual[i].GmDiscretion);
        }

        // The three banded rows are the ones where the book gives a range and no way to choose
        // inside it. Flagging them is what stops a simulator picking the floor and calling it
        // the rule, so the count is pinned rather than left to the loop above.
        Assert.Equal(3, actual.Count(t => t.GmDiscretion));
        Assert.Contains($"p.{CanonicalChallengeRules.ThresholdsPage}", entry.SourceRef, StringComparison.Ordinal);
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
    /// </summary>
    [Fact]
    public void EverySourceRefNamesAPageInChapterThreeOrTheGlossary()
    {
        var faults = new List<string>();
        var checkedCount = 0;

        foreach (var (id, sourceRef) in AllEntries())
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
        }

        // Positive control: an extraction that stopped matching would fault nothing and prove
        // nothing, which is the shape of guard failure this repository has shipped four times.
        Assert.True(checkedCount >= 15, $"Only {checkedCount} entries were read across the two play rules files.");
        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <c>verified_fields</c> is what distinguishes a transcribed value from a plausible one, and
    /// it is worthless if the vocabulary drifts: the closed list lives in each file's header and
    /// both files have to agree on it.
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresVerifiedFieldsDrawnFromTheClosedList()
    {
        var meta = Meta();
        var challenge = Challenge();

        Assert.Equal(meta.Header.VerifiedFieldsClosedList, challenge.Header.VerifiedFieldsClosedList);

        var closed = meta.Header.VerifiedFieldsClosedList;
        Assert.NotEmpty(closed);

        var faults = new List<string>();

        foreach (var (id, fields) in
                 meta.Entries.Select(e => (e.Id, e.VerifiedFields))
                     .Concat(challenge.Entries.Select(e => (e.Id, e.VerifiedFields))))
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
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

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
    /// <b>Every field in a play rules file is read by the models above.</b> Same failure this
    /// guards against as <see cref="RulesFileCoverageTests"/>: a key nothing deserializes reads
    /// like a source of truth and is not one, and cannot fail a test because nothing loads it.
    /// </summary>
    [Theory]
    [InlineData("play_meta.json")]
    [InlineData("challenge.json")]
    public void EveryFieldInAPlayRulesFileIsReadByTheTestModels(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(PlayDataPath, fileName));

        var ex = Record.Exception(() => fileName == "play_meta.json"
            ? JsonSerializer.Deserialize<PlayFile<MetaEntry>>(json, Strict())
            : (object?)JsonSerializer.Deserialize<PlayFile<ChallengeEntry>>(json, Strict()));

        Assert.True(ex is null,
            $"{fileName} carries a field no model reads, so nothing can hold it to the "
            + $"rulebook: {ex?.Message}");
    }

    /// <summary>Both files, as (id, source_ref) pairs.</summary>
    private static IEnumerable<(string Id, string SourceRef)> AllEntries() =>
        Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.SourceRef))
            .Concat(Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.SourceRef)));

    /// <summary>Both files, as (id, description) pairs.</summary>
    private static IEnumerable<(string Id, string Description)> AllDescriptions() =>
        Meta().Entries.Select(e => ($"play_meta.json/{e.Id}", e.Description))
            .Concat(Challenge().Entries.Select(e => ($"challenge.json/{e.Id}", e.Description)));
}
