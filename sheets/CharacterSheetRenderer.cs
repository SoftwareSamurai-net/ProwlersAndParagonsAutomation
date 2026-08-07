using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Builds the two character sheet exports — the ruled text sheet and the JSON one — as
/// strings.
///
/// <para>This used to be part of the CLI's exporter, which built the strings and wrote the
/// files in one method. The browser needs the same two documents but hands them to a
/// download rather than to <c>File.WriteAllText</c>, so the building moved here and the
/// writing stayed behind. Both hosts produce byte-identical output because there is only
/// one copy of it.</para>
/// </summary>
public static class CharacterSheetRenderer
{
    /// <summary>
    /// A file name safe on any host: the character's name, stripped of anything a
    /// filesystem or a Content-Disposition header would object to, plus a timestamp.
    /// Returned without an extension so the caller can append .txt or .json.
    /// </summary>
    public static string BaseFileName(CharacterSheet sheet, DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var safeName = string.IsNullOrWhiteSpace(sheet.Name)
            ? "unnamed"
            : new string(sheet.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        return $"{safeName}_{generatedAt:yyyyMMdd_HHmmss}";
    }

    // ── Text sheet ────────────────────────────────────────────────────────

    /// <summary>The ruled text sheet, exactly as the CLI has always written it.</summary>
    public static string RenderText(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        ValidationResult validation,
        DateTime generatedAt)
    {
        var sb = new StringBuilder();
        WriteHeader(sb, sheet, rules, costs);
        WriteAbilities(sb, sheet, rules);
        WriteTalents(sb, sheet, rules);
        WritePowers(sb, sheet, rules, costs, derived);
        WritePerks(sb, sheet, rules, costs);
        WriteFlaws(sb, sheet, rules);
        WriteGear(sb, sheet, rules, costs);
        WriteDerived(sb, sheet, derived);
        WriteNarrative(sb, sheet);
        WriteValidation(sb, validation, generatedAt);
        return sb.ToString();
    }

    private static void WriteHeader(StringBuilder sb, CharacterSheet sheet,
        RulesRepository rules, CostCalculator costs)
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
        if (sheet.SelectedPowers.Count == 0)
        {
            sb.AppendLine("─── POWERS ─────────────────────────────────────────────────");
            sb.AppendLine("  (none)");
            sb.AppendLine();
            return;
        }

        // A published sheet groups Powers under Source headings — TECH POWERS, MAGIC
        // POWERS — rather than listing them flat. See SourceGrouping.
        foreach (var group in new SourceGrouping(rules).GroupPowers(sheet))
        {
            var rule = new string('─', Math.Max(3, 59 - group.Heading.Length));
            sb.AppendLine($"─── {group.Heading} {rule}");

            foreach (var sp in group.Powers)
            {
                var power     = rules.GetPower(sp.PowerId);
                var name      = power?.Name ?? sp.PowerId;
                var baseline  = power is null ? 0 : derived.GetBaselineRank(power, sheet, sp);
                var effective = power is null ? 0 : derived.GetEffectiveRank(sp, sheet);
                var cost      = costs.PowerCost(sp);
                var review    = power?.NeedsReview == true ? " [mechanics unverified]" : "";

                sb.AppendLine($"  {name}{review}");
                if (power is not null)
                    sb.AppendLine($"    {PowerFormatter.StatLine(power)}");

                sb.AppendLine(effective > 0
                    ? $"    Effective rank: {effective}d  " +
                      $"(baseline {baseline}d + purchased {sp.PurchasedRanks}d)  — {cost} HP"
                    : $"    No rank  — {cost} HP");

                if (sp.Pros.Count > 0)
                    sb.AppendLine("    Pros: " + string.Join(", ",
                        sp.Pros.Select(p => p.VariantKey is null ? p.Id : $"{p.Id}:{p.VariantKey}")));

                if (sp.Cons.Count > 0)
                    sb.AppendLine("    Cons: " + string.Join(", ",
                        sp.Cons.Select(c => c.VariantKey is null ? c.Id : $"{c.Id}:{c.VariantKey}")));
            }

            sb.AppendLine();
        }
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

    private static void WriteGear(StringBuilder sb, CharacterSheet sheet,
        RulesRepository rules, CostCalculator costs)
    {
        sb.AppendLine("─── GEAR ───────────────────────────────────────────────────");
        if (sheet.Gear.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (var item in sheet.Gear)
                sb.AppendLine($"  • {GearFormatter.Describe(item, rules, costs)}");

            // Mundane gear is free, so this is 0 unless something was customised.
            var spent = costs.TotalGearCost(sheet);
            if (spent > 0) sb.AppendLine($"  Gear total: {spent} HP");
        }
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

    private static void WriteValidation(StringBuilder sb, ValidationResult validation, DateTime generatedAt)
    {
        sb.AppendLine("─── VALIDATION ─────────────────────────────────────────────");
        sb.AppendLine(validation.IsValid ? "  STATUS: VALID" : "  STATUS: INVALID");

        foreach (var issue in validation.Issues)
            sb.AppendLine($"  [{issue.Severity}] {issue.Code}: {issue.Message}");

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"  Generated: {generatedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
    }

    // ── JSON sheet ────────────────────────────────────────────────────────

    /// <summary>
    /// The JSON export. Item 6 in PROGRESS.md wants to read this back in, so it carries
    /// everything a rebuild would need — including <c>rank_against_powers</c>, which is
    /// otherwise invisible: a rankless Power exports <c>effective_rank: 0</c> beside it.
    /// </summary>
    public static string RenderJson(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        ValidationResult validation,
        DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(derived);
        ArgumentNullException.ThrowIfNull(validation);

        var tier    = sheet.SelectedTierId is null ? null : rules.GetTier(sheet.SelectedTierId);
        var pkg     = sheet.SelectedPackageId is null ? null
                      : rules.CreationRules.OptionalPackages.FirstOrDefault(p => p.Id == sheet.SelectedPackageId);
        var spent   = costs.TotalCost(sheet);
        var budget  = tier?.HeroPoints ?? 0;

        var root = new JsonObject
        {
            ["meta"] = new JsonObject
            {
                ["generated"] = generatedAt.ToString("o"),
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
                var baseline   = power is null ? 0 : derived.GetBaselineRank(power, sheet, sp);
                var effective  = power is null ? 0 : derived.GetEffectiveRank(sp, sheet);
                var powerCost  = costs.PowerCost(sp);
                return (JsonNode)new JsonObject
                {
                    ["id"]              = sp.PowerId,
                    ["name"]            = power?.Name ?? sp.PowerId,
                    ["range"]           = power?.Range,
                    ["rank_type"]       = power?.RankType,
                    ["cost_type"]       = power?.CostType,
                    ["purchased_ranks"] = sp.PurchasedRanks,
                    ["baseline_rank"]   = baseline,
                    ["baseline_trait"]  = sp.BaselineTraitId,
                    ["effective_rank"]  = effective,
                    ["units"]           = sp.Units,
                    ["cost_variant"]    = sp.CostVariantKey,
                    ["cost"]            = powerCost,
                    ["source"]          = sp.SourceId,
                    ["source_heading"]  = SourceGrouping.HeadingFor(
                                              sp.SourceId is null ? null : rules.GetSource(sp.SourceId)),
                    // The rank this Power uses when another Power acts on it. For a
                    // rankless Power that is the Source's default rank, not 0.
                    ["rank_against_powers"] = derived.GetRankAgainstPowers(sp, sheet),
                    ["mechanics_verified"] = power?.MechanicsVerified ?? false,
                    ["source_ref"]      = power?.SourceRef,
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
            ["gear"] = new JsonArray(sheet.Gear.Select(g => (JsonNode)new JsonObject
            {
                ["name"]     = g.Name,
                ["cost"]     = costs.GearCost(g),
                ["paired_under_two_fisted"] = g.PairedUnderTwoFisted,
                ["features"] = new JsonArray(g.Features.Select(f => (JsonNode)new JsonObject
                {
                    ["id"]    = f.FeatureId,
                    ["name"]  = rules.GetGearFeature(f.FeatureId)?.Name ?? f.FeatureId,
                    ["grade"] = f.GradeKey
                }).ToArray()),
                ["pros"] = new JsonArray(g.Pros.Select(p => (JsonNode)JsonValue.Create(p.Id)!).ToArray()),
                ["cons"] = new JsonArray(g.Cons.Select(c => (JsonNode)JsonValue.Create(c.Id)!).ToArray())
            }).ToArray()),
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
}
