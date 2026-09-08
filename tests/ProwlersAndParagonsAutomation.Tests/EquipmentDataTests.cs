using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Chapter 6's creation-side equipment, pp.88-93, held to the page.</b>
/// <c>data/rules/gear.json</c> carries armour, shields, the Weapon Features glossary, the
/// mundane equipment list, Custom Gear and p.93's Pros and Cons rule, and this is what checks it.
///
/// <para><b>The application reads that file now</b> — it is on
/// <see cref="RulesRepository.DataFileNames"/>, <see cref="RulesRepository.Equipment"/> answers it
/// and <see cref="GearCatalogue"/> offers its rows. These tests are still what holds it to the
/// page: the repository is lenient at runtime like it is for every other file, and strictness
/// lives here. It was verified first and consumed second, which is the order the 141 Powers were
/// extracted in.</para>
///
/// <para><b>The three weapons tables in that file are a copy</b> of the ones in
/// <c>data/rules/play/equipment.json</c>. Neither side may read the other's store, so a figure both
/// need is stored twice with a guard between the copies —
/// <see cref="TheWeaponTablesAreACopyOfThePlayStoresAndAreHeldEqualToIt"/>.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class EquipmentDataTests
{
    private readonly RulesFixture _f;

    public EquipmentDataTests(RulesFixture fixture) => _f = fixture;

    private const string FileName = "gear.json";
    private const int ChapterFirstPage = 87;
    private const int ChapterLastPage = 104;

    private static string RulebookPath => Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

    private static JsonSerializerOptions Options(bool strict) => new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        UnmappedMemberHandling      = strict
            ? JsonUnmappedMemberHandling.Disallow
            : JsonUnmappedMemberHandling.Skip
    };

    private static string Json() => File.ReadAllText(Path.Combine(RulesFixture.DataPath, FileName));

    private static EquipmentDataModel Equipment() =>
        JsonSerializer.Deserialize<EquipmentDataModel>(Json(), Options(strict: false))!;

    // ── Where the file lives, and what does not read it ──────────────────────

    /// <summary>
    /// <b>Every field is read by a model, or it is unread data pretending to be a source of
    /// truth.</b> <see cref="RulesFileCoverageTests"/> makes the same check for every loaded file
    /// and now covers this one too. It is kept here as well because the rest of this file is what a
    /// reader of <c>gear.json</c> comes to, and a strict-deserialise failure reported beside the
    /// page checks says which file drifted without a second lookup.
    /// </summary>
    [Fact]
    public void EveryFieldInTheEquipmentFileIsReadByAModel()
    {
        var ex = Record.Exception(
            () => JsonSerializer.Deserialize<EquipmentDataModel>(Json(), Options(strict: true)));

        Assert.True(ex is null,
            $"{FileName} carries a field no model reads, so nothing can hold it to the rulebook: "
            + ex?.Message);
    }

    /// <summary>
    /// <b>The file is in the character rules store and every self-loading host fetches it.</b>
    /// Both halves matter: it sits at the top level of <c>data/rules/</c> so every csproj's
    /// non-recursive glob copies it, and it is on
    /// <see cref="RulesRepository.DataFileNames"/> so the browser has it before its first render —
    /// the engine is synchronous, so a repository built on a half-loaded set throws on whichever
    /// collection is touched first.
    ///
    /// <para><b>This test was the inverse of itself until the Gear step could pick from the
    /// catalogue.</b> It asserted the file was <em>off</em> the list, beside a
    /// <see cref="RulesSourceTests"/> exemption naming it — the pair the earlier slice left so that
    /// removing one without the other would fail. Both moved together.</para>
    /// </summary>
    [Fact]
    public void TheEquipmentFileIsOnTheRepositorysLoadListAndReallyLoads()
    {
        Assert.True(File.Exists(Path.Combine(RulesFixture.DataPath, FileName)));

        // Positive control: the list is the real one and holds the files that were loaded before.
        Assert.Contains("gear_features.json", RulesRepository.DataFileNames);

        Assert.Contains(FileName, RulesRepository.DataFileNames);

        // And it loads through the repository rather than only through this file's own reader,
        // which is the half a name on a list does not prove.
        Assert.Equal(CanonicalArmourRules.ArmorTable.RowCount, _f.Rules.Equipment.ArmorTable.Rows.Count);
    }

    /// <summary>
    /// The header is the file's own account of itself, and a claim it stops making is a claim
    /// nobody reads. Each of its strings has to be there, and the three load-bearing ones have to
    /// still say what they are for.
    /// </summary>
    [Fact]
    public void TheHeaderSaysWhyTheFileIsHereWhatIsCopiedAndWhatWasLeftOut()
    {
        var header = Equipment().Header;

        Assert.All(
            new[]
            {
                header.WhatThisIs, header.WhyHereAndNotInThePlayStore,
                header.TheWeaponTablesAreACopyAndThatIsDeliberate, header.LoadedBy,
                header.PlacementNote, header.DescriptionsAreOurs, header.DeliberatelyOmitted,
                header.SourceRef
            },
            text => Assert.False(string.IsNullOrWhiteSpace(text)));

        // The copy rule names the other file, or a reader cannot find the thing they must edit too.
        Assert.Contains("data/rules/play/equipment.json",
            header.TheWeaponTablesAreACopyAndThatIsDeliberate, StringComparison.Ordinal);

        // The omissions name the pages that really are elsewhere: p.87 is the play file's, the
        // twelve custom features are gear_features.json, and pp.94-104 are not extracted here.
        Assert.Contains("p.87", header.DeliberatelyOmitted, StringComparison.Ordinal);
        Assert.Contains("gear_features.json", header.DeliberatelyOmitted, StringComparison.Ordinal);
        Assert.Contains("94", header.DeliberatelyOmitted, StringComparison.Ordinal);

        Assert.NotEmpty(header.VerifiedFieldsClosedList);
    }

    // ── Armour, p.88 ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The Armor table is derived from the corpus rather than trusted as typed.</b>
    ///
    /// <para>The book prints three columns — armour, Armor Bonus, features — and unlike the three
    /// weapons tables this one arrives from the extractor in row order, so the rows are read
    /// straight down instead of paired out of two blocks. That is still a reading of the page, which
    /// is why the entry carries an <c>interpretation</c> saying so and why the rows are on
    /// <see cref="DerivedPaths"/> rather than transcribed into
    /// <see cref="CanonicalArmourRules"/>.</para>
    ///
    /// <para><b>Two controls on the parse.</b> The block has to <em>tile</em> — nine rows matched
    /// end to end from the first character, with nothing after them that begins another row — and
    /// the parser is driven one row past the end and required to throw, because a parser that
    /// quietly returned a short list would let a truncated block agree with a truncated
    /// expectation.</para>
    /// </summary>
    [Fact]
    public void TheArmorTableIsDerivedFromTheCorpusColumns()
    {
        var block = ChapterSixSections()
            .Single(s => s.Page == 88 && s.Heading.Contains("ARMOR BONUS FEATURES", StringComparison.Ordinal));

        var table = Equipment().ArmorTable;

        // The count is transcribed rather than derived: a derivation cannot notice a table that has
        // lost half of itself when the expectation lost the same half.
        Assert.Equal(CanonicalArmourRules.ArmorTable.RowCount, table.RowCount);
        Assert.Equal(CanonicalArmourRules.ArmorTable.RowCount, table.Rows.Count);

        var (rows, rest) = ArmorRows(block.Text, CanonicalArmourRules.ArmorTable.RowCount);

        var next = ArmorRow().Match(rest);
        Assert.False(next.Success && next.Index == 0,
            $"the corpus block carries another row after the {rows.Count} the table is supposed to "
            + $"have — '{next.Value}'");

        for (var i = 0; i < rows.Count; i++)
        {
            var (category, name, bonus, feature) = rows[i];

            Assert.Equal(name, table.Rows[i].Name);
            Assert.Equal(category, table.Rows[i].Category);
            Assert.Equal(bonus, table.Rows[i].ArmorBonusDice);
            Assert.Equal(feature is null ? [] : new[] { feature }, table.Rows[i].Features);
        }

        // The anchor row, transcribed in full, read out of the derived rows rather than out of the
        // shipped file: a column that had slipped by one row moves it.
        var anchor = rows.Single(r =>
            string.Equals(r.Name, CanonicalArmourRules.ArmorTable.AnchorRowName, StringComparison.Ordinal));

        Assert.Equal(CanonicalArmourRules.ArmorTable.AnchorRowCategory, anchor.Category);
        Assert.Equal(CanonicalArmourRules.ArmorTable.AnchorRowBonusDice, anchor.Bonus);
        Assert.Equal(CanonicalArmourRules.ArmorTable.AnchorRowFeature, anchor.Feature);

        // Every era the book prints, and only those.
        Assert.Equal(
            CanonicalArmourRules.ArmorTable.Categories.Order(StringComparer.Ordinal),
            rows.Select(r => r.Category).Distinct().Order(StringComparer.Ordinal));

        // And the parser really can run out.
        Assert.Throws<InvalidOperationException>(
            () => ArmorRows(block.Text, CanonicalArmourRules.ArmorTable.RowCount + 1));
    }

    /// <summary>
    /// <b>The Gear Limit question p.88 leaves out is answered on p.87, so the entry carries a
    /// reading and not a silence.</b>
    ///
    /// <para>p.88 gives the Armor rank as Toughness plus the bonus and never restates the limit,
    /// which was recorded here as the book's silence. It is not one: <b>the silence is p.88's and
    /// the page before it answers</b>, so an <c>ambiguity</c> there hands the consumer slice a doubt
    /// the book has already settled — and a slice that inherits a doubt tends to resolve it by
    /// guessing.</para>
    ///
    /// <para><b>The four sentences are read out of the corpus rather than quoted here</b>, so this
    /// fails if the page it rests on is not the page that is there: the limit is about equipment
    /// that boosts a Trait "(usually armor and weapons)"; the worked example fixes the order of the
    /// arithmetic at limit-plus-bonus rather than trait-capped-afterwards; the limit is said to make
    /// gear less useful to a superhuman; and the one printed exception is stated for melee weapons
    /// alone, which is what leaves armour inside the rule.</para>
    /// </summary>
    [Fact]
    public void ThePageEightySevenGearLimitAnswersWhatPageEightyEightLeavesOut()
    {
        var limits = ChapterSixSections().Single(s => s.Page == 87 && s.Heading == "GEAR LIMITS");
        var exception = ChapterSixSections().Single(s => s.Page == 87 && s.Heading == "EXCEPTION: CLOSE COMBAT");

        // The page really says the four things the reading rests on. Without these the entry below
        // is one more assertion about the file rather than about the book.
        Assert.Contains("(usually armor and weapons)", limits.Text, StringComparison.Ordinal);
        Assert.Contains("less useful for characters with superhuman", limits.Text, StringComparison.Ordinal);
        Assert.Contains("involves melee weapons", exception.Text, StringComparison.Ordinal);

        // The order of the arithmetic, derived: the default limit plus the worked example's Weapon
        // Bonus is the worked example's maximum effective rank. Capping the sum instead would give
        // the limit, not the sum, so 6 + 2 == 8 is the whole of the distinction.
        var defaultLimit = DiceFigure(limits.Text, @"default Gear Limit in most games is (\d+)d");
        var pistolBonus = DiceFigure(limits.Text, @"a pistol has a \+(\d+)d Weapon Bonus");
        var pistolMaximum = DiceFigure(limits.Text, @"maximum effective rank with a pistol is (\d+)d");

        Assert.Equal(pistolMaximum, defaultLimit + pistolBonus);
        Assert.NotEqual(pistolMaximum, defaultLimit);

        var rule = Equipment().ArmorRule;

        // A reading, labelled as one — never a fact field, because p.88 does not state it.
        Assert.NotNull(rule.Armor);
        Assert.Null(rule.Ambiguity);

        Assert.NotNull(rule.Interpretation);
        Assert.False(string.IsNullOrWhiteSpace(rule.Interpretation!.GearLimitNote));
        Assert.Contains("p.87", rule.Interpretation.GearLimitNote!, StringComparison.Ordinal);

        Assert.NotNull(rule.CorroboratedBy);
        Assert.Contains("p.87", rule.CorroboratedBy!.Single(), StringComparison.Ordinal);
    }

    private static int DiceFigure(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);

        Assert.True(match.Success, $"p.87 no longer prints a figure matching /{pattern}/.");

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Bulky and Rigid, and the whole of the difference between them: a strong wearer shrugs off
    /// weight and nobody shrugs off stiffness. Asserted as a pair, because either half alone is a
    /// statement about the file rather than about the page.
    /// </summary>
    [Fact]
    public void TheTwoArmorFeaturesDifferOnlyInWhetherStrengthExcusesTheWearer()
    {
        var features = Equipment().ArmorFeatures;

        Assert.Equal(2, features.Count);

        var bulky = features.Single(f => f.Id == "bulky");
        var rigid = features.Single(f => f.Id == "rigid");

        Assert.Equal(CanonicalArmourRules.Bulky.PenaltyDice, bulky.PenaltyDice);
        Assert.Equal(CanonicalArmourRules.Rigid.PenaltyDice, rigid.PenaltyDice);
        Assert.Equal(CanonicalArmourRules.Bulky.PenalisedRolls, bulky.PenalisedRolls);
        Assert.Equal(CanonicalArmourRules.Rigid.PenalisedRolls, rigid.PenalisedRolls);

        Assert.Equal(CanonicalArmourRules.Bulky.ExemptIfMightAtLeast, bulky.ExemptIfMightAtLeast!.Value);
        Assert.Null(rigid.ExemptIfMightAtLeast);

        // Both are bought off by the same p.93 feature, and it is really there.
        Assert.Equal(CanonicalArmourRules.Bulky.RemovableByGearFeatureId, bulky.RemovableByGearFeatureId);
        Assert.Equal(CanonicalArmourRules.Rigid.RemovableByGearFeatureId, rigid.RemovableByGearFeatureId);
        Assert.NotNull(_f.Rules.GetGearFeature(bulky.RemovableByGearFeatureId));

        // Every feature the Armor table cites is one of these two, and the table cites both — a
        // control, because "every cited feature is defined" is satisfied by a table citing none.
        var cited = Equipment().ArmorTable.Rows.SelectMany(r => r.Features).Distinct().ToList();

        Assert.Equal(2, cited.Count);
        Assert.All(cited, name =>
            Assert.Contains(features, f => string.Equals(f.Name, name, StringComparison.Ordinal)));
    }

    /// <summary>
    /// A shield is two things at once, and the file records both: the die it grants while carried,
    /// and the three rows the weapons tables print for it. <b>The rows are checked against the
    /// tables</b> rather than merely listed, so a name that stopped existing is a failure here.
    /// </summary>
    [Fact]
    public void AShieldIsADefenceAndAWeaponRowBoth()
    {
        var shield = Equipment().Shields.Shield!;

        Assert.Equal(CanonicalArmourRules.Shields.BonusDice, shield.BonusDice);
        Assert.Equal(CanonicalArmourRules.Shields.BonusName, shield.BonusName);
        Assert.Equal(CanonicalArmourRules.Shields.AppliesToDefenses, shield.AppliesToDefenses);
        Assert.Equal(CanonicalArmourRules.Shields.AppliesAgainstAttackKinds, shield.AppliesAgainstAttackKinds);
        Assert.Equal(CanonicalArmourRules.Shields.NoBenefitWhen, shield.NoBenefitWhen);
        Assert.True(shield.MayBeUsedAsAnOffHandWeapon);

        var everyRow = Equipment().WeaponTables.SelectMany(t => t.Weapons).ToList();

        // Positive control before the lookup: the tables were read at all.
        Assert.True(everyRow.Count >= 60, $"Only {everyRow.Count} weapon rows were read.");

        foreach (var name in CanonicalArmourRules.Shields.WeaponRows)
        {
            var row = everyRow.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));

            Assert.Contains(CanonicalArmourRules.Shields.WeaponFeatureId, row.Features
                .Select(f => f.ToLowerInvariant()), StringComparer.Ordinal);
        }

        // The glossary entry those rows cite grants the same single die the carried shield does.
        var feature = Equipment().WeaponFeatures
            .Single(f => f.Id == CanonicalArmourRules.Shields.WeaponFeatureId);

        Assert.Equal(shield.BonusDice, feature.ShieldBonusDice);
    }

    // ── The Weapon Features glossary, pp.88 and 90 ───────────────────────────

    /// <summary>
    /// <b>Every feature name the three tables cite is defined in the glossary, and every glossary
    /// entry is cited.</b> Both directions, because a one-way check passes on a glossary that has
    /// grown an entry the book never printed as easily as on one that has lost the entry a row
    /// needs.
    ///
    /// <para>The cited set is asserted non-empty first, and that is the positive control this whole
    /// test rests on: an empty set satisfies "every cited name is defined" perfectly.</para>
    /// </summary>
    [Fact]
    public void EveryFeatureTheWeaponTablesCiteIsDefinedInTheGlossary()
    {
        var equipment = Equipment();

        var cited = equipment.WeaponTables
            .SelectMany(t => t.Weapons)
            .SelectMany(w => w.Features)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(cited.Count >= 19,
            $"The weapons tables cite only {cited.Count} feature names, which is fewer than the "
            + "glossary defines — the tables have stopped being read and this check proves nothing.");

        var defined = equipment.WeaponFeatures
            .SelectMany(f => f.CoversNames)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(defined, defined.Distinct(StringComparer.Ordinal).ToList());
        Assert.Equal(cited, defined);

        // Eighteen entries covering nineteen names, because Area/Burst is one printed entry.
        Assert.Equal(CanonicalEquipmentCatalogue.Glossary.EntryCount, equipment.WeaponFeatures.Count);
        Assert.Equal(CanonicalEquipmentCatalogue.Glossary.NameCount, defined.Count);
        Assert.Equal(
            CanonicalEquipmentCatalogue.Glossary.EntryCount + 1,
            CanonicalEquipmentCatalogue.Glossary.NameCount);
    }

    /// <summary>
    /// Six of the eighteen entries define nothing of their own — they hand the reader back to a Pro
    /// or a Con — and five more deliver a Power at the attack rank. <b>Every one of those eleven
    /// references has to resolve in the rules data</b>, or the glossary points at nothing.
    /// </summary>
    [Fact]
    public void EveryReferenceAWeaponFeatureDefersToResolvesInTheRulesData()
    {
        var referring = Equipment().WeaponFeatures.Where(f => f.WorksLike is not null).ToList();

        // Positive control: there really are references to resolve.
        Assert.Equal(11, referring.Count);

        foreach (var feature in referring)
        {
            var reference = feature.WorksLike!;

            object? resolved = reference.Kind switch
            {
                "pro"   => _f.Rules.GetPro(reference.Id),
                "con"   => _f.Rules.GetCon(reference.Id),
                "power" => _f.Rules.GetPower(reference.Id),
                _       => null
            };

            Assert.True(resolved is not null,
                $"{feature.Id} defers to {reference.Kind} '{reference.Id}', which is not in the "
                + "rules data.");
        }

        // And the split is the printed one: six point at an option, five at a Power — Shock and
        // Stun both deliver the Stun Power, which is why the second figure is five and not four.
        Assert.Equal(6, referring.Count(f => f.WorksLike!.Kind is "pro" or "con"));
        Assert.Equal(5, referring.Count(f => f.WorksLike!.Kind == "power"));
    }

    /// <summary>
    /// The two grenades the page gives a smaller blast than the general figure, checked by the name
    /// the weapons tables actually print for each — the glossary calls them "Stun Grenades" and
    /// "Entangler Grenades" and the tables print "Grenade, Stun" and "Entangler Grenade", which is
    /// the kind of gap that leaves an exception attached to nothing.
    /// </summary>
    [Fact]
    public void TheTwoSmallBurstsNameRowsTheWeaponsTablesActuallyPrint()
    {
        var equipment = Equipment();
        var burst = equipment.WeaponFeatures.Single(f => f.Id == "area_burst");

        Assert.Equal(CanonicalEquipmentCatalogue.Burst.DiameterFeet, burst.BurstDiameterFeet);
        Assert.NotNull(burst.BurstDiameterExceptions);
        Assert.Equal(2, burst.BurstDiameterExceptions!.Count);

        var everyRow = equipment.WeaponTables.SelectMany(t => t.Weapons).ToList();

        foreach (var exception in burst.BurstDiameterExceptions)
        {
            Assert.Equal(CanonicalEquipmentCatalogue.Burst.SmallDiameterFeet, exception.DiameterFeet);

            var row = everyRow.SingleOrDefault(
                r => string.Equals(r.Name, exception.Weapon, StringComparison.Ordinal));

            Assert.True(row is not null,
                $"The smaller burst is recorded against '{exception.Weapon}', which is not a row in "
                + "any weapons table.");

            Assert.Contains("Burst", row!.Features);
        }

        Assert.Equal(
            CanonicalEquipmentCatalogue.Burst.SmallDiameterWeapons.Order(StringComparer.Ordinal),
            burst.BurstDiameterExceptions.Select(e => e.Weapon).Order(StringComparer.Ordinal));
    }

    // ── The copied weapons tables ────────────────────────────────────────────

    /// <summary>
    /// <b>The three weapons tables are stored twice, and this is what keeps the copies honest.</b>
    ///
    /// <para>The rows are verified data in <c>data/rules/play/equipment.json</c>, derived there from
    /// the corpus by <c>PlayRulesDataTests.TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns</c>.
    /// <c>engine/</c> may not read that file — it is under a subdirectory no csproj glob descends
    /// into, off <see cref="RulesRepository.DataFileNames"/>, and <c>PlayPayloadTests</c> refuses
    /// both routes — and <c>play/</c> may not read <c>data/rules/</c>. The creation side's Gear step
    /// needs the same sixty-three rows, so they are copied here and held equal.</para>
    ///
    /// <para><b>Compared as serialized JSON rather than field by field</b>, so a field added to one
    /// copy and not the other fails too; a field-by-field comparison would silently ignore it. That
    /// is the same shape as the baked copies in <c>worker/corpus.js</c> and
    /// <c>worker/transcripts-corpus.js</c>, which are also two copies of one truth with a test
    /// between them.</para>
    /// </summary>
    [Fact]
    public void TheWeaponTablesAreACopyOfThePlayStoresAndAreHeldEqualToIt()
    {
        var play = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RulesFixture.DataPath, "play", "equipment.json")));

        var playTables = play.RootElement.GetProperty("entries").EnumerateArray()
            .Where(e => e.TryGetProperty("kind", out var kind) && kind.GetString() == "table")
            .ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("weapons"));

        using var mine = JsonDocument.Parse(Json());

        var myTables = mine.RootElement.GetProperty("weapon_tables").EnumerateArray()
            .ToDictionary(e => e.GetProperty("id").GetString()!, e => e.GetProperty("weapons"));

        // Positive controls before the comparison: both sides really hold three tables of rows, so
        // "the copies are equal" is not two empty collections agreeing.
        Assert.Equal(3, playTables.Count);
        Assert.Equal(3, myTables.Count);
        Assert.Equal(playTables.Keys.Order(StringComparer.Ordinal), myTables.Keys.Order(StringComparer.Ordinal));
        Assert.True(playTables.Values.Sum(v => v.GetArrayLength()) == 63);

        foreach (var (id, theirs) in playTables)
        {
            Assert.Equal(
                JsonSerializer.Serialize(theirs),
                JsonSerializer.Serialize(myTables[id]));
        }

        // The copy says where it came from, so whoever edits one knows to edit the other.
        foreach (var table in Equipment().WeaponTables)
        {
            Assert.Equal("data/rules/play/equipment.json", table.CopiedFrom);
            Assert.Equal(table.Weapons.Count, table.RowCount);
            Assert.Contains("copy", table.Interpretation?.CopyRuleNote ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Equipment, p.91 ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>The list is derived from the corpus, and what it does not print is the finding.</b>
    ///
    /// <para>Thirty-six items and not one price or availability among them: "free and untracked" is
    /// p.87's rule, and the list corroborates it by being a list of things rather than of costs.
    /// That is why <c>CostCalculator.GearCost</c> returning 0 for a plain item is correct rather
    /// than a gap, and why <c>ChooseGearStep</c> takes free text.</para>
    /// </summary>
    [Fact]
    public void TheEquipmentListIsDerivedFromTheCorpusAndPricesNothing()
    {
        var block = ChapterSixSections().Single(s => s.Page == 91 && s.Heading == "EQUIPMENT");

        var printed = Regex.Matches(block.Text, @"(?<name>[A-Z][A-Za-z]*(?:[ ,/]+[A-Za-z]+)*): ")
            .Select(m => m.Groups["name"].Value)
            .ToList();

        var catalogue = Equipment().EquipmentCatalogue;

        Assert.Equal(CanonicalEquipmentCatalogue.MundaneGear.ItemCount, printed.Count);
        Assert.Equal(CanonicalEquipmentCatalogue.MundaneGear.ItemCount, catalogue.Items.Count);
        Assert.Equal(CanonicalEquipmentCatalogue.MundaneGear.ItemCount, catalogue.MundaneGear.ItemCount);

        // Names and printed order both, because a list read out of order is a list nobody can
        // check a row of against the page.
        Assert.Equal(printed, catalogue.Items.Select(i => i.Name).ToList());

        // No item carries a price or an availability, in the file or on the page.
        Assert.Equal(0, catalogue.MundaneGear.PrintedPriceCount);
        Assert.Equal(0, catalogue.MundaneGear.PrintedAvailabilityCount);
        Assert.DoesNotContain("Hero Point", block.Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(catalogue.MundaneGear.CostsHeroPoints);
        Assert.False(catalogue.MundaneGear.IsTracked);

        // The rule the list is an example of, from p.87 and p.91 together.
        Assert.Equal(
            CanonicalEquipmentCatalogue.MundaneGear.AssumedCarriedForTalentsAtRank,
            catalogue.MundaneGear.AssumedCarriedForTalentsAtRank);

        // The four Perks the GM weighs are real Perks, not names invented here.
        Assert.All(catalogue.MundaneGear.PerksTheGmWeighs, id => Assert.NotNull(_f.Rules.GetPerk(id)));
    }

    /// <summary>
    /// The dozen or so items on p.91 that carry a real mechanical effect, with their figures. The
    /// rest are props, and asserting "some item has a bonus" would be satisfied by any one of them.
    ///
    /// <para><b>Every Power an item grants resolves in <c>powers.json</c></b>, which is the check
    /// that stops a printed parenthetical being recorded as an id that does not exist. Resolving
    /// the printed names to ids is a reading and the entry's <c>interpretation</c> says so —
    /// "Acute Hearing" is <c>super_senses_acute</c>, whose printed name is Acute (X).</para>
    /// </summary>
    [Fact]
    public void TheItemsThatCarryAMechanicalEffectCarryTheFiguresThePagePrints()
    {
        var items = Equipment().EquipmentCatalogue.Items.ToDictionary(i => i.Id, StringComparer.Ordinal);

        Assert.Equal(2, items["climbing_claws"].BonusDice);
        Assert.Equal(4, items["crowbar"].BonusDice);
        Assert.Equal(5, items["handcuffs"].BreakThreshold);
        Assert.Equal("Inhuman", items["handcuffs"].BreakThresholdLabel);
        Assert.Equal(4, items["zip_tie"].BreakThreshold);
        Assert.Equal("Brutal", items["zip_tie"].BreakThresholdLabel);

        // Rappelling Gear's Easy (0) is the roll that uses it, not a threshold to break it, and the
        // page names the Trait. EveryThresholdOnAnItemIsTheKindOfRollThePagePrints is why.
        Assert.Null(items["rappelling_gear"].BreakThreshold);
        Assert.Equal(0, items["rappelling_gear"].UseThreshold);
        Assert.Equal("Easy", items["rappelling_gear"].UseThresholdLabel);
        Assert.Equal("Agility", items["rappelling_gear"].UseTrait);

        // The one granted Power the page prints a rank for.
        Assert.Equal(9, items["parabolic_microphone"].GrantedPowerRank);
        Assert.Equal(
            1, Equipment().EquipmentCatalogue.Items.Count(i => i.GrantedPowerRank is not null));

        var granting = Equipment().EquipmentCatalogue.Items
            .Where(i => i.GrantsPowers is { Count: > 0 })
            .ToList();

        // Positive control: there are items granting Powers to resolve.
        Assert.True(granting.Count >= 6, $"Only {granting.Count} items grant a Power.");

        foreach (var item in granting)
        {
            foreach (var id in item.GrantsPowers!)
            {
                Assert.True(_f.Rules.GetPower(id) is not null,
                    $"{item.Id} grants '{id}', which is not a Power in powers.json.");
            }
        }
    }

    /// <summary>
    /// <b>Three items on p.91 print a Label (n) figure and they are not all the same kind of
    /// figure.</b> Handcuffs' Inhuman (5) and Zip Tie's Brutal (4) are thresholds "to break" the
    /// item; Rappelling Gear's Easy (0) is the Agility roll that <em>uses</em> it. Recording that
    /// third one as a break threshold — which is how it was recorded — says the gear falls apart on
    /// a roll nobody fails, and drops the Trait the page names.
    ///
    /// <para><b>Nothing above could see it.</b> Every check on these items reads a value out of the
    /// file and compares it to a figure typed beside it, and 0 and "Easy" are both correct figures;
    /// the defect is which field they are in, and a field name is not a value. So this reads each
    /// item's own printed sentence out of the corpus and asks what kind of roll it describes.</para>
    ///
    /// <para>Sliced by the same name regex
    /// <see cref="TheEquipmentListIsDerivedFromTheCorpusAndPricesNothing"/> tiles the list with, so
    /// the two agree about where an item's text begins and ends.</para>
    /// </summary>
    [Fact]
    public void EveryThresholdOnAnItemIsTheKindOfRollThePagePrints()
    {
        var printed = PrintedItemSentences();
        var items = Equipment().EquipmentCatalogue.Items;

        // Positive controls on the slice: every item is there, and a sentence really did come with
        // each of them — an empty haystack would pass every assertion below.
        Assert.Equal(items.Count, printed.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(printed[i.Name])));

        var breaking = items.Where(i => i.BreakThreshold is not null).ToList();
        var using_ = items.Where(i => i.UseThreshold is not null).ToList();

        // Positive controls on the split: there are some of each, and no item claims to be both.
        Assert.Equal(2, breaking.Count);
        Assert.Single(using_);
        Assert.DoesNotContain(items, i => i.BreakThreshold is not null && i.UseThreshold is not null);

        var faults = new List<string>();

        foreach (var item in breaking)
        {
            var sentence = printed[item.Name];
            var figure = $"{item.BreakThresholdLabel} ({item.BreakThreshold})";

            if (!sentence.Contains("threshold to break", StringComparison.OrdinalIgnoreCase))
                faults.Add($"{item.Id} records a break threshold and its printed text is not about breaking it");

            if (!sentence.Contains(figure, StringComparison.Ordinal))
                faults.Add($"{item.Id} records {figure}, which p.91 does not print for it");
        }

        foreach (var item in using_)
        {
            var sentence = printed[item.Name];
            var figure = $"{item.UseThresholdLabel} ({item.UseThreshold})";

            if (sentence.Contains("threshold to break", StringComparison.OrdinalIgnoreCase))
                faults.Add($"{item.Id} records a use threshold and its printed text is about breaking it");

            if (!sentence.Contains(figure, StringComparison.Ordinal))
                faults.Add($"{item.Id} records {figure}, which p.91 does not print for it");

            if (!sentence.Contains("roll", StringComparison.OrdinalIgnoreCase))
                faults.Add($"{item.Id} records a use threshold and p.91 prints no roll for it");

            if (item.UseTrait is null || !sentence.Contains(item.UseTrait, StringComparison.Ordinal))
                faults.Add($"{item.Id} records the Trait '{item.UseTrait}', which p.91 does not name for it");
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));

        // And nothing else on the list prints a Label (n) figure that neither field caught, which is
        // what stops this passing by only looking at the three it already knows about.
        var withFigures = printed
            .Where(pair => Regex.IsMatch(pair.Value, @"\b[A-Z][a-z]+ \(\d+\)"))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            breaking.Concat(using_).Select(i => i.Name).Order(StringComparer.Ordinal).ToList(),
            withFigures);
    }

    /// <summary>
    /// <b>Two of the granted Powers are bought by naming an option, and the id alone does not carry
    /// it.</b> Immunity is priced per unit because "each immunity is named and paid for separately",
    /// and Super Senses — Acute is printed "Acute (X)" with the sense chosen at purchase. So
    /// <c>grants_powers: ["immunity"]</c> on the Gas Mask and <c>["super_senses_acute"]</c> on the
    /// Parabolic Microphone were half of what p.91 prints — the Toxins and the Hearing were gone,
    /// and so was the Gas Mask's "limited to" clause, which is the difference between immunity to
    /// what you breathe and immunity to poison.
    ///
    /// <para><b>The existing check could not see it</b>: it resolves each granted id in
    /// <c>powers.json</c>, and both of these resolve. A Power that resolves and is under-specified
    /// looks exactly like one that is right.</para>
    ///
    /// <para><b>Which Powers need an option is read out of <c>powers.json</c>, not listed here</b> —
    /// a name printed "(X)" or a per-unit price is the book saying the option is the purchase. So an
    /// item that starts granting a different such Power is caught without this test being edited,
    /// and the option itself has to be a word p.91 prints in that item's own sentence.</para>
    /// </summary>
    [Fact]
    public void EveryGrantedPowerBoughtByNamingAnOptionRecordsTheOptionThePagePrints()
    {
        var printed = PrintedItemSentences();
        var items = Equipment().EquipmentCatalogue.Items;

        var needsAnOption = items
            .Where(i => (i.GrantsPowers ?? []).Any(PowerIsBoughtByNamingAnOption))
            .ToList();

        // Positive controls: the classifier finds Powers of both kinds among what the list grants,
        // so the sweep below is a distinction rather than an empty set.
        Assert.Equal(2, needsAnOption.Count);
        Assert.True(
            items.Any(i => (i.GrantsPowers ?? []).Count > 0 && !needsAnOption.Contains(i)),
            "No item grants a Power that carries its own option, so the split proves nothing.");

        var faults = new List<string>();

        foreach (var item in items)
        {
            var sentence = printed[item.Name];
            var wanted = needsAnOption.Contains(item);

            if (wanted && string.IsNullOrWhiteSpace(item.GrantedPowerSelection))
            {
                faults.Add(
                    $"{item.Id} grants a Power whose option is the purchase and records no "
                    + $"granted_power_selection; p.91 says: \"{sentence}\"");
                continue;
            }

            if (!wanted && item.GrantedPowerSelection is not null)
            {
                faults.Add($"{item.Id} records an option for a Power that carries its own");
                continue;
            }

            if (wanted && !sentence.Contains(item.GrantedPowerSelection!, StringComparison.OrdinalIgnoreCase))
                faults.Add($"{item.Id} records the option '{item.GrantedPowerSelection}', which p.91 does not print for it");

            // The narrowing, wherever the page puts one on a granted Power — and nowhere else.
            var isLimited = sentence.Contains("Power limited to", StringComparison.Ordinal);

            if (isLimited && string.IsNullOrWhiteSpace(item.GrantedPowerLimitedTo))
                faults.Add($"{item.Id}: p.91 limits the Power it grants and the file records no limit");

            if (!isLimited && item.GrantedPowerLimitedTo is not null)
                faults.Add($"{item.Id} records a limit p.91 does not put on the Power it grants");
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));

        // And the one narrowing the page does print, by name, so the sweep above is not vacuous.
        var gasMask = items.Single(i => i.Id == "gas_mask");
        Assert.Equal("Toxins", gasMask.GrantedPowerSelection);
        Assert.Contains("eyes", gasMask.GrantedPowerLimitedTo!, StringComparison.Ordinal);
        Assert.Equal("Hearing", items.Single(i => i.Id == "parabolic_microphone").GrantedPowerSelection);
    }

    /// <summary>
    /// <b>A Power the book buys by naming an option</b>, read off its own entry rather than listed:
    /// a printed name carrying "(X)", or a per-unit price, which is what "each is named and paid for
    /// separately" costs out as.
    /// </summary>
    private bool PowerIsBoughtByNamingAnOption(string powerId)
    {
        var power = _f.Rules.GetPower(powerId);

        Assert.True(power is not null, $"'{powerId}' is not a Power in powers.json.");

        return power!.Name.Contains("(X)", StringComparison.Ordinal)
               || string.Equals(power.CostType, "per_unit", StringComparison.Ordinal);
    }

    /// <summary>
    /// p.91's list as name to printed sentence, cut at each name the same regex finds. An item's
    /// text runs from its own colon to the next item's name, which is what makes a per-item
    /// question answerable against the page at all.
    /// </summary>
    private static Dictionary<string, string> PrintedItemSentences()
    {
        var block = ChapterSixSections().Single(s => s.Page == 91 && s.Heading == "EQUIPMENT");
        var names = Regex.Matches(block.Text, @"(?<name>[A-Z][A-Za-z]*(?:[ ,/]+[A-Za-z]+)*): ");
        var sentences = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < names.Count; i++)
        {
            var start = names[i].Index + names[i].Length;
            var end = i + 1 < names.Count ? names[i + 1].Index : block.Text.Length;

            sentences[names[i].Groups["name"].Value] = block.Text[start..end].Trim();
        }

        return sentences;
    }

    // ── Custom Gear and its Pros and Cons, pp.92-93 ──────────────────────────

    /// <summary>
    /// <b>The twelve custom features are p.93's, and this file does not repeat them.</b>
    ///
    /// <para>They were extracted long ago into <c>gear_features.json</c>, which the engine loads and
    /// <c>CostCalculator.GearCost</c> prices. What was never checked is that the twelve are *all* of
    /// them — so this reads p.93's own headings out of the corpus and requires each to resolve to a
    /// feature, rather than re-extracting a page that is already done.</para>
    /// </summary>
    [Fact]
    public void TheTwelveCustomFeaturesAreEveryOnePageNinetyThreePrints()
    {
        var headings = ChapterSixSections()
            .Where(s => s.Page == 93)
            .Select(s => s.Heading)
            .ToList();

        // The page's own furniture, which is not a feature: the section that introduces them and
        // the Pros and Cons section that follows.
        var featureHeadings = headings
            .Where(h => h is not ("CUSTOM FEATURES" or "PROS AND CONS"))
            .ToList();

        Assert.Equal(CanonicalEquipmentCatalogue.CustomGear.CustomFeatureCount, featureHeadings.Count);
        Assert.Equal(CanonicalEquipmentCatalogue.CustomGear.CustomFeatureCount, _f.Rules.GearFeatures.Count);

        // Every printed heading resolves to a shipped feature, by the name it carries.
        foreach (var heading in featureHeadings)
        {
            var wanted = heading.Replace("/", " / ", StringComparison.Ordinal);

            Assert.True(
                _f.Rules.GearFeatures.Any(
                    f => string.Equals(f.Name, wanted, StringComparison.OrdinalIgnoreCase)),
                $"p.93 prints a custom feature '{heading}' that gear_features.json has no entry for.");
        }

        // And this file points at that one rather than carrying a second copy of it.
        Assert.Equal(
            CanonicalEquipmentCatalogue.CustomGear.CustomFeatureCount,
            Equipment().CustomGear.Customization!.CustomFeatureCount);

        Assert.DoesNotContain("\"cost_range\"", Json(), StringComparison.Ordinal);
    }

    /// <summary>
    /// p.92's Two-Fisted rule, checked against the Power it names and against the engine that
    /// already implements it. A pair is one <c>SelectedGear</c> with
    /// <c>PairedUnderTwoFisted</c> set, so it is charged once by construction.
    /// </summary>
    [Fact]
    public void ThePairedWeaponRuleNamesAPowerThatExists()
    {
        var customization = Equipment().CustomGear.Customization!;

        Assert.True(customization.PairedWeaponsCostOnce);
        Assert.Equal(
            CanonicalEquipmentCatalogue.CustomGear.PairedWeaponsRequirePowerId,
            customization.PairedWeaponsRequirePowerId);

        Assert.NotNull(_f.Rules.GetPower(customization.PairedWeaponsRequirePowerId));

        // The engine charges the pair once, which is what the rule says.
        var pair = new SelectedGear("Jo Sticks")
        {
            Features = [new("upgraded")],
            PairedUnderTwoFisted = true
        };

        var single = new SelectedGear("Jo Stick") { Features = [new("upgraded")] };

        Assert.Equal(_f.Costs.GearCost(single), _f.Costs.GearCost(pair));
    }

    /// <summary>
    /// <b>p.93's twenty-four named options all resolve, and the one every item already has is not
    /// among them.</b>
    ///
    /// <para>That absence is the argument for the engine's behaviour rather than a curiosity:
    /// <c>CostCalculator.GearCost</c> neither charges nor credits the Item Con, on the grounds that
    /// "every piece of gear has the Item Con" states what gear is. If Item were on this list the
    /// grounds would be gone.</para>
    ///
    /// <para><b>One printed name needs a mapping and it lives here, not in the data</b>: the page
    /// writes "Area of Effect" where <c>pros.json</c> carries <c>area_burst</c>, whose printed name
    /// is "Area / Burst (Area of Effect)". The data transcribes the printed names and nothing
    /// else.</para>
    /// </summary>
    [Fact]
    public void EveryProAndConPageNinetyThreeNamesForGearResolvesAndItemIsNotOneOfThem()
    {
        var block = Equipment().GearProsAndCons.GearProsAndCons!;

        Assert.Equal(CanonicalEquipmentCatalogue.GearProsAndCons.CommonlyApplied, block.CommonlyApplied);
        Assert.Equal(block.CommonlyApplied.Count, block.CommonlyAppliedCount);

        foreach (var printed in block.CommonlyApplied)
        {
            var id = printed.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

            var resolved = _f.Rules.GetPro(id) is not null || _f.Rules.GetCon(id) is not null
                           || _f.Rules.Pros.Any(p => p.Name.Contains(printed, StringComparison.OrdinalIgnoreCase))
                           || _f.Rules.Cons.Any(c => c.Name.Contains(printed, StringComparison.OrdinalIgnoreCase));

            Assert.True(resolved,
                $"p.93 names '{printed}' as commonly applied to gear, and neither pros.json nor "
                + "cons.json has an entry for it.");
        }

        // The Item Con exists, and is deliberately not on the page's list.
        Assert.NotNull(_f.Rules.GetCon(CanonicalEquipmentCatalogue.GearProsAndCons.ItemConId));
        Assert.DoesNotContain("Item", block.CommonlyApplied);
        Assert.True(block.ItemIsAbsentFromTheCommonList);
    }

    /// <summary>
    /// <b>The two floors p.93 states, measured through the engine rather than read off the data.</b>
    /// Gear floors at 0 where an unranked Power floors at 1, and the Item Con is not credited — so
    /// an item discounted below nothing is free and stops, and an item carrying Item pays the same
    /// as one without it.
    /// </summary>
    [Fact]
    public void TheEngineChargesTheTwoFloorsThisPageStates()
    {
        var block = Equipment().GearProsAndCons.GearProsAndCons!;

        Assert.Equal(CanonicalEquipmentCatalogue.GearProsAndCons.MinimumCostHeroPoints, block.MinimumCostHeroPoints);

        // A 1 HP feature with a −2 Con on it: the arithmetic is −1 and the floor is the file's own.
        var discounted = new SelectedGear("Cheap knife")
        {
            Features = [new("bonded")],
            Cons = [new("build_up")]
        };

        Assert.Equal(block.MinimumCostHeroPoints, _f.Costs.GearCost(discounted));

        // Positive control: the same item without the Con costs something, so the floor above is a
        // floor rather than an item that was free anyway.
        Assert.Equal(1, _f.Costs.GearCost(new SelectedGear("Cheap knife") { Features = [new("bonded")] }));

        // A control on the floor: an item with the feature and no Con costs more than nothing, so
        // the two assertions above are about a floor rather than about an item that was free anyway.
        Assert.True(_f.Costs.GearCost(new SelectedGear("Sword") { Features = [new("upgraded")] }) > 0);
    }

    /// <summary>
    /// <b>A second divergence between the page and the engine, recorded rather than repaired — and
    /// this one contradicts a doc comment.</b>
    ///
    /// <para>p.93: "As physical objects, every piece of gear has the Item Con."
    /// <c>CostCalculator.GearCost</c>'s own comment says "The Item Con is not charged or credited
    /// here", and that is true only of what the engine <em>adds</em>: nothing puts Item on an item
    /// automatically. A host that records the Con the book says every item carries gets it credited
    /// like any other, at the −1 <c>cons.json</c> prices it — so a 1 HP feature comes out free,
    /// which is precisely the outcome the comment gives as the reason not to credit it.</para>
    ///
    /// <para>Nothing here changes that. This is a data slice, the engine's behaviour is what ships,
    /// and the honest thing is a test that says what it actually does so the next slice decides on
    /// purpose. Two readings are open and the page settles neither: Item is a description of gear
    /// and should be uncreditable, or it is a Con like any other and the floor at 0 is what stops it
    /// paying out.</para>
    /// </summary>
    [Fact]
    public void TheItemConIsCreditedWhenAHostRecordsItEvenThoughGearAlwaysHasIt()
    {
        var block = Equipment().GearProsAndCons.GearProsAndCons!;

        Assert.True(block.EveryPieceOfGearHasTheItemCon);
        Assert.True(block.ItemConIsAStatementNotADiscount);

        var without = new SelectedGear("Sword") { Features = [new("upgraded")] };
        var withItem = new SelectedGear("Sword")
        {
            Features = [new("upgraded")],
            Cons = [new(CanonicalEquipmentCatalogue.GearProsAndCons.ItemConId)]
        };

        // The engine's answer today, asserted so a change to it is a change to this test.
        Assert.Equal(2, _f.Costs.GearCost(without));
        Assert.Equal(1, _f.Costs.GearCost(withItem));

        // And a 1 HP feature really does come out free, which is the case the doc comment names.
        var cheap = new SelectedGear("Knife")
        {
            Features = [new("bonded")],
            Cons = [new(CanonicalEquipmentCatalogue.GearProsAndCons.ItemConId)]
        };

        Assert.Equal(0, _f.Costs.GearCost(cheap));
    }

    /// <summary>
    /// <b>A divergence between the page and the engine, recorded rather than repaired.</b>
    ///
    /// <para>p.93 names Overkill and Weak among the Cons commonly applied to gear. Both are defined
    /// (Ch.2) as a change to a Power's cost <em>per rank</em> — and a piece of gear has no rank, so
    /// <c>CostCalculator.ResolveConCost</c> returns 0 for either of them and the Con the player
    /// wrote on the item is worth nothing. The page does not say what it should be worth.</para>
    ///
    /// <para>This asserts the current behaviour on purpose, so that a slice which decides what those
    /// two mean on gear has to come here and change it, rather than discovering the question by
    /// accident. The entry's <c>ambiguity</c> carries the same finding for a reader of the
    /// data.</para>
    /// </summary>
    [Fact]
    public void OverkillAndWeakAreNamedForGearAndTheEngineChargesNothingForEither()
    {
        var block = Equipment().GearProsAndCons.GearProsAndCons!;

        Assert.Contains("Overkill", block.CommonlyApplied);
        Assert.Contains("Weak", block.CommonlyApplied);

        var plain = new SelectedGear("Axe") { Features = [new("upgraded")] };
        var overkill = new SelectedGear("Axe") { Features = [new("upgraded")], Cons = [new("overkill")] };
        var weak = new SelectedGear("Axe") { Features = [new("upgraded")], Cons = [new("weak")] };

        Assert.Equal(_f.Costs.GearCost(plain), _f.Costs.GearCost(overkill));
        Assert.Equal(_f.Costs.GearCost(plain), _f.Costs.GearCost(weak));

        // A control on the comparison: a Con that IS flat does move the price, so the two above are
        // worth nothing rather than every Con being worth nothing.
        var burnout = new SelectedGear("Axe") { Features = [new("upgraded")], Cons = [new("burnout")] };

        Assert.True(_f.Costs.GearCost(burnout) < _f.Costs.GearCost(plain));

        // And the data says so where a reader will find it.
        Assert.Contains("Overkill", Equipment().GearProsAndCons.Ambiguity ?? "", StringComparison.Ordinal);
    }

    // ── The envelope every entry carries ─────────────────────────────────────

    /// <summary>
    /// Every entry cites a page in Chapter 6 and names a heading printed on that page — the same
    /// narrowing the play store makes, and with the same known limit: it cannot tell two headings on
    /// one page apart.
    /// </summary>
    [Fact]
    public void EveryEntryNamesAHeadingPrintedOnThePageItCites()
    {
        var headings = ChapterSixSections()
            .Select(s => (s.Page, s.Heading))
            .ToHashSet();

        // Positive control, and a negative one on a heading that really exists on another page.
        Assert.True(headings.Count >= 100, $"Only {headings.Count} headings were read out of Chapter 6.");
        Assert.Contains((88, "SHIELDS"), headings);
        Assert.DoesNotContain((91, "SHIELDS"), headings);

        var faults = new List<string>();

        foreach (var (id, printedUnder, sourceRef) in Envelopes())
        {
            var page = int.Parse(
                Regex.Match(sourceRef, @"\bp\.(\d+)\b").Groups[1].Value, CultureInfo.InvariantCulture);

            Assert.InRange(page, ChapterFirstPage, ChapterLastPage);

            if (!headings.Contains((page, printedUnder)))
                faults.Add($"{id}: printed_under '{printedUnder}' is not a heading on p.{page}");
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <c>verified_fields</c> is the same per-field discipline <c>powers.json</c> and the play store
    /// use, and for the same reason: a single boolean drifts, and twenty-seven Power entries once
    /// sat unflagged with wrong costs. It must be non-empty, drawn from the header's own closed
    /// list, and include <c>description</c> — the one field that is written rather than transcribed,
    /// and therefore the one most easily left unchecked.
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresVerifiedFieldsFromTheClosedList()
    {
        var closed = Equipment().Header.VerifiedFieldsClosedList.ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(closed);

        var seen = 0;

        foreach (var (id, fields) in VerifiedFields())
        {
            seen++;

            Assert.True(fields.Count > 0, $"{id} declares no verified fields");
            Assert.True(fields.Contains("description"), $"{id} does not declare description verified");

            foreach (var field in fields)
                Assert.True(closed.Contains(field), $"{id} declares '{field}', which is not on the closed list");
        }

        // Positive control: the walk found the entries at all.
        Assert.True(seen >= 70, $"Only {seen} entries declared verified fields.");
    }

    /// <summary>
    /// <b>Descriptions here are ours; the book's words are the corpus's.</b> The realistic failure
    /// is a description built around a lifted clause rather than one pasted whole, so this fails on
    /// any run of ten consecutive words shared with Chapter 6, after reducing both sides to letters
    /// and digits so re-punctuating a lifted clause does not dodge it.
    ///
    /// <para>It carries a positive control, because a normaliser that quietly produced an empty
    /// haystack would pass every assertion while checking nothing.</para>
    /// </summary>
    [Fact]
    public void NoDescriptionRepeatsARunOfTheBooksOwnWords()
    {
        var chapter = Normalise(string.Join(" ", ChapterSixSections().Select(s => s.Text)));

        // The control: a phrase taken out of the corpus must be found in it.
        Assert.Contains(Normalise("every piece of gear has the Item Con"), chapter, StringComparison.Ordinal);

        var faults = new List<string>();

        foreach (var (id, description) in Descriptions())
        {
            var words = Normalise(description).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i + 10 <= words.Length; i++)
            {
                var run = string.Join(' ', words[i..(i + 10)]);

                if (chapter.Contains(run, StringComparison.Ordinal))
                {
                    faults.Add($"{id} repeats ten of the book's own words: \"{run}\"");
                    break;
                }
            }
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    private static string Normalise(string text) =>
        string.Join(' ',
            Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9 ]+", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // ── The coverage walk ────────────────────────────────────────────────────

    /// <summary>
    /// The envelope, which every entry carries and no comparer needs: identity, prose, citations
    /// and the two labelled fields for a reading and for the book's silence.
    /// </summary>
    private static readonly HashSet<string> EnvelopeFields =
        new(StringComparer.Ordinal)
        {
            "id", "name", "description", "verified_fields", "source_ref", "corroborated_by",
            "ambiguity", "printed_under", "copied_from"
        };

    /// <summary>
    /// <b>Prose is identified by name, not by a list of exemptions</b> — a list of "this one is only
    /// descriptive" is how twenty fact fields came to be unchecked in the play store.
    /// <c>what_this_is</c> labels a block as this project's own words and a <c>*_note</c> suffix
    /// marks an aside; everything else is a fact field and has to be compared to the book.
    /// </summary>
    private static bool IsProse(string leafName) =>
        leafName is "what_this_is" or "note" || leafName.EndsWith("_note", StringComparison.Ordinal);

    /// <summary>
    /// Values that are derived from the corpus or from the play store rather than transcribed into
    /// a canonical file, each proved by a named test above. Sixty-three weapons rows, nine armour
    /// rows and thirty-six items typed out a second time would be three more things to disagree with
    /// the first.
    /// </summary>
    private static readonly HashSet<string> DerivedPaths =
        new(StringComparer.Ordinal)
        {
            // TheArmorTableIsDerivedFromTheCorpusColumns
            "armor_table.rows",
            "armor_table.interpretation.row_alignment",
            // TheWeaponTablesAreACopyOfThePlayStoresAndAreHeldEqualToIt, and the play store's own
            // TheThreeWeaponsTablesArePairedOutOfTheCorpusColumns behind it.
            "ancient_weapons.weapons",
            "modern_weapons.weapons",
            "advanced_weapons.weapons",
            "ancient_weapons.interpretation.row_alignment",
            "modern_weapons.interpretation.row_alignment",
            "advanced_weapons.interpretation.row_alignment",
            // TheEquipmentListIsDerivedFromTheCorpusAndPricesNothing,
            // TheItemsThatCarryAMechanicalEffectCarryTheFiguresThePagePrints,
            // EveryThresholdOnAnItemIsTheKindOfRollThePagePrints and
            // EveryGrantedPowerBoughtByNamingAnOptionRecordsTheOptionThePagePrints
            "equipment_catalogue.items"
        };

    private static Func<object?, string?> Is(object? expected) => actual =>
        ValuesEqual(expected, actual) ? null : $"is {Show(actual)}; the rulebook says {Show(expected)}";

    private static Func<object?, string?> Covers(string featureId) =>
        Is(CanonicalEquipmentCatalogue.FeatureById(featureId).Covers);

    private static Func<object?, string?> DefersToKind(string featureId) =>
        Is(CanonicalEquipmentCatalogue.FeatureById(featureId).Kind);

    private static Func<object?, string?> DefersToId(string featureId) =>
        Is(CanonicalEquipmentCatalogue.FeatureById(featureId).Reference);

    /// <summary>
    /// <b>Every fact field of every entry, and the rulebook value it must equal.</b> A path is
    /// <c>&lt;entry id&gt;.&lt;field&gt;</c>, descending into nested objects. Registering a path
    /// also stops the walk descending into it, so a whole table is one entry here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<object?, string?>> CanonicalChecks =
        BuildChecks();

    private static Dictionary<string, Func<object?, string?>> BuildChecks()
    {
        var checks = new Dictionary<string, Func<object?, string?>>(StringComparer.Ordinal)
        {
            // ── Armor, p.88 ─────────────────────────────────────────────────
            ["armor_rank.armor.grants_power_id"] = Is(CanonicalArmourRules.Armor.GrantsPowerId),
            ["armor_rank.armor.rank_is"] = Is(CanonicalArmourRules.Armor.RankIs),
            ["armor_rank.armor.rank_base_trait"] = Is(CanonicalArmourRules.Armor.RankBaseTrait),
            ["armor_rank.armor.rank_base_may_be_the_armor_power_instead"] =
                Is(CanonicalArmourRules.Armor.RankBaseMayBeTheArmorPowerInstead),
            ["armor_rank.armor.rank_base_substitution_applies_while"] =
                Is(CanonicalArmourRules.Armor.RankBaseSubstitutionAppliesWhile),
            ["armor_rank.armor.built_in_extras_are_powers"] =
                Is(CanonicalArmourRules.Armor.BuiltInExtrasArePowers),
            ["armor_rank.armor.built_in_extras_examples"] =
                Is(CanonicalArmourRules.Armor.BuiltInExtrasExamples),

            ["armor_table.row_count"] = Is(CanonicalArmourRules.ArmorTable.RowCount),

            ["bulky.penalty_dice"] = Is(CanonicalArmourRules.Bulky.PenaltyDice),
            ["bulky.penalised_rolls"] = Is(CanonicalArmourRules.Bulky.PenalisedRolls),
            ["bulky.exempt_if_might_at_least"] = Is(CanonicalArmourRules.Bulky.ExemptIfMightAtLeast),
            ["bulky.removable_by_gear_feature_id"] = Is(CanonicalArmourRules.Bulky.RemovableByGearFeatureId),

            ["rigid.penalty_dice"] = Is(CanonicalArmourRules.Rigid.PenaltyDice),
            ["rigid.penalised_rolls"] = Is(CanonicalArmourRules.Rigid.PenalisedRolls),
            ["rigid.removable_by_gear_feature_id"] = Is(CanonicalArmourRules.Rigid.RemovableByGearFeatureId),

            // ── Shields, p.88 ───────────────────────────────────────────────
            ["shields.shield.bonus_dice"] = Is(CanonicalArmourRules.Shields.BonusDice),
            ["shields.shield.bonus_name"] = Is(CanonicalArmourRules.Shields.BonusName),
            ["shields.shield.carried_in"] = Is(CanonicalArmourRules.Shields.CarriedIn),
            ["shields.shield.applies_to_defenses"] = Is(CanonicalArmourRules.Shields.AppliesToDefenses),
            ["shields.shield.applies_against_attack_kinds"] =
                Is(CanonicalArmourRules.Shields.AppliesAgainstAttackKinds),
            ["shields.shield.may_be_used_as_an_off_hand_weapon"] =
                Is(CanonicalArmourRules.Shields.MayBeUsedAsAnOffHandWeapon),
            ["shields.shield.striking_with_it_costs"] = Is(CanonicalArmourRules.Shields.StrikingWithItCosts),
            ["shields.shield.no_benefit_when"] = Is(CanonicalArmourRules.Shields.NoBenefitWhen),
            ["shields.shield.also_a_weapon_row"] = Is(CanonicalArmourRules.Shields.AlsoAWeaponRow),
            ["shields.shield.weapon_rows"] = Is(CanonicalArmourRules.Shields.WeaponRows),
            ["shields.shield.weapon_feature_id"] = Is(CanonicalArmourRules.Shields.WeaponFeatureId),

            // ── The glossary as a block, pp.88 and 90 ───────────────────────
            ["weapon_features_glossary.glossary.describes"] = Is(CanonicalEquipmentCatalogue.Glossary.Describes),
            ["weapon_features_glossary.glossary.further_features_come_from_customizing"] =
                Is(CanonicalEquipmentCatalogue.Glossary.FurtherFeaturesComeFromCustomizing),
            ["weapon_features_glossary.glossary.customization_is_on_page"] =
                Is(CanonicalEquipmentCatalogue.Glossary.CustomizationIsOnPage),
            ["weapon_features_glossary.glossary.entry_count"] = Is(CanonicalEquipmentCatalogue.Glossary.EntryCount),
            ["weapon_features_glossary.glossary.name_count"] = Is(CanonicalEquipmentCatalogue.Glossary.NameCount),
            ["weapon_features_glossary.glossary.printed_across_pages"] =
                Is(CanonicalEquipmentCatalogue.Glossary.PrintedAcrossPages),

            // ── The glossary's own entries ──────────────────────────────────
            ["area_burst.burst_diameter_feet"] = Is(CanonicalEquipmentCatalogue.Burst.DiameterFeet),
            ["area_burst.burst_diameter_exceptions"] = BurstExceptionsAre(),

            ["binding.inflicts_damage"] = Is(false),
            ["binding.may_still_be_used_for"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.GrabsHoldsAndStunts),

            ["braced.requires"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.BracedRequires),
            ["braced.two_handed_instead_if_might_at_least"] =
                Is(CanonicalEquipmentCatalogue.FeatureFigures.BracedTwoHandedMight),

            ["dazzle.power_rank_is"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.PowerRankIsTheAttackRank),
            ["ensnare.power_rank_is"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.PowerRankIsTheAttackRank),

            ["flexible.may_also_be_used_for"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.GrabsHoldsAndStunts),

            ["irritant.power_rank_base_dice"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.IrritantBaseDice),
            ["irritant.power_rank_adds"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.IrritantRankAdds),
            ["irritant.attack_threshold"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.IrritantAttackThreshold),
            ["irritant.attack_threshold_label"] =
                Is(CanonicalEquipmentCatalogue.FeatureFigures.IrritantAttackThresholdLabel),
            ["irritant.attack_trait"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.IrritantAttackTrait),

            ["launcher.grenades_selected_separately"] = Is(true),
            ["launcher.identical_statistics_to_thrown_grenades"] = Is(true),
            ["launcher.but_not_the_same_items"] = Is(true),

            ["shield.shield_bonus_dice"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.ShieldBonusDice),

            ["shock.inflicts_damage"] = Is(true),
            ["shock.carrier_attack"] = Is(true),
            ["shock.power_rank_is"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.PowerRankIsTheAttackRank),

            ["stun.inflicts_damage"] = Is(false),
            ["stun.power_rank_is"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.PowerRankIsTheAttackRank),

            ["thrown.base_range"] = Is(CanonicalEquipmentCatalogue.FeatureFigures.ThrownBaseRange),
            ["thrown.farther_with_strength"] = Is(true),
            ["thrown.range_detail_chapter"] =
                Is(CanonicalEquipmentCatalogue.FeatureFigures.ThrownRangeDetailChapter),
            ["thrown.ammunition_is_not_tracked"] = Is(true),

            ["versatile.two_handed_bonus_dice"] =
                Is(CanonicalEquipmentCatalogue.FeatureFigures.VersatileTwoHandedBonusDice),

            // ── The copied tables' own facts ────────────────────────────────
            ["ancient_weapons.row_count"] = Is(CanonicalEquipmentRules.WeaponTableSizes.Ancient),
            ["modern_weapons.row_count"] = Is(CanonicalEquipmentRules.WeaponTableSizes.Modern),
            ["advanced_weapons.row_count"] = Is(CanonicalEquipmentRules.WeaponTableSizes.Advanced),

            // ── Equipment, p.91 ─────────────────────────────────────────────
            ["equipment_catalogue.mundane_gear.is_free"] = Is(CanonicalEquipmentCatalogue.MundaneGear.IsFree),
            ["equipment_catalogue.mundane_gear.is_tracked"] = Is(CanonicalEquipmentCatalogue.MundaneGear.IsTracked),
            ["equipment_catalogue.mundane_gear.costs_hero_points"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.CostsHeroPoints),
            ["equipment_catalogue.mundane_gear.assumed_carried_for_talents_at_rank"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.AssumedCarriedForTalentsAtRank),
            ["equipment_catalogue.mundane_gear.assumed_carried_examples"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.AssumedCarriedExamples),
            ["equipment_catalogue.mundane_gear.perks_the_gm_weighs"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.PerksTheGmWeighs),
            ["equipment_catalogue.mundane_gear.even_a_broke_hero_has_what_they_need"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.EvenABrokeHeroHasWhatTheyNeed),
            ["equipment_catalogue.mundane_gear.ordinary_possessions_are_not_tracked_either"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.OrdinaryPossessionsAreNotTrackedEither),
            ["equipment_catalogue.mundane_gear.gm_uses_common_sense"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.GmUsesCommonSense),
            ["equipment_catalogue.mundane_gear.npcs_have_whatever_the_gm_wants"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.NpcsHaveWhateverTheGmWants),
            ["equipment_catalogue.mundane_gear.minions_do_not_use_gear"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.MinionsDoNotUseGear),
            ["equipment_catalogue.mundane_gear.minion_gear_is_detail_not_mechanics"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.MinionGearIsDetailNotMechanics),
            ["equipment_catalogue.mundane_gear.list_is_examples_not_a_catalogue_of_prices"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.ListIsExamplesNotACatalogueOfPrices),
            ["equipment_catalogue.mundane_gear.item_count"] = Is(CanonicalEquipmentCatalogue.MundaneGear.ItemCount),
            ["equipment_catalogue.mundane_gear.printed_price_count"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.PrintedPriceCount),
            ["equipment_catalogue.mundane_gear.printed_availability_count"] =
                Is(CanonicalEquipmentCatalogue.MundaneGear.PrintedAvailabilityCount),

            // ── Custom Gear, p.92 ───────────────────────────────────────────
            ["custom_gear.customization.sits_between"] = Is(CanonicalEquipmentCatalogue.CustomGear.SitsBetween),
            ["custom_gear.customization.costs_hero_points"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.CostsHeroPoints),
            ["custom_gear.customization.what_may_be_added"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.WhatMayBeAdded),
            ["custom_gear.customization.custom_features_are_on_page"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.CustomFeaturesAreOnPage),
            ["custom_gear.customization.custom_feature_count"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.CustomFeatureCount),
            ["custom_gear.customization.paired_weapons_cost_once"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.PairedWeaponsCostOnce),
            ["custom_gear.customization.paired_weapons_require_power_id"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.PairedWeaponsRequirePowerId),
            ["custom_gear.customization.paired_weapons_must_be_identical"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.PairedWeaponsMustBeIdentical),
            ["custom_gear.customization.subject_to_gm_approval"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.SubjectToGmApproval),
            ["custom_gear.customization.players_may_invent_features"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.PlayersMayInventFeatures),
            ["custom_gear.customization.gm_may_gate_features_behind_perks"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.GmMayGateFeaturesBehindPerks),
            ["custom_gear.customization.gm_may_prohibit_features_entirely"] =
                Is(CanonicalEquipmentCatalogue.CustomGear.GmMayProhibitFeaturesEntirely),

            // ── Pros and Cons, p.93 ─────────────────────────────────────────
            ["gear_pros_and_cons.gear_pros_and_cons.every_piece_of_gear_has_the_item_con"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.EveryPieceOfGearHasTheItemCon),
            ["gear_pros_and_cons.gear_pros_and_cons.item_con_id"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.ItemConId),
            ["gear_pros_and_cons.gear_pros_and_cons.item_con_is_a_statement_not_a_discount"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.ItemConIsAStatementNotADiscount),
            ["gear_pros_and_cons.gear_pros_and_cons.cost_is_the_same_as_on_a_power"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.CostIsTheSameAsOnAPower),
            ["gear_pros_and_cons.gear_pros_and_cons.minimum_cost_hero_points"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.MinimumCostHeroPoints),
            ["gear_pros_and_cons.gear_pros_and_cons.gear_never_pays_out"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.GearNeverPaysOut),
            ["gear_pros_and_cons.gear_pros_and_cons.commonly_applied"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.CommonlyApplied),
            ["gear_pros_and_cons.gear_pros_and_cons.commonly_applied_count"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.CommonlyAppliedCount),
            ["gear_pros_and_cons.gear_pros_and_cons.list_is_not_exhaustive"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.ListIsNotExhaustive),
            ["gear_pros_and_cons.gear_pros_and_cons.item_is_absent_from_the_common_list"] =
                Is(CanonicalEquipmentCatalogue.GearProsAndCons.ItemIsAbsentFromTheCommonList)
        };

        // Every glossary entry's covers_names and its deferral, registered from the canonical list
        // rather than typed out eighteen times — so an entry added to the book and to the data has
        // to be added there, where the printed sentence is, before this walk will pass.
        foreach (var feature in CanonicalEquipmentCatalogue.WeaponFeatures)
        {
            checks[$"{feature.Id}.covers_names"] = Covers(feature.Id);

            if (feature.Kind is null) continue;

            checks[$"{feature.Id}.works_like.kind"] = DefersToKind(feature.Id);
            checks[$"{feature.Id}.works_like.id"] = DefersToId(feature.Id);
        }

        return checks;
    }

    private static Func<object?, string?> BurstExceptionsAre() => actual =>
    {
        if (actual is not IReadOnlyList<BurstDiameterExceptionModel> rows)
            return $"is {Show(actual)}, not a list of burst exceptions";

        var expected = CanonicalEquipmentCatalogue.Burst.SmallDiameterWeapons;

        if (rows.Count != expected.Length) return $"has {rows.Count} rows, not {expected.Length}";

        return rows.All(r => expected.Contains(r.Weapon, StringComparer.Ordinal)
                             && r.DiameterFeet == CanonicalEquipmentCatalogue.Burst.SmallDiameterFeet)
            ? null
            : $"is {Show(rows.Select(r => $"{r.Weapon} {r.DiameterFeet}ft"))}";
    };

    private static HashSet<string> RegisteredPaths =>
        [.. CanonicalChecks.Keys.Concat(DerivedPaths)];

    /// <summary>
    /// <b>Every leaf below an entry's envelope is prose, a labelled derivation, or a value compared
    /// against a canonical file.</b> A reflection walk rather than a list, so a field added to the
    /// data cannot quietly go unchecked — which is the failure the play store found around twenty
    /// times over, in fields that deserialized perfectly and were compared to nothing.
    ///
    /// <para><b>What it cannot see is a field whose value is null</b>, since a null leaf and an
    /// optional shape an entry does not use are the same thing to reflection. Rigid's missing Might
    /// escape is the one that matters, and
    /// <see cref="TheTwoArmorFeaturesDifferOnlyInWhetherStrengthExcusesTheWearer"/> asserts it by
    /// name.</para>
    /// </summary>
    [Fact]
    public void EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook()
    {
        var faults = new List<string>();
        var leaves = 0;

        foreach (var (id, entry) in AllEntryObjects())
        {
            leaves += EntryLeaves(id, entry, RegisteredPaths).Count();
            faults.AddRange(CoverageFaults(id, entry));
        }

        // Positive control on the walk itself: a walk that stopped finding properties would report
        // no faults and prove nothing, which is the shape of four historical guard failures here.
        Assert.True(leaves >= 145,
            $"The walk found only {leaves} fact fields in {FileName}, which is fewer than the "
            + "entries carry — there are 161 today. It has stopped reading the models; fix the "
            + "walk, not this number.");

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>A record with one unregistered fact field, one prose field and one aside.</summary>
    private sealed record CoverageProbe(int MadeUpNumber, string WhatThisIs, string ExtraNote);

    /// <summary>
    /// <b>The negative control for the walk above, and it is the whole instrument.</b> A classifier
    /// that faulted nothing would pass
    /// <see cref="EveryFactFieldOfEveryEntryIsComparedAgainstTheRulebook"/> perfectly while checking
    /// nothing at all.
    /// </summary>
    [Fact]
    public void TheCoverageWalkReportsAFieldNothingComparesToTheRulebook()
    {
        var faults = CoverageFaults("probe", new CoverageProbe(1, "ours", "an aside"));

        var fault = Assert.Single(faults);
        Assert.Contains("probe.made_up_number", fault, StringComparison.Ordinal);

        // And prose really is passing as prose rather than being missed by the walk, which would
        // look identical from the count above.
        var empty = CoverageFaults("probe", new CoverageProbe(1, "   ", "an aside"));
        Assert.Equal(2, empty.Count);
        Assert.Contains(empty, f => f.Contains("probe.what_this_is", StringComparison.Ordinal));
    }

    private static List<string> CoverageFaults(string entryId, object entry)
    {
        var faults = new List<string>();

        foreach (var (path, value) in EntryLeaves(entryId, entry, RegisteredPaths))
        {
            var leafName = path[(path.LastIndexOf('.') + 1)..];

            if (IsProse(leafName))
            {
                if (value is not string text || string.IsNullOrWhiteSpace(text))
                    faults.Add($"{path}: prose field is empty");

                continue;
            }

            if (DerivedPaths.Contains(path)) continue;

            if (!CanonicalChecks.TryGetValue(path, out var check))
            {
                faults.Add(
                    $"{path} is a fact field and nothing compares it to CanonicalArmourRules or "
                    + "CanonicalEquipmentCatalogue. Transcribe it there with the sentence it came "
                    + "from, register it, or move it into description/ambiguity prose — an "
                    + "unchecked fact field reads as verified data and is not");
                continue;
            }

            var fault = check(value);
            if (fault is not null) faults.Add($"{path} {fault}");
        }

        return faults;
    }

    /// <summary>
    /// Every leaf below an entry's envelope, as a dotted path. Nested models are descended into and
    /// lists of models are indexed; a list of scalars, a dictionary or a scalar is a leaf. A path in
    /// <paramref name="stopAt"/> is yielded whole rather than descended into.
    /// </summary>
    private static IEnumerable<(string Path, object Value)> EntryLeaves(
        string entryId, object entry, HashSet<string>? stopAt)
    {
        foreach (var property in entry.GetType().GetProperties())
        {
            var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
            if (EnvelopeFields.Contains(name)) continue;

            var value = property.GetValue(entry);
            if (value is null) continue;

            foreach (var leaf in Descend($"{entryId}.{name}", value, stopAt)) yield return leaf;
        }
    }

    private static IEnumerable<(string Path, object Value)> Descend(
        string path, object value, HashSet<string>? stopAt)
    {
        if (stopAt is not null && stopAt.Contains(path))
        {
            yield return (path, value);
            yield break;
        }

        if (IsDataModel(value))
        {
            foreach (var property in value.GetType().GetProperties())
            {
                var child = property.GetValue(value);
                if (child is null) continue;

                var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);

                foreach (var leaf in Descend($"{path}.{name}", child, stopAt)) yield return leaf;
            }

            yield break;
        }

        if (value is System.Collections.IEnumerable items and not string)
        {
            var rows = items.Cast<object>().ToList();

            if (rows.Count > 0 && rows.TrueForAll(IsDataModel))
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    foreach (var leaf in Descend($"{path}[{i}]", rows[i], stopAt)) yield return leaf;
                }

                yield break;
            }
        }

        yield return (path, value);
    }

    /// <summary>
    /// A model is one of the records the equipment file deserializes into, or the probe record
    /// declared in this class; anything else is a value.
    /// </summary>
    private static bool IsDataModel(object value) =>
        value.GetType().Assembly == typeof(EquipmentDataModel).Assembly
            && value.GetType().Namespace == typeof(EquipmentDataModel).Namespace
        || value.GetType().DeclaringType == typeof(EquipmentDataTests);

    /// <summary>
    /// The file's blocks, as (id, object) pairs, for the reflection walk. The glossary's eighteen
    /// entries and the two armour features are keyed by their own ids rather than by index, so a
    /// registered path names the thing it is about.
    /// </summary>
    private static IEnumerable<(string Id, object Entry)> AllEntryObjects()
    {
        var equipment = Equipment();

        yield return (equipment.ArmorRule.Id, equipment.ArmorRule);
        yield return (equipment.ArmorTable.Id, equipment.ArmorTable);
        yield return (equipment.Shields.Id, equipment.Shields);
        yield return (equipment.WeaponFeaturesRule.Id, equipment.WeaponFeaturesRule);
        yield return (equipment.EquipmentCatalogue.Id, equipment.EquipmentCatalogue);
        yield return (equipment.CustomGear.Id, equipment.CustomGear);
        yield return (equipment.GearProsAndCons.Id, equipment.GearProsAndCons);

        foreach (var feature in equipment.ArmorFeatures) yield return (feature.Id, feature);
        foreach (var feature in equipment.WeaponFeatures) yield return (feature.Id, feature);
        foreach (var table in equipment.WeaponTables) yield return (table.Id, table);
    }

    private static bool ValuesEqual(object? expected, object? actual)
    {
        if (expected is null || actual is null) return expected is null && actual is null;

        if (expected is System.Collections.IEnumerable left and not string
            && actual is System.Collections.IEnumerable right and not string)
        {
            return left.Cast<object>().SequenceEqual(right.Cast<object>());
        }

        return Equals(expected, actual);
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object>()) + "]",
        _ => value.ToString() ?? "null"
    };

    // ── Envelope walks, shared by the three envelope tests ───────────────────

    private static IEnumerable<(string Id, string PrintedUnder, string SourceRef)> Envelopes()
    {
        var e = Equipment();

        yield return (e.ArmorRule.Id, e.ArmorRule.PrintedUnder, e.ArmorRule.SourceRef);
        yield return (e.ArmorTable.Id, e.ArmorTable.PrintedUnder, e.ArmorTable.SourceRef);
        yield return (e.Shields.Id, e.Shields.PrintedUnder, e.Shields.SourceRef);
        yield return (e.WeaponFeaturesRule.Id, e.WeaponFeaturesRule.PrintedUnder, e.WeaponFeaturesRule.SourceRef);
        yield return (e.EquipmentCatalogue.Id, e.EquipmentCatalogue.PrintedUnder, e.EquipmentCatalogue.SourceRef);
        yield return (e.CustomGear.Id, e.CustomGear.PrintedUnder, e.CustomGear.SourceRef);
        yield return (e.GearProsAndCons.Id, e.GearProsAndCons.PrintedUnder, e.GearProsAndCons.SourceRef);

        foreach (var f in e.ArmorFeatures) yield return (f.Id, f.PrintedUnder, f.SourceRef);
        foreach (var f in e.WeaponFeatures) yield return (f.Id, f.PrintedUnder, f.SourceRef);
        foreach (var t in e.WeaponTables) yield return (t.Id, t.PrintedUnder, t.SourceRef);
    }

    private static IEnumerable<(string Id, IReadOnlyList<string> Fields)> VerifiedFields()
    {
        var e = Equipment();

        yield return (e.ArmorRule.Id, e.ArmorRule.VerifiedFields);
        yield return (e.ArmorTable.Id, e.ArmorTable.VerifiedFields);
        yield return (e.Shields.Id, e.Shields.VerifiedFields);
        yield return (e.WeaponFeaturesRule.Id, e.WeaponFeaturesRule.VerifiedFields);
        yield return (e.EquipmentCatalogue.Id, e.EquipmentCatalogue.VerifiedFields);
        yield return (e.CustomGear.Id, e.CustomGear.VerifiedFields);
        yield return (e.GearProsAndCons.Id, e.GearProsAndCons.VerifiedFields);

        foreach (var f in e.ArmorFeatures) yield return (f.Id, f.VerifiedFields);
        foreach (var f in e.WeaponFeatures) yield return (f.Id, f.VerifiedFields);
        foreach (var t in e.WeaponTables) yield return (t.Id, t.VerifiedFields);
        foreach (var r in e.ArmorTable.Rows) yield return (r.Id, r.VerifiedFields);
        foreach (var i in e.EquipmentCatalogue.Items) yield return (i.Id, i.VerifiedFields);
    }

    private static IEnumerable<(string Id, string Description)> Descriptions()
    {
        var e = Equipment();

        yield return (e.ArmorRule.Id, e.ArmorRule.Description);
        yield return (e.ArmorTable.Id, e.ArmorTable.Description);
        yield return (e.Shields.Id, e.Shields.Description);
        yield return (e.WeaponFeaturesRule.Id, e.WeaponFeaturesRule.Description);
        yield return (e.EquipmentCatalogue.Id, e.EquipmentCatalogue.Description);
        yield return (e.CustomGear.Id, e.CustomGear.Description);
        yield return (e.GearProsAndCons.Id, e.GearProsAndCons.Description);

        foreach (var f in e.ArmorFeatures) yield return (f.Id, f.Description);
        foreach (var f in e.WeaponFeatures) yield return (f.Id, f.Description);
        foreach (var t in e.WeaponTables) yield return (t.Id, t.Description);
        foreach (var r in e.ArmorTable.Rows) yield return (r.Id, r.Description);
        foreach (var i in e.EquipmentCatalogue.Items) yield return (i.Id, i.Description);
    }

    // ── The corpus ───────────────────────────────────────────────────────────

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
                        && page.ValueKind == JsonValueKind.Number
                            ? page.GetInt32()
                            : 0,
                    section.GetProperty("text").GetString() ?? ""))
        ];
    }

    /// <summary>
    /// One printed row of the Armor table: the era, the suit, its bonus, and the feature cell —
    /// which is an em dash where the suit carries none.
    ///
    /// <para><b>The feature vocabulary is closed on purpose</b>, the same way the weapons parser's
    /// is: a parse that accepted any word at all would tile any text and prove nothing.</para>
    /// </summary>
    private static Regex ArmorRow() => new(
        @"(?<category>Ancient|Modern|Advanced), (?<name>[A-Za-z/ ]+?) \+(?<bonus>\d+)d "
        + @"(?<feature>—|Bulky|Rigid)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The Armor table's block, as <paramref name="count"/> rows and whatever follows them.
    /// <b>It throws rather than returning a short list</b>, because a parser that stopped early
    /// would let a truncated block agree with a truncated expectation.
    /// </summary>
    private static (List<(string Category, string Name, int Bonus, string? Feature)>, string)
        ArmorRows(string text, int count)
    {
        var row = ArmorRow();
        var rows = new List<(string, string, int, string?)>();
        var at = 0;

        for (var i = 0; i < count; i++)
        {
            var match = row.Match(text, at);

            if (!match.Success || match.Index != at)
            {
                throw new InvalidOperationException(
                    $"row {i + 1} of {count} does not start at character {at} of the corpus block: "
                    + $"'{text[at..Math.Min(text.Length, at + 40)]}'");
            }

            var feature = match.Groups["feature"].Value;

            rows.Add((
                match.Groups["category"].Value,
                match.Groups["name"].Value,
                int.Parse(match.Groups["bonus"].Value, CultureInfo.InvariantCulture),
                string.Equals(feature, "—", StringComparison.Ordinal) ? null : feature));

            at = match.Index + match.Length;
            if (at < text.Length && text[at] == ' ') at++;
        }

        return (rows, text[at..]);
    }
}
