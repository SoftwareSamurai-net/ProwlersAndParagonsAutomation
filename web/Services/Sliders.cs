using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The one call into <c>wwwroot/js/slider.js</c>, with its failure swallowed.
///
/// <para><b>Home and End on a rank's <c>role="slider"</c> also scroll the document</b>, and
/// nothing in <c>RankRow</c> can stop that: Blazor fixes an element's <c>preventDefault</c> at
/// render time rather than per key, so suppressing it on the whole element would swallow Tab
/// along with Home and End and trap focus inside a rank row — a worse defect than the one being
/// fixed. A native listener that answers to exactly two keys is the only way to take those two
/// and leave everything else, Tab included, alone.</para>
///
/// <para>Same bargain as <see cref="Motion"/>, <see cref="Shortcuts"/> and <see cref="Theme"/>:
/// reached from <c>OnAfterRenderAsync</c> on every Ability and Talent row on the sheet, so an
/// unguarded call would throw out of the rendering of every rank the moment the script 404s.</para>
/// </summary>
public sealed class Sliders(IJSRuntime js)
{
    /// <summary>Whether a call has ever failed. Read by tests; nothing in the app branches on it.</summary>
    public bool ScriptIsMissing { get; private set; }

    /// <summary>
    /// Stop Home and End on this slider reaching the document underneath it.
    ///
    /// <para>Idempotent on the script's own side, so calling it on every first render of every
    /// row never stacks a second listener on an element already guarded.</para>
    /// </summary>
    public async ValueTask Guard(ElementReference pips)
    {
        try
        {
            await js.InvokeVoidAsync("ppSlider.guard", pips);
        }
        catch (JSException)
        {
            // The script is absent, or threw. Home and End keep scrolling the page, which is
            // the defect this exists to fix — not a defect this class introduces.
            ScriptIsMissing = true;
        }
        catch (InvalidOperationException)
        {
            // No JS runtime available — prerendering, or a test host that supplies none.
            ScriptIsMissing = true;
        }
    }
}
