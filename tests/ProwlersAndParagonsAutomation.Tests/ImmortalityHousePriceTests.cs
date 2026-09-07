using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>A table's price for Immortality: charged from the sheet, bounded by the Power's own entry,
/// reported and never repaired.</b>
///
/// <para>Ch.2 p.31 prices the Power at 3 Hero Points and hands the price to the GM — "In a game
/// where Heroes can die, GMs should charge more for this — somewhere between 6 and 12 Hero
/// Points." It is the one number in <c>data/rules/</c> where the book prints a range, and so the
/// one place a campaign's house rule moves what a character costs.</para>
///
/// <para><b>The precedent is the house Trait Cap, exactly.</b> The setting lives on the campaign,
/// is copied onto the sheet when a character joins, and the engine prices from the sheet — never
/// from a campaign it cannot see. What differs is that the cap costs no Hero Points and this
/// does.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ImmortalityHousePriceTests
{
    private readonly RulesFixture _f;

    public ImmortalityHousePriceTests(RulesFixture fixture) => _f = fixture;

    private const string CampaignId = "g_0000000000000000000000";

    private static readonly SelectedPower Immortality = new("immortality", 0);

    /// <summary>A legal Standard-tier character with the Power and a table charging for it.</summary>
    private CharacterSheet Undying(int? housePrice, string? campaignId = CampaignId)
    {
        var sheet = _f.LegalSheet();
        sheet.SelectedPowers.Add(Immortality);
        sheet.CampaignId = campaignId;
        sheet.ImmortalityCost = housePrice;
        return sheet;
    }

    /// <summary>
    /// <b>The book's 3 for a character at no table, and the table's figure for one at a table.</b>
    ///
    /// <para>The control that matters is the first assertion: a test that only checked the house
    /// price would pass over a calculator that had started charging 9 to everybody.</para>
    /// </summary>
    [Fact]
    public void ImmortalityCostsTheBooksThreeUntilATableSaysOtherwise()
    {
        Assert.Equal(3, _f.Costs.PowerCost(Immortality));
        Assert.Equal(3, _f.Costs.PowerCost(Immortality, null));
        Assert.Equal(9, _f.Costs.PowerCost(Immortality, 9));

        // And through the total, which is what a budget is checked against — the figure on a
        // screen and the figure in a verdict have to be one number.
        Assert.Equal(3, _f.Costs.TotalPowersCost(Undying(null)));
        Assert.Equal(9, _f.Costs.TotalPowersCost(Undying(9)));
    }

    /// <summary>
    /// <b>A table's price applies to the Power whose entry hands it the price, and to nothing
    /// else.</b>
    ///
    /// <para>Every other flat-cost Power in the book states a number, so a campaign that re-priced
    /// Immortality re-pricing Armor or Damage Resistance alongside it would be a house rule
    /// nobody wrote. The branch asks the Power for its range before it asks the table for a
    /// figure, and this is what says so.</para>
    /// </summary>
    [Fact]
    public void ATablesPriceDoesNotReachAnyOtherFlatCostPower()
    {
        var others = _f.Rules.Powers
            .Where(p => p.CostType == "flat" && !p.HasCampaignCostRange && p.Id != "specialty")
            .ToList();

        Assert.NotEmpty(others);

        foreach (var power in others)
        {
            var selection = new SelectedPower(power.Id, 0);

            Assert.Equal(_f.Costs.PowerCost(selection), _f.Costs.PowerCost(selection, 9));
        }
    }

    /// <summary>
    /// <b>A price outside the entry's range is an error, and the arithmetic still charges it.</b>
    ///
    /// <para>Reported, never repaired — clamping to the range would be worse here than usual,
    /// because the cost would then look right on every screen while the campaign's setting said
    /// something else.</para>
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(40)]
    public void APriceOutsideTheRangeIsReportedAndStillCharged(int house)
    {
        var sheet = Undying(house);
        var result = _f.Validator.Validate(sheet);

        var issue = Assert.Single(result.Issues, i => i.Code == "IMMORTALITY_COST_OUTSIDE_RANGE");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationSubject.Power, issue.SubjectKind);
        Assert.Equal("immortality", issue.SubjectId);
        Assert.Equal(house, issue.Value);

        // As written. The finding is the repair, and the figure is not quietly moved under it.
        Assert.Equal(house, _f.Costs.TotalPowersCost(sheet));
    }

    /// <summary>
    /// <b>The rulebook's own floor still applies underneath a table's price, and it is the floor
    /// that wins.</b>
    ///
    /// <para>An unranked Power costs at least 1 Hero Point (Ch.2), which is a rule about the
    /// Power and not about the table — so a campaign setting 0 or a negative figure does not
    /// produce a free Power or a Power that pays the character. That is <em>not</em> the engine
    /// repairing the setting: the finding is still reported, the campaign still says what it
    /// says, and what the floor changes is only what the Power comes to.</para>
    ///
    /// <para>Recorded rather than left implicit because it is the one place a price is not
    /// charged as written, and somebody reading the paragraph above would otherwise expect
    /// <c>-4</c>.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void APriceBelowTheRulebookFloorIsReportedAndFlooredRatherThanCharged(int house)
    {
        var sheet = Undying(house);

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i.Code == "IMMORTALITY_COST_OUTSIDE_RANGE");

        Assert.Equal(1, _f.Costs.TotalPowersCost(sheet));
    }

    /// <summary>
    /// <b>Vulnerable still takes one off, and it takes it off the table's price.</b>
    ///
    /// <para>Immortality prints one Con of its own — Vulnerable, a flat −1 — and a Con is a change
    /// to what <em>this</em> Power costs, so it applies to whatever the Power costs here. Two ways
    /// of getting that wrong both compile: charging the book's 3 and then subtracting (2 at every
    /// table), or applying the table's price and dropping the Con (the table's figure exactly, at
    /// every price). Both are one line, neither is visible on a screen, and a character sheet is
    /// the only place the difference shows.</para>
    ///
    /// <para><b>The book's own figure is the control</b>, because a test that only checked the
    /// house prices would pass over a calculator that had stopped reading the Con at all.</para>
    /// </summary>
    [Theory]
    [InlineData(null, 2)]
    [InlineData(6, 5)]
    [InlineData(9, 8)]
    [InlineData(12, 11)]
    public void TheOneConImmortalityPrintsComesOffTheTablesPrice(int? house, int expected)
    {
        var vulnerable = new SelectedPower("immortality", 0, [], [new SelectedProCon("vulnerable")]);

        Assert.Equal(expected, _f.Costs.PowerCost(vulnerable, house));
    }

    /// <summary>
    /// <b>And the unranked floor is still underneath the Con as well as underneath the price.</b>
    ///
    /// <para><c>APriceBelowTheRulebookFloorIsReportedAndFlooredRatherThanCharged</c> is this one
    /// field over: the floor is applied to the Power's total, so it catches a Con that has taken
    /// the last point off a price the table had already set low. A table charging 1 with the Con
    /// bought is the case where the two meet, and it comes to 1 rather than 0.</para>
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void ThePriceAndTheConTogetherStillCannotGoBelowTheUnrankedFloor(int house)
    {
        var vulnerable = new SelectedPower("immortality", 0, [], [new SelectedProCon("vulnerable")]);

        Assert.Equal(1, _f.Costs.PowerCost(vulnerable, house));
    }

    /// <summary>
    /// Both ends of the range are inside it, and nothing between them is reported. The ends come
    /// from the data rather than being typed here, for the reason the data carries them at all.
    /// </summary>
    [Fact]
    public void EveryPriceTheEntryPermitsIsAccepted()
    {
        var power = _f.Rules.GetPower("immortality")!;

        for (var house = power.CampaignCostMin!.Value; house <= power.CampaignCostMax!.Value; house++)
        {
            var result = _f.Validator.Validate(Undying(house));

            Assert.DoesNotContain(result.Issues, i => i.Code.StartsWith("IMMORTALITY_", StringComparison.Ordinal));
            Assert.Equal(house, _f.Costs.TotalPowersCost(Undying(house)));
        }
    }

    /// <summary>
    /// <b>A house price on a character that belongs to no game is reported — in <c>web/</c>, not
    /// here.</b>
    ///
    /// <para>A house price is a table's, so a sheet carrying one and naming no game has been
    /// hand-edited or has left a campaign without the price going with it. Saying so means reading
    /// the field that names the game, which no rules code may read — <c>PresentationFlagsTests</c>
    /// bars it outright, and the guard caught this check the first time it was written into the
    /// validator. It is <c>CampaignJoin.Inspect</c> that reports it, and
    /// <c>CampaignHouseRuleFindingTests</c> in the web suite is where that is driven.</para>
    ///
    /// <para>What is asserted here is the half this class is responsible for: <b>the engine says
    /// nothing about it either way</b>, so the two are not both reporting one fault.</para>
    /// </summary>
    [Fact]
    public void TheEngineSaysNothingAboutAHousePriceBelongingToNoGame()
    {
        var result = _f.Validator.Validate(Undying(9, campaignId: null));

        Assert.DoesNotContain(result.Issues,
            i => i.Code.StartsWith("IMMORTALITY_", StringComparison.Ordinal));

        // The control: the same character at 99 is reported, so the silence above is about the
        // membership question and not about this check having stopped running.
        Assert.Contains(_f.Validator.Validate(Undying(99, campaignId: null)).Issues,
            i => i.Code == "IMMORTALITY_COST_OUTSIDE_RANGE");
    }

    /// <summary>
    /// <b>A character carrying no house price is told nothing, which is every character stored
    /// before this existed.</b>
    /// </summary>
    [Fact]
    public void ACharacterWithNoHousePriceIsToldNothing()
    {
        foreach (var campaignId in new[] { CampaignId, null })
        {
            var result = _f.Validator.Validate(Undying(null, campaignId));

            Assert.DoesNotContain(result.Issues,
                i => i.Code.StartsWith("IMMORTALITY_", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// <b>The positive control for the whole slice: nothing that already exists moved by a
    /// point.</b>
    ///
    /// <para>The twenty published Heroes and both sample characters are priced against pages of
    /// the rulebook elsewhere in this suite, and none of them is in a campaign — so a change to
    /// <c>PowerCost</c> that had started charging a house price to everybody, or had lost the
    /// book's price on the way past, moves one of these figures. Threading a new argument through
    /// eleven call sites is exactly the change that does that quietly.</para>
    ///
    /// <para><b>It asserts the total rather than only the Power</b>, because the fault worth
    /// catching is a figure moving somewhere a reader is looking, and the total is where a budget
    /// is checked.</para>
    /// </summary>
    [Fact]
    public void NoPublishedCharacterMovedByAPoint()
    {
        var sheets = PrebuiltHeroes.All
            .Select(h => (Name: h.Name, Sheet: PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, h)))
            .Append((Name: "the sample Hero", Sheet: SampleCharacters.Hero()))
            .Append((Name: "the sample Villain", Sheet: SampleCharacters.Villain()))
            .ToList();

        Assert.Equal(22, sheets.Count);

        foreach (var (name, sheet) in sheets)
        {
            // None of them is at a table, which is the fact that makes the rest of this suite's
            // rulebook-checked figures still true. A join that had started writing a price onto
            // characters that never asked for one shows up here first.
            Assert.True(sheet.ImmortalityCost is null,
                $"{name} has picked up a house price for Immortality from somewhere.");
            Assert.True(sheet.CampaignTable is null,
                $"{name} has picked up a table's optional rules from somewhere.");

            // What it costs, against what it costs at a table that has re-priced the Power. The
            // two agree exactly when the character does not have Immortality — an invariance that
            // is true for a stated reason rather than by luck, and one that a `PowerCost` which
            // had stopped reading the price altogether could not satisfy in both directions.
            var atTheBook = _f.Costs.TotalCost(sheet);

            sheet.ImmortalityCost = 12;
            var atTheTable = _f.Costs.TotalCost(sheet);
            sheet.ImmortalityCost = null;

            if (sheet.HasPower("immortality"))
            {
                Assert.True(atTheTable == atTheBook + 9,
                    $"{name} has Immortality at the book's 3 HP, so a table charging 12 costs "
                    + $"them 9 more — {atTheBook} became {atTheTable}.");
            }
            else
            {
                Assert.True(atTheTable == atTheBook,
                    $"{name} does not have Immortality and a table's price for it moved their "
                    + $"total from {atTheBook} to {atTheTable}, so the price is reaching Powers "
                    + "whose entries never handed it one.");
            }
        }

        // The control on the control. "Nothing moved" over twenty-two characters none of which
        // has the Power is an assertion about no arithmetic at all — and this is the sort of set
        // that quietly becomes that when a fixture is rewritten.
        Assert.Contains(sheets, s => s.Sheet.HasPower("immortality"));
        Assert.Contains(sheets, s => !s.Sheet.HasPower("immortality"));
    }

    /// <summary>
    /// <b>The house price is a Hero Point cost, so it moves the budget.</b>
    ///
    /// <para>This is the whole difference between this setting and the house Trait Cap beside it: a
    /// cap changes what is legal and what Resolve comes to, and costs nothing. A table charging 9
    /// for Immortality spends 6 more of every such character's Hero Points, which is what makes it
    /// a thing a GM has to be shown under a character on the approval screen.</para>
    /// </summary>
    [Fact]
    public void ATablesPriceMovesWhatTheCharacterCosts()
    {
        var book = Undying(null);
        var table = Undying(9);

        Assert.Equal(_f.Costs.TotalCost(book) + 6, _f.Costs.TotalCost(table));
    }
}
