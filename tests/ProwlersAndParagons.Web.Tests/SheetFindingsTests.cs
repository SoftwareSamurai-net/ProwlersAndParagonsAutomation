using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <see cref="SheetFindings"/> routes a real <see cref="ValidationResult"/> — the one
/// <see cref="CharacterValidator"/> actually returns for the shipped rules — to the row that
/// broke the rule. No rendering here: these test the routing alone, against
/// <c>ValidationIssue.SubjectKind</c>/<c>SubjectId</c>/<c>OwnerId</c>, which is the whole of
/// what <see cref="SheetFindings"/> reads. The rendered half — that a component actually shows
/// what this routes to it — is in <see cref="RowFindingRenderTests"/>.
/// </summary>
public sealed class SheetFindingsTests
{
    private static RulesRepository Rules(RenderContext ctx) =>
        ctx.Services.GetRequiredService<RulesRepository>();

    /// <summary>
    /// A Trait over the Trait Cap lands on its own Ability's row, and nowhere else — asserted
    /// with the positive control alongside the absence: a sheet where every other Ability is
    /// legal reports nothing for any of them.
    /// </summary>
    [Fact]
    public void ATraitOverTheCapRoutesToItsOwnAbilityAndNoOther()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        foreach (var a in Rules(ctx).Abilities) sheet.AbilityRanks[a.Id] = 1;
        sheet.AbilityRanks["might"] = 13; // Standard's Trait Cap is 12d.

        var result = ctx.Session.Validate();

        var might = SheetFindings.ForAbility(result, "might");
        Assert.Contains(might, i => i.Code == "TRAIT_ABOVE_CAP");

        var toughness = SheetFindings.ForAbility(result, "toughness");
        Assert.Empty(toughness);
    }

    /// <summary>A Trait below its package floor lands on its own Talent's row.</summary>
    [Fact]
    public void ATraitBelowItsPackageFloorRoutesToItsOwnTalent()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPackageId = "civilian_package"; // grants 2d in every Talent.

        foreach (var t in Rules(ctx).Talents) sheet.TalentRanks[t.Id] = 2;
        sheet.TalentRanks["academics"] = 1; // above the 1d floor, below the package's 2d.

        var result = ctx.Session.Validate();

        var academics = SheetFindings.ForTalent(result, "academics");
        Assert.Contains(academics, i => i.Code == "TRAIT_BELOW_PACKAGE");

        var streetwise = SheetFindings.ForTalent(result, "streetwise");
        Assert.Empty(streetwise);
    }

    /// <summary>
    /// A Power with ranks bought against a <c>max_rank: 0</c> entry lands on that Power's row.
    /// Lightning Reflexes is one of the two named in CLAUDE.md for exactly this shape.
    /// </summary>
    [Fact]
    public void ARankedPurchaseOnARanklessPowerRoutesToThatPower()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("lightning_reflexes", 2, [], []));

        var result = ctx.Session.Validate();

        var lr = SheetFindings.ForPower(result, "lightning_reflexes");
        Assert.Contains(lr, i => i.Code == "POWER_HAS_NO_RANK");
    }

    /// <summary>
    /// A Pro not applicable to the Power it is on is filed at <c>ValidationSubject.Character</c>
    /// with the Power's id as <c>OwnerId</c>, not at <c>ValidationSubject.Power</c> — this is the
    /// case <see cref="SheetFindings.ForPower"/> exists to still find. Armor is Self range; the
    /// generic "ranged" Pro applies only to Touch and Zone Powers (pros.json).
    /// </summary>
    [Fact]
    public void AProNotApplicableToThePowerRoutesToThatPowerByOwnerId()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3, [new SelectedProCon("ranged")], []));

        var result = ctx.Session.Validate();
        var issue = Assert.Single(result.Issues, i => i.Code == "PRO_NOT_APPLICABLE");

        // The finding itself is filed at Character, not Power — the property under test is
        // that ForPower still finds it via OwnerId.
        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal("armor", issue.OwnerId);

        Assert.Contains(SheetFindings.ForPower(result, "armor"), i => i.Code == "PRO_NOT_APPLICABLE");
    }

    /// <summary>The same Pro carried twice by one Power routes to that Power.</summary>
    [Fact]
    public void ADuplicateProRoutesToItsPower()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower(
            "armor", 3, [new SelectedProCon("resisted"), new SelectedProCon("resisted")], []));

        var result = ctx.Session.Validate();

        Assert.Contains(SheetFindings.ForPower(result, "armor"), i => i.Code == "DUPLICATE_PRO");
    }

    /// <summary>
    /// A per-unit Perk bought with no units routes to that Perk, via the id-only match
    /// <see cref="SheetFindings.ForPerk"/> uses because <c>ValidationSubject</c> has no Perk
    /// case.
    /// </summary>
    [Fact]
    public void APerkBoughtWithNoUnitsRoutesToThatPerk()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.Perks.Add(new SelectedPerk("contacts", 0));

        var result = ctx.Session.Validate();

        Assert.Contains(SheetFindings.ForPerk(result, "contacts"), i => i.Code == "PER_UNIT_WITHOUT_UNITS");
    }

    /// <summary>A Power with no Source recorded routes to that Power, as a warning.</summary>
    [Fact]
    public void APowerMissingItsSourceRoutesToThatPowerAsAWarning()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPowers.Add(new SelectedPower("armor", 3, [], []));

        var result = ctx.Session.Validate();

        var issue = Assert.Single(
            SheetFindings.ForPower(result, "armor"), i => i.Code == "POWER_WITHOUT_SOURCE");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    /// <summary>
    /// Findings that belong to no single row — the budget and an unset tier — are found by
    /// none of <see cref="SheetFindings"/>' methods, on the same sheet where a routable finding
    /// exists. The budget strip already carries the first; the review step carries both. This
    /// is the deliberate half of the design, not an oversight: a caller of these methods sees
    /// only what belongs on a row.
    /// </summary>
    [Fact]
    public void FindingsWithNoSingleSubjectAreNeverRoutedToARow()
    {
        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";

        // Every Ability and every Talent at the Trait Cap (12d): 6×12 + 12×12 = 216 HP at
        // 1 HP/rank with no package discount, well past Standard's 125 — and exactly at the
        // cap rather than above it, so this carries no TRAIT_ABOVE_CAP or TRAIT_BELOW_MINIMUM
        // finding to confuse the result with.
        foreach (var a in Rules(ctx).Abilities) sheet.AbilityRanks[a.Id] = 12;
        foreach (var t in Rules(ctx).Talents) sheet.TalentRanks[t.Id] = 12;

        var result = ctx.Session.Validate();
        Assert.Contains(result.Issues, i => i.Code == "HP_BUDGET_EXCEEDED");

        foreach (var ability in Rules(ctx).Abilities)
            Assert.DoesNotContain(
                SheetFindings.ForAbility(result, ability.Id), i => i.Code == "HP_BUDGET_EXCEEDED");

        foreach (var talent in Rules(ctx).Talents)
            Assert.DoesNotContain(
                SheetFindings.ForTalent(result, talent.Id), i => i.Code == "HP_BUDGET_EXCEEDED");
    }
}
