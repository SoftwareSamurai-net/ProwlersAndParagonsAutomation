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
        var (weapon, table) = WeaponFor(item);
        var bonus = weapon?.BonusDice ?? 0;

        var raised = state.Table.RaisedGearLimit && state.Table.GearLimitRank is not null
            ? $" — this table raised it from {limit.DefaultRank}d, which is the switch p.80 offers"
            : "";

        var carried = capped == rank
            ? $"{actor.Name}'s {attack.TraitId} of {rank}d is under it and comes to bear whole"
            : $"{actor.Name}'s {attack.TraitId} of {rank}d comes to bear as {capped}d";

        var added = weapon is null
            ? $"Chapter 6 prints no weapon called {item}, so it adds nothing here and what a Weapon "
              + "Bonus for it would be is the GM's"
            : weapon.BonusDice is null
                ? $"{table} prints a dash rather than a figure for {weapon.Name}, so it adds nothing "
                  + "of its own"
                : $"{item} is {table}'s {weapon.Name} at +{weapon.BonusDice}d";

        lines.Add(new LedgerLine(
            state.Page, actor.Id, entry.Id, entry.SourceRef,
            $"p.87 caps {limit.WhatItIs} at {ceiling}d{raised}, so {carried}; {added}, for an "
            + $"attack rank of {capped + bonus}d"));

        return capped + bonus;
    }

    /// <summary>
    /// The row one of Chapter 6's three tables prints for the item a caller named, and the table it
    /// is in — or null where none of them prints one.
    ///
    /// <para><b>The item is the caller's own words and the row is a printed type</b>, so the two
    /// are matched by the longest printed name that appears in the caller's phrase as a whole word:
    /// "a basic sword" is the ancient table's <c>Sword</c>, and "a battle axe" is <c>Battle Axe</c>
    /// rather than <c>Axe</c> because the longer name wins. It is deliberately a match and not a
    /// lookup — there is no inventory here and the object a fight opens with is a phrase somebody
    /// typed, which is the same reason <see cref="Combatant.Carrying"/> is the caller's word.</para>
    ///
    /// <para><b>An item nothing matches gets no bonus and says so on the ledger</b>, rather than
    /// getting a plausible one. p.87 prices what the tables print; a rolled-up newspaper is the GM's
    /// to price, and inventing a figure for it is exactly what this store was verified to
    /// prevent.</para>
    /// </summary>
    private (EquipmentWeaponModel? Weapon, string Table) WeaponFor(string item)
    {
        EquipmentWeaponModel? best = null;
        var table = "";

        foreach (var id in WeaponTables)
        {
            foreach (var row in _play.GetEquipment(id).Weapons!)
            {
                if (!NamesTheWeapon(item, row.Name)) continue;
                if (best is not null && row.Name.Length <= best.Name.Length) continue;

                best = row;
                table = id;
            }
        }

        return (best, table);
    }

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
