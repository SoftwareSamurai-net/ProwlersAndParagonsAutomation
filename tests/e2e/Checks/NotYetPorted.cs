namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// A check this driver names but has not ported yet. It always reports FAIL.
///
/// <para><b>Why a placeholder exists at all, rather than a shorter list.</b>
/// <c>scripts/e2e.sh</c> compares the set of checks a driver reported against the set of checks
/// <c>scripts/e2e/defects.mjs</c> twins, and fails if they disagree in either direction. A driver
/// part-way through a migration would otherwise have to either shrink the twin list — deleting a
/// negative control that still guards the driver actually running in CI — or grow the driven list
/// with checks that are silently absent. Both are the failure shape this repository keeps having
/// to fix. So an unported check is present, named, and <em>red</em>.</para>
///
/// <para><b>Red rather than skipped, and that is the load-bearing half.</b> A skip prints no
/// verdict, and <c>e2e.sh</c> reads a missing verdict as a twin that proved nothing — which is
/// correct, but it reports it as a harness that fell over rather than as work that is not done.
/// This says which it is, in the line, every run.</para>
///
/// <para>Delete an entry here in the same change that ports its check. When this file is empty it
/// goes with it.</para>
/// </summary>
public static class NotYetPorted
{
    public static Check Check(string name) => new(name, _ =>
        throw new CheckFailedException(
            $"{name} has not been ported to the Playwright driver yet. It is still driven by "
            + "scripts/e2e/drive.mjs, which is the harness CI runs; this one is being built beside "
            + "it. See PROGRESS.md item 10."));
}
