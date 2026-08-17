using Microsoft.Extensions.DependencyInjection;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The rulebook's word beside each rank — Ch.2's ABILITY RANKS table on printed p.17 and its
/// TALENT RANKS table on p.18.
///
/// <para><b>The feature shipped with nothing rendering it under test.</b> Its only guard was a
/// CSS-rule check that the class is set in muted small caps — which passes while the word is
/// wrong, invented, or set to <c>display: none</c>. A reviewer got three separate mutations
/// past the whole suite: reading the 6d word for every rank above 6d (the exact failure the
/// component's own comment says is prevented), an off-by-one that prints the 5d word beside a
/// 4d Trait, and hiding the element outright.</para>
///
/// <para>Per this project's split, that is bUnit's job: the markup is identical whichever word
/// comes out, so no test that reads source can tell.</para>
/// </summary>
public sealed class RankWordTests
{
    /// <summary>
    /// The word beside a Trait at a given rank is the one the rules file records for it, gloss
    /// stripped — checked at every rank the tables cover, for every Ability and every Talent.
    ///
    /// <para>Asserted against the data rather than against a list retyped here, so this cannot
    /// drift from the rulebook independently; the anchors below are what pin it to the book.</para>
    /// </summary>
    [Theory]
    [InlineData("ability")]
    [InlineData("talent")]
    public void TheWordBesideARankIsTheOneTheRulebookGivesThatRank(string kind)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var traits = kind == "ability"
            ? rules.Abilities.Select(a => (a.Id, a.Name, a.RankGuide)).ToList()
            : rules.Talents.Select(t => (t.Id, t.Name, t.RankGuide)).ToList();

        Assert.NotEmpty(traits);

        var checkedAny = false;

        foreach (var (id, name, guide) in traits)
        {
            Assert.NotEmpty(guide);

            for (var rank = 1; rank <= 6; rank++)
            {
                if (!guide.TryGetValue($"{rank}d", out var entry)) continue;

                if (kind == "ability") ctx.Session.Sheet.AbilityRanks[id] = rank;
                else ctx.Session.Sheet.TalentRanks[id] = rank;

                var row = Row(ctx, kind, name);
                var expected = entry.Split(" (", StringSplitOptions.None)[0];

                Assert.Equal(expected, row);

                // The gloss two of the rungs carry — "Undeveloped (ordinary human average)" —
                // is a sentence about people in general, not a label on one row of a form.
                Assert.DoesNotContain("(", row, StringComparison.Ordinal);

                checkedAny = true;
            }
        }

        Assert.True(checkedAny, "No rank guide was exercised, so this test asserted nothing.");
    }

    /// <summary>
    /// Two anchors straight from the printed tables, so the theory above cannot agree with a
    /// rules file that has quietly changed — and so the two tables cannot be swapped. A 4d
    /// Ability is Noteworthy; a 4d Talent is Advanced.
    /// </summary>
    [Fact]
    public void EachTableIsTheOneItsOwnTraitsUse()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.AbilityRanks["might"] = 4;
        ctx.Session.Sheet.TalentRanks["academics"] = 4;

        Assert.Equal("Noteworthy", Row(ctx, "ability", "Might"));
        Assert.Equal("Advanced", Row(ctx, "talent", "Academics"));
    }

    /// <summary>
    /// <b>Above 6d there is no word, and none is invented.</b> Both tables stop at 6d because
    /// above that is superhuman and the rulebook names no rung — so the cell is empty rather
    /// than carrying the nearest word, which would be this program inventing a rung the book
    /// does not have. Reading the 6d entry for every higher rank left the suite green.
    /// </summary>
    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(12)]
    public void AboveSixDNoWordIsInvented(int rank)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.AbilityRanks["might"] = rank;
        ctx.Session.Sheet.TalentRanks["academics"] = rank;

        Assert.Equal("", Row(ctx, "ability", "Might"));
        Assert.Equal("", Row(ctx, "talent", "Academics"));
    }

    /// <summary>
    /// And the word is on the page at all. Every assertion above is satisfied by an element
    /// that is present and hidden, which is how <c>display: none</c> on the class passed.
    /// </summary>
    [Fact]
    public void TheWordIsRenderedRatherThanMerelyPresent()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        foreach (var ability in ctx.Services.GetRequiredService<RulesRepository>().Abilities)
            ctx.Session.Sheet.AbilityRanks[ability.Id] = 3;

        var words = ctx.Render<AbilitiesTab>().FindAll(".rank-word")
            .Select(e => e.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.Equal("Developed", w));
    }

    /// <summary>The word cell of one named Trait's row, on the tab that edits it.</summary>
    private static string Row(RenderContext ctx, string kind, string traitName)
    {
        var page = kind == "ability"
            ? ctx.Render<AbilitiesTab>()
            : (IRenderedComponent<Microsoft.AspNetCore.Components.IComponent>)ctx.Render<TalentsTab>();

        var row = page.FindAll(".rank-row")
            .FirstOrDefault(r => r.QuerySelector(".rank-name")?.TextContent.TrimStart()
                .StartsWith(traitName, StringComparison.Ordinal) == true);

        Assert.True(row is not null, $"No rank row for {traitName}.");

        return row!.QuerySelector(".rank-word")?.TextContent.Trim()
               ?? throw new InvalidOperationException(
                   $"{traitName}'s row has no .rank-word cell at all, so no word can be shown.");
    }
}
