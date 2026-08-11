using System.Text;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Cli.Export;

/// <summary>
/// Writes the two character sheet exports to disk.
///
/// <para>Building them is <see cref="CharacterSheetRenderer"/>'s job, in the shared sheets
/// layer — the browser front end produces the same two documents and hands them to a
/// download instead. All that is left here is choosing a path and writing the bytes.</para>
/// </summary>
public sealed class CharacterSheetExporter
{
    private const string OutputDir = "output";

    /// <summary>
    /// Exports the character sheet to both .txt and .json in output/, or in
    /// <paramref name="outputDirectory"/> where one is given — the headless
    /// <c>build</c> command lets the caller say where, since it may be running
    /// anywhere and against a character it did not create.
    /// Returns (txtPath, jsonPath).
    /// </summary>
    public (string TxtPath, string JsonPath) Export(
        CharacterSheet sheet,
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived,
        ValidationResult validation,
        string projectRoot,
        string? outputDirectory = null)
    {
        var dir = outputDirectory ?? Path.Combine(projectRoot, OutputDir);
        Directory.CreateDirectory(dir);

        var generatedAt = DateTime.Now;
        var baseName    = CharacterSheetRenderer.BaseFileName(sheet, generatedAt);

        // The base name is the character's name and the time to the second, and two runs
        // inside one second silently overwrote each other — both reporting paths that then
        // held somebody else's character. A repair loop runs far faster than that.
        baseName = Unused(dir, baseName);

        var txtPath  = Path.Combine(dir, baseName + ".txt");
        var jsonPath = Path.Combine(dir, baseName + ".json");

        // Both or neither. The .json path is one character longer than the .txt, so there is a
        // character-name length at which the first write succeeds and the second does not —
        // leaving half an export on disk, under a base name that then looks taken to the next
        // run. A caller told "the exports were not written" should find that they were not.
        File.WriteAllText(txtPath,
            CharacterSheetRenderer.RenderText(sheet, rules, costs, derived, validation, generatedAt),
            Encoding.UTF8);

        try
        {
            File.WriteAllText(jsonPath,
                CharacterSheetRenderer.RenderJson(sheet, rules, costs, derived, validation, generatedAt),
                Encoding.UTF8);
        }
        catch
        {
            Delete(txtPath);
            throw;
        }

        return (txtPath, jsonPath);
    }

    /// <summary>
    /// The base name, or the first numbered variant of it whose <c>.txt</c> and <c>.json</c>
    /// are both free.
    ///
    /// <para>Both, together: a name is only usable if it can hold the whole pair, or the two
    /// halves of one character's export end up under different names.</para>
    /// </summary>
    private static string Unused(string dir, string baseName)
    {
        if (IsFree(dir, baseName)) return baseName;

        for (var n = 2; n < 1000; n++)
            if (IsFree(dir, $"{baseName}_{n}")) return $"{baseName}_{n}";

        // A thousand exports of one character in one second is not a case worth a policy;
        // falling back to the plain name overwrites, which is where this started, but only
        // after a thousand attempts to avoid it.
        return baseName;
    }

    /// <summary>
    /// Removes the half-written export. Its own failure is not worth reporting over the failure
    /// that got us here, which is on its way to the caller.
    /// </summary>
    private static void Delete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsFree(string dir, string baseName) =>
        !File.Exists(Path.Combine(dir, baseName + ".txt")) &&
        !File.Exists(Path.Combine(dir, baseName + ".json"));
}
