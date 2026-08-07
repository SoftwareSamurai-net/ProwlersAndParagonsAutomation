using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The browser front end's presentation rules, asserted against its own source.
///
/// These are not unit tests of behaviour — they are the three disciplines the front end is
/// built on, each of which has already been broken once and none of which any compiler can
/// see:
///
/// <list type="number">
///   <item>No component names a colour. That is what keeps the Hero/Villain switch a
///     single attribute and lets print force a light palette with one media query.</item>
///   <item>Nothing on screen names an internal type or a build command. The app is for
///     players at a table. Rulebook references are the opposite and are wanted.</item>
///   <item>The repeated markup stays behind its component. Twenty-two hand-rolled panels
///     is twenty-two places for the next styling change to miss one.</item>
/// </list>
///
/// They read files rather than render components: adding a Blazor rendering harness to a
/// suite that currently has no front-end dependency is a bigger change than the rules it
/// would check, and these rules are all statements about the source anyway.
/// </summary>
public sealed class WebPresentationTests
{
    private static string WebRoot => Path.Combine(RulesFixture.RepoRoot, "web");

    private static IReadOnlyList<string> RazorFiles =>
        Directory.GetFiles(WebRoot, "*.razor", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string AppCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "app.css"));
    private static string ThemeCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "theme.css"));

    // ── 1. No component names a colour ──────────────────────────────────────────

    /// <summary>
    /// theme.css is the one file allowed to name a colour. Everything else asks it for one
    /// by token, which is why switching mode is a single attribute on the document element
    /// and why print can restate the palette without touching a component.
    /// </summary>
    [Fact]
    public void NoComponentNamesAColour()
    {
        var hex = new Regex(@"#[0-9A-Fa-f]{3,8}\b", RegexOptions.None, TimeSpan.FromSeconds(5));
        var keyword = new Regex(@"\bcolor:\s*(red|blue|green|white|black|grey|gray)\b",
            RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

        foreach (var file in RazorFiles)
        {
            var text = File.ReadAllText(file);
            Assert.False(hex.IsMatch(text), $"{Path.GetFileName(file)} names a colour by hex value.");
            Assert.False(keyword.IsMatch(text), $"{Path.GetFileName(file)} names a colour keyword.");
        }

        Assert.False(hex.IsMatch(AppCss), "app.css names a colour by hex value; it belongs in theme.css.");
        Assert.False(keyword.IsMatch(AppCss), "app.css names a colour keyword; it belongs in theme.css.");
    }

    /// <summary>
    /// A function-syntax colour is the loophole the hex check does not close. `transparent`
    /// is not a colour in this sense — it is the absence of one, and a placeholder border
    /// that becomes visible on hover needs it.
    /// </summary>
    [Fact]
    public void AppCssStatesNoColourInFunctionSyntaxEither()
    {
        var literal = new Regex(@"\b(rgba?|hsla?|oklch|lab)\s*\(", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
        Assert.False(literal.IsMatch(AppCss),
            "app.css builds a colour from raw channel values. Add a token to theme.css instead.");
    }

    /// <summary>
    /// Paper has no dark mode. Without this block a Villain sheet prints its near-black
    /// surface edge to edge, which is unreadable and empties a cartridge.
    /// </summary>
    [Fact]
    public void PrintForcesTheLightPaletteForBothModes()
    {
        var print = PrintBlockOf(ThemeCss);

        Assert.Contains(":root[data-mode=\"villain\"]", print, StringComparison.Ordinal);
        Assert.Contains(":root[data-mode=\"hero\"]", print, StringComparison.Ordinal);

        // Every token the two palettes declare has to be restated, or a mode's value
        // survives into print through the cascade.
        foreach (var token in new[]
                 {
                     "--surface", "--panel", "--ink", "--heading", "--primary", "--on-primary",
                     "--accent", "--danger", "--rule", "--muted", "--panel-sunk",
                     "--accent-soft", "--danger-soft", "--shadow"
                 })
        {
            Assert.Contains($"{token}:", print, StringComparison.Ordinal);
        }

        Assert.Contains("--surface:      #FFFFFF", print, StringComparison.Ordinal);
        Assert.Contains("--panel:        #FFFFFF", print, StringComparison.Ordinal);
    }

    /// <summary>
    /// Durations are tokens so that one media query can switch the whole app to no motion
    /// for anyone whose system asks for it. A hard-coded duration in app.css would ignore
    /// that request silently.
    /// </summary>
    [Fact]
    public void MotionIsTokenisedAndCanBeTurnedOffWholesale()
    {
        Assert.Contains("prefers-reduced-motion: reduce", ThemeCss, StringComparison.Ordinal);

        var duration = new Regex(@":\s*[\d.]+m?s\b", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.False(duration.IsMatch(AppCss),
            "app.css states a duration outright. Use --step / --swap / --enter so "
            + "prefers-reduced-motion can switch it off.");
    }

    // ── 2. The UI is written for players ────────────────────────────────────────

    /// <summary>
    /// The four offenders this rule was written for all presented an internal name the same
    /// way — inside a &lt;code&gt; element in a paragraph. A player has no use for the name
    /// of the class that produced their sheet, and a monospace run of it on the page is the
    /// tell every time.
    /// </summary>
    [Fact]
    public void NoPageShowsCodeToThePlayer()
    {
        foreach (var file in RazorFiles)
        {
            Assert.DoesNotContain("<code>", VisibleMarkup(File.ReadAllText(file)), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Phrases that could only be prose, so finding one means it is on the page rather than
    /// in an expression. Chapter and page references are deliberately absent from this
    /// list: those are what a player actually wants and the app should keep printing them.
    /// </summary>
    [Theory]
    [InlineData("terminal wizard")]
    [InlineData("dotnet run")]
    [InlineData("dotnet build")]
    [InlineData("byte-for-byte")]
    [InlineData("sheets layer")]
    [InlineData("the same compiled code")]
    [InlineData("shared engine")]
    public void NoPageExplainsItselfToADeveloper(string phrase)
    {
        foreach (var file in RazorFiles)
        {
            var visible = VisibleMarkup(File.ReadAllText(file));
            Assert.False(
                visible.Contains(phrase, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(file)} says \"{phrase}\" to the player. Keep it in a comment.");
        }
    }

    /// <summary>
    /// The other half of the same rule, and the reason it is not simply "delete the
    /// jargon": the rulebook references are the app's best feature. A player choosing a
    /// gear feature wants to be told it is Ch.6 p.92.
    /// </summary>
    [Fact]
    public void RulebookReferencesAreStillOnThePage()
    {
        var everything = string.Concat(RazorFiles.Select(File.ReadAllText));

        Assert.Contains("Ch.6", everything, StringComparison.Ordinal);
        Assert.Contains("Ch.9", everything, StringComparison.Ordinal);
        Assert.Contains("Trait Cap", everything, StringComparison.Ordinal);
        Assert.Contains("Hero Point", everything, StringComparison.Ordinal);
    }

    // ── 3. Repeated markup stays behind its component ───────────────────────────

    /// <summary>
    /// Each of these was hand-written between five and twenty-two times before it had a
    /// component. The print stylesheet is what made that expensive: ruled boxes and
    /// break-inside rules had to reach every one of them at once.
    /// </summary>
    [Theory]
    [InlineData("panel", "Panel.razor")]
    [InlineData("panel-head", "Panel.razor")]
    [InlineData("field", "Field.razor")]
    [InlineData("sheet-section", "SheetSection.razor")]
    [InlineData("stat-block", "StatBlock.razor")]
    [InlineData("stat-blocks", "DerivedStatBlocks.razor")]
    [InlineData("chosen", "ChosenList.razor")]
    [InlineData("options", "OptionList.razor")]
    [InlineData("option", "OptionRow.razor")]
    public void OnlyOneComponentWritesEachRepeatedClass(string cssClass, string owner)
    {
        var pattern = new Regex($@"class=""{Regex.Escape(cssClass)}(\s|""|\s*@)",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        foreach (var file in RazorFiles.Where(f => Path.GetFileName(f) != owner))
        {
            Assert.False(
                pattern.IsMatch(File.ReadAllText(file)),
                $"{Path.GetFileName(file)} writes class=\"{cssClass}\" itself. Use <{Path.GetFileNameWithoutExtension(owner)}>.");
        }
    }

    /// <summary>
    /// The budget bar's fill width is a live number and has to arrive as an inline style —
    /// it is the reason the Content-Security-Policy carries `style-src 'unsafe-inline'`.
    /// It is the only one, and that is worth keeping true: every other inline style is a
    /// rule that belongs in app.css.
    /// </summary>
    [Fact]
    public void TheOnlyInlineStyleIsTheBudgetBarsLiveWidth()
    {
        var offenders = RazorFiles
            .Where(f => File.ReadAllText(f).Contains("style=\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["HpBudgetBar.razor"], offenders);
    }

    // ── The printed sheet ───────────────────────────────────────────────────────

    /// <summary>
    /// The print stylesheet was three lines that hid the navigation, which is how a sheet
    /// came to print with no margins, no boxes and entries cut in half by a page boundary.
    /// Each rule below is one of those faults.
    /// </summary>
    [Fact]
    public void ThePrintedSheetHasAPageAPaletteAndBreakControl()
    {
        var print = PrintBlockOf(AppCss);

        Assert.Contains("@page", AppCss, StringComparison.Ordinal);
        Assert.Contains("size: A4", AppCss, StringComparison.Ordinal);

        // Ruled boxes and their headings.
        Assert.Contains(".sheet-section", print, StringComparison.Ordinal);
        Assert.Contains("break-after: avoid", print, StringComparison.Ordinal);

        // Nothing a reader follows down the page may be cut in half.
        foreach (var unit in new[] { ".power-entry", ".stat-block", ".stat-table tr", ".chosen > li" })
        {
            Assert.Contains(unit, print, StringComparison.Ordinal);
        }

        Assert.Contains("break-inside: avoid", print, StringComparison.Ordinal);
        Assert.Contains("orphans: 3", print, StringComparison.Ordinal);
        Assert.Contains("widows: 3", print, StringComparison.Ordinal);
    }

    /// <summary>
    /// Working UI is not sheet content. A printed sheet carrying the step navigation and
    /// the budget bar is a screenshot, not a character sheet.
    /// </summary>
    [Theory]
    [InlineData(".banner")]
    [InlineData(".steps")]
    [InlineData(".nav-buttons")]
    [InlineData(".budget")]
    [InlineData(".tabs")]
    [InlineData(".mode-switch")]
    [InlineData(".no-print")]
    public void ThePrintedSheetLeavesOutTheToolAroundIt(string selector)
    {
        Assert.Contains(selector, PrintBlockOf(AppCss), StringComparison.Ordinal);
    }

    /// <summary>
    /// The review page's title leads with the character's name because the browser prints
    /// the document title in its own page header — the one thing that reliably repeats on
    /// every page. A `position: fixed` running footer does not: Chrome renders it once, at
    /// the top of page two.
    /// </summary>
    [Fact]
    public void TheReviewPageTitleLeadsWithTheCharactersName()
    {
        var review = File.ReadAllText(Path.Combine(WebRoot, "Pages", "Review.razor"));
        var title = new Regex(@"<PageTitle>(.*?)</PageTitle>", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Match(review);

        Assert.True(title.Success, "Review.razor has no <PageTitle>.");
        Assert.Contains("Session.Sheet.Name", title.Groups[1].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("position: fixed", PrintBlockOf(AppCss), StringComparison.Ordinal);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .razor file with the parts a player never sees removed: Razor comments, and the
    /// <c>@code</c> block, where engineering detail belongs and is wanted.
    /// </summary>
    private static string VisibleMarkup(string razor)
    {
        var withoutComments = new Regex(@"@\*.*?\*@", RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Replace(razor, " ");

        var code = withoutComments.IndexOf("\n@code", StringComparison.Ordinal);
        return code < 0 ? withoutComments : withoutComments[..code];
    }

    /// <summary>
    /// The contents of the file's <c>@media print</c> block. Brace-counted rather than
    /// matched by regex, because the block contains nested rules.
    /// </summary>
    private static string PrintBlockOf(string css)
    {
        var start = css.IndexOf("@media print", StringComparison.Ordinal);
        Assert.True(start >= 0, "No @media print block.");

        var open = css.IndexOf('{', start);
        var depth = 0;

        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}' && --depth == 0) return css[(open + 1)..i];
        }

        throw new InvalidOperationException("The @media print block is not closed.");
    }
}
