using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>Every table setting names a rule the book prints, and the baseline is the book's.</b>
///
/// <para>A switch is a claim that a page offers the table a choice. One whose id matched no entry
/// would be a rule this project had invented, which is the fastest route there is to a simulator
/// applying something nobody wrote — so the settings are walked against the shipped data rather than
/// trusted. It is an allowlist of settings, not a denylist, so one added under a name nobody
/// anticipated is flagged rather than missed.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayTableRulesTests
{
    private readonly PlayRulesRepository _play;

    public PlayTableRulesTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    /// <summary>
    /// Each switch's id is an entry in the file it names, and the file is one of the three that
    /// print a table's choices.
    /// </summary>
    [Fact]
    public void EveryTableSettingNamesAnEntryTheBookPrints()
    {
        var known = _play.EntryIds().ToHashSet();

        // The positive control on the instrument: the store really was read. An empty set would
        // satisfy nothing below, but a set built from the wrong file would satisfy some of it.
        Assert.True(known.Count > 100, $"only {known.Count} entries were read from the play rules.");

        var faults = TableRules.Switches
            .Where(s => !known.Contains((s.File, s.EntryId)))
            .Select(s => $"{s.Name} names {s.EntryId} in {s.File}")
            .ToList();

        Assert.True(faults.Count == 0,
            "A table setting is a claim that the book offers the table this choice. These name no "
            + "entry, so they are rules this project invented: " + string.Join(", ", faults));
    }

    /// <summary>
    /// <b>The ten Gritty Combat Rules are all there and are all switches.</b> A rule quietly dropped
    /// from <see cref="TableRules"/> would leave every other test here green while a table that
    /// asked for it got the book's baseline instead.
    /// </summary>
    [Fact]
    public void EveryGrittyRuleIsATableSetting()
    {
        var gritty = _play.Gritty.Entries
            .Where(e => string.Equals(e.Kind, "table_setting", StringComparison.Ordinal))
            .Select(e => e.Id)
            .ToList();

        Assert.Equal(10, gritty.Count);

        var named = TableRules.Switches
            .Where(s => string.Equals(s.File, PlayRulesRepository.GrittyFile, StringComparison.Ordinal))
            .Select(s => s.EntryId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(gritty.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>The default is the book's baseline: every gritty rule off.</b> p.79 introduces the ten as
    /// optional and tells a table to review them before adopting any, so a simulator whose default
    /// was anything else would be measuring a game the book does not describe.
    /// </summary>
    [Fact]
    public void TheDefaultTableHasEveryGrittyRuleOff()
    {
        Assert.Empty(TableRules.Book.On());

        // And the switch mechanism is doing something: turning one on shows up.
        var gritty = TableRules.Book with { FatalDamage = true };

        Assert.Equal([nameof(TableRules.FatalDamage)], gritty.On());
        Assert.True(gritty.IsOn(nameof(TableRules.FatalDamage)));
        Assert.False(gritty.IsOn(nameof(TableRules.ToughMinions)));
    }

    /// <summary>
    /// <b>The Gear Limit's default is the entry's, not a number written in the engine.</b> p.80 sets
    /// it at six dice in most games and offers nine, twelve or beyond.
    /// </summary>
    [Fact]
    public void TheGearLimitComesFromTheEntryUntilATableRaisesIt()
    {
        var entry = _play.GetGritty("gritty_raised_gear_limit").GearLimit!;

        Assert.Equal(entry.DefaultRank, TableRules.Book.GearLimit(_play));

        var raised = TableRules.Book with { RaisedGearLimit = true, GearLimitRank = entry.RaisedOptions[1] };

        Assert.Equal(entry.RaisedOptions[1], raised.GearLimit(_play));
        Assert.NotEqual(entry.DefaultRank, raised.GearLimit(_play));
    }

    /// <summary>
    /// <b>p.80's own sword comes out at the rank the page prints.</b>
    ///
    /// <para>The one worked example the Gear Limit entry carries: a basic sword is +2d, so at the
    /// default limit of six the most a character can bring to bear with it is eight — "even if you
    /// have more than 6d Might". It is <em>computed</em> from the limit this engine reads and the
    /// bonus the entry carries, and compared with the answer the authors worked through, which is
    /// the only check here that two transcriptions cannot both pass by agreeing with each other.
    /// </para>
    ///
    /// <para>It is worth pinning even though the switch is not applied — see
    /// <c>docs/guide/play-engine.md</c> for why it cannot be. The figure this fixture drives is the
    /// one thing about the rule the engine <em>does</em> compute, and a default that had drifted
    /// would build a sword the page does not print.</para>
    /// </summary>
    [Fact]
    public void TheSwordOnPageEightyReachesTheRankTheExampleWorksThrough()
    {
        var entry = _play.GetGritty("gritty_raised_gear_limit").GearLimit!;

        // The control: the example really is a weapon with a bonus on it, so the sum below is a
        // sum of two figures rather than of one and a zero.
        Assert.NotEmpty(entry.WorkedExampleWeapon);
        Assert.True(entry.WorkedExampleWeaponBonusDice > 0);

        var reach = TableRules.Book.GearLimit(_play) + entry.WorkedExampleWeaponBonusDice;

        Assert.Equal(entry.WorkedExampleMaximumEffectiveRankAtTheDefaultLimit, reach);

        // And a table that raised the limit reaches further, which is what the switch would buy if
        // anything here could hold a sword.
        var raised = TableRules.Book with { RaisedGearLimit = true, GearLimitRank = entry.RaisedOptions[0] };

        Assert.True(
            raised.GearLimit(_play) + entry.WorkedExampleWeaponBonusDice > reach,
            "raising the limit did not raise what the sword reaches");
    }

    /// <summary>
    /// <b>A switch that is on but not yet applied says so on the ledger, on the first page.</b>
    ///
    /// <para>A setting accepted and quietly ignored is the worst of the three possible behaviours: a
    /// report would print the setting, the numbers would not carry it, and nothing would say so. So
    /// every switch is announced when the fight opens, and the ones this slice does not apply are
    /// announced differently.</para>
    ///
    /// <para><b>The unapplied one is the Gear Limit</b>, deliberately: it is the switch this engine
    /// cannot apply <em>at all</em> — a fight here has no equipment in it, so there is no Trait
    /// brought to bear through gear for a limit to cap — where every other member of that list is
    /// one a later slice may take off it and leave this fixture asserting something no longer true.
    /// </para>
    /// </summary>
    [Fact]
    public void ASwitchThatIsOnButNotYetAppliedIsAnnouncedAsSuch()
    {
        var table = TableRules.Book with
        {
            ToughMinions = true,     // applied
            RaisedGearLimit = true   // recorded, not yet applied
        };

        var encounter = new Encounter(_play, new SeededDice(11), table);

        var state = encounter.Begin([
            Combatant.Hero("hero", "the Hero", 8, 8, 2,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 }, ["toughness"]),
            Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 3, "threat")
        ]);

        var applied = Assert.Single(state.Ledger.Lines,
            l => string.Equals(l.Rule, "gritty_tough_minions", StringComparison.Ordinal));
        Assert.DoesNotContain("not yet implemented", applied.Text, StringComparison.Ordinal);

        var pending = Assert.Single(state.Ledger.Lines,
            l => string.Equals(l.Rule, "gritty_raised_gear_limit", StringComparison.Ordinal));
        Assert.Contains("not yet implemented", pending.Text, StringComparison.Ordinal);

        // The list of unapplied switches is a real subset of the settings, not a stale name list.
        Assert.All(Encounter.SwitchesNotYetApplied, name =>
            Assert.Contains(TableRules.Switches, s => string.Equals(s.Name, name, StringComparison.Ordinal)));
    }

    /// <summary>
    /// <b>Checking Your Swing swaps the success map rather than correcting the answer.</b> Under it
    /// every even face is worth one success, so a hand of three sixes scores 6 by the printed map
    /// and 3 by the flattened one. Both are computed here, from the two entries' own maps.
    /// </summary>
    [Fact]
    public void CheckingYourSwingFlattensTheSix()
    {
        int[] threeSixes = [6, 6, 6];

        var printed = new SuccessCounter(_play);
        var flattened = new SuccessCounter(_play, checkingYourSwing: true);

        Assert.Equal(6, printed.Count(threeSixes));
        Assert.Equal(3, flattened.Count(threeSixes));

        // A two and a four are worth the same under both, which is the half of the rule that does
        // not change and is what makes the assertion above about the six in particular.
        Assert.Equal(printed.Count([2, 4]), flattened.Count([2, 4]));

        // And the citation moves with the map, so a ledger line can say which one was in force.
        Assert.NotEqual(printed.MapSourceRef, flattened.MapSourceRef);
        Assert.Contains("p.69", flattened.MapSourceRef, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The sub-1d floor is a rule, not a pool of nothing.</b> p.67: one die is still thrown, it
    /// scores only on the faces the entry names, and it scores only what the entry says even then.
    /// </summary>
    [Fact]
    public void APoolBelowOneDieStillThrowsOneDieAndOnlyASixCounts()
    {
        var counter = new SuccessCounter(_play);

        var six = counter.Roll(-3, new ScriptedDice(6));
        Assert.True(six.FlooredToOneDie);
        Assert.Single(six.Faces);
        Assert.Equal(1, six.Successes);   // a six, worth one rather than the usual two

        var four = counter.Roll(0, new ScriptedDice(4));
        Assert.True(four.FlooredToOneDie);
        Assert.Equal(0, four.Successes);  // "Every other face is a total miss"

        // A pool of exactly one die is an ordinary pool, not a floored one.
        var one = counter.Roll(1, new ScriptedDice(4));
        Assert.False(one.FlooredToOneDie);
        Assert.Equal(1, one.Successes);
    }
}
