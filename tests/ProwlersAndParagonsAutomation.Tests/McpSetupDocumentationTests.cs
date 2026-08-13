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
    /// The publish command names a project file that exists, and the binary it tells a client
    /// to point at is the one that project produces. A guide that publishes one project and
    /// registers another is a session that never connects.
    /// </summary>
    [Fact]
    public void TheGuidePublishesTheProjectThatProducesTheBinaryItRegisters()
    {
        var project = Rx(@"dotnet publish (\S+\.csproj)").Match(Guide);
        Assert.True(project.Success, "The guide no longer says which project to publish.");
        Assert.True(File.Exists(Path(project.Groups[1].Value.Split('/'))),
            $"The guide publishes '{project.Groups[1].Value}', which is not in this repository.");

        var assemblyName = Rx("<AssemblyName>([^<]+)</AssemblyName>")
            .Match(File.ReadAllText(Path(project.Groups[1].Value.Split('/'))));

        Assert.True(assemblyName.Success, "The MCP project does not set an AssemblyName.");
        Assert.Contains($"{assemblyName.Groups[1].Value}.exe", Guide, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every relative link in the guide resolves. It moved out of the README, and a link that
    /// was right at the repository root is one directory wrong here — which is exactly the
    /// error a move makes and the only one nothing else would catch.
    /// </summary>
    [Fact]
    public void EveryRelativeLinkInTheGuideResolves()
    {
        var links = Rx(@"\]\((?!https?:)([^)#]+)\)").Matches(Guide)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.True(
            System.IO.Path.Exists(Path(["docs", .. link.Split('/')])),
            $"The guide links to '{link}', which does not exist from docs/."));
    }

    /// <summary>
    /// And the README still points at it. The guide is only reachable through that link, so a
    /// section rewritten without it leaves a document nobody finds — which is the same as not
    /// having written it.
    /// </summary>
    [Fact]
    public void TheReadmePointsAtTheGuide()
    {
        var readme = File.ReadAllText(Path("README.md"));

        Assert.Contains("docs/MCP-SETUP.md", readme, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guide does not tell anybody to point a client at <c>dotnet run</c>.
    ///
    /// <para>MSBuild writes its own progress to standard output, which is where the protocol
    /// lives, so a client reading it sees a corrupt stream and drops the session. The guide
    /// warns about this in as many words; what this checks is that no <em>instruction</em> in
    /// it does the opposite — a command block is the part people copy.</para>
    /// </summary>
    [Fact]
    public void NoCommandInTheGuideStartsTheServerThroughTheBuildTool()
    {
        var commands = Rx("```(?:bash|json)\r?\n(.*?)```", RegexOptions.Singleline)
            .Matches(Guide)
            .Select(m => m.Groups[1].Value);

        Assert.All(commands, block =>
            Assert.DoesNotContain("dotnet run", block, StringComparison.Ordinal));
    }
}
