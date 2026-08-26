using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The rendered half of validation-on-the-row: a broken rule actually shows up on the row that
/// broke it, in the tabs that edit Abilities, Talents, Powers, Perks and Flaws.
///
/// <para><b>Assert on <c>TextContent</c>, never on markup with the tags stripped out</b> — see
/// <c>SheetRenderTests.Rendered</c> and the rule it exists to hold. Every assertion below reads
/// a specific row's own subtree, found the way <c>RankWordTests.Row</c> already finds one, and
/// reads <c>TextContent</c> off it.</para>
///
/// <para>The routing itself — which finding lands on which row — is <see cref="SheetFindingsTests"/>'s
/// job. This file is only "does the component actually draw what it was handed".</para>
/// </summary>
public sealed class RowFindingRenderTests
{
    /// <summary>The <c>.row-findings</c> block belonging to one named Ability or Talent's row, or null.</summary>
    private static AngleSharp.Dom.IElement? TraitFindingsFor(
        IEnumerable<AngleSharp.Dom.IElement> rankRows, string traitName)
    {
        var row = rankRows.FirstOrDefault(r => r.QuerySelector(".rank-name")?.TextContent.TrimStart()
            .StartsWith(traitName, StringComparison.Ordinal) == true);

        Assert.True(row is not null, $"No rank row for {traitName}.");
        return row!.NextElementSibling is { } sib && sib.ClassList.Contains("row-findings") ? sib : null;
    }

    /// <summary>
    /// A Trait over the Trait Cap shows an error on its own row, visibly — not only through
    /// <c>aria-describedby</c>, which is the tooltip shape this design deliberately avoids.
    /// </summary>
    [Fact]
    public void ATraitOverTheCapShowsAnErrorOnItsOwnRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        foreach (var a in ctx.Services.GetRequiredService<RulesRepository>().Abilities)
            sheet.AbilityRanks[a.Id] = 1;
        sheet.AbilityRanks["might"] = 13;

        var page = ctx.Render<AbilitiesTab>();
        var rows = page.FindAll(".rank-row");

        var might = TraitFindingsFor(rows, "Might");
        Assert.NotNull(might);
        Assert.Contains("above the Trait Cap of 12d", might!.TextContent, StringComparison.Ordinal);

        // The word "Error" is really on screen, not only in an aria attribute — see
        // RowFinding.razor's rule that error and warning are never colour alone.
        var kind = might.QuerySelector(".finding-kind");
        Assert.NotNull(kind);
        Assert.Equal("Error", kind!.TextContent);
        Assert.Contains("error", might.QuerySelector(".finding")!.ClassList);

        // Positive control and absence, on the same render: Toughness carries no finding.
        var toughness = TraitFindingsFor(rows, "Toughness");
        Assert.Null(toughness);
    }

    /// <summary>A Trait below its package floor shows on its own Talent row.</summary>
    [Fact]
    public void ATraitBelowItsPackageFloorShowsOnItsOwnTalentRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPackageId = "civilian_package";
        foreach (var t in ctx.Services.GetRequiredService<RulesRepository>().Talents)
            sheet.TalentRanks[t.Id] = 2;
        sheet.TalentRanks["academics"] = 1;

        var page = ctx.Render<TalentsTab>();
        var rows = page.FindAll(".rank-row");

        var academics = TraitFindingsFor(rows, "Academics");
        Assert.NotNull(academics);
        Assert.Contains("below the", academics!.TextContent, StringComparison.Ordinal);

        var streetwise = TraitFindingsFor(rows, "Streetwise");
        Assert.Null(streetwise);
    }

    /// <summary>
    /// A Power with ranks bought against a rankless entry shows on its own row on the Powers
    /// tab — <c>ChosenRow</c>'s <c>Findings</c> parameter, exercised end to end.
    /// </summary>
    [Fact]
    public void APowerWithNoRankToBuyShowsOnItsOwnPowersRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("lightning_reflexes", 2, [], []));

        var page = ctx.Render<PowersTab>();
        var li = page.FindAll(".chosen > li")
            .FirstOrDefault(row => row.TextContent.Contains("Lightning Reflexes", StringComparison.Ordinal));

        Assert.True(li is not null, "No row for Lightning Reflexes.");
        var finding = li!.QuerySelector(".row-findings .finding");
        Assert.NotNull(finding);
        Assert.Contains("has no rank to buy", finding!.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Pro not applicable to its Power shows on that Power's row — the case that has to be
    /// routed by <c>OwnerId</c> rather than by the finding's own <c>SubjectKind</c>, and the
    /// rendered proof that the routing reaches the screen.
    /// </summary>
    [Fact]
    public void AProNotApplicableToItsPowerShowsOnThatPowersRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3, [new SelectedProCon("ranged")], []));

        var page = ctx.Render<PowersTab>();
        var li = page.FindAll(".chosen > li")
            .FirstOrDefault(row => row.TextContent.Contains("Armor", StringComparison.Ordinal));

        Assert.True(li is not null, "No row for Armor.");
        var findings = li!.QuerySelectorAll(".row-findings .finding").Select(f => f.TextContent).ToList();

        Assert.Contains(findings, t => t.Contains("cannot be applied to", StringComparison.Ordinal));
    }

    /// <summary>The same Pro carried twice on one Power shows on that Power's row.</summary>
    [Fact]
    public void ADuplicateProShowsOnItsPowersRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower(
            "armor", 3, [new SelectedProCon("resisted"), new SelectedProCon("resisted")], []));

        var page = ctx.Render<PowersTab>();
        var li = page.FindAll(".chosen > li")
            .FirstOrDefault(row => row.TextContent.Contains("Armor", StringComparison.Ordinal));

        Assert.True(li is not null, "No row for Armor.");
        var findings = li!.QuerySelectorAll(".row-findings .finding").Select(f => f.TextContent).ToList();

        Assert.Contains(findings, t => t.Contains("more than once", StringComparison.Ordinal));
    }

    /// <summary>A per-unit Perk bought with no units shows on its own Perks row.</summary>
    [Fact]
    public void APerkWithNoUnitsShowsOnItsOwnPerksRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.Perks.Add(new SelectedPerk("contacts", 0));

        var page = ctx.Render<PerksTab>();
        var li = page.FindAll(".chosen > li")
            .FirstOrDefault(row => row.TextContent.Contains("Contacts", StringComparison.Ordinal));

        Assert.True(li is not null, "No row for Contacts.");
        var finding = li!.QuerySelector(".row-findings .finding");
        Assert.NotNull(finding);
        Assert.Contains("priced by the unit", finding!.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Power missing its Source shows on its own row as a warning, printed and styled
    /// differently from an error — never colour alone.
    /// </summary>
    [Fact]
    public void APowerMissingItsSourceShowsAsAWarningNotAnErrorOnItsRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3, [], []));

        var page = ctx.Render<PowersTab>();
        var li = page.FindAll(".chosen > li")
            .FirstOrDefault(row => row.TextContent.Contains("Armor", StringComparison.Ordinal));

        Assert.True(li is not null, "No row for Armor.");
        var finding = li!.QuerySelector(".row-findings .finding");
        Assert.NotNull(finding);

        // Warning, not error: the class, the visible word, and the sentence all say so.
        Assert.Contains("warning", finding!.ClassList);
        Assert.DoesNotContain("error", finding.ClassList);
        Assert.Equal("Warning", finding.QuerySelector(".finding-kind")!.TextContent);
        Assert.Contains("no Source recorded", finding.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A row nothing is wrong with draws no finding at all — the absence half of the design,
    /// with the positive control alongside it on the same sheet (the Trait Cap test above, and
    /// this one, both carry both halves; this one is the plainest statement of it on its own).
    /// </summary>
    [Fact]
    public void ALegalCharacterShowsNoFindingsOnAnyAbilityRow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPackageId = "hero_package";
        foreach (var a in ctx.Services.GetRequiredService<RulesRepository>().Abilities)
            sheet.AbilityRanks[a.Id] = 3;
        foreach (var t in ctx.Services.GetRequiredService<RulesRepository>().Talents)
            sheet.TalentRanks[t.Id] = 2;

        var page = ctx.Render<AbilitiesTab>();

        Assert.Empty(page.FindAll(".row-findings"));
    }
}
