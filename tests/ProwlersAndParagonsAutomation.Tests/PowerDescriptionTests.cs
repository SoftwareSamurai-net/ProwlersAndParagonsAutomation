using System.Text;
using System.Text.Json;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Descriptions are what a player reads when choosing a Power, so they have to agree
/// with the mechanics the same entry declares.
///
/// <para>The history this guards: the original descriptions were invented rather than
/// taken from the rulebook, and 44 of the 46 rankless Powers described per-rank scaling
/// that does not exist.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerDescriptionTests
{
    private readonly RulesFixture _f;

    public PowerDescriptionTests(RulesFixture fixture) => _f = fixture;

    /// <summary>Phrases that only make sense for a Power that actually has ranks.</summary>
    private static readonly string[] PerRankClaims =
    [
        "per rank", "each rank", "every rank", "rank determines", "higher ranks",
        "at lower ranks", "ranks allow", "per additional rank"
    ];

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void DescriptionIsVerifiedAgainstTheRulebook(string id) =>
        Assert.True(_f.Rules.GetPower(id)!.DescriptionVerified,
            $"Power '{id}' description is not marked verified.");

    /// <summary>
    /// <b>The verified flag above is a claim about a page somebody read, and until this it
    /// survived any edit to the text it was made about.</b> Armor's whole description was
    /// replaced with "A quiet afternoon in the garden, with tea." and the suite stayed green,
    /// this file included: the checks below see prose of a reasonable length ending in a full
    /// stop, and the one consistency rule there is only fires on a rankless Power claiming
    /// per-rank scaling.
    ///
    /// <para>The description is what a player reads while choosing, so it is the half of an
    /// entry with the least mechanical hold on it and the most direct reach to a person.
    /// <see cref="CanonicalPowerDescriptions"/> records what was verified and says why it is a
    /// digest rather than a similarity score — three other framings were measured and none of
    /// them is a rule.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void ADescriptionIsTheOneThatWasVerified(string id)
    {
        var power = _f.Rules.GetPower(id)!;

        Assert.True(CanonicalPowerDescriptions.Digests.TryGetValue(id, out var verified),
            $"Power '{id}' has no recorded description digest. Read {power.SourceRef}, satisfy "
            + "yourself the description matches the entry, then add it to CanonicalPowerDescriptions.");

        Assert.True(verified == CanonicalPowerDescriptions.DigestOf(power.Description),
            $"Power '{id}' has a different description from the one that was checked against "
            + $"{power.SourceRef}, while still claiming to be verified. Read that page and agree "
            + "the new wording with it, then update the digest in CanonicalPowerDescriptions. "
            + $"It now reads: \"{power.Description}\"");
    }

    /// <summary>
    /// The table is a record of the 141 Powers and nothing else. A stale entry for a Power that
    /// has been renamed or removed is a line nothing checks, which is how a record quietly stops
    /// being one.
    /// </summary>
    [Fact]
    public void TheRecordedDescriptionsAreExactlyThePowersTheRulebookHas()
    {
        Assert.Equal(
            _f.Rules.Powers.Select(p => p.Id).Order(StringComparer.Ordinal),
            CanonicalPowerDescriptions.Digests.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void DescriptionSaysSomethingUseful(string id)
    {
        var description = _f.Rules.GetPower(id)!.Description;

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.True(description.Length >= 25,
            $"Power '{id}' description is too short to help a player choose: '{description}'");
        Assert.EndsWith(".", description.TrimEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PowerDataTests.AllPowerIds), MemberType = typeof(PowerDataTests))]
    public void RanklessPowersDoNotDescribePerRankScaling(string id)
    {
        var power = _f.Rules.GetPower(id)!;
        if (power.RankType is not ("default" or "special")) return;

        // "Its baseline is half your Might" and similar are fine on ranked Powers, but a
        // Power with no rank cannot have anything scale with one.
        var offending = PerRankClaims
            .Where(claim => power.Description.Contains(claim, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offending.Count == 0,
            $"Power '{id}' has no rank ({power.RankType}) but its description says " +
            $"\"{string.Join("\", \"", offending)}\": {power.Description}");
    }

    /// <summary>
    /// <b>A description here is this project's own words, never the book's.</b> The descriptions
    /// were rewritten to carry each entry's mechanics — what is rolled, against what, and what
    /// the net successes buy — which is exactly the rewrite most likely to drift into the page's
    /// phrasing. <c>data/rules/</c> is what the public site serves and <c>data/rulebook/</c> is
    /// not; see <c>docs/guide/rules-engine.md</c>.
    ///
    /// <para>Ten consecutive words, the same measure <c>PlayRulesDataTests</c> uses, and covering
    /// each Power's own Pros and Cons as well as the Power, since a tooltip is drawn from both.</para>
    /// </summary>
    [Fact]
    public void NoPowerTextRepeatsARunOfTheBooksOwnWords()
    {
        const int run = 10;
        var corpus = CorpusWords();

        // Positive control: a sentence printed in Shockwave's own entry has to be found, or a
        // normaliser that produced an empty haystack would pass everything below.
        var control = Runs(Normalise(
            "With 3 or more net successes, the target is also knocked prone and loses their next turn to act."),
            run).ToList();
        Assert.NotEmpty(control);
        Assert.All(control, phrase => Assert.True(
            corpus.Contains(phrase, StringComparison.Ordinal),
            $"The control phrase '{phrase}' was not found in the corpus, so this test is measuring nothing."));

        var texts = _f.Rules.Powers.SelectMany(p =>
            p.PowerPros.Select(o => ($"{p.Id}/{o.Id}", o.Description))
                .Concat(p.PowerCons.Select(o => ($"{p.Id}/{o.Id}", o.Description)))
                .Prepend((p.Id, p.Description)));

        var faults = texts
            .SelectMany(t => Runs(Normalise(t.Item2 ?? string.Empty), run)
                .Where(phrase => corpus.Contains(phrase, StringComparison.Ordinal))
                .Take(1)
                .Select(phrase => $"{t.Item1}: repeats the book verbatim — '{phrase.Trim()}'"))
            .ToList();

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// <b>The summary has to carry the numbers the entry turns on.</b> The originals were accurate
    /// about what a Power was and silent about what it did: Shockwave's said three net successes
    /// knock a target "further and harder" where the page says prone and a lost turn, and the
    /// owner played the Power without knowing. Each row is a Power and a fact its printed entry
    /// states, in the words a reader would look for.
    /// </summary>
    [Theory]
    [InlineData("shockwave", "3 or more")]
    [InlineData("shockwave", "prone")]
    [InlineData("shockwave", "lose their next turn")]
    [InlineData("shockwave", "-1d")]
    [InlineData("hyper_breath", "prone")]
    [InlineData("stun", "1 page per 2 net successes")]
    [InlineData("mind_control", "1 page per 2 net successes")]
    [InlineData("dazzle", "does not stack")]
    [InlineData("deflection", "-2d")]
    [InlineData("slick", "-2d")]
    [InlineData("danger_sense", "surprised")]
    [InlineData("teleportation", "6 or less")]
    [InlineData("nullify", "1 Resolve")]
    [InlineData("omni_power", "1d")]
    [InlineData("super_speed", "2 Minions")]
    public void ASummaryStatesTheMechanicItsEntryTurnsOn(string id, string fact) =>
        Assert.Contains(fact, _f.Rules.GetPower(id)!.Description, StringComparison.Ordinal);

    private static string CorpusWords()
    {
        var builder = new StringBuilder(" ");
        var folder = Path.Combine(RulesFixture.RepoRoot, "data", "rulebook");

        foreach (var file in Directory.EnumerateFiles(folder, "ch*.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var section in document.RootElement.GetProperty("sections").EnumerateArray())
                builder.Append(Normalise(section.GetProperty("text").GetString() ?? string.Empty)).Append(' ');
        }

        return builder.ToString();
    }

    /// <summary>Letters and digits only, lower-cased and single-spaced, so re-punctuating a lifted clause hides nothing.</summary>
    private static string Normalise(string text)
    {
        var builder = new StringBuilder(" ");
        var lastWasSpace = true;

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        if (!lastWasSpace) builder.Append(' ');

        return builder.ToString();
    }

    private static IEnumerable<string> Runs(string normalised, int length)
    {
        var words = normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i + length <= words.Length; i++)
            yield return " " + string.Join(' ', words.Skip(i).Take(length)) + " ";
    }

    [Fact]
    public void DescriptionsAreDistinct()
    {
        // Two Powers sharing wording is a copy-paste slip, not a real duplicate.
        var duplicates = _f.Rules.Powers
            .GroupBy(p => p.Description, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(p => p.Id)));

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryPowerCitesThePageItWasCheckedAgainst()
    {
        var uncited = _f.Rules.Powers
            .Where(p => string.IsNullOrWhiteSpace(p.SourceRef))
            .Select(p => p.Id);

        Assert.Empty(uncited);
    }

    [Fact]
    public void PowersWithNotesExplainSomethingTheFieldsCannot()
    {
        // Notes carry the rules a structured field cannot hold — Boost's mirrored rate,
        // Summoning's Threat formula. Every special or variable-cost Power needs one.
        var needExplaining = _f.Rules.Powers
            .Where(p => p.CostType is "special" or "per_rank_variable" or "flat_variable")
            .Where(p => string.IsNullOrWhiteSpace(p.Notes))
            .Select(p => p.Id);

        Assert.Empty(needExplaining);
    }
}
