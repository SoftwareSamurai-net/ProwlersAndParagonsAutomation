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
    internal static Regex Build(string pattern, RegexOptions options = RegexOptions.None)
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
