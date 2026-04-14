using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class FinishingTouchesStep : IWizardStep
{
    public string StepId => "finishing_touches";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 5 — Finishing Touches[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Optional fields — press Enter to skip any.[/]");
        AnsiConsole.WriteLine();

        sheet.Name = AnsiConsole.Prompt(
            new TextPrompt<string>("Hero name:")
                .AllowEmpty());

        sheet.Appearance = AnsiConsole.Prompt(
            new TextPrompt<string>("Appearance / description:")
                .AllowEmpty());

        sheet.Motivation = AnsiConsole.Prompt(
            new TextPrompt<string>("Motivation / drive:")
                .AllowEmpty());

        sheet.Quote = AnsiConsole.Prompt(
            new TextPrompt<string>("Signature quote:")
                .AllowEmpty());

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Connections[/] [grey](people or groups important to this hero — blank line to finish)[/]");

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
