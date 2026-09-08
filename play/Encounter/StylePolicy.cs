using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// A <see cref="PlayStyle"/> and a <see cref="Targeting"/>, composed into one <see cref="IPolicy"/>.
///
/// <para><b>Every rule each style uses is written down in the subclass's own doc comment, and each
/// is allowed to be simple as long as it is honest.</b> A policy is a guess about how people play
/// and not a rule the book prints — <see cref="IPolicy"/> says so — so the thing that makes a
/// measurement worth anything is that a reader can see exactly what the guess was and argue with
/// it. What is never acceptable is a rule that reads as cleverer than it is: <see cref="MinMax"/>
/// spends "whenever a spend flips an outcome it can see", and every one of the outcomes it can see
/// is listed on it.</para>
///
/// <para><b>The two axes are composed rather than multiplied out.</b> Four styles and three target
/// selectors are twelve policies; they are one class with two fields because who a side swings at
/// is independent of how freely it spends, and writing twelve would be writing the same target
/// selection out four times.</para>
///
/// <para><b>Every spend goes through <see cref="IPolicy.AfterRoll"/> and never through
/// <see cref="IPolicy.Choose"/>, and that is a decision with a cost attached.</b>
/// <c>Encounter.RunToEnd</c> gives a policy one choice and one follow-up per turn, and a purchase
/// returned from <see cref="Choose"/> would be the turn: the character would buy their place at the
/// front of the order and not swing at anybody. Buying afterwards costs nothing and reaches every
/// purchase Chapter 5 prices after the roll (p.84 says both of the commonest are decided with the
/// dice on the table). What it cannot reach is a purchase that has to be made <em>before</em> a
/// roll, and no purchase in Chapters 4 or 5 is one.</para>
///
/// <para><b>Whose money pays is decided by the kind of character, not by the style.</b> Only a Hero
/// holds Resolve (Ch.2, and <see cref="Combatant.HoldsResolve"/> makes it unconstructible for
/// anybody else), so an NPC's purchase is p.85's first Adversity spend — "whatever a point of
/// Resolve could have done, on behalf of any NPC" — out of the GM's pool. A group of Minions never
/// buys anything: p.73 and p.77 refuse them four of the ten purchases outright, and the engine's
/// refusals would be the whole of what a Minion policy produced.</para>
/// </summary>
public abstract class StylePolicy : IPolicy
{
    /// <summary>The play rules — where the Traits, the damage rate and the dice model come from.</summary>
    protected PlayRulesRepository Play { get; }

    /// <summary>Which opponent this policy goes after.</summary>
    public Targeting Targeting { get; }

    /// <param name="play">The play rules.</param>
    /// <param name="targeting">Which opponent to go after.</param>
    protected StylePolicy(PlayRulesRepository play, Targeting targeting)
    {
        Play = play ?? throw new ArgumentNullException(nameof(play));
        Targeting = targeting;
    }

    /// <summary>Which style this is.</summary>
    public abstract PlayStyle Style { get; }

    /// <summary>The wire id a caller passes to ask for this style.</summary>
    public string Id => WireOf(Style);

    /// <summary>
    /// One sentence saying what this policy assumes about the people playing — carried into every
    /// report's echo, because a rate measured under a guess has to arrive with the guess.
    /// </summary>
    public abstract string Note { get; }

    /// <inheritdoc/>
    public string Name => $"{Id} targeting {WireOf(Targeting)}";

    // ── Names on the wire ─────────────────────────────────────────────────

    /// <summary>The wire spelling of a style: <c>ManoAMano</c> is <c>mano_a_mano</c>.</summary>
    public static string WireOf(PlayStyle style) => Snake(style.ToString());

    /// <summary>The wire spelling of a target selector.</summary>
    public static string WireOf(Targeting targeting) => Snake(targeting.ToString());

    /// <summary>Every style a seeded run can be asked for, in the order they are listed.</summary>
    public static IReadOnlyList<string> StyleIds { get; } =
        [.. Enum.GetValues<PlayStyle>().Select(WireOf)];

    /// <summary>Every target selector, in the order they are listed.</summary>
    public static IReadOnlyList<string> TargetingIds { get; } =
        [.. Enum.GetValues<Targeting>().Select(WireOf)];

    /// <summary>
    /// The name the owner's fifth style goes by, refused by name rather than quietly missing.
    ///
    /// <para>It is a constant here and not a string in the server so that the sentence a caller is
    /// given and the sentence <c>PLAY-POLICY.md</c> is held to are the same words.</para>
    /// </summary>
    public const string NarrativeStyle = "narrative";

    /// <summary>Why <see cref="NarrativeStyle"/> is not on <see cref="StyleIds"/>.</summary>
    public const string NarrativeIsNotSeeded =
        "narrative is not a seeded style and there is deliberately no fake one. It means the "
        + "Villain acting befitting their character, with their Flaws coming up — and nothing in "
        + "this engine makes a Flaw bite (p.85 hands \"whenever the opportunity presents itself\" "
        + "to the GM, and the suppression spend it sells says the same), while \"befitting\" is a "
        + "judgement rather than a comparison. Run it live with take_turn, with the model as the "
        + "Villain. A seeded policy pretending to it would put a name on a measurement that was "
        + "measuring something else.";

    /// <summary>The wire id, or false where nothing is spelled that way.</summary>
    public static bool TryReadStyle(string? id, out PlayStyle style)
    {
        style = PlayStyle.Standard;

        foreach (var candidate in Enum.GetValues<PlayStyle>())
        {
            if (!string.Equals(WireOf(candidate), id, StringComparison.Ordinal)) continue;

            style = candidate;
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="TryReadStyle"/>
    public static bool TryReadTargeting(string? id, out Targeting targeting)
    {
        targeting = Targeting.Weakest;

        foreach (var candidate in Enum.GetValues<Targeting>())
        {
            if (!string.Equals(WireOf(candidate), id, StringComparison.Ordinal)) continue;

            targeting = candidate;
            return true;
        }

        return false;
    }

    /// <summary>The policy for a style and a target selector.</summary>
    public static StylePolicy For(PlayStyle style, Targeting targeting, PlayRulesRepository play) =>
        style switch
        {
            PlayStyle.ManoAMano => new ManoAMano(play, targeting),
            PlayStyle.Standard => new Standard(play, targeting),
            PlayStyle.MinMax => new MinMax(play, targeting),
            PlayStyle.Reckless => new Reckless(play, targeting),

            // No default that shrugs: a member added to the enum and not to this switch is a style
            // a caller can name and nothing can run, and a throw here is what makes that a build
            // that fails rather than a fight resolved under whichever style came first.
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "No policy for this style.")
        };

    /// <summary>The note for a style, without building a policy to ask it.</summary>
    public static string NoteFor(PlayStyle style, PlayRulesRepository play) =>
        For(style, Targeting.Weakest, play).Note;

    // ── Choosing ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public virtual Intent Choose(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        var enemies = Enemies(state, actor);
        var trait = AttackOptions.BestFor(Play, actor);

        if (enemies.Count == 0 || trait is null) return new Hold(actor.Id);

        return Aim(state, actor, Pick(state, actor, enemies), trait);
    }

    /// <summary>
    /// The attack this style makes on the target its selector picked. The base is a plain attack;
    /// a style that goes all-out or leads a team attack overrides this and says why.
    /// </summary>
    protected virtual Intent Aim(EncounterState state, Combatant actor, Combatant target, string trait) =>
        new Attack(actor.Id, target.Id, trait);

    /// <inheritdoc/>
    public virtual Intent? AfterRoll(EncounterState state, Combatant actor) => null;

    // ── Picking a target ──────────────────────────────────────────────────

    /// <summary>
    /// Everybody on the other side who is still in the fight.
    ///
    /// <para><b>The side is <see cref="Combatant.Side"/> and never <see cref="CombatantKind"/>.</b>
    /// p.73 prints a fight between Heroes and a Villain's Minions can stand against a Foe; reading
    /// the kind for it would make both invisible to every policy here.</para>
    /// </summary>
    protected static IReadOnlyList<Combatant> Enemies(EncounterState state, Combatant actor) =>
    [
        .. state.Combatants.Values
            .Where(c => !string.Equals(c.Side, actor.Side, StringComparison.Ordinal))
            .Where(c => !c.Defeated(0) || (c.Kind == CombatantKind.MinionGroup && c.GroupSize > 0))
    ];

    /// <summary>
    /// Which of them this policy's selector goes after. Ties break on the id, so a seeded run is
    /// the same run twice.
    /// </summary>
    protected Combatant Pick(EncounterState state, Combatant actor, IReadOnlyList<Combatant> enemies)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(enemies);

        return Targeting switch
        {
            Targeting.Weakest => enemies
                .OrderBy(Standing)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .First(),

            Targeting.Strongest => enemies
                .OrderByDescending(Standing)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .First(),

            // <b>Damage first and rank only as the opening guess.</b> The book has no threat
            // figure a policy could read, and the two halves answer the same question at different
            // points in a fight: before anybody has landed anything the only evidence is what the
            // sheets could do, and after that the evidence is what they have actually done.
            Targeting.HighestThreat => Threatening(state, enemies),

            _ => throw new ArgumentOutOfRangeException(nameof(enemies), Targeting, "No such selector.")
        };
    }

    /// <summary>
    /// How much of a combatant is left: Health for a character, bodies for a group of Minions.
    ///
    /// <para>p.77 gives a group one characteristic and no Health, so the count is what "weakest"
    /// can mean for them — the same reading <see cref="AttackTheWeakest"/> already makes.</para>
    ///
    /// <para><b>The two figures are then ranked against each other unconverted, and that is a
    /// reading rather than arithmetic.</b> There is no exchange rate between a body and a point of
    /// Health anywhere in Chapters 3–5 — p.77 gives a group no Health at all, which is exactly why
    /// there is nothing to convert — and a selector still has to answer. This one answers that a
    /// group of three bodies has more left standing than a character on two Health, so
    /// <see cref="Targeting.Weakest"/> finishes the character before it starts thinning the mob and
    /// <see cref="Targeting.Strongest"/> goes at the mob first. Driven by a fixture that puts
    /// exactly those two in one fight, and recorded in <c>docs/guide/play-engine.md</c>'s readings
    /// table beside the rest of this engine's.</para>
    /// </summary>
    protected static int Standing(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);

        return combatant.Kind == CombatantKind.MinionGroup
            ? combatant.GroupSize
            : combatant.CurrentHealth;
    }

    private Combatant Threatening(EncounterState state, IReadOnlyList<Combatant> enemies)
    {
        var dealt = LedgerReading.DamageDealtSoFar(state.Ledger);

        return enemies
            .OrderByDescending(c => dealt.GetValueOrDefault(c.Id))
            .ThenByDescending(BestRank)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .First();
    }

    /// <summary>The greatest rank this combatant could attack with, or nothing where they have none.</summary>
    protected int BestRank(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);

        return AttackOptions.BestFor(Play, combatant) is { } trait ? combatant.Rank(trait) : 0;
    }

    // ── Spending ──────────────────────────────────────────────────────────

    /// <summary>
    /// How many points this combatant's side could spend on them right now: a Hero's own Resolve,
    /// or the GM's pool for anybody else. Zero for a group of Minions, who buy nothing here.
    /// </summary>
    protected static int PoolFor(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.Kind == CombatantKind.MinionGroup) return 0;

        return actor.HoldsResolve ? actor.Resolve : state.Adversity;
    }

    /// <summary>
    /// One purchase, charged to whichever pool this combatant's side has.
    ///
    /// <para>p.85's first Adversity spend is the Resolve purchases with the GM's money behind them,
    /// so a policy names the purchase once and the kind of character decides who pays.</para>
    /// </summary>
    protected static Intent Buy(Combatant actor, ResolveSpend spend, int points = 1)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.HoldsResolve
            ? new SpendResolve(actor.Id, spend, points)
            : new SpendAdversity(actor.Id, AdversitySpend.AnythingResolveCan, points, spend);
    }

    /// <summary>
    /// How far the last attack fell short of the defence that answered it, or nothing where it
    /// landed or where there is no attack of this actor's on the table.
    ///
    /// <para>p.75 measures every outcome as the successes less the threshold, so what a roll needed
    /// is one success more than the defence scored.</para>
    /// </summary>
    protected static int Shortfall(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        if (state.LastAttack is not { } last
            || !string.Equals(last.Actor, actor.Id, StringComparison.Ordinal))
        {
            return 0;
        }

        return Math.Max(0, last.DefenceSuccesses + 1 - last.AttackSuccesses);
    }

    /// <summary>
    /// Whether seizing the initiative would actually move this combatant, which is the condition
    /// two of these styles buy it on.
    ///
    /// <para><b>Three things have to hold and each is p.73's.</b> They have not seized already —
    /// the purchase lasts the rest of the fight, so a second one buys nothing. They are not a group
    /// of Minions — <c>minions_act</c> puts a group after everybody as the outermost key of the
    /// order, so the point would leave the pool and the order would not move, and the engine refuses
    /// the purchase for exactly that reason. And somebody who is still standing is ahead of them in
    /// the order this page.</para>
    /// </summary>
    protected static bool SeizingWouldMove(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.Kind == CombatantKind.MinionGroup) return false;
        if (state.Seized.Contains(actor.Id, StringComparer.Ordinal)) return false;

        var mine = state.TurnOrder.ToList().IndexOf(actor.Id);

        if (mine <= 0) return false;

        return state.TurnOrder.Take(mine).Any(id =>
            state.Combatants.TryGetValue(id, out var other) && !other.Defeated(0));
    }

    /// <summary>
    /// The greatest Edge on the other side, as this fight counts it — which is what "the opponents
    /// seem faster or stronger" is read as.
    ///
    /// <para><b>It is <see cref="EncounterState.EffectiveEdge"/> rather than the sheet's figure</b>,
    /// because that is the figure the fight is actually ordered on: p.73's optional random
    /// initiative replaces it for the battle, and p.79's Drop doubles it for anybody with a weapon
    /// levelled. A policy comparing the sheets would be comparing something the order does not use.
    /// </para>
    /// </summary>
    protected static int BestEnemyEdge(EncounterState state, Combatant actor)
    {
        var enemies = Enemies(state, actor);

        return enemies.Count == 0
            ? 0
            : enemies.Max(c => state.EffectiveEdge.GetValueOrDefault(c.Id));
    }

    /// <summary>This combatant's Edge as the fight counts it.</summary>
    protected static int EdgeOf(EncounterState state, Combatant actor)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(actor);

        return state.EffectiveEdge.GetValueOrDefault(actor.Id);
    }

    private static string Snake(string name) =>
        string.Concat(name.Select((c, i) =>
            char.IsUpper(c) && i > 0
                ? "_" + char.ToLowerInvariant(c)
                : char.ToLowerInvariant(c).ToString()));
}
