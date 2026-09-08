namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 6's armour and shields, printed p.88, transcribed from the rulebook. This is what the
/// armour half of <c>data/rules/equipment.json</c> is checked against.
///
/// <para><b>Same standing as <see cref="CanonicalEquipmentRules"/>: this file is the rulebook.</b>
/// Do not "fix" a failing test by editing a value here — open p.88.</para>
///
/// <para><b>Eight of the nine armour rows are deliberately not in here.</b> The corpus already
/// carries the printed table, and unlike the three weapons tables it arrives from the extractor in
/// row order rather than split into two blocks, so
/// <c>EquipmentDataTests.TheArmorTableIsDerivedFromTheCorpusColumns</c> derives all nine rows from
/// <c>data/rulebook/ch06-equipment.json</c> and compares them. What is transcribed here is the
/// <see cref="ArmorTable.RowCount"/> and one anchor row, because a derivation cannot notice a table
/// that has lost half of itself when the expectation lost the same half.</para>
/// </summary>
public static class CanonicalArmourRules
{
    // ── Armor, p.88 ──────────────────────────────────────────────────────────

    /// <summary>
    /// "Armor grants you the Armor Power at a rank equal to your Toughness plus the Armor Bonus
    /// shown on the Armor table, depending on the armor worn. If you already have Armor, you can use
    /// it in place of Toughness when determining your effective Armor rank while wearing artificial
    /// armor. Advanced suits of powered armor may include built-in weaponry and equipment like
    /// communications, life support, sensor systems, and servos that enhance your Abilities. Features
    /// like these should be considered Powers."
    /// </summary>
    public static class Armor
    {
        public const string GrantsPowerId = "armor";
        public const string RankIs = "the wearer's Toughness plus the suit's Armor Bonus";
        public const string RankBaseTrait = "Toughness";
        public const bool RankBaseMayBeTheArmorPowerInstead = true;
        public const string RankBaseSubstitutionAppliesWhile = "wearing artificial armor";
        public const bool BuiltInExtrasArePowers = true;

        public static readonly string[] BuiltInExtrasExamples =
        [
            "built-in weaponry", "communications", "life support", "sensor systems",
            "servos that enhance Abilities"
        ];
    }

    /// <summary>
    /// The printed table: "Ancient, Leather +0d — | Ancient, Mail +1d Bulky | Ancient, Plate +2d
    /// Rigid | Modern, Armored Uniform +1d — | Modern, Tactical Gear +2d Bulky | Modern,
    /// Military/Riot Gear +3d Rigid | Advanced, Light +2d — | Advanced, Medium +3d Bulky |
    /// Advanced, Heavy +4d Rigid".
    /// </summary>
    public static class ArmorTable
    {
        public const int RowCount = 9;

        /// <summary>The three eras the table is printed in, in printed order.</summary>
        public static readonly string[] Categories = ["Ancient", "Modern", "Advanced"];

        /// <summary>
        /// One row transcribed in full, as the anchor the derivation is measured against. The
        /// heaviest suit is chosen because it carries the table's largest figure, so a
        /// column that had slipped by one row moves it.
        /// </summary>
        public const string AnchorRowName = "Heavy";

        /// <inheritdoc cref="AnchorRowName"/>
        public const string AnchorRowCategory = "Advanced";

        /// <inheritdoc cref="AnchorRowName"/>
        public const int AnchorRowBonusDice = 4;

        /// <inheritdoc cref="AnchorRowName"/>
        public const string AnchorRowFeature = "Rigid";
    }

    // ── Armor Features, p.88 ─────────────────────────────────────────────────

    /// <summary>
    /// "Characters wearing this armor suffer a −1d penalty on challenge rolls involving acrobatics,
    /// speed, stealth, and swimming unless they have a Might of 4d or greater."
    /// </summary>
    public static class Bulky
    {
        public const int PenaltyDice = -1;
        public static readonly string[] PenalisedRolls = ["acrobatics", "speed", "stealth", "swimming"];
        public const int ExemptIfMightAtLeast = 4;

        /// <summary>p.93's Fitted, the custom feature that buys the penalty off.</summary>
        public const string RemovableByGearFeatureId = "fitted";
    }

    /// <summary>
    /// "Characters wearing this armor suffer a −1d penalty on challenge rolls involving acrobatics,
    /// speed, stealth, and swimming."
    ///
    /// <para><b>The whole of the difference from Bulky is the missing clause.</b> Rigid prints no
    /// escape for a strong wearer, which is why <see cref="ExemptIfMightAtLeast"/> is null rather
    /// than some large number.</para>
    /// </summary>
    public static class Rigid
    {
        public const int PenaltyDice = -1;
        public static readonly string[] PenalisedRolls = ["acrobatics", "speed", "stealth", "swimming"];
        /// <summary>Null, and that is the transcription: Rigid prints no escape clause.</summary>
        public static int? ExemptIfMightAtLeast => null;
        public const string RemovableByGearFeatureId = "fitted";
    }

    // ── Shields, p.88 ────────────────────────────────────────────────────────

    /// <summary>
    /// "Using a mundane shield in your off-hand grants you a +1d Shield Bonus to both active and
    /// passive defense rolls against physical and energy attacks. You can use a shield as an
    /// off-hand weapon if you wish (see Weapons below), but you then lose the defensive benefit
    /// provided by the shield until your next turn to act. Shields provide no benefit when you are
    /// surprised or unable to defend yourself."
    /// </summary>
    public static class Shields
    {
        public const int BonusDice = 1;
        public const string BonusName = "Shield Bonus";
        public const string CarriedIn = "the off-hand";
        public static readonly string[] AppliesToDefenses = ["active", "passive"];
        public static readonly string[] AppliesAgainstAttackKinds = ["physical", "energy"];
        public const bool MayBeUsedAsAnOffHandWeapon = true;
        public const string StrikingWithItCosts = "the defensive bonus, until your next turn to act";
        public static readonly string[] NoBenefitWhen = ["surprised", "unable to defend yourself"];

        /// <summary>A shield is printed twice: here, and as three rows of the weapons tables.</summary>
        public const bool AlsoAWeaponRow = true;

        /// <summary>The rows, by the names the tables print. Checked against the tables themselves.</summary>
        public static readonly string[] WeaponRows = ["Shield", "Shield, Spiked", "Riot Shield"];

        /// <summary>The glossary entry those rows cite, which grants the same single die.</summary>
        public const string WeaponFeatureId = "shield";
    }
}
