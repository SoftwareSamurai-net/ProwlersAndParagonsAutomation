using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// An empty list says what to do next, not that it is empty.
///
/// <para>Six of these read "None yet.", "None." or "Nothing yet." — a full stop restating a fact
/// the reader can already see, on the one screen where a tool is least useful and best placed to
/// help. This is the guard that keeps them from going back.</para>
///
/// <para><b>It renders rather than reads the source, and it has to.</b> The sentence is the
/// deliverable here, and a source scan cannot tell an empty state that is shown from one behind a
/// branch nothing reaches — four of these six are several interactions deep in the real app, and
/// the shape of that mistake is recorded twice in this project already: a filter-box test that
/// passed against a page with no options on it, and an uppercased-text guard whose selectors
/// matched nothing on any rendered page.</para>
///
/// <para><b>The word count is crude on purpose, and it is honest about what it buys.</b> It cannot
/// tell a useful instruction from eight useless words, and nothing mechanical can — what it does
/// buy is that the six sentences cannot be quietly cut back to a negation, which is exactly how
/// they started. Whether a given one is *good* is a question for reading it, and the entry in
/// PROGRESS.md says so.</para>
/// </summary>
public sealed class EmptyStateTests
{
    /// <summary>
    /// What "names an action" is allowed to look like. Either an imperative pointing at the
    /// control to use, or — for the Pros and Cons picker, whose next action is a button already
    /// beside it — a statement of what the thing would do to the character.
    /// </summary>
    private static readonly string[] ActionWords = ["Add", "Pick", "Choose", "widens", "narrows"];

    /// <summary>
    /// Every editor that can hold nothing, rendered holding nothing.
    ///
    /// <para>The character is deliberately <b>not</b> a loaded sample: <c>RenderContext.With</c>
    /// fills every section, which is the opposite of the state under test. A tier is selected
    /// because the Characteristics step refuses to render its tabs without one, and because a
    /// budget of nothing is a different screen.</para>
    /// </summary>
    private static RenderContext Empty()
    {
        var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        return ctx;
    }

    [Fact]
    public void ThePowersTabSaysWhatToDoWithNoPowers() =>
        AssertSubstantive(Empty().Render<PowersTab>().Markup, "PowersTab");

    [Fact]
    public void ThePerksTabSaysWhatToDoWithNoPerks() =>
        AssertSubstantive(Empty().Render<PerksTab>().Markup, "PerksTab");

    [Fact]
    public void TheFlawsTabSaysWhatToDoWithNoFlaws() =>
        AssertSubstantive(Empty().Render<FlawsTab>().Markup, "FlawsTab");

    [Fact]
    public void TheGearStepSaysWhatToDoWithNothingCarried() =>
        AssertSubstantive(Empty().Render<Gear>().Markup, "Gear");

    [Fact]
    public void TheFinishingStepSaysWhatToDoWithNoConnections() =>
        AssertSubstantive(Empty().Render<Finishing>().Markup, "Finishing");

    /// <summary>
    /// The Pros and Cons picker, which serves Powers and Abilities both — so its empty state is
    /// worded for either and may not name one of them.
    ///
    /// <para><b>Driven at both settings of <c>IsPro</c>, and the first version was not.</b> It
    /// rendered <c>AbilitiesTab</c>, which is where the app puts this picker — and that tab passes
    /// <c>IsPro="false"</c> and nothing else, so the Pro half of the wording was never rendered
    /// and a mutation putting "this Power does" into it passed. A driven test covers the arguments
    /// it sends; this project has the same finding recorded against the MCP server's refusal
    /// branches, which every tool had and none of them drove.</para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThePickerSaysWhatAProAndAConDoWithNoneChosen(bool isPro)
    {
        using var ctx = Empty();

        var markup = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Scope, ProConPicker.Target.Ability)
            .Add(c => c.IsPro, isPro)
            .Add(c => c.Selected, [])).Markup;

        AssertSubstantive(markup, $"ProConPicker(IsPro: {isPro})");

        // The subject is not named, because it varies: the same component renders under a Power
        // and under an Ability, and "the Power" is false half the time it is shown.
        foreach (var state in States(markup))
            Assert.DoesNotContain("Power", state, StringComparison.Ordinal);

        // And the two halves genuinely differ — one sentence used for both would pass everything
        // above while telling a reader a Con adds to the cost.
        Assert.Contains(isPro ? "widens" : "narrows", States(markup)[0], StringComparison.Ordinal);
        Assert.DoesNotContain(isPro ? "narrows" : "widens", States(markup)[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The tab strip marks the sections that have nothing in them, and marks only the three that
    /// can be empty.
    ///
    /// <para><b>This shipped with no guard and a mutation deleting the marker passed.</b> Which is
    /// the pattern this project keeps recording — a new surface added and reviewed by nothing —
    /// so it is here rather than waiting for a reviewer to find it.</para>
    ///
    /// <para>The negative half is the load-bearing one. Ch.2 floors every Ability and Talent at
    /// 1d, so a character has all eighteen and those sections are never untouched; their figure is
    /// a cost, and 0 HP means a package covered it. Marking them would tell a reader that eighteen
    /// Traits they cannot be without are missing.</para>
    /// </summary>
    [Fact]
    public void TheTabStripMarksOnlyTheSectionsThatCanBeEmpty()
    {
        using var ctx = Empty();

        var tabs = ctx.Render<Characteristics>().FindAll(".tabs button");

        Assert.Equal(5, tabs.Count);

        foreach (var tab in tabs)
        {
            var label = tab.TextContent;
            var count = tab.QuerySelector(".tab-count");

            Assert.True(count is not null, $"The {label} tab shows no count.");

            var marked = count!.ClassList.Contains("untouched");
            var isCollection = label.Contains("Powers", StringComparison.Ordinal)
                               || label.Contains("Perks", StringComparison.Ordinal)
                               || label.Contains("Flaws", StringComparison.Ordinal);

            Assert.True(marked == isCollection,
                isCollection
                    ? $"{label.Trim()} is empty on a fresh character and is not marked untouched."
                    : $"{label.Trim()} is marked untouched, but Ch.2 floors every Ability and "
                      + "Talent at 1d — that section cannot be empty, and its figure is a cost.");
        }

        // And the marker goes when the section fills, or it is decoration rather than a state.
        ctx.Session.Sheet.Perks.Add(new SelectedPerk("contacts", 1, "A precinct dispatcher"));

        var perks = ctx.Render<Characteristics>()
            .FindAll(".tabs button")
            .Single(b => b.TextContent.Contains("Perks", StringComparison.Ordinal));

        Assert.DoesNotContain("untouched", perks.QuerySelector(".tab-count")!.ClassList);
    }

    /// <summary>
    /// Every empty state in some markup, as its text with tags removed.
    ///
    /// <para>Text nodes are concatenated rather than joined by a separator, for the reason
    /// <c>CLAUDE.md</c> records: replacing a tag with whitespace makes
    /// <c>&lt;b&gt;Armor&lt;/b&gt;&lt;span&gt;8d&lt;/span&gt;</c> read as "Armor 8d", which is the
    /// bug such a test exists to find. Here it would inflate the word count with words nobody
    /// wrote — <c>&lt;b&gt;Add a Power&lt;/b&gt;</c> is three of them either way, but a
    /// separator-joined reading of a sentence full of markup could pass a threshold the prose
    /// does not reach.</para>
    /// </summary>
    private static List<string> States(string markup) =>
        [.. System.Text.RegularExpressions.Regex
            .Matches(markup, @"<p class=""empty-state[^""]*"">(.*?)</p>",
                     System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => System.Text.RegularExpressions.Regex.Replace(m.Groups[1].Value, "<[^>]*>", ""))
            .Select(t => System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim())];

    private static void AssertSubstantive(string markup, string where)
    {
        var states = States(markup);

        // **The instrument reports its own reach.** A scan that found no empty state passes every
        // assertion below it, and that is how a guard becomes theatre — so not finding one is the
        // failure, not a quiet pass.
        Assert.True(states.Count > 0,
            $"{where} rendered with an empty character produced no .empty-state element. Either "
            + "the empty state is gone, or this test is no longer reaching the branch that shows "
            + "it — and both of those are the thing it exists to catch.");

        foreach (var state in states)
        {
            var words = state.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            Assert.True(words >= 8,
                $"{where}'s empty state is {words} words: \"{state}\". An empty list is where "
                + "this tool can most usefully say what happens next; a negation and a full stop "
                + "spends that moment restating what the reader can see.");

            // And it points somewhere. Every one of the six either names the control to use or
            // says what the thing would do — a sentence with no verb of action in it is a longer
            // way of saying "none".
            Assert.True(
                ActionWords.Any(v => state.Contains(v, StringComparison.Ordinal)),
                $"{where}'s empty state names no action: \"{state}\".");
        }
    }
}
