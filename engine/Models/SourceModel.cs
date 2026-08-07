namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// One of the six Sources (Ch.2, p.15). Every Ability, Talent and Power has one, saying
/// what the Trait is meant to be: something the character was born with, a machine, a
/// spell, a trained skill.
///
/// <para>Mechanically the Source matters in one place: a Power with no rank of its own
/// uses a <em>default rank</em> in place of a rank whenever Powers act on other Powers —
/// Drain, Nullify, Dispel, Power Absorption, Power Mimicry. Which Ability supplies that
/// rank depends on the Source, and the split is even: Innate, Super and Tech use Toughness;
/// Magic, Psychic and Trained use Willpower.</para>
/// </summary>
public record SourceModel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// The Ability that stands in as a rankless Power's rank — "toughness" or "willpower".
    /// </summary>
    public string DefaultRankAbility { get; init; } = "";

    public string Description { get; init; } = "";
    public string? Notes { get; init; }
    public string SourceRef { get; init; } = "";
}
