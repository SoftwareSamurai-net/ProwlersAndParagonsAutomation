using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Validation messages are the app's error surface, and every one of them is read by a
/// player: the GM review step prints them verbatim, and both exports carry them.
///
/// <para>They were the largest remaining place the tool talked to whoever built it — file
/// names (<c>flaws.json</c>), raw snake_case ids (<c>super_senses_thermal_vision</c>), an
/// internal flag (<c>needs_review</c>) that <b>did not exist</b>, and form-field plurals
/// (<c>flaw(s)</c>). The browser front end's own tests could never have caught any of it:
/// they read <c>web/</c>, and these strings live in the engine.</para>
///
/// <para>Every message is provoked from a real sheet rather than asserted against a
/// hard-coded list, so a message added later is covered without anyone remembering to add
/// it here.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ValidationMessageTests
{
    private readonly RulesFixture _f;

    public ValidationMessageTests(RulesFixture f) => _f = f;

    /// <summary>
    /// One sheet per failure mode, chosen to make the validator say as many different things
    /// as it can. Between them these provoke every message the validator can produce except
    /// the two guarded by a rules flag no shipped entry sets.
    /// </summary>
    public static TheoryData<string> Cases() =>
    [
        "no tier", "over budget", "above cap", "too few flaws", "too many flaws",
        "unknown ids", "gear", "rankless power with ranks", "unresolved selections", "sample hero", "sample villain"
    ];

    private CharacterSheet Build(string which)
    {
        switch (which)
        {
            case "no tier":
                return new CharacterSheet();

            case "over budget":
            {
                var sheet = RulesFixture.StandardSheet();
                foreach (var a in _f.Rules.Abilities) sheet.AbilityRanks[a.Id] = 12;
                foreach (var t in _f.Rules.Talents) sheet.TalentRanks[t.Id] = 12;
                return sheet;
            }

            case "above cap":
            {
                var sheet = RulesFixture.StandardSheet();
                sheet.AbilityRanks["intellect"] = 40;
                sheet.TalentRanks["academics"] = 40;
                sheet.SelectedPowers.Add(new SelectedPower("blast", 40));
                return sheet;
            }

            case "too few flaws":
                return RulesFixture.StandardSheet();

            case "too many flaws":
            {
                var sheet = RulesFixture.StandardSheet();
                foreach (var flaw in _f.Rules.Flaws.Take(10))
                    sheet.Flaws.Add(new SelectedFlaw(flaw.Id));
                return sheet;
            }

            case "unknown ids":
            {
                var sheet = RulesFixture.StandardSheet();
                sheet.Flaws.Add(new SelectedFlaw("being_far_too_tall"));
                sheet.SelectedPowers.Add(new SelectedPower("chronomancy", 3));
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "cosmic" });
                return sheet;
            }

            case "gear":
            {
                var sheet = RulesFixture.StandardSheet();
                sheet.Gear.Add(new SelectedGear("Mystery box") { Features = [new("teleporting")] });
                sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });
                sheet.Gear.Add(new SelectedGear("Jo Sticks")
                {
                    Features = [new("upgraded")],
                    PairedUnderTwoFisted = true
                });
                return sheet;
            }

            case "rankless power with ranks":
            {
                var sheet = RulesFixture.StandardSheet();
                sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));
                sheet.SelectedPowers.Add(new SelectedPower("communications", 0));
                return sheet;
            }

            case "unresolved selections":
            {
                var sheet = RulesFixture.StandardSheet();
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2));
                sheet.SelectedPowers.Add(new SelectedPower("deflection", 4));
                return sheet;
            }

            case "sample hero":
                return SampleCharacters.Hero();

            default:
                return SampleCharacters.Villain();
        }
    }

    private List<string> MessagesFor(string which) =>
        _f.Validator.Validate(Build(which)).Issues.Select(i => i.Message).ToList();

    /// <summary>
    /// A message that names the file the rules came out of is answering a question the
    /// reader did not ask, in a vocabulary they do not have.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NoMessageNamesAFileOrAnInternalFlag(string which)
    {
        foreach (var message in MessagesFor(which))
        {
            Assert.DoesNotContain(".json", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("needs_review", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verified_fields", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// snake_case is how this project stores an id; it is not how the rulebook prints a
    /// name. A message may still quote an id the player themselves supplied — an id that is
    /// unknown has no name to print instead — so an id inside quotes is allowed and one
    /// standing in running text is not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NoMessagePrintsAnIdWhereTheRulebookHasAName(string which)
    {
        var quoted = new Regex("'[^']*'", RegexOptions.None, TimeSpan.FromSeconds(5));
        var snake = new Regex(@"\b[a-z]+(_[a-z]+)+\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var message in MessagesFor(which))
        {
            var unquoted = quoted.Replace(message, " ");
            var leak = snake.Match(unquoted);

            Assert.False(leak.Success,
                $"A validation message prints the id '{leak.Value}' rather than a name: {message}");
        }
    }

    /// <summary>
    /// "flaw(s)" and "1 ranks" are the two ways a count goes wrong, and both read as a form
    /// rather than a sentence.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void NoMessageUsesAFormFieldPlural(string which)
    {
        var bracketed = new Regex(@"\w\(s\)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        var mismatched = new Regex(@"\b1 (?!HP\b)\w+s\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var message in MessagesFor(which))
        {
            Assert.False(bracketed.IsMatch(message), $"A validation message says \"(s)\": {message}");
            Assert.False(mismatched.IsMatch(message), $"A validation message pluralises 1: {message}");
        }
    }

    /// <summary>
    /// The point of the whole exercise: a message has to say what is wrong in a way that
    /// tells the reader what to do about it. A sentence is the low bar, and it is the one
    /// the old <c>"Tier 'Iconic' is marked needs_review."</c> failed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryMessageIsASentence(string which)
    {
        foreach (var message in MessagesFor(which))
        {
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.True(char.IsUpper(message[0]), $"A validation message does not start a sentence: {message}");
            Assert.EndsWith(".", message, StringComparison.Ordinal);
            Assert.True(message.Length > 25, $"A validation message is too terse to act on: {message}");
        }
    }

    /// <summary>
    /// The sheets a player is most likely to see. Both samples are legal, so the Hero should
    /// report nothing at all and the Villain only the deliberate missing-Source warning —
    /// which means any message either produces is a regression, not a finding.
    /// </summary>
    [Fact]
    public void TheSampleCharactersReportOnlyWhatTheyMeanTo()
    {
        Assert.Empty(_f.Validator.Validate(SampleCharacters.Hero()).Issues);

        var villain = _f.Validator.Validate(SampleCharacters.Villain());
        Assert.True(villain.IsValid);
        Assert.All(villain.Issues, i => Assert.Equal("RANKLESS_POWER_WITHOUT_SOURCE", i.Code));
    }
}
