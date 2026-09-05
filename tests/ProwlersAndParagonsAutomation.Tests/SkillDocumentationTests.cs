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
    private static string SkillPath => Path.Combine(
        RulesFixture.RepoRoot, ".claude", "skills", "prowlers-and-paragons-character", "SKILL.md");

    private static string Text => File.ReadAllText(SkillPath);

    /// <summary>
    /// The document with its line breaks flowed back into spaces, for asserting on a phrase.
    /// The file is hard-wrapped, so a sentence tested for as a raw substring passes or fails on
    /// where the wrap happened to land — a test that breaks when somebody reflows a paragraph
    /// and says nothing when they delete the sentence.
    /// </summary>
    private static string Flowed =>
        new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(Text, " ");

    private readonly RulesFixture _f;

    public SkillDocumentationTests(RulesFixture f) => _f = f;

    /// <summary>
    /// The fenced <c>jsonc</c> block that holds a character, with its comments taken out.
    /// Comments are what make the block worth reading, and are not JSON.
    ///
    /// <para>Chosen by content rather than by position. The skill grew a second annotated JSON
    /// block — the report the command writes — and taking the first fence then fed a report to
    /// a reader that only accepts a character. Two documents that look alike is exactly the
    /// confusion this whole surface is built to keep apart, so the test says which it wants.
    /// </para>
    /// </summary>
    private static string ExampleCharacter()
    {
        var fences = new Regex("```jsonc\r?\n(.*?)```", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Matches(Text)
            .Select(m => m.Groups[1].Value)
            .Where(b => b.Contains("SelectedTierId", StringComparison.Ordinal))
            .ToList();

        var block = Assert.Single(fences);

        var comments = new Regex(@"\s//.*$", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return comments.Replace(block, "");
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
    /// <b>The default build is the strongest legal one, and the person trades down from it.</b>
    /// The same principle <see cref="McpQuestionPolicyTests"/> pins for the MCP server, held
    /// here because the two documents teach the same loop to two different readers and are the
    /// kind of pair that drifts: this one gained the section second, and nothing but a test on
    /// both halves keeps them saying the same thing.
    ///
    /// <para>The limits are asserted with it. Optimising is not a licence to overrule a stated
    /// weakness or to exceed the budget, and half the principle is worse than none of it.</para>
    /// </summary>
    [Fact]
    public void TheSkillSaysToBuildAtFullStrengthAndTradeDown()
    {
        Assert.Contains("strongest legal", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Trait Cap", Flowed, StringComparison.Ordinal);
        Assert.Contains("trades", Flowed, StringComparison.OrdinalIgnoreCase);

        // The limits, without which the above rebuilds somebody's character into a better one.
        Assert.Contains("overruling a weakness they stated", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("going over budget", Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b><c>--no-build</c> is the flag that makes several agents in one working tree safe,
    /// and it appeared in no document at all.</b> Plain <c>dotnet run</c> commands collide on
    /// the compiler, and the failure that comes back says nothing about the character; it was
    /// found by guessing. The reason is asserted with the flag, because a flag with no reason
    /// beside it is one nobody has a reason to type.
    ///
    /// <para>Asserted here <em>and</em> in <c>HeadlessBuildTests</c> against the program's own
    /// usage text, deliberately: this is the pair of documents that drifts, and one test over
    /// both halves is what stops the skill keeping a claim the program has dropped.</para>
    /// </summary>
    [Fact]
    public void TheSkillNamesTheFlagThatMakesConcurrentUseSafe()
    {
        Assert.Contains("--no-build", Text, StringComparison.Ordinal);
        Assert.Contains("collide", Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every flag the command's own usage prints is in the skill's table. The skill is what a
    /// model reads instead of the program, so a flag the program grew and the skill did not is
    /// a flag nobody will use — which is exactly the history of <c>--no-build</c>.
    ///
    /// <para>The list is read out of <see cref="BuildCommand.Usage"/> rather than written down
    /// here, so adding a flag to the program adds it to this test in the same commit.</para>
    /// </summary>
    [Fact]
    public void EveryFlagTheCommandPrintsIsInTheSkill()
    {
        var flags = new Regex(@"--[a-z][a-z-]+", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(BuildCommand.Usage)
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Positive control on the instrument: an extraction that has stopped matching finds
        // nothing and satisfies an "all of them are present" assertion completely.
        Assert.True(flags.Count >= 7,
            $"Only {flags.Count} flags were found in the command's usage text. Fix this "
            + "extraction rather than the assertion.");

        Assert.All(flags, flag => Assert.Contains($"`{flag}", Text, StringComparison.Ordinal));
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
