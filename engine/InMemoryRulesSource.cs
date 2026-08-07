namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Serves the rules JSON from strings already in memory.
///
/// <para>This is what a browser build uses. Blazor WebAssembly cannot read a filesystem, so
/// the host fetches every file in <see cref="RulesRepository.DataFileNames"/> over HTTP at
/// startup — asynchronously, which is fine there — and hands the results to the engine,
/// which stays synchronous.</para>
///
/// <para>Also handy in tests that want a deliberately broken or trimmed rules set without
/// writing files.</para>
/// </summary>
public sealed class InMemoryRulesSource : IRulesSource
{
    private readonly IReadOnlyDictionary<string, string> _files;

    /// <param name="files">Keyed by file name, e.g. <c>powers.json</c>.</param>
    public InMemoryRulesSource(IReadOnlyDictionary<string, string> files) => _files = files;

    public string ReadAllText(string fileName) =>
        _files.TryGetValue(fileName, out var json)
            ? json
            : throw new FileNotFoundException(
                  $"Rules file '{fileName}' was not supplied. " +
                  $"Available: {(_files.Count == 0 ? "(none)" : string.Join(", ", _files.Keys.Order()))}. " +
                  "A host loading the rules itself must supply every name in " +
                  $"{nameof(RulesRepository)}.{nameof(RulesRepository.DataFileNames)}.",
                  fileName);
}
