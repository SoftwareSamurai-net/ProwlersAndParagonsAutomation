using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Perk editor asks each of a Perk's questions in its own box and waits for all of them —
/// the owner's ask of 2026-10-28. What is asked is <c>PerkQuestions</c>' and is held in the
/// engine's tests; what is here is that the editor draws it, waits for it, and stores the line.
/// </summary>
public sealed class PerkQuestionEditorTests
{
    private static IRenderedComponent<PerksTab> Choosing(RenderContext ctx, string perkName)
    {
        var tab = ctx.Render<PerksTab>();
        tab.FindAll("button.option-row, button").First(b =>
            b.QuerySelector(".name")?.TextContent.Trim() == perkName).Click();
        return tab;
    }

    private static bool AddIsLive(IRenderedComponent<PerksTab> tab) =>
        !tab.FindAll("button").Single(b => b.TextContent.Trim() == "Add perk").HasAttribute("disabled");

    [Fact]
    public void PatronAsksThreeQuestionsAndStoresTheAnswersUnderTheirLabels()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        var tab = Choosing(ctx, "Patron");

        var boxes = tab.FindAll("input[id^='pk-detail-']");
        Assert.Equal(3, boxes.Count);
        Assert.Contains("What do they expect in return?", tab.Markup, StringComparison.Ordinal);

        boxes[0].Input("ARGUS");
        tab.FindAll("input[id^='pk-detail-']")[1].Input("gear and intel");
        Assert.False(AddIsLive(tab), "two of three answered, and Add let it past");

        tab.FindAll("input[id^='pk-detail-']")[2].Input("missions on demand");
        Assert.True(AddIsLive(tab));
        tab.FindAll("button").Single(b => b.TextContent.Trim() == "Add perk").Click();

        var patron = Assert.Single(ctx.Session.Sheet.Perks);
        Assert.Equal("Who: ARGUS; Provides: gear and intel; Expects: missions on demand", patron.NarrativeDetail);
    }

    [Fact]
    public void ContactsDrawsOneBoxPerContactTypeBought()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        var tab = Choosing(ctx, "Contacts");

        Assert.Single(tab.FindAll("input[id^='pk-detail-']"));

        tab.Find("#pk-units").Change(3);
        Assert.Equal(3, tab.FindAll("input[id^='pk-detail-']").Count);

        tab.FindAll("input[id^='pk-detail-']")[0].Input("Police");
        tab.FindAll("input[id^='pk-detail-']")[1].Input("Media");
        Assert.False(AddIsLive(tab), "three bought and two named, and Add let it past");

        tab.FindAll("input[id^='pk-detail-']")[2].Input("Criminal");
        tab.FindAll("button").Single(b => b.TextContent.Trim() == "Add perk").Click();

        var contacts = Assert.Single(ctx.Session.Sheet.Perks);
        Assert.Equal(3, contacts.Units);
        Assert.Equal("Police; Media; Criminal", contacts.NarrativeDetail);
    }

    [Fact]
    public void FameAsksWhichIdentityOnlyWhenTheCharacterHasASecretOne()
    {
        using (var plain = new RenderContext())
        {
            plain.Session.Sheet.SelectedTierId = "standard";
            var tab = Choosing(plain, "Fame");
            Assert.Empty(tab.FindAll("input[type='radio']"));
            Assert.True(AddIsLive(tab));
        }

        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.Flaws.Add(new SelectedFlaw(PerkQuestions.SecretIdentityFlaw));
        var asked = Choosing(ctx, "Fame");

        Assert.Equal(2, asked.FindAll("input[type='radio']").Count);
        Assert.False(AddIsLive(asked));

        asked.FindAll("input[type='radio']").Single(r => r.GetAttribute("value") == "Heroic alter ego").Change(true);
        asked.FindAll("button").Single(b => b.TextContent.Trim() == "Add perk").Click();

        Assert.Equal("Heroic alter ego", Assert.Single(ctx.Session.Sheet.Perks).NarrativeDetail);
    }
}
