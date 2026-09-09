using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Renders a piece of gear as one line, the way a published sheet writes it:
/// <c>Jo Sticks (Upgraded, Two-Fisted pair) — 2 HP</c>. Plain mundane gear is free and
/// prints as just its name, which is nearly every item.
///
/// <para><b>An item chosen off Chapter 6's catalogue prints what the page prints beside its
/// name</b> — its bonus, the "(s)" that says it knocks down rather than wounds, and its
/// features: <c>Battle Axe +3 (Two-Handed)</c>. Those come off <see cref="GearCatalogue"/> by
/// the item's <see cref="SelectedGear.CatalogueId"/> and are never stored on the character, so
/// a figure corrected in <c>gear.json</c> corrects every sheet that names the row.</para>
///
/// <para><b>What is deliberately not here is the Armor rank a worn suit grants.</b> That is a
/// figure about the <em>wearer</em> — their Toughness under the Gear Limit, plus the suit's
/// bonus — so it needs the whole character, and it belongs with the other derived stats rather
/// than in a line about one object. <c>DerivedStatsCalculator.ArmorFromGear</c> answers it.</para>
/// </summary>
public static class GearFormatter
{
    public static string Describe(SelectedGear gear, RulesRepository rules, CostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(gear);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(costs);

        var row = gear.CatalogueId is null ? null : rules.Catalogue.Find(gear.CatalogueId);

        // The name, plus whatever the catalogue prints in the columns beside it. An id that
        // resolves to nothing prints as the bare name and is reported by the validator — an
        // illegal character is reported, never repaired, and a line that invented a bonus for an
        // unknown row would be the repair.
        var name = gear.Name + Bonus(row);

        if (gear is { IsCustomised: false, PairedUnderTwoFisted: false } && row is null) return name;

        var parts = new List<string>();

        // The row's own features first, because the book prints them beside the name; then what
        // this character bought for the item.
        if (row is not null) parts.AddRange(row.Features.Select(f => Feature(f, row, rules)));

        foreach (var f in gear.Features)
        {
            var feature = rules.GetGearFeature(f.FeatureId);
            parts.Add(f.GradeKey is null
                ? feature?.Name ?? f.FeatureId
                : GradeName(f.GradeKey));
        }

        parts.AddRange(gear.Pros.Select(p => rules.GetPro(p.Id)?.Name ?? p.Id));
        parts.AddRange(gear.Cons.Select(c => rules.GetCon(c.Id)?.Name ?? c.Id));

        if (gear.PairedUnderTwoFisted) parts.Add("Two-Fisted pair");

        var suffix = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";

        // **The price is printed only when there is one.** Every catalogue row is free — p.91 says
        // mundane gear is not bought — and "— 0 HP" beside a battle axe reads as a price the book
        // does not charge. An item that really did cost something still says so.
        if (gear is { IsCustomised: false, PairedUnderTwoFisted: false }) return name + suffix;

        return $"{name}{suffix} — {costs.GearCost(gear).ToString(CultureInfo.InvariantCulture)} HP";
    }

    /// <summary>
    /// The bonus column, the printed "(s)" beside it, and — on the two rows that have one — what
    /// the bonus is <em>for</em>.
    ///
    /// <para><b>A zero is printed and a null is not</b>, because they say different things: the
    /// Armor table prints 0 for Leather, and the weapons tables have one row the book gives no
    /// bonus at all. Suppressing the zero would make the two look alike on a sheet.</para>
    ///
    /// <para><b>An armour or weapon bonus applies to the thing the row is for and needs no
    /// saying; two of p.91's items are not like that.</b> The Crowbar's four dice are for "Might
    /// rolls made to force things open or apart" and the Climbing Claws' two are for climbing, and
    /// the page says so in the same breath as the figure. A sheet printing <c>Crowbar +4</c> beside
    /// <c>Battle Axe +3</c> states a general bonus the book does not grant — and the row carries
    /// the qualifier precisely so it can be printed.</para>
    /// </summary>
    private static string Bonus(GearCatalogueRow? row) =>
        row?.BonusDice is not { } dice
            ? ""
            : $" +{dice.ToString(CultureInfo.InvariantCulture)}{(row.Subdual ? "(s)" : "")}"
              + (string.IsNullOrEmpty(row.BonusAppliesTo) ? "" : $" to {row.BonusAppliesTo}");

    /// <summary>
    /// A printed feature name. <b>Shield is the one that carries a figure</b>: p.88 gives a shield
    /// in the off-hand a die on every defence, active and passive, and a line printing only the
    /// weapon half would leave out what the thing is mostly for.
    /// </summary>
    private static string Feature(string feature, GearCatalogueRow row, RulesRepository rules) =>
        row.IsShield && string.Equals(feature, "Shield", StringComparison.Ordinal)
            ? $"Shield +{rules.Catalogue.ShieldBonusDice.ToString(CultureInfo.InvariantCulture)}d defence"
            : feature;

    /// <summary>
    /// The two graded features print the grade the player bought, not the "X / Very X"
    /// heading: "very_accurate" reads as "Very Accurate".
    /// </summary>
    private static string GradeName(string gradeKey) =>
        string.Join(" ", gradeKey.Split('_')
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
