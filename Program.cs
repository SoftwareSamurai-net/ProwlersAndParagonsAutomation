using ProwlersAndParagonsAutomation.Cli;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

var projectRoot = FindProjectRoot(AppContext.BaseDirectory);

var rules     = RulesRepository.FromBasePath(projectRoot);
var costs     = new CostCalculator(rules);
var derived   = new DerivedStatsCalculator(rules);
var validator = new CharacterValidator(rules, costs, derived);

// The wizard is still what `dotnet run` with no arguments does. `build` is the way in for a
// caller that cannot answer questions — see BuildCommand for why that is a separate command
// rather than a flag on the wizard.
if (args.Length > 0 && args[0] == BuildCommand.Verb)
{
    return new BuildCommand(rules, costs, derived, validator)
        .Run(args[1..], projectRoot, Console.Out, Console.Error, Console.In);
}

if (args.Length > 0)
{
    Console.Error.WriteLine($"'{args[0]}' is not a command this program has.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Run with no arguments for the character wizard, or:");
    Console.Error.WriteLine();
    Console.Error.WriteLine(BuildCommand.Usage);
    return 2;
}

// A prompt that cannot be answered used to throw out of the first step, after the tier table
// had already rendered — a stack trace where the answer is "you wanted the other command".
if (!InteractiveTerminal.IsAvailable)
{
    Console.Error.WriteLine(InteractiveTerminal.UnavailableMessage);
    return 2;
}

var wizard = new WizardOrchestrator(rules, costs, derived, validator, projectRoot);
wizard.Run();
return 0;

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
