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

        var txtPath  = Path.Combine(dir, baseName + ".txt");
        var jsonPath = Path.Combine(dir, baseName + ".json");

        File.WriteAllText(txtPath,
            CharacterSheetRenderer.RenderText(sheet, rules, costs, derived, validation, generatedAt),
            Encoding.UTF8);

        File.WriteAllText(jsonPath,
            CharacterSheetRenderer.RenderJson(sheet, rules, costs, derived, validation, generatedAt),
            Encoding.UTF8);

        return (txtPath, jsonPath);
    }
}
