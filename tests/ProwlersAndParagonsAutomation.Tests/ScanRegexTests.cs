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
    /// <summary>
    /// The credential scan's own pattern, which is what the sighting was about — <b>the real one,
    /// not a copy of it</b>.
    /// </summary>
    /// <remarks>
    /// This file used to hold its own transcription of the pattern. Two copies of the string that
    /// decides whether a committed key is found is exactly the drift this repository pins
    /// everywhere else: the scan could be edited into something quadratic again and every
    /// assertion here would go on passing, about a string nothing runs.
    /// </remarks>
    private const string CredentialPattern = AccountsContractTests.SuspiciousPattern;

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
    /// <b>Every construct the linear engine refuses lands on the fallback, rather than throwing
    /// out of <see cref="ScanRegex.Build"/>.</b>
    /// </summary>
    /// <remarks>
    /// <para><c>Build</c> catches two exception types, and that set is a claim about a framework
    /// it does not own: <see cref="NotSupportedException"/> for a construct the non-backtracking
    /// engine cannot express, and <see cref="ArgumentOutOfRangeException"/> for an option it will
    /// not sit beside. If .NET ever refuses one of these some third way — or refuses something
    /// new — the catch misses it, and a scanning test that has always passed dies at construction
    /// with an exception about regular expressions, a long way from anything a reader would
    /// connect to this factory.</para>
    ///
    /// <para>So this walks the refusals: the three the doc comment names, plus the conditional,
    /// the balancing group, <c>\G</c> and <see cref="RegexOptions.RightToLeft"/>, which it does
    /// not. Every one must come back as a working backtracking <see cref="Regex"/>.</para>
    ///
    /// <para><b>What it deliberately does not cover</b> is a pattern that is simply invalid —
    /// <c>a(b</c>, <c>a*+</c>, a backreference to a group that does not exist. Those throw
    /// <see cref="RegexParseException"/> from <em>both</em> engines, so they are not a fallback
    /// concern: they threw the same way before <c>ScanRegex</c> existed.</para>
    /// </remarks>
    [Theory]
    [InlineData("(?=a)b", RegexOptions.None, "a lookahead")]
    [InlineData("(?<=a)b", RegexOptions.None, "a lookbehind")]
    [InlineData(@"(a)\1", RegexOptions.None, "a backreference")]
    [InlineData(@"(?<q>a)\k<q>", RegexOptions.None, "a named backreference")]
    [InlineData("(?>a+)b", RegexOptions.None, "an atomic group")]
    [InlineData("(?(a)b|c)", RegexOptions.None, "a conditional")]
    [InlineData("(?<b>x)+(?<a-b>y)", RegexOptions.None, "a balancing group")]
    [InlineData(@"\Ga", RegexOptions.None, @"the \G anchor")]
    [InlineData("a", RegexOptions.RightToLeft, "RightToLeft")]
    public void AConstructTheLinearEngineRefusesFallsBackInsteadOfThrowing(
        string pattern, RegexOptions options, string construct)
    {
        var thrown = Record.Exception(() => ScanRegex.Build(pattern, options));

        Assert.True(thrown is null,
            $"Build threw a {thrown?.GetType().Name} on {construct}, instead of falling back to "
            + "the backtracking engine. Its catch clauses no longer cover everything the "
            + "non-backtracking engine refuses, so a scanning test that has always passed now "
            + $"dies at construction. Pattern: {pattern}. Message: {thrown?.Message}");

        var rx = ScanRegex.Build(pattern, options);

        Assert.False(rx.Options.HasFlag(RegexOptions.NonBacktracking),
            $"{construct} was accepted by the linear engine, so this row no longer exercises the "
            + "fallback at all and some other row is carrying it alone.");

        Assert.Equal(ScanRegex.ScanTimeout, rx.MatchTimeout);
    }

    /// <summary>
    /// <b>The one place the two engines disagree, pinned so that nobody meets it as a bug.</b>
    /// </summary>
    /// <remarks>
    /// <para>The sweep onto <see cref="RegexOptions.NonBacktracking"/> was checked by running
    /// both engines over every pattern this repository builds against every source file it
    /// scans, and they agree on match count, span, group success, group value, group index and
    /// <c>Replace</c> output — 61,594 pattern-by-file comparisons, no difference. The single
    /// exception is <see cref="Group.Captures"/>: the linear engine keeps only the last capture
    /// of a quantified group.</para>
    ///
    /// <para><b>Nothing reads <c>Captures</c> today</b>, which is exactly why this deserves a
    /// test rather than a note — an unreached difference is the kind that gets met as a wrong
    /// answer years later. If it starts failing because the engine gained full capture tracking,
    /// delete it and the paragraph in <see cref="ScanRegex"/> together; if a scanning test starts
    /// needing every capture, this is the assertion that says why it cannot have them from
    /// here.</para>
    /// </remarks>
    [Fact]
    public void TheLinearEngineKeepsOnlyTheLastCaptureOfAQuantifiedGroup()
    {
        const string pattern = @"\b[a-z]+(_[a-z]+)+\b";
        const string input = "one_two_three";

        var backtracking = new Regex(pattern, RegexOptions.None, ScanRegex.ScanTimeout)
            .Match(input).Groups[1];
        var linear = ScanRegex.Build(pattern).Match(input).Groups[1];

        // The part every caller in this repository actually reads is identical.
        Assert.Equal(backtracking.Value, linear.Value);
        Assert.Equal(backtracking.Index, linear.Index);

        // The part none of them reads is not.
        Assert.Equal(["_two", "_three"], backtracking.Captures.Select(c => c.Value));
        Assert.Equal(["_three"], linear.Captures.Select(c => c.Value));
    }

    /// <summary>
    /// The blob the linearity control runs on: 192k of alternating <c>A-</c>, the shape a base64url
    /// payload has, and the shape whose cost under backtracking is quadratic in its length.
    /// </summary>
    /// <remarks>
    /// <para><b>It is not a solid run of token characters, and the reason is worth keeping.</b>
    /// It was, until the credential pattern gained word-boundary anchors. A <c>\b</c> means the
    /// engine only starts a match attempt at a boundary, and a solid run of <c>A</c> has exactly
    /// one of those — so the very input the original sighting was reasoned about dropped from
    /// 13.7s to 23ms and stopped being pathological at all. This control went <em>red</em> saying
    /// so, which is the only reason anybody noticed.</para>
    ///
    /// <para><c>-</c> is inside the pattern's third character class and is not a word character,
    /// so <c>A-A-A-…</c> is a fresh boundary every second character: 192k of it costs 15.9s under
    /// backtracking, anchored or not. That is a realistic shape rather than a contrived one —
    /// base64url is full of <c>-</c> — and it is why the anchors are not a substitute for the
    /// linear engine.</para>
    /// </remarks>
    private static string PathologicalBlob =>
        string.Concat(Enumerable.Repeat("A-", 96_000));

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
