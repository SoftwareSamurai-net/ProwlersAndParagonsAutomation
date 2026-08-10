using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The control that records a Trait's Source.
///
/// <para>It had no test at all, and an adversarial pass showed what that cost: storing the
/// default instead of removing the entry, dropping the change notification, and wrapping the
/// whole picker in <c>@if (false)</c> so it never rendered — all three left the suite
/// green. The engine filters a stored default away, which is good defence in depth and
/// exactly why nothing downstream noticed the editor was not holding up its half.</para>
/// </summary>
public sealed class TraitSourcePickerTests
{
    private static IRenderedComponent<TraitSourcePicker> Picker(RenderContext ctx, bool abilities = true)
    {
        var sheet = ctx.Session.Sheet;

        return ctx.Render<TraitSourcePicker>(p => p
            .Add(c => c.Title, "Sources")
            .Add(c => c.Scope, abilities ? "ability" : "talent")
            .Add(c => c.Traits, abilities
                ? ctx.Session.Rules.Abilities.Select(a => (a.Id, a.Name)).ToList()
                : ctx.Session.Rules.Talents.Select(t => (t.Id, t.Name)).ToList())
            .Add(c => c.Sources, abilities ? sheet.AbilitySources : sheet.TalentSources)
            .Add(c => c.DefaultSourceId, abilities
                ? SourceGrouping.DefaultAbilitySourceId
                : SourceGrouping.DefaultTalentSourceId));
    }

    /// <summary>
    /// Six Sources, six options — the default is offered once, as the blank entry, and is
    /// not repeated by name below it. Offering it twice gave two controls for one state
    /// whose spellings did different things: the blank removed the entry, the named one
    /// stored it.
    /// </summary>
    [Fact]
    public void TheDefaultIsOfferedOnceAndNotRepeatedByName()
    {
        using var ctx = new RenderContext();
        var options = Picker(ctx).Find("#ability-src-might").QuerySelectorAll("option");

        Assert.Equal(ctx.Session.Rules.Sources.Count, options.Length);

        var blank = options[0];
        Assert.Equal("", blank.GetAttribute("value"));
        Assert.Contains("Innate", blank.TextContent, StringComparison.Ordinal);
        Assert.Contains("default", blank.TextContent, StringComparison.Ordinal);

        // The default's id appears on no other option, so it cannot be chosen a second way.
        Assert.DoesNotContain(options.Skip(1), o =>
            o.GetAttribute("value") == SourceGrouping.DefaultAbilitySourceId);
    }

    /// <summary>Talents take the other default, and the panel says so rather than saying "Innate".</summary>
    [Fact]
    public void TheTalentPickerOffersTheTrainedDefault()
    {
        using var ctx = new RenderContext();
        var options = Picker(ctx, abilities: false).Find("#talent-src-academics").QuerySelectorAll("option");

        Assert.Contains("Trained", options[0].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(options.Skip(1), o =>
            o.GetAttribute("value") == SourceGrouping.DefaultTalentSourceId);
    }

    /// <summary>
    /// Choosing a Source records it, and the sheet then prints the line. Asserted through
    /// the grouping rather than by reading the dictionary back, because printing is the only
    /// reason the entry exists.
    /// </summary>
    [Fact]
    public void ChoosingASourceRecordsItAndTheSheetPrintsIt()
    {
        using var ctx = new RenderContext();
        var picker = Picker(ctx);

        picker.Find("#ability-src-might").Change("tech");

        Assert.Equal("tech", ctx.Session.Sheet.AbilitySources["might"]);
        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Might)"],
            ctx.Session.Grouping.GroupBySource(ctx.Session.Sheet).Single().TraitLines);
    }

    /// <summary>
    /// Going back to the default removes the entry rather than storing the default's id.
    /// A stored default prints nothing either way — the engine filters it — but it would
    /// persist to local storage and read back as a Source the sheet does not honour.
    /// </summary>
    [Fact]
    public void ReturningToTheDefaultRemovesTheEntry()
    {
        using var ctx = new RenderContext();
        var picker = Picker(ctx);

        picker.Find("#ability-src-might").Change("tech");
        Assert.True(ctx.Session.Sheet.AbilitySources.ContainsKey("might"));

        picker.Find("#ability-src-might").Change("");

        Assert.False(ctx.Session.Sheet.AbilitySources.ContainsKey("might"));
        Assert.Empty(ctx.Session.Grouping.GroupBySource(ctx.Session.Sheet));
    }

    /// <summary>
    /// The control shows the Source already recorded. Without this the picker could read
    /// back blank on every visit while the sheet printed a line, which is the disagreement
    /// a player would report as the tool forgetting what they chose.
    /// </summary>
    [Fact]
    public void TheControlShowsTheSourceAlreadyRecorded()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.AbilitySources["toughness"] = "magic";

        Assert.Equal("magic",
            Picker(ctx).Find("#ability-src-toughness").GetAttribute("value"));
    }

    /// <summary>
    /// Every change notifies the session. Without it the budget bar, the sheet and the
    /// save-through to local storage all keep showing the character as it was before.
    /// </summary>
    [Fact]
    public void EveryChangeNotifiesTheSession()
    {
        using var ctx = new RenderContext();
        var changes = 0;
        ctx.Session.Changed += () => changes++;

        var picker = Picker(ctx);
        picker.Find("#ability-src-might").Change("tech");
        picker.Find("#ability-src-might").Change("");

        Assert.Equal(2, changes);
    }

    /// <summary>
    /// The two pickers prefix their control ids differently, so an Ability and a Talent that
    /// share a name could not collide — and, more to the point, a label always points at its
    /// own control.
    /// </summary>
    [Fact]
    public void EachPickerScopesItsControlIds()
    {
        using var ctx = new RenderContext();

        foreach (var (picker, scope, traits) in new[]
                 {
                     (Picker(ctx), "ability", ctx.Session.Rules.Abilities.Select(a => a.Id).ToList()),
                     (Picker(ctx, abilities: false), "talent", ctx.Session.Rules.Talents.Select(t => t.Id).ToList())
                 })
        {
            foreach (var id in traits)
            {
                var control = picker.Find($"#{scope}-src-{id}");
                var label   = picker.Find($"label[for='{scope}-src-{id}']");

                Assert.NotNull(control);
                Assert.False(string.IsNullOrWhiteSpace(label.TextContent));
            }
        }
    }
}
