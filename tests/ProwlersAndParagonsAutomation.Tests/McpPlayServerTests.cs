using System.IO.Pipelines;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.McpPlay;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The encounter server, driven the way a client drives it.
///
/// <para>What the transport tests are for is the half a direct call cannot see: that the four tools
/// are <em>reachable</em> under the names a stranger's configuration will use, that a character
/// sent as an argument arrives as the same character, and that a refusal comes back as an answer
/// rather than as a protocol error a model has no way to act on.</para>
///
/// <para><b>Nothing here asserts a die roll.</b> <c>SeededDice</c>' own documentation rules it out:
/// <c>Random</c>'s seeded sequence is not guaranteed across .NET versions, so a Health total taken
/// from a seed is a check that passes on the machine that wrote it and fails on the next runtime
/// the CI image picks up. What is asserted is what the wire promises — the rule ids, the citations,
/// and the four things a rate is quoted with.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class McpPlayServerTests
{
    private readonly RulesFixture _f;

    private readonly PlayRulesRepository _play =
        new(new FileSystemRulesSource(PlayFixture.DataPath));

    public McpPlayServerTests(RulesFixture f) => _f = f;

    private PlayTools Tools() => new(_f.Rules, _f.Derived, _play);

    // ── Over a real transport ─────────────────────────────────────────────

    /// <summary>
    /// A client and a server on either end of a pair of pipes, in this process. It is the real
    /// protocol — initialize, the capability exchange, JSON-RPC framing — over streams that happen
    /// not to be a console.
    /// </summary>
    private async Task WithClient(Func<McpClient, Task> body)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();

        // Disposed by hand rather than with `await using`, and in this order: the server's RunAsync
        // only returns once the transport is gone, so a `using` would dispose them after the wait,
        // and the wait for a run that cannot finish would hang the suite rather than fail it.
        var transport = new StreamServerTransport(
            toServer.Reader.AsStream(), toClient.Writer.AsStream(), PlayServer.Name);

        var server = McpServer.Create(transport, PlayServer.Options(Tools()));

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

        // IsError is a bool? and null means "not an error", so Assert.False would fail on every
        // successful call — which is how the character server's tests first "found" a working
        // server broken.
        Assert.NotEqual(true, result.IsError);

        return JsonNode.Parse(Text(result))
               ?? throw new InvalidOperationException($"{tool} answered with no JSON: {Text(result)}");
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    // ── The contract ──────────────────────────────────────────────────────

    /// <summary>
    /// The four tools as a stranger's configuration spells them. <b>Literals, not
    /// <c>PlayServer</c>'s constants</b>: a constant compared with itself proves nothing about a
    /// contract somebody else has written down — the same reasoning <see cref="McpServerTests"/>
    /// records for the character server's six.
    /// </summary>
    private static readonly string[] WireNames =
        ["combat_guide", "run_encounters", "start_encounter", "take_turn"];

    /// <summary>
    /// <b>The wire names are the contract.</b> A stranger configures a client against them and the
    /// assistant calls them by name; renaming a C# method must not rename a tool, and a tool
    /// dropped from the list is a method nobody can call.
    /// </summary>
    [Fact]
    public async Task TheFourToolsAreServedUnderTheirWireNames() =>
        await WithClient(async client =>
        {
            var served = (await client.ListToolsAsync()).Select(t => t.Name).Order(StringComparer.Ordinal);

            Assert.Equal(WireNames, served);
        });

    /// <summary>
    /// And the server itself is named separately from the character builder. A client registering
    /// both sees two entries, and a name collision would leave one of them unreachable with nothing
    /// to say why.
    /// </summary>
    [Fact]
    public void TheServerIsNamedApartFromTheCharacterBuilder()
    {
        Assert.Equal("prowlers-and-paragons-play", PlayServer.Name);
        Assert.NotEqual(Mcp.CharacterServer.Name, PlayServer.Name);
    }

    // ── The four tools ────────────────────────────────────────────────────

    /// <summary>The guide arrives whole, over the wire, with the rule it exists to carry in it.</summary>
    [Fact]
    public async Task TheGuideArrivesOverTheWire() =>
        await WithClient(async client =>
        {
            var result = await client.CallToolAsync("combat_guide");

            Assert.NotEqual(true, result.IsError);

            var text = Text(result);

            Assert.Contains("the engine resolves and you narrate", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Encounter.EntriesNotYetApplied", text, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Two of Chapter 8's published Heroes, through the wire, into a fight.</b>
    ///
    /// <para>The strongest end-to-end check available here, and it is end to end in a way no unit
    /// test is: a printed character is built into a <see cref="CharacterSheet"/>, written as JSON,
    /// sent across a transport, read back through the <em>strict</em> reader, turned into a
    /// combatant, and the Edge that comes out the far side has to be the Edge the authors printed.
    /// Anything that silently dropped a field on that path — a lenient reader, a misspelling, a lost
    /// Talent — changes that number.</para>
    ///
    /// <para><b>One of them is sent as a JSON object and the other as a JSON string</b>, because a
    /// client can send either and the schema cannot stop it. Refusing one on a technicality reads to
    /// the person as the tool being broken, and only driving both proves it does not.</para>
    /// </summary>
    [Fact]
    public async Task TwoPublishedHeroesOpenAFightWithThePrintedEdge()
    {
        var published = PrebuiltHeroes.All.OrderBy(h => h.Name, StringComparer.Ordinal).Take(2).ToList();

        Assert.Equal(2, published.Count);

        var sheets = published.Select(hero =>
        {
            var sheet = PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, hero);
            sheet.Name = hero.Name;
            return sheet;
        }).ToList();

        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = new JsonArray(
                    new JsonObject
                    {
                        ["kind"] = "hero",
                        ["side"] = "heroes",
                        // As an object.
                        ["character"] = JsonNode.Parse(CharacterSheetJson.Write(sheets[0]))
                    },
                    new JsonObject
                    {
                        ["kind"] = "hero",
                        ["side"] = "heroes",
                        // As a string, which is the other shape a client sends.
                        ["character"] = CharacterSheetJson.Write(sheets[1])
                    },
                    new JsonObject
                    {
                        ["kind"] = "minions",
                        ["id"] = "thugs",
                        ["name"] = "the thugs",
                        ["threat_rank"] = 6,
                        ["count"] = 4,
                        ["side"] = "villains"
                    }),
                ["seed"] = 11
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            var order = answer["turn_order"]!.AsArray()
                .ToDictionary(
                    entry => entry!["name"]!.GetValue<string>(),
                    entry => entry!["edge"]!.GetValue<int>(),
                    StringComparer.Ordinal);

            foreach (var hero in published)
            {
                Assert.True(order.ContainsKey(hero.Name),
                    $"{hero.Name} is not in the order of action: {string.Join(", ", order.Keys)}");

                Assert.Equal(hero.Edge, order[hero.Name]);
            }

            // One Adversity per Hero per issue, with no Challenge Level — the GM's pool, off the
            // entry rather than off a number here.
            Assert.Equal(2, answer["adversity"]!.GetValue<int>());

            // And the encounter is held, which is what every take_turn afterwards depends on.
            Assert.False(string.IsNullOrWhiteSpace(answer["encounter_id"]!.GetValue<string>()));
        });
    }

    /// <summary>
    /// <b>A refused sheet is an error result naming the reason, never a crash.</b>
    ///
    /// <para>The character goes through <c>CharacterSheetJson.Read(strict: true)</c>, exactly as
    /// <c>build --from</c> and the character server read one: a field name that is not part of a
    /// character is refused rather than ignored, because a misspelled <c>AbilityRanks</c> silently
    /// drops every Ability and puts a weaker combatant into a fight nobody would know was measured
    /// wrong.</para>
    ///
    /// <para><b>And it comes back as an answer, not as a thrown exception across the transport.</b>
    /// A protocol error is something a model cannot act on; a code and a sentence is something it
    /// can fix.</para>
    /// </summary>
    [Fact]
    public async Task ARefusedSheetIsAnAnswerNamingTheReason() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "hero",
                    ["character"] = new JsonObject { ["AbilityRank"] = new JsonObject { ["might"] = 8 } }
                })
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("CHARACTER_UNREADABLE", answer["problem"]!["code"]!.GetValue<string>());

            // The sentence has to say what to do, not merely that something is wrong.
            Assert.Contains("check the spelling",
                answer["problem"]!["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        });

    /// <summary>
    /// <b>One step of p.81's fight, over the wire, cited.</b>
    ///
    /// <para>The page's own opening exchange: a Hero swings 12d Might into a group of four Threat-6
    /// Minions. What is asserted is what makes this server worth anything — that the answer carries
    /// ledger lines, that each names the rule it applied, and that each cites the printed page it
    /// came from. Not the dice: this is <c>SeededDice</c>, and the seeded sequence is not guaranteed
    /// across .NET versions.</para>
    ///
    /// <para><b>Every rule id has to be an entry that exists</b>, checked against the store rather
    /// than against a list here — a ledger line citing a rule the book does not print is the exact
    /// failure the whole citation apparatus exists to prevent, and it would read as perfectly
    /// plausible prose.</para>
    /// </summary>
    [Fact]
    public async Task TakeTurnAnswersWithLedgerLinesThatCiteTheRuleAndThePage() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = new JsonArray(
                    new JsonObject
                    {
                        ["kind"] = "hero",
                        ["id"] = "soldier",
                        ["side"] = "heroes",
                        ["character"] = new JsonObject
                        {
                            ["Name"] = "Citizen Soldier",
                            ["SelectedTierId"] = "standard",
                            ["AbilityRanks"] = new JsonObject
                            {
                                ["might"] = 12, ["toughness"] = 6, ["agility"] = 4
                            }
                        }
                    },
                    new JsonObject
                    {
                        ["kind"] = "minions",
                        ["id"] = "robots",
                        ["name"] = "the robotic Minions",
                        ["threat_rank"] = 6,
                        ["count"] = 4,
                        ["side"] = "villains"
                    }),
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack",
                    ["actor"] = "soldier",
                    ["target"] = "robots",
                    ["trait_id"] = "might"
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            var added = turn["added"]!.AsArray();

            Assert.NotEmpty(added);

            var known = _play.EntryIds().Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var line in added)
            {
                var rule = line!["rule"]!.GetValue<string>();

                Assert.True(known.Contains(rule),
                    $"the ledger cites '{rule}', which is in none of the five play rules files");

                Assert.Contains("Ultimate Edition", line["source_ref"]!.GetValue<string>(), StringComparison.Ordinal);
            }

            // p.75's Attack and Defense table is what an attack is resolved against, and the line
            // has to name both sides of the roll and the Trait that was thrown.
            var attack = added.Single(l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "attacks_and_defenses", StringComparison.Ordinal));

            Assert.Contains("Ch.4 Combat, p.75", attack!["source_ref"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("Citizen Soldier", attack["text"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("the robotic Minions", attack["text"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("might 12d", attack["text"]!.GetValue<string>(), StringComparison.Ordinal);

            // And the state came back with it, which is the other half of what take_turn promises.
            Assert.Equal(1, turn["state"]!["page"]!.GetValue<int>());
            var robots = turn["state"]!["combatants"]!.AsArray()
                .Single(c => string.Equals(c!["id"]!.GetValue<string>(), "robots", StringComparison.Ordinal))!;

            Assert.Equal(4, robots["minions_left"]!.GetValue<int>() + Defeated(added));
        });

    /// <summary>
    /// How many Minions the ledger says went down, read off the line rather than off the dice — so
    /// the assertion above holds whichever way the roll fell, and still fails if the group's size
    /// and the line disagree.
    /// </summary>
    private static int Defeated(JsonArray added)
    {
        var line = added.FirstOrDefault(l =>
            string.Equals(l!["rule"]!.GetValue<string>(), "attacking_minions", StringComparison.Ordinal));

        if (line is null) return 0;

        var text = line["text"]!.GetValue<string>();
        var at = text.LastIndexOf(": ", StringComparison.Ordinal);

        Assert.True(at >= 0, $"the Minion line no longer says how many were defeated: {text}");

        return int.Parse(text[(at + 2)..].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// <b>An intent this engine does not have is refused by name, and the refusal lists the ones it
    /// does.</b> A model that guessed a verb has to be able to correct itself from the answer, and
    /// "unknown intent" on its own sends it guessing again.
    /// </summary>
    [Fact]
    public async Task AnUnknownIntentIsRefusedAndTheKindsAreNamed() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides()
            });

            var answer = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                ["intent"] = new JsonObject { ["kind"] = "teleport_behind_them", ["actor"] = "hero" }
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_INTENT", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            // Every kind, from the list the reader is actually offered — so a kind added later and
            // left out of the message fails here rather than being discovered by a caller.
            foreach (var kind in PlayTools.IntentKinds)
                Assert.Contains(kind, message, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>A table setting this engine has no switch for is refused rather than ignored.</b> A
    /// setting accepted and quietly dropped is the worst of the three possible behaviours: the
    /// answer echoes a table, the numbers do not carry it, and nothing says so.
    /// </summary>
    [Fact]
    public async Task AnUnknownTableSettingIsRefusedRatherThanIgnored() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["exploding_sixes"] = true }
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_TABLE_SETTING", answer["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("exploding_sixes",
                answer["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    // ── Measuring ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Fewer than thirty runs is refused, and the refusal says why.</b> The alternative — an
    /// answer with a caveat beside it — is read by nobody: the number is what gets quoted.
    /// </summary>
    [Fact]
    public async Task RunEncountersRefusesTooFewRuns() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns - 1
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("TOO_FEW_RUNS", answer["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("noise wearing a percentage sign",
                answer["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Every rate comes back in the same object as the four things it may not be quoted
    /// without</b> — the N, the seeds, the policy's own name, and every table switch.
    ///
    /// <para>Not a caveat and not a second call: a caller who has to ask again what a figure was
    /// measured under will quote the figure on its own. The table echo is the <em>whole</em> list
    /// rather than the settings that are on, because a report printing only what was turned on
    /// cannot be told apart from one produced by a build that had lost a switch.</para>
    /// </summary>
    [Fact]
    public async Task RunEncountersCarriesItsNSeedsPolicyAndTable() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 500,
                ["maxPages"] = 12,
                ["table"] = new JsonObject { ["wound_penalties"] = true }
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            Assert.Equal(PlayTools.FewestRuns, answer["runs"]!.GetValue<int>());
            Assert.Equal(500, answer["seeds"]!["first"]!.GetValue<int>());
            Assert.Equal(500 + PlayTools.FewestRuns - 1, answer["seeds"]!["last"]!.GetValue<int>());

            // The policy's own Name, not a string written here: a balance figure is a figure about a
            // particular way of playing, and IPolicy.Name is what that way is called.
            Assert.Equal(
                new AttackTheWeakest(_play).Name,
                answer["policy"]!["name"]!.GetValue<string>());

            var table = answer["table"]!.AsObject();

            foreach (var setting in PlayTools.TableSettings)
                Assert.True(table.ContainsKey(setting), $"the echoed table does not carry '{setting}'");

            Assert.True(table["wound_penalties"]!.GetValue<bool>());
            Assert.Contains("wound_penalties",
                table["on"]!.AsArray().Select(v => v!.GetValue<string>()), StringComparer.Ordinal);

            // The figures themselves: a rate per side, a defeat rate per combatant, and the means.
            var sides = answer["by_side"]!.AsArray()
                .ToDictionary(s => s!["side"]!.GetValue<string>(), s => s!, StringComparer.Ordinal);

            Assert.Equal(["heroes", "villains"], sides.Keys.Order(StringComparer.Ordinal));

            var total = sides.Values.Sum(s => s["win_rate"]!.GetValue<double>())
                        + answer["draw_rate"]!.GetValue<double>();

            Assert.Equal(1.0, total, 2);

            Assert.True(answer["mean_pages"]!.GetValue<double>() > 0);

            foreach (var combatant in answer["by_combatant"]!.AsArray())
            {
                var rate = combatant!["defeat_rate"]!.GetValue<double>();
                Assert.InRange(rate, 0.0, 1.0);
            }
        });

    /// <summary>
    /// A Hero and a Villain, built the shortest way that is still a legal shape for the strict
    /// reader — enough to open a fight for the tests that are about something else.
    /// </summary>
    private static JsonArray TwoSides() =>
    [
        new JsonObject
        {
            ["kind"] = "hero",
            ["id"] = "hero",
            ["side"] = "heroes",
            ["character"] = new JsonObject
            {
                ["Name"] = "the Hero",
                ["SelectedTierId"] = "standard",
                ["AbilityRanks"] = new JsonObject { ["might"] = 8, ["toughness"] = 5, ["willpower"] = 4 }
            }
        },
        new JsonObject
        {
            ["kind"] = "villain",
            ["id"] = "villain",
            ["side"] = "villains",
            ["character"] = new JsonObject
            {
                ["Name"] = "the Villain",
                ["SelectedTierId"] = "standard",
                ["AbilityRanks"] = new JsonObject { ["might"] = 8, ["toughness"] = 5, ["willpower"] = 4 }
            }
        }
    ];
}
