using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>A Perk asks each thing its entry asks, separately</b> — the owner's ask of 2026-10-28, after
/// an audit found Patron's three questions, Contacts' one name per contact type, and Fame's and
/// Reputation's "which identity" all gathered in one line or not asked at all.
///
/// <para>Each test carries its control: a Perk that asks nothing asks nothing, and a character
/// with no secret identity is not asked which one.</para>
/// </summary>
public sealed class PerkQuestionsTests(RulesFixture f) : IClassFixture<RulesFixture>
{
    private static CharacterSheet WithSecretIdentity()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(PerkQuestions.SecretIdentityFlaw));
        return sheet;
    }

    [Fact]
    public void PatronAsksWhoWhatAndWhatInReturnAndPrintsEachUnderItsLabel()
    {
        var patron = f.Rules.GetPerk("patron")!;
        var questions = PerkQuestions.For(patron, 1, RulesFixture.StandardSheet());

        Assert.Equal(3, questions.Count);
        Assert.Equal("Who: ARGUS; Provides: gear and intel; Expects: missions on demand",
            PerkQuestions.Compose(questions, ["ARGUS", "gear and intel", "missions on demand"]));
        Assert.Null(PerkQuestions.Compose(questions, ["ARGUS", "gear and intel", " "]));
    }

    [Fact]
    public void ContactsAsksOneNamePerContactTypeBought()
    {
        var contacts = f.Rules.GetPerk("contacts")!;

        var three = PerkQuestions.For(contacts, 3, RulesFixture.StandardSheet());
        Assert.Equal(["Contact type 1", "Contact type 2", "Contact type 3"], three.Select(q => q.Ask));
        Assert.Equal("Police; Media; Criminal", PerkQuestions.Compose(three, ["Police", "Media", "Criminal"]));

        // Two bought and one named is half an answer, and is not one yet.
        Assert.Null(PerkQuestions.Compose(PerkQuestions.For(contacts, 2, RulesFixture.StandardSheet()), ["Police"]));
    }

    [Fact]
    public void FameAsksWhichIdentityOnlyOfACharacterWithASecretOne()
    {
        var fame = f.Rules.GetPerk("fame")!;

        Assert.Empty(PerkQuestions.For(fame, 1, RulesFixture.StandardSheet()));

        var asked = PerkQuestions.For(fame, 1, WithSecretIdentity());
        var only = Assert.Single(asked);
        Assert.Equal(PerkQuestions.Identities, only.Choices);
        Assert.Equal("Heroic alter ego", PerkQuestions.Compose(asked, ["Heroic alter ego"]));
    }

    [Fact]
    public void ReputationLeadsWithTheIdentityAndThenTheArena()
    {
        var reputation = f.Rules.GetPerk("reputation")!;

        Assert.Single(PerkQuestions.For(reputation, 1, RulesFixture.StandardSheet()));

        var asked = PerkQuestions.For(reputation, 1, WithSecretIdentity());
        Assert.Equal(2, asked.Count);
        Assert.Equal("Civilian identity — the criminal courts",
            PerkQuestions.Compose(asked, ["Civilian identity", "the criminal courts"]));
    }

    [Fact]
    public void AuthorityNowAsksWhatAuthorityAndWealthStillAsksNothing()
    {
        Assert.Single(PerkQuestions.For(f.Rules.GetPerk("authority")!, 1, WithSecretIdentity()));
        Assert.Empty(PerkQuestions.For(f.Rules.GetPerk("wealth")!, 1, WithSecretIdentity()));
    }

    /// <summary>
    /// The identity question is a transcription of the entry's own sentence, so the flag is on
    /// exactly the Perks whose text says it — not on a list somebody keeps beside them.
    /// </summary>
    [Fact]
    public void TheIdentityQuestionIsOnExactlyThePerksWhoseTextAsksIt()
    {
        var asking = f.Rules.Perks
            .Where(p => p.Description.Contains("secret identity, you must decide", StringComparison.Ordinal))
            .Select(p => p.Id).ToList();

        Assert.NotEmpty(asking);
        Assert.Equal(asking, f.Rules.Perks.Where(p => p.IdentityChoice).Select(p => p.Id));
    }

    /// <summary>Asked once per unit only where a unit is bought, and only where there is something to ask.</summary>
    [Fact]
    public void APerUnitAnswerIsOnlyOnAPerUnitPerkThatAsks()
    {
        var perUnit = f.Rules.Perks.Where(p => p.NarrativePerUnit).ToList();

        Assert.NotEmpty(perUnit);
        Assert.All(perUnit, p =>
        {
            Assert.Equal("per_unit", p.CostType);
            Assert.NotNull(p.NarrativeConstraint);
            Assert.Empty(p.NarrativePrompts);
        });
    }

    /// <summary>A Perk split into questions keeps the sentence it was split from, for every reader that prints one.</summary>
    [Fact]
    public void APerkWithQuestionsKeepsItsConstraint()
    {
        var split = f.Rules.Perks.Where(p => p.NarrativePrompts.Count > 0).ToList();

        Assert.NotEmpty(split);
        Assert.All(split, p =>
        {
            Assert.NotNull(p.NarrativeConstraint);
            Assert.All(p.NarrativePrompts, q =>
            {
                Assert.False(string.IsNullOrWhiteSpace(q.Label));
                Assert.EndsWith("?", q.Ask, StringComparison.Ordinal);
            });
        });
    }
}
