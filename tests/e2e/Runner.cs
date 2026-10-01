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
///
/// <para><b>The one exception is a server that has stopped answering, and it is a fourth line.</b>
/// CI run 34040527190 printed one render timeout and seven <c>net::ERR_CONNECTION_REFUSED</c>
/// verdicts, each reading as a finding about the check that printed it, after
/// <c>wrangler pages dev</c> died four seconds into the drive. A refused connection is measured
/// here rather than pattern-matched — see <see cref="ServerStoppedAnswering"/> — reported once as
/// <c>[HARNESS] the server stopped answering</c>, and the run then stops:
/// <c>E2E CHECK &lt;NAME&gt;: NOT RUN</c> for what is left, and <c>E2E RAN n CHECKS</c> counting
/// only the checks that ran. A check that did not run is in neither figure, which is what stops
/// the two of them describing a suite that did not happen.</para>
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
        var harness = new Harness(page, baseUrl, browser);

        Harness.Watch(page, harness);

        await page.AddInitScriptAsync(Harness.Recorder);

        var passed = 0;
        var ran = 0;
        var notRun = new List<string>();

        for (var i = 0; i < checks.Count; i++)
        {
            // Each check starts from a page it navigated to itself, and reads only what the
            // application put there. State left behind by an earlier check is deliberate — a
            // browser a person has used is not a fresh one — and no check depends on another
            // having run.
            var verdict = await Verdict(checks[i], harness, baseUrl);

            ran++;
            if (verdict == Kind.Passed) passed++;

            // **A second context does not survive the check that opened it, and that is not
            // tidiness.** A signed-in context left running would carry a session cookie into
            // whatever ran next, so a later check could pass or fail for a reason nothing in it
            // mentions — which is exactly the fault `A11Y` had when it scanned whichever palette
            // and page state the previous check happened to leave behind.
            await harness.CloseExtraContexts();

            harness.ConsoleErrors.Clear();
            harness.Exceptions.Clear();
            harness.ClearResponses();

            // **Stopping is the honest answer, and it is not only about the minutes.** Every
            // check below this one would navigate to a server that is not there, fail on its
            // first request, and print a verdict that reads as a finding about itself — which is
            // precisely what CI run 34040527190 printed seven times. `NOT RUN` is neither a pass
            // nor a fail, and the summary line counts only what ran.
            if (verdict != Kind.ServerGone) continue;

            notRun.AddRange(checks.Skip(i + 1).Select(c => c.Name));
            break;
        }

        await context.CloseAsync();

        foreach (var name in notRun)
        {
            Say($"E2E CHECK {name}: NOT RUN — the server stopped answering before this check");
        }

        Say($"E2E RAN {ran} CHECKS, {passed} PASSED");
        return passed == checks.Count ? 0 : 1;
    }

    /// <summary>How one check ended. <see cref="ServerGone"/> stops the run — see the loop.</summary>
    private enum Kind { Passed, Failed, ServerGone }

    /// <summary>
    /// How long <see cref="ServerStoppedAnswering"/> gives the server to answer at all. See that
    /// method's own doc comment for the argument behind the figure.
    /// </summary>
    private const int ProbeTimeoutMs = 10_000;

    /// <summary>
    /// One request, asked of the server rather than of an exception's wording: has it stopped
    /// answering at all?
    ///
    /// <para><b>The class this replaces is what CI run 34040527190 reported.</b>
    /// <c>wrangler pages dev</c> died four seconds into a nine-check drive. <c>A11Y</c>, which was
    /// running at the time, reported a 45-second render timeout as its own <c>[OUTCOME]</c>, and
    /// the seven checks after it each reported <c>net::ERR_CONNECTION_REFUSED</c> as its own
    /// finding. Eight verdicts about eight checks, not one of them about the single thing that had
    /// happened.</para>
    ///
    /// <para><b>A string match on the exception would be the third denylist this repository has
    /// been burnt by.</b> A dead server produces a dozen spellings — refused, reset, empty
    /// response, a socket that simply hangs — and the check that saw it <em>first</em> reported a
    /// render timeout containing none of them. So this asks the server: a refused connection is a
    /// fact about the world, not a guess about a message.</para>
    ///
    /// <para><b>An answer counts, whatever it says, and that narrowness is what protects the
    /// twins.</b> A 500, a redirect, a slow but arriving response — each is a server that is still
    /// there and a check that is entitled to its own verdict. A deliberately-broken twin's whole
    /// point is a check failing while the server is perfectly alive, and relabelling that as
    /// <c>[HARNESS]</c> would turn every negative control into a run <c>e2e.sh</c> refuses to
    /// count.</para>
    ///
    /// <para><b>No answer at all counts too, and leaving it out was the half of this that CI has
    /// not yet billed us for.</b> <c>docs/guide/testing.md</c> records the state in as many words:
    /// a <c>workerd</c> that has died under a live <c>wrangler pages dev</c> does not refuse
    /// connections — wrangler keeps the port and <b>answers by hanging for ever</b>, which is why
    /// readiness in <c>e2e.sh</c> is the served body rather than the status code. Against that
    /// server every check below spends its full <see cref="Harness.NavigationTimeoutMs"/> and
    /// reports it as its own <c>[OUTCOME]</c> — which is the reporting run 34040527190 was fixed
    /// for, reached by the other road. So a probe that does not come back within
    /// <see cref="ProbeTimeoutMs"/> is a server that has stopped answering, in the plain meaning
    /// of the sentence.</para>
    ///
    /// <para><b>The bound is what keeps that honest</b>: ten seconds for a <c>GET /</c> off a local
    /// static server that has already served this run, whose measured cost is milliseconds. And it
    /// is only ever asked after a check has already failed — a site that cannot serve its front
    /// page in ten seconds has no verdict worth reading anyway.</para>
    ///
    /// <para><b>The socket error is printed in its POSIX spelling</b>, matching what the retired
    /// hand-rolled driver got from Node and printed for the same failure — a fixed spelling for one
    /// fact is what let a reader compare a run from each without concluding they saw different
    /// things.</para>
    /// </summary>
    private static async Task<string?> ServerStoppedAnswering(string baseUrl)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(ProbeTimeoutMs),
            };

            using var response = await client.GetAsync($"{baseUrl}/");
            return null;
        }
        catch (HttpRequestException error)
            when (error.InnerException is System.Net.Sockets.SocketException socket
                  && socket.SocketErrorCode
                      is System.Net.Sockets.SocketError.ConnectionRefused
                      or System.Net.Sockets.SocketError.ConnectionReset)
        {
            var code = socket.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused
                ? "ECONNREFUSED"
                : "ECONNRESET";

            return $"{baseUrl}/ answered {code}";
        }
        catch (Exception error) when (error is OperationCanceledException or TimeoutException)
        {
            // `HttpClient.Timeout` cancels the request, and nothing else here cancels anything —
            // so this is the hanging server above and not somebody else's cancellation.
            return $"{baseUrl}/ did not answer within {ProbeTimeoutMs}ms";
        }
        catch (Exception)
        {
            // Anything else — a DNS answer, a protocol error — is not the state this is looking
            // for, and guessing would relabel an honest red as a harness fault.
            return null;
        }
    }

    /// <summary>
    /// Print one verdict. <b>The only place a verdict reaches stdout, and the whole line goes
    /// through <see cref="Redact"/> rather than the piece somebody remembered to wrap.</b>
    ///
    /// <para>Measured, not reasoned about: the first version redacted <c>error.Message</c> and
    /// interpolated the probe's answer beside it raw, so a driver pointed at a base URL carrying a
    /// token printed <c>&amp;t=&lt;redacted&gt;</c> in the half that had been wrapped and the token
    /// in full in the half that had not — on one line, in one verdict. That is the same shape as
    /// the CI leak the rule was written for: a rule obeyed at the call sites somebody thought of.
    /// One choke point is what makes it a rule.</para>
    ///
    /// <para>It costs nothing to redact a green verdict or a <c>NOT RUN</c> line, and "only the red
    /// half is cleaned" is a rule that holds exactly until something passes.</para>
    /// </summary>
    private static void Say(string line) => Console.WriteLine(Redact(line));

    /// <summary>
    /// A verdict message fit to print: no raw sign-in token in it, and one line.
    ///
    /// <para><b>The redaction is the rule <c>scripts/e2e/process.sh</c>'s <c>redacted_tail</c>
    /// already obeys, one layer out.</b> Stage two navigates to <c>/signin?t=&lt;raw token&gt;</c>,
    /// so Playwright's own "ERR_CONNECTION_REFUSED at &lt;url&gt;" names a bearer secret — and run
    /// 34040527190 printed three of them into a public CI log, because only the <em>shell</em>
    /// half of this harness had ever thought about it.</para>
    ///
    /// <para><b>One line, because <c>scripts/e2e.sh</c> reads these with <c>grep ^E2E CHECK</c>.</b>
    /// Playwright appends a multi-line "Call log:" to a navigation failure, and everything after
    /// the first newline landed outside the verdict the script matches on.</para>
    /// </summary>
    private static string Redact(string? message) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(
                message ?? string.Empty, @"([?&]t=)[A-Za-z0-9_-]+", "$1<redacted>"),
            @"\s*\n\s*", " ");

    /// <summary>
    /// How the browser is started.
    ///
    /// <para><b><c>Channel = "chrome"</c> is the whole reason this does not add a third renderer to
    /// the repository.</b> It launches the Google Chrome already on the machine — the same one
    /// <c>ubuntu-latest</c> ships — instead of the Chromium Playwright would otherwise download and
    /// version-manage itself. So there is no <c>playwright install</c> step, nothing to cache, and
    /// no second Chrome whose disagreement with the first would mean regenerating every pixel
    /// golden. <c>docs/guide/testing.md</c> records what that disagreement costs: the digest-pinned
    /// Docker Chrome and the runner's own Chrome differ on one proof page by 32,462 pixels.</para>
    ///
    /// <para><c>PP_E2E_CHROME</c> overrides the path, because <c>e2e.sh</c> has already looked and
    /// knows the answer on this machine.</para>
    /// </summary>
    private static BrowserTypeLaunchOptions Launch()
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                // Each of these is there for a runner rather than for a laptop: no GPU and no
                // sandbox because a container has neither, a real /dev/shm size because the
                // default one is 64 MB and Chrome crashes on it, and no background throttling
                // because a headless tab is never foregrounded and a throttled timer is a check
                // that waits out its deadline for no reason.
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
    private static async Task<Kind> Verdict(Check check, Harness harness, string baseUrl)
    {
        var started = DateTime.UtcNow;

        try
        {
            var detail = await check.Run(harness);
            Say($"E2E CHECK {check.Name}: PASS — {detail} ({Elapsed(started)}ms)");
            return Kind.Passed;
        }
        catch (Exception error)
        {
            // **One named kind for a server that has gone away, ahead of the three below.** It
            // outranks them because it is a different bug report: `CONTROL` and `OUTCOME` are
            // claims about this check, and neither is true of a check whose server evaporated
            // half way through. See `ServerStoppedAnswering` for why it is measured rather than
            // read off the exception, and `Drive` for why the run stops here.
            var gone = await ServerStoppedAnswering(baseUrl);

            if (gone is not null)
            {
                Say($"E2E CHECK {check.Name}: FAIL — [HARNESS] the server stopped answering "
                    + $"({gone}), so this verdict is about the server and not about "
                    + $"{check.Name}. What the check saw first: {error.Message} "
                    + $"({Elapsed(started)}ms)");
                return Kind.ServerGone;
            }

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

            Say($"E2E CHECK {check.Name}: FAIL — [{kind}] {error.Message} "
                + $"({Elapsed(started)}ms)");
            return Kind.Failed;
        }
    }

    private static long Elapsed(DateTime from) =>
        (long)(DateTime.UtcNow - from).TotalMilliseconds;
}
