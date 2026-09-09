using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every field in every rules file is read by some model.</b>
///
/// <para>The failure this exists for: <c>creation_rules.json</c> carried nine top-level keys
/// and <c>CreationRulesModel</c> declared four. The other five deserialized into nothing —
/// among them a <c>derived_characteristics</c> block restating the Edge, Health and Resolve
/// formulas in prose, which had drifted from the engine and said Resolve adds "Determination
/// ranks" long after Determination was settled as having no rank at all. It was marked
/// <c>needs_review: false</c>. Nothing noticed for as long as the block existed, because a
/// rules file's whole premise is that a data edit contradicting the book fails a test — and
/// this one could not fail a test, since nothing loaded it.</para>
///
/// <para>Unread data is worse than missing data. Missing data is a gap somebody can see;
/// unread data reads like a source of truth and is not one.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class RulesFileCoverageTests
{
    private readonly RulesFixture _f;

    public RulesFileCoverageTests(RulesFixture fixture) => _f = fixture;

    /// <summary>
    /// The repository's own options, plus the one thing it must not do at runtime: refuse an
    /// unmapped field. <b>The engine has to stay lenient</b> — a rules file gaining a field
    /// should not take the app down — so strictness lives here instead, where it is a failing
    /// test rather than a broken site.
    /// </summary>
    private static JsonSerializerOptions Strict() => new()
    {
        PropertyNamingPolicy         = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive  = true,
        ReadCommentHandling          = JsonCommentHandling.Skip,
        UnmappedMemberHandling       = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Which model each rules file is read into, by <see cref="RulesRepository"/>.</summary>
    private static readonly (string File, Type Model)[] Coverage =
    [
        ("tiers.json",          typeof(List<TierModel>)),
        ("abilities.json",      typeof(List<AbilityModel>)),
        ("talents.json",        typeof(List<TalentModel>)),
        ("powers.json",         typeof(List<PowerModel>)),
        ("pros.json",           typeof(List<ProModel>)),
        ("cons.json",           typeof(List<ConModel>)),
        ("flaws.json",          typeof(List<FlawModel>)),
        ("perks.json",          typeof(List<PerkModel>)),
        ("gear_features.json",  typeof(List<GearFeatureModel>)),
        ("sources.json",        typeof(List<SourceModel>)),
        ("creation_rules.json", typeof(CreationRulesModel)),
        ("gear.json",           typeof(EquipmentDataModel))
    ];

    public static TheoryData<string, Type> FilesAndModels()
    {
        var data = new TheoryData<string, Type>();
        foreach (var (file, model) in Coverage) data.Add(file, model);
        return data;
    }

    [Theory]
    [MemberData(nameof(FilesAndModels))]
    public void EveryFieldInARulesFileIsReadBySomeModel(string fileName, Type model)
    {
        var json = File.ReadAllText(Path.Combine(RulesFixture.DataPath, fileName));

        // Throws JsonException naming the offending field when a key maps to no property.
        var ex = Record.Exception(() => JsonSerializer.Deserialize(json, model, Strict()));

        Assert.True(ex is null,
            $"{fileName} carries a field no model reads, so nothing can hold it to the "
            + $"rulebook: {ex?.Message}");
    }

    /// <summary>
    /// The list the test above iterates has to be the list the repository actually loads, or
    /// a new rules file arrives uncovered and this whole file says nothing about it.
    /// </summary>
    [Fact]
    public void EveryFileTheRepositoryLoadsIsCoveredHere()
    {
        var covered = Coverage.Select(c => c.File).Order();

        Assert.Equal(RulesRepository.DataFileNames.Order(), covered);
    }

    /// <summary>
    /// <b>The formulas live in the engine, not in a rules file.</b> The deleted
    /// <c>derived_characteristics</c> block was a second statement of what
    /// <see cref="DerivedStatsCalculator"/> computes, and a second statement of a rule is the
    /// drift this repository has been bitten by three times — in the README roadmap, in the
    /// gaps list, and here. This asserts it does not come back.
    /// </summary>
    [Fact]
    public void TheDerivedStatFormulasAreNotRestatedInTheRulesData()
    {
        // <b>Scanned for the formulas themselves, not for the two key names they used to sit
        // under.</b> A review defeated the first version of this test in one move: several
        // properties here are IReadOnlyDictionary&lt;string, string&gt;, which absorbs any key at
        // all, so adding sequence_notes."derived_stat_formulas" — carrying the same rotted
        // "Resolve adds Determination ranks" sentence — reintroduced the exact regression this
        // test exists to prevent and left the whole suite green.
        string[] tells =
        [
            "derived_characteristics", "trait_costs",
            "Determination ranks", "per rank of Determination",
            "Perception + max", "highestRelevantRank", "TraitCap -"
        ];

        foreach (var fileName in RulesRepository.DataFileNames)
        {
            var json = File.ReadAllText(Path.Combine(RulesFixture.DataPath, fileName));

            foreach (var tell in tells)
                Assert.DoesNotContain(tell, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// <b>A Foe has half a Hero's Health, and deleting the prose block nearly lost it.</b> The
    /// <c>derived_characteristics</c> block that was removed carried
    /// <c>"foe_modifier": "Foes use half the normal Health value"</c> — a real rule (Ch.2 p.60,
    /// "When creating a Foe, use half this value"), and the only statement of it anywhere in
    /// the repository. The deletion was justified on the grounds that everything in the block
    /// was said authoritatively elsewhere, and for this one key that was simply untrue.
    ///
    /// <para>It is recorded on the tier rather than restored to a prose blob, and it is
    /// deliberately not applied: this tool builds Heroes, and nothing asks it for a Foe.</para>
    /// </summary>
    [Fact]
    public void TheFoeHealthRuleSurvivedTheDeletionOfTheProseBlock()
    {
        var notes = _f.Rules.CreationRules.TraitRankLimits.Notes
                  + " " + _f.Rules.CreationRules.GlobalCaps.Notes
                  + " " + _f.Rules.CreationRules.Advancement.SpendingNotes;

        Assert.Contains("Foe", notes, StringComparison.Ordinal);
        Assert.Contains("half", notes, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>Recording a sentence is not the same as recording it correctly.</b> Four
    /// <c>Assert.Contains</c> calls on "3 ranks", "6 ranks", "9d" and "18d" cannot see how the
    /// four are paired — a review swapped the thresholds, so the text read "3 ranks at 18d, 6
    /// ranks at 9d", and every test stayed green. That is the single likeliest transcription
    /// error for this sentence, and it was the one thing the test could not catch.
    /// </summary>
    [Fact]
    public void TheOptionalCapRulePairsEachAllowanceWithTheRightThreshold()
    {
        var text = _f.Rules.CreationRules.GlobalCaps.OverkillWeakException;

        var three = text.IndexOf("3 ranks", StringComparison.Ordinal);
        var six   = text.IndexOf("6 ranks", StringComparison.Ordinal);
        var nine  = text.IndexOf("9d", StringComparison.Ordinal);
        var teen  = text.IndexOf("18d", StringComparison.Ordinal);

        Assert.All(new[] { three, six, nine, teen }, i => Assert.True(i >= 0));

        // Ch.2 p.52: 3 ranks goes with 9d and 6 ranks with 18d, in that order.
        Assert.True(three < nine && nine < six && six < teen,
            $"The allowances and thresholds are paired wrongly: \"{text}\"");
    }

    /// <summary>
    /// The two Talents whose entries print a mechanical use, with their actual numbers rather
    /// than a not-null check. Changing Medicine's threshold from Hard (2) to 9 passed the whole
    /// suite: the property was modelled, which silenced the unread-field guard, and then
    /// nothing held the value to the page. An unused property that satisfies the guard and
    /// asserts nothing is the failure the guard's own docstring warns about.
    /// </summary>
    [Fact]
    public void TheTwoTalentSpecialUsesMatchTheRulebook()
    {
        var medicine = _f.Rules.GetTalent("medicine")!.SpecialUse;
        Assert.NotNull(medicine);
        Assert.Equal("treat_wounds", medicine.Action);
        Assert.Equal(2, medicine.Threshold);          // Hard
        Assert.Equal(3, medicine.ThresholdSelf);      // Daunting, treating yourself
        Assert.Equal(1, medicine.DamageHealedPerNetSuccess);

        var technology = _f.Rules.GetTalent("technology")!.SpecialUse;
        Assert.NotNull(technology);
        Assert.Equal("repair_object", technology.Action);
        Assert.Equal(2, technology.Threshold);
        Assert.Equal(1, technology.DamageRepairedPerNetSuccess);

        // The other ten print no such use, and inventing one for them would be a rule.
        Assert.Equal(2, _f.Rules.Talents.Count(t => t.SpecialUse is not null));
    }

    /// <summary>
    /// <b>The printed rank tables, locked like every other printed table.</b> They were
    /// extracted long ago and read into <c>RankGuide</c>, but nothing asserted their values —
    /// so an invented <c>"0d"</c> entry passed the whole suite, and the documents had begun
    /// telling the next session this table was finished business.
    /// </summary>
    [Fact]
    public void ThePrintedRankTablesAreWhatTheRulebookPrints()
    {
        string[] abilities = ["Impaired", "Undeveloped", "Developed", "Noteworthy", "Exceptional", "Peak"];
        string[] talents   = ["Clueless", "Unskilled", "Proficient", "Advanced", "Expert", "Master"];

        // 1d to 6d and nothing else: the tables stop at 6d because above that is superhuman.
        string[] ranks = ["1d", "2d", "3d", "4d", "5d", "6d"];

        Assert.All(_f.Rules.Abilities, a =>
        {
            Assert.Equal(ranks, a.RankGuide.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(ranks.Index(), pair =>
                Assert.StartsWith(abilities[pair.Index], a.RankGuide[pair.Item], StringComparison.Ordinal));
        });

        Assert.All(_f.Rules.Talents, t =>
        {
            Assert.Equal(ranks, t.RankGuide.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(ranks.Index(), pair =>
                Assert.StartsWith(talents[pair.Index], t.RankGuide[pair.Item], StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Advancement, transcribed from Ch.2 p.62 and now readable, so the page can hold it.
    /// Nothing consumes these figures — this tool builds a starting character — which is
    /// exactly why they need a test rather than a consumer.
    /// </summary>
    [Fact]
    public void AdvancementMatchesTheRulebook()
    {
        var a = _f.Rules.CreationRules.Advancement;

        Assert.Equal(1, a.HpPer3Issues);
        Assert.Equal(1, a.HpForNoteworthyAchievement);
        Assert.Equal(1, a.HpForMajorLifeEvent);

        Assert.Equal(["floating_cap", "hard_cap"], a.TraitCapOptions.Keys.Order());
        Assert.Contains("+1d", a.TraitCapOptions["floating_cap"], StringComparison.Ordinal);
        Assert.Contains("10", a.TraitCapOptions["floating_cap"], StringComparison.Ordinal);

        Assert.False(string.IsNullOrWhiteSpace(a.SpendingNotes));
        Assert.False(string.IsNullOrWhiteSpace(a.Retcons));
    }

    /// <summary>
    /// Both optional Trait Cap rules, which the rulebook prints inside the Overkill Con's own
    /// entry (Ch.2 p.52) rather than beside the caps — which is why one of them was recorded
    /// and the other was not until somebody read the page.
    /// </summary>
    [Fact]
    public void BothOptionalTraitCapRulesAreRecorded()
    {
        var caps = _f.Rules.CreationRules.GlobalCaps;

        Assert.Contains("3 ranks", caps.OverkillWeakException, StringComparison.Ordinal);
        Assert.Contains("6 ranks", caps.OverkillWeakException, StringComparison.Ordinal);
        Assert.Contains("9d", caps.OverkillWeakException, StringComparison.Ordinal);
        Assert.Contains("18d", caps.OverkillWeakException, StringComparison.Ordinal);

        Assert.Contains("9d", caps.OverkillWeakRequirement, StringComparison.Ordinal);
        Assert.Contains("p.52", caps.SourceRef ?? "", StringComparison.Ordinal);

        // Neither is enforced: the engine is never told which house rules are in play, and the
        // Trait Cap check stays the tier's. Stated here so that stays a decision.
        Assert.Equal(12, _f.Rules.GetTier("standard")!.TraitCapRank);
    }
}
