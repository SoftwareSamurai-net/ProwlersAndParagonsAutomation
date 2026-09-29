using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Item 21's family findings, surfaced: <see cref="AlternateFormRosterNotes"/> is the one place a
/// browser calls <see cref="AlternateForms.Families"/> to draw a note beside a roster or switcher
/// row — <c>CharacterManager.razor</c> and <c>CharacterSwitcher.razor</c> both read its result
/// rather than holding a second copy of the engine's rule.
/// </summary>
public sealed class AlternateFormRosterNotesTests
{
    private static RulesRepository Rules { get; } = RulesRepository.FromBasePath(RepoRoot());
    private static CostCalculator Costs { get; } = new(Rules);
    private static DerivedStatsCalculator Derived { get; } = new(Rules);
    private static AlternateForms Forms => new(Rules, Costs, Derived);

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

    private static CharacterSheet Standard(string name)
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard", Name = name };
        sheet.SelectedPowers.Add(new SelectedPower(AlternateForms.PowerId, 0) { Units = 3 });
        return sheet;
    }

    private static VariantRow RootRow(string id, string name) => new(id, name, null);

    private static VariantRow FormRow(string id, string name, string rootId) =>
        new(id, name, new CharacterVariant(rootId, CharacterVariant.AlternateForm));

    [Fact]
    public async Task AFamilyWithNoAlternateFormChildDoesNothingAndLoadsNoSheet()
    {
        var families = CharacterVariants.Group(
            [RootRow("root", "Airmid"),
             new VariantRow("later", "Airmid (later)", new CharacterVariant("root", CharacterVariant.Later))]);

        var loaded = new List<string>();
        Task<CharacterSheet?> Load(string id)
        {
            loaded.Add(id);
            return Task.FromResult<CharacterSheet?>(Standard(id));
        }

        var result = await AlternateFormRosterNotes.BuildAsync(families, Forms, Load);

        Assert.Empty(loaded);
        Assert.Empty(result.ByMemberId);
        Assert.Empty(result.SharedResolveByRootId);
    }

    [Fact]
    public async Task AFormBuiltToTheRootsCapPrintsTheSharedPoolAndNoWarning()
    {
        var families = CharacterVariants.Group(
            [RootRow("airmid", "Airmid"), FormRow("scathach", "Scáthach", "airmid")]);

        var root = Standard("Airmid");
        var form = Standard("Scáthach");
        form.Variant = new CharacterVariant("airmid", CharacterVariant.AlternateForm);
        form.SelectedTierId = "street_level";
        form.TraitCapRank = Rules.GetTier("standard")!.TraitCapRank; // the root's own cap
        form.SelectedPowers.Clear();
        form.SelectedPowers.Add(new SelectedPower(AlternateForms.PowerId, 0) { Units = 1 });
        root.SelectedPowers.Clear();
        root.SelectedPowers.Add(new SelectedPower(AlternateForms.PowerId, 0) { Units = 1 });

        var sheets = new Dictionary<string, CharacterSheet> { ["airmid"] = root, ["scathach"] = form };

        var result = await AlternateFormRosterNotes.BuildAsync(
            families, Forms, id => Task.FromResult<CharacterSheet?>(sheets[id]));

        Assert.True(result.SharedResolveByRootId.TryGetValue("airmid", out var pool));
        Assert.StartsWith("Shared Resolve pool:", pool);
        Assert.DoesNotContain(result.ByMemberId.GetValueOrDefault("scathach", []),
            n => n.Text.Contains("Trait Cap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFormsCapNotTheRootsPrintsTheEngineFindingOnItsOwnRow()
    {
        var families = CharacterVariants.Group(
            [RootRow("airmid", "Airmid"), FormRow("scathach", "Scáthach", "airmid")]);

        var root = Standard("Airmid");
        var form = Standard("Scáthach");
        form.Variant = new CharacterVariant("airmid", CharacterVariant.AlternateForm);
        form.SelectedTierId = "street_level";
        form.TraitCapRank = 16; // not the root's 12d, and above street_level's own 8d
        form.SelectedPowers.Clear();
        form.SelectedPowers.Add(new SelectedPower(AlternateForms.PowerId, 0) { Units = 1 });
        root.SelectedPowers.Clear();
        root.SelectedPowers.Add(new SelectedPower(AlternateForms.PowerId, 0) { Units = 1 });

        var sheets = new Dictionary<string, CharacterSheet> { ["airmid"] = root, ["scathach"] = form };

        var result = await AlternateFormRosterNotes.BuildAsync(
            families, Forms, id => Task.FromResult<CharacterSheet?>(sheets[id]));

        var notes = result.ByMemberId["scathach"];
        Assert.Contains(notes, n => !n.Warning && n.Text.Contains("Trait Cap", StringComparison.Ordinal));
    }

    /// <summary>
    /// A member this build could not read is said, not guessed past — and nothing else about the
    /// family is reported, since the engine cannot honestly answer for a set it was not given in
    /// full.
    /// </summary>
    [Fact]
    public async Task AMemberThatFailsToLoadIsReportedAndNothingElseIsGuessedAt()
    {
        var families = CharacterVariants.Group(
            [RootRow("airmid", "Airmid"), FormRow("scathach", "Scáthach", "airmid")]);

        Task<CharacterSheet?> Load(string id) =>
            Task.FromResult(id == "airmid" ? Standard("Airmid") : null);

        var result = await AlternateFormRosterNotes.BuildAsync(families, Forms, Load);

        var notes = result.ByMemberId["scathach"];
        Assert.Contains(notes, n => n.Warning && n.Text.Contains("could not be loaded", StringComparison.Ordinal));
        Assert.False(result.ByMemberId.ContainsKey("airmid"));
        Assert.Empty(result.SharedResolveByRootId);
    }
}
