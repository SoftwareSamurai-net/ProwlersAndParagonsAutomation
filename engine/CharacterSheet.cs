using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Records a perk selected for a character. Units is always 1 for flat-cost perks;
/// for per_unit perks it holds the quantity purchased (e.g. number of contact types).
/// </summary>
public record SelectedPerk(string PerkId, int Units = 1, string? NarrativeDetail = null);

/// <summary>
/// Records a flaw selected for a character, pairing the flaw id with any
/// narrative detail required by the flaw's NarrativeConstraint.
/// </summary>
public record SelectedFlaw(string FlawId, string? NarrativeDetail = null);

/// <summary>
/// Records which variant of a variable-cost pro/con the player has chosen.
/// For a fixed-cost pro/con, VariantKey is null.
/// For a variable-cost one (e.g. Charges, Area/Burst), VariantKey is the
/// key from CostModifierRange (e.g. "3_per_scene", "area").
/// </summary>
public record SelectedProCon(string Id, string? VariantKey = null)
{
    /// <summary>
    /// Quantity for a Power-specific Pro or Con that scales — how many extra Sources
    /// Also X covers, for instance. Null means "however many units the Power itself has",
    /// which is what Alternate Form's Independent Forms wants.
    /// </summary>
    public int? Units { get; init; }
}

/// <summary>
/// A power as it appears on the character sheet: the power id,
/// how many ranks were purchased (above the free baseline), and
/// which pros/cons have been applied.
/// </summary>
/// <remarks>
/// The primary constructor is marked for the serializer because this record has two, and a
/// deserializer given a choice makes none — it throws. That is the only annotation on the
/// type and it changes nothing else: it lets a host round-trip a whole sheet (the browser
/// keeps one in local storage between visits) without the engine growing a parallel set of
/// data-transfer types that would then have to be kept in step with it.
/// </remarks>
[method: JsonConstructor]
public record SelectedPower(
    string PowerId,
    int PurchasedRanks,
    IReadOnlyList<SelectedProCon> Pros,
    IReadOnlyList<SelectedProCon> Cons)
{
    public SelectedPower(string powerId, int purchasedRanks)
        : this(powerId, purchasedRanks, [], []) { }

    /// <summary>
    /// Which variant was chosen for a Power whose cost varies — a key from
    /// <see cref="Models.PowerModel.CostVariants"/> such as "broad" for Omni-Power
    /// or "extreme_range" for Stretching. Null for fixed-cost Powers.
    /// </summary>
    public string? CostVariantKey { get; init; }

    /// <summary>
    /// How many units were bought for a per_unit Power: immunities for Immunity,
    /// Resolve for Determination, power levels for Alternate Form. Always 1 otherwise.
    /// </summary>
    public int Units { get; init; } = 1;

    /// <summary>
    /// What each unit is, for a Power whose units are named rather than counted — Immunity,
    /// whose entry says each one "is named and paid for separately" (Ch.2 p.31). One entry per
    /// unit, in the order they were bought; an entry may be blank while the player decides.
    ///
    /// <para><b>Null and not an empty list when nothing was recorded</b>, so a character written
    /// before this field existed writes back byte for byte the same, and so the record's equality
    /// is not broken by two empty lists that happen to be different objects.</para>
    ///
    /// <para><b>It costs nothing and is never read by a price.</b> <see cref="Units"/> is what is
    /// paid for; a list longer or shorter than it is <c>CharacterValidator</c>'s to report, never
    /// this record's to repair. Which Powers name their units is
    /// <see cref="Models.PowerModel.UnitsAreNamed"/>.</para>
    /// </summary>
    public IReadOnlyList<string>? UnitNames { get; init; }

    /// <summary>
    /// The names of the units actually bought: the first <see cref="Units"/> entries of
    /// <see cref="UnitNames"/>, trimmed, blanks left out. What a sheet prints.
    /// </summary>
    public IReadOnlyList<string> BoughtUnitNames() =>
        [.. (UnitNames ?? []).Take(Math.Max(0, Units))
                             .Where(n => !string.IsNullOrWhiteSpace(n))
                             .Select(n => n.Trim())];

    /// <summary>
    /// The Trait the player nominated for a baseline_selected_trait Power (Boost,
    /// Expertise). For Boost this also sets the per-rank cost, which matches the affected
    /// Trait's own cost per rank.
    ///
    /// <para><b>What may go in here differs between the two, because their printed entries
    /// differ.</b> Boost (Ch.2 p.24) raises "one specific Ability, Talent, or Power", so all
    /// three kinds of id are legal. An Expertise's specialisation "must fall under one of your
    /// Abilities or Talents" (Ch.2 p.28), so a Power id on one is an error —
    /// <c>CharacterValidator</c> reports it as <c>EXPERTISE_NOMINATION_NOT_A_TRAIT</c>.</para>
    /// </summary>
    public string? BaselineTraitId { get; init; }

    /// <summary>
    /// Which of the six Sources this Power comes from (Ch.2 p.16). Costs nothing and never
    /// changes a Power's rank, but a published sheet groups Powers under Source headings,
    /// and a rankless Power takes its default rank from the Source's Ability. Null means
    /// the player has not said yet.
    /// </summary>
    public string? SourceId { get; init; }
}

/// <summary>
/// A custom feature bought for a piece of gear. GradeKey picks between the two grades of
/// the features the rulebook prices at 1 to 2 HP (Accurate/Very Accurate,
/// Powerful/Very Powerful); it is null for the ten flat ones.
/// </summary>
public record SelectedGearFeature(string FeatureId, string? GradeKey = null);

/// <summary>
/// A piece of gear on the character sheet.
///
/// <para>Ch.6 makes mundane gear free and explicitly untracked, so a plain item is just a
/// name and costs nothing. Only custom features and Pros and Cons cost Hero Points.</para>
/// </summary>
public record SelectedGear(string Name)
{
    /// <summary>
    /// The Chapter 6 catalogue row this item was chosen from, or null for something the player
    /// simply wrote down.
    ///
    /// <para><b>An id and not a copy of the row.</b> What a Battle Axe is worth is the rulebook's
    /// answer and belongs in <c>gear.json</c>; a character sheet that carried the figure would be
    /// a second copy of it, and the two would disagree the first time the data was corrected.
    /// <see cref="GearCatalogue.Find"/> resolves it, and an id that resolves to nothing is
    /// <c>CharacterValidator</c>'s to report — an illegal character is reported, never
    /// repaired.</para>
    ///
    /// <para><b>Null is the ordinary case and stays legal.</b> p.91's list is "examples, not a
    /// catalogue of prices", so a character may carry a letter from their mother; and a payload
    /// written before this field existed reads back as exactly the item it always was.</para>
    /// </summary>
    public string? CatalogueId { get; init; }

    /// <summary>Custom features bought for this item (Ch.6, p.93). Usually empty.</summary>
    public IReadOnlyList<SelectedGearFeature> Features { get; init; } = [];

    /// <summary>Pros applied to this item. They cost the same on gear as on a Power.</summary>
    public IReadOnlyList<SelectedProCon> Pros { get; init; } = [];

    /// <summary>Cons applied to this item. They never pay out — gear floors at 0 HP.</summary>
    public IReadOnlyList<SelectedProCon> Cons { get; init; } = [];

    /// <summary>
    /// Set when this entry stands for a matched pair customised under the Two-Fisted
    /// Power, which the rulebook lets you buy "for the price of one". Recording the pair
    /// as a single item already charges once; the flag exists so the sheet says why, and
    /// so the validator can check the Power is actually there.
    /// </summary>
    public bool PairedUnderTwoFisted { get; init; }

    /// <summary>True if this item costs Hero Points at all.</summary>
    public bool IsCustomised => Features.Count > 0 || Pros.Count > 0 || Cons.Count > 0;
}

/// <summary>
/// A feature bought for a vehicle or a headquarters (Ch.6 pp.96-103).
///
/// <para><b>One record for both tables, because the price has the same three shapes on each</b> —
/// a flat number, a pair or triple of graded numbers keyed by the word the entry itself uses, or a
/// rate per unit. What differs is the currency, and the currency is a fact about the thing the
/// feature is bought for rather than about the selection.</para>
/// </summary>
/// <param name="FeatureId">
/// The feature's own id, unprefixed — <c>flight</c>, <c>training_facilities</c>. The
/// <see cref="AssetCatalogue"/> prefixes are for a palette row, which has to keep three tables
/// apart; a selection already knows which table it is on from where it is stored.
/// </param>
public record SelectedAssetFeature(string FeatureId)
{
    /// <summary>
    /// How many units of a per-unit feature were bought — four more passengers each, one rank of
    /// a Mecha's Might, one Hero Point's worth of Unique Systems. Always 1 otherwise.
    /// </summary>
    public int Units { get; init; } = 1;

    /// <summary>
    /// Which grade of a graded feature, keyed as the entry words it: <c>standard</c> or
    /// <c>advanced</c> throughout, <c>small</c>/<c>large</c> on Hidden Compartments, and
    /// <c>large</c>/<c>sprawling</c>/<c>awe_inspiring</c> on Size. Null on a flat feature.
    /// </summary>
    public string? GradeKey { get; init; }
}

/// <summary>
/// A unique vehicle this character owns outright (Ch.6 pp.94-100).
///
/// <para><b>Two currencies, and only one of them is Hero Points.</b>
/// <see cref="PerkHeroPoints"/> is what the character spent on the Unique Vehicle Perk for this
/// machine, and it is the only figure here that counts against a tier's budget. Everything else is
/// priced in Vehicle Points, twenty-five to the Hero Point, and adding one to a Hero Point total is
/// a category error — see <see cref="CostCalculator.VehiclePointsSpent"/>.</para>
///
/// <para><b>The four ranks are bought from zero.</b> p.96 opens Body, Speed and Control at nothing
/// and leaves Weapons off entirely, so a machine costs what it is, and a vehicle with a rank
/// nobody paid for would be a Perk that quietly bought more than it says.
/// <see cref="SelectedGear"/>'s free baseline is the wrong precedent: mundane gear is free because
/// the chapter says it is not tracked, and a unique vehicle is the opposite of untracked.</para>
///
/// <para><b>A vehicle a whole team paid for is not this.</b> Both p.96 and p.100 let Heroes pool
/// their allowances on one object, and a <see cref="CharacterSheet"/> is one character — so a
/// shared machine is a <see cref="CampaignAssetContribution"/>, which records what this character
/// put in and nothing about what the object turned out to be. This record is for a vehicle one
/// character owns: a solo Hero's, or a Villain's.</para>
/// </summary>
public record OwnedVehicle(string Name)
{
    /// <summary>
    /// Hero Points spent on the Unique Vehicle Perk for this machine. Twenty-five Vehicle Points
    /// each, and the one figure here <see cref="CostCalculator.TotalCost"/> charges.
    /// </summary>
    public int PerkHeroPoints { get; init; }

    /// <summary>Body, at one Vehicle Point a rank: durability, armour and health in one figure.</summary>
    public int Body { get; init; }

    /// <summary>Speed, at one Vehicle Point a rank.</summary>
    public int Speed { get; init; }

    /// <summary>
    /// Control, at two Vehicle Points a rank — a modifier rather than a rank in its own right.
    /// Negative is legal and pays two points back a rank, to a floor of −3 the validator reports.
    /// </summary>
    public int Control { get; init; }

    /// <summary>
    /// Weapons, at one Vehicle Point a rank, or null for a machine with none. Null rather than
    /// zero because the printed tables print an em dash: an unarmed vehicle has no rank at all,
    /// which is not the same claim as a rank of nothing.
    /// </summary>
    public int? Weapons { get; init; }

    /// <summary>Features off p.96-100's table of twenty-three, priced in Vehicle Points.</summary>
    public IReadOnlyList<SelectedAssetFeature> Features { get; init; } = [];
}

/// <summary>
/// A headquarters this character owns outright (Ch.6 pp.100-103).
///
/// <para><b>The Perk buys the building and the second currency furnishes it.</b> p.100 grants a
/// basic base — a mansion or a warehouse, already fitted out — for the Perk alone, and every Hero
/// Point after that is three Base Points. So a headquarters with no features is not an error and
/// not free either: it cost whatever <see cref="PerkHeroPoints"/> says.</para>
///
/// <para><b>p.100 gives Villains headquarters too</b>, in as many words, which is why nothing here
/// is conditioned on what kind of character owns it. The one place that distinction bites is
/// Teamwork, which Training Facilities grants and which behaves exactly like Resolve — computed
/// for anybody, quoted for a Hero. See <see cref="DerivedStatsCalculator.CalculateTeamwork"/>.</para>
/// </summary>
public record OwnedHeadquarters(string Name)
{
    /// <summary>
    /// Hero Points spent on the Headquarters Perk for this base. Three Base Points each, and the
    /// one figure here <see cref="CostCalculator.TotalCost"/> charges.
    /// </summary>
    public int PerkHeroPoints { get; init; }

    /// <summary>Features off pp.100-103's table of twenty-two, priced in Base Points.</summary>
    public IReadOnlyList<SelectedAssetFeature> Features { get; init; } = [];
}

/// <summary>
/// A Gadget built under Ch.6 p.94 — <b>the one thing in this chapter that pays Hero Points out
/// rather than charging them.</b>
///
/// <para>A successful build hands the builder twice the Gadget's Complexity in Hero Points, and
/// they are spent on Abilities, Talents and Powers under the ordinary cost rules. That pool is not
/// the character's own budget and is deliberately not folded into
/// <see cref="CostCalculator.TotalCost"/>: a Gadget makes a character no more expensive, and a
/// total that netted the two off would report a Hero who had built three Gadgets as cheaper than
/// the same Hero on the page.</para>
///
/// <para><b>The Item Con is on it and is not credited</b>, which is the answer gear already gets —
/// p.94 says the Gadget carries it, and a statement of what a thing is is not a discount to claim.
/// See <see cref="CostCalculator.GadgetSpend"/>.</para>
/// </summary>
public record BuiltGadget(string Name)
{
    /// <summary>
    /// What the builder assigned, at least 3 and at most their own Technology rank. It decides
    /// both the difficulty of the attempt and the size of the pool a success pays out.
    /// </summary>
    public int Complexity { get; init; }

    /// <summary>Powers bought out of the pool, priced exactly as a character's own are.</summary>
    public IReadOnlyList<SelectedPower> Powers { get; init; } = [];

    /// <summary>Ability ranks bought out of the pool, at 1 HP a rank.</summary>
    public IReadOnlyDictionary<string, int> AbilityRanks { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Talent ranks bought out of the pool, at 1 HP a rank.</summary>
    public IReadOnlyDictionary<string, int> TalentRanks { get; init; } =
        new Dictionary<string, int>();
}

/// <summary>
/// Hero Points this character put into a vehicle or headquarters that belongs to the campaign
/// rather than to them (Ch.6 p.96 and p.100, which both let Heroes pool their allowances).
///
/// <para><b>This is the whole of the sheet's side of a shared object, and that is deliberate.</b> A
/// <see cref="CharacterSheet"/> is one character, so a pooled machine is either unrepresentable on
/// one or double-counted across five. What a character can honestly say is how much they put in;
/// what the object came out as is the campaign's answer, summed from every member's contribution.
/// The campaign-side object is a later slice — this shape exists so it can be summed.</para>
///
/// <para><b>It costs Hero Points on this sheet and nothing else on this sheet.</b> There is no
/// second currency here, no features and no ranks: those belong to the object, and a copy of them
/// on each member's sheet would be five copies to disagree.</para>
/// </summary>
/// <param name="AssetId">
/// The campaign's id for the object. Stable, and the key a campaign sums on — the name below is
/// what a reader sees and may be re-typed.
/// </param>
public record CampaignAssetContribution(string AssetId)
{
    /// <summary>What the object is called, for a sheet that has no campaign in front of it.</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// <see cref="Vehicle"/> or <see cref="Headquarters"/> — which currency the campaign will
    /// convert the pooled Hero Points into.
    ///
    /// <para><b>A string rather than an enum</b>, because this travels through JSON to a campaign
    /// this engine cannot see, and a serializer writing an enum as <c>0</c> would make the payload
    /// depend on a declaration order. The validator reports anything that is neither.</para>
    /// </summary>
    public string Kind { get; init; } = Vehicle;

    /// <summary>Hero Points put in. Charged on this sheet like any other spend.</summary>
    public int HeroPoints { get; init; }

    /// <summary>
    /// The proposer's own build, when this contribution <em>is</em> a proposal rather than a
    /// contribution to an object the GM already wrote down — PROGRESS item 33, rulings 5+6: the
    /// player builds a vehicle or base and presents it, and the GM adopts, adopts with changes, or
    /// refuses.
    ///
    /// <para><b>Null on every contribution that funds an object the campaign already has</b>, which
    /// is every contribution this engine has ever read before this field existed — so an older
    /// export reads back byte-identical, and nothing here bumped a stored character's version.</para>
    ///
    /// <para><b>The proposer mints <see cref="CampaignAsset.Id"/> and the same id goes on
    /// <see cref="AssetId"/> above</b> — the same id a GM's own editor would mint, off
    /// <c>StoredCampaign.NewAssetId()</c>. On adoption the campaign's own record keeps that id, so
    /// the contribution is never re-pointed and no <c>UNKNOWN_CAMPAIGN_ASSET</c> appears the
    /// moment the GM says yes. An amendment — a proposal naming an id the campaign already holds —
    /// is not a second proposal; it is the same object shown as a diff against what the GM already
    /// adopted.</para>
    ///
    /// <para><b>Priced the moment it is written, before any GM has seen it</b> — the owner's
    /// answer to "when is it spent": the points are the player's own from character creation, and
    /// a proposal riding the submission is charged like any other purchase on the sheet, by
    /// <see cref="CostCalculator.TotalAssetPerkCost"/> reading <see cref="HeroPoints"/> exactly as
    /// it always has. Nothing about pricing a contribution changed; what is new is that the object
    /// beside it may be one this sheet invented rather than one the campaign already had.</para>
    /// </summary>
    public CampaignAsset? Proposal { get; init; }

    /// <summary>The two kinds of shared object Chapter 6 lets a team pool points on.</summary>
    public const string Vehicle = "vehicle";

    /// <inheritdoc cref="Vehicle"/>
    public const string Headquarters = "headquarters";

    /// <summary>Both of the above, for a host offering a choice and for the validator's message.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Vehicle, Headquarters];

    /// <summary>
    /// The most Hero Points one contribution may put into a shared object.
    ///
    /// <para><b>The owner's 2026-09-10 ruling.</b> A campaign's unlimited-budget game legally lets
    /// a contribution of a hundred million Hero Points sit on a sheet, and
    /// <see cref="CostCalculator.CampaignAssetBudget"/> multiplying that by the object's
    /// points-per-Hero-Point rate is arithmetic the engine should never be asked to trust. Asked
    /// for a real ceiling, the owner's words were that a game would never exceed this much on a
    /// single Hero — so a figure above it is not a huge campaign, it is a mistake, and
    /// <c>CharacterValidator.CheckCampaignAssets</c> reports it rather than letting it reach the
    /// arithmetic. One constant, so the figure in the message and the figure the checked sum can
    /// actually reach never drift apart.</para>
    /// </summary>
    public const int MaxHeroPoints = 10_000;
}

/// <summary>
/// This sheet is another telling of some other character — a later chapter, a second
/// audience's view, or (not yet — see below) another form of the same one. Item 21's slice one:
/// the owner's roster held <i>Cael Hughes — Emergence</i>, <i>— Realised</i> and
/// <i>— After School Specials</i>; two characters called <i>Emir Hughes</i>; and
/// <i>Lena (true capability — GM eyes only)</i> beside <i>Lena (as observed)</i>, told apart only
/// by name. This is the mechanism, agreed with the owner on 2026-09-10.
///
/// <para><b>One-directional, child to root, and that is the whole shape.</b> A variant names the
/// id of the character it is a version of; the root carries nothing pointing the other way, and a
/// root is simply any character nothing else names. Finding a root's children means asking every
/// other character whether it names this one — <c>web/</c>'s business, since the engine cannot see
/// the roster at all.</para>
///
/// <para><b>The link changes nothing about one sheet, on purpose.</b> Nothing in
/// <see cref="CostCalculator"/> or <see cref="DerivedStatsCalculator"/> may read this record or
/// this field — <c>CharacterVariantTests.CostAndDerivedStatsNeverReadTheVariantField</c> is the
/// guard, in the shape of <c>PresentationFlagsTests.NoRulesCodeReadsAPresentationFlag</c>, which
/// keeps <see cref="CharacterSheet.IsVillain"/> out of the arithmetic. A character with a
/// <see cref="CharacterSheet.Variant"/> costs and validates exactly as it would with none.</para>
///
/// <para><b>Slice two: <see cref="AlternateForm"/> is the one kind that carries rules, and they
/// are rules about a set of sheets.</b> Ch.2's Alternate Form Power says a form is "built as a
/// separate character with its own Hero Point budget", both forms pay for the Power, and they
/// share one Resolve pool. <see cref="Later"/> and <see cref="AsSeenBy"/> name nothing an engine
/// prices; a family of alternate forms is read by <see cref="AlternateForms"/>, which is handed a
/// whole roster and reports what the set breaks — a form the root did not pay for, a form above
/// the root's power level, two forms paying differently — and the one pool at the lowest of the
/// forms' Resolve. It reports, never repairs, and each form still costs and validates alone as the
/// character it is. The one host that hands the engine two sheets at once is <c>build --from</c>
/// with more than one file.</para>
/// </summary>
/// <param name="OfCharacterId">
/// The id of the character this is a version of. Blank is reported as
/// <c>VARIANT_WITHOUT_ROOT</c> rather than repaired — an id this engine could invent would be a
/// guess about which character was meant.
/// </param>
public record CharacterVariant(string OfCharacterId, string Kind)
{
    /// <summary>The same character, later in the story — <i>Cael Hughes — Realised</i>.</summary>
    public const string Later = "later";

    /// <summary>
    /// The same character as a different audience sees them — the GM-eyes/observed pair.
    /// </summary>
    public const string AsSeenBy = "as_seen_by";

    /// <summary>
    /// Another form of the same character, Chapter 2's Alternate Form Power. The one kind that
    /// carries rules, all of them about the set — see <see cref="AlternateForms"/>.
    /// </summary>
    public const string AlternateForm = "alternate_form";

    /// <summary>Every kind a link may name, for a host offering a choice and for the validator's message.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Later, AsSeenBy, AlternateForm];
}

/// <summary>
/// Mutable state object for a character being built in the wizard.
/// All calculators and validators receive this and read from it.
/// </summary>
public class CharacterSheet
{
    /// <summary>
    /// Whether this character is a Villain rather than a Hero. <b>Presentation only: no rules
    /// code may read this, and there is a test that none does.</b>
    ///
    /// <para><b>Ch.9 builds Villains by exactly the Hero rules and prints no separate stat-block
    /// format</b>, so this changes no cost, no rank, no derived figure and no verdict. It is here
    /// rather than on a front end because a character that is a Villain should still be one after
    /// it has been exported and read back, and a palette held outside the character is a palette
    /// the file does not carry.</para>
    ///
    /// <para><b>This field replaces a rule that used to forbid it, and the reason it is now
    /// allowed is worth keeping.</b> The old refusal was correct while "Villain" meant two things
    /// at once — a palette <i>and</i> no Hero Point budget — because the second is mechanical and
    /// a mechanical flag here would be the browser deciding a rule. The budget half has moved to
    /// <see cref="UnlimitedBudget"/>, so what is left really is only a colour. Put a mechanic back
    /// on this field and the old objection applies again in full.</para>
    /// </summary>
    public bool IsVillain { get; set; }

    /// <summary>
    /// Whether this character is being built without a Hero Point limit — the sandbox.
    /// <b>Also presentation only, and held to the same test.</b>
    ///
    /// <para><b>It is independent of <see cref="IsVillain"/> on purpose.</b> A Hero can be built
    /// in the sandbox and a Villain can be held to a budget; one control deciding the other is
    /// exactly the conflation this pair was split to end.</para>
    ///
    /// <para><b>The validator is still never told, and still reports the budget.</b> A host
    /// decides what to do with the finding: the browser shows a running total and no cap, while
    /// <c>build --from</c> reports every finding the engine returns, because a report that
    /// quietly dropped one on the strength of a flag in its own input would be worth less than no
    /// report. The engine answers; hosts present.</para>
    /// </summary>
    public bool UnlimitedBudget { get; set; }

    /// <summary>Id of the selected tier, or null if not yet chosen.</summary>
    public string? SelectedTierId { get; set; }

    /// <summary>
    /// The <see cref="Campaign"/> this character belongs to, or null for one that belongs to no
    /// game. <b>No rules code may read this, and there is a test that none does.</b>
    ///
    /// <para><b>It is barred for a different reason from the two flags above, and the difference
    /// is worth keeping.</b> Those are barred as <i>presentation</i> — a palette and a way of
    /// working, which a rule branching on either would be the browser deciding. This is barred as
    /// an <i>indirection</i>: it is an id, and the only thing rules code could do with an id is
    /// resolve it, which means asking storage. Storage in a browser is asynchronous, and
    /// <see cref="IRulesSource"/> is deliberately synchronous so that <see cref="CostCalculator"/>
    /// and <see cref="CharacterValidator"/> stay pure and instantly callable — and the two guards
    /// that ban a filesystem and a network here already ban both ways such a lookup could be
    /// written. So there is no reader for this in <c>engine/</c> and there must not be one.</para>
    ///
    /// <para><b>The character still carries its own tier, cap and budget, and that is not
    /// duplication to be tidied away.</b> A character is portable: it is exported, imported,
    /// handed to the headless command and priced by an MCP client, none of which has a campaign in
    /// front of it. Joining a campaign copies settings into an empty field and reports a
    /// disagreement otherwise — never repairs one; see <c>web/Services/CampaignJoin.cs</c>.</para>
    ///
    /// <para><b>An older saved character has no such field, and reads back as null</b>, which
    /// correctly means "belongs to no campaign". That is why nothing about this bumped
    /// <c>StoredCharacter</c>'s version, which would have discarded every stored character in
    /// silence.</para>
    /// </summary>
    public string? CampaignId { get; set; }

    /// <summary>
    /// A <b>house Trait Cap</b> this character is built to, or null for "whatever the tier
    /// says". Pinnacle City caps a non-superhuman at 6d where the Standard tier allows 12d.
    ///
    /// <para><b>It substitutes for the tier's cap; it does not merely gate validation.</b>
    /// <see cref="DerivedStatsCalculator.CalculateResolve"/> is
    /// <c>max(0, (TraitCap − highestRelevantRank) × 2)</c>, so the cap <em>is</em> the datum
    /// Resolve is measured from. Gate on a 6d house cap while a 12d tier keeps doing the
    /// arithmetic and a character sitting at 4d is paid <c>(12−4)×2 = 16</c> Resolve for a
    /// restraint the campaign imposed rather than one they chose; substituting pays
    /// <c>(6−4)×2 = 4</c>, and staying low becomes a decision with a price. The owner settled
    /// this on 2026-09-05 and the trade is the player's to make. <b>The other direction comes
    /// with it</b>: a tighter cap lowers the Resolve <em>ceiling</em> too — 24 at 12d, 12 at
    /// 6d — which is what being tied to the cap means, and reads as a nerf the first time
    /// somebody sees it.</para>
    ///
    /// <para><b>The character carries its own copy for the same reason it carries its tier.</b>
    /// A character is portable: exported, imported, handed to <c>build --from</c> and priced by
    /// an MCP client, none of which has a campaign in front of it.
    /// <c>web/Services/CampaignJoin.cs</c> copies a campaign's cap into this field when it is
    /// empty and reports a disagreement otherwise — never repairs one.</para>
    ///
    /// <para><b>Read it through <see cref="DerivedStatsCalculator.EffectiveTraitCap"/> and
    /// nowhere else</b> — <em>every</em> surface that answers "what is this character built to",
    /// which is Resolve, the validator, the browser's session, the printed sheet, a replay's
    /// verdict, both report builders, the JSON export and all three rank prompts in the terminal
    /// wizard. A second spelling of <c>sheet.TraitCapRank ?? tier.TraitCapRank</c> is how one of
    /// them ends up disagreeing with the figure beside it.</para>
    ///
    /// <para><b>The list is not a count, and it used to be one.</b> This paragraph said "six
    /// places" while there were eight, because a number in a sentence is not a guard and nothing
    /// re-counted it. <c>TraitCapReadTests</c> is the guard: it scans <c>web/</c>, <c>cli/</c>,
    /// <c>mcp/</c> and <c>sheets/</c> for a tier-shaped read of this property and requires each
    /// one to be named there with the reason it is about the tier rather than about a
    /// character.</para>
    ///
    /// <para><b>Nonsense here is reported, never repaired</b>, like everything else: a cap above
    /// the tier's, or below 1d, is an error and the arithmetic still uses the number as written.
    /// An older saved character has no such field and reads back as null, which correctly means
    /// "the tier's" — which is why nothing about this bumped <c>StoredCharacter</c>'s version.</para>
    /// </summary>
    public int? TraitCapRank { get; set; }

    /// <summary>
    /// The optional rules the table this character plays at has turned on, or null for the book
    /// as printed — the ten Gritty Combat Rules and the three switches beside them.
    ///
    /// <para><b>Carried, and read by nothing in this engine.</b> Not one of these settings is
    /// about what a character costs or whether it is legal, which is the whole of what
    /// <see cref="CostCalculator"/> and <see cref="CharacterValidator"/> decide. They are about
    /// resolving a fight, and the fight belongs to the second engine — see
    /// <see cref="CampaignTable"/>, whose switches are named after the ones <c>play/</c> reads and
    /// held to that list by a test.</para>
    ///
    /// <para><b>So why is it on the sheet at all?</b> For the reason
    /// <see cref="TraitCapRank"/> is: a character is portable and a campaign is not. The encounter
    /// server is handed characters, never a campaign — it cannot resolve a
    /// <see cref="CampaignId"/> any more than this engine can — so a fight fought with somebody's
    /// Hero is fought under the book unless the Hero brought its table's rules along. The
    /// <c>.json</c> export carries the block for exactly that reader.</para>
    ///
    /// <para><b>Copied on joining, and never written over one that is already here</b> — the same
    /// rule the cap follows, in <c>web/Services/CampaignJoin.cs</c>.</para>
    ///
    /// <para><b>Leaving a game does not take it off, and that is the precedent rather than an
    /// oversight.</b> Leaving is a fact about a campaign's roster and touches nobody's own work —
    /// the tier, the sandbox flag and the house cap all stay, and a deleted campaign leaves every
    /// member still naming it on purpose, so that restoring it puts everything back. What a sheet
    /// out of its game gets instead is a <em>finding</em>: <c>CampaignJoin.Inspect</c> says so on
    /// the campaigns page. Reported, never repaired, like everything else here.</para>
    ///
    /// <para><b>Absent on an older saved character, and null is the book</b>, which is what every
    /// character stored before this existed was already playing — so nothing here bumped
    /// <c>StoredCharacter</c>'s version.</para>
    /// </summary>
    public CampaignTable? CampaignTable { get; set; }

    /// <summary>
    /// What this character's table charges for Immortality, or null for the 3 Hero Points the
    /// book prices it at.
    ///
    /// <para><b>The one house rule here that is a price, and so the one this engine reads.</b>
    /// Ch.2 p.31 prices Immortality at 3 HP and then says "In a game where Heroes can die, GMs
    /// should charge more for this — somewhere between 6 and 12 Hero Points" — a range where
    /// every other entry in <c>data/rules/</c> prints a number.
    /// <see cref="CostCalculator.PowerCost"/> charges this when it is set, so a table that
    /// charges 9 changes what the Power costs and therefore whether the character fits its
    /// budget.</para>
    ///
    /// <para><b>The bound is in the data, not in the code.</b>
    /// <c>PowerModel.CampaignCostMin</c> and <c>CampaignCostMax</c> are a transcription of that
    /// sentence, and <see cref="CharacterValidator"/> reports a price outside them as
    /// <c>IMMORTALITY_COST_OUTSIDE_RANGE</c>. A figure spelled in C# would be a price this
    /// project had invented, which is the one thing <c>data/rules/</c> exists to prevent.</para>
    ///
    /// <para><b>Reported, never repaired</b>, like everything else: a price outside the range, or
    /// one on a character in no campaign at all, is an error and the arithmetic still charges the
    /// number as written. The second of those is a finding rather than a shrug because a house
    /// price is a fact about a table — a character carrying one and naming no game is a sheet
    /// that has been hand-edited or has left a campaign without the price going with it.</para>
    ///
    /// <para><b>It costs Hero Points and the Trait Cap does not</b>, which is the one way this
    /// differs from its neighbour above: a campaign that re-prices Immortality moves the spend on
    /// every character in it that has the Power, so a diff has to show it under such a
    /// character.</para>
    /// </summary>
    public int? ImmortalityCost { get; set; }

    /// <summary>
    /// Id of the optional package applied (Civilian/Hero/Superhero), or null.
    /// The package sets a floor on ability and talent ranks — it does not
    /// prevent buying higher ranks on top.
    /// </summary>
    public string? SelectedPackageId { get; set; }

    /// <summary>Purchased ability ranks, keyed by ability id.</summary>
    public Dictionary<string, int> AbilityRanks { get; } = new();

    /// <summary>
    /// Pros and Cons applied to an Ability rather than a Power, keyed by ability id.
    /// The rulebook allows this — the Brute Option is Overkill on Might, and the
    /// published Stronghold buys four Abilities through his armour with the Item Con.
    /// </summary>
    public Dictionary<string, List<SelectedProCon>> AbilityModifiers { get; } = new();

    /// <summary>Purchased talent ranks, keyed by talent id.</summary>
    public Dictionary<string, int> TalentRanks { get; } = new();

    /// <summary>
    /// Sources for Abilities, keyed by ability id. Ch.2 p.16: every Ability, Talent and
    /// Power has a Source, and Abilities are usually Innate "at least when dealing with
    /// ordinary people. When dealing with supers and characters who aren't human, however,
    /// anything goes." An entry here is therefore a Trait whose Source is <em>not</em> the
    /// default — which is exactly what a published sheet prints, as an
    /// <c>Abilities (…)</c> line inside a Power group.
    ///
    /// <para><b>This cannot be derived from rank.</b> Ch.2 p.64 says "Sources for your
    /// Powers and Abilities with a rank of 7d or greater", which reads like a threshold and
    /// is not one — it is an instruction about which Traits to roll Sources for during
    /// random generation. The printed sheets disagree with it in both directions: Alabama
    /// Slammer marks 6d Perception and Toughness, and Citizen Soldier leaves 9d Willpower
    /// unmarked. Storing the answer is the only way to reproduce them.</para>
    /// </summary>
    public Dictionary<string, string> AbilitySources { get; } = new();

    /// <summary>
    /// Sources for Talents, keyed by talent id. Absent means the Trained default. Kept apart
    /// from <see cref="AbilitySources"/> because the two defaults differ, so a single
    /// dictionary could not say what a missing entry meant.
    /// </summary>
    public Dictionary<string, string> TalentSources { get; } = new();

    /// <summary>Powers on this character sheet.</summary>
    public List<SelectedPower> SelectedPowers { get; } = new();

    /// <summary>Perks purchased for this character.</summary>
    public List<SelectedPerk> Perks { get; } = new();

    /// <summary>Flaws selected for this character (min 1, max 3 at creation).</summary>
    public List<SelectedFlaw> Flaws { get; } = new();

    // ── Convenience helpers ───────────────────────────────────────────────

    public int GetAbilityRank(string abilityId) =>
        AbilityRanks.GetValueOrDefault(abilityId, 0);

    public int GetTalentRank(string talentId) =>
        TalentRanks.GetValueOrDefault(talentId, 0);

    public SelectedPower? GetPower(string powerId) =>
        SelectedPowers.FirstOrDefault(p => p.PowerId == powerId);

    public bool HasPower(string powerId) =>
        SelectedPowers.Any(p => p.PowerId == powerId);

    // ── Narrative & flavour (CLI-facing, not used by engine logic) ────────

    public string Name { get; set; } = "";
    public string Appearance { get; set; } = "";
    public string Motivation { get; set; } = "";
    public string Quote { get; set; } = "";
    public List<string> Connections { get; } = [];

    /// <summary>
    /// Gear carried. Mundane gear is free, so most entries cost nothing and this stays a
    /// narrative list — but a customised item does spend Hero Points, which is why it is
    /// not simply a list of strings. See <see cref="CostCalculator.TotalGearCost"/>.
    /// </summary>
    public List<SelectedGear> Gear { get; } = [];

    /// <summary>
    /// Vehicles this character owns outright, each with the Hero Points that bought it and the
    /// Vehicle Points those Hero Points became. Empty on nearly every character.
    /// </summary>
    public List<OwnedVehicle> Vehicles { get; } = [];

    /// <summary>Headquarters this character owns outright, on the same terms.</summary>
    public List<OwnedHeadquarters> Headquarters { get; } = [];

    /// <summary>
    /// Gadgets built under p.94. <b>These cost the character nothing</b> — the pool runs the other
    /// way — so nothing here is in <see cref="CostCalculator.TotalCost"/>.
    /// </summary>
    public List<BuiltGadget> Gadgets { get; } = [];

    /// <summary>
    /// Hero Points put into vehicles and headquarters that belong to a campaign rather than to
    /// this character. Charged here; everything else about the object belongs to the campaign.
    /// </summary>
    public List<CampaignAssetContribution> CampaignAssets { get; } = [];

    /// <summary>
    /// This character as another telling of some other character, or null for a root — see
    /// <see cref="CharacterVariant"/>. Null on every character that predates item 21's slice one,
    /// which is what makes it optional rather than something a migration has to touch.
    ///
    /// <para><b>Nothing that prices or derives one sheet may read this, and there is a test that
    /// none does</b> — see <see cref="CharacterVariant"/>'s own remarks; <see cref="AlternateForms"/>
    /// reads it, and is handed a roster rather than a sheet. It is here for the same reason
    /// <see cref="CampaignId"/> is: a character is portable, so the relationship travels with the
    /// export rather than living only in whichever browser drew the tree.</para>
    /// </summary>
    public CharacterVariant? Variant { get; set; }
}
