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
        "combatants", "table", "challengeLevel", "seed", "openingRange",
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
