using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// One owner and every sheet in the roster linked to them as a <see cref="CharacterVariant.Companion"/>,
/// with what Ch.2 p.55 says about the pair.
/// </summary>
/// <param name="OwnerId">The id the companions name. Also the key of <see cref="Owner"/> where it is present.</param>
/// <param name="Owner">The owner, or null where no member of the roster carries that id.</param>
/// <param name="Members">Every sheet linked to the owner as a companion, in roster order.</param>
/// <param name="Budget">
/// The Hero Points each companion was matched to — one Pet/Sidekick purchase's units times the
/// rate — keyed by companion id. Null where no purchase pays for it, or the owner is absent.
/// </param>
/// <param name="Spent">Each companion's own total, keyed by id. Null where the engine cannot price it.</param>
/// <param name="OwnResolve">The owner's Resolve as a character alone. Null with no owner or no tier.</param>
/// <param name="OwnerResolve">
/// The owner's Resolve with the companions counted: their higher Traits and their Flaws. Null
/// whenever <see cref="OwnResolve"/> is — the figure is the owner's, so it needs the owner.
/// </param>
/// <param name="Issues">What the pair breaks, in the same shape a single sheet's findings take.</param>
public sealed record CompanionFamily(
    string OwnerId,
    RosterEntry? Owner,
    IReadOnlyList<RosterEntry> Members,
    IReadOnlyDictionary<string, int?> Budget,
    IReadOnlyDictionary<string, int?> Spent,
    int? OwnResolve,
    int? OwnerResolve,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// The rules of a Pet/Sidekick companion (Ch.2 p.55) that need its owner and the companion at
/// once — the second set in this engine after <see cref="AlternateForms"/>, and built the same way.
///
/// <para><b>What p.55 says, and what each sentence becomes.</b> "Every Hero Point you spend on
/// this Perk gives you 10 Hero Points to create this character" is the companion's budget, and
/// <c>COMPANION_OVER_BUDGET</c> where it spends more; a companion no purchase pays for is
/// <c>COMPANION_NOT_PAID</c>, and a purchase no companion in the roster uses is
/// <c>PET_SIDEKICK_NO_COMPANION</c>. "They must abide by the game's Trait Cap" — the owner's — is
/// <c>COMPANION_CAP_NOT_OWNERS</c>. "If your companion has higher Trait ranks than you, use their
/// Traits when determining your Resolve", and "if your companion has Flaws, you earn Resolve as if
/// that character were a Hero", are <see cref="CompanionFamily.OwnerResolve"/>, which
/// <see cref="DerivedStatsCalculator"/> works out. "Can't have more than half your Hero Points"
/// needs only the owner, so <see cref="CharacterValidator"/> checks it as
/// <c>PET_SIDEKICK_OVER_HALF</c>; so does the refusal of Determination and of a companion's own
/// companion. Earning "1 Hero Point for every 2 that you earn" is not here: nothing in the app
/// tracks Hero Points earned in play.</para>
///
/// <para><b>One purchase pays for one companion</b> — the owner's ruling of 2026-10-29 — and a
/// purchase is matched to a companion the way <see cref="AlternateForms"/> matches a level to a
/// form: consumed when taken. The owner may hold several. Each companion, in roster order, takes
/// the smallest purchase still unspent that covers what it spent, or the largest when none does,
/// so the finding names the nearest miss. A sheet carries no index naming its purchase, so this
/// best fit is the only reading there is.</para>
///
/// <para><b>Reported, never repaired, and nothing on a single sheet moves.</b> A companion still
/// costs and validates as the character it is, with its tier's budget waived by
/// <see cref="CharacterValidator"/> because the real one is the owner's — the companion declares,
/// the family verifies.</para>
/// </summary>
public sealed class Companions
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;

    /// <summary>
    /// Whether <paramref name="sheet"/> is a companion: linked to an owner by a
    /// <see cref="CharacterVariant.Companion"/> link. Asked here, where the link's consequences
    /// live, so a one-sheet reader such as a printed sheet can say "spends its owner's Resolve"
    /// without reading the link itself.
    /// </summary>
    public static bool IsCompanion(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.Variant is { Kind: CharacterVariant.Companion } link && !string.IsNullOrWhiteSpace(link.OfCharacterId);
    }

    /// <summary>Whether <paramref name="sheet"/> holds a Perk that buys a companion — the Perk's own data says which.</summary>
    public static bool BuysACompanion(CharacterSheet sheet, RulesRepository rules)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(rules);
        return sheet.Perks.Any(p => p.PerkId is not null && rules.GetPerk(p.PerkId)?.Companion is not null);
    }

    /// <summary>
    /// A new companion for <paramref name="owner"/>, stored as <paramref name="ownerId"/>: empty but
    /// for its name, the link, and the owner's tier and house Trait Cap — p.55's "the game's Trait
    /// Cap" is the owner's, so it starts there rather than at a tier somebody picks. The palette is
    /// a host's to carry over; rules code may not read it.
    /// </summary>
    public static CharacterSheet Begin(CharacterSheet owner, string ownerId, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var companion = new CharacterSheet
        {
            Name    = name,
            Variant = new CharacterVariant(ownerId, CharacterVariant.Companion)
        };
        TakeOwnersTier(companion, owner);
        return companion;
    }

    /// <summary>Gives <paramref name="companion"/> its owner's tier and house Trait Cap — what <c>COMPANION_CAP_NOT_OWNERS</c> asks for.</summary>
    public static void TakeOwnersTier(CharacterSheet companion, CharacterSheet owner)
    {
        ArgumentNullException.ThrowIfNull(companion);
        ArgumentNullException.ThrowIfNull(owner);

        companion.SelectedTierId = owner.SelectedTierId;
        companion.TraitCapRank   = owner.TraitCapRank;
    }

    public Companions(RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        _rules   = rules   ?? throw new ArgumentNullException(nameof(rules));
        _costs   = costs   ?? throw new ArgumentNullException(nameof(costs));
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
    }

    /// <summary>
    /// Every owner the roster holds a companion question about: each id some member names through
    /// a <see cref="CharacterVariant.Companion"/> link, and each member holding a Perk with a
    /// companion rule, in the order the roster first mentions them. A roster with neither answers
    /// an empty list.
    /// </summary>
    /// <exception cref="ArgumentException">Two entries share an id.</exception>
    public IReadOnlyList<CompanionFamily> Families(IReadOnlyList<RosterEntry> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var byId = new Dictionary<string, RosterEntry>(StringComparer.Ordinal);
        foreach (var entry in roster)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (!byId.TryAdd(entry.Id, entry))
                throw new ArgumentException(
                    $"Two entries in the roster are called '{entry.Id}', so a link naming it could mean either.",
                    nameof(roster));
        }

        // Owners in the order the roster first mentions them, whether as a companion's link or as
        // the holder of the Perk.
        var order   = new List<string>();
        var members = new Dictionary<string, List<RosterEntry>>(StringComparer.Ordinal);

        foreach (var entry in roster)
        {
            if (Purchases(entry.Sheet).Count > 0 && !members.ContainsKey(entry.Id))
            {
                order.Add(entry.Id);
                members[entry.Id] = [];
            }

            if (entry.Sheet.Variant is not { Kind: CharacterVariant.Companion } link
                || string.IsNullOrWhiteSpace(link.OfCharacterId)) continue;

            if (!members.TryGetValue(link.OfCharacterId, out var list))
            {
                order.Add(link.OfCharacterId);
                members[link.OfCharacterId] = list = [];
            }

            list.Add(entry);
        }

        return [.. order.Select(id => Family(id, byId.GetValueOrDefault(id), members[id]))];
    }

    private CompanionFamily Family(string ownerId, RosterEntry? owner, IReadOnlyList<RosterEntry> companions)
    {
        var issues = new List<ValidationIssue>();
        var budget = new Dictionary<string, int?>(StringComparer.Ordinal);
        var spent  = new Dictionary<string, int?>(StringComparer.Ordinal);

        foreach (var companion in companions) spent[companion.Id] = TotalCost(companion.Sheet);

        if (owner is null)
        {
            foreach (var companion in companions)
            {
                budget[companion.Id] = null;
                issues.Add(new(ValidationSeverity.Warning, "COMPANION_OWNER_NOT_IN_ROSTER",
                    $"{Name(companion)} is a companion of '{ownerId}', which is not in this roster, so "
                    + "how many Hero Points it may spend, and its owner's Resolve with it, cannot be "
                    + "checked until it is.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = companion.Id,
                    OwnerId     = ownerId
                });
            }

            return new(ownerId, null, companions, budget, spent, null, null, issues);
        }

        // "Every Hero Point you spend on this Perk gives you 10 Hero Points to create this
        // character": one purchase, one companion, consumed when matched.
        var unspent = Purchases(owner.Sheet).ToList();

        foreach (var companion in companions)
        {
            var cost  = spent[companion.Id];
            var match = unspent.Where(p => cost is not { } c || p.Budget >= c).MinBy(p => p.Budget)
                        ?? unspent.MaxBy(p => p.Budget);

            if (match is null)
            {
                budget[companion.Id] = null;
                issues.Add(new(ValidationSeverity.Error, "COMPANION_NOT_PAID",
                    Purchases(owner.Sheet).Count == 0
                        ? $"{Name(companion)} is a companion of {Name(owner)}, who has not bought Pet/Sidekick. "
                          + "A companion is built on its owner's Pet/Sidekick, so the owner buys it or the link goes."
                        : $"{Name(companion)} is a companion of {Name(owner)}, and every Pet/Sidekick "
                          + $"{Name(owner)} has bought already pays for another companion. Each purchase pays "
                          + "for one, so the owner buys another or the link goes.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = companion.Id,
                    OwnerId     = owner.Id
                });
                continue;
            }

            unspent.Remove(match);
            budget[companion.Id] = match.Budget;

            if (cost is { } c && c > match.Budget)
                issues.Add(new(ValidationSeverity.Error, "COMPANION_OVER_BUDGET",
                    $"{Name(companion)} costs {c} Hero Points, and {Name(owner)}'s Pet/Sidekick at "
                    + $"{match.Units} gives it {match.Budget} (p.{match.Rule.Page}) — {c - match.Budget} over.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = companion.Id,
                    OwnerId     = owner.Id,
                    Value       = c,
                    Limit       = match.Budget
                });
        }

        // "They must abide by the game's Trait Cap": the owner's, whatever tier the companion
        // was saved at.
        var ownerCap = Cap(owner.Sheet);
        foreach (var companion in companions)
        {
            if (Cap(companion.Sheet) is not { } cap || ownerCap is null || cap == ownerCap) continue;

            issues.Add(new(ValidationSeverity.Error, "COMPANION_CAP_NOT_OWNERS",
                $"{Name(companion)} is built to a Trait Cap of {cap}d, and a companion abides by the "
                + $"game's — {Name(owner)}'s {ownerCap}d. Set its tier to {Name(owner)}'s.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = companion.Id,
                OwnerId     = owner.Id,
                Value       = cap,
                Limit       = ownerCap
            });
        }

        foreach (var left in unspent)
            issues.Add(new(ValidationSeverity.Warning, "PET_SIDEKICK_NO_COMPANION",
                $"{Name(owner)} has bought Pet/Sidekick at {left.Units}, worth {left.Budget} Hero Points "
                + "to a companion, and no companion in this roster uses it. Either the companion is not "
                + "in the roster or it has not been built.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = owner.Id,
                Value       = left.Budget
            });

        var own  = Resolve(owner.Sheet, []);
        var with = own is null ? null : Resolve(owner.Sheet, [.. companions.Select(c => c.Sheet)]);

        return new(ownerId, owner, companions, budget, spent, own, with, issues);
    }

    /// <summary>One purchase of a Perk carrying the companion rule, and the Hero Points it gives.</summary>
    private sealed record Purchase(int Units, int Budget, PerkCompanionRule Rule);

    private List<Purchase> Purchases(CharacterSheet sheet) =>
    [
        .. sheet.Perks
            .Select(p => (Selected: p, Rule: p.PerkId is null ? null : _rules.GetPerk(p.PerkId)?.Companion))
            .Where(p => p.Rule is not null)
            .Select(p => new Purchase(p.Selected.Units, checked((int)p.Rule!.HeroPointsFor(p.Selected.Units)), p.Rule))
    ];

    /// <summary>The companion's own total, or null where the engine declines to price it — already a finding on that sheet.</summary>
    private int? TotalCost(CharacterSheet sheet)
    {
        try { return _costs.TotalCost(sheet); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException
                                    or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private int? Cap(CharacterSheet sheet) =>
        sheet.SelectedTierId is { } id && _rules.GetTier(id) is { } tier
            ? DerivedStatsCalculator.EffectiveTraitCap(sheet, tier)
            : null;

    /// <summary>
    /// The owner's Resolve with <paramref name="companions"/> counted, or null where the owner has
    /// no resolvable tier: <see cref="DerivedStatsCalculator.CalculateResolve(CharacterSheet)"/>
    /// answers 0 there, and 0 is a real Resolve.
    /// </summary>
    private int? Resolve(CharacterSheet owner, IReadOnlyList<CharacterSheet> companions)
    {
        if (owner.SelectedTierId is null || _rules.GetTier(owner.SelectedTierId) is null) return null;

        try { return _derived.CalculateResolve(owner, companions); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    private static string Name(RosterEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Sheet.Name) ? $"'{entry.Id}'" : entry.Sheet.Name;
}
