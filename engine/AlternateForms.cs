using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// One sheet as a roster knows it: the id a <see cref="CharacterVariant.OfCharacterId"/> would
/// name it by, and the sheet itself. The sheet does not carry its own id — the browser keeps it
/// in the storage envelope and a file on disk keeps it in its name — so whoever holds a roster
/// says what each member is called.
/// </summary>
public sealed record RosterEntry(string Id, CharacterSheet Sheet);

/// <summary>
/// One root character and every sheet in the roster that is an <see cref="CharacterVariant.AlternateForm"/>
/// of it, with the two things Ch.2's Alternate Form entry says about them as a set.
/// </summary>
/// <param name="RootId">The id the forms name. Also the key of <see cref="Root"/> where it is present.</param>
/// <param name="Root">The root, or null where no member of the roster carries that id.</param>
/// <param name="Forms">Every sheet linked to the root as an alternate form, in roster order.</param>
/// <param name="Resolve">
/// Each member's own Resolve as a separate character, keyed by id — null where the engine
/// cannot derive one (no tier, or a tier the rules do not have). The root is in here too.
/// </param>
/// <param name="SharedResolve">
/// The one pool every form draws on: the lowest of <see cref="Resolve"/>. Null when the root is
/// absent or any member's own figure is — a minimum over a set with a hole in it is a number
/// that looks like an answer and is not.
/// </param>
/// <param name="Issues">What the set breaks, in the same shape a single sheet's findings take.</param>
public sealed record AlternateFormFamily(
    string RootId,
    RosterEntry? Root,
    IReadOnlyList<RosterEntry> Forms,
    IReadOnlyDictionary<string, int?> Resolve,
    int? SharedResolve,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Item 21's slice two: the rule consequences of an <see cref="CharacterVariant.AlternateForm"/>
/// link, which are the only rules in this engine that need two sheets at once.
///
/// <para><b>What Ch.2 p.21 says, and what each sentence becomes here.</b> "Both forms must pay
/// for this Power" is <c>ALTERNATE_FORM_NOT_PAID</c> on any member without it. "Each form must
/// pay this Power's total cost" is <c>ALTERNATE_FORM_COST_DIFFERS</c> where a form's Hero Points
/// on the Power are not the root's. "Your alternate form can be of any power level up to but not
/// higher than yours" is <c>ALTERNATE_FORM_ABOVE_ROOT_LEVEL</c>. "This Power's cost varies
/// depending on your other form's power level" — the Units bought are the power level the form
/// is built at, Street Level being 1 and Iconic 6 — is <c>ALTERNATE_FORM_LEVEL_NOT_PAID</c>
/// where a form's tier is not one the root has paid a purchase for, matched one purchase to one
/// form and offering only the levels no earlier form has spent, and
/// <c>ALTERNATE_FORM_PAID_NOT_IN_ROSTER</c> on the root where a purchase is left over. "Buy this
/// Power multiple times if you want multiple forms" is why the entry is marked
/// <see cref="PowerModel.Repeatable"/>: a root paying twice is not a Power listed twice.
/// "You only have one pool of Resolve: use the lowest Resolve among your various forms" is
/// <see cref="AlternateFormFamily.SharedResolve"/>.</para>
///
/// <para><b>Reported, never repaired, and nothing on a single sheet moves.</b>
/// <see cref="CostCalculator"/> and <see cref="DerivedStatsCalculator"/> still cannot see the
/// link — <c>CharacterVariantTests.CostAndDerivedStatsNeverReadTheVariantField</c> keeps it so —
/// which means each form still costs and validates exactly as the character it is, and this
/// class only adds what is true of the set. A form whose Resolve is not the pool's is not
/// given the pool's; the pool is reported beside it.</para>
///
/// <para><b>One sentence of p.21 is deliberately not applied.</b> "Your other form's power
/// level only affects the number of Hero Points you have to create it, not its Trait Cap" —
/// so a Street Level form of a Standard Hero is built on 75 Hero Points and capped at 12d, not
/// 8d. The engine's one way to say a cap is <see cref="CharacterSheet.TraitCapRank"/>, and a
/// house cap above the tier's is <c>TRAIT_CAP_ABOVE_TIER</c> by the owner's own ruling on house
/// caps; applying this sentence means deciding which of two rulings gives way, and that is the
/// owner's to decide rather than this class's. Until then a form's own Resolve here is the
/// figure its own tier's cap gives, which can be lower than the book's — and so can the pool.</para>
///
/// <para><b>Who says what a sheet is called is the host.</b> A <see cref="CharacterVariant"/>
/// names an id, and a sheet does not carry one; a browser has its storage key and the headless
/// command has a file name. So the input is <see cref="RosterEntry"/> and not a bare sheet, and
/// a roster with two entries under one id is refused rather than guessed at.</para>
/// </summary>
public sealed class AlternateForms
{
    /// <summary>The entry in <c>powers.json</c> whose text this class applies.</summary>
    public const string PowerId = "alternate_form";

    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;

    public AlternateForms(RulesRepository rules, CostCalculator costs, DerivedStatsCalculator derived)
    {
        _rules   = rules   ?? throw new ArgumentNullException(nameof(rules));
        _costs   = costs   ?? throw new ArgumentNullException(nameof(costs));
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
    }

    /// <summary>
    /// The power level a tier is, as the Alternate Form table counts them: Street Level is 1,
    /// Iconic is 6, in the order <c>tiers.json</c> lists them — which is the order p.21's table
    /// prints, and a test holds the two to each other. Null for no tier or an unknown one.
    /// </summary>
    public int? PowerLevel(string? tierId)
    {
        if (tierId is null) return null;

        var tiers = _rules.Tiers;
        for (var i = 0; i < tiers.Count; i++)
            if (string.Equals(tiers[i].Id, tierId, StringComparison.Ordinal))
                return i + 1;

        return null;
    }

    /// <summary>
    /// Every alternate-form family the roster holds: one per root id that some member names
    /// through an <see cref="CharacterVariant.AlternateForm"/> link, in the order the roster
    /// first mentions it. A roster with no such link answers an empty list. Links of the other
    /// two kinds are not families and are not here.
    /// </summary>
    /// <exception cref="ArgumentException">Two entries share an id.</exception>
    public IReadOnlyList<AlternateFormFamily> Families(IReadOnlyList<RosterEntry> roster)
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

        var families = new List<AlternateFormFamily>();

        foreach (var group in roster
                     .Where(e => e.Sheet.Variant is { Kind: CharacterVariant.AlternateForm } v
                                 && !string.IsNullOrWhiteSpace(v.OfCharacterId))
                     .GroupBy(e => e.Sheet.Variant!.OfCharacterId, StringComparer.Ordinal))
        {
            byId.TryGetValue(group.Key, out var root);
            families.Add(Family(group.Key, root, [.. group]));
        }

        return families;
    }

    private AlternateFormFamily Family(string rootId, RosterEntry? root, IReadOnlyList<RosterEntry> forms)
    {
        var issues  = new List<ValidationIssue>();
        var resolve = new Dictionary<string, int?>(StringComparer.Ordinal);

        if (root is null)
        {
            foreach (var form in forms)
            {
                resolve[form.Id] = OwnResolve(form.Sheet);
                issues.Add(new(ValidationSeverity.Warning, "ALTERNATE_FORM_ROOT_NOT_IN_ROSTER",
                    $"{Name(form)} is an alternate form of '{rootId}', which is not in this roster, "
                    + "so whether the two forms pay for Alternate Form alike, and what Resolve they "
                    + "share, cannot be checked until it is.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = form.Id,
                    OwnerId     = rootId
                });
            }

            return new(rootId, null, forms, resolve, null, issues);
        }

        var members = new List<RosterEntry>(forms.Count + 1) { root };
        members.AddRange(forms);

        // "Both forms must pay for this Power."
        var unpaid = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            resolve[member.Id] = OwnResolve(member.Sheet);

            if (Purchases(member.Sheet).Count > 0) continue;

            unpaid.Add(member.Id);
            issues.Add(new(ValidationSeverity.Error, "ALTERNATE_FORM_NOT_PAID",
                $"{Name(member)} does not have the Alternate Form Power, and every form of a "
                + "character must pay for it — both the one it turns into and the one it turns "
                + "back from.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = member.Id,
                OwnerId     = root.Id
            });
        }

        // "Each form must pay this Power's total cost."
        var rootCost = PowerCost(root.Sheet);
        foreach (var form in forms)
        {
            if (unpaid.Contains(form.Id) || unpaid.Contains(root.Id) || rootCost is null) continue;

            var cost = PowerCost(form.Sheet);
            if (cost is null || cost == rootCost) continue;

            issues.Add(new(ValidationSeverity.Error, "ALTERNATE_FORM_COST_DIFFERS",
                $"{Name(form)} pays {cost} Hero Points for Alternate Form and {Name(root)} pays "
                + $"{rootCost}. Every form pays the Power's whole cost, so the two must match.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = form.Id,
                OwnerId     = root.Id,
                Value       = cost,
                Limit       = rootCost
            });
        }

        // "Any power level up to but not higher than yours", and the level the root paid for.
        var rootLevel  = PowerLevel(root.Sheet.SelectedTierId);
        var paidLevels = Purchases(root.Sheet).Select(p => p.Units).ToList();

        // The levels still on offer to a form: what the root has paid for and no earlier form
        // in the roster has already taken. Offering a level another form spent would be a
        // repair that, taken, leaves this finding standing — one purchase pays for one form.
        List<string> Unspent() =>
            [.. paidLevels.Select(TierAtLevel).Where(t => t is not null).Select(t => t!.Id)
                .Distinct(StringComparer.Ordinal)];

        foreach (var form in forms)
        {
            // A form with no tier, or one the rules do not have, is already NO_TIER_SELECTED
            // or UNKNOWN_TIER on its own report; there is no level to measure here.
            if (PowerLevel(form.Sheet.SelectedTierId) is not { } level) continue;

            if (rootLevel is { } ceiling && level > ceiling)
                issues.Add(new(ValidationSeverity.Error, "ALTERNATE_FORM_ABOVE_ROOT_LEVEL",
                    $"{Name(form)} is built at {TierName(form.Sheet)}, which is a higher power level "
                    + $"than {Name(root)}'s {TierName(root.Sheet)}. An alternate form can be any "
                    + "power level up to the character's own, and not above it.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = form.Id,
                    OwnerId     = root.Id,
                    Value       = level,
                    Limit       = ceiling
                });

            if (unpaid.Contains(root.Id)) continue;

            // One purchase pays for one form at one level, so a level is consumed when matched.
            if (paidLevels.Remove(level)) continue;

            issues.Add(new(ValidationSeverity.Error, "ALTERNATE_FORM_LEVEL_NOT_PAID",
                $"{Name(form)} is built at {TierName(form.Sheet)}, and {Name(root)} has not paid "
                + "for an alternate form at that power level. The Power's cost is set by the "
                + "form's power level, so either the form's tier or the level paid for has to change.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = form.Id,
                OwnerId     = root.Id,
                Value       = level,
                Options     = Unspent()
            });
        }

        foreach (var level in paidLevels)
            issues.Add(new(ValidationSeverity.Warning, "ALTERNATE_FORM_PAID_NOT_IN_ROSTER",
                $"{Name(root)} pays for an alternate form at {LevelName(level)} and no form in "
                + "this roster is built at that power level. Either the form is not in the roster "
                + "or the purchase is for a form that does not exist.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = root.Id,
                Value       = level
            });

        // "Use the lowest Resolve among your various forms as your actual Resolve."
        int? shared = null;
        if (members.TrueForAll(m => resolve[m.Id] is not null))
            shared = members.Min(m => resolve[m.Id]!.Value);

        return new(rootId, root, forms, resolve, shared, issues);
    }

    private static IReadOnlyList<SelectedPower> Purchases(CharacterSheet sheet) =>
        [.. sheet.SelectedPowers.Where(p => string.Equals(p.PowerId, PowerId, StringComparison.Ordinal))];

    /// <summary>
    /// What this sheet pays for the Power across every purchase of it — the engine's figure, with
    /// the sheet's own house price forwarded as <see cref="CostCalculator.TotalPowersCost"/> does.
    /// Null where the engine declines to price one, which is already a finding on that sheet.
    /// </summary>
    private int? PowerCost(CharacterSheet sheet)
    {
        try
        {
            return checked(Purchases(sheet).Sum(p => _costs.PowerCost(p, sheet.ImmortalityCost)));
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException
                                    or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// <see cref="DerivedStatsCalculator.CalculateResolve"/> answers 0 for a sheet with no
    /// resolvable tier, and 0 is a real Resolve — so the tier is checked first, and a figure
    /// the engine could not derive is null rather than the lowest number there is.
    /// </summary>
    private int? OwnResolve(CharacterSheet sheet)
    {
        if (sheet.SelectedTierId is null || _rules.GetTier(sheet.SelectedTierId) is null) return null;

        try { return _derived.CalculateResolve(sheet); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    private TierModel? TierAtLevel(int level) =>
        level >= 1 && level <= _rules.Tiers.Count ? _rules.Tiers[level - 1] : null;

    private string LevelName(int level) =>
        TierAtLevel(level) is { } tier ? tier.Name : $"power level {level}, which no tier is";

    private string TierName(CharacterSheet sheet) =>
        (sheet.SelectedTierId is { } id ? _rules.GetTier(id)?.Name : null) ?? "no tier";

    private static string Name(RosterEntry entry) =>
        string.IsNullOrWhiteSpace(entry.Sheet.Name) ? $"'{entry.Id}'" : entry.Sheet.Name;
}
