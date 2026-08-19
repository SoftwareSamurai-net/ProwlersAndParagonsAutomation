namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>Which of the two things this site is you are currently in.</summary>
public enum Area
{
    /// <summary>
    /// The play aide: the rules reference and the character sheet helper. The thing somebody
    /// has open at a table.
    /// </summary>
    Play,

    /// <summary>
    /// The portfolio: the recorded conversations and the finished sample characters. The thing
    /// that shows somebody what was built, rather than helping them build.
    /// </summary>
    Portfolio,
}

/// <summary>
/// Which area an address belongs to.
///
/// <para><b>One site doing two jobs is why this exists.</b> The chrome above the page was built
/// for the character generator — six numbered steps and a Hero Point budget — and none of it
/// means anything on a page showing somebody else's recorded conversation. Worse, the budget
/// strip is the *visitor's own* character, so it sat above a recording of a different one with
/// nothing on screen saying whose was whose.</para>
///
/// <para><b>Decided from the address rather than passed down</b>, because the shell renders the
/// page and cannot ask it anything. That was already true of the replay check this replaces; what
/// changes is that there is now a name for the question and one place that answers it.</para>
/// </summary>
public static class Areas
{
    /// <summary>The first path segment that marks the portfolio.</summary>
    private const string PortfolioPrefix = "portfolio";

    /// <summary>
    /// The address the recordings used to live at, before there was a portfolio to put them in.
    ///
    /// <para><b>It is still a portfolio address and has to be treated as one.</b> The pages keep
    /// their old routes so shared links do not rot — and a link that still works but arrives
    /// wearing the character generator's chrome is worse than one that breaks, because the budget
    /// strip above a recording is the *visitor's own* character sitting over somebody else's with
    /// nothing saying whose is whose. That is the exact fault the strip was hidden here to fix,
    /// and moving the route reintroduced it until this line existed.</para>
    /// </summary>
    private const string LegacyReplayPrefix = "replay";

    /// <summary>
    /// The area a base-relative path belongs to.
    ///
    /// <para>Matched on the first segment, so <c>/portfolio</c> and <c>/portfolio/anything</c>
    /// both count and a future page merely beginning with those letters does not.</para>
    ///
    /// <para><b>Case-insensitively</b>, because Blazor's own route matching is: <c>/Portfolio/…</c>
    /// serves the page, and an ordinal comparison here would serve it wearing the character
    /// generator's chrome — reachable by anybody who capitalised a shared link.</para>
    /// </summary>
    public static Area Of(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var first = relativePath.Split('/', '?', '#')[0];

        return string.Equals(first, PortfolioPrefix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(first, LegacyReplayPrefix, StringComparison.OrdinalIgnoreCase)
                ? Area.Portfolio
                : Area.Play;
    }
}
