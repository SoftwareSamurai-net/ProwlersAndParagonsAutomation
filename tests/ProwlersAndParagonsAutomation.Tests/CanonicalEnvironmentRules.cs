namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 7's environment rules, pp.105-109, transcribed from the rulebook. This is what
/// <c>data/rules/play/environment.json</c> is checked against.
///
/// <para><b>Same standing as <see cref="CanonicalCombatRules"/>: this file is the rulebook.</b> Do
/// not "fix" a failing test by editing a value here.</para>
///
/// <para><b>Ten of the chapter's tables are deliberately not in here, and that is the strongest
/// thing about them.</b> The corpus already carries the printed columns, so
/// <c>PlayRulesDataTests.TheChapterSevenTablesAreReadOutOfTheCorpusColumns</c> derives every row
/// from <c>data/rulebook/ch07-environment.json</c> and compares. What this file carries instead is
/// the <see cref="TableSizes"/> - because a derivation cannot notice a table that has lost half of
/// itself when the expectation lost the same half - and the figures the chapter's own prose prints
/// a second time, which are what anchor each alignment.</para>
///
/// <para><b>One row group is here rather than derived, and the reason is a property of the
/// extraction.</b> The Smashing table's material column prints several names to a row, separated by
/// the same comma and space that separates one row from the next only by a space - "…Ice, Rope
/// Plastic, Rubber, Wood Brick…" - so where one row ends is not recoverable from the block. The
/// grouping is transcribed in <see cref="SmashingMaterials"/> and the test proves it <em>tiles</em>
/// the corpus block exactly, which is the strongest claim the extraction supports.</para>
/// </summary>
public static class CanonicalEnvironmentRules
{
    /// <summary>The chapter's printed range. Only pp.105-109 are transcribed; the header says so.</summary>
    public const int FirstPage = 105;

    /// <inheritdoc cref="FirstPage"/>
    public const int LastPage = 110;

    // ── Disasters, p.105 ─────────────────────────────────────────────────────

    /// <summary>
    /// "Battling a disaster is broken up into a number of goals. Minor disasters have 3 goals, while
    /// major ones have 9 goals. A goal is anything you need to accomplish to improve the situation…
    /// Most goals can be accomplished with a good idea and a successful challenge roll. Occasionally,
    /// however, a goal might be important enough to demand its own scene. This is up to the GM. The
    /// GM usually comes up with 2 of the goals in a minor disaster or 6 of the goals in a major
    /// disaster, leaving you and your fellow Heroes to brainstorm the others. Once all goals have
    /// been accomplished or failed, check the Disaster Results table to determine who narrates the
    /// resolution and aftermath of the disaster."
    /// </summary>
    public static class Disasters
    {
        public const int MinorGoals = 3;
        public const int MajorGoals = 9;
        public const int GmSuppliesInAMinorDisaster = 2;
        public const int GmSuppliesInAMajorDisaster = 6;
        public const string RemainingGoalsComeFrom = "the players";
        public const string AGoalIs = "anything that has to be achieved to make the situation better";
        public const string MostGoalsNeed = "a good idea and a successful challenge roll";
        public const bool AGoalMayBeWorthItsOwnScene = true;
        public const string WhoDecidesAGoalNeedsItsOwnScene = "gm";
        public const string ResolutionReadOff = "the Disaster Results table";
        public const string ReadTheTableWhen = "every goal has been achieved or failed";
    }

    // ── Energy, p.105 ────────────────────────────────────────────────────────

    /// <summary>
    /// "Although there are many different kinds of energy in the real world, P&amp;P lumps them into
    /// a few simple categories for game purposes - otherwise Powers that control or resist energy
    /// become too diluted… Because gravity and magnetism are forces that affect physical objects
    /// directly, they should be represented with Powers like Elemental Control and Telekinesis. If
    /// you must classify these forces as energy, treat them both as force energy."
    /// </summary>
    public static class Energy
    {
        public const string KindsAreLumpedInto = "a few simple categories";
        public const string Why = "so that Powers which control or resist energy are not diluted";
        public const bool GravityAndMagnetismAreEnergy = false;
        public const string GravityAndMagnetismAre = "forces that act on physical objects directly";

        public static readonly string[] GravityAndMagnetismRepresentedWith =
            ["Elemental Control", "Telekinesis"];

        public const string GravityAndMagnetismIfClassifiedAsEnergy = "force";
    }

    /// <summary>
    /// The Energy Types table's two rows that carry a mechanic. "Force/Kinetic — Physical force:
    /// attacks using this energy are considered physical attacks." And the footnote on
    /// Sound/Thunder/Vibration: "If you care, this kind of energy should be useless in a vacuum but
    /// at +3d when used underwater."
    ///
    /// <para>The nine type names are not here: they are read out of the corpus block, which prints
    /// them.</para>
    /// </summary>
    public static class EnergyTypes
    {
        public const string ForceKineticAttacksAre = "physical attacks";
        public const bool SonicFootnoteIsOfferedAsOptional = true;
        public const string SonicInAVacuum = "useless";
        public const int SonicUnderwaterBonusDice = 3;
    }

    // ── Falling, p.105 ───────────────────────────────────────────────────────

    /// <summary>
    /// "Falls are treated like attacks that can only be resisted with passive defenses (unless you
    /// can come up with a creative way to use an active defense instead). The attack rank of a fall
    /// depends on the distance fallen, as shown on the Falling table. Increase the attack rank by 3d
    /// if you land on something like spikes or sharp rocks; lower it by 3d if you land on something
    /// like cushions, garbage, or water. Additionally, whenever you fall onto soft surfaces like
    /// these, you can use Agility as an active defense against falling damage."
    /// </summary>
    public static class Falling
    {
        public const bool IsAnAttack = true;
        public const string ResistedOnlyWith = "passive defenses";
        public const bool GmMayAllowACreativeActiveDefense = true;
        public const string AttackRankDependsOn = "the distance fallen";
        public const string ReadOff = "the Falling table";
        public static readonly string[] HardLandingExamples = ["spikes", "sharp rocks"];
        public const int HardLandingBonusDice = 3;
        public static readonly string[] SoftLandingExamples = ["cushions", "garbage", "water"];
        public const int SoftLandingPenaltyDice = -3;
        public const string SoftLandingAllowsActiveDefense = "Agility";
    }

    // ── Hostile Environments, p.106 ──────────────────────────────────────────

    /// <summary>
    /// "Environmental hazards are either minor or major. A minor hazard might be a smoke-filled
    /// room, low-level radiation, extreme temperatures, and so on. You can withstand exposure to
    /// minor hazards for a number of minutes equal to your Toughness; after that, you suffer 1 point
    /// of damage per minute of exposure. Major hazards include things like crushing undersea
    /// pressure, high-level radiation, and the vacuum of outer space. Exposure to these conditions is
    /// measured in pages rather than minutes, so even if you aren't in combat you should track time
    /// in pages when dealing with a major hazard. You can withstand exposure to major hazards for a
    /// number of pages equal to your Toughness; after that, you suffer 1 point of damage per page of
    /// exposure. Hazards can't inflict more than 1 point of damage per page, even if you're exposed
    /// to multiple hazards at the same time. Certain Powers (notably Adaptation and Immunity) can
    /// protect you against environmental hazards."
    /// </summary>
    public static class HostileEnvironments
    {
        public static readonly string[] HazardGrades = ["minor", "major"];

        public static readonly string[] MinorExamples =
            ["a smoke-filled room", "low-level radiation", "extreme temperatures"];

        public static readonly string[] MajorExamples =
            ["crushing undersea pressure", "high-level radiation", "the vacuum of outer space"];

        public const string MinorWithstoodForMinutesEqualTo = "Toughness";
        public const int MinorDamagePerMinuteAfterThat = 1;
        public const string MajorWithstoodForPagesEqualTo = "Toughness";
        public const int MajorDamagePerPageAfterThat = 1;
        public const bool TrackTimeInPagesForAMajorHazardEvenOutOfCombat = true;
        public const int MaximumHazardDamagePerPage = 1;
        public const bool MaximumAppliesAcrossSimultaneousHazards = true;
        public static readonly string[] PowersThatProtect = ["Adaptation", "Immunity"];
    }

    // ── Suffocation, p.106 ───────────────────────────────────────────────────

    /// <summary>
    /// "You can hold your breath for a number of minutes equal to your Toughness. After that, you
    /// start suffering 1 point of damage per page until you can breathe again. Once you can breathe
    /// normally, all suffocation damage immediately goes away. As usual, you'll be defeated rather
    /// than killed if you suffer too much damage unless you're playing a game in which Heroes can
    /// die. Assuming you aren't, you and your GM are going to have to figure out how you managed to
    /// survive."
    /// </summary>
    public static class Suffocation
    {
        public const string HoldBreathMinutesEqualTo = "Toughness";
        public const int DamagePerPageAfterThat = 1;
        public const string AllSuffocationDamageRemovedWhen = "you can breathe normally again";
        public const bool RemovalIsImmediate = true;
        public const string DefeatedRatherThanKilledUnless = "the game is one in which Heroes can die";
        public const string SurvivingIsExplainedBy = "the player and the GM together";
    }

    // ── Swimming, p.106 ──────────────────────────────────────────────────────

    /// <summary>
    /// "Characters travel only half as fast as normal and use half their Agility when making
    /// movement-related challenge rolls while swimming. They also suffer a −3d penalty to Perception
    /// rolls while underwater. A scuba mask lowers this penalty to −1d for visual Perception rolls.
    /// When engaging in underwater combat, characters use half their Edge and suffer a −3d penalty to
    /// their physical attack and active defense rolls. As you might expect, characters with Swimming
    /// ignore these penalties. Deep water is dark, very cold, and can expose characters to pressure
    /// extremes and the risks associated with sudden changes in pressure, but those real world
    /// complexities are left for GMs to handle or ignore as they see fit."
    /// </summary>
    public static class Swimming
    {
        public const string TravelSpeed = "half";
        public const string AgilityUsedForMovementChallengeRolls = "half";
        public const int PerceptionPenaltyDiceUnderwater = -3;
        public const int ScubaMaskReducesVisualPerceptionPenaltyTo = -1;
        public const string UnderwaterCombatEdge = "half";
        public const int UnderwaterPhysicalAttackPenaltyDice = -3;
        public const int UnderwaterActiveDefensePenaltyDice = -3;
        public const string IgnoredBy = "Swimming";
        public const string DeepWaterComplexitiesLeftTo = "gm";
    }

    // ── Leaping, p.106 ───────────────────────────────────────────────────────

    /// <summary>
    /// "Distances are intentionally abstract in P&amp;P, but it can sometimes be helpful to have a
    /// more concrete idea of how far you can jump. All characters effectively have Leaping at half
    /// their Might for purposes of determining how far they can jump. However, you need to have spent
    /// Hero Points on Leaping if you want to use the Power as a means of long-distance travel."
    /// </summary>
    public static class Leaping
    {
        public const string EveryCharacterEffectivelyHas = "Leaping";
        public const string AtRank = "half your Might";
        public const string ForThePurposeOf = "working out how far you can jump";
        public const string ThePowerMustBeBoughtToUseItFor = "long-distance travel";
        public const bool DistancesAreDeliberatelyAbstract = true;
    }

    // ── Lifting, p.106 ───────────────────────────────────────────────────────

    /// <summary>
    /// "The maximum amount of weight you can lift is normally a static value that depends on your
    /// Might, but this assumes optimal conditions. Lifting a heavy object can be tricky when rushed
    /// or too distracted to get a good grip on the thing. That being the case, whenever you want to
    /// lift a heavy object in combat or during other fast-paced actions scenes, the GM may ask for a
    /// Might roll to see if you can manage it. The threshold for this roll depends on the object's
    /// weight, as shown on the Lifting table."
    /// </summary>
    public static class Lifting
    {
        public const string MaximumWeightNormally = "a static value that depends on your Might";
        public const string StaticValueAssumes = "optimal conditions";

        public const string RollAskedForWhen =
            "lifting a heavy object in combat or another fast-paced action scene";

        public const string RollIsAskedForBy = "gm";
        public const string RollTrait = "Might";
        public const string RollAgainst = "the threshold for the object's weight";
        public const string ThresholdDependsOn = "the object's weight";
        public const string ReadOff = "the Lifting table";
    }

    // ── Scorching, p.107 ─────────────────────────────────────────────────────

    /// <summary>
    /// "Heat and electricity are common sources of harm. Like falling, whenever you are exposed to
    /// either energy, you suffer an attack that can only be resisted with passive defenses (unless
    /// you can come up with a creative way of using one of your active defenses instead). Use the
    /// Scorching table as a guide when determining the rank of such attacks."
    /// </summary>
    public static class Scorching
    {
        public static readonly string[] Sources = ["heat", "electricity"];
        public const bool IsAnAttack = true;
        public const string ResistedOnlyWith = "passive defenses";
        public const bool GmMayAllowACreativeActiveDefense = true;
        public const string WorksLike = "falling";
        public const bool TableIsAGuide = true;
    }

    // ── Smashing, p.107 ──────────────────────────────────────────────────────

    /// <summary>
    /// "While vehicles and complex machines have a Body characteristic, simple objects like doors and
    /// walls have a Structure rank that determines their durability. The Structure of common
    /// materials is on the Smashing table. GMs can raise or lower an object's Structure by 1d to 4d
    /// depending on its condition, thickness, and whatever other factors they deem relevant…
    /// Whenever you want to bend, break, or smash through an object, make a challenge roll using
    /// Might or one of your Powers against the object's Structure. You can bend or make a small hole
    /// in an object with 1 to 2 net successes, but big holes require 3 or more net successes. You
    /// must do this all at once; you can't combine net successes over multiple attempts. In fact, if
    /// an object is especially thick - like castle wall thick - the GM might make you do this a few
    /// times in order to smash through it completely."
    /// </summary>
    public static class Smashing
    {
        public const string VehiclesAndComplexMachinesHave = "Body";
        public const string SimpleObjectsHave = "Structure";
        public const string StructureDetermines = "durability";
        public const int GmMayAdjustStructureByMin = 1;
        public const int GmMayAdjustStructureByMax = 4;
        public static readonly string[] AdjustmentFactors = ["condition", "thickness"];
        public const bool AdjustmentFactorsAreOpenEnded = true;
        public static readonly string[] RollTraits = ["Might", "a Power"];
        public const string RollAgainst = "the object's Structure";
        public const int BendOrSmallHoleMinNetSuccesses = 1;
        public const int BendOrSmallHoleMaxNetSuccesses = 2;
        public const int BigHoleMinNetSuccesses = 3;
        public const bool NetSuccessesMayBeCombinedOverAttempts = false;
        public const bool AnEspeciallyThickObjectMayNeedSeveralAttempts = true;
    }

    /// <summary>
    /// The Smashing table's material column, row by row, as printed. <b>This is the one table group
    /// in the chapter that the corpus cannot yield on its own</b>: several materials share a row and
    /// are separated by ", ", while one row is separated from the next by a bare space, so
    /// "…Ice, Rope Plastic, Rubber, Wood Brick…" has no recoverable row boundary. The test requires
    /// this grouping to tile the corpus block exactly - joined with ", " inside a row and " " between
    /// rows, it has to reproduce the block character for character - which is the strongest claim
    /// the extraction supports. The ranks beside them are read out of the corpus.
    ///
    /// <para>The asterisk on the last row is the setting's note about Ozymandium and carries no
    /// mechanic; the name is transcribed without it.</para>
    /// </summary>
    public static readonly string[][] SmashingMaterials =
    [
        ["Cloth", "Drywall", "Glass", "Ice", "Rope"],
        ["Plastic", "Rubber", "Wood"],
        ["Brick", "Bulletproof Glass", "Hardwood"],
        ["Asphalt", "Concrete", "Machinery"],
        ["Iron", "Stone"],
        ["Steel"],
        ["Diamond", "Titanium"],
        ["Advanced Alloy", "Magical Metal"],
        ["Ozymandium Alloy"]
    ];

    // ── Damaging Cover, p.108 ────────────────────────────────────────────────

    /// <summary>
    /// "Attacks can penetrate objects used as cover if their attack rank exceeds the object's
    /// Structure. Any time you attack a target by smashing or shooting through an object, the target
    /// can use the object's Structure as a passive defense. When combined with the above rules for
    /// smashing objects, what this means is that you have two options when attacking a target hiding
    /// behind cover. If your attack rank exceeds the cover's Structure, you can shoot right through
    /// it. If not, you can try to smash a hole in the object your target is hiding behind, after
    /// which they'll presumably be more exposed."
    ///
    /// <para><b>Chapter 4 p.75 prints both clauses too</b>, on <c>modifier_cover</c>, which is why
    /// this entry carries a <c>corroborated_by</c> and why
    /// <c>PlayRulesDataTests.TheTwoChaptersThatPrintDamagingCoverAgreeAboutIt</c> holds the two
    /// files to one answer.</para>
    /// </summary>
    public static class DamagingCover
    {
        public const string AppliesWhen = "attacking a target by smashing or shooting through an object";
        public const string AttackPenetratesWhen = "the attack rank exceeds the object's Structure";
        public const string TargetMayUseTheObjectsStructureAs = "a passive defense";

        public static readonly string[] OptionsGiven =
        [
            "shoot straight through the cover, where the attack rank exceeds its Structure",
            "smash a hole in the cover first, leaving the target more exposed"
        ];
    }

    // ── Scenery as Weapons, p.108 ────────────────────────────────────────────

    /// <summary>
    /// "If you use a heavy object or a vehicle as a club, you get a +1d bonus to your close combat
    /// attack rolls. If you use a heavy object or a vehicle as a thrown projectile, this allows you
    /// to use Might +1d to perform a ranged attack… Whenever you use a mundane object as an
    /// improvised weapon, your attack rank caps out at the object's Body or Structure plus 6d. For
    /// example, motorcycles have 5d Body, so you can't roll more than 11d when using one as an
    /// improvised weapon. Similarly, most wooden telephone poles have 7d Structure (6d plus 1d for
    /// thickness), so you can't roll more than 13d when using one as an improvised weapon… For every
    /// page that an everyday object is used as a weapon by a super strong character, its effective
    /// Body or Structure is reduced by 2d for these purposes. Again, this only applies when super
    /// strong characters use scenery and vehicles as weapons. A character with normal human strength
    /// can wield an iron crowbar until the cows come home without affecting the thing in any way.
    /// Edge cases are up to the GM's discretion."
    /// </summary>
    public static class SceneryAsWeapons
    {
        public const string AppliesTo = "a heavy object or a vehicle";
        public const int CloseCombatBonusDice = 1;
        public const string ThrownAttackTrait = "Might";
        public const int ThrownAttackBonusDice = 1;
        public const string ThrownAttackIs = "a ranged attack";
        public const string AttackRankCapsAt = "the object's Body or Structure plus the cap bonus";
        public const int CapBonusDice = 6;
        public const string WorkedExampleObject = "a motorcycle";
        public const int WorkedExampleObjectBody = 5;
        public const int WorkedExampleMaximumAttackRank = 11;
        public const string SecondWorkedExampleObject = "a wooden telephone pole";
        public const int SecondWorkedExampleStructureFromTheTable = 6;
        public const int SecondWorkedExampleThicknessAdjustment = 1;
        public const int SecondWorkedExampleObjectStructure = 7;
        public const int SecondWorkedExampleMaximumAttackRank = 13;
        public const int DegradationDicePerPage = 2;

        public const string DegradationAppliesTo =
            "an everyday object used as a weapon by a super strong character";

        public const bool DegradationIsOnlyForThesePurposes = true;
        public const bool OrdinaryHumanStrengthDegradesNothing = true;
        public const string EdgeCasesLeftTo = "gm";

        /// <summary>The two scenery rows the prose names, which anchor the table's alignment.</summary>
        public const string AnchorRowMotorcycle = "Motorcycle";

        /// <inheritdoc cref="AnchorRowMotorcycle"/>
        public const string AnchorRowTelephonePole = "Telephone Pole";
    }

    // ── Massive Objects, p.108 ───────────────────────────────────────────────

    /// <summary>
    /// "If you can manage to heft a truly massive object and somehow keep it from falling apart under
    /// its own weight, it works as described above, only using the object's weight rank instead of
    /// its Body or Structure. Unlike ordinary scenery, massive objects always break apart after the
    /// first shot."
    /// </summary>
    public static class MassiveObjects
    {
        public const string WorksLike = "scenery as weapons";
        public const string UsesInsteadOfBodyOrStructure = "the object's weight rank";

        public const string Requires =
            "hefting it and somehow keeping it from collapsing under its own weight";

        public const string AlwaysBreaksApartAfter = "the first shot";
    }

    // ── Toxins, pp.108-109 ───────────────────────────────────────────────────

    /// <summary>
    /// "Biological and chemical agents like diseases, drugs, and poisons are toxins. Toxins work like
    /// Powers with an Innate Source. Once a character is exposed to a toxin, they can only use their
    /// passive defenses - normally Toughness or the Resistance Power - to resist its effects. The
    /// following Pros and Cons apply only to mundane diseases, drugs, and poisons (unless the GM
    /// rules otherwise)."
    /// </summary>
    public static class Toxins
    {
        public static readonly string[] ToxinsAre = ["diseases", "drugs", "poisons"];
        public const string ToxinsAreDescribedAs = "biological and chemical agents";
        public const string WorkLike = "Powers with an Innate Source";
        public const string ResistedOnlyWith = "passive defenses";
        public static readonly string[] PassiveDefensesNamed = ["Toughness", "Resistance"];
        public const bool ResistanceIsAPower = true;
        public const string TheProsAndConsApplyOnlyTo = "mundane diseases, drugs and poisons";
        public const string Unless = "the GM rules otherwise";
    }

    /// <summary>
    /// The three options the section prints, and where their mechanics actually live.
    ///
    /// <para><b>None of their figures is here on purpose.</b> Caustic is a −2 Hero Point Con on Stun,
    /// Lethal Disease a +6 Pro on Slay and Non-Lethal Disease a +2 Pro on Stun - all three already
    /// transcribed and priced in <c>data/rules/powers.json</c>, where <c>PowerProConTests</c> holds
    /// them to the page. Repeating a cost here would be the second transcription
    /// <c>play-rules.md</c>'s deferral policy exists to prevent, so the entries carry
    /// <c>transcribed_here: false</c> and this record carries only the pointer.</para>
    /// </summary>
    public sealed record ToxinOption(
        string EntryId, string PrintedName, string OptionKind, string PowerId, string OptionId);

    /// <inheritdoc cref="ToxinOption"/>
    public static readonly ToxinOption[] ToxinOptions =
    [
        new("toxin_con_caustic", "Caustic", "con", "stun", "caustic"),
        new("toxin_pro_lethal_disease", "Lethal Disease", "pro", "slay", "lethal_disease"),
        new("toxin_pro_non_lethal_disease", "Non-Lethal Disease", "pro", "stun", "non_lethal_disease")
    ];

    /// <summary>The store the three options are transcribed in, named by every deferring entry.</summary>
    public const string ToxinOptionDetailStore = "data/rules/powers.json";

    /// <summary>Both toxin tables reference their options rather than transcribing them.</summary>
    public const bool ToxinTableOptionsAreReferencedNotTranscribed = true;

    /// <summary>The one asterisked row of the Diseases table (p.109).</summary>
    public static readonly string[] DiseasesFootnotedRowNames = ["Staph Infection"];

    /// <summary>The one asterisked row of the Drugs and Poisons table (p.109).</summary>
    public static readonly string[] DrugsFootnotedRowNames = ["Ancient Poison"];

    /// <summary>The one asterisked row of the Smashing table (p.107).</summary>
    public static readonly string[] SmashingFootnotedRowMaterials = ["Ozymandium Alloy"];

    // ── Table sizes ──────────────────────────────────────────────────────────

    /// <summary>
    /// How many rows each printed table has. <b>These are counts and not the rows themselves</b>:
    /// the rows are derived from the corpus, and a count typed here is what stops a derivation from
    /// agreeing with a table that has silently lost half of itself.
    /// </summary>
    public static class TableSizes
    {
        public const int DisasterResults = 4;
        public const int EnergyTypes = 9;
        public const int Falling = 6;
        public const int Lifting = 12;
        public const int Scorching = 6;
        public const int Smashing = 9;
        public const int Scenery = 9;
        public const int MassiveObjects = 12;
        public const int Diseases = 9;
        public const int DrugsAndPoisons = 18;
    }
}
