using ProwlersAndParagonsAutomation.Play.Dice;

namespace ProwlersAndParagonsAutomation.Play.Rules;

/// <summary>One roll: the pool asked for, the faces that came up, and the successes they scored.</summary>
/// <param name="Pool">The pool as asked for, before the sub-1d floor.</param>
/// <param name="Faces">The faces the source returned, in order.</param>
/// <param name="Successes">What those faces scored under the map in force.</param>
/// <param name="FlooredToOneDie">Whether the sub-1d rule replaced the pool with its single die.</param>
public sealed record RollResult(int Pool, IReadOnlyList<int> Faces, int Successes, bool FlooredToOneDie);

/// <summary>
/// Turns faces into successes, out of <c>play_meta.json</c> and nothing else.
///
/// <para><b>There is no literal 2, 4 or 6 anywhere in this class.</b> The map is the shipped data's
/// <c>success_map</c>, the floor is its <c>sub_one_die</c> block, and the automatic-success rate is
/// its <c>automatic_successes.dice_per_success</c>. A number typed here would be a second
/// transcription of a rule the store already holds, which is the failure
/// <c>docs/guide/play-rules.md</c> is largely about — and it would go on agreeing with the file
/// right up until somebody changed the file.</para>
///
/// <para><b>Checking Your Swing swaps the map rather than adjusting the answer</b> (Ch.3 p.69). It
/// is a table setting, so the map is chosen once when the counter is built: under it every even
/// face is worth exactly one success, which is a different <c>success_map</c> in
/// <c>challenge.json</c>, not an arithmetic correction to <c>play_meta.json</c>'s.</para>
/// </summary>
public sealed class SuccessCounter
{
    private readonly IReadOnlyDictionary<string, int> _map;
    private readonly Models.MetaSubOneDieModel _floor;
    private readonly int _dicePerAutomaticSuccess;

    /// <summary>The entry the map in force came from, for a ledger line to cite.</summary>
    public string MapSourceRef { get; }

    /// <summary>Whether the flattened Checking Your Swing map is the one in force.</summary>
    public bool CheckingYourSwing { get; }

    /// <param name="play">The play rules.</param>
    /// <param name="checkingYourSwing">
    /// Whether the table has taken Ch.3 p.69's optional flatter map. It is a setting for a whole
    /// table, taken before play, which is why it is fixed for the life of the counter.
    /// </param>
    public SuccessCounter(PlayRulesRepository play, bool checkingYourSwing = false)
    {
        ArgumentNullException.ThrowIfNull(play);

        CheckingYourSwing = checkingYourSwing;

        if (checkingYourSwing)
        {
            var swing = play.GetChallenge("checking_your_swing");
            _map = swing.CheckingYourSwing!.SuccessMap;
            MapSourceRef = swing.SourceRef;
        }
        else
        {
            var reading = play.GetMeta("success_map");
            _map = reading.SuccessMap!;
            MapSourceRef = reading.SourceRef;
        }

        _floor = play.GetMeta("sub_one_die_floor").SubOneDie!;
        _dicePerAutomaticSuccess = play.GetMeta("automatic_successes").AutomaticSuccesses!.DicePerSuccess;
    }

    /// <summary>What one face is worth under the map in force.</summary>
    public int Value(int face) =>
        _map.TryGetValue(face.ToString(System.Globalization.CultureInfo.InvariantCulture), out var value)
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(face), face, $"The success map in force says nothing about a face of {face}.");

    /// <summary>What a handful of faces is worth under the map in force.</summary>
    public int Count(IEnumerable<int> faces) => faces?.Sum(Value)
        ?? throw new ArgumentNullException(nameof(faces));

    /// <summary>
    /// Throws <paramref name="pool"/> dice and counts them.
    ///
    /// <para><b>A pool below one die is not a pool of nothing</b> (p.67): one die is thrown, it
    /// scores only on the faces the entry names, and it scores only what the entry says even then.
    /// The floor is applied here rather than by the caller because it is a rule, and
    /// <see cref="IDiceSource"/> is a source of numbers.</para>
    /// </summary>
    public RollResult Roll(int pool, IDiceSource dice)
    {
        ArgumentNullException.ThrowIfNull(dice);

        if (pool >= 1)
        {
            var thrown = dice.Roll(pool);
            return new RollResult(pool, thrown, Count(thrown), FlooredToOneDie: false);
        }

        var floored = dice.Roll(_floor.DiceRolled);

        var successes = floored
            .Where(face => _floor.CountingFaces.Contains(face))
            .Sum(_ => _floor.SuccessesWhenHit);

        return new RollResult(pool, floored, successes, FlooredToOneDie: true);
    }

    /// <summary>
    /// The successes a character banks by declining to roll (p.67), at the file's own rate.
    ///
    /// <para><b>The odd die is left out, and the entry's <c>ambiguity</c> is why.</b> The rule is
    /// priced in pairs and says nothing about an odd pool; the printed example is 12d taking 6,
    /// which is even and settles nothing. Integer division takes the reading that the leftover die
    /// buys nothing, which is the one that never gives a character more than the page promises.
    /// </para>
    /// </summary>
    public int AutomaticSuccesses(int pool) => Math.Max(0, pool) / _dicePerAutomaticSuccess;
}
