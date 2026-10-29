using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The engine's Pet/Sidekick calculator under a name a component may hold — the same indirection
/// <see cref="AlternateFormEngine"/> is, for the same guard. It forwards and decides nothing.
/// </summary>
public sealed class CompanionEngine
{
    private readonly Companions _companions;

    public CompanionEngine(Companions companions) =>
        _companions = companions ?? throw new ArgumentNullException(nameof(companions));

    public IReadOnlyList<CompanionFamily> Families(IReadOnlyList<RosterEntry> roster) =>
        _companions.Families(roster);
}

/// <summary>
/// One sheet's Pet/Sidekick family as the engine answers it, from where that sheet stands: its
/// owner's side or a companion's.
/// </summary>
/// <param name="ShownId">The id of the sheet this was asked about.</param>
/// <param name="Family">The engine's answer for the whole family.</param>
/// <param name="OwnerName">What the owner is called, for the lines that name them.</param>
/// <param name="Companions">Each companion's id and name, in roster order.</param>
/// <param name="AllRead">
/// False when a member could not be read. Every figure here is then about part of a family, and
/// a host has to say so rather than print it as the answer.
/// </param>
public sealed record CompanionView(
    string ShownId,
    CompanionFamily Family,
    string OwnerName,
    IReadOnlyList<(string Id, string Name)> Companions,
    bool AllRead)
{
    /// <summary>Whether the sheet asked about is a companion rather than the owner.</summary>
    public bool ShownIsCompanion => !string.Equals(ShownId, Family.OwnerId, StringComparison.Ordinal);

    /// <summary>The Hero Points the shown companion was matched to, or null — for the owner, or a companion nothing pays for.</summary>
    public int? Budget => ShownIsCompanion ? Family.Budget.GetValueOrDefault(ShownId) : null;

    /// <summary>The family's findings about the shown sheet.</summary>
    public IReadOnlyList<ValidationIssue> Issues =>
        [.. Family.Issues.Where(i => string.Equals(i.SubjectId, ShownId, StringComparison.Ordinal))];

    /// <summary>The companions' names joined for a line: "Talon", "Talon and Brute".</summary>
    public string CompanionNames =>
        Companions.Count switch
        {
            0 => "",
            1 => Companions[0].Name,
            _ => $"{string.Join(", ", Companions.Take(Companions.Count - 1).Select(c => c.Name))} and {Companions[^1].Name}"
        };
}

/// <summary>
/// Everything about a Pet/Sidekick family except the sheet on screen: who the owner is, which
/// stored characters are its companions, and their sheets, read once. <see cref="View"/> then asks
/// the engine with the live sheet in its place, so an edit on screen moves every figure at once
/// without reading storage again.
///
/// <para><b>The engine judges; this only assembles the roster</b>, the way
/// <see cref="AlternateFormRosterNotes"/> does — who is linked to whom is read off the index's
/// <c>VariantOf</c>/<c>VariantKind</c>, and each member's own sheet off storage.</para>
/// </summary>
public sealed class CompanionRoster
{
    private readonly string _shownId;
    private readonly string _ownerId;
    private readonly IReadOnlyList<RosterEntry> _others;
    private readonly IReadOnlyList<string> _order;
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly bool _allRead;

    private CompanionRoster(
        string shownId, string ownerId, IReadOnlyList<RosterEntry> others, IReadOnlyList<string> order,
        IReadOnlyDictionary<string, string> names, bool allRead)
    {
        _shownId = shownId;
        _ownerId = ownerId;
        _others  = others;
        _order   = order;
        _names   = names;
        _allRead = allRead;
    }

    /// <summary>The fallback id for a sheet that has none yet — never one a link names.</summary>
    public const string Unsaved = "unsaved";

    /// <summary>Whether a sheet is a companion: linked to an owner by a <c>companion</c> link.</summary>
    public static bool IsCompanion(CharacterSheet sheet) => Companions.IsCompanion(sheet);

    /// <summary>Whether a sheet holds a Perk that buys a companion.</summary>
    public static bool HoldsThePerk(CharacterSheet sheet, RulesRepository rules) => Companions.BuysACompanion(sheet, rules);

    /// <summary>
    /// The family <paramref name="sheet"/> belongs to, or null when it belongs to none: it is not a
    /// companion, holds no Pet/Sidekick and nothing names it as an owner.
    /// </summary>
    /// <param name="id">The sheet's stored id, or null for one never saved.</param>
    /// <param name="list">The index of every stored character, for who links to whom.</param>
    /// <param name="load">One stored sheet by id, or null where it cannot be read.</param>
    public static async Task<CompanionRoster?> LoadAsync(
        string? id, CharacterSheet sheet, RulesRepository rules,
        Func<Task<IReadOnlyList<SavedCharacterSummary>>> list,
        Func<string, Task<CharacterSheet?>> load)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(load);

        var shown = string.IsNullOrWhiteSpace(id) ? Unsaved : id;

        if (!IsCompanion(sheet) && !HoldsThePerk(sheet, rules) && id is null) return null;

        var summaries = await list();
        var ownerId   = IsCompanion(sheet) ? sheet.Variant!.OfCharacterId : shown;

        var companionIds = summaries
            .Where(s => string.Equals(s.VariantOf, ownerId, StringComparison.Ordinal)
                        && string.Equals(s.VariantKind, CharacterVariant.Companion, StringComparison.Ordinal))
            .Select(s => s.Id)
            .ToList();

        if (!IsCompanion(sheet) && !HoldsThePerk(sheet, rules) && companionIds.Count == 0) return null;

        var names = summaries.ToDictionary(s => s.Id, s => s.Label, StringComparer.Ordinal);
        var others = new List<RosterEntry>();
        var allRead = true;

        // The owner first, then the companions in the index's order — the order the engine
        // matches purchases in, so the owner's screen and each companion's agree on who got what.
        var order = new List<string> { ownerId };
        order.AddRange(companionIds);
        if (IsCompanion(sheet) && !order.Contains(shown)) order.Add(shown);

        foreach (var member in order.Where(m => !string.Equals(m, shown, StringComparison.Ordinal)))
        {
            if (await load(member) is { } other) others.Add(new RosterEntry(member, other));
            else allRead = false;
        }

        return new CompanionRoster(shown, ownerId, others, order, names, allRead);
    }

    /// <summary>The engine's answer for this family, with <paramref name="live"/> as the sheet on screen.</summary>
    public CompanionView? View(CharacterSheet live, CompanionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(engine);

        var byId = _others.ToDictionary(e => e.Id, StringComparer.Ordinal);
        byId[_shownId] = new RosterEntry(_shownId, live);

        var roster = _order.Where(byId.ContainsKey).Select(m => byId[m]).ToList();

        CompanionFamily? family;
        try
        {
            family = engine.Families(roster)
                .SingleOrDefault(f => string.Equals(f.OwnerId, _ownerId, StringComparison.Ordinal));
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (family is null) return null;

        string NameOf(RosterEntry entry) =>
            !string.IsNullOrWhiteSpace(entry.Sheet.Name) ? entry.Sheet.Name
            : _names.GetValueOrDefault(entry.Id) is { Length: > 0 } label ? label
            : "Unnamed character";

        var ownerName = family.Owner is { } owner ? NameOf(owner)
            : _names.GetValueOrDefault(_ownerId) is { Length: > 0 } label ? label
            : "its owner";

        return new CompanionView(
            _shownId, family, ownerName,
            [.. family.Members.Select(m => (m.Id, NameOf(m)))],
            _allRead);
    }
}

/// <summary>
/// The Pet/Sidekick family of the character being built, kept beside the session: read when the
/// character on screen changes which family it is in, and answered afresh from the live sheet on
/// every ask, so the budget strip, the Perks tab and the sheet all show the engine's figure for
/// what is on screen now.
/// </summary>
public sealed class Companionship : IDisposable
{
    private readonly CharacterSession _session;
    private readonly AccountCharacterStore _store;
    private readonly CompanionEngine _engine;

    private CompanionRoster? _roster;

    /// <summary>What the roster was read for, so an ordinary edit does not read storage again.</summary>
    private string? _readFor;

    public Companionship(CharacterSession session, AccountCharacterStore store, CompanionEngine engine)
    {
        _session = session;
        _store   = store;
        _engine  = engine;
        _session.Changed += OnChanged;
    }

    /// <summary>Raised once a fresh read has landed, so a component drawing <see cref="Current"/> redraws.</summary>
    public event Action? Changed;

    /// <summary>The family of the character on screen, or null for one in none or not read yet.</summary>
    public CompanionView? Current => _roster?.View(_session.Sheet, _engine);

    /// <summary>
    /// The Hero Points the character on screen is measured against: a companion's owner's purchase
    /// (Ch.2 p.55), the tier's for anybody else, or null where there is no limit — no budget switch
    /// on, no tier, or a companion whose owner has not been read or pays for it with nothing.
    /// One answer, so the front door, the list and the strip cannot say different figures.
    /// </summary>
    public int? Budget =>
        Companions.IsCompanion(_session.Sheet) ? Current?.Budget
        : _session.ShowBudget && _session.Budget > 0 ? _session.Budget
        : null;

    /// <summary>The stored id of the character on screen: the session's, or the pointer's.</summary>
    public async Task<string> CurrentIdAsync() => _session.HeldId ?? await _store.CurrentIdAsync();

    /// <summary>Which read is the latest, so one that lands after a newer one is dropped rather than drawn.</summary>
    private int _generation;

    /// <summary>Reads the family the first time a screen asks, and not again until it moves.</summary>
    public Task EnsureAsync() => _readFor is null ? RefreshAsync() : Task.CompletedTask;

    /// <summary>Reads the family again — after a companion is made, or when the screen asks first.</summary>
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        var sheet = _session.Sheet;
        var id    = await CurrentIdAsync();
        _readFor  = Key(id, sheet);

        CompanionRoster? roster;
        try
        {
            roster = await CompanionRoster.LoadAsync(id, sheet, _session.Rules,
                async () => (await _store.ListAsync()).Characters,
                async other => (await _store.ReadAsync(other))?.Sheet);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            // Storage that cannot be asked leaves no family to show, and the sheet says its
            // figures are the character's own — never a guess at the rest.
            roster = null;
        }

        if (generation != _generation) return;

        _roster = roster;
        Changed?.Invoke();
    }

    /// <summary>Reads again only when the edit moved which family this is: a new link, a Perk bought or dropped, another character.</summary>
    private void OnChanged()
    {
        var sheet = _session.Sheet;
        var key = Key(_session.HeldId ?? _readFor?.Split('|')[0], sheet);
        if (key == _readFor) return;

        _ = RefreshAsync();
    }

    private string Key(string? id, CharacterSheet sheet) =>
        $"{id}|{sheet.Variant?.Kind}:{sheet.Variant?.OfCharacterId}|{CompanionRoster.HoldsThePerk(sheet, _session.Rules)}";

    public void Dispose() => _session.Changed -= OnChanged;
}
