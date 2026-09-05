using System.Diagnostics;
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
/// <para><b>And the elapsed time is the positive control.</b> A drive handled inline comes back
/// in microseconds; one posted behind a busy renderer cannot come back until the renderer is
/// free. It fires for either way of losing this: the drive no longer being awaited, or the
/// occupation no longer occupying anything. Without it an instrument that has quietly stopped
/// working leaves the drive passing for the wrong reason, which is the failure shape
/// <c>CLAUDE.md</c> lists three of.</para>
///
/// <para><b>One copy, shared by all three classes, and that is deliberate.</b> The second test
/// that needed this was on the way to a hand-rolled second copy without the elapsed-time
/// control; a third would have been a third. It is generic over the component because the
/// drives that need it are on the palette, on <c>MainLayout</c>'s banner field, and on the
/// shell — the dispatcher is the context's either way.</para>
/// </summary>
internal static class BusyRenderer
{
    /// <summary>
    /// How long the renderer is held busy under a drive.
    ///
    /// <para><b>Long enough that the event cannot possibly be handled inline, and no longer.</b>
    /// The figure only has to be well clear of the microseconds an inline dispatch takes,
    /// because every drive that reads it asserts on a fraction of it rather than on the figure
    /// itself.</para>
    /// </summary>
    internal static readonly TimeSpan Occupation = TimeSpan.FromMilliseconds(250);

    /// <param name="page">The component whose dispatcher is the one held.</param>
    /// <param name="drive">The awaited event, or events, to post behind it.</param>
    /// <param name="what">What was driven, for the message when the control fires.</param>
    internal static async Task Occupying<TComponent>(
        IRenderedComponent<TComponent> page, Func<Task> drive, string what)
        where TComponent : IComponent
    {
        using var occupied = new ManualResetEventSlim();

        var busy = Task.Run(() => page.InvokeAsync(() =>
        {
            occupied.Set();
            Thread.Sleep(Occupation);
        }), Xunit.TestContext.Current.CancellationToken);

        occupied.Wait(Xunit.TestContext.Current.CancellationToken);

        var clock = Stopwatch.StartNew();

        await drive();

        Assert.True(clock.Elapsed > Occupation / 2,
            $"{what} came back in {clock.ElapsedMilliseconds}ms, so it was handled inline: either "
            + "it is not being awaited any more, or the renderer was not actually busy. Read "
            + "bUnit's event dispatch before deleting either half.");

        await busy;
    }
}
