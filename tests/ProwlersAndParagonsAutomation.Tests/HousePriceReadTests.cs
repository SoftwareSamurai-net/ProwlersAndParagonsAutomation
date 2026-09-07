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
    /// Calls that price the book rather than a character, by file, with how many and why. The
    /// count is part of the entry for the reason <c>TraitCapReadTests</c> records: an exemption
    /// that permitted a file outright would let a second, wrong call in beside a right one.
    /// </summary>
    private static readonly Dictionary<string, (int Calls, string Why)> Sanctioned =
        new(StringComparer.Ordinal)
        {
            // The declaration and the one place the parameter is forwarded from. `TotalPowersCost`
            // takes the sheet and passes `sheet.ImmortalityCost`, which is a call *with* the
            // argument and so never reaches this list.
            ["engine/CostCalculator.cs"] =
                (0, "the declaration itself carries no call; the forward in TotalPowersCost "
                    + "passes the sheet's price and is matched as qualified"),
        };

    private static IEnumerable<(string Path, string Line, int Number)> UnqualifiedCalls()
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
                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (Match call in Call.Matches(lines[i]))
                    {
                        if (call.Groups["args"].Value.Contains("ImmortalityCost", StringComparison.Ordinal))
                            continue;

                        yield return (
                            Path.GetRelativePath(RulesFixture.RepoRoot, file).Replace('\\', '/'),
                            lines[i].Trim(),
                            i + 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// No host prices a Power at the book's rate for a character whose table charges its own,
    /// unless the file is named above with the reason.
    /// </summary>
    [Fact]
    public void NoSurfacePricesAPowerWithoutTheTablesOwnPrice()
    {
        var found = UnqualifiedCalls().ToList();

        var offences = found
            .Where(c => !Sanctioned.TryGetValue(c.Path, out var entry) || entry.Calls == 0)
            .Select(c => $"{c.Path}:{c.Number}: {c.Line}")
            .ToList();

        Assert.True(offences.Count == 0,
            "These price a Power at the rulebook's rate for a character that may be at a table "
            + "charging its own — pass the sheet's ImmortalityCost, or name the file in "
            + $"{nameof(Sanctioned)} with the reason it is about the book:\n  "
            + string.Join("\n  ", offences));
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
            var root = Path.Combine(RulesFixture.RepoRoot, project);

            var hits = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal)
                             || f.EndsWith(".razor", StringComparison.Ordinal))
                            && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                           StringComparison.Ordinal)
                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                           StringComparison.Ordinal))
                .SelectMany(File.ReadAllLines)
                .Count(line => Call.Matches(line)
                    .Any(m => m.Groups["args"].Value.Contains("ImmortalityCost", StringComparison.Ordinal)));

            if (hits > 0) qualified.Add($"{project}={hits}");
        }

        Assert.True(qualified.Count == Scanned.Length,
            "Every one of the five projects prices a Power somewhere, so a project with no "
            + "table-priced call has either lost its pricing or the scan has stopped seeing it. "
            + $"Found: {string.Join(", ", qualified)}");
    }
}
