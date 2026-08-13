namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>What the arguments meant, or what was wrong with them.</summary>
/// <param name="Help">Print the usage and stop.</param>
/// <param name="RulesDirectory">A directory of rules files the user named, if any.</param>
/// <param name="Error">Why the arguments made no sense. Null when they did.</param>
public sealed record ServerArguments(bool Help, string? RulesDirectory, string? Error);

/// <summary>
/// What this program does with its arguments. There are only two — a directory and
/// <c>--help</c> — and it is a class rather than four lines at the top of <c>Program.cs</c>
/// for the reason the CLI's own <c>CommandLine</c> exists: those lines are the ones no test
/// can reach, and the last time they were left there one of them answered a misspelled
/// argument with an exit code and an empty stream.
///
/// <para><b>An argument this program does not have is refused rather than ignored.</b>
/// Silently ignoring <c>--rules</c> or <c>--help-me</c> leaves somebody watching a client's
/// log wondering why the flag they passed did nothing.</para>
/// </summary>
public static class CommandLine
{
    public static ServerArguments Read(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? directory = null;

        foreach (var argument in args)
        {
            if (argument is "--help" or "-h")
                return new ServerArguments(true, null, null);

            if (argument.StartsWith('-'))
                return new ServerArguments(false, null,
                    $"'{argument}' is not an option this program has. The only one is --help; "
                    + "anything else is taken as the directory holding the rules files.");

            if (directory is not null)
                return new ServerArguments(false, null,
                    "Only one directory of rules files can be given, and two were: "
                    + $"'{directory}' and '{argument}'.");

            directory = argument;
        }

        return new ServerArguments(false, directory, null);
    }
}
