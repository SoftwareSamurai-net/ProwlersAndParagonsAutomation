using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Immunity is "named and paid for separately" (Ch.2 p.31), so a sheet has to be able to say
/// <em>which</em> immunities were bought — <see cref="SelectedPower.UnitNames"/>. These hold the
/// record, the reader, the validator and both exports to that, and hold the price to not moving.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class NamedUnitTests
{
    private static readonly DateTime Stamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly RulesFixture _f;

    public NamedUnitTests(RulesFixture fixture) => _f = fixture;

    private static SelectedPower Immunity(int units, params string[] names) =>
        new("immunity", 0)
        {
            Units     = units,
            SourceId  = "tech",
            UnitNames = names.Length == 0 ? null : names
        };

    private CharacterSheet With(SelectedPower power)
    {
        var sheet = _f.LegalSheet();
        sheet.SelectedPowers.Add(power);
        return sheet;
    }

    private string Text(CharacterSheet sheet) =>
        CharacterSheetRenderer.RenderText(sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), Stamp);

    private JsonObject Json(CharacterSheet sheet) =>
        JsonNode.Parse(CharacterSheetRenderer.RenderJson(sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), Stamp))!.AsObject();

    private IEnumerable<ValidationIssue> Issues(CharacterSheet sheet, string code) =>
        _f.Validator.Validate(sheet).Issues.Where(i => i.Code == code);

    // ── Which Powers name their units ─────────────────────────────────────────

    /// <summary>
    /// <b>Derived from the Power's own text, not listed.</b> The flag is on exactly the entries
    /// whose description says each unit "is named and paid for separately" — so a second Power
    /// gaining the flag without the words, or the words without the flag, fails here. The count
    /// is the positive control: a match on nothing would pass the equality vacuously.
    /// </summary>
    [Fact]
    public void OnlyThePowersWhoseTextNamesEachUnitAreFlagged()
    {
        var flagged = _f.Rules.Powers.Where(p => p.UnitsAreNamed).Select(p => p.Id).Order().ToList();
        var worded  = _f.Rules.Powers
            .Where(p => p.Description.Contains("named and paid for separately", StringComparison.Ordinal))
            .Select(p => p.Id).Order().ToList();

        Assert.Equal(["immunity"], flagged);
        Assert.Equal(worded, flagged);
        Assert.All(_f.Rules.Powers.Where(p => p.UnitsAreNamed), p => Assert.Equal("per_unit", p.CostType));
    }

    // ── The record and the reader ─────────────────────────────────────────────

    /// <summary>
    /// <b>Names survive a strict round trip, and a Power without them writes what it wrote before
    /// the field existed.</b> Strict because a submitted file is read that way, and the Pros are
    /// carried alongside because they go through the record's constructor — the one the
    /// serializer is told to use — while the names are an init property set after it.
    /// </summary>
    [Fact]
    public void NamesSurviveAStrictRoundTripAndAnUnnamedPowerAddsNoBytes()
    {
        var plain = CharacterSheetJson.Write(With(Immunity(1)));
        Assert.DoesNotContain("UnitNames", plain, StringComparison.Ordinal);
        Assert.Null(Assert.Single(CharacterSheetJson.Read(plain, strict: true)!.SelectedPowers).UnitNames);

        var named = With(Immunity(2, "Toxins", "Fire") with { Pros = [new SelectedProCon("ranged")] });
        var written = CharacterSheetJson.Write(named);
        Assert.Contains("\"UnitNames\":[\"Toxins\",\"Fire\"]", written, StringComparison.Ordinal);

        var back = Assert.Single(CharacterSheetJson.Read(written, strict: true)!.SelectedPowers);
        Assert.Equal(["Toxins", "Fire"], back.UnitNames);
        Assert.Equal(2, back.Units);
        Assert.Equal("ranged", Assert.Single(back.Pros).Id);
    }

    /// <summary>The shape a player sheet already carries, from before there was anywhere to put a name.</summary>
    [Fact]
    public void ASheetWrittenBeforeNamesExistedStillReads()
    {
        const string json = """
            {"SelectedTierId":"standard","SelectedPowers":[{"PowerId":"immunity","Units":1,"SourceId":"tech"}]}
            """;

        var power = Assert.Single(CharacterSheetJson.Read(json, strict: true)!.SelectedPowers);

        Assert.Null(power.UnitNames);
        Assert.Empty(power.BoughtUnitNames());
        Assert.Empty(power.Pros);
    }

    [Fact]
    public void BoughtNamesAreTheFirstUnitsWorthTrimmedWithBlanksLeftOut()
    {
        var power = Immunity(3, " Toxins ", "", "Fire", "Cold");

        Assert.Equal(["Toxins", "Fire"], power.BoughtUnitNames());
    }

    // ── The price ─────────────────────────────────────────────────────────────

    [Fact]
    public void NamingAnImmunityDoesNotChangeWhatItCosts()
    {
        var unnamed = _f.Costs.PowerCost(Immunity(3));
        var named   = _f.Costs.PowerCost(Immunity(3, "Toxins", "Fire", "Cold"));
        var extra   = _f.Costs.PowerCost(Immunity(3, "Toxins", "Fire", "Cold", "Vacuum"));

        Assert.Equal(9, unnamed);
        Assert.Equal(unnamed, named);
        Assert.Equal(unnamed, extra);
    }

    // ── The validator ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A warning, and the character stays legal.</b> Checked against a sheet that is otherwise
    /// clean, so <c>IsValid</c> is saying something about this finding rather than about noise.
    /// </summary>
    [Fact]
    public void AnUnnamedImmunityIsAWarningOnALegalCharacter()
    {
        var sheet = With(Immunity(2, "Toxins"));
        var result = _f.Validator.Validate(sheet);

        var issue = Assert.Single(result.Issues, i => i.Code == "POWER_UNIT_NAMES_BELOW_UNITS");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Power, issue.SubjectKind);
        Assert.Equal("immunity", issue.SubjectId);
        Assert.Equal(1, issue.Value);
        Assert.Equal(2, issue.Limit);
        Assert.Equal("Immunity has 2 immunities and 1 is not named. Each immunity is named and "
            + "paid for separately, so the sheet should say what each one is against.", issue.Message);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(1, new string[0], "Immunity has 1 immunity and it is not named.")]
    [InlineData(3, new string[0], "Immunity has 3 immunities and none of them is named.")]
    [InlineData(3, new[] { "Toxins" }, "Immunity has 3 immunities and 2 are not named.")]
    public void TheWarningSaysHowManyAreUnnamed(int units, string[] names, string opening)
    {
        var issue = Assert.Single(Issues(With(Immunity(units, names)), "POWER_UNIT_NAMES_BELOW_UNITS"));

        Assert.StartsWith(opening, issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryImmunityNamedIsNoFinding()
    {
        var sheet = With(Immunity(2, "Toxins", "Fire"));

        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_BELOW_UNITS"));
        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_EXCEED_UNITS"));
    }

    /// <summary>Determination's Resolve is counted, not named, so there is nothing to ask for.</summary>
    [Fact]
    public void ACountedPowerIsNeverAskedForNames()
    {
        var sheet = With(new SelectedPower("determination", 0) { Units = 2 });

        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_BELOW_UNITS"));
    }

    // ── A Gadget's Powers are named the same way ─────────────────────────────

    /// <summary>
    /// The same <c>CheckUnitNames</c> gate, one budget down: p.94 buys a Gadget's Powers under
    /// the ordinary rules, so an Immunity bought inside one is still "named and paid for
    /// separately" — and the character-only sweep never walked <c>sheet.Gadgets</c>.
    /// </summary>
    private CharacterSheet WithGadgetImmunity(int units, params string[] names)
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 6;
        sheet.Gadgets.Add(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [Immunity(units, names)]
        });
        return sheet;
    }

    [Fact]
    public void AGadgetImmunityBelowUnitsIsAWarningOnTheGadget()
    {
        var sheet = WithGadgetImmunity(2, "Toxins");
        var result = _f.Validator.Validate(sheet);

        var issue = Assert.Single(result.Issues, i => i.Code == "POWER_UNIT_NAMES_BELOW_UNITS");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
        Assert.Equal(1, issue.Value);
        Assert.Equal(2, issue.Limit);
        Assert.StartsWith("Vault Breaker's Immunity has 2 immunities and 1 is not named.",
            issue.Message, StringComparison.Ordinal);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void AGadgetImmunityWithNamesPastTheCountIsAWarningOnTheGadget()
    {
        var sheet = WithGadgetImmunity(2, "Toxins", "Fire", "Cold");
        var result = _f.Validator.Validate(sheet);

        var issue = Assert.Single(result.Issues, i => i.Code == "POWER_UNIT_NAMES_EXCEED_UNITS");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
        Assert.Equal(3, issue.Value);
        Assert.Equal(2, issue.Limit);
        Assert.StartsWith("Vault Breaker's Immunity records 3 names but is bought 2 times.",
            issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGadgetImmunityFullyNamedIsNoFinding()
    {
        var sheet = WithGadgetImmunity(2, "Toxins", "Fire");

        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_BELOW_UNITS"));
        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_EXCEED_UNITS"));
    }

    /// <summary>
    /// A nameless Gadget is skipped the same way <c>CheckQuantities</c>' sweep skips one — there
    /// is nothing to attribute the finding to.
    /// </summary>
    [Fact]
    public void ANamelessGadgetsUnnamedImmunityIsNotReported()
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 6;
        sheet.Gadgets.Add(new BuiltGadget(null!) { Complexity = 3, Powers = [Immunity(2, "Toxins")] });

        Assert.Empty(Issues(sheet, "POWER_UNIT_NAMES_BELOW_UNITS"));
    }

    /// <summary>
    /// A name past the count is one nobody paid for, and the sheet will not print it. Counted by
    /// position, so a blank slot inside the count does not hide the one outside it.
    /// </summary>
    [Fact]
    public void ANamePastTheCountIsAWarning()
    {
        var issue = Assert.Single(Issues(With(Immunity(2, "", "", "Fire")), "POWER_UNIT_NAMES_EXCEED_UNITS"));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(3, issue.Value);
        Assert.Equal(2, issue.Limit);

        // Trailing blank boxes are not names.
        Assert.Empty(Issues(With(Immunity(2, "Toxins", "Fire", " ")), "POWER_UNIT_NAMES_EXCEED_UNITS"));
    }

    // ── The exports ───────────────────────────────────────────────────────────

    [Fact]
    public void TheTextSheetPrintsEachImmunityAndCountsTheUnnamed()
    {
        Assert.Contains("    Immunities: Toxins, Fire\n", Text(With(Immunity(2, "Toxins", "Fire"))).ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("    Immunities: Toxins, 2 unnamed\n", Text(With(Immunity(3, "Toxins"))).ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    /// <summary>The control for the two above: a sheet with no named Power prints no such line.</summary>
    [Fact]
    public void ASheetWithNoNamedPowerPrintsNoNamesLine()
    {
        var sheet = With(new SelectedPower("determination", 0) { Units = 2, SourceId = "innate" });

        Assert.DoesNotContain("Immunities:", Text(sheet), StringComparison.Ordinal);
        Assert.DoesNotContain("Resolves:", Text(sheet), StringComparison.Ordinal);
    }

    [Fact]
    public void TheJsonExportCarriesOnlyTheNamesBought()
    {
        var powers = Json(With(Immunity(2, "Toxins", "", "Vacuum")))["powers"]!.AsArray();
        var immunity = powers.Single(p => p!["id"]!.GetValue<string>() == "immunity")!;

        Assert.Equal(["Toxins"], immunity["unit_names"]!.AsArray().Select(n => n!.GetValue<string>()));
    }
}
