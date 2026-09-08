using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Where the rules JSON comes from.
///
/// <para><see cref="RulesRepository"/> used to call <c>File.ReadAllText</c> itself, which tied
/// the engine to a filesystem. A browser has none, so a Blazor WebAssembly build could never
/// have run the engine at all. These tests hold the seam open: the repository must work with
/// no disk access whatsoever, and must keep working from disk for the CLI.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class RulesSourceTests
{
    private readonly RulesFixture _f;

    public RulesSourceTests(RulesFixture fixture) => _f = fixture;

    /// <summary>Every rules file, read off disk once so a memory-backed source can be built.</summary>
    private static Dictionary<string, string> AllRulesJson() =>
        RulesRepository.DataFileNames.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(RulesFixture.DataPath, name)),
            StringComparer.Ordinal);

    // ── The seam ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The whole point: a repository built from strings in memory answers exactly as one
    /// reading the directory. This is the Blazor WebAssembly path — the host fetches the
    /// files over HTTP at startup, then the engine runs synchronously as it always has.
    /// </summary>
    [Fact]
    public void TheEngineRunsWithNoFilesystemAtAll()
    {
        var rules = new RulesRepository(new InMemoryRulesSource(AllRulesJson()));

        Assert.Equal(_f.Rules.Powers.Count, rules.Powers.Count);
        Assert.Equal(_f.Rules.Sources.Count, rules.Sources.Count);
        Assert.Equal(_f.Rules.Pros.Count, rules.Pros.Count);
        Assert.Equal(_f.Rules.GearFeatures.Count, rules.GearFeatures.Count);
        Assert.Equal(_f.Rules.CreationRules.OptionalPackages.Count,
                     rules.CreationRules.OptionalPackages.Count);

        // And the costing built on top of it agrees, not just the loading.
        var costs = new CostCalculator(rules);
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 8));

        Assert.Equal(_f.Costs.TotalCost(sheet), costs.TotalCost(sheet));
    }

    /// <summary>
    /// Files in <c>data/rules/</c> that <see cref="RulesRepository"/> deliberately does not load, so
    /// the check below is a check and not a running total. <b>It is an allowlist of five and every
    /// entry has to earn its place, because "the list has to match what is shipped" is the whole
    /// point of that check</b> — an exemption is how it stops being one.
    ///
    /// <para><c>meta.json</c> is provenance rather than rules. The other four are Chapter 6 —
    /// <c>gear.json</c> pp.88–93, and <c>gadgets.json</c>, <c>vehicles.json</c> and
    /// <c>headquarters.json</c> pp.94–103 — extracted before anything consumes them: the data is
    /// verified first and wired up second, which is the order the 141 Powers were done in and the
    /// order that made them trustworthy. <c>EquipmentDataTests</c> and <c>Chapter6RulesDataTests</c>
    /// read them meanwhile. Putting one on <see cref="RulesRepository.DataFileNames"/> makes every
    /// browser fetch it before its first render, so it is a deliberate act by the slice that adds
    /// a collection for it — at which point its entry comes out, and the guard below fails if only
    /// one of the two happens.</para>
    /// </summary>
    private static readonly string[] NotLoadedByTheRepository =
        ["meta.json", "gear.json", "gadgets.json", "vehicles.json", "headquarters.json"];

    /// <summary>
    /// DataFileNames is the contract a self-loading host works from. If a rules file is
    /// added and not listed, a browser build silently fetches an incomplete rules set — so
    /// the list has to match what is actually shipped, bar the files named above.
    /// </summary>
    /// <remarks>
    /// <para><b>Two kinds of file are off the list on purpose, and each one is named rather than
    /// filtered by a pattern.</b> <c>meta.json</c> is provenance rather than rules. The three
    /// Chapter 6 files are <em>extracted but not yet consumed</em>: putting one on the contract
    /// makes the browser fetch it before its first render, which is a decision about the payload
    /// and belongs to the slice that teaches <c>CostCalculator</c> what a vehicle or a
    /// headquarters costs — not to the slice that read the pages. <see cref="Chapter6RulesDataTests"/>
    /// holds them to the rulebook meanwhile, which is what stops them being unread data.</para>
    ///
    /// <para><b>Each exclusion has to still exist</b>, or an exemption for something that is no
    /// longer there sits here permitting a name for nothing — the shape this repository has been
    /// bitten by in <c>TraitCapReadTests</c>.</para>
    /// </remarks>
    [Fact]
    public void DataFileNamesListsEveryShippedRulesFile()
    {
        var onDisk = Directory.GetFiles(RulesFixture.DataPath, "*.json")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => !NotLoadedByTheRepository.Contains(n, StringComparer.Ordinal))
            .Order();

        Assert.Equal(onDisk, RulesRepository.DataFileNames.Order());
    }

    /// <summary>
    /// <b>An exemption for a file that is not there permits a name for nothing.</b> Each entry above
    /// has to name a file that exists and that the repository really does not load — otherwise a
    /// rules file could be added under a name the allowlist happens to carry and go unfetched in
    /// every browser, which is exactly the failure the check above exists for.
    /// </summary>
    [Fact]
    public void EveryFileExemptedFromThatListIsThereAndIsReallyNotLoaded()
    {
        Assert.NotEmpty(NotLoadedByTheRepository);

        foreach (var name in NotLoadedByTheRepository)
        {
            Assert.True(File.Exists(Path.Combine(RulesFixture.DataPath, name)),
                $"{name} is exempted from the shipped-files check and is not in data/rules/. An "
                + "exemption for a file that has gone permits the name for nothing — delete it.");

            Assert.DoesNotContain(name, RulesRepository.DataFileNames);
        }
    }

    [Fact]
    public void EveryListedFileActuallyLoads()
    {
        var rules = new RulesRepository(new InMemoryRulesSource(AllRulesJson()));

        // Touch every collection so a name that is listed but unreadable fails here.
        Assert.NotEmpty(rules.Tiers);
        Assert.NotEmpty(rules.Abilities);
        Assert.NotEmpty(rules.Talents);
        Assert.NotEmpty(rules.Powers);
        Assert.NotEmpty(rules.Pros);
        Assert.NotEmpty(rules.Cons);
        Assert.NotEmpty(rules.Flaws);
        Assert.NotEmpty(rules.Perks);
        Assert.NotEmpty(rules.GearFeatures);
        Assert.NotEmpty(rules.Sources);
        Assert.NotEmpty(rules.CreationRules.OptionalPackages);
    }

    // ── Failure is loud ──────────────────────────────────────────────────────

    /// <summary>
    /// A missing rules file is a broken deployment, not a case to degrade through. Both
    /// sources say which file and where they looked, because the alternative is a
    /// null-reference somewhere far away from the cause.
    /// </summary>
    [Fact]
    public void AMissingFileFromMemoryNamesTheFileAndWhatWasSupplied()
    {
        var partial = AllRulesJson();
        partial.Remove("powers.json");

        var rules = new RulesRepository(new InMemoryRulesSource(partial));
        var ex = Assert.Throws<FileNotFoundException>(() => rules.Powers);

        Assert.Contains("powers.json", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RulesRepository.DataFileNames), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingFileFromDiskNamesTheDirectoryItLookedIn()
    {
        var empty = Path.Combine(Path.GetTempPath(), "pp-no-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var rules = new RulesRepository(empty);
            var ex = Assert.Throws<FileNotFoundException>(() => rules.Tiers);

            Assert.Contains("tiers.json", ex.Message, StringComparison.Ordinal);
            Assert.Contains(empty, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    // ── The existing entry points still work ─────────────────────────────────

    [Fact]
    public void TheFileSystemConstructorsStillWork()
    {
        // The CLI uses both of these; decoupling must not have changed them.
        Assert.NotEmpty(new RulesRepository(RulesFixture.DataPath).Powers);

        var repoRoot = Directory.GetParent(Directory.GetParent(RulesFixture.DataPath)!.FullName)!.FullName;
        Assert.NotEmpty(RulesRepository.FromBasePath(repoRoot).Powers);
    }

    [Fact]
    public void LoadingStaysLazyAndCachedWhateverTheSource()
    {
        var reads = new List<string>();
        var rules = new RulesRepository(new CountingSource(AllRulesJson(), reads));

        Assert.Empty(reads);                    // nothing read until asked

        _ = rules.Powers;
        _ = rules.Powers;
        _ = rules.GetPower("blast");

        Assert.Equal(["powers.json"], reads);   // read once, then cached
    }

    private sealed class CountingSource : IRulesSource
    {
        private readonly InMemoryRulesSource _inner;
        private readonly List<string> _reads;

        public CountingSource(IReadOnlyDictionary<string, string> files, List<string> reads)
        {
            _inner = new InMemoryRulesSource(files);
            _reads = reads;
        }

        public string ReadAllText(string fileName)
        {
            _reads.Add(fileName);
            return _inner.ReadAllText(fileName);
        }
    }
}
