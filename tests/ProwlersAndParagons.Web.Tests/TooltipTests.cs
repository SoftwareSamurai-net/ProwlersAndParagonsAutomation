using Bunit;
using Microsoft.AspNetCore.Components.Web;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A term with its explanation available on demand.
///
/// <para><b>What these can see and what they cannot.</b> They cover the wiring — which events
/// open and close it, what the trigger announces, that the description resolves. They cannot see
/// that the tip is <em>invisible</em> while closed, because that is entirely a stylesheet's doing:
/// emptying the rule leaves every assertion here passing with the sentence permanently on screen.
/// That half is in <c>WebPresentationTests</c>, against the parsed rule.</para>
/// </summary>
public sealed class TooltipTests
{
    private const string Term = "the Trait Cap";
    private const string Text = "The highest rank any single Trait may reach at this tier.";

    private static IRenderedComponent<Tooltip> Render(RenderContext ctx) =>
        ctx.Render<Tooltip>(p => p.Add(x => x.Term, Term).Add(x => x.Text, Text));

    /// <summary>
    /// The description is in the document whether or not the tip is showing, and the reference
    /// resolves to it.
    ///
    /// <para><b>This is the opposite of the budget breakdown's rule, deliberately.</b> There,
    /// <c>aria-controls</c> is written only while the target exists, because it genuinely does not
    /// exist when closed. Here an <c>aria-describedby</c> that pointed at nothing while hidden
    /// would dangle for all but a moment — and a description a reader has to hover to be given is
    /// one a screen-reader user never gets at all.</para>
    /// </summary>
    [Fact]
    public void TheDescriptionResolvesWhileClosed()
    {
        using var ctx = new RenderContext();
        var tip = Render(ctx);

        // Closed: no "shown" class anywhere, so this is the closed state and not a tip that is
        // always open.
        Assert.Empty(tip.FindAll(".tip.shown"));

        var named = tip.Find(".tip-trigger").GetAttribute("aria-describedby");
        Assert.False(string.IsNullOrEmpty(named));

        var described = tip.Find($"#{named}");
        Assert.Equal("tooltip", described.GetAttribute("role"));
        Assert.Contains(Text, described.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Focus opens it and blur closes it. <b>Focus is what makes this reachable at all</b> without
    /// a mouse — a tooltip wired to hover alone excludes every keyboard and touch reader.
    /// </summary>
    [Fact]
    public void FocusOpensItAndBlurClosesIt()
    {
        using var ctx = new RenderContext();
        var tip = Render(ctx);

        tip.Find(".tip-trigger").Focus();
        Assert.Single(tip.FindAll(".tip.shown"));
        Assert.Equal("true", tip.Find(".tip-trigger").GetAttribute("aria-expanded"));

        tip.Find(".tip-trigger").Blur();
        Assert.Empty(tip.FindAll(".tip.shown"));
        Assert.Equal("false", tip.Find(".tip-trigger").GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// The pointer opens it too, and <b>the handlers are on the wrapper rather than the button</b>
    /// — so reaching the tip with the pointer does not close it under your own cursor. WCAG 1.4.13
    /// asks for hoverable, and a tip that vanishes when you move towards it fails that.
    /// </summary>
    [Fact]
    public void ThePointerOpensItAndTheTipItselfKeepsItOpen()
    {
        using var ctx = new RenderContext();
        var tip = Render(ctx);

        var wrap = tip.Find(".tip-wrap");

        wrap.MouseEnter();
        Assert.Single(tip.FindAll(".tip.shown"));

        tip.Find(".tip-wrap").MouseLeave();
        Assert.Empty(tip.FindAll(".tip.shown"));

        // **The structural half, and without it the name of this test is a claim rather than a
        // check.** `mouseenter` does not bubble, so what keeps the tip open while the pointer is
        // on it is that the tip is *inside* the element carrying the handlers. Moving those onto
        // the button would leave the two assertions above passing on a tip that closes the moment
        // you reach for it — which is the exact WCAG 1.4.13 failure this is named after. An
        // adversarial pass found that hole by moving them.
        Assert.NotNull(tip.Find(".tip-wrap .tip"));

        // ...and the button is not the thing carrying them, which is the other way to say it.
        var trigger = tip.Find(".tip-trigger");
        Assert.Null(trigger.QuerySelector(".tip"));
    }

    /// <summary>
    /// Escape dismisses it, which WCAG 1.4.13 requires and the obvious implementation leaves out:
    /// a tip that can only be closed by moving a pointer is not dismissable by somebody who is
    /// not using one.
    /// </summary>
    [Fact]
    public void EscapeDismissesIt()
    {
        using var ctx = new RenderContext();
        var tip = Render(ctx);

        tip.Find(".tip-trigger").Focus();
        Assert.Single(tip.FindAll(".tip.shown"));

        tip.Find(".tip-trigger").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(tip.FindAll(".tip.shown"));

        // A different key does not, so the check above is reading the key rather than the event.
        tip.Find(".tip-trigger").Focus();
        tip.Find(".tip-trigger").KeyDown(new KeyboardEventArgs { Key = "a" });
        Assert.Single(tip.FindAll(".tip.shown"));
    }

    /// <summary>
    /// The trigger is named for what it explains, and <b>the sentence is not its name</b>.
    ///
    /// <para>The natural mistake is to hang the explanation off the button as a label, at which
    /// point a screen reader reads the whole paragraph where it expects to be told what the
    /// control is. The sentence is the <em>description</em>; the name is short.</para>
    /// </summary>
    [Fact]
    public void TheTriggerIsNamedForTheTermAndNotByTheSentence()
    {
        using var ctx = new RenderContext();
        var trigger = Render(ctx).Find(".tip-trigger");

        Assert.Contains(Term, trigger.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(Text, trigger.TextContent, StringComparison.Ordinal);

        // The visible "?" is hidden from the reader that gets the words instead, or it is
        // announced as punctuation in the middle of the name.
        Assert.Equal("true", trigger.QuerySelector("[aria-hidden]")!.GetAttribute("aria-hidden"));
    }

    /// <summary>
    /// The identifier is derived from the term, not generated per render.
    ///
    /// <para><b>A fresh id each render would break the replay's strongest guard</b>, which renders
    /// the same character twice and requires the two pages to be identical — it would report a
    /// difference on every run that is not a difference at all.</para>
    /// </summary>
    [Fact]
    public void TheIdentifierIsStableAcrossRenders()
    {
        using var ctx = new RenderContext();

        var first = Render(ctx).Find(".tip-trigger").GetAttribute("aria-describedby");
        var second = Render(ctx).Find(".tip-trigger").GetAttribute("aria-describedby");

        Assert.Equal(first, second);
        Assert.False(string.IsNullOrEmpty(first));
    }

    /// <summary>
    /// It reaches the app: the budget breakdown explains the Trait Cap, and the figure it explains
    /// is still printed beside it.
    ///
    /// <para><b>The second half is the assertion that matters.</b> A tooltip is supplementary, and
    /// the failure mode of adding one is quietly moving something into it — at which point the
    /// readers who cannot open it have lost a number that used to be on the page.</para>
    /// </summary>
    [Fact]
    public void TheBreakdownExplainsTheTraitCapWithoutHidingIt()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var strip = ctx.Render<HpBudgetBar>();
        strip.Find(".budget-toggle").Click();

        var breakdown = strip.Find("#budget-breakdown");

        Assert.Single(breakdown.QuerySelectorAll(".tip-wrap"));

        // The Trait Cap's own cell, not the first figure in the row — the breakdown opens with
        // the package cost, and reading `.num` got that instead.
        var cell = breakdown.Children.Single(
            e => e.TextContent.StartsWith("Trait Cap", StringComparison.Ordinal));

        Assert.Contains(
            ctx.Session.TraitCap.ToString(),
            cell.QuerySelector(".num")!.TextContent,
            StringComparison.Ordinal);
    }
}
