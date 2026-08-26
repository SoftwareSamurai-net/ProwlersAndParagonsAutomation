namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// What Chapter 2 actually says about which Ability underlies each of the twelve Talents —
/// and, honestly, that is almost nothing. This is not a transcription of a printed table,
/// because <b>no such table exists in the rulebook.</b>
///
/// <para>Every Ability's own entry (pp.17-18) and every Talent's own entry (p.18) was read
/// looking for a pairing. The single explicit statement anywhere in the book is on p.17,
/// inside the Agility entry: "you can substitute half your Agility for your Covert when
/// making challenge rolls to hide, move quietly, or avoid detection." That backs exactly one
/// of the twelve <c>linked_ability</c> values in data/rules/talents.json — Covert : agility.
/// The Intellect entry (p.17) states a second substitution rule, but it is generic to
/// <em>any</em> Talent ("you can substitute half your Intellect for any Talent when making
/// challenge rolls to determine what you know about something"), not a pairing to one.</para>
///
/// <para>Challenge rolls themselves (Ch.1 p.9) are rolled against "the Trait that applies to
/// whatever your character is doing" — one Trait at a time, with no Ability+Talent combination
/// rule that would require a fixed pairing. <c>git log -p</c> on talents.json shows all twelve
/// <c>linked_ability</c> values present verbatim in the very first commit that added rules
/// data, with no comment recording a source. So eleven of the twelve values below are this
/// project's own categorization — used only to group Talents under an Ability heading in the
/// CLI and browser displays (<c>AbilitiesTab.razor</c>, <c>TalentsTab.razor</c>,
/// <c>BuyCharacteristicsStep.cs</c>) — not a fact the book states.</para>
///
/// <para>This file therefore does two different jobs. For <c>covert</c> it is a real
/// transcription, checked against p.17 the way every other Canonical* file is. For the other
/// eleven it is a recorded snapshot of the current data with <c>BookSourced: false</c>, so a
/// change to any of them is still caught by <see cref="TalentLinkedAbilityTests"/> — but the
/// test's failure message says what this file says: there is no page to check it against.</para>
/// </summary>
public static class CanonicalTalentLinkedAbilities
{
    public sealed record Entry(string TalentId, string Ability, bool BookSourced, int? Page, string Evidence);

    public static readonly IReadOnlyList<Entry> All =
    [
        new("academics", "intellect", false, null,
            "Not stated in the book. Academics' own entry (p.18) describes its subject matter and gives no Ability pairing."),
        new("charm", "willpower", false, null,
            "Not stated in the book. Charm's own entry (p.18) gives no Ability pairing."),
        new("command", "willpower", false, null,
            "Not stated in the book. Command's own entry (p.18) gives no Ability pairing."),
        new("covert", "agility", true, 17,
            "\"...you can substitute half your Agility for your Covert when making challenge rolls to hide, move quietly, or avoid detection.\" (Agility's own entry, p.17)"),
        new("investigation", "perception", false, null,
            "Not stated in the book. Investigation's own entry (p.18) gives no Ability pairing."),
        new("medicine", "intellect", false, null,
            "Not stated in the book. Medicine's own entry (p.18) gives no Ability pairing."),
        new("professional", "intellect", false, null,
            "Not stated in the book. Professional's own entry (p.18) gives no Ability pairing."),
        new("science", "intellect", false, null,
            "Not stated in the book. Science's own entry (p.18) gives no Ability pairing."),
        new("streetwise", "perception", false, null,
            "Not stated in the book. Streetwise's own entry (p.19) gives no Ability pairing."),
        new("survival", "might", false, null,
            "Not stated in the book. Survival's own entry (p.19) gives no Ability pairing."),
        new("technology", "intellect", false, null,
            "Not stated in the book. Technology's own entry (p.19) gives no Ability pairing."),
        new("vehicles", "agility", false, null,
            "Not stated in the book. Vehicles' own entry (p.19) gives no Ability pairing."),
    ];
}
