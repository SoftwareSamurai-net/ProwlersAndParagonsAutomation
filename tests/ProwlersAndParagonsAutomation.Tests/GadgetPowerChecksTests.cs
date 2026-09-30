using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// PROGRESS.md item 36: four of the validator's per-Power checks walked
/// <see cref="CharacterSheet.SelectedPowers"/> alone, so the same Power gap that
/// <c>CheckUnitNames</c> had (PR #200, fixed by hand for that one check) shipped a third time —
/// this time in four checks at once. One probe per check, against a Gadget's own Powers, which
/// p.94 buys "under the ordinary rules" and are therefore ordinary <see cref="SelectedPower"/>s
/// one budget down.
///
/// <para><b>A fifth probe, found while building the source-reading guard for this fix
/// (<c>GadgetPowerWalkReadTests</c>), not named in PROGRESS.md item 36.</b>
/// <c>CheckPowerRanks</c> is the same shape exactly — it also walked
/// <see cref="CharacterSheet.SelectedPowers"/> alone — and nothing before this pull request had
/// noticed. Folded into the same fix rather than left for a sixth sighting.</para>
///
/// <para><b>What is not here: a probe for the fifth of item 36's four, the description-verified
/// half of <c>CheckUnverifiedPowers</c>.</b> Every one of the 141 Powers is fully verified today
/// — <c>needs_review</c> is empty and every entry's <c>verified_fields</c> includes
/// <c>description</c> (see <c>PowerDataTests</c> and <c>CLAUDE.md</c>'s settled list) — so no
/// sheet, character's or Gadget's, can provoke <c>POWER_MECHANICS_UNVERIFIED</c> or
/// <c>POWER_DESCRIPTION_UNVERIFIED</c> through the real rules <see cref="RulesFixture"/> loads.
/// A hand-built <c>PowerModel</c> with <c>NeedsReview</c> forced true would need a second,
/// fixture-built <c>RulesRepository</c> and would stop testing the shipped data at all — exactly
/// what this suite's own rule says not to do. The gap is covered structurally instead:
/// <c>CheckUnverifiedPowers</c> is folded into <see cref="EveryPaidPowerWalksBothCollections"/>
/// below alongside the other four, and <c>GadgetPowerWalkReadTests</c> proves it no longer
/// contains a bare walk of <c>sheet.SelectedPowers</c> of its own — the same source-level proof a
/// sixth check forgetting Gadgets would fail under.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class GadgetPowerChecksTests
{
    private readonly RulesFixture _f;

    public GadgetPowerChecksTests(RulesFixture fixture) => _f = fixture;

    private CharacterSheet WithGadget(BuiltGadget gadget)
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 6;
        sheet.Gadgets.Add(gadget);
        return sheet;
    }

    private IEnumerable<ValidationIssue> Issues(CharacterSheet sheet, string code) =>
        _f.Validator.Validate(sheet).Issues.Where(i => i.Code == code);

    // ── (a) CheckDuplicatePowers ────────────────────────────────────────────

    /// <summary>
    /// The same non-repeatable Power bought twice inside one Gadget is priced twice — exactly the
    /// character-level bug this code already catches — and drew no <c>DUPLICATE_POWER</c> because
    /// the check never looked inside a Gadget at all.
    /// </summary>
    [Fact]
    public void TheSamePowerTwiceInOneGadgetIsADuplicateOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("blast", 1), new SelectedPower("blast", 1)]
        });

        var issue = Assert.Single(Issues(sheet, "DUPLICATE_POWER"));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    /// <summary>A repeatable Power (Alternate Form, Duplication) is exempt inside a Gadget too.</summary>
    [Fact]
    public void ARepeatablePowerTwiceInAGadgetIsNotADuplicate()
    {
        var alternateForm = _f.Rules.Powers.First(p => p.Repeatable);
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower(alternateForm.Id, 1), new SelectedPower(alternateForm.Id, 1)]
        });

        Assert.Empty(Issues(sheet, "DUPLICATE_POWER"));
    }

    // ── (b) CheckPowerCosts ───────────────────────────────────────────────────

    /// <summary>
    /// Cons driving a Gadget Power to the rulebook floor drew no <c>POWER_COST_AT_MINIMUM</c>, so
    /// a further Con on it silently bought nothing and nobody was told — the same probe
    /// <c>ConsThatBottomOutTheCostRaiseAWarningNotAnError</c> runs against the character's own.
    /// </summary>
    [Fact]
    public void ConsThatBottomOutAGadgetPowersCostAreAWarningOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     =
            [
                new SelectedPower("blast", 6,
                    [], [new SelectedProCon("overkill"), new SelectedProCon("limited", "severely_limited")])
            ]
        });

        var issue = Assert.Single(Issues(sheet, "POWER_COST_AT_MINIMUM"));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    // ── (c) CheckSources ──────────────────────────────────────────────────────

    /// <summary>A rankless Gadget Power with no Source has no default rank against other Powers.</summary>
    [Fact]
    public void ARanklessGadgetPowerWithNoSourceIsAWarningOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("communications", 0)]
        });

        var issue = Assert.Single(Issues(sheet, "RANKLESS_POWER_WITHOUT_SOURCE"));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    /// <summary>A ranked Gadget Power with no Source will print unsourced on the Gadget's own group.</summary>
    [Fact]
    public void ARankedGadgetPowerWithNoSourceIsAWarningOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("blast", 3)]
        });

        var issue = Assert.Single(Issues(sheet, "POWER_WITHOUT_SOURCE"));

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    /// <summary>A Gadget Power naming a Source the rulebook does not have is an error, as it is on the character's own.</summary>
    [Fact]
    public void AGadgetPowerNamingAnUnknownSourceIsAnErrorOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("blast", 3) { SourceId = "cosmic" }]
        });

        var issue = Assert.Single(Issues(sheet, "UNKNOWN_SOURCE"));

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    // ── (extra) CheckPowerRanks, the fifth instance found via the guard ───────

    /// <summary>
    /// A Power the rulebook gives no rank cannot have ranks bought for it inside a Gadget either —
    /// <c>GadgetSpend</c> prices it through the same <c>PowerCost</c> as the character's own, so
    /// buying ranks on one bought nothing and, until this fix, said nothing.
    /// </summary>
    [Fact]
    public void BuyingRanksForARanklessGadgetPowerIsAnErrorOnTheGadget()
    {
        var sheet = WithGadget(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("communications", 3)]
        });

        var issue = Assert.Single(Issues(sheet, "POWER_HAS_NO_RANK"));

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(ValidationSubject.Gadget, issue.SubjectKind);
        Assert.Equal("Vault Breaker", issue.SubjectId);
    }

    // ── The shared enumeration itself ─────────────────────────────────────────

    /// <summary>
    /// <b>The positive control for the fix's own shape.</b> Every one of the five checks above
    /// walks one enumeration now; this asserts the enumeration itself carries both collections; a
    /// version that dropped the Gadget branch would still pass every probe above only by accident
    /// of what each one happens to assert, which is exactly the shape <c>CLAUDE.md</c> asks every
    /// check here to rule out for itself.
    /// </summary>
    [Fact]
    public void EveryPaidPowerWalksBothCollections()
    {
        var sheet = _f.LegalSheet();
        sheet.TalentRanks["technology"] = 6;
        sheet.SelectedPowers.Add(new SelectedPower("communications", 0));
        sheet.Gadgets.Add(new BuiltGadget("Vault Breaker")
        {
            Complexity = 3,
            Powers     = [new SelectedPower("communications", 0)]
        });

        // One rankless, unsourced Power on the character and the same one inside a Gadget: a
        // single Validate() call has to report both, or the shared walk has quietly dropped one
        // collection back to accidental single coverage.
        var result = _f.Validator.Validate(sheet);

        Assert.Contains(result.Issues, i => i.Code == "RANKLESS_POWER_WITHOUT_SOURCE"
            && i.SubjectKind == ValidationSubject.Power && i.SubjectId == "communications");
        Assert.Contains(result.Issues, i => i.Code == "RANKLESS_POWER_WITHOUT_SOURCE"
            && i.SubjectKind == ValidationSubject.Gadget && i.SubjectId == "Vault Breaker");
    }
}
