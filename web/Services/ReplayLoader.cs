namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Fetches the recorded conversations the first time somebody actually opens the replay, and
/// remembers the answer for the rest of the visit.
///
/// <para><b>On demand, not at startup.</b> The recordings used to be fetched before the first
/// render alongside the rules — four files every visitor paid for, almost none of whom could
/// ever see the pages that play them back, since those pages are behind an account. Now the
/// server refuses <c>api/transcripts</c> to anybody not signed in, which is what makes fetching
/// them worth deferring: a visitor who never opens a recording never asks for one.</para>
///
/// <para><b>Scoped, which in Blazor WebAssembly is a singleton for the life of the app</b> —
/// there is one DI scope for the whole session, so the first fetch really is the only fetch:
/// opening a second recording after the first does not ask again.</para>
/// </summary>
public sealed class ReplayLoader
{
    private readonly HttpClient _http;

    /// <summary>
    /// The one fetch in flight, or already finished. A field rather than a re-check on every
    /// call, so two components opening the replay at once — the list page rendering its cards
    /// while a link straight to one conversation is also loading — share the same request
    /// instead of racing two of them.
    /// </summary>
    private Task<ReplayLibrary>? _loading;

    public ReplayLoader(HttpClient http) => _http = http;

    /// <summary>The library, fetched once and cached for every caller after the first.</summary>
    public Task<ReplayLibrary> LoadAsync() => _loading ??= ReplayLibrary.LoadAsync(_http);
}
