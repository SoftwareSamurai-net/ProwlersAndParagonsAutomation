using System.Globalization;
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

    /// <summary>
    /// The page the app boots into.
    ///
    /// <para><b>It is the other file in the payload that can carry CSS, and the colour and
    /// typeface rules did not read it.</b> A <c>&lt;style&gt;</c> block dropped in here setting
    /// everything in Comic Sans applied — the CSP carries <c>style-src 'unsafe-inline'</c> for
    /// the budget bar's live width, so an inline block is not blocked at runtime either — and
    /// every presentation test stayed green. It also holds the boot screen, whose markup no
    /// component owns.</para>
    /// </summary>
    private static string IndexHtml => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "index.html"));

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
    [InlineData("index.html")]
    public void NoComponentNamesAColour(string what)
    {
        var hex = Rx(@"#[0-9A-Fa-f]{3,8}\b");
        var keyword = Rx(@":\s*(red|blue|green|white|black|grey|gray|yellow|orange|purple)\b", RegexOptions.IgnoreCase);
        var channels = Rx(@"\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch)\s*\(", RegexOptions.IgnoreCase);

        List<(string, string)> sources = what switch
        {
            "app.css" => [("app.css", Scannable(AppCss, css: true))],
            "index.html" => [("index.html", Scannable(Scannable(IndexHtml, css: true), css: false))],
            _ => RazorFiles.Select(f => (Path.GetFileName(f), Scannable(File.ReadAllText(f), css: false))).ToList()
        };

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
    /// The type tokens are restated for paper too.
    ///
    /// <para><b>The theory above cannot see them and would not notice them going.</b> It
    /// derives its list from the two <c>data-mode</c> blocks, and these three live on a bare
    /// <c>:root</c> because they are not per-mode — so a screen tracking of 0.06em, which reads
    /// as deliberate at 1.7rem, would silently open an 8pt heading bar on paper into loose
    /// letters. They are restated today; nothing held them there.</para>
    ///
    /// <para><c>--font-display</c> and <c>--font-body</c> are deliberately <b>not</b> in this
    /// list: they have no per-mode variant to survive, and paper wants the same two faces the
    /// screen does.</para>
    /// </summary>
    [Theory]
    [InlineData("--display-track")]
    [InlineData("--display-leading")]
    [InlineData("--label-track")]
    public void PrintRestatesTheTypeTokensToo(string token)
    {
        var print = OnlyPrintBlockOf(ThemeCss);
        var screen = ThemeCss[..ThemeCss.IndexOf("@media print", StringComparison.Ordinal)];

        Assert.Contains($"{token}:", Normalise(screen), StringComparison.Ordinal);
        Assert.Contains($"{token}:", Normalise(print), StringComparison.Ordinal);
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

    // ── No component names a typeface ───────────────────────────────────────────

    /// <summary>
    /// The same rule as colour, radius and duration, for the same reason: theme.css is the one
    /// file allowed to name a face, and everything else asks it for one by token. That is what
    /// makes changing the house style an edit in one place — and it is what will make dropping
    /// in two self-hosted files a change to two token values rather than to forty rules.
    ///
    /// <para><b>Both spellings are checked.</b> <c>font-family</c> is the obvious one;
    /// <c>font</c> is the shorthand, and it carries a family too. <c>font: inherit</c> is all
    /// over app.css and is not naming anything — what a shorthand may not do is carry a quoted
    /// family or a generic family keyword.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("razor")]
    [InlineData("index.html")]
    public void NoComponentNamesATypeface(string what)
    {
        List<(string, string)> sources = what switch
        {
            "app.css" => [("app.css", WithoutCssComments(AppCss))],
            "index.html" => [("index.html", WithoutCssComments(IndexHtml))],
            _ => RazorFiles.Select(f => (Path.GetFileName(f), Scannable(File.ReadAllText(f), css: false))).ToList()
        };

        var family = Rx(@"font-family\s*:\s*([^;}]+)");
        var shorthand = Rx(@"(?<![\w-])font\s*:\s*([^;}]+)");
        var token = Rx(@"^var\(--font-[a-z-]+\)$");
        var generic = Rx(@"\b(serif|sans-serif|monospace|cursive|fantasy|system-ui|ui-[a-z-]+)\b",
            RegexOptions.IgnoreCase);

        foreach (var (name, text) in sources)
        {
            foreach (Match declaration in family.Matches(text))
            {
                var value = declaration.Groups[1].Value.Trim();
                Assert.True(token.IsMatch(value),
                    $"{name} sets font-family to '{value}'. Ask theme.css for --font-display or "
                    + "--font-body instead.");
            }

            foreach (Match declaration in shorthand.Matches(text))
            {
                var value = declaration.Groups[1].Value.Trim();
                Assert.True(
                    !value.Contains('"') && !value.Contains('\'') && !generic.IsMatch(value),
                    $"{name} names a typeface in a font shorthand: '{value}'.");
            }
        }
    }

    /// <summary>
    /// Two faces, and they have to be two. A condensed display face for every heading, label
    /// and figure, and a separate face for running prose — which is the single biggest visual
    /// difference between this app and the one it was measured against, where levels were
    /// separated by size and weight in one face and read as a document rather than a page.
    ///
    /// <para><b>Both halves are asserted, and the first without the second is theatre.</b> Two
    /// tokens declared to the same stack satisfy any check that they exist and are used, and
    /// leave the app looking exactly as it did; and a display token nothing asks for is a
    /// token, not a typeface.</para>
    /// </summary>
    [Fact]
    public void TheTwoFacesAreTokensAndDoDifferentJobs()
    {
        var theme = WithoutCssComments(ThemeCss);

        var body = Rx(@"--font-body\s*:\s*([^;]+);").Match(theme);
        var display = Rx(@"--font-display\s*:\s*([^;]+);").Match(theme);

        Assert.True(body.Success, "theme.css declares no --font-body.");
        Assert.True(display.Success, "theme.css declares no --font-display.");
        Assert.NotEqual(Normalise(body.Groups[1].Value), Normalise(display.Groups[1].Value));

        var css = WithoutCssComments(AppCss);
        Assert.Contains("var(--font-display)", css, StringComparison.Ordinal);
        Assert.Contains("var(--font-body)", css, StringComparison.Ordinal);

        // The headings are the job the display face exists for, and they are set together in
        // one rule. If that rule stops asking for the face, every heading in the app silently
        // goes back to the body stack while both tokens are still declared and still used.
        var headings = Rx(@"h1,\s*h2,\s*h3,\s*h4\s*\{([^{}]*)\}").Match(css);
        Assert.True(headings.Success, "app.css no longer sets h1–h4 together.");
        Assert.Contains("font-family:var(--font-display)", Normalise(headings.Groups[1].Value),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every self-hosted face resolves to a file that is actually there, and carries the
    /// licence that lets it be redistributed.
    ///
    /// <para><b>A missing font file fails silently and looks like a design decision.</b> The
    /// stacks name system fallbacks after each self-hosted family — deliberately, so a failed
    /// load still leaves a readable page — which means renaming a file, or dropping it from
    /// the publish, degrades the whole app to the system stack with every other test in this
    /// file green. Nothing but the bytes on disk can catch that.</para>
    ///
    /// <para><b>The licence is asserted beside the font because shipping it is a condition of
    /// the SIL Open Font License, not a courtesy.</b> These files are redistributed by every
    /// deploy and by every fork of this repository.</para>
    /// </summary>
    [Fact]
    public void EverySelfHostedFaceIsPresentAndCarriesItsLicence()
    {
        var fonts = Path.Combine(WebRoot, "wwwroot", "fonts");

        Assert.NotEmpty(FontFaces());

        foreach (var (family, file) in FontFaces())
        {
            var path = Path.Combine(fonts, file);

            Assert.True(File.Exists(path),
                $"theme.css asks for {file}, which is not in wwwroot/fonts. The page falls back "
                + "to the system stack and nothing else notices.");

            // A file that exists and is empty loads as a broken font, which fails the same way.
            Assert.True(new FileInfo(path).Length > 1024, $"{file} is empty.");
        }

        // One licence per family, not one licence in the folder: two families ship here and a
        // single OFL.txt would cover whichever of them somebody assumed.
        foreach (var family in FontFaces().Select(f => f.Family).Distinct(StringComparer.Ordinal))
        {
            var licence = Path.Combine(fonts, $"{family}-OFL.txt");

            Assert.True(File.Exists(licence),
                $"{family} ships without the Open Font License text that permits redistributing it.");

            // **The text, not the file.** This is the one guard here whose green carries a legal
            // claim — these files are redistributed by every deploy and every fork — and a
            // 23-byte stub saying "Oswald is a nice font." satisfied `File.Exists`. The reserved
            // font name is asserted too, so a licence cannot be copied from the other family.
            var text = File.ReadAllText(licence);

            Assert.Contains("SIL OPEN FONT LICENSE", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Copyright", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(family, text.Replace(" ", "", StringComparison.Ordinal),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A family may only be served the file that carries it.
    ///
    /// <para><b>Nothing else correlates the two, and without this the redesign's headline item
    /// silently reverts.</b> Pointing Oswald's <c>src</c> at the Public Sans file leaves every
    /// heading, label, figure and section bar rendering in the body face — with the two tokens
    /// still declared, still different, still both asked for, and every file still present and
    /// licensed. Four font guards stay green while the app looks exactly as it did before the
    /// slice. Swapping the two <c>src</c> lines is the same hole and sets the prose in a
    /// condensed display face.</para>
    /// </summary>
    [Fact]
    public void EachFamilyIsServedItsOwnFile()
    {
        Assert.NotEmpty(FontFaces());

        foreach (var (family, file) in FontFaces())
            Assert.StartsWith(family, file, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every <c>@font-face</c> in theme.css as (family with spaces stripped, file name). The
    /// family is stripped because a file name cannot carry a space: "Public Sans" ships as
    /// <c>PublicSans-Variable.ttf</c>.
    /// </summary>
    private static List<(string Family, string File)> FontFaces() =>
        Rx(@"@font-face\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)
            .Matches(WithoutCssComments(ThemeCss))
            .Select(m => (
                Family: Rx(@"font-family:\s*""([^""]+)""").Match(m.Groups["body"].Value)
                    .Groups[1].Value.Replace(" ", "", StringComparison.Ordinal),
                File: Rx(@"url\(""\.\./fonts/([^""]+)""\)").Match(m.Groups["body"].Value)
                    .Groups[1].Value))
            .Where(f => f.Family.Length > 0 && f.File.Length > 0)
            .ToList();

    /// <summary>
    /// The self-hosted family leads each stack. A file that is downloaded, served and then
    /// listed behind the system face is paid for on every visit and never seen.
    /// </summary>
    [Theory]
    [InlineData("--font-display")]
    [InlineData("--font-body")]
    public void TheSelfHostedFaceIsTheFirstOneAskedFor(string token)
    {
        var theme = WithoutCssComments(ThemeCss);

        var declared = Rx($@"{token}\s*:\s*([^;]+);").Match(theme);
        Assert.True(declared.Success, $"theme.css declares no {token}.");

        var first = Normalise(declared.Groups[1].Value).Split(',')[0].Trim('"');

        var hosted = Rx(@"@font-face\s*\{[^}]*?font-family:\s*""([^""]+)""", RegexOptions.Singleline)
            .Matches(theme)
            .Select(m => Normalise(m.Groups[1].Value))
            .ToList();

        Assert.Contains(first, hosted, StringComparer.Ordinal);
    }

    /// <summary>
    /// The rulebook's word for a rank is set behind the rank it glosses, not level with it.
    /// The number is the fact a player rolls; the word is Ch.2's name for it, and in the same
    /// size and ink the two compete. Nothing in the markup can carry this — the element is
    /// there either way — which is why it is asserted against the rule.
    /// </summary>
    [Fact]
    public void TheRankWordIsSetBehindTheRankItGlosses()
    {
        var rule = Rx(@"(?<![\w.-])\.rank-word\s*\{([^{}]*)\}").Match(WithoutCssComments(AppCss));

        Assert.True(rule.Success,
            "app.css has no .rank-word rule, so the rulebook's word is set like the rank.");

        var declarations = Normalise(rule.Groups[1].Value);

        Assert.Contains("color:var(--muted)", declarations, StringComparison.Ordinal);
        Assert.Contains("text-transform:uppercase", declarations, StringComparison.Ordinal);
        Assert.Contains("font-family:var(--font-display)", declarations, StringComparison.Ordinal);

        // A step behind the rank beside it, which is set at 1.1rem. The band, not the presence
        // of a size: `Contains("font-size:")` is satisfied by 2rem, which would put the gloss
        // in front of the figure.
        var size = Rx(@"font-size:([0-9.]+)rem").Match(declarations);
        Assert.True(size.Success, "The .rank-word rule sets no font size in rem.");
        Assert.InRange(double.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture), 0.6, 0.9);

        // **And it is shown at all.** Everything above is satisfied by an element that is
        // present and hidden — `display: none` on the class passed every assertion here and
        // every rendering test in the bUnit project, because a hidden element still has its
        // class and still has its text. The rulebook's word for a rank simply stopped
        // appearing. Checked over every rule that targets the class, not the base one, since a
        // more specific rule further down wins the cascade.
        Assert.All(RulesTargeting(".rank-word"), rule =>
            Assert.DoesNotContain("display:none", Normalise(rule), StringComparison.Ordinal));
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
        ("option", "OptionRow.razor"),

        // The blank ruled lines. SheetView hand-wrote three of these — two in the masthead
        // and one inside a MarkupString that also hand-rolled its own HtmlEncode, which is a
        // raw-HTML sink in the file whose whole argument is that markup lives in components.
        ("ruled", "RuledLines.razor"),
        ("rule-line", "RuledLines.razor"),

        // A recorded turn and the engine's answer about the character in it. They are owned
        // for the reason the rest are, plus one of this surface's own: the attribution above
        // a turn is half of what keeps a recording from reading as a live conversation, so
        // it may not be something a second page can write without it.
        ("replay-turn", "ReplayTurn.razor"),
        ("replay-who", "ReplayTurn.razor"),
        ("replay-figures", "ReplayVerdict.razor")
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

    /// <summary>
    /// The two boxes that can be taller than a page opt back out, and they have to.
    ///
    /// <para><c>break-inside: avoid</c> is a request Chrome honours by moving the whole box to
    /// the next page first. On a box that cannot fit on any page that means the previous page
    /// is abandoned — a fifteen-Power character left two thirds of page one white. The Powers
    /// group is one such box; the <c>fill</c> boxes are the other, because they stretch to the
    /// height of the tallest column and the Powers column is unbounded.</para>
    /// </summary>
    [Theory]
    [InlineData(".sheet-section.powers")]
    [InlineData(".sheet-section.fill")]
    public void TheBoxesThatCanExceedAPageAreAllowedToBreak(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"The print block does not let {selector} break.");
        Assert.Contains("break-inside:auto", Normalise(rule!), StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing on the printed sheet is set below 7pt. Under that, small caps at a letter-space
    /// stop being something a person reads and become a texture — and everything the sheet
    /// carries, it carries because somebody at a table needs it.
    ///
    /// <para>This is measured rather than eyeballed because the one place it went wrong was a
    /// deliberate choice: Hero Point costs were set at 6.8pt to push them behind the ranks,
    /// which the case and the ink already do.</para>
    ///
    /// <para><b>Every size on paper is read, not only the ones already in points.</b> The
    /// pattern was <c>font-size:\s*([\d.]+)pt</c>, which does not match a size in any other
    /// unit — so setting the two densest blocks on the sheet, the stat tables and the Power
    /// stat lines, to <c>0.3rem</c> and <c>4px</c> left this green, because the sizes it did
    /// find were all still above the floor and <c>Assert.NotEmpty</c> was satisfied by them.
    /// A print size in <c>rem</c> is also wrong on its own terms: it is relative to a root
    /// size the print block resets, so it says nothing about what comes out of the printer.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingOnPaperIsSetBelowSevenPoint()
    {
        var sizes = Rx(@"font-size:\s*([^;}]+)")
            .Matches(OnlyPrintBlockOf(AppCss))
            .Select(m => (Text: Normalise(m.Value), Value: m.Groups[1].Value.Trim()))
            .ToList();

        Assert.NotEmpty(sizes);

        Assert.All(sizes, s =>
        {
            var points = Rx(@"^([\d.]+)pt$").Match(s.Value);

            Assert.True(points.Success,
                $"{s.Text} is not set in points, so nothing here can say how big it prints.");

            Assert.True(
                double.Parse(points.Groups[1].Value, CultureInfo.InvariantCulture) >= 7,
                $"{s.Text} is too small to read on paper.");
        });

        // **And no `font` shorthand**, which sets a size without ever writing `font-size` —
        // `font: 400 4pt/1.1 inherit` on the stat tables puts the densest block of the sheet at
        // 4pt and every check above still passes. Refused outright rather than parsed: the
        // shorthand also silently resets four other properties, so a print block that sets
        // sizes one property at a time is the right rule anyway.
        Assert.DoesNotMatch(Rx(@"(?<![\w-])font:"), Normalise(OnlyPrintBlockOf(AppCss)));
    }

    /// <summary>
    /// The heading bars are the sheet's one run of colour on paper, and a browser drops print
    /// backgrounds unless a page asks for them. Without the ask, every bar prints white and
    /// the sheet comes out looking half-styled — which is the failure the rule's own comment
    /// describes, and which nothing asserted: <c>exact</c> could be changed to <c>economy</c>
    /// with the suite green.
    ///
    /// <para>Both spellings, because the unprefixed property is not what Chrome reads.</para>
    /// </summary>
    [Fact]
    public void TheHeadingBarsAskToBePrintedRatherThanDroppedAsBackgrounds()
    {
        var rule = PrintRuleFor(".sheet-section > h3");

        Assert.True(rule is not null, "The print block does not style the heading bars at all.");

        var declarations = Normalise(rule!);

        // The tint is what there is to print. Asserted here rather than left implied: a rule
        // that asks for its background to be printed and then has none is a no-op that reads
        // like a guarantee.
        Assert.Contains("background:var(--accent-soft)", declarations, StringComparison.Ordinal);
        Assert.Contains("print-color-adjust:exact", declarations, StringComparison.Ordinal);
        Assert.Contains("-webkit-print-color-adjust:exact", declarations, StringComparison.Ordinal);

        // **And nothing later in the block may take it back.** Reading one rule by its exact
        // selector is defeated by a second, more specific one — `.sheet .sheet-section h3 {
        // background: none; print-color-adjust: economy }` prints every bar white and this
        // test never sees it, because `PrintRuleFor` matches the selector string it was given.
        // So: no value of the property other than `exact`, anywhere on paper.
        var print = OnlyPrintBlockOf(AppCss);

        Assert.All(
            Rx(@"print-color-adjust:\s*([^;}]+)").Matches(print).Select(m => m.Groups[1].Value.Trim()),
            value => Assert.Equal("exact", value));

        // And no heading rule on paper may drop the tint it is asking to have printed.
        Assert.All(
            Rx(@"([^{}]+)\{([^{}]*)\}").Matches(print)
                .Where(r => r.Groups[1].Value.Contains("h3", StringComparison.Ordinal))
                .Select(r => Normalise(r.Groups[2].Value)),
            r => Assert.DoesNotMatch(Rx(@"background(-color)?:(none|transparent|#fff|white)"), r));
    }

    /// <summary>
    /// Nothing on paper scales the sheet as a whole. The 7pt floor reads declared sizes, and a
    /// declared size is only what comes out of the printer if nothing shrinks the page under
    /// it — <c>zoom: 0.55</c> on the printed sheet leaves every declaration at or above 7pt
    /// while the 7.5pt stat lines arrive at about 4pt.
    ///
    /// <para>"Make it fit on one page" is the most likely reason anybody edits this block, and
    /// the sheet being one page is a stated goal, so this is a change somebody would make in
    /// good faith. The one-page property is held by the <c>fill</c> boxes absorbing the
    /// difference, not by shrinking the type.</para>
    /// </summary>
    [Fact]
    public void ThePrintedSheetIsNotScaledDownUnderItsOwnTypeSizes()
    {
        var print = Normalise(OnlyPrintBlockOf(AppCss));

        Assert.DoesNotContain("zoom:", print, StringComparison.Ordinal);
        Assert.DoesNotMatch(Rx(@"transform:[^;}]*scale\("), print);
    }

    /// <summary>
    /// A Hero Point cost is set apart from a rank, and the separation is entirely typographic
    /// — so it is entirely in the stylesheet, where no test that reads rendered markup can
    /// see it. Emptying this rule leaves costs in the same size, weight and ink as the numbers
    /// a player rolls, and the sheet goes back to reading as a receipt.
    ///
    /// <para><b>The values, not the properties.</b> <c>Contains("font-size:")</c> is satisfied
    /// by <c>2.4rem</c> at weight 800 — a cost set three times the size of the rank it sits
    /// beside, which is the opposite of what this rule is for and exactly the weakness the
    /// neighbouring <see cref="ATraitSourceLineIsSetApartFromThePowersBelowIt"/> was hardened
    /// against and this one was not.</para>
    /// </summary>
    /// <para><b>Every rule that targets <c>.hp</c>, not the first one found.</b> Reading only
    /// the first is defeated without touching it: a more specific rule six lines below —
    /// <c>.power-entry .head .hp</c>, which is already in the file — wins the cascade and was
    /// never read, so every Hero Point cost on the Powers stack could go to 2.4rem at weight
    /// 800 in heading ink with the suite green. <c>CLAUDE.md</c> records this trap twice, on
    /// <c>.ruled</c> and on gear alignment; it applies to a test as much as to a stylesheet.
    /// </para>
    /// </summary>
    [Fact]
    public void AHeroPointCostIsSetApartFromTheNumbersAPlayerRolls()
    {
        var rules = RulesTargeting(".hp");

        Assert.NotEmpty(rules);

        // The base rule carries the intent; the rest may not undo it.
        var declarations = Normalise(
            Rx(@"(?<![\w.-])\.hp\s*\{([^{}]*)\}").Match(WithoutCssComments(AppCss)) is { Success: true } m
                ? m.Groups[1].Value
                : throw new InvalidOperationException(
                    "app.css has no .hp rule, so costs are set like everything else."));

        Assert.Contains("letter-spacing:", declarations, StringComparison.Ordinal);
        Assert.Contains("color:var(--muted)", declarations, StringComparison.Ordinal);
        Assert.Contains("text-transform:uppercase", declarations, StringComparison.Ordinal);

        var weight = Rx(@"font-weight:(\d+)").Match(declarations);
        Assert.True(weight.Success, "The .hp rule sets no font weight.");

        var size = Rx(@"font-size:([0-9.]+)rem").Match(declarations);
        Assert.True(size.Success, "The .hp rule sets no font size in rem.");

        // Every declaration of each property in every one of those rules, not the first found.
        // Two `font-size` declarations in one block is the same shadowing trick at a smaller
        // scale, and the later one wins.
        static IEnumerable<string> Values(string rule, string pattern) =>
            Rx(pattern).Matches(rule).Select(m => m.Groups[1].Value);

        Assert.All(rules, rule =>
        {
            // A step behind the body size, not a shout. Set in small caps, so it reads smaller
            // than its figure — the lower bound is what stops that becoming a texture. A size
            // in points belongs to the print block and is held by the 7pt floor instead.
            Assert.All(Values(rule, @"font-size:([0-9.]+)rem"),
                v => Assert.InRange(double.Parse(v, CultureInfo.InvariantCulture), 0.6, 0.95));

            // Never emphasised. A cost is bookkeeping; bolding it puts it in front of the rank,
            // whatever the size and the ink are doing.
            Assert.All(Values(rule, @"font-weight:(\d+)"),
                v => Assert.InRange(int.Parse(v, CultureInfo.InvariantCulture), 100, 500));

            // And never brought back into the body ink or out of small caps, which are the
            // other two halves of the separation.
            Assert.All(Values(rule, @"(?<!-)color:([^;]+)"), v => Assert.Equal("var(--muted)", v));
            Assert.All(Values(rule, @"text-transform:([^;]+)"), v => Assert.Equal("uppercase", v));
        });
    }

    /// <summary>
    /// The <c>Abilities (…)</c> line that opens a Source group is set apart from the Powers
    /// under it. It is not a Power — no rank, no cost — and in the same face it reads as the
    /// first entry in the list, which is a misreading the markup cannot prevent: the class is
    /// on the element either way, so every rendering test passes with this rule emptied.
    ///
    /// <para>The print size is asserted too. On screen it is set in <c>rem</c>, which the
    /// print block overrides for everything else on the sheet — leaving this one behind would
    /// print it visibly larger than the Power names beside it.</para>
    /// </summary>
    [Fact]
    public void ATraitSourceLineIsSetApartFromThePowersBelowIt()
    {
        var css  = WithoutCssComments(AppCss);
        var rule = Rx(@"(?<![\w.-])\.power-entry\.trait-sources\s*\{([^{}]*)\}").Match(css);

        Assert.True(rule.Success,
            "app.css does not set the trait Source line apart, so it reads as the first Power.");

        var declarations = Normalise(rule.Groups[1].Value);
        Assert.Contains("font-style:italic", declarations, StringComparison.Ordinal);

        // The value, not just the property. `Contains("font-size:")` passed at 3rem — three
        // times the body size — which is the same weakness this test's print half already
        // had. Set apart means a step down from the entries it sits above, not a shout.
        var screen = Rx(@"font-size:\s*([0-9.]+)rem").Match(declarations);
        Assert.True(screen.Success, "The screen rule sets no font size in rem.");
        Assert.InRange(double.Parse(screen.Groups[1].Value, CultureInfo.InvariantCulture), 0.7, 1.0);

        var printed = PrintRuleFor(".power-entry.trait-sources");
        Assert.True(printed is not null,
            "The print block leaves the trait Source line at its screen size.");

        // The value, not merely the unit. `Contains("pt")` passed at 30pt, while this test's
        // own comment claimed it stopped the line printing larger than the entries around it.
        // The band it belongs to runs from the stat lines (7.5pt) to the body size (10.5pt).
        var size = Rx(@"font-size:\s*([0-9.]+)pt").Match(Normalise(printed!));
        Assert.True(size.Success, "The print rule sets no font size in points.");

        // Strictly under the 10.5pt body size the Power names take, which an inclusive upper
        // bound of 10.5 did not enforce: the line has to read as a step below them.
        var points = double.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.InRange(points, 7.5, 9.5);
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
    [InlineData(".banner-link")]
    [InlineData(".replay")]
    [InlineData(".no-print")]
    // A filter box is a control and the count beside it is a fact about a screen.
    [InlineData(".options-filter")]
    // The rule a figure came out of. The sheet does not ask for one, but printing any other
    // page should not put three lines of small print under each of four boxes either.
    [InlineData(".stat-block .formula")]
    [InlineData("h1")]
    public void ThePrintedSheetLeavesOutTheToolAroundIt(string selector)
    {
        var rule = PrintRuleFor(selector);

        Assert.True(rule is not null, $"Nothing in the print block hides {selector}.");
        Assert.Contains("display:none", Normalise(rule!), StringComparison.Ordinal);

        // **And the thing being hidden still exists to be hidden.** A class renamed in the
        // components leaves this rule hiding nothing at all — it passes here, and passes every
        // rendering test, while the element it was written for prints. So the class has to be
        // written by some component or styled for the screen somewhere; a print rule for a
        // selector nothing produces is a rule that has quietly stopped working.
        var name = selector.Split(' ').Last().TrimStart('.');

        if (!selector.StartsWith('.') && !selector.Contains(" .", StringComparison.Ordinal)) return;

        var written = RazorFiles.Any(f => File.ReadAllText(f).Contains(name, StringComparison.Ordinal))
                      || WithoutCssComments(AppCss)
                          .Contains($".{name}", StringComparison.Ordinal);

        Assert.True(written,
            $"The print block hides '{selector}', but nothing writes \"{name}\" any more. The "
            + "rule is hiding an element that no longer exists under that name.");
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

    /// <summary>
    /// The browser builds its replay library through the loader that promises a failed fetch
    /// leaves the app running, rather than open-coding the fetch again.
    ///
    /// <para>This is the source half of a guarantee whose behaviour is tested in
    /// <c>ProwlersAndParagons.Web.Tests.ReplayLoadingTests</c>. Both are needed and neither is
    /// enough: a guarded loader nobody calls guarantees nothing, and it lived here as a
    /// <c>try</c>/<c>catch</c> in top-level statements that no test could reach — deleting the
    /// <c>try</c> was green, and one 404 then took the character generator to a blank page.
    /// </para>
    /// </summary>
    [Fact]
    public void TheBrowserBuildsItsReplayLibraryThroughTheGuardedLoader()
    {
        var program = File.ReadAllText(Path.Combine(WebRoot, "Program.cs"));

        Assert.Contains("ReplayLibrary.LoadAsync(http)", program, StringComparison.Ordinal);

        // And nowhere else builds one. Constructing it here is how the guard gets bypassed
        // without anything looking wrong.
        Assert.DoesNotContain("new ReplayLibrary(", program, StringComparison.Ordinal);

        // **Nor may it do the fetching itself.** Calling the guarded loader is not the same as
        // being guarded: an adversarial pass fetched every transcript here, in a bare loop,
        // and handed the loader a delegate that only read the resulting dictionary. The
        // throwing call was back outside the try, one 404 took the app to a blank page, and
        // both this test and every behavioural test stayed green. So the path lives on
        // ReplayLibrary and this file may not name it.
        Assert.DoesNotContain("transcripts", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// The address the app asks for and the folder the build stages the recordings into are
    /// the same, and they are written down in two files that nothing else connects.
    ///
    /// <para>The behavioural half — that the loader really requests that path — is
    /// <c>ReplayLoadingTests.TheLibraryAsksForEachRecordingWhereTheBuildPutsIt</c>. This is the
    /// other end: that the path it agrees on is where the csproj puts the files. Either alone
    /// is satisfied by a consistent, wrong answer.</para>
    /// </summary>
    [Fact]
    public void TheRecordingsAreAskedForFromTheFolderTheBuildStagesThemInto()
    {
        var served = Rx(@"ServedFrom\s*=\s*""([^""]+)""")
            .Match(File.ReadAllText(Path.Combine(WebRoot, "Services", "ReplayLibrary.cs")));

        Assert.True(served.Success, "ReplayLibrary does not say where the recordings are served from.");

        // The folders the build actually stages into, read whole. `Contains` is wrong here and
        // was: `wwwroot\data\transcript` is a substring of `wwwroot\data\transcripts`, so
        // dropping the "s" — the exact one-character mistake this test exists to catch — passed.
        var staged = Rx(@"DestinationFolder=""[^""]*\\wwwroot\\([^""]+)""")
            .Matches(File.ReadAllText(Path.Combine(WebRoot, "ProwlersAndParagons.Web.csproj")))
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Contains(served.Groups[1].Value.Replace('/', '\\'), staged, StringComparer.Ordinal);
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
    /// <summary>
    /// The normalised declarations of every rule in the whole stylesheet whose selector ends
    /// in <paramref name="target"/> — so a more specific rule further down, which is what
    /// actually wins the cascade, is read too. Asserting on "the first rule with this class in
    /// it" is defeated by adding a second one and never touching the first.
    /// </summary>
    private static List<string> RulesTargeting(string target) =>
        [.. Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(WithoutCssComments(AppCss))
            .Where(rule => rule.Groups[1].Value
                .Split(',')
                .Select(Normalise)
                .Any(s => s.EndsWith(target, StringComparison.Ordinal)))
            .Select(rule => Normalise(rule.Groups[2].Value))];

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
