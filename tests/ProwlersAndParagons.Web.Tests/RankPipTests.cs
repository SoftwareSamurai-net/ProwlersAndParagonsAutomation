using System.Globalization;
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
        RenderContext ctx, int rank = 4, int floor = 1, int package = 0, int max = Max)
    {
        var asked = new List<int>();

        var row = ctx.Render<RankRow>(p => p
            .Add(x => x.Name, "Might")
            .Add(x => x.Rank, rank)
            .Add(x => x.Max, max)
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
    /// <b>A house Trait Cap below a package's granted rank does not take the step down with
    /// it.</b>
    ///
    /// <para>A campaign's cap is a number the GM types — the form takes 1 to 30 — and a table
    /// capped at 2d with a character on the 3d package leaves this row with a minimum above its
    /// maximum. <c>Math.Clamp</c> throws <c>ArgumentException</c> on exactly that, so the first
    /// click on any pip used to take out the whole Abilities step; the character is one the
    /// validator already has findings about, and a row that throws reports none of them.</para>
    ///
    /// <para>The row is bounded by the floor instead, so the click lands on a legal rank. The
    /// positive control is that a rank really was asked for: an implementation that swallowed the
    /// click would satisfy "no exception" perfectly.</para>
    /// </summary>
    [Fact]
    public void ACapBelowThePackageFloorDoesNotThrow()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 3, floor: 1, package: 3, max: 2);

        var drawn = row.FindAll(".pip").Count;

        Assert.NotEqual(0, drawn);

        // Re-found each time: the click re-renders the row, and a handler read off the previous
        // pass is a handler the renderer no longer knows.
        for (var i = 0; i < drawn; i++)
        {
            row.FindAll(".pip")[i].Click();
        }

        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "End" });
        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "Home" });

        Assert.NotEmpty(asked);
        Assert.All(asked, rank => Assert.Equal(3, rank));
    }

    /// <summary>
    /// <b>A rank above the cap in force is announced inside the slider's own range.</b>
    ///
    /// <para>A character built to 8d whose table then caps at 6d drew six pips and announced
    /// <c>aria-valuenow="8"</c> against <c>aria-valuemax="6"</c> — a value outside the control's
    /// declared range, which is the fault the budget strip already records for its
    /// <c>progressbar</c>. Nothing is repaired: the rank stays where the sheet has it and the
    /// validator's <c>TRAIT_ABOVE_CAP</c> finding under the row is what says it is wrong.</para>
    /// </summary>
    [Fact]
    public void ARankAboveTheCapIsStillInsideTheAnnouncedRange()
    {
        using var ctx = new RenderContext();
        var (row, asked) = Row(ctx, rank: 8, floor: 1, max: 6);

        var pips = row.Find(".pips");

        var now = int.Parse(pips.GetAttribute("aria-valuenow")!, CultureInfo.InvariantCulture);
        var most = int.Parse(pips.GetAttribute("aria-valuemax")!, CultureInfo.InvariantCulture);
        var least = int.Parse(pips.GetAttribute("aria-valuemin")!, CultureInfo.InvariantCulture);

        Assert.Equal(8, now);
        Assert.True(now <= most, $"aria-valuenow {now} is above aria-valuemax {most}");
        Assert.True(least <= now, $"aria-valuenow {now} is below aria-valuemin {least}");

        // Every rank the row draws is reachable, which is what "the bounds it announces are the
        // bounds it enforces" means — eight pips over a six-pip promise would be the same lie the
        // other way up.
        Assert.Equal(8, row.FindAll(".pip").Count);

        // It comes down and does not climb: nothing here lowers the rank, and nothing lets it go
        // higher than the sheet already has it.
        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        row.Find(".pips").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal([8, 7], asked);
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
