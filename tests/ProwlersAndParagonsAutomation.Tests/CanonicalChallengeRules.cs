namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 3, Action (pp.67-72), transcribed from the rulebook, plus the one Introduction
/// Glossary rule (p.7) the chapter's arithmetic leans on. This is the reference
/// <c>data/rules/play/*.json</c> is checked against, so a data edit that contradicts the
/// book fails a test rather than quietly changing how a simulated encounter resolves.
///
/// <para><b>Same standing as <see cref="CanonicalPowers"/>: this file is the rulebook.</b> Do
/// not "fix" a failing test by editing a number here. Open the page named beside the value
/// and fix whichever side is actually wrong.</para>
///
/// <para><b>Every value carries its printed page</b> because Chapter 3 states its mechanics on
/// six different pages and a transcription with no page is a claim nobody can check. The pages
/// are the printed numbers, which are the PDF page less three.</para>
///
/// <para><b>And every value carries the sentence it came out of</b>, which is what took this file
/// from a third of Chapter 3's fact fields to all of them. An adversarial pass found some twenty
/// fields that deserialized and were never compared to anything — booleans and numbers reading as
/// verified data with nothing behind them. A value with no quote beside it is the same claim
/// nobody can check as a value with no page.</para>
///
/// <para><b>What must never be here is a reading.</b> The Thresholds table's "the GM chooses
/// inside this row" sat in this file as though it were a printed column; see
/// <see cref="Threshold"/>. Ours goes in the data under <c>interpretation</c> and is derived.</para>
/// </summary>
public static class CanonicalChallengeRules
{
    /// <summary>One row of a net-successes band table. A null bound is open-ended.</summary>
    public sealed record Band(int? Min, int? Max, string Outcome, bool? Embellishment);

    /// <summary>
    /// One row of the Thresholds table, and <b>nothing but what the row prints</b>:
    /// "Easy 0 · Average 1 · Hard 2 · Daunting 3 · Brutal 4 · Inhuman 5 · Superhuman 6 to 8 ·
    /// Legendary 9 to 11 · Godlike 12 or more". <paramref name="Max"/> differs from
    /// <paramref name="Min"/> only on the three banded rows, and null means no ceiling.
    ///
    /// <para><b>There is no <c>GmDiscretion</c> here and there must not be one.</b> The book prints
    /// two columns; "the GM chooses inside this row" is this repository's reading of the three rows
    /// that print a range, and a reading has no business in a file whose whole standing is that it
    /// was transcribed from the page. It lives in the data under <c>interpretation</c>, labelled as
    /// ours, and is asserted by deriving it from <paramref name="Max"/> — never from a hand-typed
    /// count, which is what a canonical column had made it.</para>
    /// </summary>
    public sealed record Threshold(string Difficulty, int Min, int? Max);

    /// <summary>One row of the Judging Thresholds guideline.</summary>
    public sealed record Judging(string Descriptor, string Difficulty, int ThresholdValue);

    // ── The dice model, p.67 ─────────────────────────────────────────────────

    public const int DiceModelPage = 67;

    /// <summary>
    /// The five values an entry may claim in <c>verified_fields</c>, plus <c>description</c>, are
    /// the book's own vocabulary; <b>this is ours</b> — the shape of a mechanic, used to sort the
    /// entries. It is a closed list so a typo cannot invent a sixth kind nobody handles.
    /// </summary>
    public static readonly IReadOnlyList<string> EntryKinds =
        ["formula", "narrative", "scalar", "special_case", "table", "table_setting"];

    /// <summary>"you roll a number of 6-sided dice equal to the Trait that applies to the action attempted".</summary>
    public const int DieSides = 6;
    public const string PoolFormula = "dice = rank of the Trait that applies";

    /// <summary>Face to successes: "one success for every 2 and 4 rolled, and two successes for every 6".</summary>
    public static readonly IReadOnlyDictionary<int, int> SuccessMap =
        new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 0, [4] = 1, [5] = 0, [6] = 2 };

    /// <summary>
    /// "If penalties ever leave you with less than one die, you nevertheless roll one die, and you
    /// must roll a 6 to earn just one success. Anything else means you fail the roll completely."
    /// </summary>
    public const int SubOneDieDiceRolled = 1;
    public const int SubOneDieCountingFace = 6;
    public const int SubOneDieSuccessesWhenHit = 1;
    public const string SubOneDieOtherwise = "complete_failure";

    /// <summary>"1 automatic success for every 2 dice you choose to not roll (although the GM can veto this option)".</summary>
    public const int DiceNotRolledPerAutomaticSuccess = 2;
    public const bool AutomaticSuccessesCanBeVetoed = true;

    /// <summary>"You must then subtract the action's threshold from your successes to determine your net successes."</summary>
    public const string NetSuccessFormula = "net = successes - threshold";

    /// <summary>
    /// "The GM can apply a modifier of anywhere from +4d to −4d to either or both rolls to account
    /// for conditions like lousy weather, unstable footing, and so on."
    /// </summary>
    public const int ConditionModifierMinDice = -4;
    public const int ConditionModifierMaxDice = 4;
    public const string ConditionModifierAppliedBy = "gm";
    public const string ConditionModifierMayApplyTo = "either roll, or to both";

    public static readonly IReadOnlyList<string> ConditionModifierExamples =
        ["lousy weather", "unstable footing"];

    // ── Who is the Actor, p.67 ───────────────────────────────────────────────

    /// <summary>
    /// "the Actor is the person making the roll and the Opponent is the one resisting it. If two or
    /// more characters are trying to accomplish the same goal, whoever rolls the most successes is
    /// the Actor, and whoever rolls the next most successes is the Opponent. Because actions like
    /// cracking a safe or scaling a wall do not involve an Opponent, the GM acts as the Opponent in
    /// cases like these (although GMs are encouraged to accept player input when the Actor is an
    /// NPC)."
    /// </summary>
    public const string ActorIs = "the character making the roll";
    public const string OpponentIs = "the character resisting it";
    public const string WhenTwoOrMorePursueTheSameGoal =
        "highest successes is the Actor, second highest is the Opponent";
    public const bool GmIsOpponentWhenUnopposed = true;
    public const string GmAcceptsPlayerInputWhenActorIsNpc = "encouraged, not required";

    // ── An opponent's successes are the threshold, p.67 ──────────────────────

    /// <summary>
    /// "Whenever you perform an action opposed by another character, they make a challenge roll to
    /// resist your efforts and their successes become your threshold. If no one is resisting your
    /// efforts, the GM assigns a threshold…"
    /// </summary>
    public const string OpposedThresholdSource = "the resisting character's own challenge roll successes";
    public const string StaticThresholdUsedWhen = "nobody is resisting";

    /// <summary>
    /// "Challenge rolls against a static threshold are typically referred to by their difficulty and
    /// threshold followed by the name of the Trait use. For example, an Average (1) Might roll."
    /// </summary>
    public const string ThresholdNamingConvention =
        "difficulty, threshold in brackets, then the Trait - an Average (1) Might roll";

    // ── The Glossary's rounding rule, p.7 ────────────────────────────────────

    public const int HalfRulePage = 7;

    /// <summary>
    /// "Whenever we refer to half of an odd number (or half of an odd number of dice), always round
    /// up, regardless of the context. For example, half of 3d is 2d and half of 1d is 1d."
    /// </summary>
    public const string HalfRoundingDirection = "up";
    public const string HalfRuleScope =
        "every reference to half a number, or to half a number of dice, anywhere in the book";

    public static readonly IReadOnlyList<string> HalfRuleExamples =
        ["half of 3d is 2d", "half of 1d is 1d"];

    /// <summary>
    /// The sole printed exception, and it belongs to Chapter 4 rather than to this one:
    /// Tough Minions defeats 1 Minion per 2 <em>full</em> net successes, "note that you are
    /// rounding down in this unique case".
    /// </summary>
    public const string HalfRuleExceptionName = "Tough Minions";
    public const int HalfRuleExceptionPage = 81;
    public const string HalfRuleExceptionDirection = "down";
    public const string HalfRuleExceptionGoverns =
        "how many Minions one attack defeats, at 1 per 2 full net successes";

    // ── Challenge Rolls table, p.67 ──────────────────────────────────────────

    public const int NarrativeControlPage = 67;

    public static readonly IReadOnlyList<Band> NarrativeControl =
    [
        new(null, -2, "opponent", false),
        new(-1, 0, "opponent", true),
        new(1, 2, "actor", true),
        new(3, null, "actor", false)
    ];

    // ── Thresholds table, p.67 ───────────────────────────────────────────────

    public const int ThresholdsPage = 67;

    public static readonly IReadOnlyList<Threshold> Thresholds =
    [
        new("Easy", 0, 0),
        new("Average", 1, 1),
        new("Hard", 2, 2),
        new("Daunting", 3, 3),
        new("Brutal", 4, 4),
        new("Inhuman", 5, 5),
        new("Superhuman", 6, 8),
        new("Legendary", 9, 11),
        new("Godlike", 12, null)
    ];

    // ── Embellishments and compromises, p.68 ─────────────────────────────────

    public const int EmbellishmentsPage = 68;

    /// <summary>
    /// "the party who doesn't have narrative control can add to the other person's narration in
    /// some small but meaningful way. … Embellishments can't render the original narration untrue
    /// or true but effectively meaningless."
    /// </summary>
    public const string EmbellishmentHeldBy = "the party without narrative control";
    public const bool EmbellishmentMustNotContradictTheNarration = true;
    public const bool EmbellishmentMustNotRenderItMeaningless = true;
    public const string EmbellishmentSize = "small but meaningful";

    public const int CompromisesPage = 68;

    /// <summary>
    /// "When someone else has the right to embellish your narration, you can offer a compromise.
    /// This means you describe a lessthan-perfect outcome for your action in exchange for them
    /// giving up the right to embellish your narration. To have a compromise, both sides must agree
    /// on the final narration. Opponents are never obligated to accept a compromise."
    /// </summary>
    public const string CompromiseOfferedBy = "the party holding narrative control";
    public const string CompromiseTrade =
        "a worse outcome for the offering side, in exchange for the embellishment right being given up";
    public const bool CompromiseRequiresAgreementOfBoth = true;
    public const bool CompromiseOpponentMayRefuse = true;

    // ── Traditional Results table, p.69 ──────────────────────────────────────

    public const int TraditionalResultsPage = 69;

    public static readonly IReadOnlyList<Band> TraditionalResults =
    [
        new(null, -2, "complete_failure", null),
        new(-1, 0, "failure_with_silver_lining", null),
        new(1, 2, "success_with_complication", null),
        new(3, null, "complete_success", null)
    ];

    /// <summary>
    /// "When using this table, the GM determines the nature of all silver linings and complications.
    /// You can even mix and match these systems, with some players using narrative results and
    /// others using traditional results."
    /// </summary>
    public const string TraditionalResultsQualifiersDecidedBy = "gm";
    public const bool TraditionalResultsMixablePerPlayer = true;

    // ── Checking Your Swing, p.69 ────────────────────────────────────────────

    public const int CheckingYourSwingPage = 69;

    /// <summary>At this table setting every even face is worth exactly one success.</summary>
    public static readonly IReadOnlyDictionary<int, int> CheckingYourSwingSuccessMap =
        new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 0, [4] = 1, [5] = 0, [6] = 1 };

    /// <summary>
    /// "all even numbers are worth one success; 6s don't provide extra successes but they explode
    /// if you spend 1 Resolve (you decide whether or not to spend Resolve after rolling the dice).
    /// … you can keep rerolling them as long as you keep rolling 6s. … There are a few other
    /// instances in these rules that allow you to spend 1 Resolve to make your 6s explode. When
    /// using this rule, those situations provide no extra benefit."
    /// </summary>
    public const int CheckingYourSwingExplodeCostResolve = 1;
    public const string CheckingYourSwingSixesExplode = "only by spending Resolve";
    public const bool CheckingYourSwingDecidedAfterTheRoll = true;
    public const bool CheckingYourSwingExplosionRecurses = true;
    public const bool CheckingYourSwingOtherExplodeOffersProvideNoExtraBenefit = true;

    // ── Assisting and group actions, p.69 ────────────────────────────────────

    /// <summary>
    /// "the person helping you makes their own challenge roll against a Hard (2) threshold and you
    /// gain a +1d bonus to your challenge roll for every 2 net successes they roll (rounding up as
    /// always). When working with multiple helpers, use the bonus provided by the one who rolls
    /// best."
    /// </summary>
    public const int AssistingPage = 69;
    public const int AssistHelperThreshold = 2;
    public const string AssistHelperDifficulty = "Hard";
    public const int AssistNetSuccessesPerBonusDie = 2;
    public const bool AssistBestHelperOnly = true;
    public const string AssistBonusFormula = "bonus_dice = ceil(helper_net_successes / 2)";

    public const int GroupActionPage = 69;

    /// <summary>
    /// <b>The printed bound is strict.</b> "characters who earn <em>more than</em> 3 net successes
    /// can distribute these extra net successes among their allies to help them succeed as well."
    /// So 3 distributes nothing and 4 is the first figure that does — an inequality a bare 3
    /// cannot carry, and the difference between a band being reachable and not.
    /// </summary>
    public const int GroupActionDistributeAboveNetSuccesses = 3;
    public const bool GroupActionAboveIsStrict = true;
    public const int GroupActionMinimumNetSuccessesToDistribute = 4;

    /// <summary>
    /// "Occasionally, you and your allies must perform individual actions as a group, like climbing
    /// a mountain or sneaking into a Villain's lair. In cases like these, everyone makes their own
    /// challenge roll… can distribute these extra net successes among their allies".
    /// </summary>
    public const string GroupActionTrigger =
        "allies performing individual actions as a group, such as a climb or an infiltration";
    public const bool GroupActionEveryoneRollsIndividually = true;
    public const string GroupActionDistributeTo = "allies in the same group action";

    public static readonly IReadOnlyList<string> GroupActionExamples =
        ["climbing a mountain", "sneaking into a Villain's lair"];

    // ── Contests, p.70 ───────────────────────────────────────────────────────

    public const int ContestsPage = 70;

    /// <summary>"Most contests should involve 3 exchanges".</summary>
    public const int ContestTypicalExchanges = 3;

    /// <summary>
    /// <b>Six is a floor, not a figure.</b> "Most contests should involve 3 exchanges, but
    /// especially arduous ones can have <em>6 or more</em>, assuming the GM can make each exchange
    /// interesting." Modelled the way the Thresholds table models Godlike's "12 or more": a
    /// minimum with a null ceiling, because a bare 6 states a cap the book does not print.
    /// </summary>
    public const int ContestArduousExchangesMin = 6;
    public static readonly int? ContestArduousExchangesMax;

    /// <summary>
    /// "The winner of each exchange is allowed to describe something that happens during that
    /// exchange … and earns a +2d bonus on their challenge roll in the next exchange. Whoever wins
    /// the final exchange gets to describe the overall outcome of the contest."
    /// </summary>
    public const int ContestExchangeWinBonusDice = 2;
    public const bool ContestExchangeWinnerNarratesThatExchange = true;
    public const bool ContestFinalExchangeDecidesTheContest = true;

    /// <summary>
    /// "These challenges are contests, and they're broken down into a number of exchanges. Each
    /// exchange covers a separate part of the overall task and requires its own challenge roll." The
    /// arduous condition is the tail of the six-or-more sentence: "assuming the GM can make each
    /// exchange interesting".
    /// </summary>
    public const string ContestStructure =
        "a challenge split into exchanges, each with its own challenge roll";
    public const string ContestArduousCondition = "the GM can make each exchange interesting";

    // ── Defining Moments and the one-shot variant, p.70 ──────────────────────

    /// <summary>
    /// "First, any 6s you roll explode. … you can keep rerolling them as long as you keep rolling
    /// 6s (and each 6 still counts as 2 successes). Second, every point of Resolve you spend on
    /// that challenge roll earns you 3 extra dice instead of the usual 1 extra die. … Despite the
    /// name, Defining Moments can last longer than an instant. … You can only declare one Defining
    /// Moment per story. … only one Hero can declare a Defining Moment per scene (although Heroes
    /// in different scenes … can declare Defining Moments that just happen to occur at the same
    /// time). … you must permanently reduce one of your Abilities by 1d. … Although the reduction
    /// is permanent, this doesn't prevent you from spending Hero Points to raise that Ability in
    /// the future."
    /// </summary>
    public const int DefiningMomentsPage = 70;
    public const string DefiningMomentDeclaredBy = "the player, for a challenge roll everything rests on";
    public const bool DefiningMomentSixesExplode = true;
    public const bool DefiningMomentExplosionRecurses = true;
    public const int DefiningMomentDicePerResolveSpent = 3;
    public const int OrdinaryDicePerResolveSpent = 1;
    public const int DefiningMomentLimitPerStory = 1;
    public const int DefiningMomentLimitPerScene = 1;
    public const bool DefiningMomentConcurrentScenesEachAllowOne = true;
    public const bool DefiningMomentMayLastLongerThanAnInstant = true;
    public const int DefiningMomentPermanentAbilityLossDice = 1;
    public const bool DefiningMomentAbilityMayBeBoughtBackLater = true;

    public static readonly IReadOnlyList<string> DefiningMomentPhysicalAbilities =
        ["agility", "might", "toughness"];

    public static readonly IReadOnlyList<string> DefiningMomentMentalAbilities =
        ["intellect", "perception", "willpower"];

    /// <summary>
    /// "When playing a convention game or any one-shot game where you don't expect to use your Hero
    /// again… After you resolve your Defining Moment, your Health drops to 0 and you fall
    /// unconscious. You regain your wits at the end of the scene, but you suffer a −2d penalty to
    /// all challenge rolls for the rest of the story."
    /// </summary>
    public const string OneShotContext = "a convention game or any session where the Hero will not be used again";
    public const int OneShotHealthAfter = 0;
    public const bool OneShotUnconscious = true;
    public const string OneShotRegainConsciousness = "end of the scene";
    public const int OneShotChallengeRollPenaltyDice = -2;
    public const string OneShotPenaltyDuration = "the rest of the story";

    /// <summary>
    /// <b>The only replacement the page states, and it is stated about ordinary games.</b> "GMs may
    /// let Heroes in ordinary games choose this option <em>instead of</em> reducing one of their
    /// Abilities by 1d, but that's entirely optional."
    ///
    /// <para>The one-shot case itself is left open on purpose: that paragraph opens "Defining
    /// Moments are even more debilitating in one-shot games", which reads additively, and never
    /// says the new price replaces the rank. The entry's <c>ambiguity</c> carries both readings;
    /// nothing in the fact fields asserts either.</para>
    /// </summary>
    public const bool OneShotOptionOfferedInOrdinaryGamesAtGmOption = true;
    public const bool OneShotOptionInOrdinaryGamesReplacesTheAbilityLoss = true;
    public const bool OneShotOptionInOrdinaryGamesIsMandatory = false;

    // ── Judging thresholds, p.71 ─────────────────────────────────────────────

    public const int JudgingThresholdsPage = 71;

    public static readonly IReadOnlyList<Judging> JudgingThresholds =
    [
        new("difficult", "Hard", 2),
        new("heroically difficult", "Brutal", 4),
        new("super-heroically difficult", "Superhuman", 6)
    ];

    /// <summary>
    /// "remember this general guideline, which is especially applicable to Standard Heroes … and
    /// remember that it's perfectly okay to just wing it and go with whatever feels right."
    /// </summary>
    public const string JudgingThresholdsCalibratedFor = "Standard tier Heroes";
    public const bool JudgingThresholdsWingItIsEndorsed = true;

    // ── The worked example printed with the Challenge Rolls table, p.67 ──────

    /// <summary>
    /// Citizen Soldier and Gatecrasher arm-wrestling, printed under the Challenge Rolls table.
    /// Both have 12d Might; the Soldier declines to roll and banks 6, Gatecrasher rolls the
    /// twelve dice below for 7, and 1 net success puts him in the Actor-with-Embellishment band.
    ///
    /// <para><b>This is the fixture the whole file exists to support.</b> A transcription can be
    /// self-consistently wrong; an example the authors worked through in print cannot be.</para>
    /// </summary>
    public static class ArmWrestling
    {
        public const int Page = 67;
        public const int PoolDice = 12;

        /// <summary>Gatecrasher's twelve dice, exactly as printed.</summary>
        public static readonly IReadOnlyList<int> GatecrasherRoll =
            [1, 2, 2, 2, 3, 3, 3, 5, 5, 5, 6, 6];

        /// <summary>Three 2s at one each, two 6s at two each.</summary>
        public const int GatecrasherSuccesses = 7;

        /// <summary>Citizen Soldier takes 6 automatic successes for his 12d rather than rolling.</summary>
        public const int CitizenSoldierAutomaticSuccesses = 6;

        public const int NetSuccesses = 1;
        public const string Outcome = "actor";
        public const bool Embellishment = true;
    }
}
