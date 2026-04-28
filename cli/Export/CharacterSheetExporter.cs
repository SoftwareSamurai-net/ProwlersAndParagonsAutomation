using System.Text;
using ProwlersAndParagonsAutomation.Engine;
using EngineValidationResult = ProwlersAndParagonsAutomation.Engine.ValidationResult;

namespace ProwlersAndParagonsAutomation.Cli.Export;

public sealed class CharacterSheetExporter
{
    private const string OutputDir = "output";

    public string Export(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        EngineValidationResult validation,
        string projectRoot)
    {
        var dir = Path.Combine(projectRoot, OutputDir);
        Directory.CreateDirectory(dir);

        var safeName = string.IsNullOrWhiteSpace(sheet.Name)
            ? "unnamed"
            : string.Concat(sheet.Name.Split(Path.GetInvalidFileNameChars()));

        var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var path     = Path.Combine(dir, fileName);

        var sb = new StringBuilder();
        WriteHeader(sb, sheet, rules, costs, derived);
        WriteAbilities(sb, sheet, rules);
        WriteTalents(sb, sheet, rules);
        WritePowers(sb, sheet, rules, costs, derived);
        WriteFlaws(sb, sheet, rules);
        WriteGear(sb, sheet);
        WriteDerived(sb, sheet, derived);
        WriteNarrative(sb, sheet);
        WriteValidation(sb, validation);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    // ── Sections ──────────────────────────────────────────────────────────

    private static void WriteHeader(StringBuilder sb, CharacterSheet sheet,
        RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        var tier  = sheet.SelectedTierId is null ? "None" : rules.GetTier(sheet.SelectedTierId)?.Name ?? "Unknown";
        var total = costs.TotalCost(sheet);
        var budget = rules.GetTier(sheet.SelectedTierId ?? "")?.HeroPoints ?? 0;

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("  PROWLERS & PARAGONS — CHARACTER SHEET");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  Name:     {sheet.Name}");
        sb.AppendLine($"  Tier:     {tier}");
        sb.AppendLine($"  HP Spent: {total} / {budget}");
        sb.AppendLine();
    }

    private static void WriteAbilities(StringBuilder sb, CharacterSheet sheet, RulesRepository rules)
    {
        sb.AppendLine("─── ABILITIES ──────────────────────────────────────────────");
        foreach (var ab in rules.Abilities)
        {
            var rank = sheet.GetAbilityRank(ab.Id);
            sb.AppendLine($"  {ab.Name,-14} {rank}d");
        }
        sb.AppendLine();
    }

    private static void WriteTalents(StringBuilder sb, CharacterSheet sheet, RulesRepository rules)
    {
        sb.AppendLine("─── TALENTS ────────────────────────────────────────────────");
        foreach (var ta in rules.Talents)
        {
            var rank = sheet.GetTalentRank(ta.Id);
            if (rank > 0)
                sb.AppendLine($"  {ta.Name,-16} {rank}d  (linked: {ta.LinkedAbility})");
        }
        if (sheet.TalentRanks.Values.All(r => r == 0))
            sb.AppendLine("  (none)");
        sb.AppendLine();
    }

    private static void WritePowers(StringBuilder sb, CharacterSheet sheet,
        RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        sb.AppendLine("─── POWERS ─────────────────────────────────────────────────");
        if (sheet.SelectedPowers.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var sp in sheet.SelectedPowers)
            {
                var power     = rules.GetPower(sp.PowerId);
                var name      = power?.Name ?? sp.PowerId;
                var baseline  = power is null ? 0 : derived.GetBaselineRank(power, sheet);
                var effective = baseline + sp.PurchasedRanks;
                var cost      = costs.PowerCost(sp);
                var review    = power?.NeedsReview == true ? " [needs_review]" : "";

                sb.AppendLine($"  {name}{review}");
                sb.AppendLine($"    Effective rank: {effective}d  " +
                              $"(baseline {baseline}d + purchased {sp.PurchasedRanks}d)  " +
                              $"— {cost} HP");

                if (sp.Pros.Count > 0)
                    sb.AppendLine("    Pros: " + string.Join(", ",
                        sp.Pros.Select(p => p.VariantKey is null ? p.Id : $"{p.Id}:{p.VariantKey}")));

                if (sp.Cons.Count > 0)
                    sb.AppendLine("    Cons: " + string.Join(", ",
                        sp.Cons.Select(c => c.VariantKey is null ? c.Id : $"{c.Id}:{c.VariantKey}")));
            }
        }
        sb.AppendLine();
    }

    private static void WriteFlaws(StringBuilder sb, CharacterSheet sheet, RulesRepository rules)
    {
        sb.AppendLine("─── FLAWS ──────────────────────────────────────────────────");
        if (sheet.Flaws.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var sf in sheet.Flaws)
            {
                var flaw = rules.GetFlaw(sf.FlawId);
                var name = flaw?.Name ?? sf.FlawId;
                var type = flaw?.FlawType switch
                {
                    "condition"               => " [Condition]",
                    "plot_hook"               => " [Plot Hook]",
                    "plot_hook_and_condition" => " [Plot Hook + Condition]",
                    _                         => ""
                };
                sb.AppendLine($"  • {name}{type}");
                if (sf.NarrativeDetail is not null)
                    sb.AppendLine($"      Detail: {sf.NarrativeDetail}");
            }
        }
        sb.AppendLine();
    }

    private static void WriteGear(StringBuilder sb, CharacterSheet sheet)
    {
        sb.AppendLine("─── GEAR ───────────────────────────────────────────────────");
        if (sheet.Gear.Count == 0)
            sb.AppendLine("  (none)");
        else
            foreach (var item in sheet.Gear)
                sb.AppendLine($"  • {item}");
        sb.AppendLine();
    }

    private static void WriteDerived(StringBuilder sb, CharacterSheet sheet, DerivedStatsCalculator derived)
    {
        sb.AppendLine("─── DERIVED STATS ──────────────────────────────────────────");
        sb.AppendLine($"  Edge:   {derived.CalculateEdge(sheet)}");
        sb.AppendLine($"  Health: {derived.CalculateHealth(sheet)}");
        sb.AppendLine("  Resolve: (see Chapter 5 — not yet calculated)");
        sb.AppendLine();
    }

    private static void WriteNarrative(StringBuilder sb, CharacterSheet sheet)
    {
        sb.AppendLine("─── NARRATIVE ──────────────────────────────────────────────");
        sb.AppendLine($"  Appearance:  {sheet.Appearance}");
        sb.AppendLine($"  Motivation:  {sheet.Motivation}");
        sb.AppendLine($"  Quote:       \"{sheet.Quote}\"");
        if (sheet.Connections.Count > 0)
        {
            sb.AppendLine("  Connections:");
            foreach (var conn in sheet.Connections)
                sb.AppendLine($"    • {conn}");
        }
        sb.AppendLine();
    }

    private static void WriteValidation(StringBuilder sb, EngineValidationResult validation)
    {
        sb.AppendLine("─── VALIDATION ─────────────────────────────────────────────");
        sb.AppendLine(validation.IsValid ? "  STATUS: VALID" : "  STATUS: INVALID");

        foreach (var issue in validation.Issues)
            sb.AppendLine($"  [{issue.Severity}] {issue.Code}: {issue.Message}");

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
    }
}
