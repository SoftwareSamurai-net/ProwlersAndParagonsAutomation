using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e;

/// <summary>One check: the name its verdict is printed under, and the work.</summary>
/// <param name="Name">Uppercase, and the same name <c>scripts/e2e/defects.mjs</c> twins it by.</param>
/// <param name="Run">Returns the detail quoted in a green verdict; throws to report a fault.</param>
public sealed record Check(string Name, Func<Harness, Task<string>> Run);

/// <summary>
/// Runs the checks and prints the verdict protocol <c>scripts/e2e.sh</c> reads.
///
/// <para><b>The protocol is a contract with the shell, not a log format.</b> <c>e2e.sh</c> greps
/// <c>^E2E CHECK &lt;NAME&gt;: FAIL</c> out of a run against a deliberately-broken twin of the
/// published site and requires exactly that check to be red there; it reads the check names out of
/// the real run to make sure every one of them <em>has</em> a twin; and it reads the summary line's
/// two figures rather than trusting an exit code, because a driver that died on its second check
/// still prints two verdicts and two greens out of five is not four reds — it is a run that did not
/// happen. Changing any of these three lines means changing that script.</para>
///
/// <para><b>A check that throws says FAIL rather than ending the run.</b> A twin must
/// <em>say</em> FAIL, never merely fail to say PASS: three of this repository's four historical
/// guard faults were a harness that never ran being read as a harness that passed, and
/// <c>e2e.sh</c> treats a missing verdict as a failure of the twin for exactly that reason.</para>
/// </summary>
public static class Runner
{
    /// <summary>
    /// Launch a browser, run every check against <paramref name="baseUrl"/>, and return the exit
    /// code: zero only if every check passed.
    /// </summary>
    public static async Task<int> Drive(string baseUrl, IReadOnlyList<Check> checks)
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(Launch());
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });

        var page = await context.NewPageAsync();
        var harness = new Harness(page, baseUrl);

        page.Console += (_, message) =>
        {
            if (message.Type == "error") harness.ConsoleErrors.Add(message.Text);
        };
        page.PageError += (_, error) => harness.Exceptions.Add(error);

        await page.AddInitScriptAsync(Harness.Recorder);

        var passed = 0;

        foreach (var check in checks)
        {
            // Each check starts from a page it navigated to itself, and reads only what the
            // application put there. State left behind by an earlier check is deliberate — a
            // browser a person has used is not a fresh one — and no check depends on another
            // having run.
            if (await Verdict(check, harness)) passed++;

            harness.ConsoleErrors.Clear();
            harness.Exceptions.Clear();
        }

        await context.CloseAsync();

        Console.WriteLine($"E2E RAN {checks.Count} CHECKS, {passed} PASSED");
        return passed == checks.Count ? 0 : 1;
    }

    /// <summary>
    /// How the browser is started.
    ///
    /// <para><b><c>Channel = "chrome"</c> is the whole reason this does not add a third renderer to
    /// the repository.</b> It launches the Google Chrome already on the machine — the same browser
    /// <c>scripts/e2e/cdp.mjs</c> drives and the same one <c>ubuntu-latest</c> ships — instead of
    /// the Chromium Playwright would otherwise download and version-manage itself. So there is no
    /// <c>playwright install</c> step, nothing to cache, and no second Chrome whose disagreement
    /// with the first would mean regenerating every pixel golden. <c>docs/guide/testing.md</c>
    /// records what that disagreement costs: the digest-pinned Docker Chrome and the runner's own
    /// Chrome differ on one proof page by 32,462 pixels.</para>
    ///
    /// <para><c>PP_E2E_CHROME</c> overrides the path, the same seam <c>cdp.mjs</c> has and for the
    /// same reason: <c>e2e.sh</c> has already looked and knows the answer on this machine.</para>
    /// </summary>
    private static BrowserTypeLaunchOptions Launch()
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                // The same flags cdp.mjs passes, and each is there for a runner rather than for a
                // laptop: no GPU and no sandbox because a container has neither, a real /dev/shm
                // size because the default one is 64 MB and Chrome crashes on it, and no
                // background throttling because a headless tab is never foregrounded and a
                // throttled timer is a check that waits out its deadline for no reason.
                "--disable-gpu",
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-background-timer-throttling",
                "--disable-renderer-backgrounding",
                "--hide-scrollbars",
            ],
        };

        var chrome = Environment.GetEnvironmentVariable("PP_E2E_CHROME");

        if (!string.IsNullOrWhiteSpace(chrome)) options.ExecutablePath = chrome;
        else options.Channel = "chrome";

        return options;
    }

    /// <summary>Run one check and print its verdict, whichever way it went.</summary>
    private static async Task<bool> Verdict(Check check, Harness harness)
    {
        var started = DateTime.UtcNow;

        try
        {
            var detail = await check.Run(harness);
            Console.WriteLine($"E2E CHECK {check.Name}: PASS — {detail} ({Elapsed(started)}ms)");
            return true;
        }
        catch (Exception error)
        {
            // **Three kinds, not two, and the third is not padding.** CONTROL is "the work did not
            // happen" and OUTCOME is "it happened and was wrong" — different bug reports, which is
            // the rule CLAUDE.md states. HARNESS is neither: it is this file having a bug, and a
            // twin whose only red verdict is a HARNESS one has not been watched to fail for the
            // reason it claims. It still says FAIL, because a verdict that is not printed is the
            // failure shape this whole protocol exists to avoid.
            var kind = error switch
            {
                ControlFailedException => "CONTROL",
                CheckFailedException => "OUTCOME",
                _ => "HARNESS",
            };

            Console.WriteLine(
                $"E2E CHECK {check.Name}: FAIL — [{kind}] {error.Message} ({Elapsed(started)}ms)");
            return false;
        }
    }

    private static long Elapsed(DateTime from) =>
        (long)(DateTime.UtcNow - from).TotalMilliseconds;
}
