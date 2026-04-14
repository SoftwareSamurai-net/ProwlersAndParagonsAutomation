using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli;

public sealed class HpBudgetDisplay
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;

    public HpBudgetDisplay(RulesRepository rules, CostCalculator costs)
    {
        _rules = rules;
        _costs = costs;
    }

    public void Render(CharacterSheet sheet)
    {
        var tier = sheet.SelectedTierId is null
            ? null
            : _rules.GetTier(sheet.SelectedTierId);

        string content;
        Color borderColor;

        if (tier is null)
        {
            content     = "[grey]No tier selected yet.[/]";
            borderColor = Color.Grey;
        }
        else
        {
            var budget    = tier.HeroPoints;
            var spent     = _costs.TotalCost(sheet);
            var remaining = budget - spent;

            borderColor = remaining < 0 ? Color.Red
                        : remaining <= 10 ? Color.Yellow
                        : Color.Green;

            var remainingMarkup = remaining < 0
                ? $"[red]{remaining} HP[/]"
                : remaining <= 10
                    ? $"[yellow]{remaining} HP[/]"
                    : $"[green]{remaining} HP[/]";

            content = $"[bold]Tier:[/] {Markup.Escape(tier.Name)}   " +
                      $"[bold]Budget:[/] {budget} HP   " +
                      $"[bold]Spent:[/] {spent} HP   " +
                      $"[bold]Remaining:[/] {remainingMarkup}   " +
                      $"[bold]Trait Cap:[/] {tier.TraitCapRank}d";
        }

        AnsiConsole.Write(
            new Panel(new Markup(content))
                .Header("[bold yellow] ★ HP Budget [/]")
                .BorderColor(borderColor));
    }
}
