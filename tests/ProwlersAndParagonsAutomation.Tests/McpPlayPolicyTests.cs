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
    ///
    /// <para><b>The entries half branches on the engine rather than requiring a sentence.</b> It
    /// used to assert the set was empty and then require this document to carry the words "is
    /// empty" — which is a guard that reads the document and never reads the engine into it: adding
    /// an id to <see cref="Encounter.EntriesNotYetApplied"/> left both document assertions green,
    /// because a document nobody changed still said what it had always said. The branch below makes
    /// the document's obligation depend on the engine's list, so an entry the engine declines to
    /// apply and this document does not name goes red on the parse that cannot find a table.</para>
    /// </summary>
    [Fact]
    public void ThePolicysNotAppliedListsAreTheEnginesNotAppliedLists()
    {
        // <b>Both lists are empty now, so neither half can supply the parse's control any more</b>
        // — the Gear Limit's two switches were the last rows in this document's table and the
        // engine applies them. The instrument itself is driven in
        // <c>PlayEngineStepTests.TheGuideTableParseCanFindRowsAndTellThemApart</c>, over a document
        // written for it; here each half's obligation depends on the engine's own list.
        if (Encounter.SwitchesNotYetApplied.Count == 0)
        {
            Assert.Contains("`Encounter.SwitchesNotYetApplied` is empty", Flowed, StringComparison.Ordinal);
            Assert.DoesNotContain("**`Encounter.SwitchesNotYetApplied`**", Text, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("`Encounter.SwitchesNotYetApplied` is empty", Flowed, StringComparison.Ordinal);

            Assert.Equal(
                Encounter.SwitchesNotYetApplied.Order(StringComparer.Ordinal),
                ListedUnder("**`Encounter.SwitchesNotYetApplied`**").Order(StringComparer.Ordinal));
        }

        if (Encounter.EntriesNotYetApplied.Count == 0)
        {
            // Nothing is unapplied, so the document says so in as many words and carries no table —
            // a table reappearing here without the engine agreeing is the direction that would
            // otherwise tell every conversation not to narrate an effect that now works.
            Assert.Contains("`Encounter.EntriesNotYetApplied` is empty", Flowed, StringComparison.Ordinal);
            Assert.DoesNotContain("**`Encounter.EntriesNotYetApplied`**", Text, StringComparison.Ordinal);

            return;
        }

        // Something is unapplied, so the document names it — in a table, and without the sentence
        // that says there is nothing to name.
        Assert.DoesNotContain("`Encounter.EntriesNotYetApplied` is empty", Flowed, StringComparison.Ordinal);

        Assert.Equal(
            Encounter.EntriesNotYetApplied.Order(StringComparer.Ordinal),
            ListedUnder("**`Encounter.EntriesNotYetApplied`**").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>Every scenery row a caller may name is named in the document, and the figure beside it is
    /// the shipped one.</b>
    ///
    /// <para>The three tables are what <c>cover_scenery</c>, a knockback's <c>solid_object</c> and
    /// an improvised weapon's <c>item</c> are matched against, and a name none of them prints is
    /// refused. So a caller has to be able to find out what the names are — and a list typed into a
    /// document is a second transcription that will disagree with the first the day somebody
    /// corrects the data. It is derived here instead: every printed row name, with its own rank
    /// beside it, has to appear in the served text.</para>
    ///
    /// <para><b>The rank is part of the claim on purpose.</b> A document that carried every name
    /// under one wrong heading would satisfy a check on the names alone, and a caller reading it
    /// would pick a wall by a Structure the engine does not use.</para>
    /// </summary>
    [Fact]
    public void ThePolicyNamesEveryRowChapterSevenRates()
    {
        var missing = new List<string>();
        var counted = 0;

        foreach (var (name, rank) in SceneryRows())
        {
            counted++;

            // The name, and the rank printed for it, in the same run of text — the tables are
            // written out rank-first, so the group's figure is what precedes its names.
            if (!Flowed.Contains(name, StringComparison.Ordinal)) missing.Add($"{name} (not named)");
            else if (!Flowed.Contains($"{rank}: ", StringComparison.Ordinal)) missing.Add($"{name} ({rank})");
        }

        // The positive control: a document check that had stopped finding rows would report nothing
        // missing and prove nothing.
        Assert.True(counted >= 55,
            $"only {counted} scenery rows were read out of environment.json, and the three object "
            + "tables print fifty-eight between them — this check is walking a table that has lost "
            + "most of itself and would pass against a document naming nothing.");

        Assert.True(missing.Count == 0,
            "mcp-play/PLAY-POLICY.md does not name these rows, or names them under the wrong rank: "
            + string.Join(", ", missing)
            + ". A caller can only name what the document lists, and this server refuses a name no "
            + "table prints.");
    }

    /// <summary>Every printed row of Chapter 7's three object tables, with the rank beside it.</summary>
    private IEnumerable<(string Name, int Rank)> SceneryRows()
    {
        var smashing = _f.Play.GetEnvironment("smashing_table").SmashingTable!;

        foreach (var row in smashing.Rows)
        {
            foreach (var material in row.Materials) yield return (material, row.Structure);
        }

        foreach (var row in _f.Play.GetEnvironment("scenery_table").SceneryTable!)
        {
            foreach (var thing in row.Scenery) yield return (thing, row.Structure);
        }

        foreach (var row in _f.Play.GetEnvironment("massive_objects_table").MassiveObjectsTable!)
        {
            foreach (var thing in row.Objects) yield return (thing, row.WeightRank);
        }
    }

    /// <summary>
    /// <b>The policy says no spend refuses by name, and no spend does</b> — driven rather than
    /// listed.
    ///
    /// <para>The two tables above are pinned against static fields, which are themselves claims —
    /// <see cref="PlayEngineStepTests.EveryPurchaseEitherRefusesByNameOrResolves"/> is what keeps
    /// those honest, and this is the same instrument pointed at what the document tells a model it
    /// may narrate. Every member of both spend enums goes through <see cref="Encounter.Step"/> and
    /// is sorted by what happened, and the document has to agree — by the wire spelling a caller
    /// would pass, since that is what it is naming.</para>
    ///
    /// <para><b>The table this used to compare against is gone, because the last spend on it was
    /// applied</b>, and the claim it made is now a sentence: every `kind` resolves, and what is
    /// still answered with a `not yet implemented` line is what `anything_resolve_can` may
    /// <em>name</em>. So the two-valued control that keeps the classifier honest lives in
    /// <see cref="EveryPurchaseTheGmsPoolMayNameAnswersTheWayThePolicySaysItDoes"/>, which drives
    /// the same instrument over that table and still sees both answers. What is asserted here is
    /// the stronger pair: nothing refused, everything resolved, and every one of them is named in
    /// the document a caller reads.</para>
    /// </summary>
    [Fact]
    public void ThePolicySaysNoSpendRefusesByNameAndNoneDoes()
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

            // Each spend is handed what its own rule asks for, so a purchase lands in the resolved
            // pile because it resolved and not because it was refused for want of an argument:
            // p.85's first purchase has to name a Resolve purchase, and its three own purchases have
            // to say, in the GM's words, what the point bought.
            var step = encounter.Step(state, new SpendAdversity(
                "villain", kind,
                AsResolve: kind == AdversitySpend.AnythingResolveCan ? ResolveSpend.ExtraDice : null,
                Narration: "a hot temper"));

            (step.Added.Any(l => l.Text.Contains("not yet implemented", StringComparison.Ordinal))
                ? refused
                : resolved).Add(PlayTools.Wire(kind.ToString()));
        }

        // The control: every purchase in both enums was driven, and every one of them resolved.
        Assert.Equal(
            Enum.GetValues<ResolveSpend>().Select(k => PlayTools.Wire(k.ToString()))
                .Concat(Enum.GetValues<AdversitySpend>().Select(k => PlayTools.Wire(k.ToString())))
                .Order(StringComparer.Ordinal),
            resolved.Order(StringComparer.Ordinal));

        Assert.True(
            refused.Count == 0,
            "these spends refuse as not yet implemented: "
            + string.Join(", ", refused.Order(StringComparer.Ordinal))
            + ". The play policy says none does, and a document served to every conversation this "
            + "server has is wrong everywhere at once — say so in it before this can pass.");

        // And the document has to say it, both ways: the claim itself, and every spend named, so a
        // caller reading it can learn that each is available.
        Assert.Contains("No spend refuses by name any more", Flowed, StringComparison.Ordinal);

        foreach (var kind in resolved)
        {
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
    /// GM could name and the table has forgotten fails here rather than being found by a caller.
    /// </para>
    ///
    /// <para><b>Every row says <c>bought</c> now, so the two-valued control has moved twice, and
    /// this paragraph is where it says where to.</b> It used to be this table itself — four rows one
    /// way and six the other — and a comparison whose right-hand side has one value cannot tell a
    /// working classifier from one that has stopped recognising the phrase at all. It then moved to
    /// <see cref="Encounter.SwitchesNotYetApplied"/>, which is empty now that p.80's Gear Limit is
    /// applied. The case left is <see cref="NotYetImplemented"/>: a clause inside a rule that is
    /// otherwise applied, written by <c>Step</c> resolving a Minion group's attack, and the
    /// substring test is watched answering both ways at the end of this test. Should that pair ever
    /// go one-valued too, this guard is asserting nothing and wants a new control before it is
    /// worth reading.</para>
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

        // The control: every purchase was driven, and the classifier that sorted them can still see
        // its own phrase. That second half is no longer this table's to supply — every row answers
        // "bought" — nor the Gear Limit switch's, which is applied now. It comes off
        // NotYetImplemented instead: p.77's unapplied clause, written by Step resolving a Minion
        // group's attack, against the same attack made by one character, which does not say it.
        Assert.Equal(
            Enum.GetValues<ResolveSpend>().Select(k => PlayTools.Wire(k.ToString())).Order(StringComparer.Ordinal),
            answered.Keys.Order(StringComparer.Ordinal));

        Assert.True(
            NotYetImplemented.ARunThatSaysIt(_f.Play)
                .Any(l => l.Text.Contains(NotYetImplemented.Phrase, StringComparison.Ordinal)),
            "p.77's unapplied clause is what is left of the 'not yet implemented' classifier this "
            + "test sorts on, and the run that should say it did not. With every row of the table "
            + "reading 'bought', nothing else here can tell a working classifier from one that has "
            + "stopped recognising the phrase.");

        Assert.DoesNotContain(
            NotYetImplemented.ARunThatDoesNot(_f.Play),
            l => l.Text.Contains(NotYetImplemented.Phrase, StringComparison.Ordinal));

        // And that run really did resolve an attack, or the two above are two silent runs agreeing.
        Assert.Contains(
            NotYetImplemented.ARunThatDoesNot(_f.Play),
            l => l.Text.Contains("attacks", StringComparison.Ordinal));

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

    /// <summary>
    /// <b>Every problem code the policy prints is a code this server can actually answer with, and
    /// the two the table rules add are among them.</b>
    ///
    /// <para>A refusal is a dead end unless the document that a conversation reads names it: a
    /// model handed <c>TABLE_DISAGREES</c> with nothing in the guide about it will either narrate
    /// around the refusal or invent a repair, and both are worse than the fight not starting. The
    /// other direction is the one that goes stale: a code renamed in <c>PlayTools</c> leaves this
    /// document telling every conversation to expect one that can no longer arrive.</para>
    ///
    /// <para><b>Against the codes the server can emit</b>, which
    /// <c>McpPlayServerTests.TheseAreEveryCodeTheServerCanEmit</c> reads out of the source and
    /// drives one by one — so this is not a second list to keep in step. The control is that the
    /// parse finds code spans at all: a regular expression that had stopped matching would agree
    /// with any document whatever.</para>
    /// </summary>
    [Fact]
    public void EveryProblemCodeThePolicyNamesIsOneTheServerCanAnswerWith()
    {
        var named = new Regex(@"`([A-Z][A-Z_]{3,})`", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(Text)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // The control: the parse found something. Two of these are the whole point of the section
        // that was added with them, so their absence is a finding rather than a quiet pass.
        Assert.Contains("TABLE_DISAGREES", named, StringComparer.Ordinal);
        Assert.Contains("CALL_TABLE_DISAGREES", named, StringComparer.Ordinal);

        var real = McpPlayServerTests.ProblemCodes;

        Assert.True(real.Count > 20, $"only {real.Count} problem codes were found to check against.");

        var invented = named.Except(real).ToList();

        Assert.True(invented.Count == 0,
            "mcp-play/PLAY-POLICY.md names these refusals and this server cannot answer with any "
            + "of them, so a conversation is told to expect a code that never arrives: "
            + string.Join(", ", invented) + ".");
    }
}
