using System.Text.RegularExpressions;
using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Item 21's slice one: <see cref="CharacterVariant"/> round-trips through
/// <see cref="CharacterSheetJson"/> byte-identically for every character that carries none, and
/// the pricing and derived-stats arithmetic stays blind to it — in the shape of
/// <c>PresentationFlagsTests.NoRulesCodeReadsAPresentationFlag</c>, but scoped to only the two
/// files that actually price and derive a character: <see cref="CharacterValidator"/> is
/// deliberately not scanned here, because it is the one piece of rules code that must be able to
/// read <see cref="CharacterSheet.Variant"/> — <c>UNKNOWN_VARIANT_KIND</c> and
/// <c>VARIANT_WITHOUT_ROOT</c> are structural checks about the field itself, not a rule that
/// branches on what it says.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class CharacterVariantTests
{
    private readonly RulesFixture _f;

    public CharacterVariantTests(RulesFixture f) => _f = f;

    // ── Byte-identical round trip for every character that carries no Variant ──────────

    /// <summary>
    /// Every published Hero, plus both sample characters, is read, written back out, and the
    /// bytes are unchanged — a <c>null</c> <see cref="CharacterSheet.Variant"/> must not add a
    /// key to the wire shape at all, which is what lets an existing saved character or export
    /// keep meaning exactly what it always did.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPublishedHero))]
    public void APublishedHeroRoundTripsByteIdenticallyWithNoVariant(string heroName)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == heroName);
        var sheet = PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, hero);

        Assert.Null(sheet.Variant);

        var written = CharacterSheetJson.Write(sheet);
        Assert.DoesNotContain("\"Variant\"", written, StringComparison.Ordinal);

        var read = CharacterSheetJson.Read(written, strict: true);
        Assert.NotNull(read);
        Assert.Null(read!.Variant);

        // Byte-identical on a second pass, not merely "parses again": nothing added, dropped or
        // reordered by writing a sheet that has already been round-tripped once.
        Assert.Equal(written, CharacterSheetJson.Write(read));
    }

    public static IEnumerable<object[]> EveryPublishedHero() =>
        PrebuiltHeroes.All.Select(h => new object[] { h.Name });

    [Fact]
    public void TheSampleCharactersRoundTripByteIdenticallyWithNoVariant()
    {
        foreach (var sheet in new[] { SampleCharacters.Hero(), SampleCharacters.Villain() })
        {
            var written = CharacterSheetJson.Write(sheet);
            Assert.DoesNotContain("\"Variant\"", written, StringComparison.Ordinal);
            Assert.Equal(written, CharacterSheetJson.Write(CharacterSheetJson.Read(written, strict: true)!));
        }
    }

    // ── The link itself round-trips when one is set ─────────────────────────────────────

    [Theory]
    [InlineData(CharacterVariant.Later)]
    [InlineData(CharacterVariant.AsSeenBy)]
    [InlineData(CharacterVariant.AlternateForm)]
    public void ALinkRoundTripsThroughStrictAndLenientReading(string kind)
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Variant = new CharacterVariant("c_root123", kind);

        foreach (var strict in new[] { true, false })
        {
            var read = CharacterSheetJson.Read(CharacterSheetJson.Write(sheet), strict);

            Assert.NotNull(read!.Variant);
            Assert.Equal("c_root123", read.Variant!.OfCharacterId);
            Assert.Equal(kind, read.Variant.Kind);
        }
    }

    /// <summary>
    /// A payload written before this field existed has no <c>Variant</c> key at all, and has to
    /// keep meaning "no link" rather than throwing under the strict reader a submitted file goes
    /// through.
    /// </summary>
    [Fact]
    public void APayloadWithNoVariantKeyReadsBackAsNoLink()
    {
        var withoutTheField = JsonSerializer.Serialize(new
        {
            SelectedTierId = "standard",
            AbilityRanks = new Dictionary<string, int> { ["might"] = 2 }
        });

        var read = CharacterSheetJson.Read(withoutTheField, strict: true);

        Assert.NotNull(read);
        Assert.Null(read!.Variant);
    }

    // ── Validator: the two structural findings ──────────────────────────────────────────

    private CharacterSheet Legal()
    {
        var sheet = _f.LegalSheet();
        sheet.Flaws.Clear();
        sheet.Flaws.Add(new SelectedFlaw("code"));
        return sheet;
    }

    [Fact]
    public void ABlankRootIsRefused()
    {
        var sheet = Legal();
        sheet.Variant = new CharacterVariant("", CharacterVariant.Later);

        var issue = Assert.Single(_f.Validator.Validate(sheet).Issues, i => i.Code == "VARIANT_WITHOUT_ROOT");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    [Fact]
    public void AnUnknownKindIsRefusedAndOffersTheThreeKinds()
    {
        var sheet = Legal();
        sheet.Variant = new CharacterVariant("c_root", "cursed_mirror");

        var issue = Assert.Single(_f.Validator.Validate(sheet).Issues, i => i.Code == "UNKNOWN_VARIANT_KIND");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(CharacterVariant.Kinds.Order(), issue.Options.Order());

        foreach (var kind in issue.Options)
        {
            var repaired = Legal();
            repaired.Variant = new CharacterVariant("c_root", kind);
            Assert.DoesNotContain(_f.Validator.Validate(repaired).Issues, i => i.Code == "UNKNOWN_VARIANT_KIND");
        }
    }

    [Theory]
    [InlineData(CharacterVariant.Later)]
    [InlineData(CharacterVariant.AsSeenBy)]
    [InlineData(CharacterVariant.AlternateForm)]
    public void AWellFormedLinkReportsNeitherStructuralFinding(string kind)
    {
        var sheet = Legal();
        sheet.Variant = new CharacterVariant("c_root", kind);

        var issues = _f.Validator.Validate(sheet).Issues.Select(i => i.Code).ToList();
        Assert.DoesNotContain("VARIANT_WITHOUT_ROOT", issues);
        Assert.DoesNotContain("UNKNOWN_VARIANT_KIND", issues);
    }

    [Fact]
    public void NoVariantAtAllReportsNeitherFinding()
    {
        var sheet = Legal();
        Assert.Null(sheet.Variant);

        var issues = _f.Validator.Validate(sheet).Issues.Select(i => i.Code).ToList();
        Assert.DoesNotContain("VARIANT_WITHOUT_ROOT", issues);
        Assert.DoesNotContain("UNKNOWN_VARIANT_KIND", issues);
    }

    // ── The engine stays blind: cost and derived stats never move ───────────────────────

    /// <summary>
    /// The design's own claim, checked rather than trusted: a character costs and derives
    /// exactly the same with a <see cref="CharacterVariant"/> on it as without one.
    /// </summary>
    [Theory]
    [InlineData(CharacterVariant.Later)]
    [InlineData(CharacterVariant.AsSeenBy)]
    [InlineData(CharacterVariant.AlternateForm)]
    public void ALinkChangesNoCostAndNoDerivedFigure(string kind)
    {
        var plain = RulesFixture.StandardSheet();
        plain.AbilityRanks["might"] = 6;

        var linked = RulesFixture.StandardSheet();
        linked.AbilityRanks["might"] = 6;
        linked.Variant = new CharacterVariant("c_root", kind);

        Assert.Equal(_f.Costs.TotalCost(plain), _f.Costs.TotalCost(linked));
        Assert.Equal(_f.Derived.CalculateEdge(plain), _f.Derived.CalculateEdge(linked));
        Assert.Equal(_f.Derived.CalculateHealth(plain), _f.Derived.CalculateHealth(linked));
        Assert.Equal(_f.Derived.CalculateResolve(plain), _f.Derived.CalculateResolve(linked));
    }

    /// <summary>
    /// <b>The guard, in the shape of <c>PresentationFlagsTests.NoRulesCodeReadsAPresentationFlag</c>,
    /// scoped to the two files that actually turn a sheet into a number.</b> Scanning all of
    /// <c>engine/</c> would also flag <see cref="CharacterSheet"/> (the declaration),
    /// <see cref="CharacterValidator"/> (which must read it to report the two structural codes
    /// above) and <see cref="CharacterSheetJson"/> (which round-trips the whole type by
    /// reflection and names no property at all).
    ///
    /// <para><b>A plain <c>Contains("Variant")</c> would false-positive on every one of these
    /// files</b>, because <c>SelectedProCon.VariantKey</c> and <c>SelectedPower.CostVariantKey</c>
    /// are an unrelated, long-standing concept — a variable-cost Pro or Con's chosen variant —
    /// and both identifiers contain the substring "Variant". So this looks for the type name
    /// <c>CharacterVariant</c> and for a member access spelled exactly <c>.Variant</c> (a
    /// trailing word boundary excludes <c>.VariantKey</c>), rather than for the bare word.</para>
    /// </summary>
    [Fact]
    public void CostAndDerivedStatsNeverReadTheVariantField()
    {
        var scanned = new[]
        {
            Path.Combine(RulesFixture.RepoRoot, "engine", "CostCalculator.cs"),
            Path.Combine(RulesFixture.RepoRoot, "engine", "DerivedStatsCalculator.cs"),
        };

        var pattern = new Regex(@"\bCharacterVariant\b|\.Variant\b(?!Key)",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        var offenders = new List<string>();

        foreach (var file in scanned)
        {
            Assert.True(File.Exists(file), $"expected to find {file}");
            if (pattern.IsMatch(File.ReadAllText(file)))
                offenders.Add(Path.GetFileName(file));
        }

        foreach (var dir in new[] { "sheets", "play" })
        {
            var path = Path.Combine(RulesFixture.RepoRoot, dir);
            if (!Directory.Exists(path)) continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                if (pattern.IsMatch(File.ReadAllText(file)))
                    offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0,
            "These price or derive ONE character and must not be able to see its variant link. "
            + "A link's rule consequences are about a set of sheets and live in "
            + "engine/AlternateForms.cs, which is handed a roster:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The positive control: the pattern really does find the thing it is looking for, and does
    /// not confuse it with the unrelated <c>VariantKey</c> concept living in the very files this
    /// guard reads.
    /// </summary>
    [Fact]
    public void TheBlindnessScanFindsWhatItLooksForAndIgnoresVariantKey()
    {
        var pattern = new Regex(@"\bCharacterVariant\b|\.Variant\b(?!Key)",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.Matches(pattern, "var v = sheet.Variant;");
        Assert.Matches(pattern, "var v = new CharacterVariant(rootId, kind);");
        Assert.DoesNotMatch(pattern, "selection.CostVariantKey");
        Assert.DoesNotMatch(pattern, "new SelectedProCon(id, VariantKey: variant)");

        // The real files really do carry VariantKey without tripping the guard — proving the
        // exemption is real rather than merely asserted above.
        var costCalculator = File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "engine", "CostCalculator.cs"));
        Assert.Contains("VariantKey", costCalculator, StringComparison.Ordinal);
        Assert.DoesNotMatch(pattern, costCalculator);
    }
}
