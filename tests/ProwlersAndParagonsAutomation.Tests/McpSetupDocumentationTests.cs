using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Mcp;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The setup guide at <c>docs/MCP-SETUP.md</c>, held to the code it describes.
///
/// <para><b>It is the one document a stranger follows with nothing else open</b>, and it names
/// things that rot silently: six tool names, an environment variable, a project path, and the
/// binary a client is pointed at. None of those breaks a build when it goes stale — it breaks
/// somebody's afternoon, at the point where they have no way to tell whether the instructions
/// or their machine is wrong.</para>
///
/// <para>Same treatment as <see cref="SkillDocumentationTests"/>, and for the same reason: that
/// file's example was documentation of a schema, this one is documentation of a setup, and both
/// are the kind that is only ever read by somebody who cannot check it.</para>
/// </summary>
public sealed class McpSetupDocumentationTests
{
    private static string Path(params string[] parts) =>
        System.IO.Path.Combine([RulesFixture.RepoRoot, .. parts]);

    private static string Guide => File.ReadAllText(Path("docs", "MCP-SETUP.md"));

    /// <summary>
    /// Every tree an MCP server in this repository is compiled from: the character builder, the
    /// encounter server, and the project holding what both of them need before they can serve
    /// anything.
    /// </summary>
    private static readonly string[] ServerTrees = ["mcp", "mcp-shared", "mcp-play"];

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The six wire names, as literals.
    ///
    /// <para>Not the constants on <c>CharacterServer</c>, for the reason
    /// <see cref="McpServerTests"/> already records: comparing a document with the constant it
    /// was written from is a comparison with itself, and renaming a tool would break every
    /// configuration a stranger has written down while passing. The chain that makes this
    /// worth anything is that <c>McpServerTests</c> holds the <em>served</em> names to the same
    /// literals — so the guide, the server and this list either all agree or something fails.
    /// </para>
    /// </summary>
    private static readonly string[] WireNames =
    [
        "character_sheet", "check_character", "creation_guide",
        "list_options", "power_detail", "search_powers"
    ];

    /// <summary>
    /// The names in the guide's table of tools, which is what a reader counts.
    ///
    /// <para><b>The table rows, not the section.</b> The prose under it names
    /// <c>cost_character</c> and <c>validate_character</c> — two tools that deliberately do not
    /// exist, because costing without judging is an invitation to quote a price for a character
    /// that breaks a rule. Reading the whole section fails on the explanation of why the list
    /// is the length it is.</para>
    /// </summary>
    private static List<string> ToolsTheGuideNames()
    {
        var section = Rx("^## The six tools.*?(?=^## )", RegexOptions.Multiline | RegexOptions.Singleline)
            .Match(Guide);

        Assert.True(section.Success, "The guide no longer has a section listing the tools.");

        var rows = section.Value
            .Split('\n')
            .Where(line => line.TrimStart().StartsWith('|'));

        return [.. Rx("`([a-z][a-z_]*)`")
            .Matches(string.Join('\n', rows))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Both directions. A tool dropped from the guide leaves a reader unaware it exists; a tool
    /// named in the guide and not served sends them looking for something that is not there,
    /// which is worse, because they will assume they installed it wrong.
    /// </summary>
    [Fact]
    public void TheGuideNamesExactlyTheToolsTheServerServes() =>
        Assert.Equal(WireNames.Order(StringComparer.Ordinal).ToList(), ToolsTheGuideNames());

    /// <summary>
    /// The variable the guide sends a stuck reader to set is the variable the server reads.
    ///
    /// <para>This is the pairing with the sharpest history in this repository: a mistyped
    /// <c>PROWLERS_RULES_DIR</c> used to fall through to the shipped rules silently, so a
    /// working server on the wrong rulebook was the reward for following a stale instruction.
    /// The fall-through is gone; the instruction going stale is what this covers.</para>
    /// </summary>
    [Fact]
    public void TheGuideNamesTheEnvironmentVariableTheServerActuallyReads()
    {
        Assert.Contains(RulesLocation.OverrideVariable, Guide, StringComparison.Ordinal);

        // And no other variable of that shape, which is how a rename leaves both spellings in
        // a document — the old one in the troubleshooting and the new one in the setup.
        var named = Rx(@"\bPROWLERS_[A-Z_]+\b").Matches(Guide)
            .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();

        Assert.Equal([RulesLocation.OverrideVariable], named);
    }

    /// <summary>
    /// Every publish command names a project file that exists, and the binary the guide tells a
    /// client to point at is the one that project produces. A guide that publishes one project
    /// and registers another is a session that never connects.
    ///
    /// <para><b>Every command, not the first.</b> The guide gives one per shell now, and a
    /// reviewer pointed out that checking only the first leaves the rest free — which turned
    /// out to matter immediately, because the PowerShell one is written with backslashes and
    /// was the one that broke.</para>
    ///
    /// <para>Which is also why the separator is normalised. A Windows-style path in the
    /// document combines cleanly on Windows and not on Linux, so this passed locally and failed
    /// in the container — the whole reason the suite is run there before anything is pushed.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryPublishCommandNamesTheProjectThatProducesTheRegisteredBinary()
    {
        var projects = Rx(@"dotnet publish (\S+\.csproj)").Matches(Guide)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(projects);

        foreach (var project in projects)
        {
            var onDisk = Path(project.Split('/', '\\'));

            Assert.True(File.Exists(onDisk),
                $"The guide publishes '{project}', which is not in this repository.");

            var assemblyName = Rx("<AssemblyName>([^<]+)</AssemblyName>").Match(File.ReadAllText(onDisk));

            Assert.True(assemblyName.Success, $"'{project}' does not set an AssemblyName.");
            Assert.Contains(assemblyName.Groups[1].Value, Guide, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>The guide still contains the things a stranger cannot finish without.</b>
    ///
    /// <para>Every other test here checks that what the guide says is <em>true</em>. None of
    /// them noticed when an adversarial pass cut the document from 104 lines to a 21-line stub
    /// — no registration command, no scope, no Desktop configuration, no troubleshooting — and
    /// the suite stayed green, because everything that remained was accurate. A document can be
    /// wrong by omission and that is the likelier way this one rots: somebody tidying it.</para>
    ///
    /// <para>Deliberately a short list of load-bearing parts rather than a line count. A line
    /// count would fail on an edit that improved it.</para>
    /// </summary>
    [Theory]
    [InlineData("dotnet publish", "how to build the server at all")]
    [InlineData("claude mcp add", "how to register it with Claude Code")]
    [InlineData("--scope user", "the scope, which is the one argument that is easy to get wrong")]
    [InlineData("mcpServers", "the Claude Desktop configuration")]
    [InlineData("claude mcp remove", "how to undo it")]
    [InlineData("## Troubleshooting", "what to do when it does not work")]
    public void TheGuideStillCarriesEveryPartAStrangerNeeds(string needle, string why) =>
        Assert.True(Guide.Contains(needle, StringComparison.Ordinal),
            $"The guide no longer says {why} (looked for '{needle}').");

    /// <summary>
    /// The scope is <c>user</c>, and the guide says why.
    ///
    /// <para>Its own prose calls this "the part that matters": <c>--scope local</c> is the
    /// default and registers the server for this project only, so a reader who follows a guide
    /// that quietly said <c>local</c> gets a server that works in the repository and nowhere
    /// else — which is the opposite of what a character builder is for, and reads as a broken
    /// installation rather than a wrong flag. Changing it was one of the mutations nothing
    /// caught.</para>
    /// </summary>
    [Fact]
    public void EveryRegistrationCommandInTheGuideUsesTheUserScope()
    {
        var commands = Rx(@"claude mcp add[^\r\n]*").Matches(Guide)
            .Select(m => m.Value).ToList();

        Assert.NotEmpty(commands);
        Assert.All(commands, command =>
            Assert.Contains("--scope user", command, StringComparison.Ordinal));
    }

    /// <summary>
    /// The path the guide publishes to is the path it then registers.
    ///
    /// <para>A guide that publishes to one directory and points the client at another is the
    /// single most confusing failure available here, because both commands succeed: the server
    /// is built, the registration is accepted, and the tools never appear. Checked per shell,
    /// because the guide gives three and they are the pairs most likely to drift apart when one
    /// of them is edited.</para>
    /// </summary>
    [Fact]
    public void EveryShellPublishesToThePathItThenRegisters()
    {
        var published = Rx(@"dotnet publish[^\r\n]*-o ""([^""]+)""").Matches(Guide)
            .Select(m => m.Groups[1].Value).ToList();

        var registered = Rx(@"claude mcp add[^\r\n]*-- ""([^""]+)""").Matches(Guide)
            .Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(published);
        Assert.Equal(published.Count, registered.Count);

        // Pairwise and in order, which is how they are written and how a reader takes them.
        foreach (var (directory, binary) in published.Zip(registered))
            Assert.True(
                binary.StartsWith(directory, StringComparison.Ordinal),
                $"The guide publishes to '{directory}' and registers '{binary}', which is "
                + "somewhere else. Both commands would succeed and no tool would appear.");
    }

    /// <summary>
    /// <b>The Claude Desktop half, which was checked by nothing at all.</b>
    ///
    /// <para>Every other test here reads the Claude Code instructions:
    /// <see cref="EveryShellPublishesToThePathItThenRegisters"/> regexes <c>claude mcp add</c>,
    /// and <see cref="EveryPublishCommandNamesTheProjectThatProducesTheRegisteredBinary"/> only
    /// asks that the assembly name appear <em>somewhere</em> in the guide — which the Code
    /// commands satisfy on their own. So breaking the <c>command</c> path in both JSON blocks
    /// left the suite green, and a Desktop user follows the guide and gets nothing, with no way
    /// to tell whether the document or their machine is wrong.</para>
    ///
    /// <para>Three things are checked, because the failures are different: the block has to be
    /// JSON (a hand-edited configuration file is pasted whole, and Desktop refuses the lot if it
    /// will not parse); the server has to be keyed under the name the rest of the guide uses; and
    /// the command has to be an <em>absolute</em> path to the binary this repository builds — the
    /// guide's own prose says why, since a client starts the program from a working directory of
    /// its own choosing.</para>
    /// </summary>
    [Fact]
    public void TheClaudeDesktopConfigurationIsJsonThatNamesThisServersBinary()
    {
        var blocks = Rx("```json\r?\n(.*?)```", RegexOptions.Singleline)
            .Matches(Guide)
            .Select(m => m.Groups[1].Value)
            .Where(b => b.Contains("mcpServers", StringComparison.Ordinal))
            .ToList();

        // One per platform. The guide gives Windows and then macOS/Linux, and the second exists
        // because the first teaches doubled backslashes and no extension is wrong there.
        Assert.Equal(2, blocks.Count);

        var assemblyName = Rx("<AssemblyName>([^<]+)</AssemblyName>")
            .Match(File.ReadAllText(Path("mcp", "ProwlersAndParagons.Mcp.csproj")))
            .Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(assemblyName));

        foreach (var block in blocks)
        {
            var parsed = JsonNode.Parse(block);

            Assert.NotNull(parsed);

            var servers = parsed!["mcpServers"]!.AsObject();
            var entry = Assert.Single(servers);

            // The same name the Claude Code commands register, so somebody reading both halves
            // is told about one server rather than two.
            Assert.Equal(CharacterServer.Name, entry.Key);

            var command = entry.Value!["command"]!.GetValue<string>();

            // <b>Absolute by the shape the document writes, not by the running host's rules.</b>
            // Path.IsPathRooted(@"C:\Users\…") is false on Linux — a backslash is not a separator
            // there and "C:" is not a root — so asking the framework would fail this test in CI
            // on the Windows block while passing locally. That is the mistake McpStdioTests
            // records having made once already, in the other direction.
            Assert.True(Rx(@"^([A-Za-z]:[\\/]|/)").IsMatch(command),
                $"The Desktop configuration points at '{command}', which is not an absolute path. "
                + "A client starts the program from a working directory of its own choosing.");

            Assert.Contains(assemblyName, command, StringComparison.Ordinal);

            // And it is the binary rather than a directory or the project, which is the mistake
            // that produces a server Desktop reports as "failed to start" and nothing else.
            //
            // <b>The extension follows whether the path is written for Windows — any drive
            // letter, not the letter C.</b> `(\.exe)?$` on its own accepts `.exe` on the macOS
            // block, contradicting the guide's own prose two paragraphs above it; and a version
            // of this that asked `StartsWith("C:")` let a `D:` path drop the extension, which
            // sends a Windows Desktop user to a file that does not exist.
            var windows = Rx("^[A-Za-z]:").IsMatch(command);

            Assert.Matches($"{Regex.Escape(assemblyName)}{(windows ? @"\.exe" : "")}$", command);

            // <b>And the directory is one step 1 actually publishes to.</b> This checked JSON,
            // the server key, absoluteness and the file name, and never the directory — so both
            // blocks could point at somewhere no publish command produces and pass, which is the
            // "follows the guide and gets nothing" failure this test exists for. It is the pairing
            // EveryShellPublishesToThePathItThenRegisters does for Claude Code, whose absence here
            // was the whole finding.
            Assert.Contains(PublishTargets(),
                target => Slashes(command).Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        // The Windows one has its backslashes doubled and the other has none, which is the
        // difference the two blocks exist to show — and a single JSON escape gone wrong is the
        // likeliest way this rots, because it still parses.
        Assert.Contains(blocks, b => b.Contains("\\\\", StringComparison.Ordinal));
        Assert.Contains(blocks, b => !b.Contains('\\', StringComparison.Ordinal));
    }

    /// <summary>
    /// Where step 1 publishes to, with the leading shell variable dropped and the separators
    /// normalised — the part of the path a Desktop configuration writing it out in full has to
    /// contain. <c>$env:LOCALAPPDATA\ProwlersAndParagons\mcp-server</c> and
    /// <c>C:\Users\you\AppData\Local\ProwlersAndParagons\mcp-server\…</c> are the same directory
    /// spelt two ways, and the shared tail is what says so.
    /// </summary>
    private static List<string> PublishTargets()
    {
        var targets = Rx(@"dotnet publish[^\r\n]*-o ""([^""]+)""").Matches(Guide)
            .Select(m => Slashes(m.Groups[1].Value))
            .Select(path => Rx(@"^\$(env:)?[A-Za-z_]+/").Replace(path, ""))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(targets);

        // A target that is still just a variable would match everything, which would make the
        // assertion above it worthless rather than wrong.
        Assert.DoesNotContain(targets, t => t.Contains('$', StringComparison.Ordinal));

        return targets;
    }

    private static string Slashes(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Every relative link in the guide resolves. It moved out of the README, and a link that
    /// was right at the repository root is one directory wrong here — which is exactly the
    /// error a move makes and the only one nothing else would catch.
    ///
    /// <para><b>Anchored links are checked too.</b> The first version excluded anything
    /// containing a <c>#</c>, so a link to a heading was skipped in silence — and had every
    /// link acquired an anchor, the test would have asserted over an empty set and passed on
    /// nothing at all. The anchor is dropped and the file part is what is checked.</para>
    /// </summary>
    [Fact]
    public void EveryRelativeLinkInTheGuideResolves()
    {
        var links = Rx(@"\]\((?!https?:)([^)]+)\)").Matches(Guide)
            .Select(m => m.Groups[1].Value.Split('#')[0])
            .Where(path => path.Length > 0)   // a bare "#anchor" points inside this document
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.True(
            System.IO.Path.Exists(Path(["docs", .. link.Split('/')])),
            $"The guide links to '{link}', which does not exist from docs/."));
    }

    /// <summary>
    /// And the README still <em>links</em> to it. The guide is only reachable through that link,
    /// so a section rewritten without it leaves a document nobody finds — which is the same as
    /// not having written it.
    ///
    /// <para><b>A markdown link, not the bare string.</b> A substring check is satisfied by the
    /// directory-tree diagram alone, and by prose reading "docs/MCP-SETUP.md was deleted; ask in
    /// the issue tracker" — both mention the path and neither gets a reader there. That was a
    /// mutation nothing caught.</para>
    /// </summary>
    [Fact]
    public void TheReadmeLinksToTheGuide()
    {
        var readme = File.ReadAllText(Path("README.md"));

        Assert.Matches(@"\]\(docs/MCP-SETUP\.md\)", readme);
    }

    /// <summary>
    /// <b>Nothing in <c>mcp/</c> sends a reader to the README for setup.</b>
    ///
    /// <para>This is the failure that prompted the test, found by a reviewer rather than by
    /// anything here: the setup moved out of the README, and three places in the server were
    /// left pointing at where it used to be — including the program's own <c>--help</c>, which
    /// is text a stuck person reads on the way to being more stuck. `CLAUDE.md` and
    /// `PROGRESS.md` had the identical sentence corrected in the same commit; the code did
    /// not, because prose in a source file is the copy nobody greps for.</para>
    ///
    /// <para>Matched on the file name rather than on the word "README", so that a sentence
    /// mentioning the README for some other reason is still allowed to exist.</para>
    /// </summary>
    [Fact]
    public void NothingInTheServerSendsAReaderToTheReadmeForSetup()
    {
        var sources = ServerTrees
            .SelectMany(tree => Directory.GetFiles(Path(tree), "*.cs", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        // All three trees, because the sentence this catches is prose in a source file and the
        // second server's `--help` is the same text in a second place. A scan of one tree would
        // report the repository clean while the newer program sent a stuck reader nowhere.
        Assert.True(sources.Count >= 8, $"Only {sources.Count} server sources were read.");

        Assert.All(sources, file => Assert.False(
            Rx(@"\bREADME(\.md)?\b").IsMatch(File.ReadAllText(file)),
            $"{System.IO.Path.GetFileName(file)} points a reader at the README. The setup a "
            + "stranger needs is docs/MCP-SETUP.md."));
    }

    /// <summary>
    /// No fenced block in the guide starts the server through <c>dotnet run</c>.
    ///
    /// <para><b>The reason is no longer the one this test was written for, and the assertion
    /// outlived it.</b> It used to be that MSBuild wrote its progress to standard output, where
    /// the protocol lives, so a client reading it saw a corrupt stream. Measured on the .NET 10
    /// SDK this repository pins — a launch driven through a forced full NuGet restore and a
    /// recompile — standard output carried 4,448 bytes and every one of them was protocol. The
    /// blanket warning was too broad, and <c>.mcp.json</c> now relies on it not being true.</para>
    ///
    /// <para>What survives is narrower and still worth holding: <b>a fenced block is the part
    /// people copy</b>, and every fenced block in this guide is for the case where the client is
    /// <em>not</em> working inside a checkout. <c>dotnet run</c> needs the checkout, so a copyable
    /// block offering it hands a stranger a command that breaks the moment they move or delete the
    /// folder — which is the failure recorded as item 18 in <c>PROGRESS.md</c>, in a different
    /// spelling. The in-checkout case is served by <c>.mcp.json</c>, which is not a block anybody
    /// copies, and is held by <see cref="TheProjectRegistrationNamesAProjectThatIsThere"/>.</para>
    ///
    /// <para><b>Every fenced block, whatever it is tagged.</b> The first version listed
    /// <c>bash</c> and <c>json</c>, and a <c>powershell</c> block carrying
    /// <c>dotnet run</c> walked straight past it — a tag the guide's own prose invites, since
    /// it gives PowerShell commands. A test that names the spellings it will look at is a test
    /// that misses the next one.</para>
    /// </summary>
    [Fact]
    public void NoCommandInTheGuideStartsTheServerThroughTheBuildTool()
    {
        var blocks = Rx("```[a-z]*\r?\n(.*?)```", RegexOptions.Singleline)
            .Matches(Guide)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(blocks);
        Assert.All(blocks, block =>
            Assert.DoesNotContain("dotnet run", block, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The checked-in registration runs what section 0 tells you to publish.</b>
    ///
    /// <para>This is the guard for the failure that has actually cost this repository a session.
    /// The MCP server was registered at a path outside the checkout, the file at that path was
    /// not there, and the client reported <c>CONNECTION_CLOSED</c> — a failure with no server log
    /// to read, because no process ever started. Nothing in a build could see it: the registration
    /// lived in a file on one machine that no test had ever heard of.</para>
    ///
    /// <para><b>Moving the registration into the repository is what makes it checkable at all</b>,
    /// and this is the check. It is deliberately about the things that rot — the path and the
    /// command — rather than about every flag beside them.</para>
    ///
    /// <para><b>It used to assert that the registered <c>--project</c> was on disk, and it cannot
    /// any more.</b> The registration runs a published copy rather than the project, because a
    /// server holding <c>mcp/bin/Release</c> fails a Release build of this repository outright;
    /// and a published copy is git-ignored build output, so a clone does not have one to point at.
    /// What replaces existence is the pairing that actually rots: the guide publishes a project to
    /// a directory, and the registration runs an assembly out of <em>that</em> directory. Both
    /// halves are checked here, the project file is still required to exist, and the directory is
    /// still required to be ignored — a committed copy would go stale in silence.</para>
    ///
    /// <para><b>And <c>exec</c> is asserted rather than assumed.</b> <c>dotnet exec missing.dll</c>
    /// exits 129 with an empty standard output and one line on standard error; plain
    /// <c>dotnet missing.dll</c> exits 1 and puts its "Possible reasons for this include" block on
    /// standard <em>output</em>, which is the stream the protocol lives on. Both measured. Dropping
    /// the word is a one-token edit that leaves a working server and breaks the never-published
    /// case in exactly the way this repository has spent three corrections on.</para>
    /// </summary>
    /// <param name="name">The wire name of the server entry, as a client sees it.</param>
    [Theory]
    [InlineData("prowlers-and-paragons")]
    [InlineData("prowlers-and-paragons-play")]
    public void TheCheckedInRegistrationRunsWhatSectionZeroPublishes(string name)
    {
        var registration = JsonNode.Parse(File.ReadAllText(Path(".mcp.json")))!;
        var server = registration["mcpServers"]?[name];

        Assert.NotNull(server);
        Assert.Equal("dotnet", server!["command"]?.GetValue<string>());

        var arguments = server["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToList();

        Assert.Equal("exec", arguments[0]);

        var assembly = Slashes(arguments[^1]);
        var directory = assembly[..assembly.LastIndexOf('/')];

        Assert.Contains($"{directory}/", File.ReadAllText(Path(".gitignore")), StringComparison.Ordinal);

        // Every publish command in the guide that lands in that directory. Matched by where it
        // publishes to rather than by document order, so this cannot be satisfied by whichever
        // block happens to come first.
        var publishes = Rx(@"dotnet publish (\S+\.csproj) -c Release -o (\S+)").Matches(Guide)
            .Select(m => (Project: m.Groups[1].Value, Output: Slashes(m.Groups[2].Value.Trim('"'))))
            .Where(p => p.Output.TrimEnd('/') == directory)
            .ToList();

        Assert.True(publishes.Count > 0,
            $".mcp.json runs an assembly out of '{directory}', and no command in the guide "
            + "publishes there. Both would look right on their own and no tool would appear.");

        foreach (var (project, _) in publishes)
        {
            var onDisk = Path(project.Split('/', '\\'));

            Assert.True(File.Exists(onDisk),
                $"The guide publishes '{project}', which is not in this repository.");

            var assemblyName = Rx("<AssemblyName>([^<]+)</AssemblyName>")
                .Match(File.ReadAllText(onDisk)).Groups[1].Value;

            Assert.False(string.IsNullOrWhiteSpace(assemblyName));

            Assert.Equal($"{assemblyName}.dll", assembly[(directory.Length + 1)..]);
        }
    }


    /// <summary>
    /// <b>The two registrations are two programs, not one entry copied twice.</b>
    ///
    /// <para>Both are published from different projects to different directories, and the failure
    /// worth guarding is the one that looks right in every other test here: a copy-and-paste that
    /// leaves both entries running the same assembly. A client would then register two servers,
    /// connect both, and get the character builder's six tools under two names — with nothing
    /// anywhere saying why the encounter tools never appeared.</para>
    /// </summary>
    [Fact]
    public void TheTwoRegistrationsRunTwoDifferentPrograms()
    {
        var servers = JsonNode.Parse(File.ReadAllText(Path(".mcp.json")))!["mcpServers"]!.AsObject();

        Assert.Equal(2, servers.Count);

        var assemblies = servers
            .Select(entry => entry.Value!["args"]!.AsArray()[^1]!.GetValue<string>())
            .ToList();

        Assert.Equal(assemblies.Count, assemblies.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // And they are published to different directories, so one publish cannot overwrite the
        // other's binary — which is what a shared output directory would silently do.
        var directories = assemblies
            .Select(a => Slashes(a)[..Slashes(a).LastIndexOf('/')])
            .ToList();

        Assert.Equal(directories.Count, directories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// <b>Section 0 publishes both servers.</b> A guide that publishes one and registers two leaves
    /// a reader with half a working checkout and a `CONNECTION_CLOSED` for the other half — the
    /// failure item 18 in <c>PROGRESS.md</c> records, in a second spelling.
    /// </summary>
    [Fact]
    public void SectionZeroPublishesBothServers()
    {
        var published = Rx(@"dotnet publish (\S+\.csproj) -c Release -o (\S+)").Matches(Guide)
            .Select(m => Slashes(m.Groups[2].Value.Trim('"')).TrimEnd('/'))
            .ToList();

        foreach (var directory in new[] { "mcp-server", "mcp-play-server" })
            Assert.Contains(directory, published, StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>The registration is relative, so a clone, a worktree and another machine are all
    /// already right.</b> An absolute path is correct on exactly one computer, and the guide used
    /// to say — wrongly — that this was a reason not to check a registration in at all.
    /// </summary>
    [Theory]
    [InlineData("prowlers-and-paragons")]
    [InlineData("prowlers-and-paragons-play")]
    public void TheProjectRegistrationIsRelativeToTheCheckout(string name)
    {
        var registration = JsonNode.Parse(File.ReadAllText(Path(".mcp.json")))!;
        var arguments = registration["mcpServers"]![name]!["args"]!
            .AsArray().Select(a => a!.GetValue<string>());

        Assert.All(arguments, argument => Assert.False(
            System.IO.Path.IsPathRooted(argument) || argument.Contains(':'),
            $"'{argument}' is an absolute path. It is right on the machine it was written on and "
            + "on no other, which is the whole reason this registration is in the repository."));
    }
}
