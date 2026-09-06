namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Which memberships in a list are holding a sheet with nothing on it.
///
/// <para><b>Because the list level was the one place that still drew an empty submission as a
/// character.</b> Both screens open a membership and say so — "The submission was empty — ask the
/// player to resubmit" — but a reader arrives at the list first, and there the same row said
/// <em>Approved</em> beside the character's name. A GM scanning their roster, and a player
/// checking where their character stands, were both told the game was holding a character it is
/// not holding. A state that reads as emptiness is not the same as nothing being there, and the
/// reader is owed which one it is at the level they are reading.</para>
///
/// <para><b>It costs a read per row and there is no cheaper answer available.</b> A list row
/// carries the two slot flags and no payload — the server never parses one — so whether a slot
/// holds a character can only be learned by opening it. The rows that have never had anything
/// sent are skipped, and the answer is worked out once per refresh rather than per render. A row
/// that could not be read is left unmarked, which is the safe direction: "empty submission" over
/// a sheet nobody managed to fetch would be exactly the false alarm these sentences exist to
/// avoid.</para>
///
/// <para><b>The slot asked about is the one the screens draw</b> — the waiting snapshot where
/// there is one, and the clone otherwise. That is <c>CampaignApproval</c>'s own order, and a
/// marker about the settled sheet beside a row whose standing says "changes pending" would be
/// about a different sheet from the one the reader is being pointed at.</para>
/// </summary>
public static class EmptySubmissions
{
    /// <summary>
    /// What a row says when the sheet behind it has nothing on it. One spelling, shared by both
    /// lists, because two of them is two things to keep in step.
    /// </summary>
    public const string Marker = "empty submission";

    /// <summary>
    /// The ids of the memberships whose current sheet has nothing on it. Empty when nothing does,
    /// and empty when nothing could be read.
    /// </summary>
    public static async Task<IReadOnlySet<string>> AmongAsync(
        ApiMembershipStore memberships,
        CharacterSession session,
        IEnumerable<MembershipSummary>? rows)
    {
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(session);

        var empty = new HashSet<string>(StringComparer.Ordinal);

        if (rows is null) return empty;

        foreach (var row in rows.Where(r => r.HasPending || r.HasApproved))
        {
            if (await memberships.ReadAsync(row.Id) is not { } detail) continue;

            // The waiting snapshot first, for the reason in the remarks: it is the sheet both
            // screens draw when there is one.
            var showing = detail.Pending ?? detail.Approved;

            if (showing is not null && session.HasNothingOnIt(showing)) empty.Add(row.Id);
        }

        return empty;
    }
}
