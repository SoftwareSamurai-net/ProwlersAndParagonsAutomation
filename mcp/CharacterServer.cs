using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>
/// The server: which tools exist, what they are called on the wire, and what the client is
/// told about the server before anybody calls anything.
///
/// <para>Separate from <c>Program.cs</c> so it can be driven by a test over a pair of
/// streams rather than over a process's standard input and output. The wizard has no harness
/// for exactly the opposite reason, and it is the gap this project keeps paying for.</para>
/// </summary>
public static class CharacterServer
{
    public const string Name = "prowlers-and-paragons";

    /// <summary>
    /// The wire names. They are set here rather than taken from the method names so that
    /// renaming a C# method cannot rename a tool a stranger's client is configured against —
    /// and the tests assert the literal strings, because a constant compared with itself
    /// proves nothing about a contract somebody else has written down.
    ///
    /// <para>The order they are declared in is not the order a client lists them in: the
    /// collection a server keeps its tools in does not promise one.</para>
    /// </summary>
    public const string CreationGuideTool = "creation_guide";
    public const string ListOptionsTool = "list_options";
    public const string SearchPowersTool = "search_powers";
    public const string PowerDetailTool = "power_detail";
    public const string CheckCharacterTool = "check_character";
    public const string CharacterSheetTool = "character_sheet";

    /// <summary>
    /// What the client is told at the start of the session. Short on purpose — the policy is
    /// long and lives behind <c>creation_guide</c>, and instructions a client shows in full
    /// crowd out the conversation. The one thing that has to be true before the first tool
    /// call is who decides: the model proposes a character and the engine prices and judges
    /// it, never the other way round.
    /// </summary>
    private const string Instructions =
        "Builds Prowlers & Paragons Ultimate Edition characters from a description. "
        + "Call creation_guide first: it holds the two or three questions worth asking and "
        + "the JSON shape a character takes. You propose; the engine decides. Never state a "
        + "Hero Point cost, a derived stat or that a character is legal unless "
        + "check_character said so — the arithmetic is not guessable, and a plausible number "
        + "is worse than none.";

    public static McpServerOptions Options(CharacterTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = Name,
                Title = "Prowlers & Paragons character builder",
                Version = typeof(CharacterServer).Assembly.GetName().Version?.ToString() ?? "1.0.0"
            },
            ServerInstructions = Instructions,
            ToolCollection = []
        };

        foreach (var tool in Tools(tools))
            options.ToolCollection.Add(tool);

        return options;
    }

    public static IEnumerable<McpServerTool> Tools(CharacterTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        yield return McpServerTool.Create(tools.CreationGuide,   Named(CreationGuideTool));
        yield return McpServerTool.Create(tools.ListOptions,     Named(ListOptionsTool));
        yield return McpServerTool.Create(tools.SearchPowers,    Named(SearchPowersTool));
        yield return McpServerTool.Create(tools.PowerDetail,     Named(PowerDetailTool));
        yield return McpServerTool.Create(tools.CheckCharacter,  Named(CheckCharacterTool));
        yield return McpServerTool.Create(tools.CharacterSheetText, Named(CharacterSheetTool));
    }

    private static McpServerToolCreateOptions Named(string name) => new()
    {
        Name = name,

        // Every tool here reads: none of them writes a file, changes anything on the machine,
        // or has an effect a second call would repeat. Saying so is what lets a client run one
        // without stopping to ask, and a character builder that needs approval per lookup is
        // not a conversation.
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false
    };

    /// <summary>
    /// Everything a host needs, wired the way the CLI wires it: by hand, with the engine's
    /// own types.
    /// </summary>
    public static CharacterTools ToolsFor(string rulesDirectory)
    {
        var rules     = new RulesRepository(rulesDirectory);
        var costs     = new CostCalculator(rules);
        var derived   = new DerivedStatsCalculator(rules);
        var validator = new CharacterValidator(rules, costs, derived);

        return new CharacterTools(rules, costs, derived, validator);
    }
}
