using ProwlersAndParagonsAutomation.Engine;
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
    /// <para>It pins the arithmetic p.80 states; that the fight engine now performs it on a held
    /// weapon is <c>PlayEngineStepTests.AnAttackWithAHeldWeaponIsCappedAndTheWeaponsBonusIsAdded</c>,
    /// and that Chapter 6's own tables give the sword the same two dice is
    /// <c>PlayRulesDataTests.TheTwoChaptersThatPrintTheGearLimitAgreeAboutIt</c>.</para>
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

        // And a table that raised the limit reaches further, which is what the switch buys.
        var raised = TableRules.Book with { RaisedGearLimit = true, GearLimitRank = entry.RaisedOptions[0] };

        Assert.True(
            raised.GearLimit(_play) + entry.WorkedExampleWeaponBonusDice > reach,
            "raising the limit did not raise what the sword reaches");
    }

    /// <summary>
    /// <b>Every switch that is on is announced on the first page, and none of them is announced as
    /// declined.</b>
    ///
    /// <para>A setting accepted and quietly ignored is the worst of the three possible behaviours: a
    /// report would print the setting, the numbers would not carry it, and nothing would say so. So
    /// every switch is announced when the fight opens, and one this engine does not apply would be
    /// announced differently.</para>
    ///
    /// <para><b>The Gear Limit was the last switch on that list and is applied now</b>, so this
    /// fixture drives it as an applied setting rather than as a declined one — with
    /// <see cref="Encounter.SwitchesNotYetApplied"/> asserted empty beside it, because "nothing is
    /// announced as declined" is a different claim from "nothing is on the list", and the guard is
    /// worth having only while both are checked.</para>
    /// </summary>
    [Fact]
    public void EverySwitchThatIsOnIsAnnouncedAndNoneIsDeclined()
    {
        var table = TableRules.Book with
        {
            ToughMinions = true,
            RaisedGearLimit = true,
            GearLimitRank = 9
        };

        var encounter = new Encounter(_play, new SeededDice(11), table);

        var state = encounter.Begin([
            Combatant.Hero("hero", "the Hero", 8, 8, 2,
                new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8 }, ["toughness"]),
            Combatant.Minions("minions", "the Minions", threat: 4, groupSize: 3, "threat")
        ]);

        // The control: the fixture really did turn settings on, so the loop below is over something.
        Assert.Equal(3, table.On().Count);

        foreach (var name in table.On())
        {
            var announcement = Assert.Single(state.Ledger.Lines,
                l => l.Text.StartsWith($"table setting {name} is on", StringComparison.Ordinal));

            Assert.DoesNotContain("not yet implemented", announcement.Text, StringComparison.Ordinal);
        }

        // The Gear Limit's own entry is cited by both of its switches, which is the pair that used
        // to be the one thing on this list.
        Assert.Equal(2, state.Ledger.Lines.Count(
            l => string.Equals(l.Rule, "gritty_raised_gear_limit", StringComparison.Ordinal)));

        Assert.Empty(Encounter.SwitchesNotYetApplied);

        // And the list, were it not empty, would still be a real subset of the settings rather than
        // a stale name list.
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

    // ── What a campaign stored, as what a fight is resolved under ─────────

    /// <summary>
    /// <b>A campaign's block crosses the seam whole, and the crossing is checked against the
    /// switch list rather than against a second list written here.</b>
    ///
    /// <para><c>CampaignTableNamesTests</c> reads <c>TableRules.From</c> as source, which catches
    /// a switch left out of the copy. This is the behaviour on the other side of that scan: every
    /// setting turned on over there is on over here, driven through <see cref="TableRules.IsOn"/>
    /// by the same name list a report echoes. Its control is that the fixture really did turn
    /// everything on — an all-false block would satisfy an all-false conversion.</para>
    /// </summary>
    [Fact]
    public void EverySwitchACampaignTurnedOnIsOnTheTableAFightIsResolvedUnder()
    {
        var campaign = new CampaignTable
        {
            ActiveDefensesCost = true,
            CloseRangePenalty = true,
            TheDrop = true,
            FatalDamage = true,
            FriendlyFire = true,
            HardTargets = true,
            RaisedGearLimit = true,
            SlowHealing = true,
            ToughMinions = true,
            WoundPenalties = true,
            GmAlternativeToSeizingInitiative = true,
            CheckingYourSwing = true,
            RandomInitiative = true,
            GearLimitRank = 12
        };

        // The control: the fixture above has to have turned something on, or the conversion below
        // could drop every switch and still agree with it.
        Assert.False(campaign.IsTheBook);

        var table = TableRules.From(campaign);

        var names = TableRules.Switches.Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(names.Count >= 11, $"only {names.Count} settings were found to check.");

        var off = names.Where(name => !table.IsOn(name)).ToList();

        Assert.True(off.Count == 0,
            "A campaign turned these on and the table the fight is resolved under has them off: "
            + string.Join(", ", off));

        // The one that is a figure rather than a flag: 12 has to arrive as 12, not as "set".
        Assert.Equal(12, table.GearLimitRank);

        // And the whole list comes back on the report a rate is quoted with.
        Assert.Equal(names.Order(StringComparer.Ordinal), table.On().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>A campaign that stored nothing is the book, and so is one that stored a block with
    /// nothing on.</b> p.79 offers the ten as optional, so a table that has said nothing has said
    /// "the book as printed" — which is what lets a character saved before any of this existed
    /// fight under the game it was always playing.
    /// </summary>
    [Fact]
    public void ACampaignThatAdoptedNothingIsTheBook()
    {
        Assert.Equal(TableRules.Book, TableRules.From(null));
        Assert.Equal(TableRules.Book, TableRules.From(new CampaignTable()));
        Assert.Empty(TableRules.From(CampaignTable.Book).On());
    }

    /// <summary>
    /// <b>Two tables that disagree are told apart by the setting they disagree about, and two that
    /// agree are not told apart at all.</b>
    ///
    /// <para>The naming is the whole product here: a fight refused because its sheets disagree is
    /// refused with the switch in the message, so somebody can go and fix the one that is wrong.
    /// The rank is compared as a figure rather than as a presence, which
    /// <see cref="TableRules.IsOn"/> alone would not do.</para>
    /// </summary>
    [Fact]
    public void TheFirstDisagreementIsNamedAndAgreementIsSilent()
    {
        var book = TableRules.Book;
        var gritty = TableRules.From(new CampaignTable { WoundPenalties = true });

        Assert.Null(TableRules.FirstDifference(book, TableRules.From(new CampaignTable())));
        Assert.Null(TableRules.FirstDifference(gritty, gritty with { }));

        Assert.Equal(nameof(TableRules.WoundPenalties), TableRules.FirstDifference(book, gritty));
        Assert.Equal(nameof(TableRules.WoundPenalties), TableRules.FirstDifference(gritty, book));

        // Two raised limits at different ranks are two different games, and IsOn cannot see it.
        var nine = TableRules.From(new CampaignTable { RaisedGearLimit = true, GearLimitRank = 9 });
        var twelve = TableRules.From(new CampaignTable { RaisedGearLimit = true, GearLimitRank = 12 });

        Assert.True(nine.IsOn(nameof(TableRules.GearLimitRank)));
        Assert.True(twelve.IsOn(nameof(TableRules.GearLimitRank)));
        Assert.Equal(nameof(TableRules.GearLimitRank), TableRules.FirstDifference(nine, twelve));
    }

    /// <summary>
    /// <b>A Gear Limit rank behind a switch nobody turned on is not a disagreement, because it is
    /// not a setting.</b>
    ///
    /// <para><see cref="TableRules.GearLimit"/> reads the figure only where <c>RaisedGearLimit</c>
    /// is on, and the field's own doc says that is why the two are separate fields at all: "a rank
    /// left here while the switch is off is a figure the table has not adopted". Two tables that
    /// both left the switch off are playing the same game whatever numbers sit behind it — every
    /// roll in a fight between them is identical — so a fight refused over it is a fight blocked by
    /// a figure nothing reads.</para>
    ///
    /// <para><b>It is also a refusal nobody could act on.</b> The campaign form clears the rank
    /// when the switch goes off and does not draw the input while it is off, so the only repair
    /// available to a GM handed that refusal is hand-editing character JSON — which is what the
    /// decision beside this one, to accept a sheet carrying no table at all, exists to spare them.
    /// The rule this leaves standing is the one that matters: a setting a table <em>has</em>
    /// adopted is never quietly ignored.</para>
    ///
    /// <para><b>Two controls keep it from being a hole.</b> The switch on for either side brings
    /// the figure back into the comparison; and a table with the switch on against one with it off
    /// is still named — by <c>RaisedGearLimit</c>, which comes earlier in
    /// <see cref="TableRules.Switches"/> and is the more useful of the two names.</para>
    /// </summary>
    [Fact]
    public void ARankBehindAnUnadoptedGearLimitSwitchIsNotADisagreement()
    {
        var six = TableRules.From(new CampaignTable { GearLimitRank = 6 });
        var twelve = TableRules.From(new CampaignTable { GearLimitRank = 12 });
        var none = TableRules.From(new CampaignTable());

        Assert.Null(TableRules.FirstDifference(six, twelve));
        Assert.Null(TableRules.FirstDifference(six, none));
        Assert.Null(TableRules.FirstDifference(none, six));

        // Nothing in a fight between them could tell them apart either.
        Assert.Equal(six.GearLimit(_play), twelve.GearLimit(_play));

        // The first control: adopted on one side, and the switch itself is named — earlier in
        // Switches than the rank, and the more actionable of the two.
        var adopted = TableRules.From(new CampaignTable { RaisedGearLimit = true, GearLimitRank = 12 });

        Assert.Equal(nameof(TableRules.RaisedGearLimit), TableRules.FirstDifference(six, adopted));

        // Adopted on both, and the figure is a disagreement again.
        var alsoAdopted = TableRules.From(new CampaignTable { RaisedGearLimit = true, GearLimitRank = 6 });

        Assert.Equal(nameof(TableRules.GearLimitRank), TableRules.FirstDifference(adopted, alsoAdopted));

        // The second control: the ranks really were different, so the nulls above are about the
        // switch and not about a comparison that has stopped reading the figure at all.
        Assert.NotEqual(six.GearLimitRank, twelve.GearLimitRank);
    }
}
