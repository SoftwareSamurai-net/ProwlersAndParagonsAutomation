using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>Every surface that prints a Trait Cap prints the one in force, and names the tier's beside
/// it when the two differ.</b>
///
/// <para>"Trait Cap 6d" at the Standard tier looks like a mistake to anybody who knows the tier
/// allows 12d, so four surfaces say both — the budget strip, the printed sheet's meta line, the
/// Resolve breakdown and a replay's verdict. <b>Only the strip was tested.</b> The other three
/// were written and asserted nowhere, which is one edit away from a screen printing the tier's
/// ceiling over a character bounded and judged by something else.</para>
///
/// <para>The GM's own form is here too, for the mistake at the other end: a house cap
/// <em>looser</em> than its tier is nonsense the box accepted in silence.</para>
/// </summary>
public sealed class TraitCapOnScreenTests
{
    /// <summary>The Standard tier's own ceiling, which every case below moves off.</summary>
    private const int TiersCap = 12;

    private const int HouseCap = 6;

    private const string CampaignId = "g_0000000000000000000000";

    /// <summary>A character at the Standard tier, optionally built to a house cap.</summary>
    private static CharacterSheet ACharacter(int? houseCap)
    {
        return new CharacterSheet
        {
            Name = "Ninefold",
            SelectedTierId = "standard",
            TraitCapRank = houseCap,
            AbilityRanks = { ["might"] = 4, ["agility"] = 3 },
        };
    }

    // ── The printed sheet's meta line ────────────────────────────────────────────────────

    /// <summary>
    /// The sheet's frame line carries the cap the character is built to, and the tier's beside it.
    ///
    /// <para>This is the line somebody prints and takes to a table, so it is the one place the two
    /// figures most need to be together: a sheet reading "Standard · Trait Cap 6d" invites the
    /// reader to correct it.</para>
    /// </summary>
    [Fact]
    public void ThePrintedSheetNamesBothCeilings()
    {
        using var ctx = new RenderContext();

        var meta = SheetMetaOf(ctx, ACharacter(HouseCap));

        Assert.Contains($"Trait Cap {HouseCap}d", meta, StringComparison.Ordinal);
        Assert.Contains($"tier {TiersCap}d", meta, StringComparison.Ordinal);
    }

    /// <summary>
    /// The positive control: without a house cap the sheet names one figure, so the assertion
    /// above cannot be satisfied by a line that always prints two.
    /// </summary>
    [Fact]
    public void ThePrintedSheetNamesOneCeilingWhenThereIsOnlyOne()
    {
        using var ctx = new RenderContext();

        var meta = SheetMetaOf(ctx, ACharacter(null));

        Assert.Contains($"Trait Cap {TiersCap}d", meta, StringComparison.Ordinal);
        Assert.DoesNotContain("tier 12d", meta, StringComparison.Ordinal);
    }

    private static string SheetMetaOf(RenderContext ctx, CharacterSheet sheet) =>
        SheetText.Visible(ctx.Render<SheetView>(p => p.Add(x => x.Character, sheet)).Find(".sheet"));

    // ── The Resolve breakdown ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The Resolve working names both ceilings, and says which one the figure came from —
    /// without claiming a direction it cannot know.</b>
    ///
    /// <para>The sentence used to read "This game caps tighter than its tier's 12d", and the flag
    /// behind it is <c>TraitCapIsNotTheTiers</c>, which cannot tell tighter from looser. A cap
    /// above the tier's is an error the validator reports and <em>still uses</em> — reported,
    /// never repaired — so that line really could be drawn over a 20d house cap at a 12d tier,
    /// saying the opposite of the two numbers printed either side of it.</para>
    /// </summary>
    [Theory]
    [InlineData(HouseCap)]   // tighter, the ordinary case
    [InlineData(20)]         // looser, which is an error and is still what the figures use
    public void TheResolveWorkingNamesBothCeilingsWithoutClaimingADirection(int houseCap)
    {
        using var ctx = new RenderContext();

        ctx.Session.Open(ACharacter(houseCap), SheetMode.Hero);

        var text = ResolveWorkingOf(ctx);

        Assert.Contains($"Trait Cap {houseCap}d", text, StringComparison.Ordinal);
        Assert.Contains($"Not the tier's {TiersCap}d", text, StringComparison.Ordinal);

        // Neither direction is claimed, because neither can be told from the flag behind it.
        Assert.DoesNotContain("tighter", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("looser", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The control: a character on the tier's own cap is told about one figure.</summary>
    [Fact]
    public void TheResolveWorkingNamesOneCeilingWhenThereIsOnlyOne()
    {
        using var ctx = new RenderContext();

        ctx.Session.Open(ACharacter(null), SheetMode.Hero);

        var text = ResolveWorkingOf(ctx);

        Assert.Contains($"Trait Cap {TiersCap}d", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Not the tier's", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Resolve section's own working. There are two <c>ul.small</c> lists on that page — Edge
    /// has one as well — so the list is picked by what it is about rather than by being first.
    /// </summary>
    private static string ResolveWorkingOf(RenderContext ctx) =>
        ctx.Render<Derived>().FindAll("ul.small")
            .Select(list => list.TextContent)
            .Single(text => text.Contains("Trait Cap", StringComparison.Ordinal));

    // ── A replay's verdict ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A recording's verdict shows the cap its own figures were computed from, and names the
    /// tier's when they differ — the panel is the whole point of a replay, so a figure in it that
    /// came from somewhere else would be showing the wrong demonstration.
    /// </summary>
    [Fact]
    public void AReplaysVerdictNamesBothCeilings()
    {
        using var ctx = new RenderContext();

        var text = VerdictOf(ctx, ACharacter(HouseCap));

        Assert.Contains($"Trait Cap {HouseCap}", text, StringComparison.Ordinal);
        Assert.Contains($"not the tier's {TiersCap}d", text, StringComparison.Ordinal);
    }

    /// <summary>The control, again: one cap, one figure.</summary>
    [Fact]
    public void AReplaysVerdictNamesOneCeilingWhenThereIsOnlyOne()
    {
        using var ctx = new RenderContext();

        var text = VerdictOf(ctx, ACharacter(null));

        Assert.Contains($"Trait Cap {TiersCap}", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not the tier's", text, StringComparison.Ordinal);
    }

    private static string VerdictOf(RenderContext ctx, CharacterSheet sheet) =>
        ctx.Render<ReplayVerdict>(p => p.Add(x => x.Character, sheet))
            .Find(".replay-figures").TextContent;

    // ── The GM's own form ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A house cap above the chosen tier's is said out loud on the form that took it.</b>
    ///
    /// <para>The box takes any whole number from 1 to 30 whatever tier sits above it, so 20d over
    /// the Standard tier's 12d was accepted in silence — under a hint reading "Tighter than the
    /// tier's" — and every character joining with no cap of its own inherited a
    /// <c>TRAIT_CAP_ABOVE_TIER</c> error on a screen the GM never opens.</para>
    ///
    /// <para>Said and not refused: a GM may type the cap before choosing the tier, and a Save that
    /// silently does nothing is a control that looks broken. It is also the answer the engine
    /// gives the same mistake one level down.</para>
    /// </summary>
    [Fact]
    public async Task TheCampaignFormSaysWhenACapIsLooserThanItsTier()
    {
        await using var ctx = AGmWithACampaign("standard", 20);

        var settings = await OpenSettings(ctx);

        Assert.Contains("above the Standard tier's 12d", settings, StringComparison.Ordinal);
    }

    /// <summary>
    /// The positive control, over both directions the form has to stay quiet about: a cap under
    /// the tier's ceiling, which is what a house cap is for, and a campaign that names no tier at
    /// all, which has nothing to be above.
    /// </summary>
    [Theory]
    [InlineData("standard", HouseCap)]
    [InlineData("standard", TiersCap)]
    [InlineData(null, 20)]
    public async Task TheCampaignFormSaysNothingAboutACapThatIsInBounds(string? tier, int cap)
    {
        await using var ctx = AGmWithACampaign(tier, cap);

        var settings = await OpenSettings(ctx);

        Assert.DoesNotContain("A house cap tightens a tier's ceiling", settings,
            StringComparison.Ordinal);
    }

    private static RenderContext AGmWithACampaign(string? tierId, int? traitCap)
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(CampaignId, "Pinnacle City", tierId, traitCap, false)));

        return ctx;
    }

    /// <summary>
    /// Presses "Rename or retier" and hands back the editor's prose. The form is behind that
    /// button, so a test reading the page without pressing it reads markup no GM can see.
    /// </summary>
    private static async Task<string> OpenSettings(RenderContext ctx)
    {
        var page = ctx.Render<Campaigns>();

        var settings = page.FindAll("button").Single(b => b.TextContent.Trim() == "Settings");
        await settings.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var edit = page.FindAll("button").Single(b => b.TextContent.Trim() == "Rename or retier");
        await edit.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        return page.Find(".campaign-editor").TextContent;
    }
}
