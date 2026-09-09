using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Renders a vehicle, a headquarters, a Gadget and a contribution to a campaign's shared object
/// as the lines a published sheet writes.
///
/// <para><b>Every line names its own currency, and that is not decoration.</b> A vehicle costs
/// Vehicle Points, a base costs Base Points, and only the Perk on each is in Hero Points — a sheet
/// printing three bare numbers beside each other would be inviting the exact category error
/// <c>CostCalculator</c> is careful not to make.</para>
///
/// <para><b>A feature's name comes off the rules data, never off the character.</b> The same rule
/// <see cref="GearFormatter"/> follows: a price corrected in <c>vehicles.json</c> corrects every
/// sheet, and an id that resolves to nothing prints as the id and is reported by the validator —
/// an illegal character is reported, never repaired, and a line that invented a name for an
/// unknown feature would be the repair.</para>
/// </summary>
public static class AssetFormatter
{
    /// <summary>
    /// A vehicle's headline: <c>The Wing — 24/25 Vehicle Points (1 HP)</c>. The spend against the
    /// budget rather than the spend alone, because a machine's whole story is how much of what its
    /// Perk bought it has used.
    /// </summary>
    public static string Describe(OwnedVehicle vehicle, CostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(costs);

        return $"{vehicle.Name} — {Spend(() => costs.VehiclePointsSpent(vehicle),
                                          costs.VehiclePointBudget(vehicle), "Vehicle Points")} "
             + $"({N(vehicle.PerkHeroPoints)} HP)";
    }

    /// <summary>
    /// The four characteristics, as one line: <c>Body 8d · Speed 10d · Control 5 · Weapons 12d</c>.
    ///
    /// <para><b>Control carries no "d" and an unarmed machine says so.</b> p.94 calls Control "a
    /// modifier rather than a rank in its own right", and the printed tables give an unarmed
    /// vehicle an em dash rather than a zero — which is a different claim from a rank of
    /// nothing.</para>
    /// </summary>
    public static string Characteristics(OwnedVehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        var control = vehicle.Control > 0 ? $"+{N(vehicle.Control)}" : N(vehicle.Control);

        return $"Body {N(vehicle.Body)}d · Speed {N(vehicle.Speed)}d · Control {control} · "
             + (vehicle.Weapons is { } weapons ? $"Weapons {N(weapons)}d" : "unarmed");
    }

    /// <summary>A headquarters' headline, in the other second currency.</summary>
    public static string Describe(OwnedHeadquarters headquarters, CostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(headquarters);
        ArgumentNullException.ThrowIfNull(costs);

        return $"{headquarters.Name} — {Spend(() => costs.BasePointsSpent(headquarters),
                                               costs.BasePointBudget(headquarters), "Base Points")} "
             + $"({N(headquarters.PerkHeroPoints)} HP)";
    }

    /// <summary>
    /// A Gadget's headline: <c>Freeze Ray — 10/12 of a pool that paid out</c>.
    ///
    /// <para><b>"paid out" rather than "HP", because the direction is the whole point.</b> A
    /// Gadget costs the character nothing; the figure is what its build handed over, and a reader
    /// who reads it as a spend has read the one calculation in this chapter backwards.</para>
    /// </summary>
    public static string Describe(BuiltGadget gadget, CostCalculator costs, int? houseImmortalityCost = null)
    {
        ArgumentNullException.ThrowIfNull(gadget);
        ArgumentNullException.ThrowIfNull(costs);

        return $"{gadget.Name} (Complexity {N(gadget.Complexity)}) — "
             + Spend(() => costs.GadgetSpend(gadget, houseImmortalityCost),
                     costs.GadgetPool(gadget), "Hero Points the build paid out");
    }

    /// <summary>
    /// What this character put into a campaign's shared vehicle or base.
    ///
    /// <para><b>Only what they put in.</b> What the object came out as is the campaign's answer,
    /// summed from every member — a sheet that printed the machine would be printing one of five
    /// copies of it.</para>
    /// </summary>
    public static string Describe(CampaignAssetContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        var name = string.IsNullOrWhiteSpace(contribution.Name)
            ? contribution.AssetId
            : contribution.Name;

        var kind = string.Equals(contribution.Kind, CampaignAssetContribution.Headquarters,
                                 StringComparison.Ordinal)
            ? "shared headquarters"
            : "shared vehicle";

        return $"{name} ({kind}) — {N(contribution.HeroPoints)} HP put in";
    }

    /// <summary>
    /// A feature as it prints on a sheet: its name, the grade where it has one, and the count
    /// where it is bought per unit — <c>Passengers ×2</c>, <c>Science Labs (Advanced)</c>.
    /// </summary>
    /// <param name="selection">The feature bought.</param>
    /// <param name="name">Its printed name, or null when the id resolves to nothing.</param>
    /// <param name="perUnit">Whether the rulebook prices it per unit.</param>
    public static string Feature(SelectedAssetFeature selection, string? name, bool perUnit)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var printed = name ?? selection.FeatureId;

        if (selection.GradeKey is { } grade) return $"{printed} ({GradeName(grade)})";

        return perUnit && selection.Units != 1 ? $"{printed} ×{N(selection.Units)}" : printed;
    }

    /// <summary>One vehicle feature, resolved against the rules data.</summary>
    public static string Feature(SelectedAssetFeature selection, RulesRepository rules, bool onAVehicle)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(rules);

        if (onAVehicle)
        {
            var vehicle = rules.Assets.FindVehicleFeature(selection.FeatureId);
            return Feature(selection, vehicle?.Name, vehicle?.CostType == "per_unit");
        }

        var basic = rules.Assets.FindBaseFeature(selection.FeatureId);
        return Feature(selection, basic?.Name, basic?.CostType == "per_unit");
    }

    /// <summary>
    /// A grade key as a reader would write it: <c>awe_inspiring</c> reads as "Awe Inspiring".
    /// The same conversion <see cref="GearFormatter"/> makes, for the same reason — snake_case is
    /// how this project stores an id and not how the rulebook prints a word.
    /// </summary>
    private static string GradeName(string gradeKey) =>
        string.Join(" ", gradeKey.Split('_')
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>
    /// <b>Whether a figure this sheet wants can be reached at all.</b>
    ///
    /// <para><c>CostCalculator</c> throws on a feature it cannot price, a Pro or Con the rulebook
    /// does not have and a variant key that resolves to nothing — deliberately, because a price is
    /// not a thing to guess at. Every one of those is a mistake the validator already
    /// <em>reports</em>, so a sheet is being asked to print the finding and the figure beside it,
    /// and an exception takes the whole report down over one mistyped id instead.</para>
    ///
    /// <para><b>Asked here rather than answered structurally.</b> The JSON export's Gadget gate
    /// checked the Power ids alone and let an unknown Con through, which is the shape of every
    /// partial answer to this question: the calculator knows what it can price and nothing
    /// else does.</para>
    /// </summary>
    public static int? Reachable(Func<int> figure)
    {
        ArgumentNullException.ThrowIfNull(figure);

        try
        {
            return figure();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// A spend against the budget it comes out of — <c>24/25 Vehicle Points</c> — or the sentence
    /// that says the spend could not be worked out, with the budget kept because that half is
    /// knowable and a reader still wants it.
    /// </summary>
    private static string Spend(Func<int> spent, int budget, string currency) =>
        Reachable(spent) is { } figure
            ? $"{N(figure)}/{N(budget)} {currency}"
            : $"spend cannot be worked out, out of {N(budget)} {currency}";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
