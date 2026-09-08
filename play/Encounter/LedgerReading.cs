using System.Globalization;

namespace ProwlersAndParagonsAutomation.Play.Encounter;

/// <summary>
/// The two things a reader of a run needs that <see cref="EncounterState"/> does not carry, read
/// back off the ledger the engine wrote.
///
/// <para><b>This is a parse of prose and that is a cost, not a design.</b> Everything else a style
/// policy or a measurement here wants — who attacked whom, with which Trait, for how much, and who
/// went down — comes off <see cref="ResolvedAttack"/> and off the difference between two immutable
/// states, which cannot be mis-read. Two facts do not: <em>which Trait answered an attack</em> is
/// chosen inside <c>Encounter.Step</c> and never recorded on the state, and <em>how much damage a
/// particular attacker has done so far</em> is a running total nothing keeps. Both are written on
/// the ledger, in one place each, by one line of code each.</para>
///
/// <para><b>So each reading names the phrase it is anchored on and throws when that phrase moves.</b>
/// That is the discipline <c>Encounter</c> already applies to a printed word it depends on — see
/// <c>seize_initiative_gm_alternative</c>'s "doubles" — and it is here for the same reason: a parse
/// that quietly found nothing would report a fight in which no defence was ever rolled and nobody
/// ever hit anybody, which is a plausible-looking answer with nothing behind it. A throw names the
/// line it could not read.</para>
///
/// <para><b>It is a reading and it is recorded as one</b>, in <c>docs/guide/play-engine.md</c>'s
/// readings table, beside the reason the alternative — putting the two figures on the state — was
/// not taken here.</para>
/// </summary>
public static class LedgerReading
{
    /// <summary>The id of the entry every attack line cites: p.75's Attack and Defense table.</summary>
    public const string AttackRule = "attacks_and_defenses";

    /// <summary>The id of the entry every damage line cites (p.75).</summary>
    public const string DamageRule = "damage";

    /// <summary>
    /// The phrase that separates an attack line's attacking half from its defending half.
    /// <c>Encounter.Step</c> writes it once, in <c>ResolveAttack</c>.
    /// </summary>
    private const string DefendsWith = " defends with ";

    /// <summary>What follows the defending pool: <c>"{trait} {pool}d for {successes}"</c>.</summary>
    private const string DiceFor = "d for ";

    /// <summary>The phrase an attack line opens its two halves with.</summary>
    private const string Attacks = " attacks ";

    /// <summary>What a damage line puts either side of the figure taken off.</summary>
    private const string NetSuccessesIs = " net successes is ";

    /// <inheritdoc cref="NetSuccessesIs"/>
    private const string DamageSemicolon = " damage; ";

    /// <summary>
    /// Whether this line is the one <c>ResolveAttack</c> writes for an exchange — as opposed to one
    /// of the refusals that cite the same entry ("has no rank in …", "cannot be seen at all").
    /// </summary>
    public static bool IsAnAttack(LedgerLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return string.Equals(line.Rule, AttackRule, StringComparison.Ordinal)
               && line.Text.Contains(Attacks, StringComparison.Ordinal)
               && line.Text.Contains(DefendsWith, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Trait that answered the attack this line records.
    ///
    /// <para>The sentence is <c>"… defends with {trait} {pool}d for {successes}"</c>, and the Trait
    /// may be several words — a target with nothing to answer with defends with "no defence".</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The line no longer has the shape this reading is anchored on, which means the sentence in
    /// <c>ResolveAttack</c> has been rewritten. Thrown rather than shrugged: a defence silently read
    /// as nothing would put a report's whole defence table at zero and look exactly like a fight in
    /// which nobody dodged.
    /// </exception>
    public static string DefenceTraitIn(LedgerLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var opens = line.Text.IndexOf(DefendsWith, StringComparison.Ordinal);
        var closes = opens < 0 ? -1 : line.Text.IndexOf(DiceFor, opens, StringComparison.Ordinal);

        if (opens < 0 || closes < 0)
        {
            throw new InvalidOperationException(
                $"An attack line no longer reads '…{DefendsWith}{{trait}} {{pool}}{DiceFor}…', so "
                + "the Trait that answered it cannot be read back. The line was: " + line.Text);
        }

        var between = line.Text[(opens + DefendsWith.Length)..closes].Trim();
        var lastSpace = between.LastIndexOf(' ');

        if (lastSpace <= 0)
        {
            throw new InvalidOperationException(
                "An attack line's defending half no longer carries a Trait and a pool. The line "
                + "was: " + line.Text);
        }

        return between[..lastSpace];
    }

    /// <summary>How every purchase line in this engine opens, when the GM's pool paid.</summary>
    private const string TheGmSpends = "the GM spends ";

    /// <summary>And when a Hero's own Resolve did: <c>"{name} spends {cost} Resolve"</c>.</summary>
    private const string Spends = " spends ";

    /// <inheritdoc cref="Spends"/>
    private const string ResolveWord = " Resolve";

    /// <summary>
    /// Whether this line records a purchase that was actually paid for — one point or more leaving
    /// somebody's pool.
    ///
    /// <para><b>It is anchored on <c>Encounter.Step</c>'s own <c>Paid</c> helper</b>, which writes
    /// the same two sentences for all ten purchases in both currencies, and it requires the sentence
    /// to <em>open</em> the line. That is what keeps a refusal out: "p.85 spends a point of Adversity
    /// on behalf of any NPC …" is a refusal quoting the page and mentions the same words halfway
    /// through, and "{name} spends a page moving" is a movement.</para>
    ///
    /// <para><b>Its control is <c>Standard</c>'s half of the mano-a-mano fixture.</b> A rewritten
    /// <c>Paid</c> would make this reading find nothing, and "nothing was ever spent" is exactly
    /// what a policy that never spends looks like — so the fixture requires the other policy, on the
    /// same seeds, to produce lines this finds.</para>
    /// </summary>
    public static bool IsAPurchase(LedgerLine line, string actorName)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(actorName);

        if (line.Text.StartsWith(TheGmSpends, StringComparison.Ordinal)) return true;

        return line.Text.StartsWith(actorName + Spends, StringComparison.Ordinal)
               && line.Text.Contains(ResolveWord, StringComparison.Ordinal);
    }

    /// <summary>Every line in this fight that records a purchase, whoever made it.</summary>
    public static IReadOnlyList<LedgerLine> Purchases(EncounterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return
        [
            .. state.Ledger.Lines.Where(line =>
                line.Actor.Length > 0
                && state.Combatants.TryGetValue(line.Actor, out var actor)
                && IsAPurchase(line, actor.Name))
        ];
    }

    /// <summary>
    /// Whether anybody not on <paramref name="side"/> made a purchase on <paramref name="page"/> —
    /// what a policy that spends "in response to the other side spending" reads.
    /// </summary>
    public static bool OtherSideBoughtOn(EncounterState state, string side, int page)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Purchases(state).Any(line =>
            line.Page == page
            && !string.Equals(state.Combatants[line.Actor].Side, side, StringComparison.Ordinal));
    }

    /// <summary>
    /// How much damage each combatant has done in this fight so far, by attacker id.
    ///
    /// <para><b>It is what a policy that goes after the biggest threat has to read</b>, and nothing
    /// on <see cref="EncounterState"/> is a running total of it: Health says what a character has
    /// <em>taken</em> and never who took it off them.</para>
    ///
    /// <para>Every damage line is written by one statement in <c>InflictDamage</c> and carries the
    /// attacker as its <see cref="LedgerLine.Actor"/>, so the id is structural and only the figure
    /// is parsed.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A line citing the damage entry has the anchor phrase and an unreadable figure between the
    /// two halves of it.
    /// </exception>
    public static IReadOnlyDictionary<string, int> DamageDealtSoFar(Ledger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var dealt = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in ledger.Lines)
        {
            if (!string.Equals(line.Rule, DamageRule, StringComparison.Ordinal)) continue;

            var opens = line.Text.IndexOf(NetSuccessesIs, StringComparison.Ordinal);
            var closes = opens < 0 ? -1 : line.Text.IndexOf(DamageSemicolon, opens, StringComparison.Ordinal);

            if (opens < 0 || closes < 0) continue;

            var figure = line.Text[(opens + NetSuccessesIs.Length)..closes].Trim();

            if (!int.TryParse(figure, NumberStyles.Integer, CultureInfo.InvariantCulture, out var damage))
            {
                throw new InvalidOperationException(
                    $"A damage line reads '{figure}' where this reading expects the figure taken "
                    + "off. The line was: " + line.Text);
            }

            if (line.Actor.Length == 0) continue;

            dealt[line.Actor] = dealt.GetValueOrDefault(line.Actor) + damage;
        }

        return dealt;
    }
}
