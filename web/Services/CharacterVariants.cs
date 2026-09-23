using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// One row a roster can draw — enough of a character to place it in a variant tree and nothing
/// more, the same cut <see cref="SavedCharacterSummary"/> already makes for the same reason: a
/// list of many should not have to read a full payload to draw a row.
/// </summary>
/// <param name="Id">The character's own id.</param>
/// <param name="Name">What to call it on screen.</param>
/// <param name="Variant">
/// What this character is a version of, or null for a root. The engine's own
/// <see cref="CharacterVariant"/> — this is not a second copy of the shape, only of the sheet
/// it travels attached to.
/// </param>
public readonly record struct VariantRow(string Id, string Name, CharacterVariant? Variant);

/// <summary>
/// A root character with the versions of it this browser (or account) also holds, in the order a
/// roster draws them.
/// </summary>
/// <param name="Root">
/// The root row, or null when nothing on this list actually holds the id every child names — see
/// <see cref="CharacterVariants.Group"/>'s remarks on an orphaned chain.
/// </param>
/// <param name="Children">
/// Every row naming <paramref name="Root"/>'s id, later version first for two of the same
/// vintage, then alphabetically — <see cref="CharacterVariants.Label"/> order and then name.
/// </param>
public sealed record VariantFamily(VariantRow? Root, IReadOnlyList<VariantRow> Children)
{
    /// <summary>
    /// Whether this family is worth drawing as a tree at all. A root with no children, or a
    /// lone orphan, renders exactly as a flat row always has — see item 21's own design: "a
    /// root with no variants renders exactly as today."
    /// </summary>
    public bool IsGrouped => Children.Count > 0;
}

/// <summary>
/// Item 21's slice one: builds the tree a roster draws out of the flat list every character's own
/// <see cref="CharacterSheet.Variant"/> travels with, decides what a link's kind reads as on
/// screen, and refuses the two links the engine cannot: a self-link and a cycle.
///
/// <para><b>Built on <see cref="PlayerGrouping"/>, which was written with this in mind.</b> A
/// root's own key is its id; a child's key is the root id it names — so grouping by that one
/// string, with the root weighted above its children, reproduces the tree with no new grouping
/// mechanism. The weight is what
/// <see cref="PlayerGrouping.Group{T}"/>'s own <c>amount</c> parameter is for: 1 for a row that
/// is itself a root (whether or not anybody names it — see <see cref="Group"/>'s remarks on an
/// orphan), 0 for a row that names another. Ties are broken by the label — later version, then
/// as-seen-by, then alternate form, then name — which is the order <see cref="Label"/> reads
/// off, not a second sort the roster has to agree with independently.</para>
///
/// <para><b>Whether a root is actually held is a question about the roster, so it is answered
/// here rather than in the engine</b> — the same line <c>CampaignAssets.Orphaned</c> draws
/// against <c>UNKNOWN_CAMPAIGN_ASSET</c>. The engine's own <c>VARIANT_WITHOUT_ROOT</c> and
/// <c>UNKNOWN_VARIANT_KIND</c> are about the field's own shape; this is about whether the id it
/// names actually appears in front of the reader now.</para>
/// </summary>
public static class CharacterVariants
{
    /// <summary>
    /// What a child's link reads as, in the reader's own words rather than the data's — answering
    /// what a reader would actually ask ("what is this row?"), per <c>docs/guide/browser.md</c>'s
    /// copy rule. Null for a kind this app does not know, which is <c>UNKNOWN_VARIANT_KIND</c>'s
    /// own territory and not a label to guess at.
    /// </summary>
    public static string? Label(string kind) => kind switch
    {
        CharacterVariant.Later => "later version",
        CharacterVariant.AsSeenBy => "as seen by another audience",
        CharacterVariant.AlternateForm => "alternate form",
        _ => null,
    };

    /// <summary>
    /// A row out of the index — <c>SavedCharacterSummary</c>'s <c>VariantOf</c>/<c>VariantKind</c>
    /// pair, put back together into the engine's own shape. Both null answers a root; either one
    /// alone is a payload the engine itself would call <c>VARIANT_WITHOUT_ROOT</c> or
    /// <c>UNKNOWN_VARIANT_KIND</c> on the next validate, so it is read as a root here too rather
    /// than as a half-built link this method would have to invent a meaning for.
    /// </summary>
    public static VariantRow FromSummary(SavedCharacterSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var variant = summary is { VariantOf.Length: > 0, VariantKind.Length: > 0 }
            ? new CharacterVariant(summary.VariantOf!, summary.VariantKind!)
            : null;

        return new VariantRow(summary.Id, summary.Label, variant);
    }

    /// <summary>
    /// The finding a browser-side check can make that the engine cannot: whether the root a
    /// version names is actually held by this account or browser — the same shape as
    /// <c>UNKNOWN_CAMPAIGN</c>, computed here rather than in <c>CharacterValidator</c> because
    /// answering it means asking the roster, which the engine may never do.
    /// </summary>
    public const string RootNotHeldCode = "VARIANT_ROOT_NOT_HELD";

    /// <summary>
    /// The sentence for <see cref="RootNotHeldCode"/> — reported, never repaired: the link stays
    /// on the character exactly as <see cref="CharacterVariant"/>'s own remarks describe every
    /// other finding here.
    /// </summary>
    public static string RootNotHeldMessage(VariantRow row) =>
        $"{row.Name} is recorded as a version of a character this account does not hold. "
        + "Nothing has been changed — put the root character back, or clear the link.";

    /// <summary>
    /// The order children of one root draw in: by what their link reads as, then by name. Reading
    /// off <see cref="Label"/>'s own case order rather than a second list keeps the two from
    /// disagreeing about which kind comes first.
    /// </summary>
    private static readonly string[] KindOrder =
        [CharacterVariant.Later, CharacterVariant.AsSeenBy, CharacterVariant.AlternateForm];

    /// <summary>
    /// Every root in <paramref name="rows"/> with the versions of it also in the list, in the
    /// order a roster draws them: alphabetically by the root's own name, a lone root exactly as
    /// it always rendered, and a chain whose named root is not itself in <paramref name="rows"/>
    /// rendered as its own flat family with no root at all — see
    /// <see cref="VariantFamily.Root"/> and the item 21 design's own words: "Orphan (root not
    /// held) renders flat with the Warning finding on its row."
    ///
    /// <para><b>A row with no <see cref="CharacterVariant"/> is always a root</b>, whether or not
    /// anything names it. A row that names another is never a root, even if something else names
    /// <em>it</em> in turn. <b>This is one level deep, on purpose</b> — the owner's own examples
    /// (Cael Hughes' three tellings, Lena's pair) are siblings of one root rather than a chain, so
    /// nothing here needs to nest a version three deep. A version <em>of</em> a version groups by
    /// the parent it names, which is not present in that group as a root — it draws as its own
    /// orphan family, exactly as a link to a root nobody holds does. Deepening this is a later
    /// slice's problem, if the roster ever grows one.</para>
    /// </summary>
    public static IReadOnlyList<VariantFamily> Group(IEnumerable<VariantRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var all = rows.ToList();

        // Never null: a root's own key is its id, and a child's is the id it names — both are
        // plain strings, so PlayerGrouping's "shares with nobody" branch for a null key never
        // fires here.
        static string Key(VariantRow r) =>
            r.Variant is { OfCharacterId.Length: > 0 } v ? v.OfCharacterId : r.Id;

        // A root weighs 1 and a child weighs 0, so PlayerGrouping's own "largest first" puts the
        // root at the head of its family without a second sort — and an orphan chain, which has
        // no row weighing 1, sorts to the foot of the whole list, which is where a broken link
        // belongs relative to families that are actually rooted.
        static int Weight(VariantRow r) => r.Variant is null ? 1 : 0;

        var groups = PlayerGrouping.Group(all, Key, Weight, r => r.Name);

        return
        [
            .. groups.Select(g =>
            {
                var rootRow = g.Members.SingleOrDefault(
                    m => m.Variant is null && string.Equals(m.Id, g.Key, StringComparison.Ordinal));

                // A default VariantRow (Id null) means no member of this group is the root the
                // rest name — an orphan chain, per this method's own remarks.
                var root = rootRow.Id is null ? (VariantRow?)null : rootRow;

                var children = g.Members
                    .Where(m => root is null || !string.Equals(m.Id, root.Value.Id, StringComparison.Ordinal))
                    .OrderBy(m => Array.IndexOf(KindOrder, m.Variant?.Kind ?? ""))
                    .ThenBy(m => m.Name, StringComparer.CurrentCulture)
                    .ToList();

                return new VariantFamily(root, children);
            })
        ];
    }

    /// <summary>
    /// Whether naming <paramref name="rootId"/> as the root of <paramref name="characterId"/>
    /// would make a character a version of itself, directly or through a chain — refused where
    /// the link is made, per item 21's design: "a cycle (A of B, B of A) or a self-link refused
    /// where the link is made, not repaired." The engine cannot do this: it sees one sheet, and a
    /// cycle is a fact about the whole roster.
    /// </summary>
    /// <param name="characterId">The character about to be given a link.</param>
    /// <param name="rootId">The id it would name as its root.</param>
    /// <param name="lookup">Every other character's own recorded root, by id.</param>
    public static bool WouldCreateCycle(
        string characterId, string rootId, IReadOnlyDictionary<string, string?> lookup)
    {
        ArgumentNullException.ThrowIfNull(characterId);
        ArgumentNullException.ThrowIfNull(rootId);
        ArgumentNullException.ThrowIfNull(lookup);

        // Walk the chain the candidate root already sits in, and one visited set carries every
        // case: a self-link sets current to characterId on the very first pass, and the set
        // already holds it because it is seeded with characterId; an ordinary cycle revisits
        // some node already walked, of which characterId is only one possibility. A separate
        // `characterId == rootId` line, and a separate `next == characterId` line, were both
        // tried here and neither ever changed an answer — every case either reaches `return
        // false` because the chain runs out, or is caught by `seen.Add` failing, one step sooner
        // or later than a dedicated check would have caught it. Removed rather than kept as
        // decoration: a line that cannot be made to fail by any input is not a guard.
        //
        // Bounded by the number of characters this browser holds, so a chain somebody
        // hand-edited into a genuine loop terminates here rather than looping forever.
        var seen = new HashSet<string>(StringComparer.Ordinal) { characterId };
        var current = rootId;

        for (var steps = 0; steps <= lookup.Count; steps++)
        {
            if (!seen.Add(current)) return true;
            if (!lookup.TryGetValue(current, out var next) || next is null) return false;
            current = next;
        }

        return true; // the walk outran every character this browser holds — treat it as a loop
    }
}
