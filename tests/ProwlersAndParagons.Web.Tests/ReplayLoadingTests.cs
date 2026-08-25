using System.Net;
using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>A failed transcript fetch must not stop the app.</b>
///
/// <para>Missing rules are a broken deployment — every answer the app gives would be wrong,
/// so it refuses to start. Missing recordings are a missing demonstration, and the app is a
/// character generator: somebody who came to build a character must still be able to.
/// <c>CLAUDE.md</c> has said so since the replay shipped and <b>nothing tested it</b>, because
/// the guarantee was a <c>try</c>/<c>catch</c> in top-level statements that no test can reach.
/// Deleting it was green, and one 404 then took the whole app to a blank page, which is the
/// worst outcome available to a static site.</para>
///
/// <para>So the loading lives in <see cref="ReplayLibrary.LoadAsync"/>, which takes the fetch
/// as an argument and can therefore be handed one that fails.
/// <c>WebPresentationTests.TheBrowserDoesNotFetchTheReplayLibraryAtStartup</c> is the other
/// half: that nothing calls this before somebody actually opens the replay.</para>
/// </summary>
public sealed class ReplayLoadingTests
{
    private static string TranscriptsDirectory =>
        Path.Combine(RepoRoot(), "data", "transcripts");

    /// <summary>
    /// The whole bundle, as the gated route answers it: every recording, keyed by file name, in
    /// one JSON object — read from the real files so this exercises the shipped recordings
    /// rather than an invented one.
    /// </summary>
    private static string Bundle()
    {
        var files = TranscriptLibrary.FileNames.ToDictionary(
            name => name,
            name => JsonDocument.Parse(
                File.ReadAllText(Path.Combine(TranscriptsDirectory, name))).RootElement,
            StringComparer.Ordinal);

        return JsonSerializer.Serialize(files);
    }

    /// <summary>
    /// The ordinary case, asserted first so the failures below cannot be passing for the
    /// trivial reason that nothing ever loads.
    /// </summary>
    [Fact]
    public async Task WhenEveryRecordingArrivesTheLibraryHoldsThemAll()
    {
        var library = await ReplayLibrary.LoadAsync(() => Task.FromResult(Bundle()));

        Assert.Null(library.Problem);
        Assert.Equal(TranscriptLibrary.FileNames.Count, library.Conversations.Count);
    }

    /// <summary>
    /// <b>And it asks the one gated address, not a folder of files.</b>
    ///
    /// <para>Nothing pinned the URL. Misspelling it — <c>api/transcript</c> — is one character,
    /// and it takes the whole replay out of the deployed site while every test passes, because
    /// every test supplies its own fetch. The app goes through the <see cref="HttpClient"/>
    /// overload, so this drives that one and reads back what was requested.</para>
    /// </summary>
    [Fact]
    public async Task TheLibraryAsksTheGatedAddressForEveryRecordingAtOnce()
    {
        var asked = new List<string>();

        using var handler = new Recorder(asked, Bundle());
        using var http = new HttpClient(handler);

        // Assigned rather than set in an initialiser: an initialiser that threw would leave the
        // client undisposed, because `using` has not taken hold of it yet.
        http.BaseAddress = new Uri("https://example.invalid/");

        var library = await ReplayLibrary.LoadAsync(http);

        // It loaded, which is what makes the address below meaningful rather than a record of
        // where a failing request went.
        Assert.Null(library.Problem);
        Assert.Equal(TranscriptLibrary.FileNames.Count, library.Conversations.Count);

        Assert.Equal([$"/{ReplayLibrary.ServedFrom}"], asked);
    }

    /// <summary>Answers every request with a fixed body, and records what was asked for.</summary>
    private sealed class Recorder : HttpMessageHandler
    {
        private readonly List<string> _asked;
        private readonly string _body;

        public Recorder(List<string> asked, string body)
        {
            _asked = asked;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _asked.Add(request.RequestUri!.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
        }
    }

    /// <summary>
    /// A fetch that throws — a 404, a network that went away, a deploy that shipped the app
    /// without the recordings.
    /// </summary>
    [Fact]
    public async Task AFetchThatFailsLeavesAnEmptyLibraryCarryingTheReason()
    {
        const string reason = "the transcripts answered 404";

        var library = await ReplayLibrary.LoadAsync(
            () => Task.FromException<string>(new HttpRequestException(reason)));

        Assert.Empty(library.Conversations);

        // The reason, not merely "something went wrong": both replay pages print it, and an
        // empty list with no explanation reads as deliberate to somebody following a link
        // that is perfectly good.
        Assert.Contains(reason, library.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a bundle that arrived but this build cannot read is the same thing — malformed JSON,
    /// or a recording whose reader is deliberately strict: a field a character no longer has
    /// fails at load rather than quietly emptying a section, so a rules change can put the app
    /// in exactly this state.
    /// </summary>
    [Fact]
    public async Task ABundleThisBuildCannotReadIsTheSameAsNoRecordings()
    {
        var library = await ReplayLibrary.LoadAsync(() => Task.FromResult("{ not json"));

        Assert.Empty(library.Conversations);
        Assert.False(string.IsNullOrWhiteSpace(library.Problem));
    }

    /// <summary>
    /// <b>One recording short is no recordings, not most of them.</b> A library holding three
    /// of four looks deliberate on the list page and answers the fourth address with "no such
    /// recording" — a confident lie about a link that is fine. Partial is the state this must
    /// never be in, which is what the strict reader's own refusal to see a name it was not told
    /// about turns a short bundle into.
    /// </summary>
    [Fact]
    public async Task OneRecordingMissingLeavesNoneRatherThanMost()
    {
        var missing = TranscriptLibrary.FileNames[^1];

        var files = TranscriptLibrary.FileNames
            .Where(name => name != missing)
            .ToDictionary(
                name => name,
                name => JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(TranscriptsDirectory, name))).RootElement,
                StringComparer.Ordinal);

        var library = await ReplayLibrary.LoadAsync(() => Task.FromResult(JsonSerializer.Serialize(files)));

        Assert.Empty(library.Conversations);
        Assert.False(string.IsNullOrWhiteSpace(library.Problem));
    }

    /// <summary>Same walk as <see cref="RenderContext"/>: up to the solution file.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
