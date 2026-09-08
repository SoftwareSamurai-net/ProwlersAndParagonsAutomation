using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Chapter 6's three pickable tables, flattened into the rows every host offers.</b>
///
/// <para><see cref="GearCatalogue"/> is the one flattening, because five surfaces need it — the
/// browser's Gear step, the terminal wizard's, the command palette, the sheet formatter and the
/// validator — and a second one would be a second thing to disagree with the first. What is
/// checked here is the flattening: that every row of every table arrives, that the ids are stable
/// and distinct, and that a row still carries the bonus and features the page prints beside its
/// name. The tables' own contents are held to the rulebook by <see cref="EquipmentDataTests"/>.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GearCatalogueTests
{
    private readonly RulesFixture _f;
    private readonly GearCatalogue _catalogue;

    public GearCatalogueTests(RulesFixture fixture)
    {
        _f = fixture;
        _catalogue = new GearCatalogue(_f.Rules);
    }

    /// <summary>
    /// Every row of every table arrives, and the counts are the tables' own.
    ///
    /// <para>Asserted against each table's transcribed <c>row_count</c> rather than against 9, 63
    /// and 36 written here, so a table that lost half of itself fails
    /// <see cref="EquipmentDataTests"/> rather than quietly agreeing with a smaller number
    /// here.</para>
    /// </summary>
    [Fact]
    public void EveryRowOfEveryTableIsOffered()
    {
        var equipment = _f.Rules.Equipment;

        var armour = _catalogue.Rows.Where(r => r.Kind == GearCatalogueKind.Armor).ToList();
        var weapons = _catalogue.Rows.Where(r => r.Kind == GearCatalogueKind.Weapon).ToList();
        var items = _catalogue.Rows.Where(r => r.Kind == GearCatalogueKind.Item).ToList();

        Assert.Equal(equipment.ArmorTable.RowCount, armour.Count);
        Assert.Equal(equipment.WeaponTables.Sum(t => t.RowCount), weapons.Count);
        Assert.Equal(equipment.EquipmentCatalogue.MundaneGear.ItemCount, items.Count);

        // The three together are the whole list — nothing else is offered, and nothing is dropped.
        Assert.Equal(_catalogue.Rows.Count, armour.Count + weapons.Count + items.Count);
    }

    /// <summary>
    /// <b>The ids are distinct, and they are distinct because of the prefix rather than by luck.</b>
    /// The three tables are three id spaces — the armour rows carry ids of their own, the items
    /// carry theirs, and the weapons have only a printed name — so an unprefixed scheme would put
    /// an armour row and an item one collision away from each other.
    /// </summary>
    [Fact]
    public void EveryRowIdIsDistinctAndSaysWhichTableItCameFrom()
    {
        var ids = _catalogue.Rows.Select(r => r.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        Assert.All(_catalogue.Rows, row => Assert.StartsWith(
            row.Kind switch
            {
                GearCatalogueKind.Armor => GearCatalogue.ArmorPrefix,
                GearCatalogueKind.Weapon => GearCatalogue.WeaponPrefix,
                _ => GearCatalogue.ItemPrefix
            },
            row.Id, StringComparison.Ordinal));

        // Positive control on the distinctness above: there really are 108 rows, so "all distinct"
        // is not a statement about an empty list.
        Assert.Equal(108, _catalogue.Rows.Count);
        Assert.All(_catalogue.Rows, row => Assert.NotEqual("", row.Name));

        // **The prefix is prevention, not a fix for a collision that exists today.** Strip it and
        // the 108 segments are still distinct, so nothing here would break without it — which is
        // worth saying rather than claiming a control that is not there. It is kept because the
        // three tables are three id spaces the book fills independently: an armour row and an item
        // are one printed name apart, and the failure that would cause is a character sheet whose
        // recorded gear silently resolves to the wrong table's row.
        var segments = _catalogue.Rows.Select(r => r.Id[(r.Id.IndexOf(':') + 1)..]).ToList();
        Assert.Equal(segments.Count, segments.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A row that resolves and a row that does not. <see cref="GearCatalogue.Find"/> answering
    /// null is what lets the validator report an unknown id rather than the app throwing.
    /// </summary>
    [Fact]
    public void ARowIsFoundByItsIdAndAnUnknownOneIsNotInvented()
    {
        var plate = _catalogue.Find(GearCatalogue.ArmorPrefix + "ancient_plate");

        Assert.NotNull(plate);
        Assert.Equal("Plate", plate.Name);
        Assert.Equal(GearCatalogueKind.Armor, plate.Kind);

        Assert.Null(_catalogue.Find(GearCatalogue.ArmorPrefix + "ancient_platte"));
        Assert.Null(_catalogue.Find("ancient_plate"));
    }

    /// <summary>
    /// <b>The armour rows keep their bonus and their feature.</b> Plate is the anchor: 2 dice and
    /// Rigid, which is the feature that has no Might escape.
    /// </summary>
    [Fact]
    public void AnArmourRowCarriesItsBonusAndItsFeature()
    {
        var plate = _catalogue.Find(GearCatalogue.ArmorPrefix + "ancient_plate")!;

        Assert.Equal(2, plate.BonusDice);
        Assert.Equal(["Rigid"], plate.Features);
        Assert.Equal("Ancient", plate.Category);

        // Leather is the row with no bonus and no feature, which is what says the two above are
        // read off the row rather than filled in for every armour row alike.
        var leather = _catalogue.Find(GearCatalogue.ArmorPrefix + "ancient_leather")!;

        Assert.Equal(0, leather.BonusDice);
        Assert.Empty(leather.Features);
    }

    /// <summary>
    /// <b>A weapon row keeps its bonus, its features and its subdual mark</b>, and its id is
    /// derived from the printed name because the copied tables have no id column.
    /// </summary>
    [Fact]
    public void AWeaponRowCarriesItsPrintedBonusFeaturesAndSubdualMark()
    {
        var axe = _catalogue.Find(GearCatalogue.WeaponPrefix + "battle_axe")!;

        Assert.Equal(3, axe.BonusDice);
        Assert.Equal(["Two-Handed"], axe.Features);
        Assert.False(axe.Subdual);
        Assert.Equal("Ancient", axe.Category);

        // The baton is the counterpart on all three: one die, Thrown, and subdual.
        var baton = _catalogue.Find(GearCatalogue.WeaponPrefix + "baton")!;

        Assert.Equal(1, baton.BonusDice);
        Assert.Equal(["Thrown"], baton.Features);
        Assert.True(baton.Subdual);

        // And the three eras are all represented, so a table that stopped being read would show up
        // here rather than only in the count.
        Assert.Equal(
            ["Advanced", "Ancient", "Modern"],
            _catalogue.Rows.Where(r => r.Kind == GearCatalogueKind.Weapon)
                .Select(r => r.Category).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>A shield is a weapon row and a defence both, and the row says so.</b> p.88 names the
    /// three rows; the die they add is read off the shield rule rather than written here.
    /// </summary>
    [Fact]
    public void TheThreeShieldRowsAreMarkedAndTheDieComesFromThePage()
    {
        var shields = _catalogue.Rows.Where(r => r.IsShield).Select(r => r.Name).Order(StringComparer.Ordinal);

        Assert.Equal(_f.Rules.Equipment.Shields.Shield!.WeaponRows.Order(StringComparer.Ordinal), shields);

        Assert.Equal(1, _catalogue.ShieldBonusDice);

        // Every marked row really is a weapon row, which is the half "the names match" does not say.
        Assert.All(_catalogue.Rows.Where(r => r.IsShield),
            r => Assert.Equal(GearCatalogueKind.Weapon, r.Kind));

        // Control: an axe is not a shield.
        Assert.False(_catalogue.Find(GearCatalogue.WeaponPrefix + "battle_axe")!.IsShield);
    }

    /// <summary>
    /// <b>An item off p.91 carries whatever the page prints beside it and nothing else.</b> Most
    /// of the thirty-six are props with no figure at all, and inventing one for them would be a
    /// rule this project made up.
    /// </summary>
    [Fact]
    public void AnItemCarriesTheBonusThePagePrintsAndTheRestCarryNone()
    {
        var crowbar = _catalogue.Find(GearCatalogue.ItemPrefix + "crowbar")!;

        Assert.Equal(4, crowbar.BonusDice);
        Assert.NotNull(crowbar.BonusAppliesTo);
        Assert.NotEqual("", crowbar.Description);

        var torchless = _catalogue.Find(GearCatalogue.ItemPrefix + "polyhedral_dice")!;

        Assert.Null(torchless.BonusDice);
        Assert.Null(torchless.BonusAppliesTo);

        // The page prints no feature list for any of the thirty-six.
        Assert.All(_catalogue.Rows.Where(r => r.Kind == GearCatalogueKind.Item), r => Assert.Empty(r.Features));
    }

    /// <summary>The slug is what makes a printed name an id segment, and punctuation is not part of it.</summary>
    [Theory]
    [InlineData("Battle Axe", "battle_axe")]
    [InlineData("Shield, Spiked", "shield_spiked")]
    [InlineData("Military/Riot Gear", "military_riot_gear")]
    [InlineData("Sword (Great)", "sword_great")]
    public void APrintedNameBecomesAnIdSegment(string name, string expected) =>
        Assert.Equal(expected, GearCatalogue.Slug(name));

    // ── What a catalogue item costs, which is nothing ────────────────────────

    /// <summary>
    /// <b>Every kind of catalogue row is free.</b> p.91 is explicit that mundane gear is not bought
    /// and not tracked, and a weapon is mundane gear as much as a torch is — the whole reason the
    /// Gear step can offer a battle axe without a budget appearing.
    ///
    /// <para>Driven through <see cref="CostCalculator.GearCost"/> for one of each kind, because
    /// "the picker adds it at 0" is a fact about a component and this is a fact about the engine.</para>
    /// </summary>
    [Theory]
    [InlineData(GearCatalogue.WeaponPrefix + "battle_axe")]
    [InlineData(GearCatalogue.ArmorPrefix + "ancient_plate")]
    [InlineData(GearCatalogue.WeaponPrefix + "shield")]
    [InlineData(GearCatalogue.ItemPrefix + "crowbar")]
    public void EveryKindOfCatalogueRowIsFree(string rowId)
    {
        var row = _catalogue.Find(rowId);
        Assert.NotNull(row);

        Assert.Equal(0, _f.Costs.GearCost(new SelectedGear(row.Name) { CatalogueId = rowId }));
    }

    /// <summary>
    /// <b>A custom feature on a catalogue item costs what it costs, and naming the row changes
    /// nothing.</b> p.92 is the one place gear spends Hero Points, and it spends the same on a
    /// battle axe off the table as on something a player wrote down.
    /// </summary>
    [Fact]
    public void ACustomFeatureCostsTheSameOnACatalogueRowAsOnAnythingElse()
    {
        var axe = new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe",
            Features = [new("upgraded")]
        };

        var written = new SelectedGear("Battle Axe") { Features = [new("upgraded")] };

        Assert.Equal(_f.Costs.GearCost(written), _f.Costs.GearCost(axe));

        // The control: the feature really does cost something, so the equality above is not two
        // zeroes agreeing.
        Assert.True(_f.Costs.GearCost(axe) > 0);
    }

    /// <summary>
    /// <b>A whole sheet of catalogue gear costs nothing and moves no total.</b> The figure a
    /// character is judged on is unchanged by what they carry, which is what "free and untracked"
    /// has to mean at the level a budget is read.
    /// </summary>
    [Fact]
    public void ASheetOfCatalogueGearMovesNoTotal()
    {
        var bare = _f.LegalSheet();
        var laden = _f.LegalSheet();

        foreach (var row in _catalogue.Rows)
            laden.Gear.Add(new SelectedGear(row.Name) { CatalogueId = row.Id });

        Assert.Equal(_catalogue.Rows.Count, laden.Gear.Count);
        Assert.Equal(0, _f.Costs.TotalGearCost(laden));
        Assert.Equal(_f.Costs.TotalCost(bare), _f.Costs.TotalCost(laden));
    }
}
