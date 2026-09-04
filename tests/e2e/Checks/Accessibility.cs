using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace ProwlersAndParagons.E2e.Checks;

/// <summary>
/// axe-core against the assembled application.
///
/// <para><b>The half of this harness that is new rather than a port.</b> Everything else here has
/// a predecessor in <c>scripts/e2e/drive.mjs</c>; this does not, and it is the reason the migration
/// was worth doing at all. <c>css/theme.css</c> writes contrast ratios into its comments as
/// <em>claims</em> — "7.5:1", "9.5:1", "1.8:1! NEVER text" — beside its own note saying "re-measure
/// if you change it; do not eyeball". Two of its tokens are <c>color-mix()</c>, which only a
/// browser resolves. Nothing in this repository measured any of that in the assembled app: a bUnit
/// test reads markup, and the pixel goldens compare a picture against a picture.</para>
///
/// <para><b>What it found.</b> Across four palettes and four addresses, with axe's full default
/// ruleset and no rule turned off: 536 passing rule instances and one violation — the wizard's
/// disabled Next control, which WCAG 1.4.3 exempts and axe cannot tell is inactive. See
/// <see cref="ExemptRule"/> for the figures and for why it is dropped by node rather than by rule.
/// Everything <c>theme.css</c> claims about its own contrast holds; it had simply never been
/// measured in the running app.</para>
///
/// <para><b>Two earlier readings were artefacts and a third was real and nearly deleted as a
/// fourth.</b> The artefacts were pages measured mid-animation — see <see cref="Scan"/>. The real
/// one was missed because this check used to run <em>last</em>, after BUILD had saved a character,
/// which enables that Next control and takes the element off the page entirely. Hence its position
/// in <c>Program.cs</c>, which is documented there and is not arbitrary.</para>
///
/// </summary>
public static class Accessibility
{
    public static Check Check => new("A11Y", Run);

    /// <summary>
    /// The addresses scanned.
    ///
    /// <para>One per distinct <em>page shape</em> rather than one per route: the front door, a step
    /// of the wizard with its cards and its budget strip, a long data page, and the account page
    /// with its form. Nine addresses would cost nine app boots for four kinds of answer.</para>
    /// </summary>
    private static readonly string[] Addresses = ["/", "/build", "/rules", "/signin"];

    /// <summary>
    /// All four, and the fourfold cost is bought by exactly one rule.
    ///
    /// <para>Everything else axe checks here — structure, names, roles, ARIA validity — is drawn
    /// from the DOM, which switching <c>data-mode</c> or <c>data-theme</c> does not touch: no
    /// attribute and no heading changes, only which tokens paint. <c>color-contrast</c> is the
    /// exception, and it is the whole reason this file exists: <c>theme.css</c> writes its ratios
    /// into its comments as claims, two of its tokens are <c>color-mix()</c> which only a browser
    /// resolves, and <c>EveryScreenPairInUseHoldsItsContrastFloor</c> measures the <em>tokens</em>,
    /// which is not the same claim as "every rendered combination clears its floor". A one-palette
    /// scan would answer that question for a quarter of the app.</para>
    ///
    /// <para>Measured: 11.8s for one palette's four addresses, 45.5s for all sixteen scans.</para>
    /// </summary>
    private static readonly (string Mode, string Theme)[] Palettes =
        [("Hero", "Light"), ("Hero", "Dark"), ("Villain", "Light"), ("Villain", "Dark")];

    /// <summary>
    /// Which of axe's rules run, and at what level.
    ///
    /// <para><b>No tag filter, and that is the decision.</b> The obvious first cut is
    /// <c>RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa"] }</c>, and it
    /// is wrong here: measured against this build with <c>page.GetAxeRules()</c>,
    /// <c>color-contrast</c> carries <c>wcag2aa</c> (among others) but <c>heading-order</c> and
    /// <c>page-has-heading-one</c> carry only <c>cat.semantics, best-practice</c> — axe's
    /// <c>best-practice</c> rules are never WCAG-tagged at all, by design. A
    /// <c>wcag2a,wcag2aa</c> filter would run one of the three rules this work was scoped around
    /// and silently drop the other two. So this runs axe's full default ruleset — every rule axe
    /// ships that is not itself marked <c>experimental</c> or <c>deprecated</c> — and turns
    /// individual rules off by name, below, each with its own dated reason. A reader can tell what
    /// is live by reading <see cref="Options"/>: no <c>RunOnly</c>, and one entry in
    /// <c>Rules</c>.</para>
    /// </summary>
    private static readonly AxeRunOptions Options = new()
    {
        // **Nothing is disabled, and that is a measurement rather than an aspiration.**
        //
        // The first three passes at this file disabled `color-contrast` with a long written reason
        // naming a stable 2.23:1 on `/build`'s `.disabled` control, reproduced on consecutive runs.
        // The ratio was real and the element was real; the *reading* was an artefact of scanning a
        // panel part-way through its entrance fade, and it went away the moment `Scan` waited for
        // animations. So the rule is live. If a future palette change puts a genuine contrast
        // failure in, this check goes red and the answer is to fix the palette or to disable the
        // rule *with a dated measurement taken after the waits in `Scan`* — a rule disabled on a
        // reading nobody re-took is how the last three attempts got the wrong answer.
        Rules = new Dictionary<string, RuleOptions>(),
    };

    /// <summary>
    /// The one thing axe reports here that is not a defect, dropped node by node rather than by
    /// turning its rule off — and counted, so it cannot quietly grow.
    ///
    /// <para><b>What it is.</b> <c>web/Components/StepButtons.razor</c> renders the wizard's Next
    /// control as an anchor with <c>class="btn primary disabled"</c> and
    /// <c>aria-disabled="true"</c>, which <c>app.css</c> paints at <c>opacity: 0.45</c> with
    /// <c>pointer-events: none</c>. On <c>/build</c> before a tier is chosen, axe measures it and
    /// reports <c>color-contrast</c> in every palette: <b>2.23:1 Hero/Light, 3.28:1 Hero/Dark,
    /// 2.54:1 Villain/Light, 3.22:1 Villain/Dark</b> against a 4.5:1 floor. Measured 2026-09-04 in
    /// real Chrome against the published site, after the waits in <see cref="Scan"/>, stable over
    /// five consecutive runs.</para>
    ///
    /// <para><b>Why it is exempt.</b> WCAG 1.4.3 says in as many words that text which is part of
    /// an inactive user interface component has no contrast requirement, and this control is
    /// inactive: <c>aria-disabled="true"</c>, no pointer events. axe cannot apply that exemption
    /// because it recognises the <c>disabled</c> <em>attribute</em>, which an anchor cannot carry.
    /// So this is axe unable to see a rule it agrees with, not a defect being waved past.</para>
    ///
    /// <para><b>What is nonetheless being taken on trust, and is the owner's call rather than
    /// this file's.</b> A disabled Next is exactly what a reader looks at to work out why they
    /// cannot go on, and 2.23:1 is faint. WCAG exempts it; legibility does not. That is a design
    /// decision, so this reports the figures and does not fail on them — see PROGRESS.md item 10.
    /// </para>
    ///
    /// <para><b>Node by node, not rule by rule</b>, so <c>color-contrast</c> stays live on every
    /// other element of every page — which is the whole reason this file exists. And the exemption
    /// carries its own positive control below: if it stops matching, it has become dead code, and
    /// a dead exemption makes a check imperceptibly easier every year.</para>
    /// </summary>
    private const string ExemptRule = "color-contrast";

    /// <inheritdoc cref="ExemptRule"/>
    private const string ExemptTarget = ".disabled";

    /// <summary>
    /// One address's scan: axe's rules against the page as rendered, and the three waits that had
    /// to be discovered rather than assumed.
    ///
    /// <para><b>Without them the check answers a different question every run, and that was found
    /// by running it, not by reading it.</b> Three consecutive runs against one unmodified build
    /// reported 2, 3 and 2 contrast violations, on different elements, with the <em>same</em>
    /// background reported as <c>#15151a</c>, <c>#17171c</c> and <c>#19191e</c>. Three shades of
    /// one colour is the tell: axe measures contrast against the composited pixel, and
    /// <c>.panel</c> carries <c>animation: rise var(--enter) both</c>, which starts at
    /// <c>opacity: 0</c>. Every reading was a panel caught at a different point of its fade.
    /// <c>docs/guide/testing.md</c> records the same trap one layer over — a bare screenshot of a
    /// proof page "proofs a washed-out lie" without <c>--virtual-time-budget</c>.</para>
    ///
    /// <para>An earlier pass blamed <c>font-display: swap</c> and added the first two waits. They
    /// are kept — a face that has not loaded really can change what axe measures, and they cost
    /// nothing — but they did not fix it, and the file said they had. The third one did.</para>
    ///
    /// <para><c>a.finished</c> rather than a sleep: a sleep is a guess that gets shorter as the
    /// machine gets busier, which is the direction that makes a check flaky instead of slow.</para>
    /// </summary>
    private static async Task<AxeResult> Scan(Harness harness, string address)
    {
        await harness.Open(address);
        await harness.Page.EvaluateAsync("document.fonts.ready");
        await harness.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await harness.Page.EvaluateAsync(
            "() => Promise.all(document.getAnimations().map(a => a.finished))");

        return await harness.Page.RunAxe(Options);
    }

    private static async Task<string> Run(Harness harness)
    {
        var findings = new List<string>();
        var scanned = 0;
        var passes = 0;
        var rulesEvaluated = 0;
        var exempted = 0;

        // **The palette this scans is chosen here, not inherited, and that was a real defect.**
        // This check runs last, so it used to scan whatever palette the PALETTE check happened to
        // leave the browser in — Villain/Dark — while every comment about it, and the contrast
        // figures first recorded for it, described Hero/Light. A check whose subject depends on
        // which check ran before it is not reproducible, and the report it prints names the wrong
        // thing. Clicked rather than stamped, for the reason the whole harness exists: a palette
        // set by hand is one nothing proves a reader can reach.
        foreach (var (mode, theme) in Palettes)
        {
            await harness.Open("/");
            await SettingsMenu.ClickButton(harness, "mode-switch", mode);
            await SettingsMenu.ClickButton(harness, "theme-switch", theme);

            Harness.Control(
                await harness.Eval<bool>(
                    "document.documentElement.getAttribute('data-mode') === "
                    + $"'{mode.ToLowerInvariant()}'"
                    + " && document.documentElement.getAttribute('data-theme') === "
                    + $"'{theme.ToLowerInvariant()}'"),
                $"the two settings switches did not reach {mode}/{theme}, so this scan would be "
                + "measuring a palette it cannot name");

            foreach (var address in Addresses)
            {
                var results = await Scan(harness, address);

                scanned++;
                passes += results.Passes.Length;
                rulesEvaluated += results.Passes.Select(r => r.Id)
                    .Concat(results.Violations.Select(r => r.Id))
                    .Concat(results.Incomplete.Select(r => r.Id))
                    .Concat(results.Inapplicable.Select(r => r.Id))
                    .Distinct()
                    .Count();

                foreach (var violation in results.Violations)
                {
                    var nodes = violation.Nodes.Where(n => !IsExempt(violation.Id, n)).ToList();

                    exempted += violation.Nodes.Length - nodes.Count;

                    if (nodes.Count == 0) continue;

                    findings.Add(
                        $"{mode}/{theme} {address}: {violation.Id} [{violation.Impact}] "
                        + $"x{nodes.Count} — {violation.Help} "
                        + $"(first: {nodes[0].Target} — {Summarise(nodes[0].Any)})");
                }
            }
        }

        // **The positive control, and it is not decoration.** axe returns an empty violation list
        // both when a page is clean and when it never ran a rule at all — a bad selector, a page
        // that had not rendered, an injection that silently failed, or a `RunOnly`/`Rules` typo
        // that filtered out everything. Counting *passing rule instances* would not catch that last
        // one cleanly: it conflates how many rules ran with how many elements a page happens to
        // have. So the control counts distinct rule ids that were evaluated at all — passed,
        // violated, incomplete or inapplicable is still "axe looked and had an opinion" — and
        // requires most of the default ruleset to have fired on every page. Measured against this
        // build: 88–89 distinct rule ids per address, out of axe's ~90-rule default set. 70 is
        // comfortably below every real run and comfortably above what a scan of an empty or
        // half-built document would reach.
        Harness.Control(scanned == Addresses.Length * Palettes.Length,
            $"only {scanned} of {Addresses.Length * Palettes.Length} scans happened");
        Harness.Control(rulesEvaluated >= 70 * scanned,
            $"axe evaluated only {rulesEvaluated} distinct rule instances across {scanned} pages, "
            + "so it scanned something that was not this application, or a rule filter dropped "
            + "most of the ruleset");

        // The exemption's own positive control. A selector that has stopped matching is dead
        // code that makes this check imperceptibly easier every year, which is how a suite rots.
        Harness.Control(exempted > 0,
            $"the {ExemptRule} exemption for '{ExemptTarget}' matched nothing. Either the wizard's "
            + "disabled Next control is gone — in which case delete the exemption — or this scan "
            + "is no longer reaching /build before a tier is chosen.");

        Harness.Outcome(findings.Count == 0,
            $"{findings.Count} accessibility violation(s): {string.Join(" | ", findings)}");

        return $"{Palettes.Length} palettes x {Addresses.Length} addresses, {passes} passing "
            + $"rule instances, no violations ({exempted} exempt: {ExemptTarget})";
    }

    /// <summary>
    /// Whether one failing node is the documented exemption. See <see cref="ExemptRule"/>.
    /// </summary>
    private static bool IsExempt(string rule, AxeResultNode node) =>
        rule == ExemptRule
        && node.Target.ToString().Contains(ExemptTarget, StringComparison.Ordinal);

    /// <summary>The one-line "why" axe attaches to a failing node.</summary>
    private static string Summarise(IEnumerable<AxeResultCheck> checks) =>
        string.Join("; ", checks.Select(c => c.Message));
}
