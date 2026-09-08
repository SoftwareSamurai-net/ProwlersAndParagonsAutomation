using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.McpPlay;

/// <summary>
/// The encounter server: which tools exist, what they are called on the wire, and what the client
/// is told before anybody calls anything.
///
/// <para>Separate from <c>Program.cs</c> for the reason <c>CharacterServer</c> is: so it can be
/// driven by a test over a pair of streams rather than over a process's standard input and
/// output.</para>
/// </summary>
public static class PlayServer
{
    /// <summary>
    /// The wire name of the server itself. <b>Not the character server's</b>: a client
    /// registering both sees two entries, and a name collision would leave one of them
    /// unreachable with nothing to say why.
    /// </summary>
    public const string Name = "prowlers-and-paragons-play";

    /// <summary>
    /// The wire names. Set here rather than taken from the method names so that renaming a C#
    /// method cannot rename a tool a stranger's client is configured against — and the tests
    /// assert the literal strings, because a constant compared with itself proves nothing about a
    /// contract somebody else has written down.
    /// </summary>
    public const string CombatGuideTool = "combat_guide";
    public const string StartEncounterTool = "start_encounter";
    public const string TakeTurnTool = "take_turn";
    public const string RunEncountersTool = "run_encounters";
    public const string RunMatrixTool = "run_matrix";

    /// <summary>
    /// What the client is told at the start of the session. Short on purpose — the policy is long
    /// and lives behind <c>combat_guide</c>. The one thing that has to be true before the first
    /// tool call is who decides: the engine rolls, counts and resolves, and the model says what it
    /// looked like.
    /// </summary>
    private const string Instructions =
        "Runs Prowlers & Paragons Ultimate Edition encounters through the rules engine. Call "
        + "combat_guide first. The engine resolves and you narrate: every answer carries ledger "
        + "lines naming the rule applied and the page it is printed on, and you must never state a "
        + "success count, a damage figure, a Health total or an outcome that is not in one of "
        + "them. Costing and validating a character is the other server's job "
        + "(prowlers-and-paragons); nothing here decides whether a character is legal.";

    public static McpServerOptions Options(PlayTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = Name,
                Title = "Prowlers & Paragons encounter engine",
                Version = typeof(PlayServer).Assembly.GetName().Version?.ToString() ?? "1.0.0"
            },
            ServerInstructions = Instructions,
            ToolCollection = []
        };

        foreach (var tool in Tools(tools))
            options.ToolCollection.Add(tool);

        return options;
    }

    public static IEnumerable<McpServerTool> Tools(PlayTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        yield return McpServerTool.Create(tools.CombatGuide, Read(CombatGuideTool));

        // <b>Not read-only, and that is not a formality.</b> start_encounter and take_turn change
        // state this server holds: a second identical call to start_encounter opens a second
        // fight, and a second take_turn takes a second turn. Saying otherwise is what lets a
        // client replay a call it thinks was free, and a replayed turn is a fight that quietly
        // went differently from the one in the transcript.
        yield return McpServerTool.Create(tools.StartEncounter, Changes(StartEncounterTool));
        yield return McpServerTool.Create(tools.TakeTurn, Changes(TakeTurnTool));

        // run_encounters holds nothing and leaves nothing behind — it is a measurement, and the
        // same arguments give the same answer because the seeds are the caller's. run_matrix is
        // the same thing across every style and every matchup at once, and is read-only for the
        // same reason.
        yield return McpServerTool.Create(tools.RunEncounters, Read(RunEncountersTool));
        yield return McpServerTool.Create(tools.RunMatrix, Read(RunMatrixTool));
    }

    /// <summary>A tool that reads and has no effect a second call would repeat.</summary>
    private static McpServerToolCreateOptions Read(string name) => new()
    {
        Name = name,
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    };

    /// <summary>
    /// A tool that moves this server's own state on. Not destructive — nothing is lost and no file
    /// is written — but not idempotent either, because the whole point of a turn is that taking it
    /// twice is two turns.
    /// </summary>
    private static McpServerToolCreateOptions Changes(string name) => new()
    {
        Name = name,
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false
    };

    /// <summary>
    /// Everything a host needs, wired by hand with both engines' own types.
    /// </summary>
    /// <param name="rulesDirectory">The character rules — <c>data/rules</c>.</param>
    /// <param name="playRulesDirectory">The play rules — <c>data/rules/play</c>.</param>
    public static PlayTools ToolsFor(string rulesDirectory, string playRulesDirectory)
    {
        var rules = new RulesRepository(rulesDirectory);
        var derived = new DerivedStatsCalculator(rules);
        var play = new PlayRulesRepository(new FileSystemRulesSource(playRulesDirectory));

        return new PlayTools(rules, derived, play);
    }
}
