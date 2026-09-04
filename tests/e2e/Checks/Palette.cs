namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The four palettes are four palettes.
///
/// <para><b>Control</b>: each combination is actually reached — the two attributes read back what
/// was asked for — and the stylesheet is actually painting, so the banner has an opaque background
/// rather than the transparent one an unstyled element has. A pixel golden proves a page looks
/// right; nothing proved that clicking these two switches gets a reader to it.</para>
///
/// <para><b>Outcome</b>: the four readings are pairwise different. Hero/Villain is an identity and
/// light/dark is a reader's preference, and neither is derivable from the other — so two of these
/// collapsing into one is a real fault and not a cosmetic one.</para>
/// </summary>
public static class Palette
{
    public static Check Check => new("PALETTE", Run);

    private static readonly (string Mode, string Theme)[] Wanted =
    [
        ("Hero", "Light"),
        ("Hero", "Dark"),
        ("Villain", "Light"),
        ("Villain", "Dark"),
    ];

    /// <summary>
    /// The colours a palette actually resolves to, on real elements as well as in the tokens.
    ///
    /// <para><b><c>.banner</c>'s <c>backgroundImage</c> and not its <c>backgroundColor</c>.</b> The
    /// banner is a <c>linear-gradient</c> over <c>--primary</c>, so its background <em>colour</em>
    /// is transparent and reading that reports every palette as unpainted — which this harness duly
    /// did on its first run, as a control failure, which is what a control is for.</para>
    ///
    /// <para><b>Nothing identifying is in here, and that is the one thing this port must not lose.</b>
    /// This check's first version returned <c>data-mode</c> and <c>data-theme</c> alongside the
    /// colours and then asserted the four readings were pairwise different — which they are
    /// <em>by construction</em>, because the two attributes differ. It would have passed against a
    /// site with one palette actually painted in it. Read only what the palette decides.</para>
    /// </summary>
    private const string PaletteReading = """
        (() => {
            const root = getComputedStyle(document.documentElement);
            const banner = document.querySelector('.banner');
            const bannerStyle = banner ? getComputedStyle(banner) : null;
            return JSON.stringify({
                surface: root.getPropertyValue('--surface').trim(),
                ink: root.getPropertyValue('--ink').trim(),
                primary: root.getPropertyValue('--primary').trim(),
                bannerFill: bannerStyle ? bannerStyle.backgroundImage : null,
                bannerInk: bannerStyle ? bannerStyle.color : null,
                bodyBackground: getComputedStyle(document.body).backgroundColor,
                bodyInk: getComputedStyle(document.body).color,
            });
        })()
        """;

    private sealed record Reading(
        string Surface, string Ink, string Primary,
        string? BannerFill, string? BannerInk, string BodyBackground, string BodyInk);

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/");

        var readings = new List<(string Want, Reading Reading)>();

        foreach (var (mode, theme) in Wanted)
        {
            await SettingsMenu.ClickButton(harness, "mode-switch", mode);
            await SettingsMenu.ClickButton(harness, "theme-switch", theme);

            var expectedMode = mode.ToLowerInvariant();
            var expectedTheme = theme.ToLowerInvariant();

            try
            {
                await harness.WaitFor(
                    $"document.documentElement.getAttribute('data-mode') === '{expectedMode}'"
                    + $" && document.documentElement.getAttribute('data-theme') === '{expectedTheme}'",
                    $"the document to be stamped {expectedMode}/{expectedTheme}", 15_000);
            }
            catch
            {
                throw new ControlFailedException($"{mode}/{theme} never reached the document element");
            }

            var json = await harness.Eval<string>(PaletteReading);
            var reading = System.Text.Json.JsonSerializer.Deserialize<Reading>(json, JsonOptions)!;

            Harness.Control(reading.Surface != "" && reading.Ink != "",
                $"{mode}/{theme} resolved no palette tokens, so theme.css did not load");
            Harness.Control(!string.IsNullOrEmpty(reading.BannerFill) && reading.BannerFill != "none",
                $"{mode}/{theme} left the banner unpainted, so app.css did not apply");
            Harness.Control(
                !string.IsNullOrEmpty(reading.BodyBackground)
                && reading.BodyBackground != "rgba(0, 0, 0, 0)",
                $"{mode}/{theme} left the page ground unpainted");

            readings.Add(($"{mode}/{theme}", reading));
        }

        Harness.Control(readings.Count == 4, $"only {readings.Count} palettes were reached");

        for (var i = 0; i < readings.Count; i++)
        {
            for (var j = i + 1; j < readings.Count; j++)
            {
                var a = readings[i];
                var b = readings[j];

                Harness.Outcome(
                    System.Text.Json.JsonSerializer.Serialize(a.Reading)
                        != System.Text.Json.JsonSerializer.Serialize(b.Reading),
                    $"{a.Want} and {b.Want} are the same palette: "
                    + $"surface {a.Reading.Surface}, ink {a.Reading.Ink}, primary {a.Reading.Primary}");
            }
        }

        // Ground and fill, because either alone repeats: Hero light and Hero dark share `--primary`
        // and differ in `--surface`, and a summary quoting one of the two reads as a collapsed pair.
        return string.Join(", ", readings.Select(r => $"{r.Want} {r.Reading.Surface} on {r.Reading.Primary}"));
    }
}
