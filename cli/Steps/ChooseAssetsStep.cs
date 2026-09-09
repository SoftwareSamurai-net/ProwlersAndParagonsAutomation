using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

/// <summary>
/// Vehicles, headquarters and Gadgets — Chapter 6, pp.94-103.
///
/// <para><b>Three currencies pass through this step and only one of them is Hero Points.</b> The
/// Unique Vehicle Perk turns a Hero Point into twenty-five Vehicle Points and the Headquarters
/// Perk into three Base Points; a Gadget runs the other way and pays Hero Points out. Every
/// prompt and every line here names the currency it is in, because a bare number beside the
/// budget panel at the top of the screen would read as a Hero Point price.</para>
///
/// <para><b>Nothing here decides a cost.</b> Every figure is asked of <see cref="CostCalculator"/>
/// and every list comes off <see cref="AssetCatalogue"/> — a total added up in this file would be
/// a second calculator to disagree with the first.</para>
///
/// <para><b>The step is skippable and nearly everybody skips it.</b> Most characters own no
/// vehicle and no base, so the menu opens on a summary of nothing and one way out.</para>
/// </summary>
public sealed class ChooseAssetsStep : IWizardStep
{
    public string StepId => "choose_assets";
    public string DisplayName => "Vehicles & Bases";

    private const string Back = "Back";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(costs);

        AnsiConsole.Write(new Rule("[bold yellow]Step 4 — Vehicles & Bases[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]A vehicle is bought in Vehicle Points and a base in Base "
                             + "Points — second currencies the Perks convert Hero Points into.[/]");
        AnsiConsole.MarkupLine("[grey]A Gadget runs the other way: building one pays Hero Points "
                             + "into a pool of its own, and costs your budget nothing.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            Summarise(sheet, rules, costs, derived);

            var choices = new List<string> { "Add a vehicle", "Add a headquarters", "Build a Gadget" };

            if (sheet.Vehicles.Count > 0) choices.Add("Edit a vehicle");
            if (sheet.Headquarters.Count > 0) choices.Add("Edit a headquarters");
            if (sheet.Vehicles.Count + sheet.Headquarters.Count + sheet.Gadgets.Count > 0)
                choices.Add("Remove one");

            choices.Add("Done — finish vehicles and bases");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("Vehicles and bases:").AddChoices(choices));

            switch (action)
            {
                case "Done — finish vehicles and bases":
                    return;

                case "Add a vehicle":
                    AddVehicle(sheet, rules, costs);
                    break;

                case "Add a headquarters":
                    AddHeadquarters(sheet, rules, costs);
                    break;

                case "Build a Gadget":
                    BuildGadget(sheet, rules);
                    break;

                case "Edit a vehicle":
                    EditVehicle(sheet, rules, costs);
                    break;

                case "Edit a headquarters":
                    EditHeadquarters(sheet, rules, costs);
                    break;

                case "Remove one":
                    Remove(sheet, costs);
                    break;
            }

            AnsiConsole.WriteLine();
        }
    }

    // ── What is on the sheet ──────────────────────────────────────────────

    private static void Summarise(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        if (sheet.Vehicles.Count + sheet.Headquarters.Count
            + sheet.Gadgets.Count + sheet.CampaignAssets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing owned. Most characters own none of this.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var vehicle in sheet.Vehicles)
        {
            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(AssetFormatter.Describe(vehicle, costs))}[/]");
            AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(AssetFormatter.Characteristics(vehicle))}[/]");

            foreach (var feature in vehicle.Features)
                AnsiConsole.MarkupLine(
                    $"    [grey]- {Markup.Escape(AssetFormatter.Feature(feature, rules, onAVehicle: true))}[/]");
        }

        foreach (var headquarters in sheet.Headquarters)
        {
            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(AssetFormatter.Describe(headquarters, costs))}[/]");

            foreach (var feature in headquarters.Features)
                AnsiConsole.MarkupLine(
                    $"    [grey]- {Markup.Escape(AssetFormatter.Feature(feature, rules, onAVehicle: false))}[/]");
        }

        foreach (var gadget in sheet.Gadgets)
            AnsiConsole.MarkupLine(
                $"  [green]{Markup.Escape(AssetFormatter.Describe(gadget, costs, sheet.ImmortalityCost))}[/]");

        foreach (var contribution in sheet.CampaignAssets)
            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(AssetFormatter.Describe(contribution))}[/]");

        // Teamwork is Resolve's twin — computed for anybody, held only by a Hero — so the wizard
        // says it here rather than beside the derived stats, where a Villain's sheet would print
        // a currency the GM holds instead.
        if (derived.CalculateTeamwork(sheet) is > 0 and var teamwork)
            AnsiConsole.MarkupLine(
                $"  [grey]Teamwork: {teamwork} at the start of each issue, for a Hero.[/]");

        AnsiConsole.WriteLine();
    }

    // ── Vehicles ──────────────────────────────────────────────────────────

    private static void AddVehicle(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var name = Name("Vehicle name:");
        if (name is null) return;

        var perk = Points("Hero Points on the Unique Vehicle Perk for it:");

        var vehicle = new OwnedVehicle(name) { PerkHeroPoints = perk };

        // p.96's six machines are worked examples printed with their totals, so copying one is how
        // somebody starts. Nothing records which row it came from: after this it is their machine.
        if (AnsiConsole.Confirm("Start from one of the six stock vehicles (p.96)?", defaultValue: false))
            vehicle = CopyStock(vehicle, rules);

        sheet.Vehicles.Add(vehicle);
        EditVehicle(sheet, rules, costs, vehicle);
    }

    private static OwnedVehicle CopyStock(OwnedVehicle vehicle, RulesRepository rules)
    {
        var rows = rules.Assets.Rows.Where(r => r.Kind == AssetRowKind.StockVehicle).ToList();

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<AssetCatalogueRow>()
                .Title("Stock vehicle:")
                .UseConverter(StockLabel)
                .AddChoices(rows));

        var stock = picked.Stock!;

        return vehicle with
        {
            Body = stock.Body,
            Speed = stock.Speed,
            Control = stock.Control,
            Weapons = stock.Weapons,

            // "Rader (Sonar)" is the one printed feature line that is not a bare feature name — it
            // is the Radar Power taken through Unique Systems — so it is skipped rather than
            // guessed at, and the machine comes out under budget rather than over.
            // Which printed lines can be copied, and why "Rader (Sonar)" is not one of them, is
            // AssetCatalogue.CopyableFeatures' answer — the same one the browser's assets step
            // gets. This step used to carry its own copy of that reading, without the comment
            // the browser's copy had, so the one host where the decision looked like a bug was
            // the one nothing explained it in.
            Features = rules.Assets.CopyableFeatures(stock)
        };
    }

    /// <summary>One stock row as a line: its four ranks and what it costs in Vehicle Points.</summary>
    public static string StockLabel(AssetCatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"{row.Name} — {row.Cost} Vehicle Points · {row.Description}";
    }

    private static void EditVehicle(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<OwnedVehicle>()
                .Title("Which vehicle?")
                .UseConverter(v => AssetFormatter.Describe(v, costs))
                .AddChoices(sheet.Vehicles));

        EditVehicle(sheet, rules, costs, picked);
    }

    private static void EditVehicle(
        CharacterSheet sheet, RulesRepository rules, CostCalculator costs, OwnedVehicle vehicle)
    {
        while (true)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(AssetFormatter.Describe(vehicle, costs))}[/]");
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(AssetFormatter.Characteristics(vehicle))}[/]");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"{vehicle.Name}:")
                    .AddChoices("Set the four characteristics", "Add a feature", "Remove a feature", Back));

            switch (action)
            {
                case Back:
                    return;

                case "Set the four characteristics":
                    // p.96: all four are bought from zero, Control at two points a rank and
                    // capped at half the Speed. The prompts refuse nothing — an illegal machine is
                    // reported by the validator and never repaired here.
                    vehicle = Replace(sheet, vehicle, vehicle with
                    {
                        Body    = Points("Body (1 Vehicle Point a rank):"),
                        Speed   = Points("Speed (1 Vehicle Point a rank):"),
                        Control = AnsiConsole.Prompt(
                            new TextPrompt<int>("Control (2 Vehicle Points a rank, may be negative):")
                                .DefaultValue(vehicle.Control)),
                        Weapons = AnsiConsole.Confirm("Armed?", defaultValue: vehicle.Weapons is not null)
                            ? Points("Weapons (1 Vehicle Point a rank):")
                            : null
                    });
                    break;

                case "Add a feature":
                    if (PickFeature(rules, onAVehicle: true) is { } added)
                        vehicle = Replace(sheet, vehicle, vehicle with
                        {
                            Features = [.. vehicle.Features, added]
                        });
                    break;

                case "Remove a feature":
                    if (vehicle.Features.Count == 0) break;

                    var gone = AnsiConsole.Prompt(
                        new SelectionPrompt<SelectedAssetFeature>()
                            .Title("Which feature?")
                            .UseConverter(f => AssetFormatter.Feature(f, rules, onAVehicle: true))
                            .AddChoices(vehicle.Features));

                    vehicle = Replace(sheet, vehicle, vehicle with
                    {
                        Features = [.. vehicle.Features.Where(f => !ReferenceEquals(f, gone))]
                    });
                    break;
            }
        }
    }

    private static OwnedVehicle Replace(CharacterSheet sheet, OwnedVehicle old, OwnedVehicle updated)
    {
        var index = sheet.Vehicles.IndexOf(old);
        if (index >= 0) sheet.Vehicles[index] = updated;
        return updated;
    }

    // ── Headquarters ──────────────────────────────────────────────────────

    private static void AddHeadquarters(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var name = Name("Headquarters name:");
        if (name is null) return;

        // p.100 grants the building itself for the Perk alone, so a base with no features is not
        // an error — and is not free either.
        var headquarters = new OwnedHeadquarters(name)
        {
            PerkHeroPoints = Points("Hero Points on the Headquarters Perk for it:")
        };

        sheet.Headquarters.Add(headquarters);
        EditHeadquarters(sheet, rules, costs, headquarters);
    }

    private static void EditHeadquarters(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<OwnedHeadquarters>()
                .Title("Which headquarters?")
                .UseConverter(h => AssetFormatter.Describe(h, costs))
                .AddChoices(sheet.Headquarters));

        EditHeadquarters(sheet, rules, costs, picked);
    }

    private static void EditHeadquarters(
        CharacterSheet sheet, RulesRepository rules, CostCalculator costs, OwnedHeadquarters headquarters)
    {
        while (true)
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(AssetFormatter.Describe(headquarters, costs))}[/]");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"{headquarters.Name}:")
                    .AddChoices("Add a feature", "Remove a feature", Back));

            switch (action)
            {
                case Back:
                    return;

                case "Add a feature":
                    if (PickFeature(rules, onAVehicle: false) is { } added)
                    {
                        var index = sheet.Headquarters.IndexOf(headquarters);
                        headquarters = headquarters with { Features = [.. headquarters.Features, added] };
                        if (index >= 0) sheet.Headquarters[index] = headquarters;
                    }
                    break;

                case "Remove a feature":
                    if (headquarters.Features.Count == 0) break;

                    var gone = AnsiConsole.Prompt(
                        new SelectionPrompt<SelectedAssetFeature>()
                            .Title("Which feature?")
                            .UseConverter(f => AssetFormatter.Feature(f, rules, onAVehicle: false))
                            .AddChoices(headquarters.Features));

                    var at = sheet.Headquarters.IndexOf(headquarters);
                    headquarters = headquarters with
                    {
                        Features = [.. headquarters.Features.Where(f => !ReferenceEquals(f, gone))]
                    };
                    if (at >= 0) sheet.Headquarters[at] = headquarters;
                    break;
            }
        }
    }

    // ── Features, for either table ────────────────────────────────────────

    /// <summary>
    /// One feature off whichever table, with its grade or its count asked for at the point of
    /// choosing.
    ///
    /// <para><b>A graded feature is never left without a grade</b>, because
    /// <see cref="CostCalculator"/> throws rather than guessing one — the same answer the Gear
    /// step's custom features get.</para>
    /// </summary>
    private static SelectedAssetFeature? PickFeature(RulesRepository rules, bool onAVehicle)
    {
        var kind = onAVehicle ? AssetRowKind.VehicleFeature : AssetRowKind.BaseFeature;
        var rows = rules.Assets.Rows.Where(r => r.Kind == kind).ToList();

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<AssetCatalogueRow>()
                .Title("Feature:")
                .PageSize(15)
                .UseConverter(FeatureLabel)
                .AddChoices(rows));

        var id = picked.Id[(picked.Id.IndexOf(':', StringComparison.Ordinal) + 1)..];

        if (picked.Grades is { } grades)
        {
            var grade = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Which grade?")
                    .UseConverter(key => $"{Humanise(key)} — {Priced(grades[key], picked)}")
                    .AddChoices(grades.OrderBy(g => g.Value).Select(g => g.Key)));

            return new SelectedAssetFeature(id) { GradeKey = grade };
        }

        if (picked.CostPerUnit is not null)
            return new SelectedAssetFeature(id)
            {
                Units = AnsiConsole.Prompt(
                    new TextPrompt<int>($"How many ({picked.UnitLabel})?").DefaultValue(1))
            };

        return new SelectedAssetFeature(id);
    }

    /// <summary>
    /// One feature row as a line: its name, its price <b>with the currency</b>, and any restriction
    /// the entry prints.
    ///
    /// <para>The currency is on every line because this step also shows Hero Point figures, and a
    /// bare "10" beside a budget panel that counts Hero Points is the category error the whole
    /// chapter is careful about.</para>
    /// </summary>
    public static string FeatureLabel(AssetCatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var price = row switch
        {
            { Cost: { } flat }        => Priced(flat, row),
            { CostPerUnit: { } rate } => $"{Priced(rate, row)} per {row.UnitLabel}",
            { Grades: { } grades }    => $"{grades.Values.Min()}-{grades.Values.Max()} {Unit(row)}",
            _                         => ""
        };

        return row.RestrictedTo is null
            ? $"{row.Name} — {price}"
            : $"{row.Name} — {price} · {row.RestrictedTo} only";
    }

    private static string Unit(AssetCatalogueRow row) =>
        row.Kind == AssetRowKind.BaseFeature ? "Base Points" : "Vehicle Points";

    /// <summary>
    /// A figure with its currency, singular where the figure is one. "1 Vehicle Points" reads as a
    /// form field rather than a sentence, which is the rule the validation messages are held to and
    /// there is no reason a menu should be exempt from it.
    /// </summary>
    private static string Priced(int points, AssetCatalogueRow row) =>
        Math.Abs(points) == 1
            ? $"{points} {Unit(row)[..^1]}"
            : $"{points} {Unit(row)}";

    // ── Gadgets ───────────────────────────────────────────────────────────

    /// <summary>
    /// A Gadget, which is a name and a Complexity here.
    ///
    /// <para><b>What it spends its pool on is not asked for in the terminal</b>, and that is a
    /// deliberate stopping point rather than an oversight: pricing a Power means the whole
    /// Pro-and-Con selector under every Power of every Gadget, which is the Powers step over
    /// again. The browser's step and a payload handed to <c>build --from</c> both carry them and
    /// both price them correctly.</para>
    /// </summary>
    private static void BuildGadget(CharacterSheet sheet, RulesRepository rules)
    {
        var name = Name("Gadget name:");
        if (name is null) return;

        var minimum = rules.Assets.MinimumGadgetComplexity;

        var complexity = AnsiConsole.Prompt(
            new TextPrompt<int>($"Complexity (at least {minimum}, at most your Technology rank):")
                .DefaultValue(minimum));

        sheet.Gadgets.Add(new BuiltGadget(name) { Complexity = complexity });

        AnsiConsole.MarkupLine(
            $"  [green]Built:[/] {Markup.Escape(name)} — the build pays out "
            + $"{complexity * rules.Assets.GadgetBuild.HeroPointsGrantedMultiplier} Hero Points, "
            + "spent under the ordinary rules and not from your own budget.");
    }

    // ── Removing ──────────────────────────────────────────────────────────

    private static void Remove(CharacterSheet sheet, CostCalculator costs)
    {
        var lines = new Dictionary<string, Action>(StringComparer.Ordinal);

        foreach (var vehicle in sheet.Vehicles.ToList())
            lines[AssetFormatter.Describe(vehicle, costs)] = () => sheet.Vehicles.Remove(vehicle);

        foreach (var headquarters in sheet.Headquarters.ToList())
            lines[AssetFormatter.Describe(headquarters, costs)] = () => sheet.Headquarters.Remove(headquarters);

        foreach (var gadget in sheet.Gadgets.ToList())
            lines[AssetFormatter.Describe(gadget, costs, sheet.ImmortalityCost)] = () => sheet.Gadgets.Remove(gadget);

        if (lines.Count == 0) return;

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Remove which?")
                .AddChoices([.. lines.Keys, Back]));

        if (picked == Back) return;

        lines[picked]();
        AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(picked)}");
    }

    // ── Prompts ───────────────────────────────────────────────────────────

    /// <summary>A name, or null if the reader typed nothing. A nameless asset cannot be reported about.</summary>
    private static string? Name(string title)
    {
        var name = AnsiConsole.Prompt(new TextPrompt<string>(title).AllowEmpty()).Trim();
        return name.Length == 0 ? null : name;
    }

    private static int Points(string title) =>
        AnsiConsole.Prompt(new TextPrompt<int>(title).DefaultValue(0));

    private static string Humanise(string key) =>
        string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
