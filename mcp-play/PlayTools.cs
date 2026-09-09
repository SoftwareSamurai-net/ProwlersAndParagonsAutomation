using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.McpPlay;

/// <summary>
/// The four tools, and the reasoning behind there being four.
///
/// <para><b>They were chosen by asking what running a fight needs.</b> Somebody has to be taught
/// the one rule that makes this worth anything — the engine resolves and the model narrates — set
/// a fight up, take a turn, and measure a matchup over enough runs to mean something. That is what
/// is here and nothing else is.</para>
///
/// <para><b>Acting and rolling are one tool, deliberately.</b> The obvious surface is
/// <c>declare_intent</c> beside <c>roll</c>, which is the engine's internals rather than the
/// conversation's: an intent is a <em>request</em>, and the whole discipline of the second engine
/// is that the engine decides what a request produces. A separate rolling call would be an
/// invitation to declare an attack, look at the dice, and decide afterwards what was being
/// attempted.</para>
///
/// <para><b>Nothing here counts a success, rolls a die or decides an outcome.</b> Every figure
/// comes back from <see cref="Encounter"/>, and every ledger line carries the id of the rule that
/// produced it and that rule's printed page. See <c>PLAY-POLICY.md</c>, which is the payload of
/// <see cref="CombatGuide"/>.</para>
/// </summary>
public sealed class PlayTools
{
    private readonly RulesRepository _rules;
    private readonly DerivedStatsCalculator _derived;
    private readonly PlayRulesRepository _play;
    private readonly Func<string> _guide;
    private readonly Action<string>? _midTurn;

    /// <summary>
    /// Every encounter this session has started, by id.
    ///
    /// <para><b>In memory and in this process only, which is the honest scope.</b> A fight is a
    /// conversation's working state; persisting it would make this server a store, and a store
    /// wants a lifetime, an eviction rule and somebody's disk. A client that loses the session
    /// starts the fight again from the same seed and gets the same fight.</para>
    ///
    /// <para><b>Concurrent because a client may have two calls in flight — and that on its own was
    /// not enough.</b> An earlier version of this comment stopped at the sentence above and said the
    /// immutability of <see cref="EncounterState"/> made it safe. It does not: taking a turn is
    /// <em>read the held state, step it, write the result back</em>, and two of those overlapping on
    /// one encounter both read the same state and the second write silently discards the first
    /// turn. Both callers get an <c>ok: true</c> and a ledger, and one of the two turns simply never
    /// happened — a fight quietly missing a page of itself is the worst possible failure for a
    /// server whose whole product is a record you can trust. What immutability buys is that the
    /// discarded turn cannot corrupt the surviving one; it does not buy the turn.</para>
    ///
    /// <para>So each fight carries its own gate and the read-modify-write happens under it. Per
    /// encounter, not one lock for the server: two clients running two fights have nothing to
    /// serialise, and a single lock would make the second wait on the first for no reason.</para>
    /// </summary>
    private readonly ConcurrentDictionary<string, Held> _encounters =
        new(StringComparer.Ordinal);

    private int _nextEncounter;

    /// <summary>One fight in progress: the engine that is stepping it, and where it has got to.</summary>
    /// <param name="Gate">
    /// The one-at-a-time lock for this fight's read-modify-write. It is a positional parameter so
    /// that <c>held with { State = … }</c> carries the <em>same</em> gate forward: a gate rebuilt on
    /// every step would be a lock nobody else is holding, which is no lock at all. Never disposed,
    /// deliberately — an encounter lives as long as the session and a disposed gate is a fight that
    /// throws on its next turn.
    /// </param>
    private sealed record Held(
        Encounter Engine, EncounterState State, int Seed, int ChallengeLevel, SemaphoreSlim Gate);

    /// <param name="rules">The character rules, for turning a sheet into a combatant.</param>
    /// <param name="derived">The character engine's Edge, Health, Resolve and effective ranks.</param>
    /// <param name="play">The play rules, which is what resolves the fight.</param>
    /// <param name="guide">
    /// The play policy. Injectable for the reason <c>CharacterTools</c>' is: it is an embedded
    /// resource, so the way it goes missing is a csproj edit, and reading it in
    /// <see cref="ReadEverything"/> is what makes that a refusal at startup rather than a
    /// conversation that opens with an empty document — a claim that can only be driven if the
    /// read can be made to fail.
    /// </param>
    /// <param name="midTurn">
    /// Called inside a fight's gate, with the encounter's id, once the held state has been read
    /// and before the step that replaces it is written back — <b>the one seam this class has, and
    /// it exists because the race it guards cannot otherwise be driven.</b>
    ///
    /// <para>The lost update <see cref="_encounters"/> documents lives in the handful of
    /// microseconds between that read and that write. A test that fires two <c>take_turn</c>s and
    /// hopes they overlap reproduces it about one run in five — measured, not guessed — which is a
    /// guard that reports green on a server that has lost a turn. Holding the first turn here
    /// makes the window as wide as the test wants it, so the second turn either gets in (no gate,
    /// and both turns write the same page) or waits (gate, and the two turns land in order). No
    /// clock is involved in either answer.</para>
    ///
    /// <para>Null in every host: <see cref="PlayServer.ToolsFor"/> does not pass it and there is
    /// no argument, environment variable or file that turns it on.</para>
    /// </param>
    public PlayTools(
        RulesRepository rules,
        DerivedStatsCalculator derived,
        PlayRulesRepository play,
        Func<string>? guide = null,
        Action<string>? midTurn = null)
    {
        _rules   = rules;
        _derived = derived;
        _play    = play;
        _guide   = guide ?? (() => PlayPolicy.Text);
        _midTurn = midTurn;
    }

    /// <summary>
    /// Reads everything the tools will need — <b>both stores, every file of each</b> — so that a
    /// bad or partial rules directory is a refusal at startup rather than an error on every
    /// question.
    ///
    /// <para><b>Every file, not one.</b> Both repositories load lazily, so warming the tiers alone
    /// let a directory holding a single file start cleanly and then throw out of most of the
    /// tools — the exact failure this exists to prevent, passing its own check. That was the
    /// character server's history and there is no reason to repeat it here, with twice as many
    /// files to be missing. The guide is read too, because a csproj edit that stopped embedding it
    /// is otherwise a conversation that opens with an empty document.</para>
    /// </summary>
    /// <exception cref="Exception">Whatever reading the rules threw. The caller reports it and
    /// exits; there is nothing this class can do about it.</exception>
    public void ReadEverything()
    {
        // The character rules: one touch per file in RulesRepository.DataFileNames, so a file
        // that is missing or malformed is found here rather than on the first combatant.
        _ = _rules.Tiers.Count;
        _ = _rules.Abilities.Count;
        _ = _rules.Talents.Count;
        _ = _rules.Powers.Count;
        _ = _rules.Pros.Count;
        _ = _rules.Cons.Count;
        _ = _rules.Flaws.Count;
        _ = _rules.Perks.Count;
        _ = _rules.GearFeatures.Count;
        _ = _rules.Sources.Count;
        _ = _rules.CreationRules.TraitRankLimits.Minimum;

        // The play rules: all five, through the property that loads each file.
        _ = _play.Meta.Entries.Count;
        _ = _play.Challenge.Entries.Count;
        _ = _play.Combat.Entries.Count;
        _ = _play.Gritty.Entries.Count;
        _ = _play.Resolve.Entries.Count;

        _ = _guide().Length;
    }

    // ── The guide ─────────────────────────────────────────────────────────

    /// <summary>
    /// The play policy, verbatim from the file. One copy: a paraphrase in a string literal here
    /// would drift from the document the next person reads, and the drift would be invisible.
    /// </summary>
    [Description(
        "How to run a fight through this server: that the engine resolves and you narrate, what "
        + "you may never state without a ledger line behind it, who holds Resolve and who holds "
        + "Adversity, what a measurement has to be quoted with, and what this engine does not yet "
        + "model. Read this before starting an encounter.")]
    public string CombatGuide() => _guide();

    // ── Starting a fight ──────────────────────────────────────────────────

    [Description(
        "Opens a fight and holds it by id for this session. Combatants are character sheets — the "
        + "shape the character server's creation_guide describes — each with a kind (hero, "
        + "villain, foe, extra) and a side, or a group of Minions with a threat_rank and a count. "
        + "Answers with the encounter id, the order of action with each combatant's Edge, the GM's "
        + "opening Adversity pool, and the table settings echoed back.")]
    public string StartEncounter(
        [Description(
            "The fight, as a JSON array. Each entry: {\"kind\": \"hero\"|\"villain\"|\"foe\"|"
            + "\"extra\", \"character\": {…the character's inputs…}, \"side\": \"heroes\", "
            + "\"id\": \"optional\"} — or {\"kind\": \"minions\", \"name\": \"the robots\", "
            + "\"threat_rank\": 6, \"count\": 4, \"side\": \"villains\"}. kind and side are yours "
            + "to say: nothing derives them from the sheet. Either shape may also carry \"size\", "
            + "\"invisible\", \"hard_target\" — a machine, vehicle or thick object, whose "
            + "passive defences double while the hard_targets setting is on — \"ready\", a "
            + "weapon or Power aimed and ready, which under the_drop doubles their Edge for the "
            + "order of action, and \"holding\", the one handheld item this combatant walks in "
            + "with. That last is what p.76's grab is aimed at: a grab for an item its target is "
            + "not holding is refused with nothing rolled, so say what a character is carrying "
            + "here or nobody can be disarmed of it.")]
        JsonElement combatants,
        [Description(
            "The switches this table threw before play, as a JSON object of booleans — "
            + "active_defenses_cost, fatal_damage, tough_minions, wound_penalties, "
            + "checking_your_swing, random_initiative and the rest. Omit for the book's baseline, "
            + "which is every optional Gritty rule off.")]
        JsonElement? table = null,
        [Description("The scene's Challenge Level, which adds to the GM's Adversity pool. 0 by default.")]
        int? challengeLevel = null,
        [Description("The seed the dice are drawn from. The same seed gives the same fight. 0 by default.")]
        int? seed = null,
        [Description("The range class the fight opens in: close, distant or extreme. Close by default.")]
        string? openingRange = null,
        [Description(
            "What the light is like in this scene, which Ch.4 p.75 costs on attack rolls and on "
            + "active defence rolls alike: clear, poor or none. Clear by default, which is no "
            + "modifier at all.")]
        string? visibility = null)
    {
        if (!TryReadSetup(combatants, table, challengeLevel, seed, openingRange, visibility, out var setup, out var problem))
            return Write(problem);

        var engine = new Encounter(_play, new SeededDice(setup.Seed), setup.Table);

        EncounterState state;
        try
        {
            state = engine.Begin(setup.Combatants, setup.ChallengeLevel, setup.Opening, setup.Visibility);
        }
        catch (Exception e) when (IsCallersFault(e))
        {
            return Write(Problem("ENCOUNTER_WOULD_NOT_OPEN", e.Message));
        }

        var id = $"enc_{Interlocked.Increment(ref _nextEncounter)}";
        _encounters[id] = new Held(engine, state, setup.Seed, setup.ChallengeLevel, new SemaphoreSlim(1, 1));

        return Write(new JsonObject
        {
            ["ok"]              = true,
            ["encounter_id"]    = id,
            ["seed"]            = setup.Seed,
            ["challenge_level"] = setup.ChallengeLevel,
            ["opening_range"]   = Wire(setup.Opening.ToString()),
            ["adversity"]       = state.Adversity,
            ["turn_order"]      = TurnOrder(state),
            ["table"]           = TableEcho(setup.Table, setup.Visibility, setup.Source, setup.SourceNote),
            ["ledger"]          = Lines([TableSourceLine(setup), .. TierLines(setup), .. state.Ledger.Lines])
        });
    }

    // ── Taking a turn ─────────────────────────────────────────────────────

    [Description(
        "Takes ONE turn: an encounter id and one intent. Acting and rolling are the same call — "
        + "an intent is a request and the engine decides what it produces. Answers with the ledger "
        + "lines this step added, each naming the rule it applied and the page it is printed on, "
        + "and the public state afterwards. Never state a success count, a damage figure or an "
        + "outcome that is not in a line this returned.")]
    public string TakeTurn(
        [Description("The id start_encounter answered with.")]
        string? encounterId,
        [Description(
            "One intent, as a JSON object with a \"kind\" of: attack, move, hold, grapple, toss, "
            + "break_free, spend_resolve, spend_adversity, stabilise, end_turn, end_page. "
            + "For example {\"kind\": \"attack\", \"actor\": \"gatecrasher\", \"target\": "
            + "\"mecha\", \"trait_id\": \"blast\", \"type\": \"physical_power\"}. An attack also "
            + "takes \"cover\" (none, light, heavy, almost_full, complete) and, where the shot goes "
            + "through the obstacle rather than at the exposed part of the target, "
            + "\"cover_structure\" — the obstacle's Structure rank — or \"cover_scenery\", "
            + "which names the obstacle instead and takes its Structure off Chapter 7's own "
            + "tables; pass one or the other, never both. \"vulnerable_part\", "
            + "which under the hard_targets setting costs four dice and cancels the target's "
            + "doubled passive defence, and \"close_range_only\" for an ordinary thrown weapon or "
            + "anything else that only works up close, which the close_range_penalty setting "
            + "ignores. A grapple with \"move\": \"grab\" needs an \"item\" — p.76 aims a grab at "
            + "an object and a hold at a person, so a grab naming nothing is refused with nothing "
            + "rolled, and so is a grab for an item the target is not holding. A full grab puts "
            + "that item in the winner's hands for the page: an attack may name it as \"item\" "
            + "(which keeps it past the page turn), \"toss\" throws it away, and neither spends "
            + "the turn. Anything else a grab won and nobody swung is tossed aside when the page "
            + "ends. An attack that names an \"item\" on the \"melee_weapon\" or "
            + "\"ranged_weapon\" row is capped at the Gear Limit (6d unless the table raised it) "
            + "and then collects the weapon's own bonus dice, so a 10d Might swinging a sword "
            + "rolls 8d; a Power row and \"unarmed\" are never capped, and \"unarmed\" with the "
            + "weapon still in hand is how p.87's close-combat exception is taken. An \"item\" "
            + "Chapter 6 prints no weapon for and Chapter 7 does rate is an improvised weapon "
            + "under p.108 instead: a die for swinging it, a die for throwing it, and an attack "
            + "rank capped at the object's own rank plus six. A \"spend_resolve\" of "
            + "\"knockback\" also takes a \"solid_object\" — what the target is thrown into, "
            + "which costs them half the blow again unless their passive defence beats its "
            + "Structure. **Every one of those names, and every \"cover_scenery\", is a printed "
            + "row of Chapter 7's three object tables — `smashing_table` (materials, p.107), "
            + "`scenery_table` (things, p.108) and `massive_objects_table` (p.108); combat_guide "
            + "lists all of them.** A name none of the three prints is refused with nothing "
            + "rolled and nothing spent, rather than being given a figure no page carries.")]
        JsonElement intent)
    {
        if (string.IsNullOrWhiteSpace(encounterId) || !_encounters.TryGetValue(encounterId, out var held))
        {
            return Write(Problem("NO_SUCH_ENCOUNTER",
                $"No encounter '{encounterId}' is running here. Encounters live in this server's "
                + "memory for the length of the session; start one with start_encounter. "
                + (_encounters.IsEmpty
                    ? "None is running."
                    : "Running: " + string.Join(", ", _encounters.Keys.Order(StringComparer.Ordinal)))));
        }

        if (!TryReadIntent(intent, out var read, out var problem)) return Write(problem);

        // <b>Read, step and write back under this fight's own gate.</b> Without it the three are a
        // classic lost update: two overlapping turns read the same state, both step it, and the
        // second write discards the first turn while its caller is told `ok: true` and handed a
        // ledger. A fight quietly missing a page of itself is the worst failure available to a
        // server whose whole product is a record you can trust. `EncounterState` being immutable
        // stops the discarded turn corrupting the surviving one; it does not stop the discard.
        // Held in a local, because the `Held` record is replaced on every step and the gate released
        // in the `finally` has to be the one that was taken. The `with` below carries the same
        // instance forward, so a second lookup would find the same gate — the local says that is
        // relied on rather than hoped for.
        var gate = held.Gate;

        gate.Wait();

        try
        {
            // Re-read inside the gate: the lookup above happened outside it, and a turn that was
            // queued ahead of this one has replaced what it found. Indexed rather than tried,
            // because nothing anywhere removes an encounter — the dictionary only ever grows, so an
            // id that was present before the wait is present after it. If that ever stops being
            // true (the eviction rule the field's own comment says a store would want), this is the
            // line that has to grow a refusal rather than throw.
            held = _encounters[encounterId];

            // The seam, held open by a test so that two turns really do overlap. Null in every
            // host — see the constructor.
            _midTurn?.Invoke(encounterId);

            // <b>A fight that is over takes no more turns.</b> `Over` means one side has nobody
            // standing, and the engine went on stepping past it: defeated combatants kept being
            // rolled for, the page count kept climbing, and the ledger filled with lines about a
            // fight already decided. Every one of those lines is a real citation of a real rule, so
            // nothing in the answer tells a reader they are looking at the aftermath — which is the
            // one thing a ledger exists to make impossible. Checked in here, because "is it over"
            // is a read of the same state the step is about.
            if (held.State.Over)
            {
                return Write(Problem("ENCOUNTER_OVER",
                    $"'{encounterId}' is over: one side has nobody left standing, and the state it "
                    + "answered with last says so in \"over\". Taking another turn would add ledger "
                    + "lines about a fight that is already decided, and they would look exactly like "
                    + "the ones that decided it. Start another fight with start_encounter, or measure "
                    + "the matchup with run_encounters."));
            }

            StepResult step;
            try
            {
                step = held.Engine.Step(held.State, read);
            }
            // The engine throws for a request that names somebody who is not in the fight, or charges
            // a pool a combatant does not hold. Both are the caller's, both are recoverable, and both
            // arrive as a protocol error a model cannot act on if they are not caught here.
            catch (Exception e) when (IsCallersFault(e))
            {
                return Write(Problem("INTENT_REFUSED", e.Message));
            }

            _encounters[encounterId] = held with { State = step.State };

            return Write(new JsonObject
            {
                ["ok"]           = true,
                ["encounter_id"] = encounterId,
                ["added"]        = Lines(step.Added),
                ["state"]        = PublicState(held, step.State)
            });
        }
        finally
        {
            gate.Release();
        }
    }

    // ── Measuring ─────────────────────────────────────────────────────────

    /// <summary>
    /// The fewest runs this tool will answer with.
    ///
    /// <para><b>It refuses rather than answering with a caveat.</b> A win rate off five fights is
    /// noise wearing a percentage sign, and a caveat beside a number is read by nobody — the number
    /// is what gets quoted. Thirty is the conventional floor for a proportion to be worth
    /// reporting at all; it is not a claim that thirty is enough for any particular question.</para>
    /// </summary>
    public const int FewestRuns = 30;

    /// <summary>
    /// The most, so that a typo cannot hang somebody's client.
    ///
    /// <para>A thousand runs of the book's own example fight is a couple of seconds; a hundred
    /// thousand is a session that never answers, over a transport with no progress and no cancel.
    /// </para>
    /// </summary>
    public const int MostRuns = 5_000;

    /// <summary>
    /// The named policies this server will run besides the styles, by the name a caller passes.
    ///
    /// <para><b>It is one entry and it is kept.</b> <c>attack_the_weakest</c> is the policy this
    /// server shipped with and every figure in <c>PROGRESS.md</c>'s first balance measurement was
    /// taken under it — so removing the name would make that measurement unreproducible from its own
    /// echo, which is the one thing a report here exists to prevent. New work uses
    /// <c>style</c> and <c>targeting</c>.</para>
    /// </summary>
    public static IReadOnlyList<string> Policies { get; } = ["attack_the_weakest"];

    /// <summary>
    /// The fewest runs a fairness verdict is allowed off, which is the owner's line and not a rule.
    ///
    /// <para>The rest of this tool answers from 30 up, because a rate is worth reporting there. A
    /// verdict is a different claim: <c>unfair</c> says a side loses at least half the time, and the
    /// owner set both halves of that — a hundred fights or more, and a win rate at or below one in
    /// two. Below the floor the flag is <c>null</c> rather than <c>false</c>, because "not unfair"
    /// and "not enough fights to say" are different answers and only one of them is reassuring.
    /// </para>
    /// </summary>
    public const int FewestRunsForAVerdict = 100;

    /// <summary>The win rate at or below which a side's matchup is flagged. The owner's figure.</summary>
    public const double UnfairAtOrBelow = 0.5;

    /// <summary>What the flag means, in the answer, every time it appears.</summary>
    private const string UnfairNote =
        "unfair is a threshold the owner set and not a rule the book prints: a side that wins half "
        + "its fights or fewer over at least "
        + "100 runs. It is null below that many runs, because "
        + "\"not unfair\" and \"not enough fights to say\" are different answers. A draw is a win "
        + "for neither side, so it counts against both — read the flag beside draw_rate, because a "
        + "fight nobody ever wins is flagged against each side and that is not the other one "
        + "beating them.";

    [Description(
        "Runs the same fight N times on consecutive seeds and reports the rates. Refuses fewer "
        + "than 30 runs. The answer carries N, the seeds, the policy and the table settings in the "
        + "same object as the figures, because a balance figure quoted without them is a figure "
        + "about no particular game — quote all four or quote none of it.")]
    public string RunEncounters(
        [Description("The fight, in the same shape start_encounter takes.")]
        JsonElement combatants,
        [Description(
            "How many fights to run. At least 30, at most 5000 — this is the N a rate off this "
            + "tool may never be quoted without, so it is asked for rather than defaulted.")]
        int? runs,
        [Description("The table's switches, in the same shape start_encounter takes.")]
        JsonElement? table = null,
        [Description("The scene's Challenge Level. 0 by default.")]
        int? challengeLevel = null,
        [Description("The first seed. The runs use seed, seed+1, … so the whole call reproduces.")]
        int? seed = null,
        [Description("The range class the fight opens in: close, distant or extreme. Close by default.")]
        string? openingRange = null,
        [Description(
            "The style both sides play in: mano_a_mano, standard, min_max or reckless. Standard by "
            + "default. A style is a guess about how people play and never a rule, so its note "
            + "comes back beside the figures. \"narrative\" is refused by name — it needs a model "
            + "as the Villain, through take_turn.")]
        string? style = null,
        [Description(
            "Which opponent each side goes after: weakest, strongest or highest_threat. Weakest by "
            + "default. An axis of its own, independent of the style.")]
        string? targeting = null,
        [Description(
            "The original policy this server shipped with, attack_the_weakest — kept so the "
            + "measurements taken under it stay reproducible. Given, it overrides style and "
            + "targeting; leave it out and use style instead.")]
        string? policy = null,
        [Description("The page each run stops at if neither side is down. 20 by default.")]
        int? maxPages = null,
        [Description(
            "What the light is like, in the same shape start_encounter takes: clear, poor or none. "
            + "Clear by default. It is echoed back inside \"table\", because a rate measured in the "
            + "dark is a rate about a different game.")]
        string? visibility = null)
    {
        var n = runs ?? 0;

        if (n < FewestRuns)
        {
            return Write(Problem("TOO_FEW_RUNS",
                $"{n} runs is refused: this tool answers with rates, and a rate off fewer than "
                + $"{FewestRuns} fights is noise wearing a percentage sign. Ask for at least "
                + $"{FewestRuns}."));
        }

        if (n > MostRuns)
        {
            return Write(Problem("TOO_MANY_RUNS",
                $"{n} runs is refused: at most {MostRuns}. There is no progress and no cancel over "
                + "this transport, so a run that takes minutes is a session that looks broken."));
        }

        var pages = maxPages ?? 20;

        if (pages < 1)
            return Write(Problem("BAD_PAGE_LIMIT", $"A run has to be allowed at least one page, and {pages} is not."));

        if (policy is not null)
        {
            var wanted = policy.Trim().ToLowerInvariant();

            if (!Policies.Contains(wanted, StringComparer.Ordinal))
            {
                return Write(Problem("NO_SUCH_POLICY",
                    $"'{policy}' is not a policy this server has. Available: "
                    + string.Join(", ", Policies)
                    + ". A policy is a guess about how people play, not a rule — its name goes in "
                    + "the answer for that reason. The styles are asked for with \"style\" "
                    + "instead: " + string.Join(", ", StylePolicy.StyleIds) + "."));
            }
        }

        if (!TryReadStyle(style, targeting, out var chosenStyle, out var chosenTargeting, out var named))
            return Write(named);

        if (!TryReadSetup(combatants, table, challengeLevel, seed, openingRange, visibility, out var setup, out var problem))
            return Write(problem);

        // <b>The runs are consecutive seeds, so the last one has to be a seed.</b> `seed + runs - 1`
        // is int arithmetic and it is unchecked: a first seed near int.MaxValue wrapped past it, the
        // report printed a `last` seed *below* its `first`, and the runs themselves were taken on
        // seeds that ran off the top and came back round — every one of them a real fight, none of
        // them the fight the caller asked for, and the whole answer reproducible only by somebody
        // who repeated the overflow. Computed in long here so the check cannot be the thing that
        // overflows.
        var last = (long)setup.Seed + n - 1;

        if (last > int.MaxValue)
        {
            return Write(Problem("SEED_RANGE",
                $"{n} runs from the seed {setup.Seed} would end at {last}, which is past the "
                + $"largest seed there is ({int.MaxValue}). The runs are consecutive seeds and the "
                + "report prints the last one, so this would answer with a seed range that does not "
                + "reproduce it. Start lower, or ask for fewer runs."));
        }

        // Typed as the interface deliberately: a policy is a seam, and every report here
        // prints the seam's own Name rather than a class name written down beside it.
        IPolicy chooser = policy is null
            ? StylePolicy.For(chosenStyle, chosenTargeting, _play)
            : new AttackTheWeakest(_play);

        try
        {
            return Write(Measure(setup, chooser, n, pages));
        }
        catch (Exception e) when (IsCallersFault(e))
        {
            return Write(Problem("RUN_REFUSED", e.Message));
        }
    }

    /// <summary>
    /// The style and the target selector a caller asked for, or a refusal naming what is available.
    ///
    /// <para><b>The owner's fifth style is refused by its own name rather than falling into "no such
    /// style".</b> <c>narrative</c> is a real way of playing and its absence is a decision, not an
    /// omission: a seed cannot fake a Villain acting befitting their character, because nothing in
    /// this engine makes a Flaw bite and "befitting" is a judgement. A caller who asks for it is
    /// told where it lives — <c>take_turn</c>, with the model as the Villain — so nobody reads the
    /// gap as an oversight and nobody builds a fake one.</para>
    /// </summary>
    private static bool TryReadStyle(
        string? style, string? targeting,
        out PlayStyle chosen, out Targeting selector, out JsonObject problem)
    {
        chosen = PlayStyle.Standard;
        selector = Targeting.Weakest;
        problem = new JsonObject();

        var wantedStyle = (style ?? StylePolicy.WireOf(PlayStyle.Standard)).Trim().ToLowerInvariant();

        if (string.Equals(wantedStyle, StylePolicy.NarrativeStyle, StringComparison.Ordinal))
        {
            problem = Problem("NARRATIVE_IS_NOT_SEEDED", StylePolicy.NarrativeIsNotSeeded);
            return false;
        }

        if (!StylePolicy.TryReadStyle(wantedStyle, out chosen))
        {
            problem = Problem("NO_SUCH_STYLE",
                $"'{style}' is not a style this server runs. Available: "
                + string.Join(", ", StylePolicy.StyleIds)
                + ". A style is a guess about how people play and not a rule, which is why its note "
                + "comes back beside every figure measured under it.");
            return false;
        }

        var wantedTargeting =
            (targeting ?? StylePolicy.WireOf(Targeting.Weakest)).Trim().ToLowerInvariant();

        if (StylePolicy.TryReadTargeting(wantedTargeting, out selector)) return true;

        problem = Problem("NO_SUCH_TARGETING",
            $"'{targeting}' is not a way of choosing a target here. Available: "
            + string.Join(", ", StylePolicy.TargetingIds)
            + ". It is an axis of its own, so any of them composes with any style.");

        return false;
    }

    // ── The matrix ────────────────────────────────────────────────────────

    /// <summary>
    /// The fewest runs a matrix cell may be measured over — the owner's floor, and the same one a
    /// fairness verdict needs, because every cell carries one.
    /// </summary>
    public const int FewestRunsACell = FewestRunsForAVerdict;

    /// <summary>Which matchups a matrix is built out of, by the name a caller passes.</summary>
    public static IReadOnlyList<string> Matchups { get; } = ["all", "party", "each_hero_alone"];

    [Description(
        "Runs the same fight at N per cell across every simulated style and every matchup — the "
        + "whole party against the opposition, and each Hero alone against it — and answers one "
        + "table: rows are matchups, columns are styles, cells are a win rate, an unfair flag and "
        + "the mean pages. Refuses fewer than 100 runs a cell, which is the owner's floor for "
        + "saying a fight is unfair at all. Every cell's seeds are derived from the one base seed "
        + "and echoed, so the whole table reproduces.")]
    public string RunMatrix(
        [Description("The fight, in the same shape start_encounter takes.")]
        JsonElement combatants,
        [Description("How many fights per cell. At least 100. 100 by default.")]
        int? runs = null,
        [Description(
            "Which rows: all (the default), party, or each_hero_alone. A Hero alone is that Hero "
            + "against everybody not on their side.")]
        string? matchups = null,
        [Description(
            "Which opponent each side goes after in every cell: weakest, strongest or "
            + "highest_threat. Weakest by default. The styles are the columns and are not chosen.")]
        string? targeting = null,
        [Description("The table's switches, in the same shape start_encounter takes.")]
        JsonElement? table = null,
        [Description("The scene's Challenge Level. 0 by default.")]
        int? challengeLevel = null,
        [Description(
            "The base seed. Each cell takes a block of consecutive seeds derived from it, in the "
            + "order the rows and columns are listed, and every block is echoed.")]
        int? seed = null,
        [Description("The range class every fight opens in: close, distant or extreme.")]
        string? openingRange = null,
        [Description("What the light is like: clear, poor or none. Echoed inside \"table\".")]
        string? visibility = null,
        [Description("The page each run stops at if neither side is down. 20 by default.")]
        int? maxPages = null)
    {
        var perCell = runs ?? FewestRunsACell;

        if (perCell < FewestRunsACell)
        {
            return Write(Problem("TOO_FEW_RUNS_A_CELL",
                $"{perCell} runs a cell is refused: every cell of this table carries a fairness "
                + $"verdict, and the owner's line for one is at least {FewestRunsACell} fights. "
                + "run_encounters will answer a bare rate from 30, without the verdict."));
        }

        var pages = maxPages ?? 20;

        if (pages < 1)
            return Write(Problem("BAD_PAGE_LIMIT", $"A run has to be allowed at least one page, and {pages} is not."));

        if (!TryReadStyle(null, targeting, out _, out var selector, out var named))
            return Write(named);

        var wantedRows = (matchups ?? Matchups[0]).Trim().ToLowerInvariant();

        if (!Matchups.Contains(wantedRows, StringComparer.Ordinal))
        {
            return Write(Problem("NO_SUCH_MATCHUP",
                $"'{matchups}' is not a set of rows this tool builds. Available: "
                + string.Join(", ", Matchups) + "."));
        }

        if (!TryReadSetup(combatants, table, challengeLevel, seed, openingRange, visibility, out var setup, out var problem))
            return Write(problem);

        var heroes = setup.Combatants.Where(c => c.Kind == CombatantKind.Hero).ToList();

        if (heroes.Count == 0)
        {
            return Write(Problem("NO_PARTY",
                "A matrix is the party against the opposition and each Hero alone against it, and "
                + "no combatant here has a \"kind\" of hero. The kind is yours to say and nothing "
                + "derives it — the Hero/Villain flag on a sheet is presentation — so say which of "
                + "these characters is the party, or measure the one matchup with run_encounters."));
        }

        var partySide = heroes[0].Side;
        var opposition = setup.Combatants
            .Where(c => !string.Equals(c.Side, partySide, StringComparison.Ordinal))
            .ToList();

        // <b>There is deliberately no "nobody to fight" refusal here.</b> `TryReadSetup` has already
        // refused a fight whose combatants all share one side, and the party's side is taken off a
        // Hero who is in this fight — so a set with two sides and a Hero on one of them always has
        // somebody on the other. A guard no call can reach is worse than none.

        var rows = Rows(wantedRows, setup, heroes, partySide, opposition);
        var styles = Enum.GetValues<PlayStyle>();
        var cells = rows.Count * styles.Length;
        var total = (long)cells * perCell;

        // <b>The whole table is capped and not each cell.</b> There is no progress and no cancel
        // over this transport, so what matters is how long the one call takes; a cap per cell would
        // let twenty of them add up to the session that looks broken.
        if (total > MostRuns)
        {
            return Write(Problem("MATRIX_TOO_LARGE",
                $"{rows.Count} matchups times {styles.Length} styles is {cells} cells, and at "
                + $"{perCell} runs each that is {total} fights — more than the {MostRuns} this "
                + "transport will answer in one call. Ask for fewer runs a cell, or narrow the "
                + "rows with \"matchups\"."));
        }

        // <b>Consecutive blocks, so the whole matrix is one contiguous seed range.</b> Every cell
        // reproduces on its own, no two cells share a fight, and a reader with the base seed and
        // the row and column order can rebuild any cell of it — which is what an echoed seed is
        // for.
        var last = (long)setup.Seed + total - 1;

        if (last > int.MaxValue)
        {
            return Write(Problem("SEED_RANGE",
                $"{total} fights from the seed {setup.Seed} would end at {last}, which is past the "
                + $"largest seed there is ({int.MaxValue}). Start lower, or ask for fewer runs."));
        }

        var matrix = new JsonArray();
        var index = 0;

        try
        {
            foreach (var row in rows)
            {
                var cellsOut = new JsonArray();

                foreach (var style in styles)
                {
                    var cellSeed = setup.Seed + index * perCell;
                    index++;

                    var policy = StylePolicy.For(style, selector, _play);
                    var measured = Cell(setup with { Combatants = row.Fight, Seed = cellSeed },
                        policy, perCell, pages, partySide);

                    cellsOut.Add(new JsonObject
                    {
                        ["style"]      = StylePolicy.WireOf(style),
                        ["win_rate"]   = Rate(measured.Wins, perCell),
                        ["unfair"]     = Unfair(measured.Wins, perCell),
                        ["draw_rate"]  = Rate(measured.Draws, perCell),
                        ["mean_pages"] = Mean(measured.Pages, perCell),
                        ["seeds"]      = new JsonObject
                        {
                            ["first"] = cellSeed,
                            ["last"]  = cellSeed + perCell - 1
                        }
                    });
                }

                matrix.Add(new JsonObject
                {
                    ["matchup"]    = row.Id,
                    ["combatants"] = Strings(row.Fight.Select(c => c.Id)),
                    ["cells"]      = cellsOut
                });
            }
        }
        catch (Exception e) when (IsCallersFault(e))
        {
            return Write(Problem("RUN_REFUSED", e.Message));
        }

        return Write(new JsonObject
        {
            ["ok"]            = true,
            ["runs_a_cell"]   = perCell,
            ["cells"]         = cells,
            ["total_runs"]    = total,
            ["side"]          = partySide,
            ["styles"]        = Strings(styles.Select(StylePolicy.WireOf)),
            ["matchups"]      = Strings(rows.Select(r => r.Id)),

            ["seeds"] = new JsonObject
            {
                ["first"] = setup.Seed,
                ["last"]  = last,
                ["note"]  = "each cell takes a block of runs_a_cell consecutive seeds, in the order "
                            + "the rows and then the columns are listed, so no two cells share a "
                            + "fight and the whole table rebuilds from this one figure"
            },

            ["targeting"] = new JsonObject
            {
                ["id"]   = StylePolicy.WireOf(selector),
                ["note"] = TargetingNote(selector)
            },

            ["style_notes"] = StyleNotes(),

            ["table"]           = TableEcho(setup.Table, setup.Visibility, setup.Source, setup.SourceNote),
            ["challenge_level"] = setup.ChallengeLevel,
            ["opening_range"]   = Wire(setup.Opening.ToString()),
            ["max_pages"]       = pages,

            ["unfair_threshold"] = new JsonObject
            {
                ["win_rate_at_or_below"] = UnfairAtOrBelow,
                ["fewest_runs"]          = FewestRunsForAVerdict,
                ["note"]                 = UnfairNote
            },

            ["narrative"] = StylePolicy.NarrativeIsNotSeeded,

            ["matrix"] = matrix
        });
    }

    /// <summary>One row of the matrix: which fight it is, and what it is called.</summary>
    private sealed record Matchup(string Id, IReadOnlyList<Combatant> Fight);

    /// <summary>
    /// The rows: the whole party against the opposition, and each Hero alone against it.
    ///
    /// <para><b>"Alone" is one Hero and everybody not on their side</b>, which drops any ally who is
    /// not a Hero as well as the other Heroes — the question the owner asked it for is whether a
    /// one-on-one can be epic, and a Foe standing beside the Hero is not that fight.</para>
    /// </summary>
    private static List<Matchup> Rows(
        string wanted, Setup setup, IReadOnlyList<Combatant> heroes, string partySide,
        IReadOnlyList<Combatant> opposition)
    {
        var rows = new List<Matchup>();

        if (!string.Equals(wanted, "each_hero_alone", StringComparison.Ordinal))
            rows.Add(new Matchup("party", setup.Combatants));

        if (string.Equals(wanted, "party", StringComparison.Ordinal)) return rows;

        foreach (var hero in heroes.OrderBy(h => h.Id, StringComparer.Ordinal))
            rows.Add(new Matchup(hero.Id, [hero, .. opposition]));

        return rows;
    }

    /// <summary>What one cell measured.</summary>
    /// <param name="Wins">How many of the runs the party's side was the last one standing in.</param>
    /// <param name="Draws">How many ended with both sides up, or neither.</param>
    /// <param name="Pages">Pages across every run, for the mean.</param>
    private sealed record CellResult(int Wins, int Draws, long Pages);

    /// <summary>
    /// N seeded fights of one matchup under one style — the rate, the draws and the pages, and
    /// nothing else.
    ///
    /// <para>It is deliberately not <see cref="Measure"/>: a cell is one number in a table of
    /// twenty, and answering each of them with a full per-character report would be an answer
    /// nobody can read. A reader who wants the detail of one cell calls <c>run_encounters</c> with
    /// that cell's own seed block, which is why every one of them is echoed.</para>
    /// </summary>
    private CellResult Cell(Setup setup, IPolicy policy, int runs, int maxPages, string side)
    {
        var wins = 0;
        var draws = 0;
        long pages = 0;

        var sides = setup.Combatants.Select(c => c.Side).Distinct(StringComparer.Ordinal).ToList();

        for (var run = 0; run < runs; run++)
        {
            var engine = new Encounter(_play, new SeededDice(setup.Seed + run), setup.Table);

            var state = engine.RunToEnd(
                engine.Begin(setup.Combatants, setup.ChallengeLevel, setup.Opening, setup.Visibility),
                policy, maxPages);

            var floor = engine.DefeatFloor;
            pages += state.Page;

            var standing = sides
                .Where(s => state.Combatants.Values
                    .Any(c => string.Equals(c.Side, s, StringComparison.Ordinal) && !c.Defeated(floor)))
                .ToList();

            if (standing.Count != 1) draws++;
            else if (string.Equals(standing[0], side, StringComparison.Ordinal)) wins++;
        }

        return new CellResult(wins, draws, pages);
    }

    /// <summary>Every style's note, so a matrix carries the guess behind each of its columns.</summary>
    private JsonObject StyleNotes()
    {
        var notes = new JsonObject();

        foreach (var style in Enum.GetValues<PlayStyle>())
            notes[StylePolicy.WireOf(style)] = StylePolicy.NoteFor(style, _play);

        return notes;
    }

    /// <summary>
    /// N seeded runs of one fight, and what came out of them.
    ///
    /// <para><b>Every rate is printed beside the things that make it mean anything.</b> The N, the
    /// seeds, the policy's own name, the style and the selector with their notes, and the table's
    /// settings are in the same object as the figures, deliberately — a caller that has to make a
    /// second call to find out what a number was measured under will quote the number on its own.
    /// </para>
    ///
    /// <para><b>The second half of the report answers the owner's second question</b> — what has
    /// this party no answer for, and where is each character strongest and weakest — and it is
    /// <em>observed</em> rather than restated. Which attack forms landed, which defences held and
    /// what put each character down all come off <see cref="Encounter.RunObserved"/>, which reads
    /// them off the engine's own record of each roll and off the difference between two states. A
    /// report that echoed the intents instead would credit an attack the rules refused with a
    /// miss.</para>
    /// </summary>
    private JsonObject Measure(Setup setup, IPolicy policy, int runs, int maxPages)
    {
        var sides = setup.Combatants.Select(c => c.Side).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var wins = sides.ToDictionary(s => s, _ => 0, StringComparer.Ordinal);
        var healthBySide = sides.ToDictionary(s => s, _ => 0L, StringComparer.Ordinal);
        var resolveSpentBySide = sides.ToDictionary(s => s, _ => 0L, StringComparer.Ordinal);

        var defeats = setup.Combatants.ToDictionary(c => c.Id, _ => 0, StringComparer.Ordinal);
        var healthLeft = setup.Combatants.ToDictionary(c => c.Id, _ => 0L, StringComparer.Ordinal);
        var groupLeft = setup.Combatants.ToDictionary(c => c.Id, _ => 0L, StringComparer.Ordinal);

        var tallies = setup.Combatants.ToDictionary(c => c.Id, _ => new Tally(), StringComparer.Ordinal);

        var openingResolve = setup.Combatants.Sum(c => (long)c.Resolve);

        long totalPages = 0;
        long adversitySpent = 0;
        var draws = 0;
        var unread = 0;

        for (var run = 0; run < runs; run++)
        {
            var seed = setup.Seed + run;
            var engine = new Encounter(_play, new SeededDice(seed), setup.Table);
            var opened = engine.Begin(
                setup.Combatants, setup.ChallengeLevel, setup.Opening, setup.Visibility);

            var adversity = opened.Adversity;

            var result = engine.RunObserved(opened, policy, maxPages);
            var state = result.State;
            var floor = engine.DefeatFloor;

            totalPages += state.Page;
            adversitySpent += adversity - state.Adversity;
            unread += result.Observed.DefenceTraitsUnread;

            var standing = sides
                .Where(side => state.Combatants.Values
                    .Any(c => string.Equals(c.Side, side, StringComparison.Ordinal) && !c.Defeated(floor)))
                .ToList();

            if (standing.Count == 1) wins[standing[0]]++;
            else draws++;

            foreach (var combatant in state.Combatants.Values)
            {
                if (combatant.Defeated(floor)) defeats[combatant.Id]++;

                healthLeft[combatant.Id] += combatant.CurrentHealth;
                groupLeft[combatant.Id] += combatant.GroupSize;

                healthBySide[combatant.Side] += combatant.Kind == CombatantKind.MinionGroup ? 0 : combatant.CurrentHealth;
                resolveSpentBySide[combatant.Side] += combatant.Resolve;

                tallies[combatant.Id].Pages +=
                    result.Observed.LastPageStandingOn.GetValueOrDefault(combatant.Id, state.Page);
            }

            foreach (var attack in result.Observed.Attacks)
            {
                tallies[attack.Attacker].Attacking(attack);
                tallies[attack.Target].Defending(attack);
            }

            foreach (var defeat in result.Observed.Defeats)
                tallies[defeat.Combatant].Defeated(defeat);
        }

        // Resolve *spent* is what the fight opened with less what is left, which is why the loop
        // above accumulated what is left: a spend is not recorded anywhere else, and adding one up
        // from the ledger would be this program doing the engine's arithmetic.
        var openingBySide = setup.Combatants
            .GroupBy(c => c.Side, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(c => (long)c.Resolve), StringComparer.Ordinal);

        // <b>A side with nobody on it who has Health reports no Health, and <c>0.00</c> is not
        // that.</b> A group of Minions has a count and no Health — Ch.4 p.77 gives them one
        // characteristic — so the sum above adds nothing for them, and a side made only of Minions
        // came back with <c>mean_health_remaining: 0.0</c>. That reads as a side wiped out to the
        // last point in every run, which is the opposite of what the run may have found, and it is
        // the figure a balance question is asked about. <c>by_combatant</c> already answers null for
        // the same reason; this is the same honesty one level up.
        var holdsHealth = sides.ToDictionary(
            side => side,
            side => setup.Combatants.Any(c =>
                string.Equals(c.Side, side, StringComparison.Ordinal) && c.Kind != CombatantKind.MinionGroup),
            StringComparer.Ordinal);

        var bySide = new JsonArray();

        foreach (var side in sides)
        {
            var members = Tally.Merged(setup.Combatants
                .Where(c => string.Equals(c.Side, side, StringComparison.Ordinal))
                .Select(c => tallies[c.Id]));

            bySide.Add(new JsonObject
            {
                ["side"]                   = side,
                ["win_rate"]               = Rate(wins[side], runs),

                // <b>The owner's line, and it says so wherever it appears.</b> A side that wins half
                // its fights or fewer over a hundred or more is flagged; below a hundred the answer
                // is null rather than false, because "not unfair" and "not enough fights to say" are
                // different things and only one of them is reassuring.
                ["unfair"]                 = Unfair(wins[side], runs),

                ["mean_health_remaining"]  = holdsHealth[side] ? Mean(healthBySide[side], runs) : null,
                ["mean_resolve_spent"]     = Mean(openingBySide[side] * runs - resolveSpentBySide[side], runs),

                ["attack_forms"]           = AttackForms(members, runs),
                ["defences"]               = Defences(members),
                ["defeated_by"]            = DefeatedBy(members, runs, oneCharacter: false)
            });
        }

        var byCombatant = new JsonArray();

        foreach (var combatant in setup.Combatants)
        {
            var tally = tallies[combatant.Id];

            byCombatant.Add(new JsonObject
            {
                ["id"]                    = combatant.Id,
                ["name"]                  = combatant.Name,
                ["kind"]                  = Wire(combatant.Kind.ToString()),
                ["side"]                  = combatant.Side,

                // <b>The two of p.75's three modifiers that are a fact about a character travel
                // with the rate, the way the scene's light travels inside `table`.</b> A rate is
                // quoted with four things and none of them can carry these: size is on the
                // combatant and moves the defender's active defence by up to two dice, invisibility
                // is on the combatant and costs whoever faces one three. A report measured against
                // a giant, echoed back as though everybody were the same size, is a figure about a
                // fight nobody can reconstruct from it — which is the whole reason the light is
                // echoed at all. Ch.4 p.75.
                ["size"]                  = combatant.Size,
                ["invisible"]             = combatant.Invisible,
                ["hard_target"]           = combatant.HardTarget,
                ["ready"]                 = combatant.Ready,

                // <b>And what they walked in holding, for the same reason.</b> p.76 aims a grab at
                // an item its target has, so a fight opened with a weapon in somebody's hands is a
                // fight where a grab is possible and one opened without is a fight where every grab
                // is refused. That is a difference between two measurements, and a rate echoed
                // without it is a figure about a fight nobody can reconstruct. It is the opening
                // hand rather than the closing one: a run ends N times and this object is one.
                ["holding"]               = combatant.Holding?.Name,

                ["defeat_rate"]           = Rate(defeats[combatant.Id], runs),
                ["mean_pages_survived"]   = Mean(tally.Pages, runs),
                ["mean_health_remaining"] = combatant.Kind == CombatantKind.MinionGroup
                    ? null
                    : Mean(healthLeft[combatant.Id], runs),
                ["mean_minions_remaining"] = combatant.Kind == CombatantKind.MinionGroup
                    ? Mean(groupLeft[combatant.Id], runs)
                    : null,

                // <b>Where this character is strongest and weakest, which is the owner's second
                // question.</b> Every figure is off the observation and none is off the intent: an
                // attack the rules refused is not an exchange, and a defence that held is one that
                // out-rolled the attack rather than one that was declared.
                ["attack_forms"]          = AttackForms(tally, runs),
                ["defences"]              = Defences(tally),
                ["defeated_by"]           = DefeatedBy(tally, runs, oneCharacter: true)
            });
        }

        var style = policy as StylePolicy;

        return new JsonObject
        {
            ["ok"] = true,

            // The four that a rate may never be quoted without, first and in the same object.
            ["runs"]            = runs,
            ["seeds"]           = new JsonObject
            {
                ["first"] = setup.Seed,
                ["last"]  = (long)setup.Seed + runs - 1,
                ["note"]  = "one run per seed, consecutively, so this call reproduces exactly"
            },
            ["policy"]          = new JsonObject
            {
                ["id"]   = style?.Id ?? Policies[0],
                ["name"] = policy.Name,
                ["note"] = "a policy is a guess about how people play, not a rule — this figure is "
                           + "about a party that plays this way"
            },

            // <b>The two axes are echoed apart from each other, because they are two guesses.</b>
            // How freely a side spends and who it swings at are independent, and a reader arguing
            // with one of them has to be able to see which one they are arguing with.
            ["style"]           = new JsonObject
            {
                ["id"]   = style is null ? Policies[0] : style.Id,
                ["note"] = style?.Note
                           ?? "the policy this server shipped with: hit whoever is nearly down, and "
                              + "buy a reroll when the roll came close"
            },
            ["targeting"]       = new JsonObject
            {
                ["id"]   = StylePolicy.WireOf(style?.Targeting ?? Targeting.Weakest),
                ["note"] = TargetingNote(style?.Targeting ?? Targeting.Weakest)
            },

            ["table"]           = TableEcho(setup.Table, setup.Visibility, setup.Source, setup.SourceNote),

            ["challenge_level"] = setup.ChallengeLevel,
            ["opening_range"]   = Wire(setup.Opening.ToString()),
            ["max_pages"]       = maxPages,

            ["mean_pages"]           = Mean(totalPages, runs),
            ["draw_rate"]            = Rate(draws, runs),
            ["mean_adversity_spent"] = Mean(adversitySpent, runs),
            ["opening_resolve"]      = openingResolve,

            ["unfair_threshold"] = new JsonObject
            {
                ["win_rate_at_or_below"] = UnfairAtOrBelow,
                ["fewest_runs"]          = FewestRunsForAVerdict,
                ["note"]                 = UnfairNote
            },

            // <b>Published rather than swallowed.</b> The Trait that answered an attack is the one
            // figure in this report that is read back out of a ledger sentence rather than off the
            // state, and this is how many exchanges it could not be read for. It is zero on every
            // fight this engine resolves today; above zero, the defence tables below are short by
            // that many rows and the sentence LedgerReading is anchored on has moved.
            ["defence_traits_unread"] = unread,

            ["by_side"]      = bySide,
            ["by_combatant"] = byCombatant
        };
    }

    /// <summary>
    /// Whether this side's rate is at or below the owner's line, or null where there are too few
    /// runs to say.
    ///
    /// <para><b>Public so the boundary can be driven at the boundary.</b> Half of a hundred is the
    /// case the owner's sentence turns on — "half or less chance of victory" — and no fight can be
    /// made to land there on demand, so the comparison is asked directly at 0.50, at 0.51 and one
    /// run below the floor. The wire tests beside it drive the flag through a real lopsided
    /// matchup, which is the control that this is the predicate the report actually calls.</para>
    ///
    /// <para><b>Both sides carry the flag, and a draw counts against both of them.</b> A run that
    /// ended with neither side down is a win for nobody, so it lowers every side's rate at once —
    /// which means two combatants who cannot get through each other come back flagged against each
    /// other, and "unfair" there does not mean anybody is being beaten. It is the honest answer to
    /// the question the owner asked (is the fight worth playing) and the wrong reading of the word,
    /// so <c>unfair_threshold.note</c> says it in the same object as the flag and a fixture drives
    /// the stalemate.</para>
    /// </summary>
    public static bool? IsUnfair(int wins, int runs) =>
        runs < FewestRunsForAVerdict ? null : (double)wins / runs <= UnfairAtOrBelow;

    private static JsonValue? Unfair(int wins, int runs) =>
        IsUnfair(wins, runs) is { } verdict ? JsonValue.Create(verdict) : null;

    /// <summary>One sentence about what a target selector assumes, for the echo.</summary>
    private static string TargetingNote(Targeting targeting) => targeting switch
    {
        Targeting.Weakest =>
            "focus fire on whoever is nearly down — a real table habit, and one of several",
        Targeting.Strongest =>
            "take the hardest opponent down first, while everybody is still fresh",
        Targeting.HighestThreat =>
            "go after whoever has done the most damage so far, and before anybody has landed "
            + "anything, whoever has the greatest attack rank",
        _ => throw new ArgumentOutOfRangeException(nameof(targeting), targeting, "No such selector.")
    };

    // ── What a run is tallied into ────────────────────────────────────────

    /// <summary>What one attack form did, across every run.</summary>
    private sealed class Swinging
    {
        public int Exchanges { get; set; }
        public int Landed { get; set; }
        public long Damage { get; set; }
        public long Minions { get; set; }
    }

    /// <summary>What one defending Trait did, across every run.</summary>
    private sealed class Answering
    {
        public int Answered { get; set; }
        public int Held { get; set; }
    }

    /// <summary>
    /// One combatant's whole record across N runs — what they swung with, what answered for them,
    /// what put them down, and how long they lasted.
    /// </summary>
    private sealed class Tally
    {
        public Dictionary<string, Swinging> Attacks { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Answering> Defences { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Defeats { get; } = new(StringComparer.Ordinal);
        public long Pages { get; set; }

        public void Attacking(ObservedAttack attack)
        {
            if (!Attacks.TryGetValue(attack.TraitId, out var form))
                Attacks[attack.TraitId] = form = new Swinging();

            form.Exchanges++;
            if (attack.Landed) form.Landed++;
            form.Damage += attack.Damage;
            form.Minions += attack.MinionsDefeated;
        }

        public void Defending(ObservedAttack attack)
        {
            var trait = attack.DefenceTrait;

            // A defence the ledger reading could not recover is counted nowhere rather than counted
            // under a made-up name. `defence_traits_unread` at the top of the report is where it is
            // said out loud.
            if (trait is null) return;

            if (!Defences.TryGetValue(trait, out var answering))
                Defences[trait] = answering = new Answering();

            answering.Answered++;
            if (attack.DefenceHeld) answering.Held++;
        }

        public void Defeated(ObservedDefeat defeat)
        {
            // p.79's dying clock belongs to nobody's turn, so a defeat with no attacker is filed
            // under the sentence that says so rather than dropped.
            var by = defeat.By is null || defeat.With is null
                ? "no attack — the page turn, a clock or an effect running out"
                : $"{defeat.By} with {defeat.With}";

            Defeats[by] = Defeats.GetValueOrDefault(by) + 1;
        }

        /// <summary>Several combatants' records added together, which is what a side's row is.</summary>
        public static Tally Merged(IEnumerable<Tally> tallies)
        {
            var merged = new Tally();

            foreach (var tally in tallies)
            {
                merged.Pages += tally.Pages;

                foreach (var (trait, form) in tally.Attacks)
                {
                    if (!merged.Attacks.TryGetValue(trait, out var into))
                        merged.Attacks[trait] = into = new Swinging();

                    into.Exchanges += form.Exchanges;
                    into.Landed += form.Landed;
                    into.Damage += form.Damage;
                    into.Minions += form.Minions;
                }

                foreach (var (trait, answering) in tally.Defences)
                {
                    if (!merged.Defences.TryGetValue(trait, out var into))
                        merged.Defences[trait] = into = new Answering();

                    into.Answered += answering.Answered;
                    into.Held += answering.Held;
                }

                foreach (var (by, count) in tally.Defeats)
                    merged.Defeats[by] = merged.Defeats.GetValueOrDefault(by) + count;
            }

            return merged;
        }
    }

    /// <summary>
    /// Which attack forms landed damage and how much, by the Trait or Power that was rolled —
    /// strongest first, so the answer to "what is this character best at" is the first row.
    /// </summary>
    private static JsonArray AttackForms(Tally tally, int runs) =>
    [
        .. tally.Attacks
            .OrderByDescending(pair => pair.Value.Damage)
            .ThenByDescending(pair => pair.Value.Minions)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonNode)new JsonObject
            {
                ["trait_id"]           = pair.Key,
                ["exchanges"]          = pair.Value.Exchanges,
                ["landed"]             = pair.Value.Landed,
                ["land_rate"]          = Ratio(pair.Value.Landed, pair.Value.Exchanges),
                ["total_damage"]       = pair.Value.Damage,
                ["mean_damage_a_run"]  = Mean(pair.Value.Damage, runs),
                ["minions_defeated"]   = pair.Value.Minions
            })
    ];

    /// <summary>
    /// Which defences answered for this combatant and how often they held — the other half of "where
    /// are they weakest", and the half a Health total cannot show.
    /// </summary>
    private static JsonArray Defences(Tally tally) =>
    [
        .. tally.Defences
            .OrderByDescending(pair => pair.Value.Answered)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonNode)new JsonObject
            {
                ["trait"]     = pair.Key,
                ["answered"]  = pair.Value.Answered,
                ["held"]      = pair.Value.Held,
                ["hold_rate"] = Ratio(pair.Value.Held, pair.Value.Answered)
            })
    ];

    /// <summary>
    /// What put this combatant — or this side — out of the fight, by attacker and by what they used.
    ///
    /// <para><b>A combatant's figure is a rate and a side's is a mean, and they are spelled
    /// differently because they are different things.</b> One character goes down at most once a
    /// fight, so their count over N is a proportion between nothing and one. A side of four does
    /// not: the same division answered <c>3.315</c>, which reads exactly like a rate and is not one.
    /// A figure that looks like something it is not is the shape of defect this whole report is
    /// written against.</para>
    /// </summary>
    private static JsonArray DefeatedBy(Tally tally, int runs, bool oneCharacter) =>
    [
        .. tally.Defeats
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonNode)(oneCharacter
                ? new JsonObject
                {
                    ["by"]   = pair.Key,
                    ["runs"] = pair.Value,
                    ["rate"] = Rate(pair.Value, runs)
                }
                : new JsonObject
                {
                    ["by"]          = pair.Key,
                    ["runs"]        = pair.Value,
                    ["mean_a_run"]  = Mean(pair.Value, runs)
                }))
    ];

    // ── Reading a setup ───────────────────────────────────────────────────

    /// <summary>Everything a fight needs before it can be opened.</summary>
    /// <param name="Tiers">
    /// The tier each character combatant was built to, by combatant id — the input the character
    /// engine derives Resolve from, kept so that the opening ledger can say which one was used. A
    /// group of Minions has no sheet and so no entry.
    /// </param>
    /// <param name="Source">Where <paramref name="Table"/> came from.</param>
    /// <param name="SourceNote">
    /// The same in a sentence, naming the sheets — what page one of the run says, and what a
    /// report's echo carries so a measurement is reproducible from its own answer.
    /// </param>
    private sealed record Setup(
        IReadOnlyList<Combatant> Combatants,
        IReadOnlyDictionary<string, string> Tiers,
        TableRules Table,
        TableSource Source,
        string SourceNote,
        int ChallengeLevel,
        int Seed,
        RangeBand Opening,
        Visibility Visibility);

    /// <summary>
    /// Where the table a fight is resolved under came from.
    ///
    /// <para><b>It is published rather than inferred.</b> A rate is quoted with its table, and two
    /// runs whose echoed tables read the same may have got them from different places — one off a
    /// campaign's characters and one off an argument somebody typed. A reader deciding whether a
    /// measurement is of <em>their</em> game needs to know which.</para>
    /// </summary>
    private enum TableSource
    {
        /// <summary>Nobody said anything: the book as printed, which is p.79's own baseline.</summary>
        Book,

        /// <summary>The caller's argument, and no sheet carries a table.</summary>
        Call,

        /// <summary>The sheets handed in, which agree.</summary>
        Sheets,

        /// <summary>Both, saying the same thing switch by switch.</summary>
        SheetsAndCall
    }

    private static readonly IReadOnlyDictionary<string, string> NoTiers =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private bool TryReadSetup(
        JsonElement combatants,
        JsonElement? table,
        int? challengeLevel,
        int? seed,
        string? openingRange,
        string? visibility,
        out Setup setup,
        out JsonObject problem)
    {
        setup = new Setup(
            [], NoTiers, TableRules.Book, TableSource.Book, "", 0, 0, RangeBand.Close, Visibility.Clear);
        problem = new JsonObject();

        if (!TryReadTable(table, out var onTheCall, out problem)) return false;
        if (!TryReadRange(openingRange, out var opening, out problem)) return false;
        if (!TryReadVisibility(visibility, out var light, out problem)) return false;

        // <b>A Challenge Level below zero is refused, not clamped.</b> `Math.Max(0, …)` read −3 as
        // 0, opened the fight with the Adversity a Challenge Level of nothing buys, and echoed
        // `challenge_level: 0` back — so a scene somebody had deliberately set below the baseline
        // was measured as the baseline and the answer said the baseline was what they asked for.
        // That is the same "accepted and quietly ignored" the unknown table setting and the
        // non-boolean switch are both refused for; a negative Challenge Level is a typo or a
        // misunderstanding, and either way it is worth a sentence.
        if (challengeLevel is { } level && level < 0)
        {
            problem = Problem("BAD_CHALLENGE_LEVEL",
                $"The Challenge Level is {level}. Ch.5 p.85 adds it to the GM's opening Adversity "
                + "pool, so the smallest one that means anything is 0 — a scene that adds nothing. "
                + "A negative one used to be read as 0 and echoed back as 0, which is a measurement "
                + "of a different scene from the one that was asked for.");
            return false;
        }

        if (!TryReadCombatants(combatants, out var everyone, out var tiers, out var carried, out var barefaced, out problem))
            return false;

        // <b>A fight needs two sides, and this is refused rather than run.</b> `Over` and every
        // policy partition on `Combatant.Side` alone, so a fight in which everybody shares one
        // answers `win_rate: 1` for that side from the first page — a figure that looks exactly like
        // a real one, is quotable, reproducible, printed beside its N, its seeds, its policy and its
        // table, and means nothing whatever. The commonest way to produce one is to leave `side` off
        // every entry, since the default is derived from the kind: a fight between Heroes is a fight
        // p.73 prints, and it is a fight nobody can win until somebody says who is against whom.
        var sides = everyone.Select(c => c.Side).Distinct(StringComparer.Ordinal).ToList();

        if (sides.Count < 2)
        {
            problem = Problem("ONE_SIDED",
                $"Every combatant is on the side '{sides[0]}', so nobody can lose: the fight is over "
                + "before it starts and a rate off it would be 1.000 for that side with the N, the "
                + "seeds, the policy and the table printed beside it. Give the two halves different "
                + "\"side\" values — the side is yours to say and nothing derives it, so a fight "
                + "between two Heroes is fine and a fight in which everyone shares a side is not.");
            return false;
        }

        // <b>The table comes off the sheets, and this is where the fight learns it.</b> The
        // encounter server holds no account and cannot resolve a campaign id — see
        // docs/guide/mcp-and-headless.md — so the only way a house rule reaches a fight is on the
        // characters in it. Read after the combatants for that reason, and refused rather than
        // reconciled where two of them disagree.
        if (!TryAgreeTable(onTheCall, everyone, carried, barefaced,
                out var rules, out var source, out var note, out problem))
        {
            return false;
        }

        setup = new Setup(
            everyone, tiers, rules, source, note, challengeLevel ?? 0, seed ?? 0, opening, light);
        return true;
    }

    /// <summary>
    /// The table this fight is resolved under, out of the sheets that carry one and the argument
    /// the caller may also have passed — or a refusal naming what disagrees with what.
    ///
    /// <para><b>Four cases, and the middle two are where the reasoning is.</b></para>
    ///
    /// <para><b>Two sheets carrying different tables is a refusal</b>, <c>TABLE_DISAGREES</c>,
    /// naming both characters and the first setting they differ on. Two blocks that disagree are
    /// two contrary claims about which game is being played, and there is no honest way to pick:
    /// taking either one measures a fight under rules half its combatants were not built for, and
    /// the report's echo would say the table came from "the sheets" while naming only one of
    /// them.</para>
    ///
    /// <para><b>A sheet carrying no table beside sheets that do is accepted, and page one says
    /// so.</b> That is deliberately not the same answer, because an absent block is not a contrary
    /// claim — it is silence. Refusing here would make the commonest fight there is unfightable
    /// without hand-editing JSON: a campaign's Hero against a Villain somebody built in the
    /// sandbox, which is exactly what the GM running that campaign does every week. What the
    /// refusal would protect against — a rule quietly applied to somebody who never agreed to
    /// it — is answered instead by naming the sheet that carried none on page one and in the
    /// echo, which is the discipline this server applies to every table setting it accepts.</para>
    ///
    /// <para><b>Silence about what, though — and this is where the sentence has to be careful.</b>
    /// It is tempting to say a sheet without a block has never been in a game that adopted
    /// anything, and that is <em>not true</em>. <c>CampaignJoin</c> copies the campaign's table
    /// into an empty field and never over a full one, so a character who joined before the GM
    /// decided anything keeps a null block for ever and nothing on the campaigns page reports it —
    /// see docs/guide/browser.md, "The copy going stale is reported in one of its two directions".
    /// So an absent block is ambiguous three ways: no game at all, a game that plays the book, or a
    /// stale copy of a game that does not. This server can tell the first of those from the other
    /// two, because <c>CharacterSheet.CampaignId</c> is on the sheet beside the block, and page one
    /// says which it is rather than asserting the flattering one. A sentence that told a GM their
    /// player's Hero was at no table, when it names a campaign whose rules simply never travelled,
    /// would be this server inventing the reassurance it was built to withhold.</para>
    ///
    /// <para><b>A caller who also passes a table has to agree with the sheets, switch by
    /// switch.</b> Agreement is fine and is echoed as both; a disagreement is
    /// <c>CALL_TABLE_DISAGREES</c> rather than a silent precedence rule, because either precedence
    /// is somebody's setting thrown away — and a run whose echo says <c>wound_penalties: true</c>
    /// off an argument, fought by characters built without it, is the "accepted and quietly
    /// ignored" every other reader in this file refuses.</para>
    /// </summary>
    /// <param name="fromCall">The table argument, or null where the caller passed none.</param>
    /// <param name="everyone">The fight, in the order it was handed in. <b>That order is not used
    /// below</b>: carriers are taken by id, so which pair a refusal names — and which switch,
    /// where three sheets disagree three ways — is a fact about the set of sheets rather than
    /// about how somebody typed the array.</param>
    /// <param name="carried">The block each sheet carried, by combatant id, for those that did.</param>
    /// <param name="barefaced">The character combatants whose sheet carried none, each with the
    /// campaign its sheet names if it names one. A group of Minions has no sheet at all and is on
    /// neither list.</param>
    private static bool TryAgreeTable(
        TableRules? fromCall,
        IReadOnlyList<Combatant> everyone,
        IReadOnlyDictionary<string, CampaignTable> carried,
        IReadOnlyList<AtNoTable> barefaced,
        out TableRules rules,
        out TableSource source,
        out string note,
        out JsonObject problem)
    {
        problem = new JsonObject();

        // <b>By id, and not in the order the sheets were handed in.</b> Everything below is a
        // function of which carrier is "the first": which pair a refusal names, and — where three
        // or more sheets disagree with each other — which switch, because the first difference is
        // the first difference *from that sheet*. Arrival order would make the same fight refuse
        // two different ways depending on how the array was typed, so two GMs comparing notes
        // about one bad export would be reading two different findings. Ids are unique here
        // (DUPLICATE_COMBATANT is refused above), so ordinal order is total and the answer is a
        // fact about the set of sheets.
        var carriers = everyone
            .Select(c => c.Id)
            .Where(carried.ContainsKey)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (carriers.Count == 0)
        {
            rules = fromCall ?? TableRules.Book;
            source = fromCall is null ? TableSource.Book : TableSource.Call;
            note = fromCall is null
                ? "the book as printed: no sheet in this fight carries a table and none was passed "
                  + "on the call, and p.79 offers the optional rules to a table that asks for them"
                : "the \"table\" argument on this call — no sheet in this fight carries one";
            return true;
        }

        var first = carriers[0];
        var agreed = TableRules.From(carried[first]);

        // Every carrier against the first, rather than the first against the second: a fight of
        // four sheets in which the fourth is the odd one out has to be refused too, and naming the
        // pair is what makes the refusal actionable.
        foreach (var other in carriers.Skip(1))
        {
            if (TableRules.FirstDifference(agreed, TableRules.From(carried[other])) is not { } differs)
                continue;

            rules = TableRules.Book;
            source = TableSource.Book;
            note = "";

            problem = Problem("TABLE_DISAGREES",
                $"'{first}' and '{other}' carry different house rules: they disagree about "
                + $"{Wire(differs)}. A fight is resolved under one table, and these two sheets are "
                + "two contrary claims about which game is being played — taking either would "
                + "measure a fight under rules half the characters in it were not built for, and "
                + "the answer would say the table came from the sheets while naming only one of "
                + "them. Export both characters from the same campaign, or fight them under a "
                + "table you pass on the call and sheets that carry none.");
            return false;
        }

        var without = Barefaced(barefaced);

        if (fromCall is { } given)
        {
            if (TableRules.FirstDifference(given, agreed) is { } clash)
            {
                rules = TableRules.Book;
                source = TableSource.Book;
                note = "";

                problem = Problem("CALL_TABLE_DISAGREES",
                    $"The \"table\" on this call and the table '{first}' carries disagree about "
                    + $"{Wire(clash)}. Neither is quietly preferred: whichever won, the other is a "
                    + "setting somebody chose and this server threw away, and the answer would echo "
                    + "a table that half this fight was not built for. Pass no table and the sheets' "
                    + "own is used, or pass the same one they carry.");
                return false;
            }

            rules = agreed;
            source = TableSource.SheetsAndCall;
            note = $"the sheets handed in, starting with '{first}', and the \"table\" on this call "
                   + "says the same thing switch by switch" + without;
            return true;
        }

        rules = agreed;
        source = TableSource.Sheets;
        note = $"the sheets handed in, starting with '{first}', which agree" + without;
        return true;
    }

    /// <summary>
    /// The clause page one adds for the sheets that carried no house rules — and it says, of each
    /// of them, which kind of silence it is.
    ///
    /// <para><b>Two clauses rather than one, because the honest sentence is different.</b> A sheet
    /// naming no campaign was built outside any game, and being fought under somebody else's table
    /// takes nothing away from it. A sheet naming a campaign and carrying no block is the case this
    /// server must not flatter: <c>CampaignJoin</c> writes the campaign's table into an empty field
    /// only, so the block is missing either because that game adopted nothing or because the copy
    /// was taken before it did — and the second is a state the browser produces and does not report
    /// (docs/guide/browser.md). Calling that character "at no table" would tell a GM the thing they
    /// would most like to hear and have no way to check.</para>
    ///
    /// <para><b>Both clauses keep "carries no table"</b>, which is the fact a reader scans for, and
    /// neither of them says the character agreed to anything.</para>
    /// </summary>
    private static string Barefaced(IReadOnlyList<AtNoTable> sheets)
    {
        if (sheets.Count == 0) return "";

        // By id, for the reason the carriers are: page one is a description of a fight and not of
        // the array somebody typed, and two calls listing the same combatants in two orders have
        // to produce the same sentence or nobody can compare two runs by reading them.
        var byId = sheets.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

        var sandbox = byId.Where(s => s.CampaignId is null).Select(s => s.Id).ToList();
        var clauses = new List<string>();

        if (sandbox.Count > 0)
        {
            clauses.Add($"{Sentence(sandbox)} carr{(sandbox.Count == 1 ? "ies" : "y")} no table "
                        + $"and name{(sandbox.Count == 1 ? "s" : "")} no campaign, so "
                        + $"{(sandbox.Count == 1 ? "it is" : "they are")} fought under this one");
        }

        foreach (var sheet in byId.Where(s => s.CampaignId is not null))
        {
            clauses.Add($"'{sheet.Id}' carries no table but names campaign "
                        + $"'{sheet.CampaignId}' — either that game adopted nothing or this copy "
                        + "was taken before it did, and this server cannot tell which — so it too "
                        + "is fought under this one");
        }

        return "; " + string.Join("; ", clauses);
    }

    /// <summary>A list of ids in a sentence: <c>a</c>, <c>a and b</c>, <c>a, b and c</c>.</summary>
    private static string Sentence(List<string> ids) => ids.Count switch
    {
        1 => $"'{ids[0]}'",
        _ => string.Join(", ", ids.Take(ids.Count - 1).Select(id => $"'{id}'"))
             + $" and '{ids[^1]}'"
    };

    /// <summary>
    /// Which tier each character in the fight was built to, on the opening ledger, citing the entry
    /// that says what a tier buys.
    ///
    /// <para><b>It is on the ledger because it is an input to a figure nobody can otherwise check.</b>
    /// Ch.5 p.83 measures a Hero's opening Resolve down from their Trait Cap, and the Trait Cap comes
    /// from the tier — so two identical sheets at two tiers open a fight with different Resolve and
    /// nothing in the answer said which was used. A reader who may not quote a number the ledger did
    /// not print is exactly the reader who needs the tier printed.</para>
    /// </summary>
    /// <summary>
    /// Page one says where this fight's table came from — always, including where the answer is
    /// "nobody said anything".
    ///
    /// <para><b>It is on the ledger and not only in the echo because the echo is a statement of
    /// what is on, and this is a statement of who decided.</b> A run under a campaign's house
    /// rules and a run under the same rules typed onto the call are the same numbers and are not
    /// the same claim, and the second one is the one somebody could have got wrong. Where the
    /// answer is the book, saying so is worth the line for the reason the switch lines are worth
    /// theirs: a reader can tell a fight measured under the baseline from a fight whose table
    /// this server failed to read.</para>
    ///
    /// <para><b>Cited to <c>gritty_overview</c>, and the sentence is written so that what the page
    /// says and what this server says are separable.</b> That entry is p.79's paragraph about a
    /// table reviewing the optional rules and adopting what it wants before play — it is what makes
    /// "whose table is this" a question the book asks rather than one this server invented, and it
    /// is quoted for that and nothing more. <b>Where the switches came from is not on any page</b>:
    /// no paragraph in the book has an opinion about a <c>table</c> argument on an MCP call or a
    /// block copied onto a character sheet, so a line reading "the optional rules this fight is
    /// resolved under came from the sheets handed in" cited to p.79 attributes this server's
    /// bookkeeping to the rulebook. <b>And p.79 speaks for ten switches, not thirteen</b> —
    /// Checking Your Swing is p.69's and the two initiative settings are p.73's — so the sentence
    /// says so rather than letting one citation stand for all of them.</para>
    /// </summary>
    private LedgerLine TableSourceLine(Setup setup)
    {
        var entry = _play.GetGritty("gritty_overview");

        return new LedgerLine(
            1, "", entry.Id, entry.SourceRef,
            "p.79 leaves the optional combat rules to the table, to review and adopt before play; "
            + "the switches this fight is resolved under — those ten and the three beside them — "
            + $"came from {setup.SourceNote}");
    }

    private IEnumerable<LedgerLine> TierLines(Setup setup)
    {
        var entry = _play.GetResolve("starting_resolve");

        foreach (var combatant in setup.Combatants)
        {
            if (!setup.Tiers.TryGetValue(combatant.Id, out var tierId)) continue;

            var cap = _rules.GetTier(tierId)!.TraitCapRank;

            yield return new LedgerLine(
                1, combatant.Id, entry.Id, entry.SourceRef,
                $"{combatant.Name} is built to the {tierId} tier, whose Trait Cap is {cap}d, and "
                + (combatant.HoldsResolve
                    ? $"opens with {combatant.Resolve} Resolve"
                    : "holds no Resolve — only a Hero does"));
        }
    }

    private static readonly IReadOnlyDictionary<string, CampaignTable> NoTables =
        new Dictionary<string, CampaignTable>(StringComparer.Ordinal);

    /// <summary>
    /// What one sheet says about the game it came from: the house rules it carries, and the
    /// campaign it names.
    ///
    /// <para><b>The two travel together because an absent block on its own does not say enough.</b>
    /// A sheet carrying no table and naming no campaign was built outside any game. A sheet
    /// carrying no table and naming one is a different thing — either the game adopted nothing, or
    /// the copy predates what it adopted, which is a state `CampaignJoin` produces and nothing
    /// reports (docs/guide/browser.md). Both are accepted; page one says which.</para>
    /// </summary>
    private sealed record Membership(CampaignTable? Table, string? CampaignId)
    {
        /// <summary>A group of Minions, which has no sheet and so says nothing about any game.</summary>
        public static Membership None { get; } = new(null, null);
    }

    /// <param name="carried">
    /// The house rules each character sheet brought with it, by combatant id, for the sheets that
    /// carried any. A campaign copies its table onto a character when it joins and the
    /// <c>.json</c> export carries the block, which is the only route a house rule has into a
    /// fight: this server holds no account and cannot resolve a campaign.
    /// </param>
    /// <summary>
    /// One combatant whose sheet carried no house rules, and the campaign that sheet names if it
    /// names one — which is the difference page one has to print. See <see cref="Membership"/>.
    /// </summary>
    private sealed record AtNoTable(string Id, string? CampaignId);

    /// <param name="barefaced">
    /// The character combatants whose sheet carried none, in the order they were handed in, each
    /// with the campaign it names if it names one. A group of Minions is on neither list — it has
    /// no sheet to carry anything.
    /// </param>
    private bool TryReadCombatants(
        JsonElement combatants,
        out IReadOnlyList<Combatant> everyone,
        out IReadOnlyDictionary<string, string> tiers,
        out IReadOnlyDictionary<string, CampaignTable> carried,
        out IReadOnlyList<AtNoTable> barefaced,
        out JsonObject problem)
    {
        everyone = [];
        tiers = NoTiers;
        carried = NoTables;
        barefaced = [];
        problem = new JsonObject();

        var array = AsNode(combatants) as JsonArray;

        if (array is null || array.Count == 0)
        {
            problem = Problem("NO_COMBATANTS",
                "A fight needs somebody in it. Pass a JSON array of combatants: a character sheet "
                + "with a kind and a side, or a group of Minions with a threat_rank and a count.");
            return false;
        }

        var built = new List<Combatant>();
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        var tables = new Dictionary<string, CampaignTable>(StringComparer.Ordinal);
        var without = new List<AtNoTable>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject entry)
            {
                problem = Problem("BAD_COMBATANT", $"Combatant {i + 1} is not a JSON object.");
                return false;
            }

            if (!TryReadCombatant(entry, i + 1, out var combatant, out var tier, out var from, out problem))
                return false;

            if (tier is not null) byId[combatant.Id] = tier;

            // <b>A group of Minions is on neither list, and that is a decision rather than a
            // gap.</b> It has no sheet, so it carries no table and names no campaign — putting it
            // on the barefaced list would have page one announce that the robots brought no house
            // rules, which is noise of a kind that trains a reader to stop reading the line. What
            // it cannot do is carry a table nobody notices: there is no field on a Minion group to
            // put one in, so silence here is complete rather than partial.
            if (from.Table is not null) tables[combatant.Id] = from.Table;
            else if (combatant.Kind != CombatantKind.MinionGroup)
                without.Add(new AtNoTable(combatant.Id, from.CampaignId));

            if (!ids.Add(combatant.Id))
            {
                problem = Problem("DUPLICATE_COMBATANT",
                    $"Two combatants are both called '{combatant.Id}'. Ids are how every intent "
                    + "names who is doing what, so give one of them an explicit \"id\".");
                return false;
            }

            built.Add(combatant);
        }

        everyone = built;
        tiers = byId;
        carried = tables;
        barefaced = without;
        return true;
    }

    /// <summary>The kinds a combatant may be, on the wire.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["hero", "villain", "foe", "extra", "minions"];

    /// <param name="membership">
    /// The house rules the sheet carried and the campaign it names, either of which may be null —
    /// and <see cref="Membership.None"/> where the combatant is a group of Minions, which has no
    /// sheet at all.
    /// </param>
    private bool TryReadCombatant(
        JsonObject entry, int position, out Combatant combatant, out string? tier,
        out Membership membership, out JsonObject problem)
    {
        combatant = Combatant.Extra("placeholder", "placeholder", 0, 1,
            new Dictionary<string, int>(StringComparer.Ordinal), []);
        tier = null;
        membership = Membership.None;
        problem = new JsonObject();

        var kind = Text(entry, "kind").Trim().ToLowerInvariant();

        if (!Kinds.Contains(kind, StringComparer.Ordinal))
        {
            problem = Problem("NO_SUCH_KIND",
                $"Combatant {position} has a kind of '{kind}'. The kinds are: "
                + string.Join(", ", Kinds)
                + ". The kind decides tie order, whether Health is halved and who holds Resolve; "
                + "it is yours to say and nothing reads it off the sheet.");
            return false;
        }

        var side = Text(entry, "side").Trim();
        var id = Text(entry, "id").Trim();

        // <b>Read before the Minion branch, so it applies to a mob as well as to a character.</b>
        // p.75's size bands are about the attacker and the defender, and a group of Minions is both
        // in its turn; refusing a giant robot a size because it has no sheet would be a rule about
        // this server's shapes rather than about the page.
        if (!TryReadSize(entry, position, out var size, out problem)) return false;

        var invisible = Flag(entry, "invisible");

        // <b>p.80's Hard Targets, and read here for the reason the size is.</b> A group of Minions
        // can be a swarm of machines, so the flag is read before the Minion branch rather than
        // being a fact only a character sheet may carry — and nothing on a sheet carries it either.
        var hardTarget = Flag(entry, "hard_target");

        // p.79's Drop, and a group of Minions may hold one too — or would, if p.73 gave them an
        // Edge to double. The refusal is the engine's and it is on the ledger; this only reads.
        var ready = Flag(entry, "ready");

        // <b>p.76's grab needs an opponent with something to take, and this is the only way to say
        // so.</b> A grab is "an attempt to take a weapon or other handheld item away from your
        // opponent"; nothing on a character sheet can answer for a hand — gear there is a name with
        // custom features on it and the play engine may not read the character rules at all — so it
        // is the caller's word, like the size and the light. Read before the Minion branch for the
        // reason the size is: a mob can be the one carrying the artefact.
        var holding = Text(entry, "holding").Trim();

        if (string.Equals(kind, "minions", StringComparison.Ordinal))
        {
            if (!TryReadMinions(
                    entry, position, side, id, size, invisible, hardTarget, ready,
                    out combatant, out problem))
            {
                return false;
            }

            if (holding.Length > 0) combatant = combatant.Carrying(holding);

            return true;
        }

        if (entry["character"] is not { } character)
        {
            problem = Problem("NO_CHARACTER",
                $"Combatant {position} is a {kind} and carries no \"character\". Pass the "
                + "character's inputs — the shape the character server's creation_guide describes.");
            return false;
        }

        if (!TryReadSheet(character, position, out var sheet, out problem)) return false;
        if (!TryReadTier(sheet, position, out tier, out problem)) return false;

        // <b>The one thing this server reads off a sheet that is not about the character.</b>
        // `CharacterSheet.CampaignTable` is written by joining a campaign and copied by the
        // `.json` export, so it is how a table's house rules reach a fight — this server holds no
        // account and cannot resolve a `CampaignId`.
        //
        // <b>The id is read beside it because an absent block is two different states.</b> A sheet
        // naming no campaign was built outside any game; a sheet naming one and carrying no block
        // is either in a game that adopted nothing or a copy taken before it did — see
        // docs/guide/browser.md on the stale direction that nothing reports. Neither is a contrary
        // claim, so both are accepted, and page one tells them apart rather than printing one
        // sentence that is true of only one of them. See TryAgreeTable.
        membership = new Membership(sheet.CampaignTable, sheet.CampaignId);

        var rung = kind switch
        {
            "hero"    => CombatantKind.Hero,
            "villain" => CombatantKind.Villain,
            "foe"     => CombatantKind.Foe,
            _         => CombatantKind.Extra
        };

        try
        {
            combatant = CombatantFactory.From(
                sheet, _rules, _derived, _play, rung,
                id.Length == 0 ? null : id,
                side.Length == 0 ? null : side,
                size, invisible, hardTarget, ready);

            if (holding.Length > 0) combatant = combatant.Carrying(holding);

            return true;
        }
        // A sheet in the right shape that the engine cannot derive a figure from — an invented
        // Power id, a tier that is not there. It is the caller's, and it arrives as a protocol
        // error a model cannot act on if it is not caught. The other server judges characters;
        // this one only says which figure it could not get.
        catch (Exception e) when (IsCallersFault(e))
        {
            problem = Problem("COMBATANT_UNBUILDABLE",
                $"Combatant {position} is a character this engine could not turn into a combatant: "
                + e.Message
                + " Whether the character is legal is the other server's question — "
                + "prowlers-and-paragons, check_character.");
            return false;
        }
    }

    /// <summary>
    /// The tier the character says it was built to, or a refusal naming it and the ones there are.
    ///
    /// <para><b>A tier this repository does not have is refused rather than costed at nothing.</b>
    /// <c>DerivedStatsCalculator.CalculateResolve</c> answers <b>0</b> for an absent or unresolvable
    /// tier — the right answer for a figure it cannot derive, and a silent lie once that figure is a
    /// combatant in a fight. A Hero at 0 Resolve buys no extra die, no reroll and no stabilise, so a
    /// misspelling (<c>standrad</c>) and an omission both put a quietly weaker character into a
    /// measurement, and nothing in the answer says so. That is the shape
    /// <c>docs/guide/mcp-and-headless.md</c> calls the worst of the three behaviours: accepted,
    /// ignored, and unannounced.</para>
    ///
    /// <para>Whether the character is <em>legal</em> at that tier is still the other server's
    /// question. This one only refuses a tier it cannot look up at all.</para>
    /// </summary>
    private bool TryReadTier(
        CharacterSheet sheet, int position, out string? tier, out JsonObject problem)
    {
        tier = null;
        problem = new JsonObject();

        var wanted = sheet.SelectedTierId?.Trim() ?? "";

        if (wanted.Length > 0 && _rules.GetTier(wanted) is not null)
        {
            tier = wanted;
            return true;
        }

        var known = string.Join(", ", _rules.Tiers.Select(t => t.Id).Order(StringComparer.Ordinal));

        problem = Problem("NO_SUCH_TIER",
            (wanted.Length == 0
                ? $"Combatant {position} names no \"SelectedTierId\"."
                : $"Combatant {position} names the tier '{wanted}', which these rules do not have.")
            + " A tier fixes the Trait Cap, and Ch.5 p.83 measures a Hero's opening Resolve down "
            + "from it — so a tier that cannot be looked up would put a combatant into the fight "
            + "with 0 Resolve and nothing to say the figure was never computed. The tiers are: "
            + known + ".");

        return false;
    }

    private static bool TryReadMinions(
        JsonObject entry, int position, string side, string id, double size, bool invisible,
        bool hardTarget, bool ready, out Combatant combatant, out JsonObject problem)
    {
        combatant = Combatant.Minions("placeholder", "placeholder", 1, 1, "threat");
        problem = new JsonObject();

        var threat = Number(entry, "threat_rank");
        var count = Number(entry, "count");

        if (threat is not { } rank || rank <= 0)
        {
            problem = Problem("BAD_MINIONS",
                $"Combatant {position} is a group of Minions and needs a whole \"threat_rank\" "
                + "above zero. Ch.4 p.77 gives a group one characteristic and that is it.");
            return false;
        }

        if (count is not { } bodies || bodies <= 0)
        {
            problem = Problem("BAD_MINIONS",
                $"Combatant {position} is a group of Minions and needs a whole \"count\" above "
                + "zero — how many of them there are.");
            return false;
        }

        var name = Text(entry, "name").Trim();
        if (name.Length == 0) name = "the Minions";

        combatant = Combatant.Minions(
            id.Length == 0 ? name : id, name, rank, bodies, "threat",
            side.Length == 0 ? Combatant.OpposingSide : side, size, invisible, hardTarget, ready);

        return true;
    }

    /// <summary>
    /// How big a combatant is, for Ch.4 p.75's size bands, or a refusal naming what is wrong with
    /// the figure.
    ///
    /// <para><b>It is a bare number whose only meaning is the ratio between two of them</b>, so any
    /// unit will do as long as one fight uses one: feet, metres, "a person is 1". The default is
    /// that everybody is the same size, which is the only arrangement in which none of p.75's four
    /// bands applies.</para>
    ///
    /// <para><b>A size that is not a real figure above zero is refused rather than taken as the
    /// default.</b> A zero divides, and the infinity that comes out satisfies every band there is —
    /// so a typo would put a standing +2d on somebody's defence with nothing anywhere saying the
    /// value had been thrown away.</para>
    /// </summary>
    private static bool TryReadSize(JsonObject entry, int position, out double size, out JsonObject problem)
    {
        size = Combatant.SameSize;
        problem = new JsonObject();

        if (entry["size"] is not { } given) return true;

        if (given is JsonValue value && value.TryGetValue<double>(out var read)
            && read > 0 && !double.IsNaN(read) && !double.IsInfinity(read))
        {
            size = read;
            return true;
        }

        problem = Problem("BAD_SIZE",
            $"Combatant {position} has a \"size\" of {given.ToJsonString()}. Ch.4 p.75 compares two "
            + "combatants' sizes as a ratio — at least twice, at least 5 times, no more than half, "
            + "no more than one-fifth — so a size is a number above zero in whatever unit this "
            + $"fight is using, and {Combatant.SameSize} means the same size as everybody else. "
            + "Omit it for that.");
        return false;
    }

    /// <summary>
    /// The character as the engine's own shape, or a refusal naming the reason.
    ///
    /// <para><b>Read strictly</b>, exactly as the <c>build</c> command and the character server
    /// read a submitted character: a field name that is not part of a character is refused rather
    /// than ignored, because a misspelled <c>AbilityRanks</c> silently drops every Ability and puts
    /// a weaker combatant into a fight nobody would know was measured wrong.</para>
    ///
    /// <para>A client that sends the character as a JSON <em>string</em> rather than an object is
    /// accommodated: both are the same character, the schema cannot stop either, and refusing one
    /// on a technicality reads to the person as the tool being broken.</para>
    /// </summary>
    private static bool TryReadSheet(
        JsonNode character, int position, out CharacterSheet sheet, out JsonObject problem)
    {
        sheet = new CharacterSheet();
        problem = new JsonObject();

        var text = character is JsonValue value && value.TryGetValue<string>(out var asString)
            ? asString
            : character.ToJsonString();

        if (string.IsNullOrWhiteSpace(text))
        {
            problem = Problem("NO_CHARACTER", $"Combatant {position} carries an empty character.");
            return false;
        }

        try
        {
            if (CharacterSheetJson.Read(text, strict: true) is not { } read)
            {
                problem = Problem("NO_CHARACTER", $"Combatant {position} holds no character.");
                return false;
            }

            sheet = read;
            return true;
        }
        catch (JsonException e)
        {
            var where = e.Path is { } path ? $" at {path}" : "";
            var line = e.LineNumber is { } n ? $", line {n + 1}" : "";

            problem = Problem("CHARACTER_UNREADABLE",
                $"Combatant {position} is not a character in the shape these tools take{where}"
                + $"{line}. A field name that is not part of a character is refused rather than "
                + "ignored, so check the spelling — and a rank is a number, so 8 rather than \"8d\".");
            return false;
        }
        // InvalidOperationException, not JsonException, is what the deserializer throws when asked
        // to put a null into one of the get-only collections — "AbilityRanks": null is well-formed
        // JSON that any hand-written character might carry.
        catch (InvalidOperationException)
        {
            problem = Problem("CHARACTER_UNREADABLE",
                $"Combatant {position} is not a character in the shape these tools take: one of "
                + "its lists is null where it should be absent or an array.");
            return false;
        }
    }

    // ── Reading the table ─────────────────────────────────────────────────

    /// <summary>
    /// Every table switch, by the name a caller passes — the property name in the engine, spelled
    /// the way every other field on this wire is spelled.
    /// </summary>
    public static IReadOnlyList<string> TableSettings { get; } =
        [.. TableRules.Switches.Select(s => Wire(s.Name)).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The table the caller passed, or null where they passed none.
    ///
    /// <para><b>Absent and "the book" are different answers here, and that is the change this
    /// reader needed.</b> It used to hand back <see cref="TableRules.Book"/> for both, which was
    /// fine while the argument was the only source there was. Now that the sheets can carry one,
    /// an omitted argument has to mean <em>the caller said nothing</em> — otherwise every fight
    /// under a campaign's house rules would look like a caller demanding the book, and the
    /// disagreement refusal would fire on every single one of them.</para>
    /// </summary>
    private static bool TryReadTable(JsonElement? table, out TableRules? rules, out JsonObject problem)
    {
        rules = null;
        problem = new JsonObject();

        if (AsNode(table) is not { } node) return true;

        if (node is not JsonObject settings)
        {
            problem = Problem("BAD_TABLE",
                "The table settings are a JSON object of booleans — for example "
                + "{\"wound_penalties\": true}. Omit it for the book's baseline, which is every "
                + "optional Gritty rule off.");
            return false;
        }

        var unknown = settings
            .Select(pair => pair.Key)
            .Where(key => !TableSettings.Contains(key, StringComparer.Ordinal))
            .ToList();

        if (unknown.Count > 0)
        {
            problem = Problem("NO_SUCH_TABLE_SETTING",
                $"The table names {string.Join(", ", unknown)}, which this engine has no switch "
                + "for. A setting accepted and quietly ignored is the worst of the three possible "
                + "behaviours, so an unknown one is refused. The settings are: "
                + string.Join(", ", TableSettings) + ".");
            return false;
        }

        // <b>A switch whose value is not a boolean is refused, not read as off.</b> `true`, 1, "yes"
        // and null all failed `TryGetValue<bool>` and fell through to false — so a table that
        // plainly meant to turn Wound Penalties on measured a game without them, the echo said
        // `false`, and nothing anywhere said the value had been thrown away. That is the same
        // "accepted and quietly ignored" the unknown-key refusal above exists to prevent, one layer
        // in: the key was known and the value was not.
        var rank = Wire(nameof(TableRules.GearLimitRank));

        foreach (var pair in settings)
        {
            var isRank = string.Equals(pair.Key, rank, StringComparison.Ordinal);

            var ok = isRank
                ? pair.Value is JsonValue number && number.TryGetValue<int>(out _)
                : pair.Value is JsonValue flag && flag.TryGetValue<bool>(out _);

            if (ok) continue;

            problem = Problem("BAD_TABLE",
                $"The table sets {pair.Key} to {pair.Value?.ToJsonString() ?? "null"}, and that "
                + $"setting takes {(isRank ? "a whole number of ranks" : "true or false")}. A value "
                + "this engine cannot read is refused rather than taken as off: a setting accepted "
                + "and quietly ignored is the worst of the three possible behaviours, and it is "
                + "worst of all here, where the answer would go on echoing the switch as off while "
                + "the caller believed they had turned it on.");
            return false;
        }

        bool On(string name) => settings[Wire(name)] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

        var gearLimit = Number(settings, rank);

        rules = new TableRules
        {
            ActiveDefensesCost = On(nameof(TableRules.ActiveDefensesCost)),
            CloseRangePenalty = On(nameof(TableRules.CloseRangePenalty)),
            TheDrop = On(nameof(TableRules.TheDrop)),
            FatalDamage = On(nameof(TableRules.FatalDamage)),
            FriendlyFire = On(nameof(TableRules.FriendlyFire)),
            HardTargets = On(nameof(TableRules.HardTargets)),
            RaisedGearLimit = On(nameof(TableRules.RaisedGearLimit)),
            SlowHealing = On(nameof(TableRules.SlowHealing)),
            ToughMinions = On(nameof(TableRules.ToughMinions)),
            WoundPenalties = On(nameof(TableRules.WoundPenalties)),
            GmAlternativeToSeizingInitiative = On(nameof(TableRules.GmAlternativeToSeizingInitiative)),
            CheckingYourSwing = On(nameof(TableRules.CheckingYourSwing)),
            RandomInitiative = On(nameof(TableRules.RandomInitiative)),
            GearLimitRank = gearLimit
        };

        return true;
    }

    private static bool TryReadRange(string? wanted, out RangeBand band, out JsonObject problem)
    {
        band = RangeBand.Close;
        problem = new JsonObject();

        if (string.IsNullOrWhiteSpace(wanted)) return true;

        // <b>By name, exactly as <see cref="TryReadEnum{T}"/> reads every other enum on this wire.</b>
        // `Enum.TryParse` also accepts the *numeral* of a member, and for a plain enum it accepts any
        // numeral at all — so "1" opened a fight at Distant, which is a band nobody named, and "99"
        // opened one at a RangeBand that does not exist, from which `Wire(band.ToString())` echoed
        // back "99" and every range comparison downstream was against an undefined value. There are
        // three range classes on p.73 and none of them is spelled with a digit.
        foreach (var name in Enum.GetNames<RangeBand>())
        {
            if (!string.Equals(Wire(name), wanted.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

            band = Enum.Parse<RangeBand>(name);
            return true;
        }

        problem = Problem("NO_SUCH_RANGE",
            $"'{wanted}' is not a range class. p.73 prints three: "
            + string.Join(", ", Enum.GetNames<RangeBand>().Select(Wire)) + ".");
        return false;
    }

    /// <summary>
    /// What the light is like, by name — read exactly as <see cref="TryReadRange"/> reads a range
    /// class, and for the same reason: <c>Enum.TryParse</c> also accepts a numeral, and a
    /// <c>Visibility</c> that is not one of the three would put every roll in the fight through a
    /// band lookup nothing can answer.
    ///
    /// <para><b>An unreadable value is refused rather than taken as clear air.</b> A scene somebody
    /// meant to fight in the dark, measured in daylight and echoed back as daylight, is the
    /// "accepted and quietly ignored" this server refuses a table setting for.</para>
    /// </summary>
    private static bool TryReadVisibility(string? wanted, out Visibility visibility, out JsonObject problem)
    {
        visibility = Visibility.Clear;
        problem = new JsonObject();

        if (string.IsNullOrWhiteSpace(wanted)) return true;

        foreach (var name in Enum.GetNames<Visibility>())
        {
            if (!string.Equals(Wire(name), wanted.Trim(), StringComparison.OrdinalIgnoreCase)) continue;

            visibility = Enum.Parse<Visibility>(name);
            return true;
        }

        problem = Problem("NO_SUCH_VISIBILITY",
            $"'{wanted}' is not a visibility. Ch.4 p.75 prices two and this engine names the third "
            + "state as well: "
            + string.Join(", ", Enum.GetNames<Visibility>().Select(Wire))
            + ". Clear is the default and is no modifier.");
        return false;
    }

    // ── Reading an intent ─────────────────────────────────────────────────

    /// <summary>The intents this engine takes, by the name a caller passes.</summary>
    public static IReadOnlyList<string> IntentKinds { get; } =
    [
        "attack", "move", "hold", "grapple", "toss", "break_free",
        "spend_resolve", "spend_adversity", "stabilise", "end_turn", "end_page"
    ];

    private static bool TryReadIntent(JsonElement intent, out Intent read, out JsonObject problem)
    {
        read = new EndPage("");
        problem = new JsonObject();

        if (AsNode(intent) is not JsonObject entry)
        {
            problem = Problem("BAD_INTENT",
                "An intent is a JSON object with a \"kind\". The kinds are: "
                + string.Join(", ", IntentKinds) + ".");
            return false;
        }

        var kind = Text(entry, "kind").Trim().ToLowerInvariant();
        var actor = Text(entry, "actor").Trim();

        switch (kind)
        {
            case "attack":
                if (!TryReadEnum<DamageKind>(entry, "damage", DamageKind.Lethal, out var damage, out problem)) return false;
                if (!TryReadEnum<AttackType>(entry, "type", AttackType.Unarmed, out var type, out problem)) return false;
                if (!TryReadEnum<Cover>(entry, "cover", Cover.None, out var cover, out problem)) return false;

                read = new Attack(
                    actor,
                    Text(entry, "target").Trim(),
                    Text(entry, "trait_id").Trim(),
                    damage,
                    type,
                    Text(entry, "effect") is { Length: > 0 } effect ? effect : null,
                    Flag(entry, "all_out"),
                    Flag(entry, "charge"),
                    Flag(entry, "area"),
                    // <b>p.79's team attack, and it was the one flag the reader did not have.</b>
                    // PLAY-POLICY.md tells every conversation to send it; without this line it was
                    // dropped on the floor, the entry's +2d never reached the pool, and
                    // `spend_resolve` naming `team_attack` answered "was not a team attack" for
                    // ever. The spelling guard over that document is scoped to tool arguments, and
                    // the fields of an intent are not among them, so nothing disagreed.
                    Flag(entry, "team"),
                    cover,
                    // <b>Absent and 0 are different, which is why this is not `Number(...) ?? 0`.</b>
                    // Supplying a Structure is the declaration that the shot goes through the
                    // obstacle, and p.75 makes two things follow from it; a missing one has to mean
                    // "at whatever of the target is exposed" rather than "through an obstacle of
                    // Structure 0", which every attack in the book gets through.
                    Number(entry, "cover_structure"),
                    // p.80's Hard Targets: the attacker aims at the weak points instead, which
                    // costs four dice and cancels the doubling for this one shot.
                    Flag(entry, "vulnerable_part"),
                    // p.79's Close Range, and its own exception. A fight here has no equipment in
                    // it, so a thrown weapon is the caller's word or it is nothing at all.
                    Flag(entry, "close_range_only"),
                    // p.76's "use it ... on that same page". Refused unless the actor is holding
                    // exactly this — an opening "holding" and a full grab are the two ways anything
                    // reaches their hands here, so an item this engine does not know about is one a
                    // caller would otherwise conjure into the fight by naming it. Refused too while
                    // a partial grab is being fought over it, which is p.76's "neither can use it".
                    //
                    // <b>And it is what p.87's Gear Limit bites on</b>, on the two weapon rows of
                    // p.75's table: the Trait is capped and the item's Weapon Bonus added to what
                    // is left. See Encounter.GearLimited.
                    Text(entry, "item") is { Length: > 0 } wielded ? wielded : null,
                    // Ch.7 pp.107-108, the other way of saying what is in the way: a printed row
                    // name rather than a figure. Both at once is refused on the ledger rather than
                    // here, because which of two answers to one question a caller meant is a rule's
                    // refusal and not a malformed argument.
                    Text(entry, "cover_scenery") is { Length: > 0 } behind ? behind.Trim() : null);
                return true;

            case "move":
                read = new Move(actor, Text(entry, "toward").Trim(), entry["closer"] is null || Flag(entry, "closer"));
                return true;

            case "hold":
                read = new Hold(actor);
                return true;

            case "grapple":
                if (!TryReadEnum<GrappleMove>(entry, "move", GrappleMove.Grab, out var move, out problem)) return false;
                read = new GrappleIntent(
                    actor, Text(entry, "target").Trim(), move,
                    // p.76 aims a grab at an object and a hold at a person, so a grab that names
                    // nothing is refused on the ledger with nothing rolled. Left null here rather
                    // than refused as a bad argument: it is a rule of the book and the ledger is
                    // where a rule's refusal belongs.
                    Text(entry, "item") is { Length: > 0 } grabbed ? grabbed.Trim() : null);
                return true;

            case "toss":
                read = new Toss(actor, Text(entry, "item").Trim());
                return true;

            case "break_free":
                read = new BreakFree(actor, Text(entry, "trait_id").Trim(), Number(entry, "threshold") ?? 0);
                return true;

            case "spend_resolve":
                if (!TryReadEnum<ResolveSpend>(entry, "spend", null, out var spend, out problem)) return false;
                read = new SpendResolve(
                    actor, spend, Number(entry, "points") ?? 1,
                    Text(entry, "target") is { Length: > 0 } lured ? lured.Trim() : null,
                    // p.78's knockback: what the target hits on the way, by the name Chapter 7
                    // prints for it. A purchase naming nothing is the rule minus its last clause
                    // and says so on the ledger; one naming something no page rates is refused
                    // with nothing spent.
                    Text(entry, "solid_object") is { Length: > 0 } into ? into.Trim() : null);
                return true;

            case "spend_adversity":
                if (!TryReadEnum<AdversitySpend>(entry, "spend", null, out var gm, out problem)) return false;

                ResolveSpend? asResolve = null;
                if (entry["as_resolve"] is not null)
                {
                    if (!TryReadEnum<ResolveSpend>(entry, "as_resolve", null, out var named, out problem)) return false;
                    asResolve = named;
                }

                read = new SpendAdversity(
                    actor, gm, Number(entry, "points") ?? 1, asResolve,
                    Text(entry, "target") is { Length: > 0 } onto ? onto.Trim() : null,
                    // <b>p.85's three own purchases each need the GM's own words, and a field the
                    // reader does not have is a field the SDK drops in silence.</b> That is exactly
                    // how the team flag was lost: the policy told every conversation to send it and
                    // nothing here read it. The spelling guard over that document is scoped to tool
                    // arguments and the fields of an intent are not among them, so each of these is
                    // driven over the wire in McpPlayServerTests instead.
                    Text(entry, "narration") is { Length: > 0 } said ? said.Trim() : null,
                    // The same field p.85's first purchase needs to buy a knockback for an NPC.
                    Text(entry, "solid_object") is { Length: > 0 } struck ? struck.Trim() : null);
                return true;

            case "stabilise":
                read = new Stabilise(actor, Text(entry, "target").Trim());
                return true;

            case "end_turn":
                read = new EndTurn(actor);
                return true;

            case "end_page":
                read = new EndPage(actor);
                return true;

            default:
                problem = Problem("NO_SUCH_INTENT",
                    $"'{kind}' is not an intent this engine takes. The kinds are: "
                    + string.Join(", ", IntentKinds)
                    + ". An intent this engine knows and does not yet resolve is answered with a "
                    + "ledger line saying so by name; this one it does not know at all.");
                return false;
        }
    }

    /// <summary>
    /// One enum-valued field, by the snake_case spelling of its member name.
    /// </summary>
    /// <param name="fallback">What an absent field means, or null where the field is required.</param>
    private static bool TryReadEnum<T>(
        JsonObject entry, string field, T? fallback, out T value, out JsonObject problem)
        where T : struct, Enum
    {
        problem = new JsonObject();
        var wanted = Text(entry, field).Trim();

        if (wanted.Length == 0)
        {
            if (fallback is { } given)
            {
                value = given;
                return true;
            }

            value = default;
            problem = Problem("NO_SUCH_" + field.ToUpperInvariant(),
                $"This intent needs a \"{field}\". The values are: " + Names<T>() + ".");
            return false;
        }

        foreach (var name in Enum.GetNames<T>())
        {
            if (!string.Equals(Wire(name), wanted, StringComparison.OrdinalIgnoreCase)) continue;

            value = Enum.Parse<T>(name);
            return true;
        }

        value = default;
        problem = Problem("NO_SUCH_" + field.ToUpperInvariant(),
            $"'{wanted}' is not a \"{field}\" this engine takes. The values are: " + Names<T>() + ".");
        return false;
    }

    private static string Names<T>() where T : struct, Enum =>
        string.Join(", ", Enum.GetNames<T>().Select(Wire));

    // ── Reporting ─────────────────────────────────────────────────────────

    private static JsonArray Lines(IEnumerable<LedgerLine> lines) =>
    [
        .. lines.Select(l => (JsonNode)new JsonObject
        {
            ["page"]       = l.Page,
            ["actor"]      = l.Actor,
            ["rule"]       = l.Rule,
            ["source_ref"] = l.SourceRef,
            ["text"]       = l.Text
        })
    ];

    private static JsonArray TurnOrder(EncounterState state) =>
    [
        .. state.TurnOrder.Select(id => (JsonNode)new JsonObject
        {
            ["id"]   = id,
            ["name"] = state[id].Name,
            ["kind"] = Wire(state[id].Kind.ToString()),
            ["side"] = state[id].Side,
            ["edge"] = state.EffectiveEdge[id],
            ["size"] = state[id].Size,
            ["invisible"] = state[id].Invisible,
            ["hard_target"] = state[id].HardTarget,
            ["ready"] = state[id].Ready,
            ["conscious_at_zero_or_less"] = state[id].ConsciousAtZeroOrLess
        })
    ];

    /// <summary>
    /// What a client may see of a fight: who is in it, how they are, whose turn it is, and what
    /// is still running on whom. Nothing here is computed — every figure is read off the state the
    /// engine returned, or off the fight it is being stepped by.
    ///
    /// <para><b>The Health a combatant is out of the fight at is the engine's own
    /// <see cref="Encounter.DefeatFloor"/>, not a second lookup of the same entry.</b> This used to
    /// read <c>damage.defeated_at_health</c> for itself, which is the engine's figure spelled again
    /// somewhere the engine cannot see — and <c>defeated</c> on this wire is the one field a client
    /// reads to decide whether the fight is worth another call. Two readings of one rule is one
    /// reading too many.</para>
    ///
    /// <para><b>The seed and the Challenge Level are echoed here as well as on
    /// <c>start_encounter</c>.</b> They are what the fight reproduces from, and a conversation that
    /// has taken twenty turns is a conversation whose opening answer is a long way up: a client that
    /// wants to replay the fight had to go back and find it, and a model summarising one had nothing
    /// in front of it to quote. They come off <see cref="Held"/>, so they are the values the fight is
    /// actually running under rather than the arguments of this call.</para>
    /// </summary>
    private static JsonObject PublicState(Held held, EncounterState state)
    {
        var floor = held.Engine.DefeatFloor;

        return new JsonObject
        {
            ["page"]            = state.Page,
            ["turn_index"]      = state.TurnIndex,
            ["seed"]            = held.Seed,
            ["challenge_level"] = held.ChallengeLevel,
            ["current"]         = state.Current?.Id,
            ["over"]            = state.Over,
            ["adversity"]       = state.Adversity,
            ["visibility"]      = Wire(state.Visibility.ToString()),
            ["turn_order"]      = TurnOrder(state),
            ["holds"]           = Strings(state.Holds),
            ["seized"]          = Strings(state.Seized),
            ["villainy"]        = Strings(state.Villainy),

            ["combatants"] = new JsonArray([
                .. state.TurnOrder.Select(id => state[id]).Select(c => (JsonNode)new JsonObject
                {
                    ["id"]                 = c.Id,
                    ["name"]               = c.Name,
                    ["kind"]               = Wire(c.Kind.ToString()),
                    ["side"]               = c.Side,
                    ["edge"]               = state.EffectiveEdge[c.Id],
                    ["health"]             = c.Kind == CombatantKind.MinionGroup ? null : c.CurrentHealth,
                    ["full_health"]        = c.Kind == CombatantKind.MinionGroup ? null : c.FullHealth,
                    ["minions_left"]       = c.Kind == CombatantKind.MinionGroup ? c.GroupSize : null,
                    ["resolve"]            = c.HoldsResolve ? c.Resolve : null,
                    ["dying"]              = c.Dying,
                    ["defeated_by_effect"] = c.DefeatedByEffect,
                    ["flaw_suppressed"]    = c.SuppressedFlaw,
                    ["size"]               = c.Size,
                    ["invisible"]          = c.Invisible,
                    ["hard_target"]        = c.HardTarget,
                    ["ready"]              = c.Ready,
                    // p.76's full grab: what it put in their hands, the page it was won on, and
                    // whether they have swung it — which is what decides whether the page turn
                    // takes it away again.
                    //
                    // <b>An item nobody won reports no page, and 0 is not that.</b> A combatant may
                    // walk into the fight holding something, which is the caller's word and the
                    // fact a grab is aimed at; p.76's one-page clause is a limit on what a grab
                    // wins, so it never applied to one of these. Publishing the engine's internal
                    // 0 would read as "won on page zero", which is a page no fight has.
                    ["holding"]            = c.Holding is null
                        ? null
                        : new JsonObject
                        {
                            ["item"]         = c.Holding.Name,
                            ["won_on_page"]  = c.Holding.CarriedIn ? null : c.Holding.WonOnPage,
                            ["carried_in"]   = c.Holding.CarriedIn,
                            ["used"]         = c.Holding.Used
                        },
                    // p.80's Slow Healing: on their feet at a Health that would otherwise have
                    // them out, and one point of damage from being out again.
                    ["conscious_at_zero_or_less"] = c.ConsciousAtZeroOrLess,
                    ["defeated"]           = c.Defeated(floor)
                })
            ]),

            ["effects"] = new JsonArray([
                .. state.Effects.Select(e => (JsonNode)new JsonObject
                {
                    ["target"]          = e.Target,
                    ["source"]          = e.Source,
                    ["name"]            = e.Name,
                    ["remaining_pages"] = e.RemainingPages
                })
            ]),

            ["grapples"] = new JsonArray([
                .. state.Grapples.Select(g => (JsonNode)new JsonObject
                {
                    ["holder"] = g.Holder,
                    ["held"]   = g.Held,
                    ["move"]   = Wire(g.Move.ToString()),
                    ["kind"]   = Wire(g.Kind.ToString()),
                    // What the two of them have hold of, for a grab; null for a hold, which is
                    // aimed at a person.
                    ["item"]   = g.Item
                })
            ]),

            // <b>A pair, not a key.</b> `EncounterState.PairKey` joins two ids with a literal NUL —
            // the right choice inside the engine, because it is the one character an id cannot
            // contain — and putting it on the wire shipped `"robot soldier"` as a JSON *member
            // name*. A client that split on a space got one combatant called "robot soldier"; one
            // that echoed the key into a terminal or a log truncated it at the NUL. Neither is a
            // failure anybody would look for, and the engine's private spelling of a key is not
            // something this server has any business publishing. Built from the turn order rather
            // than from the dictionary's keys, so the separator never has to be parsed back out.
            ["ranges"] = new JsonArray([
                .. state.TurnOrder
                    .SelectMany((a, i) => state.TurnOrder.Skip(i + 1).Select(b => (A: a, B: b)))
                    .Select(pair => (JsonNode)new JsonObject
                    {
                        ["a"]    = pair.A,
                        ["b"]    = pair.B,
                        ["band"] = Wire(state.RangeBetween(pair.A, pair.B).ToString())
                    })
            ])
        };
    }

    /// <summary>
    /// Every switch, on or off, by the name a caller passes — <b>the whole list rather than the
    /// ones that are on</b>. A report that printed only what was turned on cannot be told apart
    /// from one produced by a build that had lost a switch, and the reader of a balance figure is
    /// exactly the person who needs to know which game was measured.
    /// </summary>
    /// <param name="source">Where the table came from — see <see cref="TableSource"/>.</param>
    /// <param name="note">The same in a sentence, naming the sheets.</param>
    private JsonObject TableEcho(
        TableRules table, Visibility visibility, TableSource source, string note)
    {
        var echo = new JsonObject();

        // <b>Where the table came from travels inside it, for the reason the light does.</b> The
        // play policy says a rate is quoted with four things and one of them is `table`; two runs
        // whose echoed switches read alike may have got them from different places, and a reader
        // deciding whether a measurement is of their game needs to know whether the settings came
        // off the characters or off an argument somebody typed. `run_encounters` returns no ledger,
        // so this is the only place that answer is written down in a report.
        echo["source"] = Wire(source.ToString());
        echo["source_note"] = note;

        // <b>The light is echoed inside the table and not beside it, and that placement is the
        // point.</b> The play policy says a rate is quoted with four things and one of them is
        // `table`; a fight measured in the dark is a different game from the same fight in
        // daylight, by up to three dice on every attack roll and every dodge in it — so the figure
        // has to travel with the thing a quoter is already told to carry. Ch.4 p.75.
        echo["visibility"] = Wire(visibility.ToString());

        foreach (var name in TableRules.Switches.Select(s => s.Name).Distinct(StringComparer.Ordinal))
        {
            echo[Wire(name)] = string.Equals(name, nameof(TableRules.GearLimitRank), StringComparison.Ordinal)
                ? table.GearLimitRank
                : table.IsOn(name);
        }

        // <b>The Gear Limit in force, as a figure, for the reason the light is echoed here.</b>
        // It is the one table setting that is a number rather than a flag, and the two switches
        // above say only that a rank was set and what it was — not what an attack made with a held
        // item is actually capped at, which is what a reader of a rate needs. p.80 offers the
        // switch, p.87 is the ceiling itself, and TableRules.GearLimit is the one place the two
        // are composed.
        echo["gear_limit"] = new JsonObject
        {
            ["rank"] = table.GearLimit(_play),
            ["raised"] = table.RaisedGearLimit && table.GearLimitRank is not null,
            ["source"] = table.RaisedGearLimit && table.GearLimitRank is not null
                ? "the campaign's table"
                : "gritty_raised_gear_limit.default_rank",
            ["applies_to"] =
                "an attack naming an item the actor holds, on p.75's melee_weapon or ranged_weapon "
                + "row — the item's Weapon Bonus is then added to what is left of the Trait",
            ["note"] =
                "Ch.4 p.80 offers the switch and Ch.6 pp.87-90 is the ceiling and the weapons "
                + "tables. A Power-backed attack and a fist are never capped; see PLAY-POLICY.md."
        };

        echo["on"] = Strings(table.On().Select(Wire));

        echo["not_yet_applied"] = Strings(
            table.On().Where(Encounter.SwitchesNotYetApplied.Contains).Select(Wire));

        return echo;
    }

    // ── Small shared things ───────────────────────────────────────────────

    /// <summary>
    /// A refusal, in the shape every tool here answers with: <c>ok: false</c> and a problem with a
    /// code and a sentence — never an exception across the transport, where the message arrives as
    /// a protocol error a model has no way to act on.
    ///
    /// <para>The same shape the character server uses, spelled again rather than shared: what is
    /// worth sharing between the two servers is where the rules are, and a six-line JSON object is
    /// not a dependency worth having.</para>
    /// </summary>
    private static JsonObject Problem(string code, string message) => new()
    {
        ["ok"] = false,
        ["problem"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        }
    };

    /// <summary>
    /// Whether an exception is something the caller asked for rather than a fault in this program.
    ///
    /// <para>The engine throws <see cref="KeyNotFoundException"/> for a combatant who is not in the
    /// fight, <see cref="InvalidOperationException"/> for a pool a combatant does not hold, and the
    /// argument exceptions for a figure that cannot be what it is. All four are recoverable and all
    /// four are the caller's; anything else is this program's and is left to the transport.</para>
    /// </summary>
    private static bool IsCallersFault(Exception e) =>
        e is KeyNotFoundException or InvalidOperationException
          or ArgumentException or FormatException;

    /// <summary>
    /// The wire spelling of a C# name: <c>WoundPenalties</c> is <c>wound_penalties</c>, and
    /// <c>MeleeWeapon</c> is <c>melee_weapon</c>. One rule for enum members, table settings and
    /// combatant kinds, so a caller who has seen one field has seen them all.
    /// </summary>
    public static string Wire(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return string.Concat(name.Select((c, i) =>
            char.IsUpper(c) && i > 0
                ? "_" + char.ToLowerInvariant(c)
                : char.ToLowerInvariant(c).ToString()));
    }

    private static JsonNode? AsNode(JsonElement element) =>
        element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : JsonNode.Parse(element.GetRawText());

    /// <summary>
    /// The same, for an argument a caller may leave out altogether.
    ///
    /// <para><b>The optional arguments here are <c>JsonElement?</c> and not <c>JsonElement =
    /// default</c>, and that was measured rather than chosen.</b> The SDK builds a tool's schema
    /// from the delegate, and a <c>JsonElement</c> carrying a default value throws
    /// <c>InvalidOperationException</c> out of <c>McpServerTool.Create</c> — which the startup
    /// check turns into "the rules could not be read", a refusal several layers from the cause.
    /// A nullable is the spelling that describes an omitted argument to both.</para>
    /// </summary>
    private static JsonNode? AsNode(JsonElement? element) =>
        element is { } value ? AsNode(value) : null;

    private static string Text(JsonObject entry, string field) =>
        entry[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static int? Number(JsonObject entry, string field) =>
        entry[field] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static bool Flag(JsonObject entry, string field) =>
        entry[field] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static JsonArray Strings(IEnumerable<string> values) =>
        [.. values.Select(v => JsonValue.Create(v))];

    /// <summary>A proportion, rounded to three places — a rate off 5,000 runs has no more in it.</summary>
    private static JsonValue Rate(int count, int runs) =>
        JsonValue.Create(Math.Round((double)count / runs, 3))!;

    /// <summary>A mean, rounded to two places.</summary>
    private static JsonValue Mean(long total, int runs) =>
        JsonValue.Create(Math.Round((double)total / runs, 2))!;

    /// <summary>
    /// A proportion of something that may not have happened at all — a land rate over no exchanges,
    /// a hold rate over no attacks.
    ///
    /// <para><b>It answers null rather than 0.</b> "This Trait landed nothing" and "this Trait was
    /// never swung" are different findings and only one of them is about the character; a report
    /// that printed <c>0.0</c> for both would answer the owner's question about where somebody is
    /// weakest with a figure about a Trait they never used.</para>
    /// </summary>
    private static JsonValue? Ratio(long count, long outOf) =>
        outOf <= 0 ? null : JsonValue.Create(Math.Round((double)count / outOf, 3));

    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true };

    private static string Write(JsonNode report) => report.ToJsonString(Formatting);
}
