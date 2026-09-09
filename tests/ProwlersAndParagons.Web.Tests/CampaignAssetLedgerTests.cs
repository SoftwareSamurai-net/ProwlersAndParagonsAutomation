using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The campaign side of Chapter 6's pooling rule, as a screen reads it</b>: what a shared
/// object's members bought it, what it cost, who paid, and a contribution naming an object that is
/// not there.
///
/// <para><c>CampaignAssetTests</c> in the engine suite covers the arithmetic. This covers the two
/// decisions that cannot live in the engine because both need a campaign in front of them — which
/// is the same line <c>CampaignJoin</c> is on, and the reason <c>UNKNOWN_CAMPAIGN</c> is in
/// <c>web/</c> rather than in the validator.</para>
/// </summary>
public sealed class CampaignAssetLedgerTests
{
    private const string TheWing = "a_0000000000000000000000";
    private const string TheRoost = "a_1111111111111111111111";

    private static CampaignAsset Wing =>
        new(TheWing, CampaignAssetContribution.Vehicle, "The Wing") { Body = 8, Speed = 10 };

    private static CampaignAsset Roost =>
        new(TheRoost, CampaignAssetContribution.Headquarters, "The Roost")
        {
            Features = [new SelectedAssetFeature("training_facilities")]
        };

    private static Campaign Game(params CampaignAsset[] assets) =>
        new("g_0000000000000000000000", "Nightfall", "standard", 8, false, Assets: assets);

    private static CharacterSheet Who(params (string Id, string Kind, int HeroPoints)[] put)
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard" };

        foreach (var (id, kind, points) in put)
        {
            sheet.CampaignAssets.Add(new CampaignAssetContribution(id)
            {
                Kind = kind, HeroPoints = points, Name = "The Wing"
            });
        }

        return sheet;
    }

    // ── The budget is summed across the members ───────────────────────────────

    /// <summary>
    /// <b>Three members' Hero Points buy one object, and the row says who paid what.</b> That is
    /// the whole of what the campaign side is for: the object cannot live on a sheet, so the sum
    /// has to happen where several sheets can be read at once.
    /// </summary>
    [Fact]
    public void TheBudgetIsEveryMembersContributionAndTheRowSaysWhoPaidWhat()
    {
        using var ctx = new RenderContext();

        var ledger = CampaignAssets.Ledger(Game(Wing), ctx.Session.Costs,
        [
            ("m_1", "Bulwark", Who((TheWing, CampaignAssetContribution.Vehicle, 1))),
            ("m_2", "Nightjar", Who((TheWing, CampaignAssetContribution.Vehicle, 3))),
            ("m_3", "Halo",     Who((TheWing, CampaignAssetContribution.Vehicle, 2))),
        ]);

        var line = Assert.Single(ledger);

        Assert.Equal(6 * ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint, line.Budget);

        // Largest first, so the question "who paid for this" is answered by the top of the list.
        Assert.Equal(["Nightjar", "Halo", "Bulwark"], line.Contributors.Select(c => c.Who));
        Assert.Equal([3, 2, 1], line.Contributors.Select(c => c.HeroPoints));
    }

    /// <summary>
    /// <b>A member who put nothing in is not a row.</b> Every member of a game has a sheet and
    /// almost none of them has put into any one object, so a line per member would be a list of
    /// zeroes with the answer buried in it.
    /// </summary>
    [Fact]
    public void AMemberWhoPutNothingInIsNotAContributor()
    {
        using var ctx = new RenderContext();

        var ledger = CampaignAssets.Ledger(Game(Wing), ctx.Session.Costs,
        [
            ("m_1", "Bulwark",  Who((TheWing, CampaignAssetContribution.Vehicle, 2))),
            ("m_2", "Nightjar", Who()),
        ]);

        Assert.Equal(["Bulwark"], Assert.Single(ledger).Contributors.Select(c => c.Who));
    }

    /// <summary>
    /// <b>Two objects do not fund each other, and each is priced in its own currency.</b> A single
    /// pool would be the category error the whole chapter is careful about — twenty-five Vehicle
    /// Points to the Hero Point against three Base Points.
    /// </summary>
    [Fact]
    public void EachObjectIsFundedAndPricedOnItsOwn()
    {
        using var ctx = new RenderContext();

        var ledger = CampaignAssets.Ledger(Game(Wing, Roost), ctx.Session.Costs,
        [
            ("m_1", "Bulwark", Who((TheWing, CampaignAssetContribution.Vehicle, 2),
                                   (TheRoost, CampaignAssetContribution.Headquarters, 4))),
        ]);

        Assert.Equal(2, ledger.Count);
        Assert.Equal(2 * ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint, ledger[0].Budget);
        Assert.Equal(4 * ctx.Session.Rules.Assets.BasePointsPerHeroPoint, ledger[1].Budget);
    }

    /// <summary>
    /// <b>An object nobody has funded is still a row.</b> A GM writes the machine down and the
    /// table pays for it afterwards; a list that drew only the funded ones would answer "nothing
    /// shared here" to a GM looking at the object they had just made.
    /// </summary>
    [Fact]
    public void AnUnfundedObjectStillGetsARow()
    {
        using var ctx = new RenderContext();

        var line = Assert.Single(CampaignAssets.Ledger(Game(Wing), ctx.Session.Costs, []));

        Assert.Equal(0, line.Budget);
        Assert.Empty(line.Contributors);
        Assert.True(line.IsOverBudget, "an object built with nothing put in is over its budget");
    }

    // ── Over budget is reported, never repaired ───────────────────────────────

    /// <summary>
    /// <b>An object built past what its members paid for says so, and both figures survive.</b>
    /// Reported and never repaired — the remedy is somebody putting more in or the object losing a
    /// feature, and both are decisions about a table's game.
    ///
    /// <para><b>Three states rather than two, and the third was added because a mutation
    /// survived.</b> Under budget is the positive control, without which an implementation calling
    /// everything over budget would pass. Funded by nobody is the extreme. Neither of those
    /// noticed <c>Spent &gt; Budget * 2</c>: at a budget of nothing that is still true, and 18
    /// against 50 is still false — so <b>an object over by a little</b> is the case that decides
    /// whether the comparison is the one this line claims to make.</para>
    /// </summary>
    [Fact]
    public void AnObjectBuiltPastItsBudgetIsReportedWithBothFigures()
    {
        using var ctx = new RenderContext();

        var rate = ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint;

        // Body 8 + Speed 10 is 18 Vehicle Points; one Hero Point buys 25 and none buys nothing.
        var funded = Assert.Single(CampaignAssets.Ledger(Game(Wing), ctx.Session.Costs,
            [("m_1", "Bulwark", Who((TheWing, CampaignAssetContribution.Vehicle, 1)))]));

        // Body 20 + Speed 10 is 30, against the same one Hero Point's 25: over, and nowhere near
        // twice over.
        var barely = Assert.Single(CampaignAssets.Ledger(
            Game(Wing with { Body = 20 }), ctx.Session.Costs,
            [("m_1", "Bulwark", Who((TheWing, CampaignAssetContribution.Vehicle, 1)))]));

        var starved = Assert.Single(CampaignAssets.Ledger(Game(Wing), ctx.Session.Costs, []));

        Assert.False(funded.IsOverBudget);
        Assert.Equal(rate, funded.Budget);
        Assert.Equal(rate - funded.Spent, funded.Remaining);

        Assert.True(barely.IsOverBudget);
        Assert.Equal(rate, barely.Budget);
        Assert.True(barely.Spent > rate && barely.Spent < rate * 2,
            $"the fixture stopped being over by a little: {barely.Spent} against {rate}");
        Assert.Equal(rate - barely.Spent, barely.Remaining);
        Assert.True(barely.Remaining < 0, "an object over its budget has nothing remaining");

        Assert.True(starved.IsOverBudget);
        Assert.Equal(funded.Spent, starved.Spent);
        Assert.Equal(0, starved.Budget);
    }

    /// <summary>
    /// <b>An object these rules cannot price is a row with no figure, and it does not take the
    /// rest of the ledger down with it.</b>
    ///
    /// <para><b>This is reachable rather than theoretical.</b> A campaign's objects live inside a
    /// payload written by whatever build the GM was running and read by whatever build is open
    /// now, so a feature id this one has never heard of is an ordinary thing to meet — and
    /// <c>CostCalculator</c> throws on one, deliberately, rather than guessing a price. Before the
    /// guard, that exception came out of <c>Ledger</c> and took the GM's whole roster with it:
    /// every other object in the game, and every contributor to it, unreadable because one
    /// feature id was from another build.</para>
    ///
    /// <para><b>The budget survives and is asserted</b>, which is the positive control: a row that
    /// answered null to everything would satisfy "it did not throw" while telling the GM nothing
    /// about the Hero Points their players have put in. And the second object in the fixture is
    /// what proves the ledger kept going rather than stopping at the bad one.</para>
    /// </summary>
    [Fact]
    public void AnObjectTheseRulesCannotPriceIsARowWithNoFigure()
    {
        using var ctx = new RenderContext();

        var fromAnotherBuild = Wing with
        {
            Features = [new SelectedAssetFeature("warp_nacelles")]
        };

        var ledger = CampaignAssets.Ledger(Game(fromAnotherBuild, Roost), ctx.Session.Costs,
        [
            ("m_1", "Bulwark", Who((TheWing, CampaignAssetContribution.Vehicle, 2),
                                   (TheRoost, CampaignAssetContribution.Headquarters, 4))),
        ]);

        Assert.Equal(2, ledger.Count);

        Assert.Null(ledger[0].Spent);
        Assert.Null(ledger[0].Remaining);
        Assert.False(ledger[0].IsOverBudget,
            "an object with no price cannot be over a budget it has not been measured against");

        // The half that still answers: who paid, and what they bought it.
        Assert.Equal(2 * ctx.Session.Rules.Assets.VehiclePointsPerHeroPoint, ledger[0].Budget);
        Assert.Equal(["Bulwark"], ledger[0].Contributors.Select(c => c.Who));

        // And the object after it is priced as though nothing had happened, which is what a
        // caught exception buys over a caught-and-abandoned one.
        Assert.NotNull(ledger[1].Spent);
    }

    /// <summary>
    /// <b>An object whose every feature these rules do have is priced</b> — the positive control
    /// for the test above, without which an implementation that answered null to everything would
    /// pass it.
    /// </summary>
    [Fact]
    public void AnObjectWhoseFeaturesTheseRulesHaveIsPriced()
    {
        using var ctx = new RenderContext();

        var withSensors = Wing with { Features = [new SelectedAssetFeature("sensors")] };

        var line = Assert.Single(CampaignAssets.Ledger(Game(withSensors), ctx.Session.Costs, []));

        // Body 8 + Speed 10 + Sensors at its printed price, all from the rules data.
        Assert.Equal(8 + 10 + ctx.Session.Costs.VehicleFeatureCost(new SelectedAssetFeature("sensors")),
                     line.Spent);
    }

    /// <summary>Nothing to draw for a game that owns nothing shared, and none for no game at all.</summary>
    [Fact]
    public void AGameWithNoSharedObjectHasNoLedger()
    {
        using var ctx = new RenderContext();

        var none = new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false);

        Assert.Empty(CampaignAssets.Ledger(none, ctx.Session.Costs, []));
        Assert.Empty(CampaignAssets.Ledger(null, ctx.Session.Costs, []));
    }

    // ── A contribution to an object that is not there ─────────────────────────

    /// <summary>
    /// <b>A contribution naming an object the game does not have is reported and kept.</b> Dropping
    /// it would edit somebody's character to make a report go away, and it would take the Hero
    /// Points with it — the same answer deleting a campaign gets, where its members go on naming
    /// it.
    /// </summary>
    [Fact]
    public void AContributionToAnObjectTheGameHasNotIsReportedAndKept()
    {
        var sheet = Who((TheWing, CampaignAssetContribution.Vehicle, 2));

        var finding = Assert.Single(CampaignAssets.Orphaned(sheet, Game(Roost)));

        Assert.Equal(CampaignAssets.UnknownAsset, finding.Code);
        Assert.Contains("The Wing", finding.Message, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(sheet.CampaignAssets).HeroPoints);
    }

    /// <summary>
    /// <b>An object the game does have is not reported</b> — the positive control, without which
    /// an implementation that reported every contribution would pass the test above.
    /// </summary>
    [Fact]
    public void AContributionToARealObjectIsNotReported() =>
        Assert.Empty(CampaignAssets.Orphaned(
            Who((TheWing, CampaignAssetContribution.Vehicle, 2)), Game(Wing)));

    /// <summary>
    /// <b>Nothing at all when no campaign resolved, and this is the whole reason the check lives
    /// on a screen rather than in the engine.</b> A member's browser resolves no campaign for a
    /// game that is perfectly alive, because the payload is scoped to the account that owns it —
    /// so reporting an orphan against a null would tell every member of every live game that the
    /// object they funded had been deleted. That is the fault <c>UNKNOWN_CAMPAIGN</c> shipped and
    /// <c>WorthSaying</c> exists to undo, one screen up.
    /// </summary>
    [Fact]
    public void NoCampaignIsNoEvidence() =>
        Assert.Empty(CampaignAssets.Orphaned(
            Who((TheWing, CampaignAssetContribution.Vehicle, 2)), null));

    /// <summary>
    /// <b>A contribution with no id at all is left to the engine</b>, which already reports it as
    /// <c>CAMPAIGN_ASSET_WITHOUT_ID</c> on the character's own findings. Two sentences about one
    /// mistake is one too many, and this one would be the less useful of the two.
    /// </summary>
    [Fact]
    public void ABlankIdIsTheEnginesToReportAndNotThis()
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard" };
        sheet.CampaignAssets.Add(new CampaignAssetContribution("") { HeroPoints = 2 });

        Assert.Empty(CampaignAssets.Orphaned(sheet, Game(Wing)));
    }
}
