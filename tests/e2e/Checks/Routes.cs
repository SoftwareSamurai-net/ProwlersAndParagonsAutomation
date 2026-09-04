using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// The routes.
///
/// <para><b>Control, part one</b>: a token planted on <c>window</c> survives the click, which is
/// what proves the router handled the navigation rather than the browser reloading the document.
/// Without it, "the address bar changed and the right page is on screen" is satisfied by a full
/// page load, which is not what a <c>NavLink</c> is for.</para>
///
/// <para><b>Control, part two</b>: each deep link was answered 200 <em>at the address that was
/// asked for</em>. <c>web/wwwroot/_redirects</c> rewrites every path to <c>index.html</c> with a
/// 200 rather than a redirect precisely so a shared link keeps its path; a 302 would drop it and
/// land every reader on step one, and reading only the rendered heading would never see that.</para>
///
/// <para><b>Outcome</b>: the right page renders at each address, including one that is routed
/// nowhere.</para>
/// </summary>
public static class Routes
{
    public static Check Check => new("ROUTES", Run);

    /// <summary>
    /// The nine addresses the app is expected to serve, and the heading each one renders.
    ///
    /// <para>Deliberately walked with plain navigation (<see cref="Harness.Goto"/>), not a click —
    /// these stand in for a shared link arriving cold, which is exactly the case the router's own
    /// navigation (checked first, below) does not exercise. Clicking every one of these would prove
    /// nothing that the first click has not already proved, and would not catch a deep link the
    /// server itself answers wrong.</para>
    /// </summary>
    private static readonly (string Path, string Expected)[] Addresses =
    [
        ("/", "Prowlers & Paragons"),
        ("/build", "Choose a tier"),
        ("/build/gear", "Gear"),
        ("/build/review", "GM review"),
        ("/build/characters", "Your characters"),
        ("/rules", "Rules reference"),
        ("/campaign", "Campaigns"),
        ("/signin", "Your account"),
        ("/no-such-address", "No such page"),
    ];

    private static async Task<string> Run(Harness harness)
    {
        await harness.Open("/");

        var token = await harness.Eval<string?>("window.__ppE2E.document");
        Harness.Control(!string.IsNullOrEmpty(token), "no per-document token was planted");

        // A real click through the browser — `ILocator.ClickAsync` dispatches an actual mouse
        // event at the element's own coordinates — never an evaluated `el.click()` and never a
        // `Goto` standing in for one. A `NavLink` exists to be clicked; driving it any other way
        // would prove nothing about the router.
        await harness.Page
            .Locator(".avenue-nav .banner-link")
            .GetByText("Build", new LocatorGetByTextOptions { Exact = true })
            .ClickAsync();

        await harness.WaitFor("location.pathname.startsWith('/build')",
            "the router to follow the Build link");

        Harness.Control(await harness.Eval<string?>("window.__ppE2E.document") == token,
            "the document reloaded, so that navigation was not client-side routing");

        await harness.WaitFor($"({Harness.H1}) === 'Choose a tier'", "the tier page to render");

        foreach (var (path, expected) in Addresses)
        {
            await harness.Goto(path);

            var served = harness.LastDocument;
            Harness.Control(served?.Status == 200,
                $"{path} was answered {served?.Status.ToString() ?? "nothing"}");

            var servedPath = new Uri(served!.Url).AbsolutePath;
            Harness.Control(servedPath == path,
                $"{path} was served as {servedPath}, so the path was dropped");

            // Named, for the reason `Harness.Open` gives: nine addresses share one timeout message
            // otherwise, and the address that failed would be lost in it.
            try
            {
                await harness.WaitForApp();
            }
            catch (Exception error)
            {
                throw new CheckFailedException($"{path}: {error.Message}", error);
            }

            var heading = await harness.Eval<string?>(Harness.H1);
            Harness.Outcome(heading == expected,
                $"{path} rendered \"{heading}\" rather than \"{expected}\"");
        }

        return $"client-side routing plus {Addresses.Length} addresses served at their own paths";
    }
}
