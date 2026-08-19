using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Where the character being built is kept between visits.
///
/// <para><b>An interface so that a server-backed store is a registration change rather than a
/// rewrite.</b> One implementation ships — the browser's local storage — and the shape here is
/// deliberately the smallest thing every caller actually uses: save the inputs, load them back,
/// throw them away. Nothing exposes where they went.</para>
///
/// <para><b>What travels is <see cref="CharacterSheet"/>, the inputs — never the export.</b> The
/// export is a report carrying derived stats, costs and findings, all of which are answers, and
/// reading one back would rebuild a character from its own conclusions. That holds whatever the
/// storage is, so it is stated here rather than in one implementation.</para>
///
/// <para><b>Nothing here may throw.</b> A character from an older build, hand-edited storage, a
/// browser that refuses storage, and one day a network that is not there are all the same case to
/// a caller: there is no character, start empty. Restoring happens before the first render, so an
/// exception is not a lost character but an app that does not start.</para>
/// </summary>
public interface ICharacterStore
{
    /// <summary>Write the character down. Failure is not worth reporting to the player.</summary>
    Task SaveAsync(CharacterSheet sheet, SheetMode mode);

    /// <summary>The stored character, or null if there is none this build can trust.</summary>
    Task<(CharacterSheet Sheet, SheetMode Mode)?> LoadAsync();

    /// <summary>Throw the stored character away.</summary>
    Task ClearAsync();
}
