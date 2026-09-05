using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The shipped play rules with one documented line substituted — <b>the broken twin, as a
/// reusable piece</b>.
///
/// <para><c>CLAUDE.md</c> records the discipline: a denylist of spellings cannot make a verdict
/// honest, so what proves a check can see a defect is running the byte-identical check against
/// something that has one. <see cref="PlayEngineTwinTests"/> uses this to flip the special effect's
/// rounding; <see cref="PlayEngineStepTests"/> uses it to flip the Glossary's book-wide rounding
/// direction and to blank a printed word the engine reads. One implementation, because two copies of
/// a guard's substitution machinery is two places for it to stop reproducing.</para>
///
/// <para><b><see cref="WithDefect"/> throws if the line it names does not occur exactly once.</b>
/// Zero means the twin has stopped reproducing its defect and would pass for the wrong reason; more
/// than one means it is not the single change it documents. Both are silent failures otherwise, and
/// both have happened to harnesses here.</para>
/// </summary>
internal static class SubstitutedPlayRules
{
    /// <summary>The five shipped files, by name.</summary>
    public static Dictionary<string, string> ShippedFiles() =>
        PlayRulesRepository.DataFileNames.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(PlayFixture.DataPath, name)),
            StringComparer.Ordinal);

    /// <summary>
    /// The shipped rules with <paramref name="find"/> replaced by <paramref name="replace"/> in
    /// <paramref name="fileName"/>, and every other file byte-identical.
    /// </summary>
    public static PlayRulesRepository With(string fileName, string find, string replace)
    {
        var files = ShippedFiles();
        files[fileName] = WithDefect(files[fileName], find, replace);

        return new PlayRulesRepository(new InMemoryRulesSource(files));
    }

    /// <summary>
    /// The text with one documented line substituted, <b>throwing if that line does not occur
    /// exactly once</b>. See this class's summary for why both directions are a throw.
    /// </summary>
    public static string WithDefect(string source, string find, string replace)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = Occurrences(source, find);

        if (count != 1)
        {
            throw new InvalidOperationException(
                $"The twin substitutes one documented line and found it {count} times: \"{find}\". "
                + "Zero means the twin has stopped reproducing its defect and would pass for the "
                + "wrong reason; more than one means it is not the single change it documents. Fix "
                + "the line this twin names, not this check.");
        }

        return source.Replace(find, replace, StringComparison.Ordinal);
    }

    /// <summary>How many times one string occurs in another, counting non-overlapping matches.</summary>
    public static int Occurrences(string source, string find)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(find);

        var count = 0;
        var at = source.IndexOf(find, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = source.IndexOf(find, at + find.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
