using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>What happened when a character was put into a campaign.</summary>
public enum CampaignJoinOutcome
{
    /// <summary>
    /// The character's tier was empty, so the campaign's settings were copied into it and the
    /// character now names the campaign.
    ///
    /// <para><b>Which settings, exactly, is on <see cref="CampaignJoinResult"/> and not derivable
    /// from here</b> — a campaign that names no tier gives none, and a character that already had
    /// a house cap keeps it even in this branch.</para>
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

/// <summary>
/// What a join did, and what it actually copied.
///
/// <para><b>The two flags are here because the outcome alone could not carry the sentence.</b>
/// <see cref="CampaignJoinOutcome.Inherited"/> and <see cref="CampaignJoinOutcome.Joined"/> both
/// copy <em>some</em> subset of a campaign's settings — the tier only where the character had
/// none, the cap only where the character had none, a campaign that sets neither copies nothing at
/// all — and a screen reading the outcome alone had to guess. It guessed wrong: "Its tier and its
/// Trait Cap are now yours" was printed over a join that took the tier and left a cap the
/// character already had, which is the one thing this class most carefully does not do. A message
/// that claims a change nobody made is worse than no message, because it teaches a reader to
/// distrust the ones that are true.</para>
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="TookTier">
/// Whether the campaign's tier was written onto the character. False where the character already
/// had one, and false where the campaign names none — a GM who has not set a power level is not
/// giving anybody one.
/// </param>
/// <param name="TookTraitCap">
/// Whether the campaign's house Trait Cap was written onto the character. False where the
/// character already had a cap of its own, and false where the campaign has set none. <b>This is
/// the flag that changes a figure</b>: Resolve is measured from the cap, so a join that took one
/// moved it.
/// </param>
/// <param name="TookHouseRules">
/// Whether the campaign's optional rules or its price for Immortality were written onto the
/// character. False where the character already carried either, and false where the campaign has
/// set neither.
///
/// <para><b>One flag for two fields, unlike the two above, and the reason is what a sentence can
/// carry.</b> The tier and the cap each move a figure a player can point at — a budget, a Resolve
/// — so each gets named. The table's rules are a list a reader goes and looks at on the campaign
/// page, and a join sentence enumerating thirteen switches is not a sentence anybody reads. What
/// the message says is that the game's rules came with it; the page says which.</para>
/// </param>
public readonly record struct CampaignJoinResult(
    CampaignJoinOutcome Outcome, bool TookTier = false, bool TookTraitCap = false,
    bool TookHouseRules = false);

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
/// <param name="CharacterImmortalityCost">
/// The price this character is charged for Immortality, where the finding is about that price.
/// Carried rather than written into the sentence for the reason the ranks and the ids are: a
/// screen renders the pair, and the message stays true without them.
/// </param>
/// <param name="CampaignImmortalityCost">
/// The price the campaign charges, where the finding is about the price.
/// </param>
public sealed record CampaignFinding(
    string Code, string Message, string? CharacterTierId = null, string? CampaignTierId = null,
    int? CharacterTraitCapRank = null, int? CampaignTraitCapRank = null,
    int? CharacterImmortalityCost = null, int? CampaignImmortalityCost = null);

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
    /// The one finding code a caller has to be able to name, because it is the one finding whose
    /// truth depends on who is reading it.
    ///
    /// <para>Every other finding here is computed from a campaign that resolved, so producing one
    /// at all means the reader's account owns the game. This one is produced from a campaign that
    /// did <em>not</em> resolve — which for a GM means it is gone, and for a player means only that
    /// a campaign's payload is scoped to the account that owns it, which is true of every live
    /// game they are in. <c>Campaigns.razor</c> holds the membership list that tells those two
    /// apart; see its <c>WorthSaying</c>.</para>
    /// </summary>
    public const string UnknownCampaign = "UNKNOWN_CAMPAIGN";

    /// <summary>
    /// The character joined before the table decided anything, so it carries no copy of the rules
    /// the game now has.
    ///
    /// <para>Named here because it is the one finding a screen has to be able to ask for
    /// <em>before</em> it has anything to compare: a member's browser cannot resolve their own
    /// campaign, so <c>Campaigns.razor</c> hands <see cref="Inspect"/> the live table it read
    /// through the membership instead. See that page's <c>ResolveCampaign</c>.</para>
    /// </summary>
    public const string HouseRulesNotCopied = "CAMPAIGN_HOUSE_RULES_NOT_COPIED";

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
    /// <returns>
    /// What happened, <b>and which settings were really copied</b> — see
    /// <see cref="CampaignJoinResult"/> for why the outcome alone was not enough to write a true
    /// sentence with.
    /// </returns>
    public static CampaignJoinResult Apply(CharacterSheet sheet, Campaign? campaign)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (campaign is null) return new(CampaignJoinOutcome.CampaignIsNotHere);

        // An empty tier is the inheriting case, and it is the only one that copies a setting. The
        // sandbox toggle travels with the tier rather than on its own: they are one statement
        // about how this table plays, and splitting them would let a character inherit half a
        // campaign.
        if (sheet.SelectedTierId is null)
        {
            // Read before the write, both of them: what was copied is what the character did not
            // already have, and after the assignment there is no way to tell.
            var takesTier = campaign.TierId is not null;
            var takesCap = sheet.TraitCapRank is null && campaign.TraitCapRank is not null;
            var takesHouseRules = TakesHouseRules(sheet, campaign);

            sheet.CampaignId = campaign.Id;
            sheet.SelectedTierId = campaign.TierId;
            sheet.UnlimitedBudget = campaign.UnlimitedBudget;
            sheet.TraitCapRank ??= campaign.TraitCapRank;
            CopyHouseRules(sheet, campaign);

            return new(CampaignJoinOutcome.Inherited, takesTier, takesCap, takesHouseRules);
        }

        // A campaign that names no tier has nothing to disagree with — a GM who has not set a
        // power level is not overruling anybody.
        if (campaign.TierId is not null
            && !string.Equals(campaign.TierId, sheet.SelectedTierId, StringComparison.Ordinal))
        {
            return new(CampaignJoinOutcome.TierDisagrees);
        }

        sheet.CampaignId = campaign.Id;

        // <b>An empty cap is filled even where the tier was not empty.</b> `??=` is the whole of
        // it: a character that has already been built to a house cap keeps it, and the
        // disagreement is Inspect's to report. Writing over one would move Resolve on somebody's
        // finished character in the course of typing a join code.
        //
        // Which is exactly why the answer says whether it happened. `??=` is silent by
        // construction, and a screen that assumed it had fired told somebody their Resolve had
        // moved when it had not.
        var tookCap = sheet.TraitCapRank is null && campaign.TraitCapRank is not null;
        var tookHouseRules = TakesHouseRules(sheet, campaign);

        sheet.TraitCapRank ??= campaign.TraitCapRank;
        CopyHouseRules(sheet, campaign);

        return new(CampaignJoinOutcome.Joined, TookTraitCap: tookCap,
                   TookHouseRules: tookHouseRules);
    }

    /// <summary>
    /// Whether this join is about to write the campaign's table rules or its Immortality price
    /// onto a character that has neither.
    ///
    /// <para><b>Asked before the write, because <c>??=</c> is silent by construction</b> — the
    /// same reason the cap's flag is read first, and the same fault: a screen that assumed the
    /// assignment had fired told somebody their game's rules were now theirs when they had kept
    /// their own.</para>
    /// </summary>
    private static bool TakesHouseRules(CharacterSheet sheet, Campaign campaign) =>
        (sheet.CampaignTable is null && campaign.Table is not null)
        || (sheet.ImmortalityCost is null && campaign.ImmortalityCost is not null);

    /// <summary>
    /// The campaign's optional rules and its price for Immortality, into empty fields only.
    ///
    /// <para><b>The same rule as the cap, one field at a time.</b> A character that already
    /// carries a table's rules keeps them and the disagreement is <see cref="Inspect"/>'s to
    /// report. Writing over them would be worse here than for the cap in one respect and better in
    /// another: the price moves a spend the player may have budgeted around, and the switches move
    /// nothing at all until somebody fights with the sheet — but both are somebody's answer to
    /// "which game is this character from", and overwriting an answer in the course of typing a
    /// join code is the thing this whole class exists not to do.</para>
    /// </summary>
    private static void CopyHouseRules(CharacterSheet sheet, Campaign campaign)
    {
        sheet.CampaignTable ??= campaign.Table;
        sheet.ImmortalityCost ??= campaign.ImmortalityCost;
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
    ///   <item><c>IMMORTALITY_COST_WITHOUT_CAMPAIGN</c> — the character carries a table's price
    ///     for Immortality and belongs to no game at all. <b>It is the one finding asked before
    ///     the "in no campaign" exit</b>, because it is about exactly that state; every other one
    ///     here is about a character and its game disagreeing. It lives here rather than in
    ///     <c>CharacterValidator</c> because saying it means reading the campaign id, which no
    ///     rules code may do.</item>
    ///   <item><c>CAMPAIGN_IMMORTALITY_COST_MISMATCH</c> — both have set a price and they differ.
    ///     A price is Hero Points, so this is a character whose spend was counted against a figure
    ///     its table did not set.</item>
    ///   <item><c>CAMPAIGN_HOUSE_RULES_NOT_COPIED</c> — the game has set a price or turned a rule
    ///     on and the character carries neither, because it joined before the GM decided. <b>The
    ///     other direction of the two checks above</b>, both of which need each side to have set
    ///     something and so say nothing at all about this one. Reported before the mismatch below
    ///     because it can move a figure: a character costed at the book's 3 for Immortality at a
    ///     table charging 12.</item>
    ///   <item><c>CAMPAIGN_TABLE_MISMATCH</c> — both carry optional rules and they are not the
    ///     same ones. <b>Last, because it is the one finding that moves no figure</b>: the
    ///     switches decide what happens in a fight and nothing about cost or legality.</item>
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

        // **Asked before the "no campaign" exit, and it is the only finding that can be.** A house
        // price on a character that belongs to no game is exactly the state that exit describes,
        // so a check after it could never fire. It is here rather than in `CharacterValidator`
        // because saying it means reading the campaign id, which no rules code may do — see that
        // class's `CheckHouseImmortalityCost` for the line.
        if (sheet.CampaignId is null)
        {
            return sheet.ImmortalityCost is { } orphaned
                ? new CampaignFinding("IMMORTALITY_COST_WITHOUT_CAMPAIGN",
                    "This character is charged a house price for Immortality and belongs to no "
                    + "game. A house price is a table's, so there is nobody whose rule this is — "
                    + "join the game it came from, or set the price back to the rulebook's. "
                    + "Nothing has been changed either way.",
                    CharacterImmortalityCost: orphaned)
                : null;
        }

        if (campaign is null)
        {
            return new CampaignFinding(UnknownCampaign,
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

        // Both set and different, exactly as the cap above. The character's is the one that is
        // charged — a price is Hero Points, so this is a character whose spend was worked out
        // against a figure its table did not set.
        if (campaign.ImmortalityCost is { } theirPrice && sheet.ImmortalityCost is { } ourPrice
            && ourPrice != theirPrice)
        {
            return new CampaignFinding("CAMPAIGN_IMMORTALITY_COST_MISMATCH",
                "This character is charged a different price for Immortality from the game it "
                + "belongs to. The character's own is what its Hero Points were counted against. "
                + "Nothing has been changed either way.",
                CharacterImmortalityCost: ourPrice, CampaignImmortalityCost: theirPrice);
        }

        // **The other direction of the two checks above, and it was silent for a whole slice.**
        // Both of those need the campaign *and* the character to have set something, so a
        // character that joined before the GM decided anything produces neither: the sheet carries
        // nothing, the table charges 12, and the engine goes on pricing Immortality at the book's
        // 3 with no panel saying so. That is item 30, and it is a real report rather than a
        // bookkeeping difference — a price is Hero Points.
        //
        // **Reported and never repaired, which is this class's whole rule.** Copying the table's
        // rules in now would move somebody's spend while they were reading a list, and it would do
        // it behind the back of the one guarantee joining makes: a write into an empty field
        // happens at the join and nowhere else. So the remedy in the sentence is joining again,
        // which fires the same `??=` into the same still-empty field.
        //
        // **Before the mismatch below because it can move a figure and that one cannot.** The
        // price is the half that matters; the switch block is carried along with it because a
        // character that took neither took neither, and two findings for one join is two sentences
        // saying the same thing.
        if ((campaign.ImmortalityCost is not null && sheet.ImmortalityCost is null)
            || (campaign.Table is { IsTheBook: false } && sheet.CampaignTable is null))
        {
            return new CampaignFinding(HouseRulesNotCopied,
                "This character joined before the game set its house rules, so it carries none of "
                + "them and is costed by the book. Join again to take the table's rules as they "
                + "stand. Nothing has been changed either way.",
                CampaignImmortalityCost: campaign.ImmortalityCost);
        }

        // Last, because it is the finding that changes no figure: the optional rules decide what
        // happens in a fight and nothing about what the character costs or whether it is legal. A
        // character at the wrong power level, or paying the wrong price, has a bigger problem, and
        // only one finding comes back.
        if (campaign.Table is not null && sheet.CampaignTable is not null
            && sheet.CampaignTable != campaign.Table)
        {
            return new CampaignFinding("CAMPAIGN_TABLE_MISMATCH",
                "This character carries a different set of optional rules from the game it "
                + "belongs to. They decide what happens when the sheet is used in a fight, and "
                + "the character's own are what travel with it. Nothing has been changed either "
                + "way.");
        }

        return null;
    }
}
