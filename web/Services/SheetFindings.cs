using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Routes a <see cref="ValidationResult"/>'s findings to the row that broke the rule.
///
/// <para><b>The engine decides; this only reads what it already said.</b> Every method here is
/// a filter over <see cref="ValidationIssue.SubjectKind"/>, <see cref="ValidationIssue.SubjectId"/>
/// and <see cref="ValidationIssue.OwnerId"/> — the same three fields <c>CharacterValidator</c>
/// already fills in for exactly this purpose (see <see cref="ValidationIssue"/>'s own doc comment:
/// "so a caller does not have to parse the message back into the facts it was built from"). There
/// is no arithmetic here and no rule of its own — a tab that wants to know whether its own row is
/// legal still gets the answer from <see cref="CharacterSession.Validate()"/>, which asks the
/// engine. This only decides which row a finding the engine already produced belongs on.</para>
/// </summary>
public static class SheetFindings
{
    private static IReadOnlyList<ValidationIssue> BySubject(
        ValidationResult result, ValidationSubject kind, string id) =>
        [.. result.Issues.Where(i => i.SubjectKind == kind && i.SubjectId == id)];

    /// <summary>
    /// Findings against a Pro or Con carried by this owner.
    ///
    /// <para><c>CharacterValidator.CheckModifierList</c> files these at
    /// <see cref="ValidationSubject.Character"/> rather than at the Power, Ability or piece of
    /// gear that carries the Pro or Con — the finding is about the Pro or Con, not about its
    /// owner — and records who carries it in <see cref="ValidationIssue.OwnerId"/> instead,
    /// exactly so a caller can still find the row. <c>PRO_NOT_APPLICABLE</c>,
    /// <c>CON_NOT_APPLICABLE</c>, <c>DUPLICATE_PRO</c>, <c>DUPLICATE_CON</c>, <c>UNKNOWN_PRO</c>,
    /// <c>UNKNOWN_CON</c>, <c>PRO_VARIANT_NOT_CHOSEN</c> and <c>CON_VARIANT_NOT_CHOSEN</c> all
    /// arrive this way.</para>
    /// </summary>
    private static IReadOnlyList<ValidationIssue> ByOwner(ValidationResult result, string ownerId) =>
        [.. result.Issues.Where(i => i.OwnerId == ownerId)];

    /// <summary>An Ability's row: its own findings, plus any Pro or Con findings it carries.</summary>
    public static IReadOnlyList<ValidationIssue> ForAbility(ValidationResult result, string abilityId) =>
        [.. BySubject(result, ValidationSubject.Ability, abilityId), .. ByOwner(result, abilityId)];

    /// <summary>
    /// A Talent's row. Talents carry no Pros or Cons of their own — Ch.2 names Overkill and Weak
    /// against Might alone, and the rulebook attaches no generic option to a Talent — so there is
    /// no <see cref="ByOwner"/> half here.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ForTalent(ValidationResult result, string talentId) =>
        BySubject(result, ValidationSubject.Talent, talentId);

    /// <summary>A Power's row: its own findings, plus any Pro or Con findings it carries.</summary>
    public static IReadOnlyList<ValidationIssue> ForPower(ValidationResult result, string powerId) =>
        [.. BySubject(result, ValidationSubject.Power, powerId), .. ByOwner(result, powerId)];

    public static IReadOnlyList<ValidationIssue> ForFlaw(ValidationResult result, string flawId) =>
        BySubject(result, ValidationSubject.Flaw, flawId);

    /// <summary>A piece of gear's row: its own findings, plus any Pro, Con or feature findings it carries.</summary>
    public static IReadOnlyList<ValidationIssue> ForGear(ValidationResult result, string gearName) =>
        [.. BySubject(result, ValidationSubject.Gear, gearName), .. ByOwner(result, gearName)];

    /// <summary>
    /// A Perk's row.
    ///
    /// <para><b><see cref="ValidationSubject"/> has no Perk case.</b> <c>CheckPerks</c> and the
    /// per-unit and negative-quantity checks in <c>CheckQuantities</c> file a perk's findings at
    /// <see cref="ValidationSubject.Character"/> with the perk's own id as
    /// <see cref="ValidationIssue.SubjectId"/> — a sixth subject kind for one collection was not
    /// worth adding to the enum. Restricted to the three codes that actually do this, rather than
    /// every Character-kind finding that happens to carry a matching id, since a Pro or Con id is
    /// drawn from a different collection and the type system does not keep the two apart.</para>
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ForPerk(ValidationResult result, string perkId) =>
        [.. result.Issues.Where(i =>
            i.SubjectKind == ValidationSubject.Character
            && i.SubjectId == perkId
            && i.Code is "PER_UNIT_WITHOUT_UNITS" or "NEGATIVE_UNITS" or "UNKNOWN_PERK")];
}
