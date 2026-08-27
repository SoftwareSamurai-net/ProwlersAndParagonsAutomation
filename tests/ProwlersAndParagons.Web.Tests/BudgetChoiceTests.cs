using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Hero Point limit, as the two cards it became.
///
/// <para><b>It was one button in a full-width panel and had no rendering coverage at all.</b>
/// That button's label flipped between naming the action and naming the state — off it read
/// "Hold me to the tier's budget", which is what pressing it would do, and on it read "Building
/// without a limit", which is what was already happening — so a glance could not tell which of
/// the two it was reporting. Two cards, both always visible and exactly one selected, is what
/// removes the ambiguity, and every assertion here is about a property that pair has and the
/// single button did not.</para>
/// </summary>
public sealed class BudgetChoiceTests
{
    private const string Cards = ".budget-choice .option";

    /// <summary>
    /// Both cards are on screen in both states, and exactly one of them is pressed.
    ///
    /// <para><b>Both halves matter and neither implies the other.</b> A pair where the unselected
    /// card is hidden is the flipping label again in another spelling; a pair where both or
    /// neither is pressed is a group that reports no state at all.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactlyOneOfTheTwoCardsIsPressed(bool unlimited)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Session.UnlimitedBudget = unlimited;

        var cards = ctx.Render<ChooseTier>().FindAll(Cards);

        Assert.Equal(2, cards.Count);
        Assert.Single(cards, c => c.GetAttribute("aria-pressed") == "true");
        Assert.Single(cards, c => c.GetAttribute("aria-pressed") == "false");

        // The pressed one is the state the character is actually in, not merely *a* consistent
        // pair — which every assertion above is satisfied by with the two cards swapped.
        var pressed = cards.Single(c => c.GetAttribute("aria-pressed") == "true");
        Assert.Contains(
            unlimited ? "without a limit" : "tier's budget",
            pressed.TextContent,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>aria-pressed</c> is a string on both cards, in both states.
    ///
    /// <para><b>Blazor drops a false bool attribute and renders a true one as
    /// <c>aria-pressed=""</c></b>, and empty is invalid ARIA that assistive technology reads as
    /// <i>not</i> pressed — so the natural spelling announces the opposite of the state in both
    /// directions. This is the assertion that fails when somebody tidies the ternary away.</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothCardsWriteAriaPressedAsAString(bool unlimited)
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Session.UnlimitedBudget = unlimited;

        var cards = ctx.Render<ChooseTier>().FindAll(Cards);

        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.Contains(
            c.GetAttribute("aria-pressed"), ["true", "false"], StringComparer.Ordinal));
    }

    /// <summary>
    /// Both labels name the same kind of thing as each other.
    ///
    /// <para>The defect the pair exists to fix was one label naming an action and the other
    /// naming a state, so the shape of the words is the fix and not a matter of taste. Asserted
    /// as the parallel construction they are written in — two states, both beginning
    /// "Building" — because a rule about English is only checkable where the two are alike in a
    /// way a machine can read.</para>
    /// </summary>
    [Fact]
    public void BothLabelsNameAStateRatherThanOneNamingAnAction()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);

        var names = ctx.Render<ChooseTier>()
            .FindAll(Cards + " .name")
            .Select(n => n.TextContent.Trim())
            .ToList();

        Assert.Equal(2, names.Count);
        Assert.All(names, n => Assert.StartsWith("Building", n, StringComparison.Ordinal));

        // And they are two different states rather than one written twice.
        Assert.Equal(2, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Pressing a card moves the flag and nothing else on the character.
    ///
    /// <para><b>Compared as the whole exported character rather than field by field</b>, because
    /// the property is "nothing else moved" and a list of fields to check is a list that goes
    /// stale the first time somebody adds one. The engine is never told about the flag, so its
    /// findings do not move either — which is the other half asserted here.</para>
    /// </summary>
    [Fact]
    public void ChoosingACardChangesTheFlagAndNothingElse()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var validator = ctx.Services.GetRequiredService<CharacterValidator>();

        var before = CharacterSheetJson.Write(ctx.Session.Sheet);
        var spentBefore = ctx.Session.Costs.TotalCost(ctx.Session.Sheet);
        var foundBefore = Codes(validator);

        var page = ctx.Render<ChooseTier>();
        page.FindAll(Cards)[1].Click();

        // The positive control: the click did something at all.
        Assert.True(ctx.Session.UnlimitedBudget);

        Assert.Equal(spentBefore, ctx.Session.Costs.TotalCost(ctx.Session.Sheet));
        Assert.Equal(foundBefore, Codes(validator));

        // Everything the character is, with the one flag put back as it was: byte for byte what
        // it was before the click.
        ctx.Session.Sheet.UnlimitedBudget = false;
        Assert.Equal(before, CharacterSheetJson.Write(ctx.Session.Sheet), StringComparer.Ordinal);

        List<string> Codes(CharacterValidator v) =>
            v.Validate(ctx.Session.Sheet).Issues.Select(i => i.Code).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Choosing the card already chosen does nothing at all.
    ///
    /// <para>A pair of cards is not a toggle wearing two hats: pressing the state you are already
    /// in is not a way to leave it. Read off the session's version counter, which is what the
    /// autosave and both undo buffers key on — an edit recorded here would close an undo window
    /// somebody was still inside.</para>
    /// </summary>
    [Fact]
    public void ChoosingTheCardAlreadyChosenIsNotAnEdit()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        var page = ctx.Render<ChooseTier>();

        var version = ctx.Session.Version;
        page.FindAll(Cards)[0].Click();

        Assert.False(ctx.Session.UnlimitedBudget);
        Assert.Equal(version, ctx.Session.Version);

        // The control: the *other* card is an edit, so the counter above is being read from a
        // session that does move.
        page.FindAll(Cards)[1].Click();
        Assert.NotEqual(version, ctx.Session.Version);
    }

    /// <summary>
    /// A tier is still picked without a limit, and the Trait Cap still applies.
    ///
    /// <para><b>This is why the pair is its own group and not a seventh and eighth tier card.</b>
    /// The two questions are orthogonal: switching the budget off leaves the six tiers to choose
    /// between, leaves the step refusing to go on until one is chosen, and leaves the cap that
    /// tier sets in force. A reader who reads the pair as "the tier no longer matters" has been
    /// told something false.</para>
    /// </summary>
    [Fact]
    public void ATierIsStillChosenWithoutALimitAndTheCapStillApplies()
    {
        using var ctx = new RenderContext();
        ctx.Session.UnlimitedBudget = true;

        var page = ctx.Render<ChooseTier>();

        // Six tiers, still there and still unanswered, with the way on refused.
        Assert.Equal(ctx.Session.Rules.Tiers.Count, page.FindAll(".options.cards .option").Count - 2);
        Assert.Null(ctx.Session.Sheet.SelectedTierId);

        var next = page.Find(".nav-buttons a.primary");
        Assert.Contains("disabled", next.ClassName ?? "", StringComparison.Ordinal);
        Assert.False(next.HasAttribute("href"));

        // And the cap the chosen tier sets is enforced in the sandbox exactly as it is outside
        // it — the engine is never told about the flag at all.
        var tier = ctx.Session.Rules.Tiers[0];
        ctx.Session.Sheet.SelectedTierId = tier.Id;
        ctx.Session.Sheet.AbilityRanks["might"] = tier.TraitCapRank + 1;

        var issues = ctx.Services.GetRequiredService<CharacterValidator>()
            .Validate(ctx.Session.Sheet).Issues;

        Assert.Contains(issues, i => i.Code == "TRAIT_ABOVE_CAP");
    }
}
