using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The recorded conversations the browser replays.
///
/// <para><b>They are data, and data rots silently.</b> A Power id renamed in a rules file
/// leaves a transcript replaying a character the engine cannot answer for, and the failure
/// lands on a visitor rather than on a build — which is the same shape of problem
/// <see cref="SkillDocumentationTests"/> exists for, so it gets the same treatment: every
/// character in every transcript goes through the strict reader and the validator, exactly
/// as if somebody had submitted it.</para>
///
/// <para><b>The second job here is the honesty rule</b>, which is the thing that would ruin
/// the replay if it slipped. Every Hero Point figure and every derived stat the visitor sees
/// is computed in their browser from the character stored in the transcript. If a recorded
/// line quoted one instead, the replay would be showing a number that had stopped being true
/// and looking exactly as convincing —
/// <see cref="NoRecordedLineQuotesAFigureTheEngineIsSupposedToAnswer"/> is what stops it.</para>
///
/// <para><b>What is not checked here, and cannot be: whether a recorded sentence about the
/// rules is true.</b> The characters are held to the engine and the figures are banned from
/// the prose, but a line claiming "the Trait Cap is a limit on Abilities alone" would pass
/// every test in this file. That is not hypothetical — the cheap conversation shipped for two
/// commits asserting the rulebook has no Power for detecting a lie, which it has, and a person
/// caught it rather than a test. So do not read a green suite as saying a recording is
/// accurate; it says the characters are legal and no figure was quoted. **Read the prose
/// against the rulebook before merging a change to one of these files.**</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class TranscriptTests
{
    private readonly RulesFixture _f;

    public TranscriptTests(RulesFixture f) => _f = f;

    private static string Directory =>
        Path.Combine(RulesFixture.RepoRoot, "data", "transcripts");

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options, TimeSpan.FromSeconds(5));

    /// <summary>
    /// The transcripts, read the way the browser reads them: from file contents keyed by name,
    /// through <see cref="TranscriptLibrary.ReadAll"/>. Reading them any other way here would
    /// be testing a path nothing uses.
    /// </summary>
    private static IReadOnlyList<Transcript> All()
    {
        var files = TranscriptLibrary.FileNames.ToDictionary(
            name => name,
            name => File.ReadAllText(Path.Combine(Directory, name)),
            StringComparer.Ordinal);

        return TranscriptLibrary.ReadAll(files);
    }

    /// <summary>
    /// <b>Everything a transcript puts in front of a visitor, wherever it keeps it.</b>
    ///
    /// <para>The honesty scan read <c>Title</c>, <c>Blurb</c> and the recorded lines, and that
    /// is not what the replay shows: the character travels with the conversation, and
    /// <c>SheetView</c> prints its Name, Motivation, Quote, Description, Connections and the
    /// narrative detail on every Flaw and Perk. A figure written into any of those sat on the
    /// printed sheet unguarded.</para>
    ///
    /// <para>The character is walked by reflection rather than by naming those fields.
    /// <b>A list of field names is exactly the thing that went stale here once already</b> —
    /// it would be right until somebody adds a seventh free-text field, and wrong silently
    /// from then on. Ids come back too and are harmless: an id is one word, and every rule
    /// below needs a number beside a word about money.</para>
    /// </summary>
    private static IEnumerable<(string Where, string Text)> EveryProseIn(Transcript t)
    {
        yield return ("title", t.Title);
        yield return ("blurb", t.Blurb);

        for (var i = 0; i < t.Turns.Count; i++)
        {
            yield return ($"turn {i + 1}", t.Turns[i].Text);

            if (t.Turns[i].Character is not { } character)
                continue;

            // Reference equality, not the default. SelectedProCon and friends compare by value,
            // and a character legitimately carries two equal ones — Also X three times. A
            // value-equality visited set would walk the first and skip the rest.
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

            foreach (var (path, text) in StringsIn(character, "character", seen))
                yield return ($"turn {i + 1} {path}", text);
        }
    }

    /// <summary>
    /// Every string reachable from an object, with the path it was found at. Depth is bounded
    /// by the object graph a character is — there are no cycles in it — and a visited set
    /// guards the assumption rather than trusting it.
    /// </summary>
    private static IEnumerable<(string Path, string Text)> StringsIn(
        object? node, string path, HashSet<object> seen)
    {
        switch (node)
        {
            case null:
                yield break;

            case string text:
                yield return (path, text);
                yield break;

            case System.Collections.IDictionary map:
                foreach (var key in map.Keys)
                {
                    if (key is string name) yield return ($"{path} key", name);
                    foreach (var found in StringsIn(map[key], $"{path}[{key}]", seen))
                        yield return found;
                }

                yield break;

            case System.Collections.IEnumerable list:
            {
                var i = 0;
                foreach (var item in list)
                {
                    foreach (var found in StringsIn(item, $"{path}[{i}]", seen))
                        yield return found;
                    i++;
                }

                yield break;
            }
        }

        // A value type with no strings in it — an int rank, a bool — and the recursion stops.
        if (node.GetType().IsPrimitive || node is Enum || !seen.Add(node))
            yield break;

        foreach (var property in node.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length > 0) continue;

            foreach (var found in StringsIn(property.GetValue(node), $"{path}.{property.Name}", seen))
                yield return found;
        }
    }

    private static IEnumerable<(Transcript Transcript, int Turn, CharacterSheet Character)> EveryCharacter() =>
        All().SelectMany(t => t.Turns
            .Select((turn, i) => (Transcript: t, Turn: i + 1, turn.Character))
            .Where(x => x.Character is not null)
            .Select(x => (x.Transcript, x.Turn, x.Character!)));

    // ── The list and the directory agree ────────────────────────────────────────

    /// <summary>
    /// A browser cannot glob a directory it has no filesystem for, so the file names are a
    /// list in code — and a list in code beside a directory of files is two places to change.
    /// A transcript added and not listed exists everywhere except in the app, which is a
    /// silent nothing rather than a failure.
    /// </summary>
    [Fact]
    public void TheListOfTranscriptFilesMatchesTheDirectory()
    {
        var onDisk = System.IO.Directory.GetFiles(Directory, "*.json")
            .Select(path => new FileInfo(path).Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(onDisk);
        Assert.Equal(onDisk, TranscriptLibrary.FileNames.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The id is what a shared link carries, so it has to be the file name and it has to be
    /// unique. Two transcripts on one id is a page that plays whichever came first.
    /// </summary>
    [Fact]
    public void EveryTranscriptIsIdentifiedByItsOwnFileName()
    {
        var all = All();

        Assert.Equal(
            TranscriptLibrary.FileNames.Select(n => n.Replace(".json", "", StringComparison.Ordinal)).ToList(),
            all.Select(t => t.Id).ToList());

        Assert.Equal(all.Count, all.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// What the list of recordings needs before a visitor picks one: a name, and one sentence
    /// saying what this conversation shows that the others do not. Both are prose nothing
    /// else would miss.
    /// </summary>
    [Fact]
    public void EveryTranscriptSaysWhatItIsAndWhatItShows() =>
        Assert.All(All(), t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title), $"{t.Id} has no title.");
            Assert.False(string.IsNullOrWhiteSpace(t.Blurb), $"{t.Id} has no blurb.");
        });

    // ── The shape of a conversation ─────────────────────────────────────────────

    /// <summary>
    /// A conversation opens with the person describing something and ends with a character
    /// the visitor can be handed. Without the second, the replay reaches its last turn and
    /// has nothing to put in front of them, which is the whole point of it.
    /// </summary>
    [Fact]
    public void EveryConversationStartsWithADescriptionAndEndsWithACharacter() =>
        Assert.All(All(), t =>
        {
            Assert.NotEmpty(t.Turns);
            Assert.Equal(TranscriptSpeaker.Person, t.Turns[0].Speaker);
            Assert.True(t.FinalCharacter is not null, $"{t.Id} never arrives at a character.");
            Assert.All(t.Turns, turn => Assert.False(string.IsNullOrWhiteSpace(turn.Text)));
        });

    /// <summary>
    /// The question policy is what makes this a demonstration of the design rather than of a
    /// chat, and its headline rule is a number: <b>at most three questions</b>. A recording
    /// that interrogates somebody is a questionnaire wrapped around a wizard that already
    /// exists, and it would be teaching the opposite of what
    /// <c>mcp/QUESTION-POLICY.md</c> says.
    ///
    /// <para>Counted over the whole conversation rather than per turn, because the cost to
    /// the person is the number of things they have to answer and not how they were grouped.
    /// </para>
    ///
    /// <para><b>A question mark is not what makes something a question.</b> This counted
    /// <c>'?'</c> characters and nothing else, so seven imperative demands — "Tell me the
    /// tier. Tell me whether she is one Power or several. …" — were a conversation that asked
    /// nothing. That is a questionnaire, in the one file whose job is to demonstrate the
    /// opposite. A demand phrased as an instruction costs the person exactly what a question
    /// costs them, so it is counted as one.</para>
    ///
    /// <para><b>The detector is a heuristic and cannot be anything else, which is worth saying
    /// plainly and was once said too strongly here.</b> It knows the phrasings a request is
    /// normally written in; "Settle the tier before I go on. Work out whether she is one Power
    /// or several. Have a look at what she is ordinary at." is three demands and scores zero,
    /// because those verbs are not on the list and no list closes that. An earlier version of
    /// this note called
    /// <see cref="ARecordingNeverMakesThePersonAnswerMoreThanThreeTimes"/> "the half no
    /// wording can defeat", and that is not true either: it counts <em>replies</em>, so seven
    /// demands bundled into one turn cost one reply and pass. Bundling defeats it, not wording.
    /// </para>
    ///
    /// <para>The two together catch a questionnaire written the ordinary way and neither is a
    /// proof. There are four recordings, they are hand-written, and they change rarely — so
    /// the real guarantee is the one <c>CLAUDE.md</c> already states for the prose: <b>read a
    /// changed transcript</b>. These tests are here to catch drift, not to classify English.
    /// </para>
    /// </summary>
    [Fact]
    public void NoRecordedConversationAsksMoreThanThreeQuestions() =>
        Assert.All(All(), t =>
        {
            var asked = t.Turns
                .Where(turn => turn.Speaker == TranscriptSpeaker.Assistant)
                .Sum(turn =>
                    turn.Text.Count(c => c == '?')
                    + Sentences(turn.Text).Count(s =>
                        !s.Contains('?', StringComparison.Ordinal) && IsARequest(s)));

            Assert.True(asked <= 3,
                $"{t.Id} asks {asked} things of the person. The policy allows three.");
        });

    /// <summary>
    /// The same rule measured a way no phrasing can get round: <b>how many times the person
    /// had to reply.</b>
    ///
    /// <para>Counting the shape of the assistant's sentences will always be a matter of
    /// recognising how a request is written. Counting the person's turns is not — whatever
    /// they were asked and however it was worded, a conversation where they speak five times
    /// made them supply five things, and that is the cost the question policy is about.</para>
    ///
    /// <para>Their opening description is not an answer to anything, so it does not count.</para>
    /// </summary>
    [Fact]
    public void ARecordingNeverMakesThePersonAnswerMoreThanThreeTimes() =>
        Assert.All(All(), t =>
        {
            var answers = t.Turns.Skip(1).Count(turn => turn.Speaker == TranscriptSpeaker.Person);

            Assert.True(answers <= 3,
                $"{t.Id} makes the person answer {answers} times. The policy allows three.");
        });

    /// <summary>Roughly, sentences — enough to ask what each one opens with.</summary>
    private static IEnumerable<string> Sentences(string text) =>
        Rx("[^.!?]+[.!?]*").Matches(text)
            .Select(m => m.Value.Trim())
            .Where(s => s.Length > 0);

    /// <summary>
    /// Whether a sentence asks the person for something without a question mark on it. Either
    /// it opens with a verb that demands an answer, or it carries one of the phrases a request
    /// is normally wrapped in.
    ///
    /// <para><c>let</c> is deliberately not an opener — "Let me build her" announces what the
    /// assistant is about to do, which is the thing this design wants more of. "Let me know"
    /// is a request and is caught as a phrase.</para>
    ///
    /// <para><b>A politeness wrapper is stripped before the opener is read.</b> Matching the
    /// first word alone missed every request phrased the way people actually phrase them:
    /// "Could you settle the tier for me. Could you say whether she is one Power or several.
    /// Please supply her Source." — five demands counted as none, because each opens with
    /// "could" or "please". A modal plus the second person <em>is</em> the request; the verb
    /// after it is the same verb.</para>
    /// </summary>
    private static bool IsARequest(string sentence)
    {
        string[] openers =
        [
            "tell", "say", "give", "name", "describe", "choose", "pick", "decide", "confirm",
            "specify", "list", "explain", "answer", "state", "provide", "share", "send",
            "supply", "settle", "pin", "set", "select", "work", "think", "consider", "have",
            "go", "bring", "look", "check", "point", "sort"
        ];

        string[] phrases =
        [
            "tell me", "let me know", "i need to know", "i need you to", "i'll need you to",
            "i need from you", "your answer", "answer me"
        ];

        var lower = sentence.ToLowerInvariant();

        if (phrases.Any(p => lower.Contains(p, StringComparison.Ordinal))) return true;

        // "Could you …", "Would you mind …", "Please …" — a request whatever follows, so this
        // returns true on the wrapper rather than stripping it and hoping the verb is listed.
        if (Rx(@"^\W*(please\b|(could|would|can|will|might)\s+you\b)").IsMatch(lower)) return true;

        // And the wrapper again, this time stripped, so "First, could you please name her
        // Source" is read as "name her Source".
        var stripped = Rx(@"^\W*((please|kindly|first|then|now|also)\b\W*)*").Replace(lower, "");
        var first = Rx("^[^a-z]*([a-z']+)").Match(stripped);

        return first.Success && openers.Contains(first.Groups[1].Value, StringComparer.Ordinal);
    }

    // ── The rot guard ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every character in every transcript is one the engine can price.</b> Reading it is
    /// already strict — <see cref="TranscriptLibrary"/> refuses a field name a character no
    /// longer has — and this is the other half: an id that has gone, a rank that is now over
    /// a cap, a Trait under a package floor.
    ///
    /// <para><b>The budget allowance is named, not blanket.</b> It was blanket, and an
    /// adversarial pass showed what that costs: pushed to 146 Hero Points against a 75 budget,
    /// the cheap character still passed every test here, while the page rendered "Over by 71"
    /// beside a recorded line saying she comes in under it. Two conversations are <em>about</em>
    /// being over — the draft that had to give something up, and the Villain, who has no budget
    /// at all under Ch.9 — and the prose of both depends on that. So the exact turns that may
    /// be over are listed, and any other transcript going over is a failure like any other.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheTwoConversationsThatAreAboutNotFittingGoOverTheBudget()
    {
        (string Id, int Turn)[] expected = [("sheet-lightning", 4), ("the-conductor", 4)];

        var over = EveryCharacter()
            .Where(x => _f.Validator.Validate(x.Character).Issues.Any(i => i.Code == "HP_BUDGET_EXCEEDED"))
            .Select(x => (x.Transcript.Id, x.Turn))
            .ToList();

        Assert.Equal(expected, over);
    }

    /// <summary>
    /// And nothing else the validator can say. Every other code means the transcript has
    /// drifted away from the rules, whatever it is.
    /// </summary>
    [Fact]
    public void EveryCharacterInEveryTranscriptIsStillLegalByTheseRules() =>
        Assert.All(EveryCharacter(), x =>
        {
            var issues = _f.Validator.Validate(x.Character).Issues
                .Where(i => i.Code != "HP_BUDGET_EXCEEDED")
                .Select(i => $"{i.Code}: {i.Message}")
                .ToList();

            Assert.True(issues.Count == 0,
                $"{x.Transcript.Id} turn {x.Turn} no longer builds a legal character: "
                + string.Join(" / ", issues));
        });

    /// <summary>
    /// And priceable, which the validator reports rather than throws about — so a character it
    /// cannot cost comes back as a finding there and as an exception here. The replay costs
    /// every draft in front of the visitor; one that throws is a blank panel where the
    /// judgement was supposed to happen.
    /// </summary>
    [Fact]
    public void EveryCharacterInEveryTranscriptCanBeCostedAndItsStatsDerived() =>
        Assert.All(EveryCharacter(), x =>
        {
            _ = _f.Costs.TotalCost(x.Character);
            _ = _f.Derived.CalculateEdge(x.Character);
            _ = _f.Derived.CalculateHealth(x.Character);
            _ = _f.Derived.CalculateResolve(x.Character);
        });

    /// <summary>
    /// The one conversation that turns on a first draft not fitting has to have a first draft
    /// that does not fit. It is the only transcript whose <em>point</em> is an engine answer,
    /// so it is the only one where that answer is worth asserting — and if the rules ever
    /// made it affordable, the recording would be talking about a trade the panel beside it
    /// no longer shows.
    /// </summary>
    [Fact]
    public void TheConversationAboutNotFittingStillHasADraftThatDoesNotFit()
    {
        var transcript = Assert.Single(All(), t => t.Id == "sheet-lightning");

        var drafts = transcript.Turns.Where(t => t.Character is not null).Select(t => t.Character!).ToList();

        Assert.True(drafts.Count >= 2, "The conversation no longer records a draft and a settlement.");

        Assert.Contains(
            _f.Validator.Validate(drafts[0]).Issues,
            i => i.Code == "HP_BUDGET_EXCEEDED");

        Assert.DoesNotContain(
            _f.Validator.Validate(drafts[^1]).Issues,
            i => i.Code == "HP_BUDGET_EXCEEDED");
    }

    // ── The honesty rule ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>No recorded line may quote a figure the engine is there to answer.</b> A Hero Point
    /// total, a spend, a remaining budget, an Edge, a Health or a Resolve: all of those are
    /// computed in the visitor's browser from the character beside the line, and a number
    /// written into the prose would sit there looking identical while being wrong.
    ///
    /// <para>Ranks are deliberately <em>not</em> caught. A rank is an input — it is in the
    /// character the transcript already carries, and the assistant saying "his Might at 11d"
    /// is repeating a decision rather than reporting a calculation.</para>
    ///
    /// <para><b>Three holes an adversarial pass drove straight through, all now closed.</b>
    /// The scan read <c>Text</c> only, so a figure in a <c>Title</c> or a <c>Blurb</c> — both
    /// printed on the list of recordings — was unguarded. It matched digits only, so "works
    /// out to nine" walked past. And its second direction listed the verbs it would accept
    /// (<c>of|is|at|:</c>), which is a closed set masquerading as a rule.</para>
    ///
    /// <para><b>And two more a later one found.</b> It never read the <em>character</em>,
    /// though the character is what the replay prints a sheet from — so a figure in a
    /// Motivation, a Quote, a Description, a Connection or a Flaw's narrative detail passed.
    /// See <see cref="EveryProseIn"/>. And the word set was the engine's vocabulary rather
    /// than the page's: <c>ReplayVerdict</c> labels the gap <c>Over by</c> and <c>Left</c>,
    /// and "nineteen over … with three to spare" — which is how anybody would write it —
    /// matched none of <c>HP|hero points|points|edge|health|resolve|budget</c>.</para>
    ///
    /// <para>The positional words will occasionally catch a sentence that meant nothing of the
    /// kind — "she left with two bags" is a match. That is the right way round for a guard
    /// whose failure mode is a wrong number on a page nobody can tell is wrong, and the
    /// message quotes what it matched, so rewording is a minute's work.</para>
    ///
    /// <para>What separates them from ordinary English is <b>punctuation, not distance</b>.
    /// "over" and "left" are common words, and the first attempt at this gave them a one-word
    /// window to keep Vera Nunn's "Seventy-one, an apron over a cardigan" out — which duly let
    /// "over by a full nineteen" through, three words being all it takes. A quoted figure and
    /// its label are in one clause; a description is not. So the window is three words as
    /// everywhere else, and what may sit between them is words and spaces.</para>
    ///
    /// <para><b>This rule is about shape and the one below is about value</b>, and both are
    /// needed. A recording saying "over by a full nineteen" is quoting a figure whether or not
    /// nineteen is the right answer — arguably worse if it is not — so it cannot be left to a
    /// check that compares against what the engine says.</para>
    /// </summary>
    [Fact]
    public void NoRecordedLineQuotesAFigureTheEngineIsSupposedToAnswer()
    {
        const string number =
            @"(\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen"
            + "|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty"
            + "|fifty|sixty|seventy|eighty|ninety|hundred)";

        // "points" on its own is in the set, and it was not: "she spends 40 points of the 75
        // the tier hands you" walked straight through. That is not an exotic phrasing — it is
        // how the rulebook and this app's own sheet ("Points Spent") write it.
        //
        // "cost" stays out. "It costs more than all six of her Abilities put together" is a
        // comparison rather than a quoted figure, and is exactly the sentence this surface
        // exists to allow: a recording that can say where the money went without saying how
        // much of it there was.
        const string figure = "(HP|hero points?|points?|edge|health|resolve|budget)";

        // The words the page itself uses for the gap: "Over by 19", "Left 3". They are also
        // ordinary English, so they are constrained differently — see the remarks.
        const string gap = "(over|overspent|overspend|under|left|remaining|spare|short)";

        // Both directions, with a short window either way rather than a list of verbs.
        //
        // The gap words take the same three-word window as the rest, but the words between
        // them and the number must be **words and spaces only**. That is what tells a quoted
        // figure from ordinary prose, and it is a better rule than the narrow window it
        // replaces: a figure and its label sit in one clause — "over by a full nineteen",
        // "three to spare" — while Vera Nunn's "Seventy-one, an apron over a cardigan" has a
        // hyphen and a comma in the way. A one-word window kept that description out and let
        // "over by a full nineteen" straight through.
        var quoted = Rx(
            $@"\b{number}\W+(\w+\W+){{0,3}}{figure}\b|\b{figure}\W+(\w+\W+){{0,3}}{number}\b"
            + $@"|\b{number}\s+(\w+\s+){{0,3}}{gap}\b|\b{gap}\s+(\w+\s+){{0,3}}{number}\b",
            RegexOptions.IgnoreCase);

        foreach (var transcript in All())
        {
            // Title and Blurb are prose the visitor reads before choosing a recording, and the
            // character is what the sheet at the end is printed from. All of it is held to the
            // same rule as a line inside the conversation.
            foreach (var (where, text) in EveryProseIn(transcript))
            {
                var match = quoted.Match(text);

                Assert.False(match.Success,
                    $"{transcript.Id} {where} says \"{match.Value}\". Every such figure has to "
                    + "come back from the engine in the browser, not out of the recording.");
            }
        }
    }

    /// <summary>
    /// <b>And no recorded line may carry a number the engine works out for the character
    /// beside it — whatever words are around it, or none.</b>
    ///
    /// <para>The rule above is a vocabulary, and a vocabulary can always be walked round. An
    /// adversarial pass wrote "She lands on 75 exactly, and the tier hands her 75 to spend"
    /// into a recorded line — her exact spend and her exact budget, twice in one sentence —
    /// and it matched nothing, because "lands on" and "hands her" are not on any list and
    /// never could be. So this asks the engine what the figures actually are and refuses those
    /// numerals outright. It is the rule <c>CLAUDE.md</c> states: <b>if a transcript ever holds
    /// a Hero Point total, that is the bug.</b></para>
    ///
    /// <para><b>Spelled out as well as in digits.</b> "over by a full nineteen" is the same
    /// quoted figure as "over by 19", and it slipped past the vocabulary rule too — that rule
    /// gives its positional words a one-word window, so three words of padding walk through
    /// it, and "overspent" does not contain the word "over" at all. Neither dodge survives
    /// asking what the number actually is, in either spelling.</para>
    ///
    /// <para><b>Two deliberate limits.</b> Figures under ten are left to the vocabulary rule:
    /// below that a digit on a page is as likely to be a count of Powers, a rank or a year, and
    /// the small figures are exactly the ones written with a word beside them — "three to
    /// spare" — which the rule above already catches. And the <b>Trait Cap is not in the set</b>,
    /// because it is a rank: ranks are inputs the transcript already carries and are allowed to
    /// be quoted. A rank written the way the rulebook writes one, <c>12d</c>, is not matched by
    /// a word-bounded <c>12</c> in any case.</para>
    /// </summary>
    [Fact]
    public void NoRecordedLineCarriesANumberTheEngineWorksOutForItsOwnCharacter()
    {
        const int smallest = 10;

        foreach (var transcript in All())
        {
            var figures = new SortedSet<int>();

            foreach (var x in EveryCharacter().Where(c => c.Transcript.Id == transcript.Id))
            {
                var tier = _f.Rules.GetTier(x.Character.SelectedTierId!)!;
                var spent = _f.Costs.TotalCost(x.Character);

                figures.UnionWith(
                [
                    spent,
                    tier.HeroPoints,
                    Math.Abs(tier.HeroPoints - spent),
                    _f.Derived.CalculateEdge(x.Character),
                    _f.Derived.CalculateHealth(x.Character),
                    _f.Derived.CalculateResolve(x.Character),
                    _f.Costs.PackageCost(x.Character),
                    _f.Costs.AbilityCost(x.Character),
                    _f.Costs.TalentCost(x.Character),
                    _f.Costs.TotalPowersCost(x.Character),
                    _f.Costs.TotalPerksCost(x.Character),
                    _f.Costs.TotalGearCost(x.Character)
                ]);
            }

            foreach (var figure in figures.Where(f => f >= smallest))
            {
                var spellings = InWords(figure)
                    .Select(Regex.Escape)
                    .Prepend(figure.ToString(System.Globalization.CultureInfo.InvariantCulture));

                var quoted = Rx($@"\b({string.Join('|', spellings)})\b", RegexOptions.IgnoreCase);

                foreach (var (where, text) in EveryProseIn(transcript))
                {
                    var match = quoted.Match(text);

                    Assert.False(match.Success,
                        $"{transcript.Id} {where} says \"{match.Value}\", which is {figure} — a "
                        + "figure the engine works out for the character this recording carries. "
                        + "It has to come back from the browser, not out of the recording.");
                }
            }
        }
    }

    /// <summary>
    /// A whole number written the way somebody would write it, in every spelling worth
    /// guarding: <c>seventy-five</c> and <c>seventy five</c>, <c>one hundred and five</c> and
    /// <c>one hundred five</c>. Only reached for figures of ten and over, so the single-word
    /// forms below twenty are here to be composed with rather than used alone.
    /// </summary>
    private static IEnumerable<string> InWords(int n)
    {
        string[] ones =
        [
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
            "seventeen", "eighteen", "nineteen"
        ];

        string[] tens =
        [
            "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
        ];

        if (n < 0 || n > 999) yield break;

        if (n < 20)
        {
            yield return ones[n];
            yield break;
        }

        if (n < 100)
        {
            if (n % 10 == 0)
            {
                yield return tens[n / 10];
                yield break;
            }

            yield return $"{tens[n / 10]}-{ones[n % 10]}";
            yield return $"{tens[n / 10]} {ones[n % 10]}";
            yield break;
        }

        var hundreds = $"{ones[n / 100]} hundred";

        if (n % 100 == 0)
        {
            yield return hundreds;
            yield break;
        }

        foreach (var rest in InWords(n % 100))
        {
            yield return $"{hundreds} {rest}";
            yield return $"{hundreds} and {rest}";
        }
    }

    /// <summary>
    /// The other half, without which the rule above is satisfied by a transcript that says
    /// nothing at all: the numbers have to be <em>gettable</em>. Every conversation carries a
    /// tier, because the tier is what a budget and a Trait Cap hang off — a character without
    /// one cannot be measured against anything, and the replay's whole closing move is
    /// measuring it.
    /// </summary>
    [Fact]
    public void EveryCharacterInEveryTranscriptCarriesATierToBeMeasuredAgainst() =>
        Assert.All(EveryCharacter(), x =>
            Assert.True(
                x.Character.SelectedTierId is { } id && _f.Rules.GetTier(id) is not null,
                $"{x.Transcript.Id} turn {x.Turn} has no tier, so nothing about it can be costed."));
}
