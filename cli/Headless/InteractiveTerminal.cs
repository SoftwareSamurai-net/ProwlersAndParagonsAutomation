using Spectre.Console;

namespace ProwlersAndParagonsAutomation.Cli.Headless;

/// <summary>
/// Whether there is a person at the other end of this terminal.
///
/// <para>The wizard is a conversation: every step ends in a Spectre
/// <c>SelectionPrompt</c>, and one of those throws
/// <see cref="NotSupportedException"/> when the terminal cannot be read
/// interactively — piped input, a redirected console, a CI job. Nothing caught it,
/// so a piped run rendered the tier table correctly and then dumped a stack trace,
/// which reads as a broken program rather than as the wrong way to run it.</para>
///
/// <para>There is somewhere to send those callers now, which is why this is fixed
/// here rather than earlier: <see cref="BuildCommand"/> is the non-interactive
/// way in.</para>
/// </summary>
public static class InteractiveTerminal
{
    /// <summary>
    /// True when the wizard can prompt. Both halves are needed: standard input can be
    /// redirected while Spectre still reports a capable terminal, and a terminal that
    /// cannot render its own prompts fails before it ever reads a key.
    /// </summary>
    public static bool IsAvailable =>
        IsAvailableGiven(Console.IsInputRedirected, AnsiConsole.Profile.Capabilities.Interactive);

    /// <summary>
    /// The decision, separated from the two facts it is made of so that both can be tested.
    ///
    /// <para>As one expression it could only ever be checked in the environment a test runner
    /// provides — where both halves happen to say the same thing — so either half could be
    /// deleted and nothing would notice. The comment above claims both are needed; this is what
    /// makes that claim checkable.</para>
    /// </summary>
    public static bool IsAvailableGiven(bool inputRedirected, bool profileIsInteractive) =>
        !inputRedirected && profileIsInteractive;

    /// <summary>
    /// What to say instead of a stack trace. It names the command that does work this way,
    /// because "this needs a terminal" without an alternative is a dead end for exactly the
    /// caller most likely to hit it.
    /// </summary>
    public static string UnavailableMessage =>
        $"""
         The character wizard asks questions, so it needs a terminal it can read.
         This one cannot be read — input is redirected, or the console is not interactive.

         To build a character without a terminal, use the {BuildCommand.Verb} command:

             dotnet run -- {BuildCommand.Verb} --from character.json

         Run 'dotnet run -- {BuildCommand.Verb} --help' for what it accepts.
         """;
}
