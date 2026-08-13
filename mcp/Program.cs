using ModelContextProtocol.Server;
using ProwlersAndParagonsAutomation.Mcp;

// The MCP server, over standard input and output.
//
// **Nothing may be written to standard output except the protocol.** A stray line — a
// greeting, a warning, MSBuild's own chatter — is not a cosmetic problem: it lands in the
// middle of a JSON-RPC stream and the client drops the session with an error the person
// reading it cannot connect to anything. Everything this program says to a human goes to
// standard error, which clients collect into a log. That is also why docs/MCP-SETUP.md tells
// a stranger to point their client at the built binary rather than at `dotnet run`.

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

var located = RulesLocation.Find(arguments.RulesDirectory);

if (located.Directory is not { } rulesDirectory)
{
    Console.Error.WriteLine(located.Refusal);
    return 2;
}

var tools = CharacterServer.ToolsFor(rulesDirectory);

// Fail here rather than on the first tool call. A repository is lazy, so a directory that
// holds the wrong files gets as far as a connected session and then answers every question
// with an error the client shows as a tool failure. One line on standard error and a
// non-zero exit is something a person can act on — and it reads *everything*, because warming
// one catalogue let a directory holding a single rules file start cleanly and then throw out
// of five of the six tools, which is the failure this check is here to prevent.
try
{
    _ = CharacterServer.Tools(tools).ToList();
    tools.ReadEverything();
}
// ArgumentException as well as the three obvious ones: a duplicate id in a rules file throws
// it out of the lookup dictionaries rather than out of the deserializer, and that is this
// program's own data being wrong — exactly the case this check exists to catch early.
catch (Exception e) when (e is IOException or InvalidOperationException
                            or System.Text.Json.JsonException or ArgumentException)
{
    Console.Error.WriteLine($"The rules in '{rulesDirectory}' could not be read: {e.Message}");
    return 2;
}

Console.Error.WriteLine($"Prowlers & Paragons MCP server, rules from '{rulesDirectory}'.");

await using var transport = new StdioServerTransport(CharacterServer.Name);
await using var server = McpServer.Create(transport, CharacterServer.Options(tools));

await server.RunAsync().ConfigureAwait(false);

return 0;

static string Usage() =>
    """
    Prowlers & Paragons character builder, as an MCP server.

    It speaks the Model Context Protocol over standard input and output, so it is started by
    an MCP client rather than by a person. Connect it to Claude Desktop or Claude Code and
    describe a character; see docs/MCP-SETUP.md for the two configuration snippets.

      <directory>   Where the rules JSON files are. Defaults to the copy beside this
                    program, then to a data/rules folder in any directory above it.
                    PROWLERS_RULES_DIR does the same. A directory named either way and
                    not found is refused rather than guessed past.
      --help        This text, on standard error.

    Standard output carries the protocol and nothing else.
    """;
