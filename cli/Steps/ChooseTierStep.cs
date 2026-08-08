using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class ChooseTierStep : IWizardStep
{
    public string StepId => "choose_tier";
    public string DisplayName => "Choose Tier";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 1 — Choose Tier[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Overview table
        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Tier")
            .AddColumn(new TableColumn("Hero Points").Centered())
            .AddColumn(new TableColumn("Trait Cap").Centered())
            .AddColumn("Description");

        foreach (var t in rules.Tiers)
        {
            var note = t.NeedsReview ? "[yellow](GM discretion)[/] " : "";
            var desc = Markup.Escape(t.Description.Length > 70
                ? t.Description[..70] + "…"
                : t.Description);
            table.AddRow(
                Markup.Escape(t.Name),
                t.HeroPoints.ToString(),
                $"{t.TraitCapRank}d",
                $"{note}{desc}");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Tier selection
        var chosen = AnsiConsole.Prompt(
            new SelectionPrompt<TierModel>()
                .Title("Select a tier:")
                .UseConverter(t => $"{t.Name}  ({t.HeroPoints} HP, cap {t.TraitCapRank}d)" +
                                   (t.NeedsReview ? " [yellow]*[/]" : ""))
                .AddChoices(rules.Tiers));

        sheet.SelectedTierId = chosen.Id;
        AnsiConsole.MarkupLine($"[green]✓[/] Tier set to [bold]{Markup.Escape(chosen.Name)}[/].");
        AnsiConsole.WriteLine();

        // Optional package
        OfferPackage(sheet, rules);
    }

    private static void OfferPackage(CharacterSheet sheet, RulesRepository rules)
    {
        AnsiConsole.MarkupLine("[bold]Optional Starting Package[/]");
        AnsiConsole.MarkupLine("[grey]Packages give a flat HP cost in exchange for a floor rank " +
                               "across all abilities and/or talents.[/]");
        AnsiConsole.WriteLine();

        var pkgTable = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Package")
            .AddColumn(new TableColumn("HP Cost").Centered())
            .AddColumn(new TableColumn("Abilities").Centered())
            .AddColumn(new TableColumn("Talents").Centered())
            .AddColumn("Description");

        foreach (var pkg in rules.CreationRules.OptionalPackages)
            pkgTable.AddRow(
                Markup.Escape(pkg.Name),
                pkg.Cost.ToString(),
                $"{pkg.AbilitiesRank}d",
                $"{pkg.TalentsRank}d",
                Markup.Escape(pkg.Description));

        AnsiConsole.Write(pkgTable);
        AnsiConsole.WriteLine();

        var choices = rules.CreationRules.OptionalPackages
            .Select(p => $"{p.Name} ({p.Cost} HP)")
            .Prepend("Skip — no package")
            .ToList();

        var pkgPick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Apply a starting package?")
                .AddChoices(choices));

        if (pkgPick.StartsWith("Skip", StringComparison.Ordinal))
        {
            sheet.SelectedPackageId = null;
            return;
        }

        var selected = rules.CreationRules.OptionalPackages
            .First(p => pkgPick.StartsWith(p.Name, StringComparison.Ordinal));

        sheet.SelectedPackageId = selected.Id;

        // Apply floor ranks
        foreach (var ab in rules.Abilities)
            if (sheet.GetAbilityRank(ab.Id) < selected.AbilitiesRank)
                sheet.AbilityRanks[ab.Id] = selected.AbilitiesRank;

        foreach (var ta in rules.Talents)
            if (sheet.GetTalentRank(ta.Id) < selected.TalentsRank)
                sheet.TalentRanks[ta.Id] = selected.TalentsRank;

        AnsiConsole.MarkupLine($"[green]✓[/] Package [bold]{Markup.Escape(selected.Name)}[/] applied " +
                               $"({selected.Cost} HP). Abilities ≥ {selected.AbilitiesRank}d, " +
                               $"Talents ≥ {selected.TalentsRank}d.");
    }
}
