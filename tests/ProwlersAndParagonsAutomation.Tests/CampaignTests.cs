using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// A campaign is data the engine is handed and never asks for.
///
/// <para>Two things are pinned here and both are about what a campaign is <em>not</em> allowed to
/// do. It is not allowed to move a figure — <see cref="ACampaignsTraitCapDoesNotMoveResolve"/>,
/// which is the deferral this slice made explicit — and it is not allowed to arrive through the
/// engine, which <see cref="PresentationFlagsTests"/> and
/// <see cref="AccountsContractTests.TheEngineHasNoNetwork"/> already hold.</para>
/// </summary>
public sealed class CampaignTests : IClassFixture<RulesFixture>
{
    private readonly RulesFixture _f;

    public CampaignTests(RulesFixture f) => _f = f;

    /// <summary>
    /// A campaign whose Trait Cap disagrees with its tier's leaves Resolve exactly where a
    /// character with no campaign at all leaves it.
    ///
    /// <para><b>This is the guard on an owner-approved deferral, and the figure is why it is
    /// worth a test rather than a note.</b> <c>DerivedStatsCalculator.CalculateResolve</c> is
    /// <c>max(0, (TraitCap − highestRelevantRank) × 2)</c> plus the purchases, so a cap applied
    /// from a campaign would move a number the player spent Hero Points on — silently, and
    /// without anything else in the suite noticing. Every published Hero's Resolve is asserted
    /// against the book, so applying a campaign cap <em>anywhere</em> the engine can see it would
    /// show up there; what this test adds is the case those cannot reach, which is a character
    /// that really is in a campaign.</para>
    ///
    /// <para><b>The three campaigns differ in every field that could possibly bite</b> — a cap
    /// well under the tier's, one well over, and the sandbox toggle — because a deferral that
    /// only holds for the values nobody would choose is not a deferral.</para>
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(null)]
    public void ACampaignsTraitCapDoesNotMoveResolve(int? campaignCap)
    {
        var withNoCampaign = InACampaign(null);
        var expected = _f.Derived.CalculateResolve(withNoCampaign);

        // The positive control: the figure is a real one that a cap really could move. A sheet
        // whose Resolve happened to be 0 would satisfy every comparison below for free.
        Assert.True(expected > 0,
            $"the fixture's Resolve is {expected}, so this test cannot tell a cap being applied "
            + "from one being ignored.");

        var campaign = new Campaign("g_0000000000000000000000", "The Long Winter",
            "standard", campaignCap, UnlimitedBudget: true);

        var joined = InACampaign(campaign.Id);

        Assert.Equal(expected, _f.Derived.CalculateResolve(joined));

        // And the same for the two figures the tier decides, which is the other half of what a
        // campaign carries. Nothing about being in one changes what a character costs or what it
        // is allowed to spend.
        Assert.Equal(_f.Costs.TotalCost(withNoCampaign), _f.Costs.TotalCost(joined));
    }

    /// <summary>
    /// A character at the Standard tier with ranks a Trait Cap really does bear on, optionally in
    /// a campaign. Deliberately a plain sheet rather than a published Hero: the point is a figure
    /// that would move if a cap were applied, not a figure that matches the book.
    /// </summary>
    private static CharacterSheet InACampaign(string? campaignId)
    {
        return new CharacterSheet
        {
            SelectedTierId = "standard",
            CampaignId = campaignId,
            AbilityRanks = { ["might"] = 8, ["agility"] = 6 },
            TalentRanks = { ["athletics"] = 4 },
        };
    }
}
