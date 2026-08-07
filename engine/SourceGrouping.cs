using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Groups a character's Powers under Source headings the way the published sheets print
/// them — <c>TECH POWERS</c>, <c>MAGIC POWERS</c>, and so on.
///
/// <para>This lives in the engine rather than in a renderer because every surface needs the
/// same answer: the text sheet, the JSON export, the wizard's review step, and whatever
/// front end comes later. Only the styling differs between a Hero sheet and a Villain one —
/// Ch.9 builds Villains exactly like Heroes and prints no separate stat-block format.</para>
///
/// <para>Abilities are deliberately not grouped. The rulebook gives a Source to any Ability
/// of 7d or greater, but no published sheet marks one: Stronghold prints 10d Intellect,
/// Might and Toughness and Psidearm 10d Agility, all in a plain block. The rule and the
/// printed layout differ, and the layout is what a sheet reproduces.</para>
/// </summary>
public sealed class SourceGrouping
{
    private readonly RulesRepository _rules;

    public SourceGrouping(RulesRepository rules) => _rules = rules;

    /// <summary>Heading and Powers beneath it, in the order the sheet should print them.</summary>
    public sealed record Group(string Heading, SourceModel? Source, IReadOnlyList<SelectedPower> Powers);

    /// <summary>
    /// Heading a published sheet prints for a Source: "TECH POWERS", "MAGIC POWERS".
    /// </summary>
    public static string HeadingFor(SourceModel? source) =>
        source is null ? "POWERS" : $"{source.Name.ToUpperInvariant()} POWERS";

    /// <summary>
    /// The character's Powers, grouped by Source.
    ///
    /// <para>Groups follow sources.json order so a sheet is stable between runs rather than
    /// reordering itself as Powers are added. Powers with no Source recorded collect under
    /// a plain "POWERS" heading at the end instead of being dropped — the validator already
    /// warns about them, and silently hiding a Power from its own sheet would be worse.</para>
    /// </summary>
    public IReadOnlyList<Group> GroupPowers(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = _rules.Sources
            .Select(s => new Group(
                HeadingFor(s), s,
                sheet.SelectedPowers.Where(p => p.SourceId == s.Id).ToList()))
            .Where(g => g.Powers.Count > 0)
            .ToList();

        var unsourced = sheet.SelectedPowers
            .Where(p => p.SourceId is null || _rules.GetSource(p.SourceId) is null)
            .ToList();

        if (unsourced.Count > 0)
            groups.Add(new Group(HeadingFor(null), null, unsourced));

        return groups;
    }
}
