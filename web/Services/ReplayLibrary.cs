using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The recorded conversations the app can replay, fetched at startup.
///
/// <para>It holds no logic of its own beyond finding one by id. Everything a replay puts on
/// the screen — what a draft cost, whether it broke a rule, the printed sheet — is asked of
/// the engine at the moment it is shown, from the character stored in the transcript. This
/// class deliberately has no method that returns a number.</para>
///
/// <para><b>It can legitimately be empty.</b> The recordings are a demonstration and the app
/// is a character generator: a fetch that fails, or a transcript this build can no longer
/// read, must not stop somebody building a character. So <c>Program.cs</c> registers an empty
/// library rather than refusing to start, and the replay pages say so on the page instead of
/// showing an empty list that looks deliberate.</para>
/// </summary>
public sealed class ReplayLibrary
{
    public ReplayLibrary(IReadOnlyList<Transcript> conversations, string? problem = null)
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
    /// Fetches every recording and reads it, or answers with an empty library carrying the
    /// reason it could not.
    ///
    /// <para><b>This is a method rather than a block in <c>Program.cs</c> because that is
    /// where nothing could reach it.</b> The guarantee it makes — a failed fetch means no
    /// recordings and never no app — was written as a <c>try</c> around a loop in top-level
    /// statements, and deleting the <c>try</c> left the whole suite green while one 404 took
    /// the character generator to a blank page. A browser cannot glob a directory it has no
    /// filesystem for, so <see cref="TranscriptLibrary.FileNames"/> is the contract, exactly
    /// as <c>RulesRepository.DataFileNames</c> is for the rules.</para>
    ///
    /// <para><b>The rules are deliberately not loaded this way.</b> Missing rules are a broken
    /// deployment and an app that answers every question wrongly; missing recordings are a
    /// missing demonstration. Only the second is worth starting without.</para>
    /// </summary>
    /// <param name="fetch">Asked for one file by name, and may throw.</param>
    public static async Task<ReplayLibrary> LoadAsync(Func<string, Task<string>> fetch)
    {
        ArgumentNullException.ThrowIfNull(fetch);

#pragma warning disable CA1031 // any failure here means "no recordings", never "no app"
        try
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var name in TranscriptLibrary.FileNames)
                files[name] = await fetch(name).ConfigureAwait(false);

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
