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
    /// The linear engine is not merely requested but actually pays off, on the input that
    /// produced the sighting.
    /// </summary>
    /// <remarks>
    /// <para><b>A wall-clock budget in a test is normally a flake waiting to happen, and the size
    /// of this one was chosen by watching it fail rather than by taste.</b> It is not measuring
    /// performance — it is a tripwire for the pattern going quadratic again. The linear engine
    /// scans this input in well under a millisecond; the backtracking engine needs about eleven
    /// seconds for it on an idle machine. Five seconds sits between the two with four orders of
    /// magnitude of headroom over the real cost, so no amount of runner load reaches it while a
    /// return to quadratic cannot miss it.</para>
    ///
    /// <para>An earlier draft used a 128k run against a fifteen-second budget and <em>passed
    /// with the pattern forced back onto the backtracking engine</em> — the mutation was caught
    /// only because the fallback was broken deliberately and this test was watched. That is the
    /// whole reason the figures are what they are.</para>
    /// </remarks>
    [Fact]
    public void TheCredentialPatternStaysLinearOnALongRunOfTokenCharacters()
    {
        // 192k of unbroken token characters: the shape a minified bundle or a base64 blob has.
        // The cost of this input under backtracking is quadratic in the length of the run.
        var blob = new string('A', 192_000);

        var started = DateTime.UtcNow;
        Assert.DoesNotMatch(ScanRegex.Build(CredentialPattern), blob);
        var took = DateTime.UtcNow - started;

        Assert.True(took < TimeSpan.FromSeconds(5),
            $"scanning 192k of token characters took {took.TotalSeconds:F1}s, against under a "
            + "millisecond on the linear engine. The credential pattern has stopped being "
            + "linear, which is what timed it out on a loaded machine before ScanRegex existed.");
    }
}
