using ProwlersAndParagonsAutomation.Cli;
using ProwlersAndParagonsAutomation.Engine;

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);

var rules     = RulesRepository.FromBasePath(projectRoot);
var costs     = new CostCalculator(rules);
var derived   = new DerivedStatsCalculator(rules);
var validator = new CharacterValidator(rules, costs, derived);

var wizard = new WizardOrchestrator(rules, costs, derived, validator, projectRoot);
wizard.Run();

// ── Helpers ────────────────────────────────────────────────────────────────

static string FindProjectRoot(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
            return dir.FullName;
        dir = dir.Parent;
    }
    return startPath;
}
