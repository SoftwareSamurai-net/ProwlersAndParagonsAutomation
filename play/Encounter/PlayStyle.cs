namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// How a side plays, as a seeded run can express it.
///
/// <para><b>A style is a policy and a policy is not a rule.</b> Nothing here is printed anywhere in
/// Chapters 3–5: the book says what a spend buys, never when a person would buy one. So each of
/// these is a guess about a way of playing, stated so it can be argued with, and every report that
/// quotes a figure off one prints its name and its note beside the figure.</para>
///
/// <para><b>The owner named five and this enum has four.</b> The fifth is <em>narrative</em> — the
/// Villain acting befitting their character, with their Flaws coming up — and a seed cannot fake it:
/// nothing in <c>play/</c> makes a Flaw bite (p.85's own suppression spend says so in as many words,
/// and the entry hands "whenever the opportunity presents itself" to the GM), and "befitting" is a
/// judgement rather than a comparison. It is the live <c>take_turn</c> path with a model as the
/// Villain, and the encounter server refuses the name <c>narrative</c> saying exactly that — so
/// nobody reads its absence from this list as an oversight.</para>
/// </summary>
public enum PlayStyle
{
    /// <summary>Sheets alone: no Resolve, no Adversity, no purchase of any kind.</summary>
    ManoAMano,

    /// <summary>A mixture: seize when out-Edged, and answer the other side's spending.</summary>
    Standard,

    /// <summary>Spend whenever a spend flips an outcome the policy can compute.</summary>
    MinMax,

    /// <summary>All-out on every page, and never a point spent on staying alive.</summary>
    Reckless
}

/// <summary>
/// Which opponent a side goes after — an axis of its own, independent of <see cref="PlayStyle"/>.
///
/// <para><b>It is separate because the two answer different questions.</b> How freely a table
/// spends and who they swing at are not the same habit: a party that never touches its Resolve may
/// still focus fire on whoever is nearly down, and a party that min-maxes every roll may still
/// spread its damage. Composing them means a measurement can hold one still and move the other,
/// which is what a balance question needs.</para>
/// </summary>
public enum Targeting
{
    /// <summary>Whoever has the least Health left — a group of Minions, the fewest bodies.</summary>
    Weakest,

    /// <summary>Whoever has the most left: take the hardest one down while everybody is fresh.</summary>
    Strongest,

    /// <summary>
    /// Whoever has done the most damage so far, and — before anybody has landed anything — whoever
    /// has the greatest attack rank.
    /// </summary>
    HighestThreat
}
