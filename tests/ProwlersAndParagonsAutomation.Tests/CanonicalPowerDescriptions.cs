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
        ["adaptation"] = "f368c4192bc5",
        ["alternate_form"] = "5fcb27614af5",
        ["animal_control"] = "69443f430968",
        ["animal_empathy"] = "bf4f334e2b49",
        ["animal_mimicry"] = "520a8f4b0565",
        ["animation"] = "d2876b041e4f",
        ["armor"] = "fd3384ac0794",
        ["astral_projection"] = "9339a42b73a7",
        ["attuned"] = "8199fc936c02",
        ["aura"] = "549eb16f18ed",
        ["banish"] = "35ff9a86128d",
        ["blast"] = "a4681d9d28e6",
        ["blending"] = "2d106dd11a37",
        ["blind"] = "5dd7b47d1e0f",
        ["blind_fighting"] = "35d0550e1a25",
        ["blink"] = "7ee3acc760e8",
        ["boost"] = "40e91547b48a",
        ["buff"] = "ed09125451b8",
        ["clairvoyance"] = "61cdd23116a5",
        ["cloud_minds"] = "33169f721302",
        ["communications"] = "635df145b703",
        ["constructs"] = "94b82204335c",
        ["danger_sense"] = "f4040530807c",
        ["darkness"] = "9e7c01510359",
        ["dazzle"] = "24ba400bb509",
        ["deflection"] = "687ab31e7b04",
        ["density"] = "19bf92c56f26",
        ["detection"] = "bd47c695c1b9",
        ["determination"] = "fd825e5dfc32",
        ["dimensional_travel"] = "ac5f3783e77d",
        ["dispel"] = "a8ccae1f151f",
        ["drain"] = "ce426763f0db",
        ["duplication"] = "b83deeb21e65",
        ["elemental_control"] = "ab649c440613",
        ["emotion_control"] = "b24fcbeb060f",
        ["energy_absorption"] = "7af04ff0aafd",
        ["ensnare"] = "379f4277b266",
        ["evasion"] = "66aee1e41312",
        ["expertise"] = "8f74661a23f9",
        ["extra_limbs"] = "bd31bac0b76b",
        ["flight"] = "4d7e6167c1b6",
        ["force_field"] = "7158d5deb578",
        ["form_energy"] = "0a24b168a640",
        ["form_gaseous"] = "ad344986d1e5",
        ["form_liquid"] = "a12132c69b7d",
        ["form_solid"] = "bc393cc2cfca",
        ["gestalt"] = "e3ce0391b337",
        ["growth"] = "3403f09d7826",
        ["hard_to_kill"] = "72bba6701798",
        ["healing"] = "4f6d7edf257e",
        ["hibernation"] = "15e2d685225f",
        ["hyper_breath"] = "81b5db0dbbb1",
        ["illusions"] = "ba907a5cd3b9",
        ["immortality"] = "d9cd882fbfc3",
        ["immunity"] = "16a28f714f62",
        ["inanimate"] = "687b8663de29",
        ["invisibility"] = "64c5c83d3f65",
        ["irritant"] = "560a056bf547",
        ["languages"] = "818708c80ad1",
        ["leadership"] = "b0cb8cfeac59",
        ["leaping"] = "79be2671c972",
        ["life_drain"] = "c4299f0d1cf1",
        ["light_effect"] = "4860c21ca7b3",
        ["lightning_reflexes"] = "1789a24dd93c",
        ["luck"] = "485575f6b79a",
        ["machine_control"] = "7d17ac9eedd5",
        ["martial_arts"] = "24899676b681",
        ["master_of_disguise"] = "3b2827a35596",
        ["matter_chameleon"] = "27370e5f66a2",
        ["mind_blast"] = "72f66ed83354",
        ["mind_control"] = "b887eb119b22",
        ["nullify"] = "8bf12f70766a",
        ["omni_power"] = "211d938e2f7d",
        ["phasing"] = "88b06f2cb17f",
        ["plasticity"] = "1c0050354cd0",
        ["polymorph"] = "05775602dc3d",
        ["portable_storehouse"] = "b24ebb1c158e",
        ["possession"] = "3a94b8613470",
        ["power_absorption"] = "5400152746d6",
        ["power_mimicry"] = "f2bfde5c14da",
        ["precognition"] = "6671f41c2d08",
        ["preparation"] = "ba83cd323303",
        ["psi_screen"] = "c099902ecd7b",
        ["psychometry"] = "96b170ff7ac8",
        ["quick_change"] = "cbf6f184d0d7",
        ["radar"] = "eae0d3981492",
        ["regeneration"] = "a93a9f577692",
        ["resistance"] = "7d456ababdcd",
        ["running"] = "9c9ae9397b70",
        ["separation"] = "2e97fa940da9",
        ["shockwave"] = "74a84594ccea",
        ["shrinking"] = "891cd8f684ca",
        ["slay"] = "49c43a01667c",
        ["slick"] = "6e405e5e274a",
        ["speak_with_dead"] = "17d7d06d3b92",
        ["specialty"] = "290a8a44715a",
        ["spinning"] = "06832d0c3bf0",
        ["star_gate"] = "f41046546755",
        ["stretching"] = "d54605eb8650",
        ["strike"] = "43855980bc73",
        ["stun"] = "0809d2d28b09",
        ["summoning"] = "d620703aead2",
        ["super_senses_acute"] = "9468df8c8868",
        ["super_senses_analytic"] = "42855e85bc86",
        ["super_senses_astral_sight"] = "6e12adf115dc",
        ["super_senses_circular_vision"] = "6bdf28eb81d1",
        ["super_senses_enhanced_hearing"] = "9378f75a84dd",
        ["super_senses_hypersensitive_touch"] = "23851c4a8516",
        ["super_senses_lie_detection"] = "4446beaa4a53",
        ["super_senses_microscopic_vision"] = "c750b0ca087d",
        ["super_senses_night_vision"] = "edca7890130b",
        ["super_senses_radio_hearing"] = "f0f9444f30ec",
        ["super_senses_telescopic_vision"] = "38e1dbfd47ab",
        ["super_senses_thermal_vision"] = "a82843e85acf",
        ["super_senses_tracking_scent"] = "78252d046894",
        ["super_senses_true_sight"] = "d7dba54be9ed",
        ["super_senses_ultra_vision"] = "6155c59de9f3",
        ["super_senses_x_ray_vision"] = "dc749d1bc8d1",
        ["super_speed"] = "120cb89f833a",
        ["swimming"] = "b37fe694056f",
        ["swing_line"] = "2446b38c819c",
        ["telekinesis"] = "c5e8faf91c40",
        ["telepathy"] = "ce4bcd258d89",
        ["teleportation"] = "1c688a7badc0",
        ["time_stop"] = "d9ca2cb8f8cc",
        ["time_travel"] = "c85b99f0b04d",
        ["total_recall"] = "4b6fc42e8385",
        ["tracer"] = "5ac8a8654997",
        ["transformation_animal_forms"] = "2688314ccd74",
        ["transformation_doppelganger"] = "6ff16978dd3b",
        ["transformation_object_forms"] = "1980644ac03e",
        ["transformation_shapeshifting"] = "3ace9eb7df36",
        ["transmutation"] = "941c8cbf4a53",
        ["tunneling"] = "bd29531c8859",
        ["two_dimensional"] = "6b0bd6cc5a4b",
        ["two_fisted"] = "4b8ae41082ca",
        ["vanish"] = "873215485b61",
        ["variant"] = "de8856f33e7a",
        ["ventriloquism"] = "b63fbb73ac51",
        ["wall_crawling"] = "787344b3d6f4",
        ["weakness_detection"] = "28bb75298f9e",
    };

    /// <summary>The digest of a description as it stands now, in the same form as the table.</summary>
    public static string DigestOf(string description) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(description)))
            .ToLowerInvariant()[..12];
}
