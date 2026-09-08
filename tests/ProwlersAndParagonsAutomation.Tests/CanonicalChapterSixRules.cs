namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Chapter 6's Gadgets, Vehicles and Headquarters rules, printed pp.94-104, transcribed from the
/// rulebook. This is what <c>data/rules/gadgets.json</c>, <c>vehicles.json</c> and
/// <c>headquarters.json</c> are checked against.
///
/// <para><b>Same standing as <see cref="CanonicalPowers"/> and
/// <see cref="CanonicalEquipmentRules"/>: this file is the rulebook.</b> Do not "fix" a failing
/// test by editing a value here. Check the page the entry's <c>source_ref</c> names, and fix
/// whichever side is wrong.</para>
///
/// <para><b>Why a dictionary keyed by path rather than a record per entry.</b> The three files
/// carry 183 fact leaves between them across twenty-eight scalar entries. Written as records that
/// is twenty-eight types nobody reads, and — worse — the reflection walk that proves every leaf is
/// compared to something would have to be told, type by type, which record answers which entry. A
/// path key does both jobs at once: the walk builds the same key from the JSON and asks whether
/// this dictionary has it, so <b>a field added to a data file with no canonical value fails by
/// name</b>, and a field deleted from a data file fails as a canonical key nothing reached. The
/// printed sentence each group comes from is in the comment above it, which is the part that makes
/// this the rulebook rather than a copy of the data.</para>
///
/// <para><b>What is deliberately not in here: the four tables.</b> Seventy-eight table rows typed
/// out a second time would be a second transcription to disagree with the first, and the corpus
/// already carries the printed page. <see cref="Chapter6RulesDataTests"/> derives every mundane
/// vehicle row, every stock vehicle and every feature price out of
/// <c>data/rulebook/ch06-equipment.json</c> and compares. What this file carries instead is the
/// <b>counts</b> and a handful of <b>anchors</b>, because a derivation cannot notice a table that
/// has lost half of itself when the expectation lost the same half.</para>
/// </summary>
public static class CanonicalChapterSixRules
{
    /// <summary>The chapter's printed range. This slice transcribes pp.94-103; p.104 has no text.</summary>
    public const int FirstPage = 94;

    /// <inheritdoc cref="FirstPage"/>
    public const int LastPage = 104;

    /// <summary>The last page of the chapter that carries any prose at all.</summary>
    public const int LastPageWithText = 103;

    /// <summary>
    /// Every fact leaf of every scalar entry, keyed <c>entry_id.payload_key.field</c>. The comment
    /// above each group is the printed text it was taken from.
    ///
    /// <para><b>Values are <c>int</c>, <c>string</c>, <c>bool</c> or <c>string[]</c>, and nothing
    /// else.</b> The comparison in the walk is by JSON kind, so a canonical <c>6</c> against a data
    /// file's <c>"6"</c> fails rather than passing through a loose conversion.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, object> Facts = new Dictionary<string, object>
    {
        // ── gadgets.json ─────────────────────────────────────────────────────
        // Building a Gadget
        // p.94 GADGETS: "To create a Gadget, you need to have at least 6d Technology. You also need
        // access to tools, materials, and facilities." … "The maximum number of Gadgets you can create
        // per issue equals half your Intellect."
        ["gadget_prerequisites.prerequisites.minimum_technology_rank"]   = 6,
        ["gadget_prerequisites.prerequisites.also_needs"]                = new[] { "tools", "materials", "facilities" },
        ["gadget_prerequisites.prerequisites.maximum_per_issue_formula"] = "half the builder's Intellect",
        ["gadget_prerequisites.prerequisites.maximum_per_issue_trait"]   = "intellect",
        ["gadget_prerequisites.prerequisites.maximum_per_issue_divisor"] = 2,

        // Step 1: Assign Complexity
        // p.94 GADGETS: "Step 1: Assign Complexity. Assign the Gadget a Complexity of anywhere from 3 to
        // your Technology rank. A gadget's Complexity determines how powerful it will be and how hard it
        // will be to create."
        ["gadget_complexity.complexity.minimum"]         = 3,
        ["gadget_complexity.complexity.maximum_formula"] = "the builder's Technology rank",
        ["gadget_complexity.complexity.maximum_trait"]   = "technology",
        ["gadget_complexity.complexity.governs"]         = new[] { "how powerful the Gadget will be", "how hard it will be to create" },

        // Step 2: The Challenge Roll
        // p.94 GADGETS: "Step 2: The Challenge Roll. Make a Technology roll using the gadget's Complexity
        // as the threshold. If successful, you build the Gadget… If you succeed by only 1 or 2 points, the
        // gadget will have some unique flaw, quirk, or side-effect, the nature of which is entirely up to
        // the GM." … "Whether or not you succeed, creating a Gadget takes one scene that lasts about 10
        // minutes times the item's Complexity."
        ["gadget_challenge_roll.challenge_roll.trait_rolled"]                             = "technology",
        ["gadget_challenge_roll.challenge_roll.threshold_is"]                             = "the Gadget's Complexity",
        ["gadget_challenge_roll.challenge_roll.on_success"]                               = "the Gadget is built",
        ["gadget_challenge_roll.challenge_roll.marginal_success_minimum"]                 = 1,
        ["gadget_challenge_roll.challenge_roll.marginal_success_maximum"]                 = 2,
        ["gadget_challenge_roll.challenge_roll.marginal_success_effect"]                  = "the Gadget carries a flaw, quirk or side-effect chosen by the GM",
        ["gadget_challenge_roll.challenge_roll.marginal_success_decided_by"]              = "gm",
        ["gadget_challenge_roll.challenge_roll.scene_minutes_per_complexity"]             = 10,
        ["gadget_challenge_roll.challenge_roll.time_is_spent_whether_or_not_it_succeeds"] = true,

        // Step 3: Build the Gadget
        // p.94 GADGETS: "Step 3: Build the Gadget. If you make the roll, you gain a number of Hero Points
        // equal to double the item's Complexity to buy Abilities, Talents, and Powers that represent your
        // new Gadget. By default, all Gadgets have the Item Con. You get no extra points for that. However,
        // you can give your Gadget any other Pros and Cons you wish. Optionally, you can also use these
        // Hero Points to modify mundane gear as discussed above."
        //
        // This is the one price in the whole chapter that runs the other way: a Gadget is not bought with
        // Hero Points, it pays them out. Nothing about it touches a character's own budget.
        ["gadget_build.build.hero_points_granted_multiplier"]  = 2,
        ["gadget_build.build.hero_points_granted_formula"]     = "double the Gadget's Complexity",
        ["gadget_build.build.spendable_on"]                    = new[] { "abilities", "talents", "powers" },
        ["gadget_build.build.default_con"]                     = "item",
        ["gadget_build.build.default_con_is_credited"]         = false,
        ["gadget_build.build.other_pros_and_cons_allowed"]     = true,
        ["gadget_build.build.may_instead_modify_mundane_gear"] = true,

        // Instability
        // p.94 GADGETS: "Gadgets are inherently unstable and often temperamental. Roll one die each time
        // you use one. When you roll less than the number of times you've used it, the Gadget stops
        // working."
        ["gadget_instability.instability.dice_rolled_per_use"] = 1,
        ["gadget_instability.instability.fails_when"]          = "the die rolls lower than the number of times the Gadget has been used",
        ["gadget_instability.instability.on_failure"]          = "the Gadget stops working",
        ["gadget_instability.instability.roll_is_made"]        = "each time the Gadget is used",

        // Other Talents
        // p.94 GADGETS: "You can also use Science to create Gadgets that represent compounds and materials
        // or Medicine to create Gadgets that represent drugs and medicines. If your GM allows it, you might
        // even be able to create magical or techno-magical Gadgets… the GM will have to determine which
        // Talent applies… Regardless of what you create, the maximum number of Gadgets you can create per
        // issue always equals half your Intellect."
        ["gadget_talents.talents.technology_makes"]                       = "devices and machines",
        ["gadget_talents.talents.science_makes"]                          = "compounds and materials",
        ["gadget_talents.talents.medicine_makes"]                         = "drugs and medicines",
        ["gadget_talents.talents.gm_may_allow_magical_or_techno_magical"] = true,
        ["gadget_talents.talents.gm_chooses_the_talent_for_those"]        = true,
        ["gadget_talents.talents.per_issue_ceiling_is_unchanged"]         = "half the builder's Intellect",

        // What these rules are for
        // p.94 GADGETS: "These rules cover the spur-of-the-moment, emergency kitbashing… Characters who
        // always have at least a few gadgets at the ready will normally have a Power like Omni-Power
        // (Gadgets). However, if your GM allows it, you may be able to use these rules to create Gadgets
        // before an adventure begins. If so, these Gadgets will still count against the total number of
        // Gadgets you can create per issue."
        ["gadget_scope.scope.intended_for"]                                         = "improvised building in the middle of a story",
        ["gadget_scope.scope.standing_supply_is_a_power"]                           = "Omni-Power (Gadgets)",
        ["gadget_scope.scope.gm_may_allow_building_before_an_adventure"]            = true,
        ["gadget_scope.scope.pre_built_still_counts_against_the_per_issue_ceiling"] = true,

        // ── vehicles.json ────────────────────────────────────────────────────
        // Body
        // p.94 BODY: "This characteristic measures a vehicle's durability, taking defenses like
        // countermeasures and energy screens into account. Except as noted under Capital Ships later in
        // this section, a vehicle's Body counts as both Armor and Health. A vehicle's Body also protects
        // its passengers. Characters inside a vehicle can use its Body as their passive defense against
        // attacks coming from outside the vehicle unless the vehicle has an open cockpit."
        ["vehicle_body.body.measures"]                     = "durability, counting countermeasures and screens",
        ["vehicle_body.body.counts_as"]                    = new[] { "armor", "health" },
        ["vehicle_body.body.exception"]                    = "capital ships, whose Health is a separate figure",
        ["vehicle_body.body.protects_passengers"]          = true,
        ["vehicle_body.body.passengers_use_it_as"]         = "passive defense against attacks from outside the vehicle",
        ["vehicle_body.body.protection_lost_with_feature"] = "open_cockpit",

        // Speed
        // p.94 SPEED: "This characteristic determines how fast a vehicle can move and how long it takes to
        // travel from one place to another. It's the equivalent of a Travel Power for these purposes. If a
        // vehicle has more than one mode of movement, it should have a different Speed rank for each."
        ["vehicle_speed.speed.measures"]                        = "how fast the vehicle moves and how long a journey takes",
        ["vehicle_speed.speed.equivalent_to"]                   = "a Travel Power",
        ["vehicle_speed.speed.separate_rank_per_movement_mode"] = true,

        // Control
        // p.94 CONTROL: "This characteristic is a modifier that reflects how well the vehicle performs. It
        // covers everything from handling and maneuverability to targeting systems and fire controls.
        // Whenever the rules call for a Control roll, you make a Vehicles roll modified by your vehicle's
        // Control. Capital ships… always have a Control of −3d for every 30 points of Health."
        //
        // That last figure is transcribed twice on purpose — here, where it is printed, and on
        // capital_ships, which is where a reader looks for it. Both are the same sentence and the walk
        // compares both, so they cannot drift apart.
        ["vehicle_control.control.is_a"]                               = "modifier rather than a rank in its own right",
        ["vehicle_control.control.covers"]                             = new[] { "handling", "maneuverability", "targeting systems", "fire controls" },
        ["vehicle_control.control.control_roll_is"]                    = "a Vehicles roll modified by the vehicle's Control",
        ["vehicle_control.control.control_roll_trait"]                 = "vehicles",
        ["vehicle_control.control.capital_ship_control_per_thirty_health"] = -3,

        // Weapons
        // p.94 WEAPONS: "This characteristic represents the vehicle's weapon systems. It's an abstraction
        // that covers everything from a single nose gun or turret to a vast array of weapon batteries and
        // offensive systems." The tables print "n/a" for a vehicle that has none.
        ["vehicle_weapons.weapons.represents"]        = "the vehicle's weapon systems, from a single gun to whole batteries",
        ["vehicle_weapons.weapons.is_an_abstraction"] = true,
        ["vehicle_weapons.weapons.unarmed_value"]     = "n/a",

        // The vehicular Gear Limit
        // p.94 CONTROL, second paragraph: "Like all mundane gear, vehicles are subject to the game's Gear
        // Limit. However, if vehicles are going to play an important role in a particular series, the GM
        // may wish to raise the vehicular Gear Limit to 9d, 12d, or more, allowing characters to take
        // advantage of their full Vehicles rank."
        ["vehicular_gear_limit.vehicular_gear_limit.vehicles_are_mundane_gear"]      = true,
        ["vehicular_gear_limit.vehicular_gear_limit.default_applies"]                = "the game's ordinary Gear Limit",
        ["vehicular_gear_limit.vehicular_gear_limit.raised_options"]                 = new[] { 9, 12 },
        ["vehicular_gear_limit.vehicular_gear_limit.raised_options_are_open_ended"]  = true,
        ["vehicular_gear_limit.vehicular_gear_limit.raising_it_lets_characters_use"] = "their full Vehicles rank",

        // Piloting
        // p.95 VEHICLE COMBAT: "If you know what you're doing, you can automatically operate a vehicle
        // under ordinary conditions without trouble. If you find yourself doing something out of the
        // ordinary, the GM may call for a challenge roll, which in this case is usually going to mean a
        // Control roll."
        ["vehicle_piloting.piloting.routine_operation_is_automatic"] = true,
        ["vehicle_piloting.piloting.roll_called_for_when"]           = "the pilot attempts something out of the ordinary",
        ["vehicle_piloting.piloting.roll_is_usually"]                = "a Control roll",

        // Edge
        // p.95 EDGE: "When operating a vehicle, your Edge equals your Vehicles as modified by the
        // vehicle's Control. Use your full Vehicles rank when determining your Edge, regardless of the
        // game's Gear Limit. Capital ships always act after ordinary vehicles, but Edge determines action
        // order among them."
        ["vehicle_edge.edge.formula"]                                    = "edge = vehicles + control",
        ["vehicle_edge.edge.trait"]                                      = "vehicles",
        ["vehicle_edge.edge.uses_full_rank_regardless_of_gear_limit"]    = true,
        ["vehicle_edge.edge.capital_ships_act_after_ordinary_vehicles"]  = true,
        ["vehicle_edge.edge.edge_orders_capital_ships_among_themselves"] = true,

        // Chases
        // p.95 CHASES: "Vehicular chases work just like those between characters, as discussed in Chapter
        // 4. When moving through clear and unobstructed terrain, you can use a vehicle's Speed as its
        // Travel Power. When moving through winding terrain where maneuverability matters, you make Control
        // rolls instead."
        //
        // The chase rules themselves are Chapter 4's and stay there — data/rules/play/combat.json. Copying
        // them in would be the second transcription that policy exists to prevent, so the entry carries the
        // reference and the two figures this page adds.
        ["vehicle_chases.chases.resolved_as"]          = "an ordinary chase between characters, per Chapter 4",
        ["vehicle_chases.chases.open_terrain_uses"]    = "the vehicle's Speed as its Travel Power",
        ["vehicle_chases.chases.winding_terrain_uses"] = "Control rolls",
        ["vehicle_chases.chases.transcribed_here"]     = false,
        ["vehicle_chases.chases.deferred_to_chapter"]  = 4,

        // Attacks and Defenses
        // p.95 ATTACKS AND DEFENSES: "When firing a vehicle weapon at an enemy, make a Control roll against
        // your target's active defense or a Weapons roll against their passive defense. Use whichever option
        // favors the defender. If your vehicle has an attack Power, substitute its rank for your Weapons rank
        // when using that Power. If you're trying to ram an enemy instead of shooting them, substitute your
        // vehicle's Body rank for its Weapons rank. Vehicles use Control rolls for active defense and Body
        // rolls for passive defense." … "The Control roll represents the attack's accuracy and the Weapons
        // roll represents how much damage it inflicts." … "When characters attack vehicles, it works like an
        // ordinary attack, with the target… using the greater of their active defense (a Control roll) or
        // their passive defense (a Body roll)."
        ["vehicle_attacks_and_defenses.attacks_and_defenses.attacker_option_one"]                = "a Control roll against the target's active defense",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.attacker_option_two"]                = "a Weapons roll against the target's passive defense",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.which_option_is_used"]               = "whichever favours the defender",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.control_roll_represents"]            = "accuracy",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.weapons_roll_represents"]            = "damage",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.attack_power_substitutes_for"]       = "the Weapons rank, when that Power is used",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.ram_substitutes_for_weapons"]        = "the vehicle's Body rank",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.vehicle_active_defense_roll"]        = "control",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.vehicle_passive_defense_roll"]       = "body",
        ["vehicle_attacks_and_defenses.attacks_and_defenses.characters_attacking_a_vehicle_use"] = "an ordinary attack, the vehicle taking the greater of its active or passive defense",

        // Damage and Repair
        // p.95 DAMAGE AND REPAIR: "Like characters, vehicles suffer 1 point of damage per net success scored
        // against them. Ram attacks inflict 1 point of damage per net success scored on the attack roll, but
        // if the attacker's Body is less than or equal to the target's Body, the attacker suffers the same
        // amount of damage. For these purposes, characters have an effective Body equal to their Armor or
        // half their Toughness. Most vehicles are disabled or destroyed after suffering a number of points of
        // damage equal to their Body." … "Vehicles don't heal themselves… they have to be repaired… you can
        // attempt to perform field repairs on a damaged vehicle if you have the necessary tool and materials
        // on hand."
        ["vehicle_damage_and_repair.damage_and_repair.damage_per_net_success"]              = 1,
        ["vehicle_damage_and_repair.damage_and_repair.disabled_or_destroyed_at"]            = "damage equal to the vehicle's Body",
        ["vehicle_damage_and_repair.damage_and_repair.ram_damage_per_net_success"]          = 1,
        ["vehicle_damage_and_repair.damage_and_repair.rammer_suffers_the_same_damage_when"] = "the attacker's Body is less than or equal to the target's Body",
        ["vehicle_damage_and_repair.damage_and_repair.character_effective_body_is"]         = "their Armor, or half their Toughness",
        ["vehicle_damage_and_repair.damage_and_repair.vehicles_do_not_heal"]                = true,
        ["vehicle_damage_and_repair.damage_and_repair.repair_talent"]                       = "technology",
        ["vehicle_damage_and_repair.damage_and_repair.field_repair_needs"]                  = "tools and materials on hand",

        // Targeting a system
        // p.95 DAMAGE AND REPAIR: "GMs may wish to let attackers target specific vehicle systems by accepting
        // a −2d penalty to their attack rolls. If the attack succeeds, instead of inflicting damage, it
        // disables one of the target's systems. That can mean disabling one of the vehicle's features…, its
        // ability to move, or its weapons. Rolling 1 or 2 net successes disables the system until the end of
        // the attacker's next turn to act. Rolling 3 or more net successes disables the system for the rest
        // of the scene."
        ["vehicle_targeted_systems.targeted_systems.is_a_gm_option"]          = true,
        ["vehicle_targeted_systems.targeted_systems.attack_penalty_dice"]     = -2,
        ["vehicle_targeted_systems.targeted_systems.inflicts_damage"]         = false,
        ["vehicle_targeted_systems.targeted_systems.disables"]                = "one of the target's features, its ability to move, or its weapons",
        ["vehicle_targeted_systems.targeted_systems.net_successes_short_min"] = 1,
        ["vehicle_targeted_systems.targeted_systems.net_successes_short_max"] = 2,
        ["vehicle_targeted_systems.targeted_systems.short_duration"]          = "until the end of the attacker's next turn to act",
        ["vehicle_targeted_systems.targeted_systems.net_successes_long_min"]  = 3,
        ["vehicle_targeted_systems.targeted_systems.long_duration"]           = "the rest of the scene",

        // Capital Ships
        // p.95 CAPITAL SHIPS: "Massive vehicles like modern-day warships and their sci-fi equivalents are
        // capital ships… A capital ship's Edge equals its captain's Intellect, assuming the captain knows
        // what they're doing (this is up to the GM), or half the captain's Intellect if they don't… you may
        // want to require that characters use the Expertise Power… Standard capital ship weapon attacks are
        // always resolved using the ship's Weapons, but they have the Overkill Con when used against anything
        // other than other capital ships or fixed locations. That means ordinary vehicles and characters
        // double their successes when using active defenses… These weapons have a rank 6d lower than the
        // ship's Weapons, but they don't have the Overkill Con. Capital ships are usually armed with enough
        // pinpoint weapons to attack every ship within range once per page. Capital ships never perform
        // active defenses." … "these oversized vehicles have Health scores in multiples of 30 (30, 60, 90,
        // etc.). There's no magic formula here… a capital ship struck by an attack that inflicts at least its
        // Body in damage in one shot is immediately disabled or destroyed."
        ["capital_ships.capital_ships.what_they_are"]                      = "warship-scale vessels and their science fiction equivalents",
        ["capital_ships.capital_ships.edge_is"]                            = "the captain's Intellect",
        ["capital_ships.capital_ships.edge_trait"]                         = "intellect",
        ["capital_ships.capital_ships.edge_halved_when"]                   = "the captain does not know what they are doing, as the GM judges",
        ["capital_ships.capital_ships.gm_may_require_power"]               = "Expertise, for experience in command",
        ["capital_ships.capital_ships.health_is_separate_from_body"]       = true,
        ["capital_ships.capital_ships.health_multiple_of"]                 = 30,
        ["capital_ships.capital_ships.health_has_no_formula"]              = true,
        ["capital_ships.capital_ships.control_per_thirty_health"]              = -3,
        ["capital_ships.capital_ships.standard_weapons_carry_con"]         = "overkill",
        ["capital_ships.capital_ships.overkill_applies_except_against"]    = new[] { "other capital ships", "fixed locations" },
        ["capital_ships.capital_ships.overkill_effect"]                    = "the target doubles its successes when defending actively",
        ["capital_ships.capital_ships.pinpoint_weapon_rank_offset"]        = -6,
        ["capital_ships.capital_ships.pinpoint_weapons_carry_overkill"]    = false,
        ["capital_ships.capital_ships.pinpoint_weapons_usually_enough_to"] = "attack every ship in range once per page",
        ["capital_ships.capital_ships.never_perform_active_defenses"]      = true,
        ["capital_ships.capital_ships.one_shot_destruction_threshold"]     = "damage equal to or greater than the ship's Body in a single attack",

        // Capital ship ramming
        // p.95 CAPITAL SHIPS: "Capital ships can only ram other capital ships and slow-moving or stationary
        // objects. These attacks are resolved with opposed Edge rolls between the captains. If the vessels
        // have different Control values, the smaller one gets a bonus to this roll equal to the difference
        // between them. For example, a capital ship with 30 Health and −3d Control gets a +6d bonus to ram or
        // evade a capital ship with 90 Health and −9d Control. If a ram attack succeeds, both vessels suffer
        // damage equal to the smaller ship's full Health (even if that ship is damaged). If the attacker
        // rolls 3 or more net successes, they can choose to suffer half damage. If the attacker rolls 5 or
        // more net successes, they can instead choose to inflict double damage on the target vessel."
        //
        // The worked example is also a check on p.94's −3d per 30 Health: 30 Health gives −3d, 90 gives −9d,
        // and the difference is the +6d the page prints.
        ["capital_ship_ramming.capital_ship_ramming.may_only_ram"]                          = new[] { "other capital ships", "slow-moving or stationary objects" },
        ["capital_ship_ramming.capital_ship_ramming.resolved_by"]                           = "opposed Edge rolls between the captains",
        ["capital_ship_ramming.capital_ship_ramming.smaller_ship_bonus"]                    = "the difference between the two Control values",
        ["capital_ship_ramming.capital_ship_ramming.worked_example_small_health"]           = 30,
        ["capital_ship_ramming.capital_ship_ramming.worked_example_small_control"]          = -3,
        ["capital_ship_ramming.capital_ship_ramming.worked_example_large_health"]           = 90,
        ["capital_ship_ramming.capital_ship_ramming.worked_example_large_control"]          = -9,
        ["capital_ship_ramming.capital_ship_ramming.worked_example_bonus_dice"]             = 6,
        ["capital_ship_ramming.capital_ship_ramming.damage_on_success"]                     = "the smaller ship's full Health, to both vessels",
        ["capital_ship_ramming.capital_ship_ramming.damage_uses_undamaged_health"]          = true,
        ["capital_ship_ramming.capital_ship_ramming.half_damage_option_at_net_successes"]   = 3,
        ["capital_ship_ramming.capital_ship_ramming.double_damage_option_at_net_successes"] = 5,

        // Foe and Minion pilots
        // p.96 FOE AND MINION PILOTS: "When Foes pilot vehicles, those vehicles can sustain only half as much
        // damage as usual. For example, an ordinary sedan with 7d Body is disabled or destroyed after
        // suffering only 4 points of damage (half of 7) when driven by a Foe. When Minions pilot vehicles,
        // they still act like Minions. They use Threat in place of the Vehicles Talent. They attack in groups
        // and enjoy their usual attack roll bonuses (these bonuses apply to both the Control roll and the
        // Weapons roll). And they fall at the usual pace of 1 Minion—or in this case 1 vehicle—per net
        // success rolled against them. These rules don't apply to capital ships."
        //
        // The sedan is one of the three anchors on the mundane tables' pairing: p.97's ground table gives
        // Car, Sedan a Body of 7d, printed nowhere near this sentence.
        ["foe_and_minion_pilots.foe_and_minion_pilots.foe_vehicle_damage_capacity"]          = "half the usual",
        ["foe_and_minion_pilots.foe_and_minion_pilots.foe_worked_example_vehicle"]           = "an ordinary sedan",
        ["foe_and_minion_pilots.foe_and_minion_pilots.foe_worked_example_body"]              = 7,
        ["foe_and_minion_pilots.foe_and_minion_pilots.foe_worked_example_damage_capacity"]   = 4,
        ["foe_and_minion_pilots.foe_and_minion_pilots.minions_use_in_place_of_vehicles"]     = "threat",
        ["foe_and_minion_pilots.foe_and_minion_pilots.minions_attack_in_groups"]             = true,
        ["foe_and_minion_pilots.foe_and_minion_pilots.minion_group_bonus_applies_to"]        = new[] { "the Control roll", "the Weapons roll" },
        ["foe_and_minion_pilots.foe_and_minion_pilots.minion_vehicles_lost_per_net_success"] = 1,
        ["foe_and_minion_pilots.foe_and_minion_pilots.applies_to_capital_ships"]             = false,

        // The Unique Vehicle Perk
        // p.96 UNIQUE VEHICLES: "The Unique Vehicle Perk means you possess a vehicle with unique properties.
        // Every Hero Point you put into this Perk grants you 25 Vehicle Points. You spend Vehicle Points to
        // create unique vehicles from scratch or to improve unique vehicles you already possess. Multiple
        // Heroes can pool their Vehicle Points together to create a single, seriously sweet ride if they
        // wish."
        //
        // This is the exchange rate between the two currencies, and the only place Hero Points buy a vehicle.
        ["unique_vehicle_perk.unique_vehicle_perk.perk_id"]                       = "unique_vehicle",
        ["unique_vehicle_perk.unique_vehicle_perk.vehicle_points_per_hero_point"] = 25,
        ["unique_vehicle_perk.unique_vehicle_perk.spendable_on"]                  = new[] { "creating a unique vehicle from scratch", "improving one already owned" },
        ["unique_vehicle_perk.unique_vehicle_perk.heroes_may_pool_points"]        = true,

        // Buying characteristics
        // p.96 CHARACTERISTICS: "You spend Vehicle Points to buy or improve a unique vehicle's Body, Speed,
        // Control, and Weapons. Body, Speed, and Weapons cost 1 Vehicle Point per rank. Control costs 2
        // Vehicle Points per rank and can't exceed half the vehicle's Speed. Giving your unique vehicle a
        // negative Control value lowers its cost by 2 Vehicle Points per negative rank, down to a minimum of
        // −3d Control. When creating a unique vehicle from scratch, Body, Speed, and Control have an initial
        // rank of 0d, while Weapons has an initial rank of \"n/a\"."
        //
        // These five rates are what the stock vehicle arithmetic fixture replays: five of the six printed
        // totals on the same page come out exactly, so a wrong rate here is caught by the authors' own sums.
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.body_cost_per_rank"]               = 1,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.speed_cost_per_rank"]              = 1,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.weapons_cost_per_rank"]            = 1,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.control_cost_per_rank"]            = 2,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.control_may_not_exceed"]           = "half the vehicle's Speed",
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.negative_control_refund_per_rank"] = 2,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.negative_control_minimum"]         = -3,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.initial_body"]                     = 0,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.initial_speed"]                    = 0,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.initial_control"]                  = 0,
        ["unique_vehicle_characteristics.unique_vehicle_characteristics.initial_weapons"]                  = "n/a",

        // Limiting stock upgrades
        // p.96 STOCK VEHICLES: "GMs who want to keep vehicles somewhat realistic may want to limit how much
        // you can upgrade stock vehicles. If so, it would be reasonable to rule that you can increase a stock
        // vehicle's Body by up to 6 ranks, Speed by up to 4 ranks, and Control by up to 2 ranks. Again, this
        // is completely optional and best reserved for more realistic games."
        ["stock_vehicle_upgrade_limit.stock_upgrade_limit.is_optional"]             = true,
        ["stock_vehicle_upgrade_limit.stock_upgrade_limit.suits"]                   = "more realistic games",
        ["stock_vehicle_upgrade_limit.stock_upgrade_limit.max_body_ranks_added"]    = 6,
        ["stock_vehicle_upgrade_limit.stock_upgrade_limit.max_speed_ranks_added"]   = 4,
        ["stock_vehicle_upgrade_limit.stock_upgrade_limit.max_control_ranks_added"] = 2,

        // ── headquarters.json ────────────────────────────────────────────────
        // The Headquarters Perk
        // p.100 BASE FEATURES: "The Headquarters Perk grants you a basic headquarters, a space no larger than
        // an average mansion or warehouse with whatever basic rooms, equipment, and facilities you would
        // like… every Hero Point you put into the Headquarters Perk also grants you 3 Base Points you can
        // spend to improve your headquarters by adding the unique features described below… Multiple Heroes
        // can pool their Base Points together to create an even better headquarters."
        //
        // This is the exchange rate between the two currencies, and the only place Hero Points buy a base.
        ["headquarters_perk.headquarters_perk.perk_id"]                                   = "headquarters",
        ["headquarters_perk.headquarters_perk.base_points_per_hero_point"]                = 3,
        ["headquarters_perk.headquarters_perk.grants_by_default"]                         = "a basic headquarters no larger than an average mansion or warehouse, with whatever ordinary rooms, equipment and facilities the owner wants",
        ["headquarters_perk.headquarters_perk.default_headquarters_costs_no_base_points"] = true,
        ["headquarters_perk.headquarters_perk.heroes_may_pool_points"]                    = true,

        // What a feature is worth at the table
        // p.100 BASE FEATURES: "For the most part, features are described in narrative rather than mechanical
        // terms. However, GMs may grant Heroes a +1d bonus to challenge rolls whenever they can take
        // advantage of an advanced feature in their headquarters. For example, if your base has the Advanced
        // Science Labs feature, the GM may grant you a +1d bonus to Investigation rolls if you can bring any
        // clues you find back to your headquarters for further analysis."
        ["advanced_feature_bonus.advanced_feature_bonus.features_are_mostly_narrative"] = true,
        ["advanced_feature_bonus.advanced_feature_bonus.gm_may_grant_bonus_dice"]       = 1,
        ["advanced_feature_bonus.advanced_feature_bonus.bonus_applies_to"]              = "a challenge roll the feature can be brought to bear on",
        ["advanced_feature_bonus.advanced_feature_bonus.is_gm_discretion"]              = true,
        ["advanced_feature_bonus.advanced_feature_bonus.worked_example_feature"]        = "Advanced Science Labs",
        ["advanced_feature_bonus.advanced_feature_bonus.worked_example_roll"]           = "Investigation",

        // Mobile headquarters
        // p.102 MOBILE: "Your headquarters can travel from place to place. Although this feature is free,
        // mobile bases are also unique vehicles, so you'll have to spend Hero Points on the Unique Vehicles
        // Perk to purchase the base's vehicular characteristics and unique vehicle features. Mobile
        // headquarters are always capital ships: standard bases have 30 Health, bases with the Large Size
        // feature have 60 Health, and those with the Sprawling Size feature have 120 Health. Headquarters
        // with the Enormous Size feature are completely off the scale and can't be attacked or destroyed like
        // normal capital ships."
        ["mobile_headquarters.mobile_headquarters.base_point_cost"]                              = 0,
        ["mobile_headquarters.mobile_headquarters.is_also_a_unique_vehicle"]                     = true,
        ["mobile_headquarters.mobile_headquarters.vehicle_characteristics_bought_with"]          = "Hero Points spent on the Unique Vehicle Perk",
        ["mobile_headquarters.mobile_headquarters.always_a_capital_ship"]                        = true,
        ["mobile_headquarters.mobile_headquarters.health_default"]                               = 30,
        ["mobile_headquarters.mobile_headquarters.health_with_large_size"]                       = 60,
        ["mobile_headquarters.mobile_headquarters.health_with_sprawling_size"]                   = 120,
        ["mobile_headquarters.mobile_headquarters.largest_size_cannot_be_attacked_or_destroyed"] = true,

        // Teamwork
        // p.103 TRAINING FACILITIES: "If you and your teammates have a headquarters with this feature, you
        // each gain 1 point of Teamwork at the start of every issue. Teamwork works like Resolve that can
        // only be used to assist your allies in combat or during action scenes."
        ["teamwork.teamwork.granted_by_feature"] = "training_facilities",
        ["teamwork.teamwork.points_per_issue"]   = 1,
        ["teamwork.teamwork.granted_to"]         = "every character who shares the headquarters",
        ["teamwork.teamwork.behaves_like"]       = "resolve",
        ["teamwork.teamwork.spendable_only_on"]  = "assisting an ally in combat or in an action scene",
    };

    /// <summary>
    /// How many rows each printed table has. <b>These are counts and not the rows themselves</b>:
    /// the rows are derived from the corpus, and a count typed here is what stops a derivation from
    /// agreeing with a table that has silently lost half of itself.
    /// </summary>
    public static class TableSizes
    {
        /// <summary>The mundane vehicle tables, printed pp.97-98.</summary>
        public const int AirSpaceVehicles = 24;

        /// <inheritdoc cref="AirSpaceVehicles"/>
        public const int GroundVehicles = 15;

        /// <inheritdoc cref="AirSpaceVehicles"/>
        public const int WaterVehicles = 15;

        /// <summary>The stock vehicles, printed p.96.</summary>
        public const int StockVehicles = 6;

        /// <summary>The vehicle features, printed pp.96-100.</summary>
        public const int VehicleFeatures = 23;

        /// <summary>The base features, printed pp.100-103.</summary>
        public const int BaseFeatures = 22;
    }

    /// <summary>
    /// The rows the chapter prints a <em>second</em> time, away from the tables, and which is what
    /// anchors the pairing of each mundane table's two printed blocks. Each is looked up out of the
    /// corpus by the test rather than compared with the number beside it here.
    ///
    /// <para>p.96's Foe example gives the sedan seven dice of Body; p.96's six stock vehicles
    /// reprint five rows of pp.97-98 characteristic for characteristic — and the sixth,
    /// <see cref="StockOnlyVehicle"/>, is a stock entry with no row of its own.</para>
    /// </summary>
    public static class AnchorRows
    {
        /// <summary>Ch.6 p.96, FOE AND MINION PILOTS: "an ordinary sedan with 7d Body".</summary>
        public const string SedanRowName = "Car, Sedan";

        /// <inheritdoc cref="SedanRowName"/>
        public const int SedanBody = 7;

        /// <summary>Stock vehicle name to mundane-table row name, for the five that are both.</summary>
        public static readonly IReadOnlyDictionary<string, string> StockToTableRow =
            new Dictionary<string, string>
            {
                ["Helicopter"]  = "Helicopter, Civilian",
                ["Jet Fighter"] = "Airplane, Jet Fighter",
                ["Motorcycle"]  = "Motorcycle",
                ["Speedboat"]   = "Speedboat",
                ["Submersible"] = "Submersible"
            };

        /// <summary>The one stock vehicle the mundane tables do not print a row for.</summary>
        public const string StockOnlyVehicle = "Sports Car";
    }

    /// <summary>
    /// p.94's Control entry: "Capital ships … always have a Control of −3d for every 30 points of
    /// Health." Every capital-ship row in the three mundane tables obeys it exactly, which is the
    /// second witness that the two printed blocks were paired in the right order — a misalignment
    /// of one row breaks it on nine rows at once.
    /// </summary>
    public static class CapitalShipControlRule
    {
        public const int HealthPerStep = 30;
        public const int ControlPerStep = -3;
    }

    /// <summary>
    /// p.96's own arithmetic, replayed. Each stock vehicle's printed Vehicle Point total should be
    /// its characteristics priced by CHARACTERISTICS plus its features priced by VEHICLE FEATURES.
    /// <b>Five of the six come out exactly and the Submersible does not</b>, which is a fact about
    /// the page rather than about the transcription — see that entry's <c>ambiguity</c>. The figure
    /// is recorded so the discrepancy cannot quietly become two.
    /// </summary>
    public static class StockVehicleArithmetic
    {
        /// <summary>Stock vehicles whose printed total the rules on the same page reproduce.</summary>
        public static readonly string[] ReconcileExactly =
            ["Helicopter", "Jet Fighter", "Motorcycle", "Speedboat", "Sports Car"];

        /// <summary>The one that does not, and by how much the rules overshoot its printed total.</summary>
        public const string DoesNotReconcile = "Submersible";

        /// <inheritdoc cref="DoesNotReconcile"/>
        public const int SubmersibleOvershoot = 1;

        /// <summary>Ch.2 p.38 prices Radar at 3 HP, which Unique Systems converts one for one.</summary>
        public const int SonarCostAsUniqueSystem = 3;
    }

    /// <summary>
    /// What the twenty-three vehicle features and twenty-two base features cost, in aggregate. The
    /// individual prices are derived from the corpus; these are the sums and counts a derivation
    /// cannot check itself against.
    /// </summary>
    public static class FeaturePriceShape
    {
        /// <summary>Vehicle features whose printed price is negative: they pay points back.</summary>
        public static readonly string[] VehicleFeaturesWithNegativeCost =
            ["giant", "open_cockpit", "swimming", "transforming"];

        /// <summary>The one vehicle feature that costs nothing at all.</summary>
        public const string FreeVehicleFeature = "running";

        /// <summary>The dearest vehicle feature, and its price.</summary>
        public const string DearestVehicleFeature = "sensors";

        /// <inheritdoc cref="DearestVehicleFeature"/>
        public const int DearestVehicleFeatureCost = 10;

        /// <summary>The one base feature that costs nothing, and buys a Hero Point bill instead.</summary>
        public const string FreeBaseFeature = "mobile";

        /// <summary>Base features the chapter prices as "1 to 2 Base Points".</summary>
        public const int BaseFeaturesGradedOneToTwo = 10;

        /// <summary>The one base feature priced across three grades.</summary>
        public const string BaseFeatureGradedOneToThree = "size";

        /// <summary>The one base feature priced per unit rather than per grade.</summary>
        public const string BaseFeaturePricedPerUnit = "alternate_headquarters";
    }
}
