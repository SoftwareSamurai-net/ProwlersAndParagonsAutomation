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

        // A Power whose own entry tells you to apply a named option overrides that option's
        // Range rule — and nothing else. Only the Power's printed text can put an id here
        // (see PowerModel.ProsAllowedByOwnText); until it could, T-Kay's printed
        // Force Field 12d (Zone) was refused by both editors and reported an error by the
        // validator, so a Hero in the rulebook could not be built in this tool.
        //
        // The first version of this sat above both checks and returned early, which exempted
        // the rank-type rule as well; and because this method takes the interface, a Con
        // sharing one of the ids would have been exempted by a field named for Pros. Neither
        // was reachable with the data as it stands, and neither was prevented.
        if (!AllowedByOwnText(option, power) &&
            option.AppliesToRanges.Count > 0 &&
            !string.Equals(power.Range, SpecialRange, StringComparison.Ordinal) &&
            !option.AppliesToRanges.Contains(power.Range, StringComparer.Ordinal))
            return false;

        return option.AppliesToRankTypes.Count == 0 ||
               option.AppliesToRankTypes.Contains(power.RankType, StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether this Power's own printed text names this option. Pros only: the field is named
    /// for Pros and a Con sharing an id must not ride along on it.
    /// </summary>
    private static bool AllowedByOwnText(IGenericProCon option, PowerModel power) =>
        option is ProModel &&
        power.ProsAllowedByOwnText.Any(a => string.Equals(a.Id, option.Id, StringComparison.Ordinal));

    /// <summary>
    /// The grades this Power may pick from for an option priced by grade — every grade the
    /// option prints, unless the Power's own text allows the option and names which apply.
    ///
    /// <para>This exists because the grades of Zone/Nova and Ranged encode a <em>Range</em>,
    /// and the Powers that reach those options through their own text have a Range the
    /// rulebook never prices. Force Field is Self: <c>zone_ranged</c> is +2 and
    /// <c>zone_touch</c> is +4, both were accepted, and T-Kay's printed character therefore
    /// costed two ways. The validator and both editors ask here so they cannot disagree.</para>
    /// </summary>
    public static IReadOnlyList<string> GradesFor(
        IGenericProCon option, PowerModel? power, IEnumerable<string> printedGrades)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(printedGrades);

        var all = printedGrades.ToList();
        if (power is null) return all;

        var allowance = power.ProsAllowedByOwnText
            .FirstOrDefault(a => option is ProModel &&
                                 string.Equals(a.Id, option.Id, StringComparison.Ordinal));

        if (allowance is null || allowance.Grades.Count == 0) return all;

        // Intersected rather than returned as recorded, so a grade the rules file names but
        // the option does not price cannot invent a key.
        return all.Where(k => allowance.Grades.Contains(k, StringComparer.Ordinal)).ToList();
    }

    /// <summary>Generic Pros that may be applied to this Power, in rules-file order.</summary>
    public IReadOnlyList<ProModel> ProsFor(PowerModel power) =>
        _rules.Pros.Where(p => IsApplicable(p, power)).ToList();

    /// <summary>Generic Cons that may be applied to this Power, in rules-file order.</summary>
    public IReadOnlyList<ConModel> ConsFor(PowerModel power) =>
        _rules.Cons.Where(c => IsApplicable(c, power)).ToList();
}
