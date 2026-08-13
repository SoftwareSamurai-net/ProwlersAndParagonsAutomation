using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
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
        var stats = page.FindAll(".replay-verdict").First()
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
    /// </summary>
    [Fact]
    public void AVillainIsNotCalledIllegalForHavingNoBudget()
    {
        using var ctx = new RenderContext();
        var page = Play(ctx, Villain);
        ShowAll(page);

        Assert.Empty(page.FindAll(".verdict"));

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
    /// <para><b>All three figures, because asserting one of three was not enough.</b> An
    /// adversarial pass put Health and Resolve back on the visitor's own character and left
    /// Edge alone; the suite stayed green, under a comment naming all three.</para>
    /// </summary>
    [Theory]
    [InlineData("Edge")]
    [InlineData("Health")]
    [InlineData("Resolve")]
    public void TheSheetAtTheEndCarriesTheRecordedCharactersOwnFigures(string label)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var derived = ctx.Services.GetRequiredService<DerivedStatsCalculator>();

        var recorded = Conversation(ctx, Cheap).FinalCharacter!;

        int Figure(CharacterSheet sheet) => label switch
        {
            "Edge" => derived.CalculateEdge(sheet),
            "Health" => derived.CalculateHealth(sheet),
            _ => derived.CalculateResolve(sheet)
        };

        // The test can only bite if the two disagree. Asserting that first turns a sample that
        // drifted into a failure here rather than into a test that passes for no reason.
        Assert.NotEqual(Figure(ctx.Session.Sheet), Figure(recorded));

        var page = Play(ctx, Cheap);
        ShowAll(page);

        var sheet = page.Find(".sheet");

        Assert.Contains(recorded.Name, sheet.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(ctx.Session.Sheet.Name, sheet.TextContent, StringComparison.Ordinal);

        var block = page.FindAll(".sheet .stat-block")
            .Single(b => b.TextContent.Contains(label, StringComparison.Ordinal));

        Assert.Contains(
            Figure(recorded).ToString(System.Globalization.CultureInfo.InvariantCulture),
            block.TextContent,
            StringComparison.Ordinal);
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
    [Fact]
    public void TheVisitorsOwnBudgetBarIsNotShownOverARecordedCharacter()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        // The wizard, where it belongs.
        nav.NavigateTo("characteristics");
        Assert.Single(ctx.Render<MainLayout>(p => p.Add(l => l.Body, b => { })).FindAll(".budget"));

        // And a recording, where it does not.
        nav.NavigateTo($"replay/{Villain}");
        Assert.Empty(ctx.Render<MainLayout>(p => p.Add(l => l.Body, b => { })).FindAll(".budget"));
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
