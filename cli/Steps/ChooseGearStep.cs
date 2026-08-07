using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

/// <summary>
/// Gear. Ch.6 makes mundane gear free and explicitly untracked, so free text with no cost
/// is the right default and stays the default here. The optional extra is customising an
/// item with the Ch.6 custom features (p.92), which do cost Hero Points.
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

            var choices = new List<string> { "Add an item", "Done — finish gear" };
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
