namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>
/// Where the rules files are, for a program an MCP client starts from a directory of its own
/// choosing.
///
/// <para>The terminal wizard finds the repository by walking up for a <c>.sln</c>, which is
/// right for a program run out of a checkout and wrong for this one: a client launches the
/// published binary with whatever working directory it likes, and there may be no repository
/// on the machine at all. So the copy that ships beside the binary is the answer, and the
/// two overrides exist for the cases where it is not.</para>
/// </summary>
public static class RulesLocation
{
    /// <summary>
    /// The environment variable a user can set to point at a directory of rules files —
    /// their own, or a checkout's, rather than the copy beside the binary.
    /// </summary>
    public const string OverrideVariable = "PROWLERS_RULES_DIR";

    /// <summary>
    /// The first directory that holds the rules, or null if none of the candidates does.
    ///
    /// <para>Returning null rather than a guess is deliberate. A repository built for a
    /// directory that is not there throws on the first tool call, several layers from the
    /// cause, and over a transport where the message may not reach anybody.</para>
    /// </summary>
    /// <param name="explicitPath">A directory named on the command line, if any.</param>
    /// <param name="environment">Reads an environment variable. Injected so a test does not
    /// have to set one on the machine it is running on.</param>
    /// <param name="baseDirectory">Where the binary is.</param>
    /// <param name="directoryExists">Whether a directory is there. Injected for the same reason.</param>
    public static string? Find(
        string? explicitPath,
        Func<string, string?> environment,
        string baseDirectory,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(directoryExists);

        foreach (var candidate in Candidates(explicitPath, environment, baseDirectory))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && directoryExists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>The real thing: the process's environment, its own directory, and the disk.</summary>
    public static string? Find(string? explicitPath) =>
        Find(explicitPath, Environment.GetEnvironmentVariable, AppContext.BaseDirectory, Directory.Exists);

    /// <summary>
    /// In order of precedence: what the user said on the command line, what they put in the
    /// environment, the copy beside the binary, and — last — a repository above it, so that
    /// <c>dotnet run</c> during development picks up edits to the rules files.
    /// </summary>
    public static IEnumerable<string> Candidates(
        string? explicitPath, Func<string, string?> environment, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (explicitPath is not null) yield return explicitPath;

        if (environment(OverrideVariable) is { } fromEnvironment) yield return fromEnvironment;

        yield return Path.Combine(baseDirectory, "data", "rules");

        // bin/<configuration>/<framework>/ is three deep, and a checkout may be deeper still,
        // so this walks rather than counting directories.
        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, "data", "rules");
            directory = directory.Parent;
        }
    }

    /// <summary>
    /// What to say when none of them is there. It names the variable, because the person
    /// reading this is looking at a client's log with no idea where the program looked.
    /// </summary>
    public static string NotFoundMessage(string baseDirectory) =>
        $"The rules files could not be found. Looked beside the program ({baseDirectory}) and "
        + $"in every directory above it, for a data/rules folder. Set {OverrideVariable} to the "
        + "directory holding tiers.json and the rest, or pass it as the first argument.";
}
