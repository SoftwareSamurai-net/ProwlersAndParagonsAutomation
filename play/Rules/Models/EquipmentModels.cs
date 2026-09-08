// Written from the shipped data/rules/play/equipment.json. Every key of every entry in that file
// has a property here, because PlayRulesFileCoverageTests deserializes it with
// JsonUnmappedMemberHandling.Disallow: a field nothing reads is a field nothing can hold to the
// rulebook.
//
// Prose, `ambiguity` and `interpretation` are modelled too, for the reason the other five files'
// models model them: a reader of an entry needs the reading beside the number. What the engine may
// *consume* is narrower — see docs/guide/play-engine.md.

namespace ProwlersAndParagonsAutomation.Play.Rules.Models;

/// <summary>
/// One row of one of Chapter 6's three weapons tables (pp.89–90).
/// </summary>
/// <param name="Name">The type as printed — "Sword", "Pistol", "Vibro Knife".</param>
/// <param name="Class">"Melee" or "Ranged", as the table's second column prints it.</param>
/// <param name="BonusDice">
/// The Weapon Bonus in dice, or null where the table prints a dash instead of a figure.
///
/// <para><b>Null is a printed cell and not a missing one.</b> The Grenade Launcher has no bonus of
/// its own because what it fires is bought separately, which p.88's <c>Launcher</c> feature says in
/// as many words. A zero would be a different claim — the Throwing Star's <c>+0d</c> and the Riot
/// Shield's <c>+0d (s)</c> are both printed as figures.</para>
/// </param>
/// <param name="Subdual">
/// Whether the bonus is printed with an "(s)" beside it. p.88: most weapons inflict lethal damage
/// and the marker is what says otherwise.
/// </param>
/// <param name="Features">The features column, as the printed names — an empty list for a dash.</param>
public sealed record EquipmentWeaponModel(
    string Name,
    string Class,
    int? BonusDice,
    bool Subdual,
    IReadOnlyList<string> Features);

/// <summary>p.87's Gear Limit: the ceiling mundane equipment puts on a Trait.</summary>
public sealed record EquipmentGearLimitModel(
    string WhatItIs,
    int DefaultRank,
    string Usually,
    string MaximumEffectiveRankIs,
    string WorkedExampleWeapon,
    int WorkedExampleWeaponBonusDice,
    string WorkedExampleTrait,
    int WorkedExampleTraitRank,
    int WorkedExampleMaximumEffectiveRank,
    bool TraitCapIsADifferentThing,
    int StandardPowerLevelTraitCap,
    string MakesMundaneGearLessUsefulFor);

/// <summary>p.87's one exception: a melee weapon may be swung on a bare-handed figure instead.</summary>
public sealed record EquipmentCloseCombatExceptionModel(
    string AppliesTo,
    string Condition,
    string YouMayUseInstead,
    bool AtTheWieldersOption,
    string WorkedExampleWeapon,
    int WorkedExampleWeaponBonusDice,
    int WorkedExampleGearLimit,
    int WorkedExampleArmedMaximumEffectiveRank,
    int WorkedExampleUnarmedRank,
    string WhatTheWeaponStillBuys);

/// <summary>p.87's options for lifting the ceiling, and its warning about doing so.</summary>
public sealed record EquipmentRaisedLimitModel(
    IReadOnlyList<int> RaisedOptions,
    bool RaisedOptionsAreOpenEnded,
    bool MayBeDisregardedEntirely,
    string Suits,
    int PowersAreOvershadowedUnlessTheTraitCapExceedsTheGearLimitBy,
    IReadOnlyList<string> BalanceOptionsGiven,
    int BalanceOptionBonusDice,
    string BalanceOptionsExclude);

/// <summary>p.88: what a Weapon Bonus is added to, and what a weapon's damage is.</summary>
public sealed record EquipmentWeaponBonusModel(
    bool EveryWeaponHasOne,
    IReadOnlyList<string> MeleeAttackTraits,
    IReadOnlyList<string> MeleeDefenseTraits,
    IReadOnlyList<string> RangedAttackTraits,
    string AddedTo,
    string SubdualMarker,
    string DefaultDamage,
    string AncientAndModernDamage,
    string AdvancedDamage,
    IReadOnlyList<string> AdvancedPhysicalExceptions,
    string RangedWeaponsReach,
    IReadOnlyList<string> RangedReachExceptions);

/// <summary>
/// This project's reading of how a weapons table's printed columns line up, kept apart from the
/// transcription of it for the reason <c>play-rules.md</c> states: a fact field is a claim that the
/// page states the thing, and the pairing is a claim about the extraction.
/// </summary>
public sealed record EquipmentInterpretationModel(string WhatThisIs, string RowAlignment);

/// <summary>One entry of <c>equipment.json</c> — Chapter 6, pp.87–90.</summary>
public sealed record EquipmentEntry(
    string Id,
    string Name,
    string Kind,
    string PrintedUnder,
    string Description,
    IReadOnlyList<string> VerifiedFields,
    string SourceRef,
    IReadOnlyList<string>? CorroboratedBy,
    string? Ambiguity,
    EquipmentGearLimitModel? GearLimit,
    EquipmentCloseCombatExceptionModel? CloseCombatException,
    EquipmentRaisedLimitModel? RaisedLimit,
    EquipmentWeaponBonusModel? WeaponBonus,
    IReadOnlyList<EquipmentWeaponModel>? Weapons,
    EquipmentInterpretationModel? Interpretation);
