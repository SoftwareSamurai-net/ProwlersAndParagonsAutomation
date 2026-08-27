using System.Text.Json;
using ProwlersAndParagonsAutomation.Sheets;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Campaigns, from the browser's side: what is stored, what is inherited, what is only reported,
/// and — the one that matters most — what a character in no campaign at all is not affected by.
///
/// <para>Everything here shares <see cref="FakeLocalStorage"/> with the character tests on
/// purpose. The two prefixes are separate strings in the same browser, and a test built against a
/// different fake would not catch them colliding.</para>
/// </summary>
public sealed class CampaignStorageTests
{
    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly DerivedStatsCalculator Derived = new(Rules);
    private static readonly CharacterValidator Validator = new(Rules, Costs, Derived);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static SavedCampaigns FreshCampaigns(FakeLocalStorage storage) =>
        new(storage, new LocalIdentity());

    private static SavedCharacters FreshCharacters(FakeLocalStorage storage) =>
        new(storage, Costs, Validator, new LocalIdentity());

    /// <summary>
    /// The chooser, wired for an anonymous visitor — so it never reaches the HTTP half, which is
    /// what <c>tests/worker</c> drives against the real server.
    /// </summary>
    private static AccountCampaignStore FreshStore(FakeLocalStorage storage) =>
        new(new LocalIdentity(), FreshCampaigns(storage),
            new ApiCampaignStore(new HttpClient(new FakeApi())
            {
                BaseAddress = new Uri("https://pp.example.test/"),
            }));

    private static Campaign ACampaign(string id = "g_0000000000000000000000",
        string name = "The Long Winter", string? tierId = "standard",
        int? traitCap = null, bool unlimited = false) =>
        new(id, name, tierId, traitCap, unlimited);

    // ── The most important guard in the slice ────────────────────────────────────────────

    /// <summary>
    /// A character in no campaign is byte-for-byte the character it was before campaigns existed.
    ///
    /// <para><b>Everything else in this slice could be wrong and be a feature that does not work;
    /// this being wrong is every character in the application quietly changing.</b> The failure
    /// shape it guards is specific and tempting: resolution answering a <em>default</em> campaign
    /// for a character that names none, which would apply a tier, a cap and a budget to every
    /// character nobody has put in a game. It is checked against the two sample characters and a
    /// fixture at every tier the rulebook has, on both of the answers the engine gives — the
    /// rendered sheet in full, and the ordered list of finding codes.</para>
    ///
    /// <para><b>Ordered, not a set.</b> A validator's findings are read top to bottom by whoever
    /// is fixing them, and a comparison that sorted them would pass while the order somebody reads
    /// changed underneath them.</para>
    /// </summary>
    [Fact]
    public async Task ACharacterInNoCampaignIsUnchanged()
    {
        var storage = new FakeLocalStorage();
        var store = FreshStore(storage);

        // A campaign really is stored, so resolution has something it *could* return. Without
        // this the test would pass against a store that can find nothing at all.
        Assert.True(await FreshCampaigns(storage).SaveAsync(
            ACampaign(tierId: "iconic", traitCap: 20, unlimited: true)));

        foreach (var (what, sheet) in InNoCampaign())
        {
            var beforeText = Render(sheet);
            var beforeCodes = Codes(sheet);

            // The positive control: these are real answers with real content in them. A renderer
            // that returned "" and a validator that returned nothing would satisfy every
            // comparison below for free.
            Assert.True(beforeText.Length > 200, $"{what} rendered {beforeText.Length} characters");

            // The whole campaign path, exactly as a host would run it.
            var campaign = await store.ForAsync(sheet);
            var finding = CampaignJoin.Inspect(sheet, campaign);

            Assert.Null(campaign);
            Assert.Null(finding);

            Assert.Equal(beforeText, Render(sheet));
            Assert.Equal(beforeCodes, Codes(sheet));
            Assert.Null(sheet.CampaignId);
        }
    }

    /// <summary>
    /// The two samples plus one fixture at every tier the rulebook prints, none of them in a
    /// campaign. The tiers come from the rules data rather than a list here, so a tier added to
    /// the book is covered without anybody remembering this file.
    /// </summary>
    private static IEnumerable<(string What, CharacterSheet Sheet)> InNoCampaign()
    {
        yield return ("the Hero sample", SampleCharacters.Hero());
        yield return ("the Villain sample", SampleCharacters.Villain());

        foreach (var tier in Rules.Tiers)
        {
            var sheet = new CharacterSheet { SelectedTierId = tier.Id, Name = $"A {tier.Name} build" };
            sheet.AbilityRanks["might"] = 6;
            sheet.AbilityRanks["agility"] = 4;
            sheet.TalentRanks["athletics"] = 3;

            yield return ($"a {tier.Id} fixture", sheet);
        }
    }

    private static string Render(CharacterSheet sheet) =>
        CharacterSheetRenderer.RenderText(sheet, Rules, Costs, Derived,
            Validator.Validate(sheet), new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc));

    private static List<string> Codes(CharacterSheet sheet) =>
        [.. Validator.Validate(sheet).Issues.Select(i => i.Code)];

    // ── Inheriting, and the mismatch that is only ever reported ──────────────────────────

    /// <summary>
    /// The positive control for the mismatch guard below, and it is not optional.
    ///
    /// <para><b>"Nothing is changed" is an absence, and an absence is satisfied completely by a
    /// join that does nothing at all.</b> This is the case that must change something: an empty
    /// tier inherits the campaign's, and the sandbox setting travels with it.</para>
    /// </summary>
    [Fact]
    public void AnEmptyTierIsInherited()
    {
        var sheet = new CharacterSheet();
        var campaign = ACampaign(tierId: "high_level", unlimited: true);

        Assert.Equal(CampaignJoinOutcome.Inherited, CampaignJoin.Apply(sheet, campaign));

        Assert.Equal("high_level", sheet.SelectedTierId);
        Assert.True(sheet.UnlimitedBudget);
        Assert.Equal(campaign.Id, sheet.CampaignId);
    }

    /// <summary>
    /// A tier that disagrees is reported and never repaired — at the join, and at every point
    /// afterwards where the character is written down and read back.
    ///
    /// <para><b>Repair would be worse than usual in both directions.</b> Raising the character's
    /// tier turns an illegal character legal in silence; lowering it changes Resolve, which is
    /// <c>(TraitCap − highestRelevantRank) × 2</c>, without anybody asking. So the disagreement is
    /// handed back.</para>
    /// </summary>
    [Fact]
    public async Task AMismatchIsReportedAndNeverRepaired()
    {
        var storage = new FakeLocalStorage();
        var characters = FreshCharacters(storage);

        var sheet = new CharacterSheet { SelectedTierId = "street_level", Name = "Ninefold" };
        sheet.AbilityRanks["might"] = 6;

        var campaign = ACampaign(tierId: "legendary", unlimited: true);
        Assert.True(await FreshCampaigns(storage).SaveAsync(campaign));

        // At the join.
        Assert.Equal(CampaignJoinOutcome.TierDisagrees, CampaignJoin.Apply(sheet, campaign));
        Assert.Equal("street_level", sheet.SelectedTierId);
        Assert.False(sheet.UnlimitedBudget);
        Assert.Null(sheet.CampaignId);

        // After an autosave and a reload — because a repair that happened on the way through
        // storage would be invisible to the assertion above.
        sheet.CampaignId = campaign.Id;
        await characters.SaveCurrentAsync(Identity.Anonymous, sheet, SheetMode.Hero);

        var restored = await characters.LoadCurrentAsync(Identity.Anonymous);
        Assert.NotNull(restored);
        Assert.Equal("street_level", restored!.Value.Sheet.SelectedTierId);
        Assert.False(restored.Value.Sheet.UnlimitedBudget);
        Assert.Equal(campaign.Id, restored.Value.Sheet.CampaignId);

        // And it is still reported, rather than having quietly become agreement.
        var finding = CampaignJoin.Inspect(restored.Value.Sheet, campaign);
        Assert.NotNull(finding);
        Assert.Equal("CAMPAIGN_TIER_MISMATCH", finding!.Code);
        Assert.Equal("street_level", finding.CharacterTierId);
        Assert.Equal("legendary", finding.CampaignTierId);
    }

    /// <summary>
    /// A campaign that names no tier overrules nobody, and a campaign that agrees is simply
    /// joined. Both are the "nothing to disagree about" half of the rule above.
    /// </summary>
    [Fact]
    public void ACampaignWithNoTierOverrulesNobody()
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard" };

        Assert.Equal(CampaignJoinOutcome.Joined, CampaignJoin.Apply(sheet, ACampaign(tierId: null)));
        Assert.Equal("standard", sheet.SelectedTierId);
        Assert.Null(CampaignJoin.Inspect(sheet, ACampaign(tierId: null)));

        var agreeing = new CharacterSheet { SelectedTierId = "standard" };
        Assert.Equal(CampaignJoinOutcome.Joined, CampaignJoin.Apply(agreeing, ACampaign()));
        Assert.Null(CampaignJoin.Inspect(agreeing, ACampaign()));
    }

    /// <summary>
    /// A campaign that has been deleted leaves its members naming it, and they say so.
    ///
    /// <para>Owner-approved: no cascade, and the field is not nulled. Restoring the campaign — or
    /// opening the browser that still has it — puts everything back, which neither of those could.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ADeletedCampaignLeavesItsMembersNamingIt()
    {
        var storage = new FakeLocalStorage();
        var campaigns = FreshCampaigns(storage);
        var store = FreshStore(storage);
        var campaign = ACampaign();

        Assert.True(await campaigns.SaveAsync(campaign));

        var sheet = new CharacterSheet { SelectedTierId = "standard", CampaignId = campaign.Id };

        Assert.Null(CampaignJoin.Inspect(sheet, await store.ForAsync(sheet)));

        await campaigns.DeleteAsync(campaign.Id);

        Assert.Equal(campaign.Id, sheet.CampaignId);
        Assert.Empty(await campaigns.ListAsync());

        var finding = CampaignJoin.Inspect(sheet, await store.ForAsync(sheet));
        Assert.NotNull(finding);
        Assert.Equal("UNKNOWN_CAMPAIGN", finding!.Code);
    }

    // ── Nothing already stored is disturbed ──────────────────────────────────────────────

    /// <summary>
    /// A payload written before <c>CampaignId</c> existed still restores, with every other field
    /// intact and no campaign.
    ///
    /// <para><b>A checked-in literal rather than something this build wrote</b>, because a payload
    /// produced by the current serializer would carry whatever this build happens to emit — which
    /// is exactly the difference the test is about. This string is what the store wrote before
    /// this slice.</para>
    ///
    /// <para><b>And this is the guard on the one mistake that would destroy everyone's data.</b>
    /// <c>StoredCharacter.CurrentVersion</c> is 1 and stayed 1; a mismatch is discarded in
    /// silence, so bumping it would empty every returning visitor's browser and every account.
    /// An absent <c>campaignId</c> deserialising to null is what makes the bump unnecessary.</para>
    /// </summary>
    [Fact]
    public async Task APayloadWrittenBeforeCampaignsRestoresIntact()
    {
        const string beforeCampaigns =
            """
            {"Version":1,"Mode":1,"Sheet":{"IsVillain":true,"UnlimitedBudget":false,
            "SelectedTierId":"standard","SelectedPackageId":"hero","Name":"Ninefold",
            "AbilityRanks":{"might":8,"agility":6},"TalentRanks":{"athletics":4},
            "Flaws":[{"FlawId":"code_of_honor"}]}}
            """;

        var storage = new FakeLocalStorage();
        storage.Poke("pp.character.v1", beforeCampaigns.ReplaceLineEndings(""));

        var restored = await FreshCharacters(storage).LoadCurrentAsync(Identity.Anonymous);

        Assert.NotNull(restored);

        var sheet = restored!.Value.Sheet;

        Assert.Null(sheet.CampaignId);
        Assert.Equal(SheetMode.Villain, restored.Value.Mode);
        Assert.True(sheet.IsVillain);
        Assert.Equal("standard", sheet.SelectedTierId);
        Assert.Equal("hero", sheet.SelectedPackageId);
        Assert.Equal("Ninefold", sheet.Name);
        Assert.Equal(8, sheet.AbilityRanks["might"]);
        Assert.Equal(6, sheet.AbilityRanks["agility"]);
        Assert.Equal(4, sheet.TalentRanks["athletics"]);
        Assert.Equal("code_of_honor", Assert.Single(sheet.Flaws).FlawId);
    }

    /// <summary>
    /// An index written before <c>CampaignId</c> existed still lists every character in it.
    ///
    /// <para><b>Measured rather than reasoned about, and the measurement corrected the reason.</b>
    /// This was written believing the new parameter's <c>= null</c> default was what made an old
    /// index readable. It is not: removing the default was tried and this test stayed green,
    /// because <c>System.Text.Json</c> supplies a positional parameter's own default for a key
    /// absent from the JSON, and <c>default(string?)</c> is null anyway. <b>The guard is real and
    /// was watched to fail</b> — marking the parameter <c>JsonRequired</c> makes every entry throw
    /// and the list come back empty, which is a returning visitor's characters silently
    /// disappearing.</para>
    ///
    /// <para>A literal three-field index is deserialised here, and the same index is put through
    /// <see cref="SavedCharacters.ListAsync"/> — because a record that deserialises fine is no use
    /// if the list that reads it throws.</para>
    /// </summary>
    [Fact]
    public async Task AnIndexWrittenBeforeCampaignsStillLists()
    {
        const string oldIndex =
            """[{"Id":"c_aaaaaaaaaaaaaaaaaaaaaa","Label":"Ninefold","UpdatedAt":1755600000000},{"Id":"c_bbbbbbbbbbbbbbbbbbbbbb","Label":"Vector","UpdatedAt":1755500000000}]""";

        var entries = JsonSerializer.Deserialize<List<SavedCharacterSummary>>(oldIndex);

        Assert.NotNull(entries);
        Assert.Equal(2, entries!.Count);
        Assert.Equal("Ninefold", entries[0].Label);
        Assert.Null(entries[0].CampaignId);

        // …and through the store, with payloads under the keys the entries name.
        var storage = new FakeLocalStorage();
        storage.Poke("pp.character.v1.index", oldIndex);
        storage.Poke("pp.character.v1.c_aaaaaaaaaaaaaaaaaaaaaa",
            """{"Version":1,"Mode":0,"Sheet":{"SelectedTierId":"standard","Name":"Ninefold"}}""");
        storage.Poke("pp.character.v1.c_bbbbbbbbbbbbbbbbbbbbbb",
            """{"Version":1,"Mode":0,"Sheet":{"SelectedTierId":"standard","Name":"Vector"}}""");

        var listed = await FreshCharacters(storage).ListAsync();

        Assert.Equal(["Ninefold", "Vector"], listed.Select(e => e.Label));
        Assert.All(listed, e => Assert.Null(e.CampaignId));
    }

    /// <summary>
    /// A character's campaign reaches the index, so a list can group by game without reading and
    /// costing every payload it draws a row for.
    /// </summary>
    [Fact]
    public async Task TheIndexCarriesTheCampaign()
    {
        var storage = new FakeLocalStorage();
        var characters = FreshCharacters(storage);

        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            Name = "Ninefold",
            CampaignId = "g_0000000000000000000000",
        };

        await characters.SaveCurrentAsync(Identity.Anonymous, sheet, SheetMode.Hero);

        Assert.Equal("g_0000000000000000000000", Assert.Single(await characters.ListAsync()).CampaignId);
    }

    // ── The store itself ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Campaigns are kept under their own top-level prefix, never as a suffix on the character
    /// key — the key that must keep meaning exactly what it always did.
    /// </summary>
    [Fact]
    public async Task CampaignsAreKeptUnderTheirOwnPrefix()
    {
        var storage = new FakeLocalStorage();
        var campaign = ACampaign();

        Assert.True(await FreshCampaigns(storage).SaveAsync(campaign));

        Assert.NotNull(storage.Peek($"pp.campaign.v1.{campaign.Id}"));
        Assert.NotNull(storage.Peek("pp.campaign.v1.index"));

        // The character slot is untouched — no payload, no index, nothing under a suffix of it.
        Assert.Null(storage.Peek("pp.character.v1"));
        Assert.Null(storage.Peek("pp.character.v1.index"));
        Assert.All(storage.Calls,
            c => Assert.DoesNotContain("pp.character.v1", c.Key, StringComparison.Ordinal));
    }

    /// <summary>A campaign round-trips through the browser with every field intact.</summary>
    [Fact]
    public async Task ACampaignRoundTrips()
    {
        var storage = new FakeLocalStorage();
        var campaigns = FreshCampaigns(storage);
        var campaign = ACampaign(tierId: "high_level", traitCap: 14, unlimited: true);

        Assert.True(await campaigns.SaveAsync(campaign));

        Assert.Equal(campaign, await campaigns.LoadAsync(campaign.Id));
    }

    /// <summary>
    /// An index naming a campaign that is not stored is dropped from the list. Storage is the
    /// source of truth for what exists — the same direction <see cref="SavedCharacters"/> takes.
    /// </summary>
    [Fact]
    public async Task AnIndexEntryNamingNothingIsDropped()
    {
        var storage = new FakeLocalStorage();
        var campaigns = FreshCampaigns(storage);

        Assert.True(await campaigns.SaveAsync(ACampaign()));
        const string ghost =
            """,{"Id":"g_9999999999999999999999","Label":"Ghost","UpdatedAt":1}]""";

        var index = storage.Peek("pp.campaign.v1.index")!;

        // The positive control on the doctoring: an index this replace did not touch would leave
        // the assertion below passing because the ghost was never there to drop.
        Assert.EndsWith("]", index, StringComparison.Ordinal);
        storage.Poke("pp.campaign.v1.index", index[..^1] + ghost);

        var listed = await campaigns.ListAsync();

        Assert.Equal("The Long Winter", Assert.Single(listed).Label);
    }

    /// <summary>
    /// A browser that refuses storage says so rather than reporting a save that went nowhere —
    /// the defect an adversarial review found on the character side.
    /// </summary>
    [Fact]
    public async Task ARefusedWriteIsReportedRatherThanClaimed()
    {
        var storage = new FakeLocalStorage { Refuses = true };
        var campaigns = FreshCampaigns(storage);

        Assert.False(await campaigns.SaveAsync(ACampaign()));
        Assert.Null(await campaigns.LoadAsync("g_0000000000000000000000"));
        Assert.Empty(await campaigns.ListAsync());
    }

    /// <summary>
    /// A minted campaign id is the character id shape with the other letter, so neither can be
    /// passed where the other is meant and the server refuses the wrong one.
    /// </summary>
    [Fact]
    public void AMintedIdIsTheShapeTheServerValidates()
    {
        for (var i = 0; i < 20; i++)
        {
            var id = SavedCampaigns.NewId();

            Assert.Matches("^g_[A-Za-z0-9_-]{22}$", id);
        }
    }
}
