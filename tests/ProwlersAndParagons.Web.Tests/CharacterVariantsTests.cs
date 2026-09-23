using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Item 21's slice one, the roster half: <see cref="CharacterVariants.Group"/> builds the tree a
/// roster draws, <see cref="CharacterVariants.Label"/> is the copy on a child row, and
/// <see cref="CharacterVariants.WouldCreateCycle"/> is what a chooser asks before it lets a link
/// be made.
/// </summary>
public sealed class CharacterVariantsTests
{
    private static VariantRow Root(string id, string name) => new(id, name, null);

    private static VariantRow Child(string id, string name, string rootId, string kind) =>
        new(id, name, new CharacterVariant(rootId, kind));

    // ── Labels ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CharacterVariant.Later, "later version")]
    [InlineData(CharacterVariant.AsSeenBy, "as seen by another audience")]
    [InlineData(CharacterVariant.AlternateForm, "alternate form")]
    public void EveryKnownKindHasALabel(string kind, string expected) =>
        Assert.Equal(expected, CharacterVariants.Label(kind));

    [Fact]
    public void AnUnknownKindHasNoLabel() =>
        Assert.Null(CharacterVariants.Label("cursed_mirror"));

    // ── A lone character renders exactly as it always did ───────────────────────────

    [Fact]
    public void ALoneCharacterIsNotGrouped()
    {
        var families = CharacterVariants.Group([Root("c1", "Solo")]);

        var family = Assert.Single(families);
        Assert.False(family.IsGrouped);
        Assert.Equal("c1", family.Root!.Value.Id);
        Assert.Empty(family.Children);
    }

    // ── A root with several variants draws a tree, root first ───────────────────────

    [Fact]
    public void ARootWithTwoVariantsDrawsATreeInTheRightOrder()
    {
        var rows = new[]
        {
            Child("c2", "Cael Hughes — After School Specials", "c1", CharacterVariant.Later),
            Root("c1", "Cael Hughes — Emergence"),
            Child("c3", "Cael Hughes — Realised", "c1", CharacterVariant.Later),
        };

        var family = Assert.Single(CharacterVariants.Group(rows));

        Assert.True(family.IsGrouped);
        Assert.Equal("c1", family.Root!.Value.Id);
        Assert.Equal(2, family.Children.Count);

        // Both children share a kind, so the tie is broken by name — "Realised" before
        // "After School Specials" is alphabetically wrong, so this also proves the order is
        // driven by the sort and not by input order.
        Assert.Equal(
            ["Cael Hughes — After School Specials", "Cael Hughes — Realised"],
            family.Children.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void ChildrenOfDifferentKindsOrderByKindFirst()
    {
        var rows = new[]
        {
            Root("lena-root", "Lena"),
            Child("lena-form", "Lena (alternate form)", "lena-root", CharacterVariant.AlternateForm),
            Child("lena-later", "Lena (later)", "lena-root", CharacterVariant.Later),
            Child("lena-seen", "Lena (as observed)", "lena-root", CharacterVariant.AsSeenBy),
        };

        var family = Assert.Single(CharacterVariants.Group(rows));

        Assert.Equal(
            ["Lena (later)", "Lena (as observed)", "Lena (alternate form)"],
            family.Children.Select(c => c.Name).ToArray());
    }

    // ── An orphan chain (root not held) renders flat, with no root at all ───────────

    [Fact]
    public void AChainWhoseRootIsNotHeldHasNoRootAndDrawsItsChildAsAFamilyOfOne()
    {
        var rows = new[] { Child("c2", "Lena (as observed)", "missing-root", CharacterVariant.AsSeenBy) };

        var family = Assert.Single(CharacterVariants.Group(rows));

        Assert.Null(family.Root);
        var only = Assert.Single(family.Children);
        Assert.Equal("c2", only.Id);
    }

    // ── Two independent families never bleed into each other ────────────────────────

    [Fact]
    public void TwoIndependentFamiliesStaySeparate()
    {
        var rows = new[]
        {
            Root("cael", "Cael Hughes"),
            Child("cael-later", "Cael Hughes — Realised", "cael", CharacterVariant.Later),
            Root("emir-a", "Emir Hughes"),
            Root("emir-b", "Emir Hughes"),
        };

        var families = CharacterVariants.Group(rows);

        Assert.Equal(3, families.Count);
        Assert.Single(families, f => f.Root?.Id == "cael" && f.Children.Count == 1);
        Assert.Equal(2, families.Count(f => f.Root is not null && f.Children.Count == 0));
    }

    /// <summary>
    /// <b>This mechanism is one level deep, and a chain three characters long shows it.</b> Every
    /// child names the same root — Ch.8's own examples are three siblings of one root, not a
    /// chain — so a version <em>of</em> a version has no root anybody actually holds in this
    /// family's group, and it draws exactly as an orphan does: see
    /// <see cref="AChainWhoseRootIsNotHeldHasNoRootAndDrawsItsChildAsAFamilyOfOne"/>. Deepening
    /// this is out of scope for slice one and not something the owner's roster needs today.
    /// </summary>
    [Fact]
    public void AVersionOfAVersionIsAnOrphanOfItsOwnRatherThanNestingTwoDeep()
    {
        var rows = new[]
        {
            Root("gen1", "Original"),
            Child("gen2", "Later", "gen1", CharacterVariant.Later),
            Child("gen3", "Even later", "gen2", CharacterVariant.Later),
        };

        var families = CharacterVariants.Group(rows).ToList();

        var rootFamily = families.Single(f => f.Root?.Id == "gen1");
        Assert.Single(rootFamily.Children, c => c.Id == "gen2");

        var orphan = families.Single(f => f.Root is null);
        Assert.Single(orphan.Children, c => c.Id == "gen3");
    }

    // ── Cycle and self-link refusal ──────────────────────────────────────────────────

    [Fact]
    public void ASelfLinkIsRefused() =>
        Assert.True(CharacterVariants.WouldCreateCycle(
            "c1", "c1", new Dictionary<string, string?>()));

    [Fact]
    public void ADirectCycleIsRefused()
    {
        // B is already recorded as a version of A. Making A a version of B would close the loop.
        var lookup = new Dictionary<string, string?> { ["b"] = "a" };

        Assert.True(CharacterVariants.WouldCreateCycle("a", "b", lookup));
    }

    [Fact]
    public void AnIndirectCycleThroughTheWholeChainIsRefused()
    {
        // c -> b -> a. Making a a version of c would close a three-character loop.
        var lookup = new Dictionary<string, string?> { ["b"] = "a", ["c"] = "b" };

        Assert.True(CharacterVariants.WouldCreateCycle("a", "c", lookup));
    }

    [Fact]
    public void AnOrdinaryLinkToAnUnrelatedRootIsAllowed()
    {
        var lookup = new Dictionary<string, string?> { ["b"] = "a" };

        Assert.False(CharacterVariants.WouldCreateCycle("z", "a", lookup));
    }

    [Fact]
    public void LinkingTwoRootsTogetherIsAllowed() =>
        Assert.False(CharacterVariants.WouldCreateCycle("child", "root", new Dictionary<string, string?>()));
}
