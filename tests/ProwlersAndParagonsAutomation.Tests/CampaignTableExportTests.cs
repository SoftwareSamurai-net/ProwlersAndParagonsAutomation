using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The shape of <c>campaign_table</c> and <c>immortality_cost</c> in the JSON export — a
/// contract, because something other than this repository reads it.</b>
///
/// <para>The encounter server is handed characters and never a campaign: it cannot resolve a
/// campaign id any more than the engine can. So a fight fought with an exported sheet is fought
/// under the book unless the sheet carries its table's rules, and this document is how they
/// travel. A key renamed here is a setting silently un-adopted over there.</para>
///
/// <para><b>The key list is held to the switch list rather than typed out.</b> Writing thirteen
/// key names into a test is thirteen chances to assert the export against itself; what is asserted
/// instead is that every switch <c>CampaignTable</c> has appears under its snake_case name, and
/// that the block holds nothing else. <c>CampaignTableNamesTests</c> already holds that switch list
/// to the one <c>play/</c> reads, so the two guards together run from the simulator to the file.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class CampaignTableExportTests
{
    private readonly RulesFixture _f;

    public CampaignTableExportTests(RulesFixture fixture) => _f = fixture;

    private JsonObject Export(CharacterSheet sheet) =>
        JsonNode.Parse(CharacterSheetRenderer.RenderJson(
            sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), DateTime.UnixEpoch))!.AsObject();

    private static CharacterSheet AtATable()
    {
        var sheet = SampleCharacters.Hero();
        sheet.CampaignId = "g_0000000000000000000000";
        sheet.ImmortalityCost = 9;
        sheet.CampaignTable = new CampaignTable
        {
            FatalDamage     = true,
            SlowHealing     = true,
            RaisedGearLimit = true,
            GearLimitRank   = 12
        };
        return sheet;
    }

    /// <summary>
    /// <b>Every switch is in the block, under its snake_case name, and nothing else is.</b>
    ///
    /// <para>Every one is written including the ones that are off, unlike the text sheet's list: a
    /// document is read by a program, an absent key is indistinguishable from a build that had not
    /// heard of the setting, and "off" is a thing this table decided as much as "on" is.</para>
    /// </summary>
    [Fact]
    public void TheBlockCarriesEverySwitchAndNothingElse()
    {
        var block = Export(AtATable())["campaign_table"]!.AsObject();

        var expected = HouseRuleFormatter.All
            .Select(e => string.Concat(e.Key.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}")))
            .Append("gear_limit_rank")
            .Order(StringComparer.Ordinal)
            .ToList();

        // The control: the switch list is not empty, so the comparison below is a comparison.
        Assert.True(expected.Count >= 11, $"Only {expected.Count} keys were expected.");

        Assert.Equal(expected, block.Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>What each key holds: the switch as set, and the rank beside it.</summary>
    [Fact]
    public void TheBlockSaysWhatTheTableTurnedOn()
    {
        var block = Export(AtATable())["campaign_table"]!.AsObject();

        Assert.True(block["fatal_damage"]!.GetValue<bool>());
        Assert.True(block["slow_healing"]!.GetValue<bool>());
        Assert.True(block["raised_gear_limit"]!.GetValue<bool>());
        Assert.Equal(12, block["gear_limit_rank"]!.GetValue<int>());

        // The ones nobody turned on are written as false rather than left out.
        Assert.False(block["the_drop"]!.GetValue<bool>());
        Assert.False(block["checking_your_swing"]!.GetValue<bool>());
    }

    /// <summary>
    /// <b>A character at no table exports both keys as null, and every figure beside them is
    /// unchanged.</b>
    ///
    /// <para>The second half is the one worth having: a document that gained two keys and moved a
    /// cost would be a change to every reader of this file, and the whole point of a house price
    /// is that it applies only where a table set one.</para>
    /// </summary>
    [Fact]
    public void ACharacterAtNoTableExportsNullsAndNothingElseMoved()
    {
        var plain = Export(SampleCharacters.Hero());

        // Present and null, not absent: a reader distinguishing "this table decided nothing"
        // from "this build had not heard of the field" needs the key to be there.
        Assert.True(plain.ContainsKey("campaign_table"));
        Assert.True(plain.ContainsKey("immortality_cost"));
        Assert.Null(plain["campaign_table"]);
        Assert.Null(plain["immortality_cost"]);

        var atATable = Export(AtATable());

        Assert.Equal(9, atATable["immortality_cost"]!.GetValue<int>());

        // The sample Hero has no Immortality, so its spend is the same figure at either table —
        // and the control below is what stops that being a statement about nothing.
        Assert.Equal(plain["hp_budget"]!["spent"]!.GetValue<int>(),
                     atATable["hp_budget"]!["spent"]!.GetValue<int>());
        Assert.False(SampleCharacters.Hero().HasPower("immortality"));
    }

    /// <summary>
    /// <b>The spend in the document is the table's, for a character that has the Power.</b>
    ///
    /// <para>The positive control for the test above: without it, "the spend did not move" is
    /// satisfied by a renderer that had stopped reading the house price at all.</para>
    /// </summary>
    [Fact]
    public void TheSpendInTheDocumentIsTheTablesPrice()
    {
        var sheet = SampleCharacters.Hero();
        sheet.SelectedPowers.Add(new SelectedPower("immortality", 0));

        var atTheBook = Export(sheet)["hp_budget"]!["spent"]!.GetValue<int>();

        sheet.CampaignId = "g_0000000000000000000000";
        sheet.ImmortalityCost = 12;

        var atTheTable = Export(sheet);

        Assert.Equal(atTheBook + 9, atTheTable["hp_budget"]!["spent"]!.GetValue<int>());

        // And the Power's own row says the same figure, so the two halves of one document agree.
        var row = atTheTable["powers"]!.AsArray()
            .Single(p => p!["id"]!.GetValue<string>() == "immortality")!;

        Assert.Equal(12, row["cost"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>The text sheet prints the house rules, and prints nothing at all for a table that has
    /// decided nothing.</b>
    ///
    /// <para>The second half is what keeps every previously exported <c>.txt</c> byte-identical,
    /// and it is why a block of thirteen "no"s is not what this prints: a heading over settings
    /// that are true of every game tells a reader less than its absence does.</para>
    /// </summary>
    [Fact]
    public void TheTextSheetPrintsWhatTheTableDecidedAndNothingWhenItDecidedNothing()
    {
        string Text(CharacterSheet sheet) => CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        Assert.DoesNotContain("HOUSE RULES", Text(SampleCharacters.Hero()), StringComparison.Ordinal);

        // A table that opened the form and turned nothing on is the same game as one that never
        // opened it, so an all-off block prints nothing either.
        var book = SampleCharacters.Hero();
        book.CampaignId = "g_0000000000000000000000";
        book.CampaignTable = CampaignTable.Book;

        Assert.DoesNotContain("HOUSE RULES", Text(book), StringComparison.Ordinal);

        var printed = Text(AtATable());

        Assert.Contains("HOUSE RULES", printed, StringComparison.Ordinal);
        Assert.Contains("Fatal Damage", printed, StringComparison.Ordinal);
        Assert.Contains("Slow Healing", printed, StringComparison.Ordinal);
        Assert.Contains("Gear Limit 12d", printed, StringComparison.Ordinal);
        Assert.Contains("Immortality: 9 HP", printed, StringComparison.Ordinal);

        // The book's headings, not the property names — every other name in this application is
        // the one the rulebook prints.
        Assert.DoesNotContain("FatalDamage", printed, StringComparison.Ordinal);

        // And nothing the table did not turn on.
        Assert.DoesNotContain("The Drop", printed, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A Gear Limit rank left behind under a switch that is off is not printed as a rule</b> —
    /// but it is still in the document, because a program reading that file applies
    /// <c>raised_gear_limit</c> itself, exactly as <c>TableRules.GearLimit</c> does.
    /// </summary>
    [Fact]
    public void AGearLimitUnderAnUnadoptedSwitchIsCarriedButNotPrinted()
    {
        var sheet = SampleCharacters.Hero();
        sheet.CampaignId = "g_0000000000000000000000";
        sheet.CampaignTable = new CampaignTable { FatalDamage = true, GearLimitRank = 12 };

        var printed = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        Assert.Contains("HOUSE RULES", printed, StringComparison.Ordinal);
        Assert.DoesNotContain("Gear Limit", printed, StringComparison.Ordinal);

        Assert.Equal(12, Export(sheet)["campaign_table"]!["gear_limit_rank"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>Every switch has a printed name, and none of them is the property name.</b>
    ///
    /// <para>A switch with no entry in <see cref="HouseRuleFormatter"/> prints as nothing at all —
    /// the list is simply one shorter, which is the quietest way for a setting to become invisible
    /// to the people it is about. Driven by turning every switch on at once, which is also the
    /// control that the formatter reads them all.</para>
    /// </summary>
    [Fact]
    public void EverySwitchHasAPrintedNameThatIsNotItsPropertyName()
    {
        var everything = new CampaignTable
        {
            ActiveDefensesCost = true, CloseRangePenalty = true, TheDrop = true,
            FatalDamage = true, FriendlyFire = true, HardTargets = true,
            RaisedGearLimit = true, SlowHealing = true, ToughMinions = true,
            WoundPenalties = true, GmAlternativeToSeizingInitiative = true,
            CheckingYourSwing = true, RandomInitiative = true
        };

        var on = HouseRuleFormatter.On(everything);

        Assert.Equal(HouseRuleFormatter.All.Count, on.Count);
        Assert.DoesNotContain(on, n => string.IsNullOrWhiteSpace(n));

        foreach (var (key, name, page) in HouseRuleFormatter.All)
        {
            Assert.NotEqual(key, name);
            Assert.StartsWith("p.", page, StringComparison.Ordinal);
        }

        // And the reverse control: a table playing the book names nothing.
        Assert.Empty(HouseRuleFormatter.On(CampaignTable.Book));
    }
}
