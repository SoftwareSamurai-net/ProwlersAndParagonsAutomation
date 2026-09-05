namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The ten optional Gritty Combat Rules, Chapter 4 pp.79-81, transcribed from the rulebook, plus the
/// paragraph that introduces them. The combat rules they modify are in
/// <see cref="CanonicalCombatRules"/>. This is what <c>data/rules/play/gritty.json</c> is checked
/// against.
///
/// <para><b>Same standing as <see cref="CanonicalCombatRules"/>: this file is the rulebook.</b> Do
/// not "fix" a failing test by editing a number here.</para>
///
/// <para><b>Tough Minions is the one place in the whole book where a half goes downward</b>, and it
/// says so in as many words — "note that you are rounding down in this unique case". The Glossary's
/// rounding rule (p.7) records it as its single exception, and
/// <c>PlayRulesDataTests.ToughMinionsIsTheOnePlaceAHalfGoesDownward</c> asserts the pair together, by
/// name, so neither side can drift.</para>
/// </summary>
public static class CanonicalGrittyRules
{
    /// <summary>One band of the Slow Healing table (p.80). A null bound is open-ended.</summary>
    public sealed record HealingBand(int? MinToughness, int? MaxToughness, int HealthPerDay, int OneEvery);

    // ── The preamble, p.79 ───────────────────────────────────────────────────

    /// <summary>
    /// "These combat rules are not even slightly realistic. They reflect how combat works in superhero
    /// cartoons and to a lesser extent in comics and movies… If you want a grittier game in which
    /// combat is serious business, use one or more of the following Gritty Combat Rules. A number of
    /// these rules will have a dramatic impact on the game; review them carefully before deciding
    /// which ones you want to use, and allow yourself a retcon or a do-over if you decide not to use a
    /// particular rule after testing it in play."
    /// </summary>
    public static class Overview
    {
        public const string DefaultCombatIs =
            "four-color, and modelled on superhero cartoons more than on comics or movies";
        public const bool RulesAreOptional = true;
        public const bool AnySubsetMayBeUsed = true;
        public const bool ReviewBeforeAdopting = true;
        public const bool ARetconOrDoOverIsAllowedIfARuleIsDroppedAfterPlay = true;
    }

    // ── Active Defenses, p.79 ────────────────────────────────────────────────

    /// <summary>
    /// "Active defenses are minor actions. You suffer a cumulative −1d penalty to all active defense
    /// rolls after the first on the same page. For example, if you have to make 3 active defense rolls
    /// on the same page, you make the first roll at no penalty, the second roll at a −1d penalty, and
    /// the third roll at a −2d penalty. This rule does not affect your passive defenses in any way."
    /// </summary>
    public static class ActiveDefensePenalty
    {
        public const bool ActiveDefensesAreMinorActions = true;
        public const int CumulativePenaltyDicePerExtraActiveDefense = -1;
        public const bool FirstActiveDefenseOnAPageIsUnpenalised = true;
        public const string CountedPer = "page";
        public const bool AffectsPassiveDefenses = false;
    }

    // ── Close Range, p.79 ────────────────────────────────────────────────────

    /// <summary>
    /// "Ranged attacks are difficult to avoid up close. You suffer a −2d penalty to your active defense
    /// rolls against ranged attacks when you are within Close Range of your attacker. This applies only
    /// to attacks that can be used at Distant or Extreme Range. Ignore this rule when using active
    /// defenses against ordinary thrown weapons and other short-range attacks that can only be used at
    /// Close Range."
    /// </summary>
    public static class CloseRangePenalty
    {
        public const int PenaltyDiceToActiveDefense = -2;
        public const string AppliesAgainst = "ranged attacks made from within Close Range";
        public const string AppliesOnlyToAttacksUsableAt = "Distant or Extreme Range";
        public const string IgnoredFor =
            "ordinary thrown weapons and other attacks that can only be used at Close Range";
    }

    // ── The Drop, p.79 ───────────────────────────────────────────────────────

    /// <summary>
    /// "When a character has a weapon or Power aimed and ready to strike, they have the drop on everyone
    /// who doesn't. For example, if some crook has a gun aimed at Siren while her sidearm is holstered,
    /// he has the drop on her. It's the same if the crook is holding a knife to a hostage's throat…
    /// Similarly, a character armed with a ranged weapon or ranged attack Power has the drop on anyone
    /// moving up to them to engage them in close combat. When someone has the drop on you, their
    /// effective Edge is doubled against you… the GM always has the final say when deciding who has the
    /// drop on whom."
    /// </summary>
    public static class TheDrop
    {
        public const string HeldBy = "a character with a weapon or Power aimed and ready to strike";
        public const string HeldAgainst = "everyone who does not have one aimed and ready";
        public const string Effect = "the holder's effective Edge is doubled against them";
        public const string AlsoHeldBy =
            "a character with a ranged weapon or ranged attack Power, against anyone closing to engage them in close combat";

        public static readonly string[] ExamplesGiven =
        [
            "a crook with a gun aimed at a character whose sidearm is holstered",
            "a crook holding a knife to a hostage's throat"
        ];

        public const string FinalSay = "gm";
    }

    // ── Fatal Damage, p.79 ───────────────────────────────────────────────────

    /// <summary>
    /// "Damage can reduce your Health past 0 and into negative numbers. If you ever reach the negative
    /// value of your full Health, you have been killed. You can always spend 1 Resolve to reduce the
    /// damage you suffer or inflict on someone else to 1 point below this fatal threshold. This also
    /// leaves you stabilized if necessary… If an attack that inflicts lethal damage reduces you to −1
    /// Health or less, you begin dying. You suffer 1 point of damage every page until you are either
    /// stabilized or dead. You can be stabilized with a Hard (2) Medicine roll or a Power like Healing.
    /// You can also spend 1 Resolve to immediately stabilize yourself or your target. You must be stable
    /// to use the Instant Recovery rule discussed earlier in this chapter."
    ///
    /// <para>The worked example: "Clint Castle is a tough guy with 5 Health… he's down to 1 Health…
    /// A ninja master then stabs him for 6 points of damage, taking him down to −5 Health. Clint's full
    /// Health is 5, so that's just enough to kill him. Our hero spends 1 Resolve to prevent that from
    /// happening, leaving him at −4 Health."</para>
    ///
    /// <para><b>The printed word and the printed arithmetic disagree.</b> "1 point below this fatal
    /// threshold" computes to −6 at Clint's −5 threshold; the worked example two sentences later
    /// leaves him at −4, one point ABOVE it. <see cref="ResolveReducesDamageTo"/> carries the word as
    /// printed, because this file is the rulebook and not a repair of it — the reading the arithmetic
    /// supports is <see cref="InterpretedResolveReducesDamageTo"/>, and the entry's own <c>ambiguity</c>
    /// names the contradiction. <c>PlayRulesDataTests.TheFatalDamagePrintedWordAndItsWorkedExampleDisagree</c>
    /// proves both halves out of the corpus, so it fires if the page is ever re-extracted differently.</para>
    /// </summary>
    public static class FatalDamage
    {
        public const bool HealthCanGoNegative = true;
        public const string KilledAt = "the negative value of your full Health";
        public const int CostResolveToAvoid = 1;
        public const string ResolveReducesDamageTo = "1 point below the fatal threshold";

        /// <summary>Not printed. The reading the worked example's own arithmetic supports — see the
        /// class comment above — and what a simulator should compute rather than the printed word.</summary>
        public const string InterpretedResolveReducesDamageTo = "1 point above the fatal threshold";

        public const bool ResolveMayBeSpentOnDamageYouInflictOnSomeoneElse = true;
        public const bool ResolveAlsoStabilisesIfNecessary = true;
        public const int DyingBeginsWhenLethalDamageReducesYouTo = -1;
        public const int DyingDamagePerPage = 1;
        public const string DyingEndsAt = "stabilization or death";
        public const string StabiliseRoll = "Medicine";
        public const string StabiliseDifficulty = "Hard";
        public const int StabiliseThreshold = 2;
        public const string StabiliseAlsoBy = "a Power like Healing";
        public const int CostResolveToStabiliseImmediately = 1;
        public const bool InstantRecoveryRequiresBeingStable = true;

        /// <summary>Clint Castle, p.79, used as a fixture.</summary>
        public const int ExampleFullHealth = 5;
        public const int ExampleCurrentHealth = 1;
        public const int ExampleDamage = 6;
        public const int ExampleHealthAfter = -5;
        public const int ExampleHealthAfterSpendingResolve = -4;
    }

    // ── Friendly Fire, p.80 ──────────────────────────────────────────────────

    /// <summary>
    /// "You suffer a −4d penalty on your attack roll when firing a ranged attack at a target engaged in
    /// close combat or otherwise bunched up with other characters. If you score 0 or fewer net successes,
    /// you must make a second attack against another target involved in the melee, this time at no
    /// penalty. The GM selects this second target randomly."
    /// </summary>
    public static class FriendlyFire
    {
        public const int PenaltyDice = -4;
        public const string AppliesWhen =
            "firing a ranged attack at a target engaged in close combat or otherwise bunched up with other characters";
        public const int SecondAttackTriggeredAtNetSuccesses = 0;
        public const string SecondAttackIsAgainst = "another target involved in the melee";
        public const int SecondAttackPenaltyDice = 0;
        public const string SecondTargetSelectedBy = "gm";
        public const string SecondTargetSelected = "randomly";
    }

    // ── Hard Targets, p.80 ───────────────────────────────────────────────────

    /// <summary>
    /// "In the real world, hard targets like machines, vehicles, and thick, inanimate objects are sturdier
    /// than these rules suggest. To account for this, double a hard target's passive defense rank, but let
    /// attackers target the vulnerable parts of a complex machine or vehicle by accepting a −4d penalty to
    /// their attack rolls, thereby negating this effect. When using this rule, large vehicle-scale weapons
    /// should have the Penetrating Pro, as should the physical attacks of powerful superhuman characters."
    /// </summary>
    public static class HardTargets
    {
        public const string AppliesTo = "machines, vehicles, and thick inanimate objects";
        public const string PassiveDefenseRank = "doubled";
        public const int PenaltyDiceToNegateIt = -4;
        public const string NegationAvailableAgainst = "the vulnerable parts of a complex machine or vehicle";
        public const string RecommendedProForVehicleScaleWeapons = "Penetrating";
        public const string RecommendedProForThePhysicalAttacksOfPowerfulSuperhumanCharacters = "Penetrating";
    }

    // ── Raised Gear Limit, p.80 ──────────────────────────────────────────────

    /// <summary>
    /// "All games have a Gear Limit, which indicates the maximum effective Trait rank you can bring to bear
    /// when using mundane equipment. In most games, the Gear Limit is 6d. For example, a basic sword has a
    /// +2d Weapon Bonus, which means you roll Might +2d when using a sword to attack. In a game with a 6d
    /// Gear Limit, your maximum effective rank with a sword is 8d… In more realistic games, the Gear Limit
    /// can be raised to 9d, 12d, or more… For more on Gear Limits, see Chapter 6."
    /// </summary>
    public static class GearLimit
    {
        public const string WhatItIs =
            "the maximum effective Trait rank you can bring to bear when using mundane equipment";
        public const int DefaultRank = 6;
        public static readonly int[] RaisedOptions = [9, 12];
        public const bool RaisedOptionsAreOpenEnded = true;
        public const string WorkedExampleWeapon = "a basic sword";
        public const int WorkedExampleWeaponBonusDice = 2;
        public const int WorkedExampleMaximumEffectiveRankAtTheDefaultLimit = 8;
        public const string DetailChapter = "Ultimate Edition, Ch.6 Equipment";
    }

    // ── Slow Healing, p.80 ───────────────────────────────────────────────────

    /// <summary>
    /// "You heal 1 point of damage per day if your Toughness is 6d or less, 2 points per day (1 every 12
    /// hours) if your Toughness is 7d to 12d, 3 points per day (1 every 8 hours) if your Toughness is 13d to
    /// 24d, or 4 points per day (1 every 6 hours) if your Toughness is 25d or greater. You do not heal after
    /// each battle or when you regain consciousness after a defeat. As a result, you may be conscious while
    /// at 0 or negative Health (if using the Fatal Damage rules); you are defeated if you take even a single
    /// point of damage in this condition. Last, ordinary medical care is less effective. Although you can be
    /// stabilized as often as necessary, you can only be healed with the Medicine Talent once per week, and a
    /// successful Medicine roll heals only 1 point of damage per 2 net successes rolled."
    /// </summary>
    public static class SlowHealing
    {
        public static readonly IReadOnlyList<HealingBand> Bands =
        [
            new(null, 6, 1, 24),
            new(7, 12, 2, 12),
            new(13, 24, 3, 8),
            new(25, null, 4, 6)
        ];

        public const bool HealingAfterEachBattle = false;
        public const bool HealingOnRegainingConsciousnessAfterADefeat = false;
        public const bool YouMayBeConsciousAtZeroOrNegativeHealth = true;
        public const bool InThatConditionAnyDamageAtAllDefeatsYou = true;
        public const bool StabilizationAvailableAsOftenAsNecessary = true;
        public const string MedicineHealingLimit = "once per week";
        public const int MedicineHealthPerNetSuccesses = 1;
        public const int MedicineNetSuccessesPerPoint = 2;
    }

    // ── Tough Minions, p.81 ──────────────────────────────────────────────────

    /// <summary>
    /// "Accordingly, whenever you hit a group of Minions with an attack, you defeat 1 Minion for every 2 full
    /// net successes rolled (note that you are rounding down in this unique case). For example, if you roll 5
    /// net successes when attacking a group of Minions, you will only defeat 2 of them. When performing an
    /// area attack or other attack that normally affects 2 Minions per net successes rolled, you instead
    /// affect only 1 Minion per net success rolled when using this rule. If this still seems like too much,
    /// you can do away with the idea of Minions entirely and use Foes in their place."
    ///
    /// <para><b>This is the exception the Introduction's rounding rule names</b>, and the only place in the
    /// book where a half goes downward.</para>
    /// </summary>
    public static class ToughMinions
    {
        public const int NetSuccessesPerMinionDefeated = 2;
        public const bool FullNetSuccessesRequired = true;
        public const string Rounding = "down";
        public const bool RoundingIsANamedUniqueException = true;
        public const int WorkedExampleNetSuccesses = 5;
        public const int WorkedExampleMinionsDefeated = 2;
        public const int AreaAttackMinionsPerNetSuccess = 1;
        public const int AreaAttackRateItReplaces = 2;
        public const string AlternativeOffered = "do away with Minions entirely and use Foes in their place";
    }

    // ── Wound Penalties, p.81 ────────────────────────────────────────────────

    /// <summary>
    /// "Injuries impair your performance. Whenever you are down to half your full Health or less, you suffer a
    /// −2d penalty to all challenge rolls. Whenever you are down to 0 Health or less (which is possible when
    /// using the Fatal Damage rules) you suffer a −4d penalty to all challenge rolls. You can fight past your
    /// injuries by spending Resolve: every point spent allows you to ignore this penalty for 1 page."
    ///
    /// <para><b>The corpus filed this section's heading under the Example of Combat's until the extractor was
    /// fixed in this slice</b> — "EXAMPLE OF COMBAT — WOUND PENALTIES", the qualifier inverted. The prose was
    /// never damaged; only the heading was. See <c>docs/guide/rulebook-corpus.md</c>.</para>
    /// </summary>
    public static class WoundPenalties
    {
        public const int AtOrBelowHalfFullHealthPenaltyDice = -2;
        public const int AtOrBelowZeroHealthPenaltyDice = -4;
        public const string ZeroOrLessIsReachableOnlyWith = "the Fatal Damage rule";
        public const string AppliesTo = "all challenge rolls";
        public const int CostResolveToIgnore = 1;
        public const int PagesIgnoredPerResolvePoint = 1;
    }
}
