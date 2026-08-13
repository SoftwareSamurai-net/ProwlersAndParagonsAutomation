using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Mcp;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The two things about the MCP server that no rendering or protocol test can see: what may
/// be written to standard output, and where the rules are found.
///
/// <para><b>Standard output belongs to the protocol.</b> A stray line — a greeting, a warning,
/// a progress message — lands in the middle of a JSON-RPC stream, and the client drops the
/// session with an error nobody can connect back to the line that caused it. This is a
/// statement about how the source is written, so it is asserted by reading the source, which
/// is the same split the browser front end's tests use: what a thing produces is tested by
/// running it, how it is written is tested by reading it.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class McpStdioTests
{
    private static string McpDirectory => Path.Combine(RulesFixture.RepoRoot, "mcp");

    /// <summary>
    /// A directory that is rooted, is not there, and is spelt the way the host spells one.
    ///
    /// <para><b>These tests built one out of a drive letter, and passed on Windows and failed
    /// in CI.</b> Combining "C:" with a folder name gives a <em>relative</em> path on Linux —
    /// a directory literally called <c>C:</c> — so <see cref="RulesLocation.Candidates"/>'s
    /// walk resolved it against the working directory and started climbing the runner's
    /// checkout. The production code was right and the test was asserting a Windows-shaped
    /// answer, which is the one kind of mistake a green local run cannot show you.</para>
    /// </summary>
    private static string Nowhere(params string[] parts) =>
        Path.GetFullPath(Path.Combine([
            Path.GetTempPath(), "pp-mcp-nowhere-" + nameof(McpStdioTests), .. parts]));

    private static IEnumerable<string> SourceFiles =>
        Directory.EnumerateFiles(McpDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// <b>Nothing in the server writes to standard output.</b> Everything it says to a human
    /// goes to standard error, which every client collects into a log.
    ///
    /// <para>Matched on <c>Console.</c> followed by anything other than <c>Error</c>, rather
    /// than on <c>Console.WriteLine</c> — <c>Console.Out.Write</c>, <c>Console.OpenStandardOutput</c>
    /// and a held reference to <c>Console.Out</c> are the same mistake in three spellings, and a
    /// check for the obvious one would pass while any of them shipped.</para>
    /// </summary>
    [Fact]
    public void NothingWritesToStandardOutput()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // A comment about standard output is not a write to it, and this file's own
                // reasoning is written down in several of them.
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;

                // <b>Every occurrence on the line, not the first.</b> Looking at the first one
                // only, `Console.Error.WriteLine(a); Console.Write(b);` passed whole.
                foreach (var index in Occurrences(line, "Console."))
                {
                    if (line.AsSpan(index).StartsWith("Console.Error", StringComparison.Ordinal))
                        continue;

                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                }

                // The spellings that get at the same stream without the token: an alias or a
                // static import, and the handle itself. `using static System.Console;` and a
                // bare WriteLine is invisible to any search for "Console.".
                foreach (var spelling in new[]
                         {
                             "using static System.Console", "= System.Console", "= Console",
                             "OpenStandardOutput", "SetOut", "Console.Out"
                         })
                {
                    if (line.Contains(spelling, StringComparison.Ordinal))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Standard output carries the protocol and nothing else, so the server may only "
            + "write to Console.Error:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<int> Occurrences(string line, string token)
    {
        for (var index = line.IndexOf(token, StringComparison.Ordinal);
             index >= 0;
             index = line.IndexOf(token, index + 1, StringComparison.Ordinal))
            yield return index;
    }

    /// <summary>
    /// <b>The property itself, read off the stream.</b> The source check above cannot see a
    /// write from a library, from <c>engine/</c> or <c>sheets/</c>, or through a spelling
    /// nobody thought of — so this starts the built program the way a client does and reads
    /// its standard output by hand.
    ///
    /// <para><b>Every line has to be a JSON-RPC message.</b> An earlier version of this test
    /// drove the same binary through the SDK's client and asserted the session worked, under a
    /// comment claiming a stray line would break the handshake. It does not: a real stray line
    /// on that stream, printed before the transport starts, left the client perfectly happy and
    /// the test green. The client skipping what it cannot parse is exactly why this has to look
    /// at the bytes.</para>
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramSpeaksNothingButTheProtocol()
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var server = Start(ServerExecutable(), rulesDirectory: null);

        try
        {
            await Say(server, """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"stdout-hygiene","version":"1"}}}
                """, cancellation);
            await Say(server, """{"jsonrpc":"2.0","method":"notifications/initialized"}""", cancellation);
            await Say(server, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""", cancellation);

            var lines = new List<string>();

            // Reads until the answer to the second request arrives, so the run covers the
            // startup, the handshake and a request — every point at which something could
            // print. A line that is not JSON is a failure whatever else it says.
            while (await server.StandardOutput.ReadLineAsync(cancellation) is { } line)
            {
                if (line.Length == 0) continue;

                lines.Add(line);

                JsonNode node;
                try
                {
                    node = JsonNode.Parse(line)
                           ?? throw new InvalidOperationException("null");
                }
                catch (Exception e) when (e is JsonException or InvalidOperationException)
                {
                    Assert.Fail(
                        "Standard output carries the protocol and nothing else, and this line "
                        + $"is not a JSON-RPC message:\n{line}");
                    return;
                }

                Assert.Equal("2.0", node["jsonrpc"]!.GetValue<string>());

                if (node["id"]?.GetValue<int>() == 2) break;
            }

            // Two answers, and the second one really is the tool list — so the loop above did
            // not fall out of an empty stream having asserted nothing.
            Assert.Equal(2, lines.Count);
            Assert.Contains(CharacterServer.CheckCharacterTool, lines[1], StringComparison.Ordinal);
        }
        finally
        {
            Stop(server);
        }
    }

    /// <summary>
    /// <b>A partial rules directory is refused by the program, not merely by a method it
    /// owns.</b> The startup check has a unit test, and swapping <c>Program.cs</c> back to
    /// warming one catalogue left that test green while the binary started cleanly on a
    /// directory holding a single rules file — the whole bug, restored, invisible. This runs
    /// the program.
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramRefusesAPartialRulesDirectory()
    {
        var cancellation = TestContext.Current.CancellationToken;

        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            File.Copy(Path.Combine(RulesFixture.DataPath, "tiers.json"),
                      Path.Combine(scratch, "tiers.json"));

            using var server = Start(ServerExecutable(), scratch);

            await Ends(server, cancellation);

            Assert.Equal(2, server.ExitCode);

            var said = await server.StandardError.ReadToEndAsync(cancellation);
            Assert.Contains("could not be read", said, StringComparison.OrdinalIgnoreCase);

            // And it said nothing on the stream that belongs to the protocol, even while
            // failing — a client that saw a diagnostic there would report a broken session
            // rather than a missing file.
            Assert.Equal("", (await server.StandardOutput.ReadToEndAsync(cancellation)).Trim());
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// And a directory the user named that is not there, from the outside as well: the fix
    /// that stopped it falling through to the shipped copy is only worth what the program
    /// does with it.
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramRefusesADirectoryThatIsNotThere()
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var server = Start(ServerExecutable(),
            Path.Combine(Path.GetTempPath(), "pp-mcp-not-here-" + Guid.NewGuid().ToString("N")));

        await Ends(server, cancellation);

        Assert.Equal(2, server.ExitCode);
        Assert.Contains("not a directory on this machine",
            await server.StandardError.ReadToEndAsync(cancellation), StringComparison.OrdinalIgnoreCase);
    }

    private static Process Start(string executable, string? rulesDirectory)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // Somewhere with no rules under it, so a run that is meant to fail cannot be
            // rescued by the walk upwards finding this repository's own copy.
            WorkingDirectory = Path.GetTempPath()
        };

        if (rulesDirectory is not null) start.ArgumentList.Add(rulesDirectory);

        return Process.Start(start)
               ?? throw new InvalidOperationException($"'{executable}' did not start.");
    }

    /// <summary>
    /// Waits for a run that is supposed to stop, and fails rather than waiting for ever if it
    /// does not.
    ///
    /// <para><b>A server that keeps running is exactly what these two tests are looking for</b>
    /// — it is what the bug they cover looks like — and a bare wait turns catching it into a
    /// test run that never finishes. Verified by reintroducing the bug: the mutation hung the
    /// suite instead of failing it.</para>
    /// </summary>
    private static async Task Ends(Process server, CancellationToken cancellation)
    {
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        giveUp.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await server.WaitForExitAsync(giveUp.Token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            Stop(server);
            Assert.Fail(
                "The server was supposed to refuse this and stop, and it started instead. It is "
                + "now serving a session on rules it should not have accepted.");
        }
    }

    private static async Task Say(Process server, string message, CancellationToken cancellation)
    {
        await server.StandardInput.WriteLineAsync(message.AsMemory(), cancellation);
        await server.StandardInput.FlushAsync(cancellation);
    }

    private static void Stop(Process server)
    {
        try
        {
            if (!server.HasExited) server.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* already gone */ }
    }

    /// <summary>
    /// The server as it was built beside this test run — same configuration, same framework,
    /// so this cannot end up testing a stale binary from some earlier build.
    /// </summary>
    private static string ServerExecutable()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);          // …/bin/<cfg>/<tfm>/
        var framework = here.Name;
        var configuration = here.Parent!.Name;

        var path = Path.Combine(
            RulesFixture.RepoRoot, "mcp", "bin", configuration, framework,
            OperatingSystem.IsWindows() ? "ProwlersAndParagons.Mcp.exe" : "ProwlersAndParagons.Mcp");

        Assert.True(File.Exists(path),
            $"The MCP server was not built at '{path}'. It is a project reference of this test "
            + "project, so `dotnet test` builds it; a missing binary means the layout moved.");

        return path;
    }

    /// <summary>
    /// It looks beside the binary first, then upwards. A client launches the published program
    /// from a directory of its own choosing and there may be no repository on the machine at
    /// all, which is why the CLI's "walk up for a .sln" is the wrong answer here.
    /// </summary>
    [Fact]
    public void TheRulesAreLookedForBesideTheBinaryFirst()
    {
        var candidates = RulesLocation.Candidates(
            null, _ => null, Nowhere("app", "bin")).ToList();

        Assert.Equal(Nowhere("app", "bin", "data", "rules"), candidates[0]);
        Assert.Contains(Nowhere("app", "data", "rules"), candidates);
    }

    /// <summary>
    /// What the user says wins over what the environment says, and both win over the copy
    /// beside the binary — otherwise an override is only an override when nothing shipped.
    ///
    /// <para><b>Asserted through <c>Find</c> with every candidate existing</b>, not only
    /// through the order <c>Candidates</c> yields. With one directory in existence, reversing
    /// the search order left both precedence tests green while the shipped copy beat an
    /// explicit argument.</para>
    /// </summary>
    [Fact]
    public void AnExplicitDirectoryAndTheEnvironmentBothOutrankTheShippedCopy()
    {
        static string? Environment(string name) =>
            name == RulesLocation.OverrideVariable ? "from-environment" : null;

        var beside = Nowhere("app", "data", "rules");

        Assert.Equal("explicit",
            RulesLocation.Find("explicit", Environment, Nowhere("app"), _ => true).Directory);

        Assert.Equal("from-environment",
            RulesLocation.Find(null, Environment, Nowhere("app"), _ => true).Directory);

        Assert.Equal(beside,
            RulesLocation.Find(null, _ => null, Nowhere("app"), _ => true).Directory);
    }

    /// <summary>
    /// <b>A directory the user named and that is not there is refused, not skipped.</b> The
    /// README's troubleshooting tells a stuck user to set that variable; when it fell through
    /// to the shipped copy, a typo produced a server that worked perfectly on rules that were
    /// not the ones they meant, and said nothing.
    /// </summary>
    [Fact]
    public void ADirectoryTheUserNamedAndThatIsNotThereIsRefused()
    {
        var everythingElseExists = new Func<string, bool>(d => d != "wrong");

        var fromArgument = RulesLocation.Find(
            "wrong", _ => null, Nowhere("app"), everythingElseExists);

        Assert.Null(fromArgument.Directory);
        Assert.Contains("wrong", fromArgument.Refusal!, StringComparison.Ordinal);
        Assert.Contains("first argument", fromArgument.Refusal!, StringComparison.OrdinalIgnoreCase);

        var fromEnvironment = RulesLocation.Find(
            null, v => v == RulesLocation.OverrideVariable ? "wrong" : null,
            Nowhere("app"), everythingElseExists);

        Assert.Null(fromEnvironment.Directory);
        Assert.Contains(RulesLocation.OverrideVariable, fromEnvironment.Refusal!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Set and empty is refused too.</b> It was skipped, so it fell through to the shipped
    /// copy in silence — the failure the refusal above exists to stop, on the one path it did
    /// not cover. `"env": {"PROWLERS_RULES_DIR": ""}` in a client's configuration is how it
    /// arrives, and an empty entry there is easier to write than a wrong one.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOverrideThatIsSetAndBlankIsRefusedRatherThanIgnored(string blank)
    {
        var located = RulesLocation.Find(
            null, v => v == RulesLocation.OverrideVariable ? blank : null,
            Nowhere("app"), _ => true);

        Assert.Null(located.Directory);
        Assert.Contains(RulesLocation.OverrideVariable, located.Refusal!, StringComparison.Ordinal);
        Assert.Contains("empty", located.Refusal!, StringComparison.OrdinalIgnoreCase);

        // And unset still means "use the copy beside the binary", which is the whole point of
        // telling the two apart.
        Assert.NotNull(RulesLocation.Find(null, _ => null, Nowhere("app"), _ => true).Directory);
    }

    /// <summary>
    /// <b>No rules is a refusal, not a guess.</b> A repository built for a directory that is
    /// not there gets as far as a connected session and then answers every question with an
    /// error, several layers from the cause.
    /// </summary>
    [Fact]
    public void NoRulesAnywhereIsAnAnswerRatherThanAGuess()
    {
        var nowhere = RulesLocation.Find(null, _ => null, Nowhere("app"), _ => false);

        Assert.Null(nowhere.Directory);
        Assert.Contains(RulesLocation.OverrideVariable, nowhere.Refusal!, StringComparison.Ordinal);

        Assert.Equal(
            Nowhere("app", "data", "rules"),
            RulesLocation.Find(null, _ => null, Nowhere("app"),
                d => d == Nowhere("app", "data", "rules")).Directory);
    }

    /// <summary>
    /// The arguments, which were four lines at the top of <c>Program.cs</c> where nothing
    /// could reach them. An option this program does not have is refused rather than ignored:
    /// somebody who passes <c>--rules</c> and sees it do nothing has no way to find out why.
    /// </summary>
    [Fact]
    public void TheArgumentsAreReadAndAnUnknownOneIsRefused()
    {
        Assert.True(CommandLine.Read(["--help"]).Help);
        Assert.True(CommandLine.Read(["-h"]).Help);
        Assert.True(CommandLine.Read(["some/dir", "--help"]).Help);

        Assert.Equal("some/dir", CommandLine.Read(["some/dir"]).RulesDirectory);
        Assert.Null(CommandLine.Read([]).RulesDirectory);
        Assert.Null(CommandLine.Read([]).Error);

        Assert.Contains("--rules", CommandLine.Read(["--rules", "x"]).Error!, StringComparison.Ordinal);
        Assert.Contains("Only one", CommandLine.Read(["a", "b"]).Error!, StringComparison.Ordinal);

        // A blank argument — a quoted empty variable in a client's configuration — is the one
        // shape that still fell through to the shipped copy without saying anything.
        Assert.Contains("blank", CommandLine.Read([" "]).Error!, StringComparison.Ordinal);
        Assert.Contains("blank", CommandLine.Read([""]).Error!, StringComparison.Ordinal);
    }

    /// <summary>
    /// And when there are none it says where it looked and how to say otherwise, because the
    /// person reading that line is looking at a client's log with no idea what the program's
    /// working directory was.
    /// </summary>
    [Fact]
    public void TheMessageForNoRulesNamesTheOverride()
    {
        var message = RulesLocation.NotFoundMessage(Nowhere("app"));

        Assert.Contains(RulesLocation.OverrideVariable, message, StringComparison.Ordinal);
        Assert.Contains(Nowhere("app"), message, StringComparison.Ordinal);
        Assert.Contains("data/rules", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The startup check reads every rules file, not one.</b> It warmed the tiers alone, so
    /// a directory holding nothing but <c>tiers.json</c> started cleanly and then threw out of
    /// five of the six tools — the exact failure the check exists to prevent, passing itself.
    /// </summary>
    [Fact]
    public void APartialRulesDirectoryIsRefusedAtStartupRatherThanAtTheFirstQuestion()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            File.Copy(Path.Combine(RulesFixture.DataPath, "tiers.json"),
                      Path.Combine(scratch, "tiers.json"));

            var tools = CharacterServer.ToolsFor(scratch);

            Assert.ThrowsAny<Exception>(tools.ReadEverything);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (IOException) { /* a temp directory that outlives the run is not a failure */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>And a complete one is not, which is what keeps the test above honest.</summary>
    [Fact]
    public void ACompleteRulesDirectoryStartsCleanly()
    {
        CharacterServer.ToolsFor(RulesFixture.DataPath).ReadEverything();
    }

    /// <summary>
    /// <b>The wire names of a validation subject are the same here as from the <c>build</c>
    /// command.</b> Two copies of a conversion, and a doc comment saying a caller reads the
    /// same names from both — with nothing asserting it. The same conversion in the same
    /// repository once shipped <c>gearfeature</c> on one side and <c>gear_feature</c> on the
    /// other, with both tests green.
    /// </summary>
    [Fact]
    public void ASubjectKindIsSpeltTheSameWayAsTheBuildCommandSpellsIt()
    {
        foreach (var kind in Enum.GetValues<ValidationSubject>())
            Assert.Equal(Cli.Headless.BuildCommand.SubjectKindName(kind), Judgement.SubjectKindName(kind));

        // And it is the snake_case spelling rather than the enum's own, which is what the two
        // documents promise their readers.
        Assert.Equal("gear_feature", Judgement.SubjectKindName(ValidationSubject.GearFeature));
    }
}
