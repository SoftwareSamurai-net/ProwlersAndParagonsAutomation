using ProwlersAndParagonsAutomation.Cli.Powers;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class BuyCharacteristicsStep : IWizardStep
{
    public string StepId => "buy_characteristics";
    public string DisplayName => "Buy Characteristics";

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 2 — Buy Characteristics[/]").LeftJustified());
        AnsiConsole.WriteLine();

        BuyAbilities(sheet, rules, costs);
        BuyTalents(sheet, rules, costs);

        AnsiConsole.Write(new Rule("[bold]Powers[/]").LeftJustified());
        new PowerBrowser(rules, costs, derived).Run(sheet);

        ChoosePerks(sheet, rules, costs);
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
                .Prepend(SourceMenuEntry)
                .Prepend("Done — move to Talents")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Adjust an ability rank (or Done):")
                    .AddChoices(abilityChoices));

            if (pick.StartsWith("Done", StringComparison.Ordinal)) break;

            if (pick == SourceMenuEntry)
            {
                ChooseTraitSource(rules,
                    rules.Abilities.Select(a => (a.Id, a.Name)).ToList(),
                    sheet.AbilitySources, SourceGrouping.DefaultAbilitySourceId, "ability");
                continue;
            }

            var ability = rules.Abilities.First(a =>
                pick.StartsWith(a.Name, StringComparison.Ordinal));

            AdjustRank(
                label:     ability.Name,
                getId:     () => sheet.GetAbilityRank(ability.Id),
                setId:     rank => sheet.AbilityRanks[ability.Id] = rank,
                min:       1,
                max:       Cap(sheet, tier),
                packageMin: PackageFloorForAbility(sheet, rules));
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
                .Prepend(SourceMenuEntry)
                .Prepend("Done — move to Powers")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Adjust a talent rank (or Done):")
                    .AddChoices(talentChoices));

            if (pick.StartsWith("Done", StringComparison.Ordinal)) break;

            if (pick == SourceMenuEntry)
            {
                ChooseTraitSource(rules,
                    rules.Talents.Select(t => (t.Id, t.Name)).ToList(),
                    sheet.TalentSources, SourceGrouping.DefaultTalentSourceId, "talent");
                continue;
            }

            var talent = rules.Talents.First(t =>
                pick.StartsWith(t.Name, StringComparison.Ordinal));

            AdjustRank(
                label:     talent.Name,
                getId:     () => sheet.GetTalentRank(talent.Id),
                setId:     rank => sheet.TalentRanks[talent.Id] = rank,
                min:       0,
                max:       Cap(sheet, tier),
                packageMin: PackageFloorForTalent(sheet, rules));
        }
    }

    /// <summary>
    /// The ceiling this character is built to: its own house cap where it has one, and the tier's
    /// otherwise.
    ///
    /// <para><b>The same figure the validator judges by and Resolve is measured from</b>, read
    /// through <c>DerivedStatsCalculator.EffectiveTraitCap</c> rather than spelled out again —
    /// a rank field bounded by one number and judged against another is how the two disagree.
    /// The wizard has no step that sets a house cap; a character built in the browser or handed
    /// to <c>build --from</c> can arrive carrying one.</para>
    /// </summary>
    private static int Cap(CharacterSheet sheet, TierModel tier) =>
        DerivedStatsCalculator.EffectiveTraitCap(sheet, tier) ?? tier.TraitCapRank;

    // ── Sources on Abilities and Talents ──────────────────────────────────

    private const string SourceMenuEntry = "Sources — say what these Traits are";

    /// <summary>
    /// Records the Source of one Ability or Talent (Ch.2, p.16). A Source costs nothing and
    /// changes no rank; it says what the Trait is meant to be, and a published sheet prints
    /// the ones that deviate from the default beside the Powers from the same Source.
    ///
    /// <para>Choosing the default removes the entry rather than storing it. The two are the
    /// same Source but not the same statement — a sheet prints exceptions, so a Trait held
    /// explicitly at its own default would print a line that says nothing.</para>
    ///
    /// <para><b>It loops until Done</b>, like every other menu in this step. Stronghold's
    /// four armoured Abilities are the case this exists for, and returning to the rank table
    /// after each one reprinted the whole table three times for no reason — the browser's
    /// picker shows all twelve at once.</para>
    /// </summary>
    private static void ChooseTraitSource(
        RulesRepository rules,
        IReadOnlyList<(string Id, string Name)> traits,
        Dictionary<string, string> sources,
        string defaultSourceId,
        string traitKind)
    {
        var defaultName = rules.GetSource(defaultSourceId)?.Name ?? defaultSourceId;

        AnsiConsole.MarkupLine(
            $"[grey]Left alone, every {traitKind} is {Markup.Escape(defaultName)}, which the " +
            "rulebook assumes and a sheet leaves unprinted. Set one only where it differs.[/]");

        // The default is offered once, as the first entry. Listing it again below would give
        // seven choices for six Sources, and the two would not behave the same — the marked
        // one removes the entry, the bare one would store it.
        var sourceChoices = rules.Sources
            .Where(s => s.Id != defaultSourceId)
            .Select(s => s.Name)
            .Prepend($"{defaultName} (default)")
            .ToList();

        while (true)
        {
            var labels = traits
                .Select(t => $"{t.Name} — {SourceLabel(rules, sources, t.Id, defaultName)}")
                .Prepend(DoneEntry)
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title($"Which {traitKind}?").AddChoices(labels));

            if (pick == DoneEntry) return;

            // FirstOrDefault, not First: this matches a menu label by prefix, so a label that
            // is neither Done nor a Trait would otherwise throw and take the whole wizard —
            // and the character — down rather than simply not matching.
            if (traits.FirstOrDefault(t => pick.StartsWith(t.Name, StringComparison.Ordinal))
                is not { Name.Length: > 0 } trait)
                continue;

            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Source for {Markup.Escape(trait.Name)}:")
                    .AddChoices(sourceChoices));

            if (rules.Sources.FirstOrDefault(s => s.Name == chosen) is { } source)
                sources[trait.Id] = source.Id;
            else
                sources.Remove(trait.Id);   // the "(default)" entry is the only other choice

            AnsiConsole.MarkupLine($"  [green]{Markup.Escape(trait.Name)}[/] → " +
                                   $"{Markup.Escape(SourceLabel(rules, sources, trait.Id, defaultName))}");
        }
    }

    private const string DoneEntry = "Done";

    private static string SourceLabel(RulesRepository rules,
        Dictionary<string, string> sources, string traitId, string defaultName) =>
        sources.TryGetValue(traitId, out var id)
            ? rules.GetSource(id)?.Name ?? id
            : $"{defaultName} (default)";

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

    // ── Perks ─────────────────────────────────────────────────────────────

    private static void ChoosePerks(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        AnsiConsole.Write(new Rule("[bold]Perks[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Perks are social advantages from the world around you. " +
                               "They have no ranks and don't take Pros or Cons. " +
                               "Perks are optional — skip if you don't want any.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            RenderPerkSummary(sheet, rules, costs);
            AnsiConsole.WriteLine();

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Perk menu:")
                    .AddChoices(BuildPerkMenuChoices(sheet)));

            if (action == "Done — finish perks") break;

            if (action == "Remove a perk")
            {
                RemovePerk(sheet, rules, costs);
                continue;
            }

            AddPerk(sheet, rules, costs);
        }

        var total = costs.TotalPerksCost(sheet);
        AnsiConsole.MarkupLine($"[green]✓[/] {sheet.Perks.Count} perk(s) selected — {total} HP.");
    }

    private static IEnumerable<string> BuildPerkMenuChoices(CharacterSheet sheet)
    {
        yield return "Add a perk";
        if (sheet.Perks.Count > 0) yield return "Remove a perk";
        yield return "Done — finish perks";
    }

    private static void AddPerk(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        var selectedIds = sheet.Perks.Select(p => p.PerkId).ToHashSet();
        var available   = rules.Perks
            .Where(p => !selectedIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToList();

        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]All perks already selected.[/]");
            return;
        }

        var choices = available
            .Select(PerkLabel)
            .Prepend("-- Back --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose a perk:")
                .EnableSearch()
                .AddChoices(choices));

        if (pick == "-- Back --") return;

        var perk = available[choices.IndexOf(pick) - 1];

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(perk.Name)}[/]").LeftJustified());
        AnsiConsole.MarkupLine(Markup.Escape(perk.Description));
        AnsiConsole.WriteLine();

        int units = 1;
        if (perk.CostType == "per_unit")
        {
            var unitLabel = perk.UnitLabel ?? "unit";
            AnsiConsole.MarkupLine($"[grey]Cost: {perk.CostPerUnit} HP per {Markup.Escape(unitLabel)}[/]");
            units = AnsiConsole.Prompt(
                new TextPrompt<int>($"How many ({Markup.Escape(unitLabel)}s)?")
                    .DefaultValue(1)
                    .Validate(u => u >= 1
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Must be at least 1.")));
        }

        string? narrativeDetail = null;
        if (perk.NarrativeConstraint is not null)
        {
            AnsiConsole.MarkupLine($"[yellow]Required detail:[/] {Markup.Escape(perk.NarrativeConstraint)}");
            narrativeDetail = AnsiConsole.Prompt(
                new TextPrompt<string>("Your answer:")
                    .Validate(v => !string.IsNullOrWhiteSpace(v)
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Cannot be empty.")));
        }

        var selection = new SelectedPerk(perk.Id, units, narrativeDetail?.Trim());
        sheet.Perks.Add(selection);

        var cost = costs.PerkCost(selection);
        AnsiConsole.MarkupLine($"  [green]Added:[/] [bold]{Markup.Escape(perk.Name)}[/]" +
            (units > 1 ? $" ×{units}" : "") +
            $" — [bold]{cost} HP[/]");
    }

    private static void RemovePerk(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        if (sheet.Perks.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No perks to remove.[/]");
            return;
        }

        var choices = sheet.Perks
            .Select(sp =>
            {
                var perk = rules.GetPerk(sp.PerkId);
                var name = perk?.Name ?? sp.PerkId;
                var cost = costs.PerkCost(sp);
                return sp.Units > 1
                    ? $"{name} ×{sp.Units} — {cost} HP"
                    : $"{name} — {cost} HP";
            })
            .Prepend("-- Cancel --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Remove which perk?")
                .AddChoices(choices));

        if (pick == "-- Cancel --") return;

        var index   = choices.IndexOf(pick) - 1;
        var removed = sheet.Perks[index];
        sheet.Perks.RemoveAt(index);

        var removedName = rules.GetPerk(removed.PerkId)?.Name ?? removed.PerkId;
        AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removedName)}");
    }

    private static void RenderPerkSummary(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        if (sheet.Perks.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No perks selected yet.[/]");
            return;
        }

        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Perk")
            .AddColumn(new TableColumn("Units").Centered())
            .AddColumn(new TableColumn("HP Cost").Centered())
            .AddColumn("Detail");

        foreach (var sp in sheet.Perks)
        {
            var perk = rules.GetPerk(sp.PerkId);
            var cost = costs.PerkCost(sp);
            table.AddRow(
                Markup.Escape(perk?.Name ?? sp.PerkId),
                sp.Units > 1 ? sp.Units.ToString() : "[grey]—[/]",
                $"[bold]{cost}[/]",
                sp.NarrativeDetail is not null ? Markup.Escape(sp.NarrativeDetail) : "[grey]—[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [grey]Total perk cost: {costs.TotalPerksCost(sheet)} HP[/]");
    }

    private static string PerkLabel(PerkModel p)
    {
        var cost = p.CostType == "flat"
            ? $"{p.Cost} HP"
            : $"{p.CostPerUnit} HP/{p.UnitLabel ?? "unit"}";
        return $"{p.Name}  [{cost}]";
    }

    // ── Flaws ─────────────────────────────────────────────────────────────

    private static void ChooseFlaws(CharacterSheet sheet, RulesRepository rules)
    {
        AnsiConsole.Write(new Rule("[bold]Flaws[/]").LeftJustified());

        var flawRules = rules.CreationRules.FlawRules;
        AnsiConsole.MarkupLine(
            $"[grey]Flaws are narrative disadvantages that earn you Resolve when they cause trouble. " +
            $"Choose [bold]{flawRules.MinAtCreation}–{flawRules.MaxAtCreation}[/] at creation " +
            $"(max ever: {flawRules.MaxEver}). " +
            $"Each flaw beyond {flawRules.MaxAtCreation} costs {flawRules.ExtraFlawCostHp} HP.[/]");
        AnsiConsole.MarkupLine(
            "[grey][[C]] = Condition (always in effect, +1 Resolve/issue)   " +
            "[[PH]] = Plot Hook (GM-triggered, +1 Resolve/issue)[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            RenderFlawSummary(sheet, rules);
            AnsiConsole.WriteLine();

            var canFinish = sheet.Flaws.Count >= flawRules.MinAtCreation;
            var atMax     = sheet.Flaws.Count >= flawRules.MaxAtCreation;

            var menuChoices = new List<string>();
            if (!atMax)               menuChoices.Add("Add a flaw");
            if (sheet.Flaws.Count > 0) menuChoices.Add("Remove a flaw");
            if (canFinish)             menuChoices.Add("Done — accept flaws");

            var title = atMax
                ? "[yellow]Maximum flaws at creation reached.[/]"
                : $"Flaw menu ({sheet.Flaws.Count}/{flawRules.MaxAtCreation}):";

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title(title)
                    .AddChoices(menuChoices));

            if (action == "Done — accept flaws") break;

            if (action == "Remove a flaw")
            {
                RemoveFlaw(sheet, rules);
                continue;
            }

            // Add a flaw
            AddFlaw(sheet, rules);
        }

        AnsiConsole.MarkupLine($"[green]✓[/] {sheet.Flaws.Count} flaw(s) selected.");
    }

    private static void AddFlaw(CharacterSheet sheet, RulesRepository rules)
    {
        var selectedIds = sheet.Flaws.Select(f => f.FlawId).ToHashSet();
        var available   = rules.Flaws
            .Where(f => !selectedIds.Contains(f.Id))
            .OrderBy(f => f.Name)
            .ToList();

        if (available.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]All flaws already selected.[/]");
            return;
        }

        var choices = available
            .Select(f => $"{f.Name}{FlawTypeSuffix(f.FlawType)}")
            .Prepend("-- Back --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose a flaw:")
                .EnableSearch()
                .AddChoices(choices));

        if (pick == "-- Back --") return;

        var flaw = available[choices.IndexOf(pick) - 1]; // -1 for Back

        // Show description
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(flaw.Name)}[/]").LeftJustified());
        AnsiConsole.MarkupLine(Markup.Escape(flaw.Description));
        AnsiConsole.WriteLine();

        // Prompt for narrative detail if required
        string? narrativeDetail = null;
        if (flaw.NarrativeConstraint is not null)
        {
            AnsiConsole.MarkupLine($"[yellow]Required detail:[/] {Markup.Escape(flaw.NarrativeConstraint)}");
            narrativeDetail = AnsiConsole.Prompt(
                new TextPrompt<string>("Your answer:")
                    .Validate(v => !string.IsNullOrWhiteSpace(v)
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Cannot be empty.")));
        }

        sheet.Flaws.Add(new SelectedFlaw(flaw.Id, narrativeDetail?.Trim()));
        AnsiConsole.MarkupLine($"  [green]Added:[/] [bold]{Markup.Escape(flaw.Name)}[/]" +
            (narrativeDetail is not null ? $" — {Markup.Escape(narrativeDetail.Trim())}" : ""));
    }

    private static void RemoveFlaw(CharacterSheet sheet, RulesRepository rules)
    {
        if (sheet.Flaws.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No flaws to remove.[/]");
            return;
        }

        var choices = sheet.Flaws
            .Select(sf =>
            {
                var flaw = rules.GetFlaw(sf.FlawId);
                var label = flaw?.Name ?? sf.FlawId;
                return sf.NarrativeDetail is not null
                    ? $"{label} — {sf.NarrativeDetail}"
                    : label;
            })
            .Prepend("-- Cancel --")
            .ToList();

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Remove which flaw?")
                .AddChoices(choices));

        if (pick == "-- Cancel --") return;

        var index = choices.IndexOf(pick) - 1;
        var removed = sheet.Flaws[index];
        sheet.Flaws.RemoveAt(index);

        var removedName = rules.GetFlaw(removed.FlawId)?.Name ?? removed.FlawId;
        AnsiConsole.MarkupLine($"[red]Removed:[/] {Markup.Escape(removedName)}");
    }

    private static void RenderFlawSummary(CharacterSheet sheet, RulesRepository rules)
    {
        if (sheet.Flaws.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No flaws selected yet.[/]");
            return;
        }

        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Flaw")
            .AddColumn("Type")
            .AddColumn("Detail");

        foreach (var sf in sheet.Flaws)
        {
            var flaw = rules.GetFlaw(sf.FlawId);
            table.AddRow(
                Markup.Escape(flaw?.Name ?? sf.FlawId),
                FlawTypeDisplay(flaw?.FlawType ?? "regular"),
                sf.NarrativeDetail is not null ? Markup.Escape(sf.NarrativeDetail) : "[grey]—[/]");
        }

        AnsiConsole.Write(table);
    }

    private static string FlawTypeSuffix(string flawType) => flawType switch
    {
        "condition"              => " [C]",
        "plot_hook"              => " [PH]",
        "plot_hook_and_condition" => " [PH+C]",
        _                        => ""
    };

    private static string FlawTypeDisplay(string flawType) => flawType switch
    {
        "condition"              => "[blue]Condition[/]",
        "plot_hook"              => "[yellow]Plot Hook[/]",
        "plot_hook_and_condition" => "[yellow]Plot Hook[/]+[blue]Condition[/]",
        _                        => "[grey]Regular[/]"
    };

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

    private static int PackageFloorForAbility(CharacterSheet sheet, RulesRepository rules)
    {
        if (sheet.SelectedPackageId is null) return 0;
        var pkg = rules.CreationRules.OptionalPackages
            .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        return pkg?.AbilitiesRank ?? 0;
    }

    private static int PackageFloorForTalent(CharacterSheet sheet, RulesRepository rules)
    {
        if (sheet.SelectedPackageId is null) return 0;
        var pkg = rules.CreationRules.OptionalPackages
            .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        return pkg?.TalentsRank ?? 0;
    }
}
