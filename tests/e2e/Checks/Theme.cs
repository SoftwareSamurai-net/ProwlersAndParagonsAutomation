namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// A chosen light/dark theme survives a reload.
///
/// <para><b>Control</b>: <c>window.ppThemeStats.stamps</c> moved when the button was pressed. That
/// counter is in <c>js/theme.js</c> for exactly this purpose, and until now nothing read it across a
/// real reload — <c>proof-theme.html</c> re-executes the module in one document, which is a
/// different claim.</para>
///
/// <para><b>Outcome</b>: after a genuine reload the attribute is stamped before the app boots, and
/// the choice is in storage.</para>
/// </summary>
public static class Theme
{
    /// <summary>The local-storage key the application writes. Read here, never written.</summary>
    private const string ThemeKey = "pp.theme.v1";

    public static Check Check => new("THEME", Run);

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/");

        var stampsBefore = await harness.Eval<int>("window.ppThemeStats?.stamps ?? -1");
        Harness.Control(stampsBefore >= 1, "js/theme.js never ran, so it stamped nothing to begin with");

        await SettingsMenu.ClickButton(harness, "theme-switch", "Dark");

        // A timeout here is reported as a control failure and not a plain outcome failure: it
        // means pressing the button never reached the script at all, which is a different bug
        // report from "it ran and stamped the wrong thing".
        try
        {
            await harness.WaitFor($"(window.ppThemeStats?.stamps ?? -1) > {stampsBefore}",
                "the theme script to record a stamp", 10_000);
        }
        catch (CheckFailedException)
        {
            throw new ControlFailedException("pressing Dark never reached js/theme.js");
        }

        Harness.Control(
            await harness.Eval<bool>("document.documentElement.getAttribute('data-theme') === 'dark'"),
            "pressing Dark did not stamp the document");
        Harness.Control(await harness.Eval<bool>(SettingsMenu.Pressed("theme-switch", "Dark")),
            "the Dark button did not report itself pressed");

        await harness.Reload();

        // Read *before* waiting for the app: the whole point of js/theme.js is that the attribute
        // is there in the head, seconds before WebAssembly lands. Reading it after boot would pass
        // just as happily on a build that only stamped it from C#.
        var stampedEarly = await harness.Eval<string?>(
            "document.documentElement.getAttribute('data-theme')");
        Harness.Outcome(stampedEarly == "dark",
            $"after a reload the document was stamped \"{stampedEarly}\" before boot");

        await harness.WaitForApp();

        var remembered = await harness.Eval<string?>($"localStorage.getItem('{ThemeKey}')");
        Harness.Outcome(remembered == "dark", $"the stored theme was \"{remembered}\"");
        Harness.Outcome(
            await harness.Eval<bool>("document.documentElement.getAttribute('data-theme') === 'dark'"),
            "the app un-stamped the theme once it booted");

        // The menu has to be reopened: its buttons are not in the document while it is closed, and
        // asserting `aria-pressed` on an element that is not there reads as "not pressed" — which
        // is how this assertion failed on its first run against a perfectly good app.
        await SettingsMenu.Open(harness);
        Harness.Outcome(await harness.Eval<bool>(SettingsMenu.Pressed("theme-switch", "Dark")),
            "after a reload the Dark button no longer reported itself pressed");

        return "dark chosen, stamped before boot, still dark after a reload";
    }
}
