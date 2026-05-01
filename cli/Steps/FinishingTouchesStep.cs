using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class FinishingTouchesStep : IWizardStep
{
    public string StepId => "finishing_touches";
    public string DisplayName => "Finishing Touches";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 5 — Finishing Touches[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Optional fields — press Enter to keep the existing value or skip.[/]");
        AnsiConsole.WriteLine();

        sheet.Name = AnsiConsole.Prompt(
            new TextPrompt<string>("Hero name:")
                .DefaultValue(sheet.Name)
                .AllowEmpty());

        sheet.Appearance = AnsiConsole.Prompt(
            new TextPrompt<string>("Appearance / description:")
                .DefaultValue(sheet.Appearance)
                .AllowEmpty());

        sheet.Motivation = AnsiConsole.Prompt(
            new TextPrompt<string>("Motivation / drive:")
                .DefaultValue(sheet.Motivation)
                .AllowEmpty());

        sheet.Quote = AnsiConsole.Prompt(
            new TextPrompt<string>("Signature quote:")
                .DefaultValue(sheet.Quote)
                .AllowEmpty());

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Connections[/] [grey](people or groups important to this hero)[/]");

        if (sheet.Connections.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Existing connections ({sheet.Connections.Count}):[/]");
            foreach (var c in sheet.Connections)
                AnsiConsole.MarkupLine($"  • {Markup.Escape(c)}");

            var keep = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Connections:")
                    .AddChoices("Keep existing connections", "Clear and re-enter"));

            if (keep == "Clear and re-enter")
                sheet.Connections.Clear();
        }

        if (sheet.Connections.Count == 0)
            AnsiConsole.MarkupLine("[grey]Enter connections one per line — blank line to finish.[/]");
        else
            AnsiConsole.MarkupLine("[grey]Add more connections — blank line to finish.[/]");

        while (true)
        {
            var conn = AnsiConsole.Prompt(
                new TextPrompt<string>("Connection:")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(conn)) break;
            sheet.Connections.Add(conn.Trim());
            AnsiConsole.MarkupLine($"  [green]Added:[/] {Markup.Escape(conn.Trim())}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓[/] Finishing touches saved.");
    }
}
