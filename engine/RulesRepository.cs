using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Loads and caches all rules data from the data/rules/ JSON files.
/// Each collection is loaded lazily on first access.
/// </summary>
public sealed class RulesRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _dataPath;

    // Lazy backing fields
    private IReadOnlyList<TierModel>? _tiers;
    private IReadOnlyList<AbilityModel>? _abilities;
    private IReadOnlyList<TalentModel>? _talents;
    private IReadOnlyList<PowerModel>? _powers;
    private IReadOnlyList<ProModel>? _pros;
    private IReadOnlyList<ConModel>? _cons;
    private IReadOnlyList<FlawModel>? _flaws;
    private IReadOnlyList<PerkModel>? _perks;
    private IReadOnlyList<GearFeatureModel>? _gearFeatures;
    private IReadOnlyList<SourceModel>? _sources;
    private CreationRulesModel? _creationRules;

    // Lookup dictionaries (built on first use)
    private Dictionary<string, TierModel>? _tierMap;
    private Dictionary<string, AbilityModel>? _abilityMap;
    private Dictionary<string, TalentModel>? _talentMap;
    private Dictionary<string, PowerModel>? _powerMap;
    private Dictionary<string, ProModel>? _proMap;
    private Dictionary<string, ConModel>? _conMap;
    private Dictionary<string, FlawModel>? _flawMap;
    private Dictionary<string, PerkModel>? _perkMap;
    private Dictionary<string, GearFeatureModel>? _gearFeatureMap;
    private Dictionary<string, SourceModel>? _sourceMap;

    public RulesRepository(string dataRulesPath)
    {
        _dataPath = dataRulesPath;
    }

    /// <summary>
    /// Convenience factory: appends "data/rules" to the provided base path.
    /// Typically called with AppContext.BaseDirectory or the project root.
    /// </summary>
    public static RulesRepository FromBasePath(string basePath) =>
        new(Path.Combine(basePath, "data", "rules"));

    // ── Collections ───────────────────────────────────────────────────────

    public IReadOnlyList<TierModel> Tiers =>
        _tiers ??= Load<List<TierModel>>("tiers.json");

    public IReadOnlyList<AbilityModel> Abilities =>
        _abilities ??= Load<List<AbilityModel>>("abilities.json");

    public IReadOnlyList<TalentModel> Talents =>
        _talents ??= Load<List<TalentModel>>("talents.json");

    public IReadOnlyList<PowerModel> Powers =>
        _powers ??= Load<List<PowerModel>>("powers.json");

    public IReadOnlyList<ProModel> Pros =>
        _pros ??= Load<List<ProModel>>("pros.json");

    public IReadOnlyList<ConModel> Cons =>
        _cons ??= Load<List<ConModel>>("cons.json");

    public IReadOnlyList<FlawModel> Flaws =>
        _flaws ??= Load<List<FlawModel>>("flaws.json");

    public IReadOnlyList<PerkModel> Perks =>
        _perks ??= Load<List<PerkModel>>("perks.json");

    /// <summary>
    /// Custom features that can be bought for a piece of mundane gear (Ch.6, p.92). The
    /// gear itself is free; these are the only part of it that costs Hero Points.
    /// </summary>
    public IReadOnlyList<GearFeatureModel> GearFeatures =>
        _gearFeatures ??= Load<List<GearFeatureModel>>("gear_features.json");

    /// <summary>
    /// The six Sources (Ch.2, p.15). A Source says what a Trait is meant to be, and sets
    /// the default rank a rankless Power uses when Powers act on other Powers.
    /// </summary>
    public IReadOnlyList<SourceModel> Sources =>
        _sources ??= Load<List<SourceModel>>("sources.json");

    public CreationRulesModel CreationRules =>
        _creationRules ??= Load<CreationRulesModel>("creation_rules.json");

    // ── Lookups ───────────────────────────────────────────────────────────

    public TierModel? GetTier(string id) =>
        (_tierMap ??= Tiers.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public AbilityModel? GetAbility(string id) =>
        (_abilityMap ??= Abilities.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public TalentModel? GetTalent(string id) =>
        (_talentMap ??= Talents.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public PowerModel? GetPower(string id) =>
        (_powerMap ??= Powers.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public ProModel? GetPro(string id) =>
        (_proMap ??= Pros.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public ConModel? GetCon(string id) =>
        (_conMap ??= Cons.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public FlawModel? GetFlaw(string id) =>
        (_flawMap ??= Flaws.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public PerkModel? GetPerk(string id) =>
        (_perkMap ??= Perks.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public GearFeatureModel? GetGearFeature(string id) =>
        (_gearFeatureMap ??= GearFeatures.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    public SourceModel? GetSource(string id) =>
        (_sourceMap ??= Sources.ToDictionary(x => x.Id)).GetValueOrDefault(id);

    // ── Private helpers ───────────────────────────────────────────────────

    private T Load<T>(string fileName)
    {
        var path = Path.Combine(_dataPath, fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize {fileName}.");
    }

}
