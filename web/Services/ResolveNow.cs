using System.Text.Json;
using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// How much Resolve a Hero holds right now, during play, remembered in this browser for a day so
/// that an accidental refresh does not reset it.
///
/// <para><b>The owner's ruling of 2026-10-28, and the reason this is so small.</b> Resolve is
/// reset at the start of every issue (Ch.5 p.83), so there is nothing worth remembering past a
/// session: every open of a sheet seeds the count from the engine's figure, and storage is only
/// the safety net under a refresh or a lost tab. A stored count is honoured for
/// <see cref="Lifetime"/> after it was written and then ignored. No issue is tracked, nothing goes
/// to the account, and nothing goes on <see cref="ProwlersAndParagonsAutomation.Engine.CharacterSheet"/>
/// — whose strict reader refuses a member it does not know, so a play-state field there would
/// make every older build refuse every sheet carrying it.</para>
///
/// <para><b>No arithmetic about what Resolve a character should have lives here.</b> The opening
/// figure is the engine's, and the caller passes it in; this stores the one integer a player moved
/// it to, or nothing.</para>
///
/// <para><b>Writes are one at a time, latest value coalesced</b> — the shape
/// <see cref="Autosave"/> took after item 31 lost an edit to its own predecessor. Each write
/// carries the whole count rather than a delta, so the only way to lose a click is for an older
/// write to land after a newer one, and with one write open at a time that cannot happen.</para>
///
/// <para><b>A browser that refuses storage still counts</b>: the component holds the number in
/// memory, and a refused write only means a refresh goes back to the engine's figure.</para>
/// </summary>
public sealed class ResolveNow
{
    /// <summary>How long a stored count outlives the write that stored it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly IJSRuntime _js;
    private readonly TimeProvider _clock;

    public ResolveNow(IJSRuntime js, TimeProvider clock)
    {
        _js = js;
        _clock = clock;
    }

    public static string StorageKey(string characterKey) => $"pp.resolve.now.v1.{characterKey}";

    /// <summary>What is stored: the count, and when it was written.</summary>
    private sealed record Stored(int Now, DateTimeOffset At);

    /// <summary>
    /// Writes waiting behind the open one, latest per character. A null value is a clear.
    /// Keyed per character so a switch mid-write cannot drop the previous character's last count.
    /// </summary>
    private readonly Dictionary<string, Stored?> _pending = new(StringComparer.Ordinal);

    private bool _writing;

    /// <summary>
    /// The count stored for this character, or null when there is none worth honouring: never
    /// written, older than <see cref="Lifetime"/>, stamped in the future, negative, unreadable, or
    /// in a browser that refuses storage. Null means "start from the engine's figure".
    /// </summary>
    public async Task<int?> LoadAsync(string characterKey)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("ppStore.load", StorageKey(characterKey));
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var stored = JsonSerializer.Deserialize<Stored>(raw);
            if (stored is null || stored.Now < 0) return null;

            // A stamp from the future is a clock that moved, and trusting it would keep a count
            // alive for as long as the clock is wrong. Either way round, the engine's figure is
            // the honest fallback.
            var age = _clock.GetUtcNow() - stored.At;
            return age >= TimeSpan.Zero && age < Lifetime ? stored.Now : null;
        }
        catch (Exception e) when (IsStorageFailure(e)) { return null; }
    }

    /// <summary>Remembers <paramref name="now"/> for this character, stamped with the current time.</summary>
    public Task SaveAsync(string characterKey, int now) =>
        Enqueue(characterKey, new Stored(Math.Max(0, now), _clock.GetUtcNow()));

    /// <summary>Forgets this character's count, so the next open starts from the engine's figure.</summary>
    public Task ClearAsync(string characterKey) => Enqueue(characterKey, null);

    private async Task Enqueue(string characterKey, Stored? value)
    {
        _pending[characterKey] = value;
        if (_writing) return;

        _writing = true;
        try
        {
            while (_pending.Count > 0)
            {
                var (key, next) = _pending.First();
                _pending.Remove(key);

                try
                {
                    if (next is null)
                        await _js.InvokeVoidAsync("ppStore.clear", StorageKey(key));
                    else
                        await _js.InvokeVoidAsync("ppStore.save", StorageKey(key), JsonSerializer.Serialize(next));
                }
                catch (Exception e) when (IsStorageFailure(e)) { /* the count is still held on screen */ }
            }
        }
        finally { _writing = false; }
    }

    /// <summary>The same list <see cref="DismissedFindings"/> swallows, kept apart so the two can drift only on purpose.</summary>
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
