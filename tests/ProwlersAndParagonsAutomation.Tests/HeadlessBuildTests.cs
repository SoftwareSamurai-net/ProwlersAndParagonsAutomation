using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Cli.Headless;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

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
        Assert.Equal(tier.TraitCapRank, (int)run.Report["tier_trait_cap"]!);
        Assert.Equal(hero.SelectedPackageId, (string?)run.Report["character"]!["package"]);
    }

    // ── The house Trait Cap ───────────────────────────────────────────────

    /// <summary>
    /// <b><c>--trait-cap</c> is the whole finding, end to end.</b> A campaign caps a
    /// non-superhuman at 6d and the tool could not see it, so a 7d Ability at the Standard
    /// tier validated <c>ok: true</c>. Under the flag it is <c>TRAIT_ABOVE_CAP</c> against a
    /// limit of 6, and Resolve moves with the cap because the cap is what Resolve is measured
    /// from — the two halves the owner settled together, asserted together.
    ///
    /// <para>The run without the flag is the positive control: 7d really is legal at this tier,
    /// so nothing below can be satisfied by a character that was already illegal.</para>
    /// </summary>
    [Fact]
    public void TheTraitCapFlagMovesTheCapTheCharacterIsJudgedAndPaidAgainst()
    {
        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["intellect"] = 7;
        var file = CharacterFile(CharacterSheetJson.Write(sheet));

        var tier = _f.Rules.GetTier("standard")!;
        var free = Invoke("--from", file, "--no-export");

        Assert.Equal(BuildCommand.Ok, free.ExitCode);
        Assert.Null(free.Issue("TRAIT_ABOVE_CAP"));
        Assert.Equal(tier.TraitCapRank, (int)free.Report["trait_cap"]!);
        Assert.Equal((tier.TraitCapRank - 7) * 2, (int)free.Report["derived"]!["resolve"]!);

        var capped = Invoke("--from", file, "--no-export", "--trait-cap", "6");

        Assert.Equal(BuildCommand.CharacterIllegal, capped.ExitCode);
        Assert.Equal(6, (int)capped.Report["trait_cap"]!);
        Assert.Equal(tier.TraitCapRank, (int)capped.Report["tier_trait_cap"]!);

        var issue = capped.Issue("TRAIT_ABOVE_CAP");
        Assert.NotNull(issue);
        Assert.Equal("intellect", (string?)issue["subject_id"]);
        Assert.Equal(7, (int)issue["value"]!);
        Assert.Equal(6, (int)issue["limit"]!);

        // Resolve is measured from the cap in force, so a rank over it pays nothing.
        Assert.Equal(0, (int)capped.Report["derived"]!["resolve"]!);
    }

    /// <summary>
    /// The flag applies to <b>every</b> character in a roster and beats the field on the file.
    /// A house cap is a fact about the table, and a caller checking twenty-eight sheets against
    /// a campaign's rule must not be answered about twenty-seven of them plus whatever the
    /// twenty-eighth believed about itself.
    /// </summary>
    [Fact]
    public void TheTraitCapFlagAppliesToEveryCharacterAndBeatsTheFileField()
    {
        var plain = _f.LegalSheet();
        plain.Name = "Plain";

        var believes = _f.LegalSheet();
        believes.Name = "Believes";
        believes.TraitCapRank = 10;

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(plain)),
                         "--from", CharacterFile(CharacterSheetJson.Write(believes)),
                         "--no-export", "--trait-cap", "6");

        var reported = run.Report["characters"]!.AsArray()
            .Select(c => (int)c!["trait_cap"]!)
            .ToList();

        Assert.Equal([6, 6], reported);
        Assert.All(run.Report["characters"]!.AsArray(),
            c => Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, (int)c!["tier_trait_cap"]!));
    }

    /// <summary>
    /// <b>The file's own field is honoured with no flag at all</b>, which is what makes a
    /// character portable: the cap travels with it and does not have to be remembered on a
    /// command line.
    /// </summary>
    [Fact]
    public void AHouseCapOnTheFileIsHonouredWithNoFlag()
    {
        var sheet = _f.LegalSheet();
        sheet.TraitCapRank = 6;
        sheet.AbilityRanks["intellect"] = 7;

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.Equal(6, (int)run.Report["trait_cap"]!);
        Assert.Equal(6, (int)run.Issue("TRAIT_ABOVE_CAP")!["limit"]!);
    }

    /// <summary>
    /// A cap that is not a whole number is an argument fault — exit 2, and the report names the
    /// flag rather than the character. A number that is merely nonsense is not: 0d and a cap
    /// above the tier's are findings on the character, the same ones the file's field gets, so
    /// the flag and the field cannot disagree about what a bad cap means.
    /// </summary>
    [Theory]
    [InlineData("six")]
    [InlineData("6d")]
    [InlineData("")]
    public void ATraitCapThatIsNotAWholeNumberIsRefused(string value)
    {
        var run = Invoke("--from", SampleHeroFile(), "--no-export", "--trait-cap", value);

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.Equal("BAD_ARGUMENTS", (string?)run.Issues[0]!["code"]);
        Assert.Contains("--trait-cap", (string?)run.Issues[0]!["message"] ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void ATraitCapWithNoValueIsRefused()
    {
        var run = Invoke("--from", SampleHeroFile(), "--no-export", "--trait-cap");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.Equal("BAD_ARGUMENTS", (string?)run.Issues[0]!["code"]);
    }

    [Theory]
    [InlineData("0", "TRAIT_CAP_BELOW_MINIMUM")]
    [InlineData("40", "TRAIT_CAP_ABOVE_TIER")]
    public void ACapThatIsANumberAndStillNonsenseIsAFindingOnTheCharacter(string value, string code)
    {
        var run = Invoke("--from", SampleHeroFile(), "--no-export", "--trait-cap", value);

        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.NotNull(run.Issue(code));
        Assert.Equal(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                     (int)run.Report["trait_cap"]!);
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
              "abilityRanks": { "might": 8, "agility": 1, "intellect": 1,
                                "perception": 1, "toughness": 1, "willpower": 1 },
              "talentRanks": { "academics": 1, "charm": 1, "command": 1, "covert": 1,
                               "investigation": 1, "medicine": 1, "professional": 1,
                               "science": 1, "streetwise": 1, "survival": 1,
                               "technology": 1, "vehicles": 1 },
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

        // <b>A named finding, not merely "did not crash".</b> Accepting any of the three exits
        // made this a crash test wearing a behaviour test's name: gutting the null-entry repair
        // degraded every one of these from a full report to "this character is unusable", and
        // the test stayed green because unusable is also one of the three.
        Assert.Equal(BuildCommand.CharacterIllegal, run.ExitCode);
        Assert.Null(run.Issue("CHARACTER_UNUSABLE"));
        Assert.NotEmpty(run.Issues);

        var document = JsonDocument.Parse(run.StdOut);
        Assert.Equal(run.ExitCode, document.RootElement.GetProperty("exit_code").GetInt32());
    }

    /// <summary>
    /// <b>The ten top-level collections, each written as JSON <c>null</c>.</b> The deserializer
    /// throws <see cref="InvalidOperationException"/> rather than a JSON exception when asked to
    /// put a null into a get-only collection, and only the latter was caught — so all ten crashed
    /// the command outright. The theory above covers nested nulls and never covered these, so the
    /// catch that fixes them could be removed with the suite green.
    /// </summary>
    [Theory]
    [InlineData("AbilityRanks")]
    [InlineData("AbilityModifiers")]
    [InlineData("TalentRanks")]
    [InlineData("AbilitySources")]
    [InlineData("TalentSources")]
    [InlineData("SelectedPowers")]
    [InlineData("Perks")]
    [InlineData("Flaws")]
    [InlineData("Connections")]
    [InlineData("Gear")]
    public void ACollectionWrittenAsNullIsReportedRatherThanThrown(string field)
    {
        var run = Invoke("--from",
            CharacterFile($"{{\"SelectedTierId\":\"standard\",\"{field}\":null}}"), "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("INPUT_UNREADABLE"));
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(run.StdOut).RootElement.ValueKind);
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
        var requested = Path.Combine(_scratch, "half");
        var hero = SampleCharacters.Hero();
        hero.Name = new string('B', 235);

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)), "--out", requested);

        // Either both were written or neither was; never one.
        var written = Directory.Exists(requested) ? Directory.GetFiles(requested) : [];

        Assert.True(written.Length is 0 or 2, $"An export left {written.Length} file(s) behind.");
        Assert.Equal(written.Length == 0, run.Report["exports"] is null);
    }

    /// <summary>
    /// A name is only usable if it can hold <b>both</b> halves. Checking only the <c>.txt</c>
    /// would let a run whose <c>.json</c> already exists write over it, so the pair on disk would
    /// be two different characters under one name.
    /// </summary>
    [Fact]
    public void ANameIsOnlyFreeIfBothHalvesAre()
    {
        var requested = Path.Combine(_scratch, "halves");
        Directory.CreateDirectory(requested);

        var hero = SampleCharacters.Hero();
        var first = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)),
                           "--out", requested);

        // Take away only the .txt, leaving the .json occupying that base name.
        File.Delete((string)first.Report["exports"]!["text"]!);
        var occupied = (string)first.Report["exports"]!["json"]!;

        var second = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)),
                            "--out", requested);

        Assert.NotEqual(occupied, (string)second.Report["exports"]!["json"]!);
        Assert.True(File.Exists(occupied), "The surviving half of an earlier export was overwritten.");
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
    /// A relative <c>--out</c>, which is the case <c>Path.GetFullPath</c> was added for: the
    /// report gave a relative path for a relative <c>--out</c> and an absolute one otherwise, so a
    /// caller resolving it from anywhere but the process's own directory found nothing half the
    /// time. Asserting "the reported path is absolute" while only ever passing an absolute
    /// <c>--out</c> proved nothing.
    /// </summary>
    [Fact]
    public void ARelativeOutputDirectoryIsReportedAsAnAbsolutePath()
    {
        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_scratch);

            var run  = Invoke("--from", SampleHeroFile(), "--out", "relative-out");
            var text = (string)run.Report["exports"]!["text"]!;

            Assert.True(Path.IsPathFullyQualified(text), $"'{text}' is not an absolute path.");
            Assert.True(File.Exists(text));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    /// <summary>
    /// A figure the engine could not supply, with no error beside it, is a fault here — and
    /// saying so is what stops a caller repairing a character that is already legal for ever.
    /// </summary>
    [Fact]
    public void AFigureMissingWithNothingToFixIsNeverReportedAsSuccess()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Flaws.Add(new SelectedFlaw("code"));
        sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
        { Units = 600_000_000, SourceId = "innate" });

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(sheet)), "--no-export");

        Assert.NotEqual(BuildCommand.Ok, run.ExitCode);
        Assert.False((bool)run.Report["ok"]!);
        Assert.Contains(run.Issues, i => (string?)i!["severity"] == "error");
    }

    /// <summary>
    /// <b>Both halves of the terminal check, each on its own.</b> As one expression it could only
    /// be tested in the environment a runner provides — where both halves say the same thing — so
    /// either could be deleted and nothing would notice. The doc comment says both are needed;
    /// this is what makes that checkable.
    /// </summary>
    [Theory]
    [InlineData(false, true, true)]     // a real terminal
    [InlineData(true, true, false)]     // piped input, capable console
    [InlineData(false, false, false)]   // a console that cannot prompt
    [InlineData(true, false, false)]
    public void ATerminalIsUsableOnlyWhenBothHalvesAgree(
        bool inputRedirected, bool profileIsInteractive, bool expected)
    {
        Assert.Equal(expected,
            InteractiveTerminal.IsAvailableGiven(inputRedirected, profileIsInteractive));
    }

    [Fact]
    public void ThereIsNoInteractiveTerminalUnderATestRunner()
    {
        Assert.True(Console.IsInputRedirected, "This test assumes the runner redirects input.");
        Assert.False(InteractiveTerminal.IsAvailable);
    }

    // ── A roster in one process ───────────────────────────────────────────

    /// <summary>
    /// <b>One character reports exactly as it always did.</b> Every caller of this command
    /// reads that document, and wrapping a single character in a roster would break all of
    /// them for nothing — there is no cross-sheet question to ask about one sheet. Asserted
    /// as the absence of the roster keys as well as the presence of the old ones, because
    /// adding <c>characters</c> beside <c>character</c> would satisfy every existing test.
    /// </summary>
    [Fact]
    public void OneCharacterIsReportedExactlyAsItAlwaysWas()
    {
        var run = Invoke("--from", SampleHeroFile(), "--no-export", "--traits-above", "1");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Null(run.Report["characters"]);
        Assert.Null(run.Report["roster"]);
        Assert.Null(run.Report["source"]);
        Assert.NotNull(run.Report["character"]);
        Assert.NotNull(run.Report["hero_points"]);
    }

    /// <summary>
    /// Twenty-eight characters were twenty-eight process starts, and every re-check after an
    /// edit was another twenty-eight. One process, one document, one report per character —
    /// each keeping its own verdict, since "something in this roster is wrong" is not an
    /// answer anybody can act on.
    /// </summary>
    [Fact]
    public void ARosterIsOneDocumentHoldingEachCharactersOwnReport()
    {
        var hero    = SampleCharacters.Hero();
        var villain = SampleCharacters.Villain();

        var first  = CharacterFile(CharacterSheetJson.Write(hero));
        var second = CharacterFile(CharacterSheetJson.Write(villain));

        var run = Invoke("--from", first, "--from", second, "--no-export");

        // One JSON document on standard output, whatever the count. JsonDocument.Parse throws
        // on trailing content, so two reports printed one after another fail here.
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(run.StdOut).RootElement.ValueKind);

        var characters = run.Report["characters"]!.AsArray();
        Assert.Equal(2, characters.Count);
        Assert.Equal([first, second], characters.Select(c => (string?)c!["source"]));
        Assert.Equal([hero.Name, villain.Name],
                     characters.Select(c => (string?)c!["character"]!["name"]));

        // Each character's own figures, asked of the engine rather than of a constant.
        Assert.Equal(_f.Costs.TotalCost(hero),    (int)characters[0]!["hero_points"]!["spent"]!);
        Assert.Equal(_f.Costs.TotalCost(villain), (int)characters[1]!["hero_points"]!["spent"]!);

        // The top level is the roster's, not the first character's.
        Assert.Null(run.Report["character"]);
        Assert.Null(run.Report["hero_points"]);
    }

    /// <summary>
    /// <b>The worst news in the run is the exit code, and each character keeps its own.</b>
    /// A caller looping over a roster learns from one number that something needs attention,
    /// and from the reports which sheet it was. Every pair is exercised, because a max that
    /// had become a first-or-last would pass on three of the four.
    /// </summary>
    [Theory]
    [InlineData(false, false, BuildCommand.Ok)]
    [InlineData(true,  false, BuildCommand.CharacterIllegal)]
    [InlineData(false, true,  BuildCommand.InputUnusable)]
    [InlineData(true,  true,  BuildCommand.InputUnusable)]
    public void TheRostersExitCodeIsTheWorstOfItsCharacters(bool illegal, bool unreadable, int expected)
    {
        // Two legal characters always, so every case is a roster and the shape is the same
        // one; a single input is deliberately reported as a single character.
        var files = new List<string>
        {
            CharacterFile(CharacterSheetJson.Write(SampleCharacters.Hero())),
            CharacterFile(CharacterSheetJson.Write(SampleCharacters.Villain()))
        };

        // An illegal character first, so a max that had become "the last one" fails.
        if (illegal) files.Insert(0, CharacterFile(CharacterSheetJson.Write(RulesFixture.StandardSheet())));
        if (unreadable) files.Insert(0, CharacterFile("{ nope"));

        var run = Invoke([.. files.SelectMany<string, string>(f => ["--from", f]), "--no-export"]);

        Assert.Equal(expected, run.ExitCode);
        Assert.Equal(expected, (int)run.Report["exit_code"]!);
        Assert.Equal(expected == BuildCommand.Ok, (bool)run.Report["ok"]!);

        // And the exit code on the wire is each character's own, never the roster's.
        Assert.Contains(run.Report["characters"]!.AsArray(),
            c => (int)c!["exit_code"]! == BuildCommand.Ok);
    }

    /// <summary>
    /// <b>An unreadable file costs its own report and nothing else.</b> A caller checking
    /// twenty-eight sheets should not lose twenty-seven answers to a typo in one file name —
    /// which is what a run that stopped at the first bad file would do.
    /// </summary>
    [Fact]
    public void AnUnreadableFileIsOneExitTwoReportRatherThanTheEndOfTheRun()
    {
        var hero    = SampleCharacters.Hero();
        var broken  = CharacterFile("{ nope");
        var missing = Path.Combine(_scratch, "no-such-character.json");

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)),
                         "--from", broken,
                         "--from", missing,
                         "--from", CharacterFile(CharacterSheetJson.Write(hero)),
                         "--no-export");

        var characters = run.Report["characters"]!.AsArray();
        Assert.Equal(4, characters.Count);
        Assert.Equal([BuildCommand.Ok, BuildCommand.InputUnusable,
                      BuildCommand.InputUnusable, BuildCommand.Ok],
                     characters.Select(c => (int)c!["exit_code"]!));

        // The two that could be read are still whole reports, with the engine's own figures.
        Assert.Equal(_f.Costs.TotalCost(hero), (int)characters[3]!["hero_points"]!["spent"]!);

        // And the ones that could not name the file they came from, since the message is prose.
        Assert.Equal(broken, (string?)characters[1]!["source"]);
        Assert.Equal(missing, (string?)characters[2]!["source"]);
        Assert.Equal("INPUT_UNREADABLE",
            (string?)characters[1]!["issues"]!.AsArray()[0]!["code"]);
    }

    /// <summary>
    /// A directory of sheets, in name order, and nothing else in it. The order matters
    /// because two runs over one roster should produce reports a reader can diff; the filter
    /// matters because a roster directory usually has notes in it.
    /// </summary>
    [Fact]
    public void FromDirTakesEveryJsonDirectlyInItInNameOrder()
    {
        var dir = Path.Combine(_scratch, "roster");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "old"));

        foreach (var name in new[] { "c.json", "a.json", "b.json" })
            File.WriteAllText(Path.Combine(dir, name), CharacterSheetJson.Write(SampleCharacters.Hero()));

        File.WriteAllText(Path.Combine(dir, "notes.txt"), "not a character");
        File.WriteAllText(Path.Combine(dir, "old", "d.json"),
            CharacterSheetJson.Write(SampleCharacters.Hero()));

        var run = Invoke("--from-dir", dir, "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal(["a.json", "b.json", "c.json"],
            run.Report["characters"]!.AsArray()
               .Select(c => Path.GetFileName((string)c!["source"]!)));
    }

    [Fact]
    public void FromDirAndFromAreOneList()
    {
        var dir = Path.Combine(_scratch, "combined");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.json"),
            CharacterSheetJson.Write(SampleCharacters.Hero()));

        var run = Invoke("--from-dir", dir, "--from", SampleHeroFile(), "--no-export");

        Assert.Equal(2, run.Report["characters"]!.AsArray().Count);
    }

    /// <summary>
    /// A directory that is not there, and one with no characters in it, are argument faults
    /// rather than empty rosters — an empty roster reports "every character is legal", which
    /// is a true sentence about nothing and reads as success.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADirectoryWithNoCharactersInItIsRefusedRatherThanReportedEmpty(bool exists)
    {
        var dir = Path.Combine(_scratch, exists ? "empty" : "not-there");
        if (exists) Directory.CreateDirectory(dir);

        var run = Invoke("--from-dir", dir, "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
    }

    /// <summary>
    /// <b>Standard input can only be drained once.</b> A second <c>--from -</c> reads a
    /// stream the first one emptied, so the second character would come back "no character
    /// arrived" — an answer about a character nobody submitted, which is worse than a refusal.
    /// </summary>
    [Fact]
    public void StandardInputMayOnlyBeNamedOnce()
    {
        var run = InvokeWithInput(CharacterSheetJson.Write(SampleCharacters.Hero()),
                                  "--from", "-", "--from", "-", "--no-export");

        Assert.Equal(BuildCommand.InputUnusable, run.ExitCode);
        Assert.NotNull(run.Issue("BAD_ARGUMENTS"));
        Assert.Contains("once", run.Issue("BAD_ARGUMENTS")!["message"]!.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StandardInputCanBeOneCharacterOfARoster()
    {
        var run = InvokeWithInput(CharacterSheetJson.Write(SampleCharacters.Hero()),
                                  "--from", "-", "--from", SampleHeroFile(), "--no-export");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Equal(2, run.Report["characters"]!.AsArray().Count);
    }

    // ── Stable export names ───────────────────────────────────────────────

    /// <summary>
    /// <b>Re-exporting a roster should replace its sheets, not double them.</b> The default
    /// name carries a timestamp, so twenty-eight characters exported twice are fifty-six
    /// files and a de-duplication script. The control is the run without the flag: two pairs,
    /// which is the behaviour <c>--overwrite</c> exists to opt out of.
    /// </summary>
    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 4)]
    public void OverwriteReplacesTheSamePairWhereTheDefaultAddsOne(bool overwrite, int expectedFiles)
    {
        var dir  = Path.Combine(_scratch, overwrite ? "stable" : "timestamped");
        var file = CharacterFile(CharacterSheetJson.Write(SampleCharacters.Hero()));

        string[] args = overwrite
            ? ["--from", file, "--out", dir, "--overwrite"]
            : ["--from", file, "--out", dir];

        var first  = Invoke(args);
        var second = Invoke(args);

        Assert.Equal(expectedFiles, Directory.GetFiles(dir).Length);
        Assert.Equal(overwrite, (string)first.Report["exports"]!["text"]!
                              == (string)second.Report["exports"]!["text"]!);
        Assert.True(File.Exists((string)second.Report["exports"]!["text"]!));
    }

    /// <summary>
    /// And the stable name really is the character's, with no timestamp left in it — a name
    /// that still carried one would pass the count above by colliding with itself only when
    /// two runs landed in the same second.
    /// </summary>
    [Fact]
    public void AnOverwrittenExportIsNamedAfterTheCharacterAlone()
    {
        var dir  = Path.Combine(_scratch, "named");
        var hero = SampleCharacters.Hero();

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(hero)),
                         "--out", dir, "--overwrite");

        Assert.Equal(CharacterSheetRenderer.BaseFileName(hero) + ".txt",
                     Path.GetFileName((string)run.Report["exports"]!["text"]!));
    }

    /// <summary>
    /// <b>Two characters cannot quietly share one pair of files.</b> <em>Cael Hughes</em> and
    /// <em>Cael-Hughes</em> have the same safe name, so under a stable name one writes over
    /// the other and both reports name paths holding somebody else. Reported on every
    /// character that shares the name, so it cannot matter which one a caller looked at.
    /// </summary>
    [Fact]
    public void TwoCharactersWithOneSafeNameAreReportedRatherThanClobbered()
    {
        var first  = SampleCharacters.Hero();
        var second = SampleCharacters.Hero();
        first.Name  = "Cael Hughes";
        second.Name = "Cael-Hughes";

        var firstFile  = CharacterFile(CharacterSheetJson.Write(first));
        var secondFile = CharacterFile(CharacterSheetJson.Write(second));

        var run = Invoke("--from", firstFile, "--from", secondFile,
                         "--out", Path.Combine(_scratch, "collide"), "--overwrite");

        var reports = run.Report["characters"]!.AsArray();

        Assert.All(reports, report =>
        {
            var issue = report!["issues"]!.AsArray()
                .FirstOrDefault(i => (string?)i!["code"] == "EXPORT_NAME_COLLISION");

            Assert.NotNull(issue);
            Assert.Equal("warning", (string?)issue["severity"]);
            Assert.Equal(CharacterSheetRenderer.BaseFileName(first), (string?)issue["value"]);
            Assert.Equal([firstFile, secondFile],
                         issue["options"]!.AsArray().Select(o => (string?)o));
        });

        // A warning, not an error: the characters are legal, and what is wrong is the pair of
        // names the caller chose.
        Assert.Equal(BuildCommand.Ok, run.ExitCode);
    }

    /// <summary>
    /// Without <c>--overwrite</c> there is nothing to collide over — the timestamp and the
    /// numbered fallback keep the two apart — so the warning must not appear.
    /// </summary>
    [Fact]
    public void TheCollisionWarningIsAboutOverwriteAndNotAboutTheNames()
    {
        var first  = SampleCharacters.Hero();
        var second = SampleCharacters.Hero();
        first.Name  = "Cael Hughes";
        second.Name = "Cael-Hughes";

        var run = Invoke("--from", CharacterFile(CharacterSheetJson.Write(first)),
                         "--from", CharacterFile(CharacterSheetJson.Write(second)),
                         "--out", Path.Combine(_scratch, "no-collision"));

        Assert.DoesNotContain(run.Report["characters"]!.AsArray(),
            c => c!["issues"]!.AsArray().Any(i => (string?)i!["code"] == "EXPORT_NAME_COLLISION"));
    }

    // ── Cross-sheet questions ─────────────────────────────────────────────

    /// <summary>
    /// A roster with two characters, one Perk each, and Traits over a rank — built once and
    /// asked of the engine by every test below it.
    /// </summary>
    private (string FirstFile, string SecondFile, CharacterSheet First, CharacterSheet Second) Pair()
    {
        var first = _f.LegalSheet();
        first.Name = "Padded";
        first.AbilityRanks["might"]      = 9;
        first.AbilityRanks["toughness"]  = 8;
        first.TalentRanks["streetwise"]  = 7;
        first.SelectedPowers.Add(new SelectedPower("armor", 5) { SourceId = "tech" });
        first.SelectedPowers.Add(new SelectedPower("running", 4) { SourceId = "innate" });
        first.Perks.Add(new SelectedPerk("contacts", 4));

        var second = _f.LegalSheet();
        second.Name = "Lean";
        second.AbilityRanks["agility"] = 8;
        second.Perks.Add(new SelectedPerk("contacts", 2));

        return (CharacterFile(CharacterSheetJson.Write(first)),
                CharacterFile(CharacterSheetJson.Write(second)),
                first, second);
    }

    /// <summary>
    /// <b>Nobody asked, so there is no answer.</b> An empty list here would read as "no Trait
    /// on this roster is above the rank", which answers a question that was never put.
    /// </summary>
    [Fact]
    public void TraitsAboveIsAbsentUntilItIsAskedFor()
    {
        var (first, second, _, _) = Pair();

        var without = Invoke("--from", first, "--from", second, "--no-export");
        var with    = Invoke("--from", first, "--from", second, "--no-export", "--traits-above", "6");

        Assert.Null(without.Report["roster"]!["traits_above"]);
        Assert.NotNull(with.Report["roster"]!["traits_above"]);
    }

    /// <summary>
    /// <b>Every Ability, Talent and Power above the rank, and the Power's rank is the
    /// engine's effective one.</b> Checked against <see cref="DerivedStatsCalculator"/>
    /// rather than against a number worked out here — Armor's baseline is half a Toughness
    /// the test also sets, so a literal would pin two rules at once and neither on purpose.
    /// </summary>
    [Fact]
    public void TraitsAboveListsEveryTraitOverTheRankAtTheEnginesEffectiveRank()
    {
        var (firstFile, secondFile, first, _) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile,
                         "--no-export", "--traits-above", "6");

        var rows = run.Report["roster"]!["traits_above"]!.AsArray();
        Assert.Equal(2, rows.Count);

        var padded = rows[0]!["traits"]!.AsArray();

        Assert.Equal(9, (int)Row(padded, "ability", "might")["rank"]!);
        Assert.Equal(8, (int)Row(padded, "ability", "toughness")["rank"]!);
        Assert.Equal(7, (int)Row(padded, "talent", "streetwise")["rank"]!);

        var armour = first.SelectedPowers.Single(p => p.PowerId == "armor");
        Assert.Equal(_f.Derived.GetEffectiveRank(armour, first),
                     (int)Row(padded, "power", "armor")["rank"]!);
        Assert.Equal(_f.Derived.GetBaselineRank(_f.Rules.GetPower("armor")!, first, armour),
                     (int)Row(padded, "power", "armor")["baseline_rank"]!);

        // Nothing at or below the rank. Streetwise is 7 and every other Talent is 1.
        Assert.DoesNotContain(padded, t => (string?)t!["kind"] == "talent"
                                        && (string?)t["id"] != "streetwise");

        // The second character's Agility is 8 and is on its own row, so this is not one
        // character's answer printed twice.
        Assert.Equal(8, (int)Row(rows[1]!["traits"]!.AsArray(), "ability", "agility")["rank"]!);
    }

    private static JsonNode Row(JsonArray traits, string kind, string id) =>
        traits.Single(t => (string?)t!["kind"] == kind && (string?)t["id"] == id)!;

    /// <summary>
    /// <b>Which Trait a Power's baseline is read from is the half that answers the
    /// question.</b> "Does a Power justify the rank" cannot be told from the rank: a 10d
    /// Power bought outright and a 10d Power sitting on a 10d Ability are the same number.
    /// </summary>
    [Fact]
    public void APowersRowSaysWhichTraitItsBaselineComesFrom()
    {
        var (firstFile, secondFile, _, _) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile,
                         "--no-export", "--traits-above", "6");

        var armour = Row(run.Report["roster"]!["traits_above"]!.AsArray()[0]!["traits"]!.AsArray(),
                         "power", "armor");

        Assert.Equal("baseline_half", (string?)armour["baseline_relationship"]);
        Assert.Equal(DerivedStatsCalculator.BaselineTraitIds(_f.Rules.GetPower("armor")!),
                     armour["baseline_traits"]!.AsArray().Select(t => (string?)t));
    }

    /// <summary>
    /// <b>Nothing is filtered by opinion.</b> Running does not affect Resolve, and it is
    /// still a high rank on somebody's sheet — excluding it would be this program deciding
    /// which of a character's Traits are worth a reader's attention. The flag is reported so
    /// the reader can decide instead.
    /// </summary>
    [Fact]
    public void APowerThatDoesNotAffectResolveIsStillListed()
    {
        var (firstFile, secondFile, _, _) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile,
                         "--no-export", "--traits-above", "6");

        var running = Row(run.Report["roster"]!["traits_above"]!.AsArray()[0]!["traits"]!.AsArray(),
                          "power", "running");

        Assert.False(DerivedStatsCalculator.ResolveAffectedByPower(_f.Rules.GetPower("running")!),
            "The positive control failed: Running now affects Resolve, so this proves nothing.");
        Assert.False((bool)running["affects_resolve"]!);
    }

    /// <summary>
    /// <b>The per-category totals are the engine's, one call each.</b> Asserted against
    /// <see cref="CostCalculator"/> rather than against literals, because a figure this
    /// program worked out itself is the one thing the whole command exists not to produce.
    /// </summary>
    [Fact]
    public void TheRosterSpendingIsTheEnginesOwnPerCategoryFigures()
    {
        var (firstFile, secondFile, first, second) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile, "--no-export");
        var rows = run.Report["roster"]!["spending"]!.AsArray();

        Assert.Equal(2, rows.Count);

        foreach (var (row, sheet) in rows.Zip<JsonNode?, CharacterSheet>([first, second]))
        {
            var totals = row!["totals"]!;

            Assert.Equal(_f.Costs.PackageCost(sheet),     (int)totals["package"]!);
            Assert.Equal(_f.Costs.AbilityCost(sheet),     (int)totals["abilities"]!);
            Assert.Equal(_f.Costs.TalentCost(sheet),      (int)totals["talents"]!);
            Assert.Equal(_f.Costs.TotalPowersCost(sheet), (int)totals["powers"]!);
            Assert.Equal(_f.Costs.TotalPerksCost(sheet),  (int)totals["perks"]!);
            Assert.Equal(_f.Costs.TotalGearCost(sheet),   (int)totals["gear"]!);
            Assert.Equal(_f.Costs.TotalCost(sheet),       (int)totals["total"]!);
        }

        // A positive control: the two characters really do spend differently, so a report
        // that had started printing one of them twice would fail rather than agree with itself.
        Assert.NotEqual(_f.Costs.TotalCost(first), _f.Costs.TotalCost(second));
    }

    /// <summary>
    /// <b>"Padded with Contacts" is invisible in a category total</b>, which is why each Perk
    /// travels with its Units and its price. Five sheets were padded with invented contact
    /// categories and their totals looked ordinary.
    /// </summary>
    [Fact]
    public void EveryPerkIsItemisedWithItsUnitsAndTheEnginesPrice()
    {
        var (firstFile, secondFile, first, _) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile, "--no-export");

        var perk = run.Report["roster"]!["spending"]!.AsArray()[0]!["perks"]!.AsArray().Single()!;

        Assert.Equal("contacts", (string?)perk["id"]);
        Assert.Equal(4, (int)perk["units"]!);
        Assert.Equal(_f.Costs.PerkCost(first.Perks[0]), (int)perk["cost"]!);
    }

    /// <summary>
    /// The roster-wide half of the same question: which Perk is everybody leaning on, and how
    /// hard. Two counts and no price — a Hero Point figure across the roster would be this
    /// program doing the engine's arithmetic.
    /// </summary>
    [Fact]
    public void PerksByIdCountsHoldersAndUnitsAcrossTheRoster()
    {
        var (firstFile, secondFile, first, second) = Pair();

        var run = Invoke("--from", firstFile, "--from", secondFile, "--no-export");

        var contacts = run.Report["roster"]!["perks_by_id"]!.AsArray().Single()!;

        Assert.Equal("contacts", (string?)contacts["id"]);
        Assert.Equal(2, (int)contacts["characters"]!);
        Assert.Equal(first.Perks[0].Units + second.Perks[0].Units, (int)contacts["units"]!);
    }

    /// <summary>
    /// A character nobody could read is in <c>characters</c> and in no roster section: there
    /// is no sheet to ask about, and a row of nulls beside the ones that answered would read
    /// as a character who spent nothing.
    /// </summary>
    [Fact]
    public void ACharacterThatCouldNotBeReadIsAbsentFromEveryRosterSection()
    {
        var (firstFile, secondFile, _, _) = Pair();

        var run = Invoke("--from", firstFile, "--from", CharacterFile("{ nope"), "--from", secondFile,
                         "--no-export", "--traits-above", "6");

        var roster = run.Report["roster"]!;

        Assert.Equal(3, (int)roster["character_count"]!);
        Assert.Equal(2, (int)roster["read_count"]!);
        Assert.Equal(2, roster["spending"]!.AsArray().Count);
        Assert.Equal(2, roster["traits_above"]!.AsArray().Count);
    }

    // ── What --help documents ─────────────────────────────────────────────

    /// <summary>
    /// <b><c>--no-build</c> is what makes several agents in one working tree safe</b>, and it
    /// was undocumented in every surface — found by guessing. Several <c>dotnet run</c>
    /// commands at once collide on the compiler; <c>--no-build</c> skips the build entirely.
    /// The reason is asserted with the flag, because a flag with no reason beside it is one
    /// nobody has a reason to type.
    /// </summary>
    [Fact]
    public void HelpNamesTheFlagThatMakesConcurrentUseSafeAndSaysWhy()
    {
        var run = Invoke("--help");

        Assert.Equal(BuildCommand.Ok, run.ExitCode);
        Assert.Contains("--no-build", run.StdOut, StringComparison.Ordinal);
        Assert.Contains("collide", run.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every flag the command accepts is in the text it prints. A flag the parser takes and
    /// the usage never mentions is one only its author knows about — which is exactly how
    /// <c>--no-build</c> went unfound.
    /// </summary>
    [Theory]
    [InlineData("--from")]
    [InlineData("--from-dir")]
    [InlineData("--out")]
    [InlineData("--no-export")]
    [InlineData("--overwrite")]
    [InlineData("--traits-above")]
    [InlineData("--help")]
    public void EveryFlagTheCommandTakesIsInItsUsage(string flag)
    {
        Assert.Contains(flag, BuildCommand.Usage, StringComparison.Ordinal);

        // And the parser really does take it, so a flag documented after being dropped from
        // the switch fails here rather than passing on the usage text alone.
        var run = Invoke("--from", SampleHeroFile(), "--no-export", flag, "1");

        Assert.DoesNotContain($"'{flag}' is not an option", run.StdOut, StringComparison.Ordinal);
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
