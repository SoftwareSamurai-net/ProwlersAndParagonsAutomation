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
        ("creation_rules.json", typeof(CreationRulesModel))
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
        foreach (var fileName in RulesRepository.DataFileNames)
        {
            var json = File.ReadAllText(Path.Combine(RulesFixture.DataPath, fileName));

            Assert.DoesNotContain("derived_characteristics", json, StringComparison.Ordinal);
            Assert.DoesNotContain("trait_costs", json, StringComparison.Ordinal);
        }
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
