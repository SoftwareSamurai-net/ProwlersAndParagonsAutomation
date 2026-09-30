using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Groups a character's Powers — and any Abilities and Talents that do not take their
/// default Source — under Source headings the way the published sheets print them:
/// <c>TECH POWERS</c>, <c>MAGIC POWERS</c>, and so on.
///
/// <para>This lives in the engine rather than in a renderer because every surface needs the
/// same answer: the text sheet, the JSON export, the wizard's review step, and the browser's
/// sheet. Only the styling differs between a Hero sheet and a Villain one — Ch.9 builds
/// Villains exactly like Heroes and prints no separate stat-block format.</para>
///
/// <para><b>The Abilities block itself carries no Source marking</b>, and that is the
/// printed layout rather than an omission. A sheet records a Trait's Source as an
/// <c>Abilities (Might, Toughness)</c> line <em>inside</em> the relevant Power group —
/// Stronghold's four armoured Abilities sit under <c>TECH POWERS</c> — so the Abilities and
/// Talents tables stay plain lists of ranks.</para>
/// </summary>
public sealed class SourceGrouping
{
    private readonly RulesRepository _rules;

    public SourceGrouping(RulesRepository rules) => _rules = rules;

    /// <summary>
    /// The Source an Ability takes when the sheet records none (Ch.2, p.16: "Abilities are
    /// usually Innate"). A default is never printed — the sheets list only the exceptions.
    /// </summary>
    public const string DefaultAbilitySourceId = "innate";

    /// <summary>The Source a Talent takes when the sheet records none — "Talents are usually Trained".</summary>
    public const string DefaultTalentSourceId = "trained";

    /// <summary>
    /// One Source heading on a printed sheet, and what goes beneath it: the
    /// <c>Abilities (…)</c> / <c>Talents (…)</c> lines first, as the sheets print them,
    /// then the Powers.
    /// </summary>
    public sealed record Group(
        string Heading,
        SourceModel? Source,
        IReadOnlyList<string> TraitLines,
        IReadOnlyList<SelectedPower> Powers);

    /// <summary>
    /// Heading a published sheet prints for a Source: "TECH POWERS", "MAGIC POWERS".
    /// </summary>
    public static string HeadingFor(SourceModel? source) =>
        source is null ? "POWERS" : $"{source.Name.ToUpperInvariant()} POWERS";

    /// <summary>
    /// The character's Powers and non-default Trait Sources, grouped by Source.
    ///
    /// <para>Groups follow sources.json order so a sheet is stable between runs rather than
    /// reordering itself as Powers are added. Powers with no Source recorded collect under
    /// a plain "POWERS" heading at the end instead of being dropped — the validator already
    /// warns about them, and silently hiding a Power from its own sheet would be worse.
    /// Traits are not treated that way: a Trait with no Source is not unsourced, it is on
    /// its default, so it contributes no line at all.</para>
    /// </summary>
    public IReadOnlyList<Group> GroupBySource(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = _rules.Sources
            .Select(s => new Group(
                HeadingFor(s), s,
                TraitLines(sheet, s.Id),
                sheet.SelectedPowers.Where(p => p.SourceId == s.Id).ToList()))
            .Where(g => g.Powers.Count > 0 || g.TraitLines.Count > 0)
            .ToList();

        var unsourced = sheet.SelectedPowers
            .Where(p => p.SourceId is null || _rules.GetSource(p.SourceId) is null)
            .ToList();

        // Traits naming a Source the rulebook does not have, for the same reason: the
        // validator errors, but a Trait the player marked and no surface ever mentions again
        // is worse than one printed under a heading that admits it has no Source. Only
        // reachable from stored or hand-edited data, since neither editor offers a bad id.
        var unsourcedTraits = UnknownSourceTraitLines(sheet);

        if (unsourced.Count > 0 || unsourcedTraits.Count > 0)
            groups.Add(new Group(HeadingFor(null), null, unsourcedTraits, unsourced));

        return groups;
    }

    /// <summary>
    /// The <c>Abilities (…)</c> and <c>Talents (…)</c> lines a sheet prints under one Source
    /// heading, in the rulebook's Trait order.
    ///
    /// <para>Three shapes, all taken from the printed sheets. Every Ability and every Talent
    /// on the one Source collapses to <c>Abilities and Talents (All)</c>, which is how the
    /// two Heralds and Nano are printed. A whole block on its own reads <c>(All)</c>.
    /// Otherwise the Traits are named: <c>Abilities (Might, Toughness)</c>.</para>
    ///
    /// <para>Abilities are split by the Pros and Cons applied to them, because the marking on
    /// the printed line belongs to the whole line — Stronghold's reads <c>Abilities (Agility,
    /// Might, Perception, Toughness) (Item: armor)</c>, one Con covering all four. Two
    /// Abilities on the same Source with different Cons are therefore two lines rather than
    /// one line with a modifier that does not apply to all of it. The engine has nowhere to
    /// keep the "armor" half of that label, so it prints the Con's name alone.</para>
    /// </summary>
    private List<string> TraitLines(CharacterSheet sheet, string sourceId)
    {
        // A Trait recorded on its own default is filtered here, not only by the editors.
        // The sheets print the exceptions, and "explicitly Innate" and "Innate by default"
        // are the same statement — there is no way to tell them apart on paper. Doing it in
        // the engine means a hand-edited or restored character cannot print a line that says
        // nothing, which is not something the editors can promise on their own.
        var abilities = _rules.Abilities
            .Where(a => Recorded(sheet.AbilitySources, a.Id, DefaultAbilitySourceId) == sourceId)
            .ToList();

        var talents = _rules.Talents
            .Where(t => Recorded(sheet.TalentSources, t.Id, DefaultTalentSourceId) == sourceId)
            .ToList();

        if (abilities.Count == 0 && talents.Count == 0) return [];

        var allTalents = talents.Count == _rules.Talents.Count;

        // "Abilities and Talents (All)" — the whole character on one Source, which is how
        // a sheet prints a wholly magical or wholly robotic Hero.
        if (abilities.Count == _rules.Abilities.Count && allTalents
            && abilities.All(a => ModifierLabel(sheet, a.Id) is null))
            return ["Abilities and Talents (All)"];

        var lines = new List<string>();

        foreach (var block in abilities.GroupBy(a => ModifierLabel(sheet, a.Id)))
        {
            // "(All)" is a claim about this line, so it is counted on the block and not on
            // the Source. Counting the Source printed "Abilities (All)" on a line naming
            // four of the six, whenever a Con split the rest onto a line of their own.
            var named = block.Count() == _rules.Abilities.Count
                ? "All"
                : string.Join(", ", block.Select(a => a.Name));

            lines.Add($"Abilities ({named})" + (block.Key is null ? "" : $" ({block.Key})"));
        }

        if (talents.Count > 0)
            lines.Add($"Talents ({(allTalents ? "All" : string.Join(", ", talents.Select(t => t.Name)))})");

        return lines;
    }

    /// <summary>
    /// Trait lines for Traits naming a Source that is not one of the six, printed under the
    /// plain <c>POWERS</c> heading beside any Power in the same state. Empty for every
    /// character either editor can produce.
    /// </summary>
    private List<string> UnknownSourceTraitLines(CharacterSheet sheet)
    {
        var unknown = _rules.Abilities
            .Where(a => IsUnknown(sheet.AbilitySources, a.Id))
            .Select(a => a.Name)
            .ToList();

        var unknownTalents = _rules.Talents
            .Where(t => IsUnknown(sheet.TalentSources, t.Id))
            .Select(t => t.Name)
            .ToList();

        var lines = new List<string>();
        if (unknown.Count > 0)        lines.Add($"Abilities ({string.Join(", ", unknown)})");
        if (unknownTalents.Count > 0) lines.Add($"Talents ({string.Join(", ", unknownTalents)})");
        return lines;

        // Whitespace counts as blank, not as an unknown Source — the same reading Recorded
        // and the validator take. Splitting on IsNullOrEmpty here and IsNullOrWhiteSpace
        // there put "  " in the unsourced group while every other surface called it a default.
        bool IsUnknown(Dictionary<string, string> sources, string traitId) =>
            !string.IsNullOrWhiteSpace(sources.GetValueOrDefault(traitId))
            && _rules.GetSource(sources[traitId]) is null;
    }

    /// <summary>
    /// The Source a sheet records for a Trait, or null when it records nothing the sheet
    /// would print under a Source heading — no entry, an entry equal to the default, or a
    /// blank one. A blank is not something the editors write; it comes from hand-edited or
    /// stale stored data, and the validator reports it separately.
    /// </summary>
    private static string? Recorded(
        Dictionary<string, string> sources, string traitId, string defaultSourceId)
    {
        var recorded = sources.GetValueOrDefault(traitId);

        // Whitespace as well as empty, matching the validator: a value that names nothing is
        // "on the default" on every surface, not a default here and an unknown Source there.
        return string.IsNullOrWhiteSpace(recorded) || recorded == defaultSourceId ? null : recorded;
    }

    /// <summary>
    /// The Pros and Cons applied to one Ability, as the printed line labels them, or null if
    /// it carries none. Names rather than ids: the line is read by a player holding a book.
    /// </summary>
    private string? ModifierLabel(CharacterSheet sheet, string abilityId)
    {
        var mods = sheet.AbilityModifiers.GetValueOrDefault(abilityId);
        if (mods is null || mods.Count == 0) return null;

        // `Item: armor` where the player wrote what the item is, `Item` where they did not —
        // which is the book's own shape for Stronghold's line, and was this project's recorded
        // gap while the selection had no words on it.
        return string.Join(", ", mods.Select(m =>
        {
            var name = _rules.GetCon(m.Id)?.Name ?? _rules.GetPro(m.Id)?.Name ?? m.Id;
            return string.IsNullOrWhiteSpace(m.Detail) ? name : $"{name}: {m.Detail.Trim()}";
        }));
    }

    /// <summary>
    /// The Source a Trait actually has, default included — what the character <em>is</em>,
    /// rather than what the sheet prints. Nothing mechanical reads this: the default rank
    /// rule is about Powers only. The JSON export writes it beside the recorded value,
    /// because "nothing was recorded" and "this is what it is" are different answers and a
    /// rebuild needs the first one.
    ///
    /// <para>It goes through <see cref="Recorded"/> so it cannot disagree with what the
    /// sheet prints. Reading the dictionary directly, a stored blank exported
    /// <c>effective_source: ""</c> while the same document's group structure showed the
    /// Trait on its default — one JSON file contradicting itself.</para>
    /// </summary>
    public string EffectiveAbilitySource(CharacterSheet sheet, string abilityId)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Recorded(sheet.AbilitySources, abilityId, DefaultAbilitySourceId)
               ?? DefaultAbilitySourceId;
    }

    /// <inheritdoc cref="EffectiveAbilitySource"/>
    public string EffectiveTalentSource(CharacterSheet sheet, string talentId)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Recorded(sheet.TalentSources, talentId, DefaultTalentSourceId)
               ?? DefaultTalentSourceId;
    }
}
