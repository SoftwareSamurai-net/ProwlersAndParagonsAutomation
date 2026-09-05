using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The account of finished work is a directory of files, and this is what stops it becoming a
/// section again.
///
/// <para><b>The problem it exists for is a merge conflict, not a style.</b> Every slice used to
/// append its entry at the head of <c>PROGRESS.md</c>'s <b>Completed work</b> — the same anchor,
/// in the same file, every time — so any two branches open at once conflicted on bytes neither had
/// anything to do with. One slice rebased five times in an afternoon and every conflict was that
/// heading or the <c>| Tests |</c> row above it. A file per slice cannot collide.</para>
///
/// <para><b>So the thing to hold is that entries stay out of <c>PROGRESS.md</c>.</b> Nothing stops
/// somebody adding one back except this test, and the pull that would do it is real: the section
/// heading is still there, naming where the entries went.</para>
/// </summary>
public sealed class ProgressArchiveTests
{
    private static string Root => RulesFixture.RepoRoot;

    private static string ArchiveDirectory => Path.Combine(Root, "docs", "progress");

    private static string Progress => File.ReadAllText(Path.Combine(Root, "PROGRESS.md"));

    private static string[] Entries =>
        [.. Directory.EnumerateFiles(ArchiveDirectory, "*.md")
             .Where(f => Path.GetFileName(f) != "README.md")
             .Order(StringComparer.Ordinal)];

    /// <summary>
    /// <b>The positive control, and every other test here is an absence without it.</b> A
    /// directory that had been deleted, moved or emptied would satisfy "no entry has a bad title"
    /// and "PROGRESS.md carries no entries" perfectly.
    /// </summary>
    [Fact]
    public void TheArchiveIsThereAndHasSomethingInIt()
    {
        Assert.True(Directory.Exists(ArchiveDirectory),
            "docs/progress/ is gone, so every other assertion in this file is about nothing.");

        Assert.True(File.Exists(Path.Combine(ArchiveDirectory, "README.md")),
            "docs/progress/README.md is what tells the next person the convention. Without it the "
            + "directory is a pile of files and the section grows back.");

        Assert.NotEmpty(Entries);
    }

    /// <summary>
    /// <b>The rule the split exists to enforce.</b> <c>PROGRESS.md</c> keeps the heading, because
    /// it says where the work went; what it must not keep is entries under it, which is the shared
    /// anchor every concurrent branch collided on.
    /// </summary>
    [Fact]
    public void ProgressKeepsNoCompletedEntriesOfItsOwn()
    {
        var text = Progress;

        // **At the start of a line, not anywhere in the text.** A bare IndexOf anchored on the
        // first *mention* of the heading rather than the heading, and item 22 mentions it in an
        // inline code span while arguing about where new items get appended. The section then ran
        // from that sentence to the real heading and swallowed every entry in between, reporting
        // an open item as a completed one. Any entry that discusses this document's own structure
        // would have done the same.
        var match = Regex.Match(text, "^## Completed work", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        var start = match.Success ? match.Index : -1;

        Assert.True(start >= 0,
            "PROGRESS.md no longer has a Completed work heading, so this test is looking for "
            + "entries in a section that does not exist and would pass whatever was there.");

        // To the next top-level heading, or the end. Only this section is the subject: the open
        // items above it are `### 12. …` headings and are exactly where they belong.
        var rest = text[start..];
        var next = rest.IndexOf("\n## ", StringComparison.Ordinal);
        var section = next >= 0 ? rest[..next] : rest;

        var entries = Regex.Matches(section, "^### (.+)$", RegexOptions.Multiline, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(entries.Count == 0,
            "PROGRESS.md's Completed work section has grown entries again: "
            + string.Join("; ", entries)
            + ". They belong in docs/progress/, one file per slice — see that directory's README. "
            + "A shared anchor here is a merge conflict between every two concurrent branches.");
    }

    /// <summary>
    /// Each file opens by saying what it is. A slug in a file name is not a title, and an entry
    /// nobody titled is one nobody can find.
    /// </summary>
    [Fact]
    public void EveryEntryOpensWithATitle()
    {
        foreach (var file in Entries)
        {
            var first = File.ReadLines(file).FirstOrDefault(l => l.Trim().Length > 0);

            Assert.True(first?.StartsWith("# ", StringComparison.Ordinal) == true,
                $"{Path.GetFileName(file)} does not open with an `# ` title — it starts "
                + $"\"{first ?? "(empty file)"}\".");
        }
    }

    /// <summary>
    /// <b>The file name carries the date, and that is what orders the directory.</b> There is no
    /// index — an index line is one more shared anchor, which is the thing this whole arrangement
    /// removes — so the name is the only ordering there is.
    ///
    /// <para>Two slices finishing on one day write two slugs and merge clean. Two slices choosing
    /// the <em>same</em> slug on one day is a real collision, and reads as one.</para>
    /// </summary>
    [Fact]
    public void EveryEntryIsNamedByDateAndSlug()
    {
        foreach (var file in Entries)
        {
            var name = Path.GetFileNameWithoutExtension(file);

            Assert.Matches(@"^\d{4}-\d{2}-\d{2}-[a-z0-9]+(-[a-z0-9]+)*$", name);
        }
    }

    /// <summary>
    /// <b>The pointer in <c>PROGRESS.md</c> names the directory.</b> Otherwise the section reads
    /// as work that was deleted rather than work that moved — and somebody looking for the
    /// reasoning behind a finished decision has nowhere to go.
    /// </summary>
    [Fact]
    public void ProgressSaysWhereTheWorkWent()
    {
        Assert.Contains("docs/progress/", Progress, StringComparison.Ordinal);
    }
}
