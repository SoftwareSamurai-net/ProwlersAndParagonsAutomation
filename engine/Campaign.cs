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
/// an empty field and reports a disagreement otherwise — <c>web/Services/CampaignJoin.cs</c> — and
/// <see cref="TraitCapRank"/> in particular is reported and never applied in this slice, so a
/// campaign cannot silently move a character's Resolve.</para>
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
/// <para><b>Reported, never enforced.</b> Applying it would move
/// <see cref="DerivedStatsCalculator.CalculateResolve"/>, which is
/// <c>(TraitCap − highestRelevantRank) × 2</c> — so a campaign cap could silently change a figure
/// the player paid Hero Points for. Deferred deliberately; there is a test that a campaign cap
/// leaves Resolve exactly where no campaign at all leaves it.</para>
/// </param>
/// <param name="UnlimitedBudget">
/// Whether this table builds without a Hero Point limit — the sandbox, one level up from
/// <see cref="CharacterSheet.UnlimitedBudget"/>, which is where it has been living.
/// </param>
public sealed record Campaign(
    string Id,
    string Name,
    string? TierId,
    int? TraitCapRank,
    bool UnlimitedBudget);
