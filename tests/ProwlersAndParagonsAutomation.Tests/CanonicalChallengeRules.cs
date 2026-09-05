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
/// </summary>
public static class CanonicalChallengeRules
{
    /// <summary>One row of a net-successes band table. A null bound is open-ended.</summary>
    public sealed record Band(int? Min, int? Max, string Outcome, bool? Embellishment);

    /// <summary>
    /// One row of the Thresholds table. <paramref name="Max"/> differs from
    /// <paramref name="Min"/> only on the three banded rows, and null means no ceiling.
    /// </summary>
    public sealed record Threshold(string Difficulty, int Min, int? Max, bool GmDiscretion);

    /// <summary>One row of the Judging Thresholds guideline.</summary>
    public sealed record Judging(string Descriptor, string Difficulty, int ThresholdValue);

    // ── The dice model, p.67 ─────────────────────────────────────────────────

    public const int DiceModelPage = 67;

    /// <summary>Face to successes: "one success for every 2 and 4 rolled, and two successes for every 6".</summary>
    public static readonly IReadOnlyDictionary<int, int> SuccessMap =
        new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 0, [4] = 1, [5] = 0, [6] = 2 };

    /// <summary>Below one die you still roll one, only a 6 counts, and it counts for one success.</summary>
    public const int SubOneDieDiceRolled = 1;
    public const int SubOneDieCountingFace = 6;
    public const int SubOneDieSuccessesWhenHit = 1;

    /// <summary>"1 automatic success for every 2 dice you choose to not roll (although the GM can veto this option)".</summary>
    public const int DiceNotRolledPerAutomaticSuccess = 2;
    public const bool AutomaticSuccessesCanBeVetoed = true;

    /// <summary>The GM's condition modifier, in dice, applied to either roll or to both.</summary>
    public const int ConditionModifierMinDice = -4;
    public const int ConditionModifierMaxDice = 4;

    // ── The Glossary's rounding rule, p.7 ────────────────────────────────────

    public const int HalfRulePage = 7;
    public const string HalfRoundingDirection = "up";

    /// <summary>
    /// The sole printed exception, and it belongs to Chapter 4 rather than to this one:
    /// Tough Minions defeats 1 Minion per 2 <em>full</em> net successes, "note that you are
    /// rounding down in this unique case".
    /// </summary>
    public const string HalfRuleExceptionName = "Tough Minions";
    public const int HalfRuleExceptionPage = 81;
    public const string HalfRuleExceptionDirection = "down";

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
        new("Easy", 0, 0, false),
        new("Average", 1, 1, false),
        new("Hard", 2, 2, false),
        new("Daunting", 3, 3, false),
        new("Brutal", 4, 4, false),
        new("Inhuman", 5, 5, false),
        new("Superhuman", 6, 8, true),
        new("Legendary", 9, 11, true),
        new("Godlike", 12, null, true)
    ];

    // ── Traditional Results table, p.69 ──────────────────────────────────────

    public const int TraditionalResultsPage = 69;

    public static readonly IReadOnlyList<Band> TraditionalResults =
    [
        new(null, -2, "complete_failure", null),
        new(-1, 0, "failure_with_silver_lining", null),
        new(1, 2, "success_with_complication", null),
        new(3, null, "complete_success", null)
    ];

    // ── Checking Your Swing, p.69 ────────────────────────────────────────────

    public const int CheckingYourSwingPage = 69;

    /// <summary>At this table setting every even face is worth exactly one success.</summary>
    public static readonly IReadOnlyDictionary<int, int> CheckingYourSwingSuccessMap =
        new Dictionary<int, int> { [1] = 0, [2] = 1, [3] = 0, [4] = 1, [5] = 0, [6] = 1 };

    public const int CheckingYourSwingExplodeCostResolve = 1;

    // ── Assisting and group actions, p.69 ────────────────────────────────────

    public const int AssistingPage = 69;
    public const int AssistHelperThreshold = 2;
    public const string AssistHelperDifficulty = "Hard";
    public const int AssistNetSuccessesPerBonusDie = 2;
    public const bool AssistBestHelperOnly = true;

    public const int GroupActionPage = 69;
    public const int GroupActionDistributionPivot = 3;

    // ── Contests, p.70 ───────────────────────────────────────────────────────

    public const int ContestsPage = 70;
    public const int ContestTypicalExchanges = 3;
    public const int ContestArduousExchanges = 6;
    public const int ContestExchangeWinBonusDice = 2;

    // ── Defining Moments and the one-shot variant, p.70 ──────────────────────

    public const int DefiningMomentsPage = 70;
    public const int DefiningMomentDicePerResolveSpent = 3;
    public const int OrdinaryDicePerResolveSpent = 1;
    public const int DefiningMomentLimitPerStory = 1;
    public const int DefiningMomentLimitPerScene = 1;
    public const int DefiningMomentPermanentAbilityLossDice = 1;

    public static readonly IReadOnlyList<string> DefiningMomentPhysicalAbilities =
        ["agility", "might", "toughness"];

    public static readonly IReadOnlyList<string> DefiningMomentMentalAbilities =
        ["intellect", "perception", "willpower"];

    public const int OneShotHealthAfter = 0;
    public const int OneShotChallengeRollPenaltyDice = -2;

    // ── Judging thresholds, p.71 ─────────────────────────────────────────────

    public const int JudgingThresholdsPage = 71;

    public static readonly IReadOnlyList<Judging> JudgingThresholds =
    [
        new("difficult", "Hard", 2),
        new("heroically difficult", "Brutal", 4),
        new("super-heroically difficult", "Superhuman", 6)
    ];

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
