using ProwlersAndParagonsAutomation.Cli.Powers;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class BuyCharacteristicsStep : IWizardStep
{
    public string StepId => "buy_characteristics";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 2 — Buy Characteristics[/]").LeftJustified());
        AnsiConsole.WriteLine();

        BuyAbilities(sheet, rules, costs);
        BuyTalents(sheet, rules, costs);

        AnsiConsole.Write(new Rule("[bold]Powers[/]").LeftJustified());
        new PowerBrowser(rules, costs, derived).Run(sheet);

        ChooseFlaws(sheet, rules);
    }

    // ── Abilities ─────────────────────────────────────────────────────────

    private static void BuyAbilities(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        AnsiConsole.Write(new Rule("[bold]Abilities[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Each rank costs 1 HP. Ordinary human average is 2d.[/]");

        var tier = rules.GetTier(sheet.SelectedTierId!)!;

        while (true)
        {
            AnsiConsole.WriteLine();
            RenderAbilitiesTable(sheet, rules, costs);
            AnsiConsole.WriteLine();

            var abilityChoices = rules.Abilities
                .Select(a => $"{a.Name,-14} — {sheet.GetAbilityRank(a.Id)}d  ({sheet.GetAbilityRank(a.Id)} HP)")
                .Prepend("Done — move to Talents")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Adjust an ability rank (or Done):")
                    .AddChoices(abilityChoices));

            if (pick.StartsWith("Done")) break;

            var ability = rules.Abilities.First(a =>
                pick.StartsWith(a.Name));

            AdjustRank(
                label:     ability.Name,
                getId:     () => sheet.GetAbilityRank(ability.Id),
                setId:     rank => sheet.AbilityRanks[ability.Id] = rank,
                min:       1,
                max:       tier.TraitCapRank,
                packageMin: PackageFloorForAbility(sheet, rules, ability.Id));
        }
    }

    private static void BuyTalents(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        AnsiConsole.Write(new Rule("[bold]Talents[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Each rank costs 1 HP. Ordinary human average is 2d.[/]");

        var tier = rules.GetTier(sheet.SelectedTierId!)!;

        while (true)
        {
            AnsiConsole.WriteLine();
            RenderTalentsTable(sheet, rules, costs);
            AnsiConsole.WriteLine();

            var talentChoices = rules.Talents
                .Select(t => $"{t.Name,-16} — {sheet.GetTalentRank(t.Id)}d  ({sheet.GetTalentRank(t.Id)} HP)")
                .Prepend("Done — move to Powers")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Adjust a talent rank (or Done):")
                    .AddChoices(talentChoices));

            if (pick.StartsWith("Done")) break;

            var talent = rules.Talents.First(t =>
                pick.StartsWith(t.Name));

            AdjustRank(
                label:     talent.Name,
                getId:     () => sheet.GetTalentRank(talent.Id),
                setId:     rank => sheet.TalentRanks[talent.Id] = rank,
                min:       0,
                max:       tier.TraitCapRank,
                packageMin: PackageFloorForTalent(sheet, rules, talent.Id));
        }
    }

    private static void AdjustRank(string label, Func<int> getId, Action<int> setId,
        int min, int max, int packageMin)
    {
        var current = getId();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(label)}[/] — current rank: [bold]{current}d[/]");

        if (packageMin > min)
            AnsiConsole.MarkupLine($"[grey]Package floor: {packageMin}d (cannot go below)[/]");

        var effectiveMin = Math.Max(min, packageMin);

        var newRank = AnsiConsole.Prompt(
            new TextPrompt<int>($"Set rank ({effectiveMin}d – {max}d):")
                .DefaultValue(current)
                .Validate(r => r >= effectiveMin && r <= max
                    ? Spectre.Console.ValidationResult.Success()
                    : Spectre.Console.ValidationResult.Error($"Must be {effectiveMin}–{max}.")));

        setId(newRank);

        var delta = newRank - current;
        var sign  = delta >= 0 ? "+" : "";
        AnsiConsole.MarkupLine($"  [green]{Markup.Escape(label)}[/] → {newRank}d  " +
                               $"([grey]{sign}{delta}d[/])");
    }

    // ── Flaws ─────────────────────────────────────────────────────────────

    private static void ChooseFlaws(CharacterSheet sheet, RulesRepository rules)
    {
        AnsiConsole.Write(new Rule("[bold]Flaws[/]").LeftJustified());

        var flawRules = rules.CreationRules.FlawRules;
        AnsiConsole.MarkupLine($"[grey]Flaws are narrative disadvantages. " +
                               $"You must choose [bold]{flawRules.MinAtCreation}–{flawRules.MaxAtCreation}[/] " +
                               $"at creation (max ever: {flawRules.MaxEver}). " +
                               $"Each flaw beyond {flawRules.MaxAtCreation} costs {flawRules.ExtraFlawCostHp} HP.[/]");
        AnsiConsole.MarkupLine("[grey]Enter flaws as free text — there is no flaws.json yet.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            AnsiConsole.MarkupLine($"[grey]Flaws so far ({sheet.Flaws.Count}):[/] " +
                (sheet.Flaws.Count == 0
                    ? "[grey]none[/]"
                    : string.Join(", ", sheet.Flaws.Select(Markup.Escape))));

            var canFinish = sheet.Flaws.Count >= flawRules.MinAtCreation;
            var atMax     = sheet.Flaws.Count >= flawRules.MaxAtCreation;

            var menuChoices = new List<string>();
            menuChoices.Add("Add a flaw");
            if (sheet.Flaws.Count > 0) menuChoices.Add("Remove last flaw");
            if (canFinish)             menuChoices.Add("Done — accept flaws");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(atMax
                        ? "[yellow]Maximum flaws at creation reached.[/]"
                        : "Flaw menu:")
                    .AddChoices(atMax
                        ? new[] { "Remove last flaw", "Done — accept flaws" }
                        : menuChoices));

            if (action == "Done — accept flaws") break;

            if (action == "Remove last flaw")
            {
                var removed = sheet.Flaws[^1];
                sheet.Flaws.RemoveAt(sheet.Flaws.Count - 1);
                AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removed)}");
                continue;
            }

            // Add a flaw
            var flaw = AnsiConsole.Prompt(
                new TextPrompt<string>("Flaw description:")
                    .Validate(f => !string.IsNullOrWhiteSpace(f)
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Flaw cannot be empty.")));

            sheet.Flaws.Add(flaw.Trim());
            AnsiConsole.MarkupLine($"  [yellow]Flaw added:[/] {Markup.Escape(flaw.Trim())}");
        }

        AnsiConsole.MarkupLine($"[green]✓[/] {sheet.Flaws.Count} flaw(s) set.");
    }

    // ── Rendering helpers ──────────────────────────────────────────────────

    private static void RenderAbilitiesTable(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Ability")
            .AddColumn("Linked Talents")
            .AddColumn(new TableColumn("Rank").Centered())
            .AddColumn(new TableColumn("HP Cost").Centered());

        foreach (var ab in rules.Abilities)
        {
            var rank         = sheet.GetAbilityRank(ab.Id);
            var linkedTalent = rules.Talents
                .Where(t => t.LinkedAbility == ab.Id)
                .Select(t => t.Name)
                .ToList();

            table.AddRow(
                Markup.Escape(ab.Name),
                linkedTalent.Count > 0 ? string.Join(", ", linkedTalent.Select(Markup.Escape)) : "[grey]—[/]",
                rank > 0 ? $"{rank}d" : "[grey]0d[/]",
                rank.ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [grey]Total ability cost: {costs.AbilityCost(sheet)} HP[/]");
    }

    private static void RenderTalentsTable(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Talent")
            .AddColumn("Linked Ability")
            .AddColumn(new TableColumn("Rank").Centered())
            .AddColumn(new TableColumn("HP Cost").Centered());

        foreach (var ta in rules.Talents)
        {
            var rank = sheet.GetTalentRank(ta.Id);
            table.AddRow(
                Markup.Escape(ta.Name),
                Markup.Escape(ta.LinkedAbility),
                rank > 0 ? $"{rank}d" : "[grey]0d[/]",
                rank.ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [grey]Total talent cost: {costs.TalentCost(sheet)} HP[/]");
    }

    // ── Package floor helpers ──────────────────────────────────────────────

    private static int PackageFloorForAbility(CharacterSheet sheet, RulesRepository rules, string abilityId)
    {
        if (sheet.SelectedPackageId is null) return 0;
        var pkg = rules.CreationRules.OptionalPackages
            .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        return pkg?.AbilitiesRank ?? 0;
    }

    private static int PackageFloorForTalent(CharacterSheet sheet, RulesRepository rules, string talentId)
    {
        if (sheet.SelectedPackageId is null) return 0;
        var pkg = rules.CreationRules.OptionalPackages
            .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        return pkg?.TalentsRank ?? 0;
    }
}
