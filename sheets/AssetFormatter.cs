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
    /// What one of the campaign's own objects is, in the line a picker prints beside its name:
    /// <c>shared vehicle · Body 8d · Speed 10d · Control +3 · unarmed</c>.
    ///
    /// <para><b>No figure in the object's own currency, unlike every other line in this class</b>,
    /// and that is the whole difference between this and <see cref="Describe(OwnedVehicle,
    /// CostCalculator)"/>. A shared object's budget is the sum of what its members put in, so the
    /// pair a reader wants — spent against budget — cannot be worked out from the object alone: it
    /// needs the sheets, which is what a campaign's own screen has and a character's has not. A
    /// number printed here would be half of that pair with nothing saying so.</para>
    ///
    /// <para><b>A base prints no characteristics</b>, because pp.100–103 give it none — the same
    /// reason <see cref="CostCalculator.CampaignAssetPointsSpent"/> does not charge for any.</para>
    /// </summary>
    public static string Describe(CampaignAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.IsHeadquarters) return "shared headquarters";

        var control = asset.Control > 0 ? $"+{N(asset.Control)}" : N(asset.Control);

        return $"shared vehicle · Body {N(asset.Body)}d · Speed {N(asset.Speed)}d · "
             + $"Control {control} · "
             + (asset.Weapons is { } weapons ? $"Weapons {N(weapons)}d" : "unarmed");
    }

    /// <summary>
    /// A campaign's shared object with its books open: <c>The Wing — 18/25 Vehicle Points</c>.
    ///
    /// <para><b>The pair, where <see cref="Describe(CampaignAsset)"/> prints neither half.</b>
    /// That method is for a reader holding the object alone, who cannot know what its members put
    /// in; this one is for the campaign's own page, which has read the sheets and can. The budget
    /// is the sum of every contribution naming it and is worked out by the caller — see
    /// <see cref="CostCalculator.CampaignAssetBudget"/> — because joining an object to the sheets
    /// that paid for it is storage, and no rules code may do that.</para>
    ///
    /// <para><b>A spend of null is an object these rules cannot price</b>, and the line then
    /// carries what was put in and stops. Printing a zero would be a claim that nothing has been
    /// built with it, which is the opposite of what is known: what is known is that nothing here
    /// can say. A screen drawing this is expected to say which, in its own words.</para>
    /// </summary>
    /// <param name="asset">The shared object.</param>
    /// <param name="spent">What has been built with it, or null where that cannot be worked out.</param>
    /// <param name="budget">What its members' Hero Points bought it, in the same currency.</param>
    public static string Describe(CampaignAsset asset, int? spent, int budget)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var name = string.IsNullOrWhiteSpace(asset.Name) ? asset.Id : asset.Name;
        var currency = asset.IsHeadquarters ? "Base Points" : "Vehicle Points";

        return spent is { } cost
            ? $"{name} — {N(cost)}/{N(budget)} {currency}"
            : $"{name} — {N(budget)} {currency} put in";
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
