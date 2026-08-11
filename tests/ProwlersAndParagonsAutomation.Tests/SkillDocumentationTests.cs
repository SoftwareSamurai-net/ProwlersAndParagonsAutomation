using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The skill that teaches a model to build a character.
///
/// <para><b>It is documentation of a schema, which is the kind of documentation that rots
/// silently.</b> A field renamed in <see cref="CharacterSheet"/> leaves the skill describing
/// a shape nothing accepts, and the failure lands on whoever follows it — as a character that
/// is refused, or worse, one that arrives with a section quietly missing. So the example in
/// the skill is not an illustration here: it is fed to the same reader the command uses, and
/// held to the same standard as a character a player submitted.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class SkillDocumentationTests
{
    private static string Path_ => System.IO.Path.Combine(
        RulesFixture.RepoRoot, ".claude", "skills", "prowlers-and-paragons-character", "SKILL.md");

    private static string Text => File.ReadAllText(Path_);

    private readonly RulesFixture _f;

    public SkillDocumentationTests(RulesFixture f) => _f = f;

    /// <summary>
    /// The one fenced <c>jsonc</c> block, with its comments taken out. Comments are what make
    /// the block worth reading, and are not JSON; nothing else in the file is a character.
    /// </summary>
    private static string ExampleCharacter()
    {
        var fence = new Regex("```jsonc\r?\n(.*?)```", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        var block = fence.Match(Text);

        Assert.True(block.Success, "The skill no longer shows an example character.");

        var comments = new Regex(@"\s//.*$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return comments.Replace(block.Groups[1].Value, "");
    }

    /// <summary>
    /// <b>The example has to be a character the command accepts.</b> Reading it strictly is
    /// the point: an unknown field is refused, so this fails the moment the skill names a
    /// field the engine does not have — which is exactly what a rename would leave behind.
    /// </summary>
    [Fact]
    public void TheExampleIsACharacterTheCommandWouldAccept()
    {
        var sheet = CharacterSheetJson.Read(ExampleCharacter(), strict: true);

        Assert.NotNull(sheet);
        Assert.True(_f.Validator.Validate(sheet).IsValid,
            "The skill's example character is not legal: " +
            string.Join(" ", _f.Validator.Validate(sheet).Errors.Select(e => e.Message)));
    }

    /// <summary>
    /// And every id in it is one the rules have. A file that parses and validates could still
    /// be teaching a Power id that does not exist — the validator would report it, but this
    /// test says so at the line rather than through a character somebody else submitted.
    /// </summary>
    [Fact]
    public void EveryIdInTheExampleIsOneTheRulesHave()
    {
        var sheet = CharacterSheetJson.Read(ExampleCharacter(), strict: true)!;

        Assert.NotNull(_f.Rules.GetTier(sheet.SelectedTierId!));
        Assert.Contains(_f.Rules.CreationRules.OptionalPackages, p => p.Id == sheet.SelectedPackageId);

        Assert.All(sheet.AbilityRanks.Keys, id => Assert.NotNull(_f.Rules.GetAbility(id)));
        Assert.All(sheet.TalentRanks.Keys, id => Assert.NotNull(_f.Rules.GetTalent(id)));
        Assert.All(sheet.AbilitySources, s => Assert.NotNull(_f.Rules.GetSource(s.Value)));
        Assert.All(sheet.TalentSources, s => Assert.NotNull(_f.Rules.GetSource(s.Value)));
        Assert.All(sheet.Perks, p => Assert.NotNull(_f.Rules.GetPerk(p.PerkId)));
        Assert.All(sheet.Flaws, f => Assert.NotNull(_f.Rules.GetFlaw(f.FlawId)));

        Assert.All(sheet.SelectedPowers, sp =>
        {
            Assert.NotNull(_f.Rules.GetPower(sp.PowerId));
            Assert.NotNull(_f.Rules.GetSource(sp.SourceId!));
            Assert.All(sp.Pros, p => Assert.NotNull(_f.Rules.GetPro(p.Id)));
            Assert.All(sp.Cons, c => Assert.NotNull(_f.Rules.GetCon(c.Id)));
        });

        Assert.All(sheet.Gear.SelectMany(g => g.Features),
            f => Assert.NotNull(_f.Rules.GetGearFeature(f.FeatureId)));
    }

    /// <summary>
    /// The skill's whole claim on the reader is that the engine decides and the model does
    /// not. Losing that sentence would leave a document that reads like an invitation to
    /// work the costs out, which is the one thing it exists to prevent.
    /// </summary>
    [Fact]
    public void TheSkillStillSaysWhoDecides()
    {
        Assert.Contains("The engine decides", Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(BuildCommand.Verb, Text, StringComparison.Ordinal);
        Assert.Contains("--from", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every rules file the skill sends a reader to has to be one that ships. This is the
    /// same failure as the example's field names, in the half of the document that is a map
    /// rather than a character.
    /// </summary>
    [Fact]
    public void EveryRulesFileTheSkillNamesIsOneThatShips()
    {
        var named = new Regex(@"`(\w+\.json)`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(Text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(named);
        Assert.All(named, file => Assert.Contains(file, RulesRepository.DataFileNames));
    }

    /// <summary>
    /// The exit codes a caller branches on, stated in the document and in the program. Three
    /// numbers are cheap to write down and cheap to get wrong.
    /// </summary>
    [Fact]
    public void TheExitCodesInTheSkillAreTheOnesTheCommandReturns()
    {
        Assert.Contains($"**Exit {BuildCommand.Ok}**", Text, StringComparison.Ordinal);
        Assert.Contains($"**Exit {BuildCommand.CharacterIllegal}**", Text, StringComparison.Ordinal);
        Assert.Contains($"**Exit {BuildCommand.InputUnusable}**", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The subject kinds are what a repair loop switches on, and the skill prints the list.
    /// A kind added to the engine and not to the document is a repair nobody writes.
    /// </summary>
    [Fact]
    public void EverySubjectKindTheEngineCanReportIsInTheSkill()
    {
        // Asked of the command, never re-derived here. This test used to convert the enum
        // itself, which meant it validated the document against a second copy of the
        // conversion — so dropping the underscore shipped `gearfeature` on the wire with the
        // document still saying `gear_feature`, and both stayed green.
        foreach (var kind in Enum.GetValues<ValidationSubject>())
        {
            if (kind == ValidationSubject.None) continue;

            Assert.Contains($"`{BuildCommand.SubjectKindName(kind)}`", Text, StringComparison.Ordinal);
        }
    }
}
