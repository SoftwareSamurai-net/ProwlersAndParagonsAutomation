using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Pet/Sidekick in the browser (Ch.2 p.55): the Perks row that makes and opens the companion, the
/// companion's budget strip and tier step, both sheets' Resolve, and the roster.
///
/// <para><b>The fixture is the engine's own pair</b> — Kestrel, every Trait at 1d, with
/// Pet/Sidekick at 4, and Talon, the same with Agility 9 and the Enemy Flaw — so every figure here
/// is one <c>CompanionTests</c> has already proved: Talon's budget is 40, Kestrel's Resolve is 22
/// alone and 7 with Talon. Each test carries the control of the same screen without the family.</para>
/// </summary>
public sealed class CompanionBrowserTests
{
    private sealed record Account(RenderContext Ctx, string OwnerId, string? TalonId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Ctx.DisposeAsync();
    }

    private static CharacterSheet Legal(RenderContext ctx, string name)
    {
        var sheet = new CharacterSheet { Name = name, SelectedTierId = "standard" };
        foreach (var ability in ctx.Session.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 1;
        foreach (var talent in ctx.Session.Rules.Talents) sheet.TalentRanks[talent.Id] = 1;
        sheet.Flaws.Add(new SelectedFlaw("absentminded"));
        return sheet;
    }

    /// <summary>Kestrel in a signed-in account, open on screen, with Talon saved beside her when asked.</summary>
    private static async Task<Account> Kestrel(bool withTalon = true, int units = 4, bool openTalon = false)
    {
        // Stores for real: the current-character pointer lives in browser storage, and a context
        // that drops it adopts the most recent row — the companion — and autosaves the owner over it.
        var ctx = new RenderContext(storesForReal: true);
        ctx.Api.SignedIn = ("acct-7", "player");
        var api = ctx.Services.GetRequiredService<ApiCharacterStore>();

        var owner = Legal(ctx, "Kestrel");
        owner.Perks.Add(new SelectedPerk("pet_sidekick", units, "Talon, a red-tailed hawk"));
        var ownerId = SavedCharacters.NewId();
        await api.SaveAsync(ownerId, owner.Name, owner, SheetMode.Hero);

        string? talonId = null;
        if (withTalon)
        {
            var talon = Legal(ctx, "Talon");
            talon.AbilityRanks["agility"] = 9;
            talon.Flaws.Add(new SelectedFlaw("enemy"));
            talon.Variant = new CharacterVariant(ownerId, CharacterVariant.Companion);
            talonId = SavedCharacters.NewId();
            await api.SaveAsync(talonId, talon.Name, talon, SheetMode.Hero);
        }

        var store = ctx.Services.GetRequiredService<AccountCharacterStore>();
        var open  = openTalon ? talonId! : ownerId;
        var read  = (await store.OpenAsync(open))!.Value;
        ctx.Session.Open(read.Sheet, read.Mode, open);

        return new Account(ctx, ownerId, talonId);
    }

    private static string Resolve(IRenderedComponent<IComponent> page) =>
        page.FindAll(".sheet .stat-block").Single(b => b.QuerySelector(".label")!.TextContent.Contains("Resolve", StringComparison.Ordinal))
            .QuerySelector(".value")!.TextContent.Trim();

    private static string? ResolveSub(IRenderedComponent<IComponent> page) =>
        page.FindAll(".sheet .stat-block").Single(b => b.QuerySelector(".label")!.TextContent.Contains("Resolve", StringComparison.Ordinal))
            .QuerySelector(".sub")?.TextContent.Trim();

    // ── The Perks row ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ThePerkRowSaysWhatTheCompanionGetsAndOffersToBuildIt()
    {
        await using var account = await Kestrel(withTalon: false);
        var tab = account.Ctx.Render<PerksTab>();

        tab.WaitForAssertion(() =>
            Assert.Contains("Gives a companion 40 Hero Points (p.55). At most 6: a companion may have half of 125.",
                tab.Find(".companion-line").TextContent, StringComparison.Ordinal));
        Assert.Single(tab.FindAll("button"), b => b.TextContent.Trim() == "Build the companion");
    }

    [Fact]
    public async Task BuildingTheCompanionSavesItLinkedAndOpensIt()
    {
        await using var account = await Kestrel(withTalon: false);
        var ctx = account.Ctx;
        var tab = ctx.Render<PerksTab>();

        // The strip is up before the switch, so what it shows after is the family read again for
        // the companion, not the owner's left standing.
        var bar = ctx.Render<HpBudgetBar>();
        bar.WaitForAssertion(() => Assert.Equal("/ 125", bar.Find(".budget-of").TextContent.Trim()));

        await tab.FindAll("button").Single(b => b.TextContent.Trim() == "Build the companion").ClickAsync(new());

        bar.WaitForAssertion(() => Assert.Equal("/ 40", bar.Find(".budget-of").TextContent.Trim()));
        Assert.Equal("from Kestrel's Pet/Sidekick", bar.Find(".budget-source").TextContent.Trim());

        tab.WaitForAssertion(() => Assert.True(Companions.IsCompanion(ctx.Session.Sheet)));
        Assert.Equal(account.OwnerId, ctx.Session.Sheet.Variant!.OfCharacterId);
        Assert.Equal("Kestrel's companion", ctx.Session.Sheet.Name);
        Assert.Equal("standard", ctx.Session.Sheet.SelectedTierId);
        Assert.EndsWith("build", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);

        // Stored, not only on screen: the index lists it as Kestrel's companion.
        var listed = await ctx.Services.GetRequiredService<AccountCharacterStore>().ListAsync();
        var row = Assert.Single(listed.Characters, c => c.VariantKind == CharacterVariant.Companion);
        Assert.Equal(account.OwnerId, row.VariantOf);
        Assert.Equal(ctx.Session.HeldId, row.Id);
    }

    [Fact]
    public async Task AnOwnerWithACompanionIsOfferedToOpenIt()
    {
        await using var account = await Kestrel();
        var tab = account.Ctx.Render<PerksTab>();

        tab.WaitForAssertion(() =>
            Assert.Single(tab.FindAll("button"), b => b.TextContent.Trim() == "Open Talon"));
        Assert.DoesNotContain(tab.FindAll("button"), b => b.TextContent.Trim() == "Build the companion");
    }

    [Fact]
    public async Task WithoutABudgetLimitTheCeilingIsTheGmsCall()
    {
        await using var account = await Kestrel(withTalon: false);
        account.Ctx.Session.UnlimitedBudget = true;

        var tab = account.Ctx.Render<PerksTab>();

        Assert.Contains("With no budget limit, how many it may have is the GM's call.",
            tab.Find(".companion-line").TextContent, StringComparison.Ordinal);
    }

    // ── The companion's own screens ──────────────────────────────────────────────────

    /// <summary>The strip measures a companion against its owner's purchase, and names it; the owner's own strip is the tier's.</summary>
    [Fact]
    public async Task ACompanionsBudgetStripIsItsOwnersPurchase()
    {
        await using (var owner = await Kestrel())
        {
            var strip = owner.Ctx.Render<HpBudgetBar>();
            Assert.Equal("/ 125", strip.Find(".budget-of").TextContent.Trim());
            Assert.Empty(strip.FindAll(".budget-source"));
        }

        await using var account = await Kestrel(openTalon: true);
        var bar = account.Ctx.Render<HpBudgetBar>();

        bar.WaitForAssertion(() => Assert.Equal("/ 40", bar.Find(".budget-of").TextContent.Trim()));
        Assert.Equal("from Kestrel's Pet/Sidekick", bar.Find(".budget-source").TextContent.Trim());
    }

    [Fact]
    public async Task ACompanionOverItsOwnersPurchaseIsOver()
    {
        await using var account = await Kestrel(units: 1, openTalon: true);
        var bar = account.Ctx.Render<HpBudgetBar>();

        bar.WaitForAssertion(() => Assert.Equal("/ 10", bar.Find(".budget-of").TextContent.Trim()));
        Assert.EndsWith("HP over", bar.Find(".over-text").TextContent.Trim(), StringComparison.Ordinal);
    }

    /// <summary>The open character's row measures a companion against its owner's purchase too, not its tier.</summary>
    [Fact]
    public async Task TheOpenRowMeasuresACompanionAgainstItsOwnersPurchase()
    {
        await using var account = await Kestrel(openTalon: true);
        var manager = account.Ctx.Render<CharacterManager>();

        manager.WaitForAssertion(() =>
            Assert.Contains($"{account.Ctx.Session.Spent} of 40 Hero Points", manager.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("of 125 Hero Points", manager.Markup, StringComparison.Ordinal);
    }

    /// <summary>A companion's tier step says where its tier comes from instead of offering six, and offers the owner's when it differs.</summary>
    [Fact]
    public async Task ACompanionsTierStepTakesItsOwnersTier()
    {
        await using var account = await Kestrel(openTalon: true);
        var ctx = account.Ctx;
        var page = ctx.Render<ChooseTier>();

        page.WaitForAssertion(() =>
            Assert.Contains("A companion of Kestrel (p.55): 40 Hero Points from Kestrel's Pet/Sidekick, and Kestrel's Trait Cap of 12d.",
                page.Markup, StringComparison.Ordinal));
        Assert.Empty(page.FindAll(".budget-choice"));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.StartsWith("Use Kestrel", StringComparison.Ordinal));

        await page.InvokeAsync(() =>
        {
            ctx.Session.Sheet.SelectedTierId = "high_level";
            ctx.Session.NotifyChanged();
        });

        var take = page.WaitForElement("button.btn.small");
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Use Kestrel's tier"));
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Use Kestrel's tier").ClickAsync(new());

        Assert.Equal("standard", ctx.Session.Sheet.SelectedTierId);
        _ = take;
    }

    // ── Resolve on the sheet ─────────────────────────────────────────────────────────

    /// <summary>The owner's Resolve is counted with the companion and says so; alone it would be 22.</summary>
    [Fact]
    public async Task AnOwnersSheetCountsItsCompanionAndSaysSo()
    {
        await using var account = await Kestrel();
        var page = account.Ctx.Render<SheetPage>();

        page.WaitForAssertion(() => Assert.Equal("7", Resolve(page)));
        Assert.Equal("with Talon · 22 alone", ResolveSub(page));
        Assert.Contains("Starts each issue at 7.", page.Find(".resolve-now").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOwnerWithNoCompanionYetSaysSo()
    {
        await using var account = await Kestrel(withTalon: false);
        var page = account.Ctx.Render<SheetPage>();

        page.WaitForAssertion(() => Assert.Equal("no companion yet", ResolveSub(page)));
        Assert.Equal("22", Resolve(page));
    }

    /// <summary>A companion has no Resolve: its box says whose it spends, and its counter is the owner's, under the owner's name.</summary>
    [Fact]
    public async Task ACompanionsSheetSpendsItsOwnersResolve()
    {
        await using var account = await Kestrel(openTalon: true);
        var page = account.Ctx.Render<SheetPage>();

        page.WaitForAssertion(() => Assert.Equal("spends Kestrel's", ResolveSub(page)));
        Assert.Equal("—", Resolve(page));
        Assert.Equal("Kestrel's Resolve now", page.Find(".resolve-now-label").TextContent.Trim());
        Assert.Contains("Starts each issue at 7.", page.Find(".resolve-now").TextContent, StringComparison.Ordinal);

        // Spent on Talon's behalf comes out of Kestrel's count, stored under Kestrel's id.
        await page.Find("button[aria-label=\"Spend 1 Kestrel's Resolve\"]").ClickAsync(new());
        var now = account.Ctx.Services.GetRequiredService<ResolveNow>();
        Assert.Equal(6, await now.LoadAsync(account.OwnerId));
        Assert.Null(await now.LoadAsync(account.TalonId!));
    }

    /// <summary>A stored owner shown at its own address is counted with its companion too.</summary>
    [Fact]
    public async Task AStoredOwnersSheetIsCountedWithItsCompanion()
    {
        await using var account = await Kestrel();
        account.Ctx.Session.StartAgain(offerUndo: false);

        var page = account.Ctx.Render<SheetPage>(p => p.Add(c => c.Id, account.OwnerId));

        page.WaitForAssertion(() => Assert.Equal("7", Resolve(page)));
        Assert.Equal("with Talon · 22 alone", ResolveSub(page));
    }

    /// <summary>A sheet nobody read the family for marks the owner's own figure rather than printing it as the answer.</summary>
    [Fact]
    public async Task AnOwnersFigureWithNoFamilyReadIsMarked()
    {
        await using var account = await Kestrel();
        var sheet = (await account.Ctx.Services.GetRequiredService<AccountCharacterStore>().ReadAsync(account.OwnerId))!.Value.Sheet;

        var view = account.Ctx.Render<SheetView>(p => p.Add(c => c.Character, sheet));

        Assert.Equal("22*", Resolve(view));
        Assert.Equal("* without its companion", ResolveSub(view));
    }

    // ── The roster ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheRosterCountsTheOwnersResolveWithTheCompanionAndNamesItsFaults()
    {
        // Talon open, so Kestrel is a row of the list rather than the open character's block.
        await using (var fine = await Kestrel(openTalon: true))
        {
            var manager = fine.Ctx.Render<CharacterManager>(p => p.Add(c => c.ListsEveryCharacter, true));
            manager.WaitForAssertion(() =>
                Assert.Contains("Resolve 7 with Talon", manager.Markup, StringComparison.Ordinal));
        }

        await using var over = await Kestrel(units: 1);
        var cut = over.Ctx.Render<CharacterManager>(p => p.Add(c => c.ListsEveryCharacter, true));
        cut.WaitForAssertion(() =>
            Assert.Contains("Error: Talon costs", cut.Markup, StringComparison.Ordinal));
    }
}
