using ProwlersAndParagons.E2e;
using ProwlersAndParagons.E2e.Checks;

// The end-to-end checks: real Chrome, against the published site as `wrangler pages dev` serves it.
//
//     dotnet run --project tests/e2e -- http://127.0.0.1:8788
//
// **The server is not this program's business, and that is a deliberate answer rather than an
// omission.** `scripts/e2e.sh` publishes the site, parses the wrangler version out of
// `.github/workflows/deploy.yml`, starts the server from `.e2e/` **specifically so no `functions/`
// directory is found**, builds a deliberately-broken twin per check, and drives each of them. All
// of that is hard-won — read its header and `docs/guide/testing.md` on the three server facts that
// each cost a debugging round — and none of it is easier in C#. So this program takes a URL, and
// `PP_E2E_DRIVER` is the seam that points that script at it:
//
//     PP_E2E_DRIVER="dotnet <path>/ProwlersAndParagons.E2e.dll" ./scripts/e2e.sh
//
// That seam already existed, for a different reason — a driver that never returns cannot be
// arranged with the real one, so the deadline could not otherwise be watched to fire — and it is
// what lets this land beside `scripts/e2e/drive.mjs` instead of replacing it. The old driver is
// green, twinned, and the only thing that runs today; `PROGRESS.md` item 10 names the condition
// under which it is retired, and this is not it yet.

// **The verdict line is parsed by a `sed` that contains an em-dash**, and on Windows the default
// console encoding turns that into a `-` — so `e2e.sh` would find the FAIL and then fail to strip
// the prefix off the reason it prints beside it. Cosmetic, and found by reading the first run's
// output against the script that consumes it rather than by anything going red.
Console.OutputEncoding = System.Text.Encoding.UTF8;

var baseUrl = (args.Length > 0 ? args[0] : "").TrimEnd('/');

if (string.IsNullOrEmpty(baseUrl))
{
    await Console.Error.WriteLineAsync(
        "usage: dotnet run --project tests/e2e -- <base-url>");
    return 2;
}

// The order is the cheapest failure first: if the app cannot boot, everything below it is a
// timeout apiece, and `Harness.AppEverRendered` latches on BOOT's verdict to keep it to one.
Check[] checks = [Boot.Check];

return await Runner.Drive(baseUrl, checks);
