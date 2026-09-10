using System.Text.Json;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Which Warnings a player has waved off, remembered per character, in this browser and
/// nowhere else.
///
/// <para><b>Never on <see cref="CharacterSheet"/>, never in the export, never in a campaign's
/// shared copy of a character.</b> A dismissal recorded on the sheet would make the GM see "the
/// player waved this away", and it would be one more field every rules-adjacent reader — the
/// engine, the JSON report, a teammate's clone of this character — has to learn to ignore for no
/// reason of its own. This service is a browser-side opinion about what to <em>show</em>, not a
/// fact about the character, so it lives beside <see cref="SheetFindings"/> instead: the same
/// place that already decides which row a finding belongs on, not on the sheet those findings
/// are about.</para>
///
/// <para><b>Keyed on the same four fields <see cref="SheetFindings"/> already routes on</b> —
/// <see cref="ValidationIssue.Code"/>, <see cref="ValidationIssue.SubjectKind"/>,
/// <see cref="ValidationIssue.SubjectId"/> and <see cref="ValidationIssue.OwnerId"/> — never on
/// <c>Message</c> (the sentence can be reworded without the finding it names changing) and never
/// on <c>Value</c>/<c>Limit</c> (a dismissal that silently reappears the moment a number moves
/// reads as a broken control, not as a control that noticed something new).</para>
///
/// <para><b>Errors can never reach a dismissal here</b> — <see cref="DismissAsync"/> refuses one
/// outright and <see cref="IsDismissed"/> answers false for one regardless of what storage holds
/// — because <c>ValidationResult.IsValid</c> is "no Error", and a dismissable Error would let an
/// illegal character look legal on screen, against the settled rule that an illegal character is
/// reported and never repaired.</para>
///
/// <para><b>A browser that refuses storage dismisses nothing</b>, the same failure discipline
/// <see cref="SavedCharacters"/> uses: every method here swallows the same list of storage
/// failures, and a write that never lands never updates the in-memory answer either — so a
/// "dismiss" that storage refused looks, correctly, like nothing happened.</para>
/// </summary>
public sealed class DismissedFindings
{
    private readonly IJSRuntime _js;

    public DismissedFindings(IJSRuntime js) => _js = js;

    /// <summary>
    /// Which character's dismissals a session is looking at — <see cref="CharacterSession.HeldId"/>
    /// where the session knows one, and a fixed fallback for a character this browser holds no id
    /// for (an unsaved build, or the legacy slot before its first save). One spelling, so every
    /// caller keys the same character the same way.
    /// </summary>
    public static string CharacterKey(CharacterSession session) => session.HeldId ?? "current";

    private static string StorageKey(string characterId) => $"pp.findings.dismissed.v1.{characterId}";

    /// <summary>The four fields <see cref="SheetFindings"/> routes on, as one comparable value.</summary>
    private sealed record Key(string Code, string SubjectKind, string SubjectId, string OwnerId);

    private static Key KeyFor(ValidationIssue issue) =>
        new(issue.Code, issue.SubjectKind.ToString(), issue.SubjectId ?? "", issue.OwnerId ?? "");

    private readonly Dictionary<string, HashSet<Key>> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Makes sure this character's dismissed set has been read from storage at least once.
    /// Idempotent and cheap after the first call for a given id — callers are expected to call
    /// this from a lifecycle method that can run more than once (a character switch, a step
    /// re-entered) rather than guard it themselves.
    /// </summary>
    public async Task LoadAsync(string characterId)
    {
        if (_cache.ContainsKey(characterId)) return;

        try
        {
            var raw = await _js.InvokeAsync<string?>("ppStore.load", StorageKey(characterId));
            _cache[characterId] = string.IsNullOrWhiteSpace(raw)
                ? []
                : JsonSerializer.Deserialize<HashSet<Key>>(raw) ?? [];
        }
        // A hand-edited or corrupted set is not a crash: it is a character with nothing waved
        // off, same as one that was never written. See SavedCharacters.ReadIndexAsync.
        catch (Exception e) when (IsStorageFailure(e)) { _cache[characterId] = []; }
    }

    /// <summary>
    /// Whether this issue is currently waved off for this character. Requires
    /// <see cref="LoadAsync"/> to have been called for <paramref name="characterId"/> first;
    /// answers false (nothing dismissed) for an id nothing has loaded yet, the same honest
    /// default a browser that refuses storage gets.
    /// </summary>
    public bool IsDismissed(string characterId, ValidationIssue issue) =>
        issue.Severity == ValidationSeverity.Warning
        && _cache.TryGetValue(characterId, out var set)
        && set.Contains(KeyFor(issue));

    /// <summary>
    /// How many of <paramref name="liveIssues"/> are currently dismissed, for the review panel's
    /// "N warnings dismissed" note.
    ///
    /// <para><b>Counted against what the engine says right now</b>, not against however many
    /// keys storage happens to hold — a stale key for a Trait that has since changed away and
    /// back is not counted until the finding it names actually recurs.</para>
    /// </summary>
    public int VisibleCount(string characterId, IEnumerable<ValidationIssue> liveIssues) =>
        _cache.TryGetValue(characterId, out var set) && set.Count > 0
            ? liveIssues.Count(i => i.Severity == ValidationSeverity.Warning && set.Contains(KeyFor(i)))
            : 0;

    /// <summary>
    /// Waves off one Warning.
    ///
    /// <para><b>Refuses an Error outright</b> rather than trusting a caller's own filtering — the
    /// same defence <see cref="RowFinding"/>'s control is drawn under.</para>
    ///
    /// <para><b>Pruned on write.</b> <paramref name="stillLive"/> is whatever the caller currently
    /// knows this exact subject can produce — <see cref="SheetFindings"/>'s own routed list for
    /// the row <paramref name="issue"/> came off, <em>before</em> any dismissed-filtering — and
    /// any previously-dismissed key naming the same SubjectKind, SubjectId and OwnerId but absent
    /// from it is dropped rather than carried forward forever. A key surviving this needs its
    /// full routed list, not the already-filtered one a page draws: a still-valid earlier
    /// dismissal is invisible in the filtered list precisely because it is still dismissed, and
    /// pruning against that view alone would erase it the moment a sibling warning was waved
    /// off.</para>
    /// </summary>
    public async Task DismissAsync(
        string characterId, ValidationIssue issue, IReadOnlyList<ValidationIssue> stillLive)
    {
        if (issue.Severity != ValidationSeverity.Warning) return;

        await LoadAsync(characterId);

        var set = new HashSet<Key>(_cache.GetValueOrDefault(characterId) ?? []);
        PruneToSubject(set, issue, stillLive);
        set.Add(KeyFor(issue));

        try
        {
            await _js.InvokeVoidAsync("ppStore.save", StorageKey(characterId), JsonSerializer.Serialize(set));
            _cache[characterId] = set; // only once the write actually lands
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* storage refused it; nothing is dismissed */ }
    }

    /// <summary>Removes every dismissal for this character. What "Show them" does.</summary>
    public async Task ClearAsync(string characterId)
    {
        try
        {
            await _js.InvokeVoidAsync("ppStore.save", StorageKey(characterId), JsonSerializer.Serialize(new HashSet<Key>()));
            _cache[characterId] = [];
        }
        catch (Exception e) when (IsStorageFailure(e)) { /* nothing to undo if it never took */ }
    }

    /// <summary>
    /// Drops any key already in <paramref name="set"/> that names the same subject as
    /// <paramref name="issue"/> — same SubjectKind, SubjectId and OwnerId — but is not among
    /// <paramref name="stillLive"/>'s own keys. That subject's finding has changed shape since
    /// the dismissal was recorded (a different code fires now, or none at all), and the old key
    /// would otherwise sit in storage forever, matching nothing.
    /// </summary>
    private static void PruneToSubject(
        HashSet<Key> set, ValidationIssue issue, IReadOnlyList<ValidationIssue> stillLive)
    {
        var liveKeys = stillLive.Select(KeyFor).ToHashSet();
        var subjectKind = issue.SubjectKind.ToString();
        var subjectId = issue.SubjectId ?? "";
        var ownerId = issue.OwnerId ?? "";

        set.RemoveWhere(k =>
            k.SubjectKind == subjectKind && k.SubjectId == subjectId && k.OwnerId == ownerId
            && !liveKeys.Contains(k));
    }

    /// <summary>
    /// Everything that can go wrong between here and the browser's storage. Every one means the
    /// same thing: there is nothing dismissed for this character — carry on. Mirrors
    /// <see cref="SavedCharacters"/>'s own list rather than sharing it, so the two can drift only
    /// on purpose.
    /// </summary>
    private static bool IsStorageFailure(Exception e) =>
        e is JsonException
          or JSException
          or InvalidOperationException
          or ObjectDisposedException
          or TaskCanceledException
          or ArgumentException
          or OverflowException
          or NotSupportedException;
}
