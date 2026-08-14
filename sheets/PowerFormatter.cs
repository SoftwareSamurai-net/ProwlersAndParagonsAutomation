using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// Renders a Power's rulebook stat line — Range, Rank and Cost — the way the
/// rulebook prints it, so what the wizard shows can be checked against the book.
/// </summary>
public static class PowerFormatter
{
    /// <summary>
    /// A Power's Pros or Cons as one line, with a repeated option collapsed to "Name ×N".
    ///
    /// <para>Three options in the rulebook are bought again rather than repeated by mistake,
    /// and Blastwave's Energy Absorption carries five copies of Also X — which printed as
    /// "Also X, Also X, Also X, Also X, Also X". The count is the fact worth printing;
    /// <c>SelectedProCon</c> has no free-text label, so which five energies they are cannot be
    /// recovered here either way. Shared by the text export and both browser surfaces so a
    /// repeat cannot read one way on the sheet and another on the tab.</para>
    /// </summary>
    /// <param name="choices">The selections, in the order the character carries them.</param>
    /// <param name="label">How to name one — the callers differ, so they say.</param>
    public static string ModifierLine(
        IEnumerable<SelectedProCon> choices, Func<SelectedProCon, string> label)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(label);

        var labelled = choices.Select(label).ToList();

        // First-seen order rather than GroupBy's, so a sheet lists options in the order the
        // character carries them and a repeat does not reshuffle the line.
        var parts  = new List<string>();
        var seen   = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in labelled) if (seen.Add(text)) parts.Add(text);

        var counts = labelled
            .GroupBy(t => t, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return string.Join(", ",
            parts.Select(t => counts[t] > 1 ? $"{t} ×{counts[t].ToString(CultureInfo.InvariantCulture)}" : t));
    }

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
    ///
    /// <para>Invariant, not current, culture. This is the one place in this file that is not
    /// formatting a number for a reader: the input is an ASCII rules id and the output is a
    /// rulebook term, which does not change with the machine's locale. Under a Turkish locale
    /// the current culture upper-cases <c>i</c> to <c>İ</c>, so <c>item</c> would print as
    /// <c>İtem</c> — a word that is in no edition of the book.</para>
    /// </summary>
    private static string Title(string? id) =>
        string.IsNullOrEmpty(id)
            ? "—"
            : string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..]));
}
