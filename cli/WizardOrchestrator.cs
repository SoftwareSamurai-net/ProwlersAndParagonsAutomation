using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Cli.Steps;
using ProwlersAndParagonsAutomation.Engine;
using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli;

public sealed class WizardOrchestrator
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;
    private readonly CharacterValidator _validator;
    private readonly HpBudgetDisplay _budget;
    private readonly IReadOnlyList<IWizardStep> _steps;

    public WizardOrchestrator(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        CharacterValidator validator,
        string projectRoot)
    {
        _rules     = rules;
        _costs     = costs;
        _derived   = derived;
        _validator = validator;
        _budget    = new HpBudgetDisplay(rules, costs);

        var exporter = new CharacterSheetExporter();

        _steps =
        [
            new ChooseTierStep(),
            new BuyCharacteristicsStep(),
            new ChooseGearStep(),
            new CalculateDerivedStep(),
            new FinishingTouchesStep(),
            new GmReviewStep(validator, exporter, projectRoot),
        ];
    }

    public void Run()
    {
        AnsiConsole.Clear();

        AnsiConsole.Write(
            new FigletText("P&P Wizard")
                .Centered()
                .Color(Color.Gold1));

        AnsiConsole.Write(new Rule("[grey]Prowlers & Paragons Ultimate Edition — Character Creation[/]"));
        AnsiConsole.WriteLine();

        var sheet = new CharacterSheet();

        foreach (var step in _steps)
        {
            AnsiConsole.Clear();
            _budget.Render(sheet);
            AnsiConsole.WriteLine();

            step.Execute(sheet, _rules, _costs, _derived);

            if (step.StepId != "gm_review")
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Press Enter to continue to the next step...[/]");
                Console.ReadLine();
            }
        }
    }
}
