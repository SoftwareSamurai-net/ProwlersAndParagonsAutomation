using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.McpPlay;
using ProwlersAndParagonsAutomation.Play.Dice;
using ProwlersAndParagonsAutomation.Play.Encounter;
using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The play policy — <c>mcp-play/PLAY-POLICY.md</c>, which is what the <c>combat_guide</c> tool
/// answers with.
///
/// <para><b>It is the deliverable of this server, not documentation of it.</b> The transport was
/// the easy half; what decides whether a fight run through here is worth anything is whether the
/// model narrating it knows that it may not supply a number. So the document is held to the same
/// standard as code: the claims that keep the ordering honest are asserted rather than trusted, and
/// its account of what the engine does not model is checked against the engine <em>by driving
/// it</em> rather than against a list somebody typed twice.</para>
///
/// <para>This is the same job <see cref="McpQuestionPolicyTests"/> does for the character server's
/// question policy, and for the same reason: a document served to every conversation is wrong
/// everywhere at once, and nothing compiles it.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class McpPlayPolicyTests
{
    private readonly PlayFixture _f;

    public McpPlayPolicyTests(PlayFixture f) => _f = f;

    private static string Text => PlayPolicy.Text;

    private static string DocumentPath =>
        Path.Combine(RulesFixture.RepoRoot, "mcp-play", "PLAY-POLICY.md");

    /// <summary>
    /// The document with its line breaks flowed back into spaces, for asserting on a phrase. The
    /// file is hard-wrapped, so a sentence tested for as a substring passes or fails on where the
    /// wrap happened to land — a test that breaks when somebody reflows a paragraph and says
    /// nothing when they delete the sentence.
    /// </summary>
    private static string Flowed =>
        new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(Text, " ");

    /// <summary>
    /// The document as the tool serves it, compared with the file in the repository. Both halves
    /// are the point: the file could be perfect while the csproj had stopped embedding it — and
    /// then every conversation starts with the wrong document, or with none.
    /// </summary>
    [Fact]
    public void TheGuideToolAnswersWithTheDocumentOnDisk()
    {
        var tools = new PlayTools(
            RulesRepository.FromBasePath(RulesFixture.RepoRoot),
            new DerivedStatsCalculator(RulesRepository.FromBasePath(RulesFixture.RepoRoot)),
            _f.Play);

        // <b>Against the file, not against itself.</b> Comparing PlayPolicy.Text with CombatGuide(),
        // which returns PlayPolicy.Text, is a comparison that cannot fail — leaving the claim that
        // the tool serves *this document* asserted nowhere. Pointing the csproj at any other long
        // markdown file would pass that.
        var onDisk = File.ReadAllText(DocumentPath);

        // Line endings are the one difference allowed: git checks this file out with the platform's,
        // and an embedded resource keeps whatever was on disk at build time.
        Assert.Equal(Normalised(onDisk), Normalised(tools.CombatGuide()));
        Assert.True(Text.Length > 3000, "The embedded play policy is a stub.");
    }

    private static string Normalised(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// <b>The one rule, stated both ways round.</b> "The engine resolves and you narrate" on its own
    /// reads as a division of labour; what makes it a rule is the prohibition beside it, and a
    /// document carrying the first half without the second is an invitation to estimate.
    /// </summary>
    [Theory]
    [InlineData("the engine resolves and you narrate", "who decides")]
    [InlineData("You may not state a success count", "the prohibition itself")]
    [InlineData("A plausible number is worse than no number", "why estimating is worse than refusing")]
    [InlineData("not yet implemented", "what a refusal looks like on the ledger")]
    [InlineData("Do not narrate around it", "that a refusal may not be narrated past")]
    public void ThePolicySaysTheEngineResolvesAndTheModelNarrates(string sentence, string why) =>
        Assert.True(Flowed.Contains(sentence, StringComparison.OrdinalIgnoreCase),
            $"The play policy no longer says {why} (looked for '{sentence}').");

    /// <summary>
    /// <b>Only Heroes hold Resolve and the GM holds Adversity.</b> CLAUDE.md settles it and
    /// <see cref="Combatant"/> makes it unconstructible otherwise; the document has to say so, or a
    /// model will spend a Villain's Resolve and read the resulting error as the tool being broken.
    /// </summary>
    [Fact]
    public void ThePolicySaysOnlyHeroesHoldResolveAndTheGmHoldsAdversity()
    {
        Assert.Contains("Only Heroes hold Resolve", Flowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Adversity", Flowed, StringComparison.Ordinal);

        // And the way out for an NPC, without which the sentence above reads as "the GM cannot".
        Assert.Contains("anything_resolve_can", Flowed, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A measurement is quoted with its N, its seeds, its policy and its table settings, or it is
    /// not quoted.</b> Every one of the four is named, because a rate missing any of them is a rate
    /// about a game nobody can identify — and the tool prints all four in the same object precisely
    /// so that this instruction is followable.
    /// </summary>
    [Theory]
    [InlineData("runs", "the N")]
    [InlineData("seeds", "the seeds")]
    [InlineData("policy", "the policy")]
    [InlineData("table", "the table settings")]
    [InlineData("a guess about how people play", "that a policy is not a rule")]
    [InlineData("noise wearing a percentage sign", "why fewer than 30 runs is refused")]
    public void ThePolicySaysWhatAMeasurementIsQuotedWith(string needle, string why) =>
        Assert.True(Flowed.Contains(needle, StringComparison.OrdinalIgnoreCase),
            $"The play policy no longer names {why} (looked for '{needle}').");

    /// <summary>
    /// Every name in the first column of the table under <paramref name="heading"/>, backticked —
    /// the same parse <see cref="PlayEngineStepTests"/> makes of the engine guide, because these two
    /// documents make the same claim to two different audiences and both go stale the same way.
    /// </summary>
    private static HashSet<string> ListedUnder(string heading)
    {
        var at = Text.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(at >= 0, $"mcp-play/PLAY-POLICY.md no longer contains \"{heading}\".");

        var rest = Text[at..];
        var table = rest.IndexOf("|---|", StringComparison.Ordinal);

        Assert.True(table >= 0, $"no table follows \"{heading}\" in the play policy.");

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in rest[table..].Split('\n').Skip(1))
        {
            if (!line.StartsWith('|')) break;

            var first = line.Split('|')[1].Trim();
            if (first.StartsWith('`') && first.EndsWith('`')) names.Add(first.Trim('`'));
        }

        return names;
    }

    /// <summary>
    /// <b>The policy's not-applied lists and the engine's are the same lists.</b>
    ///
    /// <para>This document is served to every conversation this server has, so its account of what
    /// is unimplemented is the account a model acts on. A claim nothing checks is a claim that goes
    /// stale, and this one would go stale in the direction that matters: a rule implemented since
    /// would leave the document telling every conversation not to narrate an effect that now works,
    /// and a rule added to the engine's list would leave it silent about one that does not.</para>
    ///
    /// <para>Both directions, and both sets non-empty first — an empty-equals-empty comparison is
    /// the shape of a guard that proves nothing.</para>
    /// </summary>
    [Fact]
    public void ThePolicysNotAppliedListsAreTheEnginesNotAppliedLists()
    {
        var entries = ListedUnder("**`Encounter.EntriesNotYetApplied`**");
        var switches = ListedUnder("**`Encounter.SwitchesNotYetApplied`**");

        Assert.NotEmpty(entries);
        Assert.NotEmpty(switches);

        Assert.Equal(
            Encounter.EntriesNotYetApplied.Order(StringComparer.Ordinal),
            entries.Order(StringComparer.Ordinal));

        Assert.Equal(
            Encounter.SwitchesNotYetApplied.Order(StringComparer.Ordinal),
            switches.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>The spends the policy says refuse are the spends that actually refuse</b>, driven rather
    /// than listed.
    ///
    /// <para>The two tables above are pinned against static fields, which are themselves claims —
    /// <see cref="PlayEngineStepTests.EveryPurchaseEitherRefusesByNameOrResolves"/> is what keeps
    /// those honest, and this is the same instrument pointed at the third table, which is the one a
    /// model reads to decide whether it may narrate a knockback. So every member of both spend enums
    /// goes through <see cref="Encounter.Step"/> and is sorted by what happened, and the document
    /// has to name exactly the ones that refused — by the wire spelling a caller would pass, since
    /// that is what the table is a table of.</para>
    ///
    /// <para>Both halves non-empty is the control: a run in which nothing refused, or nothing
    /// resolved, would satisfy the comparison while measuring nothing.</para>
    /// </summary>
    [Fact]
    public void ThePolicysRefusingSpendsAreTheSpendsThatRefuse()
    {
        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 9,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var refused = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kind in Enum.GetValues<ResolveSpend>())
        {
            var encounter = new Encounter(_f.Play, new SeededDice(21), TableRules.Book with { FatalDamage = true });
            var state = encounter.Begin([hero, villain]);

            // Give the purchases that are decided after the roll something to work on.
            state = encounter.Step(state, new Attack("hero", "villain", "might")).State;

            var step = encounter.Step(state, new SpendResolve("hero", kind));

            (step.Added.Any(l => l.Text.Contains("not yet implemented", StringComparison.Ordinal))
                ? refused
                : resolved).Add(PlayTools.Wire(kind.ToString()));
        }

        foreach (var kind in Enum.GetValues<AdversitySpend>())
        {
            var encounter = new Encounter(_f.Play, new SeededDice(21));
            var state = encounter.Begin([hero, villain]);

            var step = encounter.Step(state, new SpendAdversity(
                "villain", kind,
                AsResolve: kind == AdversitySpend.AnythingResolveCan ? ResolveSpend.ExtraDice : null));

            (step.Added.Any(l => l.Text.Contains("not yet implemented", StringComparison.Ordinal))
                ? refused
                : resolved).Add(PlayTools.Wire(kind.ToString()));
        }

        Assert.NotEmpty(refused);
        Assert.NotEmpty(resolved);

        var named = ListedUnder("**Spends that refuse by name**");

        Assert.Equal(refused.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));

        // And the other direction, out of the prose beneath that table: a spend that resolves must
        // not be sitting in the sentence listing the ones that do not.
        foreach (var kind in resolved)
        {
            Assert.DoesNotContain(kind, named, StringComparer.Ordinal);

            Assert.True(Flowed.Contains(kind, StringComparison.Ordinal),
                $"'{kind}' resolves, and the policy does not name it at all — a caller reading this "
                + "document has no way to learn it is available.");
        }
    }

    /// <summary>
    /// Both columns of the table under <paramref name="heading"/>, backticked first column to
    /// trimmed second — the same parse as <see cref="ListedUnder"/>, kept beside it, for the one
    /// table here whose claim is not "these refuse" but "this one does and that one does not".
    /// </summary>
    private static Dictionary<string, string> PairsUnder(string heading)
    {
        var at = Text.IndexOf(heading, StringComparison.Ordinal);

        Assert.True(at >= 0, $"mcp-play/PLAY-POLICY.md no longer contains \"{heading}\".");

        var rest = Text[at..];
        var table = rest.IndexOf("|---|", StringComparison.Ordinal);

        Assert.True(table >= 0, $"no table follows \"{heading}\" in the play policy.");

        var rows = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in rest[table..].Split('\n').Skip(1))
        {
            if (!line.StartsWith('|')) break;

            var cells = line.Split('|');
            var first = cells[1].Trim();

            if (first.StartsWith('`') && first.EndsWith('`')) rows[first.Trim('`')] = cells[2].Trim();
        }

        return rows;
    }

    /// <summary>
    /// <b>Every purchase the document says the GM's pool may name, driven, and the answer has to be
    /// the answer the document claims.</b>
    ///
    /// <para>This is the table that was wrong. The document advertised <c>anything_resolve_can</c>
    /// buying all six of a Hero's purchases; the engine bought two and refused the other four — and
    /// refused them in words that did not carry <c>not yet implemented</c>, so the guard beside this
    /// one sorted them into neither pile and nothing disagreed with the claim. A model reading that
    /// document had no way to tell a purchase that had happened from one that had not, which is the
    /// single failure this whole server is built to make impossible.</para>
    ///
    /// <para><b>Every value of the enum is driven, not only the ones named</b>, so a purchase the
    /// GM could name and the table has forgotten fails here rather than being found by a caller. And
    /// both answers have to occur: a table saying "not yet implemented" of everything, or of
    /// nothing, would satisfy a comparison that only ever checked one of them.</para>
    /// </summary>
    [Fact]
    public void EveryPurchaseTheGmsPoolMayNameAnswersTheWayThePolicySaysItDoes()
    {
        var claimed = PairsUnder("**What `anything_resolve_can` may name**");

        Assert.Equal(
            Enum.GetValues<ResolveSpend>().Select(k => PlayTools.Wire(k.ToString())).Order(StringComparer.Ordinal),
            claimed.Keys.Order(StringComparer.Ordinal));

        var hero = Combatant.Hero("hero", "the Hero", edge: 9, health: 10, resolve: 9,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var villain = Combatant.Villain("villain", "the Villain", edge: 7, health: 10,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["might"] = 8, ["toughness"] = 5 },
            ["toughness"]);

        var answered = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var purchase in Enum.GetValues<ResolveSpend>())
        {
            var encounter = new Encounter(_f.Play, new SeededDice(21), TableRules.Book with { FatalDamage = true });
            var state = encounter.Begin([hero, villain]);

            // An attack first, so the two purchases decided after the roll have a roll to work on.
            state = encounter.Step(state, new Attack("hero", "villain", "might")).State;

            var step = encounter.Step(state, new SpendAdversity("villain", AdversitySpend.AnythingResolveCan,
                Points: 1, AsResolve: purchase));

            answered[PlayTools.Wire(purchase.ToString())] =
                step.Added.Any(l => l.Text.Contains("not yet implemented", StringComparison.Ordinal))
                    ? "not yet implemented"
                    : "bought";
        }

        // The control: both answers have to have occurred, or the comparison below is vacuous.
        Assert.Contains("bought", answered.Values, StringComparer.Ordinal);
        Assert.Contains("not yet implemented", answered.Values, StringComparer.Ordinal);

        foreach (var (purchase, says) in claimed.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            Assert.True(string.Equals(says, answered[purchase], StringComparison.Ordinal),
                $"The play policy says the GM's pool answers '{purchase}' with \"{says}\", and the "
                + $"engine answers \"{answered[purchase]}\". A document served to every conversation "
                + "this server has is wrong everywhere at once.");
        }
    }

    /// <summary>
    /// <b>The startup check reads the guide as well as the rules.</b> The guide is an embedded
    /// resource, so the way it goes missing is a csproj edit — and the claim <see cref="PlayTools"/>
    /// makes for itself is that such an edit becomes a refusal at startup rather than a conversation
    /// that opens with an empty document.
    ///
    /// <para>Driven, not grepped, for the reason the character server's twin of this records: a
    /// search of the method's body for a token is satisfied by a <c>nameof</c>, a comment or a
    /// <c>using</c>, none of which reads anything. The guide is handed in, and this hands one that
    /// throws.</para>
    /// </summary>
    [Fact]
    public void TheStartupCheckReadsTheGuideAndNotOnlyTheRules()
    {
        var rules = RulesRepository.FromBasePath(RulesFixture.RepoRoot);

        var missing = new PlayTools(
            rules, new DerivedStatsCalculator(rules), _f.Play,
            guide: () => throw new InvalidOperationException(
                "The play policy is not embedded in this assembly."));

        var refusal = Assert.ThrowsAny<Exception>(missing.ReadEverything);

        Assert.Contains("not embedded", refusal.Message, StringComparison.Ordinal);

        // And a complete one does not throw, which is what keeps the assertion above honest.
        new PlayTools(rules, new DerivedStatsCalculator(rules), _f.Play).ReadEverything();
    }

    /// <summary>
    /// <b>The startup check reads every play rules file, not one.</b> Both repositories are lazy, so
    /// a directory holding a single file would otherwise start cleanly and then throw out of most of
    /// the tools — which is the character server's recorded history, with twice as many files here
    /// to be missing.
    /// </summary>
    [Fact]
    public void APartialPlayRulesDirectoryIsRefusedAtStartupRatherThanAtTheFirstQuestion()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "pp-mcp-play-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            File.Copy(
                Path.Combine(PlayFixture.DataPath, PlayRulesRepository.CombatFile),
                Path.Combine(scratch, PlayRulesRepository.CombatFile));

            var rules = RulesRepository.FromBasePath(RulesFixture.RepoRoot);

            var tools = new PlayTools(
                rules, new DerivedStatsCalculator(rules),
                new PlayRulesRepository(new FileSystemRulesSource(scratch)));

            Assert.ThrowsAny<Exception>(tools.ReadEverything);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (IOException) { /* a temp directory that outlives the run is not a failure */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
