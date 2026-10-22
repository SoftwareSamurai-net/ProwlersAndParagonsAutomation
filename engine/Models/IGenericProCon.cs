namespace ProwlersAndParagonsAutomation.Engine.Models;

/// <summary>
/// What a generic Pro and a generic Con have in common: an identity and the rulebook's
/// statement of which Powers it can be applied to.
///
/// <para>The rulebook does not list suitable Pros and Cons inside each Power. It states
/// applicability the other way round, in the option's own entry — "This Pro applies to Zone
/// Powers", "applies to Powers that only affect you". So applicability is derived from the
/// option, not curated per Power.</para>
/// </summary>
public interface IGenericProCon
{
    string Id { get; }
    string Name { get; }

    /// <summary>
    /// What the entry asks the player to write on taking it, or null where it asks nothing.
    /// The answer is <c>SelectedProCon.Detail</c>.
    /// </summary>
    string? NarrativeConstraint { get; }

    /// <summary>
    /// How the option is priced: "flat" for the great majority, "flat_variable" for a graded
    /// one, and <b>"special" for Overkill and Weak alone</b> — the two that change a Power's
    /// rate per rank rather than its total, which is why neither prints a figure and why
    /// <see cref="ProwlersAndParagonsAutomation.Engine.CostCalculator"/> answers 0 for either
    /// on anything that has no rank.
    /// </summary>
    string CostType { get; }

    /// <summary>
    /// Base Power Ranges this option can be applied to (Ch.2 p.19: Self, Touch, Ranged,
    /// Zone, Special). Empty means the entry states no range constraint and it applies to
    /// any Power.
    /// </summary>
    IReadOnlyList<string> AppliesToRanges { get; }

    /// <summary>
    /// Rank types this option can be applied to (power, baseline, default, special).
    /// Empty means no rank-type constraint. Only Degrades uses this.
    /// </summary>
    IReadOnlyList<string> AppliesToRankTypes { get; }

    /// <summary>
    /// True when the option's own entry says it may be taken more than once on the same
    /// Power, each copy charged again. Affect Inanimate is the generic case: "You can apply
    /// this Pro multiple times to affect different types of inanimate beings" (Ch.2 p.48).
    /// A repeat of anything else is a second discount for one thing and is refused.
    /// </summary>
    bool Repeatable { get; }

    /// <summary>
    /// The constraint the entry states but which cannot be checked against the data —
    /// "Powers that inflict physical or energy damage", "Powers that can be activated and
    /// deactivated at will". Shown to the player as a caveat rather than enforced, because
    /// deciding it per Power would mean inventing data the rulebook does not give.
    /// Null when the entry states no such constraint.
    /// </summary>
    string? ApplicabilityCaveat { get; }
}
