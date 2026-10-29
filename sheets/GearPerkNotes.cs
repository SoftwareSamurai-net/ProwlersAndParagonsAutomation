using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// The two places Chapter 6 hands gear to the GM with the character's Perks in mind, as the line
/// a Gear step prints: p.91, where what mundane gear is reasonable takes Contacts, Patron,
/// Resources and Wealth into account, and p.92, where the GM may keep custom features back from a
/// character without Patron, Resources or Wealth.
///
/// <para><b>Words, never a rule.</b> Both are GM discretion, so nothing here is priced or
/// validated: the line names the Perks the page names and which of them this character holds.
/// The Perk ids and the page come from <c>gear.json</c> — <c>perks_the_gm_weighs</c>,
/// <c>gm_may_gate_features_behind_perks</c> and each entry's <c>source_ref</c> — so the list is
/// the data's, never a second copy here. Shared by the browser and the terminal wizard.</para>
/// </summary>
public static partial class GearPerkNotes
{
    /// <summary>p.91's line, or null if the data names no Perk.</summary>
    public static string? MundaneGear(RulesRepository rules, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(sheet);

        var catalogue = rules.Equipment.EquipmentCatalogue;
        var ids = catalogue.MundaneGear.PerksTheGmWeighs;

        return ids.Count == 0 ? null
            : $"Whether an item is reasonable is the GM's call, taking {Join(rules, ids, "and")} into account"
              + $"{Cite(catalogue.SourceRef)}.{Held(rules, sheet, ids)}";
    }

    /// <summary>p.92's line, or null if the data names no Perk.</summary>
    public static string? CustomFeatures(RulesRepository rules, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(sheet);

        var entry = rules.Equipment.CustomGear;
        var ids = entry.Customization?.GmMayGateFeaturesBehindPerks ?? [];

        return ids.Count == 0 ? null
            : $"The GM may keep a custom feature back unless the character has {Join(rules, ids, "or")}"
              + $"{Cite(entry.SourceRef)}.{Held(rules, sheet, ids)}";
    }

    /// <summary>" This character has Patron and Wealth." — or nothing, for a character with none of them.</summary>
    private static string Held(RulesRepository rules, CharacterSheet sheet, IReadOnlyList<string> ids)
    {
        var held = ids.Where(id => sheet.Perks.Any(p => p.PerkId == id)).ToList();

        return held.Count == 0 ? "" : $" This character has {Join(rules, held, "and")}.";
    }

    /// <summary>"Contacts, Patron, Resources and Wealth", by each Perk's printed name.</summary>
    private static string Join(RulesRepository rules, IReadOnlyList<string> ids, string conjunction)
    {
        var names = ids.Select(id => rules.GetPerk(id)?.Name ?? id).ToList();

        return names.Count == 1 ? names[0]
            : $"{string.Join(", ", names.Take(names.Count - 1))} {conjunction} {names[^1]}";
    }

    /// <summary>" (p.91)" from the entry's own <c>source_ref</c>, or nothing if it names no page.</summary>
    private static string Cite(string sourceRef)
    {
        var page = PageOf().Match(sourceRef);

        return page.Success ? $" (p.{page.Groups[1].Value})" : "";
    }

    [GeneratedRegex(@"\bp\.(\d+)\b")]
    private static partial Regex PageOf();
}
