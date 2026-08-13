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
        var sources = Directory
            .GetFiles(Path("mcp"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sources);

        Assert.All(sources, file => Assert.False(
            Rx(@"\bREADME(\.md)?\b").IsMatch(File.ReadAllText(file)),
            $"{System.IO.Path.GetFileName(file)} points a reader at the README. The setup a "
            + "stranger needs is docs/MCP-SETUP.md."));
    }

    /// <summary>
    /// The guide does not tell anybody to point a client at <c>dotnet run</c>.
    ///
    /// <para>MSBuild writes its own progress to standard output, which is where the protocol
    /// lives, so a client reading it sees a corrupt stream and drops the session. The guide
    /// warns about this in as many words; what this checks is that no <em>instruction</em> in
    /// it does the opposite — a command block is the part people copy.</para>
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
}
