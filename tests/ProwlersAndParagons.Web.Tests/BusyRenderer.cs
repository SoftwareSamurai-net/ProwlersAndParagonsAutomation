using Bunit;
using Microsoft.AspNetCore.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The instrument every dispatch trap in the palette's three test classes is proved with:
/// drive a bUnit event with the renderer deliberately busy, and say so when it was not.
///
/// <para><b>A bUnit event is <i>dispatched</i>, not applied.</b> The synchronous
/// <c>Click</c>, <c>KeyDown</c>, <c>Input</c>, <c>Change</c> and <c>Submit</c> post the event
/// and return without waiting whenever the renderer is not idle, so a read after one of them
/// can be a read from <em>before</em> it. While the renderer is idle the post runs inline and
/// the difference never shows — which is why such a drive passes on a quiet laptop and goes
/// red on a loaded runner. Holding the renderer busy makes that ordering the one every run
/// takes, so the failure is driven rather than waited for.</para>
///
/// <para><b>Held from another thread, because work posted from this one runs inline</b> while
/// the renderer is idle and would occupy nothing at all.</para>
///
/// <para><b>The hold is a gate this method opens, and it used to be a 250ms sleep.</b> That is
/// the whole of this file's second version, and both halves of the old spelling depended on a
/// wall clock:
/// <list type="bullet">
/// <item><description><b>Whether the drive really landed behind the hold</b> depended on
/// everything before the post fitting inside the sleep — and the post is not the first thing
/// a drive does. Most call sites here spell it
/// <c>() =&gt; page.Find(".palette-box").InputAsync(…)</c>, and that <c>Find</c> is inside the
/// timed window: measured at <b>178ms of the 250</b> the first time AngleSharp and the CSS
/// engine are touched in a process. Slower than that — a cold, loaded runner — and the
/// occupation was already over when the event was posted, so the losing order was not driven
/// at all and nothing said so.</description></item>
/// <item><description><b>Whether the positive control was true</b> depended on the test thread
/// getting back on a core promptly: the control read <c>elapsed &gt; 125ms</c>, and elapsed is
/// the sleep minus however long this thread took to post the drive after being woken. Stall it
/// for a fifth of a second — which is what a loaded runner does for free — and the control goes
/// false while the product and the drive are both perfectly correct. That is a test failing for
/// a fact about the machine, and it is the shape <c>CLAUDE.md</c> bans.</description></item>
/// </list>
/// A gate has neither reading in it. The renderer is held until <em>this method</em> opens it,
/// however long the drive takes to post and however busy the machine is; and the control is a
/// fact about the queue rather than a stopwatch reading.</para>
///
/// <para><b>And the control is still two claims, because there are still two ways to lose
/// this.</b> A drive that is no longer awaited comes back before the gate opens, and a hold that
/// has stopped holding lets the renderer go before the drive is posted — both are read below,
/// both while the renderer is still held, and neither by looking at a clock. Without them an
/// instrument that has quietly stopped working leaves the drive passing for the wrong reason,
/// which is the failure shape <c>CLAUDE.md</c> lists three of. <c>BusyRendererTests</c> watches
/// the first of them fire; the second cannot be reached while the hold is a gate — nothing but
/// the line below it opens one — so it is watched by editing this file rather than by a test,
/// and <c>BusyRendererTests</c> says so at length rather than leaving it looking covered.</para>
///
/// <para><b>One copy, shared by all three classes, and that is deliberate.</b> The second test
/// that needed this was on the way to a hand-rolled second copy without the positive control; a
/// third would have been a third. It is generic over the component because the drives that need
/// it are on the palette, on <c>MainLayout</c>'s banner field, and on the shell — the dispatcher
/// is the context's either way.</para>
/// </summary>
internal static class BusyRenderer
{
    /// <param name="page">The component whose dispatcher is the one held.</param>
    /// <param name="drive">The awaited event, or events, to post behind it.</param>
    /// <param name="what">What was driven, for the message when the control fires.</param>
    internal static async Task Occupying<TComponent>(
        IRenderedComponent<TComponent> page, Func<Task> drive, string what)
        where TComponent : IComponent
    {
        var stopping = Xunit.TestContext.Current.CancellationToken;

        using var occupied = new ManualResetEventSlim();
        using var opened = new ManualResetEventSlim();
        using var released = new ManualResetEventSlim();

        // The hold. It sits on the dispatcher doing nothing until the gate below is opened, so
        // the renderer is busy for exactly as long as this method needs it to be.
        var busy = Task.Run(() => page.InvokeAsync(() =>
        {
            occupied.Set();
            opened.Wait(stopping);
            released.Set();
        }), stopping);

        occupied.Wait(stopping);

        try
        {
            // Posted behind the hold: bUnit's `…Async` drives put the event on the dispatcher
            // before they hand back their task, so this is queued and cannot run yet.
            var driving = drive();

            // A drive that threw on its way out has a real failure to report, and reporting it
            // as one of the two below would bury it.
            if (driving.IsFaulted) await driving;

            Assert.False(released.IsSet,
                $"the renderer was free again before {what} was posted, so nothing was occupied "
                + "and the ordering this test exists to drive was not driven. The hold is no "
                + "longer holding. Read bUnit's event dispatch before deleting it.");

            Assert.False(driving.IsCompleted,
                $"{what} came back while the renderer was still held, so it was handled inline "
                + "rather than queued behind the hold: it is not being awaited any more. Read "
                + "bUnit's event dispatch before changing the drive.");

            opened.Set();

            await driving;
        }
        finally
        {
            // The gate opens whatever happened above, or the dispatcher stays held and the two
            // waits below it are disposed under a thread that is still inside one of them.
            opened.Set();
            await busy;
        }
    }
}
