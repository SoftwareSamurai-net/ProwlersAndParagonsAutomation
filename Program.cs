using ProwlersAndParagonsAutomation.Cli;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);

var rules     = RulesRepository.FromBasePath(projectRoot);
var costs     = new CostCalculator(rules);
var derived   = new DerivedStatsCalculator(rules);
var validator = new CharacterValidator(rules, costs, derived);

// Everything about what the arguments mean is in CommandLine, so that it can be tested. This
// file is the wiring: real services, the real console, the real terminal check.
var build = new BuildCommand(rules, costs, derived, validator);

var commandLine = new CommandLine(
    build,
    // The live database, reached through wrangler under this machine's own login — the same
    // road the vault's pull script takes, and the only one there is (see WranglerDatabase).
    new PushCommand(build, costs, new WranglerDatabase(projectRoot)),
    () => new WizardOrchestrator(rules, costs, derived, validator, projectRoot).Run(),
    () => InteractiveTerminal.IsAvailable);

return commandLine.Run(args, projectRoot, Console.Out, Console.Error, Console.In);

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
