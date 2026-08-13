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
    /// </summary>
    [Fact]
    public void NoRecordedConversationAsksMoreThanThreeQuestions() =>
        Assert.All(All(), t =>
        {
            var asked = t.Turns
                .Where(turn => turn.Speaker == TranscriptSpeaker.Assistant)
                .Sum(turn => turn.Text.Count(c => c == '?'));

            Assert.True(asked <= 3, $"{t.Id} asks {asked} questions. The policy allows three.");
        });

    // ── The rot guard ───────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Every character in every transcript is one the engine can price.</b> Reading it is
    /// already strict — <see cref="TranscriptLibrary"/> refuses a field name a character no
    /// longer has — and this is the other half: an id that has gone, a rank that is now over
    /// a cap, a Trait under a package floor.
    ///
    /// <para>The allowance is deliberately one code and not a per-file list. Two of these
    /// conversations are <em>about</em> a character being over the tier's budget — the draft
    /// that had to give something up, and the Villain, who has no budget at all under Ch.9.
    /// Everything else the validator can say means the transcript has drifted away from the
    /// rules, whatever it is.</para>
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
    /// </summary>
    [Fact]
    public void NoRecordedLineQuotesAFigureTheEngineIsSupposedToAnswer()
    {
        // Both directions: "12 Hero Points" and "Resolve of 5". Word characters between the
        // number and the noun are allowed for "125 spare Hero Points" and the like.
        var quoted = Rx(
            @"\b\d+\s*(\w+\s+){0,2}(HP|hero points?|edge|health|resolve)\b"
            + @"|\b(HP|hero points?|edge|health|resolve)\s+(of|is|at|:)\s*\d+",
            RegexOptions.IgnoreCase);

        foreach (var transcript in All())
        {
            foreach (var (turn, i) in transcript.Turns.Select((t, i) => (t, i + 1)))
            {
                var match = quoted.Match(turn.Text);

                Assert.False(match.Success,
                    $"{transcript.Id} turn {i} says \"{match.Value}\". Every such figure has to "
                    + "come back from the engine in the browser, not out of the recording.");
            }
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
