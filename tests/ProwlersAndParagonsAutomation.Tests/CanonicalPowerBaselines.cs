namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Which Trait, and by which relationship, each of the 27 baseline-rank Powers in
/// Prowlers &amp; Paragons Ultimate Edition derives its free baseline rank from — transcribed
/// from each Power's own stat line ("Baseline Rank (X)") in Chapter 2, not from
/// data/rules/powers.json.
///
/// <para>Before this file, <c>PowerDataTests.PrerequisiteRelationshipIsOneTheCalculatorHandles</c>
/// checked only that a baseline Power's Prerequisite named a relationship the calculator
/// understands and, for <c>baseline_equal</c>/<c>baseline_half</c>, that its Ability id
/// resolved to a real Ability — never that it was the <em>right</em> Ability. Swapping Armor's
/// baseline from Toughness to Willpower, for example, changes nothing an existing test could
/// see.</para>
///
/// <para>Sixteen of the 27 are the Super Senses options, all of which share one printed stat
/// line — "Self • Baseline Rank (Perception) • ..." — verified individually against p.43
/// rather than assumed from the group's shared cost table.</para>
/// </summary>
public static class CanonicalPowerBaselines
{
    public sealed record Entry(
        string PowerId,
        string Relationship,
        string? Ability,
        int? FixedValue,
        IReadOnlyList<string> Powers,
        int Page,
        string Printed);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("armor", "baseline_half", "toughness", null, [], 22,
            "Self - Baseline Rank (½ Toughness) - 1 Hero Point per rank"),
        new("boost", "baseline_selected_trait", null, null, [], 24,
            "Self - Baseline Rank (Special) - Special. \"You can raise the rank of one specific Ability, Talent, or Power (pick one)...This Power costs as many Hero Points per rank as the Trait it affects and uses that Trait as its baseline rank.\""),
        new("danger_sense", "baseline_equal", "perception", null, [], 25,
            "Self - Baseline Rank (Perception) - 1 Hero Point per rank"),
        new("evasion", "baseline_equal", "agility", null, [], 28,
            "Self - Baseline Rank (Agility) - 1 Hero Point per 2 ranks"),
        new("expertise", "baseline_selected_trait", null, null, [], 28,
            "Self - Baseline Rank (Special) - 1 Hero Point per 2 ranks. \"This Power's baseline rank equals the rank of the Ability or Talent it falls under.\""),
        new("leaping", "baseline_half", "might", null, [], 32,
            "Self - Baseline Rank (½ Might) - 1 Hero Point per rank"),
        new("martial_arts", "baseline_equal", "might", null, [], 34,
            "Touch - Baseline Rank (Might) - 1 Hero Point per rank"),
        new("psi_screen", "baseline_equal", "willpower", null, [], 38,
            "Self - Baseline Rank (Willpower) - 1 Hero Point per rank"),
        new("resistance", "baseline_equal", "toughness", null, [], 39,
            "Self - Baseline Rank (Toughness) - 1 Hero Point per 2 ranks"),
        new("running", "baseline_fixed", null, 3, [], 39,
            "Self - Baseline Rank (3d) - 1 Hero Point per rank. \"This Power's 3d baseline rank reflects standard human running speed.\""),
        new("strike", "baseline_greater_of", "might", null, ["martial_arts"], 41,
            "Touch - Baseline Rank (Might) or (Martial Arts) - 1 Hero Point per rank"),

        // Super Senses (p.43): sixteen options, each printed "Self - Baseline Rank
        // (Perception) - ..." with its own cost, verified per entry rather than assumed.
        new("super_senses_acute", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point per 2 ranks (Acute X)"),
        new("super_senses_analytic", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Analytic X)"),
        new("super_senses_astral_sight", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Astral Sight)"),
        new("super_senses_circular_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Circular Vision)"),
        new("super_senses_enhanced_hearing", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Enhanced Hearing)"),
        new("super_senses_hypersensitive_touch", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Hypersensitive Touch)"),
        new("super_senses_lie_detection", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 3 Hero Points (Lie Detection)"),
        new("super_senses_microscopic_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Microscopic Vision)"),
        new("super_senses_night_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 3 Hero Points (Night Vision)"),
        new("super_senses_radio_hearing", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Radio Hearing)"),
        new("super_senses_telescopic_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Telescopic Vision)"),
        new("super_senses_thermal_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 2 Hero Points (Thermal Vision)"),
        new("super_senses_tracking_scent", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 1 Hero Point (Tracking Scent)"),
        new("super_senses_true_sight", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 6 Hero Points (True Sight)"),
        new("super_senses_ultra_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 2 Hero Points (Ultra Vision)"),
        new("super_senses_x_ray_vision", "baseline_equal", "perception", null, [], 43,
            "Self - Baseline Rank (Perception) - 6 Hero Points (X-Ray Vision)"),
    ];
}
