using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Spending and gaining Resolve from <c>/sheet</c> during play.
///
/// <para><b>The owner's ruling decides the shape.</b> Resolve resets every issue, so each open
/// seeds the count from the engine's figure, and browser storage is only a net under an accidental
/// refresh — honoured for a day after it was written, then ignored. Nothing is tracked about which
/// issue it is, nothing goes to the account, and nothing goes on the sheet.</para>
/// </summary>
public sealed class ResolveTrackerTests
{
    /// <summary>A clock a test can move, so a day's expiry is a line rather than a day.</summary>
    private sealed class MovableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 28, 19, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static void Open(RenderContext ctx, string tier = "standard")
    {
        ctx.Session.Sheet.Name = "Lynchpin";
        ctx.Session.Sheet.SelectedTierId = tier;
    }

    private static int Opening(RenderContext ctx, CharacterSheet sheet) =>
        ctx.Session.Derived.CalculateResolve(sheet);

    private static int Shown(IRenderedComponent<SheetPage> page) =>
        int.Parse(page.Find(".resolve-now-value").TextContent.Trim(), System.Globalization.CultureInfo.InvariantCulture);

    private static void Spend(IRenderedComponent<SheetPage> page) =>
        page.Find("button[aria-label='Spend 1 Resolve']").Click();

    private static void Gain(IRenderedComponent<SheetPage> page) =>
        page.Find("button[aria-label='Gain 1 Resolve']").Click();

    // ── What is drawn, and where ─────────────────────────────────────────────────────

    /// <summary>
    /// A Hero's sheet opens on the engine's figure.
    ///
    /// <para>The figure is asserted non-zero first: a counter that always drew 0 would match an
    /// opening of 0 and prove nothing about where its number came from.</para>
    /// </summary>
    [Fact]
    public void AHerosCountStartsAtTheEnginesFigure()
    {
        using var ctx = new RenderContext();
        Open(ctx);

        var page = ctx.Render<SheetPage>();
        var opening = Opening(ctx, ctx.Session.Sheet);

        Assert.True(opening > 0, "the fixture must open on some Resolve, or matching it proves nothing");
        Assert.Equal(opening, Shown(page));
        Assert.Contains($"Starts each issue at {opening}.", page.Find(".resolve-now").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The count follows the engine while it is untouched: a figure that moves under an edit moves
    /// the counter with it, rather than freezing the value the page happened to open on.
    /// </summary>
    [Fact]
    public void AnUntouchedCountFollowsTheSheet()
    {
        using var ctx = new RenderContext();
        Open(ctx, "standard");

        // Saved once before the page opens, so the edit below does not give the character its
        // first id. That first save moves the key the count is held under, which re-creates the
        // counter — and a fresh counter follows the engine whether or not a live one would.
        ctx.Session.NotifyChanged();
        var key = DismissedFindings.CharacterKey(ctx.Session);

        var page = ctx.Render<SheetPage>();
        var before = Shown(page);

        page.InvokeAsync(() =>
        {
            ctx.Session.Sheet.SelectedTierId = "legendary";
            ctx.Session.NotifyChanged();
        });

        var after = Opening(ctx, ctx.Session.Sheet);
        Assert.NotEqual(before, after);
        Assert.Equal(key, DismissedFindings.CharacterKey(ctx.Session));
        page.WaitForAssertion(() => Assert.Equal(after, Shown(page)));
    }

    /// <summary>
    /// A Villain's sheet counts the same pool under the word Adversity — the owner's ask of
    /// 2026-10-28 — on the counter and on the ruled box above it. The Hero render beside it is the
    /// control: the same counter, still called Resolve.
    /// </summary>
    [Fact]
    public void AVillainCountsAdversity()
    {
        using (var hero = new RenderContext().With(SheetMode.Hero))
        {
            var heroPage = hero.Render<SheetPage>();
            Assert.Equal("Resolve now", heroPage.Find(".resolve-now-label").TextContent.Trim());
            Assert.Single(heroPage.FindAll("button[aria-label='Spend 1 Resolve']"));
            Assert.DoesNotContain("Adversity", heroPage.Find(".sheet").TextContent, StringComparison.Ordinal);
        }

        using var ctx = new RenderContext().With(SheetMode.Villain);
        var page = ctx.Render<SheetPage>();
        var opening = Opening(ctx, ctx.Session.Sheet);

        Assert.Equal("Adversity now", page.Find(".resolve-now-label").TextContent.Trim());
        Assert.Equal("Adversity during play", page.Find(".resolve-now").GetAttribute("aria-label"));
        Assert.Equal(opening, Shown(page));

        page.Find("button[aria-label='Gain 1 Adversity']").Click();
        Assert.Equal(opening + 1, Shown(page));
        page.Find("button[aria-label='Spend 1 Adversity']").Click();
        Assert.Equal(opening, Shown(page));

        // The ruled box above it carries the same word, and Resolve is nowhere a label on it.
        var labels = page.FindAll(".sheet .stat-block .label .term-name").Select(l => l.TextContent.Trim()).ToList();
        Assert.Contains("Edge", labels);
        Assert.Contains("Adversity", labels);
        Assert.DoesNotContain("Resolve", labels);
    }

    /// <summary>
    /// Only <c>/sheet</c> draws it. The review step draws the same <c>SheetView</c> and is not play.
    /// </summary>
    [Fact]
    public void TheReviewStepHasNoCounter()
    {
        using var ctx = new RenderContext();
        Open(ctx);

        Assert.Single(ctx.Render<SheetPage>().FindAll(".resolve-now"));

        var review = ctx.Render<Review>();
        Assert.Single(review.FindAll(".sheet"));
        Assert.Empty(review.FindAll(".resolve-now"));
    }

    /// <summary>The printed sheet is unchanged: the counter is screen only.</summary>
    [Fact]
    public void TheCounterDoesNotPrint()
    {
        using var ctx = new RenderContext();
        Open(ctx);

        var counter = ctx.Render<SheetPage>().Find(".resolve-now");

        Assert.Contains("no-print", counter.ClassList);
    }

    // ── Spending and gaining ─────────────────────────────────────────────────────────

    /// <summary>
    /// Spend lowers it, gain raises it — past the opening figure, because earning adds to the pool
    /// and the engine's number is where an issue opens, not a ceiling.
    /// </summary>
    [Fact]
    public void GainingGoesPastTheOpeningFigure()
    {
        using var ctx = new RenderContext();
        Open(ctx);
        var page = ctx.Render<SheetPage>();
        var opening = Opening(ctx, ctx.Session.Sheet);

        Spend(page);
        Assert.Equal(opening - 1, Shown(page));

        Gain(page);
        Gain(page);
        Gain(page);
        Assert.Equal(opening + 2, Shown(page));
    }

    /// <summary>
    /// The count is marked below the opening figure once something is spent and above it once
    /// something is earned past it, and unmarked at the opening figure itself.
    /// </summary>
    [Fact]
    public void TheCountIsMarkedAboveAndBelowTheOpeningFigure()
    {
        using var ctx = new RenderContext();
        Open(ctx);
        var page = ctx.Render<SheetPage>();
        IReadOnlyList<string> Marks() =>
            page.Find(".resolve-now-value").ClassList.Where(c => c is "above" or "below").ToList();

        Assert.Empty(Marks());

        Spend(page);
        Assert.Equal(["below"], Marks());

        Gain(page);
        Assert.Empty(Marks());

        Gain(page);
        Assert.Equal(["above"], Marks());
    }

    /// <summary>
    /// The two marks are coloured from the palette, each its own ink: the heading ink above, the
    /// danger ink below. Read from the shipped stylesheet, since a render cannot see a colour.
    /// </summary>
    [Theory]
    [InlineData("above", "--heading")]
    [InlineData("below", "--danger")]
    public void EachMarkHasItsOwnInk(string mark, string token)
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "web", "wwwroot", "css", "app.css"));
        var rule = System.Text.RegularExpressions.Regex.Match(
            css, $@"\.resolve-now-value\.{mark}\s*\{{([^}}]*)\}}");

        Assert.True(rule.Success, $".resolve-now-value.{mark} has no rule in app.css");
        Assert.Matches($@"(^|;)\s*color:\s*var\({token}\)\s*;", rule.Groups[1].Value);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("no .sln above the test binaries");
    }

    /// <summary>The floor is 0: spending stops there, and the button says so by being disabled.</summary>
    [Fact]
    public void SpendingStopsAtZero()
    {
        using var ctx = new RenderContext();
        Open(ctx);
        var page = ctx.Render<SheetPage>();
        var opening = Opening(ctx, ctx.Session.Sheet);

        for (var i = 0; i < opening; i++) Spend(page);

        Assert.Equal(0, Shown(page));
        Assert.True(page.Find("button[aria-label='Spend 1 Resolve']").HasAttribute("disabled"));
    }

    /// <summary>Reset goes back to the engine's figure, and only appears once there is something to reset.</summary>
    [Fact]
    public void ResetReturnsToTheOpeningFigure()
    {
        using var ctx = new RenderContext();
        Open(ctx);
        var page = ctx.Render<SheetPage>();
        var opening = Opening(ctx, ctx.Session.Sheet);

        Assert.Empty(page.FindAll(".resolve-now-reset"));

        Spend(page);
        Spend(page);
        page.Find(".resolve-now-reset").Click();

        Assert.Equal(opening, Shown(page));
        Assert.Empty(page.FindAll(".resolve-now-reset"));
    }

    // ── Surviving a refresh, for a day ───────────────────────────────────────────────

    /// <summary>
    /// A refresh — a second render reading the same storage — comes back on the count the player
    /// left, not on the engine's figure.
    /// </summary>
    [Fact]
    public void ARefreshKeepsTheCount()
    {
        using var ctx = new RenderContext(storesForReal: true);
        Open(ctx);
        var opening = Opening(ctx, ctx.Session.Sheet);

        var first = ctx.Render<SheetPage>();
        Spend(first);
        Spend(first);

        var second = ctx.Render<SheetPage>();

        second.WaitForAssertion(() => Assert.Equal(opening - 2, Shown(second)));
    }

    /// <summary>
    /// After a day the stored count is ignored and the sheet opens on the engine's figure again.
    /// Just inside the day it is still honoured — both sides of the line, so a guard that always
    /// expired, or never did, fails one of the two.
    /// </summary>
    [Theory]
    [InlineData(23, true)]
    [InlineData(24, false)]
    public void AStoredCountExpiresAfterADay(int hoursLater, bool kept)
    {
        var clock = new MovableClock();
        using var ctx = new RenderContext(storesForReal: true, clock: clock);
        Open(ctx);
        var opening = Opening(ctx, ctx.Session.Sheet);

        var first = ctx.Render<SheetPage>();
        Spend(first);
        Spend(first);
        Spend(first);

        clock.Now += TimeSpan.FromHours(hoursLater);
        var later = ctx.Render<SheetPage>();

        later.WaitForAssertion(() => Assert.Equal(kept ? opening - 3 : opening, Shown(later)));
    }

    /// <summary>
    /// The count is remembered per character: a saved character shown at <c>/sheet/{id}</c> opens
    /// on its own figure, untouched by what was spent on the one being built.
    /// </summary>
    [Fact]
    public async Task EachCharacterKeepsItsOwnCount()
    {
        await using var ctx = new RenderContext(storesForReal: true);
        Open(ctx, "standard");

        var own = ctx.Render<SheetPage>();
        Spend(own);

        var other = new CharacterSheet { Name = "Vandergraff", SelectedTierId = "legendary" };
        Assert.True(await ctx.Services.GetRequiredService<AccountCharacterStore>()
            .RestoreAsync("c_BBBBBBBBBBBBBBBBBBBBBB", "Vandergraff", other, SheetMode.Hero));

        var page = ctx.Render<SheetPage>(p => p.Add(c => c.Id, "c_BBBBBBBBBBBBBBBBBBBBBB"));

        page.WaitForAssertion(() =>
            Assert.Contains("Vandergraff", SheetText.Visible(page.Find(".sheet")), StringComparison.Ordinal));
        Assert.NotEqual(Opening(ctx, ctx.Session.Sheet), Opening(ctx, other));
        Assert.Equal(Opening(ctx, other), Shown(page));
    }

    /// <summary>
    /// A browser that refuses storage still counts on screen; it just cannot survive a refresh.
    /// </summary>
    [Fact]
    public void RefusedStorageStillCounts()
    {
        using var ctx = new RenderContext(storesForReal: true);
        ctx.Storage!.Refuses = true;
        Open(ctx);
        var opening = Opening(ctx, ctx.Session.Sheet);

        var page = ctx.Render<SheetPage>();
        Assert.Equal(opening, Shown(page));

        Spend(page);
        Assert.Equal(opening - 1, Shown(page));

        var refreshed = ctx.Render<SheetPage>();
        Assert.Equal(opening, Shown(refreshed));
    }

    // ── Rapid clicks ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A browser whose writes land when the test says so, holding the first one at the wire.
    /// A write is applied to storage only when it completes, which is when a real one is durable.
    /// </summary>
    private sealed class HeldStorage : IJSRuntime
    {
        public readonly Dictionary<string, string> Items = new(StringComparer.Ordinal);
        private readonly List<(string Key, string Value, TaskCompletionSource Done)> _held = [];
        public bool HoldNext { get; set; } = true;

        public void ReleaseHeld()
        {
            foreach (var (key, value, done) in _held) { Items[key] = value; done.SetResult(); }
            _held.Clear();
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "ppStore.save" && args is [string key, string value])
            {
                if (HoldNext)
                {
                    HoldNext = false;
                    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _held.Add((key, value, done));
                    return new ValueTask<TValue>(done.Task.ContinueWith(_ => default(TValue)!, TaskScheduler.Default));
                }

                Items[key] = value;
            }

            if (identifier == "ppStore.load" && args is [string read] && Items.GetValueOrDefault(read) is TValue stored)
                return ValueTask.FromResult(stored);

            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    /// <summary>
    /// Five clicks while the first write is still out land as the fifth count, not the first.
    ///
    /// <para>The shape item 31 found in the account autosave: an older write landing after a newer
    /// one wins. With writes one at a time, the held write lands first and the latest count is
    /// written after it; without, the four later writes land at once and the held one overwrites
    /// them on release.</para>
    /// </summary>
    [Fact]
    public async Task RapidClicksLandAsTheLastCount()
    {
        var js = new HeldStorage();
        var store = new ResolveNow(js, TimeProvider.System);

        var writes = new List<Task>();
        for (var now = 9; now >= 5; now--) writes.Add(store.SaveAsync("c_A", now));

        // Positive control: the first write really is held, so the order below is being tested
        // against a race rather than against writes that were never concurrent.
        Assert.False(writes[0].IsCompleted, "the first write must still be out when the others are made");

        js.ReleaseHeld();
        await Task.WhenAll(writes);

        Assert.Equal(5, await store.LoadAsync("c_A"));
    }
}
