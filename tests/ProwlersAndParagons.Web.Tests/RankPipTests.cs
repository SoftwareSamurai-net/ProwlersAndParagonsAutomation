using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The pips, which are the control now rather than a picture of one.
///
/// <para>They were <c>aria-hidden</c> decoration beside a <c>+</c>/<c>−</c> stepper, so the only
/// way from 2d to 9d was seven clicks. Clicking the fifth pip sets 5d, the arrows move by one,
/// and Home and End go to the ends.</para>
/// </summary>
public sealed class RankPipTests
{
    private const int Max = 12;

    /// <summary>
    /// Renders one row and records what it asked for. <paramref name="floor"/> and
    /// <paramref name="package"/> are the two lower bounds the rules impose.
    /// </summary>
    private static (IRenderedComponent<RankRow> Row, List<int> Asked) Row(
        RenderContext ctx, int rank = 4, int floor = 1, int package = 0)
    {
        var asked = new List<int>();

        var row = ctx.Render<RankRow>(p => p
            .Add(x => x.Name, "Might")
            .Add(x => x.Rank, rank)
            .Add(x => x.Max, Max)
            .Add(x => x.Floor, floor)
            .Add(x => x.PackageFloor, package)
            .Add(x => x.RankChanged, asked.Add));

        return (row, asked);
    }

    /// <summary>
    /// It announces itself as a slider carrying the rank, and the bounds it announces are the
    /// bounds it enforces.
    ///
    /// <para><b>The minimum is the interesting one.</b> A package's granted ranks cannot be
    /// lowered below the package rank, so a packaged Trait's floor is not the Trait's own — and a
    /// slider announcing a minimum it will not actually go to is telling a screen-reader user
    /// something untrue about the control in front of them.</para>
    /// </summary>
    [Fact]
    public void ItAnnouncesItselfAsASliderWithTheRulesBounds()
    {
        using var ctx = new RenderContext();
        var (row, _) = Row(ctx, rank: 5, floor: 1, package: 3);

        var pips = row.Find(".pips");

        Assert.Equal("slider", pips.GetAttribute("role"));
        Assert.Equal("0", pips.GetAttribute("tabindex"));
        Assert.Equal("5", pips.GetAttribute("aria-valuenow"));
        Assert.Equal(Max.ToString(), pips.GetAttribute("aria-valuemax"));

        // The package floor, not the Trait floor of 1.
        Assert.Equal("3", pips.GetAttribute("aria-valuemin"));

        // The name is the Trait's, or eighteen sliders on one page are all called "rank".
        Assert.Contains("Might", pips.GetAttribute("aria-label")!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The announced value carries the rulebook's word, not the bare number.
    ///
    /// <para>A number alone reads as a quantity. The word is the rung a player is choosing
    /// between, and it is already on screen for everybody who can see the row.</para>
    /// </summary>
    [Fact]
    public void TheAnnouncedValueCarriesTheRulebooksWord()
    {
        using var ctx = new RenderContext();

        var asked = new List<int>();
        var row = ctx.Render<RankRow>(p => p
            .Add(x => x.Name, "Might")
            .Add(x => x.Rank, 4)
            .Add(x => x.Max, Max)
            .Add(x => x.Floor, 1)
            .Add(x => x.RankGuide, new Dictionary<string, string> { ["4d"] = "Noteworthy" })
            .Add(x => x.RankChanged, asked.Add));

        var text = row.Find(".pips").GetAttribute("aria-valuetext");

        Assert.Contains("4d", text!, StringComparison.Ordinal);
        Assert.Contains("Noteworthy", text!, StringComparison.Ordinal);
    }

    /// <summary>Clicking the fifth pip asks for 5d — the whole point of the change.</summary>
    [Fact]
    public void ClickingAPipAsksForThatRank()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 2);

        row.FindAll(".pip")[4].Click();

        Assert.Equal([5], asked);
    }

    /// <summary>
    /// Clicking below a package's granted rank is clamped up, not obeyed.
    ///
    /// <para>The ranks a package grants cannot be lowered below the package rank, and the pips
    /// draw those in their own colour — so they look like targets. Clicking one must land on the
    /// floor rather than on an illegal rank the validator would then report.</para>
    /// </summary>
    [Fact]
    public void ClickingUnderThePackageFloorClampsToIt()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 6, package: 3);

        row.FindAll(".pip")[0].Click();

        Assert.Equal([3], asked);
    }

    /// <summary>The arrows move by one, in both orientations.</summary>
    [Theory]
    [InlineData("ArrowRight", 5)]
    [InlineData("ArrowUp", 5)]
    [InlineData("ArrowLeft", 3)]
    [InlineData("ArrowDown", 3)]
    public void TheArrowsMoveByOne(string key, int expected)
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 4);

        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal([expected], asked);
    }

    /// <summary>Home and End go to the ends, and the ends are the rules' ends.</summary>
    [Fact]
    public void HomeAndEndGoToTheBounds()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 6, floor: 1, package: 3);

        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "Home" });
        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal([3, Max], asked);
    }

    /// <summary>
    /// The arrows stop at the bounds rather than asking for a rank outside them.
    ///
    /// <para>Clamping happens in one place, so this is really a check that the keys go through
    /// it — an implementation that added one and told the parent would push a Trait past the
    /// Trait Cap from the keyboard while the stepper's disabled button said it could not.</para>
    /// </summary>
    [Fact]
    public void TheArrowsDoNotLeaveTheBounds()
    {
        using var ctx = new RenderContext();

        var (top, atTop) = Row(ctx, rank: Max);
        top.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal([Max], atTop);

        var (bottom, atBottom) = Row(ctx, rank: 1, floor: 1);
        bottom.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal([1], atBottom);
    }

    /// <summary>
    /// A key the slider does not handle asks for nothing.
    ///
    /// <para>The positive control for the arrow tests: a handler that answered every key would
    /// satisfy all of them, and would fight the browser over Tab.</para>
    /// </summary>
    [Fact]
    public void AnUnhandledKeyChangesNothing()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx);

        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "Tab" });
        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Empty(asked);
    }

    /// <summary>
    /// <b>The row guards its slider against Home and End on first render, and only once.</b>
    ///
    /// <para>The keyboard test above, <c>HomeAndEndGoToTheBounds</c>, cannot see this at all —
    /// bUnit's synthetic <c>KeyDown</c> calls the Blazor handler directly and never touches a
    /// real <c>preventDefault</c>, so a version of <c>Key</c> that suppressed nothing would pass
    /// it identically. What is asserted here is the interop call that a browser harness in
    /// <c>ProofPages</c> then drives for real.</para>
    /// </summary>
    [Fact]
    public void TheSliderIsGuardedOnce()
    {
        using var ctx = new RenderContext();
        Row(ctx);

        Assert.Single(ctx.JSInterop.Invocations, i => i.Identifier == "ppSlider.guard");
    }

    /// <summary>
    /// The individual pips stay hidden from assistive technology.
    ///
    /// <para>They are the slider's own rendering. A reader told "4d Noteworthy, slider" does not
    /// also want twelve unlabelled children — and now that each one carries a click handler, the
    /// temptation to expose them is exactly what this refuses.</para>
    /// </summary>
    [Fact]
    public void ThePipsThemselvesAreNotAnnounced()
    {
        using var ctx = new RenderContext();
        var (row, _) = Row(ctx);

        Assert.All(row.FindAll(".pip"), pip =>
            Assert.Equal("true", pip.GetAttribute("aria-hidden")));
    }
}
