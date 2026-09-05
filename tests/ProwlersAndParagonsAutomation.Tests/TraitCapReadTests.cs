using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every surface that prints a character's Trait Cap reads it through
/// <c>DerivedStatsCalculator.EffectiveTraitCap</c>.</b>
///
/// <para>A character may be built to a <em>house</em> cap tighter than its tier's, and that cap is
/// the ceiling its ranks are judged against and the datum its Resolve is measured from. So
/// <c>tier.TraitCapRank</c> is the right answer to "what does this tier allow" and the wrong answer
/// to "what is this character built to" — and the two questions are one keystroke apart. Four
/// places in the terminal wizard were still asking the first and printing the answer as though it
/// were the second: the budget panel above every step, the Powers browser's rank prompt, and both
/// rank prompts on the Abilities and Talents step. A field bounded by one number and judged by
/// another is how a screen and a verdict come to disagree.</para>
///
/// <para><b>Counting places was tried in prose and does not work.</b> The doc comment on
/// <c>CharacterSheet.TraitCapRank</c> and <c>rules-engine.md</c> both said "six places" while there
/// were eight. A number in a sentence is not a guard, so this is one: every tier-shaped read of
/// <c>TraitCapRank</c> in a host project is named here with the reason it is allowed, and a new
/// one costs an entry rather than nothing.</para>
///
/// <para><b>It is an allowlist of receivers rather than a denylist of them</b>, which is the shape
/// this repository has already been bitten for getting the wrong way round. A read off
/// <c>sheet</c> or <c>campaign</c> is the character's own cap or the game's own setting and is
/// nobody's business here; <em>everything else</em> — <c>tier</c>, <c>t</c>, <c>Tier</c>, or
/// whatever a future variable is called — counts as a tier read and must be sanctioned. So the way
/// to smuggle one in is not to pick a name this test has not heard of.</para>
/// </summary>
public sealed class TraitCapReadTests
{
    /// <summary>The four projects that put a figure in front of somebody.</summary>
    private static readonly string[] Hosts = ["web", "cli", "mcp", "sheets"];

    /// <summary>
    /// The receivers that are demonstrably not a tier: the character being judged, and the
    /// campaign whose setting is copied onto one. Anything else is treated as a tier.
    /// </summary>
    private static readonly HashSet<string> NotATier =
        new(["sheet", "Sheet", "campaign", "Campaign"], StringComparer.Ordinal);

    /// <summary>
    /// Every tier-shaped read of the cap in a host project, by file, with how many and why.
    ///
    /// <para><b>The count is part of the entry on purpose.</b> An exemption that permitted a file
    /// outright would let a second, wrong read in beside a right one — which is the failure mode
    /// this repository records for exemptions whose subject moved.</para>
    /// </summary>
    private static readonly Dictionary<string, (int Reads, string Why)> Sanctioned =
        new(StringComparer.Ordinal)
        {
            // ── The tier catalogue: what each tier allows, for somebody choosing one ──────
            //
            // Not a figure about a character at all. These are the six cards, and a card that
            // printed some character's house cap would be describing the wrong thing entirely.

            ["cli/Steps/ChooseTierStep.cs"] =
                (2, "the tier catalogue — the six tiers a person is choosing between, each "
                    + "stating its own ceiling"),
            ["web/Pages/ChooseTier.razor"] =
                (1, "the same catalogue in the browser, on the cards"),
            ["mcp/CharacterTools.cs"] =
                (1, "list_options' tier catalogue, the same list over the protocol"),

            // ── The tier's own, printed beside the character's ───────────────────────────
            //
            // "Trait Cap 6d" at the Standard tier looks like a mistake to anybody who knows the
            // tier allows 12d, so these say both. The character's half of each pair is
            // EffectiveTraitCap; only the tier's half is read here.

            ["sheets/CharacterSheetRenderer.cs"] =
                (1, "the export's `tier` block, which is a fact about the tier — the character's "
                    + "own ceiling is the top-level `trait_cap` beside it"),
            ["cli/Headless/BuildCommand.cs"] =
                (1, "`tier_trait_cap` in the report, beside `trait_cap` — with one figure a "
                    + "caller cannot tell a specialist from a table's rule"),
            ["mcp/Judgement.cs"] =
                (1, "the same pair over the protocol"),
            ["web/Services/CharacterSession.cs"] =
                (2, "`TierTraitCap` and `TraitCapIsNotTheTiers`, which exist so a screen can name "
                    + "the tier's beside the one in force — `TraitCap` itself is EffectiveTraitCap"),
            ["web/Components/SheetView.razor"] =
                (2, "the printed sheet's meta line naming the tier's beside the effective cap"),
            ["web/Components/ReplayVerdict.razor"] =
                (2, "a replay's verdict doing the same"),
            ["web/Pages/Campaigns.razor"] =
                (2, "the GM's form checking the cap it was given against the tier the same form "
                    + "chose — a house cap tightens a tier's ceiling and never loosens it"),

            // ── Unwrapping the nullable, on a path that already has a tier ───────────────
            //
            // `EffectiveTraitCap` answers null only when the character has neither a house cap
            // nor a tier, which neither of these can reach. The read is the `??` and not a
            // second opinion.

            ["cli/Powers/PowerBrowser.cs"] =
                (1, "the `??` unwrapping EffectiveTraitCap's int? where the tier is already known"),
            ["cli/Steps/BuyCharacteristicsStep.cs"] =
                (1, "the same unwrap, for the Ability and Talent rank prompts"),
        };

    /// <summary>
    /// The wizard files that were reading the tier's cap and printing it as the character's.
    /// Each has to name <c>EffectiveTraitCap</c>, or the fix has been undone.
    /// </summary>
    private static readonly string[] MustAskTheEngine =
    [
        "cli/HpBudgetDisplay.cs",
        "cli/Powers/PowerBrowser.cs",
        "cli/Steps/BuyCharacteristicsStep.cs",
    ];

    /// <summary><c>tier.TraitCapRank</c>, <c>t?.TraitCapRank</c>, and anything else with a receiver.</summary>
    private static readonly Regex Read = new(
        @"(?<![A-Za-z0-9_])(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\s*\??\.TraitCapRank\b",
        RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void NoHostReadsATiersTraitCapForACharactersFigureWithoutSayingWhy()
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (relative, source) in HostSources())
        {
            var reads = Read.Matches(WithoutComments(source))
                .Count(m => !NotATier.Contains(m.Groups["receiver"].Value));

            if (reads > 0) found[relative] = reads;
        }

        var offenders = new List<string>();

        foreach (var (file, reads) in found.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!Sanctioned.TryGetValue(file, out var allowed))
            {
                offenders.Add(
                    $"{file} reads a tier's Trait Cap {reads} time(s) and is not listed. If that "
                    + "figure is about a character, it is DerivedStatsCalculator."
                    + "EffectiveTraitCap; if it really is about the tier, add it here with the "
                    + "reason.");
                continue;
            }

            if (reads != allowed.Reads)
            {
                offenders.Add(
                    $"{file} reads a tier's Trait Cap {reads} time(s); {allowed.Reads} are "
                    + $"sanctioned, for: {allowed.Why}");
            }
        }

        // An entry whose reads have gone permits its file for nothing and reports nothing — the
        // failure this repository already records for an exemption whose subject was renamed away.
        foreach (var (file, allowed) in Sanctioned.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!found.ContainsKey(file))
            {
                offenders.Add(
                    $"{file} is sanctioned for {allowed.Reads} tier read(s) and has none. Delete "
                    + $"the entry — it is now permitting anything written there. It was for: "
                    + allowed.Why);
            }
        }

        Assert.True(offenders.Count == 0,
            "A tier's Trait Cap is what the tier allows; a character's is what it is built to, "
            + "and a house cap makes them different numbers:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders));
    }

    /// <summary>
    /// <b>The positive control, and it is two halves.</b>
    ///
    /// <para>The scan above is an absence over a file list, so an empty file list satisfies it
    /// completely — and so would a repository in which nothing mentioned the cap at all. So the
    /// scan has to have read a real set of files and found real reads in them.</para>
    ///
    /// <para>And the three wizard files this was written for have to be asking the engine, not the
    /// tier. Without this the scan would go green the moment somebody put <c>tier.TraitCapRank</c>
    /// back and added a line to the list above, which is the cheapest possible way to satisfy
    /// it.</para>
    /// </summary>
    [Fact]
    public void TheScanIsLookingAtSomethingAndTheWizardAsksTheEngine()
    {
        var sources = HostSources().ToList();

        Assert.True(sources.Count > 50, $"only {sources.Count} host source files found");

        var withReads = sources.Count(e =>
            Read.Matches(WithoutComments(e.Source))
                .Any(m => !NotATier.Contains(m.Groups["receiver"].Value)));

        Assert.True(withReads == Sanctioned.Count,
            $"the scan found tier reads in {withReads} files and {Sanctioned.Count} are listed");

        foreach (var file in MustAskTheEngine)
        {
            var source = sources.SingleOrDefault(e => e.Relative == file).Source;

            Assert.True(source is not null, $"{file} is gone, so this control checks nothing");

            Assert.Contains("EffectiveTraitCap", source!, StringComparison.Ordinal);
        }
    }

    /// <summary>Every <c>.cs</c> and <c>.razor</c> file in the four host projects.</summary>
    private static IEnumerable<(string Relative, string Source)> HostSources()
    {
        var root = RepoRoot();

        foreach (var host in Hosts)
        {
            var directory = Path.Combine(root, host);

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(file) is not (".cs" or ".razor")) continue;

                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return (relative, File.ReadAllText(file));
            }
        }
    }

    /// <summary>
    /// The source with its comments taken out, in all three spellings this repository writes them.
    ///
    /// <para>Without this the guard would be a scan over prose: every paragraph explaining why a
    /// read is or is not a tier's mentions <c>tier.TraitCapRank</c> by name, and the entries above
    /// would be counting sentences.</para>
    /// </summary>
    private static string WithoutComments(string source)
    {
        source = Regex.Replace(source, @"@\*.*?\*@", "", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        return Regex.Replace(source, @"//.*", "", RegexOptions.None, TimeSpan.FromSeconds(5));
    }

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && here.GetFiles("*.sln").Length == 0) here = here.Parent;

        Assert.NotNull(here);
        return here!.FullName;
    }
}
