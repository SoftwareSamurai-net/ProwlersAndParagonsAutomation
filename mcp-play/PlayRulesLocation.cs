namespace ProwlersAndParagonsAutomation.McpPlay;

/// <summary>
/// Where the play rules are, given where the character rules are.
///
/// <para><b>There is one variable and one argument, and this is why.</b>
/// <c>RulesLocation</c> — shared with the character server — answers "where is
/// <c>data/rules</c>", and the play rules are <c>data/rules/play</c> in the repository, beside
/// the binary, and in a directory somebody points <c>PROWLERS_RULES_DIR</c> at. A second
/// environment variable for the second store would double the number of ways a stuck reader can
/// be pointed at rules that are not the ones they meant, for a directory that has never been
/// anywhere but under the first.</para>
///
/// <para><b>It refuses rather than guessing</b>, for the reason <c>RulesLocation</c> does: a
/// repository built for a directory that is not there gets as far as a connected session and then
/// answers every question with an error, several layers from the cause. A character-rules
/// directory with no <c>play</c> under it is the shape a reader gets by pointing
/// <c>PROWLERS_RULES_DIR</c> at a checkout of an older version, and it is a refusal at startup.
/// </para>
/// </summary>
public static class PlayRulesLocation
{
    /// <summary>The subdirectory of the character rules the five play files live in.</summary>
    public const string SubDirectory = "play";

    /// <summary>Where the play rules are, or why this program is not going to start.</summary>
    /// <param name="Directory">The directory holding the five play rules files, or null.</param>
    /// <param name="Refusal">Why not, ready to print. Null when <paramref name="Directory"/> is set.</param>
    public readonly record struct Located(string? Directory, string? Refusal);

    /// <summary>
    /// The play rules beside the character rules, or a refusal naming both directories.
    /// </summary>
    /// <param name="characterRulesDirectory">What <c>RulesLocation.Find</c> settled on.</param>
    /// <param name="directoryExists">Whether a directory is there. Injected so a test does not
    /// have to build one on the machine it is running on.</param>
    public static Located Find(string characterRulesDirectory, Func<string, bool> directoryExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterRulesDirectory);
        ArgumentNullException.ThrowIfNull(directoryExists);

        var candidate = Path.Combine(characterRulesDirectory, SubDirectory);

        return directoryExists(candidate)
            ? new Located(candidate, null)
            : new Located(null,
                $"The play rules could not be found. The character rules are in "
                + $"'{characterRulesDirectory}', and this program needs the five play rules files "
                + $"in a '{SubDirectory}' folder under it — '{candidate}', which is not a directory "
                + "on this machine. It is not guessed past: this server resolves fights, and a "
                + "fight resolved without the rules that resolve it is not a thing it can offer.");
    }

    /// <summary>The real thing: the disk.</summary>
    public static Located Find(string characterRulesDirectory) =>
        Find(characterRulesDirectory, Directory.Exists);
}
