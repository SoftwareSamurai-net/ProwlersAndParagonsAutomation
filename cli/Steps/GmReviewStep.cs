using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;
using Spectre.Console;
using EngineValidationResult = ProwlersAndParagonsAutomation.Engine.ValidationResult;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public sealed class GmReviewStep : IWizardStep
{
    private readonly CharacterValidator _validator;
    private readonly CharacterSheetExporter _exporter;
    private readonly string _projectRoot;

    public string StepId => "gm_review";
    public string DisplayName => "GM Review";

    public GmReviewStep(CharacterValidator validator, CharacterSheetExporter exporter, string projectRoot)
    {
        _validator   = validator;
        _exporter    = exporter;
        _projectRoot = projectRoot;
    }

    public void Execute(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Step 6 — GM Review[/]").LeftJustified());
        AnsiConsole.WriteLine();

        RenderAbilities(sheet, rules);
        RenderTalents(sheet, rules);
        RenderPowers(sheet, rules, costs, derived);
        RenderPerks(sheet, rules, costs);
        RenderDerived(sheet, derived);
        RenderNarrative(sheet, rules, costs);

        AnsiConsole.WriteLine();

        // Validate
        var result = _validator.Validate(sheet);
        RenderValidation(result);

        AnsiConsole.WriteLine();

        // Export
        AnsiConsole.Status()
            .Start("Saving character sheet…", ctx =>
            {
                var (txtPath, jsonPath) = _exporter.Export(sheet, rules, costs, derived, result, _projectRoot);
                ctx.Status("Done");
                AnsiConsole.MarkupLine($"[green]✓ Text:[/] {Markup.Escape(txtPath)}");
                AnsiConsole.MarkupLine($"[green]✓ JSON:[/] {Markup.Escape(jsonPath)}");
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]Character creation complete.[/]");
    }

    // ── Rendering ─────────────────────────────────────────────────────────

    private static void RenderAbilities(CharacterSheet sheet, RulesRepository rules)
    {
        var table = new Table()
            .Title("[bold]ABILITIES[/]")
            .BorderColor(Color.Grey)
            .AddColumn("Ability")
            .AddColumn(new TableColumn("Rank").Centered())
            .AddColumn("Description");

        foreach (var ab in rules.Abilities)
        {
            var rank = sheet.GetAbilityRank(ab.Id);
            table.AddRow(
                Markup.Escape(ab.Name),
                rank > 0 ? $"[bold]{rank}d[/]" : "[grey]0d[/]",
                Markup.Escape(ab.Description.Length > 60 ? ab.Description[..60] + "…" : ab.Description));
        }

        AnsiConsole.Write(table);
    }

    private static void RenderTalents(CharacterSheet sheet, RulesRepository rules)
    {
        var purchased = rules.Talents.Where(t => sheet.GetTalentRank(t.Id) > 0).ToList();
        if (purchased.Count == 0) return;

        var table = new Table()
            .Title("[bold]TALENTS[/]")
            .BorderColor(Color.Grey)
            .AddColumn("Talent")
            .AddColumn(new TableColumn("Rank").Centered())
            .AddColumn("Linked Ability");

        foreach (var ta in purchased)
            table.AddRow(
                Markup.Escape(ta.Name),
                $"[bold]{sheet.GetTalentRank(ta.Id)}d[/]",
                Markup.Escape(ta.LinkedAbility));

        AnsiConsole.Write(table);
    }

    private static void RenderPowers(CharacterSheet sheet, RulesRepository rules,
        CostCalculator costs, DerivedStatsCalculator derived)
    {
        // Grouped under Source headings, the way a published sheet prints them. A group can
        // hold only an Abilities (…) line — a Trait bought through powered armour on a
        // character with no Tech Power — so this is not gated on there being any Powers.
        foreach (var group in new SourceGrouping(rules).GroupBySource(sheet))
            RenderPowerGroup(group, sheet, rules, costs, derived);
    }

    private static void RenderPowerGroup(SourceGrouping.Group group, CharacterSheet sheet,
        RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        var table = new Table()
            .Title($"[bold]{Markup.Escape(group.Heading)}[/]")
            .BorderColor(Color.Grey)
            .AddColumn("Power")
            .AddColumn(new TableColumn("Effective").Centered())
            .AddColumn("Pros / Cons")
            .AddColumn(new TableColumn("HP Cost").Centered());

        // The Abilities (…) and Talents (…) lines print inside the group, above the Powers,
        // which is where a published sheet puts them. They have no rank and no cost, so both
        // of those columns read the same "—" rather than one of them claiming a cost of 0.
        foreach (var line in group.TraitLines)
            table.AddRow($"[italic]{Markup.Escape(line)}[/]", "[grey]—[/]", "[grey]—[/]", "[grey]—[/]");

        foreach (var sp in group.Powers)
        {
            var power     = rules.GetPower(sp.PowerId);
            var name      = power?.Name ?? sp.PowerId;
            var effective = power is null ? 0 : derived.GetEffectiveRank(sp, sheet);
            var cost      = costs.PowerCost(sp, sheet.ImmortalityCost);
            var review    = power?.NeedsReview == true ? " [yellow]*[/]" : "";

            // Through the shared formatter, so a repeated option reads the same here as on the
            // sheet and in the export — five copies of Also X is "also_x ×5", not five names.
            static string Keyed(SelectedProCon choice) =>
                Markup.Escape(choice.VariantKey is null ? choice.Id : $"{choice.Id}:{choice.VariantKey}");

            var proConParts = new List<string>();
            if (sp.Pros.Count > 0)
                proConParts.Add("[green]+" + PowerFormatter.ModifierLine(sp.Pros, Keyed)
                    .Replace(", ", ", +", StringComparison.Ordinal) + "[/]");
            if (sp.Cons.Count > 0)
                proConParts.Add("[red]-" + PowerFormatter.ModifierLine(sp.Cons, Keyed)
                    .Replace(", ", ", -", StringComparison.Ordinal) + "[/]");

            table.AddRow(
                $"{Markup.Escape(name)}{review}",
                effective > 0 ? $"[bold]{effective}d[/]" : "[grey]no rank[/]",
                proConParts.Count > 0 ? string.Join("  ", proConParts) : "[grey]—[/]",
                $"[bold]{cost}[/]");
        }

        AnsiConsole.Write(table);
    }

    private static void RenderPerks(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        if (sheet.Perks.Count == 0) return;

        var table = new Table()
            .Title("[bold]PERKS[/]")
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
    }

    private static void RenderDerived(CharacterSheet sheet, DerivedStatsCalculator derived)
    {
        var table = new Table()
            .Title("[bold]DERIVED STATS[/]")
            .BorderColor(Color.Gold1)
            .AddColumn("Stat")
            .AddColumn(new TableColumn("Value").Centered());

        table.AddRow("[bold]Edge[/]",    $"[bold green]{derived.CalculateEdge(sheet)}[/]");
        table.AddRow("[bold]Health[/]",  $"[bold green]{derived.CalculateHealth(sheet)}[/]");
        table.AddRow("[bold]Resolve[/]", $"[bold green]{derived.CalculateResolve(sheet)}[/]");

        AnsiConsole.Write(table);
    }

    private static void RenderNarrative(CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        if (string.IsNullOrWhiteSpace(sheet.Name) &&
            string.IsNullOrWhiteSpace(sheet.Motivation) &&
            sheet.Flaws.Count == 0) return;

        var panel = new Panel(
            new Markup(
                $"[bold]Name:[/]        {Markup.Escape(sheet.Name)}\n" +
                $"[bold]Appearance:[/]  {Markup.Escape(sheet.Appearance)}\n" +
                $"[bold]Motivation:[/]  {Markup.Escape(sheet.Motivation)}\n" +
                $"[bold]Quote:[/]       [italic]\"{Markup.Escape(sheet.Quote)}\"[/]\n" +
                $"[bold]Flaws:[/]       {(sheet.Flaws.Count > 0 ? string.Join(", ", sheet.Flaws.Select(sf => Markup.Escape(rules.GetFlaw(sf.FlawId)?.Name ?? sf.FlawId))) : "[grey]none[/]")}\n" +
                $"[bold]Connections:[/] {(sheet.Connections.Count > 0 ? string.Join(", ", sheet.Connections.Select(Markup.Escape)) : "[grey]none[/]")}\n" +
                $"[bold]Gear:[/]        {(sheet.Gear.Count > 0 ? string.Join(", ", sheet.Gear.Select(g => Markup.Escape(GearFormatter.Describe(g, rules, costs)))) : "[grey]none[/]")}"
            ))
            .Header("[bold]NARRATIVE[/]")
            .BorderColor(Color.MediumPurple);

        AnsiConsole.Write(panel);
    }

    private static void RenderValidation(EngineValidationResult result)
    {
        if (result.IsValid && !result.Warnings.Any())
        {
            AnsiConsole.Write(new Panel(new Markup("[bold green]✓ Character is VALID — no issues.[/]"))
                .BorderColor(Color.Green));
            return;
        }

        var lines = new List<string>
        {
            result.IsValid
                ? "[bold green]✓ Character is VALID[/]"
                : "[bold red]✗ Character is INVALID[/]"
        };

        foreach (var e in result.Errors)
            lines.Add($"  [red]ERROR [{Markup.Escape(e.Code)}]:[/] {Markup.Escape(e.Message)}");

        foreach (var w in result.Warnings)
            lines.Add($"  [yellow]WARN  [{Markup.Escape(w.Code)}]:[/] {Markup.Escape(w.Message)}");

        var color = result.IsValid ? Color.Yellow : Color.Red;
        AnsiConsole.Write(new Panel(new Markup(string.Join("\n", lines)))
            .Header("[bold]VALIDATION[/]")
            .BorderColor(color));
    }
}
