using System.Text.Json;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The recorded conversations the app can replay, fetched on demand — see <see cref="ReplayLoader"/>
/// for when.
///
/// <para>It holds no logic of its own beyond finding one by id. Everything a replay puts on
/// the screen — what a draft cost, whether it broke a rule, the printed sheet — is asked of
/// the engine at the moment it is shown, from the character stored in the transcript. This
/// class deliberately has no method that returns a number.</para>
///
/// <para><b>It can legitimately be empty.</b> The recordings are a demonstration and the app
/// is a character generator: a fetch that fails, or a transcript this build can no longer
/// read, must not stop somebody building a character. So a failed load answers an empty
/// library rather than throwing, and the replay pages say so on the page instead of showing
/// an empty list that looks deliberate.</para>
/// </summary>
public sealed class ReplayLibrary
{
    private ReplayLibrary(IReadOnlyList<Transcript> conversations, string? problem = null)
    {
        Conversations = conversations;
        Problem = problem;
    }

    /// <summary>Every recording, in the order they are meant to be met.</summary>
    public IReadOnlyList<Transcript> Conversations { get; }

    /// <summary>
    /// Why there are none, when there are none. Null when the library loaded, whatever it
    /// loaded.
    /// </summary>
    public string? Problem { get; }

    /// <summary>
    /// Where the recordings are served from. Here rather than in <c>Program.cs</c> so the
    /// address and the guard cannot be separated.
    ///
    /// <para><b>The gated route, not a folder.</b> The recordings used to be ordinary files
    /// under <c>wwwroot/data/transcripts</c>, fetched by name; the server bundles them now, the
    /// way <c>data/rulebook/</c> is bundled into the worker, so this is one address answering
    /// every recording at once rather than a folder of four.</para>
    /// </summary>
    public const string ServedFrom = "api/transcripts";

    /// <summary>
    /// Fetches every recording and reads it, or answers with an empty library carrying the
    /// reason it could not.
    ///
    /// <para><b>This takes the client rather than a fetch, and that is the whole point.</b>
    /// With a <c>Func&lt;Task&lt;string&gt;&gt;</c> parameter, a caller could fetch the bundle
    /// itself and hand this one a delegate that only reads the result — the guard still
    /// called, the throwing fetch back outside it, and every test green. An adversarial pass
    /// did exactly that against the per-file version of this method. The overload below still
    /// exists for the tests, which need a fetch that fails; nothing else may use it.</para>
    ///
    /// <para><b>And it is a method rather than a block wherever it is called from, because
    /// that is where nothing could reach it.</b> The guarantee it makes — a failed fetch means
    /// no recordings and never no app — was once written as a <c>try</c> around a loop in
    /// <c>Program.cs</c>'s top-level statements, and deleting the <c>try</c> left the whole
    /// suite green while one 404 took the character generator to a blank page.</para>
    ///
    /// <para><b>The rules are deliberately not loaded this way.</b> Missing rules are a broken
    /// deployment and an app that answers every question wrongly; missing recordings are a
    /// missing demonstration. Only the second is worth starting without — and now nobody
    /// starts without them, because nobody fetches them until they ask to see one. See
    /// <see cref="ReplayLoader"/>.</para>
    /// </summary>
    public static Task<ReplayLibrary> LoadAsync(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);

        return LoadAsync(async () =>
        {
            using var response = await http.GetAsync(ServedFrom).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"The recordings answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        });
    }

    /// <summary>
    /// The same, with the fetch injected. <b>For tests only</b> — see the remarks above on why
    /// the app must go through the <see cref="HttpClient"/> overload.
    /// </summary>
    /// <param name="fetch">
    /// Answers the whole bundle — every recording, keyed by the file name it was baked under —
    /// as one JSON object, and may throw.
    /// </param>
    internal static async Task<ReplayLibrary> LoadAsync(Func<Task<string>> fetch)
    {
        ArgumentNullException.ThrowIfNull(fetch);

#pragma warning disable CA1031 // any failure here means "no recordings", never "no app"
        try
        {
            var bundle = await fetch().ConfigureAwait(false);

            using var document = JsonDocument.Parse(bundle);

            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in document.RootElement.EnumerateObject())
                files[file.Name] = file.Value.GetRawText();

            return new ReplayLibrary(TranscriptLibrary.ReadAll(files));
        }
        catch (Exception e)
        {
            // The message travels with the library rather than being logged and lost: the
            // replay pages print it, because an empty list with no explanation looks
            // deliberate and sends somebody following a good link away thinking they mistyped.
            return new ReplayLibrary([], e.Message);
        }
#pragma warning restore CA1031
    }

    /// <summary>One recording by the id in the address, or null.</summary>
    public Transcript? Find(string? id) =>
        Conversations.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
