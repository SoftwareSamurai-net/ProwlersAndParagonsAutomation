namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The app boots.
///
/// <para><b>Control</b>: the document that was served was the app's own shell — its boot screen was
/// in it — and the framework then started: <c>window.Blazor</c> exists and a <c>_framework/</c>
/// payload really came down the wire with bytes in it. Without that second half a check on the
/// rendered page is satisfied by any page at all.</para>
///
/// <para><b>Outcome</b>: the boot screen is gone, a page is rendered inside <c>#app</c>, the banner
/// is there, and nothing threw on the way. Nothing in this repository ran the boot before
/// <c>scripts/e2e/drive.mjs</c>: a proof page is markup and CSS, and a boot-time failure was
/// invisible until the deploy.</para>
/// </summary>
public static class Boot
{
    public static Check Check => new("BOOT", Run);

    private static async Task<string> Run(Harness harness)
    {
        await harness.Goto("/");

        var served = harness.LastDocument;
        Harness.Control(served?.Status == 200,
            $"the server answered {served?.Status.ToString() ?? "nothing"} for /");
        Harness.Control(await harness.Eval<bool>("window.__ppE2E?.sawBootScreen === true"),
            "the served document carried no boot screen, so it was not this app");

        try
        {
            await harness.WaitFor("!!window.Blazor", "the framework to start");
        }
        catch
        {
            harness.AppEverRendered ??= false;
            throw;
        }

        var payload = await harness.Eval<int>(
            "performance.getEntriesByType('resource')"
            + ".filter(r => r.name.includes('/_framework/') && r.decodedBodySize > 0).length");
        Harness.Control(payload > 0,
            "no _framework payload was fetched, so WebAssembly never started");

        // Latched either way, so a twin whose app never mounts costs one timeout rather than ten.
        // See `Harness.AppEverRendered` — it can only ever shorten a run that is already failing.
        try
        {
            await harness.WaitFor($"!document.querySelector('.boot') && ({Harness.H1})",
                "the app to replace its boot screen");
            harness.AppEverRendered = true;
        }
        catch
        {
            harness.AppEverRendered = false;
            throw;
        }

        var heading = await harness.Eval<string?>(Harness.H1);
        Harness.Outcome(heading == "Prowlers & Paragons", $"the front door rendered \"{heading}\"");
        Harness.Outcome(await harness.Eval<bool>("!!document.querySelector('.banner')"),
            "the banner did not render");

        var title = await harness.Page.TitleAsync();
        Harness.Outcome(title.Contains("Prowlers", StringComparison.Ordinal),
            $"the document title was \"{title}\"");

        // The real Content-Security-Policy is in force here — `wrangler pages dev` applies
        // `_headers` — so a policy that refuses one of the app's own scripts shows up as a console
        // error. That is a whole class of fault a `file://` proof page cannot see, because it has
        // no policy at all.
        var refused = harness.ConsoleErrors
            .Where(m => m.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Harness.Outcome(refused.Count == 0, $"the CSP refused something: {string.Join(" | ", refused)}");
        Harness.Outcome(harness.Exceptions.Count == 0,
            $"the page threw: {string.Join(" | ", harness.Exceptions)}");

        return $"booted, {payload} framework payload(s), heading \"{heading}\"";
    }
}
