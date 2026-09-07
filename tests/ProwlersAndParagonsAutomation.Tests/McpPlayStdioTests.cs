using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.McpPlay;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The two things about the encounter server that no protocol test can see: what may be written to
/// standard output, and where the rules are found.
///
/// <para>The shape is <see cref="McpStdioTests"/>' against the second binary, deliberately — the
/// failures it exists for are the same failures, and the character server has already paid for
/// finding out that they are not caught by reading, by a source scan alone, or by a runtime test
/// that never enters a tool body. What is new here is that there are <b>two</b> rules directories
/// to be missing, and that the second one is a subdirectory of the first.</para>
/// </summary>
public sealed class McpPlayStdioTests
{
    private static string PlayDirectory => Path.Combine(RulesFixture.RepoRoot, "mcp-play");

    private static IEnumerable<string> SourceFiles =>
        Directory.EnumerateFiles(PlayDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// The five trees the published binary is built out of: its own sources, both engines, the
    /// arguments the two servers share, and the rules data the project files copy beside it. A
    /// change in any of them is a change in what <c>mcp-play-server/</c> should contain.
    ///
    /// <para><c>data/rules</c> earns its place for the same reason as the other four, not a weaker
    /// one: <c>ProwlersAndParagons.McpPlay.csproj</c> copies both <c>data/rules/*.json</c> (the
    /// character rules, non-recursive) and <c>data/rules/play/*.json</c> beside the binary, and the
    /// server reads that copy at runtime — <see cref="ProwlersAndParagonsAutomation.Mcp.RulesLocation"/>
    /// and <see cref="ProwlersAndParagonsAutomation.McpPlay.PlayRulesLocation"/> both walk up from
    /// the binary's own directory first. A rule edited in the repository and never republished is
    /// invisible to a running server in exactly the way a source edit is: this directory has no
    /// subdirectory but <c>play</c> and no file that is not <c>*.json</c>, so scanning it recursively
    /// for every file, the same way the other four trees are scanned, covers precisely what both
    /// globs copy and nothing else.</para>
    /// </summary>
    private static readonly string[] BuiltFrom =
        ["mcp-play", "play", "engine", "mcp-shared", Path.Combine("data", "rules")];

    /// <summary>
    /// Every file the published binary is built out of, each with a hash of its <b>content</b>: the
    /// repository-relative path and a SHA-256, one line apiece, in path order.
    ///
    /// <para>Every file rather than <c>*.cs</c>: the policy document is an embedded resource, the
    /// rules under <c>data/rules</c> are JSON, the project files decide what is copied beside the
    /// binary, and all three are sources of what gets published. <c>bin</c> and <c>obj</c> are
    /// skipped: build output changes whenever anything is compiled, so including it would report
    /// the binary as stale immediately after publishing it.</para>
    ///
    /// <para><b>The path half of each line is always <c>/</c>-separated</b>, regardless of
    /// <see cref="Path.DirectorySeparatorChar"/> on the machine that computed it. CI publishes fresh
    /// on every run and never reads a checked-in record, so this has no effect there — but a record
    /// written on this developer's Mac and later read on a different machine, or vice versa, has to
    /// agree on what a path looks like or every line reads as changed. Forcing <c>/</c> is one fixed
    /// choice rather than "whatever the writer's OS did".</para>
    /// </summary>
    private static string SourceFingerprint() =>
        string.Join('\n', BuiltFrom
            .SelectMany(tree => Directory.EnumerateFiles(
                Path.Combine(RulesFixture.RepoRoot, tree), "*", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(RulesFixture.RepoRoot, f).Replace('\\', '/'))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .Select(relative => relative + ' ' + Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(RulesFixture.RepoRoot, relative))))));

    /// <summary>
    /// Where the fingerprint of the sources a publish was asked to build is recorded: beside the
    /// binary, so it is thrown away with it and cannot outlive the directory it describes.
    /// </summary>
    private static string PublishedFrom(string binary) =>
        Path.Combine(Path.GetDirectoryName(binary)!, ".published-from");

    /// <summary>
    /// Why <paramref name="binary"/> is not a build of the sources on disk, or null if it is.
    ///
    /// <para><b>This asks about content, and it used to ask about time.</b> The question is whether
    /// the published binary was built from the code that is there now, and a modification time
    /// answers a different one. The build is deterministic: edit a source, restore it byte for byte
    /// — which every mutation check in this repository does — and the source is newer than the
    /// binary while producing byte-identical output, so <c>publish</c> skips the copy, the binary's
    /// timestamp does not move, and a timestamp guard fails <em>again</em> telling the reader to
    /// publish, which is the one thing that cannot fix it. Three agents in one day worked round that
    /// with <c>rm -rf mcp-play-server</c> or <c>touch</c>. A guard whose remedy does not work is a
    /// guard people learn to route around, and the next stale binary goes with them.</para>
    ///
    /// <para>So a successful publish records the fingerprint of what it was asked to build, and this
    /// compares that record with the sources as they are. Restoring a file byte for byte is then not
    /// a change at all and nothing is republished; changing one is a change however the timestamps
    /// fell. A publish that <em>fails</em> — the running-server case CLAUDE.md names — records
    /// nothing, so the binary stays stale and the assertion still fires.</para>
    /// </summary>
    private static string? StaleBecause(string binary, string fingerprint)
    {
        if (!File.Exists(binary)) return "it is not there at all";

        var record = PublishedFrom(binary);

        if (!File.Exists(record))
            return $"nothing beside it says what it was published from ('{record}' is missing)";

        var recorded = File.ReadAllText(record).Split('\n');
        var current = fingerprint.Split('\n');

        for (var i = 0; i < Math.Max(recorded.Length, current.Length); i++)
        {
            var was = i < recorded.Length ? recorded[i] : null;
            var now = i < current.Length ? current[i] : null;

            if (string.Equals(was, now, StringComparison.Ordinal)) continue;

            // A line is "<path> <hash>", so the first field of whichever side has one names the
            // file that changed, appeared or went away.
            return $"'{(now ?? was)!.Split(' ')[0]}' is not what it was published from";
        }

        return null;
    }

    /// <summary>
    /// A scratch binary and its <c>.published-from</c> record, in a throwaway directory, so
    /// <see cref="StaleBecause"/> can be driven directly without touching the real
    /// <c>mcp-play-server/</c> or waiting on a publish. The "binary" is any file — <c>StaleBecause</c>
    /// only asks whether it exists.
    /// </summary>
    private static string NewScratchBinary(string? record)
    {
        var directory = Path.Combine(Path.GetTempPath(), "pp-stale-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var binary = Path.Combine(directory, "binary.dll");
        File.WriteAllText(binary, "not a real binary, StaleBecause never reads it");

        if (record is not null) File.WriteAllText(PublishedFrom(binary), record);

        return binary;
    }

    /// <summary>
    /// The positive control for the three tests beside this one: an unmodified record must read as
    /// fresh, or every "and this one reads as stale" below is meaningless — a <see cref="StaleBecause"/>
    /// that always answers non-null would pass them too.
    /// </summary>
    [Fact]
    public void StaleBecauseIsFreshWhenTheRecordMatches()
    {
        const string fingerprint = "a.cs 1111\nb.cs 2222\nc.cs 3333";
        var binary = NewScratchBinary(fingerprint);

        Assert.Null(StaleBecause(binary, fingerprint));
    }

    /// <summary>
    /// A file present when the record was written and gone now — the shape of a source deleted
    /// since the last publish — has to read as stale. Manual controls only ever exercised an edit
    /// (same set of files, one hash changed); nothing had checked that a deletion is caught at all.
    /// </summary>
    [Fact]
    public void StaleBecauseIsStaleWhenASourceWasDeleted()
    {
        const string wasPublishedFrom = "a.cs 1111\nb.cs 2222\nc.cs 3333";
        const string onDiskNow = "a.cs 1111\nb.cs 2222";
        var binary = NewScratchBinary(wasPublishedFrom);

        var stale = StaleBecause(binary, onDiskNow);

        Assert.NotNull(stale);
        Assert.Contains("c.cs", stale);
    }

    /// <summary>
    /// A file that was not there when the record was written and is there now — the shape of a
    /// source added since the last publish — has to read as stale for the same reason a deletion
    /// does: the record no longer describes the tree on disk.
    /// </summary>
    [Fact]
    public void StaleBecauseIsStaleWhenASourceWasAdded()
    {
        const string wasPublishedFrom = "a.cs 1111\nb.cs 2222";
        const string onDiskNow = "a.cs 1111\nb.cs 2222\nc.cs 3333";
        var binary = NewScratchBinary(wasPublishedFrom);

        var stale = StaleBecause(binary, onDiskNow);

        Assert.NotNull(stale);
        Assert.Contains("c.cs", stale);
    }

    /// <summary>
    /// <b>Nothing in this server writes to standard output.</b> Everything it says to a human goes
    /// to standard error, which every client collects into a log.
    ///
    /// <para>Matched on <c>Console.</c> followed by anything other than <c>Error</c>, plus the
    /// spellings that reach the same stream without the token, exactly as the character server's
    /// scan does — <c>Console.Out.Write</c>, <c>OpenStandardOutput</c> and a static import are the
    /// same mistake in three spellings, and a check for the obvious one passes while any of them
    /// ships. <c>mcp-shared/</c> is scanned by <see cref="McpStdioTests"/>, which both servers
    /// compile.</para>
    ///
    /// <para><b>It is line-by-line, and that is a real hole rather than a detail.</b> What closes it
    /// is <see cref="TheBuiltProgramSpeaksNothingButTheProtocol"/> entering all four tool bodies, so
    /// a write happens on a stream something is reading.</para>
    /// </summary>
    [Fact]
    public void NothingWritesToStandardOutput()
    {
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in SourceFiles)
        {
            scanned++;

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // A comment about standard output is not a write to it, and this server's own
                // reasoning is written down in several of them.
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;

                foreach (var index in Occurrences(line, "Console."))
                {
                    if (line.AsSpan(index).StartsWith("Console.Error", StringComparison.Ordinal))
                        continue;

                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                }

                foreach (var spelling in Spellings)
                {
                    if (line.Contains(spelling, StringComparison.Ordinal))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
                }
            }
        }

        // The control: "no offender was found" is satisfied completely by a scan that read nothing.
        Assert.True(scanned >= 4,
            $"Only {scanned} source files were read under {PlayDirectory}. The scan has stopped "
            + "finding the server's sources; fix it rather than the assertion.");

        Assert.True(offenders.Count == 0,
            "Standard output carries the protocol and nothing else, so the server may only write "
            + "to Console.Error:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// The spellings that reach standard output without the token <c>Console.</c> — an alias, a
    /// static import, and the handle itself.
    /// </summary>
    private static readonly string[] Spellings =
    [
        "using static System.Console", "= System.Console", "= Console",
        "OpenStandardOutput", "SetOut", "Console.Out"
    ];

    private static IEnumerable<int> Occurrences(string line, string token)
    {
        for (var index = line.IndexOf(token, StringComparison.Ordinal);
             index >= 0;
             index = line.IndexOf(token, index + 1, StringComparison.Ordinal))
            yield return index;
    }

    // ── The property itself, off the stream ───────────────────────────────

    /// <summary>
    /// <b>Every line the built program puts on standard output is a JSON-RPC message, with all four
    /// tools driven.</b>
    ///
    /// <para>The source scan above cannot see a write from a library, from <c>engine/</c>, from
    /// <c>play/</c>, or through a spelling nobody thought of, so this starts the built program the
    /// way a client does and reads its standard output by hand. <b>A stray line does not break a
    /// client</b> — the SDK's own client skips what it cannot parse — which is exactly why this has
    /// to look at the bytes rather than assert that the session worked.</para>
    ///
    /// <para><b>Every tool body is entered, on the way in and on the way out.</b> The character
    /// server's history is unambiguous here: a two-line <c>Console.WriteLine</c> inside a tool
    /// reached a real client's stream with both guards green, because no tool was ever called. So
    /// each of the four is driven twice — once to the far end of the body and once to its refusal —
    /// and each answer has to carry a string only that body produces, or a call answered "unknown
    /// tool" would satisfy this while running no code at all.</para>
    ///
    /// <para><b>Sequential, unlike the character server's version, because one call depends on
    /// another.</b> <c>take_turn</c> needs an encounter <c>start_encounter</c> opened, and requests
    /// sent in a batch are not promised to be answered in order. Every line is still read and
    /// judged; nothing is skipped on the way to an id.</para>
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramSpeaksNothingButTheProtocol()
    {
        var cancellation = TestContext.Current.CancellationToken;

        // Bounded, because the failure being looked for is a stream that never produces the line
        // this is waiting for, and a bare read would hang the suite rather than fail it.
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        giveUp.CancelAfter(TimeSpan.FromMinutes(2));

        using var server = Start(ServerExecutable(), rulesDirectory: null);

        var waitingFor = "a line on standard error at startup";

        try
        {
            // <b>The startup diagnostic, which nothing else asserts.</b> docs/MCP-SETUP.md sends a
            // reader to their client's MCP log to find it, so deleting the line makes that
            // instruction a dead end. It names *both* directories here, which is the thing this
            // server has that the other does not.
            var said = new List<string>();
            string? greeting = null;

            while (said.Count < 20 &&
                   await server.StandardError.ReadLineAsync(giveUp.Token) is { } line)
            {
                said.Add(line);

                if (line.Contains(Path.Combine("data", "rules", "play"), StringComparison.Ordinal))
                {
                    greeting = line;
                    break;
                }
            }

            Assert.True(greeting is not null,
                "No line on standard error named the play rules directory the server found. The "
                + "setup guide sends a stuck reader to their client's log to look for it. What was "
                + "said instead:\n" + string.Join("\n", said));

            waitingFor = "a reply to every request";

            var answers = new Dictionary<int, string>();
            var id = 0;

            async Task<string> Ask(string method, JsonObject? parameters, string marker)
            {
                id++;

                await Say(server, Request(id, method, parameters), giveUp.Token);

                // Read until this request is answered, judging every line on the way. A line that
                // is not JSON-RPC fails here whatever else it says.
                while (!answers.ContainsKey(id))
                {
                    var line = await server.StandardOutput.ReadLineAsync(giveUp.Token);

                    Assert.True(line is not null,
                        $"Standard output ended before '{method}' was answered.");

                    if (line!.Length == 0) continue;

                    JsonNode node;
                    try
                    {
                        node = JsonNode.Parse(line) ?? throw new InvalidOperationException("null");
                    }
                    catch (Exception e) when (e is JsonException or InvalidOperationException)
                    {
                        Assert.Fail(
                            "Standard output carries the protocol and nothing else, and this line "
                            + $"is not a JSON-RPC message:\n{line}");
                        return "";
                    }

                    Assert.Equal("2.0", node["jsonrpc"]!.GetValue<string>());

                    // A notification carries no id and is not an answer to anything.
                    if (node["id"]?.GetValue<int>() is not { } answered) continue;

                    Assert.Null(node["error"]);
                    answers[answered] = line;
                }

                Assert.Contains(marker, answers[id], StringComparison.Ordinal);

                return answers[id];
            }

            await Ask("initialize", new JsonObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject { ["name"] = "stdout-hygiene", ["version"] = "1" }
            }, "protocolVersion");

            await Say(server, """{"jsonrpc":"2.0","method":"notifications/initialized"}""", giveUp.Token);

            var listed = await Ask("tools/list", null, "start_encounter");

            var driven = new List<string>();

            foreach (var (tool, arguments, marker) in EveryToolCall())
            {
                driven.Add(tool);

                await Ask("tools/call", new JsonObject
                {
                    ["name"] = tool, ["arguments"] = arguments
                }, marker);
            }

            // <b>And every tool the server serves was one of them, taken from its own tool list
            // rather than from a count here.</b> Without this the cover is whatever
            // <see cref="EveryToolCall"/> happens to yield: dropping one leaves three tools driven
            // and a green test, and a fifth tool added later would never be called at all.
            var served = JsonNode.Parse(listed)!["result"]!["tools"]!.AsArray()
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
    /// <para><b>A marker has to be something only that body can produce.</b> <c>encounter_id</c>
    /// would not do for <c>start_encounter</c>: <c>take_turn</c> echoes it back, so wiring the two
    /// wire names to each other would pass. <c>ledger</c> is written by the opening answer alone,
    /// <c>defeated_by_effect</c> by the public state alone, and <c>by_combatant</c> by the
    /// measurement alone.</para>
    ///
    /// <para><b>The refusal branches matter as much as the happy ones.</b> The character server's
    /// recorded hole was a stray write in a <c>JsonException</c> branch reached by the commonest
    /// first-draft mistake there is — a misspelled field — which the happy path never goes near.
    /// <c>combat_guide</c> takes no argument and has no refusal to drive.</para>
    ///
    /// <para><b>The encounter id is <c>enc_1</c> by construction</b>, which is why the opening call
    /// comes first here: ids are handed out from one, in order, and this is the first fight of the
    /// process.</para>
    /// </summary>
    private static IEnumerable<(string Tool, JsonObject Arguments, string Marker)> EveryToolCall()
    {
        yield return ("combat_guide", new JsonObject(), "the engine resolves and you narrate");

        yield return ("start_encounter", new JsonObject
        {
            ["combatants"] = Fight(), ["seed"] = 81
        }, "ledger");

        yield return ("start_encounter", new JsonObject
        {
            ["combatants"] = new JsonArray(new JsonObject
            {
                ["kind"] = "hero",
                ["character"] = new JsonObject { ["AbilityRank"] = new JsonObject() }
            })
        }, "CHARACTER_UNREADABLE");

        yield return ("take_turn", new JsonObject
        {
            ["encounterId"] = "enc_1",
            ["intent"] = new JsonObject
            {
                ["kind"] = "attack",
                ["actor"] = "soldier",
                ["target"] = "robots",
                ["trait_id"] = "might"
            }
        }, "defeated_by_effect");

        yield return ("take_turn", new JsonObject
        {
            ["encounterId"] = "enc_1",
            ["intent"] = new JsonObject { ["kind"] = "quantum_leap", ["actor"] = "soldier" }
        }, "NO_SUCH_INTENT");

        yield return ("run_encounters", new JsonObject
        {
            ["combatants"] = Fight(), ["runs"] = PlayTools.FewestRuns, ["seed"] = 3, ["maxPages"] = 8
        }, "by_combatant");

        yield return ("run_encounters", new JsonObject
        {
            ["combatants"] = Fight(), ["runs"] = 1
        }, "TOO_FEW_RUNS");
    }

    /// <summary>A Hero and a group of Minions, which is enough for both sides to be able to lose.</summary>
    private static JsonArray Fight() =>
    [
        new JsonObject
        {
            ["kind"] = "hero",
            ["id"] = "soldier",
            ["side"] = "heroes",
            ["character"] = new JsonObject
            {
                ["Name"] = "Citizen Soldier",
                ["SelectedTierId"] = "standard",
                ["AbilityRanks"] = new JsonObject { ["might"] = 12, ["toughness"] = 6, ["agility"] = 4 }
            }
        },
        new JsonObject
        {
            ["kind"] = "minions",
            ["id"] = "robots",
            ["name"] = "the robotic Minions",
            ["threat_rank"] = 6,
            ["count"] = 4,
            ["side"] = "villains"
        }
    ];

    // ── Where the rules are ───────────────────────────────────────────────

    /// <summary>
    /// <b>Character rules with no play rules under them is a refusal by the program, not by a method
    /// it owns.</b>
    ///
    /// <para>This is the failure mode this server has and the character server does not: a reader
    /// following the setup guide points <c>PROWLERS_RULES_DIR</c> at a <c>data/rules</c> that is
    /// perfectly good for building characters and has no <c>play</c> folder in it. Guessed past, it
    /// would be a connected session that answers every question with an error.</para>
    ///
    /// <para>It runs the program, because the unit test for it passes while <c>Program.cs</c> is
    /// mutated back to the bug — a method nobody calls is not a check.</para>
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramRefusesCharacterRulesWithNoPlayRulesUnderThem()
    {
        var cancellation = TestContext.Current.CancellationToken;

        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-play-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            // A complete set of character rules, so the refusal cannot be about those.
            foreach (var file in Directory.GetFiles(RulesFixture.DataPath, "*.json"))
                File.Copy(file, Path.Combine(scratch, Path.GetFileName(file)));

            using var server = Start(ServerExecutable(), scratch);

            await Ends(server, cancellation);

            Assert.Equal(2, server.ExitCode);

            var said = await server.StandardError.ReadToEndAsync(cancellation);

            Assert.Contains("play rules could not be found", said, StringComparison.OrdinalIgnoreCase);

            // And the directory it wanted, by name — the message is read out of a client's log by
            // somebody with no idea what the program's working directory was.
            Assert.Contains(Path.Combine(scratch, "play"), said, StringComparison.Ordinal);

            // Nothing on the stream that belongs to the protocol, even while failing.
            Assert.Equal("", (await server.StandardOutput.ReadToEndAsync(cancellation)).Trim());
        }
        finally
        {
            Delete(scratch);
        }
    }

    /// <summary>
    /// <b>And a <c>play</c> folder that is there and incomplete is a refusal too.</b> Both stores
    /// load lazily, so a directory holding one play file would otherwise start cleanly and then
    /// throw out of every tool that resolves anything — the exact failure the startup check exists
    /// to prevent, passing its own check.
    /// </summary>
    [Fact]
    public async Task TheBuiltProgramRefusesAPartialPlayRulesDirectory()
    {
        var cancellation = TestContext.Current.CancellationToken;

        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-play-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            foreach (var file in Directory.GetFiles(RulesFixture.DataPath, "*.json"))
                File.Copy(file, Path.Combine(scratch, Path.GetFileName(file)));

            var play = Directory.CreateDirectory(Path.Combine(scratch, "play"));

            File.Copy(
                Path.Combine(PlayFixture.DataPath, "combat.json"),
                Path.Combine(play.FullName, "combat.json"));

            using var server = Start(ServerExecutable(), scratch);

            await Ends(server, cancellation);

            Assert.Equal(2, server.ExitCode);
            Assert.Contains("could not be read",
                await server.StandardError.ReadToEndAsync(cancellation), StringComparison.OrdinalIgnoreCase);

            Assert.Equal("", (await server.StandardOutput.ReadToEndAsync(cancellation)).Trim());
        }
        finally
        {
            Delete(scratch);
        }
    }

    /// <summary>And a directory the user named that is not there at all, from the outside.</summary>
    [Fact]
    public async Task TheBuiltProgramRefusesADirectoryThatIsNotThere()
    {
        var cancellation = TestContext.Current.CancellationToken;

        using var server = Start(ServerExecutable(),
            Path.Combine(Path.GetTempPath(), "pp-mcp-play-not-here-" + Guid.NewGuid().ToString("N")));

        await Ends(server, cancellation);

        Assert.Equal(2, server.ExitCode);
        Assert.Contains("not a directory on this machine",
            await server.StandardError.ReadToEndAsync(cancellation), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The play rules are the <c>play</c> folder under the character rules, and a character-rules
    /// directory without one is a refusal rather than a candidate that failed — the same rule
    /// <c>RulesLocation</c> applies to the store above it, for the same reason.
    /// </summary>
    [Fact]
    public void ThePlayRulesAreLookedForUnderTheCharacterRules()
    {
        var found = PlayRulesLocation.Find(Path.Combine("anywhere", "data", "rules"), _ => true);

        Assert.Equal(Path.Combine("anywhere", "data", "rules", "play"), found.Directory);
        Assert.Null(found.Refusal);

        var refused = PlayRulesLocation.Find(Path.Combine("anywhere", "data", "rules"), _ => false);

        Assert.Null(refused.Directory);
        Assert.Contains("play rules could not be found", refused.Refusal!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("anywhere", "data", "rules", "play"), refused.Refusal!, StringComparison.Ordinal);
    }

    // ── The launch a checkout performs ────────────────────────────────────

    /// <summary>
    /// <b>The launch the repository's own <c>.mcp.json</c> performs for this server puts nothing but
    /// protocol on standard output.</b>
    ///
    /// <para>The second of the two launch paths, and the one every checkout actually uses. The
    /// command comes out of <c>.mcp.json</c> rather than out of this file: a copy of the arguments
    /// here would go on passing after somebody changed the registration, which is the failure this
    /// whole area is about — a launch nothing tests.</para>
    ///
    /// <para><b>Positive control first.</b> "No line was bad" is satisfied by a stream with no lines
    /// at all, and a launch that never ran is how three of this repository's historical guards were
    /// wrong. So the <c>initialize</c> reply has to arrive before anything is judged, and a tool
    /// body is entered before it is finished: a session that only handshakes never runs a line of
    /// this repository's code, and the stray write this whole area exists for was inside a tool.
    /// </para>
    ///
    /// <para><b>And it publishes when the binary was not built from the code, not only when it is
    /// missing.</b> The check used to be <c>File.Exists</c>, so a <c>mcp-play-server/</c> published
    /// once and never again made this a test of a binary from another week — green while the
    /// registration, the tools, either engine, the shared arguments or the rules data had all moved
    /// on underneath it. What replaced it was a timestamp comparison, which was right about the
    /// danger and wrong about the instrument; <see cref="StaleBecause"/> carries why, and the
    /// question is now about the content of the five trees the binary is built out of.</para>
    /// </summary>
    [Fact]
    public async Task TheCheckedInRegistrationSpeaksNothingButTheProtocol()
    {
        var cancellation = TestContext.Current.CancellationToken;

        // Generous, and for one reason: this may have to publish first. A warm run is a couple of
        // seconds; a cold one on a fresh clone is a build of engine, play and mcp-play on top.
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        giveUp.CancelAfter(TimeSpan.FromMinutes(5));

        var registration = JsonNode.Parse(
            File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, ".mcp.json")))!;
        var server = registration["mcpServers"]![PlayServer.Name]!;

        // <b>The registration never builds, so something has to have published.</b> CI publishes
        // nothing, and a developer running the suite may have no `mcp-play-server/` at all; the
        // honest answer is to produce it rather than to skip. Not asserted on its exit code: with a
        // server already running out of that directory the copy fails while leaving a binary in
        // place. What matters is the file, and that it is not older than the code it was built from.
        var published = Path.Combine(
            RulesFixture.RepoRoot, "mcp-play-server", "ProwlersAndParagons.McpPlay.dll");

        // Taken before the publish, deliberately: this is the fingerprint of the sources the
        // publish is being *asked* to build. Taking it afterwards would claim currency for a file
        // edited while the build was reading, which is the direction that fails unsafely — recorded
        // this way, such an edit simply shows up as stale on the next run.
        var sources = SourceFingerprint();

        if (StaleBecause(published, sources) is not null)
        {
            using var publish = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList =
                {
                    "publish", Path.Combine("mcp-play", "ProwlersAndParagons.McpPlay.csproj"),
                    "-c", "Release", "-o", "mcp-play-server", "--verbosity", "quiet"
                },
                WorkingDirectory = RulesFixture.RepoRoot,
                UseShellExecute = false
            })!;

            await publish.WaitForExitAsync(giveUp.Token);

            // <b>Only a publish that worked may say what the binary was built from.</b> This one is
            // allowed to fail — with a server running out of that directory the copy fails while
            // leaving a binary in place — and recording the fingerprint anyway would hand a stale
            // binary a certificate of freshness, which is the whole trap this guard exists for.
            if (publish.ExitCode == 0 && File.Exists(published))
                File.WriteAllText(PublishedFrom(published), sources);
        }

        Assert.True(File.Exists(published),
            $"'{published}' is not there, and .mcp.json's launch never builds, so nothing will "
            + "produce it. Run: dotnet publish mcp-play/ProwlersAndParagons.McpPlay.csproj "
            + "-c Release -o mcp-play-server");

        // The publish above is allowed to fail, so this is where a stale binary is caught. The
        // commonest reason it fails is the one CLAUDE.md names: a server running out of that
        // directory holds the file, and that is the sentence to print rather than a hash.
        var stale = StaleBecause(published, sources);

        Assert.True(stale is null,
            $"'{published}' was not published from the sources on disk — {stale}. This would test a "
            + "binary built from other code than the code it is supposed to be checking. Publishing "
            + "it here did not fix that, and the usual reason is a running server holding the file: "
            + "stop it, then run dotnet publish mcp-play/ProwlersAndParagons.McpPlay.csproj "
            + "-c Release -o mcp-play-server");

        var start = new ProcessStartInfo(server["command"]!.GetValue<string>())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // The path in the registration is relative, and a client starts a project-scoped server
            // from the project root. Anywhere else and this would be testing a launch nobody
            // performs.
            WorkingDirectory = RulesFixture.RepoRoot
        };

        foreach (var argument in server["args"]!.AsArray())
            start.ArgumentList.Add(argument!.GetValue<string>());

        foreach (var variable in server["env"]!.AsObject())
            start.Environment[variable.Key] = variable.Value!.GetValue<string>();

        using var launched = Process.Start(start)
            ?? throw new InvalidOperationException("The registered command did not start.");

        try
        {
            await Say(launched,
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""",
                giveUp.Token);

            var lines = new List<string>();

            while (await launched.StandardOutput.ReadLineAsync(giveUp.Token) is { } line)
            {
                lines.Add(line);
                if (line.Contains("\"id\":1", StringComparison.Ordinal)) break;
            }

            Assert.True(lines.Count > 0,
                "The registration in .mcp.json produced no reply to initialize at all. The launch "
                + "itself is broken, which is worse than the stray-line case this guards.");

            // <b>And a tool body is entered on this path too.</b> A handshake runs none of this
            // repository's code: the SDK answers `initialize` before a line of `PlayTools` is
            // reached, so a session that stops there judges standard output over a program that has
            // done nothing yet — which is exactly the hole the character server shipped a stray
            // write through. `start_encounter` is the call that goes furthest: it finds both rules
            // directories from the registration's own working directory and environment, builds
            // combatants through both engines and writes a ledger. The marker is `ledger`, which
            // only the opening answer produces.
            await Say(launched,
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""", giveUp.Token);

            await Say(launched, Request(2, "tools/call", new JsonObject
            {
                ["name"] = PlayServer.StartEncounterTool,
                ["arguments"] = new JsonObject { ["combatants"] = Fight(), ["seed"] = 81 }
            }), giveUp.Token);

            string? opened = null;

            while (opened is null && await launched.StandardOutput.ReadLineAsync(giveUp.Token) is { } line)
            {
                lines.Add(line);
                if (line.Contains("\"id\":2", StringComparison.Ordinal)) opened = line;
            }

            Assert.True(opened is not null,
                "The registration's server never answered a tools/call, so nothing but the SDK's "
                + "own handshake ran and standard output was judged over a program that had not yet "
                + "reached a tool.");

            Assert.Null(JsonNode.Parse(opened!)!["error"]);
            Assert.Contains("ledger", opened!, StringComparison.Ordinal);

            Assert.All(lines, line => Assert.True(
                JsonNode.Parse(line) is not null,
                $"'{line}' is on standard output and is not a JSON-RPC message. Standard output "
                + "belongs to the protocol, and this is the launch every checkout performs."));

            // And the reply is this server's, not the character builder's — a registration pointing
            // both entries at one binary would satisfy every assertion above.
            Assert.Contains(PlayServer.Name, string.Join("\n", lines), StringComparison.Ordinal);
        }
        finally
        {
            Stop(launched);
        }
    }

    /// <summary>
    /// <b>The root project does not compile the encounter server's sources.</b>
    ///
    /// <para>The root <c>.csproj</c> sits at the repository root, so its default <c>**/*.cs</c> glob
    /// pulls in every sibling project's sources alongside its own. Every other sibling has a
    /// <c>&lt;Compile Remove&gt;</c>; the two projects added here need theirs, and the failure a
    /// missing one produces is a duplicate-type build error in a project that has nothing to do with
    /// the change.</para>
    /// </summary>
    [Theory]
    [InlineData("mcp-play")]
    [InlineData("mcp-shared")]
    public void TheRootProjectDoesNotCompileTheServerSources(string tree)
    {
        var project = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "ProwlersAndParagonsAutomation.csproj"));

        Assert.Contains($@"<Compile Remove=""{tree}\**"" />", project, StringComparison.Ordinal);
    }

    // ── Process plumbing ──────────────────────────────────────────────────

    private static Process Start(string executable, string? rulesDirectory)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // Somewhere with no rules under it, so a run that is meant to fail cannot be rescued by
            // the walk upwards finding this repository's own copy.
            WorkingDirectory = Path.GetTempPath()
        };

        if (rulesDirectory is not null) start.ArgumentList.Add(rulesDirectory);

        return Process.Start(start)
               ?? throw new InvalidOperationException($"'{executable}' did not start.");
    }

    /// <summary>
    /// Waits for a run that is supposed to stop, and fails rather than waiting for ever if it does
    /// not. A server that keeps running is exactly what these tests are looking for, and a bare wait
    /// turns catching it into a test run that never finishes.
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
                "The server was supposed to refuse this and stop, and it started instead. It is now "
                + "serving a session on rules it should not have accepted.");
        }
    }

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

    private static void Delete(string directory)
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { /* a temp directory that outlives the run is not a failure */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The server as it was built beside this test run — same configuration, same framework, so
    /// this cannot end up testing a stale binary from some earlier build.
    /// </summary>
    private static string ServerExecutable()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);          // …/bin/<cfg>/<tfm>/
        var framework = here.Name;
        var configuration = here.Parent!.Name;

        var path = Path.Combine(
            RulesFixture.RepoRoot, "mcp-play", "bin", configuration, framework,
            OperatingSystem.IsWindows() ? "ProwlersAndParagons.McpPlay.exe" : "ProwlersAndParagons.McpPlay");

        Assert.True(File.Exists(path),
            $"The encounter server was not built at '{path}'. It is a project reference of this "
            + "test project, so `dotnet test` builds it; a missing binary means the layout moved.");

        return path;
    }
}
