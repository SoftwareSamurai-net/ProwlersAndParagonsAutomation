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

    /// <summary>One recording by the id in the address, or null.</summary>
    public Transcript? Find(string? id) =>
        Conversations.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
