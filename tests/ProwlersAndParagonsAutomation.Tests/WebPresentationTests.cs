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

    /// <summary>
    /// The app's own scripts — the third route into the payload, and the one that bypasses CSS
    /// entirely.
    ///
    /// <para><b>A CSSOM write is not subject to the Content-Security-Policy and no stylesheet
    /// scan can see it.</b> Three lines added to the existing <c>ppSetMode</c> —
    /// <c>documentElement.style.setProperty("--font-body", …)</c> and the same for
    /// <c>--font-display</c> and <c>--heading</c> — override both typefaces and a text colour
    /// at runtime with every presentation test green. That file already touches
    /// <c>documentElement</c> to set the palette mode, so it is the plausible place for it to
    /// happen rather than a contrived one.</para>
    /// </summary>
    private static IEnumerable<(string Name, string Text)> Scripts =>
        SourceFiles(Path.Combine(WebRoot, "wwwroot"), "*.js")
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

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

    /// <summary>The three elevation steps, named once so a palette cannot be checked for two.</summary>
    private static readonly string[] ElevationSteps = ["--shadow-1", "--shadow-2", "--shadow-3"];

    /// <summary>
    /// Every way to make an element invisible while leaving its class and its text in place.
    ///
    /// <para><b>Shared, because the second copy of it was weaker than the first.</b> Banning
    /// <c>display: none</c> alone leaves <c>visibility: hidden</c>, <c>color: transparent</c> and
    /// <c>font-size: 0</c> — several ways to one result, all green. And they are matched as whole
    /// declarations: <c>font-size:0</c> is a prefix of a rule's own <c>font-size:0.72rem</c> and
    /// <c>opacity:0</c> of <c>opacity:0.8</c>, so a naive spelling fires on the thing it is
    /// protecting, and the obvious fix for that is to weaken the ban.</para>
    ///
    /// <para>A guard added later for the empty state wrote its own four-entry version without the
    /// prefix guards. Two lists is one list going stale, so there is one.</para>
    /// </summary>
    /// <remarks>
    /// <c>color</c> is anchored with <c>(?&lt;![\w-])</c> because it is a suffix of
    /// <c>border-left-color</c>, <c>border-bottom-color</c> and the rest — so an entirely legitimate
    /// transparent border on a guarded class was reported as hiding the element. A ban that fires
    /// on something innocent is worse than no ban, because the natural fix is to weaken it.
    /// </remarks>
    private static readonly string[] EverySpellingOfHidden =
    [
        "display:none", "visibility:hidden", "visibility:collapse",
        @"(?<![\w-])color:transparent", @"font-size:0(?![.\d])", @"opacity:0(?![.\d])",
        "content-visibility:hidden"
    ];

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
    ///
    /// <para><b>The channel-function list is a denylist of named CSS Color Module functions,
    /// and it missed <c>light-dark()</c> until a mutation planted it in a real rule in
    /// app.css and every case here stayed green.</b> <c>light-dark(white, black)</c> is
    /// standards-track CSS Color 5, the regex knew none of the six names it had, and its
    /// arguments follow <c>(</c> and <c>,</c> rather than the <c>:</c> the keyword regex
    /// required — so all three detectors missed it at once. The list is now <c>rgba?</c>,
    /// <c>hsla?</c>, <c>hwb</c>, <c>lab</c>, <c>lch</c>, <c>oklab</c>, <c>oklch</c>,
    /// <c>color</c>, <c>light-dark</c>, <c>color-contrast</c> and <c>device-cmyk</c> — every
    /// colour-producing function in the CSS Color 4/5 drafts, <c>color-mix()</c> excepted
    /// (masked below, since this codebase's one use of it takes only tokens). <b>It is still
    /// a denylist and will rot again</b> if the spec grows another one: an allowlist of the
    /// functions this codebase actually uses (<c>var</c>, <c>calc</c>, <c>clamp</c>,
    /// <c>min</c>, <c>max</c>, <c>minmax</c>, <c>repeat</c>, <c>url</c>, <c>translateX/Y</c>,
    /// <c>cubic-bezier</c>, <c>linear-gradient</c>, <c>inset</c>, <c>brightness</c>, <c>not</c>,
    /// <c>where</c>, <c>has</c>, <c>nth-child</c>, <c>format</c>, the <c>view-transition-*</c>
    /// pseudo-functions) flagging anything else was tried and rejected here: the razor scan
    /// runs over files that mix markup with C#, and a Razor <c>@@code</c> block is full of
    /// unrelated calls — <c>ToList()</c>, <c>Where()</c>, <c>Select()</c> — that an
    /// allow-everything-else rule would have to special-case one by one, which is the same
    /// denylist problem moved one level up. Extending the known-colour list is the honest
    /// shape for this scan; re-run the function census in the comment above (a grep for
    /// <c>[a-zA-Z_-]+\(</c> over app.css and the razor tree) if this rots again.</para>
    ///
    /// <para><b>The keyword regex's anchor on <c>:\s*</c> was the deeper hole, and it is gone
    /// now rather than widened.</b> A colour keyword is equally a colour after <c>(</c>, after
    /// <c>,</c>, or after a bare space in a shorthand like <c>border: 1px solid black</c> —
    /// none of which follow a colon. The replacement matches the keyword anywhere, bounded on
    /// both sides by <c>(?&lt;![\w-])</c> / <c>(?![\w-])</c> rather than plain <c>\b</c>,
    /// because a plain word boundary treats a hyphen as a boundary too and <c>white-space</c>
    /// — a real property name, not a colour — is "white" immediately followed by one. Checked
    /// against the whole <c>web/</c> tree with the position requirement dropped entirely: zero
    /// matches outside comments today, so this is not scoped further than that.</para>
    ///
    /// <para><c>currentColor</c> is deliberately <b>not</b> flagged, on the same reasoning as
    /// <c>transparent</c>: neither names a hue. Both are a reference to something else — the
    /// absence of paint, or whatever ink already applies — not a colour chosen here.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("razor")]
    [InlineData("index.html")]
    public void NoComponentNamesAColour(string what)
    {
        var hex = Rx(@"#[0-9A-Fa-f]{3,8}\b");
        var keyword = Rx(@"(?<![\w-])(red|blue|green|white|black|grey|gray|yellow|orange|purple)(?![\w-])",
            RegexOptions.IgnoreCase);
        var channels = Rx(@"\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|light-dark|color-contrast|device-cmyk)\s*\(",
            RegexOptions.IgnoreCase);

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
    ///   <item><b>For a razor file, XML doc comments too</b> — <c>///</c> lines inside
    ///     <c>@@code</c>. They are C# comments, not <c>@@* *@@</c> Razor ones, so the strip
    ///     above never touched them, and once the colour-keyword scan stopped requiring a
    ///     leading colon (see <see cref="NoComponentNamesAColour"/>) one of them started
    ///     failing the test it was explaining: a <c>&lt;summary&gt;</c> in
    ///     <c>ChooseTier.razor</c> reads "…about five lines of dead white above their cost
    ///     rule", which is prose about a screenshot, not a declaration. Same reasoning as the
    ///     Razor-comment exclusion — a doc comment does not compile into anything the browser
    ///     paints, so it cannot be a component naming a colour. Scoped to <c>///</c> lines
    ///     specifically, not general <c>//</c> or <c>/* */</c> C# comments: neither appears
    ///     carrying this kind of prose anywhere in <c>web/</c> today, so stripping them was
    ///     not needed to make the real tree pass and is left undone rather than guessed at.</item>
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

        if (!css)
            stripped = Rx(@"^\s*///.*$", RegexOptions.Multiline).Replace(stripped, " ");

        stripped = Rx("&#x?[0-9A-Fa-f]+;").Replace(stripped, " ");
        return Rx(@"\bcolor-mix\s*\(", RegexOptions.IgnoreCase).Replace(stripped, "MIX(");
    }

    /// <summary>
    /// <b>The chosen light/dark theme is stamped before the first paint, and nothing else in
    /// this repository can tell you whether it still is.</b>
    ///
    /// <para>Every other script in <c>index.html</c> is at the foot of <c>&lt;body&gt;</c>, which
    /// is the right place for all of them and the wrong place for this one. The WebAssembly
    /// payload is ~27 MiB and there is a boot screen on the page while it downloads: a theme
    /// applied from C# lands seconds late, and a theme applied from the foot of the body lands
    /// after the boot screen has already been painted. Both look like a flash of the wrong
    /// colours to a reader who asked for dark, and <b>both leave every test in both suites
    /// green</b> — a render test cannot see a paint, and the attribute ends up correct either
    /// way.</para>
    ///
    /// <para>Also asserted: <c>data-mode</c> is on the markup and <c>data-theme</c> is not.
    /// The first because every palette block names it, so a boot screen without it matches no
    /// palette at all; the second because <em>absent</em> is what the system state is — the dark
    /// blocks are written <c>:not([data-theme="light"])</c>, and a value stamped here would
    /// override a reader's stored choice with a default on every visit.</para>
    /// </summary>
    [Fact]
    public void TheThemeIsStampedBeforeTheFirstPaint()
    {
        var html = IndexHtml;

        // **Matched on the tag and located by its own offset, not by searching a slice of the
        // file for the file name.** The first version of this took `html[..indexOf("</head>")]`
        // and asked whether it contained "js/theme.js" — and passed with the script moved to the
        // foot of the body, because a comment near the top of the file *mentions* the name. Two
        // comments here do. A comment loads nothing.
        var tag = Rx(@"<script[^>]*\bsrc=""js/theme\.js""[^>]*>").Match(html);

        Assert.True(tag.Success, "index.html does not load js/theme.js at all.");
        Assert.Equal(1, Rx(@"<script[^>]*\bsrc=""js/theme\.js""").Count(html));

        Assert.True(tag.Index < html.IndexOf("</head>", StringComparison.Ordinal),
            "js/theme.js is loaded after </head>, so the boot screen is painted before the "
            + "chosen theme is applied — a flash of the wrong palette for as long as the "
            + "WebAssembly download takes, with every test in both suites green.");

        // Render-blocking. `defer` and `async` both hand the paint back before it has run,
        // which is the same flash by another route.
        Assert.DoesNotContain("defer", tag.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("async", tag.Value, StringComparison.OrdinalIgnoreCase);

        var root = Rx(@"<html\b[^>]*>").Match(html).Value;
        Assert.Contains("data-mode=", root, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme=", root, StringComparison.Ordinal);
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
    ///
    /// <para><b>And it resolves the whole cascade rather than reading the print block, which
    /// is a change the four palettes forced.</b> Reading the print block on its own answers
    /// "does print declare white paper", and that was the same question until light and dark
    /// became independent. A dark palette guarded by <c>:not([data-theme="light"])</c> is
    /// specificity (0,3,0); the print block is (0,2,0) and <c>@media</c> contributes nothing to
    /// specificity — so on paper the dark block wins, and every assertion below would still
    /// hold while a reader in dark mode printed the full-bleed near-black page this test exists
    /// to prevent. What stops it is <c>@media screen</c> on the dark half, and what would
    /// notice its removal is asking, for each of the six states a reader can be in, what the
    /// paper actually resolves to.</para>
    /// </summary>
    [Theory]
    [InlineData("hero", null, false)]
    [InlineData("hero", null, true)]
    [InlineData("hero", "dark", false)]
    [InlineData("villain", null, false)]
    [InlineData("villain", null, true)]
    [InlineData("villain", "dark", true)]
    public void PrintKeepsThePaperWhiteAndTheInkReadable(string mode, string? chosen, bool systemIsDark)
    {
        var state = new ThemeState(mode, chosen, systemIsDark);
        var palette = Palette(state, printing: true);

        // Paper, and anything that sits behind body text.
        foreach (var token in new[] { "--surface", "--panel", "--panel-sunk", "--primary", "--accent-soft" })
            Assert.True(Luminance(palette[token]) > 0.85,
                $"print {token} darkens the paper for {state} ({palette[token]}).");

        // Ink. 0.45 is about a 4.5:1 contrast floor against white, which is what the small
        // print on this sheet needs.
        foreach (var token in new[] { "--ink", "--heading", "--rule", "--accent", "--on-primary", "--muted", "--danger" })
            Assert.True(Luminance(palette[token]) < 0.45,
                $"print {token} is too pale to read on white for {state} ({palette[token]}).");
    }

    /// <summary>
    /// Every token any screen palette declares has to be restated by the print block for each
    /// mode, whether from the shared rule or the mode's own. A token left out keeps its screen
    /// value through the cascade, which is exactly how the near-black page happened.
    ///
    /// <para><b>The list of tokens is collected by running the stylesheet's rules rather than
    /// by pattern-matching a block, and that was not a tidy-up.</b> The regex this used to do
    /// it with anchored on <c>data-mode="x"] {</c>, which cannot see
    /// <c>:root[data-mode="x"]:not([data-theme="light"]) {</c> — so the moment the dark
    /// palettes arrived, every token they declare would have been left out of the list and
    /// print would not have been asked to restate any of them. A guard that silently stops
    /// covering half the file is worse than one that fails.</para>
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void PrintRestatesEveryTokenTheScreenPalettesDeclare(string mode)
    {
        // The palette blocks only. The shape and motion tokens on the plain `:root` blocks are
        // not colours, and print has no reason to restate a transition duration.
        var declared = CssRules(ThemeCss)
            .Where(r => !r.Media.Contains("print", StringComparison.Ordinal))
            .Where(r => r.Selector.Contains("data-mode", StringComparison.Ordinal))
            .SelectMany(r => Rx(@"(--[a-z0-9-]+)\s*:").Matches(r.Body).Select(d => d.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);

        // Both dark palettes are among them, or this is measuring the light half only.
        Assert.Equal(4, CssRules(ThemeCss)
            .Count(r => r.Selector.Contains("data-mode", StringComparison.Ordinal)
                        && !r.Media.Contains("print", StringComparison.Ordinal)
                        && (r.Media.Length > 0 || r.Selector.Contains("data-theme", StringComparison.Ordinal))));

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

            foreach (Match declaration in Rx(@"(--[a-z0-9-]+)\s*:\s*([^;]+);").Matches(rule.Groups[2].Value))
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

    /// <summary>
    /// <b>Reduced motion turns the entrance animation off, rather than making it fast.</b>
    ///
    /// <para><c>theme.css</c> collapsing <c>--enter</c> to <c>0.01ms</c> — which
    /// <see cref="MotionIsTokenisedAndCanBeTurnedOffWholesale"/> above is the guard for — is not
    /// enough for the four rules that carry <c>animation: rise … both</c>. A <c>both</c> fill is
    /// <em>backwards</em> as well as forwards, so the element sits at the keyframe's starting
    /// <c>opacity: 0</c> from layout until the animation begins, and shortening the run does
    /// nothing to that window because the window is before the start.</para>
    ///
    /// <para><b>Found by a screenshot, and only because the pixel comparator had been
    /// tightened.</b> <c>scripts/visual-regression.sh</c> captures with
    /// <c>--force-prefers-reduced-motion</c> so the frame is settled by construction, and
    /// <c>shell-hero-light</c> still came back with both its panels at opacity 0 on one CI run of
    /// three — same tree hash, same runner image, unreproducible across four local captures. The
    /// <c>.card</c> elements between the two panels matched exactly, which is what named the
    /// cause: only <c>rise</c> was involved.</para>
    ///
    /// <para>The print block has carried this same fix for the same reason for far longer
    /// ("an animation with <c>both</c> fill can leave an element at its starting opacity if
    /// print runs before it completes"). This asserts it for the other two readers who need
    /// it: somebody who asked for no motion, and a camera.</para>
    ///
    /// <para><b>Selector and declaration are checked together, and the pairing is the point.</b>
    /// "This selector appears under a reduced-motion query" and "<c>animation:none</c> appears
    /// somewhere" are each satisfied by a stylesheet where they are nowhere near each other.</para>
    /// </summary>
    [Fact]
    public void ReducedMotionStopsTheEntranceRatherThanShorteningIt()
    {
        var block = Rx(@"@media\s*\(prefers-reduced-motion:\s*reduce\)\s*\{((?:[^{}]|\{[^{}]*\})*)\}")
            .Match(WithoutCssComments(AppCss));

        Assert.True(block.Success,
            "app.css has no `@media (prefers-reduced-motion: reduce)` block, so the four rules "
            + "carrying `animation: rise … both` keep a backwards fill for a reader who asked "
            + "for no motion — and for every screenshot the visual check takes.");

        // Every selector that carries the entrance animation, read out of the stylesheet rather
        // than listed here — a hard-coded list goes stale the first time a fifth one is added,
        // and the failure would be a rule silently uncovered.
        var animated = Rx(@"([^{}]+)\{([^{}]*animation:\s*rise[^{}]*)\}")
            .Matches(WithoutCssComments(AppCss))
            .SelectMany(m => m.Groups[1].Value.Split(','))
            .Select(Normalise)
            .Where(s => s.Length > 0 && !s.StartsWith('@'))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The positive control on the scan itself: a regex that has stopped matching finds no
        // selectors, and "every selector in an empty list is covered" is true of anything.
        Assert.True(animated.Count >= 4,
            $"only {animated.Count} selectors carry `animation: rise`; the scan has stopped "
            + "matching and this test would pass whatever the stylesheet said.");

        var turnedOff = Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(block.Groups[1].Value)
            .Where(rule => Normalise(rule.Groups[2].Value)
                .Contains("animation:none", StringComparison.Ordinal))
            .SelectMany(rule => rule.Groups[1].Value.Split(','))
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = animated.Where(s => !turnedOff.Contains(s)).ToList();

        Assert.True(uncovered.Count == 0,
            "these carry `animation: rise … both` and are not switched off under reduced "
            + "motion, so each can still be caught at opacity 0: " + string.Join(", ", uncovered));
    }

    // ── No rule names a raw length ──────────────────────────────────────────────

    /// <summary>
    /// The screen half of the stylesheet asks the spacing and type scales for every padding,
    /// margin, gap and font size. It may not name one itself.
    ///
    /// <para>This is the same rule as colour, typeface, radius and duration, for the same
    /// reason, and it was the last of the five missing. Before the scales existed the screen
    /// half spent <b>twenty-seven</b> distinct lengths on padding, margin and gap and
    /// <b>twenty</b> on font size, ten of the latter crowded between 0.68rem and 0.9rem where
    /// no reader can tell one from the next. That is not a design, it is a history of
    /// individual decisions — and nothing could have told you so, because every one of them
    /// was locally reasonable.</para>
    ///
    /// <para><b>Both spellings are refused, which is the point.</b> Checking only <c>rem</c>
    /// leaves <c>padding: 13px</c> as an open door, and px is what somebody reaching for a
    /// value rather than a rung would most naturally write.</para>
    ///
    /// <para><b>The print block is deliberately out of scope.</b> It is mm and pt — a
    /// different medium with its own scale, where 2.5mm is a measurement of paper and not a
    /// rung on a screen rhythm. Its sizes have their own tests further down this file
    /// (<c>NothingOnPaperIsSetBelowSevenPoint</c> and the break rules).</para>
    ///
    /// <para><c>index.html</c> is scanned too. It is the other file in the payload that can
    /// carry CSS, and both the colour and typeface rules once missed it. <b>Its expected reach
    /// is zero and that is stated rather than hidden</b>: it carries no CSS at all today, so
    /// this half of the theory is a prohibition on a file that could gain some — the CSP allows
    /// an inline <c>&lt;style&gt;</c> block there — and not a check on any rule that exists.
    /// Only the app.css half proves the scan can see anything.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css", 100)]
    [InlineData("index.html", 0)]
    public void NoScreenRuleNamesARawSpacingOrTypeLength(string what, int leastExpected)
    {
        var css = what == "app.css" ? ScreenHalfOfAppCss : Scannable(IndexHtml, css: true);
        var scanned = 0;

        foreach (var (selector, declarations) in RulesOf(css))
        {
            foreach (Match declaration in Rx(@"(?<prop>(?:padding|margin|gap|font-size)[a-z-]*)\s*:\s*(?<value>[^;}]+)")
                         .Matches(declarations))
            {
                var property = declaration.Groups["prop"].Value;
                var value = declaration.Groups["value"].Value.Trim();
                scanned++;

                // Normalised on both sides. Comparing a normalised declaration against a
                // hand-written exemption matched nothing, so every exemption was dead and the
                // rule fired on all three of them — which is the failure mode this whole test
                // is about, arriving first inside the test itself.
                if (ExemptLengths.Any(e => e.Selector == selector
                                           && Normalise(e.Declaration) == Normalise($"{property}:{value}")))
                    continue;   // and the reason is checked by EveryExemptedLengthStillExists

                // Every var(…) reference removed, then anything still carrying a digit and a
                // unit is a literal. `0`, `auto` and a bare ratio survive this and should.
                var residue = Rx(@"var\(\s*--[a-z0-9-]+\s*\)").Replace(value, " ");
                var literal = Rx(ANumberWithAUnit).Match(residue);

                Assert.False(literal.Success,
                    $"{what}: `{selector} {{ {property}: {value} }}` names the length "
                    + $"'{literal.Value}' outright. Use a --space-* or --text-* token from "
                    + "theme.css, or add it to ExemptLengths with a reason.");
            }
        }

        // A scan that matched nothing passes every assertion in it. That has happened in this
        // file before — thirteen of one guard's twenty-five selectors matched no rendered page
        // at all — so the instrument reports its own reach.
        Assert.True(scanned >= leastExpected,
            $"{what}: only {scanned} spacing or type declarations were found, expected at least "
            + $"{leastExpected}. The scan is not reading the stylesheet it is supposed to read.");
    }

    /// <summary>
    /// A number carrying any unit at all. <b>Not a list of units.</b>
    ///
    /// <para>This has now been the wrong shape twice, and the second time was worse because it
    /// was written knowing better. The first version listed <c>px|rem|em|ch|vh|vw|%</c> and
    /// <c>margin-top: 9pt</c> walked through. The second listed thirty-odd units, said in its own
    /// doc comment that "an allow-list of units is the wrong shape for a ban" — and then shipped a
    /// longer allow-list, which <c>margin-top: 9dvmin; padding: 3svb 2lvi; font-size: 4PX</c>
    /// walked through four times over: <c>dvi dvb dvmin dvmax svi svb svmin svmax lvi lvb lvmin
    /// lvmax</c> were all missing, and the match was case-sensitive so every capitalised spelling
    /// of every unit escaped as well.</para>
    ///
    /// <para><b>Inverted, so there is nothing to leave out.</b> In these four properties a digit
    /// followed by letters or a percent sign is a length, whatever the letters are — CSS has no
    /// other meaning for that shape here, and a unit invented by a future specification is caught
    /// on the day it ships rather than on the day somebody remembers it. Bare <c>0</c> and
    /// keywords like <c>auto</c> and <c>inherit</c> carry no digit and are unaffected.</para>
    /// </summary>
    /// <remarks>
    /// No whitespace between the number and the unit, because CSS allows none — and permitting it
    /// made <c>margin: 0 auto</c> read as the length "0 auto", which is a false positive on a
    /// perfectly ordinary centring rule.
    /// </remarks>
    private const string ANumberWithAUnit = @"\d*\.?\d+(?:[A-Za-z]+|%)";

    /// <summary>
    /// theme.css is the only file that may <b>declare</b> a custom property.
    ///
    /// <para><b>This is the door two separate mutations walked through, and neither needed a
    /// token name the scales use.</b> A custom property declared in app.css is not one of the four
    /// properties the raw-length scan reads, and every <c>var()</c> reference is stripped before
    /// the scan looks for a literal — so <c>--table-inset: 1.2rem</c> beside
    /// <c>width: calc(100% - var(--table-inset))</c> restored the table misalignment byte for
    /// byte, at 3.20px on all fifteen tables, with the suite green; and
    /// <c>--pad-lg: 4rem</c> with <c>padding: var(--pad-lg)</c> re-padded an element with a raw
    /// length under a name no rule about the scales could ever match.</para>
    ///
    /// <para>Narrowing the earlier check to <c>--space-*</c> and <c>--text-*</c> was the mistake:
    /// it defended the *names* of the scales rather than the property that makes a scale
    /// meaningful, which is that there is one place lengths are decided. <b>The general rule is
    /// the enforceable one</b>, it costs nothing — app.css declares no custom property today —
    /// and it is the same rule already holding for colour and for typefaces.</para>
    /// </summary>
    [Theory]
    [InlineData("app.css")]
    [InlineData("index.html")]
    public void OnlyTheThemeDeclaresACustomProperty(string what)
    {
        var css = WithoutCssComments(what == "app.css" ? AppCss : IndexHtml);

        // A declaration, not a reference: `--x:` at the start of a declaration rather than
        // inside `var(--x)`. The lookbehind is what tells the two apart.
        var declaration = Rx(@"(?<!var\(\s*)(--[A-Za-z0-9-]+)\s*:");
        var found = declaration.Match(css);

        Assert.False(found.Success,
            $"{what} declares the custom property {found.Groups[1].Value}. theme.css is the only "
            + "file that may — a token declared here is a length, colour or duration decided "
            + "outside the one file that is supposed to decide them, and no scan of padding, "
            + "margin, gap or font-size can see it.");

        // The instrument reports its reach: theme.css must declare plenty, or the regex is wrong
        // and this test is passing because it matches nothing anywhere.
        Assert.True(declaration.Count(WithoutCssComments(ThemeCss)) > 20,
            "The declaration pattern finds almost nothing in theme.css, so it is not capable of "
            + "finding one in " + what);
    }

    /// <summary>
    /// The three literals the rule above allows, each with the selector it belongs to.
    ///
    /// <para>All three are <b>optical</b> rather than rhythmic, which is the distinction that
    /// makes them exemptions rather than holes: a spacing token says how far apart two things
    /// sit, and these three adjust one thing against itself. The pair of 1px paddings are the
    /// gap between a word and the border-bottom standing in for its underline — a hairline, and
    /// on the scale it would be 2px, which is a visibly detached underline. The negative em
    /// pulls back the letter-spacing added after the final character of a Hero Point cost so
    /// the column of them lines up on the right edge; it is in em because it is a fraction of
    /// the tracking, and a spacing token in rem cannot express that.</para>
    ///
    /// <para>Each is asserted to still exist, by the test below. A stale exemption is a hole
    /// somebody can walk through later, so the list is not allowed to outlive its subjects.</para>
    /// </summary>
    /// <remarks>
    /// <c>AlsoRequires</c> is the exemption's own <em>reason</em>, asserted. The two 1px paddings
    /// are justified entirely by the <c>border-bottom</c> they sit against — that is what makes
    /// them an optical hairline rather than spacing — and a guard that checked only that the
    /// declaration still existed passed after the border was replaced with
    /// <c>text-decoration: underline</c>, leaving 1px of dead padding and a stated reason that
    /// was no longer true. The doc below calls a stale exemption "worse than a missing one"; that
    /// is the stale case that matters, and checking the string alone could not see it.
    /// </remarks>
    /// <remarks>
    /// <c>AlsoRequires</c> is a property <b>name</b> with no colon — it is looked up through
    /// <see cref="EffectiveValue"/>, which supplies the colon itself. Written as
    /// <c>"border-bottom:"</c> it produced a pattern demanding two colons, matched nothing, and
    /// reported every exemption's reason as missing.
    /// </remarks>
    private static readonly (string Selector, string Declaration, string AlsoRequires, string RequiredOn)[] ExemptLengths =
    [
        (".budget-toggle", "padding:0 0 1px", "border-bottom", ".budget-toggle"),
        (".banner-link", "padding-bottom:1px", "border-bottom", ".banner-link"),
        // The standard clip-to-nothing pattern for text that is read out and never drawn. The
        // -1px is part of the recipe rather than spacing anybody chose: the element is a 1px box
        // pulled back over itself so it occupies no layout at all, and rounding it to a spacing
        // rung would give it size. `clip-path` is the precondition — with the clip gone this is
        // just an element positioned 1px off, which scrolls into view and is a different bug.
        (".sr-only", "margin:-1px", "clip-path", ".sr-only"),
        // The negative em pulls back the letter-spacing added after the final character, so the
        // thing it depends on is the letter-spacing, not a border — and it comes from the base
        // `.hp` rule, which is why the selector it is required on is not the one it is exempt on.
        (".power-entry .head .hp", "margin-right:-0.08em", "letter-spacing", ".hp"),
    ];

    /// <summary>
    /// Every exemption above still names a rule that exists and still carries that declaration.
    ///
    /// <para><b>A guard that enumerates subjects has to refuse a subject it never found.</b>
    /// That lesson is recorded twice in this project already: an uppercased-text guard shipped
    /// with 13 of its 25 selectors matching nothing, and a font check filtered out the very
    /// face it existed to correlate. An exemption whose selector has been renamed away is
    /// worse than a missing one — it silently permits that declaration on any selector, because
    /// the tuple can never match again and nothing says so.</para>
    /// </summary>
    [Fact]
    public void EveryExemptedLengthStillExists()
    {
        var rules = RulesOf(ScreenHalfOfAppCss);

        foreach (var (selector, declaration, alsoRequires, requiredOn) in ExemptLengths)
        {
            var rule = rules.Where(r => r.Selector == selector).ToList();

            Assert.True(rule.Count > 0,
                $"ExemptLengths names `{selector}`, which no screen rule in app.css declares. "
                + "Remove the exemption or fix the selector — a tuple that can never match "
                + "permits its declaration everywhere.");

            Assert.True(rule.Any(r => Normalise(r.Declarations).Contains(Normalise(declaration), StringComparison.Ordinal)),
                $"`{selector}` no longer carries `{declaration}`. The exemption is stale.");

            // And the thing the exemption's reason rests on is still **in effect**. Without this
            // the declaration survives as dead decoration while the justification for exempting
            // it has gone — the stale case that actually costs something.
            //
            // Read as a **value**, on the **named** selector. Two routes reached the same end
            // state past weaker versions of this: `border-bottom: none` instead of deleting the
            // line, which a check for the property name accepted; and — once the value was read —
            // `.sheet .budget-toggle { border-bottom: … }`, a selector matching nothing in this
            // app, supplying the reason while the real rule lost it. A source-reading test cannot
            // know which selectors match real elements, so the exemption names the one that has to
            // carry its reason. For `.hp` that is the base rule rather than the exempt selector,
            // because the letter-spacing is inherited.
            var effective = EffectiveValue(ScreenHalfOfAppCss, requiredOn, alsoRequires, exact: true);

            Assert.True(effective is not null,
                $"`{requiredOn}` does not declare `{alsoRequires}`, which is the whole reason "
                + $"`{declaration}` on `{selector}` is exempt. The literal is now unjustified "
                + "rather than exempt.");

            Assert.True(effective is not ("none" or "normal" or "0" or "unset" or "initial"),
                $"`{alsoRequires}` resolves to `{effective}` on `{requiredOn}`, which is the same "
                + $"as not having it — so `{declaration}` is dead decoration and its stated reason "
                + "is false. Remove both, or remove the exemption.");
        }
    }

    /// <summary>
    /// The spacing and type scales, rung by rung, <b>with their values</b>.
    ///
    /// <para><b>The literals are duplicated here on purpose, and the duplication is the whole
    /// point.</b> The first version of this guard asserted that each token <em>name</em> was
    /// declared in theme.css and referenced by app.css, and never read a value — so
    /// <c>--space-4: 4rem</c> passed, re-padding most boxes in the app, the narrow-viewport
    /// shell and the strip's bleed from 12px to 64px with the whole suite green. Nothing else
    /// in the suite pinned any <c>--space-*</c> at all.</para>
    ///
    /// <para>This is the pattern <c>PROGRESS.md</c> records for the server instructions and the
    /// baseline note: where the value <em>is</em> the deliverable, the value is the assertion,
    /// and what the duplicated literal buys is that changing it has to be deliberate and visible
    /// in a diff. A design scale is exactly that kind of subject — the point of it is that the
    /// rungs stop moving.</para>
    /// </summary>
    private static readonly (string Name, double Rem)[] SpaceScale =
    [
        ("--space-0", 0.125), ("--space-1", 0.25), ("--space-2", 0.375), ("--space-3", 0.5),
        ("--space-4", 0.75),  ("--space-5", 1.0),  ("--space-6", 1.5),   ("--space-7", 2.0),
        ("--space-8", 3.0),
    ];

    private static readonly (string Name, double Rem)[] TextScale =
    [
        ("--text-xs", 0.72), ("--text-sm", 0.82), ("--text-base", 0.97), ("--text-lg", 1.15),
        ("--text-xl", 1.4),  ("--text-2xl", 1.7), ("--text-3xl", 2.15),
    ];

    /// <summary>
    /// Every rung of a scale is declared in theme.css at the value recorded above, is asked for
    /// by app.css, and is the only rung of that scale in existence.
    ///
    /// <para><b>Four things, and the first version had only two of them.</b> A token declared
    /// and used by nothing is dead weight that reads as a decision — this app shipped a
    /// <c>.label-line</c> class applied to nothing for exactly that reason, and it was found by
    /// looking at a rendered page rather than by any test. A token used and not declared is a
    /// rule that silently does nothing, because an unresolvable <c>var()</c> invalidates the
    /// whole declaration: the browser drops it and no error appears anywhere. The value is
    /// checked because a name-only check pins nothing. And the rungs must be strictly
    /// increasing, because a scale whose steps are not ordered is a list.</para>
    ///
    /// <para><b>The set is read from every stylesheet in the payload, not from theme.css.</b>
    /// The range check's own comment said it stopped "one <c>--space-9</c> at a time, each
    /// locally reasonable" — and it computed the declared set from theme.css alone, so a rung
    /// declared inside a <c>:root</c> block in <em>app.css</em> joined the scale invisibly and
    /// the raw-length scan waved through every <c>var()</c> that used it. That is precisely the
    /// accumulation this rule exists to prevent, entering by the one door nobody watched.</para>
    /// </summary>
    [Theory]
    [InlineData("space")]
    [InlineData("text")]
    public void EveryRungOfAScaleIsDeclaredOnceAtItsRecordedValue(string which)
    {
        var scale = which == "space" ? SpaceScale : TextScale;
        var prefix = which == "space" ? "--space-" : "--text-";

        var app = WithoutCssComments(AppCss);

        foreach (var (name, rem) in scale)
        {
            // **Every declaration of the rung, not one of them.** `Assert.Contains` on the
            // expected literal was satisfied by the original while a duplicate lower in the same
            // `:root` block won the cascade — a fix-audit put `--space-4: 4rem` under `--space-8`
            // and the workhorse rung sat at 64px with the full suite green. So a rung is required
            // to be declared exactly once, and that once at its recorded value. Declaring it
            // twice is refused outright even if both copies agree: a scale with two homes for one
            // rung is a scale that will disagree with itself later.
            var declarations = DeclarationsOf(name);

            Assert.True(declarations.Count == 1,
                $"{name} is declared {declarations.Count} times in theme.css "
                + $"({string.Join(", ", declarations)}). A rung has one home.");

            Assert.Equal($"{rem.ToString(CultureInfo.InvariantCulture)}rem", declarations[0]);
            Assert.Contains($"var({name})", app, StringComparison.Ordinal);
        }

        // Strictly increasing, in the order recorded. A scale is an ordering before it is a set
        // of values, and two rungs that cross make every "a step down from" comment a lie.
        var values = scale.Select(s => s.Rem).ToList();
        Assert.Equal(values.Order(), values);
        Assert.Equal(values.Distinct().Count(), values.Count);

        // No rung of this scale declared anywhere but theme.css, and none there beyond the set
        // above. Both stylesheets and index.html are read: theme.css is the only file allowed
        // to declare one, so a :root block elsewhere is the interesting case.
        foreach (var (_, css) in new[] { ("app.css", AppCss), ("index.html", IndexHtml) })
            Assert.DoesNotMatch(Rx($@"{Regex.Escape(prefix)}[a-z0-9-]+\s*:"), WithoutCssComments(css));

        var declared = Rx($@"({Regex.Escape(prefix)}[a-z0-9-]+)\s*:")
            .Matches(WithoutCssComments(ThemeCss))
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(scale.Select(s => s.Name).Order(StringComparer.Ordinal), declared);
    }

    /// <summary>
    /// The three elevation steps are declared for both palettes, all three are used, and no two
    /// of them are the same shadow.
    ///
    /// <para>Three tokens holding one value is the state this scale replaced, spelled three ways
    /// — and a name-only check cannot tell the difference. The <em>values</em> differ per palette,
    /// which is why they are declared with the colours; what is asserted here is that within a
    /// palette the three steps are three steps.</para>
    /// </summary>
    [Theory]
    [InlineData("hero-light")]
    [InlineData("hero-dark")]
    [InlineData("villain-light")]
    [InlineData("villain-dark")]
    public void TheThreeElevationStepsAreThreeDifferentShadows(string palette)
    {
        // Resolved for the state that reaches this palette, rather than read off one block:
        // two of the four live inside a media query, and a block-shaped regex could not see
        // them at all.
        var resolved = Palette(StateFor(palette));

        var shadows = ElevationSteps
            .ToDictionary(name => name, name => Normalise(resolved.GetValueOrDefault(name, "")),
                StringComparer.Ordinal);

        Assert.DoesNotContain("", shadows.Values);
        Assert.Equal(3, shadows.Values.Distinct(StringComparer.Ordinal).Count());

        foreach (var name in shadows.Keys)
            Assert.Contains($"var({name})", WithoutCssComments(AppCss), StringComparison.Ordinal);
    }

    /// <summary>
    /// Elevation is a hierarchy, and <c>--shadow-3</c> is claimed by one element.
    ///
    /// <para>The sticky budget strip is the only thing in the app that moves independently of
    /// the document, and so the only thing that should read as floating over it. One
    /// <c>--shadow</c> used to carry the banner, every panel, the sheet, the tier cards and the
    /// strip — five heights spelled one way, which is the same as no height at all. Spreading
    /// the top step back across the page would undo the distinction without changing a single
    /// value, which is why this is asserted by count rather than by the token existing.</para>
    /// </summary>
    [Fact]
    public void OnlyTheStickyStripReadsAsFloating()
    {
        var floating = RulesOf(ScreenHalfOfAppCss)
            .Where(r => r.Declarations.Contains("var(--shadow-3)", StringComparison.Ordinal))
            .Select(r => r.Selector)
            .ToList();

        Assert.Equal([".budget"], floating);

        // The strip is sticky, which is the property that earns it the top step. Asserted here
        // rather than left implied: if it stops being sticky it stops being the exception.
        //
        // Every rule targeting `.budget`, not one of them — there are two, the base rule and
        // the narrow-viewport rule that re-states the bleed, and `Single` threw on the pair.
        //
        // **What actually applies, which took two goes to get right.** `Contains("position:
        // sticky")` was satisfied by a declaration overridden one line below it, so
        // `position: static` un-stuck the strip while it kept the top elevation step. Reading the
        // last declaration fixed that and left the other half: filtering on
        // `Selector == ".budget"` misses `.budget, .breakdown { position: static }` added later,
        // because a comma list is not that string — and the comment written with the first fix
        // claimed it read *every* rule targeting `.budget`, which it did not.
        Assert.Equal("sticky", EffectiveValue(ScreenHalfOfAppCss, ".budget", "position"));
    }

    /// <summary>
    /// A finding printed under the row that broke it is told apart from a warning by more than
    /// its colour — WCAG 1.4.1, and the same rule the budget strip's over-fill and the option
    /// rows' current-cursor ring already carry.
    ///
    /// <para><b>The whole substance of this is CSS</b>, which is why it lives here rather than
    /// in the bUnit suite: <c>RowFinding.razor</c>'s markup already carries the visible word
    /// "Error"/"Warning" (asserted in <c>RowFindingRenderTests</c>), and this is the other half
    /// — that the stylesheet does not undo the distinction by drawing both the same shape. An
    /// error's rule is solid; a warning's is dashed, read as the actually-applying declaration
    /// rather than assumed from the source.</para>
    /// </summary>
    [Fact]
    public void AFindingsErrorAndWarningStatesDifferInShapeNotOnlyColour()
    {
        var error = EffectiveValue(ScreenHalfOfAppCss, ".finding", "border-left", exact: true);
        var warning = EffectiveValue(ScreenHalfOfAppCss, ".finding.warning", "border-left", exact: true);

        Assert.True(error is not null, "app.css has no rule for .finding at all.");
        Assert.True(warning is not null, "app.css has no rule for .finding.warning.");

        Assert.Contains("solid", error!, StringComparison.Ordinal);
        Assert.Contains("dashed", warning!, StringComparison.Ordinal);

        // The colour differs too, but that alone would be exactly the failure this test exists
        // to catch — so the property under test is the *style* keyword above, not this.
        Assert.NotEqual(error, warning);
    }

    /// <summary>
    /// <b>A row's finding is on screen, not only reachable through a hidden element</b> — the
    /// constraint the whole design exists to satisfy: WCAG is explicit that information carried
    /// only by a tooltip is information some readers do not get, and the rows already had the
    /// plumbing (<c>aria-describedby</c>, an <c>sr-only</c> twin) for exactly that shape from the
    /// hover-description work. <c>RowFinding</c> deliberately does not reuse it.
    /// </summary>
    [Fact]
    public void ARowFindingIsOnScreenNotOnlyToAssistiveTechnology()
    {
        Assert.Equal("grid", EffectiveValue(ScreenHalfOfAppCss, ".row-findings", "display", exact: true));

        var source = File.ReadAllText(Path.Combine(WebRoot, "Components", "RowFinding.razor"));
        Assert.DoesNotContain("sr-only", source, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-describedby", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// No <c>calc()</c> on the screen side names a raw length. If a value is derived from a
    /// spacing rung, it derives from the <b>token</b>.
    ///
    /// <para><b>This is the class of bug the four-property scan let through, one property name
    /// outside its own scope.</b> <c>.sheet-section &gt; table</c> read
    /// <c>width: calc(100% - 1.2rem)</c> and was a matched pair with the 0.6rem inset on its
    /// siblings. Phase 0 moved the inset onto the scale and left the width behind, so a table's
    /// right edge fell 3.2px short of every other child of the box and of the heading bar above
    /// it — 8.00px of inset on the left against 11.20px on the right, on five boxes a sheet, in
    /// both palettes. Two reviewers found it independently by measuring a rendered sheet; no test
    /// could, because <c>width</c> is not padding, margin, gap or font-size.</para>
    ///
    /// <para>Banning the literal inside the <c>calc()</c> is the general fix, and it is a better
    /// rule than adding <c>width</c> to the property list would have been: what makes the bug
    /// possible is not the property, it is a number that has to agree with a token and has no
    /// way of doing so. <c>calc(100% - 2 * var(--space-3))</c> cannot fall out of step.</para>
    ///
    /// <para>Percentages and unitless factors are fine — <c>100%</c> is the container and
    /// <c>-1</c> and <c>2</c> are multipliers, not lengths.</para>
    ///
    /// <para><b>So is the whole viewport, and only the whole viewport.</b> <c>100vh</c> is the
    /// same kind of thing as <c>100%</c>: it names the container rather than a size somebody
    /// chose, and there is no token it could ever be expected to agree with. <c>37svh</c> is not
    /// — that is a chosen length wearing a viewport unit — so the exemption is pinned to the
    /// figure 100 rather than to the unit. Exempting the unit is the shape of mistake this file
    /// already records twice: an allow-list of units let <c>9pt</c> through, and its
    /// thirty-unit replacement let <c>9dvmin</c> and <c>4PX</c> through.</para>
    /// </summary>
    [Fact]
    public void NoScreenCalcNamesARawLength()
    {
        var calls = Rx(@"calc\(([^()]*(?:\([^()]*\)[^()]*)*)\)").Matches(ScreenHalfOfAppCss);

        Assert.NotEmpty(calls);

        var absolute = Rx(ANumberWithAUnit);

        foreach (Match call in calls)
        {
            // The percentage is a share of the container, not a length somebody chose — and the
            // whole viewport is the same statement in another unit. Both are stripped before the
            // scan; anything other than the whole of it still reads as a length.
            var body = Rx(@"\d*\.?\d+%").Replace(call.Groups[1].Value, " ");
            body = Rx(@"(?<![\d.])100(?:d|s|l)?v(?:h|w|b|i|min|max)\b").Replace(body, " ");

            var literal = absolute.Match(body);

            Assert.False(literal.Success,
                $"`calc({call.Groups[1].Value})` names the length '{literal.Value}'. If it is "
                + "derived from a spacing rung, derive it from the token: a literal that agrees "
                + "with a token today has no way of still agreeing tomorrow.");
        }
    }

    /// <summary>
    /// The chrome bands centre their contents on the same column as the shell, and reserve the
    /// same padding around it, at every breakpoint — so the step labels, the spend figure and
    /// the page heading share a left edge.
    ///
    /// <para><b>This replaces a guard on a mechanism that no longer exists, which is the better
    /// outcome of the two.</b> The step list and the budget strip used to live inside the shell
    /// and escape its padding with a negative margin; that bleed had to cancel the shell's
    /// padding exactly, and when the narrow-viewport query cut the padding and left the pull
    /// alone the band ran 8px past the page on both sides — scrollWidth 368 against clientWidth
    /// 360 at a 375px viewport. The bands are siblings of <c>main</c> now and are already the
    /// width of the window, so there is nothing to escape and no negative margin anywhere in the
    /// file. <b>An invariant is better deleted than guarded when the thing it constrains can be
    /// removed.</b></para>
    ///
    /// <para>What remains is a real requirement in the direction that cannot overflow: three
    /// bands agreeing on one column. It is the agreement that is asserted rather than any value,
    /// so the column may be re-padded to any rung as long as all three follow — and the media
    /// queries are discovered rather than listed, so a third breakpoint is covered the day it is
    /// added rather than the day somebody remembers it. That is the half the old bleed rules got
    /// wrong.</para>
    /// </summary>
    [Fact]
    public void TheChromeBandsShareTheShellsColumn()
    {
        var css = ScreenHalfOfAppCss;

        // The shell, and the inner column of every band outside it — **including the banner's**,
        // which the first version of this left out while the layout's own comment offered the
        // banner as the example the others follow. It was the only band not on the column.
        string[] columns = [".shell", ".banner-inner", ".steps-list", ".budget-strip", ".breakdown"];

        // The band elements themselves. A band is a full-width fill and may not pad its own
        // contents: padding here shifts the column inside it and nothing else, so
        // `.budget { padding: … var(--space-6) … }` moved the spend figure 24px off the heading
        // and stopped the rail running edge to edge, with the column checks all still passing.
        string[] bands = [".banner", ".steps", ".budget"];

        var scopes = new List<(string Where, string Css)> { ("the base rules", TopLevelOf(css)) };

        // **Every media query touching any of the columns, not just the ones that mention
        // `.shell`.** Gating on `.shell` meant a breakpoint re-padding only the chrome bands was
        // not a scope at all, so between two breakpoints the step labels sat 8px in while the
        // heading sat 24px in — the exact "stop sharing a left edge" failure this test is for,
        // arriving in a query the scan declined to look at.
        foreach (var (condition, body) in MediaQueriesOf(css))
            if (columns.Any(c => body.Contains(c, StringComparison.Ordinal)))
                scopes.Add(($"@media {condition}", body));

        Assert.True(scopes.Count >= 2,
            "Only one scope touching a column was found. The narrow-viewport query pads all five, "
            + "so the query scan is not reading the stylesheet.");

        // Every column is capped on the same token, and **centred**, and neither was fully
        // checked. `max-width` was read in the base rules only, so a new breakpoint widening
        // `.shell` alone passed while the doc claimed queries were discovered rather than listed;
        // and centring was not read at all, so `.budget-strip { margin: 0 }` put the spend figure
        // flush against the window edge while the labels above and the heading below stayed on the
        // column — the exact failure this test's summary names.
        foreach (var column in columns)
        {
            Assert.Equal("var(--column)", EffectiveValue(css, column, "max-width"));

            var margin = EffectiveValue(css, column, "margin");
            Assert.True(margin is not null && margin.Contains("auto", StringComparison.Ordinal),
                $"{column} is capped on --column but not centred (margin: {margin ?? "unset"}), so "
                + "it sits at the left edge of a band that runs the whole window.");
        }

        // And no band pads its own contents, **by any spelling**. Asked through
        // `HorizontalPaddingTokenOf` this passed for `padding-inline: var(--space-6)`, because that
        // reader returned null for "no padding" and for "a spelling I do not read" alike — so the
        // question is put to the one that only asks whether the property is set at all.
        foreach (var band in bands)
            Assert.False(SetsAnyHorizontalPadding(css, band),
                $"{band} sets horizontal padding. A band is a full-width fill; the padding belongs "
                + "to the column inside it, or the two stop agreeing and the rail stops running "
                + "edge to edge.");

        // And every band reserves the same padding around it, in each scope that sets any of
        // them. A scope that re-pads the shell and forgets a band is the failure the old bleed
        // rules had, arriving from the other side.
        foreach (var (where, scope) in scopes)
        {
            var reserved = columns
                .Select(c => (Column: c, Padding: HorizontalPaddingTokenOf(scope, c)))
                .Where(p => p.Padding is not null)
                .ToList();

            if (reserved.Count == 0) continue;

            Assert.True(reserved.Count == columns.Length,
                $"{where} pads {string.Join(", ", reserved.Select(r => r.Column))} but not "
                + $"{string.Join(", ", columns.Except(reserved.Select(r => r.Column)))}. All "
                + $"{columns.Length} columns narrow together or they stop sharing a left edge.");

            Assert.True(reserved.Select(r => r.Padding).Distinct(StringComparer.Ordinal).Count() == 1,
                $"{where} reserves different padding per band: "
                + string.Join(", ", reserved.Select(r => $"{r.Column} {r.Padding}")));
        }
    }

    /// <summary>
    /// Every <c>@media</c> block, as its condition and its body, found by <b>matching braces</b>
    /// rather than by pattern.
    ///
    /// <para><b>The regex this replaces assumed a formatted block</b> — it ended at the first
    /// <c>\n}</c> — so a query written on one line was not found at all, and its contents were
    /// swallowed into whichever block *did* end that way. A fix-audit's single-line breakpoint duly
    /// evaded the scan, and the guard's own doc claims the queries are discovered. CSS formatting is
    /// not a property a guard may depend on.</para>
    /// </summary>
    private static List<(string Condition, string Body)> MediaQueriesOf(string css)
    {
        var found = new List<(string, string)>();

        foreach (Match at in Rx(@"@media\s*([^{]+)\{").Matches(css))
        {
            var open = at.Index + at.Length - 1;
            var depth = 0;

            for (var i = open; i < css.Length; i++)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}' && --depth == 0)
                {
                    found.Add((at.Groups[1].Value.Trim(), css[(open + 1)..i]));
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>The stylesheet with every <c>@media</c> block's contents removed.</summary>
    private static string TopLevelOf(string css)
    {
        var stripped = css;

        // Longest first, so removing an outer block cannot invalidate an inner one's offsets.
        foreach (var (_, body) in MediaQueriesOf(css).OrderByDescending(q => q.Body.Length))
            stripped = stripped.Replace(body, " ", StringComparison.Ordinal);

        return stripped;
    }

    /// <summary>
    /// The single <c>--space-*</c> token a selector's horizontal padding resolves to, or null if
    /// it sets none or sets the two sides differently. Reads both the shorthand and the
    /// longhands, and reads every rule for the selector rather than the first — the narrow
    /// query writes longhands and the base rule writes a shorthand.
    /// </summary>
    private static string? HorizontalPaddingTokenOf(string css, string selector)
    {
        string? left = null, right = null;

        // **Comma lists split.** This filtered on the whole selector string being equal, which
        // is the same weakness a fix-audit found in the sticky-strip guard — and it bit
        // immediately: the narrow-viewport rule pads the three chrome columns in one grouped
        // rule, so `.steps-list, .budget-strip, .breakdown` matched none of them and the padding
        // read as absent. Exact per-selector match, not a suffix: this asks "does this rule pad
        // this band", and a descendant selector padding something else is not an answer.
        foreach (var declarations in RulesOf(css)
                     .Where(r => r.Selector.Split(',').Select(Normalise).Contains(Normalise(selector), StringComparer.Ordinal))
                     .Select(r => r.Declarations))
        {
            if (Rx(@"padding:\s*([^;}]+)").Match(declarations) is { Success: true } shorthand)
            {
                // The same four-side reader the bleed uses, rather than a second splitter that
                // happens to work on the shorthands in the file today. This one split on plain
                // whitespace, which is correct only while no padding contains a `calc()`.
                var sides = MarginSides(shorthand.Groups[1].Value);
                if (sides is not null)
                {
                    right = TokenIn(sides[1]);
                    left = TokenIn(sides[3]);
                }
            }

            // `padding-inline` sets the same two sides under a name sharing no prefix with them,
            // and this reader knew only the physical pair — so `padding-inline: var(--space-6)` on
            // a band read as "no padding at all" and walked past the guard. Read as one of the
            // spellings rather than refused, because it is the modern way to write exactly this
            // and the file may reasonably use it.
            if (Rx(@"padding-inline:\s*([^;}]+)").Match(declarations) is { Success: true } inline)
            {
                var sides = MarginSides(inline.Groups[1].Value);
                if (sides is not null) { right = TokenIn(sides[1]); left = TokenIn(sides[3]); }
            }

            if (Rx(@"padding-left:\s*([^;}]+)").Match(declarations) is { Success: true } l) left = TokenIn(l.Groups[1].Value);
            if (Rx(@"padding-right:\s*([^;}]+)").Match(declarations) is { Success: true } r) right = TokenIn(r.Groups[1].Value);
            if (Rx(@"padding-inline-start:\s*([^;}]+)").Match(declarations) is { Success: true } s) left = TokenIn(s.Groups[1].Value);
            if (Rx(@"padding-inline-end:\s*([^;}]+)").Match(declarations) is { Success: true } e) right = TokenIn(e.Groups[1].Value);
        }

        return left is not null && left == right ? left : null;
    }

    /// <summary>
    /// Every spelling that sets horizontal padding, so a caller asking "does this element pad its
    /// contents" can be sure a null means none rather than a name the reader above skipped.
    /// </summary>
    private static readonly string[] HorizontalPaddingSpellings =
    [
        "padding", "padding-left", "padding-right",
        "padding-inline", "padding-inline-start", "padding-inline-end",
    ];

    /// <summary>
    /// Whether a selector sets horizontal padding by <b>any</b> spelling — the question
    /// <see cref="HorizontalPaddingTokenOf"/> cannot answer, because it returns null both for "no
    /// padding" and for "padding this reader could not resolve to a rung".
    /// </summary>
    private static bool SetsAnyHorizontalPadding(string css, string selector) =>
        RulesFor(css, selector, exact: true)
            .Any(r => HorizontalPaddingSpellings.Any(p =>
                Rx($@"(?<![\w-]){Regex.Escape(p)}\s*:").IsMatch(r)));

    /// <summary>
    /// A <c>margin</c> shorthand as its four sides, top-right-bottom-left, or null if it cannot
    /// be read. Splitting on whitespace does not work: <c>calc(-1 * var(--space-6))</c> contains
    /// three spaces and would arrive as four parts, which is how the caller above came to be
    /// unable to tell one edge from another.
    /// </summary>
    private static string[]? MarginSides(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();

        foreach (var c in value.Trim())
        {
            if (c == '(') depth++;
            if (c == ')') depth--;

            if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) parts.Add(current.ToString());

        return parts.Count switch
        {
            1 => [parts[0], parts[0], parts[0], parts[0]],
            2 => [parts[0], parts[1], parts[0], parts[1]],
            3 => [parts[0], parts[1], parts[2], parts[1]],
            4 => [.. parts],
            _ => null,
        };
    }

    /// <summary>
    /// The rung a single box side is set to, or null — and <b>anchored</b>, so the side has to be
    /// exactly that token and nothing else.
    ///
    /// <para><b>This took the first token it found anywhere inside the side, which is the same
    /// first-match weakness the four-side parser was written to remove, surviving one level
    /// down.</b> A fix-audit got through with <c>calc(var(--space-6) * 3)</c> — 72px of padding
    /// reported as matching a 24px bleed. Anchoring means anything more complicated than a bare
    /// token returns null and the caller reports "sets no horizontal padding token", which fails.
    /// That is the right direction: this exists to compare two rungs, and a side doing arithmetic
    /// is not a rung.</para>
    ///
    /// <para>Its negative twin, <c>NegativeTokenIn</c>, went with the bleed it read — the chrome
    /// bands are siblings of <c>main</c> now and there is no negative margin left in the
    /// stylesheet for it to parse. A test helper kept past its subject is the same dead weight as
    /// a CSS class applied to nothing.</para>
    /// </summary>
    private static string? TokenIn(string value) =>
        Rx(@"^var\(\s*(--space-[a-z0-9-]+)\s*\)$").Match(Normalise(value)) is { Success: true } m
            ? m.Groups[1].Value
            : null;

    /// <summary>
    /// The chrome always ends in a visible edge, on the one band that always renders.
    ///
    /// <para><b>This is the most severe defect of the phase, and it was fixed without a guard
    /// until a mutation put it straight back.</b> <c>.steps</c> lost its bottom rule on the
    /// argument that the budget strip directly beneath carries the edge for both — and the strip
    /// is absent on three whole classes of screen: <b>every page in Villain mode</b>, since Ch.9
    /// gives Villains no budget and the component renders nothing at all; <b>the tier page before
    /// a tier is chosen</b>, which is the first screen a new visitor sees; <b>every
    /// <c>/replay</c> route</b>, where the layout hides it deliberately; and <b><c>/admin</c></b>,
    /// which hides it for the same reason a recording does — the page is not a character. On all
    /// four the step chips sat on the page ground with nothing under them.</para>
    ///
    /// <para>So the requirement is on the band that is always there. The three conditions are
    /// asserted as well as the edge, because they are the *reason* for it: if the strip ever
    /// became unconditional the argument for a rule here would change, and a guard whose premise
    /// has quietly gone is worse than none.</para>
    /// </summary>
    [Fact]
    public void TheChromeAlwaysEndsInAVisibleEdge()
    {
        // `exact: true`, because the question is "does the step band still declare this" rather
        // than "what applies to a step band somewhere". Without it a `.sheet .steps` rule —
        // matching no element in this app, since no step nav renders inside a sheet — supplied the
        // value while the real rule had none. That is verbatim the defeat recorded on the `exact`
        // parameter itself from an earlier audit, reached again by a guard that did not pass it.
        var edge = EffectiveValue(ScreenHalfOfAppCss, ".steps", "border-bottom", exact: true);

        Assert.True(edge is not null,
            "The step band sets no bottom edge. The budget strip below it does not render in "
            + "Villain mode, before a tier is chosen, or on a replay route — so on those screens "
            + "nothing closes the chrome and the step chips sit on the page ground.");

        AssertIsAVisibleEdge(".steps", "border-bottom", edge!);

        // The premise. Each of these is what makes the strip conditional; together they are why
        // the edge cannot be left to it.
        var strip = File.ReadAllText(Path.Combine(WebRoot, "Components", "HpBudgetBar.razor"));
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Layout", "MainLayout.razor"));

        Assert.Contains("Session.ShowBudget", strip, StringComparison.Ordinal);
        Assert.Contains("SelectedTierId is not null", strip, StringComparison.Ordinal);
        // The strip renders on the character generator's own routes and nowhere else. Read as
        // the whole condition rather than as a word inside it: `Where == Area.Play` is what makes
        // it conditional, and an assertion on some fragment of that would survive the day it
        // becomes unconditional.
        Assert.Contains("@if (Where == Area.Play)\r\n{\r\n    <HpBudgetBar />",
            layout.ReplaceLineEndings("\r\n"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Three treatments that were <b>fixed and left unguarded</b>, so each of their defects reverted
    /// green when a fix-audit deleted the one line that repairs it.
    ///
    /// <para>Each is a single declaration whose whole substance is in the stylesheet, so no
    /// rendering test can see any of them — the class stays on the element and the markup is
    /// identical either way. Together they are the same lesson three more times: a repair without a
    /// guard is a repair that lasts until somebody tidies the file.</para>
    ///
    /// <list type="bullet">
    ///   <item><c>.options</c>'s bottom rule marks where a 22rem scroller <em>clips</em> 141 Powers.
    ///     Without it a row cut through the middle of its own stat line reads as a rendering fault
    ///     against the panel's white, with only the scrollbar thumb saying otherwise.</item>
    ///   <item><c>.grid &gt; .field</c>'s reset. A field carries its own bottom margin and a grid row
    ///     already supplies one, so without it the Sources editor's rows sit 32px apart and its
    ///     columns 16px.</item>
    ///   <item>The untouched ring on the tab strip. Deleting the rule leaves the class on the
    ///     element, every assertion in <c>EmptyStateTests</c> reading <c>ClassList</c>, and no
    ///     marking on the page — the <c>.hp</c> trap yet again, on the sibling of the feature the
    ///     change had just closed it for.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheTreatmentsThatOnlyExistInTheStylesheetAreStillThere()
    {
        var css = ScreenHalfOfAppCss;

        // The scroller is clipped, and says so.
        var clip = EffectiveValue(css, ".options", "border-bottom", exact: true);
        Assert.True(clip is not null,
            "`.options` has no bottom rule, so nothing marks where the list is clipped and a "
            + "half-row against the panel's own ground reads as a rendering fault.");
        AssertIsAVisibleEdge(".options", "border-bottom", clip!);

        // A grid cell does not add to the gap the grid already supplies.
        Assert.Equal("0", EffectiveValue(css, ".grid > .field", "margin-bottom", exact: true));

        // And the untouched marker is drawn.
        var ring = RulesFor(css, ".tab-count.untouched", exact: false);
        Assert.NotEmpty(ring);
        Assert.Contains(ring, r => Rx(@"border\s*:").IsMatch(r));
        Assert.All(ring, r =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(r)),
                    $"A rule targeting .tab-count.untouched hides it with '{way}'.")));
    }

    /// <summary>
    /// An empty state is marked out as guidance, and the marking is entirely typographic — so it
    /// is entirely in the stylesheet, where no rendering test can see it.
    ///
    /// <para><b>Deleting the whole <c>.empty-state</c> rule left every one of the eight
    /// <c>EmptyStateTests</c> green, and both class-ownership tests too.</b> The class is still on
    /// the element and every one of those assertions reads markup, so the leading edge and the
    /// padding — the entire mechanism that stops the sentence reading as the first row of the list
    /// it stands in for — could go without a word. That is the <c>.hp</c> trap this file already
    /// records verbatim, reproduced by the change that cites it as a lesson.</para>
    ///
    /// <para>Checked over every rule targeting the class rather than the base one, because a more
    /// specific rule further down wins the cascade — the same reason the <c>.hp</c> guard does it.</para>
    /// </summary>
    [Fact]
    public void AnEmptyStateIsMarkedOutAsGuidance()
    {
        var rules = RulesTargeting(".empty-state");

        Assert.NotEmpty(rules);

        var edge = EffectiveValue(ScreenHalfOfAppCss, ".empty-state", "border-left", exact: true);

        Assert.True(edge is not null,
            "The empty state has no leading edge, so it reads as the first row of the list it is "
            + "standing in for rather than as guidance about it.");

        AssertIsAVisibleEdge(".empty-state", "border-left", edge!);

        // And set in from that edge, or the rule sits against the text.
        Assert.True(Rx(@"padding[^;}]*:[^;}]*var\(--space-").IsMatch(string.Join(';', rules)),
            "The empty state sets no padding, so its leading edge touches the sentence.");

        // Never hidden. Everything above is satisfied by an element that is present and invisible,
        // and every rendering test passes on one too — a hidden element keeps its class and its
        // text. The shared list, not a fresh one: the first version written here had four entries
        // and none of the prefix guards, so it would have fired on `opacity: 0.8`.
        Assert.All(rules, rule =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(rule)),
                    $"A rule targeting .empty-state hides it with '{way}': {Normalise(rule)}")));
    }

    /// <summary>
    /// The smallest type rung is exactly the size <c>--muted</c> was measured against.
    ///
    /// <para>theme.css holds <c>--muted</c> to a 4.5:1 contrast floor rather than 3:1, and says
    /// why: it carries the explanatory prose at this size. That measurement is a statement
    /// about a number, and rounding the bottom rung of the type scale down to fit a ratio would
    /// invalidate it silently — the colour would still pass its own test, at a size nobody
    /// checked. <b>The floor is the anchor the scale is built from, not a value the scale
    /// happens to produce.</b></para>
    /// </summary>
    [Fact]
    public void TheSmallestTypeRungIsTheSizeTheMutedInkWasMeasuredAt()
    {
        var xs = Rx(@"--text-xs:\s*([0-9.]+)rem").Match(WithoutCssComments(ThemeCss));

        Assert.True(xs.Success, "theme.css does not declare --text-xs in rem.");
        Assert.Equal(0.72, double.Parse(xs.Groups[1].Value, CultureInfo.InvariantCulture), 3);
    }

    /// <summary>
    /// Every value theme.css declares for one custom property, in source order.
    ///
    /// <para>The <b>list</b> rather than the value, because "how many times is this declared" is
    /// the question a <c>Contains</c> cannot answer, and a second declaration lower down is how
    /// three guards in this file were defeated at once. Only the screen half is read: the print
    /// block deliberately restates most of the palette, and a restatement there is the mechanism
    /// rather than a duplicate.</para>
    /// </summary>
    private static List<string> DeclarationsOf(string token)
    {
        var screen = WithoutCssComments(ThemeCss);
        screen = screen[..screen.IndexOf("@media print", StringComparison.Ordinal)];

        return [.. Rx($@"(?<![\w-]){Regex.Escape(token)}\s*:\s*([^;}}]+)")
            .Matches(screen)
            .Select(m => Normalise(m.Groups[1].Value))];
    }

    /// <summary>
    /// The type scale as theme.css declares it: token name to size in rem.
    ///
    /// <para><b>A duplicate throws rather than resolving.</b> That began as an accident of
    /// <c>ToDictionary</c> — and it was the accident protecting the type scale from the shadowing
    /// mutation that got through against the spacing scale, so it is now deliberate and says so.
    /// A token with two declarations has no single answer here, and quietly taking either one is
    /// how a guard reads a value that the browser does not use.</para>
    /// </summary>
    private static Dictionary<string, double> TypeScale
    {
        get
        {
            var found = Rx(@"(--text-[a-z0-9-]+):\s*([0-9.]+)rem")
                .Matches(WithoutCssComments(ThemeCss))
                .Select(m => (Name: m.Groups[1].Value, Rem: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
                .ToList();

            var twice = found.GroupBy(f => f.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();

            Assert.True(twice.Count == 0,
                $"theme.css declares {string.Join(", ", twice.Select(g => g.Key))} more than once. "
                + "Whichever this helper picked would be a value the cascade might not use.");

            return found.ToDictionary(f => f.Name, f => f.Rem, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every font size a declaration block sets, in rem, <b>with the type scale resolved</b>.
    ///
    /// <para>The three typographic guards in this file — the Hero Point cost, the rank word and
    /// the trait Source line — assert that a size falls in a band, because the relationship
    /// between two sizes is the whole substance of each rule and <c>Contains("font-size:")</c>
    /// is satisfied by a cost set three times the size of the rank beside it. Each read the
    /// number out of the declaration with a regex, which stopped working the moment the sizes
    /// became tokens.</para>
    ///
    /// <para><b>Resolving is the fix, and loosening the band would have been the bug.</b> The
    /// obvious repair — accept a <c>var()</c> and skip the range check — turns three measured
    /// assertions into three assertions that a property is present, which is the exact weakness
    /// every one of their doc comments records being hardened against. Resolved, they are
    /// stronger than before: they now also catch a scale rung moved to a wrong value, which no
    /// literal read could see.</para>
    ///
    /// <para>An unresolvable token <b>throws</b> rather than being skipped. A <c>var()</c>
    /// naming a token that does not exist makes the whole declaration invalid, so the browser
    /// drops it and the element falls back to inherited size — a real failure that looks like
    /// nothing. Sizes in <c>pt</c> are skipped by design: they belong to the print block, which
    /// has its own floor test, and this reads rem.</para>
    /// </summary>
    private static List<double> FontSizesInRem(string declarations)
    {
        var sizes = new List<double>();

        foreach (Match declaration in Rx(@"font-size:\s*([^;}]+)").Matches(declarations))
        {
            var value = declaration.Groups[1].Value.Trim();

            if (Rx(@"var\(\s*(--[a-z0-9-]+)\s*\)").Match(value) is { Success: true } token)
            {
                var name = token.Groups[1].Value;

                if (!TypeScale.TryGetValue(name, out var rem))
                    throw new InvalidOperationException(
                        $"`font-size: {value}` asks for {name}, which theme.css does not declare "
                        + "as a rem size. An unresolvable var() invalidates the whole "
                        + "declaration and the browser drops it.");

                sizes.Add(rem);
                continue;
            }

            if (Rx("^([0-9.]+)rem$").Match(value) is { Success: true } literal)
            {
                sizes.Add(double.Parse(literal.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }

            // Paper, or a keyword like `inherit`. Anything else carrying a number is a unit
            // this helper does not understand, and silently skipping it would make the band
            // assertions above vacuous.
            if (!value.EndsWith("pt", StringComparison.Ordinal) && Rx(@"\d").IsMatch(value))
                throw new InvalidOperationException(
                    $"`font-size: {value}` is a length this helper cannot resolve, so no band "
                    + "assertion can be made about it. Use a --text-* token.");
        }

        return sizes;
    }

    /// <summary>
    /// The value of <paramref name="property"/> that actually applies to <paramref name="selector"/>
    /// — the <b>last</b> one declared, across <b>every</b> rule that targets it — or null.
    ///
    /// <para><b>Three separate guards in this file were defeated by one root cause, and the fix
    /// for it was already here twice.</b> A fix-audit reached the same bad end state three
    /// different ways, each time by declaring the thing <em>again</em> rather than by editing the
    /// declaration the guard was reading:</para>
    ///
    /// <list type="bullet">
    ///   <item>a duplicate <c>--space-4: 4rem</c> lower in the same <c>:root</c> block — the
    ///     pinned-value check is a <c>Contains</c>, the shadowed original still satisfied it, and
    ///     the workhorse rung sat at 64px with the whole suite green;</item>
    ///   <item><c>.budget, .breakdown { position: static }</c> added later in the file — the strip
    ///     un-stuck while keeping the top elevation step, because the guard filtered on
    ///     <c>Selector == ".budget"</c> and a comma list is not that string, while its own new
    ///     comment claimed it read every rule targeting <c>.budget</c>;</item>
    ///   <item><c>border-bottom: none</c> instead of deleting the line — the exemption's
    ///     precondition asked whether the property <em>name</em> appeared, which is the same
    ///     "the string is still there" weakness it was written to replace.</item>
    /// </list>
    ///
    /// <para>So the instrument is the cascade rather than a substring: comma lists split,
    /// suffix-matched the way <see cref="RulesTargeting"/> already does, and the last declaration
    /// wins. It does not resolve specificity, and that is deliberately the safe direction — every
    /// one of those three mutations was a later override, which is what this does model.</para>
    /// </summary>
    /// <param name="exact">
    /// When true, only a rule whose selector <b>is</b> <paramref name="selector"/> counts, rather
    /// than any selector ending in it.
    ///
    /// <para>Suffix matching is right for asking "what applies to this element", and wrong for
    /// asking "does this rule still say this". A fix-audit used the difference: with the real
    /// <c>.budget-toggle</c> rule stripped of its <c>border-bottom</c>, adding
    /// <c>.sheet .budget-toggle { border-bottom: … }</c> — a selector that matches nothing in this
    /// app, since no budget strip renders inside a sheet — supplied the precondition and the guard
    /// passed. A source-reading test cannot tell which selectors match real elements, so the
    /// exemption records which selector has to carry its reason instead of accepting any that
    /// mentions it.</para>
    /// </param>
    /// <param name="css">The stylesheet to read — normally the screen half of <c>app.css</c>.</param>
    /// <param name="selector">The selector to look for, matched by suffix unless <c>exact</c>.</param>
    /// <param name="property">The property whose winning value is wanted.</param>
    private static string? EffectiveValue(string css, string selector, string property, bool exact = false)
    {
        var rules = RulesFor(css, selector, exact);

        // **Anything that could change this property but is spelled differently makes the answer
        // unavailable, rather than silently not counting.** This read one spelling per property, and
        // a fix-audit defeated four separate guards through the gap in exactly the same way:
        // `border-bottom-color: transparent` beat a `border-bottom` check, `border-left-width: 0`
        // beat a `border-left` check, `margin-left: 0` beat a `margin` check, and
        // `padding-inline: …` beat a padding check. Every one of them left the guard reading the
        // shorthand it knew and reporting the value it wanted.
        //
        // Modelling the cascade across longhands and logical properties properly is a bigger job
        // than any of these guards needs, so this fails instead — an unreadable answer is a red
        // test, and a red test is the safe direction. If a stylesheet ever legitimately needs one
        // of these spellings, the guard for it gets written then rather than guessed at now.
        // Every declaration that decides this property, in source order — the property itself and
        // any spelling that can override it.
        var deciding = rules
            .SelectMany(r => Rx(@"(?<![\w-])([a-z-]+)\s*:\s*([^;}]+)").Matches(r)
                                 .Select(m => (Property: m.Groups[1].Value, Value: Normalise(m.Groups[2].Value))))
            .Where(d => d.Property == property || CouldOverride(property, d.Property))
            .ToList();

        if (deciding.Count == 0) return null;

        // **Order is what makes this correct rather than merely strict.** `.budget-toggle` writes
        // `border: none` and then `border-bottom: …`, which the cascade resolves the way the author
        // meant — so a check that simply refused any related spelling would fail on correct CSS.
        // What is not readable is a *later* spelling this helper does not model, which is exactly
        // how a fix-audit beat four guards: `border-bottom-color: transparent` after a
        // `border-bottom`, `border-left-width: 0` after a `border-left`, `margin-left: 0` after a
        // `margin`, `padding-inline` instead of the pair. Each left the guard reading a declaration
        // the cascade no longer decides.
        var last = deciding[^1];

        Assert.True(last.Property == property,
            $"A rule targeting `{selector}` sets `{last.Property}: {last.Value}` after the last "
            + $"`{property}`, so it — not `{property}` — is what the cascade resolves. This reader "
            + $"does not model that spelling. Write the intent as `{property}`, or teach "
            + "CouldOverride and this helper to read the other one.");

        return last.Value;
    }

    /// <summary>
    /// A border shorthand actually draws something: a non-zero width, a style that is not
    /// <c>none</c>, and an ink that is not transparent.
    ///
    /// <para><b>Checking for the absence of the word <c>none</c> is not enough, and a fix-audit got
    /// through both edge guards on that.</b> <c>border-left: 0 solid var(--rule)</c> contains no
    /// <c>none</c>, contains the right token, and draws nothing at all — so the empty state lost
    /// its leading edge and the step band could have lost the edge that closes the chrome, with
    /// both guards green. "It mentions a colour" is not "it is visible".</para>
    /// </summary>
    private static void AssertIsAVisibleEdge(string selector, string property, string value)
    {
        Assert.DoesNotContain("none", value, StringComparison.Ordinal);
        Assert.DoesNotContain("transparent", value, StringComparison.Ordinal);

        // A zero width, in any unit or none. `0`, `0px` and `0rem` all draw nothing.
        Assert.False(Rx($@"(?<![.\d])0({CssZeroUnits})?(?![.\d])").IsMatch(value),
            $"`{selector} {{ {property}: {value} }}` has a zero width, so it draws nothing — the "
            + "value names an ink and an edge that is not there.");

        // And it is the hairline token rather than a length chosen here, which is the same rule the
        // rest of the file lives by.
        Assert.Contains("var(--rule", value, StringComparison.Ordinal);
    }

    /// <summary>Units a zero width might carry. A bare <c>0</c> is matched by the optional group.</summary>
    private const string CssZeroUnits = "px|rem|em|pt|mm|cm|in|ex|ch";

    /// <summary>The declaration blocks of every rule targeting a selector, in source order.</summary>
    private static List<string> RulesFor(string css, string selector, bool exact)
    {
        var wanted = Normalise(selector);

        return [.. RulesOf(css)
            .Where(r => r.Selector.Split(',').Select(Normalise)
                         .Any(s => s == wanted || (!exact && s.EndsWith(wanted, StringComparison.Ordinal))))
            .Select(r => r.Declarations)];
    }

    /// <summary>
    /// Whether <paramref name="other"/> can change what <paramref name="property"/> resolves to —
    /// a longhand of it, or its logical-property equivalent.
    ///
    /// <para>The logical family is the half that is easy to forget, and a fix-audit used it twice:
    /// <c>padding-inline</c> and <c>margin-inline</c> set the same two sides as the
    /// <c>-left</c>/<c>-right</c> pair every reader here was written against, under names that share
    /// no prefix with them.</para>
    /// </summary>
    private static bool CouldOverride(string property, string other)
    {
        // **`all` resets every property there is, and this helper could not see it.** A fix audit
        // put `.book-text { all: unset; display: block }` after the real rule: the whole suite
        // stayed green while the box lost its background, its padding and its left edge in any
        // real browser. It is one keyword that defeats every guard in this file at once, which
        // makes it worth the first line rather than a case among the aliases below.
        if (other == "all") return true;

        // A longhand: `border-bottom` is changed by `border-bottom-color`, `padding` by
        // `padding-left`. Also the other way round, since a shorthand resets a longhand.
        if (other.StartsWith($"{property}-", StringComparison.Ordinal)) return true;
        if (property.StartsWith($"{other}-", StringComparison.Ordinal)) return true;

        var logical = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["margin"] = ["margin-inline", "margin-block"],
            ["margin-left"] = ["margin-inline", "margin-inline-start", "margin-inline-end"],
            ["margin-right"] = ["margin-inline", "margin-inline-start", "margin-inline-end"],
            ["padding"] = ["padding-inline", "padding-block"],
            ["padding-left"] = ["padding-inline", "padding-inline-start", "padding-inline-end"],
            ["padding-right"] = ["padding-inline", "padding-inline-start", "padding-inline-end"],
            ["border-bottom"] = ["border-block-end", "border-block", "border"],
            ["border-left"] = ["border-inline-start", "border-inline", "border"],
            ["max-width"] = ["max-inline-size", "inline-size", "width"],
        };

        // **Not everything spelled `border-…` is part of the `border` shorthand**, and reading it
        // that way made the helper refuse to answer about correct CSS: `border-radius` after a
        // `border-left` reported that the radius was what the cascade resolved for the edge, which
        // is not a thing a radius can do. These four are separate properties that happen to share
        // the prefix — `border` resets none of them and none of them resets `border`.
        //
        // The direction matters: this makes the helper answer where it used to refuse, so it is a
        // narrowing of a guard rather than a widening. It is scoped to four names for that reason,
        // rather than to a rule about prefixes.
        if (NotPartOfTheBorderShorthand(other)) return false;

        return logical.TryGetValue(property, out var aliases)
               && aliases.Any(a => a == other || other.StartsWith($"{a}-", StringComparison.Ordinal));
    }

    /// <summary>
    /// Properties that begin <c>border-</c> and are not part of the <c>border</c> shorthand.
    ///
    /// <para>Matched on the whole name or a longhand of it, so <c>border-radius</c>,
    /// <c>border-top-left-radius</c> and <c>border-image-source</c> are all covered.</para>
    /// </summary>
    private static bool NotPartOfTheBorderShorthand(string property) =>
        property is "border-radius" or "border-collapse" or "border-spacing" or "border-image"
        || property.EndsWith("-radius", StringComparison.Ordinal)
        || property.StartsWith("border-image-", StringComparison.Ordinal);

    /// <summary>
    /// The stylesheet up to its print block, with the <c>@page</c> box removed. Comments stripped.
    ///
    /// <para><b><c>@page</c> is paper and it is not inside <c>@media print</c>.</b> It sits just
    /// above it, so cutting at the first <c>@media print</c> left <c>@page { margin: 14mm 13mm }</c>
    /// in the region this file calls "the screen half" — and the raw-length guard's own doc claim
    /// that the print rules are out of scope was false for it. It passed only because <c>mm</c>
    /// was missing from the unit list, so closing that gap would have turned a legitimate print
    /// declaration red. Excluded by name, which is the honest fix: the page box is a paper rule
    /// wherever it is written.</para>
    ///
    /// <para><b>The first version of this note cited <c>ThePageIsA4WithMargins</c> as the
    /// compensating check and that was false when written.</b> That test read the *first*
    /// <c>@page</c> while this strips *every* one of them, so a second page box after the A4
    /// block — A5 landscape, margin 0 — won the cascade and was seen by nothing. It reads all of
    /// them and requires exactly one now, which is what makes stripping them here safe. A
    /// pseudo-page such as <c>@page :first</c> is stripped too, and counted there.</para>
    /// </summary>
    private static string ScreenHalfOfAppCss
    {
        get
        {
            var css = WithoutCssComments(AppCss);
            var print = css.IndexOf("@media print", StringComparison.Ordinal);
            Assert.True(print > 0, "app.css has no @media print block, so the screen half cannot be bounded.");

            var screen = css[..print];

            // Assert it was there before removing it: an @page block that has moved or been
            // renamed would otherwise be silently un-excluded, which is the stale-exemption
            // shape this file has been caught by twice.
            Assert.Contains("@page", screen, StringComparison.Ordinal);
            return Rx(@"@page\b[^{]*\{[^{}]*\}").Replace(screen, " ");
        }
    }

    /// <summary>
    /// Every <c>selector { declarations }</c> pair in a stylesheet, flattened — a rule inside an
    /// <c>@media</c> query is returned as itself, which is what makes the mobile query's own
    /// paddings visible to the scan above. Comments must already be stripped.
    /// </summary>
    private static List<(string Selector, string Declarations)> RulesOf(string css) =>
        [.. Rx(@"([^{}]+)\{([^{}]*)\}")
            .Matches(css)
            .Select(m => (Selector: Rx(@"\s+").Replace(m.Groups[1].Value.Trim(), " "),
                          Declarations: m.Groups[2].Value))];

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
    /// No script sets a theme token either.
    ///
    /// <para>The colour and typeface rules read stylesheets, and a stylesheet is not the only
    /// way a token gets a value: <c>documentElement.style.setProperty("--heading", …)</c> wins
    /// over every rule in theme.css, is invisible to both scans, and is not something the
    /// Content-Security-Policy has any opinion about. The one thing the app's scripts may do to
    /// the document element is set <c>data-mode</c>, which is the palette switch.</para>
    /// </summary>
    [Fact]
    public void NoScriptSetsAThemeTokenOrNamesAFace()
    {
        var token = Rx(@"setProperty\s*\(\s*[""']--", RegexOptions.IgnoreCase);
        var face = Rx("font-family|--font-", RegexOptions.IgnoreCase);
        var colour = Rx(@"#[0-9A-Fa-f]{3,8}\b|\b(rgba?|hsla?|oklch)\s*\(", RegexOptions.IgnoreCase);

        Assert.NotEmpty(Scripts);

        foreach (var (name, text) in Scripts)
        {
            var body = Rx(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline).Replace(text, " ");

            Assert.False(token.IsMatch(body),
                $"{name} sets a custom property directly, which beats every rule in theme.css "
                + "and no stylesheet scan can see. Set data-mode and let the cascade do it.");
            Assert.False(face.IsMatch(body), $"{name} names a typeface.");
            Assert.False(colour.IsMatch(body), $"{name} names a colour.");
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

        foreach (var (_, file) in FontFaces())
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

            // **The operative parts, not the header.** All three assertions above appear in the
            // first nine lines of an OFL file, so `head -9` — 383 bytes, with the permission
            // grant, all five conditions and the warranty disclaimer deleted — satisfied them.
            // What makes this file a licence rather than a title page is what follows.
            Assert.Contains("PERMISSION IS HEREBY GRANTED", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WITHOUT WARRANTY", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CONDITIONS", text, StringComparison.OrdinalIgnoreCase);

            Assert.True(new FileInfo(licence).Length > 3000,
                $"{family}'s licence is {new FileInfo(licence).Length} bytes; the OFL is ~4.5 KB. "
                + "A truncated licence is not the licence.");
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
        var faces = FontFaces();

        Assert.NotEmpty(faces);

        foreach (var (family, file) in faces)
        {
            // The name, which catches pointing one family's rule at the other's file...
            Assert.StartsWith(family, file, StringComparison.OrdinalIgnoreCase);

            // ...and the bytes, which catch the same regression done by overwriting the file
            // instead of by renaming it. A name check correlates a string; nothing in it reads
            // the font. A TrueType `name` table stores its family in UTF-16BE, so the family
            // appears in the file with a null byte between every character — a cheap proxy for
            // parsing the table, and enough to tell Oswald's bytes from Public Sans'.
            var utf16 = string.Concat(family.Select(c => $"\0{c}"));
            var bytes = File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "fonts", file),
                System.Text.Encoding.Latin1);

            Assert.True(bytes.Contains(utf16, StringComparison.Ordinal),
                $"{file} does not name itself {family} internally, so the file serving this "
                + "family is some other font under its name.");
        }
    }

    /// <summary>
    /// Every <c>@font-face</c> in theme.css as (family with spaces stripped, file name). The
    /// family is stripped because a file name cannot carry a space: "Public Sans" ships as
    /// <c>PublicSans-Variable.ttf</c>.
    ///
    /// <para><b>A face whose <c>src</c> this cannot read is a failure, not a face to skip.</b>
    /// An earlier version filtered those out — so writing <c>url("/fonts/PublicSans-Variable.ttf")</c>
    /// with an absolute path dropped the Oswald face from this list *and* from the licence
    /// guard, while <c>&lt;base href="/"&gt;</c> made the path perfectly live. The app served
    /// Public Sans for every heading with both guards green. The filter meant to make this
    /// robust widened the hole it was closing.</para>
    /// </summary>
    private static List<(string Family, string File)> FontFaces()
    {
        var faces = new List<(string, string)>();

        foreach (Match face in Rx(@"@font-face\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)
                     .Matches(WithoutCssComments(ThemeCss)))
        {
            var body = face.Groups["body"].Value;

            var family = Rx(@"font-family:\s*""([^""]+)""").Match(body);
            Assert.True(family.Success, $"An @font-face names no family: {Normalise(body)}");

            var url = Rx(@"url\(""([^""]+)""\)").Match(body);
            Assert.True(url.Success, $"An @font-face has no url(): {Normalise(body)}");

            // Relative to the stylesheet, and only from the one folder that ships fonts. An
            // absolute path or a second location is how a face escapes every check below.
            var reference = Rx(@"^\.\./fonts/(?<file>[^/]+)$").Match(url.Groups[1].Value);
            Assert.True(reference.Success,
                $"An @font-face loads from '{url.Groups[1].Value}'. Fonts are served from "
                + "../fonts/ so that every one of them is checked; a path this test cannot read "
                + "is a face that silently escapes it.");

            faces.Add((
                family.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal),
                reference.Groups["file"].Value));
        }

        return faces;
    }

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

        // A step behind the rank beside it. The band, not the presence of a size:
        // `Contains("font-size:")` is satisfied by 2rem, which would put the gloss in front of
        // the figure. The scale is resolved, so a rung moved to a wrong value fails here too.
        var sizes = FontSizesInRem(declarations);
        Assert.NotEmpty(sizes);
        Assert.All(sizes, rem => Assert.InRange(rem, 0.6, 0.9));

        // **And strictly smaller than the rank it glosses — which is `.stepper .value`, and the
        // first version of this named the wrong element.** It compared against `.trait-table td`,
        // the rank on the printed sheet; but `.rank-word` is emitted by `RankRow.razor` and
        // `.trait-table` by `SheetView.razor`, so **the two never appear on the same surface** and
        // the stated claim was about a pair that cannot be seen together. The comparison passed
        // only by being conservative: 0.82 is tighter than the 1.15 it should have been read
        // against, so the failure it was written for was still reachable from the other side —
        // shrinking `.stepper .value` to `--text-xs` put the gloss exactly level with the figure
        // it sits behind, on the only surface where they co-occur, with the guard green.
        //
        // Before Phase 0 the two were unrelated literals in two rules and this could not be
        // expressed at all. Both are rungs of one resolvable scale now, so it is asserted.
        var rank = RulesOf(ScreenHalfOfAppCss)
            .Where(r => r.Selector is ".stepper .value")
            .SelectMany(r => FontSizesInRem(Normalise(r.Declarations)))
            .ToList();

        Assert.NotEmpty(rank);
        Assert.True(sizes.Max() < rank.Min(),
            $"The rank word is set at {sizes.Max()}rem and the rank it glosses — the stepper's own "
            + $"value, which is the figure it renders beside — at {rank.Min()}rem. The word is a "
            + "gloss on the number and has to read as a step behind it, not level with it.");

        // The two really do render together, which is what makes the comparison meaningful. This
        // is the assertion whose absence let the guard name an element on another page.
        var rankRow = File.ReadAllText(Path.Combine(WebRoot, "Components", "RankRow.razor"));
        Assert.Contains("rank-word", rankRow, StringComparison.Ordinal);
        Assert.Contains("stepper", rankRow, StringComparison.Ordinal);

        // **And it is shown at all.** Everything above is satisfied by an element that is
        // present and hidden — a hidden element still has its class and still has its text, so
        // every rendering test in the bUnit project passes while the rulebook's word simply
        // stops appearing. Checked over every rule that targets the class, not the base one,
        // since a more specific rule further down wins the cascade.
        //
        // **Every spelling of hidden, not one.** Banning `display: none` alone left
        // `visibility: hidden`, `color: transparent` and `font-size: 0` — three ways to the
        // identical result, all green.
        // Matched as whole declarations, not as substrings: `font-size:0` is a prefix of the
        // rule's own `font-size:0.72rem`, and `opacity:0` of `opacity:0.8`. A ban that fires on
        // the thing it is protecting is worse than no ban, because the fix is to weaken it.
        Assert.All(RulesTargeting(".rank-word"), targeting =>
            Assert.All(EverySpellingOfHidden, way =>
                Assert.False(Rx(way).IsMatch(Normalise(targeting)),
                    $"A rule targeting .rank-word hides it with '{way}': {Normalise(targeting)}")));
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
    /// <b>A name on the printed sheet is a word, not a control.</b>
    ///
    /// <para>The explained sheet turns forty names into buttons with a dotted underline and a
    /// help cursor. The tip itself never reaches paper — it is <c>display: none</c> until somebody
    /// hovers, and hovering does not happen on a printer — but the underline and the cursor would
    /// survive, and the printed sheet is what this whole tool produces. It has to come out
    /// identical whether the explanations were on or not.</para>
    ///
    /// <para><b>Asserted against the resolved cascade, not against the print block's own
    /// declarations.</b> <c>@media</c> contributes nothing to specificity, so a screen rule can
    /// outrank a print rule written below it — which is how a dark palette once printed a
    /// full-bleed near-black page. <c>.term-name</c> is one class in both halves, so source order
    /// decides and the print half is later; that is exactly the kind of thing that stops being
    /// true when somebody adds a selector, and <see cref="EffectiveValue"/> is what notices.</para>
    ///
    /// <para>Nothing here is visible to a rendering test: the class sits on the element in both
    /// cases, and the whole substance of this rule is in the stylesheet.</para>
    /// </summary>
    [Theory]
    [InlineData("text-decoration", "none")]
    [InlineData("cursor", "auto")]
    public void ATermOnPaperIsAWordRatherThanAControl(string property, string expected)
    {
        var css = AppCss;

        // The positive control on the pair: on screen it really is marked as carrying something,
        // so the two absences above are a change of state rather than a rule that never existed.
        // Whitespace-normalised, which is how `EffectiveValue` answers — `position:sticky` and
        // `position: sticky` have to be one string for it to be usable at all.
        Assert.Equal("underlinedottedvar(--muted)",
            EffectiveValue(ScreenHalfOfAppCss, ".term-name", "text-decoration"));
        Assert.Equal("help", EffectiveValue(ScreenHalfOfAppCss, ".term-name", "cursor"));

        Assert.Equal(expected, EffectiveValue(css, ".term-name", property));
    }

    /// <summary>
    /// Phrases that could only ever be prose. A player has no use for how the app is built
    /// or what it is built with.
    ///
    /// <para><b>The lower half of this list was added after the owner read four of them on the
    /// screen and said so.</b> The rule they broke is one step past "no jargon": copy answers
    /// what the reader came to do, and anything explaining <em>why the app is built this way</em>
    /// belongs in a <c>@* *@</c> comment. The four were the sign-in page explaining that it will
    /// not say whether an account exists (which also advertises the defence), the replay page
    /// accounting for who would pay for the model, a sample character vouched for by "there is a
    /// test that says so", and "nothing was pre-computed".</para>
    ///
    /// <para><b>This is a denylist and cannot be anything else</b> — there is no pattern that
    /// separates a sentence about a character from a sentence about the program. It grows when
    /// somebody reads the app. What it does do is stop the same phrase coming back.</para>
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
    [InlineData("there is a test")]
    [InlineData("pre-computed")]
    [InlineData("precomputed")]
    [InlineData("repository")]
    [InlineData("source code")]
    [InlineData("unit test")]
    [InlineData("local storage")]
    [InlineData("localstorage")]
    [InlineData("costs anything to run")]
    public void NoPageExplainsItselfToADeveloper(string phrase) =>
        Assert.All(RazorFiles, f =>
            Assert.False(
                VisibleText(File.ReadAllText(f)).Contains(phrase, StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(f)} says \"{phrase}\" to the player. Keep it in a comment."));

    /// <summary>
    /// <b>No page points a reader at a file in this repository.</b>
    ///
    /// <para>Unlike the denylist above this one is structural, and it is the half that would have
    /// caught the worst instance on its own: the replay page told a reader wanting the live
    /// version that "the setup guide is in the project's repository, at docs/MCP-SETUP.md". That
    /// is an accurate sentence, and it is an instruction to go and read a Markdown file in a
    /// source tree — offered to somebody who came to look at a superhero.</para>
    ///
    /// <para>Matched on the extension rather than on a list of names, so a page naming
    /// <c>powers.json</c>, <c>app.css</c> or <c>SKILL.md</c> fails the same way. The rulebook's
    /// own page references — <c>Ch.2 p.29</c> — are a different thing entirely and are required
    /// by the test below.</para>
    /// </summary>
    [Fact]
    public void NoPagePointsAtAFileInThisRepository()
    {
        var path = Rx(@"\b[\w./-]+\.(md|json|txt|cs|razor|js|css|html|csproj|sln|ya?ml)\b",
            RegexOptions.IgnoreCase);

        foreach (var file in RazorFiles)
        {
            var found = path.Match(VisibleText(File.ReadAllText(file)));

            Assert.False(found.Success,
                $"{Path.GetFileName(file)} points the player at '{found.Value}'. A file in this "
                + "repository is not something a reader of this app can be sent to; keep it in a "
                + "@* *@ comment.");
        }
    }

    /// <summary>
    /// The longest run of prose in one paragraph on any screen, in words.
    ///
    /// <para><b>A ceiling, not a target.</b> Most copy in this app is under ten words and should
    /// stay there; this is the point past which a paragraph has stopped answering a question and
    /// started explaining itself.</para>
    /// </summary>
    private const int LongestParagraph = 28;

    /// <summary>
    /// <b>No paragraph on screen is an essay.</b>
    ///
    /// <para>The owner read the app and said so: "so much commentary on EVERY button click and
    /// EVERY step — just cut it to raw process and unclear actions". Before that pass the visible
    /// prose ran to <b>1,943 words</b> across 53 paragraphs of twelve words or more, the worst of
    /// them 88 words above a recording and 81 above a list of findings. Four of the offenders were
    /// three-sentence accounts of what a button would do, printed above the button.</para>
    ///
    /// <para><b>It is a word count because nothing subtler is enforceable.</b> No pattern
    /// separates "turn Headers and footers off" — a real instruction nobody could guess — from a
    /// paragraph restating the heading above it; <see cref="NoPageExplainsItselfToADeveloper"/> is
    /// a denylist for the same reason. What a ceiling does catch is the shape the drift always
    /// takes, which is one more qualifying sentence.</para>
    ///
    /// <para><b>Razor control flow splits a paragraph rather than being counted through it.</b>
    /// The character list's closing sentence is an <c>@if</c> over two eleven-word branches; read
    /// as one run that is twenty-two words of prose no reader ever sees together.</para>
    /// </summary>
    [Fact]
    public void NoParagraphOnScreenIsAnEssay()
    {
        var offenders = new List<string>();
        var longest = 0;

        foreach (var file in RazorFiles)
        {
            foreach (var (words, text) in Paragraphs(File.ReadAllText(file)))
            {
                longest = Math.Max(longest, words);

                if (words > LongestParagraph)
                    offenders.Add($"{Path.GetFileName(file)}: {words} words — \"{text}\"");
            }
        }

        // The positive control, and it is not optional: every assertion above is an absence, and
        // an app whose copy had all been deleted would satisfy every one of them. Something on
        // screen still has to be a sentence.
        Assert.True(longest >= 12,
            $"The longest paragraph in the whole app is {longest} words. That is not this rule "
            + "working — it is the prose having gone, or this scan having stopped reading it.");

        Assert.True(offenders.Count == 0,
            $"Copy answers what the reader came to do; over {LongestParagraph} words it is "
            + "explaining itself. Cut these, or move the reasoning into a @* *@ comment:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// Each paragraph of visible prose on a screen, as a word count and the text itself.
    ///
    /// <para><c>EmptyState</c> counts as one: it is a paragraph in every way a reader can tell,
    /// and two of the longest runs in the app were written as one.</para>
    ///
    /// <para>Split on Razor's own braces, so an <c>@if</c>/<c>else</c> over two short branches is
    /// two short paragraphs rather than one long one — which is what a reader gets.</para>
    /// </summary>
    private static IEnumerable<(int Words, string Text)> Paragraphs(string razor)
    {
        var markup = VisibleMarkup(razor);

        foreach (Match block in Rx(@"<(p|EmptyState)\b[^>]*>(.*?)</\1>", RegexOptions.Singleline)
                     .Matches(markup))
        {
            var text = Rx("<[^>]*>").Replace(block.Groups[2].Value, " ");
            text = Rx(@"@\([^()]*(\([^()]*\))?[^()]*\)").Replace(text, " ");
            text = Rx(@"@\w+(\.\w+)*(\([^()]*\))?").Replace(text, " ");

            foreach (var branch in text.Split('{', '}'))
            {
                var run = Rx(@"\s+").Replace(branch, " ").Trim();
                var words = run.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (words > 0) yield return (words, run);
            }
        }
    }

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

        // ChosenRow's own content, split out from the <li> so a finding can sit under it and
        // still be one list item — see RowFinding below.
        ("chosen-row", "ChosenRow.razor"),

        ("options", "OptionList.razor"),
        ("option", "OptionRow.razor"),

        // Validation on the row that broke it. Two classes because a row can carry more than
        // one finding — the list itself, and each finding inside it.
        ("row-findings", "RowFinding.razor"),
        ("finding", "RowFinding.razor"),

        // What a list says when it holds nothing. Owned for the usual reason and one of its
        // own: six copies of this treatment is six chances for an empty state to go back to
        // being a full stop, and the guard that refuses that needs one element to find.
        ("empty-state", "EmptyState.razor"),

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
        ("replay-figures", "ReplayVerdict.razor"),

        // How a passage of the book is set. Two pages hand-wrote this — the Power editor's
        // disclosure and the rules reference — and the reference's own comment already claimed
        // "one idiom for the book's own words, not a second one for the same text on another
        // page", which is a promise a comment cannot keep. It is `BookText` now, and the stat
        // line and option blocks it draws are owned with it.
        ("book-text", "BookText.razor"),
        ("book-stat", "BookText.razor"),
        ("book-options", "BookText.razor")
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
        foreach (var file in RazorFiles.Where(f => Path.GetFileName(f) != owner))
        {
            var offending = ClassAttributeValues(File.ReadAllText(file))
                .FirstOrDefault(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(cssClass, StringComparer.Ordinal));

            Assert.True(offending is null,
                $"{Path.GetFileName(file)} writes class=\"{offending}\" itself, which includes "
                + $"\"{cssClass}\". Use <{Path.GetFileNameWithoutExtension(owner)}>.");
        }
    }

    /// <summary>Every <c>class="…"</c> attribute value in a razor file's source, unsplit.</summary>
    private static IEnumerable<string> ClassAttributeValues(string source) =>
        Rx("""class\s*=\s*(?<q>["'])(?<v>[^"']*)\k<q>""")
            .Matches(source)
            .Select(m => m.Groups["v"].Value);

    /// <summary>Every whitespace-split token across every <c>class="…"</c> attribute value.</summary>
    private static IEnumerable<string> ClassAttributeTokens(string source) =>
        ClassAttributeValues(source).SelectMany(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The seven owned classes that are not written straight into a <c>class="…"</c>
    /// attribute at all, and the member whose C# body builds them instead — <c>ClassName</c>
    /// for five components that pick between a bare token and an interpolated one
    /// (<c>string.IsNullOrEmpty(Class) ? "field" : $"field {Class}"</c>), <c>RowClass</c> for
    /// <c>OptionRow</c>'s concatenation, and <c>Lines</c> for the one place a class reaches
    /// the page through <c>RenderTreeBuilder.AddAttribute</c> inside a <c>RenderFragment</c>
    /// delegate rather than through markup. None of these seven member bodies appear as
    /// <c>class="…"</c> in source text, so <see cref="ClassAttributeTokens"/> cannot see them
    /// and must not be asked to guess at a substring instead.
    /// </summary>
    private static readonly Dictionary<string, string> ClassBuiltInCode = new(StringComparer.Ordinal)
    {
        ["panel"] = "ClassName",
        ["field"] = "ClassName",
        ["sheet-section"] = "ClassName",
        ["stat-blocks"] = "ClassName",
        ["options"] = "ClassName",
        ["option"] = "RowClass",
        ["rule-line"] = "Lines",
    };

    /// <summary>
    /// The other half, without which the exemption above is decorative: an owner that stops
    /// writing its class would satisfy the test by writing nothing at all.
    ///
    /// <para><b>A bare <c>Contains($"\"{cssClass}", source)</c> is exactly that "nothing at
    /// all" in disguise, and it took every one of the fifteen cases with it.</b> Renaming
    /// <c>panel</c> to <c>panelish</c> in <c>Panel.razor</c> — so the component never writes
    /// the real class again — still leaves the substring <c>"panel</c> in the source, because
    /// <c>"panelish"</c> starts with it. Watched to fire: with the rename in place and this
    /// check unchanged, all fifteen theory cases passed.</para>
    ///
    /// <para>The fix reuses the real tokenizer twenty lines up rather than writing a third
    /// spelling of it. Eight of the fifteen classes are written straight into a
    /// <c>class="…"</c> attribute and <see cref="ClassAttributeTokens"/> finds them exactly
    /// the way <see cref="OnlyOneComponentWritesEachRepeatedClass"/> does. The other seven are
    /// read from the C# member named in <see cref="ClassBuiltInCode"/>, tokenised the same
    /// way — split on whitespace, exact membership, never a prefix — so a rename to
    /// <c>panelish</c> there fails for the identical reason it fails on the markup side: it is
    /// a different token, not a superstring match.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Owned))]
    public void EachOwnerActuallyWritesTheClassItOwns(string cssClass, string owner)
    {
        var source = File.ReadAllText(Path.Combine(WebRoot, "Components", owner));

        var tokens = ClassBuiltInCode.TryGetValue(cssClass, out var member)
            ? ClassLiteralTokens(MemberBody(source, member))
            : ClassAttributeTokens(source);

        Assert.Contains(cssClass, tokens);
    }

    /// <summary>
    /// Every whitespace-split token inside a C# string literal in <paramref name="text"/> —
    /// the literal segments only. An interpolation hole is replaced with a space rather than
    /// read as text, so <c>$"field {Class}"</c> still yields the single token <c>field</c>
    /// rather than a token containing a brace.
    /// </summary>
    private static IEnumerable<string> ClassLiteralTokens(string text) =>
        Rx(@"""([^""]*)""")
            .Matches(text)
            .SelectMany(m => Rx(@"\{[^{}]*\}").Replace(m.Groups[1].Value, " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The source of one expression-bodied C# member — from its declaration through the
    /// expression it returns — found by locating <paramref name="member"/> immediately
    /// followed by <c>=&gt;</c> (so the markup reference <c>@ClassName</c>, which appears
    /// earlier in every one of these files, is never mistaken for the declaration) and then
    /// walking forward tracking <c>(</c>/<c>)</c> and <c>{</c>/<c>}</c> depth, stopping at the
    /// first <c>;</c> seen at depth zero.
    ///
    /// <para><b>String literals are skipped whole, interpolation holes included</b>, so a
    /// brace inside <c>$"field {Class}"</c> never throws the depth count off and a semicolon
    /// can never appear inside one to begin with — this file has none that do, but the scan
    /// does not assume it. Skipping literals this way is also what keeps the result narrow: an
    /// earlier version that grabbed everything up to the next sibling declaration line pulled
    /// in the next member's XML doc comment when the following line did not itself start with
    /// an access modifier, and a broader version that searched the whole file for
    /// <c>"…"</c> literals picked up unrelated quoted text — <c>OptionRow.razor</c> writes
    /// <c>role="@(Navigable ? "option" : null)"</c>, an ARIA role that happens to spell the
    /// same word as the CSS class, which would have kept reporting <c>RowClass</c> as writing
    /// <c>option</c> even after that property stopped.</para>
    ///
    /// <para>Not a C# parser — a lexer scoped to what the six members in
    /// <see cref="ClassBuiltInCode"/> actually are: an expression-bodied property, or one
    /// <c>RenderFragment</c> lambda with no member declarations nested inside it.</para>
    /// </summary>
    private static string MemberBody(string source, string member)
    {
        var declaration = Rx($@"(?<![\w.]){Regex.Escape(member)}\b\s*=>").Match(source);
        Assert.True(declaration.Success, $"No expression-bodied member named {member}.");

        var depth = 0;
        var i = declaration.Index;

        for (; i < source.Length; i++)
        {
            var c = source[i];

            if (c == '"')
            {
                i++;
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '{')
                    {
                        var braceDepth = 1;
                        i++;
                        while (i < source.Length && braceDepth > 0)
                        {
                            if (source[i] == '{') braceDepth++;
                            else if (source[i] == '}') braceDepth--;
                            i++;
                        }
                        continue;
                    }

                    i++;
                }

                continue;
            }

            if (c is '(' or '{') depth++;
            else if (c is ')' or '}') depth--;
            else if (c == ';' && depth == 0) { i++; break; }
        }

        return source[declaration.Index..Math.Min(i, source.Length)];
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
        // **Exactly one page box, and every one of them read.** This used `Match`, i.e. the first
        // only — and the raw-length guard had meanwhile started stripping *every* `@page` block
        // out of the region it scans, citing this test as the compensating check. It was not one:
        // a second `@page { size: A5 landscape; margin: 0 }` placed after the A4 block wins the
        // cascade, prints the sheet A5 landscape with no margins, and was seen by nothing at all.
        //
        // `ThereIsExactlyOnePrintBlockInEachStylesheet` exists in this file for precisely this
        // failure one at-rule over; the page box had no equivalent. A pseudo-page — `@page :first`
        // — counts as another one here rather than being tolerated, because it can restate the
        // size and the margin just as completely.
        var pages = Rx(@"@page\b([^{]*)\{([^}]*)\}").Matches(WithoutCssComments(AppCss));

        Assert.True(pages.Count > 0, "app.css has no @page rule, so print uses whatever the browser guesses.");
        Assert.True(pages.Count == 1,
            $"app.css has {pages.Count} @page rules ({string.Join(" / ", pages.Select(p => $"@page{p.Groups[1].Value.Trim()}"))}). "
            + "A later one overrides the size and the margins, and the length guard strips them all "
            + "from its own scan — so only the first is checked anywhere. Keep one.");

        var body = pages[0].Groups[2].Value;
        Assert.Contains("size:A4", Normalise(body), StringComparison.Ordinal);
        Assert.Matches(@"margin:\s*[\d.]+mm", body);
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
            r => Assert.DoesNotMatch(Rx("background(-color)?:(none|transparent|#fff|white)"), r));
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
    ///
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

        Assert.NotEmpty(FontSizesInRem(declarations));

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
            Assert.All(FontSizesInRem(rule), rem => Assert.InRange(rem, 0.6, 0.95));

            // Never emphasised. A cost is bookkeeping; bolding it puts it in front of the rank,
            // whatever the size and the ink are doing.
            Assert.All(Values(rule, @"font-weight:(\d+)"),
                v => Assert.InRange(int.Parse(v, CultureInfo.InvariantCulture), 100, 500));

            // And never brought back into the body ink or out of small caps, which are the
            // other two halves of the separation.
            Assert.All(Values(rule, "(?<!-)color:([^;]+)"), v => Assert.Equal("var(--muted)", v));
            Assert.All(Values(rule, "text-transform:([^;]+)"), v => Assert.Equal("uppercase", v));
        });
    }

    /// <summary>
    /// The Hero Point figure on the sticky budget strip is the app's largest numeral, not a
    /// caption beside one.
    ///
    /// <para><b>Why this is a stylesheet test and not a bUnit one.</b> Emptying
    /// <c>.budget-figure strong</c>'s <c>font-size</c> back to a body-adjacent size leaves the
    /// same markup, the same class, and the same digits on screen — every render test that
    /// asserts the figure is present would still pass. The size is entirely typographic, the
    /// same reason <see cref="AHeroPointCostIsSetApartFromTheNumbersAPlayerRolls"/> reads the
    /// parsed rule for <c>.hp</c> rather than rendered markup.</para>
    ///
    /// <para><b>Anchored to the scale's own top rung, not a value chosen here.</b>
    /// <c>DerivedStatBlocks</c> already sets Edge, Health, Resolve and the Hero Point total at
    /// <c>--text-3xl</c> on the derived-stats step and on the sheet; asserting the same token
    /// on the strip is what makes the number a player watches continuously the same size as the
    /// numbers they see occasionally, rather than a full step behind them.</para>
    /// </summary>
    [Fact]
    public void TheWatchedFigureIsTheAppsLargestNumeral()
    {
        var size = EffectiveValue(ScreenHalfOfAppCss, ".budget-figure strong", "font-size", exact: true);
        Assert.Equal("var(--text-3xl)", size);

        // Coloured as a heading, not left in body ink — the figure this app is actually about
        // reads as the point of the screen. --heading on --panel already holds its own floor
        // in EveryScreenPairInUseHoldsItsContrastFloor, so this introduces no unmeasured pair.
        var color = EffectiveValue(ScreenHalfOfAppCss, ".budget-figure strong", "color", exact: true);
        Assert.Equal("var(--heading)", color);
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
        var screen = FontSizesInRem(declarations);
        Assert.NotEmpty(screen);
        Assert.All(screen, rem => Assert.InRange(rem, 0.7, 1.0));

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
    /// <b>The browser does not fetch the recorded conversations at startup.</b>
    ///
    /// <para>They used to be fetched before the first render, exactly like the rules — four
    /// files every visitor paid for, almost none of whom could ever reach the pages that play
    /// them back, since those are behind an account. The server now refuses
    /// <c>api/transcripts</c> to anybody not signed in, which is what makes fetching worth
    /// deferring: a visitor who never opens a recording must never ask for one.
    /// <c>ReplayLoader</c> is where that fetch happens instead, on the first call a component
    /// makes to it — this file must not build the library, or even mention what it is loading,
    /// itself.</para>
    ///
    /// <para>This is the source half; the behavioural half —that opening a recording really
    /// does ask, and that nothing else does— is
    /// <c>ReplayRenderTests.NothingFetchesTheRecordingsUntilOneIsOpened</c> in the bUnit
    /// project.</para>
    /// </summary>
    [Fact]
    public void TheBrowserDoesNotFetchTheReplayLibraryAtStartup()
    {
        var program = File.ReadAllText(Path.Combine(WebRoot, "Program.cs"));

        Assert.DoesNotContain("ReplayLibrary", program, StringComparison.Ordinal);
        Assert.DoesNotContain("transcripts", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// The address the app asks for is the one gated route the server answers this from.
    ///
    /// <para>The recordings used to be ordinary files under a folder the csproj staged into
    /// <c>wwwroot</c>, and this test compared the address against that folder. They are bundled
    /// into the worker now, the way the rulebook corpus is, so there is no folder to compare
    /// against — <c>AccountsContractTests.EveryAddressTheBrowserAsksForIsOneTheServerAnswers</c>
    /// is what holds the two ends of the actual route together. This just pins the address
    /// itself, so a rename here does not slip past unnoticed.</para>
    /// </summary>
    [Fact]
    public void TheRecordingsAreAskedForFromTheGatedRoute()
    {
        var served = Rx(@"ServedFrom\s*=\s*""([^""]+)""")
            .Match(File.ReadAllText(Path.Combine(WebRoot, "Services", "ReplayLibrary.cs")));

        Assert.True(served.Success, "ReplayLibrary does not say where the recordings are served from.");
        Assert.Equal("api/transcripts", served.Groups[1].Value);
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

    /// <summary>Every spelling of "start moving something" this app can use.</summary>
    private static readonly string[] AnimationCalls = ["startViewTransition(", ".animate("];

    /// <summary>
    /// <b>Every scripted animation asks whether movement is wanted — each one, not the file.</b>
    ///
    /// <para>The duration tokens collapse to <c>0.01ms</c> under <c>prefers-reduced-motion</c>,
    /// which covers every CSS transition in the app and nothing a script does:
    /// <c>startViewTransition()</c> and <c>element.animate()</c> read no custom property. <b>A
    /// token cannot reach a script</b>, and the stylesheet looking correct is why this is easy to
    /// miss.</para>
    ///
    /// <para><b>This was a whole-file grep wearing a per-entry-point docstring, and two reviewers
    /// demonstrated it independently.</b> The helper took the <em>first</em> <c>animate(</c> —
    /// which is in the file's header comment, above any brace — so it fell back to returning the
    /// whole file, and "the block calling this gates on <c>still()</c>" became "the file mentions
    /// <c>still()</c> somewhere". A new entry point added below, gated by nothing, was green here
    /// <em>and</em> in the browser harness, which drives only the entry points it knows about.
    /// Comments are stripped first now, and every occurrence is checked rather than the first.</para>
    /// </summary>
    [Fact]
    public void EveryScriptedAnimationAsksWhetherMovementIsWanted()
    {
        var motion = Scripts.SingleOrDefault(s => s.Name == "motion.js");

        Assert.False(motion.Text is null,
            "motion.js has gone. If scripted motion moved to another file, point this test at it: "
            + "the reduced-motion setting is unreachable from the stylesheet.");

        Assert.Contains("prefers-reduced-motion", motion.Text, StringComparison.Ordinal);
        Assert.Contains("matchMedia", motion.Text, StringComparison.Ordinal);

        // **Every script the app ships, not just motion.js.** `download.js` is already a second
        // file in index.html and was unchecked; the next animation need not land in the file this
        // test was named after.
        var scripts = Scripts.ToList();

        Assert.Contains(scripts, s => s.Name == "motion.js");

        var gated = 0;

        foreach (var (name, text) in scripts)
        {
            var code = WithoutJsComments(text);

            foreach (var needle in AnimationCalls)
            {
                foreach (var at in Occurrences(code, needle))
                {
                    gated++;

                    var body = EnclosingBlock(code, at);

                    Assert.True(
                        body.Contains("still()", StringComparison.Ordinal),
                        $"{name}: a call to {needle} is not gated on still(), so a reduced-motion "
                        + "user gets the full animation. The stylesheet's 0.01ms tokens do not "
                        + "reach a script.");
                }
            }
        }

        Assert.True(
            gated >= 3,
            $"Found {gated} animation calls across the app's scripts; expected at least the three "
            + "entry points. If they moved, point this test at them rather than letting it pass "
            + "on an empty list.");
    }

    /// <summary>
    /// <b>A view transition that is never released leaves the page frozen.</b>
    ///
    /// <para>While one is open the live DOM is hidden behind a snapshot, so a navigation that
    /// throws, or a handler disposed mid-flight, strands the user looking at a still image of
    /// the app with no way back. That is the worst failure available in this file, and it is
    /// invisible in every ordinary run because the release normally arrives.</para>
    ///
    /// <para>So the script must release on a timer as well as on the render. This asserts the
    /// timer exists <em>and</em> that it is armed after the transition is opened, since a
    /// <c>setTimeout</c> that an early return skips is not a safety net.</para>
    /// </summary>
    [Fact]
    public void AnOpenViewTransitionIsAlwaysReleased()
    {
        var motion = Scripts.Single(s => s.Name == "motion.js").Text;

        Assert.Contains("setTimeout", motion, StringComparison.Ordinal);
        Assert.Contains("clearTimeout", motion, StringComparison.Ordinal);

        var timer = motion.IndexOf("setTimeout", StringComparison.Ordinal);
        var opens = motion.IndexOf("startViewTransition", StringComparison.Ordinal);

        Assert.True(opens >= 0, "Nothing opens a transition; this test has lost its subject.");
        Assert.True(
            timer > opens,
            "The safety timer is armed before the transition is opened, so an early return skips "
            + "it and leaves the snapshot up for ever.");
    }

    /// <summary>
    /// <b>The transition is opened from the <c>LocationChanging</c> handler itself.</b>
    ///
    /// <para><c>LocationChanged</c> is the hook already in this layout and the one anybody would
    /// reach for, and it fires <em>after</em> the navigation, when the old page is gone and there
    /// is nothing left to snapshot. The failure is silent — no error, just no animation, which is
    /// indistinguishable from a browser that does not support it.</para>
    ///
    /// <para><b>The first version of this test asserted four substrings over the whole file and
    /// checked none of that.</b> A reviewer moved the snapshot into <c>Moved</c> — the
    /// <c>LocationChanged</c> handler — left <c>OpenTransition</c> registered and empty, and every
    /// one of the four still matched: the whole suite and all six browser harnesses stayed green.
    /// So this resolves the registered handler by name and asks what <em>its</em> body does.</para>
    ///
    /// <para>The behavioural half is <c>TheShellOpensATransitionOnNavigation</c> in the bUnit
    /// project, which drives a real navigation and reads the interop back. Both are wanted: this
    /// one names the mechanism, that one proves it fires.</para>
    /// </summary>
    [Fact]
    public void TheTransitionIsOpenedFromTheLocationChangingHandler()
    {
        var layout = File.ReadAllText(Path.Combine(WebRoot, "Layout", "MainLayout.razor"));

        // Which method is registered as the changing handler?
        var registered = Rx(@"RegisterLocationChangingHandler\(\s*(\w+)\s*\)").Match(layout);
        Assert.True(
            registered.Success,
            "Nothing registers a LocationChanging handler, so the snapshot cannot be taken while "
            + "the old page is still on screen.");

        var handler = registered.Groups[1].Value;

        // ...and what does that method's body do? Anchored on the declaration, so moving the call
        // into LocationChanged's handler no longer satisfies it.
        // Comments blanked first: the fix-audit left a comment mentioning Motion.Begin() inside
        // the handler while moving the real call into LocationChanged, and this passed.
        var body = MethodBodyOf(WithoutJsComments(layout), handler);

        Assert.True(
            body.Contains("Motion.Begin()", StringComparison.Ordinal),
            $"'{handler}' is registered as the LocationChanging handler but does not open the "
            + "transition. If the snapshot is taken from LocationChanged instead, it captures the "
            + "page that has already gone.");


        // **The release is not in the changing handler.** `Begin(); End();` there passes every
        // other check — a transition is opened, from the right hook, and released — and animates
        // nothing at all, because the second snapshot is taken before Blazor has rendered the new
        // page. The release belongs to the render.
        Assert.False(
            body.Contains("Motion.End()", StringComparison.Ordinal),
            "The transition is released inside the LocationChanging handler, so the second "
            + "snapshot is taken before the new page has rendered and nothing ever animates. "
            + "Release it from OnAfterRenderAsync.");

        Assert.True(
            MethodBodyOf(WithoutJsComments(layout), "OnAfterRenderAsync")
                .Contains("Motion.End()", StringComparison.Ordinal),
            "Nothing releases the transition after a render, so the app is left behind a "
            + "snapshot of itself until the failsafe fires.");

        // The registration is disposed. bUnit creates a layout per test, and an undisposed handler
        // outlives the component that registered it.
        Assert.Contains("_leaving?.Dispose()", layout, StringComparison.Ordinal);
    }

    /// <summary>
    /// The body of a C# method declared in <paramref name="source"/>, found by name and matched by
    /// brace depth from its opening brace.
    /// </summary>
    private static string MethodBodyOf(string source, string name)
    {
        var declared = Rx($@"\b{Regex.Escape(name)}\s*\(")
            .Matches(source)
            .Select(m => m.Index)
            .ToList();

        foreach (var at in declared)
        {
            var open = source.IndexOf('{', at);
            if (open < 0) continue;

            // A call site — `RegisterLocationChangingHandler(OpenTransition)` — has no body of its
            // own, so require the brace to be close enough to be this declaration's.
            if (source[at..open].Contains(';', StringComparison.Ordinal)) continue;

            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0)
                {
                    var body = source[(open + 1)..i];
                    if (body.Trim().Length > 0) return body;
                    break;
                }
            }
        }

        Assert.Fail($"Could not find a method body for '{name}'.");
        return string.Empty;
    }


    /// <summary>
    /// <b>Nothing calls the app's own scripts except through <c>Motion</c>.</b>
    ///
    /// <para><c>Motion</c> exists because a missing or broken <c>motion.js</c> would otherwise
    /// throw out of a <c>LocationChanging</c> handler and out of every <c>ChosenList</c> render,
    /// taking navigation down with a 404. But guarding the two call sites is not the same
    /// property as guarding the app: the fix-audit put <c>Js.InvokeVoidAsync("ppLand", …)</c>
    /// straight back into <c>ChosenList</c> and every test stayed green.</para>
    ///
    /// <para>So this asserts the shape rather than the instances. <c>ppSetMode</c>,
    /// <c>ppStore</c> and <c>ppDownload</c> are exempt by name: they are reached only by an
    /// explicit user action — switching palette, saving, downloading — where a failure is
    /// visible and recoverable, not by a render or a navigation. Adding a fourth needs a reason
    /// of the same kind.</para>
    /// </summary>
    [Fact]
    public void TheAppsOwnScriptsAreCalledOnlyThroughMotion()
    {
        // Reached by a user action, not by rendering or navigating.
        // ppSetMode has left this list. It used to be a click and nothing else; the palette now
        // follows the character, so the layout pushes it from OnAfterRenderAsync on every page,
        // and an unguarded call there throws out of every render of the shell. It goes through
        // Theme now. **The list shrinking is the point** — an entry here is a claim that the call
        // is only ever reached by a user action, and this one stopped being true.
        string[] byHand = ["ppStore", "ppDownload"];

        var offenders = new List<string>();

        foreach (var file in RazorFiles)
        {
            var text = File.ReadAllText(file);

            foreach (Match call in Rx(@"Invoke(Void)?Async(<[^>]+>)?\(\s*""(pp[A-Za-z.]+)""").Matches(text))
            {
                var target = call.Groups[3].Value;
                var root = target.Split('.')[0];

                if (byHand.Contains(root, StringComparer.Ordinal)) continue;

                offenders.Add($"{Path.GetFileName(file)} calls {target} directly");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These reach a script without going through Motion, so a missing motion.js throws "
            + "out of a render or a navigation instead of being swallowed:\n  "
            + string.Join("\n  ", offenders));
    }
    /// <summary>The three chrome bands, each appearing exactly once in the component tree.</summary>
    private static readonly string[] SingletonSelectors = [".banner", ".steps", ".budget"];

    /// <summary>
    /// <b>No two <em>elements</em> can share a <c>view-transition-name</c>.</b>
    ///
    /// <para>A duplicate name is a spec error and the browser abandons the whole transition, so
    /// the symptom is that nothing animates anywhere — which reads as "not supported" rather than
    /// as a mistake in this stylesheet.</para>
    ///
    /// <para><b>Checking that the declared names differ is not that check</b>, and both reviewers
    /// broke it the same way: one name on a selector matching many elements is unique as a string
    /// and duplicated on the page. <c>.panel { view-transition-name: panel }</c> passed here, and
    /// driven against real Chrome produced <c>InvalidStateError: Transition was aborted because of
    /// invalid state</c> — every transition in the app dead. So the <em>selector</em> is what is
    /// checked, against the three known to match exactly one element.</para>
    /// </summary>
    [Fact]
    public void EveryViewTransitionNameIsOnASingletonSelector()
    {
        // A local would fail CA1861 under warnings-as-errors; see SingletonSelectors.

        // **Both stylesheets, and case-insensitively.** The fix-audit broke the first version two
        // ways without touching this file: a rule put in `theme.css`, which was never read, and
        // the same rule spelled `VIEW-TRANSITION-NAME`, which CSS treats as identical and a
        // case-sensitive regex does not. Both reached driven Chrome as
        // `InvalidStateError: Transition was aborted because of invalid state`.
        var rules = Rx(
                @"([^{}]+)\{([^{}]*?view-transition-name:\s*([A-Za-z-][\w-]*)[^{}]*)\}",
                RegexOptions.IgnoreCase)
            .Matches(WithoutCssComments(AppCss) + "\n" + WithoutCssComments(ThemeCss))
            .Select(m => (Selector: Normalise(m.Groups[1].Value), Name: m.Groups[3].Value))
            .ToList();

        Assert.True(
            rules.Count >= 3,
            $"Found {rules.Count} view-transition-name rules, expected the three chrome bands. If "
            + "they moved, point this test at them rather than letting it pass on an empty list.");

        foreach (var (selector, name) in rules)
        {
            Assert.True(
                SingletonSelectors.Contains(selector, StringComparer.Ordinal),
                $"'{selector}' carries view-transition-name: {name} and is not one of the selectors "
                + $"known to match exactly one element ({string.Join(", ", SingletonSelectors)}). A name on "
                + "a selector matching several elements is a spec error, and the browser responds "
                + "by abandoning every transition in the app.");
        }

        var duplicated = rules.GroupBy(r => r.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicated.Count == 0, $"Shared name: {string.Join(", ", duplicated)}.");
    }

    /// <summary>Every index at which <paramref name="needle"/> occurs.</summary>
    private static IEnumerable<int> Occurrences(string text, string needle)
    {
        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            yield return at;
        }
    }

    /// <summary>
    /// JavaScript with its comments blanked out, so a match inside prose cannot anchor anything.
    ///
    /// <para><b>This is what two reviewers broke independently.</b> The first <c>animate(</c> and
    /// the first <c>startViewTransition</c> in <c>motion.js</c> are both in the file's header
    /// comment, above any brace — so a helper that took the first occurrence and searched upwards
    /// for a <c>{</c> found none and fell back to the whole file, turning a per-entry-point check
    /// into a whole-file grep.</para>
    ///
    /// <para>Blanked rather than removed, so every index still lines up with the original.</para>
    /// </summary>
    private static string WithoutJsComments(string js)
    {
        var outp = new System.Text.StringBuilder(js.Length);
        var i = 0;

        while (i < js.Length)
        {
            if (i + 1 < js.Length && js[i] == '/' && js[i + 1] == '/')
            {
                while (i < js.Length && js[i] != '\n') { outp.Append(' '); i++; }
                continue;
            }

            if (i + 1 < js.Length && js[i] == '/' && js[i + 1] == '*')
            {
                while (i + 1 < js.Length && !(js[i] == '*' && js[i + 1] == '/'))
                {
                    outp.Append(js[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                for (var k = 0; k < 2 && i < js.Length; k++, i++) outp.Append(' ');
                continue;
            }

            outp.Append(js[i]);
            i++;
        }

        return outp.ToString();
    }

    /// <summary>
    /// The smallest braced block containing <paramref name="at"/>, or <b>the empty string</b> when
    /// there is none.
    ///
    /// <para><b>Returning the whole file as a fallback is what made the old helper a whole-file
    /// grep</b>, and the fix-audit showed the fallback surviving the first repair: a call at
    /// module top level has no enclosing block, so the file came back — and the file contains
    /// <c>still()</c>. An ungated animation in a module-level helper passed. Empty is the honest
    /// answer, and it fails the caller's assertion, which is the right outcome: an animation
    /// nothing encloses is an animation nothing gates.</para>
    /// </summary>
    private static string EnclosingBlock(string text, int at)
    {
        var open = text.LastIndexOf('{', at);

        while (open >= 0)
        {
            var depth = 0;

            for (var i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0)
                {
                    // A block that closes before the call is a sibling, not an ancestor.
                    if (i > at) return text[(open + 1)..i];
                    break;
                }
            }

            open = text.LastIndexOf('{', open - 1);
        }

        return string.Empty;
    }

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
    /// <b>No component explains anything with a <c>title</c> attribute.</b>
    ///
    /// <para>This is the entire reason <c>Tooltip</c> is a component rather than an attribute.
    /// <c>title</c> never appears on a touch screen, is unreliable for keyboard users, cannot be
    /// styled, cannot be dismissed, and is announced inconsistently by screen readers — and none of
    /// that is visible to a compiler, to a rendering test, or to somebody reading the markup and
    /// finding it perfectly reasonable. It is the single easiest way to undo this work, because it
    /// is the obvious thing to write.</para>
    ///
    /// <para><c>&lt;title&gt;</c> the element is a different thing and is not matched: the scan
    /// requires the attribute form, an <c>=</c> after the name.</para>
    /// </summary>
    [Fact]
    public void NoComponentExplainsAnythingWithATitleAttribute()
    {
        var attribute = Rx(@"(?<![\w-])title\s*=");

        var offenders = RazorFiles
            .Where(f => attribute.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A title attribute is not a tooltip — no touch, unreliable by keyboard, unstyleable, "
            + "not dismissable. Use the Tooltip component:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <b>A closed tip takes no layout box at all — <c>display: none</c>, not
    /// <c>visibility: hidden</c>.</b>
    ///
    /// <para><b>This assertion is the exact inverse of the one it replaces, and CI is why.</b> The
    /// first version hid the tip with <c>visibility</c> so that the accessible description could
    /// be read off it while closed, and this test required that it not be <c>display: none</c>.
    /// A hidden element keeps its layout box: an absolutely-positioned tip up to 22rem wide then
    /// contributed real horizontal overflow at 375px, while closed, on every page carrying one.
    /// The narrow harness caught it in CI; the whole suite passed locally, because no rendering
    /// test here can see a box leaving the viewport.</para>
    ///
    /// <para>The description now lives on a separate <c>sr-only</c> element — a clipped 1px box
    /// that contributes no overflow — so the visible copy is free to leave layout entirely. Both
    /// halves are asserted, because keeping only this one would allow the description to be
    /// deleted and the tooltip to become decoration.</para>
    /// </summary>
    [Fact]
    public void AClosedTipTakesNoLayoutBox()
    {
        Assert.Equal("none", EffectiveValue(ScreenHalfOfAppCss, ".tip", "display"));
        Assert.Equal("block", EffectiveValue(ScreenHalfOfAppCss, ".tip.shown", "display"));

        // visibility:hidden would put the overflow straight back. Named rather than left implied,
        // since it is the spelling somebody reaches for when restoring a fade.
        Assert.NotEqual("hidden", EffectiveValue(ScreenHalfOfAppCss, ".tip", "visibility"));

        // And the described element is not the one that just left layout. The component points
        // aria-describedby at an sr-only copy; a version that pointed it back at `.tip` would be
        // naming a `display: none` element, which is exactly the description-loss this shape
        // exists to avoid.
        var tooltip = File.ReadAllText(Path.Combine(WebRoot, "Components", "Tooltip.razor"));
        Assert.Matches(Rx(@"id=""@Id""\s+class=""sr-only"""), tooltip);
    }

    /// <summary>
    /// <b>The skip link is off-screen until it is focused, and back off-screen the moment focus
    /// leaves it.</b>
    ///
    /// <para>The whole substance of this is in the stylesheet, exactly as
    /// <see cref="AClosedTipTakesNoLayoutBox"/> is: a bUnit render sees the anchor and its href
    /// either way, so a rule that stopped moving it on focus — the one thing that makes it
    /// reachable at all rather than a link nobody can ever see — would leave every rendered
    /// assertion about <c>MainLayout</c> passing.</para>
    ///
    /// <para><c>transform</c>, not <c>display</c> or <c>visibility</c>: both of those would also
    /// have to be undone on <c>:focus</c>, which is two properties agreeing rather than one, and
    /// this file has already found that shape wrong in both directions on other elements.</para>
    /// </summary>
    [Fact]
    public void TheSkipLinkIsOffscreenUntilFocused()
    {
        var atRest = EffectiveValue(ScreenHalfOfAppCss, ".skip-link", "transform", exact: true);
        Assert.NotNull(atRest);
        Assert.NotEqual("none", atRest);
        Assert.NotEqual("translateY(0)", atRest);

        Assert.Equal("translateY(0)",
            EffectiveValue(ScreenHalfOfAppCss, ".skip-link:focus", "transform", exact: true));
    }

    /// <summary>
    /// <b>A row's description appears on hover <i>and</i> on focus, and takes no layout box when
    /// it does not.</b>
    ///
    /// <para>The whole substance of this is in the stylesheet. Emptying the rule leaves the class
    /// on the element, the sentence in the document and every one of the nine rendered assertions
    /// in <c>RowDescriptionTests</c> passing — with the descriptions invisible to everybody. That
    /// is the failure shape this file exists for.</para>
    ///
    /// <para><b>Focus as well as hover, asserted separately.</b> A hover-only rule is the obvious
    /// thing to write and makes the feature a mouse feature, which is the objection to a
    /// <c>title</c> attribute restated in CSS.</para>
    ///
    /// <para><b>And <c>display</c> rather than <c>visibility</c>, for the reason recorded on
    /// <see cref="AClosedTipTakesNoLayoutBox"/>:</b> a hidden element keeps its box, and an
    /// absolutely-positioned tip that keeps its box put real horizontal overflow into CI once
    /// already.</para>
    /// </summary>
    [Fact]
    public void ARowsDescriptionOpensOnHoverAndOnFocusAndIsOtherwiseAbsent()
    {
        // `exact`, because suffix matching would let `.option:hover .row-tip` answer for the bare
        // selector — and that rule says `block`, so the closed state would report itself open.
        Assert.Equal("none",
            EffectiveValue(ScreenHalfOfAppCss, ".row-tip", "display", exact: true));

        Assert.Equal("block",
            EffectiveValue(ScreenHalfOfAppCss, ".option:hover .row-tip", "display", exact: true));
        Assert.Equal("block",
            EffectiveValue(ScreenHalfOfAppCss, ".option:focus-visible .row-tip", "display", exact: true));

        // The Trait rows, where the trigger is the name rather than the whole row.
        Assert.Equal("block",
            EffectiveValue(ScreenHalfOfAppCss, ".rank-name:hover .row-tip", "display", exact: true));
        Assert.Equal("block",
            EffectiveValue(ScreenHalfOfAppCss, ".trait-term:focus-visible ~ .row-tip", "display", exact: true));

        // visibility:hidden would put the overflow straight back. Named rather than left implied,
        // since it is the spelling somebody reaches for when restoring a fade.
        Assert.NotEqual("hidden",
            EffectiveValue(ScreenHalfOfAppCss, ".row-tip", "visibility", exact: true));

        // Escape has to beat hover, or the dismissal is a flag nothing reads.
        Assert.Equal("none", EffectiveValue(
            ScreenHalfOfAppCss, ".option.tip-dismissed:hover .row-tip", "display", exact: true));
        Assert.Equal("none", EffectiveValue(
            ScreenHalfOfAppCss, ".rank-name:hover .row-tip.dismissed", "display", exact: true));
    }

    /// <summary>
    /// <b>The sheet sits beside the editors rather than under them, and stays put while they
    /// scroll.</b>
    ///
    /// <para>Every assertion in <c>PreviewColumnTests</c> passes with this rule deleted — the
    /// component is still rendered, still holds the character, still says everything it says.
    /// It is simply a full-width sheet below a full-width editor, which is the page that existed
    /// before and is not what the second column is for.</para>
    ///
    /// <para><b>One column first, two above a breakpoint</b>, and asserted in that order: a
    /// two-column grid with no narrow fallback is the shape that puts a Power list and a
    /// three-column sheet into 300px each on a laptop.</para>
    /// </summary>
    [Fact]
    public void TheSheetIsAColumnBesideTheEditorsAndStaysWhileTheyScroll()
    {
        // **Outside every media query**, or the wide rule answers for the narrow one: the CSS is
        // read as a flat string here, so `EffectiveValue` takes the last declaration whichever
        // scope it is in — and the last one is `grid`. Asking that way would report the narrow
        // layout as two columns and pass with the fallback deleted.
        var unconditional = string.Join("\n", MediaQueriesOf(ScreenHalfOfAppCss)
            .Aggregate(ScreenHalfOfAppCss, (css, q) => css.Replace(q.Body, " ", StringComparison.Ordinal)));

        Assert.Equal("block",
            EffectiveValue(unconditional, ".with-preview", "display", exact: true));

        // Two columns where there is room. Read out of the wide query rather than off the base
        // rule, which is what makes this a statement about the breakpoint.
        var wide = MediaQueriesOf(ScreenHalfOfAppCss)
            .Where(q => q.Condition.Contains("min-width", StringComparison.Ordinal))
            .Select(q => q.Body)
            .Where(b => b.Contains(".with-preview", StringComparison.Ordinal))
            .ToList();

        Assert.True(wide.Count > 0,
            "Nothing widens .with-preview at any breakpoint, so the sheet never gets a column.");

        var body = string.Join("\n", wide);

        Assert.Contains("grid", body, StringComparison.Ordinal);
        Assert.Equal("sticky", EffectiveValue(body, ".preview", "position", exact: true));
    }

    /// <summary>
    /// <b>The content column widens where the second one appears, and the bands follow it.</b>
    ///
    /// <para>The five bands agree on <c>--column</c> — the shell, the banner, the step list, the
    /// budget strip and the breakdown — and there is a test holding them to that. This is the
    /// other half: that the widening happens on the token rather than on the shell, so the
    /// agreement survives it. A shell widened on its own would leave four bands at the old figure
    /// and the page reading as two columns that nearly line up.</para>
    /// </summary>
    [Fact]
    public void TheColumnWidensOnTheTokenSoEveryBandFollows()
    {

        var widened = MediaQueriesOf(ThemeCss)
            .Where(q => q.Condition.Contains("min-width", StringComparison.Ordinal))
            .Select(q => q.Body)
            .Where(b => b.Contains("--column", StringComparison.Ordinal))
            .ToList();

        Assert.True(widened.Count > 0,
            "--column never widens, so the sheet beside the editors has no room to be a sheet.");

        // And app.css still declares no custom property of its own — the rule that makes a widening
        // here reach every band rather than one of them.
        Assert.DoesNotContain("--column:", ScreenHalfOfAppCss, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The Trait name that carries a description is marked as carrying one.</b>
    ///
    /// <para>A control that looks exactly like plain text is a control nobody finds. The row's
    /// name is a button with no button styling — deliberately, since it is a term and not an
    /// action — so the only thing saying there is something here is the underline.</para>
    /// </summary>
    [Fact]
    public void ATraitNameThatExplainsItselfLooksLikeIt()
    {
        var decoration = EffectiveValue(ScreenHalfOfAppCss, ".trait-term", "text-decoration");

        Assert.NotNull(decoration);
        Assert.Contains("dotted", decoration!, StringComparison.Ordinal);

        // A colour token rather than a named colour, like everything else in this file.
        Assert.Contains("var(--", decoration, StringComparison.Ordinal);
    }

    /// <summary>
    /// The book's own words are marked as somebody else's, in a way that survives both palettes.
    ///
    /// <para><b>Both halves of this were wrong when written, and neither was visible to any
    /// rendering test</b> — the class was on the element either way. The edge was
    /// <c>var(--rule-weight) solid var(--accent)</c>, which is a different width in each palette
    /// (1px Hero, 2px Villain) and, in Hero, gold on a near-white ground: measured at
    /// <b>1.57:1</b>, which is no edge at all. It looked deliberate in the Villain proof, where
    /// the same declaration measured 5.62:1.</para>
    ///
    /// <para>So the weight is a fixed length rather than a token that varies by mode, and the
    /// colour is <c>--heading</c> — 6.76:1 and 5.62:1 on <c>--panel-sunk</c>, both measured. This
    /// pins the decision rather than the numbers, because a contrast figure cannot be read out of
    /// a stylesheet; re-measure if the palette moves.</para>
    /// </summary>
    [Fact]
    public void ThePrintedEntryIsSetApartFromThisProjectsOwnWords()
    {
        var css = ScreenHalfOfAppCss;

        Assert.Equal("var(--panel-sunk)", EffectiveValue(css, ".book-text", "background"));

        var edge = EffectiveValue(css, ".book-text", "border-left");

        Assert.NotNull(edge);

        // Not --rule-weight: it is 1px in one palette and 2px in the other, so one declaration
        // drew two different things.
        Assert.DoesNotContain("--rule-weight", edge, StringComparison.Ordinal);

        // Normalise strips the spaces, so the value reads "3pxsolidvar(--heading)".
        Assert.Matches(Rx(@"^\d+px"), edge!);

        // Not --accent, which is 1.57:1 on this ground in the Hero palette.
        Assert.DoesNotContain("--accent", edge, StringComparison.Ordinal);
        Assert.Contains("var(--heading)", edge, StringComparison.Ordinal);

        // And a zero width is not a visible edge, whatever colour it names.
        Assert.DoesNotMatch(Rx("^0(px)?[a-z]"), edge!);
    }

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

    // ── The four screen palettes' contrast, measured ───────────────────────────────
    //
    // **The print palette had a luminance test and the screen palettes had none**, so every
    // contrast claim in CLAUDE.md about the screen was a number somebody worked out once by
    // hand and wrote down. Two of them are recorded there as *failures* — villain-dark
    // --heading on --accent-soft, and --danger on --danger-soft — and nothing would have
    // noticed if a change made a third.
    //
    // Three things had to be built before any of it could be checked:
    //
    // * **`Luminance` above is not WCAG relative luminance.** It weights the raw channel values
    //   and skips the sRGB gamma linearisation the standard requires, which is fine for the
    //   one-sided "is the paper light / is the ink dark" checks the print test makes and useless
    //   for a ratio. `RelativeLuminance` below does it properly, so the two coexist on purpose.
    // * **`--muted` and `--accent-soft` are `color-mix()`**, not hex, so neither `Luminance` nor
    //   any regex over the file could read them. They were the tokens with the *tightest*
    //   claims on them — --muted carries prose at 0.72rem and is supposed to hold 4.5:1 — and
    //   they were the two nothing could measure. `Resolve` walks `var()` and
    //   `color-mix(in srgb, A n%, B)` down to a triple.
    // * **The palette is no longer one block per mode.** Light and dark are independent of Hero
    //   and Villain, so there are six screen blocks in three shapes — bare, an OS-dark one
    //   guarded by `:not([data-theme="light"])`, and an explicit `[data-theme="dark"]` one —
    //   two of them inside media queries. Reading two blocks by regex cannot answer what any
    //   of the four palettes actually resolves to, so `Palette` below **runs the cascade**:
    //   brace-matched rules in source order, each admitted or refused by its media condition
    //   and by its selector against a document element carrying a given `data-mode` and
    //   `data-theme`.
    //
    // The positive control is `TheContrastInstrumentReproducesTheKnownFailures`, and it is not
    // optional: a resolver that quietly returned null for every mix would make every assertion
    // below pass by measuring nothing.

    /// <summary>WCAG 2.1 relative luminance, linearised per channel as the standard defines.</summary>
    private static double RelativeLuminance((int R, int G, int B) rgb)
    {
        static double Channel(int raw)
        {
            var c = raw / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(rgb.R)) + (0.7152 * Channel(rgb.G)) + (0.0722 * Channel(rgb.B));
    }

    /// <summary>The WCAG contrast ratio between two resolved colours, 1:1 to 21:1.</summary>
    private static double ContrastRatio((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var (high, low) = (RelativeLuminance(a), RelativeLuminance(b));
        if (low > high) (high, low) = (low, high);

        return (high + 0.05) / (low + 0.05);
    }

    /// <summary>
    /// One rule out of a stylesheet: the at-rule conditions enclosing it, its selector list,
    /// and its declarations.
    /// </summary>
    private readonly record struct CssRule(string Media, string Selector, string Body);

    /// <summary>
    /// Every rule in a stylesheet, in source order, each carrying the at-rule conditions it sits
    /// inside.
    ///
    /// <para><b>Brace-matched rather than pattern-matched, for the reason CLAUDE.md gives about
    /// the last scan that was not:</b> a media-query scan that ended at the first newline-brace
    /// could not see a query written on one line and swallowed its contents into the following
    /// block. Nesting here is real — two of the six palettes live inside <c>@media</c> — so the
    /// depth has to be counted rather than assumed.</para>
    /// </summary>
    private static List<CssRule> CssRules(string css)
    {
        var body = WithoutCssComments(css);
        var rules = new List<CssRule>();
        var conditions = new Stack<string>();
        var prelude = new System.Text.StringBuilder();

        for (var i = 0; i < body.Length;)
        {
            var c = body[i];

            if (c == '}')
            {
                if (conditions.Count > 0) conditions.Pop();
                prelude.Clear();
                i++;
                continue;
            }

            if (c != '{')
            {
                prelude.Append(c);
                i++;
                continue;
            }

            var head = Rx(@"\s+").Replace(prelude.ToString(), " ").Trim();
            prelude.Clear();
            i++;

            // An at-rule opens a context its children are read inside; a plain rule is taken
            // whole, to its own matching close.
            if (head.StartsWith('@'))
            {
                conditions.Push(head);
                continue;
            }

            var depth = 1;
            var start = i;

            while (i < body.Length && depth > 0)
            {
                if (body[i] == '{') depth++;
                else if (body[i] == '}') depth--;
                i++;
            }

            rules.Add(new CssRule(string.Join(" ", conditions.Reverse()), head, body[start..(i - 1)]));
        }

        return rules;
    }

    /// <summary>
    /// One theme state a reader can actually be in: the identity, whether they have made an
    /// explicit light/dark choice, and what their system asks for when they have not.
    /// </summary>
    private readonly record struct ThemeState(string Mode, string? Chosen, bool SystemIsDark)
    {
        public override string ToString() =>
            $"{Mode}/{Chosen ?? "system"}/{(SystemIsDark ? "os-dark" : "os-light")}";
    }

    /// <summary>
    /// How strongly a rule applies to a document element in a given state on a given medium, or
    /// null if it does not apply at all.
    ///
    /// <para><b>It returns a specificity rather than a yes, and that is not a refinement — it is
    /// the whole thing this resolver exists to model.</b> The first version answered a bool and
    /// applied the winners in source order, which is what a stylesheet of same-weight rules
    /// resolves to and is <em>wrong here</em>: <c>@media</c> contributes nothing to specificity,
    /// so a dark palette block at (0,3,0) beats the later print block at (0,2,0). That version
    /// was mutation-tested by removing <c>screen</c> from the OS-dark media query — the exact
    /// change that reinstates the near-black printed page — and it passed all 148 tests. A
    /// source-order resolver cannot see a specificity bug, and a specificity bug is the only
    /// kind this arrangement has.</para>
    ///
    /// <para>Counted the way the standard counts it, for the shapes this stylesheet uses: one
    /// for <c>:root</c>, one for each attribute selector, and <c>:not(X)</c> contributing X's.
    /// There are no id or type selectors anywhere in theme.css, so a single number is the whole
    /// of it.</para>
    ///
    /// <para><b>It refuses a selector or a condition it does not model rather than guessing</b>,
    /// the same bargain <c>EffectiveValue</c> makes elsewhere in this file: a resolver that
    /// silently skipped a block it could not read would report a palette that is not the one on
    /// screen, and every ratio measured from it would be fiction.</para>
    /// </summary>
    private static int? Admits(CssRule rule, ThemeState state, bool printing)
    {
        var condition = rule.Media;

        if (condition.Length > 0)
        {
            var screenOnly = condition.Contains("screen", StringComparison.Ordinal);
            var printOnly = condition.Contains("print", StringComparison.Ordinal);
            var systemDark = condition.Contains("prefers-color-scheme: dark", StringComparison.Ordinal);

            // Not a colour question, and no palette declares one. Refused rather than silently
            // admitted, so a colour appearing inside one would show up as an unmodelled shape.
            var motion = condition.Contains("prefers-reduced-motion", StringComparison.Ordinal);

            Assert.True(screenOnly || printOnly || systemDark || motion,
                $"the palette resolver does not model the at-rule `{condition}`.");

            if (motion) return null;
            if (printOnly && !printing) return null;
            if (screenOnly && printing) return null;
            if (systemDark && !state.SystemIsDark) return null;
        }

        // A selector list is weighed selector by selector, so the one that matches most
        // strongly is the rule's weight for this element.
        var weights = rule.Selector
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => Matches(s, state))
            .Select(Specificity)
            .ToList();

        return weights.Count == 0 ? null : weights.Max();

        static int Specificity(string selector) =>
            1 + Rx(@"\[data-(?:mode|theme)=""[a-z]+""\]").Count(selector);

        static bool Matches(string selector, ThemeState state)
        {
            Assert.StartsWith(":root", selector, StringComparison.Ordinal);

            var rest = selector[":root".Length..];
            var attributes = Rx(@"\[data-(mode|theme)=""([a-z]+)""\]");

            // Everything the resolver understands, removed as it is read. Whatever is left
            // over is a shape nobody modelled, and it fails rather than being ignored.
            var negated = Rx(@":not\(\s*(\[data-(?:mode|theme)=""[a-z]+""\])\s*\)").Matches(rest);
            var remainder = Rx(@":not\(\s*\[data-(?:mode|theme)=""[a-z]+""\]\s*\)").Replace(rest, "");
            var required = attributes.Matches(remainder);
            remainder = attributes.Replace(remainder, "").Trim();

            Assert.True(remainder.Length == 0,
                $"the palette resolver does not model the selector `{selector}`.");

            foreach (Match one in required)
                if (Attribute(state, one.Groups[1].Value) != one.Groups[2].Value) return false;

            foreach (Match one in negated)
            {
                var inner = attributes.Match(one.Groups[1].Value);
                if (Attribute(state, inner.Groups[1].Value) == inner.Groups[2].Value) return false;
            }

            return true;
        }

        static string? Attribute(ThemeState state, string name) =>
            name == "mode" ? state.Mode : state.Chosen;
    }

    /// <summary>
    /// The tokens a document element in this state actually resolves to: the cascade run by
    /// specificity first and source order second, which is the order a browser runs it in.
    ///
    /// <para>See <see cref="Admits"/> for why the second half alone is not enough, and for the
    /// mutation that proved it.</para>
    /// </summary>
    private static Dictionary<string, string> Palette(ThemeState state, bool printing = false)
    {
        var winners = new Dictionary<string, (int Weight, int Order, string Value)>(StringComparer.Ordinal);
        var order = 0;

        foreach (var rule in CssRules(ThemeCss))
        {
            var weight = Admits(rule, state, printing);
            order++;

            if (weight is not { } strength) continue;

            foreach (Match declaration in Rx(@"(--[a-z0-9-]+)\s*:\s*([^;]+);").Matches(rule.Body))
            {
                var token = declaration.Groups[1].Value;
                var standing = winners.GetValueOrDefault(token);

                if (winners.ContainsKey(token)
                    && (standing.Weight > strength
                        || (standing.Weight == strength && standing.Order > order))) continue;

                winners[token] = (strength, order, declaration.Groups[2].Value.Trim());
            }
        }

        return winners.ToDictionary(e => e.Key, e => e.Value.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// A token resolved to a colour, following <c>var()</c> and mixing <c>color-mix(in srgb,
    /// A n%, B)</c>. Returns null for anything it cannot model — never a guess, because a
    /// guessed colour is a contrast figure nobody can trust.
    /// </summary>
    private static (int R, int G, int B)? Resolve(
        string token, Dictionary<string, string> palette, HashSet<string>? seen = null)
    {
        seen ??= new HashSet<string>(StringComparer.Ordinal);

        var value = token.StartsWith("--", StringComparison.Ordinal)
            ? palette.GetValueOrDefault(token)
            : token;

        if (value is null) return null;
        value = value.Trim();

        var hex = Rx("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$").Match(value);
        if (hex.Success)
        {
            var digits = hex.Groups[1].Value;
            if (digits.Length == 3) digits = string.Concat(digits.Select(c => new string(c, 2)));

            return (Convert.ToInt32(digits[..2], 16),
                    Convert.ToInt32(digits[2..4], 16),
                    Convert.ToInt32(digits[4..], 16));
        }

        var indirect = Rx(@"^var\(\s*(--[a-z0-9-]+)\s*\)$").Match(value);
        if (indirect.Success)
        {
            // A cycle would otherwise recurse until the stack goes, which a crashed test
            // process reports as a pass — see CLAUDE.md on `Catastrophic failure`.
            if (!seen.Add(indirect.Groups[1].Value)) return null;

            return Resolve(indirect.Groups[1].Value, palette, seen);
        }

        var mix = Rx(@"^color-mix\(\s*in\s+srgb\s*,\s*(.+?)\s+([\d.]+)%\s*,\s*(.+?)\s*\)$").Match(value);
        if (mix.Success)
        {
            var first = Resolve(Inner(mix.Groups[1].Value), palette, new HashSet<string>(seen, StringComparer.Ordinal));
            var second = Resolve(Inner(mix.Groups[3].Value), palette, new HashSet<string>(seen, StringComparer.Ordinal));

            if (first is null || second is null) return null;

            var weight = double.Parse(mix.Groups[2].Value, CultureInfo.InvariantCulture) / 100.0;

            return ((int)Math.Round((first.Value.R * weight) + (second.Value.R * (1 - weight))),
                    (int)Math.Round((first.Value.G * weight) + (second.Value.G * (1 - weight))),
                    (int)Math.Round((first.Value.B * weight) + (second.Value.B * (1 - weight))));
        }

        return null;

        static string Inner(string part)
        {
            var named = Rx(@"var\(\s*(--[a-z0-9-]+)\s*\)").Match(part);
            return named.Success ? named.Groups[1].Value : part.Trim();
        }
    }

    /// <summary>The state a reader is in when they are looking at one of the four palettes.</summary>
    private static ThemeState StateFor(string palette)
    {
        var parts = palette.Split('-');
        return new ThemeState(parts[0], parts[1], SystemIsDark: parts[1] == "dark");
    }

    /// <summary>
    /// <b>The contrast instrument is checked against figures somebody measured by hand.</b>
    ///
    /// <para>CLAUDE.md records two villain-dark pairs as measured failures — <c>--heading</c> on
    /// <c>--accent-soft</c> at 4.08:1 and <c>--danger</c> on <c>--danger-soft</c> at 3.94:1 —
    /// and both are <c>color-mix()</c> grounds, so both are exactly what nothing here could
    /// read before. Reproducing them is what makes every other figure in this region worth
    /// reading.</para>
    ///
    /// <para><b>It anchors on villain-dark specifically, and that is what makes it still a
    /// control after the palette split.</b> Those values are unchanged by this slice, so the
    /// two hand-measured figures still stand — and reaching them now requires the resolver to
    /// run the cascade into a block nested inside a media query, which is the new thing it
    /// does. The light villain palette clears both pairs comfortably, so anchoring there would
    /// have retired the control rather than kept it.</para>
    ///
    /// <para><b>It is a control, not a requirement that they stay bad.</b> If a later change
    /// fixes either pair this test is what should be updated, to whatever the new
    /// instrument-verified figure is. What it must never do is quietly start returning null and
    /// pass.</para>
    /// </summary>
    [Fact]
    public void TheContrastInstrumentReproducesTheKnownFailures()
    {
        var villain = Palette(StateFor("villain-dark"));

        var heading = Resolve("--heading", villain);
        var accentSoft = Resolve("--accent-soft", villain);
        var danger = Resolve("--danger", villain);
        var dangerSoft = Resolve("--danger-soft", villain);

        Assert.True(heading is not null && accentSoft is not null,
            "the resolver cannot read a color-mix() ground any more, so every contrast figure "
            + "in this file is now measuring nothing. Fix Resolve, do not relax this.");
        Assert.True(danger is not null && dangerSoft is not null,
            "--danger / --danger-soft no longer resolve; see above.");

        // Both to one decimal place: the hand-measured figures are 4.08 and 3.94, and a
        // resolver that is right will land within rounding of them rather than exactly on.
        Assert.Equal(4.1, Math.Round(ContrastRatio(heading!.Value, accentSoft!.Value), 1), 1);
        Assert.Equal(3.9, Math.Round(ContrastRatio(danger!.Value, dangerSoft!.Value), 1), 1);
    }

    /// <summary>
    /// <b>Every pair the app actually puts together holds its WCAG floor, in all four
    /// palettes.</b>
    ///
    /// <para>4.5:1 for text, because all of these carry words. <c>--focus</c> is the one 3:1
    /// entry: a focus ring is a non-text indicator under WCAG 1.4.11, and it is a separate
    /// token from <c>--accent</c> precisely because hero-light <c>--accent</c> is 1.8:1 and
    /// invisible as a ring.</para>
    ///
    /// <para><b>The pairs are the ones in use, not every combination.</b> Two tokens can
    /// contrast badly and be perfectly safe if no rule ever puts them together — which is the
    /// situation the two in the control above are in, since the hover grounds moved to
    /// <c>--panel-sunk</c>. Asserting over the cross product would fail on colours nobody can
    /// see at once.</para>
    /// </summary>
    [Theory]
    [InlineData("hero-light")]
    [InlineData("hero-dark")]
    [InlineData("villain-light")]
    [InlineData("villain-dark")]
    public void EveryScreenPairInUseHoldsItsContrastFloor(string mode)
    {
        var palette = Palette(StateFor(mode));

        (string Fg, string Bg, double Floor)[] pairs =
        [
            ("--ink",        "--surface",     4.5),
            ("--ink",        "--panel",       4.5),
            ("--ink",        "--panel-sunk",  4.5),
            ("--muted",      "--panel",       4.5),   // prose at --text-xs; the tightest claim
            ("--muted",      "--surface",     4.5),
            ("--heading",    "--panel",       4.5),
            ("--heading",    "--panel-sunk",  4.5),   // where the hover grounds moved to
            ("--danger",     "--panel",       4.5),
            ("--on-primary", "--primary",     4.5),
            ("--focus",      "--surface",     3.0),   // WCAG 1.4.11, not 1.4.3
        ];

        foreach (var (fg, bg, floor) in pairs)
        {
            var foreground = Resolve(fg, palette);
            var background = Resolve(bg, palette);

            Assert.True(foreground is not null,
                $"{mode}: {fg} does not resolve to a colour, so it is unmeasured rather than passing.");
            Assert.True(background is not null,
                $"{mode}: {bg} does not resolve to a colour, so it is unmeasured rather than passing.");

            var ratio = ContrastRatio(foreground!.Value, background!.Value);

            Assert.True(ratio >= floor,
                $"{mode}: {fg} on {bg} measures {ratio:0.00}:1 and needs {floor:0.0}:1. "
                + "Re-measure with this test rather than adjusting by eye.");
        }
    }

    /// <summary>
    /// <b>All six ways of arriving at a screen land on the palette they are supposed to.</b>
    ///
    /// <para>There are three theme states and not two — an explicit <c>light</c>, an explicit
    /// <c>dark</c>, and no choice at all, where only <c>prefers-color-scheme</c> separates them
    /// — so each identity has six routes in and four of them are the same two palettes reached
    /// differently. The one that is easy to get wrong is <b>an explicit light choice on a dark
    /// system</b>: it is the whole reason the OS block carries
    /// <c>:not([data-theme="light"])</c>, and without that guard the system would beat the
    /// person, silently and only for some readers.</para>
    ///
    /// <para>Asserted on <c>--surface</c> and <c>--heading</c> together rather than on either
    /// alone: the four palettes share no surface and no heading, so a state landing on the
    /// wrong one cannot agree on both by coincidence.</para>
    /// </summary>
    [Theory]
    [InlineData("hero", null, false, "hero-light")]
    [InlineData("hero", null, true, "hero-dark")]
    [InlineData("hero", "light", false, "hero-light")]
    [InlineData("hero", "light", true, "hero-light")]     // the person beats the system
    [InlineData("hero", "dark", false, "hero-dark")]      // ...in both directions
    [InlineData("hero", "dark", true, "hero-dark")]
    [InlineData("villain", null, false, "villain-light")]
    [InlineData("villain", null, true, "villain-dark")]
    [InlineData("villain", "light", false, "villain-light")]
    [InlineData("villain", "light", true, "villain-light")]
    [InlineData("villain", "dark", false, "villain-dark")]
    [InlineData("villain", "dark", true, "villain-dark")]
    public void EveryThemeStateResolvesToTheIntendedPalette(
        string mode, string? chosen, bool systemIsDark, string expected)
    {
        var state = new ThemeState(mode, chosen, systemIsDark);
        var reached = Palette(state);
        var intended = Palette(StateFor(expected));

        foreach (var token in new[] { "--surface", "--heading" })
            Assert.True(
                string.Equals(reached.GetValueOrDefault(token), intended.GetValueOrDefault(token),
                    StringComparison.Ordinal),
                $"{state} resolves {token} to {reached.GetValueOrDefault(token) ?? "nothing"} "
                + $"and should be on {expected}, which is {intended.GetValueOrDefault(token)}.");
    }

    /// <summary>
    /// <b>The two routes into a dark palette declare the same thing.</b>
    ///
    /// <para>A dark set is written twice — once inside <c>@media (prefers-color-scheme: dark)</c>
    /// for a reader who has chosen nothing, once on <c>[data-theme="dark"]</c> for one who has —
    /// because CSS has no way to name a set of declarations and apply it to two selectors when
    /// one of them has to live inside a media query. Duplication is the cost; two places to
    /// drift is the risk, and it would show up only for readers on one of the two routes.</para>
    ///
    /// <para>Compared as declarations rather than as text, so reordering or reformatting one
    /// block is not a failure and changing a value is.</para>
    /// </summary>
    [Theory]
    [InlineData("hero")]
    [InlineData("villain")]
    public void TheTwoRoutesIntoDarkAgree(string mode)
    {
        var bySystem = Declarations(CssRules(ThemeCss).Single(r =>
            r.Media.Contains("prefers-color-scheme: dark", StringComparison.Ordinal)
            && r.Selector.Contains($@"[data-mode=""{mode}""]", StringComparison.Ordinal)));

        var byChoice = Declarations(CssRules(ThemeCss).Single(r =>
            r.Selector.Contains(@"[data-theme=""dark""]", StringComparison.Ordinal)
            && r.Selector.Contains($@"[data-mode=""{mode}""]", StringComparison.Ordinal)));

        Assert.NotEmpty(bySystem);
        Assert.Equal(bySystem, byChoice);

        static Dictionary<string, string> Declarations(CssRule rule) =>
            Rx(@"(--[a-z0-9-]+)\s*:\s*([^;]+);").Matches(rule.Body)
                .ToDictionary(m => m.Groups[1].Value, m => Normalise(m.Groups[2].Value),
                    StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>Every page with a route can be clicked to from somewhere else.</b>
    ///
    /// <para>This is the guard that was missing when the front door was built. The recordings and
    /// the sample characters moved to <c>/admin/portfolio</c>, the banner cross-link that had been
    /// the only way in was replaced by the new navigation in the same change, and nothing was put
    /// back — so the page existed, rendered, was tested, and could not be reached by anybody who
    /// did not already know the address. An audit found it.</para>
    ///
    /// <para><b>No rendering test could have.</b> The suite renders these pages by type and asks
    /// what chrome an address gets once you are already on it; whether a person can get there at
    /// all is a question about every <i>other</i> file. So this reads the routes out of the
    /// <c>page</c> directives and looks for each one in somebody's <c>href</c> — and deliberately
    /// does not count a link a page makes to itself, because a portfolio pointing at its own
    /// sub-pages is exactly how this escaped.</para>
    /// </summary>
    [Fact]
    public void EveryRoutedPageIsReachableFromAnotherPage()
    {
        var pages = Directory.EnumerateFiles(Path.Combine(WebRoot, "Pages"), "*.razor").ToList();
        Assert.True(pages.Count > 5, $"only {pages.Count} pages found; this test is reading nothing.");

        var links = new List<(string File, string Target)>();
        var linkPattern = Rx(@"(?:href=""|NavigateTo\(""|Next=""|Back="")([A-Za-z0-9/_-]*)""");

        foreach (var file in Directory.EnumerateFiles(WebRoot, "*.razor", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(WebRoot, "*.cs", SearchOption.AllDirectories))
                     .Where(NotBuildArtefact))
        {
            foreach (Match m in linkPattern.Matches(File.ReadAllText(file)))
            {
                links.Add((Path.GetFileName(file), m.Groups[1].Value.Trim('/')));
            }
        }

        Assert.True(links.Count > 10,
            $"only {links.Count} links found; the pattern has stopped matching and this test is "
            + "asserting nothing.");

        // Reached by the router rather than by a link, and named here so the exemption is a
        // decision rather than a silence. `App.razor` passes it as `Router`'s `NotFoundPage`, and
        // the file's own comment says so — a page that is *supposed* to have no way in.
        string[] reachedWithoutALink = ["NotFoundPage.razor"];

        var unreachable = new List<string>();

        foreach (var page in pages)
        {
            var name = Path.GetFileName(page);

            if (reachedWithoutALink.Contains(name, StringComparer.Ordinal)) continue;

            var routes = Rx(@"@page ""/([^""]*)""").Matches(File.ReadAllText(page))
                .Select(m => m.Groups[1].Value.Trim('/'))
                // A parameterised route is reached with an id the linking page builds, which this
                // cannot match textually. Its parent listing is checked instead.
                .Where(r => !r.Contains('{', StringComparison.Ordinal))
                .ToList();

            if (routes.Count == 0) continue;

            var reached = routes.Any(route => links.Any(link =>
                string.Equals(link.Target, route, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(link.File, name, StringComparison.Ordinal)));

            if (!reached)
            {
                unreachable.Add($"{name} ({string.Join(", ", routes.Select(r => "/" + r))})");
            }
        }

        Assert.True(unreachable.Count == 0,
            "These pages have a route and nothing outside themselves links to it, so nobody who "
            + "does not already know the address can reach them:\n  "
            + string.Join("\n  ", unreachable));
    }

    /// <summary>Not a build artefact — obj/ and bin/ hold generated copies of every component.</summary>
    private static bool NotBuildArtefact(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
