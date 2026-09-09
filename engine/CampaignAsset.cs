using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// A vehicle or headquarters the <em>campaign</em> owns, paid for by the Hero Points its members
/// put in (Ch.6 p.96 and p.100, which both let Heroes pool their allowances).
///
/// <para><b>This is the other half of <see cref="CampaignAssetContribution"/>, and the split is
/// the owner's answer to the pooling question.</b> A <see cref="CharacterSheet"/> is one
/// character, so a pooled machine recorded on a sheet is either unrepresentable or copied across
/// five sheets with five chances to disagree. What a character records is what it put in; what the
/// object came out as is recorded once, here, and its budget is the sum of every contribution
/// naming this <see cref="Id"/>. <b>Nothing in <c>engine/</c> ever joins the two</b> — that means
/// turning a campaign's id into a campaign, which is storage, and storage in a browser is
/// asynchronous while <see cref="IRulesSource"/> is synchronous precisely to forbid it. So the
/// collecting is a host's job and only the arithmetic is
/// <see cref="CostCalculator.CampaignAssetBudget"/>'s.</para>
///
/// <para><b>There is no Perk on this record, and its absence is the whole difference from
/// <see cref="OwnedVehicle"/>.</b> A machine one character owns is bought with that character's
/// Unique Vehicle Perk and its budget is <em>that</em> Perk's Hero Points; a shared one is bought
/// by everybody who put in, and what each of them spent is on their own sheet, where their own
/// budget charges it — <see cref="CostCalculator.TotalAssetPerkCost"/> already does. A
/// <c>PerkHeroPoints</c> here would be a sixth copy of a figure five sheets hold, and the one copy
/// no budget charges.</para>
///
/// <para><b>Everything else is deliberately the same shape as the sheet's two records</b>, down to
/// the property names — <see cref="Body"/>, <see cref="Speed"/>, <see cref="Control"/>,
/// <see cref="Weapons"/> and a list of <see cref="SelectedAssetFeature"/> — because both are
/// priced from one set of printed rates and a second spelling is a second thing to correct when
/// <c>vehicles.json</c> moves. <c>CampaignAssetShapeTests</c> holds the two name sets
/// together.</para>
/// </summary>
/// <param name="Id">
/// The campaign's own id for the object, and the key a contribution names. Stable, which is
/// exactly why a contribution does not key on the name: <see cref="Name"/> is what a reader sees
/// and is re-typed the first time somebody dislikes it.
/// </param>
/// <param name="Kind">
/// <see cref="CampaignAssetContribution.Vehicle"/> or
/// <see cref="CampaignAssetContribution.Headquarters"/>, from those same two constants — a
/// contribution and the object it names have to agree about which currency the pooled Hero Points
/// buy, and two spellings of that agreement is one too many. A string rather than an enum for the
/// reason the contribution's own field is one: this travels through JSON to a server that never
/// parses it, and a serializer writing an enum as <c>0</c> would make the payload depend on a
/// declaration order.
/// </param>
/// <param name="Name">What the table calls it. Opaque: no rule reads it.</param>
public sealed record CampaignAsset(string Id, string Kind, string Name)
{
    /// <summary>
    /// Body, at one Vehicle Point a rank: durability, armour and health in one figure. Nothing on
    /// a headquarters, which has no characteristics at all — see <see cref="IsHeadquarters"/>.
    /// </summary>
    public int Body { get; init; }

    /// <summary>Speed, at one Vehicle Point a rank. Nothing on a headquarters.</summary>
    public int Speed { get; init; }

    /// <summary>
    /// Control, at two Vehicle Points a rank — a modifier rather than a rank in its own right.
    /// Negative is legal and pays two points back a rank, exactly as on a machine one character
    /// owns. Nothing on a headquarters.
    /// </summary>
    public int Control { get; init; }

    /// <summary>
    /// Weapons, at one Vehicle Point a rank, or null for an object with none. Null rather than
    /// zero because the printed tables print an em dash: an unarmed vehicle has no rank at all,
    /// which is not the same claim as a rank of nothing.
    /// </summary>
    public int? Weapons { get; init; }

    /// <summary>
    /// The features bought — off p.96–100's table of twenty-three for a vehicle, and
    /// pp.100–103's twenty-two for a base. <see cref="Kind"/> says which table, the same way a
    /// sheet says it by which of its two collections a machine is stored in.
    /// </summary>
    public IReadOnlyList<SelectedAssetFeature> Features { get; init; } = [];

    /// <summary>
    /// Whether this is a headquarters, which decides both the currency and the feature table.
    ///
    /// <para><b><see cref="JsonIgnoreAttribute"/> because it is a reading of
    /// <see cref="Kind"/> rather than a field</b>, and a payload carrying both would be a payload
    /// that can contradict itself. A kind that is neither spelling reads as a vehicle here and is
    /// reported by the host that draws it — the same direction
    /// <c>CharacterValidator.CheckCampaignAssets</c> takes with a contribution's own kind.</para>
    /// </summary>
    [JsonIgnore]
    public bool IsHeadquarters =>
        string.Equals(Kind, CampaignAssetContribution.Headquarters, StringComparison.Ordinal);

    /// <summary>
    /// A campaign's shared objects, with its null read as what it means: a game that owns nothing.
    ///
    /// <para><b>It is here rather than on <see cref="Campaign"/> because that file is a bare
    /// record declaration and has to stay one.</b> It is skipped by the presentation-flag scan on
    /// the strength of having no body at all — a record with one could branch on a sheet's flag
    /// while being skipped for doing it — and
    /// <c>PresentationFlagsTests.TheOnlyOtherSkippedFileHasNoLogicInItAtAll</c> holds it to that.
    /// So the one reading every host wants lives beside the type it hands back.</para>
    ///
    /// <para>A null campaign answers the same empty list a campaign with no objects does, because
    /// a host asking this has a character in front of it and may have no campaign resolved — which
    /// is the ordinary state for a member whose account cannot read the game.</para>
    /// </summary>
    public static IReadOnlyList<CampaignAsset> On(Campaign? campaign) => campaign?.Assets ?? [];
}
