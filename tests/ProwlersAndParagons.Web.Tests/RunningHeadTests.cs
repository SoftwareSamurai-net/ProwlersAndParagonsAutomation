using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The name every printed page carries in its top margin, and how it gets onto the document.
///
/// <para><b>What this file can see is the push, not the page.</b> The margin box itself is CSS
/// (<c>@page { @top-center { content: var(--sheet-name) … } }</c>, asserted against the parsed
/// rule in <c>WebPresentationTests</c>) and the property is written by <c>download.js</c>; neither
/// exists inside bUnit. What a rendering test can hold is that the sheet on the page asks for its
/// own name to be put there — the right name, once, and taken down again when the last sheet has
/// gone. Whether a filled box prints on every page was measured against headless Chrome 153
/// rather than assumed, and the figures are in the guide.</para>
/// </summary>
public sealed class RunningHeadTests
{
    private const string Script = "ppSetSheetName";

    private static CharacterSheet Named(string name) =>
        new() { Name = name, SelectedTierId = "standard" };

    private static IEnumerable<JSRuntimeInvocation> Pushes(RenderContext ctx) =>
        ctx.JSInterop.Invocations.Where(i => i.Identifier == Script);

    private static RunningHead HeadOf(RenderContext ctx) => ctx.Services.GetRequiredService<RunningHead>();

    /// <summary>
    /// A host that draws a number of sheets, so one can be taken off the page while another
    /// stays — the approval page's shape, which draws two copies of one character side by side.
    /// </summary>
    private sealed class Sheets : ComponentBase
    {
        [Parameter] public int Count { get; set; }
        [Parameter] public CharacterSheet? Character { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            for (var i = 0; i < Count; i++)
            {
                builder.OpenComponent<SheetView>(i);
                builder.AddComponentParameter(1, nameof(SheetView.Character), Character);
                builder.CloseComponent();
            }
        }
    }

    [Fact]
    public void TheSheetPutsItsNameOnTheDocumentAfterRendering()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.Name = "Lynchpin";

        ctx.Render<SheetView>();

        var push = Assert.Single(Pushes(ctx));
        Assert.Equal("Lynchpin", Assert.Single(push.Arguments));
        Assert.Equal("Lynchpin", HeadOf(ctx).Current);
    }

    /// <summary>
    /// The head says what the sheet's own masthead says, so a page two of an unnamed character
    /// reads "Unnamed" rather than " · page 2 of 3".
    /// </summary>
    [Fact]
    public void ANamelessCharacterIsHeadedTheWayItsMastheadIs()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.Name = "   ";

        var sheet = ctx.Render<SheetView>();

        Assert.Equal("Unnamed", sheet.Find(".ident-name").TextContent.Trim());
        Assert.Equal("Unnamed", Assert.Single(Assert.Single(Pushes(ctx)).Arguments));
    }

    /// <summary>
    /// <b>The character on the page, not the one in the session.</b> A stored character on
    /// <c>/sheet/{id}</c>, a recording, a submission on the approval page — each is handed to the
    /// sheet as a parameter while somebody else's character sits in the session, and the printed
    /// pages are the parameter's.
    /// </summary>
    [Fact]
    public void TheHeadNamesTheCharacterOnThePageNotTheOneInTheSession()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.Name = "Session Character";

        ctx.Render<SheetView>(p => p.Add(c => c.Character, Named("Recorded Character")));

        var push = Assert.Single(Pushes(ctx));
        Assert.Equal("Recorded Character", Assert.Single(push.Arguments));
        Assert.DoesNotContain(Pushes(ctx), i => i.Arguments.Contains("Session Character"));
    }

    /// <summary>
    /// A re-render that changed nothing about the name costs no interop; a change of name is
    /// pushed. The sheet redraws on every edit to the character, and a rank stepper must not be
    /// paid for in document writes.
    /// </summary>
    [Fact]
    public async Task OnlyAChangeOfNameIsPushed()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.Name = "Lynchpin";

        var sheet = ctx.Render<SheetView>();
        Assert.Single(Pushes(ctx));

        // The sheet follows the session, so this is a real redraw — and the name is the same.
        // Positive control on the redraw itself: the render count moves.
        var renders = sheet.RenderCount;
        await sheet.InvokeAsync(ctx.Session.NotifyChanged);
        Assert.True(sheet.RenderCount > renders, "the sheet did not redraw, so nothing below is tested");
        Assert.Single(Pushes(ctx));

        await sheet.InvokeAsync(() =>
        {
            ctx.Session.Sheet.Name = "Vandergraff";
            ctx.Session.NotifyChanged();
        });

        Assert.Equal(2, Pushes(ctx).Count());
        Assert.Equal("Vandergraff", Assert.Single(Pushes(ctx).Last().Arguments));
        Assert.Equal("Vandergraff", sheet.Find(".ident-name").TextContent.Trim());
    }

    /// <summary>
    /// <b>The last sheet out takes the name down, and not before.</b> Cleared when one of two
    /// sheets leaves, the survivor's pages would print anonymous; left when the last one leaves,
    /// the roster would print under the previous character's name.
    /// </summary>
    [Fact]
    public void TheLastSheetOutTakesTheNameDown()
    {
        using var ctx = new RenderContext();
        var character = Named("Lynchpin");

        var host = ctx.Render<Sheets>(p => p.Add(c => c.Count, 2).Add(c => c.Character, character));

        // Positive control: two sheets really are on the page, and both took a hold.
        Assert.Equal(2, host.FindAll(".sheet").Count);
        Assert.Equal(2, HeadOf(ctx).Holds);
        Assert.DoesNotContain(Pushes(ctx), i => i.Arguments.Contains(null));

        host.Render(p => p.Add(c => c.Count, 1).Add(c => c.Character, character));

        Assert.Single(host.FindAll(".sheet"));
        Assert.Equal(1, HeadOf(ctx).Holds);
        Assert.Equal("Lynchpin", HeadOf(ctx).Current);
        Assert.DoesNotContain(Pushes(ctx), i => i.Arguments.Contains(null));

        host.Render(p => p.Add(c => c.Count, 0).Add(c => c.Character, character));

        Assert.Empty(host.FindAll(".sheet"));
        Assert.Equal(0, HeadOf(ctx).Holds);
        Assert.Null(HeadOf(ctx).Current);
        Assert.Contains(Pushes(ctx), i => i.Arguments.Contains(null));
    }
}
