using System.Security.Cryptography;
using System.Text;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>A digest of every player-facing Power description, so that the claim it was checked
/// against the rulebook cannot outlive the text it was checked on.</b>
///
/// <para>Each entry in <c>powers.json</c> carries <c>verified_fields</c>, and
/// <c>PowerDescriptionTests.DescriptionIsVerifiedAgainstTheRulebook</c> asserts that
/// <c>description</c> is among them. That flag is a boolean claim about a page somebody read —
/// and <b>it survives any edit to the description it was made about</b>. Armor's whole
/// description was replaced with "A quiet afternoon in the garden, with tea." and every test in
/// the suite stayed green, the verified flag included: the presence checks saw prose of the
/// right length ending in a full stop, and the one consistency rule there is only fires on a
/// rankless Power claiming per-rank scaling.</para>
///
/// <para><b>Why a digest rather than a copy of the prose.</b> Three other things were measured
/// first and none of them is a rule. A description need not repeat its own Power's name — 48 of
/// the 141 do not, and that is good writing rather than a fault. Word overlap against the
/// printed entry in <c>data/rulebook/</c> does not work either, because these descriptions are
/// deliberately <em>re-worded</em> rather than quoted: Blind Fighting's shares one distinctive
/// word in nine with the page it was written from ("sight" for "vision", "fight" for "combat"),
/// which is the policy working, not a defect. Ranked against all 1439 sections of the book, 130
/// of the 141 match their own entry best — but Blind Fighting comes 215th and the Super Senses
/// options cannot be scored at all, because the book gives the group one entry rather than one
/// per option. Any similarity test therefore needs a threshold plus a list of named exemptions,
/// which is the shape of guard this repository keeps discovering was theatre.</para>
///
/// <para>So this pins what can honestly be pinned: <b>the text is the text that was verified</b>.
/// A description that changes fails here, and the failure names the page to go and read. It is
/// the same bargain as <see cref="CanonicalPowers"/> and <see cref="PrebuiltHeroes"/> — a record
/// somebody made against the book — compressed to one line each because the prose is already in
/// <c>data/rules/</c> and a second copy of it would rot rather than guard.</para>
///
/// <para><b>What this does not say is that a description is true</b>, and nothing here can. A
/// sentence that is wrong about the rules has a stable digest like any other. That is the same
/// limit <c>CLAUDE.md</c> records for the replay transcripts, and it is closed by reading the
/// page rather than by a test.</para>
/// </summary>
internal static class CanonicalPowerDescriptions
{
    /// <summary>The first 12 hex characters of the SHA-256 of each description, as verified.</summary>
    public static readonly Dictionary<string, string> Digests = new(StringComparer.Ordinal)
    {
        ["adaptation"] = "8f2602c868f0",
        ["alternate_form"] = "960d27962862",
        ["animal_control"] = "990155b4cd5c",
        ["animal_empathy"] = "13764ecd3489",
        ["animal_mimicry"] = "6d41e27ec608",
        ["animation"] = "bace857faf8f",
        ["armor"] = "17c46b75d79d",
        ["astral_projection"] = "2b860496553d",
        ["attuned"] = "a6137cca4ec9",
        ["aura"] = "268d36ec46e0",
        ["banish"] = "fed69f8280e4",
        ["blast"] = "12afa5040146",
        ["blending"] = "1483947d3d20",
        ["blind"] = "64fedf7178d4",
        ["blind_fighting"] = "09f03988d2cb",
        ["blink"] = "220f74d2cf97",
        ["boost"] = "ad2879474ed2",
        ["buff"] = "23e3028f352f",
        ["clairvoyance"] = "275db29cda38",
        ["cloud_minds"] = "c9c8073d4ca4",
        ["communications"] = "0932a1708add",
        ["constructs"] = "059dcc1b2fae",
        ["danger_sense"] = "f30736ee4bf8",
        ["darkness"] = "3575b985197b",
        ["dazzle"] = "1baa4aaab50c",
        ["deflection"] = "228beb9a3612",
        ["density"] = "8110e8ae5d2c",
        ["detection"] = "28bae71b9819",
        ["determination"] = "eebca917e13f",
        ["dimensional_travel"] = "720162daf573",
        ["dispel"] = "6d3f2369b229",
        ["drain"] = "f1f1ac84bac4",
        ["duplication"] = "8e90592cd498",
        ["elemental_control"] = "7943b3c3f207",
        ["emotion_control"] = "abdf6424476b",
        ["energy_absorption"] = "d998fbe5b277",
        ["ensnare"] = "870874d5e135",
        ["evasion"] = "d22167298874",
        ["expertise"] = "e5f6951c04cb",
        ["extra_limbs"] = "1e89dfca7ab6",
        ["flight"] = "abb4927f6999",
        ["force_field"] = "8cf329a28162",
        ["form_energy"] = "30a7826eac8f",
        ["form_gaseous"] = "02448c73f2df",
        ["form_liquid"] = "2e36816e40a0",
        ["form_solid"] = "4df31111de21",
        ["gestalt"] = "ba73bedd4d73",
        ["growth"] = "965fa0cca4e6",
        ["hard_to_kill"] = "c19879e78d7c",
        ["healing"] = "695378c87f3b",
        ["hibernation"] = "dd94d9ca201a",
        ["hyper_breath"] = "7616395ee909",
        ["illusions"] = "c56689ce1789",
        ["immortality"] = "24b3188a8cbb",
        ["immunity"] = "85bbcb578ed5",
        ["inanimate"] = "07e9da51260e",
        ["invisibility"] = "71ad64ce4e18",
        ["irritant"] = "97febf6f4efd",
        ["languages"] = "f9d4e8b4ebf3",
        ["leadership"] = "da0003dbbf9c",
        ["leaping"] = "3490737073f6",
        ["life_drain"] = "d56ca64dde09",
        ["light_effect"] = "870a130e39b7",
        ["lightning_reflexes"] = "6f75db50e678",
        ["luck"] = "e545a4a32e79",
        ["machine_control"] = "fddb99455339",
        ["martial_arts"] = "3c736615b34c",
        ["master_of_disguise"] = "4c5e82b2707f",
        ["matter_chameleon"] = "2e1b4ba2f86f",
        ["mind_blast"] = "f094aa252ff8",
        ["mind_control"] = "c9229e83cfe9",
        ["nullify"] = "f22028c8a988",
        ["omni_power"] = "c1e1dcdef23a",
        ["phasing"] = "a1ce5ec526e0",
        ["plasticity"] = "0d6a1fbda581",
        ["polymorph"] = "4ede33180986",
        ["portable_storehouse"] = "e776f6f43b70",
        ["possession"] = "6c1693262edf",
        ["power_absorption"] = "cec7eb3fb59b",
        ["power_mimicry"] = "6b1b1cef21e1",
        ["precognition"] = "22efbdd76577",
        ["preparation"] = "041e9ff534e9",
        ["psi_screen"] = "657acb4f3486",
        ["psychometry"] = "2732c3e92726",
        ["quick_change"] = "fb36c438f881",
        ["radar"] = "a8ca8ad18451",
        ["regeneration"] = "f3487be816c5",
        ["resistance"] = "69ca52660fee",
        ["running"] = "6dede386b8b3",
        ["separation"] = "c414af21bbff",
        ["shockwave"] = "8f1fdeb655d6",
        ["shrinking"] = "a4b64f0b56bd",
        ["slay"] = "eaceda788577",
        ["slick"] = "ba784a8ea747",
        ["speak_with_dead"] = "25e178bbaf56",
        ["specialty"] = "6ce7175e0d09",
        ["spinning"] = "80f38807087f",
        ["star_gate"] = "b44d372b7003",
        ["stretching"] = "03c61418af47",
        ["strike"] = "695e6437238d",
        ["stun"] = "30dcfe8acec0",
        ["summoning"] = "f9c74ecee2a4",
        ["super_senses_acute"] = "e4302729540e",
        ["super_senses_analytic"] = "55b453996377",
        ["super_senses_astral_sight"] = "e4e79693cfa8",
        ["super_senses_circular_vision"] = "1738ac8eb8b2",
        ["super_senses_enhanced_hearing"] = "aa9309567877",
        ["super_senses_hypersensitive_touch"] = "a5db0e873a3f",
        ["super_senses_lie_detection"] = "6d8152936c27",
        ["super_senses_microscopic_vision"] = "957f4df04e9e",
        ["super_senses_night_vision"] = "76c7630bbf77",
        ["super_senses_radio_hearing"] = "f0f9444f30ec",
        ["super_senses_telescopic_vision"] = "036a31f522db",
        ["super_senses_thermal_vision"] = "c559e02e79db",
        ["super_senses_tracking_scent"] = "3a656736ef79",
        ["super_senses_true_sight"] = "751231e18c5b",
        ["super_senses_ultra_vision"] = "169b97f93b38",
        ["super_senses_x_ray_vision"] = "6efae08ca320",
        ["super_speed"] = "628460a247c9",
        ["swimming"] = "f1b2c05afd62",
        ["swing_line"] = "f76d5ff2c72d",
        ["telekinesis"] = "41765eb1bb35",
        ["telepathy"] = "e276e45a3ae6",
        ["teleportation"] = "6e3034eff1e7",
        ["time_stop"] = "bfda7d14da33",
        ["time_travel"] = "008a2e9fffe6",
        ["total_recall"] = "1908ded94eff",
        ["tracer"] = "a412b42c0653",
        ["transformation_animal_forms"] = "c7d1251420fb",
        ["transformation_doppelganger"] = "eb422e8782ce",
        ["transformation_object_forms"] = "c766b8dca84e",
        ["transformation_shapeshifting"] = "f519a03e641c",
        ["transmutation"] = "ccd16bfbd122",
        ["tunneling"] = "5705422a2a95",
        ["two_dimensional"] = "6eef7fb8acb6",
        ["two_fisted"] = "7b92350add20",
        ["vanish"] = "09300239bd28",
        ["variant"] = "01010cfc1d07",
        ["ventriloquism"] = "b4e3293c1a18",
        ["wall_crawling"] = "f99cc283557f",
        ["weakness_detection"] = "290cbaffaadb",
    };

    /// <summary>The digest of a description as it stands now, in the same form as the table.</summary>
    public static string DigestOf(string description) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(description)))
            .ToLowerInvariant()[..12];
}
