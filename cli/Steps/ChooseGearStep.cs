using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

/// <summary>
/// Gear. Ch.6 makes mundane gear free and explicitly untracked, so free text with no cost
/// is the right default and stays the default here. The optional extra is customising an
/// item with the Ch.6 custom features (p.92), which do cost Hero Points.
///
/// <para><b>Chapter 6's own catalogue is offered beside the free text, not instead of it.</b>
/// p.91 calls its list "examples, not a catalogue of prices", so a character may carry a letter
/// from their mother and typing one is still how that is done. What the catalogue adds is the
/// printed bonus and the printed features, which somebody typing "Battle Axe" would otherwise
/// have to look up — and an armour row's Armor rank, which is the figure that is on neither
/// page alone (p.88 for the rank, p.87 for the Gear Limit that caps it).</para>
///
/// <para><b>Nothing here costs a Hero Point and nothing here buys a Power.</b> A catalogue row
/// is free like every other piece of mundane gear, and an armour row's Armor rank is reported
/// rather than added to the Powers the character bought.</para>
/// </summary>
public sealed class ChooseGearStep : IWizardStep
{
    public string StepId => "choose_gear";
    public string DisplayName => "Choose Gear";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 3 — Choose Gear[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Mundane gear has no Hero Point cost. " +
                               "Add anything the GM considers reasonable for your character.[/]");
        AnsiConsole.MarkupLine("[grey]Custom features (Ch.6) do cost Hero Points, " +
                               "and are subject to the GM's approval.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            ShowCurrentGear(sheet, rules, costs);

            var choices = new List<string> { "Pick from the book (Ch.6)", "Add an item", "Done — finish gear" };
            if (sheet.Gear.Count > 0)
            {
                choices.Insert(1, "Customise an item");
                choices.Insert(2, "Remove last item");
                choices.Insert(3, "Clear all gear");
            }

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Gear menu:")
                    .AddChoices(choices));

            switch (action)
            {
                case "Done — finish gear":
                    Summarise(sheet, costs);
                    return;

                case "Remove last item":
                    var removed = sheet.Gear[^1];
                    sheet.Gear.RemoveAt(sheet.Gear.Count - 1);
                    AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removed.Name)}");
                    break;

                case "Clear all gear":
                    sheet.Gear.Clear();
                    AnsiConsole.MarkupLine("[red]All gear cleared.[/]");
                    break;

                case "Pick from the book (Ch.6)":
                    PickFromCatalogue(sheet, rules, derived);
                    break;

                case "Customise an item":
                    Customise(sheet, rules, costs);
                    break;

                default:
                    AddItem(sheet);
                    break;
            }
        }
    }

    private static void ShowCurrentGear(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        if (sheet.Gear.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No gear added yet.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.MarkupLine($"[grey]Current gear ({sheet.Gear.Count} item(s)):[/]");
        foreach (var g in sheet.Gear)
            AnsiConsole.MarkupLine($"  • {Markup.Escape(GearFormatter.Describe(g, rules, costs))}");
        AnsiConsole.WriteLine();
    }

    // ── Chapter 6's catalogue ─────────────────────────────────────────────

    private static void PickFromCatalogue(
        CharacterSheet sheet, RulesRepository rules, DerivedStatsCalculator derived)
    {
        var rows = rules.Catalogue.Rows;

        // One page of a 108-row list is unusable, so the era or the kind is asked first. The
        // groups are the book's own headings rather than a taxonomy invented here.
        var groups = rows
            .GroupBy(r => r.Kind == GearCatalogueKind.Item ? "Equipment (p.91)" : $"{r.Kind} — {r.Category}")
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var group = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which table?")
                .PageSize(15)
                .AddChoices([.. groups.Keys.Order(StringComparer.Ordinal), Back]));

        if (group == Back) return;

        var picked = AnsiConsole.Prompt(
            new SelectionPrompt<GearCatalogueRow>()
                .Title($"[bold]{Markup.Escape(group)}[/]:")
                .PageSize(20)
                .UseConverter(r => CatalogueLabel(r, sheet, derived, rules))
                .AddChoices(groups[group]));

        sheet.Gear.Add(new SelectedGear(picked.Name) { CatalogueId = picked.Id });

        AnsiConsole.MarkupLine(
            $"  [green]Added:[/] {Markup.Escape(CatalogueLabel(picked, sheet, derived, rules))}");
    }

    /// <summary>The way out of the catalogue without picking anything.</summary>
    private const string Back = "Back";

    /// <summary>
    /// One catalogue row as a line: its name, the columns the book prints beside it, and — for a
    /// suit of armour — the Armor rank it would grant <em>this</em> character.
    ///
    /// <para><b>The rank is the figure worth showing before the choice is made</b>, because it is
    /// the one thing the page does not print: p.88 gives the rank as Toughness plus the suit's
    /// bonus and p.87 caps the Toughness half at the Gear Limit, so which suit is worth taking
    /// depends on the wearer. A shield says what its die is for the same reason — the weapons
    /// table prints only the half you get by swinging it.</para>
    /// </summary>
    public static string CatalogueLabel(
        GearCatalogueRow row, CharacterSheet sheet, DerivedStatsCalculator derived, RulesRepository rules)
    {
        var parts = new List<string>();

        if (row.BonusDice is { } dice) parts.Add($"+{dice}{(row.Subdual ? " (s)" : "")}");
        if (row.Features.Count > 0) parts.Add(string.Join(", ", row.Features));

        if (row.Kind == GearCatalogueKind.Armor)
            parts.Add($"grants Armor {derived.ArmorRankInSuit(sheet, row.BonusDice ?? 0)}d");

        if (row.IsShield)
            parts.Add($"+{rules.Catalogue.ShieldBonusDice}d to every defence in the off hand");

        return parts.Count == 0 ? row.Name : $"{row.Name} — {string.Join(" · ", parts)}";
    }

    private static void AddItem(CharacterSheet sheet)
    {
        var item = AnsiConsole.Prompt(
            new TextPrompt<string>("Gear item:")
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? Spectre.Console.ValidationResult.Success()
                    : Spectre.Console.ValidationResult.Error("Item cannot be empty.")));

        sheet.Gear.Add(new SelectedGear(item.Trim()));
        AnsiConsole.MarkupLine($"  [green]Added:[/] {Markup.Escape(item.Trim())}");
    }

    // ── Customising ───────────────────────────────────────────────────────

    private static void Customise(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var target = AnsiConsole.Prompt(
            new SelectionPrompt<SelectedGear>()
                .Title("Which item?")
                .UseConverter(g => GearFormatter.Describe(g, rules, costs))
                .AddChoices(sheet.Gear));

        var features = new List<SelectedGearFeature>(target.Features);

        while (true)
        {
            var available = rules.GearFeatures.Where(f => features.All(s => s.FeatureId != f.Id)).ToList();

            var options = available
                .Select(f => $"{f.Name} — {PriceLabel(f)} ({f.AppliesTo})")
                .Append("Done")
                .ToList();

            var picked = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Features on [bold]{Markup.Escape(target.Name)}[/]:")
                    .PageSize(15)
                    .AddChoices(options));

            if (picked == "Done") break;

            var feature = available[options.IndexOf(picked)];
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(feature.Description)}[/]");

            features.Add(new SelectedGearFeature(feature.Id, ChooseGrade(feature)));
        }

        // Two-Fisted lets a matched pair be customised for the price of one, so the pair
        // is recorded as a single item rather than charged twice.
        var paired = target.PairedUnderTwoFisted;
        if (features.Count > 0 && sheet.HasPower("two_fisted"))
        {
            paired = AnsiConsole.Confirm(
                "Is this a matched pair customised under Two-Fisted (one price for both)?", paired);
        }

        var updated = target with { Features = features, PairedUnderTwoFisted = paired };
        sheet.Gear[sheet.Gear.IndexOf(target)] = updated;

        AnsiConsole.MarkupLine(
            $"  [green]Updated:[/] {Markup.Escape(GearFormatter.Describe(updated, rules, costs))}");
    }

    /// <summary>Ten features are a flat price; the other two are 1 or 2 HP by grade.</summary>
    private static string PriceLabel(GearFeatureModel feature) =>
        feature.CostType == "flat"
            ? $"{feature.Cost} HP"
            : $"{feature.CostRange!.Values.Min()} to {feature.CostRange.Values.Max()} HP";

    private static string? ChooseGrade(GearFeatureModel feature)
    {
        if (feature.CostType == "flat") return null;

        var grades = feature.CostRange!;
        var labels = grades.OrderBy(g => g.Value).Select(g => $"{g.Key} ({g.Value} HP)").ToList();

        var chosen = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("  Which grade?").AddChoices(labels));

        return grades.OrderBy(g => g.Value).ElementAt(labels.IndexOf(chosen)).Key;
    }

    private static void Summarise(CharacterSheet sheet, CostCalculator costs)
    {
        if (sheet.Gear.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No gear recorded.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var spent = costs.TotalGearCost(sheet);
        AnsiConsole.MarkupLine($"[green]✓[/] {sheet.Gear.Count} gear item(s) recorded" +
                               (spent > 0 ? $", {spent} HP spent on customisation." : ", none customised."));
        AnsiConsole.WriteLine();
    }
}
