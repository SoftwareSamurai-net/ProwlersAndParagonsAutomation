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
    public static string BaseFileName(CharacterSheet sheet, DateTime generatedAt) =>
        $"{BaseFileName(sheet)}_{generatedAt:yyyyMMdd_HHmmss}";

    /// <summary>
    /// The same name with no timestamp, so re-exporting a character replaces its sheets
    /// instead of adding a pair beside them.
    ///
    /// <para><b>The timestamp is right for one export and wrong for a roster.</b> Twenty-eight
    /// characters re-exported after an edit reached fifty-six <c>.txt</c> files before anybody
    /// noticed, and finding the newest of each needed a script. This is what
    /// <c>build --overwrite</c> names its files with; the timestamped form stays the default,
    /// because replacing a file nobody asked to replace is the worse failure of the two.</para>
    ///
    /// <para>Two characters whose names differ only in punctuation share this name — the
    /// caller has to notice that, and <c>build</c> reports it rather than letting one write
    /// over the other in silence.</para>
    /// </summary>
    public static string BaseFileName(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return string.IsNullOrWhiteSpace(sheet.Name)
            ? "unnamed"
            : new string(sheet.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
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

        WriteHouseRules(sb, sheet, rules);
    }

    /// <summary>
    /// <b>What this character's table has decided, printed only when it has decided something.</b>
    ///
    /// <para>Three kinds of thing, in the order somebody reading a sheet at a table wants them:
    /// the Trait Cap, because it is the ceiling every rank on the page is judged against and the
    /// datum the Resolve further down is measured from; the price for Immortality, because it is
    /// Hero Points and the total above has already spent them; and the optional rules, because
    /// they decide what happens when the sheet is used in a fight.</para>
    ///
    /// <para><b>Absent in full for a character at no table</b> — which is every character exported
    /// before this existed, so every one of those files is unchanged byte for byte. A heading over
    /// "Fatal Damage: no" thirteen times is a block that is true of every game and tells a reader
    /// nothing; <see cref="CampaignTable.IsTheBook"/> is the question that keeps it off the
    /// page.</para>
    ///
    /// <para><b>The cap prints only when the character carries a house one</b>, and then as
    /// <see cref="DerivedStatsCalculator.EffectiveTraitCap"/>. A tier's own ceiling is a fact about
    /// the tier and this block is about the table.</para>
    /// </summary>
    private static void WriteHouseRules(StringBuilder sb, CharacterSheet sheet, RulesRepository rules)
    {
        var switches = sheet.CampaignTable is { IsTheBook: false } table ? table : null;

        if (sheet.TraitCapRank is null && sheet.ImmortalityCost is null && switches is null) return;

        sb.AppendLine("─── HOUSE RULES ────────────────────────────────────────────");

        if (sheet.TraitCapRank is not null)
        {
            var tier = sheet.SelectedTierId is null ? null : rules.GetTier(sheet.SelectedTierId);
            sb.AppendLine($"  Trait Cap: {DerivedStatsCalculator.EffectiveTraitCap(sheet, tier)}d");
        }

        if (sheet.ImmortalityCost is { } price)
            sb.AppendLine($"  {rules.GetPower("immortality")?.Name ?? "Immortality"}: {price} HP");

        if (switches is not null)
        {
            foreach (var name in HouseRuleFormatter.On(switches)) sb.AppendLine($"  {name}");

            // The rank only where the switch that reads it is on. A figure left behind under a
            // switch nobody turned on is not a rule this table adopted, and printing it would say
            // it was.
            if (switches is { RaisedGearLimit: true, GearLimitRank: { } rank })
                sb.AppendLine($"    Gear Limit {rank}d");
        }

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
        // A published sheet groups Powers under Source headings — TECH POWERS, MAGIC
        // POWERS — rather than listing them flat, and records an Ability's or Talent's
        // Source as a line inside the group rather than on the Abilities block. See
        // SourceGrouping.
        var groups = new SourceGrouping(rules).GroupBySource(sheet);

        if (groups.Count == 0)
        {
            sb.AppendLine("─── POWERS ─────────────────────────────────────────────────");
            sb.AppendLine("  (none)");
            sb.AppendLine();
            return;
        }

        foreach (var group in groups)
        {
            var rule = new string('─', Math.Max(3, 59 - group.Heading.Length));
            sb.AppendLine($"─── {group.Heading} {rule}");

            foreach (var line in group.TraitLines)
                sb.AppendLine($"  {line}");

            foreach (var sp in group.Powers)
            {
                var power     = rules.GetPower(sp.PowerId);
                var name      = power?.Name ?? sp.PowerId;
                var baseline  = power is null ? 0 : derived.GetBaselineRank(power, sheet, sp);
                var effective = power is null ? 0 : derived.GetEffectiveRank(sp, sheet);
                var cost      = costs.PowerCost(sp, sheet.ImmortalityCost);
                var review    = power?.NeedsReview == true ? " [mechanics unverified]" : "";

                sb.AppendLine($"  {name}{review}");
                if (power is not null)
                    sb.AppendLine($"    {PowerFormatter.StatLine(power)}");

                sb.AppendLine(effective > 0
                    ? $"    Effective rank: {effective}d  " +
                      $"(baseline {baseline}d + purchased {sp.PurchasedRanks}d)  — {cost} HP"
                    : $"    No rank  — {cost} HP");

                if (sp.Pros.Count > 0)
                    sb.AppendLine("    Pros: " + PowerFormatter.ModifierLine(sp.Pros, Keyed));

                if (sp.Cons.Count > 0)
                    sb.AppendLine("    Cons: " + PowerFormatter.ModifierLine(sp.Cons, Keyed));
            }

            sb.AppendLine();
        }
    }

    /// <summary>
    /// How the text export names one Pro or Con: its id, and its grade where it has one. Ids
    /// rather than printed names, because this export is the machine-readable half.
    /// </summary>
    private static string Keyed(SelectedProCon choice) =>
        choice.VariantKey is null ? choice.Id : $"{choice.Id}:{choice.VariantKey}";

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
        var grouping = new SourceGrouping(rules);

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

                // The tier's own ceiling, which is a fact about the tier and stays one. The cap
                // this character is actually built to is the top-level `trait_cap` below, and it
                // differs whenever a table has imposed a house rule.
                ["trait_cap"]   = tier.TraitCapRank
            },

            // <b>The ceiling this character is built to</b>, which is the house cap on the sheet
            // where there is one and the tier's otherwise. Top-level rather than inside `tier`
            // because it is not always the tier's — and it is here at all because it is the datum
            // `derived.resolve` further down is measured from, so a sheet that printed the figure
            // and not the cap would be unreadable the first time a campaign tightened one.
            ["trait_cap"] = DerivedStatsCalculator.EffectiveTraitCap(sheet, tier),

            // <b>The optional rules this character's table has turned on, for the reader that
            // cannot ask the campaign.</b> The encounter server is handed characters and never a
            // campaign — it cannot resolve a campaign id any more than the engine can — so a fight
            // fought with this sheet is fought under the book unless the sheet says otherwise.
            // Null for a character at no table, which is every export made before this existed.
            //
            // <b>Every switch is written, including the ones that are off</b>, unlike the text
            // sheet's list. A document is read by a program: an absent key would be
            // indistinguishable from a build that had not heard of the setting, and "off" is a
            // thing this table decided as much as "on" is.
            ["campaign_table"] = TableJson(sheet.CampaignTable),

            // What this table charges for Immortality, or null for the book's 3. It is beside the
            // switches rather than inside them because it is the one house rule here that costs
            // Hero Points — `hp_budget.spent` below has already been charged it.
            ["immortality_cost"] = sheet.ImmortalityCost is { } price
                ? JsonValue.Create(price)
                : JsonValue.Create<int?>(null),
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
                ["rank"] = sheet.GetAbilityRank(ab.Id),
                // Both, because they answer different questions. "source" is what the sheet
                // recorded and null means "the default was not overridden" — the input a
                // rebuild needs. "effective_source" is what the Trait actually is.
                ["source"]           = sheet.AbilitySources.GetValueOrDefault(ab.Id),
                ["effective_source"] = grouping.EffectiveAbilitySource(sheet, ab.Id)
            }).ToArray()),
            // Bought ranks, plus any Talent carrying a Source. A Source can be set on a 0d
            // Talent — both editors offer all twelve — and the rank filter alone dropped it,
            // so every sheet printed "Talents (Academics)" while this export carried no
            // Academics entry for it to come from. Abilities are unfiltered, so the two
            // behaved differently for the same state.
            ["talents"] = new JsonArray(rules.Talents
                .Where(ta => sheet.GetTalentRank(ta.Id) > 0 || sheet.TalentSources.ContainsKey(ta.Id))
                .Select(ta => (JsonNode)new JsonObject
                {
                    ["id"]               = ta.Id,
                    ["name"]             = ta.Name,
                    ["rank"]             = sheet.GetTalentRank(ta.Id),
                    ["linked_ability"]   = ta.LinkedAbility,
                    ["source"]           = sheet.TalentSources.GetValueOrDefault(ta.Id),
                    ["effective_source"] = grouping.EffectiveTalentSource(sheet, ta.Id)
                }).ToArray()),
            // The Source headings as a sheet prints them, with the Abilities (…) and
            // Talents (…) lines that belong inside each. Otherwise this export is the one
            // surface of the three that cannot show a Trait's Source where the book puts it.
            ["source_groups"] = new JsonArray(grouping.GroupBySource(sheet)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["heading"]     = g.Heading,
                    ["source"]      = g.Source?.Id,
                    ["trait_lines"] = new JsonArray(g.TraitLines
                        .Select(l => (JsonNode)JsonValue.Create(l)!).ToArray()),
                    ["power_ids"]   = new JsonArray(g.Powers
                        .Select(p => (JsonNode)JsonValue.Create(p.PowerId)!).ToArray())
                }).ToArray()),
            ["powers"] = new JsonArray(sheet.SelectedPowers.Select(sp =>
            {
                var power      = rules.GetPower(sp.PowerId);
                var baseline   = power is null ? 0 : derived.GetBaselineRank(power, sheet, sp);
                var effective  = power is null ? 0 : derived.GetEffectiveRank(sp, sheet);
                var powerCost  = costs.PowerCost(sp, sheet.ImmortalityCost);
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

    /// <summary>
    /// A table's optional rules as JSON, or null for a character at no table.
    ///
    /// <para><b>The keys are the property names <c>CampaignTable</c> and
    /// <c>play/Encounter/TableRules.cs</c> share, in snake_case</b> — the spelling every other key
    /// in this document uses. They are written out one at a time rather than serialised off the
    /// record, because a serialiser's naming policy is a thing that can be changed elsewhere, and
    /// this is a contract the encounter server reads: <c>CampaignTableExportTests</c> holds the
    /// key list to the switch list so a setting cannot be added on one side alone.</para>
    ///
    /// <para><b>The Gear Limit rank is written whether or not the switch that reads it is on</b>,
    /// unlike the text sheet, where a rank under an unadopted switch would read as a rule. A
    /// document says what the campaign holds; a program reading it applies
    /// <c>raised_gear_limit</c> itself, exactly as <c>TableRules.GearLimit</c> does.</para>
    /// </summary>
    private static JsonObject? TableJson(CampaignTable? table)
    {
        if (table is null) return null;

        var block = new JsonObject();

        foreach (var (key, _, _) in HouseRuleFormatter.All)
            block[SnakeCase(key)] = HouseRuleFormatter.IsOn(table, key);

        block["gear_limit_rank"] = table.GearLimitRank is { } rank
            ? JsonValue.Create(rank)
            : JsonValue.Create<int?>(null);

        return block;
    }

    /// <summary>
    /// <c>ActiveDefensesCost</c> to <c>active_defenses_cost</c>. The one place a property name
    /// becomes a key in this document, so the two sides of the contract cannot drift by somebody
    /// typing a key out by hand.
    /// </summary>
    private static string SnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }

        return sb.ToString();
    }
}
