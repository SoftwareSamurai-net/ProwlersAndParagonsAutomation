using ProwlersAndParagonsAutomation.Play.Rules;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The book's own worked examples, replayed through the engine against the shipped data.</b>
///
/// <para><b>The rule these follow.</b> A mechanic is proved by a printed example or by a property,
/// never by a test that restates the code. Two transcriptions can agree and both be wrong — which is
/// why <c>data/rules/play</c> is held to the book by a reflection walk rather than by a second copy
/// of itself — and an engine checked against a test somebody wrote from the same reading of the page
/// is a third transcription carrying the same defect. The authors' own arithmetic cannot be talked
/// round.</para>
///
/// <para><b>The examples themselves live in <see cref="PlayWorkedExamples"/> and return verdicts</b>,
/// because <see cref="PlayEngineTwinTests"/> drives the byte-identical methods against a
/// deliberately broken copy of the data and requires the one it aimed at to say <c>FAIL</c>. A twin
/// with a doctored harness proves nothing, so there is exactly one copy of each example.</para>
/// </summary>
[Collection(SharedPlayRules.Name)]
public sealed class PlayEngineTests
{
    private readonly PlayRulesRepository _play;

    public PlayEngineTests(PlayFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _play = fixture.Play;
    }

    public static TheoryData<string> Examples()
    {
        var data = new TheoryData<string>();
        foreach (var name in PlayWorkedExamples.Names) data.Add(name);
        return data;
    }

    /// <summary>
    /// Every worked example comes out as printed, against the data the repository ships.
    /// </summary>
    [Theory]
    [MemberData(nameof(Examples))]
    public void AWorkedExampleComesOutAsPrinted(string name) =>
        Assert.Equal(PlayWorkedExamples.Pass, PlayWorkedExamples.Run(name, _play));

    /// <summary>
    /// <b>The list of examples is the list the book prints, and it is eight.</b>
    ///
    /// <para>Without this, an example quietly dropped from <see cref="PlayWorkedExamples.Names"/>
    /// takes its whole mechanic out of the suite while every remaining test stays green — the same
    /// failure <c>TheTenGrittyRulesAreEachATableSetting</c> exists to prevent one file along. The
    /// count is asserted with the names beside it, so a rename shows as a rename rather than as a
    /// number that still adds up.</para>
    /// </summary>
    [Fact]
    public void TheEightExamplesTheBookPrintsAreAllHere()
    {
        Assert.Equal(
            [
                "p.67 arm wrestling",
                "p.74 movement",
                "p.74 chase",
                "p.76 special effect",
                "p.76 breaking free",
                "p.79 fatal damage",
                "p.81 example of combat",
                "p.85 adversity"
            ],
            PlayWorkedExamples.Names);
    }

    /// <summary>
    /// <b>The harness reports a failure rather than swallowing one.</b> An unknown name is fed to the
    /// same dispatcher the theory above uses, and it has to come back saying <c>FAIL</c> — not throw,
    /// and not return <c>PASS</c>. Without this, a dispatcher that had started answering <c>PASS</c>
    /// to everything would satisfy all eight rows of the theory perfectly.
    /// </summary>
    [Fact]
    public void TheHarnessSaysFailWhenAnExampleDoesNotComeOut()
    {
        var verdict = PlayWorkedExamples.Run("no such example", _play);

        Assert.StartsWith("FAIL", verdict, StringComparison.Ordinal);
        Assert.NotEqual(PlayWorkedExamples.Pass, verdict);
    }
}
