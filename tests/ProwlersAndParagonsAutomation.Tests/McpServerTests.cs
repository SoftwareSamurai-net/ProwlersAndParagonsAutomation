using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Mcp;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The MCP server, driven the way a client drives it.
///
/// <para>Most of what is asserted here is asserted against <see cref="CharacterTools"/>
/// directly, because that is where a failure names a line. What the transport tests are for
/// is the half that a direct call cannot see: that the tools are <em>reachable</em> under the
/// names a stranger's configuration will use, that their schemas are the ones a client can
/// fill in, and that a character sent as an argument arrives as the same character. A tool
/// renamed in C# and not on the wire is a working method nobody can call.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class McpServerTests
{
    private readonly RulesFixture _f;

    public McpServerTests(RulesFixture f) => _f = f;

    private CharacterTools Tools() =>
        new(_f.Rules, _f.Costs, _f.Derived, _f.Validator, () => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // ── Over a real transport ─────────────────────────────────────────────

    /// <summary>
    /// A client and a server on either end of a pair of pipes, in this process. It is the
    /// real protocol — initialize, the capability exchange, JSON-RPC framing — over streams
    /// that happen not to be a console.
    /// </summary>
    private async Task WithClient(Func<McpClient, Task> body)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();

        // Disposed by hand rather than with `await using`, and in this order. The server's
        // RunAsync only returns once the transport is gone, so the two have to be closed
        // before that task is awaited — a `using` would dispose them after, and the wait for
        // a run that cannot finish would hang the suite rather than fail it.
        var transport = new StreamServerTransport(
            toServer.Reader.AsStream(), toClient.Writer.AsStream(), CharacterServer.Name);

        var server = McpServer.Create(transport, CharacterServer.Options(Tools()));

        var running = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()));

        try
        {
            await body(client);
        }
        finally
        {
            await client.DisposeAsync();
            await transport.DisposeAsync();
            await server.DisposeAsync();
            try { await running; } catch (OperationCanceledException) { }
        }
    }

    private static async Task<JsonNode> Call(
        McpClient client, string tool, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var result = await client.CallToolAsync(tool, arguments);

        // IsError is a bool? and null means "not an error", so Assert.False would fail on
        // every successful call — which is how this test first "found" a working server broken.
        Assert.NotEqual(true, result.IsError);

        return JsonNode.Parse(Text(result))
            ?? throw new InvalidOperationException($"{tool} answered with no JSON: {Text(result)}");
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    /// <summary>
    /// <b>The wire names are the contract.</b> A stranger configures a client against them
    /// and the assistant calls them by name; renaming a C# method must not rename a tool, and
    /// adding a seventh must be a decision rather than an accident. Both directions are
    /// asserted, so a tool that quietly stops being served fails here.
    /// </summary>
    /// <summary>
    /// The six names a stranger's configuration and an assistant's tool calls use, written out
    /// rather than taken from the constants that produce them.
    /// </summary>
    private static readonly string[] WireNames =
    [
        "character_sheet", "check_character", "creation_guide",
        "list_options", "power_detail", "search_powers"
    ];

    [Fact]
    public async Task TheServerServesTheSixToolsUnderTheirWireNames()
    {
        await WithClient(async client =>
        {
            // Compared as sets: the collection a server keeps its tools in does not promise an
            // order, and a client shows them in whatever order it likes. Both directions, so
            // that a tool quietly dropped and a seventh quietly added both fail here.
            var served = (await client.ListToolsAsync())
                .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            // <b>The literal strings, not the constants.</b> Comparing the served names with
            // the constants the server registers them from is a comparison with itself:
            // renaming check_character to "judge" would break every configuration a stranger
            // has written down, and pass.
            Assert.Equal(WireNames, served);
        });
    }

    /// <summary>
    /// Every tool has to describe itself, because the description is the whole of what a model
    /// sees before choosing one. An empty description is a tool that gets called by accident
    /// or not at all.
    /// </summary>
    [Fact]
    public async Task EveryToolSaysWhatItIsFor()
    {
        await WithClient(async client =>
        {
            foreach (var tool in await client.ListToolsAsync())
            {
                Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                    $"{tool.Name} has no description.");
                Assert.True(tool.Description!.Length > 60, $"{tool.Name}'s description is a stub.");
            }
        });
    }

    /// <summary>
    /// Every tool is declared read-only and non-destructive, which is what lets a client run
    /// one without stopping to ask. A character builder that needs approval per rules lookup
    /// is not a conversation — and the annotations are a promise: nothing here writes a file
    /// or changes anything on the machine.
    /// </summary>
    [Fact]
    public async Task EveryToolIsDeclaredReadOnly()
    {
        await WithClient(async client =>
        {
            foreach (var tool in await client.ListToolsAsync())
            {
                Assert.Equal(true, tool.ProtocolTool.Annotations?.ReadOnlyHint);
                Assert.Equal(false, tool.ProtocolTool.Annotations?.DestructiveHint);
            }
        });
    }

    /// <summary>
    /// The instructions the client is handed before anything is called. Losing the ordering
    /// sentence would leave a server that reads like an invitation to work the costs out,
    /// which is the one thing this surface exists to prevent.
    /// </summary>
    [Fact]
    public async Task TheServerSaysWhoDecidesBeforeAnythingIsCalled()
    {
        await WithClient(client =>
        {
            Assert.NotNull(client.ServerInstructions);
            Assert.Contains("the engine decides", client.ServerInstructions!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(CharacterServer.CheckCharacterTool, client.ServerInstructions!, StringComparison.Ordinal);
            Assert.Contains(CharacterServer.CreationGuideTool, client.ServerInstructions!, StringComparison.Ordinal);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// A whole character, across the transport, priced and judged. This is the end-to-end
    /// claim of the slice: a client sends a description of a character as JSON and gets the
    /// engine's answer back — the same answer the engine gives in this process.
    /// </summary>
    [Fact]
    public async Task ACharacterSentOverTheWireIsCostedByTheEngine()
    {
        var sheet = SampleCharacters.Hero();
        var expected = _f.Costs.TotalCost(sheet);

        await WithClient(async client =>
        {
            var report = await Call(client, CharacterServer.CheckCharacterTool,
                new Dictionary<string, object?>
                {
                    ["character"] = JsonSerializer.Deserialize<JsonElement>(CharacterSheetJson.Write(sheet))
                });

            Assert.True(report["ok"]!.GetValue<bool>(), report.ToJsonString());
            Assert.Equal(expected, report["hero_points"]!["spent"]!.GetValue<int>());
            Assert.Equal(_f.Derived.CalculateEdge(sheet), report["derived"]!["edge"]!.GetValue<int>());
        });
    }

    /// <summary>
    /// A character sent as a JSON <em>string</em> rather than an object. A client is free to
    /// do either, the schema cannot stop it, and refusing one would read to the person on the
    /// other end as the tool being broken rather than as their client being unusual.
    /// </summary>
    [Fact]
    public async Task ACharacterSentAsAStringIsReadTheSameWay()
    {
        var sheet = SampleCharacters.Hero();

        await WithClient(async client =>
        {
            var report = await Call(client, CharacterServer.CheckCharacterTool,
                new Dictionary<string, object?> { ["character"] = CharacterSheetJson.Write(sheet) });

            Assert.Equal(_f.Costs.TotalCost(sheet), report["hero_points"]!["spent"]!.GetValue<int>());
        });
    }

    // ── The judge ─────────────────────────────────────────────────────────

    private JsonNode Check(CharacterSheet sheet) =>
        Parse(Tools().CheckCharacter(Element(CharacterSheetJson.Write(sheet))));

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json) ?? throw new InvalidOperationException($"Not JSON: {json}");

    /// <summary>
    /// <b>Every figure the tool reports is the engine's own answer.</b> Not "a number that
    /// looks right" — the same number the calculator returns for the same sheet, asserted for
    /// each figure separately, because a total can agree while a part does not.
    ///
    /// <para>This is the guard against the failure the whole ordering exists to prevent: a
    /// front end that works a cost out for itself and reports it confidently.</para>
    /// </summary>
    [Fact]
    public void EveryFigureReportedIsTheEnginesOwnAnswer()
    {
        // Illegal sheets as well as legal ones. A character that breaks a rule is still costed
        // and its figures are what the conversation quotes when deciding what to give up, so
        // "the numbers are only trustworthy while the character is legal" would be no use.
        var overBudget = _f.LegalSheet();
        foreach (var ability in _f.Rules.Abilities) overBudget.AbilityRanks[ability.Id] = 12;
        foreach (var talent in _f.Rules.Talents) overBudget.TalentRanks[talent.Id] = 12;

        var aboveCap = _f.LegalSheet();
        aboveCap.AbilityRanks["intellect"] = 40;

        var sheets = new[]
        {
            SampleCharacters.Hero(), SampleCharacters.Villain(), _f.LegalSheet(),
            overBudget, aboveCap
        };

        Assert.Contains(sheets, s => !_f.Validator.Validate(s).IsValid);

        foreach (var sheet in sheets)
        {
            var report = Check(sheet);

            Assert.Equal(_f.Costs.TotalCost(sheet), report["hero_points"]!["spent"]!.GetValue<int>());
            Assert.Equal(_f.Rules.GetTier(sheet.SelectedTierId!)!.HeroPoints,
                report["hero_points"]!["budget"]!.GetValue<int>());
            Assert.Equal(_f.Rules.GetTier(sheet.SelectedTierId!)!.HeroPoints - _f.Costs.TotalCost(sheet),
                report["hero_points"]!["remaining"]!.GetValue<int>());
            Assert.Equal(_f.Rules.GetTier(sheet.SelectedTierId!)!.TraitCapRank,
                report["trait_cap"]!.GetValue<int>());

            Assert.Equal(_f.Derived.CalculateEdge(sheet), report["derived"]!["edge"]!.GetValue<int>());
            Assert.Equal(_f.Derived.CalculateHealth(sheet), report["derived"]!["health"]!.GetValue<int>());
            Assert.Equal(_f.Derived.CalculateResolve(sheet), report["derived"]!["resolve"]!.GetValue<int>());

            var spending = report["spending"]!;
            Assert.Equal(_f.Costs.AbilityCost(sheet), spending["abilities"]!.GetValue<int>());
            Assert.Equal(_f.Costs.TalentCost(sheet), spending["talents"]!.GetValue<int>());
            Assert.Equal(_f.Costs.PackageCost(sheet), spending["package"]!.GetValue<int>());
            Assert.Equal(_f.Costs.TotalPowersCost(sheet), spending["powers"]!.GetValue<int>());
            Assert.Equal(_f.Costs.TotalPerksCost(sheet), spending["perks"]!.GetValue<int>());
            Assert.Equal(_f.Costs.TotalGearCost(sheet), spending["gear"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// And the per-Power figures, which are what a conversation about an unaffordable
    /// character actually points at. A breakdown that named the right Powers with the wrong
    /// numbers beside them would pass a test that only checked the total.
    /// </summary>
    [Fact]
    public void ThePerPowerBreakdownIsTheEnginesToo()
    {
        var sheet = SampleCharacters.Hero();
        var byPower = Check(sheet)["spending"]!["by_power"]!.AsArray();

        Assert.NotEmpty(byPower);
        Assert.Equal(sheet.SelectedPowers.Count, byPower.Count);

        for (var i = 0; i < sheet.SelectedPowers.Count; i++)
        {
            var selection = sheet.SelectedPowers[i];

            Assert.Equal(selection.PowerId, byPower[i]!["power_id"]!.GetValue<string>());
            Assert.Equal(_f.Rules.GetPower(selection.PowerId)!.Name,
                byPower[i]!["name"]!.GetValue<string>());
            Assert.Equal(_f.Costs.PowerCost(selection), byPower[i]!["hero_points"]!.GetValue<int>());
            Assert.Equal(_f.Derived.GetEffectiveRank(selection, sheet),
                byPower[i]!["effective_rank"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// And the Perks, which had no assertion at all while their sibling had a thorough one —
    /// so emptying <c>by_perk</c> was invisible. Per-unit Perks are where a quantity was worth
    /// unlimited Hero Points once already, which makes their line the one worth showing.
    /// </summary>
    [Fact]
    public void ThePerPerkBreakdownIsTheEnginesToo()
    {
        var sheet = SampleCharacters.Hero();
        var byPerk = Check(sheet)["spending"]!["by_perk"]!.AsArray();

        Assert.NotEmpty(sheet.Perks);
        Assert.Equal(sheet.Perks.Count, byPerk.Count);

        for (var i = 0; i < sheet.Perks.Count; i++)
        {
            var perk = sheet.Perks[i];

            Assert.Equal(perk.PerkId, byPerk[i]!["perk_id"]!.GetValue<string>());
            Assert.Equal(_f.Rules.GetPerk(perk.PerkId)!.Name, byPerk[i]!["name"]!.GetValue<string>());
            Assert.Equal(perk.Units, byPerk[i]!["units"]!.GetValue<int>());
            Assert.Equal(_f.Costs.PerkCost(perk), byPerk[i]!["hero_points"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// The character block: which character this report is about. It is what a conversation
    /// holding two drafts tells them apart by, and it was emitted and never read.
    /// </summary>
    [Fact]
    public void TheReportSaysWhichCharacterItIsAbout()
    {
        var sheet = SampleCharacters.Hero();
        var character = Check(sheet)["character"]!;

        Assert.Equal(sheet.Name, character["name"]!.GetValue<string>());
        Assert.Equal(sheet.SelectedTierId, character["tier"]!.GetValue<string>());
        Assert.Equal(sheet.SelectedPackageId, character["package"]!.GetValue<string>());
    }

    /// <summary>
    /// <b>Legality is the validator's word and nothing else's.</b> Checked over a set of
    /// sheets that includes illegal ones, because a tool that always said "legal" would pass
    /// a test run only over legal characters — and that is the failure that matters, since it
    /// is the one that certifies somebody's character wrongly.
    /// </summary>
    [Fact]
    public void TheVerdictIsTheValidatorsAnswerAndNotThisPrograms()
    {
        var overBudget = _f.LegalSheet();
        foreach (var ability in _f.Rules.Abilities) overBudget.AbilityRanks[ability.Id] = 12;
        foreach (var talent in _f.Rules.Talents) overBudget.TalentRanks[talent.Id] = 12;

        var aboveCap = _f.LegalSheet();
        aboveCap.AbilityRanks["intellect"] = 40;

        var noTier = _f.LegalSheet();
        noTier.SelectedTierId = null;

        var sheets = new[]
        {
            SampleCharacters.Hero(), SampleCharacters.Villain(), _f.LegalSheet(),
            overBudget, aboveCap, noTier, new CharacterSheet()
        };

        foreach (var sheet in sheets)
        {
            var expected = _f.Validator.Validate(sheet);
            var report = Check(sheet);

            Assert.Equal(expected.IsValid, report["ok"]!.GetValue<bool>());
            Assert.Equal(expected.IsValid ? "legal" : "breaks_a_rule", report["verdict"]!.GetValue<string>());

            Assert.Equal(
                expected.Issues.Select(i => i.Code).ToList(),
                report["issues"]!.AsArray().Select(i => i!["code"]!.GetValue<string>()).ToList());
        }

        // And the illegal ones really were illegal, so the loop above was not vacuous.
        Assert.False(Check(overBudget)["ok"]!.GetValue<bool>());
        Assert.False(Check(aboveCap)["ok"]!.GetValue<bool>());
    }

    /// <summary>
    /// The overspend is the number a conversation quotes, so it has to be exact and it has to
    /// be negative. Reporting a floor of zero, or the absolute value, would send somebody
    /// looking for the wrong amount to give up.
    /// </summary>
    [Fact]
    public void AnUnaffordableCharacterReportsTheOverspendExactly()
    {
        var sheet = _f.LegalSheet();
        foreach (var ability in _f.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 12;
        foreach (var talent in _f.Rules.Talents) sheet.TalentRanks[talent.Id] = 12;

        var report = Check(sheet);
        var budget = _f.Rules.GetTier("standard")!.HeroPoints;

        Assert.False(report["ok"]!.GetValue<bool>());
        Assert.Equal(budget - _f.Costs.TotalCost(sheet), report["hero_points"]!["remaining"]!.GetValue<int>());
        Assert.True(report["hero_points"]!["remaining"]!.GetValue<int>() < 0);
        Assert.Contains(report["issues"]!.AsArray(),
            i => i!["code"]!.GetValue<string>() == "HP_BUDGET_EXCEEDED");
    }

    /// <summary>
    /// <b>A figure the engine cannot supply comes back null, never 0.</b> Reporting 0 for a
    /// character that cannot be priced is a lie a caller acts on — it reads as "free", which
    /// is the cheapest character there is.
    /// </summary>
    [Fact]
    public void AFigureTheEngineCannotSupplyIsNullRatherThanZero()
    {
        var sheet = _f.LegalSheet();

        // A variable-cost Power with no variant chosen has no cost at all, and asking for one
        // throws rather than guessing.
        var variable = _f.Rules.Powers.First(p => p.CostType == "per_rank_variable");
        sheet.SelectedPowers.Add(new SelectedPower(variable.Id, 2));

        var report = Check(sheet);

        Assert.Null(report["hero_points"]!["spent"]);
        Assert.Null(report["spending"]!["powers"]);
        Assert.False(report["ok"]!.GetValue<bool>());
        Assert.Contains(report["issues"]!.AsArray(),
            i => i!["code"]!.GetValue<string>() == "POWER_VARIANT_NOT_CHOSEN");
    }

    /// <summary>
    /// <b>The backstop for a legal character the engine cannot price</b>, which no character
    /// can reach: <see cref="CharacterValidator"/> reports <c>CHARACTER_NOT_PRICEABLE</c> for
    /// every sheet whose total throws, so a legal character always has a total. It is here
    /// because a caller told "fix the errors and the figure appears" about a character with no
    /// errors has a loop with no way out — and because the branch was documented in the guide
    /// as one of three verdicts while nothing in the suite ever produced it.
    /// </summary>
    [Fact]
    public void ALegalCharacterWithNoTotalIsThisProgramsFaultAndSaysSo()
    {
        var sheet = _f.LegalSheet();
        var judgement = new Judgement(_f.Rules, _f.Costs, _f.Derived, _f.Validator);

        var report = judgement.Report(
            sheet, new ValidationResult([]), _f.Rules.GetTier("standard"), spent: null);

        Assert.False(report["ok"]!.GetValue<bool>());
        Assert.Equal("engine_could_not_answer", report["verdict"]!.GetValue<string>());

        var issue = Assert.Single(report["issues"]!.AsArray());
        Assert.Equal("ENGINE_COULD_NOT_ANSWER", issue!["code"]!.GetValue<string>());
        Assert.Contains("fault in this program", issue["message"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <c>Judge</c> asks the engine and then hands the answers to <c>Report</c>, which is the
    /// seam the test above uses. That is only worth having if the two agree, so this drives
    /// <c>Judge</c> directly and checks it against the same document built from the engine's
    /// own figures — otherwise the branch above is tested through a door nothing walks
    /// through.
    /// </summary>
    [Fact]
    public void JudgingACharacterIsAskingTheEngineAndThenReporting()
    {
        var sheet = SampleCharacters.Hero();
        var judgement = new Judgement(_f.Rules, _f.Costs, _f.Derived, _f.Validator);

        var judged = judgement.Judge(sheet);

        var reported = judgement.Report(
            sheet,
            _f.Validator.Validate(sheet),
            _f.Rules.GetTier(sheet.SelectedTierId!),
            _f.Costs.TotalCost(sheet));

        Assert.Equal(reported.ToJsonString(), judged.ToJsonString());
    }

    /// <summary>
    /// And the guarantee that keeps that branch dark, asserted where it actually lives: a
    /// character the engine cannot price is one the validator refuses. If that ever stops
    /// being true, the branch above stops being unreachable — and this is the test that says
    /// so, rather than a comment claiming it.
    /// </summary>
    [Fact]
    public void ACharacterTheEngineCannotPriceIsAlwaysRefusedByTheValidator()
    {
        var variable = _f.LegalSheet();
        variable.SelectedPowers.Add(
            new SelectedPower(_f.Rules.Powers.First(p => p.CostType == "per_rank_variable").Id, 2));

        var enormous = _f.LegalSheet();
        enormous.Perks.Add(new SelectedPerk("contacts", int.MaxValue));

        var invented = _f.LegalSheet();
        invented.SelectedPowers.Add(new SelectedPower("no_such_power", 1));

        foreach (var sheet in new[] { variable, enormous, invented })
        {
            var priced = true;
            try { _ = _f.Costs.TotalCost(sheet); }
            catch (Exception e) when (Judgement.IsUnanswerable(e)) { priced = false; }

            Assert.False(priced, "This sheet was supposed to be one the engine cannot price.");
            Assert.False(_f.Validator.Validate(sheet).IsValid);
        }
    }

    /// <summary>
    /// The issues carry the facts as well as the sentence, because a repair loop that has to
    /// parse English back into the numbers it was built from will get it wrong. Same fields as
    /// the <c>build</c> command's report, and the same wire spelling.
    /// </summary>
    [Fact]
    public void AnIssueCarriesTheFactsToRepairFrom()
    {
        var sheet = _f.LegalSheet();
        sheet.AbilityRanks["intellect"] = 40;

        var issue = Assert.Single(Check(sheet)["issues"]!.AsArray(),
            i => i!["code"]!.GetValue<string>() == "TRAIT_ABOVE_CAP");

        Assert.Equal("ability", issue!["subject_kind"]!.GetValue<string>());
        Assert.Equal("intellect", issue["subject_id"]!.GetValue<string>());
        Assert.Equal(40, issue["value"]!.GetValue<int>());
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, issue["limit"]!.GetValue<int>());
    }

    /// <summary>
    /// A warning does not become an "illegal" verdict, which would send a repair loop after a
    /// character that is fine.
    ///
    /// <para><b>The sheet is the Villain, and that is the whole test.</b> It was the Hero,
    /// which carries no findings at all — so the assertion compared zero warnings to zero
    /// warnings, and reporting every warning in every report as an error would have passed.
    /// The Villain deliberately leaves one Power without a Source, so there is a warning here
    /// to be got wrong.</para>
    /// </summary>
    [Fact]
    public void AWarningDoesNotMakeACharacterIllegal()
    {
        var sheet = SampleCharacters.Villain();
        var expected = _f.Validator.Validate(sheet);

        Assert.True(expected.IsValid);
        Assert.NotEmpty(expected.Warnings);

        var report = Check(sheet);

        Assert.True(report["ok"]!.GetValue<bool>());
        Assert.Equal("legal", report["verdict"]!.GetValue<string>());
        Assert.Equal(
            expected.Warnings.Count(),
            report["issues"]!.AsArray().Count(i => i!["severity"]!.GetValue<string>() == "warning"));
        Assert.DoesNotContain(report["issues"]!.AsArray(),
            i => i!["severity"]!.GetValue<string>() == "error");
    }

    // ── Input this program did not write ──────────────────────────────────

    /// <summary>
    /// <b>A payload that is not a character is reported, never thrown.</b> An exception across
    /// the transport arrives as a protocol error with a C# type name in it, which a model
    /// cannot act on and a person cannot read. Every one of these is something a client will
    /// send eventually.
    /// </summary>
    private static readonly string[] RefusalCodes = ["NO_CHARACTER", "CHARACTER_UNREADABLE"];

    /// <summary>What a row that matched on nothing but a word in its own entry reports.</summary>
    private static readonly string[] DescriptionOnly = ["description"];

    /// <summary>The searchable words of "turns invisible" — the filler is dropped.</summary>
    private static readonly string[] TurnsInvisible = ["turns", "invisible"];

    [Theory]
    [InlineData("\"a washed-up boxer who punches through time\"")] // the description, not a character
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"\"")]
    [InlineData("{ \"AbilityRanks\": null }")]                    // well-formed, and a null collection
    [InlineData("{ \"AbilityRnaks\": { \"might\": 8 } }")]        // a misspelled field
    [InlineData("{ \"SelectedTierId\": 12 }")]                    // right field, wrong type
    public void APayloadThatIsNotACharacterIsReportedRatherThanThrown(string payload)
    {
        var answer = Parse(Tools().CheckCharacter(Element(payload)));

        Assert.False(answer["ok"]!.GetValue<bool>());
        Assert.Contains(answer["problem"]!["code"]!.GetValue<string>(), RefusalCodes);
        Assert.False(string.IsNullOrWhiteSpace(answer["problem"]!["message"]!.GetValue<string>()));
    }

    /// <summary>
    /// <b>A correctly named field holding the wrong kind of value is not a misspelling, and
    /// is not reported as one.</b> `"might": "8d"` is the rank written the way the rulebook
    /// writes it — the likeliest first mistake there is — and the answer used to send a repair
    /// loop hunting for a spelling error that did not exist.
    /// </summary>
    [Fact]
    public void AWrongValueAndAWrongFieldNameAreToldApart()
    {
        var wrongValue = Parse(Tools().CheckCharacter(Element(
            """{ "SelectedTierId": "standard", "AbilityRanks": { "might": "8d" } }""")));

        Assert.Equal("CHARACTER_UNREADABLE", wrongValue["problem"]!["code"]!.GetValue<string>());

        var said = wrongValue["problem"]!["message"]!.GetValue<string>();
        Assert.Contains("wrong kind of thing", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("spelling", said, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AbilityRanks.might", said, StringComparison.Ordinal);

        var wrongName = Parse(Tools().CheckCharacter(Element(
            """{ "SelectedTierId": "standard", "AbilityRnaks": { "might": 8 } }""")));

        Assert.Contains("spelling", wrongName["problem"]!["message"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Half-written JSON, which cannot arrive as an object — a client that could not parse it
    /// would not have got this far — but can and does arrive as a string, which is the shape a
    /// model uses when it builds the character as text.
    /// </summary>
    [Fact]
    public void HalfWrittenJsonInAStringIsReportedRatherThanThrown()
    {
        var answer = Parse(Tools().CheckCharacter(
            JsonSerializer.SerializeToElement("{ \"SelectedTierId\": ")));

        Assert.False(answer["ok"]!.GetValue<bool>());
        Assert.Equal("CHARACTER_UNREADABLE", answer["problem"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// <b>A misspelled field name is refused rather than ignored</b>, which is the same rule
    /// the <c>build</c> command applies to a submitted file. Ignoring it hands back a cheaper,
    /// legal character with a section silently missing — the worst answer available, because
    /// nothing in it looks wrong.
    /// </summary>
    [Fact]
    public void AMisspelledFieldIsRefusedRatherThanSilentlyDroppingASection()
    {
        var answer = Parse(Tools().CheckCharacter(Element(
            """{ "SelectedTierId": "standard", "AbilityRnaks": { "might": 8 } }""")));

        Assert.False(answer["ok"]!.GetValue<bool>());
        Assert.Equal("CHARACTER_UNREADABLE", answer["problem"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// <b>A null argument is answered, not thrown at.</b> A schema saying "string" does not
    /// stop a client sending <c>null</c>, and one arrives here as one — so the guards on these
    /// three are load-bearing rather than defensive habit, and the parameters are declared
    /// nullable to say so. Declared non-null they read as dead code to an inspection, and
    /// removing them would turn each of these answers into an exception across the transport.
    /// </summary>
    [Fact]
    public void ANullArgumentIsAnAnswerRatherThanACrash()
    {
        Assert.Equal("NO_SUCH_CATEGORY",
            Parse(Tools().ListOptions(null))["problem"]!["code"]!.GetValue<string>());

        Assert.Equal("EMPTY_QUERY",
            Parse(Tools().SearchPowers(null))["problem"]!["code"]!.GetValue<string>());

        Assert.Equal("NO_SUCH_POWER",
            Parse(Tools().PowerDetail(null))["problem"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// And the same over the wire, because the argument has to survive being deserialized as
    /// well as being handled — this is the shape a client actually sends.
    /// </summary>
    [Fact]
    public async Task ANullArgumentOverTheWireIsAnsweredToo()
    {
        await WithClient(async client =>
        {
            var report = await Call(client, CharacterServer.ListOptionsTool,
                new Dictionary<string, object?> { ["category"] = null });

            Assert.False(report["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_CATEGORY", report["problem"]!["code"]!.GetValue<string>());
        });
    }

    /// <summary>
    /// The empty-argument case, which every client produces at least once — a model calling a
    /// tool before it has a character to send.
    /// </summary>
    [Fact]
    public void NoCharacterAtAllIsAnAnswerRatherThanACrash()
    {
        foreach (var report in new[]
                 {
                     Parse(Tools().CheckCharacter(default)),
                     Parse(Tools().CharacterSheetText(default))
                 })
        {
            Assert.False(report["ok"]!.GetValue<bool>());
            Assert.Equal("NO_CHARACTER", report["problem"]!["code"]!.GetValue<string>());
        }
    }

    // ── The sheet ─────────────────────────────────────────────────────────

    /// <summary>
    /// The printed sheet is the same document the CLI writes and the browser downloads —
    /// rendered by the same renderer, not a third copy of the layout.
    /// </summary>
    [Fact]
    public void TheSheetIsTheOneTheOtherFrontEndsPrint()
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Both samples, and the Villain matters: it is the one with a finding on it. Rendered
        // against a sheet with none, handing the renderer an empty ValidationResult instead of
        // the validator's own answer produced a byte-identical document, so the sheet could
        // have printed with its findings section silently blank.
        foreach (var sheet in new[] { SampleCharacters.Hero(), SampleCharacters.Villain() })
        {
            var expected = Sheets.CharacterSheetRenderer.RenderText(
                sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), stamp);

            Assert.Equal(expected, Tools().CharacterSheetText(Element(CharacterSheetJson.Write(sheet))));
        }

        Assert.NotEmpty(_f.Validator.Validate(SampleCharacters.Villain()).Issues);
    }

    /// <summary>
    /// A character the engine cannot price has no sheet, and says so rather than throwing —
    /// the sheet prints costs, so there is nothing to print.
    /// </summary>
    [Fact]
    public void ACharacterThatCannotBePricedHasNoSheetAndSaysSo()
    {
        var sheet = _f.LegalSheet();
        var variable = _f.Rules.Powers.First(p => p.CostType == "per_rank_variable");
        sheet.SelectedPowers.Add(new SelectedPower(variable.Id, 2));

        var answer = Parse(Tools().CharacterSheetText(Element(CharacterSheetJson.Write(sheet))));

        Assert.Equal("NO_SHEET_TO_PRINT", answer["problem"]!["code"]!.GetValue<string>());
    }

    // ── The catalogues ────────────────────────────────────────────────────

    /// <summary>
    /// Every category answers, and every id it hands back is one the rules have. An id that
    /// does not resolve is worse than a missing category: it is refused later, by the
    /// validator, as though the person who used it had invented it.
    /// </summary>
    [Fact]
    public void EveryCategoryAnswersWithIdsTheRulesHave()
    {
        foreach (var category in CharacterTools.Categories)
        {
            var report = Parse(Tools().ListOptions(category));

            Assert.True(report["ok"]!.GetValue<bool>(), category);

            var entries = report["entries"]!.AsArray();
            Assert.NotEmpty(entries);

            foreach (var id in entries.Select(e => e!["id"]!.GetValue<string>()))
            {
                var known = category switch
                {
                    "tiers"         => _f.Rules.GetTier(id) is not null,
                    "packages"      => _f.Rules.CreationRules.OptionalPackages.Any(p => p.Id == id),
                    "abilities"     => _f.Rules.GetAbility(id) is not null,
                    "talents"       => _f.Rules.GetTalent(id) is not null,
                    "sources"       => _f.Rules.GetSource(id) is not null,
                    "perks"         => _f.Rules.GetPerk(id) is not null,
                    "flaws"         => _f.Rules.GetFlaw(id) is not null,
                    "pros"          => _f.Rules.GetPro(id) is not null,
                    "cons"          => _f.Rules.GetCon(id) is not null,
                    "gear_features" => _f.Rules.GetGearFeature(id) is not null,
                    _               => false
                };

                Assert.True(known, $"{category} offered '{id}', which the rules do not have.");
            }
        }
    }

    /// <summary>
    /// The tiers carry the two numbers everything else is measured against. Read from the
    /// rules, never from a memory of them — this is the figure the assistant is most likely
    /// to think it knows.
    /// </summary>
    [Fact]
    public void TheTiersCarryTheBudgetAndTheCapFromTheRules()
    {
        var entries = Parse(Tools().ListOptions("tiers"))["entries"]!.AsArray();

        Assert.Equal(_f.Rules.Tiers.Count, entries.Count);

        foreach (var (entry, tier) in entries.Zip(_f.Rules.Tiers))
        {
            Assert.Equal(tier.HeroPoints, entry!["hero_points"]!.GetValue<int>());
            Assert.Equal(tier.TraitCapRank, entry["trait_cap"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// <b>And the numbers beside the ids, which is what the catalogues are for.</b> Checking
    /// only that the ids resolve left every figure free: reporting each Pro's Hero Point cost
    /// as zero passed, and an assistant reading that would offer them all as free.
    ///
    /// <para>Cons are asserted negative for the same reason — the sign is the difference
    /// between a discount and a surcharge, and it is stored rather than derived.</para>
    /// </summary>
    [Fact]
    public void TheCatalogueNumbersAreTheRulesOwn()
    {
        var packages = Parse(Tools().ListOptions("packages"))["entries"]!.AsArray();

        foreach (var (entry, package) in packages.Zip(_f.Rules.CreationRules.OptionalPackages))
        {
            Assert.Equal(package.Cost, entry!["hero_points"]!.GetValue<int>());
            Assert.Equal(package.AbilitiesRank, entry["grants_every_ability"]!.GetValue<int>());
            Assert.Equal(package.TalentsRank, entry["grants_every_talent"]!.GetValue<int>());
        }

        var abilities = Parse(Tools().ListOptions("abilities"))["entries"]!.AsArray();

        foreach (var (entry, ability) in abilities.Zip(_f.Rules.Abilities))
        {
            Assert.Equal(ability.CostPerRank, entry!["hero_points_per_rank"]!.GetValue<int>());
            Assert.Equal(ability.OrdinaryHumanRank, entry["ordinary_human_rank"]!.GetValue<int>());
        }

        var talents = Parse(Tools().ListOptions("talents"))["entries"]!.AsArray();

        foreach (var (entry, talent) in talents.Zip(_f.Rules.Talents))
        {
            Assert.Equal(talent.CostPerRank, entry!["hero_points_per_rank"]!.GetValue<int>());
            Assert.Equal(talent.LinkedAbility, entry["linked_ability"]!.GetValue<string>());
        }

        var perks = Parse(Tools().ListOptions("perks"))["entries"]!.AsArray();

        foreach (var (entry, perk) in perks.Zip(_f.Rules.Perks))
        {
            Assert.Equal(perk.CostType, entry!["cost_type"]!.GetValue<string>());
            Assert.Equal(perk.Cost, entry["hero_points"]?.GetValue<int>());
            Assert.Equal(perk.CostPerUnit, entry["hero_points_per_unit"]?.GetValue<int>());
        }

        var pros = Parse(Tools().ListOptions("pros"))["entries"]!.AsArray();

        foreach (var (entry, pro) in pros.Zip(_f.Rules.Pros))
        {
            Assert.Equal(pro.CostModifier, entry!["hero_points"]?.GetValue<int>());
            Assert.Equal(pro.AppliesToRanges,
                entry["applies_to_ranges"]!.AsArray().Select(r => r!.GetValue<string>()).ToList());
            Assert.Equal(pro.ApplicabilityCaveat, entry["caveat"]?.GetValue<string>());
        }

        var cons = Parse(Tools().ListOptions("cons"))["entries"]!.AsArray();

        foreach (var (entry, con) in cons.Zip(_f.Rules.Cons))
        {
            Assert.Equal(con.CostModifier, entry!["hero_points"]?.GetValue<int>());
            Assert.Equal(con.AppliesToRankTypes,
                entry["applies_to_rank_types"]!.AsArray().Select(r => r!.GetValue<string>()).ToList());
        }

        // A Con is stored as a negative number, and reporting one as positive would read as a
        // surcharge. Every flat Con, and there is at least one.
        var flatCons = cons.Where(e => e!["hero_points"] is not null).ToList();
        Assert.NotEmpty(flatCons);
        Assert.All(flatCons, e => Assert.True(e!["hero_points"]!.GetValue<int>() <= 0));

        var features = Parse(Tools().ListOptions("gear_features"))["entries"]!.AsArray();

        foreach (var (entry, feature) in features.Zip(_f.Rules.GearFeatures))
        {
            Assert.Equal(feature.Cost, entry!["hero_points"]?.GetValue<int>());
            Assert.Equal(feature.AppliesTo, entry["applies_to"]!.GetValue<string>());
        }

        var sources = Parse(Tools().ListOptions("sources"))["entries"]!.AsArray();

        foreach (var (entry, source) in sources.Zip(_f.Rules.Sources))
            Assert.Equal(source.DefaultRankAbility, entry!["default_rank_ability"]!.GetValue<string>());
    }

    /// <summary>
    /// The flaw count at creation, which is a rule rather than a number on any flaw: one to
    /// three, and a fourth is refused rather than charged for.
    /// </summary>
    [Fact]
    public void TheFlawsCatalogueCarriesTheLimitsAtCreation()
    {
        var report = Parse(Tools().ListOptions("flaws"));
        var rules = _f.Rules.CreationRules.FlawRules;

        Assert.Equal(rules.MinAtCreation, report["at_creation"]!["minimum"]!.GetValue<int>());
        Assert.Equal(rules.MaxAtCreation, report["at_creation"]!["maximum"]!.GetValue<int>());
    }

    /// <summary>An unknown category names the ones there are, rather than answering nothing.</summary>
    [Fact]
    public void AnUnknownCategoryNamesTheOnesThereAre()
    {
        var report = Parse(Tools().ListOptions("powers"));

        Assert.False(report["ok"]!.GetValue<bool>());
        Assert.Equal("NO_SUCH_CATEGORY", report["problem"]!["code"]!.GetValue<string>());

        var message = report["problem"]!["message"]!.GetValue<string>();
        Assert.All(CharacterTools.Categories, c => Assert.Contains(c, message, StringComparison.Ordinal));
    }

    // ── Powers ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Search never returns a Power that does not exist</b>, whatever it is asked. That is
    /// the property that matters: a made-up id survives into a character, where it becomes
    /// either an error or — if it happens to collide with a real one — a Power nobody chose.
    /// </summary>
    [Theory]
    [InlineData("punches through time")]
    [InlineData("turns invisible")]
    [InlineData("talks to fish")]
    [InlineData("plays the harmonica beautifully")]
    [InlineData("armor")]
    [InlineData("超能力")]
    [InlineData("!!!")]
    public void SearchOnlyEverReturnsPowersTheRulesHave(string query)
    {
        var report = Parse(Tools().SearchPowers(query));

        // A query with nothing searchable in it is refused, which is an answer and not a match.
        if (report["ok"]!.GetValue<bool>() is false)
        {
            Assert.Equal("EMPTY_QUERY", report["problem"]!["code"]!.GetValue<string>());
            return;
        }

        foreach (var match in report["matches"]!.AsArray())
            Assert.NotNull(_f.Rules.GetPower(match!["id"]!.GetValue<string>()));
    }

    /// <summary>
    /// And it finds the obvious things, or it is an honest tool that is no use. Both halves
    /// matter: the test above is satisfied by a search that always returns nothing.
    /// </summary>
    [Theory]
    [InlineData("turns invisible", "invisibility")]
    [InlineData("flying", "flight")]
    [InlineData("reads minds", "telepathy")]
    [InlineData("armor plating", "armor")]
    [InlineData("regenerates from injury", "regeneration")]
    public void SearchFindsTheObviousPower(string query, string expected)
    {
        var ids = Parse(Tools().SearchPowers(query))["matches"]!.AsArray()
            .Select(m => m!["id"]!.GetValue<string>()).ToList();

        Assert.Contains(expected, ids);
    }

    /// <summary>
    /// The honest half of the search: a caller is told these are the closest entries rather
    /// than a promise, and told not to invent an id. Without that sentence a model handed
    /// five vaguely related Powers reads them as the answer.
    /// </summary>
    [Fact]
    public void SearchSaysWhatItsMatchesAreWorth()
    {
        var report = Parse(Tools().SearchPowers("punches through time"));

        var caution = report["caution"]!.GetValue<string>();
        Assert.Contains("closest", caution, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not exist", caution, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(_f.Rules.Powers.Count, report["searched"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>The description that names something the rulebook does not have.</b> A search that
    /// always returns its five best rows reads as five answers however carefully the caution
    /// is worded, and this is the description a model is most likely to build anyway — so the
    /// case is reported as a fact rather than left to be inferred from the matched_on lists.
    ///
    /// <para>Both halves are asserted, because a flag that is always true is worth nothing:
    /// a real effect matches by name and is not flagged.</para>
    /// </summary>
    [Theory]
    [InlineData("he plays the trumpet so beautifully that people weep", true)]
    [InlineData("he can fly", true)]
    [InlineData("turns invisible", false)]
    [InlineData("reads minds", false)]
    public void SearchSaysWhenNothingMatchedByNameAtAll(string query, bool expected)
    {
        var report = Parse(Tools().SearchPowers(query));

        Assert.Equal(expected, report["nothing_matched_by_name"]!.GetValue<bool>());

        // The caution follows the flag, and it says what the flag is worth rather than what to
        // conclude from it. "He can fly" is in this theory as a true case on purpose: the flag
        // is right there and the rulebook does have the Power.
        var caution = report["caution"]!.GetValue<string>();

        Assert.Contains(
            expected ? "matched on a word inside its description" : "closest entries",
            caution, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A match on the Power's own name is never called a description-only match. This is the
    /// half that keeps the flag from being free: a flag that is always true would satisfy the
    /// theory above for its true cases and say nothing.
    /// </summary>
    [Fact]
    public void AMatchByNameIsReportedAsOne()
    {
        var report = Parse(Tools().SearchPowers("invisibility"));
        var first = report["matches"]!.AsArray()[0]!;

        Assert.Equal("invisibility", first["id"]!.GetValue<string>());
        Assert.Contains("name", first["matched_on"]!.AsArray().Select(m => m!.GetValue<string>()));
        Assert.False(report["nothing_matched_by_name"]!.GetValue<bool>());
    }

    /// <summary>
    /// "Super" is in seventeen Power names, so it carried a query on its own: "super strong"
    /// answered with Super Speed and three Super Senses options and neither Might nor Strike.
    /// In a rulebook about supers the word says nothing about what the effect is.
    /// </summary>
    [Fact]
    public void SuperDoesNotCarryASearchByItself()
    {
        var report = Parse(Tools().SearchPowers("super strong"));
        var ids = report["matches"]!.AsArray().Select(m => m!["id"]!.GetValue<string>()).ToList();

        Assert.DoesNotContain("super_speed", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("super_senses", StringComparison.Ordinal));

        // <b>And the honest answer to "super strong" is that there is no Power for it</b> —
        // strength is the Might Ability, which this tool does not search and should not
        // pretend to. So the assertions above are not satisfied by a search that has stopped
        // working: this one says the result really is empty, and which answer that produces.
        Assert.Equal(0, report["found"]!.GetValue<int>());
        Assert.Contains("Nothing matched at all",
            report["caution"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        // The word is only dropped as a whole word, so the Powers it names are still reachable.
        Assert.Contains("super_speed",
            Parse(Tools().SearchPowers("moves at superhuman speed"))["matches"]!.AsArray()
                .Select(m => m!["id"]!.GetValue<string>()));
    }

    /// <summary>
    /// <b>The flag describes the search, not the page of it that was asked for.</b> It was
    /// computed after the list was cut to <c>limit</c>, so asking for one match on a query
    /// whose name match ranked second turned "these matched by description" into "nothing
    /// matched by name" — and the guide tells an assistant to act on that by declining to
    /// build. `limit` is the caller's, and no test passed one at all.
    /// </summary>
    [Fact]
    public void ATightLimitDoesNotChangeWhatTheSearchFound()
    {
        // <b>The query matters, and the first one chosen here did not bite.</b> Its top row
        // matched by name, so cutting the list to one left a name match in it and the buggy
        // and fixed versions agreed. This one ranks a description-only row first — enough of
        // the words are inside Telepathy's entry to outscore a single name match — and puts
        // every name match below the cut.
        const string query =
            "read thoughts within distant range sense sentient probe memories armor";

        var wide = Parse(Tools().SearchPowers(query, 25));
        var narrow = Parse(Tools().SearchPowers(query, 1));

        var top = narrow["matches"]!.AsArray().Single()!;
        Assert.Equal(
            DescriptionOnly,
            top["matched_on"]!.AsArray().Select(m => m!.GetValue<string>()).ToArray());

        Assert.Contains("name",
            wide["matches"]!.AsArray().SelectMany(m => m!["matched_on"]!.AsArray())
                .Select(m => m!.GetValue<string>()));

        Assert.Equal(wide["found"]!.GetValue<int>(), narrow["found"]!.GetValue<int>());
        Assert.False(wide["nothing_matched_by_name"]!.GetValue<bool>());
        Assert.False(narrow["nothing_matched_by_name"]!.GetValue<bool>());
        Assert.Equal(wide["caution"]!.GetValue<string>(), narrow["caution"]!.GetValue<string>());
    }

    /// <summary>A limit outside the range it accepts is brought inside it rather than obeyed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ALimitOutsideTheRangeIsBroughtInsideIt(int limit)
    {
        var matches = Parse(Tools().SearchPowers("armor", limit))["matches"]!.AsArray();

        Assert.InRange(matches.Count, 1, 25);
    }

    /// <summary>
    /// A search that found nothing says so, and says nothing about "the nearest" — there is no
    /// nearest, and inviting a model to name one out of an empty list is how a Power that was
    /// never returned ends up on a character sheet.
    /// </summary>
    [Fact]
    public void ASearchThatFoundNothingDoesNotAskForTheNearest()
    {
        var report = Parse(Tools().SearchPowers("bread bakery sourdough"));

        Assert.Empty(report["matches"]!.AsArray());
        Assert.Equal(0, report["found"]!.GetValue<int>());

        var caution = report["caution"]!.GetValue<string>();
        Assert.Contains("Nothing matched at all", caution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nearest", caution, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("above", caution, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>And the description of a Power the rulebook does have is not told the rulebook has
    /// none.</b> "Fly" is not a prefix of "Flight", so Flight matches on the word inside its
    /// own entry — the first version of the note read that as "usually means the rulebook has
    /// no Power for this", which is the worst answer this tool can give a description it can
    /// actually serve.
    /// </summary>
    [Theory]
    [InlineData("he can fly", "flight")]
    [InlineData("heals fast", "healing")]
    public void ADescriptionOnlyMatchIsNotReportedAsTheRulebookHavingNothing(string query, string expected)
    {
        var report = Parse(Tools().SearchPowers(query));

        Assert.Contains(expected,
            report["matches"]!.AsArray().Select(m => m!["id"]!.GetValue<string>()));

        var caution = report["caution"]!.GetValue<string>();
        Assert.DoesNotContain("no Power for this", caution, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Read each one", caution, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>A row that matched one common word looks exactly like a row that answers the
    /// description, and the only thing that tells them apart is the word.</b> "Walks through
    /// walls" puts twenty-one Powers on two points each, every one of them on the filler word
    /// "through", and which of them a caller sees is alphabetical — so the answer says which
    /// word each row matched, that ties are unordered, and that there are more.
    /// </summary>
    [Fact]
    public void AThinMatchCanBeSeenToBeThin()
    {
        // A window this test names rather than the default one, so what it asserts about the
        // cut does not quietly become an assertion about whatever the default happens to be.
        const int window = 6;

        var report = Parse(Tools().SearchPowers("walks through walls", window));

        var onOneCommonWord = report["matches"]!.AsArray()
            .Where(m => m!["matched_terms"]!.AsArray().Count == 1
                     && m["matched_terms"]![0]!.GetValue<string>() == "through")
            .ToList();

        Assert.NotEmpty(onOneCommonWord);
        Assert.True(report["more_beyond_these"]!.GetValue<bool>());
        Assert.True(report["found"]!.GetValue<int>() > window);
        Assert.Equal(window, report["matches"]!.AsArray().Count);

        var caution = report["caution"]!.GetValue<string>();
        Assert.Contains("matched_terms", caution, StringComparison.Ordinal);
        Assert.Contains("no meaningful order", caution, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("more_beyond_these", caution, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every row says which words it matched, which is the only way a reader can tell a real
    /// match from a coincidence — "matched on its description" does not say whether the word
    /// was "invisible" or "through".
    /// </summary>
    [Fact]
    public void EveryMatchSaysWhichWordsItMatched()
    {
        var report = Parse(Tools().SearchPowers("turns invisible", 5));

        foreach (var match in report["matches"]!.AsArray())
        {
            var terms = match!["matched_terms"]!.AsArray().Select(t => t!.GetValue<string>()).ToList();

            Assert.NotEmpty(terms);
            Assert.All(terms, t => Assert.Contains(t, TurnsInvisible));
        }

        // And whether there are more than were returned, so a caller reading eight of
        // twenty-two knows the rest exist rather than assuming eight is all there is.
        Assert.True(Parse(Tools().SearchPowers("turns invisible", 1))["more_beyond_these"]!
            .GetValue<bool>());
    }

    /// <summary>
    /// A word with a different ending is the same word: "invisible" reaches Invisibility and
    /// "regenerates" reaches Regeneration.
    ///
    /// <para><b>This is what a tighter rule would have cost.</b> Requiring the leftovers to be
    /// short refuses "animals"/"animation" — a coincidence worth refusing — and refuses both
    /// of these with it, because "le"/"ility" is no shorter than "l"/"tion". A search that
    /// misses Invisibility for "invisible" is a worse tool than one that offers Animation for
    /// "animals", so the coincidence stays and `matched_terms` shows the reader the word that
    /// caused it.</para>
    /// </summary>
    [Theory]
    [InlineData("invisible", "invisibility")]
    [InlineData("regenerates", "regeneration")]
    [InlineData("armored", "armor")]
    public void AWordWithADifferentEndingIsTheSameWord(string query, string expected)
    {
        var match = Assert.Single(
            Parse(Tools().SearchPowers(query, 25))["matches"]!.AsArray(),
            m => m!["id"]!.GetValue<string>() == expected);

        Assert.Contains("name", match!["matched_on"]!.AsArray().Select(m => m!.GetValue<string>()));
    }

    /// <summary>
    /// A Power's detail offers only the options it may legally take. The pickers in both other
    /// front ends derive this the same way, from the option's own entry — so an assistant
    /// reading this list cannot propose the Ranged Pro on a Self-range Power.
    /// </summary>
    [Fact]
    public void PowerDetailOffersOnlyTheOptionsTheRulebookAllows()
    {
        foreach (var power in _f.Rules.Powers)
        {
            var report = Parse(Tools().PowerDetail(power.Id));
            var applicability = new ProConApplicability(_f.Rules);

            Assert.Equal(
                applicability.ProsFor(power).Select(p => p.Id).ToList(),
                report["pros"]!["generic"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).ToList());

            Assert.Equal(
                applicability.ConsFor(power).Select(c => c.Id).ToList(),
                report["cons"]!["generic"]!.AsArray().Select(c => c!["id"]!.GetValue<string>()).ToList());

            Assert.Equal(
                power.PowerPros.Select(p => p.Id).ToList(),
                report["pros"]!["own"]!.AsArray().Select(p => p!["id"]!.GetValue<string>()).ToList());

            // The Power's own Cons, which had no assertion at all — so serving its Pros in
            // their place was invisible, across 62 Powers and 106 options.
            Assert.Equal(
                power.PowerCons.Select(c => c.Id).ToList(),
                report["cons"]!["own"]!.AsArray().Select(c => c!["id"]!.GetValue<string>()).ToList());
        }
    }

    /// <summary>
    /// <b>And what a Power's own Pros and Cons cost, which is not one number.</b> Eleven of
    /// the 106 change the Power's rate rather than its total — Constructs' Devices is +2 Hero
    /// Points <em>per rank</em> — so reporting a per-rank figure in the flat field is wrong by
    /// a factor of the Power's rank, and nothing was reading either field.
    /// </summary>
    [Fact]
    public void APowersOwnProsAndConsCarryTheirRealPrices()
    {
        var perRank = 0;

        foreach (var power in _f.Rules.Powers.Where(p => p.PowerPros.Count + p.PowerCons.Count > 0))
        {
            var report = Parse(Tools().PowerDetail(power.Id));

            foreach (var (entry, option) in report["pros"]!["own"]!.AsArray()
                         .Zip(power.PowerPros)
                         .Concat(report["cons"]!["own"]!.AsArray().Zip(power.PowerCons)))
            {
                Assert.Equal(option.CostType, entry!["cost_type"]!.GetValue<string>());
                Assert.Equal(option.CostModifier, entry["hero_points"]?.GetValue<int>());
                Assert.Equal(option.CostPerRank, entry["hero_points_per_rank"]?.GetValue<double>());
                Assert.Equal(option.CostPerUnit, entry["hero_points_per_unit"]?.GetValue<int>());
                Assert.Equal(option.NeedsVariant, entry["needs_variant"]!.GetValue<bool>());

                if (option.CostPerRank is not null) perRank++;
            }
        }

        // The per-rank ones are the reason this test exists; if the data ever stopped having
        // any, the loop above would be asserting nothing interesting and should be revisited.
        Assert.True(perRank > 0, "No Power-specific option priced per rank was checked.");
    }

    /// <summary>
    /// A Power the rules do not have is reported by name, with the near misses, because an id
    /// guessed from a printed name is the mistake this tool exists to catch — several do not
    /// match.
    /// </summary>
    [Fact]
    public void AnUnknownPowerIsReportedWithTheNearMisses()
    {
        var report = Parse(Tools().PowerDetail("time_punch"));

        Assert.False(report["ok"]!.GetValue<bool>());
        Assert.Equal("NO_SUCH_POWER", report["problem"]!["code"]!.GetValue<string>());
        Assert.Contains("time_punch", report["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);

        // <b>The near misses, which this test's own name promises and did not read.</b>
        // Deleting the whole suggestion block left it green, and the name was the only thing
        // asserting the feature existed at all.
        var suggestions = report["problem"]!["did_you_mean"]!.AsArray()
            .Select(s => s!.GetValue<string>()).ToList();

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, id => Assert.NotNull(_f.Rules.GetPower(id)));
        Assert.Contains("time_travel", suggestions);
    }

    /// <summary>
    /// An id with nothing to go on gets the refusal without invented suggestions. The near-miss
    /// scan works on words, and a Power id is not going to be recovered from two letters.
    /// </summary>
    [Fact]
    public void AnUnknownPowerWithNothingToGoOnStillRefusesCleanly()
    {
        foreach (var id in new[] { "", "  ", "ab", "!!" })
        {
            var report = Parse(Tools().PowerDetail(id));

            Assert.False(report["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_POWER", report["problem"]!["code"]!.GetValue<string>());
        }
    }

    /// <summary>
    /// An id in the case a person reads off the page is the same Power. Every id in the rules
    /// files is lower case, and refusing "Blast" costs a round trip on the most natural
    /// mistake there is — while a name that is not an id is still refused.
    /// </summary>
    [Fact]
    public void APowerIdIsFoundWhateverCaseItIsWrittenIn()
    {
        Assert.Equal("blast", Parse(Tools().PowerDetail("Blast"))["id"]!.GetValue<string>());
        Assert.Equal("blast", Parse(Tools().PowerDetail("  BLAST "))["id"]!.GetValue<string>());

        Assert.False(Parse(Tools().PowerDetail("Blast Power"))["ok"]!.GetValue<bool>());
    }

    /// <summary>
    /// A rankless Power says so, in the field a proposer reads before buying ranks for it.
    /// Invisibility and Lightning Reflexes both look rankable and are not, and buying ranks
    /// for one is an error rather than a waste.
    /// </summary>
    [Fact]
    public void ARanklessPowerSaysItsRanksAreNotPurchasable()
    {
        Assert.False(Parse(Tools().PowerDetail("invisibility"))["ranks_purchasable"]!.GetValue<bool>());
        Assert.True(Parse(Tools().PowerDetail("armor"))["ranks_purchasable"]!.GetValue<bool>());
    }

    /// <summary>
    /// A baseline Power says where its free rank comes from, and that purchased ranks stack on
    /// top — which is how a Standard-tier character goes over the cap without anybody noticing.
    /// </summary>
    [Fact]
    public void ABaselinePowerSaysWhereItsFreeRankComesFrom()
    {
        var report = Parse(Tools().PowerDetail("armor"));

        Assert.Equal("baseline_half", report["baseline"]!["relationship"]!.GetValue<string>());
        Assert.Equal("toughness", report["baseline"]!["ability"]!.GetValue<string>());
    }

    private static JsonNode Row(JsonNode report, string half, string kind, string id) =>
        report[half]![kind]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == id)!;

    /// <summary>
    /// <b>An option that may be bought again says so, rather than leaving it to be inferred
    /// from English.</b> Blastwave's six energy types are five copies of one Pro; a model with
    /// no machine-readable signal either under-buys in silence or is refused for something the
    /// rulebook permits. It sits beside <c>needs_variant</c> because it is the same kind of
    /// fact — how to shape the selection — and that field is the precedent.
    /// </summary>
    [Fact]
    public void APowerDetailSaysWhichOptionsMayBeBoughtAgain()
    {
        var absorption = Parse(Tools().PowerDetail("energy_absorption"));

        Assert.True(Row(absorption, "pros", "own", "also_x")["repeatable"]!.GetValue<bool>());
        Assert.True(Row(absorption, "pros", "generic", "affect_inanimate")["repeatable"]!.GetValue<bool>());

        // And the ordinary case is stated rather than absent, so the field can be relied on.
        Assert.False(Row(absorption, "pros", "generic", "armor_piercing")["repeatable"]!.GetValue<bool>());

        // The per-unit Also X entries are not repeatable: there the quantity is the mechanism.
        Assert.False(Row(Parse(Tools().PowerDetail("drain")), "pros", "own", "also_x")["repeatable"]!.GetValue<bool>());
    }

    /// <summary>
    /// <b>A row that contradicts the Power it is listed on has to explain itself.</b> Force
    /// Field is <c>"range": "self"</c> and legitimately offers the Ranged Pro, whose
    /// <c>applies_to_ranges</c> reads touch and zone — which a model reading its own tool
    /// output cannot tell from a bug. The row now carries the Power's printed sentence, and
    /// the grades it offers are the ones that Power may actually pick.
    /// </summary>
    [Fact]
    public void APowerWhoseOwnTextAllowsAProSaysSoAndNarrowsTheGrades()
    {
        var forceField = Parse(Tools().PowerDetail("force_field"));

        Assert.Equal("self", forceField["range"]!.GetValue<string>());

        var zone = Row(forceField, "pros", "generic", "zone_nova");
        var why  = zone["allowed_by_this_power_text"]!.GetValue<string>();

        Assert.Contains("Zone Pro", why, StringComparison.Ordinal);
        Assert.Contains("p.29", why, StringComparison.Ordinal);

        // Only the grades this Power may take, so the document cannot offer what
        // check_character would then refuse.
        Assert.Equal(["nova_ranged", "zone_ranged"],
                     zone["grades"]!.AsObject().Select(kv => kv.Key).Order());

        // A Power that reaches the option by its own Range is untouched by any of this.
        var telekinesis = Parse(Tools().PowerDetail("telekinesis"));
        var itsZone     = Row(telekinesis, "pros", "generic", "zone_nova");

        Assert.Null(itsZone["allowed_by_this_power_text"]);
        Assert.Equal(4, itsZone["grades"]!.AsObject().Count);
    }
}
