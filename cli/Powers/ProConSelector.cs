using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Powers;

public sealed class ProConSelector
{
    private readonly ProConApplicability _applicability;

    public ProConSelector(RulesRepository rules) => _applicability = new ProConApplicability(rules);

    public List<SelectedProCon> SelectPros(PowerModel power)
    {
        var selected = new List<SelectedProCon>();

        // The Power's own Pros come first: they are printed in its rulebook entry, so
        // they are the ones a player reading the book expects to see offered.
        var specific = power.PowerPros.ToList();

        // Derived from each Pro's own statement of what it applies to, not from a list
        // curated on the Power. Only Range and Rank type rule anything out; a Pro whose
        // remaining constraint cannot be checked is offered with that caveat shown.
        var available = _applicability.ProsFor(power);

        if (specific.Count == 0 && available.Count == 0) return selected;

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Pros already added:[/] " +
                (selected.Count == 0
                    ? "[grey]none[/]"
                    : string.Join(", ", selected.Select(s => Markup.Escape(s.Id)))));

            var choices = specific
                .Select(FormatPowerProCon)
                .Concat(available.Select(FormatPro))
                .Prepend("Done — no more pros")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]Add a Pro? (optional)[/]")
                    .AddChoices(choices));

            if (pick.StartsWith("Done", StringComparison.Ordinal)) break;

            if (specific.FirstOrDefault(s => FormatPowerProCon(s) == pick) is { } own)
            {
                selected.Add(BuildSelection(own));
                AnsiConsole.MarkupLine($"  [green]Pro added:[/] {Markup.Escape(own.Name)}");
                continue;
            }

            var proModel = available.First(p => FormatPro(p) == pick);

            if (proModel.CostModifierRange is not null)
            {
                // Only the grades this Power may pick — a Power that reaches an option through
                // its own printed text does not get every grade the option prices, because
                // those grades encode a Range it does not have. Asked of the engine so the
                // wizard cannot offer what the validator would refuse.
                var grades = ProConApplicability.GradesFor(
                    proModel, power, proModel.CostModifierRange.Keys);

                var variantKey = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Select variant for [bold]{Markup.Escape(proModel.Name)}[/]:")
                        .UseConverter(k => $"{k}  (+{proModel.CostModifierRange[k]} HP)")
                        .AddChoices(grades));

                selected.Add(new SelectedProCon(proModel.Id, variantKey));
            }
            else
            {
                selected.Add(new SelectedProCon(proModel.Id));
            }

            AnsiConsole.MarkupLine($"  [green]Pro added:[/] {Markup.Escape(proModel.Name)}");
        }

        return selected;
    }

    public List<SelectedProCon> SelectCons(PowerModel power)
    {
        var selected = new List<SelectedProCon>();

        var specific = power.PowerCons.ToList();

        var available = _applicability.ConsFor(power);

        if (specific.Count == 0 && available.Count == 0) return selected;

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Cons already added:[/] " +
                (selected.Count == 0
                    ? "[grey]none[/]"
                    : string.Join(", ", selected.Select(s => Markup.Escape(s.Id)))));

            var choices = specific
                .Select(FormatPowerProCon)
                .Concat(available.Select(FormatCon))
                .Prepend("Done — no more cons")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]Add a Con? (optional)[/]")
                    .AddChoices(choices));

            if (pick.StartsWith("Done", StringComparison.Ordinal)) break;

            if (specific.FirstOrDefault(s => FormatPowerProCon(s) == pick) is { } own)
            {
                selected.Add(BuildSelection(own));
                AnsiConsole.MarkupLine($"  [yellow]Con added:[/] {Markup.Escape(own.Name)}");
                continue;
            }

            var conModel = available.First(c => FormatCon(c) == pick);

            if (conModel.CostModifierRange is not null)
            {
                var variantKey = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Select variant for [bold]{Markup.Escape(conModel.Name)}[/]:")
                        .UseConverter(k => $"{k}  ({conModel.CostModifierRange[k]} HP)")
                        .AddChoices(conModel.CostModifierRange.Keys));

                selected.Add(new SelectedProCon(conModel.Id, variantKey));
            }
            else
            {
                selected.Add(new SelectedProCon(conModel.Id));
            }

            AnsiConsole.MarkupLine($"  [yellow]Con added:[/] {Markup.Escape(conModel.Name)}");
        }

        return selected;
    }

    /// <summary>
    /// Prompts for whatever a Power-specific Pro or Con still needs: a variant when it is
    /// graded, and a quantity when it scales (how many extra Sources, for instance).
    /// </summary>
    private static SelectedProCon BuildSelection(PowerProConModel entry)
    {
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(entry.Description)}[/]");

        string? variantKey = null;
        if (entry.NeedsVariant)
        {
            var options = entry.CostModifierRange is not null
                ? entry.CostModifierRange.ToDictionary(kv => kv.Key, kv => (double)kv.Value)
                : entry.CostPerRankRange!.ToDictionary(kv => kv.Key, kv => kv.Value);

            var suffix = entry.CostType == "per_rank_variable" ? " HP per rank" : " HP";
            var byLabel = options.ToDictionary(
                kv => $"{kv.Key.Replace('_', ' ')} — {kv.Value:+#;-#;0}{suffix}",
                kv => kv.Key);

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Which version of [bold]{Markup.Escape(entry.Name)}[/]?")
                    .AddChoices(byLabel.Keys));

            variantKey = byLabel[pick];
        }

        int? units = null;
        if (entry.CostType == "per_rank_per_unit")
        {
            units = AnsiConsole.Prompt(
                new TextPrompt<int>($"How many {Markup.Escape(entry.CostUnitLabel ?? "unit")}(s)?")
                    .DefaultValue(1)
                    .Validate(u => u >= 1
                        ? Spectre.Console.ValidationResult.Success()
                        : Spectre.Console.ValidationResult.Error("Must be at least 1.")));
        }

        return new SelectedProCon(entry.Id, variantKey) { Units = units };
    }

    private static string FormatPowerProCon(PowerProConModel e)
    {
        var cost = e.CostType switch
        {
            "flat"              => $"{e.CostModifier:+#;-#;0} HP",
            "per_rank"          => $"{e.CostPerRank:+#;-#;0} HP per rank",
            "per_unit"          => $"{e.CostPerUnit:+#;-#;0} HP per {e.CostUnitLabel}",
            "per_rank_per_unit" => $"{e.CostPerRank:+#;-#;0} HP per rank per {e.CostUnitLabel}",
            "flat_variable"     => string.Join(" / ",
                                       (e.CostModifierRange ?? new Dictionary<string, int>())
                                           .Select(kv => $"{kv.Value:+#;-#;0}")) + " HP",
            "per_rank_variable" => string.Join(" / ",
                                       (e.CostPerRankRange ?? new Dictionary<string, double>())
                                           .Select(kv => $"{kv.Value:+#;-#;0}")) + " HP per rank",
            _ => e.CostType
        };

        // Marked so it is obvious these come from the Power's own entry.
        return $"{e.Name}  ({cost})  ‹this Power›";
    }

    private static string FormatPro(ProModel p)
    {
        var cost = p.CostModifier.HasValue
            ? $"+{p.CostModifier} HP"
            : p.CostModifierRange is not null
                ? "variable: " + string.Join(" / ", p.CostModifierRange.Select(kv => $"{kv.Key} +{kv.Value}"))
                : "special";
        return $"{p.Name}  ({cost}){Caveat(p)}";
    }

    private static string FormatCon(ConModel c)
    {
        var cost = c.CostModifier.HasValue
            ? $"{c.CostModifier} HP"
            : c.CostModifierRange is not null
                ? "variable: " + string.Join(" / ", c.CostModifierRange.Select(kv => $"{kv.Key} {kv.Value}"))
                : "special";
        return $"{c.Name}  ({cost}){Caveat(c)}";
    }

    /// <summary>
    /// Some options state a condition the rules data cannot check — Armor Piercing wants a
    /// Power that inflicts physical or energy damage, Constant one that can be switched off.
    /// Deciding that per Power would mean inventing data the rulebook does not give, so the
    /// condition is shown to the player and the GM approves it, which is how Ch.2 frames
    /// the whole list anyway.
    /// </summary>
    private static string Caveat(IGenericProCon option) =>
        option.ApplicabilityCaveat is null ? "" : $"  — {option.ApplicabilityCaveat}";
}
