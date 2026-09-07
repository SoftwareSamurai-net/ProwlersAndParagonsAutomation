using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The switches a GM can turn on and the switches an encounter can read are the same list.</b>
///
/// <para><c>CampaignTable</c> is what a campaign stores and what a joining character carries;
/// <c>play/Encounter/TableRules.cs</c> is what the second engine actually reads when it resolves a
/// fight. They are two types on purpose — <c>engine/</c> may not reference <c>play/</c>, and the
/// arrows are guarded at the csproj and at the source — so nothing in the compiler holds them
/// together. This does.</para>
///
/// <para><b>The failure it exists for is a switch that only exists on one side.</b> Added to the
/// campaign alone, it is a toggle a GM turns on, sees on the campaign page, tells their table
/// about, and that no encounter will ever read: a setting that is a promise to somebody who can
/// never be told, which is the exact fault this repository keeps hitting. Added to
/// <c>TableRules</c> alone, it is a rule the simulator applies that no campaign can express, so
/// every measurement is of a game nobody chose.</para>
///
/// <para><b>It reads source text, which is the same instrument
/// <c>TraitCapReadTests</c> and <c>WebPresentationTests</c> use</b>, and for the same reason: the
/// fact being checked is a relationship between two projects that cannot see each other. Reading
/// <c>TableRules</c> by reflection would mean this test project referencing <c>play/</c> to
/// enforce that <c>engine/</c> does not, which reads as the guard undoing itself; the campaign
/// side <em>is</em> read by reflection, because this project does reference the engine and a real
/// type is a better witness than a second regular expression.</para>
///
/// <para><b>The positive control is that the scan finds a non-empty set.</b> A regular expression
/// that has stopped matching — a file moved, a property spelling changed, a record rewritten with
/// primary-constructor parameters — makes two empty sets, and two empty sets are equal. That is a
/// green test asserting nothing, which is three of this repository's four historical guard
/// faults.</para>
/// </summary>
public sealed class CampaignTableNamesTests
{
    /// <summary>The second engine's own list, which this one may not reference.</summary>
    private static string TableRulesSource => Path.Combine(
        RulesFixture.RepoRoot, "play", "Encounter", "TableRules.cs");

    /// <summary>
    /// A declared <c>bool</c> property, and nothing else. <c>int? GearLimitRank</c> is checked
    /// separately below because it is not a switch, and <c>IsOn</c>/<c>Switches</c> are methods
    /// and lists rather than settings.
    /// </summary>
    private static readonly Regex BoolProperty = new(
        @"^\s*public\s+bool\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*init;\s*\}",
        RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    private static SortedSet<string> PlaySideSwitches()
    {
        var source = File.ReadAllText(TableRulesSource);

        return [.. BoolProperty.Matches(source).Select(m => m.Groups["name"].Value)];
    }

    private static SortedSet<string> CampaignSideSwitches() =>
        [.. typeof(CampaignTable)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
            .Select(p => p.Name)];

    /// <summary>
    /// Every switch is on both sides, named the same, and there is at least one.
    /// </summary>
    [Fact]
    public void ACampaignsTableBlockNamesExactlyTheSwitchesAnEncounterReads()
    {
        var play = PlaySideSwitches();
        var campaign = CampaignSideSwitches();

        // The control. Both sides going empty together would satisfy the equality below while
        // proving nothing at all, and the likeliest way for that to happen is the regular
        // expression above quietly ceasing to match a rewritten `TableRules`.
        Assert.True(play.Count >= 10,
            $"Only {play.Count} bool properties were found in {TableRulesSource}. The ten Gritty "
            + "Combat Rules alone are that many, so the scan has stopped seeing them — check the "
            + "property spelling this test matches before believing the comparison below.");

        Assert.True(campaign.Count >= 10,
            $"Only {campaign.Count} settable bool properties were found on {nameof(CampaignTable)}.");

        var missingFromCampaign = play.Except(campaign).ToList();
        var missingFromPlay = campaign.Except(play).ToList();

        Assert.True(missingFromCampaign.Count == 0,
            "These table settings are read when a fight is resolved and no campaign can turn "
            + "them on, so nobody can ever choose them: "
            + string.Join(", ", missingFromCampaign)
            + $". Add each to {nameof(CampaignTable)} with the same name and the entry's page.");

        Assert.True(missingFromPlay.Count == 0,
            "A GM can turn these on and no encounter reads them, so the campaign page promises "
            + "something the simulator will never do: "
            + string.Join(", ", missingFromPlay)
            + ". Add each to play/Encounter/TableRules.cs, with the rulebook entry it names.");
    }

    /// <summary>
    /// <b>The Gear Limit rank is on both sides too, and it is not a switch.</b>
    ///
    /// <para>It is the one setting that is a number, and it is paired with its own boolean rather
    /// than replacing it, because <c>TableRules.GearLimit</c> reads it that way — a rank left
    /// behind while <c>RaisedGearLimit</c> is off is a figure the table has not adopted. Checked
    /// on its own because the scan above deliberately sees only booleans, and a number that
    /// existed on one side alone would slip straight through it.</para>
    /// </summary>
    [Fact]
    public void TheGearLimitRankIsOnBothSidesAndIsNullable()
    {
        var source = File.ReadAllText(TableRulesSource);

        Assert.Contains("public int? GearLimitRank { get; init; }", source, StringComparison.Ordinal);

        var onCampaign = typeof(CampaignTable).GetProperty(nameof(CampaignTable.GearLimitRank));

        Assert.NotNull(onCampaign);
        Assert.Equal(typeof(int?), onCampaign.PropertyType);
    }

    /// <summary>
    /// A setting assigned from the same-named setting on the campaign's block, inside
    /// <c>TableRules.From</c> — the seam that turns what a campaign stored into what a fight is
    /// resolved under.
    /// </summary>
    private static readonly Regex CopiedFromCampaign = new(
        @"^\s*(?<to>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*campaign\.(?<from>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s*$",
        RegexOptions.Multiline, TimeSpan.FromSeconds(5));

    /// <summary>
    /// <b>Every switch a campaign stores is carried across the seam, and none is silently
    /// dropped.</b>
    ///
    /// <para>The test above holds the two <em>lists</em> together. This holds the
    /// <em>conversion</em> to them, which is a separate failure and a quieter one: a switch that
    /// exists on both sides and is left out of <c>TableRules.From</c> compiles, passes the test
    /// above, and is a house rule a GM turned on that every fight is resolved without. Nothing
    /// else would say so — the campaign page shows it on, the sheet carries it, and the run's own
    /// echo reports the switch the encounter was actually built with, which is off.</para>
    ///
    /// <para><b>Each pair has to be same-named on both sides</b>, so a copy that reached the wrong
    /// field — <c>ToughMinions = campaign.WoundPenalties</c> — is caught as well as a missing one.
    /// The <see cref="GearLimitRank"/> crosses under the same rule, because a raised limit that
    /// stayed behind is a table playing a game nobody chose.</para>
    ///
    /// <para><b>The positive control is the one this file already relies on twice</b>: the scan
    /// has to find a non-empty set. A regular expression that has stopped matching — the method
    /// renamed, the parameter renamed, the object initialiser rewritten as assignments — produces
    /// an empty set, and an empty set is missing nothing.</para>
    /// </summary>
    [Fact]
    public void EverySwitchACampaignStoresIsCarriedIntoTheOneAnEncounterIsBuiltWith()
    {
        var source = File.ReadAllText(TableRulesSource);

        var copied = CopiedFromCampaign.Matches(source).ToList();

        Assert.True(copied.Count >= 11,
            $"Only {copied.Count} settings are copied from a campaign's block in {TableRulesSource}. "
            + "The ten Gritty Combat Rules and the Gear Limit rank alone are eleven, so the scan has "
            + "stopped seeing them — check that TableRules.From is still an object initialiser "
            + "assigning from a parameter called 'campaign' before believing the comparison below.");

        var crossed = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var pair in copied)
        {
            var to = pair.Groups["to"].Value;
            var from = pair.Groups["from"].Value;

            Assert.True(string.Equals(to, from, StringComparison.Ordinal),
                $"TableRules.From assigns {to} from the campaign's {from}. A setting copied out of "
                + "its neighbour is a fight resolved under a rule the table never turned on, and it "
                + "compiles: both are booleans. Copy each from the setting of the same name.");

            crossed.Add(to);
        }

        var expected = CampaignSideSwitches();
        expected.Add(nameof(CampaignTable.GearLimitRank));

        var dropped = expected.Except(crossed).ToList();

        Assert.True(dropped.Count == 0,
            "A campaign can store these and TableRules.From leaves them behind, so a table that "
            + "turned them on has every fight resolved without them and nothing anywhere says so: "
            + string.Join(", ", dropped)
            + ". Copy each in play/Encounter/TableRules.cs, from the setting of the same name.");

        var invented = crossed.Except(expected).ToList();

        Assert.True(invented.Count == 0,
            "TableRules.From copies these and no campaign has them: "
            + string.Join(", ", invented) + ".");
    }

    /// <summary>
    /// <b>The engine still cannot see the second engine, which is what makes the source-reading
    /// above necessary rather than merely convenient.</b>
    ///
    /// <para>If <c>engine/</c> ever gained a reference to <c>play/</c>, the honest thing would be
    /// one type instead of two — and this test, which exists only because there are two, would be
    /// the wrong shape. So the assumption is asserted rather than assumed.</para>
    /// </summary>
    [Fact]
    public void TheEngineDoesNotReferenceTheSecondEngine()
    {
        var csproj = File.ReadAllText(Path.Combine(
            RulesFixture.RepoRoot, "engine", "ProwlersAndParagons.Engine.csproj"));

        Assert.DoesNotContain("ProwlersAndParagons.Play", csproj, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "ProwlersAndParagonsAutomation.Play",
            File.ReadAllText(Path.Combine(RulesFixture.RepoRoot, "engine", "CampaignTable.cs")),
            StringComparison.Ordinal);
    }
}
