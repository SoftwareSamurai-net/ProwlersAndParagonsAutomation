using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using ProwlersAndParagonsAutomation.Engine;
using Bunit;
using ProwlersAndParagonsAutomation.Web.Components;
using ProwlersAndParagonsAutomation.Web.Pages;
using ProwlersAndParagonsAutomation.Web.Layout;

namespace ProwlersAndParagons.Web.Tests;

/// <summary>
/// Nothing the stylesheet sets in capitals may carry notation the rulebook writes in mixed
/// case — a rank is <c>12d</c> and a citation is <c>Ch.6</c>, never <c>12D</c> or <c>CH.6</c>.
///
/// <para><b>This is a class of bug, not an instance, and the redesign hit it twice.</b> A tier
/// card set its consequence line in capitals and printed <c>TRAIT CAP 12D</c>; the form labels
/// were set in capitals and turned "Custom features (Ch.6, p.93)" into <c>CH.6, P.93</c>. Both
/// were found by looking at a rendered page, and neither was visible to any test: the markup is
/// identical either way and the CSS rule reads as a perfectly ordinary label treatment.</para>
///
/// <para><b>It has to cross the two halves to see it.</b> The selectors come from the
/// stylesheet — whatever it actually uppercases today, not a list somebody remembered to
/// update — and the text comes from the rendered page. A new uppercased rule over an element
/// that happens to carry a rank fails the day it is written.</para>
/// </summary>
public sealed class UppercasedTextTests
{
    /// <summary>A rank as the rulebook writes it: a number, then a lower-case d.</summary>
    private static readonly Regex Rank =
        new(@"\b\d+d\b", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>A chapter or page citation: Ch.2, p.17.</summary>
    private static readonly Regex Citation =
        new(@"\b(Ch|p|pp)\.\s*\d", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Every selector <c>app.css</c> sets in capitals, read out of the stylesheet rather than
    /// listed here. A list would go stale the first time somebody adds a rule, which is the
    /// failure this whole file exists to catch.
    /// </summary>
    public static TheoryData<string> UppercasedSelectors()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "web", "wwwroot", "css", "app.css"));

        // Comments first: one of them contains the words "text-transform: uppercase" while
        // explaining why a rule does not use it, which would otherwise read as a selector.
        css = new Regex(@"/\*.*?\*/", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Replace(css, " ");

        var rules = new Regex(@"([^{}]+)\{([^{}]*)\}", RegexOptions.None, TimeSpan.FromSeconds(5));

        var data = new TheoryData<string>();

        foreach (Match rule in rules.Matches(css))
        {
            if (!rule.Groups[2].Value.Contains("text-transform: uppercase", StringComparison.Ordinal)
                && !rule.Groups[2].Value.Contains("text-transform:uppercase", StringComparison.Ordinal))
                continue;

            foreach (var selector in rule.Groups[1].Value.Split(','))
            {
                var trimmed = selector.Trim();

                // Pseudo-elements and at-rule preludes are not queryable, and `::after`
                // content is decoration rather than text a reader is given.
                if (trimmed.Length == 0 || trimmed.StartsWith('@') || trimmed.Contains("::", StringComparison.Ordinal))
                    continue;

                data.Add(trimmed);
            }
        }

        return data;
    }

    /// <summary>
    /// The rendered pages, with a character loaded so every section has something in it. An
    /// empty sheet would satisfy this test by rendering nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(UppercasedSelectors))]
    public async Task NothingSetInCapitalsCarriesARankOrACitation(string selector)
    {
        await using var ctx = new RenderContext().With(SheetMode.Hero);

        // The Hero sample costs about 105, so putting it on the 75-point tier drives the budget
        // strip's over-budget branch — which carries `.over-text`, and which nothing else here
        // renders. A branch that only appears when something is wrong is exactly the branch a
        // test set built from the happy path never reaches.
        await using var over = new RenderContext().With(SheetMode.Hero);
        over.Session.Sheet.SelectedTierId = "street_level";

        // A Power editor belonging to somebody signed in, because the book's own entry — and so
        // `.book-toggle` — renders for nobody else. Without this the selector is reachable on no
        // page in the list, which this theory refuses rather than exempts: a guard that grows
        // subjects without growing coverage is worth less each time.
        await using var signedIn = new RenderContext().With(SheetMode.Hero);
        signedIn.Api.SignedIn = ("acct-7", "player");
        signedIn.Api.Book["Armor"] = "Self • Half Toughness • 1 Hero Point per rank\n"
            + "Armor reduces the damage you take, as described on p.21.";

        var armor = signedIn.Services.GetRequiredService<RulesRepository>().Powers
            .Single(p => p.Id == "armor");

        // **A manager holding more than one character**, because two of its labels appear on no
        // other page in this list: the caption over the other characters, and the mark on the one
        // that is open. The caption in particular is drawn only once there is a second character to
        // caption — so without this the sweep would find that selector nowhere and this theory
        // would refuse it, which is the positive control at the foot working exactly as intended.
        // **Signed in before the sample is loaded, and the order is load-bearing.** `Accounts`
        // resolves who is here once and holds the answer; anything that touches the session first
        // settles that question as "nobody", and the panel then reads the browser's empty local
        // store instead of this account's two characters.
        await using var holding = new RenderContext();
        holding.Api.SignedIn = ("acct-11", "player");
        holding.With(SheetMode.Hero);

        var account = holding.Services.GetRequiredService<ApiCharacterStore>();
        await account.SaveAsync(
            SavedCharacters.NewId(), "Ninth Precinct", SampleCharacters.Hero(), SheetMode.Hero);
        await account.SaveAsync(
            SavedCharacters.NewId(), "The Quiet Hour", SampleCharacters.Villain(), SheetMode.Villain);

        // **Waited for, not assumed.** The panel reads its list in `OnInitializedAsync`, so the
        // first render draws the character on screen and no list at all; the caption this page is
        // here to expose arrives on the render after that.
        var manager = holding.Render<CharacterManager>();
        await manager.WaitForElementAsync(".others-head");

        var pages = new List<IRenderedComponent<Microsoft.AspNetCore.Components.IComponent>>
        {
            manager,

            signedIn.Render<PowerEditor>(p => p.Add(e => e.Power, armor)),

            // A passage of the book, drawn open. `PowerEditor` above reaches `.book-toggle` and
            // stops there — `RulebookEntry` renders the passage itself only once somebody opens
            // it, so the stat line's own labels and the PRO/CON markers are two more interactions
            // deep than any page in this list goes. The text is a Power's entry with an option in
            // it, because that is the shape carrying both.
            ctx.Render<BookText>(p => p.Add(
                b => b.Text,
                "Self • Power Rank • 2 Hero Points per rank You are incredibly lucky. "
                + "PRO Control (+4): You can alter probability fields.")),
            over.Render<HpBudgetBar>(),
            ctx.Render<ChooseTier>(),
            ctx.Render<AbilitiesTab>(),
            ctx.Render<TalentsTab>(),
            ctx.Render<PowersTab>(),
            ctx.Render<PerksTab>(),
            ctx.Render<FlawsTab>(),
            ctx.Render<Derived>(),
            ctx.Render<Gear>(),
            ctx.Render<Finishing>(),
            ctx.Render<Review>(),
            ctx.Render<Characteristics>(),
            ctx.Render<SheetView>(),
            ctx.Render<HpBudgetBar>(),
            ctx.Render<StepNav>(),

            // The front door. It is rendered from a context with a character already on the
            // sheet, because that is the branch carrying a figure — the empty one shows a
            // catalogue count and would leave the spend's label unexercised.
            over.Render<Home>(),

            // **The banner, which four of the exemptions below used to stand in for.** They read
            // "MainLayout, which needs a Body fragment and a router", and that was not true: the
            // layout renders on its own — `BannerTests` has rendered it since the day it was
            // written — with `Body` left null and every band drawn. Four uppercased selectors sat
            // unchecked behind a reason nobody re-read, which is the same shape as the thirteen
            // this test's own closing assertion was added to catch.
            //
            // From `over`, so the subtitle names a character rather than the front door.
            over.Render<MainLayout>(),

            // **The same layout with its settings menu open, because two uppercased selectors
            // are behind that disclosure now and neither is reachable without it.**
            // `.mode-switch button` and `.theme-switch button` were on the band and were
            // reached by the render above; the palette switches moved into a menu, and a render
            // that stops at the shut state would leave this theory refusing both — which is the
            // positive control at the foot doing its job, and the right answer to it is to open
            // the menu rather than to write two more exemptions. This file already records four
            // exemptions that had been false since the day they were written.
            OpenedSettings(over),
        };

        var seen = 0;

        foreach (var page in pages)
        {
            foreach (var element in page.FindAll(selector))
            {
                seen++;

                // **What a reader sees, not `TextContent`.** The sheet explains every name on it
                // now, so a Trait cell set in capitals also holds two hidden copies of that Trait's
                // description — and a description that happens to say "1d=50 lbs" would fail this
                // for text nobody can see. The rule is about what is *set* in capitals.
                var text = SheetText.Visible(element);

                Assert.False(Rank.IsMatch(text),
                    $"'{selector}' is set in capitals and carries a rank: \"{Collapse(text)}\". "
                    + "The rulebook writes a rank as 12d; in capitals it reads 12D.");

                Assert.False(Citation.IsMatch(text),
                    $"'{selector}' is set in capitals and carries a rulebook citation: "
                    + $"\"{Collapse(text)}\". In capitals it reads CH.6, which is not how the "
                    + "rulebook cites itself.");

                // **And the words printed inside a form control, which the sweep above cannot
                // see.** See `PlaceholdersIn` for why they are read separately and why they are
                // read at all.
                foreach (var word in PlaceholdersIn(element))
                {
                    Assert.False(Rank.IsMatch(word),
                        $"'{selector}' is set in capitals and holds a field whose placeholder "
                        + $"carries a rank: \"{Collapse(word)}\". The rulebook writes a rank as "
                        + "12d; in capitals it reads 12D.");

                    Assert.False(Citation.IsMatch(word),
                        $"'{selector}' is set in capitals and holds a field whose placeholder "
                        + $"carries a rulebook citation: \"{Collapse(word)}\". In capitals it "
                        + "reads CH.6, which is not how the rulebook cites itself.");
                }
            }
        }

        // **The selector has to have been found somewhere, or this theory asserted nothing.**
        // The first version closed with `Assert.True(seen >= 0)` — a tautology — and **13 of the
        // 25 uppercased selectors matched nothing on any rendered page**, including `.verdict`,
        // `.steps a`, `.tabs button` and all three the budget strip had just added. Setting
        // `.verdict` to "Legal, Trait Cap 12d (Ch.9, p.150)" rendered LEGAL, TRAIT CAP 12D
        // (CH.9, P.150) with the whole suite green — precisely the bug class this file exists
        // for. A guard that grows subjects without growing coverage is worth less each time.
        //
        // Anything genuinely unreachable is named here with the reason, so the exemption is a
        // decision rather than a silence.
        string[] unreachable =
        [
            ".boot-title",   // index.html's pre-WebAssembly screen; no component renders it.
            ".replay-who",   // a recorded turn; ReplayRenderTests covers that surface.
            ".sheet-footer", // print-only; `display: none` on screen.
            ".panel.replay-label b:first-child", // the replay notice; same surface as above.
            "h4"             // in the shared heading rule; no component renders one today.
        ];

        if (unreachable.Contains(selector, StringComparer.Ordinal)) return;

        Assert.True(seen > 0,
            $"'{selector}' is set in capitals and this test found it on no page, so it asserted "
            + "nothing about it. Render a page that shows it, or name it in `unreachable` with "
            + "a reason.");
    }

    /// <summary>
    /// The visible words printed inside a form control under (or at) this element.
    ///
    /// <para><b>The sweep above cannot see them, and the banner is where that stopped being
    /// hypothetical.</b> Its search control is an <c>&lt;input&gt;</c> now, and an input holds no
    /// text: <c>SheetText.Visible</c> on the tool that wraps it returns the two key boxes and
    /// nothing else, so the one word a reader actually sees there — <c>SEARCH</c> — was swept by
    /// nothing at all. A control whose only visible word is its placeholder is exactly the shape
    /// this file's rule is about.</para>
    ///
    /// <para><b>It reads inputs under an uppercased selector rather than inputs the stylesheet
    /// uppercases directly, and that is deliberately the wider net.</b> The capitals on this one
    /// are set on <c>::placeholder</c>, which the theory's own selector list skips because a
    /// pseudo-element is not queryable — so a rule keyed to the input itself would find nothing.
    /// The cost of the wider net is a placeholder that is <em>not</em> uppercased being checked
    /// anyway; the failure message says what it read, and a placeholder carrying <c>12d</c> or
    /// <c>Ch.6</c> is worth a second look under either rule.</para>
    /// </summary>
    private static IEnumerable<string> PlaceholdersIn(IElement element)
    {
        const string boxes = "input[placeholder], textarea[placeholder]";

        var found = element.Matches(boxes)
            ? [element, .. element.QuerySelectorAll(boxes)]
            : element.QuerySelectorAll(boxes).ToList();

        return found.Select(e => e.GetAttribute("placeholder") ?? "")
                    .Where(word => word.Length > 0);
    }

    /// <summary>
    /// <b>The positive control on the sweep above: the banner's field really is read.</b>
    ///
    /// <para>A helper that found no placeholder anywhere would leave every assertion it feeds
    /// asserting nothing, and the theory would stay green — which is this repository's most common
    /// way for a guard to be wrong and the reason that theory closes with a count of its own. This
    /// names the one control the extension was written for and asserts the word comes back through
    /// the same path, from the same selector the stylesheet uppercases.</para>
    /// </summary>
    [Fact]
    public void TheSweepReadsTheBannerFieldsPlaceholder()
    {
        using var ctx = new RenderContext();

        var layout = ctx.Render<MainLayout>();

        // `.banner-tool` is one of the selectors `app.css` sets in capitals, and the field is drawn
        // inside the one that opens the palette. Read the same way the theory reads it.
        var words = layout.FindAll(".banner-tool").SelectMany(PlaceholdersIn).ToList();

        Assert.Contains("Search", words);
    }

    /// <summary>
    /// The same rule for form labels, read from the source rather than from a rendered page.
    ///
    /// <para><b>This half exists because the rendered half could not see the case that caused
    /// it.</b> The label that reads "Custom features (Ch.6, p.93)" sits inside a per-item panel
    /// on the gear step, and rendering that page with a character loaded produces <i>zero</i>
    /// labels — the state those labels live in is several interactions deep. Reinstating the
    /// uppercase rule on <c>label</c> left the theory above green, which is the exact shape of
    /// "a runtime test is only worth the paths it drives".</para>
    ///
    /// <para>So this reads every label text the app can produce, and is conditional on the
    /// stylesheet: if <c>label</c> stops being uppercased the constraint lifts, and if somebody
    /// uppercases it again this fails on the strings that are already there.</para>
    /// </summary>
    [Fact]
    public void NoFormLabelCarriesARankOrACitationWhileLabelsAreUppercased()
    {
        var web = Path.Combine(RepoRoot(), "web");
        var css = File.ReadAllText(Path.Combine(web, "wwwroot", "css", "app.css"));

        var rule = new Regex(@"(?<![\w.-])label\s*\{([^{}]*)\}", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Match(new Regex(@"/\*.*?\*/", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
                .Replace(css, " "));

        Assert.True(rule.Success, "app.css no longer styles the label element at all.");

        if (!rule.Groups[1].Value.Contains("text-transform", StringComparison.Ordinal)) return;

        var literal = new Regex(@"Label\s*=\s*""([^""@]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var file in Directory.GetFiles(web, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (Match match in literal.Matches(File.ReadAllText(file)))
            {
                var text = match.Groups[1].Value;

                Assert.False(Rank.IsMatch(text),
                    $"{Path.GetFileName(file)} labels a field \"{text}\" while app.css sets every "
                    + "label in capitals, so the rank reads 12D.");

                Assert.False(Citation.IsMatch(text),
                    $"{Path.GetFileName(file)} labels a field \"{text}\" while app.css sets every "
                    + "label in capitals, so the citation reads CH.6.");
            }
        }
    }

    /// <summary>
    /// The layout with its settings menu disclosed.
    ///
    /// <para>The click is asserted to have done something before the page joins the list. A menu
    /// that rendered nothing would contribute no elements, which reads here exactly like a
    /// selector nobody uppercased — and the theory would then fail on the wrong thing, or, worse,
    /// be quieted with an exemption naming a reason that was never true.</para>
    /// </summary>
    private static IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> OpenedSettings(
        RenderContext ctx)
    {
        var layout = ctx.Render<MainLayout>();

        layout.Find(".settings-open").Click();
        Assert.Single(layout.FindAll(".settings-menu-list"));

        return layout;
    }

    private static string Collapse(string text) =>
        new Regex(@"\s+", RegexOptions.None, TimeSpan.FromSeconds(5)).Replace(text, " ").Trim();

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

