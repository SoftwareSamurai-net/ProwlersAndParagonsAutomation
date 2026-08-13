using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Mcp;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The question policy — <c>mcp/QUESTION-POLICY.md</c>, which is what the
/// <c>creation_guide</c> tool answers with.
///
/// <para><b>It is the deliverable of this slice, not documentation of it.</b> The transport
/// was the easy half; which two or three questions are worth asking is the part that decides
/// whether this is a conversation or a questionnaire wrapped around a wizard that already
/// exists. So the document is held to the same standard as code: its example character is fed
/// to the same strict reader a submitted character goes through, and the claims that keep the
/// ordering honest are asserted rather than trusted.</para>
///
/// <para>This is the same job <see cref="SkillDocumentationTests"/> does for the skill, and
/// for the same reason: a schema described in prose rots silently, and the failure lands on
/// whoever follows it.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class McpQuestionPolicyTests
{
    private readonly RulesFixture _f;

    public McpQuestionPolicyTests(RulesFixture f) => _f = f;

    private static string Text => QuestionPolicy.Text;

    /// <summary>
    /// The document with its line breaks flowed back into spaces, for asserting on a phrase.
    /// The file is hard-wrapped, so a sentence tested for as a substring passes or fails on
    /// where the wrap happened to land — which is a test that breaks when somebody reflows a
    /// paragraph and says nothing when they delete the sentence.
    /// </summary>
    private static string Flowed =>
        new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(Text, " ");

    /// <summary>
    /// The document as the tool serves it. Reading the embedded copy rather than the file on
    /// disk is deliberate: the file could be perfect and the csproj could have stopped
    /// embedding it, and then every conversation starts with an empty guide.
    /// </summary>
    [Fact]
    public void TheGuideToolAnswersWithTheDocumentOnDisk()
    {
        var tools = new CharacterTools(_f.Rules, _f.Costs, _f.Derived, _f.Validator);

        // <b>Against the file, not against itself.</b> This compared QuestionPolicy.Text with
        // CreationGuide(), which returns QuestionPolicy.Text — a comparison that cannot fail,
        // leaving the claim that the tool serves *this document* asserted nowhere. Pointing
        // the csproj at any other long markdown file passed.
        var onDisk = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "mcp", "QUESTION-POLICY.md"));

        // Line endings are the one difference allowed: git checks this file out with the
        // platform's, and an embedded resource keeps whatever was on disk at build time.
        Assert.Equal(Normalised(onDisk), Normalised(tools.CreationGuide()));
        Assert.True(Text.Length > 3000, "The embedded question policy is a stub.");
    }

    private static string Normalised(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// <b>All four questions are named.</b> The policy is that these four change the build and
    /// that almost nothing else does; losing one of them from the document loses it from every
    /// conversation, and nothing else would notice.
    /// </summary>
    [Theory]
    [InlineData("tier")]
    [InlineData("one Power or several")]
    [InlineData("ordinary at")]
    [InlineData("Source")]
    public void TheFourQuestionsThatChangeTheBuildAreNamed(string question)
    {
        Assert.Contains(question, Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And the restraint that goes with them. Four questions worth asking is only half the
    /// policy — the other half is that a description under-determines dozens of fields and
    /// almost all of them should be decided silently and shown. Without that sentence the
    /// document reads as a licence to ask about everything it mentions.
    /// </summary>
    [Fact]
    public void TheGuideSaysToAskFewQuestionsAndDecideTheRest()
    {
        Assert.Contains("two or three questions", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ask at most three", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("decide silently", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("questionnaire", Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The ordering, in the document that teaches the loop. Every slice that has drifted here
    /// produced a confidently wrong number, so the sentence that forbids it is asserted.
    /// </summary>
    [Fact]
    public void TheGuideSaysWhoDecides()
    {
        Assert.Contains("The engine decides", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CharacterServer.CheckCharacterTool, Text, StringComparison.Ordinal);
        Assert.Contains("reported, never repaired", Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The two traps this surface was built knowing about: a description that cannot be
    /// afforded, and one that names something the rulebook has no Power for. Both are in the
    /// document because both are answered by saying something true rather than by building
    /// something.
    /// </summary>
    [Fact]
    public void TheGuideAnswersTheTwoDescriptionsThatHaveNoGoodBuild()
    {
        Assert.Contains("remaining", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offer the trade", Flowed, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("no Power for it", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never invent a Power id", Flowed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And what the search's own flag means, in the document that tells the assistant to act
    /// on it. `nothing_matched_by_name` is true of "he can fly", which the rulebook answers
    /// with Flight — a guide that reads the flag as "there is no Power" turns the tool's most
    /// consequential field into a refusal to build something buildable.
    /// </summary>
    [Fact]
    public void TheGuideSaysWhatTheSearchFlagIsWorth()
    {
        Assert.Contains("nothing_matched_by_name", Flowed, StringComparison.Ordinal);
        Assert.Contains("not that the rulebook has nothing", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("found: 0", Flowed, StringComparison.Ordinal);
    }

    /// <summary>
    /// Villains. Ch.9 builds them by the Hero rules and the engine is never told which is
    /// being built, so <c>HP_BUDGET_EXCEEDED</c> arrives on a Villain exactly as it does on a
    /// Hero — and for a Villain it is the GM's call rather than a rule broken. Without this
    /// the assistant reports every Villain over the tier's points as illegal.
    /// </summary>
    [Fact]
    public void TheGuideSaysWhatToDoWithAVillain()
    {
        Assert.Contains("Villain", Text, StringComparison.Ordinal);
        Assert.Contains("no Hero Point budget", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HP_BUDGET_EXCEEDED", Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A null figure has three causes and they want different things. The guide used to give
    /// one instruction — "fix the errors and the figures appear" — which is wrong for the two
    /// that have no errors to fix, and one of those is a loop with no way out.
    /// </summary>
    [Fact]
    public void TheGuideTellsTheThreeNullFiguresApart()
    {
        Assert.Contains("hero_points.spent` is null", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no usable tier", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fault in the tool", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("engine_could_not_answer", Text, StringComparison.Ordinal);
    }

    /// <summary>Every tool the guide names is one the server actually serves.</summary>
    [Fact]
    public void EveryToolTheGuideNamesIsOneTheServerServes()
    {
        string[] served =
        [
            CharacterServer.CreationGuideTool, CharacterServer.ListOptionsTool,
            CharacterServer.SearchPowersTool, CharacterServer.PowerDetailTool,
            CharacterServer.CheckCharacterTool, CharacterServer.CharacterSheetTool
        ];

        Assert.All(served, tool =>
            Assert.Contains(tool, Text, StringComparison.Ordinal));

        // And the reverse: a tool named in the guide that no longer exists sends an assistant
        // after something that answers "unknown tool" — which reads as the server being broken.
        var named = new Regex(@"`(?<tool>[a-z_]+)`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(Text)
            .Select(m => m.Groups["tool"].Value)
            .Where(t => t.EndsWith("_character", StringComparison.Ordinal)
                     || t.EndsWith("_powers", StringComparison.Ordinal)
                     || t.EndsWith("_options", StringComparison.Ordinal)
                     || t.EndsWith("_guide", StringComparison.Ordinal)
                     || t.EndsWith("_detail", StringComparison.Ordinal)
                     || t.EndsWith("_sheet", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(named);
        Assert.All(named, tool => Assert.Contains(tool, served));
    }

    // ── The example character ─────────────────────────────────────────────

    /// <summary>
    /// The fenced <c>jsonc</c> block that holds a character, with its comments taken out.
    /// Comments are what make the block worth reading, and are not JSON.
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
    /// <b>The example has to be a character the tools accept.</b> Reading it strictly is the
    /// point: an unknown field is refused, so this fails the moment the guide names a field
    /// the engine does not have — which is exactly what a rename would leave behind.
    /// </summary>
    [Fact]
    public void TheExampleIsACharacterTheToolsWouldAccept()
    {
        var sheet = CharacterSheetJson.Read(ExampleCharacter(), strict: true);

        Assert.NotNull(sheet);

        var validation = _f.Validator.Validate(sheet);
        Assert.True(validation.IsValid,
            "The guide's example character is not legal: "
            + string.Join(" ", validation.Errors.Select(e => e.Message)));
    }

    /// <summary>
    /// And every id in it is one the rules have. A file that parses and validates could still
    /// teach a Power id that does not exist — the validator would report it, but this says so
    /// at the line rather than through a character somebody else proposed.
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

        Assert.All(sheet.AbilityModifiers.Keys, id => Assert.NotNull(_f.Rules.GetAbility(id)));
        Assert.All(sheet.AbilityModifiers.Values.SelectMany(m => m),
            m => Assert.True(_f.Rules.GetPro(m.Id) is not null || _f.Rules.GetCon(m.Id) is not null,
                $"{m.Id} is neither a Pro nor a Con."));

        Assert.All(sheet.Gear.SelectMany(g => g.Features),
            f => Assert.NotNull(_f.Rules.GetGearFeature(f.FeatureId)));
    }

    /// <summary>
    /// The example carries a custom gear feature, because that shape is the one a proposer is
    /// most likely to get wrong: a feature is <c>{FeatureId, GradeKey}</c> and a Pro two lines
    /// above it is <c>{Id, VariantKey}</c>, reading is strict, and the wrong guess comes back
    /// as an unreadable character with no hint which spelling was wanted.
    ///
    /// <para>It also makes the id check below mean something. It was <c>"Features": []</c>,
    /// so <c>Assert.All</c> over the example's gear features iterated zero times.</para>
    /// </summary>
    [Fact]
    public void TheExampleShowsWhatACustomGearFeatureLooksLike()
    {
        var sheet = CharacterSheetJson.Read(ExampleCharacter(), strict: true)!;

        var feature = Assert.Single(sheet.Gear.SelectMany(g => g.Features));

        Assert.NotNull(_f.Rules.GetGearFeature(feature.FeatureId));
        Assert.NotNull(feature.GradeKey);

        // A graded feature is the one that needs the key, and showing an ungraded one with a
        // key would teach the opposite of the rule the guide states beside it.
        var graded = _f.Rules.GetGearFeature(feature.FeatureId)!;
        Assert.Equal("flat_variable", graded.CostType);
        Assert.Contains(feature.GradeKey!, graded.CostRange!.Keys);
    }

    /// <summary>
    /// The example writes out all eighteen Traits, because that is the trap the document warns
    /// about two paragraphs later — and an example that broke its own rule would teach the
    /// mistake more loudly than the paragraph corrects it.
    /// </summary>
    [Fact]
    public void TheExampleWritesOutEverySixAbilitiesAndTwelveTalents()
    {
        var sheet = CharacterSheetJson.Read(ExampleCharacter(), strict: true)!;

        Assert.Equal(_f.Rules.Abilities.Count, sheet.AbilityRanks.Count);
        Assert.Equal(_f.Rules.Talents.Count, sheet.TalentRanks.Count);
    }
}
