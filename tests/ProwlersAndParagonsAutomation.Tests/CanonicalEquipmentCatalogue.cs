namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 6's Weapon Features glossary (pp.88, 90), its equipment list (p.91), Custom Gear (p.92)
/// and the Pros and Cons rule for gear (p.93), transcribed from the rulebook. This is what the rest
/// of <c>data/rules/gear.json</c> is checked against.
///
/// <para><b>Same standing as <see cref="CanonicalEquipmentRules"/>: this file is the rulebook.</b>
/// Do not "fix" a failing test by editing a value here.</para>
///
/// <para><b>The thirty-six items of p.91's list are deliberately not transcribed here</b>, for the
/// reason the sixty-three weapons rows are not: the corpus already carries the printed list, and a
/// second transcription is a second thing to disagree with the first.
/// <c>EquipmentDataTests.TheEquipmentListIsDerivedFromTheCorpusAndPricesNothing</c> derives every
/// item name from <c>data/rulebook/ch06-equipment.json</c>. What is here is the count, and the rule
/// the list is an example of — which is the part a character sheet actually needs.</para>
/// </summary>
public static class CanonicalEquipmentCatalogue
{
    // ── Weapon Features, pp.88 and 90 ────────────────────────────────────────

    /// <summary>
    /// One glossary entry: the name it is printed under, the names the weapons tables cite it by,
    /// and the Pro, Con or Power it defers to where it defers to one.
    /// </summary>
    public sealed record Feature(string Id, string Name, string[] Covers, string? Kind, string? Reference);

    /// <summary>
    /// The eighteen printed entries, in printed order — three on p.88 beside the first table and
    /// fifteen on p.90 after the last one.
    ///
    /// <para><b>Eighteen entries define nineteen cited names</b>, because "Area/Burst: These
    /// features work like the Pros of the same name" is one entry covering two.</para>
    ///
    /// <para>The six that carry a <c>Kind</c> of pro or con are the ones whose whole printed text is
    /// "This feature works like the Pro/Con of the same name"; the four that carry a power are the
    /// ones that deliver one at the attack rank.</para>
    /// </summary>
    public static readonly Feature[] WeaponFeatures =
    [
        // p.88 — "Area/Burst: These features work like the Pros of the same name."
        new("area_burst", "Area/Burst", ["Area", "Burst"], "pro", "area_burst"),

        // p.88 — "Armor Piercing: This feature works like the Pro of the same name."
        new("armor_piercing", "Armor Piercing", ["Armor Piercing"], "pro", "armor_piercing"),

        // p.88 — "Binding: The weapon is used to capture targets rather than hurt them. Although
        // the weapon doesn't inflict damage, it can still be used to perform grabs, holds, and
        // combat stunts."
        new("binding", "Binding", ["Binding"], null, null),

        // p.90 — "Braced: The weapon is too heavy to use effectively unless braced or mounted on
        // something. If you have at least 6d Might, however, you're perfectly capable of using this
        // weapon in two hands."
        new("braced", "Braced", ["Braced"], null, null),

        // p.90 — "Dazzle: The weapon works like a version of the Dazzle Power with a Power rank
        // equal to your attack rank (your Trait plus the item's Weapon Bonus)."
        new("dazzle", "Dazzle", ["Dazzle"], "power", "dazzle"),

        // p.90 — "Ensnare: The weapon delivers an attack that works like the Ensnare Power with a
        // Power rank equal to your attack rank (your Trait plus the item's Weapon Bonus)."
        new("ensnare", "Ensnare", ["Ensnare"], "power", "ensnare"),

        // p.90 — "Flexible: The weapon is flexible, like a chain or whip, and can be used to
        // perform grabs, holds, and combat stunts as well as to inflict damage."
        new("flexible", "Flexible", ["Flexible"], null, null),

        // p.90 — "Irritant: The weapon works like a version of the Irritant Power with a Power rank
        // of 3d plus your net successes on an Easy (0) attack roll (using your Agility plus the
        // item's Weapon Bonus)."
        new("irritant", "Irritant", ["Irritant"], "power", "irritant"),

        // p.90 — "Launcher: The weapon fires grenades. Select the grenades separately. Although
        // they have identical statistics for game purposes, these grenades aren't the same as those
        // tossed by hand."
        new("launcher", "Launcher", ["Launcher"], null, null),

        // p.90 — "Line of Sight: This feature works like the Pro of the same name."
        new("line_of_sight", "Line of Sight", ["Line of Sight"], "pro", "line_of_sight"),

        // p.90 — "Penetrating: This feature works like the Pro of the same name."
        new("penetrating", "Penetrating", ["Penetrating"], "pro", "penetrating"),

        // p.90 — "Readied: This feature works like the Con of the same name."
        new("readied", "Readied", ["Readied"], "con", "readied"),

        // p.90 — "Shield: The weapon serves as a shield and provides a +1d Shield Bonus."
        new("shield", "Shield", ["Shield"], null, null),

        // p.90 — "Shock: The weapon inflicts damage and also delivers a carrier attack that works
        // like the Stun Power with a Power rank equal to your attack rank…"
        new("shock", "Shock", ["Shock"], "power", "stun"),

        // p.90 — "Stun: The weapon does not inflict damage, but it delivers an attack that works
        // like the Stun Power with a Power rank equal to your attack rank…"
        new("stun", "Stun", ["Stun"], "power", "stun"),

        // p.90 — "Thrown: The weapon can be thrown at targets within Close Range (or farther,
        // depending on how strong you are, as discussed in Chapter 4)…"
        new("thrown", "Thrown", ["Thrown"], null, null),

        // p.90 — "Two-Handed: This feature works like the Con of the same name."
        new("two_handed", "Two-Handed", ["Two-Handed"], "con", "two_handed"),

        // p.90 — "Versatile: The weapon can be used in one or two hands. Increase the item's Weapon
        // Bonus by 1d when wielding the weapon in two hands…"
        new("versatile", "Versatile", ["Versatile"], null, null)
    ];

    public static Feature FeatureById(string id) =>
        WeaponFeatures.Single(f => string.Equals(f.Id, id, StringComparison.Ordinal));

    /// <summary>The glossary as a block: what it says it is, rather than what is in it.</summary>
    public static class Glossary
    {
        public const string Describes = "the features printed in the weapons tables' fourth column";
        public const bool FurtherFeaturesComeFromCustomizing = true;
        public const int CustomizationIsOnPage = 93;
        public const int EntryCount = 18;

        /// <summary>Nineteen, because Area/Burst is one entry covering two cited names.</summary>
        public const int NameCount = 19;

        public static readonly int[] PrintedAcrossPages = [88, 90];
    }

    /// <summary>
    /// "For ease of reference, you can assume that all Burst weapons have a 30-foot diameter area
    /// of effect, except for Stun Grenades and Entangler Grenades, which have a 15-foot diameter
    /// area of effect."
    /// </summary>
    public static class Burst
    {
        public const int DiameterFeet = 30;
        public const int SmallDiameterFeet = 15;

        /// <summary>The two exceptions, under the names the weapons tables print for them.</summary>
        public static readonly string[] SmallDiameterWeapons = ["Grenade, Stun", "Entangler Grenade"];
    }

    /// <summary>The figures the glossary states inside individual entries.</summary>
    public static class FeatureFigures
    {
        /// <summary>Braced: "If you have at least 6d Might…"</summary>
        public const int BracedTwoHandedMight = 6;

        public const string BracedRequires = "bracing or a mount";

        /// <summary>Irritant: a Power rank of 3d plus net successes, on an Easy (0) roll.</summary>
        public const int IrritantBaseDice = 3;

        /// <inheritdoc cref="IrritantBaseDice"/>
        public const int IrritantAttackThreshold = 0;

        /// <inheritdoc cref="IrritantBaseDice"/>
        public const string IrritantAttackThresholdLabel = "Easy";

        /// <inheritdoc cref="IrritantBaseDice"/>
        public const string IrritantAttackTrait = "Agility";

        /// <inheritdoc cref="IrritantBaseDice"/>
        public const string IrritantRankAdds = "net successes on the attack roll";

        /// <summary>Shield: "provides a +1d Shield Bonus", the same die a carried shield grants.</summary>
        public const int ShieldBonusDice = 1;

        /// <summary>Versatile: "Increase the item's Weapon Bonus by 1d…in two hands".</summary>
        public const int VersatileTwoHandedBonusDice = 1;

        /// <summary>Thrown: "within Close Range (or farther, depending on how strong you are…)".</summary>
        public const string ThrownBaseRange = "Close Range";

        /// <inheritdoc cref="ThrownBaseRange"/>
        public const string ThrownRangeDetailChapter = "Ch.4 Combat";

        /// <summary>
        /// The rank the four Power-delivering features run at: "a Power rank equal to your attack
        /// rank (your Trait plus the item's Weapon Bonus)".
        /// </summary>
        public const string PowerRankIsTheAttackRank =
            "the attack rank: the wielder's Trait plus the item's Weapon Bonus";

        /// <summary>Grabs, holds and combat stunts — what Binding and Flexible each allow.</summary>
        public static readonly string[] GrabsHoldsAndStunts = ["grabs", "holds", "combat stunts"];
    }

    // ── Equipment, p.91 ──────────────────────────────────────────────────────

    /// <summary>
    /// "Heroes are assumed to carry whatever tools and other equipment they need to use their
    /// skills, meaning their Talents with a rank of 4d or more… While that's normally enough, you
    /// will sometimes need other equipment. The following list should get you started."
    ///
    /// <para>Together with p.87: "Players don't have to worry about buying mundane gear. Heroes are
    /// assumed to have any mundane items they want as long as the GM thinks it's reasonable, taking
    /// Perks like Contacts, Patron, Resources, and Wealth into account… Unless a player goes way off
    /// the rails, none of this needs to be tracked… NPCs have whatever gear the GM wants them to
    /// have. Remember, however, that Minions don't use gear."</para>
    /// </summary>
    public static class MundaneGear
    {
        public const bool IsFree = true;
        public const bool IsTracked = false;
        public const bool CostsHeroPoints = false;
        public const int AssumedCarriedForTalentsAtRank = 4;
        public static readonly string[] AssumedCarriedExamples = ["lock picks for a Covert specialist"];

        /// <summary>The four Perks p.87 tells the GM to weigh, as perks.json ids.</summary>
        public static readonly string[] PerksTheGmWeighs = ["contacts", "patron", "resources", "wealth"];

        public const bool EvenABrokeHeroHasWhatTheyNeed = true;
        public const bool OrdinaryPossessionsAreNotTrackedEither = true;
        public const bool GmUsesCommonSense = true;
        public const bool NpcsHaveWhateverTheGmWants = true;
        public const bool MinionsDoNotUseGear = true;
        public const bool MinionGearIsDetailNotMechanics = true;
        public const bool ListIsExamplesNotACatalogueOfPrices = true;
        public const int ItemCount = 36;

        /// <summary>
        /// Zero, both of them, and that is the corroboration rather than a formality: the page
        /// prints no price and no availability for any of the thirty-six, which is what makes
        /// "mundane gear is free" a reading of the list as well as of p.87's paragraph.
        /// </summary>
        public const int PrintedPriceCount = 0;

        /// <inheritdoc cref="PrintedPriceCount"/>
        public const int PrintedAvailabilityCount = 0;
    }

    // ── Custom Gear, p.92 ────────────────────────────────────────────────────

    /// <summary>
    /// "Custom gear falls somewhere between the mundane armor, weapons, and equipment used by
    /// ordinary characters and the technological marvels and arcane artifacts represented by Powers
    /// with the Item Con. You can customize your mundane gear by adding unique elements, including
    /// Pros, Cons, and the features below. You have to spend Hero Points to customize gear. If you
    /// have the Two-Fisted power and you fight with paired weapons, you can customize two identical
    /// weapons for the price of one (you don't have to pay for each weapon separately). As with
    /// everything else, all gear customization is subject to the GM's review and approval."
    ///
    /// <para>And p.93's opening: "The GM is free to make features like these and any others they
    /// wish unavailable to Heroes unless they have Perks like Patron, Resources, or Wealth, or to
    /// prohibit certain features completely… if the GM approves, you should also feel free to get
    /// creative and make up your own features".</para>
    /// </summary>
    public static class CustomGear
    {
        public static readonly string[] SitsBetween = ["mundane gear", "a Power with the Item Con"];
        public const bool CostsHeroPoints = true;
        public static readonly string[] WhatMayBeAdded = ["pros", "cons", "custom features"];
        public const int CustomFeaturesAreOnPage = 93;

        /// <summary>Twelve, and they are <c>gear_features.json</c> rather than repeated here.</summary>
        public const int CustomFeatureCount = 12;

        public const bool PairedWeaponsCostOnce = true;
        public const string PairedWeaponsRequirePowerId = "two_fisted";
        public const bool PairedWeaponsMustBeIdentical = true;
        public const bool SubjectToGmApproval = true;
        public const bool PlayersMayInventFeatures = true;

        /// <summary>Three of the four Perks; p.93 names Patron, Resources and Wealth, not Contacts.</summary>
        public static readonly string[] GmMayGateFeaturesBehindPerks = ["patron", "resources", "wealth"];

        public const bool GmMayProhibitFeaturesEntirely = true;
    }

    // ── Pros and Cons, p.93 ──────────────────────────────────────────────────

    /// <summary>
    /// "As physical objects, every piece of gear has the Item Con. Apart from that, many other Pros
    /// and Cons can be applied to a piece of gear to make it unique. Pros and Cons cost the same when
    /// applied to gear as when applied to Powers. Regardless of Cons, no piece of gear can cost less
    /// than 0 Hero Points (in other words, no piece of gear will end up granting you extra Hero
    /// Points). Pros and Cons commonly applied to gear include Area of Effect, Armor Piercing,
    /// Build-Up, Burnout, Carrier Attack, Charges, Delay, Fuse, Independent, Line of Sight, Ongoing,
    /// Overkill, Overload, Penetrating, Phase Shift, Readied, Recharge, Selective, Subtle, Toxin,
    /// Trap, Two-Handed, Unreliable, and Weak."
    /// </summary>
    public static class GearProsAndCons
    {
        public const bool EveryPieceOfGearHasTheItemCon = true;
        public const string ItemConId = "item";

        /// <summary>
        /// <b>Why the engine does not credit it.</b> "Every piece of gear has the Item Con" says
        /// what gear is; it is not a discount to claim, and the same page's list of what is commonly
        /// applied does not include it — which is <see cref="ItemIsAbsentFromTheCommonList"/>.
        /// </summary>
        public const bool ItemConIsAStatementNotADiscount = true;

        public const bool CostIsTheSameAsOnAPower = true;

        /// <summary>Zero, where an unranked Power floors at one.</summary>
        public const int MinimumCostHeroPoints = 0;

        public const bool GearNeverPaysOut = true;

        /// <summary>The twenty-four, in printed order and printed spelling.</summary>
        public static readonly string[] CommonlyApplied =
        [
            "Area of Effect", "Armor Piercing", "Build-Up", "Burnout", "Carrier Attack", "Charges",
            "Delay", "Fuse", "Independent", "Line of Sight", "Ongoing", "Overkill", "Overload",
            "Penetrating", "Phase Shift", "Readied", "Recharge", "Selective", "Subtle", "Toxin",
            "Trap", "Two-Handed", "Unreliable", "Weak"
        ];

        public const int CommonlyAppliedCount = 24;

        /// <summary>"include" — so it is a list of the usual ones, not a closed set.</summary>
        public const bool ListIsNotExhaustive = true;

        /// <inheritdoc cref="ItemConIsAStatementNotADiscount"/>
        public const bool ItemIsAbsentFromTheCommonList = true;
    }
}
