namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The fifteen applicability caveats, and the printed sentence behind each one.</b>
///
/// <para>A caveat exists because the constraint it carries is not something the rulebook prints
/// per Power — "Powers that inflict physical or energy damage" cannot be decided from anything
/// in a Power's entry, and deciding it here would mean inventing the data the old
/// <c>available_pros</c> lists were made of. So the design is that the caveat is <em>shown to
/// the player instead of being enforced</em>, which makes its wording the entire deliverable:
/// there is no mechanism behind it to be right when the sentence is wrong.</para>
///
/// <para>Nothing held that wording to anything. Penetrating's caveat was replaced with "Applies
/// to absolutely any Power at all, no conditions." — the opposite of what the book says, on the
/// one field a player is meant to act on — and the suite stayed green, because the only test
/// asked whether it was non-blank and ended in a full stop.</para>
///
/// <para><b>Two halves, and the second is what stops this being a copy of the data.</b>
/// <see cref="Caveat"/> is what <c>data/rules/</c> must carry, so a change to it fails.
/// <see cref="PrintedConstraint"/> is the clause the caveat restates, and it is asserted to
/// appear verbatim in <c>data/rulebook/ch02-characters.json</c> — so the transcription cannot
/// drift into something the book does not say, which a table checked only against itself
/// could.</para>
///
/// <para>Two things worth knowing before editing. Area/Burst and Zone/Nova are printed under one
/// AREA OF EFFECT heading and share its opening clause, which is why they share a constraint
/// here. And the Only Inanimate <em>Con</em> genuinely opens "This Pro applies to…" in the book;
/// that is the publisher's slip, transcribed rather than tidied, because this field exists to
/// record what is printed.</para>
/// </summary>
internal static class CanonicalCaveats
{
    internal sealed record Entry(string Id, bool IsPro, string PrintedConstraint, string Caveat);

    public static readonly Entry[] All =
    [
        new("affect_inanimate", true,
            "This Pro applies to Powers that affect only living beings.",
            "Only for a Power that affects living beings alone."),

        new("area_burst", true,
            "These Pros apply to Powers that only affect individual targets",
            "Only for a Power that targets individuals rather than an area."),

        new("zone_nova", true,
            "These Pros apply to Powers that only affect individual targets",
            "Only for a Power that targets individuals rather than an area."),

        new("armor_piercing", true,
            "This Pro applies to Powers that inflict physical or energy damage.",
            "Only for a Power that inflicts physical or energy damage."),

        new("carrier_attack", true,
            "This Pro applies to attack Powers.",
            "Only for an attack Power."),

        new("expansive", true,
            "You cannot apply this Pro to Powers that affect targets directly like Dazzle or Shockwave",
            "Not for a Zone Power that targets a subject directly rather than an area."),

        new("imbue", true,
            "This Pro applies to Powers that only affect you.",
            "Only for a Power that affects you alone."),

        new("ongoing", true,
            "This Pro applies to Powers that inflict damage.",
            "Only for a Power that inflicts damage."),

        new("penetrating", true,
            "This Pro applies to Powers that inflict physical or energy damage",
            "Only for a Power that inflicts physical or energy damage."),

        new("resisted", true,
            "This Pro applies to Powers that inflict special effects and can be resisted with active defenses.",
            "Only for a Power that inflicts a special effect and can be resisted by an active defence."),

        new("ricochet", true,
            "This Pro applies to ranged attack Powers.",
            "Only for an attack Power."),

        new("only_inanimate", false,
            "This Pro applies to Powers that only affect living beings.",
            "Only for a Power that affects living beings alone."),

        new("constant", false,
            "This Con applies to Powers that can be activated and deactivated at will.",
            "Only for a Power that can normally be switched on and off at will."),

        new("uncontrolled", false,
            "This Con applies to Powers that can be activated and deactivated at will.",
            "Only for a Power that can normally be switched on and off at will."),

        new("sustained", false,
            "This Con applies to Powers that last or can be maintained for some amount of time.",
            "Only for a Power that lasts, or can be maintained, for a length of time."),
    ];
}
