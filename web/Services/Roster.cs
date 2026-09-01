namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>Which order a roster's rows are drawn in.</summary>
public enum RosterOrder
{
    /// <summary>
    /// Grouped under the game each character is in, with the ones in no game last.
    ///
    /// <para>The default, and it is the only one of the three that costs nothing to know: the
    /// campaign is already on every row of the index, so grouping by it is a read this panel has
    /// already done. See <see cref="SavedCharacterSummary.CampaignId"/>.</para>
    /// </summary>
    ByGame,

    /// <summary>By name, so a character somebody is looking for is where the alphabet says.</summary>
    Alphabetical,

    /// <summary>Most recently touched first — the order the stores answer in, left alone.</summary>
    Recent,
}

/// <summary>
/// What the ids on a row resolve to, for the one screen that has both lookups to hand.
///
/// <para><b>Two dictionaries and not two parameters, because they are the same kind of thing and
/// have the same type.</b> Adjacent parameters of type
/// <c>IReadOnlyDictionary&lt;string, string&gt;</c> can be passed the wrong way round in silence,
/// and the failure would be a roster grouped by tier name — which is not a compile error and not
/// obviously wrong on a screen.</para>
/// </summary>
/// <param name="Games">What each campaign this account holds is called, by id. Empty when nothing
/// could be asked, which is not the same as an account with no campaigns — see
/// <see cref="Roster.NameOf"/>.</param>
/// <param name="Tiers">What each tier is called, by id, out of <c>data/rules/tiers.json</c>. Used
/// for matching and for drawing, never stored: the name is the rules data's to change.</param>
public sealed record RosterNames(
    IReadOnlyDictionary<string, string> Games, IReadOnlyDictionary<string, string> Tiers)
{
    /// <summary>Nothing resolvable. What a signed-out visitor's roster has.</summary>
    public static RosterNames None { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new Dictionary<string, string>(StringComparer.Ordinal));
}

/// <summary>
/// The word for a stored <c>kind</c>, for a reader rather than for a column.
///
/// <para><b>Here rather than on <see cref="SheetMode"/>, and the line is the same one
/// <c>Standings</c> draws.</b> What an index stores is a lower-case key that outlives any
/// particular screen; what a row prints is a capitalised word. An unrecognised key — a row written
/// by a later version, or a hand-edited one — is <c>null</c> rather than a guess, so a roster
/// draws no chip instead of an invented one.</para>
/// </summary>
public static class Kinds
{
    /// <summary>What to call one stored kind, or null when it is not one this app writes.</summary>
    public static string? Word(string? kind) => kind switch
    {
        "hero" => "Hero",
        "villain" => "Villain",
        _ => null,
    };
}

/// <summary>One heading and the rows under it.</summary>
/// <param name="Heading">What the heading reads. Never empty.</param>
/// <param name="Aside">
/// The count beside the heading, or null when there is only one group and the count would be
/// the list's own length restated.
/// </param>
/// <param name="Characters">The rows, in the order they are to be drawn.</param>
public sealed record RosterGroup(
    string Heading, string? Aside, IReadOnlyList<SavedCharacterSummary> Characters);

/// <summary>
/// What a list of many characters is filtered and grouped into, decided without rendering
/// anything.
///
/// <para><b>The component is presentation and this is where the decisions are</b> — the same split
/// <see cref="Commands"/> makes, and for the same reason: which rows survive a query, which
/// heading they land under and what the count beside it says are all answerable without a
/// browser, and keeping them here is what makes them testable without one.</para>
///
/// <para><b>Nothing here reads a payload.</b> Every field it works from is one the index already
/// holds — the label, the campaign id, the time — because a list of many characters must not
/// deserialize and cost every one of them to draw a row. That constraint is
/// <see cref="SavedCharacters"/>'s and it is the reason this class can only group by the things
/// it groups by.</para>
///
/// <para><b>The matching rule is <see cref="OptionFilter.Matches"/> and is deliberately not a
/// second one.</b> Six pick-lists and the command palette already answer to it; a roster that
/// matched differently would teach a reader that what they had learnt was about one list.</para>
/// </summary>
public static class Roster
{
    /// <summary>The heading for characters that belong to no campaign. They sort last.</summary>
    public const string NoGame = "In no game";

    /// <summary>
    /// The heading for characters naming a campaign this browser cannot resolve.
    ///
    /// <para><b>Reported, never repaired</b> — the same answer <c>UNKNOWN_CAMPAIGN</c> gives one
    /// level up. Deleting a campaign leaves its members naming it on purpose, so that restoring it
    /// puts everything back; and a signed-out visitor cannot resolve any campaign at all, which is
    /// a fact about this moment rather than about the character.</para>
    /// </summary>
    public const string GameNotHere = "A game that is not here";

    /// <summary>
    /// What to call the game one character is in.
    /// </summary>
    /// <param name="campaignId">The character's campaign, or null for one in none.</param>
    /// <param name="names">What each campaign this account holds is called, by id.</param>
    public static string NameOf(string? campaignId, RosterNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (campaignId is null) return NoGame;

        return names.Games.TryGetValue(campaignId, out var name) ? name : GameNotHere;
    }

    /// <summary>
    /// Whether one row survives what the reader has typed.
    ///
    /// <para><b>The game's name is matched though the row does not always print it</b> — the same
    /// bargain a Power's tags make on the Powers tab. Typing a campaign's name is how somebody
    /// asks for "everyone in that game", and it is the question a roster exists to answer.</para>
    /// </summary>
    public static bool Admits(SavedCharacterSummary one, string query, RosterNames names)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(names);

        return OptionFilter.Matches(
            query,
            one.Label,
            NameOf(one.CampaignId, names),
            Kinds.Word(one.Kind),
            one.TierId is { } tier && names.Tiers.TryGetValue(tier, out var named) ? named : null);
    }

    /// <summary>
    /// The rows a roster draws, in their groups.
    /// </summary>
    /// <param name="characters">Every character the list holds, unfiltered.</param>
    /// <param name="query">What the reader has typed. Empty admits everything.</param>
    /// <param name="order">Which order the reader asked for.</param>
    /// <param name="names">What each campaign this account holds is called, by id.</param>
    /// <param name="oneGroup">
    /// The heading to use when there is nothing to tell apart — the sentence the panel has always
    /// carried, saying where these characters live.
    /// </param>
    /// <remarks>
    /// <para><b>A group with nothing matching it is not drawn, and the count says what was
    /// hidden.</b> An empty heading is furniture over nothing; the surviving headings read
    /// "4 of 11" while a filter is on, so a reader can see that a game holds more than they are
    /// being shown. With no filter the count is the group's own size and nothing else.</para>
    ///
    /// <para><b>How many groups there are is decided from the unfiltered list.</b> Deciding it
    /// from the matches would let a heading change identity as somebody types — narrowing to one
    /// game would rename its heading to the sentence about where characters live — and a heading
    /// that moves under a reader's typing is worse than one that is briefly redundant.</para>
    /// </remarks>
    public static IReadOnlyList<RosterGroup> Group(
        IReadOnlyList<SavedCharacterSummary> characters,
        string query,
        RosterOrder order,
        RosterNames names,
        string oneGroup)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(names);

        var matching = characters.Where(c => Admits(c, query, names)).ToList();

        if (order != RosterOrder.ByGame)
        {
            var ordered = order == RosterOrder.Alphabetical
                ? matching.OrderBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase).ToList()
                : matching;

            return ordered.Count == 0 ? [] : [new RosterGroup(oneGroup, null, ordered)];
        }

        // Every game the account's characters are in, whether or not anything in it matched — see
        // the remark above on why the shape of the headings is not the filter's business.
        var everyGame = characters
            .Select(c => NameOf(c.CampaignId, names))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var sizes = characters
            .GroupBy(c => NameOf(c.CampaignId, names), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return
        [
            .. matching
                .GroupBy(c => NameOf(c.CampaignId, names), StringComparer.Ordinal)
                .OrderBy(g => Rank(g.Key))
                .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new RosterGroup(
                    Heading(g.Key, everyGame, oneGroup),
                    Aside(g.Key, g.Count(), sizes, everyGame, query),
                    [.. g])),
        ];
    }

    /// <summary>
    /// What the heading over one group reads.
    ///
    /// <para><b>A single group of a named game keeps the game's name, and the other two do
    /// not.</b> "Ashfall" over every row says something true and useful; "In no game" over every
    /// row of an account that has never had one is an answer to a question nobody asked, and so
    /// is a heading reporting that a campaign could not be resolved when that is every row on the
    /// screen. In both of those the sentence the panel has always carried — where these
    /// characters live — is the better heading.</para>
    /// </summary>
    private static string Heading(string game, List<string> everyGame, string oneGroup) =>
        everyGame.Count == 1 && game is NoGame or GameNotHere ? oneGroup : game;

    /// <summary>
    /// The count beside a heading, or null when there is nothing for it to add.
    ///
    /// <para>One group of everything is the list's own length, which <c>Aside</c> on the panel
    /// already states — printing it again here is the fact-read-twice the panel's own remarks
    /// warn about.</para>
    /// </summary>
    private static string? Aside(
        string game, int shown, Dictionary<string, int> sizes, List<string> everyGame, string query)
    {
        if (everyGame.Count == 1) return null;

        var total = sizes.TryGetValue(game, out var size) ? size : shown;

        return query.Length == 0 ? $"{total}" : $"{shown} of {total}";
    }

    /// <summary>
    /// Which band a heading sorts into: real games first, then the unresolvable ones, then the
    /// characters in no game at all.
    ///
    /// <para>"In no game" is last because it is the absence of the thing being grouped by, and a
    /// list of games that opens with the pile of things that are in none reads as though that
    /// pile were the subject.</para>
    /// </summary>
    private static int Rank(string game) => game switch
    {
        NoGame => 2,
        GameNotHere => 1,
        _ => 0,
    };
}
