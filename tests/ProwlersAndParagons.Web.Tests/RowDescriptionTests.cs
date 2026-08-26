using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// What an option or a Trait actually is, available where it is being chosen.
///
/// <para><b>The lists said what things cost and never what they were.</b> A Powers row printed a
/// name, a stat line and a category; an Ability row printed a name, a rank and the rulebook's word
/// for it. Neither said what Presence covers or what Blending does, and the descriptions were in
/// the rules data the whole time with nothing showing them.</para>
///
/// <para>The rendered half is here. The half whose whole substance is in the stylesheet — that the
/// tip is hidden rather than absent, and appears on hover and on focus — is in
/// <c>WebPresentationTests</c>, because emptying that rule leaves every assertion below passing.
/// </para>
/// </summary>
public sealed class RowDescriptionTests
{
    /// <summary>
    /// A Power's row carries what the Power does, and the description its row points at is really
    /// in the document.
    ///
    /// <para><b>A dangling <c>aria-describedby</c> is worse than none</b>: it promises a screen
    /// reader a sentence and hands it nothing, and every assertion about the attribute's presence
    /// passes either way. So the target is resolved rather than the attribute counted.</para>
    /// </summary>
    [Fact]
    public void APowersRowSaysWhatThePowerDoes()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var page = ctx.Render<PowersTab>();
        var rows = page.FindAll(".option[aria-describedby]");

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            var id = row.GetAttribute("aria-describedby");
            Assert.False(string.IsNullOrWhiteSpace(id));

            var described = page.FindAll($"#{id}");
            Assert.Single(described);
            Assert.False(string.IsNullOrWhiteSpace(described[0].TextContent),
                $"the row for {id} names an empty description, which promises a sentence "
                + "and delivers nothing.");
        }
    }

    /// <summary>
    /// The description shown is the one the rules data carries, not a repeat of the stat line.
    ///
    /// <para>Named against a real entry, because a test that only checked "some text is present"
    /// would pass with the name written into the tip twice.</para>
    /// </summary>
    [Fact]
    public void TheDescriptionIsThePowersOwn()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var blending = ctx.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "blending");

        var page = ctx.Render<PowersTab>();
        var tip = page.FindAll(".row-tip").Select(e => e.TextContent).ToList();

        Assert.Contains(blending.Description, tip, StringComparer.Ordinal);
    }

    /// <summary>
    /// An Ability's name is a real button, so the description is reachable by keyboard and by
    /// touch rather than by a mouse alone.
    ///
    /// <para><b>A <c>span</c> with a mouse handler is a tooltip only for people using a
    /// mouse</b>, which is the rule <c>Tooltip</c> already exists to hold, and the obvious
    /// spelling here is exactly that span.</para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATraitsNameIsARealButton(bool abilities)
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var page = abilities
            ? (IRenderedComponent<Microsoft.AspNetCore.Components.IComponent>)ctx.Render<AbilitiesTab>()
            : ctx.Render<TalentsTab>();

        var terms = page.FindAll(".trait-term");

        Assert.NotEmpty(terms);
        Assert.All(terms, t => Assert.Equal("BUTTON", t.TagName));

        // ...and each names a description that is in the document.
        foreach (var term in terms)
        {
            var id = term.GetAttribute("aria-describedby");
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.Single(page.FindAll($"#{id}"));
        }
    }

    /// <summary>
    /// Every Ability and every Talent has one, not merely some of them.
    ///
    /// <para>Ch.2 floors both at 1d, so a character always has all six and all twelve, and a row
    /// that silently had no description would be a row whose word is never explained.</para>
    /// </summary>
    [Fact]
    public void EveryAbilityAndTalentCarriesOne()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var rules = ctx.Services.GetRequiredService<RulesRepository>();

        Assert.Equal(rules.Abilities.Count, ctx.Render<AbilitiesTab>().FindAll(".trait-term").Count);
        Assert.Equal(rules.Talents.Count, ctx.Render<TalentsTab>().FindAll(".trait-term").Count);
    }

    /// <summary>
    /// The description is in the document whether or not anybody has hovered.
    ///
    /// <para><b>Rendering it only while open would take the <c>aria-describedby</c> target with
    /// it</b> — the reference would dangle for everybody not using a pointer, which is the
    /// population it exists for. Same rule <c>Tooltip</c> records, and the same reason.</para>
    /// </summary>
    [Fact]
    public void TheDescriptionIsPresentWithoutHovering()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var page = ctx.Render<PowersTab>();

        // Nothing has been hovered or focused in a bUnit render, so anything found here is
        // unconditionally present.
        Assert.NotEmpty(page.FindAll(".sr-only[id^='opt-']"));
    }

    /// <summary>
    /// Two renders of the same list produce the same ids.
    ///
    /// <para><b>A generated identifier differs every render</b> and would break the replay's
    /// strongest guard, which renders one character twice and requires the two pages to be
    /// identical — it would report a difference that is not one. Derived from the name instead,
    /// which is the trap <c>Tooltip</c> hit first.</para>
    /// </summary>
    [Fact]
    public void TheIdsAreDerivedFromTheNameAndNotGenerated()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        static IReadOnlyList<string> Ids(IRenderedComponent<PowersTab> page) =>
            [.. page.FindAll(".option[aria-describedby]")
                .Select(e => e.GetAttribute("aria-describedby") ?? "")];

        var first = Ids(ctx.Render<PowersTab>());
        var second = Ids(ctx.Render<PowersTab>());

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// The visible copy is hidden from assistive technology, and the read copy is hidden from the
    /// screen.
    ///
    /// <para>Without the split the sentence is announced twice — once as the row's description and
    /// again as text inside the row's own accessible name.</para>
    /// </summary>
    [Fact]
    public void TheSentenceIsAnnouncedOnceAndShownOnce()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var page = ctx.Render<PowersTab>();

        Assert.All(page.FindAll(".row-tip"),
            tip => Assert.Equal("true", tip.GetAttribute("aria-hidden")));

        Assert.All(page.FindAll(".sr-only[id^='opt-']"),
            read => Assert.Null(read.GetAttribute("aria-hidden")));
    }

    /// <summary>
    /// A row whose second line already carries the description does not carry it twice.
    ///
    /// <para>Perks, Flaws and both kinds of Pro and Con print their description as the row's own
    /// caveat. Adding a tip there would say the same sentence in two places on one row, and the
    /// tip would cover the row below while saying nothing new.</para>
    ///
    /// <para><b>The fixture chooses a Perk and a Flaw, and it did not used to.</b> It set the
    /// tier and nothing else, so both tabs rendered with an empty <em>chosen</em> list and the
    /// assertion only ever reached the pickable <c>OptionRow</c>s — while the sentence above
    /// claims a rule about the rows of both lists. A tip appearing on a chosen row would have
    /// gone unnoticed. Found by a fresh agent reading this file for an unrelated reason, which
    /// is the failure this repository has shipped before in several spellings: a guard that
    /// passes for a reason narrower than the one it states.</para>
    /// </summary>
    [Fact]
    public void ARowThatAlreadyPrintsItsDescriptionHasNoTip()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.Perks.Add(new SelectedPerk("contacts"));
        ctx.Session.Sheet.Flaws.Add(new SelectedFlaw("amnesia"));

        var perks = ctx.Render<PerksTab>();
        var flaws = ctx.Render<FlawsTab>();

        // The positive control on the fixture: the chosen rows have to actually be on the page,
        // or every assertion below is satisfied by a list that rendered nothing.
        Assert.NotEmpty(perks.FindAll(".chosen-row"));
        Assert.NotEmpty(flaws.FindAll(".chosen-row"));

        Assert.Empty(perks.FindAll(".row-tip"));
        Assert.Empty(flaws.FindAll(".row-tip"));

        // The control: the list that does not print one still has them, so this is a statement
        // about these two lists rather than about the feature having been removed.
        Assert.NotEmpty(ctx.Render<PowersTab>().FindAll(".row-tip"));
    }
}
