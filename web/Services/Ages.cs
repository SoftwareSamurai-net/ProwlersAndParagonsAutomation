namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// How long ago something was touched, in the words a reader would use.
///
/// <para><b>It exists because a list of characters needs to be told apart, and the only thing the
/// index holds that can do that is a timestamp.</b> <see cref="SavedCharacters"/> keeps labels and
/// <c>UpdatedAt</c> and nothing else — a Hero Point figure would cost a read, a cost and a validate
/// per row, which that class's own remarks refuse. So a row carries a time, and this turns the
/// number into a phrase.</para>
///
/// <para><b>"Now" is a parameter rather than read here</b>, which is the whole of what makes this
/// testable: a helper that called <see cref="DateTimeOffset.UtcNow"/> itself could only be checked
/// against the clock it was run on, and a test written against a moving answer is a test that fails
/// on a slow machine at midnight.</para>
/// </summary>
public static class Ages
{
    /// <summary>
    /// A phrase for how long ago <paramref name="updatedAtUnixMs"/> was, or <c>null</c> when there
    /// is nothing honest to say.
    ///
    /// <para><b>Null for a timestamp of zero, and that is not a tidy-up.</b> The legacy slot — the
    /// one character a browser could hold before there was a list — is synthesised into the list
    /// with <c>UpdatedAt = 0</c>, because nothing ever recorded when it was written. Formatted
    /// naively that reads "edited 56 years ago", which is a confident answer to a question nobody
    /// can answer. Null for a time in the future too: a clock that disagrees with the one that
    /// wrote the record is not a thing to report as a negative age.</para>
    ///
    /// <para>The scale is deliberately coarse. This is a label that helps a reader pick the right
    /// row out of five, not a log line — "3 days ago" and "3 days and 4 hours ago" answer the same
    /// question and only one of them is readable at a glance.</para>
    /// </summary>
    public static string? Since(long updatedAtUnixMs, DateTimeOffset now)
    {
        if (updatedAtUnixMs <= 0) return null;

        var then = DateTimeOffset.FromUnixTimeMilliseconds(updatedAtUnixMs);
        var elapsed = now - then;

        if (elapsed < TimeSpan.Zero) return null;

        if (elapsed < TimeSpan.FromMinutes(2)) return "just now";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes} minutes ago";
        if (elapsed < TimeSpan.FromHours(2)) return "an hour ago";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours} hours ago";
        if (elapsed < TimeSpan.FromDays(2)) return "yesterday";
        if (elapsed < TimeSpan.FromDays(31)) return $"{(int)elapsed.TotalDays} days ago";
        if (elapsed < TimeSpan.FromDays(365)) return $"{(int)(elapsed.TotalDays / 30)} months ago";

        // A year is where a relative phrase stops helping: "14 months ago" and "2 years ago" are
        // both true of the same character and neither tells a reader which one it is. Past this
        // point the only useful thing left is that it is old.
        return "over a year ago";
    }
}
