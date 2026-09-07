namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// A game somebody is running: a name, the power level everyone at the table is built to, and
/// the way the table works.
///
/// <para><b>Data, and nothing else.</b> There is no method here, no reader beside it, and
/// nothing in <c>engine/</c> that turns a <see cref="CharacterSheet.CampaignId"/> back into one
/// of these. That is deliberate rather than unfinished: resolving an id would mean storage, and
/// storage in a browser is asynchronous, and <see cref="IRulesSource"/> is synchronous precisely
/// so that <see cref="CostCalculator"/> and <see cref="CharacterValidator"/> stay pure and
/// instantly callable. <c>AccountsContractTests.TheEngineHasNoFilesystemAccess</c> and
/// <c>TheEngineHasNoNetwork</c> already ban the two ways such a reader could be written.</para>
///
/// <para><b>So why is the type here at all?</b> For the same reason
/// <see cref="CharacterSheet.IsVillain"/> is: it has to survive being written down and read
/// back, by a browser and by an account's server alike, and a shape defined in one host is a
/// shape the other host copies. It is a record the hosts share, not a rule the engine reads.</para>
///
/// <para><b>Why the settings are here rather than on the character.</b> The tier sets the Hero
/// Point budget and the Trait Cap, which are facts about the game rather than about one Hero —
/// five characters in one campaign silently disagreeing about the power level is the defect
/// <c>PROGRESS.md</c> item 11 records. This is where they belong; the character still carries its
/// own copy, because a character is a portable thing that has to price itself with no campaign in
/// front of it.</para>
///
/// <para><b>Nothing here is enforced on a character.</b> Joining a campaign copies settings into
/// an empty field and reports a disagreement otherwise — <c>web/Services/CampaignJoin.cs</c>. That
/// is as true of <see cref="TraitCapRank"/> as of the tier: it is copied into a character that has
/// no cap of its own, and a character that already has one keeps it and the disagreement is handed
/// back. What the character then does with its own copy is the character's business, and it is a
/// great deal — see the field.</para>
/// </summary>
/// <param name="Id">
/// <c>g_</c> followed by 22 URL-safe characters, mirroring the <c>c_</c> shape a character id
/// uses — minted client-side, validated by the server as a well-formed key and nothing more.
/// </param>
/// <param name="Name">What the table calls the game. Opaque: no rule reads it.</param>
/// <param name="TierId">
/// The tier every character in this campaign is built to, or null if the GM has not said.
/// </param>
/// <param name="TraitCapRank">
/// A Trait Cap the GM has set for this table, or null for "whatever the tier says".
///
/// <para><b>It reaches a character by being copied onto it, and never by being read from here.</b>
/// Joining copies this into <see cref="CharacterSheet.TraitCapRank"/> when that is empty, and from
/// then on the character's own field is the only thing anything reads — which is what keeps a
/// character portable and keeps the engine out of storage.</para>
///
/// <para><b>And it moves Resolve, which is the point rather than the hazard.</b>
/// <see cref="DerivedStatsCalculator.CalculateResolve"/> is
/// <c>(TraitCap − highestRelevantRank) × 2</c>, so the cap <em>is</em> the datum Resolve is
/// measured from; a cap that only gated validation would pay a character for room the table has
/// taken away from them. The owner settled that on 2026-09-05, reversing the deferral this
/// paragraph used to record. It still cannot happen behind anybody's back: nothing here is applied
/// to a character that has a cap of its own.</para>
/// </param>
/// <param name="UnlimitedBudget">
/// Whether this table builds without a Hero Point limit — the sandbox, one level up from
/// <see cref="CharacterSheet.UnlimitedBudget"/>, which is where it has been living.
/// </param>
/// <param name="Table">
/// The optional rules this table has turned on, or null for the book as printed.
///
/// <para><b>It reaches a character the way the Trait Cap does, and for the same reason.</b>
/// Joining copies it onto <see cref="CharacterSheet.CampaignTable"/>; nothing here resolves a
/// campaign to read it. What is different is that <em>this</em> engine never reads it at all —
/// see <see cref="CampaignTable"/>: these are rules for resolving a fight, and a fight is the
/// second engine's business. The block rides on the sheet so that the encounter server, handed a
/// character and no campaign, can still be told which game the character came from.</para>
///
/// <para><b>Null and "every switch off" are the same game</b>, which is what lets a campaign
/// stored before this existed read back unchanged rather than as a table that has opted out of
/// something.</para>
/// </param>
/// <param name="ImmortalityCost">
/// What this table charges for Immortality, or null for the 3 Hero Points the book prices it at.
///
/// <para><b>The one number in the data where the book prints a range instead of a price.</b> Ch.2
/// p.31: "In a game where Heroes can die, GMs should charge more for this — somewhere between 6
/// and 12 Hero Points." Both ends are <c>campaign_cost_min</c> and <c>campaign_cost_max</c> on the
/// Power's own entry, so the bound is a fact in <c>data/rules/</c> rather than a figure written
/// into a calculator.</para>
///
/// <para><b>Unlike the table block, this one <em>is</em> read by this engine</b> — through the
/// character, never from here. It is a price, and pricing is the whole of what
/// <see cref="CostCalculator"/> is for: a table charging 9 changes what every character in it
/// costs and so whether each fits its budget. It is copied onto
/// <see cref="CharacterSheet.ImmortalityCost"/> on joining and read from there, which is the same
/// route the cap takes and for the same reason — a character is portable and has to price itself
/// with no campaign in front of it.</para>
/// </param>
public sealed record Campaign(
    string Id,
    string Name,
    string? TierId,
    int? TraitCapRank,
    bool UnlimitedBudget,
    CampaignTable? Table = null,
    int? ImmortalityCost = null);
