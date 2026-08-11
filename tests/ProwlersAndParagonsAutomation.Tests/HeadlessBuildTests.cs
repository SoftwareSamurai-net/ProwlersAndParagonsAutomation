using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The headless <c>build</c> command, end to end.
///
/// <para><b>This is the first test coverage the CLI has ever had</b>, and that is not a
/// coincidence: the wizard is a conversation with a terminal and there is no harness for
/// one, so every previous slice left the gap where it found it. A command that reads a file
/// and writes a report has no such excuse — it is testable by construction, which was one of
/// the reasons for choosing it over an API.</para>
///
/// <para>Everything here drives <see cref="BuildCommand.Run"/> through its writers rather
/// than shelling out to <c>dotnet run</c>, so a failure names a line rather than a process.
/// The one thing that costs is <see cref="RulesFixture"/>, and it is shared.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class HeadlessBuildTests : IDisposable
{
    private readonly RulesFixture _f;
    private readonly string _scratch;

    public HeadlessBuildTests(RulesFixture f)
    {
        _f = f;
        _scratch = Path.Combine(Path.GetTempPath(), "pp-headless-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); }
        catch (IOException) { /* a temp directory that outlives the run is not a test failure */ }
        catch (UnauthorizedAccessException) { }
    }

    // ── Running it ────────────────────────────────────────────────────────

    private sealed record Run(int ExitCode, string StdOut, string StdErr)
    {
        /// <summary>
        /// The report, parsed. Asserting on the parsed document rather than on the text is
        /// the same rule the rendering tests learned the hard way: a substring check against
        /// a whole document is satisfied by something other than the thing being tested.
        /// </summary>
        public JsonNode Report => JsonNode.Parse(StdOut)
            ?? throw new InvalidOperationException($"The command wrote no JSON report:\n{StdOut}");

        public JsonArray Issues => Report["issues"]!.AsArray();

        public JsonNode? Issue(string code) =>
            Issues.FirstOrDefault(i => (string?)i!["code"] == code);
    }

    /// <summary>
    /// The arguments are the ones after the verb, which is what <c>Program</c> passes on.
    /// Named rather than overloaded on a leading string: the overload that took standard
    /// input first silently ate the first argument of every other call.
    /// </summary>
    private Run Invoke(params string[] args) => InvokeWithInput("", args);

    private Run InvokeWithInput(string stdin, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = new BuildCommand(_f.Rules, _f.Costs, _f.Derived, _f.Validator)
            .Run(args, RulesFixture.RepoRoot, stdout, stderr, new StringReader(stdin));

        return new Run(exit, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Writes a character to the scratch directory and returns its path.</summary>
    private string File_(string json)
    {
        var path = Path.Combine(_scratch, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private string SampleHeroFile() => File_(CharacterSheetJson.Write(SampleCharacters.Hero()));

    // ── The three exits ───────────────────────────────────────────────────

    /// <summary>
    /// A legal character is accepted, and the figures in the report are the engine's own.
    /// Asserting them against the calculators rather than against constants is what keeps
    /// this a test of the command instead of a second, staler copy of the rules.
    /// </summary>
    [Fact]
    public void ALegalCharacterExitsZeroAndReportsTheEnginesFigures()
    {
        var hero = SampleCharacters.Hero();
        var run  = Invoke("--from", File_(CharacterSheetJson.Write(hero)), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.True((bool)run.Report["ok"]!);
        Assert.DoesNotContain(run.Issues, i => (string?)i!["severity"] == "error");

        Assert.Equal(_f.Costs.TotalCost(hero), (int)run.Report["hero_points"]!["spent"]!);
        Assert.Equal(_f.Derived.CalculateEdge(hero), (int)run.Report["derived"]!["edge"]!);
        Assert.Equal(_f.Derived.CalculateHealth(hero), (int)run.Report["derived"]!["health"]!);
        Assert.Equal(_f.Derived.CalculateResolve(hero), (int)run.Report["derived"]!["resolve"]!);
        Assert.Equal(hero.Name, (string?)run.Report["character"]!["name"]);
    }

    /// <summary>
    /// The whole reason the command exits non-zero: a caller driving it in a loop has to be
    /// able to tell "this character is finished" from "this character is over budget" without
    /// reading English.
    /// </summary>
    [Fact]
    public void AnOverBudgetCharacterExitsOneAndSaysBySoMuch()
    {
        // Everything at the Trait Cap: legal rank by rank, and far past what 125 HP buys.
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        foreach (var ability in _f.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 12;
        foreach (var talent in _f.Rules.Talents) sheet.TalentRanks[talent.Id] = 12;

        var run = Invoke("--from", File_(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.False((bool)run.Report["ok"]!);

        var issue = run.Issue("HP_BUDGET_EXCEEDED");
        Assert.NotNull(issue);
        Assert.Equal(_f.Costs.TotalCost(sheet), (int)issue["value"]!);
        Assert.Equal(_f.Rules.GetTier("standard")!.HeroPoints, (int)issue["limit"]!);
    }

    /// <summary>
    /// An id the caller invented. The figures it makes unanswerable come back null rather
    /// than taking the report down with them — the engine throws on an unknown Power id
    /// rather than guessing, and this command has to survive that, because a caller
    /// proposing characters will produce one sooner or later.
    /// </summary>
    [Fact]
    public void AnUnknownIdIsReportedRatherThanThrown()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        sheet.SelectedPowers.Add(new SelectedPower("chronomancy", 3));

        var run = Invoke("--from", File_(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);

        var issue = run.Issue("UNKNOWN_POWER");
        Assert.NotNull(issue);
        Assert.Equal("chronomancy", (string?)issue["subject_id"]);
        Assert.Equal("power", (string?)issue["subject_kind"]);

        // Unanswerable, not zero. Reporting a cost of 0 for a character that cannot be
        // costed would be a lie a caller could act on.
        Assert.Null(run.Report["hero_points"]!["spent"]);
    }

    /// <summary>
    /// A character the engine can read but not price — a variable-cost Power with no variant
    /// chosen. It is a half-finished character rather than a broken one, so it is reported
    /// with the choice attached rather than refused as unreadable.
    /// </summary>
    [Fact]
    public void AnUnpriceableSelectionIsAFindingWithItsChoicesAttached()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        sheet.SelectedPowers.Add(new SelectedPower("omni_power", 2));

        var run = Invoke("--from", File_(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);

        var issue = run.Issue("POWER_VARIANT_NOT_CHOSEN");
        Assert.NotNull(issue);
        Assert.Equal("omni_power", (string?)issue["subject_id"]);

        // The options are the keys the data accepts, not the prose the message sets them as.
        var options = issue["options"]!.AsArray().Select(o => (string?)o).ToList();
        Assert.NotEmpty(options);
        Assert.Equal(_f.Rules.GetPower("omni_power")!.CostVariants!.Keys.Order(), options.Order());
        Assert.Null(run.Report["hero_points"]!["spent"]);
    }

    // ── Input that is not a character ─────────────────────────────────────

    [Fact]
    public void MalformedJsonExitsTwoAndStillWritesOneReport()
    {
        var run = Invoke("--from", File_("{\"SelectedTierId\":"), "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
        Assert.False((bool)run.Report["ok"]!);
    }

    [Fact]
    public void AMissingFileExitsTwoRatherThanThrowing()
    {
        var run = Invoke("--from", Path.Combine(_scratch, "no-such-character.json"), "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    /// <summary>
    /// <c>null</c> is well-formed JSON and deserializes to no character at all. Left
    /// unguarded it would arrive as an empty sheet — which costs nothing, breaks the flaw
    /// minimum, and would report as an ordinary illegal character rather than as a file with
    /// nothing in it.
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("   ")]
    public void AFileWithNoCharacterInItIsNotAnEmptyCharacter(string contents)
    {
        var run = Invoke("--from", File_(contents), "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    [Fact]
    public void AnUnknownOptionIsRefusedRatherThanIgnored()
    {
        var run = Invoke("--from", SampleHeroFile(), "--outt", _scratch);

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
        Assert.Contains("--outt", run.Issue("BAD_ARGUMENTS")!["message"]!.ToString(), StringComparison.Ordinal);

        // The usage goes to stderr, so stdout stays one JSON document whatever happened.
        Assert.Contains("--from", run.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueFlagWithNoValueIsRefused()
    {
        var run = Invoke("--from");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
    }

    [Fact]
    public void NoCharacterAtAllIsRefusedWithTheWayToPassOne()
    {
        var run = Invoke("--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.Contains("--from", run.Issue("BAD_ARGUMENTS")!["message"]!.ToString(), StringComparison.Ordinal);
    }

    // ── The report is one document, always ────────────────────────────────

    /// <summary>
    /// A caller reads standard output whole. If any exit could leave it empty, or leave a
    /// warning printed above the JSON, the caller would need to filter it — and the first
    /// thing to be filtered wrongly would be a report it needed.
    /// </summary>
    [Fact]
    public void EveryExitWritesExactlyOneJsonDocumentOnStandardOutput()
    {
        var runs = new[]
        {
            Invoke("--from", SampleHeroFile(), "--no-export"),                    // 0
            Invoke("--from", File_("{}"), "--no-export"),                         // 1
            Invoke("--from", File_("{ nope"), "--no-export"),                     // 2
            Invoke("--nonsense")                                                  // 2
        };

        Assert.Equal([BuildCommand.Ok, BuildCommand.CharacterIllegal,
                      BuildCommand.InputUnusable, BuildCommand.InputUnusable],
                     runs.Select(r => r.ExitCode));

        foreach (var run in runs)
        {
            var document = JsonDocument.Parse(run.StdOut);      // throws on trailing content
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            Assert.Equal(run.ExitCode, document.RootElement.GetProperty("exit_code").GetInt32());
            Assert.Equal(run.ExitCode == BuildCommand.Ok, document.RootElement.GetProperty("ok").GetBoolean());
        }
    }

    /// <summary>The three exits have to be three different numbers to be worth having.</summary>
    [Fact]
    public void TheThreeExitCodesAreDistinct()
    {
        Assert.Equal(3, new[] { BuildCommand.Ok, BuildCommand.CharacterIllegal, BuildCommand.InputUnusable }
            .Distinct().Count());
    }

    // ── Standard input, and the files it writes ───────────────────────────

    [Fact]
    public void ACharacterCanArriveOnStandardInput()
    {
        var run = InvokeWithInput(CharacterSheetJson.Write(SampleCharacters.Hero()),
                                  "--from", "-", "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
    }

    [Fact]
    public void NothingOnStandardInputIsSaidRatherThanTreatedAsACharacter()
    {
        var run = InvokeWithInput("", "--from", "-", "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    /// <summary>
    /// The exports are the deliverable — a sheet to hand to a player — so the report has to
    /// name files that are really there and really have the character in them.
    /// </summary>
    [Fact]
    public void TheExportsAreWrittenWhereTheCallerAskedAndHoldTheCharacter()
    {
        var hero = SampleCharacters.Hero();
        var out_ = Path.Combine(_scratch, "exports");

        var run = Invoke("--from", File_(CharacterSheetJson.Write(hero)), "--out", out_);

        Assert.Equal(BuildCommand.Ok, run.ExitCode);

        var text = (string)run.Report["exports"]!["text"]!;
        var json = (string)run.Report["exports"]!["json"]!;

        Assert.True(File.Exists(text));
        Assert.True(File.Exists(json));
        Assert.Contains(hero.Name, File.ReadAllText(text), StringComparison.Ordinal);
        Assert.Equal(_f.Costs.TotalCost(hero),
            JsonNode.Parse(File.ReadAllText(json))!["hp_budget"]!["spent"]!.GetValue<int>());
    }

    /// <summary>
    /// An illegal character still gets its sheet. Seeing what was built is how a caller works
    /// out what to give up, and a tool that withholds the evidence when the news is bad is
    /// worse than one that never had it.
    /// </summary>
    [Fact]
    public void AnIllegalCharacterStillGetsItsExports()
    {
        var sheet = RulesFixture.StandardSheet();     // no flaws: illegal at creation
        var run   = Invoke("--from", File_(CharacterSheetJson.Write(sheet)),
                           "--out", Path.Combine(_scratch, "illegal"));

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.True(File.Exists((string)run.Report["exports"]!["text"]!));
    }

    [Fact]
    public void NoExportWritesNothingAtAll()
    {
        var out_ = Path.Combine(_scratch, "unwanted");

        var run = Invoke("--from", SampleHeroFile(), "--out", out_, "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Null(run.Report["exports"]);
        Assert.False(Directory.Exists(out_));
    }

    [Fact]
    public void HelpIsWrittenToStandardOutputAndNamesTheCommand()
    {
        var run = Invoke("--help");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Contains("--from", run.StdOut, StringComparison.Ordinal);
        Assert.Contains(BuildCommand.Verb, run.StdOut, StringComparison.Ordinal);
    }

    // ── The trap that would make every character legal ────────────────────

    /// <summary>
    /// <b>The submitted character has to arrive whole.</b> <see cref="CharacterSheet"/>
    /// exposes its collections as get-only properties, and a deserializer left to itself
    /// skips them rather than filling them — silently. Every ability, talent, power, perk,
    /// flaw and piece of gear would be dropped, and what arrived would be an empty character:
    /// free, inside any budget, under every cap.
    ///
    /// <para>That is the worst failure this command has available, because it does not look
    /// like one. The tool would answer "legal, 0 HP" to everything, confidently, for ever.
    /// So it is checked by costing what came back rather than by counting fields — a field
    /// count goes stale the first time somebody adds one.</para>
    /// </summary>
    [Fact]
    public void EverySectionOfASubmittedCharacterSurvivesTheJourney()
    {
        var hero = SampleCharacters.Hero();
        var run  = Invoke("--from", File_(CharacterSheetJson.Write(hero)),
                          "--out", Path.Combine(_scratch, "whole"));

        var spent = (int)run.Report["hero_points"]!["spent"]!;
        Assert.Equal(_f.Costs.TotalCost(hero), spent);
        Assert.True(spent > 0, "A character that costs nothing did not arrive.");

        // Read back out of the command's own export, so this is what the command saw rather
        // than what a second round trip on the side would have produced.
        var built = JsonNode.Parse(File.ReadAllText((string)run.Report["exports"]!["json"]!))!;

        Assert.Equal(hero.SelectedPowers.Count, built["powers"]!.AsArray().Count);
        Assert.Equal(hero.Flaws.Count, built["flaws"]!.AsArray().Count);
        Assert.Equal(hero.Perks.Count, built["perks"]!.AsArray().Count);
        Assert.Equal(hero.Gear.Count, built["gear"]!.AsArray().Count);

        // Dictionaries are the same trap in a second shape, and the two that carry Sources
        // are invisible to cost — nothing else here would notice them being dropped.
        Assert.Equal(hero.AbilitySources.Count,
            built["abilities"]!.AsArray().Count(a => a!["source"] is not null));
        Assert.Equal(hero.AbilityRanks.Values.Sum(),
            built["abilities"]!.AsArray().Sum(a => (int)a!["rank"]!));
        Assert.Equal(hero.TalentRanks.Values.Sum(),
            built["talents"]!.AsArray().Sum(t => (int)t!["rank"]!));
    }

    // ── The other front end has to keep working ───────────────────────────

    /// <summary>
    /// The message the wizard prints when its terminal cannot be read. It replaced a stack
    /// trace, and the thing that makes it an answer rather than a dead end is that it names
    /// the command that does work this way.
    /// </summary>
    [Fact]
    public void TheNonInteractiveMessageNamesTheCommandThatNeedsNoTerminal()
    {
        Assert.Contains(BuildCommand.Verb, InteractiveTerminal.UnavailableMessage, StringComparison.Ordinal);
        Assert.Contains("--from", InteractiveTerminal.UnavailableMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", InteractiveTerminal.UnavailableMessage, StringComparison.Ordinal);
    }
}
