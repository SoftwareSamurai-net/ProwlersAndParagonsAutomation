using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What one row of the palette will do if it is chosen.</summary>
public enum CommandKind
{
    /// <summary>Go to one of the six creation steps.</summary>
    Step,

    /// <summary>Open a Power in the Powers editor, wherever the reader is now.</summary>
    Power,

    /// <summary>
    /// Read a passage of the rulebook, at <c>/rules</c>, with what was typed carried over.
    ///
    /// <para>Only ever offered to somebody signed in — see <see cref="Commands.BookIsOffered"/>.
    /// </para>
    /// </summary>
    Passage,
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
///
/// <para><b>The rulebook is the second corpus, and it grows this file rather than the script.</b>
/// <c>wwwroot/js/palette.js</c> says in as many words to resist growing it, and nothing here
/// makes it any bigger: it is still one listener, two focus calls and one question about the
/// keyboard, and a test holds it to that byte for byte. What the palette *offers* was always
/// decided here, so a second body of text to offer is an addition where the decisions already
/// live — see <see cref="AskTheBookAsync"/> for why a corpus behind an account gate is safe on
/// this side of the wire.</para>
/// </summary>
public sealed class Commands
{
    private readonly CharacterSession _session;
    private readonly RulebookReader _book;
    private readonly IIdentitySource _who;

    public Commands(CharacterSession session, RulebookReader book, IIdentitySource who)
    {
        _session = session;
        _book = book;
        _who = who;
    }

    /// <summary>
    /// The six creation steps, in the order the terminal wizard runs them. The band at the top
    /// of every page draws this, and so does the palette.
    /// </summary>
    public static readonly IReadOnlyList<Command> Steps =
    [
        new(CommandKind.Step, "build", "Tier", "Step 1", ["start", "budget", "package"]),
        new(CommandKind.Step, "build/characteristics", "Characteristics", "Step 2",
            ["abilities", "talents", "powers", "perks", "flaws"]),
        new(CommandKind.Step, "build/gear", "Gear", "Step 3", ["equipment", "items"]),
        new(CommandKind.Step, "build/derived", "Derived stats", "Step 4",
            ["edge", "health", "resolve"]),
        new(CommandKind.Step, "build/finishing", "Finishing touches", "Step 5",
            ["name", "appearance", "motivation", "quote", "connections"]),
        new(CommandKind.Step, "build/review", "GM review", "Step 6",
            ["export", "print", "sheet", "validate"]),
    ];

    /// <summary>
    /// Where the builder starts, for anything that needs to send somebody to it.
    ///
    /// <para>Read off the list rather than written out again: the six steps' addresses moved
    /// under a prefix once, and every second copy of the first one is a link that will not
    /// move with them next time.</para>
    /// </summary>
    public static string FirstStep => Steps[0].Target;

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
    /// The section of the characteristics step the reader asked for and has not been shown yet.
    ///
    /// <para><b>The same shape as <see cref="RequestedPowerId"/>, for the same reason</b>: the
    /// sections live on another step, so naming one is a request rather than an action. It is a
    /// fact about a screen and deliberately not a field on the character.</para>
    ///
    /// <para><b>Taken rather than peeked, which is the opposite of the Power above.</b> A Power is
    /// peeked by the step and taken by the editor, because two components need it. A section has
    /// exactly one consumer — the step's own tab — so it is cleared the moment that reads it; left
    /// set, it would drag the reader back to the same tab every time anything else on the step
    /// re-rendered.</para>
    /// </summary>
    public string? RequestedSection { get; private set; }

    /// <summary>
    /// Ask for a section of the characteristics step to be shown. Used by the GM review step's
    /// findings, which name the step a broken rule belongs to.
    /// </summary>
    public void RequestSection(string section)
    {
        RequestedSection = section;
        Changed?.Invoke();
    }

    /// <summary>
    /// The search the reader asked for from the palette and has not been shown yet, or nothing.
    ///
    /// <para><b>The same shape as the two above, and it is the reason the palette does not
    /// navigate to <c>/rules?q=…</c> instead.</b> A query string would have to be parsed back out
    /// of the address by hand — there is no query-string reader in this project and a routed
    /// parameter would be a second way into a page whose one entry point is its own form — for the
    /// one thing it buys, which is a link somebody could share. This is the idiom the palette
    /// already uses to hand a Power to an editor on another step, and <c>/rules</c> is another
    /// screen in exactly that sense.</para>
    ///
    /// <para>It is a fact about a screen and deliberately not a field on the character.</para>
    /// </summary>
    public string? RequestedSearch { get; private set; }

    /// <summary>
    /// Ask for a search to be run on the rules reference, and close the palette.
    ///
    /// <para>A request rather than an action, because the page that can read a passage is another
    /// page — the same reason choosing a Power is a request.</para>
    /// </summary>
    public void RequestSearch(string query)
    {
        RequestedSearch = query;
        IsOpen = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Take the requested search, clearing it so it is acted on once.
    ///
    /// <para><b>Taken rather than peeked, like the section above and unlike the Power.</b> One
    /// consumer, and a request left set would re-run the palette's query over whatever the reader
    /// had gone on to type into the box themselves.</para>
    /// </summary>
    public string? TakeRequestedSearch()
    {
        var query = RequestedSearch;
        RequestedSearch = null;
        return query;
    }

    /// <summary>Take the requested section, clearing it so it is acted on once.</summary>
    public string? TakeRequestedSection()
    {
        var section = RequestedSection;
        RequestedSection = null;
        return section;
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

    // ─────────────────────────────────────────────────────────────────────────────
    // The book, which is the second corpus and the only one behind an account gate.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The most passages to offer, and it is small on purpose.
    ///
    /// <para>The palette is a way to reach something, not a results page — <c>/rules</c> is the
    /// results page, and the row that gets chosen goes there. Five is the number of rows a reader
    /// can weigh without scrolling a list that already holds six steps and up to eight Powers
    /// above it, and it keeps the book a third of the box rather than the whole of it.</para>
    /// </summary>
    public const int BookLimit = 5;

    /// <summary>
    /// How much has to be typed before the book is asked, and the figure is the server's.
    ///
    /// <para><b>Three, because <c>terms()</c> in <c>worker/search.js</c> drops every word of two
    /// characters or fewer.</b> So a two-letter query is not a narrow search — it is a search the
    /// server cannot run at all, and it answers <c>found: 0</c> for a question it never asked.
    /// Sending it would spend a round trip to be told nothing, in the one place where a round trip
    /// happens on a keystroke.</para>
    /// </summary>
    public const int BookThreshold = 3;

    /// <summary>
    /// How long the box has to be still before the book is asked.
    ///
    /// <para><b>220ms, and the reason it is not zero is that this is the only thing in the app
    /// that goes over the network per keystroke.</b> Everything else here is in memory — the six
    /// steps and 141 Powers are filtered in the browser — so "knockback" typed at an ordinary
    /// speed would be nine requests for eight answers nobody reads. Two figures bound it: under
    /// about 150ms the pause saves nothing on a fast typist, and over about 300ms the box feels
    /// like it has stopped responding. It is settable so a test can take the pause out of a
    /// question that is not about the pause; nothing in the app ever assigns it.</para>
    /// </summary>
    public TimeSpan BookPause { get; set; } = TimeSpan.FromMilliseconds(220);

    /// <summary>
    /// How the pause is actually taken. <see cref="Task.Delay(TimeSpan)"/> in the app; nothing
    /// here ever assigns it.
    ///
    /// <para><b>It exists so that "a burst of keystrokes is one request" can be checked without a
    /// wall clock.</b> Against a real delay that test is asking whether two calls could be
    /// dispatched inside a fifth of a second, which is a question about how busy the machine is —
    /// and it flaked once for exactly that reason. Handed a gate the test holds open, the same
    /// drive asks the thing the guard is actually for: that a keystroke arriving while the pause is
    /// running collapses the one before it.</para>
    /// </summary>
    public Func<TimeSpan, Task> Pausing { get; set; } = pause => Task.Delay(pause);

    /// <summary>
    /// Whether this reader is offered the book at all.
    ///
    /// <para><b>False until somebody has been asked, and false for everybody anonymous.</b> It
    /// gates the request, so a signed-out reader makes none — and it gates the box's own label, so
    /// the palette does not promise a book it will not show. Refreshed by
    /// <see cref="NoteWhoIsAskingAsync"/>.</para>
    /// </summary>
    public bool BookIsOffered { get; private set; }

    /// <summary>
    /// Whether an answer about the book is outstanding.
    ///
    /// <para>Read by the palette so it does not print "nothing matches" over a question it is
    /// still waiting on. Without it, typing a word only the book knows shows that sentence for a
    /// fifth of a second and then replaces it with five rows, which reads as the app changing its
    /// mind rather than as an answer arriving.</para>
    /// </summary>
    public bool BookIsBeingAsked { get; private set; }

    /// <summary>
    /// Whether the last thing the book was asked came back unanswered — refused, failed, or never
    /// reached.
    ///
    /// <para><b>Read by the palette so it does not print "nothing here matches what you typed"
    /// over a question the server never answered.</b> That sentence is a claim about the corpus,
    /// and a session that expired server-side, a 500 or a laptop off the network are not the
    /// corpus saying anything. The state it leaves is deliberately quiet — no rows, and no
    /// sentence either — because there is nothing true and short to say: "the book could not be
    /// reached" over an open palette is an error report for a thing the reader did not ask for,
    /// and <c>/rules</c> is where a failed search belongs on screen.</para>
    ///
    /// <para>Cleared by the next answer that does land, and by every ask that never leaves —
    /// see <see cref="Settle"/>.</para>
    /// </summary>
    public bool BookCouldNotBeAsked { get; private set; }

    /// <summary>
    /// The passages the book last answered with, for the query on screen now.
    ///
    /// <para><b>"For the query on screen now" is enforced rather than hoped for.</b> The rows are
    /// dropped the moment the box changes — see <see cref="AskTheBookAsync"/> — so they are never
    /// the previous question's answer sitting under the current question's text for the pause plus
    /// a round trip. That mattered twice over: those rows are read as an answer, and each one
    /// carries the query it will hand to <c>/rules</c> if it is chosen, so a stale row pressed
    /// Enter on used to search for a word the reader had already typed over.</para>
    /// </summary>
    public IReadOnlyList<Command> Book { get; private set; } = [];

    /// <summary>
    /// Raised when the book's rows, or the offer of them, have changed.
    ///
    /// <para><b>Deliberately not <see cref="Changed"/>, and this is not tidiness.</b> The palette
    /// treats a <c>Changed</c> raised while it is open as "it just opened" and empties the box —
    /// which is right, because that is the only thing that used to raise one. An answer about the
    /// book arrives *because* of what is in the box, so ringing that bell would clear the query
    /// that asked the question, one keystroke after it was typed.</para>
    /// </summary>
    public event Action? BookAnswered;

    /// <summary>How many searches have been started. The newest one is the one that counts.</summary>
    private int _asked;

    /// <summary>The number of the newest search whose answer has been put on screen.</summary>
    private int _answered;

    /// <summary>
    /// The query the newest search is for, which is the only query <see cref="Book"/> may hold rows
    /// for. Trimmed, and empty before anything has been typed.
    /// </summary>
    private string _wanted = "";

    /// <summary>
    /// The account the book last refused, or null. Nobody is offered the book again on this key.
    ///
    /// <para><b>A 401 from the book means the account is gone even though this browser still
    /// thinks it is signed in</b> — a session expired on the server, or was signed out from
    /// another tab. <see cref="Accounts"/> answers who is here out of its own memory, so asking it
    /// again returns the same stale yes; without this the palette would offer the book, be refused,
    /// stop offering, and offer it again on the next open, for ever. There is no cheaper hook to
    /// pull: <see cref="IIdentitySource"/> is one method and carries no way to say "that answer has
    /// gone stale", and inventing one here would be this class deciding who is signed in.</para>
    /// </summary>
    private string? _refusedBy;

    /// <summary>Whose key <see cref="NoteWhoIsAskingAsync"/> last saw, so a refusal has somebody to
    /// be about. Read where there is no <c>await</c> to be had.</summary>
    private string? _asking;

    /// <summary>
    /// Ask who is here, and remember whether the book is theirs to search.
    ///
    /// <para><b>Asked rather than subscribed to.</b> <see cref="Accounts"/> raises an event when
    /// the answer changes, and a guarantee that depends on an event being raised is one somebody
    /// can remove by editing another file — the same reasoning <see cref="RulebookReader"/>
    /// records for its own cache. The palette asks on every open, which is where it matters:
    /// signing out is done from the account page, so the next open is the first thing that could
    /// show a signed-out reader an account's prose.</para>
    ///
    /// <para><b>The clearing below is defence and not the mechanism, and saying so is the point.</b>
    /// Opening the palette empties the box and clears the rows through the ordinary path, so on
    /// every route a reader can actually take this line has already been done for it — a mutation
    /// removing it survives, and recording it as covered would be recording coverage that is not
    /// there. It stays because it costs nothing and the fault it guards against is one this app
    /// has already shipped once, in <see cref="RulebookReader"/>'s cache: Blazor WebAssembly has
    /// one DI scope for the life of the app and signing out is pure SPA state with no reload, so
    /// anything holding the book's words holds them across a sign-out unless something drops
    /// them.</para>
    /// </summary>
    public async Task NoteWhoIsAskingAsync()
    {
        var who = await _who.CurrentAsync();

        _asking = who.Key;

        // A different account is a fresh chance: the refusal above is about one key, and somebody
        // else signing in on this browser has not been refused anything.
        if (_refusedBy is { } refused && !string.Equals(refused, who.Key, StringComparison.Ordinal))
            _refusedBy = null;

        var offer = who.IsSignedIn && _refusedBy is null;

        if (offer == BookIsOffered) return;

        BookIsOffered = offer;

        if (!BookIsOffered) StopAsking();

        BookAnswered?.Invoke();
    }

    /// <summary>
    /// Drop the book's rows and every question about it that is still in the air.
    ///
    /// <para><b>Bumping the counter is what makes the drop stick.</b> An ask that is sitting in its
    /// pause, or waiting on the wire, will come back — and it was started by somebody who was
    /// offered the book and is not any more. Taking the number past it makes both of its guards
    /// refuse it: it stops at <see cref="AskTheBookAsync"/>'s check after the pause, and
    /// <see cref="Settle"/> will not put it on screen if it gets that far. <see cref="_answered"/>
    /// moves with it so a dropped answer cannot leave "still being asked" showing for ever.</para>
    /// </summary>
    private void StopAsking()
    {
        _answered = ++_asked;
        Book = [];
        BookIsBeingAsked = false;
        BookCouldNotBeAsked = false;
    }

    /// <summary>
    /// Ask the book about what has been typed, and offer what it answers.
    ///
    /// <para><b>Why a second corpus behind an account gate is safe here.</b> The palette only ever
    /// offers what <see cref="RulebookReader"/> answers; the reader is bundled into the accounts
    /// server and the server refuses every address under <c>/api/rulebook/</c> on the prefix, so
    /// there is no arrangement of this class that can show the book's words to somebody who is not
    /// signed in. Nothing about the text is staged into the site's own files, nothing is cached
    /// across a sign-out, and for an anonymous reader no request is made at all — so the palette
    /// makes no claim about the book, quotes none of it, and does not even tell them it is
    /// there.</para>
    ///
    /// <para><b>The pause and the sequence number are two different guards and both are
    /// needed.</b> The pause collapses a burst of keystrokes into one request. The sequence number
    /// is for the requests that really do overlap — two words typed a second apart, the first
    /// answering last — where nothing about the pause helps and the older answer would otherwise
    /// land on top of the newer one's rows. That is the defect class this repository has shipped
    /// before, in the autosave, and it is silent when it happens: the rows look like an answer,
    /// they are just an answer to the previous question.</para>
    ///
    /// <para>An answer is appended below the steps and the Powers rather than inserted among them,
    /// so a row arriving cannot move the row the reader has Enter poised over.</para>
    /// </summary>
    /// <param name="query">What the reader has typed.</param>
    public async Task AskTheBookAsync(string query)
    {
        var wanted = (query ?? "").Trim();
        var mine = ++_asked;

        // **The rows are keyed to the query and dropped the moment it moves.** Left up they are
        // the previous question's answer under the current question's text, for the pause plus a
        // round trip — read as an answer, and worse than that: each row carries the query it hands
        // to /rules, so Enter on one searched for a word that had already been typed over.
        if (!string.Equals(wanted, _wanted, StringComparison.Ordinal))
        {
            _wanted = wanted;

            if (Book.Count > 0)
            {
                Book = [];
                BookAnswered?.Invoke();
            }
        }

        if (!BookIsOffered || wanted.Length < BookThreshold)
        {
            // Not "leave the rows alone": whatever the book last said was about a query that is
            // no longer on screen, and a stale row is worse than none. This takes a sequence
            // number of its own so an answer already in flight cannot put those rows back.
            Settle(mine, wanted, [], AskedTheBook.Answered);
            return;
        }

        BookIsBeingAsked = true;

        await Pausing(BookPause);

        // Somebody typed again while this was waiting. That keystroke's own call is the one that
        // will ask, so this one stops here — and leaves BookIsBeingAsked set, because a question
        // is still outstanding, just not this one.
        if (mine != _asked) return;

        // **Asked again after the pause, and that is the whole of the sign-out guard on this
        // path.** The check above the pause is a fifth of a second stale by now, and signing out
        // is pure SPA state: a reader who was offered the book when this keystroke landed can be
        // anonymous by the time the request would go, and the one thing that must never happen is
        // this app sending a signed-out reader's typing to the address that serves the
        // publisher's text.
        if (!BookIsOffered)
        {
            Settle(mine, wanted, [], AskedTheBook.Answered);
            return;
        }

        var answer = await _book.AskAboutAsync(wanted, limit: BookLimit);

        var rows = answer.Results is { } results
            ? results.Results.Take(BookLimit).Select(r => Passage(r, wanted)).ToList()
            : [];

        Settle(mine, wanted, rows, answer.How);
    }

    /// <summary>
    /// Put an answer on screen, unless a newer one is already there.
    ///
    /// <para>The whole of the race guard. A search that started earlier can finish later — a
    /// slower query, a retried connection, an ordinary jitter — and the number it was given when
    /// it started is what says so.</para>
    ///
    /// <para><b>"Nothing is outstanding" is a claim about the newest question, not about this
    /// one.</b> Landing an answer clears <see cref="BookIsBeingAsked"/> only when the answer is the
    /// newest question's — otherwise an older answer arriving under a newer question would say the
    /// palette is done waiting while it is not, and "nothing here matches what you typed" would
    /// print in the middle of a search that is still running.</para>
    /// </summary>
    /// <param name="mine">The number this search was given when it started.</param>
    /// <param name="wanted">The query it was for, which must still be the one on screen.</param>
    /// <param name="rows">What to show, which is empty for every answer that is not passages.</param>
    /// <param name="how">Whether the server answered at all — see <see cref="AskedTheBook"/>.</param>
    private void Settle(int mine, string wanted, List<Command> rows, AskedTheBook how)
    {
        if (mine <= _answered) return;

        _answered = mine;
        BookIsBeingAsked = mine != _asked;
        BookCouldNotBeAsked = how != AskedTheBook.Answered;

        // Refused means the account this browser thinks it is signed in as is not one the server
        // will answer for. Stop offering the book — the box's own label promises it, and promising
        // a rulebook to somebody the server will refuse is the wrong promise twice over.
        if (how == AskedTheBook.Refused)
        {
            _refusedBy = _asking;
            BookIsOffered = false;
        }

        // An answer for a query nobody is asking any more shows nothing, whatever it found. The
        // rows were already dropped when the box changed; this is what stops a slower answer for
        // the older query putting its own back.
        if (!string.Equals(wanted, _wanted, StringComparison.Ordinal)) rows = [];

        if (rows.Count == 0 && Book.Count == 0)
        {
            // Nothing was showing and nothing is showing. Still worth a redraw, because the
            // sentence about nothing matching is drawn off the two flags above and both just moved.
            BookAnswered?.Invoke();
            return;
        }

        Book = rows;
        BookAnswered?.Invoke();
    }

    /// <summary>
    /// One passage as a row: the book's own heading, and the page a paper copy prints it on.
    ///
    /// <para>The citation is <see cref="RulebookCitation.For"/> — the spelling <c>/rules</c> prints
    /// — rather than a second format written out here, so a reader who has learnt to read
    /// <c>Ch.4 p.62</c> on one screen reads the same thing on the other.</para>
    ///
    /// <para><b>The target is the reader's own query, not the passage.</b> Choosing a row runs
    /// that search on <c>/rules</c>, which is where a passage can actually be read — the palette
    /// is 44rem of overlay and the book's prose is the publisher's, set apart in its own component
    /// on a page built for it. The row chosen is in that page's answer, because it is the same
    /// ranked list a few rows further down.</para>
    /// </summary>
    private static Command Passage(RulebookResult result, string query) =>
        new(CommandKind.Passage,
            query,
            result.Heading,
            RulebookCitation.For(result),
            []);
}
