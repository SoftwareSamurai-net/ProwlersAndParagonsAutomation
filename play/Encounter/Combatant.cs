namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// What kind of character a combatant is, which is what Ch.4 p.73's tie-break ladder sorts on and
/// what decides whether Health is halved, absent, or as written.
///
/// <para><b>The caller says which; nothing derives it.</b> A character sheet carries a Hero/Villain
/// field, and it is presentation — a palette — which no rules code may read, and
/// <c>PresentationFlagsTests</c> scans this project to be sure none does. The encounter is told
/// instead. That is also the only honest arrangement: the same sheet is a Villain in one GM's game
/// and a Foe in another's, and the ladder has five rungs where the flag has two.</para>
/// </summary>
public enum CombatantKind
{
    /// <summary>A player's character. The only kind that holds Resolve (Ch.5 p.84).</summary>
    Hero,

    /// <summary>A named antagonist, built by the Hero rules and with the same Health formula.</summary>
    Villain,

    /// <summary>Ch.4 p.75: "Foes halve the result" — half a Villain's Health, and no Resolve.</summary>
    Foe,

    /// <summary>A group of Minions, acting as one character with one number: Threat (p.77).</summary>
    MinionGroup,

    /// <summary>Anybody else on the ladder's bottom rung.</summary>
    Extra
}

/// <summary>
/// An immutable snapshot of one participant in a fight.
///
/// <para><b>It is a snapshot, not a view of a character sheet.</b> The encounter never holds a
/// <c>CharacterSheet</c>, never mutates one, and cannot: <see cref="CombatantFactory"/> is the one
/// place a sheet is read, and what comes out is this. That is what makes
/// <c>Encounter.Step</c> a pure function of its arguments, and it is checked from the other side
/// too — a fixture round-trips a sheet through the exporter before and after a whole encounter and
/// requires the bytes to be identical.</para>
///
/// <para><b>Only a Hero can hold Resolve, and the type is what says so.</b> Ch.2 says it twice and
/// Ch.5 gives the GM Adversity instead; the character engine computes the figure for everybody and
/// it is noise on a Villain. Here it is not merely noise, it is unconstructible: the constructor is
/// private and <see cref="Hero"/> is the only factory that takes a Resolve pool.
/// <see cref="Spending"/> throws on anyone else, so a spend written against the wrong kind of
/// combatant fails loudly rather than quietly drawing on a pool that does not exist.</para>
/// </summary>
public sealed class Combatant
{
    private Combatant(
        string id,
        string name,
        CombatantKind kind,
        string side,
        int edge,
        int fullHealth,
        int currentHealth,
        int resolve,
        int groupSize,
        IReadOnlyDictionary<string, int> traitRanks,
        IReadOnlyList<string> defences,
        string? defeatedByEffect,
        bool dying,
        int instantRecoveriesUsed,
        string? suppressedFlaw,
        double size,
        bool invisible,
        bool hardTarget,
        IReadOnlySet<string> powers)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Side = side;
        Edge = edge;
        FullHealth = fullHealth;
        CurrentHealth = currentHealth;
        Resolve = resolve;
        GroupSize = groupSize;
        TraitRanks = traitRanks;
        Defences = defences;
        DefeatedByEffect = defeatedByEffect;
        Dying = dying;
        InstantRecoveriesUsed = instantRecoveriesUsed;
        SuppressedFlaw = suppressedFlaw;
        Size = size;
        Invisible = invisible;
        HardTarget = hardTarget;
        Powers = powers;
    }

    /// <summary>The side the book's own fights are written from: the player characters'.</summary>
    public const string HeroSide = "heroes";

    /// <summary>The side everything the book's fights point at is on, by default.</summary>
    public const string OpposingSide = "villains";

    /// <summary>
    /// The size everybody is unless somebody says otherwise — <b>the figure that makes every one of
    /// p.75's size bands not apply</b>, because a ratio of one is neither twice nor half anything.
    /// </summary>
    public const double SameSize = 1;

    /// <summary>The Powers a combatant nobody built from a sheet is carrying: none that can be read.</summary>
    private static readonly IReadOnlySet<string> EmptyPowers =
        new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The id the encounter refers to this combatant by.</summary>
    public string Id { get; }

    /// <summary>The name a ledger line prints.</summary>
    public string Name { get; }

    /// <summary>Which rung of p.73's ladder this combatant is on.</summary>
    public CombatantKind Kind { get; }

    /// <summary>
    /// Whose side this combatant is on, as a free-form name the caller chooses.
    ///
    /// <para><b>It is a field because it is not derivable, and deriving it was a defect.</b> Nothing
    /// in Chapters 3–5 says who is on whose side — p.73's ladder is about precedence, not teams — so
    /// an engine that read <see cref="Kind"/> for it was answering a different question, and it got
    /// two printed cases wrong: p.73's Heroes fighting each other (which the page names, and which
    /// that reading made unresolvable), and a Villain's Minions against a Foe. <see cref="Kind"/>
    /// still decides tie order, Health and who holds Resolve; this decides who is fighting whom, and
    /// nothing else may.</para>
    ///
    /// <para>The factories default to <see cref="HeroSide"/> for a Hero and
    /// <see cref="OpposingSide"/> for everybody else, which is the arrangement every fight the book
    /// works through happens to have. A caller who wants another says so.</para>
    /// </summary>
    public string Side { get; }

    /// <summary>Ch.4 p.73's order of action, already derived by the character engine.</summary>
    public int Edge { get; }

    /// <summary>Health as built. Fatal Damage measures the killing line from this, not from what is left.</summary>
    public int FullHealth { get; }

    /// <summary>Health now. Defeat is at the figure <c>damage.defeated_at_health</c> names.</summary>
    public int CurrentHealth { get; }

    /// <summary>The Resolve pool, which is zero for everyone but a Hero and cannot be otherwise.</summary>
    public int Resolve { get; }

    /// <summary>How many Minions are left in the group; zero for anybody who is not one.</summary>
    public int GroupSize { get; }

    /// <summary>
    /// Every Trait rank the encounter may roll, keyed by the id it has in the character rules —
    /// the six Abilities, the twelve Talents, and each Power at its effective rank.
    /// </summary>
    public IReadOnlyDictionary<string, int> TraitRanks { get; }

    /// <summary>
    /// The Trait ids this combatant may answer an attack with, in no particular order.
    /// <c>active_and_passive_defenses</c> says one defence answers each attack and that it is
    /// normally the largest, which is what <c>Encounter</c> does with this list.
    /// </summary>
    public IReadOnlyList<string> Defences { get; }

    /// <summary>
    /// The special effect that has put this combatant out for the scene, or null.
    ///
    /// <para><b>p.76's second way to be defeated, and it is not a Health total.</b> Where a special
    /// effect's duration reaches the target's current Health the target is out "for the rest of the
    /// scene" — an Ensnare or a Mind Control long enough to end a fight ends it without doing a point
    /// of damage. Recording it as Health would be a lie about how they went down, and a lie the wound
    /// penalties and the Fatal Damage threshold would both go on to read.</para>
    /// </summary>
    public string? DefeatedByEffect { get; }

    /// <summary>
    /// Whether lethal damage has this combatant bleeding out — p.79's Fatal Damage clock, which runs
    /// at <c>dying_damage_per_page</c> a page until it is stopped or they die.
    ///
    /// <para>Only reachable with that table setting on: it is the rule that lets Health go below the
    /// defeat figure at all.</para>
    /// </summary>
    public bool Dying { get; }

    /// <summary>
    /// Whether the clock has stopped — the <c>instant_recovery_requires_being_stable</c> gate, and
    /// the other half of <c>dying_ends_at: "stabilization or death"</c>. A character who was never
    /// dying is stable.
    /// </summary>
    public bool Stable => !Dying;

    /// <summary>How many instant recoveries this combatant has taken, against p.76's one a scene.</summary>
    public int InstantRecoveriesUsed { get; }

    /// <summary>
    /// The Flaw the GM has bought this character out of for the rest of the scene (Ch.5 p.85), in
    /// the GM's own words, or null.
    ///
    /// <para><b>It is the whole of what that purchase leaves behind, and it is read rather than
    /// kept for the look of it.</b> Nothing in <c>play/</c> makes a Flaw bite — an NPC's Flaws
    /// "come into play whenever the opportunity presents itself", which is the GM's judgement and
    /// not a roll — so the suppression itself is narration. What is not narration is the printed
    /// limit beside it: "no character can benefit from this more than once per issue", so a second
    /// purchase against a character who carries this is refused, and a reader of the public state
    /// sees which weakness was bought off and whose.</para>
    ///
    /// <para><b>The duration needs no clock.</b> p.85 gives the suppression "the rest of the
    /// scene", and an <see cref="Encounter"/> is one scene — <see cref="Encounter.Step"/> turns
    /// pages and there is nothing in it that ends a scene — so it is carried to the end of the
    /// fight and never expires inside one. That reading is recorded in
    /// <c>docs/guide/play-engine.md</c>.</para>
    /// </summary>
    public string? SuppressedFlaw { get; }

    /// <summary>
    /// How big this combatant is, as a bare figure whose only meaning is the ratio between two of
    /// them. p.75's size bands are "at least twice your size" and "no more than one-fifth your
    /// size", so what a fight needs is a comparison and not a unit.
    ///
    /// <para><b>It is the caller's word, and that is the honest answer rather than a shortcut.</b>
    /// Chapter 2 has no size stat: the only size a sheet carries is Growth's and Shrinking's, and
    /// what those Powers print is a rank-to-<em>height</em> table in the rulebook (pp.30 and 39),
    /// which is not in <c>data/rules/</c> at all — <c>powers.json</c> carries the prose and no
    /// table. <c>play/</c> reads the play rules and a character's ranks through the first engine;
    /// it reads neither <c>data/rulebook/</c> nor a height. Typing that table in here would be a
    /// transcription in code, which is the one thing <c>docs/guide/play-rules.md</c> exists to
    /// forbid. So a GM who has a giant in the scene says so, and the engine derives the factor.
    /// </para>
    ///
    /// <para><b>The default is that everybody is the same size</b>, which is what makes the guide's
    /// sentence about a balance run being measured "against somebody the same size" true rather
    /// than merely unstated.</para>
    /// </summary>
    public double Size { get; }

    /// <summary>
    /// Whether this combatant cannot be seen, which p.75 makes equivalent to no visibility at all
    /// for anybody attacking or dodging them.
    ///
    /// <para><b>It is the caller's word and not a read of the Invisibility Power, and the two are
    /// different claims.</b> Ch.2 p.32 prints "You <em>can</em> turn invisible" — carrying the Power
    /// is a capability, being invisible is a state, and nothing in Chapters 3–5 turns one on. An
    /// engine that read the Power would have every character who owns it invisible for the whole of
    /// every fight, including the pages they spent shouting at somebody.</para>
    ///
    /// <para>The other half of p.75's sentence — the Powers that compensate — <em>is</em> a
    /// capability, is always on ("whatever the reason", p.24), and so is read off the sheet into
    /// <see cref="Powers"/>.</para>
    /// </summary>
    public bool Invisible { get; }

    /// <summary>
    /// Whether this combatant is one of p.80's hard targets — "machines, vehicles, and thick,
    /// inanimate objects" — whose passive defence rank the Hard Targets table setting doubles.
    ///
    /// <para><b>It is the caller's word, for the reason <see cref="Invisible"/> is.</b> Nothing on a
    /// character sheet says a character is a machine: Chapter 2 has no such flag, the Hero/Villain
    /// field is presentation and no rules code may read it, and the same sheet is a battlesuit in
    /// one GM's game and the person inside it in another's. So a GM with a tank in the scene says
    /// so.</para>
    ///
    /// <para><b>It is read only while <c>TableRules.HardTargets</c> is on</b>, which is p.79's own
    /// arrangement: the ten Gritty rules are optional and every figure this engine produces is
    /// measured with all ten off unless the run said otherwise. A combatant declared a hard target
    /// in a fight that did not take the setting changes nothing, and the page-one ledger line is
    /// what says the setting was not taken.</para>
    /// </summary>
    public bool HardTarget { get; }

    /// <summary>
    /// The ids of the Powers on this combatant's sheet, <b>whatever their rank</b>.
    ///
    /// <para><b>It exists because <see cref="TraitRanks"/> cannot answer the question p.75 asks.</b>
    /// Blind Fighting and Radar are default-rank Powers, so
    /// <c>DerivedStatsCalculator.GetEffectiveRank</c> answers 0 for both by design — which makes
    /// "has Blind Fighting" and "has never heard of Blind Fighting" the same reading of a rank. The
    /// visibility modifier turns on exactly that difference.</para>
    ///
    /// <para>Empty for a combatant built by hand and for a group of Minions, who have no sheet.</para>
    /// </summary>
    public IReadOnlySet<string> Powers { get; }

    /// <summary>Whether this combatant holds Resolve at all — true for a Hero and nobody else.</summary>
    public bool HoldsResolve => Kind == CombatantKind.Hero;

    /// <summary>
    /// Whether this combatant is out of the fight — beaten down to the defeat figure, wiped out to
    /// the last body, or held by an effect that has run past what is left of them (p.76).
    /// </summary>
    public bool Defeated(int defeatedAtHealth) =>
        DefeatedByEffect is not null
        || (Kind == CombatantKind.MinionGroup ? GroupSize <= 0 : CurrentHealth <= defeatedAtHealth);

    /// <summary>The rank of one Trait, or zero where the combatant has none of it.</summary>
    public int Rank(string traitId) => TraitRanks.TryGetValue(traitId, out var rank) ? rank : 0;

    /// <summary>A Hero, with the Resolve the character engine computed for them.</summary>
    public static Combatant Hero(
        string id, string name, int edge, int health, int resolve,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = HeroSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false) =>
        Build(id, name, CombatantKind.Hero, side, edge, health, resolve, 0, traitRanks, defences,
            size, invisible, powers, hardTarget);

    /// <summary>A Villain: the Hero rules, the same Health formula, and no Resolve.</summary>
    public static Combatant Villain(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false) =>
        Build(id, name, CombatantKind.Villain, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget);

    /// <summary>A Foe, whose Health has already been halved by <see cref="CombatantFactory"/>.</summary>
    public static Combatant Foe(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false) =>
        Build(id, name, CombatantKind.Foe, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget);

    /// <summary>An Extra.</summary>
    public static Combatant Extra(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false) =>
        Build(id, name, CombatantKind.Extra, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget);

    /// <summary>
    /// A group of Minions: one Threat rank, no Health, no Edge, and a body count.
    ///
    /// <para>They carry no Edge because p.73 says they have none and act after everyone else, and
    /// no Health because p.75 says they do not use it. Both are recorded as zero here and the
    /// rules that read them go through <see cref="Kind"/>, so a Minion group with a Health of zero
    /// is never mistaken for a character who has been beaten down to zero.</para>
    /// </summary>
    public static Combatant Minions(
        string id, string name, int threat, int groupSize, string threatTraitId,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        bool hardTarget = false) =>
        Build(
            id, name, CombatantKind.MinionGroup, side, edge: 0, health: 0, resolve: 0, groupSize: groupSize,
            traitRanks: new Dictionary<string, int>(StringComparer.Ordinal) { [threatTraitId] = threat },
            defences: [threatTraitId], size: size, invisible: invisible, powers: null,
            hardTarget: hardTarget);

    /// <summary>This combatant with a different Health. Nothing else moves.</summary>
    public Combatant WithHealth(int health) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, health, Resolve, GroupSize, TraitRanks, Defences,
            DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Powers);

    /// <summary>This combatant bleeding out, or steadied. p.79's clock, started and stopped.</summary>
    public Combatant Bleeding(bool dying) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Powers);

    /// <summary>
    /// This combatant brought round by p.76's instant recovery: on their feet at
    /// <paramref name="health"/>, free of whatever effect had them, and one nearer the scene's limit.
    /// </summary>
    public Combatant Recovered(int health) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, health, Resolve, GroupSize, TraitRanks, Defences,
            defeatedByEffect: null, Dying, InstantRecoveriesUsed + 1, SuppressedFlaw,
            Size, Invisible, HardTarget, Powers);

    /// <summary>
    /// This combatant put out of the fight by <paramref name="effect"/> — p.76's defeat by special
    /// effect, which lasts the rest of the scene.
    /// </summary>
    public Combatant OutForTheScene(string effect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effect);

        return new Combatant(
            Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, effect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Powers);
    }

    /// <summary>
    /// This combatant with <paramref name="flaw"/> bought off them for the rest of the scene — Ch.5
    /// p.85's <c>adversity_spend_suppress_flaw</c>.
    ///
    /// <para>A throw rather than a silent overwrite where one is already suppressed: the page allows
    /// one per character per issue, so a second is a rule the engine refuses on the ledger before it
    /// ever reaches here, and reaching here anyway would be a bug rather than a request.</para>
    /// </summary>
    public Combatant Suppressing(string flaw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flaw);

        if (SuppressedFlaw is { } already)
        {
            throw new InvalidOperationException(
                $"{Name} already has {already} suppressed, and p.85 allows one per character per "
                + "issue. The engine refuses the second purchase on the ledger; this is reached "
                + "only by a caller that has gone round it.");
        }

        return new Combatant(
            Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, flaw,
            Size, Invisible, HardTarget, Powers);
    }

    /// <summary>
    /// This combatant with <paramref name="points"/> taken out of their Resolve pool.
    ///
    /// <para>A throw rather than a no-op on a combatant who holds none, because a spend charged to
    /// a pool that does not exist is a rule applied to the wrong character, and a silent zero would
    /// let the encounter go on reporting the effect it paid for.</para>
    /// </summary>
    public Combatant Spending(int points)
    {
        if (!HoldsResolve)
        {
            throw new InvalidOperationException(
                $"{Name} is a {Kind} and holds no Resolve — Ch.2 says only Heroes have any, and "
                + "Ch.5 gives the GM Adversity to spend on an NPC instead. Spend Adversity, or "
                + "build this combatant as a Hero.");
        }

        if (points > Resolve)
        {
            throw new InvalidOperationException(
                $"{Name} has {Resolve} Resolve and the spend costs {points}.");
        }

        return new Combatant(
            Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve - points, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Powers);
    }

    /// <summary>This Minion group with fewer bodies in it.</summary>
    public Combatant WithGroupSize(int groupSize) =>
        Kind == CombatantKind.MinionGroup
            ? new Combatant(
                Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, Math.Max(0, groupSize),
                TraitRanks, Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
                Size, Invisible, HardTarget, Powers)
            : throw new InvalidOperationException($"{Name} is a {Kind}, not a group of Minions.");

    private static Combatant Build(
        string id, string name, CombatantKind kind, string side, int edge, int health, int resolve,
        int groupSize, IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        double size = SameSize, bool invisible = false, IReadOnlySet<string>? powers = null,
        bool hardTarget = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(side);
        ArgumentNullException.ThrowIfNull(traitRanks);
        ArgumentNullException.ThrowIfNull(defences);

        // <b>A size of nothing is refused rather than clamped.</b> p.75's bands are a ratio of two
        // sizes, so a zero or a negative one makes the comparison meaningless — and the failure
        // would be silent, since a division by zero is an infinity that satisfies every band.
        if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
        {
            throw new ArgumentOutOfRangeException(
                nameof(size), size,
                $"{name} is {size} big. p.75's size bands compare two sizes as a ratio, so a size "
                + $"has to be a real figure above zero; {SameSize} means the same size as everybody "
                + "else, which is the default.");
        }

        return new Combatant(
            id, name, kind, side, edge, health, health, resolve, groupSize,
            new Dictionary<string, int>(traitRanks, StringComparer.Ordinal),
            [.. defences],
            defeatedByEffect: null, dying: false, instantRecoveriesUsed: 0, suppressedFlaw: null,
            size, invisible, hardTarget, powers ?? EmptyPowers);
    }
}
