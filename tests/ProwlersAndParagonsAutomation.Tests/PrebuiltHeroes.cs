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
///   <item>Gear on the GEAR line is mundane, and Ch.6 is explicit that mundane gear costs
///   nothing and is not tracked. A Gear Limit caps the Trait rank you can apply while
///   using it; it is not a budget. Signature equipment is bought as a Power with the Item
///   Con instead, which is transcribed. Custom features on mundane gear (Upgraded,
///   Silenced, Thrown) do cost Hero Points, and those are not modelled yet.</item>
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
        // Resolve bought through Determination, printed on the sheet as "(+N Resolve)".
        // A plain comment, not XML: a positional parameter is not a language element a
        // <summary> can be attached to, and the compiler does not read it there either.
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

    /// <summary>
    /// The Pros and Cons each sheet prints in parentheses after a Power, keyed
    /// "Hero|powerId". Each entry is <c>kind:id</c>, optionally <c>:variant</c> and
    /// <c>#units</c> — so <c>"con:charges:3_per_scene"</c> or <c>"pro:also_x#5"</c>.
    ///
    /// <para>Parentheses that merely name what the Power does are not Pros or Cons and
    /// are not listed: Aura (Fire), Strike (Physical), Omni-Power (Sorcery),
    /// Attuned (Space-Time Disturbances) and the like.</para>
    ///
    /// <para>Where a sheet writes one Con for a Super Senses group, it is recorded
    /// against a single option. The rulebook treats Super Senses as one Power, so the
    /// Con applies once even though this project stores each option separately.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> ProsConsByHero =
        new Dictionary<string, string[]>
        {
            // Alabama Slammer
            ["Alabama Slammer|phasing"] = ["con:concentration"],

            // Black Dragon
            ["Black Dragon|boost"]        = ["con:charges:3_per_scene"],
            ["Black Dragon|martial_arts"] = ["pro:penetrating"],

            // Blastwave — the sheet lists six energy types, so five are extra.
            ["Blastwave|energy_absorption"] = ["pro:also_x", "pro:also_x", "pro:also_x",
                                               "pro:also_x", "pro:also_x",
                                               "con:side_effect:detrimental"],

            // Combustion
            ["Combustion|elemental_control"] = ["pro:area_burst:area"],
            ["Combustion|flight"]            = ["con:signature"],

            // Darkwolf
            ["Darkwolf|regeneration"] = ["pro:fast"],

            // Herald (Airmid)
            ["Herald (Airmid)|alternate_form"]    = ["con:independent_forms"],
            ["Herald (Airmid)|elemental_control"] = ["pro:area_burst:area", "pro:zone_nova:zone_ranged"],

            // Herald (Scathach)
            ["Herald (Scathach)|alternate_form"] = ["con:independent_forms"],
            ["Herald (Scathach)|armor"]          = ["con:item"],
            ["Herald (Scathach)|strike"]         = ["pro:deflect", "con:item",
                                                    "pro:phase_shift", "pro:reach_throw"],

            // Pandora
            ["Pandora|omni_power"] = ["pro:area_burst:area"],

            // Psi Lance
            ["Psi Lance|armor"]        = ["con:activated"],
            ["Psi Lance|mind_control"] = ["con:costly"],
            ["Psi Lance|telepathy"]    = ["pro:cloak_others", "pro:mind_link"],

            // Psidearm
            ["Psidearm|telepathy"]                 = ["pro:mind_link"],
            ["Psidearm|armor"]                     = ["con:item"],
            ["Psidearm|communications"]            = ["con:item"],
            ["Psidearm|immunity"]                  = ["con:item"],
            ["Psidearm|super_senses_night_vision"] = ["con:item"],
            ["Psidearm|blast"]                     = ["con:weapons"],

            // Shadow
            ["Shadow|armor"]              = ["con:item"],
            ["Shadow|blending"]           = ["con:item"],
            ["Shadow|communications"]     = ["con:item"],
            ["Shadow|immunity"]           = ["con:item"],
            ["Shadow|invisibility"]       = ["con:item", "con:jamming"],
            ["Shadow|super_senses_acute"] = ["con:item"],
            ["Shadow|swing_line"]         = ["con:item"],
            ["Shadow|wall_crawling"]      = ["con:item"],
            ["Shadow|strike"]             = ["con:weapons"],

            // Siren
            ["Siren|armor"] = ["con:item"],
            ["Siren|blast"] = ["con:item"],

            // Stronghold
            ["Stronghold|armor"]          = ["con:item"],
            ["Stronghold|blast"]          = ["pro:area_burst:area", "con:item"],
            ["Stronghold|variant"]        = ["con:build_up", "con:charges:6_per_scene",
                                             "con:item", "pro:penetrating"],
            ["Stronghold|communications"] = ["con:item"],
            ["Stronghold|flight"]         = ["con:item"],
            ["Stronghold|immunity"]       = ["con:item"],
            ["Stronghold|omni_power"]     = ["con:item"],

            // T-Kay
            ["T-Kay|force_field"]        = ["pro:zone_nova:zone_ranged"],
            ["T-Kay|lightning_reflexes"] = ["con:limited:significantly_limited"],
            ["T-Kay|telekinesis"]        = ["pro:area_burst:area", "pro:overload",
                                            "pro:zone_nova:zone_ranged"],

            // Talon
            ["Talon|armor"]              = ["con:item"],
            ["Talon|ensnare"]            = ["con:item", "pro:line"],
            ["Talon|flight"]             = ["con:item"],
            ["Talon|strike"]             = ["con:item"],
            ["Talon|super_senses_acute"] = ["con:item"],

            // Vector
            ["Vector|blink"]        = ["con:exclusive"],
            // "Deflection (Physical and Energy)" — the Power picks one type unless you
            // pay double, which is what the parenthesis on the sheet is buying.
            ["Vector|deflection"]   = ["pro:both_types", "con:exclusive"],
            ["Vector|phasing"]      = ["con:exclusive"],
            ["Vector|regeneration"] = ["con:conditional:often_works"],
            ["Vector|telekinesis"]  = ["con:exclusive"],

            // Vigilant
            ["Vigilant|super_senses_night_vision"] = ["con:item"],
            ["Vigilant|swing_line"]                = ["con:item"]
        };

    /// <summary>
    /// The starting package each Hero was built with, and how far the rebuild still lands
    /// from their 125 Hero Point budget.
    ///
    /// <para>The sheets do not print which package was taken, so it is inferred: for the
    /// twelve Heroes with a residual of 0 the inference is certain, because exactly one
    /// package makes the total land on 125 to the point. For the other eight no package
    /// gets there, so the one recorded is simply the closest, and the residual is the part
    /// still unexplained — mostly Chapter 6 gear, which is not modelled.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Package, int Residual)> BuildByHero =
        new Dictionary<string, (string, int)>
        {
            // Exact: one package lands these on 125 to the Hero Point.
            ["Alabama Slammer"]   = ("superhero_package", 0),
            ["Black Dragon"]      = ("superhero_package", 0),
            ["Citizen Soldier"]   = ("superhero_package", 0),
            ["Darkwolf"]          = ("superhero_package", 0),
            ["Psi Lance"]         = ("superhero_package", 0),
            ["Psidearm"]          = ("superhero_package", 0),
            ["Siren"]             = ("superhero_package", 0),
            ["Combustion"]        = ("hero_package", 0),
            ["Eidolon"]           = ("hero_package", 0),
            ["Nano"]              = ("hero_package", 0),
            ["Pandora"]           = ("hero_package", 0),
            ["Blastwave"]         = ("civilian_package", 0),
            ["Stronghold"]        = ("superhero_package", 0),
            ["Talon"]             = ("superhero_package", 0),
            ["Vector"]            = ("hero_package", 0),

            // Not exact. Closest package, with what is left over. None of these is explained by
            // gear: Ch.6 makes mundane gear free, so that earlier guess was wrong.
            ["Herald (Scathach)"] = ("hero_package", 1),        // Strike carries four Pros/Cons at once
            ["Shadow"]            = ("superhero_package", 1),   // unexplained
            ["T-Kay"]             = ("superhero_package", -1),  // Limited grade is not stated
            ["Vigilant"]          = ("superhero_package", -1),  // Jo Sticks are Upgraded, a custom feature not modelled
            ["Herald (Airmid)"]   = ("hero_package", 0)
        };

    /// <summary>One Source heading on a printed sheet, and the Powers listed beneath it.</summary>
    public sealed record SourceGroup(string SourceId, string[] PowerIds);

    /// <summary>
    /// How each published sheet groups its Powers under Source headings, transcribed in the
    /// printed order (Ch.8). This is the sheet's own layout, not an inference: Alabama
    /// Slammer prints <c>INNATE POWERS</c> then <c>SUPER POWERS</c>, and Psidearm prints
    /// three groups.
    ///
    /// <para>Ability Sources are printed too, but as an <c>Abilities (…)</c> entry inside a
    /// Power group rather than on the Abilities block — Stronghold's four armoured
    /// Abilities sit under <c>TECH POWERS</c>. Those are not transcribed here because the
    /// engine gives Abilities no Source yet; see PROGRESS.md.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SourceGroup[]> PowerSourcesByHero =
        new Dictionary<string, SourceGroup[]>
        {
            ["Alabama Slammer"] =
            [
                new("innate", ["determination"]),
                new("super",  ["phasing", "regeneration", "resistance", "super_speed"])
            ],
            ["Black Dragon"] =
            [
                new("psychic", ["boost", "danger_sense", "leaping", "regeneration",
                                "resistance", "running", "super_senses_true_sight"]),
                new("trained", ["blind_fighting", "martial_arts", "two_fisted"])
            ],
            ["Blastwave"] =
            [
                new("super",   ["energy_absorption"]),
                new("trained", ["lightning_reflexes", "martial_arts", "running"])
            ],
            ["Citizen Soldier"] =
            [
                new("super",   ["armor", "regeneration"]),
                new("trained", ["determination", "leadership"])
            ],
            ["Combustion"] =
            [
                new("innate",  ["determination"]),
                new("super",   ["aura", "elemental_control", "flight", "immunity"]),
                new("trained", ["expertise", "evasion"])
            ],
            ["Darkwolf"] =
            [
                new("super", ["animal_empathy", "hard_to_kill", "leaping", "regeneration",
                              "running", "strike", "super_senses_acute",
                              "super_senses_enhanced_hearing", "super_senses_tracking_scent",
                              "super_senses_ultra_vision", "two_fisted"])
            ],
            ["Eidolon"] =
            [
                new("innate", ["omni_power", "light_effect"])
            ],
            ["Herald (Airmid)"] =
            [
                // Expertise twice: the sheet prints one for Medicine and one for Science.
                new("magic", ["alternate_form", "elemental_control", "expertise", "expertise",
                              "healing"])
            ],
            ["Herald (Scathach)"] =
            [
                new("magic", ["alternate_form", "attuned", "armor", "danger_sense",
                              "determination", "expertise", "lightning_reflexes",
                              "martial_arts", "regeneration", "strike", "weakness_detection"])
            ],
            ["Nano"] =
            [
                new("tech", ["communications", "determination", "form_gaseous", "immortality",
                             "inanimate", "form_liquid", "machine_control", "separation",
                             "transformation_shapeshifting", "super_senses_analytic",
                             "super_senses_circular_vision", "super_senses_microscopic_vision"])
            ],
            ["Pandora"] =
            [
                new("magic",   ["omni_power"]),
                new("trained", ["expertise"])
            ],
            ["Psi Lance"] =
            [
                new("psychic", ["armor", "mind_blast", "mind_control", "telekinesis", "telepathy"]),
                new("trained", ["expertise"])
            ],
            ["Psidearm"] =
            [
                new("super",   ["telepathy"]),
                new("tech",    ["armor", "communications", "immunity", "super_senses_night_vision"]),
                new("trained", ["blast", "lightning_reflexes", "martial_arts",
                                "master_of_disguise", "two_fisted"])
            ],
            ["Shadow"] =
            [
                new("tech",    ["armor", "blending", "communications", "immunity", "invisibility",
                                "super_senses_acute", "super_senses_night_vision",
                                "super_senses_telescopic_vision", "swing_line", "wall_crawling"]),
                new("trained", ["determination", "martial_arts", "preparation", "strike", "two_fisted"])
            ],
            ["Siren"] =
            [
                new("innate", ["blending", "transformation_doppelganger", "radar", "running",
                               "super_senses_ultra_vision", "swimming"]),
                new("tech",   ["armor", "blast"])
            ],
            ["Stronghold"] =
            [
                new("tech", ["armor", "blast", "variant", "communications", "flight",
                             "immunity", "omni_power"])
            ],
            ["T-Kay"] =
            [
                new("super", ["determination", "flight", "force_field",
                              "lightning_reflexes", "telekinesis"])
            ],
            ["Talon"] =
            [
                new("tech", ["armor", "ensnare", "flight", "strike", "super_senses_acute",
                             "super_senses_telescopic_vision", "super_senses_thermal_vision"])
            ],
            ["Vector"] =
            [
                new("magic",   ["attuned", "blink", "deflection", "immunity", "phasing",
                                "regeneration", "telekinesis"]),
                new("trained", ["expertise"])
            ],
            ["Vigilant"] =
            [
                new("tech",    ["super_senses_night_vision", "swing_line"]),
                new("trained", ["leaping", "martial_arts", "preparation", "running",
                                "two_fisted", "weakness_detection"])
            ]
        };

    /// <summary>The Source a given Hero's Power is printed under.</summary>
    public static string SourceOf(string heroName, string powerId) =>
        PowerSourcesByHero[heroName].Single(g => g.PowerIds.Contains(powerId)).SourceId;

    /// <summary>
    /// Every Ability id, for the sheets marked <c>Abilities and Talents (All)</c>. Declared
    /// above <see cref="TraitSourcesByHero"/> on purpose: static field initialisers run in
    /// declaration order, so spreading this in from below would spread a null.
    /// </summary>
    public static readonly string[] AllAbilities =
        ["agility", "intellect", "might", "perception", "toughness", "willpower"];

    /// <summary>
    /// Every Talent id, the same twelve as <see cref="TalentIds"/> — a second name so the
    /// entries below read as "all Abilities and all Talents". A property rather than a field
    /// because it forwards to <see cref="TalentIds"/>, which sidesteps the ordering trap
    /// above rather than working around it.
    /// </summary>
    public static string[] AllTalents => TalentIds;

    /// <summary>
    /// The Abilities and Talents each published sheet marks with a Source, keyed
    /// "Hero|sourceId". Ten of the twenty sheets carry such a marking; the other ten
    /// carry none, and their absence from this table is the transcription.
    ///
    /// <para>A sheet prints this as a line <em>inside</em> a Power group — Stronghold's
    /// <c>TECH POWERS</c> opens with <c>Abilities (Agility, Might, Perception, Toughness)
    /// (Item: armor)</c> — never as a marking on the Abilities block. The entries here are
    /// the Trait ids named on that line, or <see cref="AllAbilities"/> and
    /// <see cref="AllTalents"/> for the three sheets printed as
    /// <c>Abilities and Talents (All)</c>.</para>
    ///
    /// <para><b>This is not derivable from rank, and reading it as a threshold is the trap
    /// this table exists to close.</b> Ch.2 p.64 — the Random Hero Generator — says "the
    /// Sources for your Powers and Abilities with a rank of 7d or greater", and the sheets
    /// contradict it in both directions: Alabama Slammer marks 6d Perception and Toughness,
    /// and Citizen Soldier leaves 9d Willpower unmarked while marking his 12s.
    /// Ch.2 p.16 is the actual rule — every Trait has a Source, Abilities default to Innate
    /// and Talents to Trained, and those defaults hold "at least when dealing with ordinary
    /// people", after which "anything goes" — so what a sheet prints is the exception list,
    /// and only the author knows it.
    /// See <see cref="PrebuiltHeroTests.ThePrintedTraitSourcesAreNotARankThreshold"/>.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> TraitSourcesByHero =
        new Dictionary<string, string[]>
        {
            ["Alabama Slammer|super"] = ["perception", "toughness"],
            ["Blastwave|super"]       = ["toughness", "willpower"],
            ["Citizen Soldier|super"] = ["might", "toughness"],
            ["Darkwolf|super"]        = ["agility", "might", "perception", "toughness"],
            ["Psi Lance|psychic"]     = ["willpower"],
            ["Stronghold|tech"]       = ["agility", "might", "perception", "toughness"],
            ["T-Kay|super"]           = ["willpower"],

            // "Abilities and Talents (All)": the whole character on one Source. Both Heralds
            // are goddesses and Nano is a swarm of machines, so nothing about any of them is
            // Innate or Trained.
            ["Herald (Airmid)|magic"]   = [.. AllAbilities, .. AllTalents],
            ["Herald (Scathach)|magic"] = [.. AllAbilities, .. AllTalents],
            ["Nano|tech"]               = [.. AllAbilities, .. AllTalents]
        };

    /// <summary>
    /// The trait line each of those ten sheets prints, keyed the same way. This
    /// is the transcription the renderers are held to: <see cref="TraitSourcesByHero"/> is
    /// the input, this is the printed output, and a test builds one from the other.
    ///
    /// <para>Stronghold is the one that does not match character for character. His sheet
    /// reads <c>(Item: armor)</c>; the engine records the Item Con against each Ability but
    /// has nowhere to keep the "armor" half — <c>SelectedProCon</c> carries an id and a
    /// variant key, not a narrative label — so it prints the Con's name alone. That is a
    /// known and deliberate shortfall rather than a mismatch to be tuned away; giving
    /// Pros and Cons a free-text label is its own change.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PrintedTraitLinesByHero =
        new Dictionary<string, string>
        {
            ["Alabama Slammer|super"]   = "Abilities (Perception, Toughness)",
            ["Blastwave|super"]         = "Abilities (Toughness, Willpower)",
            ["Citizen Soldier|super"]   = "Abilities (Might, Toughness)",
            ["Darkwolf|super"]          = "Abilities (Agility, Might, Perception, Toughness)",
            ["Psi Lance|psychic"]       = "Abilities (Willpower)",
            ["Stronghold|tech"]         = "Abilities (Agility, Might, Perception, Toughness) (Item)",
            ["T-Kay|super"]             = "Abilities (Willpower)",
            ["Herald (Airmid)|magic"]   = "Abilities and Talents (All)",
            ["Herald (Scathach)|magic"] = "Abilities and Talents (All)",
            ["Nano|tech"]               = "Abilities and Talents (All)"
        };

    /// <summary>
    /// Pros and Cons a sheet applies to an Ability rather than a Power, keyed
    /// "Hero|abilityId". Stronghold is the only published Hero who does this: his sheet
    /// reads <c>Abilities (Agility, Might, Perception, Toughness) (Item: armor)</c>, so
    /// those four come from the armour and carry the Item Con.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> AbilityModifiersByHero =
        new Dictionary<string, string[]>
        {
            ["Stronghold|agility"]    = ["con:item"],
            ["Stronghold|might"]      = ["con:item"],
            ["Stronghold|perception"] = ["con:item"],
            ["Stronghold|toughness"]  = ["con:item"]
        };

    public static readonly IReadOnlyList<Hero> All =
    [
        new("Alabama Slammer", 127,
            Agility: 3, Intellect: 3, Might: 3, Perception: 6, Toughness: 6, Willpower: 5,
            Powers:
            [
                P("determination"), P("phasing"), P("regeneration"),
                P("resistance", 12), P("super_speed", 12)
            ],
            Flaws: ["quirk", "relationship", "secret_identity"],
            Edge: 36, Health: 6, Resolve: 4, DeterminationResolve: 3,
            Note: "Edge 36 is Super Speed 12d x 3, which is the only place that rule shows up in a printed sheet."),

        new("Black Dragon", 128,
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

        new("Blastwave", 129,
            Agility: 5, Intellect: 4, Might: 5, Perception: 5, Toughness: 10, Willpower: 7,
            Powers: [new("energy_absorption", 12, null, 1, "kinetic"), P("lightning_reflexes"), P("martial_arts", 10), P("running", 5)],
            Flaws: ["relationship", "secret_identity", "wanted"],
            Edge: 16, Health: 9, Resolve: 2),

        new("Citizen Soldier", 130,
            Agility: 6, Intellect: 6, Might: 12, Perception: 6, Toughness: 12, Willpower: 9,
            Powers: [P("armor", 12), P("regeneration"), P("determination"), P("leadership")],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 12, Health: 12, Resolve: 4, DeterminationResolve: 2),

        new("Combustion", 131,
            Agility: 4, Intellect: 3, Might: 3, Perception: 3, Toughness: 4, Willpower: 4,
            Powers:
            [
                P("determination"), P("aura", 9), P("elemental_control", 12), P("flight", 8),
                P("immunity"), new("expertise", 4, "professional"), P("evasion", 8)
            ],
            Flaws: ["finite_power", "quirk", "relationship"],
            Edge: 7, Health: 4, Resolve: 2, DeterminationResolve: 1),

        new("Darkwolf", 132,
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

        new("Eidolon", 133,
            Agility: 8, Intellect: 3, Might: 3, Perception: 6, Toughness: 8, Willpower: 6,
            Powers: [new("omni_power", 10, null, 1, "broad"), P("light_effect")],
            Flaws: ["enemy", "relationship", "outsider"],
            Edge: 14, Health: 7, Resolve: 6),

        new("Herald (Airmid)", 134,
            Agility: 6, Intellect: 4, Might: 4, Perception: 6, Toughness: 4, Willpower: 8,
            Powers:
            [
                // Two Expertise Powers, both printed at 12d: "Expertise (Medicine: Ancient
                // Remedies)" and "Expertise (Science: Botany)". The second was missing, and it
                // is the whole of her residual — Expertise costs half a Hero Point per rank and
                // takes its baseline from the nominated Trait, so 12d over Science 2d is ten
                // purchased ranks and exactly 5 HP.
                new("alternate_form", 0, null, 3), P("elemental_control", 10),
                new("expertise", 12, "medicine"), new("expertise", 12, "science"),
                P("healing", 10)
            ],
            Flaws: ["alter_ego", "relationship", "secret_identity"],
            Edge: 12, Health: 6, Resolve: 5),

        new("Herald (Scathach)", 135,
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

        new("Nano", 136,
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

        new("Pandora", 137,
            Agility: 3, Intellect: 5, Might: 3, Perception: 4, Toughness: 3, Willpower: 12,
            Powers: [new("omni_power", 12, null, 1, "broad"), new("expertise", 10, "academics")],
            Flaws: ["enemy", "relationship", "secret_identity"],
            Edge: 9, Health: 8, Resolve: 2),

        new("Psi Lance", 138,
            Agility: 3, Intellect: 6, Might: 3, Perception: 3, Toughness: 3, Willpower: 12,
            Powers:
            [
                P("armor", 10), P("mind_blast", 12), P("mind_control", 6), P("telekinesis", 6),
                P("telepathy", 12), new("expertise", 10, "medicine")
            ],
            Flaws: ["obligation", "relationship", "secret_identity"],
            Edge: 9, Health: 8, Resolve: 2),

        new("Psidearm", 139,
            Agility: 10, Intellect: 3, Might: 3, Perception: 6, Toughness: 5, Willpower: 9,
            Powers:
            [
                P("telepathy", 9), P("armor", 6), P("communications"), P("immunity"),
                P("super_senses_night_vision"), P("blast", 10), P("lightning_reflexes"),
                P("martial_arts", 6), P("master_of_disguise"), P("two_fisted")
            ],
            Flaws: ["compulsion", "relationship", "secret_identity"],
            Edge: 22, Health: 7, Resolve: 5),

        new("Shadow", 140,
            Agility: 10, Intellect: 3, Might: 3, Perception: 8, Toughness: 4, Willpower: 6,
            Powers:
            [
                P("armor", 6), P("blending"), P("communications"), P("immunity"),
                P("invisibility"), P("super_senses_acute", 10), P("super_senses_night_vision"),
                P("super_senses_telescopic_vision"), P("swing_line", 6), P("wall_crawling", 4),
                P("determination"), P("martial_arts", 8), P("preparation"), P("strike", 10),
                P("two_fisted")
            ],
            Flaws: ["enemy", "quirk", "secret_identity"],
            Edge: 18, Health: 5, Resolve: 6, DeterminationResolve: 1),

        new("Siren", 141,
            Agility: 9, Intellect: 6, Might: 9, Perception: 6, Toughness: 9, Willpower: 6,
            Powers:
            [
                P("blending"), P("transformation_doppelganger"), P("radar"), P("running", 5),
                P("super_senses_ultra_vision"), P("swimming", 6), P("armor", 10), P("blast", 10)
            ],
            Flaws: ["light_sensitive", "quirk", "relationship"],
            Edge: 15, Health: 9, Resolve: 5),

        new("Stronghold", 142,
            Agility: 6, Intellect: 10, Might: 10, Perception: 6, Toughness: 10, Willpower: 5,
            Powers:
            [
                P("armor", 11), P("blast", 11), P("variant"), P("communications"),
                P("flight", 9), new("immunity", 0, null, 2), new("omni_power", 2, null, 1, "narrow")
            ],
            Flaws: ["finite_power", "relationship", "secret_identity"],
            Edge: 16, Health: 10, Resolve: 3),

        new("T-Kay", 143,
            Agility: 4, Intellect: 3, Might: 3, Perception: 4, Toughness: 3, Willpower: 9,
            Powers:
            [
                P("determination"), P("flight", 8), P("force_field", 12),
                P("lightning_reflexes"), P("telekinesis", 12)
            ],
            Flaws: ["aversion_fear", "relationship", "secret_identity"],
            Edge: 14, Health: 6, Resolve: 3, DeterminationResolve: 2,
            Note: "The sheet prints \"Edge 8/14\" because this Lightning Reflexes carries Limited (only for Telekinesis), so the +6 applies conditionally. The engine has no notion of a conditionally active Con and reports the unrestricted 14."),

        new("Talon", 144,
            Agility: 6, Intellect: 6, Might: 6, Perception: 6, Toughness: 6, Willpower: 9,
            Powers:
            [
                P("armor", 10), P("ensnare", 10), P("flight", 9), P("strike", 10),
                P("super_senses_acute", 10), P("super_senses_telescopic_vision"),
                P("super_senses_thermal_vision")
            ],
            Flaws: ["relationship", "secret", "secret_identity"],
            Edge: 12, Health: 8, Resolve: 5),

        new("Vector", 145,
            Agility: 3, Intellect: 6, Might: 3, Perception: 4, Toughness: 4, Willpower: 4,
            Powers:
            [
                P("attuned"), P("blink", 10), P("deflection", 10), P("immunity"),
                P("phasing"), P("regeneration"), P("telekinesis", 6),
                new("expertise", 12, "science")
            ],
            Flaws: ["enemy", "finite_power", "relationship"],
            Edge: 10, Health: 4, Resolve: 6),

        new("Vigilant", 146,
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
