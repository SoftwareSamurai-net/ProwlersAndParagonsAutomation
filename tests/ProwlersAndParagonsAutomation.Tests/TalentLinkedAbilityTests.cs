namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Checks talents.json's linked_ability against <see cref="CanonicalTalentLinkedAbilities"/>.
///
/// <para><b>This is the one area of the four where the finding is that the book has almost
/// nothing to check against</b> — see that file's own remarks. Before this file,
/// <c>RulesDataTests.EveryTalentNamesAnAbilityThatExists</c> checked only that
/// <c>linked_ability</c> named a real Ability id, which any of the other five wrong Abilities
/// would also satisfy. This file pins the current value of all twelve (so a change is still
/// caught and has to be deliberate) and, separately, asserts the one value the rulebook
/// actually states — Covert : agility — against its page.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class TalentLinkedAbilityTests
{
    private readonly RulesFixture _f;

    public TalentLinkedAbilityTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string> AllTalentIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in CanonicalTalentLinkedAbilities.All) data.Add(e.TalentId);
        return data;
    }

    // ── Coverage: the transcription cannot quietly cover a subset ──────────────

    [Fact]
    public void EveryRulebookTalentIsCoveredAndNothingExtraIs()
    {
        var expected = CanonicalTalentLinkedAbilities.All.Select(e => e.TalentId).OrderBy(x => x, StringComparer.Ordinal);
        var actual   = _f.Rules.Talents.Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheTranscriptionCoversAllTwelveTalents() =>
        Assert.Equal(12, CanonicalTalentLinkedAbilities.All.Count);

    /// <summary>
    /// Exactly one of the twelve is stated in the book. If a second one ever gets a page
    /// reference, that is a real find worth updating this bound for — not a bug in the test.
    /// </summary>
    [Fact]
    public void ExactlyOneLinkedAbilityIsStatedInTheRulebook()
    {
        Assert.Equal(1, CanonicalTalentLinkedAbilities.All.Count(e => e.BookSourced));
        Assert.Equal("covert", CanonicalTalentLinkedAbilities.All.Single(e => e.BookSourced).TalentId);
    }

    // ── Against the data (regression snapshot; see the file remarks for sourcing) ──────

    [Theory]
    [MemberData(nameof(AllTalentIds))]
    public void LinkedAbilityMatchesTheTranscription(string id)
    {
        var expected = CanonicalTalentLinkedAbilities.All.Single(e => e.TalentId == id);
        var talent   = _f.Rules.GetTalent(id);

        Assert.NotNull(talent);
        Assert.Equal(expected.Ability, talent.LinkedAbility);
    }

    // ── The one book-sourced value, checked against its page ────────────────────

    [Fact]
    public void CovertSubstitutesHalfAgilityPerTheAgilityEntryOnPage17()
    {
        var covert = CanonicalTalentLinkedAbilities.All.Single(e => e.TalentId == "covert");

        Assert.True(covert.BookSourced);
        Assert.Equal(17, covert.Page);
        Assert.Equal("agility", covert.Ability);

        var talent = _f.Rules.GetTalent("covert");
        Assert.NotNull(talent);
        Assert.Equal("agility", talent.LinkedAbility);
    }
}
