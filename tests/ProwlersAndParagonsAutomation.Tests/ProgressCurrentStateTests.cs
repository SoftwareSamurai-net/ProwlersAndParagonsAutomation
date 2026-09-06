using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The <c>Current state</c> table names what measures a figure instead of quoting the figure.
///
/// <para><b>This is the table, of everything in this repository, that concurrent branches
/// actually collide on.</b> Measured on 2026-09-05: of the 201 commits that have ever touched
/// <c>PROGRESS.md</c>, <b>122 touch that 23-line table</b> — 61% of all churn on the file,
/// concentrated in about 1.5% of its lines. The <c>Remaining work</c> preamble, which looked like
/// the obvious culprit, accounts for 15. Two branches editing two different <c>###</c> items merge
/// clean; two branches editing this table are odds-on to conflict, and a re-push costs a
/// ~19-minute CI job.</para>
///
/// <para><b>The fix is not a restructure, and the <c>Tests</c> row is the proof.</b> That row used
/// to carry five figures in prose, went wrong four separate ways — summands that did not add up, a
/// count read off CI against a stale local baseline, the same again a slice later, and the row
/// rewritten on a branch while <c>main</c> rewrote it too — and now names
/// <c>./scripts/count-tests.sh</c> instead. The rule it implies is the one this test enforces:
/// <b>a cell whose content is a measured figure should name the command that measures it, rather
/// than quoting the answer.</b> A figure that moves when the <em>data</em> moves is fine and stays
/// — <c>Powers: 141 entries</c> is exactly what that row is for. A figure that moves when somebody
/// <em>deploys</em>, or re-runs a scanner, or adds a test, is a fact living in another process,
/// written down where it cannot be re-derived.</para>
///
/// <para><b>What this cannot do.</b> It matches shapes, and the shape space is not bounded by the
/// three below: a count spelled a way this does not know — "a handful of", a figure inside a
/// sentence about something else, a state described without a number at all — walks straight
/// through. It is the cheap catch on the three shapes that actually went stale here, not a proof
/// that the table is true. Nothing checks a cell against reality; that is what the pointer in the
/// cell is for, and why the cell has to carry one.</para>
/// </summary>
public sealed class ProgressCurrentStateTests
{
    private static string ProgressPath => Path.Combine(RulesFixture.RepoRoot, "PROGRESS.md");

    /// <summary>
    /// The rows of the <c>Current state</c> table, as <c>(line, label, cell)</c>.
    ///
    /// <para>Bounded by the heading above it and the next top-level heading or rule below, so a
    /// table added elsewhere in the file is not silently swept in — and so a rename of the heading
    /// empties this rather than quietly matching something else, which the positive control
    /// below is there to notice.</para>
    /// </summary>
    private static List<(int Line, string Label, string Cell)> Rows()
    {
        var lines = File.ReadAllLines(ProgressPath);
        var start = Array.FindIndex(lines, l => l.StartsWith("## Current state", StringComparison.Ordinal));

        if (start < 0)
            return [];

        var rows = new List<(int, string, string)>();

        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal))
                break;

            if (!line.StartsWith('|'))
                continue;

            var cells = line.Split('|');

            // `| | |` is the header and `|---|---|` its rule; a row is a label and one cell.
            if (cells.Length < 4)
                continue;

            var label = cells[1].Trim();
            var cell = string.Join('|', cells[2..^1]).Trim();

            if (label.Length == 0 || label.All(c => c == '-'))
                continue;

            rows.Add((i + 1, label, cell));
        }

        return rows;
    }

    /// <summary>
    /// The shapes of figure that have gone stale in this table, each with what it should be
    /// replaced by.
    ///
    /// <para><b>Three judgements are baked into these patterns and each is deliberate.</b></para>
    ///
    /// <para><b>"Zero" is not a counted figure.</b> <c>Zero warnings at CI strictness</c> is a
    /// standard the build enforces on every run, not a measurement somebody took once — the
    /// difference is whether anything fails when it stops being true.</para>
    ///
    /// <para><b>Chapters and suites are excluded from the noun list.</b> The book has ten chapters
    /// and that is a fact about a printed object; the count of test suites is structural, moves
    /// about once a year, and is the subject of the <c>Tests</c> row that this whole rule is
    /// modelled on. Tests, migrations, sections and checks are none of those: every one of them is
    /// a number that moves under somebody else's hand between two edits of this file.</para>
    ///
    /// <para><b>Only the stale spelling of "pending" is refused.</b> "applies pending D1
    /// migrations" describes the mechanism and is exactly what the cell should say; "0006 is
    /// pending" was a reading of a live database, and it stopped being true the moment somebody
    /// deployed.</para>
    /// </summary>
    private static readonly (string Name, Regex Pattern, string Instead)[] WentStale =
    [
        (
            "a counted figure",
            new Regex(
                @"\b(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+"
                + @"(?:\w+\s+){0,2}?(?:tests?|migrations?|sections?|checks?|warnings?)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled,
                TimeSpan.FromSeconds(5)),
            "name the command, script or workflow that counts them"),
        (
            "a commit sha",
            new Regex(@"`(?=[0-9a-f]{7,40}`)[0-9a-f]*[a-f][0-9a-f]*`",
                RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
            "name the change rather than the commit — an amend orphaned one of these within the hour"),
        (
            "a pending state",
            new Regex(@"\b(?:is|are|was|were)\s+(?:still\s+)?pending\b|\b\d+\s+pending\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5)),
            "say where the question is asked — the deploy asks it before every upload"),
    ];

    /// <summary>
    /// <b>The positive control, and every assertion here is an absence without it.</b> A renamed
    /// heading, a table converted to a list, or a row split off elsewhere all yield no rows — and
    /// "no cell carries a stale figure" is perfectly true of no cells. It also pins the model the
    /// rule is derived from: the <c>Tests</c> row still names the script that counts them, which
    /// is the shape every converted cell was converted into.
    /// </summary>
    [Fact]
    public void TheTableIsThereAndTheTestsRowStillPointsAtWhatCountsIt()
    {
        var rows = Rows();

        Assert.True(
            rows.Count >= 10,
            $"Found {rows.Count} rows under `## Current state`. CLAUDE.md sends every reader to this "
            + "file for that table; if it has been renamed or restructured, fix this extraction "
            + "rather than lowering the number — every other assertion in this class is about "
            + "nothing until it finds rows.");

        var tests = rows.SingleOrDefault(r => r.Label == "Tests");

        Assert.True(
            tests.Label is not null,
            "The `Tests` row is gone from the Current state table. It is the row this whole rule is "
            + "modelled on — the one that stopped quoting five figures and started naming the "
            + "script that measures them.");

        Assert.Contains("./scripts/count-tests.sh", tests.Cell, StringComparison.Ordinal);

        // And the thing it points at is really there, so the model row is not itself a dead
        // pointer — which is the failure this table has already had in four other cells.
        Assert.True(
            File.Exists(Path.Combine(RulesFixture.RepoRoot, "scripts", "count-tests.sh")),
            "The Tests row names ./scripts/count-tests.sh and that script is not there.");
    }

    /// <summary>
    /// No cell quotes a figure that lives in somebody else's process.
    ///
    /// <para>Break it by pasting a Qodana count back into the <c>Static analysis</c> cell, or a
    /// migration total into <c>Accounts</c>, and watching this go red naming the row.</para>
    /// </summary>
    [Fact]
    public void NoCellQuotesAFigureInsteadOfNamingWhatMeasuresIt()
    {
        var rows = Rows();

        Assert.NotEmpty(rows);

        // The control on the instrument. Three patterns that match nothing would satisfy the
        // assertion below completely — which is the shape of guard failure this repository has
        // shipped four times — so each is first watched to fire on the cell text that produced it.
        var provocations = new (string Shape, string Text)[]
        {
            ("a counted figure", "**All seven D1 migrations are applied to the remote database.**"),
            ("a counted figure", "five checks with a positive control each and a deliberately-broken twin"),
            ("a commit sha", "On the deploy of `a978806` the gate read one pending file"),
            ("a pending state", "This row said **0006 is pending** and was right when written"),
        };

        foreach (var (shape, text) in provocations)
        {
            var pattern = WentStale.Single(w => w.Name == shape).Pattern;

            Assert.True(
                pattern.IsMatch(text),
                $"The `{shape}` pattern no longer matches the text it was written for: \"{text}\". "
                + "It would pass over every row and prove nothing.");
        }

        // And the other way: the figures this table is *supposed* to keep must not match, or the
        // rule collapses into "no numbers in the table", which is not what item 22 argues for.
        foreach (var kept in new[]
                 {
                     "141 entries, all mechanically verified against Ch.2 pp.21–48",
                     "**Five suites, and the figures are not written down here.**",
                     "All ten chapters of the printed text are extracted into `data/rulebook/`",
                     "Zero warnings at CI strictness, which the build enforces rather than records.",
                 })
        {
            var hit = WentStale.FirstOrDefault(w => w.Pattern.IsMatch(kept));

            Assert.True(
                hit.Name is null,
                $"The `{hit.Name}` pattern fires on a figure this table is meant to keep: \"{kept}\". "
                + "A count of entries moves when the data moves, which is the point of the row; the "
                + "rule is about figures that move under somebody else's hand.");
        }

        var faults = (from row in rows
                      from shape in WentStale
                      let m = shape.Pattern.Match(row.Cell)
                      where m.Success
                      select $"line {row.Line}, {row.Label}: {shape.Name} — \"{m.Value.Trim()}\" — {shape.Instead}")
            .ToList();

        Assert.True(
            faults.Count == 0,
            "The Current state table quotes figures that live in another process:\n  "
            + string.Join("\n  ", faults)
            + "\n\n122 of the 201 commits that have touched PROGRESS.md touch this 23-line table, "
            + "which is 61% of the file's churn in 1.5% of its lines — and every one of those "
            + "commits is somebody transcribing a number they measured somewhere else. Name what "
            + "measures it, the way the Tests row names ./scripts/count-tests.sh, and keep the "
            + "sentence that says what the row is.");
    }
}
