using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Tools.RulebookExtractor;
using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><c>data/rulebook/</c> is the whole book, and it is not <c>data/rules/</c>.</b>
///
/// <para>The two stores answer different questions and must not be confused. <c>data/rules/</c>
/// is the <em>mechanics</em> a character is built from — structured, verified entry by entry
/// against the page, and the only thing the engine reads. <c>data/rulebook/</c> is the
/// <em>text</em>, extracted from the publisher's PDF chapter by chapter, so a player can be
/// shown what a Power actually says. Nothing in the engine reads it, no cost comes from it, and
/// a disagreement between the two is always resolved in favour of <c>data/rules/</c>.</para>
///
/// <para><b>It is deliberately not part of the browser payload.</b> The web csproj copies
/// <c>data/rules</c> and <c>data/transcripts</c> into <c>wwwroot</c> and nothing else, so this
/// corpus is not served by the deployed site. That is a decision waiting on the account-gated
/// reader, not an oversight — see <c>docs/RULEBOOK-COVERAGE.md</c>.</para>
///
/// <para><b>The first version of this file checked shape and almost nothing else</b>, and the
/// corpus it was guarding was materially wrong: every chapter opening was scrambled, 135 sections
/// held no text at all, 83 carried a doubled page number in mid-sentence, and Chapter 9 ended
/// with eighteen sections scraped off the blank Hero Sheet form. Every one of those passed. The
/// tests below are the ones that would not have. Regenerate with
/// <c>dotnet run --project tools/RulebookExtractor -- &lt;pdf&gt; data/rulebook</c>.</para>
/// </summary>
public sealed class RulebookCorpusTests
{
    private static string CorpusPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    // Named Number rather than Chapter because a record member may not share its type's name;
    // the JSON key is "chapter", so it is bound explicitly.
    private sealed record Chapter(
        [property: System.Text.Json.Serialization.JsonPropertyName("chapter")] int Number,
        string Title, int[] PrintedPages, string SourceRef, Section[] Sections);

    private sealed record Section(string Heading, int PrintedPage, string Text);

    // Cached rather than constructed per call: CA1869, which is an error under
    // ContinuousIntegrationBuild and so does not show up in a local `dotnet test`.
    private static readonly JsonSerializerOptions SnakeCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static Chapter[] All() =>
        Directory.GetFiles(CorpusPath, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<Chapter>(File.ReadAllText(f), SnakeCase)
                ?? throw new InvalidOperationException($"{f} is not readable as a chapter."))
            .ToArray();

    [Fact]
    public void EveryChapterOfTheBookIsPresentAndParses()
    {
        var chapters = All();

        Assert.Equal(10, chapters.Length);
        Assert.Equal(Enumerable.Range(0, 10), chapters.Select(c => c.Number));

        Assert.All(chapters, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Title));
            Assert.NotEmpty(c.Sections);
            Assert.Contains("Ultimate Edition", c.SourceRef, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The chapters tile the book from the Introduction to the end of Chapter 9 with no gap and
    /// no overlap, apart from the one page the book itself skips between chapters.
    ///
    /// <para><b>Both halves are asserted, because the first version only checked the gap.</b> It
    /// compared <c>From - previous To &lt;= 2</c>, which any negative difference also satisfies —
    /// so a chapter could claim three other chapters' pages and pass, which in turn made the
    /// in-range check on every one of its sections vacuous.</para>
    ///
    /// <para>Chapter 9's text ends on printed 188. Printed 189 is the blank Hero Sheet form: it
    /// is a form, not prose, and extracting it produced eighteen sections of field labels with
    /// nothing under them.</para>
    /// </summary>
    [Fact]
    public void TheChaptersCoverThePrintedBookWithoutAGapOrAnOverlap()
    {
        var ranges = All().Select(c => (c.Number, From: c.PrintedPages[0], To: c.PrintedPages[1]))
                          .OrderBy(r => r.From)
                          .ToList();

        Assert.Equal(5, ranges[0].From);
        Assert.Equal(188, ranges[^1].To);

        Assert.All(ranges, r => Assert.True(r.To >= r.From,
            $"Ch.{r.Number} ends before it starts: pp.{r.From}-{r.To}."));

        for (var i = 1; i < ranges.Count; i++)
        {
            var step = ranges[i].From - ranges[i - 1].To;

            Assert.True(step >= 1,
                $"Ch.{ranges[i].Number} starts at p.{ranges[i].From}, inside Ch.{ranges[i - 1].Number} "
                + $"which runs to p.{ranges[i - 1].To}. Overlapping ranges make every in-range "
                + "page check below meaningless.");

            Assert.True(step <= 2,
                $"printed pp.{ranges[i - 1].To}-{ranges[i].From} belong to no chapter file.");
        }
    }

    /// <summary>
    /// Every section says which printed page it came from, inside its chapter's range — that is
    /// what makes a quotation checkable against the book by whoever reads it next.
    /// </summary>
    [Fact]
    public void EverySectionCarriesAPageInsideItsChapter()
    {
        Assert.All(All(), c => Assert.All(c.Sections, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Heading));
            Assert.InRange(s.PrintedPage, c.PrintedPages[0], c.PrintedPages[1]);
        }));
    }

    /// <summary>
    /// <b>A heading with nothing under it is a layout artifact, not a section of the book.</b>
    /// The shipped corpus carried 135 of them — most of Chapter 9's tail, where the blank Hero
    /// Sheet form was read as eighteen headings with no text.
    ///
    /// <para>Nothing asserted a section had any text at all, so an adversarial pass blanked all
    /// 33 sections of Chapter 5 and the whole suite stayed green.</para>
    /// </summary>
    [Fact]
    public void EverySectionHasTextUnderIt()
    {
        foreach (var c in All())
            foreach (var s in c.Sections)
                Assert.False(string.IsNullOrWhiteSpace(s.Text),
                    $"Ch.{c.Number} p.{s.PrintedPage} \"{s.Heading}\" has no text. A heading with "
                    + "nothing under it is an extraction artifact.");
    }

    /// <summary>
    /// A chapter's sections are spread across its pages rather than all citing one. The page is
    /// the only thing that makes a quotation checkable, and nothing noticed when an adversarial
    /// pass collapsed every citation in a chapter onto its first page — each one was still "in
    /// range", which was all that was asked.
    /// </summary>
    [Fact]
    public void ASectionsPageIsNotTheSameOneForTheWholeChapter()
    {
        foreach (var c in All())
        {
            var pages = c.PrintedPages[1] - c.PrintedPages[0] + 1;
            if (pages < 4) continue;                      // too short for the shape to mean anything

            var distinct = c.Sections.Select(s => s.PrintedPage).Distinct().Count();

            Assert.True(distinct >= pages / 2,
                $"Ch.{c.Number} covers {pages} printed pages but its {c.Sections.Length} sections "
                + $"cite only {distinct} of them.");
        }
    }

    /// <summary>
    /// <b>The publisher's watermark is not content and must never be redistributed.</b> Every
    /// page of the PDF carries the purchaser's name and order number, and it extracts as four
    /// separate words — so a filter written against the whole phrase let all four through into
    /// the prose, which is how the first extraction shipped it into fifty chapters' worth of text.
    ///
    /// <para><b>Asserted token by token, which is the shape the watermark actually has.</b> The
    /// first version of this test checked the surname, the order number and the exact spelling
    /// "(Order #" — so an adversarial pass injected <c>Dorian Order #</c> and it passed. The
    /// given name and a paren-less "Order #" were both uncovered, and those are two of the four
    /// tokens the docstring is about.</para>
    /// </summary>
    [Theory]
    [InlineData("Sheiles")]
    [InlineData("Dorian")]
    [InlineData("50732985")]
    [InlineData("Order #")]
    public void ThePurchaserWatermarkIsNotInTheCorpus(string token)
    {
        foreach (var file in Directory.GetFiles(CorpusPath, "*.json"))
            Assert.DoesNotContain(token, File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>The display faces are faked bold by drawing the text twice a fraction of a point
    /// apart</b>, so the two passes interleave and the page number 29 reads as "2299". The
    /// shipped corpus carried 83 of those inside sentences — "…per extra force field. 2299 PRO
    /// Inviolate…" — and three chapter titles as "EEDDIITTIIOONN", "UULLTTIIMMAATTEE" and
    /// "HHEERROO SSHHEEEETT".
    /// </summary>
    [Fact]
    public void NoDoubledGlyphArtifactSurvivesInTheCorpus()
    {
        // Every character doubled. The two halves are separate because the honest threshold
        // differs: a doubled page number is four digits ("2299"), while two doubled letters is
        // "WWII", which is a word the Equipment chapter really uses. Three doubled letter pairs
        // is not a word in any language this book is written in.
        var doubled = new Regex(@"\b(?:(\d)\1){2,}\b|\b(?:(\p{L})\2){3,}\b",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var c in All())
            foreach (var s in c.Sections)
            {
                Assert.DoesNotMatch(doubled, s.Heading);

                var hit = doubled.Match(s.Text);
                Assert.False(hit.Success,
                    $"Ch.{c.Number} p.{s.PrintedPage} \"{s.Heading}\" carries the doubled-glyph "
                    + $"artifact \"{hit.Value}\".");
            }
    }

    /// <summary>
    /// The chapter title runs up the outer margin one letter at a time, and the word "chapter"
    /// runs beside it — rotated, so it extracts reversed. Grouped by baseline it lands inside
    /// body lines, and the shipped corpus had "retpahc" as a section heading twice.
    /// </summary>
    [Fact]
    public void TheRotatedMarginaliaIsNotReadAsProse()
    {
        foreach (var c in All())
            foreach (var s in c.Sections)
            {
                Assert.DoesNotContain("retpahc", s.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("retpahc", s.Heading, StringComparison.OrdinalIgnoreCase);
            }
    }

    /// <summary>
    /// <b>Every chapter opening in the book is set full width, and the first extractor cut every
    /// one of them in half.</b> It split each page at a fixed midpoint and emitted the left half
    /// then the right, so a full-width line lost its middle and the orphaned right-hand halves
    /// piled up as their own block. Chapter 5 opened "Although Heroes have powers and abilities
    /// beyond the ken of makes these characters heroes…", which still scans as English — which is
    /// exactly why nothing caught it.
    ///
    /// <para>Asserted as whole sentences rather than as scattered substrings. A
    /// <c>Contains</c> on a short phrase survives the damage: both halves of a butchered line
    /// still contain their own fragments.</para>
    /// </summary>
    [Fact]
    public void AFullWidthChapterOpeningReadsAsWholeSentences()
    {
        var ch5 = All().Single(c => c.Number == 5).Sections[0];

        Assert.Equal("RESOLVE AND ADVERSITY", ch5.Heading);
        Assert.Contains(
            "Although Heroes have powers and abilities beyond the ken of normal men and women, "
            + "that isn’t what makes them heroes.",
            ch5.Text, StringComparison.Ordinal);

        var ch2 = All().Single(c => c.Number == 2).Sections[0];

        Assert.Contains(
            "Characters include all beings in the game world, from the Heroes run by the players "
            + "to the Villains, Foes, Minions, and Extras run by the GM.",
            ch2.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A spot check that the extraction is the <em>book</em> and not a scramble: entries whose
    /// printed text is known, on pages far apart, each read back with their stat line intact.
    ///
    /// <para>The two-column layout is the hazard, and it bites in both directions. Read by
    /// baseline alone the headings of facing entries interleave — printed p.52 gave the single
    /// heading "OVERKILL PHASE SHIFT", which is two separate Cons — while splitting every page at
    /// a fixed midpoint destroys anything set full width. So both are asserted here: two facing
    /// entries are separate sections, and each carries its own text.</para>
    /// </summary>
    [Fact]
    public void KnownEntriesReadBackAsTheyArePrinted()
    {
        var ch2 = All().Single(c => c.Number == 2);

        var phasing = ch2.Sections.Single(s => s.Heading == "PHASING");
        Assert.Equal(36, phasing.PrintedPage);
        Assert.Contains("Self • Default Rank • 9 Hero Points", phasing.Text, StringComparison.Ordinal);
        Assert.Contains("out of phase with the physical world", phasing.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("PSYCHOMETRY", phasing.Text, StringComparison.Ordinal);

        // A two-word heading, which is where guessing word breaks from letter gaps rather than
        // reading the page's own space glyphs turned "FORCE FIELD" into "FORCEFIELD".
        var forceField = ch2.Sections.Single(s => s.Heading == "FORCE FIELD");
        Assert.Equal(29, forceField.PrintedPage);
        Assert.Contains(
            "Apply the Zone Pro to shield large areas, the Ranged Pro to shield things at a "
            + "distance, or the Area Pro to shield large areas at a distance.",
            forceField.Text, StringComparison.Ordinal);

        // The two facing entries on p.52, which used to be one section.
        var overkill = ch2.Sections.Single(s => s.Heading == "OVERKILL");
        Assert.Equal(52, overkill.PrintedPage);
        Assert.Contains("1 Hero Point per 2 ranks", overkill.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHASE SHIFT", overkill.Text, StringComparison.Ordinal);

        var phaseShift = ch2.Sections.Single(s => s.Heading == "PHASE SHIFT");
        Assert.Equal(52, phaseShift.PrintedPage);
    }

    /// <summary>
    /// <b>The book is most of a megabyte of prose, and nothing measured how much of it was
    /// there.</b> An adversarial pass truncated every section to forty characters — deleting
    /// 90% of the corpus — and the whole suite stayed green, because every test above asks
    /// whether a section has <em>some</em> text rather than whether the book is still in here.
    ///
    /// <para>The bound is loose on purpose: it is a floor against mass loss, not a checksum, and
    /// re-extracting with a better reader is allowed to move the figure a little.</para>
    /// </summary>
    [Fact]
    public void TheCorpusStillHoldsTheWholeBook()
    {
        var chapters = All();
        var prose = chapters.Sum(c => c.Sections.Sum(s => (long)s.Text.Length));
        var sections = chapters.Sum(c => c.Sections.Length);

        Assert.True(prose > 550_000,
            $"the corpus holds {prose:N0} characters of prose; the book is about 615,000. "
            + "Something has removed most of it.");

        Assert.True(sections > 1_200, $"only {sections} sections; there should be about 1,500.");

        // Chapter 2 is the one the engine's data is drawn from, and is by far the largest.
        var ch2 = chapters.Single(c => c.Number == 2);
        Assert.True(ch2.Sections.Sum(s => (long)s.Text.Length) > 200_000,
            "Chapter 2 has lost most of its text.");
    }

    /// <summary>
    /// <b>Every Power's entry begins with the stat line the rulebook prints for it, and the Range
    /// in that line is the Range <c>data/rules</c> records.</b>
    ///
    /// <para>This is the test that ties a section's body to its own heading, across a hundred and
    /// sixteen entries rather than the four spot checks above. An adversarial pass rotated every
    /// section in the corpus onto the wrong heading — all 1,492 of them — and nothing went red,
    /// because no test asked whether the text under a heading had anything to do with it.</para>
    ///
    /// <para>It is also the sharpest check on reading order there is. Chapter 2 sets one Power
    /// entry after another down two columns, so any mistake in how the columns are ordered shows
    /// up immediately as an entry that opens with the tail of its neighbour: "ARMOR" beginning
    /// "Power rank or your Power rank. When mimicking…". Twenty-six entries were in that state at
    /// one point in this change and every other test passed.</para>
    ///
    /// <para>The two stores are checked against each other on purpose. <c>data/rules</c> is
    /// verified entry by entry against the page and locked by <c>PowerDataTests</c>, so it is a
    /// fair independent witness to what the corpus should say.</para>
    /// </summary>
    [Fact]
    public void EveryPowerEntryOpensWithItsOwnPrintedStatLine()
    {
        var rules = new RulesRepository(RulesFixture.DataPath);
        var ch2 = All().Single(c => c.Number == 2);

        var matched = 0;
        var wrong = new List<string>();

        foreach (var power in rules.Powers)
        {
            var heading = power.Name.ToUpperInvariant();
            var entries = ch2.Sections.Where(s => s.Heading == heading).ToList();
            if (entries.Count == 0) continue;             // an option of a grouped Power, or a table

            matched++;

            // "Self • Power Rank • 1 Hero Point per rank" — the bullet is the book's own.
            var range = char.ToUpperInvariant(power.Range[0]) + power.Range[1..].ToLowerInvariant();
            var opening = $"{range} •";

            if (!entries.Any(e => e.Text.StartsWith(opening, StringComparison.Ordinal)))
                wrong.Add($"{heading} should open \"{opening}\" but opens "
                    + $"\"{Truncate(entries[0].Text)}\"");
        }

        Assert.True(matched >= 110,
            $"only {matched} of {rules.Powers.Count} Powers have an entry in the corpus; "
            + "the headings have stopped matching the rules data.");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} of {matched} Power entries do not open with their own stat line, "
            + $"which means the text under those headings is not theirs:\n  "
            + string.Join("\n  ", wrong.Take(10)));
    }

    private static string Truncate(string s) =>
        s.Length <= 45 ? s : s[..45] + "…";

    /// <summary>
    /// <b>Chapter 8 prints forty named characters, and an extraction dropped every one of their
    /// names.</b> A code name is followed straight away by the "hero"/"villain" label, so it
    /// never accumulated a body of its own and was discarded as an empty heading — leaving page
    /// after page of anonymous ABILITIES and POWERS with nothing to say whose they were.
    ///
    /// <para>The names are taken from <see cref="PrebuiltHeroes"/>, which transcribes the printed
    /// sheets and is held to them by <c>PrebuiltHeroTests</c>, so this cannot be satisfied by
    /// whatever the extractor happened to produce.</para>
    /// </summary>
    [Fact]
    public void EveryPublishedCharacterIsNamedInChapterEight()
    {
        var headings = All().Single(c => c.Number == 8).Sections
            .Select(s => s.Heading.ToUpperInvariant()).ToList();

        // The book prints two characters as "HERALD"; the transcription disambiguates them as
        // "Herald (Airmid)" and "Herald (Scathach)", which is a name only this repository uses.
        var missing = PrebuiltHeroes.All
            .Select(h => h.Name.Split('(')[0].Trim().ToUpperInvariant())
            .Distinct()
            .Where(name => !headings.Any(h => h.Contains(name, StringComparison.Ordinal)))
            .ToList();

        Assert.True(missing.Count == 0,
            "Chapter 8 prints these characters but the corpus never names them: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// Both optional Trait Cap rules are printed inside the Overkill Con's own entry, which is
    /// why <c>data/rules</c> records them against p.52 rather than against the chapter that
    /// discusses caps. The pairing is asserted in <see cref="RulesFileCoverageTests"/>; what is
    /// asserted here is that the page they are cited from actually says it.
    /// </summary>
    [Fact]
    public void TheOptionalTraitCapRulesArePrintedOnThePageTheDataCites()
    {
        var overkill = All().Single(c => c.Number == 2).Sections
            .Single(s => s is { Heading: "OVERKILL", PrintedPage: 52 });

        Assert.Contains("3 ranks", overkill.Text, StringComparison.Ordinal);
        Assert.Contains("never be lower than 9d", overkill.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The corpus used to run a whole entry together as one flat line.</b> LUCK's description
    /// arrived as a single 120-word run because nothing kept the vertical spacing the printed page
    /// sets a paragraph start with. See <c>PROGRESS.md</c> item 1c and <see cref="ParagraphJoiner"/>.
    ///
    /// <para>Positive control first: LUCK really does have four paragraphs on printed p.33 — the
    /// stat line sits on its own with extra leading below it (18pt against the entry's own 12pt
    /// normal), then the description, then a PRO Control block set off the same way, then a PRO
    /// Unbelievable block — so a passage with no <c>\n</c> at all would already be the wrong
    /// answer whatever else this test checked. <c>RulebookProse.Read</c> already splits and
    /// renders on exactly this character, so a corpus that gains real breaks needs no change on
    /// that side to show them; the stat line's own leading <c>\n</c> lands inside the text
    /// <c>RulebookProse.Read</c> strips off as the matched stat line, so its own output is
    /// unaffected — see <see cref="RulebookProseTests"/>.</para>
    /// </summary>
    [Fact]
    public void LucksDescriptionKeepsItsFourPrintedParagraphs()
    {
        var luck = All().Single(c => c.Number == 2).Sections.Single(s => s.Heading == "LUCK");

        var paragraphs = luck.Text.Split('\n');

        Assert.True(paragraphs.Length > 1, // positive control: splitting actually happened
            $"LUCK's description is still one flat run of {luck.Text.Length} characters.");

        Assert.Equal(4, paragraphs.Length);
        Assert.Equal("Self • Power Rank • 2 Hero Points per rank", paragraphs[0]);
        Assert.StartsWith("You are incredibly lucky", paragraphs[1], StringComparison.Ordinal);
        Assert.EndsWith("without you needing to do anything.", paragraphs[1], StringComparison.Ordinal);
        Assert.StartsWith("PRO Control (+4):", paragraphs[2], StringComparison.Ordinal);
        Assert.StartsWith("PRO Unbelievable (+1 per rank):", paragraphs[3], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Over-splitting guard, scoped to what this fix is actually for.</b> A paragraph invented
    /// mid-sentence is the failure mode the whole feature answers to, and the cheapest strong
    /// signal of one is a paragraph that begins with a lowercase letter — an ordinary sentence
    /// never does, so a lowercase start almost always means the line before it was cut off rather
    /// than finished.
    ///
    /// <para><b>This is scoped to Chapter 2's Power entries, not the whole book, and that scoping
    /// is deliberate rather than a shortcut.</b> Two pre-existing, already-documented extraction
    /// limitations (see <c>PageReader</c>'s own doc comments) produce lowercase-looking text
    /// elsewhere that has nothing to do with this feature: a table of three or more columns is
    /// read across rather than down, which the vertical-gap logic then also splits at row
    /// boundaries without repairing the underlying scramble; and Chapter 8's small-capitals field
    /// labels ("orIGIn:", "aBIlItIes") extract with their case as stored, so a genuine, correct
    /// field-to-field break can start with what looks like a lowercase letter. Both are measured,
    /// named, and unrelated to paragraph detection — asserting the whole book here would either
    /// need to special-case them (inventing exactly the kind of denylist this repository's own
    /// guidance warns against) or hide a real regression in the 116 Power entries this fix exists
    /// for behind noise from table pages it was never meant to fix. What this check cannot catch:
    /// a false split that happens to land after a full stop, or one whose next word happens to be
    /// capitalized (a proper noun, "I", the start of a quoted sentence) — those still read as
    /// English and are why the corpus tests were re-run and several passages were spot-checked
    /// against the printed page rather than relying on this alone.</para>
    /// </summary>
    [Fact]
    public void NoChapterTwoPowerParagraphContinuesMidSentence()
    {
        var rules = new RulesRepository(RulesFixture.DataPath);
        var ch2 = All().Single(c => c.Number == 2);

        var checkedParagraphs = 0;
        var badBreaks = new List<string>();

        foreach (var power in rules.Powers)
        {
            var heading = power.Name.ToUpperInvariant();
            foreach (var entry in ch2.Sections.Where(s => s.Heading == heading))
            {
                var paragraphs = entry.Text.Split('\n');
                for (var i = 1; i < paragraphs.Length; i++)
                {
                    checkedParagraphs++;
                    var p = paragraphs[i];
                    if (p.Length > 0 && char.IsLower(p[0]))
                        badBreaks.Add($"{heading}: \"{Truncate(p)}\"");
                }
            }
        }

        Assert.True(checkedParagraphs > 50, // positive control: this actually looked at real splits
            $"only {checkedParagraphs} paragraph breaks were found across Chapter 2's Power "
            + "entries to check — the passage this guard is meant to police may not be reachable.");

        Assert.True(badBreaks.Count == 0,
            "a paragraph inside a Chapter 2 Power entry starts with a lowercase letter, which "
            + "usually means a break was invented mid-sentence rather than read off the page:\n  "
            + string.Join("\n  ", badBreaks));
    }

    /// <summary>
    /// <b>The sentences <c>powers.json</c>'s combat-skill carve-out is read out of, held to the
    /// pages they are claimed from.</b> Ch.5 p.83 exempts "Expertise (except for combat skills)"
    /// and never says what a combat skill is — the phrase occurs once in the whole book, which this
    /// asserts too. So the reading in <c>affects_resolve_when_nominated</c> rests on Chapter 2
    /// instead, and a reading resting on quotes nobody checks is a claim nobody can check.
    ///
    /// <para><b>The set comes off one printed table, which is why that table is pinned row by
    /// row.</b> Ch.4 p.75's Attack and Defense table is the book's own statement of which Traits
    /// attack and which defend, and its five rows name four Abilities between them: Might, Agility,
    /// Toughness, Willpower. Intellect and Perception appear in no row, and this asserts that too —
    /// the exclusion is half the reading, and it is the half a widened set would break silently.</para>
    ///
    /// <para><b>The Talents half is the load-bearing one and it is a negative.</b> "Martial Arts"
    /// is a <em>Power</em> in this game, not a Talent, so the natural first guess at the set — the
    /// combat Talents — has no members at all. p.18 names all twelve, and this requires each of
    /// them to be printed there and none of the four chosen Abilities to be among them: if a combat
    /// Talent ever turns out to exist, the set is wrong and this is where it shows.</para>
    ///
    /// <para>See <c>PowerDataTests.OnlyExpertiseCarriesTheCombatSkillCarveOutAndItNamesTheCombatAbilities</c>
    /// for the set itself and the whole argument.</para>
    /// </summary>
    [Fact]
    public void ThePagesBehindTheCombatSkillReadingSayWhatItRestsOn()
    {
        var chapters = All();
        var ch2 = chapters.Single(c => c.Number == 2);
        var ch4 = chapters.Single(c => c.Number == 4);

        string Section(string heading, int printedPage)
        {
            var found = ch2.Sections.Single(s =>
                string.Equals(s.Heading, heading, StringComparison.Ordinal) && s.PrintedPage == printedPage);

            // Positive control per lookup: an empty passage would satisfy nothing asserted of it
            // and would look exactly like agreement.
            Assert.True(found.Text.Length > 200,
                $"Ch.2 p.{printedPage} '{heading}' came back as {found.Text.Length} characters.");

            return found.Text;
        }

        // "Combat skills" is p.83's phrase and the book's only use of it, so there is no definition
        // anywhere to transcribe instead of reading one.
        var uses = chapters
            .SelectMany(c => c.Sections)
            .Count(s => s.Text.Contains("combat skill", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, uses);

        // Ch.4 p.75, the Attack and Defense table: five rows, and the four Abilities in them are
        // the set. Held row by row rather than by searching the page, so a row that lost its
        // Toughness or gained an Intellect has to disagree with a line here.
        var table = ch4.Sections.Single(s =>
            s.PrintedPage == 75
            && s.Heading.StartsWith("ATTACK AND DEFENSE", StringComparison.Ordinal));

        string[] rows =
        [
            "Unarmed Might Agility or Toughness or Power",
            "Melee Weapon Might Agility or ½ Toughness or Power",
            "Ranged Weapon Agility Agility or ½ Toughness or Power",
            "Physical Power Power Agility or ½ Toughness or Power",
            "Mental Power Power Willpower or Power"
        ];

        Assert.All(rows, row => Assert.Contains(row, table.Text, StringComparison.Ordinal));

        // And the two Abilities that are in no row at all. This is the exclusion the reading turns
        // on, so it is asserted of the page rather than inferred from the four that are there.
        Assert.DoesNotContain("Intellect", table.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Perception", table.Text, StringComparison.Ordinal);

        // Ch.2 p.17 on each of the four, in the book's own words. Might and Agility attack;
        // Toughness and Willpower defend, and defending is what p.75's table is half about.
        Assert.Contains(
            "used to perform armed and unarmed close combat attacks",
            Section("MIGHT", 17),
            StringComparison.Ordinal);

        Assert.Contains(
            "applies when firing mundane ranged weapons and defending against attacks",
            Section("AGILITY", 17),
            StringComparison.Ordinal);

        Assert.Contains(
            "used to resist Powers that affect you physically",
            Section("TOUGHNESS", 17),
            StringComparison.Ordinal);

        Assert.Contains(
            "used to defend against Powers that affect the mind or soul",
            Section("WILLPOWER", 17),
            StringComparison.Ordinal);

        // And not one of the twelve Talents is a combat Talent, which is why the set holds none.
        var talents = Section("TALENTS", 18);
        var rules = new RulesRepository(RulesFixture.DataPath);

        Assert.Equal(12, rules.Talents.Count);
        Assert.All(rules.Talents, t => Assert.Contains(t.Name, talents, StringComparison.Ordinal));

        var combatAbilities = new[] { "Might", "Agility", "Toughness", "Willpower" };
        Assert.All(
            combatAbilities,
            name => Assert.DoesNotContain(
                rules.Talents,
                t => string.Equals(t.Name, name, StringComparison.Ordinal)));

        // The nomination's granularity, and the book's own combat specialisation.
        var expertise = ch2.Sections.Single(s =>
            string.Equals(s.Heading, "EXPERTISE", StringComparison.Ordinal));

        Assert.Contains(
            "Your specialization must fall under one of your Abilities or Talents",
            expertise.Text,
            StringComparison.Ordinal);

        Assert.Contains("Expertise (Agility: Firearms)", expertise.Text, StringComparison.Ordinal);
    }
}
