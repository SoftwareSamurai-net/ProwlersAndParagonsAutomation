using System.Globalization;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Turns the rules data's own keys into something a player can read.
///
/// <para>Most things in the rules carry a printed name and should be looked up by it —
/// <c>GetAbility(id)?.Name</c>, never this. What is left is the handful of values that are
/// dictionary keys and nothing else: a cost variant (<c>kinetic</c>), a Pro or Con grade
/// (<c>severely_limited</c>), a gear feature grade (<c>very_powerful</c>). Those have no
/// name in the data, so the key is all there is, and a key set in sentence case reads as an
/// option rather than as a field name.</para>
/// </summary>
public static class Labels
{
    /// <summary>
    /// <c>very_powerful</c> becomes <c>Very Powerful</c>. Title case rather than a bare
    /// underscore swap, because "very powerful" beside "Powerful" in the same list looks
    /// like a typo rather than a second grade of the same feature.
    ///
    /// <para>Invariant culture: the input is an ASCII rules id and the output is a rulebook
    /// term, neither of which changes with the browser's locale. Under a Turkish one the
    /// current culture upper-cases <c>i</c> to <c>İ</c>, so <c>item</c> would read
    /// <c>İtem</c>.</para>
    /// </summary>
    public static string Humanise(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var words = key.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', words.Select(w =>
            char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..]));
    }
}
