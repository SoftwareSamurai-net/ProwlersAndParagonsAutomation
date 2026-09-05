namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 4, Combat (printed pp.73-79), transcribed from the rulebook. The ten optional Gritty
/// Combat Rules on pp.79-81 are in <see cref="CanonicalGrittyRules"/> beside this. This is the
/// reference <c>data/rules/play/combat.json</c> is checked against, so a data edit that
/// contradicts the book fails a test rather than quietly changing how a simulated fight resolves.
///
/// <para><b>Same standing as <see cref="CanonicalPowers"/> and
/// <see cref="CanonicalChallengeRules"/>: this file is the rulebook.</b> Do not "fix" a failing
/// test by editing a number here. Open the page named beside the value and fix whichever side is
/// actually wrong.</para>
///
/// <para><b>Every value carries the sentence it came out of.</b> Chapter 4 states its mechanics
/// across seven printed pages and a value with no quote beside it is a claim nobody can check —
/// which is exactly how twenty fact fields in the Chapter 3 file once came to be unchecked.</para>
///
/// <para><b>What must never be here is a reading.</b> Three of this chapter's figures are ours
/// rather than the book's — that a Health average rounds up, that a special effect's duration
/// rounds up, and that the GM's alternative to seizing the initiative lasts as long as the
/// purchase it replaces — and all three live in the data under <c>interpretation</c>, derived by
/// a named test from a value that <em>is</em> printed. See <c>docs/guide/play-rules.md</c>.</para>
/// </summary>
public static class CanonicalCombatRules
{
    /// <summary>The chapter's printed range, and the pages this file transcribes.</summary>
    public const int FirstPage = 73;
    public const int LastPage = 82;

    /// <summary>One row of the Attack and Defense table (p.75).</summary>
    public sealed record AttackDefenseRow(string Type, string AttackTrait, string[] DefenseTraits);

    /// <summary>One row of the Throwing table (p.74). A null ceiling is open-ended.</summary>
    public sealed record ThrowingRow(int MinRank, int? MaxRank, string Range);

    /// <summary>One row of the Grappling table (p.76). A null bound is open-ended.</summary>
    public sealed record GrapplingRow(int? Min, int? Max, string Grab, string Hold, string Escape);

    /// <summary>One band of the Combat Stunts table (p.77).</summary>
    public sealed record StuntBand(int? Min, int? Max, string[] SampleEffects);

    /// <summary>One of the four sample stunts printed under that table (p.77).</summary>
    public sealed record SampleStunt(string Name, string[] AttackTraits, string[] DefenseTraits);

    /// <summary>One row of the Threat Ranks table (p.77). A null ceiling is open-ended.</summary>
    public sealed record ThreatRow(string Category, int MinThreat, int? MaxThreat);

    /// <summary>One row of the Minion Group Attack table (p.78).</summary>
    public sealed record MinionGroupRow(int MinMinions, int MaxMinions, int BonusDice);

    /// <summary>One of the three range classes (p.73).</summary>
    public sealed record RangeClass(string Name, string Covers);

    /// <summary>One band of a dice-modifier table (p.75): a condition and what it is worth.</summary>
    public sealed record ModifierBand(string Condition, int Dice);

    // ── Pages and turns, p.73 ────────────────────────────────────────────────

    /// <summary>
    /// "Time is broken down into pages during combat. A page represents a few seconds of time in
    /// the game world. Every character involved in combat gets a turn to act on each page of the
    /// action." — and, from ACTIONS on the same page, "Once everyone has taken their turn to act
    /// (or chosen to skip their turn), the current page ends and a new one begins."
    /// </summary>
    public static class Page
    {
        public const string APageIs = "a few seconds of time in the game world";
        public const int TurnsPerCharacterPerPage = 1;
        public const string PageEndsWhen = "everyone has taken their turn or chosen to skip it";
    }

    // ── Edge, p.73 ───────────────────────────────────────────────────────────

    /// <summary>
    /// "Characters act in order of their Edge, from highest to lowest. Your Edge equals your
    /// Perception plus Agility or your Perception plus Intellect—use whichever is greater. If you
    /// prefer random initiative, have characters make Edge rolls when combat begins and use their
    /// successes as their effective Edge for that battle."
    ///
    /// <para>Reprinted at Ch.2 p.60: "Your Edge equals your Perception plus the greater of your
    /// Agility or Intellect."</para>
    /// </summary>
    public static class Edge
    {
        public const string Formula = "edge = perception + max(agility, intellect)";
        public const string ActsInOrder = "highest Edge first";
        public const string OptionalRandomInitiative = "have everyone make an Edge roll when the fight starts";
        public const string RandomInitiativeEffectiveEdge = "the successes rolled";
        public const string RandomInitiativeLasts = "that battle";
        public const int CorroboratingPage = 60;
    }

    /// <summary>
    /// "When characters have the same Edge, action order is as follows: Heroes, Villains, Foes, and
    /// Extras. If that doesn't break the tie (such as when Heroes fight other Heroes), the
    /// characters act simultaneously, making it possible for them to knock each other out. Minions
    /// have no Edge, so they always act after everyone else. If it ever matters, Minion good guys
    /// and Minion bad guys act simultaneously."
    /// </summary>
    public static class TieBreak
    {
        public static readonly string[] Order = ["heroes", "villains", "foes", "extras"];
        public const string StillTiedAct = "simultaneously";
        public const bool SimultaneousCharactersCanKnockEachOtherOut = true;
        public const bool MinionsHaveAnEdge = false;
        public const string MinionsAct = "after everyone else";
        public const string MinionAlliesAndEnemiesAct = "simultaneously";
    }

    /// <summary>
    /// "You can always hold your action in reserve, waiting to act in response to something that
    /// might happen later that page. If whatever you were waiting for doesn't happen, you lose
    /// your turn to act on that page. If two or more characters hold their actions waiting for
    /// something to happen and it finally does, action order among those characters is determined
    /// by their Edge."
    /// </summary>
    public static class Holding
    {
        public const bool MayHoldInReserve = true;
        public const string WaitingFor = "something that might happen later on the same page";
        public const string IfItNeverHappens = "the turn to act is lost";
        public const string OrderAmongHolders = "edge";
    }

    // ── Seizing the initiative, p.73 ─────────────────────────────────────────

    /// <summary>
    /// "You can spend 1 Resolve to jump ahead of everyone else in combat. From that point on, you
    /// act first on every page of the action. When two or more characters seize the initiative,
    /// they go before everyone else, but their Edge scores determine the order in which they act."
    /// </summary>
    public static class SeizeInitiative
    {
        public const int CostResolve = 1;
        public const string Effect = "you act ahead of everyone else";
        public const string Duration = "every page of the action from that point on";
        public const bool SeizersGoBeforeEveryoneElse = true;
        public const string OrderAmongSeizers = "edge";
    }

    /// <summary>
    /// "Optionally, rather than allowing characters to automatically act first, GMs may instead
    /// have this double a character's effective Edge, making it harder to seize the initiative when
    /// facing opponents with truly superhuman reflexes."
    ///
    /// <para><b>There is no duration here and there must not be one.</b> The sentence attaches
    /// none, and reading it as inheriting <see cref="SeizeInitiative.Duration"/> is this
    /// repository's inference — it lives in the data under <c>interpretation</c> and is derived by
    /// a test from the value above rather than typed.</para>
    /// </summary>
    public static class SeizeInitiativeGmAlternative
    {
        public const string InsteadOf = "letting the buyer act first automatically";
        public const string Effect = "doubles the buyer's effective Edge";
        public const string ChosenBy = "gm";
        public const string Rationale =
            "so the purchase bites less against opponents with truly superhuman reflexes";
    }

    // ── Actions, p.73 ────────────────────────────────────────────────────────

    /// <summary>
    /// "When it's your turn to act, you can move and perform 1 or more actions. An action is a
    /// brief act that requires a bit of attention. Being that this is combat, attacks are the most
    /// common type of action… You can also defend yourself and perform as many free actions as the
    /// GM considers reasonable. Free actions are minor actions, things like drawing, sheathing, or
    /// dropping an item, opening or closing a door, or saying a few words to someone within Close
    /// Range."
    /// </summary>
    public static class Actions
    {
        public const string OnYourTurn = "you may move and perform one or more actions";
        public const string AnActionIs = "a brief act that requires a bit of attention";
        public const bool AttacksAreTheCommonestAction = true;
        public const bool DefendingYourselfIsAvailable = true;
        public const string FreeActionsAllowed = "as many as the GM considers reasonable";

        public static readonly string[] FreeActionExamples =
        [
            "drawing an item", "sheathing an item", "dropping an item",
            "opening or closing a door", "saying a few words to someone within Close Range"
        ];
    }

    /// <summary>
    /// "You can perform more than 1 action per page, but if you do, you suffer a −2d penalty to all
    /// challenge rolls per extra action taken… You must declare your multiple actions before making
    /// any challenge rolls. This penalty applies only to your multiple actions; it doesn't affect
    /// your defense rolls or other challenge rolls. Despite these rules, you can't attack the same
    /// target more than once per page. Actions and movement are different things, so you can't use
    /// multiple actions to move farther than normal."
    /// </summary>
    public static class MultipleActions
    {
        public const int PenaltyDicePerExtraAction = -2;
        public const string AppliesTo = "every challenge roll made for the actions taken";
        public const bool MustBeDeclaredBeforeAnyChallengeRoll = true;
        public const bool AppliesToDefenseRolls = false;
        public const bool AppliesToOtherChallengeRolls = false;
        public const bool SameTargetMoreThanOncePerPage = false;
        public const bool ExtraActionsBuyExtraMovement = false;
    }

    // ── Range, p.73 ──────────────────────────────────────────────────────────

    /// <summary>
    /// "Close Range covers anything from physical contact to within the distance an ordinary person
    /// can move in one page. Distant Range covers anything beyond Close Range but within range of
    /// most weapons and Powers. Extreme Range covers anything beyond Distant Range but close enough
    /// to see…"
    /// </summary>
    public static readonly IReadOnlyList<RangeClass> Ranges =
    [
        new("Close", "physical contact out to the distance an ordinary person can move in one page"),
        new("Distant", "beyond Close Range but within reach of most weapons and Powers"),
        new("Extreme", "beyond Distant Range but still close enough to see")
    ];

    /// <summary>
    /// "Rather than measure distances precisely, three range classes are used to approximate
    /// distances… The GM always determines the initial range class between combatants… Close combat
    /// attacks can only be used on targets that are adjacent to you. As long as you can move
    /// around, you can use a close combat attack on anyone within Close Range, but you have to move
    /// up to them if they aren't already adjacent to you. Ranged attacks, on the other hand, can
    /// typically be used on targets within Close Range or Distant Range. There are some
    /// exceptions—for example, thrown weapons normally only work within Close Range, while sniper
    /// rifles can often reach Extreme Range—but not many."
    /// </summary>
    public static class RangeRules
    {
        public const bool MeasuredPrecisely = false;
        public const string InitialRangeClassSetBy = "gm";
        public const string CloseCombatAttacksRequire =
            "an adjacent target, which may mean moving up to them first";
        public const string CloseCombatAttacksReach = "anyone within Close Range, as long as you can move";
        public const string RangedAttacksReach = "Close Range or Distant Range";

        public static readonly string[] ExceptionsGiven =
        [
            "thrown weapons normally only work within Close Range",
            "sniper rifles can often reach Extreme Range"
        ];
    }

    /// <summary>
    /// SUPER TIP!, p.74: "It may help to think of Close Range as within 50 feet, Distant Range as
    /// within 500 feet, and Extreme Range as within 5,000 feet, but these are just rough estimates."
    /// </summary>
    public static class RangeEstimates
    {
        public const int CloseFeet = 50;
        public const int DistantFeet = 500;
        public const int ExtremeFeet = 5000;
        public const string StatedAs = "rough estimates, offered only as an aid to imagining the bands";
    }

    // ── Throwing, p.74 ───────────────────────────────────────────────────────

    /// <summary>
    /// "Ordinary people can accurately throw weapons and light objects as far as Close Range… If
    /// your Might is greater than 6d, use the Throwing table to determine how far you can accurately
    /// throw things. Your throwing rank equals your Might minus the object's weight rank (minimum
    /// 0d)."
    /// </summary>
    public static class Throwing
    {
        public const string OrdinaryPeopleReach = "Close Range";
        public const int TableUsedWhenMightExceeds = 6;
        public const string RankFormula = "throwing rank = Might - the object's weight rank";
        public const int MinimumRank = 0;
        public const bool AccuracyIsWhatIsMeasured = true;
    }

    /// <summary>
    /// The Throwing table, p.74: "3d to 6d Close Range · 7d to 12d Distant Range · 13d to 24d
    /// Extreme Range · 25d or more Past Extreme Range".
    /// </summary>
    public static readonly IReadOnlyList<ThrowingRow> ThrowingTable =
    [
        new(3, 6, "Close Range"),
        new(7, 12, "Distant Range"),
        new(13, 24, "Extreme Range"),
        new(25, null, "Past Extreme Range")
    ];

    // ── Movement, p.74 ───────────────────────────────────────────────────────

    /// <summary>
    /// "Moving up to or away from someone already within Close Range of you takes one page. Moving
    /// doesn't prevent you from taking actions… Moving one range class closer to or farther away
    /// from someone takes 2 pages, unless you have a power that lets you move faster than normal (a
    /// Travel Power) at rank 6d or greater, in which case you can cross one range class per page.
    /// This assumes you're fighting in an ordinary environment with terrain that limits how fast
    /// characters with Travel Powers can move. If the terrain is wide open, the GM is free to let
    /// characters with Travel Powers at high ranks cross 2 or even 3 range classes in a single page."
    ///
    /// <para><b>The last sentence is a range with two qualifiers on it, and it used to be recorded as the
    /// number 3.</b> "2 or even 3" is a floor and a ceiling the GM chooses between; "if the terrain is wide
    /// open" and "at high ranks" are conditions on the whole allowance, and flattening them left a bare 3
    /// that reads as a rate. All four are separate constants below. <b>"High ranks" is not the 6d threshold
    /// restated</b> — that figure buys the ordinary one-class-per-page allowance three sentences earlier, so
    /// reading it in here would make one rank buy both; the page means a higher bar and never names it, which
    /// is what the entry's <c>ambiguity</c> records.</para>
    /// </summary>
    public static class Movement
    {
        public const int PagesToCloseOrOpenWithinCloseRange = 1;
        public const int PagesPerRangeClass = 2;
        public const int PagesPerRangeClassWithATravelPower = 1;
        public const int TravelPowerRankRequired = 6;
        public const bool MovingPreventsActions = false;
        public const string AssumedTerrain =
            "an ordinary environment whose terrain limits how fast a Travel Power can move";
        public const int OpenTerrainGmMayAllowRangeClassesPerPageMin = 2;
        public const int OpenTerrainGmMayAllowRangeClassesPerPageMax = 3;
        public const string OpenTerrainAllowanceAppliesTo = "characters with Travel Powers at high ranks";
        public const bool OpenTerrainAllowanceIsGmDiscretion = true;
    }

    /// <summary>
    /// "Whenever there's a question about how far or how fast you can move in one page, or when you
    /// need to know who gets somewhere first, have all characters involved in the action make
    /// challenge rolls using their Agility or Travel Power to determine who gets to describe what
    /// happens. Characters on foot use half their Agility when rolling against characters using
    /// Travel Powers…"
    /// </summary>
    public static class MovementContest
    {
        public const string Trigger =
            "any question of how far or how fast a character moves in one page, or who arrives somewhere first";
        public const string Roll = "Agility or a Travel Power";
        public const string OnFootAgainstATravelPowerUses = "half Agility";
        public const string WinnerGets = "to describe what happens";
    }

    // ── Chases, p.74 ─────────────────────────────────────────────────────────

    /// <summary>
    /// "Chases like these are handled as contests, but with an unlimited number of exchanges. Each
    /// exchange in a chase lasts one page… This roll isn't considered an action, so characters
    /// involved in a chase can still attack or perform other actions as usual. If you have multiple
    /// pursuers or quarries, each pursuer picks one quarry to chase (and roll against). As usual,
    /// winning an exchange grants you a +2d bonus on the next exchange. More importantly, scoring
    /// at least 3 net successes lets you close or expand the distance between you and your opponent
    /// by one range class. The chase ends if the parties move closer than Close Range or farther
    /// than Extreme Range. At closer than Close Range, the quarry gets cornered or outmaneuvered and
    /// can't go any farther (although they can still fight). At farther than Extreme Range, the
    /// quarry escapes."
    /// </summary>
    public static class Chase
    {
        public const string Structure = "a contest with an unlimited number of exchanges";
        public const int PagesPerExchange = 1;
        public const string Roll = "Agility or a Travel Power";
        public const string OnFootAgainstATravelPowerUses = "half Agility";
        public const bool TheRollIsAnAction = false;
        public const bool EachPursuerPicksOneQuarry = true;
        public const int ExchangeWinBonusDiceNextExchange = 2;
        public const int NetSuccessesToMoveOneRangeClass = 3;
        public const string EndsCloserThan = "Close Range";
        public const string EndsFartherThan = "Extreme Range";
        public const string AtCloserThanCloseRange =
            "the quarry is cornered or outmaneuvered and can go no farther, though they can still fight";
        public const string AtFartherThanExtremeRange = "the quarry escapes";
    }

    // ── Attacks and defenses, p.75 ───────────────────────────────────────────

    /// <summary>
    /// "When attacking, you make an attack roll using the Trait that corresponds to your attack, and
    /// your target makes a defense roll using the Trait that corresponds to their defense to
    /// determine the threshold you have to beat. If you roll more successes than your target, the
    /// attack hits and inflicts damage or a special effect… If you don't beat the threshold, the
    /// attack either misses or it hits the target but has no effect… Attackers use one Trait that
    /// lumps accuracy and damage together for their attack rolls, and defenders use one Trait that
    /// represents either defense or damage resistance—whichever is better—for their defense rolls."
    /// </summary>
    public static class Attack
    {
        public const string Roll = "the Trait that corresponds to the attack";
        public const string ThresholdSource = "the target's own defense roll";
        public const string OnMoreSuccessesThanTheTarget =
            "the attack hits and inflicts damage or a special effect";
        public const string OnFailingTheThreshold = "the attack misses, or hits with no effect";
        public const bool AccuracyAndDamageAreOneTrait = true;
        public const bool DefenseAndDamageResistanceAreOneTrait = true;
        public const string DefenderUses = "whichever of defense or damage resistance is better";
    }

    /// <summary>
    /// The Attack and Defense table, p.75: "Unarmed Might / Agility or Toughness or Power · Melee
    /// Weapon Might / Agility or ½ Toughness or Power · Ranged Weapon Agility / Agility or ½
    /// Toughness or Power · Physical Power Power / Agility or ½ Toughness or Power · Mental Power
    /// Power / Willpower or Power".
    /// </summary>
    public static readonly IReadOnlyList<AttackDefenseRow> AttackDefenseTable =
    [
        new("Unarmed", "Might", ["Agility", "Toughness", "Power"]),
        new("Melee Weapon", "Might", ["Agility", "1/2 Toughness", "Power"]),
        new("Ranged Weapon", "Agility", ["Agility", "1/2 Toughness", "Power"]),
        new("Physical Power", "Power", ["Agility", "1/2 Toughness", "Power"]),
        new("Mental Power", "Power", ["Willpower", "Power"])
    ];

    /// <summary>
    /// "Active defenses represent attempts to block, dodge, or parry attacks, while passive defenses
    /// represent the ability to resist or withstand attacks. Agility is a common active defense,
    /// while Toughness, Willpower, and Powers like Armor and Force Field are common passive
    /// defenses. The only distinction between active and passive defenses is that you can't use
    /// active defenses if you are immobilized, surprised, unconscious, or otherwise unable to
    /// actively defend yourself. As long as you can move, however, neither being in a cramped or
    /// awkward position nor losing your next turn to act prevents you from using active defenses. No
    /// matter how many defenses you have available to you, you always use only one defense against
    /// each attack, normally the one with the greatest rank."
    /// </summary>
    public static class Defenses
    {
        public const string ActiveRepresent = "attempts to block, dodge, or parry an attack";
        public const string PassiveRepresent = "the ability to resist or withstand an attack";
        public static readonly string[] CommonActiveTraits = ["Agility"];
        public static readonly string[] CommonPassiveTraits = ["Toughness", "Willpower", "Armor", "Force Field"];

        public static readonly string[] ActiveUnusableWhen =
        [
            "immobilized", "surprised", "unconscious", "otherwise unable to actively defend yourself"
        ];

        public const bool ACrampedOrAwkwardPositionPreventsActiveDefenses = false;
        public const bool LosingYourNextTurnPreventsActiveDefenses = false;
        public const int DefensesUsedPerAttack = 1;
        public const string DefenseChosen = "normally the one with the greatest rank";
    }

    /// <summary>
    /// "Most physical attacks, hazards, weapons, and Powers inflict lethal damage, the more
    /// dangerous of the two. Targets can use only half their Toughness as a passive defense against
    /// attacks that inflict lethal damage. However, there are a few attacks—notably unarmed attacks
    /// and those made with light clubbing weapons—that inflict subdual damage instead. Targets can
    /// use their full Toughness as a passive defense against attacks that inflict subdual damage.
    /// Unless otherwise noted, always assume physical damage is lethal. Psychic damage is neither
    /// lethal nor subdual and is resisted with Willpower in any case."
    /// </summary>
    public static class DamageTypes
    {
        public const bool LethalIsTheMoreDangerous = true;
        public const string ToughnessAgainstLethal = "half";
        public const string ToughnessAgainstSubdual = "full";

        public static readonly string[] SubdualSourcesGiven =
            ["unarmed attacks", "attacks made with light clubbing weapons"];

        public const string PhysicalDamageDefault = "lethal";
        public const string PsychicDamageIs = "neither lethal nor subdual";
        public const string PsychicDamageResistedWith = "Willpower";
    }

    // ── Modifiers, p.75 ──────────────────────────────────────────────────────

    /// <summary>
    /// "Cover affects your attack rolls. You suffer a −1d penalty if your target has light cover, a
    /// −2d penalty if they have heavy cover, or a −3d penalty if they have almost full cover. You
    /// can't hit a target completely hidden behind cover, but if your attack rank exceeds the
    /// cover's Structure, you can attack through it. If you do, your target can use the cover's
    /// Structure as a passive defense against your attack."
    /// </summary>
    public static class Cover
    {
        public const string Affects = "attack rolls";

        public static readonly IReadOnlyList<ModifierBand> Bands =
            [new("light", -1), new("heavy", -2), new("almost full", -3)];

        public const bool ACompletelyHiddenTargetCannotBeHit = true;
        public const string AttackingThroughCoverRequires = "an attack rank greater than the cover's Structure";
        public const bool TargetMayUseTheCoversStructureAsAPassiveDefense = true;
    }

    /// <summary>
    /// "Size affects your active defense rolls. You get a +1d bonus if your attacker is at least
    /// twice your size or a +2d bonus if they are at least 5 times your size. Conversely, you suffer
    /// a −1d penalty if your attacker is no more than half your size or a −2d penalty if they are no
    /// more than one-fifth your size."
    /// </summary>
    public static class Size
    {
        public const string Affects = "active defense rolls";

        public static readonly IReadOnlyList<ModifierBand> Bands =
        [
            new("at least twice your size", 1),
            new("at least 5 times your size", 2),
            new("no more than half your size", -1),
            new("no more than one-fifth your size", -2)
        ];
    }

    /// <summary>
    /// "Visibility affects your attack rolls and your active defense rolls. You suffer a −1d penalty
    /// if the visibility is poor or a −3d penalty if you have no visibility. Poor visibility includes
    /// dim lighting, fog, smoke, etc. No visibility is usually due to blindness or darkness. You
    /// effectively have no visibility against an invisible opponent unless you have a Power that
    /// compensates for this, like Blind Fighting or Radar."
    /// </summary>
    public static class Visibility
    {
        public const string Affects = "attack rolls and active defense rolls";

        public static readonly IReadOnlyList<ModifierBand> Bands = [new("poor", -1), new("none", -3)];

        public static readonly string[] PoorExamples = ["dim lighting", "fog", "smoke"];
        public static readonly string[] NoneExamples = ["blindness", "darkness"];
        public const bool AnInvisibleOpponentCountsAsNoVisibility = true;
        public static readonly string[] PowersThatCompensateGiven = ["Blind Fighting", "Radar"];
    }

    // ── Damage and Health, p.75 ──────────────────────────────────────────────

    /// <summary>
    /// "Every net success rolled on a damaging attack inflicts 1 point of damage. Damage reduces a
    /// target's Health. Once a target's Health falls to 0, they are defeated and knocked out for the
    /// rest of the scene. Because this game emulates a four-color comic book style, you don't have
    /// to worry about killing or being killed in combat unless using the optional Gritty Combat
    /// Rules."
    /// </summary>
    public static class Damage
    {
        public const int DamagePerNetSuccess = 1;
        public const string Reduces = "the target's Health";
        public const int DefeatedAtHealth = 0;
        public const string DefeatedMeans = "knocked out for the rest of the scene";
        public const bool DeathOnlyUnderTheGrittyCombatRules = true;
    }

    /// <summary>
    /// "Your Health equals the average of your Toughness and Might or the average of your Toughness
    /// and Willpower—use whichever option gives you a greater value. Villains and Foes use the same
    /// formula to calculate their suggested Health, but as mentioned earlier, this total is halved
    /// for Foes (suggested because NPC Health is always determined by the GM, as discussed in
    /// Chapter 8). Minions don't use Health, as discussed later in this chapter."
    ///
    /// <para>Reprinted at Ch.2 p.60: "Your Health equals the average of your Toughness and Might or
    /// the average of your Toughness and Willpower. Use whichever option gives you the most Health.
    /// When creating a Foe, use half this value."</para>
    ///
    /// <para><b>There is no rounding rule here and there must not be one.</b> Neither printing says
    /// which way an odd total goes; that the average rounds up follows from the Glossary's book-wide
    /// convention (p.7) and is this repository's inference, kept in the data under
    /// <c>interpretation</c> and derived by a test.</para>
    /// </summary>
    public static class Health
    {
        public const string Formula = "health = max(average(toughness, might), average(toughness, willpower))";
        public const bool VillainsUseTheSameFormula = true;
        public const bool FoesHalveTheResult = true;
        public const bool NpcTotalsAreSuggestions = true;
        public const bool MinionsUseHealth = false;
        public const int CorroboratingPage = 60;
    }

    /// <summary>
    /// "…(suggested because NPC Health is always determined by the GM, as discussed in Chapter 8)."
    /// The chapter is named and the rule is not restated, so the entry is a reference and carries
    /// nothing but the flag, the chapter, and what was deferred.
    /// </summary>
    public static class NpcHealth
    {
        public const bool TranscribedHere = false;
        public const string DetailChapter = "Ultimate Edition, Ch.8 Friends and Foes";
        public static readonly string[] DeferredTopics = ["how a GM settles an NPC's Health"];
    }

    // ── Healing, p.76 ────────────────────────────────────────────────────────

    /// <summary>
    /// "Once a fight ends, you can make a Hard (2) Toughness roll to recover 1 point of Health for
    /// every net success rolled. You can also do this after every night of rest… If you spend a full
    /// 24 hours doing nothing but resting, the threshold for this roll drops to Average (1). Once
    /// defeated and carted off to the authorities or otherwise removed from the scene, NPCs recover
    /// from their injuries as quickly as the plot requires."
    /// </summary>
    public static class Healing
    {
        public const string Roll = "Toughness";
        public const string AfterAFightDifficulty = "Hard";
        public const int AfterAFightThreshold = 2;
        public const int HealthPerNetSuccess = 1;
        public const string AlsoAvailableAfter = "every night of rest";
        public const int FullRestHours = 24;
        public const string FullRestDifficulty = "Average";
        public const int FullRestThreshold = 1;
        public const string RemovedNpcsRecover = "as quickly as the plot requires";
    }

    // ── Special effects, p.76 ────────────────────────────────────────────────

    /// <summary>
    /// "Powers like Ensnare, Mind Control, and Stun inflict conditions and effects other than
    /// damage, called special effects. Special effects last a number of pages equal to half the net
    /// successes rolled on your attack and expire at the end of your turn to act on that page. You
    /// can stack a special effect's duration by attacking the same target multiple times. If the
    /// duration of a special effect ever equals or exceeds the target's current Health, they are
    /// defeated by the effect, which then lasts for the rest of the scene."
    ///
    /// <para>The worked example on the same page: 8 successes against 3 leaves 5 net, and
    /// "Heartbreaker gains control of Parthian's mind for 3 pages" — which is round-up, though the
    /// rule itself never says so.</para>
    /// </summary>
    public static class SpecialEffect
    {
        public static readonly string[] SourcesGiven = ["Ensnare", "Mind Control", "Stun"];
        public const string DurationFormula = "pages = half the net successes rolled on the attack";
        public const string ExpiresAt = "the end of your turn to act on that page";
        public const bool DurationStacksByAttackingTheSameTargetAgain = true;
        public const string DefeatedWhenTheDurationReaches = "the target's current Health";
        public const string DefeatByEffectLasts = "the rest of the scene";

        /// <summary>The printed example, used as a fixture: 8 successes, 3 defence, 3 pages.</summary>
        public const int ExampleAttackSuccesses = 8;
        public const int ExampleDefenseSuccesses = 3;
        public const int ExampleDurationPages = 3;
    }

    /// <summary>
    /// "If you are suffering from a special effect but have not been defeated by it, you can try to
    /// free yourself of the effect when your turn to act comes along. Make a challenge roll using
    /// the passive defense identified in the Power's description (often Might, Toughness, or
    /// Willpower) against the Power's rank. If successful, the special effect's duration is reduced
    /// by half your net successes. If you reduce the duration to 0 or fewer pages, you break free of
    /// the effect and can act on that same page."
    ///
    /// <para>The worked example on the same page: 7 successes against 4 leaves 3 net, and "This
    /// reduces the Mind Control duration by 2 pages" — round-up again, and again unstated in the
    /// rule.</para>
    /// </summary>
    public static class BreakingFree
    {
        public const string AvailableWhen = "suffering a special effect without having been defeated by it";
        public const string TakenOn = "your turn to act";
        public const string Roll = "the passive defense named in the Power's description";
        public static readonly string[] RollExamplesGiven = ["Might", "Toughness", "Willpower"];
        public const string ThresholdSource = "the Power's rank";
        public const string DurationReducedBy = "half your net successes";
        public const int FreeWhenTheDurationReaches = 0;
        public const bool MayActOnTheSamePageWhenFreed = true;

        /// <summary>The printed example, used as a fixture: 7 successes, 4 defence, 2 pages removed.</summary>
        public const int ExampleAttemptSuccesses = 7;
        public const int ExampleOpposingSuccesses = 4;
        public const int ExamplePagesRemoved = 2;
    }

    /// <summary>
    /// "Whenever you defeat a target with a special effect, you can spend 1 Resolve to extend the
    /// effect's duration so that it lasts until the end of the following scene. You can keep
    /// extending a special effect's duration from one scene to the next as long as you keep spending
    /// Resolve."
    /// </summary>
    public static class KeepingHold
    {
        public const string Trigger = "defeating a target with a special effect";
        public const int CostResolve = 1;
        public const string ExtendsTo = "the end of the following scene";
        public const bool MayBeRepeatedSceneAfterScene = true;
    }

    /// <summary>
    /// "If you get defeated by an attack that inflicts damage, you can spend 1 point of Resolve on
    /// your next turn to act to regain consciousness and recover 3 points of Health. Similarly, if
    /// you're under the influence of a special effect Power, you can spend 1 point of Resolve on your
    /// next turn to act to break free of it (notice you don't need to be defeated to use this option
    /// to free yourself of a special effect)… You can only use instant recovery once per scene."
    /// </summary>
    public static class InstantRecovery
    {
        public const int CostResolve = 1;
        public const string TakenOn = "your next turn to act";
        public const bool AfterADamagingDefeatRegainsConsciousness = true;
        public const int AfterADamagingDefeatRestoresHealth = 3;
        public const bool AlsoFreesYouFromASpecialEffect = true;
        public const bool RequiresBeingDefeatedToFreeYourselfFromAnEffect = false;
        public const int LimitPerScene = 1;
    }

    // ── Grappling, pp.76-77 ──────────────────────────────────────────────────

    /// <summary>
    /// "Grappling is often just another way of inflicting damage in close combat. You don't need
    /// special rules for that. The rules below apply when you try to perform a grab, hold, or escape.
    /// A grab is any attempt to take a weapon or other handheld item away from your opponent. A hold
    /// is any attempt to control or restrain your opponent. An escape is any attempt to break out of
    /// a hold. These moves all require a Might roll against your opponent's Might. If you aren't
    /// already grappling, your opponent can instead use an active defense against your roll."
    /// </summary>
    public static class Grappling
    {
        public const string AGrabIs = "an attempt to take a weapon or other handheld item away from an opponent";
        public const string AHoldIs = "an attempt to control or restrain an opponent";
        public const string AnEscapeIs = "an attempt to break out of a hold";
        public const string Roll = "Might";
        public const string ThresholdSource = "the opponent's Might";
        public const bool OpponentMayUseAnActiveDefenseInsteadWhenNotAlreadyGrappling = true;
        public const bool InflictingOrdinaryDamageInCloseCombatNeedsNoSpecialRules = true;
    }

    /// <summary>
    /// The Grappling table, p.76: "0 or Less No Effect / No Effect / No Effect · 1 to 2 Partial Grab
    /// / Partial Hold / Partial Escape · 3 or More Full Grab / Full Hold / Full Escape".
    /// </summary>
    public static readonly IReadOnlyList<GrapplingRow> GrapplingTable =
    [
        new(null, 0, "no effect", "no effect", "no effect"),
        new(1, 2, "partial grab", "partial hold", "partial escape"),
        new(3, null, "full grab", "full hold", "full escape")
    ];

    /// <summary>
    /// "A partial grab means you and your opponent are fighting over an item (often a weapon). They
    /// can't use it, but neither can you. As long as this continues, neither of you can perform
    /// active defenses against anyone else, and you each get to make opposed Might rolls on your turn
    /// to act to try gaining control. You can exit grappling combat at any time by letting go of the
    /// object. A full grab means you gain control of the object and can use it or toss it aside on
    /// that same page without suffering a multiple action penalty. In effect, a full grab is a free
    /// action."
    /// </summary>
    public static class Grab
    {
        public const string PartialMeans = "both characters are fighting over the item and neither can use it";
        public const bool PartialBlocksActiveDefensesAgainstAnyoneElse = true;
        public const string PartialResolvedBy = "an opposed Might roll on each character's turn to act";
        public const bool MayExitByLettingGoOfTheObject = true;
        public const string FullMeans = "control of the object";
        public const bool FullAllowsUsingOrTossingItTheSamePage = true;
        public const bool FullSuffersTheMultipleActionPenalty = false;
        public const bool FullIsInEffectAFreeAction = true;
    }

    /// <summary>
    /// "A partial hold means you and your opponent start wrestling. They can't go anywhere or do
    /// anything else, but neither can you… neither of you can perform active defenses against anyone
    /// else. The only physical action either of you can take on your turn to act is making an opposed
    /// Might roll against the other to try for a full hold or an escape. A full hold means you gain
    /// control over your opponent. From then on, the only physical action they can take is to try
    /// escaping, but you can attack them on subsequent pages. You can inflict damage on them by
    /// making a Might roll against the greater of their Might or Toughness, or you can inflict pain to
    /// make them give in, tap out, or surrender… by making a Might roll against the greater of their
    /// Toughness or Willpower." And: "characters in a partial hold or a full hold remain free to use
    /// any Power they could reasonably use despite being physically restrained… Powers that need to be
    /// aimed (including most attack Powers) or that require freedom of movement (including most Travel
    /// Powers) are probably not available while you are held. Beyond that, the GM will have to
    /// adjudicate these matters on a case-by-case basis."
    /// </summary>
    public static class Hold
    {
        public const string PartialMeans = "both characters are wrestling and neither can move or do anything else";
        public const bool PartialBlocksActiveDefensesAgainstAnyoneElse = true;
        public const string PartialOnlyPhysicalAction = "an opposed Might roll, aiming for a full hold or an escape";
        public const string FullMeans = "control over the opponent";
        public const string FullLeavesTheHeldCharacterOnly = "trying to escape";
        public const bool FullAllowsAttacksOnSubsequentPages = true;
        public const string FullDamageRoll = "Might";
        public const string FullDamageThresholdSource = "the greater of the target's Might or Toughness";
        public const string FullSubmissionRoll = "Might";
        public const string FullSubmissionThresholdSource = "the greater of the target's Toughness or Willpower";
        public const string AHeldCharacterMayUse = "any Power they could reasonably use while physically restrained";

        public static readonly string[] PowersProbablyUnavailableWhenHeld =
            ["Powers that need to be aimed", "Powers that require freedom of movement"];

        public const string AdjudicatedCaseByCaseBy = "gm";
    }

    /// <summary>
    /// "A partial escape allows you to slip out of a partial hold. If your opponent has you in a full
    /// hold, a partial escape turns the full hold into a partial hold. A full escape lets you slip out
    /// of any hold. Either way, if you slip out of a hold, you can also choose to exit grappling
    /// combat completely."
    /// </summary>
    public static class Escape
    {
        public const string PartialOutOfAPartialHold = "you slip out of it";
        public const string PartialOutOfAFullHold = "it becomes a partial hold";
        public const string Full = "you slip out of any hold";
        public const bool MayAlsoExitGrapplingCompletely = true;
    }

    // ── Combat stunts, p.77 ──────────────────────────────────────────────────

    /// <summary>
    /// "A combat stunt is any attempt to outmaneuver, outsmart, or gain an advantage over an opponent
    /// without harming them… Combat stunts work like ordinary attacks, but the Traits used to perform
    /// and resist them vary depending on what you're doing; the GM always determines this. If
    /// successful, you describe the effect your combat stunt has on your opponent… The effect will
    /// last until the end of your next turn to act (you can't make it last longer by delaying your
    /// next action). The penalties imposed by multiple combat stunts are cumulative."
    /// </summary>
    public static class CombatStunt
    {
        public const string WhatItIs =
            "an attempt to outmaneuver, outsmart, or gain an advantage over an opponent without harming them";
        public const string WorksLike = "an ordinary attack";
        public const string TraitsUsedAreChosenBy = "gm";
        public const string EffectDescribedBy = "the character performing it";
        public const string Duration = "until the end of your next turn to act";
        public const bool DelayingYourNextActionExtendsIt = false;
        public const bool PenaltiesFromMultipleStuntsAreCumulative = true;
    }

    /// <summary>
    /// The four sample stunts printed under the table, p.77 — "Fancy Footwork… Use Agility or a Travel
    /// Power against your opponent's Agility or Travel Power. Flashy Move… Use a regular attack roll
    /// against your opponent's active defense or one of their physical Abilities; the GM decides
    /// whether to use Agility, Might, or Toughness… Overpower… Use Might or a Power that affects
    /// physical objects (like Hyper Breath or Telekinesis) against your opponent's Might or any active
    /// defense they prefer. Taunt… Use Charm or Command against your opponent's Willpower."
    /// </summary>
    public static readonly IReadOnlyList<SampleStunt> SampleStunts =
    [
        new("Fancy Footwork", ["Agility", "a Travel Power"], ["Agility", "a Travel Power"]),
        new("Flashy Move", ["a regular attack roll"],
            ["an active defense", "Agility", "Might", "Toughness"]),
        new("Overpower", ["Might", "a Power that affects physical objects"],
            ["Might", "any active defense the opponent prefers"]),
        new("Taunt", ["Charm", "Command"], ["Willpower"])
    ];

    /// <summary>
    /// The Combat Stunts table, p.77: "0 or Less no effect · 1 to 2 −1d to challenge rolls • speed
    /// halved · 3 to 4 −2d to challenge rolls • drop item or weapon • can't move · 5 or More −3d to
    /// challenge rolls • lose next turn to act". Transcribed with the book's minus sign folded to a
    /// hyphen, the way every other dice figure in <c>data/rules/</c> is written.
    /// </summary>
    public static readonly IReadOnlyList<StuntBand> CombatStuntBands =
    [
        new(null, 0, []),
        new(1, 2, ["-1d to challenge rolls", "speed halved"]),
        new(3, 4, ["-2d to challenge rolls", "drop item or weapon", "can't move"]),
        new(5, null, ["-3d to challenge rolls", "lose next turn to act"])
    ];

    // ── Minions, pp.77-78 ────────────────────────────────────────────────────

    /// <summary>
    /// "Minions only have a Threat characteristic, and they act in groups rather than as individual
    /// characters in combat."
    /// </summary>
    public static class Minions
    {
        public const string OnlyCharacteristic = "Threat";
        public const bool ActInGroupsRatherThanAsIndividuals = true;
    }

    /// <summary>
    /// The Threat Ranks table, p.77 — "Civilians 2d · Bruisers 3d · Professionals 4d · Elites 5d ·
    /// Enhanced 6d · Super 7d or More" — reprinted from Ch.2 p.13, where the identical table appears
    /// under MINIONS.
    /// </summary>
    public static readonly IReadOnlyList<ThreatRow> ThreatRanks =
    [
        new("Civilians", 2, 2),
        new("Bruisers", 3, 3),
        new("Professionals", 4, 4),
        new("Elites", 5, 5),
        new("Enhanced", 6, 6),
        new("Super", 7, null)
    ];

    /// <summary>The page of Chapter 2 that reprints the Threat Ranks table.</summary>
    public const int ThreatRanksCorroboratingPage = 13;

    /// <summary>
    /// "Minions don't have Health scores. Whenever you hit a group of Minions in combat, you defeat 1
    /// Minion per net success rolled, or 2 Minions per net success rolled when using an area attack
    /// (up to the number of Minions in the area of effect or within reach). No attack can defeat more
    /// than 2 Minions per net success rolled regardless of the situation (in other words, you can't
    /// stack effects that let you defeat 2 Minions per net success rolled). If the attack inflicts
    /// damage, defeated Minions are knocked out. If it inflicts a special effect, defeated Minions are
    /// subject to that effect for the rest of the scene."
    ///
    /// <para><b>The cap is a parenthesis on the area-attack clause, and it is recorded there.</b> It closes
    /// "2 Minions per net success rolled when using an area attack (…)", not the sentence — so the constant
    /// is <see cref="AreaAttackCappedBy"/> and not a bare <c>CappedBy</c> that reads as covering the whole
    /// entry. Its own second half reads wider than its position: "or within reach" is the phrase for an
    /// ordinary attack rather than an area one. That is the entry's <c>ambiguity</c>, not a licence to file
    /// the parenthesis somewhere it is not printed.</para>
    /// </summary>
    public static class AttackingMinions
    {
        public const bool MinionsHaveHealth = false;
        public const int MinionsDefeatedPerNetSuccess = 1;
        public const int MinionsDefeatedPerNetSuccessWithAnAreaAttack = 2;
        public const string AreaAttackCappedBy = "the number of Minions in the area of effect or within reach";
        public const int MaximumMinionsPerNetSuccess = 2;
        public const bool EffectsThatDoubleTheRateDoNotStack = true;
        public const string OnADamagingAttack = "the defeated Minions are knocked out";
        public const string OnASpecialEffect = "the defeated Minions are subject to it for the rest of the scene";
    }

    /// <summary>
    /// "Although a group of Minions can break into two or more groups to attack multiple Heroes, each
    /// group acts like a single character. Each Minion group targets one enemy per page, making one
    /// attack roll opposed by one defense roll… Again, this bonus applies only to attack rolls; it does
    /// not apply when determining whether an attack can penetrate cover or harm characters using Powers
    /// like Armor or Force Field. Up to 6 Minions can gang up on one target in close combat, but up to
    /// 12 can attack the same target in ranged combat."
    /// </summary>
    public static class MinionsAttacking
    {
        public const string AGroupActsLike = "a single character";
        public const bool AGroupMaySplitToAttackMultipleEnemies = true;
        public const int TargetsPerGroupPerPage = 1;
        public const int AttackRollsPerGroupPerPage = 1;
        public const int DefenseRollsOpposingIt = 1;
        public const string TheGroupBonusAppliesTo = "attack rolls only";
        public const string TheGroupBonusDoesNotApplyTo =
            "penetrating cover, or harming characters using Powers like Armor or Force Field";
        public const int MaximumAttackingOneTargetInCloseCombat = 6;
        public const int MaximumAttackingOneTargetAtRange = 12;
    }

    /// <summary>
    /// The Minion Group Attack table, p.78: "1 to 2 +2d · 3 to 5 +4d · 6 to 8 +6d · 9 to 12 +8d".
    /// </summary>
    public static readonly IReadOnlyList<MinionGroupRow> MinionGroupAttack =
    [
        new(1, 2, 2), new(3, 5, 4), new(6, 8, 6), new(9, 12, 8)
    ];

    // ── Special cases, pp.78-79 ──────────────────────────────────────────────

    /// <summary>
    /// "Ambushing someone requires a successful challenge roll using your Covert against your target's
    /// Perception. When using deception or seduction to catch a target off guard in a different way,
    /// roll Charm instead of Covert. If you succeed, your target is surprised and can't act or use
    /// active defenses on the first page of combat. Targets with embellishment rights can say they were
    /// only partially surprised, which lets them use their active defenses, but that's about it. If you
    /// fail, your target isn't surprised and can act normally. When you have multiple ambushers, they can
    /// make Covert rolls as a group… Either way, each target should be allowed their own Perception roll
    /// (although Minions should roll in groups)."
    ///
    /// <para><b>"But that's about it" is a hedge, and it used to be transcribed as "and nothing else".</b>
    /// The two are not the same sentence: the page grants the active defenses and then declines to enumerate
    /// what else, if anything, a partially surprised target keeps. <see cref="PartialSurpriseKeeps"/> carries
    /// the grant and <see cref="PartialSurpriseLimitPrintedAs"/> carries the qualifier in the book's own
    /// words; the entry's <c>ambiguity</c> says what neither settles.</para>
    /// </summary>
    public static class Ambush
    {
        public const string Roll = "Covert";
        public const string DeceptionOrSeductionRoll = "Charm";
        public const string ThresholdSource = "the target's Perception";
        public const string OnSuccess = "the target is surprised";
        public const bool ASurprisedTargetCanAct = false;
        public const bool ASurprisedTargetCanUseActiveDefenses = false;
        public const string SurpriseLasts = "the first page of combat";
        public const bool EmbellishmentRightsAllowPartialSurprise = true;
        public const string PartialSurpriseKeeps = "their active defenses";
        public const string PartialSurpriseLimitPrintedAs = "but that's about it";
        public const string OnFailure = "the target is not surprised and acts normally";
        public const bool MultipleAmbushersMayRollAsAGroup = true;
        public const bool EveryTargetRollsTheirOwnPerception = true;
        public const bool MinionsRollPerceptionInGroups = true;
    }

    /// <summary>
    /// "Explosions, blasts of dragon fire, and other attacks that target an area of effect are area
    /// attacks. Area attacks target everyone and everything in their area of effect. When performing an
    /// area attack, you make a single attack roll and everyone in the area makes their own defense roll.
    /// Area attacks are hard to evade; a target using an active defense against an area attack must
    /// either halve their defense rank or forfeit their next turn to act diving out of the area or
    /// behind cover."
    /// </summary>
    public static class AreaAttack
    {
        public const string Targets = "everyone and everything in the area of effect";
        public const int AttackRolls = 1;
        public const string DefenseRolls = "one from every character in the area";

        public static readonly string[] AnActiveDefenseMustEither =
            ["halve its rank", "forfeit the next turn to act"];

        public static readonly string[] ExamplesGiven = ["explosions", "blasts of dragon fire"];
    }

    /// <summary>
    /// "A charge attack is any attack in which you slam into your target. You can use any Trait you
    /// would normally use to make close combat attacks, but since weight and speed matter here, you can
    /// also use Density, Growth, or any Travel Power to make your attack roll (obviously, you can't use
    /// Swimming unless you're underwater). You get a +2d bonus to your attack roll, but your active
    /// defense ranks are halved until after your next turn to act. If your target uses a passive defense
    /// against the charge, you have to make your own passive defense roll against the attack to see if
    /// you suffer damage from the impact. On the bright side, the damage you suffer is reduced by the
    /// amount you inflict on your target."
    /// </summary>
    public static class Charge
    {
        public const string WhatItIs = "an attack in which you slam into your target";

        public static readonly string[] AttackTraits =
            ["any Trait usable for a close combat attack", "Density", "Growth", "any Travel Power"];

        public const bool SwimmingMayBeUsedOnlyUnderwater = true;
        public const int AttackBonusDice = 2;
        public const string OwnActiveDefenseRanks = "halved";
        public const string PenaltyLasts = "until after your next turn to act";
        public const string IfTheTargetUsesAPassiveDefense =
            "the charger makes their own passive defense roll against the attack to see whether the impact hurts them";
        public const string SelfDamageReducedBy = "the damage inflicted on the target";
    }

    /// <summary>
    /// "A clobbering attack is when you use one enemy to hit another… you make a single attack roll at a
    /// −2d penalty, and each target makes their own defense roll. Clobbering attacks always have a primary
    /// target and a secondary target. The primary target is the one you're pushing, throwing, or swinging
    /// into the other. If the primary target uses an active defense and suffers no damage, the clobbering
    /// attack never makes it to the secondary target."
    /// </summary>
    public static class Clobbering
    {
        public const string WhatItIs = "using one enemy to hit another";
        public const int AttackRolls = 1;
        public const int AttackPenaltyDice = -2;
        public const string DefenseRolls = "one from each target";
        public static readonly string[] Targets = ["primary", "secondary"];
        public const string ThePrimaryTargetIs = "the one being pushed, thrown, or swung into the other";
        public const bool StopsIfThePrimaryDefendsActivelyAndTakesNoDamage = true;
    }

    /// <summary>
    /// "You can forego your next turn to act to defend anyone within Close Range of you… This lets you
    /// substitute your defense roll for theirs. You can use active or passive defenses when defending
    /// others. If you use an active defense and the attack inflicts damage, it harms the person you were
    /// trying to protect. Or at least it might, because the original target can still use a passive
    /// defense to resist the attack. On the other hand, if you use a passive defense and the attack
    /// inflicts damage, it harms you."
    /// </summary>
    public static class DefendingOthers
    {
        public const string Cost = "forego your next turn to act";
        public const string Range = "Close Range";
        public const string Effect = "your defense roll is substituted for theirs";
        public const bool MayUseAnActiveOrAPassiveDefense = true;
        public const string AnActiveDefenseLeavesTheDamageOn = "the character being protected";
        public const bool TheProtectedCharacterMayStillUseAPassiveDefense = true;
        public const string APassiveDefenseLeavesTheDamageOn = "the defender";
    }

    /// <summary>
    /// "All-out attacking grants you a +2d bonus to your attack rolls but halves your defense ranks (both
    /// active and passive) until after your next turn to act. Despite this, opponents who can't harm you
    /// because they can't penetrate your Armor, Force Field, or other passive defense remain unable to do
    /// so when you all-out attack."
    /// </summary>
    public static class AllOutAttack
    {
        public const int AttackBonusDice = 2;
        public const string DefenseRanks = "halved";
        public const bool AffectsActiveDefenses = true;
        public const bool AffectsPassiveDefenses = true;
        public const string Lasts = "until after your next turn to act";
        public const string OpponentsWhoCouldNotPenetrateYourPassiveDefense = "still cannot";
    }

    /// <summary>
    /// "All-out defending grants you a +2d bonus to your defense rolls until your next turn to act comes
    /// around but prevents you from attacking or performing other actions when it does. Although you can't
    /// perform actions, you can still move and perform free actions while all-out defending. In fact, when
    /// performing an all-out defense, you can use your Travel Power or Speed to make active defense rolls."
    /// </summary>
    public static class AllOutDefense
    {
        public const int DefenseBonusDice = 2;
        public const string Lasts = "until your next turn to act comes around";
        public const bool PreventsAttacking = true;
        public const bool PreventsOtherActions = true;
        public const bool AllowsMovement = true;
        public const bool AllowsFreeActions = true;
        public const bool ATravelPowerOrSpeedMayBeUsedAsAnActiveDefense = true;
    }

    /// <summary>
    /// "Any time you hit a target with an attack that inflicts subdual damage and inflicts at least 6
    /// points of damage, you can spend 1 point of Resolve to inflict knockback on them. A target that
    /// suffers knockback flies backwards as if they were thrown by someone with a Might rank equal to your
    /// attack rank. They also fall prone, losing their next turn to act. If the target hits a solid object,
    /// they suffer half as much damage as the original attack inflicted, assuming the object they strike is
    /// tougher than they are (if the target's passive defense exceeds the object's Structure, they smash
    /// though unharmed)."
    /// </summary>
    public static class Knockback
    {
        public const string RequiresDamageType = "subdual";
        public const int MinimumDamage = 6;
        public const int CostResolve = 1;
        public const string TargetIsThrownAsIfByAMightRankEqualTo = "your attack rank";
        public const bool TargetFallsProne = true;
        public const bool TargetLosesTheirNextTurnToAct = true;
        public const string DamageOnStrikingASolidObject = "half the original attack's damage";
        public const bool TheObjectMustBeTougherThanTheTarget = true;
        public const string APassiveDefenseAboveTheObjectsStructure = "the target smashes through unharmed";
    }

    /// <summary>
    /// "Luring an enemy involves having them attack you and then moving out of the way at the last possible
    /// instant so they strike what's behind you. Whenever an enemy targets you with a physical or energy
    /// attack, you can declare you are luring them before they make their attack roll. You must use an
    /// active defense when luring. If your defense roll exceeds their attack roll by 3 or more, you can
    /// spend 1 Resolve to have the attack strike whatever lies directly behind you. You can lure an opponent
    /// into attacking someone else (rather than an inanimate object), but you have to forego you next turn
    /// to act to do so, and the new target is allowed to make their own defense roll against the attack."
    /// </summary>
    public static class Luring
    {
        public const string WhatItIs =
            "letting an enemy attack you and moving aside so their attack strikes what is behind you";
        public static readonly string[] AppliesToAttackTypes = ["physical", "energy"];
        public const string DeclaredBefore = "the attacker makes their attack roll";
        public const bool RequiresAnActiveDefense = true;
        public const int DefenseMustExceedTheAttackRollBy = 3;
        public const int CostResolve = 1;
        public const string RedirectsTo = "whatever lies directly behind you";
        public const bool MayRedirectOntoAPerson = true;
        public const string RedirectingOntoAPersonCosts = "your next turn to act";
        public const bool TheNewTargetMakesTheirOwnDefenseRoll = true;
    }

    /// <summary>
    /// "When performing a team attack, you and your allies have to wait until the end of the page to take
    /// your actions, and you all have to target the same enemy. This grants you a +2d bonus to your attack
    /// rolls and lets you spend 1 Resolve to have your 6s explode… you can keep rerolling them as long as
    /// you keep rolling 6s. Team attacks are meant to be used sparingly, for dramatic effect. Unless the
    /// Heroes are clever about it or the GM rules otherwise, no character can be subject to more than one
    /// team attack per battle."
    /// </summary>
    public static class TeamAttack
    {
        public const string WhatItIs = "several characters coordinating their attacks against a single opponent";
        public const string ParticipantsActAt = "the end of the page";
        public const bool AllParticipantsMustTargetTheSameEnemy = true;
        public const int AttackBonusDice = 2;
        public const int CostResolveToMakeSixesExplode = 1;
        public const bool ExplosionRecursesWhileSixesKeepComing = true;
        public const int LimitPerTargetPerBattle = 1;
        public const string TheLimitMayBeLiftedBy =
            "the Heroes being clever about it, or the GM ruling otherwise";
        public const bool UseSparingly = true;
    }

    // ── The Example of Combat, p.81 ──────────────────────────────────────────

    /// <summary>
    /// <b>The worked fight, transcribed as figures so the data can be made to resolve it.</b>
    /// It is not an entry in <c>combat.json</c> — a worked example is not a mechanic — and this is the
    /// stronger use for it: every step below is reached through the JSON's own tables rather than
    /// through a number typed into the test.
    ///
    /// <para>"The mecha has 13 Edge, Citizen Soldier has 12 Edge, and Gatecrasher has 9 Edge. Being
    /// Minions, the robots don't have Edge scores… the giant mecha acts first, Citizen Soldier acts
    /// second, Gatecrasher acts third, and the robotic Minions act last."</para>
    /// </summary>
    public static class ExampleOfCombat
    {
        public const int MechaEdge = 13;
        public const int CitizenSoldierEdge = 12;
        public const int GatecrasherEdge = 9;

        public static readonly string[] TurnOrder =
            ["the mecha", "Citizen Soldier", "Gatecrasher", "the robotic Minions"];

        /// <summary>The Example's own name for the mob. The fixture places it using
        /// <c>tie_break.minions_act</c> rather than appending it to the three named characters, so
        /// a file that stopped putting Minions last would move the order the fixture builds.</summary>
        public const string MinionsLabel = "the robotic Minions";

        /// <summary>
        /// "It rolls its 13d Might and gets 8 successes. Gate uses his 12d Armor to defend himself and
        /// rolls 6 successes. With a total of 2 net successes, a giant mechanical foot stomps Gate into
        /// the ground, inflicting 2 points of damage."
        /// </summary>
        public const int StompAttackSuccesses = 8;
        public const int StompDefenseSuccesses = 6;
        public const int StompDamage = 2;

        /// <summary>
        /// "he leaps at the robot Minions using his 12d Might, and they defend themselves with their 6d
        /// Threat. The Soldier rolls 8 successes on his attack, and the robots roll 3 successes on their
        /// defense… With 5 net successes, our Hero could have defeated up to five of these robotic
        /// rogues, so the player describes how Citizen Soldier turns these four into scrap metal."
        /// </summary>
        public const int MinionAttackSuccesses = 8;
        public const int MinionThreat = 6;
        public const int MinionDefenseSuccesses = 3;
        public const int MinionsCouldHaveBeenDefeated = 5;
        public const int MinionsPresent = 4;

        /// <summary>
        /// "Using his own 12d Might, he rolls 9 successes. However, the mecha also gets 9 successes when
        /// it rolls its 15d Armor for defense. Gatecrasher's attack has no effect—that thing is tough!"
        /// </summary>
        public const int ChargeAttackSuccesses = 9;
        public const int ChargeDefenseSuccesses = 9;

        /// <summary>
        /// "Using its 13d Might, the mecha rolls 7 successes on its attack. The Soldier uses his 12d Might
        /// to resist the attack, but only manages to score 4 successes… With 3 net successes, the mecha
        /// places our Hero in a full hold."
        /// </summary>
        public const int HoldAttackSuccesses = 7;
        public const int HoldDefenseSuccesses = 4;
        public const string HoldResult = "full hold";

        /// <summary>
        /// "He makes a Might roll and gets 6 successes, but the mecha makes its own Might roll and also
        /// gets 6 successes. With no net successes, the Soldier remains trapped…"
        /// </summary>
        public const int EscapeAttemptSuccesses = 6;
        public const int EscapeOpposingSuccesses = 6;
        public const string EscapeResult = "no effect";

        /// <summary>
        /// "Gatecrasher fires his eyebeams (a 15d Blast) at the mecha and rolls 8 successes. The mecha uses
        /// its 15d Armor to make its defense roll and gets 7 successes. One net success may not be much, but
        /// it's enough for narrative control… But with only one net success, the GM gets an embellishment…"
        /// </summary>
        public const int EyebeamAttackSuccesses = 8;
        public const int EyebeamDefenseSuccesses = 7;
    }
}
