namespace ProwlersAndParagonsAutomation.Play.Dice;

/// <summary>
/// Where the second engine gets dice from.
///
/// <para><b>It returns raw d6 faces, and that is the whole of the design decision.</b> A source
/// that answered with a count of successes would be smaller and would be wrong: three separate
/// printed rules change the <em>mapping</em> from faces to successes rather than the number of dice.
/// Checking Your Swing (Ch.3 p.69) flattens a six from two successes to one; the sub-1d floor
/// (p.67) counts only sixes and only for one; and every exploding-six offer in the book — the
/// Defining Moment's, the team attack's, Checking Your Swing's paid one — rerolls a face. None of
/// those can be expressed after the faces have been thrown away.</para>
///
/// <para><b>It is deliberately not <c>Random</c>.</b> A fixture replaying a printed example needs
/// the book's own dice, a property test needs a seed it can name in a report, and a balance
/// measurement needs both. <see cref="SeededDice"/> and <see cref="ScriptedDice"/> are the two
/// implementations that ship.</para>
/// </summary>
public interface IDiceSource
{
    /// <summary>
    /// <paramref name="count"/> six-sided dice, as faces in 1..6.
    ///
    /// <para>The caller asks for the pool it wants thrown, including the single die the sub-1d
    /// floor throws: applying the floor is a rules question and belongs to
    /// <see cref="Rules.SuccessCounter"/>, not to a source of numbers.</para>
    /// </summary>
    int[] Roll(int count);
}
