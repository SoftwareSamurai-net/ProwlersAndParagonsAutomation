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
