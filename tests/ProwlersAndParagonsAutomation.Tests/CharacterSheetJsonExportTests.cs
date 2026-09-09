using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Pins the shape of <see cref="CharacterSheetRenderer.RenderJson"/> — the keys, the nesting
/// and the JSON type each one holds. CLAUDE.md is explicit that this export is a report, not
/// the input shape: <c>build --from</c> reads a <see cref="CharacterSheet"/>, never this
/// document, so nothing round-trips it and nothing would notice a renamed or dropped key
/// short of somebody reading a diff by hand.
///
/// <para><b>Key sets, not a whole-document snapshot — deliberately.</b> The numbers in this
/// document are real engine answers (a total, a derived stat, a per-Power cost) and are
/// expected to move whenever the two sample characters or the priced rules change; a
/// byte-for-byte snapshot would fail on every one of those changes and train whoever hits it
/// to regenerate without reading why, which is the exact failure mode this repository already
/// warns about for the visual goldens ("Regenerate ... only ever deliberately"; "a golden
/// updated as a side effect of an unrelated change is a regression signed off by nobody").
/// What must not move without somebody noticing is the <em>shape</em>: which keys exist, how
/// they nest, and what type each one holds — a rename, a drop, or a number silently turned
/// into a string are all still caught, and a mutation below proves each. Both samples are
/// used so a key that appears only when a section is non-empty (Perks; a graded gear
/// feature) is covered by at least one of them.</para>
/// </summary>
public sealed class CharacterSheetJsonExportTests : IClassFixture<RulesFixture>
{
    private static readonly DateTime Stamp = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly RulesFixture _f;

    public CharacterSheetJsonExportTests(RulesFixture fixture) => _f = fixture;

    private JsonObject Render(CharacterSheet sheet)
    {
        var validation = _f.Validator.Validate(sheet);
        var json = CharacterSheetRenderer.RenderJson(sheet, _f.Rules, _f.Costs, _f.Derived, validation, Stamp);
        return JsonNode.Parse(json)!.AsObject();
    }

    private JsonObject Hero() => Render(SampleCharacters.Hero());
    private JsonObject Villain() => Render(SampleCharacters.Villain());

    /// <summary>
    /// Compares a JSON object's key set exactly — not "contains", which would miss an extra
    /// key, and not order-dependent, since <see cref="JsonObject"/> does not promise one.
    /// </summary>
    private static void AssertKeys(JsonObject obj, params string[] expected)
    {
        var actual = obj.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Array.Sort(expected, StringComparer.Ordinal);
        Assert.Equal<IEnumerable<string>>(expected, actual);
    }

    private static JsonObject FirstOf(JsonArray array)
    {
        Assert.NotEmpty(array);
        return array[0]!.AsObject();
    }

    // ── Positive control: the document actually has content ────────────────

    /// <summary>
    /// Every "does not contain key X" assertion below is worthless against a document that
    /// is empty or <c>{}</c> — this is the control that proves the document rendered from
    /// the sample actually carries the sample's content before anything about its shape is
    /// trusted.
    /// </summary>
    [Fact]
    public void TheExportIsNonTriviallyPopulatedBeforeAnythingElseIsAsserted()
    {
        var hero = Hero();

        Assert.True(hero.Count >= 12, $"Expected at least 12 top-level keys, found {hero.Count}.");
        Assert.True(hero["abilities"]!.AsArray().Count == 6);
        Assert.True(hero["powers"]!.AsArray().Count == 7);
        Assert.True(hero["gear"]!.AsArray().Count == 2);
        Assert.Equal("Ninth Precinct", hero["name"]!.GetValue<string>());
    }

    // ── Top level ────────────────────────────────────────────────────────────

    [Fact]
    public void TopLevelKeysAreExactlyThese()
    {
        string[] expected =
        [
            "meta", "name", "tier", "package", "hp_budget", "trait_cap",
            // The table's own rules, present and null for a character at no table — a reader
            // has to be able to tell "this table decided nothing" from "this build had not
            // heard of the field", so the keys are written either way.
            "campaign_table", "immortality_cost",
            "abilities", "talents",
            "source_groups", "powers", "perks", "flaws", "gear",

            // Chapter 6's four, written whether or not the character owns any. A reader has to be
            // able to tell "this character has no vehicle" from "this build had not heard of
            // vehicles", which is the same reason campaign_table is a key with a null in it.
            "vehicles", "headquarters", "gadgets", "campaign_assets",

            "derived", "narrative",
            "validation"
        ];

        AssertKeys(Hero(), expected);
        // The Villain is a legal character built by the same rules (Ch.9) and must export
        // the same shape — nothing about the document's keys may depend on which sample it
        // came from.
        AssertKeys(Villain(), expected);
    }

    [Fact]
    public void MetaCarriesGeneratedAndSystem()
    {
        var meta = Hero()["meta"]!.AsObject();

        AssertKeys(meta, "generated", "system");
        Assert.Equal(JsonValueKind.String, meta["generated"]!.GetValueKind());
        Assert.Equal("Prowlers & Paragons Ultimate Edition", meta["system"]!.GetValue<string>());
    }

    // ── Tier and package ─────────────────────────────────────────────────────

    [Fact]
    public void TierCarriesIdNameHeroPointsAndTraitCap()
    {
        var tier = Hero()["tier"]!.AsObject();

        AssertKeys(tier, "id", "name", "hero_points", "trait_cap");
        Assert.Equal("standard", tier["id"]!.GetValue<string>());
        Assert.Equal(_f.Rules.GetTier("standard")!.HeroPoints, tier["hero_points"]!.GetValue<int>());
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, tier["trait_cap"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>A house Trait Cap survives being written out and read back, and an absent one costs a
    /// character nothing.</b> Portability is the only reason the cap is on the character rather
    /// than only on the campaign, so that is the thing worth asserting.
    ///
    /// <para>The second half is the compatibility guarantee that let <c>StoredCharacter</c>'s
    /// version stay where it is: a character with no house cap writes <b>byte-for-byte</b> what it
    /// wrote before the field existed, so every export and fixture already on disk is unchanged
    /// and every stored character reads back as "the tier's".</para>
    /// </summary>
    [Fact]
    public void AHouseTraitCapSurvivesARoundTripAndAnAbsentOneAddsNoBytes()
    {
        var plain = CharacterSheetJson.Write(SampleCharacters.Hero());

        Assert.DoesNotContain("TraitCapRank", plain, StringComparison.Ordinal);

        var housed = SampleCharacters.Hero();
        housed.TraitCapRank = 6;

        var written = CharacterSheetJson.Write(housed);
        Assert.Contains("\"TraitCapRank\":6", written, StringComparison.Ordinal);

        // Strict, because a submitted file is read that way and a field the strict reader refuses
        // is a field a caller cannot use.
        var back = CharacterSheetJson.Read(written, strict: true);
        Assert.NotNull(back);
        Assert.Equal(6, back!.TraitCapRank);

        Assert.Null(CharacterSheetJson.Read(plain, strict: true)!.TraitCapRank);
    }

    /// <summary>
    /// <b>A catalogue row survives a round trip, and an item that names none writes what it wrote
    /// before the field existed.</b>
    ///
    /// <para>The same compatibility guarantee as the cap above and for the same reason: a stored
    /// character written before <c>CatalogueId</c> existed reads back as exactly the item it always
    /// was, so <c>StoredCharacter.CurrentVersion</c> did not have to move — and a bump would have
    /// discarded every stored character in silence.</para>
    ///
    /// <para><b>Strict, because a submitted file is read that way.</b> A field the strict reader
    /// refuses is a field a caller cannot use, and the whole point of putting the row on the sheet
    /// is that somebody can hand this program a character wearing Plate.</para>
    /// </summary>
    [Fact]
    public void ACatalogueRowSurvivesARoundTripAndAnItemWithoutOneAddsNoBytes()
    {
        var plain = RulesFixture.StandardSheet();
        plain.Gear.Add(new SelectedGear("A letter from his mother"));

        var written = CharacterSheetJson.Write(plain);
        Assert.DoesNotContain("CatalogueId", written, StringComparison.Ordinal);

        var back = CharacterSheetJson.Read(written, strict: true)!;
        Assert.Null(Assert.Single(back.Gear).CatalogueId);

        var armoured = RulesFixture.StandardSheet();
        armoured.Gear.Add(new SelectedGear("Plate")
        {
            CatalogueId = GearCatalogue.ArmorPrefix + "ancient_plate"
        });

        var withRow = CharacterSheetJson.Write(armoured);
        Assert.Contains("\"CatalogueId\":\"armor:ancient_plate\"", withRow, StringComparison.Ordinal);

        var readBack = CharacterSheetJson.Read(withRow, strict: true)!;
        Assert.Equal(GearCatalogue.ArmorPrefix + "ancient_plate", Assert.Single(readBack.Gear).CatalogueId);
    }

    /// <summary>
    /// <b>A row id that is not a row is read and then reported — never repaired.</b>
    ///
    /// <para>Both halves matter and they are the two failure modes this project has already
    /// shipped. Throwing at the reader would take a whole submitted character down over one
    /// misspelling; <em>dropping</em> the id would hand back a legal character carrying a plain
    /// item where a Battle Axe was sent, which is the misspelled-field-name failure the strict
    /// reader exists for. So the reader keeps it and <c>CharacterValidator</c> names it.</para>
    /// </summary>
    [Fact]
    public void AnUnknownRowIdIsKeptByTheReaderAndReportedByTheValidator()
    {
        var sheet = _f.LegalSheet();
        sheet.Gear.Add(new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battel_axe"
        });

        var back = CharacterSheetJson.Read(CharacterSheetJson.Write(sheet), strict: true)!;

        Assert.Equal(GearCatalogue.WeaponPrefix + "battel_axe", Assert.Single(back.Gear).CatalogueId);

        var finding = Assert.Single(
            _f.Validator.Validate(back).Issues,
            i => i.Code == "UNKNOWN_GEAR_CATALOGUE_ROW");

        Assert.Equal(ValidationSeverity.Error, finding.Severity);
        Assert.Contains("battel_axe", finding.Message, StringComparison.Ordinal);

        // The control: the correctly spelled row is not reported, so this is about the id being
        // wrong rather than about recording one at all.
        var fixedUp = _f.LegalSheet();
        fixedUp.Gear.Add(new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe"
        });

        Assert.DoesNotContain(_f.Validator.Validate(fixedUp).Issues,
            i => i.Code == "UNKNOWN_GEAR_CATALOGUE_ROW");
    }

    /// <summary>
    /// <b>A table's optional rules and its price for Immortality survive a round trip, and a
    /// character at no table still writes what it wrote before either field existed.</b>
    ///
    /// <para>Same guarantee as the cap above and for the same reason: portability is the whole
    /// point of the fields being on the character, and byte-for-byte identity for a character
    /// carrying neither is what let <c>StoredCharacter.CurrentVersion</c> stay where it is. A
    /// bump would have discarded every stored character in silence.</para>
    ///
    /// <para><b>The switches are spelled the way the rest of a character is.</b> This reader's
    /// naming policy is the property name and it does not forgive an underscore. The
    /// <em>export</em> spells the same block <c>campaign_table</c>, because that document is
    /// snake_case throughout — two files with two conventions, which is how they have always
    /// been.</para>
    /// </summary>
    [Fact]
    public void ATablesRulesSurviveARoundTripAndACharacterAtNoTableAddsNoBytes()
    {
        var plain = CharacterSheetJson.Write(SampleCharacters.Hero());

        Assert.DoesNotContain("CampaignTable", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("ImmortalityCost", plain, StringComparison.Ordinal);

        var housed = SampleCharacters.Hero();
        housed.CampaignId = "g_0000000000000000000000";
        housed.ImmortalityCost = 9;
        housed.CampaignTable = new CampaignTable
        {
            FatalDamage     = true,
            WoundPenalties  = true,
            RaisedGearLimit = true,
            GearLimitRank   = 12
        };

        var written = CharacterSheetJson.Write(housed);
        var back = CharacterSheetJson.Read(written, strict: true);

        Assert.NotNull(back);
        Assert.Equal(9, back!.ImmortalityCost);
        Assert.NotNull(back.CampaignTable);
        Assert.True(back.CampaignTable!.FatalDamage);
        Assert.True(back.CampaignTable.WoundPenalties);
        Assert.True(back.CampaignTable.RaisedGearLimit);
        Assert.Equal(12, back.CampaignTable.GearLimitRank);

        // The switches nobody turned on come back off rather than missing, which is what makes a
        // block written by an older build and one written today the same game.
        Assert.False(back.CampaignTable.TheDrop);
        Assert.False(back.CampaignTable.IsTheBook);

        // A payload from before either field existed reads back as the book, not as a refusal.
        var older = CharacterSheetJson.Read(plain, strict: true)!;
        Assert.Null(older.CampaignTable);
        Assert.Null(older.ImmortalityCost);
    }

    /// <summary>
    /// <b>The strict reader still refuses a field that is not part of a character, and the two new
    /// ones open no door.</b>
    ///
    /// <para>Its whole job on a submitted file is to say when a caller has misspelled something,
    /// because a dropped field is a cheaper, legal character nobody notices is wrong. The
    /// snake_case spellings the <em>export</em> uses are exactly what a caller who had read the
    /// wrong document would reach for, so those are the ones asserted.</para>
    /// </summary>
    [Theory]
    [InlineData("campaign_table")]
    [InlineData("immortality_cost")]
    [InlineData("CampaignTabel")]
    public void TheStrictReaderRefusesASpellingThatIsNotACharactersField(string key)
    {
        var json = $$"""{"Name":"Nobody","{{key}}":9}""";

        Assert.Throws<JsonException>(() => CharacterSheetJson.Read(json, strict: true));

        // The lenient reader — a browser restoring its own storage — keeps the character and
        // drops the field, which is the opposite answer and the right one there.
        Assert.Equal("Nobody", CharacterSheetJson.Read(json, strict: false)!.Name);
    }

    /// <summary>
    /// <b>The cap the character is built to, beside the tier's own.</b> They are the same figure
    /// until a table tightens one, and then they are not: <c>tier.trait_cap</c> stays a fact about
    /// the tier and the top-level one is what <c>derived.resolve</c> was measured from. A document
    /// carrying only the first would say 12d over a Resolve computed from 6d.
    /// </summary>
    [Fact]
    public void TheTopLevelTraitCapIsTheOneTheCharacterIsBuiltTo()
    {
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank,
            Hero()["trait_cap"]!.GetValue<int>());

        var housed = SampleCharacters.Hero();
        housed.TraitCapRank = 6;

        var rendered = Render(housed);

        Assert.Equal(6, rendered["trait_cap"]!.GetValue<int>());
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank,
            rendered["tier"]!["trait_cap"]!.GetValue<int>());
    }

    [Fact]
    public void PackageCarriesIdNameAndCost()
    {
        var pkg = Hero()["package"]!.AsObject();

        AssertKeys(pkg, "id", "name", "cost");
        Assert.Equal("hero_package", pkg["id"]!.GetValue<string>());
        Assert.Equal(JsonValueKind.Number, pkg["cost"]!.GetValueKind());
    }

    /// <summary>
    /// Neither sample leaves the tier or the package unset, so the null branches of both
    /// <c>root["tier"]</c> and <c>root["package"]</c> are otherwise unreached. A bare sheet
    /// exercises them without needing a third sample character.
    /// </summary>
    [Fact]
    public void AnUnsetTierAndPackageExportAsJsonNull()
    {
        var bare = Render(new CharacterSheet());

        // System.Text.Json.Nodes represents a present-but-JSON-null property as the key
        // existing with a null CLR reference, not as a JsonValue wrapping null — so the
        // key's presence and its null-ness are two separate assertions.
        Assert.True(bare.ContainsKey("tier"));
        Assert.True(bare.ContainsKey("package"));
        Assert.Null(bare["tier"]);
        Assert.Null(bare["package"]);
    }

    // ── HP budget ─────────────────────────────────────────────────────────────

    [Fact]
    public void HpBudgetCarriesTotalSpentAndRemainingAndTheyAgreeWithTheEngine()
    {
        var sheet  = SampleCharacters.Hero();
        var budget = _f.Rules.GetTier("standard")!.HeroPoints;
        var spent  = _f.Costs.TotalCost(sheet);

        var hp = Render(sheet)["hp_budget"]!.AsObject();

        AssertKeys(hp, "total", "spent", "remaining");
        Assert.Equal(budget, hp["total"]!.GetValue<int>());
        Assert.Equal(spent, hp["spent"]!.GetValue<int>());
        Assert.Equal(budget - spent, hp["remaining"]!.GetValue<int>());
    }

    // ── Abilities and Talents ───────────────────────────────────────────────

    [Fact]
    public void EveryAbilityCarriesIdNameRankAndBothSourceFields()
    {
        var abilities = Hero()["abilities"]!.AsArray();

        Assert.Equal(_f.Rules.Abilities.Count, abilities.Count);
        AssertKeys(FirstOf(abilities), "id", "name", "rank", "source", "effective_source");

        var might = abilities.Select(a => a!.AsObject()).Single(a => a["id"]!.GetValue<string>() == "might");
        Assert.Equal(JsonValueKind.Number, might["rank"]!.GetValueKind());
        // Recorded explicitly on the sample (Ch.6 armour), so this is the non-null half of
        // "source" — the null half is covered by any Ability the sample left untouched.
        Assert.Equal("tech", might["source"]!.GetValue<string>());
        Assert.Equal("tech", might["effective_source"]!.GetValue<string>());

        var agility = abilities.Select(a => a!.AsObject()).Single(a => a["id"]!.GetValue<string>() == "agility");
        Assert.Null(agility["source"]);
        Assert.Equal("innate", agility["effective_source"]!.GetValue<string>());
    }

    [Fact]
    public void TalentsCarryLinkedAbilityAlongsideBothSourceFields()
    {
        var talents = Villain()["talents"]!.AsArray();

        AssertKeys(FirstOf(talents), "id", "name", "rank", "linked_ability", "source", "effective_source");

        var academics = talents.Select(t => t!.AsObject()).Single(t => t["id"]!.GetValue<string>() == "academics");
        Assert.Equal("magic", academics["source"]!.GetValue<string>());
        Assert.Equal(JsonValueKind.String, academics["linked_ability"]!.GetValueKind());
    }

    // ── Source groups ─────────────────────────────────────────────────────────

    [Fact]
    public void SourceGroupsCarryHeadingSourceTraitLinesAndPowerIds()
    {
        var groups = Hero()["source_groups"]!.AsArray();

        AssertKeys(FirstOf(groups), "heading", "source", "trait_lines", "power_ids");

        var totalPowerIds = groups.Sum(g => g!.AsObject()["power_ids"]!.AsArray().Count);
        Assert.Equal(SampleCharacters.Hero().SelectedPowers.Count, totalPowerIds);
    }

    /// <summary>
    /// The Villain's <c>lightning_reflexes</c> deliberately carries no Source (see
    /// <c>SampleCharacters</c>), so it must surface under the plain "POWERS" fallback
    /// heading with a null <c>source</c> — the shape a sheet falls back to rather than
    /// dropping an unsourced Power.
    /// </summary>
    [Fact]
    public void AnUnsourcedPowerFallsUnderThePlainPowersHeading()
    {
        var groups = Villain()["source_groups"]!.AsArray().Select(g => g!.AsObject()).ToList();

        var fallback = Assert.Single(groups, g => g["heading"]!.GetValue<string>() == "POWERS");
        Assert.Null(fallback["source"]);
        Assert.Contains(fallback["power_ids"]!.AsArray(),
            id => id!.GetValue<string>() == "lightning_reflexes");
    }

    // ── Powers ───────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPowerCarriesTheFullFieldSet()
    {
        var powers = Hero()["powers"]!.AsArray();

        Assert.Equal(7, powers.Count);
        AssertKeys(FirstOf(powers),
            "id", "name", "range", "rank_type", "cost_type", "purchased_ranks", "baseline_rank",
            "baseline_trait", "effective_rank", "units", "cost_variant", "cost", "source",
            "source_heading", "rank_against_powers", "mechanics_verified", "source_ref",
            "pros", "cons");
    }

    /// <summary>
    /// A Power's cost, source heading and derived ranks are real engine answers, checked
    /// against the same calculators the renderer calls rather than a hand-copied number —
    /// so a mutation that changes the arithmetic fails here for the arithmetic reason, not
    /// because a hard-coded expectation went stale.
    /// </summary>
    [Fact]
    public void APowersFieldsAgreeWithTheEngine()
    {
        var sheet = SampleCharacters.Hero();
        var armor = sheet.SelectedPowers.Single(p => p.PowerId == "armor");
        var model = _f.Rules.GetPower("armor")!;

        var power = Hero()["powers"]!.AsArray()
            .Select(p => p!.AsObject())
            .Single(p => p["id"]!.GetValue<string>() == "armor");

        Assert.Equal(_f.Costs.PowerCost(armor), power["cost"]!.GetValue<int>());
        Assert.Equal(_f.Derived.GetBaselineRank(model, sheet, armor), power["baseline_rank"]!.GetValue<int>());
        Assert.Equal(_f.Derived.GetEffectiveRank(armor, sheet), power["effective_rank"]!.GetValue<int>());
        Assert.Equal("tech", power["source"]!.GetValue<string>());
        Assert.Equal("TECH POWERS", power["source_heading"]!.GetValue<string>());
    }

    /// <summary>
    /// A rankless Power's default rank against other Powers is not 0, even though its
    /// effective rank is — <c>invisibility</c> on the Villain is the sample built to show
    /// exactly this (CLAUDE.md, "Sources, and the default rank").
    /// </summary>
    [Fact]
    public void ARanklessPowersDefaultRankIsNotZero()
    {
        var sheet = SampleCharacters.Villain();
        var invisibility = sheet.SelectedPowers.Single(p => p.PowerId == "invisibility");

        var power = Villain()["powers"]!.AsArray()
            .Select(p => p!.AsObject())
            .Single(p => p["id"]!.GetValue<string>() == "invisibility");

        Assert.Equal(0, power["effective_rank"]!.GetValue<int>());
        Assert.Equal(_f.Derived.GetRankAgainstPowers(invisibility, sheet),
            power["rank_against_powers"]!.GetValue<int>());
        Assert.True(power["rank_against_powers"]!.GetValue<int>() > 0,
            "A rankless Power's default rank against other Powers should not be 0 — " +
            "otherwise this test cannot tell 'computed' from 'always zero'.");
    }

    [Fact]
    public void APowersProsAndConsAreObjectsCarryingIdAndVariantKey()
    {
        var sheet = SampleCharacters.Villain();
        var power = Villain()["powers"]!.AsArray()
            .Select(p => p!.AsObject())
            .Single(p => p["id"]!.GetValue<string>() == "mind_control");

        var con = FirstOf(power["cons"]!.AsArray());
        AssertKeys(con, "id", "variant_key");
        Assert.Equal("unreliable", con["id"]!.GetValue<string>());

        Assert.Empty(power["pros"]!.AsArray());
        Assert.Contains(sheet.SelectedPowers.Single(p => p.PowerId == "mind_control").Cons,
            c => c.Id == "unreliable");
    }

    // ── Perks and Flaws ──────────────────────────────────────────────────────

    [Fact]
    public void PerksCarryIdNameUnitsCostAndNarrativeDetail()
    {
        var perks = Hero()["perks"]!.AsArray();

        Assert.NotEmpty(perks);   // the Villain sample carries none — Hero is the positive case
        var contacts = perks.Select(p => p!.AsObject()).Single(p => p["id"]!.GetValue<string>() == "contacts");

        AssertKeys(contacts, "id", "name", "units", "cost", "narrative_detail");
        Assert.Equal(2, contacts["units"]!.GetValue<int>());
        Assert.Equal(JsonValueKind.String, contacts["narrative_detail"]!.GetValueKind());
    }

    [Fact]
    public void TheVillainHasNoPerksAndTheArrayIsEmptyNotMissing()
    {
        var villain = Villain();

        Assert.True(villain.ContainsKey("perks"));
        Assert.Empty(villain["perks"]!.AsArray());
    }

    [Fact]
    public void FlawsCarryIdNameFlawTypeAndNarrativeDetail()
    {
        var flaws = Hero()["flaws"]!.AsArray().Select(f => f!.AsObject()).ToList();

        AssertKeys(flaws[0], "id", "name", "flaw_type", "narrative_detail");

        var secretIdentity = flaws.Single(f => f["id"]!.GetValue<string>() == "secret_identity");
        Assert.Equal("regular", secretIdentity["flaw_type"]!.GetValue<string>());

        var enemy = Villain()["flaws"]!.AsArray()
            .Select(f => f!.AsObject())
            .Single(f => f["id"]!.GetValue<string>() == "wanted");
        Assert.Equal("plot_hook", enemy["flaw_type"]!.GetValue<string>());
    }

    // ── Gear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GearCarriesNameCostPairedFlagAndFeatures()
    {
        var gear = Hero()["gear"]!.AsArray().Select(g => g!.AsObject()).ToList();

        AssertKeys(gear[0], "name", "catalogue_id", "bonus_dice", "catalogue_features",
            "cost", "paired_under_two_fisted", "features", "pros", "cons");

        var maul = gear.Single(g => g["name"]!.GetValue<string>() == "Breaching maul");
        Assert.Equal(1, maul["cost"]!.GetValue<int>());   // "powerful" grade, 1 HP
        Assert.False(maul["paired_under_two_fisted"]!.GetValue<bool>());

        var feature = FirstOf(maul["features"]!.AsArray());
        AssertKeys(feature, "id", "name", "grade");
        Assert.Equal("powerful", feature["id"]!.GetValue<string>());
        Assert.Equal("powerful", feature["grade"]!.GetValue<string>());
    }

    /// <summary>
    /// A gear item's Pros and Cons are plain id strings — <c>["armor_piercing"]</c> — unlike
    /// a Power's, which are <c>{id, variant_key}</c> objects. That asymmetry is real in
    /// <c>CharacterSheetRenderer</c> today; a "helpfully" consistent refactor would be a
    /// silent shape change for anything already parsing gear Pros as strings.
    /// </summary>
    [Fact]
    public void GearProsAndConsAreBareIdStringsNotObjects()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Gear.Add(new SelectedGear("Rifle")
        {
            Features = [new SelectedGearFeature("upgraded")],
            Pros     = [new SelectedProCon("penetrating")]
        });

        var gear = Render(sheet)["gear"]!.AsArray().Select(g => g!.AsObject()).Single();

        var pro = Assert.Single(gear["pros"]!.AsArray());
        Assert.Equal(JsonValueKind.String, pro!.GetValueKind());
        Assert.Equal("penetrating", pro.GetValue<string>());
    }

    [Fact]
    public void PlainGearStillExportsAllNineKeysWithEmptyCollectionsAndNoCatalogueRow()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Gear.Add(new SelectedGear("Padded costume"));

        var gear = Render(sheet)["gear"]!.AsArray().Select(g => g!.AsObject()).Single();

        AssertKeys(gear, "name", "catalogue_id", "bonus_dice", "catalogue_features",
            "cost", "paired_under_two_fisted", "features", "pros", "cons");

        Assert.Equal(0, gear["cost"]!.GetValue<int>());
        Assert.Empty(gear["features"]!.AsArray());

        // **The three catalogue keys are present and null**, which is the shape a reader can rely
        // on: an item somebody typed names no row, and the two figures beside a row it does not
        // have would be invented. They are written rather than omitted for the reason every other
        // key here is — a key that comes and goes is a shape a parser has to branch on.
        Assert.Null(gear["catalogue_id"]);
        Assert.Null(gear["bonus_dice"]);
        Assert.Null(gear["catalogue_features"]);
    }

    /// <summary>
    /// <b>An item chosen off Chapter 6's catalogue exports the row it names and what the page
    /// prints beside it.</b> The id is the fact — a reader with <c>gear.json</c> can resolve
    /// everything else from it — and the two figures are the convenience for a reader without it.
    /// </summary>
    [Fact]
    public void ACatalogueItemExportsItsRowIdAndThePrintedFigures()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Gear.Add(new SelectedGear("Battle Axe")
        {
            CatalogueId = GearCatalogue.WeaponPrefix + "battle_axe"
        });

        var gear = Render(sheet)["gear"]!.AsArray().Select(g => g!.AsObject()).Single();

        Assert.Equal(GearCatalogue.WeaponPrefix + "battle_axe", gear["catalogue_id"]!.GetValue<string>());
        Assert.Equal(3, gear["bonus_dice"]!.GetValue<int>());
        Assert.Equal(["Two-Handed"], gear["catalogue_features"]!.AsArray().Select(f => f!.GetValue<string>()));

        // Still free: p.91 says mundane gear is not bought, and a battle axe is mundane gear.
        Assert.Equal(0, gear["cost"]!.GetValue<int>());
    }

    // ── Derived stats and narrative ───────────────────────────────────────────

    // ── Chapter 6's vehicles, headquarters and Gadgets ───────────────────────

    /// <summary>
    /// <b>A vehicle exports its two currencies apart</b>: the Hero Points its Perk cost, and the
    /// Vehicle Points those bought and spent. A document that gave one number would be one a
    /// reader had to guess the unit of.
    /// </summary>
    [Fact]
    public void AVehicleExportsBothCurrenciesAndItsFourRanks()
    {
        var sheet = SampleCharacters.Hero();
        sheet.Vehicles.Add(new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 2,
            Body = 8, Speed = 10, Control = 5, Weapons = 12,
            Features =
            [
                new SelectedAssetFeature("flight"),
                new SelectedAssetFeature("passengers") { Units = 3 },
                new SelectedAssetFeature("hidden_compartments") { GradeKey = "large" }
            ]
        });

        var vehicle = FirstOf(Render(sheet)["vehicles"]!.AsArray());

        AssertKeys(vehicle, "name", "perk_hero_points", "vehicle_points_budget",
            "vehicle_points_spent", "body", "speed", "control", "weapons", "features");

        Assert.Equal("The Wing", vehicle["name"]!.GetValue<string>());
        Assert.Equal(2,  vehicle["perk_hero_points"]!.GetValue<int>());
        Assert.Equal(50, vehicle["vehicle_points_budget"]!.GetValue<int>());
        Assert.Equal(_f.Costs.VehiclePointsSpent(sheet.Vehicles[0]),
                     vehicle["vehicle_points_spent"]!.GetValue<int>());
        Assert.Equal(12, vehicle["weapons"]!.GetValue<int>());

        var features = vehicle["features"]!.AsArray();
        Assert.Equal(3, features.Count);
        AssertKeys(FirstOf(features), "id", "name", "units", "grade");

        // The name comes off the rules data, never off the character — a price or a name
        // corrected in vehicles.json corrects every sheet that names the row.
        Assert.Equal("Flight", FirstOf(features)["name"]!.GetValue<string>());
        Assert.Equal("large", features[2]!["grade"]!.GetValue<string>());
    }

    /// <summary>
    /// <b>An unarmed machine exports null Weapons, not zero.</b> The printed tables give it an em
    /// dash, which is a different claim from a rank of nothing — and a reader summing weapon ranks
    /// across a fleet would count a zero.
    /// </summary>
    [Fact]
    public void AnUnarmedVehicleExportsNullWeapons()
    {
        var sheet = SampleCharacters.Hero();
        sheet.Vehicles.Add(new OwnedVehicle("The Van") { PerkHeroPoints = 1, Body = 6, Speed = 6 });

        var vehicle = FirstOf(Render(sheet)["vehicles"]!.AsArray());

        // A present-but-JSON-null property is the key existing with a null CLR reference, not a
        // JsonValue wrapping null — so the presence and the null-ness are two assertions.
        Assert.True(vehicle.ContainsKey("weapons"));
        Assert.Null(vehicle["weapons"]);

        // The control: an armed one really does carry a number, so the null above is about this
        // machine and not about the key being unwritten.
        sheet.Vehicles[0] = sheet.Vehicles[0] with { Weapons = 4 };
        Assert.Equal(4, FirstOf(Render(sheet)["vehicles"]!.AsArray())["weapons"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>A feature the rulebook does not have leaves the spend null rather than taking the whole
    /// report down.</b> The id is kept and the validator names it — reported, never repaired — and
    /// a reader gets every other figure on the sheet.
    /// </summary>
    [Fact]
    public void AnUnpriceableVehicleExportsANullSpendAndKeepsTheId()
    {
        var sheet = SampleCharacters.Hero();
        sheet.Vehicles.Add(new OwnedVehicle("The Mystery")
        {
            PerkHeroPoints = 1,
            Features = [new SelectedAssetFeature("teleport_bay")]
        });

        var document = Render(sheet);
        var vehicle = FirstOf(document["vehicles"]!.AsArray());

        Assert.True(vehicle.ContainsKey("vehicle_points_spent"));
        Assert.Null(vehicle["vehicle_points_spent"]);
        Assert.Equal("teleport_bay", FirstOf(vehicle["features"]!.AsArray())["id"]!.GetValue<string>());

        // And the report still says what is wrong, which is the half that makes the null readable.
        var errors = document["validation"]!["errors"]!.AsArray().Select(e => e!.GetValue<string>());
        Assert.Contains(errors, e => e.Contains("UNKNOWN_ASSET_FEATURE", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A headquarters exports Base Points, and a Gadget exports a pool that was paid out.</b>
    /// The Gadget's key is <c>hero_points_granted</c> rather than a cost, because a reader who
    /// netted it off the total would have a cheaper character than the one on the page.
    /// </summary>
    [Fact]
    public void AHeadquartersAndAGadgetExportTheirOwnCurrencies()
    {
        var sheet = SampleCharacters.Hero();
        sheet.TalentRanks["technology"] = 6;

        sheet.Headquarters.Add(new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("training_facilities")]
        });
        sheet.Gadgets.Add(new BuiltGadget("Freeze Ray")
        {
            Complexity = 5,
            Powers = [new SelectedPower("blast", 4)],
            AbilityRanks = new Dictionary<string, int> { ["might"] = 1 }
        });

        var document = Render(sheet);

        var headquarters = FirstOf(document["headquarters"]!.AsArray());
        AssertKeys(headquarters, "name", "perk_hero_points", "base_points_budget",
            "base_points_spent", "features");
        Assert.Equal(6, headquarters["base_points_budget"]!.GetValue<int>());
        Assert.Equal(2, headquarters["base_points_spent"]!.GetValue<int>());

        var gadget = FirstOf(document["gadgets"]!.AsArray());
        AssertKeys(gadget, "name", "complexity", "hero_points_granted", "hero_points_spent",
            "powers", "ability_ranks", "talent_ranks");
        Assert.Equal(10, gadget["hero_points_granted"]!.GetValue<int>());
        Assert.Equal(_f.Costs.GadgetSpend(sheet.Gadgets[0]), gadget["hero_points_spent"]!.GetValue<int>());
        Assert.Equal(["blast"], gadget["powers"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.Equal(1, gadget["ability_ranks"]!["might"]!.GetValue<int>());

        // Teamwork travels beside Resolve, computed for anybody: the engine is never told which
        // kind of character it has, and a host that knows keeps the silence.
        Assert.Equal(1, document["derived"]!["teamwork"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>A contribution to a campaign's shared object exports what went in and nothing about the
    /// object.</b> That is the whole shape: the campaign sums these, and a copy of the machine on
    /// each member's sheet would be five copies to disagree.
    /// </summary>
    [Fact]
    public void AContributionExportsWhatWentInAndNothingElse()
    {
        var sheet = SampleCharacters.Hero();
        sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-1")
        {
            Name = "The Aerie", Kind = CampaignAssetContribution.Headquarters, HeroPoints = 3
        });

        var contribution = FirstOf(Render(sheet)["campaign_assets"]!.AsArray());

        AssertKeys(contribution, "asset_id", "name", "kind", "hero_points");
        Assert.Equal("asset-1", contribution["asset_id"]!.GetValue<string>());
        Assert.Equal("headquarters", contribution["kind"]!.GetValue<string>());
        Assert.Equal(3, contribution["hero_points"]!.GetValue<int>());
    }

    /// <summary>
    /// <b>All four collections survive a round trip through the strict reader</b>, and a character
    /// stored before any of them existed still reads — which is why nothing here bumped a stored
    /// character's version.
    ///
    /// <para><b>They are not absent from a fresh payload, and an earlier draft of this test claimed
    /// they were.</b> An empty list is written as <c>[]</c>, not omitted — <c>WhenWritingNull</c>
    /// does not skip one — so what makes the old payload safe is the reader, not the writer.</para>
    /// </summary>
    [Fact]
    public void TheFourNewCollectionsRoundTripAndAnOlderPayloadStillReads()
    {
        var sheet = SampleCharacters.Hero();
        sheet.TalentRanks["technology"] = 6;
        sheet.Vehicles.Add(new OwnedVehicle("The Wing")
        {
            PerkHeroPoints = 2, Body = 8, Speed = 10, Control = 5, Weapons = 12,
            Features = [new SelectedAssetFeature("passengers") { Units = 3 }]
        });
        sheet.Headquarters.Add(new OwnedHeadquarters("The Loft")
        {
            PerkHeroPoints = 2,
            Features = [new SelectedAssetFeature("science_labs") { GradeKey = "advanced" }]
        });
        sheet.Gadgets.Add(new BuiltGadget("Freeze Ray")
        {
            Complexity = 5,
            Powers = [new SelectedPower("blast", 4)],
            TalentRanks = new Dictionary<string, int> { ["vehicles"] = 1 }
        });
        sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-1")
        {
            Name = "The Aerie", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 3
        });

        var read = CharacterSheetJson.Read(CharacterSheetJson.Write(sheet), strict: true)!;

        Assert.Equal(12, read.Vehicles[0].Weapons);
        Assert.Equal(3,  read.Vehicles[0].Features[0].Units);
        Assert.Equal("advanced", read.Headquarters[0].Features[0].GradeKey);
        Assert.Equal(5,  read.Gadgets[0].Complexity);
        Assert.Equal("blast", read.Gadgets[0].Powers[0].PowerId);
        Assert.Equal(1,  read.Gadgets[0].TalentRanks["vehicles"]);
        Assert.Equal("asset-1", read.CampaignAssets[0].AssetId);

        // The totals agree either side, which is the claim a key-by-key comparison would not make.
        Assert.Equal(_f.Costs.TotalCost(sheet), _f.Costs.TotalCost(read));

        // **A character stored before any of this existed still reads**, which is why nothing here
        // bumped a stored character's version. Absent means empty on all four, and the strict
        // reader — the one that refuses a field it does not know — is the harder of the two to
        // satisfy, so it is the one asked.
        var old = CharacterSheetJson.Read(
            """{"Name":"Nobody","SelectedTierId":"standard","AbilityRanks":{"might":3}}""",
            strict: true)!;

        Assert.Empty(old.Vehicles);
        Assert.Empty(old.Headquarters);
        Assert.Empty(old.Gadgets);
        Assert.Empty(old.CampaignAssets);
        Assert.Equal(3, old.GetAbilityRank("might"));

        // The control on that: the four really are written when there is something in them, so
        // the emptiness above is about the payload rather than about four properties nothing
        // serialises.
        var full = CharacterSheetJson.Write(sheet);
        Assert.Contains("\"Vehicles\":", full, StringComparison.Ordinal);
        Assert.Contains("\"CampaignAssets\":", full, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedCarriesEdgeHealthAndResolveAndTheyAgreeWithTheEngine()
    {
        var sheet = SampleCharacters.Hero();
        var derived = Render(sheet)["derived"]!.AsObject();

        AssertKeys(derived, "edge", "health", "resolve", "armor_from_gear", "teamwork");
        Assert.Equal(_f.Derived.CalculateEdge(sheet), derived["edge"]!.GetValue<int>());
        Assert.Equal(_f.Derived.CalculateHealth(sheet), derived["health"]!.GetValue<int>());
        Assert.Equal(_f.Derived.CalculateResolve(sheet), derived["resolve"]!.GetValue<int>());

        // Null on a character wearing no armour, which is the sample Hero — the figure is granted
        // by a suit and there is none.
        Assert.Null(derived["armor_from_gear"]);
    }

    /// <summary>
    /// <b>And it carries the rank once a suit is worn — capped by the Gear Limit, not by the
    /// wearer's Toughness.</b> p.88 gives the rank as Toughness plus the suit's bonus; p.87 caps
    /// the Toughness half. A 10d Hero in Plate is 8d in a standard game, which is p.87's own
    /// stated intent: mundane armour is less useful to a superhuman.
    /// </summary>
    [Fact]
    public void DerivedCarriesTheArmorRankAWornSuitGrants()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 10;
        sheet.Gear.Add(new SelectedGear("Plate")
        {
            CatalogueId = GearCatalogue.ArmorPrefix + "ancient_plate"
        });

        var derived = Render(sheet)["derived"]!.AsObject();

        Assert.Equal(8, derived["armor_from_gear"]!.GetValue<int>());

        // The control: the wearer's whole Toughness plus the bonus would be 12, which is the
        // reading p.87 rules out and the one this figure would silently be if the cap were lost.
        Assert.NotEqual(12, derived["armor_from_gear"]!.GetValue<int>());
    }

    [Fact]
    public void NarrativeCarriesTheFourFreeTextFieldsAndConnections()
    {
        var narrative = Hero()["narrative"]!.AsObject();

        AssertKeys(narrative, "appearance", "motivation", "quote", "connections");
        var connections = narrative["connections"]!.AsArray();
        Assert.NotEmpty(connections);
        Assert.Equal(JsonValueKind.String, connections[0]!.GetValueKind());
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public void ValidationCarriesValidErrorsAndWarnings()
    {
        var hero = Hero();
        var validation = hero["validation"]!.AsObject();

        AssertKeys(validation, "valid", "errors", "warnings");
        Assert.Equal(JsonValueKind.True, validation["valid"]!.GetValueKind());
        Assert.Empty(validation["errors"]!.AsArray());
    }

    /// <summary>
    /// A legal character can still carry warnings — the Villain's unsourced Power is one —
    /// so <c>errors</c> being empty is not the same claim as <c>warnings</c> being empty,
    /// and both arrays need a sample that actually exercises them.
    /// </summary>
    [Fact]
    public void WarningsCarryTheCodeAndMessageEvenOnALegalCharacter()
    {
        var warnings = Villain()["validation"]!.AsObject()["warnings"]!.AsArray();

        Assert.NotEmpty(warnings);
        Assert.All(warnings, w => Assert.Equal(JsonValueKind.String, w!.GetValueKind()));
        Assert.Contains(warnings, w => w!.GetValue<string>().Contains("POWER_WITHOUT_SOURCE",
            StringComparison.Ordinal));
    }
}
