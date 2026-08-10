using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Decides which generic Pros and Cons may be applied to a Power.
///
/// <para>The rulebook never lists suitable options inside a Power's entry. It states
/// applicability inside each option — "This Pro applies to Zone Powers", "applies to Powers
/// that only affect you", "applies to Power Rank Powers and Baseline Rank Powers". So the
/// answer is derived from the option, and there is nothing to curate per Power.</para>
///
/// <para>Powers used to carry hand-written <c>available_pros</c> / <c>available_cons</c>
/// lists instead. Those were this project's guesses, and the wizard filtered on them
/// absolutely: 68 of the 141 Powers offered no generic Pro at all, and six Self-range
/// Powers offered the Ranged Pro, which its own text does not permit.</para>
///
/// <para>Only constraints the rulebook prints for every Power — its Range and its Rank
/// type — are enforced. The other constraints options state ("Powers that inflict physical
/// or energy damage") would need per-Power judgements the rulebook does not supply, so they
/// travel as a caveat on the option and the player and GM decide. That matches the book,
/// which calls this list "not intended to cover every possible option" and puts everything
/// under the GM's approval.</para>
/// </summary>
public sealed class ProConApplicability
{
    private readonly RulesRepository _rules;

    public ProConApplicability(RulesRepository rules) => _rules = rules;

    /// <summary>
    /// A Range of Special means the Power works in a way its own description defines (p.19), so
    /// nothing can be ruled out for it and every option stays on offer.
    /// </summary>
    private const string SpecialRange = "special";

    /// <summary>
    /// True unless the option's own entry rules this Power out on Range or Rank type.
    /// An option that states no such constraint applies to every Power.
    /// </summary>
    public static bool IsApplicable(IGenericProCon option, PowerModel power)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(power);

        if (option.AppliesToRanges.Count > 0 &&
            !string.Equals(power.Range, SpecialRange, StringComparison.Ordinal) &&
            !option.AppliesToRanges.Contains(power.Range, StringComparer.Ordinal))
            return false;

        return option.AppliesToRankTypes.Count == 0 ||
               option.AppliesToRankTypes.Contains(power.RankType, StringComparer.Ordinal);
    }

    /// <summary>Generic Pros that may be applied to this Power, in rules-file order.</summary>
    public IReadOnlyList<ProModel> ProsFor(PowerModel power) =>
        _rules.Pros.Where(p => IsApplicable(p, power)).ToList();

    /// <summary>Generic Cons that may be applied to this Power, in rules-file order.</summary>
    public IReadOnlyList<ConModel> ConsFor(PowerModel power) =>
        _rules.Cons.Where(c => IsApplicable(c, power)).ToList();
}
