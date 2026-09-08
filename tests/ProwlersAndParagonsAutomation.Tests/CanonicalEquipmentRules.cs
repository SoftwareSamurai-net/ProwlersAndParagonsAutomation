namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 6's Gear Limit and its weapons rules, pp.87-90, transcribed from the rulebook. This is
/// what <c>data/rules/play/equipment.json</c> is checked against.
///
/// <para><b>Same standing as <see cref="CanonicalCombatRules"/>: this file is the rulebook.</b> Do
/// not "fix" a failing test by editing a value here.</para>
///
/// <para><b>The three weapons tables are deliberately not in here, and that is the strongest thing
/// about them.</b> Sixty-three rows typed out a second time would be a second transcription to
/// disagree with the first, and the corpus already carries the printed columns — so
/// <c>PlayRulesDataTests.TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns</c> derives every row
/// from <c>data/rulebook/ch06-equipment.json</c> and compares. What anchors that derivation is
/// three rows the book prints a <em>second</em> time in prose: the pistol's two dice and the battle
/// axe's three, both on p.87 below, and the basic sword's two on Ch.4 p.80
/// (<see cref="CanonicalGrittyRules.GearLimit"/>).</para>
///
/// <para><b>Chapter 4 prints the same rule and this file does not repeat its wording.</b> p.80's
/// summary is <see cref="CanonicalGrittyRules.GearLimit"/>; p.87 is the detail it points at. The
/// two agree on the default rank and on the raised options, which
/// <c>PlayRulesDataTests.TheTwoChaptersThatPrintTheGearLimitAgreeAboutIt</c> requires.</para>
/// </summary>
public static class CanonicalEquipmentRules
{
    /// <summary>The chapter's printed range. Only pp.87-90 are transcribed; the header says so.</summary>
    public const int FirstPage = 87;

    /// <inheritdoc cref="FirstPage"/>
    public const int LastPage = 104;

    // ── Gear Limits, p.87 ────────────────────────────────────────────────────

    /// <summary>
    /// "All games have a Gear Limit that indicates the maximum Trait rank you can apply when using
    /// mundane equipment that boosts your Traits for a specific purpose (usually armor and
    /// weapons). The default Gear Limit in most games is 6d. That means the maximum effective rank
    /// you can have when using a piece of mundane gear is 6d plus whatever bonus it provides. For
    /// example, a pistol has a +2d Weapon Bonus; you roll Agility +2d when using a pistol to attack.
    /// In a game with a 6d Gear Limit, your maximum effective rank with a pistol is 8d. Even if your
    /// Agility is 7d or higher, you can't add more than 6d (the game's Gear Limit) to the pistol's
    /// Weapon Bonus. This makes mundane gear - especially armor and weapons - less useful for
    /// characters with superhuman Ability ranks. Trait Caps and Gear Limits are not the same thing…
    /// most Standard power level games have a 12d Trait Cap and a 6d Gear Limit… Despite your 12d
    /// Agility (Trait Cap), you roll only 8d when firing a +2d Weapon Bonus pistol (Gear Limit)."
    /// </summary>
    public static class GearLimit
    {
        public const string WhatItIs =
            "the maximum Trait rank you can apply when using mundane equipment that boosts your "
            + "Traits for a specific purpose";

        public const int DefaultRank = 6;
        public const string Usually = "armor and weapons";
        public const string MaximumEffectiveRankIs = "the Gear Limit plus the item's bonus";
        public const string WorkedExampleWeapon = "a pistol";
        public const int WorkedExampleWeaponBonusDice = 2;
        public const string WorkedExampleTrait = "Agility";
        public const int WorkedExampleTraitRank = 12;
        public const int WorkedExampleMaximumEffectiveRank = 8;
        public const bool TraitCapIsADifferentThing = true;
        public const int StandardPowerLevelTraitCap = 12;
        public const string MakesMundaneGearLessUsefulFor = "characters with superhuman Ability ranks";
    }

    // ── Exception: Close Combat, p.87 ────────────────────────────────────────

    /// <summary>
    /// "The one exception to this rule involves melee weapons. If your unarmed attack or defense
    /// rank exceeds your effective rank while armed with a melee weapon, you can use your unarmed
    /// attack or defense rank instead. For example, assume you are playing a game with a standard 6d
    /// Gear Limit. If you have 12d Might and are wielding a battle axe with a +3d Weapon Bonus, you
    /// can use your 12d Might in place of the axe's 9d maximum effective rank when making attack
    /// rolls. In this case, the benefit of the axe is that it lets you inflict lethal damage rather
    /// than subdual damage."
    /// </summary>
    public static class CloseCombatCarveOut
    {
        public const string AppliesTo = "melee weapons";

        public const string Condition =
            "your unarmed attack or defense rank exceeds your effective rank while armed";

        public const string YouMayUseInstead = "your unarmed attack or defense rank";
        public const bool AtTheWieldersOption = true;
        public const string WorkedExampleWeapon = "a battle axe";
        public const int WorkedExampleWeaponBonusDice = 3;
        public const int WorkedExampleGearLimit = 6;
        public const int WorkedExampleArmedMaximumEffectiveRank = 9;
        public const int WorkedExampleUnarmedRank = 12;
        public const string WhatTheWeaponStillBuys = "lethal damage rather than subdual damage";
    }

    // ── Raising the Limit, p.87 ──────────────────────────────────────────────

    /// <summary>
    /// "There are comics, notably grittier ones and those set in sci-fi or fantasy worlds, where
    /// obviously superhuman characters regularly use mundane equipment… As mentioned in Chapter 4,
    /// GMs can raise the Gear Limit to 9d, 12d, or more… GMs can even disregard Gear Limits
    /// completely if they want everyone to use gear. Note that mundane gear will overshadow Powers
    /// if the game's Trait Cap doesn't exceed its Gear Limit by at least 3 points… The first way to
    /// balance Powers and gear under these circumstances is to give characters a +3d bonus when
    /// using Powers to inflict or resist physical damage (this doesn't apply to mental/psychic
    /// damage…). A second option is to let Heroes purchase Powers up to 3 ranks above the game's
    /// Trait Cap… And a third approach would be to create special equipment that provides
    /// gear-equivalent bonuses for Powers."
    /// </summary>
    public static class RaisedLimit
    {
        public static readonly int[] RaisedOptions = [9, 12];
        public const bool RaisedOptionsAreOpenEnded = true;
        public const bool MayBeDisregardedEntirely = true;
        public const string Suits = "grittier settings, and science fiction or fantasy worlds";
        public const int PowersAreOvershadowedUnlessTheTraitCapExceedsTheGearLimitBy = 3;

        public static readonly string[] BalanceOptionsGiven =
        [
            "give a bonus when a Power inflicts or resists physical damage",
            "let Powers be bought above the Trait Cap, usable only against physical damage",
            "invent equipment that grants the same sort of bonus to a Power"
        ];

        public const int BalanceOptionBonusDice = 3;
        public const string BalanceOptionsExclude = "mental or psychic damage";
    }

    // ── Weapons, p.88 ────────────────────────────────────────────────────────

    /// <summary>
    /// "All weapons have a Weapon Bonus. When wielding a melee weapon, you add its Weapon Bonus to
    /// your Might or Martial Arts rank when making close combat attacks, and you add its Weapon
    /// Bonus to your Agility or Martial Arts rank when defending yourself against close combat
    /// attacks. When wielding a ranged weapon, you add its Weapon Bonus to your Agility when
    /// attacking in ranged combat. Most weapons inflict lethal damage, but an "(s)" after the Weapon
    /// Bonus means the weapon inflicts subdual damage. Ancient and modern weapons inflict physical
    /// damage. Most advanced weapons inflict energy damage, but "Stun" and "Vibro" weapons inflict
    /// physical damage. Ranged weapons can be used at up to Distant Range unless they have a feature
    /// like Line of Sight or Thrown."
    /// </summary>
    public static class WeaponBonus
    {
        public const bool EveryWeaponHasOne = true;
        public static readonly string[] MeleeAttackTraits = ["Might", "Martial Arts"];
        public static readonly string[] MeleeDefenseTraits = ["Agility", "Martial Arts"];
        public static readonly string[] RangedAttackTraits = ["Agility"];
        public const string AddedTo = "the wielder's rank in that Trait";
        public const string SubdualMarker = "(s)";
        public const string DefaultDamage = "lethal";
        public const string AncientAndModernDamage = "physical";
        public const string AdvancedDamage = "energy";
        public static readonly string[] AdvancedPhysicalExceptions = ["Stun", "Vibro"];
        public const string RangedWeaponsReach = "Distant Range";
        public static readonly string[] RangedReachExceptions = ["Line of Sight", "Thrown"];
    }

    // ── The three weapons tables, pp.89-90 ───────────────────────────────────

    /// <summary>
    /// How many rows each printed table has. <b>These are counts and not the rows themselves</b>:
    /// the rows are derived from the corpus, and a count typed here is what stops a derivation from
    /// agreeing with a table that has silently lost half of itself.
    /// </summary>
    public static class WeaponTableSizes
    {
        public const int Ancient = 27;
        public const int Modern = 21;
        public const int Advanced = 15;
    }

    /// <summary>
    /// The three rows the chapter prints a second time in prose, which is what anchors the pairing
    /// of the printed columns. Each is read out of the corpus by the test rather than compared with
    /// the number beside it here; the name is what the test looks the row up by.
    /// </summary>
    public static class AnchorRows
    {
        public const string PistolName = "Pistol";
        public const int PistolBonusDice = 2;
        public const string BattleAxeName = "Battle Axe";
        public const int BattleAxeBonusDice = 3;

        /// <summary>Ch.4 p.80's own worked example, and the one anchor from outside this chapter.</summary>
        public const string SwordName = "Sword";
    }
}
