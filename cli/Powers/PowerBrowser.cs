using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Powers;

public sealed class PowerBrowser
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;

    public PowerBrowser(RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        _rules   = rules;
        _costs   = costs;
        _derived = derived;
    }

    public void Run(CharacterSheet sheet)
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            RenderPowerSummary(sheet);
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Powers menu[/]")
                    .AddChoices(
                        "Browse by category",
                        "Search by name",
                        "Remove a power",
                        "Done — finish powers"));

            switch (action)
            {
                case "Done — finish powers":
                    return;
                case "Remove a power":
                    RemovePower(sheet);
                    break;
                case "Browse by category":
                    var browsed = BrowseByCategory();
                    if (browsed is not null) ConfigurePower(sheet, browsed);
                    break;
                case "Search by name":
                    var found = SearchPower();
                    if (found is not null) ConfigurePower(sheet, found);
                    break;
            }
        }
    }

    // ── Browse ────────────────────────────────────────────────────────────

    private PowerModel? BrowseByCategory()
    {
        var categories = _rules.Powers
            .Select(p => p.Category)
            .Distinct()
            .Order()
            .Prepend("-- Back --")
            .ToList();

        var cat = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a category:")
                .AddChoices(categories));

        if (cat == "-- Back --") return null;

        var inCategory = _rules.Powers
            .Where(p => p.Category == cat)
            .OrderBy(p => p.Name)
            .ToList();

        var choices = inCategory
            .Select(PowerLabel)
            .Prepend("-- Back --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Powers in [bold]{Markup.Escape(cat)}[/]:")
                .EnableSearch()
                .AddChoices(choices));

        if (pick == "-- Back --") return null;

        return inCategory.First(p => PowerLabel(p) == pick);
    }

    private PowerModel? SearchPower()
    {
        var query = AnsiConsole.Prompt(new TextPrompt<string>("Search (name or tag):"));

        var matches = _rules.Powers
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Name)
            .ToList();

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No matches.[/]");
            return null;
        }

        var choices = matches.Select(PowerLabel).Prepend("-- Back --").ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Results for '[italic]{Markup.Escape(query)}[/]':")
                .AddChoices(choices));

        if (pick == "-- Back --") return null;

        return matches.First(p => PowerLabel(p) == pick);
    }

    // ── Configure ─────────────────────────────────────────────────────────

    private void ConfigurePower(CharacterSheet sheet, PowerModel power)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(power.Name)}[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Category:[/] {Markup.Escape(power.Category)}");
        AnsiConsole.MarkupLine(Markup.Escape(power.Description));

        if (power.NeedsReview)
            AnsiConsole.MarkupLine("[yellow]⚠ This power is flagged needs_review — verify with GM.[/]");

        AnsiConsole.WriteLine();

        var tier     = _rules.GetTier(sheet.SelectedTierId!)!;
        var baseline = _derived.GetBaselineRank(power, sheet);

        if (baseline > 0)
            AnsiConsole.MarkupLine($"[grey]Baseline rank (free from ability): [bold]{baseline}d[/][/]");

        var maxPurchasable = Math.Max(0,
            Math.Min(power.MaxRank ?? tier.TraitCapRank, tier.TraitCapRank) - baseline);

        int purchasedRanks;
        if (maxPurchasable == 0)
        {
            AnsiConsole.MarkupLine("[grey]Effective rank fully covered by baseline — 0 purchased ranks.[/]");
            purchasedRanks = 0;
        }
        else
        {
            purchasedRanks = AnsiConsole.Prompt(
                new TextPrompt<int>($"Purchased ranks (0–{maxPurchasable}):")
                    .DefaultValue(0)
                    .Validate(r => r >= 0 && r <= maxPurchasable
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error($"Must be 0–{maxPurchasable}.")));
        }

        var effectiveRank = baseline + purchasedRanks;
        AnsiConsole.MarkupLine($"[grey]Effective rank: [bold]{effectiveRank}d[/] " +
                               $"({baseline}d baseline + {purchasedRanks}d purchased)[/]");

        // Pros and cons
        var selector = new ProConSelector(_rules);
        var pros = selector.SelectPros(power);
        var cons = selector.SelectCons(power);

        var selection = new SelectedPower(
            power.Id,
            purchasedRanks,
            pros.AsReadOnly(),
            cons.AsReadOnly());

        var cost = _costs.PowerCost(selection);

        // Replace any existing entry for this power
        sheet.SelectedPowers.RemoveAll(sp => sp.PowerId == power.Id);
        sheet.SelectedPowers.Add(selection);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Added:[/] [bold]{Markup.Escape(power.Name)}[/] — " +
                               $"effective {effectiveRank}d — [bold]{cost} HP[/]");
    }

    // ── Remove ────────────────────────────────────────────────────────────

    private void RemovePower(CharacterSheet sheet)
    {
        if (sheet.SelectedPowers.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No powers to remove.[/]");
            return;
        }

        var choices = sheet.SelectedPowers
            .Select(sp =>
            {
                var power = _rules.GetPower(sp.PowerId);
                return power is null ? sp.PowerId : $"{power.Name} (cost: {_costs.PowerCost(sp)} HP)";
            })
            .Prepend("-- Cancel --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Remove which power?")
                .AddChoices(choices));

        if (pick == "-- Cancel --") return;

        var index = choices.IndexOf(pick) - 1; // -1 for the Cancel prepend
        var removed = sheet.SelectedPowers[index];
        sheet.SelectedPowers.RemoveAt(index);

        AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removed.PowerId)}");
    }

    // ── Rendering ─────────────────────────────────────────────────────────

    private void RenderPowerSummary(CharacterSheet sheet)
    {
        if (sheet.SelectedPowers.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No powers selected yet.[/]");
            return;
        }

        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Power")
            .AddColumn(new TableColumn("Baseline").Centered())
            .AddColumn(new TableColumn("Purchased").Centered())
            .AddColumn(new TableColumn("Effective").Centered())
            .AddColumn("Pros")
            .AddColumn("Cons")
            .AddColumn(new TableColumn("HP Cost").Centered());

        foreach (var sp in sheet.SelectedPowers)
        {
            var power    = _rules.GetPower(sp.PowerId);
            var name     = power is null ? sp.PowerId : power.Name;
            var baseline = power is null ? 0 : _derived.GetBaselineRank(power, sheet);
            var effective = baseline + sp.PurchasedRanks;
            var cost     = _costs.PowerCost(sp);
            var review   = power?.NeedsReview == true ? " [yellow]*[/]" : "";

            table.AddRow(
                $"{Markup.Escape(name)}{review}",
                baseline > 0 ? $"{baseline}d" : "—",
                $"{sp.PurchasedRanks}d",
                $"[bold]{effective}d[/]",
                sp.Pros.Count > 0 ? string.Join(", ", sp.Pros.Select(p => Markup.Escape(p.Id))) : "[grey]—[/]",
                sp.Cons.Count > 0 ? string.Join(", ", sp.Cons.Select(c => Markup.Escape(c.Id))) : "[grey]—[/]",
                $"[bold]{cost}[/]");
        }

        AnsiConsole.Write(table);
    }

    private static string PowerLabel(PowerModel p) =>
        $"{p.Name}{(p.NeedsReview ? " *" : "")}";
}
