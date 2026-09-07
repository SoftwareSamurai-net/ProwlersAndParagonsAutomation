using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// The optional rules a table has turned on — the ten Gritty Combat Rules of pp.79–81, the GM's
/// alternative to seizing the initiative, Checking Your Swing, the optional Edge roll, and the
/// Gear Limit if the table raised one.
///
/// <para><b>Data, and nothing else, for the same reason <see cref="Campaign"/> is.</b> No rule in
/// <c>engine/</c> reads a switch here: none of them is about what a character <em>costs</em> or
/// whether it is <em>legal</em>, which is the whole of what this engine decides. They are about
/// resolving a fight, and that is the second engine's business — <c>play/Encounter/TableRules.cs</c>,
/// which is where every one of these names comes from and where each is tied to the rulebook entry
/// that prints it.</para>
///
/// <para><b>So why is the block here rather than there?</b> Because <c>engine/</c> may not
/// reference <c>play/</c> — the arrows are guarded at the csproj and at the source — and because
/// this is the half that has to be <em>written down</em>: a campaign is stored by the browser,
/// relayed by an account's server that never parses it, and copied onto a character so the sheet
/// stays portable. That is the same job <see cref="CharacterSheet.TraitCapRank"/> does one field
/// over, and it is a record the hosts share rather than a rule this engine reads.</para>
///
/// <para><b>The two name sets are held together by a test rather than by care.</b>
/// <c>CampaignTableNamesTests</c> reads both source files and requires every <c>bool</c> property
/// on <c>TableRules</c> to have a same-named property here and the reverse — so a switch added to
/// one side alone is a failing build rather than a setting a GM can turn on and no encounter will
/// ever read. A shared project between the two engines would have been the other answer and was
/// not taken: it would put a play-rules type on the path of every character this engine prices,
/// for the sake of thirteen booleans that no rules code here is allowed to look at.</para>
///
/// <para><b>Every switch defaults to off, which is the book's own baseline.</b> p.79 introduces
/// the ten as optional and tells a table to review them before adopting any, so a campaign that
/// has said nothing has said "the book as printed" — the same thing <c>TableRules.Book</c> means.
/// That makes a null block and an all-false block the same game, which is what lets a campaign
/// written before this existed read back as the game it always was.</para>
/// </summary>
public sealed record CampaignTable
{
    /// <summary>The book as printed: every optional rule off, and no raised Gear Limit.</summary>
    public static CampaignTable Book { get; } = new();

    // ── The ten Gritty Combat Rules, pp.79–81 ────────────────────────────────

    /// <summary>p.79: a second active defence on a page costs a die, and each one after another.</summary>
    public bool ActiveDefensesCost { get; init; }

    /// <summary>p.79: a ranged attack from inside Close Range costs the dodger two dice.</summary>
    public bool CloseRangePenalty { get; init; }

    /// <summary>p.79: a readied weapon doubles its holder's effective Edge against anyone not ready.</summary>
    public bool TheDrop { get; init; }

    /// <summary>p.79: Health goes below zero and a character dies at the negative of their full Health.</summary>
    public bool FatalDamage { get; init; }

    /// <summary>p.80: shooting into a melee costs four dice, and a shot that lands nothing goes somewhere else.</summary>
    public bool FriendlyFire { get; init; }

    /// <summary>p.80: machines and thick objects double their passive defence.</summary>
    public bool HardTargets { get; init; }

    /// <summary>p.80: the Gear Limit is raised above its default — see <see cref="GearLimitRank"/>.</summary>
    public bool RaisedGearLimit { get; init; }

    /// <summary>p.80: the between-fights healing roll goes away and time takes its place.</summary>
    public bool SlowHealing { get; init; }

    /// <summary>p.81: a Minion takes two whole net successes, and the leftover is thrown away.</summary>
    public bool ToughMinions { get; init; }

    /// <summary>p.81: everything a hurt character rolls is worse, at two bands of damage.</summary>
    public bool WoundPenalties { get; init; }

    // ── The three switches that are not Gritty rules ─────────────────────────

    /// <summary>
    /// p.73: the GM's alternative to seizing the initiative — doubling the buyer's effective Edge
    /// rather than putting them in front of everyone.
    /// </summary>
    public bool GmAlternativeToSeizingInitiative { get; init; }

    /// <summary>
    /// Ch.3 p.69: the flatter success map, under which every even face is worth one success.
    /// </summary>
    public bool CheckingYourSwing { get; init; }

    /// <summary>
    /// p.73: rolling for the order of action instead of counting Edge downward — everyone makes
    /// an Edge roll when the fight starts, and the successes stand in as their Edge for it.
    /// </summary>
    public bool RandomInitiative { get; init; }

    /// <summary>
    /// The highest effective Trait rank mundane equipment will carry, or null for the default the
    /// Gear Limit entry prints. Only read when <see cref="RaisedGearLimit"/> is on; p.80 offers 9
    /// and 12 and says the ladder is open-ended.
    ///
    /// <para><b>It is the one setting here that is a number rather than a switch</b>, and it is
    /// paired with its own boolean rather than replacing it, because <c>play/</c> reads it that
    /// way: a rank left here while the switch is off is a figure the table has not adopted.</para>
    /// </summary>
    public int? GearLimitRank { get; init; }

    /// <summary>
    /// Whether this is the book as printed — no switch on, and no raised Gear Limit.
    ///
    /// <para><b>What a screen asks before printing a list of house rules.</b> A campaign that has
    /// adopted nothing has a house-rules block that is true of every game, and a panel headed
    /// "House rules" over thirteen "no"s tells a reader less than one sentence saying the table
    /// plays the book. It is also what makes a stored campaign written before any of this existed
    /// indistinguishable from one whose GM opened the form and turned nothing on, which is
    /// correct: they are the same game.</para>
    ///
    /// <para><b>Not written down</b>, because it is an answer rather than a setting: a stored
    /// block carrying it would be a second opinion about the thirteen beside it, and the strict
    /// reader would have to be taught a key that means nothing.</para>
    /// </summary>
    [JsonIgnore]
    public bool IsTheBook =>
        this == Book || (!ActiveDefensesCost && !CloseRangePenalty && !TheDrop && !FatalDamage
                         && !FriendlyFire && !HardTargets && !RaisedGearLimit && !SlowHealing
                         && !ToughMinions && !WoundPenalties && !GmAlternativeToSeizingInitiative
                         && !CheckingYourSwing && !RandomInitiative && GearLimitRank is null);
}
