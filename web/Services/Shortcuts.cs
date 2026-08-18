using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Every call into <c>wwwroot/js/palette.js</c>, with its failures swallowed.
///
/// <para><b>The same bargain <see cref="Motion"/> makes, for the same reason.</b> All three of
/// these calls are reached from a render — registering the key listener on first render, moving
/// focus in when the palette opens, putting it back when it closes — so a <c>palette.js</c> that
/// 404s or fails to parse would throw out of <c>OnAfterRenderAsync</c> on <em>every</em> render
/// of the layout, which is every page. A missing keyboard shortcut would take the app with it,
/// and the shortcut is a convenience: the six steps are all still one click away in the band at
/// the top, and every Power is still in the list on its own step.</para>
///
/// <para><b>It is a class rather than a <c>try</c> at each call site</b> for the reason
/// <c>CLAUDE.md</c> records twice over: a block inside a component is somewhere no test can
/// reach, and the one that lived in top-level statements had its <c>try</c> deleted with the
/// whole suite staying green.</para>
///
/// <para>It swallows only the failure of the <em>script</em>. Nothing here decides anything
/// about a character, and no caller reads a result back, so there is no answer to be wrong.</para>
/// </summary>
public sealed class Shortcuts(IJSRuntime js)
{
    /// <summary>Whether a call has ever failed. Read by tests; nothing in the app branches on it.</summary>
    public bool ScriptIsMissing { get; private set; }

    /// <summary>
    /// Start listening for the opening chord. <paramref name="owner"/> is the component the
    /// listener calls back into.
    /// </summary>
    public ValueTask Listen(object owner) => Call("ppPalette.listen", owner);

    /// <summary>Remember where focus was, and move it into the palette's box.</summary>
    public ValueTask Enter(ElementReference box) => Call("ppPalette.enter", box);

    /// <summary>Put focus back where it came from. Does nothing if there is nothing to go back to.</summary>
    public ValueTask Leave() => Call("ppPalette.leave");

    private async ValueTask Call(string name, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(name, args);
        }
        catch (JSException)
        {
            // The script is absent, or threw. Either way the page still works.
            ScriptIsMissing = true;
        }
        catch (InvalidOperationException)
        {
            // No JS runtime available — prerendering, or a test host that supplies none.
            ScriptIsMissing = true;
        }
    }
}
