using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The one call that puts the palette on the document element, with its failure swallowed.
///
/// <para><b>This used to be an unguarded call in the layout, and it was allowed to be.</b> The
/// guard over the app's interop keeps a short list of calls reached by a <em>user action</em>
/// rather than by rendering, and <c>ppSetMode</c> was on it: somebody clicking Hero or Villain
/// is a click, and a click that throws costs the click.</para>
///
/// <para><b>Moving the mode onto the character is what changed that.</b> The palette now follows
/// whichever character is being built — restored from storage, loaded as a sample, taken out of a
/// recording — so it is pushed from <c>OnAfterRenderAsync</c>, on the layout, which renders on
/// every page. Unguarded, a missing or broken script would throw out of every render of the
/// shell. The allow-list entry stopped being true the moment the call stopped being a click, and
/// the guard said so.</para>
///
/// <para>Same bargain as <see cref="Motion"/> and <see cref="Shortcuts"/>: the app in the wrong
/// colours is a worse-looking app, and an app that will not render is no app.</para>
/// </summary>
public sealed class Theme(IJSRuntime js)
{
    /// <summary>Whether a call has ever failed. Read by tests; nothing in the app branches on it.</summary>
    public bool ScriptIsMissing { get; private set; }

    /// <summary>Dress the document as a Hero or as a Villain.</summary>
    public async ValueTask Apply(SheetMode mode)
    {
        try
        {
            await js.InvokeVoidAsync("ppSetMode", mode == SheetMode.Hero ? "hero" : "villain");
        }
        catch (JSException)
        {
            // The script is absent, or threw. The app renders in the default palette.
            ScriptIsMissing = true;
        }
        catch (InvalidOperationException)
        {
            // No JS runtime available — prerendering, or a test host that supplies none.
            ScriptIsMissing = true;
        }
    }
}
