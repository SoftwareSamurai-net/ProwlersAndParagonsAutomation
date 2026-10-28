using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// What a character's spendable pool is called on screen: Resolve for a Hero, Adversity for a
/// Villain.
///
/// <para><b>The owner's ask of 2026-10-28, for flavour.</b> The figure is the engine's Resolve
/// either way; a Villain's sheet names it after the pool a Villain's side spends, so a campaign
/// played as Villains reads right. A presentation word, so it lives here and never in
/// <c>engine/</c> or <c>sheets/</c>, which may not read the palette flag.</para>
/// </summary>
public static class PoolName
{
    /// <summary>The word for this character's pool.</summary>
    public static string For(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.IsVillain ? "Adversity" : "Resolve";
    }
}
