using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Layout;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// What the replay actually renders.
///
/// <para>Two things would ruin this surface and neither is visible to a compiler. The first is
/// <b>faking the numbers</b>: if a figure on the page came out of the recording instead of out
/// of the engine, the demonstration would be misrepresenting the thing it exists to
/// demonstrate — so the tests below read the figures off the rendered page and assert they
/// equal what the calculators answer for the same character, rather than merely that a number
/// appeared. The second is <b>not labelling it</b>, which is asserted on the rendered page for
/// the same reason a source scan is not enough: the label being in the file is not the label
/// being on the screen.</para>
///
/// <para>Everything here reads <c>TextContent</c> rather than markup with the tags taken out.
/// See <see cref="SheetRenderTests"/> for what that concession costs.</para>
/// </summary>
public sealed class ReplayRenderTests
{
    private const string Cheap = "vera-nunn";
    private const string Ambiguous = "chrono-jab";
    private const string DidNotFit = "sheet-lightning";
    private const string Villain = "the-conductor";

    /// <summary>
    /// The two sample characters, named the way a recording is so both can be written as one
    /// <c>InlineData</c>. They are here because no recorded character has a rankless Power and
    /// the Hero sample does — see
    /// <see cref="ARanklessPowerPrintsTheStandInRankOfTheCharacterOnThePage"/>.
    /// </summary>
    private const string OurHero = "sample:hero";

    private const string OurVillain = "sample:villain";

    private static CharacterSheet Character(RenderContext ctx, string key) => key switch
    {
        OurHero => SampleCharacters.Hero(),
        OurVillain => SampleCharacters.Villain(),
        _ => Conversation(ctx, key).FinalCharacter
             ?? throw new InvalidOperationException($"'{key}' never arrives at a character.")
    };

    private static Transcript Conversation(RenderContext ctx, string id) =>
        ctx.Services.GetRequiredService<ReplayLibrary>().Find(id)
        ?? throw new InvalidOperationException($"No recording called '{id}'.");

    private static IRenderedComponent<ReplayConversation> Play(RenderContext ctx, string id) =>
        ctx.Render<ReplayConversation>(p => p.Add(c => c.Id, id));

    private static IElement Button(IRenderedComponent<ReplayConversation> page, string label) =>
        page.FindAll("button").First(b => b.TextContent.Contains(label, StringComparison.Ordinal));

    /// <summary>
    /// One labelled figure off the rendered page — "Spent 72" becomes 72. It reads the whole
    /// element's text, so a value split across two elements still arrives as one string, which
    /// is the failure a markup search cannot see.
    /// </summary>
    private static int Figure(IRenderedComponent<ReplayConversation> page, string label)
    {
        var text = page.FindAll(".replay-figures span")
            .Select(s => s.TextContent.Trim())
            .First(t => t.StartsWith(label, StringComparison.Ordinal));

        // The Trait Cap is a rank and prints as "12d", the way the rulebook writes one. The
        // trailing d is trimmed here rather than the parse being made lenient: anything else
        // left over is still a failure, which is what catches a figure that arrived with the
        // wrong unit stuck to it.
        return int.Parse(
            text[label.Length..].Trim().TrimEnd('d'),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ShowAll(IRenderedComponent<ReplayConversation> page) =>
        Button(page, "Show the rest").Click();

    /// <summary>
    /// Everything the page reads as text, the way a browser concatenates it — not the markup
    /// with the tags taken out, which puts a separator wherever a tag was and so satisfies a
    /// search for two words that are never next to each other on the screen.
    /// </summary>
    private static string Text<T>(IRenderedComponent<T> page) where T : IComponent =>
        string.Concat(page.Nodes.Select(n => n.TextContent));

    // ── It says it is a recording ───────────────────────────────────────────────

    /// <summary>
    /// A replayed conversation presented as a live one is a lie about what the visitor is
    /// looking at. The label is asserted on the rendered page and on the <b>first</b> screen,
    /// before anything has been revealed — a notice that only appears at the end has been read
    /// after it was needed.
    /// </summary>
    [Theory]
    [InlineData(Cheap)]
    [InlineData(Ambiguous)]
    [InlineData(DidNotFit)]
    [InlineData(Villain)]
    public void EveryRecordingSaysItIsARecordingBeforeAnythingElseHappens(string id)
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, id);

        var text = Text(page);
        Assert.Contains("This is a recording", text, StringComparison.Ordinal);

        // Before the conversation, in reading order — not merely somewhere on the page. Moved
        // to the foot, the label passed a `Contains` check while a visitor met the recording
        // first and the notice about it only if they scrolled past everything.
        Assert.True(
            text.IndexOf("This is a recording", StringComparison.Ordinal)
            < text.IndexOf(Conversation(ctx, id).Turns[0].Text, StringComparison.Ordinal),
            "The label comes after the first line of the recording it is labelling.");
    }

    /// <summary>
    /// And every line is attributed to the side that actually said it.
    ///
    /// <para><b>Asserting the attribution is <em>present</em> is not enough</b>, which an
    /// adversarial pass demonstrated by swapping the two labels: every line in every recording
    /// was credited to the wrong speaker and this test, which counted the labels and checked
    /// they were not blank, stayed green. The parser already refuses a turn with no speaker
    /// and says why — that guard is about the file, and this one is about the screen.</para>
    /// </summary>
    [Theory]
    [InlineData(Cheap)]
    [InlineData(Ambiguous)]
    [InlineData(DidNotFit)]
    [InlineData(Villain)]
    public void EveryLineIsAttributedToTheSideThatSaidIt(string id)
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, id);
        ShowAll(page);

        var recorded = Conversation(ctx, id).Turns;
        var rendered = page.FindAll(".replay-turn");

        Assert.Equal(recorded.Count, rendered.Count);

        for (var i = 0; i < recorded.Count; i++)
        {
            var who = rendered[i].QuerySelector(".replay-who")!.TextContent.Trim();

            // The recorded words and the attribution beside them, together. Checked as a pair
            // so a page that labelled every turn correctly while showing them out of order
            // still fails.
            Assert.Contains(recorded[i].Text, rendered[i].TextContent, StringComparison.Ordinal);
            Assert.Equal(
                recorded[i].Speaker == TranscriptSpeaker.Person ? "The player" : "The assistant",
                who);
        }
    }

    // ── It goes at the visitor's pace ───────────────────────────────────────────

    /// <summary>
    /// One line to start with, and the rest when they ask. A page that dumps the whole
    /// conversation at once is a transcript, which they could read in the repository.
    /// </summary>
    [Fact]
    public void ARecordingStartsOnItsFirstLineAndAdvancesOnAClick()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, DidNotFit);

        Assert.Single(page.FindAll(".replay-turn"));

        Button(page, "Next").Click();
        Assert.Equal(2, page.FindAll(".replay-turn").Count);

        ShowAll(page);
        Assert.Equal(Conversation(ctx, DidNotFit).Turns.Count, page.FindAll(".replay-turn").Count);
    }

    /// <summary>
    /// The character, the sheet and the hand-off only appear at the end. Offering to replace
    /// somebody's character halfway through a conversation offers them a draft — and in one of
    /// these recordings the draft is the one that does not fit.
    /// </summary>
    [Fact]
    public void TheHandOffAndTheSheetWaitUntilTheConversationIsOver()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, DidNotFit);

        Assert.Empty(page.FindAll(".sheet"));
        Assert.DoesNotContain("Open ", Text(page), StringComparison.Ordinal);

        ShowAll(page);

        Assert.Single(page.FindAll(".sheet"));
        Assert.Contains("Open ", Text(page), StringComparison.Ordinal);
    }

    // ── The numbers are the engine's ────────────────────────────────────────────

    /// <summary>
    /// <b>The figure on the page is the calculator's answer for that character.</b> This is
    /// the test the whole surface stands on: a replay that showed a stored number would look
    /// exactly like this one and be worthless.
    ///
    /// <para>Asserted against the engine's own answer rather than against a number written
    /// here, because a hard-coded expectation is the same mistake in a different file — it
    /// would go on passing after the rules changed, agreeing with a page that had gone wrong.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Cheap)]
    [InlineData(Ambiguous)]
    [InlineData(DidNotFit)]
    [InlineData(Villain)]
    public void EverySpendOnThePageIsTheOneTheCalculatorAnswers(string id)
    {
        using var ctx = new RenderContext();
        var costs = ctx.Services.GetRequiredService<CostCalculator>();
        var page = Play(ctx, id);
        ShowAll(page);

        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();
        var conversation = Conversation(ctx, id);
        var character = conversation.Turns.First(t => t.Character is not null).Character!;

        Assert.Equal(costs.TotalCost(character), Figure(page, "Spent"));
        Assert.Equal(costs.PackageCost(character), Figure(page, "Package"));
        Assert.Equal(costs.AbilityCost(character), Figure(page, "Abilities"));
        Assert.Equal(costs.TalentCost(character), Figure(page, "Talents"));
        Assert.Equal(costs.TotalPowersCost(character), Figure(page, "Powers"));

        // Perks and Gear were left off this list, and a pass that added 7 to one and 3 to the
        // other went unnoticed. They are 0 on every recorded character, which is exactly why
        // they need asserting: a figure nobody checks is a figure that can say anything.
        Assert.Equal(costs.TotalPerksCost(character), Figure(page, "Perks"));
        Assert.Equal(costs.TotalGearCost(character), Figure(page, "Gear"));

        // The tier's own two numbers, and the gap — the line somebody actually reads to decide
        // whether the character fits.
        var tier = rules.GetTier(character.SelectedTierId!)!;
        Assert.Equal(tier.TraitCapRank, Figure(page, "Trait Cap"));

        if (!conversation.Villain)
        {
            Assert.Equal(tier.HeroPoints, Figure(page, "Budget"));
            Assert.Equal(
                Math.Abs(tier.HeroPoints - costs.TotalCost(character)),
                Figure(page, costs.TotalCost(character) > tier.HeroPoints ? "Over by" : "Left"));
        }

        // And the three figures a player reads off mid-scene. Scoped to the first panel, to
        // match the character taken above: one recording puts a draft and a settlement on the
        // page and each gets a panel of its own.
        var stats = page.FindAll(".replay-verdict")[0]
            .QuerySelectorAll(".stat-block").Select(b => b.TextContent).ToList();
        foreach (var (label, value) in new[]
                 {
                     ("Edge", derived.CalculateEdge(character)),
                     ("Health", derived.CalculateHealth(character)),
                     ("Resolve", derived.CalculateResolve(character))
                 })
        {
            var block = stats.Single(s => s.Contains(label, StringComparison.Ordinal));
            Assert.Contains(value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                block, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>And nothing in the verdict panel comes from the visitor's character either.</b>
    ///
    /// <para>The test above reads the figures off the page and compares them to the engine's
    /// answer for the recorded character — which catches a stored number and a hard-coded
    /// offset, and does not catch the panel costing the <em>wrong character</em>, because the
    /// context it renders in has an empty sheet and the recorded characters have no Perks and
    /// no gear. All three figures are 0, so all three agree. An adversarial pass pointed the
    /// Perks and Gear rows at <c>Session.Sheet</c> and both suites stayed green — on the two
    /// rows that had been added specifically to close that gap.</para>
    ///
    /// <para>So this asks the question directly: the same recorded character, rendered under
    /// two <em>different</em> visitors, has to produce the same panel. No figure is named, so
    /// a row added later is covered the day it is added.</para>
    /// </summary>
    [Theory]
    [InlineData(Cheap, false)]
    [InlineData(DidNotFit, false)]
    [InlineData(Villain, true)]
    public void TheVerdictPanelReadsOnlyTheCharacterItWasGiven(string id, bool villain)
    {
        string Panel(RenderContext context, CharacterSheet visitors, CharacterSheet subject)
        {
            context.Session.Restore(visitors, SheetMode.Hero);

            return context.Render<ReplayVerdict>(p => p
                    .Add(v => v.Character, subject)
                    .Add(v => v.Villain, villain))
                .Find(".replay-verdict").TextContent;
        }

        using var ctx = new RenderContext();
        var costs = ctx.Services.GetRequiredService<CostCalculator>();
        var subject = Character(ctx, id);

        // Two visitors who differ in every figure this panel prints. The Hero sample carries
        // Perks and customised gear and the Villain sample carries neither, which is what makes
        // those two rows able to bite at all; the tier is moved so the budget and the Trait Cap
        // can too.
        var one = SampleCharacters.Hero();
        var other = SampleCharacters.Villain();
        other.SelectedTierId = "street_level";

        Assert.NotEqual(one.SelectedTierId, other.SelectedTierId);
        Assert.NotEqual(costs.TotalPerksCost(one), costs.TotalPerksCost(other));
        Assert.NotEqual(costs.TotalGearCost(one), costs.TotalGearCost(other));
        Assert.NotEqual(costs.TotalCost(one), costs.TotalCost(other));

        Assert.Equal(Panel(ctx, one, subject), Panel(ctx, other, subject));
    }

    /// <summary>
    /// The recording that turns on a draft not fitting has to <em>show</em> it not fitting, and
    /// then show the settled character fitting. Both verdicts are the validator's, taken off
    /// the rendered page.
    /// </summary>
    [Fact]
    public void TheDraftThatDidNotFitIsShownNotFittingAndTheSettledOneIsNot()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, DidNotFit);
        ShowAll(page);

        var verdicts = page.FindAll(".verdict").Select(v => v.TextContent.Trim()).ToList();

        Assert.Equal(2, verdicts.Count);
        Assert.Equal("Not legal yet", verdicts[0]);
        Assert.Equal("Legal", verdicts[^1]);

        // And the finding itself, in the validator's own words rather than a paraphrase.
        Assert.Contains("budget", page.FindAll(".issues li").Single().TextContent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Villain has no Hero Point budget (Ch.9) and the engine is never told which it is
    /// looking at, so it reports the overspend regardless. The replay neither hides that nor
    /// calls the character illegal for it: no verdict is claimed, and the reason is on the
    /// page in words.
    ///
    /// <para><b>Both halves, and only the second used to be asserted.</b> The GM review step
    /// legitimately filters <c>HP_BUDGET_EXCEEDED</c> in Villain mode, so copying that one line
    /// into this panel "for consistency" is a change somebody would make — and it left the
    /// Conductor printing "Nothing to report." underneath a paragraph explaining a finding
    /// that was no longer there, with the suite green. <c>CLAUDE.md</c> is explicit that this
    /// recording shows its budget finding rather than hiding it; that is what the recording is
    /// <em>about</em>.</para>
    /// </summary>
    [Fact]
    public void AVillainIsNotCalledIllegalForHavingNoBudget()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, Villain);
        ShowAll(page);

        Assert.Empty(page.FindAll(".verdict"));

        // The finding itself is on the page, in the validator's own words rather than a
        // paraphrase — asked of the engine here so this cannot agree with a message that has
        // drifted.
        var overspend = ctx.Services.GetRequiredService<CharacterValidator>()
            .Validate(Conversation(ctx, Villain).FinalCharacter!)
            .Issues.Single(i => i.Code == "HP_BUDGET_EXCEEDED");

        Assert.Contains(
            page.FindAll(".replay-verdict .issues li"),
            li => li.TextContent.Contains(overspend.Message, StringComparison.Ordinal));

        var text = Text(page);
        Assert.Contains("Ch.9", text, StringComparison.Ordinal);
        Assert.Contains("GM's call", text, StringComparison.Ordinal);

        // The budget is not offered as a figure either: there is nothing to measure against.
        Assert.DoesNotContain(
            page.FindAll(".replay-figures span").Select(s => s.TextContent.Trim()),
            t => t.StartsWith("Budget", StringComparison.Ordinal));
    }

    // ── The sheet is the recorded character's ───────────────────────────────────

    /// <summary>
    /// <b>The sheet at the end shows the recorded character, not the visitor's own.</b>
    ///
    /// <para>This is not hypothetical. The four big figures on the sheet come from a component
    /// that reads the character being built, so before it was given the recorded one to read,
    /// a replay printed somebody else's Edge, Health and Resolve under a recorded character's
    /// name — which is the kind of wrong that looks entirely right. The test loads a sample
    /// first, so there is a different character present to be printed by mistake.</para>
    ///
    /// <para><b>All four boxes, and asserted on the value rather than on the box.</b> Two
    /// adversarial passes walked through weaker versions of this test. The first asserted Edge
    /// alone, so Health and Resolve went back to the visitor's character unnoticed. Widened to
    /// three, the next moved the <em>fourth</em> box — the Hero Point spend, which is the
    /// headline figure a GM checks a character against — and it still passed, because the box
    /// carries "105" over a sub-line reading "of 75" and a search of the box's whole text for
    /// "75" finds the budget. So this reads <c>.value</c>, and compares it whole.</para>
    /// </summary>
    [Theory]
    [InlineData("Edge")]
    [InlineData("Health")]
    [InlineData("Resolve")]
    [InlineData("Hero Points")]
    public void TheSheetAtTheEndCarriesTheRecordedCharactersOwnFigures(string label)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();
        var costs = ctx.Services.GetRequiredService<CostCalculator>();

        var recorded = Conversation(ctx, Cheap).FinalCharacter!;

        int Stat(CharacterSheet sheet) => label switch
        {
            "Edge" => derived.CalculateEdge(sheet),
            "Health" => derived.CalculateHealth(sheet),
            "Resolve" => derived.CalculateResolve(sheet),
            _ => costs.TotalCost(sheet)
        };

        // The test can only bite if the two disagree. Asserting that first turns a sample that
        // drifted into a failure here rather than into a test that passes for no reason.
        Assert.NotEqual(Stat(ctx.Session.Sheet), Stat(recorded));

        var page = Play(ctx, Cheap);
        ShowAll(page);

        var sheet = page.Find(".sheet");

        Assert.Contains(recorded.Name, sheet.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(ctx.Session.Sheet.Name, sheet.TextContent, StringComparison.Ordinal);

        var block = page.FindAll(".sheet .stat-block")
            .Single(b => b.QuerySelector(".label")!.TextContent.Trim() == label);

        Assert.Equal(
            Stat(recorded).ToString(System.Globalization.CultureInfo.InvariantCulture),
            block.QuerySelector(".value")!.TextContent.Trim());
    }

    /// <summary>
    /// And every Power on that sheet prints its <em>own</em> effective rank.
    ///
    /// <para>The four boxes were not the only figures able to read the wrong character: moving
    /// the sheet's rank calculation back to the visitor's own sheet gives every Power on Vera
    /// Nunn's sheet an extra rank, because both of hers take a baseline from an Ability that
    /// would then belong to somebody else. A rank is what a player rolls, so a wrong one is
    /// worse than a wrong cost.</para>
    /// </summary>
    [Fact]
    public void EveryPowerOnTheSheetPrintsTheRecordedCharactersOwnRank()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();
        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        var recorded = Conversation(ctx, Cheap).FinalCharacter!;

        // Both of her Powers move if the wrong character is read. Asserting that keeps this
        // test honest if she ever changes into one whose Powers would not.
        Assert.All(recorded.SelectedPowers, p => Assert.NotEqual(
            derived.GetEffectiveRank(p, ctx.Session.Sheet),
            derived.GetEffectiveRank(p, recorded)));

        var page = Play(ctx, Cheap);
        ShowAll(page);

        var entries = page.FindAll(".sheet .power-entry .head").Select(e => e.TextContent).ToList();

        foreach (var power in recorded.SelectedPowers)
        {
            var name = rules.GetPower(power.PowerId)!.Name;
            var rank = derived.GetEffectiveRank(power, recorded)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Name and rank read as one string, the way a browser concatenates them — which is
            // also what catches the separator going missing and printing "Armor8d".
            Assert.Contains(entries, e => e.Contains($"{name} {rank}d", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// <b>A sheet printed for somebody else's character reads exactly as it would if that
    /// character were the visitor's own.</b>
    ///
    /// <para>This is the one assertion on this surface that does not go stale. The two tests
    /// above name the figures they check, and everything they do not name was free: an audit
    /// moved <c>Tier</c>, the budget sub-line, the Quote, the Motivation, the Description, the
    /// Connections and the stand-in rank from the printed character to the visitor's own, and
    /// all seven mutations were green. Naming seven more fields would only move the boundary —
    /// the eighth field somebody adds is unguarded again the day it is added.</para>
    ///
    /// <para>So it compares two renderings of the <em>same</em> character: one where the
    /// session holds it, one where the session holds somebody else entirely and it arrives as
    /// a parameter. Every read of <c>Session.Sheet</c> that should have been a read of the
    /// parameter is a difference between the two, whatever field it is in.</para>
    ///
    /// <para>The pairs are chosen so the two characters differ in the things a sheet prints.
    /// One deliberately crosses tiers — only one recorded character is not Standard, and the
    /// tier is what the masthead, the colophon and the budget sub-line are drawn from.</para>
    /// </summary>
    [Theory]
    [InlineData(Cheap, OurHero)]
    [InlineData(OurHero, Cheap)]
    [InlineData(Villain, OurVillain)]
    [InlineData(DidNotFit, OurHero)]
    public void ASheetPrintedForSomebodyElsesCharacterReadsExactlyAsTheirOwnWould(
        string printed, string visitors)
    {
        // Both renderings pass ShowBudget explicitly. It is the one thing on this sheet that
        // is genuinely allowed to come from outside the character — whether a recorded
        // character is a Villain has nothing to do with the palette the visitor is wearing —
        // so leaving it to default would make the two renderings differ for a legitimate
        // reason and hide every illegitimate one behind it.
        static string AsTheirOwn(RenderContext ctx, CharacterSheet sheet)
        {
            ctx.Session.Restore(sheet, SheetMode.Hero);
            return ctx.Render<SheetView>(p => p.Add(s => s.ShowBudget, true))
                .Find(".sheet").TextContent;
        }

        using var ctx = new RenderContext();
        var subject = Character(ctx, printed);
        var mine = Character(ctx, visitors);

        var expected = AsTheirOwn(ctx, subject);

        // The bite guard. If the two characters printed the same page the comparison below
        // would hold however thoroughly the component read the wrong one.
        Assert.NotEqual(expected, AsTheirOwn(ctx, mine));

        // And now the visitor's character is the one in the session, with the subject passed
        // in — which is exactly what the replay does. Asserted rather than left to the order
        // of the two calls above: with the subject still in the session this whole comparison
        // would pass by rendering the same thing twice.
        Assert.Same(mine, ctx.Session.Sheet);

        var actual = ctx.Render<SheetView>(p => p
                .Add(s => s.Character, subject)
                .Add(s => s.ShowBudget, true))
            .Find(".sheet").TextContent;

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The one thing on a printed sheet the pairing above cannot exercise: a rankless Power's
    /// stand-in rank, which no recorded character has.
    ///
    /// <para>A rankless Power has no rank of its own, but it is not rankless when another
    /// Power acts on it — its Source names an Ability that stands in, and the sheet prints
    /// what that comes to. Read off the wrong character it is a number a player would act on
    /// the moment somebody Drained it.</para>
    /// </summary>
    [Fact]
    public void ARanklessPowerPrintsTheStandInRankOfTheCharacterOnThePage()
    {
        using var ctx = new RenderContext();
        var rules = ctx.Services.GetRequiredService<RulesRepository>();
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();

        // Printed: the Hero sample, whose Communications is rankless and Tech-Sourced.
        // In the session: a recorded character, whose Toughness is a different number.
        var printed = SampleCharacters.Hero();
        ctx.Session.Restore(Character(ctx, Cheap), SheetMode.Hero);

        var rankless = printed.SelectedPowers
            .Where(p => rules.GetPower(p.PowerId) is { RankType: "default" or "special" })
            .ToList();

        Assert.NotEmpty(rankless);

        var sheet = ctx.Render<SheetView>(p => p.Add(s => s.Character, printed))
            .Find(".sheet").TextContent;

        foreach (var power in rankless)
        {
            var theirs = derived.GetRankAgainstPowers(power, printed);
            var visitors = derived.GetRankAgainstPowers(power, ctx.Session.Sheet);

            Assert.NotEqual(theirs, visitors);

            var ability = rules.GetAbility(rules.GetSource(power.SourceId!)!.DefaultRankAbility)!.Name;

            Assert.Contains($"Against other Powers: {ability} {theirs}d", sheet, StringComparison.Ordinal);
            Assert.DoesNotContain($"Against other Powers: {ability} {visitors}d", sheet, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>A recorded character is measured against a budget if and only if <em>they</em> have
    /// one</b> — which has nothing to do with the palette the visitor happens to be wearing.
    ///
    /// <para>Ch.9 gives a Villain no Hero Point budget, so the Conductor's fourth box carries
    /// what he cost and nothing to measure it against. Dropping the flag the replay passes
    /// falls back to the app's own mode, so a visitor in Hero colours read the Conductor's
    /// sheet as "Hero Points … of 125" — a budget the rulebook says he does not have.
    /// <see cref="AVillainIsNotCalledIllegalForHavingNoBudget"/> makes that claim for the
    /// verdict panel and never for the sheet.</para>
    ///
    /// <para>Both directions, because the fallback is right half the time by accident: a Hero
    /// recording read by a visitor in Villain colours has to keep its budget.</para>
    /// </summary>
    [Theory]
    [InlineData(Villain, SheetMode.Hero, "Points Spent", null)]
    [InlineData(Cheap, SheetMode.Villain, "Hero Points", "of 75")]
    public void ARecordedCharacterIsMeasuredAgainstTheirOwnBudgetAndNotTheVisitors(
        string id, SheetMode visitorsMode, string label, string? sub)
    {
        using var ctx = new RenderContext().With(visitorsMode);
        var page = Play(ctx, id);
        ShowAll(page);

        var block = page.FindAll(".sheet .stat-block")
            .Single(b => b.QuerySelector(".label")!.TextContent.Trim() is "Hero Points" or "Points Spent");

        Assert.Equal(label, block.QuerySelector(".label")!.TextContent.Trim());
        Assert.Equal(sub, block.QuerySelector(".sub")?.TextContent.Trim());
    }

    /// <summary>
    /// <b>A character the engine cannot price still renders.</b>
    ///
    /// <para>The sheet's Perk and Gear boxes called the engine bare while every other cost on
    /// it went through a guard, and the engine throws rather than guessing on an id it does not
    /// have. A throw during render in the browser takes down the whole app rather than one box,
    /// and it reaches a character restored from an older build as much as a recorded one —
    /// which is why this builds the character by hand rather than going through the replay,
    /// where none of the four has a Perk or a piece of gear to break.</para>
    /// </summary>
    [Fact]
    public void ASheetWithAnIdTheRulesDoNotHaveStillRenders()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        ctx.Session.Sheet.Perks.Add(new SelectedPerk("no_such_perk"));
        ctx.Session.Sheet.Gear.Add(new SelectedGear("Something odd")
        {
            Features = [new SelectedGearFeature("no_such_feature")]
        });

        var page = ctx.Render<SheetView>();
        var sheet = page.Find(".sheet").TextContent;

        // It rendered at all, which is most of the assertion — and it kept the parts it could
        // still answer for rather than dropping the line, which is the other part.
        Assert.Contains("no_such_perk", sheet, StringComparison.Ordinal);
        Assert.Contains("Something odd", sheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The shell's budget bar is the visitor's own character and does not belong above a
    /// recording of somebody else's.</b>
    ///
    /// <para>Both are six labelled figures in the same format, so a visitor part-way through
    /// their own build met "of 125 spent, remaining" directly over a recorded character costed
    /// at something else, with nothing saying whose was whose. Worst on the Villain recording,
    /// which shows no budget of its own on purpose: the only budget on the screen belonged to
    /// a different character entirely. Rendered through the layout, because the bar is in the
    /// shell and the page under it cannot see it.</para>
    /// </summary>
    /// <param name="address">
    /// The capitalised form is not decoration. Blazor's route matching is case-insensitive, so
    /// <c>/Replay/…</c> serves the recording; an ordinal comparison in the shell served it with
    /// the budget bar over the top, reachable by anybody who capitalised a shared link.
    /// </param>
    [Theory]
    [InlineData("replay/the-conductor")]
    [InlineData("Replay/the-conductor")]
    [InlineData("replay")]
    public void TheVisitorsOwnBudgetBarIsNotShownOverARecordedCharacter(string address)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        // The wizard first, so a shell that never draws the bar at all cannot pass the half of
        // this that matters.
        nav.NavigateTo("characteristics");
        var shell = ctx.Render<MainLayout>(p => p.Add(l => l.Body, _ => { }));
        Assert.Single(shell.FindAll(".budget"));

        // The same rendered shell is navigated rather than a fresh one, because the layout has
        // to notice the move on its own — and then back, because a bar that never returns is
        // the same bug facing the other way.
        nav.NavigateTo(address);
        Assert.Empty(shell.FindAll(".budget"));

        nav.NavigateTo("characteristics");
        Assert.Single(shell.FindAll(".budget"));
    }

    // ── The hand-off ────────────────────────────────────────────────────────────

    /// <summary>
    /// The same bargain the tier page strikes over its samples: the character is kept in this
    /// browser between visits, so opening a recorded one over it destroys work written down
    /// nowhere else. It asks first, and until it is answered nothing has moved.
    /// </summary>
    [Fact]
    public void OpeningARecordedCharacterOverOneInProgressAsksFirst()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var before = ctx.Session.Sheet.Name;

        var page = Play(ctx, Cheap);
        ShowAll(page);
        Button(page, "Open ").Click();

        Assert.Equal(before, ctx.Session.Sheet.Name);
        Assert.Contains("Keep what I have", Text(page), StringComparison.Ordinal);
    }

    /// <summary>
    /// And on an untouched sheet there is nothing to ask about, so it opens on one click — the
    /// visit where somebody is most likely to want a recorded character and least likely to
    /// have anything at stake.
    /// </summary>
    [Fact]
    public void OnAnEmptySheetTheRecordedCharacterOpensOnOneClick()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, Cheap);
        ShowAll(page);

        Button(page, "Open ").Click();

        var recorded = Conversation(ctx, Cheap).FinalCharacter!;
        Assert.Equal(recorded.Name, ctx.Session.Sheet.Name);
        Assert.Equal(recorded.SelectedPowers.Count, ctx.Session.Sheet.SelectedPowers.Count);
    }

    /// <summary>
    /// <b>What is handed over is a copy.</b> The library is read once at startup and shared by
    /// every visit, so handing the instance itself over would let the first edit rewrite the
    /// recording — after which the replay would be playing back a character somebody had
    /// changed, and there is nothing on the page that would say so.
    /// </summary>
    [Fact]
    public void EditingWhatWasHandedOverDoesNotChangeTheRecording()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, Cheap);
        ShowAll(page);
        Button(page, "Open ").Click();

        ctx.Session.Sheet.Name = "Somebody else";
        ctx.Session.Sheet.SelectedPowers.Clear();

        var recorded = Conversation(ctx, Cheap).FinalCharacter!;
        Assert.NotEqual("Somebody else", recorded.Name);
        Assert.NotEmpty(recorded.SelectedPowers);
    }

    /// <summary>
    /// A recording built as a Villain takes the palette with it, the way loading the Villain
    /// sample does. It is presentation and nothing else — the sheet is built by identical
    /// rules — but arriving at a Villain in Hero colours reads as the wrong character.
    /// </summary>
    [Fact]
    public void OpeningAVillainTakesThePaletteWithIt()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, Villain);
        ShowAll(page);

        Button(page, "Open ").Click();

        Assert.Equal(SheetMode.Villain, ctx.Session.Mode);
        Assert.Contains(ctx.JSInterop.Invocations,
            i => i.Identifier == "ppSetMode" && i.Arguments.Contains("villain"));
    }

    // ── Addresses ───────────────────────────────────────────────────────────────

    /// <summary>
    /// An address naming no recording says so rather than rendering an empty page — a shared
    /// link outlives whatever it pointed at.
    /// </summary>
    [Fact]
    public void AnAddressThatNamesNoRecordingSaysSo()
    {
        using var ctx = new RenderContext();
        var page = ctx.Render<ReplayConversation>(p => p.Add(c => c.Id, "no-such-thing"));

        Assert.Contains("No such recording", Text(page), StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".replay-turn"));
    }

    /// <summary>
    /// <b>And an address reaches its recording whatever case it was typed in.</b>
    ///
    /// <para>Blazor's route matching is case-insensitive, so <c>/Replay/The-Conductor</c>
    /// reaches this page perfectly happily; only the lookup that follows can refuse it. An
    /// ordinal comparison there answers a link somebody capitalised — or that an email client
    /// sentence-cased for them — with "that address does not name one of the recorded
    /// conversations", which is a confident lie about a link that is fine.</para>
    ///
    /// <para>This is the identical bug
    /// <see cref="TheVisitorsOwnBudgetBarIsNotShownOverARecordedCharacter"/> guards for the
    /// shell, on the sibling call site, which had no test of its own. The lower-case row is
    /// the control: without it, a lookup that matched nothing at all would fail this the same
    /// way and say nothing about case.</para>
    /// </summary>
    [Theory]
    [InlineData(Villain)]
    [InlineData("The-Conductor")]
    [InlineData("VERA-NUNN")]
    [InlineData("Sheet-Lightning")]
    public void AnAddressReachesItsRecordingWhateverCaseItWasTypedIn(string id)
    {
        using var ctx = new RenderContext();
        var page = ctx.Render<ReplayConversation>(p => p.Add(c => c.Id, id));

        Assert.DoesNotContain("No such recording", Text(page), StringComparison.Ordinal);
        Assert.NotEmpty(page.FindAll(".replay-turn"));
    }

    /// <summary>
    /// <b>A recording that could not be loaded is not a bad link, and must not be reported as
    /// one.</b>
    ///
    /// <para>Both states reach the same branch — the library cannot find the id — and the app
    /// answered both with "that address does not name one of the recorded conversations". So a
    /// deploy that failed to ship the transcripts told everyone following a perfectly good
    /// shared link that they had typed it wrong, while the actual reason sat unread on the
    /// library. The two are told apart now, and both pages print the reason.</para>
    /// </summary>
    [Fact]
    public void RecordingsThatCouldNotBeLoadedAreNotReportedAsABadAddress()
    {
        const string reason = "the transcripts answered 404";
        using var ctx = new RenderContext(reason);

        var conversation = ctx.Render<ReplayConversation>(p => p.Add(c => c.Id, DidNotFit));
        var text = Text(conversation);

        Assert.DoesNotContain("No such recording", text, StringComparison.Ordinal);
        Assert.Contains(reason, text, StringComparison.Ordinal);

        // And the list, which is where somebody who did not follow a link arrives.
        Assert.Contains(reason, Text(ctx.Render<Replay>()), StringComparison.Ordinal);
    }

    /// <summary>
    /// Blazor reuses this component across a navigation between two addresses that differ only
    /// in the id, so without a reset the second recording opens part-read — at whichever line
    /// the visitor had reached in the first.
    /// </summary>
    [Fact]
    public void OpeningASecondRecordingStartsItAtTheBeginning()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, DidNotFit);
        ShowAll(page);

        page.Render(p => p.Add(c => c.Id, Cheap));

        Assert.Single(page.FindAll(".replay-turn"));
    }

    /// <summary>
    /// The list offers every recording, by the name each carries, and says what each shows.
    /// A card with no blurb is four indistinguishable buttons.
    /// </summary>
    [Fact]
    public void TheListOffersEveryRecordingWithSomethingToTellThemApart()
    {
        using var ctx = new RenderContext();
        var page = ctx.Render<Replay>();
        var text = Text(page);

        foreach (var conversation in ctx.Services.GetRequiredService<ReplayLibrary>().Conversations)
        {
            Assert.Contains(conversation.Title, text, StringComparison.Ordinal);
            Assert.Contains(conversation.Blurb, text, StringComparison.Ordinal);
        }
    }
}
