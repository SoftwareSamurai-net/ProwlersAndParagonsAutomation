using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The rules ask the player to write something in forty places, and every one of them now has
/// a field: Perks and Flaws had <c>NarrativeDetail</c>; a Pro, a Con and a Power have
/// <c>Detail</c> since the owner's ask of 2026-09-30.
///
/// <para><b>What is held here is the engine's half</b> — that the field round-trips, that it
/// never changes what a character costs, that it reaches both exports and the Ability source
/// line, and that a character written before the field existed writes back byte for byte. The
/// editors that ask for it are held in the browser suite (<c>NarrativeDetailEditorTests</c>).</para>
/// </summary>
public sealed class NarrativeDetailTests : IClassFixture<RulesFixture>
{
    private readonly RulesFixture _f;

    public NarrativeDetailTests(RulesFixture fixture) => _f = fixture;

    /// <summary>
    /// Every entry that asks in its prose now asks in a field. The three added on 2026-09-30 are
    /// named, with the count of generic Pros and Cons beside them as the control that the data
    /// this reads is the shipped data.
    /// </summary>
    [Fact]
    public void TheEntriesThatAskInProseAskInAField()
    {
        Assert.NotNull(_f.Rules.GetPower("expertise")!.NarrativeConstraint);
        Assert.NotNull(_f.Rules.GetPower("animation")!.NarrativeConstraint);
        Assert.NotNull(_f.Rules.GetPower("immortality")!.PowerCons.Single(c => c.Id == "vulnerable").NarrativeConstraint);

        Assert.Equal(2, _f.Rules.Powers.Count(p => p.NarrativeConstraint is not null));
        Assert.Equal(11, _f.Rules.Cons.Count(c => c.NarrativeConstraint is not null));
        Assert.Equal(3, _f.Rules.Pros.Count(p => p.NarrativeConstraint is not null));
    }

    /// <summary>
    /// The field goes round through the strict reader and back, on a Pro, a Con and a Power —
    /// and a sheet that never wrote one serialises without the key, which is what keeps every
    /// stored character before this field byte-identical.
    /// </summary>
    [Fact]
    public void ADetailRoundTripsAndAnAbsentOneWritesNothing()
    {
        var sheet = ASheet();

        var json = CharacterSheetJson.Write(sheet);
        var read = CharacterSheetJson.Read(json, strict: true)!;

        var expertise = read.SelectedPowers.Single(p => p.PowerId == "expertise");
        Assert.Equal("Firearms", expertise.Detail);
        Assert.Equal("only under an open sky", read.SelectedPowers.Single(p => p.PowerId == "blast")
            .Cons.Single(c => c.Id == "conditional").Detail);
        Assert.Equal("plate armour", read.AbilityModifiers["might"].Single().Detail);

        // The control: a character with nothing written carries no such key at all.
        var bare = CharacterSheetJson.Write(new CharacterSheet
        {
            SelectedTierId = "standard",
            SelectedPowers = { new SelectedPower("blast", 4, [], [new SelectedProCon("conditional", "often_works")]) },
        });
        Assert.DoesNotContain("Detail", bare, StringComparison.Ordinal);
    }

    /// <summary>Words cost nothing: the same sheet with and without them prices the same.</summary>
    [Fact]
    public void ADetailNeverMovesTheSpend()
    {
        var with = ASheet();
        var without = CharacterSheetJson.Read(CharacterSheetJson.Write(with), strict: true)!;

        without.SelectedPowers[0] = without.SelectedPowers[0] with { Detail = null };
        without.SelectedPowers[1] = without.SelectedPowers[1] with
        {
            Cons = [.. without.SelectedPowers[1].Cons.Select(c => c with { Detail = null })]
        };
        without.AbilityModifiers["might"] = [.. without.AbilityModifiers["might"].Select(c => c with { Detail = null })];

        Assert.Equal(_f.Costs.TotalCost(without), _f.Costs.TotalCost(with));
    }

    /// <summary>
    /// Both exports carry the words. The text export puts them after the option's key and under
    /// the Power; the JSON export has a <c>detail</c> on each Pro, Con and Power.
    /// </summary>
    [Fact]
    public void BothExportsCarryTheWords()
    {
        var sheet = ASheet();

        var text = CharacterSheetRenderer.RenderText(sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("Detail: Firearms", text, StringComparison.Ordinal);
        Assert.Contains("conditional:often_works (only under an open sky)", text, StringComparison.Ordinal);

        var json = JsonNode.Parse(CharacterSheetRenderer.RenderJson(sheet, _f.Rules, _f.Costs, _f.Derived,
            _f.Validator.Validate(sheet), new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)))!;

        var powers = json["powers"]!.AsArray();
        Assert.Equal("Firearms", powers.Single(p => p!["id"]!.GetValue<string>() == "expertise")!["detail"]!.GetValue<string>());
        var blast = powers.Single(p => p!["id"]!.GetValue<string>() == "blast")!;
        Assert.Equal("only under an open sky", blast["cons"]!.AsArray().Single()!["detail"]!.GetValue<string>());
    }

    /// <summary>
    /// The Ability source line prints the book's own shape — <c>(Item: armor)</c> — where the
    /// player wrote what the item is, and the bare <c>(Item)</c> it always printed where they did
    /// not. This closes the gap <c>docs/guide/rules-engine.md</c> recorded for a long time.
    /// </summary>
    [Fact]
    public void TheAbilitySourceLinePrintsTheItemsName()
    {
        var sheet = ASheet();
        var grouping = new SourceGrouping(_f.Rules);

        static IReadOnlyList<string> Lines(SourceGrouping g, CharacterSheet s) =>
            g.GroupBySource(s).Single(x => x.Source?.Id == "tech").TraitLines;

        Assert.Contains(Lines(grouping, sheet), l => l.Contains("(Item: plate armour)", StringComparison.Ordinal));

        sheet.AbilityModifiers["might"] = [new SelectedProCon("item")];
        Assert.Contains(Lines(grouping, sheet), l => l.EndsWith("(Item)", StringComparison.Ordinal));
    }

    private static CharacterSheet ASheet() => new()
    {
        SelectedTierId = "standard",
        Name = "Quill",
        AbilityRanks = { ["might"] = 6, ["agility"] = 4 },
        AbilitySources = { ["might"] = "tech" },
        AbilityModifiers = { ["might"] = [new SelectedProCon("item") { Detail = "plate armour" }] },
        SelectedPowers =
        {
            new SelectedPower("expertise", 2) { BaselineTraitId = "agility", Detail = "Firearms", SourceId = "trained" },
            new SelectedPower("blast", 4, [], [new SelectedProCon("conditional", "often_works") { Detail = "only under an open sky" }])
            {
                SourceId = "tech"
            },
        },
    };
}
