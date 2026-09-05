using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// A campaign is data the engine is handed and never asks for.
///
/// <para>Two things are pinned here. A campaign is not allowed to arrive <em>through</em> the
/// engine, which <see cref="PresentationFlagsTests"/> and
/// <see cref="AccountsContractTests.TheEngineHasNoNetwork"/> already hold — and it is not allowed
/// to move a figure by being pointed at, which <see cref="ACampaignsTraitCapIsNotReadFromTheCampaign"/>
/// holds here.</para>
///
/// <para><b>What is <em>not</em> pinned here any more is that a Trait Cap cannot move Resolve.</b>
/// It can, and it must: the owner settled on 2026-09-05 that a house cap substitutes for the
/// tier's rather than merely gating validation, because <c>CalculateResolve</c> measures from the
/// cap and gating pays a character for room the table has taken away. The route is the difference
/// and it is the whole of what survives the reversal — <see cref="CharacterSheet.TraitCapRank"/>
/// is a field on the character, put there by a join, and <see cref="Campaign.TraitCapRank"/> is
/// still never read by anything that computes anything.</para>
/// </summary>
public sealed class CampaignTests : IClassFixture<RulesFixture>
{
    private readonly RulesFixture _f;

    public CampaignTests(RulesFixture f) => _f = f;

    /// <summary>
    /// Being <em>in</em> a campaign moves nothing. The campaign holds a cap well under the
    /// tier's, one well over, and the sandbox toggle, and the character names it by id — and the
    /// engine, which cannot resolve an id, answers exactly what it answers for a character in no
    /// campaign at all.
    ///
    /// <para><b>This is the guard that stayed after the design question was answered.</b> The
    /// cap now really does move Resolve, so the thing worth pinning is no longer "a cap is
    /// inert" but "a cap reaches the engine only by being written onto the character" — see
    /// <see cref="AHouseTraitCapMovesResolve"/> for the other half. If a reader for
    /// <c>CampaignId</c> is ever written, this is the test that fails.</para>
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(null)]
    public void ACampaignsTraitCapIsNotReadFromTheCampaign(int? campaignCap)
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
    /// <b>The reversal, in one figure.</b> A 4d character at Standard is paid <c>(12−4)×2 = 16</c>
    /// Resolve for the room the tier gives it. Put the same character under a 6d house cap and it
    /// is paid <c>(6−4)×2 = 4</c> — the room it actually has.
    ///
    /// <para>Written as the arithmetic rather than as "less than before", because "lower" passes
    /// for any substitution at all, including one off by a factor of two. This is the assertion
    /// the old <c>ACampaignsTraitCapDoesNotMoveResolve</c> was the negation of.</para>
    /// </summary>
    [Fact]
    public void AHouseTraitCapMovesResolve()
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            AbilityRanks = { ["might"] = 4 },
        };

        Assert.Equal(12, _f.Rules.GetTier("standard")!.TraitCapRank);
        Assert.Equal(16, _f.Derived.CalculateResolve(sheet));

        sheet.TraitCapRank = 6;

        Assert.Equal(4, _f.Derived.CalculateResolve(sheet));
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
