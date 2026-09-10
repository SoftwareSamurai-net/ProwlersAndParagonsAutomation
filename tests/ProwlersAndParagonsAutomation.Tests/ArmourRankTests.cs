using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>What a worn suit of armour is worth, which is neither page's answer alone.</b>
///
/// <para>p.88 gives the rank as the wearer's Toughness plus the suit's Armor Bonus and never
/// restates the Gear Limit. p.87 is where the limit is: it caps the Trait rank you can apply when
/// using mundane equipment that boosts your Traits "(usually armor and weapons)", its worked
/// example fixes the arithmetic at limit-plus-bonus rather than trait-plus-bonus-then-capped, and
/// it says outright that this makes mundane armour less useful to a superhuman. So the answer is
/// <c>min(base, limit) + bonus</c>, and the surprising half is the printed intent.
/// <c>docs/guide/rules-engine.md</c> carries the whole argument; <c>gear.json</c> records it as an
/// <c>interpretation</c> rather than as a fact p.88 states.</para>
///
/// <para><b>It is a figure the engine reports and never a Power it buys.</b> Mundane gear is free,
/// an Armor Power on the sheet would cost Hero Points, and this engine does not make design
/// decisions about somebody's character.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ArmourRankTests
{
    private readonly RulesFixture _f;

    public ArmourRankTests(RulesFixture fixture) => _f = fixture;

    private CharacterSheet InPlate(int toughness)
    {
        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["toughness"] = toughness;
        sheet.Gear.Add(new SelectedGear("Plate") { CatalogueId = GearCatalogue.ArmorPrefix + "ancient_plate" });
        return sheet;
    }

    /// <summary>
    /// <b>The default Gear Limit is a figure in C#, and this is what holds it to the book.</b>
    ///
    /// <para>p.87 is extracted, and it is extracted in <c>data/rules/play/equipment.json</c> — the
    /// play store, which <c>engine/</c> may not read and <c>play/</c> cannot do without. The
    /// alternative was a second transcription of one number into <c>gear.json</c>, which is the
    /// duplication the weapon-table copy rule already carries for sixty-three rows and a poor trade
    /// for one. So the figure lives in the engine and this test is the seam, exactly as
    /// <c>EquipmentDataTests.TheWeaponTablesAreACopyOfThePlayStoresAndAreHeldEqualToIt</c> is the
    /// seam for the rows: a test may read both stores, and neither store may read the other.</para>
    /// </summary>
    [Fact]
    public void TheDefaultGearLimitIsThePlayStoresAndTheBooksSameFigure()
    {
        var json = File.ReadAllText(
            Path.Combine(RulesFixture.DataPath, "play", "equipment.json"));

        var entry = JsonDocument.Parse(json).RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == "gear_limit");

        var printed = entry.GetProperty("gear_limit").GetProperty("default_rank").GetInt32();

        Assert.Equal(DerivedStatsCalculator.DefaultGearLimitRank, printed);

        // And the entry really is the p.87 one, so this is pinned to the page rather than to
        // whatever an entry called gear_limit happens to say.
        Assert.Contains("p.87", entry.GetProperty("source_ref").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>p.87's own worked example, run through the engine.</b> The page works it for a weapon —
    /// a 12d-Agility character with a 2-die pistol tops out at 8 — and the same arithmetic is what
    /// caps a suit. A 10d-Toughness Hero in Plate is 8d, not 12d.
    /// </summary>
    [Fact]
    public void TheGearLimitCapsTheWearerAndTheSuitsBonusIsAddedAfterwards()
    {
        Assert.Equal(8, _f.Derived.ArmorFromGear(InPlate(10)));

        // The rival reading, which p.87 rules out: the whole Toughness plus the bonus.
        Assert.NotEqual(12, _f.Derived.ArmorFromGear(InPlate(10)));

        // Under the limit, the wearer's own figure is what is used — so the cap is a cap and not a
        // flat 6 for everybody.
        Assert.Equal(6, _f.Derived.ArmorFromGear(InPlate(4)));
        Assert.Equal(8, _f.Derived.ArmorFromGear(InPlate(6)));
    }

    /// <summary>
    /// <b>A table that raised the limit gets the raised figure</b>, and a rank left on the table
    /// while the switch is off is a figure the table has not adopted — which is how
    /// <c>play/</c> reads the pair and therefore how this must.
    /// </summary>
    [Fact]
    public void ARaisedGearLimitIsUsedAndAnUnadoptedRankIsNot()
    {
        var raised = InPlate(10);
        raised.CampaignTable = new CampaignTable { RaisedGearLimit = true, GearLimitRank = 12 };

        Assert.Equal(12, _f.Derived.ArmorFromGear(raised));

        // The switch off: the rank is written down and not adopted, so the default stands.
        var written = InPlate(10);
        written.CampaignTable = new CampaignTable { GearLimitRank = 12 };

        Assert.Equal(8, _f.Derived.ArmorFromGear(written));
    }

    /// <summary>
    /// <b>Somebody who already has the Armor Power measures from it</b> — p.88's second sentence,
    /// so "an armoured hero in a borrowed shell is not reduced to an ordinary person's baseline".
    /// A Power's rank substituted in that way is a Trait rank like any other and is capped the
    /// same, which is the guide's reading of p.87.
    /// </summary>
    [Fact]
    public void TheWearersOwnArmorPowerStandsInForToughnessAndIsCappedTheSameWay()
    {
        // Armor's baseline is half Toughness, so 2d Toughness plus 3 purchased ranks is 4d — above
        // the raw Toughness this character has, which is the case the sentence is about.
        var sheet = InPlate(2);
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3));

        var ownRank = _f.Derived.GetEffectiveRank(sheet.GetPower("armor")!, sheet);

        Assert.True(ownRank > 2, $"the fixture is not exercising the substitution: own Armor is {ownRank}d "
            + "and Toughness is 2d, so the higher of the two is Toughness and this test proves nothing.");

        Assert.Equal(ownRank + 2, _f.Derived.ArmorFromGear(sheet));
    }

    /// <summary>
    /// <b>A suit never lowers a wearer's own Armor</b> — the owner's 2026-09-10 ruling. p.88 calls
    /// what a suit does a grant and what an existing Armor Power gets an option; neither is a
    /// subtraction, so a Power well above the Gear Limit is not capped down to it: Armor 12d in
    /// Plate prints 12d, and the suit contributes nothing. This is the case the ruling's own
    /// example names, and it is the opposite of the old behaviour, which this test used to pin at
    /// 8 before the ruling.
    /// </summary>
    [Fact]
    public void ASuitNeverLowersAWearersOwnArmor()
    {
        var superhuman = InPlate(2);
        superhuman.SelectedPowers.Add(new SelectedPower("armor", 12));

        var ownRank = _f.Derived.GetEffectiveRank(superhuman.GetPower("armor")!, superhuman);

        Assert.Equal(ownRank, _f.Derived.ArmorFromGear(superhuman));
        Assert.True(_f.Derived.WornArmorSuitContributesNothing(superhuman));
    }

    /// <summary>
    /// <b>The boundary, driven from both sides.</b> Plate's bonus is +2 and the default Gear Limit
    /// is 6d, so the suit's own contribution tops out at 8d. An own Armor rank of 7d is still below
    /// that and the suit still supplies the higher figure; at 8d the two are equal; at 9d the
    /// wearer's own rank is what governs and the suit adds nothing — the floor from the ruling
    /// above, exercised one die on either side of where it starts to matter.
    /// </summary>
    [Fact]
    public void TheOwnArmorFloorIsExercisedFromBothSidesOfWhatTheSuitWouldGrant()
    {
        var justBelow = ArmorRankInSuitAt(7);
        var atTheBoundary = ArmorRankInSuitAt(8);
        var justAbove = ArmorRankInSuitAt(9);

        Assert.Equal(8, justBelow); // the suit's capped contribution still wins
        Assert.False(_f.Derived.WornArmorSuitContributesNothing(SheetWithOwnArmor(7)));

        Assert.Equal(8, atTheBoundary); // equal either way, so either reading answers 8

        Assert.Equal(9, justAbove); // the wearer's own rank now wins, floored rather than capped
        Assert.True(_f.Derived.WornArmorSuitContributesNothing(SheetWithOwnArmor(9)));
    }

    /// <summary>
    /// A sheet in Plate whose own Armor Power sits at exactly <paramref name="ownArmorRank"/>,
    /// built by giving Armor no baseline contribution (Toughness 0) and purchasing the rank
    /// directly, so the figure under test is exact rather than derived from a baseline formula.
    /// </summary>
    private CharacterSheet SheetWithOwnArmor(int ownArmorRank)
    {
        var sheet = InPlate(0);
        sheet.SelectedPowers.Add(new SelectedPower("armor", ownArmorRank));
        return sheet;
    }

    private int ArmorRankInSuitAt(int ownArmorRank) =>
        _f.Derived.ArmorFromGear(SheetWithOwnArmor(ownArmorRank))!.Value;

    /// <summary>
    /// <b>Only the best suit answers.</b> A character wearing two suits is wearing one and carrying
    /// the other, and adding them would pay twice for a thing the page grants once.
    /// </summary>
    [Fact]
    public void TwoSuitsGrantTheBetterRankRatherThanBoth()
    {
        var sheet = InPlate(10);
        sheet.Gear.Add(new SelectedGear("Leather")
        {
            CatalogueId = GearCatalogue.ArmorPrefix + "ancient_leather"
        });

        Assert.Equal(8, _f.Derived.ArmorFromGear(sheet));

        // The control: the worse suit alone really does answer differently, so "the better one"
        // is a choice rather than the only figure available.
        var poor = _f.LegalSheet();
        poor.AbilityRanks["toughness"] = 10;
        poor.Gear.Add(new SelectedGear("Leather")
        {
            CatalogueId = GearCatalogue.ArmorPrefix + "ancient_leather"
        });

        Assert.Equal(6, _f.Derived.ArmorFromGear(poor));
    }

    /// <summary>
    /// <b>No suit, no figure — and no Power either way.</b> Null rather than 0, because a character
    /// wearing nothing has no Armor rank at all and 0d is a rank. And nothing anywhere adds the
    /// Power: p.91 makes the suit free, and an Armor Power on the sheet would cost Hero Points.
    /// </summary>
    [Fact]
    public void AnUnarmouredCharacterHasNoRankAndAnArmouredOneBuysNoPower()
    {
        var bare = _f.LegalSheet();
        Assert.Null(_f.Derived.ArmorFromGear(bare));

        // A weapon is not armour, so the answer is still nothing.
        bare.Gear.Add(new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe"
        });

        Assert.Null(_f.Derived.ArmorFromGear(bare));

        // And the suit costs nothing and buys nothing. Measured against the same character with
        // the suit taken off, so the 10d Toughness that character bought is on both sides.
        var armoured = InPlate(10);

        var unarmoured = _f.LegalSheet();
        unarmoured.AbilityRanks["toughness"] = 10;

        Assert.Empty(armoured.SelectedPowers);
        Assert.Equal(0, _f.Costs.TotalGearCost(armoured));
        Assert.Equal(_f.Costs.TotalCost(unarmoured), _f.Costs.TotalCost(armoured));
    }
}
