namespace ProwlersAndParagonsAutomation.Play.Dice;

/// <summary>
/// Ordinary dice from a named seed.
///
/// <para><b>The seed is the point.</b> A balance measurement is worth nothing without the seed, the
/// N, the policy and the table settings printed beside it, so the seed is a property rather than a
/// constructor argument that disappears — <c>Encounter</c>'s ledger and any report built on it can
/// name it.</para>
///
/// <para><see cref="Random"/> with an explicit seed is deterministic across runs on .NET for a
/// given implementation, which is what a reproducible measurement needs; it is not a claim about
/// statistical quality, and nothing here needs one.</para>
///
/// <para><b>"For a given implementation" is the whole of the caveat, and it rules one thing out:
/// never check in a ledger, a report or a Health total produced from a seed as an expected value.</b>
/// <see cref="Random"/>'s seeded sequence is explicitly not guaranteed across .NET versions or
/// platforms — the algorithm changed at .NET 6 and Microsoft's own documentation reserves the right
/// to change it again — so a golden taken from a seed is a check that passes on the machine that
/// wrote it and fails on the next runtime the CI image picks up. That failure would arrive as a
/// suite that is red for a reason nobody can reproduce locally, which is the most expensive kind
/// this repository has.</para>
///
/// <para>What may be asserted about this class is what is true of any d6 stream: the faces are in
/// range, the same seed gives the same sequence <em>within one run</em>, and different seeds
/// diverge. A fixture that wants exact faces uses <see cref="ScriptedDice"/>, which is what the
/// printed examples do.</para>
/// </summary>
public sealed class SeededDice : IDiceSource
{
    private readonly Random _random;

    public SeededDice(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    /// <summary>The seed this source was built with, for a report to quote.</summary>
    public int Seed { get; }

    /// <summary>How many dice have been thrown so far, across every roll.</summary>
    public int Thrown { get; private set; }

    public int[] Roll(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var faces = new int[count];
        for (var i = 0; i < count; i++) faces[i] = _random.Next(1, 7);

        Thrown += count;
        return faces;
    }
}
