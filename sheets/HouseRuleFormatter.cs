using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Sheets;

/// <summary>
/// What a table's optional rules are <b>called</b>, and which of them are on.
///
/// <para><b>It is here for the reason every other formatter in this project is here.</b> The
/// printed sheet, the browser's campaign page and the terminal wizard all have to name the same
/// switch the same way, and three copies of thirteen names is three chances for one of them to
/// read differently from the rulebook. <c>PowerFormatter</c> is the precedent: the engine holds
/// the fact and <c>sheets/</c> holds the sentence.</para>
///
/// <para><b>The names are the rulebook's headings, not the property names.</b>
/// <c>GmAlternativeToSeizingInitiative</c> is a C# identifier; "Doubled Edge instead of seizing
/// the initiative" is what a GM turned on. Every finding and every row in this application names
/// things the way the book does rather than by their key, and a house-rules list is the most
/// obviously reader-facing place that rule applies.</para>
///
/// <para><b>The page each is on travels with the name</b>, because a table looking at a list of
/// what they have adopted is a table about to go and read them.</para>
///
/// <para><b>Every switch has an entry and a test says so.</b> A switch with no name would print as
/// nothing at all — the list would simply be one shorter, which is the quietest possible way for a
/// setting to become invisible to the people it is about.</para>
/// </summary>
public static class HouseRuleFormatter
{
    /// <summary>
    /// The printed name and page of each switch, by the property name
    /// <see cref="CampaignTable"/> and <c>play/Encounter/TableRules.cs</c> both use.
    ///
    /// <para><b>In the book's own order</b> — the ten Gritty Combat Rules of pp.79–81 and then the
    /// three that are not Gritty rules — because that is the order a table reviewing them reads
    /// them in, and a list that reorders itself between two screens is a list somebody has to
    /// re-scan.</para>
    /// </summary>
    private static readonly (string Key, string Name, string Page)[] Entries =
    [
        (nameof(CampaignTable.ActiveDefensesCost), "Active defences cost dice", "p.79"),
        (nameof(CampaignTable.CloseRangePenalty), "Close-range ranged attacks", "p.79"),
        (nameof(CampaignTable.TheDrop), "The Drop", "p.79"),
        (nameof(CampaignTable.FatalDamage), "Fatal Damage", "p.79"),
        (nameof(CampaignTable.FriendlyFire), "Friendly Fire", "p.80"),
        (nameof(CampaignTable.HardTargets), "Hard Targets", "p.80"),
        (nameof(CampaignTable.RaisedGearLimit), "Raised Gear Limit", "p.80"),
        (nameof(CampaignTable.SlowHealing), "Slow Healing", "p.80"),
        (nameof(CampaignTable.ToughMinions), "Tough Minions", "p.81"),
        (nameof(CampaignTable.WoundPenalties), "Wound Penalties", "p.81"),
        (nameof(CampaignTable.GmAlternativeToSeizingInitiative),
            "Doubled Edge instead of seizing the initiative", "p.73"),
        (nameof(CampaignTable.CheckingYourSwing), "Checking Your Swing", "p.69"),
        (nameof(CampaignTable.RandomInitiative), "Edge roll for initiative", "p.73")
    ];

    /// <summary>Every switch, in the book's order, with its name and page. Never empty.</summary>
    public static IReadOnlyList<(string Key, string Name, string Page)> All => Entries;

    /// <summary>
    /// Whether the named switch is on for this table, by the key <see cref="All"/> uses.
    ///
    /// <para><b>A key nothing knows throws rather than answering false</b>, which is the same
    /// choice <c>TableRules.IsOn</c> makes one project over: a misspelling answering "off" is a
    /// setting silently reported as not adopted, and a table would never know to look.</para>
    /// </summary>
    public static bool IsOn(CampaignTable table, string key)
    {
        ArgumentNullException.ThrowIfNull(table);

        return key switch
        {
            nameof(CampaignTable.ActiveDefensesCost) => table.ActiveDefensesCost,
            nameof(CampaignTable.CloseRangePenalty) => table.CloseRangePenalty,
            nameof(CampaignTable.TheDrop) => table.TheDrop,
            nameof(CampaignTable.FatalDamage) => table.FatalDamage,
            nameof(CampaignTable.FriendlyFire) => table.FriendlyFire,
            nameof(CampaignTable.HardTargets) => table.HardTargets,
            nameof(CampaignTable.RaisedGearLimit) => table.RaisedGearLimit,
            nameof(CampaignTable.SlowHealing) => table.SlowHealing,
            nameof(CampaignTable.ToughMinions) => table.ToughMinions,
            nameof(CampaignTable.WoundPenalties) => table.WoundPenalties,
            nameof(CampaignTable.GmAlternativeToSeizingInitiative) =>
                table.GmAlternativeToSeizingInitiative,
            nameof(CampaignTable.CheckingYourSwing) => table.CheckingYourSwing,
            nameof(CampaignTable.RandomInitiative) => table.RandomInitiative,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No such table setting.")
        };
    }

    /// <summary>
    /// The printed name of every switch this table has turned on, in the book's order. Empty for a
    /// table playing the book, which is what a caller checks before printing a heading.
    /// </summary>
    public static IReadOnlyList<string> On(CampaignTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        return [.. Entries.Where(e => IsOn(table, e.Key)).Select(e => e.Name)];
    }

    /// <summary>
    /// The same table with one switch set, by the key <see cref="All"/> uses.
    ///
    /// <para><b>The other direction of <see cref="IsOn"/>, and here beside it on purpose.</b> A
    /// form has to turn a switch on by name, and a second place mapping a key to a property is a
    /// second place for one of thirteen to be wired to the wrong one. An unknown key throws, for
    /// the reason <see cref="IsOn"/> does: a misspelling that silently changed nothing is a
    /// control that looks broken.</para>
    /// </summary>
    public static CampaignTable With(CampaignTable table, string key, bool on)
    {
        ArgumentNullException.ThrowIfNull(table);

        return key switch
        {
            nameof(CampaignTable.ActiveDefensesCost) => table with { ActiveDefensesCost = on },
            nameof(CampaignTable.CloseRangePenalty) => table with { CloseRangePenalty = on },
            nameof(CampaignTable.TheDrop) => table with { TheDrop = on },
            nameof(CampaignTable.FatalDamage) => table with { FatalDamage = on },
            nameof(CampaignTable.FriendlyFire) => table with { FriendlyFire = on },
            nameof(CampaignTable.HardTargets) => table with { HardTargets = on },
            nameof(CampaignTable.RaisedGearLimit) => table with { RaisedGearLimit = on },
            nameof(CampaignTable.SlowHealing) => table with { SlowHealing = on },
            nameof(CampaignTable.ToughMinions) => table with { ToughMinions = on },
            nameof(CampaignTable.WoundPenalties) => table with { WoundPenalties = on },
            nameof(CampaignTable.GmAlternativeToSeizingInitiative) =>
                table with { GmAlternativeToSeizingInitiative = on },
            nameof(CampaignTable.CheckingYourSwing) => table with { CheckingYourSwing = on },
            nameof(CampaignTable.RandomInitiative) => table with { RandomInitiative = on },
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No such table setting.")
        };
    }

    /// <summary>The printed name of one switch, by key.</summary>
    public static string NameOf(string key) =>
        Entries.FirstOrDefault(e => e.Key == key).Name
        ?? throw new ArgumentOutOfRangeException(nameof(key), key, "No such table setting.");
}
