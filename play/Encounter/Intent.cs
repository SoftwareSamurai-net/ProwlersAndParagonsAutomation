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

/// <summary>
/// How much of a target is behind something (p.75's MODIFIERS).
///
/// <para><b>It is a property of one attack and not of the fight</b>, because it is: the page prices
/// cover by how much of the target it hides <em>from the attacker</em>, and the same wall hides a
/// character from one shooter and nobody else. <see cref="Complete"/> carries no band — the printed
/// table stops at "almost full" — and what the page says about a completely hidden target is that
/// you cannot hit one, which is a refusal made before anything is rolled rather than a penalty.
/// </para>
/// </summary>
public enum Cover
{
    /// <summary>Nothing in the way. The default, and what every measurement here is taken in.</summary>
    None,

    /// <summary>The page's first band.</summary>
    Light,

    /// <summary>The page's second.</summary>
    Heavy,

    /// <summary>The page's third, and the last one the table prices.</summary>
    AlmostFull,

    /// <summary>
    /// Hidden altogether. p.75: you cannot hit one — unless the attack goes through the cover, which
    /// is what <see cref="Attack.CoverStructure"/> declares.
    /// </summary>
    Complete
}

/// <summary>
/// What the light is like, which p.75 costs on attack rolls and on active defence rolls alike.
///
/// <para><b>It is a property of the scene and lives on the state</b> — the fog is the same fog for
/// everybody in it. What is <em>not</em> the scene's is an opponent nobody can see, which p.75 makes
/// a fact about a pair: <see cref="Combatant.Invisible"/> carries that half.</para>
/// </summary>
public enum Visibility
{
    /// <summary>Clear air. The default, and what every measurement here is taken in.</summary>
    Clear,

    /// <summary>Dim lighting, fog, smoke.</summary>
    Poor,

    /// <summary>Blindness or darkness — and what an invisible opponent counts as.</summary>
    None
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
    AvoidFatalDamage,

    /// <summary>Ch.4 p.79: one point stops a dying character's clock at once, without a roll.</summary>
    Stabilise
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
/// <param name="Team">
/// p.79: part of a team attack — two dice on, once per target per battle, and the sixes may be made
/// to explode for a point of Resolve afterwards.
///
/// <para><b>What this engine cannot express is the coordination.</b> A <see cref="Encounter.Step"/>
/// is one character's action, so "you and your allies have to wait until the end of the page" and
/// "you all have to target the same enemy" are clauses the ledger names rather than rules it
/// applies — see <c>docs/guide/play-engine.md</c>.</para>
/// </param>
/// <param name="Cover">
/// p.75: how much of the target is behind something, which costs the attacker one, two or three
/// dice — and which, at <see cref="Encounter.Cover.Complete"/>, means there is nothing to shoot at
/// unless the attack goes through the obstacle.
///
/// <para><b>It is on the attack because cover is a fact about a line of sight and not about the
/// scene.</b> The same wall hides a character from the shooter in front of it and from nobody
/// behind it, so a field on the fight would be a claim about everybody at once.</para>
/// </param>
/// <param name="CoverStructure">
/// The Structure rank of the obstacle, where the attack is being sent <em>through</em> it rather
/// than at whatever of the target is exposed — p.75's "if your attack rank exceeds the cover's
/// Structure, you can attack through it".
///
/// <para><b>Supplying it is the declaration.</b> Two printed consequences follow and both are
/// applied: the attack rank has to be greater than the Structure or nothing is rolled, and the
/// target may answer with the Structure as a passive defence. A caller who means to shoot at the
/// exposed part of a partly-covered target leaves it out and pays the band alone; a completely
/// hidden target cannot be hit without it.</para>
///
/// <para><b>The number stays the caller's and <see cref="CoverScenery"/> is the other way of
/// saying it.</b> Chapter 7's tables rate a material (p.107) and a thing (p.108), so a wall with a
/// printed row need not be a figure somebody typed; but p.107 also lets the GM move a Structure by
/// as much as four dice for how thick or how rotten the obstacle is — its own worked example does
/// exactly that — and no table prints every object in a city. So both ways in are kept, and
/// supplying both at once is refused rather than one of them silently winning.</para>
/// </param>
/// <param name="CoverScenery">
/// The obstacle by the name Chapter 7 prints for it, where the Structure is to come off the page
/// rather than out of the caller's head — p.107's Smashing table for a material and p.108's Scenery
/// table for a thing.
///
/// <para><b>It is an alternative to <see cref="CoverStructure"/> and never a modifier on it.</b>
/// Naming a row and supplying a figure are two answers to one question, so an attack carrying both
/// is refused with nothing rolled. A name nothing in either table prints is refused the same way:
/// the tables are what this engine can cite a page for, and inventing a figure for a rolled-up
/// hoarding is what a bare number is there for.</para>
///
/// <para><b>p.108's Massive Objects table is not one of the two</b>, because the page says so:
/// <c>uses_instead_of_body_or_structure</c> is "the object's weight rank", so those rows carry no
/// Structure and hiding behind a skyscraper is refused rather than answered with a weight.</para>
/// </param>
/// <param name="VulnerablePart">
/// p.80's Hard Targets: the attacker is aiming at "the vulnerable parts of a complex machine or
/// vehicle", which costs <c>penalty_dice_to_negate_it</c> and cancels the doubled passive defence.
///
/// <para><b>It is a declaration on one attack rather than a state of the target</b>, because the
/// page prices it that way — the attacker accepts a penalty on <em>their</em> roll, and the next
/// character to swing at the same machine may decline to. Whether the machine is complex enough to
/// have a weak point is the GM's, and the ledger line says so rather than pretending this engine
/// checked.</para>
/// </param>
/// <param name="CloseRangeOnly">
/// p.79's Close Range rule, and its own exception: this attack is one of the "ordinary thrown
/// weapons and other short-range attacks that can only be used at Close Range", so a dodger does
/// not lose the two dice for meeting it up close.
///
/// <para><b>It is the caller's word, and the default is the other way because the page's is.</b>
/// <c>range_classes</c>' <c>ranged_attacks_reach</c> is the rule — a ranged attack reaches Close
/// Range or Distant Range — and thrown weapons are printed beside it as an <em>exception</em>. So a
/// ranged attack reaches Distant unless somebody says otherwise, and this is where they say it.</para>
///
/// <para><b>It is no longer the only thing that can say it.</b> When this was written a fight had no
/// equipment in it and nothing could tell a pistol from a throwing knife but the person running the
/// fight; an attack names an <see cref="Item"/> now, and Chapter 6 prints <c>Thrown</c> against the
/// rows the rule is ignored for — so an attack made with one of those is exempt whether or not this
/// flag was set, which is <c>Encounter.ThrownItemIsExempt</c>. The declaration still covers what a
/// printed row cannot: an item no table carries, and a weapon being used in a way its row does not
/// describe.</para>
///
/// <para>It changes nothing for an attack the rule never reached — a fist, a sword, a Power whose
/// own Range is not <c>ranged</c> — and the ledger says so rather than leaving a caller thinking
/// they bought something.</para>
/// </param>
/// <param name="Item">
/// The item this attack is made with, where it is the one p.76's full grab has just put in the
/// actor's hands — "you gain control of the object and can use it or toss it aside on that same
/// page" — or the one they walked into the fight carrying.
///
/// <para><b>It is refused unless the actor is actually holding it</b>, by name and with nothing
/// rolled. This engine has no inventory and never claims to: two things reach
/// <see cref="Combatant.Holding"/> and both are somebody's word said out loud — an opening hand
/// declared when the fight was built (<see cref="Combatant.Carrying"/>) and an object a full grab
/// took off its holder. An attack naming anything else is a claim about equipment nothing here can
/// answer for.</para>
///
/// <para><b>And it is refused while a partial grab is being fought over it</b>, for either party:
/// p.76's half-measure says "they can't use it, but neither can you".</para>
///
/// <para><b>It moves the pool, and only through p.87's Gear Limit.</b> On p.75's two weapon rows the
/// actor's Trait rank is capped at the limit in force and the item's printed Weapon Bonus is added
/// to what is left — a figure read out of <c>data/rules/play/equipment.json</c> rather than one this
/// engine has an opinion about. It still moves neither the row of p.75's table nor the damage: which
/// row an attack is on and what it inflicts are the caller's, and an item nothing in Chapter 6
/// prints is capped and adds nothing, with the ledger saying the figure is the GM's. See the Gear
/// Limit in <c>docs/guide/play-engine.md</c>.</para>
///
/// <para><b>It also decides p.79's Close Range exemption where the row carries it</b>, because the
/// printed row knows what <see cref="CloseRangeOnly"/> was written not to know: an item Chapter 6
/// prints as <c>Thrown</c> is one of the weapons that rule is ignored for. And it spends the page
/// p.76 gives the winner, so an item that was used stays with them past the page turn instead of
/// being tossed aside at it.</para>
/// </param>
public sealed record Attack(
    string Actor,
    string Target,
    string TraitId,
    DamageKind Damage = DamageKind.Lethal,
    AttackType Type = AttackType.Unarmed,
    string? Effect = null,
    bool AllOut = false,
    bool Charge = false,
    bool Area = false,
    bool Team = false,
    Cover Cover = Cover.None,
    int? CoverStructure = null,
    bool VulnerablePart = false,
    bool CloseRangeOnly = false,
    string? Item = null,
    string? CoverScenery = null) : Intent(Actor);

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
/// <param name="Actor">Who is making the move.</param>
/// <param name="Target">Who they are making it against.</param>
/// <param name="Move">A grab, a hold or an escape.</param>
/// <param name="Item">
/// What is being grabbed, in the caller's own words — required by <see cref="GrappleMove.Grab"/> and
/// meaningless on the other two.
///
/// <para><b>A grab that names nothing is refused with nothing rolled, and that is the page's own
/// distinction rather than a validation rule.</b> p.76 defines a grab as "an attempt to take a weapon
/// or other handheld item away from your opponent" and a hold as "an attempt to control or restrain
/// your opponent" — so a grab with no object is a hold by another name, and resolving one would put a
/// contest over nothing in particular on the ledger and, on three net successes, hand somebody
/// control of it.</para>
///
/// <para><b>It is the caller's word, for the reason <see cref="Combatant.Size"/> and
/// <see cref="Combatant.Invisible"/> are.</b> There is no inventory here and no sheet this engine may
/// read carries one — see <see cref="HeldItem"/>. What the engine does with the word is exactly what
/// p.76 states: a partial grab records it as the thing both characters have hold of, and a full grab
/// moves it.</para>
///
/// <para><b>And the target has to be recorded as holding it</b>, or the grab is refused with nothing
/// rolled: p.76 takes an item "away from your opponent", so a grab presupposes an opponent who has
/// one. <see cref="Combatant.Carrying"/> is how a fight is opened with somebody armed. The one
/// exception is the object a partial grab between the pair is already over, which is in nobody's
/// hands and which the page settles by exactly these rolls.</para>
/// </param>
public sealed record GrappleIntent(
    string Actor, string Target, GrappleMove Move, string? Item = null) : Intent(Actor);

/// <summary>
/// p.76: throwing away the item a full grab has just won — "you can use it or toss it aside on that
/// same page without suffering a multiple action penalty".
///
/// <para><b>It is an intent of its own rather than a flag on <see cref="EndTurn"/> because it is a
/// thing a character does</b>, and the ledger has to be able to say they did it: an item tossed and
/// an item merely dropped when the page turned are different events, and only one of them was
/// somebody's decision.</para>
///
/// <para><b>It does not use up the turn</b>, which is the page's "in effect, a free action". Nothing
/// in this engine counts actions per page — multiple actions and their −2d are on the guide's list of
/// mechanics with no intent yet — so what the clause buys here is that the actor may still take their
/// ordinary action, and a fixture drives exactly that rather than asserting a penalty that is not
/// modelled.</para>
/// </summary>
/// <param name="Actor">Who is throwing it away.</param>
/// <param name="Item">Which item — refused by name unless it is the one they are holding.</param>
public sealed record Toss(string Actor, string Item) : Intent(Actor);

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
/// <param name="Target">
/// Somebody the purchase is aimed at, for the one purchase that aims at anybody:
/// <see cref="ResolveSpend.Luring"/>, whose <c>may_redirect_onto_a_person</c> sends the attack the
/// buyer dodged into a person rather than into the scenery (p.79).
///
/// <para><b>It is optional because no other purchase has anywhere to point.</b> A spend that named
/// a target everywhere would invite one on the six that do not, and the guide's list of what this
/// engine cannot reach — p.79's Resolve spent on damage inflicted on somebody else — is a note
/// about exactly that.</para>
/// </param>
/// <param name="SolidObject">
/// What a knocked-back target hits, by the name Chapter 7 prints for it — p.78's "if the target
/// hits a solid object, they suffer half as much damage as the original attack inflicted".
///
/// <para><b>It is optional and the clause is what turns on it.</b> p.78 knocks a target across the
/// room whether or not there is anything in the way, so a purchase that names nothing is the whole
/// rule minus its last sentence, and the ledger says which clause was not applied. Naming something
/// is what turns that sentence on.</para>
///
/// <para><b>An object no page rates is refused with nothing spent</b>, the shape p.79's lure that
/// names nobody is refused in: the extra damage is priced off the object's Structure, and a figure
/// invented for it would be damage on the ledger with no page behind it. p.108's Massive Objects
/// rows are refused for the same reason — the page gives them a weight rank instead of a
/// Structure.</para>
/// </param>
public sealed record SpendResolve(
    string Actor, ResolveSpend Kind, int Points = 1, string? Target = null,
    string? SolidObject = null) : Intent(Actor);

/// <summary>One of the GM's purchases out of the Adversity pool.</summary>
/// <param name="Actor">The NPC the point is spent on behalf of.</param>
/// <param name="Kind">Which of p.85's four purchases.</param>
/// <param name="Points">How many points.</param>
/// <param name="AsResolve">
/// For <see cref="AdversitySpend.AnythingResolveCan"/>: which Resolve purchase the GM is buying.
/// p.85 says a point of Adversity does "whatever a point of Resolve could have done, on behalf of
/// any NPC", so the purchase has to be named — a spend that did not say which one would be a point
/// spent on nothing in particular.
/// </param>
/// <param name="Target">
/// Where the purchase points, for the one purchase that points anywhere — the same field
/// <see cref="SpendResolve.Target"/> carries, so p.85's "whatever a point of Resolve could have
/// done" can do the whole of what that purchase does rather than most of it.
/// </param>
/// <param name="Narration">
/// What the GM says the point buys, in their own words, for the three purchases whose effect is a
/// thing that happens in the fiction rather than to a roll.
///
/// <para><b>It is required by those three and refused when it is missing, because the alternative
/// is a point spent on nothing.</b> p.85 defines a misfortune by three examples and two
/// prohibitions and attaches no roll, threshold or duration to it; an act of villainy is "anything
/// necessary to advance the story"; a suppressed Flaw is a named weakness on a character this
/// engine holds no Flaws for. In every one of the three the mechanical half is the point leaving
/// the pool and the rest is the GM's, so the ledger carries their sentence — a purchase recorded
/// as having happened and nothing said about what it was is a line nobody can narrate from and
/// nobody can audit. It is the same refusal p.79's luring makes of a lure that names nobody.</para>
/// </param>
/// <param name="SolidObject">
/// What a knocked-back target hits, for the same reason <paramref name="Target"/> is here: p.85's
/// first purchase is the Resolve purchases with the GM's money behind them, so a knockback bought
/// out of the pool has to be able to say what the NPC's victim was thrown into. See
/// <see cref="SpendResolve.SolidObject"/>.
/// </param>
public sealed record SpendAdversity(
    string Actor,
    AdversitySpend Kind,
    int Points = 1,
    ResolveSpend? AsResolve = null,
    string? Target = null,
    string? Narration = null,
    string? SolidObject = null) : Intent(Actor);

/// <summary>
/// p.79's Fatal Damage rule: spending a turn steadying somebody who is bleeding out, rolling the
/// Trait <c>gritty_fatal_damage.stabilise_roll</c> names against the threshold beside it.
///
/// <para>A character may steady themselves — the entry puts no range or helper on it — and the roll,
/// the difficulty and the threshold are all the entry's.</para>
/// </summary>
/// <param name="Actor">Who is making the roll.</param>
/// <param name="Target">Who is bleeding out.</param>
public sealed record Stabilise(string Actor, string Target) : Intent(Actor);

/// <summary>Done: pass the turn to whoever is next in the order.</summary>
public sealed record EndTurn(string Actor) : Intent(Actor);

/// <summary>
/// p.73: everybody has acted or skipped, so the page ends and a new one opens — durations tick down
/// and anybody still holding an action loses it.
/// </summary>
public sealed record EndPage(string Actor) : Intent(Actor);
