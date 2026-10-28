using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// One question a front end asks when a Perk is bought: what to ask, what the answer prints
/// after, and — for the identity question — the only answers there are.
/// </summary>
/// <param name="Ask">The question, in the entry's own terms.</param>
/// <param name="Label">What the answer prints after on the sheet, or null to print it bare.</param>
/// <param name="Choices">The answers to pick between, or null for words the player writes.</param>
public sealed record PerkQuestion(string Ask, string? Label = null, IReadOnlyList<string>? Choices = null);

/// <summary>
/// What a Perk asks the player to write down, as separate questions, and the one line of detail
/// their answers make.
///
/// <para><b>One place, so the browser and the terminal ask the same things.</b> The sheet still
/// stores one string per Perk (<see cref="SelectedPerk.NarrativeDetail"/>), so every sheet, export
/// and diff that prints it already prints the result, and a stored character round-trips byte for
/// byte. What changed is how many boxes the answer is gathered in.</para>
///
/// <para><b>Words, never a price.</b> Nothing here is read by <see cref="CostCalculator"/> or
/// checked by <see cref="CharacterValidator"/>: an unanswered question is a box the editor will not
/// let past, the rule the Flaws and Pros and Cons already follow.</para>
/// </summary>
public static class PerkQuestions
{
    /// <summary>The Flaw whose holder has two identities for a Perk to belong to.</summary>
    public const string SecretIdentityFlaw = "secret_identity";

    /// <summary>The two answers to the identity question, in the entry's own words.</summary>
    public static readonly IReadOnlyList<string> Identities = ["Civilian identity", "Heroic alter ego"];

    /// <summary>
    /// Every question <paramref name="perk"/> asks of <paramref name="sheet"/> at
    /// <paramref name="units"/> units, in the order they are asked. Empty for a Perk that asks
    /// nothing.
    /// </summary>
    public static IReadOnlyList<PerkQuestion> For(PerkModel perk, int units, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(sheet);

        var questions = new List<PerkQuestion>();

        // Asked only of a character who has two identities: "if you have a secret identity, you
        // must decide". Anybody else has one identity and nothing to decide.
        if (perk.IdentityChoice && sheet.Flaws.Any(f => f.FlawId == SecretIdentityFlaw))
        {
            questions.Add(new PerkQuestion(
                $"You have a secret identity. Which one has {perk.Name}?", Choices: Identities));
        }

        if (perk.NarrativePrompts.Count > 0)
        {
            questions.AddRange(perk.NarrativePrompts.Select(p => new PerkQuestion(p.Ask, p.Label)));
        }
        else if (perk.NarrativeConstraint is not null && perk.NarrativePerUnit)
        {
            // One box per unit, each named for what the unit buys. The constraint itself is the
            // heading over them — a front end prints it once, above "Contact type 1".
            var noun = Capitalised(perk.UnitLabel ?? "Answer");
            for (var i = 1; i <= Math.Max(1, units); i++) questions.Add(new PerkQuestion($"{noun} {i}"));
        }
        else if (perk.NarrativeConstraint is { } only)
        {
            questions.Add(new PerkQuestion(only));
        }

        return questions;
    }

    /// <summary>
    /// The one line of detail <paramref name="answers"/> make, or null while any question is
    /// unanswered — so a caller has one test for "may this be added yet".
    /// </summary>
    public static string? Compose(IReadOnlyList<PerkQuestion> questions, IReadOnlyList<string?> answers)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(answers);

        if (questions.Count == 0) return null;
        if (answers.Count < questions.Count) return null;

        var trimmed = answers.Take(questions.Count).Select(a => a?.Trim() ?? "").ToList();
        if (trimmed.Any(a => a.Length == 0)) return null;

        // The identity, where asked, leads and is set off from the rest: "Heroic alter ego — the
        // criminal courts" reads as one fact qualified, where a semicolon would read as two.
        var lead = questions[0].Choices is not null ? trimmed[0] : null;
        var rest = questions.Zip(trimmed)
            .Skip(lead is null ? 0 : 1)
            .Select(pair => pair.First.Label is { } label ? $"{label}: {pair.Second}" : pair.Second)
            .ToList();

        var body = string.Join("; ", rest);

        return lead is null ? body
            : body.Length == 0 ? lead
            : $"{lead} — {body}";
    }

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
