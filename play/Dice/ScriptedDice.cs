namespace ProwlersAndParagonsAutomation.Play.Dice;

/// <summary>
/// The faces a printed example was rolled with, handed out in order.
///
/// <para><b>Running out is a throw, and that is a positive control rather than defensive
/// programming.</b> A fixture that replays a worked example is a claim that the engine made exactly
/// the rolls the page describes. A source that quietly returned zeros, or wrapped around, would let
/// an engine that made <em>fewer</em> rolls than the page — one that skipped the defence roll, say —
/// still reach the printed answer, and the fixture would report a mechanic it had never exercised.
/// That is the single most common way a check in this repository has been wrong.</para>
///
/// <para>The other half of the control is <see cref="Remaining"/>: a fixture asserts it is zero at
/// the end, so an engine making <em>more</em> rolls than the page is caught as well as one making
/// fewer.</para>
/// </summary>
public sealed class ScriptedDice : IDiceSource
{
    private readonly int[] _faces;
    private int _next;

    /// <param name="faces">Every face the encounter will be given, in the order it will ask.</param>
    public ScriptedDice(params int[] faces)
    {
        ArgumentNullException.ThrowIfNull(faces);

        foreach (var face in faces)
        {
            if (face is < 1 or > 6)
                throw new ArgumentOutOfRangeException(
                    nameof(faces), face, "A scripted die face has to be a face a d6 has.");
        }

        _faces = [.. faces];
    }

    /// <summary>How many scripted faces have not been asked for yet.</summary>
    public int Remaining => _faces.Length - _next;

    public int[] Roll(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_next + count > _faces.Length)
        {
            throw new InvalidOperationException(
                $"The encounter asked for {count} dice with only {Remaining} scripted faces left "
                + $"(of {_faces.Length}). Either the fixture is short of the rolls the printed "
                + "example makes, or the engine is making a roll the page does not.");
        }

        var faces = _faces[_next..(_next + count)];
        _next += count;
        return faces;
    }
}
