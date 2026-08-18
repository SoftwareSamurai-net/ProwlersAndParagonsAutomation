using Bunit;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>The five sections of the characteristics step, in the order the strip shows them.</summary>
    private static readonly string[] Sections = ["Abilities", "Talents", "Powers", "Perks", "Flaws"];

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
    /// The Pros and Cons picker's empty state is worded for <b>any</b> subject, and may name none
    /// of the three it renders under.
    ///
    /// <para><b>Driven at every scope and both settings of <c>IsPro</c>, and two earlier versions
    /// were not.</b> The first rendered <c>AbilitiesTab</c>, which is where the app puts this
    /// picker — and that tab passes <c>IsPro="false"</c> and nothing else, so the Pro half of the
    /// wording was never rendered and putting "this Power does" into it passed. The second drove
    /// both halves and still banned only the word <c>Power</c>, on a component whose
    /// <c>Target</c> has three members: naming the <em>Ability</em> passed, and is false under a
    /// Power and under a piece of Gear. A driven test covers the arguments it sends, and a ban
    /// covers the words it lists.</para>
    /// </summary>
    [Theory]
    [InlineData(ProConPicker.Target.Power, true)]
    [InlineData(ProConPicker.Target.Power, false)]
    [InlineData(ProConPicker.Target.Ability, true)]
    [InlineData(ProConPicker.Target.Ability, false)]
    [InlineData(ProConPicker.Target.Gear, true)]
    [InlineData(ProConPicker.Target.Gear, false)]
    public void ThePickerSaysWhatAProAndAConDoWithNoneChosen(ProConPicker.Target scope, bool isPro)
    {
        using var ctx = Empty();

        var markup = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Scope, scope)
            .Add(c => c.IsPro, isPro)
            .Add(c => c.Selected, [])).Markup;

        AssertSubstantive(markup, $"ProConPicker({scope}, IsPro: {isPro})");

        // **No subject named, by any of the words that name one.** The enum's own members are read
        // so a fourth scope is covered the day it is added — but the property wanted is "names no
        // subject", not "names no enum member", and a fix-audit used the difference: "what this
        // Trait does" contains no member name and is false of a piece of Gear. The nouns this
        // rulebook uses for the three subjects are listed beside them.
        string[] subjects = [.. Enum.GetNames<ProConPicker.Target>(), "Trait", "item", "equipment"];

        foreach (var state in States(markup))
            foreach (var subject in subjects)
                Assert.DoesNotContain(subject, state, StringComparison.OrdinalIgnoreCase);

        // And the two halves genuinely differ — one sentence used for both would pass everything
        // above while telling a reader that a Con adds to the cost.
        Assert.Contains(isPro ? "widens" : "narrows", States(markup)[0], StringComparison.Ordinal);
        Assert.DoesNotContain(isPro ? "narrows" : "widens", States(markup)[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The Flaws tab's empty state quotes the creation minimum, and quotes it <b>from the rules</b>.
    ///
    /// <para><b>It is the one empty state that asserts a rules figure, and nothing checked the
    /// figure.</b> Swapping <c>MinAtCreation</c> for <c>MaxAtCreation</c> rendered "the rules ask
    /// for at least 3 at creation" — a false statement about the rulebook, told to the player, with
    /// the whole suite green: <c>AssertSubstantive</c> counts words and looks for a verb, and
    /// neither notices which number is in the sentence.</para>
    ///
    /// <para>Read from <c>creation_rules.json</c> through the repository rather than typed here, so
    /// this cannot drift from the data — and the *minimum* is asserted against the maximum as well,
    /// because on this rulebook they are 1 and 3, and a test that only looked for "1" would pass a
    /// sentence that had quoted the wrong end of a range that happened to start there.</para>
    /// </summary>
    [Fact]
    public void TheFlawsEmptyStateQuotesTheCreationMinimumFromTheRules()
    {
        using var ctx = Empty();

        var flawRules = ctx.Services
            .GetRequiredService<RulesRepository>()
            .CreationRules.FlawRules;

        var state = States(ctx.Render<FlawsTab>().Markup).Single();

        // **The whole clause, not the number in it.** Reading only "at least {min}" left the number
        // right and the claim false: "the rules make them optional, though at least 1 buys extra
        // Resolve" passed, and the rules require 1–3 at creation. Where the content of a sentence is
        // the deliverable, the sentence is the assertion — the same reason this project pins the MCP
        // server's baseline note verbatim. The duplicated literal buys the one thing that matters:
        // changing it has to be deliberate and visible in a diff.
        Assert.Contains(
            $"the rules ask for at least {flawRules.MinAtCreation} at creation",
            state, StringComparison.Ordinal);

        Assert.NotEqual(flawRules.MinAtCreation, flawRules.MaxAtCreation);
        Assert.DoesNotContain($"at least {flawRules.MaxAtCreation}", state, StringComparison.Ordinal);

        // And it does not simultaneously call them optional, which is the shape that got through.
        Assert.DoesNotContain("optional", state, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// On a fresh character <b>every</b> section is marked untouched, and each marker clears when
    /// its own section is filled.
    ///
    /// <para><b>The first version of this pinned the wrong rule, and an adversarial pass caught
    /// it.</b> It asserted that Abilities and Talents may <em>never</em> be marked, on the argument
    /// that Ch.2 floors every Trait at 1d so those sections are never empty, and that 0 HP there
    /// means a package covered the cost. The first half is true of a finished character and is
    /// exactly why a fresh one needs telling; the second is simply false — <c>AbilityCost</c> walks
    /// <c>AbilityRanks</c>, which is empty on a new sheet, so no package also costs 0 HP. The two
    /// sections that most needed marking were the two this test forbade from saying so, on a
    /// character the engine reports eighteen <c>TRAIT_BELOW_MINIMUM</c> errors deep.</para>
    ///
    /// <para><b>Each marker is now cleared independently, which the first version also did not
    /// do.</b> It filled Perks alone, so a mutation pinning Powers or Flaws to permanently
    /// untouched passed with fifteen entries on the sheet. Five sections, five fills.</para>
    /// </summary>
    [Fact]
    public void EverySectionIsMarkedUntouchedUntilItIsFilled()
    {
        using var ctx = Empty();

        var fresh = Marked(ctx);

        Assert.Equal(5, fresh.Count);
        Assert.All(fresh, m => Assert.True(m.Value,
            $"{m.Key} is empty on a fresh character and is not marked untouched."));

        // Each section filled on its own, and only its own marker may clear. A test that fills
        // one and checks one cannot tell an independent marker from a hard-coded true.
        var fills = new (string Section, Action Fill)[]
        {
            ("Abilities", () => ctx.Session.Sheet.AbilityRanks["might"] = 3),
            ("Talents",   () => ctx.Session.Sheet.TalentRanks["covert"] = 3),
            ("Powers",    () => ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("armor", 3))),
            ("Perks",     () => ctx.Session.Sheet.Perks.Add(new SelectedPerk("contacts", 1, "A dispatcher"))),
            ("Flaws",     () => ctx.Session.Sheet.Flaws.Add(new SelectedFlaw("enemy", "An old partner"))),
        };

        // **The marking is announced, not only drawn.** The ring is a shape, which survives a reader
        // who cannot see colour and reaches a screen reader not at all — so the word is there too,
        // off-screen. Replacing that span with an empty one passed everything else in this file,
        // because every other assertion reads the class list.
        var announced = ctx.Render<Characteristics>().FindAll(".tabs .sr-only");

        Assert.Equal(5, announced.Count);
        Assert.All(announced, s => Assert.False(string.IsNullOrWhiteSpace(s.TextContent),
            "An untouched section is ringed but says nothing a screen reader can read."));

        var filled = new List<string>();

        foreach (var (section, fill) in fills)
        {
            fill();
            filled.Add(section);

            foreach (var (name, marked) in Marked(ctx))
                Assert.True(marked != filled.Contains(name),
                    marked
                        ? $"{name} is still marked untouched after being filled — the marker is "
                          + "decoration rather than a state."
                        : $"{name} lost its marker when {section} was filled, and it is still "
                          + "empty. The markers are not independent of each other.");
        }
    }

    /// <summary>Each tab's section name, and whether its count is marked untouched.</summary>
    private static Dictionary<string, bool> Marked(RenderContext ctx) =>
        ctx.Render<Characteristics>()
            .FindAll(".tabs button")
            .ToDictionary(
                b => Sections.First(n => b.TextContent.Contains(n, StringComparison.Ordinal)),
                b => b.QuerySelector(".tab-count") is { } c && c.ClassList.Contains("untouched"),
                StringComparer.Ordinal);

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
