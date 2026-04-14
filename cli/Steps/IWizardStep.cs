using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Cli.Steps;

public interface IWizardStep
{
    string StepId { get; }
    void Execute(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived);
}
