using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public interface IWizardStep
{
    string StepId { get; }
    string DisplayName { get; }
    void Execute(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived);
}
