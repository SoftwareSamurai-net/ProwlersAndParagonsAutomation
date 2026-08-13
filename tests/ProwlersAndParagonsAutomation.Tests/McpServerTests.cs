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

        await using var transport = new StreamServerTransport(
            toServer.Reader.AsStream(), toClient.Writer.AsStream(), CharacterServer.Name);

        await using var server = McpServer.Create(transport, CharacterServer.Options(Tools()));

        var running = server.RunAsync();

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()));

        try
        {
            await body(client);
        }
        finally
        {
            await client.DisposeAsync();
            await transport.DisposeAsync();
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

            Assert.Equal(
                new[]
                {
                    CharacterServer.CreationGuideTool,
                    CharacterServer.ListOptionsTool,
                    CharacterServer.SearchPowersTool,
                    CharacterServer.PowerDetailTool,
                    CharacterServer.CheckCharacterTool,
                    CharacterServer.CharacterSheetTool
                }.OrderBy(n => n, StringComparer.Ordinal).ToList(),
                served);
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
        foreach (var sheet in new[] { SampleCharacters.Hero(), SampleCharacters.Villain(), _f.LegalSheet() })
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
            Assert.Equal(_f.Costs.PowerCost(selection), byPower[i]!["hero_points"]!.GetValue<int>());
            Assert.Equal(_f.Derived.GetEffectiveRank(selection, sheet),
                byPower[i]!["effective_rank"]!.GetValue<int>());
        }
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
    /// A duplicate Power costs Hero Points twice and prints once, so the engine warns about
    /// it. The point of this test is not the warning: it is that a warning does not become an
    /// "illegal" verdict, which would send a repair loop after a character that is fine.
    /// </summary>
    [Fact]
    public void AWarningDoesNotMakeACharacterIllegal()
    {
        var sheet = SampleCharacters.Hero();
        var report = Check(sheet);

        Assert.True(report["ok"]!.GetValue<bool>());
        Assert.Equal(
            _f.Validator.Validate(sheet).Warnings.Count(),
            report["issues"]!.AsArray().Count(i => i!["severity"]!.GetValue<string>() == "warning"));
    }

    // ── Input this program did not write ──────────────────────────────────

    /// <summary>
    /// <b>A payload that is not a character is reported, never thrown.</b> An exception across
    /// the transport arrives as a protocol error with a C# type name in it, which a model
    /// cannot act on and a person cannot read. Every one of these is something a client will
    /// send eventually.
    /// </summary>
    private static readonly string[] RefusalCodes = ["NO_CHARACTER", "CHARACTER_UNREADABLE"];

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
        var sheet = SampleCharacters.Hero();
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var expected = Sheets.CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), stamp);

        Assert.Equal(expected, Tools().CharacterSheetText(Element(CharacterSheetJson.Write(sheet))));
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
    [InlineData("she bakes the best bread in the city", true)]
    [InlineData("turns invisible", false)]
    [InlineData("reads minds", false)]
    public void SearchSaysWhenNothingMatchedByNameAtAll(string query, bool expected)
    {
        var report = Parse(Tools().SearchPowers(query));

        Assert.Equal(expected, report["nothing_matched_by_name"]!.GetValue<bool>());

        if (expected)
            Assert.Contains("no Power for this",
                report["note"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        else
            Assert.Null(report["note"]);
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
        }
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
}
