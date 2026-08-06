namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The twenty pre-built Heroes from Prowlers &amp; Paragons Ultimate Edition, Chapter 8
/// ("Heroes and Villains", pp.126+), transcribed from the rulebook. Each is a finished,
/// playable Standard-tier Hero on a 125 Hero Point budget with its Edge, Health and
/// Resolve already worked out, which makes them the best end-to-end check available:
/// they were built by the authors, so the engine has to reproduce their numbers.
///
/// <para>Two things in the printed blocks are deliberately not transcribed:</para>
/// <list type="bullet">
///   <item><c>Abilities (...)</c> and <c>Abilities and Talents (All)</c> are Source tags
///   marking which Traits are superhuman, not Powers, and cost nothing.</item>
///   <item>Gear is bought against the Gear Limit rather than with Hero Points (Ch.6),
///   so it does not enter any of these calculations.</item>
/// </list>
/// </summary>
public static class PrebuiltHeroes
{
    /// <summary>
    /// A Power as printed on a Hero's sheet: its id and its final rank. <paramref name="Units"/>
    /// carries the quantity for per-unit Powers — how many immunities Immunity lists, or
    /// which power level an Alternate Form is (Standard being the third, so 3).
    /// </summary>
    public sealed record Power(
        string Id,
        int EffectiveRank = 0,
        string? BaselineTrait = null,
        int Units = 1,
        string? CostVariant = null);

    public sealed record Hero(
        string Name,
        int Page,
        int Agility,
        int Intellect,
        int Might,
        int Perception,
        int Toughness,
        int Willpower,
        IReadOnlyList<Power> Powers,
        IReadOnlyList<string> Flaws,
        int Edge,
        int Health,
        int Resolve,
        /// <summary>Resolve bought through Determination, printed as "(+N Resolve)".</summary>
        int DeterminationResolve = 0,
        string? Note = null);

    private static Power P(string id, int rank = 0) => new(id, rank);

    /// <summary>A purchased Perk: its id and, for per-unit Perks, how many units.</summary>
    public sealed record PerkPurchase(string Id, int Units = 1);

    /// <summary>
    /// Talent ranks in the order the sheets print them: Academics, Charm, Command,
    /// Covert, Investigation, Medicine, Professional, Science, Streetwise, Survival,
    /// Technology, Vehicles. Kept apart from <see cref="All"/> so the Hero entries stay
    /// readable; <see cref="TalentIds"/> pairs them up.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int[]> TalentsByHero = new Dictionary<string, int[]>
    {
        ["Alabama Slammer"]   = [3, 5, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3],
        ["Black Dragon"]      = [3, 3, 6, 6, 3, 3, 3, 3, 6, 3, 3, 3],
        ["Blastwave"]         = [2, 2, 4, 4, 4, 2, 2, 2, 4, 4, 4, 2],
        ["Citizen Soldier"]   = [3, 5, 6, 5, 3, 3, 3, 3, 3, 3, 3, 5],
        ["Combustion"]        = [2, 6, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2],
        ["Darkwolf"]          = [3, 3, 3, 6, 6, 3, 3, 3, 6, 6, 3, 3],
        ["Eidolon"]           = [3, 4, 3, 4, 4, 3, 3, 4, 3, 3, 3, 3],
        ["Herald (Airmid)"]   = [2, 4, 2, 2, 2, 6, 2, 2, 2, 6, 2, 2],
        ["Herald (Scathach)"] = [2, 4, 4, 4, 2, 2, 2, 2, 2, 4, 2, 2],
        ["Nano"]              = [2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2],
        ["Pandora"]           = [6, 2, 6, 2, 2, 2, 2, 2, 2, 2, 2, 2],
        ["Psi Lance"]         = [3, 3, 3, 3, 3, 6, 3, 6, 3, 3, 3, 3],
        ["Psidearm"]          = [3, 6, 3, 6, 6, 3, 3, 3, 3, 6, 3, 6],
        ["Shadow"]            = [3, 3, 3, 9, 6, 3, 3, 3, 6, 6, 3, 3],
        ["Siren"]             = [3, 5, 5, 5, 7, 3, 3, 3, 3, 3, 5, 5],
        ["Stronghold"]        = [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 12, 3],
        ["T-Kay"]             = [3, 4, 3, 3, 3, 3, 3, 3, 4, 3, 3, 3],
        ["Talon"]             = [3, 3, 6, 6, 6, 3, 3, 3, 6, 6, 6, 6],
        ["Vector"]            = [4, 2, 2, 4, 4, 2, 2, 6, 4, 2, 6, 2],
        ["Vigilant"]          = [3, 6, 6, 6, 6, 3, 3, 3, 6, 6, 3, 6]
    };

    public static readonly string[] TalentIds =
    [
        "academics", "charm", "command", "covert", "investigation", "medicine",
        "professional", "science", "streetwise", "survival", "technology", "vehicles"
    ];

    /// <summary>
    /// Perks as printed. Contacts is 1 HP per type of contact, so its Units is how many
    /// types the sheet lists.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PerkPurchase[]> PerksByHero =
        new Dictionary<string, PerkPurchase[]>
        {
            ["Alabama Slammer"]   = [],
            ["Black Dragon"]      = [new("contacts", 2)],
            ["Blastwave"]         = [],
            ["Citizen Soldier"]   = [new("authority"), new("contacts", 2)],
            ["Combustion"]        = [new("contacts"), new("wealth")],
            ["Darkwolf"]          = [],
            ["Eidolon"]           = [],
            ["Herald (Airmid)"]   = [],
            ["Herald (Scathach)"] = [],
            ["Nano"]              = [],
            ["Pandora"]           = [new("contacts")],
            ["Psi Lance"]         = [],
            ["Psidearm"]          = [new("contacts"), new("resources")],
            ["Shadow"]            = [new("contacts"), new("resources")],
            ["Siren"]             = [new("contacts", 2)],
            ["Stronghold"]        = [new("contacts")],
            ["T-Kay"]             = [new("contacts")],
            ["Talon"]             = [new("contacts")],
            ["Vector"]            = [new("contacts", 2)],
            ["Vigilant"]          = [new("contacts", 3), new("wealth")]
        };

    public static readonly IReadOnlyList<Hero> All =
    [
        new("Alabama Slammer", 137,
            Agility: 3, Intellect: 3, Might: 3, Perception: 6, Toughness: 6, Willpower: 5,
            Powers:
            [
                P("determination"), P("phasing"), P("regeneration"),
                P("resistance", 12), P("super_speed", 12)
            ],
            Flaws: ["quirk", "relationship", "secret_identity"],
            Edge: 36, Health: 6, Resolve: 4, DeterminationResolve: 3,
            Note: "Edge 36 is Super Speed 12d x 3, which is the only place that rule shows up in a printed sheet."),

        new("Black Dragon", 138,
            Agility: 10, Intellect: 4, Might: 4, Perception: 6, Toughness: 6, Willpower: 10,
            Powers:
            [
                new("boost", 9, "might"), P("danger_sense", 10), P("leaping", 5), P("regeneration"),
                P("resistance", 10), P("running", 5), P("super_senses_true_sight"),
                P("blind_fighting"), P("martial_arts", 10), P("two_fisted")
            ],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 20, Health: 8, Resolve: 6,
            Note: "Edge 20 = Danger Sense 10d + Agility 10d. Adding Danger Sense to Perception would give 26, so this sheet proves it replaces Perception."),

        new("Blastwave", 139,
            Agility: 5, Intellect: 4, Might: 5, Perception: 5, Toughness: 10, Willpower: 7,
            Powers: [new("energy_absorption", 12, null, 1, "kinetic"), P("lightning_reflexes"), P("martial_arts", 10), P("running", 5)],
            Flaws: ["relationship", "secret_identity", "wanted"],
            Edge: 16, Health: 9, Resolve: 2),

        new("Citizen Soldier", 140,
            Agility: 6, Intellect: 6, Might: 12, Perception: 6, Toughness: 12, Willpower: 9,
            Powers: [P("armor", 12), P("regeneration"), P("determination"), P("leadership")],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 12, Health: 12, Resolve: 4, DeterminationResolve: 2),

        new("Combustion", 141,
            Agility: 4, Intellect: 3, Might: 3, Perception: 3, Toughness: 4, Willpower: 4,
            Powers:
            [
                P("determination"), P("aura", 9), P("elemental_control", 12), P("flight", 8),
                new("immunity", 0, null, 1), new("expertise", 4, "professional"), P("evasion", 8)
            ],
            Flaws: ["finite_power", "quirk", "relationship"],
            Edge: 7, Health: 4, Resolve: 2, DeterminationResolve: 1),

        new("Darkwolf", 142,
            Agility: 10, Intellect: 3, Might: 6, Perception: 10, Toughness: 10, Willpower: 10,
            Powers:
            [
                P("animal_empathy"), P("hard_to_kill"), P("leaping", 6), P("regeneration"),
                P("running", 6), P("strike", 10), P("super_senses_acute", 12),
                P("super_senses_enhanced_hearing"), P("super_senses_tracking_scent"),
                P("super_senses_ultra_vision"), P("two_fisted")
            ],
            Flaws: ["relationship", "restriction", "secret_identity"],
            Edge: 20, Health: 10, Resolve: 5),

        new("Eidolon", 143,
            Agility: 8, Intellect: 3, Might: 3, Perception: 6, Toughness: 8, Willpower: 6,
            Powers: [new("omni_power", 10, null, 1, "broad"), P("light_effect")],
            Flaws: ["enemy", "relationship", "outsider"],
            Edge: 14, Health: 7, Resolve: 6),

        new("Herald (Airmid)", 144,
            Agility: 6, Intellect: 4, Might: 4, Perception: 6, Toughness: 4, Willpower: 8,
            Powers:
            [
                new("alternate_form", 0, null, 3), P("elemental_control", 10),
                new("expertise", 12, "medicine"), P("healing", 10)
            ],
            Flaws: ["alter_ego", "relationship", "secret_identity"],
            Edge: 12, Health: 6, Resolve: 5),

        new("Herald (Scathach)", 145,
            Agility: 8, Intellect: 3, Might: 8, Perception: 6, Toughness: 6, Willpower: 6,
            Powers:
            [
                new("alternate_form", 0, null, 3), P("attuned"), P("armor", 10), P("danger_sense", 8),
                P("determination"), new("expertise", 12, "academics"), P("lightning_reflexes"),
                P("martial_arts", 10), P("regeneration"), P("strike", 11), P("weakness_detection")
            ],
            Flaws: ["relationship"],
            Edge: 22, Health: 7, Resolve: 5, DeterminationResolve: 2,
            Note: "Edge 22 = Danger Sense 8d + Agility 8d + Lightning Reflexes 6. A second sheet confirming Danger Sense replaces Perception."),

        new("Nano", 146,
            Agility: 3, Intellect: 3, Might: 3, Perception: 3, Toughness: 6, Willpower: 6,
            Powers:
            [
                P("communications"), P("determination"), P("form_gaseous"), P("immortality"),
                P("inanimate"), P("form_liquid"), P("machine_control", 6), P("separation"),
                P("transformation_shapeshifting", 12), P("super_senses_analytic"),
                P("super_senses_circular_vision"), P("super_senses_microscopic_vision")
            ],
            Flaws: ["emotionless", "quirk", "restriction"],
            Edge: 6, Health: 6, Resolve: 2, DeterminationResolve: 2),

        new("Pandora", 147,
            Agility: 3, Intellect: 5, Might: 3, Perception: 4, Toughness: 3, Willpower: 12,
            Powers: [new("omni_power", 12, null, 1, "broad"), new("expertise", 10, "academics")],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 9, Health: 8, Resolve: 2),

        new("Psi Lance", 148,
            Agility: 3, Intellect: 6, Might: 3, Perception: 3, Toughness: 3, Willpower: 12,
            Powers:
            [
                P("armor", 10), P("mind_blast", 12), P("mind_control", 6), P("telekinesis", 6),
                P("telepathy", 12), new("expertise", 10, "medicine")
            ],
            Flaws: ["obligation", "relationship", "secret_identity"],
            Edge: 9, Health: 8, Resolve: 2),

        new("Psidearm", 149,
            Agility: 10, Intellect: 3, Might: 3, Perception: 6, Toughness: 5, Willpower: 9,
            Powers:
            [
                P("telepathy", 9), P("armor", 6), P("communications"), new("immunity", 0, null, 1),
                P("super_senses_night_vision"), P("blast", 10), P("lightning_reflexes"),
                P("martial_arts", 6), P("master_of_disguise"), P("two_fisted")
            ],
            Flaws: ["compulsion", "relationship", "secret_identity"],
            Edge: 22, Health: 7, Resolve: 5),

        new("Shadow", 150,
            Agility: 10, Intellect: 3, Might: 3, Perception: 8, Toughness: 4, Willpower: 6,
            Powers:
            [
                P("armor", 6), P("blending"), P("communications"), new("immunity", 0, null, 1),
                P("invisibility"), P("super_senses_acute", 10), P("super_senses_night_vision"),
                P("super_senses_telescopic_vision"), P("swing_line", 6), P("wall_crawling", 4),
                P("determination"), P("martial_arts", 8), P("preparation"), P("strike", 10),
                P("two_fisted")
            ],
            Flaws: ["enemy", "quirk", "secret_identity"],
            Edge: 18, Health: 5, Resolve: 6, DeterminationResolve: 1),

        new("Siren", 151,
            Agility: 9, Intellect: 6, Might: 9, Perception: 6, Toughness: 9, Willpower: 6,
            Powers:
            [
                P("blending"), P("transformation_doppelganger"), P("radar"), P("running", 5),
                P("super_senses_ultra_vision"), P("swimming", 6), P("armor", 10), P("blast", 10)
            ],
            Flaws: ["light_sensitive", "quirk", "relationship"],
            Edge: 15, Health: 9, Resolve: 5),

        new("Stronghold", 152,
            Agility: 6, Intellect: 10, Might: 10, Perception: 6, Toughness: 10, Willpower: 5,
            Powers:
            [
                P("armor", 11), P("blast", 11), P("variant"), P("communications"),
                P("flight", 9), new("immunity", 0, null, 2), new("omni_power", 2, null, 1, "narrow")
            ],
            Flaws: ["finite_power", "relationship", "secret_identity"],
            Edge: 16, Health: 10, Resolve: 3),

        new("T-Kay", 153,
            Agility: 4, Intellect: 3, Might: 3, Perception: 4, Toughness: 3, Willpower: 9,
            Powers:
            [
                P("determination"), P("flight", 8), P("force_field", 12),
                P("lightning_reflexes"), P("telekinesis", 12)
            ],
            Flaws: ["aversion_fear", "relationship", "secret_identity"],
            Edge: 14, Health: 6, Resolve: 3, DeterminationResolve: 2,
            Note: "The sheet prints \"Edge 8/14\" because this Lightning Reflexes carries Limited (only for Telekinesis), so the +6 applies conditionally. The engine has no notion of a conditionally active Con and reports the unrestricted 14."),

        new("Talon", 154,
            Agility: 6, Intellect: 6, Might: 6, Perception: 6, Toughness: 6, Willpower: 9,
            Powers:
            [
                P("armor", 10), P("ensnare", 10), P("flight", 9), P("strike", 10),
                P("super_senses_acute", 10), P("super_senses_telescopic_vision"),
                P("super_senses_thermal_vision")
            ],
            Flaws: ["relationship", "secret", "secret_identity"],
            Edge: 12, Health: 8, Resolve: 5),

        new("Vector", 155,
            Agility: 3, Intellect: 6, Might: 3, Perception: 4, Toughness: 4, Willpower: 4,
            Powers:
            [
                P("attuned"), P("blink", 10), P("deflection", 10), new("immunity", 0, null, 1),
                P("phasing"), P("regeneration"), P("telekinesis", 6),
                new("expertise", 12, "science")
            ],
            Flaws: ["enemy", "finite_power", "relationship"],
            Edge: 10, Health: 4, Resolve: 6),

        new("Vigilant", 156,
            Agility: 9, Intellect: 4, Might: 4, Perception: 9, Toughness: 6, Willpower: 9,
            Powers:
            [
                P("super_senses_night_vision"), P("swing_line", 6), P("leaping", 3),
                P("martial_arts", 9), P("preparation"), P("running", 4), P("two_fisted"),
                P("weakness_detection")
            ],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 18, Health: 8, Resolve: 8)
    ];
}
