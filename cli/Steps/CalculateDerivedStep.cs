using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class CalculateDerivedStep : IWizardStep
{
    public string StepId => "calculate_derived";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 4 — Derived Stats[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]These are calculated automatically from your trait ranks.[/]");
        AnsiConsole.WriteLine();

        var edge   = derived.CalculateEdge(sheet);
        var health = derived.CalculateHealth(sheet);

        var table = new Table()
            .BorderColor(Color.Gold1)
            .AddColumn("Stat")
            .AddColumn(new TableColumn("Value").Centered())
            .AddColumn("Formula");

        table.AddRow(
            "[bold]Edge[/]",
            $"[bold green]{edge}[/]",
            "Perception + max(Agility, Intellect) + Danger Sense + Lightning Reflexes bonuses");

        table.AddRow(
            "[bold]Health[/]",
            $"[bold green]{health}[/]",
            "max( ⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉ )");

        AnsiConsole.Write(table);

        if (sheet.HasPower("danger_sense"))
        {
            var ds = sheet.GetPower("danger_sense")!;
            AnsiConsole.MarkupLine($"[grey]  Danger Sense effective rank: " +
                                   $"{derived.GetEffectiveRank(ds, sheet)}d " +
                                   $"(adds to Edge)[/]");
        }

        if (sheet.HasPower("lightning_reflexes"))
        {
            var lr = sheet.GetPower("lightning_reflexes")!;
            AnsiConsole.MarkupLine($"[yellow]  ⚠ Lightning Reflexes: +{lr.PurchasedRanks * 2} to Edge " +
                                   $"(formula is needs_review — verify with GM)[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press Enter to continue...[/]");
        Console.ReadLine();
    }
}
