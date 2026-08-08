using System.Globalization;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Renders a Power's rulebook stat line — Range, Rank and Cost — the way the
/// rulebook prints it, so what the wizard shows can be checked against the book.
/// </summary>
public static class PowerFormatter
{
    private static string Range(PowerModel p) => p.Range switch
    {
        "self"    => "Self",
        "touch"   => "Touch",
        "ranged"  => "Ranged",
        "zone"    => "Zone",
        "special" => "Special",
        _         => p.Range
    };

    public static string RankType(PowerModel p) => p.RankType switch
    {
        "power"    => "Power Rank",
        "default"  => "Default Rank (no rank)",
        "special"  => "Special",
        "baseline" => p.Prerequisite is { } q
                          ? $"Baseline Rank ({BaselineSource(q)})"
                          : "Baseline Rank",
        _          => p.RankType
    };

    private static string BaselineSource(PowerPrerequisiteModel q) => q.Relationship switch
    {
        "baseline_half"           => $"½ {Title(q.Ability)}",
        "baseline_fixed"          => $"{q.FixedValue ?? 0}d",
        "baseline_greater_of"     => $"{Title(q.Ability)} or {string.Join(" or ", q.Powers.Select(Title))}",
        "baseline_selected_trait" => "a Trait you nominate",
        _                         => Title(q.Ability)
    };

    private static string Cost(PowerModel p) => p.CostType switch
    {
        // A constant pattern rather than ==. The rates the rulebook prints are 0.5, 1, 2
        // and 3, all exact in binary floating point, so this comparison is safe — but a
        // reader (and an inspection) is right to be suspicious of `== 0.5` on a double,
        // and the pattern says "is this rate" rather than "is this arithmetic equal".
        "per_rank" => p.CostPerRank is 0.5
            ? "1 HP per 2 ranks"
            : $"{p.CostPerRank} HP per rank",

        "flat" => $"{p.CostFlat} HP",

        "per_unit" => $"{p.CostPerUnit} HP per {p.CostUnitLabel}",

        "per_rank_variable" => string.Join(" or ",
            (p.CostVariants ?? new Dictionary<string, double>())
                .Select(v => $"{v.Value} HP per rank ({Title(v.Key)})")),

        "flat_variable" => string.Join(" or ",
            (p.CostVariants ?? new Dictionary<string, double>())
                .Select(v => $"{v.Value} HP ({Title(v.Key)})")),

        "special" => "Special",

        _ => p.CostType
    };

    /// <summary>The full stat line, e.g. "Self · Baseline Rank (½ Toughness) · 1 HP per rank".</summary>
    public static string StatLine(PowerModel p) =>
        $"{Range(p)} · {RankType(p)} · {Cost(p)}";

    /// <summary>
    /// A rules id set the way the rulebook prints the thing it names: "Toughness", not
    /// "toughness"; "Martial Arts", not "martial_arts".
    ///
    /// <para>This used to swap underscores for spaces and stop, so a stat line read
    /// "Baseline Rank (½ toughness)" at a player holding a book that capitalises every
    /// Trait — while the doc comment on <see cref="StatLine"/> claimed otherwise.</para>
    /// </summary>
    private static string Title(string? id) =>
        string.IsNullOrEmpty(id)
            ? "—"
            : string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0], CultureInfo.CurrentCulture) + w[1..]));
}
