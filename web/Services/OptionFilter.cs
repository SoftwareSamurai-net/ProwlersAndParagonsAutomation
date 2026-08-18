namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The live filter over a list of pickable options, and the tally of what it let through.
///
/// <para><b>This exists so the filter is written once rather than five times.</b> Pros, Cons,
/// Perks, Flaws, gear features and Powers are all pick-from-a-list surfaces and only the
/// Powers tab had a search box; the report from the table was that scrolling the others is
/// annoying, and Powers alone is 141 entries. <c>OptionList</c> owns the box and this type,
/// and <c>OptionRow</c> asks it whether to draw itself — so a new list of options gets the
/// filter by being a list of options.</para>
///
/// <para><b>It is passed down as a cascading value and replaced on every render pass rather
/// than mutated in place.</b> Blazor only re-renders a child when something it can compare
/// has changed, and a cascading value that is the same object with different contents is not
/// something it can compare: the rows would keep their last answer and the list would stop
/// responding to the box above it. <see cref="Pass"/> is what makes each render's value
/// distinct, which is the whole reason it is on the record.</para>
/// </summary>
/// <param name="Query">What the reader has typed. Empty means everything matches.</param>
/// <param name="Tally">Where the rows record what happened; one per render pass.</param>
/// <param name="Pass">Distinguishes this render's value from the last one. See above.</param>
public sealed record OptionFilter(string Query, OptionTally Tally, int Pass)
{
    /// <summary>
    /// Whether a row with this text survives the filter, counting it either way.
    ///
    /// <para><b>Every field a row can be found by is passed in, including ones it does not
    /// display.</b> The Powers tab searched names <i>and</i> tags, and tags are never printed
    /// — so a filter that could only read what was on screen would have been a quiet
    /// regression on the one list that already had a search box.</para>
    ///
    /// <para>Matching is <c>Contains</c>, deliberately, and not the word-and-prefix rule the
    /// MCP server's Power search uses. That rule exists because a model acts on its answer and
    /// a bad match there is worse than none; here a person is watching a list shorten as they
    /// type, and "plast" ought to reach Plasticity while they are still typing it.</para>
    /// </summary>
    public bool Admits(params string?[] text)
    {
        Tally.Total++;

        if (!Matches(Query, text)) return false;

        Tally.Shown++;
        return true;
    }

    /// <summary>
    /// The matching rule itself, with no tally and no render pass attached to it.
    ///
    /// <para><b>It is separate so that the command palette matches the way the lists match,
    /// rather than growing a second rule that drifts.</b> A reader who has learnt that "plast"
    /// finds Plasticity in the Powers list has learnt something about this app, and a palette
    /// that answered differently would be teaching them it was about one list. The tallying
    /// wrapper above stays, because a list counts what it drew and a palette does not.</para>
    /// </summary>
    /// <param name="query">What the reader has typed. Empty matches everything.</param>
    /// <param name="text">Every field this row can be found by, printed or not.</param>
    public static bool Matches(string query, params string?[] text)
    {
        if (query.Length == 0) return true;

        foreach (var candidate in text)
        {
            if (candidate is null) continue;
            if (candidate.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}

/// <summary>
/// What one render pass let through. Mutable and deliberately not part of a record's value
/// equality — it is filled in by the rows <i>after</i> the list has already drawn itself, so
/// the list reads it on the pass after the one that filled it.
/// </summary>
public sealed class OptionTally
{
    public int Total { get; set; }
    public int Shown { get; set; }
}
