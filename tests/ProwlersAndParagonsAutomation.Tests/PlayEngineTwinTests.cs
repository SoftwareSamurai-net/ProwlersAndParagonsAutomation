using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The deliberately broken twin: proof that the worked examples would notice.</b>
///
/// <para><b>Why a twin and not a denylist.</b> <c>CLAUDE.md</c> records the general form: a denylist
/// of spellings cannot make a verdict honest, because the spelling space is unbounded and a longer
/// list buys one more spelling and no more. What proves a harness can see a defect is running the
/// <em>byte-identical</em> harness against something that has one. So <see cref="PlayWorkedExamples"/>
/// is driven twice — once by <see cref="PlayEngineTests"/> against the shipped rules, and once here
/// against a copy with a single documented substitution in it.</para>
///
/// <para><b>The substitution is one line and it throws if that line has moved.</b>
/// <see cref="WithDefect"/> reads the shipped bytes, requires the line to occur exactly once, and
/// replaces it — zero occurrences would mean the twin had stopped reproducing anything and would
/// pass for the wrong reason, and more than one would mean it is not the single change it documents.
/// That is the <c>ProofPages.WithDefect</c> property, in C# and against a rules file.</para>
///
/// <para><b>The line is the special effect's rounding direction, and substituting it in the data is
/// the substitution.</b> The engine does not decide which way a half goes; it reads
/// <c>special_effects.interpretation.duration_rounds</c> and applies it, because that direction is
/// this project's reading of p.76 rather than something the page states. So flipping the file from
/// <c>up</c> to <c>down</c> is the same defect as writing the wrong rounding into the engine, and it
/// has a second virtue: an engine that had stopped consuming the interpretation and started assuming
/// a direction would leave the twin <em>green</em>, and this test would fail saying so.</para>
///
/// <para><b>A twin must say FAIL, not merely fail to say PASS.</b> Every example catches internally
/// and returns a verdict either way, so an example that died before reaching its assertion still
/// says <c>FAIL</c> with the reason — and this test additionally requires the failure message to
/// name the thing that moved, so a twin that broke for some unrelated reason cannot be read as a
/// working negative control.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayEngineTwinTests
{
    /// <summary>The one line the twin substitutes, as it is spelled in <c>combat.json</c>.</summary>
    private const string HealthyLine = "\"duration_rounds\": \"up\"";

    /// <summary>What it is substituted with: the same reading, the other way round.</summary>
    private const string DefectiveLine = "\"duration_rounds\": \"down\"";

    /// <summary>
    /// The examples the defect is expected to kill.
    ///
    /// <para><b>It is two rather than one, and that is a property of the page and not of the twin.</b>
    /// p.76 prints one exchange: Heartbreaker's Mind Control lasts three pages, and Parthian's escape
    /// removes two of those three. An effect that lasted two pages instead is not the effect the
    /// escape example is written about — the escape would end it outright — so both halves go red
    /// together. Naming both here, rather than letting the second one be a surprise, is what stops
    /// the twin's verdict from being read as "one example is sensitive to this and one is not".</para>
    /// </summary>
    private static readonly Dictionary<string, string> Expected = new(StringComparer.Ordinal)
    {
        // The duration itself: three pages become two.
        ["p.76 special effect"] = "lasts 2 pages, not the printed 3",

        // And the escape, which removes two pages, now removes the whole of a two-page effect and
        // frees Parthian outright — so there is no effect left to have one page on it.
        ["p.76 breaking free"] = "0 effects are running, not 1"
    };

    /// <summary>
    /// <b>The substitution is exactly one line, and it is really in the shipped file.</b> Run before
    /// anything else uses it: a <see cref="WithDefect"/> that matched nothing would build a twin
    /// identical to the real thing, and every example would pass under it for the wrong reason.
    /// </summary>
    [Fact]
    public void TheSubstitutionMatchesExactlyOneLine()
    {
        var shipped = File.ReadAllText(Path.Combine(PlayFixture.DataPath, PlayRulesRepository.CombatFile));

        Assert.Equal(1, Occurrences(shipped, HealthyLine));

        var broken = WithDefect(shipped, HealthyLine, DefectiveLine);

        Assert.NotEqual(shipped, broken);
        Assert.Equal(0, Occurrences(broken, HealthyLine));
        Assert.Equal(1, Occurrences(broken, DefectiveLine));

        // And the guard fires when the line is not there, which is the whole of its value. A
        // WithDefect that shrugged at a missing line is a twin that silently stops reproducing.
        Assert.Throws<InvalidOperationException>(() => WithDefect(shipped, "\"no_such_field\": 1", "x"));
        Assert.Throws<InvalidOperationException>(() => WithDefect(shipped, "\"source_ref\"", "x"));
    }

    /// <summary>
    /// <b>The two examples p.76 prints go red under the defect, and the other six stay green.</b>
    ///
    /// <para>The second half is as load-bearing as the first: a twin that turned everything red would
    /// prove only that the harness can fail, not that these examples can see <em>this</em> defect.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwinTurnsThePageSeventySixExamplesRedAndNothingElse()
    {
        var broken = BrokenRules();

        var verdicts = PlayWorkedExamples.Names
            .ToDictionary(name => name, name => PlayWorkedExamples.Run(name, broken), StringComparer.Ordinal);

        var failed = verdicts
            .Where(v => !string.Equals(v.Value, PlayWorkedExamples.Pass, StringComparison.Ordinal))
            .Select(v => v.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected.Keys.Order(StringComparer.Ordinal), failed);

        // A twin must SAY FAIL, and it must fail for the reason it claims. An example that died on
        // its way to the assertion would leave a verdict that is neither, and "not PASS" would read
        // that as a working negative control — so each verdict has to name the figure that moved.
        foreach (var (name, because) in Expected)
        {
            Assert.StartsWith("FAIL — ", verdicts[name], StringComparison.Ordinal);
            Assert.Contains(because, verdicts[name], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>The rest of the twin's rules are the shipped ones, byte for byte.</b>
    ///
    /// <para>Without this the "six stay green" half above could be true because the twin's other four
    /// files were subtly different, or because the harness had quietly fallen back to the real data
    /// — both of which would make the whole negative control a coincidence.</para>
    /// </summary>
    [Fact]
    public void OnlyOneOfTheFiveFilesDiffersInTheTwin()
    {
        var differing = PlayRulesRepository.DataFileNames
            .Where(name => !string.Equals(
                File.ReadAllText(Path.Combine(PlayFixture.DataPath, name)),
                TwinFiles()[name],
                StringComparison.Ordinal))
            .ToList();

        Assert.Equal([PlayRulesRepository.CombatFile], differing);
    }

    /// <summary>The five files a twin is built from: four shipped, one substituted.</summary>
    private static Dictionary<string, string> TwinFiles()
    {
        var files = PlayRulesRepository.DataFileNames.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(PlayFixture.DataPath, name)),
            StringComparer.Ordinal);

        files[PlayRulesRepository.CombatFile] =
            WithDefect(files[PlayRulesRepository.CombatFile], HealthyLine, DefectiveLine);

        return files;
    }

    private static PlayRulesRepository BrokenRules() => new(new InMemoryRulesSource(TwinFiles()));

    /// <summary>
    /// The shipped text with one documented line substituted, <b>throwing if that line does not occur
    /// exactly once</b>.
    ///
    /// <para>Zero occurrences means the twin has stopped reproducing its defect and would pass for
    /// the wrong reason; more than one means it is not the single change it documents. Both are
    /// silent failures otherwise, and both have happened to harnesses in this repository.</para>
    /// </summary>
    private static string WithDefect(string source, string find, string replace)
    {
        var count = Occurrences(source, find);

        if (count != 1)
        {
            throw new InvalidOperationException(
                $"The twin substitutes one documented line and found it {count} times: \"{find}\". "
                + "Zero means the twin has stopped reproducing its defect and would pass for the "
                + "wrong reason; more than one means it is not the single change it documents. Fix "
                + "the line this twin names, not this check.");
        }

        return source.Replace(find, replace, StringComparison.Ordinal);
    }

    private static int Occurrences(string source, string find)
    {
        var count = 0;
        var at = source.IndexOf(find, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = source.IndexOf(find, at + find.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
