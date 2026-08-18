using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Every call into <c>wwwroot/js/motion.js</c>, with its failures swallowed.
///
/// <para><b>Motion is decoration; navigation is not.</b> Before this existed, the interop calls
/// were made directly from <c>MainLayout</c>'s <c>LocationChanging</c> handler and from
/// <c>ChosenList.OnAfterRenderAsync</c>, unguarded — so a <c>motion.js</c> that 404s or fails to
/// parse leaves <c>window.ppMotion</c> undefined and throws out of the handler that runs on
/// <em>every internal navigation</em>. A missing animation would have taken the app with it.</para>
///
/// <para>This is the same trade <c>CLAUDE.md</c> already settles for the recordings: missing
/// rules are a broken deployment and should fail loudly, missing decoration is a missing
/// demonstration and must not. <b>And it is a class rather than a <c>try</c> around each call
/// site for the same reason <c>ReplayLibrary.LoadAsync</c> is a method</b> — a block inside a
/// component is somewhere no test can reach, and the one that was a block in top-level
/// statements had its <c>try</c> deleted with the whole suite staying green.</para>
///
/// <para>It swallows only the failure of the <em>script</em>. Nothing here decides anything about
/// a character, and no caller reads a result back, so there is no answer to be wrong.</para>
/// </summary>
public sealed class Motion(IJSRuntime js)
{
    /// <summary>Whether a call has ever failed. Read by tests; nothing in the app branches on it.</summary>
    public bool ScriptIsMissing { get; private set; }

    /// <summary>Snapshot the page before a navigation replaces it.</summary>
    public ValueTask Begin() => Call("ppMotion.begin");

    /// <summary>Release the snapshot once the new page has rendered.</summary>
    public ValueTask End() => Call("ppMotion.end");

    /// <summary>Count a figure from one engine answer to another.</summary>
    public ValueTask Count(ElementReference figure, int from, int to) =>
        Call("ppCount", figure, from, to);

    /// <summary>Land any row in <paramref name="list"/> that has not been seen before.</summary>
    public ValueTask Land(ElementReference list, bool announce) =>
        Call("ppLand", list, announce);

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
