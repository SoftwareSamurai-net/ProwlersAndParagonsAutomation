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

                var index = line.IndexOf("Console.", StringComparison.Ordinal);
                if (index < 0) continue;

                // A comment about standard output is not a write to it, and this file's own
                // reasoning is written down in several of them.
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.AsSpan(index).StartsWith("Console.Error", StringComparison.Ordinal)) continue;

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Standard output carries the protocol and nothing else, so the server may only "
            + "write to Console.Error:\n" + string.Join("\n", offenders));
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
    /// </summary>
    [Fact]
    public void AnExplicitDirectoryAndTheEnvironmentBothOutrankTheShippedCopy()
    {
        var withBoth = RulesLocation.Candidates(
            "explicit", v => v == RulesLocation.OverrideVariable ? "from-environment" : null,
            Path.Combine("C:", "app")).ToList();

        Assert.Equal("explicit", withBoth[0]);
        Assert.Equal("from-environment", withBoth[1]);

        var withEnvironmentOnly = RulesLocation.Candidates(
            null, v => v == RulesLocation.OverrideVariable ? "from-environment" : null,
            Path.Combine("C:", "app")).ToList();

        Assert.Equal("from-environment", withEnvironmentOnly[0]);
    }

    /// <summary>
    /// <b>No rules is null, not a guess.</b> A repository built for a directory that is not
    /// there gets as far as a connected session and then answers every question with an
    /// error, several layers from the cause.
    /// </summary>
    [Fact]
    public void NoRulesAnywhereIsAnAnswerRatherThanAGuess()
    {
        Assert.Null(RulesLocation.Find(null, _ => null, Path.Combine("C:", "app"), _ => false));

        Assert.Equal(
            Path.Combine("C:", "app", "data", "rules"),
            RulesLocation.Find(null, _ => null, Path.Combine("C:", "app"),
                d => d == Path.Combine("C:", "app", "data", "rules")));
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
}
