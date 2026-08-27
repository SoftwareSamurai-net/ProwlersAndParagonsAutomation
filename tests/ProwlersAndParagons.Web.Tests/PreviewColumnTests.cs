using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The sheet drawn beside the editors, rather than only at the end.
///
/// <para><b>The printed sheet is this tool's deliverable and it appeared only behind six
/// steps.</b> Somebody choosing a Power had no way to see what it did to the thing they are
/// making until they had finished making it.</para>
///
/// <para>The half whose substance is in the stylesheet — that there are two columns at all, and
/// that the sheet stays put while the editors scroll — is in <c>WebPresentationTests</c>. Every
/// assertion here passes with that rule deleted and the preview stacked under the editors, which
/// is a worse page rather than a broken one.</para>
/// </summary>
public sealed partial class PreviewColumnTests
{
    private static RenderContext Building()
    {
        var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        return ctx;
    }

    /// <summary>
    /// The editing step draws the sheet beside the editors.
    /// </summary>
    [Fact]
    public void TheSheetIsOnTheStepWhereTheCharacterIsBuilt()
    {
        using var ctx = Building();

        var page = ctx.Render<Characteristics>();

        Assert.Single(page.FindAll(".preview"));
        Assert.Single(page.FindAll(".preview .sheet"));
    }

    /// <summary>
    /// <b>It is the same sheet component the review step renders, not a preview-shaped twin.</b>
    ///
    /// <para>There is exactly one sheet in this app by design — the recordings taught that
    /// lesson, where a second rendering path printed the visitor's own figures under a recorded
    /// name. A preview built from its own markup would be free to drift from the thing it claims
    /// to be previewing, and the drift would look like nothing at all.</para>
    /// </summary>
    [Fact]
    public void ItIsTheSameSheetTheReviewStepDraws()
    {
        using var ctx = Building();
        ctx.Session.LoadSample(SheetMode.Hero);

        // **Blazor's own event-handler ids are stripped, and nothing else is.** The sheet
        // explains every name on it now, so each term's button carries `blazor:onkeydown="N"` where
        // N is a per-renderer counter — two renders of the identical component get different
        // numbers, and comparing the raw markup would fail on a difference no reader could ever
        // see. This is the same trap `Term` already documents for its own ids and solves by
        // deriving them from the name; these are Blazor's and cannot be derived from anything.
        static string Stable(string markup) =>
            HandlerId().Replace(markup, "blazor:$1=\"\"");

        var preview = Stable(ctx.Render<Characteristics>().Find(".preview .sheet").InnerHtml);
        var review = Stable(ctx.Render<Review>().Find(".sheet").InnerHtml);

        Assert.Equal(review, preview);

        // The positive control: something really was stripped, so a change that stopped the sheet
        // rendering its terms at all could not pass this by making both sides trivially equal.
        Assert.Contains("blazor:onkeydown", preview, StringComparison.Ordinal);
    }

    /// <summary>
    /// It shows the character being built, and follows it.
    ///
    /// <para><b>The control comes first.</b> Asserting only that a Power appears would pass
    /// against a sheet that had been showing it all along, so the Power is asserted absent, then
    /// added, then asserted present.</para>
    /// </summary>
    [Fact]
    public async Task ItFollowsTheCharacter()
    {
        // `await using`, because this test is async and RenderContext disposes asynchronously:
        // a synchronous `using` on it blocks the disposal on a renderer that may still be
        // draining, which is the same deadlock shape as blocking on InvokeAsync above.
        await using var ctx = Building();

        var session = ctx.Session;
        var flight = ctx.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "flight");

        var page = ctx.Render<Characteristics>();
        Assert.DoesNotContain(flight.Name, page.Find(".preview").TextContent, StringComparison.Ordinal);

        // Through the dispatcher, because notifying the session is what triggers the re-render and
        // Blazor refuses one raised off its own thread. **Awaited rather than blocked on**: a test
        // that blocks on a renderer task can deadlock against that same dispatcher, which is a
        // failure that would arrive as a hung CI run rather than as a red test.
        await page.InvokeAsync(() =>
        {
            session.Sheet.SelectedPowers.Add(new SelectedPower(flight.Id, 4));
            session.NotifyChanged();
        });

        Assert.Contains(flight.Name, page.Find(".preview").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A sheet handed a character does not follow the session, and that is the half of the
    /// subscription that matters.</b>
    ///
    /// <para>The sheet only redraws on a change to the character being built because nothing else
    /// could make it notice — it reads the session and takes no parameter that changes, so Blazor
    /// skips it when the parent re-renders. Subscribing unconditionally would tie a recorded
    /// character's sheet to the visitor's own edits, which is the exact influence the replay
    /// renders two pages to forbid.</para>
    ///
    /// <para>The positive control is the test above: the session's own sheet does follow.</para>
    /// </summary>
    [Fact]
    public async Task ASheetHandedACharacterIgnoresTheSession()
    {
        // `await using`, because this test is async and RenderContext disposes asynchronously:
        // a synchronous `using` on it blocks the disposal on a renderer that may still be
        // draining, which is the same deadlock shape as blocking on InvokeAsync above.
        await using var ctx = Building();

        var session = ctx.Session;
        var recorded = SampleCharacters.Villain();
        var flight = ctx.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "flight");

        var page = ctx.Render<ProwlersAndParagonsAutomation.Web.Components.SheetView>(
            p => p.Add(v => v.Character, recorded).Add(v => v.ShowBudget, false));

        var before = page.Markup;

        await page.InvokeAsync(() =>
        {
            session.Sheet.SelectedPowers.Add(new SelectedPower(flight.Id, 4));
            session.NotifyChanged();
        });

        Assert.Equal(before, page.Markup);
    }

    /// <summary>
    /// <b>The aside does not announce itself as a seventh section of a five-section step.</b>
    ///
    /// <para>It is complementary content — the same character, drawn as it will print — so it
    /// carries a label rather than a heading. A heading would put it in the document outline
    /// beside Abilities, Talents, Powers, Perks and Flaws, which are things to do.</para>
    /// </summary>
    [Fact]
    public void ThePreviewIsLabelledRatherThanHeaded()
    {
        using var ctx = Building();

        var aside = ctx.Render<Characteristics>().Find(".preview");

        Assert.Equal("ASIDE", aside.TagName);
        Assert.False(string.IsNullOrWhiteSpace(aside.GetAttribute("aria-label")));
        Assert.Empty(aside.QuerySelectorAll("h1, h2"));
    }

    /// <summary>
    /// No preview before a tier is chosen, because there is no step yet.
    ///
    /// <para>The tier sets the budget and the Trait Cap everything is measured against, and this
    /// step refuses to draw its editors without one. A sheet beside that refusal would be a sheet
    /// beside no editors at all.</para>
    /// </summary>
    [Fact]
    public void NothingIsPreviewedBeforeATierIsChosen()
    {
        using var ctx = new RenderContext();

        Assert.Empty(ctx.Render<Characteristics>().FindAll(".preview"));
    }

    /// <summary>
    /// <b>The finishing step deliberately has none</b>, and that is a decision rather than an
    /// omission.
    ///
    /// <para>That is where the free text is — name, appearance, motivation — and a whole sheet
    /// re-rendered behind every keypress is the render cost the front-end plan warns about. The
    /// step where the character is actually built types nothing letter by letter: the ranks are
    /// steppers and the lists are pickers.</para>
    /// </summary>
    [Fact]
    public void TheStepThatTypesLetterByLetterHasNoPreview()
    {
        using var ctx = Building();

        Assert.Empty(ctx.Render<Finishing>().FindAll(".preview"));

        // The control: the step that does have one still has it, so this says something about
        // where the preview is rather than about it having been removed.
        Assert.Single(ctx.Render<Characteristics>().FindAll(".preview"));
    }

    /// <summary>Blazor's per-render event-handler ids, which mean nothing to a reader.</summary>
    [GeneratedRegex(@"blazor:(on[a-z]+)=""\d+""")]
    private static partial Regex HandlerId();
}
