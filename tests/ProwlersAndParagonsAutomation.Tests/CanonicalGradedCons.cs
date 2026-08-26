namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The variant-to-value mapping for the four graded Cons in Chapter 2's Pros and Cons
/// section (pp.48-54) — Conditional, Limited, Shutdown, Side Effect — transcribed from each
/// Con's own printed grading sentence, not from data/rules/cons.json.
///
/// <para><c>RulesDataTests.GradedConsRunFromMinusOneToMinusFour</c> already checks the
/// <em>multiset</em> of values each of these four carries is {-1, -2, -4}; it does not check
/// which variant key earns which value. Swapping two grades — so, say, Limited's "somewhat
/// limited" priced at -4 instead of -1 — passes that test and every other one in the suite,
/// because nothing compares a variant key to its value against the book.</para>
///
/// <para>All four entries follow the same printed pattern: "This is a -1 Con if [mild], a -2
/// Con if [moderate], or a -4 Con if [severe]." The variant keys in cons.json paraphrase the
/// bracketed clauses; the values are transcribed straight from the sentence.</para>
/// </summary>
public static class CanonicalGradedCons
{
    public sealed record Entry(string Id, IReadOnlyDictionary<string, int> Grades, int Page, string Printed);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("conditional",
            new Dictionary<string, int> { ["often_works"] = -1, ["occasionally_works"] = -2, ["rarely_works"] = -4 },
            49,
            "\"...this is a -1 Con if the Power often works, a -2 Con if it occasionally works, or a -4 Con if it rarely works.\""),

        new("limited",
            new Dictionary<string, int> { ["somewhat_limited"] = -1, ["significantly_limited"] = -2, ["severely_limited"] = -4 },
            51,
            "\"This is a -1 Con if the Power is somewhat limited, a -2 Con if it's significantly limited, or a -4 Con if it's severely limited.\""),

        new("shutdown",
            new Dictionary<string, int> { ["often_works"] = -1, ["occasionally_works"] = -2, ["rarely_works"] = -4 },
            53,
            "\"...this is a -1 Con if the Power often works, a -2 Con if it occasionally works, or a -4 Con if it rarely works.\""),

        new("side_effect",
            new Dictionary<string, int> { ["annoying"] = -1, ["detrimental"] = -2, ["catastrophic"] = -4 },
            53,
            "\"This is a -1 Con if the side effect is annoying or inconvenient, a -2 Con if the side effect is detrimental or harmful, or a -4 Con if the side effect is catastrophic or debilitating.\""),
    ];
}
