using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
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
        using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => new HttpClient(new StaticJsonHandler(RealJson))
        {
            BaseAddress = new Uri("https://pp.example.test/"),
        });
        ctx.Services.AddScoped<ResolveReferenceReader>();

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

    [Fact]
    public async Task AFailedFetchSaysTheReferenceCouldNotBeLoaded()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => new HttpClient(new FailingHandler())
        {
            BaseAddress = new Uri("https://pp.example.test/"),
        });
        ctx.Services.AddScoped<ResolveReferenceReader>();

        var page = ctx.Render<ResolveReference>();

        await page.WaitForAssertionAsync(
            () => Assert.Contains("could not be loaded", page.Markup, StringComparison.OrdinalIgnoreCase), Patient);
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
