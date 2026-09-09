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

    /// <summary>
    /// The Chapter 7 row an item is priced off, or null where Chapter 6 already prices it.
    ///
    /// <para><b>A printed weapon wins, and that is a reading rather than an ordering
    /// convenience.</b> p.108's improvised weapons are "a heavy object or a vehicle" picked up and
    /// swung; Chapter 6's tables are things made to be swung. Where both could match — a wooden
    /// club is p.89's <c>Club</c> and p.107 rates <c>Wood</c> — the manufactured weapon is what the
    /// caller is holding and the material is what it happens to be made of. So Chapter 6 is asked
    /// first, and Chapter 7 answers only for what Chapter 6 does not print. An item Chapter 6 prints
    /// ambiguously stays ambiguous rather than falling through to a different chapter's figure.
    /// </para>
    /// </summary>
    private SceneryRow? ImprovisedFrom(string item)
    {
        var (weapon, _, ambiguous) = WeaponFor(item);

        if (weapon is not null || ambiguous.Count > 0) return null;

        return SceneryFor(item).Row;
    }

    /// <summary>
    /// p.108's Scenery as Weapons, applied to an attack made with something Chapter 7 rates and
    /// Chapter 6 does not: the printed bonus die, and the ceiling the object itself puts on the
    /// attack rank.
    ///
    /// <para><b>Which bonus is p.75's row, the same classification p.87's Gear Limit is read
    /// through.</b> p.108 prices a swung object at <c>close_combat_bonus_dice</c> and a thrown one at
    /// <c>thrown_attack_bonus_dice</c>, and the Attack and Defense table's own type column is the
    /// only thing here that says which an attack is. A Melee Weapon row is the car being swung and a
    /// Ranged Weapon row is the car being thrown; the two Power rows and the Unarmed row never reach
    /// this method at all, because <see cref="GearLimited"/> returns before it for each of them.</para>
    ///
    /// <para><b>Both ceilings apply, and the entry's own <c>ambiguity</c> is why that is written
    /// down.</b> "Whether the six-dice cap replaces Chapter 6's ceiling or sits beside it is not
    /// stated" — so this engine applies both: p.87 caps the <em>Trait</em> before the bonus is added
    /// and p.108 caps the <em>attack rank</em> after it, and neither can hand out more than its own
    /// page allows. Taking one alone would have to be a choice about which page to ignore.</para>
    ///
    /// <para><b>What is named and not applied is the wear.</b> <c>degradation_dice_per_page</c> is
    /// two dice a page and <c>degradation_applies_to</c> scopes it to "an everyday object used as a
    /// weapon by a super strong character" — and the entry's <c>ambiguity</c> says "super strong" is
    /// never given a rank, drawn by contrast with normal human strength rather than by a number. A
    /// threshold this engine invented would be a threshold nobody could argue with, so the line
    /// names the rate and hands the question back.</para>
    /// </summary>
    private int ImprovisedWeapon(
        EncounterState state, Combatant actor, Attack attack, SceneryRow row, int capped,
        List<LedgerLine> lines)
    {
        var entry = _play.GetEnvironment("scenery_as_weapons");
        var rule = entry.SceneryAsWeapons!;

        var thrown = attack.Type == AttackType.RangedWeapon;
        var bonus = thrown ? rule.ThrownAttackBonusDice : rule.CloseCombatBonusDice;
        var ceiling = ImprovisedCeiling(row);
        var brought = Math.Min(capped + bonus, ceiling);

        var trait = Normalise(rule.ThrownAttackTrait);

        var rolled = thrown && !string.Equals(attack.TraitId, trait, StringComparison.Ordinal)
            ? $" p.108 rolls a thrown object on {rule.ThrownAttackTrait} and this attack rolls "
              + $"{attack.TraitId}; the Trait a caller declares is theirs and no figure of it is "
              + "substituted here."
            : "";

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{row.Name} is {rule.AppliesTo} rather than a weapon Chapter 6 prints, so p.108 prices "
            + $"it: {(thrown ? $"thrown, which is {rule.ThrownAttackIs}" : "swung in close combat")} "
            + $"at +{bonus}d, capped at {rule.AttackRankCapsAt} — {row.Rank} plus "
            + $"{rule.CapBonusDice}, so {ceiling}d. {actor.Name} brings {brought}d to bear."
            + rolled
            + $" The {rule.DegradationDicePerPage}d a page of wear is not applied: it is scoped to "
            + $"{rule.DegradationAppliesTo}, and {entry.Ambiguity}"));

        return brought;
    }

    /// <summary>
    /// p.74's throwing range, with the object's weight rank supplied by p.108's Massive Objects
    /// table — and the attack refused where the target is farther off than the throw reaches.
    ///
    /// <para><b>This is the operand p.74 has never had.</b> <c>throwing_range.rank_formula</c> is
    /// "throwing rank = Might - the object's weight rank" and Chapters 3-5 give nothing a weight
    /// rank, which is why p.78's knockback reads the throwing rank as the attack rank instead. The
    /// Massive Objects table is where the book prints one, and Ch.2 p.17 is what settles that its
    /// first column is on the weight scale at all — the entry's own <c>corroborated_by</c> points
    /// there.</para>
    ///
    /// <para><b>The reach is a limit and not a penalty, which is a reading.</b> p.74 says what a
    /// throw reaches and prints nothing at all about throwing farther, so the two honest answers are
    /// to refuse the throw or to report the reach and let it fly. Refusing is the one that never
    /// resolves an attack the page has no rules for; the ledger line names the figure and the
    /// formula, so a caller can see how far it would have gone. <c>docs/guide/play-engine.md</c>
    /// records it.</para>
    ///
    /// <para><b>An object with no weight rank is reported and not refused</b>, because p.108 plainly
    /// allows a motorcycle to be thrown and no page says how far. That silence is the GM's, and the
    /// line says so rather than this engine reading a Structure as a weight.</para>
    /// </summary>
    private EncounterState? TheThrowFallsShort(
        EncounterState state, Combatant actor, Attack attack, SceneryRow row, List<LedgerLine> lines)
    {
        if (attack.Type != AttackType.RangedWeapon) return null;

        var entry = _play.GetCombat("throwing_range");
        var throwing = entry.Throwing!;
        var scenery = _play.GetEnvironment("scenery_as_weapons");

        if (row.WeightRank is not { } weight)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{row.Name} is thrown, and p.74 works a throw's distance out as "
                + $"\"{throwing.RankFormula}\": {row.Entry} rates it at {row.Rank} and that figure "
                + $"is a Structure rather than a weight rank, so there is nothing here to subtract. "
                + "Only p.108's Massive Objects table prints a weight rank, so how far this one goes "
                + "is the GM's and the throw is not limited here"));

            return null;
        }

        if (!throwing.RankFormula.Contains("Might", StringComparison.OrdinalIgnoreCase)
            || !throwing.RankFormula.Contains("weight rank", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"throwing_range now works a throw out as '{throwing.RankFormula}'. This engine "
                + "subtracts an object's weight rank from the thrower's Might because the entry said "
                + "so; a different formula is a rule it cannot apply, and it will not guess at the "
                + "operands. See docs/guide/play-engine.md's readings table.");
        }

        var might = actor.Rank(Normalise(scenery.SceneryAsWeapons!.ThrownAttackTrait));
        var rank = Math.Max(throwing.MinimumRank, might - weight);
        var (band, printed, past) = ThrowReach(rank);
        var here = state.RangeBetween(actor.Id, attack.Target);

        var reach = $"p.108 gives {row.Name} a weight rank of {weight} and p.74 works the throw out "
                    + $"as \"{throwing.RankFormula}\": {actor.Name}'s "
                    + $"{scenery.SceneryAsWeapons.ThrownAttackTrait} of {might} less {weight} is "
                    + $"{rank}, which reaches {printed}"
                    + (past ? ", further than this engine's outermost range class" : "");

        if ((int)here <= (int)band)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{reach}, and {state[attack.Target].Name} is at {here} Range"));

            return null;
        }

        return Refuse(state, actor.Id, entry.Id, entry.SourceRef, lines,
            $"{reach} — and {state[attack.Target].Name} is at {here} Range, which is farther than "
            + "that. p.74 says how far a throw carries and says nothing about throwing past it, so "
            + "this one is refused rather than resolved under a rule the page does not print. "
            + "Nothing was rolled.");
    }

    /// <summary>
    /// p.108's one printed cost of picking up something that big:
    /// <c>always_breaks_apart_after</c> is "the first shot".
    ///
    /// <para><b>It is applied rather than announced, because there is somewhere for it to land.</b>
    /// This engine already has a state for an object leaving somebody's hands —
    /// <see cref="Combatant.Dropped"/>, which p.76's page turn uses — so a mountain that has been
    /// swung once is a mountain nobody is holding, and an attack naming it again is refused by the
    /// guard that already refuses an attack made with what its actor does not hold. Naming the
    /// clause and leaving the object in place would let one freight train be thrown every page for
    /// the rest of the fight.</para>
    ///
    /// <para><b>Only the Massive Objects table, because only it prints the sentence.</b> A
    /// motorcycle is on p.108's Scenery table and wears out at the rate the entry above scopes to
    /// "super strong" characters and never says what that is; the break-apart is printed flat,
    /// against the rows that have a weight rank instead of a Structure.</para>
    /// </summary>
    private EncounterState TheMassiveObjectBreaksApart(
        EncounterState state, Combatant actor, Attack attack, List<LedgerLine> lines)
    {
        if (attack.Item is not { Length: > 0 } item) return state;
        if (ImprovisedFrom(item.Trim()) is not { WeightRank: not null } row) return state;
        if (state[actor.Id].Holding is null) return state;

        var entry = _play.GetEnvironment("massive_objects");

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"{row.Name} is on p.108's Massive Objects table, and one of those always breaks apart "
            + $"after {entry.MassiveObjects!.AlwaysBreaksApartAfter}: {actor.Name} is holding "
            + "nothing now, and an attack naming it again is refused for the same reason any attack "
            + "made with what its actor does not hold is"));

        return state.With(state[actor.Id].Dropped());
    }
}
