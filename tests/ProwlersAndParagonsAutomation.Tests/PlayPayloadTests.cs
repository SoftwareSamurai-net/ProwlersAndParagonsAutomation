using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><c>data/rules/play/</c> is a subdirectory as an access-control decision, and this is the
/// proof rather than the claim.</b>
///
/// <para>Four csproj files copy the rules data and every one of them globs
/// <c>data\rules\*.json</c> with a single star, which does not descend. One of them stages
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

    /// <summary>
    /// The four projects that copy the rules data, and the file each does it in.
    ///
    /// <para><b><c>mcp-play/</c> is on this list because it copies the rules too, not because it is
    /// forbidden the play ones.</b> It is the one project that carries them deliberately — a client
    /// launches the published encounter server from a directory of its own choosing, and it cannot
    /// resolve a single action without them. What the scan below still holds it to is the property
    /// that matters: <b>a single star, which does not descend</b>. A project left off this list is
    /// a project free to write <c>**</c>, and that is a two-character edit.</para>
    /// </summary>
    private static readonly string[] ProjectFiles =
    [
        Path.Combine(RepoRoot, "ProwlersAndParagonsAutomation.csproj"),
        Path.Combine(RepoRoot, "mcp", "ProwlersAndParagons.Mcp.csproj"),
        Path.Combine(RepoRoot, "mcp-play", "ProwlersAndParagons.McpPlay.csproj"),
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
    ///
    /// <para><b>Both separators match, and that decides which failure you get.</b> MSBuild accepts
    /// <c>data/rules/**/*.json</c> exactly as it accepts the backslash spelling, and a pattern that
    /// only matched <c>data\rules</c> would find no globs at all in a rewritten project — so the
    /// recursive edit this file exists to catch would have surfaced as "the extraction has stopped
    /// matching" rather than as "this glob descends into subdirectories". A guard that reports the
    /// wrong failure sends the next reader to fix the wrong thing.</para>
    /// </summary>
    private static List<(string Project, string Include)> RulesIncludes()
    {
        var found = new List<(string, string)>();

        foreach (var project in ProjectFiles)
        {
            var xml = Regex.Replace(File.ReadAllText(project), "<!--.*?-->", " ", RegexOptions.Singleline);

            foreach (Match match in Regex.Matches(xml, @"Include=""([^""]*data[\\/]rules[^""]*)"""))
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
            includes.Count >= 5,
            $"Found only {includes.Count} rules-data Include globs across the four projects that "
            + "copy them. The extraction has stopped matching; fix it rather than the assertion. "
            + $"Found: {string.Join(" | ", includes.Select(i => $"{i.Project}: {i.Include}"))}");

        // And all four projects have to be represented, or one could quietly go recursive while
        // the count above is satisfied by the others.
        Assert.Equal(4, includes.Select(i => i.Project).Distinct(StringComparer.Ordinal).Count());

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
        var play = everything.Where(IsUnderPlay).Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.Ordinal).ToList();
        Assert.Equal(PlayFileNames.Order(StringComparer.Ordinal).ToList(), play);

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

    /// <summary>
    /// The staging expectation with no staged directory to read: <c>web/</c>'s own
    /// <c>RulesDataFile</c> glob, expanded from the project directory the way MSBuild expands it.
    /// Weaker than reading the copy — it says what the build would write, not what it wrote — and
    /// the failure message says so, so nobody mistakes one for the other.
    /// </summary>
    private static void AssertTheWebGlobWouldStageNoPlayFile()
    {
        var webDirectory = Path.Combine(RepoRoot, "web");
        var staged = new List<string>();

        foreach (var (_, include) in RulesIncludes()
                     .Where(i => string.Equals(i.Project, "ProwlersAndParagons.Web.csproj", StringComparison.Ordinal)))
        {
            // The wwwroot Content item re-includes what the target already wrote; the source-side
            // glob is the one that decides what gets written.
            if (include.Contains("wwwroot", StringComparison.OrdinalIgnoreCase)) continue;

            var pattern = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var directory = Path.GetFullPath(Path.Combine(webDirectory, Path.GetDirectoryName(pattern) ?? string.Empty));

            staged.AddRange(Directory.GetFiles(directory, Path.GetFileName(pattern)));
        }

        // Same two controls as the on-disk check: the glob has to have matched the character rules,
        // or an expectation of nothing would be satisfied by nothing.
        Assert.True(
            staged.Count >= 11,
            $"web/'s rules glob expands to {staged.Count} files, which is fewer than the character "
            + "rules alone, so this substitute for the staged directory is measuring nothing.");

        var play = staged
            .Where(f => IsUnderPlay(f) || PlayFileNames.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .ToList();

        Assert.True(
            play.Count == 0,
            "web/wwwroot/data/rules has not been built, so this was checked against web/'s own glob "
            + "instead of against the staged copy — and the glob would stage these play files into "
            + "wwwroot, where they become public URLs: " + string.Join(", ", play));
    }

    /// <summary>
    /// Every file in <c>data/rules/play/</c>, by name. <b>Named as well as pathed</b>, because a
    /// play file copied up one level is outside the directory and inside every host's glob — which
    /// is the whole failure, and which a path check alone misses.
    /// </summary>
    private static readonly string[] PlayFileNames =
        ["challenge.json", "combat.json", "equipment.json", "gritty.json", "play_meta.json",
         "resolve.json"];

    private static bool IsUnderPlay(string path) =>
        Path.GetFullPath(path).StartsWith(
            PlayDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    /// The globs above are the mechanism; these are the directories they actually wrote. Checking
    /// the output as well as the source is the difference between "the rule says so" and "the copy
    /// does not contain it".
    ///
    /// <para><b>The web directory is not always there, and this used to fail rather than say so.</b>
    /// <c>web/wwwroot/data/rules</c> is written by <c>web/</c>'s own build, and this test project
    /// does not reference <c>web/</c> — so a solution-wide <c>dotnet test</c> happens to have staged
    /// it (the bUnit project pulls <c>web/</c> in) while running this project alone does not. The
    /// answer is not to skip: when the directory is absent the expectation is built from the
    /// csproj's own glob instead, which is what MSBuild would have staged, and the assertion says
    /// which of the two it read. A check that reports "run a build" is a check nobody runs.</para>
    ///
    /// <para><b>It used to read this test project's own output directory, as a stand-in for the
    /// character server's identical <c>Content</c> item — and the stand-in stopped standing in the
    /// moment a second server was referenced here.</b> <c>mcp-play/</c> copies the play rules
    /// deliberately, MSBuild flows a referenced project's content into the referencing one, and so
    /// this assembly's neighbouring <c>data/rules/play</c> is now correct rather than a fault. The
    /// two directories that are actually launched are read instead, which is what the stand-in was
    /// standing in for; <see cref="TheEncounterServerIsTheOneCopyThatCarriesThePlayRules"/> is the
    /// other half, so the allowance has something behind it.</para>
    /// </summary>
    [Theory]
    [InlineData("web")]
    [InlineData("cli")]
    [InlineData("mcp")]
    public void NoPlayFileReachesACopiedRulesDirectory(string which)
    {
        var directory = which switch
        {
            // What web/'s StageRulesDataInWwwroot target writes, and what the publish uploads.
            "web" => Path.Combine(RepoRoot, "web", "wwwroot", "data", "rules"),

            // The terminal wizard's own copy.
            "cli" => Path.Combine(Built(RepoRoot), "data", "rules"),

            // The character server's, which is what a client launches.
            _ => Path.Combine(Built(Path.Combine(RepoRoot, "mcp")), "data", "rules")
        };

        if (which == "web" && !Directory.Exists(directory))
        {
            AssertTheWebGlobWouldStageNoPlayFile();
            return;
        }

        // Positive control before the absence assertion: an empty or missing directory satisfies
        // "contains no play file" completely while proving nothing at all. The CLI's copy and the
        // character server's both reach here unconditionally — this test project references both,
        // so a build that produced this assembly produced those directories too.
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
            .Where(f => PlayFileNames.Contains(Path.GetFileName(f), StringComparer.Ordinal)
                        || f.Contains($"{Path.DirectorySeparatorChar}play{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            play.Count == 0,
            $"Play rules reached {directory}: {string.Join(", ", play)}. For web/ that directory "
            + "is served publicly. Delete the copy — a stale wwwroot/data is not swept by a build; "
            + "see the comment in web/ProwlersAndParagons.Web.csproj.");
    }


    /// <summary>
    /// <b>The allowance has something behind it: the encounter server's copy holds both stores.</b>
    ///
    /// <para>Every other test here says where a play file may not go. An allowance with nothing
    /// behind it would silently permit a whole project — a <c>Content</c> item deleted, a
    /// <c>LinkBase</c> mistyped — and the copy would go on looking clean while the published server
    /// refused to start on every machine that installed it. So the one project excused from the
    /// rule is required to carry <em>every</em> file of <em>both</em> stores, which is exactly the
    /// pairing <see cref="TheSecondEngineIsTheOneProjectThatNamesAPlayRulesFile"/> makes on the
    /// source side.</para>
    ///
    /// <para>Read off the build output rather than off the csproj, because "the glob says so" and
    /// "the copy contains it" are different claims and only the second is the one a client depends
    /// on.</para>
    /// </summary>
    [Fact]
    public void TheEncounterServerIsTheOneCopyThatCarriesThePlayRules()
    {
        var beside = Path.Combine(Built(Path.Combine(RepoRoot, "mcp-play")), "data", "rules");

        Assert.True(Directory.Exists(beside),
            $"{beside} does not exist, so this check would pass without looking at anything. It is "
            + "written by a build; run one.");

        // The character rules, which the encounter server needs to turn a sheet into a combatant.
        Assert.True(
            Directory.GetFiles(beside, "*.json").Length >= 11,
            $"{beside} holds fewer character rules files than the engine loads. The encounter "
            + "server builds combatants out of them and cannot start without them.");

        var play = Directory.GetFiles(Path.Combine(beside, "play"), "*.json")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(PlayFileNames.Order(StringComparer.Ordinal).ToList(), play);
    }

    /// <summary>
    /// A sibling project's build output for this run's own configuration and framework, so a check
    /// cannot end up reading a stale directory left by some earlier build.
    /// </summary>
    private static string Built(string projectDirectory)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);   // …/bin/<cfg>/<tfm>/

        return Path.Combine(projectDirectory, "bin", here.Parent!.Name, here.Name);
    }

    /// <summary>
    /// <c>DataFileNames</c> is the contract a self-loading host fetches over HTTP. A play file on
    /// that list would be requested by every browser at boot, which is the same exposure by
    /// another route.
    /// </summary>
    [Fact]
    public void NoPlayRulesFileIsListedForASelfLoadingHost()
    {
        // Positive control: the list has to hold the character rules, or "no play file is on it"
        // would be satisfied by an empty list.
        Assert.True(RulesRepository.DataFileNames.Count >= 11, $"DataFileNames holds {RulesRepository.DataFileNames.Count} files.");

        foreach (var name in PlayFileNames) Assert.DoesNotContain(name, RulesRepository.DataFileNames);
    }

    /// <summary>
    /// The five source trees that make up the application, none of which may read a play file.
    ///
    /// <para><b><c>play/</c> is deliberately not on this list, and that is the change slice (d)
    /// made.</b> The second engine is the one project that may name these files — that is what it
    /// is for. What has not changed is which trees may not: the character engine, the renderers and
    /// all three hosts. A host reaches the play rules by holding a <c>PlayRulesRepository</c>, never
    /// by naming a file — <c>mcp-play/</c>, the one host there is, holds one and never names a play
    /// file in its own C#. Its <em>csproj</em> does, because a published server has to carry the
    /// data; that is what <see cref="TheEncounterServerIsTheOneCopyThatCarriesThePlayRules"/>
    /// pins.</para>
    /// </summary>
    private static readonly string[] ApplicationTrees =
        ["engine", "sheets", "cli", "web", "mcp", "mcp-shared"];

    /// <summary>The one tree that may — and, since it is the point of it, must.</summary>
    private const string SecondEngineTree = "play";

    private static readonly string[] SourceExtensions = ["*.cs", "*.razor", "*.csproj", "*.js", "*.json"];

    /// <summary>Every spelling of a play rules path a source file could reach one by.</summary>
    private static readonly string[] PlayFileTokens =
    [
            "play_meta.json", "challenge.json", "resolve.json", "combat.json", "gritty.json",
            "rules/play", @"rules\play"
        ];

    /// <summary>
    /// <b>Nothing in the application names a play rules file — and "the application" no longer
    /// means "everything".</b> <c>PlayRulesRepository</c> exists now, in <c>play/</c>, and reads all
    /// five; the claim this guard makes has narrowed from "nothing reads them" to "exactly one
    /// project does, and it is the second engine". The narrowing is the whole of slice (d), and it
    /// is why <see cref="TheSecondEngineIsTheOneProjectThatNamesAPlayRulesFile"/> sits beside this
    /// one: an allowance with nothing behind it would silently permit a whole tree.
    ///
    /// <para><b>What has not moved is the thing worth guarding.</b> A <c>PlayRulesRepository</c>
    /// wired into <c>engine/</c> compiles, passes, and quietly makes the character engine an
    /// authority on resolving an action — <c>CLAUDE.md</c> settles that it is not one — and a host
    /// that named a file rather than holding a repository would be a host with a rule of its own.
    /// So <c>engine/</c>, <c>sheets/</c>, <c>cli/</c>, <c>web/</c> and <c>mcp/</c> are all still
    /// forbidden, by exactly the scan they always were.</para>
    ///
    /// <para>This is the source-side companion to the payload checks above. Those say a play file
    /// cannot be <em>copied</em> anywhere; this says nothing in the application <em>names</em> one.
    /// A denylist of spellings cannot prove nobody reads the data — see <c>CLAUDE.md</c> — so what
    /// it is: cheap, and it catches the way it would actually happen.</para>
    /// </summary>
    [Fact]
    public void NothingInTheApplicationNamesAPlayRulesFile()
    {
        var faults = new List<string>();
        var scanned = 0;

        foreach (var tree in ApplicationTrees)
        {
            foreach (var file in SourceFilesUnder(Path.Combine(RepoRoot, tree)))
            {
                scanned++;

                var text = WithoutComments(File.ReadAllText(file));

                foreach (var token in PlayFileTokens)
                {
                    if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        faults.Add($"{Path.GetRelativePath(RepoRoot, file)} names '{token}'");
                    }
                }
            }
        }

        // Positive control, and it is the instrument rather than a formality: this project names
        // every token in the list, so a scan that found nothing here has stopped reading files and
        // would report the application clean whatever it contained. The count in the message is
        // read off the array — the comment said "all four" while there were five, which is the
        // kind of number that goes stale silently and reads as authority while it does.
        var self = SourceFilesUnder(Path.Combine(RepoRoot, "tests", "ProwlersAndParagonsAutomation.Tests"))
            .Select(File.ReadAllText)
            .ToList();

        foreach (var token in PlayFileTokens)
        {
            Assert.True(
                self.Exists(text => text.Contains(token, StringComparison.OrdinalIgnoreCase)),
                $"The scan found no file naming '{token}' in the test project, which names all "
                + $"{PlayFileTokens.Length} of them. It has stopped reading source; fix the scan, "
                + "not this assertion.");
        }

        Assert.True(scanned >= 50, $"Only {scanned} source files were read across {string.Join(", ", ApplicationTrees)}.");

        Assert.True(
            faults.Count == 0,
            "docs/guide/play-rules.md says nothing in the application reads data/rules/play/, and "
            + "these files name one: " + string.Join(", ", faults)
            + ". Play rules do not go into engine/ or any host — the simulator is a second engine "
            + "beside it, in a project of its own.");
    }

    /// <summary>
    /// <b>The allowance has something behind it.</b> <c>play/</c> is excused from the scan above
    /// because reading these five files is what it is for; an excuse for a tree that has stopped
    /// reading them — a repository deleted, a file list renamed away — would be a hole in the guard
    /// wearing the shape of a rule, and every other test here would stay green.
    ///
    /// <para>So the second engine is required to name <em>every</em> file, not merely one of them.
    /// A store where four of five files had quietly stopped being loaded is exactly the state
    /// <c>RulesFileCoverageTests</c> was written for on the character side.</para>
    /// </summary>
    [Fact]
    public void TheSecondEngineIsTheOneProjectThatNamesAPlayRulesFile()
    {
        var sources = SourceFilesUnder(Path.Combine(RepoRoot, SecondEngineTree)).ToList();

        Assert.True(sources.Count >= 5,
            $"only {sources.Count} source files found under {SecondEngineTree}/, so this control "
            + "would pass by measuring nothing.");

        var text = string.Concat(sources.Select(f => WithoutComments(File.ReadAllText(f))));

        foreach (var name in PlayFileNames)
        {
            Assert.True(text.Contains(name, StringComparison.Ordinal),
                $"{SecondEngineTree}/ does not name {name} outside a comment. It is excused from "
                + "the application scan because reading these files is its purpose; an excuse for a "
                + "project that has stopped reading them permits the tree for nothing.");
        }
    }

    private static IEnumerable<string> SourceFilesUnder(string directory) =>
        SourceExtensions
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// Blanks XML, line and block comments, for the reason <c>RulesIncludes</c> does: a file may
    /// legitimately explain why it does <em>not</em> reach the play rules, and a guard that cannot
    /// tell an explanation from a directive taxes the explanation.
    /// </summary>
    private static string WithoutComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, "<!--.*?-->|/\\*.*?\\*/", " ", RegexOptions.Singleline),
            "^\\s*//.*$",
            " ",
            RegexOptions.Multiline);
}
