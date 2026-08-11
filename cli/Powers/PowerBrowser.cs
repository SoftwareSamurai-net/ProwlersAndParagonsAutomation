using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;
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
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(PowerFormatter.StatLine(power))}[/]");
        if (power.SourceRef is not null)
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(power.SourceRef)}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Markup.Escape(power.Description));
        if (power.Notes is not null)
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(power.Notes)}[/]");

        if (power.NeedsReview)
            AnsiConsole.MarkupLine("[yellow]⚠ Mechanics not fully verified against the rulebook — " +
                                   "verify with GM.[/]");

        AnsiConsole.WriteLine();

        var tier = _rules.GetTier(sheet.SelectedTierId!)!;

        // Powers whose baseline comes from a Trait the player picks need that first:
        // it drives both the baseline rank and, for Boost, the cost per rank.
        var baselineTraitId = PromptBaselineTrait(power, sheet);

        // Variable-cost powers need their variant before any cost can be worked out.
        var variantKey = PromptCostVariant(power);

        var probe    = new SelectedPower(power.Id, 0) { BaselineTraitId = baselineTraitId };
        var baseline = _derived.GetBaselineRank(power, sheet, probe);

        if (baseline > 0)
            AnsiConsole.MarkupLine($"[grey]Baseline rank (free): [bold]{baseline}d[/][/]");

        var purchasedRanks = PromptPurchasedRanks(power, tier.TraitCapRank, baseline);
        var units          = PromptUnits(power);

        // Pros and cons
        var selector = new ProConSelector(_rules);
        var pros = selector.SelectPros(power);
        var cons = selector.SelectCons(power);

        var sourceId = PromptSource(power);

        var selection = new SelectedPower(
            power.Id,
            purchasedRanks,
            pros.AsReadOnly(),
            cons.AsReadOnly())
        {
            CostVariantKey  = variantKey,
            Units           = units,
            BaselineTraitId = baselineTraitId,
            SourceId        = sourceId
        };

        var cost          = _costs.PowerCost(selection);
        var effectiveRank = _derived.GetEffectiveRank(selection, sheet);

        // Replace any existing entry for this power
        sheet.SelectedPowers.RemoveAll(sp => sp.PowerId == power.Id);
        sheet.SelectedPowers.Add(selection);

        AnsiConsole.WriteLine();
        var rankText = effectiveRank > 0 ? $"effective {effectiveRank}d" : "no rank";
        AnsiConsole.MarkupLine($"[green]✓ Added:[/] [bold]{Markup.Escape(power.Name)}[/] — " +
                               $"{rankText} — [bold]{cost} HP[/]");
    }

    /// <summary>
    /// Which of the six Sources the Power comes from (Ch.2, p.16). Costs nothing, but the
    /// sheet groups Powers by it, and a Power with no rank of its own takes its default
    /// rank from the Source's Ability — so the prompt says so when that applies.
    /// </summary>
    private string? PromptSource(PowerModel power)
    {
        AnsiConsole.WriteLine();

        if (power.RankType is "default" or "special")
            AnsiConsole.MarkupLine(
                "[grey]This Power has no rank of its own, so its Source decides which Ability " +
                "stands in when another Power acts on it.[/]");

        var choices = _rules.Sources
            .Select(s => $"{s.Name} — {s.Description} (default rank: {s.DefaultRankAbility})")
            .Append("Not sure yet")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Source of [bold]{Markup.Escape(power.Name)}[/]:")
                .PageSize(8)
                .AddChoices(choices));

        return pick == "Not sure yet" ? null : _rules.Sources[choices.IndexOf(pick)].Id;
    }

    /// <summary>
    /// Ranks are only purchasable for Powers priced per rank. A Power with no rank, or
    /// one bought for a flat or per-unit price, gets none.
    /// </summary>
    private static int PromptPurchasedRanks(PowerModel power, int traitCap, int baseline)
    {
        if (power.MaxRank == 0)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(PowerFormatter.RankType(power))} — " +
                                   "no ranks are purchased for this Power.[/]");
            return 0;
        }

        var maxPurchasable = Math.Max(0, traitCap - baseline);
        if (maxPurchasable == 0)
        {
            AnsiConsole.MarkupLine("[grey]Baseline already reaches the trait cap — 0 purchased ranks.[/]");
            return 0;
        }

        return AnsiConsole.Prompt(
            new TextPrompt<int>($"Purchased ranks (0–{maxPurchasable}):")
                .DefaultValue(0)
                .Validate(r => r >= 0 && r <= maxPurchasable
                    ? Spectre.Console.ValidationResult.Success()
                    : Spectre.Console.ValidationResult.Error($"Must be 0–{maxPurchasable}.")));
    }

    /// <summary>Quantity for per-unit Powers: immunities, Resolve, power levels.</summary>
    private static int PromptUnits(PowerModel power)
    {
        if (power.CostType != "per_unit") return 1;

        var label = power.CostUnitLabel ?? "unit";
        return AnsiConsole.Prompt(
            new TextPrompt<int>($"How many {Markup.Escape(label)}(s)? " +
                                $"({power.CostPerUnit} HP each):")
                .DefaultValue(1)
                .Validate(u => u >= 1
                    ? Spectre.Console.ValidationResult.Success()
                    : Spectre.Console.ValidationResult.Error("Must be at least 1.")));
    }

    private static string? PromptCostVariant(PowerModel power)
    {
        if (power.CostType is not ("per_rank_variable" or "flat_variable")) return null;
        if (power.CostVariants is null or { Count: 0 }) return null;

        var unit = power.CostType == "per_rank_variable" ? "HP per rank" : "HP";
        var byLabel = power.CostVariants.ToDictionary(
            v => $"{v.Key.Replace('_', ' ')} — {v.Value} {unit}",
            v => v.Key);

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("This Power's cost varies. Which version?")
                .AddChoices(byLabel.Keys));

        return byLabel[pick];
    }

    /// <summary>
    /// Boost and Expertise take their baseline rank from a Trait the player nominates.
    /// </summary>
    private string? PromptBaselineTrait(PowerModel power, CharacterSheet sheet)
    {
        if (power.Prerequisite?.Relationship != "baseline_selected_trait") return null;

        // Only Traits the character actually has can serve as a baseline.
        var options = sheet.AbilityRanks.Where(a => a.Value > 0)
            .Select(a => (Id: a.Key, Rank: a.Value, Kind: "Ability"))
            .Concat(sheet.TalentRanks.Where(t => t.Value > 0)
                .Select(t => (Id: t.Key, Rank: t.Value, Kind: "Talent")))
            .ToList();

        // Boost can also raise another Power; Expertise is limited to Abilities and Talents.
        if (power.Id == "boost")
        {
            options.AddRange(sheet.SelectedPowers
                .Where(sp => sp.PowerId != power.Id)
                .Select(sp => (Id: sp.PowerId,
                               Rank: _derived.GetEffectiveRank(sp, sheet),
                               Kind: "Power")));
        }

        if (options.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ This Power's baseline comes from a Trait you nominate, " +
                                   "but you have no Traits yet. Buy abilities or talents first.[/]");
            return null;
        }

        var byLabel = options.ToDictionary(
            o => $"{o.Kind}: {o.Id.Replace('_', ' ')} ({o.Rank}d)",
            o => o.Id);

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"{Markup.Escape(power.Name)} takes its baseline rank from which Trait?")
                .AddChoices(byLabel.Keys));

        return byLabel[pick];
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
            var baseline = power is null ? 0 : _derived.GetBaselineRank(power, sheet, sp);
            var effective = power is null ? 0 : _derived.GetEffectiveRank(sp, sheet);
            var cost     = _costs.PowerCost(sp);
            var review   = power?.NeedsReview == true ? " [yellow]*[/]" : "";

            table.AddRow(
                $"{Markup.Escape(name)}{review}",
                baseline > 0 ? $"{baseline}d" : "—",
                power?.MaxRank == 0 ? "—" : $"{sp.PurchasedRanks}d",
                effective > 0 ? $"[bold]{effective}d[/]" : "[grey]no rank[/]",
                sp.Pros.Count > 0 ? string.Join(", ", sp.Pros.Select(p => Markup.Escape(p.Id))) : "[grey]—[/]",
                sp.Cons.Count > 0 ? string.Join(", ", sp.Cons.Select(c => Markup.Escape(c.Id))) : "[grey]—[/]",
                $"[bold]{cost}[/]");
        }

        AnsiConsole.Write(table);
    }

    private static string PowerLabel(PowerModel p) =>
        $"{p.Name}{(p.NeedsReview ? " *" : "")}";
}
