using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The two lines a campaign's shared vehicle or base prints</b>, and the whole of the
/// difference between them: one is for a reader holding the object alone, the other for the one
/// screen that has read the sheets that paid for it.
///
/// <para><b>Its own file rather than a section of <c>AssetFormatterTests</c></b>, which covers the
/// four lines a <em>character's</em> own machines print. These two are the campaign's half of
/// Chapter 6's pooling rule and fail differently: what they can get wrong is the currency, and a
/// currency printed wrongly here is the category error the whole chapter is careful about.</para>
/// </summary>
public sealed class CampaignAssetFormatterTests
{
    private static CampaignAsset Wing =>
        new("a_0000000000000000000000", CampaignAssetContribution.Vehicle, "The Wing")
        {
            Body = 8, Speed = 10, Control = 3
        };

    private static CampaignAsset Roost =>
        new("a_1111111111111111111111", CampaignAssetContribution.Headquarters, "The Roost");

    /// <summary>
    /// <b>The object alone prints no figure in its own currency, and that is deliberate.</b> A
    /// shared object's budget is the sum of what its members put in, so the pair a reader wants
    /// cannot be worked out from the object — and half a pair with nothing saying so is worse than
    /// neither half.
    /// </summary>
    [Fact]
    public void TheObjectAloneSaysWhatItIsAndPrintsNoBudget()
    {
        Assert.Equal("shared vehicle · Body 8d · Speed 10d · Control +3 · unarmed",
                     AssetFormatter.Describe(Wing));

        // pp.100–103 give a headquarters no characteristics at all, so there are none to print.
        Assert.Equal("shared headquarters", AssetFormatter.Describe(Roost));
    }

    /// <summary>
    /// <b>With the sheets in hand it prints the pair, each in the object's own currency.</b> A
    /// vehicle is Vehicle Points and a base is Base Points; neither is Hero Points, and the two
    /// must never be the same word.
    /// </summary>
    [Fact]
    public void TheBooksPrintTheSpendAgainstTheBudgetInTheRightCurrency()
    {
        Assert.Equal("The Wing — 18/25 Vehicle Points",
                     AssetFormatter.Describe(Wing, spent: 18, budget: 25));

        Assert.Equal("The Roost — 4/6 Base Points",
                     AssetFormatter.Describe(Roost, spent: 4, budget: 6));
    }

    /// <summary>
    /// <b>An object these rules cannot price prints what was put in and stops.</b> A zero would
    /// claim nothing had been built with it, which is the opposite of what is known: what is known
    /// is that nothing here can say.
    /// </summary>
    [Fact]
    public void AnObjectWithNoPricePrintsWhatWentInRatherThanAZero()
    {
        Assert.Equal("The Wing — 25 Vehicle Points put in",
                     AssetFormatter.Describe(Wing, spent: null, budget: 25));

        Assert.DoesNotContain("0/", AssetFormatter.Describe(Wing, spent: null, budget: 25),
                              StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An object with no name prints its id</b> — the same fallback a contribution makes, so a
    /// reader meets one answer whichever screen they are on. Inventing a name for it would be the
    /// repair this project's formatters never make.
    /// </summary>
    [Fact]
    public void AnUnnamedObjectPrintsItsId() =>
        Assert.StartsWith("a_0000000000000000000000 — ",
                          AssetFormatter.Describe(Wing with { Name = "  " }, spent: 0, budget: 0),
                          StringComparison.Ordinal);
}
