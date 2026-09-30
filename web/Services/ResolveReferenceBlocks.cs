using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>Which of the reference page's three sections an entry is shown under.</summary>
public enum ResolveBlock
{
    /// <summary>Starting Resolve, earning it, and what a Hero spends it on.</summary>
    Players,

    /// <summary>The Adversity pool, earning Adversity, and the GM's spends.</summary>
    Gm,

    /// <summary>A rule stated to bind both economies, rather than belonging to either alone.</summary>
    TableLimits,
}

/// <summary>
/// Decides which of the reference page's three blocks each entry lands in, in the one place the
/// task asked for it decided — so <c>ResolveReferenceTests</c> can hold every one of the
/// 28 entries to landing in exactly one block, and a 29th entry added later has nowhere to fall
/// silently through.
///
/// <para><b>Split by <c>who</c> and id prefix, not by <c>kind</c>.</b> <c>kind</c> answers "is
/// this a formula, a scalar or narrative prose", which cuts across the Hero/GM line — a formula
/// and a narrative entry sit on the same side of it as often as not. <c>who</c> is transcribed
/// per <c>docs/guide/play-rules.md</c>'s note that every spend names who holds the pool it
/// spends from, and it is missing on the sixteen entries that are not a spend at all, which is
/// where the id prefix carries the rest of the decision.</para>
/// </summary>
public static class ResolveReferenceBlocks
{
    /// <summary>
    /// <c>reroll_floor</c> is the one entry the book states about both economies at once — its
    /// own text says a GM's pool can do everything a Hero's can, so the floor it states binds
    /// both. Named rather than matched on any field, because nothing on the entry itself marks
    /// it as binding both; the reading is this page's, the same way an <c>interpretation</c>
    /// object elsewhere in this store is.
    /// </summary>
    private const string TableLimitsEntryId = "reroll_floor";

    /// <summary>
    /// Every id under this prefix is the GM's side of the ledger — the seven <c>adversity_*</c>
    /// entries, three of which carry no <c>who</c> at all because they are not a spend.
    /// </summary>
    private const string GmIdPrefix = "adversity_";

    public static ResolveBlock BlockFor(ResolveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.Equals(entry.Id, TableLimitsEntryId, StringComparison.Ordinal)) return ResolveBlock.TableLimits;

        if (string.Equals(entry.Who, "gm", StringComparison.Ordinal)) return ResolveBlock.Gm;
        if (entry.Id.StartsWith(GmIdPrefix, StringComparison.Ordinal)) return ResolveBlock.Gm;

        // Everything else is the Hero's — every who: "hero" spend, and the sixteen entries with
        // no who field at all that are not reroll_floor or adversity_*: Starting Resolve, the
        // exceptions to it, the six ways of earning, and the two section-opening narratives.
        return ResolveBlock.Players;
    }

    /// <summary>
    /// The printed page a citation reads, out of the entry's own <c>source_ref</c> — the same
    /// string every other play rules file carries, "Ultimate Edition, Ch.5 Resolve and
    /// Adversity, p.84". Every entry shown on the reference page cites its page from this, so a
    /// row with a source_ref this cannot parse is a fact worth surfacing rather than swallowing.
    /// </summary>
    public static string PrintedPage(string sourceRef)
    {
        ArgumentNullException.ThrowIfNull(sourceRef);

        var match = Regex.Match(sourceRef, @"p\.(\d+)(?:[-–]\d+)?\s*$");

        return match.Success ? $"p.{match.Groups[1].Value}" : sourceRef;
    }

    /// <summary>
    /// The cost or effect in plain words, where the entry's own data states one plainly enough
    /// to say in a phrase — never the description, which is prose for a reader with time to read
    /// it. Null for the five entries with no fact field this can turn into a figure: the two
    /// section-opening overviews (<c>resolve_earning_overview</c>, <c>resolve_spending_overview</c>),
    /// the exceptions list (<c>resolve_exceptions</c>), and the two spends that defer to Chapter 4
    /// or to whatever Power is being imitated (<c>spend_combat</c>, <c>spend_using_powers</c>).
    /// Null too for the two rules that carry no figure, <c>carryover</c> and
    /// <c>boost_and_shapeshifting_count_at_maximum</c>, whose descriptions say them. Also null, conditionally, for the handful of other cases below whose own data object is
    /// absent on a given entry — this switch's <c>_ => null</c> default is not the only route
    /// to a null result, only the one this list counts.
    /// </summary>
    public static string? CostWords(ResolveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Id switch
        {
            "starting_resolve" =>
                entry.StartingResolve is { } s ? $"{s.ResolvePerRankBelowCap} Resolve per rank below the Trait Cap" : null,

            // A rule with no figure: the entry's own description says it, and a sentence written
            // here would be web/ stating a rule the data does not carry.
            "boost_and_shapeshifting_count_at_maximum" => null,

            "carryover" => null,

            "earn_defeat" => Award(entry.Earning, "once per battle"),
            "earn_flaw" => Award(entry.Earning, null),
            "earn_interlude" => "amount not stated",
            "earn_motivation" => Award(entry.Earning, null),
            "earn_roleplaying" => Award(entry.Earning, null),
            "earn_sacrifice" => Award(entry.Earning, "then unconscious until the end of the scene"),

            "spend_assisting_allies" => entry.Interpretation?.InferredCostPerPointShared is { } par
                ? $"{par} Resolve per point shared"
                  + (entry.Spend?.CostPerPointSharedWhenUnableToAssist is { } unable
                      ? $" ({unable} per point if unable to assist)"
                      : "")
                : null,
            "spend_challenge_roll_dice" => Cost(entry.Spend, "per extra die"),
            "spend_reroll_challenge_roll" => Cost(entry.Spend, "to reroll the whole challenge roll"),
            "spend_reroll_other_roll" => Cost(entry.Spend, "to reroll any other roll"),
            "spend_lucky_break" => Cost(entry.Spend, "for a minor invented detail"),
            "spend_power_stunt" => Cost(entry.Spend, "to imitate another Power for a moment"),

            "reroll_floor" => "a spend may never leave the roll worse than it started",

            "adversity_pool" => entry.Adversity is { } pool ? $"{pool.PointsPerHeroPerIssue} Adversity per Hero, per issue" : null,
            "adversity_earn_challenge_level" => "Challenge Level × number of Heroes, at the start of the scene",
            "adversity_earn_unheroic_action" => entry.UnheroicAction is { } u ? $"+{u.AwardAdversity} Adversity, awarded immediately" : null,

            "adversity_spend_anything_resolve_can" => "anything Resolve can buy, on any NPC",
            "adversity_spend_suppress_flaw" => AdversityCost(entry.Spend, "once per character per issue"),
            "adversity_spend_misfortune" => AdversityCost(entry.Spend, null),
            "adversity_spend_villainy" => AdversityCost(entry.Spend, "once per story"),

            _ => null,
        };
    }

    private static string? Award(Earning? earning, string? suffix) =>
        earning?.AwardResolve is { } n ? Join($"+{n} Resolve", suffix) : null;

    private static string? Cost(Spend? spend, string suffix) =>
        spend?.CostResolve is { } n ? $"{n} Resolve {suffix}" : null;

    private static string? AdversityCost(Spend? spend, string? suffix) =>
        spend?.CostAdversity is { } n ? Join($"{n} Adversity", suffix) : null;

    private static string Join(string head, string? suffix) =>
        suffix is null ? head : $"{head}, {suffix}";
}
