using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

// Aliases, and not cosmetic ones: spelled in full,
// FeaturePrices.<member> is a 24/17/16-character dotted run, which is
// exactly the shape AccountsContractTests.NoKeyOrTokenIsInTheRepository catches as a signed token —
// that guard scans tests/ too, and it fired on eleven of these. Its lower-case-identifier exclusion
// cannot cover a PascalCase member path without also excluding a real JWT segment, so the shortening
// happens here rather than in the scanner. Do not "tidy" these back to fully qualified names.
using FeaturePrices = ProwlersAndParagonsAutomation.Tests.CanonicalChapterSixRules.FeaturePriceShape;
using StockSums = ProwlersAndParagonsAutomation.Tests.CanonicalChapterSixRules.StockVehicleArithmetic;
using FeatureLimits = ProwlersAndParagonsAutomation.Tests.CanonicalChapterSixRules.VehicleFeatureLimits;
using GradedFeature = ProwlersAndParagonsAutomation.Tests.CanonicalChapterSixRules.GradedVehicleFeature;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b><c>data/rules/gadgets.json</c>, <c>vehicles.json</c> and <c>headquarters.json</c> against the
/// pages they came from.</b> Chapter 6, printed pp.94-104: Gadgets, Vehicles and Headquarters, the
/// last of the chapter and the one <c>docs/RULEBOOK-COVERAGE.md</c> called the next thing to read.
///
/// <para>The transcription is <see cref="CanonicalChapterSixRules"/> and it is the rulebook — do
/// not "fix" a failure by editing a value there. The four printed tables are deliberately not in
/// it: they are derived out of <c>data/rulebook/ch06-equipment.json</c> here, because the corpus
/// already is the book and a second typing of seventy-eight rows would only be a second thing to
/// disagree with the first.</para>
///
/// <para><b>All three files are on <see cref="RulesRepository.DataFileNames"/> now</b>, put there
/// by the slice that taught <c>CostCalculator</c> what a vehicle, a base and a Gadget cost. This
/// file is still what holds the data to the pages: a loader proves a file parses, not that it says
/// what the book says.</para>
/// </summary>
public sealed class Chapter6RulesDataTests
{
    private static string DataPath => RulesFixture.DataPath;
    private static string RulebookPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    private static readonly JsonSerializerOptions Lenient = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip
    };

    /// <summary>
    /// The repository's own options plus the one thing it must not do at runtime: refuse a field no
    /// model reads. Same reasoning as <see cref="RulesFileCoverageTests"/> — the engine stays
    /// lenient so a rules file gaining a key cannot take the site down, and strictness lives here
    /// where it is a failing test.
    /// </summary>
    private static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        UnmappedMemberHandling      = JsonUnmappedMemberHandling.Disallow
    };

    private static string Raw(string file) => File.ReadAllText(Path.Combine(DataPath, file));

    private static GadgetRulesFile Gadgets() =>
        JsonSerializer.Deserialize<GadgetRulesFile>(Raw("gadgets.json"), Lenient)!;

    private static VehicleRulesFile Vehicles() =>
        JsonSerializer.Deserialize<VehicleRulesFile>(Raw("vehicles.json"), Lenient)!;

    private static HeadquartersRulesFile Headquarters() =>
        JsonSerializer.Deserialize<HeadquartersRulesFile>(Raw("headquarters.json"), Lenient)!;

    private static readonly (string File, Type Model)[] Coverage =
    [
        ("gadgets.json",      typeof(GadgetRulesFile)),
        ("vehicles.json",     typeof(VehicleRulesFile)),
        ("headquarters.json", typeof(HeadquartersRulesFile))
    ];

    // ── Coverage: every field is read by some model ──────────────────────────

    public static TheoryData<string, Type> FilesAndModels()
    {
        var data = new TheoryData<string, Type>();
        foreach (var (file, model) in Coverage) data.Add(file, model);
        return data;
    }

    /// <summary>
    /// <b>Every field in each of the three files is read by some model.</b> A key no model reads is
    /// unread data, which reads like a source of truth and is not one — the failure
    /// <c>creation_rules.json</c> shipped for months.
    /// </summary>
    [Theory]
    [MemberData(nameof(FilesAndModels))]
    public void EveryFieldInAChapterSixFileIsReadBySomeModel(string fileName, Type model)
    {
        var ex = Record.Exception(() => JsonSerializer.Deserialize(Raw(fileName), model, Strict));

        Assert.True(ex is null,
            $"{fileName} carries a field no model reads, so nothing can hold it to the rulebook: "
            + ex?.Message);
    }

    /// <summary>
    /// <b>All three files are on the loader's contract, and this says so out loud.</b>
    ///
    /// <para>It used to say the opposite, and the reversal is the point: the consumer slice — the
    /// one that taught <c>CostCalculator</c> what a vehicle, a base and a Gadget cost — is the
    /// slice that got to decide the browser fetches them before its first render. This test is
    /// where that decision is recorded, rather than a line somebody deleted quietly.</para>
    /// </summary>
    [Fact]
    public void TheChapterSixFilesAreOnTheRepositorysContract()
    {
        foreach (var (file, _) in Coverage)
        {
            Assert.Contains(file, RulesRepository.DataFileNames);

            // Positive control on the assertion above: the list is real and does not simply
            // contain everything anybody asks it about.
            Assert.DoesNotContain("no-such-rules-file.json", RulesRepository.DataFileNames);
        }
    }

    // ── The reflection walk ──────────────────────────────────────────────────

    /// <summary>
    /// Fields whose value is this project's prose rather than a transcribed fact. Identified by a
    /// <b>naming rule</b> and not by an exemption list, because a list of "this one is only
    /// descriptive" is exactly how twenty fact fields in <c>data/rules/play/</c> came to be
    /// compared against nothing at all.
    /// </summary>
    private static bool IsProse(string leafName) =>
        leafName is "description" or "mechanic" or "note" or "what_this_is"
        || leafName.EndsWith("_note", StringComparison.Ordinal);

    /// <summary>
    /// Paths whose value is derived from the corpus rather than transcribed here, each with the
    /// test that derives it. <b>A row on this list is not exempt from being checked</b> — it is
    /// checked somewhere stronger.
    /// </summary>
    private static readonly Dictionary<string, string> DerivedPaths =
        new(StringComparer.Ordinal)
        {
            ["mundane_vehicles_air_space.vehicles"] = nameof(TheThreeMundaneVehicleTablesArePairedOutOfTheCorpusColumns),
            ["mundane_vehicles_ground.vehicles"]    = nameof(TheThreeMundaneVehicleTablesArePairedOutOfTheCorpusColumns),
            ["mundane_vehicles_water.vehicles"]     = nameof(TheThreeMundaneVehicleTablesArePairedOutOfTheCorpusColumns),
            ["stock_vehicles.stock"]                = nameof(TheSixStockVehiclesAreReadOutOfTheCorpus),
            ["vehicle_features.features"]           = nameof(EveryVehicleFeaturePriceIsReadOutOfTheCorpus),
            ["base_features.features"]              = nameof(EveryBaseFeaturePriceIsReadOutOfTheCorpus)
        };

    /// <summary>
    /// <b>Every fact leaf of every entry is compared against the rulebook.</b> Loading a value is
    /// not verifying it: <c>data/rules/play/</c> shipped around twenty fields that deserialized
    /// perfectly and were compared to nothing, and the guard that found them is the shape of this
    /// one.
    ///
    /// <para>Each leaf below an entry's envelope must be one of three things — transcribed (a key
    /// of <see cref="CanonicalChapterSixRules.Facts"/>), derived (on <see cref="DerivedPaths"/>,
    /// with a named test that computes it from the corpus), or prose (by
    /// <see cref="IsProse"/>). Anything else fails, naming the path.</para>
    ///
    /// <para><b>The walk carries a positive control on itself</b>, because a walk that stopped
    /// finding leaves would report no faults and prove nothing.</para>
    /// </summary>
    [Fact]
    public void EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook()
    {
        var faults = new List<string>();
        var leaves = 0;
        var reached = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString()!;

                foreach (var property in entry.EnumerateObject())
                {
                    if (Envelope.Contains(property.Name)) continue;

                    Walk($"{id}.{property.Name}", property.Value);
                }
            }
        }

        // Positive control: the walk found leaves at all. 183 today.
        Assert.True(leaves >= 170,
            $"The coverage walk found only {leaves} fact leaves, which means it stopped walking "
            + "rather than that the files shrank.");

        Assert.True(faults.Count == 0, string.Join("; ", faults));

        // An entry deleted from a data file would leave its canonical values sitting here checked
        // by nothing, which is an exemption for something that is no longer there.
        var orphans = CanonicalChapterSixRules.Facts.Keys.Except(reached, StringComparer.Ordinal).ToList();

        Assert.True(orphans.Count == 0,
            "CanonicalChapterSixRules carries values no entry reached: " + string.Join(", ", orphans));

        void Walk(string path, JsonElement value)
        {
            var leafName = path[(path.LastIndexOf('.') + 1)..];

            if (DerivedPaths.ContainsKey(path)) return;

            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in value.EnumerateObject()) Walk($"{path}.{child.Name}", child.Value);
                return;
            }

            leaves++;

            if (IsProse(leafName))
            {
                if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                    faults.Add($"{path}: prose field is empty");
                return;
            }

            if (!CanonicalChapterSixRules.Facts.TryGetValue(path, out var expected))
            {
                faults.Add($"{path}: no canonical value, no derivation and not prose");
                return;
            }

            reached.Add(path);

            var mismatch = Compare(expected, value);
            if (mismatch is not null) faults.Add($"{path}: {mismatch}");
        }
    }

    private static readonly HashSet<string> Envelope = new(StringComparer.Ordinal)
    {
        "id", "name", "kind", "printed_under", "interpretation",
        "description", "verified_fields", "source_ref", "ambiguity"
    };

    /// <summary>
    /// Compares one canonical value with one JSON leaf <b>by kind as well as by value</b>, so a
    /// canonical <c>6</c> against a data file's <c>"6"</c> is a failure rather than something a
    /// loose conversion swallows.
    /// </summary>
    private static string? Compare(object expected, JsonElement actual) => expected switch
    {
        int e => actual.ValueKind == JsonValueKind.Number && actual.TryGetInt32(out var a) && a == e
            ? null
            : $"expected {e}, found {actual}",

        bool e => actual.ValueKind == (e ? JsonValueKind.True : JsonValueKind.False)
            ? null
            : $"expected {e}, found {actual}",

        string e => actual.ValueKind == JsonValueKind.String && actual.GetString() == e
            ? null
            : $"expected \"{e}\", found {actual}",

        string[] e => actual.ValueKind == JsonValueKind.Array
                      && actual.EnumerateArray().Select(x => x.GetString()).SequenceEqual(e)
            ? null
            : $"expected [{string.Join(", ", e)}], found {actual}",

        int[] e => actual.ValueKind == JsonValueKind.Array
                   && actual.EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(e)
            ? null
            : $"expected [{string.Join(", ", e)}], found {actual}",

        _ => $"canonical value of unsupported type {expected.GetType().Name}"
    };

    /// <summary>
    /// <b>The negative control on the classifier above.</b> A walk that faulted nothing would
    /// satisfy the main test perfectly, so the same classifier is fed a path nothing registers and
    /// must report it, with the two prose spellings beside it passing.
    /// </summary>
    [Fact]
    public void TheCoverageWalkReportsAFieldNothingComparesToTheRulebook()
    {
        Assert.False(CanonicalChapterSixRules.Facts.ContainsKey("made_up_entry.made_up.field"));

        Assert.False(IsProse("field"));
        Assert.True(IsProse("description"));
        Assert.True(IsProse("mechanic"));
        Assert.True(IsProse("extraction_note"));

        // And the comparison is by kind: a right-looking number in the wrong shape is a fault.
        using var six = JsonDocument.Parse("\"6\"");
        Assert.NotNull(Compare(6, six.RootElement));

        using var number = JsonDocument.Parse("6");
        Assert.Null(Compare(6, number.RootElement));
    }

    // ── The envelope on every entry ──────────────────────────────────────────

    /// <summary>
    /// Every entry declares which of its fields were checked, out of a closed list the header
    /// carries, and must claim <c>description</c> — the one field that is <em>written</em> rather
    /// than transcribed and therefore the one most easily left unchecked.
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresWhatWasVerified()
    {
        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            var closed = document.RootElement.GetProperty("header")
                .GetProperty("verified_fields_closed_list").EnumerateArray()
                .Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(closed);

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString();
                var declared = entry.GetProperty("verified_fields").EnumerateArray()
                    .Select(x => x.GetString()!).ToList();

                Assert.True(declared.Count > 0, $"{file}/{id} declares no verified fields");
                Assert.True(declared.Contains("description", StringComparer.Ordinal),
                    $"{file}/{id} does not claim its description was checked");

                foreach (var field in declared)
                {
                    Assert.True(closed.Contains(field),
                        $"{file}/{id} claims '{field}', which is not on its header's closed list");
                }
            }
        }
    }

    /// <summary>
    /// <b>Every entry cites a page in the range this slice read, and names a heading the corpus
    /// found on that page.</b> A page in an eighteen-page chapter is a wide target; the heading is a
    /// narrow one, so a wrong page and a wrong heading both fail.
    /// </summary>
    [Fact]
    public void EveryEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterSixSections()
            .Select(s => (s.Page, s.Heading))
            .ToHashSet();

        // Positive control: the lookup found the chapter's headings at all.
        Assert.True(headings.Count >= 100, $"Only {headings.Count} headings were read out of Chapter 6.");

        // Negative control on a heading that really exists on another page.
        Assert.Contains((94, "GADGETS"), headings);
        Assert.DoesNotContain((95, "GADGETS"), headings);

        var faults = new List<string>();

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));
            var (first, last) = PageRanges[file];

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString();
                var sourceRef = entry.GetProperty("source_ref").GetString()!;
                var under = entry.GetProperty("printed_under").GetString()!;

                var page = int.Parse(
                    Regex.Match(sourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                    CultureInfo.InvariantCulture);

                // Out of range is its own fault line rather than an Assert.InRange, which reports
                // the number and not the entry that cited it. The range is the file's own, not the
                // chapter's: the chapter's range catches the two headings printed twice — WEAPONS
                // on p.88 as well as p.94, REINFORCED on p.93 as well as p.102 — but nothing
                // inside it, and a gadgets.json entry citing p.96 under STOCK VEHICLES passed the
                // whole suite before this read the file's range instead.
                if (page < first || page > last)
                    faults.Add($"{file}/{id} cites p.{page}, outside that file's pp.{first}-{last}");
                else if (!headings.Contains((page, under)))
                    faults.Add($"{file}/{id}: '{under}' is not a heading on p.{page}");

                // The feature tables carry a page per row, and those are cited the same way — the
                // entry above them names only the heading the table sits under.
                if (!entry.TryGetProperty("features", out var features)) continue;

                foreach (var row in features.EnumerateArray())
                {
                    var rowPage = row.GetProperty("printed_page").GetInt32();
                    if (rowPage < first || rowPage > last)
                        faults.Add(
                            $"{file}/{id}/{row.GetProperty("id").GetString()} is printed on "
                            + $"p.{rowPage}, outside that file's pp.{first}-{last}");
                }
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));

        // Every file is covered, and each range sits inside the chapter — a range that had grown
        // to the chapter's own would pass every assertion above while checking nothing.
        Assert.Equal(Coverage.Select(c => c.File).Order(), PageRanges.Keys.Order());

        foreach (var (file, (first, last)) in PageRanges)
        {
            Assert.InRange(first, CanonicalChapterSixRules.FirstPage, CanonicalChapterSixRules.LastPageWithText);
            Assert.InRange(last, first, CanonicalChapterSixRules.LastPageWithText);
            Assert.True(
                last - first < CanonicalChapterSixRules.LastPage - CanonicalChapterSixRules.FirstPage,
                $"{file}'s range is the whole chapter, which is not a narrower check than the one "
                + "this replaced.");
        }
    }

    private static IReadOnlyDictionary<string, (int First, int Last)> PageRanges =>
        CanonicalChapterSixRules.FilePageRanges;

    /// <summary>
    /// <b>The chapter's last page carries no text, and the headers say so</b> — the same statement
    /// <c>resolve.json</c>'s header makes about p.86, so nobody goes looking for a page that has
    /// nothing on it.
    /// </summary>
    [Fact]
    public void ThePagesWithNoTextAreRecordedRatherThanLeftLooking()
    {
        var pages = ChapterSixSections().Select(s => s.Page).ToHashSet();

        Assert.Contains(CanonicalChapterSixRules.LastPageWithText, pages);
        Assert.DoesNotContain(CanonicalChapterSixRules.LastPage, pages);

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));
            var omitted = document.RootElement.GetProperty("header")
                .GetProperty("deliberately_omitted").GetString() ?? "";

            Assert.Contains("104", omitted, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>Descriptions are ours; the book's words are the corpus's.</b> The realistic failure is a
    /// description built <em>around</em> a lifted clause rather than one pasted whole, so this fails
    /// on any run of ten consecutive words shared with Chapter 6, after reducing both sides to
    /// letters and digits so re-punctuating a lifted clause does not dodge it.
    /// </summary>
    [Fact]
    public void NoDescriptionRepeatsARunOfTheBooksOwnWords()
    {
        var book = Normalise(string.Join(" ", ChapterSixSections().Select(s => s.Text)));

        // Positive control: a phrase taken out of the corpus must be found in it, or the
        // normaliser has quietly produced an empty haystack and every assertion below is vacuous.
        Assert.Contains(Normalise("Every Hero Point you put into this Perk grants you 25 Vehicle Points"),
            book, StringComparison.Ordinal);

        var faults = new List<string>();

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString();

                foreach (var text in OwnWords(entry))
                {
                    var words = Normalise(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    for (var i = 0; i + 10 <= words.Length; i++)
                    {
                        var run = string.Join(' ', words.Skip(i).Take(10));
                        if (book.Contains(run, StringComparison.Ordinal))
                            faults.Add($"{file}/{id} repeats the book: \"{run}\"");
                    }
                }
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults.Take(5)));
    }

    /// <summary>Every string this project wrote itself on one entry: its description and any row mechanic.</summary>
    private static IEnumerable<string> OwnWords(JsonElement entry)
    {
        yield return entry.GetProperty("description").GetString() ?? "";

        foreach (var key in new[] { "features", "stock", "vehicles" })
        {
            if (!entry.TryGetProperty(key, out var rows)) continue;

            foreach (var row in rows.EnumerateArray())
            {
                if (row.TryGetProperty("mechanic", out var mechanic))
                    yield return mechanic.GetString() ?? "";
            }
        }
    }

    private static string Normalise(string text) =>
        string.Join(' ', Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // ── The four tables, derived out of the corpus ───────────────────────────

    private sealed record CorpusSection(string Heading, int Page, string Text);

    private static IReadOnlyList<CorpusSection> ChapterSixSections()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch06-equipment.json")));

        return
        [
            .. document.RootElement.GetProperty("sections").EnumerateArray()
                .Select(section => new CorpusSection(
                    section.GetProperty("heading").GetString() ?? "",
                    section.TryGetProperty("printed_page", out var page)
                        && page.ValueKind == JsonValueKind.Number ? page.GetInt32() : 0,
                    section.GetProperty("text").GetString() ?? ""))
        ];
    }

    private static string Section(int page, string heading) =>
        ChapterSixSections().Single(s => s.Page == page && s.Heading == heading).Text;

    private static readonly Regex BodyRow =
        new(@"\G([A-Za-z][A-Za-z ,/]*?)(\*?)(?: \((\d+)\)\*\*)? (\d+d(?: or \d+d)?) ?",
            RegexOptions.CultureInvariant);

    private static readonly Regex SpeedRow =
        new(@"\G(\d+)d ([+−-]?\d+)d (\d+d|—) ?", RegexOptions.CultureInvariant);

    /// <summary>
    /// Tiles <paramref name="pattern"/> across <paramref name="text"/> from the first character.
    /// <b>What is left over must not begin another row</b>, which is the control that says "there
    /// is nothing else in this block" rather than merely "the rows I wanted were in it".
    /// </summary>
    private static List<Match> Tile(string text, Regex pattern, string what)
    {
        var rows = new List<Match>();
        var position = 0;

        while (position < text.Length)
        {
            var match = pattern.Match(text, position);
            if (!match.Success) break;

            rows.Add(match);
            position = match.Index + match.Length;
        }

        var leftover = text[position..].Trim();

        Assert.True(leftover.Length == 0,
            $"{what}: the block did not tile — {rows.Count} rows read and \"{leftover}\" left over.");

        return rows;
    }

    /// <summary>
    /// <b>Each mundane vehicle table is derived from the corpus's two printed blocks, and the
    /// pairing is checked rather than assumed.</b>
    ///
    /// <para>The book sets each table as four columns and the extractor reads a table of three or
    /// more columns across rather than down (see <c>docs/guide/rulebook-corpus.md</c>), so each
    /// arrives as a block of names beside their Body and a block of Speed, Control and Weapons.
    /// Pairing them row by row in printed order is this project's reading, recorded as an
    /// <c>interpretation</c> on each entry — and it was confirmed against the PDF with the
    /// extractor's <c>--page</c> diagnostic, where each Body row and its figures share a baseline
    /// exactly.</para>
    ///
    /// <para><b>Three witnesses hold the alignment here, where the diagnostic cannot run.</b> Every
    /// capital-ship row's Control equals p.94's −3d per 30 Health; all six of p.96's stock vehicles
    /// reprint a row characteristic for characteristic; and p.96's Foe example reprints the sedan's
    /// Body. A misalignment of one row breaks all three.</para>
    /// </summary>
    [Fact]
    public void TheThreeMundaneVehicleTablesArePairedOutOfTheCorpusColumns()
    {
        var tables = new (string EntryId, int Page, string BodyHeading, string SpeedHeading, int Rows)[]
        {
            ("mundane_vehicles_air_space", 97, "AIR / SPACE VEHICLES — VEHICLE BODY",
                "AIR / SPACE VEHICLES — SPEED CONTROL WEAPONS", CanonicalChapterSixRules.TableSizes.AirSpaceVehicles),
            ("mundane_vehicles_ground", 97, "GROUND VEHICLES — VEHICLE BODY",
                "GROUND VEHICLES — SPEED CONTROL WEAPONS", CanonicalChapterSixRules.TableSizes.GroundVehicles),
            ("mundane_vehicles_water", 98, "WATER — VEHICLE BODY",
                "VEHICLES — SPEED CONTROL WEAPONS", CanonicalChapterSixRules.TableSizes.WaterVehicles)
        };

        var entries = Vehicles().Entries;

        foreach (var (entryId, page, bodyHeading, speedHeading, expectedRows) in tables)
        {
            var bodyBlock = Section(page, bodyHeading).Split("*Open Cockpit")[0].Trim();
            var speedBlock = Regex.Split(Section(page, speedHeading), @"\bdirect attacks|against direct")[0].Trim();

            var bodies = Tile(bodyBlock, BodyRow, $"{entryId} body block");
            var figures = Tile(speedBlock, SpeedRow, $"{entryId} speed block");

            // The row count is typed into the canonical file, because a derivation cannot notice a
            // table that lost half of itself when the expectation lost the same half.
            Assert.Equal(expectedRows, bodies.Count);
            Assert.Equal(expectedRows, figures.Count);

            var shipped = entries.Single(e => e.Id == entryId).Vehicles!;
            Assert.Equal(expectedRows, shipped.Count);

            for (var i = 0; i < expectedRows; i++)
            {
                var derived = new MundaneVehicleRow
                {
                    Name = bodies[i].Groups[1].Value.Trim(),
                    BodyPrinted = bodies[i].Groups[4].Value,
                    Body = bodies[i].Groups[4].Value.Contains(" or ", StringComparison.Ordinal)
                        ? null
                        : int.Parse(bodies[i].Groups[4].Value.TrimEnd('d'), CultureInfo.InvariantCulture),
                    Speed = int.Parse(figures[i].Groups[1].Value, CultureInfo.InvariantCulture),
                    Control = int.Parse(figures[i].Groups[2].Value.Replace('−', '-'), CultureInfo.InvariantCulture),
                    Weapons = figures[i].Groups[3].Value == "—"
                        ? null
                        : int.Parse(figures[i].Groups[3].Value.TrimEnd('d'), CultureInfo.InvariantCulture),
                    OpenCockpit = bodies[i].Groups[2].Value == "*",
                    CapitalShipHealth = bodies[i].Groups[3].Success
                        ? int.Parse(bodies[i].Groups[3].Value, CultureInfo.InvariantCulture)
                        : null
                };

                Assert.Equal(derived, shipped[i]);
            }
        }

        // The parser is driven one row past the end of a block and required to throw, because a
        // parser that quietly returned a short list would let a truncated block agree with a
        // truncated expectation.
        Assert.Throws<Xunit.Sdk.TrueException>(() =>
            Tile(Section(97, "GROUND VEHICLES — VEHICLE BODY").Split("*Open Cockpit")[0].Trim() + " Hovercraft",
                BodyRow, "a block with a partial row after it"));
    }

    /// <summary>
    /// <b>Witness one on the pairing: every capital-ship row obeys p.94's Control rule exactly.</b>
    /// Nine rows across two tables carry a parenthesised Health, and each one's Control is −3d per
    /// 30 points of it. A misalignment of a single row moves all nine off the rule at once, which
    /// is the property that makes this worth more than a spot check.
    /// </summary>
    [Fact]
    public void EveryCapitalShipRowsControlIsMinusThreeDicePerThirtyHealth()
    {
        var rows = Vehicles().Entries
            .Where(e => e.Vehicles is not null)
            .SelectMany(e => e.Vehicles!)
            .Where(v => v.CapitalShipHealth is not null)
            .ToList();

        // Positive control: there are capital-ship rows to check.
        Assert.True(rows.Count >= 9, $"Only {rows.Count} capital-ship rows were found.");

        foreach (var row in rows)
        {
            var steps = row.CapitalShipHealth!.Value / CanonicalChapterSixRules.CapitalShipControlRule.HealthPerStep;
            var expected = steps * CanonicalChapterSixRules.CapitalShipControlRule.ControlPerStep;

            Assert.Equal(expected, row.Control);
        }

        // And the rule the rows obey is the one the data records, on both entries that state it.
        var control = Vehicles().Entries.Single(e => e.Id == "vehicle_control").Control!;
        var capital = Vehicles().Entries.Single(e => e.Id == "capital_ships").CapitalShips!;

        Assert.Equal(CanonicalChapterSixRules.CapitalShipControlRule.ControlPerStep,
            control.CapitalShipControlPerThirtyHealth);
        Assert.Equal(control.CapitalShipControlPerThirtyHealth, capital.ControlPerThirtyHealth);
        Assert.Equal(CanonicalChapterSixRules.CapitalShipControlRule.HealthPerStep, capital.HealthMultipleOf);
    }

    /// <summary>
    /// <b>Witnesses two and three: the rows the chapter prints a second time, away from the
    /// tables.</b> p.96's Foe example gives the sedan its Body in prose, and <b>all six</b> of
    /// p.96's stock vehicles reprint a table row characteristic for characteristic. Neither is
    /// anywhere near the table it agrees with, so a misalignment of one row moves them.
    ///
    /// <para>This said five, and named the Sports Car as the one the tables do not print a row
    /// for. They do: the Ground table's <c>Car, Sports</c> is 6d Body, 7d Speed, +2d Control and
    /// unarmed, which is the stock Sports Car exactly. The claim cost a witness and asserted
    /// something untrue about the book to do it, so what stands in its place is the map being
    /// exhaustive over the stock list rather than a sixth entry being forbidden.</para>
    /// </summary>
    [Fact]
    public void TheRowsTheChapterPrintsASecondTimeAgreeWithTheTables()
    {
        var rows = Vehicles().Entries
            .Where(e => e.Vehicles is not null)
            .SelectMany(e => e.Vehicles!)
            .ToDictionary(v => v.Name, StringComparer.Ordinal);

        var sedan = rows[CanonicalChapterSixRules.AnchorRows.SedanRowName];
        Assert.Equal(CanonicalChapterSixRules.AnchorRows.SedanBody, sedan.Body);

        // …and the prose that reprints it, read out of the corpus rather than off this file.
        var foes = Section(96, "FOE AND MINION PILOTS");
        Assert.Contains($"sedan with {CanonicalChapterSixRules.AnchorRows.SedanBody}d Body", foes, StringComparison.Ordinal);

        var stock = Vehicles().Entries.Single(e => e.Id == "stock_vehicles").Stock!
            .ToDictionary(s => s.Name, StringComparer.Ordinal);

        foreach (var (stockName, rowName) in CanonicalChapterSixRules.AnchorRows.StockToTableRow)
        {
            var s = stock[stockName];
            var r = rows[rowName];

            Assert.Equal(r.Body, s.Body);
            Assert.Equal(r.Speed, s.Speed);
            Assert.Equal(r.Control, s.Control);
            Assert.Equal(r.Weapons, s.Weapons);
        }

        // Every stock vehicle is a witness, so the map has to name every one of them: a map that
        // quietly dropped an entry would still pass the loop above on whatever was left.
        Assert.Equal(
            stock.Keys.Order(),
            CanonicalChapterSixRules.AnchorRows.StockToTableRow.Keys.Order());
    }

    private static readonly Regex StockLine =
        new(@"^(\d+) Vehicle Points Body (\d+)d, Speed (\d+)d, Control ([+−-]?\d+)d, "
            + @"Weapons (\d+d|n/a) Features: (.+)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// <b>The six stock vehicles are read out of the corpus, not typed a second time.</b> Each is
    /// printed under its own heading on p.96 as one line of points, characteristics and features.
    /// </summary>
    [Fact]
    public void TheSixStockVehiclesAreReadOutOfTheCorpus()
    {
        var shipped = Vehicles().Entries.Single(e => e.Id == "stock_vehicles").Stock!;

        Assert.Equal(CanonicalChapterSixRules.TableSizes.StockVehicles, shipped.Count);

        foreach (var row in shipped)
        {
            var text = Section(96, row.Name.ToUpperInvariant()).Replace("\n", " ", StringComparison.Ordinal);
            var match = StockLine.Match(text.Trim());

            Assert.True(match.Success, $"p.96's {row.Name} entry did not parse: \"{text}\"");

            // Compared as one named tuple rather than as six bare integers, so a failure says
            // which of the six vehicles and which characteristic moved. A loop of
            // Assert.Equal(int, int) reports "Expected: 10, Actual: 9" and names neither.
            var printedFeatures = match.Groups[6].Value.Split(", ", StringSplitOptions.TrimEntries);

            Assert.Equal(
                (row.Name,
                 VehiclePoints: int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                 Body: int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                 Speed: int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                 Control: int.Parse(match.Groups[4].Value.Replace('−', '-'), CultureInfo.InvariantCulture),
                 Weapons: match.Groups[5].Value == "n/a"
                     ? (int?)null
                     : int.Parse(match.Groups[5].Value.TrimEnd('d'), CultureInfo.InvariantCulture),
                 Features: string.Join(" | ", printedFeatures)),
                (Name: row.Name,
                 VehiclePoints: row.VehiclePoints,
                 Body: row.Body,
                 Speed: row.Speed,
                 Control: row.Control,
                 Weapons: row.Weapons,
                 Features: string.Join(" | ", row.Features)));
        }
    }

    /// <summary>How the chapter writes a Base Point price in prose. Indexed by the price itself.</summary>
    private static readonly string[] NumberWords = ["", "One", "Two", "Three"];

    /// <summary>
    /// <b>The run of an entry that belongs to one grade</b>: from where the entry announces that
    /// grade's price in words — "One Base Point provides you with…" — to wherever it announces the
    /// next one, or to the end.
    ///
    /// <para>This exists because the endpoints were the whole check. A graded feature was compared
    /// against the printed range by <c>Min()</c> and <c>Max()</c> alone, so <b>Size's middle grade
    /// was checked by nothing</b> — repricing <c>sprawling</c> from 2 to 3 left Min 1 and Max 3 and
    /// the whole suite green — and no graded feature had its <em>keys</em> tied to its prices at
    /// all, so swapping <c>standard</c> and <c>advanced</c> on any of the ten two-grade features
    /// was invisible too. The book states the binding plainly in every one of the eleven; nothing
    /// was reading it.</para>
    ///
    /// <para>Matching is case-insensitive because p.101's Data Store writes "two Base Points" in
    /// the middle of a sentence, and the grade key's underscore becomes a hyphen because p.103
    /// prints "awe-inspiring".</para>
    /// </summary>
    private static string GradeClause(string text, int cost, IEnumerable<int> allCosts)
    {
        var start = text.IndexOf($"{NumberWords[cost]} Base Point", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "";

        var end = text.Length;

        foreach (var other in allCosts.Where(c => c != cost))
        {
            var at = text.IndexOf($"{NumberWords[other]} Base Point", StringComparison.OrdinalIgnoreCase);
            if (at > start && at < end) end = at;
        }

        return text[start..end];
    }

    private static readonly Regex VehiclePrice =
        new(@"^(−?-?\d+) Vehicle Points?(?: per (.+?))?\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex BasePrice =
        new(@"^(\d+)(?: to (\d+))? Base Points?\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// <b>Every vehicle feature's price is read off its own printed entry, and every one of the
    /// twenty-three is a number.</b> Nothing in this table is left to the GM to price, which is what
    /// lets a later slice total a unique vehicle without inventing a figure.
    ///
    /// <para>The one feature printed as a range — Hidden Compartments, at 1 or 2 points — is
    /// recorded as graded and its two figures are checked against the printed line.</para>
    /// </summary>
    [Fact]
    public void EveryVehicleFeaturePriceIsReadOutOfTheCorpus()
    {
        var features = Vehicles().Entries.Single(e => e.Id == "vehicle_features").Features!;

        Assert.Equal(CanonicalChapterSixRules.TableSizes.VehicleFeatures, features.Count);

        foreach (var feature in features)
        {
            // p.96's Cargo Hold is printed under its bare name; the rest are qualified by the
            // running VEHICLES heading, which is a fact about the corpus rather than the page.
            var heading = feature.PrintedPage == 96
                ? feature.Name.ToUpperInvariant()
                : "VEHICLES — " + feature.Name.ToUpperInvariant();

            var text = Section(feature.PrintedPage, heading);
            var printed = VehiclePrice.Match(text);

            // The one graded feature prints "1 or 2 Vehicle Points", which is not a price line at
            // all by the shape the other twenty-two use — so it is the one that must NOT match.
            Assert.Equal(feature.CostType != "flat_variable", printed.Success);

            var amount = printed.Success
                ? int.Parse(printed.Groups[1].Value.Replace('−', '-'), CultureInfo.InvariantCulture)
                : 0;
            var perUnit = printed.Groups[2].Success;

            switch (feature.CostType)
            {
                case "flat":
                    Assert.False(perUnit, $"{feature.Id} is recorded flat but printed per unit");
                    Assert.Equal((feature.Id, (int?)amount), (feature.Id, feature.Cost));
                    Assert.Null(feature.CostRange);
                    Assert.Null(feature.CostPerUnit);
                    break;

                case "per_unit":
                    Assert.True(perUnit, $"{feature.Id} is recorded per unit but printed flat");
                    Assert.Equal((feature.Id, (int?)amount), (feature.Id, feature.CostPerUnit));
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.UnitLabel);
                    break;

                case "flat_variable":
                    Assert.NotNull(feature.CostRange);
                    Assert.Null(feature.Cost);

                    // The set of prices appears in the entry — and each one is read from where the
                    // entry actually announces it, so which grade costs which is checked too. The
                    // set alone is satisfied just as well by the two grades swapped.
                    Assert.Equal(GradedFeature.Id, feature.Id);

                    foreach (var (grade, cost) in feature.CostRange!)
                    {
                        Assert.Contains($"{cost} Vehicle Point", text, StringComparison.Ordinal);

                        var follows = GradedFeature.PriceFollows[grade];
                        var announced = Regex.Match(text, Regex.Escape(follows) + @" (\d+) Vehicle Point");

                        Assert.True(announced.Success,
                            $"{feature.Id}: p.{feature.PrintedPage} has no price after \"{follows}\"");

                        Assert.Equal(
                            (grade, cost),
                            (grade, int.Parse(announced.Groups[1].Value, CultureInfo.InvariantCulture)));
                    }

                    Assert.Equal(GradedFeature.PriceFollows.Keys.Order(), feature.CostRange.Keys.Order());
                    break;

                default:
                    Assert.Fail($"{feature.Id}: unknown cost_type '{feature.CostType}'");
                    break;
            }
        }

        // The shape of the table, which a per-feature derivation cannot see.
        Assert.Equal(
            FeaturePrices.VehicleFeaturesWithNegativeCost.Order(),
            features.Where(f => f.Cost < 0).Select(f => f.Id).Order());

        Assert.Equal([FeaturePrices.FreeVehicleFeature],
            features.Where(f => f.Cost == 0).Select(f => f.Id));

        var dearest = features.Where(f => f.Cost is not null).MaxBy(f => f.Cost!.Value)!;
        Assert.Equal(FeaturePrices.DearestVehicleFeature, dearest.Id);
        Assert.Equal(FeaturePrices.DearestVehicleFeatureCost, dearest.Cost);
    }

    /// <summary>
    /// <b>A numeric limit a vehicle feature's entry prints is on the entry.</b> A price is not the
    /// whole of what these entries state: p.99's Mecha prints "A vehicle’s Might must equal at
    /// least half its Body", which is a floor on a purchase in exactly the class of the Control cap
    /// the file already records under <c>unique_vehicle_characteristics</c>.
    ///
    /// <para>It was missing, and the model is why: <c>requires</c> names other features and
    /// <c>restricted_to</c> names a kind of vehicle, so a floor on a rank had nowhere to go and was
    /// dropped rather than recorded. The sentence is looked up in the corpus here rather than
    /// trusted from the fixture, so the page losing it fails this test.</para>
    ///
    /// <para>The closing assertion is the half that keeps it honest: <b>exactly</b> the features on
    /// the canonical list carry a constraint, so one invented for a feature the page states nothing
    /// about goes red here.</para>
    /// </summary>
    [Fact]
    public void EveryLimitAVehicleFeaturesEntryPrintsIsOnTheEntry()
    {
        var features = Vehicles().Entries.Single(e => e.Id == "vehicle_features").Features!
            .ToDictionary(f => f.Id, StringComparer.Ordinal);

        foreach (var (id, sentence) in FeatureLimits.PrintedSentence)
        {
            var feature = features[id];

            var heading = feature.PrintedPage == 96
                ? feature.Name.ToUpperInvariant()
                : "VEHICLES — " + feature.Name.ToUpperInvariant();

            Assert.Contains(sentence, Section(feature.PrintedPage, heading), StringComparison.Ordinal);

            Assert.False(string.IsNullOrWhiteSpace(feature.Constraint),
                $"p.{feature.PrintedPage} states a limit under {feature.Name} that the entry does "
                + $"not record: \"{sentence}\"");
        }

        foreach (var word in FeatureLimits.MechaConstraintNames)
            Assert.Contains(word, features["mecha"].Constraint!, StringComparison.Ordinal);

        Assert.Equal(
            FeatureLimits.PrintedSentence.Keys.Order(),
            features.Values.Where(f => f.Constraint is not null).Select(f => f.Id).Order());
    }

    /// <summary>
    /// <b>The structured prerequisite the owner's ruling of 2026-09-10 added (PROGRESS.md item 33,
    /// ruling 2) is read off the entry's own printed sentence, not trusted from the fixture.</b>
    /// Submersible needs Swimming; Transforming needs two of four movement features.
    ///
    /// <para><b>The closing assertion is the half that keeps it honest</b>: <em>exactly</em>
    /// <see cref="CanonicalChapterSixRules.VehicleFeaturePrerequisites.PrintedSentence"/>'s two ids
    /// carry <c>requires_features</c> — so a third one invented for a feature whose entry states no
    /// hard requirement, or one of these two silently dropped, goes red here.</para>
    /// </summary>
    [Fact]
    public void EveryPrerequisiteAVehicleFeaturesEntryPrintsIsOnTheEntry()
    {
        var features = Vehicles().Entries.Single(e => e.Id == "vehicle_features").Features!
            .ToDictionary(f => f.Id, StringComparer.Ordinal);

        foreach (var (id, sentence) in CanonicalChapterSixRules.VehicleFeaturePrerequisites.PrintedSentence)
        {
            var feature = features[id];

            var heading = feature.PrintedPage == 96
                ? feature.Name.ToUpperInvariant()
                : "VEHICLES — " + feature.Name.ToUpperInvariant();

            Assert.Contains(sentence, Section(feature.PrintedPage, heading), StringComparison.Ordinal);

            Assert.NotNull(feature.RequiresFeatures);
            Assert.Equal(
                CanonicalChapterSixRules.VehicleFeaturePrerequisites.AnyOf[id],
                feature.RequiresFeatures!.AnyOf);
            Assert.Equal(
                CanonicalChapterSixRules.VehicleFeaturePrerequisites.Min[id],
                feature.RequiresFeatures.Min);

            // Every id named is itself a feature on this table — a prerequisite naming something
            // that does not exist would be unsatisfiable by construction.
            foreach (var neededId in feature.RequiresFeatures.AnyOf)
                Assert.True(features.ContainsKey(neededId),
                    $"{id}'s requires_features names '{neededId}', which is not a vehicle feature.");
        }

        Assert.Equal(
            CanonicalChapterSixRules.VehicleFeaturePrerequisites.PrintedSentence.Keys.Order(),
            features.Values.Where(f => f.RequiresFeatures is not null).Select(f => f.Id).Order());
    }

    /// <summary>
    /// <b>Every base feature's price is read off its own printed entry, and every one of the
    /// twenty-two has one.</b> "Spending HQ points should map to something" is the owner's whole
    /// requirement for this file, and a feature with no price maps to nothing.
    /// </summary>
    [Fact]
    public void EveryBaseFeaturePriceIsReadOutOfTheCorpus()
    {
        var features = Headquarters().Entries.Single(e => e.Id == "base_features").Features!;

        Assert.Equal(CanonicalChapterSixRules.TableSizes.BaseFeatures, features.Count);

        foreach (var feature in features)
        {
            var text = Section(feature.PrintedPage, feature.Name.ToUpperInvariant());
            var printed = BasePrice.Match(text);

            Assert.True(printed.Success, $"{feature.Id}: no price line found on p.{feature.PrintedPage}");

            var low = int.Parse(printed.Groups[1].Value, CultureInfo.InvariantCulture);
            var high = printed.Groups[2].Success
                ? int.Parse(printed.Groups[2].Value, CultureInfo.InvariantCulture)
                : (int?)null;

            switch (feature.CostType)
            {
                case "flat":
                    Assert.Null(high);
                    Assert.Equal((feature.Id, (int?)low), (feature.Id, feature.Cost));
                    Assert.Null(feature.CostRange);
                    break;

                case "per_unit":
                    Assert.Null(high);
                    Assert.Equal((feature.Id, (int?)low), (feature.Id, feature.CostPerUnit));
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.UnitLabel);
                    break;

                case "flat_variable":
                    Assert.NotNull(high);
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.CostRange);
                    Assert.Equal((feature.Id, low), (feature.Id, feature.CostRange!.Values.Min()));
                    Assert.Equal((feature.Id, high), (feature.Id, (int?)feature.CostRange.Values.Max()));

                    // …and every grade in between, bound to the clause that announces its price.
                    // The endpoints alone leave the middle of a three-grade feature unchecked and
                    // say nothing about which grade costs which — see the method below.
                    foreach (var (grade, cost) in feature.CostRange)
                    {
                        var clause = GradeClause(text, cost, feature.CostRange.Values);

                        Assert.False(clause.Length == 0,
                            $"{feature.Id}: p.{feature.PrintedPage} announces no "
                            + $"'{NumberWords[cost]} Base Point' clause for the {grade} grade");

                        Assert.True(
                            clause.Contains(grade.Replace('_', '-'), StringComparison.OrdinalIgnoreCase),
                            $"{feature.Id}: p.{feature.PrintedPage} does not call its "
                            + $"{NumberWords[cost]} Base Point grade '{grade}'");
                    }

                    // A graded feature says what each grade buys, or the price maps to nothing.
                    Assert.NotNull(feature.GradeEffects);
                    Assert.Equal(feature.CostRange.Keys.Order(), feature.GradeEffects!.Keys.Order());
                    Assert.All(feature.GradeEffects.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
                    break;

                default:
                    Assert.Fail($"{feature.Id}: unknown cost_type '{feature.CostType}'");
                    break;
            }

            // The requirement in one line: every feature has a price, whatever shape it takes.
            Assert.True(feature.Cost is not null || feature.CostRange is not null || feature.CostPerUnit is not null,
                $"{feature.Id} has no price at all, so spending Base Points on it maps to nothing");
        }

        Assert.Equal(FeaturePrices.BaseFeaturesGradedOneToTwo,
            features.Count(f => f.CostRange is { Count: 2 }));

        Assert.Equal([FeaturePrices.BaseFeatureGradedOneToThree],
            features.Where(f => f.CostRange is { Count: 3 }).Select(f => f.Id));

        Assert.Equal([FeaturePrices.BaseFeaturePricedPerUnit],
            features.Where(f => f.CostType == "per_unit").Select(f => f.Id));

        Assert.Equal([FeaturePrices.FreeBaseFeature],
            features.Where(f => f.Cost == 0).Select(f => f.Id));
    }

    // ── The fixture: the authors' own arithmetic ─────────────────────────────

    /// <summary>
    /// <b>The strongest thing in this file: p.96's six stock vehicles priced through the rules on
    /// the same page.</b> Two transcriptions can agree and both be wrong; the authors' arithmetic
    /// cannot. Every characteristic rate and every feature price on the way to a printed total has
    /// to be right for the total to come out, and all six do.
    ///
    /// <para><b>The Submersible reconciles only because "Rader (Sonar)" carries the Con.</b> Read
    /// as a bare Radar at 3 Hero Points it comes to fifteen against a printed fourteen, which is
    /// what this repository first recorded as the book overshooting itself. Ch.2 p.38 prints
    /// <c>CON Sonar (−1)</c> inside the Radar entry, so an underwater vehicle's sonar is a 2 Hero
    /// Point Power and 2 Vehicle Points — and the printed total is exact. That price is read back
    /// out of <c>powers.json</c> here rather than restated, so a change to Radar or to its Con
    /// fails this test instead of leaving a stale constant behind.</para>
    /// </summary>
    [Fact]
    public void TheSixStockVehiclesPriceOutExactlyThroughTheRulesOnTheirOwnPage()
    {
        var vehicles = Vehicles();
        var rates = vehicles.Entries.Single(e => e.Id == "unique_vehicle_characteristics").UniqueVehicleCharacteristics!;
        var features = vehicles.Entries.Single(e => e.Id == "vehicle_features").Features!
            .ToDictionary(f => f.Name, StringComparer.Ordinal);

        var reconciled = new List<string>();

        foreach (var stock in vehicles.Entries.Single(e => e.Id == "stock_vehicles").Stock!)
        {
            var total =
                stock.Body * rates.BodyCostPerRank
                + stock.Speed * rates.SpeedCostPerRank
                + (stock.Weapons ?? 0) * rates.WeaponsCostPerRank
                + (stock.Control >= 0
                    ? stock.Control * rates.ControlCostPerRank
                    : stock.Control * rates.NegativeControlRefundPerRank);

            foreach (var printed in stock.Features) total += FeatureCost(printed);

            Assert.True(
                total == stock.VehiclePoints,
                $"{stock.Name} prices out at {total} Vehicle Points against a printed "
                + $"{stock.VehiclePoints}.");

            reconciled.Add(stock.Name);
        }

        Assert.Equal(
            StockSums.ReconcileExactly.Order(),
            reconciled.Order());

        int FeatureCost(string printed)
        {
            // "Passengers 4" and "Rader (Sonar)" are the two lines that are not a bare feature
            // name; both readings are on the entry's own interpretation.
            if (printed.StartsWith("Passengers ", StringComparison.Ordinal))
            {
                var extra = int.Parse(printed["Passengers ".Length..], CultureInfo.InvariantCulture);
                var passengers = features["Passengers"];
                return passengers.CostPerUnit!.Value * (int)Math.Ceiling(extra / 4.0);
            }

            if (printed.StartsWith("Rader", StringComparison.Ordinal))
                return RadarWithTheSonarCon();

            return features[printed].Cost
                   ?? throw new InvalidOperationException($"{printed} has no flat price");
        }
    }

    /// <summary>
    /// What p.96's "Rader (Sonar)" costs, read out of <c>powers.json</c>: Radar's own flat Hero
    /// Point price plus the modifier on the Sonar Con printed inside its Ch.2 entry. Unique Systems
    /// converts a Hero Point to a Vehicle Point one for one, so this is the figure in both.
    ///
    /// <para>Derived rather than restated on purpose — the constant it replaced said 3, which is
    /// Radar without its Con, and that is what made the Submersible look like a book error.</para>
    /// </summary>
    private static int RadarWithTheSonarCon()
    {
        var powers = JsonSerializer.Deserialize<List<PowerModel>>(Raw("powers.json"), Lenient)!;
        var radar = powers.Single(p => p.Id == "radar");
        var sonar = radar.PowerCons.Single(c => c.Id == "sonar");

        // The canonical figures are what the page prints; if either moves, the transcription and
        // this derivation disagree here rather than silently repricing a stock vehicle.
        Assert.Equal(StockSums.RadarFlatCost, radar.CostFlat);
        Assert.Equal(StockSums.SonarConModifier, sonar.CostModifier);

        return radar.CostFlat!.Value + sonar.CostModifier!.Value;
    }

    /// <summary>
    /// <b>p.95's own worked example of a capital ship ram, replayed through the shipped figures.</b>
    /// The bonus the smaller ship gets is the gap between two Control values, and both of those are
    /// what p.94's rule says they should be for the printed Health scores — so the example is a
    /// check on the rate as well as on the ramming rule.
    /// </summary>
    [Fact]
    public void TheCapitalShipRammingExampleOnPageNinetyFiveComesOutAsPrinted()
    {
        var ramming = Vehicles().Entries.Single(e => e.Id == "capital_ship_ramming").CapitalShipRamming!;

        var smallControl = ramming.WorkedExampleSmallHealth / CanonicalChapterSixRules.CapitalShipControlRule.HealthPerStep * CanonicalChapterSixRules.CapitalShipControlRule.ControlPerStep;
        var largeControl = ramming.WorkedExampleLargeHealth / CanonicalChapterSixRules.CapitalShipControlRule.HealthPerStep * CanonicalChapterSixRules.CapitalShipControlRule.ControlPerStep;

        Assert.Equal(ramming.WorkedExampleSmallControl, smallControl);
        Assert.Equal(ramming.WorkedExampleLargeControl, largeControl);
        Assert.Equal(ramming.WorkedExampleBonusDice, smallControl - largeControl);

        // …and the other direction is wrong, which is what stops this from being a restatement:
        // reading the rate as −3d per 30 Health of the *difference* gives a different bonus.
        Assert.NotEqual(ramming.WorkedExampleBonusDice,
            (ramming.WorkedExampleLargeHealth - ramming.WorkedExampleSmallHealth) / CanonicalChapterSixRules.CapitalShipControlRule.HealthPerStep
            * CanonicalChapterSixRules.CapitalShipControlRule.ControlPerStep);
    }

    /// <summary>
    /// <b>p.96's Foe example, replayed: half of a 7d Body is 4 points of damage.</b> The halving
    /// rounds up, which is the Introduction's book-wide rule on p.7 — and this example is the
    /// chapter demonstrating it rather than a coincidence to hedge against, which is what the
    /// entry's <c>ambiguity</c> used to call it. Rounding the other way gives 3, which the page
    /// does not print, so the example distinguishes the two directions on its own.
    /// </summary>
    [Fact]
    public void TheFoePilotExampleOnPageNinetySixComesOutAsPrinted()
    {
        var entry = Vehicles().Entries.Single(e => e.Id == "foe_and_minion_pilots");
        var foes = entry.FoeAndMinionPilots!;

        Assert.Equal(foes.FoeWorkedExampleDamageCapacity,
            (int)Math.Ceiling(foes.FoeWorkedExampleBody / 2.0));

        Assert.NotEqual(foes.FoeWorkedExampleDamageCapacity, foes.FoeWorkedExampleBody / 2);

        // The example is a witness for p.7 rather than a question about it, so there is nothing
        // left for a consumer to settle here.
        Assert.Null(entry.Ambiguity);
    }

    // ── The readings, each labelled and each derived ─────────────────────────

    /// <summary>
    /// <b>A reading of the page is labelled as one and kept out of the transcription.</b> Every
    /// <c>interpretation</c> block says in its own first field that it is ours, which is the
    /// discipline the Thresholds table established in <c>data/rules/play/challenge.json</c>.
    /// </summary>
    [Fact]
    public void EveryInterpretationSaysInItsOwnFirstFieldThatItIsOurs()
    {
        var found = 0;

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                if (!entry.TryGetProperty("interpretation", out var interpretation)
                    || interpretation.ValueKind != JsonValueKind.Object) continue;

                found++;

                var first = interpretation.EnumerateObject().First();
                Assert.Equal("what_this_is", first.Name);
                Assert.False(string.IsNullOrWhiteSpace(first.Value.GetString()));

                Assert.All(interpretation.EnumerateObject(),
                    p => Assert.False(string.IsNullOrWhiteSpace(p.Value.GetString())));
            }
        }

        // Positive control: there are interpretations to check. Five today — the three mundane
        // tables' shared pairing, the stock vehicles' two readings, and each feature table's.
        Assert.True(found >= 5, $"Only {found} interpretation blocks were found.");
    }

    /// <summary>
    /// <b>An ambiguity is a field, not a comment.</b> An ambiguity nobody wrote down becomes an
    /// implementation decision nobody made, so the ones this slice found are asserted by name — a
    /// file that quietly settled one of them would go red here rather than silently pick a reading.
    /// </summary>
    [Fact]
    public void TheAmbiguitiesThisSliceFoundAreOnRecord()
    {
        var recorded = new List<(string File, string Entry)>();

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var ambiguity = entry.GetProperty("ambiguity");
                if (ambiguity.ValueKind == JsonValueKind.Null) continue;

                Assert.False(string.IsNullOrWhiteSpace(ambiguity.GetString()));
                recorded.Add((file, entry.GetProperty("id").GetString()!));
            }
        }

        string[] expected =
        [
            // The granted pool is not the character's budget and its lifetime is unstated.
            "gadgets.json/gadget_build",
            // Whether the current use is counted before or after the die is rolled.
            "gadgets.json/gadget_instability",
            // A vehicular Gear Limit raised on its own, against p.87's single figure.
            "vehicles.json/vehicular_gear_limit",
            // Armor or half Toughness, with no rule for a character who has both.
            "vehicles.json/vehicle_damage_and_repair",
            // Health in multiples of thirty with no formula, and its Control rate on another page.
            "vehicles.json/capital_ships",
            // Pricing a feature the chapter invites players to invent.
            "vehicles.json/vehicle_features",
            "headquarters.json/base_features",
            // Whether the advanced-feature die stacks, and whether a standard grade earns it.
            "headquarters.json/advanced_feature_bonus",
            // The two names p.102 and p.103 give the largest Size grade, and the skipped 90 Health.
            "headquarters.json/mobile_headquarters",
            // Whether an unspent point of Teamwork carries over.
            "headquarters.json/teamwork"
        ];

        Assert.Equal(expected.Order(), recorded.Select(r => $"{r.File}/{r.Entry}").Order());
    }

    /// <summary>
    /// <b>The largest Size grade has two printed names, and the ambiguity that records it is
    /// anchored to both.</b> p.102's Mobile calls it "the Enormous Size feature" while p.103's own
    /// Size entry calls the same three Base Point grade "a truly awe-inspiring base" — so the data
    /// keys it on p.103's word, which is the entry that prices it.
    ///
    /// <para>This exists because the file said something else. The grade keys were recorded as
    /// "large, sprawling, and a third the book leaves unnamed", with <c>awe_inspiring</c> as this
    /// project's invention — and p.103 names the third exactly as much as it names the other two.
    /// The real gap is the book using two words for one grade, which is a smaller thing and a true
    /// one.</para>
    /// </summary>
    [Fact]
    public void TheLargestSizeGradeIsNamedTwiceInTheBookAndOnceInTheData()
    {
        Assert.Contains("Enormous Size", Section(102, "MOBILE"), StringComparison.Ordinal);
        Assert.Contains("awe-inspiring", Section(103, "SIZE"), StringComparison.Ordinal);

        // p.103 names all three grades it prices, which is what the interpretation now says.
        var size = Headquarters().Entries.Single(e => e.Id == "base_features").Features!
            .Single(f => f.Id == "size");

        var printed = Section(103, "SIZE");

        foreach (var grade in size.CostRange!.Keys)
            Assert.Contains(grade.Replace('_', '-'), printed, StringComparison.OrdinalIgnoreCase);

        // The Health ladder keys off the same grades, so the two entries have to agree on them.
        var mobile = Headquarters().Entries.Single(e => e.Id == "mobile_headquarters").MobileHeadquarters!;

        Assert.Contains("large", size.CostRange.Keys, StringComparer.Ordinal);
        Assert.Contains("sprawling", size.CostRange.Keys, StringComparer.Ordinal);
        Assert.True(mobile.HealthWithLargeSize < mobile.HealthWithSprawlingSize);
        Assert.True(mobile.LargestSizeCannotBeAttackedOrDestroyed);
    }

    /// <summary>
    /// <b>No entry records a halving as an open question, because the book answers every one of
    /// them on p.7.</b> The Introduction's glossary reads "Half: Whenever we refer to half of an
    /// odd number (or half of an odd number of dice), always round up, regardless of the context" —
    /// and "regardless of the context" is the whole of the point, so a chapter that halves a
    /// number without repeating the rule has not left anything unsaid.
    ///
    /// <para>Two entries here recorded one anyway: the Gadget per-issue ceiling ("half your
    /// Intellect") and the Foe damage capacity ("only half as much damage as usual"). Both
    /// ambiguities cited p.7 and then hedged past it, and the second hedged past a worked example
    /// on its own page — p.96 halves a 7d Body and gets 4. An <c>ambiguity</c> is a standing
    /// invitation for a consumer to pick a reading in its own code, and the reading it invited
    /// here is one <c>CostCalculator</c> contradicts in eight places, every one of them a
    /// <c>Math.Ceiling</c>.</para>
    ///
    /// <para>The glossary sentence is read out of the corpus rather than restated, so this fails if
    /// the rule this rests on is not in the book.</para>
    /// </summary>
    [Fact]
    public void NoEntryLeavesAHalvingOpenThatPageSevenAlreadySettles()
    {
        var glossary = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulebookPath, "ch00-introduction.json")));

        var text = string.Join(" ", glossary.RootElement.GetProperty("sections").EnumerateArray()
            .Select(s => s.GetProperty("text").GetString() ?? ""));

        Assert.Contains(CanonicalChapterSixRules.BookWideHalvingRule, text, StringComparison.Ordinal);

        foreach (var (file, _) in Coverage)
        {
            using var document = JsonDocument.Parse(Raw(file));

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var ambiguity = entry.GetProperty("ambiguity");
                if (ambiguity.ValueKind == JsonValueKind.Null) continue;

                Assert.DoesNotContain("round", ambiguity.GetString()!, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// <b>An entry that defers to another chapter carries references and nothing else.</b> Copying
    /// Chapter 4's chase rules in here would create a second transcription to disagree with the
    /// first, which is the policy <c>resolve.json</c>'s <c>spend_combat</c> broke the first time.
    /// </summary>
    [Fact]
    public void TheChaseEntryDefersToChapterFourRatherThanTranscribingIt()
    {
        var chases = Vehicles().Entries.Single(e => e.Id == "vehicle_chases").Chases!;

        Assert.False(chases.TranscribedHere);
        Assert.Equal(4, chases.DeferredToChapter);

        // What it does carry is the two figures p.95 adds to Chapter 4's rules and nothing beyond
        // them: which figure a chase uses on open ground, and which it uses on winding ground.
        Assert.False(string.IsNullOrWhiteSpace(chases.OpenTerrainUses));
        Assert.False(string.IsNullOrWhiteSpace(chases.WindingTerrainUses));
    }

    /// <summary>
    /// <b>The two second currencies, and the fact that neither is Hero Points.</b> A consumer that
    /// added a Vehicle Point to a Hero Point total would have made a category error, so each file
    /// states the exchange rate once and this holds it to the Perk it is the detail of.
    /// </summary>
    [Fact]
    public void EachSecondCurrencyNamesThePerkThatBuysItAndTheRate()
    {
        var rules = new RulesFixture().Rules;

        var vehicle = Vehicles().Entries.Single(e => e.Id == "unique_vehicle_perk").UniqueVehiclePerk!;
        var perk = rules.GetPerk(vehicle.PerkId);

        Assert.NotNull(perk);
        Assert.Equal("per_unit", perk.CostType);
        Assert.Equal(1, perk.CostPerUnit);
        Assert.Contains(vehicle.VehiclePointsPerHeroPoint.ToString(CultureInfo.InvariantCulture),
            perk.UnitLabel ?? "", StringComparison.Ordinal);

        var headquarters = Headquarters().Entries.Single(e => e.Id == "headquarters_perk").HeadquartersPerk!;
        var hqPerk = rules.GetPerk(headquarters.PerkId);

        Assert.NotNull(hqPerk);
        Assert.Equal("per_unit", hqPerk.CostType);
        Assert.Equal(1, hqPerk.CostPerUnit);
        Assert.Contains(headquarters.BasePointsPerHeroPoint.ToString(CultureInfo.InvariantCulture),
            hqPerk.UnitLabel ?? "", StringComparison.Ordinal);

        // The one feature that costs nothing in Base Points and forces a Hero Point purchase.
        var mobile = Headquarters().Entries.Single(e => e.Id == "mobile_headquarters").MobileHeadquarters!;
        Assert.Equal(0, mobile.BasePointCost);
        Assert.True(mobile.IsAlsoAUniqueVehicle);
        Assert.Contains("Unique Vehicle", mobile.VehicleCharacteristicsBoughtWith, StringComparison.Ordinal);
    }

    /// <summary>
    /// The models exist to be deserialized by reflection and nothing calls their properties, so a
    /// property nobody reads is invisible. This asserts each file's models were actually populated
    /// — the positive control the coverage guard needs, because a model whose properties all
    /// silently defaulted would satisfy every Disallow check in this file.
    /// </summary>
    [Fact]
    public void TheModelsAreActuallyPopulatedRatherThanSilentlyDefaulted()
    {
        foreach (var (file, model) in Coverage)
        {
            var loaded = JsonSerializer.Deserialize(Raw(file), model, Lenient)!;

            var header = (Chapter6Header)model.GetProperty("Header")!.GetValue(loaded)!;
            Assert.False(string.IsNullOrWhiteSpace(header.WhatThisIs));
            Assert.False(string.IsNullOrWhiteSpace(header.NotLogic));
            Assert.NotEmpty(header.VerifiedFieldsClosedList);
            Assert.Contains("Ch.6", header.SourceRef, StringComparison.Ordinal);

            var entries = (System.Collections.IEnumerable)model.GetProperty("Entries")!.GetValue(loaded)!;
            var count = 0;

            foreach (Chapter6Entry entry in entries)
            {
                count++;
                Assert.False(string.IsNullOrWhiteSpace(entry.Id));
                Assert.False(string.IsNullOrWhiteSpace(entry.Name));
                Assert.False(string.IsNullOrWhiteSpace(entry.Kind));
                Assert.False(string.IsNullOrWhiteSpace(entry.Description));

                // Exactly one payload or table property is populated per entry, which is what the
                // path keys in the canonical file assume.
                var populated = entry.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.DeclaringType == entry.GetType())
                    .Count(p => p.GetValue(entry) is not null);

                Assert.Equal(1, populated);
            }

            Assert.True(count >= 5, $"{file} deserialized only {count} entries");
        }
    }

    /// <summary>
    /// <b>Two currencies means two headers say so.</b> Gadgets is the exception and says nothing
    /// about a second currency, because a Gadget pays Hero Points out rather than charging them —
    /// which is the one thing about this chapter that reads backwards on first meeting.
    /// </summary>
    [Fact]
    public void OnlyTheTwoFilesWithASecondCurrencyDeclareOne()
    {
        Assert.Null(Gadgets().Header.TwoCurrencies);
        Assert.NotNull(Vehicles().Header.TwoCurrencies);
        Assert.NotNull(Headquarters().Header.TwoCurrencies);

        var build = Gadgets().Entries.Single(e => e.Id == "gadget_build").Build!;

        // The Item Con is what gear is, not a discount to claim — the same rule gear_features.json
        // already lives under, and the page states it in as many words.
        Assert.Equal("item", build.DefaultCon);
        Assert.False(build.DefaultConIsCredited);
    }
}
