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
        "usage: dotnet run --project tests/e2e -- <base-url> [--only CHECK[,CHECK...]]");
    return 2;
}

// **`--only` exists for one caller and one reason: a twin needs one check, not six.**
//
// `scripts/e2e.sh` drives a deliberately-broken twin per check and reads exactly one verdict out
// of each run — the check that twin must turn red. The other five verdicts are not read by
// anything, and with `A11Y` scanning four palettes across four addresses they were 45 seconds
// apiece, six times over: four and a half minutes of a CI job spent re-measuring the
// accessibility of a site broken on purpose in a way that has nothing to do with accessibility.
//
// **What this does not do is let a run quietly get smaller.** `e2e.sh` drives the *real* site with
// no filter, reads the check names out of that run, and requires every one of them to have a twin.
// A name that does not match anything here leaves the twin's verdict line absent, which `e2e.sh`
// already treats as a failed negative control rather than as a pass — so a typo is red, not quiet.
var only = new HashSet<string>(StringComparer.Ordinal);

for (var i = 1; i < args.Length; i++)
{
    if (args[i] != "--only") continue;

    if (i + 1 >= args.Length)
    {
        await Console.Error.WriteLineAsync("--only needs a comma-separated list of check names");
        return 2;
    }

    foreach (var name in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries))
        only.Add(name.Trim());
}

// The order is the cheapest failure first: if the app cannot boot, everything below it is a
// timeout apiece, and `Harness.AppEverRendered` latches on BOOT's verdict to keep it to one.
//
// **The five names below `Boot` are the ones `scripts/e2e/drive.mjs` drives today**, and they are
// listed here before they are ported so that the set this driver reports matches the set
// `scripts/e2e/defects.mjs` twins — `e2e.sh` fails if those two disagree, in either direction, and
// it is right to. An unported check is red and says why; see `NotYetPorted`. One line each, so two
// slices porting two different checks do not conflict over the shape of this list.
//
// **A11Y is second, and its position is part of what it measures.** Every other check here is
// indifferent to what ran before it — "state left behind by an earlier check is deliberate, a
// browser a person has used is not a fresh one". A11Y is not: `BUILD` leaves a character in local
// storage, which enables the wizard's Next control, which changes which elements exist on
// `/build`. Run after BUILD it scans a page with an enabled Next; run alone under `--only A11Y`,
// or second, it scans one with a disabled Next. Those are different pages and they give different
// answers, and a check whose subject depends on execution order is not reproducible. Second is
// the position that agrees with `--only`, which is how every twin drives it.
Check[] checks =
[
    Boot.Check,
    Accessibility.Check,
    Build.Check,
    Theme.Check,
    Palette.Check,
    Routes.Check,
];

if (only.Count > 0)
{
    var unknown = only.Except(checks.Select(c => c.Name), StringComparer.Ordinal).ToList();

    if (unknown.Count > 0)
    {
        await Console.Error.WriteLineAsync(
            $"--only names checks this driver does not have: {string.Join(", ", unknown)}. "
            + $"Known: {string.Join(", ", checks.Select(c => c.Name))}");
        return 2;
    }

    checks = [.. checks.Where(c => only.Contains(c.Name))];
}

return await Runner.Drive(baseUrl, checks);
