using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><c>data/rules/play/</c> is a subdirectory as an access-control decision, and this is the
/// proof rather than the claim.</b>
///
/// <para>Three csproj files copy the rules data and every one of them globs
/// <c>data\rules\*.json</c> with a single star, which does not descend. The third of them stages
/// into <c>wwwroot</c>, and <b>a file under <c>wwwroot</c> is a public URL</b> — the same reasoning
/// that keeps the rulebook corpus out of the payload, where the placement is likewise the whole
/// of the access control. Play rules are not secret; they are a store no visitor's browser has any
/// use for, and shipping them would put Chapters 3–5 into every first page load for nothing.</para>
///
/// <para><b>Why a test and not a comment.</b> The property survives only as long as nobody writes
/// <c>**</c> for convenience, and nothing about that edit looks wrong: the site keeps working, the
/// suite keeps passing, and the payload silently grows a store. This repository has shipped the
/// same shape of silent regression before — see the transcripts, which were staged into
/// <c>wwwroot</c> for every anonymous visitor until somebody looked.</para>
/// </summary>
public sealed class PlayPayloadTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;
    private static string RulesDirectory => Path.Combine(RepoRoot, "data", "rules");
    private static string PlayDirectory => Path.Combine(RulesDirectory, "play");

    /// <summary>The three projects that copy the rules data, and the file each does it in.</summary>
    private static readonly string[] ProjectFiles =
    [
        Path.Combine(RepoRoot, "ProwlersAndParagonsAutomation.csproj"),
        Path.Combine(RepoRoot, "mcp", "ProwlersAndParagons.Mcp.csproj"),
        Path.Combine(RepoRoot, "web", "ProwlersAndParagons.Web.csproj")
    ];

    /// <summary>
    /// Every <c>Include=</c> in a project file that names the rules directory, with XML comments
    /// blanked first.
    ///
    /// <para><b>Comments are stripped for the reason <c>TheEngineHasNoFilesystemAccess</c> strips
    /// them:</b> the web csproj's own commentary quotes a glob to explain why the obvious spelling
    /// does not work, and a guard that cannot tell an explanation from a directive taxes the
    /// explanation.</para>
    /// </summary>
    private static List<(string Project, string Include)> RulesIncludes()
    {
        var found = new List<(string, string)>();

        foreach (var project in ProjectFiles)
        {
            var xml = Regex.Replace(File.ReadAllText(project), "<!--.*?-->", " ", RegexOptions.Singleline);

            foreach (Match match in Regex.Matches(xml, @"Include=""([^""]*data\\rules[^""]*)"""))
            {
                found.Add((Path.GetFileName(project), match.Groups[1].Value));
            }
        }

        return found;
    }

    /// <summary>
    /// A single star does not descend; <c>**</c> does. This is the one edit that would put the
    /// play rules into the browser payload, and it is a two-character edit.
    /// </summary>
    [Fact]
    public void NoProjectGlobsTheRulesDirectoryRecursively()
    {
        var includes = RulesIncludes();

        // Positive control on the extraction. A regex that stopped matching — an attribute
        // reordered, a path spelled with forward slashes — would find no globs at all and satisfy
        // every assertion below without reading a line of any project file.
        Assert.True(
            includes.Count >= 4,
            $"Found only {includes.Count} rules-data Include globs across the three projects that "
            + "copy them. The extraction has stopped matching; fix it rather than the assertion. "
            + $"Found: {string.Join(" | ", includes.Select(i => $"{i.Project}: {i.Include}"))}");

        // And all three projects have to be represented, or one could quietly go recursive while
        // the count above is satisfied by the other two.
        Assert.Equal(3, includes.Select(i => i.Project).Distinct(StringComparer.Ordinal).Count());

        var recursive = includes.Where(i => i.Include.Contains("**", StringComparison.Ordinal)).ToList();

        Assert.True(
            recursive.Count == 0,
            "These globs descend into subdirectories, which would sweep data/rules/play/ into the "
            + "copy — and for web/, into wwwroot, where it becomes a public URL: "
            + string.Join(" | ", recursive.Select(i => $"{i.Project}: {i.Include}")));
    }

    /// <summary>
    /// The claim the placement rests on, expanded the way MSBuild expands it:
    /// <c>Directory.GetFiles(dir, "*.json")</c> is exactly what <c>data\rules\*.json</c> matches.
    /// </summary>
    [Fact]
    public void TheGlobTheProjectsUseMatchesNoPlayFile()
    {
        var matched = Directory.GetFiles(RulesDirectory, "*.json");
        var everything = Directory.GetFiles(RulesDirectory, "*.json", SearchOption.AllDirectories);

        // Two positive controls, and both are needed. The first says the glob found the character
        // rules at all; the second says there is genuinely something below it for a recursive glob
        // to have picked up — without it this test would pass trivially on a tree where
        // data/rules/play/ had been deleted, which is the state it exists to notice.
        Assert.True(matched.Length >= 11, $"The glob matched {matched.Length} files in {RulesDirectory}.");
        Assert.True(
            everything.Length > matched.Length,
            "A recursive search found no more than the flat one, so data/rules/ has no "
            + "subdirectory and this test is measuring nothing.");

        var swept = matched.Where(IsUnderPlay).ToList();

        Assert.True(
            swept.Count == 0,
            "These play rules files are matched by the glob every host copies: "
            + string.Join(", ", swept.Select(Path.GetFileName)));

        // The other direction: the play files exist, and exist where this test thinks they do.
        var play = everything.Where(IsUnderPlay).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["challenge.json", "play_meta.json"], play);

        // <b>And by name as well as by path, which the path check alone misses.</b> Found by
        // mutation: copying data/rules/play/challenge.json up one level leaves it outside the
        // play directory, so IsUnderPlay says nothing about it — while it is now swept into
        // every host's copy, which is the whole failure. A file sharing a name with one under
        // play/ is that mistake and nothing else.
        var byName = matched
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => play.Contains(n, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            byName.Count == 0,
            "A play rules file has been copied or moved up into data/rules/, where every host's "
            + $"glob sweeps it: {string.Join(", ", byName)}. It belongs in data/rules/play/.");
    }

    private static bool IsUnderPlay(string path) =>
        Path.GetFullPath(path).StartsWith(
            PlayDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    /// The globs above are the mechanism; these are the directories they actually wrote. Checking
    /// the output as well as the source is the difference between "the rule says so" and "the copy
    /// does not contain it".
    /// </summary>
    [Theory]
    [InlineData("web")]
    [InlineData("test-host")]
    public void NoPlayFileReachesACopiedRulesDirectory(string which)
    {
        var directory = which switch
        {
            // What web/'s StageRulesDataInWwwroot target writes, and what the publish uploads.
            "web" => Path.Combine(RepoRoot, "web", "wwwroot", "data", "rules"),

            // The CLI's own copy, which this test project inherits by referencing it. Stands in
            // for the MCP server's, which is the identical Content item.
            _ => Path.Combine(AppContext.BaseDirectory, "data", "rules")
        };

        // Positive control before the absence assertion: an empty or missing directory satisfies
        // "contains no play file" completely while proving nothing at all.
        Assert.True(
            Directory.Exists(directory),
            $"{directory} does not exist, so this check would pass without looking at anything. "
            + "It is written by a build; run one.");

        var copied = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);

        Assert.True(
            copied.Length >= 11,
            $"{directory} holds {copied.Length} rules files, which is fewer than the character "
            + "rules alone. A stale or partial copy makes the assertion below meaningless.");

        var play = copied
            .Where(f => Path.GetFileName(f) is "challenge.json" or "play_meta.json"
                        || f.Contains($"{Path.DirectorySeparatorChar}play{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            play.Count == 0,
            $"Play rules reached {directory}: {string.Join(", ", play)}. For web/ that directory "
            + "is served publicly. Delete the copy — a stale wwwroot/data is not swept by a build; "
            + "see the comment in web/ProwlersAndParagons.Web.csproj.");
    }

    /// <summary>
    /// <c>DataFileNames</c> is the contract a self-loading host fetches over HTTP. A play file on
    /// that list would be requested by every browser at boot, which is the same exposure by
    /// another route.
    /// </summary>
    [Fact]
    public void NoPlayRulesFileIsListedForASelfLoadingHost()
    {
        Assert.DoesNotContain("challenge.json", RulesRepository.DataFileNames);
        Assert.DoesNotContain("play_meta.json", RulesRepository.DataFileNames);
    }
}
