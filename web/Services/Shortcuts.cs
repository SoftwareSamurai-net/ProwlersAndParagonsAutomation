using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Every call into <c>wwwroot/js/palette.js</c>, with its failures swallowed.
///
/// <para><b>The same bargain <see cref="Motion"/> makes, for the same reason.</b> All four of
/// these calls are reached from a render — registering the key listener on first render, moving
/// focus in when the palette opens, putting it back when it closes, and asking once which key
/// the banner should print — so a <c>palette.js</c> that
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
/// about a character. <see cref="ReadModifier"/> is the one call that reads an answer back, and
/// a swallowed failure there answers <see langword="null"/> rather than a default — see the
/// remarks on <see cref="Modifier"/> for why the difference is the whole of its correctness.
/// </para>
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

    /// <summary>
    /// The word for the palette's modifier key on this reader's machine — <c>Ctrl</c> or
    /// <c>Cmd</c> — or <see langword="null"/> until <see cref="ReadModifier"/> has been asked
    /// and answered.
    ///
    /// <para><b>Null is a state the banner has to draw, not a value to substitute a default
    /// for.</b> The only way this stays null is a <c>palette.js</c> that is absent or threw —
    /// which is precisely the deployment where the key does nothing at all, so printing
    /// <c>Ctrl</c> anyway would teach a shortcut that is not listening. The button itself still
    /// works, because opening the palette is a click Blazor handles.</para>
    /// </summary>
    public string? Modifier { get; private set; }

    /// <summary>
    /// Ask the script which key the listener is waiting for, and remember the answer.
    ///
    /// <para><b>The word is decided here rather than in the script</b>, which answers a bare
    /// boolean. Anything a reader sees is this project's copy and belongs where the tests that
    /// read copy can see it; the script owns only the fact about the machine.</para>
    ///
    /// <para><b>And the word is <c>Cmd</c> rather than the looped-square glyph.</b> That glyph is
    /// the Mac convention and it is in neither of the two typefaces this app names, so it would
    /// fall back to a system face — silently, on one platform, which is exactly the failure the
    /// "no component names a typeface" rule exists to prevent.</para>
    /// </summary>
    public async ValueTask<string?> ReadModifier()
    {
        var mac = await Ask("ppPalette.onAMac");

        Modifier = mac switch
        {
            true  => "Cmd",
            false => "Ctrl",
            null  => null,
        };

        return Modifier;
    }

    /// <summary>
    /// The reading half of <see cref="Call"/>: the same swallow, with an answer.
    ///
    /// <para>A failure answers null rather than a default, because a default here is a claim
    /// about the reader's keyboard made by a script that did not run.</para>
    /// </summary>
    private async ValueTask<bool?> Ask(string name)
    {
        try
        {
            return await js.InvokeAsync<bool?>(name);
        }
        catch (JSException)
        {
            ScriptIsMissing = true;
            return null;
        }
        catch (InvalidOperationException)
        {
            ScriptIsMissing = true;
            return null;
        }
    }

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
