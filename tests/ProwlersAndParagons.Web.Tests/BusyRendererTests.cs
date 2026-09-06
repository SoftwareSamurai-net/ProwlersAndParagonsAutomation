using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using static ProwlersAndParagons.Web.Tests.BusyRenderer;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The instrument's own positive controls.
///
/// <para><b>26 drives across three classes are only as good as this helper is</b>, and the way
/// an instrument fails is by quietly stopping instrumenting — which leaves every one of those
/// drives passing for the wrong reason and says nothing. So the two spellings that must fire it
/// are driven here rather than trusted.</para>
///
/// <para><b>This is also where the old spelling's failure would have been caught.</b> The hold
/// was a 250ms sleep and the control was <c>elapsed &gt; 125ms</c>, so both halves were readings
/// off a wall clock: on a loaded runner the control could go false with the product and the drive
/// both correct, and on a slow one the sleep could be over before the event was posted, so the
/// ordering was not driven and nothing said so. Neither is a thing a test can assert about
/// itself, which is why the fix was to take the clock out rather than to lengthen it.</para>
///
/// <para><b>The half that is not automated, said plainly.</b> <c>Occupying</c>'s other check —
/// that the renderer was still held when the drive was posted — cannot be reached while the hold
/// is a gate, because the only thing that opens the gate is the line below the check. It is there
/// for the edit that turns the hold back into a sleep, or deletes the wait, and it was watched to
/// fire by making exactly that edit. A test cannot watch it without a seam that exists only for
/// the test, and a seam like that is a second way for the instrument to be wrong.</para>
/// </summary>
public sealed class BusyRendererTests
{
    private static RenderContext Opened()
    {
        var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<Commands>().Open();
        return ctx;
    }

    /// <summary>
    /// A drive that posts the event and returns without waiting — the exact spelling
    /// <c>PaletteDispatchTests</c> bans from the three palette files — fires the control.
    ///
    /// <para><b>This is the specimen, so the banned spelling is deliberate here.</b> It is the
    /// only place in the project that may use it, and it is not one of the three files that scan
    /// forbids.</para>
    /// </summary>
    [Fact]
    public async Task ADriveThatIsNotAwaitedIsCaught()
    {
        using var ctx = Opened();
        var page = ctx.Render<CommandPalette>();

        var fired = await Assert.ThrowsAnyAsync<Exception>(() => Occupying(page, () =>
        {
            page.Find(".palette-box").Input(new ChangeEventArgs { Value = "plast" });
            return Task.CompletedTask;
        }, "the word typed into the box"));

        Assert.Contains("handled inline", fired.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// And so does a drive that dispatches nothing at all.
    ///
    /// <para>The direction that matters most: a lambda that has stopped driving anything —
    /// commented out, refactored through a helper that no longer posts — satisfies "the drive was
    /// awaited" perfectly, and would leave the test asserting on a render nothing caused.</para>
    /// </summary>
    [Fact]
    public async Task ADriveThatPostsNothingIsCaught()
    {
        using var ctx = Opened();
        var page = ctx.Render<CommandPalette>();

        var fired = await Assert.ThrowsAnyAsync<Exception>(
            () => Occupying(page, () => Task.CompletedTask, "a drive that posts nothing"));

        Assert.Contains("handled inline", fired.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The negative control: the awaited drive passes, and the render it caused is on screen by
    /// the time <c>Occupying</c> returns.
    ///
    /// <para>Without this the two above are satisfied by a helper that throws at everything.</para>
    /// </summary>
    [Fact]
    public async Task TheAwaitedDrivePassesAndItsRenderHasLanded()
    {
        using var ctx = Opened();
        var page = ctx.Render<CommandPalette>();

        await Occupying(
            page,
            () => page.Find(".palette-box").InputAsync(new ChangeEventArgs { Value = "plast" }),
            "the word typed into the box");

        Assert.Contains(
            "Plasticity",
            page.FindAll(".palette-row .palette-label").Select(e => e.TextContent));
    }
}
