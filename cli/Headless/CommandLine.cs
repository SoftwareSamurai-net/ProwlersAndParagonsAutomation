using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Cli.Headless;

/// <summary>
/// What the program does with its arguments: run the wizard, run <see cref="BuildCommand"/>,
/// or explain itself and stop.
///
/// <para><b>It is a class rather than the top of <c>Program.cs</c> so that it can be tested.</b>
/// It was six lines of top-level statements, and every one of them was uncovered — the verb
/// routing, the unknown-command branch, the exit codes and the terminal check. One of those
/// six lines answered an unknown verb with exit 2 and an empty standard output, which
/// contradicted the contract the command publishes, and no test could see it.</para>
/// </summary>
public sealed class CommandLine
{
    private readonly BuildCommand _build;
    private readonly Action _runWizard;
    private readonly Func<bool> _terminalIsInteractive;

    /// <param name="build">The headless command.</param>
    /// <param name="runWizard">Starts the terminal wizard. Called only when a terminal can be
    /// read, and never by a test — which is why it is a delegate.</param>
    /// <param name="terminalIsInteractive">Whether the wizard can prompt. Injected so the
    /// non-interactive path is reachable from a test; the real one is
    /// <see cref="InteractiveTerminal.IsAvailable"/>.</param>
    public CommandLine(BuildCommand build, Action runWizard, Func<bool> terminalIsInteractive)
    {
        _build = build;
        _runWizard = runWizard;
        _terminalIsInteractive = terminalIsInteractive;
    }

    public int Run(IReadOnlyList<string> args, string projectRoot,
                   TextWriter stdout, TextWriter stderr, TextReader stdin)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Count > 0 && args[0] == BuildCommand.Verb)
            return _build.Run([.. args.Skip(1)], projectRoot, stdout, stderr, stdin);

        if (args.Count > 0)
        {
            // Through BuildCommand's own reporting, not a bare stderr line. A caller that
            // misspells the verb is in exactly the position the report exists for, and used to
            // get exit 2 with nothing to parse — the one case where "the arguments made no
            // sense" produced no account of what did not make sense.
            stderr.WriteLine("Run with no arguments for the character wizard, or:");
            stderr.WriteLine();
            stderr.WriteLine(BuildCommand.Usage);

            return BuildCommand.ReportArgumentError(stdout,
                $"'{args[0]}' is not a command this program has. "
                + $"The only one is '{BuildCommand.Verb}'.");
        }

        // No arguments: the wizard, which is a conversation and needs a terminal it can read.
        if (!_terminalIsInteractive())
        {
            stderr.WriteLine(InteractiveTerminal.UnavailableMessage);
            return BuildCommand.InputUnusable;
        }

        _runWizard();
        return BuildCommand.Ok;
    }
}
