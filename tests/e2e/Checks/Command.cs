namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// Ctrl-K opens the command palette, and Escape gives focus back.
///
/// <para><b>Nothing had ever pressed this key in the running application.</b> `js/palette.js` is
/// the one thing about the palette a component cannot do — hear a key pressed somewhere else — and
/// everything that tested it tested a `file://` proof page instead: `proof-shortcut.html` and its
/// broken twin. A proof page has no Blazor behind it, so what was proved was that a listener
/// increments a counter, not that the chord reaches a component that renders a dialog.</para>
///
/// <para><b>Control</b>: the listener registered (`ppPaletteStats.listeners`), the palette is not
/// already on screen, and pressing the chord was *heard* — `ppPaletteStats.presses` moves. That
/// counter is in the shipped file for exactly this purpose, and it separates the two bug reports
/// this check can produce: a key that never reached the page, and a key that reached it and did
/// nothing.</para>
///
/// <para><b>Outcome</b>: a dialog is on screen, the caret is in its box — a palette that opens
/// without taking focus is unusable by the keyboard that opened it — and Escape closes it and puts
/// focus back where it came from rather than on the body. `palette.js` says why the fallback is not
/// `document.body.focus()`: focusing the body is indistinguishable from focus being lost.</para>
/// </summary>
public static class Command
{
    public static Check Check => new("COMMAND", Run);

    /// <summary>Whether the palette's dialog is in the document.</summary>
    private const string Open = "!!document.querySelector('.palette[role=\"dialog\"]')";

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/build");

        // The layout registers the listener through interop once the palette component is alive.
        // Waited for rather than asserted immediately: it happens after the first render, and a
        // check that read it a frame early would report a missing feature as a missing listener.
        try
        {
            await harness.WaitFor("(window.ppPaletteStats?.listeners ?? 0) >= 1",
                "js/palette.js to register its keydown listener", 15_000);
        }
        catch (CheckFailedException)
        {
            var stats = await harness.Eval<string?>(
                "window.ppPaletteStats ? JSON.stringify(window.ppPaletteStats) : null");

            throw new ControlFailedException(
                $"js/palette.js never began listening (ppPaletteStats: {stats ?? "absent"}). "
                + "Nothing below this could distinguish a chord that does not work from one "
                + "nobody is listening for.");
        }

        Harness.Control(!await harness.Eval<bool>(Open),
            "the palette was already open before the chord was pressed");

        var pressesBefore = await harness.Eval<int>("window.ppPaletteStats.presses");

        // Focus somewhere ordinary first, so the chord is pressed from where a reader would press
        // it — inside a form control, which is the case `palette.js` exists for: Blazor can only
        // handle a key on an element it rendered and that has focus, and this one is neither.
        var stepper = harness.Page.Locator("input, button").First;
        await stepper.ClickAsync();

        var cameFrom = await harness.Eval<string?>(
            "document.activeElement ? document.activeElement.tagName + '#' "
            + "+ (document.activeElement.id || '(no id)') : null");

        await harness.Page.Keyboard.PressAsync("Control+k");

        try
        {
            await harness.WaitFor($"window.ppPaletteStats.presses > {pressesBefore}",
                "js/palette.js to hear Ctrl-K", 10_000);
        }
        catch (CheckFailedException)
        {
            throw new ControlFailedException(
                "Ctrl-K never reached js/palette.js's listener. The chord is Ctrl on Windows and "
                + "Linux and Command on a Mac, and the listener takes either — so a press that is "
                + "not heard at all is the listener's condition, not the platform's.");
        }

        try
        {
            await harness.WaitFor(Open, "the palette to open", 10_000);
        }
        catch (CheckFailedException)
        {
            throw new CheckFailedException(
                "Ctrl-K was heard — the press counter moved — and no palette opened. The listener "
                + "calls back into the component through Blazor's interop object; this is that "
                + "call not arriving, or the component not rendering on it.");
        }

        Harness.Outcome(
            await harness.Eval<bool>(
                "document.activeElement?.classList.contains('palette-box') === true"),
            "the palette opened without the caret in its box, so the keyboard that opened it "
            + "cannot type into it");

        // Typing, because an empty palette that focuses correctly is still not a palette. The
        // service decides what matches; this only asks that something did.
        await harness.Page.Keyboard.TypeAsync("fini");

        await harness.WaitFor(
            "document.querySelectorAll('.palette-results [role=\"option\"]').length > 0",
            "the palette to offer something for what was typed", 10_000);

        var offered = await harness.Eval<int>(
            "document.querySelectorAll('.palette-results [role=\"option\"]').length");

        await harness.Page.Keyboard.PressAsync("Escape");

        await harness.WaitFor($"!({Open})", "the palette to close on Escape", 10_000);

        var wentBackTo = await harness.Eval<string?>(
            "document.activeElement ? document.activeElement.tagName + '#' "
            + "+ (document.activeElement.id || '(no id)') : null");

        Harness.Outcome(wentBackTo == cameFrom,
            $"Escape left focus on {wentBackTo} rather than returning it to {cameFrom}. A palette "
            + "that swallows focus is a keyboard trap, which is the one thing a keyboard "
            + "affordance must not be.");

        return $"Ctrl-K opened the palette, it offered {offered} match(es) for what was typed, and "
            + $"Escape returned focus to {wentBackTo}";
    }
}
