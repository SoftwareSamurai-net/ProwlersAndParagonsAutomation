using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The browser front end's presentation rules, asserted against its own source.
///
/// <para>These are not unit tests of behaviour. They are the disciplines the front end is
/// built on, each of which has already been broken once and none of which any compiler can
/// see: no component names a colour; nothing on screen names an internal type or a build
/// command, while the rulebook references stay; each repeated class has one owner; and the
/// printed sheet has a page, a light palette and break control.</para>
///
/// <para><b>The first version of this file was mostly theatre, and knowing how is the point
/// of the comments below.</b> An adversarial pass applied thirteen violations at once —
/// including white ink on white paper, every page-break rule flipped to `auto`, the whole
/// working UI un-hidden, a second `@media print` block undoing the first, and the "Armor8d"
/// bug reinstated — and all of it passed. The rewrite that followed is why the helpers here
/// look paranoid: substring checks against a whole file are almost always satisfied by
/// something other than the thing being tested.</para>
///
/// <para>One gap is structural and stated rather than papered over: everything here reads
/// source. Nothing renders a component, so no test in this file can catch a bug in rendered
/// output — "Armor8d" itself would walk straight past. Closing that needs bUnit; see
/// PROGRESS.md.</para>
/// </summary>
public sealed class WebPresentationTests
{
    private static string WebRoot => Path.Combine(RulesFixture.RepoRoot, "web");

    private static IReadOnlyList<string> RazorFiles => SourceFiles(WebRoot, "*.razor");

    private static string AppCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "app.css"));
    private static string ThemeCss => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "css", "theme.css"));

    private static List<string> SourceFiles(string root, string pattern) =>
        Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static Regex Rx(string pattern, RegexOptions options = RegexOptions.None) =>
        new(pattern, options, TimeSpan.FromSeconds(5));

    /// <summary>The projects whose type names must never reach the page.</summary>
    private static readonly string[] ProjectsWithTypes = ["engine", "sheets"];

    // ── No component names a colour ─────────────────────────────────────────────

    /// <summary>
    /// theme.css is the one file allowed to name a colour. Everything else asks it for one
    /// by token, which is what keeps the Hero/Villain switch a single attribute on the
    /// document element and lets print restate the whole palette without touching a
    /// component.
    ///
    /// <para>All three spellings are checked in <b>both</b> app.css and the components. An
    /// earlier version checked hex everywhere but channel functions only in app.css, and a
    /// <c>&lt;style&gt;</c> block dropped into a component walked through carrying
    /// <c>rgb()</c> and <c>hsl()</c>.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("razor")]
    public void NoComponentNamesAColour(string what)
    {
        var hex = Rx(@"#[0-9A-Fa-f]{3,8}\b");
        var keyword = Rx(@":\s*(red|blue|green|white|black|grey|gray|yellow|orange|purple)\b", RegexOptions.IgnoreCase);
        var channels = Rx(@"\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\s*\(", RegexOptions.IgnoreCase);

        var sources = what == "app.css"
            ? [("app.css", Scannable(AppCss, css: true))]
            : RazorFiles.Select(f => (Path.GetFileName(f), Scannable(File.ReadAllText(f), css: false))).ToList();

        foreach (var (name, text) in sources)
        {
            Assert.False(hex.IsMatch(text), $"{name} names a colour by hex value.");
            Assert.False(keyword.IsMatch(text), $"{name} names a colour keyword.");
            Assert.False(channels.IsMatch(text),
                $"{name} builds a colour from raw channel values. Add a token to theme.css instead.");
        }
    }

    /// <summary>
    /// What the colour scan has to ignore, and why each exclusion is safe.
    ///
    /// <list type="bullet">
    ///   <item>Comments. A comment saying "never write rgb() here" failed the test that
    ///     comment exists to explain.</item>
    ///   <item>Numeric HTML entities. <c>&amp;#8212;</c> — an em dash — is four hex-looking
    ///     digits behind a hash, and reads as a colour to the regex.</item>
    ///   <item><c>color-mix()</c>, the one channel function this codebase uses. It mixes
    ///     tokens; its arguments are <c>var(…)</c> or <c>transparent</c>, never a literal,
    ///     and any literal inside one is still caught because only the function name is
    ///     masked.</item>
    /// </list>
    /// </summary>
    private static string Scannable(string text, bool css)
    {
        var stripped = css
            ? Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(text, " ")
            : Rx(@"@\*.*?\*@", RegexOptions.Singleline).Replace(text, " ");

        stripped = Rx("&#x?[0-9A-Fa-f]+;").Replace(stripped, " ");
        return Rx(@"\bcolor-mix\s*\(", RegexOptions.IgnoreCase).Replace(stripped, "MIX(");
    }

    /// <summary>
    /// The same rule from the other side. A component carrying its own
    /// <c>&lt;style&gt;</c> element can style anything without going near app.css, which is
    /// where every rule above stops applying.
    /// </summary>
    [Fact]
    public void NoComponentCarriesItsOwnStylesheet() =>
        Assert.All(RazorFiles, f =>
            Assert.DoesNotContain("<style", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Paper is white and ink is dark enough to read. That is the whole rule, and it is the
    /// one a Villain sheet used to break: on the screen palette it printed its near-black
    /// surface edge to edge — unreadable, and it empties a cartridge per character.
    ///
    /// <para>It is <b>not</b> a rule that everything prints grey. The published sheet is in
    /// full colour and the two modes keep their own headings, rules and heading bars; what
    /// they may not do is darken the paper or lighten the ink.</para>
    ///
    /// <para>The <b>values</b> are checked, not the presence of the token. Asserting only
    /// that <c>--ink</c> is declared lets it be declared white, and white ink on white paper
    /// prints blank pages that look like a printer fault rather than a bug.</para>
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void PrintKeepsThePaperWhiteAndTheInkReadable(string mode)
    {
        var print = OnlyPrintBlockOf(ThemeCss);

        Assert.Contains($":root[data-mode=\"{mode}\"]", print, StringComparison.Ordinal);

        // Resolved the way the cascade resolves it: the shared block first, then the mode's.
        var palette = PrintPalette(print, mode);

        // Paper, and anything that sits behind body text.
        foreach (var token in new[] { "--surface", "--panel", "--panel-sunk", "--primary", "--accent-soft" })
            Assert.True(Luminance(palette[token]) > 0.85,
                $"print {token} darkens the paper in {mode} mode ({palette[token]}).");

        // Ink. 0.45 is about a 4.5:1 contrast floor against white, which is what the small
        // print on this sheet needs.
        foreach (var token in new[] { "--ink", "--heading", "--rule", "--accent", "--on-primary", "--muted", "--danger" })
            Assert.True(Luminance(palette[token]) < 0.45,
                $"print {token} is too pale to read on white in {mode} mode ({palette[token]}).");
    }

    /// <summary>
    /// Every token the two screen palettes declare has to be resolved by the print block for
    /// each mode, whether from the shared rule or the mode's own. A token left out keeps its
    /// screen value through the cascade, which is exactly how the near-black page happened.
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void PrintRestatesEveryTokenTheScreenPalettesDeclare(string mode)
    {
        // The two mode palettes only. The shape and motion tokens in the plain `:root` block
        // are not colours and print has no reason to restate a transition duration.
        var screen = ThemeCss[..ThemeCss.IndexOf("@media print", StringComparison.Ordinal)];

        var declared = Rx(@"data-mode=""(hero|villain)""\s*\]?\s*\{([^}]*)\}", RegexOptions.Singleline)
            .Matches(screen)
            .SelectMany(m => Rx(@"(--[a-z-]+)\s*:").Matches(m.Groups[2].Value).Select(d => d.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);

        var palette = PrintPalette(OnlyPrintBlockOf(ThemeCss), mode);

        foreach (var token in declared)
            Assert.True(palette.ContainsKey(token),
                $"print does not restate {token} for {mode}, so its screen value survives.");
    }

    /// <summary>
    /// The print block's tokens as the cascade resolves them for one mode: the unqualified
    /// rule, then the mode's own on top.
    /// </summary>
    private static Dictionary<string, string> PrintPalette(string print, string mode)
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match rule in Rx(@"([^{}]+)\{([^{}]*)\}").Matches(print))
        {
            var selectors = rule.Groups[1].Value;
            var appliesToMode = selectors.Contains($"data-mode=\"{mode}\"", StringComparison.Ordinal)
                                || !selectors.Contains("data-mode", StringComparison.Ordinal);

            if (!appliesToMode) continue;

            foreach (Match declaration in Rx(@"(--[a-z-]+)\s*:\s*([^;]+);").Matches(rule.Groups[2].Value))
                palette[declaration.Groups[1].Value] = declaration.Groups[2].Value.Trim();
        }

        return palette;
    }

    /// <summary>
    /// Durations are tokens so one media query can switch the whole app to no motion for
    /// anyone whose system asks for it.
    ///
    /// <para>The first version of this test anchored the number to the colon, which meant it
    /// could not see the shorthand — and every transition in app.css is written
    /// <c>transition: color 180ms ease</c>, so it matched none of them and would have missed
    /// a hard-coded duration in all eleven.</para>
    /// </summary>
    [Fact]
    public void MotionIsTokenisedAndCanBeTurnedOffWholesale()
    {
        Assert.Contains("prefers-reduced-motion: reduce", ThemeCss, StringComparison.Ordinal);

        var body = Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(AppCss, " ");
        var duration = Rx(@"\b\d[\d.]*m?s\b").Match(body);

        Assert.False(duration.Success,
            $"app.css states the duration '{duration.Value}' outright. Use --step / --swap / "
            + "--enter so prefers-reduced-motion can switch it off.");
    }

    // ── The UI is written for players ───────────────────────────────────────────

    /// <summary>
    /// Setting something in monospace on a page for players is, every time so far, an
    /// internal name. Matched on the tag rather than on <c>&lt;code&gt;</c> exactly, because
    /// an attribute on it changes nothing about what the reader sees.
    /// </summary>
    [Fact]
    public void NoPageSetsAnythingInMonospaceForThePlayer()
    {
        var monospace = Rx(@"<(code|kbd|samp|pre)\b", RegexOptions.IgnoreCase);

        Assert.All(RazorFiles, f =>
            Assert.False(monospace.IsMatch(VisibleMarkup(File.ReadAllText(f))),
                $"{Path.GetFileName(f)} sets text in monospace on the page."));
    }

    /// <summary>
    /// The general form of the rule, rather than a denylist of the four offenders that
    /// prompted it: no type this project declares may appear in the prose a player reads.
    ///
    /// <para>Only compound PascalCase names are considered — <c>CharacterSheetRenderer</c>,
    /// not a hypothetical <c>Tier</c> — because a single capitalised word is also English,
    /// and the app has every right to write "Power" or "Source" on the page.</para>
    /// </summary>
    [Fact]
    public void NoPageNamesATypeThisProjectDeclares()
    {
        var declared = ProjectsWithTypes
            .SelectMany(dir => SourceFiles(Path.Combine(RulesFixture.RepoRoot, dir), "*.cs"))
            .SelectMany(f => Rx(@"\b(?:class|record|interface|enum|struct)\s+([A-Z]\w+)")
                .Matches(File.ReadAllText(f))
                .Select(m => m.Groups[1].Value))
            .Where(n => Rx("[A-Z][a-z]+[A-Z]").IsMatch(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);   // a broken scan must not pass by finding nothing

        foreach (var file in RazorFiles)
        {
            var prose = VisibleText(File.ReadAllText(file));

            foreach (var type in declared)
            {
                Assert.False(
                    Rx($@"\b{Regex.Escape(type)}\b").IsMatch(prose),
                    $"{Path.GetFileName(file)} prints the type name '{type}' at the player. "
                    + "Keep it in a @* *@ comment or the @code block.");
            }
        }
    }

    /// <summary>
    /// Phrases that could only ever be prose. A player has no use for how the app is built
    /// or what it is built with.
    /// </summary>
    [Theory]
    [InlineData("terminal wizard")]
    [InlineData("dotnet")]
    [InlineData("byte-for-byte")]
    [InlineData("sheets layer")]
    [InlineData("the same compiled code")]
    [InlineData("shared engine")]
    [InlineData("blazor")]
    [InlineData("webassembly")]
    [InlineData("command line")]
    public void NoPageExplainsItselfToADeveloper(string phrase) =>
        Assert.All(RazorFiles, f =>
            Assert.False(
                VisibleText(File.ReadAllText(f)).Contains(phrase, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(f)} says \"{phrase}\" to the player. Keep it in a comment."));

    /// <summary>
    /// The other half of the same rule, and the reason it is not simply "delete the jargon":
    /// the rulebook references are the app's best feature.
    ///
    /// <para>Asserted on the prose a player reads, not on the raw file. Read raw, this test
    /// passed while <c>Ch.6</c> survived <b>only</b> in a Razor comment — the label on the
    /// gear-features picker had lost its page reference and nothing noticed.</para>
    /// </summary>
    [Theory]
    [InlineData("Ch.6")]
    [InlineData("Ch.9")]
    [InlineData("Trait Cap")]
    [InlineData("Hero Point")]
    public void RulebookReferencesAreStillOnThePage(string reference)
    {
        var prose = string.Concat(RazorFiles.Select(f => VisibleText(File.ReadAllText(f))));

        Assert.Contains(reference, prose, StringComparison.Ordinal);
    }

    // ── Repeated markup stays behind its component ──────────────────────────────

    private static readonly (string Class, string Owner)[] OwnedClasses =
    [
        ("panel", "Panel.razor"),
        ("panel-head", "Panel.razor"),
        ("field", "Field.razor"),
        ("sheet-section", "SheetSection.razor"),
        ("stat-block", "StatBlock.razor"),
        ("stat-blocks", "StatBlockRow.razor"),
        ("chosen", "ChosenList.razor"),
        ("options", "OptionList.razor"),
        ("option", "OptionRow.razor")
    ];

    public static TheoryData<string, string> Owned()
    {
        var data = new TheoryData<string, string>();
        foreach (var (cssClass, owner) in OwnedClasses) data.Add(cssClass, owner);
        return data;
    }

    /// <summary>
    /// Each of these was hand-written between five and twenty-two times before it had a
    /// component. The print stylesheet is what made that expensive: ruled boxes and
    /// break-inside rules had to reach every one of them at once.
    ///
    /// <para>The class is matched as a member of the attribute's value, not as its prefix.
    /// Anchored at <c>class="</c>, the check waved through <c>class="wrapper panel"</c> —
    /// a hand-rolled panel with a word in front of it.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Owned))]
    public void OnlyOneComponentWritesEachRepeatedClass(string cssClass, string owner)
    {
        var attribute = Rx("""class\s*=\s*(?<q>["'])(?<v>[^"']*)\k<q>""");

        foreach (var file in RazorFiles.Where(f => Path.GetFileName(f) != owner))
        {
            var offending = attribute.Matches(File.ReadAllText(file))
                .Select(m => m.Groups["v"].Value)
                .FirstOrDefault(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(cssClass, StringComparer.Ordinal));

            Assert.True(offending is null,
                $"{Path.GetFileName(file)} writes class=\"{offending}\" itself, which includes "
                + $"\"{cssClass}\". Use <{Path.GetFileNameWithoutExtension(owner)}>.");
        }
    }

    /// <summary>
    /// The other half, without which the exemption above is decorative: an owner that stops
    /// writing its class would satisfy the test by writing nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Owned))]
    public void EachOwnerActuallyWritesTheClassItOwns(string cssClass, string owner)
    {
        var source = File.ReadAllText(Path.Combine(WebRoot, "Components", owner));

        Assert.Contains($"\"{cssClass}", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The budget bar's fill width is a live number and has to arrive as an inline style —
    /// it is the reason the Content-Security-Policy carries <c>style-src 'unsafe-inline'</c>.
    /// It is the only one, and that is worth keeping true.
    /// </summary>
    [Fact]
    public void TheOnlyInlineStyleIsTheBudgetBarsLiveWidth()
    {
        // Either quote: Razor accepts both, and checking only the double-quoted form lets
        // style='…' through, which is the same concession spelled differently.
        var inline = Rx("""(?<![-\w])style\s*=\s*["'@]""", RegexOptions.IgnoreCase);

        var offenders = RazorFiles
            .Where(f => inline.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["HpBudgetBar.razor"], offenders);
    }

    // ── The printed sheet ───────────────────────────────────────────────────────

    /// <summary>
    /// One print block, so that reading it is reading all of it. A second
    /// <c>@media print</c> appended to the file undid every rule below and nothing noticed,
    /// because the helper stopped at the first.
    /// </summary>
    [Fact]
    public void ThereIsExactlyOnePrintBlockInEachStylesheet()
    {
        Assert.Single(Rx(@"@media\s+print\b").Matches(WithoutCssComments(AppCss)));
        Assert.Single(Rx(@"@media\s+print\b").Matches(WithoutCssComments(ThemeCss)));
    }

    /// <summary>The paper, stated in an actual <c>@page</c> rule rather than anywhere at all.</summary>
    [Fact]
    public void ThePageIsA4WithMargins()
    {
        var page = Rx(@"@page\s*\{([^}]*)\}").Match(WithoutCssComments(AppCss));

        Assert.True(page.Success, "app.css has no @page rule, so print uses whatever the browser guesses.");
        Assert.Contains("size: A4", Normalise(page.Groups[1].Value).Replace("size:A4", "size: A4", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Matches(@"margin:\s*[\d.]+mm", page.Groups[1].Value);
    }

    /// <summary>
    /// Nothing a reader follows down the page may be cut in half. Asserted <b>inside its own
    /// rule</b>: a free-floating search for "break-inside: avoid" anywhere in the print block
    /// was satisfied by an unrelated rule while every one of these was flipped to
    /// <c>auto</c>.
    /// </summary>
    [Theory]
    [InlineData(".power-entry")]
    [InlineData(".stat-block")]
    [InlineData(".stat-table tr")]
    [InlineData(".chosen > li")]
    [InlineData(".sheet-section")]
    public void NoUnitOfTheSheetMayBeSplitAcrossAPage(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"The print block has no rule for {selector}.");
        Assert.Contains("break-inside:avoid", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>A heading that strands at the foot of a page belongs to nothing.</summary>
    [Fact]
    public void ASectionHeadingNeverStrandsAtTheFootOfAPage()
    {
        var rule = PrintRuleFor(".sheet-section > h3");

        Assert.True(rule is not null, "The print block does not constrain where a section heading may break.");
        Assert.Contains("break-after:avoid", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// Working UI is not sheet content. Asserted with the declaration, not just the selector:
    /// the rule was flipped to <c>display: block</c> and the test still passed, because it
    /// only ever checked that the word ".banner" appeared.
    /// </summary>
    [Theory]
    [InlineData(".banner")]
    [InlineData(".steps")]
    [InlineData(".nav-buttons")]
    [InlineData(".budget")]
    [InlineData(".tabs")]
    [InlineData(".mode-switch")]
    [InlineData(".no-print")]
    [InlineData("h1")]
    public void ThePrintedSheetLeavesOutTheToolAroundIt(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"Nothing in the print block hides {selector}.");
        Assert.Contains("display:none", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// A running footer positioned with <c>position: fixed</c> looks correct and is not:
    /// Chrome's print output renders it once, at the top of page two, over the content. What
    /// carries the character's name across every page is the document title, which the
    /// browser prints in its own header — so the review page's title leads with the name.
    /// </summary>
    [Fact]
    public void TheNameTravelsByDocumentTitleAndNotByAFixedFooter()
    {
        Assert.DoesNotContain("position:fixed", Normalise(OnlyPrintBlockOf(AppCss)), StringComparison.Ordinal);

        var review = File.ReadAllText(Path.Combine(WebRoot, "Pages", "Review.razor"));
        var title = Rx("<PageTitle>(.*?)</PageTitle>", RegexOptions.Singleline).Match(review);

        Assert.True(title.Success, "Review.razor has no <PageTitle>.");

        // Leads with, not merely contains: a browser's print header is one line and gets
        // truncated, so whatever comes first is what identifies the page.
        var text = title.Groups[1].Value;
        var name = text.IndexOf("Session.Sheet.Name", StringComparison.Ordinal);
        var app = text.IndexOf("Prowlers", StringComparison.Ordinal);

        Assert.True(name >= 0, "Review.razor's title does not carry the character's name.");
        Assert.True(app < 0 || name < app,
            "Review.razor's title puts the app name first, so a truncated print header "
            + "identifies the tool rather than the character.");
    }

    /// <summary>Widows and orphans, so a paragraph never leaves one line behind.</summary>
    [Fact]
    public void ProseDoesNotLeaveASingleLineBehind()
    {
        var normalised = Normalise(OnlyPrintBlockOf(AppCss));

        Assert.Contains("orphans:3", normalised, StringComparison.Ordinal);
        Assert.Contains("widows:3", normalised, StringComparison.Ordinal);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A .razor file with the parts a player never sees removed: Razor comments, and the
    /// <c>@code</c> block, where engineering detail belongs and is wanted.
    ///
    /// <para>The block is matched as a line that opens a brace, and the leading whitespace is
    /// tolerated. Searching for a bare <c>"\n@code"</c> meant an indented block stripped
    /// nothing — and every C# comment in it was then read as text on the page.</para>
    /// </summary>
    private static string VisibleMarkup(string razor)
    {
        var withoutComments = Rx(@"@\*.*?\*@", RegexOptions.Singleline).Replace(razor, " ");
        var block = Rx(@"^[ \t]*@code\s*\{", RegexOptions.Multiline).Match(withoutComments);

        return block.Success ? withoutComments[..block.Index] : withoutComments;
    }

    /// <summary>
    /// The prose a player actually reads: visible markup with the tags removed (which takes
    /// every attribute with them) and Razor expressions removed. What is left is text.
    ///
    /// <para>This is why a type name can be tested for at all. <c>@PowerFormatter.StatLine(p)</c>
    /// is an expression and legitimate; the same characters sitting in a paragraph are not,
    /// and only one of the two survives to here.</para>
    /// </summary>
    private static string VisibleText(string razor)
    {
        var text = VisibleMarkup(razor);

        text = Rx("<[^>]*>").Replace(text, " ");              // tags, and so all attributes
        text = Rx(@"@\([^()]*(\([^()]*\))?[^()]*\)").Replace(text, " ");   // @( … )
        text = Rx(@"@\w+(\.\w+)*(\([^()]*\))?").Replace(text, " ");        // @Foo.Bar(…), @if, @foreach

        return text;
    }

    private static string WithoutCssComments(string css) =>
        Rx(@"/\*.*?\*/", RegexOptions.Singleline).Replace(css, " ");

    /// <summary>Whitespace removed, so `position:fixed` and `position: fixed` are one string.</summary>
    private static string Normalise(string css) => Rx(@"\s+").Replace(css, "");

    /// <summary>
    /// The contents of the file's one <c>@media print</c> block, comments stripped first so a
    /// brace inside one cannot end the block early. That there is only one is its own test.
    /// </summary>
    private static string OnlyPrintBlockOf(string css)
    {
        var body = WithoutCssComments(css);
        var start = body.IndexOf("@media print", StringComparison.Ordinal);
        Assert.True(start >= 0, "No @media print block.");

        var open = body.IndexOf('{', start);
        var depth = 0;

        for (var i = open; i < body.Length; i++)
        {
            if (body[i] == '{') depth++;
            else if (body[i] == '}' && --depth == 0) return body[(open + 1)..i];
        }

        throw new InvalidOperationException("The @media print block is not closed.");
    }

    /// <summary>
    /// The declarations of the print rule whose selector list contains <paramref name="selector"/>,
    /// or null. Selector-and-declaration have to be checked together: separately, "this
    /// selector is mentioned" and "this declaration appears somewhere" are both satisfied by
    /// a stylesheet that does the opposite of what is intended.
    /// </summary>
    private static string? PrintRuleFor(string selector)
    {
        // Every rule that names the selector, not the first. A second rule setting something
        // unrelated on the same element — `print-color-adjust`, as it happened — shadowed the
        // one being asserted on, and the assertion then failed against a declaration block
        // that was never its subject.
        var matching = Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(OnlyPrintBlockOf(AppCss))
            .Where(rule => rule.Groups[1].Value
                .Split(',')
                .Select(Normalise)
                .Contains(Normalise(selector), StringComparer.Ordinal))
            .Select(rule => rule.Groups[2].Value)
            .ToList();

        return matching.Count == 0 ? null : string.Join(';', matching);
    }

    private static string Token(string block, string name) =>
        TokenOrNull(block, name) ?? throw new InvalidOperationException($"The print block does not state {name}.");

    private static string? TokenOrNull(string block, string name)
    {
        var match = Rx($@"{Regex.Escape(name)}\s*:\s*([^;]+);").Match(block);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Rough relative luminance of a #RGB or #RRGGBB value, 0 (black) to 1 (white). Rough is
    /// enough: the question is "is this ink or is this paper", not a contrast ratio. A value
    /// that is not a hex colour throws rather than guessing — a token that stopped being a
    /// colour is exactly the change this must not wave through.
    /// </summary>
    private static double Luminance(string value)
    {
        var hex = Rx(@"#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\b").Match(value);

        if (!hex.Success)
            throw new InvalidOperationException($"'{value}' is not a hex colour.");

        var digits = hex.Groups[1].Value;
        if (digits.Length == 3)
            digits = string.Concat(digits.Select(c => new string(c, 2)));

        var r = Convert.ToInt32(digits[..2], 16) / 255.0;
        var g = Convert.ToInt32(digits[2..4], 16) / 255.0;
        var b = Convert.ToInt32(digits[4..], 16) / 255.0;

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }
}
