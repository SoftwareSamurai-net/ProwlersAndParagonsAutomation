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
            + "to say: nothing derives them from the sheet.")]
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
        string? openingRange = null)
    {
        if (!TryReadSetup(combatants, table, challengeLevel, seed, openingRange, out var setup, out var problem))
            return Write(problem);

        var engine = new Encounter(_play, new SeededDice(setup.Seed), setup.Table);

        EncounterState state;
        try
        {
            state = engine.Begin(setup.Combatants, setup.ChallengeLevel, setup.Opening);
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
            ["table"]           = TableEcho(setup.Table),
            ["ledger"]          = Lines([.. TierLines(setup), .. state.Ledger.Lines])
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
            "One intent, as a JSON object with a \"kind\" of: attack, move, hold, grapple, "
            + "break_free, spend_resolve, spend_adversity, stabilise, end_turn, end_page. "
            + "For example {\"kind\": \"attack\", \"actor\": \"gatecrasher\", \"target\": "
            + "\"mecha\", \"trait_id\": \"blast\", \"type\": \"physical_power\"}.")]
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

    /// <summary>The policies this server will run, by the name a caller passes.</summary>
    public static IReadOnlyList<string> Policies { get; } = ["attack_the_weakest"];

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
        [Description("Which policy chooses each turn. Currently: attack_the_weakest.")]
        string? policy = null,
        [Description("The page each run stops at if neither side is down. 20 by default.")]
        int? maxPages = null)
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

        var wanted = (policy ?? Policies[0]).Trim().ToLowerInvariant();

        if (!Policies.Contains(wanted, StringComparer.Ordinal))
        {
            return Write(Problem("NO_SUCH_POLICY",
                $"'{policy}' is not a policy this server has. Available: "
                + string.Join(", ", Policies)
                + ". A policy is a guess about how people play, not a rule — its name goes in the "
                + "answer for that reason."));
        }

        if (!TryReadSetup(combatants, table, challengeLevel, seed, openingRange, out var setup, out var problem))
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
        IPolicy chooser = new AttackTheWeakest(_play);

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
    /// N seeded runs of one fight, and what came out of them.
    ///
    /// <para><b>Every rate is printed beside the four things that make it mean anything.</b> The N,
    /// the seeds, the policy's own name and the table's settings are in the same object as the
    /// figures, deliberately — a caller that has to make a second call to find out what a number
    /// was measured under will quote the number on its own.</para>
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

        var openingResolve = setup.Combatants.Sum(c => (long)c.Resolve);

        long totalPages = 0;
        long adversitySpent = 0;
        var draws = 0;

        for (var run = 0; run < runs; run++)
        {
            var seed = setup.Seed + run;
            var engine = new Encounter(_play, new SeededDice(seed), setup.Table);
            var state = engine.Begin(setup.Combatants, setup.ChallengeLevel, setup.Opening);

            var opened = state.Adversity;

            state = engine.RunToEnd(state, policy, maxPages);

            var floor = engine.DefeatFloor;

            totalPages += state.Page;
            adversitySpent += opened - state.Adversity;

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
            }
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
            bySide.Add(new JsonObject
            {
                ["side"]                   = side,
                ["win_rate"]               = Rate(wins[side], runs),
                ["mean_health_remaining"]  = holdsHealth[side] ? Mean(healthBySide[side], runs) : null,
                ["mean_resolve_spent"]     = Mean(openingBySide[side] * runs - resolveSpentBySide[side], runs)
            });
        }

        var byCombatant = new JsonArray();

        foreach (var combatant in setup.Combatants)
        {
            byCombatant.Add(new JsonObject
            {
                ["id"]                    = combatant.Id,
                ["name"]                  = combatant.Name,
                ["kind"]                  = Wire(combatant.Kind.ToString()),
                ["side"]                  = combatant.Side,
                ["defeat_rate"]           = Rate(defeats[combatant.Id], runs),
                ["mean_health_remaining"] = combatant.Kind == CombatantKind.MinionGroup
                    ? null
                    : Mean(healthLeft[combatant.Id], runs),
                ["mean_minions_remaining"] = combatant.Kind == CombatantKind.MinionGroup
                    ? Mean(groupLeft[combatant.Id], runs)
                    : null
            });
        }

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
                ["id"]   = "attack_the_weakest",
                ["name"] = policy.Name,
                ["note"] = "a policy is a guess about how people play, not a rule — this figure is "
                           + "about a party that plays this way"
            },
            ["table"]           = TableEcho(setup.Table),

            ["challenge_level"] = setup.ChallengeLevel,
            ["opening_range"]   = Wire(setup.Opening.ToString()),
            ["max_pages"]       = maxPages,

            ["mean_pages"]           = Mean(totalPages, runs),
            ["draw_rate"]            = Rate(draws, runs),
            ["mean_adversity_spent"] = Mean(adversitySpent, runs),
            ["opening_resolve"]      = openingResolve,

            ["by_side"]      = bySide,
            ["by_combatant"] = byCombatant
        };
    }

    // ── Reading a setup ───────────────────────────────────────────────────

    /// <summary>Everything a fight needs before it can be opened.</summary>
    /// <param name="Tiers">
    /// The tier each character combatant was built to, by combatant id — the input the character
    /// engine derives Resolve from, kept so that the opening ledger can say which one was used. A
    /// group of Minions has no sheet and so no entry.
    /// </param>
    private sealed record Setup(
        IReadOnlyList<Combatant> Combatants,
        IReadOnlyDictionary<string, string> Tiers,
        TableRules Table,
        int ChallengeLevel,
        int Seed,
        RangeBand Opening);

    private static readonly IReadOnlyDictionary<string, string> NoTiers =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private bool TryReadSetup(
        JsonElement combatants,
        JsonElement? table,
        int? challengeLevel,
        int? seed,
        string? openingRange,
        out Setup setup,
        out JsonObject problem)
    {
        setup = new Setup([], NoTiers, TableRules.Book, 0, 0, RangeBand.Close);
        problem = new JsonObject();

        if (!TryReadTable(table, out var rules, out problem)) return false;
        if (!TryReadRange(openingRange, out var opening, out problem)) return false;

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

        if (!TryReadCombatants(combatants, out var everyone, out var tiers, out problem)) return false;

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

        setup = new Setup(everyone, tiers, rules, challengeLevel ?? 0, seed ?? 0, opening);
        return true;
    }

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

    private bool TryReadCombatants(
        JsonElement combatants,
        out IReadOnlyList<Combatant> everyone,
        out IReadOnlyDictionary<string, string> tiers,
        out JsonObject problem)
    {
        everyone = [];
        tiers = NoTiers;
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
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject entry)
            {
                problem = Problem("BAD_COMBATANT", $"Combatant {i + 1} is not a JSON object.");
                return false;
            }

            if (!TryReadCombatant(entry, i + 1, out var combatant, out var tier, out problem)) return false;

            if (tier is not null) byId[combatant.Id] = tier;

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
        return true;
    }

    /// <summary>The kinds a combatant may be, on the wire.</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["hero", "villain", "foe", "extra", "minions"];

    private bool TryReadCombatant(
        JsonObject entry, int position, out Combatant combatant, out string? tier, out JsonObject problem)
    {
        combatant = Combatant.Extra("placeholder", "placeholder", 0, 1,
            new Dictionary<string, int>(StringComparer.Ordinal), []);
        tier = null;
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

        if (string.Equals(kind, "minions", StringComparison.Ordinal))
            return TryReadMinions(entry, position, side, id, out combatant, out problem);

        if (entry["character"] is not { } character)
        {
            problem = Problem("NO_CHARACTER",
                $"Combatant {position} is a {kind} and carries no \"character\". Pass the "
                + "character's inputs — the shape the character server's creation_guide describes.");
            return false;
        }

        if (!TryReadSheet(character, position, out var sheet, out problem)) return false;
        if (!TryReadTier(sheet, position, out tier, out problem)) return false;

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
                side.Length == 0 ? null : side);

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
        JsonObject entry, int position, string side, string id, out Combatant combatant, out JsonObject problem)
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
            side.Length == 0 ? Combatant.OpposingSide : side);

        return true;
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

    private static bool TryReadTable(JsonElement? table, out TableRules rules, out JsonObject problem)
    {
        rules = TableRules.Book;
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

    // ── Reading an intent ─────────────────────────────────────────────────

    /// <summary>The intents this engine takes, by the name a caller passes.</summary>
    public static IReadOnlyList<string> IntentKinds { get; } =
    [
        "attack", "move", "hold", "grapple", "break_free",
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
                    Flag(entry, "team"));
                return true;

            case "move":
                read = new Move(actor, Text(entry, "toward").Trim(), entry["closer"] is null || Flag(entry, "closer"));
                return true;

            case "hold":
                read = new Hold(actor);
                return true;

            case "grapple":
                if (!TryReadEnum<GrappleMove>(entry, "move", GrappleMove.Grab, out var move, out problem)) return false;
                read = new GrappleIntent(actor, Text(entry, "target").Trim(), move);
                return true;

            case "break_free":
                read = new BreakFree(actor, Text(entry, "trait_id").Trim(), Number(entry, "threshold") ?? 0);
                return true;

            case "spend_resolve":
                if (!TryReadEnum<ResolveSpend>(entry, "spend", null, out var spend, out problem)) return false;
                read = new SpendResolve(
                    actor, spend, Number(entry, "points") ?? 1,
                    Text(entry, "target") is { Length: > 0 } lured ? lured.Trim() : null);
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
                    Text(entry, "narration") is { Length: > 0 } said ? said.Trim() : null);
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
            ["edge"] = state.EffectiveEdge[id]
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
            ["turn_order"]      = TurnOrder(state),
            ["holds"]           = Strings(state.Holds),
            ["seized"]          = Strings(state.Seized),

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
                    ["kind"]   = Wire(g.Kind.ToString())
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
    private static JsonObject TableEcho(TableRules table)
    {
        var echo = new JsonObject();

        foreach (var name in TableRules.Switches.Select(s => s.Name).Distinct(StringComparer.Ordinal))
        {
            echo[Wire(name)] = string.Equals(name, nameof(TableRules.GearLimitRank), StringComparison.Ordinal)
                ? table.GearLimitRank
                : table.IsOn(name);
        }

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

    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true };

    private static string Write(JsonNode report) => report.ToJsonString(Formatting);
}
