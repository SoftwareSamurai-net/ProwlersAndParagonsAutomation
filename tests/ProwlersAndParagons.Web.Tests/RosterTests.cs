using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Twenty-nine characters, and the two shapes the manager takes because of them: the roster page
/// that draws them all, and the summary the tier page carries in its place.
///
/// <para><b>Split in two on purpose.</b> The grouping and the filtering are decided in
/// <see cref="Roster"/>, which needs no browser — so the first half of this file asserts them
/// directly, and the second half asserts only what rendering can say: that the tier page stopped
/// listing, that the roster page did not, and that the box on it narrows what is drawn.</para>
/// </summary>
public sealed class RosterTests
{
    private static string Text(string markup) =>
        Regex.Replace(Regex.Replace(markup, "<[^>]*>", ""), @"\s+", " ").Trim();

    /// <summary>One index row. The time descends with each call so "recent" has an order to keep.</summary>
    private static SavedCharacterSummary Row(string label, string? campaignId = null, long at = 0) =>
        new($"c_{label.Replace(" ", "", StringComparison.Ordinal)}", label, at, campaignId);

    /// <summary>The games this account can name, and no tiers. Tiers are exercised separately.</summary>
    private static RosterNames Games(params (string Id, string Name)[] games) =>
        new(games.ToDictionary(g => g.Id, g => g.Name, StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

    private const string Elsewhere = "Also on this account";

    // ── What the grouping decides, with nothing rendered ─────────────────────────────────────

    /// <summary>
    /// Games first, then the characters in no game. "In no game" is the absence of the thing
    /// being grouped by, and a list of games that opens with the pile belonging to none reads as
    /// though that pile were the subject.
    /// </summary>
    [Fact]
    public void CharactersInNoGameComeLast()
    {
        var characters = new[]
        {
            Row("Hobbes"),
            Row("Cael Hughes", "g_ash"),
            Row("Absolute"),
            Row("Emir Hughes", "g_ward"),
        };

        var groups = Roster.Group(
            characters, "", RosterOrder.ByGame, Games(("g_ash", "Ashfall"), ("g_ward", "The Quiet Ward")), Elsewhere);

        Assert.Equal(["Ashfall", "The Quiet Ward", Roster.NoGame], groups.Select(g => g.Heading));
    }

    /// <summary>
    /// A character naming a campaign this browser cannot resolve is reported, never repaired —
    /// the same answer <c>UNKNOWN_CAMPAIGN</c> gives one level up. It sorts above "in no game",
    /// because it is a game, just not one that is here.
    /// </summary>
    [Fact]
    public void ACampaignThatCannotBeResolvedGetsAHeadingSayingSo()
    {
        var characters = new[] { Row("Lynchpin", "g_gone"), Row("Effigy"), Row("Schlem", "g_ash") };

        var groups = Roster.Group(
            characters, "", RosterOrder.ByGame, Games(("g_ash", "Ashfall")), Elsewhere);

        Assert.Equal(["Ashfall", Roster.GameNotHere, Roster.NoGame], groups.Select(g => g.Heading));
    }

    /// <summary>
    /// An account whose characters are all in no game has nothing to tell apart, so the heading
    /// stays the sentence the panel has always carried. "In no game" over every row of an account
    /// that has never had one is an answer to a question nobody asked.
    /// </summary>
    [Fact]
    public void OneGroupOfCharactersInNoGameKeepsTheSentenceAboutWhereTheyLive()
    {
        var characters = new[] { Row("Hobbes"), Row("Absolute"), Row("Effigy") };

        var groups = Roster.Group(characters, "", RosterOrder.ByGame, Games(), Elsewhere);

        Assert.Equal(Elsewhere, Assert.Single(groups).Heading);
    }

    /// <summary>
    /// The other half, and it is the half that would be lost by simply suppressing a lone
    /// heading: every character in one real game keeps that game's name, because the name says
    /// something true that the sentence cannot.
    /// </summary>
    [Fact]
    public void OneGroupThatIsARealGameKeepsTheGamesName()
    {
        var characters = new[] { Row("Cael Hughes", "g_ash"), Row("Emir Hughes", "g_ash") };

        var groups = Roster.Group(
            characters, "", RosterOrder.ByGame, Games(("g_ash", "Ashfall")), Elsewhere);

        Assert.Equal("Ashfall", Assert.Single(groups).Heading);
    }

    /// <summary>
    /// One group is the list's own length, which the panel's title already states — so the count
    /// beside a lone heading is the fact-read-twice the panel's own remarks warn about.
    /// </summary>
    [Fact]
    public void ALoneHeadingCarriesNoCount()
    {
        var groups = Roster.Group(
            [Row("Hobbes"), Row("Absolute")], "", RosterOrder.ByGame, Games(), Elsewhere);

        Assert.Null(Assert.Single(groups).Aside);
    }

    /// <summary>
    /// With no filter the count is the group's own size. With one, it says how many of the group
    /// are being hidden — which is the whole point of printing it: a reader can see that a game
    /// holds more than they are being shown.
    /// </summary>
    [Fact]
    public void TheCountSaysWhatIsHiddenOnlyWhileFiltering()
    {
        var characters = new[]
        {
            Row("Cael Hughes", "g_ash"), Row("Emir Hughes", "g_ash"), Row("Marcy Kaplan", "g_ash"),
            Row("Hobbes"),
        };
        var games = Games(("g_ash", "Ashfall"));

        var unfiltered = Roster.Group(characters, "", RosterOrder.ByGame, games, Elsewhere);
        Assert.Equal("3", unfiltered[0].Aside);

        var filtered = Roster.Group(characters, "hughes", RosterOrder.ByGame, games, Elsewhere);
        Assert.Equal("2 of 3", filtered[0].Aside);
    }

    /// <summary>A heading over nothing is furniture. The group goes, and the count on the ones left says why.</summary>
    [Fact]
    public void AGroupWithNothingMatchingIsNotDrawn()
    {
        var characters = new[] { Row("Cael Hughes", "g_ash"), Row("Hobbes") };

        var groups = Roster.Group(
            characters, "hughes", RosterOrder.ByGame, Games(("g_ash", "Ashfall")), Elsewhere);

        Assert.Equal("Ashfall", Assert.Single(groups).Heading);
    }

    /// <summary>
    /// Typing a game's name asks for everyone in it, and that is the question a roster exists to
    /// answer — so the name is matched though a row need not print it. The same bargain a Power's
    /// tags make on the Powers tab.
    /// </summary>
    [Fact]
    public void TheGamesNameIsMatchedThoughARowNeedNotPrintIt()
    {
        var characters = new[] { Row("Cael Hughes", "g_ash"), Row("Hobbes") };

        var groups = Roster.Group(
            characters, "ashfall", RosterOrder.ByGame, Games(("g_ash", "Ashfall")), Elsewhere);

        Assert.Equal(["Cael Hughes"], Assert.Single(groups).Characters.Select(c => c.Label));
    }

    /// <summary>
    /// Narrowing to one game must not rename its heading. Deciding the shape from the matches
    /// would make a heading change identity under somebody's typing, which is worse than one that
    /// is briefly redundant.
    /// </summary>
    [Fact]
    public void AFilterThatLeavesOneGroupDoesNotCollapseItsHeading()
    {
        var characters = new[] { Row("Hobbes"), Row("Cael Hughes", "g_ash") };

        var groups = Roster.Group(
            characters, "hobbes", RosterOrder.ByGame, Games(("g_ash", "Ashfall")), Elsewhere);

        // One group is drawn, and it is still "In no game" rather than the one-group sentence:
        // there are two games in the list, and only one of them matched.
        Assert.Equal(Roster.NoGame, Assert.Single(groups).Heading);
    }

    /// <summary>A–Z is by name, and it is one ungrouped list under the sentence about where they live.</summary>
    [Fact]
    public void AlphabeticalIsOneListByName()
    {
        var characters = new[] { Row("Hobbes", "g_ash"), Row("Absolute"), Row("Marcy Kaplan", "g_ash") };

        var group = Assert.Single(Roster.Group(
            characters, "", RosterOrder.Alphabetical, Games(("g_ash", "Ashfall")), Elsewhere));

        Assert.Equal(Elsewhere, group.Heading);
        Assert.Equal(["Absolute", "Hobbes", "Marcy Kaplan"], group.Characters.Select(c => c.Label));
    }

    /// <summary>
    /// Recent leaves the store's order alone, and that is deliberate rather than unimplemented:
    /// both stores answer most-recently-touched-first, so re-sorting here would be a second
    /// opinion about an order somebody else already decided.
    /// </summary>
    [Fact]
    public void RecentKeepsTheOrderTheStoreAnswered()
    {
        var characters = new[] { Row("Zeta"), Row("Alpha"), Row("Mu") };

        var group = Assert.Single(
            Roster.Group(characters, "", RosterOrder.Recent, Games(), Elsewhere));

        Assert.Equal(["Zeta", "Alpha", "Mu"], group.Characters.Select(c => c.Label));
    }

    // ── What only rendering can say ──────────────────────────────────────────────────────────

    /// <summary>
    /// The tier page carries the panel and no longer the list: what it says instead is how many
    /// others there are and where they are. At twenty-nine that list was the tallest thing on a
    /// page whose job is six cards.
    /// </summary>
    [Fact]
    public async Task TheTierPagesPanelLinksToTheRosterInsteadOfListingIt()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>(p => p.Add(m => m.ListsEveryCharacter, false));

        Assert.Empty(cut.FindAll(".character-list"));
        Assert.Contains(
            cut.FindAll("a").Where(a => a.GetAttribute("href") == "build/characters"),
            a => a.TextContent.Contains('8', StringComparison.Ordinal));
    }

    /// <summary>
    /// The positive control for the guard above, and it is the one that matters: a test asserting
    /// only that the tier page has no list would pass just as well against a manager that had
    /// stopped drawing one anywhere.
    /// </summary>
    [Fact]
    public async Task TheRosterReallyDoesListThem()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>();

        Assert.Equal(8, cut.FindAll(".character-list .open-target").Count);
    }

    /// <summary>Typing narrows what is drawn, and the box says how many of how many survived.</summary>
    [Fact]
    public async Task TypingNarrowsTheList()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>();
        await cut.Find(".options-filter input").InputAsync("Character 3");

        Assert.Single(cut.FindAll(".character-list .open-target"));
        Assert.Contains("1 of 8", Text(cut.Markup), StringComparison.Ordinal);
    }

    /// <summary>
    /// A query nothing matches says so, rather than leaving a panel that looks like an account
    /// with nothing in it. The empty state above is a different sentence about a different thing.
    /// </summary>
    [Fact]
    public async Task AQueryThatMatchesNothingSaysSo()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>();
        await cut.Find(".options-filter input").InputAsync("nobody by that name");

        Assert.Empty(cut.FindAll(".character-list .open-target"));
        Assert.Contains("matches what you typed", Text(cut.Markup), StringComparison.Ordinal);
    }

    /// <summary>
    /// A search box, three order buttons and a set of headings over four characters are furniture
    /// on a list a reader can already see the whole of — the same argument the row's timestamp
    /// makes one threshold down.
    /// </summary>
    [Fact]
    public async Task AShortListGetsNoFilterAndNoOrderControl()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        await Fill(ctx, 4);

        var cut = ctx.Render<CharacterManager>();

        Assert.Equal(4, cut.FindAll(".character-list .open-target").Count);
        Assert.Empty(cut.FindAll(".options-filter"));
        Assert.Empty(cut.FindAll(".roster-order"));
    }

    /// <summary>The order control offers three, and exactly one of them is pressed.</summary>
    [Fact]
    public async Task OneOrderIsPressedAtATime()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>();
        var buttons = cut.FindAll(".roster-order button");

        Assert.Equal(3, buttons.Count);
        Assert.Single(buttons, b => b.GetAttribute("aria-pressed") == "true");

        await cut.FindAll(".roster-order button")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(
            "true", cut.FindAll(".roster-order button")[1].GetAttribute("aria-pressed"));
    }

    /// <summary>
    /// A row says what the character cost and what tier it was built to — the figure a row could
    /// not carry until the index held it, and the reason move 3 exists at all.
    /// </summary>
    [Fact]
    public async Task ARowSaysWhatItCostAndWhatTierItIs()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        var row = Text(cut.Find(".character-list .open-target").InnerHtml);

        // Not a literal figure: the sample is priced by the engine, and pinning the number here
        // would make this a test of `SampleCharacters` that fails whenever the sample changes.
        Assert.Matches(@"\d+ HP · \w", row);
    }

    /// <summary>
    /// A character the engine declines to price shows its tier and no figure. Null is an answer —
    /// the same rule the front door follows — and "0 HP" would be a claim nobody computed.
    /// </summary>
    [Fact]
    public async Task ACharacterTheEngineWillNotPriceShowsNoFigure()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        // A variable-cost Power with no variant chosen — a legitimate half-built state, and the
        // one `CostCalculator` throws for rather than guessing at.
        var halfBuilt = new CharacterSheet { Name = "Unfinished", SelectedTierId = "standard" };
        halfBuilt.SelectedPowers.Add(new SelectedPower("omni_power", 1));

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(SavedCharacters.NewId(), "Unfinished", halfBuilt, SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();
        var row = Text(cut.Find(".character-list .open-target").InnerHtml);

        Assert.DoesNotContain("HP", row, StringComparison.Ordinal);

        // The positive control: the row really is drawn and really does carry the tier, so the
        // absence above is about the figure rather than about a row that said nothing at all.
        Assert.Contains("Standard", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Hero/Villain chip is drawn only where the list holds both. On a roster that is all
    /// Villains it is one word repeated down every row; on a GM's it is the fastest thing to read.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheKindChipIsDrawnOnlyWhereThereAreBoth(bool mixed)
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(),
            mixed ? SheetMode.Villain : SheetMode.Hero);

        var cut = ctx.Render<CharacterManager>();

        Assert.Equal(2, cut.FindAll(".character-list .open-target").Count);
        Assert.Equal(mixed, cut.FindAll(".character-list .kind").Count > 0);
    }

    /// <summary>
    /// The time is drawn under "Recent", where it is what the order means, and not under "By
    /// game", where it was the column that read the same on every row.
    /// </summary>
    [Fact]
    public async Task TheTimeBelongsToTheRecentOrder()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 8);

        var cut = ctx.Render<CharacterManager>();
        Assert.Empty(cut.FindAll(".character-list .when"));

        // "Recent" is the third of the three, in the order the control offers them.
        await cut.FindAll(".roster-order button")[2].ClickAsync(new MouseEventArgs());

        Assert.Equal(8, cut.FindAll(".character-list .when").Count);
    }

    /// <summary>
    /// Typing a tier's name, or "villain", finds the characters that are one — neither of which a
    /// row need print, and both of which are what somebody actually asks a roster of thirty.
    /// </summary>
    [Fact]
    public async Task TheFilterFindsATierAndAKind()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.Api.Limit = 40;

        await Fill(ctx, 7);

        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        var cut = ctx.Render<CharacterManager>();

        await cut.Find(".options-filter input").InputAsync("villain");
        Assert.Single(cut.FindAll(".character-list .open-target"));

        // The tier the samples are built to, typed as the book writes it rather than as the id
        // spells it — which is the whole reason the names are resolved before matching.
        await cut.Find(".options-filter input").InputAsync("Standard");
        Assert.Equal(8, cut.FindAll(".character-list .open-target").Count);
    }

    // ── What reaches the index, and what an older one still does ─────────────────────────────

    /// <summary>
    /// The three fields reach this browser's index, so a row can say what a character is without
    /// the payload being read — which is the constraint the whole design is built around.
    /// </summary>
    [Fact]
    public async Task TheIndexCarriesWhatARowNeedsToSayWhatItIs()
    {
        var storage = new FakeLocalStorage();
        var characters = FreshCharacters(storage);

        await characters.SaveCurrentAsync(
            Identity.Anonymous,
            new CharacterSheet { SelectedTierId = "standard", Name = "Ninefold" },
            SheetMode.Villain);

        var entry = Assert.Single(await characters.ListAsync());

        Assert.Equal("villain", entry.Kind);
        Assert.Equal("standard", entry.TierId);
        Assert.NotNull(entry.Spent);
    }

    /// <summary>
    /// An index written before these three fields existed still lists every character in it —
    /// the same guard <c>CampaignId</c> has, one field on, and for the same reason: a
    /// <c>required</c> member here would empty a returning visitor's list in silence.
    /// </summary>
    [Fact]
    public async Task AnIndexWrittenBeforeTheseFieldsStillLists()
    {
        const string fourFieldIndex =
            """[{"Id":"c_aaaaaaaaaaaaaaaaaaaaaa","Label":"Ninefold","UpdatedAt":1755600000000,"CampaignId":null}]""";

        var entries = JsonSerializer.Deserialize<List<SavedCharacterSummary>>(fourFieldIndex);

        Assert.Equal("Ninefold", Assert.Single(entries!).Label);
        Assert.Null(entries![0].Kind);
        Assert.Null(entries[0].TierId);
        Assert.Null(entries[0].Spent);

        // …and through the store, because a record that deserialises is no use if the list that
        // reads it throws.
        var storage = new FakeLocalStorage();
        storage.Poke("pp.character.v1.index", fourFieldIndex);
        storage.Poke("pp.character.v1.c_aaaaaaaaaaaaaaaaaaaaaa",
            """{"Version":1,"Mode":0,"Sheet":{"SelectedTierId":"standard","Name":"Ninefold"}}""");

        Assert.Equal("Ninefold", Assert.Single(await FreshCharacters(storage).ListAsync()).Label);
    }

    private static readonly RulesRepository Rules = RulesRepository.FromBasePath(FindRepoRoot());
    private static readonly CostCalculator Costs = new(Rules);
    private static readonly CharacterValidator Validator =
        new(Rules, Costs, new DerivedStatsCalculator(Rules));

    private static SavedCharacters FreshCharacters(FakeLocalStorage storage) =>
        new(storage, Costs, Validator, new LocalIdentity());

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

    /// <summary>
    /// Puts <paramref name="count"/> characters on the account, one of which the manager will
    /// treat as the one on screen.
    /// </summary>
    private static async Task Fill(RenderContext ctx, int count)
    {
        var account = ctx.Services.GetRequiredService<ApiCharacterStore>();

        for (var i = 1; i <= count; i++)
        {
            await account.SaveAsync(
                SavedCharacters.NewId(), $"Character {i}", SampleCharacters.Hero(), SheetMode.Hero);
        }
    }
}
