namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>Which part of this site you are currently in.</summary>
public enum Area
{
    /// <summary>
    /// The front door: what this site offers, and a way into each of it.
    ///
    /// <para><b>It is its own area because it is nobody's step.</b> The six numbered creation
    /// steps and a running Hero Point total are the builder's, and above a page whose whole job
    /// is to ask which of three things you came for they are an answer to a question that has
    /// not been asked yet.</para>
    /// </summary>
    Home,

    /// <summary>
    /// The character builder: the seven creation steps. The thing somebody has open while making
    /// a character.
    /// </summary>
    Play,

    /// <summary>
    /// One character, as a document rather than as a job in progress.
    ///
    /// <para><b>Its own area because the builder's chrome is exactly what it exists to drop.</b>
    /// The six numbered steps and the Hero Point strip say "you are part-way through making
    /// this"; a sheet on the table is finished by definition, and it is the thing somebody reads
    /// during play or hands to a printer. An address under <c>/build</c> would inherit both bands
    /// by construction — see <see cref="Areas.Of"/> — which is the whole reason this is not one.</para>
    ///
    /// <para><b>It is not the <c>/build/sheet</c> that was retired.</b> That address was the
    /// review step with one toggle flipped, and it went when explanations became the sheet's
    /// default: it offered a route to the page you were already on. This one differs by what it
    /// omits rather than by a setting, which is the test to hold it to if anybody adds a panel
    /// here.</para>
    /// </summary>
    Sheet,

    /// <summary>
    /// The rules reference: the book's own text, searchable, cited by printed page. The thing
    /// somebody has open at a table when a question comes up mid-session.
    ///
    /// <para><b>Separate from <see cref="Play"/> for the reason every other area is separate.</b>
    /// A reader looking a rule up is not part-way through building anything, and a budget strip
    /// reporting a character they are not editing is a different subject in the same format.</para>
    /// </summary>
    Rules,

    /// <summary>
    /// Running a game: the campaigns somebody runs, what is waiting for their decision, and the
    /// games their own characters are in.
    ///
    /// <para><b>The third door, and it is its own area for the reason the other four are.</b> Six
    /// numbered creation steps and a running Hero Point total are the builder's; above a list of
    /// games and a diff of somebody else's character they are an offer to continue something the
    /// reader is not doing. The budget is worse than meaningless here — the spend on this screen
    /// belongs to a <em>different</em> character, which is exactly the confusion the strip was
    /// pulled off three areas to fix.</para>
    ///
    /// <para><b>It was reserved before it was built.</b> <c>MainLayout</c>'s own note says a third
    /// avenue costs one <c>NavLink</c> and that a door onto an empty room is worse than a wall;
    /// there is something behind it now.</para>
    /// </summary>
    Campaign,

    /// <summary>
    /// Looking after the site rather than using it: who may have an account here, and the
    /// demonstrations kept for showing it to somebody.
    ///
    /// <para><b>A third area, added because the chrome is wrong on it for exactly the reason it
    /// is wrong on a recording.</b> Six numbered creation steps and a running Hero Point total
    /// are the character generator's, and above a list of email addresses they are an offer to
    /// continue something the reader is not doing. The budget is worse than meaningless there:
    /// it is a different subject entirely, in the same six-label format.</para>
    /// </summary>
    Account,
}

/// <summary>
/// Which area an address belongs to.
///
/// <para><b>One site doing several jobs is why this exists.</b> The chrome above the page was
/// built for the character generator — six numbered steps and a Hero Point budget — and none of
/// it means anything on a page showing somebody else's recorded conversation, a list of email
/// addresses, or a rules search. Worse, the budget strip is the <i>visitor's own</i> character,
/// so it sat above a recording of a different one with nothing on screen saying whose was
/// whose.</para>
///
/// <para><b>Decided from the address rather than passed down</b>, because the shell renders the
/// page and cannot ask it anything.</para>
///
/// <para><b>The builder is under a prefix of its own, and the front door is the bare path.</b>
/// It used to be the other way round: the tier page was <c>/</c>, so the first thing a visitor
/// met was step one of a job they had not chosen yet, and the only other thing the site does was
/// a single link in the banner. Everything the builder draws is now decided by one segment,
/// which is what lets the front door carry no builder chrome without a special case for it.</para>
/// </summary>
public static class Areas
{
    /// <summary>The first path segment that marks the character builder.</summary>
    private const string PlayPrefix = "build";

    /// <summary>The first path segment that marks the rules reference.</summary>
    private const string RulesPrefix = "rules";

    /// <summary>
    /// The first path segment that marks one character read as a document.
    ///
    /// <para>Matched on the segment like every other, so <c>/sheet</c> and <c>/sheet/{id}</c> are
    /// one area and a later <c>/sheets</c> would not be.</para>
    /// </summary>
    private const string SheetPrefix = "sheet";

    /// <summary>
    /// The first path segment that marks running a game.
    ///
    /// <para>Singular, matching the address rather than the plural heading, and matched on the
    /// segment like every other — so <c>/campaign</c> and <c>/campaign/{id}</c> are one area and a
    /// later <c>/campaigns</c> would not be.</para>
    /// </summary>
    private const string CampaignPrefix = "campaign";

    /// <summary>The first path segment that marks the administration pages.</summary>
    private const string AccountPrefix = "admin";

    /// <summary>
    /// Signing in is administration of a sort and is deliberately not the builder.
    ///
    /// <para>It used to fall through to <see cref="Area.Play"/> — the only reason being that
    /// everything did — so a visitor asking for a sign-in link met six numbered creation steps
    /// with one of them marked as the step they were on. That is the same fault the recordings
    /// and the invitation list were each given an area to fix.</para>
    /// </summary>
    private const string SignInPrefix = "signin";

    /// <summary>
    /// The area a base-relative path belongs to.
    ///
    /// <para>Matched on the first segment, so <c>/build</c> and <c>/build/anything</c> both count
    /// and a future page merely beginning with those letters does not.</para>
    ///
    /// <para><b>Case-insensitively</b>, because Blazor's own route matching is: <c>/Build/…</c>
    /// serves the page, and an ordinal comparison here would serve it without the chrome that
    /// belongs to it — reachable by anybody who capitalised a shared link.</para>
    ///
    /// <para><b>Anything unrecognised is <see cref="Area.Home"/>, not <see cref="Area.Play"/>.</b>
    /// The default is what an address nobody routed gets, and the not-found page is the clearest
    /// case: a numbered step list above "no such address" offers to continue something that never
    /// started. Falling back to the builder was safe only while the builder was every address.</para>
    /// </summary>
    public static Area Of(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var first = relativePath.Split('/', '?', '#')[0];

        if (Is(first, PlayPrefix)) return Area.Play;
        if (Is(first, SheetPrefix)) return Area.Sheet;
        if (Is(first, RulesPrefix)) return Area.Rules;
        if (Is(first, CampaignPrefix)) return Area.Campaign;
        if (Is(first, AccountPrefix) || Is(first, SignInPrefix)) return Area.Account;

        return Area.Home;
    }

    private static bool Is(string segment, string prefix) =>
        string.Equals(segment, prefix, StringComparison.OrdinalIgnoreCase);
}
