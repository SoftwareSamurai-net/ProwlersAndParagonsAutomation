using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProwlersAndParagonsAutomation.Cli.Headless;

/// <summary>
/// The one thing <see cref="PushCommand"/> needs from the database: run some SQL and hand back
/// each statement's rows.
///
/// <para>An interface rather than a class so the command can be driven end to end by a test that
/// records the SQL and scripts the answers — the real one shells out to wrangler, which needs a
/// login and a network and takes seconds per call, none of which a unit test should.</para>
/// </summary>
public interface ICharacterDatabase
{
    /// <summary>
    /// Runs one or more <c>;</c>-separated statements and returns one list of rows per
    /// statement, in order. Throws <see cref="DatabaseException"/> when the database could not
    /// be reached or refused the SQL — never for a statement that simply matched nothing.
    /// </summary>
    IReadOnlyList<IReadOnlyList<JsonObject>> Execute(string sql);
}

/// <summary>The database could not be reached, or refused what it was sent.</summary>
public sealed class DatabaseException : Exception
{
    public DatabaseException() { }
    public DatabaseException(string message) : base(message) { }
    public DatabaseException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// The live D1 database behind the site, reached the way the vault's pull script reaches it:
/// <c>wrangler d1 execute --remote</c>, under whatever <c>wrangler login</c> left on this machine.
///
/// <para><b>This is the only door in.</b> The accounts server (<c>worker/</c>) is same-origin,
/// session-cookie gated and never parses a character, so there is no HTTP route a command-line
/// tool could write through as a user — and wrangler takes SQL and nothing else. The statements
/// this program sends are therefore copies of the server's own (<c>worker/db.js</c>), and a test
/// holds the copy to the original; see <see cref="PushCommand"/>.</para>
///
/// <para><b>The SQL goes as <c>--command</c>, and <c>--file</c> was tried first and does not
/// work.</b> Against a remote database wrangler sends a file through D1's <em>import</em> API: it
/// prints upload progress on standard output ahead of the JSON, and the JSON it then prints is a
/// summary — <c>"Total queries executed": 2</c> — never a statement's rows, so neither the lookup
/// nor <c>RETURNING id</c> can be read back through it. <c>--command</c> answers one
/// <c>{ results, success, meta }</c> per statement, which is what the pull script relies on too.
/// The payload is a whole character as JSON with apostrophes in its prose, which is why the
/// process is started with an argument list and no shell: nothing between here and node gets an
/// opinion about a quote. (On Windows <c>npx.cmd</c> is a batch file and does — the pull script
/// records the same limit — so this is a macOS and Linux tool until that is solved.)</para>
/// </summary>
public sealed class WranglerDatabase : ICharacterDatabase
{
    /// <summary>
    /// The database's name in <c>d1/wrangler.toml</c>. Named here rather than read out of the
    /// file so a typo in the toml fails loudly instead of quietly targeting nothing; a test holds
    /// the two together.
    /// </summary>
    public const string DatabaseName = "prowlers-and-paragons";

    private readonly string _projectRoot;

    /// <param name="projectRoot">The repository root — the directory holding <c>d1/</c>, whose
    /// <c>wrangler.toml</c> is what tells wrangler which database this is.</param>
    public WranglerDatabase(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

    public IReadOnlyList<IReadOnlyList<JsonObject>> Execute(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var d1 = Path.Combine(_projectRoot, "d1");
        if (!File.Exists(Path.Combine(d1, "wrangler.toml")))
            throw new DatabaseException($"There is no d1/wrangler.toml under '{_projectRoot}', so wrangler cannot be told which database to use.");

        // One line. A statement is one statement whether or not it has newlines in it, but the
        // pull script records that a multi-line --command is mangled on one platform and answered
        // 'incomplete input', and there is nothing a newline buys here.
        var command = sql.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ');

        var (exit, stdout, stderr) = RunWrangler(
            ["--yes", "wrangler", "--cwd", d1, "d1", "execute", DatabaseName, "--remote", "--json", "--command", command]);

        if (exit != 0)
            throw new DatabaseException($"wrangler exited {exit}.\n{Tail(stderr)}\n{Tail(stdout)}".Trim());

        return Parse(stdout);
    }

    /// <summary>
    /// Wrangler's <c>--json</c> output: one <c>{ results, success, meta }</c> per statement.
    /// Only <c>results</c> is read; a statement that was not a success is a refusal.
    ///
    /// <para>Anything printed ahead of the array is skipped. <c>--command</c> prints nothing
    /// there today; <c>--file</c> printed upload progress, and a wrangler that grows a banner
    /// should cost a parse and not a push.</para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<JsonObject>> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var start = json.IndexOf('[', StringComparison.Ordinal);
        if (start < 0) throw new DatabaseException($"wrangler answered something that is not JSON:\n{Tail(json)}");

        JsonNode? parsed;
        try { parsed = JsonNode.Parse(json[start..]); }
        catch (JsonException e)
        {
            throw new DatabaseException($"wrangler answered something that is not JSON:\n{Tail(json)}", e);
        }

        if (parsed is not JsonArray statements)
            throw new DatabaseException($"wrangler's answer is not a list of statement results:\n{Tail(json)}");

        var all = new List<IReadOnlyList<JsonObject>>();
        foreach (var statement in statements)
        {
            if (statement is not JsonObject o)
                throw new DatabaseException($"wrangler's answer holds a statement result that is not an object:\n{Tail(json)}");

            if (o["success"] is JsonValue success && success.TryGetValue<bool>(out var ok) && !ok)
                throw new DatabaseException($"the database refused a statement:\n{Tail(json)}");

            all.Add([.. ((o["results"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>()]);
        }

        return all;
    }

    private static (int Exit, string StdOut, string StdErr) RunWrangler(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in arguments) start.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(start)
                ?? throw new DatabaseException("npx could not be started.");

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                throw new DatabaseException("wrangler did not answer within three minutes.");
            }

            return (process.ExitCode, stdout.Result, stderr.Result);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            throw new DatabaseException(
                "npx could not be run. wrangler is reached through npx, so Node and npm have to be on the PATH.", e);
        }
    }

    private static string Tail(string text)
    {
        var lines = text.Split('\n');
        return string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 20))).Trim();
    }
}
