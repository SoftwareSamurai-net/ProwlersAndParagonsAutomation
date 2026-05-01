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
        var stepCount = _steps.Count;
        var current = 0;

        while (current < stepCount)
        {
            var step = _steps[current];

            AnsiConsole.Clear();
            _budget.Render(sheet);
            AnsiConsole.WriteLine();

            step.Execute(sheet, _rules, _costs, _derived);

            // GM Review is the terminus — no navigation prompt.
            if (step.StepId == "gm_review")
                break;

            var direction = PromptNavigation(current, stepCount, _steps);
            current += direction; // -1 = back, +1 = forward
        }
    }

    private static int PromptNavigation(int current, int stepCount, IReadOnlyList<IWizardStep> steps)
    {
        AnsiConsole.WriteLine();

        var choices = new List<string>();
        if (current > 0)
            choices.Add($"← Back  (Step {current}: {steps[current - 1].DisplayName})");

        var isLast = current == stepCount - 2; // one before gm_review
        var nextLabel = isLast
            ? $"Continue to Step {current + 2}: {steps[current + 1].DisplayName} →"
            : $"Continue  (Step {current + 2}: {steps[current + 1].DisplayName}) →";
        choices.Add(nextLabel);

        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[grey]─── Navigation ───[/]")
                .AddChoices(choices));

        return pick.StartsWith("←") ? -1 : 1;
    }
}
