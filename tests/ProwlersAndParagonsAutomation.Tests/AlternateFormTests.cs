using System.Text.Json;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Item 21's slice two: <see cref="AlternateForms"/> applies Ch.2 p.21's Alternate Form entry to
/// a roster — the one rule in this engine that needs two sheets at once.
///
/// <para><b>The positive control is the book's own pair.</b> Herald is printed twice, on
/// pp.134–135, as Airmid and Scáthach: both Standard, both carrying Alternate Form at the
/// Standard level with Independent Forms, both printing Resolve 5. Linked as root and form they
/// have to pass every check here and share a pool of 5, or the checks are about something other
/// than the book — so every negative case below is that pair with one thing changed.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class AlternateFormTests
{
    private readonly RulesFixture _f;

    public AlternateFormTests(RulesFixture f) => _f = f;

    private AlternateForms Forms => new(_f.Rules, _f.Costs, _f.Derived);

    private CharacterSheet Hero(string name) =>
        PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, PrebuiltHeroes.All.Single(h => h.Name == name));

    /// <summary>Airmid as the root, Scáthach linked to her as an alternate form.</summary>
    private (RosterEntry Root, RosterEntry Form) Herald()
    {
        var airmid   = Hero("Herald (Airmid)");
        var scathach = Hero("Herald (Scathach)");
        airmid.Name   = "Herald (Airmid)";
        scathach.Name = "Herald (Scathach)";
        scathach.Variant = new CharacterVariant("airmid", CharacterVariant.AlternateForm);
        return (new("airmid", airmid), new("scathach", scathach));
    }

    private static SelectedPower Purchase(CharacterSheet sheet) =>
        sheet.SelectedPowers.Single(p => p.PowerId == AlternateForms.PowerId);

    private static void Replace(CharacterSheet sheet, SelectedPower with)
    {
        var index = sheet.SelectedPowers.FindIndex(p => p.PowerId == AlternateForms.PowerId);
        sheet.SelectedPowers[index] = with;
    }

    private AlternateFormFamily Family(params RosterEntry[] roster) =>
        Assert.Single(Forms.Families(roster));

    // ── The positive control ──────────────────────────────────────────────

    [Fact]
    public void ThePublishedHeraldPairPassesEveryCheckAndSharesThePoolTheBookPrints()
    {
        var (root, form) = Herald();

        // Controls on the fixture: both really do carry the Power at the Standard level, and
        // each prints Resolve 5 on its own page — so a pool of 5 is the book's figure and not
        // a coincidence of two zeros.
        Assert.Equal(3, Purchase(root.Sheet).Units);
        Assert.Equal(3, Purchase(form.Sheet).Units);
        Assert.Equal(5, _f.Derived.CalculateResolve(root.Sheet));
        Assert.Equal(5, _f.Derived.CalculateResolve(form.Sheet));

        var family = Family(root, form);

        Assert.Empty(family.Issues);
        Assert.Equal("airmid", family.RootId);
        Assert.Same(root, family.Root);
        Assert.Equal([form], family.Forms);
        Assert.Equal(5, family.Resolve["airmid"]);
        Assert.Equal(5, family.Resolve["scathach"]);
        Assert.Equal(5, family.SharedResolve);
    }

    [Fact]
    public void ARosterWithNoAlternateFormLinkHasNoFamilies()
    {
        var (root, form) = Herald();
        form.Sheet.Variant = null;

        Assert.Empty(Forms.Families([root, form]));
    }

    [Theory]
    [InlineData(CharacterVariant.Later)]
    [InlineData(CharacterVariant.AsSeenBy)]
    public void TheOtherTwoKindsOfLinkAreNotFamilies(string kind)
    {
        var (root, form) = Herald();
        form.Sheet.Variant = new CharacterVariant("airmid", kind);

        Assert.Empty(Forms.Families([root, form]));
    }

    // ── "Use the lowest Resolve among your various forms" ─────────────────

    /// <summary>
    /// The pool is the minimum, and the case is built so that neither "the root's", nor "the
    /// first member's", nor "the last one's", nor "the first form's" gives the same answer: the
    /// members run 6, 9, 5, 7 in roster order, and the root is not the lowest.
    /// </summary>
    [Fact]
    public void ThePoolIsTheLowestOfTheFormsAndNotTheRootsOrTheFirstOrTheLast()
    {
        var (root, form) = Herald();

        // Airmid with a Condition flaw: +1 Resolve, so 6.
        Assert.Equal("condition", _f.Rules.GetFlaw("disabled")!.FlawType);
        root.Sheet.Flaws.Add(new SelectedFlaw("disabled"));

        // Scáthach's Determination buys +2; buying +6 makes 9 and buying +4 makes 7.
        RosterEntry Scathach(string id, int determination)
        {
            var sheet = Hero("Herald (Scathach)");
            sheet.Name = "Herald (Scathach)";
            sheet.Variant = new CharacterVariant("airmid", CharacterVariant.AlternateForm);
            var index = sheet.SelectedPowers.FindIndex(p => p.PowerId == "determination");
            sheet.SelectedPowers[index] = sheet.SelectedPowers[index] with { Units = determination };
            return new(id, sheet);
        }

        var high = Scathach("high", 6);
        var mid  = Scathach("mid", 4);

        // Three alternate forms: the root pays three times, and so does every form.
        foreach (var sheet in new[] { root.Sheet, form.Sheet, high.Sheet, mid.Sheet })
        {
            var purchase = Purchase(sheet);
            sheet.SelectedPowers.Add(purchase);
            sheet.SelectedPowers.Add(purchase);
        }

        var members  = new[] { root, high, form, mid };
        var resolves = members.Select(m => _f.Derived.CalculateResolve(m.Sheet)).ToList();
        Assert.Equal([6, 9, 5, 7], resolves);

        var family = Family(members);

        Assert.Empty(family.Issues);
        Assert.Equal(5, family.SharedResolve);
        Assert.Equal(resolves, members.Select(m => family.Resolve[m.Id]!.Value).ToList());
    }

    /// <summary>
    /// <see cref="DerivedStatsCalculator.CalculateResolve"/> answers 0 for a sheet with no
    /// resolvable tier, and 0 is the lowest number there is — so a pool taken over it would be
    /// 0, quoted as though it were a figure. Null instead, for the member and for the pool.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("standrad")]
    public void AFormWhoseTierCannotBeResolvedHasNoResolveAndSoThePoolHasNone(string? tierId)
    {
        var (root, form) = Herald();
        form.Sheet.SelectedTierId = tierId;

        var family = Family(root, form);

        Assert.Equal(5, family.Resolve["airmid"]);
        Assert.Null(family.Resolve["scathach"]);
        Assert.Null(family.SharedResolve);

        // And the level checks say nothing about it: its own report already carries the tier.
        Assert.DoesNotContain(family.Issues, i => i.Code is "ALTERNATE_FORM_ABOVE_ROOT_LEVEL"
                                                   or "ALTERNATE_FORM_LEVEL_NOT_PAID");
    }

    // ── "Both forms must pay for this Power" ──────────────────────────────

    [Theory]
    [InlineData("scathach")]
    [InlineData("airmid")]
    public void AMemberWithoutThePowerIsReportedAndAddingItClosesTheFinding(string who)
    {
        var (root, form) = Herald();
        var member = who == "airmid" ? root : form;
        var purchase = Purchase(member.Sheet);
        member.Sheet.SelectedPowers.Remove(purchase);

        var issue = Assert.Single(Family(root, form).Issues, i => i.Code == "ALTERNATE_FORM_NOT_PAID");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal(who, issue.SubjectId);
        Assert.Equal("airmid", issue.OwnerId);

        // The other member is not blamed for it, and nothing else fires on a pair that is
        // otherwise the book's — no cost comparison against a purchase that is not there.
        Assert.Single(Family(root, form).Issues);

        member.Sheet.SelectedPowers.Add(purchase);
        Assert.Empty(Family(root, form).Issues);
    }

    // ── "Each form must pay this Power's total cost" ──────────────────────

    [Fact]
    public void AFormPayingADifferentTotalIsReportedWithBothFigures()
    {
        var (root, form) = Herald();

        // Scáthach drops Independent Forms: 12 HP where Airmid pays 12 − 3 = 9. The level is
        // still the Standard one the root paid for, so the cost is the only thing that differs.
        Replace(form.Sheet, Purchase(form.Sheet) with { Cons = [] });

        var expected = _f.Costs.PowerCost(Purchase(form.Sheet));
        var rootPays = _f.Costs.PowerCost(Purchase(root.Sheet));
        Assert.NotEqual(expected, rootPays);

        var issue = Assert.Single(Family(root, form).Issues);

        Assert.Equal("ALTERNATE_FORM_COST_DIFFERS", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("scathach", issue.SubjectId);
        Assert.Equal("airmid", issue.OwnerId);
        Assert.Equal(expected, issue.Value);
        Assert.Equal(rootPays, issue.Limit);
    }

    // ── "Any power level up to but not higher than yours" ─────────────────

    [Fact]
    public void AFormAboveTheRootsPowerLevelIsReportedAgainstTheRootsLevel()
    {
        var (root, form) = Herald();
        form.Sheet.SelectedTierId = "high_level";
        Replace(form.Sheet, Purchase(form.Sheet) with { Units = 4 });
        Replace(root.Sheet, Purchase(root.Sheet) with { Units = 4 });

        var issues = Family(root, form).Issues;
        var issue  = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_ABOVE_ROOT_LEVEL");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("scathach", issue.SubjectId);
        Assert.Equal("airmid", issue.OwnerId);
        Assert.Equal(4, issue.Value);
        Assert.Equal(3, issue.Limit);

        // The root did pay for a High Level form, so the level is paid for: this is the one
        // finding, and the repair the structure implies — the form down to the root's level —
        // then trips the paid-level check instead, which is the right next finding.
        Assert.Single(issues);
    }

    // ── "This Power's cost varies depending on your other form's power level" ──

    [Fact]
    public void AFormAtALevelTheRootDidNotPayForIsReportedAndOffersTheLevelsPaidFor()
    {
        var (root, form) = Herald();

        // Scáthach rebuilt as a Low Level form while Airmid still pays for a Standard one.
        form.Sheet.SelectedTierId = "low_level";

        var issues = Family(root, form).Issues;
        var issue  = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_LEVEL_NOT_PAID");

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("scathach", issue.SubjectId);
        Assert.Equal("airmid", issue.OwnerId);
        Assert.Equal(2, issue.Value);
        Assert.Equal(["standard"], issue.Options);

        // And the purchase it left unmatched is reported on the root, as a warning: the form
        // may simply not be in the roster.
        var unmatched = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_PAID_NOT_IN_ROSTER");
        Assert.Equal(ValidationSeverity.Warning, unmatched.Severity);
        Assert.Equal("airmid", unmatched.SubjectId);
        Assert.Equal(3, unmatched.Value);
        Assert.Contains("Standard", unmatched.Message, StringComparison.Ordinal);

        // The repair, made from the structure alone: the form takes an offered level.
        form.Sheet.SelectedTierId = issue.Options[0];
        Assert.Empty(Family(root, form).Issues);
    }

    /// <summary>
    /// One purchase pays for one form. Two Standard forms against one Standard purchase is one
    /// form unpaid for — not both paid for because a Standard purchase exists.
    /// </summary>
    [Fact]
    public void OnePurchasePaysForOneFormAndNotForEveryFormAtThatLevel()
    {
        var (root, form) = Herald();
        var second = Hero("Herald (Scathach)");
        second.Variant = new CharacterVariant("airmid", CharacterVariant.AlternateForm);

        var issues = Family(root, form, new("second", second)).Issues;

        var unpaid = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_LEVEL_NOT_PAID");
        Assert.Equal("second", unpaid.SubjectId);
        Assert.DoesNotContain(issues, i => i.Code == "ALTERNATE_FORM_PAID_NOT_IN_ROSTER");

        // Paying for the second form on every member closes it: "each form must pay this
        // Power's total cost", so all three carry two purchases.
        foreach (var sheet in new[] { root.Sheet, form.Sheet, second })
            sheet.SelectedPowers.Add(Purchase(sheet));

        Assert.Empty(Family(root, form, new("second", second)).Issues);
    }

    [Fact]
    public void APurchaseAtALevelNoTierIsIsStillReportedByNumber()
    {
        var (root, form) = Herald();
        Replace(root.Sheet, Purchase(root.Sheet) with { Units = 9 });
        Replace(form.Sheet, Purchase(form.Sheet) with { Units = 9 });

        var issues = Family(root, form).Issues;
        var unmatched = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_PAID_NOT_IN_ROSTER");

        Assert.Equal(9, unmatched.Value);
        Assert.Contains("9", unmatched.Message, StringComparison.Ordinal);

        // And the form's own level is unpaid for, with nothing to offer but nothing to crash on.
        var unpaid = Assert.Single(issues, i => i.Code == "ALTERNATE_FORM_LEVEL_NOT_PAID");
        Assert.Empty(unpaid.Options);
    }

    // ── A root the roster does not hold ───────────────────────────────────

    [Fact]
    public void AFormWhoseRootIsNotInTheRosterIsAWarningWithNoPool()
    {
        var (_, form) = Herald();

        var family = Family(form);

        Assert.Null(family.Root);
        Assert.Equal("airmid", family.RootId);
        Assert.Equal(5, family.Resolve["scathach"]);
        Assert.Null(family.SharedResolve);

        var issue = Assert.Single(family.Issues);
        Assert.Equal("ALTERNATE_FORM_ROOT_NOT_IN_ROSTER", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("scathach", issue.SubjectId);
        Assert.Equal("airmid", issue.OwnerId);
    }

    [Fact]
    public void TwoEntriesUnderOneIdAreRefused()
    {
        var (root, form) = Herald();

        var e = Assert.Throws<ArgumentException>(() => Forms.Families([root, form, new("airmid", Hero("Vector"))]));
        Assert.Contains("airmid", e.Message, StringComparison.Ordinal);
    }

    // ── The power-level ladder is the book's table ────────────────────────

    /// <summary>
    /// <see cref="AlternateForms.PowerLevel"/> counts tiers in <c>tiers.json</c> order, and the
    /// claim that this is the order p.21's cost table prints is checked against the corpus: the
    /// table names each tier, in order, at <c>cost_per_unit × level</c>.
    /// </summary>
    [Fact]
    public void ThePowerLevelLadderIsTheOrderTheBooksCostTablePrints()
    {
        using var chapter = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "data", "rulebook", "ch02-characters.json")));
        var table = chapter.RootElement.GetProperty("sections").EnumerateArray()
            .Single(s => s.GetProperty("heading").GetString() == "ALTERNATE FORM — POWER LEVEL COST");
        Assert.Equal(21, table.GetProperty("printed_page").GetInt32());
        var line = table.GetProperty("text").GetString()!.Split('\n')[0];
        var rate  = _f.Rules.GetPower(AlternateForms.PowerId)!.CostPerUnit!.Value;

        var expected = string.Join(" ", _f.Rules.Tiers.Select((t, i) => $"{t.Name} {rate * (i + 1)}"));
        Assert.Equal(expected, line);

        for (var i = 0; i < _f.Rules.Tiers.Count; i++)
            Assert.Equal(i + 1, Forms.PowerLevel(_f.Rules.Tiers[i].Id));

        Assert.Equal(6, _f.Rules.Tiers.Count);
        Assert.Null(Forms.PowerLevel(null));
        Assert.Null(Forms.PowerLevel("standrad"));
    }

    // ── Messages, and the code list ───────────────────────────────────────

    /// <summary>
    /// Every finding this class can make, provoked. The single-sheet suite's message rules are
    /// applied by hand here because <c>ValidationMessageTests</c> is driven from
    /// <c>Validate(sheet)</c> and never reaches a roster.
    /// </summary>
    private List<ValidationIssue> EveryFinding()
    {
        var all = new List<ValidationIssue>();

        var (root, form) = Herald();
        form.Sheet.SelectedTierId = "low_level";
        all.AddRange(Family(root, form).Issues);

        (root, form) = Herald();
        form.Sheet.SelectedTierId = "high_level";
        all.AddRange(Family(root, form).Issues);

        (root, form) = Herald();
        Replace(form.Sheet, Purchase(form.Sheet) with { Cons = [] });
        all.AddRange(Family(root, form).Issues);

        (root, form) = Herald();
        form.Sheet.SelectedPowers.Remove(Purchase(form.Sheet));
        all.AddRange(Family(root, form).Issues);

        (_, form) = Herald();
        all.AddRange(Family(form).Issues);

        return all;
    }

    [Fact]
    public void EveryMessageIsASentenceNamingNoIdAndNoFile()
    {
        var findings = EveryFinding();
        Assert.True(findings.Count >= 6);

        var snakeCase = new Regex(@"\b[a-z]+_[a-z_]+\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var issue in findings)
        {
            Assert.EndsWith(".", issue.Message);
            Assert.True(char.IsUpper(issue.Message[0]) || issue.Message[0] == '\'', issue.Message);
            Assert.DoesNotMatch(snakeCase, issue.Message);
            Assert.DoesNotContain(".json", issue.Message, StringComparison.Ordinal);
        }

        // Names, not ids: a member with a printed name is called by it.
        Assert.All(findings, i => Assert.Contains("Herald", i.Message, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>The codes this file provokes are exactly the codes <c>AlternateForms.cs</c> declares,
    /// and exactly the ones <c>ValidationIssueStructureTests</c> exempts from its own case
    /// list</b> — the same three-way hold that keeps the single-sheet validator's codes honest,
    /// applied to the one class whose findings that suite cannot reach. A code added to the
    /// engine and to neither list is red here; a code removed from the engine and left on the
    /// exemption list is red here.
    /// </summary>
    [Fact]
    public void EveryCodeTheClassDeclaresIsProvokedHereAndExemptedThere()
    {
        var source   = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "engine", "AlternateForms.cs"));
        var declared = new Regex(@"""([A-Z][A-Z0-9_]*)""", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(source).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var provoked = EveryFinding().Select(i => i.Code).ToHashSet(StringComparer.Ordinal);

        var exempted = ValidationIssueStructureTests.UnprovokableCodes
            .Where(c => c.StartsWith("ALTERNATE_FORM_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= 6, "the scan has stopped finding codes: " + declared.Count);
        Assert.Equal(declared.Order(), provoked.Order());
        Assert.Equal(declared.Order(), exempted.Order());
    }
}
