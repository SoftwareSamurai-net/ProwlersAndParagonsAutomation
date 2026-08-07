using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Renders a piece of gear as one line, the way a published sheet writes it:
/// <c>Jo Sticks (Upgraded, Two-Fisted pair) — 2 HP</c>. Plain mundane gear is free and
/// prints as just its name, which is nearly every item.
/// </summary>
public static class GearFormatter
{
    public static string Describe(SelectedGear gear, RulesRepository rules, CostCalculator costs)
    {
        if (!gear.IsCustomised && !gear.PairedUnderTwoFisted) return gear.Name;

        var parts = new List<string>();

        foreach (var f in gear.Features)
        {
            var feature = rules.GetGearFeature(f.FeatureId);
            parts.Add(f.GradeKey is null
                ? feature?.Name ?? f.FeatureId
                : GradeName(f.GradeKey));
        }

        parts.AddRange(gear.Pros.Select(p => rules.GetPro(p.Id)?.Name ?? p.Id));
        parts.AddRange(gear.Cons.Select(c => rules.GetCon(c.Id)?.Name ?? c.Id));

        if (gear.PairedUnderTwoFisted) parts.Add("Two-Fisted pair");

        var cost = costs.GearCost(gear);
        var suffix = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";

        return $"{gear.Name}{suffix} — {cost} HP";
    }

    /// <summary>
    /// The two graded features print the grade the player bought, not the "X / Very X"
    /// heading: "very_accurate" reads as "Very Accurate".
    /// </summary>
    private static string GradeName(string gradeKey) =>
        string.Join(" ", gradeKey.Split('_')
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
