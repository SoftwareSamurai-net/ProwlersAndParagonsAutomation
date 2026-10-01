using System.Text.RegularExpressions;

namespace ProwlersAndParagons.E2e;

/// <summary>
/// The settings menu's two switches, shared by the checks that click them.
///
/// <para><b>Its own file, not <see cref="Harness"/>.</b> THEME and PALETTE both need these two
/// helpers and are being ported concurrently by separate slices; a shared edit to
/// <c>Harness.cs</c> would be a merge conflict for nothing. This is a candidate to fold into
/// <c>Harness.cs</c> once both land — see the integration pass named in PROGRESS.md item 10.</para>
/// </summary>
public static class SettingsMenu
{
    /// <summary>Open the settings menu if it is not already open. Its controls are not in the
    /// document until then.</summary>
    public static async Task Open(Harness harness)
    {
        var open = await harness.Eval<bool>(
            "document.querySelector('.settings-open')?.getAttribute('aria-expanded') === 'true'");

        if (!open)
        {
            await harness.Page.Locator(".settings-open").ClickAsync();
        }

        await harness.WaitFor("!!document.querySelector('.settings-menu-list')",
            "the settings menu to open", 10_000);
    }

    /// <summary>Click a button in one of the settings menu's groups, by the word on it.</summary>
    public static async Task ClickButton(Harness harness, string group, string label)
    {
        await Open(harness);

        // A real click through Playwright's own locator, not an evaluated `el.click()` — anchored
        // so it matches the button whose text is exactly `label`, the same exact match the retired
        // hand-rolled driver did by hand with `.find(b => b.textContent.trim() === label)`. A plain
        // `HasText` string is a substring match and "Dark" would also hit a hypothetical "Dark red"
        // button.
        var button = harness.Page.Locator($".settings-menu-list .{group} button")
            .Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") });

        await button.ClickAsync();
    }

    /// <summary>Whether a settings button reports itself pressed.</summary>
    public static string Pressed(string group, string label) =>
        $"[...document.querySelectorAll('.settings-menu-list .{group} button')]"
        + $".find(b => b.textContent.trim() === {Quote(label)})"
        + "?.getAttribute('aria-pressed') === 'true'";

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
