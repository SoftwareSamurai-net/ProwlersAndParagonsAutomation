using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>What one step did: the state that came out, and the lines it added to the ledger.</summary>
/// <param name="State">The new state. The state passed in is untouched.</param>
/// <param name="Added">Only this step's lines — <c>State.Ledger</c> holds all of them.</param>
public sealed record StepResult(EncounterState State, IReadOnlyList<LedgerLine> Added);

/// <summary>
/// The second engine: a fight, resolved a step at a time out of <c>data/rules/play</c>.
///
/// <para><b>It resolves; it does not price and it does not judge.</b> Whether a character is legal
/// and what they cost are the first engine's questions and are settled before a fight starts —
/// <c>PlayContractTests</c> refuses this project the two types that answer them. What happens here
/// is only ever "these dice, against this threshold, under these rules, therefore this".</para>
///
/// <para><b>Every figure comes out of the data and every ledger line cites the entry it came
/// from.</b> There is no literal damage rate, no literal Minion rate and no literal success map in
/// this file. That is not tidiness: a number typed here would be a second transcription of a rule
/// the store already holds, and the two would agree until the day somebody corrected the store.
/// </para>
///
/// <para><b>An intent this slice does not resolve is refused out loud.</b> It writes a ledger line
/// beginning <c>not yet implemented</c> and changes nothing else. A silent no-op is
/// indistinguishable from a rule that ran and had no effect, and a simulator that cannot tell those
/// apart is a simulator that is confidently wrong. <c>docs/guide/play-engine.md</c> lists them.
/// </para>
///
/// <para><b>Sides are a field the caller sets, not something derived.</b> Nothing in Chapters 3–5
/// says who is on whose side; the tie-break ladder on p.73 is about precedence, not teams. So
/// <see cref="Combatant.Side"/> carries it, <see cref="EncounterState.Over"/> and every policy
/// partition on that and on nothing else, and <see cref="CombatantKind"/> is left to do what the
/// ladder actually uses it for. Deriving the side from the kind was a defect: p.73 prints a fight
/// between Heroes, and a Villain's Minions can stand against a Foe.</para>
/// </summary>
public sealed partial class Encounter
{
    private readonly PlayRulesRepository _play;
    private readonly IDiceSource _dice;
    private readonly SuccessCounter _counter;

    /// <param name="play">The play rules.</param>
    /// <param name="dice">Where the faces come from — seeded, or the ones a printed example rolled.</param>
    /// <param name="table">
    /// What the table turned on, which decides the success map and so has to be known before the
    /// counter is built. The same settings must be the ones on the state this encounter steps.
    /// </param>
    public Encounter(PlayRulesRepository play, IDiceSource dice, TableRules? table = null)
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _dice = dice ?? throw new ArgumentNullException(nameof(dice));
        Table = table ?? TableRules.Book;
        _counter = new SuccessCounter(_play, Table.CheckingYourSwing);
    }

    /// <summary>The settings this encounter was built for.</summary>
    public TableRules Table { get; }

    /// <summary>How successes are counted here, for a fixture that wants to check the count itself.</summary>
    public SuccessCounter Counter => _counter;

    /// <summary>
    /// The switches this slice records but does not yet apply, by their <see cref="TableRules"/>
    /// name.
    ///
    /// <para><b>They are listed rather than left silent</b>, and <see cref="Begin"/> writes a ledger
    /// line for each one that is on. A table setting that is accepted and quietly ignored is the
    /// worst of the three possible behaviours: the run reports the setting, the numbers do not
    /// carry it, and nothing says so.</para>
    /// </summary>
    public static IReadOnlySet<string> SwitchesNotYetApplied { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(TableRules.FriendlyFire),
        nameof(TableRules.SlowHealing),
        nameof(TableRules.RaisedGearLimit),
        nameof(TableRules.GearLimitRank)
    };

    /// <summary>
    /// Every entry of <c>data/rules/play</c> this slice knows about and does not apply, by id.
    ///
    /// <para><b>It is a list in the code because the guide's list is a claim about the code, and a
    /// claim nothing checks is a claim that goes stale.</b> <c>PlayEngineStepTests</c> holds the two
    /// together in both directions — an entry named here and missing from the guide, or named there
    /// and quietly implemented since, fails — and drives every purchase through <see cref="Step"/> to
    /// be sure the ones listed really do refuse and the ones not listed really do not.</para>
    ///
    /// <para><b>It is empty, and it is kept rather than deleted.</b> p.75's three situational
    /// modifiers were the last three on it and are now applied — an <see cref="Attack"/> says what
    /// the target is behind, a <see cref="Combatant"/> says how big they are and whether they can be
    /// seen, and an <see cref="EncounterState"/> says what the light is like. What the field is
    /// worth empty is the guard around it: <c>PlayEngineStepTests</c> requires the guide and the
    /// play policy to agree with it in both directions, so the next entry this engine declines to
    /// apply cannot be added here and left out of the two documents that publish it.</para>
    /// </summary>
    public static IReadOnlySet<string> EntriesNotYetApplied { get; } = new HashSet<string>(StringComparer.Ordinal);

    // ── Beginning ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a fight: the order of action, the GM's pool, and the opening range class.
    ///
    /// <para><b>The Adversity pool is computed from the two entries that print it</b> — one point
    /// per Hero per issue (p.85) plus the scene's Challenge Level times the number of Heroes, whose
    /// factors and operation are read off <c>adversity_earn_challenge_level</c> rather than
    /// multiplied here. Dropping the party size from that factor list gives 2 where the page prints
    /// 8, which is what makes the fixture worth having.</para>
    /// </summary>
    /// <param name="combatants">Everyone in the fight.</param>
    /// <param name="challengeLevel">The scene's Challenge Level, or 0 for a scene without one.</param>
    /// <param name="opening">The range class the GM says the fight opens in.</param>
    /// <param name="visibility">
    /// What the light is like (p.75). <see cref="Encounter.Visibility.Clear"/> by default, which is
    /// no modifier — so a caller who says nothing gets the fight in clear air the guide says every
    /// figure here was measured in. Anything else is announced on page one, the way a table setting
    /// that is on is.
    /// </param>
    public EncounterState Begin(
        IEnumerable<Combatant> combatants,
        int challengeLevel = 0,
        RangeBand opening = RangeBand.Close,
        Visibility visibility = Visibility.Clear)
    {
        ArgumentNullException.ThrowIfNull(combatants);

        var everyone = combatants.ToDictionary(c => c.Id, StringComparer.Ordinal);

        if (everyone.Count == 0)
            throw new ArgumentException("A fight needs somebody in it.", nameof(combatants));

        var lines = new List<LedgerLine>();
        var heroes = everyone.Values.Count(c => c.Kind == CombatantKind.Hero);

        var edges = Edges([.. everyone.Values], lines);
        var adversity = OpeningAdversity(heroes, challengeLevel, lines);

        var ranges = new Dictionary<string, RangeBand>(StringComparer.Ordinal);
        foreach (var a in everyone.Keys)
        {
            foreach (var b in everyone.Keys.Where(b => !string.Equals(a, b, StringComparison.Ordinal)))
                ranges[EncounterState.PairKey(a, b)] = opening;
        }

        foreach (var name in Table.On())
        {
            var setting = TableRules.Switches.First(s => string.Equals(s.Name, name, StringComparison.Ordinal));
            var entry = SourceRefOf(setting);

            lines.Add(new LedgerLine(
                1, "", setting.EntryId, entry,
                SwitchesNotYetApplied.Contains(name)
                    ? $"table setting {name} is on and is not yet implemented — the run does not carry it"
                    : $"table setting {name} is on"));
        }

        // <b>Bad light is announced on page one, for the reason a table setting that is on is.</b>
        // Every roll in this fight is going to be a die or three short of the pool the sheets say,
        // and a reader of the run needs the reason at the top rather than inferred from thirty
        // ledger lines further down.
        if (visibility != Visibility.Clear)
        {
            var light = _play.GetCombat("modifier_visibility");
            var band = VisibilityBand(visibility);

            lines.Add(new LedgerLine(
                1, "", light.Id, light.SourceRef,
                $"the visibility here is {band.Visibility}, which is {Dice(band.Dice)} on "
                + $"{light.Visibility!.Affects} made in it"));
        }

        var order = TurnOrder(everyone, edges, [], [], lines, page: 1);

        return new EncounterState
        {
            Page = 1,
            TurnOrder = order,
            TurnIndex = 0,
            Combatants = everyone,
            Adversity = adversity,
            Effects = [],
            Holds = [],
            Seized = [],
            Grapples = [],
            Ranges = ranges,
            EffectiveEdge = edges,
            ActiveDefencesThisPage = new Dictionary<string, int>(StringComparer.Ordinal),
            DefencesHalved = new Dictionary<string, DefencePenalty>(StringComparer.Ordinal),
            MoveProgress = new Dictionary<string, int>(StringComparer.Ordinal),
            LastAttack = null,
            LosesNextTurn = [],
            TeamAttacked = [],
            Villainy = [],
            Table = Table,
            Visibility = visibility,
            Ledger = new Ledger(lines),
            Over = false
        };
    }

    /// <summary>
    /// The GM's opening pool, out of the data's own factors and operation.
    /// </summary>
    private int OpeningAdversity(int heroes, int challengeLevel, List<LedgerLine> lines)
    {
        var pool = _play.GetResolve("adversity_pool");
        var perHero = pool.Adversity!.PointsPerHeroPerIssue;
        var issue = perHero * heroes;

        lines.Add(new LedgerLine(
            1, "", pool.Id, pool.SourceRef,
            $"the GM opens on {issue} Adversity — {perHero} per Hero, and there are {heroes}"));

        if (challengeLevel <= 0) return issue;

        var level = _play.GetResolve("adversity_earn_challenge_level");
        var factors = level.ChallengeLevel!;

        var award = factors.AwardFactors.Aggregate(
            factors.AwardOperation switch
            {
                "product" => 1,
                "sum" => 0,
                var other => throw new InvalidOperationException(
                    $"adversity_earn_challenge_level's award_operation is '{other}', which this engine cannot apply.")
            },
            (running, factor) =>
            {
                var value = factor switch
                {
                    "challenge_level" => challengeLevel,
                    "hero_count" => heroes,
                    var other => throw new InvalidOperationException(
                        $"adversity_earn_challenge_level names a factor '{other}' this engine cannot supply.")
                };

                return factors.AwardOperation switch
                {
                    "product" => running * value,
                    _ => running + value
                };
            });

        lines.Add(new LedgerLine(
            1, "", level.Id, level.SourceRef,
            $"Challenge Level {challengeLevel} in a party of {heroes} awards {award} Adversity"));

        return issue + award;
    }

    /// <summary>
    /// Each combatant's effective Edge: the derived figure, or the successes of an opening Edge roll
    /// where the table has taken p.73's optional random initiative.
    /// </summary>
    private Dictionary<string, int> Edges(IReadOnlyList<Combatant> everyone, List<LedgerLine> lines)
    {
        var entry = _play.GetCombat("edge_order");
        var edges = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var combatant in everyone)
        {
            if (!Table.RandomInitiative || combatant.Kind == CombatantKind.MinionGroup)
            {
                edges[combatant.Id] = combatant.Edge;
                continue;
            }

            var roll = _counter.Roll(combatant.Edge, _dice);
            edges[combatant.Id] = roll.Successes;

            lines.Add(new LedgerLine(
                1, combatant.Id, entry.Id, entry.SourceRef,
                $"{combatant.Name} rolls {combatant.Edge}d Edge for the order and scores "
                + $"{roll.Successes}, which stands in as their Edge for this battle"));
        }

        // p.79's Drop, applied to whatever the figure above turned out to be — the derived Edge, or
        // the successes of the optional opening roll.
        if (Table.TheDrop) TheDrop([.. everyone], edges, lines);

        return edges;
    }

    /// <summary>
    /// p.79's Drop: <c>the_drop.effect</c> — "the holder's effective Edge is doubled against them"
    /// — applied to everyone the caller says has a weapon or Power aimed and ready.
    ///
    /// <para><b>A doubling that is pairwise in the book is exact as one global order, and that is
    /// the finding that made this rule applicable at all.</b> An order depends only on how each
    /// pair compares, and there are three kinds of pair. Two ready characters both double, and
    /// <c>2a</c> against <c>2b</c> orders exactly as <c>a</c> against <c>b</c> — which is right,
    /// because neither has the drop on the other. Two unready characters double neither, which is
    /// right for the same reason. A ready character against an unready one doubles exactly one of
    /// them, which is the rule as printed. So doubling every holder's <see cref="EncounterState.EffectiveEdge"/>
    /// once produces the same order as comparing every pair under p.79's own sentence, and no case
    /// is left over.</para>
    ///
    /// <para><b>The other half of the rule is pairwise and is <em>not</em> expressible, so it is
    /// named rather than applied.</b> <c>also_held_by</c> gives the drop to a character with a
    /// ranged weapon "against anyone moving up to them to engage them in close combat" — held
    /// against one opponent and not against the rest, which a single order cannot carry: doubled
    /// against one opponent and not another admits cycles, where A beats B, B beats C and C beats A.
    /// The line says so, and <c>docs/guide/play-engine.md</c> records it beside the clauses
    /// <c>team_attacks</c> and <c>minions_attacking</c> leave to a person.</para>
    ///
    /// <para><b>The factor is the printed word and is not in the data</b>, the same shape as
    /// <c>seize_initiative_gm_alternative</c>'s "doubles" and <c>gritty_hard_targets</c>' "doubled":
    /// an entry that has stopped saying it is a rule this engine throws on rather than one it goes
    /// on applying.</para>
    /// </summary>
    private void TheDrop(
        IReadOnlyList<Combatant> everyone, Dictionary<string, int> edges, List<LedgerLine> lines)
    {
        var entry = _play.GetGritty("gritty_the_drop");
        var rule = entry.TheDrop!;

        lines.Add(new LedgerLine(
            1, "", entry.Id, entry.SourceRef,
            $"the other half of p.79's Drop is not applied: it is also held by {rule.AlsoHeldBy}, "
            + "which is a doubling held against one opponent and not against the rest — and one "
            + "order of action cannot carry that, since doubled against one and not another admits "
            + $"a cycle. {rule.FinalSay} has the final say over the whole rule in any case"));

        var holders = everyone.Where(c => c.Ready).ToList();
        var others = everyone.Where(c => !c.Ready).ToList();

        if (holders.Count == 0 || others.Count == 0)
        {
            lines.Add(new LedgerLine(
                1, "", entry.Id, entry.SourceRef,
                $"nobody has the drop on anybody: it is held by {rule.HeldBy}, against "
                + $"{rule.HeldAgainst}, and "
                + (holders.Count == 0
                    ? "nobody here has one aimed and ready"
                    : "everybody here has")));

            return;
        }

        var factor = DropFactor(rule.Effect);
        var minionsHaveAnEdge = _play.GetCombat("edge_ties").TieBreak!.MinionsHaveAnEdge;

        foreach (var holder in holders)
        {
            if (holder.Kind == CombatantKind.MinionGroup && !minionsHaveAnEdge)
            {
                lines.Add(new LedgerLine(
                    1, holder.Id, entry.Id, entry.SourceRef,
                    $"{holder.Name} is ready, and p.73 gives a group of Minions no Edge at all — so "
                    + "there is nothing here to double"));

                continue;
            }

            var was = edges[holder.Id];
            edges[holder.Id] = was * factor;

            lines.Add(new LedgerLine(
                1, holder.Id, entry.Id, entry.SourceRef,
                $"{holder.Name} is {rule.HeldBy}, so they have the drop on {rule.HeldAgainst}: "
                + $"{rule.Effect}, {was} to {edges[holder.Id]}"));
        }
    }

    /// <summary>
    /// What p.79's Drop multiplies an Edge by, read out of <c>the_drop.effect</c>'s printed word.
    /// </summary>
    private static int DropFactor(string effect)
    {
        if (!effect.Contains("double", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"gritty_the_drop's effect now reads '{effect}'. This engine reads the printed word "
                + "\"doubled\" and supplies the factor of 2 itself, because the entry states an "
                + "effect in prose rather than a multiplier; a rule that no longer says it is a "
                + "rule this engine cannot apply. See docs/guide/play-engine.md's readings table.");
        }

        return 2;
    }

    /// <summary>
    /// p.73's order: whoever has seized the initiative first, then Edge downward, then the four-rung
    /// tie-break, and the Minions after everyone else because they have no Edge at all.
    ///
    /// <para>The ladder is the entry's <c>tie_break.order</c> rather than a list written here, so a
    /// reordered table reorders the fight. Where the ladder settles nothing the page says the
    /// characters act simultaneously; this engine has to pick an order to step in and picks the id,
    /// which is stated in the guide as the tie-break of last resort it is.</para>
    /// </summary>
    private List<string> TurnOrder(
        IReadOnlyDictionary<string, Combatant> everyone,
        IReadOnlyDictionary<string, int> edges,
        IReadOnlyList<string> seized,
        IReadOnlyList<string> forfeited,
        List<LedgerLine> lines,
        int page)
    {
        var ties = _play.GetCombat("edge_ties");
        var tieBreak = ties.TieBreak!;

        // <b>The ladder is looked up once rather than once per comparison.</b> It used to be
        // `ladder.ToList().IndexOf(name)` inside the comparator, which allocates a list for every
        // pair the sort looks at.
        var rungs = tieBreak.Order
            .Select((name, index) => (name, index))
            .ToDictionary(rung => rung.name, rung => rung.index, StringComparer.Ordinal);

        // p.73: Minions have no Edge and act after everyone else. Both halves are read rather than
        // assumed, so a corrected entry moves the order with it.
        var minionsLast = !tieBreak.MinionsHaveAnEdge
            && tieBreak.MinionsAct.Contains("after everyone else", StringComparison.Ordinal);

        int Rung(Combatant c)
        {
            // <b>A Minion group is on none of the four rungs, and saying it is on "extras" was a
            // claim the entry does not make.</b> The ladder breaks ties between characters who have
            // an Edge; p.73 says Minions have none. They sort after the whole ladder, which is where
            // the sentence above puts them anyway — this is the same answer, honestly spelled.
            if (c.Kind == CombatantKind.MinionGroup) return rungs.Count;

            var name = c.Kind switch
            {
                CombatantKind.Hero => "heroes",
                CombatantKind.Villain => "villains",
                CombatantKind.Foe => "foes",
                CombatantKind.Extra => "extras",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(c), c.Kind, "No rung of p.73's ladder is written for that kind.")
            };

            return rungs.TryGetValue(name, out var index) ? index : rungs.Count;
        }

        var doubling = Table.GmAlternativeToSeizingInitiative ? GmAlternativeFactor() : 1;

        int EffectiveEdge(Combatant c) =>
            Table.GmAlternativeToSeizingInitiative && seized.Contains(c.Id, StringComparer.Ordinal)
                ? edges[c.Id] * doubling
                : edges[c.Id];

        int Seizing(Combatant c) =>
            !Table.GmAlternativeToSeizingInitiative && seized.Contains(c.Id, StringComparer.Ordinal) ? 0 : 1;

        // p.78's knockback and p.79's luring take a turn away, and the turn is taken away here:
        // a character who forfeited one is not in the order at all, rather than in it with a line
        // beside them saying they are not.
        var order = everyone.Values
            .Where(c => !forfeited.Contains(c.Id, StringComparer.Ordinal))
            .OrderBy(c => minionsLast && c.Kind == CombatantKind.MinionGroup ? 1 : 0)
            .ThenBy(Seizing)
            .ThenByDescending(EffectiveEdge)
            .ThenBy(Rung)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => c.Id)
            .ToList();

        lines.Add(new LedgerLine(
            page, "", ties.Id, ties.SourceRef,
            "the order of action is " + string.Join(", ", order.Select(id => everyone[id].Name))));

        return order;
    }

    /// <summary>
    /// What the GM's alternative multiplies an Edge by.
    ///
    /// <para><b>The factor is not in the data and cannot be, because the entry's effect is a
    /// sentence.</b> <c>seize_initiative_gm_alternative.gm_alternative.effect</c> reads "doubles the
    /// buyer's effective Edge" — a printed word, not a number — so this engine reads the word and
    /// supplies the arithmetic, which is a reading and is recorded as one in
    /// <c>docs/guide/play-engine.md</c>. It throws rather than defaulting if the entry stops saying
    /// it: a silent fallback to 2 against an entry that had been corrected to say something else
    /// would apply a rule the book no longer prints, which is exactly the failure the store exists
    /// to prevent.</para>
    /// </summary>
    private int GmAlternativeFactor()
    {
        var entry = _play.GetCombat("seize_initiative_gm_alternative");
        var effect = entry.GmAlternative!.Effect;

        if (!effect.Contains("double", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"seize_initiative_gm_alternative's effect now reads '{effect}'. This engine reads "
                + "the printed word \"doubles\" and supplies the factor of 2 itself, because the "
                + "entry states an effect in prose rather than a multiplier; a rule that no longer "
                + "says \"doubles\" is a rule this engine cannot apply. See "
                + "docs/guide/play-engine.md's readings table.");
        }

        return 2;
    }

    private string SourceRefOf(TableSwitch setting) => setting.File switch
    {
        PlayRulesRepository.GrittyFile => _play.GetGritty(setting.EntryId).SourceRef,
        PlayRulesRepository.CombatFile => _play.GetCombat(setting.EntryId).SourceRef,
        PlayRulesRepository.ChallengeFile => _play.GetChallenge(setting.EntryId).SourceRef,
        var other => throw new InvalidOperationException($"No table setting is printed in {other}.")
    };
}
