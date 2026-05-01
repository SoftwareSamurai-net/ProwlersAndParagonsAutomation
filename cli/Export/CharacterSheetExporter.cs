using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using EngineValidationResult = ProwlersAndParagonsAutomation.Engine.ValidationResult;

namespace ProwlersAndParagonsAutomation.Cli.Export;

public sealed class CharacterSheetExporter
{
    private const string OutputDir = "output";

    /// <summary>
    /// Exports the character sheet to both .txt and .json in output/.
    /// Returns (txtPath, jsonPath).
    /// </summary>
    public (string TxtPath, string JsonPath) Export(
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

        var stamp    = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var baseName = $"{safeName}_{stamp}";

        var txtPath  = Path.Combine(dir, baseName + ".txt");
        var jsonPath = Path.Combine(dir, baseName + ".json");

        // .txt export
        var sb = new StringBuilder();
        WriteHeader(sb, sheet, rules, costs, derived);
        WriteAbilities(sb, sheet, rules);
        WriteTalents(sb, sheet, rules);
        WritePowers(sb, sheet, rules, costs, derived);
        WritePerks(sb, sheet, rules, costs);
        WriteFlaws(sb, sheet, rules);
        WriteGear(sb, sheet);
        WriteDerived(sb, sheet, derived);
        WriteNarrative(sb, sheet);
        WriteValidation(sb, validation);
        File.WriteAllText(txtPath, sb.ToString(), Encoding.UTF8);

        // .json export
        var json = BuildJson(sheet, rules, costs, derived, validation);
        File.WriteAllText(jsonPath, json, Encoding.UTF8);

        return (txtPath, jsonPath);
    }

    // ── JSON builder ─────────────────────────────────────────────────────

    private static string BuildJson(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        EngineValidationResult validation)
    {
        var tier    = sheet.SelectedTierId is null ? null : rules.GetTier(sheet.SelectedTierId);
        var pkg     = sheet.SelectedPackageId is null ? null
                      : rules.CreationRules.OptionalPackages.FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        var spent   = costs.TotalCost(sheet);
        var budget  = tier?.HeroPoints ?? 0;

        var root = new JsonObject
        {
            ["meta"] = new JsonObject
            {
                ["generated"] = DateTime.Now.ToString("o"),
                ["system"]    = "Prowlers & Paragons Ultimate Edition"
            },
            ["name"] = sheet.Name,
            ["tier"] = tier is null ? JsonValue.Create<string?>(null) : new JsonObject
            {
                ["id"]          = tier.Id,
                ["name"]        = tier.Name,
                ["hero_points"] = tier.HeroPoints,
                ["trait_cap"]   = tier.TraitCapRank
            },
            ["package"] = pkg is null ? JsonValue.Create<string?>(null) : new JsonObject
            {
                ["id"]   = pkg.Id,
                ["name"] = pkg.Name,
                ["cost"] = pkg.Cost
            },
            ["hp_budget"] = new JsonObject
            {
                ["total"]     = budget,
                ["spent"]     = spent,
                ["remaining"] = budget - spent
            },
            ["abilities"] = new JsonArray(rules.Abilities.Select(ab => (JsonNode)new JsonObject
            {
                ["id"]   = ab.Id,
                ["name"] = ab.Name,
                ["rank"] = sheet.GetAbilityRank(ab.Id)
            }).ToArray()),
            ["talents"] = new JsonArray(rules.Talents
                .Where(ta => sheet.GetTalentRank(ta.Id) > 0)
                .Select(ta => (JsonNode)new JsonObject
                {
                    ["id"]             = ta.Id,
                    ["name"]           = ta.Name,
                    ["rank"]           = sheet.GetTalentRank(ta.Id),
                    ["linked_ability"] = ta.LinkedAbility
                }).ToArray()),
            ["powers"] = new JsonArray(sheet.SelectedPowers.Select(sp =>
            {
                var power      = rules.GetPower(sp.PowerId);
                var baseline   = power is null ? 0 : derived.GetBaselineRank(power, sheet);
                var effective  = baseline + sp.PurchasedRanks;
                var powerCost  = costs.PowerCost(sp);
                return (JsonNode)new JsonObject
                {
                    ["id"]              = sp.PowerId,
                    ["name"]            = power?.Name ?? sp.PowerId,
                    ["purchased_ranks"] = sp.PurchasedRanks,
                    ["baseline_rank"]   = baseline,
                    ["effective_rank"]  = effective,
                    ["cost"]            = powerCost,
                    ["needs_review"]    = power?.NeedsReview ?? false,
                    ["pros"] = new JsonArray(sp.Pros.Select(p => (JsonNode)new JsonObject
                    {
                        ["id"]          = p.Id,
                        ["variant_key"] = p.VariantKey
                    }).ToArray()),
                    ["cons"] = new JsonArray(sp.Cons.Select(c => (JsonNode)new JsonObject
                    {
                        ["id"]          = c.Id,
                        ["variant_key"] = c.VariantKey
                    }).ToArray())
                };
            }).ToArray()),
            ["perks"] = new JsonArray(sheet.Perks.Select(sp =>
            {
                var perk     = rules.GetPerk(sp.PerkId);
                var perkCost = costs.PerkCost(sp);
                return (JsonNode)new JsonObject
                {
                    ["id"]             = sp.PerkId,
                    ["name"]           = perk?.Name ?? sp.PerkId,
                    ["units"]          = sp.Units,
                    ["cost"]           = perkCost,
                    ["narrative_detail"] = sp.NarrativeDetail
                };
            }).ToArray()),
            ["flaws"] = new JsonArray(sheet.Flaws.Select(sf =>
            {
                var flaw = rules.GetFlaw(sf.FlawId);
                return (JsonNode)new JsonObject
                {
                    ["id"]               = sf.FlawId,
                    ["name"]             = flaw?.Name ?? sf.FlawId,
                    ["flaw_type"]        = flaw?.FlawType ?? "regular",
                    ["narrative_detail"] = sf.NarrativeDetail
                };
            }).ToArray()),
            ["gear"] = new JsonArray(sheet.Gear.Select(g => (JsonNode)JsonValue.Create(g)!).ToArray()),
            ["derived"] = new JsonObject
            {
                ["edge"]    = derived.CalculateEdge(sheet),
                ["health"]  = derived.CalculateHealth(sheet),
                ["resolve"] = derived.CalculateResolve(sheet)
            },
            ["narrative"] = new JsonObject
            {
                ["appearance"]  = sheet.Appearance,
                ["motivation"]  = sheet.Motivation,
                ["quote"]       = sheet.Quote,
                ["connections"] = new JsonArray(sheet.Connections
                    .Select(c => (JsonNode)JsonValue.Create(c)!).ToArray())
            },
            ["validation"] = new JsonObject
            {
                ["valid"]    = validation.IsValid,
                ["errors"]   = new JsonArray(validation.Errors
                    .Select(e => (JsonNode)JsonValue.Create($"[{e.Code}] {e.Message}")!)
                    .ToArray()),
                ["warnings"] = new JsonArray(validation.Warnings
                    .Select(w => (JsonNode)JsonValue.Create($"[{w.Code}] {w.Message}")!)
                    .ToArray())
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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

    private static void WritePerks(StringBuilder sb, CharacterSheet sheet, RulesRepository rules, CostCalculator costs)
    {
        sb.AppendLine("─── PERKS ──────────────────────────────────────────────────");
        if (sheet.Perks.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var sp in sheet.Perks)
            {
                var perk = rules.GetPerk(sp.PerkId);
                var name = perk?.Name ?? sp.PerkId;
                var cost = costs.PerkCost(sp);
                var units = sp.Units > 1 ? $" ×{sp.Units}" : "";
                sb.AppendLine($"  • {name}{units}  — {cost} HP");
                if (sp.NarrativeDetail is not null)
                    sb.AppendLine($"      Detail: {sp.NarrativeDetail}");
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
        sb.AppendLine($"  Edge:    {derived.CalculateEdge(sheet)}");
        sb.AppendLine($"  Health:  {derived.CalculateHealth(sheet)}");
        sb.AppendLine($"  Resolve: {derived.CalculateResolve(sheet)}");
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
