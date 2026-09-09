using System.Text;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>Which of Chapter 6's three pickable tables a catalogue row came off.</summary>
public enum GearCatalogueKind
{
    /// <summary>p.88's nine-row Armor table.</summary>
    Armor,

    /// <summary>The sixty-three ancient, modern and advanced weapons rows.</summary>
    Weapon,

    /// <summary>p.91's thirty-six mundane items.</summary>
    Item
}

/// <summary>
/// One thing off Chapter 6's catalogue, in the one shape every host offers it in.
///
/// <para><b>It is a row, not a purchase.</b> Nothing here costs a Hero Point: p.91 says mundane
/// gear is free and untracked, and that is as true of a battle axe as of a torch. What a row
/// carries is what the page prints beside its name — a bonus, and the features that qualify it —
/// so a sheet can print the same line the book does.</para>
/// </summary>
/// <param name="Id">
/// The stable id a <see cref="SelectedGear"/> records. Prefixed by kind, because the three
/// tables are three id spaces: an armour row and an item could both be called "Scope".
/// </param>
/// <param name="Kind">Which table it came off.</param>
/// <param name="Name">The printed name, which is what the sheet shows.</param>
/// <param name="Category">Ancient, Modern or Advanced for the two era tables; null for p.91's items.</param>
/// <param name="BonusDice">
/// The Armor Bonus, the Weapon Bonus, or the item's own bonus — null where the page prints none.
/// </param>
/// <param name="Features">
/// The printed feature names: Bulky and Rigid on armour, the Weapon Features glossary's names on
/// a weapon. Empty on an item, which is priced and qualified by prose instead.
/// </param>
public sealed record GearCatalogueRow(
    string Id,
    GearCatalogueKind Kind,
    string Name,
    string? Category,
    int? BonusDice,
    IReadOnlyList<string> Features)
{
    /// <summary>The printed "(s)": the weapon knocks down rather than wounds.</summary>
    public bool Subdual { get; init; }

    /// <summary>
    /// True for the three weapon rows p.88's shield rule names. A shield in the off-hand adds a
    /// die to every defence as well as carrying the Weapon Bonus printed on its own row, so a
    /// sheet that showed only the weapon half would leave out what the thing is mostly for.
    /// </summary>
    public bool IsShield { get; init; }

    /// <summary>What an item's bonus applies to, where the page names one. Null elsewhere.</summary>
    public string? BonusAppliesTo { get; init; }

    /// <summary>The entry's own description, for a picker that shows one. Empty on a weapon row.</summary>
    public string Description { get; init; } = "";
}

/// <summary>
/// Chapter 6's armour, weapons and equipment as one flat list of pickable rows.
///
/// <para><b>It exists because five surfaces need the same answer</b> — the browser's Gear step,
/// the terminal wizard's, the command palette, the sheet formatter and the validator — and a
/// second flattening of the same three tables is a second thing to disagree with the first. The
/// rows are built once, on first use, off <see cref="RulesRepository.Equipment"/>.</para>
///
/// <para><b>Nothing here prices anything.</b> A catalogue row is free, and what a customised item
/// costs is <see cref="CostCalculator.GearCost"/>'s answer, unchanged by which row it names.</para>
/// </summary>
public sealed class GearCatalogue
{
    /// <summary>The three id prefixes, which are also the three tables.</summary>
    public const string ArmorPrefix = "armor:";

    /// <inheritdoc cref="ArmorPrefix"/>
    public const string WeaponPrefix = "weapon:";

    /// <inheritdoc cref="ArmorPrefix"/>
    public const string ItemPrefix = "item:";

    private readonly RulesRepository _rules;
    private IReadOnlyList<GearCatalogueRow>? _rows;
    private Dictionary<string, GearCatalogueRow>? _byId;

    public GearCatalogue(RulesRepository rules) => _rules = rules;

    /// <summary>Every pickable row: the nine armour rows, the sixty-three weapons, the thirty-six items.</summary>
    public IReadOnlyList<GearCatalogueRow> Rows => _rows ??= Build();

    /// <summary>The row with this id, or null. An id that resolves to nothing is the validator's business.</summary>
    public GearCatalogueRow? Find(string id) =>
        (_byId ??= Rows.ToDictionary(r => r.Id, StringComparer.Ordinal)).GetValueOrDefault(id);

    /// <summary>
    /// The die a shield in the off-hand adds to every defence (p.88). Read off the data rather
    /// than written here, so the figure has one home.
    /// </summary>
    public int ShieldBonusDice => _rules.Equipment.Shields.Shield?.BonusDice ?? 0;

    private List<GearCatalogueRow> Build()
    {
        var equipment = _rules.Equipment;

        var shieldRows = equipment.Shields.Shield?.WeaponRows ?? [];

        var rows = new List<GearCatalogueRow>();

        foreach (var armor in equipment.ArmorTable.Rows)
        {
            rows.Add(new GearCatalogueRow(
                ArmorPrefix + armor.Id, GearCatalogueKind.Armor, armor.Name,
                armor.Category, armor.ArmorBonusDice, armor.Features)
            {
                Description = armor.Description
            });
        }

        foreach (var table in equipment.WeaponTables)
        {
            foreach (var weapon in table.Weapons)
            {
                rows.Add(new GearCatalogueRow(
                    WeaponPrefix + Slug(weapon.Name), GearCatalogueKind.Weapon, weapon.Name,
                    Era(table.Id), weapon.BonusDice, weapon.Features)
                {
                    Subdual = weapon.Subdual,
                    IsShield = shieldRows.Contains(weapon.Name, StringComparer.Ordinal)
                });
            }
        }

        foreach (var item in equipment.EquipmentCatalogue.Items)
        {
            rows.Add(new GearCatalogueRow(
                ItemPrefix + item.Id, GearCatalogueKind.Item, item.Name,
                null, item.BonusDice, [])
            {
                BonusAppliesTo = item.BonusAppliesTo,
                Description = item.Description
            });
        }

        return rows;
    }

    /// <summary>
    /// "ancient_weapons" is the table's id and "Ancient" is what the reader is shown, which is
    /// also the word the armour table's own <c>category</c> column prints. One vocabulary across
    /// the two tables, so a palette can group by it.
    /// </summary>
    private static string? Era(string tableId)
    {
        var word = tableId.Split('_')[0];
        return word.Length == 0 ? null : char.ToUpperInvariant(word[0]) + word[1..];
    }

    /// <summary>
    /// A printed name as an id segment: "Shield, Spiked" becomes <c>shield_spiked</c>.
    ///
    /// <para><b>The weapons tables are the one table with no id column</b>, because they are a
    /// byte-for-byte copy of the play store's rows and that store keys them by name. Deriving the
    /// id from the name rather than transcribing sixty-three of them keeps the copy rule intact —
    /// and a name that changed in the book would change the id, which is why
    /// <c>GearCatalogueTests</c> pins every one of the sixty-three.</para>
    /// </summary>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var slug = new StringBuilder(name.Length);

        foreach (var c in name)
        {
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(char.ToLowerInvariant(c));
            else if (slug.Length > 0 && slug[^1] != '_') slug.Append('_');
        }

        return slug.ToString().Trim('_');
    }
}
