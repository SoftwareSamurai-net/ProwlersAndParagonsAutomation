using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What happened when a character was put into a campaign.</summary>
public enum CampaignJoinOutcome
{
    /// <summary>
    /// The character's tier was empty, so the campaign's settings were copied into it and the
    /// character now names the campaign.
    /// </summary>
    Inherited,

    /// <summary>
    /// The character already agreed with the campaign — same tier, or a campaign that names no
    /// tier — so it now names the campaign and nothing else moved.
    /// </summary>
    Joined,

    /// <summary>
    /// The character's tier and the campaign's tier disagree, so <b>nothing was written at
    /// all</b>. See <see cref="CampaignJoin"/>'s remarks: this is the case where repairing is
    /// worse than reporting.
    /// </summary>
    TierDisagrees,

    /// <summary>There is no such campaign here, so there was nothing to join.</summary>
    CampaignIsNotHere,
}

/// <summary>One thing a host can say about a character's campaign. A report, never a repair.</summary>
/// <param name="Code">
/// A stable code in the same spelling <c>CharacterValidator</c> uses — <c>UNKNOWN_CAMPAIGN</c>
/// deliberately reads like <c>UNKNOWN_TIER</c>, because it is the same situation one level up.
/// </param>
/// <param name="Message">What to put in front of a person.</param>
/// <param name="CharacterTierId">The tier the character is built to, where the finding is about one.</param>
/// <param name="CampaignTierId">The tier the campaign is played at, where the finding is about one.</param>
/// <remarks>
/// <b>The two ids are carried rather than written into the sentence</b>, because an id is not what
/// a tier is called: the rulebook prints names, and every other finding in this application names
/// a Trait the way the book does rather than by its key. A screen that renders this looks both up
/// and says them; the message stays true without them.
/// </remarks>
public sealed record CampaignFinding(
    string Code, string Message, string? CharacterTierId = null, string? CampaignTierId = null);

/// <summary>
/// Putting a character into a campaign: <b>inherit into an empty field, offer into a full
/// one.</b>
///
/// <para><b>This is in <c>web/</c> and not in <c>CharacterValidator</c>, and the line is worth
/// stating.</b> The validator tallies: it prices Powers, counts Flaws, compares ranks against a
/// cap. This compares two ids and names them. It also needs a campaign, and resolving a campaign
/// id means asking storage — which is exactly what <c>CharacterSheet.CampaignId</c>'s bar exists
/// to keep out of the engine. It sits beside <c>CharacterSession.TraitCap</c> and
/// <c>CharacterSession.Budget</c>, which are the other two places where a host reads a tier's
/// figures for the screen.</para>
///
/// <para><b>Why a mismatch writes nothing at all.</b> An illegal character is reported and never
/// repaired, and here repair would be worse than usual in both directions. Raising a character's
/// tier to the campaign's turns an illegal character legal in silence — the very finding somebody
/// needs to see disappears as a side effect of joining a game. Lowering it silently changes
/// Resolve, because <c>DerivedStatsCalculator.CalculateResolve</c> is
/// <c>(TraitCap − highestRelevantRank) × 2</c>: a figure the player spent Hero Points on moves
/// while nobody is looking at it. So the disagreement is handed back for somebody to decide
/// about.</para>
///
/// <para><b>And the trait cap is reported, never enforced, in this slice at all.</b>
/// <see cref="Campaign.TraitCapRank"/> is carried, listed and shown; nothing applies it. There is
/// a test that a campaign whose cap differs from its tier's leaves <c>CalculateResolve</c>
/// returning exactly what it returns with no campaign in the picture.</para>
/// </summary>
public static class CampaignJoin
{
    /// <summary>
    /// Put a character into a campaign, or report why it is not being put into one.
    ///
    /// <para>The only case that writes anything is the one where the character has no tier yet
    /// (<see cref="CampaignJoinOutcome.Inherited"/>) or already agrees
    /// (<see cref="CampaignJoinOutcome.Joined"/>). Everything else leaves the sheet exactly as it
    /// was found, including its existing campaign — a failed join is not a reason to take a
    /// character out of the game it is already in.</para>
    /// </summary>
    /// <param name="sheet">The character. Mutated only in the two cases above.</param>
    /// <param name="campaign">
    /// The campaign, already resolved by a store, or null when the id named no campaign this
    /// browser or account holds.
    /// </param>
    public static CampaignJoinOutcome Apply(CharacterSheet sheet, Campaign? campaign)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (campaign is null) return CampaignJoinOutcome.CampaignIsNotHere;

        // An empty tier is the inheriting case, and it is the only one that copies a setting. The
        // sandbox toggle travels with the tier rather than on its own: they are one statement
        // about how this table plays, and splitting them would let a character inherit half a
        // campaign.
        if (sheet.SelectedTierId is null)
        {
            sheet.CampaignId = campaign.Id;
            sheet.SelectedTierId = campaign.TierId;
            sheet.UnlimitedBudget = campaign.UnlimitedBudget;

            return CampaignJoinOutcome.Inherited;
        }

        // A campaign that names no tier has nothing to disagree with — a GM who has not set a
        // power level is not overruling anybody.
        if (campaign.TierId is not null
            && !string.Equals(campaign.TierId, sheet.SelectedTierId, StringComparison.Ordinal))
        {
            return CampaignJoinOutcome.TierDisagrees;
        }

        sheet.CampaignId = campaign.Id;

        return CampaignJoinOutcome.Joined;
    }

    /// <summary>
    /// What is worth saying about the campaign this character names, without changing anything.
    ///
    /// <para>Two findings, and both are the same shape as the tier findings the engine already
    /// produces:</para>
    /// <list type="bullet">
    ///   <item><c>UNKNOWN_CAMPAIGN</c> — the character names a campaign that is not here. It is
    ///     <em>not</em> repaired by clearing the id: deleting a campaign leaves its members
    ///     saying so on purpose, so that signing in on the browser that still has it, or
    ///     restoring it, puts everything back.</item>
    ///   <item><c>CAMPAIGN_TIER_MISMATCH</c> — the character and its campaign disagree about the
    ///     power level. Reported for as long as it is true, so a disagreement that arrived by the
    ///     GM changing the campaign is as visible as one that arrived by a failed join.</item>
    /// </list>
    /// </summary>
    /// <param name="sheet">The character.</param>
    /// <param name="campaign">
    /// The campaign its id resolved to, or null for one that resolved to nothing. A character in
    /// no campaign at all — a null id — has nothing to report and gets nothing.
    /// </param>
    public static CampaignFinding? Inspect(CharacterSheet sheet, Campaign? campaign)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.CampaignId is null) return null;

        if (campaign is null)
        {
            return new CampaignFinding("UNKNOWN_CAMPAIGN",
                "This character names a campaign that is not here. Nothing about the character "
                + "has changed — the campaign may be on another browser, or may have been "
                + "deleted.");
        }

        if (campaign.TierId is null || sheet.SelectedTierId is null
            || string.Equals(campaign.TierId, sheet.SelectedTierId, StringComparison.Ordinal))
        {
            return null;
        }

        return new CampaignFinding("CAMPAIGN_TIER_MISMATCH",
            "This character is built to a different tier from the campaign it belongs to. "
            + "Nothing has been changed either way.",
            sheet.SelectedTierId, campaign.TierId);
    }
}
