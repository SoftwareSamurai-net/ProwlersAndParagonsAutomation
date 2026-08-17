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
    /// worded for either and may not name one of them. Reached through the Abilities tab, which
    /// is the shape the app actually renders it in.
    /// </summary>
    [Fact]
    public void ThePickerSaysWhatAProAndAConDoWithNoneChosen()
    {
        var markup = Empty().Render<AbilitiesTab>().Markup;

        AssertSubstantive(markup, "AbilitiesTab");

        // Both halves of the wording, and neither may claim the subject is a Power: the same
        // component renders under an Ability, where "the Power" would simply be false.
        foreach (var state in States(markup))
            Assert.DoesNotContain("Power", state, StringComparison.Ordinal);
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
