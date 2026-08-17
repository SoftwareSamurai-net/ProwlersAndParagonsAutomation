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
/// <c>WebPresentationTests.TheBrowserBuildsItsReplayLibraryThroughTheGuardedLoader</c> is the
/// other half: that <c>Program.cs</c> actually goes through it.</para>
/// </summary>
public sealed class ReplayLoadingTests
{
    private static string TranscriptsDirectory =>
        Path.Combine(RepoRoot(), "data", "transcripts");

    private static Task<string> Read(string name) =>
        Task.FromResult(File.ReadAllText(Path.Combine(TranscriptsDirectory, name)));

    /// <summary>
    /// The ordinary case, asserted first so the failures below cannot be passing for the
    /// trivial reason that nothing ever loads.
    /// </summary>
    [Fact]
    public async Task WhenEveryRecordingArrivesTheLibraryHoldsThemAll()
    {
        var library = await ReplayLibrary.LoadAsync(Read);

        Assert.Null(library.Problem);
        Assert.Equal(TranscriptLibrary.FileNames.Count, library.Conversations.Count);
    }

    /// <summary>
    /// <b>And it asks for them where the build actually puts them.</b>
    ///
    /// <para>Nothing pinned the URL. Misspelling it — <c>data/transcript/</c> — is one
    /// character, and it takes the whole replay out of the deployed site while every test
    /// passes, because every test supplies its own fetch. The app goes through the
    /// <see cref="HttpClient"/> overload, so this drives that one and reads back what was
    /// requested.</para>
    /// </summary>
    [Fact]
    public async Task TheLibraryAsksForEachRecordingWhereTheBuildPutsIt()
    {
        var asked = new List<string>();

        using var handler = new Recorder(asked);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };

        var library = await ReplayLibrary.LoadAsync(http);

        // It loaded, which is what makes the paths below meaningful rather than a record of
        // where a failing request went.
        Assert.Null(library.Problem);
        Assert.Equal(TranscriptLibrary.FileNames.Count, library.Conversations.Count);

        Assert.Equal(
            TranscriptLibrary.FileNames.Select(n => $"/{ReplayLibrary.ServedFrom}/{n}").ToList(),
            asked);
    }

    /// <summary>Answers each request from the repository, and records what was asked for.</summary>
    private sealed class Recorder : HttpMessageHandler
    {
        private readonly List<string> _asked;

        public Recorder(List<string> asked) => _asked = asked;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            _asked.Add(path);

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    File.ReadAllText(Path.Combine(TranscriptsDirectory, path.Split('/')[^1])))
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
            _ => Task.FromException<string>(new HttpRequestException(reason)));

        Assert.Empty(library.Conversations);

        // The reason, not merely "something went wrong": both replay pages print it, and an
        // empty list with no explanation reads as deliberate to somebody following a link
        // that is perfectly good.
        Assert.Contains(reason, library.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a recording that arrived but this build cannot read is the same thing. The reader
    /// is deliberately strict — a field a character no longer has fails at load rather than
    /// quietly emptying a section — so a rules change can put the app in exactly this state.
    /// </summary>
    [Fact]
    public async Task ARecordingThisBuildCannotReadIsTheSameAsNoRecordings()
    {
        var library = await ReplayLibrary.LoadAsync(_ => Task.FromResult("{ not a transcript"));

        Assert.Empty(library.Conversations);
        Assert.False(string.IsNullOrWhiteSpace(library.Problem));
    }

    /// <summary>
    /// <b>One recording short is no recordings, not most of them.</b> A library holding three
    /// of four looks deliberate on the list page and answers the fourth address with "no such
    /// recording" — a confident lie about a link that is fine. Partial is the state this must
    /// never be in, and it is the one a loop that swallowed each failure separately would
    /// produce.
    /// </summary>
    [Fact]
    public async Task OneRecordingMissingLeavesNoneRatherThanMost()
    {
        var missing = TranscriptLibrary.FileNames[^1];

        var library = await ReplayLibrary.LoadAsync(name => name == missing
            ? Task.FromException<string>(new HttpRequestException("404"))
            : Read(name));

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
