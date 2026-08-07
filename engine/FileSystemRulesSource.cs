namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Reads the rules JSON from a directory on disk. What the CLI uses, and what the csproj
/// copies beside the binary so a published build works without the repository checked out.
/// </summary>
public sealed class FileSystemRulesSource : IRulesSource
{
    private readonly string _dataRulesPath;

    public FileSystemRulesSource(string dataRulesPath) => _dataRulesPath = dataRulesPath;

    public string ReadAllText(string fileName)
    {
        var path = Path.Combine(_dataRulesPath, fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Rules file '{fileName}' was not found in '{_dataRulesPath}'. " +
                "The data/rules JSON ships beside the binary; check the build copied it.",
                path);

        return File.ReadAllText(path);
    }
}
