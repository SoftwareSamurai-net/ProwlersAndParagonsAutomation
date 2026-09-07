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
/// An item a character has in their hands, which arrives one of two ways: they walked into the
/// fight with it, or p.76's full grab took it off somebody.
///
/// <para><b>There is no inventory here and this is not one.</b> A <c>CharacterSheet</c>'s gear is a
/// name with optional custom features on it, nothing downstream can tell an attack made with a sword
/// from a bare-handed one, and <c>play/</c> may not read the character rules anyway — see the Gear
/// Limit in <c>docs/guide/play-engine.md</c>. So the name is <b>the caller's word</b>, the way a
/// lure's target and a misfortune's narration are, and the only thing the engine claims about it is
/// what p.76 says: who has it, and whether the page it was won on has been spent.</para>
///
/// <para><b>The opening hand is the caller's word too, and it is what makes a grab honest.</b>
/// p.76 aims a grab at "a weapon or other handheld item away from your opponent" — so a grab
/// presupposes an opponent who <em>has</em> one, and an engine that took the item's existence off
/// the grabber's own say-so would hand a winner control of an object nobody was ever recorded as
/// carrying and print a line saying the loser had lost it. <see cref="Combatant.Carrying"/> is where
/// the fact goes in, and <see cref="BeforeTheFight"/> is the page it is stamped with.</para>
///
/// <para><b>The page it was won on is what makes "that same page" enforceable.</b> p.76 lets the
/// winner "use it or toss it aside on that same page"; an item won and neither used nor tossed by
/// the time the page turns is dropped, which <see cref="Encounter.Step"/> does at
/// <see cref="EndPage"/>. <see cref="Used"/> is what discharges the clause — an item the winner
/// actually swung stays with them, and what becomes of it after that is the GM's, because the page
/// stops talking. An item that was <em>carried in</em> is under no such clause at all: p.76's page
/// is a limit on what a grab wins, and nothing in Chapters 3–5 takes a weapon off somebody who
/// simply brought one.</para>
/// </summary>
/// <param name="Name">What it is, in the caller's own words.</param>
/// <param name="WonOnPage">
/// The page the full grab that took it landed on, or <see cref="BeforeTheFight"/> where nobody won
/// it and the character walked in with it.
/// </param>
/// <param name="Used">Whether the holder has attacked with it since.</param>
public sealed record HeldItem(string Name, int WonOnPage, bool Used = false)
{
    /// <summary>
    /// The page stamped on an item nobody won: the character brought it into the fight.
    ///
    /// <para>A fight's first page is 1, so this can never be the page that is ending — which is
    /// exactly the behaviour p.76 asks for, since the clause it would fire is a limit on what a
    /// grab wins and no grab won this.</para>
    /// </summary>
    public const int BeforeTheFight = 0;

    /// <summary>Whether nobody won this: it was carried into the fight.</summary>
    public bool CarriedIn => WonOnPage == BeforeTheFight;
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
        bool ready,
        bool consciousAtZeroOrLess,
        IReadOnlySet<string> powers,
        IReadOnlySet<string> rangedPowers,
        HeldItem? holding)
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
        Ready = ready;
        ConsciousAtZeroOrLess = consciousAtZeroOrLess;
        Powers = powers;
        RangedPowers = rangedPowers;
        Holding = holding;
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
    /// Whether this combatant has "a weapon or Power aimed and ready to strike" (p.79), which under
    /// the Drop table setting doubles their effective Edge against everyone who has not.
    ///
    /// <para><b>It is the caller's word, for the reason <see cref="Invisible"/> is</b> — and it is
    /// the same distinction: carrying a gun is a capability and having it levelled is a state, and
    /// nothing in Chapters 3–5 levels one. An engine that read the sheet would have every armed
    /// character holding the drop for every page of every fight, including the ones they spent with
    /// the weapon holstered, which is exactly what p.79's own example turns on. The page hands the
    /// question over in as many words: <c>final_say</c> is the GM's.</para>
    ///
    /// <para><b>The default is that nobody is ready</b>, which is the arrangement every figure this
    /// engine has produced was measured in.</para>
    /// </summary>
    public bool Ready { get; }

    /// <summary>
    /// Whether p.80's Slow Healing has this combatant on their feet at or below the figure that
    /// would otherwise have them out of the fight — "you may be conscious while at 0 or negative
    /// Health".
    ///
    /// <para><b>It is a consequence of that rule and is only ever set by it.</b> Slow Healing takes
    /// away the healing a character gets when they come round after a defeat, so what a p.76 instant
    /// recovery brings back is a character standing on whatever Health they went down with. Nothing
    /// else in Chapters 3-5 reaches this state, and nothing may set it from outside: it is
    /// <see cref="Recovered"/>'s to give and <see cref="Overcome"/>'s to take away.</para>
    ///
    /// <para><b>It is read by <see cref="Defeated"/>, which is what makes it a state rather than a
    /// flag.</b> Without that, an instant recovery under Slow Healing would put a character back on
    /// zero Health and the very next line would find them defeated again — the purchase would be a
    /// point spent on a sentence.</para>
    ///
    /// <para><b>p.80's parenthetical is satisfied without a check.</b> The page allows this "(if
    /// using the Fatal Damage rules)" and the parenthetical is about the <em>negative</em> half:
    /// exactly the defeat figure needs no optional rule, since p.75 defeats a character there in any
    /// fight, and Health cannot fall below it at all unless Fatal Damage is on. That is the same
    /// reading <c>gritty_wound_penalties</c>' own <c>interpretation</c> makes of the identical
    /// phrase, and it is recorded in <c>docs/guide/play-engine.md</c>.</para>
    /// </summary>
    public bool ConsciousAtZeroOrLess { get; }

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

    /// <summary>
    /// The ids of the Powers on this combatant's sheet whose <b>own Range reaches past Close
    /// Range</b> — Ch.2 p.19's <c>ranged</c>, and no other value of that field.
    ///
    /// <para><b>It is read off the sheet because it is the one half of p.79's Close Range rule a
    /// sheet can answer.</b> That rule applies "only to attacks that can be used at Distant or
    /// Extreme Range", and for the two Power rows of p.75's table the answer is a property of the
    /// Power rather than of the row: a Blast reaches across the street and a Growth does not.
    /// <see cref="Powers"/> cannot answer it — it is every id whatever its rank and says nothing
    /// about reach — and <see cref="TraitRanks"/> cannot either.</para>
    ///
    /// <para><b>Only <c>ranged</c> counts, and that is a reading with its direction stated.</b>
    /// <c>self</c> and <c>touch</c> plainly cannot be used at a distance; <c>zone</c> and
    /// <c>special</c> are neither said to nor said not to, and p.79's rule is a penalty, so the
    /// narrow reading is the one that never costs a dodger dice the page may not have meant them to
    /// lose. <c>docs/guide/play-engine.md</c> records it.</para>
    ///
    /// <para>Empty for a combatant built by hand and for a group of Minions, who have no sheet —
    /// for whom p.75's Ranged Weapon row is what says an attack is ranged.</para>
    /// </summary>
    public IReadOnlySet<string> RangedPowers { get; }

    /// <summary>
    /// The item p.76's full grab put in this combatant's hands, or null.
    ///
    /// <para><b>It is the whole of what a full grab leaves behind, and it is read rather than kept
    /// for the look of it.</b> Three rules turn on it: an <see cref="Attack"/> may name an item and
    /// is refused unless it is this one, a <see cref="Toss"/> may drop it and is refused unless it is
    /// this one, and <see cref="EndPage"/> drops an item won on the page that is ending and neither
    /// used nor tossed — which is p.76's "on that same page" made a state rather than a sentence.
    /// </para>
    ///
    /// <para><b>Null is not "empty-handed" and never claims to be.</b> This engine has no inventory:
    /// nobody starts a fight holding anything it knows about, and a combatant carrying a sword on
    /// their sheet comes out of <see cref="CombatantFactory"/> indistinguishable from one who is not.
    /// So null means <em>nothing this fight has taken off anybody</em>, and what else a character has
    /// in their hands is the GM's, exactly as the Gear Limit's absence is.</para>
    /// </summary>
    public HeldItem? Holding { get; }

    /// <summary>Whether this combatant holds Resolve at all — true for a Hero and nobody else.</summary>
    public bool HoldsResolve => Kind == CombatantKind.Hero;

    /// <summary>
    /// Whether this combatant is out of the fight — beaten down to the defeat figure, wiped out to
    /// the last body, or held by an effect that has run past what is left of them (p.76).
    /// </summary>
    public bool Defeated(int defeatedAtHealth) =>
        DefeatedByEffect is not null
        || (Kind == CombatantKind.MinionGroup
            ? GroupSize <= 0
            : CurrentHealth <= defeatedAtHealth && !ConsciousAtZeroOrLess);

    /// <summary>The rank of one Trait, or zero where the combatant has none of it.</summary>
    public int Rank(string traitId) => TraitRanks.TryGetValue(traitId, out var rank) ? rank : 0;

    /// <summary>A Hero, with the Resolve the character engine computed for them.</summary>
    public static Combatant Hero(
        string id, string name, int edge, int health, int resolve,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = HeroSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false,
        IReadOnlySet<string>? rangedPowers = null, bool ready = false) =>
        Build(id, name, CombatantKind.Hero, side, edge, health, resolve, 0, traitRanks, defences,
            size, invisible, powers, hardTarget, rangedPowers, ready);

    /// <summary>A Villain: the Hero rules, the same Health formula, and no Resolve.</summary>
    public static Combatant Villain(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false,
        IReadOnlySet<string>? rangedPowers = null, bool ready = false) =>
        Build(id, name, CombatantKind.Villain, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget, rangedPowers, ready);

    /// <summary>A Foe, whose Health has already been halved by <see cref="CombatantFactory"/>.</summary>
    public static Combatant Foe(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false,
        IReadOnlySet<string>? rangedPowers = null, bool ready = false) =>
        Build(id, name, CombatantKind.Foe, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget, rangedPowers, ready);

    /// <summary>An Extra.</summary>
    public static Combatant Extra(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        string side = OpposingSide, double size = SameSize, bool invisible = false,
        IReadOnlySet<string>? powers = null, bool hardTarget = false,
        IReadOnlySet<string>? rangedPowers = null, bool ready = false) =>
        Build(id, name, CombatantKind.Extra, side, edge, health, 0, 0, traitRanks, defences,
            size, invisible, powers, hardTarget, rangedPowers, ready);

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
        bool hardTarget = false, bool ready = false) =>
        Build(
            id, name, CombatantKind.MinionGroup, side, edge: 0, health: 0, resolve: 0, groupSize: groupSize,
            traitRanks: new Dictionary<string, int>(StringComparer.Ordinal) { [threatTraitId] = threat },
            defences: [threatTraitId], size: size, invisible: invisible, powers: null,
            hardTarget: hardTarget, rangedPowers: null, ready: ready);

    /// <summary>This combatant with a different Health. Nothing else moves.</summary>
    public Combatant WithHealth(int health) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, health, Resolve, GroupSize, TraitRanks, Defences,
            DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding);

    /// <summary>This combatant bleeding out, or steadied. p.79's clock, started and stopped.</summary>
    public Combatant Bleeding(bool dying) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding);

    /// <summary>
    /// This combatant brought round by p.76's instant recovery: on their feet at
    /// <paramref name="health"/>, free of whatever effect had them, and one nearer the scene's limit.
    /// </summary>
    /// <param name="health">The Health they come round on.</param>
    /// <param name="consciousAtZeroOrLess">
    /// p.80's Slow Healing: they are standing on a figure that would otherwise have them out. Only
    /// that rule sets it — see <see cref="ConsciousAtZeroOrLess"/>.
    /// </param>
    public Combatant Recovered(int health, bool consciousAtZeroOrLess = false) =>
        new(Id, Name, Kind, Side, Edge, FullHealth, health, Resolve, GroupSize, TraitRanks, Defences,
            defeatedByEffect: null, Dying, InstantRecoveriesUsed + 1, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, consciousAtZeroOrLess, Powers, RangedPowers, Holding);

    /// <summary>
    /// p.80's other half of the same sentence: a character standing at or below the defeat figure
    /// "is defeated if you take even a single point of damage in this condition".
    ///
    /// <para>Taking the state away is the whole of it — their Health is already at or past the
    /// figure <c>damage.defeated_at_health</c> names, so <see cref="Defeated"/> answers true the
    /// moment they stop being the exception to it.</para>
    /// </summary>
    public Combatant Overcome() =>
        new(Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, consciousAtZeroOrLess: false, Powers, RangedPowers, Holding);

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
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding);
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
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding);
    }

    /// <summary>
    /// This combatant with <paramref name="points"/> taken out of their Resolve pool.
    ///
    /// <para>A throw rather than a no-op on a combatant who holds none, because a spend charged to
    /// a pool that does not exist is a rule applied to the wrong character, and a silent zero would
    /// let the encounter go on reporting the effect it paid for.</para>
    ///
    /// <para><b>And a throw the other way, because subtraction has two directions and only one of
    /// them is a spend.</b> Every price in Chapters 4 and 5 is a whole number of points and none is
    /// below one, so a negative cost is nothing the book can ask for — but the arithmetic here
    /// obliges it, and the one purchase whose price is the caller's figure duly minted Resolve out
    /// of a request for −5 dice. <see cref="Encounter.Step"/> refuses that on the ledger, where an
    /// intent the rules do not allow belongs; this is the guard behind it, and reaching it is a
    /// programming error rather than a request.</para>
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

        if (points < 0)
        {
            throw new InvalidOperationException(
                $"a spend of {points} points would put Resolve back into {Name}'s pool. Chapters 4 "
                + "and 5 price every purchase at a whole point or more, and a purchase priced "
                + "backwards is one the engine refuses on the ledger before it reaches here.");
        }

        if (points > Resolve)
        {
            throw new InvalidOperationException(
                $"{Name} has {Resolve} Resolve and the spend costs {points}.");
        }

        return new Combatant(
            Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve - points, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding);
    }

    /// <summary>
    /// This combatant holding <paramref name="item"/>, won by a full grab on
    /// <paramref name="wonOnPage"/> — p.76's "you gain control of the object".
    ///
    /// <para>Whatever they were holding before is gone, because a pair of hands is what the page is
    /// talking about and this engine models one item, not a bag. A grab that takes a second thing off
    /// somebody drops the first, and the ledger line says so.</para>
    /// </summary>
    public Combatant Holds(string item, int wonOnPage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(item);

        return new Combatant(
            Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers,
            new HeldItem(item, wonOnPage));
    }

    /// <summary>
    /// This combatant walking into the fight with <paramref name="item"/> in their hands — the
    /// caller's word, the way <see cref="Size"/>, <see cref="Invisible"/>, <see cref="HardTarget"/>
    /// and <see cref="Ready"/> are.
    ///
    /// <para><b>It exists because a grab has to have something to be aimed at.</b> p.76 defines a
    /// grab as an attempt to take a handheld item "away from your opponent"; with no way to say what
    /// anybody walked in with, every grab in a real fight was for an item its target was not
    /// recorded as holding, and the winner ended up holding an object that came from nowhere while
    /// the ledger said the loser had lost it. <see cref="Encounter.Step"/> refuses a grab for an
    /// item the target is not holding, so this is where the item comes from.</para>
    ///
    /// <para>It is stamped <see cref="HeldItem.BeforeTheFight"/>, which is never a page that ends,
    /// because p.76's one-page clause is a limit on what a <em>grab</em> wins and no grab won this.
    /// </para>
    /// </summary>
    public Combatant Carrying(string item) => Holds(item, HeldItem.BeforeTheFight);

    /// <summary>
    /// This combatant having swung what they are holding — p.76's "can use it".
    ///
    /// <para><b>What that discharges is the clause and not the possession.</b> The page gives the
    /// winner the object and one page in which to use or discard it; an item that was used is an item
    /// the page has stopped talking about, so <see cref="Encounter.Step"/> stops dropping it when the
    /// page turns and what becomes of it afterwards is the GM's.</para>
    ///
    /// <para>A throw on empty hands, because the engine refuses an attack naming an item nobody holds
    /// on the ledger long before this: reaching here is a programming error rather than a request.
    /// </para>
    /// </summary>
    public Combatant Used() =>
        Holding is { } held
            ? new Combatant(
                Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
                Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
                Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers,
                held with { Used = true })
            : throw new InvalidOperationException(
                $"{Name} is holding nothing, so there is nothing for them to have used. An attack "
                + "naming an item its actor does not hold is refused on the ledger before it "
                + "reaches here.");

    /// <summary>
    /// This combatant empty-handed: p.76's "toss it aside", and what the page turn does to an item
    /// its winner neither used nor tossed.
    ///
    /// <para>Nobody holds it afterwards — this engine has no floor to put it on, so the ledger line
    /// is where it went and there is no state for a dropped object.</para>
    /// </summary>
    public Combatant Dropped() =>
        new(Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, GroupSize, TraitRanks,
            Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
            Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers,
            holding: null);

    /// <summary>This Minion group with fewer bodies in it.</summary>
    public Combatant WithGroupSize(int groupSize) =>
        Kind == CombatantKind.MinionGroup
            ? new Combatant(
                Id, Name, Kind, Side, Edge, FullHealth, CurrentHealth, Resolve, Math.Max(0, groupSize),
                TraitRanks, Defences, DefeatedByEffect, Dying, InstantRecoveriesUsed, SuppressedFlaw,
                Size, Invisible, HardTarget, Ready, ConsciousAtZeroOrLess, Powers, RangedPowers, Holding)
            : throw new InvalidOperationException($"{Name} is a {Kind}, not a group of Minions.");

    private static Combatant Build(
        string id, string name, CombatantKind kind, string side, int edge, int health, int resolve,
        int groupSize, IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences,
        double size = SameSize, bool invisible = false, IReadOnlySet<string>? powers = null,
        bool hardTarget = false, IReadOnlySet<string>? rangedPowers = null, bool ready = false)
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
            size, invisible, hardTarget, ready, consciousAtZeroOrLess: false,
            powers ?? EmptyPowers, rangedPowers ?? EmptyPowers, holding: null);
    }
}
