using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
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
                             "using static System.Console", "= System.Console",
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
    /// <b>And the property itself, from the outside.</b> The check above reads source, which
    /// cannot see a write from a library, from <c>engine/</c> or <c>sheets/</c>, or through a
    /// spelling nobody thought of. This starts the built program the way a client does and
    /// completes a real session with it: anything else on that stream and the handshake fails,
    /// because a JSON-RPC reader has no way to skip a line it did not expect.
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramSpeaksNothingButTheProtocol()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "stdout-hygiene",
                Command = ServerExecutable()
            }),
            cancellationToken: cancellation);

        var tools = await client.ListToolsAsync(cancellationToken: cancellation);

        Assert.Equal(6, tools.Count);

        var guide = await client.CallToolAsync(
            CharacterServer.CreationGuideTool, cancellationToken: cancellation);

        Assert.NotEqual(true, guide.IsError);
        Assert.Contains("The engine decides",
            string.Concat(guide.Content.OfType<TextContentBlock>().Select(c => c.Text)),
            StringComparison.OrdinalIgnoreCase);
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
            null, _ => null, Path.Combine("C:", "app", "bin")).ToList();

        Assert.Equal(Path.Combine("C:", "app", "bin", "data", "rules"), candidates[0]);
        Assert.Contains(Path.Combine("C:", "app", "data", "rules"), candidates);
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

        var beside = Path.Combine("C:", "app", "data", "rules");

        Assert.Equal("explicit",
            RulesLocation.Find("explicit", Environment, Path.Combine("C:", "app"), _ => true).Directory);

        Assert.Equal("from-environment",
            RulesLocation.Find(null, Environment, Path.Combine("C:", "app"), _ => true).Directory);

        Assert.Equal(beside,
            RulesLocation.Find(null, _ => null, Path.Combine("C:", "app"), _ => true).Directory);
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
            "wrong", _ => null, Path.Combine("C:", "app"), everythingElseExists);

        Assert.Null(fromArgument.Directory);
        Assert.Contains("wrong", fromArgument.Refusal!, StringComparison.Ordinal);
        Assert.Contains("first argument", fromArgument.Refusal!, StringComparison.OrdinalIgnoreCase);

        var fromEnvironment = RulesLocation.Find(
            null, v => v == RulesLocation.OverrideVariable ? "wrong" : null,
            Path.Combine("C:", "app"), everythingElseExists);

        Assert.Null(fromEnvironment.Directory);
        Assert.Contains(RulesLocation.OverrideVariable, fromEnvironment.Refusal!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>No rules is a refusal, not a guess.</b> A repository built for a directory that is
    /// not there gets as far as a connected session and then answers every question with an
    /// error, several layers from the cause.
    /// </summary>
    [Fact]
    public void NoRulesAnywhereIsAnAnswerRatherThanAGuess()
    {
        var nowhere = RulesLocation.Find(null, _ => null, Path.Combine("C:", "app"), _ => false);

        Assert.Null(nowhere.Directory);
        Assert.Contains(RulesLocation.OverrideVariable, nowhere.Refusal!, StringComparison.Ordinal);

        Assert.Equal(
            Path.Combine("C:", "app", "data", "rules"),
            RulesLocation.Find(null, _ => null, Path.Combine("C:", "app"),
                d => d == Path.Combine("C:", "app", "data", "rules")).Directory);
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
    }

    /// <summary>
    /// And when there are none it says where it looked and how to say otherwise, because the
    /// person reading that line is looking at a client's log with no idea what the program's
    /// working directory was.
    /// </summary>
    [Fact]
    public void TheMessageForNoRulesNamesTheOverride()
    {
        var message = RulesLocation.NotFoundMessage(Path.Combine("C:", "app"));

        Assert.Contains(RulesLocation.OverrideVariable, message, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("C:", "app"), message, StringComparison.Ordinal);
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
