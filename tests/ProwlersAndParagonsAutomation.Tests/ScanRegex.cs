using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ProwlersAndParagons.Testing;

/// <summary>
/// The one place a source-scanning test builds a <see cref="Regex"/>.
/// </summary>
/// <remarks>
/// <para><b>A wall-clock match timeout is the wrong guard for a scan, and this repository has
/// now been bitten by one.</b> <c>AccountsContractTests.NoKeyOrTokenIsInTheRepository</c> failed
/// with a <see cref="RegexMatchTimeoutException"/> during a full run on a ten-core machine whose
/// load average was ~176 — not because anything was wrong with the tree, but because a starved
/// thread turned ~130ms of matching into more than the five seconds the pattern allowed. A
/// timeout that fires turns a guard into a flake, and a flake is retried past; a scan that is
/// retried past is a scan that is not run.</para>
///
/// <para><b>The real problem was never the cap — it was that the match could be super-linear at
/// all.</b> Measured on this repository's own patterns and its own files: the credential scan's
/// token alternative is quadratic in the length of any unbroken run of token characters (64k of
/// it costs 1.9s idle, and 128k throws at the five-second cap with no CPU load whatsoever), and
/// <c>app.css</c>'s two <c>([^{}]+)\{…\}</c> scanners cost ~640ms each over the real
/// stylesheets. Under the load that produced the sighting, each of those is seconds.</para>
///
/// <para><b>So every scan is built <see cref="RegexOptions.NonBacktracking"/> where the pattern
/// allows it.</b> Match time becomes linear in the input, the two CSS scanners drop from ~640ms
/// to under 1ms with byte-identical results, and the credential scan drops from ~130ms to ~7ms.
/// With no runaway left to catch, the timeout is <see cref="Regex.InfiniteMatchTimeout"/>: the
/// only thing a cap could still do is convert a slow machine into a false verdict.</para>
///
/// <para><b>The fallback is narrow and cannot hide.</b> <see cref="RegexOptions.NonBacktracking"/>
/// refuses lookarounds, backreferences and atomic groups — by throwing at <em>construction</em>,
/// not by quietly going exponential — so a pattern needing one lands on the backtracking engine
/// with <see cref="ScanTimeout"/>. That cap is deliberately not five seconds: it exists to stop a
/// genuinely pathological pattern hanging a CI job forever, not to police normal work, so it is
/// set far above anything a healthy scan on a loaded runner could reach.</para>
///
/// <para>Consequence worth knowing when adding a pattern: <b>a lookaround silently costs the
/// linear guarantee.</b> Prefer a formulation without one where the choice exists.</para>
///
/// <para><b>And one semantic difference, because "byte-identical results" is true of everything
/// except this.</b> Both engines were run over every pattern this repository actually builds —
/// the 206 that take the linear path, against every source file under <c>web/</c>,
/// <c>worker/</c>, <c>functions/</c>, <c>docs/</c> and the rest, 61,594 pattern-by-file
/// comparisons — and they agree exactly on match count, match span, group success, group value,
/// group index and <see cref="Regex.Replace(string, string)"/> output. They disagree on one
/// thing: <b><see cref="Group.Captures"/> under <see cref="RegexOptions.NonBacktracking"/> holds
/// only the <em>final</em> capture of a quantified group</b>, where the backtracking engine
/// holds every one. <c>\b[a-z]+(_[a-z]+)+\b</c> over <c>one_two_three</c> is the whole of it:
/// <c>Groups[1].Value</c> is <c>"_three"</c> either way, but <c>Groups[1].Captures.Count</c> is
/// 2 under backtracking and 1 here.</para>
///
/// <para>Nothing in either test project reads <see cref="Group.Captures"/> today, which is why
/// the sweep onto this engine was safe. <c>ScanRegexTests</c> pins the difference anyway, so the
/// first caller to reach for it meets a test that says so rather than a quietly wrong answer.
/// <b>If you need every capture of a quantified group, this factory is the wrong way to build
/// the pattern</b> — match repeatedly, or ask for the backtracking engine deliberately.</para>
/// </remarks>
internal static class ScanRegex
{
    /// <summary>
    /// The last-resort cap for a pattern that cannot be made linear, and the reason it is not
    /// five seconds: at five seconds this repository lost a credential scan to a busy machine
    /// rather than to a bad tree. Nothing healthy here takes a minute — the whole 1.36MB
    /// credential corpus scans in ~7ms — so a pattern that reaches this is genuinely pathological
    /// and the exception is the correct report.
    /// </summary>
    internal static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// A scanning <see cref="Regex"/>: linear-time where the pattern permits it, and capped well
    /// clear of a loaded runner where it does not.
    /// </summary>
    internal static Regex Build(string pattern, RegexOptions options = RegexOptions.None) =>
        Built.GetOrAdd((pattern, options), static key => Construct(key.Pattern, key.Options));

    /// <summary>
    /// One <see cref="Regex"/> per distinct pattern and option set, for the life of the run.
    /// </summary>
    /// <remarks>
    /// <para><b>Construction is where a <see cref="RegexOptions.NonBacktracking"/> pattern spends
    /// its time, and the scans here build the same pattern inside loops.</b>
    /// <c>WebPresentationTests.NoPageNamesATypeThisProjectDeclares</c> built one regex per declared
    /// type per Razor file — files × types constructions of a pattern whose match takes
    /// microseconds — and was 33 seconds on its own, in a class xunit runs serially and which was
    /// therefore the whole engine suite's critical path. Measured: the class went from 74s to 7s
    /// and the suite's wall from 76s to 21s on a sixteen-core machine, with the same 5,451 results.</para>
    ///
    /// <para>Nothing about a verdict changes. A <see cref="Regex"/> is immutable and thread-safe,
    /// so two callers sharing one is the same as each holding its own; and
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey,TValue})"/> caches
    /// nothing when the factory throws, so a pattern the engine refuses still throws on every call
    /// exactly as it did.</para>
    /// </remarks>
    private static readonly ConcurrentDictionary<(string Pattern, RegexOptions Options), Regex> Built = new();

    private static Regex Construct(string pattern, RegexOptions options)
    {
        try
        {
            // `Compiled` is meaningless beside `NonBacktracking` and the pair is rejected
            // outright, so it is dropped for this attempt and kept for the fallback.
            var linear = (options & ~RegexOptions.Compiled) | RegexOptions.NonBacktracking;
            return new Regex(pattern, linear, Regex.InfiniteMatchTimeout);
        }
        catch (NotSupportedException)
        {
            // A lookaround, a backreference or an atomic group.
            return new Regex(pattern, options, ScanTimeout);
        }
        catch (ArgumentOutOfRangeException)
        {
            // An option `NonBacktracking` will not sit beside, such as `RightToLeft`.
            return new Regex(pattern, options, ScanTimeout);
        }
    }
}
