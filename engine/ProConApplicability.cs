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
/// <para>One thing does come from the Power: a Power whose own printed text names a generic
/// option overrides that option's Range rule, through
/// <see cref="PowerModel.ProsAllowedByOwnText"/>. That is a record of a printed sentence,
/// not a curated list of suitable options, and the distinction is the whole reason the
/// removed lists are not creeping back.</para>
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

        // A Power whose own entry tells you to apply a named option overrides the option's
        // Range rule. Only the Power's printed text can put an id here — see
        // PowerModel.ProsAllowedByOwnText — and until it did, T-Kay's printed
        // Force Field 12d (Zone) was refused by both editors and reported an error by the
        // validator, so a Hero in the rulebook could not be built in this tool.
        if (power.ProsAllowedByOwnText.Contains(option.Id, StringComparer.Ordinal))
            return true;

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
