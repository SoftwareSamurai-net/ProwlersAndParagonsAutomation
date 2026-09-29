using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Pages;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// The Resolve and Adversity quick reference at <c>/reference/resolve</c> — the one narrow
/// exemption that lets <c>web/</c> read a play rules file at all. See
/// <c>ResolveReferenceReader</c>'s own remarks and <c>docs/guide/play-rules.md</c>.
///
/// <para><b>Against the real file, not a stub.</b> Same reasoning as <c>RenderContext</c>'s own
/// doc comment: a reader whose models line up with an invented fixture and not with the real
/// <c>data/rules/play/resolve.json</c> has been tested for nothing.</para>
/// </summary>
public sealed class ResolveReferenceTests
{
    private static readonly Lazy<string> RealJsonLazy = new(() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "data", "rules", "play", "resolve.json")));

    private static string RealJson => RealJsonLazy.Value;

    // ── The strict reader against the real file ─────────────────────────────────────────────

    [Fact]
    public void TheRealFileParsesUnderTheStrictReaderAndHasTwentyEightEntries()
    {
        var document = ResolveReferenceReader.Parse(RealJson);

        Assert.Equal(28, document.Entries.Count);
        Assert.False(string.IsNullOrWhiteSpace(document.Header.SourceRef));
    }

    /// <summary>
    /// The whole point of <c>JsonUnmappedMemberHandling.Disallow</c>: a field this file gains
    /// that <c>ResolveReferenceModels.cs</c> does not name throws rather than being quietly
    /// dropped. Proved by mutation on the real file's own bytes rather than on an invented
    /// fixture, so a model that happens to match a hand-written test object but not the real
    /// file cannot pass this by accident.
    /// </summary>
    [Fact]
    public void AnUnmappedFieldOnTheRealFileIsRefused()
    {
        const string marker = "\"id\": \"starting_resolve\",";
        Assert.Contains(marker, RealJson, StringComparison.Ordinal);

        var mutated = RealJson.Replace(
            marker, marker + " \"an_unrecognised_field\": true,", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => ResolveReferenceReader.Parse(mutated));
    }

    // ── The block mapping ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every one of the 28 entries lands in exactly one of the three blocks — the property the
    /// task asked to be provable, so a 29th entry added later cannot silently miss all three (or
    /// land in two, which <see cref="ResolveReferenceBlocks.BlockFor"/> cannot do by construction
    /// since it returns one value, but a test that only checked the total would not notice a
    /// block quietly emptying out while another one absorbed its share).
    /// </summary>
    [Fact]
    public void EveryEntryLandsInExactlyOneBlock()
    {
        var document = ResolveReferenceReader.Parse(RealJson);
        Assert.Equal(28, document.Entries.Count);

        var byBlock = document.Entries
            .GroupBy(ResolveReferenceBlocks.BlockFor)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(28, byBlock.Values.Sum());
        Assert.Equal(20, byBlock.GetValueOrDefault(ResolveBlock.Players));
        Assert.Equal(7, byBlock.GetValueOrDefault(ResolveBlock.Gm));
        Assert.Equal(1, byBlock.GetValueOrDefault(ResolveBlock.TableLimits));
    }

    [Fact]
    public void TheTableLimitsBlockIsRerollFloor()
    {
        var document = ResolveReferenceReader.Parse(RealJson);

        var tableLimits = document.Entries
            .Where(e => ResolveReferenceBlocks.BlockFor(e) == ResolveBlock.TableLimits)
            .Select(e => e.Id)
            .ToList();

        Assert.Equal(["reroll_floor"], tableLimits);
    }

    [Fact]
    public void EveryAdversityPrefixedEntryIsInTheGmBlock()
    {
        var document = ResolveReferenceReader.Parse(RealJson);

        foreach (var entry in document.Entries.Where(e => e.Id.StartsWith("adversity_", StringComparison.Ordinal)))
        {
            Assert.Equal(ResolveBlock.Gm, ResolveReferenceBlocks.BlockFor(entry));
        }
    }

    [Fact]
    public void EveryEntryWhoseWhoIsHeroIsInThePlayersBlock()
    {
        var document = ResolveReferenceReader.Parse(RealJson);

        var heroSpends = document.Entries.Where(e => e.Who == "hero").ToList();
        Assert.True(heroSpends.Count >= 8, "expected several who:hero spends in the real file.");

        foreach (var entry in heroSpends)
        {
            Assert.Equal(ResolveBlock.Players, ResolveReferenceBlocks.BlockFor(entry));
        }
    }

    [Fact]
    public void CostWordsNeverThrowsOnAnyRealEntry()
    {
        var document = ResolveReferenceReader.Parse(RealJson);

        foreach (var entry in document.Entries)
        {
            // Not every entry has a plain-words cost — null is a legitimate answer — but every
            // one has to be answerable without throwing.
            _ = ResolveReferenceBlocks.CostWords(entry);
        }
    }

    [Theory]
    [InlineData("starting_resolve", "2 Resolve per rank below the Trait Cap")]
    [InlineData("adversity_pool", "1 Adversity per Hero, per issue")]
    [InlineData("resolve_exceptions", null)]
    public void CostWordsReadsTheEntryItIsAbout(string id, string? expected)
    {
        var document = ResolveReferenceReader.Parse(RealJson);
        var entry = document.Entries.Single(e => e.Id == id);

        Assert.Equal(expected, ResolveReferenceBlocks.CostWords(entry));
    }

    /// <summary>
    /// Every figure in the cost column is read off the entry, never written beside it. The
    /// assisting rate is the one a reviewer found spelled out as "2": this doubles the field in
    /// the real file's own bytes and requires the words to move with it, which a literal cannot.
    /// </summary>
    [Fact]
    public void TheAssistingRateIsReadFromTheEntryNotWrittenHere()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(RealJson)!;
        var assisting = node["entries"]!.AsArray()
            .Single(e => (string?)e!["id"] == "spend_assisting_allies")!;
        var rate = (int)assisting["spend"]!["cost_per_point_shared_when_unable_to_assist"]!;
        assisting["spend"]!["cost_per_point_shared_when_unable_to_assist"] = rate * 2;

        var entry = ResolveReferenceReader.Parse(node.ToJsonString()).Entries
            .Single(e => e.Id == "spend_assisting_allies");

        Assert.Contains($"({rate * 2} per point if unable to assist)",
            ResolveReferenceBlocks.CostWords(entry), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintedPageReadsTheTrailingPageNumberOffASourceRef()
    {
        Assert.Equal(
            "p.84",
            ResolveReferenceBlocks.PrintedPage("Ultimate Edition, Ch.5 Resolve and Adversity, p.84"));
    }

    // ── Rendered ──────────────────────────────────────────────────────────────────────────────

    /// <summary>How long a test waits for the page's one async fetch to land.</summary>
    private static readonly TimeSpan Patient = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ThePageRendersAllThreeBlocksFromTheRealFile()
    {
        using var ctx = NewContext(new StaticJsonHandler(RealJson));

        var page = ctx.Render<ResolveReference>();

        await page.WaitForAssertionAsync(() => Assert.Contains("For the GM", page.Markup), Patient);

        Assert.Contains("For players", page.Markup);
        Assert.Contains("Table limits", page.Markup);

        // A named entry from each block, so this is a render check on real data rather than
        // three headings with nothing under them.
        Assert.Contains("Starting Resolve", page.Markup);
        Assert.Contains("Villainy", page.Markup);
        Assert.Contains("p.85", page.Markup);
    }

    /// <summary>
    /// <b>The page shows the player-facing <c>summary</c>, never the maintainer's <c>description</c>.</b>
    /// The owner's ruling was that a visitor reads what a rule does, not a transcription note
    /// naming <c>DerivedStatsCalculator</c> or talking about "the engine". Checked both ways on a
    /// known entry: the summary's own words are present, and a phrase that appears in the
    /// description and nowhere in the summary is absent — so a component reverted to
    /// <c>@entry.Description</c> fails this rather than merely failing to fail. Broken by mutating
    /// <see cref="ResolveEntryList"/>'s <c>@entry.Summary</c> back to <c>@entry.Description</c> and
    /// watching it go red on both assertions.
    /// </summary>
    [Fact]
    public async Task ThePageShowsTheSummaryNotTheDescription()
    {
        using var ctx = NewContext(new StaticJsonHandler(RealJson));

        var document = ResolveReferenceReader.Parse(RealJson);
        var entry = document.Entries.Single(e => e.Id == "starting_resolve");

        Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
        Assert.DoesNotContain("DerivedStatsCalculator", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("DerivedStatsCalculator", entry.Description, StringComparison.Ordinal);

        var page = ctx.Render<ResolveReference>();

        await page.WaitForAssertionAsync(() => Assert.Contains("Starting Resolve", page.Markup), Patient);

        // The summary's own words, taken from the real file rather than typed out twice here.
        Assert.Contains("left three dice unbought opens on 6", page.Markup, StringComparison.Ordinal);

        // A phrase that only the description carries — naming the engine type that computes this
        // figure — must never reach the rendered page.
        Assert.DoesNotContain("DerivedStatsCalculator", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedFetchSaysTheReferenceCouldNotBeLoaded()
    {
        using var ctx = NewContext(new FailingHandler());

        var page = ctx.Render<ResolveReference>();

        await page.WaitForAssertionAsync(
            () => Assert.Contains("could not be loaded", page.Markup, StringComparison.OrdinalIgnoreCase), Patient);
    }

    /// <summary>
    /// A minimal render context of this test's own, rather than <c>RenderContext</c>: that one
    /// resolves <c>CharacterSession</c> inside its own constructor, which locks bUnit's service
    /// provider before this page's <c>HttpClient</c> and <c>ResolveReferenceReader</c> could be
    /// added. <c>ResolveEntryList</c> is built on <c>&lt;ChosenList&gt;</c>/<c>&lt;ChosenRow&gt;</c>
    /// rather than hand-written markup, so those still need the same engine wiring
    /// <c>RenderContext</c> gives every other rendered page — registered here, before anything
    /// is resolved.
    /// </summary>
    private static BunitContext NewContext(HttpMessageHandler handler)
    {
        var ctx = new BunitContext();

        var rules = RulesRepository.FromBasePath(RepoRoot());
        var costs = new CostCalculator(rules);
        var derived = new DerivedStatsCalculator(rules);
        var validator = new CharacterValidator(rules, costs, derived);

        ctx.Services.AddSingleton(rules);
        ctx.Services.AddSingleton(costs);
        ctx.Services.AddSingleton(derived);
        ctx.Services.AddSingleton(validator);
        ctx.Services.AddSingleton(new ProConApplicability(rules));
        ctx.Services.AddSingleton(new SourceGrouping(rules));
        ctx.Services.AddScoped<CharacterSession>();
        ctx.Services.AddScoped<DismissedFindings>();
        ctx.Services.AddScoped<Motion>();

        ctx.Services.AddScoped(_ => new HttpClient(handler)
        {
            BaseAddress = new Uri("https://pp.example.test/"),
        });
        ctx.Services.AddScoped<ResolveReferenceReader>();

        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        return ctx;
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}
