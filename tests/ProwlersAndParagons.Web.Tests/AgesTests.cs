using ProwlersAndParagonsAutomation.Web.Services;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// How long ago a character was touched, in the words a reader would use.
///
/// <para><b>"Now" is a parameter, which is the whole of what makes this testable</b> — a helper
/// that read the clock itself could only be checked against the clock it ran on, and a test written
/// against a moving answer is a test that fails on a slow machine at midnight. Every case below
/// pins a fixed instant and asks about a fixed offset from it.</para>
/// </summary>
public sealed class AgesTests
{
    /// <summary>
    /// A fixed instant, so nothing here depends on when the suite runs. The date is arbitrary and
    /// is deliberately not "today" — a fixture that drifts with the calendar is the thing this
    /// class's whole shape exists to avoid.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static string? Ago(TimeSpan elapsed) =>
        Ages.Since((Now - elapsed).ToUnixTimeMilliseconds(), Now);

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(1, "just now")]
    [InlineData(5, "5 minutes ago")]
    [InlineData(59, "59 minutes ago")]
    [InlineData(61, "an hour ago")]
    [InlineData(60 * 5, "5 hours ago")]
    [InlineData(60 * 30, "yesterday")]
    [InlineData(60 * 24 * 3, "3 days ago")]
    [InlineData(60 * 24 * 60, "2 months ago")]
    public void ATimeIsSaidTheWayAReaderWouldSayIt(int minutesAgo, string expected) =>
        Assert.Equal(expected, Ago(TimeSpan.FromMinutes(minutesAgo)));

    /// <summary>
    /// Past a year a relative phrase stops helping: "14 months ago" and "2 years ago" are both true
    /// of the same character and neither tells a reader which one it is.
    /// </summary>
    [Fact]
    public void PastAYearItSaysOnlyThatItIsOld() =>
        Assert.Equal("over a year ago", Ago(TimeSpan.FromDays(400)));

    /// <summary>
    /// <b>Nothing at all for a timestamp of zero, and this is the case that matters.</b> The legacy
    /// slot — the one character a browser could hold before there was a list — is synthesised into
    /// the list with <c>UpdatedAt = 0</c>, because nothing ever recorded when it was written.
    /// Formatted naively that reads "over a year ago", which is a confident answer to a question
    /// nobody can answer, printed beside somebody's oldest character.
    /// </summary>
    [Fact]
    public void AnUnrecordedTimeSaysNothing()
    {
        Assert.Null(Ages.Since(0, Now));
        Assert.Null(Ages.Since(-1, Now));
    }

    /// <summary>
    /// A stamp from the future says nothing either. Two clocks that disagree — a browser's and a
    /// server's — is an ordinary thing, and "in -3 minutes" is not a sentence.
    /// </summary>
    [Fact]
    public void ATimeInTheFutureSaysNothing() =>
        Assert.Null(Ages.Since(Now.AddMinutes(5).ToUnixTimeMilliseconds(), Now));

    /// <summary>
    /// The positive control for the two absences above: a real stamp really does produce a phrase,
    /// so "null" is a decision this class made rather than the only thing it can do.
    /// </summary>
    [Fact]
    public void AnOrdinaryTimeReallyDoesProduceAPhrase() =>
        Assert.NotNull(Ages.Since(Now.AddHours(-3).ToUnixTimeMilliseconds(), Now));
}
