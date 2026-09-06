using System.Text.Json;
using System.Text.Json.Serialization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Rules.Models;

namespace ProwlersAndParagonsAutomation.Play.Rules;

/// <summary>
/// Loads and caches the five <c>data/rules/play</c> files — the play rules, which resolve an
/// action, as opposed to the character rules <see cref="RulesRepository"/> answers about cost and
/// validity.
///
/// <para><b>It is a second repository beside that one and not an extension of it.</b>
/// <c>CLAUDE.md</c> settles why in a line: <c>engine/</c> is the authority on cost and validity and
/// knows nothing about resolving an action. So the two lists of file names never meet —
/// <see cref="RulesRepository.DataFileNames"/> is the contract a browser fetches at boot and a play
/// file on it would put the whole of Chapters 3–5 into every first page load, which
/// <c>PlayPayloadTests</c> refuses.</para>
///
/// <para><b>It reads through <see cref="IRulesSource"/>, which stays synchronous.</b> Same seam and
/// same reason as the character engine: an async source would push <c>await</c> through every lazy
/// collection here and from there into <c>Encounter.Step</c>, turning a pure function into an async
/// one for nothing. A host that can only load asynchronously does so once and hands over strings.
/// Nothing in <c>play/</c> touches a filesystem — <c>AccountsContractTests</c> now scans this
/// project for that too.</para>
///
/// <para><b>Lenient at runtime, strict in a test.</b> This deserializer ignores a field no model
/// reads, so a data edit cannot take a host down; <c>PlayRulesFileCoverageTests</c> re-reads the
/// same five files with <c>JsonUnmappedMemberHandling.Disallow</c> and fails naming the field. That
/// is the shape <see cref="RulesRepository"/> and <c>RulesFileCoverageTests</c> already use, and it
/// exists because unread data reads as a source of truth and is not one.</para>
/// </summary>
public sealed class PlayRulesRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The file name of each play rules file, as it appears in <c>data/rules/play</c>.</summary>
    public const string PlayMetaFile = "play_meta.json";

    /// <inheritdoc cref="PlayMetaFile"/>
    public const string ChallengeFile = "challenge.json";

    /// <inheritdoc cref="PlayMetaFile"/>
    public const string CombatFile = "combat.json";

    /// <inheritdoc cref="PlayMetaFile"/>
    public const string GrittyFile = "gritty.json";

    /// <inheritdoc cref="PlayMetaFile"/>
    public const string ResolveFile = "resolve.json";

    /// <summary>
    /// Every play rules file a self-loading host must supply. <b>Deliberately not
    /// <see cref="RulesRepository.DataFileNames"/> and never merged into it</b>: that list is what a
    /// browser fetches for the character rules, and a play file on it is a public URL nobody asked
    /// for.
    /// </summary>
    public static IReadOnlyList<string> DataFileNames { get; } =
        [PlayMetaFile, ChallengeFile, CombatFile, GrittyFile, ResolveFile];

    private readonly IRulesSource _source;

    private PlayFile<PlayMetaEntry>? _meta;
    private PlayFile<ChallengeEntry>? _challenge;
    private PlayFile<CombatEntry>? _combat;
    private PlayFile<GrittyEntry>? _gritty;
    private PlayFile<ResolveEntry>? _resolve;

    private Dictionary<string, PlayMetaEntry>? _metaMap;
    private Dictionary<string, ChallengeEntry>? _challengeMap;
    private Dictionary<string, CombatEntry>? _combatMap;
    private Dictionary<string, GrittyEntry>? _grittyMap;
    private Dictionary<string, ResolveEntry>? _resolveMap;

    /// <summary>Reads the play rules from an arbitrary source — a directory, memory, anywhere.</summary>
    public PlayRulesRepository(IRulesSource source) => _source = source;

    /// <summary>The dice model: the pool, the success map, the sub-1d floor, the rounding rule.</summary>
    public PlayFile<PlayMetaEntry> Meta => _meta ??= Load<PlayMetaEntry>(PlayMetaFile);

    /// <summary>Chapter 3 Action, pp.67–72.</summary>
    public PlayFile<ChallengeEntry> Challenge => _challenge ??= Load<ChallengeEntry>(ChallengeFile);

    /// <summary>Chapter 4 Combat, pp.73–79.</summary>
    public PlayFile<CombatEntry> Combat => _combat ??= Load<CombatEntry>(CombatFile);

    /// <summary>Chapter 4's ten optional Gritty Combat Rules, pp.79–81, and the paragraph offering them.</summary>
    public PlayFile<GrittyEntry> Gritty => _gritty ??= Load<GrittyEntry>(GrittyFile);

    /// <summary>Chapter 5 Resolve and Adversity, pp.83–85.</summary>
    public PlayFile<ResolveEntry> Resolve => _resolve ??= Load<ResolveEntry>(ResolveFile);

    /// <summary>
    /// One entry by id, or a throw naming the file and the id.
    ///
    /// <para><b>Missing is a throw and not a null</b>, unlike the character repository's lookups.
    /// Every read here is a rule the engine is about to apply, and a rule that silently resolved to
    /// nothing would produce a plausible number with no rule behind it — which is precisely the
    /// class of defect this whole store was verified to prevent.</para>
    /// </summary>
    public PlayMetaEntry GetMeta(string id) =>
        Get(_metaMap ??= Meta.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal), id, PlayMetaFile);

    /// <inheritdoc cref="GetMeta"/>
    public ChallengeEntry GetChallenge(string id) =>
        Get(_challengeMap ??= Challenge.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal), id, ChallengeFile);

    /// <inheritdoc cref="GetMeta"/>
    public CombatEntry GetCombat(string id) =>
        Get(_combatMap ??= Combat.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal), id, CombatFile);

    /// <inheritdoc cref="GetMeta"/>
    public GrittyEntry GetGritty(string id) =>
        Get(_grittyMap ??= Gritty.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal), id, GrittyFile);

    /// <inheritdoc cref="GetMeta"/>
    public ResolveEntry GetResolve(string id) =>
        Get(_resolveMap ??= Resolve.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal), id, ResolveFile);

    /// <summary>
    /// Every entry id in the store, with the file it is in — what a table-settings check needs to
    /// prove a switch names a rule the book actually prints.
    /// </summary>
    public IEnumerable<(string File, string Id)> EntryIds() =>
        Meta.Entries.Select(e => (PlayMetaFile, e.Id))
            .Concat(Challenge.Entries.Select(e => (ChallengeFile, e.Id)))
            .Concat(Combat.Entries.Select(e => (CombatFile, e.Id)))
            .Concat(Gritty.Entries.Select(e => (GrittyFile, e.Id)))
            .Concat(Resolve.Entries.Select(e => (ResolveFile, e.Id)));

    private static T Get<T>(Dictionary<string, T> map, string id, string fileName) =>
        map.TryGetValue(id, out var entry)
            ? entry
            : throw new KeyNotFoundException(
                $"No entry '{id}' in {fileName}. Available: {string.Join(", ", map.Keys.Order(StringComparer.Ordinal))}.");

    private PlayFile<TEntry> Load<TEntry>(string fileName)
    {
        var json = _source.ReadAllText(fileName);
        return JsonSerializer.Deserialize<PlayFile<TEntry>>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize {fileName}.");
    }
}
