using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Holds the two end-to-end drivers to the rules that make them worth having.
///
/// <para>There are two: <c>scripts/e2e/drive.mjs</c>, the hand-rolled DevTools Protocol client that
/// runs in CI today, and <c>tests/e2e</c>, the Playwright one being built beside it.
/// <c>PROGRESS.md</c> item 10 carries the condition under which the first is retired. Until then
/// both exist, and two invariants that used to be one script's business are now a claim about the
/// pair — which is what this file is for.</para>
///
/// <para><b>What a source scan here can and cannot do, said plainly because <c>CLAUDE.md</c>
/// requires it.</b> A denylist of spellings cannot make a verdict honest: <c>MustNotShow</c> banned
/// the literal <c>say(true</c> and <c>|| true</c> walked straight through it. Nothing below is
/// offered as a guarantee. What a scan <em>is</em> good for is the lazy spelling — somebody
/// reaching for <c>localStorage.setItem</c> to arrange a state because it is quicker than clicking
/// — and it costs nothing. The guarantee is the deliberately-broken twin, which proves behaviour;
/// this is the cheap catch in front of it.</para>
/// </summary>
public sealed class E2eDriverTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;

    private static string NodeDriverPath =>
        Path.Combine(RepoRoot, "scripts", "e2e", "drive.mjs");

    private static string DefectsPath =>
        Path.Combine(RepoRoot, "scripts", "e2e", "defects.mjs");

    private static string PlaywrightDriverDirectory =>
        Path.Combine(RepoRoot, "tests", "e2e");

    private static List<string> PlaywrightSources() =>
        Directory.EnumerateFiles(PlaywrightDriverDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// A source file with its comments stripped.
    ///
    /// <para><b>Necessary, and the reason is the whole difficulty of scanning this repository.</b>
    /// Every rule below is <em>written down in a comment right beside the code that obeys it</em> —
    /// <c>Harness.cs</c>'s own summary says "no <c>localStorage.setItem</c> to arrange a state" — so
    /// a scan of the raw text finds the prohibition and reports it as the violation. That is not
    /// hypothetical; it is what the first version of this test did.</para>
    /// </summary>
    private static string CodeOnly(string path)
    {
        var text = File.ReadAllText(path);

        text = Regex.Replace(text, @"/\*.*?\*/", " ",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"^[ \t]*///.*$", " ",
            RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"^[ \t]*//.*$", " ",
            RegexOptions.Multiline, TimeSpan.FromSeconds(5));

        return text;
    }

    /// <summary>
    /// <b>The positive control, and every assertion below is an absence without it.</b> A scan
    /// pointed at a directory that has been renamed away finds no files, finds no violations, and
    /// reports the driver as perfectly compliant. That is this repository's oldest failure shape
    /// and it has shipped four times.
    /// </summary>
    [Fact]
    public void TheScanReallyReadsBothDrivers()
    {
        var sources = PlaywrightSources();

        Assert.True(
            sources.Count >= 4,
            $"Expected the Playwright driver to be several files; found {sources.Count} under "
            + $"{PlaywrightDriverDirectory}. If it has been moved or retired, fix this path — do "
            + "not lower the number, because every other assertion in this file is an absence and "
            + "an empty scan satisfies all of them.");

        Assert.True(File.Exists(NodeDriverPath),
            $"{NodeDriverPath} is gone. If the hand-rolled driver has been retired — see "
            + "PROGRESS.md item 10 for the condition — then the cross-driver assertions below need "
            + "rewriting rather than deleting.");

        // **And the comment stripper has to work in both directions, which is a sharper control
        // than "it left something behind".** `Harness.cs` states the prohibition in its own doc
        // comment — the literal `localStorage.setItem` is in the file — and the scan below would
        // report that prohibition as the violation. So: the raw text must contain it and the
        // stripped text must not. A stripper that ate the file passes the second half and fails
        // the third assertion; one that stripped nothing fails the second.
        var harness = Path.Combine(PlaywrightDriverDirectory, "Harness.cs");

        Assert.Contains("localStorage.setItem", File.ReadAllText(harness), StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage.setItem", CodeOnly(harness), StringComparison.Ordinal);
        Assert.Contains("namespace ProwlersAndParagons.E2e", CodeOnly(harness), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing reaches past the browser.</b>
    ///
    /// <para>The argument is <c>PROGRESS.md</c> item 10's: a feature shipped here while nothing in
    /// the application ever wrote to the store it read from, and every unit and component test
    /// passed <em>honestly</em>, because every one of them called the store directly. A test that
    /// reaches a feature by hand cannot notice that nothing else reaches it. So a check that
    /// arranges its own state has stopped answering the only question this harness exists to
    /// answer, and it does so while staying green.</para>
    ///
    /// <para>Reads are fine and are the point: asserting that the application wrote something is
    /// how the store defect would have been caught. It is the <em>writes</em> that are banned.</para>
    /// </summary>
    [Theory]
    [InlineData("localStorage.setItem", "arranges a state the application did not write")]
    [InlineData("sessionStorage.setItem", "arranges a state the application did not write")]
    [InlineData("localStorage.removeItem", "clears a state by hand instead of by using the app")]
    [InlineData("localStorage.clear", "clears a state by hand instead of by using the app")]
    public void NoDriverWritesToTheBrowsersStorage(string spelling, string why)
    {
        var offenders = new List<string>();

        foreach (var path in PlaywrightSources())
        {
            if (CodeOnly(path).Contains(spelling, StringComparison.Ordinal))
                offenders.Add(Path.GetFileName(path));
        }

        // The Node driver has no comments to strip in the same way, and its own header states the
        // rule in prose — so it is matched on the call shape rather than the bare name.
        if (Regex.IsMatch(File.ReadAllText(NodeDriverPath), Regex.Escape(spelling) + @"\s*\(",
                RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            offenders.Add("scripts/e2e/drive.mjs");
        }

        Assert.True(
            offenders.Count == 0,
            $"{spelling} appears in {string.Join(", ", offenders)}, which {why}. Every state a "
            + "check needs is arrived at by clicking what a person clicks; the only reads that go "
            + "round the front are the ones asserting on storage after the app has written it.");
    }

    /// <summary>
    /// <b>A click is a real mouse event, not one synthesised inside the page.</b>
    ///
    /// <para><c>el.click()</c> from an evaluated expression dispatches an untrusted event straight
    /// at one element: it does not scroll, does not wait for the element to settle, is not blocked
    /// by an overlay sitting on top, and would not notice a control that is unreachable by a
    /// pointer. It is the same mistake as reaching past the browser, one layer in.
    /// <c>ILocator.ClickAsync</c> and <c>cdp.mjs</c>'s <c>Input.dispatchMouseEvent</c> both put the
    /// event through the browser at real coordinates.</para>
    /// </summary>
    [Fact]
    public void NoDriverClicksFromInsideThePage()
    {
        var offenders = new List<string>();

        // `.click()` with no arguments, inside a C# string literal — which is the only place an
        // evaluated expression can live in this driver. `ClickAsync(` is a different token and is
        // not matched.
        var inEvaluatedString = new Regex(@"""[^""\r\n]*\.click\s*\(\s*\)",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var path in PlaywrightSources())
        {
            if (inEvaluatedString.IsMatch(CodeOnly(path))) offenders.Add(Path.GetFileName(path));
        }

        if (Regex.IsMatch(File.ReadAllText(NodeDriverPath), @"\.click\s*\(\s*\)",
                RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            offenders.Add("scripts/e2e/drive.mjs");
        }

        Assert.True(
            offenders.Count == 0,
            $"An element is clicked from inside the page in {string.Join(", ", offenders)}. Use "
            + "ILocator.ClickAsync (Playwright) or page.click (the Node driver), both of which "
            + "dispatch a real mouse event at real coordinates through the browser.");
    }

    /// <summary>
    /// <b>Every check either driver reports has a deliberately-broken twin, and every twin belongs
    /// to a check somebody drives.</b>
    ///
    /// <para><c>scripts/e2e.sh</c> already compares these two sets — but it compares them for
    /// <em>one</em> driver, the one it was told to run, and it can only do it after a full publish,
    /// a server and a browser. With two drivers in the tree that leaves a gap it cannot see: a
    /// check ported to one driver and not the other, or a twin added for a check only the retired
    /// driver knows about. Both are silent, and both mean a green run is quoting a smaller suite
    /// than the reader thinks.</para>
    ///
    /// <para>The union rather than the intersection, deliberately. During the port the Playwright
    /// driver names checks it has not implemented — see <c>NotYetPorted</c>, which is red rather
    /// than absent for this reason — so both drivers report the same five names today. What this
    /// forbids is a sixth name appearing anywhere without a negative control behind it.</para>
    /// </summary>
    [Fact]
    public void EveryCheckHasATwinAndEveryTwinHasACheck()
    {
        var driven = new SortedSet<string>(StringComparer.Ordinal);

        // The Node driver's list: `['BOOT', checkBoot],`
        foreach (Match m in Regex.Matches(File.ReadAllText(NodeDriverPath),
                     @"\['([A-Z][A-Z0-9_]*)',\s*check", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            driven.Add(m.Groups[1].Value);
        }

        var nodeCount = driven.Count;

        Assert.True(nodeCount >= 5,
            $"Read only {nodeCount} check names out of {NodeDriverPath}. Its CHECKS list has "
            + "changed shape; fix this extraction rather than the assertion, because an empty set "
            + "agrees with everything.");

        // The Playwright driver's list: `Boot.Check,` and `NotYetPorted.Check("BUILD"),`, plus the
        // name each check declares in `new("BOOT", Run)`.
        var program = CodeOnly(Path.Combine(PlaywrightDriverDirectory, "Program.cs"));

        foreach (Match m in Regex.Matches(program, @"NotYetPorted\.Check\(""([A-Z][A-Z0-9_]*)""\)",
                     RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            driven.Add(m.Groups[1].Value);
        }

        foreach (Match m in Regex.Matches(program, @"^\s*([A-Z][A-Za-z]*)\.Check,",
                     RegexOptions.Multiline, TimeSpan.FromSeconds(5)))
        {
            var name = CheckNameDeclaredBy(m.Groups[1].Value);

            Assert.True(name is not null,
                $"Program.cs lists {m.Groups[1].Value}.Check but no file under "
                + $"{PlaywrightDriverDirectory} declares the name it reports. A check whose name "
                + "cannot be read here cannot be matched against a twin.");

            driven.Add(name!);
        }

        Assert.True(driven.Count >= nodeCount,
            "The Playwright driver's check list could not be read; fix the extraction.");

        // The twins: `check: 'BOOT',`
        var twinned = new SortedSet<string>(
            Regex.Matches(File.ReadAllText(DefectsPath), @"check:\s*'([A-Z][A-Z0-9_]*)'",
                    RegexOptions.None, TimeSpan.FromSeconds(5))
                .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

        Assert.True(twinned.Count >= 5,
            $"Read only {twinned.Count} twinned check names out of {DefectsPath}.");

        var unproven = driven.Except(twinned, StringComparer.Ordinal).ToList();
        var orphaned = twinned.Except(driven, StringComparer.Ordinal).ToList();

        Assert.True(
            unproven.Count == 0,
            $"These checks are driven but have no deliberately-broken twin: "
            + $"{string.Join(", ", unproven)}. A check that has never been watched to fail is a "
            + "claim. Add an entry to scripts/e2e/defects.mjs, or remove the check.");

        Assert.True(
            orphaned.Count == 0,
            $"These twins name a check no driver runs: {string.Join(", ", orphaned)}. "
            + "scripts/e2e.sh would fail the run on this, after a publish, a server and a browser; "
            + "it costs a second here.");
    }

    /// <summary>
    /// The check name a <c>Checks/*.cs</c> class declares in its <c>new("NAME", Run)</c>.
    /// </summary>
    private static string? CheckNameDeclaredBy(string className)
    {
        var path = Path.Combine(PlaywrightDriverDirectory, "Checks", $"{className}.cs");

        if (!File.Exists(path)) return null;

        var m = Regex.Match(CodeOnly(path), @"new\(""([A-Z][A-Z0-9_]*)""",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        return m.Success ? m.Groups[1].Value : null;
    }
}
