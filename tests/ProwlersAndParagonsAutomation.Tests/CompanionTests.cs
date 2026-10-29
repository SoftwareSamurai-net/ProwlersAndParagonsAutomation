using System.Text.Json;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Ch.2 p.55's Pet/Sidekick: a companion is a second character linked to its owner, built on 10
/// Hero Points per unit the owner bought, held to the owner's Trait Cap, and counted in the
/// owner's Resolve. <see cref="Companions"/> checks the pair; <see cref="CharacterValidator"/>
/// checks what one sheet can say alone.
///
/// <para><b>The positive control is a pair that passes</b>: Kestrel, all eighteen Traits at 1d,
/// with Pet/Sidekick at 4, and Talon, the same with Agility 9 and the Enemy Flaw. Every negative
/// case is that pair with one thing changed.</para>
/// </summary>
public sealed class CompanionTests(RulesFixture f) : IClassFixture<RulesFixture>
{
    private Companions Companions => new(f.Rules, f.Costs, f.Derived);

    private const string PetSidekick = "pet_sidekick";

    private CharacterSheet Owner(int units = 4)
    {
        var sheet = f.LegalSheet();
        sheet.Name = "Kestrel";
        if (units > 0) sheet.Perks.Add(new SelectedPerk(PetSidekick, units, "Talon, a red-tailed hawk"));
        return sheet;
    }

    private CharacterSheet Companion(string owner = "kestrel", string name = "Talon")
    {
        var sheet = f.LegalSheet();
        sheet.Name = name;
        sheet.AbilityRanks["agility"] = 9;
        sheet.Flaws.Add(new SelectedFlaw("enemy"));
        sheet.Variant = new CharacterVariant(owner, CharacterVariant.Companion);
        return sheet;
    }

    private CompanionFamily Family(params RosterEntry[] roster) => Assert.Single(Companions.Families(roster));

    private static IEnumerable<string> Codes(CompanionFamily family) => family.Issues.Select(i => i.Code);

    // ── The positive control ──────────────────────────────────────────────

    [Fact]
    public void ALegalPairPassesAndTheCompanionGetsTenHeroPointsAUnit()
    {
        var owner = new RosterEntry("kestrel", Owner());
        var talon = new RosterEntry("talon", Companion());

        var family = Family(owner, talon);

        Assert.Empty(family.Issues);
        Assert.Same(owner, family.Owner);
        Assert.Equal([talon], family.Members);
        Assert.Equal(40, family.Budget["talon"]);
        Assert.Equal(f.Costs.TotalCost(talon.Sheet), family.Spent["talon"]);
        Assert.True(family.Spent["talon"] is > 0 and <= 40, $"the fixture must fit its budget: {family.Spent["talon"]}");
    }

    [Theory]
    [InlineData(CharacterVariant.Later)]
    [InlineData(CharacterVariant.AlternateForm)]
    public void AnotherKindOfLinkIsNotACompanion(string kind)
    {
        var talon = Companion();
        talon.Variant = new CharacterVariant("kestrel", kind);

        var family = Family(new("kestrel", Owner()), new("talon", talon));

        Assert.Empty(family.Members);
        Assert.Equal(["PET_SIDEKICK_NO_COMPANION"], Codes(family));
    }

    [Fact]
    public void ARosterWithNoOwnerAndNoCompanionHasNoFamilies() =>
        Assert.Empty(Companions.Families([new("kestrel", Owner(units: 0)), new("other", f.LegalSheet())]));

    // ── The owner's Resolve ───────────────────────────────────────────────

    /// <summary>
    /// "If your companion has higher Trait ranks than you, use their Traits", and "if your
    /// companion has Flaws, you earn Resolve as if that character were a Hero". Kestrel alone:
    /// highest rank 1d against a 12d cap, (12 − 1) × 2 = 22, and a Regular Flaw adds nothing.
    /// With Talon: Agility 9, so (12 − 9) × 2 = 6, plus 1 for Talon's Enemy, a Plot Hook.
    /// </summary>
    [Fact]
    public void TheOwnersResolveCountsTheCompanionsHigherTraitsAndItsFlaws()
    {
        var family = Family(new("kestrel", Owner()), new("talon", Companion()));

        Assert.Equal(22, family.OwnResolve);
        Assert.Equal(f.Derived.CalculateResolve(family.Owner!.Sheet), family.OwnResolve);
        Assert.Equal(7, family.OwnerResolve);
    }

    /// <summary>The control: a companion with lower Traits and no Resolve-earning Flaw leaves the figure alone.</summary>
    [Fact]
    public void ACompanionWithLowerTraitsAndNoSuchFlawChangesNothing()
    {
        var talon = Companion();
        talon.AbilityRanks["agility"] = 1;
        talon.Flaws.RemoveAll(fl => fl.FlawId == "enemy");

        var family = Family(new("kestrel", Owner()), new("talon", talon));

        Assert.Equal(family.OwnResolve, family.OwnerResolve);
    }

    // ── The budget ────────────────────────────────────────────────────────

    [Fact]
    public void ACompanionSpendingMoreThanItsPurchaseGivesIsOverBudget()
    {
        var talon  = new RosterEntry("talon", Companion());
        var family = Family(new("kestrel", Owner(units: 1)), talon);

        var issue = Assert.Single(family.Issues);
        Assert.Equal("COMPANION_OVER_BUDGET", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("talon", issue.SubjectId);
        Assert.Equal("kestrel", issue.OwnerId);
        Assert.Equal(f.Costs.TotalCost(talon.Sheet), issue.Value);
        Assert.Equal(10, issue.Limit);
    }

    /// <summary>
    /// One purchase pays for one companion, and each takes the smallest purchase that covers it.
    /// Built so that taking the largest first fails: Talon (cheap) is first in the roster and
    /// would take the 40, leaving Brute (dear) over a 30.
    /// </summary>
    [Fact]
    public void EachCompanionTakesTheSmallestPurchaseThatCoversIt()
    {
        var owner = Owner(units: 0);
        owner.Perks.Add(new SelectedPerk(PetSidekick, 4, "Brute"));
        owner.Perks.Add(new SelectedPerk(PetSidekick, 3, "Talon"));

        var talon = Companion();
        talon.AbilityRanks["agility"] = 1;
        var brute = Companion(name: "Brute");
        while (f.Costs.TotalCost(brute) <= 30) brute.AbilityRanks["might"] = brute.GetAbilityRank("might") + 1;

        Assert.True(f.Costs.TotalCost(talon) <= 30, "Talon must fit the smaller purchase");
        Assert.InRange(f.Costs.TotalCost(brute), 31, 40);

        var family = Family(new("kestrel", owner), new("talon", talon), new("brute", brute));

        Assert.Empty(family.Issues);
        Assert.Equal(30, family.Budget["talon"]);
        Assert.Equal(40, family.Budget["brute"]);
    }

    [Fact]
    public void ACompanionNoPurchasePaysForIsNotPaid()
    {
        var withNone = Family(new("kestrel", Owner(units: 0)), new("talon", Companion()));
        Assert.Equal(["COMPANION_NOT_PAID"], Codes(withNone));
        Assert.Null(withNone.Budget["talon"]);

        // One purchase, two companions: the second has nothing left to take.
        var twice = Family(new("kestrel", Owner()), new("talon", Companion()),
                           new("talon_2", Companion(name: "Talon II")));
        var issue = Assert.Single(twice.Issues);
        Assert.Equal("COMPANION_NOT_PAID", issue.Code);
        Assert.Equal("talon_2", issue.SubjectId);
        Assert.Contains("already pays for another companion", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APurchaseNoCompanionUsesIsAWarningOnTheOwner()
    {
        var family = Family(new RosterEntry("kestrel", Owner()));

        var issue = Assert.Single(family.Issues);
        Assert.Equal("PET_SIDEKICK_NO_COMPANION", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("kestrel", issue.SubjectId);
        Assert.Equal(40, issue.Value);
    }

    [Fact]
    public void ACompanionWhoseOwnerIsNotInTheRosterIsAWarningWithNoFigures()
    {
        var family = Family(new RosterEntry("talon", Companion()));

        Assert.Null(family.Owner);
        Assert.Equal("kestrel", family.OwnerId);
        Assert.Null(family.Budget["talon"]);
        Assert.Null(family.OwnResolve);
        Assert.Null(family.OwnerResolve);

        var issue = Assert.Single(family.Issues);
        Assert.Equal("COMPANION_OWNER_NOT_IN_ROSTER", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    // ── The Trait Cap ─────────────────────────────────────────────────────

    [Fact]
    public void ACompanionAbidesByItsOwnersTraitCap()
    {
        var talon = Companion();
        talon.SelectedTierId = "high_level";

        var issue = Assert.Single(Family(new("kestrel", Owner()), new("talon", talon)).Issues);

        Assert.Equal("COMPANION_CAP_NOT_OWNERS", issue.Code);
        Assert.Equal(16, issue.Value);
        Assert.Equal(12, issue.Limit);
    }

    [Fact]
    public void TwoEntriesUnderOneIdAreRefused() =>
        Assert.Throws<ArgumentException>(() =>
            Companions.Families([new("kestrel", Owner()), new("kestrel", Companion())]));

    // ── What one sheet says alone ─────────────────────────────────────────

    /// <summary>
    /// "Can't have more than half your Hero Points": Standard's 125 halves to 62, so 6 units
    /// (60) is the most and 7 (70) is over. The control is 6 on the same sheet.
    /// </summary>
    [Fact]
    public void AnOwnerBuyingMoreThanHalfTheirBudgetIsOverTheCeiling()
    {
        Assert.DoesNotContain(f.Validator.Validate(Owner(units: 6)).Issues, i => i.Code == "PET_SIDEKICK_OVER_HALF");

        var issue = Assert.Single(f.Validator.Validate(Owner(units: 7)).Issues, i => i.Code == "PET_SIDEKICK_OVER_HALF");
        Assert.Equal(PetSidekick, issue.SubjectId);
        Assert.Equal(70, issue.Value);
        Assert.Equal(62, issue.Limit);
    }

    /// <summary>
    /// A companion's budget is its owner's, so the tier's is waived on its sheet. The control is
    /// the same over-spending sheet with the link taken off.
    /// </summary>
    [Fact]
    public void ACompanionsTierBudgetIsWaivedOnItsOwnSheet()
    {
        var talon = Companion();
        talon.SelectedTierId = "street_level";
        while (f.Costs.TotalCost(talon) <= 75) talon.AbilityRanks["might"] = talon.GetAbilityRank("might") + 1;

        Assert.DoesNotContain(f.Validator.Validate(talon).Issues, i => i.Code == "HP_BUDGET_EXCEEDED");

        talon.Variant = null;
        Assert.Contains(f.Validator.Validate(talon).Issues, i => i.Code == "HP_BUDGET_EXCEEDED");
    }

    [Fact]
    public void ACompanionBuysNoDeterminationAndNoCompanionOfItsOwn()
    {
        var talon = Companion();
        Assert.DoesNotContain(f.Validator.Validate(talon).Issues, i => i.Code.StartsWith("COMPANION_", StringComparison.Ordinal));

        talon.SelectedPowers.Add(new SelectedPower("determination", 0) { Units = 1, SourceId = "innate" });
        talon.Perks.Add(new SelectedPerk(PetSidekick, 1, "A flea"));
        var codes = f.Validator.Validate(talon).Issues.Select(i => i.Code).ToList();

        Assert.Contains("COMPANION_HAS_NO_RESOLVE", codes);
        Assert.Contains("COMPANION_HAS_COMPANION", codes);

        // The same purchases on a sheet that is nobody's companion are ordinary.
        talon.Variant = null;
        Assert.DoesNotContain(f.Validator.Validate(talon).Issues, i => i.Code.StartsWith("COMPANION_", StringComparison.Ordinal));
    }

    // ── The data is the entry ─────────────────────────────────────────────

    /// <summary>
    /// Every field of Pet/Sidekick's companion block, read back out of the entry's own text in
    /// <c>perks.json</c> and out of the printed page in the corpus, and only that Perk carries one.
    /// </summary>
    [Fact]
    public void TheCompanionRuleIsWhatPageFiftyFiveSays()
    {
        Assert.Equal([PetSidekick], f.Rules.Perks.Where(p => p.Companion is not null).Select(p => p.Id));

        var perk = f.Rules.GetPerk(PetSidekick)!;
        var rule = perk.Companion!;

        using var chapter = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "data", "rulebook", "ch02-characters.json")));
        var printed = chapter.RootElement.GetProperty("sections").EnumerateArray()
            .Single(s => s.GetProperty("heading").GetString() == "PET/SIDEKICK");
        var page = printed.GetProperty("text").GetString()!;

        Assert.Equal(printed.GetProperty("printed_page").GetInt32(), rule.Page);
        Assert.Contains($"gives you {rule.HeroPointsPerUnit} Hero Points to create this character", page, StringComparison.Ordinal);
        Assert.Contains($"gives your companion {rule.HeroPointsPerUnit} Hero Points", perk.Description, StringComparison.Ordinal);

        Assert.Equal(2, rule.CapDivisorOfOwner);
        Assert.Contains("can’t have more than half your Hero Points", page, StringComparison.Ordinal);

        Assert.True(rule.AbidesByTraitCap);
        Assert.Contains("must abide by the game’s Trait Cap", page, StringComparison.Ordinal);

        Assert.Contains($"earns 1 Hero Point for every {rule.OwnerHeroPointsPerCompanionPoint} that you earn", page, StringComparison.Ordinal);

        Assert.False(rule.HasResolve);
        Assert.Contains("They lack Resolve", page, StringComparison.Ordinal);

        Assert.True(rule.OwnerResolveUsesHigherTraits);
        Assert.Contains("use their Traits when determining your Resolve", page, StringComparison.Ordinal);

        Assert.True(rule.FlawsEarnOwnerResolve);
        Assert.Contains("you earn Resolve as if that character were a Hero", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The codes this file provokes are exactly the codes <c>Companions.cs</c> declares, and
    /// exactly the ones <c>ValidationIssueStructureTests</c> exempts — the hold
    /// <c>AlternateFormTests</c> keeps on its own class.
    /// </summary>
    [Fact]
    public void EveryCodeTheClassDeclaresIsProvokedHereAndExemptedThere()
    {
        var source   = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "engine", "Companions.cs"));
        var declared = new Regex(@"""([A-Z][A-Z0-9_]*)""", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(source).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var provoked = EveryFinding().Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        var exempted = ValidationIssueStructureTests.UnprovokableCodes
            .Where(declared.Contains).ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= 5, "the scan has stopped finding codes: " + declared.Count);
        Assert.Equal(declared.Order(), provoked.Order());
        Assert.Equal(declared.Order(), exempted.Order());
    }

    private IEnumerable<ValidationIssue> EveryFinding()
    {
        var overBudget = Family(new("kestrel", Owner(units: 1)), new("talon", Companion()));
        var notPaid    = Family(new("kestrel", Owner(units: 0)), new("talon", Companion()));
        var unused     = Family(new RosterEntry("kestrel", Owner()));
        var noOwner    = Family(new RosterEntry("talon", Companion()));

        var high = Companion();
        high.SelectedTierId = "high_level";
        var cap = Family(new("kestrel", Owner()), new("talon", high));

        return new[] { overBudget, notPaid, unused, noOwner, cap }.SelectMany(fam => fam.Issues);
    }
}
