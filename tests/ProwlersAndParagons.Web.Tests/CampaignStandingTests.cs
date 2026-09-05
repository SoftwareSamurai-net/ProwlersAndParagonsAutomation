using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>What a character and its campaign disagree about, on a screen.</b>
///
/// <para><c>CampaignJoin.Inspect</c> shipped called by nothing but its own unit tests: all three
/// findings were computed, asserted and shown to nobody, so a character built to an 8d Trait Cap
/// in a 6d game was told on no screen in the application. That is the fault this repository keeps
/// hitting — a feature that works and no reader can reach — and these are the tests that would
/// have caught it, because every one of them renders the page rather than calling the method.</para>
///
/// <para><b>The campaign is the signed-in account's own, and that is the fixture rather than the
/// subject.</b> <see cref="FakeApi"/> scopes a campaign to the account that holds it, exactly as
/// the real server does, so resolving one from a second account is a thing to drive through the
/// join route. What is under test here is the page asking <c>Inspect</c> at all and saying what it
/// answers.</para>
/// </summary>
public sealed class CampaignStandingTests
{
    private const string CampaignId = "g_0000000000000000000000";

    /// <summary>
    /// A signed-in reader whose character is in a campaign the account can resolve.
    /// </summary>
    /// <param name="tierId">The tier the game is played at, or null for a game that sets none.</param>
    /// <param name="traitCap">The house Trait Cap the game has set, or null for the tier's.</param>
    private static RenderContext AGame(string? tierId = "standard", int? traitCap = null)
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");
        ctx.Api.Campaign(CampaignId, "Pinnacle City",
            StoredCampaign.Write(new Campaign(CampaignId, "Pinnacle City", tierId, traitCap, false)));

        return ctx;
    }

    /// <summary>The prose of the panel a character's standing is drawn in.</summary>
    private static string GamesPanel(RenderContext ctx) => ctx.Render<Campaigns>().Markup;

    /// <summary>
    /// <b>A character built to a tighter cap than its game is told so, on screen.</b>
    ///
    /// <para>Joining never writes over a cap somebody already set — that would move Resolve on a
    /// finished character in the course of typing a join code — so the disagreement outlives the
    /// join and has to be said for as long as it is true.</para>
    ///
    /// <para>The screen says both ranks, because the finding carries them as fields rather than in
    /// its sentence: an id is not what a tier is called, and the same bargain applies to a pair of
    /// numbers a reader cannot act on without.</para>
    /// </summary>
    [Fact]
    public void ACharacterCappedAboveItsGameIsToldOnScreen()
    {
        using var ctx = AGame(traitCap: 6);

        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.TraitCapRank = 8;
        ctx.Session.Sheet.CampaignId = CampaignId;

        var markup = GamesPanel(ctx);

        Assert.Contains("different Trait Cap", markup, StringComparison.Ordinal);
        Assert.Contains("Trait Cap of 8d", markup, StringComparison.Ordinal);
        Assert.Contains("the game caps at 6d", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The positive control, and it is not optional.</b> Every assertion above is a presence,
    /// and a page that printed the sentence unconditionally would satisfy all of them while saying
    /// something false to everybody whose character agrees with their table.
    /// </summary>
    [Fact]
    public void ACharacterThatAgreesWithItsGameIsToldNothing()
    {
        using var ctx = AGame(traitCap: 6);

        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.TraitCapRank = 6;
        ctx.Session.Sheet.CampaignId = CampaignId;

        var markup = GamesPanel(ctx);

        Assert.DoesNotContain("Trait Cap", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("different tier", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tier disagreement reaches the same place, and names both tiers rather than their ids.
    ///
    /// <para>This finding predates the cap by a slice and was shown on no screen either. It is
    /// information rather than an error: these campaigns climb tiers in play.</para>
    /// </summary>
    [Fact]
    public void ATierDisagreementIsToldOnScreenByName()
    {
        using var ctx = AGame(tierId: "high_level");

        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.CampaignId = CampaignId;

        var markup = GamesPanel(ctx);

        Assert.Contains("different tier", markup, StringComparison.Ordinal);
        Assert.Contains("Built to Standard", markup, StringComparison.Ordinal);
        Assert.Contains("played at High Level", markup, StringComparison.Ordinal);

        // The names, never the ids — the rule every finding in this application follows.
        Assert.DoesNotContain("high_level", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A character naming a campaign that is not here says so, rather than being quietly repaired.
    ///
    /// <para>Deleting a campaign leaves its members naming it on purpose, so restoring it puts
    /// everything back — and the member has to be able to find out.</para>
    /// </summary>
    [Fact]
    public void ACampaignThatIsNotHereIsSaidOnScreen()
    {
        using var ctx = AGame();

        ctx.Session.Sheet.CampaignId = "g_1111111111111111111111";

        Assert.Contains("names a campaign that is not here", GamesPanel(ctx),
            StringComparison.Ordinal);
    }
}
