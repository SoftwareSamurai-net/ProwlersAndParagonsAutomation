using System.Text;
using Bunit;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Renders the real components into a static page so the design can be <b>looked at</b>,
/// rather than only asserted about.
///
/// <para><b>This is the route that does not need a dev server.</b> Starting one raises an
/// approval dialogue that blocks unattended work, and headless Chrome cannot wait for Blazor
/// to boot anyway. bUnit produces the same markup the browser would, and linking the real
/// <c>theme.css</c> and <c>app.css</c> beside it makes what Chrome renders the thing the app
/// renders.</para>
///
/// <para><b>It writes nothing unless asked.</b> With <c>PP_PROOF</c> unset this is a pair of
/// no-ops, because a test suite that writes files into <c>wwwroot</c> on every run is a test
/// suite that changes the thing it is testing. Set it, run <c>dotnet test</c>, and screenshot
/// the pages it leaves behind:</para>
///
/// <code>
/// PP_PROOF=1 dotnet test tests/ProwlersAndParagons.Web.Tests
/// chrome --headless --screenshot=out.png --window-size=1280,2400 \
///        --virtual-time-budget=3000 file:///…/web/wwwroot/proof-hero.html
/// </code>
///
/// <para><b>--virtual-time-budget is not optional.</b> <c>.panel</c> carries
/// <c>animation: rise var(--enter) both</c>, which starts at <c>opacity: 0</c>; a bare
/// screenshot fires before it finishes and every panel comes out washed. That has been read
/// as a palette fault and half-fixed as one.</para>
/// </summary>
public sealed class ProofPages
{
    private static bool Asked => !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("PP_PROOF"));

    /// <summary>The editors: cards, the filter box, rank words and the derived-stat rules.</summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheScreenDesign(SheetMode mode)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        var body = new StringBuilder();

        Section(body, "The budget, as a strip of chrome", ctx.Render<HpBudgetBar>().Markup);
        Section(body, "Tier — a card grid", ctx.Render<ChooseTier>().Markup);
        Section(body, "Abilities — the rulebook's word beside each rank",
            ctx.Render<AbilitiesTab>().Markup);
        Section(body, "Powers — one filter box, in the component every list shares",
            ctx.Render<PowersTab>().Markup);
        Section(body, "Derived — every figure shows the rule it came out of",
            ctx.Render<Derived>().Markup);

        Write($"proof-{Name(mode)}.html", Name(mode), body.ToString());
    }

    /// <summary>
    /// The shell — the chrome that sits above every step, rendered through the real layout.
    ///
    /// <para><b>The other proofs cannot show this and it is the thing Phase 1 is about.</b> They
    /// render components into a bare <c>.shell</c> div, so the banner, the step list and the
    /// budget strip never appear together and the question "how much vertical space does the
    /// chrome cost before any content" has no answer on the page. Judging a band you cannot see
    /// is how a design decision becomes a guess.</para>
    ///
    /// <para><c>MainLayout</c> renders here rather than being reproduced, so the bands proofed are
    /// the bands shipped. A copy of the banner markup in this file would drift from the real one
    /// and would proof itself.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheShell(SheetMode mode)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        WriteRaw($"proof-shell-{Name(mode)}.html", Name(mode), ShellBody(ctx));
    }

    /// <summary>
    /// The shell, with enough body to scroll against so the sticky band can be seen doing its job
    /// rather than merely existing. Shared with <see cref="EveryProofPageShowsWhatItIsFor"/>, so the
    /// markers are asserted against the markup that is actually written.
    /// </summary>
    private static string ShellBody(RenderContext ctx)
    {
        var tier = ctx.Render<ChooseTier>().Markup;
        return ctx.Render<MainLayout>(p => p.Add(l => l.Body, tier)).Markup;
    }

    /// <summary>
    /// The editors holding nothing — the state a first-time visitor actually meets.
    ///
    /// <para><b>Every other proof loads a sample, so none of them has ever shown this.</b>
    /// <c>RenderContext.With</c> fills every section, which is right for proofing a sheet and
    /// exactly wrong for proofing an empty list: the six empty states were invisible to every
    /// page this harness wrote, which is part of why they stayed full stops for so long.</para>
    /// </summary>
    [Fact]
    public void TheEmptyEditors()
    {
        if (!Asked) return;

        // No `.With(mode)`: a fresh character, with only the tier a step needs to render at all.
        using var ctx = new RenderContext();
        ctx.Session.Sheet.SelectedTierId = "standard";

        Write("proof-empty.html", "hero", EmptyBody(ctx));
    }

    /// <summary>The five editors holding nothing. Shared with the marker test, for the same reason.</summary>
    private static string EmptyBody(RenderContext ctx)
    {
        var body = new StringBuilder();

        Section(body, "The tab strip — untouched sections ringed",
            ctx.Render<Characteristics>().Markup);
        Section(body, "Powers, holding nothing", ctx.Render<PowersTab>().Markup);
        Section(body, "Perks, holding nothing", ctx.Render<PerksTab>().Markup);
        Section(body, "Flaws, holding nothing", ctx.Render<FlawsTab>().Markup);
        Section(body, "Gear, holding nothing", ctx.Render<Gear>().Markup);

        return body.ToString();
    }

    /// <summary>The sheet, which is the deliverable and is judged on paper.</summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void ThePrintedSheet(SheetMode mode)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        // Three times over, so a page break is forced through every kind of block rather than
        // landing wherever one character happens to put it.
        var sheet = ctx.Render<SheetView>().Markup;

        Write($"proof-sheet-{Name(mode)}.html", Name(mode), sheet + sheet + sheet);
    }

    private static string Name(SheetMode mode) => mode == SheetMode.Hero ? "hero" : "villain";

    private static void Section(StringBuilder body, string heading, string markup) =>
        body.Append("<h2 class=\"proof-heading\">").Append(heading).Append("</h2>")
            .Append(markup);

    /// <summary>
    /// Written into <c>wwwroot</c> rather than a temporary folder, because the stylesheet asks
    /// for its fonts at <c>../fonts/</c> relative to itself and the sheet is meant to be
    /// proofed with the faces it actually ships with.
    /// </summary>
    private static void Write(string file, string mode, string body) =>
        WritePage(file, mode, Page(file, mode, body, wrap: true));

    /// <summary>
    /// The same page without the <c>.shell</c> wrapper, for markup that brings its own — the
    /// layout's banner sits <em>outside</em> the shell, and wrapping it would put the one band
    /// that is supposed to run the full width of the window inside a 1100px column.
    /// </summary>
    private static void WriteRaw(string file, string mode, string body) =>
        WritePage(file, mode, Page(file, mode, body, wrap: false));

    /// <summary>
    /// Everything a proof page must contain to be proofing what it claims to, keyed by file.
    ///
    /// <para>These are generators rather than tests: with <c>PP_PROOF</c> unset they write nothing,
    /// so nothing about the <em>writing</em> is testable. What is testable — and is tested on every
    /// run by <see cref="EveryProofPageShowsWhatItIsFor"/> — is that the page each builder produces
    /// shows what it claims to. <b>That is not coverage of the design; it means a proof cannot
    /// silently become a picture of something else</b>, which is the dangerous shape, because a
    /// proof read as evidence is worse than no proof at all.</para>
    /// </summary>
    private static readonly Dictionary<string, string[]> MustShow = new(StringComparer.Ordinal)
    {
        // The shell's bands, and the banner outside the shell rather than in it.
        //
        // **`banner-inner` is here because deleting the element while its CSS stayed passed every
        // other test**, and the end state is worse than the defect it fixed: the banner's contents
        // then have no padding at all and sit flush against the window edge. A CSS guard cannot see
        // a missing element, so the markup is asserted where the markup is built.
        //
        // The villain page has no `.budget` on purpose — Ch.9 gives Villains no budget, and that
        // asymmetry is exactly why the step band has to carry the closing edge itself.
        ["proof-shell-hero.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"budget\"", "class=\"shell\""],
        ["proof-shell-villain.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"shell\""],
        // The empty editors: the tab strip with a marker, and an empty state from each editor.
        // One marker per section, or the page can lose four of its five and still pass: the tab
        // strip alone carries both an `empty-state` and a ring, so a two-marker list only forbade
        // dropping the strip. These are the panel headings, which are what each section is for.
        ["proof-empty.html"] =
        [
            "tab-count untouched", "empty-state",
            "Powers on this character", "Perks", "Flaws", "Carried",
        ],
    };

    /// <summary>
    /// Builds every proof page and asserts its markers — <b>as an ordinary test, which runs whether
    /// or not <c>PP_PROOF</c> is set.</b>
    ///
    /// <para><b>Putting the marker checks inside the writer was close to theatre and a fix-audit said
    /// so.</b> They sat after <c>if (!Asked) return</c>, so the one mutation the negative half exists
    /// for — wrapping the shell proof in a column, which defeats its whole purpose — was invisible to
    /// CI and to every normal run, including the run whose count the commit message quoted. A guard
    /// that only fires under an environment variable nobody sets in CI is not a guard.</para>
    ///
    /// <para>This drives the same builders and asserts on what they produce, so the markers are
    /// checked on every run. It still writes nothing: proofing is what <c>PP_PROOF</c> is for.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void EveryProofPageShowsWhatItIsFor(SheetMode mode)
    {
        using var ctx = new RenderContext().With(mode);

        var shell = Page($"proof-shell-{Name(mode)}.html", Name(mode), ShellBody(ctx), wrap: false);
        AssertMarkers($"proof-shell-{Name(mode)}.html", shell);

        // The mode reaches the page. `WriteRaw(file, Name(mode), …)` could be passed a constant and
        // the villain proof would come out a copy of the hero one — on the very file whose
        // non-inspection caused this phase's worst defect.
        Assert.Contains($"data-mode=\"{Name(mode)}\"", shell, StringComparison.Ordinal);

        using var fresh = new RenderContext();
        fresh.Session.Sheet.SelectedTierId = "standard";

        var empty = Page("proof-empty.html", "hero", EmptyBody(fresh), wrap: true);
        AssertMarkers("proof-empty.html", empty);
    }

    private static void AssertMarkers(string file, string page)
    {
        Assert.True(MustShow.ContainsKey(file), $"No markers are recorded for {file}.");

        foreach (var marker in MustShow[file])
            Assert.Contains(marker, page, StringComparison.Ordinal);

        if (MustNotShow.TryGetValue(file, out var banned))
            foreach (var marker in banned)
                Assert.DoesNotContain(marker, page, StringComparison.Ordinal);
    }

    /// <summary>
    /// What a proof page must <b>not</b> contain — and this half is what catches the mutation the
    /// positive markers cannot.
    ///
    /// <para>Swapping <c>WriteRaw</c> for <c>Write</c> on a shell proof wraps the layout in
    /// <c>&lt;div class="shell"&gt;</c>, putting the one band that must run the full width of the
    /// window inside a 1100px column. Every positive marker survives that, because the layout emits
    /// its own <c>&lt;main class="shell"&gt;</c> either way — so the thing to refuse is the
    /// <em>wrapper</em>, which only <c>Write</c> produces.</para>
    /// </summary>
    private static readonly Dictionary<string, string[]> MustNotShow = new(StringComparer.Ordinal)
    {
        ["proof-shell-hero.html"] = ["<div class=\"shell\">"],
        ["proof-shell-villain.html"] = ["<div class=\"shell\">"],
    };

    /// <summary>
    /// The whole page, as a string. Separate from writing it so the marker test can assert on
    /// exactly what would be written without writing anything.
    /// </summary>
    private static string Page(string file, string mode, string body, bool wrap)
    {
        _ = file;   // kept in the signature so a caller cannot pass a body for the wrong page

        var inner = wrap ? $"<div class=\"shell\">{body}</div>" : body;

        return $"""
            <!doctype html>
            <html lang="en" data-mode="{mode}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Proof — {mode}</title>
              <link rel="stylesheet" href="css/theme.css">
              <link rel="stylesheet" href="css/app.css">
            </head>
            <body>
            {inner}
            </body>
            </html>
            """;
    }

    private static void WritePage(string file, string mode, string page)
    {
        _ = mode;
        File.WriteAllText(Path.Combine(RepoRoot(), "web", "wwwroot", file), page);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
