using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What one row of the palette will do if it is chosen.</summary>
public enum CommandKind
{
    /// <summary>Go to one of the six creation steps.</summary>
    Step,

    /// <summary>Open a Power in the Powers editor, wherever the reader is now.</summary>
    Power,
}

/// <summary>
/// One thing the palette can do: a label, the words it can be found by, and enough to do it.
/// </summary>
/// <param name="Kind">Which of the two things this is.</param>
/// <param name="Target">A step's address, or a Power's id.</param>
/// <param name="Label">What the row reads.</param>
/// <param name="Detail">The quiet second line — a step's position, a Power's stat line.</param>
/// <param name="Keywords">Words it can be found by and does not print, as on a list row.</param>
public sealed record Command(
    CommandKind Kind,
    string Target,
    string Label,
    string? Detail,
    IReadOnlyList<string> Keywords);

/// <summary>
/// The command palette's state and the list of things it can do.
///
/// <para><b>The component is presentation and this is where the decisions are.</b> What the
/// palette offers, in what order, and how many — those are answerable without a browser, and
/// keeping them here is what makes them testable without rendering anything.</para>
///
/// <para><b>The six steps live here rather than in the step list component, because two lists
/// of steps would drift.</b> A palette that offers a step the band above it does not have —
/// or misses one it does — is worse than no palette, and nothing about the two being in
/// different files would have caught it. The band now reads this.</para>
///
/// <para><b>Nothing here computes a Hero Point or decides a rank.</b> A palette is a way to
/// reach a control, never a second place where a character is changed: choosing a Power opens
/// the same editor the Powers list opens, so the ranks and variants are chosen once, in the
/// one component that knows how.</para>
/// </summary>
public sealed class Commands
{
    private readonly CharacterSession _session;

    public Commands(CharacterSession session) => _session = session;

    /// <summary>
    /// The six creation steps, in the order the terminal wizard runs them. The band at the top
    /// of every page draws this, and so does the palette.
    /// </summary>
    public static readonly IReadOnlyList<Command> Steps =
    [
        new(CommandKind.Step, "", "Tier", "Step 1", ["start", "budget", "package"]),
        new(CommandKind.Step, "characteristics", "Characteristics", "Step 2",
            ["abilities", "talents", "powers", "perks", "flaws"]),
        new(CommandKind.Step, "gear", "Gear", "Step 3", ["equipment", "items"]),
        new(CommandKind.Step, "derived", "Derived stats", "Step 4",
            ["edge", "health", "resolve"]),
        new(CommandKind.Step, "finishing", "Finishing touches", "Step 5",
            ["name", "appearance", "motivation", "quote", "connections"]),
        new(CommandKind.Step, "review", "GM review", "Step 6",
            ["export", "print", "sheet", "validate"]),
    ];

    /// <summary>Whether the palette is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Raised when the palette opens or closes, or a Power is requested.</summary>
    public event Action? Changed;

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }

    /// <summary>
    /// The Power the reader asked for and has not been shown yet, or nothing.
    ///
    /// <para><b>This is a fact about a screen and deliberately not a field on the character.</b>
    /// The sheet is what gets exported and restored; a half-finished intention to look at a
    /// Power is neither.</para>
    /// </summary>
    public string? RequestedPowerId { get; private set; }

    /// <summary>
    /// Ask for a Power to be opened, and close the palette. The Powers editor is on another
    /// step, so this is a request rather than an action — see <see cref="TakeRequestedPower"/>.
    /// </summary>
    public void RequestPower(string powerId)
    {
        RequestedPowerId = powerId;
        IsOpen = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Take the requested Power, clearing it so it is acted on once.
    ///
    /// <para><b>Read-once is the point.</b> The step that holds the editor re-renders for every
    /// keystroke elsewhere on it, and a request that stayed set would reopen the editor over
    /// whatever the reader had moved on to — repeatedly, with nothing on screen explaining
    /// why. Peeking at the value without taking it is what <see cref="RequestedPowerId"/> is
    /// for, and the step above the editor uses it only to decide which section to show.</para>
    /// </summary>
    public string? TakeRequestedPower()
    {
        var id = RequestedPowerId;
        RequestedPowerId = null;
        return id;
    }

    /// <summary>
    /// What to offer for what has been typed: the steps that match, then the Powers.
    ///
    /// <para><b>Powers appear only once something has been typed.</b> There are 141 of them and
    /// six steps; offering all of them to an empty box would bury the steps under a catalogue
    /// nobody opened the palette to browse. An empty box is "where do I want to go", and the
    /// answer to that is six rows long.</para>
    ///
    /// <para>Matching is <see cref="OptionFilter.Matches"/> — the same rule the five pickable
    /// lists use, so what a reader has learnt about finding things here holds there.</para>
    /// </summary>
    /// <param name="query">What the reader has typed.</param>
    /// <param name="limit">The most Powers to offer. The steps are never truncated.</param>
    public IReadOnlyList<Command> Matching(string query, int limit)
    {
        var found = new List<Command>();

        foreach (var step in Steps)
        {
            if (OptionFilter.Matches(query, [step.Label, step.Detail, .. step.Keywords]))
                found.Add(step);
        }

        if (query.Length == 0) return found;

        foreach (var power in _session.Rules.Powers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (found.Count(c => c.Kind == CommandKind.Power) >= limit) break;

            // The stat line is shown and the tags are not, exactly as on a Powers row — a
            // reader who found a Power by its tag in the list can find it by its tag here.
            var line = PowerFormatter.StatLine(power);
            if (!OptionFilter.Matches(query, [power.Name, line, .. power.Tags])) continue;

            found.Add(new Command(CommandKind.Power, power.Id, power.Name, line, power.Tags));
        }

        return found;
    }
}
