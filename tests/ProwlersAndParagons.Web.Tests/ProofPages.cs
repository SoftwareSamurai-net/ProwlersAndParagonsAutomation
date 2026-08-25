using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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

        // Opened first, or it renders nothing and the proof shows a blank section — which is
        // exactly the shape of "verified by looking" that let a chrome band with no bottom edge
        // ship in Villain mode.
        ctx.Services.GetRequiredService<Commands>().Open();
        Section(body, "The command palette — Ctrl-K, from anywhere",
            ctx.Render<CommandPalette>().Markup);
        ctx.Services.GetRequiredService<Commands>().Close();

        // The breakdown is disclosed on request and proofed shut shows nothing but the strip
        // itself — the same trap as the palette above, and the one this section exists to
        // avoid: the meters that show where the points went only render once opened.
        var budget = ctx.Render<HpBudgetBar>();
        budget.Find(".budget-toggle").Click();
        Section(body, "The budget, as a strip of chrome", budget.Markup);
        Section(body, "Tier — a card grid", ctx.Render<ChooseTier>().Markup);
        Section(body, "The portfolio — the demonstrations, out of the tool",
            ctx.Render<Portfolio>().Markup);
        Section(body, "Abilities — the rulebook's word beside each rank",
            ctx.Render<AbilitiesTab>().Markup);
        Section(body, "Powers — one filter box, in the component every list shares",
            ctx.Render<PowersTab>().Markup);
        Section(body, "Derived — every figure shows the rule it came out of",
            ctx.Render<Derived>().Markup);

        // Accounts, both states, because the interesting one is invisible from the other. A
        // separate context: this one is signed in, and the sign-in page renders whichever half
        // of itself applies — so a proof built from one context could only ever show one.
        Section(body, "Sign in — no password, and nothing here holds a credential",
            ctx.Render<SignIn>().Markup);

        // **Signed in before the sample is loaded, and that order is load-bearing.** Loading a
        // sample raises the session's change event, which writes the character through — which
        // asks who is here and *remembers the answer*. Set afterwards, this proof rendered the
        // signed-out form under a heading saying "Signed in", and looked entirely plausible.
        using var signedIn = new RenderContext();
        signedIn.Api.SignedIn = ("acct-7", "player");
        signedIn.With(mode);
        signedIn.Api.Book["Armor"] =
            "Self • Half Toughness • 1 Hero Point per rank\n"
            + "Armor represents protection against damage, whether it is a thick hide, a force "
            + "field, or a suit of powered plate. Each rank reduces the damage you take.\n"
            + "Armor does not stack with other Armor: only the highest rank applies.";

        Section(body, "Signed in — the account, and what signing out leaves behind",
            signedIn.Render<SignIn>().Markup);

        var armor = signedIn.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "armor");

        // Opened, for the same reason the palette above is: a disclosure proofed shut shows an
        // empty section and reads as a feature that works.
        var editor = signedIn.Render<PowerEditor>(p => p.Add(e => e.Power, armor));
        editor.Find(".book-toggle").Click();

        Section(body, "A Power's printed entry — the book's voice, set apart from ours",
            editor.Markup);

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
    /// <remarks>
    /// <b>Four pages, because there are four palettes.</b> Light and dark are independent of
    /// Hero and Villain, so proofing the two identities alone leaves half the app unlooked-at —
    /// and the half that is new. The dark pair stamp <c>data-theme="dark"</c>, which is what an
    /// explicit choice does; the light pair stamp nothing, which is the default.
    /// </remarks>
    [Theory]
    [InlineData(SheetMode.Hero, null)]
    [InlineData(SheetMode.Hero, "dark")]
    [InlineData(SheetMode.Villain, null)]
    [InlineData(SheetMode.Villain, "dark")]
    public void TheShell(SheetMode mode, string? theme)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        WriteRaw(ShellPage(mode, theme), Name(mode), ShellBody(ctx), theme);
    }

    /// <summary>
    /// The file name for one of the four shell proofs. The light pair keep the names the sticky,
    /// narrow and inset harnesses already point at — those measure geometry, which no palette
    /// changes, so pointing them at a second copy would double the run for nothing.
    /// </summary>
    private static string ShellPage(SheetMode mode, string? theme) =>
        theme is null ? $"proof-shell-{Name(mode)}.html" : $"proof-shell-{Name(mode)}-{theme}.html";

    /// <summary>
    /// The shell, with enough body to scroll against so the sticky band can be seen doing its job
    /// rather than merely existing. Shared with <see cref="EveryProofPageShowsWhatItIsFor"/>, so the
    /// markers are asserted against the markup that is actually written.
    /// </summary>
    private static string ShellBody(RenderContext ctx)
    {
        // **On the builder's own address, because the chrome is now decided by the address.**
        // The step band and the budget strip are the builder's and are drawn nowhere else, so a
        // shell proof rendered at the front door would be a picture of a page with neither —
        // captioned as the thing it is not, which is the failure this file exists to prevent.
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(Commands.FirstStep);

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

    /// <summary>
    /// Accounts, on a page of their own.
    ///
    /// <para><b>The screen proof carries these too, and that is not enough.</b> It runs to some
    /// seven thousand pixels, so the three surfaces this slice added are a strip near the bottom
    /// of an image nobody can read at a glance — and "verified by looking" at a page too big to
    /// look at is how a chrome band with no bottom edge shipped once already. This is short
    /// enough to actually see.</para>
    ///
    /// <para>Both palettes, because the entry sets the book's own prose on a sunk ground with a
    /// rule in <c>--accent</c>, and Villain <c>--accent</c> is a different colour on a different
    /// surface. A proof of one says nothing about the other.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public async Task TheAccountSurfaces(SheetMode mode)
    {
        if (!Asked) return;

        var body = new StringBuilder();

        // **The manager with characters in it**, because the empty state shows none of the rows and
        // none of the per-row controls — which is most of the component. Proofing it empty is how a
        // panel gets called verified without being seen, and the first two attempts at this did
        // exactly that.
        //
        // Two things had to be got right, and both were got wrong first:
        //
        // * **Signed in before `.With(mode)`.** Loading a sample raises the session's change event,
        //   which writes the character through, which asks who is here and *remembers the answer* —
        //   so setting it afterwards left the manager reading the anonymous list and captioned "In
        //   this browser". The same trap this file already documents twenty lines down.
        // * **Saved through the account, not through local storage.** bUnit's JS interop here is
        //   loose: `ppStore.save` is recorded and `ppStore.load` answers null, so nothing written to
        //   local storage can be read back. `FakeApi` is a real in-memory store, so the account is
        //   the only side that can actually hold a character for a proof to render.
        await using var holding = new RenderContext();
        holding.Api.SignedIn = ("acct-7", "player");
        holding.With(mode);

        var account = holding.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        Section(body, "Your characters — the top of the tier page, where a panel of one red button was",
            holding.Render<CharacterManager>().Markup);

        await using var anonymous = new RenderContext().With(mode);

        Section(body, "Signed out — one field, and no password anywhere",
            anonymous.Render<SignIn>().Markup);

        // **Signed in before the sample is loaded, and that order is load-bearing.** Loading a
        // sample raises the session's change event, which writes the character through — which
        // asks who is here and *remembers the answer*. Set afterwards, this proof rendered the
        // signed-out form under a heading saying "Signed in", and looked entirely plausible.
        await using var signedIn = new RenderContext();
        signedIn.Api.SignedIn = ("acct-7", "player");
        signedIn.With(mode);
        signedIn.Api.Book["Armor"] =
            "Self • Half Toughness • 1 Hero Point per rank\n"
            + "Armor represents protection against damage, whether it is a thick hide, a force "
            + "field, or a suit of powered plate. Each rank reduces the damage you take.\n"
            + "Armor does not stack with other Armor: only the highest rank applies.";

        Section(body, "Signed in — and what signing out leaves behind",
            signedIn.Render<SignIn>().Markup);

        var armor = signedIn.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "armor");

        // Shut, then open, both on the page: the closed state is the one every reader meets
        // first, and a proof of only the open one says nothing about whether the offer reads as
        // an offer.
        Section(body, "A Power, with the entry offered but not opened",
            signedIn.Render<PowerEditor>(p => p.Add(e => e.Power, armor)).Markup);

        var opened = signedIn.Render<PowerEditor>(p => p.Add(e => e.Power, armor));
        await opened.Find(".book-toggle").ClickAsync();

        Section(body, "…and opened — the book's voice, set apart from ours", opened.Markup);

        Write($"proof-accounts-{Name(mode)}.html", Name(mode), body.ToString());
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
    private static void WriteRaw(string file, string mode, string body, string? theme = null) =>
        WritePage(file, mode, Page(file, mode, body, wrap: false, theme));

    /// <summary>
    /// Does the budget strip still stick? <b>A measured check, because nothing else can answer it.</b>
    ///
    /// <para><c>position: sticky</c> is bounded by the element's containing block. The strip stays
    /// put across a whole step only because that block is the document — it is a sibling of
    /// <c>main</c>, not a child of any wrapper. Anything that wraps it, and any <c>transform</c>,
    /// <c>filter</c> or <c>contain</c> on an ancestor, silently ends that: nothing looks wrong, the
    /// markup is unchanged, and every CSS guard still passes. <b>The View Transitions work in
    /// Phase 2 is precisely the change that would introduce such a wrapper.</b></para>
    ///
    /// <para>bUnit cannot see this — it has no layout engine, so there is no
    /// <c>getBoundingClientRect</c> and no scrolling. The only honest answer comes from a browser,
    /// so this writes a harness that iframes the real shell proof, scrolls it, and measures.</para>
    ///
    /// <para><b>It states its verdict as a token rather than leaving it to the eye</b>, so the check
    /// is read out of the DOM instead of a screenshot:</para>
    ///
    /// <code>
    /// chrome --headless=new --no-sandbox --allow-file-access-from-files \
    ///        --user-data-dir=&lt;temp&gt; --virtual-time-budget=3000 \
    ///        --dump-dom file:///…/web/wwwroot/proof-sticky.html
    /// </code>
    ///
    /// <para><b>Read the verdict out of the <c>&lt;title&gt;</c>, not the page text.</b> Both verdict
    /// strings appear in the harness's own script source, so a dumped DOM contains
    /// <c>STICKY: PASS</c> whether or not the script ever ran — grepping the whole document for it
    /// is a check that passes on a harness that never fired. The title is written only by the
    /// verdict, and its resting value is neither, so it separates the three states:</para>
    ///
    /// <code>
    /// grep -o '&lt;title&gt;STICKY: [A-Z]*' dump.html    # PASS, FAIL, or nothing at all
    /// </code>
    ///
    /// <para><b>Assert on the positive.</b> Grepping for FAIL and finding nothing calls both a real
    /// failure and a broken harness green.</para>
    /// </summary>
    [Fact]
    public void TheStickyStrip()
    {
        if (!Asked) return;

        WritePage("proof-sticky.html", "hero", StickyHarness());
    }
    /// <summary>
    /// Does the motion script actually behave? <b>A driven check, because the source-reading ones
    /// were theatre and a variant proved it.</b>
    ///
    /// <para>Two guards in <c>WebPresentationTests</c> assert that <c>motion.js</c> mentions
    /// <c>still()</c> and <c>setTimeout</c>. Both pass against a script that does the opposite of
    /// what it says: <c>if (… || !still()) return</c> serves the animation to exactly the people
    /// who asked for none, and <c>setTimeout(() =&gt; {}, 1000)</c> is a safety net that catches
    /// nothing. Both are one-token edits, both survived, and <b>no string assertion can tell them
    /// apart from the real thing</b> — the property is behavioural, so the instrument has to run
    /// the code.</para>
    ///
    /// <para>So this harness loads the real <c>motion.js</c>, stubs
    /// <c>document.startViewTransition</c> to observe it, and asks three questions: does
    /// <c>begin()</c> open a transition, does <c>end()</c> release it, and does the safety timer
    /// release one that <c>end()</c> never reaches. Run it twice — the second with Chrome's
    /// <c>--force-prefers-reduced-motion</c>, under which opening a transition at all is the
    /// failure:</para>
    ///
    /// <code>
    /// chrome --headless=new --no-sandbox --allow-file-access-from-files \
    ///        --user-data-dir=&lt;temp&gt; --virtual-time-budget=5000 \
    ///        --dump-dom file:///…/web/wwwroot/proof-motion.html
    ///
    /// chrome … --force-prefers-reduced-motion --dump-dom file:///…/proof-motion.html
    /// </code>
    ///
    /// <para>The harness reads the media query itself and expects the opposite behaviour under
    /// it, so one file covers both runs. Read the verdict from the <c>&lt;title&gt;</c>, for the
    /// reason given on <see cref="TheStickyStrip"/>.</para>
    /// </summary>
    [Fact]
    public void TheMotionScript()
    {
        if (!Asked) return;

        WritePage("proof-motion.html", "hero", MotionHarness());
    }

    /// <summary>
    /// The motion harness, as a string. It loads the shipped script rather than a copy: a
    /// reproduction here would drift, and would then proof itself.
    /// </summary>
    /// <summary>
    /// The half of the command palette that no render test can reach: the key itself.
    ///
    /// <para><b>bUnit can drive the component's key handler and cannot drive the document.</b>
    /// Ctrl-K is heard by a listener on an element no render tree contains, so every assertion
    /// about what the arrow keys do sits on top of an opening chord that nothing checks. That
    /// is the shape of gap this repository has shipped four times — a feature that never ran,
    /// mistaken for a feature that worked.</para>
    ///
    /// <para>So this drives the real <c>wwwroot/js/palette.js</c> with synthetic key events and
    /// a stand-in for the component, and asserts the negative cases too: a bare <c>k</c> and a
    /// <c>Ctrl-J</c> must reach nothing. Focus is checked in both directions, because a palette
    /// that takes focus and does not give it back is a keyboard trap, which is the one thing a
    /// keyboard affordance must not be.</para>
    ///
    /// <code>
    /// chrome … --dump-dom file:///…/web/wwwroot/proof-shortcut.html
    /// </code>
    /// </summary>
    [Fact]
    public void TheShortcutListener()
    {
        if (!Asked) return;

        WritePage("proof-shortcut.html", "hero", ShortcutHarness());
    }

    /// <summary>
    /// <b>Does the light/dark choice survive a reload?</b> Nothing in either .NET suite can
    /// answer that, and the gap was found by mutation rather than by reading.
    ///
    /// <para>Deleting the <c>localStorage.setItem</c> from <c>theme.js</c> — so a choice applies
    /// for the visit and is forgotten the moment the tab is closed — left <b>all 4,115 tests
    /// green</b>. The C# side is guarded properly: it is checked that <c>Choose</c> sends the
    /// right word to <c>ppTheme.set</c>, and that a stored value is read back into the control.
    /// Both of those are true of a script that stores nothing. <em>Persistence happens entirely
    /// inside the script</em>, and only a browser can see it.</para>
    ///
    /// <para>So this drives the shipped file and asserts the whole loop: that a choice is
    /// written to durable storage under the key the app claims, that returning to the default
    /// removes it rather than storing a third word, and — by executing the module a second
    /// time, which is what a reload does — that a stored choice is stamped on the document at
    /// load with nobody calling anything.</para>
    ///
    /// <para><b>The preference is deliberately not attached to an account.</b> It is a fact
    /// about a person and a browser: somebody signed in on a shared machine should not impose
    /// their theme on the next reader, and somebody with no account should still keep theirs.
    /// <c>localStorage</c> over a cookie because a cookie is sent with every request — every
    /// font, every framework asset — to a server that has no use for it.</para>
    ///
    /// <code>
    /// chrome … --dump-dom file:///…/web/wwwroot/proof-theme.html
    /// </code>
    /// </summary>
    [Fact]
    public void TheThemePreference()
    {
        if (!Asked) return;

        WritePage("proof-theme.html", "hero", ThemeHarness());
    }

    private static string ThemeHarness() =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheThemePreference. Do not edit: rewritten on every
             PP_PROOF run. Drives the real wwwroot/js/theme.js. -->
        <html lang="en" data-mode="hero">
        <head>
          <meta charset="utf-8">
          <title>Proof — does the light/dark choice survive a reload?</title>
          <style>
            body { margin: 0; font: 14px monospace; background: #111; color: #eee; padding: 12px }
            #verdict { white-space: pre }
            .bad { color: #f66 }
          </style>
          <!-- Loaded here, as index.html loads it, so what runs is the shipped file in the
               position it actually occupies. -->
          <script src="js/theme.js"></script>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <script>
        (() => {
          const box = document.getElementById('verdict');
          const checks = [];
          const check = (name, ok, detail) => checks.push({ name, ok, detail });

          const KEY = 'pp.theme.v1';
          const stored = () => { try { return localStorage.getItem(KEY); } catch (e) { return 'THREW: ' + e.name; } };
          const stamped = () => document.documentElement.getAttribute('data-theme');

          // **The positive control, before anything that depends on it.** Every check below
          // asserts an outcome, and an outcome is satisfied by a script that never ran — this
          // file could have thrown on load and the "nothing is stamped" cases would all hold.
          check('the script ran at all (positive control)',
                window.ppThemeStats && window.ppThemeStats.stamps === 1,
                `stamps = ${window.ppThemeStats && window.ppThemeStats.stamps}`);

          // Storage has to be reachable, or every assertion about it is about an exception.
          try { localStorage.setItem(KEY + '.probe', '1'); localStorage.removeItem(KEY + '.probe'); }
          catch (e) { check('local storage is usable in this harness', false, e.name); }

          try { localStorage.removeItem(KEY); } catch { /* reported above */ }

          // ── The write half ────────────────────────────────────────────────────────────
          window.ppTheme.set('dark');
          check('choosing dark stores it under the key the app claims',
                stored() === 'dark',
                `${KEY} = ${stored()}`);
          check('...and stamps it on the document',
                stamped() === 'dark',
                `data-theme = ${stamped()}`);

          window.ppTheme.set('light');
          check('choosing light replaces it rather than adding to it',
                stored() === 'light' && stamped() === 'light',
                `${KEY} = ${stored()}, data-theme = ${stamped()}`);

          // Returning to the default stores *nothing*. A third word here would match neither
          // the light path nor `:not([data-theme="light"])`, which is a palette nobody designed.
          window.ppTheme.set('system');
          check('returning to Auto removes the value rather than storing a third word',
                stored() === null,
                `${KEY} = ${stored()}`);
          check('...and removes the attribute rather than setting it to "system"',
                stamped() === null,
                `data-theme = ${stamped()}`);

          // ── The read half, which is the one that survives a reload ────────────────────
          //
          // Executing the module a second time is what a fresh page load does: the file is an
          // IIFE that reads storage and stamps the attribute at the bottom. Nothing calls set()
          // here, so a script that stores but never reads back fails exactly here.
          window.ppTheme.set('dark');
          document.documentElement.removeAttribute('data-theme');

          // Four set() calls have run since the file did, so the counter stands at five. The
          // module declares `ppThemeStats` fresh, so a re-execution puts it *back to one* —
          // which is what makes this a control rather than a hope. The first version asserted
          // the counter had gone **up**, and failed on working code: the harness caught its own
          // control being wrong, which is the only reason this one is right.
          const beforeReload = window.ppThemeStats.stamps;

          const again = document.createElement('script');
          again.src = 'js/theme.js';
          again.onload = () => {
            check('a fresh load of the script stamps the stored choice, with nothing calling it',
                  stamped() === 'dark',
                  `data-theme = ${stamped()} after re-executing the module`);

            check('...and the module really re-executed (positive control)',
                  beforeReload === 5 && window.ppThemeStats.stamps === 1,
                  `stamps went ${beforeReload} → ${window.ppThemeStats.stamps}; a reset to 1 is `
                  + 'only reachable by the file running again');

            // Leave the browser profile as it was found.
            try { localStorage.removeItem(KEY); } catch { /* nothing to do */ }

            const ok = checks.every((c) => c.ok);
            document.title = ok ? 'THEME: PASS' : 'THEME: FAIL';
            box.className = ok ? '' : 'bad';
            box.textContent =
              (ok ? 'THEME: PASS' : 'THEME: FAIL') + '\n' +
              checks.map((c) => `  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name} — ${c.detail}`).join('\n');
          };

          // A script that fails to load leaves the verdict at its resting value, which is
          // neither PASS nor FAIL — the same three-state convention the sticky harness uses.
          again.onerror = () => {
            document.title = 'THEME: FAIL';
            box.className = 'bad';
            box.textContent = 'THEME: FAIL\n  FAIL js/theme.js could not be loaded a second time';
          };

          document.head.appendChild(again);
        })();
        </script>
        </body>
        </html>
        """;

    private static string ShortcutHarness() =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheShortcutListener. Do not edit: rewritten on every
             PP_PROOF run. Drives the real wwwroot/js/palette.js. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — does Ctrl-K reach the palette?</title>
          <style>
            body { margin: 0; font: 14px monospace; background: #111; color: #eee; padding: 12px }
            #verdict { white-space: pre }
            .bad { color: #f66 }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <input id="outside" />
        <input id="inside" />
        <script src="js/palette.js"></script>
        <script>
        (() => {
          const box = document.getElementById('verdict');
          const checks = [];
          const check = (name, ok, detail) => checks.push({ name, ok, detail });

          // The stand-in for the component. Blazor hands the script an interop object with
          // exactly this one method on it; nothing else about the component is reachable from
          // here, and nothing else needs to be.
          const calls = [];
          const owner = { invokeMethodAsync: (name) => { calls.push(name); return Promise.resolve(); } };

          // **The positive control, before anything that depends on it.** Every check below
          // asserts an outcome, and an outcome is satisfied by a listener that was never
          // registered — the script could have thrown on load and every negative case would
          // still hold.
          window.ppPalette.listen(owner);
          check('the listener registered at all (positive control)',
                window.ppPaletteStats.listeners === 1,
                `listeners = ${window.ppPaletteStats.listeners}`);

          // A second call must not stack a second listener, or one press toggles twice — which
          // reads as the key not working rather than as working twice.
          window.ppPalette.listen(owner);
          check('registering twice still leaves one listener',
                window.ppPaletteStats.listeners === 1,
                `listeners = ${window.ppPaletteStats.listeners}`);

          const press = (init) => {
            const e = new KeyboardEvent('keydown', { ...init, bubbles: true, cancelable: true });
            document.dispatchEvent(e);
            return e;
          };

          const ctrl = press({ key: 'k', ctrlKey: true });
          check('Ctrl-K reaches the component',
                calls.length === 1 && calls[0] === 'Toggle',
                `calls = ${JSON.stringify(calls)}`);

          // The browser focuses its address bar or its search box on this chord. Taking the key
          // is what makes the affordance reachable at all, so a version that listened without
          // suppressing the default would look right here and be unusable in a real browser.
          check('Ctrl-K takes the key from the browser',
                ctrl.defaultPrevented === true,
                `defaultPrevented = ${ctrl.defaultPrevented}`);

          press({ key: 'k', metaKey: true });
          check('Command-K reaches the component too',
                calls.length === 2,
                `calls = ${calls.length}`);

          // The negative cases. Without them the checks above are satisfied by a listener that
          // fires on every key, which would make the palette open while somebody types a name.
          press({ key: 'k' });
          press({ key: 'j', ctrlKey: true });
          press({ key: 'K' });
          check('a bare k and a Ctrl-J reach nothing',
                calls.length === 2,
                `calls = ${calls.length} after three more presses`);

          check('the press counter agrees with the calls (positive control)',
                window.ppPaletteStats.presses === 2,
                `presses = ${window.ppPaletteStats.presses}`);

          // Focus, both ways. `enter` remembers where focus was and moves it; `leave` puts it
          // back. A palette that takes focus and does not return it is a keyboard trap.
          const outside = document.getElementById('outside');
          const inside = document.getElementById('inside');

          outside.focus();
          window.ppPalette.enter(inside);
          check('focus moves into the palette box',
                document.activeElement === inside,
                `activeElement = ${document.activeElement && document.activeElement.id}`);

          window.ppPalette.leave();
          check('focus goes back where it came from',
                document.activeElement === outside,
                `activeElement = ${document.activeElement && document.activeElement.id}`);

          // ...and a second leave with nothing remembered must not move focus. Focusing the
          // body as a fallback is indistinguishable from focus being lost.
          inside.focus();
          window.ppPalette.leave();
          check('a second leave moves nothing',
                document.activeElement === inside,
                `activeElement = ${document.activeElement && document.activeElement.id}`);

          const ok = checks.every((c) => c.ok);
          document.title = ok ? 'SHORTCUT: PASS' : 'SHORTCUT: FAIL';
          box.className = ok ? '' : 'bad';
          box.textContent =
            (ok ? 'SHORTCUT: PASS' : 'SHORTCUT: FAIL') + '\n' +
            checks.map((c) => `  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name} — ${c.detail}`).join('\n');
        })();
        </script>
        </body>
        </html>
        """;

    private static string MotionHarness() =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheMotionScript. Do not edit: rewritten on every PP_PROOF run.
             Drives the real wwwroot/js/motion.js. Run once plainly and once with Chrome's
             --force-prefers-reduced-motion; the expectations invert, and the harness knows it. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — does the motion script behave?</title>
          <!-- **theme.css is linked because a script that reads a duration token needs it, and
               the absence of it fails silently upwards.** The parked counting figure read
               `--enter` from the document element; with no stylesheet the token resolves to the
               empty string, parseFloat gives NaN, and the script took its "nothing to animate"
               path on every call — at which point four checks of the animation passed without a
               single animation running. That is exactly how this harness shipped for one commit.
               The `--enter resolves` check below is the guard against it recurring. -->
          <link rel="stylesheet" href="css/theme.css">
          <style>
            body { margin: 0; font: 14px monospace; background: #111; color: #eee; padding: 12px }
            #verdict { white-space: pre }
            .bad { color: #f66 }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <script src="js/motion.js"></script>
        <script>
        (async () => {
          const box = document.getElementById('verdict');
          const checks = [];
          const check = (name, ok, detail) => checks.push({ name, ok, detail });

          // **The harness's own precondition, asserted before anything depends on it.**
          // Nothing in `motion.js` reads a duration token today — the counting figure that did is
          // parked — but the trap it recorded is real and returns the moment one does: a script
          // that cannot read `--enter` degrades to assigning the end state, and a test of the end
          // state then holds trivially. **A proof must fail when its subject is not running, not
          // when it is.** Kept as a live check rather than a comment, because a comment does not
          // fail.
          const enterMs = parseFloat(
            window.getComputedStyle(document.documentElement).getPropertyValue('--enter'));
          check('--enter resolves, so a scripted duration could be read',
                enterMs > 0,
                `--enter parsed as ${enterMs}`);

          // Whether this run is the reduced-motion one. Every expectation below inverts on it,
          // which is the whole point: a gate that is present but backwards passes one run and
          // fails the other, and no reading of the source can tell the two apart.
          const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

          // Stub the API so the script's behaviour can be observed without a real navigation.
          // The callback's promise is what `begin()` holds open; capturing it is how `end()`
          // and the safety timer are told apart from doing nothing at all.
          let started = 0;
          let held = null;
          let releasedAt = null;

          // Every opened transition, in order, and which of them have been released. The overlap
          // check needs both: a `begin()` while one is open must release the first, and a single
          // "was it released" flag cannot tell one release from two.
          const released = [];
          document.startViewTransition = (cb) => {
            started++;
            const mine = started;
            held = cb();
            held.then(() => {
              releasedAt = releasedAt ?? 'released';
              released.push(mine);
            });
            return { ready: Promise.resolve(), finished: Promise.resolve(),
                     updateCallbackDone: Promise.resolve(), skipTransition: () => {} };
          };

          const settle = () => new Promise((r) => setTimeout(r, 0));

          // 1. begin() opens a transition — unless movement is not wanted, when it must not.
          window.ppMotion.begin();
          check('begin opens a transition',
                started === (reduced ? 0 : 1),
                `reduced=${reduced} started=${started}`);

          // 2. end() releases it, which is what lets the second snapshot be taken.
          window.ppMotion.end();
          await settle();
          check('end releases it',
                reduced ? started === 0 : releasedAt === 'released',
                `releasedAt=${releasedAt}`);

          // 3. The safety net: a transition nobody ends must release itself, or the live DOM
          //    stays hidden behind a snapshot and the app is frozen with no way back.
          started = 0; held = null; releasedAt = null;
          window.ppMotion.begin();
          await new Promise((r) => setTimeout(r, 1500));
          check('an unreleased transition frees itself',
                reduced ? started === 0 : releasedAt === 'released',
                `reduced=${reduced} started=${started} releasedAt=${releasedAt}`);


          // ── Positive controls ────────────────────────────────────────────────────────
          //
          // **Twice a check here passed because the feature never ran**: once when an inverted
          // gate meant no transition opened, once when an unreadable duration token meant no
          // count did. Both times the *outcome* assertions were satisfied by the absence of the
          // work. So the work is now asserted to have happened, before anything asks whether it
          // was right — and under reduced motion the same counter must read zero, which is the
          // outcome there.
          check('a transition actually opened (positive control)',
                reduced
                  ? window.ppMotionStats.transitions === 0
                  : window.ppMotionStats.transitions > 0,
                `reduced=${reduced} transitions=${window.ppMotionStats.transitions}`);

          // ── The counting figure ──────────────────────────────────────────────────────
          //
          // Driven by *seeking* the clock, not by waiting. --virtual-time-budget suppresses
          // frame production, so rAF never ticks and the document timeline never advances —
          // measured, and the reason the count is a WAAPI animation rather than a rAF loop.
          // `draw()` is a pure function of `clock.currentTime`, and it is the same `draw` a
          // visitor's frame calls, so this drives the shipped path rather than a reproduction.
          const cell = document.createElement('span');
          document.body.appendChild(cell);

          const before = window.ppMotionStats.counts;
          window.ppCount(cell, 10, 20);

          check('a count actually started (positive control)',
                reduced
                  ? window.ppMotionStats.counts === before
                  : window.ppMotionStats.counts === before + 1,
                `reduced=${reduced} counts ${before} -> ${window.ppMotionStats.counts}`);

          if (reduced) {
            // Under reduced motion there is no clock at all, and the answer is already shown.
            check('reduced motion shows the answer with no clock',
                  cell.textContent === '20' && !cell.ppCount,
                  `showed ${cell.textContent}, clock ${cell.ppCount ? 'present' : 'absent'}`);
          } else {
            const clock = cell.ppCount.clock;
            const ms = enterMs;

            // Seek to fixed moments and read the figure at each.
            const samples = [0, 0.25, 0.5, 0.75, 1].map((f) => {
              clock.currentTime = ms * f;
              cell.ppCount.draw();
              return { at: f, shown: cell.textContent };
            });

            const shown = samples.map((s) => Number(s.shown));

            check('the resting frame is the engine number exactly',
                  samples[samples.length - 1].shown === '20',
                  `at t=1 showed ${samples[samples.length - 1].shown}`);

            check('no seeked frame invents a figure outside the two answers',
                  shown.every((n) => n >= 10 && n <= 20),
                  samples.map((s) => `t=${s.at}:${s.shown}`).join('  '));

            check('the figure actually moves between the two ends',
                  shown[0] !== shown[shown.length - 1],
                  `t=0 showed ${samples[0].shown}, t=1 showed ${samples[samples.length - 1].shown}`);

            check('the count never runs backwards',
                  shown.every((n, i) => i === 0 || n >= shown[i - 1]),
                  shown.join(' -> '));
          }

          // An interrupted count must land on the newest answer, not the one it was heading for.
          // **The second call is made in both modes.** An earlier version made it only when
          // animating, then asserted its result in both — so reduced motion failed a check about
          // an interruption that had not been performed. The harness's own asymmetry, and the
          // reason every branch here states what it does rather than skipping silently.
          const busy = document.createElement('span');
          document.body.appendChild(busy);
          window.ppCount(busy, 0, 100);

          if (!reduced) {
            busy.ppCount.clock.currentTime = enterMs * 0.4;
            busy.ppCount.draw();
          }

          window.ppCount(busy, Number(busy.textContent), 42);

          if (!reduced) {
            busy.ppCount.clock.currentTime = enterMs;
            busy.ppCount.draw();
          }

          check('an interrupted count lands on the newest answer',
                busy.textContent === '42',
                `reduced=${reduced} landed on ${busy.textContent}`);

          // **One live clock on the figure, and this is what the interrupt check was missing.**
          // Seeking and calling `draw()` bypasses the rAF pump entirely, so deleting the
          // `cancel()` in `ppCount` passed every check here — while in a real browser the
          // abandoned count kept pumping and wrote its own final frame *after* the newer count
          // had settled. The strip came to rest on a Hero Point figure the engine no longer
          // returns, which is the shape CLAUDE.md forbids in as many words. `getAnimations()`
          // sees the abandoned clock without needing a single frame to run.
          check('an interrupted count leaves one live clock, not two',
                reduced ? busy.getAnimations().length === 0 : busy.getAnimations().length === 1,
                `reduced=${reduced} live clocks on the figure: ${busy.getAnimations().length}`);

          // **An interrupt that takes an early return.** The check above uses 78 -> 42, which
          // never hits one — so moving the cancel *below* the early returns passed it, while
          // `HpBudgetBar` produces the early-return case routinely: `OnAfterRenderAsync` calls
          // with `from === to` whenever a render changes nothing about the spend. With an older
          // count still in flight, that path assigned the answer and left the old count pumping
          // over the top of it.
          const early = document.createElement('span');
          document.body.appendChild(early);
          window.ppCount(early, 0, 100);

          if (!reduced) { early.ppCount.clock.currentTime = enterMs * 0.4; early.ppCount.draw(); }

          // Same value both ends: the immediate path.
          window.ppCount(early, 100, 100);

          check('an interrupt taking the immediate path still stops the old count',
                early.textContent === '100' && early.getAnimations().length === 0,
                `shows ${early.textContent}, live clocks ${early.getAnimations().length}`);

          // A completed count leaves none. `fill: "forwards"` keeps a finished animation
          // relevant, so without an explicit cancel they accumulated one per count on the one
          // element in the budget strip — measured growing 1..10 over ten counts.
          const tidy = document.createElement('span');
          document.body.appendChild(tidy);
          window.ppCount(tidy, 1, 2);
          if (!reduced) {
            tidy.ppCount.clock.currentTime = enterMs;
            tidy.ppCount.clock.finish();
          }

          // The tidy-up hangs off `clock.finished`, which settles as a microtask — checking
          // straight after `finish()` reads the animation before its own promise has run.
          await settle();
          check('a finished count leaves no animation behind',
                tidy.getAnimations().length === 0,
                `live clocks after finishing: ${tidy.getAnimations().length}`);

          // **A frame that arrives after the tidy-up must not undo the answer.** This is the
          // regression the previous fix shipped: `clock.cancel()` leaves `currentTime` null, the
          // old code read that as `t = 0` through `?? 0`, and a pump frame still scheduled at
          // completion wrote `from` back over the figure and rescheduled itself for ever. The
          // strip came to rest on the *previous* Hero Point total.
          //
          // It was invisible to everything here because `--virtual-time-budget` produces no
          // frames, so no pump was ever pending. Capturing `draw` and calling it after the
          // tidy-up reproduces that frame exactly, with no frames required.
          const late = document.createElement('span');
          document.body.appendChild(late);
          window.ppCount(late, 40, 60);

          if (reduced) {
            check('a frame arriving after the tidy-up cannot undo the answer',
                  late.textContent === '60',
                  `reduced=${reduced} shows ${late.textContent}`);
          } else {
            const leftover = late.ppCount;
            leftover.clock.currentTime = enterMs;
            leftover.clock.finish();
            await settle();

            const settled = late.textContent;
            leftover.draw();

            check('a frame arriving after the tidy-up cannot undo the answer',
                  settled === '60' && late.textContent === '60',
                  `settled on ${settled}, then a late frame showed ${late.textContent}`);
          }

          // **Overlapping transitions.** `begin()` while one is open must release the first, or
          // its snapshot stays on screen for ever — the failsafe cannot help, because `guard` is
          // a module singleton the second `begin()` overwrote. Two navigations inside 260ms is
          // an ordinary click-through.
          released.length = 0;
          window.ppMotion.begin();
          window.ppMotion.begin();
          window.ppMotion.end();
          await settle();
          check('a second transition releases the first',
                reduced ? released.length === 0 : released.length === 2,
                `reduced=${reduced} released ${released.length} of 2 opened`);

          // **The release must come after the snapshot, not with it.** Moving `end()` below
          // `startViewTransition` inside `begin()` releases every transition before Blazor has
          // rendered the new page, so nothing ever animates — and checks 2, 3 and the overlap
          // check are all satisfied by a release that happened too early. Holding the promise
          // open across a `begin()` is the property; this asserts it directly.
          released.length = 0;
          window.ppMotion.begin();

          // **Settle first.** The release resolves a promise, so reading the counter
          // synchronously after begin() reports 0 whether or not begin() released it — which is
          // how the first version of this check passed the very variant it was written for.
          await settle();
          const releasedWhileOpen = released.length;
          window.ppMotion.end();
          await settle();

          check('a transition stays open until end() is called',
                reduced
                  ? releasedWhileOpen === 0 && released.length === 0
                  : releasedWhileOpen === 0 && released.length === 1,
                `reduced=${reduced} released ${releasedWhileOpen} before end(), ` +
                `${released.length} after`);


          // ── A row landing in a list ──────────────────────────────────────────────────
          //
          // Identity is a `data-landed` mark in the script, not a key in the component, so what
          // is tested here is exactly what decides it in the app.
          const list = document.createElement('ul');
          list.innerHTML = '<li>one</li><li>two</li>';
          document.body.appendChild(list);

          // First render: mark, do not animate. Restoring a saved character must not play a
          // dozen arrivals at once — but the rows must still be marked, or the next genuine
          // addition lands the whole list with it.
          const landingsBefore = window.ppMotionStats.landings;
          window.ppLand(list, false);

          check('a first render marks rows without landing them',
                window.ppMotionStats.landings === landingsBefore &&
                  list.querySelectorAll('li[data-landed]').length === 2,
                `landings ${landingsBefore} -> ${window.ppMotionStats.landings}, ` +
                `marked ${list.querySelectorAll('li[data-landed]').length}/2`);

          // A row arrives. Only that one may animate.
          const fresh = document.createElement('li');
          fresh.textContent = 'three';
          list.appendChild(fresh);
          window.ppLand(list, true);

          // **getAnimations() is the positive control**: it asks the browser what is actually
          // running, rather than trusting a counter this file also owns.
          const running = fresh.getAnimations().length;
          const others = [...list.querySelectorAll('li')]
            .filter((li) => li !== fresh)
            .reduce((n, li) => n + li.getAnimations().length, 0);

          check('an arriving row lands (positive control)',
                reduced ? running === 0 : running === 1,
                `reduced=${reduced} animations on the new row: ${running}`);

          check('rows already present do not land again',
                others === 0,
                `animations on the two pre-existing rows: ${others}`);

          check('every row is marked once landing has run',
                list.querySelectorAll('li[data-landed]').length === 3,
                `${list.querySelectorAll('li[data-landed]').length}/3 marked`);

          if (!reduced && running === 1) {
            // The curve is the stylesheet's. A hard-coded easing here would drift from the token
            // and no CSS test could see it, because the animation is built in script.
            const eased = fresh.getAnimations()[0].effect.getTiming().easing;
            const token = window.getComputedStyle(document.documentElement)
              .getPropertyValue('--ease-emphasised').trim();
            check('the landing uses --ease-emphasised from the stylesheet',
                  eased === token && token.length > 0,
                  `effect easing "${eased}" vs token "${token}"`);
          }


          const ok = checks.every((c) => c.ok);
          document.title = ok ? 'MOTION: PASS' : 'MOTION: FAIL';
          box.className = ok ? '' : 'bad';
          box.textContent =
            (ok ? 'MOTION: PASS' : 'MOTION: FAIL') +
            ` (prefers-reduced-motion: ${reduced ? 'reduce' : 'no-preference'})\n` +
            checks.map((c) => `  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name} — ${c.detail}`).join('\n');
        })();
        </script>
        </body>
        </html>
        """;


    /// <summary>
    /// The sticky harness, as a string. Hand-written rather than rendered: what it proves is a
    /// fact about layout in a browser, and there is no component whose markup would express it.
    ///
    /// <para>It reports the numbers as well as the verdict. The bug this guards against is a strip
    /// that scrolls away — a difference of a few hundred pixels — but the failure mode next door is
    /// a strip that sticks to the wrong offset, and a bare yes/no cannot tell those apart.</para>
    /// </summary>
    private static string StickyHarness() =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheStickyStrip. Do not edit: it is rewritten on every
             PP_PROOF run. Iframes the real shell proof so what is measured is what ships. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — does the budget strip stick?</title>
          <style>
            html, body { margin: 0; background: #888; font: 14px monospace }
            iframe { width: 1200px; height: 700px; border: 0; display: block }
            #verdict { position: fixed; top: 0; right: 0; z-index: 9; padding: 6px 10px;
                       background: #000; color: #0f0; white-space: pre }
            #verdict.bad { background: #900; color: #fff }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <iframe id="f" src="proof-shell-hero.html"></iframe>
        <script>
        document.getElementById('f').addEventListener('load', () => {
          const box = document.getElementById('verdict');
          const say = (ok, detail) => {
            box.textContent = (ok ? 'STICKY: PASS' : 'STICKY: FAIL') + '\n' + detail;
            box.className = ok ? '' : 'bad';
            // Also the title, which is where the verdict is read from. The <div> is not enough on
            // its own: both verdict strings appear in this script's own source, so a dumped DOM
            // contains "STICKY: PASS" whether or not the script ever ran. The title is written
            // only here, and the resting value below is neither verdict — so it tells a pass,
            // a failure and a harness that never fired apart.
            document.title = ok ? 'STICKY: PASS' : 'STICKY: FAIL';
          };
          try {
            const w = document.getElementById('f').contentWindow;
            const d = w.document;
            const strip = d.querySelector('.budget');
            const steps = d.querySelector('.steps');
            if (!strip) { say(false, 'no .budget in the shell proof'); return; }
            if (!steps) { say(false, 'no .steps in the shell proof'); return; }

            const before = strip.getBoundingClientRect().top;
            w.scrollTo(0, 600);
            // Layout is synchronous after scrollTo, so the rect below is post-scroll.
            const after = strip.getBoundingClientRect().top;
            const stepsAfter = steps.getBoundingClientRect().bottom;

            // Stuck at the top, and the band above it genuinely scrolled away — without the
            // second half, a page that simply did not scroll would report a perfect pass.
            // **Positive controls.** An empty iframe scrolls nowhere and sticks nothing, and a
            // page that never scrolled reports a strip perfectly at rest — both read as a clean
            // pass. So: the shell rendered, and the window actually moved.
            const rendered = !!d.querySelector('.banner') && !!d.querySelector('.shell');
            const moved = w.scrollY > 0;
            // **`before` is in the verdict, and leaving it out let `fixed` pass as `sticky`.**
            // A fixed strip sits at the top from the start, so it reports `before 0.0` and
            // `after 0.0` — indistinguishable from a working sticky strip on the `after`
            // measurement alone, which is all this used to check. A strip that begins below the
            // banner is what sticky means; one that never moved was never sticky.
            const startedBelow = before > 20;

            const stuck = Math.abs(after) < 1.5;
            const scrolled = stepsAfter < 0;
            say(rendered && moved && startedBelow && stuck && scrolled,
              `scrollY ${w.scrollY}\n` +
              `.budget top: before ${before.toFixed(1)}, after ${after.toFixed(1)} (want ~0)\n` +
              `.steps bottom after: ${stepsAfter.toFixed(1)} (want negative — scrolled away)`);
          } catch (e) {
            say(false, 'blocked: ' + e.message);
          }
        });
        </script>
        </body>
        </html>
        """;

    /// <summary>
    /// The narrow viewport, measured rather than eyeballed — one harness per page that has to
    /// survive 375px.
    ///
    /// <para><b>Do not test this with <c>--window-size=375</c>.</b> Headless Chrome clamps its
    /// window to about 485px, so a "375-wide" screenshot is a 485px render cropped: it looks like
    /// catastrophic overflow and is not, and a reviewer has already nearly filed that. An iframe
    /// is genuinely 375 wide.</para>
    ///
    /// <para><b>And the measurement is printed, because the eye cannot see it.</b> The bug this
    /// exists for was 8px of horizontal overflow — invisible in a screenshot and unmistakable as
    /// <c>clientWidth 360, scrollWidth 368</c>.</para>
    /// </summary>
    [Fact]
    public void TheNarrowViewport()
    {
        if (!Asked) return;

        WritePage("proof-narrow.html", "hero", NarrowHarness("proof-hero.html"));
        WritePage("proof-narrow-shell.html", "hero", NarrowHarness("proof-shell-hero.html"));
    }

    /// <summary>
    /// A 375px-wide iframe onto <paramref name="target"/>, reporting every element that overflows
    /// it. <b>The verdict is the widest offender, not a yes/no</b>: "something overflows" sends the
    /// next session hunting, and the element's own selector ends the hunt.
    /// </summary>
    private static string NarrowHarness(string target) =>
        $$"""
        <!doctype html>
        <!-- Generated by ProofPages.TheNarrowViewport. Do not edit: rewritten on every PP_PROOF
             run. 375px is a real iframe width, not a Chrome window size — headless clamps the
             window to ~485px and a cropped render reads as overflow that is not there. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — 375px</title>
          <style>
            html, body { margin: 0; background: #222; color: #eee; font: 13px monospace }
            iframe { width: 375px; height: 900px; border: 0; display: block; background: #fff }
            #verdict { position: fixed; top: 0; right: 0; z-index: 9; padding: 6px 10px;
                       background: #000; color: #0f0; white-space: pre; max-width: 60vw }
            #verdict.bad { background: #900; color: #fff }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <iframe id="f" src="{{target}}"></iframe>
        <script>
        document.getElementById('f').addEventListener('load', () => {
          const box = document.getElementById('verdict');
          const say = (ok, detail) => {
            document.title = ok ? 'NARROW: PASS' : 'NARROW: FAIL';
            box.textContent = (ok ? 'NARROW: PASS' : 'NARROW: FAIL') + '\n' + detail;
            box.className = ok ? '' : 'bad';
          };
          try {
            const d = document.getElementById('f').contentDocument;
            const root = d.documentElement;

            // The page itself. A document that scrolls sideways at 375 is the whole bug.
            const client = root.clientWidth;
            const scroll = root.scrollWidth;

            // And which element did it, since "the page is 8px wide" names no culprit. Anything
            // whose right edge lies beyond the viewport is reported, widest first.
            const over = [...d.querySelectorAll('*')]
              .map((el) => ({ el, right: el.getBoundingClientRect().right }))
              .filter((x) => x.right > client + 0.5)
              .sort((a, b) => b.right - a.right)
              .slice(0, 5)
              .map((x) => `    ${x.el.tagName.toLowerCase()}` +
                          `${x.el.className ? '.' + String(x.el.className).split(' ').join('.') : ''}` +
                          ` right ${x.right.toFixed(1)}`);

            // **Positive control, and this harness needs one most of all.** An iframe that failed
            // to load has clientWidth === scrollWidth === the frame width, which is a clean PASS
            // reporting that nothing overflows an empty document. So the target must have
            // actually rendered, and the frame must actually be narrow.
            const rendered = d.querySelectorAll('*').length;
            const narrow = client > 300 && client < 400;

            // **Both directions.** Content overflowing to the *left* in LTR does not grow
            // `scrollWidth` and *reduces* an element's `right`, so the two measurements above
            // move the wrong way and a figure dragged off the left edge reported "nothing
            // overflows" with every 375px guard green. Demonstrated with a negative margin on
            // the budget figure: the Hero Point total sat entirely off-screen.
            const off = [...d.querySelectorAll('*')]
              .map((el) => ({ el, box: el.getBoundingClientRect() }))
              .filter((x) => x.box.width > 0 && x.box.left < -0.5)
              .sort((a, b) => a.box.left - b.box.left)
              .slice(0, 5)
              .map((x) => `    ${x.el.tagName.toLowerCase()}` +
                          `${x.el.className ? '.' + String(x.el.className).split(' ').join('.') : ''}` +
                          ` left ${x.box.left.toFixed(1)}`);

            // **`over` is in the verdict, and leaving it out made this incapable of failing.**
            // `documentElement.scrollWidth` is decoupled from real overflow by any clipping
            // ancestor, so `overflow-x: clip` on the shell — the commonest wrong fix for
            // horizontal overflow — hides 350px of unreachable content behind a document that
            // reports no scroll at all. The offenders were being computed, sorted, printed and
            // then ignored. WCAG 1.4.10 is about content you cannot reach, not about a
            // scrollbar.
            // **`::before` and `::after` too, and this is not a corner case.** `querySelectorAll`
            // returns no pseudo-elements, so a generated box pushed off the viewport is invisible
            // to the sweep above — and leftward overflow does not grow `scrollWidth` either, so
            // nothing else notices. A 300px `::before` at `left: -320px` sat entirely off a 375px
            // screen with both narrow harnesses green. Range boxes are what a pseudo-element can
            // be measured with; the element's own box does not include it.
            const generated = [];
            for (const el of d.querySelectorAll('*')) {
              for (const which of ['::before', '::after']) {
                const gen = d.defaultView.getComputedStyle(el, which);
                if (!gen || gen.content === 'none' || gen.content === '' ) continue;
                if (gen.position !== 'absolute' && gen.position !== 'fixed') continue;

                const l = parseFloat(gen.left);
                const wide = parseFloat(gen.width);
                if (Number.isNaN(l) || Number.isNaN(wide)) continue;

                const box = el.getBoundingClientRect();
                const from = box.left + l;
                if (from < -0.5 || from + wide > client + 0.5) {
                  generated.push(`    ${el.tagName.toLowerCase()}${which} spans ` +
                                 `${from.toFixed(1)}..${(from + wide).toFixed(1)}`);
                }
              }
            }

            say(rendered > 20 && narrow && over.length === 0 && off.length === 0 && generated.length === 0 && scroll <= client + 0.5,
              `target {{target}}  elements ${rendered}\nclientWidth ${client}  scrollWidth ${scroll}` +
              (generated.length ? `\ngenerated boxes off-screen:\n${generated.join('\n')}` : '') +
              (off.length ? `\noff the left edge:\n${off.join('\n')}` : '') +
              (over.length ? `\noverflowing:\n${over.join('\n')}` : '\nnothing overflows'));
          } catch (e) {
            say(false, 'blocked: ' + e.message);
          }
        });
        </script>
        </body>
        </html>
        """;

    /// <summary>
    /// Box insets, printed. The companion to <see cref="TheNarrowViewport"/> and the same trick:
    /// a 3.2px table misalignment is invisible to the eye and obvious as a pair of numbers.
    /// </summary>
    [Fact]
    public void TheBoxInsets()
    {
        if (!Asked) return;

        WritePage("proof-measure.html", "hero", MeasureHarness());
    }

    /// <summary>
    /// Reports the left and right edge of each band and of the shell, so the four are known to
    /// agree rather than believed to. <b>The chrome bands each centre their own contents on
    /// <c>--column</c>; a band that quietly stopped would look almost right.</b>
    /// </summary>
    private static string MeasureHarness() =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheBoxInsets. Do not edit: rewritten on every PP_PROOF run. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — box insets</title>
          <style>
            html, body { margin: 0; background: #222; color: #eee; font: 13px monospace }
            iframe { width: 1200px; height: 700px; border: 0; display: block; background: #fff }
            #verdict { position: fixed; top: 0; right: 0; z-index: 9; padding: 6px 10px;
                       background: #000; color: #0f0; white-space: pre }
            #verdict.bad { background: #900; color: #fff }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <iframe id="f" src="proof-shell-hero.html"></iframe>
        <script>
        document.getElementById('f').addEventListener('load', () => {
          const box = document.getElementById('verdict');
          const say = (ok, detail) => {
            document.title = ok ? 'INSETS: PASS' : 'INSETS: FAIL';
            box.textContent = (ok ? 'INSETS: PASS' : 'INSETS: FAIL') + '\n' + detail;
            box.className = ok ? '' : 'bad';
          };
          try {
            const d = document.getElementById('f').contentDocument;

            // The inner element of each band, which is what actually sits on the column.
            const parts = {
              banner: '.banner-inner',
              steps: '.steps-list',
              budget: '.budget-strip',
              shell: '.shell',
            };

            const rows = [];
            const lefts = [];
            const rights = [];
            for (const [name, sel] of Object.entries(parts)) {
              const el = d.querySelector(sel);
              if (!el) { rows.push(`    ${name}: ${sel} NOT FOUND`); continue; }
              // **Two different sources, because one measurement cannot catch both faults.**
              // The left edge is where the contents actually begin, so it is read off the first
              // child: a margin there moves the text 96px while the band's own box and padding
              // are untouched, and measuring the band alone reported a spread of 0.00. The right
              // edge cannot come from the first child — that is just one element's width — so it
              // is the band's padding box, which is what a padding-right change moves.
              const r = el.getBoundingClientRect();
              const pad = window.getComputedStyle(el);
              const inner = el.firstElementChild;
              const innerBox = inner ? inner.getBoundingClientRect() : null;

              const left = innerBox && innerBox.width > 0
                ? innerBox.left
                : r.left + parseFloat(pad.paddingLeft || '0');
              const right = r.right - parseFloat(pad.paddingRight || '0');

              lefts.push(left);
              rights.push(right);
              rows.push(`    ${name.padEnd(7)} content ${left.toFixed(1)} .. ${right.toFixed(1)}`);
            }

            // **Both edges, because asserting only the left let one band run 96px short.**
            // A shared column means the contents start *and* end together; a padding-right on
            // one band moves nothing this used to read, while the row underneath printed the
            // discrepancy in plain sight.
            const spread = lefts.length ? Math.max(...lefts) - Math.min(...lefts) : 999;
            const rightSpread = rights.length ? Math.max(...rights) - Math.min(...rights) : 999;

            // Positive control: the bands were actually found and measured. A spread over an
            // empty list is 999 and fails, but over a *single* found band it is 0 and passes —
            // so the count is asserted, not inferred from the spread.
            const found = lefts.length === Object.keys(parts).length;

            say(found && spread < 0.5 && rightSpread < 0.5,
              `${rows.join('\n')}\n    spread ${spread.toFixed(2)}px left, ` +
              `${rightSpread.toFixed(2)}px right (want < 0.5)`);
          } catch (e) {
            say(false, 'blocked: ' + e.message);
          }
        });
        </script>
        </body>
        </html>
        """;

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
        // The sticky harness: both measurements it takes, both verdicts it can reach, and the
        // resting text that is neither — so a script that never ran is distinguishable from a
        // pass. `scrollTo` is what makes it a test of stickiness rather than of position.
        ["proof-sticky.html"] =
        [
            ".budget", ".steps", "getBoundingClientRect", "scrollTo",
            "STICKY: PASS", "STICKY: FAIL", "measuring",
            "stuck && scrolled", "document.title",
        ],
        // The motion harness. `js/motion.js` is the load-bearing one: a harness that reproduced
        // the script inline would pass while the shipped file did anything at all, so
        // `MustNotShow` refuses a second definition of `ppMotion` here.
        ["proof-motion.html"] =
        [
            "js/motion.js", "prefers-reduced-motion", "startViewTransition",
            "MOTION: PASS", "MOTION: FAIL", "measuring",
            "checks.every", "document.title",
            // And the harness precondition. Without theme.css linked the token is unreadable
            // and every counting check passes without a count running, which is how it shipped.
            "css/theme.css", "--enter resolves",
            // The counting half, and the positive controls. `ppMotionStats` is what makes a
            // check of the outcome mean anything: twice a check passed because the feature had
            // not run at all.
            "ppCount", "ppMotionStats", "positive control",
            "currentTime", "resting frame is the engine number",
            "ppLand", "getAnimations", "ease-emphasised",
        ],
        // The 375px harnesses. `clientWidth`/`scrollWidth` is the measurement; naming the widest
        // overflowing element is what turns a failure into a fix.
        ["proof-narrow.html"] =
        [
            "375px", "clientWidth", "scrollWidth", "getBoundingClientRect",
            "NARROW: PASS", "NARROW: FAIL", "measuring", "document.title",
        ],
        ["proof-narrow-shell.html"] =
        [
            "375px", "clientWidth", "scrollWidth", "getBoundingClientRect",
            "NARROW: PASS", "NARROW: FAIL", "measuring", "document.title",
        ],
        // The insets harness: all four bands, and the spread between them.
        ["proof-measure.html"] =
        [
            ".banner-inner", ".steps-list", ".budget-strip", ".shell",
            "getBoundingClientRect", "spread",
            "INSETS: PASS", "INSETS: FAIL", "measuring", "document.title",
        ],
        // The opening chord. `ppPaletteStats` is the positive control — every check here is
        // satisfied by a listener that was never registered, so the counter is asserted before
        // anything that depends on it. The negative cases are named because without them a
        // listener that fires on every key passes the lot.
        ["proof-shortcut.html"] =
        [
            "js/palette.js", "ppPalette.listen", "ppPaletteStats", "positive control",
            "KeyboardEvent", "ctrlKey", "metaKey", "defaultPrevented",
            "a bare k and a Ctrl-J reach nothing",
            "activeElement", "keyboard trap",
            "SHORTCUT: PASS", "SHORTCUT: FAIL", "measuring", "document.title",
        ],
        // The light/dark preference, and the only check anywhere that it survives a reload.
        // `ppThemeStats` is the positive control twice over: the read-back half re-executes the
        // module, and a second execution that never happened would leave every assertion about
        // the stamped attribute holding for the wrong reason. The key is named because "it is
        // stored somewhere" is not the property — "it is stored where the app looks" is.
        ["proof-theme.html"] =
        [
            "js/theme.js", "ppTheme.set", "ppThemeStats", "positive control",
            "pp.theme.v1", "localStorage", "data-theme",
            "removes the value rather than storing a third word",
            "with nothing calling it",
            "THEME: PASS", "THEME: FAIL", "measuring", "document.title",
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

        // ...and the theme reaches it too, in both directions. The dark page has to *say* dark
        // and the light page has to say nothing at all: an attribute stamped on the default page
        // would take it off `prefers-color-scheme` and pin it, which is a different palette from
        // the one the app ships.
        var dark = Page(ShellPage(mode, "dark"), Name(mode), ShellBody(ctx), wrap: false, "dark");
        AssertMarkers($"proof-shell-{Name(mode)}.html", dark);

        Assert.Contains("data-theme=\"dark\"", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme=", shell, StringComparison.Ordinal);
        Assert.NotEqual(shell, dark);

        using var fresh = new RenderContext();
        fresh.Session.Sheet.SelectedTierId = "standard";

        var empty = Page("proof-empty.html", "hero", EmptyBody(fresh), wrap: true);
        AssertMarkers("proof-empty.html", empty);
    }

    /// <summary>
    /// <b>The rulebook entry the accounts proof captions as opened is actually open.</b>
    ///
    /// <para>Found by mutation, and the reason it is worth its own test is that nothing caught it:
    /// deleting the click from <see cref="TheAccountSurfaces"/> left the whole suite green while the
    /// page captioned "…and opened" carried a shut entry. A reader would have taken it as evidence
    /// that the open state had been looked at. That is the exact shape this file already documents
    /// twice — a proof whose assertions hold trivially because the thing being proved never
    /// happened.</para>
    ///
    /// <para><b>The positive control comes first.</b> The shut render is asserted to be shut before
    /// the open one is asserted to be open, because a component that rendered no toggle at all
    /// would otherwise satisfy a check for "not expanded" without ever having been openable — and
    /// a signed-out visitor legitimately gets no toggle, so its absence is a real state rather than
    /// an impossible one.</para>
    /// </summary>
    [Fact]
    public async Task TheOpenedRulebookEntryOnTheProofIsOpen()
    {
        await using var ctx = new RenderContext();
        ctx.Api.SignedIn = ("acct-7", "player");
        ctx.With(SheetMode.Hero);
        ctx.Api.Book["Armor"] = "Self • Half Toughness • 1 Hero Point per rank\nThe book's own words.";

        var armor = ctx.Services.GetRequiredService<RulesRepository>().Powers.Single(p => p.Id == "armor");

        var shut = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));
        Assert.Equal("false", shut.Find(".book-toggle").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("The book's own words.", shut.Markup, StringComparison.Ordinal);

        var opened = ctx.Render<PowerEditor>(p => p.Add(e => e.Power, armor));
        await opened.Find(".book-toggle").ClickAsync();

        Assert.Equal("true", opened.Find(".book-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("The book's own words.", opened.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sticky harness measures rather than asserts — checked on every run, not only under
    /// <c>PP_PROOF</c>.
    ///
    /// <para><b>A proof that cannot fail is the dangerous shape</b>, because it is read as evidence.
    /// This pins the two measurements it takes, both verdict tokens, and the resting text that is
    /// neither — and <see cref="MustNotShow"/> refuses a hard-coded pass.</para>
    ///
    /// <para>It cannot check that the strip <em>is</em> stuck: that needs a browser, and driving one
    /// is the <c>--dump-dom</c> step in <see cref="TheStickyStrip"/>. What it checks is that the
    /// harness would notice.</para>
    /// </summary>
    [Fact]
    public void TheStickyHarnessMeasuresRatherThanAsserts()
    {
        AssertMarkers("proof-sticky.html", StickyHarness());
    }

    /// <summary>
    /// The motion harness drives the shipped script and can reach a failing verdict.
    ///
    /// <para>The behavioural half needs a browser — that is <see cref="TheMotionScript"/>'s
    /// <c>--dump-dom</c> step, run twice. What runs here is the part that can be checked without
    /// one: that the harness loads <c>js/motion.js</c> rather than a copy of it, asks the media
    /// query, and computes its verdict from the checks instead of announcing one.</para>
    /// </summary>
    [Fact]
    public void TheMotionHarnessDrivesTheShippedScript()
    {
        AssertMarkers("proof-motion.html", MotionHarness());
        AssertMarkers("proof-shortcut.html", ShortcutHarness());
        AssertMarkers("proof-theme.html", ThemeHarness());
    }

    /// <summary>
    /// The narrow-viewport and inset harnesses measure rather than assert, and each is pointed at
    /// the page it claims to be about.
    ///
    /// <para><c>NarrowHarness</c> is parameterised, so the two pages it writes could both point at
    /// the same target and every marker would still pass — which is the mode-copy mistake this
    /// file already records once, in another spelling. The target is asserted per file.</para>
    /// </summary>
    [Fact]
    public void TheMeasuringHarnessesArePointedAtTheirOwnSubject()
    {
        var narrow = NarrowHarness("proof-hero.html");
        AssertMarkers("proof-narrow.html", narrow);
        Assert.Contains("src=\"proof-hero.html\"", narrow, StringComparison.Ordinal);

        var shell = NarrowHarness("proof-shell-hero.html");
        AssertMarkers("proof-narrow-shell.html", shell);
        Assert.Contains("src=\"proof-shell-hero.html\"", shell, StringComparison.Ordinal);

        AssertMarkers("proof-measure.html", MeasureHarness());
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
        // A harness that always passes is worse than no harness. The verdict has to be computed
        // from the two measurements, so a hard-coded `say(true, …)` is refused here.
        ["proof-sticky.html"] = ["say(true"],
        // A harness that defines its own ppMotion is proofing itself, not the shipped script.
        ["proof-motion.html"] = ["window.ppMotion = {"],
    };

    /// <summary>
    /// The whole page, as a string. Separate from writing it so the marker test can assert on
    /// exactly what would be written without writing anything.
    /// </summary>
    /// <summary>
    /// One proof page: the real stylesheets, the document element dressed as the app dresses it,
    /// and a body of rendered markup.
    ///
    /// <para><c>theme</c> takes <c>"dark"</c> or <c>"light"</c> to stamp an explicit choice, and
    /// <b>null for the default — which is the state that has no attribute at all</b> and follows
    /// <c>prefers-color-scheme</c>. Stamping "system" would be a fourth value the stylesheet does
    /// not model, matching neither the light path nor <c>:not([data-theme="light"])</c>.</para>
    /// </summary>
    private static string Page(string file, string mode, string body, bool wrap, string? theme = null)
    {
        _ = file;   // kept in the signature so a caller cannot pass a body for the wrong page

        // **`<div id="app">`, because that is what index.html ships.** Blazor renders the whole
        // layout inside it; these pages used to write the markup straight into `<body>`, so every
        // ancestor-borne fault was invisible to them — and the sticky harness's own docstring
        // names that category ("anything that wraps it, and any `transform`, `filter` or
        // `contain` on an ancestor"). `#app { overflow-x: hidden }` unsticks the budget strip in
        // the real app and the proof reported it pinned, because the proof had no `#app`.
        var inner = wrap ? $"<div class=\"shell\">{body}</div>" : body;
        inner = $"<div id=\"app\">{inner}</div>";

        var chosen = theme is null ? "" : $" data-theme=\"{theme}\"";

        return $"""
            <!doctype html>
            <html lang="en" data-mode="{mode}"{chosen}>
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Proof — {mode}{(theme is null ? "" : $", {theme}")}</title>
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
