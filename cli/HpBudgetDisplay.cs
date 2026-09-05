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
                      // **The cap in force, not the tier's.** A character carrying a house cap
                      // is bounded and judged by that one, and its Resolve is measured from it —
                      // so a panel above every step printing the tier's would be the one figure
                      // on screen that nothing else in the program agrees with. The wizard has no
                      // step that sets a cap; a character built elsewhere and brought back has
                      // one. See `docs/guide/cli-wizard.md`.
                      $"[bold]Trait Cap:[/] {DerivedStatsCalculator.EffectiveTraitCap(sheet, tier)}d";
        }

        AnsiConsole.Write(
            new Panel(new Markup(content))
                .Header("[bold yellow] ★ HP Budget [/]")
                .BorderColor(borderColor));
    }
}
