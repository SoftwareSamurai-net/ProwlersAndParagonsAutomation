using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;

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

    /// <summary>
    /// Whether the rows account for the spend.
    ///
    /// <para><b>This is the positive control <see cref="Compared"/> could not be.</b> That counts
    /// fields <em>examined</em>, which is the right guard against a comparison that has stopped
    /// running — and it is worth nothing against one that runs and looks at the wrong half of a
    /// field. When the diff compared a Power on its id and its ranks alone, moving Immunity from
    /// one unit to six produced <c>spend 7 → 22</c>, no rows at all, and a healthy
    /// <c>Compared</c> of 7: the Power's key <em>was</em> examined. The GM read "nothing changed"
    /// above a header saying the price had trebled.</para>
    ///
    /// <para>So the two figures are held against the rows. If the spend moved, something must have
    /// moved to move it, and the screen has to be able to say what — false means it cannot, which
    /// is a defect to print rather than an empty list to draw. It is also what makes the next
    /// cost-bearing field added to <see cref="CharacterSheet"/> fail a test instead of quietly
    /// going unreported; counting examined fields never will.</para>
    ///
    /// <para>A null spend on either side is the engine declining to price a sheet, which is a
    /// different report and not this one's business.</para>
    /// </summary>
    public bool Explained =>
        SpentBefore is null || SpentAfter is null || SpentBefore == SpentAfter || Rows.Count > 0;
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
/// Flaws by their own names. <c>CampaignApprovalTests.NoRowNamesAFieldOfAStoredCharacter</c>
/// holds it to that against the property names of <see cref="CharacterSheet"/> itself, so a field
/// added later cannot leak its own spelling onto the screen by being appended to a loop.</para>
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

        // ── The ceiling the character is built to ────────────────────────────────────
        //
        // **The cap in force, not the field.** `Trait Cap 12d → 6d` is what moved for the
        // character; `TraitCapRank: null → 6` is this application's bookkeeping, which is the same
        // reason a rank stepped back to zero reads as removed rather than as `0d`. So the figure
        // is `DerivedStatsCalculator.EffectiveTraitCap` — a house cap where there is one and the
        // tier's otherwise — and a house cap set to exactly the tier's own moves nothing and says
        // nothing.
        //
        // **Without it the screen said "nothing changed" over a real change.** The cap is the
        // datum Resolve is measured from and the ceiling every Ability is judged against, so a
        // player who added a house cap between submissions moved their Resolve and could turn a
        // legal Trait illegal while the GM's list of what changed stayed empty. It costs no Hero
        // Points, which is why the spend could not be the trigger either.
        //
        // A tier change moves this row as well as the Tier row, and that is the honest report
        // rather than a duplicate: the two rows say different things — which table this is, and
        // what the ranks are now bounded by.
        compared += Compare(rows, "Trait Cap", CapOf(rules, was), CapOf(rules, after));

        // ── The table's house rules ──────────────────────────────────────────────────
        //
        // **The price is here for the same reason the cap is, and one reason more.** A table's
        // price for Immortality is Hero Points: a campaign that raised it moved the spend on every
        // character in it that has the Power, so the total *does* move and the GM is owed the
        // reason beside the figure. It reads `Immortality 3 HP → 9 HP`, naming the Power the way
        // the book names it, and the book's own price stands in for a character that carries none
        // — `null → 9` is this application's bookkeeping and not what changed for the character.
        //
        // **Drawn only under a character that has the Power.** A price nobody is charged is a row
        // a GM cannot act on, and the approval screen's whole job is to say what is different
        // about *this* character. That is the same rule the 0d rank follows.
        if (was.HasPower(ImmortalityId) || after.HasPower(ImmortalityId))
        {
            compared += Compare(rows, PowerName(rules, ImmortalityId),
                PriceOf(rules, was), PriceOf(rules, after));
        }

        // The optional rules, one row per switch that moved, named the way the book names it —
        // `Fatal Damage off → on`. One row per switch rather than one row for the block, because
        // "House rules changed" is exactly the sentence this screen exists not to print: a GM
        // deciding about a submission needs to know *which* rule, and thirteen switches in one row
        // is a diff the reader has to do themselves.
        compared += CompareHouseRules(rows, was.CampaignTable, after.CampaignTable);

        // ── Ranks ────────────────────────────────────────────────────────────────────
        //
        // `6d → 8d`, which is how a rank is written everywhere else in this app and on the sheet.

        compared += CompareRanks(rows, was.AbilityRanks, after.AbilityRanks,
            id => rules.GetAbility(id)?.Name ?? id,
            was.AbilityModifiers, after.AbilityModifiers, rules);

        compared += CompareRanks(rows, was.TalentRanks, after.TalentRanks,
            id => rules.GetTalent(id)?.Name ?? id);

        // ── The chosen lists ─────────────────────────────────────────────────────────
        //
        // A Power carries a rank, so it is compared by name *and* rank: `Flight 4 → Flight 6` is a
        // change and `added Flight 4` is an addition. Perks and Flaws carry no rank on the sheet
        // and are compared by name alone.

        compared += CompareDetailed(rows, "Power",
            was.SelectedPowers.Select(p => Entry(p.PowerId, PowerName(rules, p.PowerId), PowerDetail(rules, p))),
            after.SelectedPowers.Select(p => Entry(p.PowerId, PowerName(rules, p.PowerId), PowerDetail(rules, p))));

        compared += CompareDetailed(rows, "Perk",
            was.Perks.Select(p => Entry(p.PerkId, PerkName(rules, p.PerkId), PerkDetail(p))),
            after.Perks.Select(p => Entry(p.PerkId, PerkName(rules, p.PerkId), PerkDetail(p))));

        compared += CompareDetailed(rows, "Flaw",
            was.Flaws.Select(f => Entry(f.FlawId, FlawName(rules, f.FlawId), Blank(f.NarrativeDetail))),
            after.Flaws.Select(f => Entry(f.FlawId, FlawName(rules, f.FlawId), Blank(f.NarrativeDetail))));

        compared += CompareDetailed(rows, "Gear",
            was.Gear.Select(g => Entry(g.Name, Blank(g.Name) ?? "Unnamed", GearDetail(rules, g))),
            after.Gear.Select(g => Entry(g.Name, Blank(g.Name) ?? "Unnamed", GearDetail(rules, g))));

        // ── A Trait's Source ─────────────────────────────────────────────────────────
        //
        // Free, printed, and not derivable from anything else on the sheet. `AbilitySources` and
        // `TalentSources` are the whole record of which Traits carry an `Abilities (…)` line
        // inside a Power group, and `CharacterSheet`'s own note says why a rank cannot stand in
        // for them: Ch.2 p.64's "7d or greater" is an instruction about random generation rather
        // than a threshold, and the published sheets disagree with it in both directions.
        //
        // An absent key means the default Source, so clearing one reads as a removal rather than
        // as a change to a word the sheet never printed.

        compared += CompareDetailed(rows, "Ability Source",
            was.AbilitySources.Select(s => Entry(s.Key, AbilityName(rules, s.Key), SourceName(rules, s.Value))),
            after.AbilitySources.Select(s => Entry(s.Key, AbilityName(rules, s.Key), SourceName(rules, s.Value))));

        compared += CompareDetailed(rows, "Talent Source",
            was.TalentSources.Select(s => Entry(s.Key, TalentName(rules, s.Key), SourceName(rules, s.Value))),
            after.TalentSources.Select(s => Entry(s.Key, TalentName(rules, s.Key), SourceName(rules, s.Value))));

        // ── Who the character is ─────────────────────────────────────────────────────
        //
        // Not one of these costs a Hero Point and every one of them prints on the sheet, so all
        // four belong to the reported half of this diff rather than the priced half. Without
        // them a player could rewrite their appearance, their motivation, their quote and every
        // connection they have between two submissions and the screen whose whole job is
        // deciding about that character would draw an empty list — which reads as agreement.

        compared += Compare(rows, "Appearance", Blank(was.Appearance), Blank(after.Appearance));
        compared += Compare(rows, "Motivation", Blank(was.Motivation), Blank(after.Motivation));
        compared += Compare(rows, "Quote", Blank(was.Quote), Blank(after.Quote));

        // Keyed on the text, because a connection is free prose with no id: rewording one is a
        // removal and an addition, which is the honest report of a line nobody can match up.
        compared += CompareDetailed(rows, "Connection",
            was.Connections.Select(c => Entry(c, c, Blank(c))),
            after.Connections.Select(c => Entry(c, c, Blank(c))));

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
        Func<string, string> name,
        IReadOnlyDictionary<string, List<SelectedProCon>>? beforeModifiers = null,
        IReadOnlyDictionary<string, List<SelectedProCon>>? afterModifiers = null,
        RulesRepository? rules = null)
    {
        var compared = 0;

        foreach (var id in Keys(
            Keys(before.Keys, beforeModifiers?.Keys ?? []),
            Keys(after.Keys, afterModifiers?.Keys ?? [])))
        {
            var was = before.GetValueOrDefault(id);
            var now = after.GetValueOrDefault(id);

            // An Ability's Pros and Cons cost Hero Points and were compared nowhere — the same
            // blindness the Powers had, in the one collection that is not a list. Folded into the
            // rank's own line, because "Might 6d · Area" is how the sheet reads it.
            var wasDetail = Detail(was, beforeModifiers, id, rules);
            var nowDetail = Detail(now, afterModifiers, id, rules);

            compared++;

            if (string.Equals(wasDetail, nowDetail, StringComparison.Ordinal)) continue;

            rows.Add(new DiffRow(name(id), wasDetail, nowDetail, KindOf(wasDetail, nowDetail)));
        }

        return compared;

        // A rank of 0 with nothing applied to it is the same thing as an absent key — the editors
        // leave a 0 behind when somebody steps a Trait back down, and a row reading `removed
        // Might 0d` would be the app reporting its own bookkeeping.
        static string? Detail(
            int rank,
            IReadOnlyDictionary<string, List<SelectedProCon>>? modifiers,
            string id,
            RulesRepository? rules)
        {
            var applied = modifiers?.GetValueOrDefault(id) ?? [];

            if (rank == 0 && applied.Count == 0) return null;

            var parts = new List<string> { Rank(rank) };

            if (rules is not null)
            {
                parts.AddRange(applied.Select(choice => ProConLabel(rules, null, choice)));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// One entry of a list, as this diff reads one: what identifies it, what it is called, and
    /// everything else about it in one line of the app's own words.
    /// </summary>
    private static (string Key, string Name, string? Detail) Entry(
        string key, string name, string? detail) => (key, name, detail);

    /// <summary>
    /// A list compared by key, with everything about each entry in one line.
    ///
    /// <para><b>The detail line is the whole of why this replaced a comparison on rank alone.</b>
    /// A Power compared on its id and its purchased ranks is blind to its Pros, its Cons, its cost
    /// variant, its units, its nominated Trait and its Source — so a player could move Immunity
    /// from one unit to six, the header would read <c>7 → 22 Hero Points</c>, and the list of what
    /// changed would be empty. Gear was worse: keyed on its name, and features are the only thing
    /// gear costs Hero Points for. See the completed entry in <c>PROGRESS.md</c> on the campaign's
/// clone and its approval request — an item number is not a stable address, and the one this
/// used to name is now about the account cap.</para>
    ///
    /// <para><b>Compared by key rather than by position</b>, because a list reordered is not a list
    /// changed and a positional comparison would report every entry after an insertion.</para>
    /// </summary>
    private static int CompareDetailed(
        List<DiffRow> rows, string kind,
        IEnumerable<(string Key, string Name, string? Detail)> before,
        IEnumerable<(string Key, string Name, string? Detail)> after)
    {
        var was = Latest(before);
        var now = Latest(after);
        var compared = 0;

        foreach (var key in Keys(was.Keys, now.Keys))
        {
            compared++;

            var had = was.TryGetValue(key, out var b);
            var has = now.TryGetValue(key, out var a);

            if (had && has && string.Equals(b.Detail, a.Detail, StringComparison.Ordinal)) continue;

            rows.Add(new DiffRow(
                $"{kind}: {(has ? a.Name : b.Name)}",
                had ? b.Detail : null,
                has ? a.Detail : null,
                KindOf(had ? "x" : null, has ? "x" : null)));
        }

        return compared;
    }

    /// <summary>
    /// Everything about one Power, in the words the sheet already prints for it.
    ///
    /// <para><b>Assembled from the app's own labels rather than from a spelling of its own.</b>
    /// <c>SheetView</c> writes a Pro as <c>Name (Variant)</c> and a Power's units as <c>×N</c>, and
    /// a cost variant goes through <see cref="Labels.Humanise"/> in three places already — so this
    /// reads the same, and a row cannot print a field name or an id that the sheet would not.</para>
    /// </summary>
    private static string PowerDetail(RulesRepository rules, SelectedPower power)
    {
        var parts = new List<string> { Rank(power.PurchasedRanks) };
        var model = rules.GetPower(power.PowerId);

        if (power.CostVariantKey is { Length: > 0 } variant) parts.Add(Labels.Humanise(variant));

        // The book's own noun for a unit where the data carries one — "immunities", "Resolve" —
        // and the sheet's `×N` where it does not. A bare count would be this class inventing a
        // word for something the rules data already names.
        if (power.Units != 1)
        {
            parts.Add(model?.CostUnitLabel is { Length: > 0 } unit
                ? $"{power.Units} {unit}"
                : $"×{power.Units}");
        }

        // Boost and Expertise nominate a Trait, and for Boost it sets the per-rank cost — so it is
        // never presentation. Named through the same lookups every other row uses.
        if (power.BaselineTraitId is { Length: > 0 } trait) parts.Add($"on {TraitName(rules, trait)}");

        if (power.SourceId is { Length: > 0 } source)
        {
            parts.Add(rules.GetSource(source)?.Name ?? Labels.Humanise(source));
        }

        parts.AddRange(power.Pros.Select(pro => ProConLabel(rules, model, pro)));
        parts.AddRange(power.Cons.Select(con => ProConLabel(rules, model, con)));

        return string.Join(" · ", parts);
    }

    /// <summary>A Perk's unit count and whatever the player wrote beside it.</summary>
    private static string PerkDetail(SelectedPerk perk)
    {
        var detail = Blank(perk.NarrativeDetail);

        return detail is null ? Rank(perk.Units) : $"{Rank(perk.Units)} · {detail}";
    }

    /// <summary>
    /// Everything a piece of gear carries, which is everything it can cost Hero Points for.
    ///
    /// <para>Ch.6 makes mundane gear free and untracked, so a plain item has no detail at all and
    /// its row reads as a bare addition or removal — which is the honest report of one. A
    /// customised item is a small character of its own, and all of it is here.</para>
    /// </summary>
    private static string? GearDetail(RulesRepository rules, SelectedGear gear)
    {
        var parts = new List<string>();

        if (gear.PairedUnderTwoFisted) parts.Add("a matched pair");

        parts.AddRange(gear.Features.Select(f => GearFeatureLabel(rules, f)));
        parts.AddRange(gear.Pros.Select(pro => ProConLabel(rules, null, pro)));
        parts.AddRange(gear.Cons.Select(con => ProConLabel(rules, null, con)));

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// A Pro or Con, written the way <c>SheetView</c> writes one.
    ///
    /// <para>A Power's own Pros and Cons are looked up on the Power first, because a
    /// Power-specific entry and a generic one may share an id and the Power's own text is the one
    /// that applies.</para>
    /// </summary>
    private static string ProConLabel(RulesRepository rules, PowerModel? power, SelectedProCon choice)
    {
        var name = power?.PowerPros.Concat(power.PowerCons).FirstOrDefault(e => e.Id == choice.Id)?.Name
                   ?? rules.GetPro(choice.Id)?.Name
                   ?? rules.GetCon(choice.Id)?.Name
                   ?? Labels.Humanise(choice.Id);

        if (choice.VariantKey is { Length: > 0 } variant)
        {
            name = $"{name} ({Labels.Humanise(variant)})";
        }

        return choice.Units is { } units ? $"{name} ×{units}" : name;
    }

    /// <summary>One custom feature of a piece of gear, with its grade where it has two.</summary>
    private static string GearFeatureLabel(RulesRepository rules, SelectedGearFeature feature)
    {
        var name = rules.GetGearFeature(feature.FeatureId)?.Name ?? Labels.Humanise(feature.FeatureId);

        return feature.GradeKey is { Length: > 0 } grade
            ? $"{name} ({Labels.Humanise(grade)})"
            : name;
    }

    /// <summary>
    /// A nominated Trait's printed name. It may be an Ability, a Talent or a Power — Boost and
    /// Expertise both take whichever — so all three are tried before the id is humanised.
    /// </summary>
    private static string TraitName(RulesRepository rules, string id) =>
        rules.GetAbility(id)?.Name
        ?? rules.GetTalent(id)?.Name
        ?? rules.GetPower(id)?.Name
        ?? Labels.Humanise(id);

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
    private static Dictionary<string, (string Name, string? Detail)> Latest(
        IEnumerable<(string Key, string Name, string? Detail)> entries)
    {
        var found = new Dictionary<string, (string Name, string? Detail)>(StringComparer.Ordinal);

        foreach (var (key, name, detail) in entries) found[key ?? ""] = (name, detail);

        return found;
    }

    private static DiffKind KindOf(string? before, string? after) =>
        before is null ? DiffKind.Added : after is null ? DiffKind.Removed : DiffKind.Changed;

    /// <summary>A rank, written the way the sheet and every editor write one.</summary>
    private static string Rank(int rank) => $"{rank}d";

    /// <summary>
    /// The one Power whose printed entry hands its price to the table. Named once here for the
    /// reason the validator names it once: two spellings is how two surfaces come to disagree
    /// about which Power they are talking about.
    /// </summary>
    private const string ImmortalityId = "immortality";

    /// <summary>
    /// What this character is charged for Immortality, in Hero Points — the table's price where
    /// there is one and the book's otherwise.
    ///
    /// <para><b>The book's price stands in rather than null</b>, so the row reads
    /// <c>3 HP → 9 HP</c>. `null → 9` would be this application's bookkeeping, which is the same
    /// objection the Trait Cap row's own note records: what changed for the character is the
    /// figure it is charged, and it was always charged something.</para>
    /// </summary>
    private static string? PriceOf(RulesRepository rules, CharacterSheet sheet) =>
        (sheet.ImmortalityCost ?? rules.GetPower(ImmortalityId)?.CostFlat) is { } price
            ? $"{price} HP"
            : null;

    /// <summary>
    /// One row per optional rule that moved, in the book's order, named the way the book names it.
    ///
    /// <para><b>A block that arrived or went away is still compared switch by switch.</b> A
    /// character joining a table that has adopted nothing gains a block whose every switch is off,
    /// which is the same game it was already playing — so <c>null</c> and "all off" compare equal
    /// and produce no rows at all. That is what <c>CampaignTable.IsTheBook</c> means, and a row
    /// saying "House rules added" over a table that adopted nothing would be the application
    /// reporting its own storage.</para>
    ///
    /// <para><b>The Gear Limit rank moves a row of its own</b>, because it is a figure rather than
    /// a switch and `Raised Gear Limit off → on` does not say to what.</para>
    /// </summary>
    private static int CompareHouseRules(
        List<DiffRow> rows, CampaignTable? before, CampaignTable? after)
    {
        var was = before ?? CampaignTable.Book;
        var now = after ?? CampaignTable.Book;
        var compared = 0;

        foreach (var (key, name, _) in HouseRuleFormatter.All)
        {
            compared += Compare(rows, name,
                HouseRuleFormatter.IsOn(was, key) ? "on" : "off",
                HouseRuleFormatter.IsOn(now, key) ? "on" : "off");
        }

        compared += Compare(rows, "Gear Limit",
            was.GearLimitRank is { } b ? Rank(b) : null,
            now.GearLimitRank is { } a ? Rank(a) : null);

        return compared;
    }

    /// <summary>Null for a name nobody has typed, so an empty field reads as absent.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The Trait Cap this character is built to, written the way every rank in this app is, or
    /// null for a character with no tier and no house cap — which has no ceiling to report.
    ///
    /// <para><b>Read through <c>DerivedStatsCalculator.EffectiveTraitCap</c> and never spelled out
    /// here</b>, so the row a GM decides about is the same number the validator judged the
    /// submission by and the same one its Resolve was measured from.</para>
    /// </summary>
    private static string? CapOf(RulesRepository rules, CharacterSheet sheet) =>
        DerivedStatsCalculator.EffectiveTraitCap(
            sheet, sheet.SelectedTierId is null ? null : rules.GetTier(sheet.SelectedTierId))
            is { } cap
            ? Rank(cap)
            : null;

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

    private static string AbilityName(RulesRepository rules, string id) =>
        rules.GetAbility(id)?.Name ?? Labels.Humanise(id);

    private static string TalentName(RulesRepository rules, string id) =>
        rules.GetTalent(id)?.Name ?? Labels.Humanise(id);

    /// <summary>
    /// A Source's printed name — the word the sheet puts in an <c>Abilities (…)</c> line, not the
    /// id it is stored under.
    /// </summary>
    private static string? SourceName(RulesRepository rules, string? id) =>
        Blank(id) is null ? null : rules.GetSource(id!)?.Name ?? Labels.Humanise(id!);
}
