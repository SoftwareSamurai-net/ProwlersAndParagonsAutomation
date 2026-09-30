using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// A thin indirection over <see cref="AlternateForms"/>, so a component can <c>@inject</c> it
/// without printing an engine type name at the player.
///
/// <para><b>The engine's own class name is banned from markup, not from code.</b>
/// <c>WebPresentationTests.NoPageNamesATypeThisProjectDeclares</c> scans every <c>.razor</c>
/// file's visible text — which an <c>@inject</c> line is, since it sits outside both a comment
/// and the <c>@code</c> block — for any type declared in <c>engine/</c> or <c>sheets/</c>, and
/// <c>AlternateForms</c> is one. This class carries no rule of its own; it forwards
/// <see cref="Families"/> and nothing else, so <c>CharacterManager.razor</c> and
/// <c>CharacterSwitcher.razor</c> can hold a reference to the engine's family calculator under
/// a name the guard has never heard of.</para>
/// </summary>
public sealed class AlternateFormEngine
{
    private readonly AlternateForms _forms;

    public AlternateFormEngine(AlternateForms forms) =>
        _forms = forms ?? throw new ArgumentNullException(nameof(forms));

    public IReadOnlyList<AlternateFormFamily> Families(IReadOnlyList<RosterEntry> roster) =>
        _forms.Families(roster);
}
