using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>Which way a field moved.</summary>
public enum DiffKind
{
    /// <summary>The field had a value and has a different one.</summary>
    Changed,

    /// <summary>The field, Power, Perk or Flaw was not there and now is.</summary>
    Added,

    /// <summary>It was there and now is not.</summary>
    Removed,
}

/// <summary>
/// One line of the diff, in the rulebook's words.
/// </summary>
/// <param name="What">
/// What moved, named the way the book names it — <c>Tier</c>, <c>Might</c>, <c>Flight</c>. Never a
/// field name from a stored payload: a screen that printed <c>selectedTierId</c> would be the app
/// describing its own internals, which is exactly the fault the rules reference's passage counts
/// were taken off a panel for.
/// </param>
/// <param name="Before">What it was, or null when it was not there at all.</param>
/// <param name="After">What it is, or null when it has gone.</param>
/// <param name="Kind">Which way it moved.</param>
public sealed record DiffRow(string What, string? Before, string? After, DiffKind Kind);

/// <summary>
/// What changed between the campaign's clone and the snapshot waiting for a decision.
/// </summary>
/// <param name="SpentBefore">
/// The clone's spend, or null when the engine refuses to price it — a variable-cost Power with no
/// variant is a question it will not guess at, and the honest answer is no figure.
/// </param>
/// <param name="SpentAfter">The snapshot's spend, on the same terms.</param>
/// <param name="Budget">
/// The tier's Hero Point budget for the snapshot, or null when it names no tier or builds without
/// a limit. Reported beside the spend, never enforced here.
/// </param>
/// <param name="Rows">Every field that moved, in a stable order.</param>
/// <param name="Compared">
/// How many fields were <em>examined</em>, whether or not they moved.
///
/// <para><b>This is the positive control, and it is not decoration.</b> A diff showing nothing is
/// indistinguishable from a diff that failed to run, and "nothing changed" is the commonest
/// honest answer this screen gives — so the screen says how much was looked at. A comparison that
/// has stopped comparing reports zero, which is a red test and a visibly wrong sentence rather
/// than a reassuring empty list.</para>
/// </param>
public sealed record CharacterDiff(
    int? SpentBefore,
    int? SpentAfter,
    int? Budget,
    IReadOnlyList<DiffRow> Rows,
    int Compared)
{
    /// <summary>Whether anything moved at all.</summary>
    public bool Unchanged => Rows.Count == 0;

    /// <summary>
    /// Whether the comparison actually ran. False means the screen must say so rather than
    /// drawing an empty list, which would read as agreement.
    /// </summary>
    public bool Ran => Compared > 0;
}

/// <summary>
/// The field-level diff a GM decides on, computed from two sheets.
///
/// <para><b>Led by the spend, because that is what a GM decides about.</b> <c>125 → 138 Hero
/// Points</c> is the first thing on the screen, and both figures are
/// <see cref="CostCalculator"/>'s answers for the two sheets — not a number read out of a payload
/// and not a difference this class works out. Everything below it is the detail behind that
/// figure.</para>
///
/// <para><b>In <c>web/</c> and not in <c>engine/</c>, for the reason <see cref="CampaignJoin"/> is
/// here.</b> The engine prices and judges one character; this compares two and names what moved
/// the way the book names it, which needs a <see cref="RulesRepository"/> to turn an id into a
/// name and needs no rule at all. It decides nothing about either character and repairs
/// nothing — an illegal character is reported, never repaired, and that applies twice as hard to
/// somebody else's.</para>
///
/// <para><b>Never a raw payload field.</b> Every row's name comes from the rules data or is a word
/// the app already prints — Tier, Package, Name, and the Abilities, Talents, Powers, Perks and
/// Flaws by their own names. <c>CampaignDiffTests</c> holds it to that against the property names
/// of <see cref="CharacterSheet"/> itself, so a field added later cannot leak its own spelling
/// onto the screen by being appended to a loop.</para>
///
/// <para><b>Whole snapshots are accepted or rejected, so this is a report and not a merge.</b>
/// There is deliberately no way to apply one row: partial application is a merge algorithm for
/// characters — a second engine, capable of producing a sheet neither person authored.</para>
/// </summary>
public static class CampaignDiff
{
    /// <summary>
    /// Compare the campaign's clone against the snapshot waiting for a decision.
    /// </summary>
    /// <param name="before">
    /// The clone, or null before the first approval — in which case every field of
    /// <paramref name="after"/> that has a value is an addition, which is the honest reading of a
    /// first submission.
    /// </param>
    /// <param name="after">The snapshot. Required: there is nothing to decide about without one.</param>
    /// <param name="rules">The rules, for turning an id into the name the book prints.</param>
    /// <param name="costs">The one authority on what either sheet costs.</param>
    public static CharacterDiff Between(
        CharacterSheet? before, CharacterSheet after, RulesRepository rules, CostCalculator costs)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(costs);

        var rows = new List<DiffRow>();
        var compared = 0;

        // The two figures the screen is led by. Both are the engine's, for the two sheets, and
        // neither is derived from the other — a difference computed here would be a third number
        // nobody asked the engine for.
        var spentBefore = before is null ? null : CharacterSession.TryCost(() => costs.TotalCost(before));
        var spentAfter = CharacterSession.TryCost(() => costs.TotalCost(after));

        var empty = new CharacterSheet();
        var was = before ?? empty;

        // ── The three plain fields ───────────────────────────────────────────────────
        //
        // Named the way the app already names them. `Tier` and `Package` are looked up so a row
        // reads `Standard → High Level` rather than `standard → high_level`: an id is not what a
        // tier is called, which is the rule `CampaignFinding` already follows for the same pair.

        compared += Compare(rows, "Name", Blank(was.Name), Blank(after.Name));
        compared += Compare(rows, "Tier", TierName(rules, was.SelectedTierId), TierName(rules, after.SelectedTierId));
        compared += Compare(rows, "Package", PackageName(rules, was.SelectedPackageId), PackageName(rules, after.SelectedPackageId));

        // ── Ranks ────────────────────────────────────────────────────────────────────
        //
        // `6d → 8d`, which is how a rank is written everywhere else in this app and on the sheet.

        compared += CompareRanks(rows, was.AbilityRanks, after.AbilityRanks,
            id => rules.GetAbility(id)?.Name ?? id);

        compared += CompareRanks(rows, was.TalentRanks, after.TalentRanks,
            id => rules.GetTalent(id)?.Name ?? id);

        // ── The chosen lists ─────────────────────────────────────────────────────────
        //
        // A Power carries a rank, so it is compared by name *and* rank: `Flight 4 → Flight 6` is a
        // change and `added Flight 4` is an addition. Perks and Flaws carry no rank on the sheet
        // and are compared by name alone.

        compared += CompareRanked(rows, "Power",
            was.SelectedPowers.Select(
                p => (Key: p.PowerId, Name: PowerName(rules, p.PowerId), Rank: p.PurchasedRanks)),
            after.SelectedPowers.Select(
                p => (Key: p.PowerId, Name: PowerName(rules, p.PowerId), Rank: p.PurchasedRanks)));

        compared += CompareRanked(rows, "Perk",
            was.Perks.Select(p => (Key: p.PerkId, Name: PerkName(rules, p.PerkId), Rank: p.Units)),
            after.Perks.Select(p => (Key: p.PerkId, Name: PerkName(rules, p.PerkId), Rank: p.Units)));

        compared += CompareNamed(rows, "Flaw",
            was.Flaws.Select(f => (Key: f.FlawId, Name: FlawName(rules, f.FlawId))),
            after.Flaws.Select(f => (Key: f.FlawId, Name: FlawName(rules, f.FlawId))));

        compared += CompareNamed(rows, "Gear",
            was.Gear.Select(g => (Key: g.Name, Name: Blank(g.Name) ?? "Unnamed")),
            after.Gear.Select(g => (Key: g.Name, Name: Blank(g.Name) ?? "Unnamed")));

        // ── The two settings a campaign cares about ──────────────────────────────────
        //
        // Both are presentation on the sheet and both are things a GM reads: a Villain in a Hero
        // campaign and a character built without a points limit are conversations to have.

        compared += Compare(rows, "Kind", was.IsVillain ? "Villain" : "Hero",
            after.IsVillain ? "Villain" : "Hero");

        compared += Compare(rows, "Hero Point limit",
            was.UnlimitedBudget ? "None" : "The tier's",
            after.UnlimitedBudget ? "None" : "The tier's");

        return new CharacterDiff(spentBefore, spentAfter, BudgetFor(rules, after), rows, compared);
    }

    /// <summary>
    /// The tier's Hero Point budget for this character, or null when it names no tier or builds
    /// without a limit.
    ///
    /// <para>Reported beside the spend so a GM can see whether a submission is over — and never
    /// enforced, because the engine already reports <c>HP_BUDGET_EXCEEDED</c> and a second opinion
    /// here would be a rule in a screen.</para>
    /// </summary>
    private static int? BudgetFor(RulesRepository rules, CharacterSheet sheet)
    {
        if (sheet.UnlimitedBudget || sheet.SelectedTierId is null) return null;

        return rules.GetTier(sheet.SelectedTierId)?.HeroPoints;
    }

    /// <summary>
    /// One field, compared. Returns 1 always — <b>the count is of fields examined, not of fields
    /// that moved</b>, which is the whole point of it: see <see cref="CharacterDiff.Compared"/>.
    /// </summary>
    private static int Compare(List<DiffRow> rows, string what, string? before, string? after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal)) return 1;

        rows.Add(new DiffRow(what, before, after, KindOf(before, after)));

        return 1;
    }

    /// <summary>
    /// Every rank in either sheet, compared. A rank of 0 and an absent key are the same thing —
    /// the editors leave a 0 behind when somebody steps a Trait back down, and a row reading
    /// <c>removed Might 0d</c> would be the app reporting its own bookkeeping.
    /// </summary>
    private static int CompareRanks(
        List<DiffRow> rows,
        IReadOnlyDictionary<string, int> before,
        IReadOnlyDictionary<string, int> after,
        Func<string, string> name)
    {
        var compared = 0;

        foreach (var id in Keys(before.Keys, after.Keys))
        {
            var was = before.GetValueOrDefault(id);
            var now = after.GetValueOrDefault(id);

            compared++;

            if (was == now) continue;

            rows.Add(new DiffRow(
                name(id),
                was == 0 ? null : Rank(was),
                now == 0 ? null : Rank(now),
                KindOf(was == 0 ? null : "x", now == 0 ? null : "x")));
        }

        return compared;
    }

    /// <summary>
    /// A list whose entries carry a rank — Powers, and Perks by their unit count.
    ///
    /// <para>Compared by key rather than by position, because a list reordered is not a list
    /// changed and a positional comparison would report every entry after an insertion.</para>
    /// </summary>
    private static int CompareRanked(
        List<DiffRow> rows, string kind,
        IEnumerable<(string Key, string Name, int Rank)> before,
        IEnumerable<(string Key, string Name, int Rank)> after)
    {
        var was = Latest(before);
        var now = Latest(after);
        var compared = 0;

        foreach (var key in Keys(was.Keys, now.Keys))
        {
            compared++;

            var had = was.TryGetValue(key, out var b);
            var has = now.TryGetValue(key, out var a);

            if (had && has && b.Rank == a.Rank) continue;

            var what = has ? a.Name : b.Name;

            rows.Add(new DiffRow(
                $"{kind}: {what}",
                had ? Rank(b.Rank) : null,
                has ? Rank(a.Rank) : null,
                KindOf(had ? "x" : null, has ? "x" : null)));
        }

        return compared;
    }

    /// <summary>A list whose entries carry no rank — Flaws, and gear by its name.</summary>
    private static int CompareNamed(
        List<DiffRow> rows, string kind,
        IEnumerable<(string Key, string Name)> before,
        IEnumerable<(string Key, string Name)> after)
    {
        var was = Latest(before.Select(e => (e.Key, e.Name, Rank: 0)));
        var now = Latest(after.Select(e => (e.Key, e.Name, Rank: 0)));
        var compared = 0;

        foreach (var key in Keys(was.Keys, now.Keys))
        {
            compared++;

            var had = was.ContainsKey(key);
            var has = now.ContainsKey(key);

            if (had == has) continue;

            rows.Add(new DiffRow(
                kind,
                had ? was[key].Name : null,
                has ? now[key].Name : null,
                has ? DiffKind.Added : DiffKind.Removed));
        }

        return compared;
    }

    /// <summary>
    /// Every key in either side, in a stable order.
    ///
    /// <para><b>Sorted, and by the key rather than by the printed name.</b> A GM reading a diff
    /// twice has to see the same order both times, and dictionary order is not a promise. Sorting
    /// on the id keeps it stable even where two things share a name.</para>
    /// </summary>
    private static IEnumerable<string> Keys(
        IEnumerable<string> before, IEnumerable<string> after) =>
        before.Concat(after).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>
    /// One entry per key, last one winning.
    ///
    /// <para>Nothing in the app puts the same Power in a list twice — <c>repeatable</c> options are
    /// variants of one entry — but a payload arriving down a wire is not the app, and a duplicate
    /// key would throw out of <c>ToDictionary</c> and take the screen with it.</para>
    /// </summary>
    private static Dictionary<string, (string Name, int Rank)> Latest(
        IEnumerable<(string Key, string Name, int Rank)> entries)
    {
        var found = new Dictionary<string, (string Name, int Rank)>(StringComparer.Ordinal);

        foreach (var (key, name, rank) in entries) found[key ?? ""] = (name, rank);

        return found;
    }

    private static DiffKind KindOf(string? before, string? after) =>
        before is null ? DiffKind.Added : after is null ? DiffKind.Removed : DiffKind.Changed;

    /// <summary>A rank, written the way the sheet and every editor write one.</summary>
    private static string Rank(int rank) => $"{rank}d";

    /// <summary>Null for a name nobody has typed, so an empty field reads as absent.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TierName(RulesRepository rules, string? id) =>
        id is null ? null : rules.GetTier(id)?.Name ?? id;

    /// <summary>
    /// A package's printed name. It is under <c>CreationRules</c> rather than in a collection of
    /// its own, which is where every other reader of one looks for it too.
    /// </summary>
    private static string? PackageName(RulesRepository rules, string? id) =>
        id is null
            ? null
            : rules.CreationRules.OptionalPackages.FirstOrDefault(p => p.Id == id)?.Name ?? id;

    private static string PowerName(RulesRepository rules, string? id) =>
        id is null ? "Unnamed" : rules.GetPower(id)?.Name ?? id;

    private static string PerkName(RulesRepository rules, string? id) =>
        id is null ? "Unnamed" : rules.GetPerk(id)?.Name ?? id;

    private static string FlawName(RulesRepository rules, string? id) =>
        id is null ? "Unnamed" : rules.GetFlaw(id)?.Name ?? id;
}
