using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Services;

using ProwlersAndParagons.Testing;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Where the rules ask the player to write something, the editor asks in the rule's own words
/// and the sheet prints the answer — the owner's ask of 2026-09-30, on the third kind of entry
/// that asks. Perks and Flaws had this; a Pro, a Con and a Power did not.
///
/// <para>Each test carries its control: an option or a Power that asks nothing draws no box,
/// so a box that appeared everywhere would not pass.</para>
/// </summary>
public sealed class NarrativeDetailEditorTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(RepoRoot());
    private static readonly CostCalculator Costs = new(Rules);

    // ── The picker ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Conditional asks for its condition, in the entry's words, and Add waits for it.</b>
    /// Armor Piercing asks nothing and draws no box — the control.
    /// </summary>
    [Fact]
    public void AConThatAsksGetsABoxLabelledWithItsAskAndAddWaitsForIt()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var selected = new List<SelectedProCon>();
        var picker = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Power, Rules.GetPower("blast"))
            .Add(c => c.IsPro, false)
            .Add(c => c.Selected, selected));

        picker.Find(".btn.small").Click();
        picker.FindAll(".option").Single(r => r.TextContent.Contains("Conditional", StringComparison.Ordinal)).Click();

        var ask = Rules.GetCon("conditional")!.NarrativeConstraint!;
        var label = picker.Find("label[for='pcd-con']");
        Assert.Equal(ask, label.TextContent);

        // Graded and asked: the grade alone does not enable Add.
        picker.Find("#pcv-con").Change("often_works");
        Assert.True(picker.Find(".btn.primary").HasAttribute("disabled"));

        picker.Find("#pcd-con").Input("only under an open sky");
        Assert.False(picker.Find(".btn.primary").HasAttribute("disabled"));
        picker.Find(".btn.primary").Click();

        var chosen = Assert.Single(selected);
        Assert.Equal("conditional", chosen.Id);
        Assert.Equal("often_works", chosen.VariantKey);
        Assert.Equal("only under an open sky", chosen.Detail);

        // The chosen row says the words.
        Assert.Contains("only under an open sky", picker.Find(".chosen").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOptionThatAsksNothingDrawsNoBox()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var selected = new List<SelectedProCon>();
        var picker = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Power, Rules.GetPower("blast"))
            .Add(c => c.IsPro, true)
            .Add(c => c.Selected, selected));

        picker.Find(".btn.small").Click();
        picker.FindAll(".option").Single(r => r.TextContent.StartsWith("Armor Piercing", StringComparison.Ordinal)).Click();

        Assert.Empty(picker.FindAll("#pcd-pro"));
        Assert.False(picker.Find(".btn.primary").HasAttribute("disabled"));
        picker.Find(".btn.primary").Click();

        Assert.Null(Assert.Single(selected).Detail);
    }

    /// <summary>A Power's own Con that asks — Immortality's Vulnerable — asks the same way.</summary>
    [Fact]
    public void APowersOwnConThatAsksGetsTheBoxToo()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        var selected = new List<SelectedProCon>();
        var picker = ctx.Render<ProConPicker>(p => p
            .Add(c => c.Power, Rules.GetPower("immortality"))
            .Add(c => c.IsPro, false)
            .Add(c => c.Selected, selected));

        picker.Find(".btn.small").Click();
        picker.FindAll(".option").Single(r => r.TextContent.StartsWith("Vulnerable", StringComparison.Ordinal)).Click();

        Assert.Equal("Describe how you can be killed.", picker.Find("label[for='pcd-con']").TextContent);
        Assert.True(picker.Find(".btn.primary").HasAttribute("disabled"));
    }

    // ── The Power editor ────────────────────────────────────────────────────────

    /// <summary>Expertise asks for its name; Blast asks nothing.</summary>
    [Fact]
    public void APowerThatAsksGetsABoxAndTheAnswerIsSaved()
    {
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";
        ctx.Session.Sheet.AbilityRanks["agility"] = 4;

        SelectedPower? committed = null;
        var editor = ctx.Render<PowerEditor>(p => p
            .Add(e => e.Power, Rules.GetPower("expertise"))
            .Add(e => e.OnCommit, (SelectedPower sp) => committed = sp));

        Assert.Equal(Rules.GetPower("expertise")!.NarrativeConstraint, editor.Find("label[for='pe-detail']").TextContent);

        editor.Find("#pe-trait").Change("agility");
        Assert.True(editor.Find(".btn.primary").HasAttribute("disabled"));

        editor.Find("#pe-detail").Input("Firearms");
        Assert.False(editor.Find(".btn.primary").HasAttribute("disabled"));
        editor.Find(".btn.primary").Click();

        Assert.Equal("Firearms", committed!.Detail);

        var plain = ctx.Render<PowerEditor>(p => p
            .Add(e => e.Power, Rules.GetPower("blast"))
            .Add(e => e.OnCommit, (SelectedPower _) => { }));
        Assert.Empty(plain.FindAll("#pe-detail"));
    }

    // ── The sheet and the diff ──────────────────────────────────────────────────

    /// <summary>The sheet prints <c>Expertise: Firearms</c> and the condition after its Con.</summary>
    [Fact]
    public void TheSheetPrintsTheWords()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Session.Sheet.AbilityRanks["agility"] = 4;
        ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("expertise", 2) { BaselineTraitId = "agility", Detail = "Firearms" });
        ctx.Session.Sheet.SelectedPowers.Add(new SelectedPower("blast", 4, [],
            [new SelectedProCon("conditional", "often_works") { Detail = "only under an open sky" }]));

        var sheet = ctx.Render<SheetView>();

        // `SheetText.Visible` breaks where a browser would and a term's hidden copies read as a
        // block to it, so the join between the term and the text after it is read with its
        // whitespace collapsed: what is held is the words and their order.
        static string Words(IElement e) =>
            System.Text.RegularExpressions.Regex.Replace(SheetText.Visible(e), @"\s+", " ").Replace(" :", ":", StringComparison.Ordinal);

        Assert.Contains(sheet.FindAll(".power-entry .head .pname"),
            e => Words(e).Contains("Expertise: Firearms", StringComparison.Ordinal));
        Assert.Contains(sheet.FindAll(".power-entry .statline"),
            e => Words(e).Contains("Conditional (Often Works) — only under an open sky", StringComparison.Ordinal));
    }

    /// <summary>A rewritten condition is a change the GM sees, as a part of the Power's row.</summary>
    [Fact]
    public void ARewrittenConditionIsAChangeInTheDiff()
    {
        var before = new CharacterSheet { SelectedTierId = "standard", AbilityRanks = { ["might"] = 4 } };
        var after = new CharacterSheet { SelectedTierId = "standard", AbilityRanks = { ["might"] = 4 } };

        before.SelectedPowers.Add(new SelectedPower("blast", 4, [], [new SelectedProCon("conditional", "often_works") { Detail = "only at night" }]));
        after.SelectedPowers.Add(new SelectedPower("blast", 4, [], [new SelectedProCon("conditional", "often_works") { Detail = "only under an open sky" }]));

        var diff = CampaignDiff.Between(before, after, Rules, Costs);

        var row = Assert.Single(diff.Rows);
        Assert.Equal("Power: Blast", row.What);
        var changed = Assert.Single(row.Parts, p => p.Kind == PartKind.Changed);
        Assert.Equal("Conditional (Often Works) — only at night", changed.Was);
        Assert.Equal("Conditional (Often Works) — only under an open sky", changed.Text);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
