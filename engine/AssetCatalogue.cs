using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>Which of Chapter 6's three pickable asset tables a row came off.</summary>
public enum AssetRowKind
{
    /// <summary>One of p.96's six stock vehicles, which seeds a machine rather than being one.</summary>
    StockVehicle,

    /// <summary>One of p.96-100's twenty-three vehicle features, priced in Vehicle Points.</summary>
    VehicleFeature,

    /// <summary>One of pp.100-103's twenty-two base features, priced in Base Points.</summary>
    BaseFeature
}

/// <summary>
/// One pickable thing off Chapter 6's vehicle and headquarters tables.
///
/// <para><b>It is a row, not a purchase</b> — the same relationship <see cref="GearCatalogueRow"/>
/// has to a piece of gear. What it carries is what the page prints beside the name, so a picker can
/// show a price without a host doing arithmetic of its own.</para>
///
/// <para><b>And the price is not in Hero Points.</b> A vehicle feature costs Vehicle Points and a
/// base feature Base Points; the two currencies are bought at twenty-five and three to the Hero
/// Point respectively, and nowhere else do they meet. A host that adds one of these to a Hero
/// Point total has made a category error.</para>
/// </summary>
/// <param name="Id">The stable id a selection records, prefixed by kind — three id spaces.</param>
/// <param name="Kind">Which table it came off.</param>
/// <param name="Name">The printed name.</param>
/// <param name="Keywords">What a filter matches beyond the name: what the feature does, and any restriction.</param>
public sealed record AssetCatalogueRow(
    string Id,
    AssetRowKind Kind,
    string Name,
    IReadOnlyList<string> Keywords)
{
    /// <summary>The flat price in the row's own currency, or null when it is graded or per unit.</summary>
    public int? Cost { get; init; }

    /// <summary>The graded prices, keyed by the word the entry itself uses. Null when flat.</summary>
    public IReadOnlyDictionary<string, int>? Grades { get; init; }

    /// <summary>The rate for a per-unit row, or null.</summary>
    public int? CostPerUnit { get; init; }

    /// <summary>What one unit of a per-unit row buys, as the page words it.</summary>
    public string? UnitLabel { get; init; }

    /// <summary>A kind of vehicle the feature is limited to, or null.</summary>
    public string? RestrictedTo { get; init; }

    /// <summary>A hard limit the entry prints beside its price, or null.</summary>
    public string? Constraint { get; init; }

    /// <summary>What the row does, in this project's words. The four ranks, for a stock vehicle.</summary>
    public string Description { get; init; } = "";

    /// <summary>The page the entry is printed on. 96 for every stock vehicle.</summary>
    public int PrintedPage { get; init; }

    /// <summary>The stock vehicle this row seeds, or null on a feature row.</summary>
    public StockVehicleRow? Stock { get; init; }
}

/// <summary>
/// Chapter 6's vehicles, headquarters and gadgets as pickable rows and named figures.
///
/// <para><b>It exists for the reason <see cref="GearCatalogue"/> does</b>: five surfaces want the
/// same answer — the browser's assets step, the terminal wizard's, the command palette, the sheet
/// formatter and the validator — and a second flattening is a second thing to disagree with the
/// first.</para>
///
/// <para><b>Nothing here prices a character.</b> What a vehicle costs in Vehicle Points is
/// <see cref="CostCalculator.VehiclePointsSpent"/>'s answer; this is where the printed rates it
/// reads come from, so that no figure is spelled in C#.</para>
/// </summary>
public sealed class AssetCatalogue
{
    /// <summary>The three id prefixes, which are also the three tables.</summary>
    public const string StockPrefix = "stock:";

    /// <inheritdoc cref="StockPrefix"/>
    public const string VehicleFeaturePrefix = "vfeature:";

    /// <inheritdoc cref="StockPrefix"/>
    public const string BaseFeaturePrefix = "bfeature:";

    /// <summary>
    /// p.99's Mecha, the one vehicle feature with a floor of its own, and p.103's Training
    /// Facilities, the one base feature that hands out a currency. Named because the validator and
    /// the derived figures ask about them by id, and a string spelled at each site is a string that
    /// only one of them would fix.
    /// </summary>
    public const string MechaFeatureId = "mecha";

    /// <inheritdoc cref="MechaFeatureId"/>
    public const string TrainingFacilitiesFeatureId = "training_facilities";

    private readonly RulesRepository _rules;
    private IReadOnlyList<AssetCatalogueRow>? _rows;
    private Dictionary<string, AssetCatalogueRow>? _byId;
    private Dictionary<string, VehicleFeatureRow>? _vehicleFeatures;
    private Dictionary<string, BaseFeatureRow>? _baseFeatures;

    public AssetCatalogue(RulesRepository rules) => _rules = rules;

    /// <summary>The six stock vehicles, the twenty-three vehicle features, the twenty-two base features.</summary>
    public IReadOnlyList<AssetCatalogueRow> Rows => _rows ??= Build();

    /// <summary>The row with this id, or null. An id that resolves to nothing is the validator's business.</summary>
    public AssetCatalogueRow? Find(string id) =>
        (_byId ??= Rows.ToDictionary(r => r.Id, StringComparer.Ordinal)).GetValueOrDefault(id);

    /// <summary>One of p.96-100's vehicle features by its own id, unprefixed, or null.</summary>
    public VehicleFeatureRow? FindVehicleFeature(string id) =>
        (_vehicleFeatures ??= VehicleFeatures.ToDictionary(f => f.Id, StringComparer.Ordinal))
            .GetValueOrDefault(id);

    /// <summary>One of pp.100-103's base features by its own id, unprefixed, or null.</summary>
    public BaseFeatureRow? FindBaseFeature(string id) =>
        (_baseFeatures ??= BaseFeatures.ToDictionary(f => f.Id, StringComparer.Ordinal))
            .GetValueOrDefault(id);

    // ── The named figures, read off the data rather than written here ─────────

    /// <summary>The twenty-three vehicle features, in printed order.</summary>
    public IReadOnlyList<VehicleFeatureRow> VehicleFeatures =>
        Entry(_rules.Vehicles.Entries, "vehicle_features").Features ?? [];

    /// <summary>The twenty-two base features, in printed order.</summary>
    public IReadOnlyList<BaseFeatureRow> BaseFeatures =>
        Entry(_rules.Headquarters.Entries, "base_features").Features ?? [];

    /// <summary>The six stock vehicles printed on p.96.</summary>
    public IReadOnlyList<StockVehicleRow> StockVehicles =>
        Entry(_rules.Vehicles.Entries, "stock_vehicles").Stock ?? [];

    /// <summary>p.96: twenty-five Vehicle Points for each Hero Point of the Unique Vehicle Perk.</summary>
    public int VehiclePointsPerHeroPoint =>
        Entry(_rules.Vehicles.Entries, "unique_vehicle_perk").UniqueVehiclePerk!.VehiclePointsPerHeroPoint;

    /// <summary>p.100: three Base Points for each Hero Point of the Headquarters Perk.</summary>
    public int BasePointsPerHeroPoint =>
        Entry(_rules.Headquarters.Entries, "headquarters_perk").HeadquartersPerk!.BasePointsPerHeroPoint;

    /// <summary>p.96: what a rank of each of the four characteristics costs, and the floor on Control.</summary>
    public UniqueVehicleCharacteristics Characteristics =>
        Entry(_rules.Vehicles.Entries, "unique_vehicle_characteristics").UniqueVehicleCharacteristics!;

    /// <summary>p.94: what a successful build pays out, per point of Complexity.</summary>
    public GadgetBuild GadgetBuild =>
        Entry(_rules.Gadgets.Entries, "gadget_build").Build!;

    /// <summary>p.94: the least Complexity a Gadget may be assigned.</summary>
    public int MinimumGadgetComplexity =>
        Entry(_rules.Gadgets.Entries, "gadget_complexity").Complexity!.Minimum;

    /// <summary>p.103: one point of Teamwork an issue, to everybody sharing the base.</summary>
    public Teamwork Teamwork =>
        Entry(_rules.Headquarters.Entries, "teamwork").Teamwork!;

    private static T Entry<T>(IReadOnlyList<T> entries, string id) where T : Chapter6Entry =>
        entries.FirstOrDefault(e => e.Id == id)
        ?? throw new InvalidOperationException(
               $"Chapter 6's rules data has no entry '{id}'. Every figure this catalogue reads is "
               + "off the data on purpose, so a missing entry is a broken rules file rather than a "
               + "default to fall back on.");

    private List<AssetCatalogueRow> Build()
    {
        var rows = new List<AssetCatalogueRow>();

        foreach (var stock in StockVehicles)
        {
            rows.Add(new AssetCatalogueRow(
                StockPrefix + GearCatalogue.Slug(stock.Name), AssetRowKind.StockVehicle, stock.Name,
                stock.Features)
            {
                Cost        = stock.VehiclePoints,
                Description = $"Body {stock.Body}d · Speed {stock.Speed}d · Control {stock.Control}"
                            + (stock.Weapons is { } w ? $" · Weapons {w}d" : " · unarmed"),
                PrintedPage = 96,
                Stock       = stock
            });
        }

        foreach (var feature in VehicleFeatures)
        {
            rows.Add(new AssetCatalogueRow(
                VehicleFeaturePrefix + feature.Id, AssetRowKind.VehicleFeature, feature.Name,
                Keywords(feature.RestrictedTo, feature.Requires))
            {
                Cost         = feature.Cost,
                Grades       = feature.CostRange,
                CostPerUnit  = feature.CostPerUnit,
                UnitLabel    = feature.UnitLabel,
                RestrictedTo = feature.RestrictedTo,
                Constraint   = feature.Constraint,
                Description  = feature.Mechanic,
                PrintedPage  = feature.PrintedPage
            });
        }

        foreach (var feature in BaseFeatures)
        {
            rows.Add(new AssetCatalogueRow(
                BaseFeaturePrefix + feature.Id, AssetRowKind.BaseFeature, feature.Name, [])
            {
                Cost        = feature.Cost,
                Grades      = feature.CostRange,
                CostPerUnit = feature.CostPerUnit,
                UnitLabel   = feature.UnitLabel,
                Constraint  = feature.Constraint,
                Description = feature.Mechanic,
                PrintedPage = feature.PrintedPage
            });
        }

        return rows;
    }

    /// <summary>
    /// <b>One of p.96's stock machines' printed feature lines, as selections a host can copy.</b>
    ///
    /// <para>The page prints a list of names, and two of the lines are not bare names. "Passengers
    /// 4" is four <em>extra</em> passengers, which is the unit the feature is priced per — the
    /// entry's own interpretation block says so. <b>"Rader (Sonar)" is not a feature at all</b>:
    /// it is the Radar Power carrying the Sonar Con printed inside its own Ch.2 entry, taken
    /// through Unique Systems, and copying it would mean pricing a Power to work out how many
    /// Vehicle Points of Unique Systems to record.</para>
    ///
    /// <para><b>So it is skipped, and the reader adds it.</b> The copied Submersible comes out at
    /// twelve Vehicle Points against a printed fourteen — under its budget rather than over, which
    /// is the safe direction for a decision a host makes on somebody's behalf. Never repaired into
    /// something else: a <c>SelectedAssetFeature("rader_(sonar)")</c> would be an id nothing has,
    /// which makes the whole machine unpriceable and is a worse answer than a system the reader
    /// can see is missing.</para>
    ///
    /// <para><b>It lives here because two hosts wanted it.</b> The browser's assets step and the
    /// terminal wizard each carried a private copy of this reading, one of them with the decision
    /// written down and one without — which is the second flattening this class exists to
    /// prevent, and the half that had no comment is the half a reader would have called a bug.</para>
    /// </summary>
    public IReadOnlyList<SelectedAssetFeature> CopyableFeatures(StockVehicleRow stock)
    {
        ArgumentNullException.ThrowIfNull(stock);

        return [.. stock.Features.Select(Read).OfType<SelectedAssetFeature>()];

        SelectedAssetFeature? Read(string printed)
        {
            if (printed.StartsWith(PassengersLine, StringComparison.Ordinal)
                && int.TryParse(printed[PassengersLine.Length..], out var extra))
                return new SelectedAssetFeature("passengers") { Units = Math.Max(1, extra / 4) };

            var id = GearCatalogue.Slug(printed);
            return FindVehicleFeature(id) is null ? null : new SelectedAssetFeature(id);
        }
    }

    private const string PassengersLine = "Passengers ";

    private static IReadOnlyList<string> Keywords(string? restrictedTo, IReadOnlyList<string> requires) =>
        restrictedTo is null ? requires : [restrictedTo, .. requires];
}
