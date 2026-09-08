using ProwlersAndParagonsAutomation.Play.Rules.Models;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// p.87's Gear Limit, applied to an attack made with a held item, and Chapter 6's Weapon Bonus
/// added to what is left of the Trait.
///
/// <para><b>Two chapters and both are read.</b> p.80 offers the table the switch and names the
/// figure — <see cref="TableRules.GearLimit"/> is where that is decided — and p.87 is the detail
/// it points at: what the ceiling covers, what a Weapon Bonus is added to, and the three tables
/// that say what each weapon is worth. Neither half is any use alone, which is why both switches
/// sat unapplied until <c>equipment.json</c> arrived.</para>
/// </summary>
public sealed partial class Encounter
{
    /// <summary>The three weapons tables of <c>equipment.json</c>, in printed order.</summary>
    private static readonly string[] WeaponTables = ["ancient_weapons", "modern_weapons", "advanced_weapons"];

    /// <summary>
    /// The rank this attack actually brings to bear: p.87's ceiling on the Trait, plus the item's
    /// Weapon Bonus where Chapter 6 prints one for it.
    ///
    /// <para><b>It is the <em>rank</em> and not the pool, because Chapter 6 says so.</b> p.90's
    /// feature glossary calls "your Trait plus the item's Weapon Bonus" the attack rank three times
    /// over, and p.87 calls the sum "your maximum effective rank". So the figure this returns is
    /// what answers a cover's Structure and what p.78's knockback is priced off, not a modifier on
    /// the roll — <c>AttackModifiers</c> is where the dice cover, size, the light and going all-out
    /// lend or take are counted, and none of those is anybody's rank.</para>
    ///
    /// <para><b>Which attacks it reaches is p.75's row, and that is a reading.</b> p.87 caps what
    /// you can apply "when using mundane equipment", and the one classification this engine has for
    /// what an attack is made with is the Attack and Defense table's own type column. The two
    /// weapon rows are mundane equipment being used; the two Power rows roll a Power's own rank,
    /// which is not equipment at all and which no Weapon Bonus is ever added to (p.88 adds one to
    /// Might, Martial Arts or Agility and to nothing else); and <c>Unarmed</c> is a fist by the
    /// table's own word. Each of the three writes a line saying which silence it is, because a
    /// setting that is on and did not reach an attack is exactly the case this engine's ledger
    /// exists to make visible. <c>docs/guide/play-engine.md</c> records the reading.</para>
    ///
    /// <para><b>p.87's own exception falls out of that rather than being applied on top of it.</b>
    /// "If your unarmed attack or defense rank exceeds your effective rank while armed with a melee
    /// weapon, you can use your unarmed attack or defense rank instead" is printed as a permission,
    /// and the entry's <c>ambiguity</c> records that the page never says who takes it or when it is
    /// declared. A caller who has been told the wielder takes it declares p.75's <c>Unarmed</c> row
    /// with the item still in hand — full Trait, no cap — and the damage kind the weapon still buys
    /// is the <see cref="Attack.Damage"/> they already declare. Substituting the higher figure
    /// silently would be this engine deciding a choice the page hands to a person, and it would
    /// make the cap unreachable in close combat.</para>
    /// </summary>
    private int GearLimited(
        EncounterState state, Combatant actor, Attack attack, string item, int rank,
        List<LedgerLine> lines)
    {
        var entry = _play.GetEquipment("gear_limit");
        var limit = entry.GearLimit!;

        if (attack.Type is AttackType.PhysicalPower or AttackType.MentalPower)
        {
            lines.Add(new LedgerLine(
                state.Page, actor.Id, entry.Id, entry.SourceRef,
                $"{actor.Name} is holding {item} and attacking on p.75's {PrintedType(attack.Type)} "
                + $"row, which rolls the Power's own rank — so the Gear Limit does not reach this "
                + $"attack: p.87 caps {limit.WhatItIs}, and a Power is not that"));

            return rank;
        }

        if (attack.Type == AttackType.Unarmed)
        {
            var carve = _play.GetEquipment("gear_limit_close_combat_exception");
            var exception = carve.CloseCombatException!;

            lines.Add(new LedgerLine(
                state.Page, actor.Id, carve.Id, carve.SourceRef,
                $"{actor.Name} is holding {item} and attacking on p.75's Unarmed row, so the Gear "
                + $"Limit does not reach this attack. That is p.87's own exception, and it is the "
                + $"wielder's to take: where {exception.Condition}, {exception.YouMayUseInstead} may "
                + $"be used instead. {actor.Name} rolls {attack.TraitId} at {rank}d uncapped, and "
                + $"what the weapon still buys is {exception.WhatTheWeaponStillBuys} — which is the "
                + "damage kind this attack declares"));

            return rank;
        }

        var ceiling = state.Table.GearLimit(_play);
        var capped = Math.Min(rank, ceiling);
        var (weapon, table, ambiguous) = WeaponFor(item);
        var bonus = weapon?.BonusDice ?? 0;

        var raised = state.Table.RaisedGearLimit && state.Table.GearLimitRank is not null
            ? $" — this table raised it from {limit.DefaultRank}d, which is the switch p.80 offers"
            : "";

        var carried = capped == rank
            ? $"{actor.Name}'s {attack.TraitId} of {rank}d is under it and comes to bear whole"
            : $"{actor.Name}'s {attack.TraitId} of {rank}d comes to bear as {capped}d";

        var added = ambiguous.Count > 0
            ? $"{item} names {string.Join(" and ", ambiguous)}, and Chapter 6 prints a different "
              + "figure for each — which of them is being swung is the GM's, so nothing is added "
              + "here rather than one of the two being picked"
            : weapon is null
                ? $"Chapter 6 prints no weapon called {item}, so it adds nothing here and what a "
                  + "Weapon Bonus for it would be is the GM's"
                : weapon.BonusDice is null
                    ? $"the {table} table prints a dash rather than a figure for {weapon.Name}, so "
                      + "it adds nothing of its own"
                    : $"{item} is the {table} table's {weapon.Name} at +{weapon.BonusDice}d";

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"p.87 caps {limit.WhatItIs} at {ceiling}d{raised}, so {carried}; {added}, for an "
            + $"attack rank of {capped + bonus}d"));

        return capped + bonus;
    }

    /// <summary>
    /// The half of pp.87–88 that lands on the <em>defending</em> side, said on the ledger because
    /// this engine does not apply it.
    ///
    /// <para><b>Chapter 6 prices a weapon in both directions and this engine reads one of them.</b>
    /// p.88: "you add its Weapon Bonus to your Agility or Martial Arts rank when defending yourself
    /// against close combat attacks", and p.87's ceiling is on "the maximum Trait rank you can apply
    /// when using mundane equipment" — a sentence about applying a Trait, not about attacking with
    /// one. So a defender with a printed melee weapon in their hands has a Weapon Bonus this engine
    /// does not lend them and a cap it does not put on them, and <b>both are silent unless something
    /// says so</b>, which is the case this ledger exists for.</para>
    ///
    /// <para><b>It is named rather than applied, and the reason is on the entry itself.</b> The
    /// close-combat exception's own <c>ambiguity</c> records that the swap to the bare-handed figure
    /// is a permission the page never says who takes — and p.87 prints it as "your unarmed attack
    /// <em>or defense</em> rank", so the defending half carries exactly the same unresolved choice
    /// the attacking half does. Applying the cap alone would take dice off a defender the page
    /// offers a way out to; applying the bonus alone would lend dice the ceiling is supposed to
    /// bound. Both halves are one decision and it is the wielder's, so what is applied here is
    /// nothing and what is written is which nothing it is.</para>
    ///
    /// <para><b>Mundane armour is the other half of p.87's "usually armor and weapons", and it
    /// cannot be reached at all.</b> A <c>Combatant</c> carries no gear — <c>SelectedGear</c> is on
    /// the character sheet and <c>CombatantFactory</c> never reads it — so no defence in a fight
    /// here is item-backed, and the Armor <em>Power</em> is a Power rather than mundane equipment.
    /// The cap on the defensive side is therefore unreached rather than declined, and
    /// <c>docs/guide/play-engine.md</c> says so.</para>
    /// </summary>
    private void ArmedDefence(
        EncounterState state, Combatant target, Attack attack, List<LedgerLine> lines)
    {
        if (attack.Type is not (AttackType.Unarmed or AttackType.MeleeWeapon)) return;
        if (target.Holding is not { } held) return;

        var (weapon, table, _) = WeaponFor(held.Name);

        if (weapon is null || !string.Equals(weapon.Class, "Melee", StringComparison.Ordinal)) return;

        var entry = _play.GetEquipment("weapon_bonus");
        var rule = entry.WeaponBonus!;
        var carve = _play.GetEquipment("gear_limit_close_combat_exception");

        lines.Add(new LedgerLine(
            state.Page, target.Id, entry.Id, entry.SourceRef,
            $"{target.Name} answers a close combat attack holding {held.Name}, which is the "
            + $"{table} table's {weapon.Name}. p.88 adds a melee weapon's Weapon Bonus to "
            + $"{string.Join(" or ", rule.MeleeDefenseTraits)} on this side of the roll and p.87 "
            + $"caps {_play.GetEquipment("gear_limit").GearLimit!.WhatItIs} whichever side it is "
            + $"applied on: this engine applies neither, because {carve.Ambiguity} — and the same "
            + "permission is printed for a defense rank as for an attack rank. What the defence "
            + "rolls is the Trait as it stands"));
    }

    /// <summary>
    /// The row one of Chapter 6's three tables prints for the item a caller named, and the table it
    /// is in — or null where none of them prints one, or where two of them do and the page gives no
    /// way to tell which.
    ///
    /// <para><b>The item is the caller's own words and the row is a printed type</b>, so the two
    /// are matched by the longest printed spelling that appears in the caller's phrase as a whole
    /// word: "a basic sword" is the ancient table's <c>Sword</c>, and "a battle axe" is
    /// <c>Battle Axe</c> rather than <c>Axe</c> because the longer name wins. It is deliberately a
    /// match and not a lookup — there is no inventory here and the object a fight opens with is a
    /// phrase somebody typed, which is the same reason <see cref="Combatant.Carrying"/> is the
    /// caller's word.</para>
    ///
    /// <para><b>Eight of the sixty-three rows are printed inverted, and the printed order is a sort
    /// key rather than a name.</b> The tables are alphabetical, so p.89 files the snub-nosed pistol
    /// under <c>Pistol, Snub</c> and the sniper's rifle under <c>Rifle, Sniper</c> — spellings
    /// nobody types. Matching the printed string alone therefore never reached any of the eight,
    /// and five of them silently collected <em>another weapon's</em> figure instead: "a sniper
    /// rifle" matched <c>Rifle</c> at +3d where the page prints +4d, "a snub pistol" matched
    /// <c>Pistol</c> at +2d where the page prints +1d, and "a spiked shield" matched the subdual
    /// <c>Shield</c> where <c>Shield, Spiked</c> is lethal. <b>A wrong match is worse than none</b>,
    /// because the ledger then names a printed weapon that is not the one in the caller's hands. So
    /// each row is matched by its printed name <em>and</em> by that name uninverted, and the
    /// comparison is on the length of the spelling that matched — which is what lets
    /// "Sniper Rifle" beat "Rifle".</para>
    ///
    /// <para><b>A hyphen is a space for this purpose</b>, so "a battle-axe" is <c>Battle Axe</c>
    /// and not <c>Axe</c>; that was the same silent-wrong-figure shape in one character.</para>
    ///
    /// <para><b>Two rows tied at the longest spelling refuse rather than pick one.</b> "his shield
    /// and dagger" names two printed weapons of the same length, and the first one the loop happened
    /// to reach is not an answer to which of them is being swung — that is a question for the person
    /// running the fight, and the ledger asks it. The rows are returned so the line can name both.
    /// A plural or a spelling no table carries stays a plain no-match, which the line already
    /// says.</para>
    ///
    /// <para><b>An item nothing matches gets no bonus and says so on the ledger</b>, rather than
    /// getting a plausible one. p.87 prices what the tables print; a rolled-up newspaper is the GM's
    /// to price, and inventing a figure for it is exactly what this store was verified to
    /// prevent.</para>
    /// </summary>
    private (EquipmentWeaponModel? Weapon, string Table, IReadOnlyList<string> Ambiguous) WeaponFor(
        string item)
    {
        var phrase = Unhyphenated(item);
        var best = 0;
        var found = new List<(EquipmentWeaponModel Row, string Table)>();

        foreach (var id in WeaponTables)
        {
            foreach (var row in _play.GetEquipment(id).Weapons!)
            {
                var matched = PrintedSpellings(row.Name)
                    .Where(spelling => NamesTheWeapon(phrase, Unhyphenated(spelling)))
                    .Select(spelling => spelling.Length)
                    .DefaultIfEmpty(0)
                    .Max();

                if (matched == 0 || matched < best) continue;

                if (matched > best)
                {
                    best = matched;
                    found.Clear();
                }

                found.Add((row, _play.GetEquipment(id).Name));
            }
        }

        return found switch
        {
            [] => (null, "", []),
            [var only] => (only.Row, only.Table, []),
            _ => (null, "", [.. found.Select(f => f.Row.Name)])
        };
    }

    /// <summary>
    /// The spellings of a printed weapon name a caller might type: the name as printed, and — where
    /// the table's alphabetical order inverted it — the same words the way round somebody says them.
    /// </summary>
    private static IEnumerable<string> PrintedSpellings(string name)
    {
        yield return name;

        var comma = name.IndexOf(", ", StringComparison.Ordinal);

        if (comma >= 0) yield return $"{name[(comma + 2)..]} {name[..comma]}";
    }

    /// <summary>A hyphen read as the space it stands in for, so "battle-axe" is "battle axe".</summary>
    private static string Unhyphenated(string text) => text.Replace('-', ' ');

    /// <summary>
    /// Whether <paramref name="item"/> names <paramref name="weapon"/> — a case-insensitive match
    /// on whole words, so "a basic sword" names the Sword and "swordfish" does not.
    /// </summary>
    private static bool NamesTheWeapon(string item, string weapon)
    {
        for (var at = item.IndexOf(weapon, StringComparison.OrdinalIgnoreCase);
             at >= 0;
             at = item.IndexOf(weapon, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var before = at == 0 || !char.IsLetterOrDigit(item[at - 1]);
            var after = at + weapon.Length == item.Length
                        || !char.IsLetterOrDigit(item[at + weapon.Length]);

            if (before && after) return true;
        }

        return false;
    }
}
