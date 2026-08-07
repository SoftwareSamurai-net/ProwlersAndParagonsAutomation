namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Where <see cref="RulesRepository"/> gets the rules JSON from.
///
/// <para>The repository used to call <c>File.ReadAllText</c> directly, which tied the engine
/// to a filesystem. A browser has none: WebAssembly fetches over HTTP, and that is
/// asynchronous. This interface is the seam between the two.</para>
///
/// <para>It is deliberately <b>synchronous</b>. Making it async would push <c>await</c> through
/// every lazy collection on the repository, and from there into <c>CostCalculator</c> and
/// <c>CharacterValidator</c> — turning a pure, instantly-callable rules engine into an async
/// one for no gain. A host that can only load asynchronously does so once at startup and
/// hands the results over as an <see cref="InMemoryRulesSource"/>. Fetching is the host's
/// problem; answering questions about the rules is the engine's.</para>
/// </summary>
public interface IRulesSource
{
    /// <summary>
    /// The contents of one rules file, named as it appears in <c>data/rules</c> — for
    /// example <c>powers.json</c>. Throws if the file is not available; a missing rules
    /// file is a broken deployment, not a case to handle.
    /// </summary>
    string ReadAllText(string fileName);
}
