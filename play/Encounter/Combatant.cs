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
        int edge,
        int fullHealth,
        int currentHealth,
        int resolve,
        int groupSize,
        IReadOnlyDictionary<string, int> traitRanks,
        IReadOnlyList<string> defences)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Edge = edge;
        FullHealth = fullHealth;
        CurrentHealth = currentHealth;
        Resolve = resolve;
        GroupSize = groupSize;
        TraitRanks = traitRanks;
        Defences = defences;
    }

    /// <summary>The id the encounter refers to this combatant by.</summary>
    public string Id { get; }

    /// <summary>The name a ledger line prints.</summary>
    public string Name { get; }

    /// <summary>Which rung of p.73's ladder this combatant is on.</summary>
    public CombatantKind Kind { get; }

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

    /// <summary>Whether this combatant holds Resolve at all — true for a Hero and nobody else.</summary>
    public bool HoldsResolve => Kind == CombatantKind.Hero;

    /// <summary>
    /// Whether this combatant is out of the fight. A Minion group is out when the last of it is.
    /// </summary>
    public bool Defeated(int defeatedAtHealth) =>
        Kind == CombatantKind.MinionGroup ? GroupSize <= 0 : CurrentHealth <= defeatedAtHealth;

    /// <summary>The rank of one Trait, or zero where the combatant has none of it.</summary>
    public int Rank(string traitId) => TraitRanks.TryGetValue(traitId, out var rank) ? rank : 0;

    /// <summary>A Hero, with the Resolve the character engine computed for them.</summary>
    public static Combatant Hero(
        string id, string name, int edge, int health, int resolve,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences) =>
        Build(id, name, CombatantKind.Hero, edge, health, resolve, 0, traitRanks, defences);

    /// <summary>A Villain: the Hero rules, the same Health formula, and no Resolve.</summary>
    public static Combatant Villain(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences) =>
        Build(id, name, CombatantKind.Villain, edge, health, 0, 0, traitRanks, defences);

    /// <summary>A Foe, whose Health has already been halved by <see cref="CombatantFactory"/>.</summary>
    public static Combatant Foe(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences) =>
        Build(id, name, CombatantKind.Foe, edge, health, 0, 0, traitRanks, defences);

    /// <summary>An Extra.</summary>
    public static Combatant Extra(
        string id, string name, int edge, int health,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences) =>
        Build(id, name, CombatantKind.Extra, edge, health, 0, 0, traitRanks, defences);

    /// <summary>
    /// A group of Minions: one Threat rank, no Health, no Edge, and a body count.
    ///
    /// <para>They carry no Edge because p.73 says they have none and act after everyone else, and
    /// no Health because p.75 says they do not use it. Both are recorded as zero here and the
    /// rules that read them go through <see cref="Kind"/>, so a Minion group with a Health of zero
    /// is never mistaken for a character who has been beaten down to zero.</para>
    /// </summary>
    public static Combatant Minions(string id, string name, int threat, int groupSize, string threatTraitId) =>
        Build(
            id, name, CombatantKind.MinionGroup, edge: 0, health: 0, resolve: 0, groupSize: groupSize,
            traitRanks: new Dictionary<string, int>(StringComparer.Ordinal) { [threatTraitId] = threat },
            defences: [threatTraitId]);

    /// <summary>This combatant with a different Health. Nothing else moves.</summary>
    public Combatant WithHealth(int health) =>
        new(Id, Name, Kind, Edge, FullHealth, health, Resolve, GroupSize, TraitRanks, Defences);

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

        return new Combatant(Id, Name, Kind, Edge, FullHealth, CurrentHealth, Resolve - points, GroupSize, TraitRanks, Defences);
    }

    /// <summary>This Minion group with fewer bodies in it.</summary>
    public Combatant WithGroupSize(int groupSize) =>
        Kind == CombatantKind.MinionGroup
            ? new Combatant(Id, Name, Kind, Edge, FullHealth, CurrentHealth, Resolve, Math.Max(0, groupSize), TraitRanks, Defences)
            : throw new InvalidOperationException($"{Name} is a {Kind}, not a group of Minions.");

    private static Combatant Build(
        string id, string name, CombatantKind kind, int edge, int health, int resolve, int groupSize,
        IReadOnlyDictionary<string, int> traitRanks, IReadOnlyList<string> defences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(traitRanks);
        ArgumentNullException.ThrowIfNull(defences);

        return new Combatant(
            id, name, kind, edge, health, health, resolve, groupSize,
            new Dictionary<string, int>(traitRanks, StringComparer.Ordinal),
            [.. defences]);
    }
}
