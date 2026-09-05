using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// One switch a table throws before play, and the entry that prints it.
/// </summary>
/// <param name="Name">The property on <see cref="TableRules"/> this switch is.</param>
/// <param name="File">Which of the play rules files the entry is in.</param>
/// <param name="EntryId">The entry's id, which <c>PlayTableRulesTests</c> requires to exist.</param>
public sealed record TableSwitch(string Name, string File, string EntryId);

/// <summary>
/// What this table has turned on: the ten Gritty Combat Rules, the GM's alternative to seizing the
/// initiative, Checking Your Swing, the optional Edge roll, and the Gear Limit.
///
/// <para><b>Every one of them defaults to the book's baseline, which is every gritty rule off.</b>
/// p.79 introduces the ten as optional and tells a table to review them before adopting any; a
/// simulator whose default was anything else would be measuring a game the book does not describe.
/// <see cref="Book"/> is that baseline and is what an encounter gets when nobody says otherwise.
/// </para>
///
/// <para><b>Each switch names an entry, and a test holds the two together.</b> A setting whose id
/// matched no entry would be a rule this project had invented — the fastest way for a simulator to
/// start applying something the book does not print — so <see cref="Switches"/> is walked against
/// the shipped data. It is an allowlist of settings rather than a denylist of them, so a switch
/// added under a name nobody anticipated is flagged rather than missed.</para>
///
/// <para><b>What a switch does <em>not</em> do is decide anything on its own.</b> Every rule that
/// reads one of these reads its entry too, so the figure it applies comes from the file and the
/// ledger line can cite the page.</para>
/// </summary>
public sealed record TableRules
{
    /// <summary>The book's baseline: every gritty rule off, the standard success map, no Edge roll.</summary>
    public static TableRules Book { get; } = new();

    // ── The ten Gritty Combat Rules, pp.79–81 ────────────────────────────────

    /// <summary>p.79: a second active defence on a page costs a die, and each one after that costs another.</summary>
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
    ///
    /// <para>It is the GM's choice and not the buyer's, and no page says whether it is fixed for a
    /// table, for a campaign, or taken per purchase; the entry's own <c>ambiguity</c> records that
    /// and Chapter 5 raises it from the other side. A setting for the whole table is the reading
    /// that makes a measurement reproducible, and it is a reading — see
    /// <c>docs/guide/play-engine.md</c>.</para>
    /// </summary>
    public bool GmAlternativeToSeizingInitiative { get; init; }

    /// <summary>
    /// Ch.3 p.69: the flatter success map, under which every even face is worth one success.
    /// A setting for a whole table by the entry's own words, which is why it is here and not an
    /// intent.
    /// </summary>
    public bool CheckingYourSwing { get; init; }

    /// <summary>
    /// p.73: rolling for the order of action instead of counting Edge downward — "have everyone
    /// make an Edge roll when the fight starts", the successes standing in as their Edge for that
    /// battle.
    /// </summary>
    public bool RandomInitiative { get; init; }

    /// <summary>
    /// The highest effective Trait rank mundane equipment will carry, or null for the default the
    /// Gear Limit entry prints. Only read when <see cref="RaisedGearLimit"/> is on; p.80 offers 9
    /// and 12 and says the ladder is open-ended.
    /// </summary>
    public int? GearLimitRank { get; init; }

    /// <summary>
    /// Every switch above, with the entry that prints it.
    ///
    /// <para><b>The list is the guard's subject.</b> A setting is a claim that the book prints a
    /// rule, and this is where that claim is made checkable.</para>
    /// </summary>
    public static IReadOnlyList<TableSwitch> Switches { get; } =
    [
        new(nameof(ActiveDefensesCost), PlayRulesRepository.GrittyFile, "gritty_active_defenses"),
        new(nameof(CloseRangePenalty), PlayRulesRepository.GrittyFile, "gritty_close_range"),
        new(nameof(TheDrop), PlayRulesRepository.GrittyFile, "gritty_the_drop"),
        new(nameof(FatalDamage), PlayRulesRepository.GrittyFile, "gritty_fatal_damage"),
        new(nameof(FriendlyFire), PlayRulesRepository.GrittyFile, "gritty_friendly_fire"),
        new(nameof(HardTargets), PlayRulesRepository.GrittyFile, "gritty_hard_targets"),
        new(nameof(RaisedGearLimit), PlayRulesRepository.GrittyFile, "gritty_raised_gear_limit"),
        new(nameof(SlowHealing), PlayRulesRepository.GrittyFile, "gritty_slow_healing"),
        new(nameof(ToughMinions), PlayRulesRepository.GrittyFile, "gritty_tough_minions"),
        new(nameof(WoundPenalties), PlayRulesRepository.GrittyFile, "gritty_wound_penalties"),
        new(nameof(GmAlternativeToSeizingInitiative), PlayRulesRepository.CombatFile, "seize_initiative_gm_alternative"),
        new(nameof(CheckingYourSwing), PlayRulesRepository.ChallengeFile, "checking_your_swing"),
        new(nameof(RandomInitiative), PlayRulesRepository.CombatFile, "edge_order"),
        new(nameof(GearLimitRank), PlayRulesRepository.GrittyFile, "gritty_raised_gear_limit")
    ];

    /// <summary>Whether the named switch is on, by the name <see cref="Switches"/> uses.</summary>
    public bool IsOn(string switchName) => switchName switch
    {
        nameof(ActiveDefensesCost) => ActiveDefensesCost,
        nameof(CloseRangePenalty) => CloseRangePenalty,
        nameof(TheDrop) => TheDrop,
        nameof(FatalDamage) => FatalDamage,
        nameof(FriendlyFire) => FriendlyFire,
        nameof(HardTargets) => HardTargets,
        nameof(RaisedGearLimit) => RaisedGearLimit,
        nameof(SlowHealing) => SlowHealing,
        nameof(ToughMinions) => ToughMinions,
        nameof(WoundPenalties) => WoundPenalties,
        nameof(GmAlternativeToSeizingInitiative) => GmAlternativeToSeizingInitiative,
        nameof(CheckingYourSwing) => CheckingYourSwing,
        nameof(RandomInitiative) => RandomInitiative,
        nameof(GearLimitRank) => GearLimitRank is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(switchName), switchName, "No such table setting.")
    };

    /// <summary>
    /// The switches that are on, by name, in <see cref="Switches"/> order — what a report prints
    /// beside its N, its seed and its policy, because a balance figure without its table settings
    /// is a figure about no particular game.
    /// </summary>
    public IReadOnlyList<string> On() =>
        [.. Switches.Select(s => s.Name).Distinct(StringComparer.Ordinal).Where(IsOn)];

    /// <summary>
    /// The Gear Limit in force: the table's if it raised one, otherwise the entry's own default.
    /// </summary>
    public int GearLimit(PlayRulesRepository play)
    {
        ArgumentNullException.ThrowIfNull(play);

        var entry = play.GetGritty("gritty_raised_gear_limit").GearLimit!;
        return RaisedGearLimit && GearLimitRank is { } raised ? raised : entry.DefaultRank;
    }
}
