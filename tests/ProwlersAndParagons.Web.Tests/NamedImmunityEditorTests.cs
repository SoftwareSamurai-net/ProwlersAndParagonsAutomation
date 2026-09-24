using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Immunity is "named and paid for separately" (Ch.2 p.31), so the editor asks for a name per
/// immunity bought, and the sheet prints them.
/// </summary>
public sealed class NamedImmunityEditorTests
{
    private static PowerModel PowerById(RenderContext ctx, string id) =>
        ctx.Services.GetRequiredService<RulesRepository>().Powers.Single(p => p.Id == id);

    [Fact]
    public void ThereIsOneNameBoxPerImmunityAndTheNamesAreSaved()
    {
        using var ctx = new RenderContext();
        SelectedPower? committed = null;

        var editor = ctx.Render<PowerEditor>(p => p
            .Add(e => e.Power, PowerById(ctx, "immunity"))
            .Add(e => e.OnCommit, (SelectedPower sp) => committed = sp));

        Assert.Single(editor.FindAll("input[id^='pe-unit-name-']"));

        editor.Find("#pe-units").Change("3");
        Assert.Equal(3, editor.FindAll("input[id^='pe-unit-name-']").Count);
        Assert.Contains("What is immunity 2 against?", editor.Find("label[for='pe-unit-name-1']").TextContent,
            StringComparison.Ordinal);

        editor.Find("#pe-unit-name-0").Change(" Toxins ");
        editor.Find("#pe-unit-name-1").Change("Fire");
        editor.FindAll(".btn.primary").Single().Click();

        Assert.NotNull(committed);
        Assert.Equal(3, committed!.Units);
        Assert.Equal(["Toxins", "Fire"], committed.UnitNames);
    }

    /// <summary>
    /// Lowering the count keeps what was typed until the Power is saved, and saves only the names
    /// the count covers — so a name for an immunity no longer being bought never reaches the sheet.
    /// </summary>
    [Fact]
    public void LoweringTheCountSavesOnlyTheNamesItCovers()
    {
        using var ctx = new RenderContext();
        SelectedPower? committed = null;
        var existing = new SelectedPower("immunity", 0) { Units = 2, UnitNames = ["Toxins", "Fire"] };

        var editor = ctx.Render<PowerEditor>(p => p
            .Add(e => e.Power, PowerById(ctx, "immunity"))
            .Add(e => e.Existing, existing)
            .Add(e => e.OnCommit, (SelectedPower sp) => committed = sp));

        Assert.Equal("Fire", editor.Find("#pe-unit-name-1").GetAttribute("value"));

        editor.Find("#pe-units").Change("1");
        editor.FindAll(".btn.primary").Single().Click();

        Assert.Equal(["Toxins"], committed!.UnitNames);
    }

    /// <summary>Nothing typed is stored as nothing, the way a character that never named one is.</summary>
    [Fact]
    public void NoNamesTypedSavesNoList()
    {
        using var ctx = new RenderContext();
        SelectedPower? committed = null;

        var editor = ctx.Render<PowerEditor>(p => p
            .Add(e => e.Power, PowerById(ctx, "immunity"))
            .Add(e => e.OnCommit, (SelectedPower sp) => committed = sp));

        editor.FindAll(".btn.primary").Single().Click();

        Assert.NotNull(committed);
        Assert.Null(committed!.UnitNames);
    }

    /// <summary>The control: a counted Power asks how many and nothing else.</summary>
    [Fact]
    public void ACountedPowerHasNoNameBoxes()
    {
        using var ctx = new RenderContext();

        var editor = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, PowerById(ctx, "determination")));

        Assert.NotNull(editor.Find("#pe-units"));
        Assert.Empty(editor.FindAll("input[id^='pe-unit-name-']"));
    }

    [Fact]
    public void TheSheetPrintsWhichImmunities()
    {
        using var ctx = new RenderContext();
        var sheet = new CharacterSheet { SelectedTierId = "standard" };
        sheet.SelectedPowers.Add(new SelectedPower("immunity", 0)
        {
            Units = 3, SourceId = "tech", UnitNames = ["Toxins", "Fire"]
        });

        var view = ctx.Render<SheetView>(p => p.Add(v => v.Character, sheet));

        Assert.Contains(view.FindAll(".power-entry .statline"),
            line => line.TextContent == "Immunities: Toxins, Fire, 1 unnamed");
    }
}
