using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Where a reader has to go to fix one finding, and what to call the place.
///
/// <para><b>The GM review step listed findings with no route back to the step that caused
/// them.</b> That was recorded in <c>PROGRESS.md</c> item 2 as a real gap found by an adversarial
/// audit: a reader is told "Might is above the Trait Cap" on the last step of six and left to
/// remember which of the five earlier ones holds Abilities. This closes it.</para>
///
/// <para><b>It is a sibling of <see cref="SheetFindings"/> and lives under the same rule: it reads
/// what the engine already said and computes nothing.</b> The destination comes from
/// <see cref="ValidationIssue.SubjectKind"/>, <see cref="ValidationIssue.SubjectId"/> and
/// <see cref="ValidationIssue.OwnerId"/> — the three fields <c>CharacterValidator</c> fills in so
/// a caller does not have to parse the message back into the facts it was built from. There is no
/// arithmetic here, no rule of its own, and nothing that decides whether a character is legal.</para>
///
/// <para><b>A finding with no honest destination gets no link, and that is the important half.</b>
/// A link is a promise that the place it names is where the fix is; pointing at an arbitrary step
/// to avoid an empty cell would be worse than the gap this closes, because a reader would follow
/// it. <see cref="For"/> answers <c>null</c> rather than guessing — see the note on the budget
/// below.</para>
/// </summary>
public static class FindingRoute
{
    /// <param name="Href">The step's address, without a leading slash, as this app writes them.</param>
    /// <param name="Label">What to call the place, in the reader's words rather than the code's.</param>
    /// <param name="Section">
    /// Which section of the characteristics step to open, or null for a step that has none.
    /// </param>
    /// <param name="PowerId">
    /// A Power whose editor should be opened on arrival, or null.
    ///
    /// <para>This is the one case where the route can land on the exact row rather than the right
    /// step, because <see cref="Commands.RequestPower"/> already exists for the command palette and
    /// does precisely this. Reusing it costs nothing and is the difference between "go to Powers"
    /// and "go to the Power you got wrong".</para>
    /// </param>
    public readonly record struct Destination(string Href, string Label, string? Section, string? PowerId);

    private const string Characteristics = "build/characteristics";

    /// <summary>
    /// The three codes <c>CheckPerks</c> and <c>CheckQuantities</c> file against a Perk.
    ///
    /// <para><see cref="ValidationSubject"/> has no Perk case — a sixth kind for one collection was
    /// not worth adding — so a Perk's findings arrive as <see cref="ValidationSubject.Character"/>
    /// carrying the Perk's id. <b>Matching on the code as well as the kind is what keeps this from
    /// claiming every Character-kind finding that happens to carry an id</b>, since a Pro or Con id
    /// is drawn from a different collection and the type system does not keep the two apart. This
    /// is the same list, and the same reasoning, as <see cref="SheetFindings.ForPerk"/>.</para>
    /// </summary>
    private static readonly string[] PerkCodes =
        ["PER_UNIT_WITHOUT_UNITS", "NEGATIVE_UNITS", "UNKNOWN_PERK"];

    /// <summary>
    /// Where to send a reader for this finding, or <c>null</c> when there is no honest answer.
    ///
    /// <para><paramref name="sheet"/> is read for one thing only: a Pro or Con finding names its
    /// owner in <see cref="ValidationIssue.OwnerId"/> and nothing in the issue says which
    /// collection that owner came from, so the character is asked whether it is a Power, an Ability
    /// or a piece of gear. That is a lookup, not a judgement — and it is why this takes the sheet
    /// while <see cref="SheetFindings"/>, which is already being rendered inside the row that owns
    /// the finding, does not.</para>
    /// </summary>
    public static Destination? For(ValidationIssue issue, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(sheet);

        switch (issue.SubjectKind)
        {
            case ValidationSubject.Tier:
                return new Destination("build", "Tier", null, null);

            case ValidationSubject.Ability:
                return new Destination(Characteristics, "Abilities", "abilities", null);

            case ValidationSubject.Talent:
                return new Destination(Characteristics, "Talents", "talents", null);

            case ValidationSubject.Power:
                return new Destination(Characteristics, "Powers", "powers", issue.SubjectId);

            case ValidationSubject.Flaw:
                return new Destination(Characteristics, "Flaws", "flaws", null);

            case ValidationSubject.Gear:
            case ValidationSubject.GearFeature:
                return new Destination("build/gear", "Gear", null, null);

            // Chapter 6's four, all on the one step that holds them. No section and no target:
            // the page is four panels and a finding about a vehicle is above the fold on it.
            case ValidationSubject.Vehicle:
            case ValidationSubject.Headquarters:
            case ValidationSubject.Gadget:
            case ValidationSubject.AssetFeature:
                return new Destination("build/assets", "Vehicles & bases", null, null);

            case ValidationSubject.Character:
                return ForCharacterSubject(issue, sheet);

            default:
                return null;
        }
    }

    /// <summary>
    /// <see cref="ValidationSubject.Character"/> is the catch-all, so it is unpacked rather than
    /// routed as one thing.
    ///
    /// <para><b>The budget deliberately gets no link, and that is a decision rather than an
    /// omission.</b> <c>HP_BUDGET_EXCEEDED</c> is not a fault in any one place — every purchase on
    /// the character contributes, and the fix is wherever the reader decides to spend less, or the
    /// tier. Sending them to the tier would name one of several answers as though it were the
    /// answer. The same reasoning already keeps it off the rows (see the budget-strip note in
    /// <c>docs/guide/browser.md</c>), and the running total is on screen from every step anyway,
    /// which is the whole reason the strip is chrome rather than a panel.</para>
    /// </summary>
    private static Destination? ForCharacterSubject(ValidationIssue issue, CharacterSheet sheet)
    {
        // Two findings that are unmistakably about one step and carry no subject to say so,
        // because the thing they are about is a *count* or a *choice* rather than a row. Both were
        // found by looking at the rendered page rather than by reading the validator: they sat
        // there with no link beside findings that had one, which reads as the feature half-working.
        if (issue.Code is "FLAW_MIN_NOT_MET" or "FLAW_MAX_EXCEEDED")
            return new Destination(Characteristics, "Flaws", "flaws", null);

        if (issue.Code is "UNKNOWN_PACKAGE")
            return new Destination("build", "Tier", null, null);

        if (issue.SubjectId is not null
            && PerkCodes.Contains(issue.Code, StringComparer.Ordinal))
            return new Destination(Characteristics, "Perks", "perks", null);

        if (issue.OwnerId is not { } owner) return null;

        // A Pro or Con: the owner says which step, and the character says what the owner is.
        if (sheet.SelectedPowers.Any(p => string.Equals(p.PowerId, owner, StringComparison.Ordinal)))
            return new Destination(Characteristics, "Powers", "powers", owner);

        if (sheet.AbilityRanks.ContainsKey(owner))
            return new Destination(Characteristics, "Abilities", "abilities", null);

        if (sheet.Gear.Any(g => string.Equals(g.Name, owner, StringComparison.Ordinal)))
            return new Destination("build/gear", "Gear", null, null);

        // An owner the character no longer carries. Naming a step would be a guess, so it does not.
        return null;
    }
}
