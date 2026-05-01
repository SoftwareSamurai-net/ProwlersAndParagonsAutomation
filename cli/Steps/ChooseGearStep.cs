using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

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
        AnsiConsole.WriteLine();

        while (true)
        {
            if (sheet.Gear.Count > 0)
            {
                AnsiConsole.MarkupLine($"[grey]Current gear ({sheet.Gear.Count} item(s)):[/]");
                foreach (var g in sheet.Gear)
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(g)}");
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]No gear added yet.[/]");
                AnsiConsole.WriteLine();
            }

            var choices = new List<string> { "Add an item", "Done — finish gear" };
            if (sheet.Gear.Count > 0) choices.Insert(1, "Remove last item");
            if (sheet.Gear.Count > 0) choices.Insert(2, "Clear all gear");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Gear menu:")
                    .AddChoices(choices));

            if (action == "Done — finish gear") break;

            if (action == "Remove last item")
            {
                var removed = sheet.Gear[^1];
                sheet.Gear.RemoveAt(sheet.Gear.Count - 1);
                AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removed)}");
                continue;
            }

            if (action == "Clear all gear")
            {
                sheet.Gear.Clear();
                AnsiConsole.MarkupLine("[red]All gear cleared.[/]");
                continue;
            }

            // Add an item
            var item = AnsiConsole.Prompt(
                new TextPrompt<string>("Gear item:")
                    .Validate(v => !string.IsNullOrWhiteSpace(v)
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Item cannot be empty.")));

            sheet.Gear.Add(item.Trim());
            AnsiConsole.MarkupLine($"  [green]Added:[/] {Markup.Escape(item.Trim())}");
        }

        AnsiConsole.MarkupLine(sheet.Gear.Count == 0
            ? "[grey]No gear recorded.[/]"
            : $"[green]✓[/] {sheet.Gear.Count} gear item(s) recorded.");
        AnsiConsole.WriteLine();
    }
}
