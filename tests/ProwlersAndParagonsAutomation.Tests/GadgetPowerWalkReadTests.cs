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
/// <para><b>An allowlist of methods is not enough on its own, and three adversarial reviews found
/// three separate ways through it — all three are closed here, each with its own control:</b></para>
///
/// <para>1. <b>The allowlist only vetted a method's name, never how many matches it had</b>, so a
/// second, unrelated <c>sheet.SelectedPowers</c> walk smuggled into an already-allowed method (say,
/// a Gadget-blind seventh check pasted inside <c>CheckModifiers</c>) passed silently — the entry
/// was true of the method once and treated as permanently true of it. Every entry below now
/// carries the exact number of direct walks it is allowed, and a count that has moved, in either
/// direction, is itself the offence: more says something new was added, fewer says a reason on
/// record no longer describes anything and needs removing.</para>
///
/// <para>2. <b>The scan matched one source line at a time</b>, so a plain reflow —
/// <c>foreach (var sp in</c> on one line, <c>sheet.SelectedPowers)</c> on the next — silently
/// disarmed it with no change in behaviour. The scan now runs over the whole stripped source as
/// one string, so a match spanning a line break is found exactly as one that does not.</para>
///
/// <para>3. <b>A check can call the shared <c>EveryPaidPower(sheet)</c> and filter its result
/// afterward</b> — <c>.Where(e => e.GadgetName is null)</c> — which reintroduces the exact
/// Gadget-blind bug this file exists to catch while never mentioning
/// <c>sheet.SelectedPowers</c> at all, so no allowlist of methods around that phrase could ever
/// see it. <see cref="NoDirectCallToEveryPaidPowerIsFilteredBeforeUse"/> is the separate, narrow
/// scan for that shape.</para>
///
/// <para><b>Comments are stripped before the scan</b>, for the reason <c>HousePriceReadTests</c>
/// strips them: this file's own subject is discussed in prose right beside the code it reads
/// (<c>CheckQuantities</c>'s Gadget clause says, in a comment, that "the clauses above walk
/// <c>sheet.SelectedPowers</c> alone"), and a paragraph explaining the pattern would otherwise be
/// reported as an instance of it.</para>
///
/// <para><b>What it still cannot do</b>, stated because <c>CLAUDE.md</c> requires it: it reads
/// source text, so a walk routed through a local helper, a local function, or spelled as
/// <c>sheet.SelectedPowers.Where(...)</c>/<c>.Select(...)</c> with no <c>foreach</c> keyword at
/// all is invisible to the direct-walk scan, and the filtered-call scan only recognises
/// <c>EveryPaidPower(sheet)</c> chained straight into <c>.Where(</c> — a filter applied after an
/// intervening <c>.Select</c>/<c>.OfType</c>, the shape <c>CheckUnverifiedPowers</c>'s own
/// legitimate <c>unverifiedText</c> query uses, reads as a different, allowed shape by design, but
/// a filter hidden a second call away in some other method would not be caught either.
/// <c>GadgetPowerChecksTests</c> is what proves the five folded checks actually cover a Gadget's
/// Powers; this only proves nobody quietly stopped asking the question the ways it already
/// knows to look for.</para>
/// </summary>
public sealed class GadgetPowerWalkReadTests
{
    private static readonly string FilePath =
        Path.Combine(RulesFixture.RepoRoot, "engine", "CharacterValidator.cs");

    /// <summary>A member declaration line: four-space indent, a visibility modifier, and a name before "(".</summary>
    private static readonly Regex MethodDeclaration = new(
        @"^ {4}(?:private|public|internal|protected|static)\b.*?(\w+)\s*\(",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// A direct walk: <c>foreach (var name in sheet.SelectedPowers</c>, whatever follows. Matched
    /// against the whole stripped source as one string (not line by line), so a walk whose
    /// <c>foreach (…)</c> is wrapped across two lines is found exactly as one that is not — <c>\s</c>
    /// matches the newline between them the same as it matches a space.
    /// </summary>
    private static readonly Regex ForEachSelectedPowers = new(
        @"foreach\s*\(\s*var\s+\w+\s+in\s+sheet\.SelectedPowers\b",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// <c>EveryPaidPower(sheet)</c> chained straight into <c>.Where(</c> — a filter applied to the
    /// shared walk's own result, which can silently drop the Gadget half again (e.g.
    /// <c>.Where(e => e.GadgetName is null)</c>) without the source ever mentioning
    /// <c>sheet.SelectedPowers</c>, so <see cref="ForEachSelectedPowers"/> has no chance of seeing
    /// it. Deliberately narrow: a filter separated from the call by an intervening <c>.Select</c>
    /// or <c>.OfType</c> — <c>CheckUnverifiedPowers</c>'s own <c>unverifiedText</c> query — is a
    /// different shape and is not what this catches; see the class doc comment.
    /// </summary>
    private static readonly Regex FilteredEveryPaidPower = new(
        @"EveryPaidPower\s*\(\s*sheet\s*\)\s*\.Where\s*\(",
        RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Methods allowed to still walk <c>sheet.SelectedPowers</c> directly, one line each on why a
    /// Gadget's Powers are, or are not, in scope there, and <b>the exact number of direct walks
    /// that reason covers today</b>. <c>CLAUDE.md</c> says do not widen <c>CheckTraitCap</c>,
    /// <c>CheckQuantities</c>, <c>CheckModifiers</c> or <c>EveryModifier</c> in this pull request,
    /// so all four are named here rather than fixed.
    ///
    /// <para><b>The count is not decoration.</b> An allowlist keyed on a method's name alone
    /// vets that the method may walk <c>sheet.SelectedPowers</c> at all, and has nothing to say
    /// about a second, unrelated walk added inside it later — which is indistinguishable from the
    /// one the reason was written about. Recording how many closes that: a mismatch in either
    /// direction is reported, by <see cref="EveryDirectWalkOfSelectedPowersIsAllowListedWithAReason"/>.</para>
    /// </summary>
    private static readonly Dictionary<string, (string Reason, int ExpectedMatches)> Allowed = new(StringComparer.Ordinal)
    {
        ["CheckTraitCap"] = (
            "GetEffectiveRank(sp, sheet) resolves a rankless Power's baseline against the " +
            "character's own AbilityRanks/TalentRanks. A Gadget's Powers price against the " +
            "Gadget's own pool (BuiltGadget.AbilityRanks/TalentRanks), a different dictionary " +
            "entirely, so asking here would cap the wrong Traits rather than the right ones.",
            1),

        ["CheckQuantities"] = (
            "Two of its three SelectedPowers-shaped clauses (negative purchased ranks, negative " +
            "units) already have a matching Gadget sweep a few lines below, in the same method, " +
            "over sheet.Gadgets. The third (PER_UNIT_WITHOUT_UNITS, a per-unit Power bought at " +
            "zero units) does not, which is a real, separate gap this pull request does not " +
            "fix — PROGRESS.md item 36 names four checks and this clause is not one of them.",
            3),

        ["CheckModifiers"] = (
            "Already walks sheet.Gadgets directly, a few lines below, because it also has to " +
            "walk gear and Ability Pros/Cons in the same pass — which are not SelectedPowers at " +
            "all, so EveryPaidPower is the wrong shape for it.",
            1),

        ["EveryModifier"] = (
            "Same reason as CheckModifiers, which it backs: already walks sheet.Gadgets " +
            "directly below for the same reason (gear and Ability modifiers beside it).",
            1),

        ["CheckPowerSelections"] = (
            "The Gadget equivalent is GadgetIsPriceable, which walks gadget.Powers and calls " +
            "the same CheckPowerIsPriceable helper directly. Folding this into EveryPaidPower " +
            "too would report UNKNOWN_POWER and UNKNOWN_GADGET_POWER for the same Power twice.",
            1),

        ["EveryPaidPower"] = (
            "This is the enumeration itself — walking sheet.SelectedPowers directly for the " +
            "character's own half is its whole job, which every other method above delegates to.",
            1)
    };

    /// <summary>One line with its trailing <c>//</c> comment removed, so prose does not count as code.</summary>
    private static string WithoutTrailingComment(string line)
    {
        var slashes = line.IndexOf("//", StringComparison.Ordinal);
        return slashes >= 0 ? line[..slashes] : line;
    }

    /// <summary>Comments stripped per line, newlines normalised, rejoined as one string.</summary>
    private static string Strip(string source) =>
        string.Join("\n", source.ReplaceLineEndings("\n").Split('\n').Select(WithoutTrailingComment));

    private static List<(int Line, string Name)> FindDeclarations(string strippedSource)
    {
        var lines = strippedSource.Split('\n');
        var declarations = new List<(int, string)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var m = MethodDeclaration.Match(lines[i]);
            if (m.Success) declarations.Add((i, m.Groups[1].Value));
        }

        return declarations;
    }

    /// <summary>The nearest declaration at or before this line — "no enclosing method" if none.</summary>
    private static string MethodAt(int lineIndex, List<(int Line, string Name)> declarations)
    {
        for (var i = declarations.Count - 1; i >= 0; i--)
            if (declarations[i].Line <= lineIndex)
                return declarations[i].Name;

        return $"(line {lineIndex + 1}, no enclosing method found)";
    }

    /// <summary>The zero-based line number a character offset into <paramref name="source"/> falls on.</summary>
    private static int LineAt(string source, int charIndex) =>
        source.AsSpan(0, charIndex).Count('\n');

    /// <summary>
    /// Every direct walk of <c>sheet.SelectedPowers</c> in the (already comment-stripped) source,
    /// grouped by enclosing method, each with the 1-based source lines it was found on. Shared by
    /// the real-file tests and the synthetic ones below, so both run the identical scan and only
    /// the input differs — the same discipline <c>WithDefect</c> uses elsewhere in this suite.
    /// </summary>
    private static Dictionary<string, List<int>> DirectWalksByMethod(string strippedSource)
    {
        var declarations = FindDeclarations(strippedSource);
        var byMethod = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (Match m in ForEachSelectedPowers.Matches(strippedSource))
        {
            var line = LineAt(strippedSource, m.Index);
            var method = MethodAt(line, declarations);
            if (!byMethod.TryGetValue(method, out var lines)) byMethod[method] = lines = [];
            lines.Add(line + 1);
        }

        return byMethod;
    }

    private static List<int> FilteredEveryPaidPowerCalls(string strippedSource) =>
        FilteredEveryPaidPower.Matches(strippedSource)
            .Select(m => LineAt(strippedSource, m.Index) + 1)
            .ToList();

    /// <summary>
    /// Every offence the two scans find in <paramref name="strippedSource"/>: an unlisted direct
    /// walk, an allow-listed method whose match count has moved away from what is on record (in
    /// either direction — more is a new walk, fewer is a stale reason), and any direct call to
    /// <c>EveryPaidPower(sheet)</c> filtered before use.
    /// </summary>
    private static List<string> FindOffences(string strippedSource)
    {
        var byMethod = DirectWalksByMethod(strippedSource);
        var offences = new List<string>();

        foreach (var (method, lines) in byMethod)
        {
            if (!Allowed.TryGetValue(method, out var entry))
            {
                offences.Add($"{method}: not allow-listed (lines {string.Join(", ", lines)})");
                continue;
            }

            if (lines.Count != entry.ExpectedMatches)
                offences.Add($"{method}: allow-listed for {entry.ExpectedMatches} direct walk(s) "
                    + $"but found {lines.Count} (lines {string.Join(", ", lines)})");
        }

        foreach (var (method, entry) in Allowed)
            if (entry.ExpectedMatches > 0 && !byMethod.ContainsKey(method))
                offences.Add($"{method}: allow-listed for {entry.ExpectedMatches} direct walk(s) "
                    + "but none were found in source — a stale entry");

        foreach (var line in FilteredEveryPaidPowerCalls(strippedSource))
            offences.Add($"line {line}: EveryPaidPower(sheet) is filtered with .Where(...) "
                + "directly after the call, which can silently drop the Gadget half again");

        return offences;
    }

    private static string ReadRealSource() => Strip(File.ReadAllText(FilePath));

    [Fact]
    public void EveryDirectWalkOfSelectedPowersIsAllowListedWithAReason()
    {
        // Reuses FindOffences but reports only the direct-walk half here, so a filtered-call
        // offence (NoDirectCallToEveryPaidPowerIsFilteredBeforeUse, below) is attributed to its
        // own test rather than doubled up in this one's failure message.
        var offences = FindOffences(ReadRealSource())
            .Where(o => !o.Contains("EveryPaidPower(sheet) is filtered", StringComparison.Ordinal))
            .ToList();

        Assert.True(offences.Count == 0,
            "A direct walk of sheet.SelectedPowers has to be folded into EveryPaidPower, named in "
            + $"{nameof(Allowed)} with the reason a Gadget's Powers are out of scope there, and "
            + "its recorded count kept equal to how many walks that reason actually covers:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offences));
    }

    [Fact]
    public void NoDirectCallToEveryPaidPowerIsFilteredBeforeUse()
    {
        var offences = FilteredEveryPaidPowerCalls(ReadRealSource());

        Assert.True(offences.Count == 0,
            "EveryPaidPower(sheet) is chained directly into .Where(...), which can silently drop "
            + "the Gadget half again exactly as filtering CharacterSheet.SelectedPowers alone "
            + "used to, at:" + Environment.NewLine + "  "
            + string.Join(Environment.NewLine + "  ", offences.Select(l => $"line {l}")));
    }

    /// <summary>
    /// <b>The positive controls.</b> A scan that stopped matching either pattern, or stopped
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

        var byMethod = DirectWalksByMethod(ReadRealSource());
        Assert.Contains("CheckTraitCap", byMethod.Keys);
    }

    // ── Mechanism proofs: the same scan, fed a synthetic broken twin ──────────
    //
    // Each of these is the "build the broken twin" discipline CLAUDE.md asks for, applied to the
    // scan itself rather than to the validator: the shipped source is expected to stay clean, so
    // proving these scans actually go red needs its own bad input, run through the identical
    // DirectWalksByMethod/FindOffences code the real-file tests use. Reproduced by hand against
    // the real file first (mutate, watch red, git checkout --) before being written here as a
    // permanent, non-destructive regression check.

    /// <summary>
    /// A second, unrelated <c>sheet.SelectedPowers</c> walk added inside an already allow-listed
    /// method used to be invisible — the allowlist only ever asked "is this method's name a key",
    /// never "how many". Two walks inside a method recorded for one is now the offence itself.
    /// </summary>
    [Fact]
    public void ASecondWalkInsideAnAlreadyAllowedMethodIsFlaggedAsACountMismatch()
    {
        const string snippet = """
                private bool CheckModifiers(CharacterSheet sheet, List<ValidationIssue> issues)
                {
                    foreach (var sp in sheet.SelectedPowers)
                    {
                        DoSomething(sp);
                    }

                    // A second, unrelated walk smuggled into the same, already-allowed method.
                    foreach (var sp in sheet.SelectedPowers)
                    {
                        DoSomethingElse(sp);
                    }
                }
            """;

        var offences = FindOffences(Strip(snippet));

        Assert.Contains(offences, o => o.StartsWith("CheckModifiers:", StringComparison.Ordinal)
            && o.Contains("found 2", StringComparison.Ordinal));
    }

    /// <summary>
    /// The mirror of the above: a method's <em>only</em> walk removed, with its allowlist entry
    /// left in place, is a reason on record that no longer describes anything.
    /// </summary>
    [Fact]
    public void AnAllowedMethodWithItsWalkRemovedIsFlaggedAsStale()
    {
        const string snippet = """
                private bool CheckModifiers(CharacterSheet sheet, List<ValidationIssue> issues)
                {
                    DoSomething(sheet);
                }
            """;

        var offences = FindOffences(Strip(snippet));

        Assert.Contains(offences, o => o.StartsWith("CheckModifiers:", StringComparison.Ordinal)
            && o.Contains("stale entry", StringComparison.Ordinal));
    }

    /// <summary>
    /// The line-scoped predecessor of this scan matched one source line at a time, so wrapping an
    /// otherwise-textbook, unlisted violation across two lines — a plain reflow, not an
    /// obfuscation attempt — made it invisible. Matching the whole stripped source as one string
    /// closes that: <c>\s</c> spans the newline the same as it spans a space.
    /// </summary>
    [Fact]
    public void AWalkWrappedAcrossTwoLinesIsStillFound()
    {
        const string snippet = """
                private bool CheckSomethingNew(CharacterSheet sheet, List<ValidationIssue> issues)
                {
                    foreach (var sp in
                        sheet.SelectedPowers)
                    {
                        DoSomething(sp);
                    }
                }
            """;

        var offences = FindOffences(Strip(snippet));

        Assert.Contains(offences, o => o.StartsWith("CheckSomethingNew:", StringComparison.Ordinal)
            && o.Contains("not allow-listed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Filtering <c>EveryPaidPower(sheet)</c>'s own result — e.g. dropping every entry whose
    /// <c>GadgetName</c> is not null — reintroduces the Gadget-blind bug this file exists to catch
    /// without ever writing <c>sheet.SelectedPowers</c>, so the direct-walk scan above cannot see
    /// it by construction. This is what <see cref="FilteredEveryPaidPower"/> is for.
    /// </summary>
    [Fact]
    public void AFilteredEveryPaidPowerCallIsFlagged()
    {
        const string snippet = """
                private void CheckSomethingNew(CharacterSheet sheet, List<ValidationIssue> issues)
                {
                    var onlyCharacterOwn = EveryPaidPower(sheet).Where(e => e.GadgetName is null);
                    foreach (var entry in onlyCharacterOwn)
                    {
                        DoSomething(entry);
                    }
                }
            """;

        var offences = FindOffences(Strip(snippet));

        Assert.Contains(offences, o => o.Contains("filtered with .Where(...)", StringComparison.Ordinal));
    }

    /// <summary>
    /// A filter that reaches <c>EveryPaidPower(sheet)</c>'s result only after an intervening
    /// <c>.Select</c>/<c>.OfType</c> — <c>CheckUnverifiedPowers</c>'s own <c>unverifiedText</c>
    /// query is exactly this shape — is not what <see cref="FilteredEveryPaidPower"/> catches, and
    /// this documents that rather than leaving it to be discovered as a surprise.
    /// </summary>
    [Fact]
    public void AFilterSeparatedByAnotherCallIsNotWhatThisCatches()
    {
        const string snippet = """
            var unverifiedText = EveryPaidPower(sheet)
                .Select(entry => Power(entry.Power.PowerId))
                .OfType<PowerModel>()
                .Where(p => !p.DescriptionVerified)
                .ToList();
            """;

        Assert.Empty(FilteredEveryPaidPowerCalls(Strip(snippet)));
    }
}
