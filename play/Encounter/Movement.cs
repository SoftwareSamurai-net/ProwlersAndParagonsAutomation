using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>One exchange of a chase, and what it did.</summary>
/// <param name="NetSuccesses">What the winner beat the quarry by.</param>
/// <param name="ClosedARangeClass">Whether it was enough to move the pair a whole class.</param>
/// <param name="BonusDiceNextExchange">What winning the exchange lends to the next one.</param>
/// <param name="Ended">Whether the pursuit has run past one of its two ends.</param>
public sealed record ChaseExchange(
    int NetSuccesses, bool ClosedARangeClass, int BonusDiceNextExchange, bool Ended);

/// <summary>
/// Distance, as Chapter 4 p.74 prices it.
///
/// <para><b>Pure functions rather than part of the encounter, because two of the chapter's printed
/// examples are about nothing else</b> — Powermad running and Flicker flying, and the three
/// exchanges of the chase after them. Keeping them here lets a fixture put the book's own numbers in
/// and read the book's own answer out, without standing a whole fight up around it.</para>
///
/// <para>Every figure is the entry's. The Travel Power rank a character needs to halve their travel
/// time, the pages a range class costs, the net successes a chase exchange needs and the dice it
/// lends are all read from <c>movement</c> and <c>chases</c>.</para>
/// </summary>
public static class Movement
{
    /// <summary>
    /// The Powers p.74 means by "a Travel Power", by the ids the character rules give them.
    ///
    /// <para><b>This list is a reading and is the one thing here the data does not answer.</b> The
    /// entry says "a Travel Power" and Chapter 2 has no such category flag — the Movement category
    /// is the closest thing, and it holds Powers nobody would call travel. So the ids are named,
    /// and <c>docs/guide/play-engine.md</c> records it as a reading rather than a transcription.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> TravelPowerIds { get; } =
        ["running", "flight", "leaping", "swimming", "burrowing", "super_speed", "teleportation", "swing_line"];

    /// <summary>
    /// How many pages it takes this combatant to cross one range class (p.74): two on foot, one with
    /// a Travel Power at the rank the entry requires.
    /// </summary>
    public static int PagesToCrossARangeClass(PlayRulesRepository play, Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(combatant);

        var rule = play.GetCombat("movement").Movement!;

        var best = TravelPowerIds.Max(combatant.Rank);

        return best >= rule.TravelPowerRankRequired
            ? rule.PagesPerRangeClassWithATravelPower
            : rule.PagesPerRangeClass;
    }

    /// <summary>
    /// How many pages a rank of travel takes to cross a range class, without a combatant to ask.
    /// The form p.74's own worked example is in: Powermad on foot, Flicker on 9d Running.
    /// </summary>
    public static int PagesToCrossARangeClass(PlayRulesRepository play, int travelPowerRank)
    {
        ArgumentNullException.ThrowIfNull(play);

        var rule = play.GetCombat("movement").Movement!;

        return travelPowerRank >= rule.TravelPowerRankRequired
            ? rule.PagesPerRangeClassWithATravelPower
            : rule.PagesPerRangeClass;
    }

    /// <summary>
    /// One exchange of a chase (p.74), from the pursuer's net successes and where the pair are now.
    ///
    /// <para>The pursuit ends closer than the innermost class or farther than the outermost, and the
    /// two names are resolved against <c>range_classes</c>'s own row order rather than assumed — an
    /// unmatched name would make the ending unreachable and leave a chase running for ever while
    /// every other figure stayed right.</para>
    /// </summary>
    /// <param name="play">The play rules.</param>
    /// <param name="net">The pursuer's net successes this exchange.</param>
    /// <param name="band">Where the pair are before it.</param>
    /// <param name="closing">True where the pursuer is closing, false where the quarry is opening.</param>
    public static ChaseExchange Exchange(PlayRulesRepository play, int net, RangeBand band, bool closing = true)
    {
        ArgumentNullException.ThrowIfNull(play);

        var chase = play.GetCombat("chases").Chase!;
        var classes = play.GetCombat("range_classes").Ranges!;

        var ladder = classes.Select(c => c.Class).ToList();

        var innermost = ladder.IndexOf(chase.EndsCloserThan.Replace(" Range", "", StringComparison.Ordinal));
        var outermost = ladder.IndexOf(chase.EndsFartherThan.Replace(" Range", "", StringComparison.Ordinal));

        if (innermost < 0 || outermost < 0)
        {
            throw new InvalidOperationException(
                $"chases names its ends as '{chase.EndsCloserThan}' and '{chase.EndsFartherThan}', "
                + $"and range_classes lists {string.Join(", ", ladder)} — so a chase could never end.");
        }

        var moved = net >= chase.NetSuccessesToMoveOneRangeClass;
        var position = (int)band + (moved ? closing ? -1 : 1 : 0);

        return new ChaseExchange(
            net,
            moved,
            chase.ExchangeWinBonusDiceNextExchange,
            position < innermost || position > outermost);
    }
}
