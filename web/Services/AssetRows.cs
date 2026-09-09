using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The two answers a screen offering Chapter 6's feature tables needs, in one place because there
/// are two such screens now and neither may disagree with the other.
///
/// <para><b>A price here always carries its currency.</b> A vehicle feature is Vehicle Points, a
/// base feature is Base Points, and neither is a Hero Point — a bare number in a list beside a
/// Hero Point budget is the one category error the whole of Chapter 6 is careful about, and it is
/// the reason this is a shared method rather than an interpolation written out twice.</para>
///
/// <para><b>It decides no figure.</b> Every number below is off the catalogue row, which is off
/// the rules data; what is decided here is the wording around it.</para>
/// </summary>
public static class AssetRows
{
    /// <summary>
    /// The right-hand column of a feature row: what it costs, <b>with its currency</b> — a flat
    /// price, a rate for one bought per unit, or the range a graded one spans.
    /// </summary>
    public static string Price(AssetCatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var unit = row.Kind == AssetRowKind.BaseFeature ? "BP" : "VP";

        return row switch
        {
            { Cost: { } flat }        => $"{flat} {unit}",
            { CostPerUnit: { } rate } => $"{rate} {unit} each",   // abbreviated, so no plural to get wrong
            { Grades: { } grades }    => $"{grades.Values.Min()}–{grades.Values.Max()} {unit}",
            _                         => ""
        };
    }

    /// <summary>
    /// The grade a graded feature goes on at, or null for one with no grades.
    ///
    /// <para><b>The cheapest, because <see cref="CostCalculator"/> throws rather than guessing
    /// one</b> — and the cheapest is the honest default to offer somebody who has not yet said
    /// which they want, since it is the one that cannot silently spend points they had other
    /// plans for.</para>
    /// </summary>
    public static string? CheapestGrade(AssetCatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Grades is { } grades ? grades.OrderBy(g => g.Value).First().Key : null;
    }
}
