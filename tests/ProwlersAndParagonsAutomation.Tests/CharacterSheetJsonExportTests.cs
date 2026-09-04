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
            "meta", "name", "tier", "package", "hp_budget", "trait_cap", "abilities", "talents",
            "source_groups", "powers", "perks", "flaws", "gear", "derived", "narrative",
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

        AssertKeys(gear[0], "name", "cost", "paired_under_two_fisted", "features", "pros", "cons");

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
    public void PlainGearStillExportsAllSixKeysWithEmptyCollections()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Gear.Add(new SelectedGear("Padded costume"));

        var gear = Render(sheet)["gear"]!.AsArray().Select(g => g!.AsObject()).Single();

        AssertKeys(gear, "name", "cost", "paired_under_two_fisted", "features", "pros", "cons");
        Assert.Equal(0, gear["cost"]!.GetValue<int>());
        Assert.Empty(gear["features"]!.AsArray());
    }

    // ── Derived stats and narrative ───────────────────────────────────────────

    [Fact]
    public void DerivedCarriesEdgeHealthAndResolveAndTheyAgreeWithTheEngine()
    {
        var sheet = SampleCharacters.Hero();
        var derived = Render(sheet)["derived"]!.AsObject();

        AssertKeys(derived, "edge", "health", "resolve");
        Assert.Equal(_f.Derived.CalculateEdge(sheet), derived["edge"]!.GetValue<int>());
        Assert.Equal(_f.Derived.CalculateHealth(sheet), derived["health"]!.GetValue<int>());
        Assert.Equal(_f.Derived.CalculateResolve(sheet), derived["resolve"]!.GetValue<int>());
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
