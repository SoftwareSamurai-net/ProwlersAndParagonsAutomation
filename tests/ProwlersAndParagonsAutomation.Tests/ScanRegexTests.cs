using System.Text.RegularExpressions;

using ProwlersAndParagons.Testing;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <see cref="ScanRegex"/> really does hand out the linear engine.
/// </summary>
/// <remarks>
/// <para><b>Because a silent fallback would look exactly like a fix.</b> Every scanning pattern
/// in this project now goes through one factory that <em>tries</em>
/// <see cref="RegexOptions.NonBacktracking"/> and quietly drops to the backtracking engine when
/// the pattern refuses it. If that fallback swallowed everything — a typo in the option, a
/// framework change, a pattern rewritten to use a lookaround — every scan would be back on the
/// engine that timed one out at load, and nothing would say so. That is this repository's most
/// common guard fault in its own words: a feature that did not run mistaken for one that worked.
/// So these assert the engine, not just the answer.</para>
/// </remarks>
public sealed class ScanRegexTests
{
    /// <summary>The credential scan's own pattern, which is what the sighting was about.</summary>
    private const string CredentialPattern =
        @"(re_[A-Za-z0-9_]{16,})|(sk_live_[A-Za-z0-9]+)|([A-Za-z0-9_\-]{24,}\.[A-Za-z0-9_\-]{16,}\.[A-Za-z0-9_\-]{16,})";

    /// <summary>The two stylesheet scanners that cost ~640ms each under backtracking.</summary>
    private const string StylesheetPattern = @"([^{}]+)\{([^{}]*animation:\s*rise[^{}]*)\}";

    [Theory]
    [InlineData(CredentialPattern)]
    [InlineData(StylesheetPattern)]
    [InlineData(@"([^{}]+)\{([^{}]*)\}")]
    [InlineData(@"\b[a-z]+(_[a-z]+)+\b")]
    public void APatternWithoutALookaroundGetsTheLinearEngineAndNoDeadline(string pattern)
    {
        var rx = ScanRegex.Build(pattern);

        Assert.True(rx.Options.HasFlag(RegexOptions.NonBacktracking),
            "this pattern fell back to the backtracking engine, so its match time is no longer "
            + "linear in the input and a busy runner can time it out — which is the failure "
            + $"ScanRegex exists to remove. Pattern: {pattern}");

        Assert.Equal(Regex.InfiniteMatchTimeout, rx.MatchTimeout);
    }

    /// <summary>
    /// The options a caller asks for survive the trip through the factory.
    /// </summary>
    [Fact]
    public void TheRequestedOptionsAreKept()
    {
        var rx = ScanRegex.Build("^a.b$", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(rx.Options.HasFlag(RegexOptions.IgnoreCase));
        Assert.True(rx.Options.HasFlag(RegexOptions.Singleline));
        Assert.True(rx.Options.HasFlag(RegexOptions.NonBacktracking));
        Assert.Matches(rx, "A\nB");
    }

    /// <summary>
    /// <b>The fallback works and is reached.</b> Without this, the theory above could be passing
    /// because every pattern takes the linear path and the fallback is dead code that would throw
    /// the first time a lookaround reached it.
    /// </summary>
    [Fact]
    public void APatternNeedingALookaroundStillWorks()
    {
        var rx = ScanRegex.Build(@"(?<![\w-])--[a-z]+");

        Assert.False(rx.Options.HasFlag(RegexOptions.NonBacktracking),
            "a lookaround cannot run on the non-backtracking engine, so reaching this line means "
            + "the factory stopped falling back and the pattern is being matched by something "
            + "other than what it was written for.");

        // The cap on the fallback is a hang detector, deliberately far above the five seconds
        // that produced the sighting.
        Assert.Equal(ScanRegex.ScanTimeout, rx.MatchTimeout);
        Assert.True(rx.MatchTimeout >= TimeSpan.FromSeconds(30),
            "the fallback cap is back down in the range that turned a credential scan into a "
            + "flake on a loaded machine.");

        Assert.Matches(rx, "colour: --red");
        Assert.DoesNotMatch(rx, "x--red");
    }

    /// <summary>
    /// The blob the linearity control runs on: 192k of unbroken token characters, the shape a
    /// minified bundle or a base64 payload has, and the shape whose cost under backtracking is
    /// quadratic in its length.
    /// </summary>
    private static string PathologicalBlob => new('A', 192_000);

    /// <summary>
    /// The linear engine is not merely requested but actually pays off, on the input that
    /// produced the sighting — asserted <b>without a stopwatch on the passing side</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>This control used to be a wall-clock budget, and the budget was the flake class
    /// this whole change was sent to remove.</b> It timed <c>Build</c> plus one match of a 192k
    /// blob and required the pair under five seconds, on the stated grounds that the real cost is
    /// "well under a millisecond" and the headroom therefore "four orders of magnitude". Both
    /// figures were wrong, because <em>the timed region includes constructing the matcher</em>:
    /// building the non-backtracking matcher for this pattern dominates the match by roughly
    /// fifty to one. Measured on a ten-core machine, cold, in a fresh process: <b>23ms idle, 39ms
    /// under 30-way CPU load, and 528–756ms under the ~176 load average that produced the
    /// original sighting.</b> That last figure is a <b>6.6x</b> margin against the five-second
    /// cap, not four orders of magnitude — and the spinners it was measured against were bare
    /// userspace loops with no allocation, where a real runner also has GC and parallel xunit
    /// workers. A 6.6x wall-clock margin on the exact machine state this repository has already
    /// been bitten by is a flake, so the stopwatch is gone.</para>
    ///
    /// <para><b>What replaces it keeps the only clock on the side that must fail.</b> The
    /// positive control — that this blob really is the pathological shape, and so that the
    /// assertion below is about something — is that the <em>backtracking</em> twin of the same
    /// pattern cannot finish it inside a short cap. Runner load can only push that further into
    /// timing out, so load can never turn this green-to-red; the failure direction needs a
    /// machine 13x faster than the one measured (192k costs 13.3s backtracking here, against a
    /// 1s cap), and if that machine ever exists this goes <em>red</em> saying the control
    /// stopped controlling, which is the safe direction for a control to break in.</para>
    ///
    /// <para>The linear side then carries <see cref="Regex.InfiniteMatchTimeout"/> and no
    /// deadline of any kind, so no amount of load can produce a verdict from it. <b>It is
    /// deliberately not a second stopwatch</b>: what makes the answer trustworthy is that the
    /// engine really is the non-backtracking one, which is asserted here and, across every
    /// pattern, by the theory above. An engine that silently reverted would fail that assertion
    /// rather than this one.</para>
    ///
    /// <para>Cost: about a second of suite time, spent inside the cap on the twin. That is the
    /// price of a control that cannot flake, against a 19-second project.</para>
    /// </remarks>
    [Fact]
    public void TheCredentialPatternStaysLinearOnALongRunOfTokenCharacters()
    {
        var blob = PathologicalBlob;

        // [CONTROL] The blob really is pathological for this pattern. Without this, everything
        // below holds just as well against an input the backtracking engine would also breeze
        // through — which is this repository's most common guard fault, a check that passes
        // because the thing it is about never happened.
        var backtracking = new Regex(CredentialPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.Throws<RegexMatchTimeoutException>(() => backtracking.IsMatch(blob));

        var linear = ScanRegex.Build(CredentialPattern);

        Assert.True(linear.Options.HasFlag(RegexOptions.NonBacktracking),
            "the credential pattern is no longer on the linear engine, so the input above — which "
            + "the line before this proved the backtracking engine cannot finish in a second — is "
            + "now being scanned by that engine. This is the failure that timed out a credential "
            + "scan on a loaded machine before ScanRegex existed.");

        // No deadline at all on this one, so no amount of runner load can reach a verdict here.
        Assert.Equal(Regex.InfiniteMatchTimeout, linear.MatchTimeout);
        Assert.DoesNotMatch(linear, blob);
    }
}
