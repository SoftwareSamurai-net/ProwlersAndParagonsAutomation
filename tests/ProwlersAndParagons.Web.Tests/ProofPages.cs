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

    /// <summary>
    /// The banner's settings menu, open, with both palette switches in it.
    ///
    /// <para>Written to be looked at, and for the one reason a rendering test cannot cover it: the
    /// menu is absolutely positioned and hangs out of the band, and <c>view-transition-name</c> on
    /// <c>.banner</c> creates a stacking context — so a <c>z-index</c> on the menu is resolved
    /// <em>inside</em> the banner, which is a static earlier sibling of <c>.steps</c>. The
    /// character switcher shipped exactly that bug and it was found in a screenshot: a menu painted
    /// behind the step band, visible, unusable, reading as a control that does nothing. The markup
    /// is identical either way.</para>
    ///
    /// <para><b>This one is right-anchored where the switcher's is left-anchored</b>, which is a
    /// second thing only a picture answers: it sits at the right end of the bar, so a menu growing
    /// rightwards would leave the window.</para>
    /// </summary>
    [Fact]
    public void TheSettingsMenu()
    {
        if (!Asked) return;

        WriteRaw("proof-settings.html", "hero", DisclosedSettingsBody());
    }

    /// <summary>
    /// The shell with its settings menu open. Shared with
    /// <see cref="TheSettingsMenuOnTheProofIsOpen"/>, so what is asserted is the markup that is
    /// actually written rather than a second render that happens to agree with it.
    /// </summary>
    private static string DisclosedSettingsBody()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("build/characteristics");

        var shell = ctx.Render<MainLayout>();
        shell.Find(".settings-open").Click();

        return shell.Markup;
    }

    /// <summary>
    /// <b>The menu the settings proof captions as open is actually open.</b>
    ///
    /// <para>Same trap as <see cref="TheOpenedRulebookEntryOnTheProofIsOpen"/>, and it has caught a
    /// real instance here before: deleting the click left a page showing a shut band that a reader
    /// would have taken as evidence the open state had been looked at. Checked on every run, not
    /// only under <c>PP_PROOF</c>, because the writing is what is conditional and the markers are
    /// not.</para>
    ///
    /// <para><b>The shut state is asserted first</b>, so "open" is a state the page reached rather
    /// than one it was always in.</para>
    /// </summary>
    [Fact]
    public void TheSettingsMenuOnTheProofIsOpen()
    {
        using var shut = new RenderContext().With(SheetMode.Hero);
        Assert.Empty(shut.Render<MainLayout>().FindAll(".settings-menu-list"));

        AssertMarkers("proof-settings.html", DisclosedSettingsBody());
    }

    /// <summary>
    /// The banner's character switcher, open, with somebody else to switch to.
    ///
    /// <para>Written to be looked at. The control hangs out of the banner, which is the one thing
    /// about it a rendering test cannot see — a disclosure clipped by its own band looks like a
    /// button that does nothing.</para>
    /// </summary>
    [Fact]
    public void TheCharacterSwitcher()
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(SheetMode.Hero);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("build/characteristics");
        ctx.Session.RestoreBeforeFirstRender(SampleCharacters.Hero(), SheetMode.Hero);

        var shell = ctx.Render<MainLayout>();
        shell.Find(".character-switch-name").Click();

        WriteRaw("proof-switcher.html", "hero", shell.Markup);
    }

    /// <summary>
    /// The GM review step's findings, each naming the step that caused it.
    ///
    /// <para><b>Written to be looked at, and deliberately not added to the pixel manifest.</b> The
    /// findings list is the one screen in the app whose content is a *list of things that are
    /// wrong*, so a golden for it would be a golden for whatever character the fixture happens to
    /// break — a page whose content depends on the fixture is the trap
    /// <c>docs/guide/testing.md</c> records three proof pages falling into. What holds this screen
    /// is <c>FindingRouteTests</c>, which renders it and reads it; this exists so somebody can see
    /// the link sitting beside the message rather than infer it from markup.</para>
    /// </summary>
    [Fact]
    public void TheReviewFindings()
    {
        if (!Asked) return;

        using var ctx = new RenderContext();
        var sheet = ctx.Session.Sheet;
        sheet.SelectedTierId = "standard";
        sheet.SelectedPackageId = "superhero_package";

        foreach (var ability in ctx.Session.Rules.Abilities) sheet.AbilityRanks[ability.Id] = 3;
        foreach (var talent in ctx.Session.Rules.Talents) sheet.TalentRanks[talent.Id] = 3;

        // One of each kind that routes, plus the budget, which deliberately does not.
        sheet.AbilityRanks["might"] = 99;
        sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));
        sheet.Gear.Add(new SelectedGear("A borrowed sword"));

        var body = new StringBuilder();
        Section(body, "Findings, each naming where to go and fix it",
            ctx.Render<Review>().Markup);

        Write("proof-review-findings.html", "hero", body.ToString());
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
    /// The sheet, with one description open.
    ///
    /// <para><b>The tip is opened, because a disclosure proofed shut shows nothing and reads as a
    /// feature that works.</b> That is the mistake this file already warns about for the Power
    /// editor's own book entry — and here there are two things to look at that no assertion can
    /// judge: whether a dotted underline under forty names reads as marking or as noise, and
    /// whether a tip hanging off a word inside a three-column sheet lands somewhere a reader can
    /// read it rather than as a sliver down one column.</para>
    ///
    /// <para><b>It renders `SheetView` rather than a page of its own, and the page is gone.</b>
    /// The explanations are how the sheet renders now, so `/build/sheet` was a second address for
    /// the sheet the review step already draws. What is left worth proofing is the open tip, which
    /// is the half no golden and no assertion can settle.</para>
    ///
    /// <para>Hover cannot be captured, so the tip is shown by focusing the term — which is the
    /// keyboard path and is the one that has to work anyway.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheExplainedSheet(SheetMode mode)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        var page = ctx.Render<SheetView>();

        // The positive control: the names really are terms. A proof of a sheet with no term on it
        // would look exactly like the ordinary sheet and be captioned as this one — and now that
        // the explanations are the default rather than a parameter this method passes, nothing
        // else here would notice them going away.
        Assert.NotEmpty(page.FindAll(".sheet .term-name"));

        // **One description shown, forced open by an inline style rather than by a class.** The
        // stylesheet opens a tip on `:hover` and `:focus-visible`, neither of which a rendered
        // proof can have — and inventing a `.proof-open` rule would put a selector in the shipped
        // stylesheet that exists only for this file. One tip, not forty: the question is whether a
        // tip lands somewhere readable inside a sheet column, and forty overlapping ones answer it
        // for nobody.
        var markup = page.Markup;
        var first = markup.IndexOf("class=\"row-tip", StringComparison.Ordinal);
        Assert.True(first > 0, "no tip in the markup, so this proof would show a sheet and no description.");

        var body = markup[..first]
            + "style=\"display:block\" "
            + markup[first..];

        Write($"proof-explained-{Name(mode)}.html", Name(mode), body);
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
        // **`storesForReal`, because the pointer has to round-trip for this proof to be honest.**
        // bUnit's own interop answers null to every read, so the current-character pointer the
        // account's store writes is never read back — and the panel then draws the character on
        // screen in its own block *and* again in the list below, because it cannot tell that they
        // are the same one. That is a picture of a bug the app does not have.
        await using var holding = new RenderContext(storesForReal: true);
        holding.Api.SignedIn = ("acct-7", "player");
        holding.With(mode);

        var account = holding.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        Section(body, "Your characters — the top of the tier page, where a panel of one red button was",
            holding.Render<CharacterManager>().Markup);

        // **And empty — the state the redesign was actually about.** Nobody's account starts
        // holding characters; this is the panel a first visit meets, and it is where the count
        // used to be printed twice and the file picker sat unstyled and equal in weight to
        // "Start a new character".
        await using var emptyAccount = new RenderContext();
        emptyAccount.Api.SignedIn = ("acct-9", "nobody yet");

        Section(body, "Your characters — nothing built yet, on a signed-in account",
            emptyAccount.Render<CharacterManager>().Markup);

        await using var emptyAnonymous = new RenderContext();

        Section(body, "Your characters — nothing built yet, signed out",
            emptyAnonymous.Render<CharacterManager>().Markup);

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

    /// <summary>
    /// The three surfaces this slice added, which no earlier proof could show: the front door,
    /// the rules reference, and the sheet drawn beside the editors.
    ///
    /// <para><b>Written because a green suite is not a working app.</b> Three visible defects
    /// survived 4,133 tests here and four pieces of developer jargon survived 4,186 — both found
    /// by a person reading the screen. Everything whose substance is appearance or wording has
    /// almost no guard, and these are three whole screens of it.</para>
    /// </summary>
    [Theory]
    [InlineData(SheetMode.Hero)]
    [InlineData(SheetMode.Villain)]
    public void TheNewSurfaces(SheetMode mode)
    {
        if (!Asked) return;

        using var ctx = new RenderContext().With(mode);

        var body = new StringBuilder();

        // The front door with a character already on the sheet, which is the branch carrying a
        // figure. The empty one is proofed below, because they are different pages.
        Section(body, "The front door — carrying on", ctx.Render<Home>().Markup);

        using var fresh = new RenderContext();
        Section(body, "The front door — nothing on the sheet yet", fresh.Render<Home>().Markup);

        Write($"proof-front-door-{Name(mode)}.html", Name(mode), body.ToString());

        // **And the same page in light, because headless Chrome here reports
        // `prefers-color-scheme: dark`** — an un-stamped proof renders the dark palette, so
        // judging a light one needs the attribute set explicitly. Recorded in CLAUDE.md, and
        // the reason a palette fault was once diagnosed off a screenshot of the wrong theme.
        WritePage($"proof-front-door-{Name(mode)}-light.html",
            Name(mode),
            Page($"proof-front-door-{Name(mode)}.html", Name(mode), body.ToString(), wrap: true, theme: "light"));

        // The rules reference, signed in and with a search actually run, because the interesting
        // page is the one with results on it — an unsearched box proves only that a box exists,
        // and a proof of a feature that never ran is the exact shape this file warns about four
        // times over.
        using var reader = new RenderContext().With(mode);
        reader.Api.SignedIn = ("acct-7", "player");

        // **A Power's entry, as flat as the extraction really leaves one, so this proof shows the
        // structure `RulebookProse` finds rather than one short sentence.** The shared stub's
        // passages are all a line long, and a proof of the new stat line and option blocks that
        // opened one of those would be a picture of a feature that never ran — which is the exact
        // failure this file warns about four times over. Added here rather than to `FakeApi`
        // because every other test in the project counts that stub's passages.
        reader.Api.Chapters[0].Passages.Insert(0, new FakeApi.FakePassage(
            "LUCK",
            "Self • Power Rank • 2 Hero Points per rank You are incredibly lucky or so skilled "
            + "that you make everything look easy. You gain a number of Luck dice equal to your "
            + "Luck rank at the start of every issue. Luck dice can be added to either side of "
            + "any challenge roll that involves you. PRO Control (+4): Rather than just being "
            + "lucky, this Power represents your ability to consciously alter probability fields. "
            + "PRO Unbelievable (+1 per rank): You can also spend Luck dice to buy yourself "
            + "lucky breaks, as described in Chapter 5."));

        var page = reader.Render<RulesReference>();

        // **Wait for the page to have finished arriving before touching it, or this proof is a
        // race.** `OnInitializedAsync` fetches the contents, which draws the "What is here" panel
        // at the foot; the search below fetches results and then a passage. Each is a real await,
        // and a proof captured between them is a picture of a page mid-load — which the pixel diff
        // reported as a 4% difference against a golden taken from the same tree, on CI and not
        // here. `Find` waits; `FindAll` does not, and an assertion that happens to pass is not the
        // same as a page that has settled.
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".chosen li")));

        page.Find("#rules-search").Input("luck");
        page.Find("form").Submit();

        // The positive control: if the search did not run, this proof is a picture of an empty
        // box captioned as a page of results. Waited for, not merely asserted — the search is a
        // fetch, and the assertion passing on the first attempt is luck rather than a settled page.
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[aria-expanded]")));

        // ...and one result opened, so the page shows the book's own words rather than only a
        // list of places they might be. The passage is a third fetch; wait for it too.
        page.FindAll("[aria-expanded]")[0].Click();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".book-text")));

        // The second positive control, and the one this proof gained a passage for: the passage
        // that opened is a Power entry, so it has to have come apart into a stat line and its two
        // Pros. Without this the page could be captured showing one unbroken paragraph and the
        // golden would record that as correct.
        page.WaitForAssertion(() =>
        {
            Assert.Equal(3, page.FindAll(".book-stat dd").Count);
            Assert.Equal(2, page.FindAll(".book-options > li").Count);
        });

        var rules = new StringBuilder();
        Section(rules, "Rules reference — searched, with one passage open", page.Markup);
        Write($"proof-rules-{Name(mode)}.html", Name(mode), rules.ToString());

        // The editors beside the sheet. It needs the wide viewport to be two columns at all, so
        // screenshot this one at 1600 wide or it proves the narrow fallback.
        using var building = new RenderContext().With(mode);
        var preview = new StringBuilder();
        Section(preview, "The sheet beside the editors — screenshot this at 1600px or wider",
            building.Render<Characteristics>().Markup);
        Write($"proof-preview-{Name(mode)}.html", Name(mode), preview.ToString());
    }

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
    /// The negative control every verdict harness below is proofed against.
    ///
    /// <para><b>A harness that has never failed is a claim, and the claim is usually wrong</b> — the
    /// discipline <c>CLAUDE.md</c> states in as many words. It was proved wrong here: appending
    /// <c>|| true</c> to the sticky harness's verdict computation and to the motion harness's
    /// <c>checks.every(...)</c> made both report <c>PASS</c> against a genuinely broken sticky strip
    /// and against the exact reduced-motion inversion the motion harness exists to catch — and
    /// <see cref="MustNotShow"/> stayed green throughout, because <c>|| true</c> is textually
    /// distinct from the banned literal <c>say(true</c>. A denylist of source spellings cannot close
    /// an unbounded spelling space.</para>
    ///
    /// <para><b>So the guarantee moves from a source scan to an observation</b>: alongside every real
    /// proof page this file also writes a <em>twin</em> that reproduces one documented defect the
    /// harness exists to catch, and CI requires the real page to say <c>PASS</c> and the twin to say
    /// <c>FAIL</c> in the same step. A harness that reports <c>PASS</c> on both proves nothing, and
    /// that is now a build failure rather than a silence nobody notices.</para>
    ///
    /// <para><b>The twin must drive the same harness script, unmodified — only the thing under test
    /// differs.</b> A twin with a doctored script proves nothing about the shipped one. Every
    /// parameterised harness below (<see cref="StickyHarness"/>, <see cref="MotionHarness"/>,
    /// <see cref="ShortcutHarness"/>, <see cref="ThemeHarness"/>, <see cref="SliderHarness"/>,
    /// <see cref="MeasureHarness"/>) takes the *target* — the shell page or the script it drives — as
    /// its only parameter, substituted into the byte-identical template via a placeholder token. The
    /// verdict logic itself never changes between the real page and its twin.</para>
    ///
    /// <para><b>A broken script is derived from the shipped one, never hand-duplicated.</b>
    /// <see cref="WithDefect"/> reads the real file and substitutes one documented regression — and
    /// throws if the line it targets has moved, rather than silently writing a twin that no longer
    /// reproduces anything. A hand-written copy could drift from the file the app actually ships and
    /// would then be proofing itself, the exact trap <c>MustNotShow</c> already refuses for the
    /// harness script's own <c>ppMotion</c>/<c>ppSlider</c> object literals.</para>
    ///
    /// <para><b>Two kinds of twin, matched to two kinds of harness.</b> The five driven-script
    /// harnesses each load one shipped file with a single well-understood failure mode, so their
    /// twins point the same <c>&lt;script src="…"&gt;</c> placeholder at a <c>data:</c> URL carrying
    /// the broken copy — see <see cref="AsScriptSrc"/> — rather than at a second file under
    /// <c>wwwroot</c>. <see cref="TheStickyStrip"/> and <see cref="TheBoxInsets"/> instead measure a
    /// rendered page's layout, so their twins are the same rendered shell with one inline
    /// <c>&lt;style&gt;</c> injected — the exact defect category this file's own docstrings already
    /// name (<c>#app</c> wrapping <c>.budget</c>'s containing block; a margin moving one band's
    /// contents out from under the others). Nothing under <c>web/wwwroot/css</c> is touched by
    /// either: the defect lives only in the twin page.</para>
    ///
    /// <para><b>One twin per harness, not one per page.</b> <c>proof-narrow.html</c>,
    /// <c>-shell</c>, <c>-front</c> and <c>-rules</c> all call the identical
    /// <see cref="NarrowHarness"/> function against different targets, so one twin — proofing
    /// that <see cref="NarrowHarness"/> itself can fail — covers all four; a second twin per
    /// target would test the same harness logic a second time, not a second harness.</para>
    /// </summary>
    private static string ShippedScript(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "web", "wwwroot", relativePath));

    /// <summary>
    /// A broken script, as a <c>data:</c> URL rather than a file under <c>wwwroot</c>.
    ///
    /// <para><c>web/wwwroot/proof-*.html</c> is gitignored generated output and every twin page
    /// follows that name, but there is no equivalent pattern for a generated <c>.js</c> file —
    /// and this file must not edit <c>.gitignore</c>. A <c>&lt;script src="…"&gt;</c> loads a
    /// <c>data:</c> URL exactly as it loads a same-origin file, so a broken script needs no file
    /// of its own: it travels inside the twin page that is already ignored.</para>
    /// </summary>
    private static string AsScriptSrc(string javaScript) =>
        "data:text/javascript;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(javaScript));

    /// <summary>
    /// One documented regression, injected into a verbatim copy of a shipped script.
    ///
    /// <para><paramref name="find"/> must occur in the file exactly once. If it does not — the
    /// source moved, or somebody already fixed the very defect this twin exists to name — writing
    /// the twin anyway would silently stop reproducing what it claims to, which is worse than not
    /// having a twin at all: a caller reading <c>docs/notes/s4-harness.md</c> would believe the
    /// negative control still holds. So this throws rather than guessing.</para>
    /// </summary>
    private static string WithDefect(string relativePath, string find, string replace)
    {
        var real = ShippedScript(relativePath);
        var occurrences = 0;
        for (var index = real.IndexOf(find, StringComparison.Ordinal);
             index >= 0;
             index = real.IndexOf(find, index + find.Length, StringComparison.Ordinal))
        {
            occurrences++;
        }

        if (occurrences != 1)
            throw new InvalidOperationException(
                $"Expected exactly one occurrence of the documented defect line in {relativePath}, " +
                $"found {occurrences}. The shipped file has moved — update the twin to match it, or " +
                "the negative control it builds is testing nothing.");

        return real.Replace(find, replace, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>js/motion.js</c> with the exact reduced-motion inversion this file's own docstring on
    /// <see cref="TheMotionScript"/> names: the entry gate for a view transition reads
    /// <c>!still()</c> instead of <c>still()</c>, which serves the animation to exactly the people
    /// who asked for none — and, in the other direction, refuses it to everyone who did not. This
    /// is the exact spelling that defeated an <c>|| true</c>-weakened motion harness; see
    /// <c>docs/notes/s4-harness.md</c>.
    /// </summary>
    private static string MotionBrokenScript() => WithDefect("js/motion.js",
        "if (!document.startViewTransition || still()) return;",
        "if (!document.startViewTransition || !still()) return;");

    /// <summary>
    /// <c>js/motion.js</c> with <c>write()</c>'s node-preserving path reverted to the
    /// <c>element.textContent =</c> assignment it replaced — the defect that shipped: the count
    /// lands on the right number while orphaning the text node Blazor's renderer holds, so every
    /// later render by Blazor updates a node that is no longer in the document.
    ///
    /// <para><b>A separate twin from <see cref="MotionBrokenScript"/> because it is a separate
    /// property.</b> The reduced-motion inversion that twin injects leaves the counting path
    /// untouched, so it cannot fail the node-identity checks; and those checks were added because
    /// every existing check in the harness passed against this defect. A twin that does not
    /// reproduce the thing under test proves nothing about it.</para>
    /// </summary>
    private static string MotionNodeBrokenScript() => WithDefect("js/motion.js",
        "node.nodeValue = value;",
        "element.textContent = value; /* defect: replaces the node the renderer owns */");

    /// <summary>
    /// <c>js/palette.js</c> with Ctrl-K reaching the component but the browser default never taken —
    /// the harness's own check names this exact failure: "Ctrl-K takes the key from the browser...
    /// a version that listened without suppressing the default would look right here and be
    /// unusable in a real browser."
    /// </summary>
    private static string PaletteBrokenScript() => WithDefect("js/palette.js",
        "e.preventDefault();",
        "/* defect: preventDefault removed, so Ctrl-K no longer takes the key from the browser */");

    /// <summary>
    /// <c>js/slider.js</c> with the same shape of defect as <see cref="PaletteBrokenScript"/>, on
    /// the other script that exists only to call <c>preventDefault</c> on two keys: Home and End
    /// reach the guard and are counted as suppressed, but the browser default is never taken.
    /// </summary>
    private static string SliderBrokenScript() => WithDefect("js/slider.js",
        "e.preventDefault();",
        "/* defect: preventDefault removed, so Home/End no longer take the key from the browser */");

    /// <summary>
    /// <c>js/theme.js</c> with the exact regression <c>CLAUDE.md</c> records: the
    /// <c>localStorage.setItem</c> call deleted, so a choice applies for the visit and is forgotten
    /// on reload. Every C# guard on this preference — that <c>Choose</c> sends the right word to
    /// <c>ppTheme.set</c>, and that a stored value is read back — stays green against it, because
    /// persistence happens entirely inside the script and only a browser can see it lost.
    /// </summary>
    private static string ThemeBrokenScript() => WithDefect("js/theme.js",
        "localStorage.setItem(KEY, choice);",
        "void 0; /* defect: setItem removed, so the choice is never persisted */");

    /// <summary>
    /// The real shell proof, rendered fresh — the same body <see cref="TheShell"/> writes, without
    /// writing the file. Shared by <see cref="TheStickyStrip"/> and <see cref="TheBoxInsets"/>, so
    /// their twins are built from the identical markup the real pages are, and the only difference
    /// between a twin and the page it is a twin of is the one injected style.
    /// </summary>
    private static string HonestShellPage()
    {
        using var ctx = new RenderContext().With(SheetMode.Hero);
        return Page("proof-shell-hero.html", "hero", ShellBody(ctx), wrap: false);
    }

    /// <summary>
    /// <see cref="HonestShellPage"/> with one inline <c>&lt;style&gt;</c> injected before
    /// <c>&lt;/head&gt;</c> — a defect confined to the twin page, never to <c>web/wwwroot/css</c>.
    /// </summary>
    private static string ShellWithInjectedDefect(string css) =>
        HonestShellPage().Replace("</head>", $"<style>{css}</style></head>", StringComparison.Ordinal);

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

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // driven against a shell that carries the exact defect its own docstring names: #app
        // wraps .budget's containing block, which is precisely what Phase 2's View Transitions
        // work would introduce. If the harness cannot fail here, it proves nothing on the real
        // page either.
        WritePage("proof-shell-hero-broken.html", "hero",
            ShellWithInjectedDefect("#app { overflow-x: hidden }"));
        WritePage("proof-sticky-broken.html", "hero", StickyHarness("proof-shell-hero-broken.html"));
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

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // driving a copy of js/motion.js with the exact reduced-motion inversion this docstring
        // names two paragraphs up: `!still()` instead of `still()`. This is the defect that
        // defeated an `|| true`-weakened motion harness in both directions — see
        // docs/notes/s4-harness.md.
        WritePage("proof-motion-broken.html", "hero", MotionHarness(AsScriptSrc(MotionBrokenScript())));

        // The second negative control, for the second property. The twin above inverts the
        // reduced-motion gate and leaves the counting path alone, so it cannot fail the
        // node-identity checks — and those exist precisely because every other check in this
        // harness passed while `ppCount` was orphaning the text node Blazor renders into.
        WritePage("proof-motion-node-broken.html", "hero",
            MotionHarness(AsScriptSrc(MotionNodeBrokenScript())));
    }

    /// <summary>
    /// <b>The nemesis a handed-over Villain is drawn as, in a browser: night ground on a daylight
    /// Hero page, the eyes and the smoke moving, and nothing moving for a reader who asked for no
    /// motion.</b>
    ///
    /// <para>None of the three is visible to bUnit, which has no cascade and no clock. The page is
    /// the real <see cref="Nemesis"/> markup under the shipped <c>theme.css</c> and <c>app.css</c>,
    /// on a document stamped Hero and light, so the island has to be the stylesheet's doing.</para>
    ///
    /// <para><b>Three twins, one per property, each built by <see cref="WithDefect"/> out of the
    /// shipped stylesheet and inlined as a <c>data:</c> URL</b>, so the harness script is
    /// byte-identical and only the subject differs:</para>
    /// <list type="bullet">
    /// <item><c>proof-nemesis-island-broken.html</c> — <c>.nemesis</c> dropped from the villain-dark
    /// selector, so the block is drawn in the Hero's daylight. FAIL in both runs.</item>
    /// <item><c>proof-nemesis-still-broken.html</c> — the near veil's loop removed: the positive
    /// control, because a harness that only ever ran with motion off would pass a block that never
    /// moved. FAIL without the reduced-motion flag.</item>
    /// <item><c>proof-nemesis-reduced-broken.html</c> — the reduced-motion rule for the nemesis
    /// removed, so the loops run for a reader who asked for none. FAIL with the flag.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheNemesis()
    {
        if (!Asked) return;

        WritePage("proof-nemesis.html", "hero", NemesisHarness());
        WritePage("proof-nemesis-island-broken.html", "hero",
            NemesisHarness(themeCss: AsStyleHref(NemesisIslandBroken())));
        WritePage("proof-nemesis-still-broken.html", "hero",
            NemesisHarness(appCss: AsStyleHref(NemesisStillBroken())));
        WritePage("proof-nemesis-reduced-broken.html", "hero",
            NemesisHarness(appCss: AsStyleHref(NemesisReducedBroken())));
    }

    /// <summary>theme.css with <c>.nemesis</c> taken out of the villain-dark selector list.</summary>
    private static string NemesisIslandBroken() => WithDefect("css/theme.css",
        "    .nemesis {",
        "    .nemesis-island-removed {");

    /// <summary>app.css with the near veil's loop gone — the block drawn, and never moving.</summary>
    private static string NemesisStillBroken() => WithDefect("css/app.css",
        "    animation: nemesis-drift calc(var(--enter) * 46) var(--ease) infinite alternate;",
        "    animation: none; /* defect: the veil never moves */");

    /// <summary>app.css with the reduced-motion rule for the nemesis gone.</summary>
    private static string NemesisReducedBroken() => WithDefect("css/app.css",
        "    .nemesis-eye, .nemesis-veil { animation: none; }",
        "    /* defect: the nemesis loops are left running under reduced motion */");

    /// <summary>A stylesheet as a <c>data:</c> URL, for the reason <see cref="AsScriptSrc"/> gives.</summary>
    private static string AsStyleHref(string css) =>
        "data:text/css;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(css));

    /// <summary>The real component's markup, rendered rather than written by hand.</summary>
    private static string NemesisBody()
    {
        using var ctx = new RenderContext();

        return ctx.Render<Nemesis>(p => p
            .Add(n => n.Name, "The Hollow Regent")
            .Add(n => n.Campaign, "Nightfall")).Markup;
    }

    private static string NemesisHarness(string themeCss = "css/theme.css", string appCss = "css/app.css") =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheNemesis. Do not edit: rewritten on every PP_PROOF run.
             Run once plainly and once with Chrome's --force-prefers-reduced-motion; the
             expectations invert, and the harness knows it. -->
        <html lang="en" data-mode="hero" data-theme="light">
        <head>
          <meta charset="utf-8">
          <title>Proof — is the nemesis drawn as one?</title>
          <link rel="stylesheet" href="__PP_THEME_CSS__">
          <link rel="stylesheet" href="__PP_APP_CSS__">
        </head>
        <body>
        <div id="app"><main class="shell">__PP_NEMESIS__</main></div>
        <div id="verdict">measuring…</div>
        <script>
        window.addEventListener('load', () => setTimeout(() => {
          const checks = [];
          const check = (name, ok, detail) => checks.push({ name, ok, detail });
          const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

          const card = document.querySelector('.nemesis');
          const name = card && card.querySelector('.nemesis-name');
          const eyes = card ? [...card.querySelectorAll('.nemesis-eye')] : [];
          const near = card && card.querySelector('.nemesis-veil:not(.nemesis-veil-far)');

          // **The positive control on the subject**: the block is there and says who it is.
          check('the nemesis is drawn, with its name and two eyes (positive control)',
                !!card && !!name && name.textContent.trim() === 'The Hollow Regent' && eyes.length === 2 && !!near,
                `card ${!!card}, name "${name && name.textContent.trim()}", eyes ${eyes.length}, veil ${!!near}`);

          // **Night on a daylight Hero page.** The document is stamped hero and light, so the
          // block's own ground can only be villain-dark's if the stylesheet put it there.
          const ground = card ? getComputedStyle(card).getPropertyValue('--surface').trim().toLowerCase() : '';
          const page = getComputedStyle(document.documentElement).getPropertyValue('--surface').trim().toLowerCase();
          check('the block is on the villain night ground, whatever the page is',
                ground === '#111114' && page.length > 0 && page !== ground,
                `block --surface ${ground}, page --surface ${page}`);

          // The smoke never crosses the name.
          const nameZ = name ? Number(getComputedStyle(name).zIndex) : NaN;
          const veilZ = near ? Number(getComputedStyle(near).zIndex) : NaN;
          check('the name sits above the smoke',
                nameZ > veilZ,
                `name z ${nameZ}, veil z ${veilZ}`);

          const running = (el) => el ? el.getAnimations().filter((a) => a.playState === 'running') : [];
          const blinking = eyes.filter((e) => running(e)
            .some((a) => a.effect.getTiming().iterations === Infinity)).length;
          const drifting = running(near).length;

          if (!reduced) {
            check('the eyes blink and the smoke drifts (positive control on the motion)',
                  blinking === 2 && drifting > 0,
                  `blinking eyes ${blinking}/2, drifting near veil ${drifting}`);
          } else {
            const any = eyes.concat(near ? [near] : []).reduce((n, el) => n + el.getAnimations().length, 0);
            check('nothing moves for a reader who asked for no motion',
                  any === 0,
                  `animations still attached ${any}`);
            const open = eyes.length === 2 && eyes.every((e) => getComputedStyle(e).opacity === '1' &&
              getComputedStyle(e).transform === 'none');
            check('and the eyes rest open',
                  open,
                  eyes.map((e) => `${getComputedStyle(e).opacity}/${getComputedStyle(e).transform}`).join(', '));
          }

          const ok = checks.length > 0 && checks.every((c) => c.ok);
          document.title = ok ? 'NEMESIS: PASS' : 'NEMESIS: FAIL';
          document.getElementById('verdict').textContent =
            (ok ? 'NEMESIS: PASS' : 'NEMESIS: FAIL') +
            ` (prefers-reduced-motion: ${reduced ? 'reduce' : 'no-preference'})\n` +
            checks.map((c) => `  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name} — ${c.detail}`).join('\n');
        }, 50));
        </script>
        </body>
        </html>
        """.Replace("__PP_THEME_CSS__", themeCss, StringComparison.Ordinal)
           .Replace("__PP_APP_CSS__", appCss, StringComparison.Ordinal)
           .Replace("__PP_NEMESIS__", NemesisBody(), StringComparison.Ordinal);

    /// <summary>
    /// The nemesis harness and its three twins, checked on every run rather than only under
    /// <c>PP_PROOF</c>: the markers say the harness measures, and each twin really differs from
    /// the real page — <see cref="WithDefect"/> throws first if a documented line has moved.
    /// </summary>
    [Fact]
    public void TheNemesisHarnessAndItsTwinsMeasureTheShippedStylesheets()
    {
        var real = NemesisHarness();
        AssertMarkers("proof-nemesis.html", real);

        foreach (var (file, twin) in new[]
                 {
                     ("proof-nemesis-island-broken.html", NemesisHarness(themeCss: AsStyleHref(NemesisIslandBroken()))),
                     ("proof-nemesis-still-broken.html", NemesisHarness(appCss: AsStyleHref(NemesisStillBroken()))),
                     ("proof-nemesis-reduced-broken.html", NemesisHarness(appCss: AsStyleHref(NemesisReducedBroken()))),
                 })
        {
            AssertMarkers(file, twin);
            Assert.NotEqual(real, twin);
        }
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

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // driving a copy of js/palette.js with preventDefault removed: Ctrl-K still reaches the
        // component, but the browser default (focusing the address bar) is never taken.
        WritePage("proof-shortcut-broken.html", "hero", ShortcutHarness(AsScriptSrc(PaletteBrokenScript())));
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

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // driving a copy of js/theme.js with the exact regression this docstring already names
        // two paragraphs up: localStorage.setItem deleted, so a choice is forgotten on reload.
        WritePage("proof-theme-broken.html", "hero", ThemeHarness(AsScriptSrc(ThemeBrokenScript())));
    }

    /// <summary>
    /// <b>Does Home/End stop scrolling the page on a guarded rank slider, and leave everything
    /// else alone?</b>
    ///
    /// <para>bUnit can drive <c>RankRow</c>'s Blazor key handler and cannot drive a real
    /// <c>preventDefault</c> — a synthetic <c>KeyboardEventArgs</c> has no browser default to
    /// suppress, so <see cref="RankPipTests.HomeAndEndGoToTheBounds"/> would pass identically
    /// against a version of the guard that suppressed nothing at all. This drives the real
    /// <c>wwwroot/js/slider.js</c> against a bare element standing in for the pips, and checks
    /// the two keys the guard exists for, the two it must never touch — Tab above all, since
    /// suppressing it would trap focus inside a rank row — and that an unguarded element is
    /// unaffected, which is the harness's own control that the effect is the guard's doing.</para>
    ///
    /// <code>
    /// chrome … --dump-dom file:///…/web/wwwroot/proof-slider.html
    /// </code>
    /// </summary>
    [Fact]
    public void TheSliderScript()
    {
        if (!Asked) return;

        WritePage("proof-slider.html", "hero", SliderHarness());

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // driving a copy of js/slider.js with preventDefault removed: Home and End still reach
        // the guard and are counted as suppressed, but the browser default is never taken.
        WritePage("proof-slider-broken.html", "hero", SliderHarness(AsScriptSrc(SliderBrokenScript())));
    }

    private static string SliderHarness(string script = "js/slider.js") =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheSliderScript. Do not edit: rewritten on every PP_PROOF
             run. Drives the real wwwroot/js/slider.js. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — do Home and End stop scrolling the page?</title>
          <style>
            body { margin: 0; font: 14px monospace; background: #111; color: #eee; padding: 12px }
            #verdict { white-space: pre }
            .bad { color: #f66 }
          </style>
        </head>
        <body>
        <div id="verdict">measuring…</div>
        <div id="guarded" role="slider" tabindex="0"></div>
        <div id="unguarded" role="slider" tabindex="0"></div>
        <script src="__PP_SLIDER_SCRIPT__"></script>
        <script>
        (() => {
          const box = document.getElementById('verdict');
          const checks = [];
          const check = (name, ok, detail) => checks.push({ name, ok, detail });

          const guarded = document.getElementById('guarded');
          const unguarded = document.getElementById('unguarded');

          // **The positive control, before anything that depends on it.** Every check below
          // asserts an outcome, and an outcome is satisfied by a guard that was never attached —
          // the script could have thrown on load and every negative case would still hold.
          window.ppSlider.guard(guarded);
          check('the guard actually attached (positive control)',
                window.ppSliderStats.listeners === 1,
                `listeners = ${window.ppSliderStats.listeners}`);

          // Guarding the same element twice must not stack a second listener: a component calls
          // this on every first render of every row, and a row is not recreated on every change,
          // but the guard has to be idempotent regardless of how many times it is asked.
          window.ppSlider.guard(guarded);
          check('guarding twice still leaves one listener',
                window.ppSliderStats.listeners === 1,
                `listeners = ${window.ppSliderStats.listeners}`);

          const press = (el, key) => {
            const e = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
            el.dispatchEvent(e);
            return e;
          };

          const home = press(guarded, 'Home');
          check('Home is taken from the browser on a guarded element',
                home.defaultPrevented === true,
                `defaultPrevented = ${home.defaultPrevented}`);

          const end = press(guarded, 'End');
          check('...and so is End',
                end.defaultPrevented === true,
                `defaultPrevented = ${end.defaultPrevented}`);

          check('the suppression counter agrees (positive control)',
                window.ppSliderStats.suppressed === 2,
                `suppressed = ${window.ppSliderStats.suppressed}`);

          // The negative cases. Without them the checks above are satisfied by a listener that
          // swallows every key, which would take Tab with it and trap focus inside a rank row.
          const tab = press(guarded, 'Tab');
          const left = press(guarded, 'ArrowLeft');
          check('Tab and the arrows reach the browser untouched',
                tab.defaultPrevented === false && left.defaultPrevented === false,
                `Tab = ${tab.defaultPrevented}, ArrowLeft = ${left.defaultPrevented}`);

          // An element nothing has guarded is unaffected — the harness's own control that the
          // effect above is the guard's doing and not something global the script installed.
          const bare = press(unguarded, 'Home');
          check('an unguarded element is left alone',
                bare.defaultPrevented === false,
                `defaultPrevented = ${bare.defaultPrevented}`);

          const ok = checks.every((c) => c.ok);
          document.title = ok ? 'SLIDER: PASS' : 'SLIDER: FAIL';
          box.className = ok ? '' : 'bad';
          box.textContent =
            (ok ? 'SLIDER: PASS' : 'SLIDER: FAIL') + '\n' +
            checks.map((c) => `  ${c.ok ? 'ok  ' : 'FAIL'} ${c.name} — ${c.detail}`).join('\n');
        })();
        </script>
        </body>
        </html>
        """.Replace("__PP_SLIDER_SCRIPT__", script, StringComparison.Ordinal);

    private static string ThemeHarness(string script = "js/theme.js") =>
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
          <script src="__PP_THEME_SCRIPT__"></script>
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
          again.src = '__PP_THEME_SCRIPT__';
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
            box.textContent = 'THEME: FAIL\n  FAIL __PP_THEME_SCRIPT__ could not be loaded a second time';
          };

          document.head.appendChild(again);
        })();
        </script>
        </body>
        </html>
        """.Replace("__PP_THEME_SCRIPT__", script, StringComparison.Ordinal);

    private static string ShortcutHarness(string script = "js/palette.js") =>
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
        <script src="__PP_SHORTCUT_SCRIPT__"></script>
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
        """.Replace("__PP_SHORTCUT_SCRIPT__", script, StringComparison.Ordinal);

    private static string MotionHarness(string script = "js/motion.js") =>
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
        <script src="__PP_MOTION_SCRIPT__"></script>
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

          // ── The text node the renderer owns ──────────────────────────────────────────
          //
          // **`element.textContent = value` replaces the element's children**, including the
          // text node Blazor's renderer holds a reference to. From the first count onward
          // Blazor's own updates to that figure land on a node that is no longer in the
          // document, and what a reader sees is whatever this script last wrote — correct only
          // for as long as every change is followed by a count. It is not: an unchanged
          // `_shown`, a clock frozen because the tab was hidden, a swallowed interop failure.
          // Seen in a browser as a spent figure reading 97 beside "99 left", with the detached
          // node holding the right answer all along.
          //
          // **Every check above passed against that defect**, because each counts into a bare
          // <span> with no children — `write()`'s fallback path — so the node the bug is about
          // never existed. This renders the figure the way Blazor does, text node already in
          // place, which is the only arrangement that can see it.
          const owned = document.createElement('strong');
          owned.textContent = '12';            // as Blazor rendered it
          document.body.appendChild(owned);
          const ownedNode = owned.firstChild;  // the node Blazor would go on updating

          window.ppCount(owned, 12, 34);
          if (!reduced) { owned.ppCount.clock.currentTime = enterMs; owned.ppCount.draw(); }

          check('a count keeps the text node the renderer owns',
                owned.firstChild === ownedNode && ownedNode.parentNode === owned,
                `reduced=${reduced} sameNode=${owned.firstChild === ownedNode} `
                + `attached=${ownedNode.parentNode === owned}`);

          // The positive control on the check above: node identity is also preserved perfectly
          // by a script that does nothing at all, so the figure must have reached the answer
          // through that same node — and through exactly one of them, since a second text node
          // beside it renders as the two values run together.
          check('and the engine answer arrives through that node',
                owned.textContent === '34' && owned.childNodes.length === 1,
                `showed ${owned.textContent} across ${owned.childNodes.length} node(s)`);

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
        """.Replace("__PP_MOTION_SCRIPT__", script, StringComparison.Ordinal);


    /// <summary>
    /// The sticky harness, as a string. Hand-written rather than rendered: what it proves is a
    /// fact about layout in a browser, and there is no component whose markup would express it.
    ///
    /// <para>It reports the numbers as well as the verdict. The bug this guards against is a strip
    /// that scrolls away — a difference of a few hundred pixels — but the failure mode next door is
    /// a strip that sticks to the wrong offset, and a bare yes/no cannot tell those apart.</para>
    /// </summary>
    private static string StickyHarness(string target = "proof-shell-hero.html") =>
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
        <iframe id="f" src="__PP_STICKY_TARGET__"></iframe>
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
        """.Replace("__PP_STICKY_TARGET__", target, StringComparison.Ordinal);

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

        // The two pages this slice added. They are ordinary panel layouts, but the front door is
        // the one screen with a grid that has to collapse, and neither had ever been measured at
        // 375px — which is the width the 8px overflow this harness exists for showed up at.
        WritePage("proof-narrow-front.html", "hero", NarrowHarness("proof-front-door-hero.html"));
        WritePage("proof-narrow-rules.html", "hero", NarrowHarness("proof-rules-hero.html"));

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // measuring a shell twin with a real, generously-wide element — the shape of the "8px
        // overflow" this harness exists for, made large enough not to be a coin flip against the
        // 0.5px tolerance the honest checks already carry.
        WritePage("proof-shell-hero-broken-narrow.html", "hero",
            ShellWithInjectedDefect(".shell { width: 3000px }"));
        WritePage("proof-narrow-broken.html", "hero",
            NarrowHarness("proof-shell-hero-broken-narrow.html"));
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

        // The negative control — see the docstring on ShippedScript above. Same harness script,
        // measuring a shell twin with the exact defect this file's own comments name: a margin
        // on one band's inner element moves its content 96px out from under the others, which
        // the band's own box and padding do not show.
        WritePage("proof-shell-hero-broken-inset.html", "hero",
            ShellWithInjectedDefect(".banner-inner { margin-left: 96px }"));
        WritePage("proof-measure-broken.html", "hero",
            MeasureHarness("proof-shell-hero-broken-inset.html"));
    }

    /// <summary>
    /// Reports the left and right edge of each band and of the shell, so the four are known to
    /// agree rather than believed to. <b>The chrome bands each centre their own contents on
    /// <c>--column</c>; a band that quietly stopped would look almost right.</b>
    /// </summary>
    private static string MeasureHarness(string target = "proof-shell-hero.html") =>
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
        <iframe id="f" src="__PP_MEASURE_TARGET__"></iframe>
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
        """.Replace("__PP_MEASURE_TARGET__", target, StringComparison.Ordinal);

    /// <summary>
    /// Does every item in the banner sit on one line? <b>A measured check, because the eye read it
    /// wrong twice and the markup reads right either way.</b>
    ///
    /// <para>The owner reported the bar as "search is vertically elevated" and it was: the three
    /// plain links had their text line at 29.13px and the SEARCH label at 27.88px, 1.25px above
    /// and the most elevated thing in the strip. <b>Nothing in the stylesheet looked wrong.</b>
    /// <c>.banner-inner</c> was <c>align-items: center</c> and every item's <em>box</em> was
    /// centred on 30.30px, correctly — what differed was how far each item's <em>text</em> sat
    /// from its own box centre, which no rule states and no source scan can compute. A link is
    /// pulled up by the underline hanging below it and the palette button by the key boxes'
    /// border and padding hanging below its shared baseline.</para>
    ///
    /// <para>So this is the only instrument that can answer it: a browser, a range box per item,
    /// and a number. bUnit has no layout engine, and a CSS guard asserting
    /// <c>align-items: baseline</c> would pass the day somebody adds a taller child that the
    /// baseline no longer saves.</para>
    ///
    /// <para><b>It measures the baseline, not the line-box centre, and the difference matters.</b>
    /// A line box's height follows its font size, so two items sharing a baseline in two sizes
    /// have line-box centres several tenths of a pixel apart — the character switcher's name is
    /// body face where the avenues are display face. Baseline is what <c>align-items: baseline</c>
    /// actually equalises and what a reader's eye reads as "on one line", so it is what is
    /// measured: a zero-sized <c>inline-block</c> probe appended to each item resolves its own
    /// baseline to its top edge, which is the line's baseline, exactly and independently of the
    /// face.</para>
    ///
    /// <para><b>Seven items now, and the seventh is why this harness had to be touched at all.</b>
    /// The banner's Search control is a field rather than a button, and an <c>&lt;input&gt;</c>
    /// brings its own box model — a border, a fill, padding and a <c>line-height</c> of the
    /// browser's choosing, none of which any rule in this repository states. It is measured as two
    /// rows: the control, whose flex line is the baseline the band aligns it by and the one its
    /// text sits on, and the key box beside it, which holds text of its own. Either alone could be
    /// right while the other was not.</para>
    ///
    /// <para><b>The wordmark is excluded and that is a decision, not an oversight.</b> It is two
    /// lines — the name and the subtitle — so it has no single text line to be on, and
    /// <c>app.css</c> gives it <c>align-self: center</c> for that reason. Putting a two-line block
    /// into a spread of one-line baselines compares two different things. The save region is
    /// excluded too, on a narrower ground: it renders no text between saves, so on this page it
    /// has no baseline to measure rather than a wrong one.</para>
    ///
    /// <code>
    /// chrome … --dump-dom file:///…/web/wwwroot/proof-align.html
    /// grep -o '&lt;title&gt;ALIGN: [A-Z]*' dump.html
    /// </code>
    /// </summary>
    [Fact]
    public void TheBannerSitsOnOneLine()
    {
        if (!Asked) return;

        WritePage("proof-align.html", "hero", AlignHarness());

        // The negative control — see the docstring on ShippedScript above. The same harness
        // script, measuring a shell twin carrying the exact defect the owner reported: the
        // banner back on `align-items: center`, so each item's box is centred correctly and each
        // item's text is not. If the harness cannot fail against the thing it was written for,
        // it proves nothing on the real page either.
        WritePage("proof-shell-hero-broken-align.html", "hero",
            ShellWithInjectedDefect(".banner-inner { align-items: center }"));
        WritePage("proof-align-broken.html", "hero",
            AlignHarness("proof-shell-hero-broken-align.html"));
    }

    /// <summary>
    /// Reports the text baseline of every one-line item in the banner, so the row is known to
    /// share one rather than believed to.
    /// </summary>
    private static string AlignHarness(string target = "proof-shell-hero.html") =>
        """
        <!doctype html>
        <!-- Generated by ProofPages.TheBannerSitsOnOneLine. Do not edit: rewritten on every PP_PROOF run. -->
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Proof — banner alignment</title>
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
        <iframe id="f" src="__PP_ALIGN_TARGET__"></iframe>
        <script>
        document.getElementById('f').addEventListener('load', async () => {
          const box = document.getElementById('verdict');
          const say = (ok, detail) => {
            document.title = ok ? 'ALIGN: PASS' : 'ALIGN: FAIL';
            box.textContent = (ok ? 'ALIGN: PASS' : 'ALIGN: FAIL') + '\n' + detail;
            box.className = ok ? '' : 'bad';
          };
          try {
            const d = document.getElementById('f').contentDocument;

            // **Measure the banner in the face the reader sees, not in whatever Chrome drew while
            // the webfont was still arriving.** `load` fires on the iframe before `font-display:
            // swap` has finished swapping, and on the CI runner the fallback for the display face
            // is a wide generic sans: in it the four avenues and the wordmark run ~150px wider,
            // the tools wrap onto a second line, and this harness reported a 48px "spread" for a
            // banner that sits on one line in Oswald — the day a fourth avenue was added, on a
            // tree the goldens (which do wait) showed correct. So wait for the fonts, and say in
            // the detail which face was measured, so a runner where the font never loads is a
            // reading somebody can see rather than a fallback that happened to fit.
            await d.fonts.ready;
            const faces = [...d.fonts].filter(f => f.status === 'loaded').map(f => f.family);

            // Every one-line item in the band, named by the element that directly holds its
            // text. `.banner-title` is deliberately absent — two lines, `align-self: center`,
            // no single baseline to share — and so is `.save-status`, which renders no text
            // between saves and so has none to measure here.
            // **Search is two rows now, and that is the field's arrival rather than padding.**
            // The control used to be a button whose label was a span holding text; it is an
            // `<input>` with the chord beside it, and an input has no children to put a probe
            // inside. So the field's line is read off the control itself — the probe joins its
            // flex line and settles on the baseline the input's own text sets, which is also the
            // baseline `.banner-tools` aligns the whole control by — and the chord is measured
            // separately, in the key box, which does hold text.
            //
            // **Two rows rather than one, and each was measured catching a defect the other
            // reports as a tidy band.** The control's baseline is what the band aligns the whole
            // thing by; the key box's is what a reader sees beside the field. Neither subsumes
            // the other, and the two mutations that establish that are worth naming exactly,
            // because the obvious guess about which row catches which is wrong:
            //
            //   * `.palette-open { align-items: center }` — the wrapper stops sharing its
            //     children's baseline. The **control** reads 28.17 against the band's 32.00 and
            //     the chord reads 32.25. Spread 4.08px, FAIL. On the chord row alone the spread
            //     is 0.25px, which is a PASS: the row that catches this is the control.
            //   * `.palette-open .key { align-self: center; position: relative; top: 3px }` — a
            //     defect confined to the key boxes. The control stays on 32.00 with the rest of
            //     the band and the chord reads 37.00. Spread 5.00px, FAIL. On the control row
            //     alone the spread is 0.00px, a clean PASS over a chord sitting 5px off the
            //     line: the row that catches this is the chord.
            //
            // **And the field's own box model is not what this proof holds.** Deleting
            // `.palette-field`'s `border: none` and `padding: 0` — restoring the UA border and
            // fill the rule exists to undo — moves every one of the seven items from 32.00 to
            // 33.00 together and leaves the spread at 0.00px. `align-items: baseline` re-aligns
            // the band to the field's new baseline, so a *spread* cannot see it. Those
            // declarations are held by the pixel goldens and by the eye, not by this page; see
            // the note on the rule itself in `app.css`.
            const parts = [
              ['build',      '.avenue-nav .banner-link'],
              ['characters', '.avenue-nav .banner-link'],
              ['run',        '.avenue-nav .banner-link'],
              ['rules',      '.avenue-nav .banner-link'],
              ['character',  '.character-switch-name'],
              ['search',     '.palette-open'],
              ['chord',      '.palette-open .key'],
              ['account',    '.banner-account'],
              ['settings',   '.settings-open-label'],
            ];

            // **The baseline, not the box and not the line-box centre.** An empty inline-block
            // has no content, so its own baseline is its bottom margin edge and its box has no
            // height — which means the browser positions its single edge exactly on the line's
            // baseline. That is a number no computed style exposes and no range box gives: a
            // range rect is the line box, whose height follows the font size, so two items
            // genuinely sharing a baseline in two sizes measure several tenths of a pixel apart.
            // The banner has two faces and two sizes in it, so that error is not hypothetical.
            //
            // **Appended to a flex container the probe is a flex item, and that is the reading
            // wanted for the search field.** `.palette-open` is `align-items: baseline`, so the
            // probe joins the same baseline group the field's text and the key boxes are in, and
            // its single edge lands on that shared line — which is the line the band aligns the
            // whole control by. It is the only way to read an `<input>`'s baseline from the
            // outside: an input takes no children, so there is nowhere inside it to put a probe.
            const baselineOf = (el) => {
              const probe = d.createElement('span');
              probe.style.cssText = 'display:inline-block;width:0;height:0;vertical-align:baseline';
              el.appendChild(probe);
              const top = probe.getBoundingClientRect().top;
              probe.remove();
              return top;
            };

            // What to print beside a measurement, so a dump says which control it read. An input
            // holds no text, so the row for the search control would otherwise be captioned with
            // the two key boxes beside it — a dump naming the wrong thing is how a proof comes to
            // be read as evidence for something it did not measure.
            const shown = (el) => {
              const box = el.matches('input') ? el : el.querySelector('input');
              return ((box ? (box.value || box.placeholder) : el.textContent) || '').trim().slice(0, 24);
            };

            const rows = [];
            const found = [];
            const seen = new Map();

            for (const [name, sel] of parts) {
              // Four rows name the same selector — the row of avenues — so each takes the next
              // match rather than the first. `querySelector` would have measured Build four times
              // and reported a spread of zero across a row where Rules had been pushed out of line.
              const nth = seen.get(sel) || 0;
              seen.set(sel, nth + 1);

              const el = d.querySelectorAll(sel)[nth];
              if (!el) { rows.push(`    ${name.padEnd(10)} ${sel} NOT FOUND`); continue; }

              const at = baselineOf(el);
              found.push(at);
              rows.push(`    ${name.padEnd(10)} baseline ${at.toFixed(2)}  "${shown(el)}"`);
            }

            const spread = found.length ? Math.max(...found) - Math.min(...found) : 999;

            // **The positive control, and it is why the count is asserted rather than inferred.**
            // A spread over an empty list is 999 and fails, but over a *single* found item it is
            // 0.00 and passes — so a banner that had lost five of its six controls would report
            // a perfectly aligned row. That is this repository's commonest guard fault in its
            // exact shape: a feature that did not run mistaken for a feature that worked.
            const complete = found.length === parts.length;

            say(complete && spread < 0.5,
              `target ${document.getElementById('f').getAttribute('src')}  items ${found.length} of ${parts.length}\n` +
              `faces loaded: ${faces.length ? faces.join(', ') : 'none — measured in the fallback'}\n` +
              `${rows.join('\n')}\n    spread ${spread.toFixed(2)}px (want < 0.5)`);
          } catch (e) {
            say(false, 'blocked: ' + e.message);
          }
        });
        </script>
        </body>
        </html>
        """.Replace("__PP_ALIGN_TARGET__", target, StringComparison.Ordinal);

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
        // The settings menu, disclosed. **Both switches by name and the list itself**, because a
        // proof of a disclosure captured shut shows an empty band and reads as a feature that
        // works — the trap this file already records for the palette, the budget breakdown and the
        // rulebook entry.
        ["proof-settings.html"] =
        [
            "class=\"banner\"", "settings-menu-list", "mode-switch", "theme-switch",
            "aria-expanded=\"true\"",
        ],
        // The alignment harness: the measurement it takes, both verdicts, the resting text that
        // is neither, and the positive control that stops a banner with one control left in it
        // reporting a perfectly aligned row.
        ["proof-align.html"] =
        [
            ".banner-account", ".palette-open", ".palette-open .key", ".settings-open-label",
            "vertical-align:baseline", "getBoundingClientRect",
            "ALIGN: PASS", "ALIGN: FAIL", "measuring",
            "complete && spread", "document.title",
            // The two exclusions, named on the page so a reader of the dump knows what was not
            // measured rather than assuming everything was.
            "banner-title", "save-status",
        ],
        ["proof-align-broken.html"] =
        [
            ".banner-account", ".palette-open", ".palette-open .key", ".settings-open-label",
            "vertical-align:baseline", "getBoundingClientRect",
            "ALIGN: PASS", "ALIGN: FAIL", "measuring",
            "complete && spread", "document.title",
            "banner-title", "save-status",
        ],
        // The two shell twins this harness is proofed against. They are ordinary shell pages
        // with one injected style, so they carry the same markers the honest one does.
        ["proof-shell-hero-broken-align.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"budget\"", "class=\"shell\""],
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
            // The node-identity half. Every other counting check here writes into a bare <span>,
            // which has no text node to orphan — so without these the defect is invisible to
            // this harness, which is exactly how it shipped.
            "text node the renderer owns", "ownedNode", "childNodes.length",
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
        ["proof-narrow-front.html"] =
        [
            "375px", "clientWidth", "scrollWidth", "getBoundingClientRect",
            "NARROW: PASS", "NARROW: FAIL", "measuring", "document.title",
        ],
        ["proof-narrow-rules.html"] =
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
        // Home and End on a rank slider. `ppSliderStats` is the positive control; the negative
        // case is named because without it a listener that swallows every key would satisfy
        // every check above while trapping Tab inside a rank row, and the unguarded element is
        // the harness's own proof that the effect is the guard's doing and not something global.
        ["proof-slider.html"] =
        [
            "js/slider.js", "ppSlider.guard", "ppSliderStats", "positive control",
            "KeyboardEvent", "defaultPrevented",
            "guarding twice still leaves one listener",
            "Tab and the arrows reach the browser untouched",
            "an unguarded element is left alone",
            "SLIDER: PASS", "SLIDER: FAIL", "measuring", "document.title",
        ],

        // ── The negative controls — see ShippedScript's docstring. Same markers as the real
        // page each twin is a twin of, because it is the identical harness script; the one
        // thing that must differ is named separately below, in each Fact that writes the twin.
        ["proof-shell-hero-broken.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"budget\"", "class=\"shell\""],
        ["proof-shell-hero-broken-inset.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"budget\"", "class=\"shell\""],
        ["proof-shell-hero-broken-narrow.html"] =
            ["class=\"banner\"", "class=\"banner-inner\"", "class=\"steps\"", "class=\"budget\"", "class=\"shell\""],
        ["proof-sticky-broken.html"] =
        [
            ".budget", ".steps", "getBoundingClientRect", "scrollTo",
            "STICKY: PASS", "STICKY: FAIL", "measuring",
            "stuck && scrolled", "document.title",
        ],
        // The nemesis harness and its twins: the same script against the real stylesheets and
        // against three broken copies of them, and it must compute its verdict from what it read.
        ["proof-nemesis.html"] = NemesisMarkers("css/theme.css"),
        ["proof-nemesis-island-broken.html"] = NemesisMarkers("data:text/css;base64,"),
        ["proof-nemesis-still-broken.html"] = NemesisMarkers("data:text/css;base64,"),
        ["proof-nemesis-reduced-broken.html"] = NemesisMarkers("data:text/css;base64,"),
        ["proof-motion-broken.html"] =
        [
            "data:text/javascript;base64,", "prefers-reduced-motion", "startViewTransition",
            "MOTION: PASS", "MOTION: FAIL", "measuring",
            "checks.every", "document.title",
            "css/theme.css", "--enter resolves",
            "ppCount", "ppMotionStats", "positive control",
            "currentTime", "resting frame is the engine number",
            "ppLand", "getAnimations", "ease-emphasised",
            "text node the renderer owns", "ownedNode", "childNodes.length",
        ],
        // The second twin: the same harness script against a motion.js whose write() helper
        // replaces the text node again. It must say FAIL, and it fails on the node-identity
        // checks specifically — the reduced-motion twin above cannot reach them.
        ["proof-motion-node-broken.html"] =
        [
            "data:text/javascript;base64,", "prefers-reduced-motion", "startViewTransition",
            "MOTION: PASS", "MOTION: FAIL", "measuring",
            "checks.every", "document.title",
            "css/theme.css", "--enter resolves",
            "ppCount", "ppMotionStats", "positive control",
            "text node the renderer owns", "ownedNode", "childNodes.length",
        ],
        ["proof-narrow-broken.html"] =
        [
            "375px", "clientWidth", "scrollWidth", "getBoundingClientRect",
            "NARROW: PASS", "NARROW: FAIL", "measuring", "document.title",
        ],
        ["proof-measure-broken.html"] =
        [
            ".banner-inner", ".steps-list", ".budget-strip", ".shell",
            "getBoundingClientRect", "spread",
            "INSETS: PASS", "INSETS: FAIL", "measuring", "document.title",
        ],
        ["proof-shortcut-broken.html"] =
        [
            "data:text/javascript;base64,", "ppPalette.listen", "ppPaletteStats", "positive control",
            "KeyboardEvent", "ctrlKey", "metaKey", "defaultPrevented",
            "a bare k and a Ctrl-J reach nothing",
            "activeElement", "keyboard trap",
            "SHORTCUT: PASS", "SHORTCUT: FAIL", "measuring", "document.title",
        ],
        ["proof-theme-broken.html"] =
        [
            "data:text/javascript;base64,", "ppTheme.set", "ppThemeStats", "positive control",
            "pp.theme.v1", "localStorage", "data-theme",
            "removes the value rather than storing a third word",
            "with nothing calling it",
            "THEME: PASS", "THEME: FAIL", "measuring", "document.title",
        ],
        ["proof-slider-broken.html"] =
        [
            "data:text/javascript;base64,", "ppSlider.guard", "ppSliderStats", "positive control",
            "KeyboardEvent", "defaultPrevented",
            "guarding twice still leaves one listener",
            "Tab and the arrows reach the browser untouched",
            "an unguarded element is left alone",
            "SLIDER: PASS", "SLIDER: FAIL", "measuring", "document.title",
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
        AssertMarkers("proof-slider.html", SliderHarness());
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

        var front = NarrowHarness("proof-front-door-hero.html");
        AssertMarkers("proof-narrow-front.html", front);
        Assert.Contains("src=\"proof-front-door-hero.html\"", front, StringComparison.Ordinal);

        var rules = NarrowHarness("proof-rules-hero.html");
        AssertMarkers("proof-narrow-rules.html", rules);
        Assert.Contains("src=\"proof-rules-hero.html\"", rules, StringComparison.Ordinal);

        AssertMarkers("proof-measure.html", MeasureHarness());

        var align = AlignHarness();
        AssertMarkers("proof-align.html", align);
        Assert.Contains("src=\"proof-shell-hero.html\"", align, StringComparison.Ordinal);
    }

    /// <summary>
    /// The negative controls: every twin drives the byte-identical harness script and is pointed
    /// at its own broken target rather than at the real one, and every broken script this file
    /// generates actually differs from the shipped one it was derived from.
    ///
    /// <para><b>The last part is the positive control on the negative control.</b> <c>WithDefect</c>
    /// throws if the line it targets is missing, which catches the file moving out from under a
    /// twin — but it would not catch a defect that was accidentally a no-op, the way the counting
    /// figure's resting frame is a no-op to remove (<c>CLAUDE.md</c> names that exact trap). So this
    /// asserts each broken script is not equal to the real one, before trusting either half of
    /// <see cref="TheStickyStrip"/>'s or <see cref="TheMotionScript"/>'s <c>--dump-dom</c> checks.
    /// Whether a broken script drives the harness to an actual <c>FAIL</c> is checked in a browser,
    /// in the build workflow — nothing in this project renders a script's runtime behaviour, so
    /// that half cannot be an xunit assertion.</para>
    /// </summary>
    [Fact]
    public void TheTwinsDriveTheSameScriptAgainstTheirOwnBrokenTarget()
    {
        var brokenShell = ShellWithInjectedDefect("#app { overflow-x: hidden }");
        AssertMarkers("proof-shell-hero-broken.html", brokenShell);
        Assert.NotEqual(HonestShellPage(), brokenShell);

        var brokenInsetShell = ShellWithInjectedDefect(".banner-inner { margin-left: 96px }");
        AssertMarkers("proof-shell-hero-broken-inset.html", brokenInsetShell);
        Assert.NotEqual(HonestShellPage(), brokenInsetShell);

        var brokenNarrowShell = ShellWithInjectedDefect(".shell { width: 3000px }");
        AssertMarkers("proof-shell-hero-broken-narrow.html", brokenNarrowShell);
        Assert.NotEqual(HonestShellPage(), brokenNarrowShell);

        var sticky = StickyHarness("proof-shell-hero-broken.html");
        AssertMarkers("proof-sticky-broken.html", sticky);
        Assert.Contains("src=\"proof-shell-hero-broken.html\"", sticky, StringComparison.Ordinal);

        var narrow = NarrowHarness("proof-shell-hero-broken-narrow.html");
        AssertMarkers("proof-narrow-broken.html", narrow);
        Assert.Contains("src=\"proof-shell-hero-broken-narrow.html\"", narrow, StringComparison.Ordinal);

        var measure = MeasureHarness("proof-shell-hero-broken-inset.html");
        AssertMarkers("proof-measure-broken.html", measure);
        Assert.Contains("src=\"proof-shell-hero-broken-inset.html\"", measure, StringComparison.Ordinal);

        // The alignment twin puts the banner back on `align-items: center` — the exact state the
        // owner reported, in which every item's box is centred correctly and every item's text
        // is not.
        var brokenAlignShell = ShellWithInjectedDefect(".banner-inner { align-items: center }");
        AssertMarkers("proof-shell-hero-broken-align.html", brokenAlignShell);
        Assert.NotEqual(HonestShellPage(), brokenAlignShell);

        var alignTwin = AlignHarness("proof-shell-hero-broken-align.html");
        AssertMarkers("proof-align-broken.html", alignTwin);
        Assert.Contains("src=\"proof-shell-hero-broken-align.html\"", alignTwin, StringComparison.Ordinal);

        var motionScript = MotionBrokenScript();
        Assert.NotEqual(ShippedScript("js/motion.js"), motionScript);
        var motion = MotionHarness(AsScriptSrc(motionScript));
        AssertMarkers("proof-motion-broken.html", motion);
        Assert.Contains($"src=\"{AsScriptSrc(motionScript)}\"", motion, StringComparison.Ordinal);

        var paletteScript = PaletteBrokenScript();
        Assert.NotEqual(ShippedScript("js/palette.js"), paletteScript);
        var shortcut = ShortcutHarness(AsScriptSrc(paletteScript));
        AssertMarkers("proof-shortcut-broken.html", shortcut);
        Assert.Contains($"src=\"{AsScriptSrc(paletteScript)}\"", shortcut, StringComparison.Ordinal);

        var themeScript = ThemeBrokenScript();
        Assert.NotEqual(ShippedScript("js/theme.js"), themeScript);
        var theme = ThemeHarness(AsScriptSrc(themeScript));
        AssertMarkers("proof-theme-broken.html", theme);
        Assert.Contains($"src=\"{AsScriptSrc(themeScript)}\"", theme, StringComparison.Ordinal);

        var sliderScript = SliderBrokenScript();
        Assert.NotEqual(ShippedScript("js/slider.js"), sliderScript);
        var slider = SliderHarness(AsScriptSrc(sliderScript));
        AssertMarkers("proof-slider-broken.html", slider);
        Assert.Contains($"src=\"{AsScriptSrc(sliderScript)}\"", slider, StringComparison.Ordinal);
    }

    private static string[] NemesisMarkers(string stylesheet) =>
    [
        stylesheet, "class=\"nemesis\"", "nemesis-eye", "nemesis-veil", "The Hollow Regent",
        "prefers-reduced-motion", "getAnimations", "positive control", "#111114",
        "NEMESIS: PASS", "NEMESIS: FAIL", "measuring", "checks.every", "document.title",
        "data-mode=\"hero\"", "data-theme=\"light\"",
    ];

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
    ///
    /// <para><b>This is a denylist of source spellings, and it was defeated by one.</b> Appending
    /// <c>|| true</c> to the sticky harness's verdict, and to the motion harness's
    /// <c>checks.every(...)</c>, made both report <c>PASS</c> against a genuinely broken sticky
    /// strip and against the motion harness's own reduced-motion inversion — and every entry here
    /// stayed green, because <c>|| true</c> is textually distinct from the banned literal
    /// <c>say(true</c>. <b>It still catches the lazy spelling, and it is cheap, so it stays</b> —
    /// but the spelling space a hard-coded verdict can take is unbounded, and no denylist closes
    /// an unbounded space. <b>The real guarantee is the twin</b>: every harness with an honest
    /// negative control is proved to fail on a genuinely broken target, in a browser, every run —
    /// see the docstring on <see cref="ShippedScript"/> and <c>docs/notes/s4-harness.md</c>.</para>
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
        // Same trap, same fix: a harness that defines its own ppSlider is proofing a copy.
        ["proof-slider.html"] = ["window.ppSlider = {"],
        // The twins drive the identical harness script — see ShippedScript's docstring — so the
        // same denylist entries apply to them.
        ["proof-align.html"] = ["say(true"],
        ["proof-align-broken.html"] = ["say(true"],
        ["proof-sticky-broken.html"] = ["say(true"],
        ["proof-motion-broken.html"] = ["window.ppMotion = {"],
        ["proof-motion-node-broken.html"] = ["window.ppMotion = {"],
        ["proof-slider-broken.html"] = ["window.ppSlider = {"],
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
