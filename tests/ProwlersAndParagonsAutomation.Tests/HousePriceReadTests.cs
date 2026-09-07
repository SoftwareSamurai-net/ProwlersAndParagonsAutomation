using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every surface that prices a Power for a character prices it at that character's table.</b>
///
/// <para>A table may charge more than the book for Immortality — Ch.2 p.31 hands the price to the
/// GM and names a range — so <c>CostCalculator.PowerCost(selection)</c> is the right answer to
/// "what does the rulebook charge" and the wrong answer to "what does this character pay". The two
/// are one argument apart, and the second argument is optional, so the wrong one compiles
/// silently.</para>
///
/// <para><b>This is <c>TraitCapReadTests</c>' shape, one field over, and for the same reason.</b>
/// The cap and the price are both facts about a table carried on a character, and both have
/// several readers that each print a figure somebody checks the character against. A budget strip
/// charging 9 above a Powers list charging 3 is how a screen and a verdict come to disagree, and
/// counting the readers in a doc comment has already been tried here and failed.</para>
///
/// <para><b>It is an allowlist of unqualified calls rather than a denylist of spellings.</b> Every
/// call in a project that puts a figure in front of somebody must pass a house price or be named
/// below with the reason it is about the book rather than about a character — so the way to smuggle
/// one in is not to pick a variable name this test has not heard of.</para>
///
/// <para><b>And the count in an entry is compared, which is the half that shipped missing.</b> The
/// first version tested <c>entry.Calls == 0</c> and otherwise permitted the file outright, so an
/// entry sanctioning one call exempted every call in that file — the exact failure its own comment
/// said the count existed to prevent, demonstrated by breaking both of
/// <c>CharacterSheetRenderer</c>'s calls under an entry declaring one and watching it stay green.
/// A stale entry was invisible too: a sanctioned file whose calls have gone permits anything
/// written there next, which is the failure this repository already records for an exemption whose
/// subject was renamed away. <c>Sanctioned</c> is empty today, and an empty list is a real state
/// rather than a missing one — nothing in the five projects prices the book for a character.</para>
///
/// <para><b>Comments are taken out before the scan</b>, for the reason <c>TraitCapReadTests</c>
/// takes them out: this file's own subject is discussed in prose all over the code it reads, and a
/// paragraph writing <c>costs.PowerCost(sp)</c> to explain why that spelling is wrong would
/// otherwise be reported as an offence. The stripping is per line, because the offence message
/// carries a line number and a whole-file replace would move it.</para>
///
/// <para><b>What it cannot do</b>, stated because <c>CLAUDE.md</c> requires it: it reads source
/// text, so a call routed through a local helper of somebody's own is invisible to it, and it has
/// no opinion at all about whether the price passed is the <em>right</em> character's — that is
/// what <c>ImmortalityHousePriceTests</c> drives through the engine and the pages. The catch here
/// is the cheap one: the shorter spelling, said no to in the same minute.</para>
/// </summary>
public sealed class HousePriceReadTests
{
    /// <summary>The projects that price a Power for somebody: the engine and its four hosts.</summary>
    private static readonly string[] Scanned = ["engine", "sheets", "web", "cli", "mcp"];

    /// <summary>
    /// A call to <c>PowerCost</c> and its argument list, as far as one balanced level of nesting —
    /// enough for <c>PowerCost(sp, sheet.ImmortalityCost)</c> and for the lambda-wrapped forms the
    /// browser uses.
    /// </summary>
    private static readonly Regex Call = new(
        @"\.PowerCost\((?<args>[^()]*(?:\([^()]*\)[^()]*)*)\)",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Calls that price the book rather than a character, by file, with how many and why.
    ///
    /// <para><b>The count is part of the entry and is compared</b>, so an exemption covers the
    /// calls it was written for and not the next one added beside them. An entry whose calls have
    /// gone is reported too, rather than silently permitting whatever is written there next.</para>
    ///
    /// <para><b>Empty today, and that is the honest state.</b> <c>CostCalculator</c> declares the
    /// method and forwards the sheet's price from <c>TotalPowersCost</c>, which is a call *with*
    /// the argument and never reaches this scan; every other call in the five projects passes the
    /// character's. A 0-count entry was here to say so and had to go — it permitted nothing and
    /// reported nothing, so it was a sentence pretending to be a guard, which is what this whole
    /// class exists because of.</para>
    /// </summary>
    private static readonly Dictionary<string, (int Calls, string Why)> Sanctioned =
        new(StringComparer.Ordinal);

    /// <summary>
    /// One line with its comments taken out. Per line, so the line number in an offence still
    /// points at the line the reader has to open.
    /// </summary>
    private static string WithoutComments(string line)
    {
        line = Regex.Replace(line, @"@\*.*?\*@", "", RegexOptions.None, TimeSpan.FromSeconds(5));

        var slashes = line.IndexOf("//", StringComparison.Ordinal);

        return slashes >= 0 ? line[..slashes] : line;
    }

    private static IEnumerable<(string Path, string Source)> ScannedSources()
    {
        foreach (var project in Scanned)
        {
            var root = Path.Combine(RulesFixture.RepoRoot, project);

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".cs", StringComparison.Ordinal)
                                     || f.EndsWith(".razor", StringComparison.Ordinal))
                         .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                                 StringComparison.Ordinal)
                                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                                    StringComparison.Ordinal)))
            {
                yield return (
                    Path.GetRelativePath(RulesFixture.RepoRoot, file).Replace('\\', '/'),
                    File.ReadAllText(file));
            }
        }
    }

    private static IEnumerable<(string Path, string Line, int Number)> UnqualifiedCalls()
    {
        foreach (var (path, source) in ScannedSources())
        {
            var lines = source.ReplaceLineEndings("\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match call in Call.Matches(WithoutComments(lines[i])))
                {
                    if (call.Groups["args"].Value.Contains("ImmortalityCost", StringComparison.Ordinal))
                        continue;

                    yield return (path, lines[i].Trim(), i + 1);
                }
            }
        }
    }

    /// <summary>
    /// No host prices a Power at the book's rate for a character whose table charges its own,
    /// unless the file is named above with the reason and the count that was sanctioned.
    /// </summary>
    [Fact]
    public void NoSurfacePricesAPowerWithoutTheTablesOwnPrice()
    {
        var found = UnqualifiedCalls().ToList();

        var byFile = found
            .GroupBy(c => c.Path, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var offences = new List<string>();

        foreach (var (file, calls) in byFile.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var where = string.Join("; ", calls.Select(c => $"line {c.Number}: {c.Line}"));

            if (!Sanctioned.TryGetValue(file, out var allowed))
            {
                offences.Add(
                    $"{file} prices a Power at the rulebook's rate {calls.Count} time(s) for a "
                    + "character that may be at a table charging its own — pass the sheet's "
                    + $"ImmortalityCost, or name the file in {nameof(Sanctioned)} with the count "
                    + $"and the reason it is about the book. {where}");

                continue;
            }

            if (calls.Count != allowed.Calls)
            {
                offences.Add(
                    $"{file} has {calls.Count} unqualified call(s); {allowed.Calls} are "
                    + $"sanctioned, for: {allowed.Why}. {where}");
            }
        }

        // An entry whose calls have gone permits its file for nothing and reports nothing — the
        // failure this repository already records for an exemption whose subject was renamed away.
        foreach (var (file, allowed) in Sanctioned.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (byFile.ContainsKey(file)) continue;

            offences.Add(
                $"{file} is sanctioned for {allowed.Calls} unqualified call(s) and has none. "
                + $"Delete the entry — it is now permitting anything written there. It was for: "
                + allowed.Why);
        }

        Assert.True(offences.Count == 0,
            "A Power's book price is what the rulebook charges; a character's is what its table "
            + "charges, and a house price makes them different numbers:"
            + Environment.NewLine + "  "
            + string.Join(Environment.NewLine + "  ", offences));
    }

    /// <summary>
    /// <b>The positive control: the scan finds the qualified calls too.</b>
    ///
    /// <para>A regular expression that has stopped matching <c>PowerCost</c> altogether reports no
    /// offences, which is a green test asserting nothing — the commonest way a guard in this
    /// repository has been wrong. So the same pattern is run without the argument filter and has
    /// to find a call in every project that has one.</para>
    /// </summary>
    [Fact]
    public void TheScanStillFindsThePricingItIsAbout()
    {
        var qualified = new List<string>();

        foreach (var project in Scanned)
        {
            var prefix = $"{project}/";

            var hits = ScannedSources()
                .Where(e => e.Path.StartsWith(prefix, StringComparison.Ordinal))
                .SelectMany(e => e.Source.ReplaceLineEndings("\n").Split('\n'))
                .Count(line => Call.Matches(WithoutComments(line))
                    .Any(m => m.Groups["args"].Value.Contains("ImmortalityCost", StringComparison.Ordinal)));

            if (hits > 0) qualified.Add($"{project}={hits}");
        }

        Assert.True(qualified.Count == Scanned.Length,
            "Every one of the five projects prices a Power somewhere, so a project with no "
            + "table-priced call has either lost its pricing or the scan has stopped seeing it. "
            + $"Found: {string.Join(", ", qualified)}");
    }
}
