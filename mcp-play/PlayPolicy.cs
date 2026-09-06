using System.Reflection;

namespace ProwlersAndParagonsAutomation.McpPlay;

/// <summary>
/// The play policy — <c>PLAY-POLICY.md</c>, embedded in the assembly and served verbatim by the
/// <c>combat_guide</c> tool.
///
/// <para>Exactly the arrangement <c>mcp/QuestionPolicy</c> uses, and for the same two reasons: it
/// is a file because it is the part of this slice somebody will need to read and argue with, and
/// it is embedded because a client launches this program from a directory of its own choosing and
/// a guide that is sometimes missing is worse than no guide at all.</para>
/// </summary>
public static class PlayPolicy
{
    /// <summary>
    /// The manifest name is built from the project's <c>RootNamespace</c>, which is
    /// <c>ProwlersAndParagonsAutomation.McpPlay</c> — not from its assembly name, which is
    /// <c>ProwlersAndParagons.McpPlay</c>. The two differ here exactly as they do in
    /// <c>mcp/</c>, and using the wrong one is a resource that is present and unfindable.
    /// </summary>
    private const string ResourceName = "ProwlersAndParagonsAutomation.McpPlay.PLAY-POLICY.md";

    private static string? _text;

    /// <summary>The document, verbatim.</summary>
    /// <exception cref="InvalidOperationException">The resource is not in the assembly, which
    /// means the csproj stopped embedding it. Thrown rather than shrugged: a server that answers
    /// <c>combat_guide</c> with an empty string teaches nothing, silently — and what it would
    /// stop teaching is that the engine resolves and the model narrates.</exception>
    public static string Text => _text ??= Load();

    private static string Load()
    {
        using var stream = typeof(PlayPolicy).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The play policy ({ResourceName}) is not embedded in this assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
