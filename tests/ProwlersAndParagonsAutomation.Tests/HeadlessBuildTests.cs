using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The headless <c>build</c> command, end to end.
///
/// <para><b>This is the first of the CLI that has been driven end to end</b>, and that is not
/// a coincidence: the wizard is a conversation with a terminal and there is no harness for
/// one, so every previous slice left the gap where it found it. A command that reads a file
/// and writes a report has no such excuse — it is testable by construction, which was one of
/// the reasons for choosing it over an API. It narrows that gap rather than closing it: the
/// wizard's steps still have none, and the exporter was already exercised by
/// <see cref="SourceTests"/>.</para>
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
        /// <summary>Whether the wizard was started. Only <c>Dispatch</c> sets it.</summary>
        public bool WizardRan { get; init; }

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
    private string CharacterFile(string json)
    {
        var path = Path.Combine(_scratch, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private string SampleHeroFile() => CharacterFile(CharacterSheetJson.Write(SampleCharacters.Hero()));

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
        var run  = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)), "--no-export");

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

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

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

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

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

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

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

    /// <summary>
    /// <b>Every figure the skill tells a caller to branch on.</b> None of these was asserted,
    /// and each could be replaced by a constant or a null with the whole suite green — the
    /// budget, what is left of it, the cap a rank is measured against, and the package. A
    /// caller reading `remaining` to decide what to give up would have read 0 for ever.
    /// </summary>
    [Fact]
    public void TheReportCarriesEveryFigureACallerBranchesOn()
    {
        var hero = SampleCharacters.Hero();
        var run  = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)), "--no-export");
        var tier = _f.Rules.GetTier(hero.SelectedTierId!)!;

        Assert.Equal(tier.HeroPoints, (int)run.Report["hero_points"]!["budget"]!);
        Assert.Equal(tier.HeroPoints - _f.Costs.TotalCost(hero),
                     (int)run.Report["hero_points"]!["remaining"]!);
        Assert.Equal(tier.TraitCapRank, (int)run.Report["trait_cap"]!);
        Assert.Equal(hero.SelectedPackageId, (string?)run.Report["character"]!["package"]);
    }

    /// <summary>
    /// Over budget means <c>remaining</c> is negative by the overspend. It is how a caller
    /// decides how much to give up, and a constant 0 there would read as "you are exactly on
    /// budget" on every failing pass.
    /// </summary>
    [Fact]
    public void RemainingGoesNegativeByTheOverspend()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        foreach (var a in _f.Rules.Abilities) sheet.AbilityRanks[a.Id] = 12;
        foreach (var t in _f.Rules.Talents) sheet.TalentRanks[t.Id] = 12;

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");
        var over = _f.Costs.TotalCost(sheet) - _f.Rules.GetTier("standard")!.HeroPoints;

        Assert.True(over > 0);
        Assert.Equal(-over, (int)run.Report["hero_points"]!["remaining"]!);
    }

    /// <summary>
    /// <b>Warnings have to survive into the report.</b> Filtering them out left every test
    /// green: nothing read a warning from the wire, so a legal character's missing Source, an
    /// Iconic budget note and an unverified Power could all have vanished silently.
    /// </summary>
    [Fact]
    public void WarningsReachTheReportOnALegalCharacter()
    {
        var villain = SampleCharacters.Villain();   // legal, with one deliberate warning
        var run     = Invoke("--from", CharacterFile(CharacterSheetJson.Write(villain)), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.True((bool)run.Report["ok"]!);

        var warnings = run.Issues.Where(i => (string?)i!["severity"] == "warning").ToList();
        Assert.NotEmpty(warnings);
        Assert.Equal(_f.Validator.Validate(villain).Warnings.Count(), warnings.Count);
    }

    /// <summary>
    /// A multi-word subject kind, on the wire, as the skill publishes it. Every other
    /// assertion on this field used a single-word value, so dropping the underscore shipped
    /// <c>gearfeature</c> and nothing noticed — including the test that checks the document,
    /// which converted the enum itself instead of asking the command.
    /// </summary>
    [Fact]
    public void AMultiWordSubjectKindKeepsItsUnderscoreOnTheWire()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw(_f.Rules.Flaws[0].Id));
        sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });

        var run   = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");
        var issue = run.Issue("GEAR_FEATURE_NEEDS_GRADE");

        Assert.NotNull(issue);
        Assert.Equal("gear_feature", (string?)issue["subject_kind"]);
        Assert.Equal(BuildCommand.SubjectKindName(ValidationSubject.GearFeature),
                     (string?)issue["subject_kind"]);

        // owner_id was asserted on the engine's object and never on the wire, so it could have
        // been dropped from the report alone.
        Assert.Equal("Pistol", (string?)issue["owner_id"]);
        Assert.Equal("accurate", (string?)issue["subject_id"]);
    }

    // ── Input that is not a character ─────────────────────────────────────

    [Fact]
    public void MalformedJsonExitsTwoAndStillWritesOneReport()
    {
        var run = Invoke("--from", CharacterFile("{\"SelectedTierId\":"), "--no-export");

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
        var run = Invoke("--from", CharacterFile(contents), "--no-export");

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
            Invoke("--from", CharacterFile("{}"), "--no-export"),                         // 1
            Invoke("--from", CharacterFile("{ nope"), "--no-export"),                     // 2
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
        var requested = Path.Combine(_scratch, "exports");

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)), "--out", requested);

        Assert.Equal(BuildCommand.Ok, run.ExitCode);

        var text = (string)run.Report["exports"]!["text"]!;
        var json = (string)run.Report["exports"]!["json"]!;

        Assert.True(File.Exists(text));
        Assert.True(File.Exists(json));

        // Under the directory that was ASKED for. Asserting only that the reported path exists
        // tests nothing about --out: the report says wherever it wrote, so ignoring the flag
        // entirely and writing to the default output/ passed this test's own name.
        Assert.Equal(Path.GetFullPath(requested), Path.GetDirectoryName(text));
        Assert.Equal(Path.GetFullPath(requested), Path.GetDirectoryName(json));

        // And absolute, whatever form --out took, so a caller can resolve them from anywhere.
        Assert.True(Path.IsPathFullyQualified(text));
        Assert.True(Path.IsPathFullyQualified(json));
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
        var run   = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)),
                           "--out", Path.Combine(_scratch, "illegal"));

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.True(File.Exists((string)run.Report["exports"]!["text"]!));
    }

    [Fact]
    public void NoExportWritesNothingAtAll()
    {
        var requested = Path.Combine(_scratch, "unwanted");

        var run = Invoke("--from", SampleHeroFile(), "--out", requested, "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Null(run.Report["exports"]);
        Assert.False(Directory.Exists(requested));
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
        var run  = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)),
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

    /// <summary>
    /// <b>A misspelled field is the same failure wearing a disguise.</b> Left ignored, a
    /// character with <c>AbilityRank</c> for <c>AbilityRanks</c> arrives with no abilities at
    /// all — cheaper, legal, and wrong in a way nothing reports. A caller who wrote the file
    /// is told; the browser restoring its own storage deliberately is not, because a field
    /// dropped in a later build should cost it a field rather than the character.
    /// </summary>
    [Fact]
    public void AFieldNameThatIsNotPartOfACharacterIsRefusedRatherThanIgnored()
    {
        var run = Invoke("--from", CharacterFile("""
            {
              "SelectedTierId": "standard",
              "AbilityRank": { "might": 8 },
              "Flaws": [ { "FlawId": "code" } ]
            }
            """), "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.Contains("AbilityRank", run.Issue("INPUT_UNREADABLE")!["message"]!.ToString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The strictness has to accept what this project itself writes, or the browser's saved
    /// characters and the wizard's own output stop being submittable — which is the obvious
    /// way to use the command and the first thing anyone would try.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WhatThisProjectWritesIsWhatTheCommandAccepts(bool villain)
    {
        var sheet = villain ? SampleCharacters.Villain() : SampleCharacters.Hero();

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
    }

    /// <summary>
    /// A field spelled the way the caller's language spells it. Case is forgiven; an
    /// underscore is not, and that is not a silent difference — an unknown field is now
    /// refused, so <c>selected_tier_id</c> is reported rather than dropped.
    /// </summary>
    [Fact]
    public void AFieldNameInAnotherCaseIsUnderstood()
    {
        var run = Invoke("--from", CharacterFile("""
            {
              "selectedTierId": "standard",
              "abilityRanks": { "might": 8 },
              "flaws": [ { "flawId": "code" } ]
            }
            """), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal("standard", (string?)run.Report["character"]!["tier"]);
        Assert.True((int)run.Report["hero_points"]!["spent"]! > 0);
    }

    /// <summary>
    /// Two characters exported in the same second used to overwrite each other, and both runs
    /// reported paths that then held the second character. The name is the character's plus a
    /// timestamp to the second, and a repair loop runs many times faster than that.
    /// </summary>
    [Fact]
    public void TwoExportsInTheSameSecondDoNotOverwriteEachOther()
    {
        var requested  = Path.Combine(_scratch, "twice");
        var first = SampleCharacters.Hero();
        var second = SampleCharacters.Hero();
        second.Motivation = "A different character with the same name";

        var a = Invoke("--from", CharacterFile(CharacterSheetJson.Write(first)), "--out", requested);
        var b = Invoke("--from", CharacterFile(CharacterSheetJson.Write(second)), "--out", requested);

        var textA = (string)a.Report["exports"]!["text"]!;
        var textB = (string)b.Report["exports"]!["text"]!;

        Assert.NotEqual(textA, textB);
        Assert.True(File.Exists(textA), "The first export was overwritten by the second.");
        Assert.DoesNotContain(second.Motivation, File.ReadAllText(textA), StringComparison.Ordinal);
        Assert.Contains(second.Motivation, File.ReadAllText(textB), StringComparison.Ordinal);
    }

    /// <summary>
    /// A caller that asked for sheets and got none has to see it in the report. It was a line
    /// on standard error and an <c>exports: null</c> nothing told them to check — exit 0 with
    /// no sheet, which reads as success.
    /// </summary>
    [Fact]
    public void ExportsThatCouldNotBeWrittenAreReportedNotJustLogged()
    {
        // --out at a path that is already a file, so the directory cannot be created.
        var blocker = Path.Combine(_scratch, "in-the-way");
        File.WriteAllText(blocker, "not a directory");

        var run = Invoke("--from", SampleHeroFile(), "--out", blocker);

        Assert.Null(run.Report["exports"]);
        Assert.NotNull(run.Issue("EXPORTS_NOT_WRITTEN"));
        Assert.NotEmpty(run.StdErr);

        // Still exit 0: the exit code answers "is this character legal", and it is.
        Assert.Equal(BuildCommand.Ok, run.ExitCode);
    }

    /// <summary>
    /// <b>Nulls where the type system says there cannot be one.</b> Six shapes of ordinary
    /// hand-written JSON took the whole run down through the validator — no report, an exit
    /// code outside the three, a stack trace. The deserializer puts a null at any depth
    /// without the compiler objecting, and a caller writing a character by hand will omit a
    /// key sooner or later.
    /// </summary>
    [Theory]
    [InlineData("""{"SelectedTierId":"standard","Gear":[{"Name":"Sword","Features":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Gear":[{"Name":"Sword","Features":[null]}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Gear":[{"Name":"Sword","Cons":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Gear":[{"Name":"Sword","Pros":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","SelectedPowers":[{"PowerId":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Perks":[{"PerkId":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Flaws":[{"FlawId":null}]}""")]
    [InlineData("""{"SelectedTierId":"standard","Gear":[{"Name":null}]}""")]
    public void ACharacterWithNullsWhereIdsBelongIsReportedNotThrown(string json)
    {
        var run = Invoke("--from", CharacterFile(json), "--no-export");

        // Whatever it is, it is one of the three exits with one JSON document on stdout.
        Assert.Contains(run.ExitCode,
            new[] { BuildCommand.Ok, BuildCommand.CharacterIllegal, BuildCommand.InputUnusable });

        var document = JsonDocument.Parse(run.StdOut);
        Assert.Equal(run.ExitCode, document.RootElement.GetProperty("exit_code").GetInt32());
        Assert.DoesNotContain("Unhandled exception", run.StdErr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>--from ""</c>. <c>File.ReadAllText</c> refuses an empty path before it touches a
    /// disk, with an exception that was not in the filter — so the one argument value most
    /// likely to arrive from an unset variable was the one that crashed.
    /// </summary>
    [Fact]
    public void AnEmptyFileNameIsReportedRatherThanThrown()
    {
        var run = Invoke("--from", "", "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    /// <summary>
    /// <b>A Windows device name is not a file, and reading one never returns.</b>
    /// <c>File.ReadAllText("CON")</c> opens the console and blocks on a read with no end — no
    /// output, no exit code, forever, which is worse than any crash and worse than the empty
    /// <c>--from</c> that was fixed alongside it. <c>NUL</c> and <c>PRN</c> happened to fail
    /// politely; the whole reserved set is refused rather than the three caught misbehaving.
    ///
    /// <para>The timeout is the assertion. A test that hangs reports nothing, so this one is
    /// written to fail rather than to stall.</para>
    /// </summary>
    [Theory]
    [InlineData("CON")]
    [InlineData("COM1")]
    [InlineData("CONIN$")]
    [InlineData("NUL")]
    [InlineData("PRN")]
    [InlineData("con.json")]
    public async Task ADeviceNameIsRefusedRatherThanOpened(string device)
    {
        var finished = Task.Run(() => Invoke("--from", device, "--no-export"),
            TestContext.Current.CancellationToken);

        var completed = await Task.WhenAny(finished, Task.Delay(TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken));

        Assert.True(completed == finished,
            $"Reading '{device}' did not finish — it is a device, not a file.");

        var run = await finished;

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
    }

    /// <summary>
    /// Both exports or neither. The <c>.json</c> path is one character longer than the
    /// <c>.txt</c>, so at one particular character-name length the first write succeeded and the
    /// second did not — leaving half an export on disk under a base name that then looked taken
    /// to the next run, while the report said nothing had been written.
    /// </summary>
    [Fact]
    public void AnExportThatCannotBeFinishedLeavesNoHalfOfItBehind()
    {
        var out_ = Path.Combine(_scratch, "half");
        var hero = SampleCharacters.Hero();
        hero.Name = new string('B', 235);

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)), "--out", out_);

        // Either both were written or neither was; never one.
        var written = Directory.Exists(out_) ? Directory.GetFiles(out_) : [];

        Assert.True(written.Length is 0 or 2, $"An export left {written.Length} file(s) behind.");
        Assert.Equal(written.Length == 0, run.Report["exports"] is null);
    }

    /// <summary>
    /// The message for a file that is not JSON must not name a C# type. It interpolated the
    /// deserializer's own message, which says things like "could not be converted to
    /// ProwlersAndParagonsAutomation.Engine.SelectedPower" — this program talking about itself
    /// to somebody holding a rulebook, and the rule the rest of the surface is held to.
    /// </summary>
    [Fact]
    public void TheUnreadableInputMessageNamesNoInternalType()
    {
        var run = Invoke("--from", CharacterFile("""
            {"SelectedTierId":"standard","SelectedPowers":[7]}
            """), "--no-export");

        var message = run.Issue("INPUT_UNREADABLE")!["message"]!.ToString();

        Assert.DoesNotContain("ProwlersAndParagons", message, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", message, StringComparison.Ordinal);
        Assert.Contains("SelectedPowers", message, StringComparison.Ordinal);   // the path helps
    }

    /// <summary>
    /// The empty-input guard has its own message, and two of the three inputs it exists for
    /// reached the same code by a different route — the JSON parser — so deleting the guard
    /// left the test green. This asserts the guard itself answered.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyFileIsSaidToBeEmptyRatherThanUnparseable(string contents)
    {
        var message = Invoke("--from", CharacterFile(contents), "--no-export")
            .Issue("INPUT_UNREADABLE")!["message"]!.ToString();

        Assert.Contains("empty", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NothingOnStandardInputSaysSoRatherThanFailingToParse()
    {
        var message = InvokeWithInput("", "--from", "-", "--no-export")
            .Issue("INPUT_UNREADABLE")!["message"]!.ToString();

        Assert.Contains("standard input", message, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// <b>The predicate, not just the message.</b> Only the string was tested, so
    /// <see cref="InteractiveTerminal.IsAvailable"/> could return true always and the wizard
    /// would go back to throwing out of its first prompt with every test green.
    ///
    /// <para>A test runner is exactly the case it exists for: standard input is redirected,
    /// so there is no terminal to prompt on. Asserting that is asserting the predicate.</para>
    /// </summary>
    [Fact]
    public void ThereIsNoInteractiveTerminalUnderATestRunner()
    {
        Assert.True(Console.IsInputRedirected, "This test assumes the runner redirects input.");
        Assert.False(InteractiveTerminal.IsAvailable);
    }

    // ── What the arguments mean ───────────────────────────────────────────

    /// <summary>
    /// <see cref="CommandLine"/> exists to be tested: as six lines at the top of Program.cs
    /// none of this was reachable, and one of those lines answered a misspelled verb with
    /// exit 2 and an empty standard output — contradicting the contract in the same breath as
    /// naming it.
    /// </summary>
    private Run Dispatch(bool interactive, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var wizardRan = false;

        var exit = new CommandLine(
                new BuildCommand(_f.Rules, _f.Costs, _f.Derived, _f.Validator),
                () => wizardRan = true,
                () => interactive)
            .Run(args, RulesFixture.RepoRoot, stdout, stderr, new StringReader(""));

        return new Run(exit, stdout.ToString(), stderr.ToString()) { WizardRan = wizardRan };
    }

    [Fact]
    public void AMisspelledVerbStillWritesAReport()
    {
        var run = Dispatch(interactive: true, "bulid", "--from", "x.json");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
        Assert.Contains("bulid", run.Issue("BAD_ARGUMENTS")!["message"]!.ToString(), StringComparison.Ordinal);
        Assert.False(run.WizardRan);
    }

    [Fact]
    public void TheBuildVerbIsRoutedToTheCommandWithoutItsOwnName()
    {
        var run = Dispatch(interactive: true, BuildCommand.Verb, "--from", SampleHeroFile(), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.True((bool)run.Report["ok"]!);
        Assert.False(run.WizardRan);
    }

    [Fact]
    public void NoArgumentsRunsTheWizardWhenThereIsATerminal()
    {
        var run = Dispatch(interactive: true);

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.True(run.WizardRan);
    }

    [Fact]
    public void NoArgumentsWithoutATerminalExplainsItselfAndDoesNotStartTheWizard()
    {
        var run = Dispatch(interactive: false);

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.False(run.WizardRan);
        Assert.Contains(BuildCommand.Verb, run.StdErr, StringComparison.Ordinal);

        // A report as well as the message. This branch returned the build command's exit code
        // with nothing on standard output, so a caller that read stdout on a non-zero exit —
        // which is the whole convention here — found nothing to read.
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
        Assert.Equal(BuildCommand.InputUnusable, (int)run.Report["exit_code"]!);
    }
}
