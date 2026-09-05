namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// One thing the engine did, and the page it did it from.
/// </summary>
/// <param name="Page">Which page of the fight — the book's unit of time, a few seconds.</param>
/// <param name="Actor">Whose action this line belongs to, or the empty string for the encounter's own.</param>
/// <param name="Rule">The id of the entry applied, exactly as it is spelled in the data.</param>
/// <param name="SourceRef">That entry's <c>source_ref</c>, so the figure traces to a printed page.</param>
/// <param name="Text">What happened, in one sentence, with the numbers in it.</param>
public sealed record LedgerLine(int Page, string Actor, string Rule, string SourceRef, string Text)
{
    /// <summary>The line as a reader sees it: the page, the rule, the sentence, and the citation.</summary>
    public override string ToString() =>
        $"p{Page} · {Rule} · {Text} [{SourceRef}]";
}

/// <summary>
/// Everything the engine did, in order.
///
/// <para><b>Every line names the rule it applied and cites its page, and that is the whole point of
/// the type.</b> A simulator's output is a number, and a number nobody can trace is a number nobody
/// should act on — the owner wants balance measured rather than guessed, and a measurement is worth
/// what its audit trail is worth. So the engine writes a line for each rule it reaches for, taking
/// the citation from the entry it read rather than from a constant beside the code: a rule whose
/// <c>source_ref</c> moved moves the ledger with it.</para>
///
/// <para><b>The ledger is also where an unimplemented spend goes.</b> An intent this slice does not
/// resolve leaves a line saying so, by name. It never silently does nothing — a no-op is
/// indistinguishable from a rule that was applied and changed nothing, which is exactly how a
/// simulator comes to be confidently wrong.</para>
/// </summary>
public sealed record Ledger(IReadOnlyList<LedgerLine> Lines)
{
    /// <summary>An empty ledger, which is where every encounter starts.</summary>
    public static Ledger Empty { get; } = new([]);

    /// <summary>This ledger with more lines on the end.</summary>
    public Ledger Plus(IEnumerable<LedgerLine> lines) => new([.. Lines, .. lines]);

    /// <summary>The whole ledger as text, one line each.</summary>
    public override string ToString() => string.Join(Environment.NewLine, Lines);
}
