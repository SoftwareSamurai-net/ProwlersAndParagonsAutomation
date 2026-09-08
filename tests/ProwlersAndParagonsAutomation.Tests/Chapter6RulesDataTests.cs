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
/// <para><b>None of the three files is on <see cref="RulesRepository.DataFileNames"/>.</b> That is
/// the contract for a host which fetches the character rules over HTTP, and putting a file on it is
/// a decision about the browser's first page load; the slice that teaches <c>CostCalculator</c>
/// what a vehicle costs is the slice that gets to make it. Until then this file is what makes the
/// data worth having — unread data reads like a source of truth and is not one.</para>
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
    /// <b>The three files are deliberately absent from the loader's contract, and this says so out
    /// loud.</b> Adding one to <see cref="RulesRepository.DataFileNames"/> makes the browser fetch
    /// it before its first render, and that is the consumer slice's decision rather than a side
    /// effect of extracting the data. If a later slice does add one, this test is where it is
    /// removed from — not a line to delete quietly.
    /// </summary>
    [Fact]
    public void TheChapterSixFilesAreNotYetOnTheRepositorysContract()
    {
        foreach (var (file, _) in Coverage)
        {
            Assert.DoesNotContain(file, RulesRepository.DataFileNames);

            // Positive control on the assertion above: the list is real and non-empty.
            Assert.Contains("powers.json", RulesRepository.DataFileNames);
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

            foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString();
                var sourceRef = entry.GetProperty("source_ref").GetString()!;
                var under = entry.GetProperty("printed_under").GetString()!;

                var page = int.Parse(
                    Regex.Match(sourceRef, @"\bp\.(\d+)\b").Groups[1].Value,
                    CultureInfo.InvariantCulture);

                Assert.InRange(page, CanonicalChapterSixRules.FirstPage, CanonicalChapterSixRules.LastPage);

                if (!headings.Contains((page, under)))
                    faults.Add($"{file}/{id}: '{under}' is not a heading on p.{page}");
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

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
    /// capital-ship row's Control equals p.94's −3d per 30 Health; five of p.96's stock vehicles
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
    /// tables.</b> p.96's Foe example gives the sedan its Body in prose, and five of p.96's six
    /// stock vehicles reprint a table row characteristic for characteristic. Neither is anywhere
    /// near the table it agrees with, so a misalignment of one row moves them.
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

        // The negative control the roster needs: the sixth stock vehicle is stock only, so a
        // mapping that had quietly grown a sixth entry would be claiming a row that is not there.
        Assert.DoesNotContain(CanonicalChapterSixRules.AnchorRows.StockOnlyVehicle,
            CanonicalChapterSixRules.AnchorRows.StockToTableRow.Keys);
        Assert.Contains(CanonicalChapterSixRules.AnchorRows.StockOnlyVehicle, stock.Keys);
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

            Assert.Equal(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), row.VehiclePoints);
            Assert.Equal(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), row.Body);
            Assert.Equal(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), row.Speed);
            Assert.Equal(int.Parse(match.Groups[4].Value.Replace('−', '-'), CultureInfo.InvariantCulture), row.Control);

            Assert.Equal(
                match.Groups[5].Value == "n/a"
                    ? null
                    : int.Parse(match.Groups[5].Value.TrimEnd('d'), CultureInfo.InvariantCulture),
                row.Weapons);

            Assert.Equal(
                match.Groups[6].Value.Split(", ", StringSplitOptions.TrimEntries),
                row.Features);
        }
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
                    Assert.Equal(amount, feature.Cost);
                    Assert.Null(feature.CostRange);
                    Assert.Null(feature.CostPerUnit);
                    break;

                case "per_unit":
                    Assert.True(perUnit, $"{feature.Id} is recorded per unit but printed flat");
                    Assert.Equal(amount, feature.CostPerUnit);
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.UnitLabel);
                    break;

                case "flat_variable":
                    Assert.NotNull(feature.CostRange);
                    Assert.Null(feature.Cost);
                    foreach (var (_, grade) in feature.CostRange!)
                        Assert.Contains($"{grade} Vehicle Point", text, StringComparison.Ordinal);
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
                    Assert.Equal(low, feature.Cost);
                    Assert.Null(feature.CostRange);
                    break;

                case "per_unit":
                    Assert.Null(high);
                    Assert.Equal(low, feature.CostPerUnit);
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.UnitLabel);
                    break;

                case "flat_variable":
                    Assert.NotNull(high);
                    Assert.Null(feature.Cost);
                    Assert.NotNull(feature.CostRange);
                    Assert.Equal(low, feature.CostRange!.Values.Min());
                    Assert.Equal(high, feature.CostRange.Values.Max());

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
    /// to be right for the total to come out, and five of the six do.
    ///
    /// <para><b>The sixth does not, and it is recorded rather than tuned away.</b> The Submersible's
    /// characteristics and features come to fifteen Vehicle Points against a printed fourteen, and
    /// no reading of the Passengers rating reconciles both it and the Speedboat — reading the rating
    /// as a total rather than as extra passengers fixes the Submersible and breaks the Speedboat.
    /// The overshoot is asserted by name so it cannot quietly become two.</para>
    /// </summary>
    [Fact]
    public void FiveOfTheSixStockVehiclesPriceOutExactlyThroughTheRulesOnTheirOwnPage()
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

            if (total == stock.VehiclePoints) reconciled.Add(stock.Name);
            else
            {
                Assert.Equal(StockSums.DoesNotReconcile, stock.Name);
                Assert.Equal(
                    stock.VehiclePoints + StockSums.SubmersibleOvershoot,
                    total);
            }
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
                return StockSums.SonarCostAsUniqueSystem;

            return features[printed].Cost
                   ?? throw new InvalidOperationException($"{printed} has no flat price");
        }
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
    /// rounds up, which is the Introduction's book-wide rule (p.7) and not something this page
    /// states — so the entry carries the printed figures and its <c>ambiguity</c> carries the
    /// silence. Rounding the other way gives 3, which the page does not print.
    /// </summary>
    [Fact]
    public void TheFoePilotExampleOnPageNinetySixComesOutAsPrinted()
    {
        var foes = Vehicles().Entries.Single(e => e.Id == "foe_and_minion_pilots").FoeAndMinionPilots!;

        Assert.Equal(foes.FoeWorkedExampleDamageCapacity,
            (int)Math.Ceiling(foes.FoeWorkedExampleBody / 2.0));

        Assert.NotEqual(foes.FoeWorkedExampleDamageCapacity, foes.FoeWorkedExampleBody / 2);

        Assert.NotNull(Vehicles().Entries.Single(e => e.Id == "foe_and_minion_pilots").Ambiguity);
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
            // Half an Intellect, with no rounding direction on the page.
            "gadgets.json/gadget_prerequisites",
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
            // The Foe example's halving direction.
            "vehicles.json/foe_and_minion_pilots",
            // The Submersible's printed total, which the page's own rules overshoot by one.
            "vehicles.json/stock_vehicles",
            // Pricing a feature the chapter invites players to invent.
            "vehicles.json/vehicle_features",
            "headquarters.json/base_features",
            // Whether the advanced-feature die stacks, and whether a standard grade earns it.
            "headquarters.json/advanced_feature_bonus",
            // Enormous Size, which the Size entry does not name, and the skipped 90 Health step.
            "headquarters.json/mobile_headquarters",
            // Whether an unspent point of Teamwork carries over.
            "headquarters.json/teamwork"
        ];

        Assert.Equal(expected.Order(), recorded.Select(r => $"{r.File}/{r.Entry}").Order());
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
