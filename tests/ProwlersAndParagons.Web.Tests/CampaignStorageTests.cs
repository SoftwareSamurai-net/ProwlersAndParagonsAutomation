using System.Text.Json;
using ProwlersAndParagonsAutomation.Sheets;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Campaigns, from the browser's side: what is stored, what is inherited, what is only reported,
/// and — the one that matters most — what a character in no campaign at all is not affected by.
///
/// <para><b>A campaign lives on an account or it does not exist</b>, so everything here goes
/// through <see cref="FakeApi"/> rather than through local storage. That is a change: campaigns
/// used to have a browser half, written before there was a screen. A campaign is the thing two
/// accounts hand a snapshot between, and one kept in a single browser can never receive a
/// submission, hold a clone, or be joined by the code it would advertise — see
/// <see cref="AccountCampaignStore"/>, which no longer falls back.</para>
///
/// <para><b>The characters are still in local storage</b>, and the same
/// <see cref="FakeLocalStorage"/> the character tests use — because a character is worth keeping
/// for somebody who has not signed in, and the two prefixes are separate strings in the same
/// browser.</para>
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

    private sealed class FixedIdentity(Identity who) : IIdentitySource
    {
        public ValueTask<Identity> CurrentAsync() => ValueTask.FromResult(who);
    }

    /// <summary>An account, which is the only place a campaign can be.</summary>
    private static readonly Identity Signed = new("u_gm", "The GM");

    private static SavedCharacters FreshCharacters(FakeLocalStorage storage) =>
        new(storage, Costs, Validator, new LocalIdentity());

    /// <summary>
    /// A server, and a store wired to it as whoever is named.
    ///
    /// <para>What the real server <em>does</em> with a campaign — the SQL scoping, the join code's
    /// unique index, the compare-and-swap — is driven against the real code in real SQLite by
    /// <c>tests/worker/campaigns.test.mjs</c> and <c>memberships.test.mjs</c>. What is pinned here
    /// is the browser's own half: which store it reaches, and what it does with each answer.</para>
    /// </summary>
    private static (AccountCampaignStore Store, FakeApi Server) FreshStore(Identity? who = null)
    {
        var server = new FakeApi();

        if (who is { } signed && signed.Key != Identity.Anonymous.Key)
        {
            server.SignedIn = (signed.Key, signed.DisplayName ?? signed.Key);
        }

        var http = new HttpClient(server) { BaseAddress = new Uri("https://pp.example.test/") };

        return (new AccountCampaignStore(
            new FixedIdentity(who ?? Identity.Anonymous), new ApiCampaignStore(http)), server);
    }

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
        var (store, _) = FreshStore(Signed);

        // A campaign really is stored, so resolution has something it *could* return. Without
        // this the test would pass against a store that can find nothing at all.
        Assert.True(await store.SaveAsync(ACampaign(tierId: "iconic", traitCap: 20, unlimited: true)));

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
            var sheet = new CharacterSheet
            {
                SelectedTierId = tier.Id,
                Name = $"A {tier.Name} build",
                AbilityRanks = { ["might"] = 6, ["agility"] = 4 },
                TalentRanks = { ["athletics"] = 3 },
            };

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

        Assert.Equal(CampaignJoinOutcome.Inherited, CampaignJoin.Apply(sheet, campaign).Outcome);

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

        var sheet = new CharacterSheet
        {
            SelectedTierId = "street_level",
            Name = "Ninefold",
            AbilityRanks = { ["might"] = 6 },
        };

        var campaign = ACampaign(tierId: "legendary", unlimited: true);
        Assert.True(await FreshStore(Signed).Store.SaveAsync(campaign));

        // At the join.
        Assert.Equal(CampaignJoinOutcome.TierDisagrees, CampaignJoin.Apply(sheet, campaign).Outcome);
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
        Assert.Contains("different tier", finding.Message, StringComparison.Ordinal);

        // The sentence names neither tier: an id is not what a tier is called, and a screen looks
        // both up. The two ids travel as fields instead.
        Assert.DoesNotContain("street_level", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("legendary", finding.Message, StringComparison.Ordinal);
        Assert.Equal("street_level", finding.CharacterTierId);
        Assert.Equal("legendary", finding.CampaignTierId);
    }

    /// <summary>
    /// <b>A campaign's Trait Cap lands in a character that has none.</b> That is the finding this
    /// whole slice came from — a table caps a non-superhuman at 6d and the character has to carry
    /// it, because the character is what everything computes from. Inherited alongside the tier
    /// where the tier was empty, and alongside nothing where it was not: an empty field is filled
    /// either way.
    ///
    /// <para>The positive control is the figure it moves. Resolve is measured from the cap, so an
    /// inherited cap is a real change to the character rather than a label — and a join that
    /// wrote the field and nothing that read it would pass an assertion about the field alone.</para>
    /// </summary>
    [Fact]
    public void AnEmptyTraitCapInheritsTheCampaigns()
    {
        // With no tier of its own: the cap arrives with the tier.
        var fresh = new CharacterSheet();
        Assert.Equal(CampaignJoinOutcome.Inherited,
            CampaignJoin.Apply(fresh, ACampaign(tierId: "standard", traitCap: 6)).Outcome);
        Assert.Equal(6, fresh.TraitCapRank);

        // With a tier that already agrees: the cap still arrives, because the field was empty.
        var built = new CharacterSheet
        {
            SelectedTierId = "standard",
            AbilityRanks = { ["might"] = 4 },
        };

        var before = Derived.CalculateResolve(built);
        Assert.Equal(16, before);   // (12 - 4) x 2 at the Standard tier's cap

        Assert.Equal(CampaignJoinOutcome.Joined,
            CampaignJoin.Apply(built, ACampaign(tierId: "standard", traitCap: 6)).Outcome);

        Assert.Equal(6, built.TraitCapRank);
        Assert.Equal(4, Derived.CalculateResolve(built));   // (6 - 4) x 2
    }

    /// <summary>
    /// <b>A cap the character already has is kept, and the disagreement is handed back.</b> Same
    /// rule as the tier and for a sharper reason: writing over it would move Resolve on somebody's
    /// finished character in the course of typing a join code.
    /// </summary>
    [Fact]
    public void ATraitCapTheCharacterAlreadyHasIsKeptAndTheDisagreementIsReported()
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard", TraitCapRank = 8 };
        var campaign = ACampaign(tierId: "standard", traitCap: 6);

        Assert.Equal(CampaignJoinOutcome.Joined, CampaignJoin.Apply(sheet, campaign).Outcome);
        Assert.Equal(8, sheet.TraitCapRank);

        var finding = CampaignJoin.Inspect(sheet, campaign);

        Assert.NotNull(finding);
        Assert.Equal("CAMPAIGN_TRAIT_CAP_MISMATCH", finding!.Code);
        Assert.Equal(8, finding.CharacterTraitCapRank);
        Assert.Equal(6, finding.CampaignTraitCapRank);

        // The sentence carries neither figure, the way the tier finding carries neither id.
        Assert.DoesNotContain("8d", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("6d", finding.Message, StringComparison.Ordinal);

        // And agreement is not a finding, so the check above is not satisfied by any two caps.
        var agreeing = new CharacterSheet { SelectedTierId = "standard", TraitCapRank = 6 };
        Assert.Null(CampaignJoin.Inspect(agreeing, campaign));
    }

    /// <summary>
    /// <b>A join says which settings it actually took, and every combination is one of them.</b>
    ///
    /// <para>The outcome alone cannot carry that sentence and a screen reading it had to guess.
    /// It guessed wrong: "Its tier and its Trait Cap are now yours" was printed over a join that
    /// took the tier and left a cap the character already had, because the write is <c>??=</c>
    /// and is silent by construction. A message claiming a change nobody made is worse than no
    /// message.</para>
    ///
    /// <para>Four rows and not three, because "took neither" is the control: a result that
    /// reported everything taken would satisfy the three positive cases on its own.</para>
    /// </summary>
    [Theory]
    // an empty character, and a game with both settings — both are taken
    [InlineData(null, null, "standard", 6, true, true)]
    // an empty character, and a game with no cap of its own — the tier alone
    [InlineData(null, null, "standard", null, true, false)]
    // a character already at this tier with no cap — the cap alone
    [InlineData("standard", null, "standard", 6, false, true)]
    // a character with a cap of its own, at a game that sets one — nothing is taken
    [InlineData("standard", 8, "standard", 6, false, false)]
    public void AJoinSaysWhichSettingsItTook(
        string? tier, int? cap, string? gameTier, int? gameCap, bool takesTier, bool takesCap)
    {
        var sheet = new CharacterSheet { SelectedTierId = tier, TraitCapRank = cap };

        var result = CampaignJoin.Apply(sheet, ACampaign(tierId: gameTier, traitCap: gameCap));

        Assert.Equal(takesTier, result.TookTier);
        Assert.Equal(takesCap, result.TookTraitCap);

        // And the flags describe the sheet, rather than being a second opinion beside it.
        Assert.Equal(takesTier ? gameTier : tier, sheet.SelectedTierId);
        Assert.Equal(takesCap ? gameCap : cap, sheet.TraitCapRank);
    }

    /// <summary>
    /// A refused join took nothing, and says so — the fourth state, which the theory above cannot
    /// reach because every row of it is a join that lands.
    /// </summary>
    [Fact]
    public void ARefusedJoinTookNothing()
    {
        var sheet = new CharacterSheet { SelectedTierId = "street_level" };

        var result = CampaignJoin.Apply(sheet, ACampaign(tierId: "standard", traitCap: 6));

        Assert.Equal(CampaignJoinOutcome.TierDisagrees, result.Outcome);
        Assert.False(result.TookTier);
        Assert.False(result.TookTraitCap);
        Assert.Null(sheet.TraitCapRank);
    }

    /// <summary>
    /// A campaign that names no tier overrules nobody, and a campaign that agrees is simply
    /// joined. Both are the "nothing to disagree about" half of the rule above.
    /// </summary>
    [Fact]
    public void ACampaignWithNoTierOverrulesNobody()
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard" };

        Assert.Equal(CampaignJoinOutcome.Joined, CampaignJoin.Apply(sheet, ACampaign(tierId: null)).Outcome);
        Assert.Equal("standard", sheet.SelectedTierId);
        Assert.Null(CampaignJoin.Inspect(sheet, ACampaign(tierId: null)));

        var agreeing = new CharacterSheet { SelectedTierId = "standard" };
        Assert.Equal(CampaignJoinOutcome.Joined, CampaignJoin.Apply(agreeing, ACampaign()).Outcome);
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
        var (store, _) = FreshStore(Signed);
        var campaign = ACampaign();

        Assert.True(await store.SaveAsync(campaign));

        var sheet = new CharacterSheet { SelectedTierId = "standard", CampaignId = campaign.Id };

        Assert.Null(CampaignJoin.Inspect(sheet, await store.ForAsync(sheet)));

        await store.DeleteAsync(campaign.Id);

        Assert.Equal(campaign.Id, sheet.CampaignId);
        Assert.Empty(await store.ListAsync());

        var finding = CampaignJoin.Inspect(sheet, await store.ForAsync(sheet));
        Assert.NotNull(finding);
        Assert.Equal("UNKNOWN_CAMPAIGN", finding!.Code);
        Assert.Contains("not here", finding.Message, StringComparison.Ordinal);
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
    /// A campaign never touches this browser's storage, and the character slot least of all.
    ///
    /// <para><b>This test used to assert the opposite</b> — that a campaign is kept under
    /// <c>pp.campaign.v1</c>, beside the characters — and the reversal is the decision recorded on
    /// <see cref="AccountCampaignStore"/>: a campaign kept in one browser can never receive a
    /// submission or be joined by the code it advertises. What survives unchanged is the half that
    /// mattered: <c>pp.character.v1</c> keeps meaning exactly what it always did.</para>
    ///
    /// <para><b>Nothing was lost by deleting the local store.</b> No screen had ever created a
    /// campaign, so no visitor could have been holding one under that key.</para>
    /// </summary>
    [Fact]
    public async Task ACampaignNeverTouchesThisBrowsersStorage()
    {
        var storage = new FakeLocalStorage();
        var (store, _) = FreshStore(Signed);

        // A character *is* written here, which is the positive control: a fake nothing wrote to
        // would satisfy every absence below for free.
        await FreshCharacters(storage).SaveCurrentAsync(
            Identity.Anonymous,
            new CharacterSheet { SelectedTierId = "standard", Name = "Ninefold" },
            SheetMode.Hero);

        Assert.NotNull(storage.Peek("pp.character.v1"));

        Assert.True(await store.SaveAsync(ACampaign()));

        Assert.Null(storage.Peek("pp.campaign.v1"));
        Assert.Null(storage.Peek("pp.campaign.v1.index"));
        Assert.Null(storage.Peek("pp.campaign.v1.g_0000000000000000000000"));
        Assert.All(storage.Calls,
            c => Assert.DoesNotContain("campaign", c.Key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A campaign round-trips through the account with every field intact.</summary>
    [Fact]
    public async Task ACampaignRoundTrips()
    {
        var (store, _) = FreshStore(Signed);
        var campaign = ACampaign(tierId: "high_level", traitCap: 14, unlimited: true);

        Assert.True(await store.SaveAsync(campaign));

        var restored = await store.LoadAsync(campaign.Id);

        Assert.Equal(campaign, restored);

        // Field by field as well as by record equality, and the cap especially: it is the one
        // field nothing in the application reads yet, so record equality is the only thing that
        // would notice it going missing — and record equality is exactly what a serializer
        // dropping an unknown key would still satisfy if the expectation were rebuilt from the
        // same round trip.
        Assert.Equal("The Long Winter", restored!.Name);
        Assert.Equal("high_level", restored.TierId);
        Assert.Equal(14, restored.TraitCapRank);
        Assert.True(restored.UnlimitedBudget);
    }

    /// <summary>
    /// Nobody signed in has no campaigns, cannot make one, and is told so rather than being
    /// written somewhere they can never be read back from.
    ///
    /// <para><b>The refusal is the point.</b> A save that answered true and went into this browser
    /// would give a GM a join code no player could ever redeem — a control that looks like it
    /// worked and did not, which is the shape of defect this repository keeps finding late.</para>
    /// </summary>
    [Fact]
    public async Task AnAnonymousVisitorHasNoCampaignsAndCannotMakeOne()
    {
        var (store, server) = FreshStore();

        Assert.False(await store.IsAvailableAsync());
        Assert.False(await store.SaveAsync(ACampaign()));
        Assert.Empty(await store.ListAsync());
        Assert.Null(await store.LoadAsync("g_0000000000000000000000"));

        // Nothing was even asked of the server, which is what makes the refusal a refusal rather
        // than a 401 the store swallowed. The positive control is the signed-in case beside it.
        Assert.Empty(server.Asked);

        var (signed, asked) = FreshStore(Signed);
        Assert.True(await signed.IsAvailableAsync());
        Assert.True(await signed.SaveAsync(ACampaign()));
        Assert.NotEmpty(asked.Asked);
    }

    /// <summary>
    /// A character that names a campaign this account does not hold is reported, never repaired —
    /// and that is also what a signed-out visitor sees.
    /// </summary>
    [Fact]
    public async Task ACampaignThatIsNotHereIsReportedRatherThanCleared()
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId = "standard",
            CampaignId = "g_9999999999999999999999",
        };

        foreach (var (what, store) in new[]
                 {
                     ("signed out", FreshStore().Store),
                     ("signed in, no such campaign", FreshStore(Signed).Store),
                 })
        {
            var finding = CampaignJoin.Inspect(sheet, await store.ForAsync(sheet));

            Assert.NotNull(finding);
            Assert.Equal("UNKNOWN_CAMPAIGN", finding!.Code);
            Assert.Equal("g_9999999999999999999999", sheet.CampaignId);
            Assert.Equal("standard", sheet.SelectedTierId);
            Assert.True(what.Length > 0);
        }
    }

    /// <summary>
    /// A server that cannot be reached says nothing rather than reporting a save that went
    /// nowhere — the defect an adversarial review found on the character side.
    /// </summary>
    [Fact]
    public async Task AnUnreachableServerIsReportedRatherThanClaimed()
    {
        var server = new FakeApi { SignedIn = ("u_gm", "The GM"), Unreachable = true };
        var http = new HttpClient(server) { BaseAddress = new Uri("https://pp.example.test/") };
        var store = new AccountCampaignStore(new FixedIdentity(Signed), new ApiCampaignStore(http));

        Assert.False(await store.SaveAsync(ACampaign()));
        Assert.Null(await store.LoadAsync("g_0000000000000000000000"));
        Assert.Empty(await store.ListAsync());
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
            var id = StoredCampaign.NewId();

            Assert.Matches("^g_[A-Za-z0-9_-]{22}$", id);
        }
    }

    /// <summary>
    /// A campaign with no name is listed under a name rather than under a blank, on both sides —
    /// one spelling of that rule, followed by the server's own default.
    /// </summary>
    [Fact]
    public void AnUnnamedCampaignStillHasSomethingToBeListedUnder()
    {
        Assert.Equal("Unnamed campaign", StoredCampaign.LabelFor(ACampaign(name: "")));
        Assert.Equal("Unnamed campaign", StoredCampaign.LabelFor(ACampaign(name: "   ")));
        Assert.Equal("Nightfall", StoredCampaign.LabelFor(ACampaign(name: "  Nightfall  ")));
    }

    /// <summary>
    /// The join code reaches the browser, because the GM has to be able to read it out — and it is
    /// the one field of a campaign the server can read at all.
    /// </summary>
    [Fact]
    public async Task TheJoinCodeIsListedBesideTheCampaign()
    {
        var (store, _) = FreshStore(Signed);

        Assert.True(await store.SaveAsync(ACampaign()));

        var row = Assert.Single(await store.ListAsync());

        Assert.Equal("The Long Winter", row.Label);
        Assert.False(string.IsNullOrWhiteSpace(row.JoinCode),
            "a campaign nobody can be told the code of is a campaign nobody can join");
    }

    /// <summary>
    /// The code the GM reads out carries its hyphen, and the code on the wire does not.
    ///
    /// <para><b>The hyphen was meant to travel with the code and did not.</b> The server stores
    /// the normalised ten symbols — <c>normaliseJoinCode</c> takes the punctuation out so a player
    /// who types the code without it still gets in — the list answers those, and the screen
    /// printed them, so a code minted as <c>Q4TWX-NPRKM</c> was only ever shown as
    /// <c>Q4TWXNPRKM</c>. Ten unbroken characters is exactly what somebody misreads over a phone,
    /// which is the whole reason the hyphen exists.</para>
    ///
    /// <para>Both halves are asserted: the wire form has no hyphen, and the spoken form does. A
    /// test on one alone would be satisfied by a formatter that had stopped formatting, or by a
    /// hyphen leaking back onto the wire the server compares against.</para>
    /// </summary>
    [Fact]
    public async Task TheSpokenJoinCodeCarriesItsHyphenAndTheWiredOneDoesNot()
    {
        var (store, _) = FreshStore(Signed);

        Assert.True(await store.SaveAsync(ACampaign()));

        var row = Assert.Single(await store.ListAsync());

        Assert.Equal(10, row.JoinCode!.Length);
        Assert.DoesNotContain('-', row.JoinCode);

        Assert.Equal($"{row.JoinCode[..5]}-{row.JoinCode[5..]}", row.Spoken);
    }

    /// <summary>
    /// <b>A campaign written before house rules existed reads back as the game it always was.</b>
    ///
    /// <para><c>StoredCampaign.CurrentVersion</c> is 1 and did not move, which is the whole claim:
    /// the two new fields are nullable with defaults, so an absent key deserialises to null and
    /// null means the book as printed. Bumping the version would have been the alternative and
    /// would have been wrong — a mismatch is discarded in silence, so every campaign anybody had
    /// would have vanished to say something that is true of all of them.</para>
    ///
    /// <para><b>The payload is the literal bytes the store used to write</b>, for the reason
    /// <c>CharacterStoreTests</c> keeps its pre-campaign character verbatim: a payload built by
    /// today's writer proves the writer round-trips itself, which is a different and much weaker
    /// claim than one about what is already in somebody's account.</para>
    /// </summary>
    [Fact]
    public void ACampaignStoredBeforeHouseRulesExistedReadsBackPlayingTheBook()
    {
        const string beforeHouseRules =
            """
            {"Version":1,"Campaign":{"Id":"g_0000000000000000000000","Name":"Pinnacle City",
            "TierId":"standard","TraitCapRank":6,"UnlimitedBudget":false}}
            """;

        var campaign = StoredCampaign.Read(beforeHouseRules.ReplaceLineEndings(""));

        Assert.NotNull(campaign);

        // Everything it did say is still what it says — the control, without which a reader that
        // had started answering an empty campaign would satisfy the three assertions below.
        Assert.Equal("Pinnacle City", campaign!.Name);
        Assert.Equal("standard", campaign.TierId);
        Assert.Equal(6, campaign.TraitCapRank);

        // And the two it could not have said are null, which is the book.
        Assert.Null(campaign.Table);
        Assert.Null(campaign.ImmortalityCost);

        // Null and "every switch off" are the same game, so a screen and the diff treat them
        // alike — that is the property the null is chosen for, rather than a happy accident.
        Assert.True((campaign.Table ?? CampaignTable.Book).IsTheBook);
    }

    /// <summary>
    /// <b>And a character stored before them prices Immortality at the book's 3.</b>
    ///
    /// <para>The other half of the same question, one envelope over:
    /// <c>CharacterSheetJson</c> is what the browser's storage and the <c>build</c> command both
    /// read, and a sheet with no <c>ImmortalityCost</c> key is a sheet at no table. Asserted
    /// against the rulebook's own figure rather than against 3, because the number belongs to
    /// <c>data/rules/</c>.</para>
    /// </summary>
    [Fact]
    public void ACharacterStoredBeforeHouseRulesExistedIsChargedTheBooksPrice()
    {
        const string beforeHouseRules =
            """
            {"SelectedTierId":"standard","Name":"Nine","SelectedPowers":[{"PowerId":"immortality",
            "PurchasedRanks":0,"Pros":[],"Cons":[]}]}
            """;

        var sheet = CharacterSheetJson.Read(beforeHouseRules.ReplaceLineEndings(""), strict: true);

        Assert.NotNull(sheet);
        Assert.Null(sheet!.CampaignTable);
        Assert.Null(sheet.ImmortalityCost);

        Assert.Equal(Rules.GetPower("immortality")!.CostFlat, Costs.TotalPowersCost(sheet));
    }

    /// <summary>
    /// A code that is not the ten symbols this minter produces is shown as it is, rather than cut
    /// in half. A formatter with an opinion about a value it does not recognise is a formatter
    /// that corrupts one.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("SHORT", "SHORT")]
    [InlineData("ELEVENSYMBS", "ELEVENSYMBS")]
    public void AnUnrecognisedJoinCodeIsShownUntouched(string? code, string? shown)
    {
        var row = new SavedCampaignSummary("g_0000000000000000000000", "A game", 0, code);

        Assert.Equal(shown, row.Spoken);
    }
}
