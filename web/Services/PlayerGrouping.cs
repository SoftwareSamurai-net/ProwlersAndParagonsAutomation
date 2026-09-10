namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One player's row in a list that would otherwise print one row per character — a lone
/// character drawn exactly as it always was, two or more folded under a parent naming the player
/// rather than any one of them.
/// </summary>
/// <typeparam name="T">
/// One row before grouping — a contributor, a roster entry, anything with a shared key and a
/// number.
/// </typeparam>
/// <param name="Key">
/// The shared key this group was built from, or null for a row that grouped with nobody. Never
/// shown: it exists to tell two groups apart, not to be printed.
/// </param>
/// <param name="Total">The sum across every row this group holds.</param>
/// <param name="Members">
/// The rows themselves, in the order <see cref="PlayerGrouping.Group{T}"/> states.
/// </param>
public sealed record PlayerGroup<T>(string? Key, int Total, IReadOnlyList<T> Members)
{
    /// <summary>
    /// Whether this is more than one row folded together — the one condition that draws a parent
    /// row at all. A group of one renders exactly as its single row would have before grouping
    /// existed, with no disclosure and nothing to open.
    /// </summary>
    public bool IsGrouped => Members.Count > 1;
}

/// <summary>
/// Folds rows that share an owner into one group apiece, by any key a caller names.
///
/// <para><b>Built once, for two callers.</b> The campaign's shared-object ledger uses this to say
/// when two contributors are one player holding two characters — see
/// <see cref="CampaignAssets.Ledger"/> — and item 21 (character variants) is expected to draw a
/// roster the same way once it exists: several characters, one identity, and a decision about
/// whether the list should say so. The grouping rule lives here rather than in either screen so
/// the two do not have to agree about it independently.</para>
/// </summary>
public static class PlayerGrouping
{
    /// <summary>
    /// Fold <paramref name="rows"/> sharing a key into one <see cref="PlayerGroup{T}"/> apiece.
    ///
    /// <para><b>A null key groups with nobody, ever.</b> Two rows that both answer null to
    /// <paramref name="key"/> are two separate facts as far as this is concerned, each its own
    /// group of one. This is what lets an older answer — one that has not learned to say which
    /// rows share an owner, and so names every key null — draw exactly the flat list it always
    /// has, rather than folding unrelated rows together under a shared "null" bucket.</para>
    ///
    /// <para><b>Ordering: largest total first, then by the group's own label, and the same two
    /// rules again inside a group.</b> "Largest player first, then largest character within, ties
    /// by label" is one rule applied twice rather than two different ones — a tie at the top is
    /// broken exactly how a tie one level down is.</para>
    /// </summary>
    /// <param name="rows">What is being grouped.</param>
    /// <param name="key">The shared identity, or null for something that shares with nothing.</param>
    /// <param name="amount">The number a group sums and sorts by.</param>
    /// <param name="label">What a row (or, for the tie-break, a group's first row) is called.</param>
    public static IReadOnlyList<PlayerGroup<T>> Group<T>(
        IEnumerable<T> rows, Func<T, string?> key, Func<T, int> amount, Func<T, string> label)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(label);

        var ordered = rows
            .OrderByDescending(amount)
            .ThenBy(label, StringComparer.CurrentCulture)
            .ToList();

        var groups = new List<PlayerGroup<T>>();
        var claimed = new bool[ordered.Count];

        for (var i = 0; i < ordered.Count; i++)
        {
            if (claimed[i]) continue;

            var rowKey = key(ordered[i]);

            if (rowKey is null)
            {
                groups.Add(new PlayerGroup<T>(null, amount(ordered[i]), [ordered[i]]));
                claimed[i] = true;
                continue;
            }

            var members = new List<T>();
            for (var j = i; j < ordered.Count; j++)
            {
                if (claimed[j] || !string.Equals(key(ordered[j]), rowKey, StringComparison.Ordinal))
                {
                    continue;
                }

                members.Add(ordered[j]);
                claimed[j] = true;
            }

            groups.Add(new PlayerGroup<T>(rowKey, members.Sum(amount), members));
        }

        return [.. groups
            .OrderByDescending(g => g.Total)
            .ThenBy(g => label(g.Members[0]), StringComparer.CurrentCulture)];
    }
}
