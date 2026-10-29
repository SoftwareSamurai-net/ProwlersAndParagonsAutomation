using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>A sheet says when its Resolve is not the whole answer</b> — the owner's ask of 2026-10-29.
/// The exports have one sheet, so they cannot count a companion in: an owner holding Pet/Sidekick
/// prints a marked figure and why, and a companion prints none of its own. The control is the
/// same sheet without the Perk or the link, which prints the bare figure.
/// </summary>
public sealed class CompanionSheetTests(RulesFixture f) : IClassFixture<RulesFixture>
{
    private static readonly DateTime Stamp = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private string Text(CharacterSheet sheet) =>
        CharacterSheetRenderer.RenderText(sheet, f.Rules, f.Costs, f.Derived, f.Validator.Validate(sheet), Stamp);

    private JsonObject Derived(CharacterSheet sheet) =>
        JsonNode.Parse(CharacterSheetRenderer.RenderJson(sheet, f.Rules, f.Costs, f.Derived, f.Validator.Validate(sheet), Stamp))!
            ["derived"]!.AsObject();

    private static string ResolveLine(string text) =>
        text.Split('\n').Single(l => l.TrimStart().StartsWith("Resolve:", StringComparison.Ordinal)).Trim();

    [Fact]
    public void AnOwnersResolveIsMarkedAsWithoutItsCompanion()
    {
        var owner = f.LegalSheet();
        var own = f.Derived.CalculateResolve(owner);

        Assert.Equal($"Resolve: {own}", ResolveLine(Text(owner)));
        Assert.Null(Derived(owner)["resolve_note"]);

        owner.Perks.Add(new SelectedPerk("pet_sidekick", 4, "Talon"));

        Assert.StartsWith($"Resolve: {own}* (* without its companion", ResolveLine(Text(owner)), StringComparison.Ordinal);
        Assert.Contains("without its companion", Derived(owner)["resolve_note"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(own, Derived(owner)["resolve"]!.GetValue<int>());
    }

    [Fact]
    public void ACompanionPrintsNoResolveOfItsOwn()
    {
        var talon = f.LegalSheet();
        talon.Variant = new CharacterVariant("kestrel", CharacterVariant.Companion);

        Assert.Equal("Resolve: none — a companion spends its owner's Resolve (p.55)", ResolveLine(Text(talon)));
        Assert.Contains("spends its owner's Resolve", Derived(talon)["resolve_note"]!.GetValue<string>(), StringComparison.Ordinal);

        // Any other link is not a companion's.
        talon.Variant = new CharacterVariant("kestrel", CharacterVariant.Later);
        Assert.Null(Derived(talon)["resolve_note"]);
    }

    [Fact]
    public void ANewCompanionStartsAtItsOwnersTierAndCap()
    {
        var owner = f.LegalSheet();
        owner.SelectedTierId = "high_level";
        owner.TraitCapRank = 14;

        var talon = Companions.Begin(owner, "kestrel", "Talon");

        Assert.Equal("Talon", talon.Name);
        Assert.Equal("high_level", talon.SelectedTierId);
        Assert.Equal(14, talon.TraitCapRank);
        Assert.Equal(new CharacterVariant("kestrel", CharacterVariant.Companion), talon.Variant);
        Assert.True(Companions.IsCompanion(talon));
        Assert.Empty(talon.Perks);
    }
}
