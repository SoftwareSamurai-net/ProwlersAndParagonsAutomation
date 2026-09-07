namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// A special effect running on somebody, with the pages it has left.
/// </summary>
/// <param name="Target">Whose it is.</param>
/// <param name="Source">Who put it there.</param>
/// <param name="Name">What it is — the Power that caused it, in the caller's own words.</param>
/// <param name="RemainingPages">
/// How long it still has. p.76: the target is defeated when this reaches their current Health, and
/// breaking free takes half the escaper's net successes off it.
/// </param>
/// <param name="KeptScenes">
/// How many times p.76's <c>keeping_hold</c> has been bought for this effect.
///
/// <para><b>It is a count and not a flag because the entry says the purchase repeats</b> —
/// <c>may_be_repeated_scene_after_scene</c>, one scene further for each point paid. Above zero, the
/// effect's duration is no longer measured in pages at all: it runs "until the end of the following
/// scene", which is past the end of the encounter this engine is stepping, so
/// <see cref="Encounter.Step"/> stops ticking it down.</para>
/// </param>
public sealed record SpecialEffect(
    string Target, string Source, string Name, int RemainingPages, int KeptScenes = 0);

/// <summary>How far apart two combatants are, in the three classes p.73 prints.</summary>
public enum RangeBand
{
    /// <summary>Physical contact out to the distance an ordinary person moves in one page.</summary>
    Close,

    /// <summary>Beyond Close but within reach of most weapons and Powers.</summary>
    Distant,

    /// <summary>Beyond Distant but still close enough to see.</summary>
    Extreme
}

/// <summary>Which of the two grappling states a pair is in (p.76's table has three rows and two of them bind).</summary>
public enum GrappleKind
{
    /// <summary>Both characters are wrestling; neither can move or do anything else.</summary>
    Partial,

    /// <summary>One character has control over the other.</summary>
    Full
}

/// <summary>
/// A hold or a grab in progress, from the winner's point of view.
///
/// <para><b>Which of the two it is has to be recorded, because p.76 gives them different
/// consequences.</b> A full hold is control over a person and leaves them only trying to escape; a
/// full grab is control of an <em>object</em> and restrains nobody. Storing both as one thing made
/// every full grab immobilise its loser — a character who had lost their sword could not dodge.
/// </para>
/// </summary>
/// <param name="Holder">Who has the upper hand, or either of them in a partial.</param>
/// <param name="Held">The other one.</param>
/// <param name="Move">A grab or a hold. An escape is not a state; it ends one.</param>
/// <param name="Kind">Partial or full.</param>
public sealed record Grapple(string Holder, string Held, GrappleMove Move, GrappleKind Kind);

/// <summary>A character's own defences halved by something they chose to do.</summary>
/// <param name="UntilPage">The last page on which the penalty still applies.</param>
/// <param name="ActiveOnly">
/// True where only active defences are halved — charging (p.78) — and false where the passive ones
/// are too, which is going all-out.
/// </param>
public sealed record DefencePenalty(int UntilPage, bool ActiveOnly);

/// <summary>
/// An attack the engine has resolved, kept so that a purchase made after the roll can unpick it.
/// </summary>
/// <param name="Actor">Who attacked.</param>
/// <param name="Target">Who they attacked.</param>
/// <param name="AttackPool">The pool that was thrown, after every bonus and penalty.</param>
/// <param name="AttackSuccesses">What it scored.</param>
/// <param name="DefenceSuccesses">What the defender scored, which is the threshold.</param>
/// <param name="TargetBefore">The target as they were before the outcome landed.</param>
/// <param name="EffectsBefore">The effects list as it was before the outcome landed.</param>
/// <param name="Effect">The special effect the attack inflicts, or null for a damaging attack.</param>
/// <param name="Area">Whether it was an area attack, which doubles the rate against Minions.</param>
/// <param name="Damage">
/// Lethal, subdual or psychic. Kept because re-applying the outcome after a purchase has to know:
/// p.79's dying clock starts on <em>lethal</em> damage, and a rebuilt attack that had forgotten
/// which kind it was would start it on a knockout blow.
/// </param>
/// <param name="AttackFaces">
/// The faces the attack pool actually came up with.
///
/// <para><b>p.79's team attack rerolls a face, which is why <c>IDiceSource</c> answers in faces at
/// all.</b> A source that had handed back a count of successes could not tell an engine which dice
/// were sixes, and "have your 6s explode" would be unimplementable. The list is rewritten as the
/// explosion goes, so the sixes that have already been rerolled cannot be rerolled again.</para>
/// </param>
/// <param name="Team">Whether it was a team attack, which is what the exploding sixes are bought off.</param>
/// <param name="TraitId">The Trait or Power that was rolled, so a redirected attack is the same attack.</param>
/// <param name="Type">
/// Which row of p.75's table the attack came from, which decides what may answer it. p.79's luring
/// sends the attack at somebody else, and the row is what says which defence <em>they</em> get.
/// </param>
/// <param name="DefenceWasActive">
/// Whether the defence that answered it was an active one. p.79's luring is bought off a dodge —
/// <c>requires_an_active_defense</c> — so an engine that had not kept this could not tell a lure
/// from a target who stood there and soaked the blow.
/// </param>
/// <param name="AttackRank">
/// The rank of the Trait that was rolled, before any bonus dice.
///
/// <para><b>It is the rank and not the pool, because p.78's knockback is priced off the rank</b> —
/// "as if they were thrown by someone with a Might rank equal to your attack rank" — and the pool
/// carries the two dice going all-out lends, the Minions' size bonus and the wound penalty, none of
/// which is anybody's rank.</para>
/// </param>
public sealed record ResolvedAttack(
    string Actor,
    string Target,
    int AttackPool,
    int AttackSuccesses,
    int DefenceSuccesses,
    Combatant TargetBefore,
    IReadOnlyList<SpecialEffect> EffectsBefore,
    string? Effect,
    bool Area,
    DamageKind Damage,
    int AttackRank,
    IReadOnlyList<int> AttackFaces,
    bool Team,
    string TraitId,
    AttackType Type,
    bool DefenceWasActive);

/// <summary>
/// The whole of a fight at one instant, immutable.
///
/// <para><b>Immutable because <c>Encounter.Step</c> is a judge and not a simulation loop.</b> Every
/// step returns a new state and the one it was given is untouched, which is what makes a fight
/// replayable, a policy testable and a defect bisectable. A property test compares the serialised
/// state before and after a step and requires them to be identical, because "the input was not
/// modified" is the kind of claim that is true right up until somebody adds a list and reaches for
/// <c>Add</c>.</para>
///
/// <para><b>Ranges are pairwise, because the book's are.</b> p.73 has the GM say what class a fight
/// opens in and characters close or open with each other; there is no board and no single distance
/// from a fixed point. So a band is recorded per unordered pair, and a pair nobody has moved is at
/// whatever <see cref="Encounter.Begin"/> opened on.</para>
/// </summary>
public sealed record EncounterState
{
    /// <summary>Which page of the fight this is. The first is 1.</summary>
    public required int Page { get; init; }

    /// <summary>
    /// The order of action for this page, as ids: p.73's Edge order, its four-rung tie-break, and
    /// the Minions last. Anybody who has seized the initiative is in front of all of it.
    /// </summary>
    public required IReadOnlyList<string> TurnOrder { get; init; }

    /// <summary>Whose turn it is, as an index into <see cref="TurnOrder"/>.</summary>
    public required int TurnIndex { get; init; }

    /// <summary>Everyone in the fight, by id.</summary>
    public required IReadOnlyDictionary<string, Combatant> Combatants { get; init; }

    /// <summary>The GM's pool: one per Hero per issue, plus Challenge Level × Heroes for the scene.</summary>
    public required int Adversity { get; init; }

    /// <summary>Every special effect still running, with what is left of its duration.</summary>
    public required IReadOnlyList<SpecialEffect> Effects { get; init; }

    /// <summary>Who is holding their action, waiting for a cue that may never come (p.73).</summary>
    public required IReadOnlyList<string> Holds { get; init; }

    /// <summary>Who has seized the initiative, and so goes before everyone else from now on (p.73).</summary>
    public required IReadOnlyList<string> Seized { get; init; }

    /// <summary>Every grapple in progress.</summary>
    public required IReadOnlyList<Grapple> Grapples { get; init; }

    /// <summary>How far apart each pair is, keyed by the two ids in a fixed order.</summary>
    public required IReadOnlyDictionary<string, RangeBand> Ranges { get; init; }

    /// <summary>
    /// Each combatant's Edge as this fight counts it: the derived figure, or — where the table took
    /// p.73's optional random initiative — the successes of the one Edge roll made when the fight
    /// started, which stand in "for that battle".
    /// </summary>
    public required IReadOnlyDictionary<string, int> EffectiveEdge { get; init; }

    /// <summary>
    /// How many active defences each combatant has already used this page, which is what the Gritty
    /// rule on p.79 charges for: the first is free and every one after it costs a die more than the
    /// last. Reset when the page turns; empty and unread when that setting is off.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ActiveDefencesThisPage { get; init; }

    /// <summary>
    /// Whose own defences are halved, and until when — what going all-out (p.78) and charging
    /// (p.78) cost the character who did it.
    ///
    /// <para><b>Both are recorded because the bonus without the cost is not the rule.</b> Two extra
    /// dice on an attack that never pays for them would bias every measurement towards attacking
    /// all-out, which is precisely the shape of "confidently wrong" a simulator is worth avoiding.
    /// The page says the penalty lasts "until after your next turn to act"; this engine expires it
    /// at the end of the page after the one it was taken on, which is that sentence to within a
    /// turn and is recorded as a reading in <c>docs/guide/play-engine.md</c>.</para>
    /// </summary>
    public required IReadOnlyDictionary<string, DefencePenalty> DefencesHalved { get; init; }

    /// <summary>
    /// How many pages of movement each combatant has banked towards crossing a range class with
    /// somebody, keyed by the mover and the pair. p.74 prices a range class at two pages, or one
    /// for a Travel Power at the rank the entry names.
    /// </summary>
    public required IReadOnlyDictionary<string, int> MoveProgress { get; init; }

    /// <summary>
    /// The attack just resolved, or null.
    ///
    /// <para><b>It is here because Chapter 5's two commonest purchases are decided after the
    /// roll.</b> A die bought with Resolve and a reroll are both bought with the dice already on the
    /// table (p.84), so the engine has to be able to unpick an outcome it has already applied. What
    /// is kept is exactly what an attack changes — the target as they were, and the effects list as
    /// it was — rather than a whole prior state, so re-applying is a recomputation and not a
    /// rewind.</para>
    /// </summary>
    public ResolvedAttack? LastAttack { get; init; }

    /// <summary>What this table turned on before play.</summary>
    public required TableRules Table { get; init; }

    /// <summary>Everything the engine has done, with its citations.</summary>
    public required Ledger Ledger { get; init; }

    /// <summary>
    /// Who has forfeited a turn they had not yet taken, and so is left out of the next page's order.
    ///
    /// <para><b>p.78's knockback and p.79's luring both take a turn away, and a turn taken away has
    /// to be missing from the order rather than mentioned on the ledger.</b> Where the character had
    /// still to act on the page the forfeit was bought, the turn they lose is that one and they come
    /// straight out of <see cref="TurnOrder"/>; where they had already acted, the turn they lose is
    /// the next page's and their id waits here until <see cref="Encounter.Step"/> builds it.</para>
    /// </summary>
    public required IReadOnlyList<string> LosesNextTurn { get; init; }

    /// <summary>
    /// Everyone who has already been on the receiving end of a team attack this fight.
    ///
    /// <para>p.79: "no character can be subject to more than one team attack per battle", which is a
    /// limit per <em>target</em> and for the whole battle rather than the page — so it is a list on
    /// the encounter and not something the page turn clears. The sentence beside it names the two
    /// ways it is lifted, both of which are a person's decision, so the refusal quotes them rather
    /// than applying them.</para>
    /// </summary>
    public required IReadOnlyList<string> TeamAttacked { get; init; }

    /// <summary>Whether the fight is over — one side left standing, or the page limit reached.</summary>
    public required bool Over { get; init; }

    /// <summary>Whose turn it is now, or null once the page has run out of turns.</summary>
    public Combatant? Current =>
        TurnIndex >= 0 && TurnIndex < TurnOrder.Count ? Combatants[TurnOrder[TurnIndex]] : null;

    /// <summary>One combatant by id, or a throw naming who is actually in the fight.</summary>
    public Combatant this[string id] =>
        Combatants.TryGetValue(id, out var combatant)
            ? combatant
            : throw new KeyNotFoundException(
                $"No combatant '{id}' in this encounter. In it: "
                + string.Join(", ", Combatants.Keys.Order(StringComparer.Ordinal)));

    /// <summary>The key a pair's range band is stored under, in an order that does not depend on who asks.</summary>
    public static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a} {b}" : $"{b} {a}";

    /// <summary>How far apart two combatants are.</summary>
    public RangeBand RangeBetween(string a, string b) =>
        Ranges.TryGetValue(PairKey(a, b), out var band) ? band : RangeBand.Close;

    /// <summary>This state with a combatant replaced.</summary>
    public EncounterState With(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);

        var combatants = new Dictionary<string, Combatant>(Combatants, StringComparer.Ordinal)
        {
            [combatant.Id] = combatant
        };

        return this with { Combatants = combatants };
    }

    /// <summary>This state with two combatants a different distance apart.</summary>
    public EncounterState WithRange(string a, string b, RangeBand band)
    {
        var ranges = new Dictionary<string, RangeBand>(Ranges, StringComparer.Ordinal)
        {
            [PairKey(a, b)] = band
        };

        return this with { Ranges = ranges };
    }
}
