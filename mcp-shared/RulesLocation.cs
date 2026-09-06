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

    /// <summary>Where the rules are, or why the program is not going to start.</summary>
    /// <param name="Directory">The directory holding the rules files, or null.</param>
    /// <param name="Refusal">Why not, ready to print. Null when <paramref name="Directory"/> is set.</param>
    public readonly record struct Located(string? Directory, string? Refusal);

    /// <summary>
    /// The first directory that holds the rules, or a refusal.
    ///
    /// <para>Refusing rather than guessing is deliberate. A repository built for a directory
    /// that is not there throws on the first tool call, several layers from the cause, and over
    /// a transport where the message may not reach anybody.</para>
    ///
    /// <para><b>A directory the user named and that is not there is a refusal, not a candidate
    /// that failed.</b> It used to fall through to the copy beside the binary, so a typo in
    /// <see cref="OverrideVariable"/> — which is what docs/MCP-SETUP.md tells a stuck user to set —
    /// produced a working server running on somebody else's rules and no message at all.</para>
    /// </summary>
    /// <param name="explicitPath">A directory named on the command line, if any.</param>
    /// <param name="environment">Reads an environment variable. Injected so a test does not
    /// have to set one on the machine it is running on.</param>
    /// <param name="baseDirectory">Where the binary is.</param>
    /// <param name="directoryExists">Whether a directory is there. Injected for the same reason.</param>
    public static Located Find(
        string? explicitPath,
        Func<string, string?> environment,
        string baseDirectory,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(directoryExists);

        foreach (var (path, named) in Named(explicitPath, environment))
        {
            if (string.IsNullOrWhiteSpace(path))
                return new Located(null,
                    $"{named} is set and empty. Unset it to use the copy of the rules that "
                    + "ships beside this program; an empty value is refused rather than "
                    + "treated as though it had never been set.");

            if (directoryExists(path)) return new Located(path, null);

            return new Located(null,
                $"{named} names '{path}', which is not a directory on this machine. It is not "
                + "guessed past: a typo there would leave this program running on rules that "
                + "are not the ones you meant.");
        }

        foreach (var candidate in Candidates(explicitPath, environment, baseDirectory))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && directoryExists(candidate))
                return new Located(candidate, null);
        }

        return new Located(null, NotFoundMessage(baseDirectory));
    }

    /// <summary>The real thing: the process's environment, its own directory, and the disk.</summary>
    public static Located Find(string? explicitPath) =>
        Find(explicitPath, Environment.GetEnvironmentVariable, AppContext.BaseDirectory, Directory.Exists);

    /// <summary>
    /// The directories the user named, with what to call each of them in a message. At most
    /// one is used: an override that is present and wrong stops the program rather than
    /// deferring to the next candidate.
    /// </summary>
    private static IEnumerable<(string Path, string Named)> Named(
        string? explicitPath, Func<string, string?> environment)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            yield return (explicitPath, "The directory given as the first argument");

        // <b>Set and blank is named, not skipped.</b> Skipping it fell through to the shipped
        // copy in silence, which is the whole failure this method exists to stop — and
        // `"env": {"PROWLERS_RULES_DIR": ""}` in a client's configuration is exactly how it
        // arrives. The blank *argument* was refused a commit earlier, on the path the comment
        // there did not describe.
        else if (environment(OverrideVariable) is { } fromEnvironment)
            yield return (fromEnvironment, OverrideVariable);
    }

    /// <summary>
    /// In order of precedence: what the user said on the command line, what they put in the
    /// environment, then the copy beside the binary and every directory above it — so that
    /// <c>dotnet run</c> during development picks up edits to the rules files.
    ///
    /// <para>The walk starts at the binary's own directory, which is what "beside the binary"
    /// means; there is no separate entry for it. There was, and it yielded the same path
    /// twice, which is how you can tell nobody had traced the loop.</para>
    /// </summary>
    public static IEnumerable<string> Candidates(
        string? explicitPath, Func<string, string?> environment, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);

        foreach (var (path, _) in Named(explicitPath, environment)) yield return path;

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
