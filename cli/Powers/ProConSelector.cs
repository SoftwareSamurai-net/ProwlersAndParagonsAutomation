using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Powers;

public sealed class ProConSelector
{
    private readonly RulesRepository _rules;

    public ProConSelector(RulesRepository rules) => _rules = rules;

    public List<SelectedProCon> SelectPros(PowerModel power)
    {
        var selected = new List<SelectedProCon>();

        var available = power.AvailablePros
            .Select(id => _rules.GetPro(id))
            .Where(p => p is not null)
            .Cast<ProModel>()
            .ToList();

        if (available.Count == 0) return selected;

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Pros already added:[/] " +
                (selected.Count == 0
                    ? "[grey]none[/]"
                    : string.Join(", ", selected.Select(s => Markup.Escape(s.Id)))));

            var choices = available
                .Select(p => FormatPro(p))
                .Prepend("Done — no more pros")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]Add a Pro? (optional)[/]")
                    .AddChoices(choices));

            if (pick.StartsWith("Done")) break;

            var proModel = available.First(p => FormatPro(p) == pick);

            if (proModel.CostModifierRange is not null)
            {
                var variantKey = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Select variant for [bold]{Markup.Escape(proModel.Name)}[/]:")
                        .UseConverter(k => $"{k}  (+{proModel.CostModifierRange[k]} HP)")
                        .AddChoices(proModel.CostModifierRange.Keys));

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

        var available = power.AvailableCons
            .Select(id => _rules.GetCon(id))
            .Where(c => c is not null)
            .Cast<ConModel>()
            .ToList();

        if (available.Count == 0) return selected;

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Cons already added:[/] " +
                (selected.Count == 0
                    ? "[grey]none[/]"
                    : string.Join(", ", selected.Select(s => Markup.Escape(s.Id)))));

            var choices = available
                .Select(c => FormatCon(c))
                .Prepend("Done — no more cons")
                .ToList();

            var pick = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[grey]Add a Con? (optional)[/]")
                    .AddChoices(choices));

            if (pick.StartsWith("Done")) break;

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

    private static string FormatPro(ProModel p)
    {
        var cost = p.CostModifier.HasValue
            ? $"+{p.CostModifier} HP"
            : "variable: " + string.Join(" / ",
                p.CostModifierRange!.Select(kv => $"{kv.Key} +{kv.Value}"));
        return $"{p.Name}  ({cost})";
    }

    private static string FormatCon(ConModel c)
    {
        var cost = c.CostModifier.HasValue
            ? $"{c.CostModifier} HP"
            : "variable: " + string.Join(" / ",
                c.CostModifierRange!.Select(kv => $"{kv.Key} {kv.Value}"));
        return $"{c.Name}  ({cost})";
    }
}
