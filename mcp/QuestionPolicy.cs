using System.Reflection;

namespace ProwlersAndParagonsAutomation.Mcp;

/// <summary>
/// The question policy — <c>QUESTION-POLICY.md</c>, embedded in the assembly and served
/// verbatim by the <c>creation_guide</c> tool.
///
/// <para><b>It is a file rather than a string literal because it is the part of this slice
/// somebody will need to read and argue with</b>, and a document nobody can find gets
/// reinvented. It is embedded rather than read from disk because a client launches this
/// program from a directory of its own choosing, and a guide that is sometimes missing is
/// worse than no guide at all.</para>
/// </summary>
public static class QuestionPolicy
{
    /// <summary>
    /// The manifest name is built from the project's <c>RootNamespace</c>, which is
    /// <c>ProwlersAndParagonsAutomation.Mcp</c> — not from its assembly name, which is
    /// <c>ProwlersAndParagons.Mcp</c>. The two differ in this repository, and using the wrong
    /// one is a resource that is present and unfindable.
    /// </summary>
    private const string ResourceName = "ProwlersAndParagonsAutomation.Mcp.QUESTION-POLICY.md";

    private static string? _text;

    /// <summary>The document, verbatim.</summary>
    /// <exception cref="InvalidOperationException">The resource is not in the assembly, which
    /// means the csproj stopped embedding it. Thrown rather than shrugged: a server that
    /// answers <c>creation_guide</c> with an empty string teaches nothing, silently.</exception>
    public static string Text => _text ??= Load();

    private static string Load()
    {
        using var stream = typeof(QuestionPolicy).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The question policy ({ResourceName}) is not embedded in this assembly.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
