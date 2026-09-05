using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The palette's three bUnit classes may not post an event and read the render it caused.
///
/// <para><b>A bUnit event is <i>dispatched</i>, not applied.</b> <c>element.Click()</c>,
/// <c>Input(…)</c>, <c>KeyDown(…)</c>, <c>Change(…)</c> and <c>Submit()</c> post the event and
/// return without waiting whenever the renderer is not idle; only the <c>…Async</c> forms come
/// back once the render it caused has finished. While the renderer is idle the post runs inline
/// and the difference never shows — which is why the synchronous form works almost everywhere in
/// this project, and why the palette is the exception: the book's answer lands on a thread-pool
/// continuation and the redraw it raises is queued through <c>InvokeAsync</c>, so the renderer is
/// busy after every answer.</para>
///
/// <para><b>Three tests in <c>PaletteBookTests</c> went red on ubuntu-latest for that one reason
/// before this file existed</b> — an arrow press reading <c>aria-selected="false"</c>, a row click
/// reading a search request that had not been made, and an Escape reading the palette still on
/// screen. Each was fixed on its own and the next one arrived. The fourth is what this stops.</para>
///
/// <para><b>What a denylist of spellings cannot do, said plainly because <c>CLAUDE.md</c> requires
/// it.</b> This is a scan for five literal spellings, and the spelling space is not bounded by
/// them: a drive routed through a local helper, an extension method of somebody's own, or
/// <c>TriggerEvent("onclick", …)</c> walks straight through it, and so does any future bUnit
/// synchronous trigger nobody adds here. It cannot see the actual defect either — a read that is
/// one render early is a fact about ordering, and no regular expression over source text has an
/// opinion about ordering. Nothing below is offered as a guarantee.
///
/// <para>What it <em>is</em> good for is the lazy spelling: somebody adding a test to one of these
/// three files reaches for <c>Find(".palette-box").Input("…")</c> because it is shorter, and this
/// says no in the same minute rather than on a loaded runner three weeks later. The thing that
/// actually proves the ordering is <c>BusyRenderer.Occupying</c>, which holds the renderer busy
/// from another thread so the losing order is driven every run and carries an elapsed-time
/// positive control saying it really was busy. That is the guarantee; this is the cheap catch in
/// front of it, and it is asserted below that every one of the three files still uses it.</para>
/// </para>
/// </summary>
public sealed class PaletteDispatchTests
{
    /// <summary>
    /// The three classes that drive the palette surface: the book's rows, the banner's field, and
    /// the palette's own keys.
    /// </summary>
    private static readonly string[] Scanned =
        ["PaletteBookTests.cs", "BannerTests.cs", "CommandPaletteTests.cs"];

    private static string PathOf(string file) =>
        Path.Combine(RulesFixture.RepoRoot, "tests", "ProwlersAndParagons.Web.Tests", file);

    /// <summary>
    /// The synchronous drives. Anchored on the dot and the open bracket, so <c>InputAsync(</c>
    /// and <c>ClickAsync(</c> — the spellings that are wanted — cannot match.
    /// </summary>
    private static readonly Regex Posted =
        new(@"\.(?:Input|Click|KeyDown|Change|Submit)\(", RegexOptions.Compiled);

    /// <summary>The awaited spellings, for the control that says these files still drive anything.</summary>
    private static readonly Regex Awaited =
        new(@"\.(?:Input|Click|KeyDown|Change|Submit)Async\(", RegexOptions.Compiled);

    /// <summary>
    /// No synchronous drive in any of the three, named by file and line when there is one.
    /// </summary>
    [Fact]
    public void NoPaletteTestPostsAnEventItThenReadsTheRenderOf()
    {
        var offences = new List<string>();

        foreach (var file in Scanned)
        {
            var lines = File.ReadAllLines(PathOf(file));

            for (var i = 0; i < lines.Length; i++)
            {
                if (Posted.IsMatch(lines[i]))
                    offences.Add($"{file}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offences.Count == 0,
            "A bUnit event is dispatched, not applied: the synchronous form posts it and returns "
            + "whenever the renderer is not idle, so the next line can read the render from before "
            + "it. Use the `…Async` form, and where the read *is* the assertion drive it under "
            + "`BusyRenderer.Occupying` so the losing order is taken every run. Found:\n"
            + string.Join("\n", offences));
    }

    /// <summary>
    /// The positive control on the scan above, and it is not optional.
    ///
    /// <para><b>"No synchronous drive" is satisfied completely by three files that drive nothing at
    /// all</b> — renamed, moved, gutted, or a regex that has stopped matching. So each file is
    /// asserted to contain the awaited spelling, and the pattern is asserted to fire on a line
    /// known to be bad.</para>
    /// </summary>
    [Fact]
    public void TheScanFiresAndTheFilesStillDriveEvents()
    {
        Assert.True(Posted.IsMatch("""page.Find(".palette-box").Input("knockback");"""),
            "the pattern no longer matches a synchronous drive, so the guard above passes "
            + "vacuously.");

        Assert.False(Posted.IsMatch("""await page.Find(".palette-box").InputAsync(args);"""),
            "the pattern matches the awaited form, so the guard above bans the fix.");

        foreach (var file in Scanned)
        {
            var source = File.ReadAllText(PathOf(file));

            Assert.True(Awaited.IsMatch(source),
                $"{file} drives no bUnit event at all any more, so the guard says nothing about "
                + "it. If the drives really have gone, take it off the scanned list deliberately.");

            // And it still reaches for the instrument that proves the ordering, rather than
            // relying on this scan — which cannot see ordering at all.
            Assert.Contains("Occupying(", source, StringComparison.Ordinal);
        }
    }
}
