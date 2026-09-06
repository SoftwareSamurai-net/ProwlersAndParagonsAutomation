using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Every pointer <c>PROGRESS.md</c> holds out has to lead somewhere.
///
/// <para><b>Why this exists, measured rather than assumed.</b> An audit on 2026-09-05 read that
/// file entry by entry against the code and found <b>21 dead pointers</b>: sixteen bare
/// <c>see [the archive](docs/progress/)</c> links left behind when <c>88e8a1b</c> deleted the
/// 6,723-line file holding all 92 pre-split entries, and five <c>see the completed entry at the
/// top of this file</c> references to a section that is now a signpost rather than a place. It
/// also found two <c>### 9.</c> headings, so a reader told to see "item 9" had two entries to
/// choose from. None of that breaks a build. <c>CLAUDE.md</c>'s standing rule is that <b>a dead
/// pointer is worse than no pointer</b> — it reads as though the reasoning was written down and
/// sends the reader to the one place it is not — and this file is the first thing every agent is
/// sent to, so one stale pointer here is inherited by all of them at once.</para>
///
/// <para><b>Fixing 21 claims once buys nothing durable</b>, which is what item 23 says in as many
/// words. This is the guard that makes the fix stay fixed.</para>
///
/// <para><b>What it cannot do, said plainly because <c>CLAUDE.md</c> requires it.</b> Every check
/// below is about a pointer — a link, an anchor, a name, a number. <b>A claim with no link is
/// invisible to all of it.</b> "The visual check covers seven pages", "seven D1 migrations are
/// applied", "<c>CLAUDE.md</c> is 290 lines" are exactly the shape of the other half of that
/// audit's findings — ten factual drifts, none of which names a file, an anchor or a test — and
/// no scan of this file's text has an opinion about any of them. Those still need somebody to
/// read the file against the code. What this removes is the half that is mechanical, so the audit
/// that is left is the half that actually needs judgement.</para>
/// </summary>
public sealed class ProgressPointerTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;

    private static string ProgressPath => Path.Combine(RepoRoot, "PROGRESS.md");

    private static string Progress => File.ReadAllText(ProgressPath);

    /// <summary>
    /// Every markdown link in the file, as <c>(line, text, target)</c>.
    ///
    /// <para>Inline links only — this file uses no reference-style links, and a scan that silently
    /// covered none of them would be worse than one that says so.</para>
    /// </summary>
    private static List<(int Line, string Text, string Target)> Links()
    {
        var text = WithoutCodeSpans(Progress);
        return Regex.Matches(text, @"\[([^\]\n]*)\]\(([^)\s]+)\)", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => (
                Line: text[..m.Index].Count(c => c == '\n') + 1,
                Text: m.Groups[1].Value,
                Target: m.Groups[2].Value))
            .ToList();
    }

    /// <summary>
    /// The file with every inline code span blanked out, same length and same line breaks.
    ///
    /// <para><b>Because this file documents its own failure modes, and the guard must not fire on
    /// the documentation of the thing it forbids.</b> Item 23 quotes the dead-pointer spelling
    /// verbatim inside backticks so a reader can recognise it, and a scan over the raw text reads
    /// that quotation as a seventeenth dead link. Blanking the spans rather than deleting them
    /// keeps every offset, so the line numbers in a failure message still point at the real
    /// line.</para>
    ///
    /// <para>The cost is that a link written inside a code span is invisible here. That is the
    /// right trade: a link inside a code span is not a link, it is a picture of one.</para>
    /// </summary>
    private static string WithoutCodeSpans(string text) =>
        Regex.Replace(
            text,
            "`[^`\n]*`",
            m => new string(' ', m.Length),
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

    /// <summary>Every ATX heading in the file, as <c>(line, level, title)</c>.</summary>
    private static List<(int Line, int Level, string Title)> Headings()
    {
        var lines = File.ReadAllLines(ProgressPath);
        var found = new List<(int, int, string)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var m = Regex.Match(lines[i], @"^(#{1,6}) (.+)$", RegexOptions.None, TimeSpan.FromSeconds(5));
            if (m.Success)
                found.Add((i + 1, m.Groups[1].Value.Length, m.Groups[2].Value.Trim()));
        }

        return found;
    }

    /// <summary>
    /// GitHub's anchor slug for a heading: lower-case it, drop every character that is not a
    /// letter, a digit, a space, a hyphen or an underscore, then turn the spaces into hyphens.
    ///
    /// <para><b>The em dash is the reason this is written out rather than approximated.</b> It is
    /// dropped like any other punctuation and the spaces on either side of it survive, so
    /// <c>app — stage</c> becomes <c>app--stage</c> with two hyphens. Half the anchors in this
    /// file have a doubled hyphen in them for exactly that reason, and a slug function that
    /// collapsed runs of hyphens would call every one of them dead.
    /// <see cref="TheSlugFunctionAgreesWithTheAnchorsAlreadyInTheFile"/> is the control that says
    /// this really is GitHub's rule and not a plausible guess at it.</para>
    /// </summary>
    private static string Slug(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var kept = Regex.Replace(lowered, @"[^0-9a-z \-_]", "", RegexOptions.None, TimeSpan.FromSeconds(5));
        return kept.Replace(' ', '-');
    }

    /// <summary>
    /// <b>The control on the instrument, and every anchor assertion is worthless without it.</b> A
    /// slug function that returned the empty string for everything would make "every anchor
    /// resolves" false loudly, which is fine — but one that is subtly wrong in the same way on both
    /// sides (say, collapsing hyphen runs on the heading *and* being compared against a set built
    /// the same way) would pass while sending readers nowhere. So it is pinned against anchors that
    /// are known to work on GitHub today, doubled hyphens and all.
    /// </summary>
    [Fact]
    public void TheSlugFunctionAgreesWithTheAnchorsAlreadyInTheFile()
    {
        Assert.Equal(
            "1b-semantic-procon-constraints-are-still-unenforced",
            Slug("1b. Semantic pro/con constraints are still unenforced"));

        Assert.Equal(
            "11-answered-it-is-a-tool-for-running-and-playing-pp",
            Slug("11. Answered: it is a tool for running *and* playing P&P"));

        // The doubled hyphen, which is the whole reason this function is spelled out.
        Assert.Equal(
            "5-the-browser-payload-is-large--a-characteristic-not-a-defect",
            Slug("5. The browser payload is large — a characteristic, not a defect"));
    }

    /// <summary>
    /// Every link in the file points at something that is there: a path that exists, or an anchor
    /// that some heading in the file actually produces.
    /// </summary>
    [Fact]
    public void EveryLinkResolves()
    {
        var links = Links();

        // Positive control. A regex that stopped matching, or a file read from the wrong path,
        // yields an empty list — which satisfies "no link is dead" completely while reading none.
        Assert.True(
            links.Count >= 30,
            $"Found {links.Count} markdown links in PROGRESS.md. That file is mostly cross-references; "
            + "if the extraction has stopped matching, fix it rather than this number.");

        var anchors = Headings()
            .Select(h => Slug(h.Title))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            anchors.Count >= 20,
            $"Found {anchors.Count} headings to build anchors from. The heading scan has broken.");

        var dead = new List<string>();

        foreach (var (line, text, target) in links)
        {
            if (target.StartsWith("http", StringComparison.Ordinal)
                || target.StartsWith("mailto:", StringComparison.Ordinal))
                continue;

            if (target.StartsWith('#'))
            {
                if (!anchors.Contains(target[1..]))
                    dead.Add($"line {line}: [{text}]({target}) — no heading in this file makes that anchor");

                continue;
            }

            var path = target.Split('#')[0];
            var full = Path.Combine(RepoRoot, path.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(full) && !Directory.Exists(full))
                dead.Add($"line {line}: [{text}]({target}) — no such path");
        }

        Assert.True(
            dead.Count == 0,
            "PROGRESS.md points at things that are not there:\n  " + string.Join("\n  ", dead)
            + "\n\nA dead pointer is worse than no pointer: it reads as though the reasoning was "
            + "written down and sends the reader to the one place it is not. Repoint it, or drop "
            + "the pointer and let the claim stand on its own.");
    }

    /// <summary>
    /// An archive link names an entry, not the directory.
    ///
    /// <para><b>This is the check the audit's biggest finding needed, and a path-existence check
    /// could never have made it.</b> <c>docs/progress/</c> is a real directory, so all sixteen of
    /// the bare <c>see [the archive](docs/progress/)</c> links "resolved" perfectly while pointing
    /// at nothing in particular — the file each of them meant had been deleted. A reader following
    /// one lands on twelve files and no way to tell which sentence was theirs, which is the same
    /// cost as no pointer plus the time spent looking.</para>
    ///
    /// <para>The single exception is the signpost under <b>Completed work</b>, whose job is to
    /// name the directory: that section is a pointer rather than a place, which is what
    /// <see cref="ProgressArchiveTests"/> holds it to.</para>
    /// </summary>
    [Fact]
    public void EveryArchiveLinkNamesAnEntryRatherThanTheDirectory()
    {
        var links = Links()
            .Where(l => l.Target.StartsWith("docs/progress/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            links.Count >= 3,
            $"Found {links.Count} archive links. If the file has stopped citing the archive at all "
            + "this test is about nothing; fix the extraction rather than the number.");

        // The signpost under `## Completed work` is the exception, and it is identified by where it
        // is rather than by how it is written: its whole job is to name the directory. Everything
        // above that heading is an open item citing an argument, and an argument lives in a file.
        var completed = Headings().Where(h => h.Level == 2 && h.Title == "Completed work").ToList();

        Assert.True(
            completed.Count == 1,
            $"Found {completed.Count} `## Completed work` headings. That section is where the one "
            + "legitimate bare archive link lives, so without it this test cannot tell the signpost "
            + "from the sixteen dead pointers.");

        var signpost = completed[0].Line;

        var bare = links
            .Where(l => l.Target is "docs/progress/" or "docs/progress")
            .Where(l => l.Line < signpost)
            .Select(l => $"line {l.Line}: [{l.Text}]({l.Target})")
            .ToList();

        Assert.True(
            bare.Count == 0,
            "These links point at the archive directory rather than at an entry in it:\n  "
            + string.Join("\n  ", bare)
            + "\n\nThe entry each of them meant was in the 6,723-line pre-split file that `88e8a1b` "
            + "deleted, so the pointer survived and its target did not. Name the file that carries "
            + "the argument, or drop the pointer and let the sentence stand alone — the fix is "
            + "per-site judgement, not a find-and-replace.");
    }

    /// <summary>
    /// Nothing in the file sends a reader to a completed entry "above", "below" or "at the top of
    /// this file". There has been no such place since the archive became a directory.
    ///
    /// <para><b>What this cannot do.</b> It is anchored on one phrase, and the spelling space is
    /// not bounded by it — "see the write-up further up", "as recorded earlier" and any paraphrase
    /// walk straight through it. <c>CLAUDE.md</c> is explicit that a denylist cannot
    /// make a verdict honest, and this one is offered as the cheap catch on the phrasing that
    /// actually occurred six times, not as a guarantee. The reason there is no broken twin here is
    /// that there is no behaviour to reproduce: the subject is one document's own text.</para>
    /// </summary>
    [Fact]
    public void NoProseSendsAReaderToACompletedEntryInThisFile()
    {
        // Whitespace-normalised, because five of the six occurrences were wrapped across two lines
        // and a line-oriented grep found only three of them.
        var flat = Regex.Replace(
            WithoutCodeSpans(Progress), @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));

        // Anchored on the phrase rather than on the direction word, because the six occurrences
        // spelled the destination five ways — "at the top of this file", "above", "below", "up
        // there", and once nothing at all. What they have in common is naming a completed entry
        // and not saying which one.
        var offenders = Regex.Matches(
                flat,
                @"completed entr(?:y|ies).{0,100}",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(5))
            .Select(m => m.Value)
            .Where(v => !v.Contains("docs/progress/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "PROGRESS.md names a completed entry without saying which file it is:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nCompleted work is one file per slice in docs/progress/ and has been since the "
            + "split; there is no entry above, below or at the top of this file to read. Name the "
            + "file, or say the thing the pointer was standing in for.");
    }

    /// <summary>
    /// Every test class and test method this file names in backticks exists in one of the two
    /// <c>dotnet test</c> projects.
    ///
    /// <para><b>A named test is the strongest pointer in the file</b> — it is the difference
    /// between "this is guarded" and "somebody believed this was guarded" — and it is the one most
    /// likely to rot silently, because renaming a test is a refactor no markdown file is consulted
    /// about.</para>
    ///
    /// <para><b>What it cannot do.</b> It matches on the <c>…Tests</c> suffix, so a guard named
    /// any other way is invisible to it: the file cites <c>EveryPublishedHeroIsALegalCharacter</c>
    /// as a bare method name and this scan never sees it. And it looks for a declaration in source
    /// text rather than asking the runner, so a method that exists but carries no <c>[Fact]</c>
    /// still counts as present.</para>
    /// </summary>
    [Fact]
    public void EveryTestNameThisFileGivesExists()
    {
        var named = Regex.Matches(
                Progress,
                @"`([A-Za-z][A-Za-z0-9_]*Tests)(?:\.([A-Z][A-Za-z0-9_]*))?`",
                RegexOptions.None,
                TimeSpan.FromSeconds(5))
            .Select(m => (Class: m.Groups[1].Value, Member: m.Groups[2].Success ? m.Groups[2].Value : null))
            .Distinct()
            .ToList();

        Assert.True(
            named.Count >= 10,
            $"PROGRESS.md names {named.Count} tests. It cites its own guards constantly; if that has "
            + "stopped being true, or the extraction has broken, fix the extraction.");

        var sources = TestSources();

        Assert.True(
            sources.Length >= 50,
            $"Found {sources.Length} test source files to search. The scan is looking in the wrong "
            + "place, and would report every name in the file as missing — or, worse, none.");

        var blob = string.Join("\n", sources.Select(File.ReadAllText));

        // The control on the search itself: a name that is not there must come back missing.
        // Without this the two assertions below are satisfied by a `blob` that matches everything.
        Assert.False(
            Declares(blob, "ThisClassDoesNotExistTests", null),
            "The declaration search claims a class that has never existed is declared, so it would "
            + "find anything and prove nothing.");

        var missing = named
            .Where(n => !Declares(blob, n.Class, n.Member))
            .Select(n => n.Member is null ? n.Class : $"{n.Class}.{n.Member}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "PROGRESS.md names tests that are not in either test project: "
            + string.Join(", ", missing)
            + ". Either the guard was renamed and this file was not, or it was never written. Both "
            + "read to the next agent as coverage that is there.");
    }

    private static bool Declares(string blob, string cls, string? member)
    {
        var hasClass = Regex.IsMatch(
            blob,
            @"\b(?:class|record|struct)\s+" + Regex.Escape(cls) + @"\b",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        if (!hasClass || member is null)
            return hasClass;

        return Regex.IsMatch(
            blob,
            @"\b" + Regex.Escape(member) + @"\s*\(",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
    }

    private static string[] TestSources() =>
        [.. Directory.EnumerateFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
             .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

    /// <summary>
    /// The commit shas this file is allowed to name, and the claim each one carries.
    ///
    /// <para><b>The list is the guard.</b> The <c>Current state</c> table already carries the rule
    /// in its own prose — <i>do not name this commit's own sha here</i> — because one was written
    /// down and an amend orphaned it within the hour. A sha is the purest dead pointer there is:
    /// it looks like evidence and cannot be checked by eye.</para>
    ///
    /// <para><b>Why an allow-list rather than asking git.</b> Reachability is the check anybody
    /// would reach for first, and it cannot run where it matters: <c>build.yml</c> checks out at
    /// <c>actions/checkout</c>'s default depth of 1, so on CI every sha older than the tip is
    /// unreachable and a reachability test would fail the build on facts that are perfectly true.
    /// Making it conditional on a full clone is worse — it would pass by not running, which is the
    /// failure mode this repository has shipped four times. So the check that runs everywhere is
    /// this one, and its cost is deliberate: naming a new sha means editing a test and saying what
    /// the sha is for.</para>
    /// </summary>
    private static readonly Dictionary<string, string> AllowedShas = new(StringComparer.Ordinal)
    {
        ["8f2add6"] = "item 10: the driver commit the first CI kill-tree evidence was read against",
        ["f0c77f2"] = "item 10: the commit carrying the kill-tree fix",
        ["1613c95"] = "item 12: the commit the banner's search field landed on",
        ["9e65abc"] = "item 23: when the replay moved behind <AdminOnly>, which is what made the old headline false",
        ["88e8a1b"] = "item 23: the commit that deleted the 6,723-line pre-split archive and orphaned 21 pointers",
        ["acbc6a6"] = "item 7: the commit that implemented the token side this entry had recorded as unimplemented",
        ["0361de7"] = "item 24: the first of the three single-test palette fixes",
        ["621939f"] = "item 24: the second of them",
    };

    /// <summary>
    /// The file names no commit sha except the ones listed above, and every one listed is still in
    /// the file.
    ///
    /// <para>The second half is what keeps the list from becoming a place stale entries
    /// accumulate — an allow-list nobody prunes is the same rot one level down.</para>
    /// </summary>
    [Fact]
    public void TheOnlyShasNamedAreTheOnesThisTestAllows()
    {
        // Anchored to the whole code span, so `0007_decision_recorded.sql` and a bare `0006` are
        // not shas, and requiring one a-f so an eleven-digit Actions run id is not one either —
        // the file names several and they are not commits. A sha of nothing but digits would slip
        // through; at seven characters that is one tree in every sixteen million.
        var named = Regex.Matches(Progress, @"`([0-9a-f]{7,40})`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .Where(s => s.Any(c => c is >= 'a' and <= 'f'))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unlisted = named.Where(s => !AllowedShas.ContainsKey(s)).ToList();

        Assert.True(
            unlisted.Count == 0,
            "PROGRESS.md names commit shas this test does not allow: " + string.Join(", ", unlisted)
            + ". A sha here is a pointer nobody can check by eye and an amend can orphan in an hour "
            + "— which has happened. If the sha really is the clearest way to say it, add it to "
            + "AllowedShas with the claim it carries; otherwise name the change, not the commit.");

        var gone = AllowedShas.Keys.Where(s => !named.Contains(s, StringComparer.Ordinal)).ToList();

        Assert.True(
            gone.Count == 0,
            "These shas are allowed by this test and are no longer in PROGRESS.md: "
            + string.Join(", ", gone)
            + ". Prune them — an allow-list nobody prunes is the same rot one level down.");
    }

    /// <summary>
    /// No two entries share an item number.
    ///
    /// <para><b>This is the defect item 22 found, generalised.</b> Two <c>### 9.</c> headings —
    /// durable telemetry and visual regression — meant that "see item 9" had two answers, and
    /// twenty-one comments in the source cite items of this file by number. GitHub's own anchors
    /// hid it: the two headings have different titles, so they make different slugs and every
    /// markdown link resolved. The ambiguity is in the number, which is what the *prose* pointers
    /// and the code comments use.</para>
    /// </summary>
    [Fact]
    public void NoTwoEntriesShareAnItemNumber()
    {
        var numbered = Headings()
            .Where(h => h.Level == 3)
            .Select(h => (h.Line, Number: Regex.Match(h.Title, @"^(\d+[a-z]?)\.", RegexOptions.None, TimeSpan.FromSeconds(5))))
            .Where(x => x.Number.Success)
            .Select(x => (x.Line, Number: x.Number.Groups[1].Value))
            .ToList();

        Assert.True(
            numbered.Count >= 20,
            $"Found {numbered.Count} numbered entries. The heading scan has broken, and 'no number "
            + "is used twice' is trivially true of nothing.");

        var repeated = numbered
            .GroupBy(x => x.Number, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} at lines {string.Join(", ", g.Select(x => x.Line))}")
            .ToList();

        Assert.True(
            repeated.Count == 0,
            "PROGRESS.md uses these item numbers more than once: " + string.Join("; ", repeated)
            + ". Code comments cite items of this file by number and prose pointers say 'see item "
            + "N'; two entries with one number means both are ambiguous. Give the newer one the "
            + "next free number.");
    }
}
