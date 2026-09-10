using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// What one pass over a campaign's memberships turned up: which rows are holding a sheet with
/// nothing on it, and the clones the game's own books are worked out from.
/// </summary>
/// <param name="Empty">
/// The ids of the memberships whose current sheet has nothing on it — see
/// <see cref="EmptySubmissions"/>, which is where the cost of knowing is argued.
/// </param>
/// <param name="Clones">
/// The sheet this campaign holds for each member that has one, with the membership id, the
/// label the roster draws it under, and the <see cref="MembershipSummary.PlayerKey"/> hint off
/// the same row — carried through so <see cref="CampaignAssets.Ledger"/> can group two
/// characters funded by one player. <b>The clone and never the waiting snapshot</b>: a snapshot is
/// a request the GM has not decided about, so counting its contributions would spend a player's
/// Hero Points on a shared object before anybody agreed they were spent.
/// </param>
public sealed record RosterRead(
    IReadOnlySet<string> Empty,
    IReadOnlyList<(string MembershipId, string Who, string? PlayerKey, CharacterSheet Sheet)> Clones);

/// <summary>
/// One read per membership, with both of the campaign page's per-member answers taken off it.
///
/// <para><b>It exists because the second answer would otherwise have doubled the requests.</b> A
/// list row carries two slot flags and no payload — the server never parses one — so anything
/// about the sheet behind a row can only be learned by opening it. That was already a read per
/// player for the empty-submission marker; summing a shared object's budget needs the same
/// payloads, and a second pass would have asked for every one of them twice on the one screen
/// that already costs the most to open.</para>
///
/// <para><b>A row that could not be read is simply absent</b>, from both answers. That is the safe
/// direction for each of them: an "empty submission" marker over a sheet nobody managed to fetch
/// is the false alarm those markers exist to avoid, and a budget that reads low is an honest
/// consequence of a payload that did not arrive, where a row invented for it would not be.</para>
/// </summary>
public static class CampaignRoster
{
    /// <summary>
    /// Open every membership that has something in it, once.
    /// </summary>
    /// <param name="memberships">The membership store.</param>
    /// <param name="session">The reader's session, for the emptiness question.</param>
    /// <param name="rows">The campaign's memberships, or null when that list could not be read.</param>
    public static async Task<RosterRead> ReadAsync(
        ApiMembershipStore memberships,
        CharacterSession session,
        IEnumerable<MembershipSummary>? rows)
    {
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(session);

        var empty = new HashSet<string>(StringComparer.Ordinal);
        var clones = new List<(string, string, string?, CharacterSheet)>();

        if (rows is null) return new RosterRead(empty, clones);

        foreach (var row in rows.Where(r => r.HasPending || r.HasApproved))
        {
            if (await memberships.ReadAsync(row.Id) is not { } detail) continue;

            if (EmptySubmissions.ShowsNothing(session, detail)) empty.Add(row.Id);

            if (detail.Approved is { } clone) clones.Add((row.Id, row.Label, row.PlayerKey, clone));
        }

        return new RosterRead(empty, clones);
    }
}
