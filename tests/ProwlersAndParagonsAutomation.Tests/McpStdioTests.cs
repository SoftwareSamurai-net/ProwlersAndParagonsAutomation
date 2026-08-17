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
    ///
    /// <para><b>It is line-by-line, and that is a real hole rather than a detail.</b>
    /// <c>Console</c> on one line and <c>.WriteLine(…)</c> on the next is one statement that no
    /// line of contains the token, and adding a multi-line pattern here would only move the
    /// hole — the spelling after that is a helper in another file, or a library. What closes it
    /// is <see cref="TheBuiltProgramSpeaksNothingButTheProtocol"/> calling every tool, so the
    /// write happens on a stream something is reading. The two halves are only complementary to
    /// the extent that the runtime half is <em>driven</em>: it covered the startup path alone
    /// for a whole slice, and this scan reported nothing on the two lines the whole time.</para>
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
    /// <b>The property itself, read off the stream — and with every tool actually called.</b>
    /// The source check above cannot see a write from a library, from <c>engine/</c> or
    /// <c>sheets/</c>, or through a spelling nobody thought of, so this starts the built program
    /// the way a client does and reads its standard output by hand.
    ///
    /// <para><b>Every line has to be a JSON-RPC message.</b> An earlier version of this test
    /// drove the same binary through the SDK's client and asserted the session worked, under a
    /// comment claiming a stray line would break the handshake. It does not: a real stray line
    /// on that stream, printed before the transport starts, left the client perfectly happy and
    /// the test green. The client skipping what it cannot parse is exactly why this has to look
    /// at the bytes.</para>
    ///
    /// <para><b>And it enters every tool body, which is the half that was missing.</b> This sent
    /// <c>initialize</c>, <c>notifications/initialized</c> and <c>tools/list</c> and stopped —
    /// so it covered the startup path and the handshake and nothing else. <c>Console</c> and
    /// <c>.WriteLine(…)</c> written on two lines inside <c>SearchPowers</c>
    /// is invisible to the source scan above, which matches the token <c>Console.</c> on one
    /// line, and was invisible here too because no tool was ever called: the stray line arrived
    /// on a real client's stream as message two, with both guards green. A write inside
    /// <c>ListOptions</c> <em>was</em> caught, and only because <c>ReadEverything</c> calls it at
    /// startup — which is how narrow the cover was.</para>
    ///
    /// <para><b>Each answer has to carry something the tool only produces at the end of its
    /// body</b>, or a call that came back "unknown tool" would satisfy this while running no
    /// code at all.</para>
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramSpeaksNothingButTheProtocol()
    {
        var cancellation = TestContext.Current.CancellationToken;

        // Bounded, because the failure being looked for is a stream that never produces the
        // line this is waiting for, and a bare read would hang the suite rather than fail it.
        // Thirty seconds, the same as Ends: the passing run is well under a second, and every
        // second beyond that is spent on a failure that has already happened.
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        giveUp.CancelAfter(TimeSpan.FromSeconds(30));

        using var server = Start(ServerExecutable(), rulesDirectory: null);

        // Which read ran out of time, because the two mean different things and both are real
        // mutations: no greeting at all, and a request that never came back.
        var waitingFor = "a line on standard error at startup";

        try
        {
            // <b>The startup diagnostic, which nothing asserted.</b> docs/MCP-SETUP.md's first
            // troubleshooting bullet sends a reader to their client's MCP log to find it, so
            // deleting the line makes that instruction a dead end. It goes to standard error,
            // before the transport starts.
            //
            // <b>Read as "some line says it", not "the first line does".</b> Standard error is
            // where everything said to a human is *supposed* to go, so an added greeting is a
            // legitimate change — and against the first-line version it failed this test, which
            // would have read as a stdout-hygiene regression and is nothing of the kind.
            var said = new List<string>();
            string? greeting = null;

            while (said.Count < 20 &&
                   await server.StandardError.ReadLineAsync(giveUp.Token) is { } line)
            {
                said.Add(line);

                if (line.Contains(Path.Combine("data", "rules"), StringComparison.Ordinal))
                {
                    greeting = line;
                    break;
                }
            }

            Assert.True(greeting is not null,
                "No line on standard error named the rules directory the server found. The setup "
                + "guide's first troubleshooting bullet sends a stuck reader to their client's log "
                + "to look for it. What was said instead:\n" + string.Join("\n", said));

            waitingFor = "a reply to every request";

            await Say(server, Request(1, "initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "stdout-hygiene", ["version"] = "1"
                }
            }), giveUp.Token);

            await Say(server, """{"jsonrpc":"2.0","method":"notifications/initialized"}""", giveUp.Token);
            await Say(server, Request(2, "tools/list", null), giveUp.Token);

            // What each answer must carry: id 1 is the handshake, id 2 the tool list, and one
            // id per tool from 3 on.
            var wanted = new Dictionary<int, string>
            {
                [1] = "protocolVersion",
                [2] = CharacterServer.CheckCharacterTool
            };

            var id = 3;
            var driven = new List<string>();

            foreach (var (tool, arguments, marker) in EveryToolCall())
            {
                wanted[id] = marker;
                driven.Add(tool);

                await Say(server, Request(id, "tools/call", new JsonObject
                {
                    ["name"] = tool, ["arguments"] = arguments
                }), giveUp.Token);

                id++;
            }

            var answered = new Dictionary<int, string>();

            // Reads until every request has been answered, so the run covers the startup, the
            // handshake, a request, and the body of all six tools — every point at which
            // something could print. A line that is not JSON is a failure whatever else it says.
            while (answered.Count < wanted.Count &&
                   await server.StandardOutput.ReadLineAsync(giveUp.Token) is { } line)
            {
                if (line.Length == 0) continue;

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

                // A notification carries no id and is not an answer to anything; there should
                // be none, and skipping one is not what this test is about.
                if (node["id"]?.GetValue<int>() is not { } answeredId) continue;

                Assert.Null(node["error"]);
                answered[answeredId] = line;
            }

            // Every request answered, and each answer carrying something only the far end of
            // that tool's body produces — so the loop above did not fall out of an empty stream,
            // and a tool that was never entered fails here rather than passing silently.
            Assert.Equal(wanted.Keys.Order(), answered.Keys.Order());

            foreach (var (requestId, marker) in wanted)
                Assert.Contains(marker, answered[requestId], StringComparison.Ordinal);

            // <b>And every tool the server serves was one of them, taken from its own tool list
            // rather than from a count here.</b> Without this the cover is whatever
            // <see cref="EveryToolCall"/> happens to yield: dropping one leaves five tools driven
            // and a green test, and a seventh tool added later would never be called at all —
            // which is the same shape of hole as sending no tool call in the first place.
            var served = JsonNode.Parse(answered[2])!["result"]!["tools"]!.AsArray()
                .Select(t => t!["name"]!.GetValue<string>())
                .ToList();

            Assert.NotEmpty(served);
            Assert.Equal(served.Order(), driven.Distinct(StringComparer.Ordinal).Order());
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            Assert.Fail($"The server never produced {waitingFor}.");
        }
        finally
        {
            Stop(server);
        }
    }

    /// <summary>
    /// Every tool, twice: once with arguments that reach the far end of the body, and once with
    /// arguments that reach its refusal. Each carries a string the answer must contain to show it
    /// got there.
    ///
    /// <para><b>A marker has to be something only that body can produce, and one was not.</b>
    /// <c>character_sheet</c>'s marker was the character's name — which the test itself sends as
    /// the argument and <c>check_character</c> echoes back as <c>character.name</c>. So wiring the
    /// <c>character_sheet</c> wire name to <c>CheckCharacter</c> passed: a client asking for the
    /// printed sheet got JSON, and this was the only test that drives that wiring at all, since
    /// every other one calls the method in process. It is the masthead now, which nothing but the
    /// text renderer writes.</para>
    ///
    /// <para><b>And the refusal branches were undriven, which left a whole class of stray write
    /// invisible.</b> A two-line <c>Console.WriteLine</c> in <c>TryReadCharacter</c>'s
    /// <c>JsonException</c> branch — reached by the commonest first-draft mistake there is, a
    /// misspelled field — passed the source scan and this test both, because the happy path never
    /// goes near it. <c>creation_guide</c> has no refusal to drive: it takes no argument and
    /// always answers.</para>
    /// </summary>
    private static IEnumerable<(string Tool, JsonObject Arguments, string Marker)> EveryToolCall()
    {
        var hero = JsonNode.Parse(CharacterSheetJson.Write(SampleCharacters.Hero()))!;

        // A character in the right shape that the engine cannot price, so the sheet has nothing
        // to print. An invented Power id costs nothing to write and throws out of the total.
        var unpriceable = JsonNode.Parse(
            """{"SelectedTierId":"standard","SelectedPowers":[{"PowerId":"no_such_power","PurchasedRanks":1}]}""")!;

        yield return (CharacterServer.CreationGuideTool, new JsonObject(),
            "The engine decides");

        yield return (CharacterServer.ListOptionsTool,
            new JsonObject { ["category"] = "tiers" }, "trait_cap");
        yield return (CharacterServer.ListOptionsTool,
            new JsonObject { ["category"] = "nothing_like_it" }, "NO_SUCH_CATEGORY");

        yield return (CharacterServer.SearchPowersTool,
            new JsonObject { ["query"] = "turns invisible" }, "nothing_matched_by_name");
        yield return (CharacterServer.SearchPowersTool,
            new JsonObject { ["query"] = "!!!" }, "EMPTY_QUERY");

        yield return (CharacterServer.PowerDetailTool,
            new JsonObject { ["powerId"] = "armor" }, "ranks_purchasable");
        yield return (CharacterServer.PowerDetailTool,
            new JsonObject { ["powerId"] = "time_punch" }, "NO_SUCH_POWER");

        yield return (CharacterServer.CheckCharacterTool,
            new JsonObject { ["character"] = hero.DeepClone() }, "verdict");
        yield return (CharacterServer.CheckCharacterTool,
            new JsonObject { ["character"] = new JsonObject { ["Nam"] = 1 } }, "CHARACTER_UNREADABLE");

        // The masthead the text renderer writes, which the judge's JSON report cannot contain.
        yield return (CharacterServer.CharacterSheetTool,
            new JsonObject { ["character"] = hero.DeepClone() }, "CHARACTER SHEET");
        yield return (CharacterServer.CharacterSheetTool,
            new JsonObject { ["character"] = unpriceable.DeepClone() }, "NO_SHEET_TO_PRINT");
    }

    /// <summary>One JSON-RPC request, on one line, which is what the framing requires.</summary>
    private static string Request(int id, string method, JsonObject? parameters)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };

        if (parameters is not null) message["params"] = parameters;

        return message.ToJsonString();
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
    /// <b>The startup check reads the embedded guide as well as the rules.</b> The guide is an
    /// embedded resource, so the way it goes missing is a csproj edit — and the claim
    /// <c>CharacterTools</c> makes for itself is that such an edit becomes a refusal at startup
    /// rather than a conversation that opens with an empty document. Deleting the one line that
    /// makes it true left the suite green.
    ///
    /// <para><b>Read from the source, and that is a limitation rather than a preference.</b> A
    /// resource cannot be un-embedded from an assembly that is already loaded, so there is no
    /// runtime arrangement in which the guide is absent for this to observe — the nearest thing
    /// available is the line that reads it. <see cref="McpQuestionPolicyTests"/> covers the
    /// other half, that the resource is there and is this document; what has no runtime test,
    /// and cannot have one from here, is which of the two failures a missing resource produces.
    /// </para>
    ///
    /// <para><b>Comments are stripped first, and the version that did not strip them was worth
    /// almost nothing.</b> Deleting the read and leaving <c>// QuestionPolicy.Text is read by
    /// CreationGuide on the first call, so there is no need to touch it here</c> passed — and
    /// that is not a contrived mutation, it is what somebody removing the line would actually
    /// write. A weak instrument is a reason to sharpen it, not an excuse for the first version
    /// of it.</para>
    /// </summary>
    [Fact]
    public void TheStartupCheckReadsTheEmbeddedGuideAndNotOnlyTheRules()
    {
        var source = File.ReadAllText(Path.Combine(McpDirectory, "CharacterTools.cs"));

        var start = source.IndexOf("public void ReadEverything()", StringComparison.Ordinal);
        Assert.True(start >= 0, "CharacterTools no longer has a ReadEverything method.");

        // To the end of the method, which at this indentation is the first line that is a
        // closing brace in the first column of the class body.
        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal);
        Assert.True(end > start, "ReadEverything's body could not be found.");

        // Code only. A comment naming the thing is not a read of it, and every assertion below
        // is satisfied by prose otherwise.
        var body = string.Join('\n', source[start..end]
            .Split('\n')
            .Select(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? "" : line));

        Assert.Contains(nameof(QuestionPolicy), body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A rules directory missing any one file is refused at startup — every file, not the one
    /// somebody picked.</b>
    ///
    /// <para>The two tests beside this used a directory holding <c>tiers.json</c> alone, which
    /// still throws on the Powers whatever else is broken. So <c>ReadEverything</c> could go back
    /// to <c>ListOptions(Categories[0])</c> — warming one catalogue, the exact bug it was written
    /// to prevent — and stay green, because a directory with everything except <c>perks.json</c>
    /// started cleanly and then threw out of the tools. The source-reading test above cannot tell
    /// "reads every catalogue" from "reads the first one" either: the token <c>Categories</c>
    /// survives both.</para>
    ///
    /// <para>Driven from <see cref="RulesRepository.DataFileNames"/>, so a rules file added later
    /// is covered without anybody remembering — which is the same reason that list exists for the
    /// browser, where a file left out of it is a silently empty rules set.</para>
    /// </summary>
    public static TheoryData<string> EveryRulesFile() => [.. RulesRepository.DataFileNames];

    [Theory]
    [MemberData(nameof(EveryRulesFile))]
    public void ARulesDirectoryMissingAnyOneFileIsRefusedAtStartup(string missing)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            foreach (var file in RulesRepository.DataFileNames.Where(f => f != missing))
                File.Copy(Path.Combine(RulesFixture.DataPath, file), Path.Combine(scratch, file));

            var tools = CharacterServer.ToolsFor(scratch);

            Assert.ThrowsAny<Exception>(tools.ReadEverything);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
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
