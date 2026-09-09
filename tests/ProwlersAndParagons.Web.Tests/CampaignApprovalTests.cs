using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Sheets;

using ProwlersAndParagons.Testing;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// A campaign holds a clone of a character; a player's edits arrive as an approval request.
///
/// <para><b>Four properties here are defects if they are wrong, rather than features that do not
/// work:</b></para>
/// <list type="number">
///   <item><b>The diff really compared two sheets.</b> A diff showing nothing and a diff that
///     failed to run are indistinguishable on screen, and "nothing changed" is the commonest
///     honest answer this screen gives — so the comparison carries a count of fields
///     <em>examined</em>, and it is asserted rather than trusted.</item>
///   <item><b>A stale decision is refused, and the refusal brings the newer snapshot back.</b>
///     Without it the GM approves a character nobody has looked at.</item>
///   <item><b>Nothing anywhere prints a field name from a stored payload.</b> A row reading
///     <c>selectedTierId</c> is the app describing its own internals, which is the fault the rules
///     reference's passage counts were taken off a panel for. Checked against the real property
///     names of <see cref="CharacterSheet"/>, so a field added later cannot leak its own spelling
///     by being appended to a loop.</item>
///   <item><b>Nothing is repaired.</b> An illegal snapshot is reported and decided on as it
///     stands; a tier that disagrees with the campaign's is information.</item>
/// </list>
///
/// <para><b>What this file cannot see:</b> it drives the browser's half against
/// <see cref="FakeApi"/>. That the server's own scoping and compare-and-swap hold is driven
/// against the real code in real SQLite by <c>tests/worker/memberships.test.mjs</c>.</para>
/// </summary>
public sealed class CampaignApprovalTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly DerivedStatsCalculator Derived = new(Rules);
    private static readonly CharacterValidator Validator = new(Rules, Costs, Derived);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static ApiMembershipStore StoreFor(FakeApi server) =>
        new(new HttpClient(server) { BaseAddress = new Uri("https://pp.example.test/") },
            Costs, Validator);

    private const string PlayerCharacter = "c_0000000000000000000000";

    /// <summary>
    /// A GM's server with a campaign, and a player joined to it — the state the approval screen
    /// starts from. Driven through the real join route, so a test about approval cannot pass
    /// against a join that has stopped working.
    /// </summary>
    private static async Task<(FakeApi Server, ApiMembershipStore Store, string Membership)>
        ATable(Campaign? campaign = null)
    {
        var server = new FakeApi { SignedIn = ("u_gm", "The GM") };
        var store = StoreFor(server);

        var code = server.Campaign(
            "g_0000000000000000000000", "Nightfall",
            StoredCampaign.Write(campaign
                ?? new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false)));

        server.SignedIn = ("u_player", "The Player");

        var joined = await store.JoinAsync(code, PlayerCharacter, "Ninefold");

        Assert.NotNull(joined);

        return (server, store, joined!.Value.Id);
    }

    private static CharacterSheet ASheet(
        string name = "Ninefold", string? tier = "standard", int might = 6, int flight = 0)
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId = tier,
            Name = name,
            AbilityRanks = { ["might"] = might, ["agility"] = 4 },
            TalentRanks = { ["covert"] = 3 },
        };

        if (flight > 0) sheet.SelectedPowers.Add(new SelectedPower("flight", flight));

        return sheet;
    }

    // ── The diff, and its positive control ──────────────────────────────────────────────

    /// <summary>
    /// <b>The diff really did compare two sheets and find the changes.</b>
    ///
    /// <para><b>This is the positive control, and it is the guard the design asks for by name.</b>
    /// Every other assertion about a diff is about what is <em>in</em> it, and every one of those
    /// is satisfied by a comparison that examined nothing at all and returned an empty list — the
    /// failure shape this repository has shipped four times. So the count of fields examined is
    /// asserted first, and the rows second.</para>
    /// </summary>
    [Fact]
    public void TheDiffReallyComparedTwoSheets()
    {
        var before = ASheet(might: 6);
        var after = ASheet(might: 8, flight: 4);
        after.Name = "Ninefold Prime";
        after.SelectedTierId = "high_level";

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        // The control: something was actually looked at. A comparison that had stopped comparing
        // reports zero and every assertion below would hold trivially.
        Assert.True(diff.Ran, "the comparison examined no fields at all");
        Assert.True(diff.Compared > 8,
            $"only {diff.Compared} fields were examined, which is fewer than a sheet has");

        Assert.False(diff.Unchanged);

        // The spend is the engine's answer for each sheet, not a difference worked out anywhere.
        Assert.Equal(Costs.TotalCost(before), diff.SpentBefore);
        Assert.Equal(Costs.TotalCost(after), diff.SpentAfter);
        Assert.NotEqual(diff.SpentBefore, diff.SpentAfter);

        var rows = diff.Rows.ToDictionary(r => r.What, StringComparer.Ordinal);

        // Named the way the book names them, with the tier looked up rather than printed as an id.
        Assert.Equal("Standard", rows["Tier"].Before);
        Assert.Equal("High Level", rows["Tier"].After);

        Assert.Equal("6d", rows["Might"].Before);
        Assert.Equal("8d", rows["Might"].After);

        Assert.Equal("Ninefold", rows["Name"].Before);
        Assert.Equal("Ninefold Prime", rows["Name"].After);

        Assert.Equal(DiffKind.Added, rows["Power: Flight"].Kind);
        Assert.Equal("4d", rows["Power: Flight"].After);
        Assert.Null(rows["Power: Flight"].Before);
    }

    /// <summary>
    /// <b>Two identical sheets compare to nothing, and the count says the comparison happened.</b>
    ///
    /// <para>This is the case the control exists for: a GM looking at a resubmission that changed
    /// nothing has to be able to tell that from a screen that broke.</para>
    /// </summary>
    [Fact]
    public void NothingChangedIsDistinguishableFromNothingCompared()
    {
        var diff = CampaignDiff.Between(ASheet(), ASheet(), Rules, Costs);

        Assert.True(diff.Unchanged);
        Assert.Empty(diff.Rows);

        // And yet it ran, which is the whole distinction.
        Assert.True(diff.Ran);

        // Eight is what two minimal sheets have between them: three plain fields, two Abilities,
        // one Talent, and the two settings. The bound is loose enough that adding a field does not
        // need this edited and tight enough to catch a comparison that has stopped comparing —
        // which reports zero, the direction that matters.
        Assert.True(diff.Compared >= 8, $"only {diff.Compared} fields were examined");
    }

    /// <summary>
    /// A first submission has no clone to compare against, so everything with a value is an
    /// addition — and the count still says how much was looked at.
    /// </summary>
    [Fact]
    public void AFirstSubmissionIsAllAdditions()
    {
        var diff = CampaignDiff.Between(null, ASheet(flight: 4), Rules, Costs);

        Assert.True(diff.Ran);
        Assert.Null(diff.SpentBefore);
        Assert.NotNull(diff.SpentAfter);

        Assert.Contains(diff.Rows, r => r is { What: "Tier", Kind: DiffKind.Added });
        Assert.Contains(diff.Rows, r => r is { What: "Power: Flight", Kind: DiffKind.Added });
        Assert.DoesNotContain(diff.Rows, r => r.Kind == DiffKind.Removed);
    }

    /// <summary>
    /// <b>A change that moves the spend moves a row, for every cost-bearing field there is.</b>
    ///
    /// <para><b>This is the guard the slice shipped without, and the defect was real.</b> The diff
    /// compared a Power on its id and its purchased ranks alone — so moving Immunity from one unit
    /// to six produced <c>spend 7 → 22</c> and an <em>empty</em> list of changes, under a header
    /// reading 7 → 22 Hero Points. Pros, Cons, cost variants, units, nominated Traits, an
    /// Ability's own modifiers and every custom feature of a piece of gear were all invisible, and
    /// gear features are the only thing gear costs Hero Points for.</para>
    ///
    /// <para><b><c>Compared</c> could not catch it and was never going to.</b> It counts fields
    /// <em>examined</em>, which is the right guard against a comparison that has stopped running
    /// and worth nothing against one that runs and looks at the wrong half of a field: the Power's
    /// key <em>was</em> examined, so it reported a healthy 7. The check that works is the one
    /// below — the engine's own two figures, held against the rows.</para>
    ///
    /// <para>Each case is driven through <see cref="CampaignDiff"/> rather than asserted on a row
    /// name, because what a row should be <em>called</em> is a judgement and what it must
    /// <em>exist</em> for is not. The positive control on every case is its own first assertion:
    /// the mutation has to move the spend, or it is testing nothing.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(CostBearingChanges))]
    public void EveryChangeThatMovesTheSpendMovesARow(string what, Action<CharacterSheet> change)
    {
        var before = ACostedSheet();
        var after = ACostedSheet();
        change(after);

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        // The positive control, and it comes first: a fixture whose mutation does not move the
        // spend is a case that proves nothing, and this repository has shipped three of those.
        Assert.True(diff is { SpentBefore: not null, SpentAfter: not null },
            $"{what}: the engine declined to price one of the sheets, so this case tests nothing");
        Assert.True(diff.SpentBefore != diff.SpentAfter,
            $"{what}: the mutation did not move the spend ({diff.SpentBefore}), so it is a no-op");

        Assert.True(diff.Rows.Count > 0,
            $"{what}: the spend moved {diff.SpentBefore} → {diff.SpentAfter} and no row says why");
        Assert.True(diff.Explained, $"{what}: Explained disagreed with the rows");
        Assert.False(diff.Unchanged, $"{what}: reported as unchanged while the spend moved");
    }

    public static TheoryData<string, Action<CharacterSheet>> CostBearingChanges() => new()
    {
        { "an Ability's rank", s => s.AbilityRanks["might"] = 8 },
        { "a Talent's rank", s => s.TalentRanks["academics"] = 5 },
        {
            "a Pro applied to an Ability",
            s => s.AbilityModifiers["might"] = [new SelectedProCon("armor_piercing")]
        },
        {
            "a Power's purchased ranks",
            s => Replace(s, p => p with { PurchasedRanks = p.PurchasedRanks + 3 })
        },
        {
            "a Power's units",
            s => Replace(s, p => p with { PowerId = "immunity", PurchasedRanks = 0, Units = 6 })
        },
        {
            "a Pro on a Power",
            s => Replace(s, p => p with { Pros = [new SelectedProCon("armor_piercing")] })
        },
        {
            "a Con on a Power",
            s => Replace(s, p => p with { Cons = [new SelectedProCon("burnout")] })
        },
        { "a Perk's unit count", s => ReplacePerk(s, p => p with { Units = 4 }) },
        {
            "a custom feature on a piece of gear",
            s => ReplaceGear(s, g => g with { Features = [new SelectedGearFeature("deflecting")] })
        },
        {
            "a Pro on a piece of gear",
            s => ReplaceGear(s, g => g with { Pros = [new SelectedProCon("armor_piercing")] })
        },
    };

    /// <summary>
    /// <b>The fields that move the diff without moving the spend, held to the same rule.</b>
    ///
    /// <para>Split from the theory above rather than folded into it, because its positive control
    /// is that the mutation moves the <em>spend</em> — and a Tier, a Hero Point limit, a Flaw and
    /// a narrative note move none. Each of these is still a thing a GM decides about: a Villain in
    /// a Hero campaign, a character built with no limit, and the Flaws that are the players'
    /// handles on one. So the requirement is the row, without the spend as its trigger.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(FreeButReportableChanges))]
    public void EveryChangeAGmDecidesAboutMovesARow(string what, Action<CharacterSheet> change)
    {
        var before = ACostedSheet();
        var after = ACostedSheet();
        change(after);

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        Assert.True(diff.Ran, $"{what}: the comparison did not run");
        Assert.True(diff.Rows.Count > 0, $"{what}: nothing was reported to the GM");
    }

    /// <summary>
    /// <b>The Trait Cap row is the cap in force, in the words every rank in this app is written
    /// in.</b>
    ///
    /// <para>The theory above only requires <em>a</em> row; this requires the right one. It is
    /// <c>Trait Cap 12d → 6d</c> and not <c>TraitCapRank: → 6</c>, because the field going from
    /// absent to present is this application's bookkeeping and the ceiling moving from the
    /// Standard tier's 12d to a table's 6d is what the GM is deciding about.</para>
    ///
    /// <para>The last case is the control that keeps the rule honest in the other direction: a
    /// house cap written at exactly the tier's own ceiling moves nothing and says nothing, the
    /// same way a rank stepped back to zero reads as removed rather than as <c>0d</c>.</para>
    /// </summary>
    [Fact]
    public void TheTraitCapRowNamesBothCeilingsTheWayARankIsWritten()
    {
        var before = ASheet();
        var after = ASheet();
        after.TraitCapRank = 6;

        var row = CampaignDiff.Between(before, after, Rules, Costs).Rows
            .Single(r => r.What == "Trait Cap");

        Assert.Equal("12d", row.Before);
        Assert.Equal("6d", row.After);
        Assert.Equal(DiffKind.Changed, row.Kind);

        // A house cap equal to the tier's is not a change to the character.
        var same = ASheet();
        same.TraitCapRank = 12;

        Assert.DoesNotContain(CampaignDiff.Between(before, same, Rules, Costs).Rows,
            r => r.What == "Trait Cap");
    }

    /// <summary>
    /// <b>The Immortality row is shown under a character that has the Power, and under no other.</b>
    ///
    /// <para>A table's price for Immortality is Hero Points: a campaign that raised it moved the
    /// spend on every character in it that has the Power, so the GM is owed the reason beside the
    /// figure. It reads <c>Immortality 3 HP → 9 HP</c> — the book's own price standing in for a
    /// character that carries none, because <c>null → 9</c> is this application's bookkeeping and
    /// not what changed for the character. That is the Trait Cap row's rule, one field over.</para>
    ///
    /// <para><b>And it is drawn only where somebody is charged it.</b> A price nobody pays is a
    /// row a GM cannot act on, and this screen's whole job is to say what is different about
    /// <em>this</em> character.</para>
    /// </summary>
    [Fact]
    public void TheImmortalityRowIsDrawnOnlyUnderACharacterThatHasThePower()
    {
        var before = ASheet();
        before.SelectedPowers.Add(new SelectedPower("immortality", 0));

        var after = ASheet();
        after.SelectedPowers.Add(new SelectedPower("immortality", 0));
        after.ImmortalityCost = 9;

        var row = CampaignDiff.Between(before, after, Rules, Costs).Rows
            .Single(r => r.What == "Immortality");

        Assert.Equal("3 HP", row.Before);
        Assert.Equal("9 HP", row.After);
        Assert.Equal(DiffKind.Changed, row.Kind);

        // The same price change under a character without the Power says nothing, because nobody
        // is charged it. The spend row is silent for the same reason, which is the control that
        // this is about the Power and not about the diff having stopped running.
        var noPower = ASheet();
        noPower.ImmortalityCost = 9;

        var quiet = CampaignDiff.Between(ASheet(), noPower, Rules, Costs);

        Assert.True(quiet.Ran);
        Assert.DoesNotContain(quiet.Rows, r => r.What == "Immortality");
    }

    /// <summary>
    /// <b>An optional rule the table adopted is one row per switch, named the way the book names
    /// it.</b>
    ///
    /// <para>One row per switch rather than one row for the block, because "House rules changed"
    /// is exactly the sentence this screen exists not to print: a GM deciding about a submission
    /// needs to know <em>which</em> rule, and thirteen switches in one row is a diff the reader
    /// has to do themselves.</para>
    ///
    /// <para><b>The control is the other direction</b>: a character joining a table that has
    /// adopted nothing gains a block whose every switch is off, which is the same game it was
    /// already playing — so <c>null</c> and "all off" produce no rows at all, and a row saying
    /// "House rules added" would be the application reporting its own storage.</para>
    /// </summary>
    [Fact]
    public void EachOptionalRuleThatMovedIsItsOwnRow()
    {
        var after = ASheet();
        after.CampaignTable = new CampaignTable
        {
            FatalDamage = true, RaisedGearLimit = true, GearLimitRank = 12
        };

        var rows = CampaignDiff.Between(ASheet(), after, Rules, Costs).Rows;

        var fatal = Assert.Single(rows, r => r.What == "Fatal Damage");
        Assert.Equal("off", fatal.Before);
        Assert.Equal("on", fatal.After);

        Assert.Single(rows, r => r.What == "Raised Gear Limit");
        Assert.Single(rows, r => r.What == "Gear Limit");

        // Nothing about the twelve nobody touched.
        Assert.DoesNotContain(rows, r => r.What == "The Drop");

        // A block that arrived carrying nothing is the same game, so no row at all.
        var adoptedNothing = ASheet();
        adoptedNothing.CampaignTable = CampaignTable.Book;

        var quiet = CampaignDiff.Between(ASheet(), adoptedNothing, Rules, Costs);

        Assert.True(quiet.Ran);
        Assert.DoesNotContain(quiet.Rows,
            r => HouseRuleFormatter.All.Any(e => e.Name == r.What) || r.What == "Gear Limit");
    }

    public static TheoryData<string, Action<CharacterSheet>> FreeButReportableChanges() => new()
    {
        { "the tier", s => s.SelectedTierId = "high_level" },

        // **A house Trait Cap costs nothing and changes what the character is.** It is the datum
        // Resolve is measured from and the ceiling every Ability is judged against, so a player
        // who set one between submissions moved their Resolve and could turn a legal Trait
        // illegal — while this screen, which compared no such field, said nothing changed. The
        // fixture is at the Standard tier, whose cap is 12d, so this really is 12d → 6d and not a
        // row about a field going from absent to present.
        { "a house Trait Cap", s => s.TraitCapRank = 6 },

        // The table's optional rules cost nothing either, and they change what happens when this
        // sheet is used in a fight — which is the whole of what a GM is deciding about.
        { "an optional rule the table adopted",
            s => s.CampaignTable = new CampaignTable { FatalDamage = true } },

        { "building without a points limit", s => s.UnlimitedBudget = true },
        { "Hero or Villain", s => s.IsVillain = true },
        { "a Flaw taken", s => s.Flaws.Add(new SelectedFlaw("alter_ego")) },
        {
            "what a Flaw was written to mean",
            s => s.Flaws.Add(new SelectedFlaw("alter_ego", "Answers to a different name at work"))
        },
        { "a plain piece of gear", s => s.Gear.Add(new SelectedGear("A borrowed van")) },
        {
            "which Source a Power comes from",
            s => Replace(s, p => p with { SourceId = "training" })
        },

        // ── Three that only a fixture carrying the thing can reach ────────────────────
        //
        // Each of these was mutated in the production file and the whole class stayed green,
        // for the reason this file's own fixture comment already records: the cases ADDED the
        // element, and an element that was not there and now is produces a row whatever its
        // detail says. `ACostedSheet` carries all three now and these change them.

        {
            "what a Perk was written to mean",
            s => ReplacePerk(s, p => p with { NarrativeDetail = "A lock-up under the arches" })
        },
        {
            "a piece of gear leaving its matched pair",
            s => ReplaceGear(s, g => g with { PairedUnderTwoFisted = false })
        },
        {
            "how many units of a Pro are bought",
            s => ReplaceGear(s, g => g with
            {
                Pros = [new SelectedProCon("affect_inanimate") { Units = 5 }],
            })
        },

        // ── Six the diff did not look at at all ───────────────────────────────────────
        //
        // Every one of them prints on the sheet and none of them costs a Hero Point, so the
        // spend cannot be the trigger. A Source is the sharper half: it is not derivable from a
        // rank, it decides which Pros an option allows, and it prints inside a Power group.

        { "how the character looks", s => s.Appearance = "Taller than the file says" },
        { "what drives them", s => s.Motivation = "Owes a debt nobody has called in" },
        { "what they say", s => s.Quote = "“Not tonight.”" },
        { "who they know", s => s.Connections.Add("A sergeant at the 14th") },
        { "which Source an Ability comes from", s => s.AbilitySources["might"] = "tech" },
        { "which Source a Talent comes from", s => s.TalentSources["academics"] = "psychic" },
    };

    /// <summary>
    /// A sheet with something of every kind already on it, so a mutation has something to move.
    ///
    /// <para><b>The Power, the Perk and the piece of gear are here rather than added by the cases,
    /// and that is the fixture's whole job.</b> The gear cases first <em>added</em> a customised
    /// Blaster — so nulling <c>GearDetail</c> entirely, which is exactly how the slice shipped,
    /// left them green: an item that was not there and now is produces an Added row whatever its
    /// detail says, and the behaviour under test was never reached. Changing an item that is
    /// already there is the only mutation that can tell a detail comparison from no comparison,
    /// and re-running against that null proved it.</para>
    /// </summary>
    private static CharacterSheet ACostedSheet()
    {
        var sheet = new CharacterSheet
        {
            Name = "The Control",
            SelectedTierId = "standard",
            AbilityRanks = { ["might"] = 5 },
            TalentRanks = { ["academics"] = 3 },
            SelectedPowers = { new SelectedPower("flight", 4) },
            Perks = { new SelectedPerk("headquarters", NarrativeDetail: "A brownstone on Cutter Street") },
            Gear =
            {
                new SelectedGear("Blaster")
                {
                    PairedUnderTwoFisted = true,
                    Pros = [new SelectedProCon("affect_inanimate") { Units = 2 }],
                },
            },
        };

        return sheet;
    }

    /// <summary>Rewrites the one Power on the fixture, so a case can name what it changed.</summary>
    private static void Replace(CharacterSheet sheet, Func<SelectedPower, SelectedPower> change)
    {
        var only = sheet.SelectedPowers[0];

        sheet.SelectedPowers.Clear();
        sheet.SelectedPowers.Add(change(only));
    }

    /// <summary>The same for the one Perk, and for the same reason.</summary>
    private static void ReplacePerk(CharacterSheet sheet, Func<SelectedPerk, SelectedPerk> change)
    {
        var only = sheet.Perks[0];

        sheet.Perks.Clear();
        sheet.Perks.Add(change(only));
    }

    /// <summary>And for the one piece of gear, which is where the fixture fault actually was.</summary>
    private static void ReplaceGear(CharacterSheet sheet, Func<SelectedGear, SelectedGear> change)
    {
        var only = sheet.Gear[0];

        sheet.Gear.Clear();
        sheet.Gear.Add(change(only));
    }

    /// <summary>
    /// <b>No row anywhere in a diff prints a field name from a stored payload.</b>
    ///
    /// <para><b>Checked against the real property names of <see cref="CharacterSheet"/></b>, read
    /// by reflection rather than from a list here — so a field added later is covered without
    /// anybody remembering this test, which is exactly how <c>selectedTierId</c> would reach a
    /// screen: by being appended to a loop.</para>
    ///
    /// <para>The positive control is that the diff has rows at all, and that the reflection found
    /// properties: an empty row set satisfies every absence, and so does an empty name set.</para>
    /// </summary>
    [Fact]
    public void NoRowNamesAFieldOfAStoredCharacter()
    {
        var before = new CharacterSheet();

        // **A tier whose id carries an underscore, and that is the fixture's whole job.** It was
        // `standard` at first, and a mutation printing the tier's *id* instead of its printed name
        // left this test green — `standard` has no underscore and is not in the named list below,
        // so the leak the test exists to catch walked through its own fixture. `high_level` prints
        // as "High Level", which the underscore rule can tell apart.
        var after = ASheet(tier: "high_level", flight: 4);
        after.Perks.Add(new SelectedPerk("headquarters", 3));
        after.Flaws.Add(new SelectedFlaw("alter_ego"));
        after.Gear.Add(new SelectedGear("A borrowed van"));
        after.IsVillain = true;
        after.UnlimitedBudget = true;
        after.SelectedPackageId = "hero";

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        Assert.True(diff.Rows.Count > 6, $"only {diff.Rows.Count} rows to inspect");

        // **A property name that is also something's printed name is dropped, and the exclusion is
        // computed rather than listed.** `CharacterSheet.Headquarters` is a collection of bases;
        // "Headquarters" is also what `perks.json` calls the Perk that buys one, so a diff row
        // reading "Perk: Headquarters" is a correct lookup and not a leaked field name — and no
        // check on the string can tell the two apart. Asking the rules data which names collide
        // keeps this from becoming a hand-list somebody can widen to silence a real leak: a name
        // only comes off when the book really does print it.
        var printed = Rules.Perks.Select(x => x.Name)
            .Concat(Rules.Powers.Select(x => x.Name))
            .Concat(Rules.Abilities.Select(x => x.Name))
            .Concat(Rules.Talents.Select(x => x.Name))
            .Concat(Rules.Flaws.Select(x => x.Name))
            .Concat(Rules.Tiers.Select(x => x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fields = typeof(CharacterSheet)
            .GetProperties()
            .Select(p => p.Name)
            .Where(n => n.Length > 4)
            .Where(n => !printed.Contains(n))
            .ToList();

        Assert.True(fields.Count > 8, $"reflection found only {fields.Count} properties");

        // The control on that exclusion: it really does drop something, and it drops the one thing
        // it was written for. A filter that matched nothing would leave this test unchanged and
        // this paragraph explaining a step that had stopped happening.
        Assert.Contains("Headquarters", printed);
        Assert.DoesNotContain("Headquarters", fields);
        Assert.Contains("AbilityRanks", fields);

        // Every visible string in the diff, which is what a reader actually sees.
        var visible = string.Join(" ",
            diff.Rows.Select(r => $"{r.What} {r.Before} {r.After}"));

        foreach (var field in fields)
        {
            Assert.DoesNotContain(field, visible, StringComparison.OrdinalIgnoreCase);
        }

        // And no id from the rules data either — an id is not what a thing is called.
        // **Real ids from the rules data, and the fixture took two goes to get right.** The first
        // draft used `code_of_honor`, which is not a Flaw this book has — so the diff correctly
        // printed the id back and the failure was the test's. The second used `code`, which *is*
        // one, and whose printed name is "Code": the id and the name are the same word, so a
        // case-insensitive check cannot tell a leak from a correct lookup. `alter_ego` prints as
        // "Alter Ego", which can.
        //
        // **So the property is stated as the shape rather than as a list**: no id in this data is
        // written the way a reader would write it, and every one that differs from its name
        // differs by carrying an underscore. Both halves are asserted — the named ids, and the
        // general rule.
        // Only the ids whose printed name actually differs from them. `flight` and `might` are
        // written the same way the book writes the Power and the Ability, so a case-insensitive
        // check on those cannot tell a leak from a correct lookup — which is what the third
        // attempt at this fixture discovered. The general rule below is what covers the rest.
        foreach (var id in new[] { "high_level", "alter_ego" })
        {
            Assert.DoesNotContain(id, visible, StringComparison.OrdinalIgnoreCase);
        }

        // The general half, which catches an id this test does not name. The positive control on
        // it is above: the rows exist and carry real content.
        Assert.DoesNotContain("_", visible, StringComparison.Ordinal);
    }

    /// <summary>
    /// An id the rules data does not know is printed as the id, and that is the right fallback.
    ///
    /// <para><b>Deliberately not "Unnamed".</b> A payload can name a Power from a build these rules
    /// do not have — a fork with extra data, or a rules file rolled back — and a diff full of rows
    /// called "Unnamed" tells a GM nothing about what changed. The id is at least the thing itself.
    /// A real id never reaches a screen; see the test above, which checks against the data.</para>
    /// </summary>
    [Fact]
    public void AnIdTheRulesDoNotKnowIsPrintedAsItself()
    {
        var before = new CharacterSheet();
        var after = new CharacterSheet { SelectedTierId = "no_such_tier" };
        after.SelectedPowers.Add(new SelectedPower("no_such_power", 4));

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        Assert.True(diff.Ran);
        Assert.Equal("no_such_tier", Assert.Single(diff.Rows, r => r.What == "Tier").After);
        Assert.Contains(diff.Rows, r => r.What == "Power: no_such_power");
    }

    /// <summary>
    /// A rank stepped back to zero reads as removed, not as <c>0d</c>.
    ///
    /// <para>The editors leave a 0 behind when somebody steps a Trait down, so a row saying
    /// <c>removed Might 0d</c> would be the app reporting its own bookkeeping.</para>
    /// </summary>
    [Fact]
    public void ARankSteppedToZeroReadsAsRemoved()
    {
        var before = ASheet(might: 6);
        var after = ASheet(might: 6);
        after.AbilityRanks["agility"] = 0;

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        var row = Assert.Single(diff.Rows, r => r.What == "Agility");

        Assert.Equal(DiffKind.Removed, row.Kind);
        Assert.Equal("4d", row.Before);
        Assert.Null(row.After);
    }

    /// <summary>
    /// A reordered list is not a changed list — compared by key, never by position.
    /// </summary>
    [Fact]
    public void AReorderedListIsNotAChange()
    {
        var before = ASheet();
        before.SelectedPowers.Add(new SelectedPower("flight", 4));
        before.SelectedPowers.Add(new SelectedPower("armor", 6));

        var after = ASheet();
        after.SelectedPowers.Add(new SelectedPower("armor", 6));
        after.SelectedPowers.Add(new SelectedPower("flight", 4));

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        Assert.True(diff.Ran);
        Assert.Empty(diff.Rows);
    }

    /// <summary>
    /// The rows come out in the same order twice, so a GM reading a diff twice sees one diff.
    /// </summary>
    [Fact]
    public void TheRowsAreInAStableOrder()
    {
        var before = new CharacterSheet();
        var after = ASheet(flight: 4);
        after.Perks.Add(new SelectedPerk("headquarters", 3));

        var first = CampaignDiff.Between(before, after, Rules, Costs).Rows.Select(r => r.What);
        var again = CampaignDiff.Between(before, after, Rules, Costs).Rows.Select(r => r.What);

        Assert.Equal(first, again);
    }

    /// <summary>
    /// A snapshot the engine refuses to price gets no figure rather than a guessed one — the same
    /// rule the front door follows.
    /// </summary>
    [Fact]
    public void ASnapshotTheEngineWillNotPriceGetsNoFigure()
    {
        var after = ASheet();

        // A variable-cost Power with no variant chosen: a question the engine refuses rather than
        // guesses at. The control is the ordinary case below it.
        after.SelectedPowers.Add(new SelectedPower("omni_power", 6));

        var refused = CampaignDiff.Between(ASheet(), after, Rules, Costs);

        Assert.Null(refused.SpentAfter);
        Assert.True(refused.Ran, "a refused figure must not stop the comparison running");
        Assert.NotEmpty(refused.Rows);

        Assert.NotNull(CampaignDiff.Between(ASheet(), ASheet(flight: 4), Rules, Costs).SpentAfter);
    }

    /// <summary>
    /// Nothing in the diff changes either sheet. It is a report; the whole snapshot is accepted or
    /// rejected, and there is deliberately no way to apply one row.
    /// </summary>
    [Fact]
    public void TheDiffChangesNeitherSheet()
    {
        var before = ASheet(might: 6);
        var after = ASheet(might: 8, flight: 4);

        var beforeText = Render(before);
        var afterText = Render(after);

        Assert.True(beforeText.Length > 200, "the positive control: a real sheet was rendered");

        _ = CampaignDiff.Between(before, after, Rules, Costs);

        Assert.Equal(beforeText, Render(before));
        Assert.Equal(afterText, Render(after));

        // And there is no method that could apply one: a merge algorithm for characters is a
        // second engine, capable of producing a sheet neither person authored.
        Assert.DoesNotContain("Apply",
            typeof(CampaignDiff).GetMethods().Select(m => m.Name).ToList());
    }

    private static string Render(CharacterSheet sheet) =>
        CharacterSheetRenderer.RenderText(sheet, Rules, Costs, Derived,
            Validator.Validate(sheet), new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc));

    // ── The compare-and-swap, from the browser's side ───────────────────────────────────

    /// <summary>
    /// <b>Approving a stale snapshot is refused, and the refusal names the newer one.</b>
    ///
    /// <para>The GM reads snapshot A; the player resubmits B; the GM presses Approve. Without the
    /// version, B — which nobody has looked at — becomes the campaign's clone.</para>
    /// </summary>
    [Fact]
    public async Task ApprovingAStaleSnapshotIsRefusedAndTheNewerOneComesBack()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 6), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");

        var looking = await store.ReadAsync(membership);

        Assert.NotNull(looking);
        Assert.Equal(1, looking!.PendingVersion);
        Assert.Equal(6, looking.Pending!.AbilityRanks["might"]);

        // The player resubmits while the diff is on screen.
        server.SignedIn = ("u_player", "The Player");
        Assert.Equal(2, await store.SubmitAsync(membership, ASheet(might: 12), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");

        var refused = await store.ApproveAsync(membership, looking.PendingVersion);

        Assert.Equal(DecisionOutcome.Stale, refused.Outcome);
        Assert.Equal(2, refused.NewerVersion);
        Assert.NotNull(refused.Newer);
        Assert.Equal(12, refused.Newer!.AbilityRanks["might"]);

        // Nothing was approved — which is what makes this about a defect rather than a status code.
        Assert.Null((await store.ReadAsync(membership))!.Approved);

        // The positive control: the same decision at the version it names goes through, so "Stale"
        // is not what this store always answers.
        Assert.Equal(DecisionOutcome.Done,
            (await store.ApproveAsync(membership, refused.NewerVersion)).Outcome);

        Assert.Equal(12, (await store.ReadAsync(membership))!.Approved!.AbilityRanks["might"]);
    }

    /// <summary>
    /// Rejecting a stale snapshot is refused the same way, and the clone does not move.
    /// </summary>
    [Fact]
    public async Task RejectingAStaleSnapshotIsRefusedToo()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 6), SheetMode.Hero));
        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 12), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");

        var refused = await store.RejectAsync(membership, 1);

        Assert.Equal(DecisionOutcome.Stale, refused.Outcome);
        Assert.Equal(12, refused.Newer!.AbilityRanks["might"]);

        // Still waiting: a refused rejection must not have quietly dropped the submission.
        Assert.NotNull((await store.ReadAsync(membership))!.Pending);
        Assert.Equal(DecisionOutcome.Done, (await store.RejectAsync(membership, 2)).Outcome);
        Assert.Null((await store.ReadAsync(membership))!.Pending);
    }

    /// <summary>
    /// Deciding when nothing is waiting says so, and the clone survives the second click.
    /// </summary>
    [Fact]
    public async Task DecidingTwiceSaysNothingIsWaiting()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 6), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);

        var again = await store.ApproveAsync(membership, 1);

        Assert.Equal(DecisionOutcome.NothingWaiting, again.Outcome);
        Assert.Null(again.Newer);
        Assert.Equal(6, (await store.ReadAsync(membership))!.Approved!.AbilityRanks["might"]);
    }

    /// <summary>
    /// Rejecting leaves the campaign's clone exactly as it was — the whole of what rejecting means.
    /// </summary>
    [Fact]
    public async Task RejectingLeavesTheCloneWhereItWas()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 6), SheetMode.Hero));
        server.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);

        server.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 12), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.RejectAsync(membership, 2)).Outcome);

        var after = await store.ReadAsync(membership);

        Assert.Equal(6, after!.Approved!.AbilityRanks["might"]);
        Assert.Null(after.Pending);
    }

    // ── Joining ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Joining hands back the campaign, and <see cref="CampaignJoin.Apply"/> is what decides —
    /// inherit into an empty tier, and report a disagreement rather than repairing it.
    /// </summary>
    [Fact]
    public async Task JoiningInheritsIntoAnEmptyTierAndReportsADisagreement()
    {
        var (_, store, _) = await ATable();

        var inheriting = new CharacterSheet();
        var joined = await store.JoinAsync("AAAA1-BBBB1", "c_1111111111111111111111", "Fresh");

        // The join itself has to have worked, or both assertions below pass by doing nothing.
        Assert.NotNull(joined);
        Assert.Equal("Nightfall", joined!.Value.Campaign.Name);

        Assert.Equal(CampaignJoinOutcome.Inherited,
            CampaignJoin.Apply(inheriting, joined.Value.Campaign).Outcome);
        Assert.Equal("standard", inheriting.SelectedTierId);

        var disagreeing = new CharacterSheet { SelectedTierId = "iconic", Name = "Ninefold" };

        Assert.Equal(CampaignJoinOutcome.TierDisagrees,
            CampaignJoin.Apply(disagreeing, joined.Value.Campaign).Outcome);

        // Nothing written at all — not the tier, not the sandbox, not even the campaign id.
        Assert.Equal("iconic", disagreeing.SelectedTierId);
        Assert.False(disagreeing.UnlimitedBudget);
        Assert.Null(disagreeing.CampaignId);
    }

    /// <summary>
    /// Every way a join can be refused gets its own answer, because each is a different sentence
    /// to a reader — and a refusal that said nothing would read as a control that is not wired up.
    /// </summary>
    [Fact]
    public async Task EveryRefusedJoinIsToldApart()
    {
        var (server, store, _) = await ATable();

        // A code that is not a code.
        Assert.Null(await store.JoinAsync("nope", "c_1111111111111111111111", "Fresh"));
        Assert.Equal(JoinRefusal.NotACode, store.LastJoinRefusal);

        // Well formed, and no campaign is using it.
        Assert.Null(await store.JoinAsync("ZZZZZ-ZZZZZ", "c_1111111111111111111111", "Fresh"));
        Assert.Equal(JoinRefusal.NoSuchCampaign, store.LastJoinRefusal);

        // Too many tries.
        server.JoinIsRateLimited = true;
        Assert.Null(await store.JoinAsync("AAAA1-BBBB1", "c_1111111111111111111111", "Fresh"));
        Assert.Equal(JoinRefusal.TooManyTries, store.LastJoinRefusal);

        // Nothing reached at all.
        server.JoinIsRateLimited = false;
        server.Unreachable = true;
        Assert.Null(await store.JoinAsync("AAAA1-BBBB1", "c_1111111111111111111111", "Fresh"));
        Assert.Equal(JoinRefusal.Unreachable, store.LastJoinRefusal);

        // The positive control: with none of the above, a join works — so the four refusals are
        // being told apart rather than every call failing.
        server.Unreachable = false;
        Assert.NotNull(await store.JoinAsync("AAAA1-BBBB1", "c_1111111111111111111111", "Fresh"));
        Assert.Equal(JoinRefusal.None, store.LastJoinRefusal);
    }

    // ── The standing ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The standing follows the two slots and says which sheet counts at the table.
    /// </summary>
    [Fact]
    public async Task TheStandingFollowsTheTwoSlots()
    {
        var (server, store, membership) = await ATable();

        async Task<MembershipSummary> Mine()
        {
            server.SignedIn = ("u_player", "The Player");
            var listed = await store.MineAsync();

            Assert.NotNull(listed);
            return Assert.Single(listed!, m => m.Id == membership);
        }

        Assert.Equal(CampaignStanding.NotSubmitted, (await Mine()).Standing);
        Assert.Equal("Not submitted", Standings.Say(CampaignStanding.NotSubmitted));

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));
        Assert.Equal(CampaignStanding.ChangesPending, (await Mine()).Standing);

        server.SignedIn = ("u_gm", "The GM");
        Assert.Equal(DecisionOutcome.Done, (await store.ApproveAsync(membership, 1)).Outcome);

        var approved = await Mine();

        Assert.Equal(CampaignStanding.Approved, approved.Standing);
        Assert.Equal("Approved for Nightfall", Standings.Say(approved.Standing, "Nightfall"));
    }

    /// <summary>
    /// <b>A rejection is a standing of its own, and it is not derivable from the two slots.</b>
    ///
    /// <para>Rejecting clears the pending slot and leaves the clone, which is what rejecting
    /// <em>means</em> — so both booleans come back reading exactly as they did after the approval
    /// before it. This walks the sequence a player actually lives: send, approved, send again,
    /// turned down. Before <c>0007</c> the last two states were the same sentence, and there was
    /// no way to learn a decision had been made at all.</para>
    /// </summary>
    [Fact]
    public async Task ARejectionIsAStandingOfItsOwnAndNotDerivableFromTheSlots()
    {
        var (server, store, membership) = await ATable();

        async Task<MembershipSummary> Mine()
        {
            server.SignedIn = ("u_player", "The Player");
            var listed = await store.MineAsync();

            Assert.NotNull(listed);
            return Assert.Single(listed!, m => m.Id == membership);
        }

        async Task Decide(bool approve, int version)
        {
            server.SignedIn = ("u_player", "The Player");
            Assert.NotNull(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));

            server.SignedIn = ("u_gm", "The GM");

            var decided = approve
                ? await store.ApproveAsync(membership, version)
                : await store.RejectAsync(membership, version);

            Assert.Equal(DecisionOutcome.Done, decided.Outcome);
        }

        // The control: no decision is its own value, and it is not `Rejected` by default.
        Assert.Equal(MembershipDecision.None, (await Mine()).Decision);

        await Decide(approve: true, version: 1);

        var approved = await Mine();

        Assert.Equal(CampaignStanding.Approved, approved.Standing);

        await Decide(approve: false, version: 2);

        var turnedDown = await Mine();

        // **Every other field reads as it did a moment ago**, which is the whole difficulty.
        Assert.Equal(approved.HasApproved, turnedDown.HasApproved);
        Assert.Equal(approved.HasPending, turnedDown.HasPending);
        Assert.Equal(approved.ApprovedAt, turnedDown.ApprovedAt);

        Assert.Equal(CampaignStanding.ChangesTurnedDown, turnedDown.Standing);
        Assert.Equal("Changes turned down", Standings.Say(turnedDown.Standing));
        Assert.Equal("Changes turned down for Nightfall",
            Standings.Say(turnedDown.Standing, "Nightfall"));

        // A waiting snapshot shadows it without anything having to clear it, and the decision
        // itself is still the last one made.
        server.SignedIn = ("u_player", "The Player");
        Assert.NotNull(await store.SubmitAsync(membership, ASheet(might: 7), SheetMode.Hero));

        var resent = await Mine();

        Assert.Equal(CampaignStanding.ChangesPending, resent.Standing);
        Assert.Equal(MembershipDecision.Rejected, resent.Decision);
    }

    /// <summary>
    /// <b>A word this build does not know is no decision, not a rejection.</b>
    ///
    /// <para>A later version of the server could spell a third outcome. Defaulting an unknown one
    /// to the value that changes what a player is told would be this build inventing a decision
    /// nobody made — the rule <see cref="StoredCharacter"/> follows for an envelope it cannot
    /// open, applied to a word it cannot read.</para>
    /// </summary>
    [Fact]
    public async Task AnUnknownDecisionReadsAsNoneRatherThanAsARejection()
    {
        var (server, store, membership) = await ATable();

        server.DecisionOnTheWire = "escalated-to-the-table";
        server.SignedIn = ("u_player", "The Player");

        var listed = await store.MineAsync();

        Assert.NotNull(listed);

        var row = Assert.Single(listed!, m => m.Id == membership);

        Assert.Equal(MembershipDecision.None, row.Decision);
        Assert.Equal(CampaignStanding.NotSubmitted, row.Standing);

        // The positive control: a word it does know is read, so the assertion above is about the
        // word rather than about the field never arriving.
        server.DecisionOnTheWire = "rejected";

        var known = Assert.Single((await store.MineAsync())!, m => m.Id == membership);

        Assert.Equal(MembershipDecision.Rejected, known.Decision);
    }

    /// <summary>
    /// <b>"I could not find out" is not "in no campaign".</b>
    ///
    /// <para>Collapsing the two tells somebody their character is out of a game it is still in,
    /// which is the fault <c>SheetPage</c> already records for a character that could not be read.
    /// A store that cannot reach the server answers null, never an empty list.</para>
    /// </summary>
    [Fact]
    public async Task NotKnownIsNotTheSameAsNotInACampaign()
    {
        var server = new FakeApi { SignedIn = ("u_player", "The Player") };
        var store = StoreFor(server);

        // Signed in and genuinely in nothing: an empty list.
        Assert.Empty((await store.MineAsync())!);

        // Unreachable: null, which every screen draws as silence rather than as a standing.
        server.Unreachable = true;
        Assert.Null(await store.MineAsync());
        Assert.Null(await store.InboxAsync());

        // And a deploy without its server, which answers the app's own page with a 200 — a success
        // that is not proof of an answer.
        server.Unreachable = false;
        server.ServerNotDeployed = true;
        Assert.Null(await store.MineAsync());

        Assert.Equal("Standing not known", Standings.Say(CampaignStanding.Unknown));
        Assert.Equal("", Standings.Say(CampaignStanding.NotInACampaign));
    }

    /// <summary>
    /// A GM is never told which account a submission came from, nor which of their characters it
    /// is — the player's own id is not the GM's business.
    /// </summary>
    [Fact]
    public async Task TheGmLearnsNothingAboutThePlayersAccount()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));

        server.SignedIn = ("u_gm", "The GM");

        var inbox = await store.InboxAsync();
        var detail = await store.ReadAsync(membership);

        Assert.NotNull(inbox);
        Assert.Single(inbox!);
        Assert.NotNull(detail);

        Assert.True(detail!.IsGm);
        Assert.Null(detail.CharacterId);
        Assert.All(inbox!, row => Assert.Null(row.CharacterId));

        // The player's own read is the mirror of it: their id, and not the GM's role.
        server.SignedIn = ("u_player", "The Player");

        var mine = await store.ReadAsync(membership);

        Assert.False(mine!.IsGm);
        Assert.Equal(PlayerCharacter, mine.CharacterId);
    }

    /// <summary>
    /// A third account reaches nothing — by id, by list, or by decision.
    /// </summary>
    [Fact]
    public async Task AThirdAccountReachesNothing()
    {
        var (server, store, membership) = await ATable();

        Assert.NotNull(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));

        server.SignedIn = ("u_stranger", "Somebody Else");

        Assert.Null(await store.ReadAsync(membership));
        Assert.Empty((await store.MineAsync())!);
        Assert.Empty((await store.InboxAsync())!);
        Assert.Equal(DecisionOutcome.Unreachable, (await store.ApproveAsync(membership, 1)).Outcome);
        Assert.Null(await store.SubmitAsync(membership, ASheet(), SheetMode.Hero));

        // The positive control: the two accounts that *are* on the row still reach it.
        server.SignedIn = ("u_gm", "The GM");
        Assert.NotNull(await store.ReadAsync(membership));
    }

    /// <summary>
    /// The join code can be replaced, and the new one is what comes back.
    /// </summary>
    [Fact]
    public async Task ANewCodeReplacesTheOldOne()
    {
        var (server, store, _) = await ATable();

        server.SignedIn = ("u_gm", "The GM");

        var fresh = await store.NewCodeAsync("g_0000000000000000000000");

        Assert.False(string.IsNullOrWhiteSpace(fresh));
        Assert.NotEqual("AAAA1-BBBB1", fresh);

        // The old code stops working; the new one does not.
        server.SignedIn = ("u_player", "The Player");
        Assert.Null(await store.JoinAsync("AAAA1-BBBB1", "c_2222222222222222222222", "Fresh"));
        Assert.NotNull(await store.JoinAsync(fresh!, "c_2222222222222222222222", "Fresh"));
    }

    // ── What the screens are held to ────────────────────────────────────────────────────

    /// <summary>
    /// <b>Neither campaign screen prints a field name from a stored payload.</b>
    ///
    /// <para>The same rule the diff is held to, one level up — on the <em>prose</em> of the two
    /// pages, so a screen cannot leak a spelling the diff would have refused. Read from the source
    /// with the comments and the code block stripped, exactly as <c>WebPresentationTests</c> reads
    /// visible text.</para>
    /// </summary>
    [Theory]
    [InlineData("Campaigns.razor")]
    [InlineData("CampaignApproval.razor")]
    public void NeitherCampaignScreenNamesAStoredField(string page)
    {
        var path = Path.Combine(FindRepoRoot(), "web", "Pages", page);
        var prose = VisibleText(File.ReadAllText(path));

        // The positive control: something survived the stripping. A stripper that ate the file
        // would satisfy every absence below.
        Assert.True(prose.Length > 100, $"{page} stripped to {prose.Length} characters of prose");

        foreach (var field in new[]
                 {
                     "SelectedTierId", "SelectedPackageId", "AbilityRanks", "TalentRanks",
                     "SelectedPowers", "UnlimitedBudget", "IsVillain", "CampaignId",
                     "pendingVersion", "approvedPayload", "pendingPayload", "campaign_members",
                 })
        {
            Assert.DoesNotContain(field, prose, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A page's visible prose: comments gone, the <c>@code</c> block gone, tags and Razor
    /// expressions gone. The same derivation <c>WebPresentationTests.VisibleText</c> uses, spelled
    /// here because that one is in the other test project.
    /// </summary>
    private static string VisibleText(string razor)
    {
        var withoutComments = ScanRegex.Build(@"@\*.*?\*@", RegexOptions.Singleline)
            .Replace(razor, " ");

        var block = ScanRegex.Build(@"^[ \t]*@code\s*\{", RegexOptions.Multiline)
            .Match(withoutComments);

        var markup = block.Success ? withoutComments[..block.Index] : withoutComments;

        markup = ScanRegex.Build("<[^>]*>").Replace(markup, " ");
        markup = ScanRegex.Build(@"@\([^()]*(\([^()]*\))?[^()]*\)").Replace(markup, " ");
        markup = ScanRegex.Build(@"@\w+(\.\w+)*(\([^()]*\))?").Replace(markup, " ");

        return markup;
    }
    // ── The screens, rendered ────────────────────────────────────────────────────────────
    //
    // **A source-reading test cannot see a bug in rendered output**, which is the whole reason
    // this project has two test suites — and the version check is the one thing in this slice
    // whose failure is a defect rather than a feature that does not work. The four cases below
    // render the real page and press the real buttons.

    /// <summary>
    /// A GM's context, with a campaign and one player's snapshot waiting in it.
    ///
    /// <para>Built through the store rather than by poking the stub's dictionaries, so a test about
    /// the screen cannot pass against a join or a submission that has stopped working.</para>
    /// </summary>
    private static async Task<(RenderContext Ctx, string Membership)> AWaitingRequest(
        CharacterSheet? submitted = null)
    {
        var joined = await AJoinedMember();

        joined.Ctx.Api.SignedIn = ("u_player", "The Player");

        Assert.NotNull(await joined.Ctx.Services.GetRequiredService<ApiMembershipStore>()
            .SubmitAsync(joined.Membership, submitted ?? ASheet(might: 6), SheetMode.Hero));

        joined.Ctx.Api.SignedIn = ("u_gm", "The GM");

        return joined;
    }

    /// <summary>
    /// A GM's context with one player joined and <b>nothing sent</b> — the state that used to open
    /// a blank panel, and the state <see cref="AWaitingRequest"/> is one submission past.
    ///
    /// <para>Driven through the real join route for the reason that helper records: a test about
    /// what the screen shows must not pass against a join that has stopped working.</para>
    /// </summary>
    private static async Task<(RenderContext Ctx, string Membership)> AJoinedMember()
    {
        var ctx = new RenderContext();

        ctx.Api.SignedIn = ("u_gm", "The GM");

        var code = ctx.Api.Campaign(
            "g_0000000000000000000000", "Nightfall",
            StoredCampaign.Write(
                new Campaign("g_0000000000000000000000", "Nightfall", "standard", 8, false)));

        ctx.Api.SignedIn = ("u_player", "The Player");

        var joined = await ctx.Services.GetRequiredService<ApiMembershipStore>()
            .JoinAsync(code, PlayerCharacter, "Ninefold");

        Assert.NotNull(joined);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        return (ctx, joined!.Value.Id);
    }

    /// <summary>
    /// Put a payload this build cannot open into the pending slot, through the wire.
    ///
    /// <para><b>Through the wire because the store is what refuses to write one.</b> An envelope
    /// from a later version of the app is an ordinary thing for
    /// <see cref="StoredCharacter.Read"/> to meet, by that class's own notes — and it is the only
    /// way to reach the two arms of this screen that must not be drawn as emptiness.</para>
    /// </summary>
    private static async Task SendAnUnreadableSnapshot(RenderContext ctx, string membership)
    {
        var http = ctx.Services.GetRequiredService<HttpClient>();
        var was = ctx.Api.SignedIn;

        ctx.Api.SignedIn = ("u_player", "The Player");

        using var sending = new StringContent(
            """{"label":"Ninefold","payload":"{\"Version\":9999,\"Sheet\":null}"}""",
            System.Text.Encoding.UTF8, "application/json");

        Assert.True(
            (await http.PutAsync($"/api/memberships/{membership}/submission", sending,
                Xunit.TestContext.Current.CancellationToken)).IsSuccessStatusCode,
            "the unreadable snapshot never landed, so nothing under test is reached");

        ctx.Api.SignedIn = was;
    }

    /// <summary>
    /// <b>The approval screen draws the diff, and the diff says how much it compared.</b>
    ///
    /// <para>The rendered page rather than the service, because a figure that is right in a record
    /// and missing from the markup is a screen that tells a GM nothing — which is the split the two
    /// test projects exist for.</para>
    /// </summary>
    [Fact]
    public async Task TheApprovalScreenDrawsTheDiffAndSaysHowMuchItCompared()
    {
        await using var ctx = (await AWaitingRequest()).Ctx;

        var page = ctx.Render<CampaignApproval>(
            p => p.Add(c => c.Id, "g_0000000000000000000000"));

        // The row is there before anything is clicked, which is the control on the read itself.
        Assert.Contains("Ninefold", page.Markup, StringComparison.Ordinal);

        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var diff = page.Find(".campaign-diff").TextContent;

        // The spend leads it, and it is the engine's figure for the snapshot.
        Assert.Contains("Hero Points", diff, StringComparison.Ordinal);

        // **The positive control, on the screen.** A diff showing nothing and a diff that failed
        // to run look identical to a reader, so the page prints how much was examined.
        Assert.Contains("fields compared", diff, StringComparison.Ordinal);
        Assert.Matches(@"\d+ fields compared", diff);

        // And the rows are in the book's words, not in a payload's.
        Assert.Contains("Tier", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedTierId", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("_", diff, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Approving a snapshot that changed while the GM was reading it is refused, and the screen
    /// says so.</b>
    ///
    /// <para><b>This is the one case in the slice where a bug costs somebody's decision.</b> The GM
    /// opens the diff; the player resubmits; the GM presses Approve. If the page sent the
    /// <em>current</em> version rather than the one it drew, the second snapshot — which nobody has
    /// looked at — would become the campaign's clone, silently.</para>
    ///
    /// <para><b>The resubmission lands after the diff is drawn and before the click</b>, which is
    /// the real sequence and — this took a mutation to establish — the only one that
    /// discriminates. A first version of this test put the resubmission between the click and the
    /// request, through a seam on the stub; the mutation it exists to catch (read the current
    /// version, approve that) sailed through, because its extra read happens <em>before</em> that
    /// seam fires and so still sees the old version. A seam in the wrong place is a test that
    /// races nothing. Proved by mutation, twice.</para>
    /// </summary>
    [Fact]
    public async Task ApprovingWhatChangedWhileItWasOnScreenIsRefusedByThePage()
    {
        var opened = await AWaitingRequest();
        await using var ctx = opened.Ctx;
        var membership = opened.Membership;

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();

        var page = ctx.Render<CampaignApproval>(
            p => p.Add(c => c.Id, "g_0000000000000000000000"));

        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        // What the GM is looking at, asserted before the race — so a page that had drawn the wrong
        // snapshot all along would fail here rather than at the end.
        Assert.Contains("6d", page.Find(".campaign-diff").TextContent, StringComparison.Ordinal);

        // The player resubmits while the diff sits on screen. The page is not told and must not
        // ask: the version it drew is the version it decides about.
        ctx.Api.SignedIn = ("u_player", "The Player");
        Assert.Equal(2, await store.SubmitAsync(membership, ASheet(might: 12), SheetMode.Hero));
        ctx.Api.SignedIn = ("u_gm", "The GM");

        // The control on the race: the screen still shows the snapshot it drew, unchanged.
        Assert.Contains("6d", page.Find(".campaign-diff").TextContent, StringComparison.Ordinal);

        await page.FindAll(".campaign-diff .btn")
            .First(b => b.TextContent.Contains("Approve", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // The screen says it changed…
        Assert.Contains("changed while you were reading it", page.Markup, StringComparison.Ordinal);

        // …and, the assertion that makes this about a defect: the unseen snapshot is NOT the clone.
        ctx.Api.SignedIn = ("u_gm", "The GM");
        var after = await store.ReadAsync(membership);

        Assert.Null(after!.Approved);
        Assert.Equal(12, after.Pending!.AbilityRanks["might"]);

        // The positive control: pressing Approve again, on the version now drawn, does land — so
        // "refused" is not what this page always does.
        await page.FindAll(".campaign-diff .btn")
            .First(b => b.TextContent.Contains("Approve", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        Assert.Equal(12, (await store.ReadAsync(membership))!.Approved!.AbilityRanks["might"]);

        // **And the GM is told, which is not the same assertion and used not to hold.** Approving
        // closes the open request, and the status line lived *inside* the open request — so the
        // sentence was assigned and thrown away unrendered, and the panel simply collapsed. Only
        // the stale branch keeps the request open, which is exactly why the check above this one
        // could pass while every other message on this screen was invisible.
        Assert.Contains("Approved", page.Markup, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A snapshot this build cannot open is not "nothing waiting", and the screen used to say
    /// it was.</b>
    ///
    /// <para><c>DecideAsync</c> reads the newer payload through <see cref="StoredCharacter"/>, and
    /// that read returns null for an envelope a later version of the app wrote — which the store's
    /// own notes treat as an ordinary thing to meet, not an error. The outcome is
    /// <c>Stale</c> with no <c>Newer</c> to draw, and the page's switch had no arm for it: it fell
    /// through to <c>NothingWaiting</c> and told the GM <em>there is nothing waiting here now</em>
    /// about a request that is sitting in the queue, with the player waiting on it.</para>
    ///
    /// <para><b>Reachable only since <see cref="FakeApi"/> stopped being more permissive than the
    /// server</b>, which is the finding underneath this one — see the deleted-campaign case
    /// below.</para>
    /// </summary>
    [Fact]
    public async Task AStaleSnapshotThatCannotBeOpenedIsNotCalledNothingWaiting()
    {
        var opened = await AWaitingRequest();
        await using var ctx = opened.Ctx;
        var membership = opened.Membership;

        var page = ctx.Render<CampaignApproval>(
            p => p.Add(c => c.Id, "g_0000000000000000000000"));

        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        // The control: the diff drew, so what follows is about the decision rather than the read.
        Assert.Contains("Hero Points", page.Find(".campaign-diff").TextContent, StringComparison.Ordinal);

        // The player resubmits an envelope this build does not understand. Sent through the wire
        // rather than through the store, because the store is what refuses to write one.
        var http = ctx.Services.GetRequiredService<HttpClient>();

        ctx.Api.SignedIn = ("u_player", "The Player");

        using var sending = new StringContent(
            """{"label":"Ninefold","payload":"{\"Version\":9999,\"Sheet\":null}"}""",
            System.Text.Encoding.UTF8, "application/json");

        Assert.True((await http.PutAsync($"/api/memberships/{membership}/submission", sending,
            Xunit.TestContext.Current.CancellationToken)).IsSuccessStatusCode,
            "the resubmission never landed, so nothing is stale");

        ctx.Api.SignedIn = ("u_gm", "The GM");

        await page.FindAll(".campaign-diff .btn")
            .First(b => b.TextContent.Contains("Approve", StringComparison.Ordinal))
            .ClickAsync(new MouseEventArgs());

        // The sentence a GM reads has to be true of the queue they are looking at.
        Assert.DoesNotContain("nothing waiting here now", page.Markup, StringComparison.Ordinal);
        Assert.Contains("changed while you were reading it", page.Markup, StringComparison.Ordinal);

        // And the decision did not land on the unreadable snapshot either.
        var after = await ctx.Services.GetRequiredService<ApiMembershipStore>().ReadAsync(membership);

        Assert.Null(after!.Approved);
    }

    /// <summary>
    /// <b>A GM who deleted the game reaches nothing of it, on the screen and not only in the
    /// store.</b>
    ///
    /// <para><b>This could not be written until <see cref="FakeApi"/> was fixed</b>, and that is
    /// the finding. The server gates the GM's half of every membership on the campaign still
    /// existing — <c>EXISTS (SELECT 1 FROM campaigns c WHERE c.user_id = gm_user_id AND c.id =
    /// campaign_id)</c>, on the GM's side alone, so the player keeps their row. The fake checked
    /// nothing, so it kept answering 200 with the clone and the waiting snapshot, and every
    /// screen's handling of a deleted game was untestable while looking tested.</para>
    ///
    /// <para>A fake that is more permissive than the server does not make tests fail; it makes
    /// them pass about a state the server never produces.</para>
    /// </summary>
    [Fact]
    public async Task AGmWhoDeletedTheGameReachesNothingOfItOnTheScreen()
    {
        var opened = await AWaitingRequest();
        await using var ctx = opened.Ctx;
        var membership = opened.Membership;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        // The positive control: it is in the inbox to lose.
        Assert.Single(await memberships.InboxAsync() ?? []);

        await ctx.Services.GetRequiredService<ApiCampaignStore>()
            .DeleteAsync("g_0000000000000000000000");

        // Read out rather than coalesced: a null inbox means the read FAILED, and `?? []` would
        // let that pass as "the GM sees nothing", which is the same sentence for a different
        // reason. The page itself keeps the old list on a null for exactly this distinction.
        var emptied = await memberships.InboxAsync();

        Assert.NotNull(emptied);
        Assert.Empty(emptied);
        Assert.Null(await memberships.ReadAsync(membership));

        var page = ctx.Render<CampaignApproval>(
            p => p.Add(c => c.Id, "g_0000000000000000000000"));

        // Nothing of the player's character survives on the GM's screen — not the label, and not
        // an Approve button under a queue that no longer means anything.
        Assert.DoesNotContain("Ninefold", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Approve", page.Markup, StringComparison.Ordinal);

        // The player's half is theirs and is untouched, which is the asymmetry the server chose so
        // that writing the campaign back is a complete undo.
        ctx.Api.SignedIn = ("u_player", "The Player");

        Assert.Single(await memberships.MineAsync() ?? []);
        Assert.NotNull(await memberships.ReadAsync(membership));

        // And submitting into it is refused rather than accepted into nowhere.
        Assert.Null(await memberships.SubmitAsync(membership, ASheet(might: 7), SheetMode.Hero));
    }

    /// <summary>
    /// An account that runs no campaign at that address is told so, and learns nothing by asking.
    /// </summary>
    [Fact]
    public async Task ACampaignThatIsNotYoursSaysSoAndNothingMore()
    {
        await using var ctx = (await AWaitingRequest()).Ctx;

        ctx.Api.SignedIn = ("u_stranger", "Somebody Else");

        var page = ctx.Render<CampaignApproval>(
            p => p.Add(c => c.Id, "g_0000000000000000000000"));

        Assert.Contains("no game here for this account", page.Markup, StringComparison.Ordinal);

        // Nothing about the campaign or the character reaches the page.
        Assert.DoesNotContain("Nightfall", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Ninefold", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The campaign screen refuses an anonymous visitor before it offers a control.</b>
    ///
    /// <para>A campaign kept in one browser could never receive a submission, and its join code
    /// would be a code nobody could redeem — so the page says so first rather than offering a
    /// button that does nothing, which is indistinguishable from one that is not wired up.</para>
    /// </summary>
    [Fact]
    public void TheCampaignScreenRefusesAnAnonymousVisitorFirst()
    {
        using var ctx = new RenderContext();

        var page = ctx.Render<Campaigns>();

        Assert.Contains("Sign in", page.Markup, StringComparison.Ordinal);

        // No control that would do nothing.
        Assert.DoesNotContain("Name a campaign", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Join", page.Markup, StringComparison.Ordinal);

        // The positive control: signed in, the page really does offer them.
        using var signedIn = new RenderContext();
        signedIn.Api.SignedIn = ("u_gm", "The GM");

        var offered = signedIn.Render<Campaigns>().Markup;

        Assert.Contains("Name a campaign", offered, StringComparison.Ordinal);
        Assert.Contains("Join", offered, StringComparison.Ordinal);
    }

    // ── What the GM can read when nothing is waiting ─────────────────────────────────────
    //
    // **The panel was gated on a diff existing, so a member with no snapshot opened nothing at
    // all** — a character approved in March was unreadable on this screen until somebody changed
    // it, and `Look` had two bare `return`s that left the box blank for an unreachable server and
    // for an empty slot alike. The five cases below are five answers that used to be one blank
    // box, and each asserts the sentence it must NOT be drawn as.

    /// <summary>
    /// <b>A settled member's sheet is readable with nothing waiting, and the control says which
    /// of the two things pressing it will do.</b>
    ///
    /// <para><c>article.sheet</c> is <c>SheetView</c>'s own element and is found rather
    /// than searched for, because a panel that opened and drew nothing would satisfy every other
    /// assertion here.</para>
    /// </summary>
    [Fact]
    public async Task TheGmCanReadTheSheetOfAMemberWithNothingWaiting()
    {
        var settled = await AWaitingRequest();
        await using var ctx = settled.Ctx;

        var store = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var waiting = (await store.InboxAsync())!.Single();

        Assert.Equal(
            DecisionOutcome.Done,
            (await store.ApproveAsync(settled.Membership, waiting.PendingVersion)).Outcome);

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        var open = page.Find(".campaign-row .btn");

        Assert.Equal("View sheet", open.TextContent.Trim());

        await open.ClickAsync(new MouseEventArgs());

        // The positive control, and it is most of this test: the sheet really drew.
        var drawn = page.Find(".campaign-diff article.sheet").TextContent;

        Assert.Contains("Ninefold", drawn, StringComparison.Ordinal);

        // Nothing is being decided, so nothing offers to decide it. Asserted over the controls
        // rather than over the markup, because the row's own standing says the word "Approved".
        Assert.Empty(page.FindAll(".campaign-diff .btn"));
    }

    /// <summary>
    /// <b>A member who has sent nothing is told exactly that</b>, rather than shown a panel that
    /// opened and drew nothing — which is what a reader cannot tell from a failure.
    /// </summary>
    [Fact]
    public async Task AMemberWhoHasSentNothingSaysSoRatherThanOpeningABlankPanel()
    {
        var joined = await AJoinedMember();
        await using var ctx = joined.Ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        var open = page.Find(".campaign-row .btn");

        Assert.Equal("View sheet", open.TextContent.Trim());

        await open.ClickAsync(new MouseEventArgs());

        var said = page.Find(".campaign-diff").TextContent;

        Assert.Contains("holds no sheet", said, StringComparison.Ordinal);

        // Not the sentence for a payload that failed to open, which is a different fact.
        Assert.DoesNotContain("cannot open", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A clone this build cannot open is not called "no sheet".</b>
    ///
    /// <para>The same class of fault as the stale snapshot above, on the read path rather than the
    /// decision path: both slots answer <c>null</c> for an envelope
    /// <see cref="StoredCharacter.Read"/> declines, and the list row is the only thing that says
    /// whether the server thinks there is anything there.</para>
    /// </summary>
    [Fact]
    public async Task ACloneThisBuildCannotOpenIsNotCalledNoSheet()
    {
        var joined = await AJoinedMember();
        await using var ctx = joined.Ctx;

        await SendAnUnreadableSnapshot(ctx, joined.Membership);

        // Decided through the store, because the screen deliberately offers no way to decide
        // about a snapshot it could not draw — which is the case below this one.
        Assert.Equal(
            DecisionOutcome.Done,
            (await ctx.Services.GetRequiredService<ApiMembershipStore>()
                .ApproveAsync(joined.Membership, 1)).Outcome);

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        await page.Find(".campaign-row .btn").ClickAsync(new MouseEventArgs());

        var said = page.Find(".campaign-diff").TextContent;

        Assert.Contains("cannot open", said, StringComparison.Ordinal);
        Assert.DoesNotContain("holds no sheet", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A waiting snapshot this build cannot open is not called "no sheet" either</b>, and it
    /// offers no decision — a GM must not approve what this build could not draw.
    /// </summary>
    [Fact]
    public async Task AWaitingSnapshotThisBuildCannotOpenOffersNoDecision()
    {
        var joined = await AJoinedMember();
        await using var ctx = joined.Ctx;

        await SendAnUnreadableSnapshot(ctx, joined.Membership);

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        var open = page.Find(".campaign-row .btn");

        // The row knows a snapshot is waiting even though nothing here can open it.
        Assert.Equal("Read the changes", open.TextContent.Trim());

        await open.ClickAsync(new MouseEventArgs());

        var said = page.Find(".campaign-diff").TextContent;

        Assert.Contains("Changes are waiting here", said, StringComparison.Ordinal);
        Assert.DoesNotContain("holds no sheet", said, StringComparison.Ordinal);

        // And nothing invites a decision about it.
        Assert.Empty(page.FindAll(".campaign-diff .btn"));
    }

    // ── Ending a membership, from both screens ──────────────────────────────────────────
    //
    // **Both controls ask twice**, because the row goes and the campaign's copy goes with it and
    // there is no undo behind either. Every case below asserts the first press did NOT end it,
    // which is the only assertion that can tell a confirm from a control that happens to be
    // labelled like one.

    private static IElement ByLabel(IRenderedComponent<IComponent> page, string label) =>
        page.FindAll("button").First(b =>
            string.Equals(b.TextContent.Trim(), label, StringComparison.Ordinal));

    /// <summary>
    /// <b>A player leaves, and the campaign stops holding their character.</b>
    /// </summary>
    [Fact]
    public async Task APlayerCanLeaveAndTheCampaignStopsHoldingThem()
    {
        var settled = await AWaitingRequest();
        await using var ctx = settled.Ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        // The GM approves first, so what is being destroyed is a clone somebody accepted rather
        // than an empty row — which is the case worth being sure about.
        var waiting = (await memberships.InboxAsync())!.Single();

        Assert.Equal(
            DecisionOutcome.Done,
            (await memberships.ApproveAsync(settled.Membership, waiting.PendingVersion)).Outcome);

        ctx.Api.SignedIn = ("u_player", "The Player");

        var page = ctx.Render<Campaigns>();

        // The control: the row is on screen to lose.
        Assert.Contains("Ninefold", page.Markup, StringComparison.Ordinal);

        await ByLabel(page, "Leave").ClickAsync(new MouseEventArgs());

        // **One press does not end it.** Without this the confirm is decoration.
        var afterOnePress = await memberships.MineAsync();

        Assert.NotNull(afterOnePress);
        Assert.Single(afterOnePress);

        await ByLabel(page, "Leave for good").ClickAsync(new MouseEventArgs());

        var afterTwo = await memberships.MineAsync();

        Assert.NotNull(afterTwo);
        Assert.Empty(afterTwo);

        // Read out rather than coalesced, for the reason the deleted-campaign case records: a
        // null list means the read failed, which is a different fact from an empty one.
        Assert.DoesNotContain("Ninefold", page.Markup, StringComparison.Ordinal);
        Assert.Contains("You have left that game", page.Markup, StringComparison.Ordinal);

        // The GM's half goes with it — there is no row left to hold the clone they accepted.
        ctx.Api.SignedIn = ("u_gm", "The GM");

        var inbox = await memberships.InboxAsync();

        Assert.NotNull(inbox);
        Assert.Empty(inbox);
    }

    /// <summary>
    /// <b>A GM removes somebody, reaching the same row by the other owner column.</b>
    /// </summary>
    [Fact]
    public async Task AGmCanRemoveAPlayerFromTheApprovalScreen()
    {
        var settled = await AWaitingRequest();
        await using var ctx = settled.Ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        Assert.Contains("Ninefold", page.Markup, StringComparison.Ordinal);

        await ByLabel(page, "Remove").ClickAsync(new MouseEventArgs());

        var afterOnePress = await memberships.InboxAsync();

        Assert.NotNull(afterOnePress);
        Assert.Single(afterOnePress);

        await ByLabel(page, "Remove for good").ClickAsync(new MouseEventArgs());

        var afterTwo = await memberships.InboxAsync();

        Assert.NotNull(afterTwo);
        Assert.Empty(afterTwo);

        Assert.Contains("no longer holds that character", page.Markup, StringComparison.Ordinal);

        // The player's half goes too. Unlike a deleted campaign — which keeps every membership so
        // that writing it back is a complete undo — this ends the membership for both of them.
        ctx.Api.SignedIn = ("u_player", "The Player");

        var mine = await memberships.MineAsync();

        Assert.NotNull(mine);
        Assert.Empty(mine);
    }

    /// <summary>
    /// <b>A removal from a game the GM has deleted does not happen, and is not reported as
    /// having happened.</b>
    ///
    /// <para>The player's row deliberately outlives a deleted campaign so that writing the
    /// campaign back is a complete undo, and <c>removeMember</c>'s <c>EXISTS</c> is what keeps
    /// that whole — the server answers 409 rather than a 204 that would report a removal that did
    /// not occur.</para>
    ///
    /// <para><b>What the GM sees is the page having moved on</b>, not a sentence about the
    /// refusal: reaching that 409 means having deleted the game, so by the time there is anything
    /// to say the panel it would be said in is no longer drawn. A message composed for that state
    /// would be thrown away unrendered, which is the fault finding 7 records about this screen —
    /// so this asserts the page is right rather than that it apologises.</para>
    /// </summary>
    [Fact]
    public async Task ARemovalFromADeletedGameDoesNotHappenAndIsNotReportedAsHavingHappened()
    {
        var settled = await AWaitingRequest();
        await using var ctx = settled.Ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        await ByLabel(page, "Remove").ClickAsync(new MouseEventArgs());

        // Deleted between the confirm being offered and it being pressed — the other-tab case,
        // and the only way a GM reaches this refusal at all.
        await ctx.Services.GetRequiredService<ApiCampaignStore>()
            .DeleteAsync("g_0000000000000000000000");

        await ByLabel(page, "Remove for good").ClickAsync(new MouseEventArgs());

        // Nothing claims the removal happened.
        Assert.DoesNotContain("no longer holds that character", page.Markup, StringComparison.Ordinal);
        Assert.Contains("no game here for this account", page.Markup, StringComparison.Ordinal);

        // And the player still has their row, which is the whole reason that refusal exists.
        ctx.Api.SignedIn = ("u_player", "The Player");

        var mine = await memberships.MineAsync();

        Assert.NotNull(mine);
        Assert.Single(mine);

        // The store still models the server's answer, which is what the worker suite drives
        // against real SQLite — the value is not reachable on this screen, and it is real.
        ctx.Api.SignedIn = ("u_gm", "The GM");

        Assert.Equal(LeftOutcome.GameIsGone, await memberships.LeaveAsync(settled.Membership));
    }

    /// <summary>
    /// <b>A player's own screen says a change was turned down.</b>
    ///
    /// <para>The rendered page rather than the record, because a standing that is right in a
    /// record and absent from the markup is the state this whole slice existed to fix — and the
    /// two test projects are split for exactly that reason.</para>
    /// </summary>
    [Fact]
    public async Task ThePlayersScreenSaysAChangeWasTurnedDown()
    {
        var waiting = await AWaitingRequest();
        await using var ctx = waiting.Ctx;

        var memberships = ctx.Services.GetRequiredService<ApiMembershipStore>();
        var pending = (await memberships.InboxAsync())!.Single();

        // The control: before the decision the player is told a change is pending, so what
        // changes below is the decision rather than the row appearing at all.
        ctx.Api.SignedIn = ("u_player", "The Player");

        Assert.Contains("Changes pending", ctx.Render<Campaigns>().Markup, StringComparison.Ordinal);

        ctx.Api.SignedIn = ("u_gm", "The GM");

        Assert.Equal(
            DecisionOutcome.Done,
            (await memberships.RejectAsync(waiting.Membership, pending.PendingVersion)).Outcome);

        ctx.Api.SignedIn = ("u_player", "The Player");

        var after = ctx.Render<Campaigns>().Markup;

        Assert.Contains("Changes turned down", after, StringComparison.Ordinal);

        // And not the two sentences it used to revert to, which are what made a rejection silent.
        Assert.DoesNotContain("Not submitted", after, StringComparison.Ordinal);
        Assert.DoesNotContain("Changes pending", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Cancelling a confirm ends nothing and puts the control back.</b>
    /// </summary>
    [Fact]
    public async Task CancellingAConfirmEndsNothing()
    {
        var joined = await AJoinedMember();
        await using var ctx = joined.Ctx;

        ctx.Api.SignedIn = ("u_player", "The Player");

        var page = ctx.Render<Campaigns>();

        await ByLabel(page, "Leave").ClickAsync(new MouseEventArgs());
        await ByLabel(page, "Cancel").ClickAsync(new MouseEventArgs());

        var still = await ctx.Services.GetRequiredService<ApiMembershipStore>().MineAsync();

        Assert.NotNull(still);
        Assert.Single(still);

        // The plain control is back, and the confirm is not still sitting open.
        Assert.Contains(page.FindAll("button"),
            b => string.Equals(b.TextContent.Trim(), "Leave", StringComparison.Ordinal));

        Assert.DoesNotContain(page.FindAll("button"),
            b => string.Equals(b.TextContent.Trim(), "Leave for good", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A read that fails says so, rather than reading as a member with nothing to show.</b>
    ///
    /// <para>Reached the way it happens: the list is drawn, the GM deletes the game in another
    /// tab, and the click that follows asks about a membership whose GM half no longer exists.
    /// Without this arm the panel opened blank, which is the same blank a member who has sent
    /// nothing produces — and those are not the same sentence.</para>
    /// </summary>
    [Fact]
    public async Task AReadThatFailsIsNotDrawnAsAMemberWithNothingToShow()
    {
        var settled = await AWaitingRequest();
        await using var ctx = settled.Ctx;

        var page = ctx.Render<CampaignApproval>(p => p.Add(c => c.Id, "g_0000000000000000000000"));

        // The control: the row is on screen and the read is about to be made from it.
        var open = page.Find(".campaign-row .btn");

        Assert.Equal("Read the changes", open.TextContent.Trim());

        await ctx.Services.GetRequiredService<ApiCampaignStore>()
            .DeleteAsync("g_0000000000000000000000");

        await open.ClickAsync(new MouseEventArgs());

        var said = page.Find(".campaign-diff").TextContent;

        Assert.Contains("could not be read", said, StringComparison.Ordinal);
        Assert.DoesNotContain("holds no sheet", said, StringComparison.Ordinal);
    }
}
