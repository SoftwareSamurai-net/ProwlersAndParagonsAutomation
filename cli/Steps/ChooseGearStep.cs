using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class ChooseGearStep : IWizardStep
{
    public string StepId => "choose_gear";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 3 — Choose Gear[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Mundane gear has no Hero Point cost. " +
                               "Add anything the GM considers reasonable for your character.[/]");
        AnsiConsole.MarkupLine("[grey]Leave blank and press Enter to finish.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var item = AnsiConsole.Prompt(
                new TextPrompt<string>("Gear item:")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(item)) break;

            sheet.Gear.Add(item.Trim());
            AnsiConsole.MarkupLine($"  [green]Added:[/] {Markup.Escape(item.Trim())}");
        }

        if (sheet.Gear.Count == 0)
            AnsiConsole.MarkupLine("[grey]No gear added.[/]");
        else
            AnsiConsole.MarkupLine($"[green]✓[/] {sheet.Gear.Count} gear item(s) recorded.");

        AnsiConsole.WriteLine();
    }
}
