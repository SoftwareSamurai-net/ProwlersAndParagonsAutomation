namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>Which sort of damage an attack does, which decides how much Toughness answers it (p.75).</summary>
public enum DamageKind
{
    /// <summary>The kind that kills. Toughness answers at half.</summary>
    Lethal,

    /// <summary>The kind that knocks out. Toughness answers in full.</summary>
    Subdual,

    /// <summary>
    /// Damage to the mind, which p.75 puts outside the pair and has Willpower answer.
    /// </summary>
    Psychic
}

/// <summary>
/// Which row of p.75's Attack and Defense table an attack comes from.
///
/// <para><b>The table is what decides which Traits may answer an attack</b>, and the five rows are
/// not interchangeable: only the unarmed row lets a target soak with the whole of their Toughness,
/// and the mental row takes it away altogether and offers Willpower or a Power instead. An engine
/// that offered every defence to every attack was letting a target answer a Mind Control with their
/// Toughness.</para>
///
/// <para>It is separate from <see cref="DamageKind"/> because the two answer different questions —
/// this one says what may be rolled, and that one says how much of a Toughness counts.</para>
/// </summary>
public enum AttackType
{
    /// <summary>Fists. The one row that lets the target answer with the whole of their Toughness.</summary>
    Unarmed,

    /// <summary>A weapon swung in close combat: Might attacking, half a Toughness answering.</summary>
    MeleeWeapon,

    /// <summary>Something aimed: Agility attacking, half a Toughness answering.</summary>
    RangedWeapon,

    /// <summary>A Power that does something physical — the Power's own rank rolls itself.</summary>
    PhysicalPower,

    /// <summary>A Power aimed at the mind. Willpower or a Power answers it; Toughness does not.</summary>
    MentalPower
}

/// <summary>Which of the three grappling moves p.76 gives rules to.</summary>
public enum GrappleMove
{
    /// <summary>Taking a weapon or other handheld item away from an opponent.</summary>
    Grab,

    /// <summary>Controlling or restraining an opponent.</summary>
    Hold,

    /// <summary>Breaking out of a hold.</summary>
    Escape
}

/// <summary>
/// The Resolve purchases Chapter 5 and Chapter 4 print. Each names the entry it comes from; the
/// ones this slice does not resolve leave a ledger line saying so, by name.
/// </summary>
public enum ResolveSpend
{
    /// <summary>Ch.5 p.84: one point, one die, decided after the roll, with no cap.</summary>
    ExtraDice,

    /// <summary>Ch.5 p.84: one point picks the whole challenge roll back up.</summary>
    Reroll,

    /// <summary>Ch.4 p.73: one point buys a place in front of everyone for the rest of the fight.</summary>
    SeizeInitiative,

    /// <summary>Ch.4 p.76: one point carries a defeating special effect into the next scene.</summary>
    KeepingHold,

    /// <summary>Ch.4 p.76: one point brings a defeated character round, or shakes off an effect.</summary>
    InstantRecovery,

    /// <summary>Ch.4 p.78: one point turns a heavy subdual blow into a spectacular flight.</summary>
    Knockback,

    /// <summary>Ch.4 p.79: one point redirects a dodged attack into whatever was behind you.</summary>
    Luring,

    /// <summary>Ch.4 p.79: one point makes a team attack's sixes explode.</summary>
    TeamAttack,

    /// <summary>Ch.4 p.79: one point buys the damage down from the Fatal Damage threshold.</summary>
    AvoidFatalDamage
}

/// <summary>The GM's purchases, from Ch.5 pp.85–86.</summary>
public enum AdversitySpend
{
    /// <summary>p.85: whatever a point of Resolve could have done, on behalf of any NPC.</summary>
    AnythingResolveCan,

    /// <summary>p.85: suppress an NPC's Flaw for a scene.</summary>
    SuppressFlaw,

    /// <summary>p.85: a piece of misfortune that is a challenge rather than a punishment.</summary>
    Misfortune,

    /// <summary>p.85: an act of villainy the Heroes cannot simply prevent.</summary>
    Villainy
}

/// <summary>
/// What a combatant is about to try.
///
/// <para><b>An intent is a request, never an outcome.</b> The engine decides whether the rules allow
/// it and what it produces; nothing here carries a result, and nothing here carries a number the
/// data could have answered. That is the same discipline the character engine works under — the
/// model proposes and the engine decides — turned around to face a fight.</para>
///
/// <para><b>An intent this slice does not resolve is refused out loud.</b> It leaves a ledger line
/// naming itself and changes nothing, rather than being quietly dropped: a silent no-op is
/// indistinguishable from a rule that ran and had no effect, and the difference is the whole
/// question a balance measurement turns on.</para>
/// </summary>
public abstract record Intent(string Actor);

/// <summary>
/// An attack: a contested roll where the target's own defence roll is the threshold (p.75).
/// </summary>
/// <param name="Actor">Who is attacking.</param>
/// <param name="Target">Who they are attacking.</param>
/// <param name="TraitId">The Trait or Power rolled — accuracy and damage are one figure (p.75).</param>
/// <param name="Damage">Lethal, subdual, or psychic — how much of a Toughness answers it (p.75).</param>
/// <param name="Type">
/// Which row of p.75's Attack and Defense table this is, which decides what may answer it.
/// </param>
/// <param name="Effect">
/// The name of the special effect this attack inflicts instead of damage — Ensnare, Mind Control,
/// Stun and the like (p.76) — or null for an ordinary damaging attack.
/// </param>
/// <param name="AllOut">p.78: two dice on, every defence halved until after the attacker's next turn.</param>
/// <param name="Charge">p.78: two dice on, own active defences halved, and the impact comes back.</param>
/// <param name="Area">p.77–78: covers everyone in the area, and doubles the rate against Minions.</param>
public sealed record Attack(
    string Actor,
    string Target,
    string TraitId,
    DamageKind Damage = DamageKind.Lethal,
    AttackType Type = AttackType.Unarmed,
    string? Effect = null,
    bool AllOut = false,
    bool Charge = false,
    bool Area = false) : Intent(Actor);

/// <summary>
/// Closing with or opening from one other combatant (p.74).
/// </summary>
/// <param name="Actor">Who is moving.</param>
/// <param name="Toward">Who they are moving relative to.</param>
/// <param name="Closer">True to close a range class, false to open one.</param>
public sealed record Move(string Actor, string Toward, bool Closer = true) : Intent(Actor);

/// <summary>p.73: keep the turn in reserve for a cue later on the same page, and lose it if none comes.</summary>
public sealed record Hold(string Actor) : Intent(Actor);

/// <summary>p.76: a grab, a hold or an escape — Might against Might, read off the Grappling table.</summary>
public sealed record GrappleIntent(string Actor, string Target, GrappleMove Move) : Intent(Actor);

/// <summary>
/// p.76: spending a turn wrestling with a special effect, rolling the passive defence the Power
/// names against the Power's own rank, and taking half your net successes off the duration.
/// </summary>
/// <param name="Actor">Who is trying to get loose.</param>
/// <param name="TraitId">The passive defence the Power's description names.</param>
/// <param name="Threshold">The Power's rank, which is the threshold the roll is measured against.</param>
public sealed record BreakFree(string Actor, string TraitId, int Threshold) : Intent(Actor);

/// <summary>One of Chapter 4's and Chapter 5's Resolve purchases, by a Hero.</summary>
/// <param name="Actor">Who is paying. A throw if they hold no Resolve.</param>
/// <param name="Kind">Which purchase.</param>
/// <param name="Points">How many points, for the one purchase that takes more than one.</param>
public sealed record SpendResolve(string Actor, ResolveSpend Kind, int Points = 1) : Intent(Actor);

/// <summary>One of the GM's purchases out of the Adversity pool.</summary>
public sealed record SpendAdversity(string Actor, AdversitySpend Kind, int Points = 1) : Intent(Actor);

/// <summary>Done: pass the turn to whoever is next in the order.</summary>
public sealed record EndTurn(string Actor) : Intent(Actor);

/// <summary>
/// p.73: everybody has acted or skipped, so the page ends and a new one opens — durations tick down
/// and anybody still holding an action loses it.
/// </summary>
public sealed record EndPage(string Actor) : Intent(Actor);
