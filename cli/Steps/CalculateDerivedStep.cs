using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class CalculateDerivedStep : IWizardStep
{
    public string StepId => "calculate_derived";
    public string DisplayName => "Derived Stats";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 4 — Derived Stats[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]These are calculated automatically from your trait ranks.[/]");
        AnsiConsole.WriteLine();

        var edge    = derived.CalculateEdge(sheet);
        var health  = derived.CalculateHealth(sheet);
        var resolve = derived.CalculateResolve(sheet);

        var table = new Table()
            .BorderColor(Color.Gold1)
            .AddColumn("Stat")
            .AddColumn(new TableColumn("Value").Centered())
            .AddColumn("Formula");

        table.AddRow(
            "[bold]Edge[/]",
            $"[bold green]{edge}[/]",
            "(Danger Sense or Perception) + max(Agility, Intellect) + Lightning Reflexes, "
            + "at least Super Speed × 3");

        table.AddRow(
            "[bold]Health[/]",
            $"[bold green]{health}[/]",
            "max( ⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉ )");

        table.AddRow(
            "[bold]Resolve[/]",
            $"[bold green]{resolve}[/]",
            "max(0, (TraitCap − highestRank) × 2) + Determination + Condition/Plot Hook flaws");

        AnsiConsole.Write(table);

        if (sheet.GetPower("danger_sense") is { } ds)
            AnsiConsole.MarkupLine($"[grey]  Danger Sense {derived.GetEffectiveRank(ds, sheet)}d " +
                                   "stands in for Perception when working out Edge.[/]");

        if (sheet.HasPower("lightning_reflexes"))
            AnsiConsole.MarkupLine($"[grey]  Lightning Reflexes: " +
                                   $"+{DerivedStatsCalculator.LightningReflexesEdgeBonus} to Edge (flat).[/]");

        if (sheet.GetPower("super_speed") is { } ss)
            AnsiConsole.MarkupLine($"[grey]  Super Speed {derived.GetEffectiveRank(ss, sheet)}d " +
                                   $"sets Edge to at least {derived.GetEffectiveRank(ss, sheet) * 3}.[/]");

        if (sheet.GetPower("determination") is { } det)
            AnsiConsole.MarkupLine($"[grey]  Determination: +{det.Units} to Resolve " +
                                   $"({det.Units * DerivedStatsCalculator.DeterminationHpPerResolve} HP " +
                                   $"at {DerivedStatsCalculator.DeterminationHpPerResolve} HP per Resolve).[/]");

        AnsiConsole.WriteLine();
    }
}
