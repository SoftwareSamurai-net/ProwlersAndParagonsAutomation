using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Mcp;
using ProwlersAndParagonsAutomation.McpPlay;

// The encounter server, over standard input and output.
//
// **Nothing may be written to standard output except the protocol.** A stray line — a greeting, a
// warning, MSBuild's own chatter — is not a cosmetic problem: it lands in the middle of a JSON-RPC
// stream and the client drops the session with an error the person reading it cannot connect to
// anything. Everything this program says to a human goes to standard error, which clients collect
// into a log. `McpPlayStdioTests` holds that twice, by reading this source and by reading the
// stream of the built program while every tool is driven.
//
// **It is a second program rather than four more tools on the character server**, and .mcp.json
// registers it separately, published to `mcp-play-server/`. `dotnet exec` rather than plain
// `dotnet <dll>`, for the reason docs/guide/mcp-and-headless.md records: exec on a file that is
// not there exits 129 with an *empty* standard output, and the other spelling puts its "Possible
// reasons for this include" block on the stream the protocol lives on.

var arguments = CommandLine.Read(args);

if (arguments.Error is { } argumentError)
{
    Console.Error.WriteLine(argumentError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage());
    return 2;
}

if (arguments.Help)
{
    Console.Error.WriteLine(Usage());
    return 0;
}

// The character rules first: the same search the character server makes, out of the same file, so
// the two servers cannot disagree about where the rules are or about what a typo in
// PROWLERS_RULES_DIR means.
var located = RulesLocation.Find(arguments.RulesDirectory);

if (located.Directory is not { } rulesDirectory)
{
    Console.Error.WriteLine(located.Refusal);
    return 2;
}

// Then the play rules, which are the `play` folder under them. Refused rather than guessed past,
// for the reason the character rules are: a repository built for a directory that is not there
// gets as far as a connected session and then answers every question with an error.
var play = PlayRulesLocation.Find(rulesDirectory);

if (play.Directory is not { } playRulesDirectory)
{
    Console.Error.WriteLine(play.Refusal);
    return 2;
}

var tools = PlayServer.ToolsFor(rulesDirectory, playRulesDirectory);

// Fail here rather than on the first tool call, and read **everything** — both stores, every file
// of each. A repository is lazy, so a directory that holds the wrong files gets as far as a
// connected session and then answers every question with an error the client shows as a tool
// failure. One line on standard error and a non-zero exit is something a person can act on.
try
{
    _ = PlayServer.Tools(tools).ToList();
    tools.ReadEverything();
}
// ArgumentException as well as the three obvious ones: a duplicate id in a rules file throws it
// out of the lookup dictionaries rather than out of the deserializer, and that is this program's
// own data being wrong — exactly the case this check exists to catch early.
catch (Exception e) when (e is IOException or InvalidOperationException
                            or System.Text.Json.JsonException or ArgumentException)
{
    Console.Error.WriteLine(
        $"The rules in '{rulesDirectory}' and '{playRulesDirectory}' could not be read: {e.Message}");
    return 2;
}

Console.Error.WriteLine(
    $"Prowlers & Paragons encounter server, rules from '{rulesDirectory}' and '{playRulesDirectory}'.");

await using var transport = new StdioServerTransport(PlayServer.Name);
await using var server = McpServer.Create(transport, PlayServer.Options(tools));

await server.RunAsync().ConfigureAwait(false);

return 0;

static string Usage() =>
    """
    Prowlers & Paragons encounter engine, as an MCP server.

    It speaks the Model Context Protocol over standard input and output, so it is started by an
    MCP client rather than by a person. Connect it to Claude Desktop or Claude Code and run a
    fight through the rules engine; see docs/MCP-SETUP.md for the configuration.

      <directory>   Where the character rules JSON files are. The play rules are the 'play'
                    folder under that one. Defaults to the copy beside this program, then to a
                    data/rules folder in any directory above it. PROWLERS_RULES_DIR does the
                    same. A directory named either way and not found is refused rather than
                    guessed past.
      --help        This text, on standard error.

    Standard output carries the protocol and nothing else.
    """;
