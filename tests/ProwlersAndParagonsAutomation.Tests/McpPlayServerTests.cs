using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
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
    /// A client and a server on either end of a pair of pipes, in this process — see
    /// <see cref="InProcessMcpServer"/>, which the character server's tests share, and whose
    /// comment says why the shutdown order there is the only one that ends cleanly.
    /// </summary>
    private Task WithClient(Func<McpClient, Task> body) => WithClient(Tools(), body);

    /// <inheritdoc cref="WithClient(Func{McpClient, Task})"/>
    /// <param name="tools">
    /// The tools the server is built over. Taken as an argument for the one test that needs a
    /// <c>midTurn</c> seam in them — every other test wants the plain ones.
    /// </param>
    /// <param name="body">What to drive over the client once it is connected.</param>
    private static Task WithClient(PlayTools tools, Func<McpClient, Task> body) =>
        InProcessMcpServer.Drive(PlayServer.Name, PlayServer.Options(tools), body);

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
    /// The five tools as a stranger's configuration spells them. <b>Literals, not
    /// <c>PlayServer</c>'s constants</b>: a constant compared with itself proves nothing about a
    /// contract somebody else has written down — the same reasoning <see cref="McpServerTests"/>
    /// records for the character server's six.
    /// </summary>
    private static readonly string[] WireNames =
        ["combat_guide", "run_encounters", "run_matrix", "start_encounter", "take_turn"];

    /// <summary>
    /// <b>The wire names are the contract.</b> A stranger configures a client against them and the
    /// assistant calls them by name; renaming a C# method must not rename a tool, and a tool
    /// dropped from the list is a method nobody can call.
    /// </summary>
    [Fact]
    public async Task TheFiveToolsAreServedUnderTheirWireNames() =>
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

    /// <summary>
    /// <b>Every argument name the play policy prints in a code span is an argument that tool really
    /// has, spelled the way the schema spells it.</b>
    ///
    /// <para>The document told every conversation this server has that <c>run_encounters</c> takes
    /// <c>max_pages</c>. The wire argument is <c>maxPages</c> — <c>max_pages</c> is what comes back
    /// in the <em>answer</em>, which is exactly why the mistake reads as correct. A model following
    /// the document sent an argument the schema does not have, the SDK dropped it, and the run took
    /// the default page limit while reporting a `max_pages` the caller never asked for. Nothing
    /// anywhere said so, which makes it the same "accepted and quietly ignored" the table settings
    /// are refused for.</para>
    ///
    /// <para><b>Against the schemas of the running server, not a list here.</b> The names come out
    /// of <c>tools/list</c> over the transport, so an argument renamed in C# renames the thing this
    /// is checked against, and the document is what has to move.</para>
    ///
    /// <para><b>How a code span is judged to be about an argument</b>: it is compared to the tool's
    /// own argument names with case and underscores removed, and a span that matches one that way
    /// has to match it exactly. That is narrow on purpose — <c>hero</c>, <c>threat_rank</c> and
    /// <c>attack_the_weakest</c> are in the same bullets and are not arguments of anything, and a
    /// rule that demanded every code span be an argument would be a rule about prose. It catches
    /// precisely the failure that shipped: the right argument, mis-spelled.</para>
    /// </summary>
    [Fact]
    public async Task EveryArgumentNameThePolicyPrintsIsSpelledTheWayTheSchemaSpellsIt() =>
        await WithClient(async client =>
        {
            var arguments = (await client.ListToolsAsync()).ToDictionary(
                tool => tool.Name,
                tool => tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)
                    ? properties.EnumerateObject().Select(p => p.Name).ToList()
                    : [],
                StringComparer.Ordinal);

            var bullets = CallBullets();

            // Two controls, because this whole check is a parse of prose and a parse that found
            // nothing would pass in silence. Every tool the server serves has to have been
            // described, and the parse has to have found code spans to judge.
            Assert.Equal(
                arguments.Keys.Order(StringComparer.Ordinal),
                bullets.Keys.Order(StringComparer.Ordinal));

            var judged = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (tool, spans) in bullets)
            {
                foreach (var span in spans)
                {
                    var match = arguments[tool].FirstOrDefault(name =>
                        string.Equals(Flatten(name), Flatten(span), StringComparison.OrdinalIgnoreCase));

                    if (match is null) continue;

                    judged.Add(match);

                    Assert.True(string.Equals(match, span, StringComparison.Ordinal),
                        $"mcp-play/PLAY-POLICY.md tells every conversation that {tool} takes "
                        + $"`{span}`. The schema's argument is `{match}`. An argument the schema "
                        + "does not have is dropped by the SDK and the call runs on the default, "
                        + "which is the quietest way this server can be wrong.");
                }
            }

            // <b>The control, and it names the arguments rather than counting them.</b> A parse that
            // had stopped finding code spans would pass every assertion above in silence, which is
            // how three of this repository's historical guards were wrong. These are the arguments
            // the document undertakes to name, and `maxPages` is the one the fault was in.
            foreach (var argument in ArgumentsThePolicyUndertakesToName)
            {
                Assert.True(judged.Contains(argument),
                    $"The play policy's \"The calls\" section no longer names `{argument}`. Either "
                    + "the document has stopped describing the call, or this parse has stopped "
                    + "reading it — and in both cases nothing is holding the spellings together.");
            }
        });

    /// <summary>
    /// The arguments the play policy's "The calls" section undertakes to name — the control for the
    /// check above, which would otherwise pass in silence on a parse that had stopped finding code
    /// spans at all. <c>maxPages</c> is the one the fault was in.
    /// </summary>
    private static readonly string[] ArgumentsThePolicyUndertakesToName =
    [
        "combatants", "table", "challengeLevel", "seed", "openingRange", "visibility",
        "encounterId", "intent", "runs", "policy", "maxPages"
    ];

    /// <summary>
    /// The <c>## The calls</c> section of the play policy, as tool name to the code spans in that
    /// tool's own bullet. Bounded to that section deliberately: elsewhere the document prints the
    /// fields of an <em>intent</em> and of an <em>answer</em>, which are not tool arguments and
    /// would be judged against the wrong list.
    /// </summary>
    private static Dictionary<string, List<string>> CallBullets()
    {
        var text = PlayPolicy.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

        var at = text.IndexOf("\n## The calls\n", StringComparison.Ordinal);

        Assert.True(at >= 0, "mcp-play/PLAY-POLICY.md no longer has a \"## The calls\" section.");

        var end = text.IndexOf("\n## ", at + 1, StringComparison.Ordinal);
        var section = end < 0 ? text[at..] : text[at..end];

        var bullets = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? current = null;

        foreach (var line in section.Split('\n'))
        {
            var opener = System.Text.RegularExpressions.Regex.Match(
                line, @"^- \*\*`([a-z_]+)`\*\*",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5));

            if (opener.Success)
            {
                current = opener.Groups[1].Value;
                bullets[current] = [];
                continue;
            }

            // A line that is not indented under a bullet has left the list.
            if (current is null) continue;
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) { current = null; continue; }

            foreach (var span in Spans(line)) bullets[current].Add(span);
        }

        // The opener line's own spans, minus the tool name itself, come back in too.
        foreach (var (tool, spans) in bullets)
        {
            var line = section.Split('\n').First(l =>
                l.StartsWith($"- **`{tool}`**", StringComparison.Ordinal));

            spans.AddRange(Spans(line).Where(s => !string.Equals(s, tool, StringComparison.Ordinal)));
        }

        return bullets;
    }

    private static IEnumerable<string> Spans(string line) =>
        System.Text.RegularExpressions.Regex
            .Matches(line, "`([^`]+)`",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value);

    /// <summary>A name with its case and its underscores taken off, so `max_pages` and `maxPages`
    /// are the same word said two ways — which is the whole of what this comparison is looking for.
    /// </summary>
    private static string Flatten(string name) =>
        name.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();

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
    /// <b>A tier these rules do not have is refused, not costed at nothing.</b>
    ///
    /// <para>The engine's <c>CalculateResolve</c> answers <b>0</b> for a tier it cannot resolve,
    /// which is the honest answer for a figure it cannot derive and a silent lie once that figure is
    /// a Hero in a fight: at 0 Resolve they buy no extra die, no reroll and no stabilise, and the
    /// answer says nothing about it. Both spellings of the fault are driven — a misspelling and an
    /// omission — because they arrive by different routes and produced the same quiet zero.</para>
    ///
    /// <para>The refusal has to name the tier that was asked for <em>and</em> the ones there are: a
    /// model that mistyped one has to be able to correct itself from the answer.</para>
    /// </summary>
    [Theory]
    [InlineData("standrad", "a misspelling")]
    [InlineData("nope", "a tier that was never in the book")]
    [InlineData("", "no tier at all")]
    public async Task ATierTheseRulesDoNotHaveIsRefused(string tier, string why) =>
        await WithClient(async client =>
        {
            var character = new JsonObject
            {
                ["Name"] = "the Hero",
                ["AbilityRanks"] = new JsonObject { ["might"] = 8, ["toughness"] = 5 }
            };

            if (tier.Length > 0) character["SelectedTierId"] = tier;

            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = new JsonArray(
                    new JsonObject { ["kind"] = "hero", ["side"] = "heroes", ["character"] = character },
                    new JsonObject
                    {
                        ["kind"] = "minions", ["id"] = "thugs", ["name"] = "the thugs",
                        ["threat_rank"] = 6, ["count"] = 4, ["side"] = "villains"
                    })
            });

            Assert.False(answer["ok"]!.GetValue<bool>(), $"{why} was accepted");
            Assert.Equal("NO_SUCH_TIER", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            if (tier.Length > 0) Assert.Contains(tier, message, StringComparison.Ordinal);

            // And every tier this repository has, so the correction is in the refusal.
            foreach (var known in _f.Rules.Tiers)
                Assert.Contains(known.Id, message, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>And <c>run_encounters</c> refuses it too</b>, which is the call whose whole product is a
    /// number somebody will quote. A measurement taken over a party built to no tier is the fault
    /// above multiplied by N.
    /// </summary>
    [Fact]
    public async Task RunEncountersRefusesATierTheseRulesDoNotHave() =>
        await WithClient(async client =>
        {
            var combatants = TwoSides();
            combatants[0]!["character"]!["SelectedTierId"] = "standrad";

            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = combatants,
                ["runs"] = PlayTools.FewestRuns
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_TIER", answer["problem"]!["code"]!.GetValue<string>());
        });

    /// <summary>
    /// <b>The tier a combatant was built to is on the opening ledger, cited.</b>
    ///
    /// <para>Refusing the unknown ones is only half of it: two identical sheets at two different
    /// tiers open a fight with different Resolve, and until this the answer carried no record of
    /// which was used. A reader forbidden to quote a number the ledger did not print is exactly the
    /// reader who needs the input to that number printed — so the line cites Ch.5 p.83, which is the
    /// entry that measures Resolve down from the Trait Cap.</para>
    /// </summary>
    [Fact]
    public async Task TheOpeningLedgerSaysWhichTierEachCharacterWasBuiltTo() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides()
            });

            Assert.True(opened["ok"]!.GetValue<bool>());

            var ledger = opened["ledger"]!.AsArray();

            var tierLines = ledger
                .Where(l => l!["text"]!.GetValue<string>()
                    .Contains("built to the standard tier", StringComparison.Ordinal))
                .ToList();

            // One per character combatant — a group of Minions has no sheet and so no tier.
            Assert.Equal(2, tierLines.Count);

            foreach (var line in tierLines)
            {
                Assert.Equal("starting_resolve", line!["rule"]!.GetValue<string>());
                Assert.Contains("p.83", line["source_ref"]!.GetValue<string>(), StringComparison.Ordinal);

                // The Trait Cap is the figure the tier actually buys, so it is in the sentence.
                Assert.Contains(
                    $"{_f.Rules.GetTier("standard")!.TraitCapRank}d",
                    line["text"]!.GetValue<string>(), StringComparison.Ordinal);
            }

            // Only the Hero holds Resolve, and the two lines have to be able to say so differently —
            // otherwise this passes on a line that says the same thing about everybody.
            Assert.Single(tierLines, l =>
                l!["text"]!.GetValue<string>().Contains("holds no Resolve", StringComparison.Ordinal));
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
    /// <b>An attack's <c>team</c> flag crosses the wire, and p.79's purchase is reachable through
    /// the server.</b>
    ///
    /// <para><b>This is the failure the document could not see.</b> <c>PLAY-POLICY.md</c> tells
    /// every conversation this server has to send <c>"team": true</c> on the attack; the intent
    /// reader had no such field, so the flag was dropped, the entry's <c>attack_bonus_dice</c> never
    /// reached the pool, and <c>spend_resolve</c> naming <c>team_attack</c> answered "was not a team
    /// attack" for ever. The policy's own spelling guard is scoped to <em>tool arguments</em> and
    /// the fields of an intent are not among them, which is why nothing disagreed.</para>
    ///
    /// <para><b>No die roll is asserted</b>, for the reason this whole class records: the pool is a
    /// figure the entry supplies, and whether the roll happened to show a six is not. So the
    /// purchase is required only to be answered by the rule that sells it and never by the refusal
    /// that says the attack was not one — which is exactly what a dropped flag produces and what a
    /// bad roll does not.</para>
    /// </summary>
    [Fact]
    public async Task AnAttacksTeamFlagCrossesTheWireAndTheSixesArePurchasable() =>
        await WithClient(async client =>
        {
            var bonus = _play.GetCombat("team_attacks").TeamAttack!.AttackBonusDice;

            // The control on the data: there is a bonus to look for, or the pool below says nothing.
            Assert.True(bonus > 0);

            var encounter = (await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 81
            }))["encounter_id"]!.GetValue<string>();

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack",
                    ["actor"] = "hero",
                    ["target"] = "villain",
                    ["trait_id"] = "might",
                    ["team"] = true
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            var added = turn["added"]!.AsArray();

            Assert.Contains(added, l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "team_attacks", StringComparison.Ordinal));

            // The Hero's 8d of Might plus the entry's own bonus: the flag arrived and was priced.
            var roll = added.Single(l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "attacks_and_defenses", StringComparison.Ordinal));

            Assert.Contains($"might {8 + bonus}d", roll!["text"]!.GetValue<string>(), StringComparison.Ordinal);

            var spend = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_resolve", ["actor"] = "hero", ["spend"] = "team_attack"
                }
            });

            var lines = spend["added"]!.AsArray();

            Assert.Contains(lines, l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "team_attacks", StringComparison.Ordinal));

            Assert.DoesNotContain(lines, l =>
                l!["text"]!.GetValue<string>()
                    .Contains("was not a team attack", StringComparison.Ordinal));
        });

    // ── p.75's three modifiers, over the wire ─────────────────────────────

    /// <summary>
    /// The <c>attacks_and_defenses</c> line of one turn's answer, which carries both pools.
    ///
    /// <para>Every fixture below reads its pool out of this rather than off a state field, because
    /// the pool is what a modifier moves and the ledger is where this server publishes it.</para>
    /// </summary>
    private static string RollLine(JsonNode turn)
    {
        // <b>By the sentence and not only by the rule id.</b> `attacks_and_defenses` is cited twice
        // on a turn that missed — once for the exchange and once for "the attack misses, or hits
        // with no effect" — so selecting on the id alone finds two lines and throws, which reads as
        // a fixture fault rather than as the two lines it is.
        var line = turn["added"]!.AsArray().SingleOrDefault(l =>
            string.Equals(l!["rule"]!.GetValue<string>(), "attacks_and_defenses", StringComparison.Ordinal)
            && l["text"]!.GetValue<string>().Contains("defends with", StringComparison.Ordinal));

        Assert.True(line is not null, "the turn resolved no attack: " + turn.ToJsonString());

        return line!["text"]!.GetValue<string>();
    }

    /// <summary>Whether a turn's answer cites one rule at all.</summary>
    private static bool Cites(JsonNode turn, string rule) =>
        turn["added"]!.AsArray().Any(l =>
            string.Equals(l!["rule"]!.GetValue<string>(), rule, StringComparison.Ordinal));

    /// <summary>A fight of two whose only defence is an Agility, so the defence chosen is active.</summary>
    private static JsonArray TwoSidesDodging()
    {
        var fight = TwoSides();

        foreach (var entry in fight)
        {
            entry!["character"]!["AbilityRanks"] =
                new JsonObject { ["might"] = 8, ["agility"] = 6 };
        }

        return fight;
    }

    /// <summary>
    /// <b>The scene's <c>visibility</c> crosses the wire, moves the pool, and comes back inside
    /// <c>table</c>.</b>
    ///
    /// <para>The placement is the half worth driving. The play policy tells every conversation that
    /// a rate is quoted with four things and one of them is <c>table</c>; a fight in the dark is a
    /// different game by up to three dice on every roll in it, so the figure has to travel with the
    /// thing a quoter is already told to carry. This asserts it on both tools.</para>
    ///
    /// <para>The control is the same fight in clear air, whose pool is the Hero's own rank — an
    /// argument the SDK had dropped would leave both answers identical, which is exactly the way
    /// the <c>team</c> flag was lost.</para>
    /// </summary>
    [Fact]
    public async Task TheScenesVisibilityCrossesTheWireAndComesBackInsideTheTable() =>
        await WithClient(async client =>
        {
            var band = _play.GetCombat("modifier_visibility").Visibility!.Bands
                .Single(b => string.Equals(b.Visibility, "poor", StringComparison.Ordinal))
                .Dice;

            Assert.NotEqual(0, band);

            async Task<JsonNode> Attack(string? light)
            {
                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = TwoSides(),
                    ["seed"] = 75,
                    ["visibility"] = light
                });

                Assert.Equal(light ?? "clear", opened["table"]!["visibility"]!.GetValue<string>());

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero",
                        ["target"] = "villain", ["trait_id"] = "might"
                    }
                });
            }

            // The control: in clear air the pool is the rank on the sheet and nothing cites p.75.
            var clear = await Attack(null);

            Assert.Contains("might 8d", RollLine(clear), StringComparison.Ordinal);
            Assert.False(Cites(clear, "modifier_visibility"));

            var dim = await Attack("poor");

            Assert.Contains($"might {8 + band}d", RollLine(dim), StringComparison.Ordinal);
            Assert.True(Cites(dim, "modifier_visibility"));

            // And a measurement carries it, in the same object as the switches.
            var measured = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["visibility"] = "none"
            });

            Assert.True(measured["ok"]!.GetValue<bool>());
            Assert.Equal("none", measured["table"]!["visibility"]!.GetValue<string>());
        });

    /// <summary>
    /// <b>A combatant's <c>size</c> crosses the wire and moves the defender's active defence.</b>
    ///
    /// <para>Both halves are asserted: the figure comes back on the public state, so a caller can
    /// see what was read, and the defence pool moves by the band p.75 prints — because a field
    /// echoed and not applied is the "accepted and quietly ignored" this server refuses a table
    /// setting for.</para>
    /// </summary>
    [Fact]
    public async Task ACombatantsSizeCrossesTheWireAndMovesTheDefendersActiveDefence() =>
        await WithClient(async client =>
        {
            var band = _play.GetCombat("modifier_size").Size!.Bands
                .Single(b => string.Equals(
                    b.AttackerRelativeSize, "at least 5 times your size", StringComparison.Ordinal))
                .Dice;

            Assert.NotEqual(0, band);

            async Task<JsonNode> Attack(JsonArray fight)
            {
                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["seed"] = 75
                });

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero",
                        ["target"] = "villain", ["trait_id"] = "might"
                    }
                });
            }

            // The control: the same size on both, which is the default and no band at all.
            var even = await Attack(TwoSidesDodging());

            Assert.Contains("agility 6d", RollLine(even), StringComparison.Ordinal);
            Assert.False(Cites(even, "modifier_size"));

            var giant = TwoSidesDodging();
            giant[0]!["size"] = 5;

            var stomped = await Attack(giant);

            Assert.Contains($"agility {6 + band}d", RollLine(stomped), StringComparison.Ordinal);
            Assert.True(Cites(stomped, "modifier_size"));

            // The figure a caller passed comes back, so they can see what was read.
            var hero = stomped["state"]!["combatants"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "hero", StringComparison.Ordinal));

            Assert.Equal(5d, hero!["size"]!.GetValue<double>());
        });

    /// <summary>
    /// <b>A combatant's <c>invisible</c> flag crosses the wire and costs whoever faces them.</b>
    ///
    /// <para>p.75 makes an invisible opponent equivalent to no visibility, so an attack on one in
    /// clear air still loses the worst band there is. Driven in clear air deliberately: the scene's
    /// own light is the other field, and a fixture that set both could not tell which one moved the
    /// pool.</para>
    /// </summary>
    [Fact]
    public async Task ACombatantsInvisibleFlagCrossesTheWireAndCostsWhoeverFacesThem() =>
        await WithClient(async client =>
        {
            var band = _play.GetCombat("modifier_visibility").Visibility!.Bands
                .Single(b => string.Equals(b.Visibility, "none", StringComparison.Ordinal))
                .Dice;

            var fight = TwoSides();
            fight[1]!["invisible"] = true;

            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = fight,
                ["seed"] = 75
            });

            // The control: the scene itself is clear, so anything below is the flag's doing.
            Assert.Equal("clear", opened["table"]!["visibility"]!.GetValue<string>());

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero",
                    ["target"] = "villain", ["trait_id"] = "might"
                }
            });

            Assert.Contains($"might {8 + band}d", RollLine(turn), StringComparison.Ordinal);
            Assert.True(Cites(turn, "modifier_visibility"));

            var villain = turn["state"]!["combatants"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "villain", StringComparison.Ordinal));

            Assert.True(villain!["invisible"]!.GetValue<bool>());
        });

    /// <summary>
    /// <b>An attack's <c>cover</c> crosses the wire and costs the band p.75 prints.</b>
    ///
    /// <para>Driven through <c>take_turn</c> rather than asserted about the reader, because the
    /// field of an intent is exactly what the policy's spelling guard cannot see — the guard is
    /// scoped to tool arguments, and this is how the <c>team</c> flag was dropped in silence.</para>
    /// </summary>
    [Fact]
    public async Task AnAttacksCoverBandCrossesTheWireAndCostsTheDiceItPrints() =>
        await WithClient(async client =>
        {
            var band = _play.GetCombat("modifier_cover").Cover!.Bands
                .Single(b => string.Equals(b.Cover, "heavy", StringComparison.Ordinal))
                .Dice;

            Assert.NotEqual(0, band);

            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 75
            });

            var id = opened["encounter_id"]!.GetValue<string>();

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                    ["trait_id"] = "might", ["cover"] = "heavy"
                }
            });

            Assert.Contains($"might {8 + band}d", RollLine(turn), StringComparison.Ordinal);

            var line = turn["added"]!.AsArray().Single(l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "modifier_cover", StringComparison.Ordinal));

            Assert.Equal(
                _play.GetCombat("modifier_cover").SourceRef,
                line!["source_ref"]!.GetValue<string>());

            Assert.Contains("heavy", line["text"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>An attack's <c>cover_structure</c> crosses the wire, and it is what turns a target who
    /// cannot be hit into one who can.</b>
    ///
    /// <para>Complete cover is refused with nothing rolled; the same attack with the obstacle's
    /// Structure named goes through, and the defence roll that answers it is the Structure's rather
    /// than the Villain's own Toughness. That last assertion is the one that matters: a field read
    /// and echoed onto a ledger line without moving a pool would be a clause this server advertised
    /// and did not apply.</para>
    /// </summary>
    [Fact]
    public async Task AnAttacksCoverStructureCrossesTheWireAndAnswersTheAttack() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 75
            });

            var id = opened["encounter_id"]!.GetValue<string>();

            async Task<JsonNode> Through(int? structure) =>
                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                        ["trait_id"] = "might", ["cover"] = "complete",
                        ["cover_structure"] = structure
                    }
                });

            // Hidden altogether, with no obstacle named: refused, and no attack was resolved.
            var hidden = await Through(null);

            Assert.True(hidden["ok"]!.GetValue<bool>());
            Assert.True(Cites(hidden, "modifier_cover"));
            Assert.DoesNotContain(hidden["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("defends with", StringComparison.Ordinal));

            // A Structure of 7, under the Hero's 8d Might: the attack goes through, and the
            // obstacle's own 7d answers it. Seven is chosen because no other figure in this fight
            // is one — the Villain's Toughness of 5 is halved to 3 against a lethal attack — so
            // "7d" in the defence half of the line can only be the Structure.
            var through = await Through(7);

            Assert.Contains("the cover's Structure 7d", RollLine(through), StringComparison.Ordinal);

            // And a Structure the attack cannot get through is refused with nothing rolled.
            var stopped = await Through(8);

            Assert.True(Cites(stopped, "modifier_cover"));
            Assert.DoesNotContain(stopped["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("defends with", StringComparison.Ordinal));
        });

    /// <summary>
    /// <b>A lure's <c>target</c> crosses the wire, and one naming nobody in the fight is an answer
    /// rather than a dropped connection.</b>
    ///
    /// <para>p.79's luring is the one purchase that points at somebody, so <c>spend_resolve</c> and
    /// <c>spend_adversity</c> both carry a <c>target</c>. A name the fight does not hold reaches the
    /// engine's indexer, which throws — and the whole contract of this server is that a caller's
    /// mistake comes back as something a model can act on. Either refusal is correct here: the
    /// engine may reject the purchase on the ledger before it ever looks the name up. What must
    /// never happen is the protocol error, and the ids have to be named either way.</para>
    /// </summary>
    [Fact]
    public async Task ALureNamingNobodyInTheFightIsAnAnswerAndNotAProtocolError() =>
        await WithClient(async client =>
        {
            var encounter = (await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 81
            }))["encounter_id"]!.GetValue<string>();

            await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                    ["trait_id"] = "might"
                }
            });

            // Call, not CallToolAsync: a protocol error is what this asserts against, and Call is
            // where that becomes a failure rather than an exception nobody reads.
            var answer = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_resolve", ["actor"] = "hero",
                    ["spend"] = "luring", ["target"] = "nobody_in_this_fight"
                }
            });

            if (!answer["ok"]!.GetValue<bool>())
            {
                Assert.Equal("INTENT_REFUSED", answer["problem"]!["code"]!.GetValue<string>());

                Assert.Contains("nobody_in_this_fight",
                    answer["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);

                return;
            }

            // The other legal answer: refused on the ledger, by the rule that sells the purchase,
            // with nothing spent.
            Assert.Contains(answer["added"]!.AsArray(), l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "luring", StringComparison.Ordinal));
        });

    /// <summary>
    /// <b>A spend's <c>narration</c> crosses the wire, and what it bought comes back on the public
    /// state.</b>
    ///
    /// <para>p.85's three own purchases each take the GM's own words, because the mechanical half of
    /// every one of them is a point leaving the pool and the rest is the fiction. A reader that had
    /// no such field would drop it, and the spend would be refused for want of a thing the caller
    /// had sent — which is the shape the team flag failed in, and the reason
    /// <c>PLAY-POLICY.md</c>'s spelling guard cannot catch it: that guard is scoped to <em>tool
    /// arguments</em>, and the fields of an intent are not among them.</para>
    ///
    /// <para>So the words are required to come back on the ledger line, which says the field
    /// arrived, and <c>flaw_suppressed</c> is required to come back on the combatant, which is the
    /// half a ledger line cannot show: this purchase leaves state behind, and a client deciding
    /// whether to buy a second one reads it there.</para>
    /// </summary>
    [Fact]
    public async Task ASuppressedFlawsNarrationCrossesTheWireAndComesBackOnTheState() =>
        await WithClient(async client =>
        {
            var encounter = (await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 81
            }))["encounter_id"]!.GetValue<string>();

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "suppress_flaw",
                    ["narration"] = "a hot temper"
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            var added = turn["added"]!.AsArray();

            Assert.Contains(added, l => string.Equals(
                l!["rule"]!.GetValue<string>(), "adversity_spend_suppress_flaw", StringComparison.Ordinal));

            // The field arrived: without it the spend is refused for naming no Flaw at all.
            Assert.Contains(added, l =>
                l!["text"]!.GetValue<string>().Contains("a hot temper", StringComparison.Ordinal));

            var villain = turn["state"]!["combatants"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "villain", StringComparison.Ordinal));

            Assert.Equal("a hot temper", villain!["flaw_suppressed"]?.GetValue<string>());
        });

    /// <summary>
    /// <b>A misfortune crosses the wire, and what comes back is the purchase and the GM's words and
    /// nothing else.</b>
    ///
    /// <para>p.85 gives a misfortune no roll, no threshold and no duration, so this is the one spend
    /// whose whole answer is a pool that fell and a sentence. Both halves are required here: the
    /// pool on the public state has to have moved, or the call did nothing; and the words have to be
    /// on the line, or the <c>narration</c> field was dropped and the spend was refused for naming
    /// nothing — which is what a client would see as "the server ignored me".</para>
    ///
    /// <para>It needs no <c>actor</c>, which is asserted by sending none: p.85 throws a misfortune
    /// at the Heroes as a side, not on behalf of a character.</para>
    /// </summary>
    [Fact]
    public async Task AMisfortunesNarrationCrossesTheWireAndCostsThePool() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();
            var before = opened["adversity"]!.GetValue<int>();

            // The control: there is a pool for the spend to come out of.
            Assert.True(before > 0, $"the fight opened on {before} Adversity");

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["spend"] = "misfortune",
                    ["narration"] = "the fire escape gives way under them"
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            Assert.Contains(turn["added"]!.AsArray(), l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "adversity_spend_misfortune", StringComparison.Ordinal)
                && l["text"]!.GetValue<string>()
                    .Contains("the fire escape gives way under them", StringComparison.Ordinal));

            Assert.Equal(before - 1, turn["state"]!["adversity"]!.GetValue<int>());
        });

    /// <summary>
    /// <b>An act of villainy crosses the wire, and the story's one act comes back on the public
    /// state.</b>
    ///
    /// <para>The narration is what the point buys — p.85's act is "anything necessary to advance
    /// the story" and this server has no story — so a dropped field would leave the spend refused
    /// for naming nothing. The <c>villainy</c> list is the other half: it is what refuses the
    /// second purchase, and a client that cannot see it has no way to know the story's one act is
    /// gone until it asks for another and is told.</para>
    ///
    /// <para>The second purchase is driven here too, because "once per story" is the only limit on
    /// this spend and a limit nobody drives over the wire is a limit that has only been read.</para>
    /// </summary>
    [Fact]
    public async Task AnActOfVillainyCrossesTheWireAndTheStorysOneActComesBackOnTheState() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["challengeLevel"] = 3,
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();
            var before = opened["adversity"]!.GetValue<int>();

            // The control: enough in the pool for two, so the second refusal below is the limit's.
            Assert.True(before >= 2, $"the fight opened on {before} Adversity");

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "villainy",
                    ["narration"] = "throws the switch and floods the lower deck"
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            Assert.Contains(turn["added"]!.AsArray(), l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "adversity_spend_villainy", StringComparison.Ordinal)
                && l["text"]!.GetValue<string>()
                    .Contains("throws the switch and floods the lower deck", StringComparison.Ordinal));

            Assert.Equal(before - 1, turn["state"]!["adversity"]!.GetValue<int>());

            Assert.Equal(
                ["villain"],
                turn["state"]!["villainy"]!.AsArray().Select(v => v!.GetValue<string>()));

            // And the story's one act is gone: the second is refused, with nothing spent.
            var again = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "villainy",
                    ["narration"] = "grabs a hostage"
                }
            });

            Assert.Contains(again["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>()
                    .Contains("act of villainy per story", StringComparison.Ordinal));

            Assert.Equal(before - 1, again["state"]!["adversity"]!.GetValue<int>());
        });

    /// <summary>
    /// <b>What the wire does with a <c>narration</c> that says nothing, one that says a great deal,
    /// and one sent where the page has nothing for it to buy.</b>
    ///
    /// <para>The three answers are different and none of them was pinned. An <b>empty</b> one is a
    /// spend that does not say what it bought and is refused on the ledger with nothing spent — the
    /// same answer a missing field gets, which is the honest one, because a caller who sent
    /// <c>""</c> has said exactly as much as a caller who sent nothing. A <b>long</b> one is carried
    /// whole: the GM's sentence is the record and truncating it would leave a line that reads as
    /// though they had stopped mid-thought, so there is no cap and this says so rather than leaving
    /// the first person to send a paragraph to find out. And one on a <b>Resolve</b> spend is
    /// <em>ignored</em>, which is what every other unknown field on every other intent gets — but a
    /// field that is silently dropped is the failure the team flag was, so it is pinned here and
    /// said in <c>PLAY-POLICY.md</c> rather than left for a model to assume its words were
    /// recorded.</para>
    /// </summary>
    [Fact]
    public async Task ANarrationIsRefusedWhenEmptyCarriedWholeWhenLongAndIgnoredOnAResolveSpend() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["challengeLevel"] = 3,
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();
            var before = opened["adversity"]!.GetValue<int>();

            // The control: there is a pool, so a refusal below is the narration's.
            Assert.True(before >= 2, $"the fight opened on {before} Adversity");

            var empty = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["spend"] = "misfortune",
                    ["narration"] = ""
                }
            });

            // A refusal is an answer and not a transport error, and it spends nothing.
            Assert.True(empty["ok"]!.GetValue<bool>());
            Assert.Equal(before, empty["state"]!["adversity"]!.GetValue<int>());
            Assert.Contains(empty["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("does not say what it is", StringComparison.Ordinal));

            // A long one is carried whole, not capped and not truncated.
            var long_ = string.Join(" ", Enumerable.Repeat("the scaffolding shifts under them", 200));

            var wordy = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["spend"] = "misfortune",
                    ["narration"] = long_
                }
            });

            Assert.True(wordy["ok"]!.GetValue<bool>());
            Assert.Equal(before - 1, wordy["state"]!["adversity"]!.GetValue<int>());
            Assert.Contains(wordy["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains(long_, StringComparison.Ordinal));

            // And one sent on a Resolve purchase is ignored: the purchase happens, and the words
            // appear nowhere in the answer — not on a line, and not on the state.
            const string Unheard = "he grits his teeth and goes first";

            var seized = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_resolve",
                    ["actor"] = "hero",
                    ["spend"] = "seize_initiative",
                    ["narration"] = Unheard
                }
            });

            Assert.True(seized["ok"]!.GetValue<bool>());

            // The control: the purchase itself went through, so "the words are absent" is not
            // "nothing happened".
            Assert.Contains(seized["state"]!["seized"]!.AsArray(), s =>
                string.Equals(s!.GetValue<string>(), "hero", StringComparison.Ordinal));

            Assert.DoesNotContain(Unheard, seized.ToJsonString(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>A range class is named, never numbered.</b>
    ///
    /// <para><c>Enum.TryParse</c> accepts the numeral of a member — and for a plain enum it accepts
    /// <em>any</em> numeral, defined or not. So <c>"1"</c> opened the fight at Distant, which is a
    /// band nobody named, and <c>"99"</c> opened it at a <c>RangeBand</c> that does not exist: the
    /// echo printed <c>99</c> back and every range comparison downstream ran against an undefined
    /// value. Both are refused, because p.73 prints three range classes and none is spelled with a
    /// digit.</para>
    /// </summary>
    [Theory]
    [InlineData("1", "the numeral of a real band")]
    [InlineData("99", "a numeral of no band at all")]
    [InlineData("point blank", "a class the book does not print")]
    public async Task ARangeClassIsNamedRatherThanNumbered(string wanted, string why) =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["openingRange"] = wanted
            });

            Assert.False(answer["ok"]!.GetValue<bool>(), $"{why} was accepted");
            Assert.Equal("NO_SUCH_RANGE", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.Contains(wanted, message, StringComparison.Ordinal);

            foreach (var band in Enum.GetNames<RangeBand>())
                Assert.Contains(PlayTools.Wire(band), message, StringComparison.Ordinal);
        });

    /// <summary>
    /// The control for the theory above: the three names still open a fight, and the opening band
    /// comes back as the one that was asked for. A reader that refused everything would satisfy all
    /// three cases.
    /// </summary>
    [Theory]
    [InlineData("close")]
    [InlineData("Distant")]
    [InlineData("  extreme ")]
    public async Task TheThreeRangeClassesAreStillAccepted(string wanted) =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["openingRange"] = wanted
            });

            Assert.True(answer["ok"]!.GetValue<bool>());
            Assert.Equal(wanted.Trim().ToLowerInvariant(), answer["opening_range"]!.GetValue<string>());
        });

    /// <summary>
    /// <b>How far apart two combatants are is a pair on the wire, and no answer carries a NUL.</b>
    ///
    /// <para><c>EncounterState.PairKey</c> joins two ids with a literal <c>\0</c> — the right choice
    /// inside the engine, since it is the one character an id cannot contain, and a catastrophe as a
    /// JSON member name. The state came back with a key spelling <c>robot\0soldier</c>: a client
    /// that split it on a space read one combatant named "robot soldier", and one that echoed it
    /// into a log or a terminal saw it truncated at the NUL. Neither is a failure anybody would go
    /// looking for.</para>
    ///
    /// <para><b>The byte is looked for in both spellings, and that is not fussiness.</b>
    /// <c>System.Text.Json</c> writes a NUL as the escape <c>\u0000</c>, so a search of the answer
    /// for the character <c>'\0'</c> passes against the defect it was written for — the defect ships
    /// the byte and the serialiser hides it. What a client gets back after parsing is the real byte
    /// either way, which is why both spellings are refused here.</para>
    ///
    /// <para><b>Both halves are asserted, and the second is the control.</b> "No NUL anywhere" is
    /// satisfied completely by an answer that stopped carrying ranges at all, which is how three of
    /// this repository's historical guards were wrong — so the pair has to be there, naming both
    /// combatants, before the absence of the byte means anything.</para>
    /// </summary>
    [Fact]
    public async Task RangesComeBackAsPairsAndNoAnswerCarriesANul() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["openingRange"] = "distant"
            });

            var turn = await client.CallToolAsync("take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                ["intent"] = new JsonObject { ["kind"] = "hold", ["actor"] = "hero" }
            });

            var raw = Text(turn);

            Assert.DoesNotContain('\0', raw);
            Assert.DoesNotContain("\\u0000", raw, StringComparison.OrdinalIgnoreCase);

            var ranges = JsonNode.Parse(raw)!["state"]!["ranges"]!.AsArray();

            var pair = Assert.Single(ranges);

            // The two ids, in their own fields — not spliced into one string a caller has to
            // take apart, and not the engine's private spelling of a dictionary key.
            Assert.Equal(["hero", "villain"],
                new[] { pair!["a"]!.GetValue<string>(), pair["b"]!.GetValue<string>() }
                    .Order(StringComparer.Ordinal));

            // And the band the fight actually opened in, so this cannot pass on a constant.
            Assert.Equal("distant", pair["band"]!.GetValue<string>());
        });

    /// <summary>
    /// <b>A grab's <c>item</c> crosses the wire, in both directions of p.76's own distinction.</b>
    ///
    /// <para>A field the reader does not have is a field the SDK drops in silence — which is exactly
    /// how p.79's team flag was lost, with the policy telling every conversation to send it and
    /// nothing here reading it. So both halves are driven: a grab that names nothing is refused with
    /// nothing rolled, and the same grab naming an object is rolled and read off the Grappling
    /// table.</para>
    ///
    /// <para>The second half is the control the first cannot supply. A reader that dropped
    /// <c>item</c> on the floor would refuse <em>every</em> grab, and a test asserting only the
    /// refusal would call that green.</para>
    /// </summary>
    [Fact]
    public async Task AGrabsItemCrossesTheWire() =>
        await WithClient(async client =>
        {
            // The sword goes to the Villain, so the grab below is aimed at an opponent who has one
            // — p.76's own premise, and the wire's "holding" field.
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSidesArmed("villain"),
                ["seed"] = 11
            });

            var id = opened["encounter_id"]!.GetValue<string>();
            var actor = opened["turn_order"]!.AsArray()[0]!["id"]!.GetValue<string>();
            var other = opened["turn_order"]!.AsArray()[1]!["id"]!.GetValue<string>();

            var bare = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "grapple", ["actor"] = actor, ["target"] = other, ["move"] = "grab"
                }
            });

            Assert.True(bare["ok"]!.GetValue<bool>());
            Assert.Contains("has not said what", bare["added"]!.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("grappling_table", bare["added"]!.ToJsonString(), StringComparison.Ordinal);

            // The control on the setup, read off a state nothing has changed yet: the "holding"
            // field arrived and the engine kept it. Without it the named grab below is refused for
            // want of an object in the target's hands rather than resolved, and the assertion that
            // it rolls would be about the wrong refusal.
            Assert.Equal("the sword", bare["state"]!["combatants"]!.AsArray()
                .Single(c => c!["id"]!.GetValue<string>() == "villain")!["holding"]!["item"]!
                .GetValue<string>());

            var named = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "grapple", ["actor"] = "hero", ["target"] = "villain",
                    ["move"] = "grab", ["item"] = "the sword"
                }
            });

            Assert.Contains("grappling_table", named["added"]!.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("has not said what", named["added"]!.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("is not holding", named["added"]!.ToJsonString(), StringComparison.Ordinal);

            // And the other half of p.76's premise, over the wire: a grab for something nobody is
            // recorded as holding is refused with nothing rolled, so the winner is never handed an
            // object that came from nowhere.
            var absent = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "grapple", ["actor"] = actor, ["target"] = other,
                    ["move"] = "grab", ["item"] = "a rocket launcher"
                }
            });

            Assert.Contains("is not holding a rocket launcher",
                absent["added"]!.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain("grappling_table", absent["added"]!.ToJsonString(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>An attack's <c>item</c> crosses the wire, and an item nobody holds is refused with nothing
    /// rolled.</b>
    ///
    /// <para>A full grab is the only way anything reaches a combatant's hands here, so an attack
    /// naming a weapon out of the air is a claim about equipment this engine cannot answer for — and
    /// a reader that dropped the field would resolve it as an ordinary attack and put a fabricated
    /// weapon on the ledger. The refusal is the control that the field arrived.</para>
    /// </summary>
    [Fact]
    public async Task AnAttacksItemCrossesTheWire() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 11
            });

            var id = opened["encounter_id"]!.GetValue<string>();
            var actor = opened["turn_order"]!.AsArray()[0]!["id"]!.GetValue<string>();
            var other = opened["turn_order"]!.AsArray()[1]!["id"]!.GetValue<string>();

            var swung = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = actor, ["target"] = other,
                    ["trait_id"] = "might", ["item"] = "a rocket launcher"
                }
            });

            var ledger = swung["added"]!.ToJsonString();

            Assert.Contains("is not holding a rocket launcher", ledger, StringComparison.Ordinal);
            Assert.DoesNotContain("defends with", ledger, StringComparison.Ordinal);

            // The control: the same attack without the field is resolved, so the refusal is about
            // the item and not about attacks being switched off.
            var plain = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = actor, ["target"] = other, ["trait_id"] = "might"
                }
            });

            Assert.Contains("defends with", plain["added"]!.ToJsonString(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b><c>toss</c> is an intent this server takes.</b>
    ///
    /// <para>The two answers it must not give are the ones this asserts against: <c>NO_SUCH_INTENT</c>
    /// would mean the kind never reached the reader, and a resolved toss would mean an item nobody
    /// holds was thrown away. What it gives instead is the ledger refusal p.76's own entry makes,
    /// which is what a kind the server has and a fight not in a state for looks like.</para>
    /// </summary>
    [Fact]
    public async Task TossIsAnIntentThisServerTakes() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 11
            });

            var answer = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                ["intent"] = new JsonObject
                {
                    ["kind"] = "toss",
                    ["actor"] = opened["turn_order"]!.AsArray()[0]!["id"]!.GetValue<string>(),
                    ["item"] = "the sword"
                }
            });

            Assert.True(answer["ok"]!.GetValue<bool>());
            Assert.Contains("toss", PlayTools.IntentKinds, StringComparer.Ordinal);
            Assert.Contains("they are recorded as holding nothing",
                answer["added"]!.ToJsonString(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Who holds what comes back on the public state, and so does what a pair is fighting
    /// over.</b>
    ///
    /// <para>A client reads the state to decide what to do next, and p.76 gives a winner one page to
    /// use or toss the object — so a fight that had taken a sword off somebody and did not say so
    /// would leave a model narrating from a fact it could not see. <c>holding</c> carries the item,
    /// the page it was won on and whether it has been swung, which is what decides whether the page
    /// turn takes it away.</para>
    ///
    /// <para>The control is that the same field reads null for a combatant who has taken nothing, so
    /// this cannot pass on a key that is always there and always says the same thing.</para>
    /// </summary>
    [Fact]
    public async Task WhoHoldsWhatComesBackOnTheState() =>
        await WithClient(async client =>
        {
            // Whoever acts second walks in with the sword, so the character who acts first can spend
            // their turn grabbing it — p.76 aims a grab at an opponent who has one, and nothing but
            // this field can say that anybody has.
            var order = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 11
            });

            var actor = order["turn_order"]!.AsArray()[0]!["id"]!.GetValue<string>();
            var other = order["turn_order"]!.AsArray()[1]!["id"]!.GetValue<string>();

            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSidesArmed(other),
                ["seed"] = 11
            });

            var id = opened["encounter_id"]!.GetValue<string>();

            // The control on the opening hand, read off a refusal so that nothing has happened yet:
            // the item is on the state before a die is thrown, and it reports no page, because
            // nobody won it.
            var before = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "toss", ["actor"] = actor, ["item"] = "the sword"
                }
            });

            Assert.Contains("they are recorded as holding nothing",
                before["added"]!.ToJsonString(), StringComparison.Ordinal);

            var carried = before["state"]!["combatants"]!.AsArray()
                .Single(c => c!["id"]!.GetValue<string>() == other)!["holding"]!;

            Assert.Equal("the sword", carried["item"]!.GetValue<string>());
            Assert.True(carried["carried_in"]!.GetValue<bool>());
            Assert.Null(carried["won_on_page"]);

            JsonNode? state = null;

            // A full grab needs three net successes, which no seed promises on one roll — so the
            // fight is played until one lands, and the loop's bound plus the assertion after it are
            // what say it did rather than that it was hoped for.
            for (var page = 0; page < 12 && state is null; page++)
            {
                var turn = await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "grapple", ["actor"] = actor, ["target"] = other,
                        ["move"] = "grab", ["item"] = "the sword"
                    }
                });

                if (turn["state"]!["grapples"]!.AsArray()
                        .Any(g => g!["kind"]!.GetValue<string>() == "full"))
                {
                    state = turn["state"];
                }

                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject { ["kind"] = "end_turn", ["actor"] = actor }
                });

                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject { ["kind"] = "end_turn", ["actor"] = other }
                });

                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject { ["kind"] = "end_page", ["actor"] = actor }
                });
            }

            Assert.NotNull(state);

            var grapple = Assert.Single(state["grapples"]!.AsArray());

            Assert.Equal("grab", grapple!["move"]!.GetValue<string>());
            Assert.Equal("the sword", grapple["item"]?.GetValue<string>());

            var combatants = state["combatants"]!.AsArray()
                .ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!["holding"]);

            // The loser has lost it, which is the state change a full grab is — and the control that
            // says this key is not simply always filled in.
            Assert.Null(combatants[other]);

            Assert.Equal("full", grapple["kind"]!.GetValue<string>());

            // And it is no longer the item they carried in: the winner's copy is stamped with the
            // page the grab landed on, which is what the page turn measures.
            Assert.Equal(false, combatants[actor]?["carried_in"]?.GetValue<bool>());
            Assert.Equal("the sword", combatants[actor]?["item"]?.GetValue<string>());
            Assert.Equal(false, combatants[actor]?["used"]?.GetValue<bool>());
            Assert.True(combatants[actor]?["won_on_page"]?.GetValue<int>() >= 1,
                "the page a full grab was won on is what decides whether the page turn takes the "
                + "item away, so it has to be on the wire beside the item itself");
        });

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

    /// <summary>
    /// <b>A switch this engine has, set to something that is not a boolean, is refused rather than
    /// read as off.</b>
    ///
    /// <para>The four spellings a client actually sends: the string <c>"true"</c>, the number
    /// <c>1</c>, the word <c>"yes"</c>, and <c>null</c>. Every one of them failed
    /// <c>TryGetValue&lt;bool&gt;</c> and fell through to <c>false</c> — so a table that plainly
    /// meant to turn Wound Penalties on measured a game without them, the echo said <c>false</c>,
    /// and nothing said the value had been thrown away. It is the unknown-key refusal's own
    /// reasoning one layer in: there, the key was not known; here, the value was not.</para>
    ///
    /// <para>The refusal names both the key and the value, because "the table is wrong" sends a
    /// caller reading their whole object.</para>
    /// </summary>
    [Theory]
    [InlineData("\"true\"", "the boolean as a string")]
    [InlineData("1", "the boolean as a number")]
    [InlineData("\"yes\"", "a word that is not JSON's")]
    [InlineData("null", "a null where a boolean belongs")]
    public async Task AKnownSwitchWithANonBooleanValueIsRefused(string json, string why) =>
        await WithClient(async client =>
        {
            var table = new JsonObject { ["wound_penalties"] = JsonNode.Parse(json) };

            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = table
            });

            Assert.False(answer["ok"]!.GetValue<bool>(), $"{why} was accepted");
            Assert.Equal("BAD_TABLE", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.Contains("wound_penalties", message, StringComparison.Ordinal);
            Assert.Contains(json, message, StringComparison.Ordinal);
        });

    /// <summary>
    /// And the control for the theory above: the same key with a real boolean is accepted and
    /// reaches the echo as on. Without this, a refusal of <em>every</em> table would pass all four
    /// cases while making the tool useless.
    /// </summary>
    [Fact]
    public async Task ASwitchWithARealBooleanIsStillAccepted() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["wound_penalties"] = true, ["gear_limit_rank"] = 8 }
            });

            Assert.True(answer["ok"]!.GetValue<bool>());
            Assert.True(answer["table"]!["wound_penalties"]!.GetValue<bool>());
            Assert.Equal(8, answer["table"]!["gear_limit_rank"]!.GetValue<int>());
        });

    /// <summary>
    /// <c>gear_limit_rank</c> is the one setting that is a number rather than a switch, so a boolean
    /// there is the same fault the other way round — and a check that only ever demanded booleans
    /// would have broken it.
    /// </summary>
    [Fact]
    public async Task TheGearLimitRankRefusesABooleanWhereARankBelongs() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["gear_limit_rank"] = true }
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("BAD_TABLE", answer["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("whole number of ranks",
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
            // particular way of playing, and IPolicy.Name is what that way is called. The default is
            // the standard style going after whoever is nearly down.
            Assert.Equal(
                StylePolicy.For(PlayStyle.Standard, Targeting.Weakest, _play).Name,
                answer["policy"]!["name"]!.GetValue<string>());

            // <b>And the two guesses inside it come back apart</b>, each with the sentence a reader
            // is meant to argue with. A style quoted without its note is a name.
            Assert.Equal("standard", answer["style"]!["id"]!.GetValue<string>());
            Assert.Equal("weakest", answer["targeting"]!["id"]!.GetValue<string>());

            Assert.False(string.IsNullOrWhiteSpace(answer["style"]!["note"]!.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(answer["targeting"]!["note"]!.GetValue<string>()));

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
    /// <b>A measurement is reproducible from its own echo, and two of p.75's three modifiers are
    /// not the scene's.</b>
    ///
    /// <para>The scene's light rides back inside <c>table</c> so that a rate quoted with the four
    /// things carries it. <c>size</c> and <c>invisible</c> cannot ride there: they are facts about a
    /// character, and <c>table</c> is a fact about the fight. They were echoed on
    /// <c>start_encounter</c>'s turn order and on the held public state and <b>nowhere in a
    /// <c>run_encounters</c> answer at all</b> — so a run against a five-times-sized attacker came
    /// back looking exactly like a run in which everybody was the same size, while every active
    /// defence in it had moved by two dice.</para>
    ///
    /// <para>Driven, and with the control that the values are the ones this call sent rather than
    /// the defaults: the same call without them is asserted to echo the defaults, so a server that
    /// hard-coded either figure cannot satisfy both.</para>
    /// </summary>
    [Fact]
    public async Task RunEncountersEchoesEachCombatantsSizeAndInvisibility() =>
        await WithClient(async client =>
        {
            var fight = TwoSides();

            fight[0]!["size"] = 5.0;
            fight[1]!["invisible"] = true;

            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = fight,
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 75
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            var rows = answer["by_combatant"]!.AsArray()
                .ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!, StringComparer.Ordinal);

            Assert.Equal(5.0, rows["hero"]["size"]!.GetValue<double>());
            Assert.False(rows["hero"]["invisible"]!.GetValue<bool>());

            Assert.Equal(Combatant.SameSize, rows["villain"]["size"]!.GetValue<double>());
            Assert.True(rows["villain"]["invisible"]!.GetValue<bool>());

            // The control: the same fight said nothing about either, and the echo says so — so the
            // figures above are this call's and not a constant printed on every answer.
            var plain = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 75
            });

            Assert.True(plain["ok"]!.GetValue<bool>());

            foreach (var row in plain["by_combatant"]!.AsArray())
            {
                Assert.Equal(Combatant.SameSize, row!["size"]!.GetValue<double>());
                Assert.False(row["invisible"]!.GetValue<bool>());
            }

            // And the control that the two fields were not merely carried through an echo: the same
            // seed against the same characters gives a different measurement, so what was echoed is
            // something the run actually used.
            Assert.NotEqual(
                plain["mean_pages"]!.GetValue<double>(),
                answer["mean_pages"]!.GetValue<double>());
        });

    /// <summary>
    /// <b>Every shape of a size p.75 cannot make a ratio of is refused by name, and nothing else
    /// is.</b>
    ///
    /// <para>The roster of refusal codes drives <c>BAD_SIZE</c> once, with a zero. A zero is the
    /// one that is obviously dangerous — it divides, and the infinity that comes out satisfies
    /// every band there is — but it is one of several: a negative size inverts the comparison, a
    /// string or a flag is not a figure at all, and a literal too large for a <c>double</c> is an
    /// infinity arriving by another door. Each is driven here, because a reader of the guard has no
    /// way to tell a case it handles from one it happens to reach through a different branch.</para>
    ///
    /// <para><b>The accepted half is the control</b>, and it is what stops this being a test that a
    /// server refusing every size would pass: the fixture below opens a fight on a size on each
    /// side of 1 and gets it back echoed as sent.</para>
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.5")]
    [InlineData("\"big\"")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("1e400")]
    public async Task ASizeThatCannotBeARatioIsRefusedByName(string literal) =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = WithRawSize(JsonNode.Parse(literal))
            });

            Assert.False(answer["ok"]!.GetValue<bool>(), $"a size of {literal} opened a fight");

            Assert.Equal("BAD_SIZE", answer["problem"]!["code"]!.GetValue<string>());

            // The refusal names the page whose bands it could not be a ratio for, rather than
            // reading as a schema complaint about a number.
            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.Contains("p.75", message, StringComparison.Ordinal);
            Assert.Contains("ratio", message, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>The control on the fixture above: a real size above zero is taken, on either side of 1.</b>
    ///
    /// <para>Without this, a server that refused every <c>"size"</c> it was ever sent would satisfy
    /// every case of <see cref="ASizeThatCannotBeARatioIsRefusedByName"/>. It is also the other
    /// half of the echo: the figure that comes back is the one that went out, so a server that
    /// accepted the value and then dropped it cannot pass either.</para>
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(180.0)]
    public async Task ARealSizeAboveZeroIsTakenAndEchoedAsSent(double size) =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = WithSize(size)
            });

            Assert.True(answer["ok"]!.GetValue<bool>(), $"a size of {size} was refused");

            var hero = Assert.Single(
                answer["turn_order"]!.AsArray(),
                c => string.Equals(c!["id"]!.GetValue<string>(), "hero", StringComparison.Ordinal));

            Assert.Equal(size, hero!["size"]!.GetValue<double>());
        });

    /// <summary>
    /// <b>Two turns in flight on one fight land in order, rather than on top of each other.</b>
    ///
    /// <para>Taking a turn is <em>read the held state, step it, write the result back</em>, and the
    /// class's own doc comment used to say that <c>EncounterState</c> being immutable made
    /// overlapping calls safe. It does not. Two overlapping turns read the same state, both step
    /// it, and the second write discards the first turn — while its caller is told <c>ok: true</c>
    /// and handed a ledger for a page the fight no longer has. A record quietly missing a page of
    /// itself is the worst failure available to a server whose whole product is a record you can
    /// trust. Immutability stops the discarded turn corrupting the surviving one; it does not stop
    /// the discard.</para>
    ///
    /// <para><b>Driven through the seam rather than by firing turns and hoping.</b> The first
    /// version of this test fired sixteen <c>take_turn</c>s at once and asserted their turn indices
    /// were 1 to 16 with no repeats. Run five times against a build with the gate taken out, it went
    /// red <em>once</em> — the window between the read and the write is a few microseconds wide, so
    /// four runs in five the sixteen calls simply queued up and the guard reported green on a server
    /// that loses turns. So <c>midTurn</c> holds the first turn open inside the gate, with the state
    /// it has just read, until this test lets go: the second turn then either gets in (no gate, and
    /// both turns write the same page) or waits at the gate (the fix, and the two turns land in
    /// order). Neither answer involves a clock.</para>
    ///
    /// <para><b>The positive control is the second fight.</b> "The second turn never got in" is also
    /// what a server that handles one call at a time looks like, and against one of those this guard
    /// would pass without the gate existing at all. So while the first turn is held, a turn on
    /// <em>another</em> encounter is driven to completion: it proves the server really does run two
    /// tool calls at once, and it proves the gate is per fight rather than one lock for the server —
    /// which is the whole reason it lives on <c>Held</c>.</para>
    ///
    /// <para>That second fight is also the bound on the race, in place of a sleep: after the second
    /// turn is sent, whole tool calls are driven to completion on the other encounter. The server
    /// reads its messages in order, so the second turn was dispatched before any of them, and each
    /// one that answers is a full round trip the second turn has had to reach the seam in. Under the
    /// defect it reaches it in microseconds; under the fix it cannot reach it at all.</para>
    /// </summary>
    [Fact]
    public async Task TwoTurnsInFlightOnOneFightLandInOrder()
    {
        var inside = 0;

        var firstIsInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGotInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var letGo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Assigned before any turn is taken, and read only by turns on the fight under test — the
        // other encounter's turns have to run freely, since they are the control.
        var underTest = "";

        var tools = new PlayTools(_f.Rules, _f.Derived, _play, midTurn: id =>
        {
            if (!string.Equals(id, underTest, StringComparison.Ordinal)) return;

            if (Interlocked.Increment(ref inside) == 1)
            {
                firstIsInside.SetResult();
                letGo.Task.Wait();
            }
            else
            {
                secondGotInside.TrySetResult();
            }
        });

        await WithClient(tools, async client =>
        {
            try
            {
                underTest = await OpenAFight(client);
                var control = await OpenAFight(client);

                var first = EndTurn(client, underTest);

                await Waited(firstIsInside.Task, "the first turn never reached the gate");

                // The control, and it has to come before the race: a server that answers one call
                // at a time would satisfy every assertion below with no gate in it anywhere.
                var elsewhere = await Waited(
                    EndTurn(client, control),
                    "a turn on a second fight could not finish while a turn on the first was held — "
                    + "either this server answers one call at a time, in which case nothing below "
                    + "means anything, or the gate is one lock for the whole server rather than one "
                    + "per fight");

                Assert.True(elsewhere["ok"]!.GetValue<bool>(), elsewhere.ToJsonString());
                Assert.Equal(1, elsewhere["state"]!["turn_index"]!.GetValue<int>());

                var second = EndTurn(client, underTest);

                // Three whole tool calls, answered, after the second turn was sent. Not a delay:
                // the server reads its messages in the order they arrive, so the second turn was
                // dispatched first and has had three round trips in which to reach the seam.
                var roundTrips = Task.Run(async () =>
                {
                    for (var i = 0; i < 3; i++) await EndTurn(client, control);
                });

                await Task.WhenAny(secondGotInside.Task, roundTrips);

                Assert.False(secondGotInside.Task.IsCompleted,
                    "two turns on one fight were inside the read-modify-write at the same time, so "
                    + "both read the same state and the second write discards the first turn");

                letGo.SetResult();

                var a = await Waited(first, "the held turn never finished");
                var b = await Waited(second, "the queued turn never finished");

                // The control for the assertion below: "no two turns claim the same index" is
                // satisfied perfectly by a run in which one of them was refused.
                Assert.True(a["ok"]!.GetValue<bool>(), a.ToJsonString());
                Assert.True(b["ok"]!.GetValue<bool>(), b.ToJsonString());
                // And the ledger says two turns happened, not one. There are two combatants in
                // the order, so the *second* end_turn is the one that runs off the end of it and
                // cites the rule that says when a page ends. Without the gate neither turn ever
                // reaches the end of the order, so that line is in neither answer.
                var pageEnded = new[] { a, b }
                    .SelectMany(turn => turn["added"]!.AsArray())
                    .Count(line => string.Equals(
                        line!["rule"]!.GetValue<string>(), "pages_and_turns", StringComparison.Ordinal));

                Assert.Equal(1, pageEnded);

                // Both turns landed, and they landed one after the other. Without the gate both
                // report 1, because both stepped the same state.
                Assert.Equal(
                    [1, 2],
                    new[] { a, b }.Select(t => t["state"]!["turn_index"]!.GetValue<int>()).Order());
            }
            finally
            {
                // So that a failed assertion is a red test rather than a held thread and a suite
                // that never finishes.
                letGo.TrySetResult();
            }
        });
    }

    private static async Task<string> OpenAFight(McpClient client)
    {
        var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
        {
            ["combatants"] = TwoSides()
        });

        Assert.True(opened["ok"]!.GetValue<bool>(), opened.ToJsonString());

        return opened["encounter_id"]!.GetValue<string>();
    }

    /// <summary>
    /// One <c>end_turn</c>, which is the intent whose effect is countable: it advances the turn
    /// index by exactly one and nothing else, so two of them that both landed are 1 and 2 and two
    /// that collided are 1 and 1.
    /// </summary>
    private static Task<JsonNode> EndTurn(McpClient client, string encounter) =>
        Call(client, "take_turn", new Dictionary<string, object?>
        {
            ["encounterId"] = encounter,
            ["intent"] = new JsonObject { ["kind"] = "end_turn", ["actor"] = "hero" }
        });

    /// <summary>
    /// How long a step of this test will wait for something that should already have happened.
    /// <b>It is not part of the reasoning</b> — every answer this test gives is settled by work the
    /// server completed, never by the clock. This is what turns a wedged server into a named failure
    /// instead of a suite that hangs, so it is generous.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static async Task Waited(Task task, string what)
    {
        Assert.True(await Task.WhenAny(task, Task.Delay(Patience)) == task, what);
        await task;
    }

    private static async Task<T> Waited<T>(Task<T> task, string what)
    {
        Assert.True(await Task.WhenAny(task, Task.Delay(Patience)) == task, what);
        return await task;
    }

    /// <summary>
    /// <b>A fight that is over takes no more turns.</b>
    ///
    /// <para><c>Over</c> means one side has nobody standing, and the server went on stepping past
    /// it: defeated combatants kept being rolled for, the page count kept climbing, and the ledger
    /// filled with lines about a fight that had already been decided. Every one of those lines
    /// carries a real rule id and a real printed page, so nothing in the answer tells a reader they
    /// are reading the aftermath — which is the one thing a ledger exists to make impossible.</para>
    ///
    /// <para><b>The fight is fought rather than faked, and the control is that it ended.</b> A
    /// 12d Hero against a single Threat-1 Minion, attacking and turning the page until the state
    /// says <c>over</c>; if it never does, this fails saying so rather than asserting anything about
    /// a refusal that was never provoked. Nothing here asserts a die roll — only that the engine
    /// eventually finishes a fight one side cannot lose slowly.</para>
    /// </summary>
    [Fact]
    public async Task TakeTurnIsRefusedOnceTheFightIsOver() =>
        await WithClient(async client =>
        {
            var id = await AFightThatIsOver(client);

            var answer = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "soldier",
                    ["target"] = "thug", ["trait_id"] = "might"
                }
            });

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("ENCOUNTER_OVER", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.Contains(id, message, StringComparison.Ordinal);

            // And what to do instead, by tool name — a refusal that only says no sends a model
            // straight back into the same call.
            Assert.Contains("start_encounter", message, StringComparison.Ordinal);
        });

    /// <summary>
    /// A fight one side cannot lose, fought until the state says it is over, and its id.
    ///
    /// <para><b>Fought rather than faked, and the control is in here rather than in the callers:</b>
    /// if the fight never ends this fails saying so, so no test built on it can go on to assert
    /// something about a refusal that was never provoked. Nothing here asserts a die roll — only
    /// that the engine eventually finishes a fight a 12d Hero is having with one Threat-1 Minion.
    /// </para>
    /// </summary>
    private static async Task<string> AFightThatIsOver(McpClient client)
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
                            ["might"] = 12, ["toughness"] = 12, ["agility"] = 12
                        }
                    }
                },
                new JsonObject
                {
                    ["kind"] = "minions", ["id"] = "thug", ["name"] = "the last thug",
                    ["threat_rank"] = 1, ["count"] = 1, ["side"] = "villains"
                }),
            ["seed"] = 7
        });

        var id = opened["encounter_id"]!.GetValue<string>();

        async Task<JsonNode> Act(JsonObject intent) =>
            await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = id, ["intent"] = intent
            });

        var over = false;

        for (var page = 0; page < 40 && !over; page++)
        {
            await Act(new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "soldier",
                ["target"] = "thug", ["trait_id"] = "might"
            });

            var turned = await Act(new JsonObject { ["kind"] = "end_page", ["actor"] = "soldier" });

            over = turned["state"]!["over"]!.GetValue<bool>();
        }

        Assert.True(over,
            "The fight never reached over: true, so nothing was provoked and the refusal the "
            + "caller is about was never reached.");

        return id;
    }

    /// <summary>
    /// <b>A fight with one side in it is refused by both tools, not measured.</b>
    ///
    /// <para><c>EncounterState.Over</c> and every policy partition on <c>Combatant.Side</c> and on
    /// nothing else, so a fight in which everybody shares a side is over before it starts and
    /// <c>run_encounters</c> answered <c>win_rate: 1.0</c> for that side — a figure that looks
    /// exactly like a real one, quotable and reproducible, printed beside its N, its seeds, its
    /// policy and its table, and meaning nothing whatever. It is the worst possible answer for this
    /// tool to give, because the whole apparatus around the number is intact.</para>
    ///
    /// <para>Both tools, because a caller who opens such a fight is one <c>take_turn</c> away from
    /// the same nonsense, and because refusing it in one place only would send them to the other.
    /// The commonest way to make one is to leave <c>side</c> off every entry: the default is derived
    /// from the kind, so two Heroes land on the same side without anybody typing it.</para>
    /// </summary>
    [Theory]
    [InlineData("start_encounter")]
    [InlineData("run_encounters")]
    public async Task AFightWithOneSideIsRefusedByBothTools(string tool) =>
        await WithClient(async client =>
        {
            var oneSided = TwoSides();
            oneSided[1]!["side"] = "heroes";
            oneSided[1]!["kind"] = "hero";

            var arguments = new Dictionary<string, object?> { ["combatants"] = oneSided };

            if (string.Equals(tool, "run_encounters", StringComparison.Ordinal))
                arguments["runs"] = PlayTools.FewestRuns;

            var answer = await Call(client, tool, arguments);

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("ONE_SIDED", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            // The side everybody is on, by name, and what to do about it.
            Assert.Contains("heroes", message, StringComparison.Ordinal);
            Assert.Contains("side", message, StringComparison.Ordinal);
        });

    /// <summary>
    /// And the same two combatants on two sides still run, which is what keeps the refusal above
    /// from being a tool that never answers. It also pins the fact the refusal turns on: two Heroes
    /// are a fight the book prints, so what is refused is one *side*, never one *kind*.
    /// </summary>
    [Fact]
    public async Task TwoHeroesOnTwoSidesStillMeasure() =>
        await WithClient(async client =>
        {
            var heroes = TwoSides();
            heroes[1]!["kind"] = "hero";

            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = heroes,
                ["runs"] = PlayTools.FewestRuns,
                ["maxPages"] = 8
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            Assert.Equal(
                ["heroes", "villains"],
                answer["by_side"]!.AsArray()
                    .Select(s => s!["side"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        });

    /// <summary>
    /// <b>A side made only of Minions reports no mean Health, rather than a mean of zero.</b>
    ///
    /// <para>Ch.4 p.77 gives a group of Minions one characteristic — Threat — and no Health, so the
    /// side's total adds nothing for them and the report came back
    /// <c>mean_health_remaining: 0.0</c>. That reads as a side ground down to the last point in
    /// every single run, which is the opposite of what the measurement may have found, and it is
    /// exactly the figure a balance question is asked about. <c>by_combatant</c> already answered
    /// null for a Minion group; this is the same honesty one level up.</para>
    ///
    /// <para>Two controls, because "the field is null" is also what a report that lost the field
    /// looks like. The Heroes' side has to carry a number in the same answer, and the Minions'
    /// entry in <c>by_combatant</c> has to carry a count of them still standing — so the run
    /// happened, the side was measured, and the null is a statement about Minions rather than a
    /// hole.</para>
    /// </summary>
    [Fact]
    public async Task ASideOfMinionsReportsNoMeanHealthRatherThanZero() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = new JsonArray(
                    new JsonObject
                    {
                        ["kind"] = "hero",
                        ["id"] = "hero",
                        ["side"] = "heroes",
                        ["character"] = new JsonObject
                        {
                            ["Name"] = "the Hero",
                            ["SelectedTierId"] = "standard",
                            ["AbilityRanks"] = new JsonObject
                            {
                                ["might"] = 8, ["toughness"] = 5, ["willpower"] = 4
                            }
                        }
                    },
                    new JsonObject
                    {
                        ["kind"] = "minions", ["id"] = "robots", ["name"] = "the robots",
                        ["threat_rank"] = 4, ["count"] = 3, ["side"] = "villains"
                    }),
                ["runs"] = PlayTools.FewestRuns,
                ["maxPages"] = 6
            });

            Assert.True(answer["ok"]!.GetValue<bool>(), answer["problem"]?.ToJsonString());

            var sides = answer["by_side"]!.AsArray()
                .ToDictionary(s => s!["side"]!.GetValue<string>(), s => s!, StringComparer.Ordinal);

            Assert.Null(sides["villains"]["mean_health_remaining"]);

            // The controls: the other side was measured, and the Minions were counted.
            Assert.NotNull(sides["heroes"]["mean_health_remaining"]);
            Assert.InRange(sides["heroes"]["mean_health_remaining"]!.GetValue<double>(), 0.0, double.MaxValue);

            var robots = answer["by_combatant"]!.AsArray()
                .Single(c => string.Equals(c!["id"]!.GetValue<string>(), "robots", StringComparison.Ordinal))!;

            Assert.NotNull(robots["mean_minions_remaining"]);
            Assert.Null(robots["mean_health_remaining"]);
        });

    /// <summary>
    /// <b>Every turn says what the fight reproduces from.</b>
    ///
    /// <para>The seed and the Challenge Level are what a fight is replayed from, and they were
    /// printed once — in the answer to <c>start_encounter</c>. A conversation twenty turns into a
    /// fight is a conversation whose opening answer is a long way up: a client that wanted to run
    /// the fight again had to go back and find it, and a model summarising one had nothing in front
    /// of it to quote. They are on every <c>take_turn</c> state now, read off the held fight rather
    /// than off the arguments of the call that opened it.</para>
    ///
    /// <para>Both are set to something that is not the default, because a field that answers 0 is
    /// indistinguishable from a field that is not there when the default is 0.</para>
    /// </summary>
    [Fact]
    public async Task EveryTurnSaysWhatTheFightReproducesFrom() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["seed"] = 4242,
                ["challengeLevel"] = 3
            });

            Assert.True(opened["ok"]!.GetValue<bool>(), opened.ToJsonString());

            var turn = await EndTurn(client, opened["encounter_id"]!.GetValue<string>());

            Assert.True(turn["ok"]!.GetValue<bool>(), turn.ToJsonString());

            var state = turn["state"]!;

            // Named rather than dereferenced, so a state that stopped carrying them fails saying so
            // instead of throwing a null reference out of the next line.
            Assert.True(state["seed"] is not null,
                $"the turn's state does not say what seed the fight is running on: {state.ToJsonString()}");

            Assert.True(state["challenge_level"] is not null,
                $"the turn's state does not say the scene's Challenge Level: {state.ToJsonString()}");

            Assert.Equal(4242, state["seed"]!.GetValue<int>());
            Assert.Equal(3, state["challenge_level"]!.GetValue<int>());
        });

    /// <summary>
    /// <b>The last seed of a run of runs is a seed.</b>
    ///
    /// <para><c>seed + runs - 1</c> is int arithmetic and it is unchecked. From a first seed near
    /// <see cref="int.MaxValue"/> it wraps: the report printed a <c>last</c> seed <em>below</em> its
    /// <c>first</c>, and the runs themselves were taken on seeds that ran off the top and came back
    /// round — every one a real fight, none of them the fight that was asked for, and the whole
    /// answer reproducible only by somebody who repeated the overflow. This tool's entire product is
    /// a number quoted beside the seeds it was measured on, so a seed range that does not reproduce
    /// it is the worst shape the answer can take.</para>
    ///
    /// <para><b>The control is the run that fits exactly.</b> A refusal is easy to get by refusing
    /// everything, so the same call one seed lower has to be answered — and its report has to print
    /// the last seed as <see cref="int.MaxValue"/> itself rather than as something negative.</para>
    /// </summary>
    [Fact]
    public async Task TheLastSeedOfARunOfRunsIsStillASeed() =>
        await WithClient(async client =>
        {
            var over = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = int.MaxValue
            });

            Assert.False(over["ok"]!.GetValue<bool>(), over.ToJsonString());
            Assert.Equal("SEED_RANGE", over["problem"]!["code"]!.GetValue<string>());

            // The one that fits, to the seed. Nothing is refused that reproduces.
            var first = int.MaxValue - (PlayTools.FewestRuns - 1);

            var fits = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = first,
                ["maxPages"] = 4
            });

            Assert.True(fits["ok"]!.GetValue<bool>(), fits["problem"]?.ToJsonString());
            Assert.Equal(first, fits["seeds"]!["first"]!.GetValue<long>());
            Assert.Equal(int.MaxValue, fits["seeds"]!["last"]!.GetValue<long>());
        });

    /// <summary>
    /// <b>A Challenge Level below zero is refused by both tools rather than read as zero.</b>
    ///
    /// <para><c>Math.Max(0, …)</c> read −3 as 0: the fight opened with the Adversity a Challenge
    /// Level of nothing buys, and the answer echoed <c>challenge_level: 0</c> — so a scene somebody
    /// had deliberately set below the baseline was measured as the baseline, and the report said the
    /// baseline was what they asked for. Accepted, ignored and unannounced, which is the shape this
    /// server refuses everywhere else.</para>
    ///
    /// <para>The control is the second half: a Challenge Level the tools <em>do</em> take comes back
    /// echoed as itself, so this is a refusal of one value and not a tool that stopped reading the
    /// argument.</para>
    /// </summary>
    [Theory]
    [InlineData("start_encounter")]
    [InlineData("run_encounters")]
    public async Task AChallengeLevelBelowZeroIsRefusedByBothTools(string tool) =>
        await WithClient(async client =>
        {
            var arguments = new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["challengeLevel"] = -3
            };

            if (string.Equals(tool, "run_encounters", StringComparison.Ordinal))
            {
                arguments["runs"] = PlayTools.FewestRuns;
                arguments["maxPages"] = 4;
            }

            var answer = await Call(client, tool, arguments);

            Assert.False(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());
            Assert.Equal("BAD_CHALLENGE_LEVEL", answer["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("-3", answer["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);

            arguments["challengeLevel"] = 3;

            var taken = await Call(client, tool, arguments);

            Assert.True(taken["ok"]!.GetValue<bool>(), taken["problem"]?.ToJsonString());
            Assert.Equal(3, taken["challenge_level"]!.GetValue<int>());
        });

    /// <summary>
    /// <b>A refusal comes back as a successful tool result with <c>ok: false</c> in it, and the
    /// policy document says so.</b>
    ///
    /// <para>This is the half of the shape a client gets wrong. MCP gives a tool result an
    /// <c>isError</c> flag, and nothing here ever sets it: a refusal is an ordinary answer whose
    /// payload says it could not do what was asked. A client waiting for a protocol-level error
    /// reads <c>{"ok": false}</c> as a fight that started and then takes turns in an encounter that
    /// does not exist — and a model does the same thing, in prose, which is worse. The document is
    /// what every conversation is taught from, so the claim has to be in it.</para>
    ///
    /// <para><b>Driven and read together, deliberately.</b> Asserting the shape without the document
    /// leaves every conversation guessing, and asserting the document without the shape is a claim
    /// about behaviour that nothing checks — which is how this document came to promise an argument
    /// the schema has not got. The control is the second call: a tool that *did* what was asked has
    /// to come back the same way, or "isError was not set" is just a server that never sets it
    /// because it never refuses.</para>
    /// </summary>
    [Fact]
    public async Task ARefusalIsAnAnswerRatherThanAProtocolError() =>
        await WithClient(async client =>
        {
            var refused = await client.CallToolAsync("start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = WithTier("standrad")
            });

            // Null, not false: the SDK leaves the flag unset, and a server that set it to false
            // would be making a different claim about the same call.
            Assert.Null(refused.IsError);

            var payload = JsonNode.Parse(Text(refused))!;

            Assert.False(payload["ok"]!.GetValue<bool>());
            Assert.Equal("NO_SUCH_TIER", payload["problem"]!["code"]!.GetValue<string>());

            // The control: a call that worked arrives the same way, so the assertion above is about
            // the refusal and not about a flag this server never touches at all.
            var opened = await client.CallToolAsync("start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides()
            });

            Assert.Null(opened.IsError);
            Assert.True(JsonNode.Parse(Text(opened))!["ok"]!.GetValue<bool>());

            // And the document a conversation is taught from says both halves.
            var flowed = new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5))
                .Replace(PlayPolicy.Text, " ");

            Assert.Contains("\"ok\": false", flowed, StringComparison.Ordinal);
            Assert.Contains("isError", flowed, StringComparison.Ordinal);
            Assert.Contains("A refusal is an answer, not a transport error", flowed, StringComparison.Ordinal);
        });

    // ── Every refusal this server can give ────────────────────────────────

    /// <summary>
    /// One refusal, and the call that provokes it.
    /// </summary>
    /// <param name="Drive">What to call. It must come back refused with the code it is filed under.</param>
    /// <param name="Server">
    /// The tools to serve while driving it, or null for the ordinary ones. Only the two refusals
    /// that wrap a throw out of the engine need their own: on the rules this repository ships the
    /// engine has no caller's fault left to throw — every one of them is refused by name before it
    /// gets there — so provoking one means handing the engine a rule it cannot apply.
    /// </param>
    private sealed record Refusal(
        Func<McpClient, Task<JsonNode>> Drive,
        Func<PlayTools>? Server = null);

    /// <summary>
    /// <b>Every problem code this server can answer with, and a call over the wire that provokes
    /// it.</b>
    ///
    /// <para>Nine of the twenty-nine were driven; the other twenty were written and never called. A
    /// refusal is the whole of what a model has to work with when a call goes wrong — it is the
    /// difference between "<c>start_encounter</c> takes a <c>side</c>" and a tool that appears to be
    /// broken — and an untested one is a sentence nobody has read since it was typed, on a branch
    /// nobody has taken. All twenty went green first time, which is the honest result and not a
    /// reason not to have asked: what the list is really for is the next code, and the control
    /// below is what makes it cost something to add one without a case.</para>
    ///
    /// <para>Filed by code rather than by tool, because the code is what the answer carries and
    /// what <see cref="TheseAreEveryCodeTheServerCanEmit"/> counts.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Refusal> Refusals =
        new Dictionary<string, Refusal>(StringComparer.Ordinal)
        {
            // ── Opening a fight ──
            ["NO_COMBATANTS"] = new(client => Open(client, new JsonArray())),

            ["BAD_COMBATANT"] = new(client => Open(client, new JsonArray(JsonValue.Create(7)))),

            ["NO_SUCH_KIND"] = new(client => Open(client, new JsonArray(
                new JsonObject { ["kind"] = "wizard", ["side"] = "heroes" }))),

            ["NO_CHARACTER"] = new(client => Open(client, new JsonArray(
                new JsonObject { ["kind"] = "hero", ["side"] = "heroes" }))),

            ["CHARACTER_UNREADABLE"] = new(client => Open(client, new JsonArray(
                new JsonObject
                {
                    ["kind"] = "hero",
                    ["side"] = "heroes",
                    // A rank is a number; "8d" is how the sheet prints it and not how it is written.
                    ["character"] = new JsonObject
                    {
                        ["Name"] = "the Hero",
                        ["SelectedTierId"] = "standard",
                        ["AbilityRanks"] = new JsonObject { ["might"] = "8d" }
                    }
                }))),

            ["COMBATANT_UNBUILDABLE"] = new(client => Open(client, new JsonArray(
                new JsonObject
                {
                    ["kind"] = "hero",
                    ["side"] = "heroes",
                    // A character in the right shape naming a Power these rules have not got: the
                    // strict reader takes it and the character engine cannot derive a rank for it.
                    ["character"] = new JsonObject
                    {
                        ["Name"] = "the Hero",
                        ["SelectedTierId"] = "standard",
                        ["SelectedPowers"] = new JsonArray(new JsonObject
                        {
                            ["PowerId"] = "chronokinesis",
                            ["PurchasedRanks"] = 3,
                            ["Pros"] = new JsonArray(),
                            ["Cons"] = new JsonArray()
                        })
                    }
                }))),

            ["NO_SUCH_TIER"] = new(client => Open(client, WithTier("standrad"))),

            ["BAD_MINIONS"] = new(client => Open(client, new JsonArray(
                new JsonObject
                {
                    ["kind"] = "minions", ["name"] = "the robots", ["count"] = 4, ["side"] = "villains"
                }))),

            ["DUPLICATE_COMBATANT"] = new(client => Open(client, BothCalled("hero"))),

            ["ONE_SIDED"] = new(client => Open(client, AllOnOneSide())),

            ["BAD_TABLE"] = new(client => Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["wound_penalties"] = "yes" }
            })),

            ["NO_SUCH_TABLE_SETTING"] = new(client => Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["wound_penalty"] = true }
            })),

            ["NO_SUCH_RANGE"] = new(client => Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["openingRange"] = "sideways"
            })),

            ["NO_SUCH_VISIBILITY"] = new(client => Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["visibility"] = "gloomy"
            })),

            // p.75's size bands are a ratio, so a zero divides and the infinity it yields satisfies
            // every band there is — refused rather than taken as the default.
            ["BAD_SIZE"] = new(client => Open(client, WithSize(0))),

            ["ENCOUNTER_WOULD_NOT_OPEN"] = new(
                client => Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = TwoSides(),
                    ["table"] = new JsonObject { ["gm_alternative_to_seizing_initiative"] = true }
                }),
                ToolsOverARuleTheEngineCannotApply),

            // ── Measuring ──
            ["TOO_FEW_RUNS"] = new(client => Measure(client, PlayTools.FewestRuns - 1)),

            ["TOO_MANY_RUNS"] = new(client => Measure(client, PlayTools.MostRuns + 1)),

            ["BAD_PAGE_LIMIT"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["maxPages"] = 0
            })),

            ["NO_SUCH_POLICY"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["policy"] = "everybody_runs_away"
            })),

            ["SEED_RANGE"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = int.MaxValue
            })),

            ["BAD_CHALLENGE_LEVEL"] = new(client => Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["challengeLevel"] = -3
            })),

            // Two sheets, two campaigns, two different sets of house rules — and no honest way to
            // pick one, so neither is picked.
            ["TABLE_DISAGREES"] = new(client => Open(client, UnderTwoTables())),

            // The sheets carry a table and the call passes a different one. Neither wins quietly.
            ["CALL_TABLE_DISAGREES"] = new(client => Call(client, "start_encounter",
                new Dictionary<string, object?>
                {
                    ["combatants"] = UnderOneTable(HouseRules()),
                    ["table"] = new JsonObject { ["fatal_damage"] = true }
                })),

            ["RUN_REFUSED"] = new(
                client => Call(client, "run_encounters", new Dictionary<string, object?>
                {
                    ["combatants"] = TwoSides(),
                    ["runs"] = PlayTools.FewestRuns,
                    ["table"] = new JsonObject { ["gm_alternative_to_seizing_initiative"] = true }
                }),
                ToolsOverARuleTheEngineCannotApply),

            // ── Taking a turn ──
            ["NO_SUCH_ENCOUNTER"] = new(client => Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = "enc_no_such_thing",
                ["intent"] = new JsonObject { ["kind"] = "end_turn", ["actor"] = "hero" }
            })),

            ["BAD_INTENT"] = new(client => Turn(client, "hold")),

            ["NO_SUCH_INTENT"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "dance", ["actor"] = "hero"
            })),

            ["NO_SUCH_DAMAGE"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                ["trait_id"] = "might", ["damage"] = "squishy"
            })),

            ["NO_SUCH_TYPE"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                ["trait_id"] = "might", ["type"] = "wizardry"
            })),

            ["NO_SUCH_COVER"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                ["trait_id"] = "might", ["cover"] = "a hedge"
            })),

            ["NO_SUCH_MOVE"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "grapple", ["actor"] = "hero", ["target"] = "villain", ["move"] = "hug"
            })),

            ["NO_SUCH_SPEND"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "spend_resolve", ["actor"] = "hero", ["spend"] = "everything"
            })),

            ["NO_SUCH_AS_RESOLVE"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "spend_adversity", ["actor"] = "villain",
                ["spend"] = "anything_resolve_can", ["as_resolve"] = "a_second_breakfast"
            })),

            ["INTENT_REFUSED"] = new(client => Turn(client, new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "nobody_in_this_fight",
                ["target"] = "villain", ["trait_id"] = "might"
            })),

            // ── The styles, the target selectors and the matrix ──────────────

            ["NO_SUCH_STYLE"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["style"] = "everybody_runs_away"
            })),

            // <b>Refused by its own name, not as "no such style".</b> narrative is a real way of
            // playing and its absence from the seeded list is a decision: nothing in this engine
            // makes a Flaw bite, so a seeded policy pretending to it would name a measurement that
            // was measuring something else.
            ["NARRATIVE_IS_NOT_SEEDED"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["style"] = "narrative"
            })),

            ["NO_SUCH_TARGETING"] = new(client => Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["targeting"] = "whoever_looks_shiftiest"
            })),

            ["TOO_FEW_RUNS_A_CELL"] = new(client => Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns
            })),

            ["NO_SUCH_MATCHUP"] = new(client => Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRunsACell,
                ["matchups"] = "everybody_against_everybody"
            })),

            ["NO_PARTY"] = new(client => Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = NobodyIsAHero(),
                ["runs"] = PlayTools.FewestRunsACell
            })),

            ["MATRIX_TOO_LARGE"] = new(client => Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.MostRuns
            })),

            ["ENCOUNTER_OVER"] = new(async client => await Call(client, "take_turn",
                new Dictionary<string, object?>
                {
                    ["encounterId"] = await AFightThatIsOver(client),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "soldier",
                        ["target"] = "thug", ["trait_id"] = "might"
                    }
                }))
        };

    /// <summary>
    /// Every code this server can refuse with, as a set — the same keys the theory below drives,
    /// so <see cref="McpPlayPolicyTests"/> can hold the play policy's named refusals to them
    /// without keeping a second list of its own.
    /// </summary>
    public static IReadOnlySet<string> ProblemCodes { get; } =
        Refusals.Keys.ToHashSet(StringComparer.Ordinal);

    public static TheoryData<string> EveryProblemCode => [.. Refusals.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// <b>Every problem code, provoked over the wire, comes back as an answer.</b>
    ///
    /// <para>Three things are asserted of each and they are three different failures: the payload
    /// says <c>ok: false</c> (a refusal that reads as a success is a fight a model will go on
    /// narrating), the code is the one this case is filed under (a branch that has drifted onto
    /// another code is a client whose error handling stops matching), and the message says
    /// something (a code with no sentence behind it is a dead end). <see cref="Call"/> asserts the
    /// fourth for every call in this file: it did not arrive as a protocol error.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryProblemCode))]
    public async Task EveryProblemCodeIsDrivenOverTheWire(string code)
    {
        var refusal = Refusals[code];

        await WithClient(refusal.Server?.Invoke() ?? Tools(), async client =>
        {
            var answer = await refusal.Drive(client);

            Assert.False(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());
            Assert.Equal(code, answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.False(string.IsNullOrWhiteSpace(message), $"{code} refuses without saying why.");
        });
    }

    /// <summary>
    /// <b>The theory above covers every code the server can emit, and the list is read out of the
    /// source rather than kept beside it.</b>
    ///
    /// <para>This is the control the theory is worth nothing without: a table of cases proves only
    /// that the cases in it work, and the failure it exists to catch is a code added to
    /// <c>PlayTools</c> and to nothing else — which is how twenty of the twenty-nine got here in
    /// the first place. Reading the codes out of the file makes the theory's list the thing that
    /// has to be updated, not a thing somebody might remember to.</para>
    ///
    /// <para><b>One code is not a literal.</b> <c>TryReadEnum</c> builds
    /// <c>"NO_SUCH_" + field.ToUpperInvariant()</c>, so the codes it can answer with are the field
    /// names it is called with — five of them, all on an intent. The scan reads those calls too,
    /// and asserts that the concatenation is still in the source: a version of it that had gone
    /// back to literals would otherwise leave five codes claimed and unscanned.</para>
    /// </summary>
    [Fact]
    public void TheseAreEveryCodeTheServerCanEmit()
    {
        var source = File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "mcp-play", "PlayTools.cs"));

        const string Concatenated = @"Problem(""NO_SUCH_"" + field.ToUpperInvariant()";

        // The scan's own controls, before its result is compared with anything: a regex that has
        // stopped matching agrees with an empty list perfectly.
        Assert.Contains(Concatenated, source, StringComparison.Ordinal);

        var literals = Regex.Matches(source, @"Problem\(""([A-Z_]+)"",")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var built = Regex.Matches(source, @"TryReadEnum<\w+>\(entry, ""(\w+)""")
            .Select(m => "NO_SUCH_" + m.Groups[1].Value.ToUpperInvariant())
            .ToList();

        Assert.Contains("ONE_SIDED", literals, StringComparer.Ordinal);
        Assert.Contains("NO_SUCH_DAMAGE", built, StringComparer.Ordinal);

        var emitted = literals.Concat(built).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            emitted.Order(StringComparer.Ordinal),
            Refusals.Keys.Order(StringComparer.Ordinal));
    }

    // ── What the cases above are built from ───────────────────────────────

    private static Task<JsonNode> Open(McpClient client, JsonNode combatants) =>
        Call(client, "start_encounter", new Dictionary<string, object?> { ["combatants"] = combatants });

    private static Task<JsonNode> Measure(McpClient client, int runs) =>
        Call(client, "run_encounters", new Dictionary<string, object?>
        {
            ["combatants"] = TwoSides(),
            ["runs"] = runs
        });

    /// <summary>One intent, on a fight opened for it — the id is never the thing under test here.</summary>
    private static async Task<JsonNode> Turn(McpClient client, JsonNode intent) =>
        await Call(client, "take_turn", new Dictionary<string, object?>
        {
            ["encounterId"] = await OpenAFight(client),
            ["intent"] = intent
        });

    private static JsonArray WithTier(string tier)
    {
        var fight = TwoSides();
        fight[0]!["character"]!["SelectedTierId"] = tier;
        return fight;
    }

    /// <summary>The fight with one combatant given a size p.75's bands cannot be a ratio of.</summary>
    private static JsonArray WithSize(double size)
    {
        var fight = TwoSides();
        fight[0]!["size"] = size;
        return fight;
    }

    /// <summary>The same, for a <c>"size"</c> that is not a number at all.</summary>
    private static JsonArray WithRawSize(JsonNode? size)
    {
        var fight = TwoSides();
        fight[0]!["size"] = size;
        return fight;
    }

    /// <summary>
    /// A campaign's house rules, in the shape a stored character carries them: the property names
    /// of <c>CampaignTable</c>, because the sheet is read by the strict reader whose naming policy
    /// is the property name. The <c>.json</c> export's <c>campaign_table</c> is a different
    /// document with a different convention — see
    /// <see cref="ASheetSpellingItsTableTheExportsWayIsRefusedByName"/>.
    /// </summary>
    private static JsonObject HouseRules(bool woundPenalties = true) => new()
    {
        ["FatalDamage"] = true,
        ["WoundPenalties"] = woundPenalties
    };

    /// <summary>The fight with the same house rules on both characters.</summary>
    private static JsonArray UnderOneTable(JsonNode table)
    {
        var fight = TwoSides();
        fight[0]!["character"]!["CampaignTable"] = table.DeepClone();
        fight[1]!["character"]!["CampaignTable"] = table.DeepClone();
        return fight;
    }

    /// <summary>
    /// The fight with a different table on each character, differing in exactly one setting so
    /// that the refusal has a predictable switch to name.
    /// </summary>
    private static JsonArray UnderTwoTables()
    {
        var fight = TwoSides();
        fight[0]!["character"]!["CampaignTable"] = HouseRules();
        fight[1]!["character"]!["CampaignTable"] = HouseRules(woundPenalties: false);
        return fight;
    }

    /// <summary>The fight with house rules on the Hero's sheet and none on the Villain's.</summary>
    private static JsonArray OneOfThemInACampaign()
    {
        var fight = TwoSides();
        fight[0]!["character"]!["CampaignTable"] = HouseRules();
        return fight;
    }

    /// <summary>A second Hero-side character, so a fight can have three sheets in it.</summary>
    private static JsonObject Ally(JsonNode? table = null)
    {
        var ally = new JsonObject
        {
            ["kind"] = "hero",
            ["id"] = "ally",
            ["side"] = "heroes",
            ["character"] = new JsonObject
            {
                ["Name"] = "the Ally",
                ["SelectedTierId"] = "standard",
                ["AbilityRanks"] = new JsonObject { ["might"] = 7, ["toughness"] = 5, ["willpower"] = 4 }
            }
        };

        if (table is not null) ally["character"]!["CampaignTable"] = table;

        return ally;
    }

    /// <summary>
    /// Three sheets disagreeing three ways, so that "the first difference from the first sheet"
    /// has more than one answer and arrival order could pick between them.
    ///
    /// <para>'ally' and 'hero' differ about <c>fatal_damage</c>, which comes fourth in
    /// <c>TableRules.Switches</c>; 'hero' and 'villain' differ about <c>wound_penalties</c>, which
    /// comes tenth. Compare from 'hero' and the refusal names the tenth; compare from 'ally' and it
    /// names the fourth. Both are true, which is exactly why the answer must not depend on which
    /// order somebody typed the array in.</para>
    /// </summary>
    private static JsonArray ThreeWaysApart(bool reversed)
    {
        var fight = TwoSides();
        fight[0]!["character"]!["CampaignTable"] = HouseRules();
        fight[1]!["character"]!["CampaignTable"] = HouseRules(woundPenalties: false);
        fight.Add(Ally(new JsonObject { ["FatalDamage"] = false, ["WoundPenalties"] = true }));

        return reversed ? new JsonArray([.. fight.Reverse().Select(c => c!.DeepClone())]) : fight;
    }

    /// <summary>
    /// One table on two of the four sheets and none on the other two, so the clause page one adds
    /// for the sheets that carried nothing has two names in it to put in an order.
    /// </summary>
    private static JsonArray TwoCarriersAndTwoWithout(bool reversed)
    {
        var fight = TwoSides();
        fight[0]!["character"]!["CampaignTable"] = HouseRules();
        fight.Add(Ally(HouseRules()));
        fight.Add(new JsonObject
        {
            ["kind"] = "extra",
            ["id"] = "bystander",
            ["side"] = "villains",
            ["character"] = new JsonObject
            {
                ["Name"] = "the Bystander",
                ["SelectedTierId"] = "standard",
                ["AbilityRanks"] = new JsonObject { ["might"] = 3, ["toughness"] = 3, ["willpower"] = 3 }
            }
        });

        return reversed ? new JsonArray([.. fight.Reverse().Select(c => c!.DeepClone())]) : fight;
    }

    private static JsonArray BothCalled(string id)
    {
        var fight = TwoSides();
        fight[0]!["id"] = id;
        fight[1]!["id"] = id;
        return fight;
    }

    private static JsonArray AllOnOneSide()
    {
        var fight = TwoSides();
        fight[0]!["side"] = "heroes";
        fight[1]!["side"] = "heroes";
        return fight;
    }

    /// <summary>
    /// The tools over a play rules set whose <c>seize_initiative_gm_alternative</c> no longer says
    /// the printed word the engine reads.
    ///
    /// <para><b>This is the only way left to make the engine throw a caller's fault.</b> Every one
    /// of them — a fight with nobody in it, two combatants sharing an id, a page limit below one —
    /// is refused by name in <c>PlayTools</c> before <c>Begin</c> or <c>RunToEnd</c> is reached, so
    /// the two <c>catch</c> blocks that turn an engine throw into a refusal guard a door nothing
    /// walks through on the rules this repository ships. What still reaches them is a rule the
    /// engine cannot apply, which is exactly what <c>GmAlternativeFactor</c> exists to say: the
    /// entry states its effect in prose, the engine reads the printed word "doubles" and supplies
    /// the factor itself, and a table that turns the switch on against an entry that no longer says
    /// it gets a refusal rather than a crash across the transport.</para>
    ///
    /// <para>The substitution asserts the printed sentence is still there before replacing it, so
    /// this cannot quietly stop reproducing the fault and start passing for another reason.</para>
    /// </summary>
    private static PlayTools ToolsOverARuleTheEngineCannotApply()
    {
        const string Printed = "doubles the buyer's effective Edge";

        var files = PlayRulesRepository.DataFileNames.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(PlayFixture.DataPath, name)),
            StringComparer.Ordinal);

        var combat = files[PlayRulesRepository.CombatFile];

        Assert.Contains(Printed, combat, StringComparison.Ordinal);

        files[PlayRulesRepository.CombatFile] =
            combat.Replace(Printed, "raises the buyer's effective Edge", StringComparison.Ordinal);

        var rules = RulesRepository.FromBasePath(RulesFixture.RepoRoot);

        return new PlayTools(
            rules,
            new DerivedStatsCalculator(rules),
            new PlayRulesRepository(new InMemoryRulesSource(files)));
    }


    // ── p.80's Hard Targets, over the wire ────────────────────────────────

    /// <summary>
    /// <b>A combatant's <c>hard_target</c> flag crosses the wire and doubles their passive
    /// defence.</b>
    ///
    /// <para>Driven through the tools rather than asserted about the reader, for the reason every
    /// fixture in this section is: a field the reader does not have is a field the SDK drops in
    /// silence, which is exactly how p.79's <c>team</c> flag was lost. The control is the same
    /// fight with the flag left off, whose defence pool is the rank on the sheet.</para>
    ///
    /// <para>Subdual, so <c>lethal_and_subdual</c> leaves the Toughness whole and the only thing
    /// moving the pool is the doubling under test.</para>
    /// </summary>
    [Fact]
    public async Task ACombatantsHardTargetFlagCrossesTheWireAndDoublesTheirPassiveDefence() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Swing(bool hard)
            {
                var fight = TwoSides();
                if (hard) fight[1]!["hard_target"] = true;

                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["table"] = new JsonObject { ["hard_targets"] = true },
                    ["seed"] = 80
                });

                var turn = await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                        ["trait_id"] = "might", ["damage"] = "subdual", ["type"] = "unarmed"
                    }
                });

                return turn;
            }

            // The control: the flag off is the rank on the sheet, and no line cites the rule.
            var soft = await Swing(hard: false);

            Assert.Contains("toughness 5d", RollLine(soft), StringComparison.Ordinal);
            Assert.False(Cites(soft, "gritty_hard_targets"));

            var machine = await Swing(hard: true);

            Assert.Contains("toughness 10d", RollLine(machine), StringComparison.Ordinal);
            Assert.True(Cites(machine, "gritty_hard_targets"));

            // And what the caller sent comes back, so a reader of the state can see what was read.
            var villain = machine["state"]!["combatants"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "villain", StringComparison.Ordinal));

            Assert.True(villain!["hard_target"]!.GetValue<bool>());
        });

    /// <summary>
    /// <b>An attack's <c>vulnerable_part</c> crosses the wire, costs the printed dice and cancels
    /// the doubling.</b>
    ///
    /// <para>Both halves are read off one answer: the attack pool falls by
    /// <c>penalty_dice_to_negate_it</c> and the defence pool falls back to the rank on the sheet. A
    /// reader that had dropped the flag would leave both at the doubled figures.</para>
    /// </summary>
    [Fact]
    public async Task AnAttacksVulnerablePartCrossesTheWireAndCancelsTheDoubling() =>
        await WithClient(async client =>
        {
            var penalty = _play.GetGritty("gritty_hard_targets").HardTargets!.PenaltyDiceToNegateIt;

            var fight = TwoSides();
            fight[1]!["hard_target"] = true;

            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = fight,
                ["table"] = new JsonObject { ["hard_targets"] = true },
                ["seed"] = 80
            });

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                    ["trait_id"] = "might", ["damage"] = "subdual", ["type"] = "unarmed",
                    ["vulnerable_part"] = true
                }
            });

            var line = RollLine(turn);

            Assert.Contains($"might {8 + penalty}d", line, StringComparison.Ordinal);
            Assert.Contains("toughness 5d", line, StringComparison.Ordinal);
            Assert.DoesNotContain("toughness 10d", line, StringComparison.Ordinal);
        });


    // ── p.79's Close Range, over the wire ─────────────────────────────────

    /// <summary>
    /// <b>The <c>close_range_penalty</c> setting costs a dodger the printed dice, and an attack's
    /// <c>close_range_only</c> turns it off.</b>
    ///
    /// <para>Both are driven through the tools rather than asserted about the reader, because the
    /// field of an intent is exactly what the play policy's spelling guard cannot see — the guard
    /// is scoped to tool arguments, and this is how p.79's <c>team</c> flag came to be dropped in
    /// silence. The control is the same shot with the setting off, whose pool is the rank on the
    /// sheet.</para>
    /// </summary>
    [Fact]
    public async Task TheCloseRangeSettingCrossesTheWireAndItsThrownWeaponExceptionDoesToo() =>
        await WithClient(async client =>
        {
            var penalty = _play.GetGritty("gritty_close_range")
                .CloseRangePenalty!.PenaltyDiceToActiveDefense;

            async Task<JsonNode> Shoot(bool setting, bool thrown)
            {
                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = TwoSidesDodging(),
                    ["table"] = new JsonObject { ["close_range_penalty"] = setting },
                    ["seed"] = 79
                });

                var intent = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                    ["trait_id"] = "might", ["type"] = "ranged_weapon"
                };

                if (thrown) intent["close_range_only"] = true;

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = intent
                });
            }

            // The control: the setting off is the rank on the sheet, and no line cites the rule.
            var baseline = await Shoot(setting: false, thrown: false);

            Assert.Contains("agility 6d", RollLine(baseline), StringComparison.Ordinal);
            Assert.False(Cites(baseline, "gritty_close_range"));

            var shot = await Shoot(setting: true, thrown: false);

            Assert.Contains($"agility {6 + penalty}d", RollLine(shot), StringComparison.Ordinal);
            Assert.True(Cites(shot, "gritty_close_range"));

            // And the page's own exception, which a reader that had dropped the flag would ignore.
            var knife = await Shoot(setting: true, thrown: true);

            Assert.Contains("agility 6d", RollLine(knife), StringComparison.Ordinal);
            Assert.True(Cites(knife, "gritty_close_range"));
        });


    // ── p.79's Drop, over the wire ────────────────────────────────────────

    /// <summary>
    /// <b>A combatant's <c>ready</c> flag crosses the wire and doubles their Edge for the order of
    /// action.</b>
    ///
    /// <para>Read off the opening answer's turn order, which is the only thing an Edge decides — a
    /// doubling nothing sorted on would be a field this server published and never used. The
    /// control is the same fight with the setting off, whose order is the Edges on the sheets.</para>
    /// </summary>
    [Fact]
    public async Task ACombatantsReadyFlagCrossesTheWireAndDoublesTheirEdgeForTheOrder() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Open(bool setting)
            {
                var fight = TwoSides();

                // The Hero's Edge is behind the Villain's until the drop doubles it.
                fight[0]!["character"]!["AbilityRanks"] =
                    new JsonObject { ["might"] = 8, ["perception"] = 2, ["agility"] = 2 };
                fight[1]!["character"]!["AbilityRanks"] =
                    new JsonObject { ["might"] = 8, ["perception"] = 4, ["agility"] = 3 };

                fight[0]!["ready"] = true;

                return await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["table"] = new JsonObject { ["the_drop"] = setting },
                    ["seed"] = 79
                });
            }

            static string[] Order(JsonNode opened) =>
                [.. opened["turn_order"]!.AsArray().Select(c => c!["id"]!.GetValue<string>())];

            // The control: with the setting off the flag changes nothing, and the Villain is first.
            var off = await Open(setting: false);

            Assert.Equal(["villain", "hero"], Order(off));

            var drawn = await Open(setting: true);

            Assert.Equal(["hero", "villain"], Order(drawn));

            // And the order echoes both the flag the caller sent and the doubled figure it bought,
            // so a reader can see what was read rather than inferring it from who went first.
            var hero = drawn["turn_order"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "hero", StringComparison.Ordinal));

            var before = off["turn_order"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "hero", StringComparison.Ordinal));

            Assert.True(hero!["ready"]!.GetValue<bool>());
            Assert.Equal(before!["edge"]!.GetValue<int>() * 2, hero["edge"]!.GetValue<int>());
        });


    /// <summary>
    /// <b>Both flags cross the wire on a group of Minions too, and both are read there.</b>
    ///
    /// <para>A Minion group is not a character sheet, so it takes a different branch out of the
    /// reader — <c>TryReadMinions</c> rather than <c>CombatantFactory</c> — and a field threaded
    /// through one and not the other is the same silent loss the <c>ready</c> flag itself arrived
    /// with. A swarm of machines is the obvious hard target, and p.79's own example of somebody
    /// with a weapon levelled is a crook, so neither flag is a thing only a Hero can carry.</para>
    ///
    /// <para><b>Each is proved by the engine's answer rather than by the echo.</b> The mob's
    /// hardness is proved by the Threat rank the Hero's attack has to beat; the echo is asserted
    /// beside it, since a state a conversation reads should say what was read.</para>
    /// </summary>
    [Fact]
    public async Task BothFlagsCrossTheWireOnAGroupOfMinionsAndAreReadThere() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Swarm(bool declared)
            {
                var fight = TwoSides();
                fight.RemoveAt(1);

                var mob = new JsonObject
                {
                    ["kind"] = "minions", ["id"] = "mob", ["name"] = "the robots",
                    ["threat_rank"] = 5, ["count"] = 4, ["side"] = "villains"
                };

                if (declared)
                {
                    mob["hard_target"] = true;
                    mob["ready"] = true;
                }

                fight.Add(mob);

                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["table"] = new JsonObject { ["hard_targets"] = true, ["the_drop"] = true },
                    ["seed"] = 80
                });

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero", ["target"] = "mob",
                        ["trait_id"] = "might", ["damage"] = "subdual", ["type"] = "unarmed"
                    }
                });
            }

            // The control: neither flag declared, so the mob answers on the Threat rank it was
            // opened with and no line cites the rule.
            var plain = await Swarm(declared: false);

            Assert.Contains("threat 5d", RollLine(plain), StringComparison.Ordinal);
            Assert.False(Cites(plain, "gritty_hard_targets"));

            var swarm = await Swarm(declared: true);

            // hard_target reached the Minion branch: the Threat answers at twice its rank.
            Assert.Contains("threat 10d", RollLine(swarm), StringComparison.Ordinal);
            Assert.True(Cites(swarm, "gritty_hard_targets"));

            var mob = swarm["state"]!["combatants"]!.AsArray().Single(c =>
                string.Equals(c!["id"]!.GetValue<string>(), "mob", StringComparison.Ordinal));

            Assert.True(mob!["hard_target"]!.GetValue<bool>());
            Assert.True(mob["ready"]!.GetValue<bool>());
        });

    /// <summary>
    /// <b><c>run_encounters</c> echoes each combatant's <c>hard_target</c> and <c>ready</c>.</b>
    ///
    /// <para>Same argument as the size and invisibility beside them, and the same defect one level
    /// on: a rate is quoted with four things and none of them can carry a fact about a character,
    /// so a run measured against a machine whose passive defences were doubled — or against a side
    /// holding the drop — came back looking exactly like a run in which neither was true. The
    /// control is the same call declaring neither, which is required to echo the defaults, so a
    /// server printing a constant cannot satisfy both.</para>
    /// </summary>
    [Fact]
    public async Task RunEncountersEchoesEachCombatantsHardTargetAndReadiness() =>
        await WithClient(async client =>
        {
            var fight = TwoSides();

            fight[0]!["ready"] = true;
            fight[1]!["hard_target"] = true;

            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = fight,
                ["table"] = new JsonObject { ["hard_targets"] = true, ["the_drop"] = true },
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 80
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            var rows = answer["by_combatant"]!.AsArray()
                .ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!, StringComparer.Ordinal);

            Assert.True(rows["hero"]["ready"]!.GetValue<bool>());
            Assert.False(rows["hero"]["hard_target"]!.GetValue<bool>());

            Assert.True(rows["villain"]["hard_target"]!.GetValue<bool>());
            Assert.False(rows["villain"]["ready"]!.GetValue<bool>());

            // The control: the same fight said nothing about either, and the echo says so.
            var quiet = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["hard_targets"] = true, ["the_drop"] = true },
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 80
            });

            Assert.True(quiet["ok"]!.GetValue<bool>());

            foreach (var row in quiet["by_combatant"]!.AsArray())
            {
                Assert.False(row!["hard_target"]!.GetValue<bool>());
                Assert.False(row["ready"]!.GetValue<bool>());
            }

            // And the control that the two were used rather than merely carried through: the same
            // seed against the same characters measures a different fight.
            Assert.NotEqual(
                quiet["mean_pages"]!.GetValue<double>(),
                answer["mean_pages"]!.GetValue<double>());
        });


    /// <summary>
    /// <b><c>run_encounters</c> echoes what each combatant walked in holding.</b>
    ///
    /// <para>Same defect one level on as the size, the invisibility, the hard target and the drop
    /// beside it: a rate is quoted with four things and none of them can carry a fact about a
    /// character. p.76 aims a grab at an opponent who <em>has</em> a handheld item, so a fight
    /// opened with a weapon in somebody's hands is one where a grab can land and a fight opened
    /// without is one where every grab is refused with nothing rolled — two different measurements,
    /// coming back looking identical.</para>
    ///
    /// <para>The control is the same call declaring nothing, which is required to echo null: a
    /// server printing a constant cannot satisfy both.</para>
    /// </summary>
    [Fact]
    public async Task RunEncountersEchoesWhatEachCombatantWalkedInHolding() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSidesArmed("villain"),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 80
            });

            Assert.True(answer["ok"]!.GetValue<bool>());

            var rows = answer["by_combatant"]!.AsArray()
                .ToDictionary(c => c!["id"]!.GetValue<string>(), c => c!, StringComparer.Ordinal);

            Assert.Equal("the sword", rows["villain"]["holding"]!.GetValue<string>());
            Assert.Null(rows["hero"]["holding"]);

            // The control: the same fight said nothing about anybody's hands, and the echo says so
            // rather than repeating the row above.
            var quiet = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns,
                ["seed"] = 80
            });

            Assert.True(quiet["ok"]!.GetValue<bool>());

            foreach (var row in quiet["by_combatant"]!.AsArray()) Assert.Null(row!["holding"]);
        });

    // ── p.80's Friendly Fire, over the wire ───────────────────────────────

    /// <summary>
    /// <b>The <c>friendly_fire</c> setting crosses the wire, costs the printed dice, and sends a
    /// second attack that really happens.</b>
    ///
    /// <para>Nothing is declared for this rule — whether a target is bunched up is derived from the
    /// range bands — so what is driven is the setting itself and the effect it has on a third
    /// combatant's Health. The control is the same fight with the setting off, whose pool is the
    /// rank on the sheet and whose bystander is untouched.</para>
    /// </summary>
    [Fact]
    public async Task TheFriendlyFireSettingCrossesTheWireAndItsSecondAttackReallyHappens() =>
        await WithClient(async client =>
        {
            var penalty = _play.GetGritty("gritty_friendly_fire").FriendlyFire!.PenaltyDice;

            async Task<JsonNode> Fire(bool setting)
            {
                var fight = TwoSides();

                fight.Add(new JsonObject
                {
                    ["kind"] = "extra",
                    ["id"] = "bystander",
                    ["side"] = "villains",
                    ["character"] = new JsonObject
                    {
                        ["Name"] = "the Bystander",
                        ["SelectedTierId"] = "standard",
                        ["AbilityRanks"] = new JsonObject { ["toughness"] = 3, ["willpower"] = 3 }
                    }
                });

                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["table"] = new JsonObject { ["friendly_fire"] = setting },
                    ["seed"] = 80
                });

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = opened["encounter_id"]!.GetValue<string>(),
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                        ["trait_id"] = "might", ["type"] = "ranged_weapon"
                    }
                });
            }

            // The control: the setting off is the rank on the sheet, and no line cites the rule.
            var quiet = await Fire(setting: false);

            Assert.Contains("might 8d", RollLine(quiet), StringComparison.Ordinal);
            Assert.False(Cites(quiet, "gritty_friendly_fire"));

            var into = await Fire(setting: true);

            Assert.Contains($"might {8 + penalty}d", RollLine(into), StringComparison.Ordinal);
            Assert.True(Cites(into, "gritty_friendly_fire"));
        });


    // ── p.80's Slow Healing, over the wire ────────────────────────────────

    /// <summary>
    /// <b>The <c>slow_healing</c> setting crosses the wire, and a character brought round under it
    /// comes back on the wire as standing at the figure that would otherwise have them out.</b>
    ///
    /// <para>Nothing is declared for this rule, so what is driven is the setting and the state it
    /// leaves behind — <c>conscious_at_zero_or_less</c> on the combatant, which is the field a
    /// conversation needs in order not to narrate a character as unconscious when the engine has
    /// them on their feet. The control is the same fight with the setting off, where p.76's own
    /// figure comes back instead.</para>
    /// </summary>
    [Fact]
    public async Task TheSlowHealingSettingCrossesTheWireAndPublishesWhoIsStillStanding() =>
        await WithClient(async client =>
        {
            var restores = _play.GetCombat("instant_recovery")
                .InstantRecovery!.AfterADamagingDefeatRestoresHealth;

            async Task<JsonNode> Recover(bool setting)
            {
                var fight = TwoSides();

                // A Hero with almost nothing to lose, so one blow puts them down and the recovery
                // below is the thing under test.
                fight[0]!["character"]!["AbilityRanks"] =
                    new JsonObject { ["might"] = 1, ["toughness"] = 1, ["willpower"] = 1 };

                var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
                {
                    ["combatants"] = fight,
                    ["table"] = new JsonObject { ["slow_healing"] = setting },
                    ["seed"] = 80
                });

                var id = opened["encounter_id"]!.GetValue<string>();

                // The Hero passes — the ladder puts them first on a tied Edge — the Villain
                // flattens them, and then the Hero buys their feet back.
                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject { ["kind"] = "end_turn", ["actor"] = "hero" }
                });

                await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "attack", ["actor"] = "villain", ["target"] = "hero",
                        ["trait_id"] = "might"
                    }
                });

                return await Call(client, "take_turn", new Dictionary<string, object?>
                {
                    ["encounterId"] = id,
                    ["intent"] = new JsonObject
                    {
                        ["kind"] = "spend_resolve", ["actor"] = "hero",
                        ["spend"] = "instant_recovery"
                    }
                });
            }

            static JsonNode Hero(JsonNode turn) =>
                turn["state"]!["combatants"]!.AsArray().Single(c =>
                    string.Equals(c!["id"]!.GetValue<string>(), "hero", StringComparison.Ordinal))!;

            // The control: with the setting off, p.76's own figure comes back and nobody is
            // standing at nothing.
            var ordinary = Hero(await Recover(setting: false));

            Assert.Equal(restores, ordinary["health"]!.GetValue<int>());
            Assert.False(ordinary["conscious_at_zero_or_less"]!.GetValue<bool>());

            var slowly = Hero(await Recover(setting: true));

            Assert.True(slowly["health"]!.GetValue<int>() <= 0);
            Assert.True(slowly["conscious_at_zero_or_less"]!.GetValue<bool>());
        });

    // ── The table comes off the sheets (the campaign's house rules) ───────

    private static JsonObject TableOf(JsonNode answer) => answer["table"]!.AsObject();

    private static IEnumerable<string> RulesOnTheLedger(JsonNode answer) =>
        answer["ledger"]!.AsArray().Select(l => l!["rule"]!.GetValue<string>());

    private static string PageOneOnTheTable(JsonNode answer) =>
        answer["ledger"]!.AsArray()
            .Single(l => string.Equals(l!["rule"]!.GetValue<string>(), "gritty_overview", StringComparison.Ordinal))!
            ["text"]!.GetValue<string>();

    /// <summary>
    /// <b>Two sheets exported from the same campaign bring their table with them, and the fight is
    /// resolved under it without anybody passing an argument.</b>
    ///
    /// <para>This is the whole slice in one call: the encounter server holds no account and cannot
    /// resolve a campaign id, so the only route a house rule has into a fight is the characters in
    /// it. <c>CampaignTable</c> is on the sheet for exactly that reader.</para>
    ///
    /// <para><b>The positive control is on the ledger rather than on the echo.</b> An echo is this
    /// server repeating what it read, and a build that read the sheets and then opened the fight on
    /// <c>TableRules.Book</c> would echo the sheets' settings perfectly while measuring the book —
    /// which is the "accepted and quietly ignored" every reader in <c>PlayTools</c> refuses. The
    /// <c>gritty_raised_gear_limit</c> line is written by <c>Encounter.Begin</c> out of the table
    /// the <em>engine</em> was built with, so its presence is proof the switches crossed. The
    /// control on the control is the same fight without the block, where the line is absent.</para>
    /// </summary>
    [Fact]
    public async Task ATableOnTheSheetsIsTheTableTheFightIsResolvedUnder() =>
        await WithClient(async client =>
        {
            var housed = await Open(client, UnderOneTable(new JsonObject
            {
                ["WoundPenalties"] = true,
                ["RaisedGearLimit"] = true,
                ["GearLimitRank"] = 12
            }));

            Assert.True(housed["ok"]!.GetValue<bool>(), housed.ToJsonString());

            // The engine really was built with these: Begin writes a line per switch that is on,
            // and one saying the raised Gear Limit is not carried.
            Assert.Contains("gritty_wound_penalties", RulesOnTheLedger(housed), StringComparer.Ordinal);
            Assert.Contains("gritty_raised_gear_limit", RulesOnTheLedger(housed), StringComparer.Ordinal);

            var table = TableOf(housed);

            Assert.True(table["wound_penalties"]!.GetValue<bool>());
            Assert.Equal(12, table["gear_limit_rank"]!.GetValue<int>());
            Assert.Equal("sheets", table["source"]!.GetValue<string>());

            // Page one says so, and it says which sheet it started from.
            Assert.Contains("'hero'", PageOneOnTheTable(housed), StringComparison.Ordinal);

            // The control: the same fight with no block on either sheet carries neither line and
            // is the book.
            var plain = await Open(client, TwoSides());

            Assert.DoesNotContain("gritty_wound_penalties", RulesOnTheLedger(plain), StringComparer.Ordinal);
            Assert.DoesNotContain("gritty_raised_gear_limit", RulesOnTheLedger(plain), StringComparer.Ordinal);
            Assert.Equal("book", TableOf(plain)["source"]!.GetValue<string>());
            Assert.Empty(TableOf(plain)["on"]!.AsArray());
        });

    /// <summary>
    /// <b>Rule (a): sheets that disagree about the table are refused by name, with the switch they
    /// disagree about.</b>
    ///
    /// <para>Two blocks that differ are two contrary claims about which game is being played.
    /// Taking either measures a fight under rules half its combatants were not built for, and the
    /// echo would say the table came from "the sheets" while naming only one of them.</para>
    ///
    /// <para><b>Both characters and the setting are in the message</b>, because a refusal a person
    /// cannot act on is a dead end: knowing that two sheets disagree is not knowing which of the
    /// two to go and re-export.</para>
    /// </summary>
    [Fact]
    public async Task SheetsThatDisagreeAboutTheTableAreRefusedNamingBothAndTheSwitch() =>
        await WithClient(async client =>
        {
            var answer = await Open(client, UnderTwoTables());

            Assert.False(answer["ok"]!.GetValue<bool>());
            Assert.Equal("TABLE_DISAGREES", answer["problem"]!["code"]!.GetValue<string>());

            var message = answer["problem"]!["message"]!.GetValue<string>();

            Assert.Contains("'hero'", message, StringComparison.Ordinal);
            Assert.Contains("'villain'", message, StringComparison.Ordinal);
            Assert.Contains("wound_penalties", message, StringComparison.Ordinal);

            // The one they agree about is not named — which is what makes "the first setting they
            // differ on" a finding rather than a list of every switch there is.
            Assert.DoesNotContain("fatal_damage", message, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Rule (b): a sheet carrying no table fights under the one the others carry, and page one
    /// names it.</b>
    ///
    /// <para><b>Accepted rather than refused, and the argument is that an absent block is silence
    /// and not a contrary claim.</b> A sheet without one has not opted out of anything. Refusing
    /// would make the commonest fight there is unfightable without hand-editing JSON: a campaign's
    /// Hero against a Villain somebody built in the sandbox, which is what the GM running that
    /// campaign does every week. Two sheets that disagree have no honest answer; this has one.
    /// <see cref="ASheetThatNamesACampaignAndCarriesNoTableIsNotAnnouncedAsBeingAtNone"/> is the
    /// other half: silence is not the same silence in every case, and page one says which.</para>
    ///
    /// <para><b>What the refusal would have protected against is answered by saying so.</b> The
    /// sheet that carried none is named on page one and in the echo, which is the discipline this
    /// server applies to every table setting it accepts: the worst of the three behaviours is
    /// accepted, ignored and unannounced, and this is accepted, applied and announced.</para>
    /// </summary>
    [Fact]
    public async Task ASheetWithNoTableFightsUnderTheOthersAndPageOneSaysWhichSheetCarriedNone() =>
        await WithClient(async client =>
        {
            var answer = await Open(client, OneOfThemInACampaign());

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var table = TableOf(answer);

            Assert.Equal("sheets", table["source"]!.GetValue<string>());
            Assert.True(table["wound_penalties"]!.GetValue<bool>());

            // The engine was built with it, not merely told about it.
            Assert.Contains("gritty_wound_penalties", RulesOnTheLedger(answer), StringComparer.Ordinal);

            // And the sheet that brought nothing is named, in the echo and on page one.
            Assert.Contains("'villain'", table["source_note"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("'villain'", PageOneOnTheTable(answer), StringComparison.Ordinal);
            Assert.Contains("no table", PageOneOnTheTable(answer), StringComparison.Ordinal);

            // This Villain names no campaign, so page one may say so — and the fixture below is the
            // one where it may not.
            Assert.Contains("no campaign", PageOneOnTheTable(answer), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>The page-one lines this server writes itself cite an entry that exists and that entry's
    /// own page — and nothing checked that before.</b>
    ///
    /// <para><c>PlayEngineStepTests.EveryLedgerLineCitesAnEntryThatExistsAndThatEntrysPage</c>
    /// walks a ledger the <em>engine</em> produced. Page one carries lines the engine never sees:
    /// the tier line and the table-source line are built here, out of entries looked up here, and
    /// they reach a reader in the same array wearing the same two fields. A citation is a promise
    /// that a printed page says this, so the class of line the engine's guard cannot see is exactly
    /// the class that needs one of its own.</para>
    ///
    /// <para><b>And the table-source line is the one where the promise is easiest to overstate.</b>
    /// <c>gritty_overview</c> is p.79's paragraph about a table reviewing the optional rules and
    /// adopting what it wants; it says nothing about where an MCP server got its switches from, and
    /// it speaks for ten of the thirteen — Checking Your Swing is p.69's, the two initiative
    /// settings are p.73's. So the sentence has to separate the page's claim from this server's
    /// bookkeeping, and the assertions below require both halves to be present rather than only
    /// checking the id.</para>
    ///
    /// <para>Driven over the wire because that is where a reader meets these lines, and with the
    /// count as its control: page one really did carry lines from both builders.</para>
    /// </summary>
    [Fact]
    public async Task ThePageOneLinesThisServerWritesCiteRealEntriesAndTheirOwnPages() =>
        await WithClient(async client =>
        {
            var answer = await Open(client, UnderOneTable(HouseRules()));

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var pageOne = answer["ledger"]!.AsArray()
                .Where(l => l!["page"]!.GetValue<int>() == 1)
                .ToList();

            // The control: both builders wrote. The table-source line is one, the tier lines are
            // one per character with a tier, and the switch lines come from the engine.
            Assert.Contains(pageOne, l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "gritty_overview", StringComparison.Ordinal));
            Assert.Contains(pageOne, l =>
                string.Equals(l!["rule"]!.GetValue<string>(), "starting_resolve", StringComparison.Ordinal));

            foreach (var line in pageOne)
            {
                var rule = line!["rule"]!.GetValue<string>();

                Assert.True(_play.EntryIds().Any(e =>
                        string.Equals(e.Id, rule, StringComparison.Ordinal)),
                    $"page one names a rule '{rule}' that is in none of the five play files: "
                    + line["text"]!.GetValue<string>());

                Assert.Equal(SourceRefOf(rule), line["source_ref"]!.GetValue<string>());
            }

            // The table-source line quotes p.79 for what p.79 says and says the rest is this
            // server's: the switches came from somewhere, and no page has an opinion about where.
            var provenance = PageOneOnTheTable(answer);

            Assert.Contains("p.79 leaves the optional combat rules to the table",
                provenance, StringComparison.Ordinal);
            Assert.Contains("the three beside them", provenance, StringComparison.Ordinal);
            Assert.Contains("came from the sheets handed in", provenance, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>The sheet that carried no table is named whether or not the caller also passed one.</b>
    ///
    /// <para><b>The clause is spelled twice and only one of the two was covered.</b>
    /// <c>TryAgreeTable</c> ends in two branches — sheets alone, and sheets plus an agreeing
    /// <c>table</c> argument — and each appends the "carries no table" clause to its own sentence.
    /// Every fixture over the second branch handed in sheets that all carried one, so deleting the
    /// clause from it passed the entire suite: a fight opened with `table` set and a sandbox
    /// Villain in it would say nothing about the Villain, and the policy's instruction not to
    /// narrate that character as having agreed to the house rules would have nothing behind
    /// it.</para>
    ///
    /// <para>Driven as a theory over both branches, because the point is that the two sentences
    /// make the same promise — and the source is asserted alongside so the case really is the
    /// branch it claims to be.</para>
    /// </summary>
    [Theory]
    [InlineData(false, "sheets")]
    [InlineData(true, "sheets_and_call")]
    public async Task ASheetCarryingNoTableIsNamedWithOrWithoutATableOnTheCall(
        bool alsoOnTheCall, string source) =>
        await WithClient(async client =>
        {
            var arguments = new Dictionary<string, object?>
            {
                ["combatants"] = OneOfThemInACampaign()
            };

            if (alsoOnTheCall)
            {
                arguments["table"] = new JsonObject
                {
                    ["fatal_damage"] = true,
                    ["wound_penalties"] = true
                };
            }

            var answer = await Call(client, "start_encounter", arguments);

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            // The control: this really is the branch the case is about.
            Assert.Equal(source, TableOf(answer)["source"]!.GetValue<string>());

            Assert.Contains("'villain'", PageOneOnTheTable(answer), StringComparison.Ordinal);
            Assert.Contains("carries no table", PageOneOnTheTable(answer), StringComparison.Ordinal);

            Assert.Contains("'villain'",
                TableOf(answer)["source_note"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>A measurement is <em>run</em> under the sheets' table, and not merely echoed with it.</b>
    ///
    /// <para><b>This is the one place the positive control the rest of the slice leans on does not
    /// reach.</b> Every other fixture proves the switches crossed by finding
    /// <c>Encounter.Begin</c>'s ledger line for them — but <c>run_encounters</c> answers with no
    /// ledger at all, so the only fixture over it could assert nothing but the echo. An echo is
    /// this server repeating what it read: a build that read the sheets, echoed them faithfully and
    /// then constructed each of its thirty encounters with <c>TableRules.Book</c> would satisfy
    /// every assertion there was, and the rate somebody quoted off it would be a rate about the
    /// wrong game. That is exactly the "accepted, applied to nothing, and unannounced" this server
    /// refuses everywhere else.</para>
    ///
    /// <para><b>The observable is structural rather than a die roll</b>, which this file may not
    /// assert. Without Fatal Damage a combatant's Health floors at the <c>damage</c> entry's
    /// <c>defeated_at_health</c>, so no mean can be below it; with it on, the floor is the negative
    /// of full Health and a fight only <em>ends</em> when the losing side is at or past that — so
    /// any run ending in a defeat rather than at the page limit drives a mean below zero. Both
    /// halves run in the same call, so the negative is proof the switch reached the engine rather
    /// than proof of a lucky seed.</para>
    /// </summary>
    [Fact]
    public async Task AMeasurementIsRunUnderTheSheetsTableAndNotOnlyEchoedWithIt() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Measure(JsonNode combatants) =>
                await Call(client, "run_encounters", new Dictionary<string, object?>
                {
                    ["combatants"] = combatants,
                    ["runs"] = PlayTools.FewestRuns,
                    ["seed"] = 41
                });

            static IEnumerable<double> MeanHealth(JsonNode report) =>
                report["by_combatant"]!.AsArray()
                    .Select(c => c!["mean_health_remaining"])
                    .Where(h => h is not null)
                    .Select(h => h!.GetValue<double>());

            var fatal = await Measure(UnderOneTable(new JsonObject { ["FatalDamage"] = true }));

            Assert.True(fatal["ok"]!.GetValue<bool>(), fatal.ToJsonString());
            Assert.True(TableOf(fatal)["fatal_damage"]!.GetValue<bool>());
            Assert.Equal("sheets", TableOf(fatal)["source"]!.GetValue<string>());

            // The control: the runs ended in defeats rather than all at the page limit, so a fight
            // really was pushed past the floor.
            Assert.True(fatal["draw_rate"]!.GetValue<double>() < 1.0,
                "every run drew at the page limit, so nothing was driven past the defeat floor.");

            // The engine really was constructed with it: no mean below zero is reachable under the
            // book's floor, and Fatal Damage moves that floor to the negative of full Health.
            Assert.Contains(MeanHealth(fatal), h => h < 0);

            // And the mirror, which is what makes the line above a measurement rather than a
            // quirk: the same fight with no block on the sheets cannot produce one.
            var book = await Measure(TwoSides());

            Assert.Equal("book", TableOf(book)["source"]!.GetValue<string>());
            Assert.All(MeanHealth(book), h => Assert.True(h >= 0,
                $"a run under the book left a mean Health of {h}, which its floor forbids."));
        });

    /// <summary>
    /// The keys the play policy tells a conversation to read off an echoed table, which are the
    /// keys the echo has to have. The four <c>source</c> values are not listed here — they are
    /// driven out of the server and compared with the document both ways.
    /// </summary>
    private static readonly string[] EchoKeysThePolicyUndertakesToName = ["source", "source_note"];

    /// <summary>
    /// The backticked spans in the play policy's paragraph about the echoed table's provenance —
    /// the keys it tells a conversation to read and the values it says they take.
    /// </summary>
    private static List<string> ProvenanceSpansThePolicyNames()
    {
        var text = PlayPolicy.Text;
        var at = text.IndexOf("Where the table came from comes back inside", StringComparison.Ordinal);

        Assert.True(at >= 0,
            "mcp-play/PLAY-POLICY.md no longer has the paragraph about where the echoed table came "
            + "from. Either it has stopped telling conversations to read `source`, or this parse "
            + "has stopped finding it — and in both cases nothing holds the spellings together.");

        var end = text.IndexOf("\n\n", at, StringComparison.Ordinal);

        Assert.True(end > at, "the provenance paragraph runs to the end of the play policy.");

        return new Regex(@"`([a-z][a-z_]*)`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(text[at..end])
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <b>Every name the play policy prints for the echoed table's provenance is one this server
    /// really answers with — the two keys and all four values.</b>
    ///
    /// <para><b>These fall outside the guard that already exists, and that is the finding.</b>
    /// <see cref="EveryArgumentNameThePolicyPrintsIsSpelledTheWayTheSchemaSpellsIt"/> reads the
    /// <c>## The calls</c> section and checks spans against the tools' <em>input schemas</em>.
    /// <c>source</c> and <c>source_note</c> are answer keys in a different section, so nothing
    /// touched them; and the four values a conversation is told to expect — <c>book</c>,
    /// <c>call</c>, <c>sheets</c>, <c>sheets_and_call</c> — come from <c>Wire</c> over a private
    /// enum's member names, so renaming a member silently changes what arrives on the wire while
    /// the document goes on naming the old one. That is the same fault the <c>max_pages</c> guard
    /// was written for, one section further down: the document is right-looking and wrong, and
    /// nothing anywhere says so.</para>
    ///
    /// <para><b>Held in both directions.</b> Every value the server can produce is driven here and
    /// has to be named in the document, and every span in the document's own paragraph has to be
    /// something the server produces — an echo key, one of those values, or a tool name. A key the
    /// document invented sends a reader looking for a field that never arrives.</para>
    /// </summary>
    [Fact]
    public async Task EveryProvenanceNameThePolicyPrintsIsOneThisServerEchoes() =>
        await WithClient(async client =>
        {
            async Task<JsonObject> Echo(JsonNode combatants, JsonNode? table)
            {
                var arguments = new Dictionary<string, object?> { ["combatants"] = combatants };

                if (table is not null) arguments["table"] = table;

                var answer = await Call(client, "start_encounter", arguments);

                Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

                return TableOf(answer);
            }

            var agreeing = new JsonObject { ["fatal_damage"] = true, ["wound_penalties"] = true };

            var echoes = new[]
            {
                await Echo(TwoSides(), null),
                await Echo(TwoSides(), new JsonObject { ["wound_penalties"] = true }),
                await Echo(UnderOneTable(HouseRules()), null),
                await Echo(UnderOneTable(HouseRules()), agreeing)
            };

            var produced = echoes.Select(e => e["source"]!.GetValue<string>())
                .ToHashSet(StringComparer.Ordinal);

            // The control: four calls, four different answers — so a server that had collapsed to
            // one source could not pass the containment below by naming a smaller set.
            Assert.Equal(4, produced.Count);

            var spans = ProvenanceSpansThePolicyNames();

            // The control on the parse: it found the paragraph's own names.
            Assert.Contains("source", spans, StringComparer.Ordinal);
            Assert.Contains("source_note", spans, StringComparer.Ordinal);

            foreach (var source in produced)
            {
                Assert.True(spans.Contains(source, StringComparer.Ordinal),
                    $"this server answers with a table source of '{source}' and the play policy "
                    + "does not name it, so a conversation is not told the value it will get.");
            }

            // Every key the document names is a key the echo has.
            foreach (var key in EchoKeysThePolicyUndertakesToName)
            {
                Assert.All(echoes, echo => Assert.True(echo.ContainsKey(key),
                    $"the play policy tells every conversation to read `{key}` off the echoed "
                    + $"table. The echo has no such key: {echo.ToJsonString()}"));
            }

            // And nothing in the paragraph is a name this server does not answer with — an echo
            // key, a field of the answer the echo sits in (`table` itself), one of the four
            // values, or a tool.
            var tools = (await client.ListToolsAsync()).Select(t => t.Name);
            var whole = await Open(client, TwoSides());

            var allowed = echoes[0].Select(p => p.Key)
                .Concat(whole.AsObject().Select(p => p.Key))
                .Concat(produced)
                .Concat(tools)
                .ToHashSet(StringComparer.Ordinal);

            var invented = spans.Where(s => !allowed.Contains(s)).ToList();

            Assert.True(invented.Count == 0,
                "mcp-play/PLAY-POLICY.md's paragraph about where a table came from prints these "
                + "names and this server answers with none of them, so a reader is sent looking "
                + "for a field or a value that never arrives: " + string.Join(", ", invented) + ".");
        });

    /// <summary>One entry's <c>source_ref</c>, whichever of the five play files it is in.</summary>
    private string SourceRefOf(string id)
    {
        foreach (var (file, entryId) in _play.EntryIds())
        {
            if (!string.Equals(entryId, id, StringComparison.Ordinal)) continue;

            return file switch
            {
                PlayRulesRepository.PlayMetaFile => _play.GetMeta(id).SourceRef,
                PlayRulesRepository.ChallengeFile => _play.GetChallenge(id).SourceRef,
                PlayRulesRepository.CombatFile => _play.GetCombat(id).SourceRef,
                PlayRulesRepository.GrittyFile => _play.GetGritty(id).SourceRef,
                PlayRulesRepository.ResolveFile => _play.GetResolve(id).SourceRef,
                var other => throw new InvalidOperationException($"Unknown play rules file {other}.")
            };
        }

        throw new KeyNotFoundException($"No entry '{id}' in the play rules.");
    }

    /// <summary>
    /// <b>Two sheets whose only difference is a Gear Limit rank behind a switch neither turned on
    /// fight, and the fight they fight is the same fight either way.</b>
    ///
    /// <para>The rank is read only where <c>RaisedGearLimit</c> is on — see
    /// <c>PlayTableRulesTests.ARankBehindAnUnadoptedGearLimitSwitchIsNotADisagreement</c> for the
    /// argument. Driven here because the refusal it would have produced is the visible half: a GM
    /// handed <c>TABLE_DISAGREES</c> naming `gear_limit_rank` cannot repair it in the browser,
    /// which clears the rank when the switch goes off and hides the input while it is off. The only
    /// repair would be hand-editing JSON, which is the thing the decision beside this one is built
    /// to spare them.</para>
    ///
    /// <para><b>The control is the adopted case</b>, in the same call: turn the switch on for one
    /// of them and the fight is refused, so this is not a server that has stopped comparing the
    /// Gear Limit at all.</para>
    /// </summary>
    [Fact]
    public async Task TwoSheetsDifferingOnlyInARankNobodyAdoptedAreNotRefused() =>
        await WithClient(async client =>
        {
            var inert = TwoSides();
            inert[0]!["character"]!["CampaignTable"] =
                new JsonObject { ["WoundPenalties"] = true, ["GearLimitRank"] = 6 };
            inert[1]!["character"]!["CampaignTable"] =
                new JsonObject { ["WoundPenalties"] = true, ["GearLimitRank"] = 12 };

            var answer = await Open(client, inert);

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());
            Assert.Equal("sheets", TableOf(answer)["source"]!.GetValue<string>());

            // The switch is off, so nothing in the fight reads either figure.
            Assert.False(TableOf(answer)["raised_gear_limit"]!.GetValue<bool>());
            Assert.Contains("gritty_wound_penalties", RulesOnTheLedger(answer), StringComparer.Ordinal);

            // The control: adopt it on one side and the same pair is refused — by the switch, which
            // is the name a GM can act on.
            var adopted = TwoSides();
            adopted[0]!["character"]!["CampaignTable"] = new JsonObject
            {
                ["WoundPenalties"] = true, ["RaisedGearLimit"] = true, ["GearLimitRank"] = 6
            };
            adopted[1]!["character"]!["CampaignTable"] =
                new JsonObject { ["WoundPenalties"] = true, ["GearLimitRank"] = 12 };

            var refused = await Open(client, adopted);

            Assert.False(refused["ok"]!.GetValue<bool>(), refused.ToJsonString());
            Assert.Equal("TABLE_DISAGREES", refused["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("raised_gear_limit",
                refused["problem"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>A group of Minions is on neither side of the table question: it carries none, and page
    /// one does not say so.</b>
    ///
    /// <para><b>Both halves are decisions and both could have gone the other way.</b> A Minion
    /// group is not a sheet — it is a threat rank and a count — so there is no field on it a
    /// campaign's house rules could arrive in, and nothing to compare against the sheets that do
    /// carry one. Excluding it from the agreement check is therefore not a hole: silence here is
    /// complete rather than partial, unlike a character sheet, where an absent block is one of
    /// three states (see
    /// <see cref="ASheetThatNamesACampaignAndCarriesNoTableIsNotAnnouncedAsBeingAtNone"/>).</para>
    ///
    /// <para><b>And it is left off page one's list of who brought nothing, deliberately.</b> That
    /// clause exists so a reader learns that a <em>character</em> is being fought under rules its
    /// own game may not play; "the robots carry no table" is true of every group of Minions there
    /// has ever been, and a line that always says the same thing is a line readers learn to skip —
    /// which costs the clause the one job it has. The GM's four robots have no game of their own to
    /// be taken out of.</para>
    ///
    /// <para>The control is that the group really is in the fight: it is in the turn order, so this
    /// is not a check that passed because the Minions were dropped on the way in.</para>
    /// </summary>
    [Fact]
    public async Task AGroupOfMinionsIsNotNamedAsCarryingNoTable() =>
        await WithClient(async client =>
        {
            var fight = OneOfThemInACampaign();
            fight.Add(new JsonObject
            {
                ["kind"] = "minions", ["id"] = "robots", ["name"] = "the robots",
                ["threat_rank"] = 5, ["count"] = 4, ["side"] = "villains"
            });

            var answer = await Open(client, fight);

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());
            Assert.Equal("sheets", TableOf(answer)["source"]!.GetValue<string>());

            // The control: the robots are in the fight, so their absence from the sentence below is
            // about the sentence and not about the fight.
            Assert.Contains("robots",
                answer["turn_order"]!.AsArray().Select(t => t!["id"]!.GetValue<string>()),
                StringComparer.Ordinal);

            var page = PageOneOnTheTable(answer);

            // The Villain's sheet carried nothing and is named. The robots have no sheet and are
            // not — naming them would be a line true of every Minion group there is.
            Assert.Contains("'villain'", page, StringComparison.Ordinal);
            Assert.DoesNotContain("robots", page, StringComparison.Ordinal);
            Assert.DoesNotContain("robots",
                TableOf(answer)["source_note"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// A <see cref="TableRules"/> built back out of an echo, switch by switch, <b>driven by
    /// <c>TableRules.Switches</c> rather than by the keys the echo happens to have</b>.
    ///
    /// <para>That direction is the whole of it. Reading the echo's own keys would prove the echo
    /// self-consistent and nothing else: a build that had dropped a switch from the echo would
    /// hand back an object this method reconstructed perfectly, agreeing with itself about a game
    /// it had not described. Asking for every switch the engine has means a missing key is a
    /// failure here, which is the defect this file has already shipped once in a different
    /// place — a setting accepted, applied and not reported.</para>
    /// </summary>
    private static TableRules RebuiltFrom(JsonObject echo)
    {
        var rebuilt = new TableRules();
        var names = TableRules.Switches.Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();

        // The control: the engine has switches to ask about at all.
        Assert.True(names.Count >= 13, $"only {names.Count} table settings were found to rebuild.");

        foreach (var name in names)
        {
            var key = PlayTools.Wire(name);

            Assert.True(echo.ContainsKey(key),
                $"the echoed table carries no '{key}'. A report is quoted with its table, and a "
                + "switch missing from the echo is a game the reader cannot identify — the run "
                + "carried it and the answer did not say so.");

            var property = typeof(TableRules).GetProperty(name)
                           ?? throw new InvalidOperationException($"TableRules has no {name}.");

            property.SetValue(rebuilt, string.Equals(name, nameof(TableRules.GearLimitRank), StringComparison.Ordinal)
                ? echo[key]?.GetValue<int>()
                : echo[key]!.GetValue<bool>());
        }

        return rebuilt;
    }

    /// <summary>
    /// <b>The table a run was resolved under can be rebuilt from the answer alone, and what comes
    /// back is the table the fight really ran under.</b>
    ///
    /// <para><c>run_encounters</c> answers with no ledger, so its echo is the only record a
    /// measurement leaves. "Quoted with its table" is worth nothing if the echo is a summary: a
    /// reader has to be able to reconstruct the thing and fight the same fight again.</para>
    ///
    /// <para><b>The round trip is closed against the engine and not against the fixture.</b>
    /// Rebuilding the echo and comparing it to the block the fixture put on the sheets would check
    /// that this server can copy a JSON object. What is checked instead is that the rebuilt table's
    /// <c>On()</c> is exactly the set of switches <c>Encounter.Begin</c> wrote a ledger line for —
    /// those lines come off the <c>TableRules</c> the encounter was constructed with, so agreement
    /// means the echo describes the game that was played. The fixture's own block is checked too,
    /// last, so that a run under the book could not satisfy the first part trivially.</para>
    /// </summary>
    [Fact]
    public async Task ATableRebuiltFromTheEchoIsTheTableTheFightRanUnder() =>
        await WithClient(async client =>
        {
            var block = new JsonObject
            {
                ["WoundPenalties"] = true,
                ["TheDrop"] = true,
                ["CheckingYourSwing"] = true,
                ["RaisedGearLimit"] = true,
                ["GearLimitRank"] = 9
            };

            var answer = await Open(client, UnderOneTable(block));

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var rebuilt = RebuiltFrom(TableOf(answer));

            // What the engine itself was built with: Begin writes one line per switch that is on,
            // naming the setting in the sentence.
            var applied = answer["ledger"]!.AsArray()
                .Select(l => l!["text"]!.GetValue<string>())
                .Where(t => t.StartsWith("table setting ", StringComparison.Ordinal))
                .Select(t => t.Split(' ')[2])
                .Order(StringComparer.Ordinal)
                .ToList();

            // The control: the run really did carry switches, so the equality below is not two
            // empty lists agreeing. Five, because `On()` counts a Gear Limit rank that was set as
            // one — the four flags plus the rank's own presence.
            Assert.Equal(5, applied.Count);

            Assert.Equal(applied, rebuilt.On().Order(StringComparer.Ordinal));

            // `On()` says a rank was set; only the echo says which one it was, so the figure needs
            // its own assertion.
            Assert.Equal(9, rebuilt.GearLimitRank);

            // Last, the whole record against the sheets' own block — a run under the book would
            // have satisfied nothing above and cannot satisfy this.
            Assert.Equal(TableRules.From(new CampaignTable
            {
                WoundPenalties = true,
                TheDrop = true,
                CheckingYourSwing = true,
                RaisedGearLimit = true,
                GearLimitRank = 9
            }), rebuilt);
        });

    /// <summary>
    /// <b>The same fight refuses the same way whichever order its sheets arrive in — the same pair
    /// and the same switch.</b>
    ///
    /// <para><b>With two sheets this is free and with three it is not</b>, which is why the fixture
    /// has three. <c>FirstDifference</c> is symmetric, so a pair always names the same setting; but
    /// every carrier is compared against <em>the first</em>, and "the first difference from the
    /// first sheet" has a different answer depending on which sheet that is. Here 'ally' and 'hero'
    /// differ about <c>fatal_damage</c> and 'hero' and 'villain' differ about
    /// <c>wound_penalties</c>: start from 'hero' and the refusal names the second, start from
    /// 'ally' and it names the first. Both are true findings, and a caller who reversed their
    /// combatant array would be handed a different one for the same bad export.</para>
    ///
    /// <para><b>That matters because a refusal is a thing two people compare.</b> A GM and a player
    /// reading the same fight typed two ways would see two settings named and reasonably conclude
    /// there are two problems. Taking carriers by id makes the answer a fact about the set of
    /// sheets; ids are unique here, so the order is total.</para>
    ///
    /// <para>The control is the last assertion: the answer is not merely <em>stable</em>, it is the
    /// one the id order picks — a build that had frozen on the arrival order would give two equal
    /// answers only if the reversal had stopped reversing.</para>
    /// </summary>
    [Fact]
    public async Task TheSameSheetsRefuseTheSameWayWhicheverOrderTheyArriveIn() =>
        await WithClient(async client =>
        {
            var asTyped = await Open(client, ThreeWaysApart(reversed: false));
            var reversed = await Open(client, ThreeWaysApart(reversed: true));

            Assert.False(asTyped["ok"]!.GetValue<bool>(), asTyped.ToJsonString());
            Assert.Equal("TABLE_DISAGREES", asTyped["problem"]!["code"]!.GetValue<string>());
            Assert.Equal("TABLE_DISAGREES", reversed["problem"]!["code"]!.GetValue<string>());

            var one = asTyped["problem"]!["message"]!.GetValue<string>();
            var two = reversed["problem"]!["message"]!.GetValue<string>();

            Assert.Equal(one, two);

            // And it is the id order's answer: 'ally' sorts first, so the pair is 'ally' and 'hero'
            // and the switch is the one they differ about.
            Assert.Contains("'ally' and 'hero'", one, StringComparison.Ordinal);
            Assert.Contains("fatal_damage", one, StringComparison.Ordinal);
            Assert.DoesNotContain("wound_penalties", one, StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>And the accepted case's page-one sentence is the same sentence whichever order the sheets
    /// arrive in.</b>
    ///
    /// <para>Page one is a description of a fight, not of the JSON array somebody typed. Two
    /// carriers and two sheets carrying nothing is the smallest fight in which both halves of the
    /// sentence — which sheet the table was read from, and the names of the sheets that brought
    /// none — have an order to get wrong.</para>
    /// </summary>
    [Fact]
    public async Task PageOnesTableSentenceIsTheSameWhicheverOrderTheSheetsArriveIn() =>
        await WithClient(async client =>
        {
            var asTyped = await Open(client, TwoCarriersAndTwoWithout(reversed: false));
            var reversed = await Open(client, TwoCarriersAndTwoWithout(reversed: true));

            Assert.True(asTyped["ok"]!.GetValue<bool>(), asTyped.ToJsonString());
            Assert.True(reversed["ok"]!.GetValue<bool>(), reversed.ToJsonString());

            Assert.Equal(PageOneOnTheTable(asTyped), PageOneOnTheTable(reversed));

            // The control: the sentence really does have all four sheets' worth of work in it, so
            // an equality that passed on two empty strings would be caught.
            Assert.Contains("'ally'", PageOneOnTheTable(asTyped), StringComparison.Ordinal);
            Assert.Contains("'bystander' and 'villain'",
                PageOneOnTheTable(asTyped), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>An absent table is not one state, and page one must not print the flattering reading of
    /// it.</b>
    ///
    /// <para><b>The claim this fixture exists to falsify</b> is the one the accept-and-announce
    /// decision was argued from: that a sheet carrying no block "has never been in a game that
    /// adopted anything". It is not true. <c>CampaignJoin.CopyHouseRules</c> writes the campaign's
    /// table into an empty field with <c>??=</c> and nothing writes at all afterwards, so a
    /// character who joined <em>before</em> the GM adopted anything keeps a null block for ever —
    /// and <c>Inspect</c>'s table check requires both sides to have set something, so no finding is
    /// produced and no panel exists to print one under. <c>docs/guide/browser.md</c> records that
    /// direction as known and unreported.</para>
    ///
    /// <para>So the same absent block means one of three things, and this server can separate the
    /// first from the other two because <c>CampaignId</c> sits on the sheet beside it. Announcing a
    /// character with a campaign as though it had none is exactly the reassurance a refusal was
    /// declined in favour of: the GM reads "carries no table", takes it for the sandbox Villain
    /// they built, and never learns that a player's Hero from another game has just been fought
    /// under rules that game may not play.</para>
    ///
    /// <para><b>Both directions are driven</b>, because a sentence that said the careful thing
    /// about every sheet would be as useless as one that said the flattering thing: the sandbox
    /// Villain above really does name no campaign, and page one really does say so.</para>
    /// </summary>
    [Fact]
    public async Task ASheetThatNamesACampaignAndCarriesNoTableIsNotAnnouncedAsBeingAtNone() =>
        await WithClient(async client =>
        {
            var fight = OneOfThemInACampaign();
            fight[1]!["character"]!["CampaignId"] = "the-other-game";

            var answer = await Open(client, fight);

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var page = PageOneOnTheTable(answer);

            // Named, and named with the game it is in.
            Assert.Contains("'villain'", page, StringComparison.Ordinal);
            Assert.Contains("carries no table", page, StringComparison.Ordinal);
            Assert.Contains("'the-other-game'", page, StringComparison.Ordinal);

            // And not announced as being outside any game, which is the false half.
            Assert.DoesNotContain("names no campaign", page, StringComparison.Ordinal);

            // The echo carries the same sentence, because run_encounters has no ledger at all.
            Assert.Contains("'the-other-game'",
                TableOf(answer)["source_note"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Rule (c): a caller who passes a table beside sheets that carry one has to agree with
    /// them, switch by switch.</b>
    ///
    /// <para>Agreement is fine and is echoed as both. A disagreement is refused rather than settled
    /// by a precedence rule, because whichever won, the other is a setting somebody chose and this
    /// server threw away — and the answer would echo a table half the fight was not built for.</para>
    /// </summary>
    [Fact]
    public async Task ACallersTableMustAgreeWithTheSheetsSwitchBySwitch() =>
        await WithClient(async client =>
        {
            // Agreeing: the same two switches, spelled the way the argument is spelled.
            var agreed = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = UnderOneTable(HouseRules()),
                ["table"] = new JsonObject
                {
                    ["fatal_damage"] = true,
                    ["wound_penalties"] = true
                }
            });

            Assert.True(agreed["ok"]!.GetValue<bool>(), agreed.ToJsonString());
            Assert.Equal("sheets_and_call", TableOf(agreed)["source"]!.GetValue<string>());
            Assert.Contains("gritty_wound_penalties", RulesOnTheLedger(agreed), StringComparer.Ordinal);

            // Disagreeing: the call turns one off that the sheets have on. A subset is a
            // disagreement, not a partial agreement.
            var clash = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = UnderOneTable(HouseRules()),
                ["table"] = new JsonObject { ["fatal_damage"] = true }
            });

            Assert.False(clash["ok"]!.GetValue<bool>());
            Assert.Equal("CALL_TABLE_DISAGREES", clash["problem"]!["code"]!.GetValue<string>());
            Assert.Contains("wound_penalties", clash["problem"]!["message"]!.GetValue<string>(),
                StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>Rule (d): no sheet carries a table and none was passed, so the fight is the book — and
    /// the argument still works on its own where the sheets are silent.</b>
    ///
    /// <para>The second half is the regression this pair exists for: teaching the reader to tell an
    /// omitted <c>table</c> argument from one that means the book is what makes the disagreement
    /// refusals possible, and getting it wrong the other way would have refused every fight under a
    /// campaign's rules — or, worse, made the argument stop working.</para>
    /// </summary>
    [Fact]
    public async Task WithNothingOnTheSheetsTheCallStillSetsTheTableAndNothingAtAllIsTheBook() =>
        await WithClient(async client =>
        {
            var book = await Open(client, TwoSides());

            Assert.Equal("book", TableOf(book)["source"]!.GetValue<string>());
            Assert.Contains("the book as printed", PageOneOnTheTable(book), StringComparison.Ordinal);

            var typed = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["table"] = new JsonObject { ["wound_penalties"] = true }
            });

            Assert.Equal("call", TableOf(typed)["source"]!.GetValue<string>());
            Assert.True(TableOf(typed)["wound_penalties"]!.GetValue<bool>());
            Assert.Contains("gritty_wound_penalties", RulesOnTheLedger(typed), StringComparer.Ordinal);
        });

    /// <summary>
    /// <b>A measurement is reproducible from its own echo, including where its table came from.</b>
    ///
    /// <para><c>run_encounters</c> answers with no ledger, so the echo is the only place a report
    /// can say this. A rate is quoted with four things and one of them is <c>table</c>: two runs
    /// whose echoed switches read alike may have got them off the characters or off an argument
    /// somebody typed, and a reader deciding whether the figure is about <em>their</em> game needs
    /// to know which.</para>
    /// </summary>
    [Fact]
    public async Task AMeasurementEchoesWhereItsTableCameFrom() =>
        await WithClient(async client =>
        {
            var report = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = UnderOneTable(HouseRules()),
                ["runs"] = PlayTools.FewestRuns
            });

            Assert.True(report["ok"]!.GetValue<bool>(), report.ToJsonString());

            var table = TableOf(report);

            Assert.Equal("sheets", table["source"]!.GetValue<string>());
            Assert.Contains("'hero'", table["source_note"]!.GetValue<string>(), StringComparison.Ordinal);

            // The four a rate may never be quoted without are still all there, in the same object.
            Assert.NotNull(report["runs"]);
            Assert.NotNull(report["seeds"]);
            Assert.NotNull(report["policy"]);

            // And the switches themselves crossed: fatal_damage and wound_penalties are on.
            Assert.True(table["fatal_damage"]!.GetValue<bool>());
            Assert.True(table["wound_penalties"]!.GetValue<bool>());

            // The control: the same call with no block on the sheets measures the book.
            var baseline = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRuns
            });

            Assert.Equal("book", TableOf(baseline)["source"]!.GetValue<string>());
            Assert.Empty(TableOf(baseline)["on"]!.AsArray());
        });

    /// <summary>
    /// <b>The sheets arrive as the stored payload, not as the <c>.json</c> export — and a caller
    /// who reaches for the export's spelling is told so rather than fought under the book.</b>
    ///
    /// <para>Two documents, two conventions. <c>CharacterSheetJson</c> is PascalCase because its
    /// naming policy is the property name; the <c>.json</c> export is snake_case throughout and
    /// spells the same block <c>campaign_table</c>. This server reads the first, strictly — and
    /// the whole value of reading it strictly is here: <c>campaign_table</c> ignored would be a
    /// campaign's house rules silently dropped, a fight measured under the book, and an echo saying
    /// the table came from nowhere in particular. There is nothing in that answer a reader could
    /// tell apart from a character that really is at no table.</para>
    ///
    /// <para>The control beneath it is the same sheet spelled the reader's way, which is accepted —
    /// otherwise this would pass on a server that refused every sheet it was handed.</para>
    /// </summary>
    [Fact]
    public async Task ASheetSpellingItsTableTheExportsWayIsRefusedByName() =>
        await WithClient(async client =>
        {
            var exported = TwoSides();
            exported[0]!["character"]!["campaign_table"] =
                new JsonObject { ["wound_penalties"] = true };

            var answer = await Open(client, exported);

            Assert.False(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());
            Assert.Equal("CHARACTER_UNREADABLE", answer["problem"]!["code"]!.GetValue<string>());

            // The control: the reader's own spelling of the same block opens the fight, so the
            // refusal above is about the spelling and not about the block.
            var accepted = await Open(client, OneOfThemInACampaign());

            Assert.True(accepted["ok"]!.GetValue<bool>(), accepted.ToJsonString());
            Assert.Equal("sheets", TableOf(accepted)["source"]!.GetValue<string>());
        });


    /// <summary>
    /// <b>p.73's seized initiative, bought out of the GM's pool over the wire.</b>
    ///
    /// <para>It used to come back as a <c>not yet implemented</c> line, so this is the first of four
    /// that a caller reading <c>PLAY-POLICY.md</c>'s last table could not previously have. Three
    /// things have to be true and each is a different failure if it is not: the <c>as_resolve</c>
    /// field crossed (a dropped one is refused for naming no purchase), the pool paid, and the
    /// effect is on the public state — a client decides whether to buy a second one by reading
    /// <c>seized</c> there, and a line saying somebody went first with an order that did not move is
    /// the failure this whole server exists to make impossible.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsPoolSeizesTheInitiativeOverTheWire() =>
        await WithClient(async client =>
        {
            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["challengeLevel"] = 3,
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();
            var before = opened["adversity"]!.GetValue<int>();

            // The control: there is a pool, so a pool that has not moved below is the purchase.
            Assert.True(before >= 1, $"the fight opened on {before} Adversity");

            var turn = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "anything_resolve_can",
                    ["as_resolve"] = "seize_initiative"
                }
            });

            Assert.True(turn["ok"]!.GetValue<bool>());

            var added = turn["added"]!.AsArray();

            Assert.DoesNotContain(added, l =>
                l!["text"]!.GetValue<string>().Contains("not yet implemented", StringComparison.Ordinal));

            Assert.Contains(added, l => string.Equals(
                l!["rule"]!.GetValue<string>(), "adversity_spend_anything_resolve_can", StringComparison.Ordinal));

            Assert.Contains(added, l => string.Equals(
                l!["rule"]!.GetValue<string>(), "seizing_initiative", StringComparison.Ordinal));

            Assert.Equal(before - 1, turn["state"]!["adversity"]!.GetValue<int>());

            Assert.Equal(
                ["villain"],
                turn["state"]!["seized"]!.AsArray().Select(s => s!.GetValue<string>()));
        });

    /// <summary>
    /// <b>p.76's instant recovery, bought out of the GM's pool over the wire, for a Villain who has
    /// been beaten down inside the same fight.</b>
    ///
    /// <para>The blow is landed rather than arranged, because the point of a wire test is the whole
    /// path: a Hero who hits far harder than the Villain can absorb puts them at the defeat figure,
    /// and the state says so before the purchase is made. What comes back is the Health the entry
    /// names and a combatant who is no longer defeated.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsPoolBringsAVillainRoundOverTheWire() =>
        await WithClient(async client =>
        {
            var restored = _play.GetCombat("instant_recovery").InstantRecovery!.AfterADamagingDefeatRestoresHealth;

            var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
            {
                ["combatants"] = Lopsided(heroMight: 40, villainToughness: 1),
                ["challengeLevel"] = 3,
                ["seed"] = 81
            });

            var encounter = opened["encounter_id"]!.GetValue<string>();
            var before = opened["adversity"]!.GetValue<int>();

            Assert.True(before >= 1, $"the fight opened on {before} Adversity");

            var struck = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                    ["trait_id"] = "might"
                }
            });

            // The control: the Villain really is down, so the purchase below is bringing somebody
            // round rather than being refused for there being nothing to recover from.
            Assert.True(Fighter(struck, "villain")["defeated"]!.GetValue<bool>(),
                "the Villain survived the opening blow, so this fixture is not about a recovery");

            var bought = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = encounter,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "anything_resolve_can",
                    ["as_resolve"] = "instant_recovery"
                }
            });

            Assert.True(bought["ok"]!.GetValue<bool>());

            Assert.DoesNotContain(bought["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("not yet implemented", StringComparison.Ordinal));

            Assert.Contains(bought["added"]!.AsArray(), l => string.Equals(
                l!["rule"]!.GetValue<string>(), "instant_recovery", StringComparison.Ordinal));

            Assert.Equal(before - 1, bought["state"]!["adversity"]!.GetValue<int>());
            Assert.Equal(restored, Fighter(bought, "villain")["health"]!.GetValue<int>());
            Assert.False(Fighter(bought, "villain")["defeated"]!.GetValue<bool>());
        });

    /// <summary>
    /// <b>p.79's two Fatal Damage purchases, both bought out of the GM's pool over the wire.</b>
    ///
    /// <para>Two fights rather than one, because the two states are different and neither can be
    /// reached from the other: a Villain on the clock is one a lethal blow took past nothing but not
    /// past the killing line, and a Villain with a blow to buy back is one it took past that line —
    /// which stops the clock rather than starting it. Both are landed rather than arranged, and the
    /// control on each is the state before the purchase.</para>
    /// </summary>
    [Fact]
    public async Task TheGmsPoolStopsTheClockAndBuysBackAFatalBlowOverTheWire() =>
        await WithClient(async client =>
        {
            // <b>On the clock.</b> A Villain built to take punishment, hit hard enough to go below
            // nothing and not hard enough to reach the negative of their full Health.
            var (dying, bleeding, pool) = await StruckDown(client, heroMight: 32, villainToughness: 20);

            Assert.True(Fighter(bleeding, "villain")["dying"]!.GetValue<bool>(),
                $"the Villain is on {Fighter(bleeding, "villain")["health"]} of "
                + $"{Fighter(bleeding, "villain")["full_health"]} and is not bleeding out, so there "
                + "is no clock for this purchase to stop");

            var steadied = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = dying,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "anything_resolve_can",
                    ["as_resolve"] = "stabilise"
                }
            });

            Assert.True(steadied["ok"]!.GetValue<bool>());

            Assert.DoesNotContain(steadied["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("not yet implemented", StringComparison.Ordinal));

            Assert.Contains(steadied["added"]!.AsArray(), l => string.Equals(
                l!["rule"]!.GetValue<string>(), "gritty_fatal_damage", StringComparison.Ordinal));

            Assert.Equal(pool - 1, steadied["state"]!["adversity"]!.GetValue<int>());
            Assert.False(Fighter(steadied, "villain")["dying"]!.GetValue<bool>());

            // The Health did not move, which is what separates this purchase from the rescue below.
            Assert.Equal(
                Fighter(bleeding, "villain")["health"]!.GetValue<int>(),
                Fighter(steadied, "villain")["health"]!.GetValue<int>());

            // <b>Past the killing line.</b> The same Villain, hit twice as hard.
            var (killed, gone, second) = await StruckDown(client, heroMight: 80, villainToughness: 20);

            var full = Fighter(gone, "villain")["full_health"]!.GetValue<int>();
            var floor = -full;

            Assert.True(Fighter(gone, "villain")["health"]!.GetValue<int>() <= floor,
                $"the Villain is on {Fighter(gone, "villain")["health"]} Health and the fatal "
                + $"threshold is {floor}, so there is no blow for this purchase to buy back");

            var rescued = await Call(client, "take_turn", new Dictionary<string, object?>
            {
                ["encounterId"] = killed,
                ["intent"] = new JsonObject
                {
                    ["kind"] = "spend_adversity",
                    ["actor"] = "villain",
                    ["spend"] = "anything_resolve_can",
                    ["as_resolve"] = "avoid_fatal_damage"
                }
            });

            Assert.True(rescued["ok"]!.GetValue<bool>());

            Assert.DoesNotContain(rescued["added"]!.AsArray(), l =>
                l!["text"]!.GetValue<string>().Contains("not yet implemented", StringComparison.Ordinal));

            Assert.Equal(second - 1, rescued["state"]!["adversity"]!.GetValue<int>());

            // p.79's worked example is one point above the threshold, which is the reading the
            // entry's interpretation carries and the engine follows.
            Assert.Equal(floor + 1, Fighter(rescued, "villain")["health"]!.GetValue<int>());
        });

    /// <summary>
    /// Opens a lopsided fight and lands one lethal blow, answering with the encounter's id, the turn
    /// that landed it and the pool as it stands.
    /// </summary>
    private static async Task<(string Encounter, JsonNode Struck, int Pool)> StruckDown(
        McpClient client, int heroMight, int villainToughness)
    {
        var opened = await Call(client, "start_encounter", new Dictionary<string, object?>
        {
            ["combatants"] = Lopsided(heroMight, villainToughness),
            ["table"] = new JsonObject { ["fatal_damage"] = true },
            ["challengeLevel"] = 3,
            ["seed"] = 81
        });

        var encounter = opened["encounter_id"]!.GetValue<string>();

        var struck = await Call(client, "take_turn", new Dictionary<string, object?>
        {
            ["encounterId"] = encounter,
            ["intent"] = new JsonObject
            {
                ["kind"] = "attack", ["actor"] = "hero", ["target"] = "villain",
                ["trait_id"] = "might", ["damage"] = "lethal"
            }
        });

        return (encounter, struck, opened["adversity"]!.GetValue<int>());
    }

    /// <summary>One combatant off a turn's public state, by id.</summary>
    private static JsonNode Fighter(JsonNode turn, string id) =>
        turn["state"]!["combatants"]!.AsArray().Single(c =>
            string.Equals(c!["id"]!.GetValue<string>(), id, StringComparison.Ordinal))!;

    /// <summary>
    /// A Hero who hits harder than the Villain can absorb, for the p.76 and p.79 wire tests.
    ///
    /// <para>The figures are arguments because the three fights want three different answers: a
    /// Villain beaten to the defeat figure, one taken past nothing and left on the clock, and one
    /// taken past the negative of their full Health. Nothing here is a legal character and nothing
    /// needs to be — whether a character is legal is the other server's question, and this one only
    /// reads the sheet.</para>
    /// </summary>
    private static JsonArray Lopsided(int heroMight, int villainToughness) =>
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
                ["AbilityRanks"] = new JsonObject
                {
                    ["might"] = heroMight, ["toughness"] = 5, ["willpower"] = 4
                }
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
                ["AbilityRanks"] = new JsonObject
                {
                    ["might"] = 8, ["toughness"] = villainToughness, ["willpower"] = 4
                }
            }
        }
    ];

    /// <summary>
    /// A Hero and a Villain, built the shortest way that is still a legal shape for the strict
    /// reader — enough to open a fight for the tests that are about something else.
    /// </summary>
    /// <summary>
    /// The same two, with <paramref name="who"/> walking into the fight holding the sword — the
    /// wire's <c>holding</c> field, which is what p.76's grab is aimed at.
    ///
    /// <para><b>Nothing else can put an item in a hand at the start of a fight, and until this field
    /// existed nothing could at all</b> — so every grab a caller made was for an object its target
    /// was not recorded as carrying, and the engine handed the winner one anyway.</para>
    /// </summary>
    private static JsonArray TwoSidesArmed(string who)
    {
        var fight = TwoSides();

        foreach (var entry in fight)
        {
            if (entry!["id"]!.GetValue<string>() == who) entry["holding"] = "the sword";
        }

        return fight;
    }

    // ── Styles, targeting, the unfair line and the matrix ─────────────────

    /// <summary>
    /// <b>The unfair flag sits exactly where the owner put it: at a half, inclusive, over at least a
    /// hundred runs.</b>
    ///
    /// <para><b>Driven at the boundary, because no fight can be made to land on it.</b> "Half or
    /// less chance of victory over a hundred or more sims" is a sentence with two edges and both are
    /// asked here — 50 wins in 100 is unfair and 51 is not, and 50 in 99 is neither, because below
    /// the floor "not unfair" and "not enough fights to say" are different answers and only one of
    /// them is reassuring. The wire test below is the control that this predicate is the one the
    /// report actually calls.</para>
    /// </summary>
    [Theory]
    [InlineData(50, 100, true)]
    [InlineData(51, 100, false)]
    [InlineData(0, 100, true)]
    [InlineData(100, 100, false)]
    public void TheUnfairFlagSitsAtTheOwnersLine(int wins, int runs, bool unfair) =>
        Assert.Equal(unfair, PlayTools.IsUnfair(wins, runs));

    /// <summary>
    /// <b>And it is null below the floor rather than false</b>, at exactly one run short of it.
    /// </summary>
    [Fact]
    public void BelowTheOwnersFloorTheFlagIsNoAnswerRatherThanAGoodOne()
    {
        Assert.Null(PlayTools.IsUnfair(50, PlayTools.FewestRunsForAVerdict - 1));
        Assert.NotNull(PlayTools.IsUnfair(50, PlayTools.FewestRunsForAVerdict));
    }

    /// <summary>
    /// <b>A side that cannot win is flagged and the side beating it is not — over the wire, out of a
    /// real report.</b>
    ///
    /// <para>Its control is the other half of the same answer: a report that flagged everything, or
    /// that had lost the field, would fail on the winning side. And the sentence saying the line is
    /// the owner's rather than the book's is asserted to be in the same object as the flag, because
    /// a threshold quoted without it reads as a rule.</para>
    /// </summary>
    [Fact]
    public async Task AHopelesslyOutmatchedSideIsFlaggedAndTheOneBeatingItIsNot() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = Lopsided(heroMight: 14, villainToughness: 1),
                ["runs"] = PlayTools.FewestRunsForAVerdict,
                ["seed"] = 4_100,
                ["style"] = "mano_a_mano"
            });

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var sides = answer["by_side"]!.AsArray()
                .ToDictionary(s => s!["side"]!.GetValue<string>(), s => s!, StringComparer.Ordinal);

            Assert.True(sides["villains"]["unfair"]!.GetValue<bool>(),
                "a Villain who loses every fight is not flagged: " + sides["villains"].ToJsonString());

            Assert.False(sides["heroes"]["unfair"]!.GetValue<bool>(),
                "the side winning every fight is flagged too, so the flag is not measuring "
                + "anything: " + sides["heroes"].ToJsonString());

            var threshold = answer["unfair_threshold"]!;

            Assert.Equal(PlayTools.UnfairAtOrBelow, threshold["win_rate_at_or_below"]!.GetValue<double>());
            Assert.Equal(PlayTools.FewestRunsForAVerdict, threshold["fewest_runs"]!.GetValue<int>());

            Assert.Contains("not a rule", threshold["note"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b>The flag is no answer at all below the floor, in a real report.</b>
    ///
    /// <para>The same fight, the same seeds and one run short of the owner's hundred: the rate is
    /// still there and the verdict is not, which is the difference between a figure and a claim.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ARateBelowTheFloorCarriesNoVerdict() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = Lopsided(heroMight: 14, villainToughness: 1),
                ["runs"] = PlayTools.FewestRunsForAVerdict - 1,
                ["seed"] = 4_100
            });

            var sides = answer["by_side"]!.AsArray();

            Assert.All(sides, side =>
            {
                Assert.NotNull(side!["win_rate"]);
                Assert.Null(side["unfair"]?.GetValue<bool?>());
            });
        });

    /// <summary>
    /// <b>The style argument reaches the fight, and the proof is a pool that did not move.</b>
    ///
    /// <para><c>mano_a_mano</c>'s whole claim is that nobody spends, so a report under it has to say
    /// no Resolve and no Adversity left anybody — and the control is the same fight under
    /// <c>min_max</c> on the same seeds, which does spend. An argument the SDK dropped would give
    /// two identical answers, which is exactly the failure the <c>max_pages</c> guard was written
    /// for one section further down.</para>
    /// </summary>
    [Fact]
    public async Task TheStyleReachesTheFightAndIsEchoedWithItsNote() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Under(string style) =>
                await Call(client, "run_encounters", new Dictionary<string, object?>
                {
                    ["combatants"] = TwoSides(),
                    ["runs"] = PlayTools.FewestRuns,
                    ["seed"] = 909,
                    ["style"] = style
                });

            var quiet = await Under("mano_a_mano");
            var greedy = await Under("min_max");

            Assert.Equal("mano_a_mano", quiet["style"]!["id"]!.GetValue<string>());
            Assert.Equal("min_max", greedy["style"]!["id"]!.GetValue<string>());

            Assert.NotEqual(
                quiet["style"]!["note"]!.GetValue<string>(),
                greedy["style"]!["note"]!.GetValue<string>());

            Assert.Equal(0.0, quiet["mean_adversity_spent"]!.GetValue<double>());

            Assert.All(quiet["by_side"]!.AsArray(), side =>
                Assert.Equal(0.0, side!["mean_resolve_spent"]!.GetValue<double>()));

            // The control: the same fight, the same seeds, a style that does spend.
            Assert.True(
                greedy["mean_adversity_spent"]!.GetValue<double>() > 0
                || greedy["by_side"]!.AsArray().Any(s => s!["mean_resolve_spent"]!.GetValue<double>() > 0),
                "min_max spent nothing either, so mano_a_mano's zero is not evidence about the "
                + "style argument: " + greedy["by_side"]!.ToJsonString());
        });

    /// <summary>
    /// <b>The targeting argument reaches the fight too, and the proof is who dies.</b>
    ///
    /// <para>Three opponents, one Hero, and the frailest of the three is the one
    /// <c>weakest</c> goes after and <c>strongest</c> leaves alone — so the frail one's defeat rate
    /// has to be the higher of the two under <c>weakest</c>. Both echoes are checked as well,
    /// because an argument the SDK dropped runs on the default and says so in the answer.</para>
    /// </summary>
    [Fact]
    public async Task TheTargetingReachesTheFightAndIsEchoedWithItsNote() =>
        await WithClient(async client =>
        {
            async Task<JsonNode> Going(string targeting) =>
                await Call(client, "run_encounters", new Dictionary<string, object?>
                {
                    ["combatants"] = ThreeOnOne(),
                    ["runs"] = PlayTools.FewestRunsForAVerdict,
                    ["seed"] = 5_050,
                    ["style"] = "mano_a_mano",
                    ["targeting"] = targeting
                });

            var weakest = await Going("weakest");
            var strongest = await Going("strongest");

            Assert.Equal("weakest", weakest["targeting"]!["id"]!.GetValue<string>());
            Assert.Equal("strongest", strongest["targeting"]!["id"]!.GetValue<string>());

            Assert.NotEqual(
                weakest["targeting"]!["note"]!.GetValue<string>(),
                strongest["targeting"]!["note"]!.GetValue<string>());

            static double DefeatRate(JsonNode report, string id) =>
                report["by_combatant"]!.AsArray()
                    .Single(c => string.Equals(c!["id"]!.GetValue<string>(), id, StringComparison.Ordinal))!
                    ["defeat_rate"]!.GetValue<double>();

            Assert.True(DefeatRate(weakest, "frail") > DefeatRate(strongest, "frail"),
                $"the frailest opponent went down {DefeatRate(weakest, "frail")} of the time when "
                + $"the Hero was going after the weakest and {DefeatRate(strongest, "frail")} when "
                + "they were going after the strongest, which is the wrong way round or no "
                + "difference at all — so the selector is not reaching the fight.");
        });

    /// <summary>
    /// <b>The report answers what a character is best and worst at, and the two tables reconcile
    /// with each other.</b>
    ///
    /// <para><b>Every attack somebody made was answered by somebody</b>, so one side's exchanges
    /// have to add up to the other side's answered defences — two tables built out of two different
    /// halves of the same observation, which is what makes this a check rather than a restatement.
    /// And every defeat filed under <c>defeated_by</c> has to add up to the defeat rate the report
    /// prints beside it.</para>
    /// </summary>
    [Fact]
    public async Task TheReportSaysWhatLandedAndWhatHeldAndTheTablesReconcile() =>
        await WithClient(async client =>
        {
            var runs = PlayTools.FewestRuns;

            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = runs,
                ["seed"] = 2_020,
                ["style"] = "mano_a_mano"
            });

            Assert.Equal(0, answer["defence_traits_unread"]!.GetValue<int>());

            var byCombatant = answer["by_combatant"]!.AsArray();

            long Swings(string side) => byCombatant
                .Where(c => !string.Equals(c!["side"]!.GetValue<string>(), side, StringComparison.Ordinal))
                .SelectMany(c => c!["attack_forms"]!.AsArray())
                .Sum(f => f!["exchanges"]!.GetValue<long>());

            long Answers(string side) => byCombatant
                .Where(c => string.Equals(c!["side"]!.GetValue<string>(), side, StringComparison.Ordinal))
                .SelectMany(c => c!["defences"]!.AsArray())
                .Sum(d => d!["answered"]!.GetValue<long>());

            Assert.True(Swings("heroes") > 0, "nobody attacked the Heroes, so there is nothing to reconcile.");

            Assert.Equal(Swings("heroes"), Answers("heroes"));
            Assert.Equal(Swings("villains"), Answers("villains"));

            // And what put each of them out adds up to how often they were put out.
            foreach (var combatant in byCombatant)
            {
                var filed = combatant!["defeated_by"]!.AsArray().Sum(d => d!["runs"]!.GetValue<int>());

                Assert.Equal(
                    Math.Round((double)filed / runs, 3),
                    combatant["defeat_rate"]!.GetValue<double>());
            }
        });

    /// <summary>
    /// <b>The matrix has a cell for every matchup and every style, and the seeds are derived from
    /// the one the caller gave.</b>
    ///
    /// <para>Rows are the party and each Hero alone; columns are every style this server simulates,
    /// taken from the server's own list rather than a count written here. The seed blocks are
    /// checked for being consecutive and non-overlapping, because that is the whole claim a derived
    /// seed makes: no two cells share a fight, and any one of them can be reopened with
    /// <c>run_encounters</c> on its own block.</para>
    /// </summary>
    [Fact]
    public async Task TheMatrixHasACellPerMatchupAndStyleWithDerivedSeeds() =>
        await WithClient(async client =>
        {
            const int Base = 7_000;
            var perCell = PlayTools.FewestRunsACell;

            var answer = await Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = PartyOfTwo(),
                ["runs"] = perCell,
                ["seed"] = Base,
                ["maxPages"] = 8
            });

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var styles = answer["styles"]!.AsArray().Select(s => s!.GetValue<string>()).ToList();

            Assert.Equal(StylePolicy.StyleIds.Order(StringComparer.Ordinal), styles.Order(StringComparer.Ordinal));

            var rows = answer["matrix"]!.AsArray();

            // One row for the party and one for each Hero alone.
            Assert.Equal(["cho", "felix", "party"],
                rows.Select(r => r!["matchup"]!.GetValue<string>()).Order(StringComparer.Ordinal));

            Assert.Equal(rows.Count * styles.Count, answer["cells"]!.GetValue<int>());
            Assert.Equal((long)rows.Count * styles.Count * perCell, answer["total_runs"]!.GetValue<long>());

            // A Hero alone is that Hero and the opposition, and nobody else.
            var alone = rows.Single(r => string.Equals(r!["matchup"]!.GetValue<string>(), "cho", StringComparison.Ordinal))!;

            Assert.Equal(["cho", "villain"],
                alone["combatants"]!.AsArray().Select(c => c!.GetValue<string>()).Order(StringComparer.Ordinal));

            // <b>Consecutive blocks, in the order the rows and then the columns are listed.</b>
            var blocks = rows
                .SelectMany(r => r!["cells"]!.AsArray())
                .Select(c => (First: c!["seeds"]!["first"]!.GetValue<int>(), Last: c["seeds"]!["last"]!.GetValue<int>()))
                .ToList();

            Assert.Equal(Base, blocks[0].First);

            for (var i = 0; i < blocks.Count; i++)
            {
                Assert.Equal(Base + i * perCell, blocks[i].First);
                Assert.Equal(blocks[i].First + perCell - 1, blocks[i].Last);
            }

            Assert.Equal(blocks[^1].Last, answer["seeds"]!["last"]!.GetValue<long>());

            // Every cell carries the three things a row of this table is read for.
            Assert.All(rows.SelectMany(r => r!["cells"]!.AsArray()), cell =>
            {
                Assert.Contains(cell!["style"]!.GetValue<string>(), styles, StringComparer.Ordinal);
                Assert.InRange(cell["win_rate"]!.GetValue<double>(), 0, 1);
                Assert.NotNull(cell["unfair"]?.GetValue<bool?>());
                Assert.True(cell["mean_pages"]!.GetValue<double>() > 0);
            });

            // And the guess behind each column travels with it.
            foreach (var style in styles)
                Assert.False(string.IsNullOrWhiteSpace(answer["style_notes"]![style]!.GetValue<string>()));

            // The fifth style is named as absent rather than missing.
            Assert.Contains("narrative", answer["narrative"]!.GetValue<string>(), StringComparison.Ordinal);
        });

    /// <summary>
    /// <b><c>matchups</c> narrows the rows, and the two narrow answers add up to the wide one.</b>
    /// </summary>
    [Fact]
    public async Task TheMatchupsArgumentChoosesTheRows() =>
        await WithClient(async client =>
        {
            async Task<List<string>> Rows(string? matchups)
            {
                var arguments = new Dictionary<string, object?>
                {
                    ["combatants"] = PartyOfTwo(),
                    ["runs"] = PlayTools.FewestRunsACell,
                    ["seed"] = 7_500,
                    ["maxPages"] = 6
                };

                if (matchups is not null) arguments["matchups"] = matchups;

                var answer = await Call(client, "run_matrix", arguments);

                Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

                return answer["matrix"]!.AsArray()
                    .Select(r => r!["matchup"]!.GetValue<string>())
                    .Order(StringComparer.Ordinal)
                    .ToList();
            }

            Assert.Equal(["party"], await Rows("party"));
            Assert.Equal(["cho", "felix"], await Rows("each_hero_alone"));
            Assert.Equal(["cho", "felix", "party"], await Rows("all"));
            Assert.Equal(["cho", "felix", "party"], await Rows(null));
        });

    /// <summary>Two Heroes and a Villain, which gives a matrix three rows.</summary>
    private static JsonArray PartyOfTwo() =>
    [
        Sheet("hero", "cho", "Cho", "heroes", might: 8, toughness: 5),
        Sheet("hero", "felix", "Felix", "heroes", might: 7, toughness: 5),
        Sheet("villain", "villain", "the Villain", "villains", might: 9, toughness: 6)
    ];

    /// <summary>
    /// One Hero against three, built so the weakest, the strongest and the hardest hitter are three
    /// different characters — which is what makes the target selectors distinguishable.
    /// </summary>
    private static JsonArray ThreeOnOne() =>
    [
        Sheet("hero", "hero", "the Hero", "heroes", might: 11, toughness: 8),
        Sheet("villain", "frail", "the frail one", "villains", might: 4, toughness: 1),
        Sheet("villain", "tank", "the tank", "villains", might: 4, toughness: 9),
        Sheet("villain", "sniper", "the sniper", "villains", might: 12, toughness: 3)
    ];

    /// <summary>One combatant off a bare sheet — nothing here is a legal character and nothing needs
    /// to be, because whether a character is legal is the other server's question.</summary>
    private static JsonObject Sheet(
        string kind, string id, string name, string side, int might, int toughness) => new()
    {
        ["kind"] = kind,
        ["id"] = id,
        ["side"] = side,
        ["character"] = new JsonObject
        {
            ["Name"] = name,
            ["SelectedTierId"] = "standard",
            ["AbilityRanks"] = new JsonObject
            {
                ["might"] = might, ["toughness"] = toughness, ["willpower"] = 4, ["agility"] = 4
            }
        }
    };

    /// <summary>
    /// <b>A character's defeats come back as a rate and a side's as a mean, and the two are spelled
    /// differently because a party of four goes down more than once a fight.</b>
    ///
    /// <para>The same division answered <c>3.315</c> under the key <c>rate</c> — a figure that reads
    /// exactly like a proportion and is not one, printed beside four that are. The control here is
    /// the pair: the side's figure has to be above one on this fight, and the characters' have to be
    /// proportions, or the two keys are not measuring different things.</para>
    /// </summary>
    [Fact]
    public async Task ASidesDefeatsAreAMeanAndACharactersAreARate() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = ThreeOnOne(),
                ["runs"] = PlayTools.FewestRunsForAVerdict,
                ["seed"] = 6_060,
                ["style"] = "reckless"
            });

            var villains = answer["by_side"]!.AsArray()
                .Single(s => string.Equals(s!["side"]!.GetValue<string>(), "villains", StringComparison.Ordinal))!;

            var filed = villains["defeated_by"]!.AsArray();

            Assert.True(filed.Count > 0, "nobody on the Villains' side went down, so there is nothing to file.");

            Assert.All(filed, row => Assert.Null(row!["rate"]));

            Assert.True(filed.Sum(r => r!["mean_a_run"]!.GetValue<double>()) > 1,
                "three opponents went down fewer than once a fight between them, so the case this "
                + "distinction exists for is not being reached: "
                + filed.ToJsonString());

            Assert.All(answer["by_combatant"]!.AsArray(), combatant =>
                Assert.All(combatant!["defeated_by"]!.AsArray(), row =>
                {
                    Assert.Null(row!["mean_a_run"]);
                    Assert.InRange(row["rate"]!.GetValue<double>(), 0, 1);
                }));
        });

    /// <summary>
    /// The sections of the play policy whose backticked names are answer keys rather than prose —
    /// the two this slice added, plus the styles table that names what a caller may ask for.
    /// </summary>
    private static readonly string[] SectionsNamingWhatComesBack =
    [
        "## Styles: how a fight is played",
        "## Is this fight too unfair, and what has the party no answer for"
    ];

    /// <summary>
    /// <b>Every name the play policy prints in those sections is one this server really answers
    /// with, or really accepts.</b>
    ///
    /// <para><b>The echo keys fall outside the guard that already exists, and that is the finding
    /// this is here for.</b>
    /// <see cref="EveryArgumentNameThePolicyPrintsIsSpelledTheWayTheSchemaSpellsIt"/> reads the
    /// <c>## The calls</c> section and checks spans against the tools' <em>input schemas</em>;
    /// <see cref="EveryProvenanceNameThePolicyPrintsIsOneThisServerEchoes"/> covers exactly two keys
    /// of the echoed table. Everything this slice added — <c>attack_forms</c>, <c>defences</c>,
    /// <c>defeated_by</c>, <c>unfair</c>, the style ids, the selector ids — is an answer key or an
    /// accepted value in a third place, and nothing was holding any of them to the server. That is
    /// the same fault the <c>max_pages</c> guard was written for: the document is right-looking and
    /// wrong, and a model reading it looks for a field that never arrives.</para>
    ///
    /// <para><b>Held in both directions.</b> Every span in those sections has to be something the
    /// server produces or accepts, and the keys the document undertakes to name have to still be in
    /// it — otherwise a parse that had stopped finding spans would pass in silence, which is how
    /// three of this repository's historical guards were wrong.</para>
    /// </summary>
    [Fact]
    public async Task EveryReportKeyThePolicyNamesIsOneThisServerAnswersWith() =>
        await WithClient(async client =>
        {
            var answer = await Call(client, "run_encounters", new Dictionary<string, object?>
            {
                ["combatants"] = TwoSides(),
                ["runs"] = PlayTools.FewestRunsForAVerdict,
                ["seed"] = 3_030
            });

            Assert.True(answer["ok"]!.GetValue<bool>(), answer.ToJsonString());

            var matrix = await Call(client, "run_matrix", new Dictionary<string, object?>
            {
                ["combatants"] = PartyOfTwo(),
                ["runs"] = PlayTools.FewestRunsACell,
                ["seed"] = 3_030,
                ["maxPages"] = 6
            });

            var produced = new HashSet<string>(StringComparer.Ordinal);

            Keys(answer, produced);
            Keys(matrix, produced);

            foreach (var name in StylePolicy.StyleIds) produced.Add(name);
            foreach (var name in StylePolicy.TargetingIds) produced.Add(name);
            foreach (var name in PlayTools.Policies) produced.Add(name);
            foreach (var name in PlayTools.Matchups) produced.Add(name);
            foreach (var tool in (await client.ListToolsAsync()).Select(t => t.Name)) produced.Add(tool);

            // `narrative` is the one name here the server refuses rather than answers with, which is
            // the whole point of it being in the document.
            produced.Add(StylePolicy.NarrativeStyle);

            // The two JSON literals the document quotes as values rather than as keys.
            produced.Add("null");
            produced.Add("false");

            var spans = SectionsNamingWhatComesBack.SelectMany(SpansUnder).ToList();

            // The control on the parse, before its result is compared with anything: it found the
            // names those sections are there to name.
            foreach (var named in ReportKeysThePolicyUndertakesToName)
            {
                Assert.Contains(named, spans, StringComparer.Ordinal);
            }

            var invented = spans.Where(s => !produced.Contains(s)).Order(StringComparer.Ordinal).ToList();

            Assert.True(invented.Count == 0,
                "mcp-play/PLAY-POLICY.md names " + string.Join(", ", invented)
                + ", and this server answers with no such thing — a reader is being sent looking "
                + "for a field that never arrives, or asking for a value that is refused.");
        });

    /// <summary>
    /// The names those sections exist to name — the control for the check above, which would
    /// otherwise pass in silence on a parse that had stopped finding spans.
    /// </summary>
    private static readonly string[] ReportKeysThePolicyUndertakesToName =
    [
        "style", "targeting", "unfair", "unfair_threshold", "attack_forms", "defences",
        "defeated_by", "mean_pages_survived", "defence_traits_unread", "land_rate", "hold_rate",
        "by_combatant", "by_side", "mano_a_mano", "standard", "min_max", "reckless",
        "weakest", "strongest", "highest_threat", "narrative"
    ];

    /// <summary>Every backticked lower-case name under one heading, to the next heading.</summary>
    private static List<string> SpansUnder(string heading)
    {
        var text = PlayPolicy.Text;
        var at = text.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(at >= 0,
            $"mcp-play/PLAY-POLICY.md no longer has the section \"{heading}\". Either it has "
            + "stopped describing what comes back, or this parse has stopped finding it — and in "
            + "both cases nothing holds the spellings together.");

        var from = at + heading.Length;
        var next = text.IndexOf("\n## ", from, StringComparison.Ordinal);
        var section = next < 0 ? text[from..] : text[from..next];

        return new Regex(@"`([a-z][a-z_0-9]*)`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(section)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Every key anywhere in a JSON answer, however deep.</summary>
    private static void Keys(JsonNode? node, HashSet<string> into)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o)
                {
                    into.Add(key);
                    Keys(value, into);
                }

                break;

            case JsonArray a:
                foreach (var item in a) Keys(item, into);
                break;
        }
    }

    /// <summary>
    /// The same fight with nobody in it whose <c>kind</c> is <c>hero</c> — a Villain against a Foe,
    /// which is a fight this engine resolves and a matrix cannot have rows for.
    /// </summary>
    private static JsonArray NobodyIsAHero()
    {
        var fight = TwoSides();
        fight[0]!["kind"] = "foe";
        return fight;
    }

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
