using Microsoft.JSInterop;

namespace ProwlersAndParagonsAutomation.Web.Services;

/// <summary>
/// Light, dark, or whatever the reader's system asks for.
///
/// <para><b>Three states and not two, and <see cref="System"/> is not a synonym for one of the
/// others.</b> Somebody who has chosen nothing follows their system, and follows it *as it
/// changes* — a machine that goes dark in the evening takes this app with it. Collapsing that
/// onto whichever value the system happened to hold at the moment of the first visit would
/// silently opt every reader out of their own setting.</para>
///
/// <para><b>It is not on <c>CharacterSheet</c>, and that is the same argument that put
/// <c>UnlimitedBudget</c> beside <c>IsVillain</c> rather than inside it.</b> Whether somebody
/// prefers a dark screen is a fact about a person and a browser; whether a character is a
/// Villain is a fact about the character, and travels with it through an export. A theme on the
/// sheet would arrive with somebody else's imported character and change the reader's screen.</para>
/// </summary>
public enum ThemeChoice
{
    /// <summary>Follow <c>prefers-color-scheme</c>. The default, and stored as no value at all.</summary>
    System,

    /// <summary>Light, whatever the system says.</summary>
    Light,

    /// <summary>Dark, whatever the system says.</summary>
    Dark,
}

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

    /// <summary>
    /// What the reader has chosen, or <see cref="ThemeChoice.System"/> until asked.
    ///
    /// <para><b>The script owns the stored value and this is a copy of its answer, deliberately.
    /// </b> The preference has to be read before the app boots — a 27 MiB payload means seconds
    /// of boot screen, and reading it here would show that screen in the wrong theme and then
    /// flip it — so <c>js/theme.js</c> reads local storage in the document head and stamps the
    /// attribute. Reading the same key a second time from C# would be two readers of one value,
    /// which is one more than can be kept in step.</para>
    /// </summary>
    public ThemeChoice Choice { get; private set; } = ThemeChoice.System;

    /// <summary>Ask the script what was chosen. Called once, when the shell first initialises.</summary>
    public async Task ReadChoice()
    {
        try
        {
            Choice = Parse(await js.InvokeAsync<string?>("ppTheme.current"));
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

    /// <summary>
    /// Record a choice and apply it.
    ///
    /// <para><see cref="Choice"/> is set whether or not the call lands, so the control shows
    /// what was clicked rather than reverting under the reader's finger. Same bargain as the
    /// palette above: the app in the wrong colours is a worse-looking app, and a control that
    /// silently refuses a click is a broken one.</para>
    /// </summary>
    public async ValueTask Choose(ThemeChoice choice)
    {
        Choice = choice;

        try
        {
            await js.InvokeVoidAsync("ppTheme.set", Name(choice));
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

    /// <summary>The wire name for a choice — the same three words the script and the stylesheet use.</summary>
    public static string Name(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => "light",
        ThemeChoice.Dark => "dark",
        _ => "system",
    };

    /// <summary>
    /// The choice a wire name means.
    ///
    /// <para>Anything unrecognised is <see cref="ThemeChoice.System"/>, which is also what a
    /// missing value means: a storage key edited by hand, or written by an older build, should
    /// leave the reader on their system's setting rather than on a guess.</para>
    /// </summary>
    private static ThemeChoice Parse(string? stored) => stored switch
    {
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => ThemeChoice.System,
    };
}
