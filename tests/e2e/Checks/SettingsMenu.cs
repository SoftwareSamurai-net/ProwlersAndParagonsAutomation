namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// Helpers for driving the settings menu — its Hero/Villain switch and its Light/Dark switch.
///
/// <para><b>Shared by THEME and PALETTE, and kept out of <see cref="Harness"/> on purpose.</b> Both
/// checks click one of the menu's groups and read back whether a button reports itself pressed, but
/// the two are being ported by different slices at the same time — putting this in <c>Harness.cs</c>
/// would make both slices edit a file neither of them owns alone, for no reason but coincidence of
/// timing. This file is the integration pass's to fold in once both land; ported from the
/// <c>openSettings</c> / <c>clickSettingsButton</c> / <c>settingsPressed</c> helpers in
/// <c>scripts/e2e/drive.mjs</c>.</para>
/// </summary>
public static class SettingsMenu
{
    /// <summary>
    /// Open the settings menu if it is not already open. Its controls are not in the document
    /// until then — reading <c>aria-pressed</c> on a button that has not been opened into the
    /// document reads as "not pressed", which is indistinguishable from a real failure unless this
    /// is called first.
    /// </summary>
    public static async Task Open(Harness harness)
    {
        var open = await harness.Eval<bool>(
            "document.querySelector('.settings-open')?.getAttribute('aria-expanded') === 'true'");

        // A real click through the browser, never an evaluated `el.click()` — see Harness's own
        // doc comment on why that distinction is load-bearing here.
        if (!open) await harness.Page.Locator(".settings-open").ClickAsync();

        await harness.WaitFor("!!document.querySelector('.settings-menu-list')",
            "the settings menu to open", 10_000);
    }

    /// <summary>
    /// Click a button in one of the settings menu's groups, by the word on it.
    ///
    /// <para><c>:text-is()</c> is Playwright's own exact-text selector — the same comparison
    /// <c>drive.mjs</c> makes with <c>b.textContent.trim() === label</c> — so a group holding both
    /// "Dark" and, say, a future "Darker" button cannot match the wrong one on a substring.</para>
    /// </summary>
    public static async Task ClickButton(Harness harness, string group, string label)
    {
        await Open(harness);

        await harness.Page
            .Locator($".settings-menu-list .{group} button:text-is({Quote(label)})")
            .ClickAsync();
    }

    /// <summary>
    /// A JS expression: whether the named button in the named group reports itself pressed.
    ///
    /// <para>Returned as an expression string, for a caller to hand to
    /// <see cref="Harness.Eval{T}"/> or <see cref="Harness.WaitFor"/> — the same shape
    /// <c>settingsPressed</c> has in <c>drive.mjs</c>, so a check can wait on it as well as read
    /// it once.</para>
    /// </summary>
    public static string Pressed(string group, string label) =>
        $"[...document.querySelectorAll('.settings-menu-list .{group} button')]"
        + $".find(b => b.textContent.trim() === {Quote(label)})"
        + "?.getAttribute('aria-pressed') === 'true'";

    /// <summary>
    /// Quote a label as a JSON string literal — safe to splice into both a Playwright text
    /// selector and a JS expression, which is all this harness ever asks a label to do.
    /// </summary>
    private static string Quote(string label) => System.Text.Json.JsonSerializer.Serialize(label);
}
