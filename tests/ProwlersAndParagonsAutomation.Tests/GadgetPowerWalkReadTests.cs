using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// PROGRESS.md item 36's shape, checked the way <c>TraitCapReadTests</c> and
/// <c>HousePriceReadTests</c> check theirs: <c>CharacterValidator</c>'s per-Power checks used to
/// walk <see cref="CharacterSheet.SelectedPowers"/> directly, one clause at a time, and a Gadget's
/// own Powers were nowhere — three separate times (<c>EveryModifier</c>/<c>CheckModifiers</c>,
/// item 32; <c>CheckUnitNames</c> alone, PR #200; then four checks at once, item 36). The fix
/// folds five of them onto one shared enumeration, <c>EveryPaidPower</c>; this is the guard that
/// says a sixth cannot be written the old way without somebody noticing.
///
/// <para><b>An allowlist of methods, not a denylist of spellings</b> — <c>CLAUDE.md</c>'s own rule
/// for exactly this shape. Every remaining <c>foreach (var … in sheet.SelectedPowers …)</c> in
/// <c>engine/CharacterValidator.cs</c> has to be named below with the one-line reason a Gadget's
/// Powers are, or are not, legitimately out of scope where it sits — so the way to add a sixth
/// unguarded walk is not to spell it differently, it is to write down why.</para>
///
/// <para><b>Comments are stripped per line before the scan</b>, for the reason
/// <c>HousePriceReadTests</c> strips them: this file's own subject is discussed in prose right
/// beside the code it reads (<c>CheckQuantities</c>'s Gadget clause says, in a comment, that "the
/// clauses above walk <c>sheet.SelectedPowers</c> alone"), and a paragraph explaining the pattern
/// would otherwise be reported as an instance of it.</para>
///
/// <para><b>What it cannot do</b>, stated because <c>CLAUDE.md</c> requires it: it reads source
/// text at member indentation (four spaces), so a walk routed through a local helper, a local
/// function, or written across an unusual line break it does not anticipate is invisible to it —
/// and it has no opinion at all about whether an allow-listed method's reason is actually true,
/// only that one is written down. <c>GadgetPowerChecksTests</c> is what proves the five folded
/// checks actually cover a Gadget's Powers; this only proves nobody quietly stopped asking the
/// question a sixth way.</para>
/// </summary>
public sealed class GadgetPowerWalkReadTests
{
    private static readonly string FilePath =
        Path.Combine(RulesFixture.RepoRoot, "engine", "CharacterValidator.cs");

    /// <summary>A member declaration line: four-space indent, a visibility modifier, and a name before "(".</summary>
    private static readonly Regex MethodDeclaration = new(
        @"^ {4}(?:private|public|internal|protected|static)\b.*?(\w+)\s*\(",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>A direct walk: <c>foreach (var name in sheet.SelectedPowers</c>, whatever follows.</summary>
    private static readonly Regex ForEachSelectedPowers = new(
        @"foreach\s*\(\s*var\s+\w+\s+in\s+sheet\.SelectedPowers\b",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Methods allowed to still walk <c>sheet.SelectedPowers</c> directly, one line each on why a
    /// Gadget's Powers are, or are not, in scope there. <c>CLAUDE.md</c> says do not widen
    /// <c>CheckTraitCap</c>, <c>CheckQuantities</c>, <c>CheckModifiers</c> or <c>EveryModifier</c>
    /// in this pull request, so all four are named here rather than fixed.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["CheckTraitCap"] =
            "GetEffectiveRank(sp, sheet) resolves a rankless Power's baseline against the " +
            "character's own AbilityRanks/TalentRanks. A Gadget's Powers price against the " +
            "Gadget's own pool (BuiltGadget.AbilityRanks/TalentRanks), a different dictionary " +
            "entirely, so asking here would cap the wrong Traits rather than the right ones.",

        ["CheckQuantities"] =
            "Two of its three SelectedPowers-shaped clauses (negative purchased ranks, negative " +
            "units) already have a matching Gadget sweep a few lines below, in the same method, " +
            "over sheet.Gadgets. The third (PER_UNIT_WITHOUT_UNITS, a per-unit Power bought at " +
            "zero units) does not, which is a real, separate gap this pull request does not " +
            "fix — PROGRESS.md item 36 names four checks and this clause is not one of them.",

        ["CheckModifiers"] =
            "Already walks sheet.Gadgets directly, a few lines below, because it also has to " +
            "walk gear and Ability Pros/Cons in the same pass — which are not SelectedPowers at " +
            "all, so EveryPaidPower is the wrong shape for it.",

        ["EveryModifier"] =
            "Same reason as CheckModifiers, which it backs: already walks sheet.Gadgets " +
            "directly below for the same reason (gear and Ability modifiers beside it).",

        ["CheckPowerSelections"] =
            "The Gadget equivalent is GadgetIsPriceable, which walks gadget.Powers and calls " +
            "the same CheckPowerIsPriceable helper directly. Folding this into EveryPaidPower " +
            "too would report UNKNOWN_POWER and UNKNOWN_GADGET_POWER for the same Power twice.",

        ["EveryPaidPower"] =
            "This is the enumeration itself — walking sheet.SelectedPowers directly for the " +
            "character's own half is its whole job, which every other method above delegates to."
    };

    /// <summary>One line with its trailing <c>//</c> comment removed, so prose does not count as code.</summary>
    private static string WithoutTrailingComment(string line)
    {
        var slashes = line.IndexOf("//", StringComparison.Ordinal);
        return slashes >= 0 ? line[..slashes] : line;
    }

    private static (string[] Lines, List<(int Line, string Name)> Declarations) Read()
    {
        var raw   = File.ReadAllText(FilePath).ReplaceLineEndings("\n");
        var lines = raw.Split('\n').Select(WithoutTrailingComment).ToArray();

        var declarations = new List<(int Line, string Name)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = MethodDeclaration.Match(lines[i]);
            if (m.Success) declarations.Add((i, m.Groups[1].Value));
        }

        return (lines, declarations);
    }

    /// <summary>The nearest declaration at or before this line — "no enclosing method" if none.</summary>
    private static string MethodAt(int lineIndex, List<(int Line, string Name)> declarations)
    {
        for (var i = declarations.Count - 1; i >= 0; i--)
            if (declarations[i].Line <= lineIndex)
                return declarations[i].Name;

        return $"(line {lineIndex + 1}, no enclosing method found)";
    }

    [Fact]
    public void EveryDirectWalkOfSelectedPowersIsAllowListedWithAReason()
    {
        var (lines, declarations) = Read();

        var offences = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!ForEachSelectedPowers.IsMatch(lines[i])) continue;

            var method = MethodAt(i, declarations);

            if (!Allowed.ContainsKey(method))
                offences.Add($"line {i + 1}, in {method}: {lines[i].Trim()}");
        }

        Assert.True(offences.Count == 0,
            "A direct walk of sheet.SelectedPowers has to be folded into EveryPaidPower or named "
            + $"in {nameof(Allowed)} with the reason a Gadget's Powers are out of scope there:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offences));
    }

    /// <summary>
    /// <b>The two positive controls.</b> A scan that stopped matching the pattern, or stopped
    /// finding method declarations at all, would pass every offence vacuously — which is the
    /// single most common way a check in this repository has been wrong. So this asserts the
    /// shared enumeration is really in the file, and that the scan really does attribute a known
    /// direct walk (<c>CheckTraitCap</c>'s) to the right method.
    /// </summary>
    [Fact]
    public void TheScanFindsTheEnumerationAndAttributesAKnownSite()
    {
        var source = File.ReadAllText(FilePath);
        Assert.Contains("EveryPaidPower", source, StringComparison.Ordinal);

        var (lines, declarations) = Read();

        var attributed = new List<string>();
        for (var i = 0; i < lines.Length; i++)
            if (ForEachSelectedPowers.IsMatch(lines[i]))
                attributed.Add(MethodAt(i, declarations));

        Assert.Contains("CheckTraitCap", attributed);
    }
}
