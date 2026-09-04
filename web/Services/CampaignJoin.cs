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
    /// tier — so it now names the campaign. <b>A house Trait Cap it did not have may still have
    /// been copied in</b>, which is inheriting into an empty field one field at a time; a cap it
    /// already had is left alone and the disagreement is reported by <see cref="CampaignJoin.Inspect"/>.
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
/// <param name="CharacterTraitCapRank">
/// The Trait Cap the character is built to, where the finding is about the cap.
/// </param>
/// <param name="CampaignTraitCapRank">
/// The Trait Cap the campaign has set, where the finding is about the cap. Carried rather than
/// written into the sentence for the reason the tier ids are: a screen renders the pair, and the
/// message stays true without them.
/// </param>
/// <remarks>
/// <b>The two ids are carried rather than written into the sentence</b>, because an id is not what
/// a tier is called: the rulebook prints names, and every other finding in this application names
/// a Trait the way the book does rather than by its key. A screen that renders this looks both up
/// and says them; the message stays true without them.
/// </remarks>
public sealed record CampaignFinding(
    string Code, string Message, string? CharacterTierId = null, string? CampaignTierId = null,
    int? CharacterTraitCapRank = null, int? CampaignTraitCapRank = null);

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
/// <para><b>The Trait Cap follows the same rule as the tier, one field at a time.</b> A campaign's
/// cap is copied into <see cref="CharacterSheet.TraitCapRank"/> when the character has none, and a
/// character that already has one keeps it and the disagreement is handed back. That is a real
/// change to what the character is: the cap <em>substitutes</em> for the tier's, so it moves
/// Resolve — see <c>docs/guide/rules-engine.md</c> for the owner's answer and the arithmetic.
/// Which is exactly why it is never written over a cap somebody already set.</para>
///
/// <para><b>The cap is inherited even where the tier is not.</b> The tier is not copied into a
/// character that already has one, because raising or lowering it is the repair this class exists
/// not to make; the cap is copied into a character that has <em>none</em>, which is not a repair
/// but the empty field being filled — the same thing that happens to the tier when the tier is
/// empty. A cap mismatch does not block the join either, and the tier mismatch does: a character
/// at the wrong power level is at the wrong table, and one whose table caps tighter than it does
/// is a character with a finding on it.</para>
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
            sheet.TraitCapRank ??= campaign.TraitCapRank;

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

        // <b>An empty cap is filled even where the tier was not empty.</b> `??=` is the whole of
        // it: a character that has already been built to a house cap keeps it, and the
        // disagreement is Inspect's to report. Writing over one would move Resolve on somebody's
        // finished character in the course of typing a join code.
        sheet.TraitCapRank ??= campaign.TraitCapRank;

        return CampaignJoinOutcome.Joined;
    }

    /// <summary>
    /// What is worth saying about the campaign this character names, without changing anything.
    ///
    /// <para><b>It is drawn on the campaigns page, at the head of "Games you are in", and that is
    /// the only place.</b> This is worth stating because it was true of nothing for a whole slice:
    /// the findings below were computed, tested and shown to nobody, which is the fault this
    /// repository keeps hitting — a feature that works and no reader can reach. The page resolves
    /// the character's campaign once per campaign id and asks this on every render, so a
    /// disagreement that arrived by the GM retiering the campaign shows up as readily as one that
    /// arrived by a refused join. A second surface would be a second thing to keep in step; if one
    /// is ever added, say so here.</para>
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
    ///   <item><c>CAMPAIGN_TRAIT_CAP_MISMATCH</c> — both have set a house Trait Cap and they are
    ///     not the same one. The character's is what everything computes from, so this is the
    ///     character being judged and paid against a ceiling its table did not set. Reported
    ///     after the tier, because a character at the wrong power level has a bigger problem than
    ///     a cap and only one finding comes back.</item>
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

        if (campaign.TierId is not null && sheet.SelectedTierId is not null
            && !string.Equals(campaign.TierId, sheet.SelectedTierId, StringComparison.Ordinal))
        {
            return new CampaignFinding("CAMPAIGN_TIER_MISMATCH",
                "This character is built to a different tier from the campaign it belongs to. "
                + "Nothing has been changed either way.",
                sheet.SelectedTierId, campaign.TierId);
        }

        // Both set and different. A campaign that has set no cap is not overruling anybody, and a
        // character with none has already inherited the campaign's — so the only case left is two
        // deliberate answers that disagree, and the character's is the one in force.
        if (campaign.TraitCapRank is { } theirs && sheet.TraitCapRank is { } ours && ours != theirs)
        {
            return new CampaignFinding("CAMPAIGN_TRAIT_CAP_MISMATCH",
                "This character is built to a different Trait Cap from the campaign it belongs "
                + "to. The character's own is what its ranks are checked against and what its "
                + "Resolve is worked out from. Nothing has been changed either way.",
                CharacterTraitCapRank: ours, CampaignTraitCapRank: theirs);
        }

        return null;
    }
}
