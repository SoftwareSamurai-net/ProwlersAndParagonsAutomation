using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// The name every printed page of a sheet carries in its top margin.
///
/// <para><b>A page in the middle of a printed sheet used to be anonymous.</b> The name is on page
/// one and in a colophon on the last, and every page between them relied on the browser's own
/// print header — which the person printing can switch off, and is told to, because it is also
/// where the web address comes from. The answer is a <c>@page</c> margin box in app.css whose
/// content reads <c>var(--sheet-name)</c>, and that property has to sit on the document element,
/// because a page context inherits from the root and from nothing else. So the sheet pushes its
/// name here, and <c>ppSetSheetName</c> in download.js writes it.</para>
///
/// <para><b>Guarded like <see cref="Theme"/>, and for the same reason.</b> It is reached from
/// <c>SheetView</c>'s <c>OnAfterRenderAsync</c> — a render, not a click — so an unguarded call
/// would throw out of every render of every page that draws a sheet if the script were missing.
/// A sheet with an anonymous page two is a worse sheet; a page that will not render is no page.</para>
///
/// <para><b>Counted, because two sheets can be on one page.</b> The approval page draws the
/// submitted and the approved copies of one character side by side, and clearing the name when
/// <em>a</em> sheet leaves the page would strip the head from the one still on it. So a sheet
/// takes a hold when it arrives and releases it when it goes, and the property is removed only
/// when the last hold is released — leaving Chrome's own header to stand in on a page with no
/// sheet, rather than the previous character's name over every page of the roster.</para>
/// </summary>
public sealed class RunningHead(IJSRuntime js)
{
    private int _holds;

    /// <summary>Whether a call has ever failed. Read by tests; nothing in the app branches on it.</summary>
    public bool ScriptIsMissing { get; private set; }

    /// <summary>The name last pushed to the document, or null once the last sheet has gone.</summary>
    public string? Current { get; private set; }

    /// <summary>How many sheets are on the page. Read by tests.</summary>
    public int Holds => _holds;

    /// <summary>A sheet has arrived on the page.</summary>
    public void Take() => _holds++;

    /// <summary>Put a name on the document. Called by a sheet after it renders, when the name changed.</summary>
    public async ValueTask Show(string name)
    {
        Current = name;

        try
        {
            await js.InvokeVoidAsync("ppSetSheetName", name);
        }
        catch (JSException)
        {
            // The script is absent, or threw. The browser's own header stands in.
            ScriptIsMissing = true;
        }
        catch (InvalidOperationException)
        {
            // No JS runtime available — prerendering, or a test host that supplies none.
            ScriptIsMissing = true;
        }
    }

    /// <summary>
    /// A sheet has left the page. The last one out takes the name off the document.
    ///
    /// <para>Fire-and-forget, because a component's <c>Dispose</c> is synchronous and there is
    /// nothing to wait for: the outcome of a clear is a document attribute, and a failure is
    /// recorded on <see cref="ScriptIsMissing"/> the same as any other.</para>
    /// </summary>
    public void Release()
    {
        if (_holds > 0) _holds--;
        if (_holds > 0) return;

        Current = null;
        _ = Clear();
    }

    private async Task Clear()
    {
        try
        {
            await js.InvokeVoidAsync("ppSetSheetName", (object?)null);
        }
        catch (JSException)
        {
            ScriptIsMissing = true;
        }
        catch (InvalidOperationException)
        {
            ScriptIsMissing = true;
        }
    }
}
