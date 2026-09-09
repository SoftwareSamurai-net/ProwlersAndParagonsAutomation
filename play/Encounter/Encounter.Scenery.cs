using ProwlersAndParagonsAutomation.Play.Rules.Models;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// Chapter 7's three object tables, read as the thing three Chapter 4 rules already name.
///
/// <para><b>p.75's cover, p.78's knockback and p.74's throw each name a figure this engine had no
/// way to look up.</b> The cover's Structure, "the object they strike is tougher than they are",
/// and "the object's weight rank" were all the caller's word or nothing at all, because
/// <c>data/rules/play/environment.json</c> was loaded and applied by nothing. It supplies every one
/// of them: p.107's Smashing table rates a <em>material</em>, p.108's Scenery table rates a
/// <em>thing</em>, and p.108's Massive Objects table replaces the rating with a weight rank for
/// what is too big to have a useful one.</para>
///
/// <para><b>A named row never replaces the caller's number, and that is deliberate.</b> p.107 lets
/// the GM move a Structure by as much as <c>gm_may_adjust_structure_by_max</c> for how thick or how
/// rotten a thing is — the chapter's own second worked example does exactly that to a telephone
/// pole — and no table prints every object in a city. So a bare figure stays what it always was,
/// and naming a row is the other way of saying the same thing: the ledger cites the page the number
/// came off instead of recording it as somebody's say-so.</para>
/// </summary>
public sealed partial class Encounter
{
    /// <summary>Chapter 7's three object tables, in printed order.</summary>
    private static readonly string[] SceneryTables =
        ["smashing_table", "scenery_table", "massive_objects_table"];

    /// <summary>
    /// One row of one of Chapter 7's three object tables, as this engine reads it.
    /// </summary>
    /// <param name="Entry">The <c>environment.json</c> entry the row is in.</param>
    /// <param name="SourceRef">That entry's printed page, for the ledger.</param>
    /// <param name="Name">The printed row name the caller's phrase matched.</param>
    /// <param name="Rank">The row's printed rank — a Structure, or a weight rank.</param>
    /// <param name="Structure">
    /// The row's Structure, or null where the row prints a weight rank instead. p.108's Massive
    /// Objects entry says so in as many words: <c>uses_instead_of_body_or_structure</c> is "the
    /// object's weight rank", so a rule that needs a Structure has none here and says so rather
    /// than reading a weight as one.
    /// </param>
    /// <param name="WeightRank">
    /// The row's weight rank, or null where the row prints a Structure instead. Ch.2 p.17 is what
    /// settles that this column is on the weight scale at all —
    /// <c>massive_objects</c>'s own <c>corroborated_by</c> points at it.
    /// </param>
    /// <param name="PrintedMaximumAttackRank">
    /// The ceiling the table prints beside the rank, where it prints one. p.107's Smashing table
    /// prints materials and ranks and no ceiling; the other two print it out.
    /// </param>
    private sealed record SceneryRow(
        string Entry,
        string SourceRef,
        string Name,
        int Rank,
        int? Structure,
        int? WeightRank,
        int? PrintedMaximumAttackRank);

    /// <summary>
    /// The row Chapter 7 prints for the object a caller named, or null where none of the three
    /// tables prints one — or where two of them tie and the page gives no way to tell which.
    ///
    /// <para><b>It is the same match <c>WeaponFor</c> makes and for the same reason</b>: there is no
    /// scenery in this engine either, so the wall a caller names is a phrase somebody typed. The
    /// longest printed spelling that appears in it as whole words wins, which is what makes "a brick
    /// wall" p.108's <c>Brick Wall</c> at Structure 6 rather than p.107's <c>Brick</c> at the same
    /// figure by luck, and "a bulletproof glass window" <c>Bulletproof Glass</c> rather than
    /// <c>Glass</c> — a difference of four dice.</para>
    ///
    /// <para><b>Two rows tied at the longest spelling refuse rather than pick one</b>, exactly as a
    /// tie between two printed weapons does. Which of two things a caller meant is a question for
    /// the person running the fight, and a wrong match is worse than no match: the ledger would then
    /// cite a printed page for a figure that is not the one in play.</para>
    /// </summary>
    private (SceneryRow? Row, IReadOnlyList<string> Ambiguous) SceneryFor(string named)
    {
        var phrase = Unhyphenated(named);
        var best = 0;
        var found = new List<SceneryRow>();

        foreach (var row in SceneryRows())
        {
            var matched = NamesTheWeapon(phrase, Unhyphenated(row.Name)) ? row.Name.Length : 0;

            if (matched == 0 || matched < best) continue;

            if (matched > best)
            {
                best = matched;
                found.Clear();
            }

            found.Add(row);
        }

        return found switch
        {
            [] => (null, []),
            [var only] => (only, []),
            _ => (null, [.. found.Select(f => $"{f.Name} ({f.Entry})")])
        };
    }

    /// <summary>
    /// Every row of Chapter 7's three object tables, one per printed name — a row naming several
    /// materials is several candidates sharing one rank, because that is how the table reads.
    /// </summary>
    private IEnumerable<SceneryRow> SceneryRows()
    {
        foreach (var id in SceneryTables)
        {
            var entry = _play.GetEnvironment(id);

            if (entry.SmashingTable is { } smashing)
            {
                foreach (var row in smashing.Rows)
                {
                    foreach (var material in row.Materials)
                    {
                        yield return new SceneryRow(
                            entry.Id, entry.SourceRef, material, row.Structure, row.Structure, null, null);
                    }
                }
            }

            foreach (var row in entry.SceneryTable ?? [])
            {
                foreach (var thing in row.Scenery)
                {
                    yield return new SceneryRow(
                        entry.Id, entry.SourceRef, thing, row.Structure, row.Structure, null,
                        row.MaximumAttackRank);
                }
            }

            foreach (var row in entry.MassiveObjectsTable ?? [])
            {
                foreach (var thing in row.Objects)
                {
                    yield return new SceneryRow(
                        entry.Id, entry.SourceRef, thing, row.WeightRank, null, row.WeightRank,
                        row.MaximumAttackRank);
                }
            }
        }
    }

    /// <summary>
    /// The Structure a caller's named piece of scenery has, or a refusal on the ledger where
    /// Chapter 7 gives it none.
    ///
    /// <para><b>A massive object is the case worth naming, because it is a refusal rather than a
    /// miss.</b> p.108's <c>massive_objects</c> says its table prints
    /// <c>uses_instead_of_body_or_structure</c> — the object's weight rank — so the Empire State
    /// Building is in the data with no Structure at all, and reading its 23 as one would put a
    /// figure on the ledger that the page does not print for that purpose.</para>
    /// </summary>
    private EncounterState? SceneryStructure(
        EncounterState state, string actorId, string named, string ruleId, string sourceRef,
        List<LedgerLine> lines, out SceneryRow? row)
    {
        var (found, ambiguous) = SceneryFor(named);
        row = found;

        if (ambiguous.Count > 0)
        {
            return Refuse(state, actorId, ruleId, sourceRef, lines,
                $"'{named}' names {string.Join(" and ", ambiguous)}, and Chapter 7 rates each of "
                + "them separately — which of them is meant is the GM's, so nothing is taken from "
                + "the tables here rather than one of the two being picked. Nothing was rolled.");
        }

        if (found is null)
        {
            return Refuse(state, actorId, ruleId, sourceRef, lines,
                $"Chapter 7 prints nothing called '{named}': pp.107-108's tables rate "
                + $"{SceneryNames()}. Its Structure is the GM's, and this engine takes a bare "
                + "figure for exactly that reason. Nothing was rolled.");
        }

        if (found.Structure is not null) return null;

        var massive = _play.GetEnvironment("massive_objects");

        row = null;

        return Refuse(state, actorId, massive.Id, massive.SourceRef, lines,
            $"{found.Name} is on p.108's Massive Objects table, which p.108 says "
            + $"{massive.MassiveObjects!.UsesInsteadOfBodyOrStructure} — so it has a weight rank of "
            + $"{found.WeightRank} and no Structure at all, and a rule that needs one has nothing "
            + "here to read. Nothing was rolled.");
    }

    /// <summary>Every printed row name Chapter 7's three tables carry, for a refusal that has to say what is there.</summary>
    private string SceneryNames() =>
        string.Join(", ", SceneryRows().Select(r => r.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>
    /// p.108's ceiling on an improvised weapon: <c>attack_rank_caps_at</c>, "the object's Body or
    /// Structure plus the cap bonus", with <c>cap_bonus_dice</c> as the bonus.
    ///
    /// <para><b>It is computed from the rule and checked against the printed column, not read off
    /// it.</b> Two of the three tables print the ceiling out and one does not, so computing it is
    /// the only thing that answers for all three — and where the table does print one, a
    /// disagreement between the rule and the column is a data fault this engine will not paper
    /// over, because whichever of the two it silently preferred would be the one nobody checked.
    /// </para>
    /// </summary>
    private int ImprovisedCeiling(SceneryRow row)
    {
        var entry = _play.GetEnvironment("scenery_as_weapons");
        var rule = entry.SceneryAsWeapons!;
        var ceiling = row.Rank + rule.CapBonusDice;

        if (row.PrintedMaximumAttackRank is { } printed && printed != ceiling)
        {
            throw new InvalidOperationException(
                $"p.108 caps an improvised weapon at {rule.AttackRankCapsAt} — "
                + $"{rule.CapBonusDice} above the object's own rank, which for {row.Name} is "
                + $"{row.Rank} and so {ceiling} — and {row.Entry} prints {printed} in its ceiling "
                + "column. The rule and the table have to agree; this engine computes the ceiling "
                + "and uses the printed column as the second witness rather than choosing between "
                + "them.");
        }

        return ceiling;
    }

    /// <summary>
    /// p.75's cover Structure, taken off Chapter 7's page instead of out of the caller's head.
    ///
    /// <para><b>Two answers to one question is a refusal, not a precedence rule.</b> An attack that
    /// both names a row and states a figure is refused with nothing rolled, because either choice
    /// this engine could make silently would be a number on the ledger that the caller did not ask
    /// for. A GM who has thickened a wall past what p.107 rates it at states the figure and says
    /// nothing about the row.</para>
    ///
    /// <para><b>The line cites the table and p.75 cites itself.</b> This one names the entry the
    /// Structure came out of — p.107's Smashing table for a material, p.108's Scenery table for a
    /// thing — and the row it matched; <see cref="CoverRefused"/> and <c>TheCoverAnswers</c> then
    /// write p.75's own two lines about the same figure. A reader can see both pages, which is the
    /// whole point of the number having come off one of them.</para>
    /// </summary>
    private EncounterState? TheCoverIsNamedScenery(
        EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines,
        out Attack rated)
    {
        rated = attack;

        var cover = _play.GetCombat("modifier_cover");
        var named = attack.CoverScenery!.Trim();

        if (attack.CoverStructure is { } stated)
        {
            return Refuse(state, actor.Id, cover.Id, cover.SourceRef, lines,
                $"this attack names '{named}' as the cover and states a Structure of {stated} as "
                + "well, which are two answers to one question. Name the row and Chapter 7's page "
                + "supplies the figure, or state the figure and Chapter 7 is not consulted — p.107 "
                + $"lets the GM move a Structure by as much as "
                + $"{_play.GetEnvironment("smashing").Smashing!.GmMayAdjustStructureByMax} dice, "
                + "which is what a stated figure is for. Nothing was rolled.");
        }

        if (SceneryStructure(state, actor.Id, named, cover.Id, cover.SourceRef, lines, out var row)
            is { } refused)
        {
            return refused;
        }

        rated = attack with { CoverStructure = row!.Structure };

        lines.Add(new LedgerLine(
            state.Page, actor.Id, row.Entry, row.SourceRef,
            $"the cover is {row.Name}, which Chapter 7 rates at Structure {row.Structure}: that is "
            + $"the figure p.75's cover clauses are applied with, rather than one this fight was "
            + "handed"));

        return null;
    }
}
