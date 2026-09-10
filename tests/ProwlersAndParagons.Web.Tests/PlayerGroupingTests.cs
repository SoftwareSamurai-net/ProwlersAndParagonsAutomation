namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// <b>The reusable grouping behind item 33.12</b>: fold rows sharing a key into one group, order
/// them, and never fold two rows together that both answer null.
///
/// <para>This is the unit-level guard on <see cref="PlayerGrouping"/> itself; the campaign ledger's
/// own use of it is covered in <c>CampaignAssetLedgerTests</c>, and the rendered tree in
/// <c>CampaignSharedBooksTests</c>.</para>
/// </summary>
public sealed class PlayerGroupingTests
{
    private sealed record Row(string? Key, string Label, int Amount);

    private static IReadOnlyList<PlayerGroup<Row>> Group(params Row[] rows) =>
        PlayerGrouping.Group(rows, r => r.Key, r => r.Amount, r => r.Label);

    /// <summary>A lone row with no key groups with nobody and draws exactly as it is.</summary>
    [Fact]
    public void ARowWithNoKeyIsItsOwnGroup()
    {
        var groups = Group(new Row(null, "Bulwark", 2));

        var group = Assert.Single(groups);
        Assert.False(group.IsGrouped);
        Assert.Equal(2, group.Total);
        Assert.Equal(["Bulwark"], group.Members.Select(m => m.Label));
    }

    /// <summary>
    /// <b>Two rows with no key never group, even with each other.</b> This is what lets an older
    /// answer that has not learned to send a key — every row null — draw a flat list rather than
    /// folding unrelated rows under one shared "null" bucket.
    /// </summary>
    [Fact]
    public void TwoRowsWithNoKeyDoNotGroupWithEachOther()
    {
        var groups = Group(new Row(null, "Bulwark", 2), new Row(null, "Nightjar", 3));

        Assert.Equal(2, groups.Count);
        Assert.True(groups.All(g => !g.IsGrouped));
    }

    /// <summary>Two rows sharing a key fold into one group, summed.</summary>
    [Fact]
    public void TwoRowsSharingAKeyFoldIntoOneGroup()
    {
        var groups = Group(new Row("p1", "Bulwark", 2), new Row("p1", "Nightjar", 3));

        var group = Assert.Single(groups);
        Assert.True(group.IsGrouped);
        Assert.Equal(5, group.Total);
        Assert.Equal(2, group.Members.Count);
    }

    /// <summary>
    /// <b>Ordering: largest total first, then by label — at the group level and inside a group,
    /// the same rule both times.</b>
    /// </summary>
    [Fact]
    public void OrderingIsLargestFirstThenLabelBothLevels()
    {
        var groups = Group(
            new Row("p1", "Halo", 2),
            new Row("p1", "Nightjar", 3),
            new Row(null, "Bulwark", 4),
            new Row(null, "Absolute", 1));

        // Three groups: the grouped pair (5), then each lone row gets its own group (a null key
        // groups with nobody) — ordered by total, so the pair outranks Bulwark's 4 outranks
        // Absolute's 1.
        Assert.Equal(3, groups.Count);
        Assert.True(groups[0].IsGrouped);
        Assert.Equal(5, groups[0].Total);
        Assert.Equal("Bulwark", groups[1].Members[0].Label);
        Assert.Equal("Absolute", groups[2].Members[0].Label);

        // Inside the grouped pair: largest first, "Nightjar" (3) before "Halo" (2).
        Assert.Equal(["Nightjar", "Halo"], groups[0].Members.Select(m => m.Label));
    }

    /// <summary>Two lone rows of equal amount are ordered by label.</summary>
    [Fact]
    public void ATieAtTheTopIsBrokenByLabel()
    {
        var groups = Group(new Row(null, "Nightjar", 2), new Row(null, "Bulwark", 2));

        Assert.Equal(["Bulwark", "Nightjar"], groups.Select(g => g.Members[0].Label));
    }
}
